# LZ4Sharp Optimization Log

Track of all performance changes attempted across optimization sessions.
**Legend**: ✅ Kept (improved perf) | ❌ Reverted (regressed) | ⏭️ Skipped (not worth it)

---

## Phase 1: Initial Optimizations

| # | Area | Change | Result |
|---|------|--------|--------|
| 1 | XXHash | Span-based overloads + SIMD (Vector256/512) in XXH32 | ✅ Kept |
| 2 | LZ4HC | Span-based CompressHC overload avoiding array copies | ✅ Kept |
| 3 | LZ4Frame | BinaryPrimitives instead of manual bit shifts | ✅ Kept |
| 4 | LZ4Frame | Eliminate temp buffer copy in frame compression | ✅ Kept |
| 5 | LZ4HC | AVX-512 CountCommonBytes (leading zero count) | ✅ Kept |
| 6 | Streams | Async alloc fix (avoid unnecessary allocations) | ✅ Kept |
| 7 | LZ4Codec | `[module: SkipLocalsInit]` on assembly | ✅ Kept |
| 8 | LZ4Frame | stackalloc for frame header instead of heap alloc | ✅ Kept |
| 9 | Encoder | Span-based checksum computation in encoder stream | ✅ Kept |
| 10 | LZ4HC | `[AggressiveOptimization]` on CompressHCUnsafe | ✅ Kept |
| 11 | LZ4Codec | Decompress tail optimization | ✅ Kept |
| 12 | LZ4Codec | SIMD WildCopy8 (Vector256/512 tiers) for compression | ✅ Kept |

## Phase 2: Streaming & Frame Optimizations

| # | Area | Change | Result |
|---|------|--------|--------|
| 1 | LZ4Codec | SIMD match copy in compression | ✅ Neutral (kept) |
| 2 | DecoderStream | Direct-to-user-buffer decompression (ReadNextBlockDirect) | ✅ Significant win |
| 3 | EncoderStream | Write coalescing (header+data in single write) | ✅ Kept |
| 4 | DecoderStream | Async header buffer pre-allocation | ✅ Kept |
| 5 | Streams | BinaryPrimitives in stream read/write paths | ✅ Kept |
| 6 | LZ4Frame | Span-based CompressFrame/DecompressFrame overloads | ✅ Kept |
| 7 | LZ4Codec | Epoch-hash-large (double hash table to 128KB) | ⏭️ Skipped — increases cache pressure |
| 8 | Streams | Compress-block-delegate (avoid per-block branching) | ⏭️ Skipped — negligible overhead |

## Phase 3: Decompression Loop Optimization

| # | Area | Change | Result |
|---|------|--------|--------|
| 1 | LZ4Codec | WildCopy8 helper for decompression | ⏭️ Already existed from Phase 1 |
| 2 | LZ4Codec | Fix byte-at-a-time literal tail copy → overlapping Poke8 | ✅ ~5% decompress improvement |
| 3 | LZ4Codec | Remove Vector256 from match copy, use Copy8+Copy8+do/while | ✅ ~8% decompress improvement |
| 4 | LZ4Codec | Simplify literal copy tiers (replace 5-tier with simple Copy8 loop) | ❌ Regressed 10-15% — multi-tier approach is optimal |
| 5 | LZ4Codec | Extract match copy to inline helpers (reduce duplication) | ⏭️ Skipped — risks JIT optimization in hot loop |
| 6 | LZ4Codec | Unconditional 16-byte match copy | ⏭️ Skipped — Copy8+Copy8 already achieves this |

---

## Key Findings

- **SIMD in decompression match copy hurts performance** — Vector256 adds branch overhead and code bloat; typical matches are 4-20 bytes. K4os uses purely scalar 8-byte copies and is faster.
- **Multi-tier literal copy is optimal** — The 5-tier (AVX-512/16B-loop/8B/Poke8/small) approach beats simple Copy8 loops because branches are predictable (most literals are short).
- **WildCopy8 (SIMD version) is NOT safe for decompression match copies** — it may overwrite past the output buffer. Must use inline do/while Copy8 loop with `cpy > oend - 8` safety guards.
- **K4os decompression insight**: K4os v1.3.8 uses `LL64.LZ4_decompress_generic` with purely scalar 8-byte WildCopy8. No SIMD anywhere in decompression. Compact `Copy8→Copy8→if(len>16) WildCopy8` pattern.

## Current Performance vs K4os (as of Phase 4)

- **Fast compress**: 0.29–0.80× (20–71% faster) ✅
- **HC compress**: 0.25–0.81× (19–75% faster) ✅
- **Codec decompress**: 1.00–1.22× (improved from 1.03–1.38×) ⚠️ still slower but narrowing
- **Stream compress**: 0.14–0.76× (up to 7× faster at HC12) ✅
- **Stream decompress**: 0.59–0.93× ✅
- **Stream roundtrip**: 0.19–0.93× ✅
- **Frame compress**: 0 allocations (direct-to-destination) ✅

## Phase 4: Further Optimizations

| # | Area | Change | Result |
|---|------|--------|--------|
| 1 | LZ4Frame | Compress directly into destination buffer, eliminate intermediate ArrayPool rent+copy | ✅ Kept — 0 alloc, removes memcpy per block |
| 2 | EncoderStream | FlushBlockDirect — bypass _inputBuffer for full-block writes | ✅ Kept — ~5-10% stream compress improvement for large data |
| 3 | LZ4Codec | Merge small literal copy tiers in decompression (remove `>= 8` branch) | ✅ Kept — one fewer branch, neutral perf |
| 4 | LZ4Codec | Hybrid near-end match copy: 8-byte copies up to oend-8, then byte-exact | ✅ Kept — faster for long matches near buffer end |
| 5 | LZ4Codec | Hash pipelining in compress | ⏭️ Already optimal — matches reference LZ4 pattern |
| 6 | LZ4Codec | Last-literals overflow unroll / WildCopy8 | ⏭️ Skipped — runs once per compress, WildCopy8 unsafe at end-of-buffer |
| 7 | LZ4Codec | Decompress fast-path literal copy optimization | ⏭️ Already optimal — unconditional 16B copy in fast path |
| 8 | LZ4HC | Batch InsertAndUpdate hash writes | ⏭️ Skipped — memory-latency bound, batching won't help |
| 9 | LZ4Codec | Mid-loop literal WildCopy8 | ⏭️ Already uses WildCopy8 (line 271) |
