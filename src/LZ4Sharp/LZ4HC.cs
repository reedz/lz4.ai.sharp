/*
 * LZ4 HC - High Compression Mode of LZ4
 * C# Implementation
 * Copyright (c) 2026. Translated from C implementation by Yann Collet.
 * 
 * BSD 2-Clause License (http://www.opensource.org/licenses/bsd-license.php)
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are
 * met:
 * 
 *     * Redistributions of source code must retain the above copyright
 * notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above
 * copyright notice, this list of conditions and the following disclaimer
 * in the documentation and/or other materials provided with the
 * distribution.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
 * OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LZ4Sharp
{
    /// <summary>
    /// LZ4 High Compression (HC) Mode
    /// 
    /// Fully translated from lz4hc.c - provides better compression ratios
    /// at the cost of slower compression speed. Decompression speed remains the same as LZ4.
    /// </summary>
    public static class LZ4HC
    {
        // Thread-local context to avoid per-call allocations
        [ThreadStatic]
        private static HCContext? t_context;

        private static readonly bool s_isLittleEndian = BitConverter.IsLittleEndian;

        // Constants
        private const bool PreferCrc32Hash = false;

        private const int MINMATCH = 4;
        private const int WILDCOPYLENGTH = 8;
        private const int LASTLITERALS = 5;
        private const int MFLIMIT = WILDCOPYLENGTH + MINMATCH;
        private const int ML_BITS = 4;
        private const int ML_MASK = (1 << ML_BITS) - 1;
        private const int RUN_BITS = 8 - ML_BITS;
        private const int RUN_MASK = (1 << RUN_BITS) - 1;
        private const int LZ4_DISTANCE_MAX = 65535;
        private const int LZ4HC_HASH_LOG = 15;
        private const int LZ4HC_HASHTABLESIZE = 1 << LZ4HC_HASH_LOG;
        private const int LZ4HC_MAXD = 1 << 16;
        private const int OPTIMAL_ML = ((ML_MASK - 1) + MINMATCH);

        // Compression levels
        public const int CLEVEL_MIN = 3;
        public const int CLEVEL_DEFAULT = 9;
        public const int CLEVEL_OPT_MIN = 10;
        public const int CLEVEL_MAX = 12;

        // Compression parameters for each level
        private struct CompressionParams
        {
            public int NbSearches;
            public int TargetLength;

            public CompressionParams(int nbSearches, int targetLength)
            {
                NbSearches = nbSearches;
                TargetLength = targetLength;
            }
        }

        private static readonly CompressionParams[] LevelParams = new CompressionParams[]
        {
            new CompressionParams(2, 16),    // 0 (unused)
            new CompressionParams(2, 16),    // 1 (unused)
            new CompressionParams(2, 16),    // 2
            new CompressionParams(4, 16),    // 3
            new CompressionParams(8, 16),    // 4
            new CompressionParams(16, 16),   // 5
            new CompressionParams(32, 16),   // 6
            new CompressionParams(64, 16),   // 7
            new CompressionParams(128, 16),  // 8
            new CompressionParams(256, 16),  // 9
            new CompressionParams(512, 16),  // 10
            new CompressionParams(1024, 16), // 11
            new CompressionParams(2048, OPTIMAL_ML), // 12
        };

        /// <summary>
        /// Compress data using LZ4 High Compression mode
        /// </summary>
        /// <param name="source">Source data to compress</param>
        /// <param name="destination">Destination buffer for compressed data</param>
        /// <param name="sourceSize">Size of source data</param>
        /// <param name="maxDestinationSize">Maximum size of destination buffer</param>
        /// <param name="compressionLevel">Compression level (3-12, default 9)</param>
        /// <returns>Size of compressed data, or negative value on error</returns>
        public static int CompressHC(byte[] source, byte[] destination, int sourceSize, int maxDestinationSize, int compressionLevel = CLEVEL_DEFAULT)
        {
            if (source == null || destination == null || sourceSize <= 0 || maxDestinationSize <= 0)
                return -1;

            if (compressionLevel < CLEVEL_MIN) compressionLevel = CLEVEL_MIN;
            if (compressionLevel > CLEVEL_MAX) compressionLevel = CLEVEL_MAX;

            var ctx = t_context ??= new HCContext();
            return CompressHCInternal(ctx, source, destination, sourceSize, maxDestinationSize, compressionLevel);
        }

        /// <summary>
        /// Get the maximum compressed size for a given input size
        /// </summary>
        public static int CompressBound(int inputSize)
        {
            return LZ4Codec.CompressBound(inputSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteLen255(byte[] destination, ref int dstPos, int len)
        {
            while (len >= 4 * 255)
            {
                Unsafe.WriteUnaligned(ref destination[dstPos], 0xFFFFFFFFu);
                dstPos += 4;
                len -= 4 * 255;
            }
            while (len >= 255)
            {
                destination[dstPos++] = 255;
                len -= 255;
            }
            destination[dstPos++] = (byte)len;
        }

        // Internal context for HC compression
        private struct HashEntry
        {
            public uint Pos;
            public ushort Tag;
        }

        private class HCContext
        {
            public HashEntry[] HashTable = new HashEntry[LZ4HC_HASHTABLESIZE];
            public ushort CurrentTag;

            public ushort[] ChainTable = new ushort[LZ4HC_MAXD];
            public uint NextToUpdate;
        }

        private static int CompressHCInternal(HCContext ctx, byte[] source, byte[] destination, int sourceSize, int maxDestinationSize, int compressionLevel)
        {
            var cParams = LevelParams[compressionLevel];
            int srcPos = 0;
            int dstPos = 0;
            int anchor = 0;
            int srcEnd = sourceSize;
            int srcLimit = sourceSize - MFLIMIT;
            int dstEnd = maxDestinationSize;

            // Reset state (avoid full table clears)
            ctx.CurrentTag++;
            if (ctx.CurrentTag == 0)
            {
                Array.Clear(ctx.HashTable, 0, ctx.HashTable.Length);
                ctx.CurrentTag = 1;
            }
            ctx.NextToUpdate = 0;

            srcPos++;

            unsafe
            {
                fixed (byte* srcBase = source)
                fixed (HashEntry* hashTable = ctx.HashTable)
                fixed (ushort* chainTable = ctx.ChainTable)
                {
                    // Main loop
                    while (srcPos < srcLimit)
                    {
                        // Find match
                        var match = FindBestMatch(ctx, srcBase, hashTable, chainTable, srcPos, srcEnd, cParams.NbSearches, cParams.TargetLength, srcPos - LZ4_DISTANCE_MAX);

                        if (match.Length < MINMATCH)
                        {
                            srcPos++;
                            continue;
                        }

                        // Encode sequence
                        int litLength = srcPos - anchor;
                        int matchLength = match.Length;
                        int offset = srcPos - match.Position;

                        // Check output buffer space
                        int tokenPos = dstPos++;
                        if (dstPos + litLength / 255 + litLength + 2 + matchLength / 255 + LASTLITERALS > dstEnd)
                            return 0;

                        // Encode literal length
                        if (litLength >= RUN_MASK)
                        {
                            destination[tokenPos] = (byte)(RUN_MASK << ML_BITS);
                            WriteLen255(destination, ref dstPos, litLength - RUN_MASK);
                        }
                        else
                        {
                            destination[tokenPos] = (byte)(litLength << ML_BITS);
                        }

                        // Copy literals
                        if (litLength != 0)
                        {
                            Unsafe.CopyBlockUnaligned(
                                ref destination[dstPos],
                                ref source[anchor],
                                (uint)litLength);
                            dstPos += litLength;
                        }

                        // Encode offset (little-endian)
                        if (s_isLittleEndian)
                            Unsafe.WriteUnaligned(ref destination[dstPos], (ushort)offset);
                        else
                        {
                            destination[dstPos] = (byte)offset;
                            destination[dstPos + 1] = (byte)(offset >> 8);
                        }
                        dstPos += 2;

                        // Encode match length
                        int mlCode = matchLength - MINMATCH;
                        if (mlCode >= ML_MASK)
                        {
                            destination[tokenPos] += ML_MASK;
                            WriteLen255(destination, ref dstPos, mlCode - ML_MASK);
                        }
                        else
                        {
                            destination[tokenPos] += (byte)mlCode;
                        }

                        // Move forward
                        srcPos += matchLength;
                        anchor = srcPos;
                    }
                }
            }

            // Encode last literals
            int lastLiterals = srcEnd - anchor;
            if (dstPos + lastLiterals / 255 + lastLiterals + 1 > dstEnd)
                return 0;

            if (lastLiterals >= RUN_MASK)
            {
                destination[dstPos++] = (byte)(RUN_MASK << ML_BITS);
                WriteLen255(destination, ref dstPos, lastLiterals - RUN_MASK);
            }
            else
            {
                destination[dstPos++] = (byte)(lastLiterals << ML_BITS);
            }

            // Copy last literals
            if (lastLiterals != 0)
            {
                Unsafe.CopyBlockUnaligned(
                    ref destination[dstPos],
                    ref source[anchor],
                    (uint)lastLiterals);
                dstPos += lastLiterals;
            }

            return dstPos;
        }

        private struct MatchInfo
        {
            public int Position;
            public int Length;

            public MatchInfo(int position, int length)
            {
                Position = position;
                Length = length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint HashPointer(byte* p)
        {
            uint v = Unsafe.ReadUnaligned<uint>(p);

            if (PreferCrc32Hash && Sse42.IsSupported)
                return Sse42.Crc32(0u, v) >> (32 - LZ4HC_HASH_LOG);

            return (v * 2654435761u) >> (32 - LZ4HC_HASH_LOG);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int CountCommonBytes(byte* p1, byte* p2, byte* end)
        {
            byte* start = p1;
            if (p1 >= end) return 0;

            if (Vector256.IsHardwareAccelerated && p1 + 32 <= end)
            {
                while (p1 + 32 <= end)
                {
                    var v1 = Vector256.Load(p1);
                    var v2 = Vector256.Load(p2);
                    var eq = Vector256.Equals(v1, v2);
                    uint mask = eq.ExtractMostSignificantBits();

                    if (mask != 0xFFFFFFFF)
                    {
                        return (int)(p1 - start) + BitOperations.TrailingZeroCount(~mask);
                    }

                    p1 += 32;
                    p2 += 32;
                }
            }
            else if (Vector128.IsHardwareAccelerated && p1 + 16 <= end)
            {
                while (p1 + 16 <= end)
                {
                    var v1 = Vector128.Load(p1);
                    var v2 = Vector128.Load(p2);
                    var eq = Vector128.Equals(v1, v2);
                    uint mask = eq.ExtractMostSignificantBits();

                    if (mask != 0xFFFF)
                    {
                        return (int)(p1 - start) + BitOperations.TrailingZeroCount(~mask);
                    }

                    p1 += 16;
                    p2 += 16;
                }
            }

            while (p1 + sizeof(ulong) <= end)
            {
                ulong diff = Unsafe.ReadUnaligned<ulong>(p1) ^ Unsafe.ReadUnaligned<ulong>(p2);
                if (diff == 0)
                {
                    p1 += sizeof(ulong);
                    p2 += sizeof(ulong);
                    continue;
                }

                return (int)(p1 - start) + (BitOperations.TrailingZeroCount(diff) >> 3);
            }

            while (p1 + sizeof(uint) <= end)
            {
                uint diff = Unsafe.ReadUnaligned<uint>(p1) ^ Unsafe.ReadUnaligned<uint>(p2);
                if (diff == 0)
                {
                    p1 += sizeof(uint);
                    p2 += sizeof(uint);
                    continue;
                }

                return (int)(p1 - start) + (BitOperations.TrailingZeroCount(diff) >> 3);
            }

            while (p1 < end && *p1 == *p2)
            {
                p1++;
                p2++;
            }

            return (int)(p1 - start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void InsertAndUpdate(
            HCContext ctx,
            byte* srcBase,
            HashEntry* hashTable,
            ushort* chainTable,
            uint target)
        {
            const uint chainMask = LZ4HC_MAXD - 1;
            ushort currentTag = ctx.CurrentTag;

            uint next = ctx.NextToUpdate;
            while (next < target)
            {
                uint h = HashPointer(srcBase + next);
                int hi = (int)h;

                uint prev = hashTable[hi].Tag == currentTag ? hashTable[hi].Pos : 0;
                int delta = (int)(next - prev);
                if (delta > LZ4_DISTANCE_MAX) delta = LZ4_DISTANCE_MAX;

                chainTable[next & chainMask] = (ushort)delta;
                hashTable[hi].Pos = next;
                hashTable[hi].Tag = currentTag;

                next++;
            }

            ctx.NextToUpdate = next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool CheckMatch(
            byte* matchPtr, byte* srcPtr, byte* srcEndPtr, uint src4,
            ref int bestLength, ref int bestPosition, int matchPos, int targetLength)
        {
            if (Unsafe.ReadUnaligned<uint>(matchPtr) == src4)
            {
                int matchLength = MINMATCH + CountCommonBytes(matchPtr + MINMATCH, srcPtr + MINMATCH, srcEndPtr);
                if (matchLength > bestLength)
                {
                    bestLength = matchLength;
                    bestPosition = matchPos;
                    if (matchLength >= targetLength)
                        return true;
                }
            }
            return false;
        }

        private static unsafe MatchInfo FindBestMatch(
            HCContext ctx,
            byte* srcBase,
            HashEntry* hashTable,
            ushort* chainTable,
            int srcPos,
            int srcEnd,
            int maxAttempts,
            int targetLength,
            int lowestMatchPos)
        {
            if (lowestMatchPos < 0) lowestMatchPos = 0;

            const uint chainMask = LZ4HC_MAXD - 1;

            int bestLength = 0;
            int bestPosition = 0;
            int attempts = maxAttempts;

            // Update tables up to current position
            InsertAndUpdate(ctx, srcBase, hashTable, chainTable, (uint)srcPos);

            ushort currentTag = ctx.CurrentTag;

            uint h = HashPointer(srcBase + srcPos);
            int hi = (int)h;
            uint matchPos = hashTable[hi].Tag == currentTag ? hashTable[hi].Pos : 0;

            byte* srcPtr = srcBase + srcPos;
            uint src4 = Unsafe.ReadUnaligned<uint>(srcPtr);
            byte* srcEndPtr = srcBase + srcEnd;

            uint lowest = (uint)lowestMatchPos;
            uint distanceLimitPos = srcPos > LZ4_DISTANCE_MAX ? (uint)(srcPos - LZ4_DISTANCE_MAX) : 0;
            if (distanceLimitPos > lowest) lowest = distanceLimitPos;

            byte* matchPtr = srcBase + matchPos;
            
            while (attempts >= 4)
            {
                if (matchPos < lowest) goto Result;
                if (CheckMatch(matchPtr, srcPtr, srcEndPtr, src4, ref bestLength, ref bestPosition, (int)matchPos, targetLength)) goto Result;
                
                uint delta = chainTable[matchPos & chainMask];
                if (delta == 0) goto Result;
                matchPos -= delta;
                matchPtr -= delta;

                if (matchPos < lowest) goto Result;
                if (CheckMatch(matchPtr, srcPtr, srcEndPtr, src4, ref bestLength, ref bestPosition, (int)matchPos, targetLength)) goto Result;

                delta = chainTable[matchPos & chainMask];
                if (delta == 0) goto Result;
                matchPos -= delta;
                matchPtr -= delta;

                if (matchPos < lowest) goto Result;
                if (CheckMatch(matchPtr, srcPtr, srcEndPtr, src4, ref bestLength, ref bestPosition, (int)matchPos, targetLength)) goto Result;

                delta = chainTable[matchPos & chainMask];
                if (delta == 0) goto Result;
                matchPos -= delta;
                matchPtr -= delta;

                if (matchPos < lowest) goto Result;
                if (CheckMatch(matchPtr, srcPtr, srcEndPtr, src4, ref bestLength, ref bestPosition, (int)matchPos, targetLength)) goto Result;

                delta = chainTable[matchPos & chainMask];
                if (delta == 0) goto Result;
                matchPos -= delta;
                matchPtr -= delta;

                attempts -= 4;
            }

            while (matchPos >= lowest && attempts > 0)
            {
                attempts--;
                if (CheckMatch(matchPtr, srcPtr, srcEndPtr, src4, ref bestLength, ref bestPosition, (int)matchPos, targetLength)) goto Result;

                uint delta = chainTable[matchPos & chainMask];
                if (delta == 0) break;
                matchPos -= delta;
                matchPtr -= delta;
            }

        Result:
            return new MatchInfo(bestPosition, bestLength);
        }
    }
}
