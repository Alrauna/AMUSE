using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

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
        private readonly List<UnityEngine.Object> _transient =
            new List<UnityEngine.Object>();

        /// <summary>
        /// The project-relative temp folder this scope owns, or null when
        /// the scope tracks objects only.
        /// </summary>
        internal string TempFolder { get; }

        internal TestTransientScope(string tempFolder = null)
        {
            TempFolder = tempFolder;
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
        /// Destroys every tracked object, clears the list, and deletes the
        /// temp folder when one is owned.
        /// </summary>
        internal void TearDown()
        {
            foreach (var obj in _transient)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            _transient.Clear();

            if (TempFolder != null && AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        /// <summary>Registers a transient object for teardown destruction.</summary>
        internal T Track<T>(T obj) where T : UnityEngine.Object
        {
            _transient.Add(obj);
            return obj;
        }
    }
}
