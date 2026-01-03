using BenchmarkDotNet.Attributes;
using System;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Micro-benchmarks for profiling specific low-level operations
    /// Used to identify the most expensive individual operations
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class MicroBenchmarks
    {
        private byte[] _sourceData = null!;
        private byte[] _destData = null!;
        private const int TestSize = 1024;

        [GlobalSetup]
        public void Setup()
        {
            var random = new Random(42);
            _sourceData = new byte[TestSize];
            _destData = new byte[TestSize];
            random.NextBytes(_sourceData);
        }

        #region Array Operations

        [Benchmark(Description = "Array.Copy - 64 bytes")]
        public void ArrayCopy64()
        {
            Array.Copy(_sourceData, 0, _destData, 0, 64);
        }

        [Benchmark(Description = "Array.Copy - 256 bytes")]
        public void ArrayCopy256()
        {
            Array.Copy(_sourceData, 0, _destData, 0, 256);
        }

        [Benchmark(Description = "Buffer.BlockCopy - 64 bytes")]
        public void BufferBlockCopy64()
        {
            Buffer.BlockCopy(_sourceData, 0, _destData, 0, 64);
        }

        [Benchmark(Description = "Buffer.BlockCopy - 256 bytes")]
        public void BufferBlockCopy256()
        {
            Buffer.BlockCopy(_sourceData, 0, _destData, 0, 256);
        }

        [Benchmark(Description = "Manual Loop - 64 bytes")]
        public void ManualLoop64()
        {
            for (int i = 0; i < 64; i++)
            {
                _destData[i] = _sourceData[i];
            }
        }

        #endregion

        #region Byte Comparison

        [Benchmark(Description = "Byte-by-byte comparison - 4 bytes")]
        public bool ByteCompare4()
        {
            return _sourceData[0] == _destData[0] &&
                   _sourceData[1] == _destData[1] &&
                   _sourceData[2] == _destData[2] &&
                   _sourceData[3] == _destData[3];
        }

        [Benchmark(Description = "UInt32 comparison - 4 bytes")]
        public bool UInt32Compare()
        {
            uint a = BitConverter.ToUInt32(_sourceData, 0);
            uint b = BitConverter.ToUInt32(_destData, 0);
            return a == b;
        }

        [Benchmark(Description = "Loop comparison - 16 bytes")]
        public bool LoopCompare16()
        {
            for (int i = 0; i < 16; i++)
            {
                if (_sourceData[i] != _destData[i])
                    return false;
            }
            return true;
        }

        #endregion

        #region Hash Operations

        [Benchmark(Description = "Hash - BitConverter")]
        public int HashBitConverter()
        {
            uint value = BitConverter.ToUInt32(_sourceData, 0);
            return (int)((value * 2654435761u) >> 20);
        }

        [Benchmark(Description = "Hash - Manual shift")]
        public int HashManual()
        {
            uint value = (uint)(_sourceData[0] | (_sourceData[1] << 8) | (_sourceData[2] << 16) | (_sourceData[3] << 24));
            return (int)((value * 2654435761u) >> 20);
        }

        #endregion

        #region Match Copying (Overlapping)

        [Benchmark(Description = "Overlapping copy - Loop")]
        public void OverlappingCopyLoop()
        {
            // Simulate overlapping copy with offset=4, length=16
            int srcPos = 0;
            int dstPos = 4;
            for (int i = 0; i < 16; i++)
            {
                _destData[dstPos++] = _destData[srcPos++];
            }
        }

        [Benchmark(Description = "Overlapping copy - Unrolled")]
        public void OverlappingCopyUnrolled()
        {
            // Simulate overlapping copy with offset=4, length=16
            int srcPos = 0;
            int dstPos = 4;
            int remaining = 16;
            
            while (remaining >= 4)
            {
                _destData[dstPos] = _destData[srcPos];
                _destData[dstPos + 1] = _destData[srcPos + 1];
                _destData[dstPos + 2] = _destData[srcPos + 2];
                _destData[dstPos + 3] = _destData[srcPos + 3];
                srcPos += 4;
                dstPos += 4;
                remaining -= 4;
            }
            
            while (remaining > 0)
            {
                _destData[dstPos++] = _destData[srcPos++];
                remaining--;
            }
        }

        #endregion

        #region Literal Length Encoding

        [Benchmark(Description = "Encode literal length - short (< 15)")]
        public byte EncodeLiteralShort()
        {
            int length = 10;
            return (byte)(length << 4);
        }

        [Benchmark(Description = "Encode literal length - long (>= 15)")]
        public int EncodeLiteralLong()
        {
            int length = 300;
            int dstPos = 0;
            _destData[dstPos++] = (byte)(15 << 4);
            int len = length - 15;
            while (len >= 255)
            {
                _destData[dstPos++] = 255;
                len -= 255;
            }
            _destData[dstPos++] = (byte)len;
            return dstPos;
        }

        #endregion
    }
}
