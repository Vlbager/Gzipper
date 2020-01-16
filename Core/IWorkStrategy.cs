using System;
using System.Collections.Concurrent;
using System.IO;

namespace Gzipper
{
    internal interface IWorkStrategy
    {
        Boolean TryGetChunk(Stream sourceStream, out CChunk chunk);

        void Act(CChunk chunk, CChunkBuffer destination);

        void WriteChunk(CChunk chunk, Stream destinationStream);
    }
}
