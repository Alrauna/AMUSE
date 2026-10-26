using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Deletes the generated output the suite leaves under NDMF's
    /// generated folder. NDMF's asset saver writes one root asset per
    /// build and one container file per saved texture, and the suite
    /// names its generated assets with the AMUSE prefix. The saver's
    /// reserved-name filter prefixes an underscore when a name holds a
    /// Windows reserved fragment anywhere, case insensitively, so the
    /// underscore form also belongs to the suite. Deletion stays
    /// inside the two pinned name prefixes. Every other file survives.
    /// </summary>
    internal static class AmuseGeneratedOutputCleanup
    {
        /// <summary>
        /// The default root: NDMF's generated folder inside its
        /// package. The value mirrors the internal temporary asset
        /// root of NDMF.
        /// </summary>
        internal const string DefaultGeneratedRoot =
            "Packages/nadena.dev.ndmf/__Generated";

        private static readonly string[] OwnedNamePrefixes =
            { "AMUSE ", "_AMUSE " };

        /// <summary>
        /// The names this class will delete under the given root. Every
        /// returned name starts with one of the two pinned prefixes.
        /// The meta file of a planned asset carries the same prefix, so
        /// it is planned with it.
        /// </summary>
        internal static IReadOnlyList<string> Plan(string generatedRoot)
        {
            if (string.IsNullOrEmpty(generatedRoot) ||
                !Directory.Exists(generatedRoot))
            {
                return new string[0];
            }

            return Directory.GetFiles(
                    generatedRoot, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(Owns)
                .Distinct()
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Deletes exactly the planned set: every file under the root
        /// whose name the plan returned. Nothing else is touched.
        /// </summary>
        internal static void Delete(string generatedRoot)
        {
            var planned = Plan(generatedRoot);
            if (planned.Count == 0)
            {
                return;
            }

            var plannedNames = new HashSet<string>(
                planned, System.StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles(
                generatedRoot, "*", SearchOption.AllDirectories))
            {
                if (plannedNames.Contains(Path.GetFileName(path)))
                {
                    File.Delete(path);
                }
            }
        }

        private static bool Owns(string fileName)
        {
            return OwnedNamePrefixes.Any(
                prefix => fileName.StartsWith(prefix,
                    System.StringComparison.Ordinal));
        }
    }
}
