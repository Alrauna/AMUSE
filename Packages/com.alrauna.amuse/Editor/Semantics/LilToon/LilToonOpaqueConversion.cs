using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Host;
using UnityEditor;
using UnityEngine;

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
    /// unreachable on the attested cutout shader: there are no outline or
    /// premultiply members (the no-outline cutout slice has no outline pass,
    /// and cutout is not premultiplied alpha), and no
    /// <c>AlreadyOpaque</c>-related state. There is also no member for a
    /// generated-material read-back disagreement: that is an invariant
    /// failure, not an unsupported material, and <c>PrepareCanonicalOpaqueClone</c>
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
    }

    internal readonly struct LilToonOpaqueConversionEligibility
    {
        internal LilToonOpaqueConversionOutcome Outcome { get; }
        internal LilToonOpaqueConversionRefusal Refusal { get; }

        private LilToonOpaqueConversionEligibility(
            LilToonOpaqueConversionOutcome outcome,
            LilToonOpaqueConversionRefusal refusal)
        {
            Outcome = outcome;
            Refusal = refusal;
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
                LilToonOpaqueConversionOutcome.Refused, refusal);
        }

        internal static LilToonOpaqueConversionEligibility Convertible()
        {
            return new LilToonOpaqueConversionEligibility(
                LilToonOpaqueConversionOutcome.Convertible,
                LilToonOpaqueConversionRefusal.None);
        }
    }

    /// <summary>
    /// The pinned lilToon opaque-conversion core: decides whether an attested
    /// cutout lilToon 2.3.4 material may be normalized to its canonical Opaque
    /// counterpart, and prepares a transient validated clone when it may.
    /// <para>
    /// Semantics describe output facts; conversion decides mutation. This
    /// class therefore models render state that the cutout alpha semantics
    /// deliberately do not, and
    /// <see cref="LilToonCutoutMaterialSemantics"/> is unchanged.
    /// </para>
    /// <para>
    /// The recipe below is a transcription of B1 §9: the eighteen scalar
    /// writes were measured 2026-08-30 from the installed jp.lilxyzw.liltoon
    /// 2.3.4 package with exact read-back (spec §9.1). lilToon regenerates its
    /// shader assets from per-project settings, so the measurements came from
    /// the installed package, not from an upstream checkout.
    /// <strong>Never re-derive this tuple from the upstream git tag.</strong>
    /// Changing the attestation pins (<see cref="LilToonSourceAttestation"/>,
    /// whose identity the recipe's values are measured against) requires
    /// re-measuring this tuple from the newly attested install before the
    /// pins are updated.
    /// </para>
    /// <para>
    /// This is one shader, one direction, one version. It is deliberately not
    /// a render-state framework, a pass model, a conversion interface, or a
    /// provider registry.
    /// </para>
    /// </summary>
    internal static class LilToonOpaqueConversion
    {
        // --- Canonical Opaque recipe (pinned lilToon 2.3.4, B1 §9) ----------

        /// <summary>
        /// The complete canonical Opaque recipe's eighteen scalar writes, in
        /// the order B1 §9 measured them. The recipe's other two actions are
        /// the render queue and the <c>RenderType</c> tag, which are not
        /// material properties and are pinned by the constants below.
        /// <para>
        /// Deliberately absent from the recipe: <c>_Cutoff</c>,
        /// <c>_Color</c>, and every alpha-mask/dither/dissolve property. The
        /// attested opaque target compiles <c>LIL_RENDER 0</c>, which excludes
        /// the alpha path at compile time. <c>_Cutoff</c> still enters
        /// conversion evidence as an eligibility-only scalar; the other
        /// properties constrain the alpha proof and are never written.
        /// </para>
        /// </summary>
        private static readonly (string Property, float Value)[] CanonicalOpaqueTuple =
        {
            ("_SrcBlend", 1f),
            ("_DstBlend", 0f),
            ("_AlphaToMask", 0f),
            ("_ZWrite", 1f),
            ("_ZTest", 4f),
            ("_OffsetFactor", 0f),
            ("_OffsetUnits", 0f),
            ("_ColorMask", 15f),
            ("_SrcBlendAlpha", 1f),
            ("_DstBlendAlpha", 10f),
            ("_BlendOp", 0f),
            ("_BlendOpAlpha", 0f),
            ("_SrcBlendFA", 1f),
            ("_DstBlendFA", 1f),
            ("_SrcBlendAlphaFA", 0f),
            ("_DstBlendAlphaFA", 1f),
            ("_BlendOpFA", 4f),
            ("_BlendOpAlphaFA", 4f),
        };

        internal static IReadOnlyList<(string Property, float Value)>
            CanonicalOpaqueProperties { get; } =
                new ReadOnlyCollection<(string, float)>(CanonicalOpaqueTuple);

        internal const int CanonicalOpaqueRenderQueue = 2000;
        internal const string RenderTypeTagName = "RenderType";
        internal const string CanonicalOpaqueRenderType = "Opaque";
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

        // --- Conversion evidence -------------------------------------------

        /// <summary>
        /// The one property outside the canonical recipe that conversion
        /// reads. The cutout shader clips with <c>clip(alpha - _Cutoff)</c>,
        /// so only a threshold that keeps alpha 1 alive authorizes a clone;
        /// the recipe never writes the property, so it is eligibility-read
        /// and never written.
        /// </summary>
        private const string CutoffProperty = "_Cutoff";

        /// <summary>
        /// The 19 properties conversion reads: the 18 recipe properties plus
        /// <see cref="CutoffProperty"/>. Derived from the tuple rather than
        /// retyped so the two cannot drift. Used both as the request's
        /// presence schema and as its scalar schema.
        /// </summary>
        private static readonly string[] ConversionSchema = BuildConversionSchema();

        internal static IReadOnlyCollection<string>
            ConversionRequiredSchemaProperties { get; } =
                new ReadOnlyCollection<string>(ConversionSchema);

        /// <summary>
        /// Conversion's own request. lilToon carries no optimizer locked-flag
        /// the conversion path reads (unlike the Poiyomi module's schema),
        /// so the scalar schema is exactly the conversion schema - the same
        /// array, not a copy - and there are no colors, vectors, or
        /// textures: the recipe writes no such properties, and everything
        /// else the cutout proof reads belongs to the cutout alpha request,
        /// not to conversion (spec §9.2).
        /// </summary>
        internal static MaterialEvidenceRequest ConversionEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: ConversionSchema,
                scalarProperties: ConversionSchema,
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties: Array.Empty<TexturePropertyEvidenceRequest>());

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
            var values = new float[ConversionSchema.Length];
            for (var index = 0; index < ConversionSchema.Length; index++)
            {
                if (!evidence.TryGetScalar(ConversionSchema[index], out values[index]))
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
            if (Read(values, "_ZTest") != LEqualDepthComparison)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDepthComparison);
            }

            // 6. Depth write. The canonical target writes depth; a cutout
            //    authored with ZWrite off expresses different visibility
            //    intent than the recipe preserves.
            if (Read(values, "_ZWrite") != DepthWriteOn)
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDepthWrite);
            }

            // 7. Color mask. The canonical target writes all channels; a
            //    masked material deliberately suppresses channel output the
            //    recipe would restore.
            if (Read(values, "_ColorMask") != ColorMaskAll)
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
            if (Read(values, "_BlendOp") != BlendOpAdd ||
                !IsUnitSourceFactorAtAlphaOne(Read(values, "_SrcBlend")) ||
                !IsZeroDestinationFactorAtAlphaOne(Read(values, "_DstBlend")))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedBlendEquation);
            }

            // 10. Base alpha blend, same degeneracy argument as gate 9.
            if (Read(values, "_BlendOpAlpha") != BlendOpAdd ||
                !IsUnitSourceFactorAtAlphaOne(
                    Read(values, "_SrcBlendAlpha")) ||
                !IsZeroDestinationFactorAtAlphaOne(
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
            if (!IsUnitSourceFactorAtAlphaOne(
                    Read(values, "_SrcBlendFA")) ||
                Read(values, "_DstBlendFA") != BlendFactorOne ||
                Read(values, "_BlendOpFA") != BlendOpMax ||
                Read(values, "_BlendOpAlphaFA") != BlendOpMax)
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

        // Unity blend enums: Zero=0, One=1, SrcAlpha=5, OneMinusSrcAlpha=10;
        // UnityEngine.Rendering.BlendOp: Add=0, Max=4.
        private const float BlendOpAdd = 0f;
        private const float BlendOpMax = 4f;
        private const float BlendFactorZero = 0f;
        private const float BlendFactorOne = 1f;
        private const float BlendFactorSrcAlpha = 5f;
        private const float BlendFactorOneMinusSrcAlpha = 10f;

        // UnityEngine.Rendering.CompareFunction.LessEqual
        private const float LEqualDepthComparison = 4f;

        // UnityEngine.Rendering.ColorWriteMask.All
        private const float ColorMaskAll = 15f;

        // UnityEngine.Rendering.DepthWrite.On
        private const float DepthWriteOn = 1f;

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
            Material material,
            out int renderQueue,
            out string renderType)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            renderQueue = material.renderQueue;
            renderType = material.GetTag(RenderTypeTagName, false);
        }

        /// <summary>
        /// Reports the first of the 20 canonical facts the candidate disagrees
        /// with, in a deterministic order: recipe order, then the render
        /// queue, then the <c>RenderType</c> tag. A property the material does
        /// not declare is reported by name; the caller decides whether that
        /// is a refusal or a defect.
        /// </summary>
        internal static bool TryFindNonCanonicalFact(
            Material candidate,
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
                factName = nameof(Material.renderQueue);
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
        /// Attests the target's capability, clones the source, swaps the
        /// clone to the caller-supplied attested opaque target shader,
        /// applies the complete canonical Opaque tuple, then re-reads and
        /// validates every canonical fact and the target identity.
        /// <para>
        /// The source material is never written: <c>new Material(source)</c>
        /// is the only relationship between them. Source avatar assets are
        /// evidence, not mutation targets.
        /// </para>
        /// <para>
        /// Nothing is saved. Persistence belongs to assignment, which is the
        /// consumer's job, so this method takes no asset saver and cannot
        /// persist anything by accident. The clone is also left unnamed:
        /// naming generated assets is a consumer obligation, because container
        /// sub-asset names come from the object's own name and NDMF guarantees
        /// no determinism.
        /// </para>
        /// <para>
        /// Its precondition is an attested and eligible source plus the
        /// attested opaque target resolved by the caller (the production
        /// wrapper below resolves it from the pinned environment; tests and
        /// seams supply a stand-in reference).
        /// </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The attested target does not declare a recipe property, a written
        /// canonical fact did not read back, or the clone did not take the
        /// attested target shader. The property check runs before
        /// <c>new Material(source)</c>, so that throw leaves no clone to
        /// destroy; the other two throw after
        /// <see cref="UnityEngine.Object.DestroyImmediate"/> has destroyed
        /// the clone, so no material leaks. Eligibility has already proven
        /// every property present on the SOURCE and every input finite, and
        /// this method writes exact canonical constants, so a read-back
        /// disagreement falsifies the assumption that AMUSE can write this
        /// material's render state; a target that cannot declare the recipe,
        /// or a target mismatch, would silently convert the material onto a
        /// wrong or partially-capable shader. All three are compatibility or
        /// programming failures, not unsupported materials, and converting
        /// either into a conservative refusal would hide a broken write path
        /// behind a plausible-looking "preserved the input" outcome.
        /// </exception>
        /// <remarks>
        /// The target-identity check is the family-specific replacement for
        /// the Poiyomi module's shader-preservation check (spec R5): the
        /// Poiyomi conversion clones in place on the same shader, while this
        /// conversion's whole point is moving a cutout material onto the
        /// attested opaque shader, so the validated fact is that the clone
        /// <em>carries the target</em>, not that it preserved the source.
        /// </remarks>
        internal static Material PrepareCanonicalOpaqueClone(
            Material source,
            Shader attestedTarget)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (attestedTarget == null)
            {
                throw new ArgumentNullException(nameof(attestedTarget));
            }

            // Attest the target's capability at the SHADER level, before any
            // clone exists. Unity's observed behavior: SetFloat on a property
            // the shader does not declare still stores the value in the
            // material's saved properties, and HasProperty/GetFloat then read
            // it back - so a material-level read-back after the writes cannot
            // detect a target shader missing a recipe property; it would
            // faithfully echo whatever the recipe wrote. The read-back below
            // therefore validates the writes, and only this shader-level
            // check validates the writer.
            foreach (var (property, _) in CanonicalOpaqueTuple)
            {
                if (attestedTarget.FindPropertyIndex(property) < 0)
                {
                    throw new InvalidOperationException(
                        "The attested opaque target shader does not declare '" +
                        property + "'.");
                }
            }

            var clone = new Material(source);
            clone.name = string.Empty;
            clone.shader = attestedTarget;
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

            if (clone.shader != attestedTarget)
            {
                UnityEngine.Object.DestroyImmediate(clone);
                throw new InvalidOperationException(
                    "Generated opaque material did not take the attested " +
                    "opaque target shader.");
            }

            return clone;
        }

        /// <summary>
        /// Resolves and fully attests the opaque lilToon target from the live
        /// environment before delegating to the explicit-target writer.
        /// Name and GUID alone are insufficient: the recipe is measured
        /// against the pinned shader, pass, include tree, package version,
        /// and <c>LIL_RENDER 0</c> source profile.
        /// <para>
        /// An attestation failure is an environment invariant failure, not an
        /// unsupported-material refusal, and occurs before any clone exists.
        /// </para>
        /// </summary>
        internal static Material PrepareCanonicalOpaqueClone(
            Material source,
            CapturedMaterialEvidence evidence)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));

            var target = Shader.Find(LilToonSourceAttestation.SupportedShaderName);
            if (target == null)
            {
                throw new InvalidOperationException(
                    "The attested lilToon environment regressed: opaque target '" +
                    LilToonSourceAttestation.SupportedShaderName +
                    "' did not resolve.");
            }

            var targetEvidence =
                LilToonSourceAttestation.GatherOpaqueTargetSourceEvidence(
                    target, evidence);
            if (!LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    targetEvidence, out var diagnostic))
            {
                throw new InvalidOperationException(
                    "The attested lilToon opaque target failed source " +
                    "attestation: " + diagnostic?.Detail);
            }

            return PrepareCanonicalOpaqueClone(source, target);
        }

        private static string[] BuildConversionSchema()
        {
            var schema = new string[CanonicalOpaqueTuple.Length + 1];
            for (var index = 0; index < CanonicalOpaqueTuple.Length; index++)
            {
                schema[index] = CanonicalOpaqueTuple[index].Property;
            }

            schema[CanonicalOpaqueTuple.Length] = CutoffProperty;
            return schema;
        }
    }
}
