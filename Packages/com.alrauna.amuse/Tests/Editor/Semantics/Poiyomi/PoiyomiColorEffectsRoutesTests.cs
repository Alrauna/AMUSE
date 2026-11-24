using System;
using System.Linq;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The capture tests of the feature group 'Color, effects, and
    /// UV' of the reviewed Poiyomi 9.3 route. The review record of
    /// 2026-11-23, sections 4.1, 4.3, 5, and 6, carries the rows of
    /// this group. Every test runs the installed bundled Poiyomi 9.3
    /// shaders through their pinned GUIDs on both rows. A wrong route
    /// fact refuses against the vendor shader.
    /// <para>
    /// The falsifiers follow the record. Falsifiers 1 to 16 cover the
    /// table rules of record section 5 for this group. Falsifier 17
    /// covers the default refusal note of spec section 7.2. Falsifier
    /// 18 is the admitted default of the clamp sampled mask. Each
    /// falsifier asserts its admitted twin first.
    /// </para>
    /// <para>
    /// Six features of the group carry no moving companion texture.
    /// Their admission tests cannot name a companion, so they assert
    /// admission, and their falsifiers carry the refusal proof of the
    /// feature. These are the UV tile discard, the audio link, the
    /// audio link volume color, the text overlay, the two stochastic
    /// modes, and the parallax, whose two textures never move.
    /// </para>
    /// </summary>
    public sealed class PoiyomiColorEffectsRoutesTests
    {
        private static readonly string[] TextSelectorProperties =
        {
            "_TextFPSUV",
            "_TextPositionUV",
            "_TextTimeUV",
            "_TextNumericUV",
        };

        private static readonly string[] ParallaxChannelProperties =
        {
            "_ParallaxUV",
            "_HeightMapUV",
            "_HeightmaskUV",
        };

        private static readonly string[] AudioUvFlagProperties =
        {
            "_AudioLinkCCStripY",
            "_DebugWaveform",
            "_DebugDFT",
            "_DebugCCStrip",
            "_DebugCCLights",
            "_DebugAutocorrelator",
        };

        private static readonly string[] AlVolumeColorSelectors =
        {
            "_ALVolumeColorUV",
            "_ALVolumeColorDirection",
        };

        private static readonly string[] VignettePropKeywords =
        {
            "PROP_RGBMASK",
            "PROP_RGBAMETALLICMAPS",
            "PROP_RGBASMOOTHNESSMAPS",
            "PROP_REDTEXTURE",
            "PROP_RGBNORMALR",
            "PROP_GREENTEXTURE",
            "PROP_RGBNORMALG",
            "PROP_BLUETEXTURE",
            "PROP_ALPHATEXTURE",
            "PROP_RGBNORMALA",
        };

        private static readonly string[] RgbaChannelPrefixes =
        {
            "Red",
            "Green",
            "Blue",
            "Alpha",
        };

        // --- the case material ----------------------------------------

        /// <summary>
        /// One material of the row with the member texture assigned.
        /// The capture needs the member texture on every admitted use,
        /// so every capture test starts here.
        /// </summary>
        private static Material NewCaseMaterial(
            PoiyomiReviewedFixture fixture, bool twoPass)
        {
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            return material;
        }

        // --- the inventory --------------------------------------------

        /// <summary>
        /// The group data of the route table must match the review
        /// record. Every keyword of the group sits on the allowed
        /// list and no other keyword joins it. The record classes no
        /// neutral texture for this group. The route set must equal
        /// the companion rows of the group in the record, with the
        /// verified sampler and the stochastic condition of each row.
        /// </summary>
        [Test]
        public void GroupInventoryIsRouted()
        {
            var groupKeywords = PoiyomiReviewedInventory.Keywords
                .Where(row => row.Group == "Color effects UV")
                .Select(row => row.Keyword)
                .ToList();
            Assert.That(
                groupKeywords, Is.Not.Empty,
                "fixture precondition: the record lists keywords" +
                " for the group");
            foreach (var keyword in groupKeywords)
            {
                Assert.That(
                    PoiyomiColorEffectsRoutes.AllowedKeywords,
                    Does.Contain(keyword),
                    "the allowed list must hold the group keyword '" +
                    keyword + "'");
            }

            Assert.That(
                PoiyomiColorEffectsRoutes.AllowedKeywords,
                Has.Length.EqualTo(groupKeywords.Count),
                "the allowed list must hold no keyword outside the" +
                " group");
            Assert.That(
                PoiyomiColorEffectsRoutes.NeutralTextures,
                Is.Empty,
                "the record classes no neutral texture for this" +
                " group");

            PoiyomiReviewedInventory.AssertRoutesMatchInventory(
                "Color effects UV",
                PoiyomiColorEffectsRoutes.Routes());
        }

        // --- the admitted features ------------------------------------

        /// <summary>
        /// The color adjust texture of the hue shift feature moves
        /// into a companion page at selector 0. The texture import
        /// runs before the keyword turns on.
        /// </summary>
        [Test]
        public void ColorGradingHdrIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_MainColorAdjustTexture", fixture.NewRouteTexture(64));
            material.EnableKeyword("COLOR_GRADING_HDR");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_MainColorAdjustTexture");
        }

        /// <summary>
        /// The distortion feature of the post process block admits
        /// with its target off UV0. Its three textures move into
        /// companion pages at selector 0. The table rule needs the
        /// distortion target 1, 2, or 3, so the twin sets 1.
        /// </summary>
        [Test]
        public void UserLutIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_DistortionFlowTexture", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_DistortionFlowTexture1", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_DistortionMask", fixture.NewRouteTexture(64));
            // The vendor default of the flow pans animates the flow
            // sample. The route needs the zero pan, so the tests pin it.
            material.SetVector("_DistortionFlowTexturePan", Vector4.zero);
            material.SetVector(
                "_DistortionFlowTexture1Pan", Vector4.zero);
            material.SetFloat("_DistortionUvToDistort", 1f);
            material.EnableKeyword("USER_LUT");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_DistortionFlowTexture",
                "_DistortionFlowTexture1",
                "_DistortionMask");
            var route = RouteOf("_DistortionMask");
            Assert.That(
                route.ConditionProperties,
                Does.Contain("_DistortionMaskUV"),
                "the distortion mask route reads its selector");
            Assert.That(
                route.ConditionProperties,
                Does.Contain("_DistortionMaskPan"),
                "the distortion mask route reads its pan");
            Assert.That(
                route.ConditionProperties,
                Does.Not.Contain("_DistortionMask_ST"),
                "the distortion mask has no transform property");
        }

        /// <summary>
        /// The UV tile discard feature carries no texture. The
        /// keyword with all row flags at zero admits. The feature has
        /// no companion, so the test asserts admission only. The
        /// falsifier of the row flag rule carries the refusal proof.
        /// </summary>
        [Test]
        public void PoiUdimDiscardIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.EnableKeyword("POI_UDIMDISCARD");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
        }

        /// <summary>
        /// The parallax feature admits with its three channels off
        /// UV0. Its two textures never move. They stay bound and
        /// keyed, so no companion joins the page.
        /// </summary>
        [Test]
        public void PoiParallaxIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_HeightMap", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_Heightmask", fixture.NewRouteTexture(64));
            material.SetFloat("_ParallaxUV", 1f);
            material.SetFloat("_HeightMapUV", 1f);
            material.SetFloat("_HeightmaskUV", 1f);
            material.EnableKeyword("POI_PARALLAX");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
            var moved = outcome.Use.Companions
                .Select(companion => companion.Property)
                .ToList();
            Assert.That(
                moved, Does.Not.Contain("_HeightMap"),
                "the height map never moves. It stays bound");
            Assert.That(
                moved, Does.Not.Contain("_Heightmask"),
                "the height mask never moves. It stays bound");
        }

        /// <summary>
        /// The audio link feature admits at its safe defaults. All
        /// UV debug flags and the CC strip Y read zero. The feature
        /// carries no companion texture of this group.
        /// </summary>
        [Test]
        public void PoiAudioLinkIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.EnableKeyword("POI_AUDIOLINK");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
        }

        /// <summary>
        /// The detail layer textures of the final pass move into
        /// companion pages at selector 0.
        /// </summary>
        [Test]
        public void FinalPassIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_DetailTex", fixture.NewRouteTexture(64));
            material.SetTexture("_DetailMask", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_DetailNormalMap", fixture.NewRouteTexture(64));
            material.EnableKeyword("FINALPASS");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_DetailTex", "_DetailMask", "_DetailNormalMap");
        }

        /// <summary>
        /// The vertex basics mask and the vertex manipulation height
        /// mask of the vertex effects move into companion pages at
        /// selector 0. The selector of the basics mask has no leading
        /// underscore. The wind and the spectrum motion stay off.
        /// </summary>
        [Test]
        public void AutoExposureIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_VertexBasicsMask", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_VertexManipulationHeightMask",
                fixture.NewRouteTexture(64));
            material.SetFloat("VertexBasicsMaskUV", 0f);
            material.SetFloat("_VertexManipulationHeightMaskUV", 0f);
            material.SetFloat("_VertexWindEnabled", 0f);
            material.SetFloat("_VertexSpectrumMotion", 0f);
            material.EnableKeyword("AUTO_EXPOSURE");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_VertexBasicsMask", "_VertexManipulationHeightMask");
            var route = RouteOf("_VertexBasicsMask");
            Assert.That(
                route.ConditionProperties,
                Does.Contain("VertexBasicsMaskUV"),
                "the basics mask selector has no leading underscore");
            Assert.That(
                route.ConditionProperties,
                Does.Contain("_VertexBasicsMaskUVPan"),
                "the basics mask pan property breaks the pattern");
            Assert.That(
                route.ConditionProperties,
                Does.Not.Contain("_VertexBasicsMaskPan"),
                "no property of that name exists");
        }

        /// <summary>
        /// The look at mask of the vertex look at feature moves into
        /// a companion page at selector 0.
        /// </summary>
        [Test]
        public void PoiVertexLookAtIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_LookAtMask", fixture.NewRouteTexture(64));
            material.EnableKeyword("POI_VERTEX_LOOKAT");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_LookAtMask");
        }

        /// <summary>
        /// The depth bulge mask joins the page at selector 0. The
        /// sample has no pan property, so the route needs only the
        /// identity transform.
        /// </summary>
        [Test]
        public void PoiDepthBulgeIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_DepthBulgeMask", fixture.NewRouteTexture(64));
            material.SetFloat("_DepthBulgeMaskUV", 0f);
            material.EnableKeyword("POI_DEPTHBULGE");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_DepthBulgeMask");
            var route = RouteOf("_DepthBulgeMask");
            Assert.That(
                route.ConditionProperties,
                Does.Contain("_DepthBulgeMaskUV"),
                "the depth bulge mask route reads its selector");
            Assert.That(
                route.ConditionProperties,
                Does.Not.Contain("_DepthBulgeMaskPan"),
                "no pan property exists for the depth bulge mask");
        }

        /// <summary>
        /// The back face texture and the back face mask move into
        /// companion pages at selector 0.
        /// </summary>
        [Test]
        public void PoiBackFaceIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_BackFaceTexture", fixture.NewRouteTexture(64));
            material.SetTexture("_BackFaceMask", fixture.NewRouteTexture(64));
            material.EnableKeyword("POI_BACKFACE");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_BackFaceTexture", "_BackFaceMask");
        }

        /// <summary>
        /// The color and alpha feature moves its mask, its two packed
        /// maps, its four channel textures, and its four channel
        /// normal maps into companion pages at selector 0. The Thry
        /// editor keeps the texture keywords on next to the feature
        /// keyword, so the twin holds that state. The presence
        /// keywords never gate a route. The feature keyword does.
        /// </summary>
        [Test]
        public void VignetteIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            var textures = new[]
            {
                "_RGBMask",
                "_RGBAMetallicMaps",
                "_RGBASmoothnessMaps",
                "_RedTexture",
                "_RgbNormalR",
                "_GreenTexture",
                "_RgbNormalG",
                "_BlueTexture",
                "_RgbNormalB",
                "_AlphaTexture",
                "_RgbNormalA",
            };
            foreach (var texture in textures)
            {
                material.SetTexture(texture, fixture.NewRouteTexture(64));
            }

            material.EnableKeyword("VIGNETTE");
            foreach (var keyword in VignettePropKeywords)
            {
                material.EnableKeyword(keyword);
            }

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, textures);
        }

        /// <summary>
        /// The RGB mask moves into a companion page under its feature
        /// keyword. The presence keyword rides along, because the
        /// Thry editor keeps it in step with the texture.
        /// </summary>
        [Test]
        public void PropRgbMaskIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_RGBMask", fixture.NewRouteTexture(64));
            material.EnableKeyword("VIGNETTE");
            material.EnableKeyword("PROP_RGBMASK");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_RGBMask");
        }

        /// <summary>
        /// The packed metallic map moves into a companion page. The
        /// split per channel samples read the same texture. Their
        /// selectors, merged transforms, pans, and stochastic flags
        /// are route conditions. The twin pins them on the route.
        /// </summary>
        [Test]
        public void PropRgbaMetallicMapsIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_RGBAMetallicMaps", fixture.NewRouteTexture(64));
            material.EnableKeyword("VIGNETTE");
            material.EnableKeyword("PROP_RGBAMETALLICMAPS");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_RGBAMetallicMaps");
            AssertSplitSampleConditionsHold("_RGBAMetallicMaps");
        }

        /// <summary>
        /// The packed smoothness map moves into a companion page. Its
        /// split per channel samples hold the same route conditions
        /// as the metallic map.
        /// </summary>
        [Test]
        public void PropRgbaSmoothnessMapsIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_RGBASmoothnessMaps", fixture.NewRouteTexture(64));
            material.EnableKeyword("VIGNETTE");
            material.EnableKeyword("PROP_RGBASMOOTHNESSMAPS");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_RGBASmoothnessMaps");
            AssertSplitSampleConditionsHold("_RGBASmoothnessMaps");
        }

        /// <summary>
        /// The red channel texture moves into a companion page. The
        /// merged transform of the red channel must also hold
        /// identity, so the route demands it.
        /// </summary>
        [Test]
        public void PropRedTextureIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_RedTexture", fixture.NewRouteTexture(64));
            material.EnableKeyword("VIGNETTE");
            material.EnableKeyword("PROP_REDTEXTURE");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_RedTexture");
            Assert.That(
                RouteOf("_RedTexture").TransformProperties,
                Does.Contain(("_RGBARedScaleOffset", new Vector4(1f, 1f, 0f, 0f))),
                "the merged red transform must hold identity");
        }

        /// <summary>
        /// The red channel normal map moves into a companion page.
        /// Its merged transform joins the route.
        /// </summary>
        [Test]
        public void PropRgbNormalRIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_RgbNormalR", fixture.NewRouteTexture(64));
            material.EnableKeyword("VIGNETTE");
            material.EnableKeyword("PROP_RGBNORMALR");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_RgbNormalR");
            Assert.That(
                RouteOf("_RgbNormalR").TransformProperties,
                Does.Contain(("_RGBARedScaleOffset", new Vector4(1f, 1f, 0f, 0f))),
                "the merged red transform must hold identity");
        }

        /// <summary>
        /// The green channel texture moves into a companion page. Its
        /// merged transform joins the route.
        /// </summary>
        [Test]
        public void PropGreenTextureIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_GreenTexture", fixture.NewRouteTexture(64));
            material.EnableKeyword("VIGNETTE");
            material.EnableKeyword("PROP_GREENTEXTURE");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_GreenTexture");
            Assert.That(
                RouteOf("_GreenTexture").TransformProperties,
                Does.Contain(("_RGBAGreenScaleOffset", new Vector4(1f, 1f, 0f, 0f))),
                "the merged green transform must hold identity");
        }

        /// <summary>
        /// The green channel normal map moves into a companion page.
        /// Its merged transform joins the route.
        /// </summary>
        [Test]
        public void PropRgbNormalGIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_RgbNormalG", fixture.NewRouteTexture(64));
            material.EnableKeyword("VIGNETTE");
            material.EnableKeyword("PROP_RGBNORMALG");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_RgbNormalG");
            Assert.That(
                RouteOf("_RgbNormalG").TransformProperties,
                Does.Contain(("_RGBAGreenScaleOffset", new Vector4(1f, 1f, 0f, 0f))),
                "the merged green transform must hold identity");
        }

        /// <summary>
        /// The blue channel texture moves into a companion page. Its
        /// merged transform joins the route.
        /// </summary>
        [Test]
        public void PropBlueTextureIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_BlueTexture", fixture.NewRouteTexture(64));
            material.EnableKeyword("VIGNETTE");
            material.EnableKeyword("PROP_BLUETEXTURE");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_BlueTexture");
            Assert.That(
                RouteOf("_BlueTexture").TransformProperties,
                Does.Contain(("_RGBABlueScaleOffset", new Vector4(1f, 1f, 0f, 0f))),
                "the merged blue transform must hold identity");
        }

        /// <summary>
        /// The blue channel normal map moves into a companion page.
        /// </summary>
        [Test]
        public void PropRgbNormalBIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_RgbNormalB", fixture.NewRouteTexture(64));
            material.EnableKeyword("VIGNETTE");
            material.EnableKeyword("PROP_RGBNORMALB");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_RgbNormalB");
            Assert.That(
                RouteOf("_RgbNormalB").TransformProperties,
                Does.Contain(("_RGBABlueScaleOffset", new Vector4(1f, 1f, 0f, 0f))),
                "the merged blue transform must hold identity");
        }

        /// <summary>
        /// The alpha channel texture moves into a companion page. Its
        /// merged transform joins the route.
        /// </summary>
        [Test]
        public void PropAlphaTextureIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_AlphaTexture", fixture.NewRouteTexture(64));
            material.EnableKeyword("VIGNETTE");
            material.EnableKeyword("PROP_ALPHATEXTURE");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_AlphaTexture");
            Assert.That(
                RouteOf("_AlphaTexture").TransformProperties,
                Does.Contain(("_RGBAAlphaScaleOffset", new Vector4(1f, 1f, 0f, 0f))),
                "the merged alpha transform must hold identity");
        }

        /// <summary>
        /// The alpha channel normal map moves into a companion page.
        /// </summary>
        [Test]
        public void PropRgbNormalAIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_RgbNormalA", fixture.NewRouteTexture(64));
            material.EnableKeyword("VIGNETTE");
            material.EnableKeyword("PROP_RGBNORMALA");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_RgbNormalA");
            Assert.That(
                RouteOf("_RgbNormalA").TransformProperties,
                Does.Contain(("_RGBAAlphaScaleOffset", new Vector4(1f, 1f, 0f, 0f))),
                "the merged alpha transform must hold identity");
        }

        /// <summary>
        /// The four dissolve textures move into companion pages at
        /// selector 0. The UV tile dissolve stays off at its default.
        /// The pan of every dissolve texture must stay zero, because
        /// it advances with time.
        /// </summary>
        [Test]
        public void DistortIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_DissolveToTexture", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_DissolveNoiseTexture", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_DissolveDetailNoise", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_DissolveMask", fixture.NewRouteTexture(64));
            material.EnableKeyword("DISTORT");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_DissolveToTexture",
                "_DissolveNoiseTexture",
                "_DissolveDetailNoise",
                "_DissolveMask");
        }

        /// <summary>
        /// The audio link decal color mask moves into a companion
        /// page at selector 0. The decal channel rule needs the
        /// decal selector off UV0, so the twin sets 1.
        /// </summary>
        [Test]
        public void PoiAlDecalIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_ALDecalColorMask", fixture.NewRouteTexture(64));
            material.SetFloat("_ALDecalUV", 1f);
            material.EnableKeyword("POI_AL_DECAL");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_ALDecalColorMask");
        }

        /// <summary>
        /// The audio link volume color feature carries no companion
        /// texture. It admits with both selectors off UV0.
        /// </summary>
        [Test]
        public void PoiAlVolumeColorIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetFloat("_ALVolumeColorUV", 1f);
            material.SetFloat("_ALVolumeColorDirection", 1f);
            material.EnableKeyword("POI_AL_VOLUMECOLOR");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
        }

        /// <summary>
        /// The pathing map and the pathing color map move into
        /// companion pages at selector 0. The point sampling flag
        /// holds its default 1, so the admitted sampler is the point
        /// repeat one.
        /// </summary>
        [Test]
        public void PoiPathingIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_PathingMap", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_PathingColorMap", fixture.NewRouteTexture(64));
            material.SetFloat("_PathPointSampling", 1f);
            material.EnableKeyword("POI_PATHING");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_PathingMap", "_PathingColorMap");
            Assert.That(
                RouteOf("_PathingMap").Sampler,
                Is.EqualTo(AtlasCompanionSampler.InlinePointRepeat),
                "the admitted pathing map sampler is the point" +
                " repeat one");
        }

        /// <summary>
        /// The mirror texture moves into a companion page at
        /// selector 0.
        /// </summary>
        [Test]
        public void PoiMirrorTextureIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_MirrorTexture", fixture.NewRouteTexture(64));
            material.EnableKeyword("POI_MIRROR_TEXTURE");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_MirrorTexture");
        }

        /// <summary>
        /// The depth mask and the depth texture of the grain feature
        /// move into companion pages at selector 0. The depth texture
        /// admits selector 0 and selector 8.
        /// </summary>
        [Test]
        public void GrainIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_DepthMask", fixture.NewRouteTexture(64));
            material.SetTexture("_DepthTexture", fixture.NewRouteTexture(64));
            material.EnableKeyword("GRAIN");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_DepthMask", "_DepthTexture");
        }

        /// <summary>
        /// The text overlay feature carries no companion texture. It
        /// admits when all four sub overlay selectors sit off UV0.
        /// </summary>
        [Test]
        public void EffectBumpIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            foreach (var selector in TextSelectorProperties)
            {
                material.SetFloat(selector, 1f);
            }

            material.EnableKeyword("EFFECT_BUMP");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
        }

        /// <summary>
        /// The post process mask moves into a companion page under
        /// the post process feature keyword.
        /// </summary>
        [Test]
        public void PostProcessIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_PPMask", fixture.NewRouteTexture(64));
            material.EnableKeyword("POSTPROCESS");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_PPMask");
        }

        /// <summary>
        /// The presence keyword of the post process mask never gates
        /// the route. The feature keyword turns the page. The twin
        /// holds both keywords, because the Thry editor keeps the
        /// presence keyword in step with the texture.
        /// </summary>
        [Test]
        public void PropPpMaskIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_PPMask", fixture.NewRouteTexture(64));
            material.EnableKeyword("POSTPROCESS");
            material.EnableKeyword("PROP_PPMASK");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_PPMask");
        }

        /// <summary>
        /// The video mask texture moves into a companion page at
        /// selector 0. The video modes hold their safe defaults.
        /// </summary>
        [Test]
        public void PoiVideoEffectsIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_VideoMaskTexture", fixture.NewRouteTexture(64));
            material.EnableKeyword("POI_VIDEO_EFFECTS");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_VideoMaskTexture");
        }

        /// <summary>
        /// The Deliot Heitz stochastic mode keyword admits. A fresh
        /// material carries it already. No companion texture belongs
        /// to it.
        /// </summary>
        [Test]
        public void StochasticModeDeliotHeitzIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.EnableKeyword("_STOCHASTICMODE_DELIOT_HEITZ");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
        }

        /// <summary>
        /// The hex tile stochastic mode keyword admits next to the
        /// stale enum keyword of a fresh material. The stale enum
        /// state keeps the union of the conditions.
        /// </summary>
        [Test]
        public void StochasticModeHextileIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.EnableKeyword("_STOCHASTICMODE_HEXTILE");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
        }

        /// <summary>
        /// The voronoi mask and the voronoi noise move into companion
        /// pages at selector 0. The voronoi space stays local.
        /// </summary>
        [Test]
        public void PoiVoronoiIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_VoronoiMask", fixture.NewRouteTexture(64));
            material.SetTexture("_VoronoiNoise", fixture.NewRouteTexture(64));
            material.EnableKeyword("POI_VORONOI");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_VoronoiMask", "_VoronoiNoise");
        }

        /// <summary>
        /// The truchet mask moves into a companion page at selector
        /// 0. The truchet texture itself never moves. It samples the
        /// pattern coordinate with explicit gradients, so it stays
        /// bound in every admitted use.
        /// </summary>
        [Test]
        public void PoiTruchetIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_TruchetTex", fixture.NewRouteTexture(64));
            material.SetTexture("_TruchetMask", fixture.NewRouteTexture(64));
            material.SetFloat("_TruchetUV", 1f);
            material.EnableKeyword("POI_TRUCHET");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_TruchetMask");
            Assert.That(
                RouteOf("_TruchetTex").UseOf(material, false),
                Is.EqualTo(AtlasRouteUse.OtherCoordinates),
                "the truchet texture never moves. It stays bound");
            var moved = outcome.Use.Companions
                .Select(companion => companion.Property)
                .ToList();
            Assert.That(
                moved, Does.Not.Contain("_TruchetTex"),
                "the truchet texture never joins a page");
        }

        // --- Falsifier 18: UzumoreMaskAtDefaultSelectorIsAdmitted -----
        // The test doubles as the admission test of POI_UZUMORE. The
        // clamp sampler serves the mask only off UV0. The property
        // doubles as the channel index. The default 3 reads UV3 and
        // channel A, so the default state admits with the texture
        // bound.
        /// <summary>
        /// The push mask of the uzumore feature stays bound at its
        /// default selector 3. The clamp sampler refuses UV0, so the
        /// texture never joins a page.
        /// </summary>
        [Test]
        public void UzumoreMaskAtDefaultSelectorIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_UzumoreMask", fixture.NewRouteTexture(64));
            material.SetFloat("_UzumoreMaskUV", 3f);
            material.EnableKeyword("POI_UZUMORE");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
            var moved = outcome.Use.Companions
                .Select(companion => companion.Property)
                .ToList();
            Assert.That(
                moved, Does.Not.Contain("_UzumoreMask"),
                "the clamp sampled mask never joins a page");
        }

        // --- Falsifier 1: DistortionTargetAtZeroRefuses ---------------
        /// <summary>
        /// The distortion target of the post process distortion
        /// writes the coordinate slot 7. Slot 0 is UV0 data, so a
        /// target of 0 refuses the use.
        /// </summary>
        [Test]
        public void DistortionTargetAtZeroRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_DistortionFlowTexture", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_DistortionFlowTexture1", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_DistortionMask", fixture.NewRouteTexture(64));
            material.SetVector("_DistortionFlowTexturePan", Vector4.zero);
            material.SetVector(
                "_DistortionFlowTexture1Pan", Vector4.zero);
            material.EnableKeyword("USER_LUT");

            material.SetFloat("_DistortionUvToDistort", 1f);
            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin,
                "_DistortionFlowTexture",
                "_DistortionFlowTexture1",
                "_DistortionMask");

            material.SetFloat("_DistortionUvToDistort", 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_DistortionUvToDistort'");
        }

        // --- Falsifier 2: DissolveUvTileOnUv0Refuses ------------------
        /// <summary>
        /// The UV tile dissolve builds its coordinate from the mesh
        /// UV of the selected channel. A tile grid on UV0 refuses.
        /// The tile dissolve on another channel admits.
        /// </summary>
        [Test]
        public void DissolveUvTileOnUv0Refuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_DissolveToTexture", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_DissolveNoiseTexture", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_DissolveDetailNoise", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_DissolveMask", fixture.NewRouteTexture(64));
            material.EnableKeyword("DISTORT");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin,
                "_DissolveToTexture",
                "_DissolveNoiseTexture",
                "_DissolveDetailNoise",
                "_DissolveMask");

            material.SetFloat("_UVTileDissolveEnabled", 1f);
            material.SetFloat("_UVTileDissolveUV", 1f);
            var otherChannel = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(otherChannel);

            material.SetFloat("_UVTileDissolveUV", 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_UVTileDissolveUV'");
        }

        // --- Falsifier 3: TextSelectorAtZeroRefuses -------------------
        /// <summary>
        /// Every text overlay reads its selected mesh UV as data. A
        /// sub overlay selector on UV0 refuses.
        /// </summary>
        [Test]
        public void TextSelectorAtZeroRefuses(
            [ValueSource(nameof(TextSelectorProperties))] string selector,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            foreach (var name in TextSelectorProperties)
            {
                material.SetFloat(name, 1f);
            }

            material.EnableKeyword("EFFECT_BUMP");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(twin);

            material.SetFloat(selector, 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'" + selector + "'");
        }

        // --- Falsifier 4: ParallaxChannelAtZeroRefuses ----------------
        /// <summary>
        /// The parallax marches the height map at the selected
        /// coordinate with explicit gradients. Every channel of the
        /// parallax must sit off UV0. A channel at 0 refuses.
        /// </summary>
        [Test]
        public void ParallaxChannelAtZeroRefuses(
            [ValueSource(nameof(ParallaxChannelProperties))] string channel,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_HeightMap", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_Heightmask", fixture.NewRouteTexture(64));
            foreach (var name in ParallaxChannelProperties)
            {
                material.SetFloat(name, 1f);
            }

            material.EnableKeyword("POI_PARALLAX");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(twin);

            material.SetFloat(channel, 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'" + channel + "'");
        }

        // --- Falsifier 5: TruchetChannelAtDefaultRefuses --------------
        /// <summary>
        /// The truchet pattern builds its coordinate from the selected
        /// mesh UV. The default channel 0 refuses. The falsifier keeps
        /// the truchet mask on UV0, so the twin moves it into a page.
        /// </summary>
        [Test]
        public void TruchetChannelAtDefaultRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_TruchetTex", fixture.NewRouteTexture(64));
            material.SetTexture("_TruchetMask", fixture.NewRouteTexture(64));
            material.SetFloat("_TruchetUV", 1f);
            material.EnableKeyword("POI_TRUCHET");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(twin, "_TruchetMask");

            material.SetFloat("_TruchetUV", 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_TruchetUV'");
        }

        // --- Falsifier 6: VoronoiSpaceUvRefuses -----------------------
        /// <summary>
        /// The voronoi space value 2 reads the mesh UV as data. The
        /// space local or world admits.
        /// </summary>
        [Test]
        public void VoronoiSpaceUvRefuses([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_VoronoiMask", fixture.NewRouteTexture(64));
            material.SetTexture("_VoronoiNoise", fixture.NewRouteTexture(64));
            material.SetFloat("_VoronoiSpace", 0f);
            material.EnableKeyword("POI_VORONOI");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, "_VoronoiMask", "_VoronoiNoise");

            material.SetFloat("_VoronoiSpace", 2f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_VoronoiSpace'");
        }

        // --- Falsifier 7: UdimDiscardRowFlagOnUv0Refuses --------------
        /// <summary>
        /// Each UV tile discard grid discards the mesh UV tiles its
        /// row flags select. A set row flag on UV0 refuses. The test
        /// covers the tile grid and the face discard grid.
        /// </summary>
        [Test]
        public void UdimDiscardRowFlagOnUv0Refuses(
            [Values(0, 1)] int grid,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.EnableKeyword("POI_UDIMDISCARD");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(twin);

            if (grid == 0)
            {
                material.SetFloat("_UDIMDiscardRow2_1", 1f);
                var outcome = fixture.CaptureSingle(material);
                PoiyomiReviewedFixture.AssertRefused(
                    outcome,
                    AtlasUseRefusal.RequiredValueNotHeld,
                    "'_UDIMDiscardRow2_1'");
                return;
            }

            material.SetFloat("_EnableUDIMFaceDiscardOptions", 1f);
            material.SetFloat("_UDIMFaceDiscardRow1_2", 1f);
            var faceOutcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                faceOutcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_UDIMFaceDiscardRow1_2'");
        }

        // --- Falsifier 8: PathingSourceUvOnUv0Refuses -----------------
        /// <summary>
        /// The UV gradient path source reads the selected mesh UV as
        /// data. Source 1 with the selector on UV0 refuses. The tex
        /// gradient source admits.
        /// </summary>
        [Test]
        public void PathingSourceUvOnUv0Refuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_PathingMap", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_PathingColorMap", fixture.NewRouteTexture(64));
            material.SetFloat("_PathPointSampling", 1f);
            material.EnableKeyword("POI_PATHING");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, "_PathingMap", "_PathingColorMap");

            material.SetFloat("_PathSource", 1f);
            material.SetFloat("_PathingUVSelect", 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_PathSource'");
        }

        // --- Falsifier 9: PathingBilinearSamplingRefuses --------------
        /// <summary>
        /// The point sampling flag selects the sampler of the pathing
        /// map. The route records the point repeat facts. A flag of 0
        /// would sample bilinear and refuses on a UV0 use.
        /// </summary>
        [Test]
        public void PathingBilinearSamplingRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_PathingMap", fixture.NewRouteTexture(64));
            material.SetFloat("_PathPointSampling", 1f);
            material.EnableKeyword("POI_PATHING");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(twin, "_PathingMap");

            material.SetFloat("_PathPointSampling", 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_PathPointSampling'");
        }

        // --- Falsifier 10: UzumoreChannelAtZeroRefuses ----------------
        /// <summary>
        /// The clamp sampler of the push mask cannot serve a page. A
        /// channel value of 0 selects UV0 and refuses. The refusal
        /// names the clamp sampler.
        /// </summary>
        [Test]
        public void UzumoreChannelAtZeroRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_UzumoreMask", fixture.NewRouteTexture(64));
            material.SetFloat("_UzumoreMaskUV", 3f);
            material.EnableKeyword("POI_UZUMORE");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(twin);

            material.SetFloat("_UzumoreMaskUV", 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "clamp");
        }

        // --- Falsifier 11: VideoModeBreaksTheUse ----------------------
        /// <summary>
        /// The video effects read the mesh UV as data in three modes.
        /// The pixelate resample, the CRT screen, and the pixel
        /// texture on UV0 refuse.
        /// </summary>
        [Test]
        public void VideoModeBreaksTheUse(
            [Values(0, 1, 2)] int mode,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_VideoMaskTexture", fixture.NewRouteTexture(64));
            material.EnableKeyword("POI_VIDEO_EFFECTS");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, "_VideoMaskTexture");

            if (mode == 0)
            {
                material.SetFloat("_VideoPixelateToResolution", 1f);
                var outcome = fixture.CaptureSingle(material);
                PoiyomiReviewedFixture.AssertRefused(
                    outcome,
                    AtlasUseRefusal.RequiredValueNotHeld,
                    "'_VideoPixelateToResolution'");
                return;
            }

            if (mode == 1)
            {
                material.SetFloat("_VideoType", 2f);
                var outcome = fixture.CaptureSingle(material);
                PoiyomiReviewedFixture.AssertRefused(
                    outcome,
                    AtlasUseRefusal.RequiredValueNotHeld,
                    "'_VideoType'");
                return;
            }

            material.SetTexture(
                "_VideoPixelTexture", fixture.NewRouteTexture(64));
            material.SetFloat("_VideoPixelTextureUV", 0f);
            material.EnableKeyword("POI_VIDEO_EFFECTS");
            var pixelOutcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                pixelOutcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_VideoPixelTextureUV'");
        }

        // --- Falsifier 12: AudioLinkUvDebugFlagRefuses ----------------
        /// <summary>
        /// Every audio link UV debug display reads the mesh UV as
        /// data. A debug flag or the CC strip Y at 1 refuses. The
        /// flags are uniforms without Properties entries, so the test
        /// plants their saved values directly. Planting a saved name
        /// that no row declares logs the missing property error of
        /// the material, and the test expects that log.
        /// </summary>
        [Test]
        public void AudioLinkUvDebugFlagRefuses(
            [ValueSource(nameof(AudioUvFlagProperties))] string flag,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.EnableKeyword("POI_AUDIOLINK");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(twin);

            material.SetFloat(flag, 1f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'" + flag + "'");
        }

        // --- Falsifier 13: AlDecalChannelAtDefaultRefuses -------------
        /// <summary>
        /// The audio link decal reads the selected mesh UV as data.
        /// The default channel 0 refuses. A channel off UV0 admits.
        /// </summary>
        [Test]
        public void AlDecalChannelAtDefaultRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_ALDecalColorMask", fixture.NewRouteTexture(64));
            material.SetFloat("_ALDecalUV", 1f);
            material.EnableKeyword("POI_AL_DECAL");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, "_ALDecalColorMask");

            material.SetFloat("_ALDecalUV", 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_ALDecalUV'");
        }

        // --- Falsifier 14: AlVolumeColorChannelAtDefaultRefuses -------
        /// <summary>
        /// The audio link volume color reads the selected mesh UV as
        /// data. Either selector at 0 refuses.
        /// </summary>
        [Test]
        public void AlVolumeColorChannelAtDefaultRefuses(
            [ValueSource(nameof(AlVolumeColorSelectors))] string selector,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetFloat("_ALVolumeColorUV", 1f);
            material.SetFloat("_ALVolumeColorDirection", 1f);
            material.EnableKeyword("POI_AL_VOLUMECOLOR");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(twin);

            material.SetFloat(selector, 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'" + selector + "'");
        }

        // --- Falsifier 15: VertexSpectrumOnUv0Refuses -----------------
        /// <summary>
        /// The vertex spectrum motion reads the selected mesh UV as
        /// data. Motion on with the selector on UV0 refuses. Motion
        /// off admits.
        /// </summary>
        [Test]
        public void VertexSpectrumOnUv0Refuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetFloat("_VertexSpectrumMotion", 0f);
            material.EnableKeyword("AUTO_EXPOSURE");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(twin);

            material.SetFloat("_VertexSpectrumMotion", 1f);
            material.SetFloat("_VertexSpectrumUV", 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_VertexSpectrumUV'");
        }

        // --- Falsifier 16: VertexWindOnPageMaskRefuses ----------------
        /// <summary>
        /// The vertex wind reads world coordinates. It must stay off
        /// when the vertex basics mask is a companion page. The wind
        /// on with the mask on UV0 refuses.
        /// </summary>
        [Test]
        public void VertexWindOnPageMaskRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture(
                "_VertexBasicsMask", fixture.NewRouteTexture(64));
            material.SetFloat("VertexBasicsMaskUV", 0f);
            material.SetFloat("_VertexWindEnabled", 0f);
            material.EnableKeyword("AUTO_EXPOSURE");

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, "_VertexBasicsMask");

            material.SetFloat("_VertexWindEnabled", 1f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_VertexWindEnabled'");
        }

        // --- Falsifier 17: ParallaxAtItsDefaultSelectorRefuses --------
        /// <summary>
        /// A user who keeps the parallax defaults gets a refusal that
        /// names the selector. All three channels default to 0. The
        /// first failing sentence names the parallax selector.
        /// </summary>
        [Test]
        public void ParallaxAtItsDefaultSelectorRefusesWithItsSelectorName(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCaseMaterial(fixture, twoPass);
            material.SetTexture("_HeightMap", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_Heightmask", fixture.NewRouteTexture(64));
            material.EnableKeyword("POI_PARALLAX");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_ParallaxUV'");
        }

        // --- the route lookups ----------------------------------------

        private static AtlasCompanionRoute RouteOf(string texture)
        {
            return AtlasRouteTable.PoiyomiReviewedMainColor
                .CompanionRoutes
                .Single(route => route.Property == texture);
        }

        /// <summary>
        /// The packed RGBA maps sample their split per channel reads
        /// from the same texture. Each split read adds its selector,
        /// its two merged transform vectors, its pan, and its
        /// stochastic flag to the route. The selectors must sit on
        /// UV0 next to the main selector, so the page serves every
        /// read. The transforms must hold identity.
        /// </summary>
        private static void AssertSplitSampleConditionsHold(
            string texture)
        {
            var route = RouteOf(texture);
            foreach (var prefix in RgbaChannelPrefixes)
            {
                Assert.That(
                    route.ConditionProperties,
                    Does.Contain("_RGBA" + prefix + "PBRUV"),
                    "the split " + prefix +
                    " selector is a route condition");
                Assert.That(
                    route.ConditionProperties,
                    Does.Contain("_RGBA" + prefix + "PBRMasksPan"),
                    "the split " + prefix + " pan is a route" +
                    " condition");
                Assert.That(
                    route.ConditionProperties,
                    Does.Contain(
                        "_RGBA" + prefix + "PBRSplitMaskStochastic"),
                    "the split " + prefix +
                    " stochastic flag is a route condition");
                Assert.That(
                    route.TransformProperties,
                    Does.Contain(
                        ("_RGBA" + prefix + "PBRMaskScaleTiling",
                            new Vector4(1f, 1f, 0f, 0f))),
                    "the split " + prefix +
                    " tiling must hold identity");
                Assert.That(
                    route.TransformProperties,
                    Does.Contain(
                        ("_RGBA" + prefix + "ScaleOffset",
                            new Vector4(1f, 1f, 0f, 0f))),
                    "the split " + prefix +
                    " scale offset must hold identity");
            }
        }
    }
}
