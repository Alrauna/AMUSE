using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The proven key pair of the harness binding proof.
    /// </summary>
    internal sealed class RealLacPassKeyPair
    {
        internal string AnalysisBackendKey;

        internal string ResizeBackendKey;
    }

    /// <summary>
    /// The shared harness for tests that run the real Limitex Avatar
    /// Compressor pass. The harness resolves the vendor component type
    /// by assembly qualified reflection, proves the binding against the
    /// pinned environment, and freezes the two LAC backend preferences
    /// for one build. A null type resolution is a named skip whose text
    /// starts with the one fixed vendor-absent prefix. The harness
    /// implements the rules of design section 4.2 of the 2026-11-09
    /// vendor-integrated test rework. It never copies vendor source.
    /// </summary>
    internal static class RealLacPass
    {
        internal const string PackageName = "dev.limitex.avatar-compressor";

        // The assembly qualified name of the internal vendor component.
        // The assembly name comes from the installed asmdef of the
        // package, read on 2026-11-10.
        internal const string ComponentTypeName =
            "dev.limitex.avatar.compressor.TextureCompressor, "
            + PackageName;

        // The assembly qualified name of the shared preferences type
        // that holds the public PrefsPrefix constant.
        internal const string PreferencesTypeName =
            "dev.limitex.avatar.compressor.editor."
            + "AvatarCompressorPreferences, "
            + PackageName + ".editor";

        // The assembly qualified name of the texture preferences type
        // that holds the two backend keys and their properties.
        internal const string TexturePreferencesTypeName =
            "dev.limitex.avatar.compressor.editor.texture."
            + "TextureCompressorPreferences, "
            + PackageName + ".editor";

        // The assembly qualified name of the vendor NDMF pass. The
        // vendor plugin registration declares the VRChat platform only,
        // so the NDMF solver stubs the pass as incompatible on a test
        // platform. The harness drives the resolved pass instance
        // through a test plugin sequence instead, with the same phase
        // and the same required animator services extension the vendor
        // plugin declares. The observation of 2026-11-10 recorded the
        // stubbed "Incompatible" display name of the solver.
        internal const string PassTypeName =
            "dev.limitex.avatar.compressor.editor.texture."
            + "TextureCompressorPass, "
            + PackageName + ".editor";

        // The CPU ordinal of the two vendor backend enums. Both enums
        // list Auto first and CPU second, so the CPU value is 1 in
        // both. The binding proof asserts the member name behind this
        // ordinal in both vendor enums, so a vendor reorder or rename
        // fails with a named message before any build runs.
        internal const int CpuBackendValue = 1;

        private const string AnalysisBackendKeySuffix = "analysisBackend";

        private const string ResizeBackendKeySuffix = "resizeBackend";

        // The version to name when the locked block of the VPM
        // manifest holds no entry for the package. The committed lock
        // pinned this version as of 2026-11-10. The locked block stays
        // the first source, so this literal answers only where the
        // lock carries no entry.
        private const string FallbackLockedVersion = "0.9.0";

        // One entry shape of the locked block. The shape follows the
        // suite start check reader, so both readers accept the same
        // manifest forms.
        private static readonly Regex LockedVersionPattern = new Regex(
            "\"" + PackageName + "\"\\s*:\\s*\\{\\s*\"version\""
            + "\\s*:\\s*\"(?<version>[^\"]*)\"");

        private static string vendorAbsentSkipPrefixCache;

        /// <summary>
        /// The one fixed prefix of every vendor-absent skip text of the
        /// LAC tests. It names the package and its locked version, then
        /// leaves the reason to the caller. The version reads from the
        /// locked block of the VPM manifest at first use, so the text
        /// cannot go stale while the lock holds. The skip guard of the
        /// CI design pins this prefix by prefix match.
        /// </summary>
        internal static string VendorAbsentSkipPrefix
        {
            get
            {
                if (vendorAbsentSkipPrefixCache == null)
                {
                    var version = LockedPackageVersion()
                        ?? FallbackLockedVersion;
                    vendorAbsentSkipPrefixCache =
                        "Vendor package absent: " + PackageName
                        + " " + version + " (locked). ";
                }

                return vendorAbsentSkipPrefixCache;
            }
        }

        /// <summary>
        /// Resolves the vendor component type. A null answer means the
        /// package is absent or its assembly name moved. The caller
        /// skips with the vendor-absent prefix, never fails.
        /// </summary>
        internal static Type ResolveComponentTypeOrNull()
        {
            return Type.GetType(ComponentTypeName);
        }

        /// <summary>
        /// Skips the calling test by name when the LAC component type
        /// does not resolve. The skip text starts with the one fixed
        /// vendor-absent prefix of the harness. A version-seam test
        /// that reads the installed version calls this guard first,
        /// per the seam-dropped rule of design section 4.2. The guard
        /// sets no opt-in gate: an installed package always runs the
        /// test, and a failed version read on an installed package
        /// fails the test instead of skipping it.
        /// </summary>
        internal static void SkipWhenComponentAbsent(string reason)
        {
            if (ResolveComponentTypeOrNull() == null)
            {
                Assert.Ignore(VendorAbsentSkipPrefix + reason);
            }
        }

        /// <summary>
        /// Resolves the vendor NDMF pass type. The null rule of the
        /// component resolution applies, because both types live in
        /// the same package.
        /// </summary>
        internal static Type ResolvePassTypeOrNull()
        {
            return Type.GetType(PassTypeName);
        }

        /// <summary>
        /// Proves the vendor pass binding before a build runs: the pass
        /// type comes from the vendor editor assembly and carries the
        /// singleton Instance property and the protected
        /// Execute(BuildContext) entry the driver calls. A vendor
        /// release that moves either member fails here with a named
        /// message instead of inside a build.
        /// </summary>
        internal static void AssertPassBinding(Type passType)
        {
            Assert.That(
                passType.Assembly.GetName().Name,
                Is.EqualTo(PackageName + ".editor"),
                "the resolved pass type must come from the "
                + PackageName + ".editor assembly");
            Assert.That(
                passType.GetProperty(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static
                    | BindingFlags.FlattenHierarchy),
                Is.Not.Null,
                "the vendor pass type must carry the Instance"
                + " property (version pin)");
            Assert.That(
                passType.GetMethod(
                    "Execute",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(BuildContext) },
                    null),
                Is.Not.Null,
                "the vendor pass type must carry the protected"
                + " Execute(BuildContext) method (version pin)");
        }

        /// <summary>
        /// Drives the resolved vendor pass over one build context. The
        /// call site is a test plugin pass body in the Optimizing
        /// phase, so the vendor pass executes inside the real solver
        /// run with the animator services extension active. A missing
        /// binding fails with a named message, because a test that
        /// reaches this driver has proved the vendor present.
        /// </summary>
        internal static void ExecutePass(
            Type passType,
            BuildContext context)
        {
            var instanceProperty = passType.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static
                | BindingFlags.FlattenHierarchy);
            Assert.That(
                instanceProperty, Is.Not.Null,
                "the vendor pass type must carry the Instance"
                + " property (version pin)");
            var instance = instanceProperty.GetValue(null);

            var executeMethod = passType.GetMethod(
                "Execute",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(BuildContext) },
                null);
            Assert.That(
                executeMethod, Is.Not.Null,
                "the vendor pass type must carry the protected"
                + " Execute(BuildContext) method (version pin)");
            executeMethod.Invoke(instance, new object[] { context });
        }

        /// <summary>
        /// Proves the vendor binding the harness needs: the component
        /// type comes from the vendor runtime assembly, the installed
        /// package version equals the version the locked block of the
        /// VPM manifest pins, every field the harness touches exists
        /// by its pinned name, and the two backend preference keys of
        /// the vendor editor assembly equal the prefix plus the pinned
        /// suffixes. A vendor release that renames any pinned member,
        /// or a version that drifts from the lock, fails here with a
        /// named message. The proof returns the two proven keys for
        /// the freeze.
        /// </summary>
        internal static RealLacPassKeyPair AssertBinding(
            Type componentType)
        {
            Assert.That(
                componentType.Assembly.GetName().Name,
                Is.EqualTo(PackageName),
                "the resolved component type must come from the "
                + PackageName + " runtime assembly");

            AssertInstalledVersionMatchesTheLock();

            AssertComponentField(componentType, "MinSourceSize");
            AssertComponentField(componentType, "TargetPlatform");
            AssertComponentField(componentType, "ProcessMainTextures");
            AssertComponentField(componentType, "DetectUnusedTextures");
            AssertComponentField(componentType, "BakeLilToonTextures");

            var preferencesType = Type.GetType(PreferencesTypeName);
            Assert.That(
                preferencesType, Is.Not.Null,
                "the shared preferences type must resolve by its "
                + "assembly qualified name");
            var prefixField = preferencesType.GetField(
                "PrefsPrefix",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(
                prefixField, Is.Not.Null,
                "the shared preferences type must carry the public "
                + "PrefsPrefix constant");
            var prefix = prefixField.GetValue(null) as string;
            Assert.That(
                prefix, Is.EqualTo(PackageName + "."),
                "the preferences prefix must name the package");

            var texturePreferencesType =
                Type.GetType(TexturePreferencesTypeName);
            Assert.That(
                texturePreferencesType, Is.Not.Null,
                "the texture preferences type must resolve by its "
                + "assembly qualified name");
            AssertPreferencesKey(
                texturePreferencesType,
                "AnalysisBackendKey",
                prefix + AnalysisBackendKeySuffix);
            AssertPreferencesKey(
                texturePreferencesType,
                "ResizeBackendKey",
                prefix + ResizeBackendKeySuffix);
            AssertPreferencesProperty(texturePreferencesType, "AnalysisBackend");
            AssertPreferencesProperty(texturePreferencesType, "ResizeBackend");
            AssertEnumMemberName(
                texturePreferencesType.GetProperty("AnalysisBackend")
                    .PropertyType,
                CpuBackendValue,
                "CPU",
                "AnalysisBackendPreference");
            AssertEnumMemberName(
                texturePreferencesType.GetProperty("ResizeBackend")
                    .PropertyType,
                CpuBackendValue,
                "CPU",
                "ResizeBackendPreference");

            return new RealLacPassKeyPair
            {
                AnalysisBackendKey = prefix + AnalysisBackendKeySuffix,
                ResizeBackendKey = prefix + ResizeBackendKeySuffix,
            };
        }

        /// <summary>
        /// Asserts that the member behind one ordinal of a vendor enum
        /// carries the pinned name. The freeze and the fixture write
        /// vendor enums by ordinal, so a vendor reorder or rename of
        /// the members fails here with a named message before any
        /// build runs.
        /// </summary>
        private static void AssertEnumMemberName(
            Type enumType,
            int ordinal,
            string expectedName,
            string enumLabel)
        {
            Assert.That(
                enumType, Is.Not.Null,
                "fixture precondition: the " + enumLabel
                + " enum type must resolve");
            Assert.That(
                Enum.GetName(enumType, ordinal),
                Is.EqualTo(expectedName),
                "the member at ordinal " + ordinal + " of the vendor "
                + enumLabel + " enum must be named " + expectedName
                + " (version pin)");
        }

        /// <summary>
        /// Reads the installed version from the package manifest of the
        /// installed package and asserts that it equals the version the
        /// locked block of the VPM manifest pins. The failure names the
        /// package, the locked version, and the observed version, so a
        /// drift names itself per design principle 3.
        /// </summary>
        private static void AssertInstalledVersionMatchesTheLock()
        {
            var lockedVersion = LockedPackageVersion();
            Assert.That(
                lockedVersion, Is.Not.Null,
                "the locked block of Packages/vpm-manifest.json must"
                + " pin " + PackageName + ", because the harness pins"
                + " the installed version against the lock");
            var installedVersion = ReadInstalledPackageVersion();
            Assert.That(
                installedVersion, Is.Not.Null,
                "the installed package manifest of " + PackageName
                + " must name a version");
            Assert.That(
                installedVersion,
                Is.EqualTo(lockedVersion),
                "vendor version drift for " + PackageName
                + ": the lock pins " + lockedVersion + " and the"
                + " installed package reads " + installedVersion
                + ". Re-derive the harness binding against the"
                + " observed version before trusting a run");
        }

        /// <summary>
        /// Reads the version field of the installed package manifest
        /// through System.IO and JsonUtility. The read is independent
        /// of the production package manager listing, so the assertion
        /// cannot pass by reading its own comparison source twice.
        /// </summary>
        private static string ReadInstalledPackageVersion()
        {
            var manifestPath = System.IO.Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Packages", PackageName, "package.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            var manifest = File.ReadAllText(manifestPath);
            var parsed = JsonUtility.FromJson<PackageManifestJson>(
                manifest);
            return parsed == null ? null : parsed.version;
        }

        [System.Serializable]
        private sealed class PackageManifestJson
        {
            public string name;

            public string version;
        }

        /// <summary>
        /// Freezes both LAC backend preferences to the CPU choice for
        /// the duration of the scope. The freeze makes the compressed
        /// format and the resize route independent of the GPU analysis
        /// of the machine. Dispose in a using block, which restores the
        /// earlier values in the equivalent of a finally block.
        /// </summary>
        internal static BackendPreferencePin FreezeCpuBackends(
            string analysisBackendKey,
            string resizeBackendKey)
        {
            return new BackendPreferencePin(
                analysisBackendKey,
                resizeBackendKey);
        }

        private static void AssertComponentField(
            Type componentType,
            string fieldName)
        {
            Assert.That(
                componentType.GetField(fieldName),
                Is.Not.Null,
                "the vendor component must carry the field "
                + fieldName + " (version pin)");
        }

        private static void AssertPreferencesKey(
            Type preferencesType,
            string fieldName,
            string expectedKey)
        {
            var field = preferencesType.GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(
                field, Is.Not.Null,
                "the texture preferences type must carry the key "
                + "constant " + fieldName + " (version pin)");
            Assert.That(
                field.GetRawConstantValue() as string,
                Is.EqualTo(expectedKey),
                "the " + fieldName + " constant must equal the "
                + "preferences prefix plus the pinned suffix");
        }

        private static void AssertPreferencesProperty(
            Type preferencesType,
            string propertyName)
        {
            Assert.That(
                preferencesType.GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null,
                "the texture preferences type must carry the "
                + propertyName + " property (version pin)");
        }

        /// <summary>
        /// Reads the locked version of the package from the locked
        /// block of the VPM manifest. The reader never reads the
        /// top-level dependencies block. A missing manifest, a missing
        /// block, or a missing entry answers null, and the skip prefix
        /// falls back to the pinned literal.
        /// </summary>
        private static string LockedPackageVersion()
        {
            var manifestPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Packages", "vpm-manifest.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            var manifest = File.ReadAllText(manifestPath);
            var lockedBlock = LockedBlockOf(manifest);
            if (lockedBlock.Length == 0)
            {
                return null;
            }

            var match = LockedVersionPattern.Match(lockedBlock);
            return match.Success ? match.Groups["version"].Value : null;
        }

        /// <summary>
        /// Extracts the balanced locked object of the manifest. The
        /// extraction follows the suite start check reader, so both
        /// readers accept the same manifest forms.
        /// </summary>
        private static string LockedBlockOf(string manifest)
        {
            var lockedAt = manifest.IndexOf(
                "\"locked\"", StringComparison.Ordinal);
            if (lockedAt < 0)
            {
                return string.Empty;
            }

            var openAt = manifest.IndexOf('{', lockedAt);
            if (openAt < 0)
            {
                return string.Empty;
            }

            var depth = 0;
            for (var i = openAt; i < manifest.Length; i++)
            {
                if (manifest[i] == '{')
                {
                    depth++;
                }
                else if (manifest[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return manifest.Substring(
                            openAt, i - openAt + 1);
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// The restore scope of the two backend preferences. The
        /// constructor saves both values, sets both to the CPU choice,
        /// and the dispose restores the saved values. A key that held
        /// no value before the freeze is deleted on restore, so the
        /// pin leaves no residue.
        /// </summary>
        internal sealed class BackendPreferencePin : IDisposable
        {
            private readonly string analysisBackendKey;

            private readonly string resizeBackendKey;

            private readonly int savedAnalysisValue;

            private readonly int savedResizeValue;

            private readonly bool hadAnalysisValue;

            private readonly bool hadResizeValue;

            internal BackendPreferencePin(
                string analysisBackendKey,
                string resizeBackendKey)
            {
                this.analysisBackendKey = analysisBackendKey;
                this.resizeBackendKey = resizeBackendKey;
                hadAnalysisValue =
                    EditorPrefs.HasKey(analysisBackendKey);
                savedAnalysisValue =
                    EditorPrefs.GetInt(analysisBackendKey);
                hadResizeValue =
                    EditorPrefs.HasKey(resizeBackendKey);
                savedResizeValue =
                    EditorPrefs.GetInt(resizeBackendKey);
                EditorPrefs.SetInt(
                    analysisBackendKey, CpuBackendValue);
                EditorPrefs.SetInt(
                    resizeBackendKey, CpuBackendValue);
            }

            public void Dispose()
            {
                if (hadAnalysisValue)
                {
                    EditorPrefs.SetInt(
                        analysisBackendKey, savedAnalysisValue);
                }
                else
                {
                    EditorPrefs.DeleteKey(analysisBackendKey);
                }

                if (hadResizeValue)
                {
                    EditorPrefs.SetInt(
                        resizeBackendKey, savedResizeValue);
                }
                else
                {
                    EditorPrefs.DeleteKey(resizeBackendKey);
                }
            }
        }
    }
}
