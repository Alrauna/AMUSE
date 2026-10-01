using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Multi source eligibility for mode 1 (cutout) and mode 2
    /// (transparent), on either supported container. Parity rule, pinned
    /// 2026-10-01: after the Multi-only gates, the resolved mode's own
    /// regular evaluator judges the material through delegation, so parity
    /// against the regular twin is outcome equality by construction, not
    /// by maintained transcription. The S8 outline support is
    /// attestation, classification, and target resolution only; a
    /// <c>Hidden/lilToonCutoutOutline</c> source classifies to the cutout
    /// family and converts through the plain cutout evaluator, whose schema
    /// names no <c>_Outline</c> property. This evaluator therefore carries
    /// no outline-specific eligibility row on either container. The
    /// outline-alpha protection question (F0 §7.3) is a pre-existing
    /// regular-family scope fact that this evaluator neither widens nor
    /// narrows.
    /// <para>
    /// The Multi-only prefix before the delegation: the mode's strict
    /// schema on the mirrored source facts, the finiteness sweep over the
    /// schema scalars and the dissolve and distance-fade vectors, the Task
    /// 5 mode-consistency gate, and the keyword feature facts as runtime
    /// scalar gates on the captured feature scalars. A keyword is never
    /// proof of a feature and a feature never hides behind its keyword. The
    /// outline property scalars ride the conversion request and gate
    /// nothing here.
    /// </para>
    /// </summary>
    internal static class LilToonMultiSourceEligibility
    {
        /// <summary>
        /// The mode scalar the Task 5 derivation and the vendor editor call
        /// the transparent mode, the value the mode-consistency gate admits
        /// at the transparent rows.
        /// </summary>
        private const int TransparentMode = 2;
        private const string CutoffProperty = "_Cutoff";
        private const string AlphaMaskModeProperty = "_AlphaMaskMode";
        private const string DitherProperty = "_UseDither";
        private const string AlphaBoostFaProperty = "_AlphaBoostFA";
        private const string SubpassCutoffProperty = "_SubpassCutoff";
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
            AlphaBoostFaProperty,
            SubpassCutoffProperty,
        };

        /// <summary>
        /// The mode-1 source facts the strict schema and the scalar gates
        /// read: the mirrored cutout set. The dither scalar is mode-1 only,
        /// because the dither block is compiled out at LIL_RENDER 2.
        /// </summary>
        private static readonly string[] CutoutSourceSchema =
        {
            LilToonSourceAttestation.ShaderFormatVersionProperty,
            CutoffProperty,
            AlphaMaskModeProperty,
            DitherProperty,
        };

        /// <summary>
        /// The mode-2 source facts the strict schema and the mirrored
        /// transparent rows read: the mirrored transparent set, minus the
        /// distance-fade vector the mode-2 schema gate checks as a vector.
        /// The dither scalar is deliberately absent, exactly as the
        /// transparent evaluator records: the dither block exists only
        /// under LIL_RENDER == 1, and this mode is LIL_RENDER 2, so a gate
        /// on it would be a free false negative.
        /// </summary>
        private static readonly string[] TransparentSourceSchema =
        {
            LilToonSourceAttestation.ShaderFormatVersionProperty,
            CutoffProperty,
            AlphaMaskModeProperty,
            AlphaBoostFaProperty,
            SubpassCutoffProperty,
        };

        /// <summary>
        /// The source scalars both modes read side by side: the capture
        /// request is mode-independent, so it carries the mode-1 facts and
        /// the mode-2 facts in one set, while each mode's strict schema
        /// names only its own.
        /// </summary>
        private static readonly string[] SourceSchema =
        {
            LilToonSourceAttestation.ShaderFormatVersionProperty,
            CutoffProperty,
            AlphaMaskModeProperty,
            DitherProperty,
            AlphaBoostFaProperty,
            SubpassCutoffProperty,
        };

        private static readonly string[] SourceVectorSchema =
        {
            DissolveParamsProperty,
        };

        /// <summary>
        /// The Multi source's own eligibility evidence: the mirrored cutout
        /// facts and the mirrored transparent facts in one mode-independent
        /// set, plus the feature scalars the runtime gates read. The
        /// dissolve and distance-fade vectors sit beside them. The recipe
        /// never writes any of them, and they are not target evidence. Each
        /// mode's strict schema still demands only the facts its own rows
        /// read.
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
        /// finiteness sweep and <see cref="Read"/> index by. There is one
        /// schema per mode, because the mirrored rows the two modes judge
        /// differ: the mode-1 schema names the mirrored cutout facts and the
        /// mode-2 schema the mirrored transparent facts. The gate-owned
        /// facts the mode-consistency gate reads keep that gate's own
        /// missing-fact policy instead, and the outline scalars are captured
        /// but swept by neither.
        /// </summary>
        private static readonly string[] EligibilitySchema =
            BuildEligibilitySchema(CutoutSourceSchema);

        private static readonly string[] TransparentEligibilitySchema =
            BuildEligibilitySchema(TransparentSourceSchema);

        private static string[] BuildEligibilitySchema(string[] sourceSchema)
        {
            var recipe = LilToonOpaqueTarget.RecipeSchemaProperties;
            var schema = new string[
                recipe.Count + sourceSchema.Length];
            var index = 0;
            foreach (var property in recipe)
            {
                schema[index++] = property;
            }

            foreach (var property in sourceSchema)
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
        /// The load-bearing order: the strict schema check of the mode's
        /// own facts, then finiteness over the schema scalars and the
        /// dissolve vector, then the Task 5
        /// mode-consistency gate, then the runtime scalar feature gates,
        /// then the mode's regular evaluator, delegated, in that
        /// evaluator's own order. The gate runs before the scalar gates
        /// because the derivation-consistency question is about the
        /// captured state as a whole, while the scalar gates judge one
        /// feature each. The delegated evaluator keeps its own order,
        /// including the queue and RenderType checks before the scalar
        /// render-state gates: canonicalization changes both facts and the
        /// alpha proof does not authorize erasing custom source overrides.
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

            // 1. Schema, strict on the mirrored source facts of the mode
            //    under evaluation. Attestation and the resolution already
            //    establish this in the production sequence; repeated here
            //    because this is a pure function that does not assume its
            //    caller resolved. The dissolve vector is one of the
            //    conversion-read facts that are not scalars, so its
            //    presence is checked here in the schema gate, as the
            //    transparent family checks _DistanceFade; the mode-2 arm
            //    checks the distance-fade vector the same way, because its
            //    mirrored transparent rows gate on it.
            var schema = mode == TransparentMode
                ? TransparentEligibilitySchema
                : EligibilitySchema;
            var values = new float[schema.Length];
            for (var index = 0; index < schema.Length; index++)
            {
                if (!evidence.TryGetScalar(schema[index], out values[index]))
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

            var hasDistanceFade = evidence.TryGetVector(
                DistanceFadeProperty, out var distanceFade);
            if (mode == TransparentMode && !hasDistanceFade)
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
            //    refuses any non-finite dissolve component. The mode-2
            //    sweep covers the four distance-fade components as well:
            //    they feed the mirrored transparent distance-fade row, and
            //    a non-finite component must be refused as
            //    ConversionPropertyNotFinite, not as UnsupportedDistanceFade.
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

            if (mode == TransparentMode)
            {
                if (float.IsNaN(distanceFade.x) ||
                    float.IsInfinity(distanceFade.x) ||
                    float.IsNaN(distanceFade.y) ||
                    float.IsInfinity(distanceFade.y) ||
                    float.IsNaN(distanceFade.z) ||
                    float.IsInfinity(distanceFade.z) ||
                    float.IsNaN(distanceFade.w) ||
                    float.IsInfinity(distanceFade.w))
                {
                    return LilToonOpaqueConversionEligibility.Refused(
                        LilToonOpaqueConversionRefusal
                            .ConversionPropertyNotFinite);
                }
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

            // 4. The keyword feature facts as runtime scalar gates.
            //    The mirrored vocabularies carry no per-feature member for
            //    them: in the regular family each is proven off at the
            //    interpretation layer before conversion eligibility is ever
            //    consulted, so the family's evaluators hold no row for any
            //    of them. This evaluator is the seam that must refuse on
            //    its own, and each feature it gates composes into the alpha
            //    or the coverage before the clip, so it breaks the one
            //    premise the mirrored rows authorize writes against: alpha
            //    exactly one provably surviving the clip. The closed
            //    vocabulary's member for that broken premise is
            //    ClipThresholdDiscardsOpaqueAlpha, and it refuses each of
            //    them. The vocabulary does not widen for Multi.
            //
            //    4a. The alpha mask (_AlphaMaskMode). The mask replaces or
            //    multiplies the alpha before the clip, and this request
            //    carries no mask texture evidence, so a mask that is on
            //    leaves the alpha at the clip unproven. Any non-zero scalar
            //    is on, exactly as the zero gates of
            //    LilToonCutoutMaterialSemantics read the feature. The mask
            //    block is compiled in at LIL_RENDER 1 and 2 alike, so the
            //    gate runs on both arms.
            if (Read(schema, values, AlphaMaskModeProperty) != 0f)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .ClipThresholdDiscardsOpaqueAlpha);
            }

            //    4b. Dither (_UseDither), mode 1 only. At LIL_RENDER 1 the
            //    dither block is live and quantizes the alpha against a
            //    threshold before the clip, so the admitted premise is the
            //    exact zero the cutout conversion design pins for it. At
            //    LIL_RENDER 2 the block is compiled out — the dither rows
            //    exist only under LIL_RENDER == 1 — so the scalar gates
            //    nothing at mode 2, exactly as the transparent evaluator
            //    records: a gate there would be a free false negative.
            if (mode != TransparentMode &&
                Read(schema, values, DitherProperty) != 0f)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .ClipThresholdDiscardsOpaqueAlpha);
            }

            //    4c. Layer dissolve (_DissolveParams.x). Dissolve mode zero,
            //    exactly, is the admitted state; the x component is the mode
            //    the vendor branches on, and a non-finite component never
            //    reaches this comparison because gate 2 already refused it.
            //    The dissolve block carries no LIL_RENDER condition, so the
            //    gate runs on both arms.
            if (dissolveParams.x != 0f)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal
                        .ClipThresholdDiscardsOpaqueAlpha);
            }

            // 5. The mode's own regular evaluator, delegated. Parity is by
            //    construction, not by maintenance: the resolved mode's
            //    evaluator judges the same captured evidence with its own
            //    schema, finiteness sweep, gate order, constants, refusal
            //    members, and DepthTestDivergence answer, so a Multi
            //    material refuses exactly where its regular twin refuses
            //    and admits exactly where it admits (the 2026-10-01 parity
            //    rule). The delegated schema is a subset of the schema gate
            //    this prefix already passed, so the evaluator's own
            //    schema-and-finiteness head cannot fire here.
            return mode == TransparentMode
                ? LilToonTransparentSourceEligibility
                    .EvaluateVerifiedEligibility(
                        evidence,
                        effectiveRenderQueue,
                        effectiveRenderType,
                        allowDepthTestChange)
                : LilToonCutoutSourceEligibility
                    .EvaluateVerifiedEligibility(
                        evidence,
                        effectiveRenderQueue,
                        effectiveRenderType,
                        allowDepthTestChange);
        }

        /// <summary>
        /// Reads one conversion-read property from the captured values,
        /// which are indexed by the evaluated mode's schema order.
        /// </summary>
        private static float Read(
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
