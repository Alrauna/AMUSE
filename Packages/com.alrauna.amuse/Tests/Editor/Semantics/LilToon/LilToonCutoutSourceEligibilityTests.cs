using System.Linq;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Tests for the cutout source-eligibility module: its bound, its
    /// evidence request, and the gates that authorize the canonical clone.
    /// <para>
    /// The expected eligibility schema is stated literally here and never
    /// derived from the production constants. A test that read its
    /// expectation from the production schema would let a wrong split test
    /// itself.
    /// </para>
    /// <para>
    /// The cutout stand-in shader does not declare the 18 recipe properties
    /// (it is the cutout contract, not the opaque one), so the
    /// conversion-eligible stand-ins are materials of the opaque stand-in
    /// shader <c>LilToonOpaqueConversionTest</c>, whose fresh defaults are
    /// canonical except <c>_AlphaToMask</c> (1, deliberately ungated) and
    /// which carries <c>_Cutoff = 0.5</c>. The cutout stand-in still plays
    /// itself where its missing tuple is the point: the
    /// <c>ConversionPropertyAbsent</c> row.
    /// </para>
    /// </summary>
    public sealed class LilToonCutoutSourceEligibilityTests : LilToonFixtureTestBase
    {
        /// <summary>
        /// The 19 properties conversion reads: the 18 canonical recipe
        /// properties plus the eligibility-only <c>_Cutoff</c>, which the
        /// recipe never writes.
        /// </summary>
        private static readonly string[] ExpectedConversionSchema =
        {
            "_SrcBlend", "_DstBlend", "_AlphaToMask", "_ZWrite", "_ZTest",
            "_OffsetFactor", "_OffsetUnits", "_ColorMask",
            "_SrcBlendAlpha", "_DstBlendAlpha", "_BlendOp", "_BlendOpAlpha",
            "_SrcBlendFA", "_DstBlendFA", "_SrcBlendAlphaFA", "_DstBlendAlphaFA",
            "_BlendOpFA", "_BlendOpAlphaFA",
            "_Cutoff",
        };

        // --- Request shape -----------------------------------------------------

        [Test]
        public void MaxProvableCutoff_IsTheControllerFixedTwiceMargin()
        {
            Assert.That(
                LilToonCutoutSourceEligibility.MaxProvableCutoff,
                Is.EqualTo(0.9999f));
        }

        [Test]
        public void EligibilitySchema_MatchesTheIndependentlyStatedSchema()
        {
            var actual =
                LilToonCutoutSourceEligibility.EligibilitySchemaProperties;

            Assert.That(actual.Count, Is.EqualTo(19));
            CollectionAssert.AreEquivalent(ExpectedConversionSchema, actual);
        }

        [Test]
        public void ConversionEvidenceRequest_RequestsExactlyTheConversionSchema()
        {
            var request =
                LilToonCutoutSourceEligibility.ConversionEvidenceRequest;

            Assert.That(request.ShaderName, Is.True);
            Assert.That(request.ActiveColorSpace, Is.False);
            Assert.That(request.PresenceProperties.Count, Is.EqualTo(19));
            Assert.That(request.ScalarProperties.Count, Is.EqualTo(19));
            Assert.That(request.ScalarProperties, Has.Member("_Cutoff"));
            // lilToon's conversion path has no locked-flag scalar: the
            // request must not grow a Poiyomi-style
            // _ShaderOptimizerEnabled-equivalent without a design change.
            Assert.That(request.ScalarProperties, Has.No.Member("_ShaderOptimizerEnabled"));
            CollectionAssert.AreEquivalent(
                ExpectedConversionSchema, request.PresenceProperties.ToArray());
            CollectionAssert.AreEquivalent(
                ExpectedConversionSchema, request.ScalarProperties.ToArray());
            Assert.That(request.ColorProperties, Is.Empty);
            Assert.That(request.VectorProperties, Is.Empty);
            Assert.That(request.TextureProperties, Is.Empty);
        }

        /// <summary>
        /// The cutout source owns exactly one property. Falsifies a split
        /// that left _Cutoff on the target, and a split that widened the
        /// source request with recipe render state it reads but does not own.
        /// </summary>
        [Test]
        public void SourceEvidenceRequest_IsExactlyCutoff()
        {
            var request = LilToonCutoutSourceEligibility.SourceEvidenceRequest;

            CollectionAssert.AreEqual(
                new[] { "_Cutoff" }, request.PresenceProperties);
            CollectionAssert.AreEqual(
                new[] { "_Cutoff" }, request.ScalarProperties);
            Assert.That(request.ColorProperties, Is.Empty);
            Assert.That(request.VectorProperties, Is.Empty);
            Assert.That(request.TextureProperties, Is.Empty);
        }

        /// <summary>
        /// The combined object both the capture schema and the conversion
        /// boundary read is still the same 19 properties the single request
        /// carried before the split: the split must not change what is
        /// captured, only who owns each half.
        /// </summary>
        [Test]
        public void ConversionEvidenceRequest_IsTheRecipePlusCutoff()
        {
            var request =
                LilToonCutoutSourceEligibility.ConversionEvidenceRequest;

            Assert.That(request.ShaderName, Is.True);
            Assert.That(request.ScalarProperties.Count, Is.EqualTo(19));
            CollectionAssert.AreEquivalent(
                ExpectedConversionSchema, request.ScalarProperties);
            CollectionAssert.AreEquivalent(
                ExpectedConversionSchema, request.PresenceProperties);
        }

        // --- Eligibility matrix -----------------------------------------------

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

        private static CapturedMaterialEvidence CaptureConversion(Material material)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    LilToonCutoutSourceEligibility.ConversionEvidenceRequest),
            })[0];
        }

        private static LilToonOpaqueConversionEligibility EvaluateFor(Material material)
        {
            LilToonOpaqueTarget.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            return LilToonCutoutSourceEligibility.EvaluateVerifiedEligibility(
                CaptureConversion(material), queue, renderType);
        }

        /// <summary>
        /// Substitutes one captured scalar before evaluating. The evaluator is
        /// pure over captured evidence, so inputs it must handle need not be
        /// reachable by writing a live material - this is how non-finite
        /// values are supplied, using the existing evidence primitive rather
        /// than a new seam.
        /// </summary>
        private static LilToonOpaqueConversionEligibility EvaluateWith(
            Material material, string property, float value)
        {
            LilToonOpaqueTarget.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            return LilToonCutoutSourceEligibility.EvaluateVerifiedEligibility(
                CaptureConversion(material).WithScalar(property, value),
                queue,
                renderType);
        }

        /// <summary>
        /// The conversion-eligible stand-in is the schema-complete cutout
        /// source. Its defaults are canonical except
        /// <c>_AlphaToMask = 1</c>, which conversion writes but deliberately
        /// does not gate (spec §9.3).
        /// </summary>
        private Material ConversionEligibleStandIn()
        {
            return NewCutoutFixtureMaterial();
        }

        /// <summary>
        /// RED against the Task 3 Step 1 refuse-all scaffold: the scaffold
        /// answers <c>ConversionPropertyNotFinite</c> for every schema-complete
        /// input, so this fails behaviorally until Step 3 implements the
        /// gates.
        /// </summary>
        [Test]
        public void CanonicalDefaultStandIn_IsConvertible()
        {
            AssertConvertible(EvaluateFor(ConversionEligibleStandIn()));
        }

        [Test]
        public void CustomRenderQueue_RefusesBeforeCanonicalization()
        {
            var material = ConversionEligibleStandIn();
            material.renderQueue = 3000;

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedRenderQueue);
        }

        [Test]
        public void CustomRenderType_RefusesBeforeCanonicalization()
        {
            var material = ConversionEligibleStandIn();
            material.SetOverrideTag("RenderType", "Transparent");

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedRenderType);
        }

        /// <summary>
        /// <c>0.9999</c> is exactly the conversion-stage bound (gate 12, B2
        /// gap 4); Poiyomi's <c>&lt;= 1</c> rule must not be reused.
        /// </summary>
        [Test]
        public void ClipThresholdAtMaxProvableCutoff_IsConvertible()
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat(
                "_Cutoff", LilToonCutoutSourceEligibility.MaxProvableCutoff);

            AssertConvertible(EvaluateFor(material));
        }

        [Test]
        public void ClipThresholdAtOne_Refuses()
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat("_Cutoff", 1f);

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ClipThresholdDiscardsOpaqueAlpha);
        }

        /// <summary>
        /// 1.001 mirrors the vendor-declared maximum scenario: a declared
        /// range constrains the inspector widget, not what renders, and any
        /// threshold above the bound discards alpha exactly 1.
        /// </summary>
        [Test]
        public void ClipThresholdBeyondTheBound_Refuses()
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat("_Cutoff", 1.001f);

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ClipThresholdDiscardsOpaqueAlpha);
        }

        /// <summary>
        /// Gate order is load-bearing: finiteness (gate 2) precedes the clip
        /// gate (gate 12), so a non-finite cutoff is caught by the finiteness
        /// gate and named <c>ConversionPropertyNotFinite</c>, not
        /// <c>ClipThresholdDiscardsOpaqueAlpha</c>. (NaN would also fail the
        /// gate-12 comparison, but it must never reach it.)
        /// </summary>
        [Test]
        public void NonFiniteClipThreshold_RefusesAsNotFinite()
        {
            var material = ConversionEligibleStandIn();

            AssertRefusal(
                EvaluateWith(material, "_Cutoff", float.NaN),
                LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
        }

        // --- Per-gate refusal matrix -----------------------------------------

        /// <summary>
        /// Gates 3-9 with their named refusal members, driven one perturbation
        /// at a time from the canonical-default stand-in so each row
        /// exercises exactly its own gate. These are the Step 3 GREEN
        /// targets: against the Step 1 refuse-all scaffold every row fails
        /// with <c>ConversionPropertyNotFinite</c> instead of its named
        /// refusal.
        /// </summary>
        [TestCase("_ZTest", 3f, LilToonOpaqueConversionRefusal.UnsupportedDepthComparison)]
        [TestCase("_ZWrite", 0f, LilToonOpaqueConversionRefusal.UnsupportedDepthWrite)]
        [TestCase("_ColorMask", 14f, LilToonOpaqueConversionRefusal.UnsupportedColorMask)]
        [TestCase("_OffsetFactor", 1f, LilToonOpaqueConversionRefusal.UnsupportedDepthOffset)]
        [TestCase("_OffsetUnits", 1f, LilToonOpaqueConversionRefusal.UnsupportedDepthOffset)]
        [TestCase("_BlendOp", 1f, LilToonOpaqueConversionRefusal.UnsupportedBlendEquation)]
        [TestCase("_SrcBlend", 2f, LilToonOpaqueConversionRefusal.UnsupportedBlendEquation)]
        [TestCase("_DstBlend", 5f, LilToonOpaqueConversionRefusal.UnsupportedBlendEquation)]
        [TestCase("_BlendOpAlpha", 1f, LilToonOpaqueConversionRefusal.UnsupportedAlphaBlendEquation)]
        [TestCase("_SrcBlendAlpha", 2f, LilToonOpaqueConversionRefusal.UnsupportedAlphaBlendEquation)]
        [TestCase("_DstBlendAlpha", 1f, LilToonOpaqueConversionRefusal.UnsupportedAlphaBlendEquation)]
        [TestCase("_SrcBlendFA", 2f, LilToonOpaqueConversionRefusal.UnsupportedForwardAddBlendEquation)]
        [TestCase("_DstBlendFA", 0f, LilToonOpaqueConversionRefusal.UnsupportedForwardAddBlendEquation)]
        [TestCase("_BlendOpFA", 0f, LilToonOpaqueConversionRefusal.UnsupportedForwardAddBlendEquation)]
        [TestCase("_BlendOpAlphaFA", 0f, LilToonOpaqueConversionRefusal.UnsupportedForwardAddBlendEquation)]
        public void NonCanonicalRenderState_RefusesWithTheNamedGate(
            string property, float value, object expected)
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat(property, value);

            AssertRefusal(
                EvaluateFor(material),
                (LilToonOpaqueConversionRefusal)expected);
        }

        /// <summary>
        /// The accepted classes inside gates 7-9 (spec §9.3): <c>_SrcBlend</c>
        /// 5 (SrcAlpha) is a unit source factor at alpha 1 and
        /// <c>_DstBlend</c> 10 (OneMinusSrcAlpha) a zero destination factor,
        /// so those states are blend-equivalent to the canonical tuple the
        /// clone writes; likewise <c>_DstBlendAlpha</c> 0.
        /// </summary>
        [TestCase("_SrcBlend", 5f)]
        [TestCase("_DstBlend", 10f)]
        [TestCase("_DstBlendAlpha", 0f)]
        public void OpaqueEquivalentBlendState_IsConvertible(
            string property, float value)
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat(property, value);

            AssertConvertible(EvaluateFor(material));
        }

        [Test]
        public void NonFiniteConversionProperty_RefusesAsNotFinite()
        {
            var material = ConversionEligibleStandIn();

            AssertRefusal(
                EvaluateWith(material, "_ZWrite", float.NaN),
                LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
        }

        /// <summary>
        /// The legacy semantic stand-in declares none of the 18 recipe
        /// properties, so the schema check refuses before any gate runs.
        /// </summary>
        [Test]
        public void MaterialWithoutTheConversionSchema_RefusesAsAbsent()
        {
            var material = NewFixtureMaterial();

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
        }

        /// <summary>
        /// Written but deliberately ungated (spec §9.3): <c>_AlphaToMask</c>
        /// admits under any state because full coverage holds at a ≡ 1
        /// [B2 §3.4], so the fresh cutout default 1 is the common case; and
        /// the attested cutout FORWARD_ADD declares its alpha blend pair as
        /// the literal <c>Zero One</c> [B2 §3.1], making
        /// <c>_SrcBlendAlphaFA</c>/<c>_DstBlendAlphaFA</c> unused by the
        /// compiled pass, so their values cannot affect the outcome.
        /// Reintroducing a gate on any of the three fails these rows.
        /// </summary>
        [TestCase("_AlphaToMask", 1f)]
        [TestCase("_SrcBlendAlphaFA", 1f)]
        [TestCase("_DstBlendAlphaFA", 0f)]
        public void UngatedProperties_DoNotAffectEligibility(
            string property, float value)
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat(property, value);

            AssertConvertible(EvaluateFor(material));
        }
    }
}
