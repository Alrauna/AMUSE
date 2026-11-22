using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// The size, the format, and the mip count of one page source. A
    /// main source and a companion source have the same shape facts.
    /// </summary>
    internal readonly struct AtlasSourceShape
    {
        internal AtlasSourceShape(
            int width,
            int height,
            GraphicsFormat format,
            int mipCount)
        {
            Width = width;
            Height = height;
            Format = format;
            MipCount = mipCount;
        }

        internal int Width { get; }

        internal int Height { get; }

        internal GraphicsFormat Format { get; }

        internal int MipCount { get; }
    }
}
