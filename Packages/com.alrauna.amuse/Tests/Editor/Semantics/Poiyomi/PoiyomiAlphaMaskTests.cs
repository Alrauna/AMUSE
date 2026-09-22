using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;
using CoreWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;

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
    /// constant this suite pins exactly. A bound mask proves through its
    /// red channel exactly under the pairs whose binary32 arithmetic needs
    /// no texel threshold: (1, 0) and the provably saturated (1, value &gt;= 1).
    /// Every other pair refuses, naming the culprit property.
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
        public void ReplaceWithAssignedMask_ProvesTheBoundAdmittedPair()
        {
            // Task 3 replaced the blanket assigned-mask refusal: with the
            // declared defaults (1, 0) and invert off the bound mask proves
            // through its red field. ReplaceBoundMask_AdmittedPair_...
            // pins that proof in full, including the sampler routing and
            // the classification seam, so this former refusal pin is now
            // the minimal end-to-end row of the same behavior. The main
            // texture is assigned because the mask sample rides the main
            // sampler, whose state is a required fact on the bound route.
            var material = ReplaceMaterial();
            material.SetTexture("_MainTex", ImportTexture("replace_main"));
            material.SetTexture(Mask, ImportTexture("replace_mask"));

            var value = Alpha(Interpret(material));

            Assert.That(
                value.Kind, Is.EqualTo(ScalarSemanticValueKind.TextureSample));
            Assert.That(
                value.GetChannel(), Is.EqualTo(TextureChannel.Red));
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

        // --- Task 3: the bound replace mask through the red field -----------

        private const string MaskUv = "_AlphaMaskUV";
        private const string MaskPan = "_AlphaMaskPan";

        /// <summary>
        /// Replace-mode material with a mask bound. The vendor mask block
        /// samples the mask through the main sampler, so the fixture assigns
        /// _MainTex too. The color alpha sits below one on purpose: a wrong
        /// implementation that proves the term from the base chain can never
        /// prove the fixture triangle, while the all-opaque red field does.
        /// </summary>
        private Material BoundMaskMaterial()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat(MaskMode, 1f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            material.SetTexture("_MainTex", ImportTexture("bound_mask_main"));
            return material;
        }

        private static TextureSample RedFieldSample(
            string maskToken,
            Vector2 scale,
            TextureSampling sampling)
        {
            return new TextureSample(
                new TextureSourceId(maskToken),
                new UvMapping(0, scale, Vector2.zero),
                sampling);
        }

        // One 8 by 8 single-level red field. The named texel, if any, is the
        // only sub-one witness.
        private static AlphaFieldProvider RedFieldWithHole(int holeX, int holeY)
        {
            var bytes = SolidRedBytes();
            bytes[holeY * 8 + holeX] = 0;
            var field = new AlphaTextureData(8, 8, bytes);
            return (TextureSourceId source,
                TextureChannel channel,
                out AlphaMipChain chain) =>
            {
                chain = new AlphaMipChain(new[] { field });
                return true;
            };
        }

        private static AlphaFieldProvider SolidRedField()
        {
            var field = new AlphaTextureData(8, 8, SolidRedBytes());
            return (TextureSourceId source,
                TextureChannel channel,
                out AlphaMipChain chain) =>
            {
                chain = new AlphaMipChain(new[] { field });
                return true;
            };
        }

        private static byte[] SolidRedBytes()
        {
            var bytes = new byte[8 * 8];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = 255;
            }

            return bytes;
        }

        // A provider that never answers. A constant verdict cannot need it,
        // so a call means the verdict wrongly consulted a texel.
        private static bool RefusingFields(
            TextureSourceId source,
            TextureChannel channel,
            out AlphaMipChain chain)
        {
            chain = default;
            return false;
        }

        private static AlphaResolution ResolveRed(
            ScalarSemanticValue value,
            AlphaFieldProvider fields)
        {
            return AlphaSemanticsResolver.Resolve(
                SemanticOutput<ScalarSemanticValue>.Complete(value), fields, 0);
        }

        private static TriangleAlphaInput InteriorTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.05f, 0.15f),
                new Vector2(0.1f, 0.15f),
                new Vector2(0.05f, 0.2f));
        }

        // The domain hugs the wrap seam. Its x range is the last half texel
        // column, so under bilinear repeat its samples blend the wrapped
        // first texel column.
        private static TriangleAlphaInput SeamTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.9375f, 0.4375f),
                new Vector2(1f, 0.4375f),
                new Vector2(0.9375f, 0.5f));
        }

        [Test]
        public void ReplaceBoundMask_AdmittedPair_ProvesThroughTheRedField()
        {
            // The mask import is point-filtered on purpose. The vendor samples
            // the mask through the main sampler, so the proven sampling must
            // be the main bilinear repeat state, never the mask import state.
            // A wrong implementation that keeps refusing an assigned mask
            // fails on the missing completion, and one that proves the term
            // from the base chain fails on the channel: the color alpha is
            // one half here, so the base chain can never prove anything.
            var material = BoundMaskMaterial();
            var mask = ImportTexture(
                "bound_mask_admitted", i => i.filterMode = FilterMode.Point);
            material.SetTexture(Mask, mask);

            var value = Alpha(Interpret(material));

            // The vendor term is saturate(r * 1 + 0) with invert off. Binary32
            // multiplies r by one exactly and adds zero exactly, and a
            // normalized red sample sits in [0, 1], so the saturate is inert
            // and the proven value is the sampled red itself under the mask's
            // own plain affine of UV0.
            Assert.That(value, Is.EqualTo(ScalarSemanticValue.Texture(
                RedFieldSample(
                    ExpectedToken(mask),
                    Vector2.one,
                    new TextureSampling(
                        TextureFilterMode.Bilinear, CoreWrapMode.Repeat)),
                TextureChannel.Red)));

            var resolution = ResolveRed(value, SolidRedField());

            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(InteriorTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        // --- Falsifier 1: mask hole ---
        // --- Falsifier 2: wrap seam ---
        [Test]
        public void ReplaceBoundMask_WithHole_ClassifiesTheHoleUnknown()
        {
            var material = BoundMaskMaterial();
            material.SetTexture(Mask, ImportTexture("bound_mask_hole"));

            var value = Alpha(Interpret(material));

            // No-op guard: the solid field proves the interior triangle, so
            // the outcomes below come from the hole texels alone.
            var solid = ResolveRed(value, SolidRedField());
            Assert.That(solid.IsResolved, Is.True);
            Assert.That(
                solid.Classify(InteriorTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));

            // A red hole inside the domain keeps the triangle unproven. It
            // must neither widen into a transparency claim over other
            // triangles nor prove opaque.
            var holed = ResolveRed(value, RedFieldWithHole(1, 1));
            Assert.That(holed.IsResolved, Is.True);
            Assert.That(
                holed.Classify(InteriorTriangle()),
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque));

            // The same hole shape sits in the wrapped first texel column. The
            // seam triangle's domain is the last half texel column, whose
            // bilinear footprint blends the wrapped texels. The wrap-blend
            // rule keeps that triangle unproven, while the same field proves
            // the interior triangle that never touches the wrapped support.
            var seam = ResolveRed(value, RedFieldWithHole(0, 3));
            Assert.That(seam.IsResolved, Is.True);
            Assert.That(
                seam.Classify(InteriorTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                seam.Classify(SeamTriangle()),
                Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        // --- Falsifier 3: non-identity mask affine ---
        [Test]
        public void ReplaceBoundMask_NonIdentityMaskSt_ProvesTriangle()
        {
            var material = BoundMaskMaterial();
            var mask = ImportTexture("bound_mask_st");
            material.SetTexture(Mask, mask);
            material.SetTextureScale(Mask, new Vector2(2f, 1f));
            material.SetTextureOffset(Mask, Vector2.zero);

            var value = Alpha(Interpret(material));

            // The mask affine survives into the proven sample.
            Assert.That(value, Is.EqualTo(ScalarSemanticValue.Texture(
                RedFieldSample(
                    ExpectedToken(mask),
                    new Vector2(2f, 1f),
                    new TextureSampling(
                        TextureFilterMode.Bilinear, CoreWrapMode.Repeat)),
                TextureChannel.Red)));

            // The mask ST is a plain affine, not the rotation path that
            // forces the main-ST family boundary, so the non-identity scale
            // flows through the family-blind affine resolver exactly like
            // the lilToon rule.
            var resolution = ResolveRed(value, SolidRedField());

            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(InteriorTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void ReplaceBoundMask_UvNotExactInteger_RefusesNamingUv(
            [Values(1.5f, 4f, -1f)] float uv)
        {
            // No-op guard: the exact default channel proves, so the refusals
            // below come from the channel selector alone.
            var guard = BoundMaskMaterial();
            guard.SetTexture(Mask, ImportTexture("bound_mask_uv_guard"));
            AssertOutputComplete(
                Interpret(guard), PoiyomiSemanticOutput.Alpha);

            var material = BoundMaskMaterial();
            material.SetTexture(Mask, ImportTexture("bound_mask_uv_bad"));
            material.SetFloat(MaskUv, uv);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                MaskUv);
        }

        [Test]
        public void ReplaceBoundMask_NonZeroPan_RefusesNamingPan(
            [Values(0, 1, 2, 3)] int component)
        {
            // No-op guard: the exact zero pan proves, so the refusals below
            // come from the pan vector alone.
            var guard = BoundMaskMaterial();
            guard.SetTexture(Mask, ImportTexture("bound_mask_pan_guard"));
            AssertOutputComplete(
                Interpret(guard), PoiyomiSemanticOutput.Alpha);

            var material = BoundMaskMaterial();
            material.SetTexture(Mask, ImportTexture("bound_mask_pan_bad"));
            var pan = Vector4.zero;
            pan[component] = 0.25f;
            material.SetVector(MaskPan, pan);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                MaskPan);
        }

        [Test]
        public void ReplaceBoundMask_InvertOnValueZero_ProvesTheInvertedField()
        {
            var material = BoundMaskMaterial();
            var mask = ImportTexture("bound_mask_invert");
            material.SetTexture(Mask, mask);
            material.SetFloat(Invert, 1f);

            var value = Alpha(Interpret(material));

            // The vendor term for invert on is 1 - saturate(r * 1 - 0).
            // Binary32 multiplies r by one exactly and subtracts zero
            // exactly, and a normalized red sample sits in [0, 1], so the
            // saturate is inert and the term is the single subtraction
            // 1 - r. The result stays in [0, 1], so the shape
            // saturate(1 - r) carries the same value with an inert clamp.
            Assert.That(
                value.Kind,
                Is.EqualTo(ScalarSemanticValueKind.SaturatingDifference));
            Assert.That(
                value.GetMinuend(),
                Is.EqualTo(ScalarSemanticValue.Constant(1f)));
            Assert.That(
                value.GetSubtrahend(),
                Is.EqualTo(ScalarSemanticValue.Texture(
                    RedFieldSample(
                        ExpectedToken(mask),
                        Vector2.one,
                        new TextureSampling(
                            TextureFilterMode.Bilinear, CoreWrapMode.Repeat)),
                    TextureChannel.Red)));

            // The inverted field never proves a triangle: the resolver's
            // difference lemma answers must-remain-transparent uniformly,
            // so a bound inverted mask stays off the opaque plan everywhere.
            var resolution = ResolveRed(value, SolidRedField());

            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(InteriorTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void ReplaceBoundMask_SaturatedPair_ProvesConstantOne(
            [Values(0f, 1f)] float invert)
        {
            var material = BoundMaskMaterial();
            material.SetTexture(Mask, ImportTexture("bound_mask_saturated"));
            material.SetFloat(MaskValue, 1f);
            material.SetFloat(Invert, invert);

            var value = Alpha(Interpret(material));

            // Invert off: in exact arithmetic r * 1 + 1 >= 1 for every red
            // sample r >= 0, binary32 rounding is monotone, so the sum stays
            // at or above one under fused and unfused rounding alike, and
            // the saturate is exactly one. Invert on: r * 1 - 1 <= 0 for r
            // in [0, 1], so the saturate is exactly zero and 1 - 0 is
            // exactly one. Both invert states replace the alpha with the
            // constant one and need no texel of the mask.
            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(1f));

            // A wrong implementation that needs a texel threshold fails on
            // the refused provider below, because a constant verdict never
            // consults a field.
            var resolution = AlphaSemanticsResolver.Resolve(
                SemanticOutput<ScalarSemanticValue>.Complete(value),
                RefusingFields,
                0);

            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.TryGetUniformOutcome(out var outcome),
                Is.True);
            Assert.That(outcome, Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [TestCase(0.5f, 0f, "_AlphaMaskBlendStrength")]
        [TestCase(2f, 0f, "_AlphaMaskBlendStrength")]
        [TestCase(0.5f, 0.5f, "_AlphaMaskBlendStrength")]
        [TestCase(1f, 0.5f, "_AlphaMaskValue")]
        [TestCase(1f, -0.25f, "_AlphaMaskValue")]
        public void ReplaceBoundMask_OtherStrengthValuePairs_RefuseNamingTheProperty(
            float strength, float value, string expectedDetail)
        {
            var material = BoundMaskMaterial();
            material.SetTexture(Mask, ImportTexture("bound_mask_offpair"));
            material.SetFloat(BlendStrength, strength);
            material.SetFloat(MaskValue, value);

            // Every other pair needs the deferred threshold-envelope
            // contract: proving saturate(r * s + v) at one needs a per-texel
            // predicate whose rounding argument is future work. The refusal
            // names the first culprit, the strength when it leaves one and
            // the value alone when the strength is exactly one.
            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                expectedDetail);
        }
    }
}
