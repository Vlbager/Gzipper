using System;
using System.Collections.Concurrent;
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
        private readonly Object _writeLockObject;
        private readonly ConcurrentQueue<CWorker> _workersQueue;
        private Int64 _writeOffset;
        private Int64 _readOffset;

        public CManager(String sourcePath, String destinationPath)
        {
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
            _workers = new CWorker[Environment.ProcessorCount];
            //_workers = new CWorker[1];
            _workersQueue = new ConcurrentQueue<CWorker>();
            _readLockObject = new Object();
            _writeLockObject = new Object();
            for (var i = 0; i < _workers.Length; i++)
                _workers[i] = new CWorker();
        }

        public void Compress()
        {
            if (File.Exists(_destinationPath))
                File.Delete(_destinationPath);

            ICompressionStrategy compressionStrategy = GetCompressionStrategy();

            using (FileStream sourceStream = File.OpenRead(_sourcePath))
            {
                foreach (CWorker worker in _workers)
                {
                    var destinationStream = new FileStream(_destinationPath, FileMode.Append, FileAccess.Write,
                        FileShare.Write);

                    worker.StartRoutine(
                        (chunk, stream) => CompressAction(chunk, stream, worker, compressionStrategy),
                        () => GetRawChunk(sourceStream, worker),
                        destinationStream);
                }

                foreach (CWorker worker in _workers)
                    worker.WaitWhenCompleted();
            }
        }

        public void Decompress()
        {
            if (File.Exists(_destinationPath))
                File.Delete(_destinationPath);

            ICompressionStrategy compressionStrategy = GetCompressionStrategy();

            using (FileStream sourceStream = File.OpenRead(_sourcePath))
            {
                foreach (CWorker worker in _workers)
                {
                    var destinationStream = new FileStream(_destinationPath, FileMode.Append, FileAccess.Write,
                        FileShare.Write);

                    worker.StartRoutine(
                        (chunk, stream) => DecompressAction(chunk, stream, worker, compressionStrategy),
                        () => GetCompressedChunk(sourceStream, worker),
                        destinationStream);
                }

                foreach (CWorker worker in _workers)
                    worker.WaitWhenCompleted(); 
            }
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

            Int64 offset = GetWriteOffset(worker, rawData.Length);

            destinationStream.Position = offset;
            destinationStream.Write(rawData, 0, rawData.Length);
        }

        private Int64 GetWriteOffset(CWorker worker, Int32 dataLength)
        {
            Int64 offset;

            lock (_writeLockObject)
            {
                while (true)
                {
                    _workersQueue.TryPeek(out CWorker nextWorker);
                    if (nextWorker == worker)
                        break;

                    Monitor.Wait(_writeLockObject);
                }

                offset = _writeOffset;
                _writeOffset += dataLength;

                _workersQueue.TryDequeue(out CWorker _);

                Monitor.PulseAll(_writeLockObject);
            }

            return offset;
        }

        private CChunk GetCompressedChunk(Stream sourceStream, CWorker consumer)
        {
            Int32 chunkSize;
            
            lock (_readLockObject)
            {
                _workersQueue.Enqueue(consumer);
                if (!sourceStream.TryReadInt32(out chunkSize))
                    return null;

                return sourceStream.ReadChunk(chunkSize);
            }
        }

        private CChunk GetRawChunk(Stream sourceStream, CWorker consumer)
        {
            var data = new Byte[SourceChunkSize];
            Int32 readCount;

            Int64 originalOffset = Interlocked.Add(ref _readOffset, SourceChunkSize) - SourceChunkSize;

            lock (_readLockObject)
            {
                _workersQueue.Enqueue(consumer);
                readCount = sourceStream.Read(data, 0, SourceChunkSize);
            }

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
