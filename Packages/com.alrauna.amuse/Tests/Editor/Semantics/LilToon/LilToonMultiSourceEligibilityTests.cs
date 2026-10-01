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
    /// Task 5 mode-consistency gate reads that name: its outline-tone
    /// keyword derivation needs the outline shader identity. The declared
    /// render state of both stand-ins is the vendor cutout form the vendor
    /// editor writes at cutout mode: blend One/Zero, <c>_AlphaToMask</c> 1,
    /// RenderType TransparentCutout, queue 2450. The mode-1 keyword feature
    /// facts are exercised as runtime scalars through captured evidence,
    /// never as keyword reads: the disabled-mask control is the
    /// vendor-state fixture itself, whose <c>_AlphaMaskMode</c> holds the
    /// declared zero and whose keyword set is exactly the pinned
    /// derivation's output for that state.
    /// </para>
    /// <para>
    /// The fixtures landed at the phase A commit as a compile red against
    /// the missing <c>LilToonMultiSourceEligibility</c> type; the green
    /// step of 2026-10-01 landed the type, and the fixtures now run the
    /// real evaluator.
    /// </para>
    /// </summary>
    public sealed class LilToonMultiSourceEligibilityTests : LilToonFixtureTestBase
    {
        // The stand-ins carry the real container identities as their shader
        // names, `_lil/lilToonMulti` and `Hidden/lilToonMultiOutline`,
        // because the Task 5 mode-consistency gate reads the captured name:
        // only its outline-tone keyword derivation consumes the container
        // identity, and no eligibility row does.
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
        /// The refusal member is the 2026-10-01 green step's documented
        /// choice inside the closed cutout vocabulary: each mode-1 feature
        /// composes into the alpha or the coverage before the cutout clip,
        /// so the clip-proof member refuses each of them.
        /// </remarks>
        // --- Falsifier: a gate that treats the _COLOROVERLAY_ON keyword itself as proof of the mask, that skips the _AlphaMaskMode scalar, or that refuses with any member but the clip-proof one, fails this fixture. ---
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

            AssertRefusal(
                EvaluateMulti(material),
                LilToonOpaqueConversionRefusal
                    .ClipThresholdDiscardsOpaqueAlpha);
        }

        /// <summary>
        /// Dither is a runtime scalar gate with the exact-zero admitted
        /// state. The scalar here is the half the vendor derivation writes
        /// no keyword for: the derivation produces the dither keyword only
        /// at exactly one, so the captured set is the mode keyword alone,
        /// the mode-consistency gate admits, and the scalar itself must
        /// refuse. Any non-zero is on, exactly as the zero gates of the
        /// cutout interpretation read the feature.
        /// </summary>
        // --- Falsifier: a dither gate borrowed from the derivation's ==1 keyword condition (silently admitting the half), one that skips the _UseDither scalar, or one that refuses with any member but the clip-proof one, fails this fixture. ---
        [Test]
        public void DitherScalarOnAtCutoutModeRefuses()
        {
            var material =
                CutoutModeContainerMaterial(BaseContainerShaderPath);
            material.SetFloat("_UseDither", 0.5f);

            AssertRefusal(
                EvaluateMulti(material),
                LilToonOpaqueConversionRefusal
                    .ClipThresholdDiscardsOpaqueAlpha);
        }

        /// <summary>
        /// The layer dissolve is a runtime scalar gate on the x component of
        /// the params vector, dissolve mode zero exactly admitted. The
        /// scalar here is a non-integer mode the vendor derivation still
        /// writes its keyword for, so the captured set is the exact
        /// derivation output, the mode-consistency gate admits, and the
        /// scalar itself must refuse.
        /// </summary>
        // --- Falsifier: a dissolve gate that reads any component but x, that keys on an integer comparison instead of the exact-zero admitted state, that skips the vector scalar, or that refuses with any member but the clip-proof one, fails this fixture. ---
        [Test]
        public void DissolveScalarOnAtCutoutModeRefuses()
        {
            var material =
                CutoutModeContainerMaterial(BaseContainerShaderPath);
            material.SetVector(
                "_DissolveParams", new Vector4(0.5f, 0f, 0.5f, 0.1f));
            material.shaderKeywords = new[]
            {
                "UNITY_UI_ALPHACLIP", "GEOM_TYPE_BRANCH_DETAIL",
            };

            AssertRefusal(
                EvaluateMulti(material),
                LilToonOpaqueConversionRefusal
                    .ClipThresholdDiscardsOpaqueAlpha);
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
