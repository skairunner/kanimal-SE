using System;
using System.Collections.Generic;
using System.Text;
using NLog;

namespace kanimal
{
    using AnimHashTable = Dictionary<int, string>;

    public static class Utilities
    {
        public const int MS_PER_S = 1000;
        
        public static void LogDebug(Logger logger, AnimHashTable hashes)
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

        public static string GetAnimIdName(string name, int index)
        {
            return $"{name}_{index}";
        }
        
        // Clamps x to the range [a, b]
        public static double ClampRange(double a, double x, double b)
        {
            if (b < a)
            {
                var t = b;
                b = a;
                a = t;
            }

            return Math.Max(Math.Min(x, b), a);
        }
    }
}