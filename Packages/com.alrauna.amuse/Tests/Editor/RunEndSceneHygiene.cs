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
    /// Unity's scene-save dialog over the editor and wedged it.
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
                }
                else if (!string.IsNullOrEmpty(scene.path))
                {
                    EditorSceneManager.OpenScene(scene.path);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }

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
