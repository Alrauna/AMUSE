using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Attests the Limitex Avatar Compressor replacement-copy shape.
    /// Every conjunct must hold; any miss refuses.
    /// </summary>
    // Upstream path (spec 2026-10-03, section 12; premise corrected
    // 2026-10-04). Persistence of each copy as a container sub-asset would
    // retire the unity-replacement identity form and the duplicate guard.
    // It would not retire producer admission: the container basis checks a
    // producer name marker, and a "_compressed" copy carries no admitted
    // marker. A name marker plus a version admission remains either way
    // (spec 2026-10-04). The same release registering bake pairs would let
    // AMUSE admit the in-build "_baked" output and retire its refusal.
    internal static class ReplacementTextureAttestation
    {
        internal const string ProducerPackageName =
            "dev.limitex.avatar-compressor";
        internal const string ReplacementNameSuffix = "_compressed";
        internal const string BakedOutputSuffix = "_baked";

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

        private static readonly AdmittedProducerVersions Versions =
            new AdmittedProducerVersions(
                ProducerPackageName,
                ProductionAdmittedVersions);

        /// <summary>
        /// Forwarded seam. Addressed by ReplacementTextureAttestationTests,
        /// GeneratedTextureAttestationTests, UnityTextureEvidenceTests, and
        /// UnityAlphaFieldEvidenceTests.
        /// </summary>
        internal static Func<string, string>
            ReadInstalledPackageVersionOrNull
        {
            get => Versions.ReadInstalledPackageVersionOrNull;
            set => Versions.ReadInstalledPackageVersionOrNull = value;
        }

        /// <summary>
        /// Forwarded seam. Addressed by ReplacementTextureAttestationTests.
        /// </summary>
        internal static IReadOnlyList<string> AdmittedVersions =>
            Versions.AdmittedVersions;

        /// <summary>
        /// Forwarded seam. Addressed by ReplacementTextureAttestationTests,
        /// GeneratedTextureAttestationTests, UnityTextureEvidenceTests, and
        /// UnityAlphaFieldEvidenceTests.
        /// </summary>
        internal static void SetAdmittedVersionsForTests(
            params string[] versions)
        {
            Versions.SetAdmittedVersionsForTests(versions);
        }

        /// <summary>
        /// Forwarded seam. Addressed by ReplacementTextureAttestationTests,
        /// GeneratedTextureAttestationTests, UnityTextureEvidenceTests, and
        /// UnityAlphaFieldEvidenceTests.
        /// </summary>
        internal static void ResetForTests()
        {
            Versions.ResetForTests();
        }

        /// <summary>
        /// Forwarded seam. Addressed by ReplacementTextureAttestationTests
        /// and GeneratedTextureAttestationTests.
        /// </summary>
        internal static bool TryReadInstalledProducerVersion(
            out string version)
        {
            return Versions.TryReadInstalledProducerVersion(out version);
        }

        /// <summary>
        /// Forwarded seam. Addressed by ReplacementTextureAttestationTests;
        /// the production session resets in AlphaSeparationApply and
        /// AmusePlatformFinishPlugin call it.
        /// </summary>
        internal static void ClearVersionCacheForSession()
        {
            Versions.ClearVersionCacheForSession();
        }

        /// <summary>
        /// Forwarded seam. Addressed by ReplacementTextureAttestationTests
        /// and GeneratedTextureAttestationTests.
        /// </summary>
        internal static bool IsVersionAdmitted(string version)
        {
            return Versions.IsVersionAdmitted(version);
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
    }
}
