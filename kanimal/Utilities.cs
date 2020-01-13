using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace kanimal
{
    using AnimHashTable = Dictionary<int, string>;

    public static class Utilities
    {
        public const int MS_PER_S = 1000;

        public static Regex SpriteNameRegex = new Regex(
            @"^(?<basename>[\w\d.* '+=-]+)_(?<frame>\d+)(?<extension>\.[\w\d]{1,4})?");

        public static void LogDebug(Logger logger, AnimHashTable hashes)
        {
            var builder = new StringBuilder();
            foreach (var entry in hashes) builder.Append($"value {entry.Key} maps onto symbol {entry.Value}\n");
            logger.Debug(builder.ToString());
        }

        public static void LogDebug(Logger logger, Dictionary<int, SpriteBaseName> buildHashes)
        {
            var builder = new StringBuilder();
            foreach (var entry in buildHashes) builder.Append($"value {entry.Key} maps onto symbol {entry.Value.Value}\n");
            logger.Debug(builder.ToString());
        }

        public static void LogDebug(Logger logger, IToDebugString debuggable)
        {
            logger.Debug(debuggable.ToDebugString());
        }

        public static void LogDebug(Logger logger, List<KBuild.Row> buildTable)
        {
            var builder = new StringBuilder();
            foreach (var row in buildTable) builder.Append(row.ToDebugString() + "\n");

            logger.Debug(builder.ToString());
        }

        public static void LogDebug(Logger logger, Dictionary<string, int> animIdMap)
        {
            var builder = new StringBuilder();
            foreach (var entry in animIdMap) builder.Append($"element {entry.Key} maps onto index {entry.Value}\n");

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

        public static string GetSpriteBaseName(string name)
        {
            return SpriteNameRegex.Match(name).Groups["basename"].Value;
        }

        public static string WithoutExtension(string name)
        {
            var i = name.LastIndexOf(".", StringComparison.Ordinal);
            if (i >= 0)
                return name.Substring(0, i);
            else
                return name;
        }

        // depends on the filename being properly formatted
        public static int GetFrameCount(string filename)
        {
            try
            {
                return int.Parse(SpriteNameRegex.Match(filename).Groups["frame"].Value);
            }
            catch (FormatException)
            {
                throw new ProjectParseException(
                    $"The file name \"{filename}\" is not in the correct format. Make sure the base name (excluding the extension) is followed by an underscore and a number.\n" +
                    $"For example: \"{filename}_0.png\".");
            }
        }

        public static int KleiHash(string str)
        {
            if (str == null)
                return 0;

            var hash = 0;
            str = str.ToLower();
            for (var i = 0; i < str.Length; i++) hash = str.ToLower()[i] + (hash << 6) + (hash << 16) - hash;

            return hash;
        }

        public static T Min<T>(params T[] values) where T : IComparable<T>
        {
            var min = values[0];
            for (var i = 1; i < values.Length; i++)
                if (values[i].CompareTo(min) < 0)
                    min = values[i];

            return min;
        }

        public static T Max<T>(params T[] values) where T : IComparable<T>
        {
            var max = values[0];
            for (var i = 1; i < values.Length; i++)
                if (values[i].CompareTo(max) > 0)
                    max = values[i];

            return max;
        }

        // As Logger.Debug, but also prints to a special dump stream, if specified
        public static TextWriter Dump = null;

        public static void LogToDump(string str, Logger logger)
        {
            logger.Debug(str);
            Dump?.WriteLine(str);
        }

        // Given value v1 at t1, and value v2 at t2, linearly interpolates (lerps)
        // the value at t.
        // Assumes that t1 <= t <= t2.
        public static float Interpolate(float t1, float v1, float t, float t2, float v2)
        {
            // translate down. t1 is now 0.
            t -= t1;
            t2 -= t1;
            // find what fraction t is on a scale of 0 to t2.
            t /= t2;
            // return the value.
            return v1 * (1 - t) + v2 * t;
        }
    }
}