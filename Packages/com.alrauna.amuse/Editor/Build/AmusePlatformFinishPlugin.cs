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

        /// <summary>How many renderers received at least one applied
        /// alpha-separation write.</summary>
        internal int AppliedRendererCount { get; set; }

        /// <summary>How many triangles were moved to proven-opaque rendering
        /// by applied writes.</summary>
        internal int AppliedOpaqueTriangleCount { get; set; }

        // Private setter, unlike the counters above: this total must stay in
        // lockstep with the per-reason buckets, so RecordRendererRefusal is its
        // only writer.
        internal int SemanticallyRefusedRendererCount { get; private set; }

        private readonly int[] rendererRefusals =
            new int[Enum.GetValues(typeof(RendererAnalysisRefusal)).Length];

        private readonly int[] slotRefusals =
            new int[Enum.GetValues(typeof(AlphaSeparationSlotRefusal)).Length];

        /// <summary>
        /// How many candidate slots were refused for this exact feature
        /// reason. <see cref="AlphaSeparationSlotRefusal.None"/> is not a
        /// refusal and never becomes a bucket, because
        /// <see cref="RecordSlotRefusal"/> is the only writer and rejects it
        /// outright.
        /// </summary>
        internal int SlotRefusalCount(AlphaSeparationSlotRefusal reason)
        {
            return slotRefusals[(int)reason];
        }

        /// <summary>
        /// Records one alpha-separation slot refusal. Slot-scoped members
        /// describe one failing slot; renderer-scoped members are recorded
        /// once per candidate slot they drop, so the buckets always count
        /// dropped slots.
        /// </summary>
        internal void RecordSlotRefusal(AlphaSeparationSlotRefusal reason)
        {
            if (reason == AlphaSeparationSlotRefusal.None)
            {
                throw new ArgumentException(
                    "AlphaSeparationSlotRefusal.None is not a refusal.",
                    nameof(reason));
            }

            slotRefusals[(int)reason]++;
        }

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

        /// <summary>
        /// What the barrier prepared for alpha separation, or null when no
        /// renderer produced a candidate slot.
        ///
        /// Like <see cref="AnimatorBindings"/> this is a live, transient host
        /// capability rather than proof evidence, and is deliberately outside
        /// the captured-evidence graph's no-live-Unity-object guarantee.
        /// </summary>
        internal PreparedAlphaSeparation Separation { get; set; }
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

            // Finally, validate every prepared candidate slot, finalize against
            // the surviving set, sweep unreferenced transients and apply the
            // single build-avatar mutation. Reactivating the extension here is
            // what makes the committed graph's clips reachable again; NDMF
            // deactivates and commits it again before this pass runs.
            sequence.WithRequiredExtension(
                typeof(AnimatorServicesContext),
                inner => inner.Run(
                    AlphaSeparationApply.PassName,
                    AlphaSeparationApply.Execute));
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
                HostLifecycleCapability.CaptureAndEvaluate(context),
                null,
                null,
                null,
                null,
                null);
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
                HostLifecycleCapability.Evaluate(facts),
                null,
                null,
                null,
                null,
                null);
        }

        /// <summary>
        /// Exercises the exact lifecycle, retained-bindings, committed-graph,
        /// renderer-loop, and accounting entry while substituting only the
        /// existing public-fixture seams for family/request selection,
        /// unavailable vendor source attestation, verified frontend
        /// interpretation, and the shader-family opaque-conversion step.
        /// <para>
        /// <paramref name="poiyomiConversion"/> and
        /// <paramref name="lilToonConversion"/> are the fourth and fifth
        /// public-fixture seams. A null value means "run the real
        /// <c>PoiyomiOpaqueConversion</c> resp. <c>LilToonOpaqueConversion</c>
        /// path", which is what production does; the three other delegates
        /// keep their guards because their null is a caller defect, while
        /// these two's null is meaningful.
        /// </para>
        /// </summary>
        internal static void Execute(
            BuildContext context,
            HostLifecycleFacts facts,
            AlphaMaterialRequestSelector selectRequest,
            ClosedAlphaMaterialCapturer capturer,
            CapturedAlphaMaterialSemanticsResolver resolveSemantics,
            VerifiedPoiyomiConversion poiyomiConversion = null,
            VerifiedLilToonConversion lilToonConversion = null)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            if (selectRequest == null)
            {
                throw new ArgumentNullException(nameof(selectRequest));
            }
            if (capturer == null) throw new ArgumentNullException(nameof(capturer));
            if (resolveSemantics == null)
            {
                throw new ArgumentNullException(nameof(resolveSemantics));
            }

            Execute(
                context,
                PendingState(context),
                HostLifecycleCapability.Evaluate(facts),
                selectRequest,
                capturer,
                resolveSemantics,
                poiyomiConversion,
                lilToonConversion);
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
            HostLifecycleCapability lifecycle,
            AlphaMaterialRequestSelector selectRequest,
            ClosedAlphaMaterialCapturer capturer,
            CapturedAlphaMaterialSemanticsResolver resolveSemantics,
            VerifiedPoiyomiConversion poiyomiConversion,
            VerifiedLilToonConversion lilToonConversion)
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

            foreach (var renderer in context.AvatarRootObject
                         .GetComponentsInChildren<Renderer>(true))
            {
                var refusal = UnityRendererAlphaAnalysis.HostStructuralRefusalFor(
                    renderer);
                if (refusal != RendererAnalysisRefusal.None)
                {
                    state.RecordRendererRefusal(refusal);
                    continue;
                }

                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform, context.AvatarRootObject.transform);
                // The live build-copy materials behind the captured admitted
                // set, index-aligned with evidence.AdmittedMaterials. Held as a
                // local transient host capability, never inside the evidence.
                IReadOnlyList<Material> admittedLiveMaterials;
                var evidence = selectRequest == null
                    ? UnityAnimationEvidenceCapture.Capture(
                        rendererPath,
                        renderer.sharedMaterials,
                        graph,
                        state.AnimatorBindings,
                        out admittedLiveMaterials)
                    : UnityAnimationEvidenceCapture.CaptureGraphForTests(
                        rendererPath,
                        renderer.sharedMaterials,
                        graph,
                        state.AnimatorBindings,
                        selectRequest,
                        capturer,
                        out admittedLiveMaterials);
                var resolved = ResolveRuntimeStates(
                    rendererPath, evidence, resolveSemantics);
                refusal = resolved.Refusal;
                var opaqueCandidateTriangleCount = 0;
                if (refusal == RendererAnalysisRefusal.None)
                {
                    var extraction = UnityRendererAlphaAnalysis.CaptureGeometry(
                        renderer, resolved.CurrentMaterials);
                    refusal = extraction.Refusal;
                    if (refusal == RendererAnalysisRefusal.None)
                    {
                        var plan = ClassifyRuntimeStates(
                            extraction.Snapshot,
                            resolved.SlotResults);
                        opaqueCandidateTriangleCount = plan.OpaqueTriangleCount;
                        RetainPreparedSeparation(
                            state,
                            extraction.MutationTarget,
                            rendererPath,
                            plan,
                            evidence,
                            admittedLiveMaterials,
                            poiyomiConversion,
                            lilToonConversion);
                    }
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
                string rendererPath,
                CapturedAnimationEvidence evidence,
                UnityRendererAlphaSnapshot snapshot,
                CapturedAlphaMaterialSemanticsResolver resolveSemantics)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var resolved = ResolveRuntimeStates(
                rendererPath, evidence, resolveSemantics);
            return resolved.Refusal == RendererAnalysisRefusal.None
                ? (RendererAnalysisRefusal.None,
                    ClassifyRuntimeStates(
                        snapshot, resolved.SlotResults).OpaqueTriangleCount)
                : (resolved.Refusal, 0);
        }

        private sealed class ResolvedRuntimeStates
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

        private static ResolvedRuntimeStates ResolveRuntimeStates(
                string rendererPath,
                CapturedAnimationEvidence evidence,
                CapturedAlphaMaterialSemanticsResolver resolveSemantics = null)
        {
            if (rendererPath == null)
                throw new ArgumentNullException(nameof(rendererPath));
            if (evidence == null)
                throw new ArgumentNullException(nameof(evidence));
            if (!evidence.IsClosed)
            {
                return Refused(
                    RendererAnalysisRefusal.MaterialDependencyClosureFailed);
            }

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
                        out var reference);
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
                            binding, rendererPath, evidence.AlphaRelevanceRequest))
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

            var slots = MaterialSlotsFor(evidence, rendererPath);

            var fields = UnityRendererAlphaAnalysis.GatherAlphaFields(
                evidence.AdmittedMaterials);
            bool AlphaFields(
                TextureSourceId source,
                TextureChannel channel,
                out AlphaMipChain chain)
            {
                chain = null;
                return channel == TextureChannel.Alpha &&
                       fields.TryGetValue(source, out chain);
            }

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
                var resolved = AdmittedMaterialStates.ResolveSlot(
                    slots[slotIndex],
                    evidence.AdmittedMaterials,
                    relevantBindings,
                    evidence.AlphaRelevanceRequest,
                    AlphaFields,
                    resolveSemantics);
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
                slotResults[slotIndex] = SlotResolutionResult.Resolved(
                    AdmittedMaterialStates.DistinctResolutions(
                        resolved.Resolutions));
            }

            // Nothing resolved, so there is no partial result to preserve and
            // the renderer keeps exactly its previous refusal, reason and
            // accounting. The first refusal is the reported one, which is the
            // same one the earlier return-on-first-failure produced.
            if (firstSlotRefusal != RendererAnalysisRefusal.None &&
                !anySlotResolved)
            {
                return Refused(firstSlotRefusal);
            }

            var currentMaterials =
                new CapturedAlphaMaterial[evidence.CurrentMaterialIndices.Count];
            for (var slot = 0; slot < currentMaterials.Length; slot++)
            {
                currentMaterials[slot] = evidence.AdmittedMaterials[
                    evidence.CurrentMaterialIndices[slot]];
            }

            return new ResolvedRuntimeStates(
                RendererAnalysisRefusal.None,
                currentMaterials,
                slotResults);
        }

        private static ResolvedRuntimeStates Refused(
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
        private static MeshSeparationPlan ClassifyRuntimeStates(
            UnityRendererAlphaSnapshot snapshot,
            IReadOnlyList<SlotResolutionResult> slotResults)
        {
            var submeshes = new List<SubmeshSeparationInput>(
                snapshot.Submeshes.Count);
            foreach (var submesh in snapshot.Submeshes)
            {
                var slot = slotResults[submesh.MaterialSlotIndex];
                submeshes.Add(new SubmeshSeparationInput(
                    submesh.MaterialSlotIndex,
                    submesh.Indices,
                    slot.IsResolved
                        ? IntersectResolvedOutcomes(snapshot, submesh, slot)
                        : UnprovenOutcomes(submesh.Indices.Count / 3)));
            }

            return MeshSeparationPlanner.Create(
                new MeshSeparationInput(snapshot.VertexCount, submeshes));
        }

        /// <summary>
        /// Retains what this renderer's plan found, for the later pass that
        /// validates and applies it. A renderer whose plan proves no triangle
        /// opaque has nothing to prepare and is not retained at all; neither
        /// is a renderer whose every candidate slot refused conversion.
        /// <para>
        /// Preparation mutates nothing but AMUSE-owned transient objects: no
        /// renderer, no clip, no source asset, and no asset saving.
        /// </para>
        /// </summary>
        private static void RetainPreparedSeparation(
            AmusePlatformFinishState state,
            UnityRendererMutationTarget mutationTarget,
            string rendererPath,
            MeshSeparationPlan plan,
            CapturedAnimationEvidence evidence,
            IReadOnlyList<Material> admittedLiveMaterials,
            VerifiedPoiyomiConversion poiyomiConversion,
            VerifiedLilToonConversion lilToonConversion)
        {
            var prepared = AlphaSeparationPreparation.Prepare(
                state,
                mutationTarget,
                rendererPath,
                plan,
                evidence,
                admittedLiveMaterials,
                poiyomiConversion,
                lilToonConversion);
            if (prepared == null)
            {
                return;
            }

            state.Separation ??= new PreparedAlphaSeparation();
            state.Separation.Add(prepared);
        }

        private static TriangleAlphaOutcome[] IntersectResolvedOutcomes(
            UnityRendererAlphaSnapshot snapshot,
            UnitySubmeshAlphaSnapshot submesh,
            SlotResolutionResult slot)
        {
            var perResolution = new List<TriangleAlphaOutcome[]>(
                slot.Resolutions.Count);
            foreach (var resolution in slot.Resolutions)
            {
                perResolution.Add(UnityRendererAlphaAnalysis.Classify(
                    submesh.Indices,
                    snapshot.Positions,
                    snapshot.HasUv0 ? snapshot.Uv0 : null,
                    resolution));
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
        private static TriangleAlphaOutcome[] UnprovenOutcomes(int triangleCount)
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

    }
}
