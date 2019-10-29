using System.IO;

namespace kanimal
{
    public static class BinaryReaderExtensions
    {
        public static string ReadPString(this BinaryReader reader)
        {
            var L = reader.ReadInt32();
            if (L <= 0)
            {
                return "";
            }
            var buff = reader.ReadBytes(L);
            return System.Text.Encoding.ASCII.GetString(buff);
        }
    }
}