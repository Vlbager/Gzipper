using System;

namespace Gzipper
{
    internal class CChunk
    {
        public Byte[] Data { get; }
        public Int64 Offset { get; }
        // 4 byte for size value, 8 byte for offset value.
        public Int32 Size => Data.Length + sizeof(Int32) + sizeof(Int64);

        public CChunk(Byte[] data, Int64 offset)
        {
            Data = data;
            Offset = offset;
        }
    }
}
