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
    }
}