using System;
using System.Collections.Generic;
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

        private const string ShaderOptimizerEnabledProperty =
            "_ShaderOptimizerEnabled";

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
                    ShaderOptimizerEnabledProperty);
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
                shader, evidence, PoiyomiMaterialSemantics.AlphaRequiredSchemaProperties);
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
                ShaderOptimizerEnabledProperty, out var optimizerEnabled) &&
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
