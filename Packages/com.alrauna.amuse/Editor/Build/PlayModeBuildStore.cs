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
