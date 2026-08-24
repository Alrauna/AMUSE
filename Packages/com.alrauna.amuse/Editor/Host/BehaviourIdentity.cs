using System;
using System.Collections.Generic;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Editor.Host
{
    internal static class BehaviourIdentity
    {
        private static readonly string[] AllowedIdentityValues =
            Array.Empty<string>();

        // Verification obligation 7: add an identity only with a recorded
        // justification that its effects are confined to animator parameters,
        // layer/playable weights, state selection, or another already-admitted
        // effect. Task 14 establishes no such positive authorization.
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

            return false;
        }
    }
}
