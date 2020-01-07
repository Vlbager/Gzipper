using System;
using System.IO;
using System.Threading;
// ReSharper disable AccessToDisposedClosure

namespace Gzipper
{
    internal class CManager
    {
        // 1 MiB
        private const Int32 SourceChunkSize = 1_048_576;

        private readonly String _sourcePath;
        private readonly String _destinationPath;
        private readonly CWorker[] _workers;

        private readonly Object _readLockObject;
        private Int64 _writeOffset;
        private Int64 _readOffset;

        public CManager(String sourcePath, String destinationPath)
        {
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
            _workers = new CWorker[Environment.ProcessorCount];
            //_workers = new CWorker[1];
            _readLockObject = new Object();
            for (var i = 0; i < _workers.Length; i++)
                _workers[i] = new CWorker();
        }

        public void Compress()
        {
            if (File.Exists(_destinationPath))
                File.Delete(_destinationPath);

            ICompressionStrategy compressionStrategy = GetCompressionStrategy();

            foreach (CWorker worker in _workers)
            {
                var sourceStream = new FileStream(_sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var destinationStream = new FileStream(_destinationPath, FileMode.Append, FileAccess.Write,
                    FileShare.Write);

                worker.StartRoutine(
                    (chunk, stream) => CompressAction(chunk, stream, worker, compressionStrategy),
                    (stream) => GetRawChunk(stream, worker),
                    destinationStream,
                    sourceStream);
            }

            foreach (CWorker worker in _workers)
                worker.WaitWhenCompleted();
        }

        public void Decompress()
        {
            if (File.Exists(_destinationPath))
                File.Delete(_destinationPath);

            ICompressionStrategy compressionStrategy = GetCompressionStrategy();

            foreach (CWorker worker in _workers)
            {
                var sourceStream = new FileStream(_sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var destinationStream = new FileStream(_destinationPath, FileMode.Append, FileAccess.Write,
                    FileShare.Write);

                worker.StartRoutine(
                    (chunk, stream) => DecompressAction(chunk, stream, worker, compressionStrategy),
                    (stream) => GetCompressedChunk(stream, worker),
                    destinationStream,
                    sourceStream);
            }

            foreach (CWorker worker in _workers)
                worker.WaitWhenCompleted();
        }

        private ICompressionStrategy GetCompressionStrategy()
        {
            return new GzipperStrategy();
        }

        private void CompressAction(CChunk sourceChunk, Stream destinationStream, CWorker worker,
            ICompressionStrategy compressionStrategy)
        {
            Byte[] compressedData = compressionStrategy.Compress(sourceChunk.Data);

            var compressedChunk = new CChunk(compressedData, sourceChunk.Offset);

            Int64 offset = GetWriteOffset(worker, compressedChunk.Size);

            destinationStream.Position = offset;
            destinationStream.WriteChunk(compressedChunk);
        }

        private void DecompressAction(CChunk sourceChunk, Stream destinationStream, CWorker worker,
            ICompressionStrategy compressionStrategy)
        {
            Byte[] rawData = compressionStrategy.Decompress(sourceChunk.Data);

            destinationStream.Position = sourceChunk.Offset;
            destinationStream.Write(rawData, 0, rawData.Length);
        }

        private Int64 GetWriteOffset(CWorker worker, Int32 dataLength)
        {
            Int64 offset = Interlocked.Add(ref _writeOffset, dataLength) - dataLength;

            return offset;
        }

        private CChunk GetCompressedChunk(Stream sourceStream, CWorker consumer)
        {
            Int32 chunkSize;

            lock (_readLockObject)
            {
                sourceStream.Position = _readOffset;
                if (!sourceStream.TryReadInt32(out chunkSize))
                    return null;

                // 4 byte for size value, 8 byte for offset value.
                _readOffset += chunkSize + sizeof(Int32) + sizeof(Int64);
            }

            return sourceStream.ReadChunk(chunkSize);
        }

        private CChunk GetRawChunk(Stream sourceStream, CWorker consumer)
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
