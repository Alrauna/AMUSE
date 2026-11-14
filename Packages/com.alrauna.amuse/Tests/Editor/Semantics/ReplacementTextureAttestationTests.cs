using System;
using System.Collections.Generic;
using System.IO;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Tests.Editor.Build;
using Alrauna.Amuse.Tests.Editor.Shared;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    // Falsifier header, dated 2026-11-10. The refusal tests and the
    // seam-mechanics tests of this class stay synthetic. No producer
    // delivers an unregistered copy, an unsuffixed copy, a chained
    // source, an unreadable install, or a replaced admitted set. Those
    // tests inject the version provider or the admitted set, and the
    // per-test reset restores the production state after each of them.
    // The admitted happy path drops the version seam. It reads the
    // installed producer version through production admission and
    // skips by name when the package is absent.
    public sealed class ReplacementTextureAttestationTests
    {
        private const string FixtureFolder = "Assets/AmuseTests_LacAttestation";

        [SetUp]
        public void ResetAttestationStateBeforeEachTest()
        {
            ReplacementTextureAttestation.Versions.ResetForTests();
            RegistryGuard = new ObjectRegistryGuard();
        }

        [TearDown]
        public void RestoreRegistryAndCleanFixtureFolder()
        {
            RegistryGuard.Dispose();
            if (AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.DeleteAsset(FixtureFolder);
            }
        }

        private ObjectRegistryGuard RegistryGuard { get; set; }

        private static Texture2D MakeAdmittedCopy(Texture2D source)
        {
            var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copy.name = source.name +
                ReplacementTextureAttestation.ReplacementNameSuffix;
            return copy;
        }

        [Test]
        public void ShippedAdmittedSetContainsOnlyTheCharacterizedVersion()
        {
            Assert.That(
                ReplacementTextureAttestation.Versions.AdmittedVersions,
                Is.EqualTo(new[] { "0.9.0" }),
                "0.9.0 joined on 2026-10-03 through the dated " +
                "characterization; any other version stays refused");
            Assert.That(
                ReplacementTextureAttestation.Versions.IsVersionAdmitted("0.9.0"),
                Is.True);
            Assert.That(
                ReplacementTextureAttestation.Versions.IsVersionAdmitted("0.1.0"),
                Is.False);
        }

        [Test]
        public void InjectedVersionIsAdmittedAndOtherVersionIsRefused()
        {
            ReplacementTextureAttestation.Versions.SetAdmittedVersionsForTests("0.9.0");
            Assert.That(
                ReplacementTextureAttestation.Versions.IsVersionAdmitted("0.9.0"),
                Is.True);
            Assert.That(
                ReplacementTextureAttestation.Versions.IsVersionAdmitted("0.1.0"),
                Is.False);
        }

        [Test]
        public void NullOrEmptyVersionIsNeverAdmitted()
        {
            ReplacementTextureAttestation.Versions.SetAdmittedVersionsForTests("0.9.0");
            Assert.That(
                ReplacementTextureAttestation.Versions.IsVersionAdmitted(null), Is.False);
            Assert.That(
                ReplacementTextureAttestation.Versions.IsVersionAdmitted(""), Is.False);
        }

        [Test]
        public void ReadableProviderAnswerSatisfiesTheInstalledVersionRead()
        {
            ReplacementTextureAttestation.Versions.ReadInstalledPackageVersionOrNull =
                _ => "1.2.3";
            Assert.That(
                ReplacementTextureAttestation.Versions.TryReadInstalledProducerVersion(
                    out var version),
                Is.True);
            Assert.That(version, Is.EqualTo("1.2.3"));
        }

        [Test]
        public void UnreadableInstalledVersionRefusesTheRead()
        {
            ReplacementTextureAttestation.Versions.ReadInstalledPackageVersionOrNull =
                _ => null;
            Assert.That(
                ReplacementTextureAttestation.Versions.TryReadInstalledProducerVersion(
                    out _),
                Is.False,
                "an unreadable version refuses, following the lilToon " +
                "baker precedent");
        }

        [Test]
        public void VersionReadCachesOneProviderCallPerSession()
        {
            var calls = 0;
            ReplacementTextureAttestation.Versions.ReadInstalledPackageVersionOrNull = _ =>
            {
                calls++;
                return "0.9.0";
            };
            ReplacementTextureAttestation.Versions.TryReadInstalledProducerVersion(out _);
            ReplacementTextureAttestation.Versions.TryReadInstalledProducerVersion(out _);
            Assert.That(calls, Is.EqualTo(1),
                "the read caches once per capture session");
            ReplacementTextureAttestation.Versions.ClearVersionCacheForSession();
            ReplacementTextureAttestation.Versions.TryReadInstalledProducerVersion(out _);
            Assert.That(calls, Is.EqualTo(2),
                "a session reset reads again");
        }

        [Test]
        public void ResetForTestsRestoresTheCharacterizedSetAndTheProductionProvider()
        {
            ReplacementTextureAttestation.Versions.SetAdmittedVersionsForTests("0.1.0");
            ReplacementTextureAttestation.Versions.ReadInstalledPackageVersionOrNull =
                _ => "9.9.9";
            ReplacementTextureAttestation.Versions.ResetForTests();
            Assert.That(
                ReplacementTextureAttestation.Versions.AdmittedVersions,
                Is.EqualTo(new[] { "0.9.0" }),
                "the reset restores the characterized production set");
            // The reset restores the production provider. The delegate
            // no longer answers "9.9.9" for every name. The independent
            // read parses the installed package manifest from disk. It
            // never trusts the seam under test. An absent manifest means
            // an absent package. Then the production read must refuse.
            // A present manifest pins the production answer to the
            // installed version.
            var manifestVersion = InstalledLacManifestVersion();
            if (manifestVersion == null)
            {
                Assert.That(
                    ReplacementTextureAttestation.Versions
                        .TryReadInstalledProducerVersion(out _),
                    Is.False,
                    "an absent package manifest refuses the " +
                    "production read");
                return;
            }
            Assert.That(
                ReplacementTextureAttestation.Versions
                    .TryReadInstalledProducerVersion(out var version),
                Is.True,
                "an installed package answers the production read");
            Assert.That(version, Is.EqualTo(manifestVersion),
                "the restored provider answers the installed " +
                "version, not the injected 9.9.9");
        }

        /// <summary>
        /// Reads the installed producer manifest from disk through
        /// System.IO and JsonUtility. The read is independent of the
        /// version seam the test exercises, so the assertion compares
        /// two routes and never a value with itself. Null means the
        /// manifest file is absent or carries no version. The test
        /// reads that as an absent package.
        /// </summary>
        private static string InstalledLacManifestVersion()
        {
            var manifestPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "Packages",
                ReplacementTextureAttestation.ProducerPackageName,
                "package.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }
            var manifest = JsonUtility.FromJson<InstalledPackageManifest>(
                File.ReadAllText(manifestPath));
            return string.IsNullOrEmpty(manifest.version)
                ? null
                : manifest.version;
        }

        [Serializable]
        private sealed class InstalledPackageManifest
        {
            public string version;
        }

        [Test]
        public void RegisteredSuffixedPathlessCopyWithAdmittedVersionIsAdmitted()
        {
            // The version conjunct reads the installed producer version
            // through production admission. No provider injection
            // remains, so the test answers for the real install or
            // skips by name when the package is absent.
            RealLacPass.SkipWhenComponentAbsent(
                "the admission route reads the installed producer"
                + " version.");
            var source = ReplacementTextureIdentityTests.ImportSourceAsset("admitted", FixtureFolder);
            var copy = MakeAdmittedCopy(source);
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copy);
                Assert.That(
                    ReplacementTextureAttestation.TryIdentifyReplacement(
                        copy, out var resolvedSource),
                    Is.True);
                Assert.That(resolvedSource, Is.EqualTo(source),
                    "the admission names the resolved asset-backed source");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void UnsuffixedRegisteredCopyIsRefused()
        {
            // --- Falsifier 2: a registered copy without the suffix refuses. ---
            var source = ReplacementTextureIdentityTests.ImportSourceAsset("nosuffix", FixtureFolder);
            var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copy.name = source.name;
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copy);
                Assert.That(
                    ReplacementTextureAttestation.TryIdentifyReplacement(
                        copy, out _),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void SuffixedUnregisteredCopyIsRefused()
        {
            // --- Falsifier 1: the suffix alone never admits. ---
            var source = ReplacementTextureIdentityTests.ImportSourceAsset("lonely", FixtureFolder);
            var copy = MakeAdmittedCopy(source);
            try
            {
                Assert.That(
                    ReplacementTextureAttestation.TryIdentifyReplacement(
                        copy, out _),
                    Is.False,
                    "a name marker without a registry registration refuses");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void CopyOfInMemorySourceIsRefusedOneHopOnly()
        {
            var memorySource = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copy.name = "chained_compressed";
            try
            {
                ObjectRegistry.RegisterReplacedObject(memorySource, copy);
                Assert.That(
                    ReplacementTextureAttestation.TryIdentifyReplacement(
                        copy, out _),
                    Is.False,
                    "the source must be asset-backed; chains admit one hop only");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
                UnityEngine.Object.DestroyImmediate(memorySource);
            }
        }

        [Test]
        public void UninstalledProducerOrUnlistedVersionRefusesTheCopy()
        {
            // --- Falsifier 3: registration plus suffix without the version
            // conjunct refuses. ---
            var source = ReplacementTextureIdentityTests.ImportSourceAsset("noversion", FixtureFolder);
            var copy = MakeAdmittedCopy(source);
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copy);
                ReplacementTextureAttestation.Versions.SetAdmittedVersionsForTests();
                Assert.That(
                    ReplacementTextureAttestation.TryIdentifyReplacement(
                        copy, out _),
                    Is.False,
                    "the shipped-empty set refuses every copy");
                ReplacementTextureAttestation.Versions.ClearVersionCacheForSession();
                ReplacementTextureAttestation.Versions.ReadInstalledPackageVersionOrNull =
                    _ => null;
                ReplacementTextureAttestation.Versions.SetAdmittedVersionsForTests("0.9.0");
                Assert.That(
                    ReplacementTextureAttestation.TryIdentifyReplacement(
                        copy, out _),
                    Is.False,
                    "an unreadable installed version refuses");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void AssetBackedTextureNeverEntersTheReplacementClass()
        {
            // No-op guard: the fixture itself must be asset-backed, or this test
            // proves nothing.
            var source = ReplacementTextureIdentityTests.ImportSourceAsset("guarded", FixtureFolder);
            Assert.That(AssetDatabase.GetAssetPath(source), Is.Not.Empty);
            Assert.That(
                ReplacementTextureAttestation.TryIdentifyReplacement(
                    source, out _),
                Is.False);
        }
    }
}
