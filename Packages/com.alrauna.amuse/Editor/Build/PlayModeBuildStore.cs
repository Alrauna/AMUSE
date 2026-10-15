using System;
using Alrauna.Amuse.Runtime;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    [Serializable]
    internal struct SerializedTransformationRow
    {
        public int kind;
        public string rendererPath;
        public string rendererTypeName;
        public string sourceMaterialPath;
        public int slotIndex;
        public int movedTriangles;
        public int provenTriangles;
        public int totalTriangles;
        public int refusedSlots;
        public int slotCount;
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

    internal static class PlayModeBuildStore
    {
        private const string SessionPrefix = "Amuse_PlayMode_";

        public static string ComputeAvatarKey(Transform avatarRoot)
        {
            if (avatarRoot == null) return string.Empty;
            var rootName = avatarRoot.name;
            if (rootName.EndsWith("(Clone)", StringComparison.Ordinal))
            {
                rootName = rootName.Substring(0, rootName.Length - 7).TrimEnd();
            }
            var scenePath = avatarRoot.gameObject.scene.path;
            if (string.IsNullOrEmpty(scenePath)) scenePath = avatarRoot.gameObject.scene.name;
            return scenePath + "::" + rootName;
        }

        public static string ComputePolicyFingerprint(
            AmuseAvatarOptimizer component,
            BuildTarget activeTarget)
        {
            if (component == null) return string.Empty;
            return string.Join(";",
                activeTarget.ToString(),
                component.PreserveTransparencyMaxMipLevel,
                component.PreserveTransparencyMinTextureSize,
                component.PolygonMinimumOpaqueCoveragePercent,
                component.MinimumOpaqueCoveragePercent,
                component.MinimumOpaqueCoverageToPreservePercent,
                component.AllowDepthTestChange,
                component.MinimumOpaqueAlphaPercent,
                component.PolygonAlphaUpperClampPercent);
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
            PlayModeBuildRecord record;
            try
            {
                record = JsonUtility.FromJson<PlayModeBuildRecord>(json);
            }
            catch (ArgumentException)
            {
                // Corrupt payloads are stale data, not defects: erase and start over.
                SessionState.EraseString(key);
                return null;
            }
            if (record == null || record.policyFingerprint != expectedFingerprint)
            {
                return null;
            }
            return record;
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
