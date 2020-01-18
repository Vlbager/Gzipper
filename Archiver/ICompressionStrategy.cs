using System;

namespace Gzipper.Archiver
{
    internal interface ICompressionStrategy
    {
        Byte[] Compress(Byte[] source);

        Byte[] Decompress(Byte[] source);
    }
}
