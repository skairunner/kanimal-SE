using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Text;
using NLog;

namespace kanimal
{
    public class KanimReader : Reader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private Stream _build, anim;

        private Bitmap image;
        private Dictionary<string, int> AnimIdMap;

        public KanimReader(Stream build, Stream anim, Stream img)
        {
            _build = build;
            this.anim = anim;
            try
            {
                image = new Bitmap(img);
            } catch (ArgumentException e)
            {
                Logger.Fatal("The given \"img\" stream is not a valid image file. Original exception is as follows:");
                ExceptionDispatchInfo.Capture(e).Throw();
            }
        }

        // Reads the entire build.bytes file
        public void ReadBuildData()
        {
            var reader = new BinaryReader(_build);

            try
            {
                VerifyHeader("BILD", reader);
            }
            catch (HeaderAssertException e)
            {
                Logger.Error(e);
                Logger.Error("Did you provide the right build.bytes file?");
                Environment.Exit((int) ExitCodes.IncorrectHeader);
            }

            ReadSymbols(reader);
            ReadBuildHashes(reader);
            BuildBuildTable(image.Width, image.Height);

            Utilities.LogDebug(Logger, BuildData);
            Utilities.LogDebug(Logger, BuildHashes);
            Utilities.LogDebug(Logger, BuildTable);
        }

        // Reads the symbols and frames
        private void ReadSymbols(BinaryReader reader)
        {
            var buildData = new KBuild.Build
            {
                Version = reader.ReadInt32(),
                SymbolCount = reader.ReadInt32(),
                FrameCount = reader.ReadInt32(),
                Name = reader.ReadPString(),
                Symbols = new List<KBuild.Symbol>()
            };
            Utilities.LogToDump(
                "=== BUILD FILE ===\n" +
                $"{buildData.Name}\n" +
                $"  Version: {buildData.Version}\n" +
                $"  # symbols: {buildData.SymbolCount}\n" +
                $"  # frames: {buildData.FrameCount}", Logger);
            Utilities.LogToDump(
                "\n<Symbols>"
                , Logger);
            for (var i = 0; i < buildData.SymbolCount; i++)
            {
                var symbol = new KBuild.Symbol
                {
                    Hash = reader.ReadInt32(),
                    Path = buildData.Version > 9 ? reader.ReadInt32() : 0,
                    Color = reader.ReadInt32(),
                    Flags = reader.ReadInt32(),
                    FrameCount = reader.ReadInt32(),
                    Frames = new List<KBuild.Frame>()
                };
                Utilities.LogToDump(
                    $"  Symbol: hash {symbol.Hash}, path {symbol.Path}, frame count {symbol.FrameCount}", Logger);

                var time = 0;
                for (var j = 0; j < symbol.FrameCount; j++)
                {
                    var frame = new KBuild.Frame
                    {
                        SourceFrameNum = reader.ReadInt32(),
                        Duration = reader.ReadInt32(),
                        BuildImageIndex = reader.ReadInt32(),
                        PivotX = reader.ReadSingle(),
                        PivotY = reader.ReadSingle(),
                        PivotWidth = reader.ReadSingle(),
                        PivotHeight = reader.ReadSingle(),
                        X1 = reader.ReadSingle(),
                        Y1 = reader.ReadSingle(),
                        X2 = reader.ReadSingle(),
                        Y2 = reader.ReadSingle(),
                        Time = time
                    };
                    Utilities.LogToDump(
                        $"    Frame {frame.SourceFrameNum}: image {frame.BuildImageIndex} for duration {frame.Duration}, BB ({frame.X1}, {frame.Y1}) - ({frame.X2}, {frame.Y2}), pivot ({frame.PivotX},{frame.PivotY})",
                        Logger);
                    time += frame.Duration;
                    symbol.Frames.Add(frame);
                }

                buildData.Symbols.Add(symbol);

                BuildData = buildData;
            }
        }

        // Reads the hashes and related strings
        private void ReadBuildHashes(BinaryReader reader)
        {
            var buildHashes = new Dictionary<int, string>();
            var numHashes = reader.ReadInt32();
            Utilities.LogToDump($"\n<Hashtable {numHashes}>", Logger);
            for (var i = 0; i < numHashes; i++)
            {
                var hash = reader.ReadInt32();
                var str = reader.ReadPString();
                Utilities.LogToDump($"  {hash} -> \"{str}\"", Logger);
                buildHashes[hash] = str;
            }

            BuildHashes = buildHashes;
        }

        // Unpacks the spritesheet into individual sprites and stores in memory.
        public void ExportTextures()
        {
            Utilities.LogToDump("\n\n=== SPRITE SHEET ===", Logger);
            Sprites = new List<Sprite>();
            foreach (var row in BuildTable)
            {
                var y = (int) (image.Height - row.Y1);
                Utilities.LogToDump($"  Sprite \"{row.Name}_{row.Index}\" @ {row.X1} {y}, {row.Width}x{row.Height}",
                    Logger);
                var sprite = image.Clone(new Rectangle((int) row.X1, y, (int) row.Width, (int) row.Height),
                    image.PixelFormat);
                Sprites.Add(new Sprite
                {
                    Name = $"{row.Name}_{row.Index}",
                    Bitmap = sprite
                });
            }
        }

        public void ReadAnimData()
        {
            var reader = new BinaryReader(anim);

            try
            {
                VerifyHeader("ANIM", reader);
            }
            catch (HeaderAssertException e)
            {
                Logger.Error(e);
                Logger.Error("Did you provide the right anim.bytes file?");
                Environment.Exit((int) ExitCodes.IncorrectHeader);
            }

            Utilities.LogToDump("\n\n=== ANIM FILE ===", Logger);
            ParseAnims(reader);
            ReadAnimHashes(reader);
            ReadAnimIds();

            Utilities.LogDebug(Logger, AnimData);
            Utilities.LogDebug(Logger, AnimHashes);
            Utilities.LogDebug(Logger, AnimIdMap);
        }

        private void ParseAnims(BinaryReader reader)
        {
            var animData = new KAnim.Anim
            {
                Version = reader.ReadInt32(),
                ElementCount = reader.ReadInt32(),
                FrameCount = reader.ReadInt32(),
                AnimCount = reader.ReadInt32(),
                Anims = new List<KAnim.AnimBank>()
            };

            Utilities.LogToDump(
                $"  Version: {animData.Version}\n" +
                $"  # elements: {animData.ElementCount}\n" +
                $"  # frames: {animData.FrameCount}\n" +
                $"  # anims: {animData.AnimCount}\n" +
                "\n<Anims>", Logger);

            for (var i = 0; i < animData.AnimCount; i++)
            {
                var name = reader.ReadPString();
                var hash = reader.ReadInt32();
                var bank = new KAnim.AnimBank
                {
                    Name = name,
                    Hash = hash,
                    Rate = reader.ReadSingle(),
                    FrameCount = reader.ReadInt32(),
                    Frames = new List<KAnim.Frame>()
                };

                Utilities.LogToDump(
                    $"  Anim \"{bank.Name}\" (hash {bank.Hash}): {bank.FrameCount} frames @ {bank.Rate} fps", Logger);

                for (var j = 0; j < bank.FrameCount; j++)
                {
                    var frame = new KAnim.Frame
                    {
                        X = reader.ReadSingle(),
                        Y = reader.ReadSingle(),
                        Width = reader.ReadSingle(),
                        Height = reader.ReadSingle(),
                        ElementCount = reader.ReadInt32(),
                        Elements = new List<KAnim.Element>()
                    };
                    Utilities.LogToDump(
                        $"    Frame @ ({frame.X}, {frame.Y}) is {frame.Width}x{frame.Height}. {frame.ElementCount} sub-elements.",
                        Logger);

                    for (var k = 0; k < frame.ElementCount; k++)
                    {
                        var element = new KAnim.Element
                        {
                            Image = reader.ReadInt32(),
                            Index = reader.ReadInt32(),
                            Layer = reader.ReadInt32(),
                            Flags = reader.ReadInt32(),
                            A = reader.ReadSingle(),
                            B = reader.ReadSingle(),
                            G = reader.ReadSingle(),
                            R = reader.ReadSingle(),
                            M1 = reader.ReadSingle(),
                            M2 = reader.ReadSingle(),
                            M3 = reader.ReadSingle(),
                            M4 = reader.ReadSingle(),
                            M5 = reader.ReadSingle(),
                            M6 = reader.ReadSingle(),
                            Order = reader.ReadSingle()
                        };
                        Utilities.LogToDump(
                            $"      Sub-element #{element.Index} is {element.Image} (\"{BuildHashes[element.Image]}\") @ layer {element.Layer}\n" +
                            $"        Matrix: ({element.M1} {element.M2} {element.M3} {element.M4}), translate {element.M5} {element.M6}. Order {element.Order}",
                            Logger);

                        frame.Elements.Add(element);
                    }

                    bank.Frames.Add(frame);
                }

                animData.Anims.Add(bank);
            }

            animData.MaxVisibleSymbolFrames = reader.ReadInt32();
            Utilities.LogToDump($"  Max visible frames: {animData.MaxVisibleSymbolFrames}", Logger);

            AnimData = animData;
        }

        private void ReadAnimHashes(BinaryReader reader)
        {
            var animHashes = new Dictionary<int, string>();

            var numHashes = reader.ReadInt32();
            Utilities.LogToDump($"\n<Anim hashes {numHashes}>", Logger);
            for (var i = 0; i < numHashes; i++)
            {
                var hash = reader.ReadInt32();
                var text = reader.ReadPString();
                Utilities.LogToDump($"  {hash} -> \"{text}\"", Logger);
                animHashes[hash] = text;
            }

            AnimHashes = animHashes;
        }

        private void ReadAnimIds()
        {
            var animIdMap = new Dictionary<string, int>();

            Utilities.LogToDump("\n<Anim ids>", Logger);
            var key = 0;
            foreach (var bank in AnimData.Anims)
            foreach (var frame in bank.Frames)
            foreach (var element in frame.Elements)
            {
                var name = $"{AnimHashes[element.Image]}_{element.Index}_{AnimHashes[element.Layer]}";
                if (!animIdMap.ContainsKey(name))
                {
                    animIdMap[name] = key;
                    Utilities.LogToDump($"  {key} -> \"{name}\"", Logger);
                    key += 1;
                }
            }

            AnimIdMap = animIdMap;
        }

        private void VerifyHeader(string expectedHeader, BinaryReader buffer)
        {
            var actualHeader = Encoding.ASCII.GetString(buffer.ReadBytes(expectedHeader.Length));

            if (expectedHeader != actualHeader)
                throw new HeaderAssertException(
                    $"Expected header \"{expectedHeader}\" but got \"{actualHeader}\" instead.",
                    expectedHeader,
                    actualHeader);
        }

        public override void Read()
        {
            Logger.Info("Parsing build data.");
            ReadBuildData();

            Logger.Info("Importing textures.");
            ExportTextures();

            Logger.Info("Parsing animation data.");
            ReadAnimData();
        }

        public override Bitmap GetSpriteSheet()
        {
            return image;
        }
    }
}