using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using CommandLine;
using kanimal;
using NLog.Fluent;

namespace kanimal_cli
{
    
    
    class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private static void SetVerbosity(ProgramOptions o)
        { 
            var config = new NLog.Config.LoggingConfiguration();
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            logconsole.Layout = "[${level}] ${message}";

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

        static void Convert(string input_format, string output_format, List<string> files, ProgramOptions opt)
        {
            SetVerbosity(opt);

            if (files.Count == 0)
            {
                Logger.Fatal("Please specify files to convert.");
                Environment.Exit((int) ExitCodes.IncorrectArguments);
            }

            Logger.Info("Reading...");
            Reader reader = null;
            switch (input_format)
            {
                case "scml":
                    var scml = files.Find(path => path.EndsWith(".scml"));
                    reader = new ScmlReader(scml);
                    reader.Read(opt.OutputPath);
                    break;
                case "kanim":
                    var png = files.Find(path => path.EndsWith(".png"));
                    var build = files.Find(path => path.EndsWith("build.bytes"));
                    var anim = files.Find(path => path.EndsWith("anim.bytes"));
                    reader = new KanimReader(
                        new FileStream(png, FileMode.Open),
                        new FileStream(build, FileMode.Open),
                        new FileStream(anim, FileMode.Open));
                    reader.Read(opt.OutputPath);
                    break;
                default:
                    Logger.Fatal($"The specified input format \"{input_format}\" is not recognized.");
                    Environment.Exit((int)ExitCodes.IncorrectArguments);
                    break;
            }
            
            Logger.Info($"Successfully read from format {input_format}.");
            Logger.Info("Writing...");

            Directory.CreateDirectory(opt.OutputPath);
            
            switch (output_format)
            {
                case "scml":
                    var scmlwriter = new ScmlWriter(reader);
                    scmlwriter.Save(Path.Join(output_format, $"{reader.BuildData.Name}.scml"));
                    scmlwriter.SaveSprites(opt.OutputPath);
                    break;
                case "kanim":
                    var kanimwriter = new KanimWriter(reader);
                    kanimwriter.Save(opt.OutputPath);
                    break;
                default:
                    Logger.Fatal($"The specified output format \"{output_format}\" is not recognized.");
                    Environment.Exit((int)ExitCodes.IncorrectArguments);
                    break;
            }

            Logger.Info($"Successfully wrote to format {output_format}");
        }
        
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<KanimToScmlOptions, ScmlToKanimOptions, GenericOptions, DumpOptions>(args)
                .WithParsed<KanimToScmlOptions>(o => Convert(
                    "kanim",
                    "scml",
                    o.Files.ToList(),
                    o))
                .WithParsed<DumpOptions>(o =>
                {
                    SetVerbosity(o);

                    var files = new List<string>(o.Files);
                    var png = files.Find(path => path.EndsWith(".png"));
                    var build = files.Find(path => path.EndsWith("build.bytes"));
                    var anim = files.Find(path => path.EndsWith("anim.bytes"));

                    Directory.CreateDirectory(o.OutputPath);
                    Utilities.dump =
                        new StreamWriter(new FileStream(Path.Join(o.OutputPath, "dump.log"), FileMode.Create));
                    var reader = new KanimReader(
                        new FileStream(build, FileMode.Open),
                        new FileStream(anim, FileMode.Open),
                        new FileStream(png, FileMode.Open));
                    reader.Read(o.OutputPath);
                    Utilities.dump.Flush();
                })
                .WithParsed<ScmlToKanimOptions>(o => Convert(
                    "scml", 
                    "kanim",
                    new List<string>{o.ScmlFile},
                    o))
                .WithParsed<GenericOptions>(o => Convert(o.InputFormat, o.OutputFormat, o.Files.ToList(), o));
        }
    }
}