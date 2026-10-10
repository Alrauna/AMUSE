using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Shared Editor-test fixture for the verified Poiyomi interpreter. It builds
    /// a schema-complete stand-in shader material and disposable texture assets
    /// under one temp folder; no real Poiyomi shader is installed. The
    /// interpreter's equation outputs are exercised through the verified-material
    /// seam, so the stand-in never needs the pinned source hash.
    /// </summary>
    public abstract class PoiyomiFixtureTestBase
    {
        internal const string FixtureShaderName =
            "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest";
        internal const string TwoPassFixtureShaderName =
            "Hidden/Alrauna/AmuseTests/PoiyomiTwoPassSemanticTest";
        protected const string TempFolder = "Assets/AmuseTests_Temp";

        private readonly TestTransientScope _scope =
            new TestTransientScope(TempFolder);

        private UnityEngine.AnisotropicFiltering _savedAnisotropicFiltering;

        [SetUp]
        public void BaseSetUp()
        {
            // The dev editor project persists Forced On anisotropic filtering.
            // Under Forced On the sampler evidence reads a default anisoLevel 1
            // texture as anisotropic, which the texture proofs refuse. Every
            // fixture texture carries a default anisoLevel, so the Per Texture
            // mode is the mode these equations were written under. The pin is
            // per test and restores the editor mode on teardown, which NUnit
            // runs even for a failed test.
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

        /// <summary>Registers a transient object for teardown destruction.</summary>
        protected T Track<T>(T obj) where T : UnityEngine.Object
        {
            return _scope.Track(obj);
        }

        protected Material NewFixtureMaterial()
        {
            return Track(CreateVerifiedMaterial());
        }

        /// <summary>
        /// Interprets through the verified-material seam, linear colour space
        /// by default.
        /// </summary>
        internal static PoiyomiSemanticResult Interpret(
            Material material,
            ColorSpace colorSpace = ColorSpace.Linear)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, colorSpace);
        }

        /// <summary>
        /// Interprets the Two Pass stand-in through its verified-material
        /// seam, linear colour space by default.
        /// </summary>
        internal static PoiyomiSemanticResult InterpretTwoPass(
            Material material,
            ColorSpace colorSpace = ColorSpace.Linear)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedTwoPassMaterial(
                material, colorSpace);
        }

        internal static Material CreateVerifiedMaterial()
        {
            var shader = Shader.Find(FixtureShaderName);
            Assert.That(
                shader,
                Is.Not.Null,
                $"Test fixture shader '{FixtureShaderName}' must import.");
            return new Material(shader);
        }

        /// <summary>
        /// Writes the pinned Poiyomi 9.3.64 Fade preset's render state onto a
        /// stand-in material: mode two, the SrcAlpha/OneMinusSrcAlpha blend
        /// pair, z-write off, the Transparent queue and RenderType. The plain
        /// stand-in's shader defaults (mode zero, One/Zero, z-write on, queue
        /// 2000, RenderType Opaque) already carry every essential opacity fact
        /// conversion checks, so a default stand-in classifies AlreadyOpaque
        /// and creates no clone. Fixtures whose test exercises the conversion
        /// path must call this first. The alpha equation reads none of these
        /// facts, so a fixture's alpha proof is unchanged.
        /// </summary>
        internal static void ApplyConvertibleRenderState(Material material)
        {
            material.SetFloat("_Mode", 2f);
            material.SetFloat("_SrcBlend", 5f);
            material.SetFloat("_DstBlend", 10f);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = 3000;
            material.SetOverrideTag("RenderType", "Transparent");
        }

        protected Material NewTwoPassFixtureMaterial()
        {
            return Track(CreateTwoPassVerifiedMaterial());
        }

        /// <summary>
        /// A material of the Two Pass stand-in shader: the plain fixture's
        /// property set plus the second family's tint and force-opaque flag.
        /// </summary>
        internal static Material CreateTwoPassVerifiedMaterial()
        {
            var shader = Shader.Find(TwoPassFixtureShaderName);
            Assert.That(
                shader,
                Is.Not.Null,
                $"Test fixture shader '{TwoPassFixtureShaderName}' must import.");
            return new Material(shader);
        }

        /// <summary>
        /// Writes, imports, and returns a tiny texture asset. The default import
        /// (no mipmaps, bilinear/repeat, sRGB) yields a supported MainTex sampler
        /// unless a test opts into an unsupported state through <paramref
        /// name="configure"/>.
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
        /// Creates a native (non-imported) texture asset: it has a stable
        /// GUID/local id and a supported default sampler, but its asset importer
        /// is not a <see cref="TextureImporter"/>, so color/alpha/normal import
        /// evidence is unavailable. Isolates the import-evidence failure path.
        /// </summary>
        protected Texture2D NewNativeTextureAsset(string name)
        {
            return TestTextureImport.ImportNative(
                TempFolder + "/" + name + ".asset");
        }

        protected static string ExpectedToken(Texture texture)
        {
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    texture,
                    out var guid,
                    out long localId),
                Is.True,
                "Test asset must have a stable GUID/local id.");
            return "unity-asset:" + guid.ToLowerInvariant() + ":" +
                   localId.ToString(CultureInfo.InvariantCulture);
        }

        // --- Diagnostic assertions -----------------------------------------

        /// <summary>
        /// Asserts the material stayed supported (identity held) but the named
        /// output is unknown with exactly the expected primary diagnostic. An
        /// optional detail pins the offending property/evidence string.
        /// </summary>
        internal static void AssertUnsupportedOutput(
            PoiyomiSemanticResult result,
            PoiyomiSemanticOutput output,
            PoiyomiSemanticDiagnosticCode code,
            string detail = null)
        {
            Assert.That(
                result.IsSupportedMaterial,
                Is.True,
                "A schema-complete verified material stays supported; only its "
                    + "outputs become unknown.");
            Assert.That(
                IsComplete(result, output),
                Is.False,
                $"{output} must be unknown.");

            var match = result.Diagnostics.FirstOrDefault(
                d => d.Output == output && d.Code == code);
            Assert.That(
                match,
                Is.Not.Null,
                $"Expected a {output}/{code} diagnostic. Diagnostics: "
                    + Describe(result));
            if (detail != null)
            {
                Assert.That(match.Detail, Is.EqualTo(detail));
            }
        }

        /// <summary>
        /// Asserts the named output is complete and carries no diagnostic of its
        /// own — the output-local "this role is proven" state.
        /// </summary>
        internal static void AssertOutputComplete(
            PoiyomiSemanticResult result,
            PoiyomiSemanticOutput output)
        {
            Assert.That(
                IsComplete(result, output),
                Is.True,
                $"{output} must be complete. Diagnostics: " + Describe(result));
            Assert.That(
                result.Diagnostics.Any(d => d.Output == output),
                Is.False,
                $"A complete {output} must not also emit a diagnostic.");
        }

        internal static bool IsComplete(
            PoiyomiSemanticResult result,
            PoiyomiSemanticOutput output)
        {
            switch (output)
            {
                case PoiyomiSemanticOutput.BaseColor:
                    return result.Semantics.BaseColor.IsComplete;
                case PoiyomiSemanticOutput.Alpha:
                    return result.Semantics.Alpha.IsComplete;
                case PoiyomiSemanticOutput.Emission:
                    return result.Semantics.Emission.IsComplete;
                case PoiyomiSemanticOutput.Normal:
                    return result.Semantics.Normal.IsComplete;
                default:
                    return false;
            }
        }

        private static string Describe(PoiyomiSemanticResult result)
        {
            if (result.Diagnostics.Count == 0)
            {
                return "(none)";
            }

            var builder = new StringBuilder();
            foreach (var d in result.Diagnostics)
            {
                builder.Append($"[{d.Output}/{d.Code}:{d.Detail}] ");
            }

            return builder.ToString();
        }
    }
}
