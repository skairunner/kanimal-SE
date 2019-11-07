using System;
using System.Collections.Generic;

namespace kanimal
{
    namespace KAnim
    {
        using AnimHashTable = Dictionary<int, string>;

        public struct Anim : IToDebugString
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
            private AnimHashTable prevHashTable;
            public List<Frame> Frames;

            public SortedDictionary<string, int> BuildHistogram(AnimHashTable animHashes)
            {
                var overallHistogram = new SortedDictionary<string, int>();

                foreach (var frame in Frames)
                {
                    var perFrameHistogram = new SortedDictionary<string, int>();
                    foreach (var element in frame.Elements)
                    {
                        var name = element.FindName(animHashes);
                        if (perFrameHistogram.ContainsKey(name))
                            perFrameHistogram[name] += 1;
                        else
                            perFrameHistogram[name] = 1;
                    }

                    // update overall histograms once maximums are found
                    foreach (var entry in perFrameHistogram)
                        if (!overallHistogram.ContainsKey(entry.Key) || overallHistogram[entry.Key] < entry.Value)
                            overallHistogram[entry.Key] = entry.Value;
                }

                return overallHistogram;
            }

            public Dictionary<string, int> BuildIdMap(AnimHashTable animHashes)
            {
                if (animHashes == prevHashTable) return ElementIdMap;
                var histogram = BuildHistogram(animHashes);
                var idMap = new Dictionary<string, int>();
                var index = 0;
                foreach (var entry in histogram)
                {
                    var name = entry.Key;
                    var occurrences = entry.Value;
                    for (var i = 0; i < occurrences; i++) idMap[Utilities.GetAnimIdName(name, i)] = index++;
                }

                ElementIdMap = idMap;
                prevHashTable = animHashes;
                return idMap;
            }
        }

        public struct Frame
        {
            public float X, Y, Width, Height;
            public int ElementCount;
            public List<Element> Elements;
        }

        public struct Element
        {
            public struct Transformation
            {
                public double X, Y, Angle, ScaleX, ScaleY;
            }

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

            public string FindName(AnimHashTable animHashes)
            {
                return $"{animHashes[Image]}_{Index}";
            }

            public string FindFilename(AnimHashTable animHashes)
            {
                return $"{animHashes[Image]}_{Index}";
            }

            // Takes the matrix values and returns a typical separate-component transform object
            public Transformation Decompose()
            {
                // is part of the formula for decomposing transformation matrix into components
                // see https://math.stackexchange.com/questions/237369/given-this-transformation-matrix-how-do-i-decompose-it-into-translation-rotati
                var scaleX = Math.Sqrt(M1 * M1 + M2 * M2);
                var scaleY = Math.Sqrt(M3 * M3 + M4 * M4);

                var det = M1 * M4 - M3 * M2;
                if (det < 0) scaleY *= -1;

                // still part of the formula for obtaining rotation component from combined rotation + scaling
                // undue scaling by dividing by scaling and then taking average value of sin/cos to make it more
                // accurate (b/c sin and cos appear twice each in 2d rotation matrix)
                var sinApprox = 0.5 * (M3 / scaleY - M2 / scaleX);
                var cosApprox = 0.5 * (M1 / scaleX + M4 / scaleY);

                var m1 = Utilities.ClampRange(-1, M1 / scaleX, 1);
                var m2 = Utilities.ClampRange(-1, M2 / scaleX, 1);
                var m3 = Utilities.ClampRange(-1, M3 / scaleY, 1);
                var m4 = Utilities.ClampRange(-1, M4 / scaleY, 1);

                var angle = Math.Atan2(sinApprox, cosApprox);

                // it seems as if the notion of simply having x,y, angle and scale are not really sufficient to describe the
                // transformation applied to each point since the 2x3 matrix m1...m6 doesn't nicely decompose into a valid rotation matrix
                // basically the two components that are sin are not equal and the two components that are cos are not equal. This would imply
                // that there is some additional transformation being applied to each point in addition to just the scale and rotation information
                // that makes it such that when we just look at that rotation information it does not produce the correct result
                if (angle < 0) angle += 2 * Math.PI;

                angle *= 180 / Math.PI;

                return new Transformation
                {
                    X = M5,
                    Y = M6,
                    Angle = angle,
                    ScaleX = scaleX,
                    ScaleY = scaleY
                };
            }
        }
    }
}