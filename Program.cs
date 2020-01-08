using System;
using System.IO;
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
                        errorCode => (Int32) EReturnCode.Fail);

                return returnCode;
            }
            catch (FileNotFoundException exception)
            {
                Console.WriteLine($"File {exception.FileName} cannot be found");
            }
            catch (InvalidDataException)
            {
                Console.WriteLine("Source file is damaged or has the invalid format");
            }
            catch (Exception exception)
            {
                Console.WriteLine("Unexpected error occured. Please, contact technical support.");
                Console.WriteLine(exception);
            }

            return (Int32)EReturnCode.Fail;
        }

        private static Int32 Compress(CCompressOptions options)
        {
            var compressionStrategy = new CGzipperStrategy();
            var manager = new CManager(options.SourceFileName, options.DestinationFileName, compressionStrategy);
            manager.Compress();

            return (Int32)EReturnCode.Ok;
        }

        private static Int32 Decompress(CDecompressOptions options)
        {
            var compressionStrategy = new CGzipperStrategy();
            var manager = new CManager(options.SourceFileName, options.DestinationFileName, compressionStrategy);
            manager.Decompress();

            return (Int32) EReturnCode.Ok;
        }


    }
}
