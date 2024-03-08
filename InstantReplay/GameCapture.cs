using System;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace InstantReplay
{
    internal class GameCapture
    {
        //Circular array
        private readonly CompressedFrame[] compressedFrames;
        //Where to write the next frame to, will always advanced 1
        private int writeIndex;
        public int readIndex { get; set; }
        //Amount of frames that have already been captured
        private int capturedFrames;
        public Vector2Int frameSize;
        public int FPS;
        public int RawFrameSize
        {
            //Image size * 3 for RGB
            get { return frameSize.x + frameSize.y * 3; }
        }
        public int FrameCount
        {
            get { return compressedFrames.Length; }
        }

        public long FrameBytes
        {
            get
            {
                long count = 0;
                for (int i = 0; i < compressedFrames.Length; i++)
                {
                    if (compressedFrames[i].compressedData != null)
                    {
                        count += compressedFrames[i].compressedData.Length;
                    }
                }
                return count;
            }
        }
        private readonly RLE decompressor = new RLE(0);

        public GameCapture(int size, int x, int y, int fPS)
        {
            compressedFrames = new CompressedFrame[size];
            writeIndex = 0;
            frameSize.x = x;
            frameSize.y = y;
            FPS = fPS;
        }

        public void AddFrame(CompressedFrame frame)
        {
            compressedFrames[writeIndex] = frame;
            writeIndex = ((writeIndex + 1) % compressedFrames.Length + compressedFrames.Length) % compressedFrames.Length;
            capturedFrames = Math.Min(capturedFrames + 1, compressedFrames.Length);
        }

        public byte[] GetUncompressedFrame(int id, bool downscale)
        {
            if (id > capturedFrames - 1 || id < 0)
            {
                InstantReplay.ME.Logger_p.LogWarning($"Requested frame ID invalid! request: {id} frameCount: {compressedFrames.Length}");
                return new byte[0];
            }
            if (downscale)
            {
                byte[] returnArray = new byte[compressedFrames[id].origSize / 2];
                decompressor.DecompressRGBandDownscale(compressedFrames[id].compressedData, returnArray, frameSize.x);
                return returnArray;
            }
            else
            {
                byte[] returnArray = new byte[compressedFrames[id].origSize];
                decompressor.DecompressRGB(compressedFrames[id].compressedData, returnArray);
                return returnArray;
            }
        }
        public byte[] GetCurrentUncompressedFrame(int move = 1, bool downscale = false)
        {
            readIndex = ((readIndex + move) % capturedFrames + capturedFrames) % capturedFrames;
            byte[] returnArray = GetUncompressedFrame(readIndex, downscale);
            return returnArray;
        }

        /// <summary>
        /// Move the read "head" either forwards or backwards
        /// </summary>
        /// <param name="count"></param>
        public void MoveRead(int count = 1)
        {
            readIndex = ((readIndex + count) % capturedFrames + capturedFrames) % capturedFrames;
        }

        public void RewindToStart()
        {
            readIndex = ((writeIndex + 1) % capturedFrames + capturedFrames) % capturedFrames;
        }

        public Process ExportGifData(string path)
        {
            int prevRead = readIndex;
            RewindToStart();
            bool halfFrames = FPS >= 30;
            int framesToWrite = Math.Min(InstantReplay.gifMaxLength.Value * FPS, capturedFrames);

            //If replay viewer is active use current viewed frame as start and output till end of replay
            if (InstantReplay.ME.ReplayState == Overlays.ReplayOverlayState.Running)
            {
                readIndex = prevRead;
                if (readIndex > writeIndex)
                    framesToWrite = compressedFrames.Length - readIndex + writeIndex;
                else if (readIndex != writeIndex)
                    framesToWrite = writeIndex - readIndex;
                else
                    framesToWrite = capturedFrames;
            }
            else //Otherwise rewind back X seconds
            {
                MoveRead(-framesToWrite);
            }
            
            GifMetadata meta = new GifMetadata()
            {
                Width = frameSize.x,
                Height = frameSize.y,
                FPS = halfFrames ? FPS / 2 : FPS,
                Frames = new GifMetadata.FrameData[halfFrames ? (int)Math.Ceiling(framesToWrite / 2f) : framesToWrite],
                Scale = InstantReplay.gifScale.Value
            };

            using (var fs = new FileStream(path + ".data", FileMode.Create, FileAccess.Write))
            {

                int imageCount = 0;


                for (int i = 0; i < framesToWrite; i++)
                {
                    if (halfFrames && i % 2 != 0)
                        continue;
                    int index = ((readIndex + i) % capturedFrames + capturedFrames) % capturedFrames;
                    meta.Frames[imageCount] = new GifMetadata.FrameData()
                    {
                        FrameCompressedSize = compressedFrames[index].compressedData.Length,
                        FrameOrigSize = compressedFrames[index].origSize
                    };
                    fs.Write(compressedFrames[index].compressedData, 0, compressedFrames[index].compressedData.Length);
                    imageCount++;
                }
            }
            File.WriteAllText(path + ".meta", JsonConvert.SerializeObject(meta));
            readIndex = prevRead;

            string GifMakerPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ".." + Path.DirectorySeparatorChar + "GifMaker" + Path.DirectorySeparatorChar + "GifMakerCore.exe");
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.EnvironmentVariables.Clear();
            psi.UseShellExecute = false;
            psi.FileName = GifMakerPath;
            psi.Arguments = $"\"{path}\"";
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.CreateNoWindow = true;
            InstantReplay.ME.Logger_p.LogInfo($"Staring GifMaker at {GifMakerPath} with args: {psi.Arguments}");
            Process proc = Process.Start(psi);
            proc.PriorityClass = ProcessPriorityClass.BelowNormal;
            return proc;
        }
    }
    public struct CompressedFrame
    {
        public byte[] compressedData;
        public int origSize;

        public CompressedFrame(byte[] compressedData, int origSize)
        {
            this.compressedData = compressedData;
            this.origSize = origSize;
        }
    }
}
