/*
 * LZ4 Frame - LZ4 Frame Format Support (C# port)
 * Derived from upstream LZ4: https://github.com/lz4/lz4
 *
 * This repository is MIT-licensed; see LICENSE.
 * Third-party BSD-2-Clause notices for upstream: see THIRD-PARTY-NOTICES.md.
 */

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace LZ4Sharp
{
    /// <summary>
    /// LZ4 Frame Format Support
    /// 
    /// Fully translated from lz4frame.c - implements LZ4 frame specification
    /// for creating and decoding LZ4 frames compatible with the lz4 CLI tool.
    /// </summary>
    public static class LZ4Frame
    {
        // Magic number for LZ4 frames
        private const uint LZ4F_MAGICNUMBER = 0x184D2204;
        private const int LZ4F_MAGIC_SIZE = 4;
        private const int LZ4F_HEADERSIZE_MIN = 7;
        private const int LZ4F_HEADERSIZE_MAX = 19;
        private const int LZ4F_BLOCK_HEADER_SIZE = 4;
        private const int LZ4F_BLOCK_CHECKSUM_SIZE = 4;
        private const int LZ4F_CONTENT_CHECKSUM_SIZE = 4;

        // Block sizes
        public enum BlockSize
        {
            Default = 0,
            Max64KB = 4,
            Max256KB = 5,
            Max1MB = 6,
            Max4MB = 7
        }

        // Block mode
        public enum BlockMode
        {
            Linked = 0,
            Independent = 1
        }

        // Content checksum
        public enum ContentChecksum
        {
            NoChecksum = 0,
            ChecksumEnabled = 1
        }

        // Frame preferences
        public class FramePreferences
        {
            public BlockSize BlockSizeId { get; set; } = BlockSize.Default;
            public BlockMode BlockMode { get; set; } = BlockMode.Linked;
            public ContentChecksum ContentChecksumFlag { get; set; } = ContentChecksum.NoChecksum;
            /// <summary>
            /// Compression level. Negative values use fast compression (CompressFast) where the
            /// magnitude is the acceleration factor (-1 = accel 1, -2 = accel 2, etc.).
            /// Values 0-2 use HC compression at minimum level. Values 3+ use HC at specified level.
            /// </summary>
            public int CompressionLevel { get; set; } = 0;
            public bool AutoFlush { get; set; } = false;

            public int GetBlockSize()
            {
                switch (BlockSizeId)
                {
                    case BlockSize.Max64KB: return 64 * 1024;
                    case BlockSize.Max256KB: return 256 * 1024;
                    case BlockSize.Max1MB: return 1024 * 1024;
                    case BlockSize.Max4MB: return 4 * 1024 * 1024;
                    default: return 64 * 1024; // Default to 64KB
                }
            }
        }

        /// <summary>
        /// Get the maximum compressed frame size for a given input size
        /// </summary>
        public static int CompressFrameBound(int sourceSize, FramePreferences? prefs = null)
        {
            prefs ??= new FramePreferences();
            int blockSize = prefs.GetBlockSize();
            int nbBlocks = (sourceSize + blockSize - 1) / blockSize;
            
            int headerSize = LZ4F_HEADERSIZE_MAX;
            int blockHeaderSize = nbBlocks * LZ4F_BLOCK_HEADER_SIZE;
            int blockChecksumSize = prefs.ContentChecksumFlag == ContentChecksum.ChecksumEnabled ? nbBlocks * LZ4F_BLOCK_CHECKSUM_SIZE : 0;
            int contentChecksumSize = prefs.ContentChecksumFlag == ContentChecksum.ChecksumEnabled ? LZ4F_CONTENT_CHECKSUM_SIZE : 0;
            int endMarkSize = LZ4F_BLOCK_HEADER_SIZE;

            int compressedSize = sourceSize + (sourceSize / 255) + (nbBlocks * 16);

            return headerSize + blockHeaderSize + compressedSize + blockChecksumSize + endMarkSize + contentChecksumSize;
        }

        /// <summary>
        /// Compress data into LZ4 frame format
        /// </summary>
        public static int CompressFrame(byte[] destination, int maxDestinationSize, byte[] source, int sourceSize, FramePreferences? prefs = null)
        {
            if (source == null || destination == null || sourceSize <= 0 || maxDestinationSize <= 0)
                return -1;

            prefs ??= new FramePreferences();
            int dstPos = 0;

            // Write frame header
            int headerSize = WriteFrameHeader(destination, dstPos, maxDestinationSize, prefs);
            if (headerSize < 0) return -1;
            dstPos += headerSize;

            // Compress data in blocks
            int srcPos = 0;
            int blockSize = prefs.GetBlockSize();

            int scratchSize = Math.Min(blockSize, sourceSize);
            byte[] compressedBlock = ArrayPool<byte>.Shared.Rent(LZ4Codec.CompressBound(scratchSize));

            try
            {
                while (srcPos < sourceSize)
                {
                    int currentBlockSize = Math.Min(blockSize, sourceSize - srcPos);

                    int maxCompressedSize = LZ4Codec.CompressBound(currentBlockSize);

                    // Compress directly from source at offset (no temp copy)
                    var srcSlice = source.AsSpan(srcPos, currentBlockSize);
                    int compressedSize;
                    if (prefs.CompressionLevel < 0)
                    {
                        int acceleration = -prefs.CompressionLevel;
                        compressedSize = LZ4Codec.CompressFast(srcSlice, compressedBlock, acceleration);
                    }
                    else if (prefs.CompressionLevel >= LZ4HC.CLEVEL_MIN)
                    {
                        compressedSize = LZ4HC.CompressHC(srcSlice, compressedBlock.AsSpan(0, maxCompressedSize), prefs.CompressionLevel);
                    }
                    else
                    {
                        compressedSize = LZ4Codec.CompressDefault(srcSlice, compressedBlock.AsSpan(0, maxCompressedSize));
                    }

                if (compressedSize <= 0)
                    return -1;

                // Check if compression is beneficial
                bool useCompressed = compressedSize < currentBlockSize;
                int blockDataSize = useCompressed ? compressedSize : currentBlockSize;

                // Write block header
                if (dstPos + 4 + blockDataSize > maxDestinationSize)
                    return -1;

                uint blockHeader = (uint)blockDataSize;
                if (!useCompressed)
                    blockHeader |= 0x80000000; // Set high bit for uncompressed

                BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(dstPos), blockHeader);
                dstPos += 4;

                // Write block data
                if (useCompressed)
                {
                    Array.Copy(compressedBlock, 0, destination, dstPos, compressedSize);
                    dstPos += compressedSize;
                }
                else
                {
                    Array.Copy(source, srcPos, destination, dstPos, currentBlockSize);
                    dstPos += currentBlockSize;
                }

                srcPos += currentBlockSize;
            }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(compressedBlock);
            }

            // Write end mark (block size = 0)
            if (dstPos + 4 > maxDestinationSize)
                return -1;

            BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(dstPos), 0);
            dstPos += 4;

            // Write content checksum if enabled
            if (prefs.ContentChecksumFlag == ContentChecksum.ChecksumEnabled)
            {
                if (dstPos + 4 > maxDestinationSize)
                    return -1;

                // Calculate checksum over entire source
                uint contentChecksum = XXHash.XXH32(source, 0);

                BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(dstPos), contentChecksum);
                dstPos += 4;
            }

            return dstPos;
        }

        private static int WriteFrameHeader(byte[] destination, int offset, int maxSize, FramePreferences prefs)
            => WriteFrameHeader(destination.AsSpan(offset, maxSize - offset), prefs);

        private static int WriteFrameHeader(Span<byte> destination, FramePreferences prefs)
        {
            if (destination.Length < LZ4F_HEADERSIZE_MIN)
                return -1;

            int dstPos = 0;

            BinaryPrimitives.WriteUInt32LittleEndian(destination, LZ4F_MAGICNUMBER);
            dstPos += 4;

            byte flg = 0x40; // Version 01
            flg |= (byte)((int)prefs.BlockMode << 5);
            flg |= (byte)((int)prefs.ContentChecksumFlag << 2);
            destination[dstPos++] = flg;

            int blockSizeIdForHeader = prefs.BlockSizeId == BlockSize.Default ? (int)BlockSize.Max64KB : (int)prefs.BlockSizeId;
            byte bd = (byte)(blockSizeIdForHeader << 4);
            destination[dstPos++] = bd;

            Span<byte> headerBytes = stackalloc byte[2] { flg, bd };
            uint headerChecksum = XXHash.XXH32((ReadOnlySpan<byte>)headerBytes, 0);
            destination[dstPos++] = (byte)((headerChecksum >> 8) & 0xFF);

            return dstPos;
        }

        /// <summary>
        /// Compress data into LZ4 frame format (Span overload)
        /// </summary>
        public static int CompressFrame(Span<byte> destination, ReadOnlySpan<byte> source, FramePreferences? prefs = null)
        {
            if (source.Length <= 0 || destination.Length <= 0)
                return -1;

            prefs ??= new FramePreferences();
            int dstPos = 0;

            int headerSize = WriteFrameHeader(destination, prefs);
            if (headerSize < 0) return -1;
            dstPos += headerSize;

            int srcPos = 0;
            int sourceSize = source.Length;
            int blockSize = prefs.GetBlockSize();
            int scratchSize = Math.Min(blockSize, sourceSize);
            byte[] compressedBlock = ArrayPool<byte>.Shared.Rent(LZ4Codec.CompressBound(scratchSize));

            try
            {
                while (srcPos < sourceSize)
                {
                    int currentBlockSize = Math.Min(blockSize, sourceSize - srcPos);
                    int maxCompressedSize = LZ4Codec.CompressBound(currentBlockSize);

                    var srcSlice = source.Slice(srcPos, currentBlockSize);
                    int compressedSize;
                    if (prefs.CompressionLevel < 0)
                    {
                        int acceleration = -prefs.CompressionLevel;
                        compressedSize = LZ4Codec.CompressFast(srcSlice, compressedBlock, acceleration);
                    }
                    else if (prefs.CompressionLevel >= LZ4HC.CLEVEL_MIN)
                    {
                        compressedSize = LZ4HC.CompressHC(srcSlice, compressedBlock.AsSpan(0, maxCompressedSize), prefs.CompressionLevel);
                    }
                    else
                    {
                        compressedSize = LZ4Codec.CompressDefault(srcSlice, compressedBlock.AsSpan(0, maxCompressedSize));
                    }

                    if (compressedSize <= 0) return -1;

                    bool useCompressed = compressedSize < currentBlockSize;
                    int blockDataSize = useCompressed ? compressedSize : currentBlockSize;

                    if (dstPos + 4 + blockDataSize > destination.Length)
                        return -1;

                    uint blockHeader = (uint)blockDataSize;
                    if (!useCompressed) blockHeader |= 0x80000000;

                    BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(dstPos), blockHeader);
                    dstPos += 4;

                    if (useCompressed)
                    {
                        compressedBlock.AsSpan(0, compressedSize).CopyTo(destination.Slice(dstPos));
                        dstPos += compressedSize;
                    }
                    else
                    {
                        srcSlice.CopyTo(destination.Slice(dstPos));
                        dstPos += currentBlockSize;
                    }

                    srcPos += currentBlockSize;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(compressedBlock);
            }

            if (dstPos + 4 > destination.Length) return -1;
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(dstPos), 0);
            dstPos += 4;

            if (prefs.ContentChecksumFlag == ContentChecksum.ChecksumEnabled)
            {
                if (dstPos + 4 > destination.Length) return -1;
                uint contentChecksum = XXHash.XXH32(source, 0);
                BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(dstPos), contentChecksum);
                dstPos += 4;
            }

            return dstPos;
        }

        /// <summary>
        /// Decompress data from LZ4 frame format (Span overload)
        /// </summary>
        public static int DecompressFrame(Span<byte> destination, ReadOnlySpan<byte> source)
        {
            if (source.Length <= 0 || destination.Length <= 0)
                return -1;

            int srcPos = 0;
            int dstPos = 0;
            int sourceSize = source.Length;

            if (srcPos + 4 > sourceSize) return -1;
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(srcPos));
            if (magic != LZ4F_MAGICNUMBER) return -1;
            srcPos += 4;

            if (srcPos + 3 > sourceSize) return -1;
            byte flg = source[srcPos++];
            byte bd = source[srcPos++];
            byte hc = source[srcPos++];

            Span<byte> headerBytes = stackalloc byte[2] { flg, bd };
            uint headerChecksum = XXHash.XXH32((ReadOnlySpan<byte>)headerBytes, 0);
            if (((headerChecksum >> 8) & 0xFF) != hc) return -1;

            bool contentChecksumFlag = ((flg >> 2) & 1) == 1;

            while (srcPos < sourceSize)
            {
                if (srcPos + 4 > sourceSize) return -1;
                uint blockHeader = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(srcPos));
                srcPos += 4;

                if (blockHeader == 0) break;

                bool isCompressed = (blockHeader & 0x80000000) == 0;
                int blockSz = (int)(blockHeader & 0x7FFFFFFF);

                if (srcPos + blockSz > sourceSize) return -1;

                if (isCompressed)
                {
                    int decompressedSize = LZ4Codec.DecompressSafe(
                        source.Slice(srcPos, blockSz),
                        destination.Slice(dstPos));
                    if (decompressedSize < 0) return -1;
                    dstPos += decompressedSize;
                }
                else
                {
                    if (dstPos + blockSz > destination.Length) return -1;
                    source.Slice(srcPos, blockSz).CopyTo(destination.Slice(dstPos));
                    dstPos += blockSz;
                }

                srcPos += blockSz;
            }

            if (contentChecksumFlag)
            {
                if (srcPos + 4 > sourceSize) return -1;
                uint storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(srcPos));
                uint calculatedChecksum = XXHash.XXH32((ReadOnlySpan<byte>)destination.Slice(0, dstPos), 0);
                if (storedChecksum != calculatedChecksum) return -1;
            }

            return dstPos;
        }

        /// <summary>
        /// Decompress data from LZ4 frame format
        /// </summary>
        public static int DecompressFrame(byte[] destination, int maxDestinationSize, byte[] source, int sourceSize)
        {
            if (source == null || destination == null || sourceSize <= 0 || maxDestinationSize <= 0)
                return -1;

            int srcPos = 0;
            int dstPos = 0;

            // Read and validate magic number
            if (srcPos + 4 > sourceSize)
                return -1;

            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(srcPos));
            if (magic != LZ4F_MAGICNUMBER)
                return -1;

            srcPos += 4;

            // Read frame descriptor
            if (srcPos + 3 > sourceSize)
                return -1;

            byte flg = source[srcPos++];
            byte bd = source[srcPos++];
            byte hc = source[srcPos++];

            // Validate header checksum
            Span<byte> headerBytes = stackalloc byte[2] { flg, bd };
            uint headerChecksum = XXHash.XXH32((ReadOnlySpan<byte>)headerBytes, 0);
            if (((headerChecksum >> 8) & 0xFF) != hc)
                return -1;

            bool contentChecksumFlag = ((flg >> 2) & 1) == 1;

            // Decompress blocks
            while (srcPos < sourceSize)
            {
                if (srcPos + 4 > sourceSize)
                    return -1;

                uint blockHeader = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(srcPos));
                srcPos += 4;

                if (blockHeader == 0)
                {
                    // End mark
                    break;
                }

                bool isCompressed = (blockHeader & 0x80000000) == 0;
                int blockSize = (int)(blockHeader & 0x7FFFFFFF);

                if (srcPos + blockSize > sourceSize)
                    return -1;

                if (isCompressed)
                {
                    int decompressedSize = LZ4Codec.DecompressSafe(
                        source.AsSpan(srcPos, blockSize),
                        destination.AsSpan(dstPos, maxDestinationSize - dstPos));
                    if (decompressedSize < 0)
                        return -1;

                    dstPos += decompressedSize;
                }
                else
                {
                    // Uncompressed block
                    if (dstPos + blockSize > maxDestinationSize)
                        return -1;

                    Array.Copy(source, srcPos, destination, dstPos, blockSize);
                    dstPos += blockSize;
                }

                srcPos += blockSize;
            }

            // Validate content checksum if present
            if (contentChecksumFlag)
            {
                if (srcPos + 4 > sourceSize)
                    return -1;

                uint storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(srcPos));

                uint calculatedChecksum = XXHash.XXH32(destination, dstPos, 0);

                if (storedChecksum != calculatedChecksum)
                    return -1;
            }

            return dstPos;
        }
    }
}
