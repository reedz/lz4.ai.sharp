/*
 * LZ4 - Fast LZ compression algorithm
 * C# Implementation
 * Copyright (c) 2026. Translated from C implementation by Yann Collet.
 * 
 * BSD 2-Clause License (http://www.opensource.org/licenses/bsd-license.php)
 */

namespace LZ4Sharp
{
    /// <summary>
    /// Options for LZ4 compression
    /// </summary>
    public record LZ4Options
    {
        /// <summary>
        /// Enable SIMD-based parallel hash computation for faster compression
        /// Requires SSE2-capable CPU (all modern x64 processors)
        /// Expected speedup: 15-25% for compression
        /// Default: false (standard scalar hashing)
        /// </summary>
        public bool UseSIMDHashing { get; init; } = false;

        /// <summary>
        /// Default options with standard (non-SIMD) hashing
        /// </summary>
        public static LZ4Options Default { get; } = new LZ4Options();

        /// <summary>
        /// Options with SIMD hashing enabled for maximum performance
        /// Note: Requires SSE2 support, will automatically fall back to scalar if unavailable
        /// </summary>
        public static LZ4Options SIMDEnabled { get; } = new LZ4Options { UseSIMDHashing = true };
    }
}
