using System;
using System.Collections.Generic;
using System.IO;

namespace Gzipper
{
    internal class CManager : IDisposable
    {
        private readonly String _sourcePath;
        private readonly String _destinationPath;
        private readonly CWorker[] _workers;
        private readonly List<IDisposable> _disposableResources;

        public CManager(String sourcePath, String destinationPath)
        {
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
            _workers = new CWorker[Environment.ProcessorCount];
            _disposableResources = new List<IDisposable>(2 * _workers.Length);
            for (var i = 0; i < _workers.Length; i++)
                _workers[i] = new CWorker();
        }

        public void Start(IWorkStrategy strategy)
        {
            if (File.Exists(_destinationPath))
                File.Delete(_destinationPath);

            foreach (CWorker worker in _workers)
            {
                var sourceStream = new FileStream(_sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _disposableResources.Add(sourceStream);

                var destinationStream = new FileStream(_destinationPath, FileMode.Append, FileAccess.Write,
                    FileShare.Write);
                _disposableResources.Add(destinationStream);

                worker.StartRoutine(
                    chunk => strategy.Act(chunk, destinationStream),
                    () => strategy.GetChunk(sourceStream));
            }

            foreach (CWorker worker in _workers)
                worker.WaitWhenCompleted();
        }

        public void Dispose()
        {
            foreach (IDisposable disposable in _disposableResources)
                disposable.Dispose();
        }
    }
}
