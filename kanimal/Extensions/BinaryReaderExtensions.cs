using System.IO;

namespace kanimal
{
    public static class BinaryReaderExtensions
    {
        public static string ReadPString(this BinaryReader reader)
        {
            var i = reader.ReadInt32();
            if (i <= 0) return "";
            var buff = reader.ReadBytes(i);
            return System.Text.Encoding.ASCII.GetString(buff);
        }
    }
}