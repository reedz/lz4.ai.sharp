/*
 * SimpleBuffer.cs
 * Copyright  : Translated from simple_buffer.c by Kyle Harper
 * License    : Follows same licensing as the lz4.c/lz4.h program. Currently, BSD 2.
 * Description: Example program to demonstrate the basic usage of the compress/decompress functions.
 *              The functions you'll likely want are CompressDefault and DecompressSafe.
 */

using System;
using System.Text;

namespace LZ4Sharp.Examples
{
    class SimpleBuffer
    {
        static void RunScreaming(string message, int code)
        {
            Console.WriteLine(message);
            Environment.Exit(code);
        }

        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0].ToLower() == "demo")
            {
                CompressionDemo.RunDemo();
                return;
            }
            
            if (args.Length > 0 && args[0].ToLower() == "hc")
            {
                HighCompressionExample.Run();
                return;
            }

            /* Introduction */
            // Below we will have a Compression and Decompression section to demonstrate.
            // There are a few important notes before we start:
            //   1) The return codes of LZ4 functions are important.
            //      Negative values indicate errors.
            //   2) LZ4 in C# uses byte arrays instead of char* pointers.

            /* Compression */
            // We'll store some text into a string to be compressed later.
            string text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Lorem ipsum dolor site amat.";
            // Convert the string to bytes (including null terminator for compatibility)
            byte[] src = Encoding.UTF8.GetBytes(text + "\0");
            // The compression function needs to know how many bytes exist.
            int srcSize = src.Length;

            Console.WriteLine($"Original string length: {srcSize} bytes");
            Console.WriteLine($"Original string: {text}");

            // LZ4 provides a function that will tell you the maximum size of compressed output based on input data.
            int maxDstSize = LZ4Codec.CompressBound(srcSize);
            Console.WriteLine($"Maximum compressed size: {maxDstSize} bytes");

            // We will use that size for our destination boundary when allocating space.
            byte[] compressedData = new byte[maxDstSize];

            // That's all the information and preparation LZ4 needs to compress src into compressedData.
            // Invoke CompressDefault now with our size values and arrays.
            // Save the return value for error checking.
            int compressedDataSize = LZ4Codec.CompressDefault(src, compressedData, srcSize, maxDstSize);

            // Check return value to determine what happened.
            if (compressedDataSize <= 0)
                RunScreaming("A 0 or negative result from CompressDefault() indicates a failure trying to compress the data.", 1);

            Console.WriteLine($"We successfully compressed some data! Ratio: {(float)compressedDataSize / srcSize:F2}");
            Console.WriteLine($"Compressed size: {compressedDataSize} bytes");

            // Not only does a positive return value mean success, the value returned == the number of bytes required.
            // You can use this to resize the array if desired. We'll do so just to demonstrate the concept.
            Array.Resize(ref compressedData, compressedDataSize);

            /* Decompression */
            // Now that we've successfully compressed the information from src to compressedData, let's do the opposite!
            // The decompression will need to know the compressed size, and an upper bound of the decompressed size.
            // In this example, we just re-use this information from previous section,
            // but in a real-world scenario, metadata must be transmitted to the decompression side.

            // First, let's create a new buffer of size srcSize since we know that value.
            byte[] regenBuffer = new byte[srcSize];

            // The DecompressSafe function needs to know where the compressed data is, how many bytes long it is,
            // where the regenBuffer memory location is, and how large regenBuffer (uncompressed) output will be.
            // Again, save the return value.
            int decompressedSize = LZ4Codec.DecompressSafe(compressedData, regenBuffer, compressedDataSize, srcSize);

            if (decompressedSize < 0)
                RunScreaming($"A negative result from DecompressSafe indicates a failure trying to decompress the data. Error code: {decompressedSize}", decompressedSize);

            Console.WriteLine("We successfully decompressed some data!");

            // Not only does a positive return value mean success,
            // value returned == number of bytes regenerated from compressed data stream.
            if (decompressedSize != srcSize)
                RunScreaming("Decompressed data is different from original!", 1);

            /* Validation */
            // We should be able to compare our original src with our regenBuffer and be byte-for-byte identical.
            bool isEqual = true;
            for (int i = 0; i < srcSize; i++)
            {
                if (src[i] != regenBuffer[i])
                {
                    isEqual = false;
                    break;
                }
            }

            if (!isEqual)
                RunScreaming("Validation failed. src and regenBuffer are not identical.", 1);

            string decompressedText = Encoding.UTF8.GetString(regenBuffer, 0, regenBuffer.Length - 1); // Exclude null terminator
            Console.WriteLine($"Validation done. The string we ended up with is:\n{decompressedText}");
            Console.WriteLine("\nSuccess! LZ4 compression and decompression completed successfully.");
        }
    }
}
