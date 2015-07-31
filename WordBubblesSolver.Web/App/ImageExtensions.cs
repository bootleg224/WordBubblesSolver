using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;

namespace WordBubblesSolver.Web.App
{
    public static class ImageExtensions
    {
        public static Bitmap ConvertToFormat(this Image image, PixelFormat format)
        {
            var copy = new Bitmap(image.Width, image.Height, format);
            using (var gr = Graphics.FromImage(copy))
            {
                gr.DrawImage(image, new Rectangle(0, 0, copy.Width, copy.Height));
            }
            return copy;
        }

        public static string ToBase64(this Bitmap image)
        {
            using (var ms = new MemoryStream())
            {
                // Convert Image to byte[]
                image.Save(ms, ImageFormat.Png);
                byte[] imageBytes = ms.ToArray();

                // Convert byte[] to Base64 String
                string base64String = Convert.ToBase64String(imageBytes);
                return base64String;
            }
        }

        public static Image FromBase64(this string base64)
        {
            var bytes = Convert.FromBase64String(base64);
            Image image;
            using (var ms = new MemoryStream(bytes))
            {
                image = Image.FromStream(ms);
            }

            return image;
        }
    }
}