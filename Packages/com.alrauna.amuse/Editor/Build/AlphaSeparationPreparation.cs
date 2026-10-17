using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Substitutes only one family's shader-opaque-conversion step for one
    /// admitted material — attestation, eligibility and clone preparation —
    /// taking the depth-test policy the avatar chose and returning either a
    /// mapped opaque <see cref="Material"/> or a conversion refusal, plus
    /// whether eligibility admitted the source through that policy. They
    /// exist so public-package fixtures whose stand-in shaders deliberately
    /// fail source attestation can still drive the feature: the internal
    /// barrier overload takes them as optional final parameters, and
    /// production passes nothing and runs the real
    /// <see cref="PoiyomiOpaqueConversion"/> and
    /// <see cref="LilToonOpaqueTarget"/> paths.
    /// <para>
    /// The policy input and the divergence and normalization outputs make
    /// the seam boundary carry the same facts the real route reads off its
    /// eligibility result, so the per-slot disclosures see one contract on
    /// both routes.
    /// </para>
    /// <para>
    /// Delegates on an existing overload, not an interface, registry,
    /// adapter hierarchy, result framework, or a test fixture framework.
    /// </para>
    /// </summary>
    internal delegate bool VerifiedPoiyomiConversion(
        Material live,
        CapturedMaterialEvidence derived,
        bool allowDepthTestChange,
        Material preparedOpaque,
        out Material opaque,
        out PoiyomiOpaqueConversionRefusal refusal,
        out bool depthTestDivergence,
        out bool premultiplyNormalization);

    /// <summary>
    /// The verified-fixture seam for the lilToon conversion families
    /// (cutout and transparent) — the exact shape of
    /// <see cref="VerifiedPoiyomiConversion"/> with the
    /// lilToon refusal vocabulary.
    /// </summary>
    internal delegate bool VerifiedLilToonConversion(
        Material live,
        CapturedMaterialEvidence derived,
        bool allowDepthTestChange,
        Material preparedOpaque,
        out Material opaque,
        out LilToonOpaqueConversionRefusal refusal,
        out bool depthTestDivergence);

    /// <summary>
    /// Barrier-side alpha-separation preparation: conversion-relevance
    /// resolution, per-slot conversion admission, the shader-family branch,
    /// the opaque mapping, material clone creation and naming, and
    /// mesh clone creation. It runs inside the extension-free semantic barrier
    /// and mutates nothing but AMUSE-owned transient objects — no renderer, no
    /// clip, no source asset, and no asset saving.
    /// <para>
    /// The shader-family branch lives here and nowhere else: Poiyomi and
    /// the two lilToon alpha frontends (cutout and transparent) convert,
    /// the opaque lilToon frontend is already the canonical opaque answer
    /// and maps to itself, and every other family is refused with
    /// <see cref="AlphaSeparationSlotRefusal.OpaqueConversionUnsupportedFamily"/>.
    /// Adding a conversion family later means adding one case here and
    /// one entry in each family map, and nothing else in the feature.
    /// </para>
    /// </summary>
    internal static class AlphaSeparationPreparation
    {
        /// <summary>
        /// Prepares one renderer's alpha separation for the later apply pass.
        /// Returns the retained record, or null when no candidate slot
        /// survived preparation — in which case nothing was prepared and
        /// nothing is retained for the renderer.
        /// <para>
        /// Every admitted material of a candidate slot must map before that
        /// slot is prepared at all; a slot with any unmapped admitted value is
        /// dropped here and never reaches validation. Mappings are registered
        /// avatar-wide only after a slot's full admitted set has mapped, so a
        /// refused slot leaves no prepared state behind, and clones created
        /// for its earlier admitted materials are destroyed immediately —
        /// they are AMUSE-owned transients from this same call.
        /// </para>
        /// <para>
        /// The two percent vectors carry one pair per slot ordinal, selected
        /// from the slot's own recorded proof arm. Both gates read the slot's
        /// own pair, so a mixed renderer never folds or refuses one slot by
        /// another arm's value. The membership vector records which arm each
        /// slot selected and is kept for the fold cause the report pass
        /// names. All three vectors cover the largest binding index plus
        /// one.
        /// </para>
        /// </summary>
        internal static PreparedRendererSeparation Prepare(
            AmusePlatformFinishState state,
            UnityRendererMutationTarget target,
            string rendererPath,
            MeshSeparationPlan plan,
            CapturedAnimationEvidence evidence,
            IReadOnlyList<Material> admittedLiveMaterials,
            VerifiedPoiyomiConversion poiyomiConversion,
            VerifiedLilToonConversion lilToonConversion,
            int minimumOpaqueCoveragePercent,
            int minimumOpaqueCoverageToPreservePercent = 0,
            IReadOnlyList<int> coveragePercentsBySlot = null,
            IReadOnlyList<int> preservePercentsBySlot = null,
            IReadOnlyList<bool> cutoutMembershipBySlot = null,
            string rendererTypeName = null,
            bool allowDepthTestChange = false,
            IReadOnlyCollection<string> grantedShaderNames = null,
            IReadOnlyList<SlotResolutionResult> slotResults = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (rendererPath == null)
                throw new ArgumentNullException(nameof(rendererPath));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            if (admittedLiveMaterials == null)
                throw new ArgumentNullException(nameof(admittedLiveMaterials));
            if (coveragePercentsBySlot == null)
            {
                throw new ArgumentNullException(nameof(coveragePercentsBySlot));
            }

            if (preservePercentsBySlot == null)
            {
                throw new ArgumentNullException(nameof(preservePercentsBySlot));
            }

            if (cutoutMembershipBySlot == null)
            {
                throw new ArgumentNullException(
                    nameof(cutoutMembershipBySlot));
            }

            // The preserve gate folds whole material slots whose every
            // submesh holds a strictly smaller unproven share than that
            // slot's own stored preserve value. A folded entry is an
            // ordinary wholly-opaque candidate for every later consumer
            // here, so no candidate-loop gate changes. The unproven count
            // per folded submesh feeds the forced report event of the
            // later report pass. A preserve value of 0 qualifies nothing,
            // so the original plan object flows through unchanged.
            var unprovenCountByFoldedSubmesh = new Dictionary<int, int>();
            var entriesByBindingIndex =
                new Dictionary<int, List<SubmeshSeparationPlan>>();
            foreach (var entry in plan.Submeshes)
            {
                if (!entriesByBindingIndex.TryGetValue(
                        entry.SourceMaterialBindingIndex,
                        out var slotEntries))
                {
                    slotEntries = new List<SubmeshSeparationPlan>();
                    entriesByBindingIndex.Add(
                        entry.SourceMaterialBindingIndex, slotEntries);
                }

                slotEntries.Add(entry);
            }

            var foldedEntries = new List<SubmeshSeparationPlan>();
            foreach (var slotGroup in entriesByBindingIndex)
            {
                if (!SlotTransformDecision.PreserveSlotQualifies(
                        slotGroup.Value,
                        preservePercentsBySlot[slotGroup.Key]))
                {
                    continue;
                }

                foreach (var entry in slotGroup.Value)
                {
                    var folded = MeshSeparationPlanner.Folded(entry);
                    unprovenCountByFoldedSubmesh.Add(
                        folded.SourceSubmeshIndex,
                        entry.TransparentTriangleOrdinals.Count);
                    foldedEntries.Add(folded);
                }
            }

            if (foldedEntries.Count > 0)
            {
                plan = MeshSeparationPlanner.WithFoldedSubmeshes(
                    plan, foldedEntries);
            }

            // An unproven slot whose own resolution samples anisotropically
            // refuses before the opaque-candidate gate: a renderer whose
            // every slot stayed unproven retains nothing, so the candidate
            // loop below never runs and the cause would go unreported. The
            // set keeps one refusal per slot, because several submeshes can
            // bind one material slot.
            var reportedAnisotropySlots = new HashSet<int>();
            if (slotResults != null)
            {
                foreach (var submesh in plan.Submeshes)
                {
                    if (submesh.Disposition ==
                        SubmeshSeparationDisposition.Unchanged)
                    {
                        var slotIndex = submesh.SourceMaterialBindingIndex;
                        if (slotIndex >= 0 &&
                            slotIndex < slotResults.Count &&
                            !reportedAnisotropySlots.Contains(slotIndex))
                        {
                            var slotResult = slotResults[slotIndex];
                            if (slotResult.IsResolved &&
                                slotResult.HasAnisotropicSampling &&
                                HasAnyUnknownOutcome(
                                    plan, submesh.SourceSubmeshIndex))
                            {
                                reportedAnisotropySlots.Add(slotIndex);
                                AmuseReports.RecordRefusal(
                                    state,
                                    target.Renderer,
                                    slotIndex,
                                    AlphaSeparationSlotRefusal
                                        .AnisotropyDifferences);
                            }
                        }
                    }
                }
            }

            if (!plan.HasAnyOpaqueCandidates)
            {
                return null;
            }

            // Conversion relevance is re-resolved only for renderers that
            // produced opaque candidates AND carry a conversion-capable
            // family, and only against the union of those families' own
            // conversion requests: the alpha resolution that ran above is
            // bit-for-bit untouched, and the same captured bindings are
            // judged a second time under a different relevance. A renderer
            // with no conversion-capable family has no conversion that
            // could read animated state, so the whole relevance pass is
            // skipped rather than run against a foreign family's request.
            var conversionCapableFamilies = new List<
                CapturedAlphaMaterialFamily>();
            foreach (var admitted in evidence.AdmittedMaterials)
            {
                if (ConversionRequestForFamily(admitted.Family) != null &&
                    !conversionCapableFamilies.Contains(admitted.Family))
                {
                    conversionCapableFamilies.Add(admitted.Family);
                }
            }

            // Per-family buckets of the renderer-wide conversion bindings:
            // each recognized binding is re-resolved against every capable
            // family's own request, because relevance is a per-family fact
            // and the union only decides recognition and the renderer-wide
            // refusals below.
            var conversionBuckets = new Dictionary<
                CapturedAlphaMaterialFamily,
                List<(CapturedFloatBinding Binding,
                      AnimatedPropertyRef Reference)>>();
            var conversionPropertyNamesByFamily = new Dictionary<
                CapturedAlphaMaterialFamily,
                HashSet<string>>();
            if (conversionCapableFamilies.Count > 0)
            {
                var unionRequest = MaterialEvidenceRequest.Combine(
                    conversionCapableFamilies
                        .Select(ConversionRequestForFamily)
                        .ToArray());
                var conversionBindings = new List<(
                    CapturedFloatBinding Binding,
                    AnimatedPropertyRef Reference)>();
                var anyConversionBindingFromAdditiveLayer = false;
                foreach (var clipEvidence in evidence.Clips)
                {
                    foreach (var binding in clipEvidence.FloatBindings)
                    {
                        var resolution =
                            UnityAnimationEvidenceCapture
                                .ResolveProofRelevant(
                                    binding,
                                    rendererPath,
                                    unionRequest,
                                    out var reference,
                                    rendererTypeName);
                        if (resolution ==
                            ProofRelevantBindingResolution
                                .UnrecognizedMaterialBinding)
                        {
                            return RefuseEveryCandidateSlot(
                                target,
                                state,
                                plan,
                                AlphaSeparationSlotRefusal
                                    .ConversionBindingUnrecognized);
                        }

                        if (resolution ==
                            ProofRelevantBindingResolution.RendererWide)
                        {
                            anyConversionBindingFromAdditiveLayer |=
                                clipEvidence.IsFromAdditiveLayer;
                            conversionBindings.Add((binding, reference));
                        }
                    }
                }

                // Conversion-relevant animated material state is
                // renderer-wide by binding scope — a material.<Property>
                // curve addresses no slot — so these two conditions
                // invalidate every candidate slot of this renderer and
                // nothing else: never the renderer's alpha analysis, never
                // another renderer.
                if (anyConversionBindingFromAdditiveLayer)
                {
                    return RefuseEveryCandidateSlot(
                        target,
                        state,
                        plan,
                        AlphaSeparationSlotRefusal
                            .ConversionStateUnderAdditiveLayer);
                }

                if (conversionBindings.Count > 0 &&
                    evidence.HasUnnormalizedDirectBlendTree)
                {
                    return RefuseEveryCandidateSlot(
                        target,
                        state,
                        plan,
                        AlphaSeparationSlotRefusal
                            .ConversionStateUnderUnnormalizedDirectBlendTree);
                }

                foreach (var family in conversionCapableFamilies)
                {
                    var bucket = new List<(
                        CapturedFloatBinding Binding,
                        AnimatedPropertyRef Reference)>();
                    var names = new HashSet<string>();
                    foreach (var (binding, _) in conversionBindings)
                    {
                        if (UnityAnimationEvidenceCapture
                                .ResolveProofRelevant(
                                    binding,
                                    rendererPath,
                                    ConversionRequestForFamily(family),
                                    out var familyReference,
                                    rendererTypeName) ==
                            ProofRelevantBindingResolution.RendererWide)
                        {
                            bucket.Add((binding, familyReference));
                            names.Add(familyReference.PropertyName);
                        }
                    }

                    conversionBuckets.Add(family, bucket);
                    conversionPropertyNamesByFamily.Add(family, names);
                }
            }

            var slots = RuntimeStateResolution.MaterialSlotsFor(
                evidence, rendererPath,
                UnityAnimationEvidenceCapture.MaterialSlotBindingParser,
                rendererTypeName);
            var candidateSlots = new List<PreparedSlotSeparation>(
                plan.Submeshes.Count);

            foreach (var submesh in plan.Submeshes)
            {
                if (submesh.Disposition ==
                    SubmeshSeparationDisposition.Unchanged)
                {
                    // An unchanged slot holds no candidate and carries no
                    // refusal of its own. The pass above already reported
                    // the anisotropy-caused ones, and the set keeps a slot
                    // shared with a split submesh from reporting twice.
                    continue;
                }

                if (submesh.Disposition ==
                        SubmeshSeparationDisposition.Split &&
                    SlotTransformDecision.OpaqueCoverageBelow(
                        coveragePercentsBySlot[
                            submesh.SourceMaterialBindingIndex],
                        submesh))
                {
                    var coverageSlotIndex =
                        submesh.SourceMaterialBindingIndex;
                    var anisotropicCause =
                        slotResults != null &&
                        coverageSlotIndex >= 0 &&
                        coverageSlotIndex < slotResults.Count &&
                        slotResults[coverageSlotIndex].IsResolved &&
                        slotResults[coverageSlotIndex]
                            .HasAnisotropicSampling &&
                        HasAnyUnknownOutcome(
                            plan, submesh.SourceSubmeshIndex);
                    if (anisotropicCause)
                    {
                        // The coverage shortfall follows the unproven
                        // triangles, and the unproven triangles follow the
                        // anisotropic proof sampling, so the report names
                        // the acceptance hint instead of the coverage
                        // minimum. One refusal per slot: the set folds the
                        // submeshes that bind one slot.
                        if (reportedAnisotropySlots.Add(coverageSlotIndex))
                        {
                            AmuseReports.RecordRefusal(
                                state,
                                target.Renderer,
                                coverageSlotIndex,
                                AlphaSeparationSlotRefusal
                                    .AnisotropyDifferences);
                        }
                    }
                    else
                    {
                        AmuseReports.RecordRefusal(
                            state,
                            target.Renderer,
                            coverageSlotIndex,
                            AlphaSeparationSlotRefusal
                                .OpaqueCoverageBelowMinimum);
                    }

                    continue;
                }

                var slotIndex = submesh.SourceMaterialBindingIndex;

                // Barrier-side marker-clip check: VirtualClip.SetObjectCurve
                // silently no-ops on a marker clip, so a special motion
                // carrying this slot's material-swap binding refuses the slot
                // before any conversion work is prepared for it. The apply
                // pass re-checks the live clip, which is the authoritative
                // one.
                var markerCarries = false;
                foreach (var clipEvidence in evidence.Clips)
                {
                    if (!clipEvidence.IsSpecialMotion)
                    {
                        continue;
                    }

                    foreach (var objectBinding in
                                 clipEvidence.ObjectBindings)
                    {
                        if (UnityAnimationEvidenceCapture
                                .TryParseMaterialSlotBindingFor(
                                    objectBinding.Path,
                                    objectBinding.TypeName,
                                    objectBinding.PropertyName,
                                    rendererPath,
                                    rendererTypeName,
                                    out var markerSlot) &&
                            markerSlot == slotIndex)
                        {
                            markerCarries = true;
                            break;
                        }
                    }

                    if (markerCarries)
                    {
                        break;
                    }
                }

                if (markerCarries)
                {
                    AmuseReports.RecordRefusal(
                        state,
                        target.Renderer,
                        slotIndex,
                        AlphaSeparationSlotRefusal
                            .MarkerClipCarriesSlotBinding);
                    continue;
                }

                var mapping = new Dictionary<Material, Material>();
                var pendingClones = new List<Material>();
                var slotRefusal = AlphaSeparationSlotRefusal.None;
                var unconvertedCount = 0;

                // One record per refused material, in admission order. The
                // inner per-gate refusal token rides along when the family
                // boundary carries one. The report prints it after the
                // cause, so the seven conversion arms stop collapsing into
                // one bare enum.
                var collectedRefusals =
                    new List<(AlphaSeparationSlotRefusal cause,
                        Material material, string detail)>();
                var slotDivergence = false;
                var slotPremultiply = false;
                var isMultiMaterialSlot = slots[slotIndex].AdmittedMaterialIndices.Count > 1;
                foreach (var admittedIndex in
                             slots[slotIndex].AdmittedMaterialIndices)
                {
                    var captured = evidence.AdmittedMaterials[admittedIndex];
                    var live = admittedLiveMaterials[admittedIndex];
                    if (mapping.ContainsKey(live))
                    {
                        continue;
                    }

                    // The avatar-wide map supplies only a reusable artifact,
                    // never a conversion decision: the conversion decision is
                    // per renderer/slot because conversion-relevant animation
                    // and admitted derived evidence are renderer-specific,
                    // while the generated opaque artifact is deduplicated
                    // avatar-wide by source material. Every renderer/slot
                    // therefore always performs its own family, admission,
                    // overwrite, attestation and eligibility validation; an
                    // already-prepared result is handed to the conversion
                    // boundary so no duplicate clone is created, and a
                    // refused renderer/slot never reuses the artifact.
                    Material preparedOpaque = null;
                    if (state.Separation != null &&
                        state.Separation.TryGetOpaque(
                            live, out var registered))
                    {
                        preparedOpaque = registered;
                    }

                    // Only conversion-capable families read their bucket:
                    // the opaque lilToon arm maps to itself before
                    // admission and the unsupported arm refuses before it,
                    // so a null bucket never reaches an admission.
                    conversionBuckets.TryGetValue(
                        captured.Family, out var familyBindings);
                    conversionPropertyNamesByFamily.TryGetValue(
                        captured.Family, out var familyPropertyNames);
                    var conversionRefusal = ConvertAdmittedMaterial(
                        captured,
                        live,
                        familyBindings,
                        familyPropertyNames,
                        preparedOpaque,
                        poiyomiConversion,
                        lilToonConversion,
                        allowDepthTestChange,
                        grantedShaderNames,
                        out var opaque,
                        out var materialDivergence,
                        out var materialPremultiply,
                        out var conversionRefusedDetail);
                    slotDivergence |= materialDivergence;
                    slotPremultiply |= materialPremultiply;
                    if (conversionRefusal != AlphaSeparationSlotRefusal.None)
                    {
                        collectedRefusals.Add(
                            (conversionRefusal, live,
                                conversionRefusedDetail));
                        if (isMultiMaterialSlot)
                        {
                            // In a multi-material swap, an unconverted material falls back
                            // to identity on the appended submesh, preserving its appearance.
                            opaque = live;
                            unconvertedCount++;
                        }
                        else
                        {
                            slotRefusal = conversionRefusal;
                            break;
                        }
                    }
                    // The same successfully mapped admitted source feeds
                    // the output map, so the output map never names a
                    // source it lacks.
                    mapping.Add(live, opaque);
                    if (!ReferenceEquals(opaque, live) &&
                        !ReferenceEquals(opaque, preparedOpaque))
                    {
                        pendingClones.Add(opaque);
                    }
                }
                if (isMultiMaterialSlot &&
                    unconvertedCount >= slots[slotIndex].AdmittedMaterialIndices.Count &&
                    slotRefusal == AlphaSeparationSlotRefusal.None)
                {
                    // Every admitted material in this swap failed conversion: refuse the slot
                    // with the last conversion failure rather than creating an un-optimized clone.
                    slotRefusal =
                        collectedRefusals[collectedRefusals.Count - 1].cause;
                }

                if (slotRefusal != AlphaSeparationSlotRefusal.None)
                {
                    var lastRefusal =
                        collectedRefusals[collectedRefusals.Count - 1];
                    AmuseReports.RecordRefusal(
                        state,
                        target.Renderer,
                        slotIndex,
                        slotRefusal,
                        offendingMaterial: lastRefusal.material,
                        detail: slotRefusal ==
                                AlphaSeparationSlotRefusal
                                    .OpaqueConversionRefused &&
                            !string.IsNullOrEmpty(lastRefusal.detail)
                            ? "OpaqueConversionRefused: " +
                              lastRefusal.detail
                            : null);
                    // The slot is dropped with nothing registered: clones
                    // created for its earlier admitted materials would be
                    // unreachable and unknown to the apply pass's sweep. They
                    // are AMUSE-owned transients created by this same call
                    // and are destroyed here.
                    foreach (var pending in pendingClones)
                    {
                        UnityEngine.Object.DestroyImmediate(pending);
                    }

                    continue;
                }
                // A slot that kept converting emits one entry per refused material,
                // so the console names every conversion that did not happen. The slot
                // is not refused, so this writes no census record.
                else if (isMultiMaterialSlot && collectedRefusals.Count > 0)
                {
                    foreach (var refusal in collectedRefusals)
                    {
                        AmuseReports.SlotSeparationRefusal(
                            target.Renderer, slotIndex, refusal.cause,
                            offendingMaterial: refusal.material,
                            detail: refusal.detail);
                    }
                }

                // Every admitted material of the slot mapped, so the slot is
                // prepared. Registration is avatar-wide and deduplicating;
                // doing it only after the full admitted set mapped keeps a
                // refused slot from leaving prepared state behind.
                foreach (var pair in mapping)
                {
                    RegisterPreparedOpaque(state, pair.Key, pair.Value);
                }

                candidateSlots.Add(new PreparedSlotSeparation(
                    submesh, mapping, slotDivergence,
                    slotPremultiply));

                // One Information entry per prepared slot whose
                // conversion admitted a depth-test divergence, so the
                // build report names the change for every affected
                // slot. Unflagged slots keep their previous silence.
                if (slotDivergence)
                {
                    AmuseReports.SlotSeparationDivergence(
                        target.Renderer,
                        slotIndex,
                        target.Renderer != null
                            ? target.Renderer.gameObject.name
                            : null);
                }

                // One Information entry per folded surviving submesh, so
                // the build report names every triangle set that moved
                // onto the opaque material without an opacity proof. The
                // fold map holds one entry per folded submesh, so a
                // folded slot reports once per submesh, and a slot the
                // gate did not fold stays silent. A refused slot never
                // reaches this point, so a marker-clip refusal reports
                // alone.
                var hasFoldedSubmesh =
                    unprovenCountByFoldedSubmesh.TryGetValue(
                        submesh.SourceSubmeshIndex,
                        out var foldedUnprovenCount);
                if (hasFoldedSubmesh)
                {
                    AmuseReports.SlotSeparationForcedOpaque(
                        target.Renderer,
                        slotIndex,
                        foldedUnprovenCount,
                        target.Renderer != null
                            ? target.Renderer.gameObject.name
                            : null);
                }

                // One Information entry per prepared slot whose
                // conversion used the revised premultiply premise, so the
                // build report states the normalization through its own
                // sentence. A folded slot selects the folded entries,
                // which name the preserve setting instead of claiming
                // exact colors. The depth sentence stays depth-only, and
                // a slot without a premultiply conversion stays silent.
                if (slotPremultiply)
                {
                    AmuseReports.SlotSeparationPremultiplyNormalization(
                        target.Renderer,
                        slotIndex,
                        target.Renderer != null
                            ? target.Renderer.gameObject.name
                            : null,
                        hasFoldedSubmesh);
                }
            }

            if (candidateSlots.Count == 0)
            {
                return null;
            }

            var expectedSubmeshIndexCounts = new int[target.ExpectedMesh.subMeshCount];
            for (var submesh = 0; submesh < expectedSubmeshIndexCounts.Length; submesh++)
            {
                expectedSubmeshIndexCounts[submesh] =
                    (int)target.ExpectedMesh.GetIndexCount(submesh);
            }

            var prepared = new PreparedRendererSeparation(
                target,
                rendererPath,
                plan,
                evidence,
                candidateSlots,
                EffectiveMaterialMaterialization.CaptureBlockState(
                    target.Renderer),
                expectedSubmeshIndexCounts,
                rendererTypeName);

            // A mesh clone is created only when the plan requires a split and
            // at least one Split slot survived preparation; clones abandoned
            // by later validation failures are handled by the apply pass's
            // sweep, not by avoiding creation.
            if (plan.RequiresAnySplit &&
                candidateSlots.Any(
                    slot => slot.Plan.Disposition ==
                            SubmeshSeparationDisposition.Split))
            {
                prepared.MeshClone = UnityEngine.Object.Instantiate(
                    target.ExpectedMesh);
            }

            return prepared;
        }

        /// <summary>
        /// Whether the source submesh behind this plan entry holds at
        /// least one triangle whose alpha outcome stayed unproven. The
        /// source input and the plan keep one entry per source submesh in
        /// the same order, so the plan's own submesh index addresses the
        /// source outcomes directly.
        /// </summary>
        private static bool HasAnyUnknownOutcome(
            MeshSeparationPlan plan,
            int submeshIndex)
        {
            if (plan.Source?.Submeshes == null ||
                submeshIndex < 0 ||
                submeshIndex >= plan.Source.Submeshes.Count)
            {
                return false;
            }

            var outcomes = plan.Source.Submeshes[submeshIndex].Outcomes;
            return outcomes?.Contains(TriangleAlphaOutcome.Unknown) == true;
        }

        /// <summary>
        /// Records one renderer-scoped refusal once per candidate slot it
        /// drops, so the buckets always count dropped slots, and retains
        /// nothing for the renderer.
        /// </summary>
        private static PreparedRendererSeparation RefuseEveryCandidateSlot(
            UnityRendererMutationTarget target,
            AmusePlatformFinishState state,
            MeshSeparationPlan plan,
            AlphaSeparationSlotRefusal reason)
        {
            foreach (var submesh in plan.Submeshes)
            {
                if (submesh.Disposition ==
                    SubmeshSeparationDisposition.Unchanged)
                {
                    continue;
                }

                AmuseReports.RecordRefusal(
                    state,
                    target.Renderer,
                    submesh.SourceMaterialBindingIndex,
                    reason);
            }

            return null;
        }

        /// <summary>
        /// True when this build's D8 transfer consent granted exactly this
        /// shader name, so the identity conjunction's failure is an accepted,
        /// user-authorized risk for this build: the unverified source is
        /// treated with the verified version's rules, exactly as the
        /// transferred capture entry already does. A null or ungranted name
        /// keeps the fail-closed refusal.
        /// </summary>
        private static bool IsGrantedShader(
            string shaderName,
            IReadOnlyCollection<string> grantedShaderNames)
        {
            return !string.IsNullOrEmpty(shaderName) &&
                   grantedShaderNames != null &&
                   grantedShaderNames.Contains(shaderName);
        }

        /// <summary>
        /// Runs the shader-family conversion boundary for one admitted
        /// material: the per-family branch, derived conversion evidence
        /// admitted against this material's own captured defaults under its
        /// family's own conversion request, the conversion step (the real
        /// family route, or the verified-fixture seam), and the renderer-wide
        /// runtime-overwrite rule against the family's own recipe. A
        /// resolved Multi material consults its stored resolution record
        /// instead of the family route: the Multi eligibility evaluator
        /// judges with the captured mode, and the recipe stays the resolved
        /// family's own. Returns
        /// the mapped opaque result, or the slot-local refusal. A material
        /// the depth-test policy admitted onto a mixed-split plan refuses
        /// here, before any material is prepared for that slot.
        /// </summary>
        internal static AlphaSeparationSlotRefusal ConvertAdmittedMaterial(
            CapturedAlphaMaterial captured,
            Material live,
            IReadOnlyList<(CapturedFloatBinding Binding,
                           AnimatedPropertyRef Reference)> conversionBindings,
            IReadOnlyCollection<string> conversionPropertyNames,
            Material preparedOpaque,
            VerifiedPoiyomiConversion poiyomiConversion,
            VerifiedLilToonConversion lilToonConversion,
            bool allowDepthTestChange,
            IReadOnlyCollection<string> grantedShaderNames,
            out Material opaque,
            out bool depthTestDivergence,
            out bool premultiplyNormalization,
            out string refusedDetail)
        {
            opaque = null;
            depthTestDivergence = false;
            premultiplyNormalization = false;
            refusedDetail = null;
            switch (captured.Family)
            {
                case CapturedAlphaMaterialFamily.LilToon:
                    // The opaque lilToon frontend is already the canonical
                    // opaque answer — alpha is forced to one — so there is
                    // nothing to convert and no admission to run. The
                    // identity mapping keeps a runtime material swap on this
                    // slot valid without a clone.
                    opaque = live;
                    return AlphaSeparationSlotRefusal.None;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                {
                    var isTransparent = captured.Family ==
                        CapturedAlphaMaterialFamily.LilToonTransparent;
                    var admission = TryAdmitConversionEvidence(
                        captured,
                        conversionBindings,
                        conversionPropertyNames,
                        out var derived);
                    if (admission != AlphaSeparationSlotRefusal.None)
                    {
                        return admission;
                    }

                    // A resolved Multi material attested once, at the one
                    // post-capture resolution point, and its verdict is
                    // stored on the material. The per-family source verify
                    // below cannot read a Multi container, so the boundary
                    // consults the resolution record instead: the Multi
                    // eligibility evaluator judges the same derived
                    // evidence with the captured mode, and the canonical
                    // recipe stays the resolved family's own. A regular
                    // material never carries a record, so this conditional
                    // is inert on every existing family.
                    if (captured.MultiResolution != null)
                    {
                        // Effective non-property facts, read in the barrier
                        // beside the evidence exactly as the regular route
                        // reads them: neither fact is animation-reachable.
                        EffectiveRenderState.ReadEffectiveRenderState(
                            live, out var multiQueue,
                            out var multiRenderType);
                        if (!LilToonMultiResolution.TryReadCapturedMode(
                                captured.Evidence, out var multiMode))
                        {
                            throw new InvalidOperationException(
                                "A resolved Multi material carries no " +
                                "readable mode.");
                        }

                        var multiEligibility =
                            LilToonMultiSourceEligibility
                                .EvaluateVerifiedEligibility(
                                    derived, multiQueue, multiRenderType,
                                    multiMode, allowDepthTestChange);
                        if (!TryMapLilToonOutcome(
                                multiEligibility,
                                () => preparedOpaque ??
                                    LilToonOpaqueTarget
                                        .PrepareCanonicalOpaqueClone(
                                            live, derived),
                                out opaque,
                                out var multiRefusal,
                                out depthTestDivergence))
                        {
                            refusedDetail = multiRefusal.ToString();
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        return AlphaSeparationSlotRefusal.None;
                    }

                    if (lilToonConversion != null)
                    {
                        // The verified-fixture seam substitutes the
                        // lilToon conversion step the caller supplies —
                        // effective render state, real eligibility and
                        // the real canonical
                        // clone recipe — and deliberately skips the
                        // source-identity check no stand-in shader can pass.
                        // The policy crosses the seam as an input and the
                        // divergence as an output, so the guard below sees
                        // the same fact the real route reads off its own
                        // eligibility result.
                        if (!lilToonConversion(
                                live,
                                derived,
                                allowDepthTestChange,
                                preparedOpaque,
                                out opaque,
                                out var seamRefusal,
                                out var seamDivergence))
                        {
                            refusedDetail = seamRefusal.ToString();
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        // The seam reported the divergence of the
                        // material that converted.
                        depthTestDivergence = seamDivergence;
                    }
                    else
                    {
                        // Effective non-property facts, read in the barrier
                        // beside the evidence: neither fact is
                        // animation-reachable, so reading them here is not a
                        // late live read of animation-relevant state.
                        EffectiveRenderState.ReadEffectiveRenderState(
                            live, out var queue, out var renderType);

                        // Conversion attestation of the pinned cutout or
                        // transparent source, per the merged conversion
                        // design.
                        var sourceEvidence = isTransparent
                            ? LilToonSourceAttestation
                                .GatherTransparentSourceEvidence(
                                    live.shader, derived)
                            : LilToonSourceAttestation
                                .GatherCutoutSourceEvidence(
                                    live.shader, derived);
                        var attested = isTransparent
                            ? LilToonSourceAttestation
                                .TryVerifyLilToonTransparentIdentity(
                                    sourceEvidence,
                                    out var lilIdentityDiagnostic)
                            : LilToonSourceAttestation
                                .TryVerifyLilToonCutoutIdentity(
                                    sourceEvidence,
                                    out lilIdentityDiagnostic);
                        if (!attested &&
                            !IsGrantedShader(
                                sourceEvidence.ShaderName,
                                grantedShaderNames))
                        {
                            refusedDetail =
                                "SourceIdentity." +
                                lilIdentityDiagnostic.Code;
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        var eligibility = isTransparent
                            ? LilToonTransparentSourceEligibility
                                .EvaluateVerifiedEligibility(
                                    derived, queue, renderType,
                                    allowDepthTestChange)
                            : LilToonCutoutSourceEligibility
                                .EvaluateVerifiedEligibility(
                                    derived, queue, renderType,
                                    allowDepthTestChange);
                        if (!TryMapLilToonOutcome(
                                eligibility,
                                () => preparedOpaque ??
                                    LilToonOpaqueTarget
                                        .PrepareCanonicalOpaqueClone(
                                            live, derived),
                                out opaque,
                                out var sourceRefusal,
                                out depthTestDivergence))
                        {
                            refusedDetail = sourceRefusal.ToString();
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }
                    }

                    return AlphaSeparationSlotRefusal.None;
                }
                case CapturedAlphaMaterialFamily.Poiyomi:
                {
                    var admission = TryAdmitConversionEvidence(
                        captured,
                        conversionBindings,
                        conversionPropertyNames,
                        out var derived);
                    if (admission != AlphaSeparationSlotRefusal.None)
                    {
                        return admission;
                    }

                    if (poiyomiConversion != null)
                    {
                        // The verified-fixture seam substitutes the
                        // shader-family conversion step — effective render
                        // state, real eligibility and the real canonical
                        // clone recipe — and deliberately skips the
                        // source-identity check no stand-in shader can pass.
                        // The policy crosses the seam as an input and the
                        // divergence and normalization as outputs, exactly
                        // as on the lilToon route above.
                        if (!poiyomiConversion(
                                live,
                                derived,
                                allowDepthTestChange,
                                preparedOpaque,
                                out opaque,
                                out var seamRefusal,
                                out var seamDivergence,
                                out var seamPremultiply))
                        {
                            refusedDetail = seamRefusal.ToString();
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        // The seam reported the facts of the material that
                        // converted.
                        depthTestDivergence = seamDivergence;
                        premultiplyNormalization = seamPremultiply;
                    }
                    else
                    {

                        // Effective non-property facts, read in the barrier
                        // beside the evidence: neither fact is
                        // animation-reachable, so reading them here is not a
                        // late live read of animation-relevant state.
                        EffectiveRenderState.ReadEffectiveRenderState(
                            live, out var queue, out var renderType);

                        // Conversion attestation of the pinned source, per
                        // the merged conversion design: a locked Poiyomi
                        // material fails here.
                        var sourceEvidence =
                            PoiyomiOpaqueConversion
                                .GatherConversionSourceEvidence(
                                    live.shader, derived);
                        if (!PoiyomiMaterialSemantics
                                .TryVerifyPoiyomiIdentity(
                                    sourceEvidence,
                                    out var poiIdentityDiagnostic))
                        {
                            refusedDetail =
                                "SourceIdentity." +
                                poiIdentityDiagnostic.Code;
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }

                        var eligibility =
                            PoiyomiOpaqueConversion
                                .EvaluateVerifiedEligibility(
                                    derived, queue, renderType,
                                    allowDepthTestChange);
                        if (!TryMapPoiyomiOutcome(
                                eligibility, live, preparedOpaque,
                                out opaque,
                                out var poiyomiRefusal,
                                out depthTestDivergence,
                                out premultiplyNormalization))
                        {
                            refusedDetail = poiyomiRefusal.ToString();
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }
                    }

                    return AlphaSeparationSlotRefusal.None;
                }
                default:
                    // No conversion-capable family attests this material,
                    // and not every admitted runtime value could be mapped.
                    return AlphaSeparationSlotRefusal
                        .OpaqueConversionUnsupportedFamily;
            }
        }

        /// <summary>
        /// The one lilToon conversion outcome mapping: a refused eligibility
        /// maps to its refusal and no material, and a convertible one maps to
        /// the caller's clone. The production arms and the verified fixture
        /// seam call this, so the outcome mapping cannot drift between the
        /// production path and the fixture path.
        /// </summary>
        internal static bool TryMapLilToonOutcome(
            LilToonOpaqueConversionEligibility eligibility,
            Func<Material> createClone,
            out Material opaque,
            out LilToonOpaqueConversionRefusal refusal,
            out bool depthTestDivergence)
        {
            if (eligibility.Outcome !=
                LilToonOpaqueConversionOutcome.Convertible)
            {
                opaque = null;
                refusal = eligibility.Refusal;
                depthTestDivergence = false;
                return false;
            }

            // An already-prepared artifact for this source is reused here;
            // only a first conversion creates the canonical clone. The caller
            // owns the clone recipe, because the production route attests
            // through the evidence overload while the verified seam pins an
            // explicit stand-in shader.
            opaque = createClone();
            refusal = LilToonOpaqueConversionRefusal.None;
            depthTestDivergence = eligibility.DepthTestDivergence;
            return true;
        }

        /// <summary>
        /// The one Poiyomi conversion outcome mapping: an already-opaque
        /// source keeps the live material, a convertible source maps to the
        /// prepared artifact or a fresh canonical clone, and a refused
        /// eligibility maps to its refusal and no material. The production
        /// arm and the verified fixture seam call this, so the outcome
        /// mapping cannot drift between the two routes.
        /// </summary>
        internal static bool TryMapPoiyomiOutcome(
            PoiyomiOpaqueConversionEligibility eligibility,
            Material live,
            Material preparedOpaque,
            out Material opaque,
            out PoiyomiOpaqueConversionRefusal refusal,
            out bool depthTestDivergence,
            out bool premultiplyNormalization)
        {
            switch (eligibility.Outcome)
            {
                case PoiyomiOpaqueConversionOutcome.AlreadyOpaque:
                    opaque = live;
                    refusal = PoiyomiOpaqueConversionRefusal.None;
                    depthTestDivergence = false;
                    premultiplyNormalization = false;
                    return true;
                case PoiyomiOpaqueConversionOutcome.Convertible:
                    // An already-prepared artifact for this source is reused
                    // here; only a first conversion creates the canonical
                    // clone.
                    opaque = preparedOpaque ??
                        PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(
                            live);
                    refusal = PoiyomiOpaqueConversionRefusal.None;
                    depthTestDivergence = eligibility.DepthTestDivergence;
                    premultiplyNormalization =
                        eligibility.PremultiplyNormalization;
                    return true;
                default:
                    opaque = null;
                    refusal = eligibility.Refusal;
                    depthTestDivergence = false;
                    premultiplyNormalization = false;
                    return false;
            }
        }

        /// <summary>
        /// The shared admission prologue of every converting arm. Derived
        /// conversion evidence: the same group-and-admit loop the alpha
        /// resolution uses, re-run against this family's own conversion
        /// request, so conversion shares one admission implementation
        /// rather than duplicating it. A conversion-relevant animated
        /// property that is not an exact singleton equal to this
        /// material's own serialized default refuses here.
        /// <para>
        /// The renderer-wide runtime-overwrite rule runs BEFORE the
        /// conversion step, against this family's own recipe: a canonical
        /// recipe property that an admitted conversion binding drives must
        /// already hold its canonical value, because admission is
        /// exact-singleton against the material's own serialized default.
        /// Failing it is a slot-local refusal, not a defect — and no
        /// material may be created for a slot already known to violate the
        /// rule the recipe depends on.
        /// </para>
        /// </summary>
        private static AlphaSeparationSlotRefusal TryAdmitConversionEvidence(
            CapturedAlphaMaterial captured,
            IReadOnlyList<(CapturedFloatBinding Binding,
                           AnimatedPropertyRef Reference)> conversionBindings,
            IReadOnlyCollection<string> conversionPropertyNames,
            out CapturedMaterialEvidence derived)
        {
            if (!AdmittedMaterialStates.TryAdmitDerivedEvidence(
                    captured,
                    conversionBindings,
                    ConversionRequestForFamily(captured.Family),
                    out derived,
                    out _,
                    UnityAnimationEvidenceCapture.TextureScaleOffsetSuffix))
            {
                return AlphaSeparationSlotRefusal
                    .ConversionStateNotAdmitted;
            }

            foreach (var (property, canonicalValue) in
                         CanonicalPropertiesForFamily(captured.Family))
            {
                if (!conversionPropertyNames.Contains(property))
                {
                    continue;
                }

                if (!derived.TryGetScalar(
                        property, out var admitted) ||
                    admitted != canonicalValue)
                {
                    return AlphaSeparationSlotRefusal
                        .ConversionPropertyOverwrittenAtRuntime;
                }
            }

            return AlphaSeparationSlotRefusal.None;
        }

        /// <summary>
        /// The conversion request one family's conversion boundary reads, or
        /// null for families with no conversion: the relevance pass unions
        /// the requests of the renderer's conversion-capable families, and
        /// per-material admission re-resolves against the material's own
        /// family request.
        /// </summary>
        private static MaterialEvidenceRequest ConversionRequestForFamily(
            CapturedAlphaMaterialFamily family)
        {
            switch (family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    return PoiyomiOpaqueConversion.ConversionEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return LilToonCutoutSourceEligibility.ConversionEvidenceRequest;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonTransparentSourceEligibility
                        .ConversionEvidenceRequest;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The canonical recipe one family's conversion writes, or null for
        /// families with no conversion: the runtime-overwrite rule checks
        /// exactly the family's own recipe properties against their
        /// canonical values.
        /// </summary>
        private static IReadOnlyList<(string Property, float Value)>
            CanonicalPropertiesForFamily(
            CapturedAlphaMaterialFamily family)
        {
            switch (family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    return PoiyomiOpaqueConversion.CanonicalOpaqueProperties;
                case CapturedAlphaMaterialFamily.LilToonCutout:
                    return LilToonOpaqueTarget.CanonicalOpaqueProperties;
                case CapturedAlphaMaterialFamily.LilToonTransparent:
                    return LilToonOpaqueTarget.CanonicalOpaqueProperties;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Registers one prepared opaque result in the avatar-wide
        /// deduplicating map, creating the retained record on first use.
        /// AlreadyOpaque identities are recorded in the mapping but never
        /// enter <see cref="PreparedAlphaSeparation.CreatedClones"/>, and a
        /// source that is already registered — shared across slots or
        /// renderers — returns its existing result, so two renderers
        /// referencing the same source material share one clone.
        /// </summary>
        private static Material RegisterPreparedOpaque(
            AmusePlatformFinishState state,
            Material source,
            Material opaque)
        {
            state.Separation ??= new PreparedAlphaSeparation();
            if (state.Separation.TryGetOpaque(source, out var existing))
            {
                return existing;
            }

            state.Separation.RegisterOpaque(source, opaque);
            return opaque;
        }
    }
}
