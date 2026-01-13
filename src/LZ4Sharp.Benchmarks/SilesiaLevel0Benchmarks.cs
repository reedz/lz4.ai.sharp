using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using K4os.Compression.LZ4;
using LZ4Sharp;
using System;

namespace LZ4Sharp.Benchmarks;

/// <summary>
/// Dedicated Silesia corpus level-0 (fastest) compression benchmark.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Config))]
public class SilesiaCodecLevel0Benchmarks
{
    private class Config : ManualConfig
    {
        public Config() => AddColumn(CompressionRatioColumn.Instance);
    }

    private byte[] _input = null!;
    private byte[] _dest = null!;

    [ParamsSource(nameof(Files))]
    public string FileName { get; set; } = "dickens";

    [Params(1, 2, 4, 8, 16)]
    public int Acceleration { get; set; } = 8;

    public static System.Collections.Generic.IEnumerable<string> Files
    {
        get
        {
            var single = Environment.GetEnvironmentVariable("SILESIA_FILE");
            return string.IsNullOrWhiteSpace(single)
                ? SilesiaCodecBenchmarks.Files
                : new[] { single };
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        _input = SilesiaCodecBenchmarks.Corpus[FileName];

        int maxOut = Math.Max(
            LZ4Codec.CompressBound(_input.Length),
            K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(_input.Length));
        _dest = new byte[maxOut];

        long inputBytes = _input.Length;
        long k4osBytes = K4os.Compression.LZ4.LZ4Codec.Encode(_input, 0, _input.Length, _dest, 0, _dest.Length, LZ4Level.L00_FAST);
        long lz4sharpBytes = LZ4Codec.CompressFast(_input, _dest, _input.Length, _dest.Length, Acceleration);

        CompressionRatioStore.Set($"SilesiaLevel0|{FileName}|{Acceleration}|K4os", inputBytes, k4osBytes);
        CompressionRatioStore.Set($"SilesiaLevel0|{FileName}|{Acceleration}|LZ4Sharp", inputBytes, lz4sharpBytes);
    }

    [Benchmark(Description = "K4os.LZ4Codec - Encode (L00_FAST)", Baseline = true)]
    public int K4osCompress()
    {
        return K4os.Compression.LZ4.LZ4Codec.Encode(_input, 0, _input.Length, _dest, 0, _dest.Length, LZ4Level.L00_FAST);
    }

    [Benchmark(Description = "LZ4Sharp - CompressFast (level 0)")]
    public int LZ4SharpCompress()
    {
        return LZ4Codec.CompressFast(_input, _dest, _input.Length, _dest.Length, Acceleration);
    }
}
