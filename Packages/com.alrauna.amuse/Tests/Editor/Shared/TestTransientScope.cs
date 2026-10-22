using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alrauna.Amuse.Tests.Editor.Shared
{
    /// <summary>
    /// One editor test's transient lifetime: tracked in-memory objects for
    /// teardown destruction, plus an optional disposable temp folder under
    /// Assets. A null <paramref name="tempFolder"/> names the teardown-only
    /// shape: tracked objects, no folder lifecycle. When the scope opens a
    /// saved scene copy, teardown restores the scene that was active before
    /// the copy opened.
    /// </summary>
    internal sealed class TestTransientScope
    {
        /// <summary>
        /// The public empty scene fixture the scope copies for saved-scene
        /// roots. The editor generated it once. Tests never handwrite it.
        /// </summary>
        internal const string EmptyIdentityScenePath =
            "Packages/com.alrauna.amuse/Tests/Editor/ReferenceFixtures/EmptyIdentityScene.unity";

        private readonly List<UnityEngine.Object> _transient =
            new List<UnityEngine.Object>();

        private readonly bool _allowDestroyingAssets;

        private Scene _ownedScene;
        private string _ownedScenePath;

        // Scene handles, not paths. The runner's untitled scene has an
        // empty path, so only a handle can identify it.
        private Scene _priorScene;

        // Cross-scope bookkeeping for nested scopes. The first scope that
        // opens a copy records the scene active at that moment. A scope
        // whose own prior scene is gone restores that scene, so a saved
        // scene is never an arbitrary first pick.
        private static Scene _firstPriorScene;
        private static int _openOwnedCopies;

        // True when this scope holds one open-copy count. The count drops
        // in TearDown, so a copy that something else closed first still
        // drains the count.
        private bool _countedCopy;

        /// <summary>
        /// The project-relative temp folder this scope owns, or null when
        /// the scope tracks objects only.
        /// </summary>
        internal string TempFolder { get; }

        internal TestTransientScope(string tempFolder = null)
        {
            TempFolder = tempFolder;
        }

        internal TestTransientScope(bool allowDestroyingAssets, string tempFolder = null)
        {
            TempFolder = tempFolder;
            _allowDestroyingAssets = allowDestroyingAssets;
        }

        /// <summary>Creates the temp folder when it is absent.</summary>
        internal void EnsureTempFolder()
        {
            if (TempFolder != null &&
                !AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", Path.GetFileName(TempFolder));
            }
        }

        /// <summary>
        /// Creates a root inside the copied saved scene owned by this scope.
        /// The first call copies the public empty scene fixture into the scope
        /// folder, opens the copy additively, and leaves it active, so later
        /// fixture objects land in the same owned scene. The copy starts in
        /// its saved state; call <see cref="SaveOwnedScene"/> after additions
        /// to persist them.
        /// </summary>
        internal GameObject CreateSavedSceneRoot(string name)
        {
            EnsureOwnedSceneCopy();
            return new GameObject(name);
        }

        /// <summary>Saves only the owned scene copy of this scope.</summary>
        internal void SaveOwnedScene()
        {
            EnsureOwnedSceneCopy();
            if (!EditorSceneManager.SaveScene(_ownedScene, _ownedScenePath))
            {
                throw new InvalidOperationException(
                    "the owned scene copy could not be saved");
            }
        }

        /// <summary>
        /// Copies and opens the owned scene copy once per scope. Additive
        /// NewScene is never used, so the untitled scene state of the runner
        /// does not matter.
        /// </summary>
        private void EnsureOwnedSceneCopy()
        {
            if (_ownedScene.IsValid())
            {
                SceneManager.SetActiveScene(_ownedScene);
                return;
            }
            if (TempFolder == null)
            {
                throw new InvalidOperationException(
                    "a saved scene root needs a scope with a temp folder");
            }
            EnsureTempFolder();
            _ownedScenePath = TempFolder + "/OwnedIdentityScene.unity";
            if (!AssetDatabase.CopyAsset(EmptyIdentityScenePath, _ownedScenePath))
            {
                throw new InvalidOperationException(
                    "The public empty scene fixture could not be copied to " +
                    "the owned test folder.");
            }
            _priorScene = SceneManager.GetActiveScene();
            if (_openOwnedCopies == 0)
            {
                _firstPriorScene = _priorScene;
            }
            _ownedScene = EditorSceneManager.OpenScene(
                _ownedScenePath, OpenSceneMode.Additive);
            _openOwnedCopies++;
            _countedCopy = true;
            SceneManager.SetActiveScene(_ownedScene);
        }

        /// <summary>
        /// Destroys every tracked object, clears the list, and deletes the
        /// temp folder when one is owned.
        /// </summary>
        internal void TearDown()
        {
            try
            {
                CloseOwnedSceneCopy();
            }
            finally
            {
                try
                {
                    foreach (var obj in _transient)
                    {
                        if (obj != null)
                        {
                            UnityEngine.Object.DestroyImmediate(
                                obj, _allowDestroyingAssets);
                        }
                    }

                    _transient.Clear();

                    if (TempFolder != null && AssetDatabase.IsValidFolder(TempFolder))
                    {
                        AssetDatabase.DeleteAsset(TempFolder);
                    }
                }
                finally
                {
                    // scene ownership fields clear even when tracked
                    // destruction or the folder deletion throws above
                    _ownedScene = default;
                    _ownedScenePath = null;
                    _priorScene = default;
                    // the copy count drops here, so a copy that something
                    // else closed first still drains the session count
                    if (_countedCopy)
                    {
                        _countedCopy = false;
                        _openOwnedCopies--;
                        if (_openOwnedCopies == 0)
                        {
                            _firstPriorScene = default;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Closes only the owned scene copy, never saving it. An active owned
        /// scene is deactivated first. The restore pick is the scene active
        /// before this scope opened its copy, while that scene is still
        /// loaded. Next comes the scene active before any owned copy opened.
        /// Last comes any other loaded scene. An untitled scene comes before
        /// a saved scene. Closing the scene destroys its GameObjects. The
        /// untitled runner scene and the user scene are never saved or
        /// closed.
        /// </summary>
        private void CloseOwnedSceneCopy()
        {
            if (!_ownedScene.IsValid() || !_ownedScene.isLoaded) return;
            var owned = _ownedScene;
            if (SceneManager.GetActiveScene() == owned)
            {
                var restore = FindRestoreScene(owned);
                if (restore.IsValid()) SceneManager.SetActiveScene(restore);
            }
            EditorSceneManager.CloseScene(owned, true);
        }

        /// <summary>
        /// Picks the scene that becomes active when the owned copy closes.
        /// It compares scene handles, not paths. All untitled scenes have
        /// the same empty path, so a path cannot identify one of them.
        /// </summary>
        private Scene FindRestoreScene(Scene owned)
        {
            if (_priorScene.IsValid() && _priorScene.isLoaded &&
                _priorScene != owned)
            {
                return _priorScene;
            }
            if (_firstPriorScene.IsValid() && _firstPriorScene.isLoaded &&
                _firstPriorScene != owned)
            {
                return _firstPriorScene;
            }
            var untitled = default(Scene);
            var saved = default(Scene);
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var candidate = SceneManager.GetSceneAt(index);
                if (!candidate.isLoaded || candidate == owned) continue;
                if (candidate.path == "")
                {
                    if (!untitled.IsValid() &&
                        !EditorSceneManager.IsPreviewScene(candidate))
                    {
                        untitled = candidate;
                    }
                    continue;
                }
                if (!saved.IsValid()) saved = candidate;
            }
            if (untitled.IsValid()) return untitled;
            return saved;
        }

        /// <summary>Registers a transient object for teardown destruction.</summary>
        internal T Track<T>(T obj) where T : UnityEngine.Object
        {
            _transient.Add(obj);
            return obj;
        }
    }
}
