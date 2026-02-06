using System;
using System.IO;
using System.Linq;
using System.Text;
using K4os.Compression.LZ4.Streams;
using LZ4Sharp;
using Xunit;
using Xunit.Abstractions;

namespace LZ4Sharp.Tests;

/// <summary>
/// Tests for LZ4Frame interoperability with K4os.Compression.LZ4 and lz4 CLI.
/// Validates that frames produced by LZ4Sharp can be decompressed by other implementations.
/// </summary>
public class FrameK4osInteropTest
{
    private readonly ITestOutputHelper _output;
    public FrameK4osInteropTest(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-8)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(9)]
    public void LZ4SharpFrame_K4osDecompress_AllLevels(int level)
    {
        var testData = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Hello World! Test data. ", 100)));
        
        var prefs = new LZ4Frame.FramePreferences { CompressionLevel = level };
        var compressed = new byte[LZ4Frame.CompressFrameBound(testData.Length, prefs)];
        int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, testData, testData.Length, prefs);
        
        Assert.True(compressedSize > 0, "Compression should succeed");
        
        using var inputStream = new MemoryStream(compressed, 0, compressedSize);
        using var decoder = LZ4Stream.Decode(inputStream);
        using var output = new MemoryStream();
        decoder.CopyTo(output);
        var result = output.ToArray();
        
        Assert.Equal(testData.Length, result.Length);
        Assert.True(testData.SequenceEqual(result), "Decompressed data should match original");
    }

    [Theory]
    [InlineData(-1, 100)]      // 100 bytes
    [InlineData(-1, 1000)]     // 1 KB
    [InlineData(-1, 10000)]    // 10 KB
    [InlineData(-1, 70000)]    // 70 KB (exceeds 64KB block)
    [InlineData(-1, 100000)]   // 100 KB (multi-block)
    [InlineData(-1, 300000)]   // 300 KB (multi-block)
    [InlineData(-8, 100000)]   // high acceleration, large data
    public void LZ4SharpFrame_K4osDecompress_VariousSizes(int level, int size)
    {
        var testData = new byte[size];
        for (int i = 0; i < size; i++)
            testData[i] = (byte)((i * 7 + 13) % 256);
        
        var prefs = new LZ4Frame.FramePreferences { CompressionLevel = level };
        var compressed = new byte[LZ4Frame.CompressFrameBound(size, prefs)];
        int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, testData, size, prefs);
        
        Assert.True(compressedSize > 0, "Compression should succeed");
        
        using var inputStream = new MemoryStream(compressed, 0, compressedSize);
        using var decoder = LZ4Stream.Decode(inputStream);
        using var output = new MemoryStream();
        decoder.CopyTo(output);
        var result = output.ToArray();
        
        Assert.Equal(size, result.Length);
        Assert.True(testData.SequenceEqual(result), $"Decompressed data should match original (level={level}, size={size})");
    }

    [Theory]
    [InlineData(-1, LZ4Frame.BlockSize.Default, LZ4Frame.BlockMode.Linked, false)]
    [InlineData(-1, LZ4Frame.BlockSize.Max64KB, LZ4Frame.BlockMode.Independent, false)]
    [InlineData(-1, LZ4Frame.BlockSize.Max64KB, LZ4Frame.BlockMode.Linked, false)]
    [InlineData(-1, LZ4Frame.BlockSize.Max256KB, LZ4Frame.BlockMode.Independent, false)]
    [InlineData(-1, LZ4Frame.BlockSize.Max64KB, LZ4Frame.BlockMode.Independent, true)]
    [InlineData(-8, LZ4Frame.BlockSize.Max64KB, LZ4Frame.BlockMode.Independent, false)]
    public void LZ4SharpFrame_K4osDecompress_FrameOptions(int level, LZ4Frame.BlockSize blockSize, LZ4Frame.BlockMode blockMode, bool checksum)
    {
        int size = 100000;
        var testData = new byte[size];
        for (int i = 0; i < size; i++)
            testData[i] = (byte)((i * 7 + 13) % 256);
        
        var prefs = new LZ4Frame.FramePreferences
        {
            CompressionLevel = level,
            BlockSizeId = blockSize,
            BlockMode = blockMode,
            ContentChecksumFlag = checksum ? LZ4Frame.ContentChecksum.ChecksumEnabled : LZ4Frame.ContentChecksum.NoChecksum
        };
        
        var compressed = new byte[LZ4Frame.CompressFrameBound(size, prefs)];
        int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, testData, size, prefs);
        
        Assert.True(compressedSize > 0, "Compression should succeed");
        
        using var inputStream = new MemoryStream(compressed, 0, compressedSize);
        using var decoder = LZ4Stream.Decode(inputStream);
        using var output = new MemoryStream();
        decoder.CopyTo(output);
        var result = output.ToArray();
        
        Assert.Equal(size, result.Length);
        Assert.True(testData.SequenceEqual(result), "Decompressed data should match original");
    }
}

/// <summary>
/// Tests for LZ4Frame interoperability with the reference lz4 CLI tool.
/// Ensures frames are valid per the LZ4 frame specification.
/// </summary>
public class Lz4CliInteropTest
{
    private readonly ITestOutputHelper _output;
    public Lz4CliInteropTest(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(-1)]
    [InlineData(-8)]
    [InlineData(0)]
    [InlineData(9)]
    public void LZ4SharpFrame_Lz4CliDecompress(int level)
    {
        int size = 100000;
        var testData = new byte[size];
        for (int i = 0; i < size; i++)
            testData[i] = (byte)((i * 7 + 13) % 256);
        
        var prefs = new LZ4Frame.FramePreferences { CompressionLevel = level };
        var compressed = new byte[LZ4Frame.CompressFrameBound(size, prefs)];
        int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, testData, size, prefs);
        
        string tempCompressed = Path.GetTempFileName();
        string tempDecompressed = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempCompressed, compressed.Take(compressedSize).ToArray());
            
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "lz4",
                Arguments = $"-d -f {tempCompressed} {tempDecompressed}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            
            using var proc = System.Diagnostics.Process.Start(psi)!;
            proc.WaitForExit();
            
            Assert.Equal(0, proc.ExitCode);
            
            var decompressed = File.ReadAllBytes(tempDecompressed);
            Assert.Equal(size, decompressed.Length);
            Assert.True(testData.SequenceEqual(decompressed), "Decompressed data should match original");
        }
        finally
        {
            File.Delete(tempCompressed);
            if (File.Exists(tempDecompressed)) File.Delete(tempDecompressed);
        }
    }

    [Theory]
    [InlineData(LZ4Frame.BlockSize.Default)]
    [InlineData(LZ4Frame.BlockSize.Max64KB)]
    [InlineData(LZ4Frame.BlockSize.Max256KB)]
    [InlineData(LZ4Frame.BlockSize.Max1MB)]
    [InlineData(LZ4Frame.BlockSize.Max4MB)]
    public void LZ4SharpFrame_Lz4CliDecompress_AllBlockSizes(LZ4Frame.BlockSize blockSize)
    {
        var testData = new byte[1000];
        for (int i = 0; i < 1000; i++)
            testData[i] = (byte)((i * 7 + 13) % 256);

        var prefs = new LZ4Frame.FramePreferences { BlockSizeId = blockSize };
        var compressed = new byte[LZ4Frame.CompressFrameBound(1000, prefs)];
        int compressedSize = LZ4Frame.CompressFrame(compressed, compressed.Length, testData, 1000, prefs);

        // Verify BD byte has valid BlockMaxSize (4-7)
        int blockSizeValue = (compressed[5] >> 4) & 0x7;
        Assert.InRange(blockSizeValue, 4, 7);
        
        string tempCompressed = Path.GetTempFileName();
        string tempDecompressed = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempCompressed, compressed.Take(compressedSize).ToArray());
            
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "lz4",
                Arguments = $"-d -f {tempCompressed} {tempDecompressed}",
                RedirectStandardError = true,
                UseShellExecute = false
            };
            
            using var proc = System.Diagnostics.Process.Start(psi)!;
            proc.WaitForExit();
            
            Assert.Equal(0, proc.ExitCode);
            
            var decompressed = File.ReadAllBytes(tempDecompressed);
            Assert.Equal(testData, decompressed);
        }
        finally
        {
            File.Delete(tempCompressed);
            if (File.Exists(tempDecompressed)) File.Delete(tempDecompressed);
        }
    }
}
