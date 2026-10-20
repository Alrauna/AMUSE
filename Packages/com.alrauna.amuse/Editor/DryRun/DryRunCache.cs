using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace Alrauna.Amuse.Editor.DryRun
{
    /// <summary>
    /// One immutable record of everything cheap the fingerprints read.
    /// It is a plain value, so production fills it from the scene and
    /// tests build it by hand. No field holds a Unity object, so the
    /// cache never pins scene memory and never leaks an avatar.
    /// </summary>
    internal readonly struct DryRunFingerprintInputs
    {
        internal DryRunFingerprintInputs(
            int avatarRootInstanceId,
            BuildTarget activeTarget,
            string policyFingerprint,
            string controllerFingerprint,
            string graphFingerprint)
        {
            AvatarRootInstanceId = avatarRootInstanceId;
            ActiveTarget = activeTarget;
            PolicyFingerprint = policyFingerprint;
            ControllerFingerprint = controllerFingerprint;
            GraphFingerprint = graphFingerprint;
        }

        internal int AvatarRootInstanceId { get; }
        internal BuildTarget ActiveTarget { get; }

        /// <summary>All nineteen component values, joined by the builder.</summary>
        internal string PolicyFingerprint { get; }

        /// <summary>Controller identities and ticks, joined by the builder.</summary>
        internal string ControllerFingerprint { get; }

        /// <summary>The structural graph outcome.</summary>
        internal string GraphFingerprint { get; }
    }

    /// <summary>
    /// Builds the cache key strings. Every join uses one separator that
    /// cannot appear in a path or a component value, so two different
    /// inputs can never concatenate into one key. The builder only
    /// formats values it is handed, so it never touches the file system.
    /// </summary>
    internal static class DryRunFingerprint
    {
        /// <summary>
        /// The unit separator code point. Paths and GUIDs cannot
        /// contain it, so a join with it cannot collide.
        /// </summary>
        internal const string Separator = "\u001f";

        /// <summary>
        /// Joins every avatar-level fact: root instance ID, active
        /// target, policy, controllers, and graph. A platform switch
        /// must invalidate everything, so the target is part of the key.
        /// </summary>
        internal static string Avatar(in DryRunFingerprintInputs inputs)
        {
            return string.Join(
                Separator,
                inputs.AvatarRootInstanceId.ToString(CultureInfo.InvariantCulture),
                inputs.ActiveTarget.ToString(),
                inputs.PolicyFingerprint,
                inputs.ControllerFingerprint,
                inputs.GraphFingerprint);
        }

        /// <summary>
        /// Joins the avatar key and the renderer key into one composite
        /// string. The cache keys renderer rows by this string, so a
        /// renderer fact alone can never resolve a stored row.
        /// </summary>
        internal static string Composite(string avatar, string renderer)
        {
            return avatar + Separator + renderer;
        }
    }

    /// <summary>
    /// The dry run session cache. It holds one avatar slot and the rows
    /// computed for that avatar. One slot keeps memory bounded during a
    /// long session and makes a cross-avatar serve impossible, because a
    /// store under a new avatar removes every earlier row. The editor
    /// window runs on the main thread, so the static state needs no lock.
    /// </summary>
    internal static class DryRunCache
    {
        /// <summary>The single stored avatar key. Null when empty.</summary>
        private static string storedAvatarKey;

        private static IReadOnlyList<MaterialTransformationRow> storedAvatarRows;
        private static readonly Dictionary<string, IReadOnlyList<MaterialTransformationRow>> RendererRows =
            new Dictionary<string, IReadOnlyList<MaterialTransformationRow>>();

        private static readonly HashSet<string> ErroredKeys = new HashSet<string>();

        /// <summary>
        /// Returns the rows stored for this avatar key. A key outside
        /// the single slot is a miss by definition.
        /// </summary>
        internal static bool TryGetAvatar(
            string avatarKey, out IReadOnlyList<MaterialTransformationRow> rows)
        {
            if (avatarKey != null && avatarKey == storedAvatarKey && storedAvatarRows != null)
            {
                rows = storedAvatarRows;
                return true;
            }

            rows = null;
            return false;
        }

        /// <summary>
        /// Stores the rows for one avatar. A new avatar key clears the
        /// slot first, so only one avatar lives in the cache at a time.
        /// </summary>
        internal static void StoreAvatar(string avatarKey, IReadOnlyList<MaterialTransformationRow> rows)
        {
            if (avatarKey == null)
            {
                return;
            }

            if (avatarKey != storedAvatarKey)
            {
                Clear();
                storedAvatarKey = avatarKey;
            }

            storedAvatarRows = rows;
        }

        /// <summary>
        /// Returns the rows stored under one composite key. The
        /// composite carries the avatar fingerprint, so a stale policy,
        /// target, or graph turns the lookup into a miss.
        /// </summary>
        internal static bool TryGetRenderer(
            string compositeKey, out IReadOnlyList<MaterialTransformationRow> rows)
        {
            if (compositeKey != null && RendererRows.TryGetValue(compositeKey, out var stored))
            {
                rows = stored;
                return true;
            }

            rows = null;
            return false;
        }

        /// <summary>
        /// Stores renderer rows under the composite of this avatar key
        /// and this renderer key. A new avatar key clears everything
        /// first, so avatar one can never serve avatar two.
        /// </summary>
        internal static void StoreRenderer(
            string avatarKey, string rendererKey, IReadOnlyList<MaterialTransformationRow> rows)
        {
            if (avatarKey == null || rendererKey == null)
            {
                return;
            }

            if (avatarKey != storedAvatarKey)
            {
                Clear();
                storedAvatarKey = avatarKey;
            }

            RendererRows[DryRunFingerprint.Composite(avatarKey, rendererKey)] = rows;
        }

        /// <summary>Marks one composite key as errored for this session.</summary>
        internal static void MarkErrored(string compositeKey)
        {
            if (compositeKey == null)
            {
                return;
            }

            ErroredKeys.Add(compositeKey);
        }

        /// <summary>Returns true while the mark survives. Clear removes it.</summary>
        internal static bool IsErrored(string compositeKey)
        {
            return compositeKey != null && ErroredKeys.Contains(compositeKey);
        }

        /// <summary>Drops every row, mark, and the avatar slot.</summary>
        internal static void Clear()
        {
            storedAvatarKey = null;
            storedAvatarRows = null;
            RendererRows.Clear();
            ErroredKeys.Clear();
        }
    }
}
