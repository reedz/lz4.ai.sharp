using K4os.Compression.LZ4;
using LZ4Sharp;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace LZ4Sharp.Benchmarks;

internal static class Level0ProfileHarness
{
    internal enum Impl
    {
        LZ4Sharp,
        K4os
    }

    internal enum Workload
    {
        JsonDataset,
        GeneratedJson
    }

    public static int Run(string[] args)
    {
        // Defaults chosen to produce a stable CPU profile quickly.
        string groupStr = GetArg(args, "--group") ?? "<1mb";
        int seconds = int.TryParse(GetArg(args, "--seconds"), out var s) ? s : 10;
        int startDelay = int.TryParse(GetArg(args, "--start-delay"), out var d) ? d : 0;
        int acceleration = int.TryParse(GetArg(args, "--acceleration"), out var a) ? a : 8;
        var workload = Enum.TryParse(GetArg(args, "--workload"), ignoreCase: true, out Workload w) ? w : Workload.JsonDataset;
        int payloadCount = int.TryParse(GetArg(args, "--count"), out var c) ? c : 1000;
        int payloadSize = int.TryParse(GetArg(args, "--size"), out var z) ? z : 72 * 1024;
        var impl = Enum.TryParse(GetArg(args, "--impl"), ignoreCase: true, out Impl parsed) ? parsed : Impl.LZ4Sharp;

        var payloads = workload switch
        {
            Workload.JsonDataset => JsonDatasetCorpus.Get(groupStr switch
            {
                "<10kb" => JsonDatasetCorpus.Group.Lt10Kb,
                "<100kb" => JsonDatasetCorpus.Group.Lt100Kb,
                "<1mb" => JsonDatasetCorpus.Group.Lt1Mb,
                ">1mb" => JsonDatasetCorpus.Group.Gt1Mb,
                _ => JsonDatasetCorpus.Group.Lt1Mb
            }).Select(p => p.Data).ToArray(),
            Workload.GeneratedJson => GenerateUniqueJsonPayloads(payloadSize, payloadCount),
            _ => JsonDatasetCorpus.Get(JsonDatasetCorpus.Group.Lt1Mb).Select(p => p.Data).ToArray()
        };

        int maxPayload = payloads.Max(p => p.Length);
        int maxCompressed = Math.Max(LZ4Codec.CompressBound(maxPayload), K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(maxPayload));
        byte[] dest = new byte[maxCompressed];

        // Warmup JIT
        for (int i = 0; i < Math.Min(payloads.Length, 16); i++)
        {
            var p = payloads[i];
            _ = impl == Impl.LZ4Sharp
                ? LZ4Codec.CompressFast(p, dest, p.Length, dest.Length, acceleration)
                : K4os.Compression.LZ4.LZ4Codec.Encode(p, 0, p.Length, dest, 0, dest.Length, LZ4Level.L00_FAST);
        }

        if (startDelay > 0)
        {
            Console.WriteLine($"pid={Environment.ProcessId} delaying={startDelay}s (attach dotnet-trace now)");
            Thread.Sleep(startDelay * 1000);
        }

        long totalBytes = 0;
        long totalOut = 0;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            for (int i = 0; i < payloads.Length; i++)
            {
                var p = payloads[i];
                totalBytes += p.Length;
                totalOut += impl == Impl.LZ4Sharp
                    ? LZ4Codec.CompressFast(p, dest, p.Length, dest.Length, acceleration)
                    : K4os.Compression.LZ4.LZ4Codec.Encode(p, 0, p.Length, dest, 0, dest.Length, LZ4Level.L00_FAST);
            }
        }

        double mb = totalBytes / 1024.0 / 1024.0;
        double mbs = mb / sw.Elapsed.TotalSeconds;

        double ratio = totalOut / (double)totalBytes;
        Console.WriteLine($"impl={impl} workload={workload} group={groupStr} max={maxPayload} count={payloads.Length} seconds={seconds} accel={acceleration} bytes={totalBytes:N0} out={totalOut:N0} ratio={ratio:F4} throughput={mbs:F1} MiB/s");
        return 0;
    }

    private static byte[][] GenerateUniqueJsonPayloads(int targetSize, int count)
    {
        var payloads = new byte[count][];
        for (int i = 0; i < count; i++)
            payloads[i] = GenerateUniqueJsonPayload(targetSize, i);
        return payloads;
    }

    private static byte[] GenerateUniqueJsonPayload(int targetSize, int seed)
    {
        // Duplicated from JsonBenchmarks to allow profiling level-0/fast path over many payloads.
        var random = new Random(42 + seed);
        var sb = new System.Text.StringBuilder();
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
                timestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(random.Next(0, 31536000)).ToString("o"),
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
                    created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(random.Next(0, 1000)).ToString("yyyy-MM-dd")
                }
            };

            sb.Append(System.Text.Json.JsonSerializer.Serialize(item));
            itemIndex++;
        }

        sb.Append(']');
        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }
}
