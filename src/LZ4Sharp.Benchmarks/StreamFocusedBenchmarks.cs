using BenchmarkDotNet.Attributes;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using LZ4Sharp.Streams;
using System;
using System.IO;

namespace LZ4Sharp.Benchmarks;

/// <summary>
/// Focused streaming benchmark for A/B comparison of streaming optimizations.
/// Uses a single payload group (>1mb) and fast compression only to keep runtime short.
/// </summary>
[MemoryDiagnoser]
public class StreamFocusedBenchmarks
{
    private byte[][] _payloads = null!;
    private byte[][] _compressedLZ4Sharp = null!;
    private byte[][] _compressedK4os = null!;
    private byte[] _randomPayload = null!;     // incompressible
    private byte[] _randomCompressedLZ4Sharp = null!;
    private byte[] _randomCompressedK4os = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Compressible payloads from JSON dataset
        _payloads = JsonDatasetCorpus.Get(JsonDatasetCorpus.Group.Gt1Mb)
            .Select(p => p.Data).ToArray();

        _compressedLZ4Sharp = new byte[_payloads.Length][];
        _compressedK4os = new byte[_payloads.Length][];

        for (int i = 0; i < _payloads.Length; i++)
        {
            _compressedLZ4Sharp[i] = CompressWithLZ4Sharp(_payloads[i]);
            _compressedK4os[i] = CompressWithK4os(_payloads[i]);
        }

        // Incompressible payload (pure random - exercises uncompressed block path)
        _randomPayload = new byte[4 * 1024 * 1024];
        new Random(99).NextBytes(_randomPayload);
        _randomCompressedLZ4Sharp = CompressWithLZ4Sharp(_randomPayload);
        _randomCompressedK4os = CompressWithK4os(_randomPayload);
    }

    // --- Compressible Compress ---

    [Benchmark(Description = "LZ4Sharp Compress (>1mb)")]
    public long CompressLZ4Sharp()
    {
        long total = 0;
        for (int i = 0; i < _payloads.Length; i++)
        {
            using var ms = new MemoryStream();
            using (var encoder = Streams.LZ4Stream.Encode(ms, LZ4CompressionLevel.Fast, leaveOpen: true))
                encoder.Write(_payloads[i]);
            total += ms.Length;
        }
        return total;
    }

    [Benchmark(Description = "K4os Compress (>1mb)", Baseline = true)]
    public long CompressK4os()
    {
        long total = 0;
        for (int i = 0; i < _payloads.Length; i++)
        {
            using var ms = new MemoryStream();
            using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(ms, LZ4Level.L00_FAST, leaveOpen: true))
                encoder.Write(_payloads[i]);
            total += ms.Length;
        }
        return total;
    }

    // --- Compressible Decompress ---

    [Benchmark(Description = "LZ4Sharp Decompress (>1mb)")]
    public long DecompressLZ4Sharp()
    {
        long total = 0;
        for (int i = 0; i < _compressedLZ4Sharp.Length; i++)
        {
            using var ms = new MemoryStream(_compressedLZ4Sharp[i]);
            using var decoder = Streams.LZ4Stream.Decode(ms);
            using var output = new MemoryStream();
            decoder.CopyTo(output);
            total += output.Length;
        }
        return total;
    }

    [Benchmark(Description = "K4os Decompress (>1mb)")]
    public long DecompressK4os()
    {
        long total = 0;
        for (int i = 0; i < _compressedK4os.Length; i++)
        {
            using var ms = new MemoryStream(_compressedK4os[i]);
            using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(ms);
            using var output = new MemoryStream();
            decoder.CopyTo(output);
            total += output.Length;
        }
        return total;
    }

    // --- Incompressible (uncompressed blocks) ---

    [Benchmark(Description = "LZ4Sharp Compress (random 4MB)")]
    public long CompressRandomLZ4Sharp()
    {
        using var ms = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(ms, LZ4CompressionLevel.Fast, leaveOpen: true))
            encoder.Write(_randomPayload);
        return ms.Length;
    }

    [Benchmark(Description = "K4os Compress (random 4MB)")]
    public long CompressRandomK4os()
    {
        using var ms = new MemoryStream();
        using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(ms, LZ4Level.L00_FAST, leaveOpen: true))
            encoder.Write(_randomPayload);
        return ms.Length;
    }

    [Benchmark(Description = "LZ4Sharp Decompress (random 4MB)")]
    public long DecompressRandomLZ4Sharp()
    {
        using var ms = new MemoryStream(_randomCompressedLZ4Sharp);
        using var decoder = Streams.LZ4Stream.Decode(ms);
        using var output = new MemoryStream();
        decoder.CopyTo(output);
        return output.Length;
    }

    [Benchmark(Description = "K4os Decompress (random 4MB)")]
    public long DecompressRandomK4os()
    {
        using var ms = new MemoryStream(_randomCompressedK4os);
        using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(ms);
        using var output = new MemoryStream();
        decoder.CopyTo(output);
        return output.Length;
    }

    // --- Helpers ---

    private static byte[] CompressWithLZ4Sharp(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(ms, LZ4CompressionLevel.Fast, leaveOpen: true))
            encoder.Write(data);
        return ms.ToArray();
    }

    private static byte[] CompressWithK4os(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(ms, LZ4Level.L00_FAST, leaveOpen: true))
            encoder.Write(data);
        return ms.ToArray();
    }
}
