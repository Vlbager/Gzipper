using System;

namespace Gzipper
{
    internal class CChunk
    {
        public Byte[] Data { get; }
        public Int64 Offset { get; }
        public Int32 Size => Data.Length;

        public CChunk(Byte[] data, Int64 offset)
        {
            Data = data;
            Offset = offset;
        }
    }
}
