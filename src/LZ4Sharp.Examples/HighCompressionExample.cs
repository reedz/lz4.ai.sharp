using LZ4Sharp;
using System.Text;

namespace LZ4Sharp.Examples
{
    /// <summary>
    /// Example demonstrating LZ4 High Compression (HC) mode
    /// </summary>
    public static class HighCompressionExample
    {
        public static void Run()
        {
            Console.WriteLine("=== LZ4 High Compression (HC) Example ===\n");

            // Create sample data with lots of repetition for better compression
            string sampleText = string.Concat(Enumerable.Repeat(
                "The quick brown fox jumps over the lazy dog. ", 100));
            
            byte[] originalData = Encoding.UTF8.GetBytes(sampleText);
            int originalSize = originalData.Length;

            Console.WriteLine($"Original data size: {originalSize:N0} bytes");
            Console.WriteLine();

            // Test different compression methods
            TestStandardCompression(originalData, originalSize);
            Console.WriteLine();
            
            TestHCCompressionLevels(originalData, originalSize);
        }

        private static void TestStandardCompression(byte[] data, int size)
        {
            Console.WriteLine("--- Standard LZ4 Compression ---");
            
            byte[] compressed = new byte[LZ4Codec.CompressBound(size)];
            int compressedSize = LZ4Codec.CompressDefault(data, compressed, size, compressed.Length);

            if (compressedSize > 0)
            {
                double ratio = (double)compressedSize / size;
                double savings = (1.0 - ratio) * 100;
                
                Console.WriteLine($"Compressed size: {compressedSize:N0} bytes");
                Console.WriteLine($"Compression ratio: {ratio:P2}");
                Console.WriteLine($"Space savings: {savings:F2}%");
            }
            else
            {
                Console.WriteLine("Compression failed!");
            }
        }

        private static void TestHCCompressionLevels(byte[] data, int size)
        {
            Console.WriteLine("--- HC Compression (Different Levels) ---");
            Console.WriteLine();

            int[] levels = { 3, 9, 12 };
            
            foreach (int level in levels)
            {
                byte[] compressed = new byte[LZ4HC.CompressBound(size)];
                byte[] decompressed = new byte[size];

                // Compress
                int compressedSize = LZ4HC.CompressHC(data, compressed, size, compressed.Length, level);

                if (compressedSize > 0)
                {
                    // Decompress to verify
                    int decompressedSize = LZ4Codec.DecompressSafe(compressed, decompressed, compressedSize, size);
                    
                    if (decompressedSize == size && data.SequenceEqual(decompressed))
                    {
                        double ratio = (double)compressedSize / size;
                        double savings = (1.0 - ratio) * 100;
                        
                        Console.WriteLine($"Level {level,2}: {compressedSize:N0} bytes ({ratio:P2} ratio, {savings:F2}% savings)");
                    }
                    else
                    {
                        Console.WriteLine($"Level {level,2}: Decompression verification failed!");
                    }
                }
                else
                {
                    Console.WriteLine($"Level {level,2}: Compression failed!");
                }
            }

            Console.WriteLine();
            Console.WriteLine("NOTE: Current HC implementation is a stub that uses standard LZ4.");
            Console.WriteLine("Full HC implementation would provide better compression ratios.");
            Console.WriteLine("Expected improvement: 10-30% better compression at similar levels.");
        }
    }
}
