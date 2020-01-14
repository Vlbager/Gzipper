using System;
using System.Threading;

namespace Gzipper
{
    internal class CWorker
    {
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _workCompletedResetEvent;

        private Func<CChunk> _chunkSource;
        private Action<CChunk> _workAction;

        private Exception _workerException;

        public CWorker()
        {
            _thread = new Thread(Routine);
            _workCompletedResetEvent = new ManualResetEventSlim(initialState: false);
        }

        public void WaitWhenCompleted()
        {
            _workCompletedResetEvent.Wait();
            if (_workerException != null)
                throw _workerException;

            _workCompletedResetEvent.Dispose();
        }

        public void StartRoutine(Action<CChunk> workAction, Func<CChunk> chunkSource)
        {
            _workAction = workAction;
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

                    _workAction(chunk);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Unexpected error occured in thread {_thread.ManagedThreadId}");
                _workerException = exception;
            }
            finally
            {
                _workCompletedResetEvent.Set();
            }
        }
    }
}
