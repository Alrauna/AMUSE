using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Shared;
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
        internal const string FixtureShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonSemanticTest";
        protected const string TempFolder = "Assets/AmuseTests_LilToon";

        internal const string CutoutConversionShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest";
        internal const string OpaqueConversionShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonOpaqueConversionTest";
        internal const string TransparentConversionShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonTransparentConversionTest";

        /// <summary>Every feature symbol a fully compiled lilToon exposes.</summary>
        protected static readonly string[] AllFeatures =
        {
            "LIL_FEATURE_NORMAL_1ST",
            "LIL_FEATURE_BumpMap",
            "LIL_FEATURE_EMISSION_1ST",
            "LIL_FEATURE_EmissionMap",
        };

        private readonly TestTransientScope _scope =
            new TestTransientScope(TempFolder);

        private UnityEngine.AnisotropicFiltering _savedAnisotropicFiltering;

        [SetUp]
        public void BaseSetUp()
        {
            // The dev editor project persists Forced On anisotropic filtering.
            // Under Forced On the sampler evidence reads a default anisoLevel 1
            // texture as anisotropic, which the footprint and wrap proofs
            // refuse. Every fixture texture carries a default anisoLevel, so
            // the Per Texture mode is the mode these equations were written
            // under. The pin is per test and restores the editor mode on
            // teardown, which NUnit runs even for a failed test.
            _savedAnisotropicFiltering =
                UnityEngine.QualitySettings.anisotropicFiltering;
            UnityEngine.QualitySettings.anisotropicFiltering =
                UnityEngine.AnisotropicFiltering.Enable;
            _scope.EnsureTempFolder();
        }

        [TearDown]
        public void BaseTearDown()
        {
            _scope.TearDown();
            UnityEngine.QualitySettings.anisotropicFiltering =
                _savedAnisotropicFiltering;
        }

        protected T Track<T>(T obj) where T : UnityEngine.Object
        {
            return _scope.Track(obj);
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

        /// <summary>
        /// Mints one full cutout-schema stand-in under the given temp
        /// folder, at the given shader name and Multi mode state: every
        /// fact the cutout and transparent alpha requests read is declared
        /// at its vendor-off default, beside the three Multi scalars, so
        /// the combined Multi request captures a complete schema and the
        /// resolved family's interpreter answers on real facts. The
        /// keyword set is exactly what the caller passes, so the
        /// mode-consistency gate sees the set the caller intends. The
        /// declared render state is the vendor cutout form at mode one and
        /// the vendor transparent form at mode two; neither the resolver
        /// nor either interpreter reads those facts, so they only keep the
        /// minted state honest. The caller owns the folder lifecycle and
        /// the returned material's destruction.
        /// </summary>
        internal static Material CreateCutoutSchemaStandIn(
            string tempFolder,
            string shaderName,
            float transparentMode,
            string[] keywords)
        {
            if (!AssetDatabase.IsValidFolder(tempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", Path.GetFileName(tempFolder));
            }

            var path = tempFolder + "/" +
                shaderName.Replace('/', '-') + ".shader";
            var shader = TestShaderWriter.WriteTestShader(
                path,
                "Shader \"" + shaderName + "\"\n" +
                "{\n    Properties\n    {\n" +
                "        _TransparentMode (\"TransparentMode\", Float) = " +
                transparentMode + "\n" +
                "        _UseClippingCanceller" +
                " (\"ClippingCanceller\", Float) = 0\n" +
                "        _AsOverlay (\"AsOverlay\", Float) = 0\n" +
                "        [HideInInspector] _lilToonVersion" +
                " (\"Version\", Int) = 45\n" +
                "        _Invisible (\"Invisible\", Int) = 0\n" +
                "        _UDIMDiscardCompile" +
                " (\"UDIMDiscardCompile\", Int) = 0\n" +
                "        _UDIMDiscardMode" +
                " (\"UDIMDiscardMode\", Int) = 0\n" +
                "        _ShiftBackfaceUV" +
                " (\"ShiftBackfaceUV\", Int) = 0\n" +
                "        _UseParallax (\"UseParallax\", Int) = 0\n" +
                "        _UseMain2ndTex (\"UseMain2ndTex\", Int) = 0\n" +
                "        _UseMain3rdTex (\"UseMain3rdTex\", Int) = 0\n" +
                "        _Color2nd (\"Color2nd\", Color) = (1,1,1,1)\n" +
                "        _Color3rd (\"Color3rd\", Color) = (1,1,1,1)\n" +
                "        _Main2ndTex (\"Main2ndTex\", 2D) = \"white\" {}\n" +
                "        _Main3rdTex (\"Main3rdTex\", 2D) = \"white\" {}\n" +
                "        _Main2ndTex_ScrollRotate" +
                " (\"Main2ndScrollRotate\", Vector) = (0,0,0,0)\n" +
                "        _Main3rdTex_ScrollRotate" +
                " (\"Main3rdScrollRotate\", Vector) = (0,0,0,0)\n" +
                "        _Main2ndTexAngle" +
                " (\"Main2ndAngle\", Float) = 0\n" +
                "        _Main3rdTexAngle" +
                " (\"Main3rdAngle\", Float) = 0\n" +
                "        _Main2ndTex_UVMode" +
                " (\"Main2ndUVMode\", Int) = 0\n" +
                "        _Main3rdTex_UVMode" +
                " (\"Main3rdUVMode\", Int) = 0\n" +
                "        _Main2ndTexAlphaMode" +
                " (\"Main2ndAlphaMode\", Int) = 0\n" +
                "        _Main3rdTexAlphaMode" +
                " (\"Main3rdAlphaMode\", Int) = 0\n" +
                "        _Main2ndTex_Cull (\"Main2ndCull\", Int) = 0\n" +
                "        _Main3rdTex_Cull (\"Main3rdCull\", Int) = 0\n" +
                "        _Main2ndTexIsDecal" +
                " (\"Main2ndIsDecal\", Int) = 0\n" +
                "        _Main3rdTexIsDecal" +
                " (\"Main3rdIsDecal\", Int) = 0\n" +
                "        _Main2ndTexIsLeftOnly" +
                " (\"Main2ndIsLeftOnly\", Int) = 0\n" +
                "        _Main3rdTexIsLeftOnly" +
                " (\"Main3rdIsLeftOnly\", Int) = 0\n" +
                "        _Main2ndTexIsRightOnly" +
                " (\"Main2ndIsRightOnly\", Int) = 0\n" +
                "        _Main3rdTexIsRightOnly" +
                " (\"Main3rdIsRightOnly\", Int) = 0\n" +
                "        _Main2ndTexShouldCopy" +
                " (\"Main2ndShouldCopy\", Int) = 0\n" +
                "        _Main3rdTexShouldCopy" +
                " (\"Main3rdShouldCopy\", Int) = 0\n" +
                "        _Main2ndTexShouldFlipMirror" +
                " (\"Main2ndShouldFlipMirror\", Int) = 0\n" +
                "        _Main3rdTexShouldFlipMirror" +
                " (\"Main3rdShouldFlipMirror\", Int) = 0\n" +
                "        _Main2ndTexShouldFlipCopy" +
                " (\"Main2ndShouldFlipCopy\", Int) = 0\n" +
                "        _Main3rdTexShouldFlipCopy" +
                " (\"Main3rdShouldFlipCopy\", Int) = 0\n" +
                "        _Main2ndTexIsMSDF" +
                " (\"Main2ndIsMSDF\", Int) = 0\n" +
                "        _Main3rdTexIsMSDF" +
                " (\"Main3rdIsMSDF\", Int) = 0\n" +
                "        _Main2ndBlendMask" +
                " (\"Main2ndBlendMask\", 2D) = \"white\" {}\n" +
                "        _Main3rdBlendMask" +
                " (\"Main3rdBlendMask\", 2D) = \"white\" {}\n" +
                "        _Main2ndDistanceFade" +
                " (\"Main2ndDistanceFade\", Vector) = (0.1,0.01,0,0)\n" +
                "        _Main3rdDistanceFade" +
                " (\"Main3rdDistanceFade\", Vector) = (0.1,0.01,0,0)\n" +
                "        _Main2ndDissolveParams" +
                " (\"Main2ndDissolveParams\", Vector) = (0,0,0.5,0.1)\n" +
                "        _Main3rdDissolveParams" +
                " (\"Main3rdDissolveParams\", Vector) = (0,0,0.5,0.1)\n" +
                "        _AudioLink2Main2nd" +
                " (\"AudioLink2Main2nd\", Int) = 0\n" +
                "        _AudioLink2Main3rd" +
                " (\"AudioLink2Main3rd\", Int) = 0\n" +
                "        _AlphaMaskMode (\"AlphaMaskMode\", Int) = 0\n" +
                "        _AlphaMaskScale" +
                " (\"AlphaMaskScale\", Float) = 1\n" +
                "        _AlphaMaskValue" +
                " (\"AlphaMaskValue\", Float) = 0\n" +
                "        _AlphaMask (\"AlphaMask\", 2D) = \"white\" {}\n" +
                "        _UseDither (\"UseDither\", Int) = 0\n" +
                "        _IDMask1 (\"IDMask1\", Int) = 0\n" +
                "        _IDMask2 (\"IDMask2\", Int) = 0\n" +
                "        _IDMask3 (\"IDMask3\", Int) = 0\n" +
                "        _IDMask4 (\"IDMask4\", Int) = 0\n" +
                "        _IDMask5 (\"IDMask5\", Int) = 0\n" +
                "        _IDMask6 (\"IDMask6\", Int) = 0\n" +
                "        _IDMask7 (\"IDMask7\", Int) = 0\n" +
                "        _IDMask8 (\"IDMask8\", Int) = 0\n" +
                "        _IDMaskControlsDissolve" +
                " (\"IDMaskControlsDissolve\", Int) = 0\n" +
                "        _Cutoff (\"Cutoff\", Range(0,1)) = 0.5\n" +
                "        _Color (\"Color\", Color) = (1,1,1,1)\n" +
                "        _MainTex (\"Texture\", 2D) = \"white\" {}\n" +
                "        _DissolveParams" +
                " (\"DissolveParams\", Vector) = (0,0,0.5,0.1)\n" +
                "        _MainTex_ScrollRotate" +
                " (\"ScrollRotate\", Vector) = (0,0,0,0)\n" +
                "        _DistanceFade" +
                " (\"DistanceFade\", Vector) = (0.1,0.01,0,0)\n" +
                "        _AlphaBoostFA" +
                " (\"AlphaBoostFA\", Float) = 10\n" +
                "        _SubpassCutoff" +
                " (\"SubpassCutoff\", Range(0,1)) = 0.5\n" +
                "    }\n" +
                "    SubShader\n    {\n" +
                "        Tags { \"RenderType\" = \"TransparentCutout\" " +
                "\"Queue\" = \"" +
                (transparentMode == 2f ? "AlphaTest+10" : "AlphaTest") +
                "\" }\n\n" +
                "        Pass\n        {\n" +
                "            CGPROGRAM\n" +
                "            #pragma vertex vert\n" +
                "            #pragma fragment frag\n" +
                "            #include \"UnityCG.cginc\"\n\n" +
                "            float4 vert(float4 vertex : POSITION) : " +
                "SV_POSITION\n            {\n" +
                "                return UnityObjectToClipPos(vertex);\n" +
                "            }\n\n" +
                "            fixed4 frag() : SV_Target\n            {\n" +
                "                return fixed4(1, 1, 1, 1);\n" +
                "            }\n" +
                "            ENDCG\n" +
                "        }\n" +
                "    }\n}\n");
            var material = new Material(shader);
            material.shaderKeywords = keywords;
            return material;
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
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(128, 64, 32, 200);
            }

            return TestTextureImport.WritePng(
                TempFolder + "/" + name + ".png",
                4,
                4,
                pixels,
                importer =>
                {
                    importer.mipmapEnabled = false;
                    configure?.Invoke(importer);
                },
                alpha: sourceHasAlpha);
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

            return TestTextureImport.WritePng(
                TempFolder + "/" + name + ".png",
                width,
                height,
                baseLevelBottomToTop,
                importer =>
                {
                    importer.mipmapEnabled = true;
                    importer.filterMode = filterMode;
                    importer.wrapMode = wrapMode;
                    importer.streamingMipmaps = false;
                    // Uncompressed keeps the imported GPU format RGBA32: the
                    // alpha-evidence format allowlist admits RGBA32 exactly,
                    // while platform compression would collapse an
                    // all-opaque source to DXT1, which has no alpha channel
                    // to prove and refuses.
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    configure?.Invoke(importer);
                });
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
            return TestTextureImport.ImportNative(
                TempFolder + "/" + name + ".asset");
        }
    }
}
