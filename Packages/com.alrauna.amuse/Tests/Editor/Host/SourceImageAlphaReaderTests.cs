using System.IO;
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
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            // 4x4 image:
            // top half alpha 0.8 (204/255), bottom half alpha 0.2 (51/255)
            var colors = new Color[16];
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 4; x++)
                {
                    colors[y * 4 + x] = new Color(1f, 1f, 1f, y >= 2 ? 0.8f : 0.2f);
                }
            }
            tex.SetPixels(colors);
            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(pngPath, bytes);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            UnityEngine.Object.DestroyImmediate(tex);

            // Test with cutoff 0.5f:
            // top half (alpha 0.8f >= 0.5f) should be 255
            // bottom half (alpha 0.2f < 0.5f) should be 0
            Assert.That(
                SourceImageAlphaReader.TryReadSourceAlphaChain(
                    imported, TextureChannel.Alpha, 0.5f, out var chain),
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
            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(pngPath, bytes);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            UnityEngine.Object.DestroyImmediate(tex);

            Assert.That(
                SourceImageAlphaReader.TryReadSourceAlphaChain(
                    imported, TextureChannel.Alpha, 1.0f, out var chain),
                Is.True);

            var mip0 = chain[0];
            Assert.That(mip0.GetAlpha(0, 0), Is.EqualTo(255));
            Assert.That(mip0.GetAlpha(1, 0), Is.EqualTo(0));
            Assert.That(mip0.GetAlpha(0, 1), Is.EqualTo(0));
            Assert.That(mip0.GetAlpha(1, 1), Is.EqualTo(0));
        }
    }
}
