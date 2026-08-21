using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LilToonPins = Alrauna.Amuse.Editor.Semantics.LilToon.LilToonSourceAttestation;
// UnityEditor.PackageManager.PackageInfo is ambiguous with UnityEngine.PackageInfo.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PoiyomiPins = Alrauna.Amuse.Editor.Semantics.Poiyomi.PoiyomiMaterialSemantics;

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
        internal CensusVendorFamily Family { get; }

        /// <summary>Null when the family is not installed.</summary>
        internal Shader Shader { get; }

        internal string ExpectedPackageName { get; }
        internal string ExpectedPackageVersion { get; }

        /// <summary>
        /// Null when the family is not installed, or when the shader resolves
        /// outside any package. Distinguished from a version mismatch on
        /// purpose: they are different findings.
        /// </summary>
        internal string InstalledPackageVersion { get; }

        internal bool IsInstalled => Shader != null;

        internal CensusVendorPresence(
            CensusVendorFamily family,
            Shader shader,
            string expectedPackageName,
            string expectedPackageVersion,
            string installedPackageVersion)
        {
            Family = family;
            Shader = shader;
            ExpectedPackageName = expectedPackageName;
            ExpectedPackageVersion = expectedPackageVersion;
            InstalledPackageVersion = installedPackageVersion;
        }
    }

    /// <summary>
    /// Locates the vendor shaders AMUSE attests, by the exact names AMUSE pins.
    /// <para>
    /// The pins are referenced as constants, never retyped as literals: a
    /// version bump in the adapter must move this probe with it, as a compile-
    /// time fact rather than a stale copy nobody notices.
    /// </para>
    /// </summary>
    internal static class CensusVendorProbe
    {
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
            string shaderName;
            string packageName;
            string packageVersion;

            switch (family)
            {
                case CensusVendorFamily.Poiyomi:
                    shaderName = PoiyomiPins.PoiyomiToonShaderName;
                    packageName = PoiyomiPins.PoiyomiPackageName;
                    packageVersion = PoiyomiPins.PoiyomiPackageVersion;
                    break;
                case CensusVendorFamily.LilToon:
                    shaderName = LilToonPins.SupportedShaderName;
                    packageName = LilToonPins.PackageName;
                    packageVersion = LilToonPins.PackageVersion;
                    break;
                default:
                    // No default guess. A new family must be added here
                    // deliberately, not silently probed as nothing.
                    throw new System.ArgumentOutOfRangeException(
                        nameof(family));
            }

            var shader = Shader.Find(shaderName);
            return new CensusVendorPresence(
                family,
                shader,
                packageName,
                packageVersion,
                InstalledVersionOf(shader));
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
