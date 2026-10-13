using UnityEditor;

namespace Alrauna.Amuse.Editor.DryRun
{
    /// <summary>
    /// The production build-target seam for the dry run. The read is one
    /// editor property, so a Func-based injection keeps tests off the real
    /// editor state while production takes the active target live.
    /// </summary>
    internal sealed class EditorBuildTargetProvider
    {
        internal static readonly EditorBuildTargetProvider Instance =
            new EditorBuildTargetProvider();

        private EditorBuildTargetProvider()
        {
        }

        internal BuildTarget Get()
        {
            return EditorUserBuildSettings.activeBuildTarget;
        }
    }
}
