
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Text;
using System.Xml;
using NLog;

namespace kanimal
{
    public class ScmlWriter: Writer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        protected XmlDocument Scml;
        protected XmlElement SpriterRoot;
        protected XmlElement Entity;
        
        public override void Init(KBuild.Build buildData, List<KBuild.Row> buildTable, KAnim.Anim animData, Dictionary<int, string> animHashes)
        {
            BuildData = buildData;
            BuildTable = buildTable;
            AnimData = animData;
            AnimHashes = animHashes;
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
            SpriterRoot.SetAttribute("generator", "BrashMonkey Spriter");
            SpriterRoot.SetAttribute("generator_version", "r11");
        }

        protected virtual void AddFolderInfo()
        {
            // idk why there's only one folder ever
            int folders = 1;
            for (int i = 0; i < folders; i++)
            {
                var foldernode = Scml.CreateElement("folder");
                foldernode.SetAttribute("id", i.ToString());
                SpriterRoot.AppendChild(foldernode);
                
                FilenameIndex = new Dictionary<string, string>();
                for (int fileIndex = 0; fileIndex < BuildData.FrameCount; fileIndex++)
                {
                    var row = BuildTable[fileIndex];
                    var key = $"{row.Name}_{row.Index}";
                    if (FilenameIndex.ContainsKey(key))
                    {
                        key += "_" + fileIndex;
                    }

                    FilenameIndex[key] = fileIndex.ToString();

                    var x = row.PivotX - row.PivotWidth / 2f;
                    var y = row.PivotY - row.PivotHeight / 2f;
                    // this computation changes pivot from being in whatever
                    // coordinate system it was originally being specified in to being specified
                    // as just a scalar multiple of the width/height (starting at the midpoint of 0.5)
                    var pivot_x = 0 - x / row.PivotWidth;
                    var pivot_y = 1 + y / row.PivotHeight;

                    var filenode = Scml.CreateElement("file");
                    filenode.SetAttribute("id", fileIndex.ToString());
                    filenode.SetAttribute("name", $"{row.Name}_{row.Index}");
                    filenode.SetAttribute("width", ((int) row.Width).ToString());
                    filenode.SetAttribute("height", ((int) row.Height).ToString());
                    filenode.SetAttribute("pivot_x", pivot_x.ToString("G9"));
                    filenode.SetAttribute("pivot_y", pivot_y.ToString("G9"));

                    foldernode.AppendChild(filenode);
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
            for (int animIndex = 0; animIndex < AnimData.AnimCount; animIndex++)
            {
                var bank = AnimData.Anims[animIndex];
                var rate = (int) (Utilities.MS_PER_S / bank.Rate);

                var animnode = Scml.CreateElement("animation");
                animnode.SetAttribute("id", animIndex.ToString());
                animnode.SetAttribute("name", bank.Name);
                animnode.SetAttribute("length", (rate * bank.FrameCount).ToString());
                animnode.SetAttribute("interval", rate.ToString());

                Entity.AppendChild(animnode);
                
                AddMainlineInfo(animnode, animIndex);
                AddTimelineInfo(animnode, animIndex);
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

            for (int frameIndex = 0; frameIndex < bank.FrameCount; frameIndex++)
            {
                var occuranceMap = new OccurenceMap();
                var keyframe = AddKeyframe(frameIndex, rate);
                var frame = bank.Frames[frameIndex];
                for (int elementIndex = 0; elementIndex < frame.ElementCount; elementIndex++)
                {
                    var object_ref = Scml.CreateElement("object_ref");
                    var element = frame.Elements[elementIndex];
                    occuranceMap.Update(element, AnimHashes);

                    var occName = occuranceMap.FindOccurenceName(element, AnimHashes);
                    Logger.Debug(occName);

                    object_ref.SetAttribute("id", idMap[occName].ToString());
                    object_ref.SetAttribute("timeline", idMap[occName].ToString());
                    // b/c ONI has animation properties for each element specified at every frame the timeline key frame that
                    // matches a mainline key frame is always the same
                    object_ref.SetAttribute("key", frameIndex.ToString());
                    object_ref.SetAttribute("z_index", (frame.ElementCount - elementIndex).ToString());

                    keyframe.AppendChild(object_ref);
                }

                mainline.AppendChild(keyframe);
            }
        }

        protected virtual void AddTimelineInfo(XmlElement parent, int animIndex)
        {
            var bank = AnimData.Anims[animIndex];
            int rate = (int) (Utilities.MS_PER_S / bank.Rate);
            var timelineMap = new Dictionary<int, XmlElement>();

            var idMap = bank.BuildIdMap(AnimHashes);
            foreach (var entry in idMap)
            {
                var timeline = Scml.CreateElement("timeline");
                timeline.SetAttribute("id", entry.Value.ToString());
                timeline.SetAttribute("name", entry.Key);
                timelineMap[entry.Value] = timeline;
            }
            
            for (int frameIndex = 0; frameIndex < bank.FrameCount; frameIndex++)
            {
                var frame = bank.Frames[frameIndex];
                var occMap = new OccurenceMap();
                for (int elementIndex = 0; elementIndex < frame.ElementCount; elementIndex++)
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

            foreach (var timeline in timelineMap.Values)
            {
                parent.AppendChild(timeline);
            }
        }
    }
}