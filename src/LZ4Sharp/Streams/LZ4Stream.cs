/*
 * LZ4Sharp Streaming API - Stream Factory
 * MIT License - see LICENSE
 */

using System.IO;

namespace LZ4Sharp.Streams
{
    /// <summary>
    /// Factory methods for creating LZ4 encoder and decoder streams.
    /// </summary>
    public static class LZ4Stream
    {
        /// <summary>
        /// Creates an encoder stream that compresses data written to it.
        /// </summary>
        /// <param name="stream">The stream to write compressed data to.</param>
        /// <param name="level">Compression level to use.</param>
        /// <param name="leaveOpen">Whether to leave the underlying stream open when disposing.</param>
        /// <returns>An encoder stream.</returns>
        public static LZ4EncoderStream Encode(Stream stream, LZ4CompressionLevel level = LZ4CompressionLevel.Fast, bool leaveOpen = false)
        {
            var settings = new LZ4EncoderSettings { CompressionLevel = level };
            return new LZ4EncoderStream(stream, settings, leaveOpen);
        }

        /// <summary>
        /// Creates an encoder stream that compresses data written to it.
        /// </summary>
        /// <param name="stream">The stream to write compressed data to.</param>
        /// <param name="settings">Encoder settings.</param>
        /// <param name="leaveOpen">Whether to leave the underlying stream open when disposing.</param>
        /// <returns>An encoder stream.</returns>
        public static LZ4EncoderStream Encode(Stream stream, LZ4EncoderSettings settings, bool leaveOpen = false)
        {
            return new LZ4EncoderStream(stream, settings, leaveOpen);
        }

        /// <summary>
        /// Creates a decoder stream that decompresses data read from it.
        /// </summary>
        /// <param name="stream">The stream to read compressed data from.</param>
        /// <param name="leaveOpen">Whether to leave the underlying stream open when disposing.</param>
        /// <param name="interactive">If true, returns data as soon as available (partial reads).</param>
        /// <returns>A decoder stream.</returns>
        public static LZ4DecoderStream Decode(Stream stream, bool leaveOpen = false, bool interactive = false)
        {
            return new LZ4DecoderStream(stream, leaveOpen, interactive);
        }

        /// <summary>
        /// Creates a decoder stream that decompresses data read from it.
        /// </summary>
        /// <param name="stream">The stream to read compressed data from.</param>
        /// <param name="settings">Decoder settings.</param>
        /// <param name="leaveOpen">Whether to leave the underlying stream open when disposing.</param>
        /// <param name="interactive">If true, returns data as soon as available (partial reads).</param>
        /// <returns>A decoder stream.</returns>
        public static LZ4DecoderStream Decode(Stream stream, LZ4DecoderSettings settings, bool leaveOpen = false, bool interactive = false)
        {
            // Note: settings.ExtraMemory is currently unused but reserved for future optimizations
            return new LZ4DecoderStream(stream, leaveOpen, interactive);
        }
    }
}
