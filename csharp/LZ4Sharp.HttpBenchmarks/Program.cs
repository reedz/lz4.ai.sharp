using LZ4Sharp.HttpBenchmarks;

// Check for --client mode
if (args.Contains("--client"))
{
    var serverUrl = "http://localhost:5555";
    var useLz4Sharp = !args.Contains("--k4os");
    var requestCount = 10000;
    
    // Parse request count if provided
    var countArg = args.FirstOrDefault(a => a.StartsWith("--count="));
    if (countArg != null && int.TryParse(countArg.Split('=')[1], out var count))
    {
        requestCount = count;
    }
    
    await BenchmarkClient.RunBenchmarks(serverUrl, useLz4Sharp, requestCount);
}
else
{
    // Run as server (default)
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

    var app = builder.Build();

    // Determine which LZ4 implementation to use
    var useLz4Sharp = !args.Contains("--k4os");
    Console.WriteLine($"Using LZ4 implementation: {(useLz4Sharp ? "LZ4Sharp" : "K4os")}");

    // Pre-allocate buffers for decompression (thread-local)
    var decompressBuffer = new ThreadLocal<byte[]>(() => new byte[1024 * 1024]);

    app.MapPost("/decompress", async (HttpContext context) =>
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        using var ms = new MemoryStream();
        await context.Request.Body.CopyToAsync(ms);
        var compressed = ms.ToArray();
        
        if (!context.Request.Headers.TryGetValue("X-Decompressed-Size", out var sizeHeader) ||
            !int.TryParse(sizeHeader, out var expectedSize))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Missing X-Decompressed-Size header");
            return;
        }
        
        var buffer = decompressBuffer.Value!;
        if (buffer.Length < expectedSize)
        {
            buffer = new byte[expectedSize];
            decompressBuffer.Value = buffer;
        }
        
        int decompressedSize;
        if (useLz4Sharp)
        {
            decompressedSize = LZ4Sharp.LZ4Codec.DecompressSafe(compressed, buffer, compressed.Length, expectedSize);
        }
        else
        {
            decompressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(
                compressed, 0, compressed.Length,
                buffer, 0, expectedSize);
        }
        
        stopwatch.Stop();
        
        context.Response.Headers["X-Decompress-Time-Us"] = stopwatch.Elapsed.TotalMicroseconds.ToString("F2");
        context.Response.Headers["X-Decompressed-Size"] = decompressedSize.ToString();
        await context.Response.WriteAsync("OK");
    });

    app.MapGet("/health", () => "OK");

    Console.WriteLine("Server starting on http://localhost:5555");
    Console.WriteLine("Run client with: dotnet run -- --client");
    Console.WriteLine("Options: --k4os (use K4os instead of LZ4Sharp), --count=N (number of requests)");
    app.Run("http://localhost:5555");
}
