using System.Linq;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The capture tests of the 'Always on' feature group of the
    /// reviewed Poiyomi 9.3 route. The record of 2026-11-23,
    /// sections 4.1, 4.3, 5, and 8, carries the rows of the group.
    /// The group routes the seven always assigned companions and the
    /// light data map that never samples in an admitted use. The
    /// table rule of the group requires <c>_LightingMapMode</c>
    /// not 4 while <c>PROP_LIGHTDATASDFMAP</c> is on. A
    /// <c>PROP_&lt;texture&gt;</c> keyword never gates a route, so a
    /// <c>PROP_BUMPMAP</c> state that disagrees with its texture
    /// stays exact.
    /// </summary>
    public sealed class PoiyomiAlwaysOnRoutesTests
    {
        private const string Group = "Always on";

        private static readonly int[] PolarSelectors = { 6, 7 };

        [Test]
        public void GroupInventoryIsRouted()
        {
            var routes = PoiyomiAlwaysOnRoutes.Routes();
            PoiyomiReviewedInventory.AssertRoutesMatchInventory(
                Group, routes);

            var table = AtlasRouteTable.PoiyomiReviewedMainColor;
            foreach (var row in PoiyomiReviewedInventory.Textures
                .Where(row => row.Group == Group))
            {
                if (row.Class == "member")
                {
                    Assert.That(
                        table.MemberProperty, Is.EqualTo(row.Texture),
                        "the member row of the group stays the member" +
                        " texture of the table");
                    continue;
                }

                if (row.Class == "companion")
                {
                    Assert.That(
                        routes.Any(
                            route => route.Property == row.Texture),
                        Is.True,
                        "the texture '" + row.Texture +
                        "' carries a companion route in the group");
                    continue;
                }

                Assert.That(
                    table.NeutralProperties, Does.Contain(row.Texture),
                    "the texture '" + row.Texture +
                    "' is a neutral of the table");
            }

            foreach (var row in PoiyomiReviewedInventory.Keywords
                .Where(row => row.Group == Group))
            {
                Assert.That(
                    row.Verdict, Is.Not.EqualTo("Refuse"),
                    "fixture precondition: the record refuses no" +
                    " keyword of the group");
                Assert.That(
                    table.AllowedKeywords, Does.Contain(row.Keyword),
                    "the keyword '" + row.Keyword +
                    "' of the group sits on the allowed list");
            }
        }

        [Test]
        public void BumpMapIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_BumpMap", fixture.NewRouteTexture(16));
            material.SetFloat("_BumpMapUV", 0f);

            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_BumpMap");
        }

        /// <summary>
        /// The alpha mask samples only while
        /// <c>_MainAlphaMaskMode</c> is not 0. The case turns the
        /// mode on, so the assigned mask moves into a page at
        /// selector 0.
        /// </summary>
        [Test]
        public void AlphaMaskIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_AlphaMask", fixture.NewRouteTexture(16));
            material.SetFloat("_AlphaMaskUV", 0f);
            material.SetFloat("_MainAlphaMaskMode", 1f);

            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_AlphaMask");
        }

        [Test]
        public void LightingAOMapsIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_LightingAOMaps", fixture.NewRouteTexture(16));
            material.SetFloat("_LightingAOMapsUV", 0f);
            material.EnableKeyword("PROP_LIGHTINGAOMAPS");

            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_LightingAOMaps");
        }

        [Test]
        public void LightingDetailShadowMapsIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_LightingDetailShadowMaps", fixture.NewRouteTexture(16));
            material.SetFloat("_LightingDetailShadowMapsUV", 0f);
            material.EnableKeyword("PROP_LIGHTINGDETAILSHADOWMAPS");

            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_LightingDetailShadowMaps");
        }

        [Test]
        public void LightingShadowMasksIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_LightingShadowMasks", fixture.NewRouteTexture(16));
            material.SetFloat("_LightingShadowMasksUV", 0f);
            material.EnableKeyword("PROP_LIGHTINGSHADOWMASKS");

            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_LightingShadowMasks");
        }

        /// <summary>
        /// The light data map samples only in light map mode 4. The
        /// table rule refuses mode 4, so the texture never samples in
        /// an admitted use. The admitted case proves that the
        /// assigned texture moves into no page and leaves the key.
        /// </summary>
        [Test]
        public void LightDataSdfMapIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_LightDataSDFMap", fixture.NewRouteTexture(16));
            material.SetFloat("_LightDataSDFMapUV", 0f);
            material.SetFloat("_LightingMapMode", 0f);
            material.EnableKeyword("PROP_LIGHTDATASDFMAP");

            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertAdmitted(outcome);
            Assert.That(
                outcome.Use.Companions, Is.Empty,
                "the light data map never samples in an admitted" +
                " use, so it moves into no page");
        }

        // --- Falsifier 1: LightMapModeFourRefuses --------------------
        [Test]
        public void LightMapModeFourRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_LightDataSDFMap", fixture.NewRouteTexture(16));
            material.SetFloat("_LightDataSDFMapUV", 0f);
            material.SetFloat("_LightingMapMode", 0f);
            material.EnableKeyword("PROP_LIGHTDATASDFMAP");

            // No-op guard: the admitted twin first. Mode 0 admits and
            // the map stays inert.
            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(twin);

            material.SetFloat("_LightingMapMode", 4f);
            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "_LightingMapMode");
        }

        // --- Falsifier 2:
        // PropKeywordOffWithAssignedTextureIsStillACompanion ----------
        /// <summary>
        /// Review focus 1. The shader declares no
        /// <c>PROP_BUMPMAP</c> keyword. Assignment is the gate, so a
        /// disabled <c>PROP_BUMPMAP</c> with an assigned texture
        /// stays a companion. The texture is a companion, not an
        /// unlisted texture and not a dead value.
        /// </summary>
        [Test]
        public void PropKeywordOffWithAssignedTextureIsStillACompanion(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_BumpMap", fixture.NewRouteTexture(16));
            material.SetFloat("_BumpMapUV", 0f);
            material.EnableKeyword("PROP_BUMPMAP");

            // No-op guard: the admitted twin first. The keyword on
            // with the texture assigned is a companion.
            var onOutcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                onOutcome, "_BumpMap");

            material.DisableKeyword("PROP_BUMPMAP");
            var offOutcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertCompanion(
                offOutcome, "_BumpMap");
        }

        // --- Falsifier 3:
        // PropKeywordOnWithUnassignedTextureIsAdmitted ----------------
        /// <summary>
        /// Review focus 1. The keyword on with no texture assigned
        /// admits. The empty texture moves into no page.
        /// </summary>
        [Test]
        public void PropKeywordOnWithUnassignedTextureIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_BumpMap", fixture.NewRouteTexture(16));
            material.SetFloat("_BumpMapUV", 0f);
            material.EnableKeyword("PROP_BUMPMAP");

            // No-op guard: the admitted twin first. The keyword on
            // with the texture assigned is a companion.
            var assigned = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                assigned, "_BumpMap");

            material.SetTexture("_BumpMap", null);
            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertAdmitted(outcome);
            Assert.That(
                outcome.Use.Companions, Is.Empty,
                "an unassigned texture moves into no page");
        }

        // --- Falsifier 4: CompanionThatIsTheMainTextureAliases -------
        /// <summary>
        /// A companion that holds the member texture asset moves into
        /// no second page. The capture records an alias instead.
        /// </summary>
        [Test]
        public void CompanionThatIsTheMainTextureAliases(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            var bumpTexture = fixture.NewRouteTexture(16);
            material.SetTexture("_BumpMap", bumpTexture);
            material.SetFloat("_BumpMapUV", 0f);

            // No-op guard: the admitted twin first. A distinct
            // texture moves into a page.
            var distinct = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                distinct, "_BumpMap");

            material.SetTexture(
                "_BumpMap", material.GetTexture("_MainTex"));
            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertAdmitted(outcome);
            Assert.That(
                outcome.Use.Companions.Any(
                    companion =>
                        companion.Property == "_BumpMap"),
                Is.False,
                "the member texture needs no second page");
            Assert.That(
                outcome.Use.PageAliasProperties,
                Does.Contain("_BumpMap"),
                "the capture records the companion as an alias of" +
                " the main page");
        }

        // --- Falsifier 5: AnimatedSelectorPropertyRefuses ------------
        /// <summary>
        /// An animation that drives the route condition
        /// <c>_BumpMapUV</c> refuses while the texture is assigned.
        /// The runtime could move the texture off selector 0.
        /// </summary>
        [Test]
        public void AnimatedSelectorPropertyRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_BumpMap", fixture.NewRouteTexture(16));
            material.SetFloat("_BumpMapUV", 0f);

            // No-op guard: the admitted twin first. No animation
            // admits and the texture moves into a page.
            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, "_BumpMap");

            fixture.AnimateScalar(fixture.Root, "_BumpMapUV");
            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.AnimatedRouteProperty,
                "_BumpMapUV");
        }

        // --- Falsifier 6: PropertyBlockOnSelectorPropertyRefuses -----
        /// <summary>
        /// A renderer property block that sets the route condition
        /// <c>_BumpMapUV</c> refuses while the texture is assigned.
        /// </summary>
        [Test]
        public void PropertyBlockOnSelectorPropertyRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_BumpMap", fixture.NewRouteTexture(16));
            material.SetFloat("_BumpMapUV", 0f);

            // No-op guard: the admitted twin first. No block admits
            // and the texture moves into a page.
            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, "_BumpMap");

            var renderer = fixture.Root
                .GetComponentInChildren<SkinnedMeshRenderer>();
            var block = new MaterialPropertyBlock();
            block.SetFloat("_BumpMapUV", 1f);
            renderer.SetPropertyBlock(block);

            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.PropertyBlockOnRouteProperty,
                "_BumpMapUV");
        }

        // --- Falsifier 7: NonIdentityTransformOrPanRefuses -----------
        /// <summary>
        /// A moved companion texture must hold its identity transform
        /// and its zero pan. Any other value refuses.
        /// </summary>
        [Test]
        public void NonIdentityTransformOrPanRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_BumpMap", fixture.NewRouteTexture(16));
            material.SetFloat("_BumpMapUV", 0f);

            // No-op guard: the admitted twin first. The identity
            // transform and the zero pan admit.
            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, "_BumpMap");

            material.SetVector(
                "_BumpMap_ST", new Vector4(2f, 2f, 0f, 0f));
            var scaled = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                scaled,
                AtlasUseRefusal.NonIdentityTransform,
                "_BumpMap_ST");

            material.SetVector(
                "_BumpMap_ST", new Vector4(1f, 1f, 0f, 0f));
            material.SetVector(
                "_BumpMapPan", new Vector4(0.25f, 0f, 0f, 0f));
            var panned = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                panned,
                AtlasUseRefusal.NonIdentityTransform,
                "_BumpMapPan");
        }

        // --- Falsifier 8: PolarSelectorsRefuse -----------------------
        /// <summary>
        /// The selectors 6 and 7 build their coordinates from UV0
        /// data. The capture refuses them with
        /// <c>RequiredValueNotHeld</c> and names the selector
        /// property.
        /// </summary>
        [Test]
        public void PolarSelectorsRefuse(
            [ValueSource(nameof(PolarSelectors))] int selector,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_BumpMap", fixture.NewRouteTexture(16));
            material.SetFloat("_BumpMapUV", 0f);

            // No-op guard: the admitted twin first. Selector 0 admits
            // and the texture moves into a page.
            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, "_BumpMap");

            material.SetFloat("_BumpMapUV", selector);
            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "_BumpMapUV");
        }

        // --- Falsifier 9: StochasticFlagRefuses ----------------------
        /// <summary>
        /// The record names <c>_BumpMapStochastic</c> for the bump
        /// map. A flag of 1 moves the coordinate, so the capture
        /// refuses it on a UV0 use.
        /// </summary>
        [Test]
        public void StochasticFlagRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_BumpMap", fixture.NewRouteTexture(16));
            material.SetFloat("_BumpMapUV", 0f);
            material.SetFloat("_BumpMapStochastic", 0f);

            // No-op guard: the admitted twin first. The flag at 0
            // admits and the texture moves into a page.
            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, "_BumpMap");

            material.SetFloat("_BumpMapStochastic", 1f);
            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "_BumpMapStochastic");
        }

        /// <summary>
        /// The outline mask reads <c>sampler_linear_repeat</c> at
        /// 18637. The capture must record the linear repeat sampler
        /// facts for its page, not the sampler state of the source
        /// texture. The case twists the source sampler to point and
        /// clamp, so a member sampler mislabel fails the test.
        /// </summary>
        [Test]
        public void OutlineMaskCapturesWithTheLinearRepeatSamplerFacts(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            var mask = fixture.NewRouteTexture(16);
            mask.filterMode = FilterMode.Point;
            mask.wrapModeU = UnityEngine.TextureWrapMode.Clamp;
            material.SetTexture("_OutlineMask", mask);
            material.SetFloat("_OutlineMaskUV", 0f);

            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_OutlineMask");
            var companion = outcome.Use.Companions
                .Single(entry => entry.Property == "_OutlineMask");
            Assert.That(
                companion.Sampler,
                Is.EqualTo(AtlasUseCapture.InlineLinearRepeat),
                "the outline mask page must carry the linear" +
                " repeat sampler facts of the record");
        }

        /// <summary>
        /// The case material of one row. The main texture is an
        /// imported asset, so the admitted use passes the source
        /// checks. Every texture of a case imports before the case
        /// sets keyword state.
        /// </summary>
        private static Material NewCaseMaterial(
            PoiyomiReviewedFixture fixture, bool twoPass)
        {
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture(
                "_MainTex", fixture.NewRouteTexture(64));
            return material;
        }
    }
}
