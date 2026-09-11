using System.IO;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// The streaming route's bounds cache contract, mirrored from the
    /// generated route's session cache test: one texture captured under
    /// two policies serves two different chains, and the cached inert
    /// chain is never served under gate-on bounds (spec section 9,
    /// cache keys).
    /// </summary>
    [TestFixture]
    public sealed class UnityStreamingTextureEvidenceTests
    {
        private const string TempFolder = "Assets/AmuseTests_StreamingEvidence";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_StreamingEvidence");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        /// <summary>
        /// Imports a streaming-resident RGBA32 texture with a source
        /// file, every texel alpha 255 except one stray texel at 3. The
        /// stray is a witness under the inert bounds and noise under a
        /// 2 percent gate, so the two policies must produce different
        /// chains.
        /// </summary>
        private Texture2D ImportStreamingStrayTexture(string name)
        {
            const int size = 8;
            var path = TempFolder + "/" + name + ".png";
            var staging = new Texture2D(
                size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(255, 255, 255, 255);
            }

            pixels[0] = new Color32(255, 255, 255, 3);
            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.streamingMipmaps = true;
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(
                loaded, Is.Not.Null,
                $"Streaming texture '{path}' must load.");
            Assert.That(
                loaded.streamingMipmaps, Is.True,
                "fixture precondition: the imported texture must be " +
                "streaming-resident");
            return loaded;
        }

        [Test]
        public void StreamingCache_DoesNotServeInertEvidenceUnderGateOnBounds()
        {
            var texture = ImportStreamingStrayTexture(
                "streaming_bounds_recapture");

            var okInert = UnityStreamingTextureEvidence.TryCapture(
                texture,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var inertChain);
            var okGateOn = UnityStreamingTextureEvidence.TryCapture(
                texture,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.From(100, 2),
                out var gateOnChain);
            var okCached = UnityStreamingTextureEvidence.TryCapture(
                texture,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var cachedChain);

            Assert.That(okInert, Is.True);
            Assert.That(okGateOn, Is.True);
            Assert.That(okCached, Is.True);

            // The bounds ride in the disk cache key, so the gate-on
            // call re-captured under its own policy instead of serving
            // the cached inert chain, and the second inert call served
            // that first chain again.
            Assert.That(
                ReferenceEquals(inertChain, gateOnChain), Is.False);
            Assert.That(
                ReferenceEquals(inertChain, cachedChain), Is.True);

            Assert.That(inertChain[0].GetAlpha(0, 0), Is.EqualTo(0));
            Assert.That(
                gateOnChain[0].GetAlpha(0, 0),
                Is.EqualTo(AlphaTextureData.ErasedFlag));
        }
    }
}
