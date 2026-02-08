/*
 * LZ4Sharp Streaming API - Encoder Stream
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
    /// A stream that compresses data written to it using LZ4 frame format.
    /// </summary>
    public class LZ4EncoderStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly bool _leaveOpen;
        private readonly LZ4EncoderSettings _settings;

        private byte[]? _inputBuffer;
        private int _inputBufferPos;
        private byte[]? _outputBuffer;

        private bool _headerWritten;
        private bool _disposed;
        private XXHash.XXH32State? _contentChecksumState;
        private readonly byte[] _asyncHeaderBuf = new byte[4];

        // Frame format constants
        private const uint LZ4F_MAGICNUMBER = 0x184D2204;

        /// <summary>
        /// Creates a new LZ4 encoder stream.
        /// </summary>
        /// <param name="innerStream">The stream to write compressed data to.</param>
        /// <param name="settings">Encoder settings.</param>
        /// <param name="leaveOpen">Whether to leave the inner stream open when disposing.</param>
        public LZ4EncoderStream(Stream innerStream, LZ4EncoderSettings? settings = null, bool leaveOpen = false)
        {
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
            _settings = settings ?? new LZ4EncoderSettings();
            _leaveOpen = leaveOpen;

            _inputBuffer = ArrayPool<byte>.Shared.Rent(_settings.BlockSize);
            _outputBuffer = ArrayPool<byte>.Shared.Rent(LZ4Codec.CompressBound(_settings.BlockSize) + 4);
            _inputBufferPos = 0;
            _headerWritten = false;

            if (_settings.ContentChecksum)
            {
                _contentChecksumState = new XXHash.XXH32State();
                XXHash.XXH32Reset(_contentChecksumState, 0);
            }
        }

        /// <inheritdoc/>
        public override bool CanRead => false;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => !_disposed;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException("Cannot read from encoder stream");

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value)
            => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            if (_disposed) throw new ObjectDisposedException(nameof(LZ4EncoderStream));

            WriteCore(buffer.AsSpan(offset, count));
        }

        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LZ4EncoderStream));
            WriteCore(buffer);
        }

        private void WriteCore(ReadOnlySpan<byte> buffer)
        {
            if (!_headerWritten)
            {
                WriteFrameHeader();
                _headerWritten = true;
            }

            int blockSize = _settings.BlockSize;

            // Fast path: compress full blocks directly from user buffer (skip _inputBuffer copy)
            while (_inputBufferPos == 0 && buffer.Length >= blockSize)
            {
                FlushBlockDirect(buffer.Slice(0, blockSize));
                buffer = buffer.Slice(blockSize);
            }

            while (buffer.Length > 0)
            {
                int bytesToCopy = Math.Min(buffer.Length, blockSize - _inputBufferPos);
                buffer.Slice(0, bytesToCopy).CopyTo(_inputBuffer.AsSpan(_inputBufferPos));
                _inputBufferPos += bytesToCopy;
                buffer = buffer.Slice(bytesToCopy);

                if (_inputBufferPos >= blockSize)
                {
                    FlushBlock();
                }
            }
        }

        /// <inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return WriteAsyncCore(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        /// <inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return WriteAsyncCore(buffer, cancellationToken);
        }

        private async ValueTask WriteAsyncCore(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LZ4EncoderStream));

            if (!_headerWritten)
            {
                WriteFrameHeader();
                await _innerStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                _headerWritten = true;
            }

            while (buffer.Length > 0)
            {
                int bytesToCopy = Math.Min(buffer.Length, _settings.BlockSize - _inputBufferPos);
                buffer.Slice(0, bytesToCopy).Span.CopyTo(_inputBuffer.AsSpan(_inputBufferPos));
                _inputBufferPos += bytesToCopy;
                buffer = buffer.Slice(bytesToCopy);

                if (_inputBufferPos >= _settings.BlockSize)
                {
                    await FlushBlockAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            if (_disposed) return;
            if (_inputBufferPos > 0)
            {
                FlushBlock();
            }
            _innerStream.Flush();
        }

        /// <inheritdoc/>
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_disposed) return;
            if (_inputBufferPos > 0)
            {
                await FlushBlockAsync(cancellationToken).ConfigureAwait(false);
            }
            await _innerStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private void FlushBlock()
        {
            if (_inputBufferPos == 0) return;

            // Update content checksum
            if (_contentChecksumState != null)
            {
                XXHash.XXH32Update(_contentChecksumState, _inputBuffer.AsSpan(0, _inputBufferPos));
            }

            // Compress to offset 4, leaving room for block header
            int compressedSize = CompressBlock(_inputBuffer!, 0, _inputBufferPos, _outputBuffer!, 4);

            bool useCompressed = compressedSize > 0 && compressedSize < _inputBufferPos;
            int blockDataSize = useCompressed ? compressedSize : _inputBufferPos;

            // Write block header at offset 0
            uint blockHeader = (uint)blockDataSize;
            if (!useCompressed)
                blockHeader |= 0x80000000;

            BinaryPrimitives.WriteUInt32LittleEndian(_outputBuffer.AsSpan(), blockHeader);

            if (useCompressed)
            {
                // Single write: header (4) + compressed data
                _innerStream.Write(_outputBuffer.AsSpan(0, 4 + compressedSize));
            }
            else
            {
                // Header only from output buffer, then uncompressed from input buffer
                _innerStream.Write(_outputBuffer.AsSpan(0, 4));
                _innerStream.Write(_inputBuffer.AsSpan(0, _inputBufferPos));
            }

            // Write block checksum if enabled
            if (_settings.BlockChecksum)
            {
                byte[] blockData = useCompressed ? _outputBuffer! : _inputBuffer!;
                int blockOff = useCompressed ? 4 : 0;
                int blockLen = useCompressed ? compressedSize : _inputBufferPos;
                uint checksum = XXHash.XXH32((ReadOnlySpan<byte>)blockData.AsSpan(blockOff, blockLen), 0);

                Span<byte> checksumBytes = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(checksumBytes, checksum);
                _innerStream.Write(checksumBytes);
            }

            _inputBufferPos = 0;
        }

        /// <summary>
        /// Compress and write a full block directly from the source span, bypassing _inputBuffer.
        /// </summary>
        private void FlushBlockDirect(ReadOnlySpan<byte> source)
        {
            int blockSize = source.Length;

            if (_contentChecksumState != null)
            {
                XXHash.XXH32Update(_contentChecksumState, source);
            }

            int compressedSize = CompressBlock(source, _outputBuffer.AsSpan(4));

            bool useCompressed = compressedSize > 0 && compressedSize < blockSize;
            int blockDataSize = useCompressed ? compressedSize : blockSize;

            uint blockHeader = (uint)blockDataSize;
            if (!useCompressed)
                blockHeader |= 0x80000000;

            BinaryPrimitives.WriteUInt32LittleEndian(_outputBuffer.AsSpan(), blockHeader);

            if (useCompressed)
            {
                _innerStream.Write(_outputBuffer.AsSpan(0, 4 + compressedSize));
            }
            else
            {
                _innerStream.Write(_outputBuffer.AsSpan(0, 4));
                _innerStream.Write(source);
            }

            if (_settings.BlockChecksum)
            {
                ReadOnlySpan<byte> blockData = useCompressed
                    ? _outputBuffer.AsSpan(4, compressedSize)
                    : source;
                uint checksum = XXHash.XXH32(blockData, 0);

                Span<byte> checksumBytes = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(checksumBytes, checksum);
                _innerStream.Write(checksumBytes);
            }
        }

        private async ValueTask FlushBlockAsync(CancellationToken cancellationToken)
        {
            if (_inputBufferPos == 0) return;

            // Update content checksum
            if (_contentChecksumState != null)
            {
                XXHash.XXH32Update(_contentChecksumState, _inputBuffer.AsSpan(0, _inputBufferPos));
            }

            // Compress to offset 4, leaving room for block header
            int compressedSize = CompressBlock(_inputBuffer!, 0, _inputBufferPos, _outputBuffer!, 4);

            bool useCompressed = compressedSize > 0 && compressedSize < _inputBufferPos;
            int blockDataSize = useCompressed ? compressedSize : _inputBufferPos;

            // Write block header at offset 0
            uint blockHeader = (uint)blockDataSize;
            if (!useCompressed)
                blockHeader |= 0x80000000;

            BinaryPrimitives.WriteUInt32LittleEndian(_outputBuffer.AsSpan(), blockHeader);

            if (useCompressed)
            {
                // Single write: header (4) + compressed data
                await _innerStream.WriteAsync(_outputBuffer.AsMemory(0, 4 + compressedSize), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Header only from output buffer, then uncompressed from input buffer
                await _innerStream.WriteAsync(_outputBuffer.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
                await _innerStream.WriteAsync(_inputBuffer.AsMemory(0, _inputBufferPos), cancellationToken).ConfigureAwait(false);
            }

            // Write block checksum if enabled
            if (_settings.BlockChecksum)
            {
                byte[] blockData = useCompressed ? _outputBuffer! : _inputBuffer!;
                int blockOff = useCompressed ? 4 : 0;
                int blockLen = useCompressed ? compressedSize : _inputBufferPos;
                uint checksum = XXHash.XXH32((ReadOnlySpan<byte>)blockData.AsSpan(blockOff, blockLen), 0);

                BinaryPrimitives.WriteUInt32LittleEndian(_asyncHeaderBuf.AsSpan(), checksum);
                await _innerStream.WriteAsync(_asyncHeaderBuf, cancellationToken).ConfigureAwait(false);
            }

            _inputBufferPos = 0;
        }

        private int CompressBlock(byte[] source, int sourceOffset, int sourceSize, byte[] dest, int destOffset)
        {
            return CompressBlock(source.AsSpan(sourceOffset, sourceSize), dest.AsSpan(destOffset, dest.Length - destOffset));
        }

        private int CompressBlock(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            int level = _settings.GetInternalCompressionLevel();

            if (level < 0)
            {
                int acceleration = -level;
                return LZ4Codec.CompressFast(source, dest, acceleration);
            }
            else if (level >= LZ4HC.CLEVEL_MIN)
            {
                return LZ4HC.CompressHC(source, dest, level);
            }
            else
            {
                return LZ4Codec.CompressDefault(source, dest);
            }
        }

        private void WriteFrameHeader()
        {
            Span<byte> header = stackalloc byte[15]; // Max header size
            int pos = 0;

            // Magic number (4 bytes, little-endian)
            BinaryPrimitives.WriteUInt32LittleEndian(header, LZ4F_MAGICNUMBER);
            pos = 4;

            // FLG byte
            byte flg = 0x40; // Version 01
            if (!_settings.ChainBlocks)
                flg |= 0x20; // Block independence
            if (_settings.ContentChecksum)
                flg |= 0x04; // Content checksum
            if (_settings.ContentLength.HasValue)
                flg |= 0x08; // Content size present
            if (_settings.BlockChecksum)
                flg |= 0x10; // Block checksum
            header[pos++] = flg;

            // BD byte
            int blockSizeId = (int)_settings.GetBlockSizeId();
            byte bd = (byte)(blockSizeId << 4);
            header[pos++] = bd;

            int headerDataStart = 4; // After magic
            int headerDataLen = 2;   // FLG + BD

            // Content size (8 bytes, optional)
            if (_settings.ContentLength.HasValue)
            {
                long contentLen = _settings.ContentLength.Value;
                for (int i = 0; i < 8; i++)
                {
                    header[pos++] = (byte)(contentLen & 0xFF);
                    contentLen >>= 8;
                }
                headerDataLen += 8;
            }

            // Dictionary ID (4 bytes, optional)
            if (_settings.Dictionary.HasValue)
            {
                uint dictId = _settings.Dictionary.Value;
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(pos), dictId);
                pos += 4;
                headerDataLen += 4;
            }

            // Header checksum (XXH32 of header data, bits 15-8)
            uint headerChecksum = XXHash.XXH32(header.Slice(headerDataStart, headerDataLen), 0);
            header[pos++] = (byte)((headerChecksum >> 8) & 0xFF);

            _innerStream.Write(header.Slice(0, pos));
        }

        private void WriteFrameFooter()
        {
            // Write end mark (block size = 0)
            Span<byte> endMark = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(endMark, 0);
            _innerStream.Write(endMark);

            // Write content checksum if enabled
            if (_settings.ContentChecksum && _contentChecksumState != null)
            {
                uint checksum = XXHash.XXH32Digest(_contentChecksumState);
                Span<byte> checksumBytes = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(checksumBytes, checksum);
                _innerStream.Write(checksumBytes);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Flush any remaining data
                if (_inputBufferPos > 0)
                {
                    FlushBlock();
                }

                // Write frame footer
                if (_headerWritten)
                {
                    WriteFrameFooter();
                }

                _innerStream.Flush();

                if (!_leaveOpen)
                {
                    _innerStream.Dispose();
                }

                // Return rented arrays
                if (_inputBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_inputBuffer);
                    _inputBuffer = null;
                }
                if (_outputBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_outputBuffer);
                    _outputBuffer = null;
                }
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
