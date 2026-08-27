using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Alpha-mask Replace-mode tests. The pinned Poiyomi 9.3.64 mask expression
    /// is
    /// <code>
    /// alphaMask = saturate(mask.r * _AlphaMaskBlendStrength
    ///                      + (_AlphaMaskInvert ? -_AlphaMaskValue : _AlphaMaskValue));
    /// if (_AlphaMaskInvert) alphaMask = 1 - alphaMask;
    /// if (_MainAlphaMaskMode == 1) alpha = alphaMask;   // Replace
    /// </code>
    /// With <c>_AlphaMask</c> unassigned the shader binds its declared "white"
    /// default, so <c>mask.r</c> is exactly one and the expression collapses to a
    /// constant this suite pins exactly. An assigned mask needs a texture-backed
    /// red-channel field AMUSE does not produce, so it stays Unknown.
    /// </summary>
    public sealed class PoiyomiAlphaMaskTests : PoiyomiFixtureTestBase
    {
        private const string MaskMode = "_MainAlphaMaskMode";
        private const string Mask = "_AlphaMask";
        private const string BlendStrength = "_AlphaMaskBlendStrength";
        private const string MaskValue = "_AlphaMaskValue";
        private const string Invert = "_AlphaMaskInvert";
        private const string Parallax = "_PoiParallax";

        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        private static ScalarSemanticValue Alpha(PoiyomiSemanticResult result)
        {
            AssertOutputComplete(result, PoiyomiSemanticOutput.Alpha);
            return result.Semantics.Alpha.GetCompleteValue();
        }

        /// <summary>Non-forced material in Replace mode with no mask assigned.</summary>
        private Material ReplaceMaterial()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat(MaskMode, 1f);
            return material;
        }

        /// <summary>Non-forced material with the mask mode proven off.</summary>
        private Material MaskOffMaterial()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat(MaskMode, 0f);
            return material;
        }

        private static void AssertConstant(
            PoiyomiSemanticResult result, float expected)
        {
            var value = Alpha(result);
            Assert.That(
                value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(expected));
        }

        // --- Replace with no assigned mask: exact constants -----------------

        [Test]
        public void ReplaceNoMask_Defaults_IsConstantOne()
        {
            // saturate(1 * 1 + 0) = 1
            AssertConstant(Interpret(ReplaceMaterial()), 1f);
        }

        [Test]
        public void ReplaceNoMask_RepresentableStrength_IsThatConstant()
        {
            var material = ReplaceMaterial();
            material.SetFloat(BlendStrength, 0.5f);

            // saturate(1 * 0.5 + 0) = 0.5
            AssertConstant(Interpret(material), 0.5f);
        }

        [Test]
        public void ReplaceNoMask_RepresentableValue_IsThatConstant()
        {
            var material = ReplaceMaterial();
            material.SetFloat(BlendStrength, 0.25f);
            material.SetFloat(MaskValue, 0.5f);

            // saturate(1 * 0.25 + 0.5) = 0.75
            AssertConstant(Interpret(material), 0.75f);
        }

        [Test]
        public void ReplaceNoMask_SaturatesAtFloor()
        {
            var material = ReplaceMaterial();
            material.SetFloat(MaskValue, -2f);

            // saturate(1 * 1 + -2) = saturate(-1) = 0
            AssertConstant(Interpret(material), 0f);
        }

        [Test]
        public void ReplaceNoMask_SaturatesAtCeiling()
        {
            var material = ReplaceMaterial();
            material.SetFloat(BlendStrength, 2f);

            // saturate(1 * 2 + 0) = saturate(2) = 1
            AssertConstant(Interpret(material), 1f);
        }

        [Test]
        public void ReplaceNoMask_Inverted_IsInvertedConstant()
        {
            var material = ReplaceMaterial();
            material.SetFloat(BlendStrength, 0.25f);
            material.SetFloat(Invert, 1f);

            // raw = saturate(1 * 0.25 + -0) = 0.25; alpha = 1 - 0.25 = 0.75
            AssertConstant(Interpret(material), 0.75f);
        }

        [Test]
        public void ReplaceNoMask_InvertedNegatesValue()
        {
            var material = ReplaceMaterial();
            material.SetFloat(MaskValue, 0.25f);
            material.SetFloat(Invert, 1f);

            // raw = saturate(1 * 1 + -0.25) = 0.75; alpha = 1 - 0.75 = 0.25
            AssertConstant(Interpret(material), 0.25f);
        }

        [Test]
        public void ReplaceNoMask_IgnoresUnprovableBaseAlphaInputs()
        {
            // Replace discards the base alpha term, so nothing that feeds it is
            // interpreted: a non-binary _MainIgnoreTexAlpha, a non-finite
            // _Color.a, and a _MainTex whose sampling state is unsupported would
            // each refuse on the mask-off path, yet none of them can reach the
            // Replace result. Pins that the shortcut reads only mask inputs.
            var material = ReplaceMaterial();
            material.SetFloat("_MainIgnoreTexAlpha", 0.5f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, float.NaN));
            material.SetTexture("_MainTex", ImportTexture("replace_unused"));
            material.SetFloat("_MainTexUV", 5f);
            material.SetFloat("_MainPixelMode", 1f);

            AssertConstant(Interpret(material), 1f);
        }

        // --- Replace refusals ----------------------------------------------

        [Test]
        public void ReplaceDoesNotBypassAlphaFeatureGates()
        {
            // The existing alpha writers are proven off before the mask is
            // interpreted. _AlphaMod adds to the alpha term downstream of the
            // mask, so a Replace constant must not short-circuit past it.
            var material = ReplaceMaterial();
            material.SetFloat("_AlphaMod", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_AlphaMod");
        }

        [Test]
        public void ReplaceWithAssignedMask_IsUnsupportedFeature()
        {
            var material = ReplaceMaterial();
            material.SetTexture(Mask, ImportTexture("replace_mask"));

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                Mask);
        }

        [Test]
        public void ReplaceNonFiniteBlendStrength_IsUnsupportedFeature()
        {
            var material = ReplaceMaterial();
            material.SetFloat(BlendStrength, float.PositiveInfinity);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                BlendStrength);
        }

        [Test]
        public void ReplaceNonFiniteValue_IsUnsupportedFeature()
        {
            var material = ReplaceMaterial();
            material.SetFloat(MaskValue, float.NaN);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                MaskValue);
        }

        [Test]
        public void ReplaceNonBinaryInvert_IsUnsupportedFeature()
        {
            var material = ReplaceMaterial();
            material.SetFloat(Invert, 0.5f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                Invert);
        }

        [Test]
        public void ReplaceFiniteInputsWhoseSumOverflows_IsUnsupportedFeature()
        {
            var material = ReplaceMaterial();
            material.SetFloat(BlendStrength, float.MaxValue);
            material.SetFloat(MaskValue, float.MaxValue);

            // Both inputs are finite but their sum is not, so the intermediate
            // cannot be proven and the shader's overflow behavior is not modeled.
            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                BlendStrength + " + " + MaskValue);
        }

        // --- Mask mode itself ----------------------------------------------

        [Test]
        public void UnsupportedMaskMode_IsUnsupportedFeature(
            [Values(2f, 3f, 4f, 1.5f, -1f)] float mode)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat(MaskMode, mode);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                MaskMode);
        }

        [Test]
        public void NonFiniteMaskMode_IsUnsupportedFeature()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat(MaskMode, float.NaN);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                MaskMode);
        }

        [Test]
        public void MaskModeOff_PreservesConstantAlpha()
        {
            var material = MaskOffMaterial();
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));

            AssertConstant(Interpret(material), 0.5f);
        }

        [Test]
        public void MaskModeOff_PreservesTextureBackedAlpha()
        {
            var material = MaskOffMaterial();
            material.SetTexture("_MainTex", ImportTexture("mask_off_alpha"));
            material.SetColor("_Color", Color.white);

            var value = Alpha(Interpret(material));

            Assert.That(
                value.Kind, Is.EqualTo(ScalarSemanticValueKind.TextureSample));
            Assert.That(value.GetChannel(), Is.EqualTo(TextureChannel.Alpha));
        }

        [Test]
        public void MaskModeOff_AssignedMaskIsIrrelevant()
        {
            // The mask is never sampled when the mode is off, so an assigned
            // mask must not refuse a mode-0 material.
            var material = MaskOffMaterial();
            material.SetTexture(Mask, ImportTexture("unused_mask"));
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.25f));

            AssertConstant(Interpret(material), 0.25f);
        }

        // --- Parallax: narrow gate on texture-backed _MainTex alpha ---------

        [Test]
        public void Parallax_RefusesTextureBackedAlpha()
        {
            // applyParallax overwrites poiMesh.uv[_ParallaxUV] before the
            // _MainTex sample, so a texture-backed alpha claim would describe a
            // view-dependent sampling domain.
            var material = MaskOffMaterial();
            material.SetTexture("_MainTex", ImportTexture("parallax_alpha"));
            material.SetColor("_Color", Color.white);
            material.SetFloat(Parallax, 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                Parallax);
        }

        [Test]
        public void Parallax_LeavesConstantAlphaComplete()
        {
            var material = MaskOffMaterial();
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            material.SetFloat(Parallax, 1f);

            AssertConstant(Interpret(material), 0.5f);
        }

        [Test]
        public void Parallax_LeavesIgnoredMainTexAlphaComplete()
        {
            var material = MaskOffMaterial();
            material.SetTexture("_MainTex", ImportTexture("parallax_ignored"));
            material.SetFloat("_MainIgnoreTexAlpha", 1f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.25f));
            material.SetFloat(Parallax, 1f);

            AssertConstant(Interpret(material), 0.25f);
        }

        [Test]
        public void Parallax_LeavesForcedOpaqueComplete()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("parallax_forced"));
            material.SetFloat(Parallax, 1f);

            AssertConstant(Interpret(material), 1f);
        }

        [Test]
        public void Parallax_LeavesReplaceNoMaskConstantComplete()
        {
            var material = ReplaceMaterial();
            material.SetTexture("_MainTex", ImportTexture("parallax_replace"));
            material.SetFloat(Parallax, 1f);

            AssertConstant(Interpret(material), 1f);
        }
    }
}
