using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using System;
using System.Text;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Benchmarks designed to measure actual CPU cycles consumed by LZ4 operations
    /// Validates theoretical CPU cycle analysis
    /// </summary>
    [MemoryDiagnoser]
    [HardwareCounters(
        HardwareCounter.BranchMispredictions,
        HardwareCounter.CacheMisses,
        HardwareCounter.InstructionRetired,
        HardwareCounter.TotalCycles)]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class CpuCycleBenchmarks
    {
        private byte[] _testData10KB = null!;
        private byte[] _testData100KB = null!;
        private byte[] _compressed10KB = null!;
        private byte[] _compressed100KB = null!;
        private int _compressedSize10KB;
        private int _compressedSize100KB;

        [GlobalSetup]
        public void Setup()
        {
            // 10KB test data
            _testData10KB = GenerateTextData(10 * 1024);
            int maxCompressed10KB = LZ4Codec.CompressBound(10 * 1024);
            _compressed10KB = new byte[maxCompressed10KB];
            _compressedSize10KB = LZ4Codec.CompressDefault(_testData10KB, _compressed10KB, 10 * 1024, maxCompressed10KB);

            // 100KB test data
            _testData100KB = GenerateTextData(100 * 1024);
            int maxCompressed100KB = LZ4Codec.CompressBound(100 * 1024);
            _compressed100KB = new byte[maxCompressed100KB];
            _compressedSize100KB = LZ4Codec.CompressDefault(_testData100KB, _compressed100KB, 100 * 1024, maxCompressed100KB);
        }

        #region 10KB Benchmarks

        [Benchmark(Description = "Compress 10KB (Text)")]
        public int Compress10KB()
        {
            var dest = new byte[_compressed10KB.Length];
            return LZ4Codec.CompressDefault(_testData10KB, dest, _testData10KB.Length, dest.Length);
        }

        [Benchmark(Description = "Decompress 10KB (Text)")]
        public int Decompress10KB()
        {
            var dest = new byte[_testData10KB.Length];
            return LZ4Codec.DecompressSafe(_compressed10KB, dest, _compressedSize10KB, dest.Length);
        }

        #endregion

        #region 100KB Benchmarks

        [Benchmark(Description = "Compress 100KB (Text)")]
        public int Compress100KB()
        {
            var dest = new byte[_compressed100KB.Length];
            return LZ4Codec.CompressDefault(_testData100KB, dest, _testData100KB.Length, dest.Length);
        }

        [Benchmark(Description = "Decompress 100KB (Text)")]
        public int Decompress100KB()
        {
            var dest = new byte[_testData100KB.Length];
            return LZ4Codec.DecompressSafe(_compressed100KB, dest, _compressedSize100KB, dest.Length);
        }

        #endregion

        #region Component-Level Cycle Analysis

        /// <summary>
        /// Measures cycles for hash computation only
        /// </summary>
        [Benchmark(Description = "Hash Computation (100KB)")]
        public int HashComputation()
        {
            int result = 0;
            for (int i = 0; i < _testData100KB.Length - 4; i++)
            {
                if (i + 4 <= _testData100KB.Length)
                {
                    uint value = BitConverter.ToUInt32(_testData100KB, i);
                    int hash = (int)((value * 2654435761u) >> 20);
                    result ^= hash; // Use result to prevent optimization
                }
            }
            return result;
        }

        /// <summary>
        /// Measures cycles for UInt32 comparison
        /// </summary>
        [Benchmark(Description = "UInt32 Comparison (100K iterations)")]
        public int UInt32Comparisons()
        {
            int matches = 0;
            for (int i = 0; i < 100000; i++)
            {
                int pos1 = i % (_testData100KB.Length - 8);
                int pos2 = (i + 1000) % (_testData100KB.Length - 8);
                
                if (pos1 + 4 <= _testData100KB.Length && pos2 + 4 <= _testData100KB.Length)
                {
                    uint val1 = BitConverter.ToUInt32(_testData100KB, pos1);
                    uint val2 = BitConverter.ToUInt32(_testData100KB, pos2);
                    if (val1 == val2)
                        matches++;
                }
            }
            return matches;
        }

        /// <summary>
        /// Measures cycles for UInt64 comparison (potential optimization)
        /// </summary>
        [Benchmark(Description = "UInt64 Comparison (100K iterations)")]
        public int UInt64Comparisons()
        {
            int matches = 0;
            for (int i = 0; i < 100000; i++)
            {
                int pos1 = i % (_testData100KB.Length - 16);
                int pos2 = (i + 1000) % (_testData100KB.Length - 16);
                
                if (pos1 + 8 <= _testData100KB.Length && pos2 + 8 <= _testData100KB.Length)
                {
                    ulong val1 = BitConverter.ToUInt64(_testData100KB, pos1);
                    ulong val2 = BitConverter.ToUInt64(_testData100KB, pos2);
                    if (val1 == val2)
                        matches++;
                }
            }
            return matches;
        }

        /// <summary>
        /// Measures cycles for Buffer.BlockCopy
        /// </summary>
        [Benchmark(Description = "Buffer.BlockCopy (1000 × 64 bytes)")]
        public void BufferBlockCopy()
        {
            var dest = new byte[_testData100KB.Length];
            for (int i = 0; i < 1000; i++)
            {
                int srcPos = (i * 64) % (_testData100KB.Length - 64);
                int dstPos = (i * 64) % (dest.Length - 64);
                Buffer.BlockCopy(_testData100KB, srcPos, dest, dstPos, 64);
            }
        }

        #endregion

        #region Different Data Patterns

        [Benchmark(Description = "Compress 100KB (Repetitive)")]
        public int CompressRepetitive()
        {
            var repetitiveData = GenerateRepetitiveData(100 * 1024);
            var dest = new byte[LZ4Codec.CompressBound(repetitiveData.Length)];
            return LZ4Codec.CompressDefault(repetitiveData, dest, repetitiveData.Length, dest.Length);
        }

        [Benchmark(Description = "Compress 100KB (Random)")]
        public int CompressRandom()
        {
            var randomData = GenerateRandomData(100 * 1024);
            var dest = new byte[LZ4Codec.CompressBound(randomData.Length)];
            return LZ4Codec.CompressDefault(randomData, dest, randomData.Length, dest.Length);
        }

        #endregion

        #region Helper Methods

        private static byte[] GenerateTextData(int size)
        {
            var sb = new StringBuilder();
            string[] words = { "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog", "Lorem", "ipsum", "dolor", "sit", "amet" };
            var random = new Random(42);

            while (sb.Length < size)
            {
                sb.Append(words[random.Next(words.Length)]);
                sb.Append(' ');
            }

            return Encoding.ASCII.GetBytes(sb.ToString(0, Math.Min(size, sb.Length)));
        }

        private static byte[] GenerateRepetitiveData(int size)
        {
            var data = new byte[size];
            byte[] pattern = Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOP");
            
            for (int i = 0; i < size; i++)
            {
                data[i] = pattern[i % pattern.Length];
            }
            
            return data;
        }

        private static byte[] GenerateRandomData(int size)
        {
            var data = new byte[size];
            var random = new Random(42);
            random.NextBytes(data);
            return data;
        }

        #endregion
    }
}
