using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The multipass rule (scope decision V2): a triangle moves only if every
    /// pass that draws it is proven opaque on that triangle. The Two Pass
    /// shader's second Base pass reuses the first pass's alpha chain under the
    /// 2-family preset, so the rule lands as a blend-pair gate over both
    /// passes: with alpha proven 1 on the whole domain, the replace, standard
    /// alpha, and premultiply pairs render the source unchanged, while the
    /// additive and multiplicative pairs add to or scale what is behind and
    /// are never visually opaque. These tests pin that gate through the real
    /// capture-and-interpret path on the verified stand-in.
    /// </summary>
    public sealed class PoiyomiMultipassRuleTests : PoiyomiFixtureTestBase
    {
        private SemanticOutput<ScalarSemanticValue> Interpret(
            Material material)
        {
            var inputs = new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    PoiyomiMaterialSemantics.AlphaEvidenceRequest),
            };
            var evidence = UnityMaterialEvidenceCapture.Capture(inputs);
            return PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                evidence[0]);
        }

        /// <summary>
        /// The primary pass's own blend pair is part of the opacity claim.
        /// An additive pair with alpha proven 1 still adds the source to
        /// what is behind, so the claim must refuse instead of completing.
        /// Falsifies claiming opacity from the alpha value alone.
        /// </summary>
        [Test]
        public void AdditivePrimaryBlendRefusesCompleteAlpha()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 1f);
            material.SetFloat("_SrcBlend", 1f);
            material.SetFloat("_DstBlend", 1f);

            Assert.That(
                Interpret(material).IsComplete,
                Is.False,
                "an additive primary blend is never visually opaque");
        }

        /// <summary>
        /// The second pass's blend pair gates exactly like the primary one:
        /// every pass that draws the triangle must be proven. Falsifies a
        /// first-pass-only proof on a material carrying the Two Pass preset
        /// family.
        /// </summary>
        [Test]
        public void AdditiveSecondPassRefusesCompleteAlpha()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 1f);
            material.SetFloat("_SrcBlend2", 1f);
            material.SetFloat("_DstBlend2", 1f);

            Assert.That(
                Interpret(material).IsComplete,
                Is.False,
                "an additive second pass is never visually opaque");
        }

        /// <summary>
        /// The standard alpha pair is provable at alpha 1 on both passes:
        /// SrcAlpha 1 contributes the source and OneMinusSrcAlpha 0 drops
        /// what is behind. The gates must not over-refuse preset-driven
        /// materials.
        /// </summary>
        [Test]
        public void StandardAlphaPairsOnBothPassesStayComplete()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 1f);
            material.SetFloat("_SrcBlend", 5f);
            material.SetFloat("_DstBlend", 10f);
            material.SetFloat("_SrcBlend2", 5f);
            material.SetFloat("_DstBlend2", 10f);

            var alpha = Interpret(material);
            Assert.That(alpha.IsComplete, Is.True);
            Assert.That(
                alpha.GetCompleteValue().GetConstantValue(), Is.EqualTo(1f));
        }

        /// <summary>
        /// The Two Pass shader is a pinned identity in its own right: its
        /// name, GUID, and measured normalized source hash verify against
        /// the pinned 9.3.64 package exactly like the plain Toon shader.
        /// Falsifies refusing the variant on its name after S10 pinned it.
        /// </summary>
        [Test]
        public void TwoPassIdentityVerifiesAgainstItsOwnPins()
        {
            var evidence = new PoiyomiSourceEvidence(
                ".poiyomi/Poiyomi Toon Two Pass",
                false,
                true,
                "eda2412ac7ab2db45a47a521f6d7d8a6",
                "b1d9ecd3072d21db97001dd23f88d089996b809e4039891a05f64d2ffcd4df67",
                true,
                "com.poiyomi.toon",
                "9.3.64",
                true);

            Assert.That(
                PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
                    evidence, out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        /// <summary>
        /// The pins themselves are measured data: the Two Pass constants must
        /// equal the official v9.3.64 values so a silent repin is impossible.
        /// </summary>
        [Test]
        public void TwoPassPins_AreTheMeasuredValues()
        {
            Assert.That(
                PoiyomiMaterialSemantics.PoiyomiTwoPassShaderName,
                Is.EqualTo(".poiyomi/Poiyomi Toon Two Pass"));
            Assert.That(
                PoiyomiMaterialSemantics.TwoPassCanonicalShaderGuid,
                Is.EqualTo("eda2412ac7ab2db45a47a521f6d7d8a6"));
            Assert.That(
                PoiyomiMaterialSemantics
                    .TwoPassCanonicalNormalizedSourceHash,
                Is.EqualTo(
                    "b1d9ecd3072d21db97001dd23f88d089996b809e4039891a05f64d2ffcd4df67"));
        }
    }
}
