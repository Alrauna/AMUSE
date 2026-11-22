using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// The byte counts of the atlas budget. Every function is a pure
    /// function of the sizes, the formats, and the mip counts of a
    /// plan. All arithmetic runs in checked 64-bit integers, so an
    /// impossible size throws instead of wrapping around.
    /// <para>
    /// Design section 10 fixes the unit and the size function. The
    /// byte numbers feed the three budget checks of the build pass.
    /// </para>
    /// </summary>
    internal static class AtlasBudget
    {
        /// <summary>
        /// The bytes of one texture shape with its full mip chain of
        /// the given level count. The value is a pure function of the
        /// four facts, so it is the same on every machine.
        /// </summary>
        internal static long TextureBytes(
            int width,
            int height,
            GraphicsFormat format,
            int mipCount)
        {
            checked
            {
                return GraphicsFormatUtility.ComputeMipChainSize(
                    width,
                    height,
                    format,
                    mipCount);
            }
        }

        /// <summary>
        /// The retained bytes of one group: the sum of the texture
        /// bytes of all its main and companion pages.
        /// </summary>
        internal static long GroupRetainedBytes(AtlasGroupPlan plan)
        {
            checked
            {
                long sum = 0;
                foreach (AtlasPagePlanEntry page in plan.Pages)
                {
                    sum += EntryBytes(page);
                }

                foreach (AtlasCompanionPage page in plan.CompanionPages)
                {
                    sum += EntryBytes(page.Entry);
                }

                return sum;
            }
        }

        private static long EntryBytes(AtlasPagePlanEntry page)
        {
            return TextureBytes(
                page.Width,
                page.Height,
                page.Format,
                page.MipCount);
        }

        /// <summary>
        /// The source bytes of one group: the texture bytes of every
        /// distinct main and companion source its admitted uses
        /// reference. A source that several uses reference counts once
        /// per group. Sources count as retained, because proof that a
        /// source has no other reference is not available.
        /// </summary>
        internal static long SourceBytes(
            AtlasGroupPlan plan,
            IReadOnlyList<CapturedAtlasUse> uses)
        {
            checked
            {
                var seenSources = new HashSet<int>();
                long sum = 0;
                foreach (int useIndex in plan.AdmittedUseIndices)
                {
                    CapturedAtlasUse use = uses[useIndex];
                    if (seenSources.Add(use.SourceTextureInstanceId))
                    {
                        sum += TextureBytes(
                            use.SourceWidth,
                            use.SourceHeight,
                            use.SourceFormat,
                            use.SourceMipCount);
                    }

                    foreach (var companion in use.Companions)
                    {
                        if (seenSources.Add(companion.SourceTextureInstanceId))
                        {
                            sum += TextureBytes(
                                companion.SourceWidth,
                                companion.SourceHeight,
                                companion.SourceFormat,
                                companion.SourceMipCount);
                        }
                    }
                }

                return sum;
            }
        }

        /// <summary>
        /// The predicted peak bytes of one group. The pass builds one
        /// page at a time and releases all temporaries of a page
        /// before the next page starts. So the peak of the group is
        /// the largest peak of its main and companion pages.
        /// </summary>
        internal static long PredictPeakBytes(
            AtlasGroupPlan plan,
            IReadOnlyList<CapturedAtlasUse> uses,
            int protectedMipLevelCount)
        {
            checked
            {
                long peak = 0;
                foreach (AtlasPagePlanEntry page in plan.Pages)
                {
                    peak = Math.Max(peak, PredictPagePeakBytes(
                        page,
                        uses,
                        protectedMipLevelCount));
                }

                foreach (AtlasCompanionPage page in plan.CompanionPages)
                {
                    peak = Math.Max(peak, PredictPagePeakBytes(
                        page.Entry,
                        uses,
                        protectedMipLevelCount));
                }

                return peak;
            }
        }

        /// <summary>
        /// The predicted peak bytes of one page. The terms follow the
        /// acquisition order of one page build: the RGBA32 compose
        /// buffer with its full mip chain, the page texture bytes, the
        /// raw protected levels and the level 0 Color32 array of every
        /// distinct source on the page, the largest import clone, and
        /// the check decode.
        /// </summary>
        internal static long PredictPagePeakBytes(
            AtlasPagePlanEntry page,
            IReadOnlyList<CapturedAtlasUse> uses,
            int protectedMipLevelCount)
        {
            checked
            {
                GraphicsFormat composeFormat =
                    GraphicsFormatUtility.IsSRGBFormat(page.Format)
                        ? GraphicsFormat.R8G8B8A8_SRGB
                        : GraphicsFormat.R8G8B8A8_UNorm;
                long total = TextureBytes(
                    page.Width,
                    page.Height,
                    composeFormat,
                    page.MipCount);
                total += TextureBytes(
                    page.Width,
                    page.Height,
                    page.Format,
                    page.MipCount);

                var seenSources = new HashSet<int>();
                long largestCloneBytes = 0;
                long largestDecodeBytes = 0;
                foreach (AtlasPlacement placement in page.Placements)
                {
                    if (!seenSources.Add(placement.SourceTextureInstanceId))
                    {
                        continue;
                    }

                    AtlasSourceShape source =
                        FindSource(uses, placement.SourceTextureInstanceId);

                    total += TextureBytes(
                        source.Width,
                        source.Height,
                        source.Format,
                        protectedMipLevelCount);
                    total += 4L * source.Width * source.Height;

                    long cloneBytes = TextureBytes(
                        source.Width,
                        source.Height,
                        source.Format,
                        source.MipCount);
                    if (cloneBytes > largestCloneBytes)
                    {
                        largestCloneBytes = cloneBytes;
                    }

                    long decodeBytes = 0;
                    for (int level = 0; level < protectedMipLevelCount; level++)
                    {
                        decodeBytes += 4L
                            * LevelSize(source.Width, level)
                            * LevelSize(source.Height, level);
                    }

                    if (decodeBytes > largestDecodeBytes)
                    {
                        largestDecodeBytes = decodeBytes;
                    }
                }

                total += largestCloneBytes;
                total += 4L * page.Width * page.Height;
                total += largestDecodeBytes;

                return total;
            }
        }

        /// <summary>
        /// The shape of one page source. A source can be the member of
        /// one use and a companion of another, and both report the same
        /// texture facts.
        /// </summary>
        private static AtlasSourceShape FindSource(
            IReadOnlyList<CapturedAtlasUse> uses,
            int sourceTextureInstanceId)
        {
            foreach (CapturedAtlasUse use in uses)
            {
                if (use.SourceTextureInstanceId == sourceTextureInstanceId)
                {
                    return new AtlasSourceShape(
                        use.SourceWidth,
                        use.SourceHeight,
                        use.SourceFormat,
                        use.SourceMipCount);
                }

                foreach (var companion in use.Companions)
                {
                    if (companion.SourceTextureInstanceId ==
                        sourceTextureInstanceId)
                    {
                        return new AtlasSourceShape(
                            companion.SourceWidth,
                            companion.SourceHeight,
                            companion.SourceFormat,
                            companion.SourceMipCount);
                    }
                }
            }

            throw new InvalidOperationException(
                "No captured use references source texture instance "
                + sourceTextureInstanceId
                + ", so the page plan holds a source the capture"
                + " never saw.");
        }

        private static int LevelSize(int size, int level)
        {
            return Math.Max(1, size >> level);
        }
    }
}
