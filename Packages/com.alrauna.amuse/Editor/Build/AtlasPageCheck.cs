using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The output check of one built page. The check decodes every
    /// protected level of the page and of each placement source with
    /// <c>GetPixels32</c> and compares every texel of each placement
    /// and its border with the source texel that the wrap mode maps it
    /// to. Alpha is included, because Poiyomi reflection can use alpha
    /// for color in an opaque output. A difference throws, because the
    /// raw block copy cannot produce one.
    /// <para>
    /// The check decodes one page level and one placement level at a
    /// time and drops each decode when its comparison ends. A ledger
    /// that rides along receives each decode while it lives, so the
    /// peak of the check stays inside the two decode terms of the page
    /// prediction.
    /// </para>
    /// <para>
    /// The check needs a readable page, so it runs before the pass
    /// closes the page with <c>Apply(false, true)</c>.
    /// </para>
    /// </summary>
    internal static class AtlasPageCheck
    {
        internal static void Verify(
            Texture2D page,
            AtlasPagePlanEntry plan,
            IReadOnlyDictionary<int, AtlasSourceImage> sources,
            int protectedMipLevelCount,
            AtlasByteLedger ledger = null)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            for (var level = 0; level < protectedMipLevelCount; level++)
            {
                VerifyLevel(
                    page, plan, sources, level, ledger);
            }
        }

        private static void VerifyLevel(
            Texture2D page,
            AtlasPagePlanEntry plan,
            IReadOnlyDictionary<int, AtlasSourceImage> sources,
            int level,
            AtlasByteLedger ledger)
        {
            var levelWidth = Math.Max(1, plan.Width >> level);
            var levelHeight = Math.Max(1, plan.Height >> level);
            var decodeBytes = 4L * levelWidth * levelHeight;
            ledger?.Add(decodeBytes);
            Color32[] pageColors = page.GetPixels32(level);
            try
            {
                foreach (AtlasPlacement placement in plan.Placements)
                {
                    VerifyPlacement(
                        pageColors,
                        levelWidth,
                        SourceOf(sources, placement),
                        placement,
                        level,
                        ledger);
                }
            }
            finally
            {
                ledger?.Release(decodeBytes);
            }
        }

        private static void VerifyPlacement(
            Color32[] pageColors,
            int levelWidth,
            AtlasSourceImage source,
            AtlasPlacement placement,
            int level,
            AtlasByteLedger ledger)
        {
            var sourceWidth = source.LevelWidth(level);
            var sourceHeight = source.LevelHeight(level);
            var decodeBytes = 4L * sourceWidth * sourceHeight;
            ledger?.Add(decodeBytes);
            Color32[] sourceColors = DecodeSourceLevel(source, level);
            try
            {
                var sourceX = placement.SourceX >> level;
                var sourceY = placement.SourceY >> level;
                var pageX = placement.PageX >> level;
                var pageY = placement.PageY >> level;
                var regionWidth = placement.Width >> level;
                var regionHeight = placement.Height >> level;
                for (var y = 0; y < regionHeight; y++)
                {
                    var insideY = AtlasPageBuilder.MapCoordinate(
                        sourceY + y,
                        sourceHeight,
                        placement.Sampler.WrapV);
                    for (var x = 0; x < regionWidth; x++)
                    {
                        var insideX = AtlasPageBuilder.MapCoordinate(
                            sourceX + x,
                            sourceWidth,
                            placement.Sampler.WrapU);
                        Color32 expected =
                            sourceColors[insideY * sourceWidth + insideX];
                        Color32 actual = pageColors[
                            (pageY + y) * levelWidth + pageX + x];
                        if (expected.r != actual.r ||
                            expected.g != actual.g ||
                            expected.b != actual.b ||
                            expected.a != actual.a)
                        {
                            throw new InvalidOperationException(
                                "The atlas page failed its output check " +
                                "at protected level " + level + ". The " +
                                "page texel " + (pageX + x) + "," +
                                (pageY + y) + " holds " + actual.r + "," +
                                actual.g + "," + actual.b + "," + actual.a +
                                " while the source texel " + insideX + "," +
                                insideY + " of texture instance " +
                                placement.SourceTextureInstanceId +
                                " decodes to " + expected.r + "," +
                                expected.g + "," + expected.b + "," +
                                expected.a + ".");
                        }
                    }
                }
            }
            finally
            {
                ledger?.Release(decodeBytes);
            }
        }

        /// <summary>
        /// Decodes one protected level of a source through the Unity
        /// decoder. The page and the source decode through the same
        /// decoder, so byte equal blocks compare equal.
        /// </summary>
        private static Color32[] DecodeSourceLevel(
            AtlasSourceImage source,
            int level)
        {
            if (source.RawLevels.Count <= level)
            {
                throw new InvalidOperationException(
                    "The page check reads level " + level + " of source " +
                    "texture image with " + source.RawLevels.Count +
                    " levels.");
            }

            var texture = new Texture2D(
                source.LevelWidth(level),
                source.LevelHeight(level),
                GraphicsFormatUtility.GetTextureFormat(source.Format),
                1,
                !GraphicsFormatUtility.IsSRGBFormat(source.Format));
            try
            {
                texture.SetPixelData(source.RawLevels[level], 0);
                texture.Apply(false, false);
                return texture.GetPixels32(0);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static AtlasSourceImage SourceOf(
            IReadOnlyDictionary<int, AtlasSourceImage> sources,
            AtlasPlacement placement)
        {
            if (!sources.TryGetValue(
                    placement.SourceTextureInstanceId, out var source))
            {
                throw new InvalidOperationException(
                    "The page plan names source texture instance " +
                    placement.SourceTextureInstanceId + ", but the " +
                    "acquired sources hold no image for it.");
            }

            return source;
        }
    }
}
