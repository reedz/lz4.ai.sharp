using BenchmarkDotNet.Attributes;
using K4os.Compression.LZ4;
using LZ4Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;

namespace LZ4Sharp.Benchmarks;

/// <summary>
/// BenchmarkDotNet version of the Silesia corpus benchmarks.
/// Downloads/extracts the corpus once into LocalApplicationData and benchmarks per-file compression.
/// </summary>
[MemoryDiagnoser]
public class SilesiaCodecBenchmarks
{
    private static readonly string[] CorpusNames =
    {
        "dickens", "mozilla", "mr", "nci", "ooffice", "osdb", "reymont", "samba", "sao", "webster", "xml", "x-ray"
    };

    private const string CorpusZipUrl = "https://sun.aei.polsl.pl/~sdeor/corpus/silesia.zip";

    private static IReadOnlyDictionary<string, byte[]> s_corpus = null!;

    internal static IReadOnlyDictionary<string, byte[]> Corpus => s_corpus ??= LoadCorpus();

    private byte[] _input = null!;
    private byte[] _dest = null!;

    [ParamsSource(nameof(Files))]
    public string FileName { get; set; } = "dickens";

    // 0 == fastest (LZ4Codec.CompressFast / K4os L00_FAST)
    [Params(0, 3, 6, 9, 12)]
    public int Level { get; set; }

    public static IEnumerable<string> Files => CorpusNames;

    [GlobalSetup]
    public void Setup()
    {
        _input = Corpus[FileName];

        int maxOut = Math.Max(
            LZ4Codec.CompressBound(_input.Length),
            K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(_input.Length));
        _dest = new byte[maxOut];
    }

    [Benchmark(Description = "LZ4Sharp Compress")]
    public int LZ4SharpCompress()
    {
        if (Level == 0)
            return LZ4Codec.CompressFast(_input, _dest, _input.Length, _dest.Length);

        return LZ4HC.CompressHC(_input, _dest, _input.Length, _dest.Length, Level);
    }

    [Benchmark(Description = "K4os.LZ4Codec Compress", Baseline = true)]
    public int K4osCompress()
    {
        var k4osLevel = Level == 0 ? LZ4Level.L00_FAST : (LZ4Level)Level;
        return K4os.Compression.LZ4.LZ4Codec.Encode(_input, 0, _input.Length, _dest, 0, _dest.Length, k4osLevel);
    }

    internal static IReadOnlyDictionary<string, byte[]> LoadCorpus()
    {
        string corpusDir = EnsureCorpus();
        return CorpusNames.ToDictionary(n => n, n => System.IO.File.ReadAllBytes(Path.Combine(corpusDir, n)), StringComparer.OrdinalIgnoreCase);
    }

    private static string EnsureCorpus()
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LZ4Sharp.Benchmarks",
            "silesia");

        string corpusDir = Path.Combine(baseDir, "corpus");
        Directory.CreateDirectory(baseDir);

        if (Directory.Exists(corpusDir) && CorpusNames.All(n => System.IO.File.Exists(Path.Combine(corpusDir, n))))
            return corpusDir;

        string zipPath = Path.Combine(baseDir, "silesia.zip");
        if (!System.IO.File.Exists(zipPath))
            DownloadFile(CorpusZipUrl, zipPath);

        if (Directory.Exists(corpusDir))
            Directory.Delete(corpusDir, recursive: true);
        Directory.CreateDirectory(corpusDir);

        ZipFile.ExtractToDirectory(zipPath, corpusDir);

        // Some zips contain a nested directory; flatten if needed.
        string? nested = Directory.EnumerateDirectories(corpusDir).FirstOrDefault();
        if (nested != null && CorpusNames.All(n => System.IO.File.Exists(Path.Combine(nested, n))))
        {
            foreach (var name in CorpusNames)
                System.IO.File.Copy(Path.Combine(nested, name), Path.Combine(corpusDir, name), overwrite: true);
        }

        foreach (var name in CorpusNames)
        {
            if (!System.IO.File.Exists(Path.Combine(corpusDir, name)))
                throw new FileNotFoundException($"Extracted corpus is missing '{name}'.");
        }

        return corpusDir;
    }

    private static void DownloadFile(string url, string destinationPath)
    {
        using var http = new HttpClient();
        using var response = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        using var src = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var dst = System.IO.File.Create(destinationPath);
        src.CopyTo(dst);
    }
}

[MemoryDiagnoser]
public class SilesiaFramePicklerBenchmarks
{
    private static IReadOnlyDictionary<string, byte[]> s_corpus = null!;

    private byte[] _input = null!;
    private byte[] _frameDest = null!;
    private LZ4Frame.FramePreferences _prefs = null!;

    public static IEnumerable<string> Files => SilesiaCodecBenchmarks.Files;

    [ParamsSource(nameof(Files))]
    public string FileName { get; set; } = "dickens";

    // 0 == fastest (LZ4Codec.CompressFast / K4os L00_FAST)
    [Params(0, 3, 6, 9, 12)]
    public int Level { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        s_corpus ??= SilesiaCodecBenchmarks.Corpus;
        _input = s_corpus[FileName];

        _prefs = new LZ4Frame.FramePreferences
        {
            BlockMode = LZ4Frame.BlockMode.Independent,
            CompressionLevel = Level == 0 ? 0 : Level
        };
        _frameDest = new byte[LZ4Frame.CompressFrameBound(_input.Length, _prefs)];
    }

    [Benchmark(Description = "LZ4Frame Compress")]
    public int FrameCompress()
    {
        return LZ4Frame.CompressFrame(_frameDest, _frameDest.Length, _input, _input.Length, _prefs);
    }

    [Benchmark(Description = "K4os.LZ4Pickler Pickle", Baseline = true)]
    public int PicklerCompress()
    {
        var k4osLevel = Level == 0 ? LZ4Level.L00_FAST : (LZ4Level)Level;
        return LZ4Pickler.Pickle(_input, k4osLevel).Length;
    }
}
