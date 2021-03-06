﻿using System;
using System.IO;
using System.Threading;
using Gzipper.Core;

namespace Gzipper.Archiver
{
    internal class CCompressor : IWorkStrategy<CChunk>
    {
        // 1 MiB
        private const Int32 SourceChunkSize = 1_048_576;

        private readonly ICompressionStrategy _compressionStrategy;

        private Int64 _writeOffset;
        private Int64 _readOffset;

        public CCompressor(ICompressionStrategy compressionStrategy)
        {
            _compressionStrategy = compressionStrategy;
        }

        public Boolean TryGetItem(Stream sourceStream, out CChunk chunk)
        {
            chunk = default;

            var data = new Byte[SourceChunkSize];

            Int64 originalOffset = Interlocked.Add(ref _readOffset, SourceChunkSize) - SourceChunkSize;

            sourceStream.Position = originalOffset;
            Int32 readCount = sourceStream.Read(data, 0, SourceChunkSize);

            if (readCount < SourceChunkSize)
            {
                if (readCount == 0)
                    return false;

                var lastChunkData = new Byte[readCount];
                Array.Copy(data, lastChunkData, readCount);

                data = lastChunkData;
            }

            chunk = new CChunk(data, originalOffset);

            return true;
        }

        public void Act(CChunk chunk, CItemsBuffer<CChunk> destination)
        {
            Byte[] compressedData = _compressionStrategy.Compress(chunk.Data);

            var compressedChunk = new CChunk(compressedData, chunk.Offset);

            destination.Add(compressedChunk);
        }

        public void WriteItem(CChunk chunk, Stream destinationStream)
        {
            Int64 writeOffset = Interlocked.Add(ref _writeOffset, chunk.Size) - chunk.Size;

            destinationStream.Position = writeOffset;
            destinationStream.WriteChunk(chunk);
        }
    }
}
