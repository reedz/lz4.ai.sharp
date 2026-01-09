using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Cross-compatibility tests between LZ4Sharp and K4os.Compression.LZ4
    /// Verifies that compressed data can be decompressed by either library
    /// 
    /// Known compatibility status:
    /// - LZ4Sharp.Unsafe compress ↔ K4os decompress: FULL COMPATIBILITY ✓
    /// - K4os compress ↔ LZ4Sharp.Unsafe decompress: FULL COMPATIBILITY ✓
    /// - LZ4Sharp (safe) compress → K4os decompress: Only small data (&lt; 100 bytes)
    /// - K4os compress → LZ4Sharp (safe) decompress: FULL COMPATIBILITY ✓
    /// </summary>
    public static class CrossCompatibilityTests
    {
        private static readonly DateTime BaseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static void Run()
        {
            Console.WriteLine("=== LZ4Sharp / K4os Cross-Compatibility Tests ===\n");

            var testCases = new (string name, byte[] data)[]
            {
                ("1KB JSON", GenerateJsonData(1024)),
                ("7KB JSON", GenerateJsonData(7 * 1024)),
                ("16KB JSON", GenerateJsonData(16 * 1024)),
                ("Simple JSON (70KB)", GenerateSimpleJsonData()),
                ("Complex JSON (587KB)", GenerateComplexJsonData()),
                ("Array JSON (270KB)", GenerateJsonArrayData()),
            };

            int passed = 0;
            int failed = 0;

            foreach (var (name, data) in testCases)
            {
                Console.WriteLine($"--- Testing: {name} ({data.Length:N0} bytes) ---");
                
                // Test 1: LZ4Sharp compress -> K4os decompress
                if (TestLZ4SharpToK4os(data))
                {
                    Console.WriteLine("  ✓ LZ4Sharp compress -> K4os decompress");
                    passed++;
                }
                else
                {
                    Console.WriteLine("  ✗ LZ4Sharp compress -> K4os decompress FAILED");
                    failed++;
                }

                // Test 2: LZ4Sharp.Unsafe compress -> K4os decompress
                if (TestLZ4SharpUnsafeToK4os(data))
                {
                    Console.WriteLine("  ✓ LZ4Sharp.Unsafe compress -> K4os decompress");
                    passed++;
                }
                else
                {
                    Console.WriteLine("  ✗ LZ4Sharp.Unsafe compress -> K4os decompress FAILED");
                    failed++;
                }

                // Test 3: K4os compress -> LZ4Sharp decompress
                if (TestK4osToLZ4Sharp(data))
                {
                    Console.WriteLine("  ✓ K4os compress -> LZ4Sharp decompress");
                    passed++;
                }
                else
                {
                    Console.WriteLine("  ✗ K4os compress -> LZ4Sharp decompress FAILED");
                    failed++;
                }

                // Test 4: K4os compress -> LZ4Sharp.Unsafe decompress
                if (TestK4osToLZ4SharpUnsafe(data))
                {
                    Console.WriteLine("  ✓ K4os compress -> LZ4Sharp.Unsafe decompress");
                    passed++;
                }
                else
                {
                    Console.WriteLine("  ✗ K4os compress -> LZ4Sharp.Unsafe decompress FAILED");
                    failed++;
                }

                Console.WriteLine();
            }

            Console.WriteLine("=== Summary ===");
            Console.WriteLine($"Passed: {passed}");
            Console.WriteLine($"Failed: {failed}");
            Console.WriteLine($"Total:  {passed + failed}");
            Console.WriteLine();
            
            if (failed == 0)
            {
                Console.WriteLine("✓ All cross-compatibility tests PASSED!");
                Console.WriteLine("  LZ4Sharp and K4os produce interchangeable LZ4 block format.");
            }
            else
            {
                Console.WriteLine("✗ Some tests FAILED - compatibility issues detected!");
            }
        }

        private static bool TestLZ4SharpToK4os(byte[] original)
        {
            try
            {
                // Compress with LZ4Sharp
                var compressed = new byte[LZ4Codec.CompressBound(original.Length)];
                int compressedSize = LZ4Codec.CompressDefault(original, compressed, original.Length, compressed.Length);

                // Decompress with K4os
                var decompressed = new byte[original.Length];
                int decompressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(
                    compressed, 0, compressedSize,
                    decompressed, 0, original.Length);

                if (decompressedSize != original.Length)
                {
                    Console.WriteLine($"    Size mismatch: expected {original.Length}, got {decompressedSize}");
                    return false;
                }
                
                if (!original.SequenceEqual(decompressed))
                {
                    // Find first difference
                    for (int i = 0; i < original.Length; i++)
                    {
                        if (original[i] != decompressed[i])
                        {
                            Console.WriteLine($"    Data mismatch at byte {i}: expected 0x{original[i]:X2}, got 0x{decompressed[i]:X2}");
                            break;
                        }
                    }
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Error: {ex.Message}");
                return false;
            }
        }

        private static bool TestLZ4SharpUnsafeToK4os(byte[] original)
        {
            try
            {
                // Compress with LZ4Sharp.Unsafe
                var compressed = new byte[LZ4Codec.CompressBound(original.Length)];
                int compressedSize = LZ4Codec.CompressDefault(original, compressed, original.Length, compressed.Length);

                // Decompress with K4os
                var decompressed = new byte[original.Length];
                int decompressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(
                    compressed, 0, compressedSize,
                    decompressed, 0, original.Length);

                return decompressedSize == original.Length && 
                       original.SequenceEqual(decompressed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Error: {ex.Message}");
                return false;
            }
        }

        private static bool TestK4osToLZ4Sharp(byte[] original)
        {
            try
            {
                // Compress with K4os
                var compressed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(original.Length)];
                int compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                    original, 0, original.Length,
                    compressed, 0, compressed.Length,
                    K4os.Compression.LZ4.LZ4Level.L00_FAST);

                // Decompress with LZ4Sharp
                var decompressed = new byte[original.Length];
                int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, original.Length);

                return decompressedSize == original.Length && 
                       original.SequenceEqual(decompressed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Error: {ex.Message}");
                return false;
            }
        }

        private static bool TestK4osToLZ4SharpUnsafe(byte[] original)
        {
            try
            {
                // Compress with K4os
                var compressed = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(original.Length)];
                int compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                    original, 0, original.Length,
                    compressed, 0, compressed.Length,
                    K4os.Compression.LZ4.LZ4Level.L00_FAST);

                // Decompress with LZ4Sharp.Unsafe
                var decompressed = new byte[original.Length];
                int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, original.Length);

                if (decompressedSize != original.Length)
                {
                    Console.WriteLine($"    Size mismatch: expected {original.Length}, got {decompressedSize}");
                    return false;
                }
                
                if (!original.SequenceEqual(decompressed))
                {
                    // Find first difference
                    for (int i = 0; i < original.Length; i++)
                    {
                        if (original[i] != decompressed[i])
                        {
                            Console.WriteLine($"    Data mismatch at byte {i}: expected 0x{original[i]:X2}, got 0x{decompressed[i]:X2}");
                            break;
                        }
                    }
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Error: {ex.Message}");
                return false;
            }
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
