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
            public Dictionary<SpriterObjectName, int> ObjectIdMap;
            private AnimHashTable prevHashTable;
            public List<Frame> Frames;

            // Trying to find the maximum occurances per frame of each sprite, so we know how many
            // spriteName_#s we need to account for.
            // E.g. if foo_0 is used up to twice a frame, we need foo_0_0 and foo_0_1.
            // Otherwise we only need foo_0
            public Dictionary<SpriteName, int> BuildHistogram(AnimHashTable animHashes)
            {
                var overallHistogram = new Dictionary<SpriteName, int>();

                // Count the frequency of spriteNames used.
                foreach (var frame in Frames)
                {
                    var perFrameHistogram = new Dictionary<SpriteName, int>();
                    foreach (var element in frame.Elements)
                    {
                        var name = element.FindName(animHashes);
                        if (perFrameHistogram.ContainsKey(name))
                            perFrameHistogram[name] += 1;
                        else
                            perFrameHistogram[name] = 1;
                    }

                    // merge the frame's entries to the overall anim's entries
                    foreach (var entry in perFrameHistogram)
                        if (!overallHistogram.ContainsKey(entry.Key) || overallHistogram[entry.Key] < entry.Value)
                            overallHistogram[entry.Key] = entry.Value;
                }

                return overallHistogram;
            }

            // Build a map of object indexes.
            // We sequentially assign an object index to each object.
            // So foo_0_0 and foo_0_1 will have different indices, but 
            // foo_0_0 in a different frame will reference the same object... i think
            public Dictionary<SpriterObjectName, int> BuildIdMap(AnimHashTable animHashes)
            {
                if (animHashes == prevHashTable)
                    return ObjectIdMap;
                var histogram = BuildHistogram(animHashes);
                var idMap = new Dictionary<SpriterObjectName, int>();
                var index = 0;
                foreach (var entry in histogram)
                {
                    var name = entry.Key;
                    var occurrences = entry.Value;
                    for (var i = 0; i < occurrences; i++) {
                        idMap[name.ToSpriterObjectName(i)] = index++;
                    }
                }

                ObjectIdMap = idMap;
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

            public int ImageHash, // The Klei Hashed sprite name for the image.
                Index, // the index for this sprite. Klei animations tend to have sprites that are related to each other
                // have the same name, but different numbers associated with them. When animated, they tend to use the
                // sprites in order. E.g. water_0 is played, followed by water_1, water_2, water_3, then back to 0.
                Layer,
                Flags; // Flags only has one known value, 1 -> foreground.
                // Spriter does not represent this, though, and it doesn't seem like it's used anyways, so for now
                // we leave it as 0.

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

            // Finding the name of a sprite should only look at the sprite itself, not its index in the
            // symbol because this way all indices of a symbol can be part of the same timeline and just
            // be sprite-swapped between in the SCML
            public SpriteName FindName(AnimHashTable animHashes)
            {
                return new SpriteName(animHashes[ImageHash]);
            }

            // This method gets the name of the sprite plus its index which we don't use when building our internal
            // representation of the animation but we need to use in order to reference the sprite on disk for actually
            // indicating which sprite out of the symbol's frames we need to swap to on any given frame
            public SpriteName FindNameWithIndex(AnimHashTable animHashes)
            {
                return new SpriteName($"{animHashes[ImageHash]}_{Index}");
            }

            // This gets the name of the sprite but at any given index in the symbol. This is important for testing if certain indices
            // exist within a given symbol
            public SpriteName FindNameWithGivenIndex(AnimHashTable animHashes, int index)
            {
                return new SpriteName($"{animHashes[ImageHash]}_{index}");
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