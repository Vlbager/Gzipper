using System;
using System.IO;
using System.Threading;

namespace Gzipper
{
    internal class CManager
    {
        // 1 MiB
        private const Int32 SourceChunkSize = 1_048_576;

        private readonly String _sourcePath;
        private readonly String _destinationPath;
        private readonly CWorker[] _workers;
        private readonly ICompressionStrategy _compressionStrategy;

        private readonly Object _readLockObject;
        private Int64 _writeOffset;
        private Int64 _readOffset;

        public CManager(String sourcePath, String destinationPath, ICompressionStrategy compressionStrategy)
        {
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
            _compressionStrategy = compressionStrategy;
            _workers = new CWorker[Environment.ProcessorCount];
            _readLockObject = new Object();
            for (var i = 0; i < _workers.Length; i++)
                _workers[i] = new CWorker();
        }

        public void Compress()
        {
            if (File.Exists(_destinationPath))
                File.Delete(_destinationPath);

            foreach (CWorker worker in _workers)
            {
                var sourceStream = new FileStream(_sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var destinationStream = new FileStream(_destinationPath, FileMode.Append, FileAccess.Write,
                    FileShare.Write);

                worker.StartRoutine(CompressAction, GetRawChunk, destinationStream, sourceStream);
            }

            foreach (CWorker worker in _workers)
                worker.WaitWhenCompleted();
        }

        public void Decompress()
        {
            if (File.Exists(_destinationPath))
                File.Delete(_destinationPath);

            foreach (CWorker worker in _workers)
            {
                var sourceStream = new FileStream(_sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var destinationStream = new FileStream(_destinationPath, FileMode.Append, FileAccess.Write,
                    FileShare.Write);

                worker.StartRoutine(DecompressAction, GetCompressedChunk, destinationStream, sourceStream);
            }

            foreach (CWorker worker in _workers)
                worker.WaitWhenCompleted();
        }

        private void CompressAction(CChunk sourceChunk, Stream destinationStream)
        {
            Byte[] compressedData = _compressionStrategy.Compress(sourceChunk.Data);

            var compressedChunk = new CChunk(compressedData, sourceChunk.Offset);

            Int64 offset = Interlocked.Add(ref _writeOffset, compressedChunk.Size) - compressedChunk.Size;

            destinationStream.Position = offset;
            destinationStream.WriteChunk(compressedChunk);
        }

        private void DecompressAction(CChunk sourceChunk, Stream destinationStream)
        {
            Byte[] rawData = _compressionStrategy.Decompress(sourceChunk.Data);

            destinationStream.Position = sourceChunk.Offset;
            destinationStream.Write(rawData, 0, rawData.Length);
        }

        private CChunk GetCompressedChunk(Stream sourceStream)
        {
            Int32 chunkSize;

            lock (_readLockObject)
            {
                sourceStream.Position = _readOffset;
                if (!sourceStream.TryReadInt32(out chunkSize))
                    return null;

                _readOffset += chunkSize + CChunk.HeaderSize;
            }

            return sourceStream.ReadChunk(chunkSize);
        }

        private CChunk GetRawChunk(Stream sourceStream)
        {
            var data = new Byte[SourceChunkSize];

            Int64 originalOffset = Interlocked.Add(ref _readOffset, SourceChunkSize) - SourceChunkSize;

            sourceStream.Position = originalOffset;
            Int32 readCount = sourceStream.Read(data, 0, SourceChunkSize);

            if (readCount < SourceChunkSize)
            {
                if (readCount == 0)
                    return null;

                var lastChunkData = new Byte[readCount];
                Array.Copy(data, lastChunkData, readCount);

                data = lastChunkData;
            }

            return new CChunk(data, originalOffset);
        }
    }
}
