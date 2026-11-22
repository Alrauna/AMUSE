using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The report facts of one page of a page set, in plan order. The
    /// main pages come first, then the companion pages.
    /// </summary>
    internal sealed class AtlasPageSetPage
    {
        internal AtlasPageSetPage(
            int width,
            int height,
            GraphicsFormat format,
            int mipCount,
            int regionCount,
            string routeProperty = null,
            int mainPageIndex = 0)
        {
            Width = width;
            Height = height;
            Format = format;
            MipCount = mipCount;
            RegionCount = regionCount;
            RouteProperty = routeProperty;
            MainPageIndex = mainPageIndex;
        }

        internal int Width { get; }

        internal int Height { get; }

        internal GraphicsFormat Format { get; }

        internal int MipCount { get; }

        /// <summary>How many source regions the page holds.</summary>
        internal int RegionCount { get; }

        /// <summary>
        /// The companion route property of a companion page, or null
        /// for a main page.
        /// </summary>
        internal string RouteProperty { get; }

        /// <summary>The main page a companion page follows.</summary>
        internal int MainPageIndex { get; }
    }
}
