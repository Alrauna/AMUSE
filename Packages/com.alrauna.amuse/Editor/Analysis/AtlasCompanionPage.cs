using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// One companion page of a group. Its entry is the main page entry
    /// shifted right by <see cref="ScaleShift"/>: every placement
    /// coordinate and size, and the page size. So the remapped UV0 of
    /// the main page addresses the companion page too.
    /// </summary>
    internal sealed class AtlasCompanionPage
    {
        internal string Property { get; }

        /// <summary>The main page this page follows.</summary>
        internal int MainPageIndex { get; }

        internal int ScaleShift { get; }

        /// <summary>The sampler the page texture carries.</summary>
        internal AtlasSamplerFacts Sampler { get; }

        internal AtlasPagePlanEntry Entry { get; }

        internal AtlasCompanionPage(
            string property,
            int mainPageIndex,
            int scaleShift,
            AtlasSamplerFacts sampler,
            AtlasPagePlanEntry entry)
        {
            Property = property;
            MainPageIndex = mainPageIndex;
            ScaleShift = scaleShift;
            Sampler = sampler;
            Entry = entry;
        }
    }
}
