using System;
using System.Threading;
using static InstantReplay.InstantReplay;

namespace InstantReplay
{

    internal class FrameCompressor : IDisposable
    {
        private RLE compressor;
        public ManualResetEvent MRE { get; set; }
        public GameCapture capture { get; set; }
        private Thread Worker;
        private bool shutdown = false;
        public byte[] frameToCompress;
        Guid GUID = Guid.NewGuid();

        public FrameCompressor(int frames, int x, int y, int FPS)
        {
            MRE = new ManualResetEvent(false);
            capture = new GameCapture(frames, x, y, FPS);
            compressor = new RLE();
            Worker = new Thread(DoWork);
            Worker.Start();
        }

        private void DoWork()
        {
            ME.Logger_p.LogInfo($"FrameCompressor worker {GUID} starting");
            while (true)
            {
                try
                {
                    //If the thread did work and the loop starts again but we are shutdown exit
                    if (shutdown)
                    {
                        ME.Logger_p.LogInfo($"FrameCompressor worker {GUID} exiting");
                        return;
                    }
                    MRE.WaitOne();
                    capture.AddFrame(new CompressedFrame(compressor.CompressRGB(frameToCompress), frameToCompress.Length));
                    MRE.Reset();
                }
                catch (Exception ex)
                {
                    ME.Logger_p.LogError($"Compressor thread crashed. {ex.Message} \n {ex.StackTrace}");
                }
            }
        }
        public void Dispose()
        {
            shutdown = true;
            MRE.Set();
        }
    }
}
