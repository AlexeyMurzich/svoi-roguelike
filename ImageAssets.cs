using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace ShpilSvoih
{
    // One owner and one decoded bitmap per resource. Views never dispose shared images.
    public sealed class ImageAssets : IDisposable
    {
        private readonly Assembly assembly;
        private readonly string directory;
        private readonly Dictionary<string, Bitmap> images = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        private readonly List<string> warnings = new List<string>();
        private bool disposed;
        public ImageAssets(Assembly assembly, string directory)
        {
            if (assembly == null)
                throw new ArgumentNullException("assembly");
            if (directory == null)
                throw new ArgumentNullException("directory");
            this.assembly = assembly;
            this.directory = directory;
        }

        public int Count
        {
            get
            {
                return images.Count;
            }
        }

        public string[] Warnings
        {
            get
            {
                return warnings.ToArray();
            }
        }

        public Bitmap Get(string relativePath, string resourceName)
        {
            if (disposed)
                throw new ObjectDisposedException("ImageAssets");
            Bitmap existing;
            if (images.TryGetValue(resourceName, out existing))
                return existing;
            Bitmap image = null;
            string path = Path.Combine(directory, relativePath);
            if (File.Exists(path))
            {
                try
                {
                    using (var stream = File.OpenRead(path))
                        image = Decode(stream, resourceName);
                }
                catch (Exception error)
                {
                    if (!(error is ArgumentException || error is IOException || error is UnauthorizedAccessException || error is OutOfMemoryException || error is System.Runtime.InteropServices.ExternalException))
                        throw;
                    warnings.Add("Не удалось прочитать " + relativePath + "; использована встроенная версия.");
                }
            }

            if (image == null)
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                        throw new InvalidDataException("Missing embedded image: " + resourceName);
                    image = Decode(stream, resourceName);
                }
            }

            images.Add(resourceName, image);
            return image;
        }

        private static Bitmap Decode(Stream stream, string resourceName)
        {
            using (var source = Image.FromStream(stream, true, true))
            {
                if (source.Width > 8192 || source.Height > 8192 || (long)source.Width * source.Height > 32000000)
                    throw new InvalidDataException("Image dimensions exceed the asset limit.");
                // Decode once into the premultiplied format used by GDI+ compositing.
                // Otherwise each cropped atlas draw converts the full source again.
                int displayWidth = source.Width;
                if (resourceName == "map-icons.png") displayWidth = Math.Min(displayWidth, 384);
                else if (resourceName == "menu-icons.png") displayWidth = Math.Min(displayWidth, 256);
                else if (resourceName == "health-icon.png" || resourceName == "coin-icon.png") displayWidth = Math.Min(displayWidth, 128);
                else if (resourceName.StartsWith("relics.", StringComparison.Ordinal)) displayWidth = Math.Min(displayWidth, 256);
                else if (resourceName == "hud-texture.png") displayWidth = Math.Min(displayWidth, 1086);
                int displayHeight = source.Height * displayWidth / source.Width;
                var bitmap = new Bitmap(displayWidth, displayHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                try
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(source, new Rectangle(0, 0, displayWidth, displayHeight), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
                    }
                    return bitmap;
                }
                catch
                {
                    bitmap.Dispose();
                    throw;
                }
            }
        }

        public void Reload(string resourceName)
        {
            if (disposed)
                throw new ObjectDisposedException("ImageAssets");
            Bitmap image;
            if (images.TryGetValue(resourceName, out image))
            {
                images.Remove(resourceName);
                image.Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            foreach (var image in images.Values)
                image.Dispose();
            images.Clear();
            disposed = true;
        }
    }
}
