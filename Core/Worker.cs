using System;
using System.Threading;

namespace Gzipper
{
    internal class CWorker : IDisposable
    {
        private readonly Thread _thread;
        private readonly ManualResetEvent _workCompletedResetEvent;

        public delegate Boolean TryGetFunc<T>(out T item);

        private TryGetFunc<CChunk> _tryGetChunkFunc;
        private Action<CChunk> _workAction;
        private Action _onCompleteAction;
        
        public Exception WorkerException { get; private set; }

        public WaitHandle WaitHandle => _workCompletedResetEvent;

        public CWorker()
        {
            _thread = new Thread(Routine) {IsBackground = true};
            _workCompletedResetEvent = new ManualResetEvent(initialState: false);
        }

        public void StartRoutine(TryGetFunc<CChunk> tryGetChunkFunc, Action<CChunk> workAction, Action onCompleteAction)
        {
            _tryGetChunkFunc = tryGetChunkFunc;
            _workAction = workAction;
            _onCompleteAction = onCompleteAction;
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
                    if (!_tryGetChunkFunc(out CChunk chunk))
                        break;

                    _workAction(chunk);
                }

                _onCompleteAction();
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
