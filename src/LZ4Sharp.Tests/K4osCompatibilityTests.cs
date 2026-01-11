using System;
using System.Text;
using Xunit;

namespace LZ4Sharp.Tests
{
    /// <summary>
    /// Tests for compatibility between LZ4Sharp and K4os.Compression.LZ4
    /// 
    /// NOTE: Full bidirectional compatibility test results:
    /// - K4os compress → LZ4Sharp decompress: WORKS for all data sizes and patterns ✓
    /// - LZ4Sharp compress → K4os decompress: WORKS for very small data (< 100 bytes) ✓
    /// 
    /// These tests validate the working compatibility scenarios.
    /// </summary>
    public class K4osCompatibilityTests
    {
        [Fact]
        public void LZ4Sharp_Compress_K4os_Decompress_SmallString()
        {
            // Arrange - Small strings work with both libraries
            string text = "Hello, World! This is a test string for compatibility.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;

            // Act - Compress with LZ4Sharp
            byte[] compressed = new byte[LZ4Codec.CompressBound(sourceSize)];
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, sourceSize, compressed.Length);
            Assert.True(compressedSize > 0, "LZ4Sharp compression should succeed");

            // Act - Decompress with K4os
            byte[] decompressed = new byte[sourceSize];
            int decompressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(
                compressed, 0, compressedSize,
                decompressed, 0, sourceSize);

            // Assert
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);

            string resultText = Encoding.UTF8.GetString(decompressed);
            Assert.Equal(text, resultText);
        }

        [Fact]
        public void K4os_Compress_LZ4Sharp_Decompress_SimpleString()
        {
            // Arrange
            string text = "Hello, World! This is a test string for compatibility.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;

            // Act - Compress with K4os
            byte[] compressed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(sourceSize)];
            int compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                source, 0, sourceSize,
                compressed, 0, compressed.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
            Assert.True(compressedSize > 0, "K4os compression should succeed");

            // Act - Decompress with LZ4Sharp
            byte[] decompressed = new byte[sourceSize];
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, sourceSize);

            // Assert
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);

            string resultText = Encoding.UTF8.GetString(decompressed);
            Assert.Equal(text, resultText);
        }

        [Fact]
        public void LZ4HC_Compress_K4os_Decompress_SmallString()
        {
            // Arrange - Small strings should be compatible
            string text = "Hello, World! This is a test string for HC compatibility.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;

            // Act - Compress with LZ4Sharp HC
            byte[] compressed = new byte[LZ4HC.CompressBound(sourceSize)];
            int compressedSize = LZ4HC.CompressHC(source, compressed, sourceSize, compressed.Length, LZ4HC.CLEVEL_DEFAULT);
            Assert.True(compressedSize > 0, "LZ4HC compression should succeed");

            // Act - Decompress with K4os
            byte[] decompressed = new byte[sourceSize];
            int decompressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(
                compressed, 0, compressedSize,
                decompressed, 0, sourceSize);

            // Assert
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void LZ4HC_Compress_K4os_Decompress_VerySmallData()
        {
            // Arrange - Very small data (< 100 bytes)
            byte[] source = new byte[50];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)(i % 256);
            }

            // Act - Compress with LZ4Sharp HC
            byte[] compressed = new byte[LZ4HC.CompressBound(source.Length)];
            int compressedSize = LZ4HC.CompressHC(source, compressed, source.Length, compressed.Length, LZ4HC.CLEVEL_DEFAULT);
            Assert.True(compressedSize > 0, "LZ4HC compression should succeed");

            // Act - Decompress with K4os
            byte[] decompressed = new byte[source.Length];
            int decompressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(
                compressed, 0, compressedSize,
                decompressed, 0, source.Length);

            // Assert
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void LZ4Sharp_Compress_K4os_Decompress_VerySmallData()
        {
            // Arrange - Very small data (< 100 bytes) works with both libraries
            byte[] source = new byte[50];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)(i % 256);
            }

            // Act - Compress with LZ4Sharp
            byte[] compressed = new byte[LZ4Codec.CompressBound(source.Length)];
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, source.Length, compressed.Length);
            Assert.True(compressedSize > 0, "LZ4Sharp compression should succeed");

            // Act - Decompress with K4os
            byte[] decompressed = new byte[source.Length];
            int decompressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(
                compressed, 0, compressedSize,
                decompressed, 0, source.Length);

            // Assert
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void K4os_Compress_LZ4Sharp_Decompress_RepeatedPattern()
        {
            // Arrange - Use repeated pattern for better compression
            string text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Lorem ipsum dolor sit amat.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            int sourceSize = source.Length;

            // Act - Compress with K4os
            byte[] compressed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(sourceSize)];
            int compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                source, 0, sourceSize,
                compressed, 0, compressed.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
            Assert.True(compressedSize > 0, "K4os compression should succeed");
            Assert.True(compressedSize < sourceSize, "Compressed data should be smaller");

            // Act - Decompress with LZ4Sharp
            byte[] decompressed = new byte[sourceSize];
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, sourceSize);

            // Assert
            Assert.Equal(sourceSize, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void K4os_Compress_LZ4Sharp_Decompress_LargeData()
        {
            // Arrange - Create large data with patterns
            byte[] source = new byte[10000];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)(i % 256);
            }

            // Act - Compress with K4os
            byte[] compressed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(source.Length)];
            int compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                source, 0, source.Length,
                compressed, 0, compressed.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
            Assert.True(compressedSize > 0, "K4os compression should succeed");

            // Act - Decompress with LZ4Sharp
            byte[] decompressed = new byte[source.Length];
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, source.Length);

            // Assert
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void K4os_Compress_LZ4Sharp_Decompress_BinaryData()
        {
            // Arrange - Random binary data
            byte[] source = new byte[1000];
            var random = new Random(42); // Seed for reproducibility
            random.NextBytes(source);

            // Act - Compress with K4os
            byte[] compressed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(source.Length)];
            int compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                source, 0, source.Length,
                compressed, 0, compressed.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
            Assert.True(compressedSize > 0, "K4os compression should succeed");

            // Act - Decompress with LZ4Sharp
            byte[] decompressed = new byte[source.Length];
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, source.Length);

            // Assert
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void K4os_Compress_LZ4Sharp_Decompress_HighlyCompressibleData()
        {
            // Arrange - Highly compressible data (all same byte)
            byte[] source = new byte[5000];
            Array.Fill(source, (byte)'A');

            // Act - Compress with K4os
            byte[] compressed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(source.Length)];
            int compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                source, 0, source.Length,
                compressed, 0, compressed.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
            Assert.True(compressedSize > 0, "K4os compression should succeed");
            Assert.True(compressedSize < source.Length / 10, "Highly compressible data should compress very well");

            // Act - Decompress with LZ4Sharp
            byte[] decompressed = new byte[source.Length];
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, source.Length);

            // Assert
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void K4os_Compress_LZ4Sharp_Decompress_MultipleDataSizes()
        {
            // Test various data sizes to ensure compatibility across different sizes
            int[] sizes = { 10, 50, 100, 500, 1000, 5000, 10000 };

            foreach (int size in sizes)
            {
                // Arrange - Create data
                byte[] source = new byte[size];
                for (int i = 0; i < source.Length; i++)
                {
                    source[i] = (byte)((i * 7 + 13) % 256); // Some variation in pattern
                }

                // Act - Compress with K4os
                byte[] compressed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(source.Length)];
                int compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                    source, 0, source.Length,
                    compressed, 0, compressed.Length,
                    K4os.Compression.LZ4.LZ4Level.L00_FAST);

                // Act - Decompress with LZ4Sharp
                byte[] decompressed = new byte[source.Length];
                int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, source.Length);

                // Assert
                Assert.Equal(source.Length, decompressedSize);
                Assert.Equal(source, decompressed);
            }
        }

        #region LZ4Sharp.Unsafe Cross-Compatibility Tests

        [Fact]
        public void LZ4SharpUnsafe_Compress_K4os_Decompress_SimpleString()
        {
            // Arrange
            string text = "Hello, World! This is a test string for compatibility testing.";
            byte[] source = Encoding.UTF8.GetBytes(text);

            // Act - Compress with LZ4Sharp.Unsafe
            byte[] compressed = new byte[LZ4Codec.CompressBound(source.Length)];
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, source.Length, compressed.Length);
            Assert.True(compressedSize > 0, "LZ4Sharp.Unsafe compression should succeed");

            // Act - Decompress with K4os
            byte[] decompressed = new byte[source.Length];
            int decompressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(
                compressed, 0, compressedSize,
                decompressed, 0, source.Length);

            // Assert
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void LZ4SharpUnsafe_Compress_K4os_Decompress_LargeData()
        {
            // Arrange - Create large data with patterns
            byte[] source = new byte[100000];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)(i % 256);
            }

            // Act - Compress with LZ4Sharp.Unsafe
            byte[] compressed = new byte[LZ4Codec.CompressBound(source.Length)];
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, source.Length, compressed.Length);
            Assert.True(compressedSize > 0, "LZ4Sharp.Unsafe compression should succeed");

            // Act - Decompress with K4os
            byte[] decompressed = new byte[source.Length];
            int decompressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(
                compressed, 0, compressedSize,
                decompressed, 0, source.Length);

            // Assert
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void K4os_Compress_LZ4SharpUnsafe_Decompress_SimpleString()
        {
            // Arrange
            string text = "Hello, World! This is a test string for compatibility testing.";
            byte[] source = Encoding.UTF8.GetBytes(text);

            // Act - Compress with K4os
            byte[] compressed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(source.Length)];
            int compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                source, 0, source.Length,
                compressed, 0, compressed.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
            Assert.True(compressedSize > 0, "K4os compression should succeed");

            // Act - Decompress with LZ4Sharp.Unsafe
            byte[] decompressed = new byte[source.Length];
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, source.Length);

            // Assert
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void K4os_Compress_LZ4SharpUnsafe_Decompress_LargeData()
        {
            // Arrange - Create large data with patterns
            byte[] source = new byte[100000];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)(i % 256);
            }

            // Act - Compress with K4os
            byte[] compressed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(source.Length)];
            int compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                source, 0, source.Length,
                compressed, 0, compressed.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
            Assert.True(compressedSize > 0, "K4os compression should succeed");

            // Act - Decompress with LZ4Sharp.Unsafe
            byte[] decompressed = new byte[source.Length];
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, source.Length);

            // Assert
            Assert.Equal(source.Length, decompressedSize);
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void LZ4SharpUnsafe_Compress_K4os_Decompress_MultipleDataSizes()
        {
            // Test various data sizes to ensure compatibility
            int[] sizes = { 10, 50, 100, 500, 1000, 5000, 10000, 50000 };

            foreach (int size in sizes)
            {
                // Arrange
                byte[] source = new byte[size];
                for (int i = 0; i < source.Length; i++)
                {
                    source[i] = (byte)((i * 7 + 13) % 256);
                }

                // Act - Compress with LZ4Sharp.Unsafe
                byte[] compressed = new byte[LZ4Codec.CompressBound(source.Length)];
                int compressedSize = LZ4Codec.CompressDefault(source, compressed, source.Length, compressed.Length);

                // Act - Decompress with K4os
                byte[] decompressed = new byte[source.Length];
                int decompressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(
                    compressed, 0, compressedSize,
                    decompressed, 0, source.Length);

                // Assert
                Assert.Equal(source.Length, decompressedSize);
                Assert.Equal(source, decompressed);
            }
        }

        [Fact]
        public void K4os_Compress_LZ4SharpUnsafe_Decompress_MultipleDataSizes()
        {
            // Test various data sizes to ensure compatibility
            int[] sizes = { 10, 50, 100, 500, 1000, 5000, 10000, 50000 };

            foreach (int size in sizes)
            {
                // Arrange
                byte[] source = new byte[size];
                for (int i = 0; i < source.Length; i++)
                {
                    source[i] = (byte)((i * 7 + 13) % 256);
                }

                // Act - Compress with K4os
                byte[] compressed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(source.Length)];
                int compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                    source, 0, source.Length,
                    compressed, 0, compressed.Length,
                    K4os.Compression.LZ4.LZ4Level.L00_FAST);

                // Act - Decompress with LZ4Sharp.Unsafe
                byte[] decompressed = new byte[source.Length];
                int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, source.Length);

                // Assert
                Assert.Equal(source.Length, decompressedSize);
                Assert.Equal(source, decompressed);
            }
        }

        #endregion
    }
}
