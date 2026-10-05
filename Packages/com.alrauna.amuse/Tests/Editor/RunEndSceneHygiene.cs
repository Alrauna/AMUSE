using Alrauna.Amuse.Tests.Editor.Build;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor
{
    /// <summary>
    /// Assembly-wide scene hygiene for the EditMode population. NUnit runs
    /// this SetUpFixture once for every test in the assembly. The teardown
    /// delegates to TransientUnlockTestLifecycle.AssertNoSavedSceneDirty,
    /// the shared static that discards every dirty open scene: preview
    /// scenes are closed through their own API, saved scenes are re-opened
    /// from disk, and an untitled scene is replaced wholesale. The
    /// population therefore never ends with a modified scene open, which
    /// is what raised Unity's scene-save dialog over the editor and wedged
    /// it.
    /// <para>
    /// Discard semantics are deliberate and documented: the test editor is
    /// a dedicated lab instance, and every scene a population dirties is
    /// fixture state, never user content. NDMF's preview scene is
    /// service-owned and re-opens clean when a build needs it.
    /// </para>
    /// <para>
    /// The research package carries a third near-verbatim copy of the
    /// discard in its own test assembly. It cannot delegate across
    /// assemblies, so that copy stays.
    /// </para>
    /// </summary>
    [SetUpFixture]
    public sealed class RunEndSceneHygiene
    {
        [OneTimeTearDown]
        public void LeaveNoModifiedSceneOpen()
        {
            // Tripwire: the shared static fails the population here if any
            // open scene still reports dirty after its best-effort
            // discard. See
            // TransientUnlockTestLifecycle.AssertNoSavedSceneDirty.
            TransientUnlockTestLifecycle.AssertNoSavedSceneDirty();
        }
    }
}
