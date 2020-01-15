using System.Collections.Concurrent;
using System.IO;

namespace Gzipper
{
    internal interface IWorkStrategy
    {
        CChunk GetChunk(Stream sourceStream);

        void Act(CChunk chunk, BlockingCollection<CChunk> destination);

        void WriteChunk(CChunk chunk, Stream destinationStream);
    }
}
