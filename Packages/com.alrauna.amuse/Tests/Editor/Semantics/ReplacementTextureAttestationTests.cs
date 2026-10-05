using System;
using System.Collections.Generic;
using System.IO;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Tests.Editor.Shared;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public sealed class ReplacementTextureAttestationTests
    {
        private const string FixtureFolder = "Assets/AmuseTests_LacAttestation";

        [SetUp]
        public void ResetAttestationStateBeforeEachTest()
        {
            ReplacementTextureAttestation.ResetForTests();
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

        /// <summary>
        /// Imports a real texture asset so the source side of a
        /// registration is asset-backed, the shape the attestation
        /// demands of every admitted copy. Kept local: the deferred
        /// (non-forced) import and the identity asserts do not fit the
        /// WritePng callback shape.
        /// </summary>
        private static Texture2D ImportSourceAsset(string marker)
        {
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(FixtureFolder));
            }
            var path = $"{FixtureFolder}/{marker}.png";
            var pixels = new Color32[4 * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 0, 0, 255);
            }
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(imported, Is.Not.Null, "the fixture import must yield a texture");
            Assert.That(
                AssetDatabase.GetAssetPath(imported), Is.EqualTo(path));
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    imported, out var guid, out long fileId),
                Is.True,
                "the fixture source must be asset-backed");
            Assert.That(guid, Is.Not.Empty);
            Assert.That(fileId, Is.Not.EqualTo(0L));
            return imported;
        }

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
                ReplacementTextureAttestation.AdmittedVersions,
                Is.EqualTo(new[] { "0.9.0" }),
                "0.9.0 joined on 2026-10-03 through the dated " +
                "characterization; any other version stays refused");
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted("0.9.0"),
                Is.True);
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted("0.1.0"),
                Is.False);
        }

        [Test]
        public void InjectedVersionIsAdmittedAndOtherVersionIsRefused()
        {
            ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted("0.9.0"),
                Is.True);
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted("0.1.0"),
                Is.False);
        }

        [Test]
        public void NullOrEmptyVersionIsNeverAdmitted()
        {
            ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted(null), Is.False);
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted(""), Is.False);
        }

        [Test]
        public void ReadableProviderAnswerSatisfiesTheInstalledVersionRead()
        {
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "1.2.3";
            Assert.That(
                ReplacementTextureAttestation.TryReadInstalledProducerVersion(
                    out var version),
                Is.True);
            Assert.That(version, Is.EqualTo("1.2.3"));
        }

        [Test]
        public void UnreadableInstalledVersionRefusesTheRead()
        {
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => null;
            Assert.That(
                ReplacementTextureAttestation.TryReadInstalledProducerVersion(
                    out _),
                Is.False,
                "an unreadable version refuses, following the lilToon " +
                "baker precedent");
        }

        [Test]
        public void VersionReadCachesOneProviderCallPerSession()
        {
            var calls = 0;
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull = _ =>
            {
                calls++;
                return "0.9.0";
            };
            ReplacementTextureAttestation.TryReadInstalledProducerVersion(out _);
            ReplacementTextureAttestation.TryReadInstalledProducerVersion(out _);
            Assert.That(calls, Is.EqualTo(1),
                "the read caches once per capture session");
            ReplacementTextureAttestation.ClearVersionCacheForSession();
            ReplacementTextureAttestation.TryReadInstalledProducerVersion(out _);
            Assert.That(calls, Is.EqualTo(2),
                "a session reset reads again");
        }

        [Test]
        public void ResetForTestsRestoresTheCharacterizedSetAndTheProductionProvider()
        {
            ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.1.0");
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "9.9.9";
            ReplacementTextureAttestation.ResetForTests();
            Assert.That(
                ReplacementTextureAttestation.AdmittedVersions,
                Is.EqualTo(new[] { "0.9.0" }),
                "the reset restores the characterized production set");
            // With the production provider restored, the delegate no
            // longer answers "9.9.9" for every name. A plan-conformant
            // environment never installs LAC, so the restored provider
            // refuses the read outright.
            Assert.That(
                ReplacementTextureAttestation.TryReadInstalledProducerVersion(
                    out var version),
                Is.False);
            Assert.That(version, Is.Null);
        }

        [Test]
        public void RegisteredSuffixedPathlessCopyWithAdmittedVersionIsAdmitted()
        {
            // The shipped-empty admitted set refuses every copy, so this
            // fixture admits one version and points the provider at it,
            // matching the test's "with admitted version" premise.
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "0.9.0";
            ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
            var source = ImportSourceAsset("admitted");
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
            var source = ImportSourceAsset("nosuffix");
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
            var source = ImportSourceAsset("lonely");
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
            var source = ImportSourceAsset("noversion");
            var copy = MakeAdmittedCopy(source);
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copy);
                ReplacementTextureAttestation.SetAdmittedVersionsForTests();
                Assert.That(
                    ReplacementTextureAttestation.TryIdentifyReplacement(
                        copy, out _),
                    Is.False,
                    "the shipped-empty set refuses every copy");
                ReplacementTextureAttestation.ClearVersionCacheForSession();
                ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => null;
                ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
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
            var source = ImportSourceAsset("guarded");
            Assert.That(AssetDatabase.GetAssetPath(source), Is.Not.Empty);
            Assert.That(
                ReplacementTextureAttestation.TryIdentifyReplacement(
                    source, out _),
                Is.False);
        }
    }
}
