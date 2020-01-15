using System;
using System.Threading;

namespace Gzipper
{
    internal class CWorker : IDisposable
    {
        private readonly Thread _thread;
        private readonly ManualResetEvent _workCompletedResetEvent;

        private Func<CChunk> _chunkSource;
        private Action<CChunk> _workAction;
        
        public Exception WorkerException { get; private set; }

        public WaitHandle WaitHandle => _workCompletedResetEvent;

        public CWorker()
        {
            _thread = new Thread(Routine) {IsBackground = true};
            _workCompletedResetEvent = new ManualResetEvent(initialState: false);
        }

        public void StartRoutine(Func<CChunk> chunkSource, Action<CChunk> workAction)
        {
            _chunkSource = chunkSource;
            _workAction = workAction;
            _thread.Start();
        }

        public void Dispose()
        {
            _workCompletedResetEvent.Dispose();
        }

        private void Routine()
        {
            try
            {
                while (true)
                {
                    CChunk chunk = _chunkSource();
                    if (chunk.IsLast)
                        break;

                    _workAction(chunk);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Unexpected error occured in thread {_thread.ManagedThreadId}");
                WorkerException = exception;
            }
            finally
            {
                _workCompletedResetEvent.Set();
            }
        }
    }
}
