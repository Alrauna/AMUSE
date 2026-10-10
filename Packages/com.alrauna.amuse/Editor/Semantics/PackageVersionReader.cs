using System;
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
            // Offline listing with indirect dependencies never touches
            // the network, so blocking inside an editor build pass is
            // safe. Unity 2022.3 has no
            // Request.WaitForCompletion, so block on IsCompleted; the
            // wait is bounded so an unresponsive package manager
            // refuses rather than wedging the editor. Indirect
            // dependencies are listed, so a producer that arrives only
            // as a dependency of another package still admits.
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
            foreach (var package in request.Result)
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
