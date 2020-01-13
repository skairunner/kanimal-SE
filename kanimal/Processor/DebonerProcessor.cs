using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using NLog;

namespace kanimal
{
    public class DebonerProcessor : Processor
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public override XmlDocument Process(XmlDocument original)
        {
            Logger.Warn("Deboning is not currently supported.");
            return original;
        }
    }
}
