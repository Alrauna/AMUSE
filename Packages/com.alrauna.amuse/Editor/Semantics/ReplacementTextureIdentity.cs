using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Mints and guards the replacement-copy identity form. One source
    /// maps to one copy per build: a second distinct claimant poisons the
    /// identity, and a poisoned identity fails closed at the resolution
    /// gate. The session clears per build beside the generated-route cache.
    /// </summary>
    internal static class ReplacementTextureIdentity
    {
        private const string Prefix = "unity-replacement:";

        private static readonly Dictionary<string, int> Claimants =
            new Dictionary<string, int>();
        private static readonly HashSet<string> Poisoned =
            new HashSet<string>();

        internal static bool TryMint(
            Texture2D source, int copyInstanceId, out TextureSourceId id)
        {
            id = default;
            if (source == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    source, out var guid, out long localId) ||
                string.IsNullOrEmpty(guid))
            {
                return false;
            }
            var value = Prefix + guid.ToLowerInvariant() + ":" + localId;
            if (Poisoned.Contains(value))
            {
                return false;
            }
            if (Claimants.TryGetValue(value, out var knownInstanceId))
            {
                if (knownInstanceId != copyInstanceId)
                {
                    Poisoned.Add(value);
                    return false;
                }
                id = new TextureSourceId(value);
                return true;
            }
            Claimants[value] = copyInstanceId;
            id = new TextureSourceId(value);
            return true;
        }

        internal static bool IsUsable(TextureSourceId id)
        {
            return !Poisoned.Contains(id.Value);
        }

        internal static void PoisonForTests(TextureSourceId id)
        {
            Poisoned.Add(id.Value);
        }

        internal static void ClearSession()
        {
            Claimants.Clear();
            Poisoned.Clear();
        }
    }
}
