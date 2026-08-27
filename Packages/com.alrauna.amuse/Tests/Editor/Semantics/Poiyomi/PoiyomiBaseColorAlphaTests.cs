using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CoreWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Task 4 BaseColor equation tests. They drive the verified-material seam
    /// with explicit <see cref="ColorSpace.Linear"/> evidence so linear-light
    /// color is exercised without changing the project's Gamma setting. Each
    /// unsupported case pins the primary diagnostic, and each source-writing
    /// group the design names is proven to fail closed at BaseColor.
    /// </summary>
    public sealed class PoiyomiBaseColorTests : PoiyomiFixtureTestBase
    {
        // Every enabled source block the design lists as changing or replacing
        // the main color term, plus the color-theme selector. This is the test's
        // authoritative gate spec: production must refuse each one.
        private static readonly string[] BaseColorFeatureGates =
        {
            "_ColorThemeIndex",
            "_MainColorAdjustToggle",
            "_MainHueShiftToggle",
            "_MainHueALCTEnabled",
            "_DetailEnabled",
            "_MainVertexColoringEnabled",
            "_BackFaceEnabled",
            "_RGBMaskEnabled",
            "_EnableDissolve",
            "_DecalEnabled",
            "_DecalEnabled1",
            "_DecalEnabled2",
            "_DecalEnabled3",
            "_EnableAniso",
            "_MatcapEnable",
            "_Matcap2Enable",
            "_Matcap3Enable",
            "_Matcap4Enable",
            "_CubeMapEnabled",
            "_EnableAudioLink",
            "_EnableFlipbook",
            "_EnableRimLighting",
            "_EnableRim2Lighting",
            "_EnableDepthRimLighting",
            "_EnableEnvironmentalRim",
            "_GlitterEnable",
            "_StylizedSpecular",
            "_EnablePathing",
            "_EnableMirrorOptions",
            "_MirrorTextureEnabled",
            "_TextEnabled",
            "_PoiInternalParallax",
            "_PoiParallax",
            "_VideoEffectsEnable",
            "_EnableTouchGlow",
            "_VoronoiEnabled",
            "_EnableTruchet",
            "_EmissionReplace0",
            "_EmissionReplace1",
            "_EmissionReplace2",
            "_EmissionReplace3",
            "_AlphaPremultiply",
        };

        private static PoiyomiSemanticResult Interpret(
            Material material,
            ColorSpace colorSpace = ColorSpace.Linear)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, colorSpace);
        }

        private static ColorSemanticValue BaseColor(PoiyomiSemanticResult result)
        {
            Assert.That(
                result.Semantics.BaseColor.IsComplete,
                Is.True,
                "BaseColor expected complete.");
            return result.Semantics.BaseColor.GetCompleteValue();
        }

        // --- Constant color (no MainTex) -----------------------------------

        [Test]
        public void MissingMainTex_WhiteColor_IsLinearWhiteConstant()
        {
            var material = NewFixtureMaterial();
            material.SetColor("_Color", Color.white);

            var value = BaseColor(Interpret(material));

            Assert.That(value.Kind, Is.EqualTo(ColorSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(Vector3.one));
        }

        [Test]
        public void MissingMainTex_DistinctColor_IsNumericLinearConstant()
        {
            // Proves a real, per-channel sRGB->linear conversion happened using a
            // nontrivial color whose three channels differ. The expected values
            // are the hardcoded sRGB->linear results (IEC 61966-2-1 curve), NOT
            // derived from Color.linear, so the test pins the actual numbers and
            // would catch identity passthrough or a channel swap:
            //   0.25 -> 0.0509,  0.50 -> 0.2140,  0.75 -> 0.5225.
            var material = NewFixtureMaterial();
            material.SetColor("_Color", new Color(0.25f, 0.5f, 0.75f, 1f));

            var value = BaseColor(Interpret(material));

            Assert.That(value.Kind, Is.EqualTo(ColorSemanticValueKind.Constant));
            var rgb = value.GetConstantValue();
            Assert.That(rgb.x, Is.EqualTo(0.0509f).Within(0.002f));
            Assert.That(rgb.y, Is.EqualTo(0.2140f).Within(0.002f));
            Assert.That(rgb.z, Is.EqualTo(0.5225f).Within(0.002f));
        }

        // --- Texture sample (MainTex assigned) -----------------------------

        [Test]
        public void AssignedMainTex_IdentityTint_IsTextureSample()
        {
            var material = NewFixtureMaterial();
            var texture = ImportTexture("basecolor_identity");
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", Color.white);

            var value = BaseColor(Interpret(material));

            var expected = ColorSemanticValue.Texture(
                new TextureSample(
                    new TextureSourceId(ExpectedToken(texture)),
                    new UvMapping(0, Vector2.one, Vector2.zero),
                    new TextureSampling(
                        TextureFilterMode.Bilinear, CoreWrapMode.Repeat)),
                TextureColorInterpretation.Srgb);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void AssignedMainTex_NonIdentityTint_IsTextureSampleTimesConstant()
        {
            var material = NewFixtureMaterial();
            var texture = ImportTexture("basecolor_tinted");
            material.SetTexture("_MainTex", texture);
            var tint = new Color(0.25f, 0.5f, 0.75f, 1f);
            material.SetColor("_Color", tint);

            var value = BaseColor(Interpret(material));

            var expected = ColorSemanticValue.TextureTimesConstant(
                new TextureSample(
                    new TextureSourceId(ExpectedToken(texture)),
                    new UvMapping(0, Vector2.one, Vector2.zero),
                    new TextureSampling(
                        TextureFilterMode.Bilinear, CoreWrapMode.Repeat)),
                TextureColorInterpretation.Srgb,
                new Vector3(tint.linear.r, tint.linear.g, tint.linear.b));
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void AssignedMainTex_LinearImport_IsLinearInterpretation()
        {
            var material = NewFixtureMaterial();
            var texture = ImportTexture(
                "basecolor_linear", i => i.sRGBTexture = false);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", Color.white);

            var value = BaseColor(Interpret(material));

            Assert.That(
                value.Kind, Is.EqualTo(ColorSemanticValueKind.TextureSample));
            Assert.That(
                value.GetColorInterpretation(),
                Is.EqualTo(TextureColorInterpretation.Linear));
        }

        [Test]
        public void AssignedMainTex_Uv1WithScaleOffset_IsCaptured()
        {
            var material = NewFixtureMaterial();
            var texture = ImportTexture("basecolor_uv1");
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_MainTexUV", 1f);
            material.SetTextureScale("_MainTex", new Vector2(2f, 3f));
            material.SetTextureOffset("_MainTex", new Vector2(0.1f, 0.2f));

            var value = BaseColor(Interpret(material));
            var sample = value.GetTextureSample();

            Assert.That(sample.Coordinates.Channel, Is.EqualTo(1));
            Assert.That(sample.Coordinates.Scale, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(sample.Coordinates.Offset, Is.EqualTo(new Vector2(0.1f, 0.2f)));
        }

        // --- Feature-writer gates ------------------------------------------

        [Test]
        public void BaseColorFeatureWriterEnabled_IsUnsupportedFeature(
            [ValueSource(nameof(BaseColorFeatureGates))] string property)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(property, 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void EmissionSlotEnabled_LeavesBaseColorComplete()
        {
            // An emission slot is not a main-color writer; only its
            // replace-base-color flag competes for BaseColor.
            var material = NewFixtureMaterial();
            material.SetFloat("_EnableEmission1", 1f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.BaseColor);
        }

        // --- UV / sampling-mode / identity / sampler / import gates ---------

        [Test]
        public void AssignedMainTex_UnsupportedUvChannel_IsUnsupportedUv()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("basecolor_uv_bad"));
            material.SetFloat("_MainTexUV", 4f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedUv);
        }

        [Test]
        public void AssignedMainTex_NonZeroPan_IsUnsupportedUv()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("basecolor_pan_bad"));
            material.SetVector("_MainTexPan", new Vector4(0.1f, 0f, 0f, 0f));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedUv);
        }

        [Test]
        public void AssignedMainTex_PixelMode_IsUnsupportedFeature()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("basecolor_pixel"));
            material.SetFloat("_MainPixelMode", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_MainPixelMode");
        }

        [Test]
        public void AssignedMainTex_Stochastic_IsUnsupportedFeature()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("basecolor_stochastic"));
            material.SetFloat("_MainTexStochastic", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_MainTexStochastic");
        }

        [Test]
        public void TransientMainTex_IsUnstableIdentity()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", Track(new Texture2D(4, 4)));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnstableTextureIdentity);
        }

        [Test]
        public void AssignedMainTex_TrilinearSampler_IsUnsupportedSampling()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture(
                "basecolor_trilinear", i => i.filterMode = FilterMode.Trilinear));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedSampling);
        }

        [Test]
        public void AssignedMainTex_NoImporter_IsUnsupportedTextureImport()
        {
            // A native texture asset passes identity and sampler checks but has
            // no TextureImporter, so its color interpretation is unprovable.
            var material = NewFixtureMaterial();
            material.SetTexture(
                "_MainTex", NewNativeTextureAsset("basecolor_native"));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedTextureImport);
        }

        // --- Value / color-space failures ----------------------------------

        [Test]
        public void NonFiniteColor_IsUnsupportedFeature()
        {
            var material = NewFixtureMaterial();
            material.SetColor("_Color", new Color(float.NaN, 0f, 0f, 1f));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_Color");
        }

        [Test]
        public void GammaProject_InvalidatesBaseColorOnly()
        {
            var material = NewFixtureMaterial();
            material.SetColor("_Color", Color.white);

            var result = Interpret(material, ColorSpace.Gamma);

            AssertUnsupportedOutput(
                result,
                PoiyomiSemanticOutput.BaseColor,
                PoiyomiSemanticDiagnosticCode.UnsupportedColorSpace);
            // Alpha is a raw scalar independent of the working color space; the
            // BaseColor invalidation must not touch the proven Alpha output.
            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
        }
    }

    /// <summary>
    /// Task 4 Alpha equation tests. Alpha is a raw scalar, so the working color
    /// space is irrelevant here; both outputs are still interpreted together
    /// through the linear seam. Force-opaque, coverage/clip mechanisms, and the
    /// per-feature alpha writers are each proven to fail closed, and the shared
    /// main sample is proven to need no color-import evidence for alpha.
    /// </summary>
    public sealed class PoiyomiAlphaTests : PoiyomiFixtureTestBase
    {
        // Coverage/clip mechanisms that change effective alpha coverage even
        // when the alpha value itself is forced opaque.
        private static readonly string[] AlphaCoverageGates =
        {
            "_AlphaToCoverage",
            "_AlphaSharpenedA2C",
            "_AlphaDithering",
            "_EnableDissolve",
            "_EnableUDIMDiscardOptions",
        };

        // Enabled writers/masks that modify the non-forced alpha term.
        // _MainAlphaMaskMode is deliberately absent: it is no longer an
        // exact-off gate but an interpreted mode, so PoiyomiAlphaMaskTests owns
        // its supported and refused cases.
        private static readonly string[] AlphaFeatureGates =
        {
            "_AlphaMod",
            "_AlphaDistanceFade",
            "_AlphaFresnel",
            "_AlphaAngular",
            "_AlphaAudioLinkEnabled",
            "_EnableAudioLink",
            "_AlphaGlobalMask",
            "_AlphaPremultiply",
            "_BackFaceEnabled",
            "_RGBMaskEnabled",
            "_DecalEnabled",
            "_DecalEnabled1",
            "_DecalEnabled2",
            "_DecalEnabled3",
            "_EnableFlipbook",
            "_EnableRimLighting",
            "_EnableRim2Lighting",
            "_EnableDepthRimLighting",
            "_EnableEnvironmentalRim",
            "_VideoEffectsEnable",
            "_EnableTouchGlow",
            "_MainVertexColoringEnabled",
        };

        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        private static ScalarSemanticValue Alpha(PoiyomiSemanticResult result)
        {
            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.True,
                "Alpha expected complete.");
            return result.Semantics.Alpha.GetCompleteValue();
        }

        // A material on the non-forced alpha path with the mask mode off, so
        // alpha is proven from _MainTex.a and/or _Color.a.
        private Material NonForcedMaterial()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            return material;
        }

        private static TextureSample MainSample(Texture texture)
        {
            return new TextureSample(
                new TextureSourceId(ExpectedToken(texture)),
                new UvMapping(0, Vector2.one, Vector2.zero),
                new TextureSampling(
                    TextureFilterMode.Bilinear, CoreWrapMode.Repeat));
        }

        // --- Forced opaque -------------------------------------------------

        [Test]
        public void DefaultForceOpaque_IsConstantOne()
        {
            var value = Alpha(Interpret(NewFixtureMaterial()));

            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(1f));
        }

        [Test]
        public void ForceOpaque_OverridesMainTexAndColorAlpha_IsConstantOne()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("alpha_forced"));
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));

            var value = Alpha(Interpret(material));

            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(1f));
        }

        // --- Constant color alpha ------------------------------------------

        [Test]
        public void NonForced_NoMainTex_IsConstantColorAlpha()
        {
            var material = NonForcedMaterial();
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));

            var value = Alpha(Interpret(material));

            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(0.5f));
        }

        [Test]
        public void NonForced_IgnoreMainAlpha_IsConstantColorAlpha()
        {
            var material = NonForcedMaterial();
            material.SetTexture("_MainTex", ImportTexture("alpha_ignore"));
            material.SetFloat("_MainIgnoreTexAlpha", 1f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.25f));

            var value = Alpha(Interpret(material));

            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(0.25f));
        }

        // --- Texture alpha sample ------------------------------------------

        [Test]
        public void NonForced_MainTexFullColorAlpha_IsTextureAlpha()
        {
            var material = NonForcedMaterial();
            var texture = ImportTexture("alpha_texture");
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", Color.white);

            var value = Alpha(Interpret(material));

            Assert.That(
                value,
                Is.EqualTo(ScalarSemanticValue.Texture(
                    MainSample(texture), TextureChannel.Alpha)));
        }

        [Test]
        public void NonForced_MainTexPartialColorAlpha_IsTextureTimesConstant()
        {
            var material = NonForcedMaterial();
            var texture = ImportTexture("alpha_texture_tinted");
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));

            var value = Alpha(Interpret(material));

            Assert.That(
                value,
                Is.EqualTo(ScalarSemanticValue.TextureTimesConstant(
                    MainSample(texture), TextureChannel.Alpha, 0.5f)));
        }

        [Test]
        public void NonForced_MainTexNoImporter_IsCompleteWithoutColorEvidence()
        {
            // Alpha is a raw scalar: unlike BaseColor, a native texture asset
            // with no color-import evidence still yields a complete alpha.
            var material = NonForcedMaterial();
            var texture = NewNativeTextureAsset("alpha_native");
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Color", Color.white);

            var result = Interpret(material);

            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
            Assert.That(
                Alpha(result),
                Is.EqualTo(ScalarSemanticValue.Texture(
                    MainSample(texture), TextureChannel.Alpha)));
        }

        // --- Coverage / feature gates --------------------------------------

        [Test]
        public void AlphaCoverageGateEnabled_IsUnsupportedFeature(
            [ValueSource(nameof(AlphaCoverageGates))] string property)
        {
            // Coverage/clip gates apply even on the force-opaque path.
            var material = NewFixtureMaterial();
            material.SetFloat(property, 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void AlphaFeatureGateEnabled_IsUnsupportedFeature(
            [ValueSource(nameof(AlphaFeatureGates))] string property)
        {
            var material = NonForcedMaterial();
            material.SetFloat(property, 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        // --- Binary-value and sample failures ------------------------------

        [Test]
        public void ForceOpaqueNonBinary_IsUnsupportedFeature()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0.5f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_AlphaForceOpaque");
        }

        [Test]
        public void IgnoreMainAlphaNonBinary_IsUnsupportedFeature()
        {
            var material = NonForcedMaterial();
            material.SetFloat("_MainIgnoreTexAlpha", 0.5f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_MainIgnoreTexAlpha");
        }

        [Test]
        public void NonFiniteColorAlpha_IsUnsupportedFeature()
        {
            var material = NonForcedMaterial();
            material.SetColor("_Color", new Color(1f, 1f, 1f, float.NaN));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_Color");
        }

        [Test]
        public void NonForced_MainTexUnsupportedUv_IsAlphaScopedUv()
        {
            // The shared main sample routes its failure to the Alpha output.
            var material = NonForcedMaterial();
            material.SetTexture("_MainTex", ImportTexture("alpha_uv_bad"));
            material.SetFloat("_MainTexUV", 4f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedUv);
        }

        // --- Independence from render state and other outputs --------------

        [Test]
        public void RenderStateProperties_DoNotChangeAlpha()
        {
            // UI preset, cutoff, and blend factors are not part of the
            // normalized alpha equation and must never gate it.
            var material = NonForcedMaterial();
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            material.SetFloat("_Mode", 3f);
            material.SetFloat("_Cutoff", 0.3f);
            material.SetFloat("_SrcBlend", 5f);
            material.SetFloat("_DstBlend", 10f);

            var value = Alpha(Interpret(material));

            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(0.5f));
        }

        [Test]
        public void BaseColorOnlyWriter_LeavesAlphaComplete()
        {
            // A writer that only invalidates BaseColor must not invalidate the
            // independently proven Alpha output.
            var material = NewFixtureMaterial();
            material.SetFloat("_DetailEnabled", 1f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }
    }
}
