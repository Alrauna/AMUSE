using System;
using System.Collections.Generic;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// The eligibility machinery the cutout and transparent source-family
    /// evaluators share: the general schema-indexed property read and the
    /// render-state gates 5-12 of spec §9.3, which the two families run in
    /// one identical order with one refusal vocabulary. Each caller passes
    /// its own schema, its depth-test policy, and its own clip bound: the
    /// bounds stay family-owned constants (design §11; T1 §9.2), and only
    /// the gate bodies are one definition, so the families cannot drift.
    /// The per-family tails stay per family: the cutout distance-fade arm
    /// and the transparent alpha-boost and subpass-cutoff arms remain in
    /// their own evaluators, in their own order.
    /// </summary>
    internal static class LilToonSharedEligibility
    {
        /// <summary>
        /// The mutation-authorizing render-state gates 5-12: depth
        /// comparison, depth write, color mask, depth offset, base RGB and
        /// alpha blends, ForwardAdd blend, and clip threshold. Returns the
        /// refusal naming the failed gate, or null when every gate passed;
        /// the out parameter then carries the depth-test divergence answer
        /// the caller reports through
        /// <see cref="LilToonOpaqueConversionEligibility.DepthTestDivergence"/>
        /// (false on every refusal: a refused material is not moved). Gate
        /// order, per-gate detail, and every refusal value are unchanged
        /// from the pre-extraction cutout block.
        /// </summary>
        internal static LilToonOpaqueConversionEligibility?
            EvaluateRenderStateGates(
                IReadOnlyList<float> values,
                string[] schema,
                bool allowDepthTestChange,
                float maxProvableCutoff,
                out bool depthTestDivergence)
        {
            depthTestDivergence = false;

            // 5. Depth comparison. Required to be LEqual already rather than
            //    normalized to it: a different comparison changes visibility
            //    independently of alpha, so a material authored to draw with
            //    Always, Greater or Disabled expresses a visibility intent
            //    the alpha proof knows nothing about. The opt-in policy
            //    admits Less beside LEqual (design §D2). Less and LEqual
            //    differ only at exact depth equality. That class depends on
            //    depth-buffer population. No per-pixel proof reaches it.
            //    The admission is a stated, consented divergence. The
            //    caller learns it through DepthTestDivergence. The recipe
            //    still writes 4. Without the policy that write is a no-op.
            //    With the policy it is the reported normalization.
            var depthComparison = Read(schema, values, "_ZTest");
            if (depthComparison !=
                    OpaqueConversionFactors.LEqualDepthComparison &&
                !(allowDepthTestChange &&
                  depthComparison ==
                  OpaqueConversionFactors.LessDepthComparison))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDepthComparison);
            }

            // 6. Depth write. The canonical target writes depth; a source
            //    authored with ZWrite off expresses different visibility
            //    intent than the recipe preserves.
            if (Read(schema, values, "_ZWrite") !=
                OpaqueConversionFactors.DepthWriteOn)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDepthWrite);
            }

            // 7. Color mask. The canonical target writes all channels; a
            //    masked material deliberately suppresses channel output the
            //    recipe would restore.
            if (Read(schema, values, "_ColorMask") !=
                OpaqueConversionFactors.ColorMaskAll)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedColorMask);
            }

            // 8. Depth offset. The canonical target applies none; a material
            //    authored with an offset expresses polygon-offset intent the
            //    recipe would erase.
            if (Read(schema, values, "_OffsetFactor") != 0f ||
                Read(schema, values, "_OffsetUnits") != 0f)
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
            if (Read(schema, values, "_BlendOp") !=
                    OpaqueConversionFactors.BlendOpAdd ||
                !OpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
                    Read(schema, values, "_SrcBlend")) ||
                !OpaqueConversionFactors.IsZeroDestinationFactorAtAlphaOne(
                    Read(schema, values, "_DstBlend")))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedBlendEquation);
            }

            // 10. Base alpha blend, same degeneracy argument as gate 9.
            if (Read(schema, values, "_BlendOpAlpha") !=
                    OpaqueConversionFactors.BlendOpAdd ||
                !OpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
                    Read(schema, values, "_SrcBlendAlpha")) ||
                !OpaqueConversionFactors.IsZeroDestinationFactorAtAlphaOne(
                    Read(schema, values, "_DstBlendAlpha")))
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
            if (!OpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
                    Read(schema, values, "_SrcBlendFA")) ||
                Read(schema, values, "_DstBlendFA") !=
                OpaqueConversionFactors.BlendFactorOne ||
                Read(schema, values, "_BlendOpFA") !=
                OpaqueConversionFactors.BlendOpMax ||
                Read(schema, values, "_BlendOpAlphaFA") !=
                OpaqueConversionFactors.BlendOpMax)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .UnsupportedForwardAddBlendEquation);
            }

            // 12. Clip threshold. Both admitted shader sites clip against
            //     _Cutoff and discard when alpha - _Cutoff is negative, so
            //     alpha exactly 1 provably survives only while _Cutoff
            //     stays at or under the caller family's own pinned bound.
            //     The bounds are family-owned constants, deliberately not
            //     shared or widened across families (design §11; T1 §9.2).
            //     A NaN cutoff never reaches this comparison: gate 2
            //     already refused it.
            if (Read(schema, values, "_Cutoff") > maxProvableCutoff)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .ClipThresholdDiscardsOpaqueAlpha);
            }

            depthTestDivergence =
                depthComparison ==
                OpaqueConversionFactors.LessDepthComparison;
            return null;
        }

        /// <summary>
        /// Reads one conversion-read property from the captured values,
        /// which are indexed by the passed schema order.
        /// </summary>
        internal static float Read(
            string[] schema,
            IReadOnlyList<float> values,
            string property)
        {
            for (var index = 0; index < schema.Length; index++)
            {
                if (string.Equals(
                        schema[index],
                        property,
                        StringComparison.Ordinal))
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
