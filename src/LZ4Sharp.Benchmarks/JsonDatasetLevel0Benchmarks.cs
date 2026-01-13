using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using K4os.Compression.LZ4;
using LZ4Sharp;
using System;
using System.Linq;

namespace LZ4Sharp.Benchmarks;

/// <summary>
/// Dedicated level-0 (fastest) JSON corpus compression benchmark.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Config))]
public class JsonDatasetLevel0Benchmarks
{
    private class Config : ManualConfig
    {
        public Config() => AddColumn(CompressionRatioColumn.Instance);
    }

    private JsonDatasetCorpus.Payload[] _payloads = null!;
    private byte[] _compressBuffer = null!;

    [Params("<10kb", "<100kb", "<1mb", ">1mb")]
    public string PayloadGroup { get; set; } = "<10kb";

    [Params(1, 2, 4, 8, 16)]
    public int Acceleration { get; set; } = 8;

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

        int maxPayload = _payloads.Max(p => p.Size);
        int maxCompressed = Math.Max(
            LZ4Codec.CompressBound(maxPayload),
            K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(maxPayload));

        _compressBuffer = new byte[maxCompressed];

        long totalBytes = 0;
        long k4osBytes = 0;
        long lz4sharpBytes = 0;

        for (int i = 0; i < _payloads.Length; i++)
        {
            var payload = _payloads[i].Data;
            totalBytes += payload.Length;

            k4osBytes += K4os.Compression.LZ4.LZ4Codec.Encode(
                payload, 0, payload.Length,
                _compressBuffer, 0, _compressBuffer.Length,
                LZ4Level.L00_FAST);

            lz4sharpBytes += LZ4Codec.CompressFast(payload, _compressBuffer, payload.Length, _compressBuffer.Length, Acceleration);
        }

        CompressionRatioStore.Set($"JsonDatasetLevel0|{PayloadGroup}|{Acceleration}|K4os", totalBytes, k4osBytes);
        CompressionRatioStore.Set($"JsonDatasetLevel0|{PayloadGroup}|{Acceleration}|LZ4Sharp", totalBytes, lz4sharpBytes);
    }

    [Benchmark(Description = "K4os.LZ4Codec - Encode (L00_FAST)", Baseline = true)]
    public long CompressAllK4os()
    {
        long total = 0;
        for (int i = 0; i < _payloads.Length; i++)
        {
            var payload = _payloads[i].Data;
            total += K4os.Compression.LZ4.LZ4Codec.Encode(
                payload, 0, payload.Length,
                _compressBuffer, 0, _compressBuffer.Length,
                LZ4Level.L00_FAST);
        }
        return total;
    }

    [Benchmark(Description = "LZ4Sharp - CompressFast (level 0)")]
    public long CompressAllLZ4Sharp()
    {
        long total = 0;
        for (int i = 0; i < _payloads.Length; i++)
        {
            var payload = _payloads[i].Data;
            total += LZ4Codec.CompressFast(payload, _compressBuffer, payload.Length, _compressBuffer.Length, Acceleration);
        }
        return total;
    }
}
