using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Tests.Editor.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The page builder and the page check of the atlas route. The
    /// fixtures import real streaming textures, acquire them through
    /// the evidence route, and build small pages that carry every
    /// supported format. The check must hold exact equality of the
    /// decoded protected levels, because the protected levels are
    /// raw block copies of the source chains.
    /// </summary>
    [TestFixture]
    public sealed class AtlasPageBuilderTests
    {
        private const string TempFolder = "Assets/AmuseTests_AtlasPageBuilder";

        private static readonly AtlasSamplerFacts RepeatSampler =
            new AtlasSamplerFacts(
                FilterMode.Bilinear,
                UnityEngine.TextureWrapMode.Repeat,
                UnityEngine.TextureWrapMode.Repeat,
                0,
                0f);

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_AtlasPageBuilder");
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
        /// A texel pattern that differs at every coordinate, so any
        /// misplaced block or border texel fails the check.
        /// </summary>
        private static Color32 NoiseTexel(int x, int y)
        {
            return new Color32(
                (byte)(17 * x + 3),
                (byte)(29 * y + 5),
                (byte)(x * 31 + y * 7),
                (byte)(x * x + 3 * y + 11));
        }

        /// <summary>
        /// Imports a streaming, non-readable source texture with a
        /// source file. The compression and the color space choose the
        /// imported format.
        /// </summary>
        private Texture2D ImportSource(
            string name,
            int size,
            TextureImporterCompression compression,
            bool srgb,
            bool alpha)
        {
            var path = TempFolder + "/" + name + ".png";
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    pixels[y * size + x] = NoiseTexel(x, y);
                }
            }

            var loaded = TestTextureImport.WritePng(
                path, size, size, pixels, importer =>
                {
                    importer.mipmapEnabled = true;
                    importer.isReadable = false;
                    importer.textureCompression = compression;
                    importer.streamingMipmaps = true;
                    importer.sRGBTexture = srgb;
                },
                alpha);
            Assert.That(
                loaded.streamingMipmaps, Is.True,
                "fixture precondition: the imported texture must be " +
                "streaming-resident");
            Assert.That(
                loaded.isReadable, Is.False,
                "fixture precondition: the imported texture must not " +
                "be readable");
            return loaded;
        }
        /// <summary>
        /// Acquires a source image and fails the test when the
        /// acquisition refuses.
        /// </summary>
        private static AtlasSourceImage Acquire(Texture2D source, int p)
        {
            var ok = UnityStreamingTextureEvidence.TryAcquireAtlasSource(
                source, p, out var image, out var refusal);
            Assert.That(
                ok, Is.True,
                "fixture precondition: the acquisition must succeed, " +
                "refusal " + refusal);
            return image;
        }

        private static Dictionary<int, AtlasSourceImage> SourcesOf(
            Texture2D source,
            AtlasSourceImage image)
        {
            return new Dictionary<int, AtlasSourceImage>
            {
                { source.GetInstanceID(), image },
            };
        }

        private static AtlasPlacement PlacementOf(
            Texture2D source,
            AtlasSamplerFacts sampler,
            int sourceX,
            int sourceY,
            int width,
            int height,
            int pageX,
            int pageY)
        {
            return new AtlasPlacement(
                source.GetInstanceID(),
                sampler,
                sourceX,
                sourceY,
                width,
                height,
                0,
                pageX,
                pageY,
                new[] { 0 });
        }


        /// <summary>The full mip chain length of a page of this size.</summary>
        private static int FullMipCount(int width, int height)
        {
            return (int)Math.Floor(Math.Log(Math.Max(width, height), 2.0)) + 1;
        }
        /// <summary>
        /// The level 0 border of the design: the larger of the
        /// anisotropy reach and the block size, scaled by the protected
        /// level count. The sampler asks for no anisotropy, so the
        /// reach is 1.
        /// </summary>
        private static int BorderOf(bool compressed, int p)
        {
            return Math.Max(1, compressed ? 4 : 1) * (1 << (p - 1));
        }

        [TestCase(
            TextureImporterCompression.Uncompressed, true, true,
            TextureFormat.RGBA32)]
        [TestCase(
            TextureImporterCompression.Uncompressed, false, true,
            TextureFormat.RGBA32)]
        [TestCase(
            TextureImporterCompression.Uncompressed, true, false,
            TextureFormat.RGB24)]
        [TestCase(
            TextureImporterCompression.Uncompressed, false, false,
            TextureFormat.RGB24)]
        [TestCase(
            TextureImporterCompression.Compressed, true, false,
            TextureFormat.DXT1)]
        [TestCase(
            TextureImporterCompression.Compressed, false, false,
            TextureFormat.DXT1)]
        [TestCase(
            TextureImporterCompression.Compressed, true, true,
            TextureFormat.DXT5)]
        [TestCase(
            TextureImporterCompression.Compressed, false, true,
            TextureFormat.DXT5)]
        [TestCase(
            TextureImporterCompression.CompressedHQ, true, true,
            TextureFormat.BC7)]
        [TestCase(
            TextureImporterCompression.CompressedHQ, false, true,
            TextureFormat.BC7)]
        public void PageHasMipMapsAndStreamingOn(
            TextureImporterCompression compression,
            bool srgb,
            bool alpha,
            TextureFormat expectedFormat)
        {
            var source = ImportSource(
                "page_formats_" + expectedFormat + (srgb ? "_srgb" : "_lin"),
                64,
                compression,
                srgb,
                alpha);
            Texture2D page = null;
            try
            {
                Assert.That(
                    source.format, Is.EqualTo(expectedFormat),
                    "fixture precondition: the import must land on the " +
                    "planned format");
                var image = Acquire(source, 1);
                var compressed =
                    expectedFormat != TextureFormat.RGBA32 &&
                    expectedFormat != TextureFormat.RGB24;
                var border = BorderOf(compressed, 1);
                var region = 64 + 2 * border;
                var placement = PlacementOf(
                    source,
                    RepeatSampler,
                    -border,
                    -border,
                    region,
                    region,
                    0,
                    0);
                var mips = FullMipCount(region, region);
                var plan = new AtlasPagePlanEntry(
                    region,
                    region,
                    source.graphicsFormat,
                    mips,
                    new[] { placement });

                page = AtlasPageBuilder.Build(
                    plan,
                    RepeatSampler,
                    SourcesOf(source, image),
                    1,
                    new AtlasByteLedger(long.MaxValue));

                Assert.That(
                    page.graphicsFormat,
                    Is.EqualTo(source.graphicsFormat),
                    "the page must keep the source format, sRGB flag " +
                    "included");
                Assert.That(page.mipmapCount, Is.EqualTo(mips));
                var serialized = new SerializedObject(page);
                Assert.That(
                    serialized.FindProperty("m_StreamingMipmaps").boolValue,
                    Is.True);
                Assert.That(page.streamingMipmaps, Is.True);
            }
            finally
            {
                if (page != null)
                {
                    UnityEngine.Object.DestroyImmediate(page);
                }
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(source));
            }
        }

        [TestCase(TextureFormat.BC7)]
        [TestCase(TextureFormat.DXT1)]
        public void ProtectedLevelsCopyExactly(TextureFormat sourceFormat)
        {
            const int p = 2;
            var compression = sourceFormat == TextureFormat.BC7
                ? TextureImporterCompression.CompressedHQ
                : TextureImporterCompression.Compressed;
            var source = ImportSource(
                "protected_exact_" + sourceFormat,
                64,
                compression,
                true,
                sourceFormat != TextureFormat.DXT1);
            Texture2D page = null;
            try
            {
                Assert.That(
                    source.format, Is.EqualTo(sourceFormat),
                    "fixture precondition: the import must land on the " +
                    "planned format");
                var image = Acquire(source, p);
                var border = BorderOf(true, p);
                var region = 64 + 2 * border;
                var placement = PlacementOf(
                    source,
                    RepeatSampler,
                    -border,
                    -border,
                    region,
                    region,
                    0,
                    0);
                var mips = FullMipCount(region, region);
                var plan = new AtlasPagePlanEntry(
                    region,
                    region,
                    source.graphicsFormat,
                    mips,
                    new[] { placement });
                var sources = SourcesOf(source, image);

                page = AtlasPageBuilder.Build(
                    plan,
                    RepeatSampler,
                    sources,
                    p,
                    new AtlasByteLedger(long.MaxValue));

                Assert.DoesNotThrow(
                    () => AtlasPageCheck.Verify(page, plan, sources, p),
                    "every protected level must decode to the source " +
                    "texels");
            }
            finally
            {
                if (page != null)
                {
                    UnityEngine.Object.DestroyImmediate(page);
                }
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(source));
            }
        }

        [Test]
        public void PlacementOneBlockOffTheGridFailsTheCheck()
        {
            const int p = 2;
            var source = ImportSource(
                "off_grid_origin",
                64,
                TextureImporterCompression.Compressed,
                true,
                false);
            Texture2D page = null;
            try
            {
                Assert.That(
                    source.format, Is.EqualTo(TextureFormat.DXT1),
                    "fixture precondition: the import must land on DXT1");
                var image = Acquire(source, p);
                // The grid for a compressed source at two protected
                // levels is eight texels. Both origins sit four texels
                // off, so level 0 still aligns to blocks and level 1
                // no longer does.
                var placement = PlacementOf(
                    source,
                    RepeatSampler,
                    -4,
                    -4,
                    80,
                    80,
                    8,
                    8);
                var plan = new AtlasPagePlanEntry(
                    88,
                    88,
                    source.graphicsFormat,
                    FullMipCount(88, 88),
                    new[] { placement });
                var sources = SourcesOf(source, image);

                page = AtlasPageBuilder.Build(
                    plan,
                    RepeatSampler,
                    sources,
                    p,
                    new AtlasByteLedger(long.MaxValue));

                Assert.Throws<InvalidOperationException>(
                    () => AtlasPageCheck.Verify(page, plan, sources, p),
                    "a level 1 block that no longer aligns must fail " +
                    "the check");
            }
            finally
            {
                if (page != null)
                {
                    UnityEngine.Object.DestroyImmediate(page);
                }
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(source));
            }
        }

        [Test]
        public void Bc1ClampEdgeBorderIsExact()
        {
            var source = ImportSource(
                "bc1_clamp_edge",
                64,
                TextureImporterCompression.Compressed,
                true,
                false);
            Texture2D page = null;
            try
            {
                Assert.That(
                    source.format, Is.EqualTo(TextureFormat.DXT1),
                    "fixture precondition: the import must land on DXT1");
                var image = Acquire(source, 1);
                var clampSampler = new AtlasSamplerFacts(
                    FilterMode.Bilinear,
                    UnityEngine.TextureWrapMode.Clamp,
                    UnityEngine.TextureWrapMode.Clamp,
                    0,
                    0f);
                // The region covers the top right quarter of the
                // source and extends eight texels past both edges.
                var placement = PlacementOf(
                    source,
                    clampSampler,
                    32,
                    32,
                    40,
                    40,
                    0,
                    0);
                var plan = new AtlasPagePlanEntry(
                    40,
                    40,
                    source.graphicsFormat,
                    FullMipCount(40, 40),
                    new[] { placement });
                var sources = SourcesOf(source, image);

                page = AtlasPageBuilder.Build(
                    plan,
                    clampSampler,
                    sources,
                    1,
                    new AtlasByteLedger(long.MaxValue));
                AtlasPageCheck.Verify(page, plan, sources, 1);

                var pageColors = page.GetPixels32(0);
                // The corner texel of the border clamps to the source
                // corner texel on both axes.
                var corner = pageColors[36 * 40 + 36];
                var expectedCorner = image.Level0[63 * 64 + 63];
                // The right border texel clamps only its column.
                var rightEdge = pageColors[10 * 40 + 36];
                var expectedRightEdge = image.Level0[42 * 64 + 63];
                Assert.That(
                    corner.r + "|" + corner.g + "|" + corner.b + "|" +
                    corner.a,
                    Is.EqualTo(
                        expectedCorner.r + "|" + expectedCorner.g + "|" +
                        expectedCorner.b + "|" + expectedCorner.a),
                    "the clamp corner border must repeat the edge texel");
                Assert.That(
                    rightEdge.r + "|" + rightEdge.g + "|" + rightEdge.b +
                    "|" + rightEdge.a,
                    Is.EqualTo(
                        expectedRightEdge.r + "|" + expectedRightEdge.g +
                        "|" + expectedRightEdge.b + "|" +
                        expectedRightEdge.a),
                    "the clamp right border must repeat the edge texel");
            }
            finally
            {
                if (page != null)
                {
                    UnityEngine.Object.DestroyImmediate(page);
                }
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(source));
            }
        }

        [Test]
        public void RepeatBorderCopiesTheOppositeEdge()
        {
            var source = ImportSource(
                "repeat_opposite_edge",
                32,
                TextureImporterCompression.Uncompressed,
                true,
                true);
            Texture2D page = null;
            try
            {
                Assert.That(
                    source.format, Is.EqualTo(TextureFormat.RGBA32),
                    "fixture precondition: the import must land on " +
                    "RGBA32");
                var image = Acquire(source, 1);
                var border = BorderOf(false, 1);
                var region = 32 + 2 * border;
                var placement = PlacementOf(
                    source,
                    RepeatSampler,
                    -border,
                    -border,
                    region,
                    region,
                    0,
                    0);
                var plan = new AtlasPagePlanEntry(
                    region,
                    region,
                    source.graphicsFormat,
                    FullMipCount(region, region),
                    new[] { placement });
                var sources = SourcesOf(source, image);

                page = AtlasPageBuilder.Build(
                    plan,
                    RepeatSampler,
                    sources,
                    1,
                    new AtlasByteLedger(long.MaxValue));
                AtlasPageCheck.Verify(page, plan, sources, 1);

                var pageColors = page.GetPixels32(0);
                // The last column wraps back to source column zero and
                // the first row wraps back to the top source row.
                var wrapped = pageColors[0 * region + (region - 1)];
                var expectedWrapped = image.Level0[31 * 32 + 0];
                var interior = pageColors[17 * region + 18];
                var expectedInterior = image.Level0[16 * 32 + 17];
                Assert.That(
                    wrapped.r + "|" + wrapped.g + "|" + wrapped.b + "|" +
                    wrapped.a,
                    Is.EqualTo(
                        expectedWrapped.r + "|" + expectedWrapped.g + "|" +
                        expectedWrapped.b + "|" + expectedWrapped.a),
                    "the Repeat border must copy the opposite edge");
                Assert.That(
                    interior.r + "|" + interior.g + "|" + interior.b + "|" +
                    interior.a,
                    Is.EqualTo(
                        expectedInterior.r + "|" + expectedInterior.g + "|" +
                        expectedInterior.b + "|" + expectedInterior.a),
                    "the interior texel must stay in place");
            }
            finally
            {
                if (page != null)
                {
                    UnityEngine.Object.DestroyImmediate(page);
                }
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(source));
            }
        }

        [Test]
        public void PageCopiesTheSourceSampler()
        {
            var source = ImportSource(
                "sampler_copy",
                32,
                TextureImporterCompression.Uncompressed,
                true,
                true);
            var sampler = new AtlasSamplerFacts(
                FilterMode.Point,
                UnityEngine.TextureWrapMode.Clamp,
                UnityEngine.TextureWrapMode.Repeat,
                4,
                0.5f);
            Texture2D page = null;
            try
            {
                var image = Acquire(source, 1);
                var border = BorderOf(false, 1);
                var region = 32 + 2 * border;
                var placement = PlacementOf(
                    source,
                    sampler,
                    -border,
                    -border,
                    region,
                    region,
                    0,
                    0);
                var plan = new AtlasPagePlanEntry(
                    region,
                    region,
                    source.graphicsFormat,
                    FullMipCount(region, region),
                    new[] { placement });

                page = AtlasPageBuilder.Build(
                    plan,
                    sampler,
                    SourcesOf(source, image),
                    1,
                    new AtlasByteLedger(long.MaxValue));

                Assert.That(page.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(page.wrapModeU, Is.EqualTo(UnityEngine.TextureWrapMode.Clamp));
                Assert.That(
                    page.wrapModeV, Is.EqualTo(UnityEngine.TextureWrapMode.Repeat));
                Assert.That(page.anisoLevel, Is.EqualTo(4));
                Assert.That(page.mipMapBias, Is.EqualTo(0.5f));
            }
            finally
            {
                if (page != null)
                {
                    UnityEngine.Object.DestroyImmediate(page);
                }
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(source));
            }
        }

        [Test]
        public void TwoSourcePageStaysInsideItsPrediction()
        {
            const int p = 1;
            var first = ImportSource(
                "ledger_first",
                64,
                TextureImporterCompression.Uncompressed,
                true,
                true);
            var second = ImportSource(
                "ledger_second",
                64,
                TextureImporterCompression.Uncompressed,
                true,
                true);
            Texture2D page = null;
            try
            {
                var border = BorderOf(false, p);
                var region = 64 + 2 * border;
                var firstPlacement = PlacementOf(
                    first,
                    RepeatSampler,
                    -border,
                    -border,
                    region,
                    region,
                    0,
                    0);
                var secondPlacement = PlacementOf(
                    second,
                    RepeatSampler,
                    -border,
                    -border,
                    region,
                    region,
                    region,
                    0);
                var pageWidth = 2 * region;
                var pageHeight = region;
                var mips = FullMipCount(pageWidth, pageHeight);
                var plan = new AtlasPagePlanEntry(
                    pageWidth,
                    pageHeight,
                    first.graphicsFormat,
                    mips,
                    new[] { firstPlacement, secondPlacement });
                var uses = new[]
                {
                    UseOf(first, 0),
                    UseOf(second, 1),
                };

                var prediction = AtlasBudget.PredictPagePeakBytes(
                    plan, uses, p);
                Assert.That(
                    prediction, Is.GreaterThan(0),
                    "fixture precondition: the prediction must be a " +
                    "positive number");

                var ledger = new AtlasByteLedger(prediction);
                var sources = new Dictionary<int, AtlasSourceImage>();
                foreach (var source in new[] { first, second })
                {
                    // The import clone lives only during the
                    // acquisition, so its bytes enter and leave the
                    // ledger around the call.
                    var cloneBytes = AtlasBudget.TextureBytes(
                        source.width,
                        source.height,
                        source.graphicsFormat,
                        source.mipmapCount);
                    ledger.Add(cloneBytes);
                    var ok =
                        UnityStreamingTextureEvidence.TryAcquireAtlasSource(
                            source, p, out var image, out var refusal);
                    Assert.That(
                        ok, Is.True,
                        "fixture precondition: the acquisition must " +
                        "succeed, refusal " + refusal);
                    ledger.Release(cloneBytes);
                    sources[source.GetInstanceID()] = image;
                }

                page = AtlasPageBuilder.Build(
                    plan, RepeatSampler, sources, p, ledger);
                AtlasPageCheck.Verify(page, plan, sources, p, ledger);

                // The page texture and the two source images are the
                // only bytes the ledger still holds. Every temporary
                // went back to zero or the release is missing.
                long expected = AtlasBudget.TextureBytes(
                    pageWidth, pageHeight, plan.Format, mips);
                foreach (var image in sources.Values)
                {
                    expected += image.HeldBytes;
                }
                Assert.That(
                    ledger.Current, Is.EqualTo(expected),
                    "the ledger must hold the page and the source " +
                    "images only");
            }
            finally
            {
                if (page != null)
                {
                    UnityEngine.Object.DestroyImmediate(page);
                }
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(first));
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(second));
            }
        }

        private static CapturedAtlasUse UseOf(
            Texture2D texture,
            int captureOrder)
        {
            return new CapturedAtlasUse(
                0,
                0,
                0,
                "Poiyomi 9.3 release main",
                texture.GetInstanceID(),
                texture.graphicsFormat,
                texture.width,
                texture.height,
                texture.mipmapCount,
                RepeatSampler,
                new AtlasUvRect(0.0, 0.0, 1.0, 1.0),
                "material",
                Array.Empty<string>(),
                captureOrder);
        }
    }
}
