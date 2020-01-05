using System;
using System.ComponentModel;
using System.IO;

namespace Gzipper
{
    internal static class SStreamExtensions
    {
        // Chunk struct:
        // 4 bytes: size,
        // n bytes: data

        /// <param name="stream">Source stream</param>
        /// <exception cref="InvalidDataException">Chunk expected to exist</exception>
        public static CChunk ReadChunk(this Stream stream)
        {
            Int64 offset = stream.Position;

            Int32 dataSize = stream.ReadInt32();

            var chunkDataBytes = new Byte[dataSize];
            Int32 readCount = stream.Read(chunkDataBytes, 0, dataSize);
            if (readCount < dataSize)
                throw new InvalidDataException("Unexpected end of stream");

            return new CChunk(chunkDataBytes, offset);
        }

        public static void WriteChunk(this Stream stream, CChunk chunk)
        {
            stream.Position = chunk.Offset;
            stream.WriteInt32(chunk.Size);
            stream.Write(chunk.Data, 0, chunk.Size);
        }

        private static Int32 ReadInt32(this Stream stream)
        {
            var bytes = new Byte[sizeof(Int32)];
            Int32 readCount = stream.Read(bytes, 0, sizeof(Int32));
            if (readCount < sizeof(Int32))
                throw new InvalidDataException("Unexpected end of stream");

            return BitConverter.ToInt32(bytes);
        }

        private static void WriteInt32(this Stream stream, Int32 value)
        {
            Byte[] bytes = BitConverter.GetBytes(value);
            stream.Write(bytes, 0, sizeof(Int32));
        }
    }
}
