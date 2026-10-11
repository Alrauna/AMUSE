using Alrauna.Amuse.Editor.Analysis;
using UnityEditor;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The exact active-target selection of the proof policy. The
    /// selection is pure: it reads only its arguments, so tests drive
    /// every target without touching the editor's build target. Only
    /// the Windows target forms the desktop group, and only Android and
    /// iOS form the mobile group; every other target refuses selection
    /// and keeps the conservative mobile policy in the output slot.
    /// Selection never enables product texture capture: the existing
    /// capture admission keeps its own target gate.
    /// </summary>
    internal static class PlatformAlphaSamplingPolicy
    {
        internal static bool TrySelect(
            BuildTarget target,
            bool acceptDesktopDifferences,
            bool acceptMobileDifferences,
            out AlphaSamplingPolicy policy)
        {
            policy = AlphaSamplingPolicy.ConservativeMobile;
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                    policy = acceptDesktopDifferences
                        ? AlphaSamplingPolicy.AcceptedNonAnisotropic
                        : AlphaSamplingPolicy.ConservativeDesktop;
                    return true;
                case BuildTarget.Android:
                case BuildTarget.iOS:
                    policy = acceptMobileDifferences
                        ? AlphaSamplingPolicy.AcceptedNonAnisotropic
                        : AlphaSamplingPolicy.ConservativeMobile;
                    return true;
                default:
                    return false;
            }
        }
    }
}
