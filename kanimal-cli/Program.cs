using System;
using System.Collections.Generic;
using System.IO;
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
            Parser.Default.ParseArguments<KanimToScmlOptions, ScmlToKanimOptions, GenericOptions>(args)
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
                })
                .WithParsed<GenericOptions>(o =>
                {
                    SetVerbosity(o);
                    
                    var files = new List<string>(o.Files);

                    Logger.Info("Reading...");
                    Reader reader = null;
                    switch (o.InputFormat)
                    {
                        case "scml":
                            var scml = files.Find(path => path.EndsWith(".scml"));
                            reader = new ScmlReader(scml);
                            break;
                        case "kanim":
                            var png = files.Find(path => path.EndsWith(".png"));
                            var build = files.Find(path => path.EndsWith("build.bytes"));
                            var anim = files.Find(path => path.EndsWith("anim.bytes"));
                            reader = new KanimReader(
                                new FileStream(png, FileMode.Open),
                                new FileStream(build, FileMode.Open),
                                new FileStream(anim, FileMode.Open));
                            reader.Read(o.OutputPath);
                            break;
                        default:
                            Logger.Fatal($"The specified input format \"{o.InputFormat}\" is not recognized.");
                            Environment.Exit((int)ExitCodes.IncorrectArguments);
                            break;
                    }
                    
                    Logger.Info($"Successfully read anim of format {o.InputFormat}.");
                    Logger.Info("Writing...");
                    
                    switch (o.OutputFormat)
                    {
                        case "scml":
                            var scmlwriter = new ScmlWriter(reader);
                            scmlwriter.Save(o.OutputPath);
                            scmlwriter.SaveSprites(o.OutputPath);
                            break;
                        case "kanim":
                            var kanimwriter = new KanimWriter(reader);
                            kanimwriter.Save(o.OutputPath);
                            break;
                        default:
                            Logger.Fatal($"The specified input format \"{o.OutputFormat}\" is not recognized.");
                            Environment.Exit((int)ExitCodes.IncorrectArguments);
                            break;
                    }

                    Logger.Info($"Successfully wrote to format {o.OutputFormat}");
                });
        }
    }
}