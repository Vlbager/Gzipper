using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Gzipper
{
    internal class CWorker
    {
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _workCompletedResetEvent;

        private Func<Stream, CChunk> _chunkSource;
        private Action<CChunk, Stream> _workAction;
        private Stream _destinationStream;
        private Stream _sourceStream;

        public CWorker()
        {
            _thread = new Thread(Routine);
            _workCompletedResetEvent = new ManualResetEventSlim(initialState: false);
        }

        public void WaitWhenCompleted()
        {
            _workCompletedResetEvent.Wait();
        }

        public void StartRoutine(Action<CChunk, Stream> workAction, Func<Stream, CChunk> chunkSource,
            Stream destinationStream, Stream sourceStream)
        {
            _workAction = workAction;
            _chunkSource = chunkSource;
            _destinationStream = destinationStream;
            _sourceStream = sourceStream;
            _thread.Start();
        }

        private void Routine()
        {
            try
            {
                while (true)
                {
                    CChunk chunk = _chunkSource(_sourceStream);
                    if (chunk == null)
                        break;

                    _workAction(chunk, _destinationStream);
                }

                _sourceStream.Dispose();
                _destinationStream.Dispose();
                _workCompletedResetEvent.Set();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Unexpected error occured in thread {_thread.ManagedThreadId}\n{exception}");
            }
        }
    }
}
