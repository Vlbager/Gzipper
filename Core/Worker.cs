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

        private Func<CChunk> _chunkSource;
        private Action<CChunk, Stream> _workAction;
        private Stream _destinationStream;

        public CWorker()
        {
            _thread = new Thread(Routine) { IsBackground = true };
            _workCompletedResetEvent = new ManualResetEventSlim(initialState: false);
        }

        public void WaitWhenCompleted()
        {
            _workCompletedResetEvent.Wait();
        }

        public void StartRoutine(Action<CChunk, Stream> workAction, Func<CChunk> chunkSource, Stream destinationStream)
        {
            _workAction = workAction;
            _destinationStream = destinationStream;
            _chunkSource = chunkSource;
            _thread.Start();
        }

        private void Routine()
        {
            try
            {
                while (true)
                {
                    CChunk chunk = _chunkSource();
                    if (chunk == null)
                        break;

                    _workAction(chunk, _destinationStream);
                }

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
