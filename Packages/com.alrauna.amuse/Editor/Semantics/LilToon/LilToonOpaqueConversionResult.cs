using System;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// The outcome of a conversion decision about one material.
    /// <para>
    /// There is deliberately no <c>AlreadyOpaque</c> member, unlike the
    /// Poiyomi module's vocabulary. Conversion's input is an attested cutout
    /// source (spec §9.3): such a material is never canonical-opaque, and the
    /// attested opaque lilToon family maps to itself before conversion is ever
    /// consulted, so a no-op outcome is unreachable by construction. A member
    /// for an unreachable state would let a mis-wired gate classify a
    /// transparent material as "nothing to do" and silently skip conversion.
    /// </para>
    /// </summary>
    internal enum LilToonOpaqueConversionOutcome
    {
        Refused,

        /// <summary>
        /// A successful decision: the material may be normalized to its
        /// canonical Opaque counterpart by preparing a validated clone.
        /// </summary>
        Convertible,
    }

    /// <summary>
    /// Conversion decisions about one material. Deliberately a separate
    /// vocabulary from <c>RendererAnalysisRefusal</c>, which belongs to
    /// renderer-scoped alpha analysis: merging them would put conversion
    /// conditions where analysis reads them, so unknown conversion state would
    /// start refusing analysis that does not depend on it.
    /// <para>
    /// The members mirror the Poiyomi refusal lattice's shape minus the states
    /// unreachable on the attested lilToon alpha sources: there are no outline
    /// or premultiply members (the no-outline cutout slice has no outline
    /// pass, and cutout is not premultiplied alpha), and no
    /// <c>AlreadyOpaque</c>-related state. There is also no member for a
    /// generated-material read-back disagreement: that is an invariant
    /// failure, not an unsupported material, and
    /// <c>LilToonOpaqueTarget.PrepareCanonicalOpaqueClone</c>
    /// throws for it.
    /// </para>
    /// </summary>
    internal enum LilToonOpaqueConversionRefusal
    {
        None,

        // Identity
        UnattestedMaterial,

        // Schema / readability
        ConversionPropertyAbsent,
        ConversionPropertyNotFinite,

        // Effective render-state eligibility (spec §9.3 gates 3-12)
        UnsupportedRenderQueue,
        UnsupportedRenderType,
        UnsupportedDepthComparison,
        UnsupportedDepthWrite,
        UnsupportedColorMask,
        UnsupportedDepthOffset,
        UnsupportedBlendEquation,
        UnsupportedAlphaBlendEquation,
        UnsupportedForwardAddBlendEquation,
        ClipThresholdDiscardsOpaqueAlpha,

        // Transparent-only post-clip writers (design §9 gates 13-15).
        UnsupportedForwardAddAlphaBoost,
        UnsupportedDistanceFade,
        UnsupportedSubpassCutoff,
    }

    internal readonly struct LilToonOpaqueConversionEligibility
    {
        internal LilToonOpaqueConversionOutcome Outcome { get; }
        internal LilToonOpaqueConversionRefusal Refusal { get; }

        /// <summary>
        /// True only when the source depth test is Less and the policy
        /// admitted it. The conversion then normalizes the comparison to
        /// LEqual, so a caller that moves the material must report the
        /// change. False on every refusal: a refused material is not moved.
        /// </summary>
        internal bool DepthTestDivergence { get; }

        private LilToonOpaqueConversionEligibility(
            LilToonOpaqueConversionOutcome outcome,
            LilToonOpaqueConversionRefusal refusal,
            bool depthTestDivergence)
        {
            Outcome = outcome;
            Refusal = refusal;
            DepthTestDivergence = depthTestDivergence;
        }

        internal static LilToonOpaqueConversionEligibility Refused(
            LilToonOpaqueConversionRefusal refusal)
        {
            if (refusal == LilToonOpaqueConversionRefusal.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(refusal), "A refusal must name its cause.");
            }

            return new LilToonOpaqueConversionEligibility(
                LilToonOpaqueConversionOutcome.Refused, refusal, false);
        }

        internal static LilToonOpaqueConversionEligibility Convertible(
            bool depthTestDivergence = false)
        {
            return new LilToonOpaqueConversionEligibility(
                LilToonOpaqueConversionOutcome.Convertible,
                LilToonOpaqueConversionRefusal.None,
                depthTestDivergence);
        }
    }

    /// <summary>
    /// Exact render-state predicates and the Unity enum constants they read,
    /// shared by the two lilToon source-eligibility modules. This is a
    /// constants-and-predicates file and must stay one: a mode parameter, a
    /// gate table, a dispatch, or a shared Evaluate* body here is out of
    /// scope and returns to the controller (design §11).
    /// </summary>
    internal static class LilToonOpaqueConversionFactors
    {
        internal const float BlendOpAdd =
            (float)UnityEngine.Rendering.BlendOp.Add;
        internal const float BlendOpMax =
            (float)UnityEngine.Rendering.BlendOp.Max;
        internal const float BlendFactorZero =
            (float)UnityEngine.Rendering.BlendMode.Zero;
        internal const float BlendFactorOne =
            (float)UnityEngine.Rendering.BlendMode.One;
        internal const float BlendFactorSrcAlpha =
            (float)UnityEngine.Rendering.BlendMode.SrcAlpha;
        internal const float BlendFactorOneMinusSrcAlpha =
            (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;

        internal const float LEqualDepthComparison =
            (float)UnityEngine.Rendering.CompareFunction.LessEqual;

        internal const float LessDepthComparison =
            (float)UnityEngine.Rendering.CompareFunction.Less;

        internal const float ColorMaskAll =
            (float)UnityEngine.Rendering.ColorWriteMask.All;

        // The Unity 2022.3 profile lacks UnityEngine.Rendering.DepthWrite (checked 2026-09-29). Revisit this literal on a Unity upgrade.
        internal const float DepthWriteOn = 1f;

        /// <summary>One and SrcAlpha both evaluate to 1 at alpha 1.</summary>
        internal static bool IsUnitSourceFactorAtAlphaOne(float factor)
        {
            return factor == BlendFactorOne || factor == BlendFactorSrcAlpha;
        }

        /// <summary>Zero and OneMinusSrcAlpha both evaluate to 0 at alpha 1.</summary>
        internal static bool IsZeroDestinationFactorAtAlphaOne(float factor)
        {
            return factor == BlendFactorZero ||
                   factor == BlendFactorOneMinusSrcAlpha;
        }
    }
}
