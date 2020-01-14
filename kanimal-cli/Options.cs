using System.Collections.Generic;
using CommandLine;

namespace kanimal_cli
{
    public abstract class ProgramOptions
    {
        [Option('v', "verbose", Required = false, HelpText = "Enable debug output.")]
        public bool Verbose { get; set; }

        [Option('s', "silent", Required = false, HelpText = "Totally silence output on success.")]
        public bool Silent { get; set; }

        [Option('o', "output", Required = false, HelpText = "Designate a directory to output result files.")]
        public string OutputPath { get; set; } = "output";
    }

    public abstract class ConversionOptions : ProgramOptions
    {
        [Option('S', "strict", Required = false, HelpText = "When writing to scml, enabling this flag ")]
        public bool Strict { get; set; }

        [Option('i', "interpolate", Required = false, HelpText = "Enable interpolating scml files on load.")]
        public bool Interp { get; set; }

        [Option('b', "debone", Required = false, HelpText = "Enable deboning scml files on load.")]
        public bool Debone { get; set; }

    }

    [Verb("dump", HelpText = "Output a dump of the specified kanim.")]
    public class DumpOptions : ProgramOptions
    {
        [Value(0)] public IEnumerable<string> Files { get; set; }
    }

    // For ones with Output and Input specifiers
    [Verb("convert", HelpText = "Convert between formats.")]
    public class GenericOptions : ConversionOptions
    {
        [Option('I', "input-format", Required = true, HelpText = "The input format, from [kanim, scml]")]
        public string InputFormat { get; set; }

        [Option('O', "output-format", Required = true, HelpText = "The output format, from [kanim, scml]")]
        public string OutputFormat { get; set; }

        [Value(0)] public IEnumerable<string> Files { get; set; }
    }

    [Verb("scml", HelpText = "Convert kanim to scml. Convenience verb equivalent to 'convert -I kanim -O scml'.")]
    public class KanimToScmlOptions : ConversionOptions
    {
        [Value(0)] public IEnumerable<string> Files { get; set; }
    }

    [Verb("kanim", HelpText = "Convert scml to kanim. Convenience verb equivalent to 'convert -I scml -O kanim'.")]
    public class ScmlToKanimOptions : ConversionOptions
    {
        [Value(0)] public string ScmlFile { get; set; }
    }

    [Verb("batch-convert", HelpText = "Given an Assets/ directory, attempt to batch convert kanim to scml.")]
    public class BatchConvertOptions : ConversionOptions
    {
        [Value(0)] public string AssetDirectory { get; set; }
    }
}