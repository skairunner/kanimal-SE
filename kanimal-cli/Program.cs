using System;
using System.Collections.Generic;
using System.IO;
using NLog;
using CommandLine;
using kanimal;

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

    [Verb("scml", HelpText = "Convert kanim to scml.")]
    class KanimToScmlOptions: ProgramOptions
    {
        [Value(0)]
        public IEnumerable<string> Files { get; set; }
    }

    [Verb("kanim", HelpText = "Convert scml to kanim.")]
    class ScmlToKanimOptions : ProgramOptions
    {
        [Value(0)]
        public string ScmlFile { get; set; }
    }
    
    class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private static void SetVerbosity(ProgramOptions o)
        { 
            var config = new NLog.Config.LoggingConfiguration();
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            logconsole.Layout = "${message}";

            if (o.Verbose && o.Silent)
            {
                Console.WriteLine("You can't mix -v/--verbose and -s/--silent.");
                Environment.Exit((int) ExitCodes.IncorrectArguments);
            }
                    
            if (o.Verbose)
            {
                config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
            }
            else if (o.Silent)
            {
                config.AddRule(LogLevel.Error, LogLevel.Fatal, logconsole);
            } else
            {
                config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            }

            LogManager.Configuration = config;
        }
        
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<KanimToScmlOptions, ScmlToKanimOptions>(args)
                .WithParsed<KanimToScmlOptions>(o =>
                {
                    SetVerbosity(o);

                    var files = new List<string>(o.Files);
                    var png = files.Find(path => path.EndsWith(".png"));
                    var build = files.Find(path => path.EndsWith("build.bytes"));
                    var anim = files.Find(path => path.EndsWith("anim.bytes"));
                    
                    Kanimal.ToScml(
                    png,
                    build,
                    anim,
                    o.OutputPath);
                })
                .WithParsed<ScmlToKanimOptions>(options =>
                {
                    SetVerbosity(options);

                    var file = options.ScmlFile;
                    
                    Kanimal.ScmlToScml(file, options.OutputPath);
                });
        }
    }
}