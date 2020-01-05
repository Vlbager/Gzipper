using System;
using System.Collections.Concurrent;
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

        private readonly Object _readLockObject;
        private readonly Object _writeLockObject;
        private readonly ConcurrentQueue<CWorker> _workersQueue;
        private Int64 _currentOffset;

        public CManager(String sourcePath, String destinationPath)
        {
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
            _workers = new CWorker[Environment.ProcessorCount];
            _workersQueue = new ConcurrentQueue<CWorker>();
            _readLockObject = new Object();
            _writeLockObject = new Object();
            for (var i = 0; i < _workers.Length; i++)
                _workers[i] = new CWorker();
        }

        public void Compress()
        {
            ICompressionStrategy compressionStrategy = GetCompressionStrategy();

            using (FileStream sourceStream = File.OpenRead(_sourcePath))
            {
                foreach (CWorker worker in _workers)
                {
                    FileStream destinationStream = File.OpenWrite(_destinationPath);

                    worker.StartRoutine(
                        (chunk, stream) => CompressAction(chunk, destinationStream, worker, compressionStrategy),
                        // ReSharper disable once AccessToDisposedClosure
                        () => GetRawChunk(sourceStream, worker),
                        destinationStream);
                }
            }

            foreach (CWorker worker in _workers)
                worker.WaitWhenCompleted();
        }

        public void Decompress()
        {
            ICompressionStrategy compressionStrategy = GetCompressionStrategy();

            using (FileStream sourceStream = File.OpenRead(_sourcePath))
            {
                foreach (CWorker worker in _workers)
                {
                    FileStream destinationStream = File.OpenWrite(_destinationPath);

                    worker.StartRoutine(
                        (chunk, stream) => DecompressAction(chunk, destinationStream, worker, compressionStrategy),
                        // ReSharper disable once AccessToDisposedClosure
                        () => GetCompressedChunk(sourceStream, worker),
                        destinationStream);
                }
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

            Int64 offset = GetWriteOffset(worker, compressedData.Length);

            var compressedChunk = new CChunk(compressedData, offset);

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

                offset = _currentOffset;
                _currentOffset += dataLength;

                Monitor.PulseAll(_writeLockObject);
            }

            return offset;
        }

        private CChunk GetCompressedChunk(Stream sourceStream, CWorker consumer)
        {
            if (sourceStream.Position == sourceStream.Length)
                return null;

            CChunk result;

            lock (_readLockObject)
            {
                _workersQueue.Enqueue(consumer);
                result = sourceStream.ReadChunk();
            }

            return result;
        }

        private CChunk GetRawChunk(Stream sourceStream, CWorker consumer)
        {
            var data = new Byte[SourceChunkSize];
            Int32 readCount;

            lock (_readLockObject)
            {
                _workersQueue.Enqueue(consumer);
                readCount = sourceStream.Read(data, 0, SourceChunkSize);
            }

            return readCount == 0
                ? null
                : new CChunk(data, 0);
        }
    }
}
