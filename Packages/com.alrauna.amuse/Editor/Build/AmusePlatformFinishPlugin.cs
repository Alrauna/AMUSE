using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

[assembly: ExportsPlugin(typeof(Alrauna.Amuse.Editor.Build.AmusePlatformFinishPlugin))]

namespace Alrauna.Amuse.Editor.Build
{
    internal sealed class AmusePlatformFinishState
    {
        internal bool HasExecuted { get; set; }
        internal HostLifecycleCapability Lifecycle { get; set; }
        internal int AnalyzedRendererCount { get; set; }
        internal int OpaqueCandidateTriangleCount { get; set; }

        // Private setter, unlike the counters above: this total must stay in
        // lockstep with the per-reason buckets, so RecordRendererRefusal is its
        // only writer.
        internal int SemanticallyRefusedRendererCount { get; private set; }

        private readonly int[] rendererRefusals =
            new int[Enum.GetValues(typeof(RendererAnalysisRefusal)).Length];

        /// <summary>
        /// How many renderers were refused for this exact reason.
        /// <see cref="RendererAnalysisRefusal.None"/> is not a refusal and never
        /// becomes a bucket, because <see cref="RecordRendererRefusal"/> is the
        /// only writer and rejects it outright.
        /// </summary>
        internal int RendererRefusalCount(RendererAnalysisRefusal reason)
        {
            return rendererRefusals[(int)reason];
        }

        /// <summary>
        /// Records one renderer-scoped refusal. The per-reason buckets and
        /// <see cref="SemanticallyRefusedRendererCount"/> are advanced together
        /// here so they cannot drift apart at a call site.
        /// </summary>
        internal void RecordRendererRefusal(RendererAnalysisRefusal reason)
        {
            if (reason == RendererAnalysisRefusal.None)
            {
                throw new ArgumentException(
                    "RendererAnalysisRefusal.None is not a refusal.",
                    nameof(reason));
            }

            rendererRefusals[(int)reason]++;
            SemanticallyRefusedRendererCount++;
        }

        /// <summary>
        /// The avatar-scoped animation refusal, or
        /// <see cref="AvatarAnimationRefusal.None"/> when the committed controller
        /// graph was enumerated and admitted. An avatar-scoped refusal stops the
        /// whole avatar: no renderer is analyzed and no partial count survives.
        /// </summary>
        internal AvatarAnimationRefusal AvatarRefusal { get; set; }

        /// <summary>
        /// The host's own animator bindings, retained by
        /// <see cref="AmuseAnimatorBindingsCapture"/> while
        /// <see cref="AnimatorServicesContext"/> was active so that the
        /// extension-free barrier can still reach them.
        ///
        /// This is a live, transient host capability, NOT proof evidence: it is not
        /// part of the immutable captured-evidence graph and is deliberately outside
        /// that graph's no-live-Unity-object guarantee.
        /// </summary>
        internal IPlatformAnimatorBindings AnimatorBindings { get; set; }
    }

    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal sealed class AmusePlatformFinishPlugin : Plugin<AmusePlatformFinishPlugin>
    {
        internal const string PluginQualifiedName = "com.alrauna.amuse";
        internal const string BindingsCapturePassName =
            "AMUSE animator bindings capture";
        internal const string BarrierPassName = "AMUSE semantic barrier";

        public override string QualifiedName => PluginQualifiedName;
        public override string DisplayName => "AMUSE";

        protected override void Configure()
        {
            var sequence = InPhase(BuildPhase.PlatformFinish);

            // Acquire the host bindings while the animator extension is active...
            sequence.WithRequiredExtension(
                typeof(AnimatorServicesContext),
                inner => inner.Run(
                    BindingsCapturePassName, AmuseAnimatorBindingsCapture.Execute));

            // ...then analyze with no extension declared, so NDMF has deactivated
            // and committed the animator graph before the barrier observes it.
            sequence.Run(BarrierPassName, AmusePlatformFinishPass.Execute);
        }
    }

    internal static class AmusePlatformFinishPass
    {
        internal static void Execute(BuildContext context)
        {
            var state = PendingState(context);
            Execute(
                context,
                state,
                HostLifecycleCapability.CaptureAndEvaluate(context));
        }

        internal static void Execute(
            BuildContext context,
            HostLifecycleFacts facts)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            Execute(
                context,
                PendingState(context),
                HostLifecycleCapability.Evaluate(facts));
        }

        /// <summary>
        /// NDMF commits the virtualized controllers when
        /// <see cref="AnimatorServicesContext"/> deactivates, so a barrier running
        /// while that extension is still active would read pre-commit controller
        /// state. The barrier declares no extension precisely so that it does not;
        /// this asserts that its declaration was not lost, because the mistake is
        /// otherwise silent — it changes only which controllers are observed, never
        /// whether the pass appears to succeed.
        ///
        /// This is an implementation defect, not a domain refusal, and it reads
        /// build-context extension state only: it does not inspect the avatar, call
        /// <c>GetInnateControllers</c>, or mutate anything. That is why it may run
        /// before <see cref="HostLifecycleCapability"/> without weakening the
        /// unsupported-host stand-down boundary.
        /// </summary>
        private static void RequireAnimatorServicesContextInactive(
            BuildContext context)
        {
            try
            {
                context.Extension<AnimatorServicesContext>();
            }
            catch (Exception exception) when (IsInactiveExtensionSignal(exception))
            {
                return;
            }

            throw new InvalidOperationException(
                "AMUSE PlatformFinish barrier ran before AnimatorServicesContext " +
                "deactivation, so the committed controller graph is not yet " +
                "available. If barrier placement is in fact correct, NDMF has " +
                "changed its inactive-extension signal and " +
                nameof(IsInactiveExtensionSignal) + " needs updating.");
        }

        // BuildContext.Extension<T> signals an inactive extension with a plain
        // System.Exception carrying this exact message. Matching both pins the
        // signal narrowly, so any other failure raised while probing the lifecycle
        // propagates instead of being read as "inactive".
        private static bool IsInactiveExtensionSignal(Exception exception)
        {
            return exception.GetType() == typeof(Exception) &&
                   string.Equals(
                       exception.Message,
                       $"Extension {typeof(AnimatorServicesContext)} not active",
                       StringComparison.Ordinal);
        }

        private static AmusePlatformFinishState PendingState(
            BuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            RequireAnimatorServicesContextInactive(context);

            var state = context.GetState<AmusePlatformFinishState>();
            if (state.HasExecuted)
            {
                throw new InvalidOperationException("AMUSE PlatformFinish barrier executed more than once.");
            }

            return state;
        }

        private static void Execute(
            BuildContext context,
            AmusePlatformFinishState state,
            HostLifecycleCapability lifecycle)
        {
            state.Lifecycle = lifecycle;
            state.HasExecuted = true;
            if (!lifecycle.MayUsePositiveMutation)
            {
                return;
            }

            // Reaching positive lifecycle permission without the bindings the
            // capture pass retains is an integration defect in the caller, not a
            // domain refusal: nothing about the avatar has been observed yet.
            if (state.AnimatorBindings == null)
            {
                throw new InvalidOperationException(
                    "AMUSE PlatformFinish barrier reached positive lifecycle " +
                    "permission with no retained animator bindings.");
            }

            var graph = CommittedControllerGraph.Enumerate(
                context.AvatarRootObject, state.AnimatorBindings);
            if (graph.Refusal != AvatarAnimationRefusal.None)
            {
                // Avatar scope: the exact named cause is preserved and the whole
                // avatar stops. No renderer is analyzed, so no partial result and
                // no per-renderer accounting can survive.
                state.AvatarRefusal = graph.Refusal;
                return;
            }

            var hasCommittedClip = false;
            foreach (var layer in graph.Layers)
                hasCommittedClip |= layer.Clips.Count > 0;

            foreach (var renderer in context.AvatarRootObject
                         .GetComponentsInChildren<Renderer>(true))
            {
                RendererAnalysisRefusal refusal;
                int opaqueCandidateTriangleCount;
                if (hasCommittedClip)
                {
                    var evidence = UnityAnimationEvidenceCapture.Capture(
                        renderer.sharedMaterials,
                        graph,
                        state.AnimatorBindings);
                    (refusal, opaqueCandidateTriangleCount) =
                        AnalyzeRuntimeStates(
                            context.AvatarRootObject, renderer, evidence);
                }
                else
                {
                    var extraction = UnityRendererAlphaAnalysis.Capture(renderer);
                    var analysis = extraction.Refusal ==
                                   RendererAnalysisRefusal.None
                        ? UnityRendererAlphaAnalysis.Analyze(extraction.Snapshot)
                        : RendererAlphaAnalysis.Refused(extraction.Refusal);
                    refusal = analysis.Refusal;
                    opaqueCandidateTriangleCount =
                        analysis.Refusal == RendererAnalysisRefusal.None
                            ? analysis.Plan.OpaqueTriangleCount
                            : 0;
                }

                if (refusal != RendererAnalysisRefusal.None)
                {
                    // Renderer scope: this renderer stops and later renderers
                    // continue. Deliberately no try/catch anywhere in this loop —
                    // unsupported inputs already return a named refusal, so an
                    // exception here is an implementation defect and must reach
                    // NDMF as a build-blocking internal failure.
                    state.RecordRendererRefusal(refusal);
                    continue;
                }

                state.AnalyzedRendererCount++;
                state.OpaqueCandidateTriangleCount +=
                    opaqueCandidateTriangleCount;
            }
        }

        /// <summary>
        /// Exercises the PlatformFinish-owned runtime-state orchestration with
        /// already-closed evidence. The optional resolver is the pre-existing
        /// verified-frontend seam used by public-package tests whose stand-in
        /// shader cannot carry vendor source attestation; production calls the
        /// same core with the real resolver.
        /// </summary>
        internal static (RendererAnalysisRefusal Refusal,
                         int OpaqueCandidateTriangleCount)
            AnalyzeRuntimeStatesForTests(
                GameObject avatarRoot,
                Renderer renderer,
                CapturedAnimationEvidence evidence,
                CapturedAlphaMaterialSemanticsResolver resolveSemantics)
        {
            return AnalyzeRuntimeStates(
                avatarRoot, renderer, evidence, resolveSemantics);
        }

        private static (RendererAnalysisRefusal Refusal,
                        int OpaqueCandidateTriangleCount)
            AnalyzeRuntimeStates(
                GameObject avatarRoot,
                Renderer renderer,
                CapturedAnimationEvidence evidence,
                CapturedAlphaMaterialSemanticsResolver resolveSemantics = null)
        {
            if (avatarRoot == null)
                throw new ArgumentNullException(nameof(avatarRoot));
            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));
            if (evidence == null)
                throw new ArgumentNullException(nameof(evidence));
            if (!evidence.IsClosed)
            {
                return (
                    RendererAnalysisRefusal.UnrecognizedAnimatedMaterialBinding,
                    0);
            }

            var rendererPath = AnimationUtility.CalculateTransformPath(
                renderer.transform, avatarRoot.transform);

            var floats = new List<CapturedFloatBinding>();
            var objects = new List<CapturedObjectBinding>();
            foreach (var clip in evidence.Clips)
            {
                floats.AddRange(clip.FloatBindings);
                objects.AddRange(clip.ObjectBindings);
            }

            var structural = UnityRendererAlphaAnalysis.StructuralRefusalFor(
                floats, objects, rendererPath);
            if (structural != RendererAnalysisRefusal.None)
                return (structural, 0);

            var relevantBindings = new List<(
                CapturedFloatBinding Binding,
                AnimatedPropertyRef Reference)>();
            foreach (var binding in floats)
            {
                var resolution =
                    UnityAnimationEvidenceCapture.ResolveProofRelevant(
                        binding,
                        rendererPath,
                        evidence.RelevanceRequest,
                        out var reference);
                if (resolution ==
                    ProofRelevantBindingResolution.UnrecognizedMaterialBinding)
                {
                    return (
                        RendererAnalysisRefusal
                            .UnrecognizedAnimatedMaterialBinding,
                        0);
                }

                if (resolution == ProofRelevantBindingResolution.RendererWide)
                    relevantBindings.Add((binding, reference));
            }

            if (relevantBindings.Count > 0 && evidence.HasAdditiveLayer)
            {
                return (
                    RendererAnalysisRefusal
                        .AdditiveLayerWithProofRelevantMaterialProperty,
                    0);
            }

            if (relevantBindings.Count > 0 &&
                evidence.HasUnnormalizedDirectBlendTree)
            {
                return (
                    RendererAnalysisRefusal
                        .UnnormalizedDirectBlendTreeWithProofRelevantMaterialProperty,
                    0);
            }

            var slots = MaterialSlotsFor(evidence, rendererPath);
            var admittedCounts = new int[slots.Count];
            for (var slot = 0; slot < slots.Count; slot++)
                admittedCounts[slot] = slots[slot].AdmittedMaterialIndices.Count;
            if (!AdmittedMaterialStates.TryBudgetProduct(
                    admittedCounts, out _))
            {
                return (
                    RendererAnalysisRefusal.AdmittedStateBudgetExceeded,
                    0);
            }

            var fields = GatherAlphaFields(evidence.AdmittedMaterials);
            bool AlphaFields(
                TextureSourceId source,
                TextureChannel channel,
                out AlphaTextureData field)
            {
                field = null;
                return channel == TextureChannel.Alpha &&
                       fields.TryGetValue(source, out field);
            }

            var resolutionsBySlot = new IReadOnlyList<AlphaResolution>[slots.Count];
            for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                var resolved = AdmittedMaterialStates.ResolveSlot(
                    slots[slotIndex],
                    evidence.AdmittedMaterials,
                    relevantBindings,
                    evidence.RelevanceRequest,
                    AlphaFields,
                    resolveSemantics);
                if (!resolved.IsResolved)
                    return (resolved.Refusal, 0);

                resolutionsBySlot[slotIndex] =
                    AdmittedMaterialStates.DistinctResolutions(
                        resolved.Resolutions);
            }

            var extraction = UnityRendererAlphaAnalysis.Capture(renderer);
            if (extraction.Refusal != RendererAnalysisRefusal.None)
                return (extraction.Refusal, 0);

            var snapshot = extraction.Snapshot;
            var submeshes = new List<SubmeshSeparationInput>(
                snapshot.Submeshes.Count);
            foreach (var submesh in snapshot.Submeshes)
            {
                var perResolution = new List<TriangleAlphaOutcome[]>();
                foreach (var resolution in
                         resolutionsBySlot[submesh.MaterialSlotIndex])
                {
                    perResolution.Add(UnityRendererAlphaAnalysis.Classify(
                        submesh.Indices,
                        snapshot.Positions,
                        snapshot.HasUv0 ? snapshot.Uv0 : null,
                        resolution));
                }

                submeshes.Add(new SubmeshSeparationInput(
                    submesh.MaterialSlotIndex,
                    submesh.Indices,
                    UnityRendererAlphaAnalysis.IntersectOutcomes(perResolution)));
            }

            var plan = MeshSeparationPlanner.Create(
                new MeshSeparationInput(snapshot.VertexCount, submeshes));
            return (RendererAnalysisRefusal.None, plan.OpaqueTriangleCount);
        }

        private static IReadOnlyList<CapturedMaterialSlotEvidence>
            MaterialSlotsFor(
                CapturedAnimationEvidence evidence,
                string rendererPath)
        {
            var admittedBySlot = new List<int>[evidence.CurrentMaterialIndices.Count];
            for (var slot = 0; slot < admittedBySlot.Length; slot++)
            {
                admittedBySlot[slot] = new List<int>
                {
                    evidence.CurrentMaterialIndices[slot],
                };
            }

            foreach (var clip in evidence.Clips)
            {
                foreach (var binding in clip.ObjectBindings)
                {
                    if (!string.Equals(
                            binding.Path, rendererPath, StringComparison.Ordinal) ||
                        !LiveAnimationObservation.TryParseMaterialSlotBinding(
                            binding.PropertyName, out var slot))
                    {
                        continue;
                    }

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
                    slot, admittedBySlot[slot]);
            }

            return slots;
        }

        private static IReadOnlyDictionary<TextureSourceId, AlphaTextureData>
            GatherAlphaFields(IReadOnlyList<CapturedAlphaMaterial> materials)
        {
            var fields = new Dictionary<TextureSourceId, AlphaTextureData>();
            foreach (var material in materials)
            {
                foreach (var texture in material.Evidence.Textures)
                {
                    if (texture.HasSourceIdentity &&
                        texture.HasAlphaChannel &&
                        !fields.ContainsKey(texture.SourceIdentity))
                    {
                        fields.Add(texture.SourceIdentity, texture.AlphaChannel);
                    }
                }
            }

            return fields;
        }
    }
}
