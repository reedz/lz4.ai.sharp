using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LZ4Sharp;

namespace LZ4Sharp.HttpBenchmarks;

/// <summary>
/// HTTP benchmark client that sends compressed JSON payloads to the server
/// </summary>
public class BenchmarkClient
{
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    private static readonly Random _random = new Random(42);
    private static readonly DateTime BaseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static async Task RunBenchmarks(string serverUrl, bool useLz4Sharp, int requestCount = 10000)
    {
        Console.WriteLine($"=== HTTP Decompression Benchmark ===");
        Console.WriteLine($"Server: {serverUrl}");
        Console.WriteLine($"Compression: {(useLz4Sharp ? "LZ4Sharp" : "K4os")}");
        Console.WriteLine($"Requests per size: {requestCount}");
        Console.WriteLine();

        // Wait for server to be ready
        await WaitForServer(serverUrl);

        // Test different payload sizes
        var sizes = new[] { 1024, 7 * 1024, 16 * 1024, 72 * 1024, 277 * 1024, 600 * 1024 };
        var sizeNames = new[] { "1KB", "7KB", "16KB", "72KB", "277KB", "600KB" };

        Console.WriteLine($"{"Size",-10} {"Requests",-10} {"Total Time",-15} {"Avg Decomp (μs)",-18} {"Throughput (req/s)",-20} {"MB/s",-10}");
        Console.WriteLine(new string('-', 90));

        for (int i = 0; i < sizes.Length; i++)
        {
            await RunSizeTest(serverUrl, useLz4Sharp, sizes[i], sizeNames[i], requestCount);
        }
    }

    private static async Task WaitForServer(string serverUrl)
    {
        Console.Write("Waiting for server...");
        for (int i = 0; i < 30; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{serverUrl}/health");
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine(" Ready!");
                    return;
                }
            }
            catch { }
            await Task.Delay(500);
            Console.Write(".");
        }
        throw new Exception("Server not available");
    }

    private static async Task RunSizeTest(string serverUrl, bool useLz4Sharp, int targetSize, string sizeName, int requestCount)
    {
        // Generate unique payloads with different internal values
        var payloads = GeneratePayloads(targetSize, requestCount);
        
        // Compress all payloads upfront
        var compressedPayloads = new List<(byte[] compressed, int originalSize)>();
        foreach (var payload in payloads)
        {
            var compressed = CompressPayload(payload, useLz4Sharp);
            compressedPayloads.Add((compressed, payload.Length));
        }

        // Warmup
        for (int i = 0; i < Math.Min(100, requestCount / 10); i++)
        {
            var (compressed, originalSize) = compressedPayloads[i % compressedPayloads.Count];
            await SendRequest(serverUrl, compressed, originalSize);
        }

        // Benchmark
        var decompressTimes = new List<double>();
        var stopwatch = Stopwatch.StartNew();
        long totalBytes = 0;

        for (int i = 0; i < requestCount; i++)
        {
            var (compressed, originalSize) = compressedPayloads[i % compressedPayloads.Count];
            var decompressTime = await SendRequest(serverUrl, compressed, originalSize);
            decompressTimes.Add(decompressTime);
            totalBytes += originalSize;
        }

        stopwatch.Stop();

        var avgDecompressUs = decompressTimes.Average();
        var throughput = requestCount / stopwatch.Elapsed.TotalSeconds;
        var mbPerSec = (totalBytes / 1024.0 / 1024.0) / stopwatch.Elapsed.TotalSeconds;

        Console.WriteLine($"{sizeName,-10} {requestCount,-10} {stopwatch.Elapsed.TotalSeconds:F2}s{"",-10} {avgDecompressUs,-18:F2} {throughput,-20:F0} {mbPerSec,-10:F2}");
    }

    private static List<byte[]> GeneratePayloads(int targetSize, int count)
    {
        var payloads = new List<byte[]>();
        // Generate a reasonable number of unique payloads
        int uniqueCount = Math.Min(count, 100);
        
        for (int i = 0; i < uniqueCount; i++)
        {
            payloads.Add(GenerateJsonPayload(targetSize, i));
        }
        
        return payloads;
    }

    private static byte[] GenerateJsonPayload(int targetSize, int seed)
    {
        var random = new Random(42 + seed);
        var sb = new StringBuilder();
        sb.Append('[');
        
        int itemIndex = 0;
        while (sb.Length < targetSize - 200)
        {
            if (itemIndex > 0) sb.Append(',');
            
            var item = new
            {
                id = Guid.NewGuid().ToString(),
                index = itemIndex,
                seed = seed,
                timestamp = BaseDate.AddSeconds(random.Next(0, 31536000)).ToString("o"),
                name = $"User_{random.Next(10000, 99999)}_{itemIndex}",
                email = $"user{random.Next(1000, 9999)}@domain{random.Next(1, 100)}.com",
                active = random.Next(2) == 1,
                score = random.NextDouble() * 1000,
                tags = new[] { 
                    $"tag{random.Next(1, 50)}", 
                    $"tag{random.Next(50, 100)}", 
                    $"tag{random.Next(100, 150)}" 
                },
                metadata = new
                {
                    version = $"{random.Next(1, 10)}.{random.Next(0, 20)}.{random.Next(0, 100)}",
                    region = new[] { "us-east", "us-west", "eu-west", "ap-south" }[random.Next(4)],
                    tier = new[] { "free", "basic", "pro", "enterprise" }[random.Next(4)]
                }
            };
            
            sb.Append(JsonSerializer.Serialize(item));
            itemIndex++;
        }
        
        sb.Append(']');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] CompressPayload(byte[] payload, bool useLz4Sharp)
    {
        if (useLz4Sharp)
        {
            var maxSize = LZ4Codec.CompressBound(payload.Length);
            var compressed = new byte[maxSize];
            var compressedSize = LZ4Codec.CompressDefault(payload, compressed, payload.Length, maxSize);
            return compressed.AsSpan(0, compressedSize).ToArray();
        }
        else
        {
            var maxSize = K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(payload.Length);
            var compressed = new byte[maxSize];
            var compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(
                payload, 0, payload.Length,
                compressed, 0, maxSize,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
            return compressed.AsSpan(0, compressedSize).ToArray();
        }
    }

    private static async Task<double> SendRequest(string serverUrl, byte[] compressed, int originalSize)
    {
        using var content = new ByteArrayContent(compressed);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{serverUrl}/decompress");
        request.Content = content;
        request.Headers.Add("X-Decompressed-Size", originalSize.ToString());

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        if (response.Headers.TryGetValues("X-Decompress-Time-Us", out var values))
        {
            return double.Parse(values.First());
        }
        return 0;
    }
}
