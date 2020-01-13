// Strongly typed names to reduce confusion when converting between many different kinds of
// names.

using System;
using System.IO;

namespace kanimal
{
    public abstract class KName
    {
        public string Value { get; protected set; }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return Value.Equals((obj as KName).Value);
        }

        public static bool operator ==(KName lhs, KName rhs)
        {
            if (ReferenceEquals(lhs, null))
            {
                if (ReferenceEquals(rhs, null))
                {
                    return true;
                }

                return false;
            }

            return lhs.Equals(rhs);
        }

        public static bool operator !=(KName lhs, KName rhs)
        {
            return !(lhs == rhs);
        }
    }
    
    // This is a name of a file. It may have a file extension, and should have an underscore and a number
    // at the end before the file extension.
    public class Filename: KName
    {
        public Filename(string filename)
        {
            Value = filename;
        }

        public static Filename FromPath(string filepath)
        {
            return new Filename(Path.GetFileName(filepath));
        }

        public SpriteName ToSpriteName()
        {
            return new SpriteName(Utilities.WithoutExtension(Value));
        }
    }

    // The name of a sprite, including an underscore and digit but with no file extension.
    public class SpriteName: KName
    {
        public int Index => Utilities.GetFrameCount(Value);
        public SpriteName(string spritename)
        {
            Value = spritename;
        }

        public static SpriteName FromFilename(Filename filename)
        {
            return filename.ToSpriteName();
        }

        public static SpriteName FromFilename(string filename)
        {
            return new SpriteName(Utilities.WithoutExtension(filename));
        }

        public SpriteBaseName ToBaseName()
        {
            return new SpriteBaseName(Utilities.GetSpriteBaseName(Value));
        }

        public Filename ToFilename(string extension = ".png")
        {
            return new Filename(Value + extension);
        }

        public SpriterObjectName ToSpriterObjectName(int suffix)
        {
            return new SpriterObjectName($"{Value}_{suffix}");
        }
    }

    // The base name of a sprite, excluding any extra info such as underscore digit or file extension.
    // Can be hashed to be turned into a KleiHash.
    public class SpriteBaseName : KName
    {
        public int KleiHashed => Utilities.KleiHash(Value);
        
        public SpriteBaseName(string basename)
        {
            Value = basename;
        }
    }

    public class SpriterObjectName : KName
    {
        public SpriterObjectName(string objectName)
        {
            Value = objectName;
        }
    }
}