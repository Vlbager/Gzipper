using System;
using CommandLine;

namespace Gzipper.CommandLineOptions
{
    internal abstract class CGzipperOptions
    {
        [Value(0, Required = true, HelpText = "source file name")]
        public String SourceFileName { get; set; }
        
        [Value(1, Required = true, HelpText = "destination file name")]
        public String DestinationFileName { get; set; }
    }
}
