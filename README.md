# LZ4.AI.Sharp

> ⚠️ **AI-Generated Code** — This entire library was migrated from C to C# and performance-tuned using GitHub Copilot.

[![NuGet](https://img.shields.io/nuget/v/LZ4.AI.Sharp.svg)](https://www.nuget.org/packages/LZ4.AI.Sharp/)
[![License](https://img.shields.io/github/license/reedz/lz4.ai.sharp.svg)](LICENSE)

A high-performance **C#/.NET** implementation of the **LZ4** compression algorithm, featuring both fast and high-compression modes. This library was created as an AI-assisted port of the LZ4 reference implementation to demonstrate modern AI-powered code migration and optimization.

## Features

- **Fast Compression** (`LZ4Codec`) — Optimized for speed with competitive compression ratios
- **High Compression** (`LZ4HC`) — Better compression ratios at slower speeds
- **Frame Format** (`LZ4Frame`) — Stream-oriented API with checksums and metadata
- **XXHash** — Fast non-cryptographic hash function
- **Pure Managed C#** — No unsafe code dependencies, runs on any .NET platform
- **Zero Allocations** — Work directly with byte arrays and spans

## Installation

```bash
dotnet add package LZ4.AI.Sharp
```

## Quick Start

### Fast Compression

```csharp
using LZ4Sharp;

byte[] input = GetYourData();
byte[] compressed = new byte[LZ4Codec.CompressBound(input.Length)];
int compressedSize = LZ4Codec.CompressDefault(input, compressed, input.Length, compressed.Length);

byte[] decompressed = new byte[input.Length];
LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, decompressed.Length);
```

### High Compression

```csharp
using LZ4Sharp;

byte[] compressed = new byte[LZ4HC.CompressBound(input.Length)];
int compressedSize = LZ4HC.CompressHC(input, compressed, input.Length, compressed.Length);
```

## Performance

See [BENCHMARKS.md](BENCHMARKS.md) for detailed performance results on JSON and Silesia corpus datasets.

**Highlights** (Intel N100, .NET 10):
- JSON dataset: ratios **0.29–0.77** vs K4os (**1.3x–3.4x faster**)
- Silesia corpus (accel=1): ratios **0.09–1.00** vs K4os (**up to 11.1x faster**)
- Silesia: faster in **11/12** files at accel=1
- Compression ratios: **Match the reference implementation**

## Testing

The test suite includes:
- **Unit Tests** — Comprehensive algorithm correctness tests
- **Compatibility Tests** — Cross-validation with K4os.Compression.LZ4 to ensure interoperability
- **Round-trip Tests** — Compression/decompression verification

All tests verify that LZ4Sharp produces output compatible with other LZ4 implementations.

## Development

```bash
# Build
dotnet build src/LZ4Sharp.sln -c Release

# Test
dotnet test src/LZ4Sharp.sln -c Release

# Run benchmarks
dotnet run --project src/LZ4Sharp.Benchmarks -c Release

# Package
dotnet pack src/LZ4Sharp/LZ4Sharp.csproj -c Release
```

## AI Provenance

This library is **100% AI-generated code**:
1. **Migration** — Ported from C reference implementation using GitHub Copilot
2. **Optimization** — Performance-tuned through iterative AI-assisted refinement
3. **Testing** — Test cases and benchmarks created with AI assistance

The project demonstrates AI capabilities in code translation, optimization, and maintaining algorithmic correctness while adapting to different language paradigms.

## Attribution

- Based on the [LZ4 reference implementation](https://github.com/lz4/lz4) by Yann Collet
- Not affiliated with the upstream LZ4 project
- See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for license details

## License

MIT License — See [LICENSE](LICENSE) for details.
