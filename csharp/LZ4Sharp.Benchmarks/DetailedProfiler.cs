using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LZ4Sharp.Benchmarks;

public static class DetailedProfiler
{
    public static void Run()
    {
        Console.WriteLine("=== LZ4Sharp Detailed Algorithm Profiling ===\n");
        
        // Test with different JSON sizes
        var sizes = new[] { 1024, 7168, 16384, 73728 };
        
        foreach (var size in sizes)
        {
            ProfileSize(size);
        }
        
        Console.WriteLine("\n=== Improvement Suggestions ===\n");
        PrintSuggestions();
    }
    
    static void ProfileSize(int targetSize)
    {
        // Generate test data
        var json = GenerateJson(targetSize);
        var data = Encoding.UTF8.GetBytes(json);
        var output = new byte[LZ4Codec.CompressBound(data.Length)];
        
        // Warm up
        for (int i = 0; i < 100; i++)
            LZ4Codec.CompressDefault(data, output, data.Length, output.Length);
        
        // Profile compression in detail
        var sw = new Stopwatch();
        const int iterations = 1000;
        
        // Measure total compression time
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            LZ4Codec.CompressDefault(data, output, data.Length, output.Length);
        sw.Stop();
        var totalMs = sw.Elapsed.TotalMilliseconds;
        
        // Measure hash computation overhead (simulate)
        sw.Restart();
        long hashOps = 0;
        unsafe
        {
            fixed (byte* ptr = data)
            {
                for (int iter = 0; iter < iterations; iter++)
                {
                    for (int i = 0; i < data.Length - 8; i++)
                    {
                        // Simulate hash computation
                        var hash = HashSimulate(ptr + i);
                        hashOps++;
                    }
                }
            }
        }
        sw.Stop();
        var hashMs = sw.Elapsed.TotalMilliseconds;
        
        // Measure memory copy overhead
        var copyDest = new byte[data.Length];
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            Buffer.BlockCopy(data, 0, copyDest, 0, data.Length);
        sw.Stop();
        var copyMs = sw.Elapsed.TotalMilliseconds;
        
        // Measure comparison overhead
        sw.Restart();
        long compareOps = 0;
        unsafe
        {
            fixed (byte* ptr = data)
            {
                for (int iter = 0; iter < iterations; iter++)
                {
                    for (int i = 0; i < data.Length - 100; i += 50)
                    {
                        var len = CountMatch(ptr + i, ptr + i + 10, ptr + data.Length);
                        compareOps++;
                    }
                }
            }
        }
        sw.Stop();
        var compareMs = sw.Elapsed.TotalMilliseconds;
        
        // Calculate percentages
        var throughput = (double)data.Length * iterations / totalMs / 1000.0; // MB/s
        
        Console.WriteLine($"Size: {data.Length / 1024}KB | Total: {totalMs:F2}ms | Throughput: {throughput:F1} MB/s");
        Console.WriteLine($"  Hash overhead estimate:    {hashMs:F2}ms ({hashMs / totalMs * 100:F1}% of total)");
        Console.WriteLine($"  Memory copy baseline:      {copyMs:F2}ms ({copyMs / totalMs * 100:F1}% of total)");
        Console.WriteLine($"  Compare overhead estimate: {compareMs:F2}ms ({compareMs / totalMs * 100:F1}% of total)");
        Console.WriteLine($"  Remaining (encoding/etc):  {totalMs - hashMs - copyMs - compareMs:F2}ms");
        Console.WriteLine();
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe uint HashSimulate(byte* ptr)
    {
        // Simulate the hash function
        ulong val = *(ulong*)ptr;
        return (uint)((val * 889523592379UL) >> (64 - 16));
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe int CountMatch(byte* p1, byte* p2, byte* limit)
    {
        byte* start = p1;
        while (p1 + 8 <= limit)
        {
            ulong diff = *(ulong*)p1 ^ *(ulong*)p2;
            if (diff != 0)
                return (int)(p1 - start) + (System.Numerics.BitOperations.TrailingZeroCount(diff) >> 3);
            p1 += 8;
            p2 += 8;
        }
        return (int)(p1 - start);
    }
    
    static string GenerateJson(int targetSize)
    {
        var sb = new StringBuilder();
        sb.Append("{\"items\":[");
        int i = 0;
        while (sb.Length < targetSize - 100)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"{{\"id\":{i},\"name\":\"Item {i}\",\"value\":{i * 1.5:F2},\"active\":{(i % 2 == 0).ToString().ToLower()}}}");
            i++;
        }
        sb.Append("]}");
        return sb.ToString();
    }
    
    static void PrintSuggestions()
    {
        Console.WriteLine("TOP 5 BOTTLENECKS AND IMPROVEMENT SUGGESTIONS:");
        Console.WriteLine();
        
        Console.WriteLine("1. HASH TABLE MEMORY ACCESS (High Impact)");
        Console.WriteLine("   Problem: Random memory access to hash table causes cache misses");
        Console.WriteLine("   Current: 16-bit hash (64KB table) or 14-bit hash (16KB table)");
        Console.WriteLine("   Suggestions:");
        Console.WriteLine("   - Use smaller hash table (12-bit = 4KB) for small inputs to fit in L1 cache");
        Console.WriteLine("   - Prefetch next hash table entry while processing current match");
        Console.WriteLine("   - Use cache-line aligned hash table");
        Console.WriteLine();
        
        Console.WriteLine("2. MATCH FINDING LOOP (High Impact)");
        Console.WriteLine("   Problem: Linear probing with step increase causes unpredictable branches");
        Console.WriteLine("   Current: Step doubles every (1 << LZ4_SKIP_TRIGGER) iterations");
        Console.WriteLine("   Suggestions:");
        Console.WriteLine("   - Try fixed step size for more predictable branches");
        Console.WriteLine("   - Use SIMD to compare multiple positions at once");
        Console.WriteLine("   - Implement lazy match evaluation");
        Console.WriteLine();
        
        Console.WriteLine("3. MATCH EXTENSION (Medium Impact)");
        Console.WriteLine("   Problem: Already optimized with AVX2, but still significant time");
        Console.WriteLine("   Current: AVX2 32-byte compare, then 8-byte XOR");
        Console.WriteLine("   Suggestions:");
        Console.WriteLine("   - Unroll AVX2 loop for 64/128 bytes at once");
        Console.WriteLine("   - Use aligned loads when possible");
        Console.WriteLine("   - Consider Vector<byte> for platform-agnostic SIMD");
        Console.WriteLine();
        
        Console.WriteLine("4. LITERAL/MATCH ENCODING (Medium Impact)");
        Console.WriteLine("   Problem: Length encoding has many conditional branches");
        Console.WriteLine("   Current: While loops for lengths >= 255");
        Console.WriteLine("   Suggestions:");
        Console.WriteLine("   - Precompute length encoding tables");
        Console.WriteLine("   - Use branchless encoding for common cases (lengths < 510)");
        Console.WriteLine("   - Batch output writes");
        Console.WriteLine();
        
        Console.WriteLine("5. DATA COPY (Lower Impact)");
        Console.WriteLine("   Problem: WildCopy8 works but could be faster for specific sizes");
        Console.WriteLine("   Current: 8-byte copies in a loop");
        Console.WriteLine("   Suggestions:");
        Console.WriteLine("   - Use Vector256 for copies >= 32 bytes");
        Console.WriteLine("   - Use rep movsb for very large copies (CPU optimized)");
        Console.WriteLine("   - Align destination for better write performance");
        Console.WriteLine();
        
        Console.WriteLine("ADDITIONAL OPTIMIZATION IDEAS:");
        Console.WriteLine("   - Use hardware prefetch instructions (Sse.Prefetch)");
        Console.WriteLine("   - Reorder code to minimize branch mispredictions");
        Console.WriteLine("   - Profile with CPU performance counters for cache misses");
        Console.WriteLine("   - Consider parallel compression for large inputs");
    }
}
