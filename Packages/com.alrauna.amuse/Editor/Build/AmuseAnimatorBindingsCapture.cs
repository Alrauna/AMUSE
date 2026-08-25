using System;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Retains the host's animator bindings for the extension-free PlatformFinish
    /// barrier.
    ///
    /// NDMF exposes <see cref="IPlatformAnimatorBindings"/> only through an active
    /// <see cref="AnimatorServicesContext"/>: the backing field is assigned in that
    /// extension's activation and the accessor otherwise throws. The barrier
    /// deliberately declares no extension, so that it observes the *committed*
    /// animator graph — NDMF commits controllers when the extension deactivates.
    /// Those two facts together mean the bindings must be acquired in an earlier,
    /// extension-declaring pass and held across deactivation. This pass is that
    /// acquisition and nothing else.
    ///
    /// The retained reference is a live, transient HOST CAPABILITY, not proof
    /// evidence. It is deliberately excluded from the immutable captured-evidence
    /// graph and its no-live-Unity-object guarantee; it exists so that later passes
    /// can *build* immutable evidence, and must never itself be treated as evidence
    /// or outlive that use. The reference is stored exactly as the host supplied it:
    /// never cloned, wrapped, reconstructed, or re-resolved.
    /// </summary>
    internal static class AmuseAnimatorBindingsCapture
    {
        internal static void Execute(BuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.GetState<AmusePlatformFinishState>().AnimatorBindings =
                context.Extension<AnimatorServicesContext>()
                    .ControllerContext.PlatformBindings;
        }
    }
}
