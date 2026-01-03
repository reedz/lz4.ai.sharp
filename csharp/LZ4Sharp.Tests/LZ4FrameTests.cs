using System.Text;
using Xunit;

namespace LZ4Sharp.Tests
{
    public class LZ4FrameTests
    {
        [Fact]
        public void CompressFrameBound_ReturnsValidSize()
        {
            int inputSize = 1000;
            int bound = LZ4Frame.CompressFrameBound(inputSize);
            
            Assert.True(bound > inputSize, "CompressFrameBound should return a size larger than input");
        }

        [Fact]
        public void CompressFrame_SimpleString_Success()
        {
            // Arrange
            string text = "Hello, World! This is a test string for frame compression.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;
            
            byte[] compressed = new byte[LZ4Frame.CompressFrameBound(sourceSize)];
            byte[] decompressed = new byte[sourceSize];

            // Act - Compress into frame format
            int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, source, sourceSize);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, "Frame compression should succeed");

            // Act - Decompress from frame format
            int decompressedSize = LZ4Frame.DecompressFrame(decompressed, decompressed.Length, compressed, compressedSize);

            // Assert - Decompression
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
            
            string resultText = Encoding.UTF8.GetString(decompressed);
            Assert.Equal(text, resultText);
        }

        [Fact]
        public void CompressFrame_WithPreferences_Success()
        {
            // Arrange
            string text = "Testing frame compression with custom preferences.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;
            
            var prefs = new LZ4Frame.FramePreferences
            {
                BlockSizeId = LZ4Frame.BlockSize.Max64KB,
                BlockMode = LZ4Frame.BlockMode.Independent,
                ContentChecksumFlag = LZ4Frame.ContentChecksum.ChecksumEnabled
            };
            
            byte[] compressed = new byte[LZ4Frame.CompressFrameBound(sourceSize, prefs)];
            byte[] decompressed = new byte[sourceSize];

            // Act - Compress
            int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, source, sourceSize, prefs);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, "Frame compression with preferences should succeed");

            // Act - Decompress
            int decompressedSize = LZ4Frame.DecompressFrame(decompressed, decompressed.Length, compressed, compressedSize);

            // Assert - Decompression
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void CompressFrame_LargeData_Success()
        {
            // Arrange - Create 10KB of data
            var text = new StringBuilder();
            for (int i = 0; i < 100; i++)
            {
                text.Append("Lorem ipsum dolor sit amet, consectetur adipiscing elit. ");
            }
            
            byte[] source = Encoding.UTF8.GetBytes(text.ToString());
            int sourceSize = source.Length;
            
            byte[] compressed = new byte[LZ4Frame.CompressFrameBound(sourceSize)];
            byte[] decompressed = new byte[sourceSize];

            // Act - Compress
            int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, source, sourceSize);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, "Frame compression should succeed");

            // Act - Decompress
            int decompressedSize = LZ4Frame.DecompressFrame(decompressed, decompressed.Length, compressed, compressedSize);

            // Assert - Decompression
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void CompressFrame_WithHCCompression_Success()
        {
            // Arrange
            string text = "This text will be compressed using HC mode in frame format. " +
                         "This text will be compressed using HC mode in frame format.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;
            
            var prefs = new LZ4Frame.FramePreferences
            {
                CompressionLevel = LZ4HC.CLEVEL_DEFAULT
            };
            
            byte[] compressed = new byte[LZ4Frame.CompressFrameBound(sourceSize, prefs)];
            byte[] decompressed = new byte[sourceSize];

            // Act - Compress with HC
            int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, source, sourceSize, prefs);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, "Frame compression with HC should succeed");

            // Act - Decompress
            int decompressedSize = LZ4Frame.DecompressFrame(decompressed, decompressed.Length, compressed, compressedSize);

            // Assert - Decompression
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void DecompressFrame_InvalidMagic_ReturnsError()
        {
            // Arrange - Create invalid frame (wrong magic number)
            byte[] invalidFrame = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x40, 0x70, 0x00 };
            byte[] dest = new byte[100];

            // Act
            int result = LZ4Frame.DecompressFrame(dest, dest.Length, invalidFrame, invalidFrame.Length);

            // Assert
            Assert.True(result < 0, "Should return error for invalid magic number");
        }

        [Fact]
        public void CompressFrame_NullInput_ReturnsError()
        {
            byte[] dest = new byte[100];
            int result = LZ4Frame.CompressFrame(dest, dest.Length, null!, 10);
            Assert.True(result < 0, "Should return error for null input");
        }

        [Fact]
        public void CompressFrame_NullDestination_ReturnsError()
        {
            byte[] source = new byte[100];
            int result = LZ4Frame.CompressFrame(null!, 100, source, 100);
            Assert.True(result < 0, "Should return error for null destination");
        }

        [Fact]
        public void CompressFrame_MultipleBlocks_Success()
        {
            // Arrange - Create data larger than default block size
            var text = new StringBuilder();
            for (int i = 0; i < 2000; i++)  // Should create multiple blocks
            {
                text.Append("The quick brown fox jumps over the lazy dog. ");
            }
            
            byte[] source = Encoding.UTF8.GetBytes(text.ToString());
            int sourceSize = source.Length;
            
            byte[] compressed = new byte[LZ4Frame.CompressFrameBound(sourceSize)];
            byte[] decompressed = new byte[sourceSize];

            // Act - Compress
            int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, source, sourceSize);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, "Multi-block frame compression should succeed");

            // Act - Decompress
            int decompressedSize = LZ4Frame.DecompressFrame(decompressed, decompressed.Length, compressed, compressedSize);

            // Assert - Decompression
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Theory]
        [InlineData(LZ4Frame.BlockSize.Max64KB)]
        [InlineData(LZ4Frame.BlockSize.Max256KB)]
        [InlineData(LZ4Frame.BlockSize.Max1MB)]
        [InlineData(LZ4Frame.BlockSize.Max4MB)]
        public void CompressFrame_DifferentBlockSizes_Success(LZ4Frame.BlockSize blockSize)
        {
            // Arrange
            string text = "Testing different block sizes for frame compression. " +
                         "Testing different block sizes for frame compression.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;
            
            var prefs = new LZ4Frame.FramePreferences
            {
                BlockSizeId = blockSize
            };
            
            byte[] compressed = new byte[LZ4Frame.CompressFrameBound(sourceSize, prefs)];
            byte[] decompressed = new byte[sourceSize];

            // Act - Compress
            int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, source, sourceSize, prefs);
            
            // Assert - Compression
            Assert.True(compressedSize > 0, $"Frame compression with block size {blockSize} should succeed");

            // Act - Decompress
            int decompressedSize = LZ4Frame.DecompressFrame(decompressed, decompressed.Length, compressed, compressedSize);

            // Assert - Decompression
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
        }
    }
}
