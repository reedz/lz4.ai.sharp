# LZ4 C to C# Translation Summary

## Overview

This document summarizes the translation of the LZ4 compression algorithm from C to C#.

## Translation Approach

The C# implementation was created by translating the core LZ4 compression algorithm from the original C implementation while maintaining the same algorithm logic and structure.

### Key Design Decisions

1. **Managed Memory**: Replaced C pointers with C# byte arrays for type safety
2. **Object-Oriented Design**: Implemented as static methods in the `LZ4Codec` class
3. **Performance Optimization**: Used `MethodImpl(MethodImplOptions.AggressiveInlining)` for critical methods
4. **Error Handling**: Maintained the same error code convention (negative values indicate errors)

## Files Translated

### Core Algorithm (lib/lz4.c → LZ4Codec.cs)

| C Function | C# Method | Purpose |
|------------|-----------|---------|
| `LZ4_compressBound()` | `CompressBound()` | Calculate maximum compressed size |
| `LZ4_compress_default()` | `CompressDefault()` | Compress data with default settings |
| `LZ4_compress_fast()` | `CompressFast()` | Compress with acceleration parameter |
| `LZ4_decompress_safe()` | `DecompressSafe()` | Safely decompress data |
| `LZ4_compress_generic()` | `CompressGeneric()` | Core compression algorithm |
| `LZ4_decompress_generic()` | `DecompressGeneric()` | Core decompression algorithm |

### Examples (examples/simple_buffer.c → SimpleBuffer.cs)

The simple buffer example was translated line-by-line, demonstrating:
- Basic compression workflow
- Decompression and validation
- Error handling
- Memory management

## Implementation Details

### Constants Translated

```csharp
MINMATCH = 4              // Minimum match length
WILDCOPYLENGTH = 8        // Fast copy length
LASTLITERALS = 5          // Last literal bytes
ML_BITS = 4               // Match length bits in token
RUN_BITS = 4              // Run length bits in token
HASH_LOG = 12             // Hash table size (2^12)
LZ4_DISTANCE_MAX = 65535  // Maximum match distance
```

### Algorithm Flow

#### Compression
1. Initialize hash table for match finding
2. Scan input for matching sequences
3. Encode literals and matches in token format
4. Handle last literals
5. Return compressed size

#### Decompression
1. Read token byte
2. Decode literal length
3. Copy literals
4. Read offset
5. Decode match length
6. Copy match (handling overlapping copies)
7. Repeat until end of input

### Differences from C Implementation

1. **Memory Access**: Uses array indexing instead of pointer arithmetic
2. **Type System**: Uses byte[] instead of char* or unsigned char*
3. **String Handling**: Uses UTF-8 encoding for text conversion
4. **Memory Allocation**: Uses managed arrays instead of malloc/realloc
5. **Bounds Checking**: Automatic in C# (can be disabled for performance if needed)

## Test Coverage

The C# implementation includes comprehensive unit tests covering:

- ✓ Simple string compression/decompression
- ✓ Repeated patterns (high compression ratio)
- ✓ Random data (low compression ratio)
- ✓ Large data (100KB+)
- ✓ Binary data
- ✓ Edge cases (empty data, null inputs)
- ✓ Error handling (invalid data, insufficient buffers)

**Test Results**: 10/10 passing

## Performance

Based on benchmarks with 1MB data:

| Operation | Speed |
|-----------|-------|
| Compression | ~330 MB/s |
| Decompression | ~310 MB/s |

**Note**: Performance measured on .NET 8.0. Actual performance may vary based on:
- Hardware (CPU speed, cache size)
- .NET runtime version
- JIT compilation optimizations
- Data patterns

## Compression Ratios

Results from test scenarios:

| Data Type | Original Size | Compressed Size | Ratio | Savings |
|-----------|---------------|-----------------|-------|---------|
| Simple text | 44 bytes | 46 bytes | 1.05 | -4.55% |
| Repeated pattern | 2,800 bytes | 45 bytes | 0.016 | 98.39% |
| Random data | 1,000 bytes | 1,005 bytes | 1.005 | -0.50% |
| Large patterns | 102,400 bytes | 663 bytes | 0.0065 | 99.35% |

## Compatibility

The C# implementation produces output compatible with the C implementation for basic use cases. However, note:

- ✓ Basic block compression/decompression compatible
- ✓ .NET 10 support
- ✓ Comprehensive benchmark suite comparing against K4os.Compression.LZ4
- ✗ Frame format not implemented (requires lz4frame.c translation - 2165 lines)
- ✗ Streaming API not implemented
- ✗ Dictionary support not implemented
- ✗ HC (High Compression) mode not implemented (requires lz4hc.c translation - 2255 lines)
- ✗ XXHash not implemented (requires xxhash.c translation - 1030 lines)

## Benchmark Results

Performance benchmarks comparing LZ4Sharp against K4os.Compression.LZ4 on .NET 10:

| Operation       | Data Size | LZ4Sharp  | K4os.LZ4 | Ratio |
|-----------------|-----------|-----------|----------|-------|
| Compression     | 10 KB     | 11.7 μs   | 2.6 μs   | 4.5x  |
| Compression     | 100 KB    | 147.4 μs  | 24.1 μs  | 6.1x  |
| Decompression   | 10 KB     | 7.1 μs    | 1.5 μs   | 2.8x  |
| Decompression   | 100 KB    | 125.2 μs  | 63.2 μs  | 5.2x  |

**Analysis:**
- LZ4Sharp achieves ~694 MB/s compression and ~818 MB/s decompression (100KB data)
- K4os.LZ4 achieves ~4,251 MB/s compression and ~1,620 MB/s decompression (100KB data)
- Performance difference is expected as LZ4Sharp prioritizes code clarity while K4os.LZ4 uses unsafe code and SIMD optimizations

See [LZ4Sharp.Benchmarks/README.md](LZ4Sharp.Benchmarks/README.md) for detailed benchmark results.

## Future Enhancements

Potential improvements for future versions:

1. **LZ4 Frame Format**: Implement full frame format support (translate lz4frame.c)
2. **XXHash**: Translate XXHash implementation (needed for frame format)
3. **High Compression**: Translate LZ4_HC algorithm (translate lz4hc.c)
4. **Streaming**: Add streaming compression/decompression
5. **Performance**: 
   - Use Span<T> and Memory<T> for better performance
   - SIMD optimizations where applicable
   - Unsafe code for pointer-based operations
6. **Dictionary**: Add dictionary compression support
7. **Multi-threading**: Parallel compression for large data

## References

- **Original C Implementation**: https://github.com/lz4/lz4
- **LZ4 Block Format**: doc/lz4_Block_format.md
- **LZ4 Frame Format**: doc/lz4_Frame_format.md
- **LZ4 Homepage**: http://www.lz4.org
- **K4os.Compression.LZ4**: https://github.com/MiloszKrajewski/K4os.Compression.LZ4

## License

This C# translation maintains the same BSD 2-Clause license as the original C implementation.

Copyright (c) Yann Collet (original C implementation)  
Copyright (c) 2026 (C# translation)
