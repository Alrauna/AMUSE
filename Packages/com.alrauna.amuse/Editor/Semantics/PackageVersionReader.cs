using System;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Reads an installed package version offline. One shared reader
    /// serves every producer-version seam, so each admission states its
    /// policy and none repeats the blocking mechanics.
    /// </summary>
    internal static class PackageVersionReader
    {
        // The production test seam. Tests substitute this field, so a
        // test never touches the real package manager. Client.List
        // returns ListRequest in Unity 2022.3.
        internal static Func<bool, bool, ListRequest> ListPackages =
            (offline, includeIndirect) => Client.List(offline, includeIndirect);

        internal static string ReadInstalledOrNull(string packageName)
        {
            // Offline listing with indirect dependencies never
            // touches the network, so blocking inside an editor
            // build pass is safe. Reflection of the loaded Unity
            // 2022.3.22f1 assemblies on 2026-10-15 verified why the
            // loop exists: Request exposes only Error, IsCompleted,
            // and Status, and ListRequest adds no members. No
            // synchronous completion call exists, so polling
            // IsCompleted is the only way to block. The poll is
            // capped at 30000 rounds of a 1 ms sleep. That is at
            // least 30 seconds, because a sleep can return late.
            // Past the cap an unresponsive package manager reads as
            // a failed listing, so the caller refuses instead of
            // wedging the editor. Indirect dependencies are listed,
            // so a producer that arrives only as a dependency of
            // another package still admits.
            var request = ListPackages(true, true);
            if (request == null)
            {
                // A seam that answers no listing reads as a failed
                // listing, so the caller refuses rather than guesses.
                return null;
            }
            var spins = 0;
            while (!request.IsCompleted && spins < 30000)
            {
                spins++;
                System.Threading.Thread.Sleep(1);
            }
            if (request.Status != StatusCode.Success ||
                request.Result == null)
            {
                return null;
            }
            return VersionFor(request.Result, packageName);
        }

        /// <summary>
        /// Finds one package's version by exact name in a successful
        /// listing. A name that is not in the listing reads as null,
        /// so every caller refuses instead of guessing a version.
        /// </summary>
        internal static string VersionFor(
            IEnumerable<PackageInfo> packages,
            string packageName)
        {
            foreach (var package in packages)
            {
                if (package.name == packageName)
                {
                    return package.version;
                }
            }
            return null;
        }
    }
}
