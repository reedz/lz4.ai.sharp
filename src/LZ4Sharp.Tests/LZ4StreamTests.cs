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
}
