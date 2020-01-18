using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Gzipper.Core
{
    internal class CItemsBuffer<T> : IDisposable
    {
        private readonly BlockingCollection<T> _items;
        private readonly CancellationTokenSource _tokenSource;

        private Int32 _producersCount;
        private Int32 _itemsCount;
        private Boolean _workIsDone;

        public CItemsBuffer(Int32 capacity, Int32 producersCount)
        {
            _items = new BlockingCollection<T>(capacity);
            _tokenSource = new CancellationTokenSource();
            _producersCount = producersCount;
        }

        public Boolean TryTake(out T item)
        {
            item = default;

            if (_workIsDone && _itemsCount == 0)
                return false;

            try
            {
                _items.TryTake(out item, -1, _tokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            Interlocked.Decrement(ref _itemsCount);

            return true;
        }

        public void Add(T item)
        {
            Interlocked.Increment(ref _itemsCount);
            _items.Add(item);
        }

        public void ProducerIsDoneCallBack()
        {
            if (Interlocked.Decrement(ref _producersCount) != 0) 
                return;

            _workIsDone = true;

            var spinWait = new SpinWait();
            while (_itemsCount != 0)
            {
                spinWait.SpinOnce();
            }

            _tokenSource.Cancel();
        }

        public void Dispose()
        {
            _items.Dispose();
            _tokenSource.Dispose();
        }
    }
}
