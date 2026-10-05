using System.IO;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Tests.Editor.Shared;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public sealed class ReplacementTextureIdentityTests
    {
        private const string FixtureFolder = "Assets/AmuseTests_LacIdentity";

        private ObjectRegistryGuard _registryGuard;

        [SetUp]
        public void StoreRegistry()
        {
            _registryGuard = new ObjectRegistryGuard();
        }

        [TearDown]
        public void RestoreRegistry()
        {
            _registryGuard.Dispose();
            ReplacementTextureIdentity.ClearSession();
        }

        [Test]
        public void MintedIdentityNamesTheSourceAsset()
        {
            // The registry lookup needs an asset-backed source, so this
            // test imports one. The folder helper mirrors the shared
            // fixture shape.
            var source = ImportSourceAsset("mint");
            var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copy.name = source.name + "_compressed";
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copy);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    source, out var guid, out long localId);
                var minted = ReplacementTextureIdentity.TryMint(
                    source, copy.GetInstanceID(), out var id);
                Assert.That(minted, Is.True);
                Assert.That(
                    id.Value, Is.EqualTo(
                        "unity-replacement:" + guid.ToLowerInvariant() +
                        ":" + localId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
                DeleteTempFolder();
            }
        }

        [Test]
        public void SameCopyRemintingOneSourceStaysUsable()
        {
            var source = ImportSourceAsset("remint");
            var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copy.name = source.name + "_compressed";
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copy);
                ReplacementTextureIdentity.TryMint(
                    source, copy.GetInstanceID(), out var id);
                Assert.That(
                    ReplacementTextureIdentity.TryMint(
                        source, copy.GetInstanceID(), out _),
                    Is.True);
                Assert.That(
                    ReplacementTextureIdentity.IsUsable(id), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
                DeleteTempFolder();
            }
        }

        [Test]
        public void SecondCopyClaimingOneSourcePoisonsTheIdentityForBoth()
        {
            // --- Falsifier 4: two distinct objects claiming one source
            // refuse both, and the refusal names the identity fact. ---
            var source = ImportSourceAsset("contested");
            var copyA = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copyA.name = source.name + "_compressed";
            var copyB = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copyB.name = source.name + "_compressed";
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copyA);
                ObjectRegistry.RegisterReplacedObject(source, copyB);
                Assert.That(
                    ReplacementTextureIdentity.TryMint(
                        source, copyA.GetInstanceID(), out var idA),
                    Is.True);
                Assert.That(
                    ReplacementTextureIdentity.TryMint(
                        source, copyB.GetInstanceID(), out _),
                    Is.False,
                    "the second claimant refuses at mint time");
                Assert.That(
                    ReplacementTextureIdentity.IsUsable(idA), Is.False,
                    "the first claimant loses usability too");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copyA);
                UnityEngine.Object.DestroyImmediate(copyB);
                DeleteTempFolder();
            }
        }

        [Test]
        public void ClearSessionDropsEveryMintedClaim()
        {
            var source = ImportSourceAsset("cleared");
            var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copy.name = source.name + "_compressed";
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copy);
                ReplacementTextureIdentity.TryMint(
                    source, copy.GetInstanceID(), out var id);
                ReplacementTextureIdentity.ClearSession();
                Assert.That(
                    ReplacementTextureIdentity.TryMint(
                        source, copy.GetInstanceID(), out _),
                    Is.True,
                    "a cleared session accepts the claim again");
                Assert.That(
                    ReplacementTextureIdentity.IsUsable(id), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
                DeleteTempFolder();
            }
        }

        /// <summary>
        /// Imports a real texture asset so the source side of a
        /// registration is asset-backed, the shape the mint demands of
        /// every source. Mirrors the shared fixture shape of the
        /// attestation tests. Kept local: the deferred (non-forced)
        /// import and the identity asserts do not fit the WritePng
        /// callback shape.
        /// </summary>
        private static Texture2D ImportSourceAsset(string marker)
        {
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", Path.GetFileName(FixtureFolder));
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

        private static void DeleteTempFolder()
        {
            if (AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.DeleteAsset(FixtureFolder);
            }
        }
    }
}
