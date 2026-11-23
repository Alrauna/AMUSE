using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The companion route builder tests of the reviewed Poiyomi 9.3
    /// route. The routes exercise the exact <c>AtlasCompanionRoute</c>
    /// objects the capture reads. A wrong helper fails these tests.
    /// The capture-level companion falsifiers, one per condition with
    /// an admitted twin, belong to the group test files, because a
    /// capture needs a route in the table.
    /// </summary>
    public sealed class PoiyomiCompanionRoutesTests
    {
        private static readonly int[] OtherSelectors = { 1, 2, 3, 4, 5, 8, 9 };
        private static readonly int[] PolarSelectors = { 6, 7 };

        private static AtlasCompanionRoute StandardBumpRoute()
        {
            return PoiyomiCompanionRoutes.Standard(
                "_BumpMap",
                AtlasCompanionSampler.Member,
                Array.Empty<string>(),
                false);
        }

        /// <summary>
        /// The texture of the standard pattern with an always-on
        /// gate. Assignment is the gate, so the empty keyword list
        /// admits. The selector reads as a float, because the
        /// selector property is an Int property on both bundled rows.
        /// </summary>
        [Test]
        public void SelectorZeroMovesTheTexture(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture(
                "_BumpMap", fixture.NewRouteTexture(64));
            material.SetFloat("_BumpMapUV", 0f);

            var route = StandardBumpRoute();

            Assert.That(
                route.UseOf(material, false),
                Is.EqualTo(AtlasRouteUse.Uv0),
                "an assigned texture at selector 0 is a companion");
            Assert.That(
                route.UnheldRequiredValue(material), Is.Null,
                "the companion at selector 0 holds every value");

            Assert.That(
                route.ConditionProperties,
                Is.EquivalentTo(
                    new[] { "_BumpMapUV", "_BumpMap_ST", "_BumpMapPan" }),
                "the route conditions are the selector, the" +
                " transform, and the pan");
        }

        [Test]
        public void SwitchedTextureWithNoKeywordOnIsInert(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            var route = PoiyomiCompanionRoutes.Standard(
                "_BumpMap",
                AtlasCompanionSampler.Member,
                new[] { "_LIGHTINGMODE_REALISTIC" },
                false);

            Assert.That(
                route.UseOf(material, false),
                Is.EqualTo(AtlasRouteUse.Inert),
                "no feature keyword on means no use");
        }

        // --- Falsifier 2: OtherSelectorsKeepTheTextureBound ----------
        [Test]
        public void OtherSelectorsKeepTheTextureBound(
            [ValueSource(nameof(OtherSelectors))] int selector,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture(
                "_BumpMap", fixture.NewRouteTexture(64));
            material.SetFloat("_BumpMapUV", selector);

            var route = StandardBumpRoute();

            Assert.That(
                route.UseOf(material, false),
                Is.EqualTo(AtlasRouteUse.OtherCoordinates),
                "selector " + selector +
                " keeps the texture bound and keyed");
            Assert.That(
                route.UnheldRequiredValue(material), Is.Null,
                "another coordinate needs no route value");
        }

        // --- Falsifier 3: PolarAndDistortedSelectorsRefuse -----------
        [Test]
        public void PolarAndDistortedSelectorsRefuse(
            [ValueSource(nameof(PolarSelectors))] int selector,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture(
                "_BumpMap", fixture.NewRouteTexture(64));
            material.SetFloat("_BumpMapUV", selector);

            var route = StandardBumpRoute();

            Assert.That(
                route.UseOf(material, false),
                Is.EqualTo(AtlasRouteUse.Uv0),
                "the coordinate is UV0 data, so the use waits for" +
                " the value check");
            Assert.That(
                route.UnheldRequiredValue(material),
                Does.Contain("'_BumpMapUV'"),
                "the detail names the selector property");
            Assert.That(
                route.UnheldRequiredValue(material),
                Does.Contain(Number(selector)),
                "the detail names the selector value");
        }

        // --- Falsifier 4: FractionalOrNaNSelectorRefuses -------------
        [Test]
        public void FractionalOrNaNSelectorRefuses(
            [Values(0.5f, 10f, -1f, float.NaN)] float selector,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetFloat("_BumpMapUV", selector);

            var route = StandardBumpRoute();

            Assert.That(
                route.UseOf(material, false),
                Is.EqualTo(AtlasRouteUse.Uv0),
                "a value that is not a listed selector fails" +
                " closed as UV0");
            Assert.That(
                route.UnheldRequiredValue(material),
                Does.Contain("'_BumpMapUV'"),
                "the detail names the selector property");
        }

        // --- Falsifier 5: NonIdentityTransformOrPanRefuses -----------
        [Test]
        public void NonIdentityTransformOrPanRefuses()
        {
            var route = StandardBumpRoute();

            Assert.That(
                route.TransformProperties,
                Is.EqualTo(
                    new (string, Vector4)[]
                    {
                        ("_BumpMap_ST", new Vector4(1f, 1f, 0f, 0f)),
                        ("_BumpMapPan", new Vector4(0f, 0f, 0f, 0f)),
                    }),
                "the route demands the identity transform and the" +
                " zero pan, so the capture refuses every other value" +
                " with NonIdentityTransform");
        }

        // --- Falsifier 6: StochasticFlagRefuses ----------------------
        [Test]
        public void StochasticFlagRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetFloat("_BumpMapUV", 0f);
            material.SetFloat("_BumpMapStochastic", 1f);

            var route = PoiyomiCompanionRoutes.Standard(
                "_BumpMap",
                AtlasCompanionSampler.Member,
                Array.Empty<string>(),
                true);

            Assert.That(
                route.UseOf(material, false),
                Is.EqualTo(AtlasRouteUse.Uv0),
                "the stochastic flag is a UV0 condition");
            Assert.That(
                route.UnheldRequiredValue(material),
                Does.Contain("'_BumpMapStochastic'"),
                "the detail names the stochastic flag");
            Assert.That(
                route.ConditionProperties,
                Does.Contain("_BumpMapStochastic"),
                "the stochastic property is a route condition");
        }

        [Test]
        public void ClampSampledTextureRefusesOnlyAtSelectorZero(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture(
                "_UzumoreMask", fixture.NewRouteTexture(64));
            // The texture import runs first. The test sets keyword
            // state after every import.
            material.EnableKeyword("POI_UZUMORE");

            var route = PoiyomiCompanionRoutes.ClampSampled(
                "_UzumoreMask",
                new[] { "POI_UZUMORE" },
                false);

            material.SetFloat("_UzumoreMaskUV", 0f);
            Assert.That(
                route.UseOf(material, false),
                Is.EqualTo(AtlasRouteUse.Uv0),
                "selector 0 waits for the value check");
            Assert.That(
                route.UnheldRequiredValue(material),
                Is.EqualTo(
                    "The texture '_UzumoreMask' samples with a" +
                    " clamp sampler on UV0. The route needs a" +
                    " selector other than 0."),
                "selector 0 refuses, because a page texture needs" +
                " Repeat on both axes");

            material.SetFloat("_UzumoreMaskUV", 3f);
            Assert.That(
                route.UseOf(material, false),
                Is.EqualTo(AtlasRouteUse.OtherCoordinates),
                "selector 3 keeps the texture bound");
            Assert.That(
                route.UnheldRequiredValue(material), Is.Null,
                "another coordinate holds no route value");
        }

        [Test]
        public void InlineSamplerCompanionsUseTheirFacts()
        {
            Assert.That(
                (int)AtlasCompanionSampler.InlineLinearRepeat,
                Is.EqualTo(3),
                "the linear repeat inline sampler holds slot 3");
            Assert.That(
                (int)AtlasCompanionSampler.InlinePointRepeat,
                Is.EqualTo(4),
                "the point repeat inline sampler holds slot 4");
            Assert.That(
                (int)AtlasCompanionSampler.InlineClamp,
                Is.EqualTo(5),
                "the clamp inline sampler holds slot 5");

            Assert.That(
                AtlasUseCapture.InlineLinearRepeat,
                Is.EqualTo(
                    new AtlasSamplerFacts(
                        FilterMode.Bilinear,
                        UnityEngine.TextureWrapMode.Repeat,
                        UnityEngine.TextureWrapMode.Repeat,
                        0,
                        0f)),
                "the linear repeat facts are bilinear Repeat" +
                " without anisotropy or bias");
            Assert.That(
                AtlasUseCapture.InlinePointRepeat,
                Is.EqualTo(
                    new AtlasSamplerFacts(
                        FilterMode.Point,
                        UnityEngine.TextureWrapMode.Repeat,
                        UnityEngine.TextureWrapMode.Repeat,
                        0,
                        0f)),
                "the point repeat facts are point Repeat without" +
                " anisotropy or bias");
        }

        /// <summary>
        /// The self-test of the shared inventory helper that the
        /// four group tasks call. The Always on group is the one
        /// group whose every companion row follows the standard
        /// pattern, so the test builds the whole route set from the
        /// inventory rows with the route builder and exercises every
        /// branch of the helper: the correct set passes, a wrong
        /// sampler throws, a stochastic row built without its
        /// stochastic condition throws, and a removed route throws.
        /// A wrong helper can therefore never reach a group test
        /// silently.
        /// </summary>
        [Test]
        public void InventoryHelperSelfTestOnTheAlwaysOnGroup()
        {
            var rows = PoiyomiReviewedInventory.Textures
                .Where(row =>
                    row.Group == "Always on" &&
                    row.Class == "companion")
                .ToList();
            Assert.That(
                rows, Has.Count.EqualTo(8),
                "fixture precondition: the record lists eight" +
                " companion rows for the Always on group");

            static AtlasCompanionRoute BuildRoute(
                (string Texture, string Group, string Class,
                    AtlasCompanionSampler? Sampler, bool Stochastic) row,
                bool stochastic,
                AtlasCompanionSampler? samplerOverride = null)
            {
                return PoiyomiCompanionRoutes.Standard(
                    row.Texture,
                    samplerOverride ?? row.Sampler.Value,
                    Array.Empty<string>(),
                    stochastic);
            }

            var correct = rows
                .Select(row => BuildRoute(row, row.Stochastic))
                .ToList();
            PoiyomiReviewedInventory.AssertRoutesMatchInventory(
                "Always on", correct);

            var wrongSampler = rows
                .Select(row => BuildRoute(
                    row,
                    row.Stochastic,
                    row.Texture == "_BumpMap"
                        ? (AtlasCompanionSampler?)
                        AtlasCompanionSampler.InlineTrilinearRepeat
                        : null))
                .ToList();
            Assert.That(
                () => PoiyomiReviewedInventory.AssertRoutesMatchInventory(
                    "Always on", wrongSampler),
                Throws.TypeOf<AssertionException>(),
                "a wrong sampler kind must fail the helper");

            var withoutStochastic = rows
                .Select(row => BuildRoute(row, false))
                .ToList();
            Assert.That(
                () => PoiyomiReviewedInventory.AssertRoutesMatchInventory(
                    "Always on", withoutStochastic),
                Throws.TypeOf<AssertionException>(),
                "a stochastic row built without its stochastic" +
                " condition must fail the helper");

            var removed = correct
                .Where(route => route.Property != "_OutlineTexture")
                .ToList();
            Assert.That(
                () => PoiyomiReviewedInventory.AssertRoutesMatchInventory(
                    "Always on", removed),
                Throws.TypeOf<AssertionException>(),
                "a removed route must fail the helper");
        }

        private static string Number(float value)
        {
            return value.ToString(
                "0.######",
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
