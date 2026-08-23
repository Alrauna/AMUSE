using System;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
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
    }

    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal sealed class AmusePlatformFinishPlugin : Plugin<AmusePlatformFinishPlugin>
    {
        internal const string PluginQualifiedName = "com.alrauna.amuse";
        internal const string BarrierPassName = "AMUSE semantic barrier";

        public override string QualifiedName => PluginQualifiedName;
        public override string DisplayName => "AMUSE";

        protected override void Configure()
        {
            InPhase(BuildPhase.PlatformFinish)
                .Run(BarrierPassName, AmusePlatformFinishPass.Execute);
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
