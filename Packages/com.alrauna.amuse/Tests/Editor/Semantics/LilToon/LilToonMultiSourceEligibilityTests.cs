using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Falsifier fixtures for the Multi mode-1 (cutout) source eligibility:
    /// the evaluator that admits the vendor Multi cutout state on the two
    /// supported containers and refuses everything else, with the regular
    /// cutout evaluator held up as the parity oracle on every shared row.
    /// <para>
    /// Every fixture drives the production capture path over a committed
    /// container stand-in carrying the real container identity, because the
    /// outline rows key on the captured container name. The declared render
    /// state of both stand-ins is the vendor cutout form the vendor editor
    /// writes at cutout mode: blend One/Zero, <c>_AlphaToMask</c> 1,
    /// RenderType TransparentCutout, queue 2450. The mode-1 keyword feature
    /// facts are exercised as runtime scalars through captured evidence,
    /// never as keyword reads: the disabled-mask control is the
    /// vendor-state fixture itself, whose <c>_AlphaMaskMode</c> holds the
    /// declared zero and whose keyword set is exactly the pinned
    /// derivation's output for that state.
    /// </para>
    /// <para>
    /// Red against the missing <c>LilToonMultiSourceEligibility</c> type:
    /// every fixture references it, so the test assembly does not compile
    /// (CS0246) until the Task 10 green step lands the type.
    /// </para>
    /// </summary>
    public sealed class LilToonMultiSourceEligibilityTests : LilToonFixtureTestBase
    {
        // The stand-ins carry the real container identities as their shader
        // names, `_lil/lilToonMulti` and `Hidden/lilToonMultiOutline`,
        // because the eligibility rows key on the captured container name.
        private const string BaseContainerShaderPath =
            "Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/" +
            "LilToonMultiCutoutTest.shader";
        private const string OutlineContainerShaderPath =
            "Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/" +
            "LilToonMultiOutlineCutoutTest.shader";
        private const int CutoutMode = 1;

        // --- Shared fixture state ----------------------------------------------

        /// <summary>
        /// One committed container stand-in at the mode-1 vendor cutout
        /// state: the mode scalar at one, both Multi gate scalars at zero,
        /// and exactly the keyword set the pinned derivation produces for
        /// that state. The mask, dither, dissolve, distance fade, and
        /// outline tone features are all off, so the derived set is the
        /// mode keyword alone. The stand-in loads by its own asset path:
        /// the container identities are vendor-mimic names, and the
        /// resolution fixtures mint same-named temp shaders in their own
        /// runs, so a name lookup here could answer with the wrong asset.
        /// </summary>
        private Material CutoutModeContainerMaterial(string shaderAssetPath)
        {
            var shader =
                AssetDatabase.LoadAssetAtPath<Shader>(shaderAssetPath);
            Assert.That(shader, Is.Not.Null, shaderAssetPath);
            var material = Track(new Material(shader));
            material.SetFloat("_TransparentMode", 1f);
            material.SetFloat("_UseClippingCanceller", 0f);
            material.SetFloat("_AsOverlay", 0f);
            material.shaderKeywords = new[] { "UNITY_UI_ALPHACLIP" };
            return material;
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

        private static CapturedMaterialEvidence CaptureMultiConversion(
            Material material)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    LilToonMultiSourceEligibility.ConversionEvidenceRequest),
            })[0];
        }

        private static LilToonOpaqueConversionEligibility EvaluateMulti(
            Material material)
        {
            LilToonOpaqueTarget.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            return LilToonMultiSourceEligibility.EvaluateVerifiedEligibility(
                CaptureMultiConversion(material), queue, renderType,
                CutoutMode);
        }

        /// <summary>
        /// The parity twin leg, labeled: the regular cutout evaluator over
        /// the regular cutout stand-in, captured under the regular family's
        /// own conversion request.
        /// </summary>
        private static LilToonOpaqueConversionEligibility
            EvaluateRegularCutoutTwin(Material material)
        {
            LilToonOpaqueTarget.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    LilToonCutoutSourceEligibility.ConversionEvidenceRequest),
            })[0];
            return LilToonCutoutSourceEligibility
                .EvaluateVerifiedEligibility(captured, queue, renderType);
        }

        // --- Eligibility matrix -----------------------------------------------

        /// <summary>
        /// The vendor Multi cutout state at mode 1 on the base container is
        /// the admitted source state: blend One/Zero,
        /// <c>_AlphaToMask</c> 1, RenderType TransparentCutout, queue 2450.
        /// This fixture is also the disabled-mask control: the declared
        /// <c>_AlphaMaskMode</c> is the zero that proves the mask off, and
        /// the captured keyword set is exactly the derivation's output for
        /// it, so no feature fact may refuse here.
        /// </summary>
        // --- Falsifier: a mode-1 gate that refuses the vendor cutout state, or one that reads the alpha mask from anything but the _AlphaMaskMode scalar, fails this fixture. ---
        [Test]
        public void VendorCutoutStateAtCutoutModeOnTheBaseContainerIsConvertible()
        {
            var material =
                CutoutModeContainerMaterial(BaseContainerShaderPath);

            AssertConvertible(EvaluateMulti(material));
        }

        /// <summary>
        /// The vendor cutout form writes 2450; the opaque normalization's
        /// 2000 is an ordering and classification intent the cutout proof
        /// does not preserve, so mode 1 refuses it exactly as the regular
        /// cutout family refuses a custom queue.
        /// </summary>
        // --- Falsifier: a mode-1 gate that accepts queue 2000 in cutout mode fails this fixture. ---
        [Test]
        public void QueueTwoThousandAtCutoutModeRefusesAsUnsupportedRenderQueue()
        {
            var material =
                CutoutModeContainerMaterial(BaseContainerShaderPath);
            material.renderQueue = 2000;

            AssertRefusal(
                EvaluateMulti(material),
                LilToonOpaqueConversionRefusal.UnsupportedRenderQueue);
        }

        /// <summary>
        /// The alpha mask is a runtime scalar gate: the captured keyword set
        /// here is exactly the pinned derivation's output for
        /// <c>_AlphaMaskMode</c> one at mode 1, so the mode-consistency gate
        /// admits, and the mask scalar itself must refuse. The keyword
        /// never stands proof of the mask, and the scalar never hides
        /// behind it.
        /// </summary>
        /// <remarks>
        /// The refusal names the alpha mask inside the closed cutout
        /// vocabulary, and no existing member names it: the vocabulary
        /// deliberately does not widen for Multi. This fixture pins the
        /// refused outcome; the green step's member choice stays inside
        /// <see cref="LilToonOpaqueConversionRefusal"/>.
        /// </remarks>
        // --- Falsifier: a gate that treats the _COLOROVERLAY_ON keyword itself as proof of the mask, or that skips the _AlphaMaskMode scalar, fails this fixture. ---
        [Test]
        public void AlphaMaskScalarOnAtCutoutModeRefuses()
        {
            var material =
                CutoutModeContainerMaterial(BaseContainerShaderPath);
            material.SetFloat("_AlphaMaskMode", 1f);
            material.shaderKeywords = new[]
            {
                "UNITY_UI_ALPHACLIP", "_COLOROVERLAY_ON",
            };

            var result = EvaluateMulti(material);

            Assert.That(
                result.Outcome,
                Is.EqualTo(LilToonOpaqueConversionOutcome.Refused),
                "refusal was " + result.Refusal);
        }

        /// <summary>
        /// The cutoff and <see cref="LilToonCutoutSourceEligibility"/>
        /// <c>MaxProvableCutoff</c> premises carry over with the cutout
        /// vocabulary, and the parity oracle is a real differential on the
        /// shared surface. Twin leg: the regular cutout evaluator over the
        /// regular cutout stand-in. Multi leg: the Multi evaluator over the
        /// base container stand-in. Both legs carry the identical threshold
        /// state, and both must refuse with the same named refusal.
        /// </summary>
        // --- Falsifier: a mode-1 gate that admits a threshold above the bound, or that refuses it with anything but the cutout vocabulary, fails this fixture. ---
        [Test]
        public void ClipThresholdAboveTheBoundRefusesWithTheSameRefusalAsTheRegularTwin()
        {
            var twin = NewCutoutFixtureMaterial();
            twin.SetFloat("_Cutoff", 1f);
            var twinResult = EvaluateRegularCutoutTwin(twin);
            AssertRefusal(
                twinResult,
                LilToonOpaqueConversionRefusal.ClipThresholdDiscardsOpaqueAlpha);

            var multi = CutoutModeContainerMaterial(BaseContainerShaderPath);
            multi.SetFloat("_Cutoff", 1f);
            var multiResult = EvaluateMulti(multi);

            AssertRefusal(multiResult, twinResult.Refusal);
        }

        /// <summary>
        /// The outline container at mode 1 in plain cutout state is the same
        /// admitted source as its regular twin, and the parity oracle is
        /// outcome equality. The 2026-10-01 ruling pins why: the regular
        /// <c>Hidden/lilToonCutoutOutline</c> twin carries no
        /// outline-specific eligibility rows at this pin — the S8 outline
        /// support is attestation, classification, and target resolution
        /// only, and the wrapper converts through the plain cutout
        /// evaluator, whose schema names no <c>_Outline</c> property — so
        /// the Multi evaluator mirrors those plain rows with no outline row
        /// of its own. Twin leg, labeled: the regular cutout evaluator over
        /// the regular cutout stand-in, which is the evaluator the regular
        /// outline wrapper itself converts through. Multi leg, labeled: the
        /// Multi evaluator over the outline container stand-in set to the
        /// same scalar surface as the admitted twin state. The
        /// outline-alpha protection question (F0 §7.3) is a pre-existing
        /// regular-family scope fact; this fixture neither widens nor
        /// narrows it.
        /// </summary>
        // --- Falsifier: a Multi evaluator whose verdict diverges from the plain cutout evaluator's on identical plain-cutout state, or one that adds an outline-specific eligibility row, fails this fixture. ---
        [Test]
        public void OutlineContainerAtCutoutModeConvertsLikeTheRegularCutoutTwin()
        {
            var twin = NewCutoutFixtureMaterial();
            var twinResult = EvaluateRegularCutoutTwin(twin);
            AssertConvertible(twinResult);

            var multi = CutoutModeContainerMaterial(
                OutlineContainerShaderPath);
            // The same scalar surface as the supported regular cutout
            // stand-in, whose fresh alpha destination factor is the
            // OneMinusSrcAlpha class both evaluators admit.
            multi.SetFloat("_DstBlendAlpha", 10f);
            var multiResult = EvaluateMulti(multi);

            Assert.That(
                multiResult.Outcome,
                Is.EqualTo(twinResult.Outcome),
                "refusal was " + multiResult.Refusal);
        }

        /// <summary>
        /// The admitting-side parity oracle: a mode-1 base container with
        /// the same scalars as a supported regular cutout material is
        /// convertible to the same degree. Twin leg: the regular cutout
        /// evaluator over the regular cutout stand-in at its fresh
        /// canonical defaults. Multi leg: the Multi evaluator over the
        /// base container stand-in set to those same scalars. Equal
        /// outcomes and equal depth-test divergence - the Multi follower
        /// never widens or narrows the regular family's verdict.
        /// </summary>
        // --- Falsifier: a mode-1 gate whose verdict diverges from the regular twin on identical scalars fails this fixture. ---
        [Test]
        public void CutoutModeMultiAndTheRegularCutoutTwinAreConvertibleToTheSameDegree()
        {
            var twin = NewCutoutFixtureMaterial();
            var twinResult = EvaluateRegularCutoutTwin(twin);

            var multi = CutoutModeContainerMaterial(BaseContainerShaderPath);
            // The same scalar surface as the supported regular cutout
            // stand-in, whose fresh alpha destination factor is the
            // OneMinusSrcAlpha class both evaluators admit.
            multi.SetFloat("_DstBlendAlpha", 10f);
            var multiResult = EvaluateMulti(multi);

            AssertConvertible(twinResult);
            AssertConvertible(multiResult);
            Assert.That(
                multiResult.DepthTestDivergence,
                Is.EqualTo(twinResult.DepthTestDivergence));
        }
    }
}
