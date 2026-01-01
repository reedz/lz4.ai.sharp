# LZ4Sharp - LZ4 Compression Algorithm in C#

This is a C# translation of the LZ4 compression algorithm from the original C implementation.

## Overview

LZ4 is lossless compression algorithm, providing compression speed > 500 MB/s per core,
scalable with multi-cores CPU. It features an extremely fast decoder, with speed in
multiple GB/s per core, typically reaching RAM speed limits on multi-core systems.

## Project Structure

- **LZ4Sharp**: Class library containing the LZ4 compression/decompression implementation
  - `LZ4Codec.cs`: Core compression and decompression algorithms
  
- **LZ4Sharp.Examples**: Console application with usage examples
  - `SimpleBuffer.cs`: Simple compression/decompression example (translated from C's simple_buffer.c)

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

## Usage

### Compression

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

## Implementation Notes

This C# implementation translates the core LZ4 compression algorithm from C to C#. Key differences:

1. **Memory Management**: Uses managed byte arrays instead of pointers
2. **Type Safety**: Uses C# types and null safety
3. **Performance**: Uses `MethodImpl(MethodImplOptions.AggressiveInlining)` for performance-critical methods
4. **Simplicity**: Simplified implementation focusing on core functionality

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

- Streaming compression/decompression
- High compression mode (LZ4_HC)
- Frame format support
- Performance optimizations using Span<T> and Memory<T>
- Multi-threading support
- Additional examples
