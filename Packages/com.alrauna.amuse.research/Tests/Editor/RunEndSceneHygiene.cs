using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Alrauna.Amuse.Research.Tests.Editor
{
    /// <summary>
    /// Assembly-wide scene hygiene for the EditMode population, matching
    /// the main test assembly's <c>RunEndSceneHygiene</c>. The teardown
    /// discards every dirty open scene: preview scenes are closed through
    /// their own API, saved scenes are re-opened from disk, and an
    /// untitled scene is replaced wholesale. The population therefore
    /// never ends with a modified scene open. Discard semantics are
    /// deliberate: the test editor is a dedicated lab instance, and
    /// fixture state is the only thing a population ever dirties.
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
                Assert.That(
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
