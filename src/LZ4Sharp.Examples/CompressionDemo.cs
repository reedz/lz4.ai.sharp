/*
 * CompressionDemo.cs
 * Demonstrates various compression scenarios and performance characteristics
 */

using System;
using System.Diagnostics;
using System.Text;

namespace LZ4Sharp.Examples
{
    public class CompressionDemo
    {
        public static void RunDemo()
        {
            Console.WriteLine("=== LZ4Sharp Compression Demonstration ===\n");

            // Test 1: Simple string compression
            TestSimpleString();
            Console.WriteLine();

            // Test 2: Repeated pattern (high compression ratio)
            TestRepeatedPattern();
            Console.WriteLine();

            // Test 3: Random data (low compression ratio)
            TestRandomData();
            Console.WriteLine();

            // Test 4: Large file simulation
            TestLargeData();
            Console.WriteLine();

            // Test 5: Performance benchmark
            PerformanceBenchmark();
        }

        static void TestSimpleString()
        {
            Console.WriteLine("Test 1: Simple String Compression");
            Console.WriteLine("-----------------------------------");

            string text = "Hello, World! This is LZ4 compression in C#.";
            byte[] source = Encoding.UTF8.GetBytes(text);
            
            CompressAndDecompress(source, "Simple String");
        }

        static void TestRepeatedPattern()
        {
            Console.WriteLine("Test 2: Repeated Pattern (High Compression)");
            Console.WriteLine("--------------------------------------------");

            // Create highly compressible data
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 100; i++)
            {
                sb.Append("This is a repeated pattern. ");
            }
            
            byte[] source = Encoding.UTF8.GetBytes(sb.ToString());
            CompressAndDecompress(source, "Repeated Pattern");
        }

        static void TestRandomData()
        {
            Console.WriteLine("Test 3: Random Data (Low Compression)");
            Console.WriteLine("--------------------------------------");

            byte[] source = new byte[1000];
            Random random = new Random(42);
            random.NextBytes(source);
            
            CompressAndDecompress(source, "Random Data");
        }

        static void TestLargeData()
        {
            Console.WriteLine("Test 4: Large Data (100KB)");
            Console.WriteLine("---------------------------");

            byte[] source = new byte[100 * 1024]; // 100KB
            
            // Fill with pattern
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)(i % 256);
            }
            
            CompressAndDecompress(source, "Large Data");
        }

        static void PerformanceBenchmark()
        {
            Console.WriteLine("Test 5: Performance Benchmark");
            Console.WriteLine("------------------------------");

            byte[] source = new byte[1024 * 1024]; // 1MB
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)(i % 256);
            }

            int iterations = 100;
            Stopwatch sw = new Stopwatch();

            // Compression benchmark
            byte[] compressed = new byte[LZ4Codec.CompressBound(source.Length)];
            
            sw.Start();
            int compressedSize = 0;
            for (int i = 0; i < iterations; i++)
            {
                compressedSize = LZ4Codec.CompressDefault(source, compressed, source.Length, compressed.Length);
            }
            sw.Stop();

            double compressTimeMs = sw.Elapsed.TotalMilliseconds;
            double compressMBps = (source.Length * iterations / (1024.0 * 1024.0)) / (compressTimeMs / 1000.0);

            Console.WriteLine($"Compression: {iterations} iterations of 1MB");
            Console.WriteLine($"Total time: {compressTimeMs:F2} ms");
            Console.WriteLine($"Speed: {compressMBps:F2} MB/s");
            Console.WriteLine($"Compression ratio: {(double)compressedSize / source.Length:F4}");

            // Decompression benchmark
            byte[] decompressed = new byte[source.Length];
            
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressed.Length);
            }
            sw.Stop();

            double decompressTimeMs = sw.Elapsed.TotalMilliseconds;
            double decompressMBps = (source.Length * iterations / (1024.0 * 1024.0)) / (decompressTimeMs / 1000.0);

            Console.WriteLine($"\nDecompression: {iterations} iterations of 1MB");
            Console.WriteLine($"Total time: {decompressTimeMs:F2} ms");
            Console.WriteLine($"Speed: {decompressMBps:F2} MB/s");
        }

        static void CompressAndDecompress(byte[] source, string testName)
        {
            int sourceSize = source.Length;
            
            // Compress
            byte[] compressed = new byte[LZ4Codec.CompressBound(sourceSize)];
            int compressedSize = LZ4Codec.CompressDefault(source, compressed, sourceSize, compressed.Length);

            if (compressedSize <= 0)
            {
                Console.WriteLine($"[{testName}] Compression FAILED!");
                return;
            }

            // Decompress
            byte[] decompressed = new byte[sourceSize];
            int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressed.Length);

            if (decompressedSize < 0)
            {
                Console.WriteLine($"[{testName}] Decompression FAILED!");
                return;
            }

            // Verify
            bool isValid = true;
            if (decompressedSize != sourceSize)
            {
                isValid = false;
            }
            else
            {
                for (int i = 0; i < sourceSize; i++)
                {
                    if (source[i] != decompressed[i])
                    {
                        isValid = false;
                        break;
                    }
                }
            }

            // Report
            double ratio = (double)compressedSize / sourceSize;
            double spaceSavings = (1.0 - ratio) * 100.0;

            Console.WriteLine($"Original size:    {sourceSize:N0} bytes");
            Console.WriteLine($"Compressed size:  {compressedSize:N0} bytes");
            Console.WriteLine($"Compression ratio: {ratio:F4} ({spaceSavings:F2}% space savings)");
            Console.WriteLine($"Validation:       {(isValid ? "PASSED ✓" : "FAILED ✗")}");
        }
    }
}
