using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Gzipper
{
    internal class CManager : IDisposable
    {
        private readonly String _sourcePath;
        private readonly String _destinationPath;
        private readonly CWorker[] _workers;
        private readonly CWorker[] _readers;
        private readonly CWorker[] _writers;
        private readonly List<IDisposable> _disposableResources;

        private readonly CChunkBuffer _sourceChunks;
        private readonly CChunkBuffer _completedChunks;

        public CManager(String sourcePath, String destinationPath)
        {
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;

            _disposableResources = new List<IDisposable>();

            Int32 workersCount = Environment.ProcessorCount;
            _workers = CreateWorkers(workersCount);
            _readers = CreateWorkers(workersCount);
            _writers = CreateWorkers(workersCount);

            Int32 capacity = 3 * workersCount;
            _sourceChunks = new CChunkBuffer(capacity, workersCount);
            _disposableResources.Add(_sourceChunks);

            _completedChunks = new CChunkBuffer(capacity, workersCount);
            _disposableResources.Add(_completedChunks);
        }

        public void Start(IWorkStrategy strategy)
        {
            if (File.Exists(_destinationPath))
                File.Delete(_destinationPath);

            foreach (CWorker reader in _readers)
            {
                var sourceStream = new FileStream(_sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _disposableResources.Add(sourceStream);

                reader.StartRoutine(
                    (out CChunk chunk) => strategy.TryGetChunk(sourceStream, out chunk),
                    chunk => _sourceChunks.Add(chunk),
                    () => _sourceChunks.ProducerIsDoneCallBack());
            }

            foreach (CWorker worker in _workers)
            {
                worker.StartRoutine(
                    _sourceChunks.TryTake,
                    chunk => strategy.Act(chunk, _completedChunks),
                    () => _completedChunks.ProducerIsDoneCallBack());
            }

            foreach (CWorker writer in _writers)
            {
                var destinationStream = new FileStream(_destinationPath, FileMode.OpenOrCreate, FileAccess.Write,
                    FileShare.Write);
                _disposableResources.Add(destinationStream);

                writer.StartRoutine(
                    _completedChunks.TryTake,
                    chunk => strategy.WriteChunk(chunk, destinationStream),
                    () => { });
            }

            WaitWhenCompleted();
        }

        public void Dispose()
        {
            foreach (var disposable in _disposableResources)
                disposable.Dispose();
        }

        private void WaitWhenCompleted()
        {
            List<CWorker> uncompletedWorkers = _readers
                .Concat(_workers)
                .Concat(_writers)
                .ToList();

            while (uncompletedWorkers.Count > 0)
            {
                WaitHandle[] waitHandles = uncompletedWorkers
                    .Select(worker => worker.WaitHandle)
                    .ToArray();

                Int32 completedIndex = WaitHandle.WaitAny(waitHandles);

                CWorker completedWorker = uncompletedWorkers[completedIndex];
                if (completedWorker.WorkerException != null)
                    throw completedWorker.WorkerException;

                uncompletedWorkers.Remove(completedWorker);
            }
        }

        private CWorker[] CreateWorkers(Int32 count)
        {
            var workers = new CWorker[count];
            for (var i = 0; i < count; i++)
            {
                var worker = new CWorker();
                workers[i] = worker;
                _disposableResources.Add(worker);
            }

            return workers;
        }
    }
}
