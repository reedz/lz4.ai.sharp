using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using System.Text;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Benchmark comparing SIMD vs non-SIMD hashing using runtime LZ4Options
    /// This benchmark runs both configurations in a single build for direct comparison
    /// </summary>
    [MemoryDiagnoser]
    [HardwareCounters(
        HardwareCounter.BranchMispredictions,
        HardwareCounter.CacheMisses,
        HardwareCounter.InstructionRetired,
        HardwareCounter.TotalCycles)]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class SIMDComparisonBenchmark
    {
        private byte[] _data10KB = null!;
        private byte[] _data100KB = null!;
        private byte[] _data1MB = null!;
        private byte[] _destBuffer = null!;

        private LZ4Options _standardOptions = null!;
        private LZ4Options _simdOptions = null!;

        [GlobalSetup]
        public void Setup()
        {
            _data10KB = GenerateTextData(10 * 1024);
            _data100KB = GenerateTextData(100 * 1024);
            _data1MB = GenerateTextData(1024 * 1024);
            _destBuffer = new byte[LZ4Codec.CompressBound(1024 * 1024)];

            _standardOptions = LZ4Options.Default;
            _simdOptions = LZ4Options.SIMDEnabled;
        }

        #region 10KB Benchmarks

        [Benchmark(Description = "10KB - Standard Hashing", Baseline = true)]
        public int Compress10KB_Standard()
        {
            return LZ4Codec.CompressDefault(_data10KB, _destBuffer, _data10KB.Length, _destBuffer.Length, _standardOptions);
        }

        [Benchmark(Description = "10KB - SIMD Hashing")]
        public int Compress10KB_SIMD()
        {
            return LZ4Codec.CompressDefault(_data10KB, _destBuffer, _data10KB.Length, _destBuffer.Length, _simdOptions);
        }

        #endregion

        #region 100KB Benchmarks

        [Benchmark(Description = "100KB - Standard Hashing")]
        public int Compress100KB_Standard()
        {
            return LZ4Codec.CompressDefault(_data100KB, _destBuffer, _data100KB.Length, _destBuffer.Length, _standardOptions);
        }

        [Benchmark(Description = "100KB - SIMD Hashing")]
        public int Compress100KB_SIMD()
        {
            return LZ4Codec.CompressDefault(_data100KB, _destBuffer, _data100KB.Length, _destBuffer.Length, _simdOptions);
        }

        #endregion

        #region 1MB Benchmarks

        [Benchmark(Description = "1MB - Standard Hashing")]
        public int Compress1MB_Standard()
        {
            return LZ4Codec.CompressDefault(_data1MB, _destBuffer, _data1MB.Length, _destBuffer.Length, _standardOptions);
        }

        [Benchmark(Description = "1MB - SIMD Hashing")]
        public int Compress1MB_SIMD()
        {
            return LZ4Codec.CompressDefault(_data1MB, _destBuffer, _data1MB.Length, _destBuffer.Length, _simdOptions);
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
