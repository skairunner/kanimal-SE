using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace kanimal
{
    public static class BitmapExtensions
    {
        // direct copy pixels bytes to keep color value of transparent pixels
        public static void CopyTo(this Bitmap src, Bitmap dst, int X, int Y)
        {
            var intersect = Rectangle.Intersect(
                new Rectangle(0, 0, dst.Width, dst.Height),
                new Rectangle(X, Y, src.Width, src.Height));
            if (!intersect.IsEmpty)
            {
                var src_data = src.LockBits(
                    new Rectangle(0, 0, src.Width, src.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int src_bytes_per_line = Math.Abs(src_data.Stride);
                ReadOnlySpan<byte> src_bytes;
                unsafe
                {
                    src_bytes = new ReadOnlySpan<byte>(src_data.Scan0.ToPointer(), src_bytes_per_line * src_data.Height);
                }

                var dst_data = dst.LockBits(
                    new Rectangle(0, 0, dst.Width, dst.Height),
                    ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                int dst_bytes_per_line = Math.Abs(dst_data.Stride);
                int bytes_per_px = dst_bytes_per_line / dst_data.Width;
                Span<byte> dst_bytes;
                unsafe
                {
                    dst_bytes = new Span<byte>(dst_data.Scan0.ToPointer(), dst_bytes_per_line * dst_data.Height);
                }

                int bytes_to_copy = intersect.Width * bytes_per_px;
                for (int line = 0; line < intersect.Height; line++)
                {
                    src_bytes.Slice(((intersect.Y - Y + line) * src.Width + intersect.X - X) * bytes_per_px, bytes_to_copy)
                        .CopyTo(dst_bytes.Slice(((intersect.Y + line) * dst.Width + intersect.X) * bytes_per_px, bytes_to_copy));
                }
                src.UnlockBits(src_data);
                dst.UnlockBits(dst_data);
            }
        }
    }
}
