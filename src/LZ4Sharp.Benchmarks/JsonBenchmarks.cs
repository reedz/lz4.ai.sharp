using BenchmarkDotNet.Attributes;
using K4os.Compression.LZ4;
using LZ4Sharp;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Focused JSON benchmarks comparing LZ4Sharp against K4os.Compression.LZ4
    /// Processes 100 unique JSON payloads with different internal values
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 2, iterationCount: 3)]
    public class JsonBenchmarks
    {
        private static readonly DateTime BaseDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const int PayloadCount = 100;

        // Arrays of unique payloads
        private byte[][] _payloads = null!;
        private byte[][] _compressedLZ4Sharp = null!;
        private byte[][] _compressedK4os = null!;
        private int[] _compressedSizesLZ4Sharp = null!;
        private int[] _compressedSizesK4os = null!;

        // Reusable buffers
        private byte[] _compressBuffer = null!;
        private byte[] _decompressBuffer = null!;

        [Params("1kb", "7kb", "16kb", "72kb")]
        public string JsonType { get; set; } = "1kb";

        [Params(3, 6, 9, 12)]
        public int CompressionLevel { get; set; } = LZ4HC.CLEVEL_DEFAULT;

        public double LZ4SharpRatio { get; private set; }
        public double K4osRatio { get; private set; }
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

            Console.WriteLine($"Generating {PayloadCount} unique JSON payloads of ~{JsonType}...");

            _payloads = new byte[PayloadCount][];
            _compressedLZ4Sharp = new byte[PayloadCount][];
            _compressedK4os = new byte[PayloadCount][];
            _compressedSizesLZ4Sharp = new int[PayloadCount];
            _compressedSizesK4os = new int[PayloadCount];

            long totalOriginalSize = 0;
            long totalCompressedLZ4Sharp = 0;
            long totalCompressedK4os = 0;

            for (int i = 0; i < PayloadCount; i++)
            {
                _payloads[i] = GenerateUniqueJsonPayload(targetSize, i);
                totalOriginalSize += _payloads[i].Length;

                // Pre-compress with LZ4Sharp (LZ4HC)
                int maxSize = LZ4HC.CompressBound(_payloads[i].Length);
                _compressedLZ4Sharp[i] = new byte[maxSize];
                _compressedSizesLZ4Sharp[i] = LZ4HC.CompressHC(
                    _payloads[i], _compressedLZ4Sharp[i], _payloads[i].Length, maxSize, CompressionLevel);
                totalCompressedLZ4Sharp += _compressedSizesLZ4Sharp[i];

                // Pre-compress with K4os (same nominal level)
                _compressedK4os[i] = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(_payloads[i].Length)];
                _compressedSizesK4os[i] = K4os.Compression.LZ4.LZ4Codec.Encode(
                    _payloads[i], 0, _payloads[i].Length,
                    _compressedK4os[i], 0, _compressedK4os[i].Length,
                    (LZ4Level)CompressionLevel);
                totalCompressedK4os += _compressedSizesK4os[i];
            }

            // Allocate reusable buffers based on max size
            int maxPayloadSize = _payloads.Max(p => p.Length);
            int maxCompressedSize = Math.Max(
                LZ4Codec.CompressBound(maxPayloadSize),
                K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(maxPayloadSize));
            _compressBuffer = new byte[maxCompressedSize];
            _decompressBuffer = new byte[maxPayloadSize];

            double avgSize = totalOriginalSize / (double)PayloadCount;
            LZ4SharpRatio = totalCompressedLZ4Sharp / (double)totalOriginalSize;
            K4osRatio = totalCompressedK4os / (double)totalOriginalSize;
            TotalOriginalSize = totalOriginalSize;

            Console.WriteLine($"Generated {PayloadCount} payloads, avg size: {avgSize:F0} bytes");
            Console.WriteLine($"Compression ratios: LZ4Sharp={LZ4SharpRatio:P1}, K4os={K4osRatio:P1}");
        }

        [Benchmark(Description = "LZ4Sharp (LZ4HC) - Compress 100")]
        public long CompressAllLZ4Sharp()
        {
            long totalCompressed = 0;
            for (int i = 0; i < PayloadCount; i++)
            {
                var payload = _payloads[i];
                totalCompressed += LZ4HC.CompressHC(payload, _compressBuffer, payload.Length, _compressBuffer.Length, CompressionLevel);
            }
            return totalCompressed;
        }

        [Benchmark(Description = "K4os.LZ4 - Compress 100", Baseline = true)]
        public long CompressAllK4os()
        {
            long totalCompressed = 0;
            for (int i = 0; i < PayloadCount; i++)
            {
                var payload = _payloads[i];
                totalCompressed += K4os.Compression.LZ4.LZ4Codec.Encode(
                    payload, 0, payload.Length,
                    _compressBuffer, 0, _compressBuffer.Length,
                    (LZ4Level)CompressionLevel);
            }
            return totalCompressed;
        }

        [Benchmark(Description = "LZ4Sharp - Decompress 100")]
        public long DecompressAllLZ4Sharp()
        {
            long totalDecompressed = 0;
            for (int i = 0; i < PayloadCount; i++)
            {
                var compressed = _compressedLZ4Sharp[i];
                var compressedSize = _compressedSizesLZ4Sharp[i];
                var originalSize = _payloads[i].Length;
                totalDecompressed += LZ4Codec.DecompressSafe(compressed, _decompressBuffer, compressedSize, originalSize);
            }
            return totalDecompressed;
        }

        [Benchmark(Description = "K4os.LZ4 - Decompress 100")]
        public long DecompressAllK4os()
        {
            long totalDecompressed = 0;
            for (int i = 0; i < PayloadCount; i++)
            {
                var compressed = _compressedK4os[i];
                var compressedSize = _compressedSizesK4os[i];
                var originalSize = _payloads[i].Length;
                totalDecompressed += K4os.Compression.LZ4.LZ4Codec.Decode(
                    compressed, 0, compressedSize,
                    _decompressBuffer, 0, originalSize);
            }
            return totalDecompressed;
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
}
