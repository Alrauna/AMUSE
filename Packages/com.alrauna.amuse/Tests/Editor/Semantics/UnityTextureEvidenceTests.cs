using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// Direct coverage of the five shader-independent Unity texture facts that
    /// both the Poiyomi and lilToon frontends consume. Each fact is a refusal
    /// predicate: unprovable import state must fail, never pass.
    /// </summary>
    public sealed class UnityTextureEvidenceTests
    {
        private const string TempFolder = "Assets/AmuseTests_TexEvidence";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_TexEvidence");
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

        private static Texture2D Import(
            string name,
            bool sourceHasAlpha,
            Action<TextureImporter> configure = null)
        {
            var path = TempFolder + "/" + name + ".png";
            var format = sourceHasAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24;
            var staging = new Texture2D(4, 4, format, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(128, 64, 32, 200);
            }

            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            configure?.Invoke(importer);
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"Imported texture '{path}' must load.");
            return loaded;
        }

        // --- TryGetSourceId ---

        [Test]
        public void TryGetSourceId_ImportedTexture_ReturnsUnityAssetIdentity()
        {
            var texture = Import("identity", sourceHasAlpha: true);

            Assert.That(
                UnityTextureEvidence.TryGetSourceId(texture, out var sourceId),
                Is.True);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                texture, out var guid, out long localId);
            Assert.That(
                sourceId,
                Is.EqualTo(new TextureSourceId(
                    "unity-asset:" + guid.ToLowerInvariant() + ":" + localId)));
        }

        [Test]
        public void TryGetSourceId_SceneOnlyTexture_IsRefused()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(
                    UnityTextureEvidence.TryGetSourceId(texture, out _),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void TryGetSourceId_Null_IsRefused()
        {
            Assert.That(UnityTextureEvidence.TryGetSourceId(null, out _), Is.False);
        }

        // --- TryGetSampling ---

        [Test]
        public void TryGetSampling_DefaultImport_IsBilinearRepeat()
        {
            var texture = Import("sampler", sourceHasAlpha: true);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = UnityEngine.TextureWrapMode.Repeat;

            Assert.That(
                UnityTextureEvidence.TryGetSampling(texture, out var sampling),
                Is.True);
            Assert.That(
                sampling,
                Is.EqualTo(new TextureSampling(
                    TextureFilterMode.Bilinear,
                    Alrauna.Amuse.Editor.Semantics.TextureWrapMode.Repeat)));
        }

        [Test]
        public void TryGetSampling_MipmappedTexture_IsRefused()
        {
            var texture = Import(
                "mipped",
                sourceHasAlpha: true,
                importer => importer.mipmapEnabled = true);

            Assert.That(UnityTextureEvidence.TryGetSampling(texture, out _), Is.False);
        }

        [Test]
        public void TryGetSampling_TrilinearFilter_IsRefused()
        {
            var texture = Import("trilinear", sourceHasAlpha: true);
            texture.filterMode = FilterMode.Trilinear;

            Assert.That(UnityTextureEvidence.TryGetSampling(texture, out _), Is.False);
        }

        [Test]
        public void TryGetSampling_MismatchedWrap_IsRefused()
        {
            var texture = Import("wrapmix", sourceHasAlpha: true);
            texture.wrapModeU = UnityEngine.TextureWrapMode.Clamp;
            texture.wrapModeV = UnityEngine.TextureWrapMode.Repeat;

            Assert.That(UnityTextureEvidence.TryGetSampling(texture, out _), Is.False);
        }

        [Test]
        public void TryGetSampling_Null_IsRefused()
        {
            Assert.That(UnityTextureEvidence.TryGetSampling(null, out _), Is.False);
        }

        // --- TryGetColorInterpretation ---

        [Test]
        public void TryGetColorInterpretation_SrgbImport_IsSrgb()
        {
            var texture = Import(
                "srgb",
                sourceHasAlpha: true,
                importer => importer.sRGBTexture = true);

            Assert.That(
                UnityTextureEvidence.TryGetColorInterpretation(texture, out var value),
                Is.True);
            Assert.That(value, Is.EqualTo(TextureColorInterpretation.Srgb));
        }

        [Test]
        public void TryGetColorInterpretation_LinearImport_IsLinear()
        {
            var texture = Import(
                "linear",
                sourceHasAlpha: true,
                importer => importer.sRGBTexture = false);

            Assert.That(
                UnityTextureEvidence.TryGetColorInterpretation(texture, out var value),
                Is.True);
            Assert.That(value, Is.EqualTo(TextureColorInterpretation.Linear));
        }

        [Test]
        public void TryGetColorInterpretation_NoImporter_IsRefused()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(
                    UnityTextureEvidence.TryGetColorInterpretation(texture, out _),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // --- TryProveSampledAlphaIsOne ---

        [Test]
        public void TryProveSampledAlphaIsOne_SourceWithoutAlpha_IsProven()
        {
            var texture = Import(
                "noalpha",
                sourceHasAlpha: false,
                importer => importer.alphaSource = TextureImporterAlphaSource.None);

            Assert.That(UnityTextureEvidence.TryProveSampledAlphaIsOne(texture), Is.True);
        }

        [Test]
        public void TryProveSampledAlphaIsOne_SourceWithAlpha_IsNotProven()
        {
            var texture = Import(
                "hasalpha",
                sourceHasAlpha: true,
                importer => importer.alphaSource = TextureImporterAlphaSource.FromInput);

            Assert.That(UnityTextureEvidence.TryProveSampledAlphaIsOne(texture), Is.False);
        }

        // --- IsCanonicalNormalMapImport ---

        [Test]
        public void IsCanonicalNormalMapImport_NormalMapWithoutFlip_IsCanonical()
        {
            var texture = Import(
                "normal",
                sourceHasAlpha: false,
                importer =>
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.flipGreenChannel = false;
                });

            Assert.That(UnityTextureEvidence.IsCanonicalNormalMapImport(texture), Is.True);
        }

        [Test]
        public void IsCanonicalNormalMapImport_FlippedGreen_IsNotCanonical()
        {
            var texture = Import(
                "normalflip",
                sourceHasAlpha: false,
                importer =>
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.flipGreenChannel = true;
                });

            Assert.That(UnityTextureEvidence.IsCanonicalNormalMapImport(texture), Is.False);
        }

        [Test]
        public void IsCanonicalNormalMapImport_DefaultTextureType_IsNotCanonical()
        {
            var texture = Import("notanormal", sourceHasAlpha: false);

            Assert.That(UnityTextureEvidence.IsCanonicalNormalMapImport(texture), Is.False);
        }

        // --- shared-class boundary guard ---

        [Test]
        public void SharedClass_ExposesExactlyFiveSemanticFacts()
        {
            var methods = typeof(UnityTextureEvidence).GetMethods(
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.Public |
                BindingFlags.DeclaredOnly);

            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var method in methods)
            {
                if (!method.IsPrivate)
                {
                    names.Add(method.Name);
                }
            }

            Assert.That(
                names,
                Is.EquivalentTo(new[]
                {
                    "TryGetSourceId",
                    "TryGetSampling",
                    "TryGetColorInterpretation",
                    "TryProveSampledAlphaIsOne",
                    "IsCanonicalNormalMapImport",
                }));
        }
    }
}
