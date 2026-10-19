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
    /// moved. Gates 5-12 run through
    /// <see cref="LilToonSharedEligibility.EvaluateRenderStateGates"/> with
    /// this family's own cutoff bound as its input. The cutout bound is
    /// deliberately not shared or parameterized with any other source
    /// family's bound (design §11).
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

        private static readonly string[] SourcePresenceSchema =
        {
            LilToonProperties.Cutoff,
            LilToonProperties.DistanceFade,
        };

        private static readonly string[] SourceSchema =
        {
            LilToonSourceAttestation.ShaderFormatVersionProperty,
            LilToonProperties.Cutoff,
        };

        /// <summary>
        /// The cutout source's own eligibility evidence: the version and
        /// cutoff scalars plus the distance-fade presence fact. The recipe
        /// never writes them, and they are not target evidence. The
        /// distance-fade vector itself rides the alpha capture request,
        /// which declares it.
        /// </summary>
        internal static MaterialEvidenceRequest SourceEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: SourcePresenceSchema,
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
        /// The 20 property names this module reads off the SOURCE material,
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
        /// The load-bearing order is spec §9.3: the schema check, then
        /// finiteness over all 20 captured scalars (gate 2), then the
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
            string effectiveRenderType,
            bool allowDepthTestChange = false)
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

            // 13. Distance fade. The vendor forward pass lerps the fragment
            //     color by the fade weight at LIL_RENDER 1, and the dither path
            //     lerps alpha before the coverage transform. A moved triangle
            //     would lose both, so a finite nonzero strength refuses. The
            //     vector rides the alpha capture request, so its absence refuses
            //     as ConversionPropertyAbsent, exactly like the transparent
            //     family.
            if (!evidence.TryGetVector(
                    LilToonProperties.DistanceFade,
                    out var distanceFade))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
            }

            if (!EvidenceGates.IsFinite(distanceFade))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
            }

            if (distanceFade.z != 0f)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDistanceFade);
            }

            // Deliberately ungated: _AlphaToMask, _SrcBlendAlphaFA and
            // _DstBlendAlphaFA. The recipe writes all three, but none is
            // load-bearing on the attested source: the FORWARD_ADD pass
            // declares its alpha pair as literal Zero One regardless of the
            // stored _SrcBlendAlphaFA/_DstBlendAlphaFA (B2 §3.1), and full
            // alpha coverage is proven at a≡1 under any _AlphaToMask
            // setting (B2 §3.4) — the property modulates the mask
            // derivative, not the a=1 sample itself.

            return LilToonOpaqueConversionEligibility.Convertible(
                depthTestDivergence);
        }
    }
}
