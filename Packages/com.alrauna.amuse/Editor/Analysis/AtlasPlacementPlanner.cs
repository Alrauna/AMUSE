using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// Groups captured atlas uses by their group key and places the
    /// source regions of each group on shared pages. The planner is a
    /// pure function of its input. It reads numbers and strings only
    /// and holds no Unity object. The result lists the groups in the
    /// capture order of the first use of each group. Every use index
    /// in the result names a use by its position in capture order, so
    /// the plan of one input never depends on the order of the input
    /// list.
    /// <para>
    /// Placement follows shelf packing. The planner sorts the
    /// placements of a group by longest side, then area, then capture
    /// order. Longest side and area descend. Capture order ascends.
    /// A shelf fills left to right up to the maximum page size. A new
    /// shelf starts below. A placement that does not fit the current
    /// page starts a new page. Pages are tight.
    /// </para>
    /// </summary>
    internal static class AtlasPlacementPlanner
    {
        /// <summary>
        /// The largest page size the planner accepts. The VRChat SDK
        /// limits textures to it, and the SDK cannot check a generated
        /// page, so the planner enforces the limit itself.
        /// </summary>
        internal const int MaximumSupportedPageSize = 8192;

        /// <summary>
        /// The largest protected level count the planner accepts. The
        /// largest supported page has 8192 texels, so its chain has 14
        /// levels. A larger count cannot name a real level.
        /// </summary>
        internal const int MaximumSupportedProtectedMipLevelCount = 14;

        /// <summary>
        /// The anisotropy ratio the border assumes. Unity names 16 as
        /// the highest anisotropic level.
        /// </summary>
        internal const int BorderedAnisoRatio = 16;

        /// <summary>The block edge of the compressed page formats.</summary>
        internal const int CompressedBlockEdge = 4;

        internal static IReadOnlyList<AtlasGroupPlan> Plan(
            IReadOnlyList<CapturedAtlasUse> uses,
            int maximumPageSize,
            int protectedMipLevelCount)
        {
            if (uses == null)
            {
                throw new ArgumentNullException(nameof(uses));
            }

            if (maximumPageSize < 1 || maximumPageSize > MaximumSupportedPageSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumPageSize),
                    "The maximum page size must be between 1 and 8192.");
            }

            if (protectedMipLevelCount < 1 ||
                protectedMipLevelCount > MaximumSupportedProtectedMipLevelCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(protectedMipLevelCount),
                    "The protected level count must be between 1 and 14.");
            }

            var orderedUses = OrderByCaptureOrder(uses);
            var plans = new List<AtlasGroupPlan>();
            var groupsByKey = new Dictionary<AtlasGroupKey, GroupBuilder>();
            var groups = new List<GroupBuilder>();

            for (var rank = 0; rank < orderedUses.Count; rank++)
            {
                var use = orderedUses[rank];
                var key = AtlasGroupKey.FromUse(use);
                if (!groupsByKey.TryGetValue(key, out var group))
                {
                    group = new GroupBuilder(key);
                    groupsByKey.Add(key, group);
                    groups.Add(group);
                }

                group.AddUse(use, rank);
            }

            foreach (var group in groups)
            {
                plans.Add(group.BuildPlan(
                    maximumPageSize,
                    protectedMipLevelCount));
            }

            return plans;
        }

        /// <summary>
        /// Orders the uses by capture order. An equal capture order is
        /// a capture defect. The input position breaks the tie, so the
        /// order stays deterministic.
        /// </summary>
        private static List<CapturedAtlasUse> OrderByCaptureOrder(
            IReadOnlyList<CapturedAtlasUse> uses)
        {
            var copies = new List<CapturedAtlasUse>(uses);
            var order = new List<int>(uses.Count);
            for (var index = 0; index < copies.Count; index++)
            {
                order.Add(index);
            }

            order.Sort((left, right) =>
            {
                var byCapture = copies[left].CaptureOrder
                    .CompareTo(copies[right].CaptureOrder);
                return byCapture != 0 ? byCapture : left.CompareTo(right);
            });

            var ordered = new List<CapturedAtlasUse>(uses.Count);
            foreach (var index in order)
            {
                ordered.Add(copies[index]);
            }

            return ordered;
        }

        /// <summary>
        /// Rounds a value down to a multiple of the grid. The floor
        /// also holds for negative values, because a Repeat border can
        /// push the region across the source edge.
        /// </summary>
        private static int AlignDown(int value, int grid)
        {
            var quotient = Math.DivRem(value, grid, out var remainder);
            if (remainder < 0)
            {
                quotient--;
            }

            return quotient * grid;
        }

        /// <summary>Rounds a value up to a multiple of the grid.</summary>
        private static int AlignUp(int value, int grid)
        {
            var quotient = Math.DivRem(value, grid, out var remainder);
            if (remainder > 0)
            {
                quotient++;
            }

            return quotient * grid;
        }

        /// <summary>
        /// The block edge of a format. Compressed formats place blocks
        /// of four by four texels, so their placements sit on a block
        /// grid. Uncompressed formats use one texel.
        /// </summary>
        private static int BlockEdgeOf(GraphicsFormat format)
        {
            return GraphicsFormatUtility.IsCompressedFormat(format)
                ? CompressedBlockEdge
                : 1;
        }

        private static bool IsBc7(GraphicsFormat format)
        {
            return format == GraphicsFormat.RGBA_BC7_SRGB ||
                   format == GraphicsFormat.RGBA_BC7_UNorm;
        }

        /// <summary>
        /// The largest power of two that is at most the value. The
        /// value must be positive.
        /// </summary>
        private static int FloorLog2(int value)
        {
            var result = 0;
            while (value > 1)
            {
                value >>= 1;
                result++;
            }

            return result;
        }

        private sealed class GroupBuilder
        {
            internal AtlasGroupKey Key { get; }

            private readonly Dictionary<
                (int InstanceId, AtlasSamplerFacts Sampler),
                PlacementBuilder> placementsByKey =
                new Dictionary<
                    (int InstanceId, AtlasSamplerFacts Sampler),
                    PlacementBuilder>();

            private readonly List<PlacementBuilder> placements =
                new List<PlacementBuilder>();

            internal GroupBuilder(AtlasGroupKey key)
            {
                Key = key;
            }

            /// <summary>
            /// Adds one use to the placement of its source. The uses of
            /// one source texture with one sampler share a placement.
            /// </summary>
            internal void AddUse(CapturedAtlasUse use, int rank)
            {
                if (!placementsByKey.TryGetValue(
                        (use.SourceTextureInstanceId, use.Sampler),
                        out var placement))
                {
                    placement = new PlacementBuilder(
                        use.SourceTextureInstanceId,
                        use.Sampler,
                        use.SourceWidth,
                        use.SourceHeight,
                        rank);
                    placementsByKey.Add(
                        (use.SourceTextureInstanceId, use.Sampler),
                        placement);
                    placements.Add(placement);
                }
                placement.UnionWith(use.UvRect, rank);
            }

            internal AtlasGroupPlan BuildPlan(
                int maximumPageSize,
                int protectedMipLevelCount)
            {
                var blockEdge = BlockEdgeOf(Key.SourceFormat);
                var levelScale = 1 << (protectedMipLevelCount - 1);
                var anisoRatio = Key.Sampler.AnisoLevel == 0
                    ? 1
                    : BorderedAnisoRatio;
                var border = Math.Max(anisoRatio, blockEdge) * levelScale;
                var grid = blockEdge * levelScale;

                var placed = new List<PlacementBuilder>();
                var admitted = new List<int>();
                var refused = new List<AtlasRefusedUse>();
                foreach (var placement in placements)
                {
                    var refusal = placement.Measure(
                        Key.SourceFormat,
                        border,
                        grid,
                        maximumPageSize);
                    if (refusal != AtlasPlacementRefusal.None)
                    {
                        foreach (var rank in placement.UseRanks)
                        {
                            refused.Add(new AtlasRefusedUse(rank, refusal));
                        }

                        continue;
                    }

                    placed.Add(placement);
                    admitted.AddRange(placement.UseRanks);
                }

                placed.Sort(CompareForPacking);
                var pages = Pack(placed, maximumPageSize, grid);

                var pageEntries = new List<AtlasPagePlanEntry>();
                foreach (var page in pages)
                {
                    pageEntries.Add(page.ToEntry(Key.SourceFormat, grid));
                }

                // The lists grow placement by placement, so uses of
                // two sources can interleave. Capture order is the
                // contract of both lists.
                admitted.Sort();
                refused.Sort((left, right) =>
                    left.UseIndex.CompareTo(right.UseIndex));

                return new AtlasGroupPlan(Key, pageEntries, admitted, refused);
            }

            /// <summary>
            /// Orders placements for packing. Longest side descends,
            /// then area descends, then the capture order of the first
            /// use ascends. The order is total, so the packing is
            /// deterministic.
            /// </summary>
            private static int CompareForPacking(
                PlacementBuilder left,
                PlacementBuilder right)
            {
                var byLongestSide = Math.Max(right.Width, right.Height)
                    .CompareTo(Math.Max(left.Width, left.Height));
                if (byLongestSide != 0)
                {
                    return byLongestSide;
                }

                var byArea = (right.Width * right.Height)
                    .CompareTo(left.Width * left.Height);
                if (byArea != 0)
                {
                    return byArea;
                }

                return left.FirstRank.CompareTo(right.FirstRank);
            }

            /// <summary>
            /// Places the placements on pages. A placement that does
            /// not fit the current page starts a new page. Every input
            /// placement fits an empty page, because the measurement
            /// refused every larger one.
            /// </summary>
            private static List<PageBuilder> Pack(
                List<PlacementBuilder> placements,
                int maximumPageSize,
                int grid)
            {
                var pages = new List<PageBuilder>();
                PageBuilder page = null;
                var shelfX = 0;
                var shelfY = 0;
                var shelfHeight = 0;

                void CloseShelf(PageBuilder currentPage)
                {
                    if (shelfHeight == 0)
                    {
                        return;
                    }

                    currentPage.Height = AlignUp(shelfY + shelfHeight, grid);
                    shelfY = currentPage.Height;
                    shelfX = 0;
                    shelfHeight = 0;
                }

                foreach (var placement in placements)
                {
                    if (page == null)
                    {
                        page = new PageBuilder();
                        pages.Add(page);
                    }

                    if (shelfX + placement.Width > maximumPageSize)
                    {
                        CloseShelf(page);
                    }

                    if (shelfY + placement.Height > maximumPageSize)
                    {
                        CloseShelf(page);
                        page = new PageBuilder();
                        pages.Add(page);
                        shelfX = 0;
                        shelfY = 0;
                        shelfHeight = 0;
                    }

                    placement.PageIndex = pages.Count - 1;
                    placement.PageX = shelfX;
                    placement.PageY = shelfY;
                    page.Placements.Add(placement);
                    shelfX += placement.Width;
                    page.WidestShelf = Math.Max(page.WidestShelf, shelfX);
                    shelfHeight = Math.Max(shelfHeight, placement.Height);
                }

                CloseShelf(page);
                return pages;
            }
        }

        private sealed class PlacementBuilder
        {
            internal int SourceTextureInstanceId { get; }

            internal AtlasSamplerFacts Sampler { get; }

            internal int FirstRank { get; }

            private int SourceWidth { get; }

            private int SourceHeight { get; }

            internal List<int> UseRanks { get; } = new List<int>();

            internal int Width { get; private set; }
            internal int Height { get; private set; }
            internal int SourceX { get; private set; }
            internal int SourceY { get; private set; }

            internal int PageIndex { get; set; }
            internal int PageX { get; set; }
            internal int PageY { get; set; }

            private double minU = double.MaxValue;
            private double minV = double.MaxValue;
            private double maxU = double.MinValue;
            private double maxV = double.MinValue;

            internal PlacementBuilder(
                int sourceTextureInstanceId,
                AtlasSamplerFacts sampler,
                int sourceWidth,
                int sourceHeight,
                int firstRank)
            {
                SourceTextureInstanceId = sourceTextureInstanceId;
                Sampler = sampler;
                SourceWidth = sourceWidth;
                SourceHeight = sourceHeight;
                FirstRank = firstRank;
            }

            /// <summary>Unions the UV rectangle of one use into the region.</summary>
            internal void UnionWith(AtlasUvRect rect, int rank)
            {
                minU = Math.Min(minU, rect.MinU);
                minV = Math.Min(minV, rect.MinV);
                maxU = Math.Max(maxU, rect.MaxU);
                maxV = Math.Max(maxV, rect.MaxV);
                UseRanks.Add(rank);
            }

            /// <summary>
            /// Measures the bordered region of the union and returns
            /// the refusal of the placement, or none. The region holds
            /// the floor of the union minimum, the ceiling of the union
            /// maximum, the border on every side, and the outward
            /// alignment to the grid. The region may cross the source
            /// edge, because a Repeat border reads across the edge.
            /// </summary>
            internal AtlasPlacementRefusal Measure(
                GraphicsFormat format,
                int border,
                int grid,
                int maximumPageSize)
            {
                var x0 = AlignDown(
                    (int)Math.Floor(minU * SourceWidth) - border,
                    grid);
                var x1 = AlignUp(
                    (int)Math.Ceiling(maxU * SourceWidth) + border,
                    grid);
                var y0 = AlignDown(
                    (int)Math.Floor(minV * SourceHeight) - border,
                    grid);
                var y1 = AlignUp(
                    (int)Math.Ceiling(maxV * SourceHeight) + border,
                    grid);

                if (IsBc7(format) && CrossesTheEdgeOnAClampAxis(x0, x1, y0, y1))
                {
                    return AtlasPlacementRefusal.ClampEdgeOnBc7;
                }

                SourceX = x0;
                SourceY = y0;
                Width = x1 - x0;
                Height = y1 - y0;

                if (Width > maximumPageSize || Height > maximumPageSize)
                {
                    return AtlasPlacementRefusal.PlacementTooLarge;
                }

                return AtlasPlacementRefusal.None;
            }

            private bool CrossesTheEdgeOnAClampAxis(
                int x0,
                int x1,
                int y0,
                int y1)
            {
                if (Sampler.WrapU == UnityEngine.TextureWrapMode.Clamp &&
                    (x0 < 0 || x1 > SourceWidth))
                {
                    return true;
                }

                return Sampler.WrapV == UnityEngine.TextureWrapMode.Clamp &&
                       (y0 < 0 || y1 > SourceHeight);
            }

            internal AtlasPlacement ToPlacement()
            {
                return new AtlasPlacement(
                    SourceTextureInstanceId,
                    Sampler,
                    SourceX,
                    SourceY,
                    Width,
                    Height,
                    PageIndex,
                    PageX,
                    PageY,
                    UseRanks);
            }
        }

        private sealed class PageBuilder
        {
            internal List<PlacementBuilder> Placements { get; } =
                new List<PlacementBuilder>();

            /// <summary>The widest shelf of the page, in page texels.</summary>
            internal int WidestShelf { get; set; }

            /// <summary>
            /// The sum of the shelf heights, each rounded up to the
            /// grid.
            /// </summary>
            internal int Height { get; set; }

            internal AtlasPagePlanEntry ToEntry(
                GraphicsFormat format,
                int grid)
            {
                var width = AlignUp(WidestShelf, grid);
                var mipCount = FloorLog2(Math.Max(width, Height)) + 1;
                var entries = new List<AtlasPlacement>();
                foreach (var placement in Placements)
                {
                    entries.Add(placement.ToPlacement());
                }

                return new AtlasPagePlanEntry(
                    width,
                    Height,
                    format,
                    mipCount,
                    entries);
            }
        }
    }
}
