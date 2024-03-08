using System.Text.Json.Serialization;
using System;

namespace Gifmaker
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

    [JsonSerializable(typeof(GifMetadata))]
    [JsonSerializable(typeof(GifMetadata.FrameData))]
    [JsonSerializable(typeof(System.Collections.BitArray))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }

    
}
