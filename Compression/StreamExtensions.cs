using System;
using System.IO;

namespace Gzipper
{
    internal static class SStreamExtensions
    {
        // Chunk struct:
        // 4 bytes: size,
        // n bytes: data


        /// <exception cref="InvalidDataException">Chunk expected to exist</exception>
        public static CChunk ReadChunk(this Stream stream, Int32 chunkSize)
        {
            Int64 offset = stream.ReadInt64();

            var chunkDataBytes = new Byte[chunkSize];
            Int32 readCount = stream.Read(chunkDataBytes, 0, chunkSize);
            if (readCount < chunkSize)
                throw new InvalidDataException("Unexpected end of stream");

            return new CChunk(chunkDataBytes, offset);
        }

        public static void WriteChunk(this Stream stream, CChunk chunk)
        {
            stream.WriteInt(chunk.Data.Length);
            stream.WriteInt(chunk.Offset);
            stream.Write(chunk.Data, 0, chunk.Data.Length);
        }

        public static Boolean TryReadInt32(this Stream stream, out Int32 value)
        {
            value = default;

            var bytes = new Byte[sizeof(Int32)];
            Int32 readCount = stream.Read(bytes, 0, sizeof(Int32));
            if (readCount < sizeof(Int32))
                return false;

            value = BitConverter.ToInt32(bytes);

            return true;
        }

        private static Int64 ReadInt64(this Stream stream)
        {
            var bytes = new Byte[sizeof(Int64)];
            Int32 readCount = stream.Read(bytes, 0, sizeof(Int64));
            if (readCount < sizeof(Int64))
                throw new InvalidDataException("Unexpected end of stream");

            return BitConverter.ToInt64(bytes);
        }

        private static void WriteInt(this Stream stream, Int32 value)
        {
            Byte[] bytes = BitConverter.GetBytes(value);
            stream.Write(bytes, 0, sizeof(Int32));
        }

        private static void WriteInt(this Stream stream, Int64 value)
        {
            Byte[] bytes = BitConverter.GetBytes(value);
            stream.Write(bytes, 0, sizeof(Int64));
        }
    }
}
