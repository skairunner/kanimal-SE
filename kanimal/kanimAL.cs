using System;
using System.Drawing.Imaging;
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

            reader.read(outputPath);

            Logger.Info("Writing...");
            var writer = new ScmlWriter(reader);

            var outputFilePath = Path.Join(outputPath, $"{reader.BuildData.Name}.scml");
            writer.Save(outputFilePath);
            writer.SaveFiles(outputPath);
            
            Logger.Info("Done.");
        }

        public static void ScmlToScml(string scmlpath, string outputdir)
        {
            Directory.CreateDirectory(outputdir);
            var reader = new ScmlReader(scmlpath);
            reader.read(outputdir);
            var writer = new ScmlWriter(reader);
            writer.Save(Path.Join(outputdir, reader.BuildData.Name + ".scml"));
            writer.SaveFiles(outputdir);
        }
    }
}