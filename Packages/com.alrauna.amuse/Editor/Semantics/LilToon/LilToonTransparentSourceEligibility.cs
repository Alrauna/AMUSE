using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Transparent source facts: the admitted render queue and
    /// <c>RenderType</c>, the conversion-stage clip bound and the two
    /// transparent-only bounds, the transparent source's own evidence
    /// request, and the gate set that authorizes the canonical clone.
    /// <para>
    /// Gates 1-11 are the merged cutout rules unchanged — gate 9 already
    /// admits the transparent canonical <c>_DstBlend</c> OneMinusSrcAlpha
    /// (T1 §7) — gate 12 carries this family's bound, and gates 13-15 refuse
    /// the transparent-only post-clip writers. Gates 5-12 run through
    /// <see cref="LilToonSharedEligibility.EvaluateRenderStateGates"/> with
    /// this family's own cutoff bound as its input. No bound here is shared
    /// with or widened against any other source family's (design §11).
    /// </para>
    /// </summary>
    internal static class LilToonTransparentSourceEligibility
    {
        internal const int SupportedTransparentRenderQueue = 2460;

        /// <summary>
        /// Unity's Transparent queue bucket plus a two-digit offset is the
        /// other admitted source queue range. Authored offsets inside the
        /// bucket (for example 3005) order the source material among other
        /// transparents; the conversion already accepts that class of
        /// ordering change for the pinned default 2460, whose proven-opaque
        /// triangles also leave the transparent ordering for the canonical
        /// opaque queue 2000. Because a proven-opaque fragment has alpha
        /// exactly one, its blending is the identity, so its own color
        /// cannot depend on within-bucket order. The triangles that stay
        /// transparent keep the authored queue, so their order among each
        /// other is untouched. Values outside the pinned default and this
        /// bucket still refuse: cross-bucket values express ordering
        /// against opaque or test geometry that the alpha proof does not
        /// preserve.
        /// </summary>
        internal const int TransparentBucketRenderQueueFloor = 3000;
        internal const int TransparentBucketRenderQueueCeiling = 3099;
        internal const string SupportedTransparentRenderType =
            "TransparentCutout";

        /// <summary>
        /// The transparent clip bound (design §9 gate 12; T1 §9.2). The
        /// transparent forward site is a plain clip(fd.col.a - _Cutoff), not
        /// the cutout coverage transform, so the cutout twice-margin bound
        /// 0.9999 is deliberately NOT reused: for any finite c &lt;= 1 the
        /// exact difference 1 - c is nonnegative, round-to-nearest preserves
        /// that sign, and clip keeps the fragment. At c == 1 the difference
        /// is exactly zero, which clip keeps. Above 1 the difference is a
        /// nonzero negative well above the underflow threshold, and clip
        /// discards.
        /// </summary>
        internal const float MaxProvableCutoff = 1f;

        /// <summary>
        /// The ForwardAdd premultiply lower bound (T1 §5.3). The base pass
        /// premultiply is fd.col.rgb *= fd.col.a, the identity at a = 1; the
        /// ForwardAdd pass instead applies saturate(fd.col.a *
        /// _AlphaBoostFA), which is the identity at a = 1 only when the
        /// boost saturates to at least one.
        /// </summary>
        internal const float MinProvableAlphaBoostFa = 1f;

        /// <summary>
        /// The subpass shadow clip bound (T1 §9.4, measured). At a = 1 the
        /// dither sample returns 1 at all sixteen positions of the
        /// _DitherMaskLOD slice the alpha selects, so the shadow clip
        /// reduces to clip(1 - _SubpassCutoff) and keeps by the same
        /// sign-preservation argument as the forward cutoff.
        /// </summary>
        internal const float MaxProvableSubpassCutoff = 1f;

        private static readonly string[] SourcePresenceSchema =
        {
            LilToonProperties.Cutoff, LilToonProperties.AlphaBoostFA, LilToonProperties.SubpassCutoff,
        };

        private static readonly string[] SourceSchema =
        {
            LilToonSourceAttestation.ShaderFormatVersionProperty,
            LilToonProperties.Cutoff, LilToonProperties.AlphaBoostFA, LilToonProperties.SubpassCutoff,
        };

        private static readonly string[] SourceVectorSchema =
        {
            LilToonProperties.DistanceFade,
        };

        /// <summary>
        /// The transparent source's own eligibility evidence: four scalars
        /// and one vector. The recipe never writes them, and they are not
        /// target evidence.
        /// </summary>
        internal static MaterialEvidenceRequest SourceEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: SourcePresenceSchema,
                scalarProperties: SourceSchema,
                colorProperties: Array.Empty<string>(),
                vectorProperties: SourceVectorSchema,
                textureProperties: Array.Empty<TexturePropertyEvidenceRequest>());

        /// <summary>
        /// The single object the capture schema and the conversion boundary
        /// both read, built once: the target's recipe request plus this
        /// family's source evidence. One canonical definition, so the
        /// properties captured for this family and the properties its
        /// conversion decision reads cannot drift apart, and no second
        /// Combine call site can disagree with this one.
        /// </summary>
        internal static MaterialEvidenceRequest
            ConversionEvidenceRequest { get; } =
                MaterialEvidenceRequest.Combine(
                    LilToonOpaqueTarget.RecipeEvidenceRequest,
                    SourceEvidenceRequest);

        /// <summary>
        /// The 22 property names this module reads off the SOURCE material,
        /// in a fixed order the finiteness sweep and <see cref="LilToonSharedEligibility.Read"/> index
        /// by. Sharing a name with the recipe does not make a source
        /// render-state fact target evidence.
        /// </summary>
        private static readonly string[] EligibilitySchema =
            EvidenceGates.BuildEligibilitySchema(
                LilToonOpaqueTarget.RecipeSchemaProperties,
                SourceSchema);

        internal static IReadOnlyCollection<string>
            EligibilitySchemaProperties { get; } =
                new ReadOnlyCollection<string>(EligibilitySchema);

        // --- Eligibility -----------------------------------------------------

        /// <summary>
        /// Pure evaluation over already-captured, already-admitted conversion
        /// evidence plus the two effective non-property facts. It performs no
        /// capture and touches no live material, so it cannot read mutable
        /// state after the evidence a decision depends on.
        /// <para>
        /// The load-bearing order is design §9: the schema check (including
        /// the presence of the _DistanceFade vector), then finiteness over
        /// all 22 captured scalars and all four vector components (gate 2),
        /// then the mutation-authorizing render-state gates. Finiteness runs
        /// first because every later scalar gate compares captured values
        /// against pinned constants, and a NaN/±inf capture would make those
        /// comparisons meaningless: <c>&gt;</c>, <c>==</c> and set membership
        /// all silently answer "not equal" for NaN, which would dress a
        /// broken capture up as a plausible named refusal. There is
        /// deliberately no AlreadyOpaque classification - the outcome enum
        /// has no such member (see its doc) - so every gate here either
        /// authorizes the recipe's writes or refuses. The effective queue
        /// and RenderType are checked before the scalar render-state gates
        /// because canonicalization changes both facts and the alpha proof
        /// does not authorize erasing custom source overrides.
        /// </para>
        /// </summary>
        internal static LilToonOpaqueConversionEligibility EvaluateVerifiedEligibility(
            CapturedMaterialEvidence evidence,
            int effectiveRenderQueue,
            string effectiveRenderType,
            bool allowDepthTestChange = false)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));

            // 1. Schema. Attestation already establishes this in the production
            //    sequence; repeated here because this is a pure function that
            //    does not assume its caller attested. _DistanceFade is the one
            //    conversion-read property that is not a scalar, so its
            //    presence is checked here in the schema gate: a missing
            //    vector is ConversionPropertyAbsent, never a named
            //    render-state refusal.
            var values = new float[EligibilitySchema.Length];
            for (var index = 0; index < EligibilitySchema.Length; index++)
            {
                if (!evidence.TryGetScalar(EligibilitySchema[index], out values[index]))
                {
                    return LilToonOpaqueConversionEligibility.Refused(
                        LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
                }
            }

            if (!evidence.TryGetVector(LilToonProperties.DistanceFade, out var distanceFade))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
            }

            // 2. Finiteness. Every later gate compares captured values
            //    against pinned constants, and those comparisons are only
            //    meaningful for finite captures: a NaN fails every equality
            //    and set-membership test, which would misreport a broken
            //    capture as a plausible named refusal. Refusing here keeps
            //    the render-state gates total. The sweep covers the four
            //    _DistanceFade components too: they feed gate 14's
            //    comparison, and a non-finite component must be refused as
            //    ConversionPropertyNotFinite, not as UnsupportedDistanceFade.
            foreach (var value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return LilToonOpaqueConversionEligibility.Refused(
                        LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
                }
            }

            if (!EvidenceGates.IsFinite(distanceFade))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
            }

            // --- Mutation-authorizing gates. The outcome enum has no
            // AlreadyOpaque member, so each gate below either authorizes the
            // recipe's writes or refuses.

            // 3-4. The admitted source queues are the canonical transparent
            //    default and Unity's Transparent bucket with a two-digit
            //    offset. The 2460 -> 2000 normalization is part of the
            //    conversion, and a bucket offset is the same class of
            //    within-transparent ordering intent; see the bucket
            //    constants for why that class is admitted. Anything else
            //    expresses ordering or classification intent that the alpha
            //    proof does not preserve.
            if (effectiveRenderQueue != SupportedTransparentRenderQueue &&
                (effectiveRenderQueue < TransparentBucketRenderQueueFloor ||
                 effectiveRenderQueue > TransparentBucketRenderQueueCeiling))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedRenderQueue);
            }

            if (!string.Equals(
                    effectiveRenderType,
                    SupportedTransparentRenderType,
                    StringComparison.Ordinal))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedRenderType);
            }

            // 5-12. The shared render-state gates, in the pinned order,
            //     with this family's own cutoff bound. On a pass the out
            //     parameter carries this family's DepthTestDivergence
            //     answer; on a refusal it is false.
            var renderStateRefusal = LilToonSharedEligibility
                .EvaluateRenderStateGates(
                    values,
                    EligibilitySchema,
                    allowDepthTestChange,
                    MaxProvableCutoff,
                    out var depthTestDivergence);
            if (renderStateRefusal.HasValue)
            {
                return renderStateRefusal.Value;
            }

            // 13. ForwardAdd premultiply. The FORWARD_ADD pass applies
            //     saturate(fd.col.a * _AlphaBoostFA) rather than the base
            //     pass's fd.col.rgb *= fd.col.a. At a = 1 the base site is
            //     an identity and the ForwardAdd site is one only when the
            //     boost saturates to at least 1; below that the additive
            //     pass composites a darker colour than the opaque target.
            if (LilToonSharedEligibility.Read(
                    EligibilitySchema, values, LilToonProperties.AlphaBoostFA) <
                MinProvableAlphaBoostFa)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .UnsupportedForwardAddAlphaBoost);
            }

            // 14. Distance fade. At LIL_RENDER 2 the block writes fd.col.a
            //     after the clip, so it is the one post-clip alpha writer.
            //     The gate is the .z strength component; _DistanceFadeColor.a
            //     drives the RGB arm and does not disable the alpha write.
            //     Non-finite components already refused at gate 2.
            if (distanceFade.z != 0f)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDistanceFade);
            }

            // 15. Subpass shadow clip. The SHADOW_CASTER pass clips against
            //     _SubpassCutoff after a dither sample measured uniformly 1
            //     at a = 1 (T1 §9.4), so the clip reduces to
            //     clip(1 - _SubpassCutoff) and keeps iff the bound holds.
            //     The target casts shadows unconditionally, so a source that
            //     clips its shadow here is not convertible.
            if (LilToonSharedEligibility.Read(
                    EligibilitySchema, values, LilToonProperties.SubpassCutoff) >
                MaxProvableSubpassCutoff)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedSubpassCutoff);
            }

            // Deliberately ungated, with reasons (design §9; T1 §4.4, §5.6,
            // §6): _AlphaToMask (full coverage at a ≡ 1 under any value),
            // _SrcBlendAlphaFA and _DstBlendAlphaFA (the FORWARD_ADD pass
            // declares its alpha pair as literal Zero One regardless of the
            // stored values), and _UseDither (compiled out: the dither block
            // exists only under LIL_RENDER == 1, and this family is
            // LIL_RENDER 2). A gate on any of them would be a free false
            // negative.

            return LilToonOpaqueConversionEligibility.Convertible(
                depthTestDivergence);
        }
    }
}
