using System.Drawing;

namespace kanimal
{
    public static class RectangleExtensions
    {
        public static int GetArea(this Rectangle r)
        {
            return r.Height * r.Width;
        }
    }
}