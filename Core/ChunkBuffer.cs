using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Gzipper
{
    internal class CChunkBuffer : IDisposable
    {
        private readonly BlockingCollection<CChunk> _items;
        private readonly CancellationTokenSource _tokenSource;

        private Int32 _producersCount;
        private Boolean _workIsDone;

        public CChunkBuffer(Int32 capacity, Int32 producersCount)
        {
            _items = new BlockingCollection<CChunk>(capacity);
            _tokenSource = new CancellationTokenSource();
            _producersCount = producersCount;
        }

        public Boolean TryTake(out CChunk chunk)
        {
            chunk = default;

            if (_workIsDone)
                return false;

            try
            {
                _items.TryTake(out chunk, -1, _tokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            return true;
        }

        public void Add(CChunk chunk)
        {
            _items.Add(chunk);
        }

        public void ProducerIsDoneCallBack()
        {
            if (Interlocked.Decrement(ref _producersCount) == 0)
            {
                _workIsDone = true;
                _tokenSource.Cancel();
            }
        }

        public void Dispose()
        {
            _items.Dispose();
        }
    }
}
