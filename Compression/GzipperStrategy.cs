using System;
using System.IO;
using System.IO.Compression;

namespace Gzipper
{
    internal class GzipperStrategy : ICompressionStrategy
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
            using (var memoryStream = new MemoryStream())
            using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(memoryStream);

                return memoryStream.ToArray();
            }
        }
    }
}
