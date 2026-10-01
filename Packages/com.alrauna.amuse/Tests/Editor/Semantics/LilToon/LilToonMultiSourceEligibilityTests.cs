using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Falsifier fixtures for the Multi source eligibility: mode 1 (cutout)
    /// and mode 2 (transparent), on the two supported containers. The
    /// evaluator admits the vendor Multi state of the mode under evaluation
    /// and refuses everything else, with the regular cutout and transparent
    /// evaluators held up as the parity oracles on every shared row.
    /// <para>
    /// Every fixture drives the production capture path over a committed
    /// container stand-in carrying the real container identity, because the
    /// Task 5 mode-consistency gate reads that name: its outline-tone
    /// keyword derivation needs the outline shader identity. The declared
    /// render state of the cutout stand-ins is the vendor cutout form the
    /// vendor editor writes at cutout mode: blend One/Zero,
    /// <c>_AlphaToMask</c> 1, RenderType TransparentCutout, queue 2450. The
    /// declared render state of the transparent stand-ins is the vendor
    /// transparent form the vendor editor writes at transparent mode:
    /// blend One/OneMinusSrcAlpha on both pairs, <c>_AlphaToMask</c> 0,
    /// <c>_ZWrite</c> on, RenderType TransparentCutout, queue 2460. The
    /// keyword feature facts are exercised as runtime scalars through
    /// captured evidence, never as keyword reads: each disabled-mask
    /// control is a vendor-state fixture itself, whose keyword set is
    /// exactly the pinned derivation's output for that state - the mode
    /// keyword alone at cutout mode, <c>UNITY_UI_CLIP_RECT</c> at
    /// transparent mode.
    /// </para>
    /// <para>
    /// The mode-1 fixtures landed at their phase A commit as a compile red
    /// against the missing <c>LilToonMultiSourceEligibility</c> type; the
    /// green step of 2026-10-01 landed the type, and they now run the real
    /// evaluator. The mode-2 fixtures landed at their phase A commit as an
    /// assertion red: the shipped evaluator carries no mode-2 rows, so a
    /// mode-2 material falls into the mode-1 arm and refuses on the cutout
    /// queue row, or earlier on the mode-1 dither scalar row.
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
        private const string BaseTransparentContainerShaderPath =
            "Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/" +
            "LilToonMultiTransparentTest.shader";
        private const string OutlineTransparentContainerShaderPath =
            "Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/" +
            "LilToonMultiOutlineTransparentTest.shader";
        private const int CutoutMode = 1;
        private const int TransparentMode = 2;

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

        /// <summary>
        /// One committed container stand-in at the mode-2 vendor transparent
        /// state: the mode scalar at two, both Multi gate scalars at zero,
        /// and exactly the keyword set the pinned derivation produces for
        /// that state - <c>UNITY_UI_CLIP_RECT</c> alone, the alpha-clip
        /// keyword being cutout-only and no feature being on. The mask,
        /// dither, dissolve, distance fade, and outline tone features are
        /// all off, and the derivation writes no dither keyword at mode 2
        /// for any dither value, so the derived set stays the mode keyword.
        /// The stand-in loads by its own asset path for the same reason the
        /// cutout helper loads by path, compounded here: the two
        /// transparent stand-ins share the vendor-mimic container names
        /// with the cutout stand-ins, so a name lookup could answer with
        /// any of the four.
        /// </summary>
        private Material TransparentModeContainerMaterial(
            string shaderAssetPath)
        {
            var shader =
                AssetDatabase.LoadAssetAtPath<Shader>(shaderAssetPath);
            Assert.That(shader, Is.Not.Null, shaderAssetPath);
            var material = Track(new Material(shader));
            material.SetFloat("_TransparentMode", 2f);
            material.SetFloat("_UseClippingCanceller", 0f);
            material.SetFloat("_AsOverlay", 0f);
            material.shaderKeywords = new[] { "UNITY_UI_CLIP_RECT" };
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
            return EvaluateMultiAtMode(material, CutoutMode);
        }

        private static LilToonOpaqueConversionEligibility EvaluateMultiAtMode(
            Material material,
            int mode)
        {
            LilToonOpaqueTarget.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            return LilToonMultiSourceEligibility.EvaluateVerifiedEligibility(
                CaptureMultiConversion(material), queue, renderType, mode);
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

        /// <summary>
        /// The transparent parity twin leg, labeled: the regular transparent
        /// evaluator over the regular transparent stand-in, captured under
        /// the regular family's own conversion request. The 2026-10-01
        /// outline ruling makes this the evaluator the regular
        /// transparent-outline twin itself converts through.
        /// </summary>
        private static LilToonOpaqueConversionEligibility
            EvaluateRegularTransparentTwin(Material material)
        {
            LilToonOpaqueTarget.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    LilToonTransparentSourceEligibility
                        .ConversionEvidenceRequest),
            })[0];
            return LilToonTransparentSourceEligibility
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

        // --- Mode 2 (transparent) eligibility matrix --------------------------

        /// <summary>
        /// The vendor Multi transparent state at mode 2 on the base container
        /// is the admitted source state: blend One/OneMinusSrcAlpha on the
        /// RGB pair and the alpha pair, <c>_ZWrite</c> on, RenderType
        /// TransparentCutout, queue 2460 - the render state the vendor
        /// editor writes at the transparent branch of the mode map, whose
        /// Multi arm writes this RenderType and queue and whose shared block
        /// writes the blend pair and the depth state. The Multi transparent
        /// identity is Normal-class only, so the admitted state carries no
        /// <c>FORWARD_BACK</c> pre-pass and no <c>_PreCutoff</c>, and the
        /// transparent-only proof facts (<c>_AlphaBoostFA</c>,
        /// <c>_SubpassCutoff</c>, the all-zero distance-fade strength) hold
        /// at the vendor defaults. This fixture is also the disabled-feature
        /// control for the mode-2 rows: the captured keyword set is exactly
        /// the pinned derivation's output at mode 2,
        /// <c>UNITY_UI_CLIP_RECT</c> alone, so no feature fact may refuse
        /// here.
        /// </summary>
        // --- Falsifier: a mode-2 gate that refuses the vendor transparent state, or one that borrows the cutout queue row and refuses 2460, fails this fixture. ---
        [Test]
        public void
            VendorTransparentStateAtTransparentModeOnTheBaseContainerIsConvertible()
        {
            var material = TransparentModeContainerMaterial(
                BaseTransparentContainerShaderPath);

            AssertConvertible(
                EvaluateMultiAtMode(material, TransparentMode));
        }

        /// <summary>
        /// The vendor transparent form writes 2460 and the worlds build
        /// overrides to 3000; the opaque normalization's 2000 is an ordering
        /// and classification intent the transparent alpha proof does not
        /// preserve, so mode 2 refuses it exactly as the regular transparent
        /// family refuses a queue outside its admitted forms.
        /// </summary>
        // --- Falsifier: a mode-2 gate that accepts queue 2000 in transparent mode fails this fixture. ---
        [Test]
        public void QueueTwoThousandAtTransparentModeRefusesAsUnsupportedRenderQueue()
        {
            var material = TransparentModeContainerMaterial(
                BaseTransparentContainerShaderPath);
            material.renderQueue = 2000;

            AssertRefusal(
                EvaluateMultiAtMode(material, TransparentMode),
                LilToonOpaqueConversionRefusal.UnsupportedRenderQueue);
        }

        /// <summary>
        /// The worlds build's explicit 3000 write is the vendor map's other
        /// transparent form, and the regular transparent family admits the
        /// whole Transparent bucket beside its pinned default; mode 2
        /// admits the override beside the pinned 2460 on the same terms as
        /// its regular twin.
        /// </summary>
        // --- Falsifier: a mode-2 queue row that admits the pinned 2460 but refuses the worlds override 3000, or one that borrows the cutout row and refuses both, fails this fixture. ---
        [Test]
        public void WorldsOverrideQueueThreeThousandAtTransparentModeIsConvertible()
        {
            var material = TransparentModeContainerMaterial(
                BaseTransparentContainerShaderPath);
            material.renderQueue = 3000;

            AssertConvertible(
                EvaluateMultiAtMode(material, TransparentMode));
        }

        /// <summary>
        /// The distance fade is a runtime scalar gate at mode 2: at
        /// LIL_RENDER 2 the <c>_FADING_ON</c> keyword compiles the block
        /// that writes the fragment alpha after the clip
        /// (lil_common_frag.hlsl:2048), and the .z strength component is
        /// the gate, exactly as the regular transparent evaluator's
        /// distance-fade row gates it. The captured keyword set here is
        /// exactly the pinned derivation's output for a non-zero strength
        /// at mode 2 - <c>_FADING_ON</c> beside the mode keyword - so the
        /// mode-consistency gate admits, and the scalar itself must refuse.
        /// The keyword never stands proof of the fade, and the fade never
        /// hides behind its keyword.
        /// </summary>
        /// <remarks>
        /// The refusal member is the mirrored transparent row's own member,
        /// the closed conversion vocabulary's dedicated distance-fade
        /// refusal: the mirrored rows keep the transparent evaluator's
        /// members by reference, and no member is re-derived for Multi.
        /// </remarks>
        // --- Falsifier: a mode-2 gate that treats the _FADING_ON keyword itself as proof of the fade, that skips the _DistanceFade strength scalar, or that refuses with any member but the mirrored transparent row's own, fails this fixture. ---
        [Test]
        public void DistanceFadeStrengthOnAtTransparentModeRefuses()
        {
            var material = TransparentModeContainerMaterial(
                BaseTransparentContainerShaderPath);
            material.SetVector(
                "_DistanceFade", new Vector4(0.1f, 0.01f, 0.5f, 0f));
            material.shaderKeywords = new[]
            {
                "UNITY_UI_CLIP_RECT", "_FADING_ON",
            };

            AssertRefusal(
                EvaluateMultiAtMode(material, TransparentMode),
                LilToonOpaqueConversionRefusal.UnsupportedDistanceFade);
        }

        /// <summary>
        /// Dither is compiled out at LIL_RENDER 2 - the dither block exists
        /// only under LIL_RENDER == 1 (T1 §6 row 16) - so
        /// <c>_UseDither</c> is not a mode-2 gate, and the mirrored
        /// transparent rows keep no row for it. The scalar here is the
        /// full-on value, and the pinned derivation writes no dither keyword
        /// at mode 2 for any value, so the captured set is the mode keyword
        /// alone, the mode-consistency gate admits, and the scalar must not
        /// refuse. A dither gate borrowed from the mode-1 rows would be a
        /// free false negative.
        /// </summary>
        // --- Falsifier: a mode-2 gate that refuses on the _UseDither scalar - the compiled-out block - fails this fixture. ---
        [Test]
        public void DitherScalarOnAtTransparentModeStaysConvertible()
        {
            var material = TransparentModeContainerMaterial(
                BaseTransparentContainerShaderPath);
            material.SetFloat("_UseDither", 1f);

            AssertConvertible(
                EvaluateMultiAtMode(material, TransparentMode));
        }

        /// <summary>
        /// The Multi transparent identity is Normal-class only: it has no
        /// <c>FORWARD_BACK</c> pre-pass and no <c>_PreCutoff</c>, so the
        /// admitted mode-2 evidence carries no pre-pass fact of any kind,
        /// and no eligibility row may demand one. The material here is the
        /// admitted vendor transparent state over a stand-in that declares
        /// no <c>_PreCutoff</c> property at all, captured under a request
        /// that carries no pre-pass field either; a schema row or gate that
        /// demanded a pre-pass analogue would refuse this state as a missing
        /// fact.
        /// </summary>
        // --- Falsifier: a mode-2 gate that demands a _PreCutoff analogue or any FORWARD_BACK pre-pass fact, and refuses the missing pre-pass, fails this fixture. ---
        [Test]
        public void TransparentModeWithoutPrePassFactsIsConvertible()
        {
            var material = TransparentModeContainerMaterial(
                BaseTransparentContainerShaderPath);

            Assert.That(
                material.HasProperty("_PreCutoff"), Is.False,
                "the Normal-class Multi transparent state declares no " +
                "_PreCutoff; the stand-in must not grow one");

            AssertConvertible(
                EvaluateMultiAtMode(material, TransparentMode));
        }

        /// <summary>
        /// The outline container at mode 2 in plain transparent state is the
        /// same admitted source as its regular twin, and the parity oracle
        /// is outcome equality. The 2026-10-01 outline ruling pins why: the
        /// regular transparent-outline twin carries no outline-specific
        /// eligibility rows at this pin - the wrapper converts through the
        /// plain transparent evaluator, whose schema names no
        /// <c>_Outline</c> property - so the Multi evaluator mirrors those
        /// plain rows with no outline row of its own on either container.
        /// Twin leg, labeled: the regular transparent evaluator over the
        /// regular transparent stand-in, which is the evaluator the regular
        /// outline wrapper itself converts through. Multi leg, labeled: the
        /// Multi evaluator over the outline container stand-in at the same
        /// plain transparent scalar surface as the fresh twin state. The
        /// outline-alpha protection question (F0 §7.3) is a pre-existing
        /// regular-family scope fact; this fixture neither widens nor
        /// narrows it.
        /// </summary>
        // --- Falsifier: a Multi evaluator whose mode-2 verdict diverges from the plain transparent evaluator's on identical plain-transparent state, or one that adds an outline-specific eligibility row, fails this fixture. ---
        [Test]
        public void OutlineContainerAtTransparentModeConvertsLikeTheRegularTransparentTwin()
        {
            var twin = NewTransparentFixtureMaterial();
            var twinResult = EvaluateRegularTransparentTwin(twin);
            AssertConvertible(twinResult);

            var multi = TransparentModeContainerMaterial(
                OutlineTransparentContainerShaderPath);
            var multiResult =
                EvaluateMultiAtMode(multi, TransparentMode);

            Assert.That(
                multiResult.Outcome,
                Is.EqualTo(twinResult.Outcome),
                "refusal was " + multiResult.Refusal);
        }
    }
}
