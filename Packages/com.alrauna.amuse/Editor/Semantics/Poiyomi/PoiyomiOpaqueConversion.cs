using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    internal enum PoiyomiOpaqueConversionOutcome
    {
        Refused,

        /// <summary>
        /// A successful no-op: the material already carries every canonical
        /// fact, so no clone is created and the caller uses it unchanged.
        /// </summary>
        AlreadyOpaque,

        Convertible,
    }

    /// <summary>
    /// Conversion decisions about one material. Deliberately a separate
    /// vocabulary from <c>RendererAnalysisRefusal</c>, which belongs to
    /// renderer-scoped alpha analysis: merging them would put conversion
    /// conditions where analysis reads them, so unknown conversion state would
    /// start refusing analysis that does not depend on it.
    /// <para>
    /// There is no member for a generated-material read-back disagreement.
    /// That is an invariant failure, not an unsupported material.
    /// </para>
    /// </summary>
    internal enum PoiyomiOpaqueConversionRefusal
    {
        None,

        // Identity
        UnattestedMaterial,

        // Schema / readability
        ConversionPropertyAbsent,
        ConversionPropertyNotFinite,

        // Effective render-state eligibility
        AlphaToCoverageEnabled,
        UnsupportedDepthComparison,
        UnsupportedBlendEquation,
        UnsupportedForwardAddBlendEquation,
        UnsupportedOutlineBlendEquation,
        ClipThresholdDiscardsOpaqueAlpha,
    }

    internal readonly struct PoiyomiOpaqueConversionEligibility
    {
        internal PoiyomiOpaqueConversionOutcome Outcome { get; }
        internal PoiyomiOpaqueConversionRefusal Refusal { get; }

        /// <summary>
        /// True only when the source depth test is Less and the policy
        /// admitted it. The conversion then normalizes the comparison to
        /// LEqual. A caller that moves the material must report the change.
        /// False on every refusal and on AlreadyOpaque: neither outcome
        /// moves the material.
        /// </summary>
        internal bool DepthTestDivergence { get; }

        /// <summary>
        /// True only when the source carries the vendor premultiply
        /// feature. The vendor factor is saturate(alpha), and alpha is
        /// exactly 1 on every triangle the proof moves, so the canonical
        /// clone reproduces the source color exactly there. The clone
        /// still normalizes the source by writing _AlphaPremultiply 0, so
        /// a caller that prepares the material must report the
        /// normalization. False on every refusal and on AlreadyOpaque:
        /// neither outcome moves the material.
        /// </summary>
        internal bool PremultiplyNormalization { get; }

        private PoiyomiOpaqueConversionEligibility(
            PoiyomiOpaqueConversionOutcome outcome,
            PoiyomiOpaqueConversionRefusal refusal,
            bool depthTestDivergence,
            bool premultiplyNormalization)
        {
            Outcome = outcome;
            Refusal = refusal;
            DepthTestDivergence = depthTestDivergence;
            PremultiplyNormalization = premultiplyNormalization;
        }

        internal static PoiyomiOpaqueConversionEligibility Refused(
            PoiyomiOpaqueConversionRefusal refusal)
        {
            if (refusal == PoiyomiOpaqueConversionRefusal.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(refusal), "A refusal must name its cause.");
            }

            return new PoiyomiOpaqueConversionEligibility(
                PoiyomiOpaqueConversionOutcome.Refused, refusal, false,
                false);
        }

        internal static PoiyomiOpaqueConversionEligibility AlreadyOpaque()
        {
            return new PoiyomiOpaqueConversionEligibility(
                PoiyomiOpaqueConversionOutcome.AlreadyOpaque,
                PoiyomiOpaqueConversionRefusal.None,
                false,
                false);
        }

        internal static PoiyomiOpaqueConversionEligibility Convertible(
            bool depthTestDivergence = false,
            bool premultiplyNormalization = false)
        {
            return new PoiyomiOpaqueConversionEligibility(
                PoiyomiOpaqueConversionOutcome.Convertible,
                PoiyomiOpaqueConversionRefusal.None,
                depthTestDivergence,
                premultiplyNormalization);
        }
    }

    /// <summary>
    /// The pinned Poiyomi opaque-conversion core: decides whether an attested
    /// unlocked Poiyomi Toon 9.3.64 material may be normalized to its canonical
    /// Opaque counterpart, and prepares a transient validated clone when it may.
    /// <para>
    /// Semantics describe output facts; conversion decides mutation. This class
    /// therefore models render state that <see cref="MaterialSemantics"/>
    /// deliberately does not, and <see cref="MaterialSemantics"/> is unchanged.
    /// </para>
    /// <para>
    /// The recipe below was derived from the vendor's own <c>_Mode</c> preset 0
    /// <c>on_value_actions</c> metadata inside the attested shader source, whose
    /// identity is pinned by
    /// <see cref="PoiyomiMaterialSemantics.CanonicalShaderGuid"/> and
    /// <see cref="PoiyomiMaterialSemantics.CanonicalNormalizedSourceHash"/>.
    /// <strong>Changing either pin requires re-deriving this tuple from the new
    /// source before the pin is updated.</strong>
    /// </para>
    /// <para>
    /// This is one shader, one direction, one version. It is deliberately not a
    /// render-state framework, a pass model, a conversion interface, or a
    /// provider registry.
    /// </para>
    /// </summary>
    internal static class PoiyomiOpaqueConversion
    {
        // --- Canonical Opaque recipe (pinned Poiyomi Toon 9.3.64) -----------

        /// <summary>
        /// The vendor's Opaque preset carries 24 actions: the 22 property
        /// writes below, the render queue, and the <c>RenderType</c> tag.
        /// Selecting the preset also sets <c>_Mode</c> itself, so the complete
        /// recipe is these 23 properties plus the two non-property facts.
        /// <para>
        /// <c>_OutlineDstBlendAlpha</c> is 0 here and 1 in every other preset;
        /// it is the field a recipe assembled by copying a neighbouring preset
        /// would get wrong.
        /// </para>
        /// <para>
        /// <c>_AddBlendOp</c> and <c>_AddBlendOpAlpha</c> are absent because no
        /// preset action writes them. Conversion leaves them untouched, so the
        /// blend operation is identical on both sides and cancels once the
        /// factors are proven equivalent at alpha 1. They are therefore not
        /// conversion dependencies and must not enter the evidence request,
        /// eligibility, or relevance. The same holds for <c>_OutlineZWrite</c>,
        /// <c>_OutlineZTest</c> and <c>_OutlineCull</c>.
        /// </para>
        /// </summary>
        private static readonly (string Property, float Value)[] CanonicalOpaqueTuple =
        {
            ("_Mode", 0f),
            ("_AlphaForceOpaque", 1f),
            ("_BlendOp", 0f),
            ("_BlendOpAlpha", 4f),
            ("_Cutoff", 0f),
            ("_SrcBlend", 1f),
            ("_DstBlend", 0f),
            ("_SrcBlendAlpha", 1f),
            ("_DstBlendAlpha", 1f),
            ("_AddSrcBlend", 1f),
            ("_AddDstBlend", 1f),
            ("_AddSrcBlendAlpha", 0f),
            ("_AddDstBlendAlpha", 1f),
            ("_AlphaToCoverage", 0f),
            ("_ZWrite", 1f),
            ("_ZTest", 4f),
            ("_AlphaPremultiply", 0f),
            ("_OutlineSrcBlend", 1f),
            ("_OutlineDstBlend", 0f),
            ("_OutlineSrcBlendAlpha", 1f),
            ("_OutlineDstBlendAlpha", 0f),
            ("_OutlineBlendOp", 0f),
            ("_OutlineBlendOpAlpha", 4f),
        };

        internal static IReadOnlyList<(string Property, float Value)>
            CanonicalOpaqueProperties { get; } =
                new ReadOnlyCollection<(string, float)>(CanonicalOpaqueTuple);

        // The three Unity-level facts live once, on the shared verification
        // skeleton. These aliases keep the family's own constant names for
        // its callers. The values never drift, because each alias reads the
        // one shared definition.
        internal const int CanonicalOpaqueRenderQueue =
            CanonicalOpaqueVerification.CanonicalOpaqueRenderQueue;
        internal const string RenderTypeTagName =
            CanonicalOpaqueVerification.RenderTypeTagName;
        internal const string CanonicalOpaqueRenderType =
            CanonicalOpaqueVerification.CanonicalOpaqueRenderType;

        // --- Conversion evidence -------------------------------------------

        private const string ShaderOptimizerEnabledProperty =
            "_ShaderOptimizerEnabled";

        /// <summary>
        /// The 23 properties conversion reads: the canonical recipe
        /// properties. Used both as the request's presence schema and
        /// as the conversion source-attestation schema.
        /// </summary>
        private static readonly string[] ConversionSchema = BuildConversionSchema();

        internal static IReadOnlyCollection<string>
            ConversionRequiredSchemaProperties { get; } =
                new ReadOnlyCollection<string>(ConversionSchema);

        /// <summary>
        /// Conversion's own request. It is independently sufficient for
        /// conversion source attestation and conversion eligibility, so
        /// conversion never runs the alpha capture path.
        /// <para>
        /// Material-dependency closure combines this into the broader schema
        /// one capture gathers, so conversion evidence is captured alongside
        /// alpha evidence. That combination does not merge the two questions:
        /// this request remains independently usable as conversion's own
        /// relevance, and
        /// <see cref="PoiyomiMaterialSemantics.AlphaEvidenceRequest"/> remains
        /// what ordinary alpha proof considers. Keeping them separate is what
        /// stops conversion-only render state from making alpha analysis refuse
        /// on state alpha does not depend on - a coverage regression, not a
        /// safety improvement.
        /// </para>
        /// </summary>
        internal static MaterialEvidenceRequest ConversionEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: ConversionSchema,
                scalarProperties: BuildScalarProperties(),
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties:
                    Array.Empty<TexturePropertyEvidenceRequest>());

        // --- Eligibility -----------------------------------------------------

        /// <summary>
        /// Pure evaluation over already-captured, already-admitted conversion
        /// evidence plus the two effective non-property facts. It performs no
        /// capture and touches no live material, so it cannot read mutable
        /// state after the evidence a decision depends on.
        /// <para>
        /// Order is load-bearing: the finiteness sweep is a data-validity
        /// refusal, so it precedes the no-op classification. Every gate whose
        /// only purpose is to authorize mutation still follows the no-op
        /// classification, because a refusal mutates nothing.
        /// </para>
        /// </summary>
        internal static PoiyomiOpaqueConversionEligibility EvaluateVerifiedEligibility(
            CapturedMaterialEvidence evidence,
            int effectiveRenderQueue,
            string effectiveRenderType,
            bool allowDepthTestChange = false)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));

            // 1. Schema. Attestation already establishes this in the production
            //    sequence; repeated here because this is a pure function that
            //    does not assume its caller attested.
            var values = new float[ConversionSchema.Length];
            for (var index = 0; index < ConversionSchema.Length; index++)
            {
                if (!evidence.TryGetScalar(ConversionSchema[index], out values[index]))
                {
                    return PoiyomiOpaqueConversionEligibility.Refused(
                        PoiyomiOpaqueConversionRefusal.ConversionPropertyAbsent);
                }
            }

            // 2. Finiteness.
            foreach (var value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return PoiyomiOpaqueConversionEligibility.Refused(
                        PoiyomiOpaqueConversionRefusal.ConversionPropertyNotFinite);
                }
            }

            // 3. AlreadyOpaque, before any transformation gate.
            if (IsFunctionallyOpaque(values, effectiveRenderQueue, effectiveRenderType))
            {
                return PoiyomiOpaqueConversionEligibility.AlreadyOpaque();
            }

            // --- Transformation gates. Each exists to authorize a change, so
            // none is reachable once step 3 has established that nothing will
            // be changed.
            // 4. Premultiplication. The vendor premultiply feature scales
            //    the color by saturate(alpha) in three passes and never
            //    writes the alpha value (note 4.3). The proof moves only
            //    triangles whose alpha is exactly 1, so the factor is
            //    exactly 1 on the whole proven domain and the canonical
            //    clone reproduces the source color exactly there. The
            //    feature is therefore admitted. The normalization is a
            //    stated, disclosed change: the caller learns it through
            //    PremultiplyNormalization. The recipe still writes 0. The
            //    scoping stays with the proven plan, which moves only the
            //    proven triangles, so the disclosed premise never reaches
            //    an unproven triangle.
            var premultiplied = Read(values, "_AlphaPremultiply") != 0f;

            // 5. Coverage.
            if (Read(values, "_AlphaToCoverage") != 0f)
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal.AlphaToCoverageEnabled);
            }

            // 6. Depth comparison. Required to be LEqual already rather than
            //    normalized to it: a different comparison changes visibility
            //    independently of alpha, so a material authored to draw with
            //    Always, Greater or Disabled expresses a visibility intent the
            //    alpha proof knows nothing about. The opt-in policy admits
            //    Less beside LEqual (design §D2). Less and LEqual differ only
            //    at exact depth equality. That class depends on depth-buffer
            //    population. No per-pixel proof reaches it. The admission is
            //    a stated, consented divergence. The caller learns it through
            //    DepthTestDivergence. The recipe still writes 4. Without the
            //    policy that write is a no-op. With the policy it is the
            //    reported normalization.
            var depthComparison = Read(values, "_ZTest");
            if (depthComparison !=
                    OpaqueConversionFactors.LEqualDepthComparison &&
                !(allowDepthTestChange &&
                  depthComparison ==
                    OpaqueConversionFactors.LessDepthComparison))
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal.UnsupportedDepthComparison);
            }

            // 7. Base RGB blend. At alpha 1 both accepted source factors
            //    evaluate to 1 and both accepted destination factors to 0, so
            //    the blend degenerates to `dst := src` and normalizing to
            //    One/Zero is an identity there.
            if (Read(values, "_BlendOp") !=
                    OpaqueConversionFactors.BlendOpAdd ||
                !OpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
                    Read(values, "_SrcBlend")) ||
                !OpaqueConversionFactors.IsZeroDestinationFactorAtAlphaOne(
                    Read(values, "_DstBlend")))
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal.UnsupportedBlendEquation);
            }

            // 8. ForwardAdd RGB blend FACTORS, which the recipe rewrites to
            //    One/One. At alpha 1 the accepted source factors evaluate to 1
            //    and the accepted destination factor to 1, so the accepted
            //    states are equivalent to the canonical tuple. The blend
            //    OPERATION is deliberately unconstrained - the recipe never
            //    writes it, so it cancels (see the recipe's remarks).
            if (!OpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
                    Read(values, "_AddSrcBlend")) ||
                Read(values, "_AddDstBlend") !=
                    OpaqueConversionFactors.BlendFactorOne)
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal
                        .UnsupportedForwardAddBlendEquation);
            }

            // 9. Outline blend tuple. The recipe writes the six outline blend
            //    fields, so the accepted source states must degenerate to the
            //    canonical tuple at alpha 1 exactly like the base pass. The RGB
            //    pair uses the base-pass argument: Add with a unit source factor
            //    and a zero destination factor gives dst := src at alpha 1. The
            //    alpha pair uses the alpha-proof vocabulary: with source factor
            //    One and the destination factor in the vendor set, both the add
            //    and the max operations yield exactly 1 at alpha 1. A material
            //    that stores any other pair renders its outline differently from
            //    the canonical clone, so it refuses by name.
            if (Read(values, "_OutlineBlendOp") !=
                    OpaqueConversionFactors.BlendOpAdd ||
                !OpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
                    Read(values, "_OutlineSrcBlend")) ||
                !OpaqueConversionFactors.IsZeroDestinationFactorAtAlphaOne(
                    Read(values, "_OutlineDstBlend")) ||
                Read(values, "_OutlineSrcBlendAlpha") !=
                    OpaqueConversionFactors.BlendFactorOne ||
                (Read(values, "_OutlineDstBlendAlpha") !=
                    OpaqueConversionFactors.BlendFactorZero &&
                 Read(values, "_OutlineDstBlendAlpha") !=
                    OpaqueConversionFactors.BlendFactorOne &&
                 Read(values, "_OutlineDstBlendAlpha") !=
                    OpaqueConversionFactors.BlendFactorOneMinusSrcAlpha) ||
                (Read(values, "_OutlineBlendOpAlpha") !=
                    OpaqueConversionFactors.BlendOpAdd &&
                 Read(values, "_OutlineBlendOpAlpha") !=
                    OpaqueConversionFactors.BlendOpMax))
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal.UnsupportedOutlineBlendEquation);
            }

            // 10. Clip threshold. The pinned shader clips unconditionally in
            //     all four shading passes with `clip(alpha - _Cutoff)`, which
            //     discards when the difference is negative. Alpha exactly 1
            //     therefore survives precisely when _Cutoff <= 1. Only once
            //     this holds is writing _Cutoff = 0 justified: it is a
            //     consequence of the proof, not a premise of it.
            if (Read(values, "_Cutoff") > 1f)
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal.ClipThresholdDiscardsOpaqueAlpha);
            }

            return PoiyomiOpaqueConversionEligibility.Convertible(
                depthComparison == OpaqueConversionFactors.LessDepthComparison,
                premultiplied);
        }

        /// <summary>
        /// Reads one conversion-read property from the captured values, which
        /// are indexed by <see cref="ConversionSchema"/> order.
        /// </summary>
        private static float Read(IReadOnlyList<float> values, string property)
        {
            for (var index = 0; index < ConversionSchema.Length; index++)
            {
                if (string.Equals(
                        ConversionSchema[index], property, StringComparison.Ordinal))
                {
                    return values[index];
                }
            }

            throw new ArgumentException(
                "Property '" + property + "' is not conversion-read.",
                nameof(property));
        }

        /// <summary>
        /// Checks whether the material is already functionally opaque.
        /// It checks essential properties for opaque rendering.
        /// It ignores inactive properties such as alpha blending factors.
        /// </summary>
        private static bool IsFunctionallyOpaque(
            IReadOnlyList<float> values,
            int effectiveRenderQueue,
            string effectiveRenderType)
        {
            var mode = Read(values, "_Mode");
            var alphaForceOpaque = Read(values, "_AlphaForceOpaque");
            if (mode != 0f && alphaForceOpaque != 1f)
            {
                return false;
            }

            if (Read(values, "_BlendOp") != 0f ||
                Read(values, "_SrcBlend") != 1f ||
                Read(values, "_DstBlend") != 0f ||
                Read(values, "_ZWrite") != 1f ||
                Read(values, "_ZTest") != 4f ||
                Read(values, "_AddSrcBlend") != 1f ||
                Read(values, "_AddDstBlend") != 1f ||
                Read(values, "_AlphaToCoverage") != 0f ||
                Read(values, "_AlphaPremultiply") != 0f)
            {
                return false;
            }

            if (Read(values, "_Cutoff") > 1f)
            {
                return false;
            }

            return effectiveRenderQueue == CanonicalOpaqueRenderQueue &&
                   string.Equals(
                       effectiveRenderType,
                       CanonicalOpaqueRenderType,
                       StringComparison.Ordinal);
        }

        // --- Effective render state and canonical-fact comparison -----------

        /// <summary>
        /// Reports the first of the 25 canonical facts the candidate disagrees
        /// with, in a deterministic order: recipe order, then the render queue,
        /// then the <c>RenderType</c> tag. A property the material does not
        /// declare is reported by name; the caller decides whether that is a
        /// refusal or a defect.
        /// </summary>
        internal static bool TryFindNonCanonicalFact(
            UnityEngine.Material candidate,
            out string factName)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));

            return CanonicalOpaqueVerification.TryFindNonCanonicalFact(
                candidate, CanonicalOpaqueTuple, out factName);
        }

        // --- Preparation ------------------------------------------------------

        /// <summary>
        /// Clones the source, applies the complete canonical Opaque tuple, then
        /// re-reads and validates every one of the 25 canonical facts.
        /// <para>
        /// The source material is never written: <c>new Material(source)</c> is
        /// the only relationship between them. Source avatar assets are
        /// evidence, not mutation targets.
        /// </para>
        /// <para>
        /// Nothing is saved. Persistence belongs to assignment, which is the
        /// consumer's job, so this method takes no asset saver and cannot
        /// persist anything by accident. The clone is also left unnamed: naming
        /// generated assets is a consumer obligation, because container
        /// sub-asset names come from the object's own name and NDMF guarantees
        /// no determinism.
        /// </para>
        /// <para>
        /// The clone, write, verify, destroy lifecycle lives on the shared
        /// skeleton in
        /// <see cref="Alrauna.Amuse.Editor.Semantics.CanonicalOpaqueVerification"/>.
        /// This family keeps its own shader, adds no extra writes, and adds
        /// no extra fact.
        /// </para>
        /// <para>
        /// Its precondition is an attested and eligible source.
        /// </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// A written canonical fact did not read back. Eligibility has already
        /// proven every property present and every input finite, and this
        /// method clones the same shader and writes exact canonical constants,
        /// so a disagreement falsifies the assumption that AMUSE can write this
        /// material's render state. That is a compatibility or programming
        /// failure, not an unsupported material, and converting it into a
        /// conservative refusal would hide a broken write path behind a
        /// plausible-looking "preserved the input" outcome. The clone is
        /// destroyed before this is thrown, so no material leaks.
        /// </exception>
        internal static UnityEngine.Material PrepareCanonicalOpaqueClone(
            UnityEngine.Material source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // The clone, write, verify, destroy lifecycle lives on the
            // shared skeleton. This family keeps its own shader, so the
            // target is null and the identity check reads the source
            // shader. The family adds no writes and no extra fact.
            return CanonicalOpaqueVerification.PrepareCanonicalClone(
                source,
                null,
                CanonicalOpaqueTuple,
                _ => { },
                _ => null,
                "Generated opaque material did not preserve the source shader.");
        }

        // --- Conversion source attestation ----------------------------------

        /// <summary>
        /// Narrow conversion entry to the shared Poiyomi source-evidence
        /// gatherer, passing conversion's own schema. Verification stays
        /// <see cref="PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity"/>, so
        /// hashing, GUID and package lookup, locked-state gathering, and the
        /// identity conjunction are reused rather than duplicated.
        /// <para>
        /// It takes already-captured evidence and deliberately offers no
        /// live-<see cref="UnityEngine.Material"/> overload: capture belongs to
        /// the caller that owns the capture schema, and re-capturing here would
        /// read mutable state after the evidence a decision depends on.
        /// </para>
        /// </summary>
        internal static PoiyomiSourceEvidence GatherConversionSourceEvidence(
            UnityEngine.Shader shader,
            CapturedMaterialEvidence evidence)
        {
            return PoiyomiMaterialSemantics.GatherSourceEvidence(
                shader, evidence, ConversionSchema);
        }

        private static string[] BuildConversionSchema()
        {
            var schema = new string[CanonicalOpaqueTuple.Length];
            for (var index = 0; index < CanonicalOpaqueTuple.Length; index++)
            {
                schema[index] = CanonicalOpaqueTuple[index].Property;
            }

            return schema;
        }

        /// <summary>
        /// The schema plus the locked-state flag the shared source-evidence
        /// gatherer reads. Derived rather than retyped so the two cannot drift.
        /// </summary>
        private static string[] BuildScalarProperties()
        {
            var scalars = new string[ConversionSchema.Length + 1];
            Array.Copy(ConversionSchema, scalars, ConversionSchema.Length);
            scalars[ConversionSchema.Length] = ShaderOptimizerEnabledProperty;
            return scalars;
        }
    }
}
