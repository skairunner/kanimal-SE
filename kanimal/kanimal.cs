using System;
using System.IO;
    
namespace kanimal
{
    public static class Kanimal
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        
        public static void ToScml(string imgPathStr, string buildPathStr, string animPathStr, string outputDir)
        {
            if (!BitConverter.IsLittleEndian)
            {
                Logger.Warn("You are on an operating system that uses big endian byte ordering. kanimAL results may not be correct.");
            }
            
            var outputPath = Path.GetFullPath(outputDir);
            // Ensure output dirs exist
            Directory.CreateDirectory(outputPath);

            Logger.Info($"Outputting path is \"{outputPath}\"");

            Logger.Info("Parsing build data.");
            var reader = new KanimReader(
                new FileStream(buildPathStr, FileMode.Open), 
                new FileStream(animPathStr, FileMode.Open),
                new FileStream(imgPathStr, FileMode.Open));
            
            Logger.Info("Parsing build data.");
            reader.ReadBuildData();
            
            Logger.Info("Exporting textures.");
            reader.ExportTextures(outputDir);
            
            Logger.Info("Parsing animation data.");
            reader.ReadAnimData();
            
            Logger.Info("Writing...");
            
        }
    }
}