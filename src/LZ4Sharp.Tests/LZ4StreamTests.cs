using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using LZ4Sharp.Streams;
using Xunit;
using Xunit.Abstractions;

namespace LZ4Sharp.Tests;

/// <summary>
/// Tests for LZ4Sharp streaming API.
/// </summary>
public class LZ4StreamTests
{
    private readonly ITestOutputHelper _output;
    public LZ4StreamTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void RoundTrip_SimpleString()
    {
        var original = Encoding.UTF8.GetBytes("Hello, World! This is a test of the streaming API.");
        
        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        _output.WriteLine($"Original: {original.Length}, Compressed: {compressed.Length}");
        
        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void RoundTrip_LargeData()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Hello World! ", 10000)));
        
        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        _output.WriteLine($"Original: {original.Length}, Compressed: {compressed.Length}");
        
        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }

    /// <summary>
    /// Regression test: WildCopy8 tail handler after SIMD copies must handle remainders > 8 bytes.
    /// A 270-byte payload produces a 75-byte literal run; after one 64-byte AVX-512 copy,
    /// 11 bytes remain and all must be copied. Previously only the last 8 were written.
    /// </summary>
    [Theory]
    [InlineData(LZ4CompressionLevel.Fast)]
    [InlineData(LZ4CompressionLevel.Level0)]
    public void RoundTrip_SmallPayload_CopyToPattern(LZ4CompressionLevel level)
    {
        var original = Encoding.UTF8.GetBytes(
            "This is test data for LZ4 compression. " +
            "It should be long enough to actually compress. " +
            "Repeated patterns help: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa " +
            "More repeated patterns: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb " +
            "And some JSON-like content: {\"key\": \"value\", \"number\": 12345}");

        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, level, leaveOpen: true))
        {
            using var input = new MemoryStream(original);
            input.CopyTo(encoder);
        }

        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var output = new MemoryStream();
        decoder.CopyTo(output);

        Assert.Equal(original, output.ToArray());
    }

    [Fact]
    public void RoundTrip_MultiBlock()
    {
        // Create data larger than default block size (64KB)
        var original = new byte[200_000];
        new Random(42).NextBytes(original);
        
        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        _output.WriteLine($"Original: {original.Length}, Compressed: {compressed.Length}");
        
        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }

    [Theory]
    [InlineData(LZ4CompressionLevel.Fast)]
    [InlineData(LZ4CompressionLevel.Level0)]
    [InlineData(LZ4CompressionLevel.HC3)]
    [InlineData(LZ4CompressionLevel.HC9)]
    [InlineData(LZ4CompressionLevel.Max)]
    public void RoundTrip_AllCompressionLevels(LZ4CompressionLevel level)
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Test data for compression. ", 1000)));
        
        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, level, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        _output.WriteLine($"Level {level}: Original={original.Length}, Compressed={compressed.Length}");
        
        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void RoundTrip_WithContentChecksum()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Checksum test! ", 1000)));
        
        var settings = new Streams.LZ4EncoderSettings
        {
            CompressionLevel = LZ4CompressionLevel.Fast,
            ContentChecksum = true
        };
        
        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, settings, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        _output.WriteLine($"With checksum - Original: {original.Length}, Compressed: {compressed.Length}");
        
        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void RoundTrip_IncrementalWrite()
    {
        var chunks = Enumerable.Range(0, 100)
            .Select(i => Encoding.UTF8.GetBytes($"Chunk {i}: Some data here\n"))
            .ToList();
        
        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            foreach (var chunk in chunks)
            {
                encoder.Write(chunk);
            }
        }
        
        var expected = chunks.SelectMany(c => c).ToArray();
        _output.WriteLine($"Incremental - Original: {expected.Length}, Compressed: {compressed.Length}");
        
        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(expected, result.ToArray());
    }

    [Fact]
    public void RoundTrip_IncrementalRead()
    {
        var original = new byte[50000];
        new Random(42).NextBytes(original);
        
        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        
        var result = new byte[original.Length];
        int totalRead = 0;
        var buffer = new byte[1024];
        int bytesRead;
        
        while ((bytesRead = decoder.Read(buffer, 0, buffer.Length)) > 0)
        {
            Array.Copy(buffer, 0, result, totalRead, bytesRead);
            totalRead += bytesRead;
        }
        
        Assert.Equal(original.Length, totalRead);
        Assert.Equal(original, result);
    }

    [Fact]
    public async Task RoundTrip_Async()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Async test data! ", 1000)));
        
        using var compressed = new MemoryStream();
        await using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            await encoder.WriteAsync(original);
        }
        
        _output.WriteLine($"Async - Original: {original.Length}, Compressed: {compressed.Length}");
        
        compressed.Position = 0;
        await using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        await decoder.CopyToAsync(result);
        
        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void StreamCopyPattern()
    {
        var original = new byte[100000];
        new Random(42).NextBytes(original);
        
        using var source = new MemoryStream(original);
        using var compressed = new MemoryStream();
        
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            source.CopyTo(encoder);
        }
        
        _output.WriteLine($"Stream copy - Original: {original.Length}, Compressed: {compressed.Length}");
        
        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }
}

/// <summary>
/// Tests for interoperability between LZ4Sharp streaming API and K4os.
/// </summary>
public class LZ4StreamK4osInteropTests
{
    private readonly ITestOutputHelper _output;
    public LZ4StreamK4osInteropTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void LZ4Sharp_Encode_K4os_Decode()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("LZ4Sharp to K4os! ", 1000)));
        
        // Compress with LZ4Sharp
        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        _output.WriteLine($"LZ4Sharp compressed: {compressed.Length} bytes");
        
        // Decompress with K4os
        compressed.Position = 0;
        using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void K4os_Encode_LZ4Sharp_Decode()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("K4os to LZ4Sharp! ", 1000)));
        
        // Compress with K4os
        using var compressed = new MemoryStream();
        using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(compressed, LZ4Level.L00_FAST, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        _output.WriteLine($"K4os compressed: {compressed.Length} bytes");
        
        // Decompress with LZ4Sharp
        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void LZ4Sharp_Encode_K4os_Decode_LargeData()
    {
        var original = new byte[500_000];
        new Random(42).NextBytes(original);
        
        // Compress with LZ4Sharp
        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        _output.WriteLine($"Large data - Original: {original.Length}, Compressed: {compressed.Length}");
        
        // Decompress with K4os
        compressed.Position = 0;
        using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void K4os_Encode_LZ4Sharp_Decode_LargeData()
    {
        var original = new byte[500_000];
        new Random(42).NextBytes(original);
        
        // Compress with K4os
        using var compressed = new MemoryStream();
        using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(compressed, LZ4Level.L00_FAST, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        _output.WriteLine($"Large data - Original: {original.Length}, Compressed: {compressed.Length}");
        
        // Decompress with LZ4Sharp
        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }

    [Theory]
    [InlineData(LZ4CompressionLevel.Fast)]
    [InlineData(LZ4CompressionLevel.HC3)]
    [InlineData(LZ4CompressionLevel.HC9)]
    public void LZ4Sharp_Encode_K4os_Decode_AllLevels(LZ4CompressionLevel level)
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Testing all levels. ", 1000)));
        
        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, level, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        _output.WriteLine($"Level {level}: {compressed.Length} bytes");
        
        compressed.Position = 0;
        using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void LZ4Sharp_Encode_K4os_Decode_WithChecksum()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Checksum interop! ", 1000)));
        
        var settings = new Streams.LZ4EncoderSettings
        {
            CompressionLevel = LZ4CompressionLevel.Fast,
            ContentChecksum = true
        };
        
        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, settings, leaveOpen: true))
        {
            encoder.Write(original);
        }
        
        compressed.Position = 0;
        using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);
        
        Assert.Equal(original, result.ToArray());
    }

    [Theory]
    [InlineData(LZ4Level.L00_FAST)]
    [InlineData(LZ4Level.L03_HC)]
    [InlineData(LZ4Level.L09_HC)]
    [InlineData(LZ4Level.L12_MAX)]
    public void K4os_Encode_LZ4Sharp_Decode_AllLevels(LZ4Level level)
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Testing all levels reverse. ", 1000)));

        using var compressed = new MemoryStream();
        using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(compressed, level, leaveOpen: true))
        {
            encoder.Write(original);
        }

        _output.WriteLine($"K4os Level {level}: {compressed.Length} bytes");

        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void K4os_Encode_LZ4Sharp_Decode_WithContentChecksum()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("K4os checksum interop! ", 1000)));

        var settings = new K4os.Compression.LZ4.Streams.LZ4EncoderSettings
        {
            ContentChecksum = true
        };

        using var compressed = new MemoryStream();
        using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(compressed, settings, leaveOpen: true))
        {
            encoder.Write(original);
        }

        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void LZ4Sharp_Encode_K4os_Decode_WithBlockChecksum()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Block checksum test! ", 1000)));

        var settings = new Streams.LZ4EncoderSettings
        {
            CompressionLevel = LZ4CompressionLevel.Fast,
            BlockChecksum = true
        };

        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, settings, leaveOpen: true))
        {
            encoder.Write(original);
        }

        compressed.Position = 0;
        using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void K4os_Encode_LZ4Sharp_Decode_WithBlockChecksum()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("K4os block checksum! ", 1000)));

        var settings = new K4os.Compression.LZ4.Streams.LZ4EncoderSettings
        {
            BlockChecksum = true
        };

        using var compressed = new MemoryStream();
        using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(compressed, settings, leaveOpen: true))
        {
            encoder.Write(original);
        }

        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public void LZ4Sharp_Encode_K4os_Decode_EmptyData()
    {
        var original = Array.Empty<byte>();

        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            encoder.Write(original);
        }

        compressed.Position = 0;
        using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Empty(result.ToArray());
    }

    [Fact]
    public void K4os_Encode_LZ4Sharp_Decode_EmptyData()
    {
        var original = Array.Empty<byte>();

        using var compressed = new MemoryStream();
        using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(compressed, LZ4Level.L00_FAST, leaveOpen: true))
        {
            encoder.Write(original);
        }

        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Empty(result.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void LZ4Sharp_Encode_K4os_Decode_SingleByte(byte value)
    {
        var original = new byte[] { value };

        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            encoder.Write(original);
        }

        compressed.Position = 0;
        using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void K4os_Encode_LZ4Sharp_Decode_SingleByte(byte value)
    {
        var original = new byte[] { value };

        using var compressed = new MemoryStream();
        using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(compressed, LZ4Level.L00_FAST, leaveOpen: true))
        {
            encoder.Write(original);
        }

        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public async Task LZ4Sharp_Encode_K4os_Decode_Async()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Async interop! ", 1000)));

        using var compressed = new MemoryStream();
        await using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            await encoder.WriteAsync(original);
        }

        compressed.Position = 0;
        await using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        await decoder.CopyToAsync(result);

        Assert.Equal(original, result.ToArray());
    }

    [Fact]
    public async Task K4os_Encode_LZ4Sharp_Decode_Async()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Async interop reverse! ", 1000)));

        using var compressed = new MemoryStream();
        await using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(compressed, LZ4Level.L00_FAST, leaveOpen: true))
        {
            await encoder.WriteAsync(original);
        }

        compressed.Position = 0;
        await using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        await decoder.CopyToAsync(result);

        Assert.Equal(original, result.ToArray());
    }

    [Theory]
    [InlineData(256 * 1024)]
    [InlineData(1024 * 1024)]
    [InlineData(4 * 1024 * 1024)]
    public void LZ4Sharp_Encode_K4os_Decode_BlockSizes(int blockSize)
    {
        // Use data larger than default 64KB block to exercise multi-block
        var original = new byte[200_000];
        new Random(42).NextBytes(original);

        var settings = new Streams.LZ4EncoderSettings
        {
            CompressionLevel = LZ4CompressionLevel.Fast,
            BlockSize = blockSize
        };

        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, settings, leaveOpen: true))
        {
            encoder.Write(original);
        }

        _output.WriteLine($"BlockSize {blockSize}: {compressed.Length} bytes");

        compressed.Position = 0;
        using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    [Theory]
    [InlineData(256 * 1024)]
    [InlineData(1024 * 1024)]
    [InlineData(4 * 1024 * 1024)]
    public void K4os_Encode_LZ4Sharp_Decode_BlockSizes(int blockSize)
    {
        var original = new byte[200_000];
        new Random(42).NextBytes(original);

        var settings = new K4os.Compression.LZ4.Streams.LZ4EncoderSettings
        {
            BlockSize = blockSize
        };

        using var compressed = new MemoryStream();
        using (var encoder = K4os.Compression.LZ4.Streams.LZ4Stream.Encode(compressed, settings, leaveOpen: true))
        {
            encoder.Write(original);
        }

        _output.WriteLine($"K4os BlockSize {blockSize}: {compressed.Length} bytes");

        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    /// <summary>
    /// Regression: decompression silently corrupted data at back-reference boundaries
    /// due to an invalid tail optimization in the small-offset match copy path.
    /// </summary>
    [Theory]
    [InlineData(LZ4CompressionLevel.Fast)]
    [InlineData(LZ4CompressionLevel.Level0)]
    [InlineData(LZ4CompressionLevel.HC9)]
    public void RoundTrip_JsonWithRepeatedChars_NoCorruption(LZ4CompressionLevel level)
    {
        var original = Encoding.UTF8.GetBytes(
            "{\"id\":0,\"type\":\"message\"," +
            "\"content\":\"This is message content number 0 with some repeated text aaaaaaaaaaaaa\"," +
            "\"timestamp\":\"2026-01-01T00:00:00.000Z\"," +
            "\"metadata\":{\"key1\":\"value1\",\"key2\":\"value2\"}}");

        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, level, leaveOpen: true))
        {
            encoder.Write(original);
        }

        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    /// <summary>
    /// Tests round-trip with payloads designed to trigger small-offset matches
    /// (offsets 1-7) with varying tail sizes in the match copy.
    /// </summary>
    [Theory]
    [InlineData(1, 50)]   // offset=1 RLE
    [InlineData(2, 50)]
    [InlineData(3, 50)]
    [InlineData(4, 50)]   // Dec64Table[4] regression
    [InlineData(5, 50)]
    [InlineData(6, 50)]
    [InlineData(7, 50)]
    [InlineData(1, 200)]  // large RLE
    [InlineData(4, 200)]
    public void RoundTrip_SmallOffsetPatterns(int period, int totalLen)
    {
        var original = new byte[totalLen];
        for (int i = 0; i < totalLen; i++)
            original[i] = (byte)(0x41 + (i % period));

        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            encoder.Write(original);
        }

        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }

    /// <summary>
    /// Verify that decoding an empty stream does not throw but produces zero bytes.
    /// </summary>
    [Fact]
    public void Decode_EmptyInput_ProducesZeroBytes()
    {
        using var empty = new MemoryStream(Array.Empty<byte>());
        using var decoder = Streams.LZ4Stream.Decode(empty);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Empty(result.ToArray());
    }

    /// <summary>
    /// Stress test: 1000 repeated JSON messages compressed and decompressed via stream.
    /// </summary>
    [Fact]
    public void RoundTrip_RepeatedJsonMessages_1000()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 1000; i++)
        {
            sb.Append($"{{\"id\":{i},\"type\":\"message\",\"content\":\"" +
                      $"This is message content number {i} with some repeated text " +
                      new string('a', 10 + (i % 20)) + "\"}}\n");
        }
        var original = Encoding.UTF8.GetBytes(sb.ToString());

        using var compressed = new MemoryStream();
        using (var encoder = Streams.LZ4Stream.Encode(compressed, LZ4CompressionLevel.Fast, leaveOpen: true))
        {
            using var input = new MemoryStream(original);
            input.CopyTo(encoder);
        }

        _output.WriteLine($"Original: {original.Length}, Compressed: {compressed.Length}");

        compressed.Position = 0;
        using var decoder = Streams.LZ4Stream.Decode(compressed);
        using var result = new MemoryStream();
        decoder.CopyTo(result);

        Assert.Equal(original, result.ToArray());
    }
}
