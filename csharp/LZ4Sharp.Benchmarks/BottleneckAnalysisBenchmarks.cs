using BenchmarkDotNet.Attributes;
using System;
using System.Text;
using System.Text.Json;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Benchmarks to analyze the top 3 performance bottlenecks in LZ4Sharp compression
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class BottleneckAnalysisBenchmarks
    {
        private byte[] _smallData = null!;  // ~1KB
        private byte[] _mediumData = null!; // ~16KB  
        private byte[] _largeData = null!;  // ~256KB (complex JSON)
        private byte[] _destBuffer = null!;

        [GlobalSetup]
        public void Setup()
        {
            _smallData = GenerateJsonData(1024);
            _mediumData = GenerateJsonData(16 * 1024);
            _largeData = GenerateComplexJsonData(); // ~600KB
            
            _destBuffer = new byte[LZ4Codec.CompressBound(_largeData.Length)];
            
            Console.WriteLine($"Small: {_smallData.Length} bytes");
            Console.WriteLine($"Medium: {_mediumData.Length} bytes");
            Console.WriteLine($"Large: {_largeData.Length} bytes");
        }

        #region Bottleneck 1: Hash Table Clear Overhead

        [Benchmark(Description = "1KB Compress (measures hash clear ratio)")]
        public int Compress1KB()
        {
            return LZ4Codec.CompressDefault(_smallData, _destBuffer, _smallData.Length, _destBuffer.Length);
        }

        [Benchmark(Description = "16KB Compress")]
        public int Compress16KB()
        {
            return LZ4Codec.CompressDefault(_mediumData, _destBuffer, _mediumData.Length, _destBuffer.Length);
        }

        [Benchmark(Description = "Large JSON Compress (~600KB)")]
        public int CompressLargeJson()
        {
            return LZ4Codec.CompressDefault(_largeData, _destBuffer, _largeData.Length, _destBuffer.Length);
        }

        #endregion

        #region K4os Baseline for Comparison

        [Benchmark(Description = "K4os 1KB Compress", Baseline = true)]
        public int K4osCompress1KB()
        {
            return K4os.Compression.LZ4.LZ4Codec.Encode(
                _smallData, 0, _smallData.Length,
                _destBuffer, 0, _destBuffer.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
        }

        [Benchmark(Description = "K4os 16KB Compress")]
        public int K4osCompress16KB()
        {
            return K4os.Compression.LZ4.LZ4Codec.Encode(
                _mediumData, 0, _mediumData.Length,
                _destBuffer, 0, _destBuffer.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
        }

        [Benchmark(Description = "K4os Large JSON Compress")]
        public int K4osCompressLargeJson()
        {
            return K4os.Compression.LZ4.LZ4Codec.Encode(
                _largeData, 0, _largeData.Length,
                _destBuffer, 0, _destBuffer.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
        }

        #endregion

        #region Bottleneck 2: Hash Clear vs No-Clear (simulated with Array.Clear benchmark)

        [Benchmark(Description = "Array.Clear 16KB (hash table simulation)")]
        public void ArrayClear16KB()
        {
            var arr = new uint[4096]; // Same as HASH_SIZE
            Array.Clear(arr, 0, 4096);
        }

        [Benchmark(Description = "Span.Clear 16KB (optimized)")]
        public void SpanClear16KB()
        {
            var arr = new uint[4096];
            arr.AsSpan().Clear();
        }

        #endregion

        #region Data Generators

        private static byte[] GenerateJsonData(int targetSize)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            
            int i = 0;
            while (sb.Length < targetSize - 100)
            {
                if (i > 0) sb.Append(',');
                sb.Append(JsonSerializer.Serialize(new
                {
                    id = i,
                    name = $"User{i}",
                    email = $"user{i}@example.com",
                    active = i % 2 == 0,
                    score = 100 + (i % 1000)
                }));
                i++;
            }
            
            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static readonly DateTime BaseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static byte[] GenerateComplexJsonData()
        {
            var sb = new StringBuilder();
            sb.Append('[');
            
            for (int i = 0; i < 500; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(JsonSerializer.Serialize(new
                {
                    id = i,
                    user = new
                    {
                        name = $"User{i}",
                        email = $"user{i}@example.com",
                        profile = new
                        {
                            age = 20 + (i % 50),
                            country = new[] { "USA", "UK", "Canada", "Australia" }[i % 4],
                            preferences = new
                            {
                                notifications = true,
                                theme = i % 2 == 0 ? "dark" : "light",
                                language = "en"
                            }
                        }
                    },
                    posts = System.Linq.Enumerable.Range(0, 5).Select(j => new
                    {
                        id = i * 100 + j,
                        title = $"Post {j} by User {i}",
                        content = "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
                        tags = new[] { "technology", "programming", "compression" },
                        timestamp = BaseDate.AddDays(-i).ToString("o")
                    }).ToArray()
                }));
            }
            
            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        #endregion
    }
}
