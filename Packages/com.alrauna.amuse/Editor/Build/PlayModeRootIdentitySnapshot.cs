using System;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Store-owned native snapshot for Play record keys: paired avatar root
    /// references and authored keys from one Play entry. The instance is non
    /// persistent and HideAndDontSave, so it lives only in the editor session
    /// and is never saved into an asset. Matching is Unity native equality
    /// between two living GameObjects, never CLR identity and never an
    /// integer join. A dead captured reference never aliases a reused native
    /// handle, and a foreign root yields no key and is never healed.
    /// </summary>
    internal sealed class PlayModeRootIdentitySnapshot : ScriptableObject
    {
        [SerializeField] private GameObject[] roots;
        [SerializeField] private string[] keys;

        /// <summary>
        /// Retains the constructed arrays without copying them. Throws when
        /// either array is null or the lengths differ.
        /// </summary>
        internal void Initialize(GameObject[] rootArray, string[] keyArray)
        {
            if (rootArray == null)
                throw new ArgumentNullException(nameof(rootArray));
            if (keyArray == null)
                throw new ArgumentNullException(nameof(keyArray));
            if (rootArray.Length != keyArray.Length)
                throw new ArgumentException(
                    "root and key arrays differ in length");
            roots = rootArray;
            keys = keyArray;
        }

        /// <summary>
        /// The authored key for an owned living root, or an empty string for
        /// anything else. Each captured reference is guarded against death
        /// before the native comparison, so a destroyed capture can never
        /// alias a recycled native handle.
        /// </summary>
        internal string KeyFor(GameObject root)
        {
            if (root == null || roots == null || keys == null) return string.Empty;
            for (var index = 0; index < roots.Length; index++)
            {
                if (roots[index] == null) continue;
                if (roots[index] == root) return keys[index];
            }
            return string.Empty;
        }
    }
}
