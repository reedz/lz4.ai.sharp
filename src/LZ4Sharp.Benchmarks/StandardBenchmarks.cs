using BenchmarkDotNet.Attributes;
using K4os.Compression.LZ4;
using LZ4Sharp;
using System;
using System.Text;
using System.Linq;

namespace LZ4Sharp.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 2, iterationCount: 3)]
    public class StandardBenchmarks
    {
        private byte[] _payload = null!;
        private byte[] _destination = null!;
        private byte[] _destinationK4os = null!;

        // 72KB is large enough to trigger CompressLargeInput which we optimized
        [Params(72 * 1024)] 
        public int Size { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _payload = new byte[Size];
            new Random(42).NextBytes(_payload);
            
            // Make it compressible
            for (int i = 0; i < Size; i++)
            {
                _payload[i] = (byte)(_payload[i] % 64); 
            }

            int maxDst = LZ4Codec.CompressBound(Size);
            _destination = new byte[maxDst];
            _destinationK4os = new byte[maxDst];
        }

        [Benchmark(Description = "LZ4Sharp - Standard Compress")]
        public int CompressStandardLZ4Sharp()
        {
            return LZ4Codec.CompressDefault(_payload, _destination, _payload.Length, _destination.Length);
        }

        [Benchmark(Description = "K4os.LZ4 - Standard Compress", Baseline = true)]
        public int CompressStandardK4os()
        {
            return K4os.Compression.LZ4.LZ4Codec.Encode(_payload, 0, _payload.Length, _destinationK4os, 0, _destinationK4os.Length, LZ4Level.L00_FAST);
        }
    }
}
