using System;
using NLog;

namespace kanimal_cli
{
    class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            
            var config = new NLog.Config.LoggingConfiguration();
            
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
            NLog.LogManager.Configuration = config;

            try
            {
                throw new Exception("test exception please ignore");
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }
}