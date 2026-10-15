using System;
using System.Collections.Generic;
using System.Threading;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// The runtime-state pipeline the PlatformFinish barrier runs per
    /// renderer: slot admission from captured evidence, per-slot resolution
    /// under the captured sampling policy, and classification of the
    /// geometry snapshot into a separation plan. The stored-value mapping
    /// halves of the pass's optimizer-backed policy helpers live here too,
    /// so an edit-time dry run can resolve the same states under the same
    /// mapping without a build.
    /// </summary>
    internal static class RuntimeStateResolution
    {
        internal sealed class ResolvedRuntimeStates
        {
            internal ResolvedRuntimeStates(
                RendererAnalysisRefusal refusal,
                IReadOnlyList<CapturedAlphaMaterial> currentMaterials,
                SlotResolutionResult[] slotResults)
            {
                Refusal = refusal;
                CurrentMaterials = currentMaterials;
                SlotResults = slotResults;
            }

            internal RendererAnalysisRefusal Refusal { get; }
            internal IReadOnlyList<CapturedAlphaMaterial> CurrentMaterials { get; }

            /// <summary>
            /// One retained result per material slot: either that slot's
            /// deduplicated resolutions or its own named refusal. Renderer- and
            /// avatar-scoped failures are reported through
            /// <see cref="Refusal"/> instead and leave this empty.
            /// <para>
            /// A refused slot's reason is retained here but is deliberately not
            /// reported anywhere: this milestone reuses the existing
            /// <see cref="SlotResolutionResult"/> and
            /// <see cref="RendererAnalysisRefusal"/> vocabulary and adds no
            /// persistent per-slot diagnostics. A concrete consumer that needs
            /// durable per-slot reporting is the point at which a separate
            /// vocabulary and state representation should be designed, not
            /// before.
            /// </para>
            /// </summary>
            internal SlotResolutionResult[] SlotResults { get; }
        }

        internal static ResolvedRuntimeStates ResolveRuntimeStates(
                string rendererPath,
                CapturedAnimationEvidence evidence,
                AlphaSamplingPolicy samplingPolicy,
                CapturedAlphaMaterialSemanticsResolver resolveSemantics = null,
                int maxMipLevel = int.MaxValue,
                int minTextureSize = 1,
                int densityCapPercent = 0,
                IReadOnlyList<BlockStateEntry> blockState = null,
                string rendererTypeName = null)
        {
            if (rendererPath == null)
                throw new ArgumentNullException(nameof(rendererPath));
            if (evidence == null)
                throw new ArgumentNullException(nameof(evidence));

            var floats = new List<CapturedFloatBinding>();
            var objects = new List<CapturedObjectBinding>();
            foreach (var clip in evidence.Clips)
            {
                floats.AddRange(clip.FloatBindings);
                objects.AddRange(clip.ObjectBindings);
            }

            var structural = UnityRendererAlphaAnalysis.StructuralRefusalFor(
                floats, objects, rendererPath, rendererTypeName);
            if (structural != RendererAnalysisRefusal.None)
                return Refused(structural);

            var relevantBindings = new List<(
                CapturedFloatBinding Binding,
                AnimatedPropertyRef Reference)>();
            foreach (var binding in floats)
            {
                var resolution =
                    UnityAnimationEvidenceCapture.ResolveProofRelevant(
                        binding,
                        rendererPath,
                        evidence.AlphaRelevanceRequest,
                        out var reference,
                        rendererTypeName);
                if (resolution ==
                    ProofRelevantBindingResolution.UnrecognizedMaterialBinding)
                {
                    return Refused(
                        RendererAnalysisRefusal
                            .UnrecognizedAnimatedMaterialBinding);
                }

                if (resolution == ProofRelevantBindingResolution.RendererWide)
                    relevantBindings.Add((binding, reference));
            }

            // An object-reference curve can name a proof-relevant material
            // property just as a float curve can, and the same fail-closed rule
            // applies: recognized structural and material-slot forms are handled
            // above and below, anything else that could address a requested
            // property is unsupported syntax rather than irrelevant.
            foreach (var binding in objects)
            {
                if (UnityAnimationEvidenceCapture
                        .IsUnrecognizedObjectMaterialBinding(
                            binding, rendererPath, evidence.AlphaRelevanceRequest, rendererTypeName))
                {
                    return Refused(
                        RendererAnalysisRefusal
                            .UnrecognizedAnimatedMaterialBinding);
                }
            }

            if (relevantBindings.Count > 0 && evidence.HasAdditiveLayer)
            {
                return Refused(
                    RendererAnalysisRefusal
                        .AdditiveLayerWithProofRelevantMaterialProperty);
            }

            if (relevantBindings.Count > 0 &&
                evidence.HasUnnormalizedDirectBlendTree)
            {
                return Refused(
                    RendererAnalysisRefusal
                        .UnnormalizedDirectBlendTreeWithProofRelevantMaterialProperty);
            }

            var slots = MaterialSlotsFor(evidence, rendererPath, rendererTypeName);
            var fields = UnityRendererAlphaAnalysis.GatherAlphaFields(
                evidence.AdmittedMaterials,
                maxMipLevel,
                minTextureSize);

            // Every slot is resolved; no slot's failure stops the loop. A
            // slot's admission failure is a fact about that slot's own admitted
            // materials, so it must not decide anything for a sibling slot
            // whose proof does not depend on it.
            // Renderer-scoped facts — closure, structural refusals,
            // unrecognized bindings, additive layers and unnormalized direct
            // blend trees — are all established above and keep their scope.
            var slotResults = new SlotResolutionResult[slots.Count];
            var firstSlotRefusal = RendererAnalysisRefusal.None;
            var anySlotResolved = false;
            for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                // A slot-scoped closure failure is a fact about this slot
                // alone. The renderer is closed, so the slot refuses here
                // and every sibling slot's own resolution continues.
                var closureWay = ClosureWayForSlot(evidence, slotIndex);
                if (closureWay !=
                    MaterialDependencyClosureFailure.None)
                {
                    slotResults[slotIndex] = SlotResolutionResult.Refused(
                        RendererAnalysisRefusal
                            .MaterialDependencyClosureFailed,
                        closureWay: closureWay);
                    if (firstSlotRefusal == RendererAnalysisRefusal.None)
                    {
                        firstSlotRefusal = RendererAnalysisRefusal
                            .MaterialDependencyClosureFailed;
                    }

                    continue;
                }

                var slotEntries = FilterBlockEntriesForSlot(blockState, slotIndex);
                var resolved = AdmittedMaterialStates.ResolveSlot(
                    slots[slotIndex],
                    evidence.AdmittedMaterials,
                    relevantBindings,
                    evidence.AlphaRelevanceRequest,
                    fields,
                    samplingPolicy,
                    densityCapPercent,
                    resolveSemantics,
                    slotEntries);
                if (!resolved.IsResolved)
                {
                    // Retained as it stands, refusal reason included, so the
                    // slot is preserved rather than dropped.
                    slotResults[slotIndex] = resolved;
                    if (firstSlotRefusal == RendererAnalysisRefusal.None)
                        firstSlotRefusal = resolved.Refusal;
                    continue;
                }

                anySlotResolved = true;
                // The deduped list keeps feeding classification; the raw
                // list rides along so reporting can attribute a refused
                // resolution to its own admitted material.
                slotResults[slotIndex] = SlotResolutionResult.Resolved(
                    AdmittedMaterialStates.DistinctResolutions(
                        resolved.Resolutions),
                    resolved.MaterialResolutions);
            }

            // Nothing resolved, so there is no partial result to preserve and
            // the renderer keeps exactly its previous refusal, reason and
            // accounting. The first refusal is the reported one, which is the
            // same one the earlier return-on-first-failure produced. The
            // per-slot results ride along: the pass reports every refused
            // slot's own exact reason even when the first refusal names the
            // renderer, so a single-slot avatar still reads which material
            // and which shader fact stopped its proof.
            if (firstSlotRefusal != RendererAnalysisRefusal.None &&
                !anySlotResolved)
            {
                return new ResolvedRuntimeStates(
                    firstSlotRefusal,
                    Array.Empty<CapturedAlphaMaterial>(),
                    slotResults);
            }

            var currentMaterials =
                new CapturedAlphaMaterial[evidence.CurrentMaterialIndices.Count];
            for (var slot = 0; slot < currentMaterials.Length; slot++)
            {
                // The -1 sentinel marks a closure-failed slot. Geometry
                // extraction carries the slots positionally and never reads
                // them for classification: a refused slot's triangles are
                // unproven outcomes, so the null entry is inert.
                var index = evidence.CurrentMaterialIndices[slot];
                currentMaterials[slot] = index >= 0
                    ? evidence.AdmittedMaterials[index]
                    : null;
            }

            return new ResolvedRuntimeStates(
                RendererAnalysisRefusal.None,
                currentMaterials,
                slotResults);
        }

        internal static IReadOnlyList<BlockStateEntry> FilterBlockEntriesForSlot(
            IReadOnlyList<BlockStateEntry> blockState,
            int slotIndex)
        {
            if (blockState == null || blockState.Count == 0)
            {
                return null;
            }

            var list = new List<BlockStateEntry>();
            for (var i = 0; i < blockState.Count; i++)
            {
                if (blockState[i].SlotIndex == slotIndex)
                {
                    list.Add(blockState[i]);
                }
            }

            return list;
        }

        /// <summary>
        /// The way the slot's material dependency closure failed, or
        /// <see cref="MaterialDependencyClosureFailure.None"/> when the slot
        /// closed cleanly. One lookup per slot over the sorted failure
        /// records; the records are unique per slot, so the first match is
        /// the only one.
        /// </summary>
        internal static MaterialDependencyClosureFailure ClosureWayForSlot(
            CapturedAnimationEvidence evidence,
            int slotIndex)
        {
            foreach (var record in evidence.SlotClosureFailures)
            {
                if (record.SlotIndex == slotIndex)
                {
                    return record.Failure;
                }
            }

            return MaterialDependencyClosureFailure.None;
        }

        internal static ResolvedRuntimeStates Refused(
            RendererAnalysisRefusal refusal)
        {
            return new ResolvedRuntimeStates(
                refusal,
                Array.Empty<CapturedAlphaMaterial>(),
                Array.Empty<SlotResolutionResult>());
        }

        /// <summary>
        /// Classifies every submesh against its own slot's admitted states and
        /// returns the separation plan. The plan was previously reduced to its
        /// opaque triangle count here; it is returned whole so the caller can
        /// also retain it, which changes no classification.
        /// </summary>
        internal static MeshSeparationPlan ClassifyRuntimeStates(
            UnityRendererAlphaSnapshot snapshot,
            IReadOnlyList<SlotResolutionResult> slotResults,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var submeshes = new List<SubmeshSeparationInput>(
                snapshot.Submeshes.Count);
            foreach (var submesh in snapshot.Submeshes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var slot = slotResults[submesh.MaterialSlotIndex];
                submeshes.Add(new SubmeshSeparationInput(
                    submesh.MaterialSlotIndex,
                    submesh.Indices,
                    slot.IsResolved
                        ? IntersectResolvedOutcomes(
                            snapshot, submesh, slot, cancellationToken)
                        : UnprovenOutcomes(submesh.Indices.Count / 3)));
            }

            return MeshSeparationPlanner.Create(
                new MeshSeparationInput(snapshot.VertexCount, submeshes));
        }

        internal static TriangleAlphaOutcome[] IntersectResolvedOutcomes(
            UnityRendererAlphaSnapshot snapshot,
            UnitySubmeshAlphaSnapshot submesh,
            SlotResolutionResult slot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var perResolution = new List<TriangleAlphaOutcome[]>(
                slot.Resolutions.Count);
            foreach (var resolution in slot.Resolutions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outcomes = UnityRendererAlphaAnalysis.Classify(
                    submesh.Indices,
                    snapshot.Positions,
                    snapshot.HasUv0 ? snapshot.Uv0 : null,
                    resolution,
                    snapshot.ExtraUvSets,
                    cancellationToken);
                perResolution.Add(outcomes);
            }

            return UnityRendererAlphaAnalysis.IntersectOutcomes(perResolution);
        }

        /// <summary>
        /// The outcomes for a slot whose admitted states could not be resolved.
        /// Such a slot proves nothing about its own triangles and nothing about
        /// any other slot's, so it keeps its submesh and its positional
        /// correspondence with its material slot, and every triangle is
        /// explicitly <see cref="TriangleAlphaOutcome.Unknown"/>: not opaque,
        /// and not asserted to require transparency either, which would be a
        /// claim this slot has no evidence for.
        /// <para>
        /// Written out rather than left to the enum's numeric default, which is
        /// <see cref="TriangleAlphaOutcome.ProvenOpaque"/> and would turn an
        /// unresolved slot into a false positive.
        /// </para>
        /// <para>
        /// This also keeps the empty-state guard in
        /// <c>IntersectOutcomes</c> intact: an unresolved slot never reaches it
        /// with no admitted states, so intersecting nothing still means what it
        /// means today.
        /// </para>
        /// </summary>
        internal static TriangleAlphaOutcome[] UnprovenOutcomes(int triangleCount)
        {
            var outcomes = new TriangleAlphaOutcome[triangleCount];
            for (var triangle = 0; triangle < outcomes.Length; triangle++)
                outcomes[triangle] = TriangleAlphaOutcome.Unknown;

            return outcomes;
        }

        /// <summary>
        /// One entry per renderer material slot addressing
        /// <see cref="CapturedAnimationEvidence.AdmittedMaterials"/>: the
        /// current assignment first, then clip/binding/value order. Shared
        /// with the barrier-side alpha-separation preparation, which maps the
        /// same per-slot admitted sets.
        /// </summary>
        internal static IReadOnlyList<CapturedMaterialSlotEvidence>
            MaterialSlotsFor(
                CapturedAnimationEvidence evidence,
                string rendererPath,
                string rendererTypeName = null)
        {
            var admittedBySlot = new List<int>[evidence.CurrentMaterialIndices.Count];
            for (var slot = 0; slot < admittedBySlot.Length; slot++)
            {
                admittedBySlot[slot] = new List<int>();

                // The -1 sentinel marks a slot whose closure failed: no
                // current material was admitted. The slot refuses as a
                // closure failure before resolution, so its admitted list
                // only needs the swap indices it still validly holds.
                if (evidence.CurrentMaterialIndices[slot] >= 0)
                {
                    admittedBySlot[slot].Add(
                        evidence.CurrentMaterialIndices[slot]);
                }
            }

            foreach (var clip in evidence.Clips)
            {
                foreach (var binding in clip.ObjectBindings)
                {
                    if (!UnityAnimationEvidenceCapture
                            .TryParseMaterialSlotBindingFor(
                                binding.Path,
                                binding.TypeName,
                                binding.PropertyName,
                                rendererPath,
                                rendererTypeName,
                                out var slot))
                        continue;

                    foreach (var material in binding.AdmittedMaterialIndices)
                    {
                        if (!admittedBySlot[slot].Contains(material))
                            admittedBySlot[slot].Add(material);
                    }
                }
            }

            var slots = new CapturedMaterialSlotEvidence[admittedBySlot.Length];
            for (var slot = 0; slot < slots.Length; slot++)
            {
                slots[slot] = new CapturedMaterialSlotEvidence(
                    slot,
                    admittedBySlot[slot],
                    CaptureRefusalsFor(evidence, admittedBySlot[slot]),
                    NoAlphaChannelPropertiesFor(evidence, admittedBySlot[slot]));
            }

            return slots;
        }

        /// <summary>
        /// Collects the texture properties on a slot's admitted materials
        /// whose captured evidence proves sampled alpha exactly one through
        /// the importer theorem: the source file has no alpha channel and the
        /// import writes none, so the runtime samples exactly one at every
        /// texel. The report names these so the author can spot a likely
        /// material misconfiguration; the proof itself is exact and changes
        /// no classification outcome.
        /// </summary>
        internal static IReadOnlyList<string> NoAlphaChannelPropertiesFor(
            CapturedAnimationEvidence evidence,
            IReadOnlyList<int> admittedIndices)
        {
            var properties = new List<string>();
            var seen = new HashSet<string>();
            foreach (var index in admittedIndices)
            {
                foreach (var entry in evidence
                             .AdmittedMaterials[index]
                             .Evidence
                             .TextureAssignments)
                {
                    if (!entry.HasValue ||
                        entry.Value.Texture == null ||
                        (entry.Value.RequestedEvidence &
                            TextureEvidenceKinds.AlphaChannel) == 0 ||
                        !entry.Value.Texture.SampledAlphaIsProvenOne ||
                        seen.Contains(entry.Name))
                    {
                        continue;
                    }

                    seen.Add(entry.Name);
                    properties.Add(entry.Name);
                }
            }

            return properties;
        }

        /// <summary>
        /// Collects one slot's named capture refusals from its admitted
        /// materials' evidence, in the slot's admitted order, with duplicates
        /// collapsed: the same texture source can serve two admitted materials
        /// of one slot, and a refusal is a fact about the capture, not about
        /// the material that hit it. The records name the texture and the
        /// reason family for the report; they refuse nothing by themselves.
        /// </summary>
        internal static IReadOnlyList<TextureCaptureRefusal> CaptureRefusalsFor(
            CapturedAnimationEvidence evidence,
            IReadOnlyList<int> admittedIndices)
        {
            var refusals = new List<TextureCaptureRefusal>();
            var seen = new HashSet<
                (string Property,
                 bool HasSource,
                 string Source,
                 TextureChannel Channel,
                 TextureCaptureRefusalReason Reason)>();
            foreach (var index in admittedIndices)
            {
                foreach (var refusal in evidence
                             .AdmittedMaterials[index]
                             .Evidence
                             .CaptureRefusals)
                {
                    var key = (
                        refusal.PropertyName,
                        refusal.HasSourceIdentity,
                        refusal.HasSourceIdentity
                            ? refusal.SourceIdentity.Value
                            : null,
                        refusal.Channel,
                        refusal.Reason);
                    if (seen.Add(key))
                    {
                        refusals.Add(refusal);
                    }
                }
            }

            return refusals;
        }

        /// <summary>
        /// The stored-value mapping half of the pass's mip-cap policy: a
        /// stored -1 (All Mips) means no cap.
        /// </summary>
        internal static int ProofMipCapFrom(int storedMaxMipLevel)
        {
            return storedMaxMipLevel < 0 ? int.MaxValue : storedMaxMipLevel;
        }

        /// <summary>
        /// The stored-value mapping half of the pass's minimum-texture-size
        /// policy: a stored -1 (All Sizes) means 1, which every level
        /// satisfies.
        /// </summary>
        internal static int MinTextureSizeFrom(int storedMinTextureSize)
        {
            return storedMinTextureSize < 1 ? 1 : storedMinTextureSize;
        }

        /// <summary>
        /// The stored-value mapping half of the pass's minimum-opaque-
        /// coverage policy: a stored value below zero is a defect against
        /// the Range attribute and reads as 0.
        /// </summary>
        internal static int MinimumCoverageFrom(int storedCoveragePercent)
        {
            return storedCoveragePercent < 0 ? 0 : storedCoveragePercent;
        }

        /// <summary>
        /// The stored-value mapping half of the pass's preserve-coverage
        /// policy: below zero reads as 0, above 100 clamps to 100.
        /// </summary>
        internal static int MinimumPreserveCoverageFrom(
            int storedPreservePercent)
        {
            return storedPreservePercent < 0 ? 0
                : storedPreservePercent > 100 ? 100 : storedPreservePercent;
        }

        /// <summary>
        /// The stored-value mapping half of the pass's opaque-percent
        /// policy: a clamp at or below zero reads as the inert exact-255
        /// contract, and anything above 100 clamps to 100.
        /// </summary>
        internal static int OpaquePercentFrom(int storedOpaqueAlphaPercent)
        {
            if (storedOpaqueAlphaPercent <= 0)
            {
                // A fully transparent texel can never be opaque
                // evidence, so a clamp at or below zero admits
                // invisible texels and reads as the inert exact-255
                // contract instead.
                return 100;
            }

            return storedOpaqueAlphaPercent > 100
                ? 100 : storedOpaqueAlphaPercent;
        }

        /// <summary>
        /// The stored-value mapping half of the pass's polygon-clamp
        /// policy: the 0 to 100 banding and the inert mapping of 100 to 0.
        /// </summary>
        internal static int PolygonClampPercentFrom(int storedClampPercent)
        {
            storedClampPercent =
                storedClampPercent < 0 ? 0
                : storedClampPercent > 100 ? 100 : storedClampPercent;
            if (storedClampPercent >= 100)
            {
                return 0;
            }

            return storedClampPercent;
        }

        /// <summary>
        /// The stored-value mapping half of the pass's density-cap policy:
        /// the coverage share's complement, with a below-zero value read as
        /// the inert 0 and an above-100 value rejected at wiring time.
        /// </summary>
        internal static int DensityCapFromCoveragePercent(
            int storedCoveragePercent)
        {
            if (storedCoveragePercent < 0)
            {
                return 0;
            }

            if (storedCoveragePercent > 100)
            {
                throw new InvalidOperationException(
                    "AMUSE Minimum Opaque Coverage to Convert (Per Polygon) is " +
                    "stored as " + storedCoveragePercent + ", outside the 0 to 100 " +
                    "range.");
            }

            return 100 - storedCoveragePercent;
        }
    }
}
