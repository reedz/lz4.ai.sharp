using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Configs;
using System.Text;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Profiling benchmarks specifically for log compression scenarios
    /// Simulates realistic application log data with various patterns
    /// Includes detailed diagnostics for performance analysis
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 5)]
    public class LogCompressionProfilingBenchmarks
    {
        private byte[] _structuredLogData = null!;
        private byte[] _unstructuredLogData = null!;
        private byte[] _mixedLogData = null!;
        private byte[] _jsonLogData = null!;
        
        private byte[] _compressedStructured = null!;
        private byte[] _compressedUnstructured = null!;
        private byte[] _compressedMixed = null!;
        private byte[] _compressedJson = null!;
        
        private int _compressedStructuredSize;
        private int _compressedUnstructuredSize;
        private int _compressedMixedSize;
        private int _compressedJsonSize;

        [Params(10 * 1024, 100 * 1024)]
        public int LogDataSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            // Generate different types of log data
            _structuredLogData = GenerateStructuredLogs(LogDataSize);
            _unstructuredLogData = GenerateUnstructuredLogs(LogDataSize);
            _mixedLogData = GenerateMixedLogs(LogDataSize);
            _jsonLogData = GenerateJsonLogs(LogDataSize);

            // Pre-compress data for decompression benchmarks
            int maxCompressedSize = LZ4Codec.CompressBound(LogDataSize);
            
            _compressedStructured = new byte[maxCompressedSize];
            _compressedStructuredSize = LZ4Codec.CompressDefault(_structuredLogData, _compressedStructured, LogDataSize, maxCompressedSize);
            
            _compressedUnstructured = new byte[maxCompressedSize];
            _compressedUnstructuredSize = LZ4Codec.CompressDefault(_unstructuredLogData, _compressedUnstructured, LogDataSize, maxCompressedSize);
            
            _compressedMixed = new byte[maxCompressedSize];
            _compressedMixedSize = LZ4Codec.CompressDefault(_mixedLogData, _compressedMixed, LogDataSize, maxCompressedSize);
            
            _compressedJson = new byte[maxCompressedSize];
            _compressedJsonSize = LZ4Codec.CompressDefault(_jsonLogData, _compressedJson, LogDataSize, maxCompressedSize);
        }

        #region Compression Benchmarks

        [Benchmark(Description = "Compress - Structured Logs")]
        public int CompressStructuredLogs()
        {
            var dest = new byte[_compressedStructured.Length];
            return LZ4Codec.CompressDefault(_structuredLogData, dest, _structuredLogData.Length, dest.Length);
        }

        [Benchmark(Description = "Compress - Unstructured Logs")]
        public int CompressUnstructuredLogs()
        {
            var dest = new byte[_compressedUnstructured.Length];
            return LZ4Codec.CompressDefault(_unstructuredLogData, dest, _unstructuredLogData.Length, dest.Length);
        }

        [Benchmark(Description = "Compress - Mixed Logs")]
        public int CompressMixedLogs()
        {
            var dest = new byte[_compressedMixed.Length];
            return LZ4Codec.CompressDefault(_mixedLogData, dest, _mixedLogData.Length, dest.Length);
        }

        [Benchmark(Description = "Compress - JSON Logs")]
        public int CompressJsonLogs()
        {
            var dest = new byte[_compressedJson.Length];
            return LZ4Codec.CompressDefault(_jsonLogData, dest, _jsonLogData.Length, dest.Length);
        }

        #endregion

        #region Decompression Benchmarks

        [Benchmark(Description = "Decompress - Structured Logs")]
        public int DecompressStructuredLogs()
        {
            var dest = new byte[_structuredLogData.Length];
            return LZ4Codec.DecompressSafe(_compressedStructured, dest, _compressedStructuredSize, _structuredLogData.Length);
        }

        [Benchmark(Description = "Decompress - Unstructured Logs")]
        public int DecompressUnstructuredLogs()
        {
            var dest = new byte[_unstructuredLogData.Length];
            return LZ4Codec.DecompressSafe(_compressedUnstructured, dest, _compressedUnstructuredSize, _unstructuredLogData.Length);
        }

        [Benchmark(Description = "Decompress - Mixed Logs")]
        public int DecompressMixedLogs()
        {
            var dest = new byte[_mixedLogData.Length];
            return LZ4Codec.DecompressSafe(_compressedMixed, dest, _compressedMixedSize, _mixedLogData.Length);
        }

        [Benchmark(Description = "Decompress - JSON Logs")]
        public int DecompressJsonLogs()
        {
            var dest = new byte[_jsonLogData.Length];
            return LZ4Codec.DecompressSafe(_compressedJson, dest, _compressedJsonSize, _jsonLogData.Length);
        }

        #endregion

        #region Log Data Generators

        private static byte[] GenerateStructuredLogs(int size)
        {
            // Structured logs with timestamp, level, component, message pattern
            var sb = new StringBuilder(size);
            var random = new Random(42);
            var levels = new[] { "INFO", "WARN", "ERROR", "DEBUG", "TRACE" };
            var components = new[] { "Authentication", "Database", "API", "Cache", "Worker", "Scheduler" };
            var messages = new[]
            {
                "Request processed successfully",
                "Connection established",
                "Query executed in {0}ms",
                "Cache hit for key {0}",
                "User {0} logged in",
                "Transaction completed",
                "Background job started",
                "Health check passed"
            };

            int lineCount = 0;
            while (sb.Length < size)
            {
                var timestamp = DateTime.UtcNow.AddSeconds(-random.Next(3600));
                var level = levels[random.Next(levels.Length)];
                var component = components[random.Next(components.Length)];
                var message = string.Format(messages[random.Next(messages.Length)], 
                    random.Next(1000));
                
                sb.AppendLine($"{timestamp:yyyy-MM-dd HH:mm:ss.fff} [{level}] {component}: {message}");
                lineCount++;
            }

            if (sb.Length > size)
                sb.Length = size;

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static byte[] GenerateUnstructuredLogs(int size)
        {
            // Unstructured logs with varying formats and content
            var sb = new StringBuilder(size);
            var random = new Random(42);
            var templates = new[]
            {
                "Error occurred while processing request: {0}",
                ">>> Debug: variable x = {0}, y = {1}",
                "WARNING! System resource usage at {0}%",
                "Success - operation took {0}s",
                "[{0}] Exception in thread 'main': NullPointerException at line {1}",
                "-- SQL Query --\nSELECT * FROM users WHERE id = {0}\n-- End Query --",
                "HTTP GET /api/users/{0} - Status: 200 OK - {1}ms"
            };

            while (sb.Length < size)
            {
                var template = templates[random.Next(templates.Length)];
                var line = string.Format(template, random.Next(1000), random.Next(100));
                sb.AppendLine(line);
            }

            if (sb.Length > size)
                sb.Length = size;

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static byte[] GenerateMixedLogs(int size)
        {
            // Mix of structured and unstructured logs (realistic scenario)
            var sb = new StringBuilder(size);
            var random = new Random(42);

            while (sb.Length < size)
            {
                // 70% structured, 30% unstructured
                if (random.Next(100) < 70)
                {
                    var timestamp = DateTime.UtcNow.AddSeconds(-random.Next(3600));
                    sb.AppendLine($"{timestamp:yyyy-MM-dd HH:mm:ss} [INFO] Request ID: {Guid.NewGuid()} - Processing");
                }
                else
                {
                    sb.AppendLine($"*** ALERT: Threshold exceeded {random.Next(100)}% ***");
                }
            }

            if (sb.Length > size)
                sb.Length = size;

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static byte[] GenerateJsonLogs(int size)
        {
            // JSON-formatted logs (common in modern applications)
            var sb = new StringBuilder(size);
            var random = new Random(42);
            var levels = new[] { "info", "warn", "error", "debug" };
            var services = new[] { "api-gateway", "user-service", "payment-service", "notification-service" };

            while (sb.Length < size)
            {
                var timestamp = DateTimeOffset.UtcNow.AddSeconds(-random.Next(3600)).ToUnixTimeMilliseconds();
                var level = levels[random.Next(levels.Length)];
                var service = services[random.Next(services.Length)];
                var duration = random.Next(1000);
                var statusCode = random.Next(100) < 90 ? 200 : 500;

                sb.AppendLine($"{{\"timestamp\":{timestamp},\"level\":\"{level}\",\"service\":\"{service}\",\"duration\":{duration},\"status\":{statusCode},\"message\":\"Request processed\",\"traceId\":\"{Guid.NewGuid()}\"}}");
            }

            if (sb.Length > size)
                sb.Length = size;

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        #endregion
    }
}
