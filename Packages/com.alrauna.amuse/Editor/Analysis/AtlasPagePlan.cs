using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// Why the planner refused to place a use. The values stay in
    /// Analysis. The build pass maps them to its own use refusals.
    /// </summary>
    internal enum AtlasPlacementRefusal
    {
        None = 0,

        /// <summary>
        /// The bordered region is wider or taller than the maximum page
        /// size, so no empty page of the group can hold it.
        /// </summary>
        PlacementTooLarge = 1,

        /// <summary>
        /// A Clamp axis of the region crosses the source edge on a BC7
        /// source. BC7 has no exact Clamp edge border, so the uses of
        /// the region refuse.
        /// </summary>
        ClampEdgeOnBc7 = 2
    }

    /// <summary>
    /// One placed source region with its border. The source rectangle
    /// holds source texels. It may cross the source edge, because a
    /// Repeat border reads across the edge. The page rectangle holds
    /// page texels of the page named by the page index. The use indices
    /// name the served uses by their position in capture order.
    /// </summary>
    internal sealed class AtlasPlacement
    {
        internal int SourceTextureInstanceId { get; }

        /// <summary>The sampler facts the region was placed under.</summary>
        internal AtlasSamplerFacts Sampler { get; }

        internal int SourceX { get; }
        internal int SourceY { get; }
        internal int Width { get; }
        internal int Height { get; }

        /// <summary>The zero-based index of the page in the group plan.</summary>
        internal int PageIndex { get; }

        /// <summary>The page origin of the region, aligned to the grid.</summary>
        internal int PageX { get; }
        internal int PageY { get; }

        /// <summary>The served uses, in capture order.</summary>
        internal IReadOnlyList<int> UseIndices { get; }

        internal AtlasPlacement(
            int sourceTextureInstanceId,
            AtlasSamplerFacts sampler,
            int sourceX,
            int sourceY,
            int width,
            int height,
            int pageIndex,
            int pageX,
            int pageY,
            IReadOnlyList<int> useIndices)
        {
            SourceTextureInstanceId = sourceTextureInstanceId;
            Sampler = sampler;
            SourceX = sourceX;
            SourceY = sourceY;
            Width = width;
            Height = height;
            PageIndex = pageIndex;
            PageX = pageX;
            PageY = pageY;
            UseIndices = new ReadOnlyCollection<int>(
                new List<int>(useIndices));
        }
    }

    /// <summary>
    /// One planned page. Width and height are the tight packing bounds
    /// rounded up to the alignment grid. The format is the source
    /// format of the group. The mip count is the full chain.
    /// </summary>
    internal sealed class AtlasPagePlanEntry
    {
        internal int Width { get; }
        internal int Height { get; }
        internal GraphicsFormat Format { get; }
        internal int MipCount { get; }
        internal IReadOnlyList<AtlasPlacement> Placements { get; }

        internal AtlasPagePlanEntry(
            int width,
            int height,
            GraphicsFormat format,
            int mipCount,
            IReadOnlyList<AtlasPlacement> placements)
        {
            Width = width;
            Height = height;
            Format = format;
            MipCount = mipCount;
            Placements = new ReadOnlyCollection<AtlasPlacement>(
                new List<AtlasPlacement>(placements));
        }
    }

    /// <summary>One refused use with the reason of its placement.</summary>
    internal sealed class AtlasRefusedUse
    {
        /// <summary>The refused use, by its position in capture order.</summary>
        internal int UseIndex { get; }

        internal AtlasPlacementRefusal Refusal { get; }

        /// <summary>
        /// The bordered region width in texels when the placement is
        /// too large. Zero for every other refusal.
        /// </summary>
        internal int RegionWidth { get; }

        /// <summary>
        /// The bordered region height in texels when the placement is
        /// too large. Zero for every other refusal.
        /// </summary>
        internal int RegionHeight { get; }

        internal AtlasRefusedUse(
            int useIndex,
            AtlasPlacementRefusal refusal,
            int regionWidth,
            int regionHeight)
        {
            UseIndex = useIndex;
            Refusal = refusal;
            RegionWidth = regionWidth;
            RegionHeight = regionHeight;
        }
    }

    /// <summary>
    /// The plan of one atlas group: its key, its pages, its admitted
    /// uses, and its refused uses. The use indices name uses by their
    /// position in capture order.
    /// </summary>
    internal sealed class AtlasGroupPlan
    {
        internal AtlasGroupKey Key { get; }

        internal IReadOnlyList<AtlasPagePlanEntry> Pages { get; }

        /// <summary>The admitted uses, in capture order.</summary>
        internal IReadOnlyList<int> AdmittedUseIndices { get; }

        /// <summary>The refused uses, in capture order.</summary>
        internal IReadOnlyList<AtlasRefusedUse> RefusedUses { get; }

        internal AtlasGroupPlan(
            AtlasGroupKey key,
            IReadOnlyList<AtlasPagePlanEntry> pages,
            IReadOnlyList<int> admittedUseIndices,
            IReadOnlyList<AtlasRefusedUse> refusedUses)
        {
            Key = key;
            Pages = new ReadOnlyCollection<AtlasPagePlanEntry>(
                new List<AtlasPagePlanEntry>(pages));
            AdmittedUseIndices = new ReadOnlyCollection<int>(
                new List<int>(admittedUseIndices));
            RefusedUses = new ReadOnlyCollection<AtlasRefusedUse>(
                new List<AtlasRefusedUse>(refusedUses));
        }
    }
}
