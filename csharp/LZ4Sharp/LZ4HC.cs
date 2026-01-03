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
using System.Runtime.CompilerServices;

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
        // Constants
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

            var ctx = new HCContext();
            return CompressHCInternal(ctx, source, destination, sourceSize, maxDestinationSize, compressionLevel);
        }

        /// <summary>
        /// Get the maximum compressed size for a given input size
        /// </summary>
        public static int CompressBound(int inputSize)
        {
            return LZ4Codec.CompressBound(inputSize);
        }

        // Internal context for HC compression
        private class HCContext
        {
            public uint[] HashTable = new uint[LZ4HC_HASHTABLESIZE];
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

            if (sourceSize > LZ4Codec.CompressBound(sourceSize))
                return 0;

            // Initialize hash table
            Array.Clear(ctx.HashTable, 0, ctx.HashTable.Length);
            Array.Clear(ctx.ChainTable, 0, ctx.ChainTable.Length);
            ctx.NextToUpdate = 0;

            srcPos++;

            // Main loop
            while (srcPos < srcLimit)
            {
                // Find match
                var match = FindBestMatch(ctx, source, srcPos, srcEnd, cParams.NbSearches, srcPos - LZ4_DISTANCE_MAX);
                
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
                    int len = litLength - RUN_MASK;
                    while (len >= 255)
                    {
                        destination[dstPos++] = 255;
                        len -= 255;
                    }
                    destination[dstPos++] = (byte)len;
                }
                else
                {
                    destination[tokenPos] = (byte)(litLength << ML_BITS);
                }

                // Copy literals
                for (int i = 0; i < litLength; i++)
                {
                    destination[dstPos++] = source[anchor + i];
                }

                // Encode offset (little-endian)
                destination[dstPos++] = (byte)offset;
                destination[dstPos++] = (byte)(offset >> 8);

                // Encode match length
                int mlCode = matchLength - MINMATCH;
                if (mlCode >= ML_MASK)
                {
                    destination[tokenPos] += ML_MASK;
                    mlCode -= ML_MASK;
                    while (mlCode >= 255)
                    {
                        destination[dstPos++] = 255;
                        mlCode -= 255;
                    }
                    destination[dstPos++] = (byte)mlCode;
                }
                else
                {
                    destination[tokenPos] += (byte)mlCode;
                }

                // Move forward
                srcPos += matchLength;
                anchor = srcPos;
            }

            // Encode last literals
            int lastLiterals = srcEnd - anchor;
            if (dstPos + lastLiterals / 255 + lastLiterals + 1 > dstEnd)
                return 0;

            if (lastLiterals >= RUN_MASK)
            {
                destination[dstPos++] = (byte)(RUN_MASK << ML_BITS);
                int len = lastLiterals - RUN_MASK;
                while (len >= 255)
                {
                    destination[dstPos++] = 255;
                    len -= 255;
                }
                destination[dstPos++] = (byte)len;
            }
            else
            {
                destination[dstPos++] = (byte)(lastLiterals << ML_BITS);
            }

            // Copy last literals
            for (int i = 0; i < lastLiterals; i++)
            {
                destination[dstPos++] = source[anchor + i];
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
        private static uint HashPointer(byte[] data, int pos)
        {
            uint value = (uint)(data[pos] | (data[pos + 1] << 8) | (data[pos + 2] << 16) | (data[pos + 3] << 24));
            return (value * 2654435761U) >> (32 - LZ4HC_HASH_LOG);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountCommonBytes(byte[] src1, int pos1, byte[] src2, int pos2, int maxCount)
        {
            int count = 0;
            while (count < maxCount && src1[pos1 + count] == src2[pos2 + count])
            {
                count++;
            }
            return count;
        }

        private static void InsertAndUpdate(HCContext ctx, byte[] source, int position, int target)
        {
            while (ctx.NextToUpdate < target)
            {
                uint h = HashPointer(source, (int)ctx.NextToUpdate);
                int delta = (int)(ctx.NextToUpdate - ctx.HashTable[h]);
                if (delta > LZ4_DISTANCE_MAX) delta = LZ4_DISTANCE_MAX;
                ctx.ChainTable[ctx.NextToUpdate & (LZ4HC_MAXD - 1)] = (ushort)delta;
                ctx.HashTable[h] = ctx.NextToUpdate;
                ctx.NextToUpdate++;
            }
        }

        private static MatchInfo FindBestMatch(HCContext ctx, byte[] source, int srcPos, int srcEnd, int maxAttempts, int lowestMatchPos)
        {
            if (lowestMatchPos < 0) lowestMatchPos = 0;

            InsertAndUpdate(ctx, source, srcPos, srcPos);

            uint h = HashPointer(source, srcPos);
            uint matchPos = ctx.HashTable[h];
            
            int bestLength = 0;
            int bestPosition = 0;
            int attempts = maxAttempts;

            while (matchPos >= lowestMatchPos && attempts-- > 0)
            {
                if (srcPos - matchPos > LZ4_DISTANCE_MAX)
                    break;

                // Check if first 4 bytes match
                if (source[matchPos] == source[srcPos] &&
                    source[matchPos + 1] == source[srcPos + 1] &&
                    source[matchPos + 2] == source[srcPos + 2] &&
                    source[matchPos + 3] == source[srcPos + 3])
                {
                    int matchLength = MINMATCH + CountCommonBytes(source, (int)matchPos + MINMATCH, source, srcPos + MINMATCH, srcEnd - srcPos - MINMATCH);
                    
                    if (matchLength > bestLength)
                    {
                        bestLength = matchLength;
                        bestPosition = (int)matchPos;
                    }
                }

                // Follow chain
                int delta = ctx.ChainTable[matchPos & (LZ4HC_MAXD - 1)];
                if (delta == 0) break;
                matchPos -= (uint)delta;
            }

            return new MatchInfo(bestPosition, bestLength);
        }
    }
}
