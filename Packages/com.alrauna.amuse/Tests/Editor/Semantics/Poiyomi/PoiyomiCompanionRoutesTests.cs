using System;
using Alrauna.Amuse.Editor.Build;
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
    /// objects the capture reads. A capture-level refusal that needs
    /// a route in the table runs against the member conditions here
    /// and against each real companion route in the group test files
    /// once the group tasks fill the stubs.
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
        public void SwitchedTextureWithNoKeywordOnIsInert()
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(false);
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
            [ValueSource(nameof(PolarSelectors))] int selector)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(false);
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
            [Values(0.5f, 10f, -1f, float.NaN)] float selector)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(false);
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
        public void NonIdentityTransformOrPanRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
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
                " zero pan");

            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetVector(
                "_MainTex_ST", new Vector4(2f, 1f, 0f, 0f));
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(material),
                AtlasUseRefusal.NonIdentityTransform,
                "The property '_MainTex_ST' is (2, 1, 0, 0)");

            var panned = fixture.NewMaterial(twoPass);
            panned.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            panned.SetVector(
                "_MainTexPan", new Vector4(0.37f, 0f, 0f, 0f));
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(panned),
                AtlasUseRefusal.NonIdentityTransform,
                "The property '_MainTexPan' is (0.37, 0, 0, 0)");
        }

        // --- Falsifier 6: StochasticFlagRefuses ----------------------
        [Test]
        public void StochasticFlagRefuses()
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(false);
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

            var member = fixture.NewMaterial(false);
            member.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            member.SetFloat("_MainTexStochastic", 1f);
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(member),
                AtlasUseRefusal.RequiredValueNotHeld,
                "The property '_MainTexStochastic' is 1");
        }

        // --- Falsifier 7: AnimatedSelectorRefuses --------------------
        [Test]
        public void AnimatedSelectorRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            PoiyomiReviewedFixture.AssertAdmitted(
                fixture.CaptureSingle(material));

            fixture.AnimateScalar(fixture.Root, "_MainTexUV");
            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.AnimatedRouteProperty,
                "_MainTexUV");
        }

        // --- Falsifier 7: PropertyBlockOnSelectorRefuses -------------
        [Test]
        public void PropertyBlockOnSelectorRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            var renderer = fixture.Root
                .transform
                .GetChild(0)
                .GetComponent<SkinnedMeshRenderer>();
            var block = new MaterialPropertyBlock();
            block.SetFloat("_MainTexUV", 1f);
            renderer.SetPropertyBlock(block);

            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.PropertyBlockOnRouteProperty,
                "_MainTexUV");
        }

        [Test]
        public void ClampSampledTextureRefusesOnlyAtSelectorZero()
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(false);
            material.SetTexture(
                "_UzumoreMask", fixture.NewRouteTexture(64));
            // The texture import runs first. A synchronous import can
            // reset the keyword state that the vendor tool manages,
            // so the keyword goes on after the import.
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

        private static string Number(float value)
        {
            return value.ToString(
                "0.######",
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
