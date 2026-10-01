using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Multi source eligibility for mode 1 (cutout), on either supported
    /// container. Parity rule, pinned 2026-10-01: this evaluator mirrors the
    /// plain cutout rows of <see cref="LilToonCutoutSourceEligibility"/>,
    /// because that is what the shipped regular outline twin evaluates
    /// through. The S8 outline support is attestation, classification, and
    /// target resolution only; a <c>Hidden/lilToonCutoutOutline</c> source
    /// classifies to the cutout family and converts through the plain cutout
    /// evaluator, whose schema names no <c>_Outline</c> property. This
    /// evaluator therefore carries no outline-specific eligibility row on
    /// either container, and parity against the regular twin is outcome
    /// equality on identical plain-cutout state. The outline-alpha
    /// protection question (F0 §7.3) is a pre-existing regular-family scope
    /// fact that this evaluator neither widens nor narrows.
    /// <para>
    /// The rows beyond the mirror are the Multi-only facts: the Task 5
    /// mode-consistency gate, and the mode-1 keyword feature facts as
    /// runtime scalar gates on the captured feature scalars. A keyword is
    /// never proof of a feature and a feature never hides behind its
    /// keyword. The outline property scalars ride the conversion request and
    /// gate nothing here, so a Multi material evaluates exactly as its
    /// regular twin does on the shared scalar surface.
    /// </para>
    /// </summary>
    internal static class LilToonMultiSourceEligibility
    {
        private const string CutoffProperty = "_Cutoff";
        private const string AlphaMaskModeProperty = "_AlphaMaskMode";
        private const string DitherProperty = "_UseDither";
        private const string DissolveParamsProperty = "_DissolveParams";
        private const string ClippingCancellerProperty =
            "_UseClippingCanceller";
        private const string OverlayProperty = "_AsOverlay";
        private const string DistanceFadeProperty = "_DistanceFade";
        private const string OutlineToneProperty = "_OutlineTexHSVG";

        /// <summary>
        /// The outline property scalars both containers declare. They ride
        /// the conversion request and gate nothing: the 2026-10-01 ruling
        /// pins that no outline-specific eligibility row exists, so a
        /// captured outline value never refuses and never admits anything
        /// the plain rows would not.
        /// </summary>
        private static readonly string[] OutlineScalarSchema =
        {
            "_OutlineWidth",
            "_OutlineDeleteMesh",
            "_OutlineDisableInVR",
            "_OutlineZTest",
            "_OutlineZWrite",
            "_OutlineCull",
            "_OutlineColorMask",
        };

        private static readonly string[] SourcePresenceSchema =
        {
            CutoffProperty,
        };

        private static readonly string[] SourceSchema =
        {
            LilToonSourceAttestation.ShaderFormatVersionProperty,
            CutoffProperty,
            AlphaMaskModeProperty,
            DitherProperty,
        };

        private static readonly string[] SourceVectorSchema =
        {
            DissolveParamsProperty,
        };

        /// <summary>
        /// The mode-1 source's own eligibility evidence: the mirrored cutout
        /// source facts plus the two feature scalars the runtime gates read.
        /// The dissolve vector sits beside them. The recipe never writes any
        /// of them, and they are not target evidence.
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
        /// The Multi state facts the Task 5 mode-consistency gate reads at
        /// this seam: the two gate scalars, the two keyword-feature vectors
        /// the derivation consults, the outline tone vector its outline
        /// shader row needs, and the keyword set itself. The outline scalars
        /// ride along and are read by no gate. The shader name is already
        /// requested by the recipe request this combines with.
        /// </summary>
        private static MaterialEvidenceRequest MultiStateEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: BuildMultiStateScalarSchema(),
                colorProperties: Array.Empty<string>(),
                vectorProperties: new[]
                {
                    DissolveParamsProperty,
                    DistanceFadeProperty,
                    OutlineToneProperty,
                },
                textureProperties: Array.Empty<TexturePropertyEvidenceRequest>(),
                captureKeywords: true);

        /// <summary>
        /// The single object the capture schema and the conversion boundary
        /// both read, built once: the target's recipe request, this family's
        /// source evidence, and the Multi state facts. One canonical
        /// definition, so the properties captured for this family and the
        /// properties its conversion decision reads cannot drift apart.
        /// </summary>
        internal static MaterialEvidenceRequest
            ConversionEvidenceRequest { get; } =
                MaterialEvidenceRequest.Combine(
                    LilToonOpaqueTarget.RecipeEvidenceRequest,
                    SourceEvidenceRequest,
                    MultiStateEvidenceRequest);

        /// <summary>
        /// The Multi state scalars in one array: the outline scalars first,
        /// then the two gate scalars.
        /// </summary>
        private static string[] BuildMultiStateScalarSchema()
        {
            var scalars = new string[OutlineScalarSchema.Length + 2];
            var index = 0;
            foreach (var property in OutlineScalarSchema)
            {
                scalars[index++] = property;
            }

            scalars[index++] = ClippingCancellerProperty;
            scalars[index] = OverlayProperty;
            return scalars;
        }

        /// <summary>
        /// The property names this module reads off the SOURCE material and
        /// holds to the family's strict schema discipline: absent refuses as
        /// <c>ConversionPropertyAbsent</c> and a non-finite value refuses as
        /// <c>ConversionPropertyNotFinite</c>, in a fixed order the
        /// finiteness sweep and <see cref="Read"/> index by. The
        /// gate-owned facts the mode-consistency gate reads keep that
        /// gate's own missing-fact policy instead, and the outline scalars
        /// are captured but swept by neither.
        /// </summary>
        private static readonly string[] EligibilitySchema =
            BuildEligibilitySchema();

        private static string[] BuildEligibilitySchema()
        {
            var recipe = LilToonOpaqueTarget.RecipeSchemaProperties;
            var schema = new string[
                recipe.Count + SourceSchema.Length];
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
        /// evidence plus the effective non-property facts and the Multi mode.
        /// It performs no capture and touches no live material, so it cannot
        /// read mutable state after the evidence a decision depends on.
        /// <para>
        /// The load-bearing order: the strict schema check, then finiteness
        /// over the schema scalars and the dissolve vector, then the Task 5
        /// mode-consistency gate, then the runtime scalar feature gates,
        /// then the mirrored cutout rows in the cutout evaluator's own
        /// order. The gate runs before the scalar gates because the
        /// derivation-consistency question is about the captured state as a
        /// whole, while the scalar gates judge one feature each. The
        /// mirrored rows keep the cutout evaluator's own order, including
        /// the queue and RenderType checks before the scalar render-state
        /// gates: canonicalization changes both facts and the alpha proof
        /// does not authorize erasing custom source overrides.
        /// </para>
        /// </summary>
        internal static LilToonOpaqueConversionEligibility
            EvaluateVerifiedEligibility(
                CapturedMaterialEvidence evidence,
                int effectiveRenderQueue,
                string effectiveRenderType,
                int mode,
                bool allowDepthTestChange = false)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));

            // 1. Schema, strict on the mirrored source facts and the two
            //    feature scalars the runtime gates read. Attestation and the
            //    resolution already establish this in the production
            //    sequence; repeated here because this is a pure function
            //    that does not assume its caller resolved. The dissolve
            //    vector is the one conversion-read fact that is not a
            //    scalar, so its presence is checked here in the schema gate,
            //    as the transparent family checks _DistanceFade.
            var values = new float[EligibilitySchema.Length];
            for (var index = 0; index < EligibilitySchema.Length; index++)
            {
                if (!evidence.TryGetScalar(EligibilitySchema[index], out values[index]))
                {
                    return LilToonOpaqueConversionEligibility.Refused(
                        LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
                }
            }

            if (!evidence.TryGetVector(
                    DissolveParamsProperty, out var dissolveParams))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
            }

            // 2. Finiteness. Every later gate compares captured values
            //    against pinned constants, and those comparisons are only
            //    meaningful for finite captures: a NaN fails every equality
            //    and set-membership test, which would misreport a broken
            //    capture as a plausible named refusal. The sweep covers the
            //    four dissolve components too, as the cutout interpretation
            //    refuses any non-finite dissolve component.
            foreach (var value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return LilToonOpaqueConversionEligibility.Refused(
                        LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
                }
            }

            if (float.IsNaN(dissolveParams.x) ||
                float.IsInfinity(dissolveParams.x) ||
                float.IsNaN(dissolveParams.y) ||
                float.IsInfinity(dissolveParams.y) ||
                float.IsNaN(dissolveParams.z) ||
                float.IsInfinity(dissolveParams.z) ||
                float.IsNaN(dissolveParams.w) ||
                float.IsInfinity(dissolveParams.w))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
            }

            // 3. The Task 5 mode-consistency gate, unchanged. The gate owns
            //    the derivation table, the two gate scalars, and the
            //    missing-fact policy for its vectors. The conversion
            //    vocabulary has no Multi-gate members — the named Multi
            //    refusals live in LilToonMultiResolutionRefusal at the
            //    resolution layer — so a state the pinned derivation cannot
            //    produce refuses here as an unattested material state.
            if (!LilToonMultiModeGate.Evaluate(evidence, mode, out _))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnattestedMaterial);
            }

            // 4. The mode-1 keyword feature facts as runtime scalar gates.
            //    The cutout vocabulary carries no per-feature member for
            //    them: in the regular family each is proven off at the
            //    interpretation layer before conversion eligibility is ever
            //    consulted, so the family's evaluators hold no row for any
            //    of them. This evaluator is the seam that must refuse on its
            //    own, and each of the three features composes into the alpha
            //    or the coverage before the cutout clip, so each breaks the
            //    one premise the mirrored rows authorize writes against:
            //    alpha exactly one provably surviving the clip. The closed
            //    vocabulary's member for that broken premise is
            //    ClipThresholdDiscardsOpaqueAlpha, and it refuses each of
            //    them. The vocabulary does not widen for Multi.
            //
            //    4a. The alpha mask (_AlphaMaskMode). The mask replaces or
            //    multiplies the alpha before the clip, and this request
            //    carries no mask texture evidence, so a mask that is on
            //    leaves the alpha at the clip unproven. Any non-zero scalar
            //    is on, exactly as the zero gates of
            //    LilToonCutoutMaterialSemantics read the feature.
            if (Read(values, AlphaMaskModeProperty) != 0f)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .ClipThresholdDiscardsOpaqueAlpha);
            }

            //    4b. Dither (_UseDither). At LIL_RENDER 1 the dither block
            //    is live and quantizes the alpha against a threshold before
            //    the clip, so the admitted premise is the exact zero the
            //    cutout conversion design pins for it.
            if (Read(values, DitherProperty) != 0f)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .ClipThresholdDiscardsOpaqueAlpha);
            }

            //    4c. Layer dissolve (_DissolveParams.x). Dissolve mode zero,
            //    exactly, is the admitted state; the x component is the mode
            //    the vendor branches on, and a non-finite component never
            //    reaches this comparison because gate 2 already refused it.
            if (dissolveParams.x != 0f)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .ClipThresholdDiscardsOpaqueAlpha);
            }

            // --- Mirrored cutout rows. Each row below mirrors one
            // LilToonCutoutSourceEligibility gate, unchanged and in the
            // source's own order: the same constants by reference, the same
            // refusal member. Nothing here re-derives a value the cutout
            // family pins, so the two evaluators cannot drift. The number
            // in parentheses is the mirrored gate's number in its own file.

            // 5-6 (cutout gates 3-4). The canonical cutout defaults are the
            //      only admitted source queue and RenderType; the vendor
            //      Multi cutout form writes exactly this pair. The intended
            //      2450 -> 2000 normalization is part of the conversion;
            //      custom overrides express ordering or classification
            //      intent that the alpha proof does not preserve, and a
            //      Multi material refuses them exactly as its regular twin
            //      does.
            if (effectiveRenderQueue !=
                LilToonCutoutSourceEligibility.SupportedCutoutRenderQueue)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedRenderQueue);
            }

            if (!string.Equals(
                    effectiveRenderType,
                    LilToonCutoutSourceEligibility
                        .SupportedCutoutRenderType,
                    StringComparison.Ordinal))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedRenderType);
            }

            // 7 (cutout gate 5). Depth comparison, mirrored. Required to be
            //    LEqual already rather than normalized to it; the opt-in
            //    policy admits Less beside LEqual as a stated, consented
            //    divergence the caller learns through DepthTestDivergence.
            //    See the mirrored row for the full visibility-intent
            //    reasoning.
            var depthComparison = Read(values, "_ZTest");
            if (depthComparison !=
                    LilToonOpaqueConversionFactors.LEqualDepthComparison &&
                !(allowDepthTestChange &&
                  depthComparison ==
                  LilToonOpaqueConversionFactors.LessDepthComparison))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDepthComparison);
            }

            // 8 (cutout gate 6). Depth write, mirrored.
            if (Read(values, "_ZWrite") !=
                LilToonOpaqueConversionFactors.DepthWriteOn)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDepthWrite);
            }

            // 9 (cutout gate 7). Color mask, mirrored.
            if (Read(values, "_ColorMask") !=
                LilToonOpaqueConversionFactors.ColorMaskAll)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedColorMask);
            }

            // 10 (cutout gate 8). Depth offset, mirrored.
            if (Read(values, "_OffsetFactor") != 0f ||
                Read(values, "_OffsetUnits") != 0f)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDepthOffset);
            }

            // 11 (cutout gate 9). Base RGB blend, mirrored. At alpha 1 both
            //     accepted source factors evaluate to 1 and both accepted
            //     destination factors to 0, so the vendor Multi One/Zero
            //     form and the regular family's admitted forms admit
            //     identically.
            if (Read(values, "_BlendOp") !=
                    LilToonOpaqueConversionFactors.BlendOpAdd ||
                !LilToonOpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
                    Read(values, "_SrcBlend")) ||
                !LilToonOpaqueConversionFactors.IsZeroDestinationFactorAtAlphaOne(
                    Read(values, "_DstBlend")))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedBlendEquation);
            }

            // 12 (cutout gate 10). Base alpha blend, mirrored, same
            //     degeneracy argument.
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

            // 13 (cutout gate 11). ForwardAdd blend, mirrored. The Multi
            //     container carries the same FORWARD_ADD pass shape the row
            //     constrains.
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

            // 14 (cutout gate 12). Clip threshold, mirrored, with the
            //     cutout family's own bound by reference. The cutout shader
            //     clips with clip(alpha - _Cutoff), so alpha exactly 1
            //     provably survives only while the threshold stays at or
            //     under the bound. A NaN cutoff never reaches this
            //     comparison: gate 2 already refused it.
            if (Read(values, CutoffProperty) >
                LilToonCutoutSourceEligibility.MaxProvableCutoff)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .ClipThresholdDiscardsOpaqueAlpha);
            }

            // Deliberately ungated, mirroring the cutout evaluator's own
            // list: _AlphaToMask (full coverage holds at alpha one under any
            // value, and the vendor Multi cutout form writes 1),
            // _SrcBlendAlphaFA and _DstBlendAlphaFA (the FORWARD_ADD pass
            // declares its alpha pair as literal Zero One regardless of the
            // stored values). Deliberately ungated and deliberately absent
            // from the schema: every outline scalar, by the 2026-10-01
            // parity ruling — they ride the request and gate nothing, so a
            // Multi material refuses exactly where its regular twin refuses
            // and admits exactly where it admits.

            return LilToonOpaqueConversionEligibility.Convertible(
                depthComparison ==
                LilToonOpaqueConversionFactors.LessDepthComparison);
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
                        EligibilitySchema[index],
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
