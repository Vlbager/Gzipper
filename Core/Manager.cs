using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Gzipper.Core
{
    internal class CManager<T> : IDisposable
    {
        private readonly String _sourcePath;
        private readonly String _destinationPath;
        private readonly IWorkStrategy<T> _strategy;

        private readonly CWorker<T>[] _readers;
        private readonly CWorker<T>[] _workers;
        private readonly CWorker<T>[] _writers;
        private readonly List<CWorker<T>> _uncompletedWorkers;
        private readonly List<IDisposable> _disposableResources;

        private readonly CItemsBuffer<T> _sourceItems;
        private readonly CItemsBuffer<T> _completedItems;

        public CManager(IWorkStrategy<T> strategy, String sourcePath, String destinationPath)
        {
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
            _strategy = strategy;

            _disposableResources = new List<IDisposable>();
            _uncompletedWorkers = new List<CWorker<T>>();

            Int32 workersCount = Environment.ProcessorCount;
            _readers = CreateWorkers(workersCount);
            _workers = CreateWorkers(workersCount);
            _writers = CreateWorkers(workersCount);

            // Let there be 5 items in the buffer for each worker.
            Int32 capacity = 5 * workersCount;
            _sourceItems = new CItemsBuffer<T>(capacity, workersCount);
            _disposableResources.Add(_sourceItems);

            _completedItems = new CItemsBuffer<T>(capacity, workersCount);
            _disposableResources.Add(_completedItems);
        }

        public void Start()
        {
            if (File.Exists(_destinationPath))
                File.Delete(_destinationPath);

            foreach (CWorker<T> reader in _readers)
            {
                var sourceStream = new FileStream(_sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _disposableResources.Add(sourceStream);

                reader.StartRoutine(
                    (out T item) => _strategy.TryGetItem(sourceStream, out item),
                    item => _sourceItems.Add(item),
                    () => _sourceItems.ProducerIsDoneCallBack());
            }

            foreach (CWorker<T> worker in _workers)
            {
                worker.StartRoutine(
                    _sourceItems.TryTake,
                    item => _strategy.Act(item, _completedItems),
                    () => _completedItems.ProducerIsDoneCallBack());
            }

            foreach (CWorker<T> writer in _writers)
            {
                var destinationStream = new FileStream(_destinationPath, FileMode.OpenOrCreate, FileAccess.Write,
                    FileShare.Write);
                _disposableResources.Add(destinationStream);

                writer.StartRoutine(
                    _completedItems.TryTake,
                    item => _strategy.WriteItem(item, destinationStream),
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
            while (_uncompletedWorkers.Count > 0)
            {
                WaitHandle[] waitHandles = _uncompletedWorkers
                    .Select(worker => worker.WaitHandle)
                    .ToArray();

                Int32 completedIndex = WaitHandle.WaitAny(waitHandles);

                CWorker<T> completedWorker = _uncompletedWorkers[completedIndex];
                if (completedWorker.WorkerException != null)
                    throw completedWorker.WorkerException;

                _uncompletedWorkers.Remove(completedWorker);
            }
        }

        private CWorker<T>[] CreateWorkers(Int32 count)
        {
            var workers = new CWorker<T>[count];
            for (var i = 0; i < count; i++)
            {
                var worker = new CWorker<T>();
                workers[i] = worker;
                _disposableResources.Add(worker);
                _uncompletedWorkers.Add(worker);
            }

            return workers;
        }
    }
}
