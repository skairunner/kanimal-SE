using System.IO;

namespace kanimal
{
    public static class Kanimal
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static void ToScml(string imgPathStr, string buildPathStr, string animPathStr, string outputDir)
        {
            var outputPath = Path.GetFullPath(outputDir);
            // Ensure output dirs exist
            Directory.CreateDirectory(outputPath);

            Logger.Info($"Output path is \"{outputPath}\"");

            var reader = new KanimReader(
                new FileStream(buildPathStr, FileMode.Open),
                new FileStream(animPathStr, FileMode.Open),
                new FileStream(imgPathStr, FileMode.Open));

            reader.Read(outputPath);

            Logger.Info("Writing...");
            var writer = new ScmlWriter(reader);

            var outputFilePath = Path.Join(outputPath, $"{reader.BuildData.Name}.scml");
            writer.Save(outputFilePath);
            writer.SaveSprites(outputPath);

            Logger.Info("Done.");
        }

        public static void ToKanim(string projectStr, string outputDir)
        {
            Directory.CreateDirectory(outputDir);

            Logger.Info($"Output path is \"{outputDir}\"");
            var reader = new ScmlReader(projectStr);
            reader.Read(outputDir);
            var writer = new KanimWriter(reader);
            writer.Save(outputDir);
        }

        public static void ScmlToScml(string scmlPath, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            var reader = new ScmlReader(scmlPath);
            reader.Read(outputDirectory);
            var writer = new ScmlWriter(reader);
            writer.Save(Path.Join(outputDirectory, reader.BuildData.Name + ".scml"));
            writer.SaveSprites(outputDirectory);
        }

        public static void KanimToKAnim(string imgPathStr, string buildPathStr, string animPathStr, string outputDir)
        {
            var outputPath = Path.GetFullPath(outputDir);
            // Ensure output dirs exist
            Directory.CreateDirectory(outputPath);

            Logger.Info($"Output path is \"{outputPath}\"");

            var reader = new KanimReader(
                new FileStream(buildPathStr, FileMode.Open),
                new FileStream(animPathStr, FileMode.Open),
                new FileStream(imgPathStr, FileMode.Open));
            reader.Read(outputDir);

            Logger.Info("Writing...");
            var writer = new KanimWriter(reader);

            writer.Save(outputPath);
            writer.SaveSprites(outputPath);

            Logger.Info("Done.");
        }
    }
}