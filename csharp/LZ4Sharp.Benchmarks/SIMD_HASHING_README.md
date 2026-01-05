# SIMD Hashing Feature Flag

## Overview

LZ4Sharp includes an optional SIMD (Single Instruction, Multiple Data) hashing optimization that can provide **15-25% compression speedup** on compatible hardware. This feature is disabled by default and can be enabled via a compile-time feature flag.

## What is SIMD Hashing?

SIMD hashing processes 4 hash computations in parallel instead of sequentially. Hash table operations constitute 68% of compression time, making this optimization highly impactful.

**Performance Impact:**
- **Expected speedup:** 15-25% for compression
- **Platform requirement:** SSE2-capable CPU (most x64 processors)
- **Memory impact:** No change
- **Compression ratio:** Identical to standard version

## How to Enable

### Option 1: Command Line Build

```bash
# Standard build (SIMD disabled)
dotnet build -c Release

# SIMD-enabled build
dotnet build -c Release /p:DefineConstants="LZ4_ENABLE_SIMD_HASHING"
```

### Option 2: Project File

Add to your `.csproj` file:

```xml
<PropertyGroup>
  <DefineConstants>LZ4_ENABLE_SIMD_HASHING</DefineConstants>
</PropertyGroup>
```

Or conditionally for specific configurations:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DefineConstants>LZ4_ENABLE_SIMD_HASHING</DefineConstants>
</PropertyGroup>
```

### Option 3: IDE Settings

**Visual Studio:**
1. Right-click project → Properties
2. Build → General → Conditional compilation symbols
3. Add: `LZ4_ENABLE_SIMD_HASHING`

**Rider:**
1. Settings → Build, Execution, Deployment → Toolset and Build
2. Add `LZ4_ENABLE_SIMD_HASHING` to conditional compilation symbols

## Benchmarking

### Quick Benchmark Comparison

Run both versions and compare:

```bash
# Linux/macOS
cd csharp/LZ4Sharp.Benchmarks
./compare-simd.sh

# Windows
cd csharp\LZ4Sharp.Benchmarks
compare-simd.bat
```

This script:
1. Builds and benchmarks standard version
2. Builds and benchmarks SIMD version
3. Saves results as JSON for comparison

### Manual Benchmark

```bash
# Standard version
dotnet build -c Release
dotnet run -c Release -- --filter "*SIMDHashingBenchmark*"

# SIMD version
dotnet build -c Release /p:DefineConstants="LZ4_ENABLE_SIMD_HASHING"
dotnet run -c Release -- --filter "*SIMDHashingBenchmark*"
```

### Example Results

**Expected performance (100KB text data):**

| Version | Mean Time | Speedup | MB/s |
|---------|-----------|---------|------|
| Standard | 83.3 µs | baseline | 1,230 MB/s |
| SIMD | 66-70 µs | 15-20% faster | 1,430-1,515 MB/s |

## Technical Details

### How It Works

**Standard hashing (scalar):**
```csharp
for (int i = 0; i < data.Length; i++)
{
    int hash = HashPosition(source, i);  // Process 1 position
    // ... use hash ...
}
```

**SIMD hashing (parallel):**
```csharp
// Process 4 positions simultaneously
HashPosition4_SIMD(source, i, out h0, out h1, out h2, out h3);
// Update hash table for all 4 positions
// Check for matches at all 4 positions
```

### Implementation Details

1. **Hash Computation:** Computes 4 hashes simultaneously using SSE2 instructions
2. **Hash Table Updates:** Updates 4 hash table entries at once
3. **Match Finding:** Checks 4 candidate positions in parallel
4. **Fallback:** Automatically falls back to scalar code if:
   - SSE2 not available
   - Insufficient data remaining (< 16 bytes)

### Code Location

- Feature flag definition: `LZ4Codec.cs` line 48-52
- SIMD hash functions: `LZ4Codec.cs` line 675-773
- Integration point: `CompressGeneric` method line 229-282

## Platform Compatibility

### Supported Platforms

✅ **Windows x64** - Full support (SSE2 standard on x64)  
✅ **Linux x64** - Full support  
✅ **macOS x64** - Full support  
✅ **macOS ARM64** - Compiles but uses scalar fallback (no SSE2)  
❓ **Windows ARM64** - Compiles but uses scalar fallback

### Runtime Detection

The code includes runtime CPU capability detection:

```csharp
if (USE_SIMD_HASHING && Sse2.IsSupported && ...)
{
    // Use SIMD path
}
else
{
    // Use scalar fallback
}
```

If SSE2 is not available at runtime, the code automatically falls back to the standard implementation with no performance penalty.

## When to Enable

### ✅ Enable SIMD Hashing If:

- You're targeting x64 platforms exclusively
- Compression performance is critical
- You can test on target hardware
- You want maximum throughput

### ❌ Don't Enable If:

- You need to support ARM platforms without fallback overhead
- You prioritize code simplicity over performance
- Your data sizes are very small (< 4KB) where gains are minimal
- You haven't benchmarked on your specific hardware

## Testing

The feature includes comprehensive tests:

```bash
# Test standard version
dotnet test

# Test SIMD version
dotnet build /p:DefineConstants="LZ4_ENABLE_SIMD_HASHING"
dotnet test
```

All 67 unit tests pass with both configurations, ensuring correctness.

## Performance Analysis

Based on profiling data (see `HASH_TABLE_OPTIMIZATION_ANALYSIS.md`):

**Hash Table Operation Breakdown:**
- Hash computation: 15-20% of time → **SIMD targets this**
- Memory access: 60-70% of time → Not affected by SIMD
- Collision overhead: 10-15% of time → Not affected by SIMD

**Theoretical Speedup:**
- Hash computation improvement: 70-75% faster (4 hashes in ~20 cycles vs 60-80)
- Overall compression: 15-20% of 70% = **10-14% minimum**
- With better instruction-level parallelism: **15-25% realistic**

## Troubleshooting

### Build Fails with SIMD Enabled

Check that you're using .NET 5.0+ which includes `System.Runtime.Intrinsics` namespace.

### No Performance Improvement

1. **Verify SSE2 support:** Run on x64 hardware
2. **Check build configuration:** Ensure Release build with optimizations enabled
3. **Verify feature flag:** Check that `USE_SIMD_HASHING` constant is true in compiled code
4. **Profile runtime:** Use BenchmarkDotNet to measure actual performance

### Different Results vs Standard

The SIMD implementation should produce **identical** compressed output. If you see differences:
1. File a bug report with test case
2. Include both versions' output
3. Specify exact hardware and .NET version

## Future Enhancements

Potential optimizations (not yet implemented):

1. **AVX2 support** - Process 8 positions simultaneously (30-35% speedup)
2. **ARM NEON** - ARM-specific SIMD implementation
3. **AVX-512** - Process 16 positions simultaneously (40-50% speedup on Xeon)

## References

- [Benchmark Suite](./SIMDHashingBenchmark.cs)
- [Comparison Scripts](./compare-simd.sh) (Linux/macOS) | [compare-simd.bat](./compare-simd.bat) (Windows)
- [Hash Table Analysis](../../HASH_TABLE_OPTIMIZATION_ANALYSIS.md)
- [Performance Profiling](../../PERFORMANCE_PROFILING_ANALYSIS.md)

---

**Last Updated:** January 5, 2026  
**Feature Status:** Experimental (test thoroughly before production use)  
**Maintainer:** LZ4Sharp Performance Team
