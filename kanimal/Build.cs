using System.Collections.Generic;

namespace kanimal
{
    namespace KBuild
    {
        public struct Build : IToDebugString
        {
            public int Version, SymbolCount, FrameCount;
            public string Name;

            public List<Symbol> Symbols;

            public string ToDebugString()
            {
                return $"{Name} v{Version}\nthere are {SymbolCount} symbols and {FrameCount} frames";
            }
        }

        public struct Symbol
        {
            public int Hash, Path, Color, Flags, FrameCount;
            // flags has 4 flags
            // 1 -> bloom
            // 2  -> onlight
            // 4 -> snapto
            // 8 -> fg
            // none of these matter for spriter and spriter can't write to any of these either
            // so flag always = 0 for translation purposes

            public List<Frame> Frames;
        }

        public struct Frame
        {
            public int SourceFrameNum, Duration, BuildImageIndex;

            // these are the pivot information for the sprite image
            public float PivotX, PivotY, PivotWidth, PivotHeight;

            // so these x y coordinates are actually uv texture coordinates - floats in the range 0 to 1
            public float X1, Y1, X2, Y2;
            public int Time;
        }

        public struct Row : IToDebugString
        {
            public Build Build;
            public SpriteBaseName Name;
            public int Index, Hash, Time, Duration;
            public float X1, Y1, X2, Y2, Width, Height, PivotX, PivotY, PivotWidth, PivotHeight;

            public SpriteName GetSpriteName()
            {
                return new SpriteName($"{Name}_{Index}");
            }
            
            public string ToDebugString()
            {
                return $"for symbol {Name}, frame index {Index} has duration {Duration}"
                       + $"and occupies rectangle ({X1}, {Y1}, {X2}, {Y2}) and has size {Width} x {Height}\n"
                       + $"pivot information: offset=({PivotX}, {PivotY} comparedToSize=({PivotWidth}, {PivotHeight})";
            }
        }
    }
}