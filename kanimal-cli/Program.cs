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
            logconsole.Layout = "${message}";
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
            LogManager.Configuration = config;

            var basepath = @"C:\Users\skairunner\RiderProjects\kanimal\testcases\geyser_gas_steam";
            kanimal.Kanimal.ToScml(
                Path.Join(basepath, "geyser_gas_steam_0.png"),
                Path.Join(basepath, "geyser_gas_steam_build.bytes"),
                Path.Join(basepath, "geyser_gas_steam_anim.bytes"),
                Path.Join(basepath, "output"));
        }
    }
}