# Standard Compression Corpus Benchmarks

This document describes the standard compression corpus benchmarks implemented in LZ4Sharp.

## Overview

The `CorpusBenchmarks` class provides benchmarks using data patterns from widely-recognized compression testing corpora:

1. **Calgary Corpus** - A standard compression benchmark suite
2. **Canterbury Corpus** - A diverse collection of files for compression testing  
3. **JSON Bench** - JSON data benchmarks for modern application testing

These benchmarks compare **LZ4Sharp** (this implementation) against **K4os.Compression.LZ4** (the industry-standard C# LZ4 library).

## Standard Compression Corpora

### Calgary Corpus

The Calgary Corpus is one of the oldest and most widely used compression benchmark suites, originally compiled in 1987 at the University of Calgary. It consists of 18 files (~3.3 MB total) representing various data types.

**Files in our benchmark:**
- `bib` - Bibliography in BibTeX format (111,261 bytes)
- `book1` - Fictional book text (768,771 bytes)
- `paper1` - Technical paper (53,161 bytes)
- `progc` - C source code (39,611 bytes)
- `progl` - LISP source code (71,646 bytes)

### Canterbury Corpus

The Canterbury Corpus was created in 1997 as a modern alternative to Calgary, with 11 files designed to represent contemporary data types more accurately.

**Files in our benchmark:**
- `alice29.txt` - Alice's Adventures in Wonderland (152,089 bytes)
- `asyoulik.txt` - Shakespeare's "As You Like It" (125,179 bytes)
- `lcet10.txt` - Literary text collection (426,754 bytes)
- `plrabn12.txt` - Paradise Lost by John Milton (481,861 bytes)

### JSON Bench

Modern applications heavily use JSON for data interchange. These benchmarks test compression on typical JSON patterns:

**Files in our benchmark:**
- `json-simple` - Array of simple JSON objects
- `json-complex` - Nested JSON structures with arrays and objects
- `json-array` - Large JSON array with metadata

## Synthetic Data Generation

Since downloading full corpus files would bloat the repository, these benchmarks use **synthetic data generators** that produce data with similar characteristics to the original corpus files:

- **Structural patterns** matching the original files
- **Repetition patterns** common in each data type
- **Size ranges** representative of the corpus
- **Deterministic generation** (same output every run) for reproducibility

## Running the Benchmarks

### Quick Run (Dry mode - fast)
```bash
cd LZ4Sharp.Benchmarks
dotnet run -c Release -- --filter "*CorpusBenchmarks*" --job dry
```

### Full Benchmark Run
```bash
cd LZ4Sharp.Benchmarks
dotnet run -c Release -- --filter "*CorpusBenchmarks*"
```

### Run Specific Corpus Type
```bash
# Calgary Corpus only
dotnet run -c Release -- --filter "*CorpusBenchmarks*" --job dry | grep Calgary

# Canterbury Corpus only  
dotnet run -c Release -- --filter "*CorpusBenchmarks*" --job dry | grep Canterbury

# JSON Bench only
dotnet run -c Release -- --filter "*CorpusBenchmarks*" --job dry | grep JSON
```

### Run Compression vs Decompression
```bash
# Compression only
dotnet run -c Release -- --filter "*CorpusBenchmarks.Compress*" --job dry

# Decompression only
dotnet run -c Release -- --filter "*CorpusBenchmarks.Decompress*" --job dry
```

## Benchmark Metrics

Each benchmark measures:

- **Compression Speed**: Time to compress data (lower is better)
- **Decompression Speed**: Time to decompress data (lower is better)
- **Memory Allocation**: Bytes allocated during operation (lower is better)
- **Compression Ratio**: Size of compressed data vs original (implicit in results)

See [CORPUS_RESULTS.md](CORPUS_RESULTS.md) for detailed benchmark results and performance analysis.

## Interpreting Results

### Performance Comparison
- **Ratio < 1.0**: LZ4Sharp is faster than K4os.LZ4 (baseline)
- **Ratio = 1.0**: Equal performance
- **Ratio > 1.0**: K4os.LZ4 is faster (expected due to optimizations)

### Example Output
```
| Method                    | File           | Mean      | Ratio | Allocated |
|-------------------------- |--------------- |----------:|------:|----------:|
| LZ4Sharp - Compress       | Calgary/bib    | 234.5 us  | 3.20  | 150 KB    |
| K4os.LZ4 - Compress       | Calgary/bib    | 73.2 us   | 1.00  | 120 KB    |
| LZ4Sharp - Decompress     | Calgary/bib    | 156.2 us  | 2.15  | 111 KB    |
| K4os.LZ4 - Decompress     | Calgary/bib    | 72.6 us   | 1.00  | 111 KB    |
```

## Data Characteristics by Corpus

### Calgary Corpus Characteristics
- **bib**: Highly structured with repeated field names (good compression)
- **book1/book2**: Natural language text (moderate compression)
- **paper1**: Academic text with technical terms (moderate compression)
- **progc**: C source code with keywords and patterns (good compression)
- **progl**: LISP source code with parentheses (very good compression)

### Canterbury Corpus Characteristics
- **alice29.txt**: Literary fiction (moderate compression)
- **asyoulik.txt**: Shakespeare dialogue format (good compression)
- **lcet10.txt**: Mixed literature (moderate compression)
- **plrabn12.txt**: Epic poetry (moderate compression)

### JSON Bench Characteristics
- **json-simple**: Flat structures with repeated keys (excellent compression)
- **json-complex**: Nested objects with repeated patterns (very good compression)
- **json-array**: Large arrays with metadata (good compression)

## Why These Corpora Matter

1. **Industry Standard**: Calgary and Canterbury are universally recognized benchmarks
2. **Reproducibility**: Results can be compared across different implementations
3. **Diverse Data**: Cover text, code, structured data - real-world variety
4. **Compression Patterns**: Test different compression scenarios (repetition, entropy, structure)
5. **Historical Comparison**: Decades of results from other compression algorithms

## References

- [Calgary Corpus](http://www.data-compression.info/Corpora/CalgaryCorpus/)
- [Canterbury Corpus](http://corpus.canterbury.ac.nz/)
- [Compression Benchmark Resources](http://mattmahoney.net/dc/text.html)

## Using Actual Corpus Files

If you want to test with actual corpus files instead of synthetic data:

1. Download the corpus files from official sources
2. Place them in a `testdata/` directory
3. Modify `CorpusBenchmarks.cs` to load from files instead of using generators
4. Update `.gitignore` to exclude the `testdata/` directory

Example modification:
```csharp
private static byte[] LoadFromFile(string corpusType, string filename)
{
    var path = Path.Combine("testdata", corpusType, filename);
    return File.ReadAllBytes(path);
}
```

## License

The benchmark code is released under the same BSD-2-Clause license as LZ4Sharp.
Note: Original corpus files may have different licenses - check before distributing.
