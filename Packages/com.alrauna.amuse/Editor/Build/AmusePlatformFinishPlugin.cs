using System;
using nadena.dev.ndmf;

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
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var state = context.GetState<AmusePlatformFinishState>();
            if (state.HasExecuted)
            {
                throw new InvalidOperationException("AMUSE PlatformFinish barrier executed more than once.");
            }

            state.Lifecycle = HostLifecycleCapability.CaptureAndEvaluate(context);
            state.HasExecuted = true;
        }
    }
}
