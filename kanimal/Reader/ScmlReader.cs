using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Xml;
using kanimal.KAnim;
using kanimal.KBuild;
using NLog;
using Frame = kanimal.KBuild.Frame;

namespace kanimal
{
    // Internal structure
    public struct AnimationData
    {
        public float X, Y, Angle, ScaleX, ScaleY;
    }

    public class ScmlReader : Reader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private XmlDocument scml;
        private Dictionary<SpriteName, XmlElement> projectSprites; // quick lookup for the sprites incl in project
        private Dictionary<string, XmlElement> projectFileIdMap; // quick lookup for the anims incl in project
        private TexturePacker textures;
        private Dictionary<Filename, Bitmap> inputSprites; // The keys in this dictionary are filenames, *with* the file extension, if it exists.

        public bool AllowMissingSprites = true;
        public bool AllowInFramePivots = true;
        public bool InterpolateMissingFrames = false;
        public bool Debone = false;

        public ScmlReader(Stream scmlStream, Dictionary<Filename, Bitmap> sprites)
        {
            scml = new XmlDocument();
            scml.Load(scmlStream);
            inputSprites = sprites;
        }
        
        public ScmlReader(string scmlpath)
        {
            scml = new XmlDocument();
            try
            {
                scml.Load(scmlpath);
            }
            catch (ArgumentNullException e)
            {
                Logger.Fatal($"You must specify a path to load the SCML from. Original exception is as follows:");
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            // Due to scml conventions, our input directory is the same as the scml file's
            var inputDir = Path.Join(scmlpath, "../");
            inputSprites = new Dictionary<Filename, Bitmap>();
            foreach (var filepath in Directory.GetFiles(inputDir, "*.png", SearchOption.TopDirectoryOnly))
            {
                inputSprites[Filename.FromPath(filepath)] = new Bitmap(filepath);
            }
        }

        private void ReadProjectSprites()
        {
            projectSprites = new Dictionary<SpriteName, XmlElement>();
            projectFileIdMap = new Dictionary<string, XmlElement>();

            var children = scml.GetElementsByTagName("folder")[0].ChildNodes.GetElements();
            foreach (var element in children)
            {
                projectSprites[SpriteName.FromFilename(element.Attributes["name"].Value)] = element;
                projectFileIdMap[element.Attributes["id"].Value] = element;
            }
        }

        public override void Read()
        {
            // process converters first
            if (InterpolateMissingFrames)
            {
                try
                {
                    scml = new KeyFrameInterpolateProcessor().Process(scml); // replace the scml with fully keyframed scml
                }
                catch (Exception e)
                {
                    Logger.Fatal($"Failed to interpolate in-between frames. Original exception is as follows:");
                    ExceptionDispatchInfo.Capture(e).Throw();
                }
            }
            if (Debone)
            {
                try
                {
                    scml = new DebonerProcessor().Process(scml); // replace the scml with deboned scml
                }
                catch (Exception e)
                {
                    Logger.Fatal($"Failed to debone the scml document. Original exception is as follows:");
                    ExceptionDispatchInfo.Capture(e).Throw();
                }
            }

            Logger.Info("Reading image files.");
            ReadProjectSprites();

            // Only use the sprites that are included in the project
            List<SpriteName> allSprites = inputSprites.Select(sprite => sprite.Key.ToSpriteName()).ToList();
            inputSprites = inputSprites.Where(sprite => projectSprites.ContainsKey(sprite.Key.ToSpriteName())).ToDictionary(x => x.Key, x => x.Value);
            List<SpriteName> usedSprites = inputSprites.Select(sprite => sprite.Key.ToSpriteName()).ToList();

            List<string> unusedSprites = allSprites.FindAll(sprite => !usedSprites.Contains(sprite)).Select(sprite => sprite.ToFilename().ToString()).ToList();
            if (unusedSprites.Count > 0)
            {
                Logger.Warn($"There were unused sprites in the SCML project folder: {unusedSprites.Join()}. Did you forget to included these in the SCML file? You must manually add in files that are part of a symbol_override if they aren't explicitly placed into any animations in the SCML. ");
            }

            // Also set the output list of sprites
            Sprites = new List<Sprite>();
            Sprites = inputSprites.Select(sprite => new Sprite {Bitmap = sprite.Value, Name = sprite.Key.ToSpriteName()}).ToList();

            Logger.Info("Reading build info.");
            textures = new TexturePacker(inputSprites.Select(
                    s => new Tuple<SpriteName, Bitmap>(s.Key.ToSpriteName(), s.Value))
                .ToList());
            // Sort packed sprites by name to facilitate build packing later on, bc it has flaky logic
            // and will otherwise fail
            textures.SpriteAtlas.Sort(
                (sprite1, sprite2) => string.Compare(sprite1.SpriteName.Value, sprite2.SpriteName.Value, StringComparison.Ordinal));

            // Once texture is packed. reads the atlas to determine build info
            PackBuild(textures);

            Logger.Info("Calculating animation info.");
            PackAnim();
        }

        public override Bitmap GetSpriteSheet()
        {
            return textures.SpriteSheet;
        }

        private void PackBuild(TexturePacker texture)
        {
            BuildData = new Build();
            BuildData.Version = 10; // magic number
            SetSymbolsAndFrames(texture.SpriteAtlas);
            BuildData.Name = scml.GetElementsByTagName("entity")[0].Attributes["name"].Value;
            var histogram = texture.GetHistogram();
            var hashTable = new Dictionary<SpriteBaseName, int>();

            BuildData.Symbols = new List<Symbol>();
            var symbolIndex = -1;
            SpriteBaseName lastName = null;

            foreach (var sprite in texture.SpriteAtlas)
            {
                // Only add each unique symbol once
                if (lastName != sprite.BaseName)
                {
                    var symbol = new Symbol();
                    // The hash table caches a KleiHash translation of all sprites.
                    // It may be unnecessary but the original had it, and I don't know if the performance impact is
                    // small enough to remove it.
                    if (!hashTable.ContainsKey(sprite.BaseName))
                        hashTable[sprite.BaseName] = sprite.BaseName.KleiHashed;

                    symbol.Hash = hashTable[sprite.BaseName];
                    symbol.Path = symbol.Hash;
                    symbol.Color = 0; // no Klei files use color other than 0 so fair assumption is it can be 0
                    // only check in decompile for flag checks flag = 8 for a layered anim (which we won't do)
                    // so should be safe to leave flags = 0
                    // have seen some Klei files in which flags = 1 for some symbols but can't determine what that does
                    symbol.Flags = 0;
                    symbol.FrameCount = histogram[sprite.BaseName];
                    symbol.Frames = new List<Frame>();
                    BuildData.Symbols.Add(symbol);

                    symbolIndex++;
                    lastName = sprite.BaseName;
                }

                var frame = new Frame();
                frame.SourceFrameNum = sprite.SpriteName.Index;
                // duration is always 1 because the frames for a symbol always are numbered incrementing by 1
                // (or at least that's why I think it's always 1 in the examples I looked at)
                frame.Duration = 1;
                // this value as read from the file is unused by Klei code and all example files have it set to 0 for all symbols
                frame.BuildImageIndex = 0;

                frame.X1 = (float) sprite.X / texture.SpriteSheet.Width;
                frame.X2 = (float) (sprite.X + sprite.Width) / texture.SpriteSheet.Width;
                frame.Y1 = (float) sprite.Y / texture.SpriteSheet.Height;
                frame.Y2 = (float) (sprite.Y + sprite.Height) / texture.SpriteSheet.Height;

                // do not set frame.time since it was a calculated property and not actually used in kbild
                frame.PivotWidth = sprite.Width * 2;
                frame.PivotHeight = sprite.Height * 2;

                // Find the appropriate pivot from the scml
                var key = $"{sprite.BaseName}_{frame.SourceFrameNum}";
                if (!projectSprites.ContainsKey(sprite.SpriteName))
                {
                    continue;
                }
                var scmlnode = projectSprites[sprite.SpriteName];
                frame.PivotX = -(float.Parse(scmlnode.Attributes["pivot_x"].Value) - 0.5f) * frame.PivotWidth;
                frame.PivotY = (float.Parse(scmlnode.Attributes["pivot_y"].Value) - 0.5f) * frame.PivotHeight;
                BuildData.Symbols[symbolIndex].Frames.Add(frame);
            }

            // Finally, flip the key/values to get the build hash table
            BuildHashes = new Dictionary<int, SpriteBaseName>();
            foreach (var entry in hashTable) BuildHashes[entry.Value] = entry.Key;

            BuildBuildTable(texture.SpriteSheet.Width, texture.SpriteSheet.Height);
        }

        private XmlElement GetMainline(XmlNodeList nodes)
        {
            foreach (var element in nodes.GetElements())
                if (element.Name == "mainline")
                    return element;

            throw new ProjectParseException(
                "SCML format exception: Can't find <mainline> child of <animation>!");
        }

        // Maps ids to timeline elements
        private Dictionary<int, XmlElement> GetTimelineMap(XmlNodeList nodes)
        {
            var map = new Dictionary<int, XmlElement>();
            foreach (var element in nodes.GetElements())
                if (element.Name == "timeline")
                    map[int.Parse(element.Attributes["id"].Value)] = element;

            return map;
        }

        private void PopulateHashTableWithAnimations()
        {
            var entity = scml.GetElementsByTagName("entity")[0];
            foreach (var animation in entity.ChildNodes.GetElements())
            {
                if (animation.Name == "character_map")
                {
                    Logger.Debug("Skipping <character_map> child of <entity>");
                    continue;
                }
                if (animation.Name != "animation")
                    throw new ProjectParseException(
                        $"SCML format exception: all children of <entity> should be <animation>, but was <{animation.Name}> instead.");

                var animName = animation.Attributes["name"].Value;
                AnimHashes[Utilities.KleiHash(animName)] = animName;
            }
        }

        private void PackAnim()
        {
            AnimData = new Anim();
            AnimData.Version = 5; // everyone loves magic numbers
            AnimData.Anims = new List<AnimBank>();
            AnimHashes = new Dictionary<int, string>();

            // reading the scml to get the data you get by counting everything
            SetAggregateData();

            // anim names need to go into animhash as well
            PopulateHashTableWithAnimations();

            var reverseHash = new Dictionary<string, int>();
            foreach (var entry in AnimHashes) reverseHash[entry.Value] = entry.Key;

            var entity = scml.GetElementsByTagName("entity")[0];
            var animations = entity.ChildNodes.GetElements();
            var animCount = 0;

            /* error checking if user has intervals that aren't consistent because ONI only processes animations
             * as if each frame is the same interval (in ms) from the last */
            var hasInconsistentIntervals = false;
            var inconsistentAnims = new HashSet<string>();

            /* error checking if user has accidentally used pivots in timeline rather than setting pivot on original sprite */
            var hasPivotsSpecifiedInTimeline = false;
            var pivotAnims = new HashSet<string>();

            foreach (var anim in animations)
            {
                if (anim.Name == "character_map")
                {
                    Logger.Debug("Skipping <character_map> child of <entity>");
                    continue;
                }
                animCount++;
                if (anim.Name != "animation")
                    throw new ProjectParseException(
                        $"SCML format exception: all children of <entity> must be <animation>, was <{anim.Name}> instead.");

                var bank = new AnimBank();
                bank.Name = anim.Attributes["name"].Value;
                bank.Hash = reverseHash[bank.Name];
                Logger.Debug($"bank.name={bank.Name}\nhashTable={bank.Hash}");
                bank.Frames = new List<KAnim.Frame>();

                var interval = -1;

                var timelines = anim.ChildNodes;
                var mainline = GetMainline(timelines);
                // Build a temporary index of object id to list of frame times.


                var timelineMap = GetTimelineMap(timelines);
                var mainlineKeys = mainline.ChildNodes.GetElements();
                var frameCount = 0;
                var lastFrameTime = -1;
                foreach (var mainline_key in mainlineKeys)
                {
                    frameCount++;

                    // we are attempting to calculate the interval. The first valid interval will be the "gold standard"
                    // by which all other intervals will be judged by. Note that different anims can have different 
                    // intervals.
                    if (lastFrameTime != -1)
                    {
                        var this_interval = int.Parse(mainline_key.Attributes["time"].Value) - lastFrameTime;
                        if (interval == -1)
                        {
                            // it's not initialized, so init
                            interval = this_interval;
                        }
                        else
                        {
                            // otherwise, verify that the interval is correct
                            if (interval != this_interval)
                            {
                                Logger.Warn(
                                    $"While parsing animation \"{bank.Name}\", found inconsistent interval at keyframe {frameCount}: it is {this_interval} ms from the last frame, when {interval} ms was expected.");
                                hasInconsistentIntervals = true;
                                inconsistentAnims.Add(bank.Name);
                            }
                        }
                    }

                    if (mainline_key.Attributes["time"] == null)
                        lastFrameTime = 0; // if no time is specified, implied to be 0
                    else
                        lastFrameTime = int.Parse(mainline_key.Attributes["time"].Value);

                    // that will be sent to klei kanim format so we have to match the timeline data to key frames
                    // - this matching will be the part for
                    if (mainline_key.Name != "key")
                        throw new ProjectParseException(
                            $"SCML format exception: all children of <animation> must be <key>, was <{anim.Name}> instead.");

                    var frame = new KAnim.Frame();
                    frame.Elements = new List<Element>();
                    // the elements for this frame will be all the elements
                    // referenced in the object_ref(s) -> their data will be found
                    // in their timeline
                    // note that we need to calculate the animation's overall bounding
                    // box for this frame which will be done by computing locations
                    // of 4 rectangular bounds of each element under transformation
                    // and tracking the max and min of x and y
                    var minX = float.MaxValue;
                    var minY = float.MaxValue;
                    var maxX = float.MinValue;
                    var maxY = float.MinValue;

                    // look through object refs - will need to maintain list of object refs
                    // because in the end it must be sorted in accordance with the z-index
                    // before appended in correct order to elementsList
                    var object_refs = mainline_key.ChildNodes.GetElements().ToList();
                    var elementCount = 0;
                    for (var i = 0; i < object_refs.Count; i++)
                    {
                        var object_ref = object_refs[i];

                        if (object_ref.Name != "object_ref")
                            throw new ProjectParseException(
                                $"SCML format exception: all children of <key> must be <object_ref>, was <{object_ref.Name}> instead.");

                        var element = new Element();
                        element.Flags = 0;
                        // Spriter does support setting alpha per keyframe, so we grab that value for the alpha channel of the kanim element
                        if (object_ref.HasAttribute("a"))
                            element.A = float.Parse(object_ref.Attributes["a"].Value);
                        else
                            element.A = 1.0f;
                        // spriter does not support changing colors of components
                        // through animation so this can be safely set to 0
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
                        // unwrap the <timeline><key> to get the <object> tags.
                        var timeline_keys = timeline_node
                            .ChildNodes
                            .GetElements()
                            .ToArray();

                        var frameId = int.Parse(object_ref.Attributes["key"].Value);
                        XmlElement frame_node;
                        try
                        {
                            frame_node = getFrameFromTimeline(timeline_node, frameId);
                        }
                        catch (ProjectParseException)
                        {
                            Logger.Warn(
                                $"Could not find frame {frameId} in timeline {timeline_id} of anim \"{bank.Name}\"!");
                            continue; // skip this element.
                        }

                        var frame_object_node = frame_node.GetElementsByTagName("object")[0];

                        // Figure out the file id from the timeline's keyframe
                        string imageName;
                        float pivotX, pivotY;
                        int width, height;
                        XmlElement image_node;
                        try
                        {
                            image_node = projectFileIdMap[frame_object_node.Attributes["file"].Value];
                            imageName = image_node.Attributes["name"].Value;
                            pivotX = float.Parse(image_node.Attributes["pivot_x"].Value);
                            pivotY = float.Parse(image_node.Attributes["pivot_y"].Value);
                            width = int.Parse(image_node.Attributes["width"].Value);
                            height = int.Parse(image_node.Attributes["height"].Value);
                        } catch (NullReferenceException)
                        {
                            var animname = timeline_node.Attributes["name"].Value;
                            if (AllowMissingSprites)
                            {
                                // if sprite is missing, we can still reference it in the anim, it just wont exist.
                                imageName = timeline_node.Attributes["name"].Value;
                                pivotX = 0;
                                pivotY = 0;
                                width = 1;
                                height = 1;
                                Logger.Warn(
                                    $"Anim \"{animname}\" in \"{bank.Name}\" does not reference any valid sprite.\n" + 
                                    "If this was not intended behaviour, use the -S/--strict flag to enforce checking this error.");
                            }
                            else
                            {
                                throw new ProjectParseException(
                                    $"Frame element \"{animname}\" in \"{bank.Name}\" does not reference any valid sprite.\n");
                            }
                        }

                        element.ImageHash = Utilities.KleiHash(Utilities.GetSpriteBaseName(imageName));
                        element.Index = Utilities.GetFrameCount(imageName);
                        // layer doesn't seem to actually be used for anything after it is parsed as a "folder"
                        // but it does need to have an associated string in the hash table so we will just
                        // write layer as the same as the image being used
                        element.Layer = element.ImageHash;
                        // Add this info to the AnimHashes dict
                        AnimHashes[element.ImageHash] = Utilities.GetSpriteBaseName(imageName);
                        AnimHashes[element.Layer] = AnimHashes[element.ImageHash];

                        // find the interpolated value if required
                        // First, check if the timeline frame's timestamp is different to the key's.
                        // If yes, that means we need to interpolate the entire value depending on previous or
                        // following nodes
                        var frameTime = frame_node.GetDefault("time", 0);
                        var mainlineTime = mainline_key.GetDefault("time", 0);

                        float Interpolate(string attrName, float defaultValue)
                        {
                            // If our timeline key node actually has the attr value and is ours',
                            // we can simply get the value.
                            if (frameTime == mainlineTime && frame_object_node.Attributes[attrName] != null)
                                return float.Parse(frame_object_node.Attributes[attrName].Value);

                            // otherwise: we need to find the last timeline key node that has our value.
                            // also find the next timeline key node that has our value, then interpolate.
                            XmlNode prev = null, next = null;
                            if (frameId >= 0) prev = timeline_keys[frameId];

                            if (frameId < timeline_keys.Length - 1) next = timeline_keys[frameId + 1];

                            // if we haven't found a prev node, that means our value is simply default.
                            if (prev == null)
                                return defaultValue;
                            // No next node means use the previous value
                            if (next == null)
                                return prev.FirstElementChild().GetDefault(attrName, defaultValue);

                            // otherwise, gotta lerp
                            return prev.Interpolate(next, mainlineTime, attrName, defaultValue);
                        }

                        var scaleX = Interpolate("scale_x", 1f);
                        var scaleY = Interpolate("scale_y", 1f);
                        var angle = Interpolate("angle", 0f);
                        var xOffset = Interpolate("x", 0f);
                        var yOffset = Interpolate("y", 0f);

                        if (frame_object_node.Attributes["pivot_x"] != null || frame_object_node.Attributes["pivot_y"] != null)
                        {
                            hasPivotsSpecifiedInTimeline = true;
                            pivotAnims.Add(bank.Name);
                        }

                        var animdata = new AnimationData
                        {
                            ScaleX = scaleX,
                            ScaleY = scaleY,
                            Angle = angle,
                            X = xOffset,
                            Y = yOffset
                        };
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
                        var points = new[] {p1, p2, p3, p4};
                        
                        var pivotMatrix = new Matrix();
                        pivotMatrix.RotateAt(angle, new PointF(centerX, centerY));
                        pivotMatrix.Scale(scaleX, scaleY, MatrixOrder.Append);
                        pivotMatrix.TransformPoints(points);
                        
                        minX = Utilities.Min(minX, p1.X, p2.X, p3.X, p4.X);
                        minY = Utilities.Min(minY, p1.Y, p2.Y, p3.Y, p4.Y);
                        maxX = Utilities.Max(maxX, p1.X, p2.X, p3.X, p4.X);
                        maxY = Utilities.Max(maxY, p1.Y, p2.Y, p3.Y, p4.Y);

                        frame.Elements.Add(element);
                        elementCount++;
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
                /* if interval is -1 we have encountered an animation with only one keyframe
                 * because -1 means we weren't able to calculate an interval
                 * 
                 * we shouldn't return a rate of -1000 = 1000 / -1 in this case
                 * instead a logical rate would be to consider the interval
                 * as just the length of the animation */
                if (interval == -1)
                {
                    Logger.Debug("Encountered an animation with only one keyframe. Interpreting as having an interval between frames equal to the entire duration of the animation.");
                    interval = int.Parse(anim.GetAttribute("length"));
                }
                bank.Rate = (float) Utilities.MS_PER_S / interval;
                AnimData.Anims.Add(bank);
            }

            if (hasInconsistentIntervals)
            {
                var anims = inconsistentAnims.ToList().Join();
                string error = $"SCML format exception: The intervals in the anims {anims} were inconsistent. Aborting read.";
                if (!InterpolateMissingFrames)
                {
                    error += " Try enabling keyframe interpolation with the \"-i\" flag and try again.";
                }
                throw new ProjectParseException(error);
            }

            if (hasPivotsSpecifiedInTimeline)
            {
                var anims = pivotAnims.ToList().Join();
                if (AllowInFramePivots)
                {
                    Logger.Warn($"Encountered pivot points specified in timelines in anims {anims}. These pivot point changes will not be respected. Strict-mode is off. Converting anyway.");
                }
                else
                {
                    throw new ProjectParseException($"SCML format exception: There were pivot points specified in timelines rather than only on the sprites in anims {anims}. Aborting read.");
                }

            }

            AnimData.AnimCount = animCount;
        }

        private XmlElement getFrameFromTimeline(XmlElement timeline, int frameId)
        {
            foreach (var key in timeline.ChildNodes.GetElements())
            {
                if (key.Name != "key")
                    throw new ProjectParseException(
                        $"SCML format exception: all children of <timeline> must be <key>, was <{key.Name}> instead.");

                if (int.Parse(key.Attributes["id"].Value) == frameId) return key;
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
                if (anim.Name == "character_map")
                {
                    Logger.Debug("Skipping <character_map> child of <entity>");
                    continue;
                }
                if (anim.Name != "animation")
                    throw new ProjectParseException(
                        $"SCML format exception: all children of <entity> must be <animation>, was <{anim.Name}> instead.");

                var mainline = anim.GetElementsByTagName("mainline")[0];
                var keyframes = mainline.ChildNodes;

                for (var frameIndex = 0; frameIndex < keyframes.Count; frameIndex++)
                {
                    if (!(keyframes[frameIndex] is XmlElement))
                    {
                        Logger.Debug("Skipping non-element child of <mainline>");
                        continue;
                    }

                    var keyframe = (XmlElement) keyframes[frameIndex];
                    if (keyframe.Name != "key")
                        throw new ProjectParseException(
                            $"SCML format exception: all children of <animation> should be <key>, was <{keyframe.Name}> instead.");

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
                            throw new ProjectParseException(
                                $"SCML format exception: all children of <key> should be <object_ref>, was <{element.Name}> instead.");

                        if (objects.Count > maxVisibleSymbolFrames) maxVisibleSymbolFrames = objects.Count;
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

                var frameCount = sprite.SpriteName.Index;
                if (frameCount == 0) BuildData.SymbolCount++;
            }
        }
    }
}
