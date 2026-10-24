using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using LilToonPins = Alrauna.Amuse.Editor.Semantics.LilToon.LilToonSourceAttestation;
// UnityEditor.PackageManager.PackageInfo is ambiguous with UnityEngine.PackageInfo.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Alrauna.Amuse.Research.Tests.Editor.Calibration
{
    internal enum CensusVendorFamily
    {
        Poiyomi,
        LilToon,
    }

    /// <summary>
    /// What the probe found, as a value. Absence is a reported state, never an
    /// exception and never a skipped test: a vendor package that has silently
    /// gone missing must fail a census, and Assert.Ignore would report that as
    /// a pass.
    /// </summary>
    internal sealed class CensusVendorPresence
    {
        /// <summary>Null when the family is not installed.</summary>
        internal Shader Shader { get; }

        internal string ExpectedPackageName { get; }
        internal string ExpectedPackageVersion { get; }

        /// <summary>
        /// Null when the family does not attest by package version, when the
        /// family is not installed, or when the shader resolves outside any
        /// package.
        /// </summary>
        internal string InstalledPackageVersion { get; }

        /// <summary>
        /// True when the reported shader file matches an admitted row by
        /// name, GUID, and digest. Only a family that attests by shader file
        /// sets it (K6.4, docs/superpowers/specs/2026-10-23-poiyomi-per-
        /// shader-attestation-design.md).
        /// </summary>
        internal bool MatchesAdmittedSource { get; }

        internal bool IsInstalled => Shader != null;

        internal CensusVendorPresence(
            Shader shader,
            string expectedPackageName,
            string expectedPackageVersion,
            string installedPackageVersion,
            bool matchesAdmittedSource)
        {
            Shader = shader;
            ExpectedPackageName = expectedPackageName;
            ExpectedPackageVersion = expectedPackageVersion;
            InstalledPackageVersion = installedPackageVersion;
            MatchesAdmittedSource = matchesAdmittedSource;
        }
    }

    /// <summary>
    /// One resolved Poiyomi shader file, in row order. The GUID and the
    /// digest are null when the asset or its file could not be read (K6.3,
    /// docs/superpowers/specs/2026-10-23-poiyomi-per-shader-attestation-
    /// design.md).
    /// </summary>
    internal readonly struct CensusPoiyomiCandidate
    {
        internal string ShaderName { get; }
        internal Shader Shader { get; }
        internal string AssetGuid { get; }
        internal string NormalizedSourceHash { get; }

        internal CensusPoiyomiCandidate(
            string shaderName,
            Shader shader,
            string assetGuid,
            string normalizedSourceHash)
        {
            ShaderName = shaderName;
            Shader = shader;
            AssetGuid = assetGuid;
            NormalizedSourceHash = normalizedSourceHash;
        }
    }

    /// <summary>
    /// Locates the vendor shaders AMUSE attests. Poiyomi attests by shader
    /// file, so the Poiyomi label is a family label, not a pin: the probe
    /// walks the admitted rows and reports the shader file it finds (K6,
    /// docs/superpowers/specs/2026-10-23-poiyomi-per-shader-attestation-
    /// design.md).
    /// <para>
    /// The pins are referenced as constants, never retyped as literals: a
    /// version bump in the adapter must move this probe with it, as a
    /// compile-time fact rather than a stale copy nobody notices. The
    /// Poiyomi rows come from the product row set, so they cannot drift
    /// either.
    /// </para>
    /// <para>
    /// The probe stays read-only by review. It calls only Shader.Find,
    /// AssetDatabase.GetAssetPath,
    /// AssetDatabase.TryGetGUIDAndLocalFileIdentifier, File.Exists, and the
    /// product hash reader (K6.5).
    /// </para>
    /// </summary>
    internal static class CensusVendorProbe
    {
        // The Poiyomi label names the package kind for census reports. It is
        // not an attested pin: Poiyomi identity is the shader file (K6.4).
        private const string PoiyomiFamilyLabel = "com.poiyomi.toon";

        internal static IReadOnlyList<CensusVendorPresence> ProbeAll()
        {
            return new[]
            {
                Probe(CensusVendorFamily.Poiyomi),
                Probe(CensusVendorFamily.LilToon),
            };
        }

        internal static CensusVendorPresence Probe(CensusVendorFamily family)
        {
            switch (family)
            {
                case CensusVendorFamily.Poiyomi:
                    return ProbePoiyomiByShaderFile();
                case CensusVendorFamily.LilToon:
                    var shader = Shader.Find(LilToonPins.SupportedShaderName);
                    return new CensusVendorPresence(
                        shader,
                        LilToonPins.PackageName,
                        LilToonPins.PackageVersion,
                        InstalledVersionOf(shader),
                        matchesAdmittedSource: false);
                default:
                    // No default guess. A new family must be added here
                    // deliberately, not silently probed as nothing.
                    throw new System.ArgumentOutOfRangeException(
                        nameof(family));
            }
        }

        /// <summary>
        /// The admitted rows the probe walks: only rows with IsTwoPass
        /// false, in row order (K6.2, docs/superpowers/specs/2026-10-23-
        /// poiyomi-per-shader-attestation-design.md).
        /// </summary>
        internal static IReadOnlyList<PoiyomiAdmittedSource> PoiyomiProbeRows()
        {
            var rows = new List<PoiyomiAdmittedSource>();
            foreach (var row in PoiyomiSourceAttestation.AdmittedSources())
            {
                if (row.IsTwoPass)
                {
                    continue;
                }

                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// Picks the reported shader from the resolved candidates. The first
        /// candidate that matches an admitted row wins. When none matches,
        /// the last candidate wins, so a 10.x install reports the bundled
        /// shader and not the 10.0 shader. An empty list reports absence
        /// (K6.3, docs/superpowers/specs/2026-10-23-poiyomi-per-shader-
        /// attestation-design.md).
        /// </summary>
        internal static CensusPoiyomiCandidate? SelectPoiyomi(
            IReadOnlyList<CensusPoiyomiCandidate> resolvedInRowOrder)
        {
            if (resolvedInRowOrder.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < resolvedInRowOrder.Count; i++)
            {
                if (MatchesAnAdmittedRow(resolvedInRowOrder[i]))
                {
                    return resolvedInRowOrder[i];
                }
            }

            return resolvedInRowOrder[resolvedInRowOrder.Count - 1];
        }

        private static bool MatchesAnAdmittedRow(
            in CensusPoiyomiCandidate candidate)
        {
            return PoiyomiSourceAttestation.TryMatchAdmittedSource(
                candidate.ShaderName,
                candidate.AssetGuid,
                candidate.NormalizedSourceHash,
                out _);
        }

        // The thin shell around the pure selection. It resolves one
        // candidate per main row and never throws for a missing or
        // unreadable file. An unreadable file yields a candidate with a
        // null digest (K6.3).
        private static CensusVendorPresence ProbePoiyomiByShaderFile()
        {
            var candidates = new List<CensusPoiyomiCandidate>();
            foreach (var row in PoiyomiProbeRows())
            {
                var candidate = ResolvePoiyomiCandidate(row.ShaderName);
                if (candidate.HasValue)
                {
                    candidates.Add(candidate.Value);
                }
            }

            var selected = SelectPoiyomi(candidates);
            return new CensusVendorPresence(
                selected.HasValue ? selected.Value.Shader : null,
                PoiyomiFamilyLabel,
                null,
                null,
                selected.HasValue && MatchesAnAdmittedRow(selected.Value));
        }

        // The same guards as GatherSourceEvidence: an empty asset path,
        // File.Exists, and the two IO exceptions. The calls stay inside the
        // read-only set named in the class comment.
        private static CensusPoiyomiCandidate? ResolvePoiyomiCandidate(
            string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(shader);
            string assetGuid = null;
            string normalizedHash = null;
            if (!string.IsNullOrEmpty(assetPath) &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    shader,
                    out var guid,
                    out long _))
            {
                // Row GUIDs are lowercase hex, so the probe compares in
                // lowercase too.
                assetGuid = guid?.ToLowerInvariant();
                try
                {
                    if (File.Exists(assetPath) &&
                        PoiyomiSourceAttestation.TryReadNormalizedSourceHashCached(
                            assetPath,
                            out var cachedHash))
                    {
                        normalizedHash = cachedHash;
                    }
                }
                catch (IOException)
                {
                    normalizedHash = null;
                }
                catch (System.UnauthorizedAccessException)
                {
                    normalizedHash = null;
                }
            }

            return new CensusPoiyomiCandidate(
                shaderName,
                shader,
                assetGuid,
                normalizedHash);
        }

        private static string InstalledVersionOf(Shader shader)
        {
            if (shader == null)
            {
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            var package = PackageInfo.FindForAssetPath(assetPath);
            return package?.version;
        }
    }
}
