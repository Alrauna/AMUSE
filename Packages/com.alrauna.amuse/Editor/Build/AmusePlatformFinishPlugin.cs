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

        /// <summary>True once the barrier passed every avatar-scope gate
        /// (lifecycle, V1 trigger, D8 consent, committed-graph) and reached
        /// per-renderer analysis. The apply pass consumes this to report the
        /// avatar summary exactly once, with applied counts, and only for
        /// builds the barrier actually analyzed.</summary>
        internal bool ReachedRendererAnalysis { get; set; }

        /// <summary>True when the avatar's alpha policy deviates from its
        /// inert defaults, so moved triangles may rest on policy-widened
        /// evidence rather than on exact proof. The apply pass consumes
        /// this beside <see cref="ReachedRendererAnalysis"/> to mark the
        /// avatar summary exactly once per analyzed build.</summary>
        internal bool AlphaPolicyActive { get; set; }

        /// <summary>
        /// The active build target the barrier captured once, before
        /// per-renderer analysis. The apply pass compares its live
        /// selection against this captured value to refuse stale
        /// candidates, so this field must never be recomputed after the
        /// capture. Default zero before the capture ran.
        /// </summary>
        internal BuildTarget SamplingTarget { get; set; }

        /// <summary>
        /// The proof policy the barrier selected from the captured
        /// target and the build avatar's own acceptance fields. Every
        /// runtime-state resolution in this build resolves under this
        /// captured policy; no pass may substitute a constant. Holds the
        /// conservative mobile value when selection refused.
        /// </summary>
        internal AlphaSamplingPolicy SamplingPolicy { get; set; }

        /// <summary>
        /// True when the policy selection admitted the captured target.
        /// False means the target has no proof policy: the build keeps
        /// the conservative output value and the named unsupported
        /// report fired before analysis. No acceptance applies to such a
        /// build.
        /// </summary>
        internal bool HasSamplingPolicy { get; set; }

        /// <summary>How many renderers received at least one applied
        /// alpha-separation write.</summary>
        internal int AppliedRendererCount { get; set; }

        /// <summary>How many triangles were moved to opaque rendering
        /// by applied writes.</summary>
        internal int AppliedOpaqueTriangleCount { get; set; }

        /// <summary>
        /// How many processed renderers kept everything original.
        /// Analyzed plus renderer-refused renderers split exactly into
        /// applied and untouched, so the summary's renderer numbers
        /// cannot hide a refused renderer. The assumption is that
        /// AppliedRendererCount counts only renderers that
        /// AnalyzedRendererCount also counts; the pass structure
        /// guarantees that today, and a future pass reorder must
        /// re-read this comment.
        /// </summary>
        internal int UntouchedRendererCount =>
            AnalyzedRendererCount + SemanticallyRefusedRendererCount
            - AppliedRendererCount;

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

        /// <summary>
        /// The enumerated committed graph from the structural check pass,
        /// stored before any animator scope virtualizes the graph: the
        /// checks that need real authored clips keep their
        /// pre-virtualization view, and the barrier reads this stored
        /// result instead of enumerating again.
        /// </summary>
        internal CommittedControllerGraphResult StructuralGraph { get; set; }

        /// <summary>
        /// The finalization the apply pass last mutated the build avatar
        /// with, or null when apply performed no mutation. The window
        /// close reads the recorded slot writes from here, because the
        /// phase-end animator rebind re-applies stale t=0 curve values
        /// over the live slot arrays: the recorded write, not the live
        /// array, is what apply intended the slot to hold. Live transient
        /// host capability, deliberately outside the evidence graph.
        /// </summary>
        internal AlphaSeparationFinalization AppliedFinalization
        {
            get;
            set;
        }

        internal List<List<PreparedSlotSeparation>> SeparationSurvivingCandidates { get; set; }

        internal List<SerializedTransformationRow> PlayModeAvatarRows { get; } =
            new List<SerializedTransformationRow>();

        internal List<SerializedTransformationRow> PlayModeRendererRows { get; } =
            new List<SerializedTransformationRow>();
    }

    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal sealed class AmusePlatformFinishPlugin : Plugin<AmusePlatformFinishPlugin>
    {
        internal const string PluginQualifiedName = "com.alrauna.amuse";
        internal const string BindingsCapturePassName =
            "AMUSE animator bindings capture";
        internal const string StructuralGraphCheckPassName =
            "AMUSE structural graph check";
        internal const string BarrierPassName = "AMUSE semantic barrier";

        public override string QualifiedName => PluginQualifiedName;
        public override string DisplayName => "AMUSE";

        protected override void Configure()
        {
            var sequence = InPhase(BuildPhase.PlatformFinish);

            // The structural refusals read real authored clips, and
            // animation events survive only before virtualization: this
            // pass runs extension-free and first, stores the enumerated
            // graph and its refusal, and reports nothing. The barrier
            // reports the stored refusal behind the consent gates. The
            // passes after it read the same graph from state.
            sequence.Run(
                StructuralGraphCheckPassName,
                AmuseStructuralGraphCheck.Execute);

            // One animator scope for everything else: the bindings
            // capture, the barrier's swap admission, and the apply's
            // validation all read the same AnimationIndex, so the barrier
            // proves exactly the values the apply can ever see.
            sequence.WithRequiredExtension(
                typeof(AnimatorServicesContext),
                inner =>
                {
                    inner.Run(
                        BindingsCapturePassName,
                        AmuseAnimatorBindingsCapture.Execute);
                    inner.Run(
                        BarrierPassName, AmusePlatformFinishPass.Execute);
                    inner.Run(
                        AlphaSeparationApply.PassName,
                        ctx => AlphaSeparationApply.Execute(ctx));
                });

            // The window close is the fifth and last pass, extension-free
            // and after the animator scope has closed. NDMF commits the
            // virtual animator graph when the scope deactivates, and that
            // commit's controller assignment makes the editor animator
            // rebind, which applies the pre-commit animation state over
            // the renderer material arrays. Running the close here makes
            // its reference writes the final word: it reverts every open
            // pair to its locked original, re-asserts apply's recorded
            // write for the slots apply transformed, and destroys the
            // clone only after every reference is back on L. No build
            // path calls the vendor.
            sequence.Run(
                TransientUnlockWindowClose.PassName,
                ctx => TransientUnlockWindowClose.Execute(ctx));
        }

        /// <summary>
        /// Test-only override for the disclosure path's build-target
        /// read. Null in production. The sampling disclosure is the
        /// only consumer; the apply and editor read sites keep the
        /// real setting.
        /// </summary>
        internal static Func<BuildTarget> ActiveBuildTargetForTests = null;

        /// <summary>
        /// Resolves the target the sampling policy captures: the armed
        /// test override when present, otherwise the real active build
        /// target. Tests set and restore the override so EditMode runs
        /// never switch the editor's own build target. Internal because
        /// the barrier pass reads it across the class boundary.
        /// </summary>
        internal static BuildTarget ActiveBuildTarget()
        {
            return ActiveBuildTargetForTests != null
                ? ActiveBuildTargetForTests()
                : EditorUserBuildSettings.activeBuildTarget;
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
                null,
                null,
                null);
        }

        internal static void Execute(
            BuildContext context,
            HostLifecycleFacts facts,
            VersionConsentPresenter consentPresenter = null)
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
                null,
                consentPresenter,
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
        /// <c>PoiyomiOpaqueConversion</c> resp. <c>LilToonOpaqueTarget</c>
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
            VerifiedLilToonConversion lilToonConversion = null,
            VersionConsentPresenter consentPresenter = null,
            Func<Material, bool> lockedOriginalAttestation = null)
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
                lilToonConversion,
                consentPresenter,
                lockedOriginalAttestation);
        }

        /// <summary>
        /// When the animator extension is active, returns its AnimationIndex:
        /// the same index the apply pass validates against. When it is
        /// inactive, returns false — the caller then admits swaps from the
        /// committed-graph walk alone, which is the legacy capture shape.
        /// </summary>
        private static bool TryGetAnimationIndex(
            BuildContext context, out AnimationIndex animationIndex)
        {
            try
            {
                animationIndex = context
                    .Extension<AnimatorServicesContext>().AnimationIndex;
                return true;
            }
            catch (Exception exception) when (
                IsInactiveExtensionSignal(exception))
            {
                animationIndex = null;
                return false;
            }
        }

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
            VerifiedLilToonConversion lilToonConversion,
            VersionConsentPresenter consentPresenter,
            Func<Material, bool> lockedOriginalAttestation)
        {
            state.Lifecycle = lifecycle;
            state.HasExecuted = true;
            UnityGeneratedTextureEvidence.ClearCache();
            ReplacementTextureIdentity.ClearSession();
            ReplacementTextureAttestation.ClearVersionCacheForSession();
            AaoAtlasTextureAttestation.ClearVersionCacheForSession();
            if (!lifecycle.MayUsePositiveMutation)
            {
                // V7: a lifecycle refusal is never silent. One plain
                // English entry on the avatar root tells the user why
                // nothing happened.
                AmuseReports.LifecycleRefusal(
                    context.AvatarRootObject, lifecycle.Refusal);
                if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                {
                    var refusalKey = AmuseReportStrings.HostKey(lifecycle.Refusal);
                    var sentence = AmuseReportStrings.Has(refusalKey)
                        ? AmuseReportStrings.Format(refusalKey)
                        : "Lifecycle refusal: " + lifecycle.Refusal;
                    state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                    {
                        kind = 0,
                        sentence = sentence
                    });
                    PlayModeBuildStore.SaveOutcome(context, state);
                }
                return;
            }

            // V1 trigger: the pipeline runs only when the avatar root
            // carries the opt-in component. Absence is a total silent
            // no-op: nothing is observed and nothing is reported. This
            // check precedes the bindings invariant because an unopted
            // avatar does no work at all. A component on a child does not
            // count; the avatar root is the only switch.
            var activated = ActivatedOptimizer(context);
            if (activated == null)
            {
                return;
            }

            // Feature switch: the alpha separator runs only with its
            // toggle on. The component stays present for later sibling
            // features. Off mirrors the Disable AMUSE contract for this
            // one feature: nothing analyzed, nothing mutated, nothing
            // reported, and the consent layer below never asks.
            if (!activated.AlphaSeparatorEnabled)
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

            // The structural pass enumerated the real graph before
            // virtualization and stored it; this pass reads that stored
            // result so the checks that need real clips keep their
            // pre-virtualization view. Direct callers that bypass the pass
            // list get the enumeration inline: at that point the graph is
            // still real, so the view is equally pre-virtualization.
            if (state.StructuralGraph == null)
            {
                AmuseStructuralGraphCheck.Execute(context);
            }

            var graph = state.StructuralGraph;

            if (graph.Refusal != AvatarAnimationRefusal.None)
            {
                // Avatar scope: the exact named cause is preserved and the whole
                // avatar stops. No renderer is analyzed, so no partial result and
                // no per-renderer accounting can survive. This gate names the
                // cause exactly once, and only for avatars the gates above it
                // admitted: positive lifecycle, the opt-in component on the
                // avatar root, and the feature switch on.
                state.AvatarRefusal = graph.Refusal;
                AmuseReports.AvatarRefusal(
                    context.AvatarRootObject, graph.Refusal);
                if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                {
                    var refusalKey = AmuseReportStrings.AvatarKey(graph.Refusal);
                    var sentence = AmuseReportStrings.Has(refusalKey)
                        ? AmuseReportStrings.Format(refusalKey)
                        : "Structural refusal: " + graph.Refusal;
                    state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                    {
                        kind = 0,
                        sentence = sentence
                    });
                    PlayModeBuildStore.SaveOutcome(context, state);
                }
                return;
            }

            // D8 consent layer: one consolidated click-through per build
            // covers host versions beyond the attested maxima and shader
            // names collected from the avatar's assigned materials and
            // swap-reachable materials whose source is not a verified
            // version. The block sits after the committed-graph gate, so
            // an avatar that refuses avatar-wide is never asked. Batch
            // mode has no user to ask and refuses. Declining is a total
            // no-op after the trigger: an opted-out avatar never asks.
            var shaderTransfer = UnityMaterialSemantics
                .CollectTransferConsent(
                    AssignedAndSwapCurvedMaterials(context, graph));
            var subjects = new List<string>(lifecycle.ConsentSubjects);
            subjects.AddRange(shaderTransfer.Subjects);
            // The granted transfer names compose into every gate that
            // verifies a source identity: the transferred capture skips the
            // check for a granted name, and the conversion boundary skips it
            // for a granted name.
            // An unconsented build grants nothing and stays fail-closed.
            var grantedShaderNames =
                shaderTransfer.GrantedShaderNames.Count > 0
                    ? shaderTransfer.GrantedShaderNames
                    : null;
            // The unlock window no longer carries its own per-build
            // consent subject. V2 and V4 passed live observation on
            // 2026-09-21, so per spec section 12 the gate moved to the
            // D8 pattern: an eligible build opens the window without
            // asking. Eligibility is the attestation gate alone: a
            // locked material whose original shader passes attestation
            // opens the window.
            var windowEligible =
                TransientUnlockAvailability.WindowEligibleForConsent(
                    AssignedAndSwapCurvedMaterials(context, graph),
                    lockedOriginalAttestation);

            if (subjects.Count > 0
                && !VersionConsentDialog.ShouldProceed(
                    subjects,
                    UnityEngine.Application.isBatchMode,
                    consentPresenter ?? VersionConsentDialog.Present))
            {
                AmuseReports.ConsentDeclined(subjects);
                if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                {
                    state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                    {
                        kind = 0,
                        sentence = "Build consent was declined."
                    });
                    PlayModeBuildStore.SaveOutcome(context, state);
                }
                return;
            }

            if (windowEligible)
            {
                context.GetState<TransientUnlockWindowState>()
                    .ConsentGranted = true;
                if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                {
                    state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                    {
                        kind = 1,
                        sentence = "This avatar holds locked or transfer protected materials. A build with granted consent may convert further than this view shows."
                    });
                }
            }

            var transferShaders = grantedShaderNames != null;

            // Every avatar-scope gate has passed: the unlock window opens
            // here, before capture, so the whole pipeline below reads the
            // swapped world. The swap-in holds its own consent gate and
            // attestation gate, so an ineligible build reaches it as a
            // counted no-op.
            TransientUnlockSwapIn.SwapIn(
                context,
                context.GetState<TransientUnlockWindowState>(),
                TransientUnlockAvailability.CreateProductionRestore(),
                lockedOriginalAttestation);

            // From here the run is reported, so the apply pass may
            // summarize it with applied counts.
            state.ReachedRendererAnalysis = true;

            // While the window is open, the capture reads the world
            // through the swapped view: the stored committed graph was
            // enumerated before the swap-in and still carries every
            // observed locked original, while the slot arrays and the
            // live clips hold the clones. Folding the substitution into
            // the observed values keeps one admitted material per
            // binding, which is exactly what the apply pass validates
            // against.
            var window = context.GetState<TransientUnlockWindowState>();
            Func<Material, Material> swappedView = window.OpenPairs.Count
                > 0
                ? window.SwappedView
                : null;

            var optimizer = context.AvatarRootObject
                .GetComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var maxMipLevel = RuntimeStateResolution.ProofMipCapFrom(
                optimizer?.PreserveTransparencyMaxMipLevel ?? -1);
            var minTextureSize = RuntimeStateResolution.MinTextureSizeFrom(
                optimizer?.PreserveTransparencyMinTextureSize ?? -1);
            var minimumOpaqueCoveragePercent =
                RuntimeStateResolution.MinimumCoverageFrom(
                    optimizer?.MinimumOpaqueCoveragePercent ?? 0);
            var minimumOpaqueCoverageToPreservePercent =
                RuntimeStateResolution.MinimumPreserveCoverageFrom(
                    optimizer?.MinimumOpaqueCoverageToPreservePercent ?? 0);
            var opaqueAlphaPercent = OpaquePercentFrom(optimizer);
            var polygonClampPercent = PolygonClampPercentFrom(optimizer);
            var densityCapPercent =
                DensityCapFromCoveragePercent(optimizer);
            var alphaPolicyBounds =
                AlphaPolicyBounds.From(opaqueAlphaPercent, polygonClampPercent);
            // The disclosure marker follows the spec's exact rule: the
            // summary names the policy only when a setting left its inert
            // default. The density alone never marks a build.
            state.AlphaPolicyActive =
                opaqueAlphaPercent < 100 || polygonClampPercent > 0;
            var ignoreOutOfRangeSlots =
                optimizer == null || optimizer.IgnoreOutOfRangeMaterialSlots;
            var allowDepthTestChange =
                optimizer == null || optimizer.AllowDepthTestChange;

            // The proof policy is a build-wide fact. Capture the active
            // target and the selected policy exactly once, before any
            // renderer analysis, from the build avatar's own optimizer
            // component - never from inspector state or a static
            // mutable. A target with no policy keeps the conservative
            // output value and names the fact before the analysis runs;
            // no acceptance applies to such a build.
            var samplingTarget = AmusePlatformFinishPlugin.ActiveBuildTarget();
            state.SamplingTarget = samplingTarget;
            state.HasSamplingPolicy = PlatformAlphaSamplingPolicy.TrySelect(
                samplingTarget,
                optimizer != null &&
                    optimizer.AcceptDesktopAnisotropyDifferences,
                optimizer != null &&
                    optimizer.AcceptMobileAnisotropyDifferences,
                out var samplingPolicy);
            state.SamplingPolicy = samplingPolicy;
            if (!state.HasSamplingPolicy)
            {
                AmuseReports.UnsupportedSamplingTarget(
                    context.AvatarRootObject, samplingTarget);
                if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                {
                    state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                    {
                        kind = 0,
                        sentence = "The active build target supports no sampling policy. A build on this target applies nothing. This view shows no results for it."
                    });
                }
            }

            if (samplingTarget != BuildTarget.StandaloneWindows64 && lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
            {
                state.PlayModeAvatarRows.Add(new SerializedTransformationRow
                {
                    kind = 0,
                    sentence = "The active build target is not Windows. Texture proof needs the Windows import. This view can prove fewer triangles than a Windows build."
                });
            }

            foreach (var renderer in context.AvatarRootObject
                         .GetComponentsInChildren<Renderer>(true))
            {
                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform, context.AvatarRootObject.transform);
                var rendererTypeName = renderer.GetType().FullName;

                var refusal = UnityRendererAlphaAnalysis.HostStructuralRefusalFor(
                    renderer,
                    out var structuralMeshSubMeshCount,
                    out var structuralMaterialSlotCount);
                if (refusal != RendererAnalysisRefusal.None)
                {
                    state.RecordRendererRefusal(refusal);
                    AmuseReports.RendererRefusal(
                        renderer,
                        refusal,
                        structuralMeshSubMeshCount,
                        structuralMaterialSlotCount);
                    if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                    {
                        RecordPlayModeRendererRefusal(
                            state, renderer, rendererPath, rendererTypeName,
                            refusal, structuralMeshSubMeshCount, structuralMaterialSlotCount, null);
                    }
                    continue;
                }

                // The live build-copy materials behind the captured admitted
                // set, index-aligned with evidence.AdmittedMaterials. Held as a
                // local transient host capability, never inside the evidence.
                IReadOnlyList<Material> admittedLiveMaterials;
                ClosedAlphaMaterialCapturer effectiveCapturer = null;
                if (transferShaders)
                {
                    var granted = shaderTransfer.GrantedShaderNames;
                    effectiveCapturer = (
                        IReadOnlyList<Material> materials,
                        IReadOnlyList<CapturedAlphaMaterialFamily> families,
                        MaterialEvidenceRequest request,
                        AlphaPolicyBounds bounds,
                        out ClosedAlphaCaptureOutcome transferred) =>
                        UnityMaterialSemantics
                            .TryCaptureClosedAlphaMaterialsTransferred(
                                materials,
                                families,
                                request,
                                bounds,
                                granted,
                                out transferred,
                                RegisteredSourceIdentity.Resolve);
                }

                var hasIndex = TryGetAnimationIndex(
                    context, out var animationIndex);
                CapturedAnimationEvidence evidence;
                if (hasIndex)
                {
                    evidence = UnityAnimationEvidenceCapture
                        .CaptureWithAnimationIndex(
                            rendererPath,
                            renderer.sharedMaterials,
                            graph,
                            state.AnimatorBindings,
                            animationIndex,
                            alphaPolicyBounds,
                            out admittedLiveMaterials,
                            selectRequest,
                            CaptureThroughEffectiveMaterials(
                                renderer, capturer ?? effectiveCapturer),
                            ignoreOutOfRangeSlots,
                            rendererTypeName,
                            LockedMaterialIdentity.PreCheckRefusal,
                            swappedView,
                            RegisteredSourceIdentity.Resolve);
                }
                else if (selectRequest == null)
                {
                    evidence = UnityAnimationEvidenceCapture.Capture(
                        rendererPath,
                        renderer.sharedMaterials,
                        graph,
                        state.AnimatorBindings,
                        alphaPolicyBounds,
                        out admittedLiveMaterials,
                        CaptureThroughEffectiveMaterials(
                            renderer, effectiveCapturer),
                        ignoreOutOfRangeSlots,
                        rendererTypeName,
                        LockedMaterialIdentity.PreCheckRefusal,
                        swappedView,
                        RegisteredSourceIdentity.Resolve);
                }
                else
                {
                    evidence = UnityAnimationEvidenceCapture
                        .CaptureGraphThroughSeams(
                            rendererPath,
                            renderer.sharedMaterials,
                            null,
                            graph,
                            state.AnimatorBindings,
                            alphaPolicyBounds,
                            selectRequest,
                            CaptureThroughEffectiveMaterials(
                                renderer, capturer),
                            out admittedLiveMaterials,
                            ignoreOutOfRangeSlots,
                            rendererTypeName,
                            LockedMaterialIdentity.PreCheckRefusal,
                            swappedView,
                            RegisteredSourceIdentity.Resolve);
                }
                var effectiveResolver = resolveSemantics
                    ?? (transferShaders
                        ? (CapturedAlphaMaterialSemanticsResolver)
                            UnityMaterialSemantics.AnalyzeAlphaMaterialTransferred
                        : null);
                var blockState =
                    EffectiveMaterialMaterialization.CaptureBlockState(renderer);
                var alphaFields = UnityRendererAlphaAnalysis.GatherAlphaFields(
                    evidence.AdmittedMaterials, maxMipLevel, minTextureSize);
                var resolved = RuntimeStateResolution.ResolveRuntimeStates(
                    rendererPath, evidence, samplingPolicy,
                    alphaFields,
                    UnityAnimationEvidenceCapture.TextureScaleOffsetSuffix,
                    UnityRendererAlphaAnalysis.StructuralRefusal,
                    UnityAnimationEvidenceCapture.ProofRelevantResolver,
                    UnityAnimationEvidenceCapture.UnrecognizedObjectBinding,
                    UnityAnimationEvidenceCapture.MaterialSlotBindingParser,
                    effectiveResolver, maxMipLevel,
                    minTextureSize, densityCapPercent, blockState,
                    rendererTypeName: rendererTypeName);
                refusal = resolved.Refusal;
                var slots = RuntimeStateResolution.MaterialSlotsFor(
                    evidence, rendererPath,
                    UnityAnimationEvidenceCapture.MaterialSlotBindingParser,
                    rendererTypeName);
                // Every refused slot reports its own exact reason, even
                // when the renderer as a whole continues or the first
                // refusal names the renderer: one line per slot, no
                // aggregation, so a manual test can read the full list.
                // The slot entry folds the slot's texture capture facts,
                // so one refused slot is one console entry. The slots and
                // the slot results come from one MaterialSlotsFor call on
                // one evidence record, so slot i of one is slot i of the
                // other, and a divergence is an invariant defect.
                for (var slotIndex = 0;
                     slotIndex < resolved.SlotResults.Length;
                     slotIndex++)
                {
                    if (resolved.SlotResults[slotIndex].IsResolved)
                    {
                        continue;
                    }

                    var closureWay =
                        resolved.SlotResults[slotIndex].ClosureWay;
                    AmuseReports.SlotAnalysisRefusal(
                        renderer,
                        slotIndex,
                        resolved.SlotResults[slotIndex].Refusal,
                        renderer.gameObject.name,
                        resolved.SlotResults[slotIndex].Offender,
                        resolved.SlotResults[slotIndex].UnknownReason,
                        slots[slotIndex].CaptureRefusals,
                        closureWay ==
                            MaterialDependencyClosureFailure.None
                            ? null
                            : AmuseReports.ClosureFailureSentence(
                                closureWay));
                }

                // An out-of-range closure failure names a slot the renderer
                // does not have, so it has no slot result. It still reports
                // its own entry: the template takes an arbitrary slot index,
                // and the apply pass reads the same record when a split
                // could append a slot onto that index.
                foreach (var record in evidence.SlotClosureFailures)
                {
                    if (record.SlotIndex < resolved.SlotResults.Length)
                    {
                        continue;
                    }

                    AmuseReports.SlotAnalysisRefusal(
                        renderer,
                        record.SlotIndex,
                        RendererAnalysisRefusal
                            .MaterialDependencyClosureFailed,
                        renderer.gameObject.name,
                        closureWaySentence:
                        AmuseReports.ClosureFailureSentence(
                            record.Failure));
                }

                // A resolved slot can still carry refused material resolutions. The
                // slot stays resolved and the triangles classify Unknown; this entry
                // names the lost proof reason. Diagnostic only: no census record.
                if (resolved.Refusal == RendererAnalysisRefusal.None)
                {
                    for (var slotIndex = 0;
                         slotIndex < resolved.SlotResults.Length;
                         slotIndex++)
                    {
                        var slotResult = resolved.SlotResults[slotIndex];
                        if (!slotResult.IsResolved)
                        {
                            continue; // refused slots report through the fold loop above.
                        }
                        for (var i = 0;
                             i < slotResult.MaterialResolutions.Count;
                             i++)
                        {
                            var resolution = slotResult.MaterialResolutions[i];
                            if (resolution.IsResolved)
                            {
                                continue;
                            }
                            // Load-bearing indirection: resolution i names the
                            // material at the slot's admitted index, and admission
                            // indices are not contiguous. AdmittedMaterials[i]
                            // compiles and misnames the material.
                            var offender = evidence.AdmittedMaterials[
                                slots[slotIndex].AdmittedMaterialIndices[i]];
                            AmuseReports.SlotResolutionRefused(
                                renderer, slotIndex, renderer.gameObject.name,
                                offender, resolution.Failure);
                        }
                    }
                }

                var opaqueCandidateTriangleCount = 0;
                var extractionMeshSubMeshCount = -1;
                var extractionMaterialSlotCount = -1;
                if (refusal == RendererAnalysisRefusal.None)
                {
                    var extraction = UnityRendererAlphaAnalysis.CaptureGeometry(
                        renderer, resolved.CurrentMaterials);
                    refusal = extraction.Refusal;
                    extractionMeshSubMeshCount = extraction.MeshSubMeshCount;
                    extractionMaterialSlotCount = extraction.MaterialSlotCount;
                    if (refusal == RendererAnalysisRefusal.None)
                    {
                        var plan = RuntimeStateResolution.ClassifyRuntimeStates(
                            extraction.Snapshot,
                            resolved.SlotResults,
                            UnityRendererAlphaAnalysis.TriangleClassifier,
                            UnityRendererAlphaAnalysis.OutcomeIntersector);
                        opaqueCandidateTriangleCount = plan.OpaqueTriangleCount;
                        for (var slotIndex = 0;
                             slotIndex < slots.Count;
                             slotIndex++)
                        {
                            // A refused slot's own entry carries the
                            // capture facts. A standalone texture entry
                            // for that slot would state one fact twice,
                            // so it reports only for resolved slots. The
                            // no-alpha-channel diagnostic refuses nothing
                            // and names a different fact, so it stays
                            // unconditional.
                            if (resolved.SlotResults[slotIndex].IsResolved)
                            {
                                foreach (var textureRefusal in
                                         slots[slotIndex].CaptureRefusals)
                                {
                                    AmuseReports.TextureCaptureRefusal(
                                        renderer,
                                        slotIndex,
                                        textureRefusal);
                                }
                            }

                            foreach (var propertyName in
                                     slots[slotIndex].NoAlphaChannelProperties)
                            {
                                AmuseReports.TextureAlphaMissing(
                                    renderer,
                                    slotIndex,
                                    propertyName);
                            }
                        }

                        RetainPreparedSeparation(
                            state,
                            extraction.MutationTarget,
                            rendererPath,
                            plan,
                            evidence,
                            admittedLiveMaterials,
                            poiyomiConversion,
                            lilToonConversion,
                            minimumOpaqueCoveragePercent,
                            minimumOpaqueCoverageToPreservePercent,
                            rendererTypeName,
                            allowDepthTestChange,
                            grantedShaderNames,
                            resolved.SlotResults);

                        if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay && plan != null)
                        {
                            RecordPlayModeSlots(
                                state, renderer, rendererPath, rendererTypeName,
                                plan, resolved.SlotResults, extraction.Snapshot);
                        }
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
                    AmuseReports.RendererRefusal(
                        renderer,
                        refusal,
                        extractionMeshSubMeshCount,
                        extractionMaterialSlotCount,
                        refusal ==
                        RendererAnalysisRefusal
                            .MaterialDependencyClosureFailed &&
                        evidence.SlotClosureFailures.Count > 0
                            ? AmuseReports.ClosureFailureSentence(
                                evidence.SlotClosureFailures[0].Failure)
                            : null);
                    // A renderer refusal suppresses the per-slot loops below,
                    if (lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay)
                    {
                        var detail = refusal ==
                            RendererAnalysisRefusal
                                .MaterialDependencyClosureFailed &&
                            evidence.SlotClosureFailures.Count > 0
                                ? AmuseReports.ClosureFailureSentence(
                                    evidence.SlotClosureFailures[0].Failure)
                                : null;
                        RecordPlayModeRendererRefusal(
                            state, renderer, rendererPath, rendererTypeName,
                            refusal, extractionMeshSubMeshCount, extractionMaterialSlotCount, detail);
                    }
                    // so the slots' captured facts would die. Report exactly
                    // the facts that did not surface: with empty slot
                    // results, every slot; otherwise only resolved slots,
                    // because refused slots folded their facts already.
                    // Report-only: no census record, and the host-structural
                    // path never reaches this point with captured evidence.
                    for (var slotIndex = 0;
                         slotIndex < slots.Count;
                         slotIndex++)
                    {
                        var foldedAlready =
                            resolved.SlotResults.Length > 0 &&
                            !resolved.SlotResults[slotIndex].IsResolved;
                        if (foldedAlready)
                        {
                            continue;
                        }

                        var slotEvidence = slots[slotIndex];
                        foreach (var captureRefusal in
                                 slotEvidence.CaptureRefusals)
                        {
                            AmuseReports.TextureCaptureRefusal(
                                renderer,
                                slotIndex,
                                captureRefusal);
                        }

                        foreach (var noAlphaProperty in
                                 slotEvidence.NoAlphaChannelProperties)
                        {
                            AmuseReports.TextureAlphaMissing(
                                renderer,
                                slotIndex,
                                noAlphaProperty);
                        }

                        // Locked identity: emit at the first carrier in
                        // material order.
                        foreach (var materialIndex in
                                 slotEvidence.AdmittedMaterialIndices)
                        {
                            var material =
                                evidence.AdmittedMaterials[materialIndex];
                            if (material.LockedIdentityRefusal !=
                                RendererAnalysisRefusal.None)
                            {
                                AmuseReports.SlotAnalysisRefusal(
                                    renderer,
                                    slotIndex,
                                    material.LockedIdentityRefusal,
                                    renderer.gameObject.name,
                                    material,
                                    unknownReason: null,
                                    captureRefusals: null,
                                    closureWaySentence: null);
                                break;
                            }
                        }
                    }

                    continue;
                }

                state.AnalyzedRendererCount++;
                state.OpaqueCandidateTriangleCount +=
                    opaqueCandidateTriangleCount;
            }
        }

        /// <summary>
        /// Wraps a closed material capturer so the attested batch is captured
        /// through each material's per-slot effective materialization: the
        /// proof must read the values the renderer shows, block overrides
        /// included. Identity flows stay on the live materials — the admitted
        /// set, the swap-value matching, and the retained live pairing keep
        /// the original references, so the write path still matches against
        /// them. A null inner capturer keeps the production default. Every
        /// clone dies inside the wrapper, on every exit path.
        /// </summary>
        private static ClosedAlphaMaterialCapturer
            CaptureThroughEffectiveMaterials(
                Renderer renderer,
                ClosedAlphaMaterialCapturer inner)
        {
            if (inner == null)
            {
                inner = (
                    IReadOnlyList<Material> batchMaterials,
                    IReadOnlyList<CapturedAlphaMaterialFamily> batchFamilies,
                    MaterialEvidenceRequest batchRequest,
                    AlphaPolicyBounds batchBounds,
                    out ClosedAlphaCaptureOutcome batchOutcome) =>
                    UnityMaterialSemantics.TryCaptureClosedAlphaMaterials(
                        batchMaterials, batchFamilies, batchRequest,
                        batchBounds, out batchOutcome,
                        RegisteredSourceIdentity.Resolve);
            }
            return (
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                AlphaPolicyBounds bounds,
                out ClosedAlphaCaptureOutcome transferred) =>
            {
                var effective = EffectiveMaterialMaterialization
                    .MaterializeAdmitted(renderer, materials, out var clones);
                try
                {
                    return inner(
                        effective, families, request, bounds, out transferred);
                }
                finally
                {
                    foreach (var clone in clones)
                    {
                        UnityEngine.Object.DestroyImmediate(clone);
                    }
                }
            };
        }

        /// <summary>
        /// The V1 trigger: true only when the avatar root itself carries
        /// <see cref="Alrauna.Amuse.Runtime.AmuseAvatarOptimizer"/> with its
        /// Disable AMUSE toggle off. A component on any child transform does
        /// not activate the pipeline, and a disabled component activates
        /// nothing at all - the build treats it exactly like an absent
        /// one. The return carries the component so downstream feature
        /// switches read their own property.
        /// </summary>
        private static Alrauna.Amuse.Runtime.AmuseAvatarOptimizer
            ActivatedOptimizer(BuildContext context)
        {
            var root = context.AvatarRootObject;
            if (root == null)
            {
                return null;
            }
            var component =
                root.GetComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            return component != null && !component.AmuseDisabled
                ? component
                : null;
        }

        /// <summary>
        /// Maps the optimizer's "Minimum Opaque Alpha Percentage" policy
        /// to the proof's opaque percent. A stored value outside the
        /// Range attribute is a defect in the stored component, and the
        /// defensive read clamps it into the expressible range instead
        /// of classifying with a policy no inspector could have saved.
        /// A missing component maps to the inert 100.
        /// </summary>
        internal static int OpaquePercentFrom(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer optimizer)
        {
            if (optimizer == null)
            {
                return 100;
            }
            return RuntimeStateResolution.OpaquePercentFrom(
                optimizer.MinimumOpaqueAlphaPercent);
        }

        /// <summary>
        /// Maps the optimizer's "Alpha Upper Clamp to Convert (Per Polygon)"
        /// policy to the proof's noise percent. The parser enforces
        /// the clamp pair invariant for preset files, and the
        /// inspector enforces it for live edits. This method keeps
        /// only the 0 to 100 banding and the inert mapping of 100 to
        /// 0, so its result covers a pair only when a serialized
        /// component was tampered with. A missing component maps to
        /// the inert 0.
        /// </summary>
        internal static int PolygonClampPercentFrom(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer optimizer)
        {
            if (optimizer == null)
            {
                return 0;
            }
            return RuntimeStateResolution.PolygonClampPercentFrom(
                optimizer.PolygonAlphaUpperClampPercent);
        }

        /// <summary>
        /// Maps the optimizer's "Minimum Opaque Coverage to Convert
        /// (Per Polygon)" policy to the classifier's density bound. The
        /// coverage share and the tolerated stray share are
        /// complements, so a coverage of 98 maps to a density bound
        /// of 2. A coverage of 100 maps to the inert 0, which never
        /// substitutes. A stored value below zero is treated as the
        /// inert 100. A stored value above 100 is rejected here
        /// rather than at the first Classify: the classifier throws
        /// outside this range, and a wiring-time failure names the
        /// component before any build work starts. A missing
        /// component maps to the inert 0.
        /// </summary>
        internal static int DensityCapFromCoveragePercent(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer optimizer)
        {
            if (optimizer == null)
            {
                return 0;
            }
            return RuntimeStateResolution.DensityCapFromCoveragePercent(
                optimizer.PolygonMinimumOpaqueCoveragePercent);
        }

        /// <summary>
        /// Every non-null assigned material on the avatar, for the D8
        /// shader-transfer pre-scan. Distinctness is the collector's job.
        /// </summary>
        private static IEnumerable<Material> AllAssignedMaterials(
            BuildContext context)
        {
            foreach (var renderer in context.AvatarRootObject
                         .GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        yield return material;
                    }
                }
            }
        }

        /// <summary>
        /// The D8 pre-scan's material universe: the assigned materials,
        /// then every material a stored-graph clip can swap onto a slot
        /// through an object-reference curve. A material reachable only
        /// through a swap must get its consent subject like an assigned
        /// one. Null and non-material key values skip, and distinctness
        /// stays the collector's job, with the seen set keeping the walk
        /// itself duplicate-free.
        /// </summary>
        private static IEnumerable<Material> AssignedAndSwapCurvedMaterials(
            BuildContext context,
            CommittedControllerGraphResult graph)
        {
            var seen = new HashSet<Material>();
            foreach (var material in AllAssignedMaterials(context))
            {
                if (seen.Add(material))
                {
                    yield return material;
                }
            }
            foreach (var layer in graph.Layers)
            {
                foreach (var clip in layer.Clips)
                {
                    foreach (var binding in AnimationUtility
                                 .GetObjectReferenceCurveBindings(clip))
                    {
                        if (!binding.propertyName.StartsWith("m_Materials"))
                        {
                            continue;
                        }
                        foreach (var key in AnimationUtility
                                     .GetObjectReferenceCurve(clip, binding))
                        {
                            if (key.value is Material material
                                && seen.Add(material))
                            {
                                yield return material;
                            }
                        }
                    }
                }
            }
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
            VerifiedLilToonConversion lilToonConversion,
            int minimumOpaqueCoveragePercent,
            int minimumOpaqueCoverageToPreservePercent,
            string rendererTypeName = null,
            bool allowDepthTestChange = false,
            IReadOnlyCollection<string> grantedShaderNames = null,
            IReadOnlyList<SlotResolutionResult> slotResults = null)
        {
            var prepared = AlphaSeparationPreparation.Prepare(
                state,
                mutationTarget,
                rendererPath,
                plan,
                evidence,
                admittedLiveMaterials,
                poiyomiConversion,
                lilToonConversion,
                minimumOpaqueCoveragePercent,
                minimumOpaqueCoverageToPreservePercent,
                rendererTypeName,
                allowDepthTestChange,
                grantedShaderNames,
                slotResults);
            if (prepared == null)
            {
                return;
            }

            state.Separation ??= new PreparedAlphaSeparation();
            state.Separation.Add(prepared);
        }

        internal static void RecordPlayModeRendererRefusal(
            AmusePlatformFinishState state,
            Renderer renderer,
            string rendererPath,
            string rendererTypeName,
            RendererAnalysisRefusal refusal,
            int meshSubMeshCount,
            int materialSlotCount,
            string detail)
        {
            var scenePath = renderer != null
                ? (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
                    .GetReference(renderer, false)?.Path
                : null;
            var effectivePath = scenePath != null ? scenePath : rendererPath;

            var key = AmuseReportStrings.RendererKey(refusal);
            var refusalSentence = AmuseReportStrings.Has(key)
                ? AmuseReportStrings.Format(
                    key,
                    AmuseReports.SlotCountPhrase(meshSubMeshCount),
                    AmuseReports.SlotCountPhrase(materialSlotCount),
                    renderer != null ? renderer.gameObject.name : "",
                    string.IsNullOrEmpty(detail) ? "" : detail)
                : refusal.ToString();

            state.PlayModeRendererRows.Add(new SerializedTransformationRow
            {
                kind = 2,
                rendererPath = effectivePath,
                rendererTypeName = rendererTypeName,
                sentence = effectivePath + " (" + rendererTypeName + "): refused. " + refusalSentence
            });

            var materials = renderer != null ? renderer.sharedMaterials : null;
            var slotCount = materials != null ? materials.Length : (materialSlotCount > 0 ? materialSlotCount : 0);
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                var mat = materials != null && slotIndex < materials.Length ? materials[slotIndex] : null;
                var sourceMat = mat != null ? RegisteredSourceIdentity.Resolve(mat) as Material ?? mat : null;
                var sourcePath = sourceMat != null ? AssetDatabase.GetAssetPath(sourceMat) : string.Empty;
                state.PlayModeRendererRows.Add(new SerializedTransformationRow
                {
                    kind = 3,
                    rendererPath = effectivePath,
                    rendererTypeName = rendererTypeName,
                    sourceMaterialPath = sourcePath,
                    slotIndex = slotIndex,
                    movedTriangles = -1,
                    provenTriangles = -1,
                    totalTriangles = -1,
                    sentence = "Refused: AMUSE currently does not support this material"
                });
            }
        }

        private static int SubmeshPolygonCount(Renderer renderer, int slotIndex)
        {
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer smr)
            {
                mesh = smr.sharedMesh;
            }
            else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
            {
                mesh = filter.sharedMesh;
            }

            if (mesh == null || slotIndex < 0 || slotIndex >= mesh.subMeshCount)
            {
                return -1;
            }

            if (mesh.GetTopology(slotIndex) != MeshTopology.Triangles)
            {
                return -1;
            }

            return (int)(mesh.GetIndexCount(slotIndex) / 3);
        }

        internal static void RecordPlayModeSlots(
            AmusePlatformFinishState state,
            Renderer renderer,
            string rendererPath,
            string rendererTypeName,
            MeshSeparationPlan plan,
            IReadOnlyList<SlotResolutionResult> slotResults,
            UnityRendererAlphaSnapshot snapshot)
        {
            var scenePath = renderer != null
                ? (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
                    .GetReference(renderer, false)?.Path
                : null;
            var effectivePath = scenePath != null ? scenePath : rendererPath;

            var materials = renderer != null ? renderer.sharedMaterials : null;
            var submeshesByBinding = new Dictionary<int, List<SubmeshSeparationPlan>>();
            foreach (var submesh in plan.Submeshes)
            {
                if (!submeshesByBinding.TryGetValue(submesh.SourceMaterialBindingIndex, out var list))
                {
                    list = new List<SubmeshSeparationPlan>();
                    submeshesByBinding.Add(submesh.SourceMaterialBindingIndex, list);
                }
                list.Add(submesh);
            }

            var slotCount = materials != null ? materials.Length : (slotResults != null ? slotResults.Count : 0);
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                var mat = materials != null && slotIndex < materials.Length ? materials[slotIndex] : null;
                var sourceMat = mat != null ? RegisteredSourceIdentity.Resolve(mat) as Material ?? mat : null;
                var sourcePath = sourceMat != null ? AssetDatabase.GetAssetPath(sourceMat) : string.Empty;

                if (slotResults != null && slotIndex < slotResults.Count && !slotResults[slotIndex].IsResolved)
                {
                    var total = SubmeshPolygonCount(renderer, slotIndex);
                    state.PlayModeRendererRows.Add(new SerializedTransformationRow
                    {
                        kind = 3,
                        rendererPath = effectivePath,
                        rendererTypeName = rendererTypeName,
                        sourceMaterialPath = sourcePath,
                        slotIndex = slotIndex,
                        movedTriangles = -1,
                        provenTriangles = -1,
                        totalTriangles = total,
                        sentence = "Refused: AMUSE currently does not support this material"
                    });
                    continue;
                }

                if (submeshesByBinding.TryGetValue(slotIndex, out var submeshes))
                {
                    var proven = 0;
                    var total = 0;
                    var isCandidate = false;
                    foreach (var s in submeshes)
                    {
                        proven += s.OpaqueTriangleOrdinals.Count;
                        total += s.OpaqueTriangleOrdinals.Count + s.TransparentTriangleOrdinals.Count;
                        if (s.Disposition == SubmeshSeparationDisposition.WhollyOpaqueCandidate ||
                            s.Disposition == SubmeshSeparationDisposition.Split)
                        {
                            isCandidate = true;
                        }
                    }

                    string sentence;
                    if (isCandidate)
                    {
                        if (total > 0 && proven == total)
                        {
                            sentence = "Convertable: " + total + " out of " + total + " polygons may be moved";
                        }
                        else if (proven > 0)
                        {
                            sentence = "Splittable: " + proven + " of out " + total + " polygons may be moved";
                        }
                        else
                        {
                            sentence = "Untouchable: 0 of out " + total + " polygons may be moved";
                        }
                    }
                    else
                    {
                        sentence = "Untouchable: 0 of out " + total + " polygons may be moved";
                    }

                    state.PlayModeRendererRows.Add(new SerializedTransformationRow
                    {
                        kind = 3,
                        rendererPath = effectivePath,
                        rendererTypeName = rendererTypeName,
                        sourceMaterialPath = sourcePath,
                        slotIndex = slotIndex,
                        movedTriangles = 0,
                        provenTriangles = proven,
                        totalTriangles = total,
                        sentence = sentence
                    });
                }
            }
        }
    }
}
