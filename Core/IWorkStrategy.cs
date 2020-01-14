using System.IO;

namespace Gzipper
{
    internal interface IWorkStrategy
    {
        CChunk GetChunk(Stream sourceStream);

        void Act(CChunk chunk, Stream destinationStream);
    }
}
