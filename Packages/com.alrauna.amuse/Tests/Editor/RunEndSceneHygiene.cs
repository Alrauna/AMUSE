using Alrauna.Amuse.Tests.Editor.Build;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor
{
    /// <summary>
    /// Suite-start check and scene hygiene for the EditMode population.
    /// NUnit scopes this SetUpFixture to this namespace and its nested
    /// namespaces, which every test file of this assembly declares. The
    /// OneTimeSetUp runs VendorSuiteStartCheck.RunAll before the first
    /// test: it fails the suite start when a known NDMF plugin-disable
    /// key is set, when a locked vendor version mismatches its installed
    /// version, or when an installed VRChat avatar SDK lost its compile
    /// define, and it records the effective environment in one dated
    /// console line.
    /// <para>
    /// The teardown delegates to
    /// TransientUnlockTestLifecycle.AssertNoSavedSceneDirty, the shared
    /// static that discards every dirty open scene: preview scenes are
    /// closed through their own API, saved scenes are re-opened from
    /// disk, and an untitled scene is replaced wholesale. The population
    /// therefore never ends with a modified scene open, which is what
    /// raised Unity's scene-save dialog over the editor and wedged it.
    /// </para>
    /// <para>
    /// Discard semantics are deliberate and documented: the test editor is
    /// a dedicated lab instance, and every scene a population dirties is
    /// fixture state, never user content. NDMF's preview scene is
    /// service-owned and re-opens clean when a build needs it.
    /// </para>
    /// <para>
    /// The teardown also runs the session-state tripwire of
    /// VendorSessionStateGuard, so a vendor plugin-disable key a
    /// fixture left off its entry value fails here with its key named,
    /// and then the generated-output cleanup deletes the files the
    /// suite owns under NDMF's generated folder.
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
        [OneTimeSetUp]
        public void AssertTheLockedVendorEnvironment()
        {
            // Suite start: fail before the first test when the
            // environment is not the locked vendor one. Absent
            // packages and accepted drifts are recorded in one dated
            // console line. See VendorSuiteStartCheck.RunAll.
            VendorSuiteStartCheck.RunAll();
        }

        [OneTimeTearDown]
        public void LeaveNoModifiedSceneOpen()
        {
            // Tripwire: the shared static fails the population here if any
            // open scene still reports dirty after its best-effort
            // discard. See
            // TransientUnlockTestLifecycle.AssertNoSavedSceneDirty.
            TransientUnlockTestLifecycle.AssertNoSavedSceneDirty();

            // The session-state tripwire runs before the cleanup, so a
            // vendor plugin-disable key a fixture left set names its key
            // while the offender test still sits in the runner output.
            // See VendorSessionStateGuard.
            VendorSessionStateGuard.AssertAllTouchedKeysAtEntryValues();

            // The generated-output cleanup deletes only the files the
            // suite owns under NDMF's generated folder. See
            // AmuseGeneratedOutputCleanup.
            AmuseGeneratedOutputCleanup.Delete(
                AmuseGeneratedOutputCleanup.DefaultGeneratedRoot);
        }
    }
}
