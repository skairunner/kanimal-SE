using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Text;
using NLog;

namespace kanimal
{
    public class KanimReader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private Stream bild, anim;
        
        private Bitmap image;
        private Dictionary<int, string> BuildHashes, AnimHashes;
        private KBuild.Build BuildData;
        private List<KBuild.Row> BuildTable;
        private KAnim.Anim AnimData;
        private Dictionary<string, int> AnimIdMap;

        private bool bildParsed = false, animParsed = false;

        public KanimReader(Stream bild, Stream anim, Stream img)
        {
            this.bild = bild;
            this.anim = anim;
            this.image = new Bitmap(img);
        }

        // Reads the entire build.bytes file
        public void ReadBuildData()
        {
            var reader = new BinaryReader(bild);
            
            try
            {
                VerifyHeader("BILD", reader);
            }
            catch (HeaderAssertException e)
            {
                Logger.Error(e);
                Logger.Error("Did you provide the right build.bytes file?");
                Environment.Exit((int)ExitCodes.IncorrectHeader);
            }
            
            ReadSymbols(reader);
            ReadBuildHashes(reader);
            BuildBuildTable(reader);
            
            Utilities.LogDebug(Logger, BuildData);
            Utilities.LogDebug(Logger, BuildHashes);
            Utilities.LogDebug(Logger, BuildTable);
        }
        
        // Reads the symbols and frames
        private void ReadSymbols(BinaryReader reader)
        {
            KBuild.Build buildData = new KBuild.Build
            {
                Version = reader.ReadInt32(),
                SymbolCount = reader.ReadInt32(),
                FrameCount = reader.ReadInt32(),
                Name = reader.ReadString(),
                Symbols = new List<KBuild.Symbol>()
            };

            for (int i = 0; i < buildData.Symbols.Count; i++)
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

                int time = 0;
                for (int j = 0; j < symbol.FrameCount; j++)
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
            for (int i = 0; i < numHashes; i++)
            {
                var hash = reader.ReadInt32();
                var str = reader.ReadString();
                buildHashes[hash] = str;
            }

            BuildHashes = buildHashes;
        }

        private void BuildBuildTable(BinaryReader reader)
        {
            var imgW = image.Width;
            var imgH = image.Height;
            var buildTable = new List<KBuild.Row>();
            foreach (var symbol in BuildData.Symbols)
            {
                foreach (var frame in symbol.Frames)
                {
                    var row = new KBuild.Row
                    {
                        Build = BuildData,
                        Name = BuildHashes[symbol.Hash],
                        Index = frame.SourceFrameNum,
                        Hash = symbol.Hash,
                        Time = frame.Time,
                        Duration = frame.Duration,
                        X1 = frame.X1 * imgW,
                        Y1 = (1f - frame.Y1) * imgH,
                        X2 = frame.X2 * imgW,
                        Y2 = (1 - frame.Y2) * imgH,
                        Width = (frame.X2 - frame.X1) * imgW,
                        Height = (frame.Y2 - frame.Y1) * imgH,
                        PivotX = frame.PivotX,
                        PivotY = frame.PivotY,
                        PivotHeight = frame.PivotHeight,
                        PivotWidth = frame.PivotWidth
                    };
                    buildTable.Add(row);
                }
            }

            BuildTable = buildTable;
        }
        
        // Unpacks the spritesheet into individual sprites, written to the output directory
        public void ExportTextures(string outputPath)
        {
            foreach (var row in BuildTable)
            {
                Logger.Debug($"{row.X1} {row.Height - row.Y1} {row.Width} {row.Height}    {image.Width} {image.Height}");
                var sprite = image.Clone(new Rectangle((int) row.X1, (int)(image.Height - row.Y1), (int)row.Width, (int)row.Height),
                    image.PixelFormat);
                var filename = $"{row.Name}_{row.Index}.png";
                sprite.Save(Path.Join(outputPath, filename));
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
                Environment.Exit((int)ExitCodes.IncorrectHeader);
            }

            ParseAnims(reader);
            ReadAnimHashes(reader);
            ReadAnimIds(reader);
            
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

            for (int i = 0; i < animData.AnimCount; i++)
            {
                var name = reader.ReadString();
                var hash = reader.ReadInt32();
                Logger.Debug($"anim with name={name} but hash={hash}");
                var bank = new KAnim.AnimBank
                {
                    Name = name,
                    Hash = hash,
                    Rate = reader.ReadSingle(),
                    FrameCount = reader.ReadInt32(),
                    Frames = new List<KAnim.Frame>()
                };

                for (int j = 0; j < bank.FrameCount; j++)
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
                    Logger.Debug($"animation frame=({frame.X},{frame.Y},{frame.Width},{frame.Height}");

                    for (int k = 0; k < frame.ElementCount; k++)
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
                        
                        Logger.Debug($"internal=({element.M5},{element.M6})");
                        Logger.Debug($"layer={element.Layer}");

                        frame.Elements.Add(element);
                    }
                    
                    Logger.Debug("");
                    bank.Frames.Add(frame);
                }

                animData.Anims.Add(bank);
            }

            animData.MaxVisibleSymbolFrames = reader.ReadInt32();

            AnimData = animData;
        }

        private void ReadAnimHashes(BinaryReader reader)
        {
            var animHashes = new Dictionary<int, string>();

            int numHashes = reader.ReadInt32();
            for (int i = 0; i < numHashes; i++)
            {
                var hash = reader.ReadInt32();
                var text = reader.ReadString();
                animHashes[hash] = text;
            }

            AnimHashes = animHashes;
        }

        private void ReadAnimIds(BinaryReader reader)
        {
            var animIdMap = new Dictionary<string, int>();

            var key = 0;
            foreach (var bank in AnimData.Anims)
            {
                foreach (var frame in bank.Frames)
                {
                    foreach (var element in frame.Elements)
                    {
                        var name = $"{AnimHashes[element.Image]}_{element.Index}_{AnimHashes[element.Layer]}";
                        if (!AnimIdMap.ContainsKey(name))
                        {
                            AnimIdMap[name] = key++;
                        }
                    }
                }
            }

            AnimIdMap = animIdMap;
        }

        private void VerifyHeader(string expectedHeader, BinaryReader buffer)
        {
            var actualHeader = Encoding.ASCII.GetString(buffer.ReadBytes(expectedHeader.Length));

            if (expectedHeader != actualHeader)
            {
                throw new HeaderAssertException(
                    $"Expected header \"{expectedHeader}\" but got \"{actualHeader}\" instead.",
                    expectedHeader,
                    actualHeader);
            }
        }
    }
}