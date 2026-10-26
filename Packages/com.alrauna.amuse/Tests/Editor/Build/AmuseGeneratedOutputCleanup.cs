using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Deletes the generated output the suite leaves under NDMF's
    /// generated folder. NDMF's asset saver writes one directory per
    /// avatar directly under the root, named with the filtered avatar
    /// name, and holds the container asset files inside it. The suite
    /// names its fixture avatars with the AMUSE prefix, and the saver's
    /// reserved-name filter prefixes an underscore when a name holds a
    /// Windows reserved fragment anywhere, case insensitively, so both
    /// pinned prefixes land on the container directory names.
    /// Deletion stays inside the two pinned name prefixes. A prefixed
    /// directory goes with everything inside it and with its meta
    /// companion. Every entry outside the prefixes survives.
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
        /// A prefixed directory is one deletion unit and takes its meta
        /// companion with it. The meta file of a prefixed directory
        /// carries the same prefix, so it is planned with it. A
        /// prefixed file inside a prefixed directory dies with its
        /// directory and needs no entry of its own.
        /// </summary>
        internal static IReadOnlyList<string> Plan(string generatedRoot)
        {
            if (string.IsNullOrEmpty(generatedRoot) ||
                !Directory.Exists(generatedRoot))
            {
                return new string[0];
            }

            var planned = new List<string>();
            foreach (var directory in DirectoriesUnder(generatedRoot))
            {
                if (Owns(NameOf(directory)) &&
                    !SitsInsideOwnedDirectory(generatedRoot, directory))
                {
                    planned.Add(NameOf(directory));
                }
            }

            foreach (var file in FilesUnder(generatedRoot))
            {
                if (Owns(NameOf(file)) &&
                    !SitsInsideOwnedDirectory(generatedRoot, file))
                {
                    planned.Add(NameOf(file));
                }
            }

            return planned
                .Distinct()
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Deletes exactly the planned set. Every prefixed directory
        /// goes recursively with its meta companion, then every
        /// prefixed file outside the deleted directories goes on its
        /// own. Nothing else is touched.
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

            foreach (var directory in DirectoriesUnder(generatedRoot))
            {
                if (SitsInsideOwnedDirectory(generatedRoot, directory) ||
                    !plannedNames.Contains(NameOf(directory)))
                {
                    continue;
                }

                Directory.Delete(directory, true);
                var meta = directory + ".meta";
                if (File.Exists(meta))
                {
                    File.Delete(meta);
                }
            }

            // The file pass runs after the directory pass, so files that
            // died with their container are no longer on disk here.
            foreach (var file in FilesUnder(generatedRoot))
            {
                if (SitsInsideOwnedDirectory(generatedRoot, file))
                {
                    continue;
                }

                if (plannedNames.Contains(NameOf(file)))
                {
                    File.Delete(file);
                }
            }
        }

        private static string[] DirectoriesUnder(string generatedRoot)
        {
            return Directory.GetDirectories(
                generatedRoot, "*", SearchOption.AllDirectories);
        }

        private static string[] FilesUnder(string generatedRoot)
        {
            return Directory.GetFiles(
                generatedRoot, "*", SearchOption.AllDirectories);
        }

        private static string NameOf(string path)
        {
            return Path.GetFileName(path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
        }

        private static bool Owns(string fileName)
        {
            return OwnedNamePrefixes.Any(
                prefix => fileName.StartsWith(prefix,
                    System.StringComparison.Ordinal));
        }

        // True when any directory between the root and the entry holds a
        // pinned prefix. Such an entry dies with that directory.
        private static bool SitsInsideOwnedDirectory(
            string generatedRoot, string path)
        {
            var rootLength = Path.GetFullPath(generatedRoot).Length;
            var relative = Path.GetFullPath(path)
                .Substring(rootLength)
                .TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            var segments = relative.Split(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (Owns(segments[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
