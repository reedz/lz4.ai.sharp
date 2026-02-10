using System;
using System.Text;
using Xunit;

namespace LZ4Sharp.Tests
{
    public class LZ4CodecTests
    {
        [Fact]
        public void CompressBound_ReturnsValidSize()
        {
            int inputSize = 1000;
            int bound = LZ4Codec.CompressBound(inputSize);
            
            Assert.True(bound > inputSize, "CompressBound should return a size larger than input");
            Assert.True(bound < inputSize * 2, "CompressBound should not be excessively large");
        }

        [Fact]
        public void CompressDecompress_SimpleString_Success()
        {
            // Arrange
            string text = "Hello, World! This is a test string.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;
            
            byte[] compressed = new byte[LZ4Codec.CompressBound(sourceSize)];
            byte[] decompressed = new byte[sourceSize];

            // Act - Compress
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, sourceSize, compressed.Length);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, "Compression should succeed");

            // Act - Decompress
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressed.Length);

            // Assert - Decompression
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
            
            string resultText = Encoding.UTF8.GetString(decompressed);
            Assert.Equal(text, resultText);
        }

        [Fact]
        public void CompressDecompress_RepeatedPattern_Success()
        {
            // Arrange - Use repeated pattern for better compression
            string text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Lorem ipsum dolor site amat.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;
            
            byte[] compressed = new byte[LZ4Codec.CompressBound(sourceSize)];
            byte[] decompressed = new byte[sourceSize];

            // Act - Compress
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, sourceSize, compressed.Length);
            
            // Assert - Compression (should have good ratio due to repetition)
            Assert.True(compressedSize > 0, "Compression should succeed");
            Assert.True(compressedSize < sourceSize, "Compressed data should be smaller than source");

            // Act - Decompress
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressed.Length);

            // Assert - Decompression
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void CompressDecompress_EmptyData_HandlesGracefully()
        {
            // Arrange
            byte[] source = Array.Empty<byte>();
            byte[] compressed = new byte[100];
            
            // Act
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, 0, compressed.Length);
            
            // Assert
            Assert.True(compressedSize <= 0, "Empty data compression should fail or return 0");
        }

        [Fact]
        public void CompressDecompress_LargeData_Success()
        {
            // Arrange - Create large data with patterns
            byte[] source = new byte[10000];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)(i % 256);
            }
            
            byte[] compressed = new byte[LZ4Codec.CompressBound(source.Length)];
            byte[] decompressed = new byte[source.Length];

            // Act - Compress
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, source.Length, compressed.Length);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, "Compression should succeed");

            // Act - Decompress
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressed.Length);

            // Assert - Decompression
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void CompressDecompress_AllSameBytes_Success()
        {
            // Arrange - Highly compressible data
            byte[] source = new byte[1000];
            Array.Fill(source, (byte)'A');
            
            byte[] compressed = new byte[LZ4Codec.CompressBound(source.Length)];
            byte[] decompressed = new byte[source.Length];

            // Act - Compress
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, source.Length, compressed.Length);
            
            // Assert - Compression (should achieve excellent compression)
            Assert.True(compressedSize > 0, "Compression should succeed");
            Assert.True(compressedSize < source.Length / 10, "Repeated bytes should compress very well");

            // Act - Decompress
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressed.Length);

            // Assert - Decompression
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void Decompress_InvalidData_ReturnsError()
        {
            // Arrange - Random data that is not valid LZ4 compressed data
            byte[] invalidCompressed = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            byte[] decompressed = new byte[100];

            // Act
            int result = LZ4Codec.DecompressSafe(invalidCompressed, decompressed, invalidCompressed.Length, decompressed.Length);

            // Assert
            Assert.True(result < 0, "Decompressing invalid data should return error");
        }

        [Fact]
        public void Compress_NullSource_ReturnsError()
        {
            // Arrange
            byte[] destination = new byte[100];

            // Act
            int result = LZ4Codec.CompressDefault(null, destination, 0, destination.Length);

            // Assert
            Assert.True(result < 0, "Null source should return error");
        }

        [Fact]
        public void Decompress_InsufficientBuffer_ReturnsError()
        {
            // Arrange
            string text = "This is a test string that will be compressed.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            byte[] compressed = new byte[LZ4Codec.CompressBound(source.Length)];
            
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, source.Length, compressed.Length);
            Assert.True(compressedSize > 0);

            // Act - Try to decompress into buffer that's too small
            byte[] tooSmall = new byte[5];
            int result = LZ4Codec.DecompressSafe(compressed, tooSmall, compressedSize, tooSmall.Length);

            // Assert
            Assert.True(result < 0, "Insufficient buffer should return error");
        }

        [Fact]
        public void CompressDecompress_BinaryData_Success()
        {
            // Arrange - Random binary data
            byte[] source = new byte[500];
            var random = new Random(42); // Seed for reproducibility
            random.NextBytes(source);
            
            byte[] compressed = new byte[LZ4Codec.CompressBound(source.Length)];
            byte[] decompressed = new byte[source.Length];

            // Act - Compress
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, source.Length, compressed.Length);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, "Compression should succeed");

            // Act - Decompress
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressed.Length);

            // Assert - Decompression
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        /// <summary>
        /// Regression: CompressFast with a 270-byte payload whose first literal run (75 bytes)
        /// triggers a SIMD remainder > 8 bytes in WildCopy8. Previously the tail handler only
        /// wrote the last 8 bytes, leaving a gap of zeroes.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(8)]
        public void CompressFast_SmallInput_RoundTripsCorrectly(int acceleration)
        {
            var source = Encoding.UTF8.GetBytes(
                "This is test data for LZ4 compression. " +
                "It should be long enough to actually compress. " +
                "Repeated patterns help: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa " +
                "More repeated patterns: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb " +
                "And some JSON-like content: {\"key\": \"value\", \"number\": 12345}");

            var compressed = new byte[LZ4Codec.CompressBound(source.Length)];
            int compSize = LZ4Codec.CompressFast(source.AsSpan(), compressed.AsSpan(), acceleration);
            Assert.True(compSize > 0);

            var decompressed = new byte[source.Length];
            int decSize = LZ4Codec.DecompressSafe(compressed.AsSpan(0, compSize), decompressed.AsSpan());
            Assert.Equal(source.Length, decSize);
            Assert.Equal(source, decompressed);
        }
    }
}
