using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using NLog;

namespace kanimal
{
    // TODO: assemble spritesheet from Sprites list & build instead of assuming it's been dealt with
    public class KanimWriter : Writer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private Bitmap spritesheet;

        public KanimWriter(Reader reader)
        {
            BuildData = reader.BuildData;
            BuildTable = reader.BuildTable;
            BuildHashes = reader.BuildHashes;
            AnimData = reader.AnimData;
            AnimHashes = reader.AnimHashes;
            Sprites = reader.Sprites;
            spritesheet = reader.GetSpriteSheet();
        }

        public override OutputFiles Save()
        {
            var files = new OutputFiles();
            var build = files[$"{BuildData.Name}_build.bytes"] = new MemoryStream();
            WriteBuild(build);
            var anim = files[$"{BuildData.Name}_anim.bytes"] = new MemoryStream();
            WriteAnim(anim);
            var png = files[$"{BuildData.Name}.png"] = new MemoryStream();
            spritesheet.Save(png, ImageFormat.Png);

            return files;
        }

        public override void SaveToDir(string path)
        {
            Save().Yeet(path);
        }

        public void WriteBuild(Stream output)
        {
            // BinaryWriter/BinaryReader are always little endian. Which is good because kanim format is also little endian.
            var writer = new BinaryWriter(output);
            writer.Write("BILD".ToCharArray());
            writer.Write(10); // build version
            Logger.Debug("version=10");
            writer.Write(BuildData.Symbols.Count);
            Logger.Debug($"symbols={BuildData.Symbols.Count}");
            writer.Write(BuildData.FrameCount);
            Logger.Debug($"frames={BuildData.FrameCount}");
            writer.WritePString(BuildData.Name);
            Logger.Debug($"name={BuildData.Name}");

            for (var i = 0; i < BuildData.Symbols.Count; i++)
            {
                var symbol = BuildData.Symbols[i];
                Logger.Debug(
                    $"symbol {i}=({symbol.Hash},{symbol.Path},{symbol.Color},{symbol.Flags},{symbol.FrameCount})");
                writer.Write(symbol.Hash);
                writer.Write(symbol.Path);
                writer.Write(symbol.Color);
                writer.Write(symbol.Flags);
                writer.Write(symbol.Frames.Count);

                for (var j = 0; j < symbol.Frames.Count; j++)
                {
                    var frame = symbol.Frames[j];
                    writer.Write(frame.SourceFrameNum);
                    writer.Write(frame.Duration);
                    writer.Write(frame.BuildImageIndex);
                    writer.Write(frame.PivotX);
                    writer.Write(frame.PivotY);
                    writer.Write(frame.PivotWidth);
                    writer.Write(frame.PivotHeight);
                    writer.Write(frame.X1);
                    writer.Write(frame.Y1);
                    writer.Write(frame.X2);
                    writer.Write(frame.Y2);
                }
            }

            writer.Write(BuildHashes.Count);
            foreach (var entry in BuildHashes)
            {
                Logger.Debug($"{entry.Key}={entry.Value}");
                writer.Write(entry.Key);
                writer.WritePString(entry.Value);
            }

            Logger.Debug("=== end build ===");
        }

        public void WriteAnim(Stream output)
        {
            var writer = new BinaryWriter(output);
            writer.Write("ANIM".ToCharArray());
            // simply read through built ANIM data structure and write out the properties
            writer.Write(AnimData.Version);
            writer.Write(AnimData.ElementCount);
            writer.Write(AnimData.FrameCount);
            writer.Write(AnimData.AnimCount);
            foreach (var bank in AnimData.Anims)
            {
                writer.WritePString(bank.Name);
                writer.Write(bank.Hash);
                writer.Write(bank.Rate);
                writer.Write(bank.FrameCount);
                foreach (var frame in bank.Frames)
                {
                    writer.Write(frame.X);
                    writer.Write(frame.Y);
                    writer.Write(frame.Width);
                    writer.Write(frame.Height);
                    writer.Write(frame.ElementCount);
                    foreach (var element in frame.Elements)
                    {
                        writer.Write(element.ImageHash);
                        writer.Write(element.Index);
                        writer.Write(element.Layer);
                        writer.Write(element.Flags);
                        writer.Write(element.A);
                        writer.Write(element.B);
                        writer.Write(element.G);
                        writer.Write(element.R);
                        writer.Write(element.M1);
                        writer.Write(element.M2);
                        writer.Write(element.M3);
                        writer.Write(element.M4);
                        writer.Write(element.M5);
                        writer.Write(element.M6);
                        writer.Write(element.Order);
                    }
                }
            }

            writer.Write(AnimData.MaxVisibleSymbolFrames);
            writer.Write(AnimHashes.Count);
            foreach (var entry in AnimHashes)
            {
                Logger.Debug($"{entry.Key}={entry.Value}");
                writer.Write(entry.Key);
                writer.WritePString(entry.Value);
            }
        }
    }
}