using System;
using CommandLine;
using Gzipper.CommandLineOptions;

namespace Gzipper
{
    public class Program
    { 
        public static Int32 Main(String[] args)
        {
            try
            {
                Int32 returnCode = Parser.Default.ParseArguments<CCompressOptions, CDecompressOptions>(args)
                    .MapResult(
                        (CCompressOptions options) => Compress(options),
                        (CDecompressOptions options) => Decompress(options),
                        errorCode => (Int32)EReturnCode.InvalidArgs);

                return returnCode;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Unexpected error occured {exception.Message}");
            }

            return (Int32)EReturnCode.FatalError;
        }

        private static Int32 Compress(CCompressOptions options)
        {
            var manager = new CManager(options.SourceFileName, options.DestinationFileName);
            manager.Compress();

            return (Int32)EReturnCode.Success;
        }

        private static Int32 Decompress(CDecompressOptions options)
        {
            var manager = new CManager(options.SourceFileName, options.DestinationFileName);
            manager.Decompress();

            return (Int32) EReturnCode.Success;
        }


    }
}
