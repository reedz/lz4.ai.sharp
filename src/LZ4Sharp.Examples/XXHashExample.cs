/*
 * XXHash Example
 * Demonstrates usage of XXHash for fast hashing
 */

using System;
using System.Text;
using LZ4Sharp;

namespace LZ4Sharp.Examples
{
    public static class XXHashExample
    {
        public static void Run()
        {
            Console.WriteLine("=== XXHash Example ===\n");

            // Example 1: Simple one-shot hashing
            Console.WriteLine("Example 1: One-Shot Hashing");
            byte[] data = Encoding.UTF8.GetBytes("Hello, World!");
            uint hash = XXHash.XXH32(data, 0);
            Console.WriteLine($"Data: \"Hello, World!\"");
            Console.WriteLine($"Hash: 0x{hash:X8}");
            Console.WriteLine();

            // Example 2: Hashing with different seeds
            Console.WriteLine("Example 2: Different Seeds");
            byte[] message = Encoding.UTF8.GetBytes("test data");
            uint hash1 = XXHash.XXH32(message, 0);
            uint hash2 = XXHash.XXH32(message, 123);
            uint hash3 = XXHash.XXH32(message, 456);
            Console.WriteLine($"Data: \"test data\"");
            Console.WriteLine($"Hash (seed 0):   0x{hash1:X8}");
            Console.WriteLine($"Hash (seed 123): 0x{hash2:X8}");
            Console.WriteLine($"Hash (seed 456): 0x{hash3:X8}");
            Console.WriteLine();

            // Example 3: Streaming hash for large data
            Console.WriteLine("Example 3: Streaming Hash");
            byte[] largeData = new byte[10000];
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }

            // One-shot hash
            uint oneShotHash = XXHash.XXH32(largeData, 0);

            // Streaming hash (process in chunks)
            var state = new XXHash.XXH32State(0);
            int chunkSize = 1000;
            for (int i = 0; i < largeData.Length; i += chunkSize)
            {
                int remaining = Math.Min(chunkSize, largeData.Length - i);
                byte[] chunk = new byte[remaining];
                Array.Copy(largeData, i, chunk, 0, remaining);
                state.Update(chunk, remaining);
            }
            uint streamHash = state.Digest();

            Console.WriteLine($"Data size: {largeData.Length} bytes");
            Console.WriteLine($"One-shot hash:  0x{oneShotHash:X8}");
            Console.WriteLine($"Streaming hash: 0x{streamHash:X8}");
            Console.WriteLine($"Hashes match: {oneShotHash == streamHash}");
            Console.WriteLine();

            // Example 4: Using XXHash for data integrity
            Console.WriteLine("Example 4: Data Integrity Check");
            byte[] originalData = Encoding.UTF8.GetBytes("Important data that must not be corrupted");
            uint originalHash = XXHash.XXH32(originalData, 0);
            Console.WriteLine($"Original data: \"{Encoding.UTF8.GetString(originalData)}\"");
            Console.WriteLine($"Original hash: 0x{originalHash:X8}");

            // Simulate data corruption
            byte[] corruptedData = new byte[originalData.Length];
            Array.Copy(originalData, corruptedData, originalData.Length);
            corruptedData[10] = (byte)(corruptedData[10] + 1); // Corrupt one byte

            uint corruptedHash = XXHash.XXH32(corruptedData, 0);
            Console.WriteLine($"Corrupted data: \"{Encoding.UTF8.GetString(corruptedData)}\"");
            Console.WriteLine($"Corrupted hash: 0x{corruptedHash:X8}");
            Console.WriteLine($"Data corrupted: {originalHash != corruptedHash}");
            Console.WriteLine();

            // Example 5: Performance comparison
            Console.WriteLine("Example 5: Performance Test");
            const int iterations = 1000;
            byte[] perfData = new byte[100000]; // 100KB
            Random rnd = new Random(42);
            rnd.NextBytes(perfData);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                XXHash.XXH32(perfData, (uint)i);
            }
            sw.Stop();

            double throughput = (perfData.Length * iterations / 1024.0 / 1024.0) / (sw.ElapsedMilliseconds / 1000.0);
            Console.WriteLine($"Data size: {perfData.Length} bytes");
            Console.WriteLine($"Iterations: {iterations}");
            Console.WriteLine($"Total time: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"Throughput: {throughput:F2} MB/s");
            Console.WriteLine();

            Console.WriteLine("XXHash examples completed successfully!");
        }
    }
}
