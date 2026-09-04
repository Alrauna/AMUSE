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
    /// the transparent-only post-clip writers. No bound here is shared,
    /// parameterized, or widened with any other source family's (design
    /// §11).
    /// </para>
    /// </summary>
    internal static class LilToonTransparentSourceEligibility
    {
        internal const int SupportedTransparentRenderQueue = 2460;
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

        /// <summary>
        /// The three properties outside the canonical recipe that conversion
        /// reads, plus the one vector. The transparent shader clips the
        /// forward pass with <c>clip(alpha - _Cutoff)</c>, scales additive
        /// light by <c>saturate(a * _AlphaBoostFA)</c>, and clips the
        /// shadow-caster subpass against <c>_SubpassCutoff</c>, so only
        /// thresholds and a boost that keep alpha 1 alive authorize a clone.
        /// The distance-fade vector drives the one post-clip alpha writer,
        /// and only an all-zero strength component leaves it an identity.
        /// The recipe never writes any of them, so they are
        /// eligibility-read and never written.
        /// </summary>
        private const string CutoffProperty = "_Cutoff";
        private const string AlphaBoostFaProperty = "_AlphaBoostFA";
        private const string SubpassCutoffProperty = "_SubpassCutoff";
        private const string DistanceFadeProperty = "_DistanceFade";

        private static readonly string[] SourceSchema =
        {
            CutoffProperty, AlphaBoostFaProperty, SubpassCutoffProperty,
        };

        private static readonly string[] SourceVectorSchema =
        {
            DistanceFadeProperty,
        };

        /// <summary>
        /// The transparent source's own eligibility evidence: three scalars
        /// and one vector. The recipe never writes them, and they are not
        /// target evidence.
        /// </summary>
        internal static MaterialEvidenceRequest SourceEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: SourceSchema,
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
        /// The 21 property names this module reads off the SOURCE material,
        /// in a fixed order the finiteness sweep and <see cref="Read"/> index
        /// by. Sharing a name with the recipe does not make a source
        /// render-state fact target evidence.
        /// </summary>
        private static readonly string[] EligibilitySchema =
            BuildEligibilitySchema();

        internal static IReadOnlyCollection<string>
            EligibilitySchemaProperties { get; } =
                new ReadOnlyCollection<string>(EligibilitySchema);

        private static string[] BuildEligibilitySchema()
        {
            var recipe = LilToonOpaqueTarget.RecipeSchemaProperties;
            var schema = new string[recipe.Count + SourceSchema.Length];
            var index = 0;
            foreach (var property in recipe)
            {
                schema[index++] = property;
            }

            foreach (var property in SourceSchema)
            {
                schema[index++] = property;
            }

            return schema;
        }

        // --- Eligibility -----------------------------------------------------

        /// <summary>
        /// Pure evaluation over already-captured, already-admitted conversion
        /// evidence plus the two effective non-property facts. It performs no
        /// capture and touches no live material, so it cannot read mutable
        /// state after the evidence a decision depends on.
        /// <para>
        /// The load-bearing order is design §9: the schema check (including
        /// the presence of the _DistanceFade vector), then finiteness over
        /// all 21 captured scalars and all four vector components (gate 2),
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
            string effectiveRenderType)
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

            if (!evidence.TryGetVector(DistanceFadeProperty, out var distanceFade))
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

            if (float.IsNaN(distanceFade.x) || float.IsInfinity(distanceFade.x) ||
                float.IsNaN(distanceFade.y) || float.IsInfinity(distanceFade.y) ||
                float.IsNaN(distanceFade.z) || float.IsInfinity(distanceFade.z) ||
                float.IsNaN(distanceFade.w) || float.IsInfinity(distanceFade.w))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
            }

            // --- Mutation-authorizing gates. The outcome enum has no
            // AlreadyOpaque member, so each gate below either authorizes the
            // recipe's writes or refuses.

            // 3-4. The canonical transparent defaults are the only admitted
            //    source queue and RenderType. The intended 2460 -> 2000
            //    normalization is part of the conversion; custom overrides
            //    express ordering or classification intent that the alpha
            //    proof does not preserve.
            if (effectiveRenderQueue != SupportedTransparentRenderQueue)
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

            // 5. Depth comparison. Required to be LEqual already rather than
            //    normalized to it: a different comparison changes visibility
            //    independently of alpha, so a material authored to draw with
            //    Always, Greater or Disabled expresses a visibility intent
            //    the alpha proof knows nothing about. The recipe still
            //    writes 4; on an eligible material that write is a no-op.
            if (Read(values, "_ZTest") !=
                LilToonOpaqueConversionFactors.LEqualDepthComparison)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDepthComparison);
            }

            // 6. Depth write. The canonical target writes depth; a source
            //    authored with ZWrite off expresses different visibility
            //    intent than the recipe preserves.
            if (Read(values, "_ZWrite") != LilToonOpaqueConversionFactors.DepthWriteOn)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDepthWrite);
            }

            // 7. Color mask. The canonical target writes all channels; a
            //    masked material deliberately suppresses channel output the
            //    recipe would restore.
            if (Read(values, "_ColorMask") != LilToonOpaqueConversionFactors.ColorMaskAll)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedColorMask);
            }

            // 8. Depth offset. The canonical target applies none; a material
            //    authored with an offset expresses polygon-offset intent the
            //    recipe would erase.
            if (Read(values, "_OffsetFactor") != 0f ||
                Read(values, "_OffsetUnits") != 0f)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDepthOffset);
            }

            // 9. Base RGB blend. At alpha 1 both accepted source factors
            //    evaluate to 1 and both accepted destination factors to 0,
            //    so the blend degenerates to `dst := src` and normalizing
            //    to One/Zero is an identity there. The operation must be
            //    Add: the accepted Max operation in ForwardAdd does not
            //    carry over here, and any other operation changes how RGB
            //    is combined, not just how factors weigh it.
            if (Read(values, "_BlendOp") != LilToonOpaqueConversionFactors.BlendOpAdd ||
                !LilToonOpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
                    Read(values, "_SrcBlend")) ||
                !LilToonOpaqueConversionFactors.IsZeroDestinationFactorAtAlphaOne(
                    Read(values, "_DstBlend")))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedBlendEquation);
            }

            // 10. Base alpha blend, same degeneracy argument as gate 9.
            if (Read(values, "_BlendOpAlpha") !=
                    LilToonOpaqueConversionFactors.BlendOpAdd ||
                !LilToonOpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
                    Read(values, "_SrcBlendAlpha")) ||
                !LilToonOpaqueConversionFactors.IsZeroDestinationFactorAtAlphaOne(
                    Read(values, "_DstBlendAlpha")))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .UnsupportedAlphaBlendEquation);
            }

            // 11. ForwardAdd blend. The recipe rewrites this pass to
            //    Max(Max) with One/One factors (B2 §3.1's FORWARD_ADD
            //    declares the literal Zero One alpha pair, which the alpha
            //    operation overwrites). At alpha 1 the accepted source
            //    factors evaluate to 1 and the accepted destination factor
            //    to 1, so the accepted states are equivalent to the
            //    canonical tuple. Unlike the Poiyomi path, the blend
            //    OPERATIONS are constrained too: lilToon's recipe writes
            //    Max into both, and an Add-kept ForwardAdd pass would
            //    double-composite against the base pass.
            if (!LilToonOpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
                    Read(values, "_SrcBlendFA")) ||
                Read(values, "_DstBlendFA") !=
                    LilToonOpaqueConversionFactors.BlendFactorOne ||
                Read(values, "_BlendOpFA") !=
                    LilToonOpaqueConversionFactors.BlendOpMax ||
                Read(values, "_BlendOpAlphaFA") !=
                    LilToonOpaqueConversionFactors.BlendOpMax)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .UnsupportedForwardAddBlendEquation);
            }

            // 12. Clip threshold. The transparent forward pass clips with
            //     `clip(alpha - _Cutoff)` — the plain clip, not the cutout
            //     coverage transform — so alpha exactly 1 provably survives
            //     while _Cutoff stays at or under this family's bound; the
            //     cutout twice-margin constant is deliberately not shared
            //     (T1 §9.2). A NaN cutoff never reaches this comparison:
            //     gate 2 already refused it.
            if (Read(values, CutoffProperty) > MaxProvableCutoff)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .ClipThresholdDiscardsOpaqueAlpha);
            }

            // 13. ForwardAdd premultiply. The FORWARD_ADD pass applies
            //     saturate(fd.col.a * _AlphaBoostFA) rather than the base
            //     pass's fd.col.rgb *= fd.col.a. At a = 1 the base site is
            //     an identity and the ForwardAdd site is one only when the
            //     boost saturates to at least 1; below that the additive
            //     pass composites a darker colour than the opaque target.
            if (Read(values, AlphaBoostFaProperty) < MinProvableAlphaBoostFa)
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
            if (Read(values, SubpassCutoffProperty) > MaxProvableSubpassCutoff)
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

            return LilToonOpaqueConversionEligibility.Convertible();
        }

        /// <summary>
        /// Reads one conversion-read property from the captured values, which
        /// are indexed by <see cref="EligibilitySchema"/> order.
        /// </summary>
        private static float Read(IReadOnlyList<float> values, string property)
        {
            for (var index = 0; index < EligibilitySchema.Length; index++)
            {
                if (string.Equals(
                        EligibilitySchema[index], property, StringComparison.Ordinal))
                {
                    return values[index];
                }
            }

            throw new ArgumentException(
                "Property '" + property + "' is not conversion-read.",
                nameof(property));
        }
    }
}
