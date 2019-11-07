using System.Collections.Generic;
using System.Text;
using System.Xml;
using NLog;

namespace kanimal
{
    public class ScmlWriter : Writer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        protected XmlDocument Scml;
        protected XmlElement SpriterRoot;
        protected XmlElement Entity;

        public ScmlWriter(Reader reader)
        {
            BuildData = reader.BuildData;
            BuildTable = reader.BuildTable;

            AnimData = reader.AnimData;
            AnimHashes = reader.AnimHashes;
            Sprites = reader.Sprites;
        }

        public override void Save(string path)
        {
            // Do all the calculation required to write to an scml, including building
            // an in-memory XML tree
            PrepareFile();
            AddFolderInfo();
            AddEntityInfo();
            AddAnimInfo();

            // Finally, output to a file
            var writer = new XmlTextWriter(path, Encoding.Unicode);
            Scml.WriteTo(writer);
            writer.Flush();
        }

        protected virtual void PrepareFile()
        {
            Scml = new XmlDocument();
            SpriterRoot = Scml.CreateElement("spriter_data");
            Scml.AppendChild(SpriterRoot);
            SpriterRoot.SetAttribute("scml_version", "1.0");
            // look into setting this to kanimAL instead; it might work?
            SpriterRoot.SetAttribute("generator", "kanimAL");
            SpriterRoot.SetAttribute("generator_version", "v1");
        }

        protected virtual void AddFolderInfo()
        {
            // idk why there's only one folder ever
            var folders = 1;
            for (var i = 0; i < folders; i++)
            {
                var folderNode = Scml.CreateElement("folder");
                folderNode.SetAttribute("id", i.ToString());
                SpriterRoot.AppendChild(folderNode);

                FilenameIndex = new Dictionary<string, string>();
                for (var fileIndex = 0; fileIndex < BuildData.FrameCount; fileIndex++)
                {
                    var row = BuildTable[fileIndex];
                    var key = $"{row.Name}_{row.Index}";
                    if (FilenameIndex.ContainsKey(key)) key += "_" + fileIndex;

                    FilenameIndex[key] = fileIndex.ToString();

                    var x = row.PivotX - row.PivotWidth / 2f;
                    var y = row.PivotY - row.PivotHeight / 2f;
                    // this computation changes pivot from being in whatever
                    // coordinate system it was originally being specified in to being specified
                    // as just a scalar multiple of the width/height (starting at the midpoint of 0.5)
                    var pivotX = 0 - x / row.PivotWidth;
                    var pivotY = 1 + y / row.PivotHeight;

                    var fileNode = Scml.CreateElement("file");
                    fileNode.SetAttribute("id", fileIndex.ToString());
                    fileNode.SetAttribute("name", $"{row.Name}_{row.Index}");
                    fileNode.SetAttribute("width", ((int) row.Width).ToString());
                    fileNode.SetAttribute("height", ((int) row.Height).ToString());
                    fileNode.SetAttribute("pivot_x", pivotX.ToString("G9"));
                    fileNode.SetAttribute("pivot_y", pivotY.ToString("G9"));

                    folderNode.AppendChild(fileNode);
                }
            }
        }

        protected virtual void AddEntityInfo()
        {
            Entity = Scml.CreateElement("entity");
            Entity.SetAttribute("id", "0");
            Entity.SetAttribute("name", BuildTable[0].Build.Name);
            SpriterRoot.AppendChild(Entity);
        }

        protected virtual void AddAnimInfo()
        {
            for (var animIndex = 0; animIndex < AnimData.AnimCount; animIndex++)
            {
                var bank = AnimData.Anims[animIndex];
                var rate = (int) (Utilities.MS_PER_S / bank.Rate);

                var animNode = Scml.CreateElement("animation");
                animNode.SetAttribute("id", animIndex.ToString());
                animNode.SetAttribute("name", bank.Name);
                animNode.SetAttribute("length", (rate * bank.FrameCount).ToString());
                animNode.SetAttribute("interval", rate.ToString());

                Entity.AppendChild(animNode);

                AddMainlineInfo(animNode, animIndex);
                AddTimelineInfo(animNode, animIndex);
            }
        }

        private XmlElement AddKeyframe(int frameIndex, int rate)
        {
            var key = Scml.CreateElement("key");
            key.SetAttribute("id", frameIndex.ToString());
            key.SetAttribute("time", (frameIndex * rate).ToString());
            return key;
        }

        protected virtual void AddMainlineInfo(XmlElement parent, int animIndex)
        {
            var mainline = Scml.CreateElement("mainline");
            parent.AppendChild(mainline);

            var bank = AnimData.Anims[animIndex];
            var rate = (int) (Utilities.MS_PER_S / bank.Rate);

            var idMap = bank.BuildIdMap(AnimHashes);

            for (var frameIndex = 0; frameIndex < bank.FrameCount; frameIndex++)
            {
                var occurenceMap = new OccurenceMap();
                var keyframe = AddKeyframe(frameIndex, rate);
                var frame = bank.Frames[frameIndex];
                for (var elementIndex = 0; elementIndex < frame.ElementCount; elementIndex++)
                {
                    var objectRef = Scml.CreateElement("object_ref");
                    var element = frame.Elements[elementIndex];
                    occurenceMap.Update(element, AnimHashes);

                    var occName = occurenceMap.FindOccurenceName(element, AnimHashes);
                    Logger.Debug(occName);

                    objectRef.SetAttribute("id", idMap[occName].ToString());
                    objectRef.SetAttribute("timeline", idMap[occName].ToString());
                    // b/c ONI has animation properties for each element specified at every frame the timeline key frame that
                    // matches a mainline key frame is always the same
                    objectRef.SetAttribute("key", frameIndex.ToString());
                    objectRef.SetAttribute("z_index", (frame.ElementCount - elementIndex).ToString());

                    keyframe.AppendChild(objectRef);
                }

                mainline.AppendChild(keyframe);
            }
        }

        protected virtual void AddTimelineInfo(XmlElement parent, int animIndex)
        {
            var bank = AnimData.Anims[animIndex];
            var rate = (int) (Utilities.MS_PER_S / bank.Rate);
            var timelineMap = new Dictionary<int, XmlElement>();

            var idMap = bank.BuildIdMap(AnimHashes);
            foreach (var entry in idMap)
            {
                var timeline = Scml.CreateElement("timeline");
                timeline.SetAttribute("id", entry.Value.ToString());
                timeline.SetAttribute("name", entry.Key);
                timelineMap[entry.Value] = timeline;
            }

            for (var frameIndex = 0; frameIndex < bank.FrameCount; frameIndex++)
            {
                var frame = bank.Frames[frameIndex];
                var occMap = new OccurenceMap();
                for (var elementIndex = 0; elementIndex < frame.ElementCount; elementIndex++)
                {
                    var keyframe = AddKeyframe(frameIndex, rate);
                    var element = frame.Elements[elementIndex];
                    occMap.Update(element, AnimHashes);
                    var name = occMap.FindOccurenceName(element, AnimHashes);

                    var trans = element.Decompose();
                    var object_def = Scml.CreateElement("object");
                    object_def.SetAttribute("folder", "0");
                    var filename = element.FindFilename(AnimHashes);

                    object_def.SetAttribute("file", FilenameIndex[filename]);
                    object_def.SetAttribute("x", (trans.X * 0.5f).ToString("G5"));
                    object_def.SetAttribute("y", (-trans.Y * 0.5f).ToString("G5"));
                    object_def.SetAttribute("angle", trans.Angle.ToString("G5"));
                    object_def.SetAttribute("scale_x", trans.ScaleX.ToString("G5"));
                    object_def.SetAttribute("scale_y", trans.ScaleY.ToString("G5"));

                    keyframe.AppendChild(object_def);
                    timelineMap[idMap[name]].AppendChild(keyframe);
                }
            }

            foreach (var timeline in timelineMap.Values) parent.AppendChild(timeline);
        }
    }
}