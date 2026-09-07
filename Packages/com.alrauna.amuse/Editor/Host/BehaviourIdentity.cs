using System;
using System.Collections.Generic;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Editor.Host
{
    internal static class BehaviourIdentity
    {
        /// <summary>
        /// The VRChat SDK3 state behaviours whose documented effects never
        /// read or write material, mesh, or renderer state. Each entry was
        /// added under verification obligation 7 with a recorded
        /// justification:
        ///
        /// - VRCAnimatorLayerControl adjusts playable-layer weights. The
        ///   capture model already treats every captured clip as potentially
        ///   active regardless of weight - the same conservative reachability
        ///   basis the animation-event refusal pins - so a weight change can
        ///   only reduce how much a clip applies, never add a state the
        ///   model does not already consider.
        /// - VRCAnimatorTrackingControl selects head, hand, and eye
        ///   tracking sources.
        /// - VRCAnimatorLocomotionControl turns locomotion on or off.
        /// - VRCAnimatorTemporaryPoseSpace toggles station pose space.
        /// - VRCAnimatorJumpAndGravity overrides jump and gravity.
        /// - VRCAnimatorPlayAudio plays an audio clip on an audio source.
        ///
        /// - VRCAvatarParameterDriver sets animator parameter values. The
        ///   proof model has no parameter or transition reachability
        ///   solver and treats every captured clip as potentially
        ///   reachable, so a parameter change cannot add a state the
        ///   model does not already consider.
        /// The tracking, locomotion, pose-space, jump-gravity, and
        /// play-audio behaviours touch no animator output AMUSE reasons
        /// about. Synced-layer overrides, animation events, and
        /// unvirtualized motions stay refused: they change or unbound what
        /// the captured clips mean.
        /// </summary>
        private static readonly string[]
            MaterialNeutralVrchatBehaviourFullNames =
            {
                "VRC.SDK3.Avatars.Components.VRCAnimatorLayerControl",
                "VRC.SDK3.Avatars.Components.VRCAnimatorTrackingControl",
                "VRC.SDK3.Avatars.Components.VRCAnimatorLocomotionControl",
                "VRC.SDK3.Avatars.Components.VRCAnimatorTemporaryPoseSpace",
                "VRC.SDK3.Avatars.Components.VRCAnimatorJumpAndGravity",
                "VRC.SDK3.Avatars.Components.VRCAnimatorPlayAudio",
                "VRC.SDK3.Avatars.Components.VRCAvatarParameterDriver",
            };

        private static readonly string[] AllowedIdentityValues =
            Array.Empty<string>();

        /// <summary>
        /// Identities pinned per exact string. The task-7 table stays for
        /// future non-VRChat admissions; it is empty today.
        /// </summary>
        internal static readonly IReadOnlyCollection<string> AllowedIdentities =
            Array.AsReadOnly(AllowedIdentityValues);

        internal static string Of(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var assembly = type.Assembly;
            var package = PackageInfo.FindForAssembly(assembly);
            // An unowned assembly is serialized exactly as
            // "<no-package>|<assembly-name>|<type-full-name>". The reserved
            // bracketed token cannot be mistaken for a package@version pair.
            var packageIdentity = package == null
                ? "<no-package>"
                : package.name + "@" + package.version;
            return packageIdentity + "|" + assembly.GetName().Name + "|" +
                   type.FullName;
        }

        internal static bool IsAllowed(string identity)
        {
            if (string.IsNullOrEmpty(identity)) return false;

            foreach (var allowedIdentity in AllowedIdentityValues)
            {
                if (string.Equals(
                        identity, allowedIdentity, StringComparison.Ordinal))
                    return true;
            }

            // The VRChat neutral set is matched on the owning SDK package
            // and the type's full name, never on the package version: a
            // range-admitted SDK patch must not silently re-refuse every
            // avatar, and the D8 consent layer separately covers the
            // unattested-version risk the version would otherwise pin.
            var parts = identity.Split('|');
            if (parts.Length != 3) return false;
            if (!parts[0].StartsWith(
                    "com.vrchat.avatars@", StringComparison.Ordinal) &&
                !parts[0].StartsWith(
                    "com.vrchat.base@", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var fullName in MaterialNeutralVrchatBehaviourFullNames)
            {
                if (string.Equals(
                        parts[2], fullName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
