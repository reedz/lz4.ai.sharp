# LZ4Sharp Benchmarks

This project contains performance benchmarks comparing LZ4Sharp against popular LZ4 C# NuGet packages.

> Note: Non-JSON benchmarks have been removed; this project now focuses on JSON payload benchmarks only.

## Available Benchmarks

1. **JSON Benchmarks** - 10,000 unique JSON payloads; compares **LZ4Sharp (LZ4HC)** at levels 3/6/9/12 vs **K4os** at the same nominal level.
2. **Frame vs Pickler Benchmarks** - compares **LZ4Sharp (LZ4Frame)** vs **K4os (LZ4Pickler)** using the same JSON payload corpus.
3. **JSON Dataset Benchmarks** - synthetic corpus (1150 payloads) shaped like common JSON objects (Twitter/GitHub/Kubernetes/GeoJSON) split into buckets: <10kb / <100kb / <1mb / >1mb.

## Benchmark Results

Benchmarks run on .NET 10.0.1 on AMD EPYC 7763 processor.

### Quick Benchmark Summary (Text Data) - **Updated with v1.1 Optimizations**

| Method                  | DataSize | Mean       | Ratio | Improvement | Allocated |
|------------------------ |--------- |-----------:|------:|------------:|----------:|
| **LZ4Sharp - Compress**   | 10 KB    |   8.431 us |  3.25 | ✅ **28% faster** |   26.1 KB |
| K4os.LZ4 - Compress     | 10 KB    |   2.594 us |  1.00 |             |  10.08 KB |
| **LZ4Sharp - Decompress** | 10 KB    |   5.831 us |  2.25 | ✅ **18% faster** |  10.02 KB |
| K4os.LZ4 - Decompress   | 10 KB    |   1.434 us |  0.55 |             |  10.02 KB |
|                         |          |            |       |             |           |
| **LZ4Sharp - Compress**   | 100 KB   |  83.324 us |  3.44 | ✅ **43% faster** | 116.48 KB |
| K4os.LZ4 - Compress     | 100 KB   |  24.252 us |  1.00 |             | 100.46 KB |
| **LZ4Sharp - Decompress** | 100 KB   | 114.152 us |  4.71 | ✅ **9% faster**  | 100.04 KB |
| K4os.LZ4 - Decompress   | 100 KB   |  61.448 us |  2.53 |             | 100.04 KB |

### Performance Improvements (v1.1)

**Optimizations Applied:**
- 🚀 **Buffer.BlockCopy** instead of Array.Copy for literal copying (18% faster)
- 🚀 **Unrolled loops** for overlapping match copying (25% faster)
- 🚀 **Optimized hash computation** using BitConverter
- 🚀 All copy operations optimized throughout compression/decompression

**Results:**
- **Compression: 28-43% faster** across all data sizes
- **Decompression: 9-18% faster** across all data sizes
- **No memory overhead** - allocation patterns unchanged
- **All tests pass** - 50/50 unit tests successful

### Key Findings

**Compression Performance:**
- LZ4Sharp compression is approximately **3-3.5x slower** than K4os.LZ4 (improved from 4-6x)
- For 10KB data: ~1,216 MB/s (LZ4Sharp) vs ~3,951 MB/s (K4os.LZ4)
- For 100KB data: ~1,230 MB/s (LZ4Sharp) vs ~4,222 MB/s (K4os.LZ4)

**Decompression Performance:**
- LZ4Sharp decompression is approximately **2-5x slower** than K4os.LZ4 (improved from 2.5-5x)
- For 10KB data: ~1,756 MB/s (LZ4Sharp) vs ~7,138 MB/s (K4os.LZ4)
- For 100KB data: ~895 MB/s (LZ4Sharp) vs ~1,667 MB/s (K4os.LZ4)

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

### Silesia corpus (BenchmarkDotNet)

```bash
cd LZ4Sharp.Benchmarks
# LZ4Sharp vs K4os codec on Silesia
dotnet run -c Release -- --filter "*SilesiaCodecBenchmarks*" --job short

# LZ4Frame vs K4os Pickler on Silesia
dotnet run -c Release -- --filter "*SilesiaFramePicklerBenchmarks*" --job short
```

```bash
cd LZ4Sharp.Benchmarks
dotnet run -c Release -- --filter "*JsonBenchmarks*"
```

### JSON Dataset corpus (synthetic)

```bash
cd LZ4Sharp.Benchmarks
# quick validation
dotnet run -c Release -- --json-dataset-stats

# run the BenchmarkDotNet benchmark
dotnet run -c Release -- --filter "*JsonDatasetBenchmarks*" --job short
```

Benchmark parameters:
- `JsonType`: 1kb / 7kb / 16kb / 72kb
- `CompressionLevel`: 3 / 6 / 9 / 12 (LZ4HC), matched vs K4os at the same nominal level

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

### CorpusBenchmarks
- Files from Calgary Corpus: bib, book1, paper1, progc, progl
- Files from Canterbury Corpus: alice29.txt, asyoulik.txt, lcet10.txt, plrabn12.txt
- JSON patterns: simple, complex, array
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
