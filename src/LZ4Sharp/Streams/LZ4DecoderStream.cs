/*
 * LZ4Sharp Streaming API - Decoder Stream
 * MIT License - see LICENSE
 */

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LZ4Sharp.Streams
{
    /// <summary>
    /// A stream that decompresses LZ4 frame data read from it.
    /// </summary>
    public class LZ4DecoderStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly bool _leaveOpen;
        private readonly bool _interactive;

        private byte[]? _decompressedBuffer;
        private int _decompressedBufferPos;
        private int _decompressedBufferLen;

        private byte[]? _compressedBlockBuffer;

        private bool _headerRead;
        private bool _endOfFrame;
        private bool _disposed;
        private readonly byte[] _asyncHeaderBuf = new byte[4];
        private readonly byte[] _asyncFrameHeaderBuf = new byte[19];

        // Frame descriptor from header
        private bool _blockIndependence;
        private bool _blockChecksum;
        private bool _contentChecksum;
        private bool _contentSizePresent;
        private int _blockMaxSize;
        private long? _contentSize;
        private XXHash.XXH32State? _contentChecksumState;

        // Frame format constants
        private const uint LZ4F_MAGICNUMBER = 0x184D2204;

        /// <summary>
        /// Creates a new LZ4 decoder stream.
        /// </summary>
        /// <param name="innerStream">The stream to read compressed data from.</param>
        /// <param name="leaveOpen">Whether to leave the inner stream open when disposing.</param>
        /// <param name="interactive">If true, return data as soon as it's available (partial reads).</param>
        public LZ4DecoderStream(Stream innerStream, bool leaveOpen = false, bool interactive = false)
        {
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
            _leaveOpen = leaveOpen;
            _interactive = interactive;
            _headerRead = false;
            _endOfFrame = false;
        }

        /// <inheritdoc/>
        public override bool CanRead => !_disposed;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => _contentSize ?? throw new NotSupportedException("Content length not available in frame header");

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException("Cannot write to decoder stream");

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value)
            => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Flush() { }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            if (_disposed) throw new ObjectDisposedException(nameof(LZ4DecoderStream));

            return ReadCore(buffer.AsSpan(offset, count));
        }

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LZ4DecoderStream));
            return ReadCore(buffer);
        }

        private int ReadCore(Span<byte> buffer)
        {
            if (_endOfFrame) return 0;

            if (!_headerRead)
            {
                ReadFrameHeader();
                _headerRead = true;
            }

            int totalRead = 0;

            while (totalRead < buffer.Length && !_endOfFrame)
            {
                // First, consume any buffered decompressed data
                if (_decompressedBufferLen > _decompressedBufferPos)
                {
                    int available = _decompressedBufferLen - _decompressedBufferPos;
                    int toCopy = Math.Min(available, buffer.Length - totalRead);
                    _decompressedBuffer.AsSpan(_decompressedBufferPos, toCopy).CopyTo(buffer.Slice(totalRead));
                    _decompressedBufferPos += toCopy;
                    totalRead += toCopy;

                    if (_interactive && totalRead > 0)
                        return totalRead;

                    continue;
                }

                // If user buffer can hold a full block, decompress directly into it
                int remaining = buffer.Length - totalRead;
                if (remaining >= _blockMaxSize)
                {
                    int directLen = ReadNextBlockDirect(buffer.Slice(totalRead, _blockMaxSize));
                    if (directLen < 0)
                    {
                        _endOfFrame = true;
                        break;
                    }
                    totalRead += directLen;

                    if (_interactive && totalRead > 0)
                        return totalRead;

                    continue;
                }

                // Need to read and decompress into intermediate buffer
                if (!ReadNextBlock())
                {
                    _endOfFrame = true;
                    break;
                }
            }

            return totalRead;
        }

        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsyncCore(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ReadAsyncCore(buffer, cancellationToken);
        }

        private async ValueTask<int> ReadAsyncCore(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LZ4DecoderStream));
            if (_endOfFrame) return 0;

            if (!_headerRead)
            {
                await ReadFrameHeaderAsync(cancellationToken).ConfigureAwait(false);
                _headerRead = true;
            }

            int totalRead = 0;

            while (totalRead < buffer.Length && !_endOfFrame)
            {
                // First, consume any buffered decompressed data
                if (_decompressedBufferLen > _decompressedBufferPos)
                {
                    int available = _decompressedBufferLen - _decompressedBufferPos;
                    int toCopy = Math.Min(available, buffer.Length - totalRead);
                    _decompressedBuffer.AsMemory(_decompressedBufferPos, toCopy).CopyTo(buffer.Slice(totalRead));
                    _decompressedBufferPos += toCopy;
                    totalRead += toCopy;

                    if (_interactive && totalRead > 0)
                        return totalRead;

                    continue;
                }

                // Need to read and decompress another block
                if (!await ReadNextBlockAsync(cancellationToken).ConfigureAwait(false))
                {
                    _endOfFrame = true;
                    break;
                }
            }

            return totalRead;
        }

        private void ReadFrameHeader()
        {
            Span<byte> header = stackalloc byte[19]; // Max header size
            int pos = 0;

            // Read magic number (4 bytes)
            ReadExact(header.Slice(0, 4));
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
            if (magic != LZ4F_MAGICNUMBER)
                throw new InvalidDataException($"Invalid LZ4 frame magic number: 0x{magic:X8}");
            pos = 4;

            // Read FLG and BD bytes
            ReadExact(header.Slice(4, 2));
            byte flg = header[4];
            byte bd = header[5];
            pos = 6;

            // Parse FLG
            int version = (flg >> 6) & 0x03;
            if (version != 1)
                throw new InvalidDataException($"Unsupported LZ4 frame version: {version}");

            _blockIndependence = ((flg >> 5) & 1) == 1;
            _blockChecksum = ((flg >> 4) & 1) == 1;
            _contentSizePresent = ((flg >> 3) & 1) == 1;
            _contentChecksum = ((flg >> 2) & 1) == 1;

            // Parse BD
            int blockMaxSizeId = (bd >> 4) & 0x07;
            _blockMaxSize = blockMaxSizeId switch
            {
                4 => 64 * 1024,
                5 => 256 * 1024,
                6 => 1024 * 1024,
                7 => 4 * 1024 * 1024,
                _ => throw new InvalidDataException($"Invalid block max size ID: {blockMaxSizeId}")
            };

            // Read content size if present (8 bytes)
            if (_contentSizePresent)
            {
                ReadExact(header.Slice(pos, 8));
                _contentSize = 0;
                for (int i = 0; i < 8; i++)
                {
                    _contentSize |= (long)header[pos + i] << (i * 8);
                }
                pos += 8;
            }

            // Read header checksum (1 byte)
            ReadExact(header.Slice(pos, 1));
            byte storedHC = header[pos];

            // Verify header checksum
            int headerDataLen = _contentSizePresent ? 10 : 2;
            uint calculatedChecksum = XXHash.XXH32((ReadOnlySpan<byte>)header.Slice(4, headerDataLen), 0);
            if (((calculatedChecksum >> 8) & 0xFF) != storedHC)
                throw new InvalidDataException("Invalid header checksum");

            // Allocate buffers
            _decompressedBuffer = ArrayPool<byte>.Shared.Rent(_blockMaxSize);
            _compressedBlockBuffer = ArrayPool<byte>.Shared.Rent(_blockMaxSize + 16);
            _decompressedBufferPos = 0;
            _decompressedBufferLen = 0;

            if (_contentChecksum)
            {
                _contentChecksumState = new XXHash.XXH32State();
                XXHash.XXH32Reset(_contentChecksumState, 0);
            }
        }

        private async ValueTask ReadFrameHeaderAsync(CancellationToken cancellationToken)
        {
            byte[] header = _asyncFrameHeaderBuf;
            int pos = 0;

            // Read magic number
            await ReadExactAsync(header.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan());
            if (magic != LZ4F_MAGICNUMBER)
                throw new InvalidDataException($"Invalid LZ4 frame magic number: 0x{magic:X8}");
            pos = 4;

            // Read FLG and BD bytes
            await ReadExactAsync(header.AsMemory(4, 2), cancellationToken).ConfigureAwait(false);
            byte flg = header[4];
            byte bd = header[5];
            pos = 6;

            // Parse FLG
            int version = (flg >> 6) & 0x03;
            if (version != 1)
                throw new InvalidDataException($"Unsupported LZ4 frame version: {version}");

            _blockIndependence = ((flg >> 5) & 1) == 1;
            _blockChecksum = ((flg >> 4) & 1) == 1;
            _contentSizePresent = ((flg >> 3) & 1) == 1;
            _contentChecksum = ((flg >> 2) & 1) == 1;

            // Parse BD
            int blockMaxSizeId = (bd >> 4) & 0x07;
            _blockMaxSize = blockMaxSizeId switch
            {
                4 => 64 * 1024,
                5 => 256 * 1024,
                6 => 1024 * 1024,
                7 => 4 * 1024 * 1024,
                _ => throw new InvalidDataException($"Invalid block max size ID: {blockMaxSizeId}")
            };

            // Read content size if present
            if (_contentSizePresent)
            {
                await ReadExactAsync(header.AsMemory(pos, 8), cancellationToken).ConfigureAwait(false);
                _contentSize = 0;
                for (int i = 0; i < 8; i++)
                {
                    _contentSize |= (long)header[pos + i] << (i * 8);
                }
                pos += 8;
            }

            // Read header checksum
            await ReadExactAsync(header.AsMemory(pos, 1), cancellationToken).ConfigureAwait(false);
            byte storedHC = header[pos];

            // Verify header checksum
            int headerDataLen = _contentSizePresent ? 10 : 2;
            uint calculatedChecksum = XXHash.XXH32((ReadOnlySpan<byte>)header.AsSpan(4, headerDataLen), 0);
            if (((calculatedChecksum >> 8) & 0xFF) != storedHC)
                throw new InvalidDataException("Invalid header checksum");

            // Allocate buffers
            _decompressedBuffer = ArrayPool<byte>.Shared.Rent(_blockMaxSize);
            _compressedBlockBuffer = ArrayPool<byte>.Shared.Rent(_blockMaxSize + 16);
            _decompressedBufferPos = 0;
            _decompressedBufferLen = 0;

            if (_contentChecksum)
            {
                _contentChecksumState = new XXHash.XXH32State();
                XXHash.XXH32Reset(_contentChecksumState, 0);
            }
        }

        private bool ReadNextBlock()
        {
            // Read block header (4 bytes)
            Span<byte> blockHeader = stackalloc byte[4];
            if (!TryReadExact(blockHeader))
                throw new InvalidDataException("Unexpected end of stream while reading block header");

            uint blockHeaderValue = BinaryPrimitives.ReadUInt32LittleEndian(blockHeader);

            // Check for end mark
            if (blockHeaderValue == 0)
            {
                // Read and verify content checksum if present
                if (_contentChecksum)
                {
                    Span<byte> checksumBytes = stackalloc byte[4];
                    ReadExact(checksumBytes);
                    uint storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(checksumBytes);
                    uint calculatedChecksum = XXHash.XXH32Digest(_contentChecksumState!);
                    if (storedChecksum != calculatedChecksum)
                        throw new InvalidDataException("Content checksum mismatch");
                }
                return false;
            }

            bool isCompressed = (blockHeaderValue & 0x80000000) == 0;
            int blockSize = (int)(blockHeaderValue & 0x7FFFFFFF);

            if (blockSize > _blockMaxSize)
                throw new InvalidDataException($"Block size {blockSize} exceeds maximum {_blockMaxSize}");

            // Read block data
            ReadExact(_compressedBlockBuffer.AsSpan(0, blockSize));

            // Read and verify block checksum if present
            if (_blockChecksum)
            {
                Span<byte> checksumBytes = stackalloc byte[4];
                ReadExact(checksumBytes);
                uint storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(checksumBytes);
                uint calculatedChecksum = XXHash.XXH32(_compressedBlockBuffer!, blockSize, 0);
                if (storedChecksum != calculatedChecksum)
                    throw new InvalidDataException("Block checksum mismatch");
            }

            // Decompress or copy
            if (isCompressed)
            {
                int decompressedSize = LZ4Codec.DecompressSafe(
                    _compressedBlockBuffer.AsSpan(0, blockSize),
                    _decompressedBuffer.AsSpan(0, _blockMaxSize));
                if (decompressedSize < 0)
                    throw new InvalidDataException("Decompression failed");
                _decompressedBufferLen = decompressedSize;
            }
            else
            {
                // Uncompressed block
                _compressedBlockBuffer.AsSpan(0, blockSize).CopyTo(_decompressedBuffer);
                _decompressedBufferLen = blockSize;
            }

            _decompressedBufferPos = 0;

            // Update content checksum
            if (_contentChecksumState != null)
            {
                XXHash.XXH32Update(_contentChecksumState, _decompressedBuffer.AsSpan(0, _decompressedBufferLen));
            }

            return true;
        }

        /// <summary>
        /// Read and decompress next block directly into target span, bypassing _decompressedBuffer.
        /// Returns decompressed byte count, or -1 on end of frame.
        /// </summary>
        private int ReadNextBlockDirect(Span<byte> target)
        {
            Span<byte> blockHeader = stackalloc byte[4];
            if (!TryReadExact(blockHeader))
                throw new InvalidDataException("Unexpected end of stream while reading block header");

            uint blockHeaderValue = BinaryPrimitives.ReadUInt32LittleEndian(blockHeader);

            if (blockHeaderValue == 0)
            {
                if (_contentChecksum)
                {
                    Span<byte> checksumBytes = stackalloc byte[4];
                    ReadExact(checksumBytes);
                    uint storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(checksumBytes);
                    uint calculatedChecksum = XXHash.XXH32Digest(_contentChecksumState!);
                    if (storedChecksum != calculatedChecksum)
                        throw new InvalidDataException("Content checksum mismatch");
                }
                return -1;
            }

            bool isCompressed = (blockHeaderValue & 0x80000000) == 0;
            int blockSize = (int)(blockHeaderValue & 0x7FFFFFFF);

            if (blockSize > _blockMaxSize)
                throw new InvalidDataException($"Block size {blockSize} exceeds maximum {_blockMaxSize}");

            ReadExact(_compressedBlockBuffer.AsSpan(0, blockSize));

            if (_blockChecksum)
            {
                Span<byte> checksumBytes = stackalloc byte[4];
                ReadExact(checksumBytes);
                uint storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(checksumBytes);
                uint calculatedChecksum = XXHash.XXH32(_compressedBlockBuffer!, blockSize, 0);
                if (storedChecksum != calculatedChecksum)
                    throw new InvalidDataException("Block checksum mismatch");
            }

            int decompressedSize;
            if (isCompressed)
            {
                decompressedSize = LZ4Codec.DecompressSafe(
                    _compressedBlockBuffer.AsSpan(0, blockSize), target);
                if (decompressedSize < 0)
                    throw new InvalidDataException("Decompression failed");
            }
            else
            {
                _compressedBlockBuffer.AsSpan(0, blockSize).CopyTo(target);
                decompressedSize = blockSize;
            }

            // Ensure intermediate buffer is marked empty
            _decompressedBufferPos = 0;
            _decompressedBufferLen = 0;

            if (_contentChecksumState != null)
            {
                XXHash.XXH32Update(_contentChecksumState, target.Slice(0, decompressedSize));
            }

            return decompressedSize;
        }

        private async ValueTask<bool> ReadNextBlockAsync(CancellationToken cancellationToken)
        {
            // Read block header
            int read = await _innerStream.ReadAsync(_asyncHeaderBuf, cancellationToken).ConfigureAwait(false);
            if (read == 0) return false;
            if (read < 4)
            {
                await ReadExactAsync(_asyncHeaderBuf.AsMemory(read, 4 - read), cancellationToken).ConfigureAwait(false);
            }

            uint blockHeaderValue = BinaryPrimitives.ReadUInt32LittleEndian(_asyncHeaderBuf.AsSpan());

            // Check for end mark
            if (blockHeaderValue == 0)
            {
                if (_contentChecksum)
                {
                    await ReadExactAsync(_asyncHeaderBuf.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
                    uint storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(_asyncHeaderBuf.AsSpan());
                    uint calculatedChecksum = XXHash.XXH32Digest(_contentChecksumState!);
                    if (storedChecksum != calculatedChecksum)
                        throw new InvalidDataException("Content checksum mismatch");
                }
                return false;
            }

            bool isCompressed = (blockHeaderValue & 0x80000000) == 0;
            int blockSize = (int)(blockHeaderValue & 0x7FFFFFFF);

            if (blockSize > _blockMaxSize)
                throw new InvalidDataException($"Block size {blockSize} exceeds maximum {_blockMaxSize}");

            // Read block data
            await ReadExactAsync(_compressedBlockBuffer.AsMemory(0, blockSize), cancellationToken).ConfigureAwait(false);

            // Read and verify block checksum if present
            if (_blockChecksum)
            {
                await ReadExactAsync(_asyncHeaderBuf.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
                uint storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(_asyncHeaderBuf.AsSpan());
                uint calculatedChecksum = XXHash.XXH32(_compressedBlockBuffer!, blockSize, 0);
                if (storedChecksum != calculatedChecksum)
                    throw new InvalidDataException("Block checksum mismatch");
            }

            // Decompress or copy
            if (isCompressed)
            {
                int decompressedSize = LZ4Codec.DecompressSafe(
                    _compressedBlockBuffer.AsSpan(0, blockSize),
                    _decompressedBuffer.AsSpan(0, _blockMaxSize));
                if (decompressedSize < 0)
                    throw new InvalidDataException("Decompression failed");
                _decompressedBufferLen = decompressedSize;
            }
            else
            {
                _compressedBlockBuffer.AsSpan(0, blockSize).CopyTo(_decompressedBuffer);
                _decompressedBufferLen = blockSize;
            }

            _decompressedBufferPos = 0;

            if (_contentChecksumState != null)
            {
                XXHash.XXH32Update(_contentChecksumState, _decompressedBuffer.AsSpan(0, _decompressedBufferLen));
            }

            return true;
        }

        private void ReadExact(Span<byte> buffer)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = _innerStream.Read(buffer.Slice(totalRead));
                if (read == 0)
                    throw new InvalidDataException("Unexpected end of stream");
                totalRead += read;
            }
        }

        private bool TryReadExact(Span<byte> buffer)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = _innerStream.Read(buffer.Slice(totalRead));
                if (read == 0)
                    return totalRead == 0 ? false : throw new InvalidDataException("Unexpected end of stream");
                totalRead += read;
            }
            return true;
        }

        private async ValueTask ReadExactAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = await _innerStream.ReadAsync(buffer.Slice(totalRead), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    throw new InvalidDataException("Unexpected end of stream");
                totalRead += read;
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                if (!_leaveOpen)
                {
                    _innerStream.Dispose();
                }

                if (_decompressedBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_decompressedBuffer);
                    _decompressedBuffer = null;
                }
                if (_compressedBlockBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_compressedBlockBuffer);
                    _compressedBlockBuffer = null;
                }
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
