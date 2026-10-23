using System;
using System.Collections.Generic;
using Alrauna.Amuse.Runtime;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The row shapes the inspector can draw. The orchestrator picks
    /// the shape, the cache stores it, and the drawer indents and
    /// labels from it, so one display decision lives in exactly one
    /// place.
    /// The numbers are a stored wire contract. Records stored
    /// earlier in this session keep loading. The v2 session prefix
    /// means older-release records are never read. The values must
    /// not change.
    /// </summary>
    internal enum MaterialTransformationRowKind
    {
        Banner = 0,
        Note = 1,
        Renderer = 2,
        Slot = 3,
        Errored = 4,
    }

    [Serializable]
    internal struct SerializedTransformationRow
    {
        public MaterialTransformationRowKind kind;
        public string rendererPath;
        public string rendererTypeName;
        public string sourceMaterialPath;
        public int slotIndex;
        public int movedTriangles;
        public int provenTriangles;
        public int totalTriangles;
        public string sentence;
    }

    [Serializable]
    internal sealed class PlayModeBuildRecord
    {
        public string avatarKey;
        public string policyFingerprint;
        public SerializedTransformationRow[] avatarRows;
        public SerializedTransformationRow[] rendererRows;
    }

    /// <summary>
    /// Play records belong to one proved avatar root. The key is the full
    /// valid global identity string of the avatar object in a loaded saved
    /// scene in edit mode, and a captured native reference match in Play
    /// mode. An empty key authorizes no save, load, or clear. The session
    /// prefix changed with the proved key, so old name-key records cannot
    /// load.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeBuildStore
    {
        private const string SessionPrefix = "Amuse_PlayMode_v2_";
        private const string PendingEntryKey = "Amuse_RootIdentity_PendingEntry";

        // Recovered owned snapshot. The overloaded Unity null treats a
        // destroyed snapshot as absent, so a stale field after a teardown or
        // a domain reload rescans through the strict recovery.
        private static PlayModeRootIdentitySnapshot _recovered;

        static PlayModeBuildStore()
        {
            // The lifecycle subscribes once per domain, independent of any
            // inspector instance.
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (SessionState.GetBool(PendingEntryKey, false))
            {
                EditorApplication.update += PendingGuard;
            }
            if (!EditorApplication.isPlaying &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // an orphan from an interrupted run is cleaned when the
                // editor loads idle, never during a Play transition
                EditorApplication.delayCall += IdleCheck;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                CaptureSnapshotForEntry();
            }
            else if (change == PlayModeStateChange.EnteredPlayMode)
            {
                // a real entry: the pending wait is over
                SessionState.SetBool(PendingEntryKey, false);
                EditorApplication.update -= PendingGuard;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                // canceled entry or normal exit: recover and destroy now,
                // but only while the editor is idle, so an immediate new
                // entry keeps its fresh capture
                if (!EditorApplication.isPlaying &&
                    !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    DestroyOwnedSnapshot();
                    SessionState.SetBool(PendingEntryKey, false);
                    EditorApplication.update -= PendingGuard;
                }
                // the delayed check handles idle-load orphan cleanup only
                EditorApplication.delayCall += IdleCheck;
            }
        }

        /// <summary>
        /// The reviewed idle predicate. A real Play start ends the pending
        /// wait. An idle editor with a stuck pending flag runs the owned
        /// idle cleanup. A Play transition in flight keeps waiting.
        /// </summary>
        private static void PendingGuard()
        {
            if (!SessionState.GetBool(PendingEntryKey, false))
            {
                EditorApplication.update -= PendingGuard;
                return;
            }
            if (EditorApplication.isPlaying)
            {
                SessionState.SetBool(PendingEntryKey, false);
                EditorApplication.update -= PendingGuard;
                return;
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                IdleCheck();
                SessionState.SetBool(PendingEntryKey, false);
                EditorApplication.update -= PendingGuard;
            }
        }

        /// <summary>
        /// Runs after native restoration. Both Play flags are rechecked, the
        /// owned snapshot is recovered and destroyed, the pending flag is
        /// cleared, and the update guard is removed.
        /// </summary>
        private static void IdleCheck()
        {
            if (EditorApplication.isPlaying ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            DestroyOwnedSnapshot();
            SessionState.SetBool(PendingEntryKey, false);
            EditorApplication.update -= PendingGuard;
        }

        /// <summary>
        /// Captures a fresh snapshot once per real entry, before the editor
        /// leaves edit mode. A previous owned snapshot is destroyed first.
        /// An empty candidate set creates no holder.
        /// </summary>
        private static void CaptureSnapshotForEntry()
        {
            DestroyOwnedSnapshot();
            SessionState.SetBool(PendingEntryKey, true);
            EditorApplication.update -= PendingGuard;
            EditorApplication.update += PendingGuard;

            var roots = new List<GameObject>();
            var keys = new List<string>();
            CollectAuthoredPairs(roots, keys);
            if (roots.Count == 0) return;

            var snapshot = ScriptableObject.CreateInstance<
                PlayModeRootIdentitySnapshot>();
            snapshot.hideFlags = HideFlags.HideAndDontSave;
            snapshot.Initialize(roots.ToArray(), keys.ToArray());
            _recovered = snapshot;
        }

        /// <summary>
        /// Enumerates optimizer components in normal loaded saved scenes,
        /// including inactive and nested objects. Persistent assets never
        /// enter this scan, preview scenes and unsaved scenes are rejected,
        /// and an invalid identity is rejected per root. The component
        /// disallows duplicates on one object, and distinct objects cannot
        /// share a global id, so no key dedup is needed.
        /// </summary>
        private static void CollectAuthoredPairs(
            List<GameObject> roots, List<string> keys)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                if (string.IsNullOrEmpty(scene.path)) continue;
                if (EditorSceneManager.IsPreviewScene(scene)) continue;
                foreach (var sceneRoot in scene.GetRootGameObjects())
                {
                    foreach (var optimizer in sceneRoot
                                 .GetComponentsInChildren<AmuseAvatarOptimizer>(
                                     true))
                    {
                        var key = AuthoredKey(
                            optimizer.transform.gameObject);
                        if (key == null) continue;
                        roots.Add(optimizer.transform.gameObject);
                        keys.Add(key);
                    }
                }
            }
        }

        /// <summary>
        /// The proved authored identity: the full global id string of a
        /// living scene object under the committed valid identity contract.
        /// Persistent assets, invalid scenes, unloaded scenes, unsaved
        /// scenes, and preview scenes are rejected before the global id is
        /// read, so a scene without an asset yields null.
        /// </summary>
        private static string AuthoredKey(GameObject root)
        {
            if (root == null || EditorUtility.IsPersistent(root)) return null;
            var scene = root.scene;
            if (!scene.IsValid() || !scene.isLoaded) return null;
            if (string.IsNullOrEmpty(scene.path)) return null;
            if (EditorSceneManager.IsPreviewScene(scene)) return null;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(root);
            if (id.identifierType != 2) return null;
            if (id.assetGUID.Equals(default(UnityEditor.GUID))) return null;
            if (id.targetObjectId == 0) return null;
            return id.ToString();
        }

        /// <summary>
        /// Full recovery scan by exact type, non-persistence, and the
        /// HideAndDontSave flag. More than one owned snapshot is an
        /// invariant failure. Capture and teardown always take this path so
        /// a second owned snapshot is detected even while the cached field
        /// is still alive.
        /// </summary>
        private static PlayModeRootIdentitySnapshot RecoverSnapshot()
        {
            PlayModeRootIdentitySnapshot found = null;
            foreach (var candidate in
                     Resources.FindObjectsOfTypeAll
                         <PlayModeRootIdentitySnapshot>())
            {
                if (candidate == null) continue;
                if (EditorUtility.IsPersistent(candidate)) continue;
                if (candidate.hideFlags != HideFlags.HideAndDontSave) continue;
                if (found != null)
                {
                    throw new InvalidOperationException(
                        "multiple owned root identity snapshots");
                }
                found = candidate;
            }
            _recovered = found;
            return found;
        }

        /// <summary>
        /// Hot path for key computation: the cached snapshot is reused and
        /// the full recovery scan only fills an absent cache.
        /// </summary>
        private static PlayModeRootIdentitySnapshot RecoverSnapshotCached()
        {
            if (_recovered != null) return _recovered;
            return RecoverSnapshot();
        }

        private static void DestroyOwnedSnapshot()
        {
            var snapshot = RecoverSnapshot();
            if (snapshot != null)
            {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
            _recovered = null;
        }

        /// <summary>
        /// In edit mode the key is the recomputed authored scene identity.
        /// In Play mode only a captured live native reference authorizes a
        /// key; an uncaptured root gets no key and is never healed.
        /// </summary>
        public static string ComputeAvatarKey(Transform avatarRoot)
        {
            if (avatarRoot == null) return string.Empty;
            if (EditorApplication.isPlaying)
            {
                // a session with an empty candidate set has no holder, and
                // that state is the contract empty key, not a defect
                var snapshot = RecoverSnapshotCached();
                if (snapshot == null) return string.Empty;
                return snapshot.KeyFor(avatarRoot.gameObject);
            }
            return AuthoredKey(avatarRoot.gameObject) ?? string.Empty;
        }

        public static string ComputePolicyFingerprint(
            AmuseAvatarOptimizer component,
            BuildTarget activeTarget)
        {
            if (component == null) return string.Empty;
            return string.Join(";",
                activeTarget.ToString(),
                component.AmuseDisabled ? "1" : "0",
                component.AlphaSeparatorEnabled ? "1" : "0",
                component.CutoutSeparatorEnabled ? "1" : "0",
                component.TransparentSeparatorEnabled ? "1" : "0",
                component.PreserveTransparencyMaxMipLevel,
                component.PreserveTransparencyMinTextureSize,
                component.MinimumOpaqueCoveragePercent,
                component.MinimumOpaqueCoverageToPreservePercent,
                component.CutoutPreserveTransparencyMaxMipLevel,
                component.CutoutPreserveTransparencyMinTextureSize,
                component.CutoutMinimumOpaqueCoveragePercent,
                component.CutoutMinimumOpaqueCoverageToPreservePercent,
                component.MinimumOpaqueAlphaPercent,
                component.PolygonAlphaUpperClampPercent,
                component.PolygonMinimumOpaqueCoveragePercent,
                component.AcceptDesktopAnisotropyDifferences ? "1" : "0",
                component.AcceptMobileAnisotropyDifferences ? "1" : "0",
                component.IgnoreOutOfRangeMaterialSlots ? "1" : "0",
                component.AllowDepthTestChange ? "1" : "0");
        }

        public static void Save(PlayModeBuildRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.avatarKey)) return;
            var json = JsonUtility.ToJson(record);
            SessionState.SetString(SessionPrefix + record.avatarKey, json);
        }

        public static PlayModeBuildRecord TryLoad(string avatarKey, string expectedFingerprint)
        {
            if (string.IsNullOrEmpty(avatarKey)) return null;
            var key = SessionPrefix + avatarKey;
            var json = SessionState.GetString(key, null);
            if (string.IsNullOrEmpty(json)) return null;
            var record = ParseCached(json);
            if (record == null)
            {
                // Corrupt payloads are stale data, not defects: erase and start over.
                SessionState.EraseString(key);
                return null;
            }
            if (record.policyFingerprint != expectedFingerprint)
            {
                return null;
            }
            return record;
        }

        private static string cachedRawRecord;
        private static PlayModeBuildRecord cachedParsedRecord;

        // Only the JsonUtility parse is cached. The policy-fingerprint comparison
        // in TryLoad stays live on every call so a platform switch with an
        // unchanged record string still fails the match.
        internal static PlayModeBuildRecord ParseCached(string rawRecord)
        {
            if (rawRecord == cachedRawRecord) return cachedParsedRecord;
            PlayModeBuildRecord parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<PlayModeBuildRecord>(rawRecord);
            }
            catch (ArgumentException)
            {
                parsed = null;
            }
            cachedRawRecord = rawRecord;
            cachedParsedRecord = parsed;
            return parsed;
        }

        public static void Clear(string avatarKey)
        {
            if (string.IsNullOrEmpty(avatarKey)) return;
            SessionState.EraseString(SessionPrefix + avatarKey);
        }

        internal static void SaveOutcome(
            BuildContext context,
            AmusePlatformFinishState state)
        {
            if (context == null || state == null) return;
            var avatarRoot = context.AvatarRootObject;
            var avatarKey = ComputeAvatarKey(
                avatarRoot != null ? avatarRoot.transform : null);
            var optimizer = avatarRoot != null
                ? avatarRoot.GetComponent<AmuseAvatarOptimizer>()
                : null;
            var target = state.SamplingTarget != 0
                ? state.SamplingTarget
                : EditorUserBuildSettings.activeBuildTarget;
            var fingerprint = ComputePolicyFingerprint(
                optimizer, target);

            var record = new PlayModeBuildRecord
            {
                avatarKey = avatarKey,
                policyFingerprint = fingerprint,
                avatarRows = state.PlayModeAvatarRows.ToArray(),
                rendererRows = state.PlayModeRendererRows.ToArray()
            };

            Save(record);
        }
    }
}
