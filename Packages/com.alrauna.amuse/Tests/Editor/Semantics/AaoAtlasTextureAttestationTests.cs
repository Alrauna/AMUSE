using System.IO;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Tests.Editor.Shared;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public sealed class AaoAtlasTextureAttestationTests
    {
        private const string FixtureFolder = "Assets/AmuseTests_AaoAttestation";

        private ObjectRegistryGuard RegistryGuard { get; set; }

        [SetUp]
        public void ResetAttestationStateBeforeEachTest()
        {
            AaoAtlasTextureAttestation.ResetForTests();
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

        private static void InstallAdmittedAaoVersion()
        {
            AaoAtlasTextureAttestation.ResetForTests();
            AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "1.9.17";
        }

        private static Texture2D NewAtlas(string name)
        {
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = name;
            return atlas;
        }

        /// <summary>
        /// Creates an asset-backed material, the shape the corroboration
        /// conjunct demands of the registered origin material's source.
        /// </summary>
        private Material NewAssetMaterial(string marker)
        {
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", Path.GetFileName(FixtureFolder));
            }
            var material = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest"));
            AssetDatabase.CreateAsset(
                material, $"{FixtureFolder}/{marker}.mat");
            return material;
        }

        [Test]
        public void ShippedAdmittedSetContainsOnlyTheCharacterizedVersion()
        {
            Assert.That(
                AaoAtlasTextureAttestation.AdmittedVersions,
                Is.EqualTo(new[] { "1.9.17" }),
                "1.9.17 joined on 2026-10-04 through the dated " +
                "characterization; any other version stays refused");
            Assert.That(
                AaoAtlasTextureAttestation.IsVersionAdmitted("1.9.17"),
                Is.True);
            Assert.That(
                AaoAtlasTextureAttestation.IsVersionAdmitted("1.9.18"),
                Is.False);
        }

        [Test]
        public void AtlasShapeRequiresThePinnedNameMarkers()
        {
            InstallAdmittedAaoVersion();
            var wrongName = NewAtlas("MainTex UV Packed");
            try
            {
                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlasShape(wrongName),
                    Is.False,
                    "a path-less texture without a pinned marker is not an " +
                    "AAO atlas, whatever its version");
            }
            finally
            {
                Object.DestroyImmediate(wrongName);
            }
        }

        [Test]
        public void AtlasShapeRefusesAnAssetBackedTexture()
        {
            InstallAdmittedAaoVersion();
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", Path.GetFileName(FixtureFolder));
            }
            var assetBacked = new Texture2D(
                4, 4, TextureFormat.RGBA32, false);
            assetBacked.name = "MainTex (AAO UV Packed)";
            AssetDatabase.CreateAsset(
                assetBacked, $"{FixtureFolder}/atlas-asset.asset");
            AssetDatabase.SaveAssets();

            Assert.That(
                AaoAtlasTextureAttestation.TryIdentifyAtlasShape(assetBacked),
                Is.False,
                "an asset-backed texture reads through the asset rules, " +
                "never through the in-memory admission");
        }

        [Test]
        public void AtlasShapeRefusesAnUnadmittedVersion()
        {
            AaoAtlasTextureAttestation.ResetForTests();
            AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "1.9.18";
            var atlas = NewAtlas("MainTex (AAO UV Packed)");
            try
            {
                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlasShape(atlas),
                    Is.False,
                    "an uncharacterized producer version keeps refusing");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
            }
        }

        [Test]
        public void AtlasShapeRefusesAnUnreadableVersion()
        {
            AaoAtlasTextureAttestation.ResetForTests();
            AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => null;
            var atlas = NewAtlas("MainTex (AAO UV Packed)");
            try
            {
                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlasShape(atlas),
                    Is.False,
                    "an unreadable or absent producer version keeps the " +
                    "gate closed");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
            }
        }

        [Test]
        public void CorroborationAcceptsARegisteredAssetBackedOriginMaterial()
        {
            InstallAdmittedAaoVersion();
            var source = NewAssetMaterial("origin-source");
            var clone = new Material(source.shader)
            {
                name = source.name + " build copy",
            };
            var atlas = NewAtlas("MainTex (AAO UV Packed)");
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, clone);

                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlas(atlas, clone),
                    Is.True,
                    "a one-hop registry pair to an asset-backed material " +
                    "corroborates the producer");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void CorroborationRefusesAnUnregisteredOriginMaterial()
        {
            InstallAdmittedAaoVersion();
            var clone = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest"));
            var atlas = NewAtlas("MainTex (AAO UV Packed)");
            try
            {
                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlas(atlas, clone),
                    Is.False,
                    "an unregistered slot material corroborates nothing");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void CorroborationRefusesAMemoryOnlySourceMaterial()
        {
            InstallAdmittedAaoVersion();
            var memorySource = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest"));
            var clone = new Material(memorySource.shader);
            var atlas = NewAtlas("MainTex (AAO UV Packed)");
            try
            {
                ObjectRegistry.RegisterReplacedObject(memorySource, clone);

                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlas(atlas, clone),
                    Is.False,
                    "the resolved source material must be asset-backed, " +
                    "exactly as the replacement conjunct demands of textures");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
                Object.DestroyImmediate(memorySource);
            }
        }

        [Test]
        public void CorroborationRefusesANullOriginMaterial()
        {
            InstallAdmittedAaoVersion();
            var atlas = NewAtlas("MainTex (AAO UV Packed)");
            try
            {
                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlas(atlas, null),
                    Is.False,
                    "the shape alone never admits; corroboration is a " +
                    "conjunct, not an option");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
            }
        }
    }
}
