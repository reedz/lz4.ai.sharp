using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace LZ4Sharp.Benchmarks;

/// <summary>
/// Detailed profiling to identify LZ4Sharp bottlenecks
/// </summary>
public class ProfilingBenchmark
{
    private static readonly DateTime BaseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void Run()
    {
        Console.WriteLine("=== LZ4Sharp Detailed Profiling ===\n");

        // Generate test data for different sizes
        var testCases = new (string name, int size)[]
        {
            ("1KB JSON", 1024),
            ("7KB JSON", 7 * 1024),
            ("16KB JSON", 16 * 1024),
            ("72KB JSON", 72 * 1024),
            ("256KB JSON", 256 * 1024),
        };

        foreach (var (name, size) in testCases)
        {
            ProfileWorkload(name, size);
        }

        Console.WriteLine("\n=== Compression Component Breakdown (16KB) ===\n");
        ProfileCompressionComponents();
        
        Console.WriteLine("\n=== Decompression Component Breakdown (16KB) ===\n");
        ProfileDecompressionComponents();
        
        Console.WriteLine("\n=== Memory Access Patterns ===\n");
        ProfileMemoryAccess();
        
        Console.WriteLine("\n=== Hash Function Performance ===\n");
        ProfileHashFunction();
    }

    private static void ProfileWorkload(string name, int targetSize)
    {
        // Generate 1000 unique payloads
        const int payloadCount = 1000;
        var payloads = new byte[payloadCount][];
        var compressed = new byte[payloadCount][];
        var compressedSizes = new int[payloadCount];

        for (int i = 0; i < payloadCount; i++)
        {
            payloads[i] = GenerateJsonPayload(targetSize, i);
            compressed[i] = new byte[LZ4Codec.CompressBound(payloads[i].Length)];
            compressedSizes[i] = LZ4Codec.CompressDefault(payloads[i], compressed[i], payloads[i].Length, compressed[i].Length);
        }

        var decompressBuffer = new byte[payloads.Max(p => p.Length)];
        var compressBuffer = new byte[LZ4Codec.CompressBound(payloads.Max(p => p.Length))];

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            LZ4Codec.CompressDefault(payloads[i], compressBuffer, payloads[i].Length, compressBuffer.Length);
            LZ4Codec.DecompressSafe(compressed[i], decompressBuffer, compressedSizes[i], payloads[i].Length);
        }

        // Profile compression
        var sw = Stopwatch.StartNew();
        long totalInputBytes = 0;
        long totalOutputBytes = 0;
        for (int i = 0; i < payloadCount; i++)
        {
            var result = LZ4Codec.CompressDefault(payloads[i], compressBuffer, payloads[i].Length, compressBuffer.Length);
            totalInputBytes += payloads[i].Length;
            totalOutputBytes += result;
        }
        sw.Stop();
        var compressTime = sw.Elapsed;
        var compressThroughput = totalInputBytes / compressTime.TotalSeconds / 1024 / 1024;

        // Profile decompression
        sw.Restart();
        long totalDecompressedBytes = 0;
        for (int i = 0; i < payloadCount; i++)
        {
            var result = LZ4Codec.DecompressSafe(compressed[i], decompressBuffer, compressedSizes[i], payloads[i].Length);
            totalDecompressedBytes += result;
        }
        sw.Stop();
        var decompressTime = sw.Elapsed;
        var decompressThroughput = totalDecompressedBytes / decompressTime.TotalSeconds / 1024 / 1024;

        var ratio = (double)totalOutputBytes / totalInputBytes;

        Console.WriteLine($"{name,-15} | Compress: {compressTime.TotalMilliseconds,8:F2}ms ({compressThroughput,7:F1} MB/s) | Decompress: {decompressTime.TotalMilliseconds,8:F2}ms ({decompressThroughput,7:F1} MB/s) | Ratio: {ratio:P1}");
    }

    private static void ProfileCompressionComponents()
    {
        var payload = GenerateJsonPayload(16 * 1024, 0);
        var compressed = new byte[LZ4Codec.CompressBound(payload.Length)];
        const int iterations = 10000;

        // Warmup
        for (int i = 0; i < 1000; i++)
            LZ4Codec.CompressDefault(payload, compressed, payload.Length, compressed.Length);

        // 1. Full compression time
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            LZ4Codec.CompressDefault(payload, compressed, payload.Length, compressed.Length);
        sw.Stop();
        var fullTime = sw.Elapsed.TotalMilliseconds;

        // 2. Hash table clear overhead
        var hashTable = new uint[1 << 14];
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            hashTable.AsSpan().Clear();
        sw.Stop();
        var hashClearTime = sw.Elapsed.TotalMilliseconds;

        // 3. Simulate hash computation overhead
        sw.Restart();
        uint hash = 0;
        for (int i = 0; i < iterations; i++)
        {
            for (int j = 0; j < payload.Length - 4; j += 4)
            {
                hash ^= (uint)(payload[j] | (payload[j+1] << 8) | (payload[j+2] << 16) | (payload[j+3] << 24));
            }
        }
        sw.Stop();
        var hashComputeTime = sw.Elapsed.TotalMilliseconds;
        _ = hash; // Prevent optimization

        // 4. Memory copy overhead (literals)
        var copyDest = new byte[payload.Length];
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            Buffer.BlockCopy(payload, 0, copyDest, 0, payload.Length);
        sw.Stop();
        var memoryCopyTime = sw.Elapsed.TotalMilliseconds;

        // 5. Random memory access (simulates hash table lookups)
        var random = new Random(42);
        var positions = new int[payload.Length];
        for (int i = 0; i < positions.Length; i++)
            positions[i] = random.Next(0, hashTable.Length);
        
        sw.Restart();
        uint sum = 0;
        for (int i = 0; i < iterations; i++)
        {
            for (int j = 0; j < positions.Length; j += 8)
                sum += hashTable[positions[j]];
        }
        sw.Stop();
        var randomAccessTime = sw.Elapsed.TotalMilliseconds;
        _ = sum;

        Console.WriteLine($"Full Compression:        {fullTime,8:F2}ms (100.0%)");
        Console.WriteLine($"Hash Table Clear:        {hashClearTime,8:F2}ms ({hashClearTime/fullTime*100,5:F1}%) - BOTTLENECK #1");
        Console.WriteLine($"Hash Computation (sim):  {hashComputeTime,8:F2}ms ({hashComputeTime/fullTime*100,5:F1}%)");
        Console.WriteLine($"Memory Copy (sim):       {memoryCopyTime,8:F2}ms ({memoryCopyTime/fullTime*100,5:F1}%)");
        Console.WriteLine($"Random Access (sim):     {randomAccessTime,8:F2}ms ({randomAccessTime/fullTime*100,5:F1}%) - BOTTLENECK #2");
        Console.WriteLine($"Other (match finding):   {fullTime - hashClearTime,8:F2}ms ({(fullTime-hashClearTime)/fullTime*100,5:F1}%) - BOTTLENECK #3");
    }

    private static void ProfileDecompressionComponents()
    {
        var payload = GenerateJsonPayload(16 * 1024, 0);
        var compressed = new byte[LZ4Codec.CompressBound(payload.Length)];
        var compressedSize = LZ4Codec.CompressDefault(payload, compressed, payload.Length, compressed.Length);
        var decompressBuffer = new byte[payload.Length];
        const int iterations = 10000;

        // Warmup
        for (int i = 0; i < 1000; i++)
            LZ4Codec.DecompressSafe(compressed, decompressBuffer, compressedSize, payload.Length);

        // Full decompression time
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            LZ4Codec.DecompressSafe(compressed, decompressBuffer, compressedSize, payload.Length);
        sw.Stop();
        var fullTime = sw.Elapsed.TotalMilliseconds;

        // Simulate literal copy
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            Buffer.BlockCopy(compressed, 0, decompressBuffer, 0, compressedSize);
        sw.Stop();
        var literalCopyTime = sw.Elapsed.TotalMilliseconds;

        // Simulate overlapping copy (match copy with small offset)
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            for (int j = 100; j < decompressBuffer.Length; j++)
                decompressBuffer[j] = decompressBuffer[j - 4];
        }
        sw.Stop();
        var overlapCopyTime = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"Full Decompression:      {fullTime,8:F2}ms (100.0%)");
        Console.WriteLine($"Literal Copy (sim):      {literalCopyTime,8:F2}ms ({literalCopyTime/fullTime*100,5:F1}%)");
        Console.WriteLine($"Overlap Copy (sim):      {overlapCopyTime,8:F2}ms ({overlapCopyTime/fullTime*100,5:F1}%) - BOTTLENECK #4");
        Console.WriteLine($"Token Parsing (est):     {fullTime - literalCopyTime,8:F2}ms ({(fullTime-literalCopyTime)/fullTime*100,5:F1}%) - BOTTLENECK #5");
    }

    private static void ProfileMemoryAccess()
    {
        const int size = 16 * 1024;
        const int iterations = 100000;
        var data = new byte[size];
        new Random(42).NextBytes(data);

        // Sequential read
        var sw = Stopwatch.StartNew();
        long sum = 0;
        for (int i = 0; i < iterations; i++)
        {
            for (int j = 0; j < size; j += 8)
                sum += data[j];
        }
        sw.Stop();
        var seqTime = sw.Elapsed.TotalMilliseconds;

        // Random read (cache misses)
        var indices = new int[size / 8];
        var rng = new Random(42);
        for (int i = 0; i < indices.Length; i++)
            indices[i] = rng.Next(0, size);

        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            for (int j = 0; j < indices.Length; j++)
                sum += data[indices[j]];
        }
        sw.Stop();
        var randomTime = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"Sequential Access:       {seqTime,8:F2}ms");
        Console.WriteLine($"Random Access:           {randomTime,8:F2}ms");
        Console.WriteLine($"Random/Sequential Ratio: {randomTime/seqTime:F2}x slower");
        _ = sum;
    }

    private static void ProfileHashFunction()
    {
        const int size = 16 * 1024;
        const int iterations = 100000;
        var data = new byte[size];
        new Random(42).NextBytes(data);

        // Test hash computation speed
        var sw = Stopwatch.StartNew();
        uint hash = 0;
        for (int i = 0; i < iterations; i++)
        {
            for (int j = 0; j < size - 4; j++)
            {
                // Simulate 4-byte hash read
                uint v = (uint)(data[j] | (data[j+1] << 8) | (data[j+2] << 16) | (data[j+3] << 24));
                hash ^= v * 2654435761u;
            }
        }
        sw.Stop();
        var hashTime = sw.Elapsed.TotalMilliseconds;

        // Test using Span
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            var span = data.AsSpan();
            for (int j = 0; j < size - 4; j++)
            {
                uint v = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(j));
                hash ^= v * 2654435761u;
            }
        }
        sw.Stop();
        var spanHashTime = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"Manual Hash Read:        {hashTime,8:F2}ms");
        Console.WriteLine($"Span-based Hash Read:    {spanHashTime,8:F2}ms");
        Console.WriteLine($"Span overhead:           {spanHashTime/hashTime:F2}x");
        _ = hash;
    }

    private static byte[] GenerateJsonPayload(int targetSize, int seed)
    {
        var random = new Random(42 + seed);
        var sb = new StringBuilder();
        sb.Append('[');
        
        int itemIndex = 0;
        while (sb.Length < targetSize - 300)
        {
            if (itemIndex > 0) sb.Append(',');
            
            var item = new
            {
                id = Guid.NewGuid().ToString(),
                index = itemIndex,
                seed = seed,
                timestamp = BaseDate.AddSeconds(random.Next(0, 31536000)).ToString("o"),
                name = $"User_{random.Next(10000, 99999)}_{seed}_{itemIndex}",
                email = $"user{random.Next(1000, 9999)}@domain{random.Next(1, 100)}.com",
                active = random.Next(2) == 1,
                score = random.NextDouble() * 1000,
                balance = random.NextDouble() * 10000 - 5000,
                tags = new[] { 
                    $"tag{random.Next(1, 50)}", 
                    $"tag{random.Next(50, 100)}", 
                    $"tag{random.Next(100, 150)}" 
                },
                metadata = new
                {
                    version = $"{random.Next(1, 10)}.{random.Next(0, 20)}.{random.Next(0, 100)}",
                    region = new[] { "us-east", "us-west", "eu-west", "ap-south" }[random.Next(4)],
                    tier = new[] { "free", "basic", "pro", "enterprise" }[random.Next(4)],
                    created = BaseDate.AddDays(random.Next(0, 1000)).ToString("yyyy-MM-dd")
                }
            };
            
            sb.Append(JsonSerializer.Serialize(item));
            itemIndex++;
        }
        
        sb.Append(']');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
