using System;
using System.IO;
using System.IO.Compression;

namespace Gzipper
{
    internal class CGzipperStrategy : ICompressionStrategy
    {
        public Byte[] Compress(Byte[] source)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                    gzipStream.Write(source, 0, source.Length);

                return memoryStream.ToArray();
            }
        }

        public Byte[] Decompress(Byte[] source)
        {
            using (var sourceMemoryStream = new MemoryStream(source))
            using (var destinationMemoryStream = new MemoryStream())
            using (var gzipStream = new GZipStream(sourceMemoryStream, CompressionMode.Decompress))
            {
                const Int32 bufferSize = 4096;
                var buffer = new Byte[bufferSize];

                while (true)
                {
                    Int32 readCount = gzipStream.Read(buffer, 0, bufferSize);
                    if (readCount == 0)
                        break;

                    destinationMemoryStream.Write(buffer, 0, readCount);
                }

                return destinationMemoryStream.ToArray();
            }
        }
    }
}
