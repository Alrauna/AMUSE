using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// Selects the supported shader family for one admitted material and the
    /// two requests that family answers with: what ordinary alpha proof may
    /// consider, and what the closed batch must gather. Selection attests
    /// nothing and captures nothing. The single
    /// <see cref="ClosedAlphaMaterialCapturer"/> call that follows is the sole
    /// material-evidence capture and the sole source-attestation decision for
    /// the whole admitted batch.
    /// <para>
    /// The two are separate because the capture schema also carries evidence
    /// no alpha proof depends on. Folding that into
    /// <paramref name="alphaRelevanceRequest"/> would make ordinary alpha
    /// analysis treat conversion-only render state as a proof input — a
    /// coverage regression, not a safety improvement.
    /// </para>
    /// </summary>
    internal delegate bool AlphaMaterialRequestSelector(
        Material material,
        out CapturedAlphaMaterialFamily family,
        out MaterialEvidenceRequest alphaRelevanceRequest,
        out MaterialEvidenceRequest captureRequest);

    /// <summary>
    /// Captures the closed evidence batch for one admitted material batch.
    /// <para>
    /// The positional order is part of this delegate's contract, not an
    /// accidental property of one implementation: on success,
    /// <paramref name="captured"/>[i] corresponds to <paramref name="materials"/>[i]
    /// and therefore also to <paramref name="families"/>[i], and
    /// <c>captured.Count</c> equals <c>materials.Count</c>.
    /// </para>
    /// <para>
    /// Returning <c>false</c> rejects the complete batch. A refusal exposes no
    /// partial captured result to the caller: <paramref name="captured"/> must
    /// be left null or empty, never a prefix of the batch.
    /// </para>
    /// <param name="bounds">
    /// The user's alpha policy as exact byte bounds. Capture applies them
    /// to every alpha field it gathers, so the proof's evidence is the
    /// policy's evidence. The inert bounds reproduce the base exact-255
    /// contract.
    /// </param>
    /// <param name="cutoutRawMipCap">
    /// The cutout arm's raw policy mip cap. The closed capture pins every
    /// shared bucket whose captured threshold sits below one to this cap.
    /// </param>
    /// <param name="exactRawMipCap">
    /// The exact arm's raw policy mip cap. The closed capture pins every
    /// bucket at the exact threshold to this cap.
    /// </param>
    /// </summary>
    internal delegate bool ClosedAlphaMaterialCapturer(
        IReadOnlyList<Material> materials,
        IReadOnlyList<CapturedAlphaMaterialFamily> families,
        MaterialEvidenceRequest request,
        AlphaPolicyBounds bounds,
        int cutoutRawMipCap,
        int exactRawMipCap,
        out ClosedAlphaCaptureOutcome outcome);

    /// <summary>
    /// The locked-identity selection pre-check. It answers, for one live
    /// material that selection could not attest, the named renderer refusal
    /// the recognized locked identity demands, or
    /// <see cref="RendererAnalysisRefusal.None"/> when the material stays on
    /// the generic path. The answer is recorded once, at capture time,
    /// because this is the only point where the live serialization is still
    /// readable. It reads the serialization only and mutates nothing.
    /// </summary>
    internal delegate RendererAnalysisRefusal LockedMaterialRefusalCheck(
        Material material);

    internal static class UnityAnimationEvidenceCapture
    {
        private const string MaterialPrefix = "material.";

        /// <summary>
        /// Unity's generated name for a texture property's packed scale and
        /// offset. Shared so the derivation below and its inverse in
        /// admitted-state resolution cannot drift apart.
        /// </summary>
        internal const string TextureScaleOffsetSuffix = "_ST";

        /// <summary>
        /// The proof-relevance resolver as a value: the same implementation
        /// as <see cref="ResolveProofRelevant"/>, in the delegate shape the
        /// orchestrators inject into
        /// <see cref="Analysis.RuntimeStateResolution.ResolveRuntimeStates"/>.
        /// </summary>
        internal static readonly TryResolveProofRelevant ProofRelevantResolver =
            ResolveProofRelevant;

        /// <summary>
        /// The unrecognized object-binding recognizer as a value: the same
        /// implementation as <see cref="IsUnrecognizedObjectMaterialBinding"/>,
        /// in the delegate shape the orchestrators inject into
        /// <see cref="Analysis.RuntimeStateResolution.ResolveRuntimeStates"/>.
        /// </summary>
        internal static readonly UnrecognizedObjectMaterialBindingCheck
            UnrecognizedObjectBinding = IsUnrecognizedObjectMaterialBinding;

        /// <summary>
        /// The material-slot binding parse as a value: the same
        /// implementation as <see cref="TryParseMaterialSlotBindingFor"/>,
        /// in the delegate shape the orchestrators inject into
        /// <see cref="Analysis.RuntimeStateResolution.MaterialSlotsFor"/>.
        /// </summary>
        internal static readonly TryParseMaterialSlotBindingFor
            MaterialSlotBindingParser = TryParseMaterialSlotBindingFor;

        private static readonly MaterialEvidenceRequest EmptyRequest =
            new MaterialEvidenceRequest(
                false,
                false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<TexturePropertyEvidenceRequest>());

        private static ClosedAlphaMaterialCapturer DefaultCapturer(
            RegisteredSourceLookup resolveRegisteredSource)
        {
            return (IReadOnlyList<Material> batchMaterials,
                IReadOnlyList<CapturedAlphaMaterialFamily> batchFamilies,
                MaterialEvidenceRequest batchRequest,
                AlphaPolicyBounds batchBounds,
                int batchCutoutRawMipCap,
                int batchExactRawMipCap,
                out ClosedAlphaCaptureOutcome batchOutcome) =>
                UnityMaterialSemantics.TryCaptureClosedAlphaMaterials(
                    batchMaterials, batchFamilies, batchRequest,
                    batchBounds, out batchOutcome,
                    resolveRegisteredSource,
                    batchCutoutRawMipCap, batchExactRawMipCap);
        }

        /// <summary>
        /// Captures animation evidence for one renderer. <paramref name="rendererPath"/>
        /// is that renderer's Unity animation path and scopes material-slot
        /// closure to it: a renderer on the avatar root has the empty path, so
        /// empty is valid and only null is rejected.
        /// </summary>
        /// <param name="admittedLiveMaterials">
        /// The live build-copy materials behind the returned evidence's
        /// <see cref="CapturedAnimationEvidence.AdmittedMaterials"/>, in the same
        /// order, so index <c>i</c> of one addresses index <c>i</c> of the other.
        /// <para>
        /// This is a live, transient host capability, NOT proof evidence: it is
        /// deliberately handed back beside the immutable evidence rather than
        /// stored in it, so the evidence graph's no-live-Unity-object guarantee is
        /// unchanged. A renderer-wide closure failure yields an empty list,
        /// never a partial one. A slot-scoped closure failure keeps the
        /// pairing: the renderer is closed, and the failed slot's materials
        /// ride the list only so the live pairing stays positional for the
        /// slots that still resolve.
        /// </para>
        /// </param>
        internal static CapturedAnimationEvidence Capture(
            string rendererPath,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings,
            AlphaPolicyBounds bounds,
            out IReadOnlyList<Material> admittedLiveMaterials,
            ClosedAlphaMaterialCapturer capturer = null,
            bool ignoreOutOfRangeSlots = false,
            string rendererTypeName = null,
            LockedMaterialRefusalCheck lockedRefusalCheck = null,
            Func<Material, Material> observedMaterialMapper = null,
            RegisteredSourceLookup resolveRegisteredSource = null,
            int cutoutRawMipCap = int.MaxValue,
            int exactRawMipCap = int.MaxValue)
        {
            ClosedAlphaMaterialCapturer effectiveCapturer = capturer ??
                DefaultCapturer(resolveRegisteredSource);
            return CaptureGraph(
                rendererPath,
                currentSlots,
                graph,
                bindings,
                bounds,
                UnityMaterialSemantics.TrySelectAlphaMaterialRequests,
                effectiveCapturer,
                out admittedLiveMaterials,
                ignoreOutOfRangeSlots,
                rendererTypeName,
                lockedRefusalCheck,
                observedMaterialMapper,
                resolveRegisteredSource,
                cutoutRawMipCap,
                exactRawMipCap);
        }
        // The seam-parameterized graph capture. The barrier's fixture
        // route uses it when a selector seam is injected and no animation
        // index is active. The friend test assembly uses it the same way.
        internal static CapturedAnimationEvidence CaptureGraphThroughSeams(
            string rendererPath,
            IReadOnlyList<Material> currentSlots,
            IReadOnlyList<LiveClipObservation> observations,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings,
            AlphaPolicyBounds bounds,
            AlphaMaterialRequestSelector selectRequest,
            ClosedAlphaMaterialCapturer capturer,
            out IReadOnlyList<Material> admittedLiveMaterials,
            bool ignoreOutOfRangeSlots = false,
            string rendererTypeName = null,
            LockedMaterialRefusalCheck lockedRefusalCheck = null,
            Func<Material, Material> observedMaterialMapper = null,
            RegisteredSourceLookup resolveRegisteredSource = null,
            int cutoutRawMipCap = int.MaxValue,
            int exactRawMipCap = int.MaxValue)
        {
            var effectiveSelectRequest = selectRequest ??
                UnityMaterialSemantics.TrySelectAlphaMaterialRequests;
            var effectiveCapturer = capturer ??
                DefaultCapturer(resolveRegisteredSource);

            if (observations != null)
            {
                return CaptureObserved(
                    rendererPath,
                    observations,
                    currentSlots,
                    graph,
                    bounds,
                    effectiveSelectRequest,
                    effectiveCapturer,
                    out admittedLiveMaterials,
                    ignoreOutOfRangeSlots,
                    rendererTypeName,
                    lockedRefusalCheck,
                    observedMaterialMapper,
                    resolveRegisteredSource,
                    cutoutRawMipCap,
                    exactRawMipCap);
            }

            return CaptureGraph(
                rendererPath,
                currentSlots,
                graph,
                bindings,
                bounds,
                effectiveSelectRequest,
                effectiveCapturer,
                out admittedLiveMaterials,
                ignoreOutOfRangeSlots,
                rendererTypeName,
                lockedRefusalCheck,
                observedMaterialMapper,
                resolveRegisteredSource,
                cutoutRawMipCap,
                exactRawMipCap);
        }

        /// <summary>
        /// The production capture for the barrier's single-scope lifecycle:
        /// observations come from the committed-graph walk plus every clip
        /// the AnimationIndex holds for this renderer's object path, so the
        /// admitted swap set matches what the apply pass validates against.
        /// The graph still decides the structural facts; the index only
        /// adds clip observations for this renderer path. The two views
        /// can disagree on values: the stored graph was enumerated before
        /// the build substituted any material, while the index holds the
        /// live clips. The optional observed-material mapper folds such a
        /// substitution into the admitted set, so both views resolve to
        /// the same material per binding; null keeps every observed value
        /// as-is.
        /// </summary>
        internal static CapturedAnimationEvidence CaptureWithAnimationIndex(
            string rendererPath,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings,
            AnimationIndex animationIndex,
            AlphaPolicyBounds bounds,
            out IReadOnlyList<Material> admittedLiveMaterials,
            AlphaMaterialRequestSelector selectRequest = null,
            ClosedAlphaMaterialCapturer capturer = null,
            bool ignoreOutOfRangeSlots = false,
            string rendererTypeName = null,
            LockedMaterialRefusalCheck lockedRefusalCheck = null,
            Func<Material, Material> observedMaterialMapper = null,
            RegisteredSourceLookup resolveRegisteredSource = null,
            int cutoutRawMipCap = int.MaxValue,
            int exactRawMipCap = int.MaxValue)
        {
            if (rendererPath == null)
                throw new ArgumentNullException(nameof(rendererPath));
            if (currentSlots == null)
                throw new ArgumentNullException(nameof(currentSlots));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (animationIndex == null)
                throw new ArgumentNullException(nameof(animationIndex));
            if (graph.Refusal != AvatarAnimationRefusal.None)
            {
                throw new InvalidOperationException(
                    "Animation evidence capture requires a successful committed graph.");
            }

            var observations = new List<LiveClipObservation>();
            foreach (var layer in graph.Layers)
            {
                foreach (var clip in layer.Clips)
                {
                    observations.Add(LiveAnimationObservation.ObserveClip(
                        clip,
                        bindings.IsSpecialMotion(clip),
                        layer.BlendingMode ==
                        AnimatorLayerBlendingMode.Additive));
                }
            }

            // ponytail: index occurrences marked false; the committed walk is the provenance authority; build real virtual provenance if a capture route stops deriving from the committed controllers
            foreach (var virtualClipObj in animationIndex
                         .GetClipsForObjectPath(rendererPath))
            {
                var virtualClip =
                    (nadena.dev.ndmf.animator.VirtualClip)virtualClipObj;
                observations.Add(LiveAnimationObservation.ObserveVirtualClip(
                    virtualClip, virtualClip.IsMarkerClip, false));
            }

            ClosedAlphaMaterialCapturer effectiveCapturer = capturer ??
                DefaultCapturer(resolveRegisteredSource);

            return CaptureObserved(
                rendererPath,
                observations,
                currentSlots,
                graph,
                bounds,
                selectRequest ?? UnityMaterialSemantics.TrySelectAlphaMaterialRequests,
                effectiveCapturer,
                out admittedLiveMaterials,
                ignoreOutOfRangeSlots,
                rendererTypeName,
                lockedRefusalCheck,
                observedMaterialMapper,
                resolveRegisteredSource,
                cutoutRawMipCap,
                exactRawMipCap);
        }
        private static CapturedAnimationEvidence CaptureGraph(
            string rendererPath,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            IPlatformAnimatorBindings bindings,
            AlphaPolicyBounds bounds,
            AlphaMaterialRequestSelector selectRequest,
            ClosedAlphaMaterialCapturer capturer,
            out IReadOnlyList<Material> admittedLiveMaterials,
            bool ignoreOutOfRangeSlots = false,
            string rendererTypeName = null,
            LockedMaterialRefusalCheck lockedRefusalCheck = null,
            Func<Material, Material> observedMaterialMapper = null,
            RegisteredSourceLookup resolveRegisteredSource = null,
            int cutoutRawMipCap = int.MaxValue,
            int exactRawMipCap = int.MaxValue)
        {
            // Empty is the avatar root's animation path and is valid; only an
            // absent path is a caller defect.
            if (rendererPath == null)
                throw new ArgumentNullException(nameof(rendererPath));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (graph.Refusal != AvatarAnimationRefusal.None)
            {
                throw new InvalidOperationException(
                    "Animation evidence capture requires a successful committed graph.");
            }

            var observations = new List<LiveClipObservation>();
            foreach (var layer in graph.Layers)
            {
                foreach (var clip in layer.Clips)
                {
                    observations.Add(LiveAnimationObservation.ObserveClip(
                        clip,
                        bindings.IsSpecialMotion(clip),
                        layer.BlendingMode ==
                        AnimatorLayerBlendingMode.Additive));
                }
            }

            return CaptureObserved(
                rendererPath,
                observations,
                currentSlots,
                graph,
                bounds,
                selectRequest,
                capturer,
                out admittedLiveMaterials,
                ignoreOutOfRangeSlots,
                rendererTypeName,
                lockedRefusalCheck,
                observedMaterialMapper,
                resolveRegisteredSource,
                cutoutRawMipCap,
                exactRawMipCap);
        }
        private static CapturedAnimationEvidence CaptureObserved(
            string rendererPath,
            IReadOnlyList<LiveClipObservation> observations,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph,
            AlphaPolicyBounds bounds,
            AlphaMaterialRequestSelector selectRequest,
            ClosedAlphaMaterialCapturer capturer,
            out IReadOnlyList<Material> admittedLiveMaterials,
            bool ignoreOutOfRangeSlots = false,
            string rendererTypeName = null,
            LockedMaterialRefusalCheck lockedRefusalCheck = null,
            Func<Material, Material> observedMaterialMapper = null,
            RegisteredSourceLookup resolveRegisteredSource = null,
            int cutoutRawMipCap = int.MaxValue,
            int exactRawMipCap = int.MaxValue)
        {
            // Assigned once here so that the renderer-wide closure-failure
            // return hands back an empty list rather than a partial one. The
            // real pairing is assigned only at the single success return,
            // after the captured count has been checked against the admitted
            // count. A slot-scoped closure failure also returns through that
            // success path, so its pairing stays positional for the slots
            // that still resolve.
            admittedLiveMaterials = Array.Empty<Material>();

            if (rendererPath == null)
                throw new ArgumentNullException(nameof(rendererPath));
            if (observations == null)
                throw new ArgumentNullException(nameof(observations));
            if (currentSlots == null)
                throw new ArgumentNullException(nameof(currentSlots));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (selectRequest == null)
                throw new ArgumentNullException(nameof(selectRequest));
            if (capturer == null) throw new ArgumentNullException(nameof(capturer));
            if (graph.Refusal != AvatarAnimationRefusal.None)
                throw new InvalidOperationException(
                    "Animation evidence capture requires a successful committed graph.");

            var hasUnnormalizedDirectBlendTree = false;
            foreach (var layer in graph.Layers)
            {
                hasUnnormalizedDirectBlendTree |=
                    layer.HasUnnormalizedDirectBlendTree;
            }

            var ignoredOutOfRangeSlots = new HashSet<int>();

            // The slot-scoped closure failures. A failure here is a fact
            // about exactly one slot, so the renderer closes with every
            // other slot's evidence intact; the failed slot alone refuses.
            // The set guards one record per slot, the list keeps first-seen
            // order, and the evidence record sorts it.
            var slotClosureFailures = new List<SlotClosureFailure>();
            var closureFailedSlots = new HashSet<int>();

            void RecordSlotClosureFailure(
                int slot,
                MaterialDependencyClosureFailure failure)
            {
                if (closureFailedSlots.Add(slot))
                {
                    slotClosureFailures.Add(new SlotClosureFailure(
                        slot, failure));
                }
            }

            var admitted = new List<Material>();
            var materialIndices = new Dictionary<Material, int>(
                ReferenceEqualityComparer<Material>.Instance);

            // The observed-material view. A caller whose build substituted
            // materials between the stored committed-graph enumeration and
            // this capture hands in the substitution here, so a keyframe
            // value read from the pre-substitution graph resolves to the
            // same material the live clip view and the slot arrays hold.
            // Null keeps the observed value as-is.
            Material View(Material material)
            {
                if (material == null)
                {
                    return null;
                }

                return observedMaterialMapper?.Invoke(material) ?? material;
            }

            bool TryAdmit(Material material, out int index)
            {
                index = default;
                if (material == null) return false;
                var viewed = View(material);
                if (materialIndices.TryGetValue(viewed, out index)) return true;
                index = admitted.Count;
                materialIndices.Add(viewed, index);
                admitted.Add(viewed);
                return true;
            }

            var currentMaterialIndices = new int[currentSlots.Count];
            for (var slot = 0; slot < currentSlots.Count; slot++)
            {
                if (!TryAdmit(currentSlots[slot], out currentMaterialIndices[slot]))
                {
                    // An unassigned current slot is a fact about this slot
                    // alone. Record the sentinel, refuse the slot at
                    // resolution, and keep every sibling slot's evidence.
                    currentMaterialIndices[slot] = -1;
                    RecordSlotClosureFailure(
                        slot,
                        MaterialDependencyClosureFailure
                            .MissingCurrentMaterial);
                }
            }

            foreach (var observation in observations)
            {
                if (observation == null)
                    throw new ArgumentException(
                        "Clip observations cannot contain null.",
                        nameof(observations));

                foreach (var binding in observation.Objects)
                {
                    if (!LiveAnimationObservation.TryParseMaterialSlotBinding(
                            binding.PropertyName, out var slot) ||
                        !AddressesAnalyzedRenderer(
                            binding.Path, binding.TypeName, rendererPath, rendererTypeName))
                    {
                        continue;
                    }

                    if (slot >= currentSlots.Count)
                    {
                        if (ignoreOutOfRangeSlots)
                        {
                            ignoredOutOfRangeSlots.Add(slot);
                            continue;
                        }

                        // A binding for a slot the renderer does not have
                        // is a fact about that binding's index alone. The
                        // index is recorded so reporting can name it and so
                        // the apply pass can refuse an appended slot that
                        // would land on it. None of the binding's values is
                        // admitted: the index addresses no real slot, so
                        // admitting its materials would widen a set no slot
                        // can reach.
                        RecordSlotClosureFailure(
                            slot,
                            MaterialDependencyClosureFailure.SlotOutOfRange);
                        continue;
                    }

                    // A null or non-material keyframe value is a fact about
                    // this binding's slot alone. The binding is skipped
                    // whole: admitting a prefix of its values would describe
                    // a swap the clip does not perform. Every other slot's
                    // reachable set is unchanged, because a material slot
                    // reaches materials only through its own current
                    // assignment and its own bindings.
                    var bindingValuesAreMaterials = true;
                    foreach (var value in binding.Values)
                    {
                        if (!(value is Material))
                        {
                            bindingValuesAreMaterials = false;
                            break;
                        }
                    }

                    if (!bindingValuesAreMaterials)
                    {
                        RecordSlotClosureFailure(
                            slot,
                            MaterialDependencyClosureFailure
                                .InvalidSwapValue);
                        continue;
                    }

                    foreach (var value in binding.Values)
                    {
                        if (!TryAdmit((Material)value, out _))
                        {
                            throw new InvalidOperationException(
                                "A validated material-slot value failed " +
                                "admission.");
                        }
                    }
                }
            }

            // Request selection is per-material, and so is its failure. A
            // material no family selects is unattested, and unattestation is
            // a fact about the slots that can hold that material - never
            // about the renderer. Unattested materials stay admitted, so
            // their indices keep addressing slot evidence, but they join no
            // capture batch and become the Unsupported sentinel, which slot
            // resolution refuses as semantics-unknown. Every attested
            // material completes selection before the one capture runs, so
            // the two closed unions below still span a fully known batch.
            var capturedByIndex =
                new CapturedAlphaMaterial[admitted.Count];
            var attestedIndices = new List<int>(admitted.Count);
            var attestedMaterials = new List<Material>(admitted.Count);
            var attestedFamilies =
                new List<CapturedAlphaMaterialFamily>(admitted.Count);
            var alphaRequests =
                new List<MaterialEvidenceRequest>(admitted.Count);
            var captureRequests =
                new List<MaterialEvidenceRequest>(admitted.Count);
            for (var index = 0; index < admitted.Count; index++)
            {
                CapturedAlphaMaterialFamily family;
                MaterialEvidenceRequest alphaRequest;
                MaterialEvidenceRequest materialCaptureRequest;
                if (!selectRequest(
                        admitted[index],
                        out family,
                        out alphaRequest,
                        out materialCaptureRequest) ||
                    alphaRequest == null ||
                    materialCaptureRequest == null)
                {
                    // The locked-identity pre-check answers once, here,
                    // while the live serialization is still readable. The
                    // answer rides on the sentinel: slot resolution reads
                    // the recorded fact and refuses by name instead of the
                    // generic destination.
                    var lockedRefusal = lockedRefusalCheck?.Invoke(
                        admitted[index])
                        ?? RendererAnalysisRefusal.None;
                    capturedByIndex[index] =
                        UnityMaterialSemantics.UnattestedMaterial(
                            lockedRefusal,
                            admitted[index],
                            resolveRegisteredSource);
                    continue;
                }

                capturedByIndex[index] = null;
                attestedIndices.Add(index);
                attestedMaterials.Add(admitted[index]);
                attestedFamilies.Add(family);
                alphaRequests.Add(alphaRequest);
                captureRequests.Add(materialCaptureRequest);
            }

            // Two closed unions over the attested batch. The capture schema
            // is what the one capture gathers; the alpha request is what
            // ordinary alpha proof may consider afterwards. Only the latter
            // is retained, because no consumer reads the broader schema once
            // the evidence it authorized has been captured. An unattested
            // material contributes no request: its slots refuse, and no
            // alpha proof of another material may depend on it.
            var alphaRelevanceRequest = attestedIndices.Count == 0
                ? EmptyRequest
                : MaterialEvidenceRequest.Combine(
                    alphaRequests.ToArray());
            var captureRequest = attestedIndices.Count == 0
                ? EmptyRequest
                : MaterialEvidenceRequest.Combine(
                    captureRequests.ToArray());

            if (attestedIndices.Count > 0)
            {
                // The closed batch capture is the sole source-attestation
                // decision for the materials it receives. Its outcome is
                // per-material: a failed member's slot receives the same
                // Unsupported sentinel a selection miss receives, so only
                // that slot refuses, and every survivor is captured exactly
                // as a fully attested batch would capture it.
                if (!capturer(
                        attestedMaterials,
                        attestedFamilies,
                        captureRequest,
                        bounds,
                        cutoutRawMipCap,
                        exactRawMipCap,
                        out var batchOutcome))
                {
                    throw new InvalidOperationException(
                        "Closed material capture failed without naming a " +
                        "material.");
                }
                if (batchOutcome.Captured.Count
                        + batchOutcome.UnattestedOrdinals.Count
                    != attestedIndices.Count)
                {
                    throw new InvalidOperationException(
                        "Closed material capture returned an invalid result " +
                        "count.");
                }
                var failedOrdinals = new HashSet<int>(batchOutcome.UnattestedOrdinals);
                var survivorOrdinal = 0;
                for (var index = 0; index < attestedIndices.Count; index++)
                {
                    var admittedIndex = attestedIndices[index];
                    if (failedOrdinals.Contains(index))
                    {
                        var lockedRefusal = lockedRefusalCheck?.Invoke(
                            admitted[admittedIndex]) ?? RendererAnalysisRefusal.None;
                        capturedByIndex[admittedIndex] =
                            UnityMaterialSemantics.UnattestedMaterial(
                                lockedRefusal,
                                admitted[admittedIndex],
                                resolveRegisteredSource);
                    }
                    else
                    {
                        capturedByIndex[admittedIndex] =
                            batchOutcome.Captured[survivorOrdinal++];
                    }
                }
            }

            var clips = new List<CapturedClipEvidence>(observations.Count);
            foreach (var observation in observations)
            {
                var floats = new List<CapturedFloatBinding>(
                    observation.Floats.Count);
                foreach (var binding in observation.Floats)
                {
                    floats.Add(new CapturedFloatBinding(
                        binding.Path,
                        binding.TypeName,
                        binding.PropertyName,
                        binding.IsFiniteExact,
                        new List<float>(binding.Values)));
                }

                var objects = new List<CapturedObjectBinding>(
                    observation.Objects.Count);
                foreach (var binding in observation.Objects)
                {
                    var indices = new List<int>();
                    if (LiveAnimationObservation.TryParseMaterialSlotBinding(
                            binding.PropertyName, out var slot))
                    {
                        // Same condition as admission above, so the immutable
                        // copy can never disagree with what was admitted. A
                        // foreign slot binding is another renderer's evidence:
                        // it is dropped outright rather than retained with an
                        // empty index list, which would read as "this renderer
                        // has a swap that admits nothing". A binding whose
                        // slot carries a closure failure is dropped for the
                        // same reason: none of its values was admitted, so a
                        // retained binding would promise values the evidence
                        // does not hold.
                        if (!AddressesAnalyzedRenderer(
                                binding.Path, binding.TypeName, rendererPath, rendererTypeName) ||
                            (slot >= currentSlots.Count && ignoreOutOfRangeSlots) ||
                            closureFailedSlots.Contains(slot))
                        {
                            continue;
                        }

                        foreach (var value in binding.Values)
                        {
                            indices.Add(materialIndices[View((Material)value)]);
                        }
                    }

                    objects.Add(new CapturedObjectBinding(
                        binding.Path,
                        binding.TypeName,
                        binding.PropertyName,
                        indices));
                }

                clips.Add(new CapturedClipEvidence(
                    observation.Name,
                    observation.IsSpecialMotion,
                    observation.IsFromAdditiveLayer,
                    floats,
                    objects));
            }

            // The success return, and the only place the live pairing escapes.
            // This list preserves the full admitted-material order - sentinels
            // included - so index i of it addresses index i of AdmittedMaterials
            // and of every slot's admitted index list. The capturer's own
            // positional contract placed its results against the attested
            // subset above; that mapping is already folded into
            // capturedByIndex.
            admittedLiveMaterials = Array.AsReadOnly(admitted.ToArray());

            var orderedIgnored = new List<int>(ignoredOutOfRangeSlots);
            orderedIgnored.Sort();

            return new CapturedAnimationEvidence(
                alphaRelevanceRequest,
                clips,
                capturedByIndex,
                currentMaterialIndices,
                hasUnnormalizedDirectBlendTree,
                orderedIgnored,
                slotClosureFailures);
        }

        internal static bool IsCompatibleRendererType(
            string bindingTypeName,
            string rendererTypeName) =>
            AnimationBindingResolution.IsCompatibleRendererType(
                bindingTypeName, rendererTypeName);

        internal static bool TryParseMaterialSlotBindingFor(
            string bindingPath,
            string bindingTypeFullName,
            string propertyName,
            string rendererPath,
            string rendererTypeFullName,
            out int slotIndex) =>
            AnimationBindingResolution.TryParseMaterialSlotBindingFor(
                bindingPath, bindingTypeFullName, propertyName,
                rendererPath, rendererTypeFullName, out slotIndex);

        internal static IReadOnlyCollection<string>
            DeriveTextureScaleOffsetProperties(MaterialEvidenceRequest relevance) =>
            AnimationBindingResolution.DeriveTextureScaleOffsetProperties(relevance);

        internal static ProofRelevantBindingResolution ResolveProofRelevant(
            CapturedFloatBinding binding,
            string rendererPath,
            MaterialEvidenceRequest relevance,
            out AnimatedPropertyRef reference,
            string rendererTypeName = null) =>
            AnimationBindingResolution.ResolveProofRelevant(
                binding, rendererPath, relevance, out reference, rendererTypeName);

        internal static bool IsUnrecognizedObjectMaterialBinding(
            CapturedObjectBinding binding,
            string rendererPath,
            MaterialEvidenceRequest relevance,
            string rendererTypeName = null) =>
            AnimationBindingResolution.IsUnrecognizedObjectMaterialBinding(
                binding, rendererPath, relevance, rendererTypeName);

        private static bool AddressesAnalyzedRenderer(
            string bindingPath,
            string bindingTypeName,
            string rendererPath,
            string rendererTypeName) =>
            AnimationBindingResolution.AddressesAnalyzedRenderer(
                bindingPath, bindingTypeName, rendererPath, rendererTypeName);

    }
}
