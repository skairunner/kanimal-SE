using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;
using NLog;

namespace kanimal
{
    // Internal structure
    public struct AnimationData
    {
        public float X, Y, Angle, ScaleX, ScaleY;
    }
    
    public class ScmlReader: Reader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private XmlDocument scml;
        private string scmlpath;
        private Dictionary<string, XmlElement> projectSprites; // quick lookup for the sprites incl in project
        private Dictionary<string, XmlElement> projectFileIdMap; // quick lookup for the anims incl in project
        private TexturePacker textures;

        public ScmlReader(string scmlpath)
        {
            this.scmlpath = scmlpath;
        }

        private void ReadProjectSprites()
        {
            projectSprites = new Dictionary<string, XmlElement>();
            projectFileIdMap = new Dictionary<string, XmlElement>();

            var children = scml.GetElementsByTagName("folder")[0].ChildNodes.GetElements();
            foreach (var element in children)
            {
                projectSprites[element.Attributes["name"].Value] = element;
                projectFileIdMap[element.Attributes["id"].Value] = element;
            }
        }
        
        public override void Read(string outputDir)
        {
            scml = new XmlDocument();
            scml.Load(scmlpath);
            Logger.Info("Reading image files.");
            ReadProjectSprites();
            // Due to scml conventions, our input directory is the same as the scml file's
            var inputDir = Path.Join(scmlpath, "../");
            var sprites = Directory
                .GetFiles(inputDir, "*.png", SearchOption.TopDirectoryOnly)
                .Select(filename => new Tuple<string, Bitmap>(
                    Utilities.WithoutExtension(Path.GetFileName(filename)),
                    new Bitmap(filename)))
                .ToList();
            
            // Also set the output list of sprites
            Sprites = new List<Sprite>();
            Sprites = sprites.Select(sprite => new Sprite {Bitmap = sprite.Item2, Name = sprite.Item1}).ToList();
            
            Logger.Info("Reading build info.");
            textures = new TexturePacker(sprites);
            // Sort packed sprites by name to facilitate build packing later on, bc it has flaky logic
            // and will otherwise fail
            textures.SpriteAtlas.Sort(
                (sprite1, sprite2) => String.Compare(sprite1.Name, sprite2.Name, StringComparison.Ordinal));
            
            // Once texture is packed. reads the atlas to determine build info
            PackBuild(textures);
            
            Logger.Info("Calculating animation info.");
            PackAnim();
            
            Logger.Info($"Successfully read {scmlpath}");
        }

        private void PackBuild(TexturePacker texture)
        {
            BuildData = new KBuild.Build();
            BuildData.Version = 10; // magic number
            SetSymbolsAndFrames(texture.SpriteAtlas);
            BuildData.Name = scml.GetElementsByTagName("entity")[0].Attributes["name"].Value;
            var histogram = texture.GetHistogram();
            var hashTable = new Dictionary<string, int>();

            BuildData.Symbols = new List<KBuild.Symbol>();
            var symbolIndex = -1;
            string lastName = null;

            foreach (var sprite in texture.SpriteAtlas)
            {
                // Only add each unique symbol once
                if (lastName != sprite.Name)
                {
                    var symbol = new KBuild.Symbol();
                    // The hash table caches a KleiHash translation of all sprites.
                    // It may be unnecessary but the original had it, and I don't know if the performance impact is
                    // small enough to remove it.
                    if (!hashTable.ContainsKey(sprite.Name))
                    {
                        hashTable[sprite.Name] = Utilities.KleiHash(sprite.Name);
                    }

                    symbol.Hash = hashTable[sprite.Name];
                    symbol.Path = symbol.Hash;
                    symbol.Color = 0; // no Klei files use color other than 0 so fair assumption is it can be 0
                    // only check in decompile for flag checks flag = 8 for a layered anim (which we won't do)
                    // so should be safe to leave flags = 0
                    // have seen some Klei files in which flags = 1 for some symbols but can't determine what that does
                    symbol.Flags = 0;
                    symbol.FrameCount = histogram[sprite.Name];
                    symbol.Frames = new List<KBuild.Frame>();
                    BuildData.Symbols.Add(symbol);

                    symbolIndex++;
                    lastName = sprite.Name;
                }

                KBuild.Frame frame = new KBuild.Frame();
                frame.SourceFrameNum = Utilities.GetFrameCount(sprite.Name);
                // duration is always 1 because the frames for a symbol always are numbered incrementing by 1
                // (or at least that's why I think it's always 1 in the examples I looked at)
                frame.Duration = 1;
                // this value as read from the file is unused by Klei code and all example files have it set to 0 for all symbols
                frame.BuildImageIndex = 0;
                
                // ReSharper disable PossibleLossOfFraction
                frame.X1 = sprite.X / texture.SpriteSheet.Width;
                frame.X2 = (sprite.X + sprite.Width) / texture.SpriteSheet.Width;
                frame.Y1 = sprite.Y / texture.SpriteSheet.Height;
                frame.Y2 = (sprite.Y + sprite.Height) / texture.SpriteSheet.Height;
                // ReSharper restore PossibleLossOfFraction
                
                // do not set frame.time since it was a calculated property and not actually used in kbild
                frame.PivotWidth = sprite.Width * 2;
                frame.PivotHeight = sprite.Height * 2;
                
                // Find the appropriate pivot from the scml
                var scmlnode = projectSprites[$"{sprite.BaseName}_{frame.SourceFrameNum}"];
                frame.PivotX = -(float.Parse(scmlnode.Attributes["pivot_x"].Value) - 0.5f) * frame.PivotWidth;
                frame.PivotY = (float.Parse(scmlnode.Attributes["pivot_y"].Value) - 0.5f) * frame.PivotHeight;
                BuildData.Symbols[symbolIndex].Frames.Add(frame);
            }
            
            // Finally, flip the key/values to get the build hash table
            BuildHashes = new Dictionary<int, string>();
            foreach (var entry in hashTable)
            {
                BuildHashes[entry.Value] = Utilities.GetSpriteBaseName(entry.Key);
            }
            
            BuildBuildTable(texture.SpriteSheet.Width, texture.SpriteSheet.Height);
        }

        private XmlElement GetMainline(XmlNodeList nodes)
        {
            foreach (var element in nodes.GetElements())
            {
                if (element.Name == "mainline")
                    return element;
            }
            throw new ProjectParseException(
                "SCML format exception: Can't find <mainline> child of <animation>!");
        }

        // Maps ids to timeline elements
        private Dictionary<int, XmlElement> GetTimelineMap(XmlNodeList nodes)
        {
            var map = new Dictionary<int, XmlElement>();
            foreach (var element in nodes.GetElements())
            {
                if (element.Name == "timeline")
                {
                    map[int.Parse(element.Attributes["id"].Value)] = element;
                }
            }

            return map;
        }

        private void PackAnim()
        {
            AnimData = new KAnim.Anim();
            AnimData.Version = 5; // everyone loves magic numbers
            AnimData.Anims = new List<KAnim.AnimBank>();
            AnimHashes = new Dictionary<int, string>();
            
            // reading the scml to get the data you get by counting everything
            SetAggregateData();
            
            var Entity = scml.GetElementsByTagName("entity")[0];
            var animations = Entity.ChildNodes.GetElements();
            var animCount = 0;
            foreach (var anim in animations)
            {
                animCount++;
                if (anim.Name != "animation")
                {
                    throw new ProjectParseException(
                        $"SCML format exception: all children of <entity> must be <animation>, was <{anim.Name}> instead.");
                }

                var bank = new KAnim.AnimBank();
                bank.Name = anim.Attributes["name"].Value;
                bank.Hash = Utilities.KleiHash(bank.Name);
                Logger.Debug($"bank.name={bank.Name}\nhashTable={bank.Hash}");
                
                // weirdly feeble attempt at parsing the interval, leaving it in
                if (!int.TryParse(anim.Attributes["interval"].Value, out var interval))
                {
                    interval = 33;
                }

                bank.Rate = (float)Utilities.MS_PER_S / interval;
                bank.Frames = new List<KAnim.Frame>();

                var timelines = anim.ChildNodes.GetElements();
                var mainline = GetMainline(anim.ChildNodes);
                var timelineMap = GetTimelineMap(anim.ChildNodes);
                var keyframes = mainline.ChildNodes.GetElements();
                var lastDataMap = new Dictionary<int, AnimationData>();
                var frameCount = 0;
                foreach (var frameNode in keyframes)
                {
                    frameCount++;
                    
                    // that will be sent to klei kanim format so we have to match the timeline data to key frames
                    // - this matching will be the part for
                    if (frameNode.Name != "key")
                    {
                        throw new ProjectParseException(
                            $"SCML format exception: all children of <animation> must be <key>, was <{anim.Name}> instead.");
                    }
                    
                    var frame = new KAnim.Frame();
                    frame.Elements = new List<KAnim.Element>();
                    // the elements for this frame will be all the elements
                    // referenced in the object_ref(s) -> their data will be found
                    // in their timeline
                    // note that we need to calculate the animation's overall bounding
                    // box for this frame which will be done by computing locations
                    // of 4 rectangular bounds of each element under transformation
                    // and tracking the max and min of x and y
                    var minX = float.MaxValue;
                    var minY = float.MaxValue;
                    // TODO: Check if never assigning a min/max value is relevant?
                    var maxX = float.MinValue;
                    var maxY = float.MinValue;
                    
                    // look through object refs - will need to maintain list of object refs
                    // because in the end it must be sorted in accordance with the z-index
                    // before appended in correct order to elementsList
                    var object_refs = frameNode.ChildNodes.GetElements();
                    var elementCount = 0;
                    foreach (var object_ref in object_refs)
                    {
                        if (object_ref.Name != "object_ref")
                        {
                            throw new ProjectParseException(
                                $"SCML format exception: all children of <key> must be <object_ref>, was <{object_ref.Name}> instead.");
                        }
                        var element = new KAnim.Element();
                        element.Flags = 0;
                        // spriter does not support changing colors of components
                        // through animation so this can be safely set to 0
                        element.A = 1.0f;
                        element.B = 1.0f;
                        element.G = 1.0f;
                        element.R = 1.0f;
                        // this field is actually unused entirely (it is parsed but ignored)
                        element.Order = 0.0f;
                        // store z Index so later can be reordered
                        element.ZIndex = int.Parse(object_ref.Attributes["z_index"].Value);
                        var timeline_id = int.Parse(object_ref.Attributes["timeline"].Value);
                        
                        // now need to get corresponding timeline object ref
                        var timeline_node = timelineMap[timeline_id];
                        var frame_id = int.Parse(object_ref.Attributes["key"].Value);
                        XmlElement frame_node;
                        try
                        {
                            frame_node = getFrameFromTimeline(timeline_node, frame_id);
                        } catch (ProjectParseException)
                        {
                            continue; // skip this element.
                        }

                        var object_node = frame_node.GetElementsByTagName("object")[0];
                        try
                        {
                            var image_node = projectFileIdMap[object_node.Attributes["file"].Value];
                            var imageName = image_node.Attributes["name"].Value;
                            if (imageName.EndsWith(".png"))
                            {
                                imageName = imageName.Substring(0, imageName.Length - 4);
                            }

                            element.Image = Utilities.KleiHash(Utilities.GetSpriteBaseName(imageName));
                            element.Index = Utilities.GetFrameCount(imageName);
                            // layer doesn't seem to actually be used for anything after it is parsed as a "folder"
                            // but it does need to have an associated string in the hash table so we will just
                            // write layer as the same as the image being used
                            element.Layer = element.Image;
                            // Add this info to the AnimHashes dict
                            AnimHashes[element.Image] = Utilities.GetSpriteBaseName(imageName);
                            AnimHashes[element.Layer] = AnimHashes[element.Image];
                            
                            // spriter animation files don't repeat data if it is unchanged between frames
                            // for an object so we have to track the last know value of the data and use
                            // that if we don't see it
                            // TODO: Spriter actually interpolates values as well, so we should add better logic
                            // so that we get the expected behaviour rather than a sudden jump.
                            float scaleX = 1.0f, scaleY = 1.0f, angle = 0f, xOffset = 0.0f, yOffset = 0.0f;
                            if (object_node.Attributes["scale_x"] != null)
                            {
                                scaleX = float.Parse(object_node.Attributes["scale_x"].Value);
                            }
                            else if (lastDataMap.ContainsKey(timeline_id))
                            {
                                scaleX = lastDataMap[timeline_id].ScaleX;
                            }

                            if (object_node.Attributes["scale_y"] != null)
                            {
                                scaleY = float.Parse(object_node.Attributes["scale_y"].Value);
                            }
                            else if (lastDataMap.ContainsKey(timeline_id))
                            {
                                scaleY = lastDataMap[timeline_id].ScaleY;
                            }

                            if (object_node.Attributes["angle"] != null)
                            {
                                angle = float.Parse(object_node.Attributes["angle"].Value);
                            }
                            else if (lastDataMap.ContainsKey(timeline_id))
                            {
                                angle = lastDataMap[timeline_id].Angle;
                            }

                            if (object_node.Attributes["x"] != null)
                            {
                                xOffset = float.Parse(object_node.Attributes["x"].Value);
                            }
                            else if (lastDataMap.ContainsKey(timeline_id))
                            {
                                xOffset = lastDataMap[timeline_id].X;
                            }

                            if (object_node.Attributes["y"] != null)
                            {
                                yOffset = float.Parse(object_node.Attributes["y"].Value);
                            }
                            else if (lastDataMap.ContainsKey(timeline_id))
                            {
                                yOffset = lastDataMap[timeline_id].Y;
                            }

                            var animdata = new AnimationData
                            {
                                ScaleX = scaleX,
                                ScaleY = scaleY,
                                Angle = angle,
                                X = xOffset,
                                Y = yOffset
                            };
                            lastDataMap[timeline_id] = animdata;
                            element.M5 = xOffset * 2;
                            element.M6 = -yOffset * 2;
                            var angleRad = Math.PI / 180 * angle;
                            var sin = (float) Math.Sin(angleRad);
                            var cos = (float) Math.Cos(angleRad);
                            element.M1 = scaleX * cos;
                            element.M2 = scaleX * -sin;
                            element.M3 = scaleY * sin;
                            element.M4 = scaleY * cos;

                            // calculate transformed bounds of this element
                            // note that we actually need the pivot of the element in order to determine where the
                            // element is located b/c the pivot acts as 0,0 for the x and y offsets
                            // additionally it is necessary b/c rotation is done aroudn the pivot
                            // (mathematically compute this as rotation around the origin just composed with
                            // translating the pivot to and from the origin)
                            var pivotX = float.Parse(image_node.Attributes["pivot_x"].Value);
                            var pivotY = float.Parse(image_node.Attributes["pivot_y"].Value);
                            var width = int.Parse(image_node.Attributes["width"].Value);
                            var height = int.Parse(image_node.Attributes["height"].Value);
                            pivotX *= width;
                            pivotY *= height;
                            var centerX = pivotX + xOffset;
                            var centerY = pivotY + yOffset;
                            var x2 = xOffset + width;
                            var y2 = yOffset + width;
                            var p1 = new PointF(xOffset, yOffset);
                            var p2 = new PointF(x2, yOffset);
                            var p3 = new PointF(x2, y2);
                            var p4 = new PointF(xOffset, y2);
                            p1 = p1.RotateAround(centerX, centerY, (float) angleRad, scaleX, scaleY);
                            p2 = p2.RotateAround(centerX, centerY, (float) angleRad, scaleX, scaleY);
                            p3 = p3.RotateAround(centerX, centerY, (float) angleRad, scaleX, scaleY);
                            p4 = p4.RotateAround(centerX, centerY, (float) angleRad, scaleX, scaleY);
                            minX = Utilities.Min(minX, p1.X, p2.X, p3.X, p4.X);
                            minY = Utilities.Min(minY, p1.Y, p2.Y, p3.Y, p4.Y);
                            frame.Elements.Add(element);
                            elementCount++;
                        }
                        catch (Exception e)
                        {
                            throw e;
                        }
                    }
                    
                    frame.Elements.Sort((e1, e2) => -e1.ZIndex.CompareTo(e2.ZIndex));
                    frame.X = (minX + maxX) / 2f;
                    frame.Y = (minY + maxY) / 2f;
                    frame.Width = maxX - minX;
                    frame.Height = maxY - minY;
                    frame.ElementCount = elementCount;
                    bank.Frames.Add(frame);
                }

                bank.FrameCount = frameCount;
                AnimData.Anims.Add(bank);
            }

            AnimData.AnimCount = animCount;
        }

        private XmlElement getFrameFromTimeline(XmlElement timeline, int frameId)
        {
            foreach (var key in timeline.ChildNodes.GetElements())
            {
                if (key.Name != "key")
                {
                    throw new ProjectParseException(
                        $"SCML format exception: all children of <timeline> must be <key>, was <{key.Name}> instead.");
                }

                if (int.Parse(key.Attributes["id"].Value) == frameId)
                {
                    return key;
                }
            }
            throw new ProjectParseException(
                $"Expected to find frame {frameId} in timeline "
            + $"{timeline.Attributes["id"]} of anim {timeline.ParentNode.Attributes["name"].Value}");
        }

        private void SetAggregateData()
        {
            var animRoot = scml.GetElementsByTagName("entity")[0];
            var animations = animRoot.ChildNodes;
            var maxVisibleSymbolFrames = 0;
            foreach (var child in animations)
            {
                if (!(child is XmlElement))
                {
                    Logger.Debug("Skipping non-element child of <entity>");
                    continue;
                }

                var anim = (XmlElement) child;
                if (anim.Name != "animation")
                {
                    throw new ProjectParseException(
                        $"SCML format exception: all children of <entity> must be <animation>, was <{anim.Name}> instead.");
                }

                var timelines = anim.ChildNodes;
                var mainline = anim.GetElementsByTagName("mainline")[0];
                var keyframes = mainline.ChildNodes;

                for (int frameIndex = 0; frameIndex < keyframes.Count; frameIndex++)
                {
                    if (!(keyframes[frameIndex] is XmlElement))
                    {
                        Logger.Debug("Skipping non-element child of <mainline>");
                        continue;
                    }

                    var keyframe = (XmlElement) keyframes[frameIndex];
                    if (keyframe.Name != "key")
                    {
                        throw new ProjectParseException(
                            $"SCML format exception: all children of <animation> should be <key>, was <{keyframe.Name}> instead.");
                    }

                    var objects = keyframe.ChildNodes;
                    foreach (var obj in objects)
                    {
                        if (!(obj is XmlElement))
                        {
                            Logger.Debug("Skipping non-element child of <key>");
                            continue;
                        }

                        var element = (XmlElement) obj;
                        if (element.Name != "object_ref")
                        {
                            throw new ProjectParseException(
                                $"SCML format exception: all children of <key> should be <object_ref>, was <{element.Name}> instead.");
                        }

                        if (objects.Count > maxVisibleSymbolFrames)
                        {
                            maxVisibleSymbolFrames = objects.Count;
                        }
                    }
                }
            }

            AnimData.AnimCount = animations.Count;
            AnimData.FrameCount = 0;
            AnimData.ElementCount = 0;
            AnimData.MaxVisibleSymbolFrames = maxVisibleSymbolFrames;
        }
        
        private void SetSymbolsAndFrames(List<PackedSprite> sprites)
        {
            BuildData.SymbolCount = 0;
            BuildData.FrameCount = 0;
            foreach (var sprite in sprites)
            {
                BuildData.FrameCount++;

                var frameCount = Utilities.GetFrameCount(sprite.Name);
                if (frameCount == 0)
                {
                    BuildData.SymbolCount++;
                }
            }
        }
    }
}