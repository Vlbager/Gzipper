using System;
using System.IO;

namespace Gzipper.Core
{
    internal interface IWorkStrategy<T>
    {
        Boolean TryGetItem(Stream sourceStream, out T item);

        void Act(T item, CItemsBuffer<T> destination);

        void WriteItem(T item, Stream destinationStream);
    }
}
