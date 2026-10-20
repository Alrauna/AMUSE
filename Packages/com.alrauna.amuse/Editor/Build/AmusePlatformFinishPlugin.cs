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

        /// <summary>True when the avatar's cutout separator switch was
        /// off at the barrier capture, so cutout-sourced slots never
        /// separated. The apply pass consumes this beside
        /// <see cref="ReachedRendererAnalysis"/> to mark the avatar
        /// summary exactly once per analyzed build.</summary>
        internal bool CutoutSeparatorOff { get; set; }

        /// <summary>True when the avatar's transparent separator switch
        /// was off at the barrier capture, so transparent-sourced slots
        /// never separated. The apply pass consumes this beside
        /// <see cref="ReachedRendererAnalysis"/> to mark the avatar
        /// summary exactly once per analyzed build.</summary>
        internal bool TransparentSeparatorOff { get; set; }

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

        /// <summary>
        /// The material slots the applied writes actually changed, keyed by
        /// renderer and slot index. Apply derives this set from the same
        /// comparison that counts the applied opaque triangles: a slot is
        /// changed when the write's material differs from what the renderer
        /// held. A surviving slot whose already-opaque material maps to
        /// itself never enters the set, because its write kept the slot's
        /// material. A surviving Split never enters it either: the
        /// finalization keeps the Split's source material and moves its
        /// opaque triangles to the appended submesh. The play-mode record
        /// keys its moved-triangle rows on this set plus surviving Split
        /// dispositions. Live transient host capability, deliberately
        /// outside the evidence graph.
        /// </summary>
        internal HashSet<(Renderer renderer, int slot)>
            AppliedChangedMaterialSlots { get; } =
            new HashSet<(Renderer renderer, int slot)>();

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
        /// Test-only override for the shared build-target read. Null in
        /// production. The barrier pass and the apply staleness guard
        /// are the consumers; every other editor read site keeps the
        /// real setting.
        /// </summary>
        internal static Func<BuildTarget> ActiveBuildTargetForTests = null;

        /// <summary>
        /// Resolves the build target the sampling policy captures and
        /// re-selects against: the armed test override when present,
        /// otherwise the real active build target. Tests set and
        /// restore the override so EditMode runs never switch the
        /// editor's own build target. Internal because the barrier
        /// pass captures through it and the apply staleness guard
        /// re-selects through it, both across the class boundary.
        /// </summary>
        internal static BuildTarget ActiveBuildTarget()
        {
            return ActiveBuildTargetForTests != null
                ? ActiveBuildTargetForTests()
                : EditorUserBuildSettings.activeBuildTarget;
        }
    }

}
