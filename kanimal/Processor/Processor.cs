using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace kanimal
{
    // The idea is that all processors should take in
    // SCML XMLDocuments and perform some processing
    // that creates a new SCML XMLDocument that still
    // generates an identical animation to the original
    // but has changed the SCML XMLDocument to have some
    // different properties compared to the original
    public abstract class Processor
    {
        // Take in the original SCML and return the processed SCML
        // does not make any modifications to the original SCML
        // the processed SCML is made from a deep copy
        public abstract XmlDocument Process(XmlDocument original);
    }
}
