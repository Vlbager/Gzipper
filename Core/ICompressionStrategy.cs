using System;

namespace Gzipper
{
    internal interface ICompressionStrategy
    {
        Byte[] Compress(Byte[] source);

        Byte[] Decompress(Byte[] source);
    }
}
