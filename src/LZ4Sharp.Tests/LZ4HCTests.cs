using System.Text;
using Xunit;

namespace LZ4Sharp.Tests
{
    public class LZ4HCTests
    {
        [Fact]
        public void CompressBound_ReturnsValidSize()
        {
            int inputSize = 1000;
            int bound = LZ4HC.CompressBound(inputSize);
            
            Assert.True(bound > inputSize, "CompressBound should return a size larger than input");
            Assert.True(bound < inputSize * 2, "CompressBound should not be excessively large");
        }

        [Fact]
        public void CompressHC_SimpleString_Success()
        {
            // Arrange
            string text = "Hello, World! This is a test string for HC compression.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;
            
            byte[] compressed = new byte[LZ4HC.CompressBound(sourceSize)];
            byte[] decompressed = new byte[sourceSize];

            // Act - Compress with HC
            int compressedSize = LZ4HC.CompressHC(source, compressed, sourceSize, compressed.Length);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, "HC Compression should succeed");

            // Act - Decompress (HC uses same decompression as standard LZ4)
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressed.Length);

            // Assert - Decompression
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
            
            string resultText = Encoding.UTF8.GetString(decompressed);
            Assert.Equal(text, resultText);
        }

        [Theory]
        [InlineData(3)]  // Minimum level
        [InlineData(9)]  // Default level
        [InlineData(12)] // Maximum level
        public void CompressHC_DifferentLevels_Success(int level)
        {
            // Arrange
            string text = "The quick brown fox jumps over the lazy dog. " +
                         "The quick brown fox jumps over the lazy dog. " +
                         "The quick brown fox jumps over the lazy dog.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;
            
            byte[] compressed = new byte[LZ4HC.CompressBound(sourceSize)];
            byte[] decompressed = new byte[sourceSize];

            // Act - Compress
            int compressedSize = LZ4HC.CompressHC(source, compressed, sourceSize, compressed.Length, level);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, $"Compression should succeed at level {level}");

            // Act - Decompress
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressed.Length);

            // Assert - Decompression
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void CompressHC_NullInput_ReturnsError()
        {
            byte[] dest = new byte[100];
            int result = LZ4HC.CompressHC(null!, dest, 10, 100);
            Assert.True(result < 0, "Should return error for null input");
        }

        [Fact]
        public void CompressHC_NullDestination_ReturnsError()
        {
            byte[] source = new byte[100];
            int result = LZ4HC.CompressHC(source, null!, 100, 100);
            Assert.True(result < 0, "Should return error for null destination");
        }

        [Fact]
        public void CompressHC_LargeData_Success()
        {
            // Arrange - Create 10KB of repetitive data
            var text = new StringBuilder();
            for (int i = 0; i < 100; i++)
            {
                text.Append("Lorem ipsum dolor sit amet, consectetur adipiscing elit. ");
            }
            
            byte[] source = Encoding.UTF8.GetBytes(text.ToString());
            int sourceSize = source.Length;
            
            byte[] compressed = new byte[LZ4HC.CompressBound(sourceSize)];
            byte[] decompressed = new byte[sourceSize];

            // Act - Compress
            int compressedSize = LZ4HC.CompressHC(source, compressed, sourceSize, compressed.Length);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, "Compression should succeed");
            Assert.True(compressedSize < sourceSize, "Compressed data should be smaller than source");

            // Act - Decompress
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressed.Length);

            // Assert - Decompression
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
        }
    }
}
