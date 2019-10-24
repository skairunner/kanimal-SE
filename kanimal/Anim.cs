using System.Collections.Generic;

namespace kanimal
{
    namespace KAnim
    {
        public struct Anim: IToDebugString
        {
            public int Version, ElementCount, FrameCount, AnimCount, MaxVisibleSymbolFrames;
            public List<AnimBank> Anims;

            public string ToDebugString()
            {
                return $"v{Version} has {AnimCount} different animations with {FrameCount} frames and "
                       + $"{ElementCount} elements with {MaxVisibleSymbolFrames} maximum visible symbol frames";
            }
        }

        public struct AnimBank
        {
            public string Name;
            public int Hash, FrameCount;
            public float Rate;
            public Dictionary<string, int> ElementIdMap;
            public List<Frame> Frames;
        }

        public struct Frame
        {
            public float X, Y, Width, Height;
            public int ElementCount;
            public List<Element> Elements;
        }

        public struct Element
        {
            public int Image, Index, Layer, Flags;
            // flags has just one value
            // 1-> fg
            // but we don't represent/deal with that in  spriter so we always leave
            // it as 0
            
            public int ZIndex; // only used in scml -> kanim conversion
            
            public float A, B, G, R, M1, M2, M3, M4, M5, M6;
            /*
             * m1 m2 m3 m4 make up rotation + scaling matrix for element on this frame
             * m5 m6 are the position offset of the element on this frame
             * specifically [a b]     [m1 m2]
             * 				[c d]  =  [m3 m4]
             * 	and (x, y) = (m5, m6)
             *
             * 	okay after some further investigation they don't *exactly* just mean
             * 	rotation and translation - it's a 2x3 tranformation matrix so it
             * 	can have some more complex stuff going on in the transformation but
             * 	it can be broken down into mostly accurate rotation + scaling + translation
             *
             * 	luckily going from rotation + scaling + translation to a 2x3 transformation matrix
             * 	is well defined even if the reverse isn't
             */
            public float Order;
        }
    }
}