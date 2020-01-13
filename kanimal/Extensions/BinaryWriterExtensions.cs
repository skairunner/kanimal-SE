using System.IO;

namespace kanimal
{
    public static class BinaryWriterExtensions
    {
        public static void WritePString(this BinaryWriter writer, string str)
        {
            writer.Write(str.Length);
            writer.Write(str.ToCharArray());
        }
        
        public static void WritePString(this BinaryWriter writer, KName name)
        {
            writer.Write(name.Value.Length);
            writer.Write(name.Value.ToCharArray());
        }
    }
}