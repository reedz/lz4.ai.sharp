using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Text;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Detailed profiling benchmarks to identify performance bottlenecks
    /// Focuses on specific operations within compression/decompression
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 5)]
    public class DetailedProfilingBenchmarks
    {
        private byte[] _textData = null!;
        private byte[] _randomData = null!;
        private byte[] _repetitiveData = null!;
        private byte[] _compressedTextData = null!;
        private byte[] _compressedRandomData = null!;
        private byte[] _compressedRepetitiveData = null!;
        private int _compressedTextSize;
        private int _compressedRandomSize;
        private int _compressedRepetitiveSize;

        [GlobalSetup]
        public void Setup()
        {
            const int size = 100 * 1024; // 100KB

            // Generate test data
            _textData = GenerateTextData(size);
            _randomData = GenerateRandomData(size);
            _repetitiveData = GenerateRepetitiveData(size);

            // Pre-compress data
            int maxCompressedSize = LZ4Codec.CompressBound(size);
            
            _compressedTextData = new byte[maxCompressedSize];
            _compressedTextSize = LZ4Codec.CompressDefault(_textData, _compressedTextData, size, maxCompressedSize);
            
            _compressedRandomData = new byte[maxCompressedSize];
            _compressedRandomSize = LZ4Codec.CompressDefault(_randomData, _compressedRandomData, size, maxCompressedSize);
            
            _compressedRepetitiveData = new byte[maxCompressedSize];
            _compressedRepetitiveSize = LZ4Codec.CompressDefault(_repetitiveData, _compressedRepetitiveData, size, maxCompressedSize);
        }

        #region Compression Profiling by Data Pattern

        [Benchmark(Description = "Compress - Text Pattern")]
        public int CompressText()
        {
            var dest = new byte[_compressedTextData.Length];
            return LZ4Codec.CompressDefault(_textData, dest, _textData.Length, dest.Length);
        }

        [Benchmark(Description = "Compress - Random Pattern")]
        public int CompressRandom()
        {
            var dest = new byte[_compressedRandomData.Length];
            return LZ4Codec.CompressDefault(_randomData, dest, _randomData.Length, dest.Length);
        }

        [Benchmark(Description = "Compress - Repetitive Pattern")]
        public int CompressRepetitive()
        {
            var dest = new byte[_compressedRepetitiveData.Length];
            return LZ4Codec.CompressDefault(_repetitiveData, dest, _repetitiveData.Length, dest.Length);
        }

        #endregion

        #region Decompression Profiling by Data Pattern

        [Benchmark(Description = "Decompress - Text Pattern")]
        public int DecompressText()
        {
            var dest = new byte[_textData.Length];
            return LZ4Codec.DecompressSafe(_compressedTextData, dest, _compressedTextSize, _textData.Length);
        }

        [Benchmark(Description = "Decompress - Random Pattern")]
        public int DecompressRandom()
        {
            var dest = new byte[_randomData.Length];
            return LZ4Codec.DecompressSafe(_compressedRandomData, dest, _compressedRandomSize, _randomData.Length);
        }

        [Benchmark(Description = "Decompress - Repetitive Pattern")]
        public int DecompressRepetitive()
        {
            var dest = new byte[_repetitiveData.Length];
            return LZ4Codec.DecompressSafe(_compressedRepetitiveData, dest, _compressedRepetitiveSize, _repetitiveData.Length);
        }

        #endregion

        #region Data Size Scaling

        [Params(1024, 10 * 1024, 100 * 1024)]
        public int DataSize { get; set; }

        private byte[] _scalingData = null!;
        private byte[] _scalingCompressed = null!;
        private int _scalingCompressedSize;

        [IterationSetup(Target = nameof(CompressScaling))]
        public void SetupCompressScaling()
        {
            _scalingData = GenerateTextData(DataSize);
        }

        [Benchmark(Description = "Compress - Scaling Test")]
        public int CompressScaling()
        {
            int maxSize = LZ4Codec.CompressBound(_scalingData.Length);
            var dest = new byte[maxSize];
            return LZ4Codec.CompressDefault(_scalingData, dest, _scalingData.Length, maxSize);
        }

        [IterationSetup(Target = nameof(DecompressScaling))]
        public void SetupDecompressScaling()
        {
            _scalingData = GenerateTextData(DataSize);
            int maxSize = LZ4Codec.CompressBound(_scalingData.Length);
            _scalingCompressed = new byte[maxSize];
            _scalingCompressedSize = LZ4Codec.CompressDefault(_scalingData, _scalingCompressed, _scalingData.Length, maxSize);
        }

        [Benchmark(Description = "Decompress - Scaling Test")]
        public int DecompressScaling()
        {
            var dest = new byte[_scalingData.Length];
            return LZ4Codec.DecompressSafe(_scalingCompressed, dest, _scalingCompressedSize, _scalingData.Length);
        }

        #endregion

        #region Helper Methods

        private static byte[] GenerateTextData(int size)
        {
            const string sampleText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                                     "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. " +
                                     "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris. ";
            
            var sb = new StringBuilder(size);
            while (sb.Length < size)
            {
                sb.Append(sampleText);
            }
            
            if (sb.Length > size)
            {
                sb.Length = size;
            }
            
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static byte[] GenerateRandomData(int size)
        {
            var random = new Random(42); // Fixed seed for reproducibility
            var data = new byte[size];
            random.NextBytes(data);
            return data;
        }

        private static byte[] GenerateRepetitiveData(int size)
        {
            var data = new byte[size];
            var pattern = Encoding.UTF8.GetBytes("ABCDEFGH");
            
            for (int i = 0; i < size; i++)
            {
                data[i] = pattern[i % pattern.Length];
            }
            
            return data;
        }

        #endregion
    }
}
