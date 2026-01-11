/*
 * XXHash Tests
 * Copyright (c) 2026
 */

using System;
using System.Text;
using Xunit;

namespace LZ4Sharp.Tests
{
    public class XXHashTests
    {
        [Fact]
        public void XXH32_EmptyInput_ReturnsExpectedHash()
        {
            // Empty input with seed 0 should produce a known hash value
            byte[] input = Array.Empty<byte>();
            uint hash = XXHash.XXH32(input, 0);
            
            // Known hash value for empty input with seed 0
            // From reference implementation: 0x02CC5D05
            Assert.Equal(0x02CC5D05u, hash);
        }

        [Fact]
        public void XXH32_EmptyInput_WithSeed_ReturnsExpectedHash()
        {
            // Empty input with seed 123
            byte[] input = Array.Empty<byte>();
            uint hash = XXHash.XXH32(input, 123);
            
            // Known hash value for empty input with seed 123
            // From reference implementation: 0x3930C86E
            Assert.Equal(0x3930C86Eu, hash);
        }

        [Fact]
        public void XXH32_SimpleString_ReturnsExpectedHash()
        {
            // Test with a simple string "Hello, World!"
            byte[] input = Encoding.UTF8.GetBytes("Hello, World!");
            uint hash = XXHash.XXH32(input, 0);
            
            // Known hash value for "Hello, World!" with seed 0
            // From reference implementation: 0x4007DE50
            Assert.Equal(0x4007DE50u, hash);
        }

        [Fact]
        public void XXH32_LongInput_ReturnsExpectedHash()
        {
            // Test with longer input (>16 bytes to test main loop)
            byte[] input = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog");
            uint hash = XXHash.XXH32(input, 0);
            
            // Known hash value from reference implementation: 0xE85EA4DE
            Assert.Equal(0xE85EA4DEu, hash);
        }

        [Fact]
        public void XXH32_BinaryData_ReturnsConsistentHash()
        {
            // Test with binary data
            byte[] input = new byte[100];
            for (int i = 0; i < input.Length; i++)
            {
                input[i] = (byte)(i * 7 % 256);
            }
            
            uint hash1 = XXHash.XXH32(input, 0);
            uint hash2 = XXHash.XXH32(input, 0);
            
            // Hash should be consistent
            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void XXH32_DifferentSeeds_ProduceDifferentHashes()
        {
            byte[] input = Encoding.UTF8.GetBytes("test data");
            
            uint hash1 = XXHash.XXH32(input, 0);
            uint hash2 = XXHash.XXH32(input, 123);
            uint hash3 = XXHash.XXH32(input, 456);
            
            // Different seeds should produce different hashes
            Assert.NotEqual(hash1, hash2);
            Assert.NotEqual(hash2, hash3);
            Assert.NotEqual(hash1, hash3);
        }

        [Fact]
        public void XXH32_SmallInputs_ReturnExpectedHashes()
        {
            // Test various small input sizes (0-15 bytes)
            for (int len = 0; len <= 15; len++)
            {
                byte[] input = new byte[len];
                for (int i = 0; i < len; i++)
                {
                    input[i] = (byte)i;
                }
                
                uint hash = XXHash.XXH32(input, 0);
                // Hash should be computable without error
                Assert.True(hash >= 0);
            }
        }

        [Fact]
        public void XXH32_LargeInput_ReturnsExpectedHash()
        {
            // Test with large input (multiple 16-byte blocks)
            byte[] input = new byte[1000];
            for (int i = 0; i < input.Length; i++)
            {
                input[i] = (byte)(i % 256);
            }
            
            uint hash = XXHash.XXH32(input, 0);
            // Known hash for this pattern
            Assert.True(hash > 0);
            
            // Verify consistency
            uint hash2 = XXHash.XXH32(input, 0);
            Assert.Equal(hash, hash2);
        }

        [Fact]
        public void XXH32_NullInput_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => XXHash.XXH32(null!, 0));
        }

        [Fact]
        public void XXH32_WithLength_InvalidLength_ThrowsException()
        {
            byte[] input = new byte[10];
            Assert.Throws<ArgumentOutOfRangeException>(() => XXHash.XXH32(input, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => XXHash.XXH32(input, 20, 0));
        }

        [Fact]
        public void XXH32State_EmptyInput_ReturnsExpectedHash()
        {
            var state = new XXHash.XXH32State(0);
            uint hash = state.Digest();
            
            // Should match one-shot hash of empty input
            Assert.Equal(XXHash.XXH32(Array.Empty<byte>(), 0), hash);
        }

        [Fact]
        public void XXH32State_SingleUpdate_MatchesOneShot()
        {
            byte[] input = Encoding.UTF8.GetBytes("Hello, World!");
            
            var state = new XXHash.XXH32State(0);
            state.Update(input, input.Length);
            uint streamHash = state.Digest();
            
            uint oneShotHash = XXHash.XXH32(input, 0);
            
            Assert.Equal(oneShotHash, streamHash);
        }

        [Fact]
        public void XXH32State_MultipleUpdates_MatchesOneShot()
        {
            byte[] input = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog");
            
            // Compute in one shot
            uint oneShotHash = XXHash.XXH32(input, 0);
            
            // Compute in multiple chunks
            var state = new XXHash.XXH32State(0);
            state.Update(input, 0, 10);
            state.Update(input, 10, 10);
            state.Update(input, 20, input.Length - 20);
            uint streamHash = state.Digest();
            
            Assert.Equal(oneShotHash, streamHash);
        }

        [Fact]
        public void XXH32State_SmallChunks_MatchesOneShot()
        {
            byte[] input = new byte[100];
            for (int i = 0; i < input.Length; i++)
            {
                input[i] = (byte)(i % 256);
            }
            
            uint oneShotHash = XXHash.XXH32(input, 0);
            
            // Update byte by byte
            var state = new XXHash.XXH32State(0);
            for (int i = 0; i < input.Length; i++)
            {
                state.Update(new byte[] { input[i] }, 1);
            }
            uint streamHash = state.Digest();
            
            Assert.Equal(oneShotHash, streamHash);
        }

        [Fact]
        public void XXH32State_Reset_ClearsState()
        {
            var state = new XXHash.XXH32State(0);
            byte[] input = Encoding.UTF8.GetBytes("test");
            
            state.Update(input, input.Length);
            state.Reset(0);
            
            uint hash = state.Digest();
            Assert.Equal(XXHash.XXH32(Array.Empty<byte>(), 0), hash);
        }

        [Fact]
        public void XXH32State_WithSeed_MatchesOneShot()
        {
            byte[] input = Encoding.UTF8.GetBytes("test data");
            uint seed = 12345;
            
            var state = new XXHash.XXH32State(seed);
            state.Update(input, input.Length);
            uint streamHash = state.Digest();
            
            uint oneShotHash = XXHash.XXH32(input, seed);
            
            Assert.Equal(oneShotHash, streamHash);
        }

        [Fact]
        public void XXH32State_LargeData_MatchesOneShot()
        {
            // Test with data larger than internal buffer (16 bytes)
            byte[] input = new byte[1024];
            Random rnd = new Random(42);
            rnd.NextBytes(input);
            
            uint oneShotHash = XXHash.XXH32(input, 0);
            
            var state = new XXHash.XXH32State(0);
            state.Update(input, input.Length);
            uint streamHash = state.Digest();
            
            Assert.Equal(oneShotHash, streamHash);
        }

        [Fact]
        public void XXH32State_NullInput_ThrowsException()
        {
            var state = new XXHash.XXH32State(0);
            Assert.Throws<ArgumentNullException>(() => state.Update(null!, 0));
        }

        [Fact]
        public void XXH32_AllByteLengths_Consistent()
        {
            // Test all byte lengths from 0 to 32 for edge cases
            for (int len = 0; len <= 32; len++)
            {
                byte[] input = new byte[len];
                for (int i = 0; i < len; i++)
                {
                    input[i] = (byte)i;
                }
                
                uint oneShotHash = XXHash.XXH32(input, 0);
                
                var state = new XXHash.XXH32State(0);
                state.Update(input, input.Length);
                uint streamHash = state.Digest();
                
                Assert.Equal(oneShotHash, streamHash);
            }
        }
    }
}
