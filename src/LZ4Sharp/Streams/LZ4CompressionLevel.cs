/*
 * LZ4Sharp Streaming API - Compression Level
 * MIT License - see LICENSE
 */

namespace LZ4Sharp.Streams
{
    /// <summary>
    /// Compression levels for LZ4 encoding.
    /// </summary>
    public enum LZ4CompressionLevel
    {
        /// <summary>Fastest compression using LZ4 fast mode with acceleration.</summary>
        Fast = -1,

        /// <summary>Default compression level.</summary>
        Level0 = 0,

        /// <summary>Compression level 1.</summary>
        Level1 = 1,

        /// <summary>Compression level 2.</summary>
        Level2 = 2,

        /// <summary>HC compression level 3 (first HC level).</summary>
        HC3 = 3,

        /// <summary>HC compression level 4.</summary>
        HC4 = 4,

        /// <summary>HC compression level 5.</summary>
        HC5 = 5,

        /// <summary>HC compression level 6.</summary>
        HC6 = 6,

        /// <summary>HC compression level 7.</summary>
        HC7 = 7,

        /// <summary>HC compression level 8.</summary>
        HC8 = 8,

        /// <summary>HC compression level 9 (default HC level).</summary>
        HC9 = 9,

        /// <summary>HC compression level 10.</summary>
        HC10 = 10,

        /// <summary>HC compression level 11.</summary>
        HC11 = 11,

        /// <summary>HC compression level 12 (maximum compression).</summary>
        HC12 = 12,

        /// <summary>Maximum compression (alias for HC12).</summary>
        Max = 12
    }
}
