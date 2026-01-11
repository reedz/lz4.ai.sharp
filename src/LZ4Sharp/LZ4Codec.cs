/*
 * LZ4 - Fast LZ compression algorithm (C# port)
 * Derived from upstream LZ4: https://github.com/lz4/lz4
 *
 * This repository is MIT-licensed; see LICENSE.
 * Third-party BSD-2-Clause notices for upstream: see THIRD-PARTY-NOTICES.md.
 */

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Buffers;

[module: SkipLocalsInit]

namespace LZ4Sharp
{
    /// <summary>
    /// LZ4 Codec - Unsafe optimized implementation for maximum performance
    /// Uses pointer arithmetic to eliminate bounds checking overhead
    /// </summary>
    public static unsafe class LZ4Codec
    {
        // Constants from the C implementation
        private const int MINMATCH = 4;
        private const int LASTLITERALS = 5;
        private const int MFLIMIT = 12; // WILDCOPYLENGTH + MINMATCH
        private const int ML_BITS = 4;
        private const int ML_MASK = (1 << ML_BITS) - 1;
        private const int RUN_BITS = 8 - ML_BITS;
        private const int RUN_MASK = (1 << RUN_BITS) - 1;
        private const int LZ4_SKIP_TRIGGER = 6;
        private const int HASH_LOG = 12; // Standard LZ4 hash log for small inputs
        private const int HASH_LOG_LARGE = 14; // Larger hash log for big inputs (16K entries)
        private const int HASH_SIZE = 1 << HASH_LOG;
        private const int HASH_SIZE_LARGE = 1 << HASH_LOG_LARGE;
        private const int LZ4_DISTANCE_MAX = 65535;
        private const int LZ4_64Klimit = 65536 + MFLIMIT - 1;
        
        // Hash multipliers (same as K4os/original LZ4)
        private const uint HASH_MULT_4 = 2654435761u;
        private const ulong HASH_MULT_5 = 889523592379ul;

        // Thread-local hash tables to avoid allocation overhead
        [ThreadStatic]
        private static uint[]? t_hashTable;
        
        [ThreadStatic]
        private static uint[]? t_hashTableLarge;

        /// <summary>
        /// Gets the maximum compressed size for a given input size
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompressBound(int inputSize)
        {
            return inputSize + (inputSize / 255) + 16;
        }

        /// <summary>
        /// Compress data using LZ4 algorithm with maximum performance (unsafe)
        /// </summary>
        public static int CompressDefault(byte[] source, byte[] destination, int sourceSize, int maxDestinationSize)
        {
            // Prefer HC at the minimum level to improve ratio while keeping CPU cost modest.
            return LZ4HC.CompressHC(source, destination, sourceSize, maxDestinationSize, LZ4HC.CLEVEL_MIN);
        }

        /// <summary>
        /// Compress data using LZ4 algorithm with Span interface (unsafe internally)
        /// </summary>
        public static int CompressDefault(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (source.Length <= 0 || destination.Length <= 0)
                return -1;

            // LZ4HC currently operates on arrays; use pooled buffers to avoid allocations.
            byte[] src = ArrayPool<byte>.Shared.Rent(source.Length);
            byte[] dst = ArrayPool<byte>.Shared.Rent(destination.Length);
            try
            {
                source.CopyTo(src);
                int result = LZ4HC.CompressHC(src, dst, source.Length, destination.Length, LZ4HC.CLEVEL_MIN);
                if (result > 0)
                    dst.AsSpan(0, result).CopyTo(destination);
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(src);
                ArrayPool<byte>.Shared.Return(dst);
            }
        }

        /// <summary>
        /// Core unsafe compression implementation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static int CompressUnsafe(byte* source, byte* dest, int inputSize, int maxOutputSize)
        {
            if (inputSize < MFLIMIT)
            {
                return CompressSmallUnsafe(source, dest, inputSize, maxOutputSize);
            }

            // Optimization 2A: Separate compression paths to eliminate branch in hot loop
            // Use 5-byte hash for larger inputs (>= 64KB) like K4os does
            if (inputSize >= LZ4_64Klimit)
            {
                return CompressLargeInput(source, dest, inputSize, maxOutputSize);
            }
            else
            {
                return CompressMediumInput(source, dest, inputSize, maxOutputSize);
            }
        }

        /// <summary>
        /// Compression for medium inputs (&lt;64KB) using 4-byte hash - no branch in hot loop
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static int CompressMediumInput(byte* source, byte* dest, int inputSize, int maxOutputSize)
        {
            byte* ip = source;
            byte* ibase = source;
            byte* iend = source + inputSize;
            byte* mflimitPlusOne = iend - MFLIMIT + 1;
            byte* matchlimit = iend - LASTLITERALS;

            byte* op = dest;
            byte* olimit = dest + maxOutputSize;

            byte* anchor = source;

            // Use thread-local hash table to avoid allocation overhead
            uint[] hashTableArray = t_hashTable ??= new uint[HASH_SIZE];
            // Optimization 1C: Use Span.Clear which is SIMD-accelerated
            hashTableArray.AsSpan(0, HASH_SIZE).Clear();
            
            fixed (uint* hashTable = hashTableArray)
            {
                // First byte - use Hash4 directly (no branch)
                hashTable[Hash4(ip)] = (uint)(ip - ibase);
                ip++;
                uint forwardH = Hash4(ip);

                // Main loop
                for (;;)
                {
                    byte* match;
                    byte* token;

                        // Find a match
                        {
                            byte* forwardIp = ip;
                            int step = 1;
                            int searchMatchNb = 1 << LZ4_SKIP_TRIGGER;

                            do
                            {
                                uint h = forwardH;
                                ip = forwardIp;
                                forwardIp += step;
                                step = searchMatchNb++ >> LZ4_SKIP_TRIGGER;

                                if (forwardIp > mflimitPlusOne)
                                    goto _last_literals;

                                match = ibase + hashTable[h];
                                
                                // Prefetch next hash entry
                                forwardH = Hash4(forwardIp);
                                if (Sse.IsSupported)
                                {
                                    Sse.Prefetch0(&hashTable[forwardH]);
                                }
                                
                                hashTable[h] = (uint)(ip - ibase);
                            }
                            while ((match + LZ4_DISTANCE_MAX < ip) || (Peek4(match) != Peek4(ip)));
                        }

                        // Catch up: check if we can extend the match backwards
                        while ((ip > anchor) && (match > source) && (ip[-1] == match[-1]))
                        {
                            ip--;
                            match--;
                        }

                        // Encode literals
                        {
                            uint litLength = (uint)(ip - anchor);
                            token = op++;

                            if (op + litLength + (2 + 1 + LASTLITERALS) + (litLength / 255) > olimit)
                                return 0; // Not enough space

                            if (litLength >= RUN_MASK)
                            {
                                int len = (int)(litLength - RUN_MASK);
                                *token = (byte)(RUN_MASK << ML_BITS);
                                // Aggressive optimization: Unroll length encoding
                                while (len >= 4 * 255)
                                {
                                    *(uint*)op = 0xFFFFFFFF;
                                    op += 4;
                                    len -= 4 * 255;
                                }
                                while (len >= 255)
                                {
                                    *op++ = 255;
                                    len -= 255;
                                }
                                *op++ = (byte)len;
                            }
                            else
                            {
                                *token = (byte)(litLength << ML_BITS);
                            }

                            // Copy literals using WildCopy for performance
                            WildCopy8(op, anchor, op + litLength);
                            op += litLength;
                        }

                    _next_match:
                        // Encode offset
                        Poke2(op, (ushort)(ip - match));
                        op += 2;

                        // Encode match length
                        {
                            uint matchCode = LZ4_count(ip + MINMATCH, match + MINMATCH, matchlimit);
                            ip += matchCode + MINMATCH;

                            if (op + (1 + LASTLITERALS) + (matchCode + 240) / 255 > olimit)
                                return 0; // Not enough space

                            if (matchCode >= ML_MASK)
                            {
                                *token += ML_MASK;
                                matchCode -= ML_MASK;
                                // Aggressive optimization: Fast path for match length encoding
                                while (matchCode >= 4 * 255)
                                {
                                    *(uint*)op = 0xFFFFFFFF;
                                    op += 4;
                                    matchCode -= 4 * 255;
                                }
                                while (matchCode >= 255)
                                {
                                    *op++ = 255;
                                    matchCode -= 255;
                                }
                                *op++ = (byte)matchCode;
                            }
                            else
                            {
                                *token += (byte)matchCode;
                            }
                        }

                        anchor = ip;

                        // Test end of chunk
                        if (ip >= mflimitPlusOne)
                            break;

                        // Fill table - use Hash4 directly
                        hashTable[Hash4(ip - 2)] = (uint)(ip - 2 - ibase);

                        // Test next position
                        {
                            uint h = Hash4(ip);
                            match = ibase + hashTable[h];
                            hashTable[h] = (uint)(ip - ibase);

                            if ((match + LZ4_DISTANCE_MAX >= ip) && (Peek4(match) == Peek4(ip)))
                            {
                                token = op++;
                                *token = 0;
                                goto _next_match;
                            }
                        }

                        forwardH = Hash4(++ip);
                    }

                _last_literals:
                    // Encode last literals
                    {
                        uint lastRun = (uint)(iend - anchor);
                        
                        if (op + lastRun + 1 + ((lastRun + 255 - RUN_MASK) / 255) > olimit)
                            return 0; // Not enough space

                        if (lastRun >= RUN_MASK)
                        {
                            uint accumulator = lastRun - RUN_MASK;
                            *op++ = (byte)(RUN_MASK << ML_BITS);
                            while (accumulator >= 255)
                            {
                                *op++ = 255;
                                accumulator -= 255;
                            }
                            *op++ = (byte)accumulator;
                        }
                        else
                        {
                            *op++ = (byte)(lastRun << ML_BITS);
                        }

                        Copy(op, anchor, (int)lastRun);
                        op += lastRun;
                    }

                    return (int)(op - dest);
            }
        }

        /// <summary>
        /// Compression for large inputs (>=64KB) using 5-byte hash with larger hash table
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static int CompressLargeInput(byte* source, byte* dest, int inputSize, int maxOutputSize)
        {
            byte* ip = source;
            byte* ibase = source;
            byte* iend = source + inputSize;
            byte* mflimitPlusOne = iend - MFLIMIT + 1;
            byte* matchlimit = iend - LASTLITERALS;

            byte* op = dest;
            byte* olimit = dest + maxOutputSize;

            byte* anchor = source;

            // Use dedicated large hash table with fast SIMD clear
            uint[] hashTableArray = t_hashTableLarge ??= new uint[HASH_SIZE_LARGE];
            hashTableArray.AsSpan(0, HASH_SIZE_LARGE).Clear();
            
            fixed (uint* hashTable = hashTableArray)
            {
                // First byte - use Hash5Large directly
                hashTable[Hash5Large(ip)] = (uint)(ip - ibase);
                ip++;
                uint forwardH = Hash5Large(ip);

                // Main loop
                for (;;)
                {
                    byte* match;
                    byte* token;

                        // Find a match
                        {
                            byte* forwardIp = ip;
                            int step = 1;
                            int searchMatchNb = 1 << LZ4_SKIP_TRIGGER;

                            do
                            {
                                uint h = forwardH;
                                ip = forwardIp;
                                forwardIp += step;
                                step = searchMatchNb++ >> LZ4_SKIP_TRIGGER;

                                if (forwardIp > mflimitPlusOne)
                                    goto _last_literals;

                                match = ibase + hashTable[h];
                                
                                // Compute next hash
                                forwardH = Hash5Large(forwardIp);
                                if (Sse.IsSupported)
                                {
                                    Sse.Prefetch0(&hashTable[forwardH]);
                                }
                                
                                hashTable[h] = (uint)(ip - ibase);
                            }
                            while ((match + LZ4_DISTANCE_MAX < ip) || (Peek4(match) != Peek4(ip)));
                        }

                        // Catch up: check if we can extend the match backwards
                        while ((ip > anchor) && (match > source) && (ip[-1] == match[-1]))
                        {
                            ip--;
                            match--;
                        }

                        // Encode literals
                        {
                            uint litLength = (uint)(ip - anchor);
                            token = op++;

                            if (op + litLength + (2 + 1 + LASTLITERALS) + (litLength / 255) > olimit)
                                return 0; // Not enough space

                            if (litLength >= RUN_MASK)
                            {
                                int len = (int)(litLength - RUN_MASK);
                                *token = (byte)(RUN_MASK << ML_BITS);
                                // Aggressive optimization: Unroll length encoding for common cases
                                while (len >= 4 * 255)
                                {
                                    *(uint*)op = 0xFFFFFFFF;
                                    op += 4;
                                    len -= 4 * 255;
                                }
                                while (len >= 255)
                                {
                                    *op++ = 255;
                                    len -= 255;
                                }
                                *op++ = (byte)len;
                            }
                            else
                            {
                                *token = (byte)(litLength << ML_BITS);
                            }

                            // Copy literals using WildCopy for performance
                            WildCopy8(op, anchor, op + litLength);
                            op += litLength;
                        }

                    _next_match:
                        // Encode offset
                        Poke2(op, (ushort)(ip - match));
                        op += 2;

                        // Encode match length
                        {
                            uint matchCode = LZ4_count(ip + MINMATCH, match + MINMATCH, matchlimit);
                            ip += matchCode + MINMATCH;

                            if (op + (1 + LASTLITERALS) + (matchCode + 240) / 255 > olimit)
                                return 0; // Not enough space

                            if (matchCode >= ML_MASK)
                            {
                                *token += ML_MASK;
                                matchCode -= ML_MASK;
                                // Aggressive optimization: Fast path for common match lengths
                                while (matchCode >= 4 * 255)
                                {
                                    *(uint*)op = 0xFFFFFFFF;
                                    op += 4;
                                    matchCode -= 4 * 255;
                                }
                                while (matchCode >= 255)
                                {
                                    *op++ = 255;
                                    matchCode -= 255;
                                }
                                *op++ = (byte)matchCode;
                            }
                            else
                            {
                                *token += (byte)matchCode;
                            }
                        }

                        anchor = ip;

                        // Test end of chunk
                        if (ip >= mflimitPlusOne)
                            break;

                        // Fill table - use Hash5Large
                        hashTable[Hash5Large(ip - 2)] = (uint)(ip - 2 - ibase);

                        // Test next position
                        {
                            uint h = Hash5Large(ip);
                            match = ibase + hashTable[h];
                            hashTable[h] = (uint)(ip - ibase);

                            if ((match + LZ4_DISTANCE_MAX >= ip) && (Peek4(match) == Peek4(ip)))
                            {
                                token = op++;
                                *token = 0;
                                goto _next_match;
                            }
                        }

                        forwardH = Hash5Large(++ip);
                    }

                _last_literals:
                    // Encode last literals
                    {
                        uint lastRun = (uint)(iend - anchor);
                        
                        if (op + lastRun + 1 + ((lastRun + 255 - RUN_MASK) / 255) > olimit)
                            return 0; // Not enough space

                        if (lastRun >= RUN_MASK)
                        {
                            uint accumulator = lastRun - RUN_MASK;
                            *op++ = (byte)(RUN_MASK << ML_BITS);
                            while (accumulator >= 255)
                            {
                                *op++ = 255;
                                accumulator -= 255;
                            }
                            *op++ = (byte)accumulator;
                        }
                        else
                        {
                            *op++ = (byte)(lastRun << ML_BITS);
                        }

                        Copy(op, anchor, (int)lastRun);
                        op += lastRun;
                    }

                    return (int)(op - dest);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompressSmallUnsafe(byte* source, byte* dest, int inputSize, int maxOutputSize)
        {
            if (maxOutputSize < inputSize + 1)
                return 0;

            dest[0] = (byte)(inputSize << ML_BITS);
            Buffer.MemoryCopy(source, dest + 1, maxOutputSize - 1, inputSize);
            return inputSize + 1;
        }

        /// <summary>
        /// Decompress LZ4 compressed data safely
        /// </summary>
        public static int DecompressSafe(byte[] source, byte[] destination, int compressedSize, int maxDecompressedSize)
        {
            if (source == null || destination == null || compressedSize < 0 || maxDecompressedSize < 0)
                return -1;

            fixed (byte* srcPtr = source)
            fixed (byte* dstPtr = destination)
            {
                return DecompressUnsafe(srcPtr, dstPtr, compressedSize, maxDecompressedSize);
            }
        }

        /// <summary>
        /// Decompress LZ4 compressed data with Span interface
        /// </summary>
        public static int DecompressSafe(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            fixed (byte* srcPtr = source)
            fixed (byte* dstPtr = destination)
            {
                return DecompressUnsafe(srcPtr, dstPtr, source.Length, destination.Length);
            }
        }

        /// <summary>
        /// Decompress LZ4 compressed data safely with destination offset
        /// </summary>
        /// <param name="source">Compressed source data</param>
        /// <param name="destination">Destination buffer for decompressed data</param>
        /// <param name="compressedSize">Size of compressed data</param>
        /// <param name="maxDecompressedSize">Maximum size of decompressed data</param>
        /// <param name="dstOffset">Offset in destination buffer to start writing</param>
        /// <returns>Size of decompressed data, or negative value on error</returns>
        public static int DecompressSafe(byte[] source, byte[] destination, int compressedSize, int maxDecompressedSize, int dstOffset)
        {
            if (source == null || destination == null || compressedSize < 0 || maxDecompressedSize < 0 || dstOffset < 0)
                return -1;

            if (dstOffset + maxDecompressedSize > destination.Length)
                return -1;

            fixed (byte* srcPtr = source)
            fixed (byte* dstPtr = &destination[dstOffset])
            {
                return DecompressUnsafe(srcPtr, dstPtr, compressedSize, maxDecompressedSize);
            }
        }

        /// <summary>
        /// Core unsafe decompression implementation
        /// Optimized with lookup tables for small offset handling
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static int DecompressUnsafe(byte* source, byte* dest, int compressedSize, int outputSize)
        {
            byte* ip = source;
            byte* iend = source + compressedSize;

            byte* op = dest;
            byte* oend = dest + outputSize;
            byte* cpy;
            byte* match;
            uint offset;

            // Shortcut pointers for fast path
            byte* shortiend = iend - 14 - 2; // 14 = maxLL, 2 = offset
            byte* shortoend = oend - 14 - 18; // 14 = maxLL, 18 = maxML

            while (ip < iend)
            {
                // Get literal length
                uint token = *ip++;
                uint length = token >> ML_BITS;

                // Fast path shortcut for common case
                if (length != RUN_MASK && ip < shortiend && op <= shortoend)
                {
                    // Copy up to 16 literals at once using direct memory operations
                    Poke8(op, Peek8(ip));
                    Poke8(op + 8, Peek8(ip + 8));
                    op += length;
                    ip += length;

                    // Get match info
                    uint matchLen = token & ML_MASK;
                    offset = Peek2(ip);
                    ip += 2;
                    match = op - offset;

                    // Fast path: no overlap, common match length
                    if (matchLen != ML_MASK && offset >= 8)
                    {
                        // Copy 18 bytes inline for speed
                        Poke8(op, Peek8(match));
                        Poke8(op + 8, Peek8(match + 8));
                        Poke2(op + 16, Peek2(match + 16));
                        op += matchLen + MINMATCH;
                        continue;
                    }

                    // Handle variable length match
                    if (matchLen == ML_MASK)
                    {
                        uint s;
                        do
                        {
                            if (ip >= iend) return -1;
                            s = *ip++;
                            matchLen += s;
                        } while (s == 255);
                    }
                    length = matchLen + MINMATCH;

                    // Copy match
                    cpy = op + length;
                    if (cpy > oend)
                        return -1;

                    // Handle copy based on offset
                    if (offset < 8)
                    {
                        // Small offset: use lookup table approach
                        op[0] = match[0];
                        op[1] = match[1];
                        op[2] = match[2];
                        op[3] = match[3];
                        match += Inc32Table[offset];
                        Poke4(op + 4, Peek4(match));
                        match -= Dec64Table[offset];
                        op += 8;

                        // Continue with overlapping copy using 8-byte chunks
                        while (op < cpy)
                        {
                            Poke8(op, Peek8(match));
                            op += 8;
                            match += 8;
                        }
                        op = cpy;
                    }
                    else
                    {
                        // Non-overlapping: copy 8 bytes at a time
                        Poke8(op, Peek8(match));
                        if (length > 8)
                        {
                            Poke8(op + 8, Peek8(match + 8));
                            if (length > 16)
                            {
                                op += 16;
                                match += 16;
                                while (op < cpy)
                                {
                                    Poke8(op, Peek8(match));
                                    op += 8;
                                    match += 8;
                                }
                            }
                        }
                        op = cpy;
                    }
                    continue;
                }

                // Decode literal length (slow path)
                if (length == RUN_MASK)
                {
                    uint s;
                    do
                    {
                        if (ip >= iend) return -1;
                        s = *ip++;
                        length += s;
                    } while (s == 255);
                }

                // Copy literals
                cpy = op + length;
                if (cpy > oend || ip + length > iend)
                    return -1;

                // Copy literals using SIMD - AVX-512 for large copies
                if (length >= 64 && Avx512F.IsSupported)
                {
                    byte* copyEnd = cpy;
                    while (op + 64 <= copyEnd)
                    {
                        Avx512F.Store(op, Avx512F.LoadVector512(ip));
                        op += 64;
                        ip += 64;
                    }
                    while (op + 32 <= copyEnd)
                    {
                        Avx.Store(op, Avx.LoadVector256(ip));
                        op += 32;
                        ip += 32;
                    }
                    while (op < copyEnd)
                    {
                        *op++ = *ip++;
                    }
                    op = copyEnd;
                }
                else if (length >= 16)
                {
                    byte* copyEnd = cpy;
                    do
                    {
                        Poke8(op, Peek8(ip));
                        Poke8(op + 8, Peek8(ip + 8));
                        op += 16;
                        ip += 16;
                    } while (op < copyEnd - 15);
                    
                    // Handle remaining bytes
                    while (op < copyEnd)
                    {
                        *op++ = *ip++;
                    }
                    op = copyEnd;
                }
                else if (length >= 8)
                {
                    Poke8(op, Peek8(ip));
                    if (length > 8)
                    {
                        Poke8(op + 8, Peek8(ip + 8));
                    }
                    ip += length;
                    op = cpy;
                }
                else
                {
                    // Small literal copy
                    Poke8(op, Peek8(ip));
                    ip += length;
                    op = cpy;
                }

                if (ip >= iend)
                    break;

                // Get offset
                offset = Peek2(ip);
                ip += 2;

                if (offset == 0 || offset > (uint)(op - dest))
                    return -1;

                match = op - offset;

                // Get match length
                length = token & ML_MASK;
                if (length == ML_MASK)
                {
                    uint s;
                    do
                    {
                        if (ip >= iend) return -1;
                        s = *ip++;
                        length += s;
                    } while (s == 255);
                }
                length += MINMATCH;

                // Copy match
                cpy = op + length;
                if (cpy > oend)
                    return -1;

                // Handle copy based on offset
                if (offset < 8)
                {
                    // Small offset: use lookup table approach
                    op[0] = match[0];
                    op[1] = match[1];
                    op[2] = match[2];
                    op[3] = match[3];
                    match += Inc32Table[offset];
                    Poke4(op + 4, Peek4(match));
                    match -= Dec64Table[offset];
                    op += 8;

                    // Continue with overlapping copy
                    while (op < cpy)
                    {
                        Poke8(op, Peek8(match));
                        op += 8;
                        match += 8;
                    }
                    op = cpy;
                }
                else
                {
                    // Non-overlapping copy: 8 bytes at a time
                    Poke8(op, Peek8(match));
                    if (length > 8)
                    {
                        Poke8(op + 8, Peek8(match + 8));
                        if (length > 16)
                        {
                            op += 16;
                            match += 16;
                            while (op < cpy)
                            {
                                Poke8(op, Peek8(match));
                                op += 8;
                                match += 8;
                            }
                        }
                    }
                    op = cpy;
                }
            }

            return (int)(op - dest);
        }

        #region Memory operations - pointer-based for maximum performance

        // Lookup tables for small offset copy optimization (from K4os)
        private static readonly int[] Inc32Table = { 0, 1, 2, 1, 0, 4, 4, 4 };
        private static readonly int[] Dec64Table = { 0, 0, 0, -1, -4, 1, 2, 3 };

        /// <summary>
        /// 4-byte hash function (for small inputs &lt; 64KB)
        /// Uses CRC32 when available for better distribution and speed
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash4(byte* p)
        {
            uint v = Peek4(p);
            if (Sse42.IsSupported)
            {
                // CRC32 provides excellent hash distribution and is very fast on modern CPUs
                return Sse42.Crc32(0, v) >> (32 - HASH_LOG);
            }
            return (v * HASH_MULT_4) >> (32 - HASH_LOG);
        }

        /// <summary>
        /// 5-byte hash function (for larger inputs >= 64KB) - provides better distribution
        /// Same algorithm as K4os/original LZ4
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash5(byte* p)
        {
            ulong sequence = Peek8(p);
            return (uint)(unchecked((sequence << 24) * HASH_MULT_5) >> (64 - HASH_LOG));
        }

        /// <summary>
        /// 5-byte hash function with larger hash log for better match finding on large inputs
        /// Uses CRC32 when available for better distribution and speed
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash5Large(byte* p)
        {
            ulong sequence = Peek8(p);
            if (Sse42.X64.IsSupported)
            {
                // CRC32 on 64-bit value for excellent distribution
                return (uint)(Sse42.X64.Crc32(0, sequence) >> (32 - HASH_LOG_LARGE));
            }
            return (uint)(unchecked((sequence << 24) * HASH_MULT_5) >> (64 - HASH_LOG_LARGE));
        }

        /// <summary>
        /// Hash position using appropriate function based on input size
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashPosition(byte* p, bool useLargeHash)
        {
            return useLargeHash ? Hash5(p) : Hash4(p);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PutPosition(byte* p, uint* hashTable, byte* srcBase, bool useLargeHash)
        {
            uint h = HashPosition(p, useLargeHash);
            hashTable[h] = (uint)(p - srcBase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PutPositionOnHash(byte* p, uint h, uint* hashTable, byte* srcBase)
        {
            hashTable[h] = (uint)(p - srcBase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte* GetPositionOnHash(uint h, uint* hashTable, byte* srcBase)
        {
            return srcBase + hashTable[h];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint LZ4_count(byte* pIn, byte* pMatch, byte* pInLimit)
        {
            byte* pStart = pIn;

            if (Vector512.IsHardwareAccelerated && (pIn + 64 <= pInLimit))
            {
                // Vector512 path (AVX-512)
                while (pIn + 64 <= pInLimit)
                {
                    var a = Vector512.Load(pIn);
                    var b = Vector512.Load(pMatch);
                    var eq = Vector512.Equals(a, b);
                    ulong mask = eq.ExtractMostSignificantBits();
                    
                    if (mask != 0xFFFFFFFFFFFFFFFF)
                    {
                        return (uint)(pIn - pStart) + (uint)System.Numerics.BitOperations.TrailingZeroCount(~mask);
                    }
                    
                    pIn += 64;
                    pMatch += 64;
                }
            }
            
            if (Vector256.IsHardwareAccelerated && (pIn + 32 <= pInLimit))
            {
                // Vector256 path (AVX2)
                while (pIn + 32 <= pInLimit)
                {
                    var a = Vector256.Load(pIn);
                    var b = Vector256.Load(pMatch);
                    var eq = Vector256.Equals(a, b);
                    uint mask = eq.ExtractMostSignificantBits();
                    
                    if (mask != 0xFFFFFFFF)
                    {
                        return (uint)(pIn - pStart) + (uint)System.Numerics.BitOperations.TrailingZeroCount(~mask);
                    }
                    
                    pIn += 32;
                    pMatch += 32;
                }
            }

            if (Vector128.IsHardwareAccelerated && (pIn + 16 <= pInLimit))
            {
                // Vector128 path (SSE2/NEON)
                while (pIn + 16 <= pInLimit)
                {
                    var a = Vector128.Load(pIn);
                    var b = Vector128.Load(pMatch);
                    var eq = Vector128.Equals(a, b);
                    uint mask = eq.ExtractMostSignificantBits();
                    
                    if (mask != 0xFFFF)
                    {
                        return (uint)(pIn - pStart) + (uint)System.Numerics.BitOperations.TrailingZeroCount(~mask);
                    }
                    
                    pIn += 16;
                    pMatch += 16;
                }
            }

            // Fast path: compare 8 bytes at a time using XOR
            while (pIn + 8 <= pInLimit)
            {
                ulong diff = Peek8(pIn) ^ Peek8(pMatch);
                if (diff != 0)
                {
                    return (uint)(pIn - pStart) + (uint)(System.Numerics.BitOperations.TrailingZeroCount(diff) >> 3);
                }
                pIn += 8;
                pMatch += 8;
            }

            // Remaining bytes
            while (pIn < pInLimit && *pIn == *pMatch)
            {
                pIn++;
                pMatch++;
            }

            return (uint)(pIn - pStart);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort Peek2(byte* p) => *(ushort*)p;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Poke2(byte* p, ushort v) => *(ushort*)p = v;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Peek4(byte* p) => *(uint*)p;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Poke4(byte* p, uint v) => *(uint*)p = v;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Peek8(byte* p) => *(ulong*)p;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Poke8(byte* p, ulong v) => *(ulong*)p = v;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Copy2(byte* dst, byte* src)
        {
            *(ushort*)dst = *(ushort*)src;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Copy4(byte* dst, byte* src)
        {
            *(uint*)dst = *(uint*)src;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Copy8(byte* dst, byte* src)
        {
            *(ulong*)dst = *(ulong*)src;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Copy16(byte* dst, byte* src)
        {
            *(ulong*)dst = *(ulong*)src;
            *(ulong*)(dst + 8) = *(ulong*)(src + 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Copy18(byte* dst, byte* src)
        {
            Copy16(dst, src);
            Copy2(dst + 16, src + 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Copy(byte* dst, byte* src, int length)
        {
            Buffer.MemoryCopy(src, dst, length, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WildCopy8(byte* dst, byte* src, byte* dstEnd)
        {
            long length = dstEnd - dst;
            
            // Optimization 3A: Inline unroll for common short literal lengths (0-31 bytes)
            if (length <= 8)
            {
                Copy8(dst, src);
                return;
            }
            if (length <= 16)
            {
                Copy8(dst, src);
                Copy8(dst + 8, src + 8);
                return;
            }
            if (length <= 24)
            {
                Copy8(dst, src);
                Copy8(dst + 8, src + 8);
                Copy8(dst + 16, src + 16);
                return;
            }
            if (length <= 32)
            {
                Copy8(dst, src);
                Copy8(dst + 8, src + 8);
                Copy8(dst + 16, src + 16);
                Copy8(dst + 24, src + 24);
                return;
            }
            
            // Use Vector512 (AVX-512)
            if (Vector512.IsHardwareAccelerated)
            {
                while (dst + 64 <= dstEnd)
                {
                    Vector512.Load(src).Store(dst);
                    dst += 64;
                    src += 64;
                }
                if (dst + 32 <= dstEnd)
                {
                    Vector256.Load(src).Store(dst);
                    dst += 32;
                    src += 32;
                }
            }
            // Use Vector256 (AVX2)
            else if (Vector256.IsHardwareAccelerated)
            {
                while (dst + 32 <= dstEnd)
                {
                    Vector256.Load(src).Store(dst);
                    dst += 32;
                    src += 32;
                }
            }
            // Use Vector128 (SSE2/NEON)
            else if (Vector128.IsHardwareAccelerated)
            {
                while (dst + 16 <= dstEnd)
                {
                    Vector128.Load(src).Store(dst);
                    dst += 16;
                    src += 16;
                }
            }
            
            // Handle remaining bytes with 8-byte copies
            while (dst < dstEnd)
            {
                Copy8(dst, src);
                dst += 8;
                src += 8;
            }
        }

        /// <summary>
        /// Copy match with overlap handling using lookup tables (K4os approach)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyMatchWithOffset(byte* op, byte* match, byte* cpy, uint offset)
        {
            if (offset < 8)
            {
                // Small offset: use lookup table approach for overlapping copies
                op[0] = match[0];
                op[1] = match[1];
                op[2] = match[2];
                op[3] = match[3];
                match += Inc32Table[offset];
                Copy4(op + 4, match);
                match -= Dec64Table[offset];
            }
            else
            {
                Copy8(op, match);
                match += 8;
            }
            op += 8;

            // WildCopy remaining
            if (op < cpy)
            {
                WildCopy8(op, match, cpy);
            }
        }

        #endregion
    }
}
