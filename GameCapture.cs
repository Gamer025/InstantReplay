using System;
using UnityEngine;

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
