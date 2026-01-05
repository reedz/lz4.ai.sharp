using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using System.Text;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Benchmark comparing adaptive hash sizing vs fixed hash table size
    /// Tests multiple data sizes to show cache optimization benefits
    /// </summary>
    [MemoryDiagnoser]
    [HardwareCounters(
        HardwareCounter.BranchMispredictions,
        HardwareCounter.CacheMisses,
        HardwareCounter.InstructionRetired,
        HardwareCounter.TotalCycles)]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class AdaptiveHashSizingBenchmark
    {
        private byte[] _data1KB = null!;
        private byte[] _data4KB = null!;
        private byte[] _data10KB = null!;
        private byte[] _data16KB = null!;
        private byte[] _data64KB = null!;
        private byte[] _data100KB = null!;
        private byte[] _data1MB = null!;
        private byte[] _destBuffer = null!;

        private LZ4Options _fixedHashOptions = null!;
        private LZ4Options _adaptiveHashOptions = null!;

        [GlobalSetup]
        public void Setup()
        {
            _data1KB = GenerateTextData(1 * 1024);
            _data4KB = GenerateTextData(4 * 1024);
            _data10KB = GenerateTextData(10 * 1024);
            _data16KB = GenerateTextData(16 * 1024);
            _data64KB = GenerateTextData(64 * 1024);
            _data100KB = GenerateTextData(100 * 1024);
            _data1MB = GenerateTextData(1024 * 1024);
            _destBuffer = new byte[LZ4Codec.CompressBound(1024 * 1024)];

            _fixedHashOptions = LZ4Options.Default;
            _adaptiveHashOptions = LZ4Options.AdaptiveHashSizing;
        }

        #region 1KB Benchmarks

        [Benchmark(Description = "1KB - Fixed Hash (4096 entries)", Baseline = true)]
        public int Compress1KB_Fixed()
        {
            return LZ4Codec.CompressDefault(_data1KB, _destBuffer, _data1KB.Length, _destBuffer.Length, _fixedHashOptions);
        }

        [Benchmark(Description = "1KB - Adaptive Hash (512 entries)")]
        public int Compress1KB_Adaptive()
        {
            return LZ4Codec.CompressDefault(_data1KB, _destBuffer, _data1KB.Length, _destBuffer.Length, _adaptiveHashOptions);
        }

        #endregion

        #region 4KB Benchmarks

        [Benchmark(Description = "4KB - Fixed Hash (4096 entries)")]
        public int Compress4KB_Fixed()
        {
            return LZ4Codec.CompressDefault(_data4KB, _destBuffer, _data4KB.Length, _destBuffer.Length, _fixedHashOptions);
        }

        [Benchmark(Description = "4KB - Adaptive Hash (512 entries)")]
        public int Compress4KB_Adaptive()
        {
            return LZ4Codec.CompressDefault(_data4KB, _destBuffer, _data4KB.Length, _destBuffer.Length, _adaptiveHashOptions);
        }

        #endregion

        #region 10KB Benchmarks

        [Benchmark(Description = "10KB - Fixed Hash (4096 entries)")]
        public int Compress10KB_Fixed()
        {
            return LZ4Codec.CompressDefault(_data10KB, _destBuffer, _data10KB.Length, _destBuffer.Length, _fixedHashOptions);
        }

        [Benchmark(Description = "10KB - Adaptive Hash (1024 entries)")]
        public int Compress10KB_Adaptive()
        {
            return LZ4Codec.CompressDefault(_data10KB, _destBuffer, _data10KB.Length, _destBuffer.Length, _adaptiveHashOptions);
        }

        #endregion

        #region 16KB Benchmarks

        [Benchmark(Description = "16KB - Fixed Hash (4096 entries)")]
        public int Compress16KB_Fixed()
        {
            return LZ4Codec.CompressDefault(_data16KB, _destBuffer, _data16KB.Length, _destBuffer.Length, _fixedHashOptions);
        }

        [Benchmark(Description = "16KB - Adaptive Hash (1024 entries)")]
        public int Compress16KB_Adaptive()
        {
            return LZ4Codec.CompressDefault(_data16KB, _destBuffer, _data16KB.Length, _destBuffer.Length, _adaptiveHashOptions);
        }

        #endregion

        #region 64KB Benchmarks

        [Benchmark(Description = "64KB - Fixed Hash (4096 entries)")]
        public int Compress64KB_Fixed()
        {
            return LZ4Codec.CompressDefault(_data64KB, _destBuffer, _data64KB.Length, _destBuffer.Length, _fixedHashOptions);
        }

        [Benchmark(Description = "64KB - Adaptive Hash (2048 entries)")]
        public int Compress64KB_Adaptive()
        {
            return LZ4Codec.CompressDefault(_data64KB, _destBuffer, _data64KB.Length, _destBuffer.Length, _adaptiveHashOptions);
        }

        #endregion

        #region 100KB Benchmarks

        [Benchmark(Description = "100KB - Fixed Hash (4096 entries)")]
        public int Compress100KB_Fixed()
        {
            return LZ4Codec.CompressDefault(_data100KB, _destBuffer, _data100KB.Length, _destBuffer.Length, _fixedHashOptions);
        }

        [Benchmark(Description = "100KB - Adaptive Hash (4096 entries)")]
        public int Compress100KB_Adaptive()
        {
            return LZ4Codec.CompressDefault(_data100KB, _destBuffer, _data100KB.Length, _destBuffer.Length, _adaptiveHashOptions);
        }

        #endregion

        #region 1MB Benchmarks

        [Benchmark(Description = "1MB - Fixed Hash (4096 entries)")]
        public int Compress1MB_Fixed()
        {
            return LZ4Codec.CompressDefault(_data1MB, _destBuffer, _data1MB.Length, _destBuffer.Length, _fixedHashOptions);
        }

        [Benchmark(Description = "1MB - Adaptive Hash (4096 entries)")]
        public int Compress1MB_Adaptive()
        {
            return LZ4Codec.CompressDefault(_data1MB, _destBuffer, _data1MB.Length, _destBuffer.Length, _adaptiveHashOptions);
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

        #endregion
    }
}
