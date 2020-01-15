using System;

namespace Gzipper
{
    internal class CChunk
    {
        // 4 byte for size value, 8 byte for offset value.
        public static Int32 HeaderSize = sizeof(Int32) + sizeof(Int64);

        public Byte[] Data { get; }
        public Int64 Offset { get; }
        public Int32 Size { get; }

        public Boolean IsLast { get; }

        public CChunk(Byte[] data, Int64 offset)
        {
            Data = data;
            Offset = offset;
            Size = Data.Length + HeaderSize;
        }

        private CChunk()
        {
            IsLast = true;
        }

        public static CChunk CreateEmptyChunk()
        {
            return new CChunk();
        }
    }
}
