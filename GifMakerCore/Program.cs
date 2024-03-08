using Gifmaker;
using ImageMagick;
using System.Text.Json;

namespace GifMaker
{
    internal class Program
    {
        static int Main(string[] args)
        {
            if (!File.Exists(args[0] + ".data") || !File.Exists(args[0] + ".meta"))
            {
                Console.WriteLine("Missing files");
                return 100;
            }

            GifMetadata meta = JsonSerializer.Deserialize<GifMetadata>(File.ReadAllText(args[0] + ".meta"), SourceGenerationContext.Default.GifMetadata);


            int delay = 100 / meta.FPS;

            MagickImage[] processedImages = new MagickImage[meta.Frames.Length];
            MagickReadSettings settings = new MagickReadSettings();
            settings.Width = meta.Width;
            settings.Height = (int)meta.Height;
            settings.Format = MagickFormat.Rgb;
            using (FileStream fs = new FileStream(args[0] + ".data",
            FileMode.Open, FileAccess.Read))
            {
                for (int i = 0; i < meta.Frames.Length; i++)
                {
                    GifMetadata.FrameData frame = meta.Frames[i];
                    byte[] compressedBytes = new byte[frame.FrameCompressedSize];
                    
                    fs.Read(compressedBytes, 0, frame.FrameCompressedSize);
                    
                    ImageProcessor scaler = new ImageProcessor(ref processedImages, i, compressedBytes, frame.FrameOrigSize, settings, delay, meta.Scale);
                    ThreadPool.QueueUserWorkItem(scaler.DownScaleImage);
                }
            }
            while (processedImages.Count(x => x != null) != meta.Frames.Length)
            {
                Thread.Sleep(100);
            }
            MagickImageCollection images = new MagickImageCollection(processedImages);
            Console.WriteLine("Starting gif gen");
            var quantSettings = new QuantizeSettings
            {
                Colors = 128
            };
            //images.Quantize(quantSettings);
            images.Optimize();
            images.Write(args[0]);

            File.Delete(args[0] + ".data");
            File.Delete(args[0] + ".meta");

            return 0;
        }
    }
}