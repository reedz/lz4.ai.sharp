using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using K4os.Compression.LZ4;
using LZ4Sharp;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace LZ4Sharp.Benchmarks;

/// <summary>
/// Benchmark comparing LZ4Frame with fast compression (-1 level) vs K4os LZ4Pickler (L00_FAST).
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Config))]
public class FrameFastBenchmarks
{
    private class Config : ManualConfig
    {
        public Config() => AddColumn(CompressionRatioColumn.Instance);
    }

    private static readonly DateTime BaseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const int PayloadCount = 100;

    private byte[][] _payloads = null!;
    private byte[][] _compressedFrames = null!;
    private byte[][] _compressedPickles = null!;

    private byte[] _frameBuffer = null!;
    private byte[] _decompressBuffer = null!;
    private LZ4Frame.FramePreferences _framePrefs = null!;

    [Params("1kb", "7kb", "16kb", "72kb")]
    public string JsonType { get; set; } = "1kb";

    [Params(1, 8)]
    public int Acceleration { get; set; } = 1;

    public double FrameRatio { get; private set; }
    public double PicklerRatio { get; private set; }
    public long TotalOriginalSize { get; private set; }

    [GlobalSetup]
    public void Setup()
    {
        int targetSize = JsonType switch
        {
            "1kb" => 1024,
            "7kb" => 7 * 1024,
            "16kb" => 16 * 1024,
            "72kb" => 72 * 1024,
            _ => 1024
        };

        _payloads = new byte[PayloadCount][];
        for (int i = 0; i < PayloadCount; i++)
            _payloads[i] = GenerateUniqueJsonPayload(targetSize, i);

        int maxPayloadSize = _payloads.Max(p => p.Length);
        _decompressBuffer = new byte[maxPayloadSize];

        // Use negative compression level to trigger CompressFast
        _framePrefs = new LZ4Frame.FramePreferences
        {
            CompressionLevel = -Acceleration,
            BlockMode = LZ4Frame.BlockMode.Independent
        };

        _frameBuffer = new byte[LZ4Frame.CompressFrameBound(maxPayloadSize, _framePrefs)];

        _compressedFrames = new byte[PayloadCount][];
        _compressedPickles = new byte[PayloadCount][];

        long totalOriginalSize = 0;
        long totalFramesSize = 0;
        long totalPicklesSize = 0;

        for (int i = 0; i < PayloadCount; i++)
        {
            var payload = _payloads[i];
            totalOriginalSize += payload.Length;

            int frameSize = LZ4Frame.CompressFrame(_frameBuffer, _frameBuffer.Length, payload, payload.Length, _framePrefs);
            if (frameSize <= 0) throw new InvalidOperationException("LZ4Frame.CompressFrame failed");

            var frame = new byte[frameSize];
            Array.Copy(_frameBuffer, 0, frame, 0, frameSize);
            _compressedFrames[i] = frame;
            totalFramesSize += frameSize;

            var pickle = LZ4Pickler.Pickle(payload, LZ4Level.L00_FAST);
            _compressedPickles[i] = pickle;
            totalPicklesSize += pickle.Length;
        }

        TotalOriginalSize = totalOriginalSize;
        FrameRatio = totalFramesSize / (double)totalOriginalSize;
        PicklerRatio = totalPicklesSize / (double)totalOriginalSize;

        CompressionRatioStore.Set($"FrameFast|{JsonType}|{Acceleration}|LZ4Sharp", totalOriginalSize, totalFramesSize);
        CompressionRatioStore.Set($"FrameFast|{JsonType}|{Acceleration}|K4os", totalOriginalSize, totalPicklesSize);
    }

    [Benchmark(Description = "LZ4Sharp LZ4Frame (fast) - Compress 100")]
    public long CompressAllFrames()
    {
        long total = 0;
        for (int i = 0; i < PayloadCount; i++)
        {
            var payload = _payloads[i];
            total += LZ4Frame.CompressFrame(_frameBuffer, _frameBuffer.Length, payload, payload.Length, _framePrefs);
        }
        return total;
    }

    [Benchmark(Description = "K4os LZ4Pickler (L00_FAST) - Compress 100", Baseline = true)]
    public long CompressAllPickles()
    {
        long total = 0;
        for (int i = 0; i < PayloadCount; i++)
        {
            var payload = _payloads[i];
            total += LZ4Pickler.Pickle(payload, LZ4Level.L00_FAST).Length;
        }
        return total;
    }

    [Benchmark(Description = "LZ4Sharp LZ4Frame (fast) - Decompress 100")]
    public long DecompressAllFrames()
    {
        long total = 0;
        for (int i = 0; i < PayloadCount; i++)
        {
            var compressed = _compressedFrames[i];
            total += LZ4Frame.DecompressFrame(_decompressBuffer, _decompressBuffer.Length, compressed, compressed.Length);
        }
        return total;
    }

    [Benchmark(Description = "K4os LZ4Pickler (L00_FAST) - Decompress 100")]
    public long DecompressAllPickles()
    {
        long total = 0;
        for (int i = 0; i < PayloadCount; i++)
        {
            total += LZ4Pickler.Unpickle(_compressedPickles[i]).Length;
        }
        return total;
    }

    private static byte[] GenerateUniqueJsonPayload(int targetSize, int seed)
    {
        var random = new Random(42 + seed);
        var sb = new StringBuilder();
        sb.Append('[');

        int itemIndex = 0;
        while (sb.Length < targetSize - 300)
        {
            if (itemIndex > 0) sb.Append(',');

            var item = new
            {
                id = Guid.NewGuid().ToString(),
                index = itemIndex,
                seed = seed,
                timestamp = BaseDate.AddSeconds(random.Next(0, 31536000)).ToString("o"),
                name = $"User_{random.Next(10000, 99999)}_{seed}_{itemIndex}",
                email = $"user{random.Next(1000, 9999)}@domain{random.Next(1, 100)}.com",
                active = random.Next(2) == 1,
                score = random.NextDouble() * 1000,
                balance = random.NextDouble() * 10000 - 5000,
                tags = new[]
                {
                    $"tag{random.Next(1, 50)}",
                    $"tag{random.Next(50, 100)}",
                    $"tag{random.Next(100, 150)}"
                },
                metadata = new
                {
                    version = $"{random.Next(1, 10)}.{random.Next(0, 20)}.{random.Next(0, 100)}",
                    region = new[] { "us-east", "us-west", "eu-west", "ap-south" }[random.Next(4)],
                    tier = new[] { "free", "basic", "pro", "enterprise" }[random.Next(4)],
                    created = BaseDate.AddDays(random.Next(0, 1000)).ToString("yyyy-MM-dd")
                }
            };

            sb.Append(JsonSerializer.Serialize(item));
            itemIndex++;
        }

        sb.Append(']');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
