using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Runs extension-free, before any animator scope virtualizes the
    /// graph: the structural refusals read real authored clips, and
    /// animation events survive only before virtualization. Stores the
    /// enumerated graph and its refusal for the passes that follow.
    /// This pass changes nothing and reports nothing. The barrier
    /// reports the stored refusal behind the lifecycle gates.
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
