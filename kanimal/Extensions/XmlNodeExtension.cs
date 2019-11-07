using System.ComponentModel;
using System.Linq;
using System.Xml;

namespace kanimal
{
    public static class XmlNodeExtension
    {
        public static string GetDefault(this XmlNode node, string attrName, string defaultValue)
        {
            if (node.Attributes[attrName] == null) return defaultValue;

            return node.Attributes[attrName].Value;
        }

        public static T GetDefault<T>(this XmlNode node, string attrName, T defaultValue)
        {
            if (node.Attributes[attrName] == null) return defaultValue;

            var converter = TypeDescriptor.GetConverter(typeof(T));

            return (T) converter.ConvertFromString(node.Attributes[attrName].Value);
        }

        public static XmlElement FirstElementChild(this XmlNode node)
        {
            return node.ChildNodes.GetElements().First();
        }

        public static float Interpolate(this XmlNode node, XmlNode nextNode, int time, string attrName,
            float defaultValue = 0f)
        {
            var t1 = int.Parse(node.GetDefault("time", "0"));
            var v1 = node.FirstElementChild().GetDefault(attrName, defaultValue);
            var t2 = int.Parse(nextNode.GetDefault("time", "0"));
            var v2 = nextNode.FirstElementChild().GetDefault(attrName, defaultValue);
            var r = Utilities.Interpolate(t1, v1, time, t2, v2);
            return r;
        }
    }
}