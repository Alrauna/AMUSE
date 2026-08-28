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
        OutlinesEnabled,
        PremultipliedAlphaEnabled,
        AlphaToCoverageEnabled,
        UnsupportedDepthComparison,
        UnsupportedBlendEquation,
        UnsupportedForwardAddBlendEquation,
        ClipThresholdDiscardsOpaqueAlpha,
    }

    internal readonly struct PoiyomiOpaqueConversionEligibility
    {
        internal PoiyomiOpaqueConversionOutcome Outcome { get; }
        internal PoiyomiOpaqueConversionRefusal Refusal { get; }

        private PoiyomiOpaqueConversionEligibility(
            PoiyomiOpaqueConversionOutcome outcome,
            PoiyomiOpaqueConversionRefusal refusal)
        {
            Outcome = outcome;
            Refusal = refusal;
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
                PoiyomiOpaqueConversionOutcome.Refused, refusal);
        }

        internal static PoiyomiOpaqueConversionEligibility AlreadyOpaque()
        {
            return new PoiyomiOpaqueConversionEligibility(
                PoiyomiOpaqueConversionOutcome.AlreadyOpaque,
                PoiyomiOpaqueConversionRefusal.None);
        }

        internal static PoiyomiOpaqueConversionEligibility Convertible()
        {
            return new PoiyomiOpaqueConversionEligibility(
                PoiyomiOpaqueConversionOutcome.Convertible,
                PoiyomiOpaqueConversionRefusal.None);
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

        internal const int CanonicalOpaqueRenderQueue = 2000;
        internal const string RenderTypeTagName = "RenderType";
        internal const string CanonicalOpaqueRenderType = "Opaque";

        // --- Conversion evidence -------------------------------------------

        /// <summary>
        /// The one property outside the canonical recipe that conversion reads.
        /// The vendor's outline pass opens with
        /// <c>clip(_EnableOutlines - 0.01)</c>, then replaces or multiplies
        /// alpha from outline texture/colour and an optional distance fade -
        /// none of which AMUSE models - before <c>_Mode == Opaque</c> forces
        /// that alpha to 1 ahead of the outline clip. Conversion therefore
        /// requires outlines disabled whenever it would mutate the material.
        /// </summary>
        private const string EnableOutlinesProperty = "_EnableOutlines";

        private const string ShaderOptimizerEnabledProperty =
            "_ShaderOptimizerEnabled";

        /// <summary>
        /// The 24 properties conversion reads: the 23 recipe properties plus
        /// <see cref="EnableOutlinesProperty"/>. Used both as the request's
        /// presence schema and as the conversion source-attestation schema.
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
        /// Order is load-bearing: the no-op classification precedes every gate
        /// whose only purpose is to authorize mutation.
        /// </para>
        /// </summary>
        internal static PoiyomiOpaqueConversionEligibility EvaluateVerifiedEligibility(
            CapturedMaterialEvidence evidence,
            int effectiveRenderQueue,
            string effectiveRenderType)
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

            // 2. AlreadyOpaque, before any transformation gate.
            if (IsCanonicalOpaque(values, effectiveRenderQueue, effectiveRenderType))
            {
                return PoiyomiOpaqueConversionEligibility.AlreadyOpaque();
            }

            // --- Transformation gates. Each exists to authorize a change, so
            // none is reachable once step 2 has established that nothing will
            // be changed. Exactly one of them - the outline gate - can fail on
            // a material that is otherwise canonical, because _EnableOutlines
            // is the only conversion-read property the recipe does not write.

            // 3. Finiteness.
            foreach (var value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return PoiyomiOpaqueConversionEligibility.Refused(
                        PoiyomiOpaqueConversionRefusal.ConversionPropertyNotFinite);
                }
            }

            // 4. Outlines. See EnableOutlinesProperty for why this refuses.
            if (Read(values, EnableOutlinesProperty) != 0f)
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal.OutlinesEnabled);
            }

            // 5. Premultiplication changes how RGB is PRODUCED, not how it is
            //    combined, so the blend predicate cannot excuse it.
            if (Read(values, "_AlphaPremultiply") != 0f)
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal.PremultipliedAlphaEnabled);
            }

            // 6. Coverage.
            if (Read(values, "_AlphaToCoverage") != 0f)
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal.AlphaToCoverageEnabled);
            }

            // 7. Depth comparison. Required to be LEqual already rather than
            //    normalized to it: a different comparison changes visibility
            //    independently of alpha, so a material authored to draw with
            //    Always, Greater or Disabled expresses a visibility intent the
            //    alpha proof knows nothing about. The recipe still writes 4;
            //    on an eligible material that write is a no-op.
            if (Read(values, "_ZTest") != LEqualDepthComparison)
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal.UnsupportedDepthComparison);
            }

            // 8. Base RGB blend. At alpha 1 both accepted source factors
            //    evaluate to 1 and both accepted destination factors to 0, so
            //    the blend degenerates to `dst := src` and normalizing to
            //    One/Zero is an identity there.
            if (Read(values, "_BlendOp") != BlendOpAdd ||
                !IsUnitSourceFactorAtAlphaOne(Read(values, "_SrcBlend")) ||
                !IsZeroDestinationFactorAtAlphaOne(Read(values, "_DstBlend")))
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal.UnsupportedBlendEquation);
            }

            // 9. ForwardAdd RGB blend FACTORS, which the recipe rewrites to
            //    One/One. At alpha 1 the accepted source factors evaluate to 1
            //    and the accepted destination factor to 1, so the accepted
            //    states are equivalent to the canonical tuple. The blend
            //    OPERATION is deliberately unconstrained - the recipe never
            //    writes it, so it cancels (see the recipe's remarks).
            if (!IsUnitSourceFactorAtAlphaOne(Read(values, "_AddSrcBlend")) ||
                Read(values, "_AddDstBlend") != BlendFactorOne)
            {
                return PoiyomiOpaqueConversionEligibility.Refused(
                    PoiyomiOpaqueConversionRefusal
                        .UnsupportedForwardAddBlendEquation);
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

            return PoiyomiOpaqueConversionEligibility.Convertible();
        }

        // Unity blend enum: Zero=0, One=1, DstColor=2, SrcColor=3,
        // OneMinusDstColor=4, SrcAlpha=5, DstAlpha=7, OneMinusSrcAlpha=10.
        private const float BlendOpAdd = 0f;
        private const float BlendFactorZero = 0f;
        private const float BlendFactorOne = 1f;
        private const float BlendFactorSrcAlpha = 5f;
        private const float BlendFactorOneMinusSrcAlpha = 10f;

        // UnityEngine.Rendering.CompareFunction.LessEqual
        private const float LEqualDepthComparison = 4f;

        /// <summary>One and SrcAlpha both evaluate to 1 at alpha 1.</summary>
        private static bool IsUnitSourceFactorAtAlphaOne(float factor)
        {
            return factor == BlendFactorOne || factor == BlendFactorSrcAlpha;
        }

        /// <summary>Zero and OneMinusSrcAlpha both evaluate to 0 at alpha 1.</summary>
        private static bool IsZeroDestinationFactorAtAlphaOne(float factor)
        {
            return factor == BlendFactorZero ||
                   factor == BlendFactorOneMinusSrcAlpha;
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
        /// Whether all 25 canonical facts already match. The recipe values
        /// occupy the leading entries of <see cref="ConversionSchema"/> in
        /// order, so the comparison indexes them directly.
        /// <para>
        /// <c>_EnableOutlines</c> is deliberately excluded: it is not written,
        /// so it cannot make a material non-canonical.
        /// </para>
        /// </summary>
        private static bool IsCanonicalOpaque(
            IReadOnlyList<float> values,
            int effectiveRenderQueue,
            string effectiveRenderType)
        {
            for (var index = 0; index < CanonicalOpaqueTuple.Length; index++)
            {
                if (values[index] != CanonicalOpaqueTuple[index].Value)
                {
                    return false;
                }
            }

            return effectiveRenderQueue == CanonicalOpaqueRenderQueue &&
                   string.Equals(
                       effectiveRenderType,
                       CanonicalOpaqueRenderType,
                       StringComparison.Ordinal);
        }

        // --- Effective render state and canonical-fact comparison -----------

        /// <summary>
        /// The two canonical facts that are not shader properties. Neither is
        /// animation-reachable - Unity's material binding syntax is
        /// <c>material.&lt;PropertyName&gt;</c>, and no binding form addresses a
        /// material's render queue or an override tag - so neither belongs in
        /// the evidence request, whose job is to close the animation-relevant
        /// set. <c>renderQueue</c> already resolves an absent override to the
        /// shader's declared queue, so "an override exists" is an
        /// implementation detail this design does not model.
        /// </summary>
        internal static void ReadEffectiveRenderState(
            UnityEngine.Material material,
            out int renderQueue,
            out string renderType)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            renderQueue = material.renderQueue;
            renderType = material.GetTag(RenderTypeTagName, false);
        }

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

            foreach (var (property, value) in CanonicalOpaqueTuple)
            {
                if (!candidate.HasProperty(property) ||
                    candidate.GetFloat(property) != value)
                {
                    factName = property;
                    return true;
                }
            }

            ReadEffectiveRenderState(candidate, out var queue, out var renderType);
            if (queue != CanonicalOpaqueRenderQueue)
            {
                factName = nameof(UnityEngine.Material.renderQueue);
                return true;
            }

            if (!string.Equals(
                    renderType, CanonicalOpaqueRenderType, StringComparison.Ordinal))
            {
                factName = RenderTypeTagName;
                return true;
            }

            factName = null;
            return false;
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

            var clone = new UnityEngine.Material(source);
            foreach (var (property, value) in CanonicalOpaqueTuple)
            {
                clone.SetFloat(property, value);
            }

            clone.renderQueue = CanonicalOpaqueRenderQueue;
            clone.SetOverrideTag(RenderTypeTagName, CanonicalOpaqueRenderType);

            if (TryFindNonCanonicalFact(clone, out var fact))
            {
                UnityEngine.Object.DestroyImmediate(clone);
                throw new InvalidOperationException(
                    "Generated opaque material did not read back canonical '" +
                    fact + "'.");
            }

            if (clone.shader != source.shader)
            {
                UnityEngine.Object.DestroyImmediate(clone);
                throw new InvalidOperationException(
                    "Generated opaque material did not preserve the source shader.");
            }

            return clone;
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
            var schema = new string[CanonicalOpaqueTuple.Length + 1];
            for (var index = 0; index < CanonicalOpaqueTuple.Length; index++)
            {
                schema[index] = CanonicalOpaqueTuple[index].Property;
            }

            schema[CanonicalOpaqueTuple.Length] = EnableOutlinesProperty;
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
