using System;
using System.IO;
using NLog;

namespace kanimal_cli
{
    class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            var config = new NLog.Config.LoggingConfiguration();
            
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            logconsole.Layout = "${level:uppercase=true}|${message}";
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
            LogManager.Configuration = config;

            var basepath = @"C:\Users\skairunner\RiderProjects\kanimal\testcases";
            kanimal.Kanimal.ToScml(
                Path.Join(basepath, "zestysalsa_0.png"),
                Path.Join(basepath, "zestysalsa_build.bytes"),
                Path.Join(basepath, "zestysalsa_anim.bytes"),
                Path.Join(basepath, "output"));
        }
    }
}