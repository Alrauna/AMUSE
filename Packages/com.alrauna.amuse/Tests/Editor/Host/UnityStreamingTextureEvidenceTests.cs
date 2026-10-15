using Alrauna.Amuse.Tests.Editor.Shared;
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
            var pixels = new Color32[size * size];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(255, 255, 255, 255);
            }

            pixels[0] = new Color32(255, 255, 255, 3);
            var loaded = TestTextureImport.WritePng(
                path, size, size, pixels, importer =>
                {
                    importer.mipmapEnabled = true;
                    importer.isReadable = true;
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    importer.streamingMipmaps = true;
                });
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
        // This EXR staging stays local: it is the single occurrence of an
        // EXR fixture, and EncodeToEXR differs from the PNG builder's encoder.
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

        /// <summary>
        /// Imports a streaming-resident RGBA32 texture with a source
        /// file, two texels: alpha byte 128 and alpha byte 0. Byte 128
        /// decodes to 0.50196, so the cutoff pair 0.50196 and 0.50205
        /// straddles it while both quantize into the same 1/10000
        /// bucket the quantized key collapsed.
        /// </summary>
        private Texture2D ImportStreamingByte128Texture(string name)
        {
            const int width = 2;
            const int height = 1;
            var path = TempFolder + "/" + name + ".png";
            var pixels = new Color32[width * height];
            pixels[0] = new Color32(255, 255, 255, 128);
            pixels[1] = new Color32(255, 255, 255, 0);
            var loaded = TestTextureImport.WritePng(
                path, width, height, pixels, importer =>
                {
                    importer.mipmapEnabled = true;
                    importer.isReadable = true;
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    importer.streamingMipmaps = true;
                });
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

        /// <summary>
        /// The streaming cache quantized the cutoff into a 1/10000
        /// bucket, so two cutoffs inside one bucket straddling a texel
        /// byte served one chain to both verdicts: the first capture's
        /// verdict survived a second cutoff that must decide the other
        /// way. The key carries the exact clamped cutoff, so each
        /// cutoff captures its own chain.
        /// </summary>
        [Test]
        public void TwoCutoffsInOneBucketStraddlingByte128CaptureSeparateChains()
        {
            var texture = ImportStreamingByte128Texture(
                "streaming_cutoff_bucket_straddle");

            AlphaMipChain first;
            AlphaMipChain second;
            UnityStreamingTextureEvidence.TryCapture(
                texture,
                TextureChannel.Alpha,
                0.50196f,
                AlphaPolicyBounds.Inert,
                out first,
                allowDiskFallback: false);
            UnityStreamingTextureEvidence.TryCapture(
                texture,
                TextureChannel.Alpha,
                0.50205f,
                AlphaPolicyBounds.Inert,
                out second,
                allowDiskFallback: false);

            // Byte 128 decodes to ~0.50196: it passes the first cutoff,
            // fails the second.
            Assert.That(
                second[0].GetAlpha(0, 0),
                Is.Not.EqualTo(first[0].GetAlpha(0, 0)),
                "the quantized key served one chain to both cutoffs");
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

        /// <summary>
        /// The first fail-closed arm: a texture with no asset path has no
        /// source the route could clone or read, so the capture refuses
        /// before any file work. The call shape follows the two happy-path
        /// tests above.
        /// </summary>
        [Test]
        public void TryCapture_TextureWithNoAssetPath_RefusesFailClosed()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, true);

            var ok = false;
            AlphaMipChain chain = null;
            try
            {
                ok = UnityStreamingTextureEvidence.TryCapture(
                    texture,
                    TextureChannel.Alpha,
                    1.0f,
                    AlphaPolicyBounds.Inert,
                    out chain);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            Assert.That(ok, Is.False, "no asset path must refuse");
            Assert.That(chain, Is.Null);
        }

        /// <summary>
        /// A fail-closed refusal for a texture that sits on an asset path
        /// but is not its file's main asset. The capture seam is silent on
        /// this extension: the source-image reader's extension gate
        /// refuses before the main-asset check, and TryCapture exposes no
        /// diagnostic, so the row proves the fail-closed refusal without
        /// arm attribution. The arm-level attribution (the non-main-asset
        /// check) lives in the execution report, alongside the fixture's
        /// own main-asset preconditions below.
        /// </summary>
        [Test]
        public void TryCapture_SubAssetTextureOnAnExistingPath_RefusesFailClosed()
        {
            var container =
                ScriptableObject.CreateInstance
                    <nadena.dev.ndmf.runtime.SubAssetContainer>();
            var containerPath = TempFolder +
                "/streaming_evidence_subasset_container.asset";
            AssetDatabase.CreateAsset(container, containerPath);

            var subTexture = new Texture2D(4, 4, TextureFormat.RGBA32, true);
            AssetDatabase.AddObjectToAsset(subTexture, containerPath);
            AssetDatabase.SaveAssets();

            var ok = false;
            AlphaMipChain chain = null;
            try
            {
                // No-op guards: the row proves the fail-closed refusal,
                // and which arm returned it is not observable through
                // this silent seam. The fixture must sit on a real path
                // whose main asset is someone else, so the non-main-asset
                // precondition holds regardless of which internal gate
                // refused, or the assertion below proves nothing.
                Assert.That(
                    AssetDatabase.GetAssetPath(subTexture),
                    Is.EqualTo(containerPath),
                    "fixture precondition: the sub-asset must sit on the " +
                    "container path");
                Assert.That(
                    ReferenceEquals(
                        AssetDatabase.LoadMainAssetAtPath(containerPath),
                        subTexture),
                    Is.False,
                    "fixture precondition: the main asset must not be the " +
                    "sub-asset");

                ok = UnityStreamingTextureEvidence.TryCapture(
                    subTexture,
                    TextureChannel.Alpha,
                    1.0f,
                    AlphaPolicyBounds.Inert,
                    out chain);
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                        containerPath) != null)
                {
                    AssetDatabase.DeleteAsset(containerPath);
                }

                if (subTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(subTexture);
                }
            }

            Assert.That(ok, Is.False, "a non-main-asset must refuse");
            Assert.That(chain, Is.Null);
        }

        /// <summary>
        /// The disk-fallback switch rides the capture request, not
        /// process state. One capture that allows the route caches a
        /// clone-provenance chain; a later capture of the same texture
        /// whose own request disallows the route is never served that
        /// entry: the key names the route that built the chain, and the
        /// disallowed capture refuses rather than serve it.
        /// </summary>
        [Test]
        public void ARequestDisallowingFallbackNeverReadsAFallbackCapturedChain()
        {
            var texture = ImportExrStreamingBandTexture(
                "streaming_fallback_request_strict");
            try
            {
                var okFallback = UnityStreamingTextureEvidence.TryCapture(
                    texture,
                    TextureChannel.Alpha,
                    0.25f,
                    AlphaPolicyBounds.Inert,
                    out var fallbackChain);
                Assert.That(
                    okFallback, Is.True,
                    "fixture precondition: the allowed capture must " +
                    "reach the clone fallback");
                Assert.That(fallbackChain, Is.Not.Null);

                var okStrict = UnityStreamingTextureEvidence.TryCapture(
                    texture,
                    TextureChannel.Alpha,
                    0.25f,
                    AlphaPolicyBounds.Inert,
                    out var strictChain,
                    allowDiskFallback: false);
                Assert.That(
                    okStrict, Is.False,
                    "a disallowing request must never be served the " +
                    "fallback-provenance cache entry");
                Assert.That(strictChain, Is.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(texture));
            }
        }

        [Test]
        public void DisallowingDiskFallbackReturnsFalseWhenSourceImageFailsWithoutWritingAssets()
        {
            var texture = ImportExrStreamingBandTexture("unreadable_streaming_for_guard");
            try
            {
                var ok = UnityStreamingTextureEvidence.TryCapture(
                    texture,
                    TextureChannel.Alpha,
                    1.0f,
                    AlphaPolicyBounds.Inert,
                    out var chain,
                    allowDiskFallback: false);
                Assert.That(ok, Is.False);
                Assert.That(chain, Is.Null);
                Assert.That(
                    AssetDatabase.IsValidFolder("Assets/Amuse.StreamingReadback"),
                    Is.False,
                    "disallowing disk fallback must not create the temporary streaming folder");
            }
            finally
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(texture));
            }
        }
    }
}
