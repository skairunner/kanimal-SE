using System.Collections.Generic;
using System.Text;
using NLog;

namespace kanimal
{
    public static class Utilities
    {
        public static void LogDebug(Logger logger, Dictionary<int, string> hashes)
        {
            var builder = new StringBuilder();
            foreach (var entry in hashes)
            {
                builder.Append($"value {entry.Key} maps onto symbol {entry.Value}\n");
            }
            logger.Debug(builder.ToString());
        }

        public static void LogDebug(Logger logger, IToDebugString debuggable)
        {
            logger.Debug(debuggable.ToDebugString());
        }

        public static void LogDebug(Logger logger, List<KBuild.Row> buildTable)
        {
            var builder = new StringBuilder();
            foreach (var row in buildTable)
            {
                builder.Append(row.ToDebugString() + "\n");
            }

            logger.Debug(builder.ToString());
        }

        public static void LogDebug(Logger logger, Dictionary<string, int> animIdMap)
        {
            var builder = new StringBuilder();
            foreach (var entry in animIdMap)
            {
                builder.Append($"element {entry.Key} maps onto index {entry.Value}\n");
            }
            
            logger.Debug(builder.ToString());
        }
    }
}