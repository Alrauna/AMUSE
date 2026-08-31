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
    /// RED suite for the lilToon cutout alpha interpretation (plan Task 2
    /// Step 3; spec §8 of the cutout-to-opaque conversion design). Evidence is
    /// captured with
    /// <see cref="LilToonCutoutMaterialSemantics.AlphaEvidenceRequest"/>;
    /// resolver-seam tests supply synthetic <see cref="AlphaMipChain"/>s
    /// through an <see cref="AlphaFieldProvider"/> keyed on the captured
    /// <see cref="TextureSourceId"/>, exactly as
    /// <c>AlphaSemanticsResolverTests</c> does. On the Step 2 scaffold the
    /// texture-backed arm refuses as Unknown, so every completing assertion
    /// here is RED by construction while the gate-refusal assertions are
    /// already covered by the implemented gate sequence.
    /// </summary>
    public sealed class LilToonCutoutAlphaTests : LilToonFixtureTestBase
    {
        private const string MainTextureProperty = "_MainTex";
        private const string ColorProperty = "_Color";
        private const string CutoffProperty = "_Cutoff";
        private const string DissolveParamsProperty = "_DissolveParams";
        private const string ScrollRotateProperty = "_MainTex_ScrollRotate";
        private const string IdMaskPrior8Property = "_IDMaskPrior8";

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
            "_UseDither",
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
        };

        private static Color32[] OddBoundaryAlphaPixels()
        {
            var pixels = new Color32[64];
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    pixels[y * 8 + x] =
                        new Color32(255, 255, 255, x < 5 ? (byte)255 : (byte)200);
                }
            }

            return pixels;
        }

        private static readonly string[] ExpectedAlphaColors = { "_Color" };

        private static readonly string[] ExpectedAlphaVectors =
        {
            "_DissolveParams",
            "_MainTex_ScrollRotate",
        };

        [Test]
        public void AlphaEvidenceRequest_MatchesTheIndependentExactSchema()
        {
            var request = LilToonCutoutMaterialSemantics.AlphaEvidenceRequest;

            Assert.That(request.ShaderName, Is.True);
            Assert.That(request.ActiveColorSpace, Is.False);
            Assert.That(request.PresenceProperties, Is.Empty);
            Assert.That(
                request.ScalarProperties.Count,
                Is.EqualTo(ExpectedAlphaScalars.Length));
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
        }

        // --- helpers ----------------------------------------------------------

        private static Color32[] SolidGrid(int width, int height, byte alpha)
        {
            var pixels = new Color32[width * height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(255, 255, 255, alpha);
            }

            return pixels;
        }

        private static Color32[] OpaqueGridWithTransparentTexel(
            int transparentX,
            int transparentY)
        {
            var pixels = SolidGrid(4, 4, 255);
            pixels[transparentY * 4 + transparentX] =
                new Color32(255, 255, 255, 0);
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

        /// <summary>2x2 fully opaque over 1x1 fully opaque.</summary>
        private static AlphaMipChain AllOpaqueChain()
        {
            return Chain(Field(2, 2, 255), Field(1, 1, 255));
        }

        /// <summary>
        /// Mip 0 fully opaque, mip 1 fully non-opaque: the one transparent
        /// texel lives only in a lower mip.
        /// </summary>
        private static AlphaMipChain OpaqueThenTransparentChain()
        {
            return Chain(Field(2, 2, 255), Field(1, 1, 0));
        }

        private static AlphaFieldProvider ProvidingNothing()
        {
            return (TextureSourceId source, TextureChannel channel,
                out AlphaMipChain result) =>
            {
                result = null;
                return false;
            };
        }

        /// <summary>
        /// Keys the synthetic chain on the captured source identity, so the
        /// resolver is only ever fed the field that belongs to the texture the
        /// interpretation actually named.
        /// </summary>
        private static AlphaFieldProvider ProvidingFor(
            CapturedMaterialEvidence evidence,
            AlphaMipChain chain)
        {
            Assert.That(
                evidence.TryGetTexture(MainTextureProperty, out var assignment),
                Is.True,
                "the cutout request captures _MainTex");
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

        /// <summary>
        /// The single transparent texel grids of the seam tests, as exact
        /// resolver-seam alpha data: the classifier, not the import, is the
        /// oracle for the boundary outcomes.
        /// </summary>
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

        private static TriangleAlphaInput MipZeroOpaqueTexelTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.51f, 0.01f),
                new Vector2(0.61f, 0.01f),
                new Vector2(0.51f, 0.11f));
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

        private static CapturedMaterialEvidence CaptureCutoutEvidence(
            Material material)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
            })[0];
        }

        /// <summary>
        /// Drives capture, the cutout interpretation, and the resolver with a
        /// provider keyed on the captured source identity.
        /// </summary>
        private AlphaResolution ResolveThroughCutoutFrontend(
            Material material,
            AlphaMipChain chain)
        {
            var captured = CaptureCutoutEvidence(material);
            var alpha =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutAlpha(
                    captured);
            return AlphaSemanticsResolver.Resolve(
                alpha, ProvidingFor(captured, chain));
        }

        private Material NewGateOffMaterialWithOpaqueTexture(string textureName)
        {
            var material = NewCutoutFixtureMaterial();
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
                $"expected an UnsupportedFeature alpha diagnostic naming " +
                propertyName);
        }

        // --- 1. constant-opaque core ------------------------------------------

        [Test]
        public void GateOffMaterial_CompletesAPlainTexturedAlphaTerm()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("core_opaque");

            var alpha =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutAlpha(
                    CaptureCutoutEvidence(material));

            // Falsifies: an opaque-only constant-1 interpretation and
            // re-deriving the boundary from a > cutoff — the cutout theorem's
            // value shape is the texture sample itself at _Color.a == 1.
            Assert.That(
                alpha.IsComplete,
                Is.True,
                "the texture-backed arm must complete for a gate-off " +
                "material with an assigned, supported texture");
            Assert.That(
                alpha.GetCompleteValue().Kind,
                Is.EqualTo(ScalarSemanticValueKind.TextureSample));
        }

        [Test]
        public void GateOffMaterial_ProvesCornerTriangleOverAllOpaqueChain()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("core_chain");

            var resolution = ResolveThroughCutoutFrontend(
                material, AllOpaqueChain());

            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        // --- 2. the _Color.a multiplier ---------------------------------------

        [Test]
        public void ColorAlphaBelowOne_YieldsUniformMustRemainTransparent()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("multiplier");
            material.SetColor(ColorProperty, new Color(1f, 1f, 1f, 0.8f));

            var captured = CaptureCutoutEvidence(material);
            var alpha =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutAlpha(
                    captured);
            var resolution = AlphaSemanticsResolver.Resolve(
                alpha, ProvidingFor(captured, AllOpaqueChain()));

            // Falsifies: ignoring _Color.a — the cutout pass multiplies the
            // sample by it, so 0.8 is a scaled sample, and the multiplier
            // lemma caps it at uniform transparency without any geometry.
            Assert.That(alpha.IsComplete, Is.True);
            Assert.That(
                alpha.GetCompleteValue().Kind,
                Is.EqualTo(ScalarSemanticValueKind.TextureSampleTimesConstant));
            Assert.That(
                resolution.TryGetUniformOutcome(out var outcome),
                Is.True,
                "a sub-one multiplier is uniform, not classified");
            Assert.That(
                outcome,
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void ColorAlphaAboveOne_RefusesAsUnsupportedMultiplier()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("multiplier_hi");
            material.SetColor(ColorProperty, new Color(1f, 1f, 1f, 1.2f));

            var resolution = ResolveThroughCutoutFrontend(
                material, AllOpaqueChain());

            // Falsifies: saturating or clamping an out-of-range multiplier —
            // alpha above one has no defined opacity meaning and must refuse.
            Assert.That(resolution.IsResolved, Is.False);
            Assert.That(
                resolution.Failure,
                Is.EqualTo(AlphaResolutionFailure.UnsupportedMultiplier));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteColorAlpha_IsUnknownNamingColor(float alphaValue)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("multiplier_nan");
            material.SetColor(ColorProperty, new Color(1f, 1f, 1f, alphaValue));

            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // Falsifies: relying on the resolver's uniform-transparent `< 1`
            // fallthrough for non-finite multipliers — the interpretation's
            // own finite check must refuse first, with a diagnostic.
            AssertAlphaGateUnknown(result, ColorProperty);
        }

        // --- 3. the cutoff boundary -------------------------------------------

        [Test]
        public void CutoffAtTwiceMargin_CompletesAndProvesCornerTriangle()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("cutoff_edge");
            material.SetFloat(CutoffProperty, 0.9999f);

            var captured = CaptureCutoutEvidence(material);
            var alpha =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutAlpha(
                    captured);
            var resolution = AlphaSemanticsResolver.Resolve(
                alpha, ProvidingFor(captured, AllOpaqueChain()));

            // Falsifies: reusing Poiyomi's `<= 1` rule at the classification
            // layer — 0.9999 is the provable bound and must still complete.
            Assert.That(alpha.IsComplete, Is.True);
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [TestCase(1.0f)]
        [TestCase(1.001f)]
        public void CutoffAboveTwiceMargin_IsUnknownNamingCutoff(float cutoff)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("cutoff_high");
            material.SetFloat(CutoffProperty, cutoff);

            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // Falsifies: plain-clip semantics that would still prove every
            // fully covered fragment — at c > 0.9999 no triangle is provable
            // and the classification layer refuses before any geometry runs.
            AssertAlphaGateUnknown(result, CutoffProperty);
        }

        [Test]
        public void NonFiniteCutoff_IsUnknownNamingCutoff()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("cutoff_nan");
            material.SetFloat(CutoffProperty, float.NaN);

            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // Falsifies: NaN-falls-through implementations (NaN fails `<=`
            // silently and could slip past a bare bound check).
            AssertAlphaGateUnknown(result, CutoffProperty);
        }

        // --- 4. the runtime gate matrix ---------------------------------------

        [TestCase("_Invisible", 1f)]
        [TestCase("_UDIMDiscardCompile", 1f)]
        [TestCase("_UDIMDiscardMode", 1f)]
        [TestCase("_ShiftBackfaceUV", 1f)]
        [TestCase("_UseParallax", 1f)]
        [TestCase("_UseMain2ndTex", 1f)]
        [TestCase("_UseMain3rdTex", 1f)]
        [TestCase("_AlphaMaskMode", 1f)]
        [TestCase("_AlphaMaskMode", 2f)]
        [TestCase("_AlphaMaskMode", 3f)]
        [TestCase("_AlphaMaskMode", 4f)]
        [TestCase("_UseDither", 1f)]
        [TestCase("_IDMask1", 1f)]
        [TestCase("_IDMask2", 1f)]
        [TestCase("_IDMask3", 1f)]
        [TestCase("_IDMask4", 1f)]
        [TestCase("_IDMask5", 1f)]
        [TestCase("_IDMask6", 1f)]
        [TestCase("_IDMask7", 1f)]
        [TestCase("_IDMask8", 1f)]
        [TestCase("_IDMaskControlsDissolve", 1f)]
        public void ActiveGate_KeepsAlphaUnknownNamingTheProperty(
            string property,
            float value)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("gate_" + property);
            material.SetFloat(property, value);

            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // Falsifies: gating on ScanCompiledFeatures alone, and omitting
            // _UDIMDiscardMode or _IDMaskControlsDissolve from the runtime
            // gate set — each captured runtime gate refuses independently.
            AssertAlphaGateUnknown(result, property);
        }

        [Test]
        public void IdMaskControlsDissolveCounterexample_NeverCompletes()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("counterexample");

            // Fixture-declaration precondition: the B2 counterexample needs
            // the vendor prior byte on the stand-in, and it is deliberately
            // NOT a member of the cutout alpha evidence request.
            Assert.That(
                material.HasProperty(IdMaskPrior8Property),
                Is.True,
                "the cutout fixture must declare the vendor prior byte");
            material.SetFloat("_IDMaskControlsDissolve", 1f);
            material.SetFloat(IdMaskPrior8Property, 1f);
            for (var index = 1; index <= 8; index++)
            {
                material.SetFloat("_IDMask" + index, 0f);
            }

            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // Falsifies: compiled-feature-only gating — with
            // _IDMaskControlsDissolve set and the vendor prior byte non-zero,
            // the vertex IDMask path forces coverage to zero even at dissolve
            // mode 0: a material that renders nothing must never be proven.
            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.False,
                "the counterexample must never complete");
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha)
                    .Any(d => d.Detail.Contains("_IDMaskControlsDissolve")),
                Is.True,
                "expected an alpha diagnostic naming _IDMaskControlsDissolve");

            var resolution = AlphaSemanticsResolver.Resolve(
                result.Semantics.Alpha, ProvidingNothing());
            Assert.That(
                resolution.IsResolved,
                Is.False,
                "no resolution — so no ProvenOpaque triangle — may come " +
                "out of the counterexample");
        }

        // --- 5. dissolve mode ---------------------------------------------------

        [Test]
        public void DissolveModeOne_IsUnknownNamingDissolveParams()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("dissolve_on");
            material.SetVector(
                DissolveParamsProperty, new Vector4(1f, 0f, 0.5f, 0.1f));

            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // Falsifies: treating _DissolveParams as RGB-only or reading the
            // rounded mode — the proof needs the exact captured component.
            AssertAlphaGateUnknown(result, DissolveParamsProperty);
        }

        [Test]
        public void DissolveModeZero_AdmitsPastTheDissolveGate()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("dissolve_off");

            var alpha =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutAlpha(
                    CaptureCutoutEvidence(material));

            // The shipped default (0, 0, 0.5, 0.1) is mode zero and inert;
            // only the gate pass is under test here.
            Assert.That(
                alpha.IsComplete,
                Is.True,
                "dissolve mode zero must not refuse the interpretation");
        }

        // --- 6. main UV scroll/rotate -------------------------------------------

        [TestCase(0.1f, 0f, 0f, 0f)]
        [TestCase(0f, 0.1f, 0f, 0f)]
        [TestCase(0f, 0f, 0.1f, 0f)]
        [TestCase(0f, 0f, 0f, 0.1f)]
        public void NonZeroScrollRotateComponent_IsUnknownNamingScrollRotate(
            float x,
            float y,
            float z,
            float w)
        {
            var material = NewGateOffMaterialWithOpaqueTexture("scroll");
            material.SetVector(ScrollRotateProperty, new Vector4(x, y, z, w));

            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // Falsifies: treating scroll/rotate as an RGB-only effect — any
            // nonzero component, including w, moves the sampling coordinate.
            AssertAlphaGateUnknown(result, ScrollRotateProperty);
        }

        // --- 7. texture-evidence boundaries at the resolver seam ---------------

        [Test]
        public void TransparentTexelOnlyInALowerMip_ForcesMustRemainTransparent()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("mip_transparent");

            var resolution = ResolveThroughCutoutFrontend(
                material, OpaqueThenTransparentChain());

            // Falsifies: mip-0-only checks — the hardware may sample any
            // level, and one transparent lower level refutes the whole proof.
            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(CornerTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void ImportedOddBoundaryMipChain_RefusesMipZeroOpaqueTriangle()
        {
            var texture = ImportMipmapTexture(
                "real_odd_boundary",
                8,
                8,
                OddBoundaryAlphaPixels(),
                FilterMode.Point,
                TextureWrapMode.Clamp);
            var material = NewGateOffMaterialWithOpaqueTexture(
                "real_odd_boundary_material");
            material.SetTexture(MainTextureProperty, texture);

            Assert.That(
                UnityAlphaFieldEvidence.TryCapture(texture, out _, out var chain),
                Is.True);
            Assert.That(chain.Count, Is.EqualTo(texture.mipmapCount));
            Assert.That(
                chain[0].GetAlpha(4, 0),
                Is.EqualTo(255),
                "Mip 0 alone would prove the tested triangle opaque.");
            Assert.That(
                chain[1].GetAlpha(2, 0),
                Is.Not.EqualTo(255),
                "The imported lower mip must cover that texel with non-opaque alpha.");

            var resolution = ResolveThroughCutoutFrontend(material, chain);

            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(
                resolution.Classify(MipZeroOpaqueTexelTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        [Test]
        public void
            BilinearFootprintReachesHalfATexelOutsideTheHull_PointDoesNot()
        {
            var pointMaterial = NewCutoutFixtureMaterial();
            pointMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "footprint_point",
                    4,
                    4,
                    OpaqueGridWithTransparentTexel(3, 0),
                    FilterMode.Point,
                    TextureWrapMode.Clamp));
            var bilinearMaterial = NewCutoutFixtureMaterial();
            bilinearMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "footprint_bilinear",
                    4,
                    4,
                    OpaqueGridWithTransparentTexel(3, 0),
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp));

            var chain = Chain(OpaqueGridWithTransparentTexelAlpha(3, 0));
            var pointResolution = ResolveThroughCutoutFrontend(
                pointMaterial, chain);
            var bilinearResolution = ResolveThroughCutoutFrontend(
                bilinearMaterial, chain);

            // Falsifies: hull-only classification — the bilinear footprint is
            // the hull dilated by half a texel, so the same placement flips
            // ProvenOpaque to MustRemainTransparent under bilinear filtering.
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
        public void
            RepeatWrapCatchesATransparentTexelAcrossTheSeam_ClampFlipsConsistently()
        {
            var repeatMaterial = NewCutoutFixtureMaterial();
            repeatMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "seam_repeat",
                    4,
                    4,
                    OpaqueGridWithTransparentTexel(0, 0),
                    FilterMode.Point,
                    TextureWrapMode.Repeat));
            var clampMaterial = NewCutoutFixtureMaterial();
            clampMaterial.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "seam_clamp",
                    4,
                    4,
                    OpaqueGridWithTransparentTexel(0, 0),
                    FilterMode.Point,
                    TextureWrapMode.Clamp));

            var chain = Chain(OpaqueGridWithTransparentTexelAlpha(0, 0));
            var repeatResolution = ResolveThroughCutoutFrontend(
                repeatMaterial, chain);
            var clampResolution = ResolveThroughCutoutFrontend(
                clampMaterial, chain);

            // Falsifies: wrap-agnostic hull checks — under Repeat the
            // seam-crossing hull wraps into the transparent texel; under
            // Clamp the same triangle pins into an opaque column and the
            // verdict flips consistently.
            Assert.That(
                repeatResolution.Classify(SeamCrossingTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent),
                "Repeat wraps the hull into the transparent texel");
            Assert.That(
                clampResolution.Classify(SeamCrossingTriangle()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                "Clamp pins the same hull away from the transparent texel");
        }

        [Test]
        public void TrilinearFilterImport_RefusesSamplingAtTheInterpretation()
        {
            var material = NewCutoutFixtureMaterial();
            material.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "trilinear",
                    4,
                    4,
                    SolidGrid(4, 4, 255),
                    FilterMode.Trilinear));

            var captured = CaptureCutoutEvidence(material);
            Assert.That(
                captured.TryGetTexture(MainTextureProperty, out var assignment),
                Is.True);
            Assert.That(
                assignment.Texture.HasSampling,
                Is.False,
                "capture admits Point/Bilinear x Clamp/Repeat only, so " +
                "trilinear arrives as missing sampling evidence");

            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // Falsifies: defaulting unsupported sampling to a supported mode.
            // Written for GREEN behavior: the sample construction refuses
            // with a diagnostic naming _MainTex. On the Step 2 scaffold this
            // is RED for the scaffold's own reason — the texture-backed arm
            // returns Unknown without ever reading texture evidence — and
            // the scaffold RED of the group otherwise comes from the
            // completing arms above.
            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.False,
                "unsupported sampling must never complete");
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha)
                    .Any(d => d.Detail.Contains(MainTextureProperty)),
                Is.True,
                "expected an alpha diagnostic naming _MainTex");
        }

        [Test]
        public void MismatchedWrapImport_RefusesSamplingAtTheInterpretation()
        {
            var material = NewCutoutFixtureMaterial();
            material.SetTexture(
                MainTextureProperty,
                ImportMipmapTexture(
                    "mismatched_wrap",
                    4,
                    4,
                    SolidGrid(4, 4, 255),
                    FilterMode.Point,
                    TextureWrapMode.Repeat,
                    importer =>
                    {
                        var settings = new TextureImporterSettings();
                        importer.ReadTextureSettings(settings);
                        settings.wrapModeU = TextureWrapMode.Repeat;
                        settings.wrapModeV = TextureWrapMode.Clamp;
                        importer.SetTextureSettings(settings);
                    }));

            var captured = CaptureCutoutEvidence(material);
            Assert.That(
                captured.TryGetTexture(MainTextureProperty, out var assignment),
                Is.True);
            Assert.That(
                assignment.Texture.HasSampling,
                Is.False,
                "mismatched wrapU/wrapV arrive as missing sampling evidence");

            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // Falsifies: reading only wrapMode and ignoring a U/V mismatch —
            // the sampler is not expressible in the closed vocabulary.
            // Written for GREEN behavior; on the scaffold this is RED for
            // the scaffold's own reason (see the trilinear note above).
            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.False,
                "unsupported sampling must never complete");
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha)
                    .Any(d => d.Detail.Contains(MainTextureProperty)),
                Is.True,
                "expected an alpha diagnostic naming _MainTex");
        }

        [Test]
        public void UnassignedMainTex_IsRefusedAndNeverBecomesAConstant()
        {
            var material = NewCutoutFixtureMaterial();

            var captured = CaptureCutoutEvidence(material);
            Assert.That(
                captured.TryGetTexture(MainTextureProperty, out var assignment),
                Is.True);
            Assert.That(
                assignment.IsAssigned,
                Is.False,
                "the stand-in default leaves _MainTex unassigned");

            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // Falsifies: the Poiyomi constant-fallback for an unassigned
            // _MainTex — the cutout pass samples _MainTex unconditionally, so
            // absence is unsupported, never a constant alpha.
            // Written for GREEN behavior; on the scaffold this is RED for
            // the scaffold's own reason (see the trilinear note above).
            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.False,
                "an unassigned main texture must never complete");
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha)
                    .Any(d => d.Detail.Contains(MainTextureProperty)),
                Is.True,
                "expected an alpha diagnostic naming _MainTex");
        }

        [Test]
        public void
            TextureWithoutResolvableSourceIdentity_IsRefusedAndNeverBecomesAConstant()
        {
            var material = NewCutoutFixtureMaterial();
            material.SetTexture(
                MainTextureProperty,
                Track(new Texture2D(4, 4, TextureFormat.RGBA32, false)));

            var captured = CaptureCutoutEvidence(material);
            Assert.That(
                captured.TryGetTexture(MainTextureProperty, out var assignment),
                Is.True);
            Assert.That(
                assignment.Texture.HasSourceIdentity,
                Is.False,
                "a runtime texture has no stable project identity");

            var result =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures);

            // Falsifies: fabricating identity from instance id or path — a
            // texture whose source cannot be resolved is refused, never
            // sampled and never turned into a constant.
            // Written for GREEN behavior; on the scaffold this is RED for
            // the scaffold's own reason (see the trilinear note above).
            Assert.That(
                result.Semantics.Alpha.IsComplete,
                Is.False,
                "unresolvable source identity must never complete");
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha)
                    .Any(d => d.Detail.Contains(MainTextureProperty)),
                Is.True,
                "expected an alpha diagnostic naming _MainTex");
        }

        // --- 8. feature-variation invariance ------------------------------------

        [Test]
        public void AlphaVerdict_IsInvariantUnderFeaturesAndColorSpace()
        {
            var material = NewGateOffMaterialWithOpaqueTexture("invariance");

            var withAllFeatures =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, AllFeatures).Semantics.Alpha;
            var unrelatedSuperset = new string[AllFeatures.Length + 1];
            Array.Copy(
                AllFeatures, unrelatedSuperset, AllFeatures.Length);
            unrelatedSuperset[AllFeatures.Length] =
                "LIL_FEATURE_UNRELATED_SUPERSET_MEMBER";
            var withSuperset =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, unrelatedSuperset).Semantics.Alpha;
            var withEmptySet =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Linear, Array.Empty<string>()).Semantics.Alpha;
            var withGamma =
                LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutMaterial(
                    material, ColorSpace.Gamma, AllFeatures).Semantics.Alpha;

            // Falsifies callback-100 and color-conversion dependence: the
            // verdict is a function of captured runtime alpha gates, never
            // of the compiled define set or project color space.
            Assert.That(
                withSuperset,
                Is.EqualTo(withAllFeatures),
                "an unrelated superset must not change the alpha output");
            Assert.That(
                withEmptySet,
                Is.EqualTo(withAllFeatures),
                "an empty define set must not change the alpha output");
            Assert.That(
                withGamma,
                Is.EqualTo(withAllFeatures),
                "color space must not change the cutout alpha output");
        }
    }
}
