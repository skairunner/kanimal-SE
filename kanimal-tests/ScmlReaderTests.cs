using System.Linq;
using kanimal;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;
using NUnit.Framework;

namespace kanimal_tests
{
    public class ScmlReaderTests
    {
        private static ScmlContext Minimal;
        
        [SetUp]
        public void Setup()
        {
            Minimal = new ScmlContext("testcases.minimal");
        }

        [Test]
        // The minimal case has a simple build content and thus a known state.
        public void TestMinimalBuildCorrect()
        {
            var reader = new ScmlReader(Minimal.Scml, Minimal.Sprites);
            reader.Read();

            var buildData = reader.BuildData;
            Assert.AreEqual(1, buildData.SymbolCount);
            Assert.AreEqual(1, buildData.FrameCount);
            Assert.AreEqual(1696137821, buildData.Symbols[0].Hash);
            var frames = buildData.Symbols[0].Frames;
            Assert.AreEqual(1, frames.Count);
            Assert.AreEqual(1, frames[0].Duration);
            Assert.AreEqual(0, frames[0].BuildImageIndex);
            Assert.AreEqual(100, frames[0].PivotX);
            Assert.AreEqual(100, frames[0].PivotY);

            var hashtable = reader.BuildHashes;
            Assert.AreEqual(1, hashtable.Count);
            Assert.AreEqual(1696137821, hashtable.Keys.First());
            Assert.AreEqual("square", hashtable.Values.First().Value);
        }
    }
}