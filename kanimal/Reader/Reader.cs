using System.Collections.Generic;
using System.Drawing;

namespace kanimal
{
    // The idea is that all readers should build the in-memory representation of 
    // Build, Anim, and the various ancillary data structures. 
    // Then you can pass the data to the writers, which transform that common representation
    // into the appropriate file format. For the subset of animation that we support,
    // theoretically we can support any direction conversion, even Spriter -> Synfig or 
    // something absolutely daft like kanim -> kanim (which happens to be a good test)
    public abstract class Reader
    {
        public Dictionary<int, SpriteBaseName> BuildHashes;
        public Dictionary<int, string> AnimHashes;
        public KBuild.Build BuildData;
        public List<KBuild.Row> BuildTable;
        public KAnim.Anim AnimData;
        public List<Sprite> Sprites;

        // Read the file specified in the constructor and populate the above data entries.
        public abstract void Read();

        // Build the table by reading from hashTable and buildData
        public void BuildBuildTable(int imgW, int imgH)
        {
            var buildTable = new List<KBuild.Row>();
            foreach (var symbol in BuildData.Symbols)
            foreach (var frame in symbol.Frames)
            {
                var row = new KBuild.Row
                {
                    Build = BuildData,
                    Name = BuildHashes[symbol.Hash],
                    Index = frame.SourceFrameNum,
                    Hash = symbol.Hash,
                    Time = frame.Time,
                    Duration = frame.Duration,
                    X1 = frame.X1 * imgW,
                    Y1 = (1f - frame.Y1) * imgH,
                    X2 = frame.X2 * imgW,
                    Y2 = (1 - frame.Y2) * imgH,
                    Width = (frame.X2 - frame.X1) * imgW,
                    Height = (frame.Y2 - frame.Y1) * imgH,
                    PivotX = frame.PivotX,
                    PivotY = frame.PivotY,
                    PivotHeight = frame.PivotHeight,
                    PivotWidth = frame.PivotWidth
                };
                buildTable.Add(row);
            }

            BuildTable = buildTable;
        }

        public abstract Bitmap GetSpriteSheet();
    }
}