using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using System.Text;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Benchmark comparing SIMD-enabled vs standard hash computation
    /// This benchmark compares the same code compiled with/without LZ4_ENABLE_SIMD_HASHING
    /// 
    /// To run both versions:
    /// 1. Run with standard build (no SIMD): dotnet run -c Release -- --filter "*SIMDHashingBenchmark*"
    /// 2. Rebuild with SIMD: dotnet build -c Release /p:DefineConstants="LZ4_ENABLE_SIMD_HASHING"
    /// 3. Run again with SIMD enabled
    /// 
    /// OR use the comparison script that builds both versions
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class SIMDHashingBenchmark
    {
        private byte[] _data10KB = null!;
        private byte[] _data100KB = null!;
        private byte[] _data1MB = null!;

        [GlobalSetup]
        public void Setup()
        {
            _data10KB = GenerateTextData(10 * 1024);
            _data100KB = GenerateTextData(100 * 1024);
            _data1MB = GenerateTextData(1024 * 1024);
        }

        #region Compression Benchmarks

        [Benchmark(Description = "Compress 10KB")]
        public int Compress10KB()
        {
            var dest = new byte[LZ4Codec.CompressBound(_data10KB.Length)];
            return LZ4Codec.CompressDefault(_data10KB, dest, _data10KB.Length, dest.Length);
        }

        [Benchmark(Description = "Compress 100KB")]
        public int Compress100KB()
        {
            var dest = new byte[LZ4Codec.CompressBound(_data100KB.Length)];
            return LZ4Codec.CompressDefault(_data100KB, dest, _data100KB.Length, dest.Length);
        }

        [Benchmark(Description = "Compress 1MB")]
        public int Compress1MB()
        {
            var dest = new byte[LZ4Codec.CompressBound(_data1MB.Length)];
            return LZ4Codec.CompressDefault(_data1MB, dest, _data1MB.Length, dest.Length);
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
