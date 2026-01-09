using Xunit;
using System;
using System.Text;

namespace LZ4Sharp.Tests
{
    /// <summary>
    /// Tests for Phase 2 Span-based API optimizations
    /// </summary>
    public class SpanApiTests
    {
        [Fact]
        public void CompressDecompress_Span_SimpleString_Success()
        {
            string testString = "Hello, World! This is a test of the Span-based LZ4 compression API.";
            byte[] source = Encoding.UTF8.GetBytes(testString);
            
            int maxCompressed = LZ4Codec.CompressBound(source.Length);
            byte[] compressed = new byte[maxCompressed];
            byte[] decompressed = new byte[source.Length];

            // Compress using Span API
            int compressedSize = LZ4Codec.CompressDefault(source.AsSpan(), compressed.AsSpan());
            Assert.True(compressedSize > 0);
            // Small strings may not compress, so just check it's within bounds
            Assert.True(compressedSize <= maxCompressed);

            // Decompress using Span API
            int decompressedSize = LZ4Codec.DecompressSafe(compressed.AsSpan(0, compressedSize), decompressed.AsSpan());
            Assert.Equal(source.Length, decompressedSize);

            // Verify content
            string result = Encoding.UTF8.GetString(decompressed);
            Assert.Equal(testString, result);
        }

        [Fact]
        public void CompressDecompress_Span_LargeData_Success()
        {
            // Test with 100KB of data
            byte[] source = new byte[100 * 1024];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)(i % 256);
            }

            int maxCompressed = LZ4Codec.CompressBound(source.Length);
            byte[] compressed = new byte[maxCompressed];
            byte[] decompressed = new byte[source.Length];

            // Compress using Span API
            int compressedSize = LZ4Codec.CompressDefault(source.AsSpan(), compressed.AsSpan());
            Assert.True(compressedSize > 0);

            // Decompress using Span API
            int decompressedSize = LZ4Codec.DecompressSafe(compressed.AsSpan(0, compressedSize), decompressed.AsSpan());
            Assert.Equal(source.Length, decompressedSize);

            // Verify content
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void CompressDecompress_Span_RepeatedPattern_Success()
        {
            // Highly compressible data
            byte[] source = new byte[10 * 1024];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)'A';
            }

            int maxCompressed = LZ4Codec.CompressBound(source.Length);
            byte[] compressed = new byte[maxCompressed];
            byte[] decompressed = new byte[source.Length];

            // Compress using Span API
            int compressedSize = LZ4Codec.CompressDefault(source.AsSpan(), compressed.AsSpan());
            Assert.True(compressedSize > 0);
            Assert.True(compressedSize < source.Length / 10); // Should compress very well

            // Decompress using Span API
            int decompressedSize = LZ4Codec.DecompressSafe(compressed.AsSpan(0, compressedSize), decompressed.AsSpan());
            Assert.Equal(source.Length, decompressedSize);

            // Verify content
            Assert.Equal(source, decompressed);
        }

        [Fact]
        public void CompressDecompress_Span_EmptyData_HandlesGracefully()
        {
            byte[] source = Array.Empty<byte>();
            byte[] compressed = new byte[100];
            
            // Empty data should return error or handle gracefully
            int compressedSize = LZ4Codec.CompressDefault(source.AsSpan(), compressed.AsSpan());
            Assert.True(compressedSize <= 0); // Empty input should fail or return 0
        }

        [Fact]
        public void CompressDecompress_Span_MatchesArrayApi_SimpleString()
        {
            string testString = "The quick brown fox jumps over the lazy dog.";
            byte[] source = Encoding.UTF8.GetBytes(testString);
            
            int maxCompressed = LZ4Codec.CompressBound(source.Length);
            
            // Compress with array API
            byte[] compressedArray = new byte[maxCompressed];
            int arraySizeCompressed = LZ4Codec.CompressDefault(source, compressedArray, source.Length, maxCompressed);
            
            // Compress with Span API
            byte[] compressedSpan = new byte[maxCompressed];
            int spanSizeCompressed = LZ4Codec.CompressDefault(source.AsSpan(), compressedSpan.AsSpan());
            
            // Results should be identical
            Assert.Equal(arraySizeCompressed, spanSizeCompressed);
            Assert.Equal(compressedArray.AsSpan(0, arraySizeCompressed).ToArray(), 
                        compressedSpan.AsSpan(0, spanSizeCompressed).ToArray());
        }

        [Fact]
        public void CompressDecompress_Span_MatchesArrayApi_LargeData()
        {
            // Test with 50KB of varied data
            byte[] source = new byte[50 * 1024];
            var random = new Random(42);
            random.NextBytes(source);
            
            int maxCompressed = LZ4Codec.CompressBound(source.Length);
            
            // Compress with array API
            byte[] compressedArray = new byte[maxCompressed];
            int arraySizeCompressed = LZ4Codec.CompressDefault(source, compressedArray, source.Length, maxCompressed);
            
            // Compress with Span API
            byte[] compressedSpan = new byte[maxCompressed];
            int spanSizeCompressed = LZ4Codec.CompressDefault(source.AsSpan(), compressedSpan.AsSpan());
            
            // Results should be identical
            Assert.Equal(arraySizeCompressed, spanSizeCompressed);
            Assert.Equal(compressedArray.AsSpan(0, arraySizeCompressed).ToArray(), 
                        compressedSpan.AsSpan(0, spanSizeCompressed).ToArray());
        }

        [Fact]
        public void Decompress_Span_CanDecompressArrayCompressed()
        {
            string testString = "Testing cross-compatibility between array and Span APIs.";
            byte[] source = Encoding.UTF8.GetBytes(testString);
            
            int maxCompressed = LZ4Codec.CompressBound(source.Length);
            byte[] compressed = new byte[maxCompressed];
            
            // Compress with array API
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, source.Length, maxCompressed);
            
            // Decompress with Span API
            byte[] decompressed = new byte[source.Length];
            int decompressedSize = LZ4Codec.DecompressSafe(compressed.AsSpan(0, compressedSize), decompressed.AsSpan());
            
            Assert.Equal(source.Length, decompressedSize);
            string result = Encoding.UTF8.GetString(decompressed);
            Assert.Equal(testString, result);
        }

        [Fact]
        public void Compress_Span_CanBeDecompressedByArrayApi()
        {
            string testString = "Testing reverse cross-compatibility between Span and array APIs.";
            byte[] source = Encoding.UTF8.GetBytes(testString);
            
            int maxCompressed = LZ4Codec.CompressBound(source.Length);
            byte[] compressed = new byte[maxCompressed];
            
            // Compress with Span API
            int compressedSize = LZ4Codec.CompressDefault(source.AsSpan(), compressed.AsSpan());
            
            // Decompress with array API
            byte[] decompressed = new byte[source.Length];
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, source.Length);
            
            Assert.Equal(source.Length, decompressedSize);
            string result = Encoding.UTF8.GetString(decompressed);
            Assert.Equal(testString, result);
        }
    }
}
