using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using CommandLine;
using kanimal;

namespace kanimal_cli
{
    internal class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static void SetVerbosity(ProgramOptions o)
        {
            var config = new NLog.Config.LoggingConfiguration();
            var targetConsole = new NLog.Targets.ConsoleTarget("logconsole");
            targetConsole.Layout = "[${level}] ${message}";

            if (o.Verbose && o.Silent)
            {
                Console.WriteLine("You can't mix -v/--verbose and -s/--silent.");
                Environment.Exit((int) ExitCodes.IncorrectArguments);
            }

            if (o.Verbose)
                config.AddRule(LogLevel.Debug, LogLevel.Fatal, targetConsole);
            else if (o.Silent)
                config.AddRule(LogLevel.Error, LogLevel.Fatal, targetConsole);
            else
                config.AddRule(LogLevel.Info, LogLevel.Fatal, targetConsole);

            LogManager.Configuration = config;
        }

        private static void Convert(string inputFormat, string outputFormat, List<string> files, ProgramOptions opt)
        {
            SetVerbosity(opt);

            if (files.Count == 0)
            {
                Logger.Fatal("Please specify files to convert.");
                Environment.Exit((int) ExitCodes.IncorrectArguments);
            }

            Logger.Info("Reading...");
            Reader reader = null;
            switch (inputFormat)
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
                    Logger.Fatal($"The specified input format \"{inputFormat}\" is not recognized.");
                    Environment.Exit((int) ExitCodes.IncorrectArguments);
                    break;
            }

            Logger.Info($"Successfully read from format {inputFormat}.");
            Logger.Info("Writing...");

            Directory.CreateDirectory(opt.OutputPath);

            switch (outputFormat)
            {
                case "scml":
                    var scmlWriter = new ScmlWriter(reader);
                    scmlWriter.Save(Path.Join(opt.OutputPath, $"{reader.BuildData.Name}.scml"));
                    scmlWriter.SaveSprites(opt.OutputPath);
                    break;
                case "kanim":
                    var kanimWriter = new KanimWriter(reader);
                    kanimWriter.Save(opt.OutputPath);
                    break;
                default:
                    Logger.Fatal($"The specified output format \"{outputFormat}\" is not recognized.");
                    Environment.Exit((int) ExitCodes.IncorrectArguments);
                    break;
            }

            Logger.Info($"Successfully wrote to format {outputFormat}");
        }

        private static void Main(string[] args)
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
                    Utilities.Dump =
                        new StreamWriter(new FileStream(Path.Join(o.OutputPath, "dump.log"), FileMode.Create));
                    var reader = new KanimReader(
                        new FileStream(build, FileMode.Open),
                        new FileStream(anim, FileMode.Open),
                        new FileStream(png, FileMode.Open));
                    reader.Read(o.OutputPath);
                    Utilities.Dump.Flush();
                })
                .WithParsed<ScmlToKanimOptions>(o => Convert(
                    "scml",
                    "kanim",
                    new List<string> {o.ScmlFile},
                    o))
                .WithParsed<GenericOptions>(o => Convert(o.InputFormat, o.OutputFormat, o.Files.ToList(), o));
        }
    }
}