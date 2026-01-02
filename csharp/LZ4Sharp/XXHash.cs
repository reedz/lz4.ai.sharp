/*
 * XXHash - Extremely Fast Hash algorithm
 * C# Implementation
 * Copyright (c) 2026. Translated from C implementation by Yann Collet.
 * 
 * Original C implementation:
 * Copyright (C) 2012-2016, Yann Collet
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
    /// XXHash - Extremely Fast Hash algorithm
    /// 
    /// XXHash is an extremely fast hash algorithm, running at RAM speed limits.
    /// It successfully passes all tests from the SMHasher suite.
    /// 
    /// This implementation provides XXH32 (32-bit hash) functionality.
    /// </summary>
    public static class XXHash
    {
        // Prime numbers used in XXH32 algorithm
        private const uint PRIME32_1 = 2654435761U;
        private const uint PRIME32_2 = 2246822519U;
        private const uint PRIME32_3 = 3266489917U;
        private const uint PRIME32_4 = 668265263U;
        private const uint PRIME32_5 = 374761393U;

        /// <summary>
        /// Calculate the 32-bit hash of a byte array
        /// </summary>
        /// <param name="input">Input data to hash</param>
        /// <param name="length">Length of input data</param>
        /// <param name="seed">Seed value (can be used to alter the result predictably)</param>
        /// <returns>32-bit hash value</returns>
        public static uint XXH32(byte[] input, int length, uint seed)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (length < 0 || length > input.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            return XXH32_Internal(input, 0, length, seed);
        }

        /// <summary>
        /// Calculate the 32-bit hash of a byte array
        /// </summary>
        /// <param name="input">Input data to hash</param>
        /// <param name="seed">Seed value</param>
        /// <returns>32-bit hash value</returns>
        public static uint XXH32(byte[] input, uint seed = 0)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            return XXH32_Internal(input, 0, input.Length, seed);
        }

        /// <summary>
        /// Internal XXH32 implementation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint XXH32_Internal(byte[] input, int offset, int length, uint seed)
        {
            int p = offset;
            int bEnd = p + length;
            uint h32;

            if (length >= 16)
            {
                int limit = bEnd - 15;
                uint v1 = seed + PRIME32_1 + PRIME32_2;
                uint v2 = seed + PRIME32_2;
                uint v3 = seed + 0;
                uint v4 = seed - PRIME32_1;

                do
                {
                    v1 = XXH32_Round(v1, ReadUInt32LE(input, p)); p += 4;
                    v2 = XXH32_Round(v2, ReadUInt32LE(input, p)); p += 4;
                    v3 = XXH32_Round(v3, ReadUInt32LE(input, p)); p += 4;
                    v4 = XXH32_Round(v4, ReadUInt32LE(input, p)); p += 4;
                } while (p < limit);

                h32 = RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
            }
            else
            {
                h32 = seed + PRIME32_5;
            }

            h32 += (uint)length;

            return XXH32_Finalize(h32, input, p, length & 15);
        }

        /// <summary>
        /// Round function for XXH32
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint XXH32_Round(uint seed, uint input)
        {
            seed += input * PRIME32_2;
            seed = RotateLeft(seed, 13);
            seed *= PRIME32_1;
            return seed;
        }

        /// <summary>
        /// Avalanche function - mix all bits
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint XXH32_Avalanche(uint h32)
        {
            h32 ^= h32 >> 15;
            h32 *= PRIME32_2;
            h32 ^= h32 >> 13;
            h32 *= PRIME32_3;
            h32 ^= h32 >> 16;
            return h32;
        }

        /// <summary>
        /// Finalize the hash with remaining bytes
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint XXH32_Finalize(uint h32, byte[] input, int p, int len)
        {
            while (len >= 4)
            {
                h32 += ReadUInt32LE(input, p) * PRIME32_3;
                p += 4;
                h32 = RotateLeft(h32, 17) * PRIME32_4;
                len -= 4;
            }

            while (len > 0)
            {
                h32 += input[p] * PRIME32_5;
                p++;
                h32 = RotateLeft(h32, 11) * PRIME32_1;
                len--;
            }

            return XXH32_Avalanche(h32);
        }

        /// <summary>
        /// Read a 32-bit little-endian unsigned integer from a byte array
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUInt32LE(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24));
        }

        /// <summary>
        /// Rotate left operation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }

        /// <summary>
        /// XXH32 State for streaming hash computation
        /// </summary>
        public class XXH32State
        {
            private uint _totalLength;
            private uint _v1;
            private uint _v2;
            private uint _v3;
            private uint _v4;
            private byte[] _memory;
            private int _memorySize;

            public XXH32State(uint seed = 0)
            {
                _memory = new byte[16];
                Reset(seed);
            }

            /// <summary>
            /// Reset the state with a new seed
            /// </summary>
            public void Reset(uint seed)
            {
                _totalLength = 0;
                _v1 = seed + PRIME32_1 + PRIME32_2;
                _v2 = seed + PRIME32_2;
                _v3 = seed + 0;
                _v4 = seed - PRIME32_1;
                _memorySize = 0;
            }

            /// <summary>
            /// Update the hash with more data
            /// </summary>
            public void Update(byte[] input, int length)
            {
                if (input == null)
                    throw new ArgumentNullException(nameof(input));
                if (length < 0 || length > input.Length)
                    throw new ArgumentOutOfRangeException(nameof(length));

                Update(input, 0, length);
            }

            /// <summary>
            /// Update the hash with more data
            /// </summary>
            public void Update(byte[] input, int offset, int length)
            {
                if (input == null)
                    throw new ArgumentNullException(nameof(input));

                _totalLength += (uint)length;

                int p = offset;
                int bEnd = offset + length;

                // If we have data in memory, try to fill it
                if (_memorySize + length < 16)
                {
                    // Not enough to process, just store
                    Array.Copy(input, offset, _memory, _memorySize, length);
                    _memorySize += length;
                    return;
                }

                // Process data from memory if any
                if (_memorySize > 0)
                {
                    int fillLength = 16 - _memorySize;
                    Array.Copy(input, offset, _memory, _memorySize, fillLength);

                    _v1 = XXH32_Round(_v1, ReadUInt32LE(_memory, 0));
                    _v2 = XXH32_Round(_v2, ReadUInt32LE(_memory, 4));
                    _v3 = XXH32_Round(_v3, ReadUInt32LE(_memory, 8));
                    _v4 = XXH32_Round(_v4, ReadUInt32LE(_memory, 12));

                    p += fillLength;
                    _memorySize = 0;
                }

                // Process 16-byte blocks
                int limit = bEnd - 16;
                while (p <= limit)
                {
                    _v1 = XXH32_Round(_v1, ReadUInt32LE(input, p)); p += 4;
                    _v2 = XXH32_Round(_v2, ReadUInt32LE(input, p)); p += 4;
                    _v3 = XXH32_Round(_v3, ReadUInt32LE(input, p)); p += 4;
                    _v4 = XXH32_Round(_v4, ReadUInt32LE(input, p)); p += 4;
                }

                // Store remaining bytes
                if (p < bEnd)
                {
                    Array.Copy(input, p, _memory, 0, bEnd - p);
                    _memorySize = bEnd - p;
                }
            }

            /// <summary>
            /// Get the final hash value
            /// </summary>
            public uint Digest()
            {
                uint h32;

                if (_totalLength >= 16)
                {
                    h32 = RotateLeft(_v1, 1) + RotateLeft(_v2, 7) + RotateLeft(_v3, 12) + RotateLeft(_v4, 18);
                }
                else
                {
                    h32 = _v3 /*seed*/ + PRIME32_5;
                }

                h32 += _totalLength;

                return XXH32_Finalize(h32, _memory, 0, _memorySize);
            }
        }
    }
}
