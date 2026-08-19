using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Shared Editor-test fixture for the verified lilToon interpreter. It
    /// builds a schema-complete stand-in material and disposable texture assets
    /// under one temp folder; no real lilToon package is installed. Equations
    /// are exercised through the verified-material seam, so the stand-in never
    /// needs the pinned digests.
    /// </summary>
    public abstract class LilToonFixtureTestBase
    {
        protected const string FixtureShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonSemanticTest";
        protected const string TempFolder = "Assets/AmuseTests_LilToon";

        /// <summary>Every feature symbol a fully compiled lilToon exposes.</summary>
        protected static readonly string[] AllFeatures =
        {
            "LIL_FEATURE_NORMAL_1ST",
            "LIL_FEATURE_BumpMap",
            "LIL_FEATURE_EMISSION_1ST",
            "LIL_FEATURE_EmissionMap",
        };

        private readonly List<UnityEngine.Object> _transient =
            new List<UnityEngine.Object>();

        [SetUp]
        public void BaseSetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_LilToon");
            }
        }

        [TearDown]
        public void BaseTearDown()
        {
            foreach (var obj in _transient)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            _transient.Clear();

            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        protected T Track<T>(T obj) where T : UnityEngine.Object
        {
            _transient.Add(obj);
            return obj;
        }

        protected Material NewFixtureMaterial()
        {
            var shader = Shader.Find(FixtureShaderName);
            Assert.That(
                shader,
                Is.Not.Null,
                $"Test fixture shader '{FixtureShaderName}' must import.");
            return Track(new Material(shader));
        }

        /// <summary>
        /// Interprets with linear colour space and every feature compiled in,
        /// the configuration under which the traced equations hold.
        /// </summary>
        internal static LilToonSemanticResult Interpret(Material material)
        {
            return LilToonMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear, AllFeatures);
        }

        internal static LilToonSemanticResult Interpret(
            Material material,
            params string[] compiledFeatures)
        {
            return LilToonMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear, compiledFeatures);
        }

        internal static IReadOnlyList<LilToonSemanticDiagnostic> DiagnosticsFor(
            LilToonSemanticResult result,
            LilToonSemanticOutput output)
        {
            return result.Diagnostics.Where(d => d.Output == output).ToList();
        }

        internal static void AssertSingleDiagnostic(
            LilToonSemanticResult result,
            LilToonSemanticOutput output,
            LilToonSemanticDiagnosticCode code,
            string detailContains)
        {
            var scoped = DiagnosticsFor(result, output);
            Assert.That(scoped.Count, Is.EqualTo(1), $"{output} diagnostics");
            Assert.That(scoped[0].Code, Is.EqualTo(code));
            Assert.That(scoped[0].Detail, Does.Contain(detailContains));
        }

        /// <summary>
        /// Writes, imports, and returns a tiny texture asset. The default import
        /// yields a supported sampler unless a test opts out through
        /// <paramref name="configure"/>.
        /// </summary>
        protected Texture2D ImportTexture(
            string name,
            Action<TextureImporter> configure = null,
            bool sourceHasAlpha = true)
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

        protected Texture2D ImportNormalMap(string name)
        {
            return ImportTexture(
                name,
                importer =>
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.flipGreenChannel = false;
                },
                sourceHasAlpha: false);
        }

        protected Texture2D ImportOpaqueColorMap(string name)
        {
            return ImportTexture(
                name,
                importer =>
                {
                    importer.sRGBTexture = true;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                },
                sourceHasAlpha: false);
        }

        /// <summary>
        /// Imports a floating-point HDR texture through a real
        /// <see cref="TextureImporter"/>, so the sampled-range proof is exercised
        /// against an importer-backed asset whose effective GraphicsFormat is
        /// outside the bounded allow-list — not merely against a texture with no
        /// importer at all.
        /// </summary>
        protected Texture2D ImportHdrTexture(string name)
        {
            var path = TempFolder + "/" + name + ".exr";
            var staging = new Texture2D(4, 4, TextureFormat.RGBAFloat, false);
            var pixels = new Color[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                // Deliberately outside [0,1]: this is the range the proof rejects.
                pixels[i] = new Color(4f, 2f, 8f, 1f);
            }

            staging.SetPixels(pixels);
            staging.Apply();
            File.WriteAllBytes(
                path, staging.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            UnityEngine.Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(
                importer,
                Is.Not.Null,
                $"HDR fixture '{path}' must have a TextureImporter; the range " +
                "proof is only meaningful against importer-backed assets.");

            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"Imported texture '{path}' must load.");
            return loaded;
        }

        /// <summary>
        /// Creates a native texture asset: stable identity and a bounded format,
        /// but no <see cref="TextureImporter"/> at all.
        /// </summary>
        protected Texture2D CreateNativeTextureAsset(string name)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.Apply();
            var path = TempFolder + "/" + name + ".asset";
            AssetDatabase.CreateAsset(texture, path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
