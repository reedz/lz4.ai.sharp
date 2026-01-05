# SIMD Hashing Benchmark Results

## Executive Summary

Benchmarked SIMD hashing vs standard scalar hashing using the new runtime `LZ4Options` parameter. **Unexpected result: SIMD is actually slower on this platform for most data sizes.**

## Test Environment

- **OS:** Ubuntu 24.04.3 LTS (Noble Numbat)
- **CPU:** AMD EPYC 7763, 1 CPU, 2 logical cores
- **.NET:** 10.0.1 (10.0.125.57005), X64 RyuJIT AVX2
- **Hardware Intrinsics:** AVX2, AES, BMI1, BMI2, FMA, LZCNT, PCLMUL, POPCNT (VectorSize=256)

## Benchmark Results

### Detailed Comparison (10 iterations with warmup)

| Data Size | Standard (µs) | SIMD (µs) | Difference | Winner |
|-----------|---------------|-----------|------------|--------|
| **10KB** | 1.105 ± 0.032 | 1.184 ± 0.022 | **+7%** ⚠️ | Standard faster |
| **100KB** | 3.944 ± 0.060 | 4.575 ± 0.111 | **+16%** ⚠️ | Standard faster |
| **1MB** | 40.008 ± 1.540 | 39.378 ± 1.000 | **-2%** ✓ | SIMD slightly faster |

### Summary Table

```
| Method                     | Mean      | Error     | StdDev    | Ratio | Allocated |
|--------------------------- |----------:|----------:|----------:|------:|----------:|
| '10KB - Standard Hashing'  |  1.105 us | 0.0317 us | 0.0209 us |  1.00 |         - |
| '10KB - SIMD Hashing'      |  1.184 us | 0.0216 us | 0.0143 us |  1.07 |     320 B |
|                            |           |           |           |       |           |
| '100KB - Standard Hashing' |  3.944 us | 0.0601 us | 0.0357 us |  1.00 |         - |
| '100KB - SIMD Hashing'     |  4.575 us | 0.1110 us | 0.0734 us |  1.16 |         - |
|                            |           |           |           |       |           |
| '1MB - Standard Hashing'   | 40.008 us | 1.5404 us | 0.9167 us |  1.00 |         - |
| '1MB - SIMD Hashing'       | 39.378 us | 1.0001 us | 0.5951 us |  0.98 |     320 B |
```

## Analysis

### Why is SIMD Slower?

Several factors contribute to the unexpected performance:

#### 1. **Runtime Branch Overhead**
```csharp
// Every compression now has this check:
if (options.UseSIMDHashing && Sse2.IsSupported && forwardPos + 16 <= srcSize)
{
    // SIMD path
}
else
{
    // Scalar path
}
```
This adds branch prediction overhead compared to compile-time selection.

#### 2. **Non-Vectorized Multiplication**
Current implementation doesn't use true SIMD for multiplication:
```csharp
// Not actually vectorized - scalar operations:
uint m0 = v0 * 2654435761u;
uint m1 = v1 * 2654435761u;
uint m2 = v2 * 2654435761u;
uint m3 = v3 * 2654435761u;
```

SSE2 doesn't have native 32-bit multiply. A true SIMD implementation would use AVX2's `_mm256_mullo_epi32`.

#### 3. **Small Data Penalty**
For 10KB and 100KB data:
- SIMD setup cost (checking SSE2 support, bounds checking)
- Fewer total hash operations to amortize overhead
- Cache behavior may favor simpler code paths

#### 4. **Memory Allocation**
SIMD path shows 320B allocation (likely for array initialization in ProcessPositions4_SIMD).

#### 5. **Cache Effects**
Processing 4 positions at once may have different cache behavior than sequential scalar processing.

### Why Does 1MB Show Improvement?

At 1MB, the **slight** 2% improvement suggests:
- More hash operations to amortize setup cost
- Better instruction-level parallelism for larger datasets
- **But** improvement is within error margin (±1.5µs standard, ±1.0µs SIMD)

## Recommendations

### 1. Keep SIMD as Optional (Current Implementation) ✅

**Pros:**
- No breaking changes
- Users can choose based on profiling
- Platform-specific optimization possible

**Default:** Standard hashing (UseSIMDHashing = false)

### 2. Add Adaptive Selection (Future Enhancement)

```csharp
public record LZ4Options
{
    public bool UseSIMDHashing { get; init; } = false;
    public bool AutoSelectSIMD { get; init; } = false; // NEW
    
    internal bool ShouldUseSIMD(int dataSize)
    {
        if (!AutoSelectSIMD) return UseSIMDHashing;
        
        // Only enable SIMD for large data where it might help
        return Sse2.IsSupported && dataSize >= 1024 * 1024; // ≥ 1MB
    }
}
```

### 3. Optimize SIMD Implementation (Future Work)

**Use AVX2 for true vectorization:**
```csharp
if (Avx2.IsSupported)
{
    // Load 4 x UInt32
    var values = Vector256.Create(v0, v1, v2, v3);
    var constant = Vector256.Create(2654435761u);
    
    // Vectorized multiply (4 operations in one instruction)
    var products = Avx2.MultiplyLow(values, constant);
    
    // Vectorized shift
    var hashes = Avx2.ShiftRightLogical(products, 20);
    
    // Extract results
    h0 = (int)hashes.GetElement(0);
    h1 = (int)hashes.GetElement(1);
    h2 = (int)hashes.GetElement(2);
    h3 = (int)hashes.GetElement(3);
}
```

**Expected improvement:** 10-15% if properly vectorized

### 4. Eliminate Runtime Checks in Hot Path

```csharp
// Instead of checking every iteration:
bool useSIMD = options.UseSIMDHashing && Sse2.IsSupported;

while (srcPos < srcLimit)
{
    if (useSIMD && forwardPos + 16 <= srcSize)
    {
        // SIMD path
    }
    else
    {
        // Scalar path
    }
}
```

### 5. Profile on Multiple Platforms

Test on:
- **Intel CPUs** (may have different AVX2 performance)
- **ARM with NEON** (would need separate implementation)
- **Different data patterns** (random vs repetitive vs structured)

## Conclusion

**Current Status:**
- ✅ SIMD implementation works correctly (passes all tests)
- ✅ Runtime configuration successful
- ⚠️ Performance is worse on AMD EPYC for small/medium data
- ✓ Marginal improvement for large data (1MB+)

**Recommendation:**
1. **Keep as optional feature** (default: disabled)
2. **Document performance characteristics** clearly
3. **Consider adaptive selection** based on data size
4. **Optimize with AVX2** for better results
5. **Benchmark on target platforms** before enabling

**For most users:** Standard hashing provides best performance on this hardware.

---

## How to Run Benchmark

```bash
cd csharp/LZ4Sharp.Benchmarks
dotnet run -c Release -- --filter "*SIMDComparisonBenchmark*" --job short
```

## Full Benchmark Output

See: `/tmp/simd_benchmark_output.txt` (generated during benchmark run)

---

**Date:** January 5, 2026  
**Benchmark Version:** BenchmarkDotNet v0.14.0  
**Test Duration:** ~2 minutes  
**Iterations:** 10 (with 3 warmup)
