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

        private readonly BlockingCollection<CChunk> _sourceChunks;
        private readonly BlockingCollection<CChunk> _completedChunks;

        private Boolean _readersIsDone;
        private Boolean _workersIsDone;

        public CManager(String sourcePath, String destinationPath)
        {
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;

            _disposableResources = new List<IDisposable>();

            Int32 workersCount = Environment.ProcessorCount;
            _workers = CreateWorkers(workersCount);
            _readers = CreateWorkers(workersCount);
            _writers = CreateWorkers(workersCount);

            Int32 capacity = 3 * _workers.Length;
            _sourceChunks = new BlockingCollection<CChunk>(capacity);
            _disposableResources.Add(_sourceChunks);

            _completedChunks = new BlockingCollection<CChunk>(capacity);
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
                    () => strategy.GetChunk(sourceStream),
                    chunk => _sourceChunks.Add(chunk));
            }

            foreach (CWorker worker in _workers)
            {
                worker.StartRoutine(
                    () => GetChunk(_sourceChunks, ref _readersIsDone),
                    chunk => strategy.Act(chunk, _completedChunks));
            }

            foreach (CWorker writer in _writers)
            {
                var destinationStream = new FileStream(_destinationPath, FileMode.Append, FileAccess.Write,
                    FileShare.Write);
                _disposableResources.Add(destinationStream);

                writer.StartRoutine(
                    () => GetChunk(_completedChunks, ref _workersIsDone),
                    chunk => strategy.WriteChunk(chunk, destinationStream));
            }

            WaitWhenCompleted();
        }

        public void Dispose()
        {
            foreach (var disposable in _disposableResources)
                disposable.Dispose();
        }

        private CChunk GetChunk(BlockingCollection<CChunk> source, ref Boolean workIsStopped)
        {
            while (true)
            {
                if (source.TryTake(out CChunk chunk, 3))
                    return chunk;

                if (workIsStopped)
                    return CChunk.CreateEmptyChunk();
            }
        }

        private void WaitWhenCompleted()
        {
            List<CWorker> uncompletedReaders = _readers.ToList();
            List<CWorker> uncompletedWorkers = _workers.ToList();
            List<CWorker> uncompletedWriters = _writers.ToList();

            List<CWorker> allUncompleted = uncompletedReaders
                .Concat(uncompletedWorkers)
                .Concat(uncompletedWriters)
                .ToList();

            while (allUncompleted.Count > 0)
            {
                WaitHandle[] waitHandles = allUncompleted
                    .Select(worker => worker.WaitHandle)
                    .ToArray();

                Int32 completedIndex = WaitHandle.WaitAny(waitHandles);

                CWorker completedWorker = allUncompleted[completedIndex];
                if (completedWorker.WorkerException != null)
                    throw completedWorker.WorkerException;

                allUncompleted.Remove(completedWorker);

                if (uncompletedReaders.Contains(completedWorker))
                {
                    uncompletedReaders.Remove(completedWorker);
                    if (uncompletedReaders.Count == 0)
                        _readersIsDone = true;
                }
                else if (uncompletedWorkers.Contains(completedWorker))
                {
                    uncompletedWorkers.Remove(completedWorker);
                    if (uncompletedWorkers.Count == 0)
                        _workersIsDone = true;
                }
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
