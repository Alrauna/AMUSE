using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Attests the in-memory atlas shape of the Optimize Texture pass
    /// of Avatar Optimizer. Every conjunct must hold; any miss refuses.
    /// </summary>
    // Upstream path (spec 2026-10-04, section 8). A future Avatar
    // Optimizer release that improves the attestability of its
    // generated textures, for example by registering each atlas in the
    // NDMF object registry or by persisting atlases into a build
    // container, would let AMUSE delete this admission almost
    // entirely: the shape predicate, the corroboration conjunct, and
    // the version seam would all retire together with the identity
    // mint and the two arms that consult them. The persisted-container
    // branch in GeneratedTextureAttestation already reads that shape.
    // Until then, this comment is the whole upstream ask.
    internal static class AaoAtlasTextureAttestation
    {
        internal const string ProducerPackageName =
            "com.anatawa12.avatar-optimizer";
        internal const string AtlasNameSuffix = " (AAO UV Packed)";
        internal const string MonotoneNamePrefix = "AAO Monotone ";

        // The admitted set is pinned, characterized data, never grown
        // at runtime. 1.9.17 joined on 2026-10-04: the installed dev
        // and Census Lab copies' source shows the shape (atlases named
        // source plus " (AAO UV Packed)" or "AAO Monotone ...", plain
        // in-memory Texture2D objects, never saved, never registered,
        // format and flags mirrored from the source), and Lab builds
        // exercised the refusal end to end before admission
        // (investigations 2026-09-27 and 2026-10-04).
        private static readonly string[] ProductionAdmittedVersions =
        {
            "1.9.17",
        };

        private static readonly AdmittedProducerVersions Versions =
            new AdmittedProducerVersions(
                ProducerPackageName,
                ProductionAdmittedVersions);

        /// <summary>
        /// Forwarded seam. Addressed by AaoAtlasTextureAttestationTests,
        /// GeneratedTextureAttestationTests, UnityTextureEvidenceTests,
        /// UnityAlphaFieldEvidenceTests, UnityGeneratedTextureEvidenceTests,
        /// and UnityMaterialEvidenceCaptureTests.
        /// </summary>
        internal static Func<string, string>
            ReadInstalledPackageVersionOrNull
        {
            get => Versions.ReadInstalledPackageVersionOrNull;
            set => Versions.ReadInstalledPackageVersionOrNull = value;
        }

        /// <summary>
        /// Forwarded seam. Addressed by AaoAtlasTextureAttestationTests.
        /// </summary>
        internal static IReadOnlyList<string> AdmittedVersions =>
            Versions.AdmittedVersions;

        /// <summary>
        /// Forwarded seam. Kept for parity with the sibling attestation's
        /// seam; currently addressed by no test.
        /// </summary>
        internal static void SetAdmittedVersionsForTests(
            params string[] versions)
        {
            Versions.SetAdmittedVersionsForTests(versions);
        }

        /// <summary>
        /// Forwarded seam. Addressed by AaoAtlasTextureAttestationTests,
        /// GeneratedTextureAttestationTests, UnityTextureEvidenceTests,
        /// UnityAlphaFieldEvidenceTests, UnityGeneratedTextureEvidenceTests,
        /// and UnityMaterialEvidenceCaptureTests.
        /// </summary>
        internal static void ResetForTests()
        {
            Versions.ResetForTests();
        }

        /// <summary>
        /// Forwarded seam. Addressed through the admission and refusal
        /// tests in AaoAtlasTextureAttestationTests.
        /// </summary>
        internal static bool TryReadInstalledProducerVersion(
            out string version)
        {
            return Versions.TryReadInstalledProducerVersion(out version);
        }

        /// <summary>
        /// Forwarded seam. The production session resets in
        /// AlphaSeparationApply and AmusePlatformFinishPlugin call it.
        /// </summary>
        internal static void ClearVersionCacheForSession()
        {
            Versions.ClearVersionCacheForSession();
        }

        /// <summary>
        /// Forwarded seam. Addressed by AaoAtlasTextureAttestationTests.
        /// </summary>
        internal static bool IsVersionAdmitted(string version)
        {
            return Versions.IsVersionAdmitted(version);
        }

        /// <summary>
        /// The shape predicate, conjuncts C1 to C3 of spec 2026-10-04
        /// section 4.1. It is deliberately texture-only: the route
        /// selector and the fact readers consult it, while the identity
        /// gate demands the corroborated form below.
        /// </summary>
        internal static bool TryIdentifyAtlasShape(Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }
            // Conjunct 1: a live copy with no asset path.
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
            {
                return false;
            }
            // Conjunct 2: the pinned name shape.
            var name = texture.name ?? string.Empty;
            var hasMarker =
                name.EndsWith(AtlasNameSuffix, StringComparison.Ordinal) ||
                name.StartsWith(MonotoneNamePrefix, StringComparison.Ordinal);
            if (!hasMarker)
            {
                return false;
            }
            // Conjunct 3: the producer is installed and its version is
            // admitted.
            return TryReadInstalledProducerVersion(out var version) &&
                   IsVersionAdmitted(version);
        }

        /// <summary>
        /// The full admission, conjuncts C1 to C4 of spec 2026-10-04
        /// section 4.1. The slot's live material must corroborate the
        /// producer: the registry resolves it one hop to an asset-backed
        /// material, the pair the DuplicateAssets pass of Avatar
        /// Optimizer registers for every renderer material it clones.
        /// </summary>
        internal static bool TryIdentifyAtlas(
            Texture2D texture,
            Material originMaterial)
        {
            if (!TryIdentifyAtlasShape(texture))
            {
                return false;
            }
            // Conjunct 4: the origin material corroborates the producer.
            if (originMaterial == null)
            {
                return false;
            }
            if (!(RegisteredSourceIdentity.Resolve(originMaterial)
                    is Material resolved))
            {
                return false;
            }
            return !string.IsNullOrEmpty(
                AssetDatabase.GetAssetPath(resolved));
        }
    }
}
