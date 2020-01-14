using System;
using System.IO;
using System.Threading;

namespace Gzipper
{
    internal class CDecompressor : IWorkStrategy
    {
        private readonly ICompressionStrategy _compressionStrategy;

        private Int64 _readOffset;

        public CDecompressor(ICompressionStrategy compressionStrategy)
        {
            _compressionStrategy = compressionStrategy;
        }

        /// <summary>
        /// Returns a compressed chunk from <see cref="sourceStream"/> or null, when end of stream
        /// </summary>
        public CChunk GetChunk(Stream sourceStream)
        {
            Int32 chunkSize;

            Boolean chunkIsCaptured;
            do
            {
                Int64 startPosition = _readOffset;
                sourceStream.Position = startPosition;
                if (!sourceStream.TryReadInt32(out chunkSize))
                    return null;

                Int64 nextOffset = startPosition + chunkSize + CChunk.HeaderSize;

                chunkIsCaptured = Interlocked
                                      .CompareExchange(ref _readOffset, nextOffset, startPosition) == startPosition;

            } while (!chunkIsCaptured);

            return sourceStream.ReadChunk(chunkSize);
        }

        public void Act(CChunk chunk, Stream destinationStream)
        {
            Byte[] rawData = _compressionStrategy.Decompress(chunk.Data);

            destinationStream.Position = chunk.Offset;
            destinationStream.Write(rawData, 0, rawData.Length);
        }
    }
}
