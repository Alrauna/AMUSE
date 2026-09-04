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

        protected const string CutoutConversionShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest";
        protected const string OpaqueConversionShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonOpaqueConversionTest";
        protected const string TransparentConversionShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonTransparentConversionTest";

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
            return Track(CreateVerifiedMaterial());
        }

        protected Material NewCutoutFixtureMaterial()
        {
            return Track(CreateCutoutConversionMaterial());
        }

        protected Material NewTransparentFixtureMaterial()
        {
            return Track(CreateTransparentConversionMaterial());
        }

        protected Material NewOpaqueConversionMaterial()
        {
            return Track(CreateOpaqueConversionMaterial());
        }

        internal static Material CreateVerifiedMaterial()
        {
            return CreateFixtureMaterial(FixtureShaderName);
        }

        /// <summary>
        /// Creates a cutout-stand-in material for the cutout-to-opaque
        /// conversion tests by shader name, without subclassing this base.
        /// The caller owns destruction.
        /// </summary>
        internal static Material CreateCutoutConversionMaterial()
        {
            return CreateFixtureMaterial(CutoutConversionShaderName);
        }

        /// <summary>
        /// Creates a transparent-stand-in material for the
        /// transparent-to-opaque conversion tests by shader name, without
        /// subclassing this base. The caller owns destruction.
        /// </summary>
        internal static Material CreateTransparentConversionMaterial()
        {
            return CreateFixtureMaterial(TransparentConversionShaderName);
        }

        /// <summary>
        /// Creates the opaque-target stand-in carrying the canonical
        /// conversion tuple by shader name, without subclassing this base.
        /// The caller owns destruction.
        /// </summary>
        internal static Material CreateOpaqueConversionMaterial()
        {
            return CreateFixtureMaterial(OpaqueConversionShaderName);
        }

        private static Material CreateFixtureMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.That(
                shader,
                Is.Not.Null,
                $"Test fixture shader '{shaderName}' must import.");
            return new Material(shader);
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

        /// <summary>
        /// Writes an explicit RGBA32 pixel grid as mip 0 and imports it as a
        /// mipmap-enabled asset; the lower levels are the importer's own
        /// downsample of the supplied base level, so the loaded texture's mip
        /// count follows from the grid size. The import is pinned to the
        /// sampler vocabulary the alpha evidence admits: Point/Bilinear
        /// filter and Clamp/Repeat wrap, no mip bias, no streaming. The
        /// default sampler is Bilinear over Repeat unless a test configures
        /// otherwise.
        /// </summary>
        protected Texture2D ImportMipmapTexture(
            string name,
            int width,
            int height,
            Color32[] baseLevelBottomToTop,
            FilterMode filterMode = FilterMode.Bilinear,
            UnityEngine.TextureWrapMode wrapMode =
                UnityEngine.TextureWrapMode.Repeat,
            Action<TextureImporter> configure = null)
        {
            if (baseLevelBottomToTop.Length != width * height)
            {
                throw new ArgumentException(
                    "Pixel grid length must equal width times height.",
                    nameof(baseLevelBottomToTop));
            }

            var path = TempFolder + "/" + name + ".png";
            var staging = new Texture2D(width, height, TextureFormat.RGBA32, false);
            staging.SetPixels32(baseLevelBottomToTop);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true;
            importer.filterMode = filterMode;
            importer.wrapMode = wrapMode;
            importer.streamingMipmaps = false;
            // Uncompressed keeps the imported GPU format RGBA32: the
            // alpha-evidence format allowlist admits RGBA32 exactly, while
            // platform compression would collapse an all-opaque source to
            // DXT1, which has no alpha channel to prove and refuses.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            configure?.Invoke(importer);
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"Imported texture '{path}' must load.");
            return loaded;
        }

        /// <summary>The mip count the importer actually produced.</summary>
        protected static int MipCount(Texture2D texture)
        {
            return texture.mipmapCount;
        }

        /// <summary>
        /// Reads one mip level of a loaded texture as RGBA32 texels,
        /// bottom-to-top, for chain-shape assertions.
        /// </summary>
        protected static Color32[] ReadMipLevel(Texture2D texture, int mipLevel)
        {
            return texture.GetPixels32(mipLevel);
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
