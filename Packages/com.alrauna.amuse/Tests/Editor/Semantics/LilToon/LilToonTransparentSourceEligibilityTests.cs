using System;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// The transparent source-eligibility gates (design §9). Gates 1-11 are
    /// the merged cutout rules unchanged; gates 12-15 are family-specific and
    /// each row below names the incorrect implementation it falsifies.
    /// <para>
    /// Eligible stand-ins are materials of the transparent stand-in shader,
    /// whose fresh defaults are the positive baseline: queue 2460,
    /// RenderType TransparentCutout, _Cutoff 0.5, _AlphaBoostFA 10,
    /// _SubpassCutoff 0.5, _DistanceFade.z 0, _DstBlend 10.
    /// </para>
    /// </summary>
    public sealed class LilToonTransparentSourceEligibilityTests
        : LilToonFixtureTestBase
    {
        /// <summary>
        /// The 21 scalars this module reads off the source: the 18 recipe
        /// names plus _Cutoff, _AlphaBoostFA and _SubpassCutoff. Stated
        /// literally; a test that read production would let a wrong schema
        /// test itself.
        /// </summary>
        private static readonly string[] ExpectedEligibilityScalars =
        {
            "_SrcBlend", "_DstBlend", "_AlphaToMask", "_ZWrite", "_ZTest",
            "_OffsetFactor", "_OffsetUnits", "_ColorMask",
            "_SrcBlendAlpha", "_DstBlendAlpha", "_BlendOp", "_BlendOpAlpha",
            "_SrcBlendFA", "_DstBlendFA", "_SrcBlendAlphaFA",
            "_DstBlendAlphaFA", "_BlendOpFA", "_BlendOpAlphaFA",
            "_Cutoff", "_AlphaBoostFA", "_SubpassCutoff",
        };

        private static CapturedMaterialEvidence Capture(Material material)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    LilToonTransparentSourceEligibility
                        .ConversionEvidenceRequest),
            })[0];
        }

        private static LilToonOpaqueConversionEligibility EvaluateFor(
            Material material)
        {
            LilToonOpaqueTarget.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            return LilToonTransparentSourceEligibility
                .EvaluateVerifiedEligibility(
                    Capture(material), queue, renderType);
        }

        private static void AssertRefusal(
            LilToonOpaqueConversionEligibility result,
            LilToonOpaqueConversionRefusal expected)
        {
            Assert.That(
                result.Outcome,
                Is.EqualTo(LilToonOpaqueConversionOutcome.Refused));
            Assert.That(result.Refusal, Is.EqualTo(expected));
        }

        private static void AssertConvertible(
            LilToonOpaqueConversionEligibility result)
        {
            Assert.That(
                result.Outcome,
                Is.EqualTo(LilToonOpaqueConversionOutcome.Convertible),
                "refusal was " + result.Refusal);
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
        public void SourceEvidenceRequest_IsExactlyTheFourSourceProperties()
        {
            var request =
                LilToonTransparentSourceEligibility.SourceEvidenceRequest;

            CollectionAssert.AreEquivalent(
                new[] { "_Cutoff", "_AlphaBoostFA", "_SubpassCutoff" },
                request.ScalarProperties);
            CollectionAssert.AreEquivalent(
                new[] { "_Cutoff", "_AlphaBoostFA", "_SubpassCutoff" },
                request.PresenceProperties);
            CollectionAssert.AreEqual(
                new[] { "_DistanceFade" }, request.VectorProperties);
            Assert.That(request.ColorProperties, Is.Empty);
            Assert.That(request.TextureProperties, Is.Empty);
        }

        [Test]
        public void EligibilitySchema_IsTheRecipePlusTheThreeSourceScalars()
        {
            CollectionAssert.AreEquivalent(
                ExpectedEligibilityScalars,
                LilToonTransparentSourceEligibility
                    .EligibilitySchemaProperties);
        }

        [Test]
        public void SupportedQueueAndRenderType_AreTheTransparentDefaults()
        {
            Assert.That(
                LilToonTransparentSourceEligibility
                    .SupportedTransparentRenderQueue,
                Is.EqualTo(2460));
            Assert.That(
                LilToonTransparentSourceEligibility
                    .SupportedTransparentRenderType,
                Is.EqualTo("TransparentCutout"));
        }

        [Test]
        public void MaxProvableCutoff_IsOne_NotTheCutoutTwiceMargin()
        {
            // Copy detector: 0.9999 here would silently refuse every
            // material authored at exactly 1.
            Assert.That(
                LilToonTransparentSourceEligibility.MaxProvableCutoff,
                Is.EqualTo(1f));
        }

        [Test]
        public void FreshTransparentStandIn_IsConvertible()
        {
            AssertConvertible(EvaluateFor(NewTransparentFixtureMaterial()));
        }

        // --- gates 3-11: the merged cutout rules, unchanged (row 17) ------

        [Test]
        public void CustomRenderQueue_RefusesUnsupportedRenderQueue()
        {
            var material = NewTransparentFixtureMaterial();
            material.renderQueue = 2475;

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedRenderQueue);
        }

        [Test]
        public void CustomRenderType_RefusesUnsupportedRenderType()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetOverrideTag("RenderType", "Transparent");

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedRenderType);
        }

        [TestCase("_ZTest", 8f,
            LilToonOpaqueConversionRefusal.UnsupportedDepthComparison)]
        [TestCase("_ZWrite", 0f,
            LilToonOpaqueConversionRefusal.UnsupportedDepthWrite)]
        [TestCase("_ColorMask", 7f,
            LilToonOpaqueConversionRefusal.UnsupportedColorMask)]
        [TestCase("_OffsetFactor", -1f,
            LilToonOpaqueConversionRefusal.UnsupportedDepthOffset)]
        [TestCase("_OffsetUnits", -1f,
            LilToonOpaqueConversionRefusal.UnsupportedDepthOffset)]
        [TestCase("_BlendOp", 2f,
            LilToonOpaqueConversionRefusal.UnsupportedBlendEquation)]
        [TestCase("_DstBlend", 5f,
            LilToonOpaqueConversionRefusal.UnsupportedBlendEquation)]
        [TestCase("_BlendOpAlpha", 2f,
            LilToonOpaqueConversionRefusal.UnsupportedAlphaBlendEquation)]
        [TestCase("_BlendOpFA", 0f,
            LilToonOpaqueConversionRefusal
                .UnsupportedForwardAddBlendEquation)]
        [TestCase("_DstBlendFA", 0f,
            LilToonOpaqueConversionRefusal
                .UnsupportedForwardAddBlendEquation)]
        public void AuthoredRenderState_RefusesWithTheExactlyNamedRefusal(
            string property,
            float value,
            object expected)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat(property, value);

            // Falsifies: silently normalizing authored render state the
            // alpha proof does not preserve. _BlendOpFA = Add in particular
            // would double-composite ForwardAdd against the base pass.
            AssertRefusal(
                EvaluateFor(material),
                (LilToonOpaqueConversionRefusal)expected);
        }

        [Test]
        public void TransparentDstBlendDefault_IsAdmittedByGateNine()
        {
            var material = NewTransparentFixtureMaterial();

            // OneMinusSrcAlpha evaluates to 0 at alpha 1, so the canonical
            // transparent default is already admitted; the recipe's 10 -> 0
            // write is an identity there (T1 §7).
            Assert.That(material.GetFloat("_DstBlend"), Is.EqualTo(10f));
            AssertConvertible(EvaluateFor(material));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void NonFiniteScalar_RefusesConversionPropertyNotFinite(
            float value)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_ZTest", value);

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
        }

        [Test]
        public void MaterialMissingTheRecipe_RefusesConversionPropertyAbsent()
        {
            // The plain semantic stand-in declares no render state at all.
            AssertRefusal(
                EvaluateFor(NewFixtureMaterial()),
                LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
        }

        // --- gate 12: the transparent cutoff bound (row 4) ----------------

        [TestCase(0.5f)]
        [TestCase(0.9999f)]
        [TestCase(1.0f)]
        public void CutoffAtOrBelowOne_IsConvertible(float cutoff)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_Cutoff", cutoff);

            AssertConvertible(EvaluateFor(material));
        }

        [Test]
        public void CutoffAboveOne_RefusesClipThresholdDiscardsOpaqueAlpha()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_Cutoff", 1.001f);

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal
                    .ClipThresholdDiscardsOpaqueAlpha);
        }

        // --- gate 13: ForwardAdd premultiply (row 11) ---------------------

        [TestCase(1f)]
        [TestCase(10f)]
        public void AlphaBoostFaAtOrAboveOne_IsConvertible(float boost)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_AlphaBoostFA", boost);

            AssertConvertible(EvaluateFor(material));
        }

        [TestCase(0.5f)]
        [TestCase(0f)]
        public void AlphaBoostFaBelowOne_RefusesTheNamedForwardAddRefusal(
            float boost)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_AlphaBoostFA", boost);

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal
                    .UnsupportedForwardAddAlphaBoost);
        }

        // --- gate 14: distance fade (row 8) -------------------------------

        [Test]
        public void DistanceFadeEnabled_RefusesUnsupportedDistanceFade()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetVector(
                "_DistanceFade", new Vector4(0.1f, 0.01f, 0.5f, 0f));

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedDistanceFade);
        }

        [Test]
        public void DistanceFadeNonFinite_RefusesConversionPropertyNotFinite()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetVector(
                "_DistanceFade",
                new Vector4(0.1f, 0.01f, 0f, float.NaN));

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
        }

        // --- gate 15: the subpass shadow clip (row 5) ---------------------

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1.0f)]
        public void SubpassCutoffAtOrBelowOne_IsConvertible(float value)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_SubpassCutoff", value);

            // 0.5 is the shipped default: a bound tighter than the measured
            // slice-15 result loses the whole default population.
            AssertConvertible(EvaluateFor(material));
        }

        [Test]
        public void SubpassCutoffJustAboveOne_RefusesUnsupportedSubpassCutoff()
        {
            var material = NewTransparentFixtureMaterial();
            var subpassCutoff = NextFloatAbove(1f);

            // Degradation guard: the row depends on the step being exactly
            // one ulp, so a future runtime change must not silently turn
            // this falsifier into a no-op.
            Assert.That(subpassCutoff, Is.GreaterThan(1f));

            material.SetFloat("_SubpassCutoff", subpassCutoff);

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedSubpassCutoff);
        }

        [Test]
        public void NonFiniteSubpassCutoff_RefusesBeforeTheNamedGate()
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat("_SubpassCutoff", float.NaN);

            // Gate order is load-bearing: a NaN must not dress itself up as
            // a plausible named refusal.
            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
        }

        // --- deliberately ungated ------------------------------------------

        [TestCase("_AlphaToMask", 1f)]
        [TestCase("_SrcBlendAlphaFA", 1f)]
        [TestCase("_DstBlendAlphaFA", 0f)]
        [TestCase("_UseDither", 1f)]
        public void DeliberatelyUngatedProperty_StaysConvertible(
            string property,
            float value)
        {
            var material = NewTransparentFixtureMaterial();
            material.SetFloat(property, value);

            // Each is proven inert at a = 1 (T1 §4.4, §5.6, §6). A gate here
            // would be a free false negative.
            AssertConvertible(EvaluateFor(material));
        }

        [Test]
        public void CutoutEligibility_IsUnchangedByTheTransparentFamily()
        {
            var cutout = NewCutoutFixtureMaterial();
            cutout.SetFloat("_Cutoff", 1f);

            LilToonOpaqueTarget.ReadEffectiveRenderState(
                cutout, out var queue, out var renderType);
            var evidence = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    cutout,
                    LilToonCutoutSourceEligibility
                        .ConversionEvidenceRequest),
            })[0];

            // Falsifies: a parameterized gate list leaking the transparent
            // <= 1 bound into cutout, whose bound stays 0.9999.
            AssertRefusal(
                LilToonCutoutSourceEligibility.EvaluateVerifiedEligibility(
                    evidence, queue, renderType),
                LilToonOpaqueConversionRefusal
                    .ClipThresholdDiscardsOpaqueAlpha);
        }
    }
}
