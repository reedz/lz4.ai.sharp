# LZ4Sharp Optimization Plan Based on CPU Cycle Analysis

**Date**: January 4, 2026  
**Repository**: reedz/lz4.ai.sharp  
**Based On**: CPU_CYCLE_ANALYSIS.md  
**Status**: Planning Document

---

## Executive Summary

This document outlines a phased optimization plan for LZ4Sharp based on theoretical CPU cycle analysis. The plan is designed to systematically approach theoretical performance limits while managing complexity and safety tradeoffs.

**Current State** (100KB compression):
- Performance: 43.9 µs (2,333 MB/s)
- Gap to theoretical: 3.7-5.5x slower than theoretical minimum (8-12 µs)
- Gap to K4os.LZ4: 2.5x slower (K4os: 17.6 µs, 5,800 MB/s)

**Target State** (after all planned optimizations):
- Performance: 15-25 µs (4,000-6,700 MB/s)
- Gap to theoretical: 1.5-2.5x slower (acceptable for managed code)
- Gap to K4os.LZ4: 0.85-1.4x (competitive)

---

## Phase 1: Safe Optimizations (Immediate - Q1 2026)

**Goal**: Achieve 15-25% performance improvement without compromising safety or readability

### 1.1 UInt64 Comparisons in Match Finding

**Current Implementation**:
```csharp
if (length == 4 && pos1 <= source.Length - 4 && pos2 <= source.Length - 4)
{
    uint val1 = BitConverter.ToUInt32(source, pos1);
    uint val2 = BitConverter.ToUInt32(source, pos2);
    return val1 == val2;
}
```

**Proposed Optimization**:
```csharp
// Add 8-byte fast path
if (length == 8 && pos1 <= source.Length - 8 && pos2 <= source.Length - 8)
{
    ulong val1 = BitConverter.ToUInt64(source, pos1);
    ulong val2 = BitConverter.ToUInt64(source, pos2);
    return val1 == val2;
}
// Keep existing 4-byte fast path
else if (length == 4 && pos1 <= source.Length - 4 && pos2 <= source.Length - 4)
{
    uint val1 = BitConverter.ToUInt32(source, pos1);
    uint val2 = BitConverter.ToUInt32(source, pos2);
    return val1 == val2;
}
```

**Changes Required**:
- File: `LZ4Codec.cs`
- Methods: `AreEqual()`, `CountMatch()`
- Lines to modify: ~20 lines
- Risk: Low (same pattern as UInt32)

**Expected Impact**:
- Compression speedup: 8-12%
- Decompression: Minimal change (not a bottleneck)
- Estimated new performance: 39-40 µs (100KB)

**Testing**:
- Unit tests: All existing tests must pass
- Benchmark: Compare before/after with CpuCycleBenchmarks
- Cycle reduction: Expect ~3,000-5,000 fewer cycles per 100KB

**Timeline**: 2-3 days
- Day 1: Implement and test UInt64 in AreEqual()
- Day 2: Implement UInt64 in CountMatch()
- Day 3: Benchmark, validate, document

**Success Criteria**:
- [ ] All 58 unit tests pass
- [ ] 8-12% compression speedup measured
- [ ] No regression in decompression
- [ ] Cycle count reduced by 3,000-5,000 cycles (100KB)

---

### 1.2 Improved CountMatch Loop Structure

**Current Implementation**:
```csharp
while (pos2 + 4 <= limit && pos1 <= source.Length - 4 && pos2 <= source.Length - 4)
{
    uint val1 = BitConverter.ToUInt32(source, pos1);
    uint val2 = BitConverter.ToUInt32(source, pos2);
    if (val1 != val2)
        break;
    pos1 += 4;
    pos2 += 4;
    count += 4;
}
```

**Proposed Optimization**:
```csharp
// Try 8-byte chunks first (UInt64)
while (pos2 + 8 <= limit && pos1 <= source.Length - 8 && pos2 <= source.Length - 8)
{
    ulong val1 = BitConverter.ToUInt64(source, pos1);
    ulong val2 = BitConverter.ToUInt64(source, pos2);
    if (val1 != val2)
        break;
    pos1 += 8;
    pos2 += 8;
    count += 8;
}

// Then 4-byte chunks for remainder
while (pos2 + 4 <= limit && pos1 <= source.Length - 4 && pos2 <= source.Length - 4)
{
    uint val1 = BitConverter.ToUInt32(source, pos1);
    uint val2 = BitConverter.ToUInt32(source, pos2);
    if (val1 != val2)
        break;
    pos1 += 4;
    pos2 += 4;
    count += 4;
}

// Handle remaining bytes (unchanged)
```

**Expected Impact**:
- Additional 3-5% compression speedup
- Especially effective for long matches (repetitive data)

**Timeline**: 1 day (can be combined with 1.1)

---

### 1.3 Enable Profile-Guided Optimization (PGO)

**Current Build Configuration**:
```xml
<PropertyGroup>
    <Configuration>Release</Configuration>
    <Optimize>true</Optimize>
</PropertyGroup>
```

**Proposed Configuration**:
```xml
<PropertyGroup>
    <Configuration>Release</Configuration>
    <Optimize>true</Optimize>
    <TieredCompilation>true</TieredCompilation>
    <TieredCompilationQuickJit>false</TieredCompilationQuickJit>
    <TieredCompilationQuickJitForLoops>false</TieredCompilationQuickJitForLoops>
</PropertyGroup>
```

**For .NET 8+ (if upgrading)**:
```xml
<PropertyGroup>
    <OptimizationPreference>Speed</OptimizationPreference>
</PropertyGroup>
```

**Changes Required**:
- File: `LZ4Sharp/LZ4Sharp.csproj`
- Lines to add: 3-5 lines
- Risk: Minimal

**Expected Impact**:
- Overall speedup: 3-7%
- Better inlining decisions by JIT
- Improved branch prediction hints

**Timeline**: 1 day
- Configure, build, benchmark
- No code changes required

---

### 1.4 Optimize Hash Table Probing

**Current Implementation**: Single probe per position
```csharp
int hash = HashPosition(source, forwardPos);
int candidate = hashTable[hash];
hashTable[hash] = forwardPos;
```

**Proposed Optimization**: 2-way set-associative cache
```csharp
private struct HashEntry
{
    public int Pos1;
    public int Pos2;
}

private HashEntry[] hashTable = new HashEntry[HASH_SIZE];

// On insert
int hash = HashPosition(source, forwardPos);
var entry = hashTable[hash];
// Check both positions
int candidate = -1;
if (entry.Pos1 >= 0 && forwardPos - entry.Pos1 <= LZ4_DISTANCE_MAX)
    candidate = entry.Pos1;
else if (entry.Pos2 >= 0 && forwardPos - entry.Pos2 <= LZ4_DISTANCE_MAX)
    candidate = entry.Pos2;

// Update (LRU-style)
entry.Pos2 = entry.Pos1;
entry.Pos1 = forwardPos;
hashTable[hash] = entry;
```

**Changes Required**:
- File: `LZ4Codec.cs`
- New struct: HashEntry (4 lines)
- Modified method: CompressGeneric (~30 lines)
- Risk: Medium (increased memory usage)

**Expected Impact**:
- Compression speedup: 5-10%
- Better match finding, especially for varied data
- Memory cost: 2x hash table (still only ~32 KB)

**Timeline**: 3-4 days
- Day 1: Implement HashEntry structure
- Day 2: Update compression logic
- Day 3-4: Test, benchmark, tune

---

**Phase 1 Total Expected Improvement**: 19-32%

**Phase 1 Estimated Timeline**: 2-3 weeks

**Phase 1 Target Performance**: 30-37 µs (100KB compression)

---

## Phase 2: Managed Performance Optimizations (Q2 2026)

**Goal**: Extract maximum performance from safe managed code before considering unsafe

### 2.1 Span<T> Migration for Zero-Copy Operations

**Current API**:
```csharp
public static int CompressDefault(byte[] source, byte[] destination, int sourceSize, int maxDestinationSize)
```

**Proposed New API** (add alongside existing):
```csharp
public static int CompressDefault(ReadOnlySpan<byte> source, Span<byte> destination)
```

**Benefits**:
- Eliminate array allocations for slicing
- Better cache locality
- Enable stackalloc for small buffers
- Modern .NET best practice

**Expected Impact**:
- Compression: 5-10% speedup
- Decompression: 10-15% speedup (more slicing operations)
- API: Non-breaking (additive only)

**Timeline**: 3-4 weeks
- Week 1: Implement Span-based CompressDefault
- Week 2: Implement Span-based DecompressSafe
- Week 3: Test, benchmark, validate
- Week 4: Documentation and examples

---

### 2.2 ArrayPool<T> for Temporary Allocations

**Current Code**:
```csharp
int[] hashTable = new int[HASH_SIZE];
Array.Fill(hashTable, -1);
```

**Proposed**:
```csharp
int[] hashTable = ArrayPool<int>.Shared.Rent(HASH_SIZE);
try
{
    Array.Fill(hashTable, -1);
    // ... compression logic ...
}
finally
{
    ArrayPool<int>.Shared.Return(hashTable);
}
```

**Benefits**:
- Reduce GC pressure
- Faster for repeated compressions
- Standard .NET pattern

**Expected Impact**:
- Single compression: 1-2% improvement
- Repeated compressions: 5-10% improvement
- Reduced memory allocations

**Timeline**: 1-2 weeks
- Implement, test, validate GC behavior

---

### 2.3 Aggressive Inlining Expansion

**Current**: Already using `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot paths

**Proposed**: Add to more methods and measure impact
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static int EncodeLength(byte[] dest, int pos, int length)
{
    // Encourage JIT to inline this
}
```

**Expected Impact**: 2-4% overall

**Timeline**: 1 week

---

**Phase 2 Total Expected Improvement**: 18-34% (cumulative: 41-77% from baseline)

**Phase 2 Estimated Timeline**: 2-3 months

**Phase 2 Target Performance**: 22-30 µs (100KB compression)

---

## Phase 3: Unsafe Code Optimizations (Q3 2026)

**Goal**: Introduce unsafe code in controlled manner for critical paths only

**Decision Point**: Community feedback required before proceeding

### 3.1 Unsafe Pointer-Based Array Access

**Approach**: Create separate `LZ4Codec.Unsafe.cs` file for unsafe implementations

**Example**:
```csharp
// LZ4Codec.Unsafe.cs
public static partial class LZ4Codec
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool AreEqualUnsafe(byte[] source, int pos1, int pos2, int length)
    {
        fixed (byte* ptr = source)
        {
            byte* p1 = ptr + pos1;
            byte* p2 = ptr + pos2;
            
            // 8-byte comparison
            if (length == 8)
                return *(ulong*)p1 == *(ulong*)p2;
            
            // 4-byte comparison
            if (length == 4)
                return *(uint*)p1 == *(uint*)p2;
            
            // Fallback
            for (int i = 0; i < length; i++)
                if (p1[i] != p2[i])
                    return false;
            
            return true;
        }
    }
}
```

**Expected Impact**: 15-25% overall speedup

**Timeline**: 4-6 weeks

**Risk**: High (loses memory safety)

---

### 3.2 Unsafe Memory Copying

**Replace**: `Buffer.BlockCopy` with unsafe `Buffer.MemoryCopy`

**Expected Impact**: 5-10% decompression speedup

**Timeline**: 2 weeks

---

**Phase 3 Total Expected Improvement**: 22-35% (cumulative: 73-146% from baseline)

**Phase 3 Estimated Timeline**: 2-3 months

**Phase 3 Target Performance**: 18-25 µs (100KB compression)

---

## Phase 4: SIMD Optimizations (Q4 2026)

**Goal**: Use hardware acceleration for bulk operations

### 4.1 AVX2 Match Finding

**Requirements**:
- .NET 6+ with hardware intrinsics
- Runtime CPU detection
- Fallback for non-AVX2 CPUs

**Example**:
```csharp
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

if (Avx2.IsSupported && length >= 32)
{
    var vec1 = Avx2.LoadVector256(source, pos1);
    var vec2 = Avx2.LoadVector256(source, pos2);
    var equals = Avx2.CompareEqual(vec1, vec2);
    int mask = Avx2.MoveMask(equals);
    if (mask == -1)  // All bytes equal
    {
        pos1 += 32;
        pos2 += 32;
        count += 32;
    }
}
```

**Expected Impact**: 30-40% compression speedup

**Timeline**: 6-8 weeks

**Risk**: Very High (platform-specific)

---

**Phase 4 Total Expected Improvement**: 40-60% (cumulative: 144-308% from baseline)

**Phase 4 Target Performance**: 12-18 µs (100KB compression)

---

## Implementation Priority Matrix

| Optimization | Impact | Complexity | Safety | Priority | Timeline |
|--------------|--------|------------|--------|----------|----------|
| UInt64 Comparisons | High | Low | Safe | **P0** | 2-3 days |
| PGO Enable | Medium | Very Low | Safe | **P0** | 1 day |
| Hash Table 2-Way | Medium | Medium | Safe | **P1** | 3-4 days |
| Span<T> API | Medium | High | Safe | **P1** | 3-4 weeks |
| ArrayPool | Low-Med | Medium | Safe | **P2** | 1-2 weeks |
| Unsafe Pointers | High | Medium | **Unsafe** | **P3** | 4-6 weeks |
| SIMD AVX2 | Very High | Very High | Safe* | **P4** | 6-8 weeks |

*Safe in terms of memory, but platform-specific

---

## Success Metrics

### Performance Targets (100KB Compression)

| Phase | Target Time (µs) | Target Throughput (MB/s) | vs Baseline | vs Theoretical |
|-------|------------------|-------------------------|-------------|----------------|
| Baseline | 43.9 | 2,333 | 1.0x | 3.7-5.5x slower |
| Phase 1 | 30-37 | 2,760-3,410 | 1.2-1.5x | 2.5-4.6x slower |
| Phase 2 | 22-30 | 3,410-4,650 | 1.5-2.0x | 1.8-3.8x slower |
| Phase 3 | 18-25 | 4,090-5,680 | 1.8-2.4x | 1.5-3.1x slower |
| Phase 4 | 12-18 | 5,680-8,530 | 2.4-3.7x | 1.0-2.3x slower |

### Quality Metrics

- [ ] All unit tests pass (100% pass rate)
- [ ] No security vulnerabilities (CodeQL clean)
- [ ] No memory leaks (validated with profiler)
- [ ] Compressed output bit-identical
- [ ] Cross-platform compatibility maintained
- [ ] Documentation updated for each phase

---

## Risk Management

### Technical Risks

1. **Breaking Changes**
   - Mitigation: Keep old API, add new API alongside
   - Version semantic: Use minor version bumps

2. **Platform-Specific Code**
   - Mitigation: Runtime detection + fallback paths
   - Testing: Validate on Windows, Linux, macOS

3. **Unsafe Code Security**
   - Mitigation: Isolate in separate file, thorough review
   - Testing: Fuzzing, edge case validation

4. **SIMD Complexity**
   - Mitigation: Start with simple cases, iterate
   - Testing: Compare output byte-for-byte

### Process Risks

1. **Scope Creep**
   - Mitigation: Strict phase boundaries, measure before moving on

2. **Compatibility Issues**
   - Mitigation: Extensive testing, beta releases

3. **Community Resistance to Unsafe**
   - Mitigation: Make it opt-in, keep safe version default

---

## Next Actions (This Week)

1. **Implement UInt64 Comparisons** (Priority P0)
   - Modify `AreEqual()` method
   - Modify `CountMatch()` method
   - Run all tests
   - Benchmark and validate 8-12% gain

2. **Enable PGO** (Priority P0)
   - Update project file
   - Rebuild and benchmark
   - Validate 3-7% gain

3. **Run CPU Cycle Benchmarks** (Priority P0)
   - Execute CpuCycleBenchmarks
   - Capture hardware counters
   - Document actual vs theoretical cycles
   - Validate CPU_CYCLE_ANALYSIS.md estimates

4. **Community Feedback** (Priority P1)
   - Share analysis and plan
   - Gather feedback on unsafe code direction
   - Decide Phase 3+ roadmap

---

## Long-Term Vision

**Goal**: Become the reference C# implementation of LZ4

**Strategy**: Multi-tier approach
- `LZ4Sharp.Safe`: Current approach, educational, safe
- `LZ4Sharp.Fast`: Unsafe + some SIMD, balanced
- `LZ4Sharp.Max`: All optimizations, maximum speed

**Timeline**: 12-18 months to complete all phases

**Success**: Within 1.5-2x of theoretical minimum, competitive with K4os.LZ4

---

**Document Version**: 1.0  
**Last Updated**: January 4, 2026  
**Author**: GitHub Copilot Agent  
**Status**: Ready for Implementation
