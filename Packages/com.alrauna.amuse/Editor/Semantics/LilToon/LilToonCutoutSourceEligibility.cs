using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Cutout source facts: the admitted render queue and <c>RenderType</c>,
    /// the conversion-stage clip-threshold bound, the cutout source's own
    /// evidence request, and the gate set that authorizes the canonical
    /// clone.
    /// <para>
    /// The gate bodies, their order, and every refusal member are unchanged
    /// from the pre-split conversion module; only the ownership of the code
    /// moved. The cutout bound is deliberately not shared or parameterized
    /// with any other source family's bound (design §11).
    /// </para>
    /// </summary>
    internal static class LilToonCutoutSourceEligibility
    {
        internal const int SupportedCutoutRenderQueue = 2450;
        internal const string SupportedCutoutRenderType = "TransparentCutout";

        /// <summary>
        /// The conversion-stage clip-threshold bound (spec §9.3 gate 12; B2
        /// gap 4): alpha exactly 1 provably survives the cutout
        /// <c>clip(alpha - _Cutoff)</c> only while the threshold stays at or
        /// under this controller-fixed twice-margin constant. Poiyomi's
        /// <c>&lt;= 1</c> rule is deliberately not reused: the attested
        /// lilToon proof carries the tighter bound, and eligibility
        /// re-checks the same bound the classification layer refuses on.
        /// </summary>
        internal const float MaxProvableCutoff = 0.9999f;

        /// <summary>
        /// The one property outside the canonical recipe that conversion
        /// reads. The cutout shader clips with <c>clip(alpha - _Cutoff)</c>,
        /// so only a threshold that keeps alpha 1 alive authorizes a clone;
        /// the recipe never writes the property, so it is eligibility-read
        /// and never written.
        /// </summary>
        private const string CutoffProperty = "_Cutoff";

        private static readonly string[] SourceSchema = { CutoffProperty };

        /// <summary>
        /// The cutout source's own eligibility evidence: one property. The
        /// recipe never writes it, and it is not target evidence.
        /// </summary>
        internal static MaterialEvidenceRequest SourceEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: SourceSchema,
                scalarProperties: SourceSchema,
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
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
        /// The 19 property names this module reads off the SOURCE material,
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
        /// The load-bearing order is spec §9.3: the schema check, then
        /// finiteness over all 19 captured scalars (gate 2), then the
        /// mutation-authorizing render-state gates. Finiteness runs first
        /// because every later scalar gate compares captured values against
        /// pinned constants, and a NaN/±inf capture would make those
        /// comparisons meaningless: <c>&gt;</c>, <c>==</c> and set membership
        /// all silently answer "not equal" for NaN, which would dress a broken
        /// capture up as a plausible named refusal. There is deliberately no
        /// AlreadyOpaque classification - the outcome enum has no such member
        /// (see its doc) - so every gate here either authorizes the recipe's
        /// writes or refuses. The effective queue and RenderType are checked
        /// before the scalar render-state gates because canonicalization
        /// changes both facts and the alpha proof does not authorize erasing
        /// custom source overrides.
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
            //    does not assume its caller attested.
            var values = new float[EligibilitySchema.Length];
            for (var index = 0; index < EligibilitySchema.Length; index++)
            {
                if (!evidence.TryGetScalar(EligibilitySchema[index], out values[index]))
                {
                    return LilToonOpaqueConversionEligibility.Refused(
                        LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
                }
            }

            // 2. Finiteness. Every later gate compares captured values
            //    against pinned constants, and those comparisons are only
            //    meaningful for finite captures: a NaN fails every equality
            //    and set-membership test, which would misreport a broken
            //    capture as a plausible named refusal. Refusing here keeps
            //    the render-state gates total.
            foreach (var value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return LilToonOpaqueConversionEligibility.Refused(
                        LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
                }
            }

            // --- Mutation-authorizing gates. The outcome enum has no
            // AlreadyOpaque member, so each gate below either authorizes the
            // recipe's writes or refuses.

            // 3-4. The canonical cutout defaults are the only admitted source
            // queue and RenderType. The intended 2450 -> 2000 normalization is
            // part of the conversion; custom overrides express ordering or
            // classification intent that the alpha proof does not preserve.
            if (effectiveRenderQueue != SupportedCutoutRenderQueue)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedRenderQueue);
            }

            if (!string.Equals(
                    effectiveRenderType,
                    SupportedCutoutRenderType,
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

            // 6. Depth write. The canonical target writes depth; a cutout
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

            // 12. Clip threshold. The cutout shader clips with
            //     `clip(alpha - _Cutoff)`, which discards when the
            //     difference is negative, so alpha exactly 1 provably
            //     survives only while _Cutoff stays at or under the
            //     controller-fixed twice-margin bound. A NaN cutoff never
            //     reaches this comparison: gate 2 already refused it.
            if (Read(values, CutoffProperty) > MaxProvableCutoff)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .ClipThresholdDiscardsOpaqueAlpha);
            }

            // Deliberately ungated: _AlphaToMask, _SrcBlendAlphaFA and
            // _DstBlendAlphaFA. The recipe writes all three, but none is
            // load-bearing on the attested source: the FORWARD_ADD pass
            // declares its alpha pair as literal Zero One regardless of the
            // stored _SrcBlendAlphaFA/_DstBlendAlphaFA (B2 §3.1), and full
            // alpha coverage is proven at a≡1 under any _AlphaToMask
            // setting (B2 §3.4) — the property modulates the mask
            // derivative, not the a=1 sample itself.

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
