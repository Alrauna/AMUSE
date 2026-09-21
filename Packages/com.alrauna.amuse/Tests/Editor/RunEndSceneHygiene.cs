using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Alrauna.Amuse.Tests.Editor
{
    /// <summary>
    /// Assembly-wide scene hygiene for the EditMode population. NUnit runs
    /// this SetUpFixture once for every test in the assembly. The teardown
    /// discards every dirty open scene: preview scenes are closed through
    /// their own API, saved scenes are re-opened from disk, and an
    /// untitled scene is replaced wholesale. The population therefore
    /// never ends with a modified scene open, which is what raised
    /// Unity's scene-save dialog over the editor and wedged it. Each
    /// discard restarts the scan, because a wholesale untitled
    /// replacement collapses the scene collection.
    /// <para>
    /// Discard semantics are deliberate and documented: the test editor is
    /// a dedicated lab instance, and every scene a population dirties is
    /// fixture state, never user content. NDMF's preview scene is
    /// service-owned and re-opens clean when a build needs it.
    /// </para>
    /// </summary>
    [SetUpFixture]
    public sealed class RunEndSceneHygiene
    {
        [OneTimeTearDown]
        public void LeaveNoModifiedSceneOpen()
        {
            // The discard mutates the scene collection (a wholesale
            // untitled replacement collapses it), so each discard
            // restarts the scan instead of walking stale indices. The
            // capped restart loop mirrors
            // TransientUnlockTestLifecycle.AssertNoSavedSceneDirty.
            var scans = 0;
            var discarded = true;
            while (discarded && scans < 8)
            {
                discarded = false;
                scans++;
                for (var index = SceneManager.sceneCount - 1;
                     index >= 0;
                     index--)
                {
                    var scene = SceneManager.GetSceneAt(index);
                    if (!scene.isDirty)
                    {
                        continue;
                    }

                    if (EditorSceneManager.IsPreviewScene(scene))
                    {
                        EditorSceneManager.ClosePreviewScene(scene);
                        discarded = true;
                    }
                    else if (!string.IsNullOrEmpty(scene.path))
                    {
                        EditorSceneManager.OpenScene(scene.path);
                        discarded = true;
                    }
                    else
                    {
                        EditorSceneManager.NewScene(
                            NewSceneSetup.EmptyScene, NewSceneMode.Single);
                        discarded = true;
                    }

                    break;
                }
            }

            // The tripwire. The discard above is best effort; a scene it
            // cannot clean must fail here instead of raising the
            // scene-save dialog over the editor later.
            for (var index = 0;
                 index < SceneManager.sceneCount;
                 index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                NUnit.Framework.Assert.That(
                    scene.isDirty,
                    Is.False,
                    "an open scene was left modified: " +
                    (string.IsNullOrEmpty(scene.path)
                        ? scene.name + " (untitled)"
                        : scene.path));
            }
        }
    }
}
