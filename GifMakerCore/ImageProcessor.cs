using ImageMagick;
using System.Globalization;

namespace GifMaker
{
    internal class ImageProcessor
    {
        MagickImage[] destination;
        int index;
        byte[] compressedBytes;
        int origSize;
        MagickReadSettings settings;
        uint delay;
        float scale;
        string fileType;
        public ImageProcessor(ref MagickImage[] destination, int index, byte[] compressedBytes, int origSize, MagickReadSettings settings, uint delay, float scale, string fileType)
        {
            this.destination = destination;
            this.index = index;
            this.compressedBytes = compressedBytes;
            this.origSize = origSize;
            this.settings = settings;
            this.delay = delay;
            this.scale = scale;
            this.fileType = fileType;
        }

        internal void DownScaleImage(object? threadContext)
        {
            byte[] decompressedBytes = new byte[origSize];
            RLE.DecompressRGB(compressedBytes, decompressedBytes);
            MagickImage image = new MagickImage(decompressedBytes, settings);
            image.AnimationDelay = delay;
            image.GifDisposeMethod = GifDisposeMethod.Previous;
            image.Flip();
            //image.InterpolativeResize((int)(image.Width * scale), 0, PixelInterpolateMethod.Nearest);
            if (scale != 1.0)
            {
                image.AdaptiveResize((uint)(image.Width * scale), 0);
            }
            if (fileType == ".gif")
            {
                image.Quantize(new QuantizeSettings()
                {
                    Colors = 128
                });
            }
            if (fileType == ".webp")
            {
                //image.Quality = 1;
            }
            destination[index] = image;
        }
    }
}
