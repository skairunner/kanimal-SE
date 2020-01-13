using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NLog;
using MaxRectsBinPack;

namespace kanimal
{
    public class PackedSprite
    {
        private Sprite sprite;

        public PackedSprite(int x, int y, Sprite sprite)
        {
            X = x;
            Y = y;
            this.sprite = sprite;
        }

        public int X { get; }
        public int Y { get; }
        public int Width => sprite.Width;
        public int Height => sprite.Height;
        public SpriteName SpriteName => sprite.Name;

        public Bitmap Sprite => sprite.Bitmap;

        public SpriteBaseName BaseName => sprite.Name.ToBaseName();
    }

    // a sprite that will be packed
    public struct Sprite
    {
        public Bitmap Bitmap;
        public SpriteName Name;

        public int Area => Bitmap.Height * Bitmap.Width;
        public int Height => Bitmap.Height;
        public int Width => Bitmap.Width;
    }

    public class TexturePacker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private List<Tuple<SpriteName, Bitmap>> sprites;
        public Bitmap SpriteSheet;
        public List<PackedSprite> SpriteAtlas;

        public TexturePacker(List<Tuple<SpriteName, Bitmap>> sprites)
        {
            this.sprites = sprites;
            SpriteAtlas = new List<PackedSprite>();

            Pack();
        }

        // Returns a histogram of unique names (e.g. mySprite_0 and mySprite_1 are both named mySprite)
        public Dictionary<SpriteBaseName, int> GetHistogram()
        {
            var histogram = new Dictionary<SpriteBaseName, int>();
            foreach (var entry in SpriteAtlas)
            {
                if (histogram.ContainsKey(entry.BaseName))
                    histogram[entry.BaseName] += 1;
                else
                    histogram[entry.BaseName] = 1;
            }

            return histogram;
        }

        private void Pack()
        {
            // Brute force trial-and-error sprite packing.
            // Double the smaller axis of the sheet each time it fails.
            var sheetW = 256;
            var sheetH = 256;

            while (!TryPack(sheetW, sheetH))
                if (sheetW > sheetH)
                    sheetH *= 2;
                else
                    sheetW *= 2;

            using (var grD = Graphics.FromImage(SpriteSheet))
            {
                foreach (var sprite in SpriteAtlas)
                    grD.DrawImage(sprite.Sprite,
                        new Rectangle(sprite.X, sprite.Y, sprite.Width, sprite.Height));
            }

            Logger.Info($"Packed {sheetW} x {sheetH}");
        }

        // returns false when the packing failed. 
        private bool TryPack(int sheet_w, int sheet_h)
        {
            SpriteAtlas.Clear();

            // load all sprites into list and sort
            var spritesToPack = new List<Sprite>(
                sprites.Select(sprite => new Sprite
                {
                    Bitmap = sprite.Item2,
                    Name = sprite.Item1
                }));
            spritesToPack.Sort((sprite1, sprite2) => sprite2.Area.CompareTo(sprite1.Area));

            var packer = new MaxRectsBinPack.MaxRectsBinPack(sheet_w, sheet_h, false);

            foreach (var sprite in spritesToPack)
            {
                var rect = packer.Insert(sprite.Width, sprite.Height, FreeRectChoiceHeuristic.RectBestShortSideFit);
                if (rect.GetArea() == 0) return false;
                SpriteAtlas.Add(new PackedSprite(rect.X, rect.Y, sprite));
            }

            SpriteSheet = new Bitmap(sheet_w, sheet_h);

            return true;
        }
    }
}