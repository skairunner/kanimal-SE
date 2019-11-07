using System.Collections.Generic;
using System.Text;

namespace kanimal
{
    public static class ListStringExtensions
    {
        public static string Join(this List<string> list, string delimiter = ", ")
        {
            var sb = new StringBuilder();
            for (var i = 0; i < list.Count; i++)
            {
                sb.Append(list[i]);
                if (i < list.Count - 1) sb.Append(delimiter);
            }

            return sb.ToString();
        }
    }
}