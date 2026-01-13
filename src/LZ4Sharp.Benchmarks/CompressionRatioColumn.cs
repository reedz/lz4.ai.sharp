using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;

namespace LZ4Sharp.Benchmarks;

internal static class CompressionRatioStore
{
    internal readonly record struct Entry(long InputBytes, long OutputBytes);

    private static readonly ConcurrentDictionary<string, Entry> _entries = new();

    public static void Set(string key, long inputBytes, long outputBytes) =>
        _entries[key] = new Entry(inputBytes, outputBytes);

    public static bool TryGet(string key, out Entry entry) =>
        _entries.TryGetValue(key, out entry);
}

internal sealed class CompressionRatioColumn : IColumn
{
    public static readonly CompressionRatioColumn Instance = new();

    public string Id => nameof(CompressionRatioColumn);
    public string ColumnName => "CRatio";
    public string Legend => "Compression ratio: CompressedBytes / InputBytes (lower is better)";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;

    public bool IsAvailable(Summary summary) => true;

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase) =>
        GetValue(summary, benchmarkCase, summary.Style);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        static string? GetParam(BenchmarkCase bc, string name) =>
            bc.Parameters.Items.FirstOrDefault(p => p.Name == name)?.Value?.ToString();

        string impl = benchmarkCase.Descriptor.WorkloadMethod.Name.Contains("K4os", StringComparison.OrdinalIgnoreCase)
            ? "K4os"
            : "LZ4Sharp";

        string key;
        if (benchmarkCase.Descriptor.Type == typeof(JsonDatasetLevel0Benchmarks))
        {
            var group = GetParam(benchmarkCase, "PayloadGroup") ?? "?";
            var accel = GetParam(benchmarkCase, "Acceleration") ?? "?";
            key = $"JsonDatasetLevel0|{group}|{accel}|{impl}";
        }
        else if (benchmarkCase.Descriptor.Type == typeof(SilesiaCodecLevel0Benchmarks))
        {
            var file = GetParam(benchmarkCase, "FileName") ?? "?";
            var accel = GetParam(benchmarkCase, "Acceleration") ?? "?";
            key = $"SilesiaLevel0|{file}|{accel}|{impl}";
        }
        else
        {
            return "?";
        }

        if (!CompressionRatioStore.TryGet(key, out var entry) || entry.InputBytes <= 0)
        {
            if (!TryComputeAndCache(key, benchmarkCase, impl, out entry) || entry.InputBytes <= 0)
                return "?";
        }

        double ratio = (double)entry.OutputBytes / entry.InputBytes;
        return ratio.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private static bool TryComputeAndCache(string key, BenchmarkCase benchmarkCase, string impl, out CompressionRatioStore.Entry entry)
    {
        entry = default;

        static string? GetParam(BenchmarkCase bc, string name) =>
            bc.Parameters.Items.FirstOrDefault(p => p.Name == name)?.Value?.ToString();

        try
        {
            if (benchmarkCase.Descriptor.Type == typeof(JsonDatasetLevel0Benchmarks))
            {
                var groupStr = GetParam(benchmarkCase, "PayloadGroup") ?? "<10kb";
                var accelStr = GetParam(benchmarkCase, "Acceleration") ?? "8";
                int acceleration = int.TryParse(accelStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : 8;

                var group = groupStr switch
                {
                    "<10kb" => JsonDatasetCorpus.Group.Lt10Kb,
                    "<100kb" => JsonDatasetCorpus.Group.Lt100Kb,
                    "<1mb" => JsonDatasetCorpus.Group.Lt1Mb,
                    ">1mb" => JsonDatasetCorpus.Group.Gt1Mb,
                    _ => JsonDatasetCorpus.Group.Lt10Kb
                };

                var payloads = JsonDatasetCorpus.Get(group).ToArray();
                int maxPayload = payloads.Max(p => p.Size);
                int maxCompressed = Math.Max(
                    LZ4Codec.CompressBound(maxPayload),
                    K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(maxPayload));

                var buffer = new byte[maxCompressed];

                long totalBytes = 0;
                long outBytes = 0;
                for (int i = 0; i < payloads.Length; i++)
                {
                    var payload = payloads[i].Data;
                    totalBytes += payload.Length;

                    outBytes += impl == "K4os"
                        ? K4os.Compression.LZ4.LZ4Codec.Encode(payload, 0, payload.Length, buffer, 0, buffer.Length, K4os.Compression.LZ4.LZ4Level.L00_FAST)
                        : LZ4Codec.CompressFast(payload, buffer, payload.Length, buffer.Length, acceleration);
                }

                CompressionRatioStore.Set(key, totalBytes, outBytes);
                entry = new CompressionRatioStore.Entry(totalBytes, outBytes);
                return true;
            }

            if (benchmarkCase.Descriptor.Type == typeof(SilesiaCodecLevel0Benchmarks))
            {
                var fileName = GetParam(benchmarkCase, "FileName") ?? "dickens";
                var accelStr = GetParam(benchmarkCase, "Acceleration") ?? "8";
                int acceleration = int.TryParse(accelStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : 8;

                var input = SilesiaCodecBenchmarks.Corpus[fileName];
                int maxOut = Math.Max(
                    LZ4Codec.CompressBound(input.Length),
                    K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(input.Length));
                var dest = new byte[maxOut];

                long inputBytes = input.Length;
                long outBytes = impl == "K4os"
                    ? K4os.Compression.LZ4.LZ4Codec.Encode(input, 0, input.Length, dest, 0, dest.Length, K4os.Compression.LZ4.LZ4Level.L00_FAST)
                    : LZ4Codec.CompressFast(input, dest, input.Length, dest.Length, acceleration);

                CompressionRatioStore.Set(key, inputBytes, outBytes);
                entry = new CompressionRatioStore.Entry(inputBytes, outBytes);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
