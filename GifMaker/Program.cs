namespace GifMaker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
        }
    }

    //public void GenerateGif()
    //{
    //    if (pauseCapture)
    //    {
    //        Logger_p.Log(LogLevel.Info, $"Tried to make new gif while already giffing");
    //        return;
    //    }
    //    Logger_p.Log(LogLevel.Info, $"Dimensions: w: {(int)game.rainWorld.options.ScreenSize.x}, h: {(int)game.rainWorld.options.ScreenSize.y}");

    //    try
    //    {
    //        pauseCapture = true;
    //        //Make sure any running captures complete
    //        while (capturingFrame)
    //        {
    //            Thread.Sleep(10);
    //        }
    //        int oldestFrame = ((curText + 1) % compressedFrames.Length + compressedFrames.Length) % compressedFrames.Length;
    //        using var images = new MagickImageCollection();
    //        var settings = new MagickReadSettings();
    //        settings.Width = (int)game.rainWorld.options.ScreenSize.x;
    //        settings.Height = (int)game.rainWorld.options.ScreenSize.y;
    //        settings.Format = MagickFormat.Rgb;
    //        var image = new MagickImage(decompressor.DecompressRGB(compressedFrames[oldestFrame]), settings);
    //        images.Add(image);
    //        images[images.Count - 1].AnimationDelay = 10;
    //        images[images.Count - 1].GifDisposeMethod = GifDisposeMethod.Previous;
    //        int size = compressedFrames.Length;
    //        for (int i = ((oldestFrame + 1) % size + size) % size; i != oldestFrame; i = ((i + 1) % size + size) % size)
    //        {
    //            image = new MagickImage(decompressor.DecompressRGB(compressedFrames[i]), settings);
    //            images.Add(image);
    //            images[images.Count - 1].AnimationDelay = 10;
    //            images[images.Count - 1].GifDisposeMethod = GifDisposeMethod.Previous;
    //        }

    //        Logger_p.Log(LogLevel.Info, $"Starting gif gen");
    //        var quantSettings = new QuantizeSettings
    //        {
    //            Colors = 256
    //        };
    //        images.Quantize(quantSettings);
    //        images.Optimize();
    //        images.Write(@"D:\tmp\testgif.gif");
    //        Logger_p.Log(LogLevel.Info, $"Gif gen done");
    //        pauseCapture = false;
    //    }
    //    catch (Exception ex)
    //    {
    //        pauseCapture = false;
    //        Logger_p.Log(LogLevel.Info, $"Gif gen error: {ex.Message} {ex}");
    //    }
    //}
}