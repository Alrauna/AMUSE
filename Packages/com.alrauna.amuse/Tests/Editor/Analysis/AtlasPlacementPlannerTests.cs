using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class AtlasPlacementPlannerTests
    {
        private static readonly AtlasSamplerFacts RepeatAnisoSampler =
            new AtlasSamplerFacts(
                FilterMode.Bilinear,
                UnityEngine.TextureWrapMode.Repeat,
                UnityEngine.TextureWrapMode.Repeat,
                16,
                0f);

        private static readonly AtlasSamplerFacts RepeatNoAnisoSampler =
            new AtlasSamplerFacts(
                FilterMode.Bilinear,
                UnityEngine.TextureWrapMode.Repeat,
                UnityEngine.TextureWrapMode.Repeat,
                0,
                0f);

        private static readonly AtlasSamplerFacts ClampAnisoSampler =
            new AtlasSamplerFacts(
                FilterMode.Bilinear,
                UnityEngine.TextureWrapMode.Clamp,
                UnityEngine.TextureWrapMode.Clamp,
                16,
                0f);

        private static CapturedAtlasUse WholeSource(
            int instanceId,
            GraphicsFormat format,
            int size,
            int captureOrder,
            string materialKey = "material",
            AtlasSamplerFacts? sampler = null)
        {
            return Region(
                instanceId,
                format,
                size,
                0.0,
                0.0,
                1.0,
                1.0,
                captureOrder,
                materialKey,
                sampler);
        }

        private static CapturedAtlasUse Region(
            int instanceId,
            GraphicsFormat format,
            int size,
            double minU,
            double minV,
            double maxU,
            double maxV,
            int captureOrder,
            string materialKey = "material",
            AtlasSamplerFacts? sampler = null)
        {
            return new CapturedAtlasUse(
                0,
                0,
                0,
                "Poiyomi 9.3 release main",
                instanceId,
                format,
                size,
                size,
                1,
                sampler ?? RepeatAnisoSampler,
                new AtlasUvRect(minU, minV, maxU, maxV),
                materialKey,
                Array.Empty<string>(),
                captureOrder);
        }

        [Test]
        public void TwoBorderedThousandPlacementsShareOnePageOfAtMost2112()
        {
            var uses = new[]
            {
                WholeSource(101, GraphicsFormat.R8G8B8A8_SRGB, 1024, 0),
                WholeSource(102, GraphicsFormat.R8G8B8A8_SRGB, 1024, 1),
            };

            var plans = AtlasPlacementPlanner.Plan(uses, 8192, 1);

            Assert.That(plans.Count, Is.EqualTo(1));
            Assert.That(plans[0].Pages.Count, Is.EqualTo(1));
            var page = plans[0].Pages[0];
            Assert.That(page.Placements.Count, Is.EqualTo(2));
            Assert.That(Math.Max(page.Width, page.Height), Is.AtMost(2112));
            Assert.That(page.Width, Is.EqualTo(2112));
            Assert.That(page.Height, Is.EqualTo(1056));
        }

        [Test]
        public void TwoWhole2048SourcesShareOne4160By2080Page()
        {
            var uses = new[]
            {
                WholeSource(201, GraphicsFormat.RGBA_DXT1_SRGB, 2048, 0),
                WholeSource(202, GraphicsFormat.RGBA_DXT1_SRGB, 2048, 1),
            };

            var plans = AtlasPlacementPlanner.Plan(uses, 8192, 1);

            Assert.That(plans.Count, Is.EqualTo(1));
            Assert.That(plans[0].Pages.Count, Is.EqualTo(1));
            var page = plans[0].Pages[0];
            Assert.That(page.Width, Is.EqualTo(4160));
            Assert.That(page.Height, Is.EqualTo(2080));
            Assert.That(page.Format, Is.EqualTo(GraphicsFormat.RGBA_DXT1_SRGB));
            Assert.That(page.MipCount, Is.EqualTo(13));
        }

        [Test]
        public void PlacementOriginsAndSizesSitOnTheGrid()
        {
            var uses = new[]
            {
                Region(
                    301,
                    GraphicsFormat.RGBA_DXT1_SRGB,
                    512,
                    0.111,
                    0.111,
                    0.777,
                    0.777,
                    0),
                WholeSource(302, GraphicsFormat.RGBA_DXT1_SRGB, 1024, 1),
            };

            var plans = AtlasPlacementPlanner.Plan(uses, 8192, 2);

            var placements = plans[0].Pages
                .SelectMany(page => page.Placements)
                .ToList();
            Assert.That(placements.Count, Is.EqualTo(2));
            foreach (var placement in placements)
            {
                Assert.That(placement.SourceX % 8, Is.Zero);
                Assert.That(placement.SourceY % 8, Is.Zero);
                Assert.That(placement.Width % 8, Is.Zero);
                Assert.That(placement.Height % 8, Is.Zero);
                Assert.That(placement.PageX % 8, Is.Zero);
                Assert.That(placement.PageY % 8, Is.Zero);
            }

            var inset = placements.Single(
                placement => placement.SourceTextureInstanceId == 301);
            Assert.That(inset.SourceX, Is.EqualTo(24));
            Assert.That(inset.SourceY, Is.EqualTo(24));
            Assert.That(inset.Width, Is.EqualTo(408));
            Assert.That(inset.Height, Is.EqualTo(408));
        }

        [Test]
        public void PlacementThatDoesNotFitAnEmptyPageRefuses()
        {
            var uses = new[]
            {
                WholeSource(401, GraphicsFormat.R8G8B8A8_SRGB, 4096, 0),
            };

            var plans = AtlasPlacementPlanner.Plan(uses, 4096, 1);

            Assert.That(plans.Count, Is.EqualTo(1));
            Assert.That(plans[0].Pages, Is.Empty);
            Assert.That(plans[0].AdmittedUseIndices, Is.Empty);
            Assert.That(plans[0].RefusedUses.Count, Is.EqualTo(1));
            Assert.That(plans[0].RefusedUses[0].UseIndex, Is.EqualTo(0));
            Assert.That(
                plans[0].RefusedUses[0].Refusal,
                Is.EqualTo(AtlasPlacementRefusal.PlacementTooLarge));
        }

        [Test]
        public void FullPageStartsANewPageInTheSameGroup()
        {
            var uses = new[]
            {
                WholeSource(501, GraphicsFormat.RGBA_DXT1_SRGB, 2048, 0),
                WholeSource(502, GraphicsFormat.RGBA_DXT1_SRGB, 2048, 1),
                WholeSource(503, GraphicsFormat.RGBA_DXT1_SRGB, 2048, 2),
                WholeSource(504, GraphicsFormat.RGBA_DXT1_SRGB, 2048, 3),
                WholeSource(505, GraphicsFormat.RGBA_DXT1_SRGB, 2048, 4),
            };

            var plans = AtlasPlacementPlanner.Plan(uses, 4160, 1);

            Assert.That(plans.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2, 3, 4 },
                plans[0].AdmittedUseIndices);
            var pages = plans[0].Pages;
            Assert.That(pages.Count, Is.EqualTo(2));
            Assert.That(pages[0].Placements.Count, Is.EqualTo(4));
            Assert.That(pages[0].Width, Is.EqualTo(4160));
            Assert.That(pages[0].Height, Is.EqualTo(4160));
            Assert.That(pages[1].Placements.Count, Is.EqualTo(1));
            Assert.That(pages[1].Width, Is.EqualTo(2080));
            Assert.That(pages[1].Height, Is.EqualTo(2080));

            var positions = pages[0].Placements.ToDictionary(
                placement => placement.SourceTextureInstanceId);
            Assert.That(positions[501].PageX, Is.EqualTo(0));
            Assert.That(positions[501].PageY, Is.EqualTo(0));
            Assert.That(positions[502].PageX, Is.EqualTo(2080));
            Assert.That(positions[502].PageY, Is.EqualTo(0));
            Assert.That(positions[503].PageX, Is.EqualTo(0));
            Assert.That(positions[503].PageY, Is.EqualTo(2080));
            Assert.That(positions[504].PageX, Is.EqualTo(2080));
            Assert.That(positions[504].PageY, Is.EqualTo(2080));
            Assert.That(pages[1].Placements[0].PageX, Is.EqualTo(0));
            Assert.That(pages[1].Placements[0].PageY, Is.EqualTo(0));
        }

        [Test]
        public void UsesOfOneSourceShareOnePlacementWithTheUnionRegion()
        {
            var uses = new[]
            {
                Region(
                    601,
                    GraphicsFormat.R8G8B8A8_SRGB,
                    1024,
                    0.1,
                    0.1,
                    0.4,
                    0.4,
                    0,
                    sampler: RepeatNoAnisoSampler),
                Region(
                    601,
                    GraphicsFormat.R8G8B8A8_SRGB,
                    1024,
                    0.2,
                    0.2,
                    0.6,
                    0.6,
                    1,
                    sampler: RepeatNoAnisoSampler),
            };

            var plans = AtlasPlacementPlanner.Plan(uses, 8192, 1);

            Assert.That(plans.Count, Is.EqualTo(1));
            Assert.That(plans[0].Pages.Count, Is.EqualTo(1));
            var placement = plans[0].Pages[0].Placements.Single();
            Assert.That(placement.SourceX, Is.EqualTo(101));
            Assert.That(placement.SourceY, Is.EqualTo(101));
            Assert.That(placement.Width, Is.EqualTo(515));
            Assert.That(placement.Height, Is.EqualTo(515));
            CollectionAssert.AreEqual(new[] { 0, 1 }, placement.UseIndices);
        }

        [Test]
        public void TwoSourceFormatsLandInDifferentGroups()
        {
            var uses = new[]
            {
                WholeSource(701, GraphicsFormat.R8G8B8A8_SRGB, 1024, 0),
                WholeSource(702, GraphicsFormat.RGBA_DXT1_SRGB, 1024, 1),
            };

            var plans = AtlasPlacementPlanner.Plan(uses, 8192, 1);

            Assert.That(plans.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 0 }, plans[0].AdmittedUseIndices);
            CollectionAssert.AreEqual(new[] { 1 }, plans[1].AdmittedUseIndices);
            Assert.That(
                plans[0].Key.SourceFormat,
                Is.EqualTo(GraphicsFormat.R8G8B8A8_SRGB));
            Assert.That(
                plans[1].Key.SourceFormat,
                Is.EqualTo(GraphicsFormat.RGBA_DXT1_SRGB));
        }

        [Test]
        public void DifferentMaterialKeysLandInDifferentGroups()
        {
            var uses = new[]
            {
                WholeSource(
                    801,
                    GraphicsFormat.R8G8B8A8_SRGB,
                    1024,
                    0,
                    materialKey: "material-a"),
                WholeSource(
                    801,
                    GraphicsFormat.R8G8B8A8_SRGB,
                    1024,
                    1,
                    materialKey: "material-b"),
            };

            var plans = AtlasPlacementPlanner.Plan(uses, 8192, 1);

            Assert.That(plans.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 0 }, plans[0].AdmittedUseIndices);
            CollectionAssert.AreEqual(new[] { 1 }, plans[1].AdmittedUseIndices);
        }

        [Test]
        public void ClampRegionTouchingTheEdgeOnBc7Refuses()
        {
            var uses = new[]
            {
                WholeSource(
                    901,
                    GraphicsFormat.RGBA_BC7_SRGB,
                    1024,
                    0,
                    sampler: ClampAnisoSampler),
            };

            var plans = AtlasPlacementPlanner.Plan(uses, 8192, 1);

            Assert.That(plans.Count, Is.EqualTo(1));
            Assert.That(plans[0].Pages, Is.Empty);
            Assert.That(plans[0].AdmittedUseIndices, Is.Empty);
            Assert.That(plans[0].RefusedUses.Count, Is.EqualTo(1));
            Assert.That(plans[0].RefusedUses[0].UseIndex, Is.EqualTo(0));
            Assert.That(
                plans[0].RefusedUses[0].Refusal,
                Is.EqualTo(AtlasPlacementRefusal.ClampEdgeOnBc7));
        }

        [Test]
        public void ClampRegionInsideTheImageOnBc7IsPlanned()
        {
            var uses = new[]
            {
                Region(
                    902,
                    GraphicsFormat.RGBA_BC7_SRGB,
                    1024,
                    0.05,
                    0.05,
                    0.9,
                    0.9,
                    0,
                    sampler: ClampAnisoSampler),
            };

            var plans = AtlasPlacementPlanner.Plan(uses, 8192, 1);

            Assert.That(plans[0].Pages.Count, Is.EqualTo(1));
            var placement = plans[0].Pages[0].Placements.Single();
            Assert.That(placement.SourceX, Is.EqualTo(32));
            Assert.That(placement.SourceY, Is.EqualTo(32));
            Assert.That(placement.Width, Is.EqualTo(908));
            Assert.That(placement.Height, Is.EqualTo(908));
            CollectionAssert.AreEqual(new[] { 0 }, plans[0].AdmittedUseIndices);
        }

        [Test]
        public void RepeatRegionTouchingTheEdgeIsPlannedAcrossTheEdge()
        {
            var uses = new[]
            {
                Region(
                    1001,
                    GraphicsFormat.R8G8B8A8_SRGB,
                    1024,
                    0.01,
                    0.01,
                    0.99,
                    0.99,
                    0),
            };

            var plans = AtlasPlacementPlanner.Plan(uses, 8192, 1);

            Assert.That(plans[0].Pages.Count, Is.EqualTo(1));
            var placement = plans[0].Pages[0].Placements.Single();
            Assert.That(placement.SourceX, Is.EqualTo(-6));
            Assert.That(placement.SourceY, Is.EqualTo(-6));
            Assert.That(placement.Width, Is.EqualTo(1036));
            Assert.That(placement.Height, Is.EqualTo(1036));
            CollectionAssert.AreEqual(new[] { 0 }, plans[0].AdmittedUseIndices);
        }

        [Test]
        public void PlacementIsDeterministic()
        {
            var ordered = new[]
            {
                WholeSource(1101, GraphicsFormat.R8G8B8A8_SRGB, 1024, 0),
                WholeSource(1102, GraphicsFormat.RGBA_DXT1_SRGB, 1024, 1),
                Region(
                    1101,
                    GraphicsFormat.R8G8B8A8_SRGB,
                    1024,
                    0.1,
                    0.1,
                    0.5,
                    0.5,
                    2),
                Region(
                    1102,
                    GraphicsFormat.RGBA_DXT1_SRGB,
                    1024,
                    0.2,
                    0.2,
                    0.8,
                    0.8,
                    3),
            };
            var shuffled = new[]
            {
                ordered[2],
                ordered[0],
                ordered[3],
                ordered[1],
            };

            var first = AtlasPlacementPlanner.Plan(ordered, 8192, 1);
            var second = AtlasPlacementPlanner.Plan(ordered, 8192, 1);
            var third = AtlasPlacementPlanner.Plan(shuffled, 8192, 1);

            AssertPlansEqual(first, second);
            AssertPlansEqual(first, third);
        }

        private static void AssertPlansEqual(
            IReadOnlyList<AtlasGroupPlan> expected,
            IReadOnlyList<AtlasGroupPlan> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (var groupIndex = 0; groupIndex < expected.Count; groupIndex++)
            {
                var expectedGroup = expected[groupIndex];
                var actualGroup = actual[groupIndex];
                Assert.That(actualGroup.Key, Is.EqualTo(expectedGroup.Key));
                CollectionAssert.AreEqual(
                    expectedGroup.AdmittedUseIndices,
                    actualGroup.AdmittedUseIndices);
                Assert.That(
                    actualGroup.RefusedUses.Count,
                    Is.EqualTo(expectedGroup.RefusedUses.Count));
                for (var refusalIndex = 0;
                     refusalIndex < expectedGroup.RefusedUses.Count;
                     refusalIndex++)
                {
                    Assert.That(
                        actualGroup.RefusedUses[refusalIndex].UseIndex,
                        Is.EqualTo(expectedGroup.RefusedUses[refusalIndex].UseIndex));
                    Assert.That(
                        actualGroup.RefusedUses[refusalIndex].Refusal,
                        Is.EqualTo(expectedGroup.RefusedUses[refusalIndex].Refusal));
                }

                Assert.That(
                    actualGroup.Pages.Count,
                    Is.EqualTo(expectedGroup.Pages.Count));
                for (var pageIndex = 0;
                     pageIndex < expectedGroup.Pages.Count;
                     pageIndex++)
                {
                    var expectedPage = expectedGroup.Pages[pageIndex];
                    var actualPage = actualGroup.Pages[pageIndex];
                    Assert.That(actualPage.Width, Is.EqualTo(expectedPage.Width));
                    Assert.That(actualPage.Height, Is.EqualTo(expectedPage.Height));
                    Assert.That(actualPage.Format, Is.EqualTo(expectedPage.Format));
                    Assert.That(actualPage.MipCount, Is.EqualTo(expectedPage.MipCount));
                    Assert.That(
                        actualPage.Placements.Count,
                        Is.EqualTo(expectedPage.Placements.Count));
                    for (var placementIndex = 0;
                         placementIndex < expectedPage.Placements.Count;
                         placementIndex++)
                    {
                        var expectedPlacement = expectedPage.Placements[placementIndex];
                        var actualPlacement = actualPage.Placements[placementIndex];
                        Assert.That(
                            actualPlacement.SourceTextureInstanceId,
                            Is.EqualTo(expectedPlacement.SourceTextureInstanceId));
                        Assert.That(
                            actualPlacement.Sampler,
                            Is.EqualTo(expectedPlacement.Sampler));
                        Assert.That(
                            actualPlacement.SourceX,
                            Is.EqualTo(expectedPlacement.SourceX));
                        Assert.That(
                            actualPlacement.SourceY,
                            Is.EqualTo(expectedPlacement.SourceY));
                        Assert.That(
                            actualPlacement.Width,
                            Is.EqualTo(expectedPlacement.Width));
                        Assert.That(
                            actualPlacement.Height,
                            Is.EqualTo(expectedPlacement.Height));
                        Assert.That(
                            actualPlacement.PageIndex,
                            Is.EqualTo(expectedPlacement.PageIndex));
                        Assert.That(
                            actualPlacement.PageX,
                            Is.EqualTo(expectedPlacement.PageX));
                        Assert.That(
                            actualPlacement.PageY,
                            Is.EqualTo(expectedPlacement.PageY));
                        CollectionAssert.AreEqual(
                            expectedPlacement.UseIndices,
                            actualPlacement.UseIndices);
                    }
                }
            }
        }
    }
}
