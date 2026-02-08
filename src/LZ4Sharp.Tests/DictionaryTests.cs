using System;
using System.Text;
using Xunit;

namespace LZ4Sharp.Tests
{
    public class DictionaryTests
    {
        // Shared helper: build a dictionary from representative samples
        private static byte[] BuildDictionary(params string[] samples)
        {
            var sb = new StringBuilder();
            foreach (var s in samples)
                sb.Append(s);
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        #region Fast compress + decompress roundtrip

        [Fact]
        public void FastDict_Roundtrip_SmallData()
        {
            var dict = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog. ");
            var data = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy cat.");

            var compressed = new byte[LZ4Codec.CompressBound(data.Length)];
            int compSize = LZ4Codec.CompressWithDict(data, compressed, data.Length, compressed.Length, dict);
            Assert.True(compSize > 0, "Compression failed");

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(compressed, decompressed, compSize, decompressed.Length, dict);
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        [Fact]
        public void FastDict_Roundtrip_SpanOverload()
        {
            var dict = Encoding.UTF8.GetBytes("repeated pattern data for dictionary training. ");
            var data = Encoding.UTF8.GetBytes("repeated pattern data for dictionary testing. ");

            Span<byte> compressed = new byte[LZ4Codec.CompressBound(data.Length)];
            int compSize = LZ4Codec.CompressWithDict(data.AsSpan(), compressed, dict.AsSpan());
            Assert.True(compSize > 0);

            Span<byte> decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(
                compressed.Slice(0, compSize),
                decompressed,
                dict.AsSpan());
            Assert.Equal(data.Length, decSize);
            Assert.True(data.AsSpan().SequenceEqual(decompressed.Slice(0, decSize)));
        }

        [Fact]
        public void FastDict_BetterRatio_ThanWithout()
        {
            // Dictionary should improve compression when data is similar
            var dict = Encoding.UTF8.GetBytes(
                @"{""name"":""John"",""age"":30,""city"":""NYC""}" +
                @"{""name"":""Jane"",""age"":25,""city"":""LA""}" +
                @"{""name"":""Bob"",""age"":40,""city"":""SF""}");

            var data = Encoding.UTF8.GetBytes(
                @"{""name"":""Alice"",""age"":28,""city"":""Chicago""}");

            // Compress WITH dictionary
            var compWithDict = new byte[LZ4Codec.CompressBound(data.Length)];
            int sizeWithDict = LZ4Codec.CompressWithDict(data.AsSpan(), compWithDict, dict.AsSpan());

            // Compress WITHOUT dictionary
            var compWithout = new byte[LZ4Codec.CompressBound(data.Length)];
            int sizeWithout = LZ4Codec.CompressFast(data.AsSpan(), compWithout);

            // Dictionary should produce smaller output for similar data
            Assert.True(sizeWithDict <= sizeWithout,
                $"Dict compress ({sizeWithDict}) should be <= no-dict ({sizeWithout})");
        }

        [Fact]
        public void FastDict_LargeData_Roundtrip()
        {
            var rng = new Random(42);
            var dict = new byte[32768]; // 32KB dictionary
            rng.NextBytes(dict);

            // Data with some patterns from dictionary mixed in
            var data = new byte[100000];
            rng.NextBytes(data);
            // Copy some dict fragments into data to create matches
            Array.Copy(dict, 1000, data, 500, 200);
            Array.Copy(dict, 5000, data, 10000, 500);

            var compressed = new byte[LZ4Codec.CompressBound(data.Length)];
            int compSize = LZ4Codec.CompressWithDict(data.AsSpan(), compressed, dict.AsSpan());
            Assert.True(compSize > 0);

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(
                compressed.AsSpan(0, compSize),
                decompressed,
                dict.AsSpan());
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        [Fact]
        public void FastDict_EmptyDictionary_WorksLikeNormal()
        {
            var dict = Array.Empty<byte>();
            var data = Encoding.UTF8.GetBytes("Hello, World! This is a test of empty dictionary compression.");

            var compressed = new byte[LZ4Codec.CompressBound(data.Length)];
            int compSize = LZ4Codec.CompressWithDict(data.AsSpan(), compressed, dict.AsSpan());
            Assert.True(compSize > 0);

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(
                compressed.AsSpan(0, compSize), decompressed, dict.AsSpan());
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        [Fact]
        public void FastDict_MaxDictionary_64KB()
        {
            var dict = new byte[65535]; // Max LZ4 distance
            new Random(123).NextBytes(dict);

            var data = new byte[1000];
            Array.Copy(dict, 60000, data, 0, 200); // Copy fragment from end of dict
            new Random(456).NextBytes(data.AsSpan(200));

            var compressed = new byte[LZ4Codec.CompressBound(data.Length)];
            int compSize = LZ4Codec.CompressWithDict(data.AsSpan(), compressed, dict.AsSpan());
            Assert.True(compSize > 0);

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(
                compressed.AsSpan(0, compSize), decompressed, dict.AsSpan());
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        [Fact]
        public void FastDict_OversizeDictionary_TruncatedTo64KB()
        {
            // Dictionary larger than 64KB should use only the last 64KB
            var dict = new byte[100000];
            new Random(789).NextBytes(dict);

            var data = new byte[500];
            // Copy from the LAST 64KB of dict (which is what should be used)
            Array.Copy(dict, 90000, data, 0, 100);
            new Random(321).NextBytes(data.AsSpan(100));

            var compressed = new byte[LZ4Codec.CompressBound(data.Length)];
            int compSize = LZ4Codec.CompressWithDict(data.AsSpan(), compressed, dict.AsSpan());
            Assert.True(compSize > 0);

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(
                compressed.AsSpan(0, compSize), decompressed, dict.AsSpan());
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        #endregion

        #region HC compress + decompress roundtrip

        [Fact]
        public void HCDict_Roundtrip_SmallData()
        {
            var dict = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n");
            var data = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\n");

            var compressed = new byte[LZ4HC.CompressBound(data.Length)];
            int compSize = LZ4HC.CompressHCWithDict(data.AsSpan(), compressed, dict.AsSpan());
            Assert.True(compSize > 0, "HC dict compression failed");

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(
                compressed.AsSpan(0, compSize), decompressed, dict.AsSpan());
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        [Fact]
        public void HCDict_Roundtrip_LargeData()
        {
            var rng = new Random(42);
            var dict = new byte[16384];
            rng.NextBytes(dict);

            var data = new byte[50000];
            rng.NextBytes(data);
            Array.Copy(dict, 2000, data, 1000, 300);
            Array.Copy(dict, 8000, data, 20000, 600);

            var compressed = new byte[LZ4HC.CompressBound(data.Length)];
            int compSize = LZ4HC.CompressHCWithDict(data.AsSpan(), compressed, dict.AsSpan(), 9);
            Assert.True(compSize > 0);

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(
                compressed.AsSpan(0, compSize), decompressed, dict.AsSpan());
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(6)]
        [InlineData(9)]
        [InlineData(12)]
        public void HCDict_AllLevels_Roundtrip(int level)
        {
            var dict = Encoding.UTF8.GetBytes(
                string.Concat(Enumerable.Repeat("sample dictionary content for training. ", 50)));
            var data = Encoding.UTF8.GetBytes(
                string.Concat(Enumerable.Repeat("sample dictionary content for testing. ", 20)));

            var compressed = new byte[LZ4HC.CompressBound(data.Length)];
            int compSize = LZ4HC.CompressHCWithDict(data.AsSpan(), compressed, dict.AsSpan(), level);
            Assert.True(compSize > 0, $"HC level {level} dict compression failed");

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(
                compressed.AsSpan(0, compSize), decompressed, dict.AsSpan());
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        [Fact]
        public void HCDict_ByteArrayOverload()
        {
            var dict = Encoding.UTF8.GetBytes("dictionary data for byte array test");
            var data = Encoding.UTF8.GetBytes("dictionary data for byte array roundtrip");

            var compressed = new byte[LZ4HC.CompressBound(data.Length)];
            int compSize = LZ4HC.CompressHCWithDict(data, compressed, data.Length, compressed.Length, dict);
            Assert.True(compSize > 0);

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(compressed, decompressed, compSize, decompressed.Length, dict);
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        #endregion

        #region CompressDefaultWithDict

        [Fact]
        public void DefaultDict_Roundtrip_Span()
        {
            var dict = Encoding.UTF8.GetBytes("default dictionary training data for compression. ");
            var data = Encoding.UTF8.GetBytes("default dictionary training data for decompression. ");

            var compressed = new byte[LZ4Codec.CompressBound(data.Length)];
            int compSize = LZ4Codec.CompressDefaultWithDict(data.AsSpan(), compressed, dict.AsSpan());
            Assert.True(compSize > 0);

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(compressed.AsSpan(0, compSize), decompressed, dict.AsSpan());
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        [Fact]
        public void DefaultDict_Roundtrip_ByteArray()
        {
            var dict = Encoding.UTF8.GetBytes("default dict byte array test data for training. ");
            var data = Encoding.UTF8.GetBytes("default dict byte array test data for roundtrip. ");

            var compressed = new byte[LZ4Codec.CompressBound(data.Length)];
            int compSize = LZ4Codec.CompressDefaultWithDict(data, compressed, data.Length, compressed.Length, dict);
            Assert.True(compSize > 0);

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(compressed, decompressed, compSize, decompressed.Length, dict);
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        #endregion

        #region Edge cases

        [Fact]
        public void Dict_VerySmallData()
        {
            var dict = Encoding.UTF8.GetBytes("abcdefghijklmnopqrstuvwxyz");
            var data = Encoding.UTF8.GetBytes("abc");

            var compressed = new byte[LZ4Codec.CompressBound(data.Length)];
            int compSize = LZ4Codec.CompressWithDict(data.AsSpan(), compressed, dict.AsSpan());
            Assert.True(compSize > 0);

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(
                compressed.AsSpan(0, compSize), decompressed, dict.AsSpan());
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        [Fact]
        public void Dict_HighlyRepetitiveData()
        {
            var dict = Encoding.UTF8.GetBytes(new string('A', 1000));
            var data = Encoding.UTF8.GetBytes(new string('A', 500));

            var compressed = new byte[LZ4Codec.CompressBound(data.Length)];
            int compSize = LZ4Codec.CompressWithDict(data.AsSpan(), compressed, dict.AsSpan());
            Assert.True(compSize > 0);

            var decompressed = new byte[data.Length];
            int decSize = LZ4Codec.DecompressWithDict(
                compressed.AsSpan(0, compSize), decompressed, dict.AsSpan());
            Assert.Equal(data.Length, decSize);
            Assert.Equal(data, decompressed);
        }

        [Fact]
        public void Dict_WrongDictionary_ProducesWrongOutput()
        {
            var correctDict = Encoding.UTF8.GetBytes("correct dictionary content for matching");
            var wrongDict = Encoding.UTF8.GetBytes("wrong dictionary that doesn't match at all!!");

            var data = Encoding.UTF8.GetBytes("correct dictionary content for testing roundtrip");

            var compressed = new byte[LZ4Codec.CompressBound(data.Length)];
            int compSize = LZ4Codec.CompressWithDict(data.AsSpan(), compressed, correctDict.AsSpan());
            Assert.True(compSize > 0);

            // Decompress with wrong dictionary — should produce different output or fail
            var decompressed = new byte[data.Length + 100];
            int decSize = LZ4Codec.DecompressWithDict(
                compressed.AsSpan(0, compSize), decompressed, wrongDict.AsSpan());

            // Either decompression fails or produces wrong data
            if (decSize > 0)
            {
                Assert.False(data.AsSpan().SequenceEqual(decompressed.AsSpan(0, decSize)),
                    "Wrong dictionary should not produce correct output");
            }
        }

        [Fact]
        public void Dict_MultipleAccelerationLevels()
        {
            var dict = Encoding.UTF8.GetBytes(
                string.Concat(Enumerable.Repeat("sample training data for acceleration test. ", 30)));
            var data = Encoding.UTF8.GetBytes(
                string.Concat(Enumerable.Repeat("sample training data for acceleration verify. ", 10)));

            foreach (int accel in new[] { 1, 2, 4, 8, 16 })
            {
                var compressed = new byte[LZ4Codec.CompressBound(data.Length)];
                int compSize = LZ4Codec.CompressWithDict(data.AsSpan(), compressed, dict.AsSpan(), accel);
                Assert.True(compSize > 0, $"Accel {accel} failed");

                var decompressed = new byte[data.Length];
                int decSize = LZ4Codec.DecompressWithDict(
                    compressed.AsSpan(0, compSize), decompressed, dict.AsSpan());
                Assert.Equal(data.Length, decSize);
                Assert.Equal(data, decompressed);
            }
        }

        #endregion
    }
}
