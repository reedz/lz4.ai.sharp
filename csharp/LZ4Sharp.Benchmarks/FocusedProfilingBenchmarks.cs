using BenchmarkDotNet.Attributes;
using System;
using System.Text;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Focused profiling benchmarks to identify specific performance bottlenecks
    /// in the compression algorithm based on algorithmic analysis
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 2, iterationCount: 5)]
    public class FocusedProfilingBenchmarks
    {
        private byte[] _testData = null!;
        private byte[] _compressedData = null!;
        private int _compressedSize;
        private const int DataSize = 100 * 1024; // 100KB

        [GlobalSetup]
        public void Setup()
        {
            // Generate text data (compressible)
            _testData = GenerateTextData(DataSize);
            
            int maxCompressedSize = LZ4Codec.CompressBound(DataSize);
            _compressedData = new byte[maxCompressedSize];
            _compressedSize = LZ4Codec.CompressDefault(_testData, _compressedData, DataSize, maxCompressedSize);
        }

        #region Compression Component Profiling

        [Benchmark(Description = "Full Compression (Baseline)")]
        public int FullCompression()
        {
            var dest = new byte[_compressedData.Length];
            return LZ4Codec.CompressDefault(_testData, dest, _testData.Length, dest.Length);
        }

        [Benchmark(Description = "Hash Table Operations")]
        public int HashTableOperations()
        {
            // Simulate hash table lookups during compression
            int[] hashTable = new int[4096];
            Array.Fill(hashTable, -1);
            int count = 0;
            
            for (int i = 0; i < _testData.Length - 4; i++)
            {
                uint value = BitConverter.ToUInt32(_testData, i);
                int hash = (int)((value * 2654435761u) >> 20);
                int candidate = hashTable[hash];
                hashTable[hash] = i;
                
                if (candidate >= 0 && i - candidate <= 65535)
                {
                    count++;
                }
            }
            
            return count;
        }

        [Benchmark(Description = "Match Finding")]
        public int MatchFinding()
        {
            // Simulate match finding without full compression
            int matchCount = 0;
            
            for (int i = 0; i < _testData.Length - 8; i += 4)
            {
                // Look back for potential matches
                int lookback = Math.Min(i, 65535);
                for (int j = Math.Max(0, i - lookback); j < i - 4; j += 64)
                {
                    if (AreEqual(_testData, j, i, 4))
                    {
                        matchCount++;
                        break;
                    }
                }
            }
            
            return matchCount;
        }

        [Benchmark(Description = "Literal Encoding")]
        public int LiteralEncoding()
        {
            // Simulate literal length encoding
            byte[] dest = new byte[DataSize * 2];
            int dstPos = 0;
            
            for (int literalLength = 0; literalLength < 1000; literalLength++)
            {
                if (literalLength >= 15)
                {
                    dest[dstPos++] = (byte)(15 << 4);
                    int len = literalLength - 15;
                    while (len >= 255)
                    {
                        dest[dstPos++] = 255;
                        len -= 255;
                    }
                    dest[dstPos++] = (byte)len;
                }
                else
                {
                    dest[dstPos++] = (byte)(literalLength << 4);
                }
            }
            
            return dstPos;
        }

        #endregion

        #region Decompression Component Profiling

        [Benchmark(Description = "Full Decompression (Baseline)")]
        public int FullDecompression()
        {
            var dest = new byte[_testData.Length];
            return LZ4Codec.DecompressSafe(_compressedData, dest, _compressedSize, _testData.Length);
        }

        [Benchmark(Description = "Token Parsing")]
        public int TokenParsing()
        {
            // Simulate token parsing overhead
            int tokenCount = 0;
            int pos = 0;
            
            while (pos < _compressedSize)
            {
                if (pos >= _compressedSize) break;
                
                int token = _compressedData[pos++];
                int literalLength = token >> 4;
                
                if (literalLength == 15)
                {
                    int len;
                    do
                    {
                        if (pos >= _compressedSize) break;
                        len = _compressedData[pos++];
                        literalLength += len;
                    } while (len == 255);
                }
                
                pos += literalLength;
                
                if (pos >= _compressedSize) break;
                
                // Skip offset
                pos += 2;
                
                int matchLength = (token & 15) + 4;
                if ((token & 15) == 15)
                {
                    int len;
                    do
                    {
                        if (pos >= _compressedSize) break;
                        len = _compressedData[pos++];
                        matchLength += len;
                    } while (len == 255);
                }
                
                tokenCount++;
            }
            
            return tokenCount;
        }

        [Benchmark(Description = "Match Copying")]
        public int MatchCopying()
        {
            // Simulate overlapping match copying
            byte[] dest = new byte[DataSize];
            int totalCopied = 0;
            
            // Simulate various offset and length combinations
            for (int i = 0; i < 1000; i++)
            {
                int dstPos = i * 16;
                int srcPos = dstPos - 8;
                int length = 16;
                
                if (srcPos >= 0 && dstPos + length < dest.Length)
                {
                    // Unrolled copy (current optimization)
                    int remaining = length;
                    while (remaining >= 4)
                    {
                        dest[dstPos] = dest[srcPos];
                        dest[dstPos + 1] = dest[srcPos + 1];
                        dest[dstPos + 2] = dest[srcPos + 2];
                        dest[dstPos + 3] = dest[srcPos + 3];
                        srcPos += 4;
                        dstPos += 4;
                        remaining -= 4;
                    }
                    while (remaining > 0)
                    {
                        dest[dstPos++] = dest[srcPos++];
                        remaining--;
                    }
                    totalCopied += length;
                }
            }
            
            return totalCopied;
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

        private static bool AreEqual(byte[] source, int pos1, int pos2, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (source[pos1 + i] != source[pos2 + i])
                    return false;
            }
            return true;
        }

        #endregion
    }
}
