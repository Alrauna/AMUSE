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

        /// <summary>
        /// Imports a streaming RGBA32 texture whose source file is an
        /// EXR image: the source-image reader refuses the extension, so
        /// the capture reaches the readable-clone fallback this test
        /// exercises. Four vertical alpha bands of 0, 32, 64, and 255
        /// keep the cutoff verdicts separated per texel band.
        /// </summary>
        private Texture2D ImportExrStreamingBandTexture(string name)
        {
            const int size = 64;
            var path = TempFolder + "/" + name + ".exr";
            var staging = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var alpha = x < size / 4
                        ? (byte)0
                        : x < size / 2
                            ? (byte)32
                            : x < size * 3 / 4 ? (byte)64 : (byte)255;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToEXR());
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
                loaded.width, Is.EqualTo(size),
                "fixture precondition: the EXR import must keep the " +
                "band grid");
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

        /// <summary>
        /// The readable-clone fallback's cutoff binarization compares
        /// decoded texel alphas against the material's declared cutoff.
        /// A texel whose decoded alpha sits below the cutoff is
        /// discarded at runtime and must never read opaque, so the
        /// comparison runs on the decoded [0,1] value, never on the raw
        /// stored byte.
        /// </summary>
        [Test]
        public void CloneRoute_BinarizesByTheDecodedCutoffNotTheRawByte()
        {
            var texture = ImportExrStreamingBandTexture(
                "streaming_clone_cutoff");

            var ok = UnityStreamingTextureEvidence.TryCapture(
                texture,
                TextureChannel.Alpha,
                0.25f,
                AlphaPolicyBounds.Inert,
                out var chain);
            Assert.That(ok, Is.True, "the clone fallback must capture");

            // Mip 0 bands at 64ths: alpha 0, 32, 64, 255. Byte 32
            // decodes to 0.125 - below the 0.25 cutoff - so it must
            // stay a witness, and byte 64 decodes to 0.251 - at or
            // above the cutoff - so it may read opaque.
            Assert.That(chain[0].GetAlpha(8, 32), Is.EqualTo(0));
            Assert.That(chain[0].GetAlpha(24, 32), Is.EqualTo(0));
            Assert.That(
                chain[0].GetAlpha(40, 32), Is.EqualTo(byte.MaxValue));
            Assert.That(
                chain[0].GetAlpha(56, 32), Is.EqualTo(byte.MaxValue));
        }
    }
}
