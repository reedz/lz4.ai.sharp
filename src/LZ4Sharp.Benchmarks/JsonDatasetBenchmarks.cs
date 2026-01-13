using BenchmarkDotNet.Attributes;
using K4os.Compression.LZ4;
using LZ4Sharp;
using System;
using System.Linq;

namespace LZ4Sharp.Benchmarks;

[MemoryDiagnoser]
public class JsonDatasetBenchmarks
{
    private JsonDatasetCorpus.Payload[] _payloads = null!;
    private int[] _originalSizes = null!;

    private byte[] _compressBuffer = null!;
    private byte[] _decompressBuffer = null!;

    private byte[][] _compressedLZ4Sharp = null!;
    private int[] _compressedSizesLZ4Sharp = null!;

    private byte[][] _compressedK4os = null!;
    private int[] _compressedSizesK4os = null!;

    [Params("<10kb", "<100kb", "<1mb", ">1mb")]
    public string PayloadGroup { get; set; } = "<10kb";

    [Params(3, 6, 9, 12)]
    public int CompressionLevel { get; set; } = LZ4HC.CLEVEL_DEFAULT;

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

        _payloads = JsonDatasetCorpus.Get(group).ToArray();
        _originalSizes = _payloads.Select(p => p.Size).ToArray();

        int maxPayload = _originalSizes.Max();
        int maxCompressed = Math.Max(
            LZ4HC.CompressBound(maxPayload),
            K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(maxPayload));

        _compressBuffer = new byte[maxCompressed];
        _decompressBuffer = new byte[maxPayload];

        _compressedLZ4Sharp = new byte[_payloads.Length][];
        _compressedSizesLZ4Sharp = new int[_payloads.Length];

        _compressedK4os = new byte[_payloads.Length][];
        _compressedSizesK4os = new int[_payloads.Length];

        for (int i = 0; i < _payloads.Length; i++)
        {
            var payload = _payloads[i].Data;

            int maxOut = LZ4HC.CompressBound(payload.Length);
            _compressedLZ4Sharp[i] = new byte[maxOut];
            _compressedSizesLZ4Sharp[i] = LZ4HC.CompressHC(payload, _compressedLZ4Sharp[i], payload.Length, maxOut, CompressionLevel);

            int maxOutK4os = K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(payload.Length);
            _compressedK4os[i] = new byte[maxOutK4os];
            _compressedSizesK4os[i] = K4os.Compression.LZ4.LZ4Codec.Encode(payload, 0, payload.Length, _compressedK4os[i], 0, maxOutK4os, (LZ4Level)CompressionLevel);
        }
    }

    [Benchmark(Description = "LZ4Sharp (LZ4HC) - Compress", Baseline = true)]
    public long CompressAllLZ4Sharp()
    {
        long total = 0;
        for (int i = 0; i < _payloads.Length; i++)
        {
            var payload = _payloads[i].Data;
            total += LZ4HC.CompressHC(payload, _compressBuffer, payload.Length, _compressBuffer.Length, CompressionLevel);
        }
        return total;
    }

    [Benchmark(Description = "K4os.LZ4Codec - Compress")
    ]
    public long CompressAllK4os()
    {
        long total = 0;
        for (int i = 0; i < _payloads.Length; i++)
        {
            var payload = _payloads[i].Data;
            total += K4os.Compression.LZ4.LZ4Codec.Encode(payload, 0, payload.Length, _compressBuffer, 0, _compressBuffer.Length, (LZ4Level)CompressionLevel);
        }
        return total;
    }

    [Benchmark(Description = "LZ4Sharp - Decompress")]
    public long DecompressAllLZ4Sharp()
    {
        long total = 0;
        for (int i = 0; i < _payloads.Length; i++)
        {
            var compressed = _compressedLZ4Sharp[i];
            int compressedSize = _compressedSizesLZ4Sharp[i];
            int originalSize = _originalSizes[i];
            total += LZ4Codec.DecompressSafe(compressed, _decompressBuffer, compressedSize, originalSize);
        }
        return total;
    }

    [Benchmark(Description = "K4os.LZ4Codec - Decompress")]
    public long DecompressAllK4os()
    {
        long total = 0;
        for (int i = 0; i < _payloads.Length; i++)
        {
            var compressed = _compressedK4os[i];
            int compressedSize = _compressedSizesK4os[i];
            int originalSize = _originalSizes[i];
            total += K4os.Compression.LZ4.LZ4Codec.Decode(compressed, 0, compressedSize, _decompressBuffer, 0, originalSize);
        }
        return total;
    }
}
