/*
 * LZ4 Frame - LZ4 Frame Format Support
 * C# Implementation
 * Copyright (c) 2026. Translated from C implementation by Yann Collet.
 * 
 * BSD 2-Clause License (http://www.opensource.org/licenses/bsd-license.php)
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are
 * met:
 * 
 *     * Redistributions of source code must retain the above copyright
 * notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above
 * copyright notice, this list of conditions and the following disclaimer
 * in the documentation and/or other materials provided with the
 * distribution.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
 * OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
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

            while (srcPos < sourceSize)
            {
                int currentBlockSize = Math.Min(blockSize, sourceSize - srcPos);
                
                // Compress block
                byte[] tempSrc = new byte[currentBlockSize];
                Array.Copy(source, srcPos, tempSrc, 0, currentBlockSize);

                int maxCompressedSize = LZ4Codec.CompressBound(currentBlockSize);
                byte[] compressedBlock = new byte[maxCompressedSize];

                int compressedSize;
                if (prefs.CompressionLevel >= LZ4HC.CLEVEL_MIN)
                {
                    compressedSize = LZ4HC.CompressHC(tempSrc, compressedBlock, currentBlockSize, maxCompressedSize, prefs.CompressionLevel);
                }
                else
                {
                    compressedSize = LZ4Codec.CompressDefault(tempSrc, compressedBlock, currentBlockSize, maxCompressedSize);
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

                destination[dstPos++] = (byte)blockHeader;
                destination[dstPos++] = (byte)(blockHeader >> 8);
                destination[dstPos++] = (byte)(blockHeader >> 16);
                destination[dstPos++] = (byte)(blockHeader >> 24);

                // Write block data
                if (useCompressed)
                {
                    Array.Copy(compressedBlock, 0, destination, dstPos, compressedSize);
                    dstPos += compressedSize;
                }
                else
                {
                    Array.Copy(tempSrc, 0, destination, dstPos, currentBlockSize);
                    dstPos += currentBlockSize;
                }

                srcPos += currentBlockSize;
            }

            // Write end mark (block size = 0)
            if (dstPos + 4 > maxDestinationSize)
                return -1;

            destination[dstPos++] = 0;
            destination[dstPos++] = 0;
            destination[dstPos++] = 0;
            destination[dstPos++] = 0;

            // Write content checksum if enabled
            if (prefs.ContentChecksumFlag == ContentChecksum.ChecksumEnabled)
            {
                if (dstPos + 4 > maxDestinationSize)
                    return -1;

                // Calculate checksum over entire source
                uint contentChecksum = XXHash.XXH32(source, 0);

                destination[dstPos++] = (byte)contentChecksum;
                destination[dstPos++] = (byte)(contentChecksum >> 8);
                destination[dstPos++] = (byte)(contentChecksum >> 16);
                destination[dstPos++] = (byte)(contentChecksum >> 24);
            }

            return dstPos;
        }

        private static int WriteFrameHeader(byte[] destination, int offset, int maxSize, FramePreferences prefs)
        {
            if (offset + LZ4F_HEADERSIZE_MIN > maxSize)
                return -1;

            int dstPos = offset;

            // Magic number (little-endian)
            uint magic = LZ4F_MAGICNUMBER;
            destination[dstPos++] = (byte)(magic & 0xFF);
            destination[dstPos++] = (byte)((magic >> 8) & 0xFF);
            destination[dstPos++] = (byte)((magic >> 16) & 0xFF);
            destination[dstPos++] = (byte)((magic >> 24) & 0xFF);

            // FLG byte
            byte flg = 0x40; // Version 01
            flg |= (byte)((int)prefs.BlockMode << 5);
            flg |= (byte)((int)prefs.ContentChecksumFlag << 2);
            destination[dstPos++] = flg;

            // BD byte
            byte bd = (byte)((int)prefs.BlockSizeId << 4);
            destination[dstPos++] = bd;

            // Header checksum (XXH32 of FLG and BD bytes, bits 15-8)
            byte[] headerBytes = new byte[2] { flg, bd };
            uint headerChecksum = XXHash.XXH32(headerBytes, 0);
            destination[dstPos++] = (byte)((headerChecksum >> 8) & 0xFF);

            return dstPos - offset;
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

            uint magic = (uint)(source[srcPos] | (source[srcPos + 1] << 8) | (source[srcPos + 2] << 16) | (source[srcPos + 3] << 24));
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
            byte[] headerBytes = new byte[2] { flg, bd };
            uint headerChecksum = XXHash.XXH32(headerBytes, 0);
            if (((headerChecksum >> 8) & 0xFF) != hc)
                return -1;

            bool contentChecksumFlag = ((flg >> 2) & 1) == 1;

            // Decompress blocks
            while (srcPos < sourceSize)
            {
                if (srcPos + 4 > sourceSize)
                    return -1;

                uint blockHeader = (uint)(source[srcPos] | (source[srcPos + 1] << 8) | (source[srcPos + 2] << 16) | (source[srcPos + 3] << 24));
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
                    // Decompress block
                    byte[] compressedBlock = new byte[blockSize];
                    Array.Copy(source, srcPos, compressedBlock, 0, blockSize);

                    int decompressedSize = LZ4Codec.DecompressSafe(compressedBlock, destination, blockSize, maxDestinationSize - dstPos, dstPos);
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

                uint storedChecksum = (uint)(source[srcPos] | (source[srcPos + 1] << 8) | (source[srcPos + 2] << 16) | (source[srcPos + 3] << 24));
                
                byte[] decompressedData = new byte[dstPos];
                Array.Copy(destination, 0, decompressedData, 0, dstPos);
                uint calculatedChecksum = XXHash.XXH32(decompressedData, 0);

                if (storedChecksum != calculatedChecksum)
                    return -1;
            }

            return dstPos;
        }
    }
}
