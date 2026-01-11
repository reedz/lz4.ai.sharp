# LZ4.AI.Sharp

A **C#/.NET** implementation of the **LZ4** compression algorithm (block + HC), generated end-to-end by AI as a learning/experimentation repo.

## Provenance / AI-generated notice

This repository is **completely AI-generated**, created as a C# port inspired by (and functionally equivalent to) the upstream **LZ4 reference implementation**.

- Upstream reference repository (keep for attribution + algorithm details): https://github.com/lz4/lz4
- This project is **not affiliated** with the upstream maintainers.

## What’s in here

- `src/LZ4Sharp` – core library (`LZ4Codec`, `LZ4HC`, `LZ4Frame`, `XXHash`)
- `src/LZ4Sharp.Tests` – unit tests
- `src/LZ4Sharp.Examples` – small usage examples
- `src/LZ4Sharp.Benchmarks` – BenchmarkDotNet benchmarks

## Install (NuGet)

(Once published)

```bash
dotnet add package LZ4.AI.Sharp
```

## Quick usage

```csharp
using LZ4Sharp;

byte[] input = ...;
byte[] compressed = new byte[LZ4Codec.CompressBound(input.Length)];
int compressedSize = LZ4Codec.CompressDefault(input, compressed, input.Length, compressed.Length);

byte[] restored = new byte[input.Length];
int restoredSize = LZ4Codec.DecompressSafe(compressed, restored, compressedSize, restored.Length);
```

## Build / test

```bash
dotnet build src/LZ4Sharp.sln -c Release
dotnet test  src/LZ4Sharp.sln -c Release
```

## Pack (NuGet)

```bash
dotnet pack src/LZ4Sharp/LZ4Sharp.csproj -c Release
```

## License

BSD-2-Clause (same license as upstream LZ4).
