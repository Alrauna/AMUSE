using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Reads an installed package version offline. One shared reader
    /// serves every producer-version seam, so each admission states its
    /// policy and none repeats the blocking mechanics.
    /// </summary>
    internal static class PackageVersionReader
    {
        internal static string ReadInstalledOrNull(string packageName)
        {
            // Offline listing never touches the network, so blocking
            // inside an editor build pass is safe. Unity 2022.3 has no
            // Request.WaitForCompletion, so block on IsCompleted; the
            // wait is bounded so an unresponsive package manager
            // refuses rather than wedging the editor.
            var request = Client.List(true);
            var spins = 0;
            while (!request.IsCompleted && spins < 30000)
            {
                spins++;
                System.Threading.Thread.Sleep(1);
            }
            if (request.Status != StatusCode.Success || request.Result == null)
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
