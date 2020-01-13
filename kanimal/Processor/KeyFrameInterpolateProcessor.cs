using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Xml;
using NLog;

namespace kanimal
{
    /* this class is a single frame for a single timeline */
    class ProcessingFrame
    {
        /* parent id is the id of the parent of this object 
         * all ids are either zero or a positive integer so -1
         * represents an object without a parent */
        public int ParentId { get; set; }

        /* just z-index no need to change - just note that
         * z-index only changes on the frame that it is changed
         * when Spriter interpolates so when interpolating in-between
         * frames the z-index of the prior frame should be used instead
         * 
         * also z-index is ignored when Type is ChildType.Bone but I
         * didn't think of a more elegant way of making z-index optional*/
        public int ZIndex { get; set; }

        /* the folder and file are ignored when Type is ChildType.Bone
         * same as above with z-index */
        public int Folder { get; set; }
        public int File { get; set; }

        public float X { get; set; }
        public float Y { get; set; }
        public float Angle { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }

        private bool populated = false;

        public ProcessingFrame(int parentId, int zIndex)
        {
            ParentId = parentId;
            ZIndex = zIndex;
        }

        public bool IsPopulated()
        {
            return populated;
        }

        public void Populate(int folder, int file, float x, float y, float angle, float scaleX, float scaleY)
        {
            Folder = folder;
            File = file;
            X = x;
            Y = y;
            Angle = angle;
            ScaleX = scaleX;
            ScaleY = scaleY;
            populated = true;
        }
    }

    /* this class is in-memory representation of the scml file
     * designed to be loaded, modified, and then written 
     * in order to faciliate programmatically modifying scml */
    class ProcessingAnimation
    {
        /* the name of the animation */
        public string Name { get; set; }

        /* interval between frames in ms - only integer amounts */
        public int Step { get; set; }

        /* the length of the animation in milliseconds - integer */
        public int Length { get; set; }

        /* 2d array of the frames, 1st axis is the ids of the timelines for all
         * of the sprites and bones in the animation, 2nd axis is the timesteps */
        public List<List<ProcessingFrame>> FrameArray { get; set; }

        public TimelineInfoMap InfoProvider { get; set; }

        public ProcessingAnimation(string name, int step, int length, List<List<ProcessingFrame>> frameArray, TimelineInfoMap infoProvider)
        {
            Name = name;
            Step = step;
            Length = length;
            FrameArray = frameArray;
            InfoProvider = infoProvider;
        }
    }

    /* interpolates all missing frames where the expected frames are all the frames that
     * are a multiple of the snapping interval
     * 
     * ex is that if we have an animation of length 330 with snapping interval of 33
     * then we expect frames to be at 0, 33, 66, ..., 297, 330 so the interpolation
     * processing will make the frames for each of time steps for every sprite when it is
     * supposed to be present at that time step */
    public class KeyFrameInterpolateProcessor : Processor
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public override XmlDocument Process(XmlDocument original)
        {
            Logger.Info("Interpolating key frames.");
            /* clone to avoid modifying original data */
            XmlDocument processedScml = (XmlDocument)original.Clone();

            /* convert the scml document into an in memory representation
             * in memory representation is basically 2d array of timelines
             * and frames which is nice data structure for filling in missing
             * frames with interpolation */
            XmlElement spriterData = (XmlElement)processedScml.GetElementsByTagName("spriter_data")[0];
            XmlElement entity = GetFirstChildByName(spriterData, "entity");
            List<ProcessingAnimation> animations = new List<ProcessingAnimation>();
            foreach (XmlNode node in entity.ChildNodes)
            {
                if (node is XmlElement && node.Name.Equals("animation"))
                {
                    animations.Add(ParseAnimation(processedScml, (XmlElement)node));
                }
            }

            /* now take the in memory representation and convert it back to the scml document 
             * everything but the animation nodes should stay in-tact so it simplfy removes
             * all animation nodes and rewrites them from the in-memory animation information */
            List<XmlNode> nodes = new List<XmlNode>();
            foreach (XmlNode node in entity.ChildNodes)
            {
                nodes.Add(node);
            }
            foreach (XmlNode node in nodes)
            {
                if (node is XmlElement && node.Name.Equals("animation"))
                {
                    entity.RemoveChild(node);
                }
            }
            for (int i = 0; i < animations.Count; i++)
            {
                entity.AppendChild(MakeAnimationNode(processedScml, i, animations[i]));
            }

            return processedScml;
        }

        private ProcessingAnimation ParseAnimation(XmlDocument scml, XmlElement animation)
        {
            string name = animation.GetAttribute("name");
            int length = int.Parse(animation.GetAttribute("length"));
            int interval = int.Parse(animation.GetAttribute("interval"));
            TimelineInfoMap infoProvider = new TimelineInfoMap(scml, name);

            /* rename the ids to match timelines in "id" and "parent" */
            IdRename(animation, infoProvider);

            /* build the frame array - description is in Alternate.Animation */
            List<List<ProcessingFrame>> frameArray = new List<List<ProcessingFrame>>();
            /* count of frames to know how large the frame array should be */
            int numberOfFrames = length / interval + 1;
            /* for each sprite and bone id a list of frames is created */
            for (int i = 0; i < infoProvider.Size(); i++)
            {
                List<ProcessingFrame> frames = new List<ProcessingFrame>();
                for (int j = 0; j < numberOfFrames; j++)
                {
                    /* start out with a completely empty frame array so it can later be populated
                     * with the existing data and then finally with the interpolated data */
                    frames.Add(null);
                }
                frameArray.Add(frames);
            }

            /* error checking to see if the animation doesn't have every frame snapped to an interval
             * because ONI expects every frame to have a consistent interval so if the frames aren't
             * snapped they will be placed wrong */
            var hasBrokenSnapping = false;
            var brokenAnims = new HashSet<string>();

            /* error checking if user has accidentally used pivots in timeline rather than setting pivot on original sprite */
            var hasPivotsSpecifiedInTimeline = false;
            var pivotAnims = new HashSet<string>();

            /* read all the data from mainline 
             * oddly it is not needed to read which timeline key frame is associated
             * with each mainline key frame since the timline key frames contain timing
             * information which can be used to exactly place them in the array since
             * we force snapping to intervals for ONI animations */
            XmlElement mainline = GetFirstChildByName(animation, "mainline");
            foreach (XmlNode keyNode in mainline.ChildNodes)
            {
                if (keyNode is XmlElement && keyNode.Name.Equals("key"))
                {
                    int time = 0;
                    XmlElement keyElement = (XmlElement)keyNode;
                    if (keyElement.HasAttribute("time"))
                    {
                        time = int.Parse(keyElement.GetAttribute("time"));
                    }
                    if (time % interval != 0)
                    {
                        Logger.Warn(
                            $"While parsing animation \"{name}\", found broken snapping in the mainline: it is time {time} ms that is not a multiple of snapping interval {interval} ms.");
                        hasBrokenSnapping = true;
                        brokenAnims.Add(name);
                    }
                    /* scale the time by the interval between frames to figure out which
                     * frame index in the array this goes to */
                    int frameIndex = time / interval;
                    foreach (XmlNode refNode in keyNode.ChildNodes)
                    {
                        if (refNode is XmlElement &&
                            (refNode.Name.Equals("object_ref") || refNode.Name.Equals("bone_ref")))
                        {
                            XmlElement refElement = (XmlElement)refNode;
                            /* the call to IdRename ensures that the ids on each ref now match the actual index in the array */
                            int id = int.Parse(refElement.GetAttribute("id"));
                            int zIndex = 0;
                            if (refElement.HasAttribute("z_index")) /* will always be true for sprites never true for bones */
                            {
                                zIndex = int.Parse(refElement.GetAttribute("z_index"));
                            }
                            int parent = -1;
                            if (refElement.HasAttribute("parent")) /* optional for both sprites and bones */
                            {
                                parent = int.Parse(refElement.GetAttribute("parent"));
                            }
                            /* set this frame to contain the data stored in the mainline */
                            frameArray[id][frameIndex] = new ProcessingFrame(parent, zIndex);
                        }
                    }
                }
            }

            /* read all the data from each timeline and use it to further populate the data of each frame */
            foreach (XmlNode timelineNode in animation.ChildNodes)
            {
                if (timelineNode is XmlElement && timelineNode.Name.Equals("timeline"))
                {
                    XmlElement timelineElement = (XmlElement)timelineNode;
                    int timeline = int.Parse(timelineElement.GetAttribute("id"));
                    int timelineIndex = infoProvider.GetId(timeline);
                    float x = 0;
                    float y = 0;
                    float angle = 0;
                    foreach (XmlNode keyNode in timelineNode.ChildNodes)
                    {
                        if (keyNode is XmlElement && keyNode.Name.Equals("key"))
                        {
                            XmlElement keyElement = (XmlElement)keyNode;
                            int time = 0;
                            if (keyElement.HasAttribute("time"))
                            {
                                time = int.Parse(keyElement.GetAttribute("time"));
                            }
                            if (time % interval != 0)
                            {
                                Logger.Warn(
                                    $"While parsing animation \"{name}\", found broken snapping at timeline {timeline}: it is time {time} ms that is not a multiple of snapping interval {interval} ms.");
                                hasBrokenSnapping = true;
                                brokenAnims.Add(name);
                            }
                            int frameIndex = time / interval;
                            XmlElement child = GetFirstChildByName(keyElement, "object");
                            if (child == null)
                            {
                                child = GetFirstChildByName(keyElement, "bone");
                            }
                            if (child == null)
                            {
                                throw new ArgumentException("Found timeline key without child object or bone");
                            }
                            int folder = -1;
                            if (child.HasAttribute("folder"))
                            {
                                folder = int.Parse(child.GetAttribute("folder"));
                            }
                            int file = -1;
                            if (child.HasAttribute("file"))
                            {
                                file = int.Parse(child.GetAttribute("file"));
                            }
                            if (child.HasAttribute("x"))
                            {
                                x = float.Parse(child.GetAttribute("x"));
                            }
                            if (child.HasAttribute("y"))
                            {
                                y = float.Parse(child.GetAttribute("y"));
                            }
                            if (child.HasAttribute("angle"))
                            {
                                angle = float.Parse(child.GetAttribute("angle"));
                            }
                            float scaleX = 1.0f;
                            if (child.HasAttribute("scale_x"))
                            {
                                scaleX = float.Parse(child.GetAttribute("scale_x"));
                            }
                            float scaleY = 1.0f;
                            if (child.HasAttribute("scale_y"))
                            {
                                scaleY = float.Parse(child.GetAttribute("scale_y"));
                            }
                            frameArray[timelineIndex][frameIndex].Populate(folder, file, x, y, angle, scaleX, scaleY);

                            if (child.HasAttribute("pivot_x") || child.HasAttribute("pivot_y"))
                            {
                                hasPivotsSpecifiedInTimeline = true;
                                pivotAnims.Add(name);
                            }
                        }
                    }
                }
            }

            if (hasBrokenSnapping)
            {
                var anims = brokenAnims.ToList().Join();
                throw new ProjectParseException(
                    $"SCML format exception: The timelines in anims {anims} had frames at times not snapped to the running interval {interval} ms. Aborting read.");
            }

            if (hasPivotsSpecifiedInTimeline)
            {
                var anims = pivotAnims.ToList().Join();
                throw new ProjectParseException(
                    $"SCML format exception: There were pivot points specified in timelines rather than only on the sprites in anims {anims}. Aborting read.");
            }

            /* determine which frames need to be interpolated by checking which frames are key frames in the mainline */
            List<bool> keyFrames = new List<bool>();
            for (int i = 0; i < numberOfFrames; i++)
            {
                keyFrames.Add(false);
            }
            foreach (XmlNode keyNode in mainline.ChildNodes)
            {
                if (keyNode is XmlElement && keyNode.Name.Equals("key"))
                {
                    int time = 0;
                    XmlElement keyElement = (XmlElement)keyNode;
                    if (keyElement.HasAttribute("time"))
                    {
                        time = int.Parse(keyElement.GetAttribute("time"));
                    }
                    /* scale the time by the interval between frames to figure out which
                     * frame index in the array this goes to */
                    int frameIndex = time / interval;
                    /* now we know this particular time step is a key frame in the mainline */
                    keyFrames[frameIndex] = true;
                }
            }

            /* create an additional array that indicates presence of each timeline on a per-frame basis */
            List<List<bool>> presenceArray = new List<List<bool>>();
            for (int i = 0; i < infoProvider.Size(); i++)
            {
                List<bool> presences = new List<bool>();
                for (int j = 0; j < numberOfFrames; j++)
                {
                    /* start out with a completely empty presence array so it can later be populated
                     * with the existing data and then finally with the interpolated data */
                    presences.Add(false);
                }
                presenceArray.Add(presences);
            }
            for (int i = 0; i < infoProvider.Size(); i++)
            {
                bool currentPresence = false;
                for (int j = 0; j < numberOfFrames; j++)
                {
                    /* if this frame is a key frame then update the current presence based on if there
                     * is a frame populated at this location */
                    if (keyFrames[j])
                    {
                        currentPresence = (frameArray[i][j] != null);
                    }
                    presenceArray[i][j] = currentPresence;
                }
                for (int j = 0; j < numberOfFrames; j++)
                {
                    /* if this frame is a key frame then update the current presence based on if there
                     * is a frame populated at this location */
                    if (keyFrames[j])
                    {
                        currentPresence = (frameArray[i][j] != null);
                    }
                    presenceArray[i][j] = currentPresence;
                }
                /* executing the loop twice is the most straightforward way to ensure that a keyframe at the end
                 * of the timeline wraps around to the front of the timeline
                 * this does mess with animations that aren't looped that don't have keyframes at time = 0 but that just doesn't make
                 * much sense (who wouldn't keyframe at time = 0 for a non-looping animation!)
                 * so I'll just document that and ignore that problem for now */
            }

            /* for every frame with presence in the array set to true that still has a null frame
             * interpolate the missing frame */
            for (int i = 0; i < infoProvider.Size(); i++)
            {
                ProcessingFrame beforeFrame = null;
                ProcessingFrame afterFrame = null;
                int beforeFrameIndex = -1;
                int afterFrameIndex = -1;
                for (int j = 0; j < numberOfFrames; j++)
                {
                    /* skip this frame if it isn't supposed to be present */
                    if (!presenceArray[i][j])
                    {
                        continue;
                    }
                    /* if this frame exists and is populated then it will be used
                     * as the before frame */
                    if (frameArray[i][j] != null && frameArray[i][j].IsPopulated())
                    {
                        beforeFrame = frameArray[i][j];
                        beforeFrameIndex = j;
                        /* probe forward to find the after array when a before array is found
                         * will use a endless loop because eventually at least we know we will
                         * terminate when it hits the exact same before array */
                        int jPrime = j + 1;
                        if (jPrime >= numberOfFrames)
                        {
                            jPrime = 0;
                        }
                        while (presenceArray[i][jPrime])
                        {
                            if (frameArray[i][jPrime] != null && frameArray[i][jPrime].IsPopulated())
                            {
                                afterFrame = frameArray[i][jPrime];
                                afterFrameIndex = jPrime;
                                break;
                            }
                            jPrime++;
                            if (jPrime >= numberOfFrames)
                            {
                                jPrime = 0;
                            }
                        }
                        /* if we found a before frame but couldn't find an after frame this means that there was a frame that is completely defined
                         * but there are more frames that need to be interpolated from this frame only
                         * since this is the only frame, spriter interprets this frame as being the frame used for all of the interpolated positions
                         * in which this sprite exists */
                        if (afterFrame == null)
                        {
                            Logger.Debug("Could not find after frame to interpolate between. Interpreting this to mean that this frame is expected to take the entire duration of the timeline.");
                            afterFrame = beforeFrame;
                            afterFrameIndex = beforeFrameIndex;
                        }
                    }
                    else if (beforeFrame != null && afterFrame != null)
                    {
                        float x = LinearInterpolate(beforeFrame.X, afterFrame.X, beforeFrameIndex,
                            afterFrameIndex + ((afterFrameIndex < beforeFrameIndex) ? numberOfFrames : 0), j);
                        float y = LinearInterpolate(beforeFrame.Y, afterFrame.Y, beforeFrameIndex,
                            afterFrameIndex + ((afterFrameIndex < beforeFrameIndex) ? numberOfFrames : 0), j);
                        float angle = LinearInterpolateAngle(beforeFrame.Angle, afterFrame.Angle, beforeFrameIndex,
                            afterFrameIndex + ((afterFrameIndex < beforeFrameIndex) ? numberOfFrames : 0), j);
                        float xScale = LinearInterpolate(beforeFrame.ScaleX, afterFrame.ScaleX, beforeFrameIndex,
                            afterFrameIndex + ((afterFrameIndex < beforeFrameIndex) ? numberOfFrames : 0), j);
                        float yScale = LinearInterpolate(beforeFrame.ScaleY, afterFrame.ScaleY, beforeFrameIndex,
                            afterFrameIndex + ((afterFrameIndex < beforeFrameIndex) ? numberOfFrames : 0), j);
                        if (frameArray[i][j] == null)
                        {
                            frameArray[i][j] = new ProcessingFrame(beforeFrame.ParentId, beforeFrame.ZIndex);
                        }
                        frameArray[i][j].Populate(beforeFrame.Folder, beforeFrame.File, x, y, angle, xScale, yScale);
                    }
                }
            }
            for (int i = 0; i < infoProvider.Size(); i++)
            {
                ProcessingFrame beforeFrame = null;
                ProcessingFrame afterFrame = null;
                int beforeFrameIndex = -1;
                int afterFrameIndex = -1;
                for (int j = 0; j < numberOfFrames; j++)
                {
                    /* skip this frame if it isn't supposed to be present */
                    if (!presenceArray[i][j])
                    {
                        continue;
                    }
                    /* if this frame exists and is populated then it will be used
                     * as the before frame */
                    if (frameArray[i][j] != null && frameArray[i][j].IsPopulated())
                    {
                        beforeFrame = frameArray[i][j];
                        beforeFrameIndex = j;
                        /* probe forward to find the after array when a before array is found
                         * will use a endless loop because eventually at least we know we will
                         * terminate when it hits the exact same before array */
                        int jPrime = j + 1;
                        if (jPrime >= numberOfFrames)
                        {
                            jPrime = 0;
                        }
                        while (presenceArray[i][jPrime])
                        {
                            if (frameArray[i][jPrime] != null && frameArray[i][jPrime].IsPopulated())
                            {
                                afterFrame = frameArray[i][jPrime];
                                afterFrameIndex = jPrime;
                                break;
                            }
                            jPrime++;
                            if (jPrime >= numberOfFrames)
                            {
                                jPrime = 0;
                            }
                        }
                        /* if we found a before frame but couldn't find an after frame this means that there was a frame that is completely defined
                         * but there are more frames that need to be interpolated from this frame only
                         * since this is the only frame, spriter interprets this frame as being the frame used for all of the interpolated positions
                         * in which this sprite exists */
                        if (afterFrame == null)
                        {
                            Logger.Debug("Could not find after frame to interpolate between. Interpreting this to mean that this frame is expected to take the entire duration of the timeline.");
                            afterFrame = beforeFrame;
                            afterFrameIndex = beforeFrameIndex;
                        }
                    }
                    else if (beforeFrame != null && afterFrame != null)
                    {
                        float x = LinearInterpolate(beforeFrame.X, afterFrame.X, beforeFrameIndex,
                            afterFrameIndex + ((afterFrameIndex < beforeFrameIndex) ? numberOfFrames : 0), j);
                        float y = LinearInterpolate(beforeFrame.Y, afterFrame.Y, beforeFrameIndex,
                            afterFrameIndex + ((afterFrameIndex < beforeFrameIndex) ? numberOfFrames : 0), j);
                        float angle = LinearInterpolateAngle(beforeFrame.Angle, afterFrame.Angle, beforeFrameIndex,
                            afterFrameIndex + ((afterFrameIndex < beforeFrameIndex) ? numberOfFrames : 0), j);
                        float xScale = LinearInterpolate(beforeFrame.ScaleX, afterFrame.ScaleX, beforeFrameIndex,
                            afterFrameIndex + ((afterFrameIndex < beforeFrameIndex) ? numberOfFrames : 0), j);
                        float yScale = LinearInterpolate(beforeFrame.ScaleY, afterFrame.ScaleY, beforeFrameIndex,
                            afterFrameIndex + ((afterFrameIndex < beforeFrameIndex) ? numberOfFrames : 0), j);
                        if (frameArray[i][j] == null)
                        {
                            frameArray[i][j] = new ProcessingFrame(beforeFrame.ParentId, beforeFrame.ZIndex);
                        }
                        frameArray[i][j].Populate(beforeFrame.Folder, beforeFrame.File, x, y, angle, xScale, yScale);
                    }
                }
            }
            /* interpolation is run twice to fix issue where time = 0 is not key frame */

            return new ProcessingAnimation(name, interval, length, frameArray, infoProvider);
        }

        private float LinearInterpolate(float x0, float x1, float t0, float t1, float t)
        {
            if (t0 == t1)
            {
                return x0;
            }
            float a = (x0 - x1) / (t0 - t1);
            float b = x0 - a * t0;
            return a * t + b;
        }

        /* requires that both input angles x0 x1 are within 0 to 360 and returns an interpolated
         * angle also between 0 and 360
         * interpolates the shortest route rather than explicitly clockwise or counterclockwise 
         *
         * this is important because spriter does shortest route interpolation so we need to
         * do the same lest our rotations come out wrong
         * reason for using degrees is that spriter saves to SCML in degrees not radians */
        private float LinearInterpolateAngle(float x0, float x1, float t0, float t1, float t)
        {
            if (t0 == t1)
            {
                return x0;
            }
            /* see https://stackoverflow.com/questions/2708476/rotation-interpolation for math
             * explanation of why this works */
            float delta = Math.Abs(x1 - x0);
            if (delta > 180)
            {
                if (x1 > x0)
                {
                    x0 += 360;
                }
                else
                {
                    x1 += 360;
                }
            }

            float x = LinearInterpolate(x0, x1, t0, t1, t);
            if (x >= 360)
            {
                x -= 360;
            }
            return x;
        }

        /* makes it such that each sprite and bones id in the scml matches those given by the id provider
         * while also ensuring that the parent ids are also kept accurate
         * this allows for consistent ids that are 0-indexed and without any missing integers */
        private void IdRename(XmlElement animation, TimelineInfoMap infoProvider)
        {
            /* when renaming parent, the id always refers to the id for the bone refs */
            XmlElement mainline = GetFirstChildByName(animation, "mainline");
            foreach (XmlNode keyNode in mainline.ChildNodes)
            {
                if (keyNode is XmlElement && keyNode.Name.Equals("key"))
                {
                    Dictionary<int, int> symbolIdToTimelineMap = new Dictionary<int, int>();
                    foreach (XmlNode refNode in keyNode.ChildNodes)
                    {
                        if (refNode is XmlElement && refNode.Name.Equals("bone_ref"))
                        {
                            int id = int.Parse(((XmlElement)refNode).GetAttribute("id"));
                            int timeline = int.Parse(((XmlElement)refNode).GetAttribute("timeline"));
                            symbolIdToTimelineMap.Add(id, timeline);
                        }
                    }
                    foreach (XmlNode refNode in keyNode.ChildNodes)
                    {
                        if (refNode is XmlElement)
                        {
                            XmlElement refElement = (XmlElement)refNode;
                            int timeline = int.Parse(refElement.GetAttribute("timeline"));
                            refElement.SetAttribute("id", infoProvider.GetId(timeline).ToString());
                            if (refElement.HasAttribute("parent"))
                            {
                                int parent = int.Parse(refElement.GetAttribute("parent"));
                                refElement.SetAttribute("parent", infoProvider.GetId(symbolIdToTimelineMap[parent]).ToString());
                            }
                        }
                    }
                }
            }
        }

        private XmlElement MakeAnimationNode(XmlDocument scml, int id, ProcessingAnimation animation)
        {
            XmlElement animationElement = scml.CreateElement("animation");
            animationElement.SetAttribute("id", id.ToString());
            animationElement.SetAttribute("name", animation.Name);
            animationElement.SetAttribute("length", animation.Length.ToString());
            animationElement.SetAttribute("interval", animation.Step.ToString());
            XmlElement mainlineElement = scml.CreateElement("mainline");
            for (int j = 0; j < animation.FrameArray[0].Count; j++)
            {
                XmlElement keyElement = scml.CreateElement("key");
                keyElement.SetAttribute("id", j.ToString());
                keyElement.SetAttribute("time", (j * animation.Step).ToString());
                for (int i = 0; i < animation.FrameArray.Count; i++)
                {
                    if (animation.FrameArray[i][j] != null)
                    {
                        ProcessingFrame frame = animation.FrameArray[i][j];
                        ChildType type = animation.InfoProvider.GetType(i);
                        if (type == ChildType.Bone)
                        {
                            XmlElement boneElement = scml.CreateElement("bone_ref");
                            boneElement.SetAttribute("id", i.ToString());
                            boneElement.SetAttribute("timeline", i.ToString());
                            boneElement.SetAttribute("key", j.ToString());
                            if (frame.ParentId != -1)
                            {
                                boneElement.SetAttribute("parent", frame.ParentId.ToString());
                            }
                            keyElement.AppendChild(boneElement);
                        }
                        else if (type == ChildType.Sprite)
                        {
                            XmlElement objectElement = scml.CreateElement("object_ref");
                            objectElement.SetAttribute("id", i.ToString());
                            objectElement.SetAttribute("timeline", i.ToString());
                            objectElement.SetAttribute("key", j.ToString());
                            if (frame.ParentId != -1)
                            {
                                objectElement.SetAttribute("parent", frame.ParentId.ToString());
                            }
                            objectElement.SetAttribute("z_index", frame.ZIndex.ToString());
                            keyElement.AppendChild(objectElement);
                        }
                    }
                }
                mainlineElement.AppendChild(keyElement);
            }
            animationElement.AppendChild(mainlineElement);
            for (int i = 0; i < animation.FrameArray.Count; i++)
            {
                XmlElement timelineElement = scml.CreateElement("timeline");
                timelineElement.SetAttribute("id", i.ToString());
                timelineElement.SetAttribute("name", animation.InfoProvider.GetName(i));
                for (int j = 0; j < animation.FrameArray[0].Count; j++)
                {
                    if (animation.FrameArray[i][j] != null)
                    {
                        XmlElement keyElement = scml.CreateElement("key");
                        keyElement.SetAttribute("id", j.ToString());
                        keyElement.SetAttribute("time", (j * animation.Step).ToString());
                        ProcessingFrame frame = animation.FrameArray[i][j];
                        ChildType type = animation.InfoProvider.GetType(i);
                        if (type == ChildType.Bone)
                        {
                            XmlElement boneElement = scml.CreateElement("bone");
                            boneElement.SetAttribute("x", frame.X.ToString());
                            boneElement.SetAttribute("y", frame.Y.ToString());
                            boneElement.SetAttribute("angle", frame.Angle.ToString());
                            boneElement.SetAttribute("scale_x", frame.ScaleX.ToString());
                            boneElement.SetAttribute("scale_y", frame.ScaleY.ToString());
                            keyElement.AppendChild(boneElement);
                        }
                        else if (type == ChildType.Sprite)
                        {
                            XmlElement objectElement = scml.CreateElement("object");
                            objectElement.SetAttribute("folder", frame.Folder.ToString());
                            objectElement.SetAttribute("file", frame.File.ToString());
                            objectElement.SetAttribute("x", frame.X.ToString());
                            objectElement.SetAttribute("y", frame.Y.ToString());
                            objectElement.SetAttribute("angle", frame.Angle.ToString());
                            objectElement.SetAttribute("scale_x", frame.ScaleX.ToString());
                            objectElement.SetAttribute("scale_y", frame.ScaleY.ToString());
                            keyElement.AppendChild(objectElement);
                        }
                        timelineElement.AppendChild(keyElement);
                    }
                }
                animationElement.AppendChild(timelineElement);
            }
            return animationElement;
        }

        private XmlElement GetFirstChildByName(XmlElement parent, string tagName)
        {
            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node is XmlElement && node.Name.Equals(tagName))
                {
                    return (XmlElement)node;
                }
            }
            return null;
        }
    }
}
