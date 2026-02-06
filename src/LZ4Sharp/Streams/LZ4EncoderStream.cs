/*
 * LZ4Sharp Streaming API - Encoder Stream
 * MIT License - see LICENSE
 */

using System;
using System.Buffers;
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
            _outputBuffer = ArrayPool<byte>.Shared.Rent(LZ4Codec.CompressBound(_settings.BlockSize));
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

            while (buffer.Length > 0)
            {
                int bytesToCopy = Math.Min(buffer.Length, _settings.BlockSize - _inputBufferPos);
                buffer.Slice(0, bytesToCopy).CopyTo(_inputBuffer.AsSpan(_inputBufferPos));
                _inputBufferPos += bytesToCopy;
                buffer = buffer.Slice(bytesToCopy);

                if (_inputBufferPos >= _settings.BlockSize)
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

            // Compress the block
            int compressedSize = CompressBlock(_inputBuffer!, 0, _inputBufferPos, _outputBuffer!);

            // Determine if compression is beneficial
            bool useCompressed = compressedSize > 0 && compressedSize < _inputBufferPos;
            int blockDataSize = useCompressed ? compressedSize : _inputBufferPos;

            // Write block header (4 bytes, little-endian)
            uint blockHeader = (uint)blockDataSize;
            if (!useCompressed)
                blockHeader |= 0x80000000; // High bit = uncompressed

            Span<byte> headerBytes = stackalloc byte[4];
            headerBytes[0] = (byte)blockHeader;
            headerBytes[1] = (byte)(blockHeader >> 8);
            headerBytes[2] = (byte)(blockHeader >> 16);
            headerBytes[3] = (byte)(blockHeader >> 24);
            _innerStream.Write(headerBytes);

            // Write block data
            if (useCompressed)
            {
                _innerStream.Write(_outputBuffer.AsSpan(0, compressedSize));
            }
            else
            {
                _innerStream.Write(_inputBuffer.AsSpan(0, _inputBufferPos));
            }

            // Write block checksum if enabled
            if (_settings.BlockChecksum)
            {
                byte[] blockData = useCompressed ? _outputBuffer! : _inputBuffer!;
                int blockLen = useCompressed ? compressedSize : _inputBufferPos;
                uint checksum = XXHash.XXH32(blockData, blockLen, 0);

                Span<byte> checksumBytes = stackalloc byte[4];
                checksumBytes[0] = (byte)checksum;
                checksumBytes[1] = (byte)(checksum >> 8);
                checksumBytes[2] = (byte)(checksum >> 16);
                checksumBytes[3] = (byte)(checksum >> 24);
                _innerStream.Write(checksumBytes);
            }

            _inputBufferPos = 0;
        }

        private async ValueTask FlushBlockAsync(CancellationToken cancellationToken)
        {
            if (_inputBufferPos == 0) return;

            // Update content checksum
            if (_contentChecksumState != null)
            {
                XXHash.XXH32Update(_contentChecksumState, _inputBuffer.AsSpan(0, _inputBufferPos));
            }

            // Compress the block
            int compressedSize = CompressBlock(_inputBuffer!, 0, _inputBufferPos, _outputBuffer!);

            // Determine if compression is beneficial
            bool useCompressed = compressedSize > 0 && compressedSize < _inputBufferPos;
            int blockDataSize = useCompressed ? compressedSize : _inputBufferPos;

            // Write block header
            uint blockHeader = (uint)blockDataSize;
            if (!useCompressed)
                blockHeader |= 0x80000000;

            byte[] headerBytes = new byte[4];
            headerBytes[0] = (byte)blockHeader;
            headerBytes[1] = (byte)(blockHeader >> 8);
            headerBytes[2] = (byte)(blockHeader >> 16);
            headerBytes[3] = (byte)(blockHeader >> 24);
            await _innerStream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);

            // Write block data
            if (useCompressed)
            {
                await _innerStream.WriteAsync(_outputBuffer.AsMemory(0, compressedSize), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _innerStream.WriteAsync(_inputBuffer.AsMemory(0, _inputBufferPos), cancellationToken).ConfigureAwait(false);
            }

            // Write block checksum if enabled
            if (_settings.BlockChecksum)
            {
                byte[] blockData = useCompressed ? _outputBuffer! : _inputBuffer!;
                int blockLen = useCompressed ? compressedSize : _inputBufferPos;
                uint checksum = XXHash.XXH32(blockData, blockLen, 0);

                byte[] checksumBytes = new byte[4];
                checksumBytes[0] = (byte)checksum;
                checksumBytes[1] = (byte)(checksum >> 8);
                checksumBytes[2] = (byte)(checksum >> 16);
                checksumBytes[3] = (byte)(checksum >> 24);
                await _innerStream.WriteAsync(checksumBytes, cancellationToken).ConfigureAwait(false);
            }

            _inputBufferPos = 0;
        }

        private int CompressBlock(byte[] source, int sourceOffset, int sourceSize, byte[] dest)
        {
            int level = _settings.GetInternalCompressionLevel();

            if (level < 0)
            {
                // Fast compression
                int acceleration = -level;
                return LZ4Codec.CompressFast(source.AsSpan(sourceOffset, sourceSize), dest, acceleration);
            }
            else if (level >= LZ4HC.CLEVEL_MIN)
            {
                // HC compression
                return LZ4HC.CompressHC(source, dest, sourceSize, dest.Length, level);
            }
            else
            {
                // Default compression
                return LZ4Codec.CompressDefault(source, dest, sourceSize, dest.Length);
            }
        }

        private void WriteFrameHeader()
        {
            Span<byte> header = stackalloc byte[15]; // Max header size
            int pos = 0;

            // Magic number (4 bytes, little-endian)
            header[pos++] = (byte)(LZ4F_MAGICNUMBER & 0xFF);
            header[pos++] = (byte)((LZ4F_MAGICNUMBER >> 8) & 0xFF);
            header[pos++] = (byte)((LZ4F_MAGICNUMBER >> 16) & 0xFF);
            header[pos++] = (byte)((LZ4F_MAGICNUMBER >> 24) & 0xFF);

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
                header[pos++] = (byte)(dictId & 0xFF);
                header[pos++] = (byte)((dictId >> 8) & 0xFF);
                header[pos++] = (byte)((dictId >> 16) & 0xFF);
                header[pos++] = (byte)((dictId >> 24) & 0xFF);
                headerDataLen += 4;
            }

            // Header checksum (XXH32 of header data, bits 15-8)
            uint headerChecksum = XXHash.XXH32(header.Slice(headerDataStart, headerDataLen).ToArray(), headerDataLen, 0);
            header[pos++] = (byte)((headerChecksum >> 8) & 0xFF);

            _innerStream.Write(header.Slice(0, pos));
        }

        private void WriteFrameFooter()
        {
            // Write end mark (block size = 0)
            Span<byte> endMark = stackalloc byte[4];
            endMark[0] = 0;
            endMark[1] = 0;
            endMark[2] = 0;
            endMark[3] = 0;
            _innerStream.Write(endMark);

            // Write content checksum if enabled
            if (_settings.ContentChecksum && _contentChecksumState != null)
            {
                uint checksum = XXHash.XXH32Digest(_contentChecksumState);
                Span<byte> checksumBytes = stackalloc byte[4];
                checksumBytes[0] = (byte)checksum;
                checksumBytes[1] = (byte)(checksum >> 8);
                checksumBytes[2] = (byte)(checksum >> 16);
                checksumBytes[3] = (byte)(checksum >> 24);
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
