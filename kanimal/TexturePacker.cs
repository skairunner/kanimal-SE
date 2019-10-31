using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using kanimal;
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
        public string Name => sprite.Name;
        
        public Bitmap Sprite => sprite.Bitmap;

        public string BaseName => Utilities.GetSpriteBaseName(Name);
    }

    // a sprite that will be packed
    public struct Sprite
    {
        public Bitmap Bitmap;
        public string Name;

        public int Area => Bitmap.Height * Bitmap.Width;
        public int Height => Bitmap.Height;
        public int Width => Bitmap.Width;
    }
    
    public class TexturePacker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private List<Tuple<string, Bitmap>> sprites;
        public Bitmap SpriteSheet;
        public List<PackedSprite> SpriteAtlas;

        public TexturePacker(List<Tuple<string, Bitmap>> sprites)
        {
            this.sprites = sprites;
            SpriteAtlas = new List<PackedSprite>();
            
            Pack();
        }

        // Returns a histogram of unique names (e.g. mySprite_0 and mySprite_1 are both named mySprite)
        public Dictionary<string, int> GetHistogram()
        {
            var histogram = new Dictionary<string, int>();
            foreach (var entry in SpriteAtlas)
            {
                var baseName = Utilities.GetSpriteBaseName(entry.Name);
                if (histogram.ContainsKey(baseName))
                {
                    histogram[baseName] += 1;
                }
                else
                {
                    histogram[baseName] = 1;
                }
            }

            return histogram;
        }

        private void Pack()
        {
            // Brute force trial-and-error sprite packing.
            // Double the smaller axis of the sheet each time it fails.
            var sheet_w = 256;
            var sheet_h = 256;

            while (!TryPack(sheet_w, sheet_h))
            {
                if (sheet_w > sheet_h)
                    sheet_h *= 2;
                else
                    sheet_w *= 2;
            }

            using (var grD = Graphics.FromImage(SpriteSheet))
            {
                foreach (var sprite in SpriteAtlas)
                {
                    grD.DrawImage(sprite.Sprite,
                        new Rectangle(sprite.X, sprite.Y, sprite.Width, sprite.Height));
                } 
            }
            Logger.Info($"Packed {sheet_w} x {sheet_h}");
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
                if (rect.GetArea() == 0)
                {
                    return false;
                }
                SpriteAtlas.Add(new PackedSprite(rect.X, rect.Y, sprite));
            }
            
            SpriteSheet = new Bitmap(sheet_w, sheet_h);

            return true;
        }
    }
}