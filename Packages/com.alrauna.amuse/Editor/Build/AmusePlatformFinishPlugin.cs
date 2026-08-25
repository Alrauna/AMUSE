using System;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEngine;

[assembly: ExportsPlugin(typeof(Alrauna.Amuse.Editor.Build.AmusePlatformFinishPlugin))]

namespace Alrauna.Amuse.Editor.Build
{
    internal sealed class AmusePlatformFinishState
    {
        internal bool HasExecuted { get; set; }
        internal HostLifecycleCapability Lifecycle { get; set; }
        internal int AnalyzedRendererCount { get; set; }
        internal int SemanticallyRefusedRendererCount { get; set; }
        internal int OpaqueCandidateTriangleCount { get; set; }

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
            HostLifecycleCapability lifecycle)
        {
            state.Lifecycle = lifecycle;
            state.HasExecuted = true;
            if (!lifecycle.MayUsePositiveMutation)
            {
                return;
            }

            foreach (var renderer in context.AvatarRootObject
                         .GetComponentsInChildren<Renderer>(true))
            {
                var extraction = UnityRendererAlphaAnalysis.Capture(renderer);
                var analysis = extraction.Refusal ==
                               RendererAnalysisRefusal.None
                    ? UnityRendererAlphaAnalysis.Analyze(extraction.Snapshot)
                    : RendererAlphaAnalysis.Refused(extraction.Refusal);
                if (analysis.Refusal != RendererAnalysisRefusal.None)
                {
                    state.SemanticallyRefusedRendererCount++;
                    continue;
                }

                state.AnalyzedRendererCount++;
                state.OpaqueCandidateTriangleCount +=
                    analysis.Plan.OpaqueTriangleCount;
            }
        }
    }
}
