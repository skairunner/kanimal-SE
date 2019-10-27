/*
 	Based on the Public Domain MaxRectsBinPack.cpp source by Jukka Jylänki
 	https://github.com/juj/RectangleBinPack/
 
 	Ported to C# by Sven Magnus
 	This version is also public domain - do whatever you want with it.
*/

using System;
using System.Collections.Generic;
using System.Drawing;

namespace MaxRectsBinPack
{
    using Rect = Rectangle;
    public enum FreeRectChoiceHeuristic
    {
        RectBestShortSideFit,

        ///< -BSSF: Positions the rectangle against the short side of a free rectangle into which it fits the best.
        RectBestLongSideFit,

        ///< -BLSF: Positions the rectangle against the long side of a free rectangle into which it fits the best.
        RectBestAreaFit,

        ///< -BAF: Positions the rectangle into the smallest free rect into which it fits.
        RectBottomLeftRule,

        ///< -BL: Does the Tetris placement.
        RectContactPointRule ///< -CP: Choosest the placement where the rectangle touches other rects as much as possible.
    }

        public class MaxRectsBinPack
        {

            public bool allowRotations;
            public int binHeight;

            public int binWidth;
            public List<Rect> freeRectangles = new List<Rect>();

            public List<Rect> usedRectangles = new List<Rect>();

            public MaxRectsBinPack(int width, int height, bool rotations = true)
            {
                Init(width, height, rotations);
            }

            public void Init(int width, int height, bool rotations = true)
            {
                binWidth = width;
                binHeight = height;
                allowRotations = rotations;

                Rect n = new Rect();
                n.X = 0;
                n.Y = 0;
                n.Width = width;
                n.Height = height;

                usedRectangles.Clear();

                freeRectangles.Clear();
                freeRectangles.Add(n);
            }

            public Rect Insert(int width, int height, FreeRectChoiceHeuristic method)
            {
                Rect newNode = new Rect();
                var score1 = 0; // Unused in this function. We don't need to know the score after finding the position.
                var score2 = 0;
                switch (method)
                {
                    case FreeRectChoiceHeuristic.RectBestShortSideFit:
                        newNode = FindPositionForNewNodeBestShortSideFit(width, height, ref score1, ref score2);
                        break;
                    case FreeRectChoiceHeuristic.RectBottomLeftRule:
                        newNode = FindPositionForNewNodeBottomLeft(width, height, ref score1, ref score2);
                        break;
                    case FreeRectChoiceHeuristic.RectContactPointRule:
                        newNode = FindPositionForNewNodeContactPoint(width, height, ref score1);
                        break;
                    case FreeRectChoiceHeuristic.RectBestLongSideFit:
                        newNode = FindPositionForNewNodeBestLongSideFit(width, height, ref score2, ref score1);
                        break;
                    case FreeRectChoiceHeuristic.RectBestAreaFit:
                        newNode = FindPositionForNewNodeBestAreaFit(width, height, ref score1, ref score2);
                        break;
                }

                if (newNode.Height == 0)
                    return newNode;

                var numRectanglesToProcess = freeRectangles.Count;
                for (var i = 0; i < numRectanglesToProcess; ++i)
                    if (SplitFreeNode(freeRectangles[i], ref newNode))
                    {
                        freeRectangles.RemoveAt(i);
                        --i;
                        --numRectanglesToProcess;
                    }

                PruneFreeList();

                usedRectangles.Add(newNode);
                return newNode;
            }

            public void Insert(List<Rect> rects, List<Rect> dst, FreeRectChoiceHeuristic method)
            {
                dst.Clear();

                while (rects.Count > 0)
                {
                    var bestScore1 = int.MaxValue;
                    var bestScore2 = int.MaxValue;
                    var bestRectIndex = -1;
                    Rect bestNode = new Rect();

                    for (var i = 0; i < rects.Count; ++i)
                    {
                        var score1 = 0;
                        var score2 = 0;
                        Rect newNode = ScoreRect((int) rects[i].Width, (int) rects[i].Height, method, ref score1,
                            ref score2);

                        if (score1 < bestScore1 || score1 == bestScore1 && score2 < bestScore2)
                        {
                            bestScore1 = score1;
                            bestScore2 = score2;
                            bestNode = newNode;
                            bestRectIndex = i;
                        }
                    }

                    if (bestRectIndex == -1)
                        return;

                    PlaceRect(bestNode);
                    rects.RemoveAt(bestRectIndex);
                }
            }

            private void PlaceRect(Rect node)
            {
                var numRectanglesToProcess = freeRectangles.Count;
                for (var i = 0; i < numRectanglesToProcess; ++i)
                    if (SplitFreeNode(freeRectangles[i], ref node))
                    {
                        freeRectangles.RemoveAt(i);
                        --i;
                        --numRectanglesToProcess;
                    }

                PruneFreeList();

                usedRectangles.Add(node);
            }

            private Rect ScoreRect(int width, int height, FreeRectChoiceHeuristic method, ref int score1,
                ref int score2)
            {
                Rect newNode = new Rect();
                score1 = int.MaxValue;
                score2 = int.MaxValue;
                switch (method)
                {
                    case FreeRectChoiceHeuristic.RectBestShortSideFit:
                        newNode = FindPositionForNewNodeBestShortSideFit(width, height, ref score1, ref score2);
                        break;
                    case FreeRectChoiceHeuristic.RectBottomLeftRule:
                        newNode = FindPositionForNewNodeBottomLeft(width, height, ref score1, ref score2);
                        break;
                    case FreeRectChoiceHeuristic.RectContactPointRule:
                        newNode = FindPositionForNewNodeContactPoint(width, height, ref score1);
                        score1 = -score1; // Reverse since we are minimizing, but for contact point score bigger is better.
                        break;
                    case FreeRectChoiceHeuristic.RectBestLongSideFit:
                        newNode = FindPositionForNewNodeBestLongSideFit(width, height, ref score2, ref score1);
                        break;
                    case FreeRectChoiceHeuristic.RectBestAreaFit:
                        newNode = FindPositionForNewNodeBestAreaFit(width, height, ref score1, ref score2);
                        break;
                }

                // Cannot fit the current rectangle.
                if (newNode.Height == 0)
                {
                    score1 = int.MaxValue;
                    score2 = int.MaxValue;
                }

                return newNode;
            }

            /// Computes the ratio of used surface area.
            public float Occupancy()
            {
                ulong usedSurfaceArea = 0;
                for (var i = 0; i < usedRectangles.Count; ++i)
                    usedSurfaceArea += (uint) usedRectangles[i].Width * (uint) usedRectangles[i].Height;

                return (float) usedSurfaceArea / (binWidth * binHeight);
            }

            private Rect FindPositionForNewNodeBottomLeft(int width, int height, ref int bestY, ref int bestX)
            {
                Rect bestNode = new Rect();
                //memset(bestNode, 0, sizeof(Rect));

                bestY = int.MaxValue;

                for (var i = 0; i < freeRectangles.Count; ++i)
                {
                    // Try to place the rectangle in upright (non-flipped) orientation.
                    if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                    {
                        var topSideY = (int) freeRectangles[i].Y + height;
                        if (topSideY < bestY || topSideY == bestY && freeRectangles[i].X < bestX)
                        {
                            bestNode.X = freeRectangles[i].X;
                            bestNode.Y = freeRectangles[i].Y;
                            bestNode.Width = width;
                            bestNode.Height = height;
                            bestY = topSideY;
                            bestX = (int) freeRectangles[i].X;
                        }
                    }

                    if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                    {
                        var topSideY = (int) freeRectangles[i].Y + width;
                        if (topSideY < bestY || topSideY == bestY && freeRectangles[i].X < bestX)
                        {
                            bestNode.X = freeRectangles[i].X;
                            bestNode.Y = freeRectangles[i].Y;
                            bestNode.Width = height;
                            bestNode.Height = width;
                            bestY = topSideY;
                            bestX = (int) freeRectangles[i].X;
                        }
                    }
                }

                return bestNode;
            }

            private Rect FindPositionForNewNodeBestShortSideFit(int width, int height, ref int bestShortSideFit,
                ref int bestLongSideFit)
            {
                Rect bestNode = new Rect();
                //memset(&bestNode, 0, sizeof(Rect));

                bestShortSideFit = int.MaxValue;

                for (var i = 0; i < freeRectangles.Count; ++i)
                {
                    // Try to place the rectangle in upright (non-flipped) orientation.
                    if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                    {
                        int leftoverHoriz = Math.Abs(freeRectangles[i].Width - width);
                        int leftoverVert = Math.Abs(freeRectangles[i].Height - height);
                        int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                        int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                        if (shortSideFit < bestShortSideFit ||
                            shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit)
                        {
                            bestNode.X = freeRectangles[i].X;
                            bestNode.Y = freeRectangles[i].Y;
                            bestNode.Width = width;
                            bestNode.Height = height;
                            bestShortSideFit = shortSideFit;
                            bestLongSideFit = longSideFit;
                        }
                    }

                    if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                    {
                        int flippedLeftoverHoriz = Math.Abs((int) freeRectangles[i].Width - height);
                        int flippedLeftoverVert = Math.Abs((int) freeRectangles[i].Height - width);
                        int flippedShortSideFit = Math.Min(flippedLeftoverHoriz, flippedLeftoverVert);
                        int flippedLongSideFit = Math.Max(flippedLeftoverHoriz, flippedLeftoverVert);

                        if (flippedShortSideFit < bestShortSideFit || flippedShortSideFit == bestShortSideFit &&
                            flippedLongSideFit < bestLongSideFit)
                        {
                            bestNode.X = freeRectangles[i].X;
                            bestNode.Y = freeRectangles[i].Y;
                            bestNode.Width = height;
                            bestNode.Height = width;
                            bestShortSideFit = flippedShortSideFit;
                            bestLongSideFit = flippedLongSideFit;
                        }
                    }
                }

                return bestNode;
            }

            private Rect FindPositionForNewNodeBestLongSideFit(int width, int height, ref int bestShortSideFit,
                ref int bestLongSideFit)
            {
                Rect bestNode = new Rect();
                //memset(&bestNode, 0, sizeof(Rect));

                bestLongSideFit = int.MaxValue;

                for (var i = 0; i < freeRectangles.Count; ++i)
                {
                    // Try to place the rectangle in upright (non-flipped) orientation.
                    if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                    {
                        int leftoverHoriz = Math.Abs((int) freeRectangles[i].Width - width);
                        int leftoverVert = Math.Abs((int) freeRectangles[i].Height - height);
                        int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                        int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                        if (longSideFit < bestLongSideFit ||
                            longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit)
                        {
                            bestNode.X = freeRectangles[i].X;
                            bestNode.Y = freeRectangles[i].Y;
                            bestNode.Width = width;
                            bestNode.Height = height;
                            bestShortSideFit = shortSideFit;
                            bestLongSideFit = longSideFit;
                        }
                    }

                    if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                    {
                        int leftoverHoriz = Math.Abs((int) freeRectangles[i].Width - height);
                        int leftoverVert = Math.Abs((int) freeRectangles[i].Height - width);
                        int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                        int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                        if (longSideFit < bestLongSideFit ||
                            longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit)
                        {
                            bestNode.X = freeRectangles[i].X;
                            bestNode.Y = freeRectangles[i].Y;
                            bestNode.Width = height;
                            bestNode.Height = width;
                            bestShortSideFit = shortSideFit;
                            bestLongSideFit = longSideFit;
                        }
                    }
                }

                return bestNode;
            }

            private Rect FindPositionForNewNodeBestAreaFit(int width, int height, ref int bestAreaFit,
                ref int bestShortSideFit)
            {
                Rect bestNode = new Rect();
                //memset(&bestNode, 0, sizeof(Rect));

                bestAreaFit = int.MaxValue;

                for (var i = 0; i < freeRectangles.Count; ++i)
                {
                    var areaFit = (int) freeRectangles[i].Width * (int) freeRectangles[i].Height - width * height;

                    // Try to place the rectangle in upright (non-flipped) orientation.
                    if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                    {
                        int leftoverHoriz = Math.Abs((int) freeRectangles[i].Width - width);
                        int leftoverVert = Math.Abs((int) freeRectangles[i].Height - height);
                        int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

                        if (areaFit < bestAreaFit || areaFit == bestAreaFit && shortSideFit < bestShortSideFit)
                        {
                            bestNode.X = freeRectangles[i].X;
                            bestNode.Y = freeRectangles[i].Y;
                            bestNode.Width = width;
                            bestNode.Height = height;
                            bestShortSideFit = shortSideFit;
                            bestAreaFit = areaFit;
                        }
                    }

                    if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                    {
                        int leftoverHoriz = Math.Abs((int) freeRectangles[i].Width - height);
                        int leftoverVert = Math.Abs((int) freeRectangles[i].Height - width);
                        int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

                        if (areaFit < bestAreaFit || areaFit == bestAreaFit && shortSideFit < bestShortSideFit)
                        {
                            bestNode.X = freeRectangles[i].X;
                            bestNode.Y = freeRectangles[i].Y;
                            bestNode.Width = height;
                            bestNode.Height = width;
                            bestShortSideFit = shortSideFit;
                            bestAreaFit = areaFit;
                        }
                    }
                }

                return bestNode;
            }

            /// Returns 0 if the two intervals i1 and i2 are disjoint, or the length of their overlap otherwise.
            private int CommonIntervalLength(int i1start, int i1end, int i2start, int i2end)
            {
                if (i1end < i2start || i2end < i1start)
                    return 0;
                return Math.Min(i1end, i2end) - Math.Max(i1start, i2start);
            }

            private int ContactPointScoreNode(int x, int y, int width, int height)
            {
                var score = 0;

                if (x == 0 || x + width == binWidth)
                    score += height;
                if (y == 0 || y + height == binHeight)
                    score += width;

                for (var i = 0; i < usedRectangles.Count; ++i)
                {
                    if (usedRectangles[i].X == x + width || usedRectangles[i].X + usedRectangles[i].Width == x)
                        score += CommonIntervalLength((int) usedRectangles[i].Y,
                            (int) usedRectangles[i].Y + (int) usedRectangles[i].Height, y, y + height);
                    if (usedRectangles[i].Y == y + height || usedRectangles[i].Y + usedRectangles[i].Height == y)
                        score += CommonIntervalLength((int) usedRectangles[i].X,
                            (int) usedRectangles[i].X + (int) usedRectangles[i].Width, x, x + width);
                }

                return score;
            }

            private Rect FindPositionForNewNodeContactPoint(int width, int height, ref int bestContactScore)
            {
                Rect bestNode = new Rect();
                //memset(&bestNode, 0, sizeof(Rect));

                bestContactScore = -1;

                for (var i = 0; i < freeRectangles.Count; ++i)
                {
                    // Try to place the rectangle in upright (non-flipped) orientation.
                    if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
                    {
                        var score = ContactPointScoreNode((int) freeRectangles[i].X, (int) freeRectangles[i].Y, width,
                            height);
                        if (score > bestContactScore)
                        {
                            bestNode.X = (int) freeRectangles[i].X;
                            bestNode.Y = (int) freeRectangles[i].Y;
                            bestNode.Width = width;
                            bestNode.Height = height;
                            bestContactScore = score;
                        }
                    }

                    if (allowRotations && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
                    {
                        var score = ContactPointScoreNode((int) freeRectangles[i].X, (int) freeRectangles[i].Y, height,
                            width);
                        if (score > bestContactScore)
                        {
                            bestNode.X = (int) freeRectangles[i].X;
                            bestNode.Y = (int) freeRectangles[i].Y;
                            bestNode.Width = height;
                            bestNode.Height = width;
                            bestContactScore = score;
                        }
                    }
                }

                return bestNode;
            }

            private bool SplitFreeNode(Rect freeNode, ref Rect usedNode)
            {
                // Test with SAT if the rectangles even intersect.
                if (usedNode.X >= freeNode.X + freeNode.Width || usedNode.X + usedNode.Width <= freeNode.X ||
                    usedNode.Y >= freeNode.Y + freeNode.Height || usedNode.Y + usedNode.Height <= freeNode.Y)
                    return false;

                if (usedNode.X < freeNode.X + freeNode.Width && usedNode.X + usedNode.Width > freeNode.X)
                {
                    // New node at the top side of the used node.
                    if (usedNode.Y > freeNode.Y && usedNode.Y < freeNode.Y + freeNode.Height)
                    {
                        Rect newNode = freeNode;
                        newNode.Height = usedNode.Y - newNode.Y;
                        freeRectangles.Add(newNode);
                    }

                    // New node at the bottom side of the used node.
                    if (usedNode.Y + usedNode.Height < freeNode.Y + freeNode.Height)
                    {
                        Rect newNode = freeNode;
                        newNode.Y = usedNode.Y + usedNode.Height;
                        newNode.Height = freeNode.Y + freeNode.Height - (usedNode.Y + usedNode.Height);
                        freeRectangles.Add(newNode);
                    }
                }

                if (usedNode.Y < freeNode.Y + freeNode.Height && usedNode.Y + usedNode.Height > freeNode.Y)
                {
                    // New node at the left side of the used node.
                    if (usedNode.X > freeNode.X && usedNode.X < freeNode.X + freeNode.Width)
                    {
                        Rect newNode = freeNode;
                        newNode.Width = usedNode.X - newNode.X;
                        freeRectangles.Add(newNode);
                    }

                    // New node at the right side of the used node.
                    if (usedNode.X + usedNode.Width < freeNode.X + freeNode.Width)
                    {
                        Rect newNode = freeNode;
                        newNode.X = usedNode.X + usedNode.Width;
                        newNode.Width = freeNode.X + freeNode.Width - (usedNode.X + usedNode.Width);
                        freeRectangles.Add(newNode);
                    }
                }

                return true;
            }

            private void PruneFreeList()
            {
                for (var i = 0; i < freeRectangles.Count; ++i)
                for (var j = i + 1; j < freeRectangles.Count; ++j)
                {
                    if (IsContainedIn(freeRectangles[i], freeRectangles[j]))
                    {
                        freeRectangles.RemoveAt(i);
                        --i;
                        break;
                    }

                    if (IsContainedIn(freeRectangles[j], freeRectangles[i]))
                    {
                        freeRectangles.RemoveAt(j);
                        --j;
                    }
                }
            }

            private bool IsContainedIn(Rect a, Rect b)
            {
                return a.X >= b.X && a.Y >= b.Y
                                  && a.X + a.Width <= b.X + b.Width
                                  && a.Y + a.Height <= b.Y + b.Height;
            }
        }
}