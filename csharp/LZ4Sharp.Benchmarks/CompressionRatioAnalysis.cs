using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Detailed compression ratio analysis comparing LZ4Sharp, LZ4Sharp.Unsafe, and K4os
    /// </summary>
    public static class CompressionRatioAnalysis
    {
        private static readonly DateTime BaseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static void Run()
        {
            Console.WriteLine("=== Compression Ratio Comparison for JSON Workloads ===\n");
            Console.WriteLine($"{"Dataset",-12} {"Size",-10} {"LZ4Sharp",-14} {"Unsafe",-14} {"K4os",-14} {"LZ4 vs K4os",-12} {"Unsafe vs K4os",-14}");
            Console.WriteLine(new string('-', 100));

            TestRatio("1KB", GenerateJsonData(1024));
            TestRatio("7KB", GenerateJsonData(7 * 1024));
            TestRatio("16KB", GenerateJsonData(16 * 1024));
            TestRatio("32KB", GenerateJsonData(32 * 1024));
            TestRatio("64KB", GenerateJsonData(64 * 1024));
            TestRatio("Simple", GenerateSimpleJsonData());
            TestRatio("Complex", GenerateComplexJsonData());
            TestRatio("Array", GenerateJsonArrayData());

            Console.WriteLine();
            Console.WriteLine("Legend:");
            Console.WriteLine("  - Ratio = compressed_size / original_size (lower is better)");
            Console.WriteLine("  - Negative % = better compression than K4os");
            Console.WriteLine("  - Positive % = worse compression than K4os");
        }

        private static void TestRatio(string name, byte[] data)
        {
            var dest = new byte[LZ4Codec.CompressBound(data.Length)];

            int lz4Size = LZ4Codec.CompressDefault(data, dest, data.Length, dest.Length);
            int unsafeSize = LZ4Codec.CompressDefault(data, dest, data.Length, dest.Length);
            int k4osSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                data, 0, data.Length, dest, 0, dest.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);

            double lz4Ratio = (double)lz4Size / data.Length;
            double unsafeRatio = (double)unsafeSize / data.Length;
            double k4osRatio = (double)k4osSize / data.Length;

            double lz4Diff = (lz4Ratio - k4osRatio) / k4osRatio * 100;
            double unsafeDiff = (unsafeRatio - k4osRatio) / k4osRatio * 100;

            string lz4Status = lz4Diff <= 1 ? "✓" : lz4Diff <= 5 ? "~" : "✗";
            string unsafeStatus = unsafeDiff <= 1 ? "✓" : unsafeDiff <= 5 ? "~" : "✗";

            Console.WriteLine($"{name,-12} {data.Length,-10:N0} {lz4Ratio:F3} ({lz4Size,6})  {unsafeRatio:F3} ({unsafeSize,6})  {k4osRatio:F3} ({k4osSize,6})  {lz4Diff:+0.0;-0.0;0.0}% {lz4Status,-3}   {unsafeDiff:+0.0;-0.0;0.0}% {unsafeStatus}");
        }

        #region JSON Data Generators

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
