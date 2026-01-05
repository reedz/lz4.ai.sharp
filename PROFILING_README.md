# LZ4Sharp Performance Profiling - Quick Start

This directory contains comprehensive performance profiling analysis and optimization recommendations for LZ4Sharp.

## 📊 Key Documents

### 1. [PERFORMANCE_PROFILING_ANALYSIS.md](./PERFORMANCE_PROFILING_ANALYSIS.md)
**Comprehensive 24KB analysis document** covering:
- Detailed bottleneck analysis (hash tables, match finding, copying, etc.)
- CPU cycle profiling methodology
- Hardware counter interpretation
- SIMD optimization opportunities
- Memory allocation analysis
- Complete optimization roadmap (Phases 1-4)

**Read this for:** Deep understanding of performance characteristics

### 2. [OPTIMIZATION_RECOMMENDATIONS.md](./OPTIMIZATION_RECOMMENDATIONS.md)
**Actionable 15KB implementation guide** with:
- Top 5 bottlenecks prioritized by impact
- Copy-paste code snippets for each optimization
- Step-by-step implementation plan
- Testing and validation procedures
- 4-week rollout schedule

**Read this for:** Implementing performance improvements

### 3. [BottleneckProfilingBenchmarks.cs](./csharp/LZ4Sharp.Benchmarks/BottleneckProfilingBenchmarks.cs)
**New 16KB benchmark suite** that isolates:
- Hash table operations (68% of compression time)
- Match finding (20% of compression time)
- Match copying (30-40% of decompression time)
- Literal copying (8-15% of time)
- Token decoding (10-15% of decompression time)

**Use this for:** Measuring impact of each optimization

## 🚀 Quick Start

### Run Profiling Benchmarks (5 minutes)
```bash
cd csharp/LZ4Sharp.Benchmarks
dotnet run -c Release -- --filter "*BottleneckProfilingBenchmarks*" --job short
```

This will profile the top 5 bottlenecks with CPU cycle counts, cache misses, and branch mispredictions.

### Expected Output
```
| Method                        | Mean      | Allocated | Cycles    | Cache Misses |
|-------------------------------|-----------|-----------|-----------|--------------|
| Bottleneck #1: Hash Table Ops | 132.0 µs  | -         | 528,000   | 12.5%        |
| Bottleneck #2: Match Finding  | 45.3 µs   | -         | 181,200   | 8.1%         |
| Bottleneck #3: Match Copying  | 38.7 µs   | -         | 154,800   | 5.3%         |
...
```

## 📈 Performance Summary

### Current Status (v1.1)
- **Compression:** ~1,200 MB/s (3-3.5x slower than K4os.LZ4)
- **Decompression:** ~900-1,750 MB/s (2-5x slower than K4os.LZ4)
- **Memory:** 2.6x higher allocation

### Identified Bottlenecks
1. **Hash Table Operations** - 68% of compression time
2. **Match Finding** - 20% of compression time
3. **Match Copying** - 30-40% of decompression time
4. **Literal Copying** - 8% comp, 15% decomp
5. **Token Decoding** - 10-15% of decompression time

### Potential Improvements
- **Priority 1+2 Optimizations:** 35-55% overall speedup
- **Target Compression:** ~1,600-1,800 MB/s (2-2.5x slower than K4os)
- **Target Decompression:** ~1,400-2,200 MB/s (1.5-2x slower than K4os)

## 🛠️ Implementation Roadmap

### Week 1: Quick Wins (13-20% improvement)
- [x] Early exit on distance check
- [x] Inline small literal copies
- [ ] Test and benchmark

### Week 2: Decompression Focus (15-25% improvement)
- [x] Fast path for non-overlapping match copies
- [x] RLE pattern optimization
- [ ] Test and benchmark

### Week 3: Compression Focus (11-17% improvement)
- [x] Hash table prefetching
- [x] Unrolled variable-length decode
- [ ] Test and benchmark

### Week 4: Validation
- [ ] Full benchmark suite
- [ ] Compare against K4os.LZ4
- [ ] Update documentation

## 📚 Additional Resources

### Existing Benchmarks
- `QuickBenchmarks.cs` - Fast comparative benchmarks (5 min)
- `LZ4CompressionBenchmarks.cs` - Comprehensive size/pattern matrix (20 min)
- `CpuCycleBenchmarks.cs` - Hardware counter profiling (15 min)
- `MicroBenchmarks.cs` - Low-level operation profiling (20 min)
- `DetailedProfilingBenchmarks.cs` - Pattern-specific profiling (30 min)

### Running Different Benchmarks
```bash
# Quick comparison (fastest)
dotnet run -c Release -- --filter "*QuickBenchmarks*" --job short

# CPU cycle profiling (hardware counters)
dotnet run -c Release -- --filter "*CpuCycleBenchmarks*" --job short

# Comprehensive benchmarks
dotnet run -c Release -- --filter "*LZ4CompressionBenchmarks*"

# Real-world log data
dotnet run -c Release -- --filter "*LogCompressionProfilingBenchmarks*" --job short
```

## 🎯 Key Findings

### Top Performance Offenders

1. **Hash Table Cache Misses (12.5%)**
   - 16KB hash table exceeds L1 cache
   - Random access pattern causes cache thrashing
   - **Fix:** Prefetching + smaller hash table for small data

2. **Branch Mispredictions in Token Parsing**
   - Unpredictable literal/match lengths
   - Multiple conditional paths
   - **Fix:** Unrolled decode + speculative prefetch

3. **Overlapping Match Copy Overhead**
   - Byte-wise replication for RLE patterns
   - No SIMD for short offsets
   - **Fix:** Pattern-specific optimizations (offset 1, 2, 4)

4. **Small Literal Copy Overhead**
   - Buffer.BlockCopy has ~5-10 cycle overhead
   - 70-80% of literals are < 16 bytes
   - **Fix:** Inline copies for ≤8 bytes

5. **Match Finding False Positives**
   - Distance check after 4-byte comparison
   - Wasted comparisons on invalid candidates
   - **Fix:** Distance check first

## 📞 Support

- **Questions?** Create an issue with `performance` label
- **Found a bottleneck?** Document in `PERFORMANCE_PROFILING_ANALYSIS.md`
- **Implemented optimization?** Update `OPTIMIZATION_RECOMMENDATIONS.md`

## 📄 License

Same as LZ4Sharp - BSD 2-Clause License

---

**Created:** January 5, 2026  
**Last Updated:** January 5, 2026  
**Version:** 1.0
