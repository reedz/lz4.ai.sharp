using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using System.Text;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Benchmarks comparing LZ4Sharp against K4os.Compression.LZ4
    /// Tests compression and decompression performance with different payload sizes
    /// </summary>
    [MemoryDiagnoser]
    [Config(typeof(Config))]
    public class LZ4CompressionBenchmarks
    {
        private class Config : ManualConfig
        {
            public Config()
            {
                AddJob(Job.Default.WithId(".NET 10"));
            }
        }

        private byte[] _data = null!;
        private byte[] _compressedLZ4Sharp = null!;
        private byte[] _compressedK4os = null!;
        private int _compressedSizeLZ4Sharp;
        private int _compressedSizeK4os;

        [Params(1024, 10 * 1024, 100 * 1024, 1024 * 1024)]
        public int DataSize { get; set; }

        [Params(DataPattern.Text, DataPattern.Random, DataPattern.Repetitive)]
        public DataPattern Pattern { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _data = GenerateData(DataSize, Pattern);

            // Setup LZ4Sharp
            int maxCompressedSize = LZ4Codec.CompressBound(DataSize);
            _compressedLZ4Sharp = new byte[maxCompressedSize];
            _compressedSizeLZ4Sharp = LZ4Codec.CompressDefault(_data, _compressedLZ4Sharp, DataSize, maxCompressedSize);

            // Setup K4os
            _compressedK4os = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(DataSize)];
            _compressedSizeK4os = K4os.Compression.LZ4.LZ4Codec.Encode(
                _data, 0, DataSize,
                _compressedK4os, 0, _compressedK4os.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
        }

        #region Compression Benchmarks

        [Benchmark(Description = "LZ4Sharp - Compress")]
        public int CompressLZ4Sharp()
        {
            var dest = new byte[_compressedLZ4Sharp.Length];
            return LZ4Codec.CompressDefault(_data, dest, DataSize, dest.Length);
        }

        [Benchmark(Description = "K4os.LZ4 - Compress", Baseline = true)]
        public int CompressK4os()
        {
            var dest = new byte[_compressedK4os.Length];
            return K4os.Compression.LZ4.LZ4Codec.Encode(
                _data, 0, DataSize,
                dest, 0, dest.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
        }

        #endregion

        #region Decompression Benchmarks

        [Benchmark(Description = "LZ4Sharp - Decompress")]
        public int DecompressLZ4Sharp()
        {
            var dest = new byte[DataSize];
            return LZ4Codec.DecompressSafe(_compressedLZ4Sharp, dest, _compressedSizeLZ4Sharp, DataSize);
        }

        [Benchmark(Description = "K4os.LZ4 - Decompress")]
        public int DecompressK4os()
        {
            var dest = new byte[DataSize];
            return K4os.Compression.LZ4.LZ4Codec.Decode(
                _compressedK4os, 0, _compressedSizeK4os,
                dest, 0, DataSize);
        }

        #endregion

        #region Data Generation

        private static byte[] GenerateData(int size, DataPattern pattern)
        {
            return pattern switch
            {
                DataPattern.Text => GenerateTextData(size),
                DataPattern.Random => GenerateRandomData(size),
                DataPattern.Repetitive => GenerateRepetitiveData(size),
                _ => throw new ArgumentException($"Unknown pattern: {pattern}")
            };
        }

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
            
            // Trim to exact size without creating intermediate string
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

    public enum DataPattern
    {
        Text,
        Random,
        Repetitive
    }
}
