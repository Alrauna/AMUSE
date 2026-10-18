using System;
using System.Linq;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Analysis
{

    /// <summary>
    /// One slot's admitted-state resolution: either one
    /// <see cref="AlphaResolution"/> per admitted material, or a named refusal
    /// and no resolution at all.
    /// <para>
    /// A refusal exposes no partial prefix. Admission is a proof about the whole
    /// slot, so resolutions produced for admitted materials examined before the
    /// refusing one are not authorization for anything and are discarded.
    /// </para>
    /// </summary>
    internal sealed class SlotResolutionResult
    {
        private SlotResolutionResult(
            RendererAnalysisRefusal refusal,
            IReadOnlyList<AlphaResolution> resolutions,
            IReadOnlyList<AlphaResolution> materialResolutions,
            CapturedAlphaMaterial offender,
            AlphaUnknownReason unknownReason,
            MaterialDependencyClosureFailure closureWay)
        {
            IsResolved = refusal == RendererAnalysisRefusal.None;
            Refusal = refusal;
            Resolutions = resolutions;
            MaterialResolutions = materialResolutions;
            Offender = offender;
            UnknownReason = unknownReason;
            ClosureWay = closureWay;
        }

        internal bool IsResolved { get; }
        internal RendererAnalysisRefusal Refusal { get; }
        internal IReadOnlyList<AlphaResolution> Resolutions { get; }

        /// <summary>
        /// The raw pre-dedup per-material resolutions, parallel to the
        /// slot's admitted material indices by construction. The deduped
        /// <see cref="Resolutions"/> list keeps feeding classification and
        /// the anisotropy scan; this list exists so reporting can attribute
        /// a refused resolution to its own admitted material. A refused
        /// slot holds none.
        /// </summary>
        internal IReadOnlyList<AlphaResolution> MaterialResolutions { get; }

        /// <summary>
        /// Reports whether any admitted material's resolution samples its
        /// texture anisotropically. A refused slot holds no resolutions and
        /// answers false.
        /// </summary>
        internal bool HasAnisotropicSampling
        {
            get
            {
                if (Resolutions == null || Resolutions.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < Resolutions.Count; i++)
                {
                    if (Resolutions[i].HasAnisotropicSampling)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// The admitted material whose own fact refused the slot, or null
        /// when the refusal names no single material. Report evidence only:
        /// the classification outcome is the refusal itself, never the
        /// offender.
        /// </summary>
        internal CapturedAlphaMaterial Offender { get; }

        /// <summary>
        /// The frontend's reason the alpha answer is Unknown, or null when
        /// the refusal has no frontend reason. Report evidence only.
        /// </summary>
        internal AlphaUnknownReason UnknownReason { get; }

        /// <summary>
        /// The way a closure-failure refusal failed, or
        /// <see cref="MaterialDependencyClosureFailure.None"/> when the
        /// refusal is not a closure failure. Report evidence only: the
        /// slot's outcome is the refusal, never the way.
        /// </summary>
        internal MaterialDependencyClosureFailure ClosureWay { get; }

        internal static SlotResolutionResult Refused(
            RendererAnalysisRefusal refusal,
            CapturedAlphaMaterial offender = null,
            AlphaUnknownReason unknownReason = null,
            MaterialDependencyClosureFailure closureWay =
                MaterialDependencyClosureFailure.None)
        {
            if (refusal == RendererAnalysisRefusal.None)
            {
                throw new ArgumentException(
                    "A refused slot must name its refusal.", nameof(refusal));
            }

            return new SlotResolutionResult(
                refusal, Array.Empty<AlphaResolution>(),
                Array.Empty<AlphaResolution>(), offender, unknownReason,
                closureWay);
        }

        internal static SlotResolutionResult Resolved(
            IReadOnlyList<AlphaResolution> resolutions)
        {
            // A caller that resolves exactly the slot's admitted materials
            // in order holds the raw list, which is parallel to the slot's
            // admitted material indices. One list serves both roles.
            return Resolved(resolutions, resolutions);
        }

        internal static SlotResolutionResult Resolved(
            IReadOnlyList<AlphaResolution> resolutions,
            IReadOnlyList<AlphaResolution> materialResolutions)
        {
            if (resolutions == null)
                throw new ArgumentNullException(nameof(resolutions));
            if (materialResolutions == null)
                throw new ArgumentNullException(nameof(materialResolutions));

            var copy = new AlphaResolution[resolutions.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = resolutions[index];
            }

            var materialCopy =
                new AlphaResolution[materialResolutions.Count];
            for (var index = 0; index < materialCopy.Length; index++)
            {
                materialCopy[index] = materialResolutions[index];
            }

            return new SlotResolutionResult(
                RendererAnalysisRefusal.None, Array.AsReadOnly(copy),
                Array.AsReadOnly(materialCopy), null, null,
                MaterialDependencyClosureFailure.None);
        }
    }

    internal static class AdmittedMaterialStates
    {
        /// <summary>
        /// Drops resolutions that are provably interchangeable, preserving the
        /// order of first occurrence.
        /// <para>
        /// Deduplication here is <em>performance-only</em>, and its correctness
        /// theorem is one-way. Leaving two interchangeable resolutions separate
        /// costs a redundant classification pass and can never change a proof.
        /// Merging two that could classify any triangle differently hands the
        /// later intersection too few states, which is a false-positive-
        /// direction defect. Every uncertain pair therefore stays separate.
        /// </para>
        /// <para>
        /// Exactly two merges are permitted, both on exact stored values:
        /// two refusals naming the same <see cref="AlphaResolutionFailure"/>,
        /// and two uniform resolutions carrying the same
        /// <see cref="TriangleAlphaOutcome"/>. Nothing merges across those two
        /// cases.
        /// </para>
        /// <para>
        /// <strong>Classified resolutions never merge — not even with
        /// themselves.</strong> Two of them are interchangeable only if their
        /// alpha fields are semantically equivalent, and reference-distinct
        /// <see cref="AlphaMipChain"/> cannot be proven equivalent cheaply.
        /// Recognizing the same instance twice would be sound in isolation but
        /// is deliberately not done either, because it invites the field-,
        /// fingerprint-, and reference-equality variants that are not. Keeping
        /// an obvious duplicate costs one extra pass and keeps the rule
        /// categorical, which is the safe direction.
        /// </para>
        /// <para>
        /// Equivalence is never inferred by classifying a sample of triangles.
        /// No finite sample proves two resolutions agree on every triangle, and
        /// a sample that happened to agree would merge a varying resolution
        /// into a constant one.
        /// </para>
        /// <para>
        /// An empty input yields an empty result. A renderer slot with no
        /// resolutions is a programming defect rather than vacuous proof of
        /// opacity, but it is the intersection step that must say so;
        /// manufacturing a resolution here — opaque, unknown, or refused —
        /// would disarm that guard before it ever ran.
        /// </para>
        /// </summary>
        internal static IReadOnlyList<AlphaResolution> DistinctResolutions(
            IReadOnlyList<AlphaResolution> resolutions)
        {
            if (resolutions == null)
                throw new ArgumentNullException(nameof(resolutions));

            // A plain stable scan over one slot's admitted resolutions. It is
            // quadratic in the worst case and that is deliberate: it makes the
            // exact equivalence obvious and needs no key, hash, or comparer that
            // could quietly widen it.
            var distinct = new List<AlphaResolution>(resolutions.Count);
            foreach (var resolution in resolutions)
            {
                var alreadyRepresented = false;
                foreach (var kept in distinct)
                {
                    if (AreExactlyInterchangeable(kept, resolution))
                    {
                        alreadyRepresented = true;
                        break;
                    }
                }

                // The first occurrence stays the representative; a later
                // duplicate never replaces or reorders it.
                if (!alreadyRepresented)
                    distinct.Add(resolution);
            }

            // Read-only for the same reason SlotResolutionResult.Resolved is:
            // a proof set a caller could downcast and mutate is not a proof.
            return Array.AsReadOnly(distinct.ToArray());
        }

        /// <summary>
        /// The whole V1 equivalence relation, on exact stored values only.
        /// Anything this does not recognize stays distinct.
        /// </summary>
        private static bool AreExactlyInterchangeable(
            AlphaResolution left, AlphaResolution right)
        {
            if (!left.IsResolved || !right.IsResolved)
            {
                // Both refused and naming the same failure, or not comparable
                // at all. A refusal never merges with a resolved value.
                return !left.IsResolved && !right.IsResolved &&
                       left.Failure == right.Failure;
            }

            // Both resolved. Only the uniform case is decidable: a classified
            // resolution answers false here and so merges with nothing.
            return left.TryGetUniformOutcome(out var leftOutcome) &&
                   right.TryGetUniformOutcome(out var rightOutcome) &&
                   leftOutcome == rightOutcome;
        }

        /// <summary>
        /// Resolves one renderer material slot: every material the slot may
        /// hold, each against its <em>own</em> captured serialized defaults.
        /// <para>
        /// A renderer-wide default would be wrong the moment two admitted
        /// materials disagree, and that error is in the false-positive
        /// direction. The same animated binding may therefore be admitted
        /// against one admitted material and refused against another.
        /// </para>
        /// <para>
        /// This is one slot. It forms no Cartesian product, deduplicates
        /// nothing, classifies no triangle, and does not consume the renderer's
        /// structural facts; those are renderer-wide and belong to the
        /// integration path.
        /// </para>
        /// </summary>
        /// <param name="relevance">
        /// MUST be the same decision-specific relevance request the supplied
        /// bindings were resolved against — for ordinary alpha proof, that is
        /// <c>CapturedAnimationEvidence.AlphaRelevanceRequest</c>, never the
        /// broader schema the one capture gathered. It is the only source that
        /// can invert a derived <c>&lt;texture&gt;_ST</c> name back to its
        /// owning texture request, and a different request would either fail
        /// that inversion outright or resolve the name against a schema the
        /// bindings were never judged against.
        /// </param>
        internal static SlotResolutionResult ResolveSlot(
            CapturedMaterialSlotEvidence slot,
            IReadOnlyList<CapturedAlphaMaterial> admittedMaterials,
            IReadOnlyList<(CapturedFloatBinding Binding,
                           AnimatedPropertyRef Reference)> slotBindings,
            MaterialEvidenceRequest relevance,
            AlphaFieldSet alphaFields,
            AlphaSamplingPolicy samplingPolicy,
            int maxNoiseTexelPercent,
            string scaleOffsetSuffix,
            CapturedAlphaMaterialSemanticsResolver resolveSemantics = null,
            IReadOnlyList<BlockStateEntry> slotBlockEntries = null)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            if (admittedMaterials == null)
                throw new ArgumentNullException(nameof(admittedMaterials));
            if (slotBindings == null)
                throw new ArgumentNullException(nameof(slotBindings));
            if (relevance == null)
                throw new ArgumentNullException(nameof(relevance));
            if (alphaFields == null)
                throw new ArgumentNullException(nameof(alphaFields));

            resolveSemantics ??= UnityMaterialSemantics.AnalyzeAlphaMaterial;

            var resolutions = new List<AlphaResolution>(
                slot.AdmittedMaterialIndices.Count);
            foreach (var index in slot.AdmittedMaterialIndices)
            {
                var material = admittedMaterials[index];

                // Each admitted material accumulates its own derivations from
                if (!TryAdmitDerivedEvidence(
                        material, slotBindings, relevance,
                        out var evidence, out var refusal,
                        scaleOffsetSuffix,
                        slotBlockEntries,
                        admittedMaterials))
                {
                    // No partial prefix: resolutions gathered for earlier
                    // admitted materials authorize nothing once the slot
                    // is refused.
                    return SlotResolutionResult.Refused(
                        refusal, material, null);
                }

                var admitted = ReferenceEquals(evidence, material.Evidence)
                    ? material
                    : new CapturedAlphaMaterial(
                        material.Family,
                        evidence,
                        material.PoiyomiEvidence,
                        material.LilToonEvidence,
                        material.LockedIdentityRefusal,
                        material.MaterialPath,
                        material.MaterialName,
                        material.ShaderName,
                        material.MultiResolution,
                        material.PredicatesDeclareCutoff);
                var capturedSemantics = resolveSemantics(admitted)
                    ?? CapturedAlphaSemantics.AllUnknown();
                // Field lookups are scoped to this admitted material's own
                // captured predicates, so a shared texture captured under a
                // sibling slot's shader cutoff answers nothing here. The
                // derived evidence carries the same texture assignments as
                // the capture: admission substitutes scalars only. The
                // family's own alpha request decides which assignments the
                // material actually samples.
                var resolution =
                    ResolveAdmittedMaterialSemantics(
                        admitted, capturedSemantics, alphaFields,
                        samplingPolicy, maxNoiseTexelPercent);
                if (resolution.Failure == AlphaResolutionFailure.SemanticsUnknown)
                {
                    // A recognized locked material refuses by its own name
                    // instead of the generic destination. Every earlier
                    // refusal path returned above and keeps its precedence.
                    // A locked serialization that records no original
                    // shader carries None and keeps the 2026-09-19
                    // destination.
                    var slotRefusal =
                        material.LockedIdentityRefusal !=
                        RendererAnalysisRefusal.None
                            ? material.LockedIdentityRefusal
                            : capturedSemantics.AlphaUnknownReason != null &&
                              capturedSemantics.AlphaUnknownReason.Kind ==
                                  AlphaUnknownKind.FeatureRetention
                                ? RendererAnalysisRefusal
                                    .AdmittedMaterialRetainedByFeature
                                : RendererAnalysisRefusal
                                    .AdmittedMaterialSemanticsUnknown;
                    return SlotResolutionResult.Refused(
                        slotRefusal,
                        material,
                        capturedSemantics.AlphaUnknownReason);
                }

                resolutions.Add(resolution);
            }

            return SlotResolutionResult.Resolved(resolutions);
        }

        /// <summary>
        /// One per-material resolution rule: the family's own alpha request scopes
        /// the field lookup to the material's own evidence, and the resolver runs
        /// at the caller's noise bound under the caller's named
        /// <see cref="AlphaSamplingPolicy"/>. The admitted-slot loop and the Host probe
        /// both call this, so field scoping cannot drift between the proof path
        /// and the census calibration path.
        /// </summary>
        internal static AlphaResolution ResolveAdmittedMaterialSemantics(
            CapturedAlphaMaterial material,
            CapturedAlphaSemantics capturedSemantics,
            AlphaFieldSet alphaFields,
            AlphaSamplingPolicy samplingPolicy,
            int maxNoiseTexelPercent)
        {
            var predicateRequest =
                UnityMaterialSemantics.AlphaRequestForFamily(
                    material.Family);
            AlphaFieldProvider materialFields =
                (TextureSourceId source,
                    TextureChannel channel,
                    out AlphaMipChain chain) =>
                {
                    chain = null;
                    return alphaFields.TryGetFor(
                        material.Evidence,
                        predicateRequest,
                        source,
                        channel,
                        out chain);
                };
            return AlphaSemanticsResolver.Resolve(
                capturedSemantics.Semantics.Alpha,
                materialFields,
                samplingPolicy,
                maxNoiseTexelPercent);
        }

        /// <summary>
        /// Admits every proof-relevant binding against <em>this</em> material's
        /// own captured defaults and hands back the evidence those admissions
        /// derive. This is exactly the per-material half of
        /// <see cref="ResolveSlot"/>, extracted so one implementation serves
        /// every decision-specific relevance set rather than being duplicated
        /// per decision.
        /// <para>
        /// <paramref name="derived"/> is reference-equal to the material's own
        /// evidence when no group changed anything, so a caller can still
        /// detect "nothing was substituted" by reference.
        /// </para>
        /// </summary>
        /// <param name="relevance">
        /// MUST be the same decision-specific relevance request the supplied
        /// bindings were resolved against, for the reason
        /// <see cref="ResolveSlot"/> documents.
        /// </param>
        internal static bool TryAdmitDerivedEvidence(
            CapturedAlphaMaterial material,
            IReadOnlyList<(CapturedFloatBinding Binding,
                           AnimatedPropertyRef Reference)> bindings,
            MaterialEvidenceRequest relevance,
            out CapturedMaterialEvidence derived,
            out RendererAnalysisRefusal refusal,
            string scaleOffsetSuffix,
            IReadOnlyList<BlockStateEntry> slotBlockEntries = null,
            IReadOnlyList<CapturedAlphaMaterial> allAdmittedMaterials = null)
        {
            return AnimatedPropertyAdmission.TryAdmitDerivedEvidence(
                material,
                bindings,
                relevance,
                out derived,
                out refusal,
                scaleOffsetSuffix,
                slotBlockEntries,
                allAdmittedMaterials);
        }

        internal static AdmittedPropertyOutcome AdmitScalar(
            IReadOnlyList<CapturedFloatBinding> bindings,
            float serializedDefault,
            out float admittedValue)
        {
            return AnimatedPropertyAdmission.AdmitScalar(
                bindings, serializedDefault, out admittedValue);
        }

        internal static AdmittedPropertyOutcome AdmitColor(
            IReadOnlyDictionary<
                int, IReadOnlyList<CapturedFloatBinding>> componentBindings,
            Color serializedDefault,
            out Color admittedValue)
        {
            return AnimatedPropertyAdmission.AdmitColor(
                componentBindings, serializedDefault, out admittedValue);
        }

        internal static AdmittedPropertyOutcome AdmitVector(
            IReadOnlyDictionary<
                int, IReadOnlyList<CapturedFloatBinding>> componentBindings,
            Vector4 serializedDefault,
            out Vector4 admittedValue)
        {
            return AnimatedPropertyAdmission.AdmitVector(
                componentBindings, serializedDefault, out admittedValue);
        }

    }
}
