using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
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
        protected const string FixtureShaderName =
            "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest";
        protected const string TempFolder = "Assets/AmuseTests_Temp";

        private readonly List<UnityEngine.Object> _transient =
            new List<UnityEngine.Object>();

        [SetUp]
        public void BaseSetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_Temp");
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

        /// <summary>Registers a transient object for teardown destruction.</summary>
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
            var material = new Material(shader);
            _transient.Add(material);
            return material;
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
            var path = TempFolder + "/" + name + ".png";
            var format = sourceHasAlpha
                ? TextureFormat.RGBA32
                : TextureFormat.RGB24;
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

            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            configure?.Invoke(importer);
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"Imported texture '{path}' must load.");
            return loaded;
        }

        /// <summary>
        /// Creates a native (non-imported) texture asset: it has a stable
        /// GUID/local id and a supported default sampler, but its asset importer
        /// is not a <see cref="TextureImporter"/>, so color/alpha/normal import
        /// evidence is unavailable. Isolates the import-evidence failure path.
        /// </summary>
        protected Texture2D NewNativeTextureAsset(string name)
        {
            var path = TempFolder + "/" + name + ".asset";
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport);
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"Native asset '{path}' must load.");
            return loaded;
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
