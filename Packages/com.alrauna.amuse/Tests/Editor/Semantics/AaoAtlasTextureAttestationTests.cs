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
        public void AtlasShapeRequiresThePinnedNameMarkers()
        {
            var wrongName = NewAtlas("MainTex UV Packed");
            try
            {
                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlasShape(wrongName),
                    Is.False,
                    "a path-less texture without a pinned marker is not " +
                    "an AAO atlas");
            }
            finally
            {
                Object.DestroyImmediate(wrongName);
            }
        }

        [Test]
        public void AtlasShapeRefusesAnAssetBackedTexture()
        {
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
        public void CorroborationAcceptsARegisteredAssetBackedOriginMaterial()
        {
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
