using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CoreWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;
using static Alrauna.Amuse.Tests.Editor.Analysis.AlphaSemanticsResolverTests;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Task 4 Alpha equation tests. Alpha is a raw scalar, so the working color
    /// space is irrelevant here. Force-opaque, coverage/clip mechanisms, and the
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
            "_BSSEnabled",
        };

        // Enabled writers/masks that modify the non-forced alpha term.
        // _MainAlphaMaskMode is deliberately absent: it is no longer an
        // exact-off gate but an interpreted mode, so PoiyomiAlphaMaskTests owns
        // its supported and refused cases. The four decal slots are absent:
        // PoiyomiDecalSlotAlphaTests owns their per-slot inert proof. The two
        // rim enable floats are absent: the rim family's alpha behavior is
        // governed by _RimApplyAlpha alone, so PoiyomiRimLightingAlphaTests
        // owns its contract. Depth rim and environmental rim stay gated.
        private static readonly string[] AlphaFeatureGates =
        {
            "_AlphaMod",
            "_AlphaDistanceFade",
            "_AlphaFresnel",
            "_AlphaAngular",
            "_AlphaAudioLinkEnabled",
            "_EnableAudioLink",
            "_AlphaGlobalMask",
            "_BackFaceEnabled",
            "_RGBMaskEnabled",
            "_EnableFlipbook",
            "_RimApplyAlpha",
            "_EnableDepthRimLighting",
            "_EnableEnvironmentalRim",
            "_VideoEffectsEnable",
            "_EnableTouchGlow",
            "_MainVertexColoringEnabled",
        };

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
        public void NonForcedMainTexNonIdentityStPreservesMappingAndClassifies()
        {
            var material = NonForcedMaterial();
            var texture = ImportTexture("alpha_nonidentity_st");
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", new Vector2(2f, 2f));
            material.SetTextureOffset("_MainTex", new Vector2(0.5f, 0.25f));
            material.SetVector("_MainTexPan", Vector4.zero);
            material.SetFloat("_MainTexUV", 0f);
            material.SetFloat("_MainPixelMode", 0f);
            material.SetFloat("_MainTexStochastic", 0f);
            material.SetFloat("_PoiParallax", 0f);
            material.SetFloat("_PoiInternalParallax", 0f);
            var value = Alpha(Interpret(material));
            var sample = value.GetTextureSample();
            AlphaFieldProvider provider = (TextureSourceId source,
                TextureChannel channel, out AlphaMipChain chain) =>
            {
                var alpha = new byte[8 * 8];
                for (var index = 0; index < alpha.Length; index++)
                {
                    alpha[index] = 255;
                }
                for (var y = 0; y < 8; y++)
                {
                    alpha[y * 8] = 0;
                    alpha[y * 8 + 1] = 0;
                    alpha[y * 8 + 7] = 0;
                }
                chain = new AlphaMipChain(new[]
                {
                    new AlphaTextureData(8, 8, alpha),
                });
                return true;
            };
            var triangle = CornerTriangle(
                new Vector2(0.05f, 0.15f),
                new Vector2(0.1f, 0.15f),
                new Vector2(0.05f, 0.2f));
            var resolution = AlphaSemanticsResolver.Resolve(
                SemanticOutput<ScalarSemanticValue>.Complete(value), provider,
                AlphaSamplingPolicy.AcceptedNonAnisotropic, 0);

            // Falsifies: dropping _MainTex ST after the attested Poiyomi gates.
            Assert.That(sample.Coordinates.Channel, Is.Zero);
            Assert.That(sample.Coordinates.Scale, Is.EqualTo(new Vector2(2f, 2f)));
            Assert.That(
                sample.Coordinates.Offset, Is.EqualTo(new Vector2(0.5f, 0.25f)));
            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(triangle),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
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

        // --- Falsifier 1: enabled module over an exactly-one domain ---------

        /// <summary>
        /// The Beat Saber module toggle turns on a post-clip alpha writer: the
        /// pass rewrites alpha as alpha = alpha * emission.z and rewrites the
        /// alpha blend pair. The color alpha here is exactly one on the whole
        /// domain, so a wrong implementation that ignores the module proves
        /// the base chain on every triangle. The alpha output must stay
        /// unknown and name the module property.
        /// </summary>
        [Test]
        public void BeatSaberModuleEnabled_KeepsAlphaUnknownNamingTheProperty()
        {
            // No-op guard: with the module off the same exactly-one domain
            // proves, so the refusal below comes from the module gate.
            AssertOutputComplete(
                Interpret(NonForcedMaterial()), PoiyomiSemanticOutput.Alpha);

            var material = NonForcedMaterial();
            material.SetFloat("_BSSEnabled", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_BSSEnabled");
        }

        // --- Falsifier 2: enabled module under force-opaque -----------------

        /// <summary>
        /// The coverage gate run precedes the forced-opaque short-circuit, so
        /// an enabled module refuses the forced path too. A wrong
        /// implementation that gates only the non-forced path proves the
        /// forced material as a constant one.
        /// </summary>
        [Test]
        public void BeatSaberModuleEnabled_WithForcedOpaque_StillRefuses()
        {
            // No-op guard: without the module the forced material proves the
            // constant, so the refusal below comes from the module gate.
            AssertOutputComplete(
                Interpret(NewFixtureMaterial()), PoiyomiSemanticOutput.Alpha);

            var material = NewFixtureMaterial();
            material.SetFloat("_BSSEnabled", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_BSSEnabled");
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

        // --- Premultiply admission -----------------------------------------

        /// <summary>
        /// The vendor premultiply feature scales the base color by
        /// saturate(alpha) in three passes and never writes the alpha value
        /// (note 4.3, vendor lines 30216, 47274, 58331). The proof only moves
        /// triangles whose alpha is exactly 1, so the factor is exactly 1 on
        /// the whole proven domain and the feature is an identity there. The
        /// alpha output must complete and carry no premultiply diagnostic. A
        /// wrong implementation that keeps the committed alpha gate refuses
        /// by name.
        /// </summary>
        [Test]
        public void Premultiply_On_WithProvenExactlyOneDomain_CompletesAlpha()
        {
            // No-op guard: without the feature the same exactly-one domain
            // proves. A refusal below can only come from the premultiply
            // gate.
            AssertOutputComplete(
                Interpret(NonForcedMaterial()), PoiyomiSemanticOutput.Alpha);

            var material = NonForcedMaterial();
            material.SetFloat("_AlphaPremultiply", 1f);

            var result = Interpret(material);

            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
            var value = result.Semantics.Alpha.GetCompleteValue();
            Assert.That(
                value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(1f));
        }

        // --- Falsifier 1: sub-one domain under premultiply ------------------

        /// <summary>
        /// Admission rests on the exactly-one premise. A material with
        /// premultiply on and a sub-one color alpha must prove no triangle.
        /// The vendor factor saturate(alpha) is below 1 there, so the alpha
        /// value itself stays sub-one. A wrong implementation that lets a
        /// sub-one region prove by editing the alpha value instead of only
        /// the gate list fails here at the classification seam.
        /// </summary>
        [Test]
        public void Premultiply_On_SubOneDomain_NeverProvesATriangle()
        {
            // No-op guard: without the feature the same sub-one domain stays
            // unproven. The answer below comes from the alpha value alone.
            var ungated = NonForcedMaterial();
            ungated.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            Assert.That(
                ClassifyAlphaTriangle(ungated),
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque));

            var material = NonForcedMaterial();
            material.SetFloat("_AlphaPremultiply", 1f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));

            Assert.That(
                ClassifyAlphaTriangle(material),
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        // Resolves one material's proven alpha through the classification
        // seam the fixture machinery already exposes.
        private static TriangleAlphaOutcome ClassifyAlphaTriangle(
            Material material)
        {
            var result = Interpret(material);
            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);

            var resolution = AlphaSemanticsResolver.Resolve(
                SemanticOutput<ScalarSemanticValue>.Complete(
                    result.Semantics.Alpha.GetCompleteValue()),
                RefusingFieldProvider, AlphaSamplingPolicy.ConservativeDesktop,
                0);
            Assert.That(resolution.IsResolved, Is.True);
            return resolution.Classify(CornerTriangle(
                new Vector2(0.05f, 0.15f),
                new Vector2(0.1f, 0.15f),
                new Vector2(0.05f, 0.2f)));
        }

        // A constant alpha never consults the field provider. A call would
        // mean the proven value is not the expected constant, so refuse.
        private static bool RefusingFieldProvider(
            TextureSourceId source,
            TextureChannel channel,
            out AlphaMipChain chain)
        {
            chain = default;
            return false;
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
        public void NonAlphaWriter_LeavesAlphaComplete()
        {
            // A writer outside the alpha equation must not invalidate the
            // independently proven Alpha output.
            var material = NewFixtureMaterial();
            material.SetFloat("_DetailEnabled", 1f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void GammaColorSpace_AlphaStaysProven()
        {
            // Alpha is a raw scalar independent of the working color space;
            // the color-space fact never touches the proven Alpha output.
            var material = NewFixtureMaterial();
            material.SetColor("_Color", Color.white);

            AssertOutputComplete(
                Interpret(material, ColorSpace.Gamma),
                PoiyomiSemanticOutput.Alpha);
        }
    }
}
