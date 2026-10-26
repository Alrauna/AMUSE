using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Runs extension-free and first, before the animator scope
    /// virtualizes the graph. The structural refusals read the
    /// committed graph, and the event scan resolves authored originals
    /// through the NDMF object registry, so an event a plugin clone
    /// dropped still refuses the build (V3, owner decision of
    /// 2026-10-25). Stores the enumerated graph and its refusal for
    /// the passes that follow. This pass changes nothing and reports
    /// nothing. The barrier reports the stored refusal behind the
    /// lifecycle gates.
    /// </summary>
    internal static class AmuseStructuralGraphCheck
    {
        internal static void Execute(BuildContext context)
        {
            var state = context.GetState<AmusePlatformFinishState>();
            var graph = CommittedControllerGraph.Enumerate(
                context.AvatarRootObject,
                state.AnimatorBindings
                    ?? VRChatPlatformAnimatorBindings.Instance);
            state.StructuralGraph = graph;
            state.AvatarRefusal = graph.Refusal;
        }
    }
}
