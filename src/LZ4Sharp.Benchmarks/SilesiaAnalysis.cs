using K4os.Compression.LZ4;
using LZ4Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace LZ4Sharp.Benchmarks;

internal static class SilesiaAnalysis
{
    // Canonical corpus file names (the zip sometimes includes them directly at root).
    private static readonly string[] CorpusNames =
    {
        "dickens", "mozilla", "mr", "nci", "ooffice", "osdb", "reymont", "samba", "sao", "webster", "xml", "x-ray"
    };

    private const string CorpusZipUrl = "https://sun.aei.polsl.pl/~sdeor/corpus/silesia.zip";

    public static void Run()
    {
        string corpusDir = EnsureCorpus();

        var cases = new[]
        {
            new Case("fastest", null, LZ4Level.L00_FAST),
            new Case("3", 3, (LZ4Level)3),
            new Case("6", 6, (LZ4Level)6),
            new Case("9", 9, (LZ4Level)9),
            new Case("12", 12, (LZ4Level)12),
        };

        Console.WriteLine("Silesia corpus: " + corpusDir);
        Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------------------------------------------------");
        Console.WriteLine($"| {"File",-10} | {"Size",10} | {"Level",-7} | {"LZ4Sharp %",-10} | {"K4os %",-10} | {"LZ4Sharp",-10} | {"K4os",-10} | {"Speedup",-8} |");
        Console.WriteLine($"| {"",-10} | {"(bytes)",10} | {"",-7} | {"",-10} | {"",-10} | {"(MB/s)",-10} | {"(MB/s)",-10} | {"",-8} |");
        Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------------------------------------------------");

        foreach (var file in LoadCorpusFiles(corpusDir))
        {
            foreach (var c in cases)
            {
                var metrics = Measure(file.Data, c);
                Console.WriteLine($"| {file.Name,-10} | {file.Data.Length,10:N0} | {c.Name,-7} | {metrics.LZ4SharpRatio,10:P2} | {metrics.K4osRatio,10:P2} | {metrics.LZ4SharpMBs,10:F1} | {metrics.K4osMBs,10:F1} | {(metrics.LZ4SharpMBs / metrics.K4osMBs),8:F2}x |");
            }
            Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------------------------------------------------");
        }

        Console.WriteLine();
        Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------------------------------------------------");
        Console.WriteLine($"| {"File",-10} | {"Size",10} | {"Level",-7} | {"Frame %",-10} | {"Pickler %",-10} | {"Frame",-10} | {"Pickler",-10} | {"Speedup",-8} |");
        Console.WriteLine($"| {"",-10} | {"(bytes)",10} | {"",-7} | {"",-10} | {"",-10} | {"(MB/s)",-10} | {"(MB/s)",-10} | {"",-8} |");
        Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------------------------------------------------");

        foreach (var file in LoadCorpusFiles(corpusDir))
        {
            foreach (var c in cases)
            {
                var metrics = MeasureFramePickler(file.Data, c);
                Console.WriteLine($"| {file.Name,-10} | {file.Data.Length,10:N0} | {c.Name,-7} | {metrics.FrameRatio,10:P2} | {metrics.PicklerRatio,10:P2} | {metrics.FrameMBs,10:F1} | {metrics.PicklerMBs,10:F1} | {(metrics.FrameMBs / metrics.PicklerMBs),8:F2}x |");
            }
            Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------------------------------------------------");
        }
    }

    private static IEnumerable<(string Name, byte[] Data)> LoadCorpusFiles(string corpusDir)
    {
        var files = Directory.EnumerateFiles(corpusDir)
            .Select(p => new FileInfo(p))
            .ToDictionary(f => f.Name, f => f.FullName, StringComparer.OrdinalIgnoreCase);

        foreach (var name in CorpusNames)
        {
            if (!files.TryGetValue(name, out var path))
                throw new FileNotFoundException($"Silesia corpus file '{name}' not found in {corpusDir}");

            yield return (name, File.ReadAllBytes(path));
        }
    }

    private static (double LZ4SharpRatio, double K4osRatio, double LZ4SharpMBs, double K4osMBs) Measure(byte[] input, Case c)
    {
        int maxOutLZ4Sharp = LZ4Codec.CompressBound(input.Length);
        int maxOutK4os = K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(input.Length);
        int maxOut = Math.Max(maxOutLZ4Sharp, maxOutK4os);
        byte[] dest = new byte[maxOut];

        // Pre-compress once to compute ratios.
        int lz4SharpSize = CompressLZ4Sharp(input, dest, c);
        int k4osSize = CompressK4os(input, dest, c);

        double lz4SharpRatio = lz4SharpSize / (double)input.Length;
        double k4osRatio = k4osSize / (double)input.Length;

        // Throughput.
        double lz4SharpMBs = MeasureMBs(() => CompressLZ4Sharp(input, dest, c), input.Length);
        double k4osMBs = MeasureMBs(() => CompressK4os(input, dest, c), input.Length);

        return (lz4SharpRatio, k4osRatio, lz4SharpMBs, k4osMBs);
    }

    private static int CompressLZ4Sharp(byte[] input, byte[] dest, Case c)
    {
        if (c.LZ4SharpLevel is null)
            return LZ4Codec.CompressDefault(input, dest, input.Length, dest.Length);

        return LZ4HC.CompressHC(input, dest, input.Length, dest.Length, c.LZ4SharpLevel.Value);
    }

    private static int CompressK4os(byte[] input, byte[] dest, Case c)
    {
        return K4os.Compression.LZ4.LZ4Codec.Encode(
            input, 0, input.Length,
            dest, 0, dest.Length,
            c.K4osLevel);
    }

    private static (double FrameRatio, double PicklerRatio, double FrameMBs, double PicklerMBs) MeasureFramePickler(byte[] input, Case c)
    {
        var prefs = new LZ4Frame.FramePreferences
        {
            BlockMode = LZ4Frame.BlockMode.Independent,
            CompressionLevel = c.LZ4SharpLevel ?? 0
        };

        byte[] frameDest = new byte[LZ4Frame.CompressFrameBound(input.Length, prefs)];

        // Pre-compress once to compute ratios.
        int frameSize = LZ4Frame.CompressFrame(frameDest, frameDest.Length, input, input.Length, prefs);
        if (frameSize <= 0) throw new InvalidOperationException("LZ4Frame.CompressFrame failed");

        int pickleSize = LZ4Pickler.Pickle(input, c.K4osLevel).Length;

        double frameRatio = frameSize / (double)input.Length;
        double pickleRatio = pickleSize / (double)input.Length;

        // Throughput.
        double frameMBs = MeasureMBs(() => LZ4Frame.CompressFrame(frameDest, frameDest.Length, input, input.Length, prefs), input.Length);
        double picklerMBs = MeasureMBs(() => LZ4Pickler.Pickle(input, c.K4osLevel).Length, input.Length);

        return (frameRatio, pickleRatio, frameMBs, picklerMBs);
    }

    private static double MeasureMBs(Func<int> action, int bytesPerOp)
    {
        // Warmup
        action();

        var sw = Stopwatch.StartNew();
        int iterations = 0;
        while (sw.ElapsedMilliseconds < 250)
        {
            action();
            iterations++;
        }
        sw.Stop();

        double totalSeconds = sw.Elapsed.TotalSeconds;
        double totalBytes = iterations * (double)bytesPerOp;
        return (totalBytes / 1024.0 / 1024.0) / totalSeconds;
    }

    private static string EnsureCorpus()
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LZ4Sharp.Benchmarks",
            "silesia");

        // Use a separate directory to avoid mixing with other artifacts.
        string corpusDir = Path.Combine(baseDir, "corpus");
        Directory.CreateDirectory(baseDir);

        if (Directory.Exists(corpusDir) && CorpusNames.All(n => File.Exists(Path.Combine(corpusDir, n))))
            return corpusDir;

        string zipPath = Path.Combine(baseDir, "silesia.zip");

        if (!File.Exists(zipPath))
        {
            Console.WriteLine($"Downloading Silesia corpus from {CorpusZipUrl} ...");
            DownloadFile(CorpusZipUrl, zipPath);
        }

        if (Directory.Exists(corpusDir))
            Directory.Delete(corpusDir, recursive: true);
        Directory.CreateDirectory(corpusDir);

        // Extract zip.
        ZipFile.ExtractToDirectory(zipPath, corpusDir);

        // Some zips contain a nested directory; flatten if needed.
        string? nested = Directory.EnumerateDirectories(corpusDir).FirstOrDefault();
        if (nested != null && CorpusNames.All(n => File.Exists(Path.Combine(nested, n))))
        {
            foreach (var name in CorpusNames)
                File.Copy(Path.Combine(nested, name), Path.Combine(corpusDir, name), overwrite: true);
        }

        // Validate.
        foreach (var name in CorpusNames)
        {
            if (!File.Exists(Path.Combine(corpusDir, name)))
                throw new FileNotFoundException($"Extracted corpus is missing '{name}'.");
        }

        return corpusDir;
    }

    private static void DownloadFile(string url, string destinationPath)
    {
        // Keep it sync to avoid async main / BDN complications.
        using var http = new HttpClient();
        using var response = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        using var src = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var dst = File.Create(destinationPath);
        src.CopyTo(dst);
    }

    private readonly record struct Case(string Name, int? LZ4SharpLevel, LZ4Level K4osLevel);
}
