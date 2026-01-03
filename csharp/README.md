# LZ4Sharp - LZ4 Compression Algorithm in C#

This is a C# translation of the LZ4 compression algorithm from the original C implementation.

## Overview

LZ4 is lossless compression algorithm, providing compression speed > 500 MB/s per core,
scalable with multi-cores CPU. It features an extremely fast decoder, with speed in
multiple GB/s per core, typically reaching RAM speed limits on multi-core systems.

## Project Structure

- **LZ4Sharp**: Class library containing the LZ4 compression/decompression implementation (.NET 10)
  - `LZ4Codec.cs`: Core compression and decompression algorithms
  - `LZ4HC.cs`: High compression mode for better ratios **NEW**
  - `LZ4Frame.cs`: Frame format support compatible with lz4 CLI **NEW**
  - `XXHash.cs`: XXHash (XXH32) fast hash algorithm
  
- **LZ4Sharp.Examples**: Console application with usage examples (.NET 10)
  - `SimpleBuffer.cs`: Simple compression/decompression example (translated from C's simple_buffer.c)
  - `CompressionDemo.cs`: Comprehensive compression tests
  - `XXHashExample.cs`: XXHash demonstration

- **LZ4Sharp.Tests**: Unit tests using xUnit (.NET 10)
  - `LZ4CodecTests.cs`: Comprehensive test suite (18 tests)
  - `LZ4HCTests.cs`: HC mode test suite (8 tests) **NEW**
  - `LZ4FrameTests.cs`: Frame format test suite (13 tests) **NEW**
  - `XXHashTests.cs`: XXHash test suite (19 tests)

- **LZ4Sharp.Benchmarks**: Performance benchmarks using BenchmarkDotNet (.NET 10)
  - Compares LZ4Sharp against K4os.Compression.LZ4
  - See [Benchmarks README](LZ4Sharp.Benchmarks/README.md) for detailed results

## Building

```bash
cd csharp
dotnet build
```

## Running Examples

### Simple Buffer Example

Basic compression and decompression example:

```bash
cd csharp
dotnet run --project LZ4Sharp.Examples/LZ4Sharp.Examples.csproj
```

### Compression Demo (Multiple Tests & Benchmarks)

Run comprehensive compression tests with different data patterns and performance benchmarks:

```bash
cd csharp
dotnet run --project LZ4Sharp.Examples/LZ4Sharp.Examples.csproj demo
```

### XXHash Example

Demonstrate XXHash fast hashing algorithm:

```bash
cd csharp
dotnet run --project LZ4Sharp.Examples/LZ4Sharp.Examples.csproj xxhash
```

## Running Benchmarks

Compare LZ4Sharp performance against K4os.Compression.LZ4:

```bash
cd csharp/LZ4Sharp.Benchmarks
dotnet run -c Release -- --filter "*QuickBenchmarks*"
```

See [Benchmarks README](LZ4Sharp.Benchmarks/README.md) for more information.

## Benchmark Results (.NET 10)

Performance comparison with K4os.Compression.LZ4 on AMD EPYC 7763:

| Operation       | Data Size | LZ4Sharp  | K4os.LZ4 | Ratio |
|-----------------|-----------|-----------|----------|-------|
| **Compression**     | 10 KB     | 11.7 μs   | 2.6 μs   | 4.5x  |
| **Compression**     | 100 KB    | 147.4 μs  | 24.1 μs  | 6.1x  |
| **Decompression**   | 10 KB     | 7.1 μs    | 1.5 μs   | 2.8x  |
| **Decompression**   | 100 KB    | 125.2 μs  | 63.2 μs  | 5.2x  |

**Throughput (100KB data):**
- LZ4Sharp: ~694 MB/s compression, ~818 MB/s decompression
- K4os.LZ4: ~4,251 MB/s compression, ~1,620 MB/s decompression

LZ4Sharp prioritizes code clarity and educational value, while K4os.LZ4 is optimized for production use.

## Usage

### Standard Block Compression

```csharp
using LZ4Sharp;

byte[] source = ...; // Your data to compress
int sourceSize = source.Length;

// Get maximum compressed size
int maxCompressedSize = LZ4Codec.CompressBound(sourceSize);
byte[] compressed = new byte[maxCompressedSize];

// Compress the data
int compressedSize = LZ4Codec.CompressDefault(source, compressed, sourceSize, maxCompressedSize);

if (compressedSize <= 0)
{
    // Compression failed
}
```

### High Compression Mode (Better Ratios)

```csharp
using LZ4Sharp;

byte[] source = ...; // Your data to compress
int sourceSize = source.Length;

int maxCompressedSize = LZ4HC.CompressBound(sourceSize);
byte[] compressed = new byte[maxCompressedSize];

// Compress with HC mode (level 9 is default, 3-12 supported)
int compressedSize = LZ4HC.CompressHC(source, compressed, sourceSize, maxCompressedSize, 9);
```

### Frame Format (Compatible with lz4 CLI)

```csharp
using LZ4Sharp;

byte[] source = ...; // Your data to compress
int sourceSize = source.Length;

// Configure frame preferences
var prefs = new LZ4Frame.FramePreferences
{
    BlockSizeId = LZ4Frame.BlockSize.Max64KB,
    ContentChecksumFlag = LZ4Frame.ContentChecksum.ChecksumEnabled,
    CompressionLevel = LZ4HC.CLEVEL_DEFAULT  // Use HC if desired
};

byte[] compressed = new byte[LZ4Frame.CompressFrameBound(sourceSize, prefs)];

// Compress into frame format
int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, source, sourceSize, prefs);

// Decompress from frame format
byte[] decompressed = new byte[sourceSize];
int decompressedSize = LZ4Frame.DecompressFrame(decompressed, decompressed.Length, compressed, compressedSize);
```

### Decompression

```csharp
using LZ4Sharp;

byte[] compressed = ...; // Your compressed data
int compressedSize = compressed.Length;
int decompressedSize = ...; // Known or maximum decompressed size

byte[] decompressed = new byte[decompressedSize];

// Decompress the data
int actualSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressedSize);

if (actualSize < 0)
{
    // Decompression failed
}
```

## API Reference

### LZ4Codec.CompressBound(int inputSize)

Returns the maximum compressed size for a given input size.

- **Parameters:**
  - `inputSize`: Size of input data in bytes
- **Returns:** Maximum possible compressed size

### LZ4Codec.CompressDefault(byte[] source, byte[] destination, int sourceSize, int maxDestinationSize)

Compress data using LZ4 algorithm with default acceleration.

- **Parameters:**
  - `source`: Source data to compress
  - `destination`: Destination buffer for compressed data
  - `sourceSize`: Size of source data
  - `maxDestinationSize`: Maximum size of destination buffer
- **Returns:** Size of compressed data, or negative value on error

### LZ4Codec.DecompressSafe(byte[] source, byte[] destination, int compressedSize, int maxDecompressedSize)

Decompress LZ4 compressed data safely.

- **Parameters:**
  - `source`: Compressed source data
  - `destination`: Destination buffer for decompressed data
  - `compressedSize`: Size of compressed data
  - `maxDecompressedSize`: Maximum size of decompressed data
- **Returns:** Size of decompressed data, or negative value on error

### LZ4HC.CompressHC(byte[] source, byte[] destination, int sourceSize, int maxDestinationSize, int compressionLevel = 9) **NEW**

Compress data using LZ4 High Compression mode for better ratios.

- **Parameters:**
  - `source`: Source data to compress
  - `destination`: Destination buffer for compressed data
  - `sourceSize`: Size of source data
  - `maxDestinationSize`: Maximum size of destination buffer
  - `compressionLevel`: Compression level (3-12, default 9)
- **Returns:** Size of compressed data, or negative value on error

### LZ4Frame.CompressFrame(byte[] destination, int maxDestinationSize, byte[] source, int sourceSize, FramePreferences? prefs = null) **NEW**

Compress data into LZ4 frame format (compatible with lz4 CLI tool).

- **Parameters:**
  - `destination`: Destination buffer for compressed frame
  - `maxDestinationSize`: Maximum size of destination buffer
  - `source`: Source data to compress
  - `sourceSize`: Size of source data
  - `prefs`: Optional frame preferences (block size, checksums, etc.)
- **Returns:** Size of compressed frame, or negative value on error

### LZ4Frame.DecompressFrame(byte[] destination, int maxDestinationSize, byte[] source, int sourceSize) **NEW**

Decompress data from LZ4 frame format.

- **Parameters:**
  - `destination`: Destination buffer for decompressed data
  - `maxDestinationSize`: Maximum size of destination buffer
  - `source`: Compressed frame data
  - `sourceSize`: Size of source data
- **Returns:** Size of decompressed data, or negative value on error

### XXHash.XXH32(byte[] input, uint seed)

Calculate the 32-bit XXHash of input data.

- **Parameters:**
  - `input`: Input data to hash
  - `seed`: Seed value (0 for default)
- **Returns:** 32-bit hash value

### XXHash.XXH32State

Streaming XXHash state for large data.

**Methods:**
- `XXH32State(uint seed)` - Create new state with seed
- `Reset(uint seed)` - Reset state with new seed
- `Update(byte[] input, int length)` - Add data to hash
- `Digest()` - Get final hash value

**Example:**
```csharp
var state = new XXHash.XXH32State(0);
state.Update(chunk1, chunk1.Length);
state.Update(chunk2, chunk2.Length);
uint hash = state.Digest();
```

## Implementation Notes

This C# implementation translates all core LZ4 files from C to C#:

1. **Memory Management**: Uses managed byte arrays instead of pointers
2. **Type Safety**: Uses C# types and null safety
3. **Performance**: Uses `MethodImpl(MethodImplOptions.AggressiveInlining)` for performance-critical methods
4. **Completeness**: All major LZ4 features implemented:
   - Standard block compression (LZ4Codec.cs)
   - High compression mode (LZ4HC.cs)
   - Frame format support (LZ4Frame.cs)
   - XXHash checksums (XXHash.cs)

**Files Migrated:**
- lz4.c (~3000 lines) → LZ4Codec.cs (377 lines)
- lz4hc.c (2,255 lines) → LZ4HC.cs (360 lines)
- lz4frame.c (2,165 lines) → LZ4Frame.cs (363 lines)
- xxhash.c (1,030 lines) → XXHash.cs (319 lines)

**Total: 8,450 lines of C code migrated to 1,419 lines of C# code**

## License

BSD 2-Clause License - Same as the original LZ4 C implementation.

Copyright (c) Yann Collet (original C implementation)
Copyright (c) 2026 (C# translation)

## Original C Implementation

The original LZ4 C implementation can be found at: https://github.com/lz4/lz4

## Testing

Run the example to verify the implementation:

```bash
cd csharp/LZ4Sharp.Examples
dotnet run
```

Expected output:
```
Original string length: 86 bytes
Original string: Lorem ipsum dolor sit amet, consectetur adipiscing elit. Lorem ipsum dolor site amat.
Maximum compressed size: 102 bytes
We successfully compressed some data! Ratio: 0.84
Compressed size: 72 bytes
We successfully decompressed some data!
Validation done. The string we ended up with is:
Lorem ipsum dolor sit amet, consectetur adipiscing elit. Lorem ipsum dolor site amat.

Success! LZ4 compression and decompression completed successfully.
```

### Compression Demo Output

The demo shows various compression scenarios:

```
=== LZ4Sharp Compression Demonstration ===

Test 1: Simple String Compression
Test 2: Repeated Pattern (High Compression - ~98% space savings)
Test 3: Random Data (Low Compression)
Test 4: Large Data (100KB with patterns - ~99% space savings)
Test 5: Performance Benchmark (~300+ MB/s)
```

## Future Enhancements

Potential improvements for future versions:

- **Performance Optimizations**: Span<T>, Memory<T>, SIMD, unsafe code
- **Streaming API**: Continuous compression/decompression support
- **Dictionary Support**: External dictionary compression
- **Advanced Features**: Multi-threading, async/await patterns
- **CLI Tool**: lz4sharp command-line tool
