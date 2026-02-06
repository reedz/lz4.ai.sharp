/*
 * LZ4Sharp Streaming API - Encoder Settings
 * MIT License - see LICENSE
 */

namespace LZ4Sharp.Streams
{
    /// <summary>
    /// Settings for LZ4 frame encoding.
    /// </summary>
    public class LZ4EncoderSettings
    {
        /// <summary>
        /// Compression level to use. Default is <see cref="LZ4CompressionLevel.Fast"/>.
        /// </summary>
        public LZ4CompressionLevel CompressionLevel { get; set; } = LZ4CompressionLevel.Fast;

        /// <summary>
        /// Block size in bytes. Must be 64KB, 256KB, 1MB, or 4MB.
        /// Default is 64KB (65536).
        /// </summary>
        public int BlockSize { get; set; } = 64 * 1024;

        /// <summary>
        /// Whether to chain blocks (linked mode) for better compression.
        /// Default is true.
        /// </summary>
        public bool ChainBlocks { get; set; } = true;

        /// <summary>
        /// Whether to include a content checksum at the end of the frame.
        /// Default is false.
        /// </summary>
        public bool ContentChecksum { get; set; } = false;

        /// <summary>
        /// Whether to include a checksum after each block.
        /// Default is false.
        /// </summary>
        public bool BlockChecksum { get; set; } = false;

        /// <summary>
        /// Optional content length to include in the frame header.
        /// </summary>
        public long? ContentLength { get; set; }

        /// <summary>
        /// Optional dictionary ID for preset dictionaries.
        /// </summary>
        public uint? Dictionary { get; set; }

        /// <summary>
        /// Extra memory allocation for encoder optimization.
        /// Default is 0.
        /// </summary>
        public int ExtraMemory { get; set; } = 0;

        /// <summary>
        /// Gets the LZ4 frame BlockSize enum value for the configured block size.
        /// </summary>
        internal LZ4Frame.BlockSize GetBlockSizeId()
        {
            return BlockSize switch
            {
                64 * 1024 => LZ4Frame.BlockSize.Max64KB,
                256 * 1024 => LZ4Frame.BlockSize.Max256KB,
                1024 * 1024 => LZ4Frame.BlockSize.Max1MB,
                4 * 1024 * 1024 => LZ4Frame.BlockSize.Max4MB,
                _ => LZ4Frame.BlockSize.Max64KB
            };
        }

        /// <summary>
        /// Gets the internal compression level value for LZ4Frame.
        /// </summary>
        internal int GetInternalCompressionLevel()
        {
            return (int)CompressionLevel;
        }
    }
}
