# LZ4Sharp Benchmarks

This project contains performance benchmarks comparing LZ4Sharp against popular LZ4 C# NuGet packages.

## Benchmark Results

Benchmarks run on .NET 10.0.1 on AMD EPYC 7763 processor.

### Quick Benchmark Summary (Text Data)

| Method                  | DataSize | Mean       | Ratio | Allocated |
|------------------------ |--------- |-----------:|------:|----------:|
| **LZ4Sharp - Compress**   | 10 KB    |  11.666 us |  4.50 |   26.1 KB |
| K4os.LZ4 - Compress     | 10 KB    |   2.594 us |  1.00 |  10.08 KB |
| **LZ4Sharp - Decompress** | 10 KB    |   7.134 us |  2.75 |  10.02 KB |
| K4os.LZ4 - Decompress   | 10 KB    |   1.451 us |  0.56 |  10.02 KB |
|                         |          |            |       |           |
| **LZ4Sharp - Compress**   | 100 KB   | 147.434 us |  6.12 | 116.48 KB |
| K4os.LZ4 - Compress     | 100 KB   |  24.087 us |  1.00 | 100.46 KB |
| **LZ4Sharp - Decompress** | 100 KB   | 125.152 us |  5.20 | 100.04 KB |
| K4os.LZ4 - Decompress   | 100 KB   |  63.199 us |  2.62 | 100.04 KB |

### Key Findings

**Compression Performance:**
- LZ4Sharp compression is approximately **4-6x slower** than K4os.LZ4
- For 10KB data: ~878 MB/s (LZ4Sharp) vs ~3,950 MB/s (K4os.LZ4)
- For 100KB data: ~694 MB/s (LZ4Sharp) vs ~4,251 MB/s (K4os.LZ4)

**Decompression Performance:**
- LZ4Sharp decompression is approximately **2.5-5x slower** than K4os.LZ4
- For 10KB data: ~1,435 MB/s (LZ4Sharp) vs ~7,058 MB/s (K4os.LZ4)
- For 100KB data: ~818 MB/s (LZ4Sharp) vs ~1,620 MB/s (K4os.LZ4)

**Memory Allocation:**
- LZ4Sharp uses approximately **2.6x more memory** for compression
- Decompression memory usage is similar between implementations

### Analysis

The performance difference is expected because:
1. **LZ4Sharp** is a pure C# translation focused on clarity and maintainability
2. **K4os.LZ4** is a highly optimized implementation with:
   - Unsafe code and pointer operations
   - SIMD optimizations
   - Extensive performance tuning

LZ4Sharp prioritizes:
- ✅ Code clarity and readability
- ✅ Educational value
- ✅ Pure managed code (no unsafe)
- ✅ Easy to understand and modify

K4os.LZ4 prioritizes:
- ✅ Maximum performance
- ✅ Production-ready optimization
- ✅ Memory efficiency

## Running the Benchmarks

### Quick Benchmarks (Fast)
```bash
cd LZ4Sharp.Benchmarks
dotnet run -c Release -- --filter "*QuickBenchmarks*"
```

### Full Benchmarks (Comprehensive)
```bash
cd LZ4Sharp.Benchmarks
dotnet run -c Release -- --filter "*LZ4CompressionBenchmarks*"
```

### Specific Benchmark
```bash
cd LZ4Sharp.Benchmarks
dotnet run -c Release -- --filter "*Compress*" --job short
```

## Benchmark Parameters

### QuickBenchmarks
- Data sizes: 10KB, 100KB
- Data patterns: Text
- Iterations: 5 warmup, 5 measured

### LZ4CompressionBenchmarks
- Data sizes: 1KB, 10KB, 100KB, 1MB
- Data patterns: Text, Random, Repetitive
- Standard BenchmarkDotNet configuration

## Interpreting Results

- **Mean**: Average time per operation
- **Ratio**: Performance relative to baseline (K4os.LZ4)
- **Allocated**: Memory allocated per operation
- Lower is better for all metrics

## Compared Libraries

1. **LZ4Sharp** (this project)
   - Pure C# implementation
   - Educational focus
   - BSD-2-Clause License

2. **K4os.Compression.LZ4** (baseline)
   - Highly optimized production library
   - Most popular LZ4 package on NuGet
   - MIT License

## Notes

- Benchmarks run with .NET 10 and BenchmarkDotNet 0.14.0
- Results may vary based on hardware and data characteristics
- For production use, consider K4os.LZ4 for maximum performance
- Use LZ4Sharp for learning, prototyping, or when pure managed code is required
