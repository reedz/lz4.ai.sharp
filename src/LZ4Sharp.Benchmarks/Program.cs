using BenchmarkDotNet.Running;
using System;
using System.Diagnostics;
using System.Linq;

namespace LZ4Sharp.Benchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Contains("--ratios"))
        {
            PrintAnalysis();
            return;
        }

        if (args.Contains("--frame-ratios"))
        {
            PrintFrameRatios();
            return;
        }

        if (args.Contains("--silesia"))
        {
            SilesiaAnalysis.Run();
            return;
        }

        if (args.Contains("--json-dataset-stats"))
        {
            PrintJsonDatasetStats();
            return;
        }

        if (args.Contains("--json-dataset-smoke"))
        {
            RunJsonDatasetSmoke();
            return;
        }

        if (args.Contains("--level0-profile"))
        {
            Environment.Exit(Level0ProfileHarness.Run(args));
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    private static void PrintAnalysis()
    {
        Console.WriteLine("Running Compression Analysis (Ratio & Performance)...");
        Console.WriteLine("------------------------------------------------------------------------------------------------------------------");
        Console.WriteLine($"| {"Size",-6} | {"Level",-5} | {"LZ4Sharp %",-10} | {"K4os %",-10} | {"Delta",-8} | {"LZ4Sharp",-10} | {"K4os",-10} | {"Speedup",-8} |");
        Console.WriteLine($"| {"",-6} | {"",-5} | {"",-10} | {"",-10} | {"",-8} | {"(MB/s)",-10} | {"(MB/s)",-10} | {"",-8} |");
        Console.WriteLine("------------------------------------------------------------------------------------------------------------------");

        var sizes = new[] { "1kb", "7kb", "16kb", "72kb" };
        var levels = new[] { 3, 6, 9, 12 };

        foreach (var size in sizes)
        {
            foreach (var level in levels)
            {
                var bench = new JsonBenchmarks();
                bench.JsonType = size;
                bench.CompressionLevel = level;
                
                // Setup (silence output)
                var originalOut = Console.Out;
                Console.SetOut(System.IO.TextWriter.Null);
                try { bench.Setup(); } finally { Console.SetOut(originalOut); }

                // Measure Performance
                double lz4Speed = MeasureMBs(() => bench.CompressAllLZ4Sharp(), bench.TotalOriginalSize);
                double k4osSpeed = MeasureMBs(() => bench.CompressAllK4os(), bench.TotalOriginalSize);

                // Calculate Metrics
                string ratioDelta = (bench.LZ4SharpRatio - bench.K4osRatio) switch 
                {
                    < -0.0001 => "BETTER",
                    > 0.0001 => "WORSE",
                    _ => "SAME"
                };

                double speedup = lz4Speed / k4osSpeed;

                Console.WriteLine($"| {size,-6} | {level,-5} | {bench.LZ4SharpRatio,10:P2} | {bench.K4osRatio,10:P2} | {ratioDelta,-8} | {lz4Speed,10:F1} | {k4osSpeed,10:F1} | {speedup,8:F2}x |");
            }
            Console.WriteLine("------------------------------------------------------------------------------------------------------------------");
        }
    }

    private static void PrintFrameRatios()
    {
        Console.WriteLine("Running Frame vs Pickler ratio analysis...");
        Console.WriteLine("---------------------------------------------------------------");
        Console.WriteLine($"| {"Size",-6} | {"Level",-5} | {"Frame %",-10} | {"Pickler %",-10} | {"Delta",-8} |");
        Console.WriteLine("---------------------------------------------------------------");

        var sizes = new[] { "1kb", "7kb", "16kb", "72kb" };
        var levels = new[] { 3, 6, 9, 12 };

        foreach (var size in sizes)
        {
            foreach (var level in levels)
            {
                var bench = new FramePicklerBenchmarks { JsonType = size, CompressionLevel = level };

                // Setup (silence output)
                var originalOut = Console.Out;
                Console.SetOut(System.IO.TextWriter.Null);
                try { bench.Setup(); } finally { Console.SetOut(originalOut); }

                string delta = (bench.FrameRatio - bench.PicklerRatio) switch
                {
                    < -0.0001 => "BETTER",
                    > 0.0001 => "WORSE",
                    _ => "SAME"
                };

                Console.WriteLine($"| {size,-6} | {level,-5} | {bench.FrameRatio,10:P2} | {bench.PicklerRatio,10:P2} | {delta,-8} |");
            }
            Console.WriteLine("---------------------------------------------------------------");
        }
    }

    private static void PrintJsonDatasetStats()
    {
        static void PrintGroup(string name, JsonDatasetCorpus.Group group)
        {
            var items = JsonDatasetCorpus.Get(group);
            var sizes = items.Select(p => p.Size).ToArray();

            Console.WriteLine($"{name,-6} count={sizes.Length,4} min={sizes.Min(),8} max={sizes.Max(),8} avg={(int)sizes.Average(),8}");
        }

        Console.WriteLine("JSON Dataset Corpus Stats (bytes)");
        PrintGroup("<10kb", JsonDatasetCorpus.Group.Lt10Kb);
        PrintGroup("<100kb", JsonDatasetCorpus.Group.Lt100Kb);
        PrintGroup("<1mb", JsonDatasetCorpus.Group.Lt1Mb);
        PrintGroup(">1mb", JsonDatasetCorpus.Group.Gt1Mb);

        int total = JsonDatasetCorpus.Get(JsonDatasetCorpus.Group.Lt10Kb).Count
            + JsonDatasetCorpus.Get(JsonDatasetCorpus.Group.Lt100Kb).Count
            + JsonDatasetCorpus.Get(JsonDatasetCorpus.Group.Lt1Mb).Count
            + JsonDatasetCorpus.Get(JsonDatasetCorpus.Group.Gt1Mb).Count;

        Console.WriteLine($"Total: {total}");
    }

    private static void RunJsonDatasetSmoke()
    {
        var b = new JsonDatasetBenchmarks
        {
            PayloadGroup = "<10kb",
            CompressionLevel = 3,
        };

        b.Setup();

        Console.WriteLine("JsonDataset smoke run (<10kb, level=3)");
        Console.WriteLine($"LZ4Sharp compress total: {b.CompressAllLZ4Sharp()}");
        Console.WriteLine($"K4os    compress total: {b.CompressAllK4os()}");
        Console.WriteLine($"LZ4Sharp decompress total: {b.DecompressAllLZ4Sharp()}");
        Console.WriteLine($"K4os    decompress total: {b.DecompressAllK4os()}");
    }

    private static double MeasureMBs(Func<long> action, long totalBytesPerOp)
    {
        // Warmup
        action();

        // Measure
        var sw = Stopwatch.StartNew();
        int iterations = 0;
        while (sw.ElapsedMilliseconds < 200) // 200ms per test is enough for estimation
        {
            action();
            iterations++;
        }
        sw.Stop();

        double totalSeconds = sw.Elapsed.TotalSeconds;
        double totalBytes = iterations * (double)totalBytesPerOp;
        return (totalBytes / 1024.0 / 1024.0) / totalSeconds;
    }
}
