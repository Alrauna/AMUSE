using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The report facts of one page of a page set, in plan order.
    /// </summary>
    internal sealed class AtlasPageSetPage
    {
        internal AtlasPageSetPage(
            int width,
            int height,
            GraphicsFormat format,
            int mipCount,
            int regionCount)
        {
            Width = width;
            Height = height;
            Format = format;
            MipCount = mipCount;
            RegionCount = regionCount;
        }

        internal int Width { get; }

        internal int Height { get; }

        internal GraphicsFormat Format { get; }

        internal int MipCount { get; }

        /// <summary>How many source regions the page holds.</summary>
        internal int RegionCount { get; }
    }
}
