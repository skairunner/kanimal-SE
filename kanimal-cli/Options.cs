using System.Collections.Generic;
using CommandLine;

namespace kanimal_cli
{
    abstract class ProgramOptions
    {
        [Option('v', "verbose", Required = false, HelpText = "Enable debug output.")]
        public bool Verbose { get; set; }
        
        [Option('s', "silent", Required = false, HelpText = "Totally silence output on success.")]
        public bool Silent { get; set; }

        [Option('o', "output", Required = false, HelpText = "Designate a directory to output result files.")]
        public string OutputPath { get; set; } = "output";
    }

    // For ones with Output and Input specifiers
    [Verb("convert", HelpText = "Convert between formats.")]
    class GenericOptions: ProgramOptions
    {
        [Option('I', "input-format", Required = true, HelpText = "The input format, from [kanim, scml]")]
        public string InputFormat { get; set; }
        
        [Option('O', "output-format", Required = true, HelpText = "The output format, from [kanim, scml]")]
        public string OutputFormat { get; set; }
        
        [Value(0)]
        public IEnumerable<string> Files { get; set; }
    }

    [Verb("scml", HelpText = "Convert kanim to scml. Convenience verb equivalent to 'convert -I kanim -O scml'.")]
    class KanimToScmlOptions: ProgramOptions
    {
        [Value(0)]
        public IEnumerable<string> Files { get; set; }
    }

    [Verb("kanim", HelpText = "Convert scml to kanim. Convenience verb equivalent to 'convert -I scml -O kanim'.")]
    class ScmlToKanimOptions : ProgramOptions
    {
        [Value(0)]
        public string ScmlFile { get; set; }
    }
    
    [Verb("Kanim", HelpText = "Convert kanim to kanim.")]
    class KanimToKAnimOptions : ProgramOptions
    {
        [Value(0)]
        public IEnumerable<string> Files { get; set; }
    }
}