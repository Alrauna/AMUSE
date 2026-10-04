using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Attests the Limitex Avatar Compressor replacement-copy shape.
    /// Every conjunct must hold; any miss refuses.
    /// </summary>
    // Upstream path (spec 2026-10-03, section 12). This admission is a
    // special case a future LAC release could delete. It retires when LAC
    // persists each copy as a sub-asset of a dedicated container asset, as
    // NDMF already does for AAO outputs, keeps registering source-and-copy
    // pairs in the object registry, publishes stable package versions, and
    // documents its per-release output shape. Until then this class, the
    // unity-replacement identity form, and the duplicate guard carry the
    // contract.
    internal static class ReplacementTextureAttestation
    {
        internal const string ProducerPackageName =
            "dev.limitex.avatar-compressor";
        internal const string ReplacementNameSuffix = "_compressed";

        // The admitted set is pinned, characterized data, never grown at
        // runtime. 0.9.0 joined on 2026-10-03: the installed Census Lab
        // copy's source shows the admitted shape (copies named source plus
        // "_compressed", registered source-and-copy pairs in the object
        // registry, forced streaming mipmaps flag, desktop formats
        // DXT1/DXT5/BC7/BC5), and a Lab build exercised the refusal path
        // end to end before admission (spec section 6).
        private static readonly string[] ProductionAdmittedVersions =
        {
            "0.9.0",
        };

        private static readonly List<string> Admitted =
            new List<string>(ProductionAdmittedVersions);

        internal static Func<string, string> ReadInstalledPackageVersionOrNull
        {
            get;
            set;
        } = ReadVersionFromPackageManager;

        internal static IReadOnlyList<string> AdmittedVersions => Admitted;

        internal static void SetAdmittedVersionsForTests(
            params string[] versions)
        {
            Admitted.Clear();
            if (versions != null)
            {
                Admitted.AddRange(versions);
            }
        }

        private static string CachedVersion;
        private static bool VersionReadCompleted;

        internal static void ResetForTests()
        {
            Admitted.Clear();
            Admitted.AddRange(ProductionAdmittedVersions);
            ReadInstalledPackageVersionOrNull = ReadVersionFromPackageManager;
            ClearVersionCacheForSession();
        }

        internal static bool TryReadInstalledProducerVersion(
            out string version)
        {
            if (!VersionReadCompleted)
            {
                CachedVersion = ReadInstalledPackageVersionOrNull?.Invoke(
                    ProducerPackageName);
                VersionReadCompleted = true;
            }
            version = CachedVersion;
            return !string.IsNullOrEmpty(version);
        }

        internal static void ClearVersionCacheForSession()
        {
            VersionReadCompleted = false;
            CachedVersion = null;
        }

        internal static bool IsVersionAdmitted(string version)
        {
            return !string.IsNullOrEmpty(version) && Admitted.Contains(version);
        }

        internal static bool TryIdentifyReplacement(Texture texture, out Texture2D source)
        {
            source = null;
            if (!(texture is Texture2D copy))
            {
                return false;
            }
            // Conjunct 1: a live copy with no asset path.
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(copy)))
            {
                return false;
            }
            // Conjunct 2: the pinned name shape.
            var name = copy.name ?? string.Empty;
            if (!name.EndsWith(ReplacementNameSuffix, StringComparison.Ordinal))
            {
                return false;
            }
            // Conjunct 3: the registry names an asset-backed source, one hop only.
            if (!(RegisteredSourceIdentity.Resolve(copy) is Texture2D resolved))
            {
                return false;
            }
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(resolved)))
            {
                return false;
            }
            // Conjunct 4: the producer is installed and its version is admitted.
            if (!TryReadInstalledProducerVersion(out var version) ||
                !IsVersionAdmitted(version))
            {
                return false;
            }
            source = resolved;
            return true;
        }

        private static string ReadVersionFromPackageManager(string packageName)
        {
            // Offline listing never touches the network, so blocking inside
            // an editor build pass is safe. Unity 2022.3 has no
            // Request.WaitForCompletion, so block on IsCompleted; the wait
            // is bounded so an unresponsive package manager refuses rather
            // than wedging the editor.
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
