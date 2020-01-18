using System;
using System.Threading;

namespace Gzipper.Core
{
    internal class CWorker<T> : IDisposable
    {
        private readonly Thread _thread;
        private readonly ManualResetEvent _workCompletedResetEvent;

        public delegate Boolean TryGetFunc<TOut>(out TOut item);

        private TryGetFunc<T> _itemsSource;
        private Action<T> _workAction;
        private Action _onCompleteAction;
        
        public Exception WorkerException { get; private set; }

        public WaitHandle WaitHandle => _workCompletedResetEvent;

        public CWorker()
        {
            _thread = new Thread(Routine);
            _workCompletedResetEvent = new ManualResetEvent(initialState: false);
        }

        public void StartRoutine(TryGetFunc<T> itemsSource, Action<T> workAction, Action onCompleteAction)
        {
            _itemsSource = itemsSource;
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
                    if (!_itemsSource(out T item))
                        break;

                    _workAction(item);
                }

                _onCompleteAction();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured in thread {_thread.ManagedThreadId}");
                WorkerException = exception;
            }
            finally
            {
                _workCompletedResetEvent.Set();
            }
        }
    }
}
