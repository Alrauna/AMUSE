using System.IO;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The fixed session-cache reset list. The reset must drop the locked
    /// source alignment memo like every other session cache, because a
    /// stale aligned record would survive into the next build (A8.4).
    /// </summary>
    public sealed class AmuseSessionCachesTests
    {
        // The streaming capture fixture folder. The teardown deletes it
        // whole, so a failed assertion cannot leak the imported PNG.
        private const string StreamingFolder =
            "Assets/AmuseTests_StreamingEvidence";

        private readonly TestTransientScope _scope =
            new TestTransientScope(allowDestroyingAssets: true);

        [SetUp]
        public void SetUp()
        {
            LockedSourceFixture.DeleteFolder();
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            _scope.TearDown();
            LockedSourceFixture.DeleteFolder();
            if (AssetDatabase.IsValidFolder(StreamingFolder))
            {
                AssetDatabase.DeleteAsset(StreamingFolder);
            }
            AssetDatabase.Refresh();
        }

        [Test]
        public void TheSessionCacheResetClearsTheLockedSourceAlignmentMemo()
        {
            var fixture = new LockedSourceFixture("caches");
            var locked = fixture.Locked("CacheCape");
            _scope.Track(locked);

            var originalText = LockedMaterialIdentity.ReadOriginalText(
                fixture.Original);
            Assert.That(originalText, Is.Not.Null,
                "fixture guard: the original text must be readable");
            var properties = LockedSourceAlignmentMemo.PropertiesOf(
                fixture.Original, originalText);

            // Prime the memo with the aligned pair.
            var primed = LockedSourceAlignmentMemo.GetOrAlign(
                fixture.Partner, fixture.Original, properties);
            Assert.That(primed.Succeeded, Is.True,
                "fixture guard: the pair must align before the reset, " +
                "but the aligner refused with " +
                (primed.RefusalDetail ?? "no sentence"));

            // The generated text changes while the write time stays
            // pinned. A stale memo would return the primed success.
            var generatedPath =
                AssetDatabase.GetAssetPath(fixture.Partner);
            var stamp = File.GetLastWriteTimeUtc(generatedPath);
            var broken = new LockedSourceFixture("caches");
            broken.WithGeneratedLine(
                "shader_master_label",
                "        shader_master_label " +
                "(\"Another Label\", Float) = 0");
            Assert.That(
                AssetDatabase.GetAssetPath(broken.Partner),
                Is.EqualTo(generatedPath),
                "fixture guard: the rewrite must target the same asset " +
                "path");
            File.SetLastWriteTimeUtc(generatedPath, stamp);

            AmuseSessionCaches.Clear();

            var cleared = LockedSourceAlignmentMemo.GetOrAlign(
                fixture.Partner, fixture.Original, properties);
            Assert.That(cleared.Succeeded, Is.False,
                "the reset must drop the memo, so the changed text " +
                "re-aligns");
            Assert.That(cleared.RefusalDetail,
                Is.EqualTo(
                    "A generated line does not match the expected " +
                    "line."),
                "the re-alignment must see the changed text");
        }

        // --- Falsifier 4: the reset that forgets the streaming cache ---
        [Test]
        public void TheSessionCacheResetClearsTheStreamingEvidenceCache()
        {
            EnsureStreamingFixtureFolder();
            var texture = ImportStreamingByte128Texture(
                "streaming_session_reset");

            // The lightest capture the streaming evidence tests prove:
            // a readable uncompressed texture captures directly, and a
            // success stores one chain in the cache.
            var captured = UnityStreamingTextureEvidence.TryCapture(
                texture,
                TextureChannel.Alpha,
                0.50196f,
                AlphaPolicyBounds.Inert,
                out _,
                allowDiskFallback: false);
            Assert.That(captured, Is.True,
                "fixture guard: the streaming capture must succeed " +
                "before the reset, or the probe proves nothing");

            // The no-op guard: the cache must hold the captured chain
            // before the reset runs. An empty start would let the last
            // assertion pass without testing the reset at all.
            Assert.That(
                UnityStreamingTextureEvidence.CacheIsEmpty, Is.False,
                "fixture guard: the capture path must fill the cache " +
                "before the reset, or the probe proves nothing");

            AmuseSessionCaches.Clear();

            Assert.That(
                UnityStreamingTextureEvidence.CacheIsEmpty, Is.True,
                "the reset must drop the streaming cache with the " +
                "other session caches");
        }

        /// <summary>
        /// Imports a streaming-resident readable texture, the lightest
        /// fixture the streaming evidence tests use. The texture is
        /// uncompressed and readable, so the capture route reads the
        /// texels directly and needs no clone folder.
        /// </summary>
        private Texture2D ImportStreamingByte128Texture(string name)
        {
            const int width = 2;
            const int height = 1;
            var path = StreamingFolder + "/" + name + ".png";
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

        /// <summary>
        /// Creates the streaming fixture folder when it is absent. The
        /// import helper needs a folder under Assets for the PNG.
        /// </summary>
        private static void EnsureStreamingFixtureFolder()
        {
            if (!AssetDatabase.IsValidFolder(StreamingFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_StreamingEvidence");
            }
        }
    }
}
