using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Alrauna.Amuse.Editor.Host;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    internal readonly struct PoiyomiSourceEvidence
    {
        internal string ShaderName { get; }
        internal bool IsLocked { get; }
        internal bool HasReadableSource { get; }
        internal string AssetGuid { get; }
        internal string NormalizedSourceHash { get; }
        internal bool HasPackage { get; }
        internal string PackageName { get; }
        internal string PackageVersion { get; }
        internal bool HasRequiredSchema { get; }

        internal PoiyomiSourceEvidence(
            string shaderName,
            bool isLocked,
            bool hasReadableSource,
            string assetGuid,
            string normalizedSourceHash,
            bool hasPackage,
            string packageName,
            string packageVersion,
            bool hasRequiredSchema)
        {
            ShaderName = shaderName;
            IsLocked = isLocked;
            HasReadableSource = hasReadableSource;
            AssetGuid = assetGuid;
            NormalizedSourceHash = normalizedSourceHash;
            HasPackage = hasPackage;
            PackageName = packageName;
            PackageVersion = packageVersion;
            HasRequiredSchema = hasRequiredSchema;
        }
    }

    /// <summary>
    /// One admitted shader file. The digest covers the normalized file text.
    /// (K1, docs/superpowers/specs/2026-10-23-poiyomi-per-shader-
    /// attestation-design.md)
    /// </summary>
    internal readonly struct PoiyomiAdmittedSource
    {
        internal string ShaderName { get; }
        internal bool IsTwoPass { get; }
        internal string AssetGuid { get; }
        internal string NormalizedSourceHash { get; }
        internal bool LockedOriginalAdmitted { get; }

        internal PoiyomiAdmittedSource(
            string shaderName,
            bool isTwoPass,
            string assetGuid,
            string normalizedSourceHash,
            bool lockedOriginalAdmitted)
        {
            ShaderName = shaderName;
            IsTwoPass = isTwoPass;
            AssetGuid = assetGuid;
            NormalizedSourceHash = normalizedSourceHash;
            LockedOriginalAdmitted = lockedOriginalAdmitted;
        }
    }

    internal static class PoiyomiSourceAttestation
    {
        internal const string PoiyomiToonShaderName = ".poiyomi/Poiyomi Toon";
        internal const string PoiyomiPackageName = "com.poiyomi.toon";
        internal const string PoiyomiPackageVersion = "9.3.64";
        internal const string CanonicalShaderGuid =
            "9444ce77bf4418748b1e8591b9d97f85";
        internal const string CanonicalNormalizedSourceHash =
            "31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755";

        internal const string PoiyomiTwoPassShaderName =
            ".poiyomi/Poiyomi Toon Two Pass";
        internal const string TwoPassCanonicalShaderGuid =
            "eda2412ac7ab2db45a47a521f6d7d8a6";
        internal const string TwoPassCanonicalNormalizedSourceHash =
            "b1d9ecd3072d21db97001dd23f88d089996b809e4039891a05f64d2ffcd4df67";

        // The admitted shader files (K1, K4, docs/superpowers/specs/
        // 2026-10-23-poiyomi-per-shader-attestation-design.md). One row per
        // measured shader file. All four rows carry the measurement date
        // 2026-10-23. Rows one and two measure the 9.3.64 release. Rows
        // three and four measure the shader files that the 10.x package
        // bundles. The bundled files reuse the 9.3.64 proof model (K4).
        //
        // Admission rule (K5): a maintainer admits a new byte stream only
        // through the K5 procedure. Download the release from the official
        // index. Compute the digest with the production recipe. Diff the
        // normalized file against the 9.3.64 release. Admit the row only
        // when the includes, the text from SubShader onward, and the read
        // property declarations pass the K5 checks. Record the measurement
        // in a dated investigation. Until then a changed byte stream
        // refuses materials fail-closed.
        private const string BundledMainNormalizedSourceHash =
            "c812d4d39ce3352b6198fdfcb5de87eb07678739a4bce7e4afd18cea28be19f4";
        private const string BundledTwoPassNormalizedSourceHash =
            "4fd1d8c1e35bd4e81a21c1dd6545ceb022542b67aa9996cb8341da7f3b64b65f";

        private static readonly PoiyomiAdmittedSource[] AdmittedSourceRows =
        {
            new PoiyomiAdmittedSource(
                PoiyomiToonShaderName,
                false,
                CanonicalShaderGuid,
                CanonicalNormalizedSourceHash,
                true),
            new PoiyomiAdmittedSource(
                PoiyomiTwoPassShaderName,
                true,
                TwoPassCanonicalShaderGuid,
                TwoPassCanonicalNormalizedSourceHash,
                true),
            new PoiyomiAdmittedSource(
                ".poiyomi/Old Versions/9.3/Poiyomi Toon",
                false,
                CanonicalShaderGuid,
                BundledMainNormalizedSourceHash,
                false),
            new PoiyomiAdmittedSource(
                ".poiyomi/Old Versions/9.3/Poiyomi Toon Two Pass",
                true,
                TwoPassCanonicalShaderGuid,
                BundledTwoPassNormalizedSourceHash,
                false),
        };

        // A read-only view. A caller cannot cast it back to the array and
        // cannot mutate it (K1.1).
        private static readonly ReadOnlyCollection<PoiyomiAdmittedSource>
            AdmittedSourceView = Array.AsReadOnly(AdmittedSourceRows);

        internal static IReadOnlyList<PoiyomiAdmittedSource> AdmittedSources()
        {
            return AdmittedSourceView;
        }

        // One row must equal all three values. Ordinal compare (K1.4).
        internal static bool TryMatchAdmittedSource(
            string shaderName,
            string assetGuid,
            string normalizedSourceHash,
            out PoiyomiAdmittedSource row)
        {
            for (var i = 0; i < AdmittedSourceRows.Length; i++)
            {
                var candidate = AdmittedSourceRows[i];
                if (string.Equals(
                        candidate.ShaderName,
                        shaderName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.AssetGuid,
                        assetGuid,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.NormalizedSourceHash,
                        normalizedSourceHash,
                        StringComparison.Ordinal))
                {
                    row = candidate;
                    return true;
                }
            }

            row = default;
            return false;
        }

        // One predicate routes every admitted name (K3).
        internal static bool TryClassifyShaderName(
            string shaderName,
            out bool isTwoPass)
        {
            for (var i = 0; i < AdmittedSourceRows.Length; i++)
            {
                var candidate = AdmittedSourceRows[i];
                if (string.Equals(
                    candidate.ShaderName,
                    shaderName,
                    StringComparison.Ordinal))
                {
                    isTwoPass = candidate.IsTwoPass;
                    return true;
                }
            }

            isTwoPass = false;
            return false;
        }

        private static readonly Dictionary<string, (DateTime LastWriteUtc, string Hash)>
            SourceHashMemo = new(StringComparer.Ordinal);

        internal static string ComputeNormalizedSourceHash(string rawSource)
        {
            return NormalizedSourceHash.Compute(rawSource);
        }

        internal static bool TryReadNormalizedSourceHashCached(
            string assetPath,
            out string normalizedHash)
        {
            var lastWrite = File.GetLastWriteTimeUtc(assetPath);
            if (SourceHashMemo.TryGetValue(assetPath, out var cached) &&
                cached.LastWriteUtc == lastWrite)
            {
                normalizedHash = cached.Hash;
                return true;
            }

            normalizedHash = ComputeNormalizedSourceHash(
                File.ReadAllText(assetPath, Encoding.UTF8));
            SourceHashMemo[assetPath] = (lastWrite, normalizedHash);
            return true;
        }

        internal static bool ClearSourceHashMemo()
        {
            var droppedAny = SourceHashMemo.Count > 0;
            SourceHashMemo.Clear();
            return droppedAny;
        }

        internal static bool TryVerifyPoiyomiIdentity(
            in PoiyomiSourceEvidence evidence,
            out PoiyomiSemanticDiagnostic diagnostic)
        {
            var isTwoPass = string.Equals(
                evidence.ShaderName,
                PoiyomiTwoPassShaderName,
                StringComparison.Ordinal);
            if (!isTwoPass &&
                !string.Equals(
                    evidence.ShaderName,
                    PoiyomiToonShaderName,
                    StringComparison.Ordinal))
            {
                diagnostic = PoiyomiMaterialSemantics.MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.UnsupportedShader,
                    $"shader name '{evidence.ShaderName}'");
                return false;
            }

            if (evidence.IsLocked)
            {
                diagnostic = PoiyomiMaterialSemantics.MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.UnsupportedShader,
                    PoiyomiProperties.ShaderOptimizerEnabled);
                return false;
            }

            if (!evidence.HasReadableSource)
            {
                diagnostic = PoiyomiMaterialSemantics.MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.MissingSourceEvidence,
                    "shader asset source");
                return false;
            }

            var expectedGuid = isTwoPass
                ? TwoPassCanonicalShaderGuid
                : CanonicalShaderGuid;
            if (!string.Equals(
                    evidence.AssetGuid,
                    expectedGuid,
                    StringComparison.Ordinal))
            {
                diagnostic = PoiyomiMaterialSemantics.MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.MissingSourceEvidence,
                    "shader asset GUID");
                return false;
            }

            if (evidence.HasPackage)
            {
                if (!string.Equals(
                        evidence.PackageName,
                        PoiyomiPackageName,
                        StringComparison.Ordinal))
                {
                    diagnostic = PoiyomiMaterialSemantics.MaterialDiagnostic(
                        PoiyomiSemanticDiagnosticCode.MissingSourceEvidence,
                        $"package name '{evidence.PackageName}'");
                    return false;
                }

                if (!string.Equals(
                        evidence.PackageVersion,
                        PoiyomiPackageVersion,
                        StringComparison.Ordinal))
                {
                    diagnostic = PoiyomiMaterialSemantics.MaterialDiagnostic(
                        PoiyomiSemanticDiagnosticCode.UnsupportedVersion,
                        $"package version '{evidence.PackageVersion}'");
                    return false;
                }
            }

            var expectedHash = isTwoPass
                ? TwoPassCanonicalNormalizedSourceHash
                : CanonicalNormalizedSourceHash;
            if (!string.Equals(
                    evidence.NormalizedSourceHash,
                    expectedHash,
                    StringComparison.Ordinal))
            {
                diagnostic = PoiyomiMaterialSemantics.MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.ModifiedShaderSource,
                    "normalized source hash");
                return false;
            }

            if (!evidence.HasRequiredSchema)
            {
                diagnostic = PoiyomiMaterialSemantics.MaterialDiagnostic(
                    PoiyomiSemanticDiagnosticCode.ModifiedShaderSource,
                    "required property schema");
                return false;
            }

            diagnostic = null;
            return true;
        }

        internal static PoiyomiSourceEvidence GatherAlphaSourceEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence)
        {
            return GatherSourceEvidence(
                shader, evidence, PoiyomiAlphaInterpreter.AlphaRequiredSchemaProperties);
        }

        internal static PoiyomiSourceEvidence GatherSourceEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence,
            IReadOnlyCollection<string> requiredSchemaProperties)
        {
            var shaderName = evidence.HasShaderName
                ? evidence.ShaderName
                : null;
            var isLocked = evidence.TryGetScalar(
                PoiyomiProperties.ShaderOptimizerEnabled, out var optimizerEnabled) &&
                optimizerEnabled != 0f;

            var assetPath = AssetDatabase.GetAssetPath(shader);

            var hasReadableSource = false;
            string assetGuid = null;
            string normalizedHash = null;
            if (!string.IsNullOrEmpty(assetPath) &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    shader,
                    out var guid,
                    out long _))
            {
                assetGuid = guid?.ToLowerInvariant();
                try
                {
                    if (File.Exists(assetPath) &&
                        TryReadNormalizedSourceHashCached(assetPath, out var cachedHash))
                    {
                        normalizedHash = cachedHash;
                        hasReadableSource = true;
                    }
                }
                catch (IOException)
                {
                    hasReadableSource = false;
                    normalizedHash = null;
                }
                catch (UnauthorizedAccessException)
                {
                    hasReadableSource = false;
                    normalizedHash = null;
                }
            }

            var package =
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);

            return new PoiyomiSourceEvidence(
                shaderName,
                isLocked,
                hasReadableSource,
                assetGuid,
                normalizedHash,
                package != null,
                package?.name,
                package?.version,
                HasRequiredSchema(evidence, requiredSchemaProperties));
        }

        private static bool HasRequiredSchema(
            CapturedMaterialEvidence evidence,
            IReadOnlyCollection<string> requiredSchemaProperties)
        {
            foreach (var property in requiredSchemaProperties)
            {
                if (!evidence.HasProperty(property))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
