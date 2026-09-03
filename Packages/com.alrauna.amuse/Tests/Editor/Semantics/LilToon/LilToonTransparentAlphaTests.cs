using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TextureWrapMode = UnityEngine.TextureWrapMode;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// The lilToon regular Transparent Normal alpha interpretation (design
    /// §7, §8; T1 §9.1). Every row here names the exact incorrect
    /// implementation it falsifies, and the rows marked "copy detector" are
    /// the ones a verbatim copy of the cutout suite would fail.
    /// </summary>
    public sealed class LilToonTransparentAlphaTests : LilToonFixtureTestBase
    {
        private const string MainTextureProperty = "_MainTex";
        private const string ColorProperty = "_Color";
        private const string CutoffProperty = "_Cutoff";
        private const string AlphaBoostFaProperty = "_AlphaBoostFA";
        private const string SubpassCutoffProperty = "_SubpassCutoff";
        private const string DistanceFadeProperty = "_DistanceFade";
        private const string DissolveParamsProperty = "_DissolveParams";
        private const string ScrollRotateProperty = "_MainTex_ScrollRotate";
        private const string UseDitherProperty = "_UseDither";
        private const string IdMaskPrior8Property = "_IDMaskPrior8";

        /// <summary>
        /// The exact transparent alpha scalar schema, stated independently of
        /// production. Three properties more than cutout
        /// (_AlphaBoostFA, _SubpassCutoff — and _DistanceFade as a vector),
        /// and one fewer: _UseDither is absent because LIL_RENDER 2 compiles
        /// the runtime dither path out (design §8; T1 §6 row 16).
        /// </summary>
        private static readonly string[] ExpectedAlphaScalars =
        {
            "_lilToonVersion",
            "_Invisible",
            "_UDIMDiscardCompile",
            "_UDIMDiscardMode",
            "_ShiftBackfaceUV",
            "_UseParallax",
            "_UseMain2ndTex",
            "_UseMain3rdTex",
            "_AlphaMaskMode",
            "_IDMask1",
            "_IDMask2",
            "_IDMask3",
            "_IDMask4",
            "_IDMask5",
            "_IDMask6",
            "_IDMask7",
            "_IDMask8",
            "_IDMaskControlsDissolve",
            "_Cutoff",
            "_AlphaBoostFA",
            "_SubpassCutoff",
        };

        private static readonly string[] ExpectedAlphaColors = { "_Color" };

        private static readonly string[] ExpectedAlphaVectors =
        {
            "_DissolveParams",
            "_MainTex_ScrollRotate",
            "_DistanceFade",
        };

        private static Color32[] SolidGrid(int width, int height, byte alpha)
        {
            var pixels = new Color32[width * height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(255, 255, 255, alpha);
            }

            return pixels;
        }

        private static AlphaTextureData Field(int width, int height, byte value)
        {
            var bytes = new byte[width * height];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = value;
            }

            return new AlphaTextureData(width, height, bytes);
        }

        private static AlphaMipChain Chain(params AlphaTextureData[] levels)
        {
            return new AlphaMipChain(levels);
        }

        private static AlphaMipChain AllOpaqueChain()
        {
            return Chain(Field(2, 2, 255), Field(1, 1, 255));
        }

        /// <summary>Mip 0 fully opaque, mip 1 fully non-opaque.</summary>
        private static AlphaMipChain OpaqueThenTransparentChain()
        {
            return Chain(Field(2, 2, 255), Field(1, 1, 0));
        }

        private static AlphaTextureData OpaqueGridWithTransparentTexelAlpha(
            int transparentX,
            int transparentY)
        {
            var bytes = new byte[4 * 4];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = 255;
            }

            bytes[transparentY * 4 + transparentX] = 0;
            return new AlphaTextureData(4, 4, bytes);
        }

        private static AlphaFieldProvider ProvidingFor(
            CapturedMaterialEvidence evidence,
            AlphaMipChain chain)
        {
            Assert.That(
                evidence.TryGetTexture(MainTextureProperty, out var assignment),
                Is.True,
                "the transparent request captures _MainTex");
            Assert.That(
                assignment.IsAssigned &&
                assignment.Texture != null &&
                assignment.Texture.HasSourceIdentity,
                Is.True,
                "resolver-seam tests key chains on a resolved source identity");

            var expected = assignment.Texture.SourceIdentity;
            return (TextureSourceId source, TextureChannel channel,
                out AlphaMipChain result) =>
            {
                if (source.Equals(expected))
                {
                    result = chain;
                    return true;
                }

                result = null;
                return false;
            };
        }

        /// <summary>Nondegenerate lower-left corner triangle.</summary>
        private static TriangleAlphaInput CornerTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.05f, 0.05f),
                new Vector2(0.45f, 0.05f),
                new Vector2(0.05f, 0.45f));
        }

        /// <summary>
        /// Hull stops at u = 0.7 in a 4-wide texture: inside texel column 2
        /// for point filtering, but within the half-texel bilinear reach of
        /// the transparent column-3 texel.
        /// </summary>
        private static TriangleAlphaInput HalfTexelOutsideHullTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.05f, 0.05f),
                new Vector2(0.7f, 0.05f),
                new Vector2(0.05f, 0.7f));
        }

        /// <summary>
        /// Crosses the u = 1 seam (u in [0.85, 1.1]): Repeat wraps into texel
        /// column 0, Clamp pins into column 3.
        /// </summary>
        private static TriangleAlphaInput SeamCrossingTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.85f, 0.05f),
                new Vector2(1.1f, 0.05f),
                new Vector2(0.85f, 0.4f));
        }

        private static CapturedMaterialEvidence CaptureTransparentEvidence(
            Material material)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest),
            })[0];
        }

        private AlphaResolution ResolveThroughTransparentFrontend(
            Material material,
            AlphaMipChain chain)
        {
            var captured = CaptureTransparentEvidence(material);
            var alpha = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentAlpha(captured);
            return AlphaSemanticsResolver.Resolve(
                alpha, ProvidingFor(captured, chain));
        }

        private Material NewGateOffMaterialWithOpaqueTexture(string textureName)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    textureName, 4, 4, SolidGrid(4, 4, 255)));
            return material;
        }

        private static void AssertAlphaGateUnknown(
            LilToonSemanticResult result,
            string propertyName)
        {
            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.False,
                $"{propertyName}: alpha must stay Unknown");
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha)
                    .Any(d =>
                        d.Code == LilToonSemanticDiagnosticCode
                            .UnsupportedFeature &&
                        d.Detail.Contains(propertyName)),
                Is.True,
                "expected an UnsupportedFeature alpha diagnostic naming " +
                propertyName);
        }

        private static LilToonSemanticResult InterpretTransparent(
            Material material)
        {
            return LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Linear, AllFeatures);
        }

        /// <summary>
        /// nextafter(value, +infinity) for binary32. MathF.BitIncrement does
        /// not exist in this Editor's API profile, so the one-ulp step comes
        /// from the bit pattern directly. The row below depends on the step
        /// being exactly one ulp: a larger step would still refuse and would
        /// stop falsifying the bound.
        /// </summary>
        private static float NextFloatAbove(float value)
        {
            return BitConverter.Int32BitsToSingle(
                BitConverter.SingleToInt32Bits(value) + 1);
        }

        [Test]
        public void AlphaEvidenceRequest_MatchesTheIndependentExactSchema()
        {
            var request =
                LilToonTransparentMaterialSemantics.AlphaEvidenceRequest;

            Assert.That(request.ShaderName, Is.True);
            Assert.That(request.ActiveColorSpace, Is.False);
            Assert.That(request.PresenceProperties, Is.Empty);
            CollectionAssert.AreEquivalent(
                ExpectedAlphaScalars, request.ScalarProperties);
            CollectionAssert.AreEquivalent(
                ExpectedAlphaColors, request.ColorProperties);
            CollectionAssert.AreEquivalent(
                ExpectedAlphaVectors, request.VectorProperties);
            Assert.That(request.TextureProperties.Count, Is.EqualTo(1));
            Assert.That(
                request.TextureProperties[0].PropertyName,
                Is.EqualTo("_MainTex"));
            Assert.That(
                request.TextureProperties[0].Evidence,
                Is.EqualTo(
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.Sampling |
                    TextureEvidenceKinds.AlphaChannel));

            // Copy detector: a widened or copied cutout request would carry
            // _UseDither, which LIL_RENDER 2 compiles out.
            CollectionAssert.DoesNotContain(
                request.ScalarProperties, UseDitherProperty);
        }

        // --- row 1: every mip is classified -------------------------------

        [Test]
        public void TransparentTexelOnlyInALowerMip_ForcesMustRemainTransparent()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mip");

            var resolution = ResolveThroughTransparentFrontend(
                material, OpaqueThenTransparentChain());

            // Falsifies: an implementation that classifies mip 0 only.
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void AllOpaqueChain_ProvesTheCornerTriangle()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_chain");

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        // --- row 2: footprint dilation and wrap ---------------------------

        [Test]
        public void PointProvesAndBilinearRefusesTheHalfTexelOutsideHull()
        {
            var pointMaterial = NewTransparentFixtureMaterial();
            pointMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "t_point", 4, 4, SolidGrid(4, 4, 255),
                    FilterMode.Point, TextureWrapMode.Clamp));
            var bilinearMaterial = NewTransparentFixtureMaterial();
            bilinearMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "t_bilinear", 4, 4, SolidGrid(4, 4, 255),
                    FilterMode.Bilinear, TextureWrapMode.Clamp));

            var chain = Chain(OpaqueGridWithTransparentTexelAlpha(3, 0));
            var pointResolution =
                ResolveThroughTransparentFrontend(pointMaterial, chain);
            var bilinearResolution =
                ResolveThroughTransparentFrontend(bilinearMaterial, chain);

            // Falsifies: hull-only classification without footprint dilation.
            Assert.That(
                pointResolution.Classify(HalfTexelOutsideHullTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                "point filtering never reads outside the hull");
            Assert.That(
                bilinearResolution.Classify(HalfTexelOutsideHullTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent),
                "bilinear filtering reaches half a texel beyond the hull");
        }

        [Test]
        public void RepeatWrapsIntoTheTransparentTexelAndClampDoesNot()
        {
            var repeatMaterial = NewTransparentFixtureMaterial();
            repeatMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "t_repeat", 4, 4, SolidGrid(4, 4, 255),
                    FilterMode.Point, TextureWrapMode.Repeat));
            var clampMaterial = NewTransparentFixtureMaterial();
            clampMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "t_clamp", 4, 4, SolidGrid(4, 4, 255),
                    FilterMode.Point, TextureWrapMode.Clamp));

            var chain = Chain(OpaqueGridWithTransparentTexelAlpha(0, 0));

            // Falsifies: missing wrap normalization.
            Assert.That(
                ResolveThroughTransparentFrontend(repeatMaterial, chain)
                    .Classify(SeamCrossingTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent),
                "Repeat wraps the hull into the transparent texel");
            Assert.That(
                ResolveThroughTransparentFrontend(clampMaterial, chain)
                    .Classify(SeamCrossingTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                "Clamp pins the same hull away from the transparent texel");
        }

        // --- row 3: the tint multiplier ------------------------------------

        [Test]
        public void ColorAlphaBelowOne_YieldsUniformMustRemainTransparent()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mult");
            material.SetColor(ColorProperty, new Color(1f, 1f, 1f, 0.8f));

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            // Falsifies: ignoring _Color.a.
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void ColorAlphaAboveOne_RefusesAsUnsupportedMultiplier()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mult_hi");
            material.SetColor(ColorProperty, new Color(1f, 1f, 1f, 1.5f));

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            Assert.That(resolution.IsResolved, Is.False);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteColorAlpha_IsUnknownNamingColor(float alphaValue)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_mult_nan");
            material.SetColor(ColorProperty, new Color(1f, 1f, 1f, alphaValue));

            // Falsifies: routing a non-finite multiplier into the resolver's
            // uniform-transparent fallthrough.
            AssertAlphaGateUnknown(
                InterpretTransparent(material), ColorProperty);
        }

        // --- row 4: the transparent cutoff bound (copy detector) ----------

        [TestCase(0.9999f)]
        [TestCase(1.0f)]
        public void CutoffAtOrBelowOne_ProvesTheCornerTriangle(float cutoff)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_cutoff_ok");
            material.SetFloat(CutoffProperty, cutoff);

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            // Falsifies: copying cutout's 0.9999 bound, which refuses 1.0.
            // The transparent site is a plain clip(a - c), and at a = 1 the
            // difference 1 - c is nonnegative for every finite c <= 1
            // (T1 §9.2).
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [TestCase(1.001f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void CutoffAboveOneOrNonFinite_IsUnknownNamingCutoff(
            float cutoff)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_cutoff_hi");
            material.SetFloat(CutoffProperty, cutoff);

            // Falsifies: modelling the cutout fwidth coverage transform here,
            // which would call 1.001 partial rather than fully discarded.
            AssertAlphaGateUnknown(
                InterpretTransparent(material), CutoffProperty);
        }

        // --- row 6: runtime gates, including the B2 counterexample --------

        [TestCase("_Invisible", 1f)]
        [TestCase("_UDIMDiscardCompile", 1f)]
        [TestCase("_UDIMDiscardMode", 1f)]
        [TestCase("_ShiftBackfaceUV", 1f)]
        [TestCase("_UseParallax", 1f)]
        [TestCase("_AlphaMaskMode", 1f)]
        [TestCase("_AlphaMaskMode", 2f)]
        [TestCase("_AlphaMaskMode", 3f)]
        [TestCase("_AlphaMaskMode", 4f)]
        [TestCase("_IDMask1", 1f)]
        [TestCase("_IDMask8", 1f)]
        [TestCase("_IDMaskControlsDissolve", 1f)]
        public void ActiveGate_KeepsAlphaUnknownNamingTheProperty(
            string property,
            float value)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_gate");
            material.SetFloat(property, value);

            // Falsifies: gating on the compiled feature set rather than
            // runtime material state.
            AssertAlphaGateUnknown(InterpretTransparent(material), property);
        }

        [Test]
        public void IdMaskControlsDissolveCounterexample_NeverCompletes()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_counter");
            material.SetFloat("_IDMaskControlsDissolve", 1f);
            material.SetFloat(IdMaskPrior8Property, 1f);
            material.SetVector(
                DissolveParamsProperty, new Vector4(0f, 0f, 0.5f, 0.1f));

            // The B2 adversarial counterexample: the vertex IDMask path can
            // force the sampled alpha chain to zero even at dissolve mode 0.
            AssertAlphaGateUnknown(
                InterpretTransparent(material), "_IDMaskControlsDissolve");
        }

        [Test]
        public void DissolveModeOne_IsUnknownNamingDissolveParams()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_dissolve");
            material.SetVector(
                DissolveParamsProperty, new Vector4(1f, 0f, 0.5f, 0.1f));

            AssertAlphaGateUnknown(
                InterpretTransparent(material), DissolveParamsProperty);
        }

        // --- row 7: _UseDither is inert here (copy detector) --------------

        [Test]
        public void ActiveUseDither_StillProvesTheCornerTriangle()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_dither");
            material.SetFloat(UseDitherProperty, 1f);

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            // Falsifies: a verbatim copy of the cutout gate array, which
            // would refuse. LIL_RENDER 2 compiles the dither path out
            // entirely (T1 §6 row 16), so an authored toggle is inert. This
            // is the positive row that makes the copy detectable.
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        // --- row 8: distance fade (copy detector) -------------------------

        [Test]
        public void DistanceFadeEnabled_IsUnknownNamingDistanceFade()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_fade_on");
            material.SetVector(
                DistanceFadeProperty, new Vector4(0.1f, 0.01f, 0.5f, 0f));

            // Falsifies: omitting the only post-clip alpha writer, and
            // gating on _DistanceFadeColor.a instead of _DistanceFade.z.
            AssertAlphaGateUnknown(
                InterpretTransparent(material), DistanceFadeProperty);
        }

        [Test]
        public void DistanceFadeNonFinite_IsUnknownNamingDistanceFade()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_fade_nan");
            material.SetVector(
                DistanceFadeProperty,
                new Vector4(0.1f, 0.01f, 0f, float.NaN));

            AssertAlphaGateUnknown(
                InterpretTransparent(material), DistanceFadeProperty);
        }

        [Test]
        public void DistanceFadeDisabled_ProvesTheCornerTriangle()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_fade_off");

            // The shipped default (0.1, 0.01, 0, 0) has z == 0 and is inert.
            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        // --- row 9: depth fade is dead code -------------------------------

        [Test]
        public void DepthFade_IsNeitherScannedNorRequested()
        {
            var request =
                LilToonTransparentMaterialSemantics.AlphaEvidenceRequest;

            // Falsifies: speculative _DepthFade* gates, and an
            // implementation that assumes the block is live. The pinned
            // package never defines LIL_FEATURE_DEPTH_FADE, so the block is
            // unreachable (T1 §5.5).
            foreach (var property in request.ScalarProperties
                         .Concat(request.VectorProperties)
                         .Concat(request.ColorProperties))
            {
                Assert.That(
                    property.StartsWith("_DepthFade", StringComparison.Ordinal),
                    Is.False,
                    "depth fade is dead code and must not be requested: " +
                    property);
            }

            CollectionAssert.DoesNotContain(
                AllFeatures, "LIL_FEATURE_DEPTH_FADE");
        }

        // --- row 10: the 2nd and 3rd layer alpha writers ------------------

        [TestCase("_UseMain2ndTex")]
        [TestCase("_UseMain3rdTex")]
        public void ActiveLayer_KeepsAlphaUnknownNamingTheLayerToggle(
            string property)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_layer");
            material.SetFloat(property, 1f);

            // Falsifies: missing the LIL_RENDER != 0 layer alpha writers.
            AssertAlphaGateUnknown(InterpretTransparent(material), property);
        }

        // --- row 11: the ForwardAdd premultiply (copy detector) -----------

        [TestCase(1f)]
        [TestCase(10f)]
        public void AlphaBoostFaAtOrAboveOne_ProvesTheCornerTriangle(
            float boost)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_boost_ok");
            material.SetFloat(AlphaBoostFaProperty, boost);

            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [TestCase(0.5f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void AlphaBoostFaBelowOne_IsUnknownNamingTheProperty(
            float boost)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_boost_bad");
            material.SetFloat(AlphaBoostFaProperty, boost);

            // Falsifies: treating ForwardAdd as if it were the base pass.
            // The base premultiply rgb *= a is the identity at a = 1; the
            // ForwardAdd premultiply saturate(a * _AlphaBoostFA) is not
            // (T1 §5.3).
            AssertAlphaGateUnknown(
                InterpretTransparent(material), AlphaBoostFaProperty);
        }

        // --- row 5 (alpha side): the subpass shadow clip ------------------

        [TestCase(0.5f)]
        [TestCase(1.0f)]
        public void SubpassCutoffAtOrBelowOne_ProvesTheCornerTriangle(
            float subpassCutoff)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_sub_ok");
            material.SetFloat(SubpassCutoffProperty, subpassCutoff);

            // 0.5 is the shipped default: a bound tighter than the measured
            // slice-15 result would silently lose the whole default
            // population (T1 §9.4).
            var resolution =
                ResolveThroughTransparentFrontend(material, AllOpaqueChain());

            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void SubpassCutoffJustAboveOne_IsUnknownNamingTheProperty()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_sub_eps");
            var subpassCutoff = NextFloatAbove(1f);

            // Degradation guard: the row depends on the step being exactly
            // one ulp, so a future runtime change must not silently turn
            // this falsifier into a no-op.
            Assert.That(subpassCutoff, Is.GreaterThan(1f));

            material.SetFloat(SubpassCutoffProperty, subpassCutoff);

            // Falsifies: omitting the subpass shadow condition entirely, or
            // treating SHADOW_CASTER as identical to the target's.
            AssertAlphaGateUnknown(
                InterpretTransparent(material), SubpassCutoffProperty);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void NonFiniteSubpassCutoff_IsUnknownNamingTheProperty(
            float value)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_sub_nan");
            material.SetFloat(SubpassCutoffProperty, value);

            AssertAlphaGateUnknown(
                InterpretTransparent(material), SubpassCutoffProperty);
        }

        // --- row 14: exact UV identity ------------------------------------

        [TestCase(1f, 1f, 0f, 0.0001f)]
        [TestCase(2f, 1f, 0f, 0f)]
        [TestCase(1f, 1f, 0.000005f, 0f)]
        public void NonIdentityMainTexSt_IsRefusedAtTheFamilyBoundary(
            float scaleX,
            float scaleY,
            float offsetX,
            float offsetY)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_st");
            material.SetTextureScale(
                MainTextureProperty, new Vector2(scaleX, scaleY));
            material.SetTextureOffset(
                MainTextureProperty, new Vector2(offsetX, offsetY));

            // Falsifies: delegating lilToon ST to PR #42's family-blind
            // affine resolver, and using Unity's epsilon-based Vector2
            // equality instead of per-binary32-component tests.
            // lilRotateUV has no zero-angle early-out at this version
            // (T1 §5.6), so transparent inherits the identity-only boundary.
            var result = InterpretTransparent(material);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha)
                    .Any(d =>
                        d.Code ==
                        LilToonSemanticDiagnosticCode.UnsupportedUv),
                Is.True);
        }

        [TestCase(0.0001f, 0f, 0f, 0f)]
        [TestCase(0f, 0.0001f, 0f, 0f)]
        [TestCase(0f, 0f, 0.0001f, 0f)]
        [TestCase(0f, 0f, 0f, 0.0001f)]
        public void NonZeroScrollRotateComponent_IsUnknownNamingScrollRotate(
            float x, float y, float z, float w)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_scroll");
            material.SetVector(
                ScrollRotateProperty, new Vector4(x, y, z, w));

            AssertAlphaGateUnknown(
                InterpretTransparent(material), ScrollRotateProperty);
        }

        // --- row 16: compilation-variant invariance -----------------------

        [Test]
        public void AlphaVerdict_IsInvariantUnderFeaturesAndColorSpace()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("t_invariance");
            var superset = AllFeatures
                .Concat(new[] { "LIL_FEATURE_UNRELATED" })
                .ToArray();

            var withAll = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Linear, AllFeatures)
                .Semantics.Alpha;
            var withSuperset = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Linear, superset)
                .Semantics.Alpha;
            var withEmpty = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Linear, Array.Empty<string>())
                .Semantics.Alpha;
            var withGamma = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentMaterial(
                    material, ColorSpace.Gamma, AllFeatures)
                .Semantics.Alpha;

            // Falsifies: a verdict that depends on the define set rather than
            // runtime gates — a broken callback-100 invariance claim.
            Assert.That(withAll.IsComplete, Is.True);
            Assert.That(withSuperset.IsComplete, Is.True);
            Assert.That(withEmpty.IsComplete, Is.True);
            Assert.That(withGamma.IsComplete, Is.True);
            Assert.That(
                withSuperset.GetCompleteValue(),
                Is.EqualTo(withAll.GetCompleteValue()));
            Assert.That(
                withEmpty.GetCompleteValue(),
                Is.EqualTo(withAll.GetCompleteValue()));
            Assert.That(
                withGamma.GetCompleteValue(),
                Is.EqualTo(withAll.GetCompleteValue()));
        }
    }
}
