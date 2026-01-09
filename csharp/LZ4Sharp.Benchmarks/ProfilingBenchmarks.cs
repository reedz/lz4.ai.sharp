using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Detailed profiling benchmarks to identify performance bottlenecks
    /// </summary>
    public class ProfilingBenchmarks
    {
        private static readonly DateTime BaseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static void RunProfiling()
        {
            Console.WriteLine("=== LZ4Sharp Performance Profiling ===\n");

            // Generate test data for various sizes
            var json1kb = GenerateJsonData(1024);
            var json7kb = GenerateJsonData(7 * 1024);
            var json16kb = GenerateJsonData(16 * 1024);
            var simpleJson = GenerateSimpleJsonData();
            var complexJson = GenerateComplexJsonData();
            var arrayJson = GenerateJsonArrayData();

            Console.WriteLine($"Test data sizes:");
            Console.WriteLine($"  1KB JSON:     {json1kb.Length:N0} bytes ({json1kb.Length / 1024.0:F1} KB)");
            Console.WriteLine($"  7KB JSON:     {json7kb.Length:N0} bytes ({json7kb.Length / 1024.0:F1} KB)");
            Console.WriteLine($"  16KB JSON:    {json16kb.Length:N0} bytes ({json16kb.Length / 1024.0:F1} KB)");
            Console.WriteLine($"  Simple JSON:  {simpleJson.Length:N0} bytes ({simpleJson.Length / 1024.0:F1} KB)");
            Console.WriteLine($"  Complex JSON: {complexJson.Length:N0} bytes ({complexJson.Length / 1024.0:F1} KB)");
            Console.WriteLine($"  Array JSON:   {arrayJson.Length:N0} bytes ({arrayJson.Length / 1024.0:F1} KB)");
            Console.WriteLine();

            // Profile each dataset
            ProfileDataset("1KB JSON", json1kb);
            ProfileDataset("7KB JSON", json7kb);
            ProfileDataset("16KB JSON", json16kb);
            ProfileDataset("Simple JSON", simpleJson);
            ProfileDataset("Complex JSON", complexJson);
            ProfileDataset("Array JSON", arrayJson);

            Console.WriteLine("\n=== Compression Ratio Analysis ===\n");
            AnalyzeCompressionRatios(json1kb, json7kb, json16kb, simpleJson, complexJson, arrayJson);
        }

        /// <summary>
        /// Generate JSON data of approximately the specified size
        /// </summary>
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
                    score = 100 + (i % 1000),
                    tags = new[] { "tag1", "tag2", "tag3" }
                }));
                i++;
            }
            
            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static void ProfileDataset(string name, byte[] data)
        {
            Console.WriteLine($"\n--- Profiling: {name} ({data.Length:N0} bytes) ---\n");

            // Warmup
            var maxSize = LZ4Codec.CompressBound(data.Length);
            var dest = new byte[maxSize];
            
            for (int i = 0; i < 10; i++)
            {
                LZ4Codec.CompressDefault(data, dest, data.Length, maxSize);
                LZ4Codec.CompressDefault(data, dest, data.Length, maxSize);
            }

            // Detailed timing
            const int iterations = 100;
            
            // Profile LZ4Sharp (safe)
            var (lz4SharpTime, lz4SharpSize) = ProfileCompression(
                "LZ4Sharp", data, dest, iterations,
                (src, dst) => LZ4Codec.CompressDefault(src, dst, src.Length, dst.Length));

            // Profile LZ4Sharp.Unsafe
            var (lz4UnsafeTime, lz4UnsafeSize) = ProfileCompression(
                "LZ4Sharp.Unsafe", data, dest, iterations,
                (src, dst) => LZ4Codec.CompressDefault(src, dst, src.Length, dst.Length));

            // Profile K4os
            var (k4osTime, k4osSize) = ProfileCompression(
                "K4os.LZ4", data, dest, iterations,
                (src, dst) => K4os.Compression.LZ4.LZ4Codec.Encode(
                    src, 0, src.Length, dst, 0, dst.Length,
                    K4os.Compression.LZ4.LZ4Level.L00_FAST));

            Console.WriteLine();
            Console.WriteLine($"  Compression Summary:");
            Console.WriteLine($"    LZ4Sharp:        {lz4SharpTime / iterations:F2} μs/op, ratio: {(double)lz4SharpSize / data.Length:F3}");
            Console.WriteLine($"    LZ4Sharp.Unsafe: {lz4UnsafeTime / iterations:F2} μs/op, ratio: {(double)lz4UnsafeSize / data.Length:F3}");
            Console.WriteLine($"    K4os.LZ4:        {k4osTime / iterations:F2} μs/op, ratio: {(double)k4osSize / data.Length:F3}");
            Console.WriteLine($"    LZ4Sharp vs K4os: {lz4SharpTime / k4osTime:F2}x");
            Console.WriteLine($"    Unsafe vs K4os:   {lz4UnsafeTime / k4osTime:F2}x");

            // Now profile decompression
            var compressedLz4 = new byte[maxSize];
            var compressedK4os = new byte[maxSize];
            int lz4CompSize = LZ4Codec.CompressDefault(data, compressedLz4, data.Length, maxSize);
            int k4osCompSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                data, 0, data.Length, compressedK4os, 0, maxSize,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);

            var decompDest = new byte[data.Length];

            // Warmup decompress
            for (int i = 0; i < 10; i++)
            {
                LZ4Codec.DecompressSafe(compressedLz4, decompDest, lz4CompSize, data.Length);
                LZ4Codec.DecompressSafe(compressedLz4, decompDest, lz4CompSize, data.Length);
            }

            Console.WriteLine();
            Console.WriteLine($"  Decompression Profiling:");

            var lz4DecompTime = ProfileDecompression(
                "LZ4Sharp", compressedLz4, lz4CompSize, decompDest, data.Length, iterations,
                (src, srcLen, dst, dstLen) => LZ4Codec.DecompressSafe(src, dst, srcLen, dstLen));

            var unsafeDecompTime = ProfileDecompression(
                "LZ4Sharp.Unsafe", compressedLz4, lz4CompSize, decompDest, data.Length, iterations,
                (src, srcLen, dst, dstLen) => LZ4Codec.DecompressSafe(src, dst, srcLen, dstLen));

            var k4osDecompTime = ProfileDecompression(
                "K4os.LZ4", compressedK4os, k4osCompSize, decompDest, data.Length, iterations,
                (src, srcLen, dst, dstLen) => K4os.Compression.LZ4.LZ4Codec.Decode(
                    src, 0, srcLen, dst, 0, dstLen));

            Console.WriteLine();
            Console.WriteLine($"  Decompression Summary:");
            Console.WriteLine($"    LZ4Sharp:        {lz4DecompTime / iterations:F2} μs/op");
            Console.WriteLine($"    LZ4Sharp.Unsafe: {unsafeDecompTime / iterations:F2} μs/op");
            Console.WriteLine($"    K4os.LZ4:        {k4osDecompTime / iterations:F2} μs/op");
            Console.WriteLine($"    LZ4Sharp vs K4os: {lz4DecompTime / k4osDecompTime:F2}x");
            Console.WriteLine($"    Unsafe vs K4os:   {unsafeDecompTime / k4osDecompTime:F2}x");
        }

        private static (double totalMicroseconds, int compressedSize) ProfileCompression(
            string name, byte[] src, byte[] dst, int iterations,
            Func<byte[], byte[], int> compress)
        {
            var sw = Stopwatch.StartNew();
            int size = 0;
            for (int i = 0; i < iterations; i++)
            {
                size = compress(src, dst);
            }
            sw.Stop();
            double totalMicros = sw.Elapsed.TotalMicroseconds;
            Console.WriteLine($"  {name,-18} Compress: {totalMicros / iterations:F2} μs/op ({size} bytes output)");
            return (totalMicros, size);
        }

        private static double ProfileDecompression(
            string name, byte[] src, int srcLen, byte[] dst, int dstLen, int iterations,
            Func<byte[], int, byte[], int, int> decompress)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                decompress(src, srcLen, dst, dstLen);
            }
            sw.Stop();
            double totalMicros = sw.Elapsed.TotalMicroseconds;
            Console.WriteLine($"  {name,-18} Decompress: {totalMicros / iterations:F2} μs/op");
            return totalMicros;
        }

        private static void AnalyzeCompressionRatios(byte[] json1kb, byte[] json7kb, byte[] json16kb, byte[] simple, byte[] complex, byte[] array)
        {
            Console.WriteLine("Compression ratios (lower = better compression):");
            Console.WriteLine();
            Console.WriteLine($"{"Dataset",-15} {"Size",-12} {"LZ4Sharp",-12} {"K4os",-12} {"Ratio Diff",-12}");
            Console.WriteLine(new string('-', 60));

            AnalyzeRatio("1KB", json1kb);
            AnalyzeRatio("7KB", json7kb);
            AnalyzeRatio("16KB", json16kb);
            AnalyzeRatio("Simple", simple);
            AnalyzeRatio("Complex", complex);
            AnalyzeRatio("Array", array);
        }

        private static void AnalyzeRatio(string name, byte[] data)
        {
            var dest = new byte[LZ4Codec.CompressBound(data.Length)];
            int lz4Size = LZ4Codec.CompressDefault(data, dest, data.Length, dest.Length);
            int k4osSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                data, 0, data.Length, dest, 0, dest.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);

            double lz4Ratio = (double)lz4Size / data.Length;
            double k4osRatio = (double)k4osSize / data.Length;
            double diff = (lz4Ratio - k4osRatio) / k4osRatio * 100;

            Console.WriteLine($"{name,-15} {data.Length,-12:N0} {lz4Ratio,-12:F3} {k4osRatio,-12:F3} {diff:+0.0;-0.0;0.0}%");
        }

        #region JSON Data Generators

        private static byte[] GenerateSimpleJsonData()
        {
            var sb = new StringBuilder();
            sb.Append('[');
            
            for (int i = 0; i < 1000; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(JsonSerializer.Serialize(new
                {
                    id = i,
                    name = $"User{i}",
                    email = $"user{i}@example.com",
                    active = i % 2 == 0
                }));
            }
            
            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

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
                    posts = Enumerable.Range(0, 5).Select(j => new
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

        private static byte[] GenerateJsonArrayData()
        {
            var data = new
            {
                metadata = new
                {
                    version = "1.0",
                    generated = BaseDate.ToString("o"),
                    recordCount = 2000
                },
                records = Enumerable.Range(0, 2000).Select(i => new
                {
                    id = i,
                    timestamp = BaseDate.AddSeconds(-i).ToString("o"),
                    value = 100.0 + (i % 100),
                    status = new[] { "active", "inactive", "pending" }[i % 3],
                    metadata = new Dictionary<string, object>
                    {
                        ["key1"] = $"value{i}",
                        ["key2"] = i % 10,
                        ["key3"] = i % 2 == 0
                    }
                }).ToArray()
            };
            
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
        }

        #endregion
    }
}
