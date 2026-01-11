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

        if (args.Contains("--silesia"))
        {
            SilesiaAnalysis.Run();
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
