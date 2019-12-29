using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace kanimal
{
    /* provides sequential 0-indexed integers
     * that are used as ids for the timelines
     * so they can be arranged in an array */
    class TimelineInfoMap
    {

        public XmlDocument Scml { get; set; }

        public string Name { get; set; }

        public Dictionary<int, int> TimelineIdMap { get; set; }

        public Dictionary<int, ChildType> TimelineTypeMap { get; set; }

        public Dictionary<int, string> TimelineNameMap { get; set; }

        private int nextIndex = 0;

        public TimelineInfoMap(XmlDocument scml, string name)
        {
            Scml = scml;
            Name = name;
            TimelineIdMap = new Dictionary<int, int>();
            TimelineTypeMap = new Dictionary<int, ChildType>();
            TimelineNameMap = new Dictionary<int, string>();
            BuildTimelineIdMap();
        }

        /* get the assigned id for a specific timeline
         * this id is the value that should be used for the id field any time
         * this timeline is referenced */
        public int GetId(int timelineId)
        {
            return TimelineIdMap[timelineId];
        }

        /* get the assigned type for a specific timeline but not referenced by timeline id
         * instead referenced by its converted id (i.e. timelineId gone through GetId)
         * each timeline will either represent a bone for all of its frames
         * or a sprite for all of its frames - never can change for the duration
         * of just a single timeline */
        public ChildType GetType(int id)
        {
            return TimelineTypeMap[id];
        }

        /* get the assigned name for a specific timeline but not referenced by timeline id
         * instead referenced by its converted id (i.e. timelineId gone through GetId) */
        public string GetName(int id)
        {
            return TimelineNameMap[id];
        }

        /* gets the amount of ids that are maintained
         * should be used as the size of the array to know
         * how many elements it needs to contain each timeline
         * information */
        public int Size()
        {
            return nextIndex;
        }

        private void BuildTimelineIdMap()
        {
            XmlElement spriterData = (XmlElement)Scml.GetElementsByTagName("spriter_data")[0];
            XmlElement entity = GetFirstChildByName(spriterData, "entity");
            XmlElement animation = GetFirstChildByAttribute(entity, "name", Name);
            XmlElement mainline = GetFirstChildByName(animation, "mainline");
            foreach (XmlNode keyNode in mainline.ChildNodes)
            {
                foreach (XmlNode refNode in keyNode.ChildNodes)
                {
                    if (refNode is XmlElement &&
                        (refNode.Name.Equals("object_ref") || refNode.Name.Equals("bone_ref")))
                    {
                        int timeline = int.Parse(((XmlElement)refNode).GetAttribute("timeline"));
                        /* assign next index to this timeline id if it has not previously been assigned a value */
                        if (!TimelineIdMap.ContainsKey(timeline))
                        {
                            TimelineIdMap[timeline] = nextIndex++;

                            /* set the type of the timeline to either sprite or bone based on which is the first seen for the timline
                             * since the timeline type is invariant across frames */
                            TimelineTypeMap[TimelineIdMap[timeline]] = ChildType.Sprite;
                            if (refNode.Name.Equals("bone_ref"))
                            {
                                TimelineTypeMap[TimelineIdMap[timeline]] = ChildType.Bone;
                            }
                        }
                    }
                }
            }

            foreach (XmlNode timelineNode in animation.ChildNodes)
            {
                if (timelineNode is XmlElement && timelineNode.Name.Equals("timeline"))
                {
                    XmlElement timelineElement = (XmlElement)timelineNode;
                    int id = int.Parse(timelineElement.GetAttribute("id"));
                    string name = timelineElement.GetAttribute("name");
                    TimelineNameMap[TimelineIdMap[id]] = name;
                }
            }
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

        private XmlElement GetFirstChildByAttribute(XmlElement parent, string attributeName, string attributeValue)
        {
            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node is XmlElement)
                {
                    XmlElement element = (XmlElement)node;
                    if (element.HasAttribute(attributeName) && element.GetAttribute(attributeName) == attributeValue)
                    {
                        return element;
                    }
                }
            }
            return null;
        }

    }
}
