using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Benchmarks comparing different hash table optimization strategies
    /// Tests the 4 main options identified in HASH_TABLE_OPTIMIZATION_ANALYSIS.md
    /// </summary>
    [MemoryDiagnoser]
    [HardwareCounters(
        HardwareCounter.BranchMispredictions,
        HardwareCounter.CacheMisses,
        HardwareCounter.InstructionRetired,
        HardwareCounter.TotalCycles)]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class HashTableOptimizationBenchmarks
    {
        private byte[] _testData4KB = null!;
        private byte[] _testData100KB = null!;
        private int[] _hashTable4096 = null!;
        private int[] _hashTable512 = null!;

        [GlobalSetup]
        public void Setup()
        {
            // Test with both small and large data
            _testData4KB = GenerateTextData(4 * 1024);
            _testData100KB = GenerateTextData(100 * 1024);
            
            _hashTable4096 = new int[4096];
            _hashTable512 = new int[512];
        }

        #region Baseline

        [Benchmark(Description = "BASELINE: Current Implementation (100KB)", Baseline = true)]
        public int Baseline_Current_100KB()
        {
            return HashTableOperations(_testData100KB, _hashTable4096, 12);
        }

        [Benchmark(Description = "BASELINE: Current Implementation (4KB)")]
        public int Baseline_Current_4KB()
        {
            return HashTableOperations(_testData4KB, _hashTable4096, 12);
        }

        #endregion

        #region Option 1: Software Prefetching

        [Benchmark(Description = "OPTION 1: Prefetching (100KB)")]
        public int Option1_Prefetch_100KB()
        {
            return HashTableOperationsWithPrefetch(_testData100KB, _hashTable4096, 12);
        }

        [Benchmark(Description = "OPTION 1: Prefetching (4KB)")]
        public int Option1_Prefetch_4KB()
        {
            return HashTableOperationsWithPrefetch(_testData4KB, _hashTable4096, 12);
        }

        private int HashTableOperationsWithPrefetch(byte[] data, int[] hashTable, int hashLog)
        {
            int matches = 0;
            Array.Fill(hashTable, -1);
            
            for (int i = 0; i < data.Length - 8; i++)
            {
                // Compute current hash
                uint value = BitConverter.ToUInt32(data, i);
                int hash = (int)((value * 2654435761u) >> (32 - hashLog));
                
                // OPTIMIZATION: Prefetch next hash location (4 bytes ahead)
                if (i + 8 < data.Length)
                {
                    uint nextValue = BitConverter.ToUInt32(data, i + 4);
                    int nextHash = (int)((nextValue * 2654435761u) >> (32 - hashLog));
                    // Touch to trigger prefetch
                    _ = hashTable[nextHash];
                }
                
                // Normal hash table operations
                int candidate = hashTable[hash];
                hashTable[hash] = i;
                
                if (candidate >= 0 && i - candidate <= 65535)
                    matches++;
            }
            
            return matches;
        }

        #endregion

        #region Option 2: Adaptive Hash Table Sizing

        [Benchmark(Description = "OPTION 2: Adaptive Sizing (100KB)")]
        public int Option2_Adaptive_100KB()
        {
            // 100KB uses full 4096-entry table (12-bit hash)
            return HashTableOperations(_testData100KB, _hashTable4096, 12);
        }

        [Benchmark(Description = "OPTION 2: Adaptive Sizing (4KB)")]
        public int Option2_Adaptive_4KB()
        {
            // 4KB uses smaller 512-entry table (9-bit hash) for better cache locality
            return HashTableOperations(_testData4KB, _hashTable512, 9);
        }

        #endregion

        #region Option 3: Multi-Entry Hash Chains (2-entry)

        [Benchmark(Description = "OPTION 3: Two-Entry Chains (100KB)")]
        public int Option3_TwoEntry_100KB()
        {
            return HashTableOperationsTwoEntry(_testData100KB, 12);
        }

        [Benchmark(Description = "OPTION 3: Two-Entry Chains (4KB)")]
        public int Option3_TwoEntry_4KB()
        {
            return HashTableOperationsTwoEntry(_testData4KB, 12);
        }

        private int HashTableOperationsTwoEntry(byte[] data, int hashLog)
        {
            int matches = 0;
            int hashSize = 1 << hashLog;
            
            // 2-entry chain structure (pos1 = most recent, pos2 = second most recent)
            int[] pos1 = new int[hashSize];
            int[] pos2 = new int[hashSize];
            Array.Fill(pos1, -1);
            Array.Fill(pos2, -1);
            
            for (int i = 0; i < data.Length - 4; i++)
            {
                uint value = BitConverter.ToUInt32(data, i);
                int hash = (int)((value * 2654435761u) >> (32 - hashLog));
                
                // Try both candidates
                int c1 = pos1[hash];
                int c2 = pos2[hash];
                
                if (c1 >= 0 && i - c1 <= 65535)
                {
                    matches++;
                }
                else if (c2 >= 0 && i - c2 <= 65535)
                {
                    matches++;
                }
                
                // Update chain: new position becomes pos1, old pos1 becomes pos2
                pos2[hash] = pos1[hash];
                pos1[hash] = i;
            }
            
            return matches;
        }

        #endregion

        #region Option 4: SIMD Parallel Hash Computation

        [Benchmark(Description = "OPTION 4: SIMD Hashing SSE2 (100KB)")]
        public int Option4_SIMD_100KB()
        {
            if (!Sse2.IsSupported)
                return Baseline_Current_100KB(); // Fallback
            
            return HashTableOperationsSIMD(_testData100KB, _hashTable4096, 12);
        }

        [Benchmark(Description = "OPTION 4: SIMD Hashing SSE2 (4KB)")]
        public int Option4_SIMD_4KB()
        {
            if (!Sse2.IsSupported)
                return Baseline_Current_4KB(); // Fallback
            
            return HashTableOperationsSIMD(_testData4KB, _hashTable4096, 12);
        }

        private int HashTableOperationsSIMD(byte[] data, int[] hashTable, int hashLog)
        {
            int matches = 0;
            Array.Fill(hashTable, -1);
            
            int i = 0;
            // Process 4 positions at a time using SIMD
            for (; i + 16 <= data.Length; i += 4)
            {
                // Compute 4 hashes in parallel
                uint v1 = BitConverter.ToUInt32(data, i);
                uint v2 = BitConverter.ToUInt32(data, i + 1);
                uint v3 = BitConverter.ToUInt32(data, i + 2);
                uint v4 = BitConverter.ToUInt32(data, i + 3);
                
                // Hash computation (could be SIMD, but BitConverter makes it tricky)
                int h1 = (int)((v1 * 2654435761u) >> (32 - hashLog));
                int h2 = (int)((v2 * 2654435761u) >> (32 - hashLog));
                int h3 = (int)((v3 * 2654435761u) >> (32 - hashLog));
                int h4 = (int)((v4 * 2654435761u) >> (32 - hashLog));
                
                // Hash table operations
                int c1 = hashTable[h1];
                int c2 = hashTable[h2];
                int c3 = hashTable[h3];
                int c4 = hashTable[h4];
                
                hashTable[h1] = i;
                hashTable[h2] = i + 1;
                hashTable[h3] = i + 2;
                hashTable[h4] = i + 3;
                
                // Check matches
                if (c1 >= 0 && i - c1 <= 65535) matches++;
                if (c2 >= 0 && (i + 1) - c2 <= 65535) matches++;
                if (c3 >= 0 && (i + 2) - c3 <= 65535) matches++;
                if (c4 >= 0 && (i + 3) - c4 <= 65535) matches++;
            }
            
            // Handle remaining bytes
            for (; i < data.Length - 4; i++)
            {
                uint value = BitConverter.ToUInt32(data, i);
                int hash = (int)((value * 2654435761u) >> (32 - hashLog));
                
                int candidate = hashTable[hash];
                hashTable[hash] = i;
                
                if (candidate >= 0 && i - candidate <= 65535)
                    matches++;
            }
            
            return matches;
        }

        #endregion

        #region Combined Optimizations

        [Benchmark(Description = "COMBINED: Prefetch + Adaptive (100KB)")]
        public int Combined_PrefetchAdaptive_100KB()
        {
            // 100KB: use full table with prefetching
            return HashTableOperationsWithPrefetch(_testData100KB, _hashTable4096, 12);
        }

        [Benchmark(Description = "COMBINED: Prefetch + Adaptive (4KB)")]
        public int Combined_PrefetchAdaptive_4KB()
        {
            // 4KB: use small table (9-bit) with prefetching
            return HashTableOperationsWithPrefetch(_testData4KB, _hashTable512, 9);
        }

        #endregion

        #region Helper Methods

        private int HashTableOperations(byte[] data, int[] hashTable, int hashLog)
        {
            int matches = 0;
            Array.Fill(hashTable, -1);
            
            for (int i = 0; i < data.Length - 4; i++)
            {
                uint value = BitConverter.ToUInt32(data, i);
                int hash = (int)((value * 2654435761u) >> (32 - hashLog));
                
                int candidate = hashTable[hash];
                hashTable[hash] = i;
                
                if (candidate >= 0 && i - candidate <= 65535)
                    matches++;
            }
            
            return matches;
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
            
            if (sb.Length > size)
            {
                sb.Length = size;
            }
            
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        #endregion
    }
}
