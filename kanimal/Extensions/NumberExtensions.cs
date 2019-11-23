using System.Globalization;

namespace kanimal
{
    public static class NumberExtensions
    {
        public static string ToStringInvariant(this float f)
        {
            return f.ToString(CultureInfo.InvariantCulture);
        }
        
        public static string ToStringInvariant(this int i)
        {
            return i.ToString(CultureInfo.InvariantCulture);
        }
        
        public static string ToStringInvariant(this double d)
        {
            return d.ToString(CultureInfo.InvariantCulture);
        }
    }
}