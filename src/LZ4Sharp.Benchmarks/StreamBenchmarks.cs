using BenchmarkDotNet.Attributes;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using LZ4Sharp.Streams;
using System;
using System.IO;
using System.Linq;

namespace LZ4Sharp.Benchmarks;

/// <summary>
/// Benchmark comparing LZ4Sharp streaming API vs K4os streaming API.
/// Covers compress-only, decompress-only, and full roundtrip on the JSON dataset corpus.
/// </summary>
[MemoryDiagnoser]
public class StreamBenchmarks
{
    private byte[][] _payloads = null!;
    private byte[][] _compressedLZ4Sharp = null!;
    private byte[][] _compressedK4os = null!;

    [Params("<10kb", "<100kb", "<1mb", ">1mb")]
    public string PayloadGroup { get; set; } = "<10kb";

    [Params(-1, 3, 9, 12)]
    public int CompressionLevel { get; set; } = -1;

    [GlobalSetup]
    public void Setup()
    {
        var group = PayloadGroup switch
        {
            "<10kb" => JsonDatasetCorpus.Group.Lt10Kb,
            "<100kb" => JsonDatasetCorpus.Group.Lt100Kb,
            "<1mb" => JsonDatasetCorpus.Group.Lt1Mb,
            ">1mb" => JsonDatasetCorpus.Group.Gt1Mb,
            _ => JsonDatasetCorpus.Group.Lt10Kb
        };

        _payloads = JsonDatasetCorpus.Get(group).Select(p => p.Data).ToArray();

        // Pre-compress all payloads for decompress-only benchmarks
        _compressedLZ4Sharp = new byte[_payloads.Length][];
        _compressedK4os = new byte[_payloads.Length][];

        var lz4SharpLevel = (LZ4CompressionLevel)CompressionLevel;
        var k4osLevel = CompressionLevel < 0 ? LZ4Level.L00_FAST : (LZ4Level)CompressionLevel;

        for (int i = 0; i < _payloads.Length; i++)
        {
            _compressedLZ4Sharp[i] = CompressWithLZ4Sharp(_payloads[i], lz4SharpLevel);
            _compressedK4os[i] = CompressWithK4os(_payloads[i], k4osLevel);
        }
    }

    // --- Compress benchmarks ---

    [Benchmark(Description = "LZ4Sharp Stream - Compress")]
    public long CompressLZ4Sharp()
    {
        long total = 0;
        var level = (LZ4CompressionLevel)CompressionLevel;
        for (int i = 0; i < _payloads.Length; i++)
        {
            using var ms = new MemoryStream();
            using (var encoder = Streams.LZ4Stream.Encode(ms, level, leaveOpen: true))
            {
                encoder.Write(_payloads[i]);
            }
            total += ms.Length;
        }
        return total;
    }

    [Benchmark(Description = "K4os Stream - Compress", Baseline = true)]
    public long CompressK4os()
    {
        long total = 0;
        var level = CompressionLevel < 0 ? LZ4Level.L00_FAST : (LZ4Level)CompressionLevel;
        for (int i = 0; i < _payloads.Length; i++)
        {
            using var ms = new MemoryStream();
            using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(ms, level, leaveOpen: true))
            {
                encoder.Write(_payloads[i]);
            }
            total += ms.Length;
        }
        return total;
    }

    // --- Decompress benchmarks ---

    [Benchmark(Description = "LZ4Sharp Stream - Decompress")]
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

    [Benchmark(Description = "K4os Stream - Decompress")]
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

    // --- Roundtrip benchmarks ---

    [Benchmark(Description = "LZ4Sharp Stream - Roundtrip")]
    public long RoundtripLZ4Sharp()
    {
        long total = 0;
        var level = (LZ4CompressionLevel)CompressionLevel;
        for (int i = 0; i < _payloads.Length; i++)
        {
            using var compressed = new MemoryStream();
            using (var encoder = Streams.LZ4Stream.Encode(compressed, level, leaveOpen: true))
            {
                encoder.Write(_payloads[i]);
            }

            compressed.Position = 0;
            using var decoder = Streams.LZ4Stream.Decode(compressed);
            using var output = new MemoryStream();
            decoder.CopyTo(output);
            total += output.Length;
        }
        return total;
    }

    [Benchmark(Description = "K4os Stream - Roundtrip")]
    public long RoundtripK4os()
    {
        long total = 0;
        var level = CompressionLevel < 0 ? LZ4Level.L00_FAST : (LZ4Level)CompressionLevel;
        for (int i = 0; i < _payloads.Length; i++)
        {
            using var compressed = new MemoryStream();
            using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(compressed, level, leaveOpen: true))
            {
                encoder.Write(_payloads[i]);
            }

            compressed.Position = 0;
            using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(compressed);
            using var output = new MemoryStream();
            decoder.CopyTo(output);
            total += output.Length;
        }
        return total;
    }

    // --- Helpers ---

    private static byte[] CompressWithLZ4Sharp(byte[] data, LZ4CompressionLevel level)
    {
        using var ms = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(ms, level, leaveOpen: true))
        {
            encoder.Write(data);
        }
        return ms.ToArray();
    }

    private static byte[] CompressWithK4os(byte[] data, LZ4Level level)
    {
        using var ms = new MemoryStream();
        using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(ms, level, leaveOpen: true))
        {
            encoder.Write(data);
        }
        return ms.ToArray();
    }
}
