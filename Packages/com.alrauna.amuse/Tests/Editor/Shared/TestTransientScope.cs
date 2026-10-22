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
    /// shape: tracked objects, no folder lifecycle.
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
        private string _priorScenePath;

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
            _priorScenePath = SceneManager.GetActiveScene().path;
            _ownedScene = EditorSceneManager.OpenScene(
                _ownedScenePath, OpenSceneMode.Additive);
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
                    _priorScenePath = null;
                }
            }
        }

        /// <summary>
        /// Closes only the owned scene copy, never saving it. An active owned
        /// scene is deactivated first, back to the still-loaded prior scene
        /// or any other loaded scene. Closing the scene destroys its
        /// GameObjects. The untitled runner scene and the user scene are
        /// never saved or closed.
        /// </summary>
        private void CloseOwnedSceneCopy()
        {
            if (!_ownedScene.IsValid() || !_ownedScene.isLoaded) return;
            var owned = _ownedScene;
            if (SceneManager.GetActiveScene() == owned)
            {
                var fallback = default(Scene);
                for (var index = 0; index < SceneManager.sceneCount; index++)
                {
                    var candidate = SceneManager.GetSceneAt(index);
                    if (candidate.path == owned.path || !candidate.isLoaded) continue;
                    if (candidate.path == _priorScenePath)
                    {
                        fallback = candidate;
                        break;
                    }
                    if (!fallback.IsValid()) fallback = candidate;
                }
                if (fallback.IsValid()) SceneManager.SetActiveScene(fallback);
            }
            EditorSceneManager.CloseScene(owned, true);
        }

        /// <summary>Registers a transient object for teardown destruction.</summary>
        internal T Track<T>(T obj) where T : UnityEngine.Object
        {
            _transient.Add(obj);
            return obj;
        }
    }
}
