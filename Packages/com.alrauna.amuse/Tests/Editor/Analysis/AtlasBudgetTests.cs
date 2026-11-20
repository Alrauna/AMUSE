using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class AtlasBudgetTests
    {
        private static readonly AtlasSamplerFacts Sampler =
            new AtlasSamplerFacts(
                FilterMode.Bilinear,
                UnityEngine.TextureWrapMode.Repeat,
                UnityEngine.TextureWrapMode.Repeat,
                0,
                0f);

        private static CapturedAtlasUse Use(
            int instanceId,
            GraphicsFormat format,
            int width,
            int height,
            int mipCount,
            int captureOrder)
        {
            return new CapturedAtlasUse(
                0,
                0,
                0,
                "Poiyomi 9.3 release main",
                instanceId,
                format,
                width,
                height,
                mipCount,
                Sampler,
                new AtlasUvRect(0.0, 0.0, 1.0, 1.0),
                "material",
                Array.Empty<string>(),
                captureOrder);
        }

        private static AtlasPlacement PlacementOf(
            int instanceId,
            int pageIndex,
            int useIndex)
        {
            return new AtlasPlacement(
                instanceId,
                Sampler,
                0,
                0,
                1,
                1,
                pageIndex,
                0,
                0,
                new[] { useIndex });
        }

        private static AtlasPagePlanEntry Page(
            GraphicsFormat format,
            int width,
            int height,
            int mipCount,
            params AtlasPlacement[] placements)
        {
            return new AtlasPagePlanEntry(
                width,
                height,
                format,
                mipCount,
                placements);
        }

        private static AtlasGroupPlan Group(
            string materialKey,
            GraphicsFormat format,
            AtlasPagePlanEntry[] pages,
            params int[] admittedUseIndices)
        {
            var key = new AtlasGroupKey(
                "Poiyomi 9.3 release main",
                AtlasGroupKey.MainColorRouteName,
                format,
                Sampler,
                materialKey,
                Array.Empty<string>());
            return new AtlasGroupPlan(
                key,
                pages,
                admittedUseIndices,
                Array.Empty<AtlasRefusedUse>());
        }

        [Test]
        public void TextureBytesMatchesTheHandComputedLength()
        {
            // DXT1 stores 8 bytes per 4 by 4 block. Level 0 of a 4160
            // by 2080 chain holds 1040 * 520 blocks, so 4326400 bytes.
            // Each smaller level rounds up to whole blocks the same
            // way. The twelve smaller levels add 1442696 bytes.
            const long expected = 5769096;

            Assert.That(
                AtlasBudget.TextureBytes(
                    4160, 2080, GraphicsFormat.RGBA_DXT1_SRGB, 13),
                Is.EqualTo(expected));

            var texture = new Texture2D(
                4160, 2080, TextureFormat.DXT1, 13, false, true);
            try
            {
                Assert.That(
                    texture.GetRawTextureData().Length,
                    Is.EqualTo(expected),
                    "a real texture of the shape must hold the same"
                    + " byte count");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void GroupRetainedBytesSumsTheBytesOfEveryPage()
        {
            var plan = Group(
                "material",
                GraphicsFormat.R8_UNorm,
                new[]
                {
                    Page(GraphicsFormat.R8_UNorm, 82, 1, 1),
                    Page(GraphicsFormat.R8_UNorm, 81, 1, 1),
                },
                0);

            Assert.That(
                AtlasBudget.GroupRetainedBytes(plan),
                Is.EqualTo(82 + 81));
        }

        [Test]
        public void SourceThatAnotherUseReferencesStaysCounted()
        {
            var format = GraphicsFormat.R8G8B8A8_SRGB;
            var uses = new[]
            {
                Use(5, format, 8, 8, 1, 0),
                Use(5, format, 8, 8, 1, 1),
                Use(6, format, 4, 4, 1, 2),
                Use(5, format, 8, 8, 1, 3),
            };
            var firstGroup = Group(
                "first",
                format,
                new[] { Page(format, 8, 8, 1) },
                0,
                1,
                2);
            var secondGroup = Group(
                "second",
                format,
                new[] { Page(format, 8, 8, 1) },
                3);

            Assert.That(
                AtlasBudget.SourceBytes(firstGroup, uses),
                Is.EqualTo(4 * 8 * 8 + 4 * 4 * 4),
                "the first group counts each distinct source once");
            Assert.That(
                AtlasBudget.SourceBytes(secondGroup, uses),
                Is.EqualTo(4 * 8 * 8),
                "the shared source still counts in the second group");
        }

        [Test]
        public void PredictPagePeakBytesSumsEveryTermOfOnePage()
        {
            // One 8 by 8 page with two sources, an 8 by 8 source and a
            // 4 by 4 source, both RGBA32 sRGB, at P = 1:
            // compose buffer 320, page bytes 320, raw levels 256 + 64,
            // Color32 arrays 256 + 64, largest clone 320, check decode
            // 256 + 256. The sum is 2112.
            var format = GraphicsFormat.R8G8B8A8_SRGB;
            var uses = new[]
            {
                Use(11, format, 8, 8, 2, 0),
                Use(12, format, 4, 4, 1, 1),
            };
            var page = Page(
                format,
                8,
                8,
                2,
                PlacementOf(11, 0, 0),
                PlacementOf(12, 0, 1));

            Assert.That(
                AtlasBudget.PredictPagePeakBytes(page, uses, 1),
                Is.EqualTo(2112));
        }

        [Test]
        public void PredictPagePeakBytesUsesEveryProtectedLevel()
        {
            // One 8 by 8 page with one 8 by 8 RGBA32 sRGB source of two
            // levels, at P = 2: compose buffer 256, page bytes 256, raw
            // levels 320, Color32 array 256, largest clone 320, check
            // decode 256 + (256 + 64). The sum is 1984.
            var format = GraphicsFormat.R8G8B8A8_SRGB;
            var uses = new[] { Use(21, format, 8, 8, 2, 0) };
            var page = Page(format, 8, 8, 1, PlacementOf(21, 0, 0));

            Assert.That(
                AtlasBudget.PredictPagePeakBytes(page, uses, 2),
                Is.EqualTo(1984));
        }

        [Test]
        public void PredictPeakBytesTakesTheLargestPagePrediction()
        {
            // The first page predicts 1792 bytes. The second page
            // predicts 448 bytes. The group keeps the larger number.
            var format = GraphicsFormat.R8G8B8A8_SRGB;
            var uses = new[]
            {
                Use(31, format, 8, 8, 1, 0),
                Use(32, format, 4, 4, 1, 1),
            };
            var plan = Group(
                "material",
                format,
                new[]
                {
                    Page(format, 8, 8, 1, PlacementOf(31, 0, 0)),
                    Page(format, 4, 4, 1, PlacementOf(32, 1, 1)),
                },
                0,
                1);

            Assert.That(
                AtlasBudget.PredictPeakBytes(plan, uses, 1),
                Is.EqualTo(1792));
        }
    }
}
