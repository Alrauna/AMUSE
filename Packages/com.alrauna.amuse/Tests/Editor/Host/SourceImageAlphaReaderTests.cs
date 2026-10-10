using System;
using System.IO;
using Alrauna.Amuse.Tests.Editor.Shared;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    [TestFixture]
    public sealed class SourceImageAlphaReaderTests
    {
        private const string TestFolder = "Assets/AmuseTests_SourceImageAlphaReader";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(TestFolder));
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.DeleteAsset(TestFolder);
            }
        }

        [Test]
        public void TryReadSourceAlphaChain_DecodesPngWithCutoutThresholdCorrectly()
        {
            var pngPath = Path.Combine(TestFolder, "cutout_test.png");
            // 4x4 image:
            // top half alpha 0.8 (204/255), bottom half alpha 0.2 (51/255)
            var pixels = new Color32[16];
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 4; x++)
                {
                    pixels[y * 4 + x] =
                        new Color32(255, 255, 255, (byte)(y >= 2 ? 204 : 51));
                }
            }
            var imported = TestTextureImport.WritePng(pngPath, 4, 4, pixels);

            // Test with cutoff 0.5f:
            // top half (alpha 0.8f >= 0.5f) should be 255
            // bottom half (alpha 0.2f < 0.5f) should be 0
            // Inert bounds: the 0.5f cutoff keeps the float threshold
            // route this test pins.
            Assert.That(
                SourceImageAlphaReader.TryReadSourceAlphaChain(
                    imported, TextureChannel.Alpha, 0.5f,
                    AlphaPolicyBounds.Inert, out var chain),
                Is.True);
            Assert.That(chain, Is.Not.Null);
            Assert.That(chain.Count, Is.GreaterThan(0));

            var mip0 = chain[0];
            Assert.That(mip0.Width, Is.EqualTo(4));
            Assert.That(mip0.Height, Is.EqualTo(4));

            for (var x = 0; x < 4; x++)
            {
                Assert.That(mip0.GetAlpha(x, 0), Is.EqualTo(0));
                Assert.That(mip0.GetAlpha(x, 1), Is.EqualTo(0));
                Assert.That(mip0.GetAlpha(x, 2), Is.EqualTo(255));
                Assert.That(mip0.GetAlpha(x, 3), Is.EqualTo(255));
            }
        }

        [Test]
        public void TryReadSourceAlphaChain_ExactOneThresholdRequiresFullAlpha()
        {
            var pngPath = Path.Combine(TestFolder, "exact_one_test.png");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[]
            {
                new Color(1f, 1f, 1f, 1.0f),
                new Color(1f, 1f, 1f, 0.99f),
                new Color(1f, 1f, 1f, 0.5f),
                new Color(1f, 1f, 1f, 0.0f)
            });
            var imported = TestTextureImport.WritePng(
                pngPath, 2, 2, tex.GetPixels32());
            UnityEngine.Object.DestroyImmediate(tex);

            // Inert bounds at cutoff 1.0f still take the float
            // threshold route this test pins.
            Assert.That(
                SourceImageAlphaReader.TryReadSourceAlphaChain(
                    imported, TextureChannel.Alpha, 1.0f,
                    AlphaPolicyBounds.Inert, out var chain),
                Is.True);

            var mip0 = chain[0];
            Assert.That(mip0.GetAlpha(0, 0), Is.EqualTo(255));
            Assert.That(mip0.GetAlpha(1, 0), Is.EqualTo(0));
            Assert.That(mip0.GetAlpha(0, 1), Is.EqualTo(0));
            Assert.That(mip0.GetAlpha(1, 1), Is.EqualTo(0));
        }

        [Test]
        public void TryReadSourceAlphaChain_RefusesGrayScaleGeneratedAlphaForTheAlphaChannel()
        {
            // Mid gray RGB with opaque alpha: the import generates an alpha
            // near 0.5 from the luminance, so the file's opaque alpha bytes
            // are not the imported representation.
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(128, 128, 128, 255);
            }
            var imported = TestTextureImport.WritePng(
                Path.Combine(TestFolder, "gray_scale_alpha.png"), 4, 4, pixels,
                importer => importer.alphaSource =
                    TextureImporterAlphaSource.FromGrayScale);

            Assert.That(
                SourceImageAlphaReader.TryReadSourceAlphaChain(
                    imported, TextureChannel.Alpha, 1.0f,
                    AlphaPolicyBounds.Inert, out var chain),
                Is.False,
                "The route cannot reproduce a generated alpha channel.");
            Assert.That(chain, Is.Null);
        }

        [Test]
        public void MaskedRouteCoarseLevelsAverageEveryTexelUnderActivePolicy()
        {
            // 4x4 with mipmaps, one zero texel. Under an active policy with
            // no shader cutoff the reader takes the masked route. Level 1
            // must publish the plain average of the imported chain, so the
            // block over the stray reads 0, not the masked 255.
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            pixels[0] = new Color32(255, 255, 255, 0);
            var pngPath = Path.Combine(TestFolder, "masked_coarse.png");
            var imported = TestTextureImport.WritePng(pngPath, 4, 4, pixels,
                importer => importer.mipmapEnabled = true);

            Assert.That(
                SourceImageAlphaReader.TryReadSourceAlphaChain(
                    imported, TextureChannel.Alpha, 1.0f,
                    AlphaPolicyBounds.From(100, 2), out var chain),
                Is.True);

            var level0 = chain[0];
            Assert.That(level0.GetAlpha(0, 0),
                Is.EqualTo(AlphaTextureData.ErasedFlag));
            var level1 = chain[1];
            Assert.That(level1.Width, Is.EqualTo(2));
            Assert.That(level1.GetAlpha(0, 0), Is.EqualTo(0),
                "The block over the stray averages all four texels.");
            Assert.That(level1.GetAlpha(1, 1), Is.EqualTo(255),
                "The block without the stray stays opaque.");
        }

        [Test]
        public void TryReadSourceAlphaChain_KeepsServingTheRedChannelOfGrayScaleImports()
        {
            // Characterization, not RED: grayscale generation rewrites only
            // the alpha channel, so the red read stays faithful and admitted.
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(128, 128, 128, 255);
            }
            var imported = TestTextureImport.WritePng(
                Path.Combine(TestFolder, "gray_scale_red.png"), 4, 4, pixels,
                importer => importer.alphaSource =
                    TextureImporterAlphaSource.FromGrayScale);

            Assert.That(
                SourceImageAlphaReader.TryReadSourceAlphaChain(
                    imported, TextureChannel.Red, 1.0f,
                    AlphaPolicyBounds.Inert, out var chain),
                Is.True);
            var mip0 = chain[0];
            for (var x = 0; x < 4; x++)
            {
                Assert.That(mip0.GetAlpha(x, 0), Is.EqualTo(0),
                    "Red 128 is below exactly one at inert bounds.");
            }
        }

        [Test]
        public void IsDecodeReadFailure_AcceptsIoAndAccessFailures()
        {
            Assert.That(
                SourceImageAlphaReader.IsDecodeReadFailure(
                    new IOException(" vanished")),
                Is.True);
            Assert.That(
                SourceImageAlphaReader.IsDecodeReadFailure(
                    new FileNotFoundException("missing.png")),
                Is.True);
            Assert.That(
                SourceImageAlphaReader.IsDecodeReadFailure(
                    new UnauthorizedAccessException("denied")),
                Is.True);
        }

        [Test]
        public void IsDecodeReadFailure_RejectsDefectExceptions()
        {
            Assert.That(
                SourceImageAlphaReader.IsDecodeReadFailure(
                    new NullReferenceException()),
                Is.False);
            Assert.That(
                SourceImageAlphaReader.IsDecodeReadFailure(
                    new IndexOutOfRangeException()),
                Is.False);
            Assert.That(
                SourceImageAlphaReader.IsDecodeReadFailure(
                    new InvalidOperationException("defect")),
                Is.False);
        }

        [Test]
        public void TryReadSourceAlphaChain_TruncatedPngRefusesWithoutThrowing()
        {
            // Characterization, not RED: malformed data refuses through the
            // LoadImage false path, with no exception involved, before and
            // after this task. It guards the refusal path against a later
            // regression to a throwing route.
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            var pngPath = Path.Combine(TestFolder, "truncated.png");
            var imported = TestTextureImport.WritePng(pngPath, 4, 4, pixels);
            File.WriteAllBytes(pngPath, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

            Assert.That(
                SourceImageAlphaReader.TryReadSourceAlphaChain(
                    imported, TextureChannel.Alpha, 1.0f,
                    AlphaPolicyBounds.Inert, out var chain),
                Is.False);
            Assert.That(chain, Is.Null);
        }
    }
}
