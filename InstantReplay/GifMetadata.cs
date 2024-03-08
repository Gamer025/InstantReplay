using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace InstantReplay
{
    internal class GifMetadata
    {
        public FrameData[] Frames { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int FPS { get; set; }
        public float Scale { get; set; }

        internal class FrameData
        {
            public int FrameCompressedSize { get; set; }
            public int FrameOrigSize { get; set; }
        }
    }
}
