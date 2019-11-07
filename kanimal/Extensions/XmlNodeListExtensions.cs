using System.Collections.Generic;
using System.Xml;

namespace kanimal
{
    public static class XmlNodeListExtensions
    {
        public static IEnumerable<XmlElement> GetElements(this XmlNodeList list)
        {
            for (var i = 0; i < list.Count; i++)
                if (list[i] is XmlElement)
                    yield return (XmlElement) list[i];
        }
    }
}