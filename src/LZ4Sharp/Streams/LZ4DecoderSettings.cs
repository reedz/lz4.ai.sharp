/*
 * LZ4Sharp Streaming API - Decoder Settings
 * MIT License - see LICENSE
 */

namespace LZ4Sharp.Streams
{
    /// <summary>
    /// Settings for LZ4 frame decoding.
    /// </summary>
    public class LZ4DecoderSettings
    {
        /// <summary>
        /// Extra memory allocation for decoder optimization.
        /// Default is 0.
        /// </summary>
        public int ExtraMemory { get; set; } = 0;
    }
}
