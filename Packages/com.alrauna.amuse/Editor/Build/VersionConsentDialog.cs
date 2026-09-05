using System.Collections.Generic;
using UnityEditor;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Presenter seam for version consent. Returns true when the risk is
    /// accepted for this build. Implementations must not remember acceptance
    /// across builds: every build asks again (decision V8).
    /// </summary>
    internal delegate bool VersionConsentPresenter(
        IReadOnlyList<string> subjects);

    /// <summary>
    /// The production consent presenter. One consolidated dialog per build
    /// lists every unattested integration with its plain English reason. In
    /// batch mode there is no user to ask, so the build refuses without a
    /// dialog; that is the fail-closed direction.
    /// </summary>
    internal static class VersionConsentDialog
    {
        /// <summary>
        /// The full consent decision, factored so tests can drive batch and
        /// decline paths without touching Unity UI APIs. No subjects means
        /// nothing to ask; batch mode always refuses; otherwise the
        /// presenter decides.
        /// </summary>
        internal static bool ShouldProceed(
            IReadOnlyList<string> subjects,
            bool batchMode,
            VersionConsentPresenter presenter)
        {
            if (subjects == null || subjects.Count == 0)
            {
                return true;
            }

            if (batchMode)
            {
                return false;
            }

            return presenter(subjects);
        }

        internal static bool Present(IReadOnlyList<string> subjects)
        {
            var message = "AMUSE has not verified these versions:\n\n" +
                string.Join("\n", subjects) + "\n\n" +
                "Proceed with this upload anyway?";
            return EditorUtility.DisplayDialog(
                "AMUSE: unverified versions", message, "Proceed", "Cancel");
        }
    }
}