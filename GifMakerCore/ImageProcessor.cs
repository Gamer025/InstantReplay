using ImageMagick;

namespace GifMaker
{
    internal class ImageProcessor
    {
        MagickImage[] destination;
        int index;
        byte[] compressedBytes;
        int origSize;
        MagickReadSettings settings;
        int delay;
        float scale;
        public ImageProcessor(ref MagickImage[] destination, int index, byte[] compressedBytes, int origSize, MagickReadSettings settings, int delay, float scale)
        {
            this.destination = destination;
            this.index = index;
            this.compressedBytes = compressedBytes;
            this.origSize = origSize;
            this.settings = settings;
            this.delay = delay;
            this.scale = scale;
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
                image.AdaptiveResize((int)(image.Width * scale), 0);
            }
            image.Quantize(new QuantizeSettings()
            {
                Colors = 128
            });
            destination[index] = image;
        }
    }
}
