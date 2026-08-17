using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CoreWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Cycle 3-A: the texture-evidence building blocks — asset source identity,
    /// UV/ST mapping, the exact-zero mode gate, and the MainTex-derived sampler.
    /// These drive the low-level extraction helpers directly with real temporary
    /// texture assets and the schema-complete test fixture material. No real
    /// Poiyomi shader is required.
    /// </summary>
    public sealed class PoiyomiTextureEvidenceTests
    {
        private const string ShaderName =
            "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest";
        private const string TempFolder = "Assets/AmuseTests_Temp";

        private readonly List<UnityEngine.Object> _transient =
            new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_Temp");
            }
        }

        [TearDown]
        public void TearDown()
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

        private Material NewFixtureMaterial()
        {
            var shader = Shader.Find(ShaderName);
            Assert.That(
                shader,
                Is.Not.Null,
                $"Test fixture shader '{ShaderName}' must import.");
            var material = new Material(shader);
            _transient.Add(material);
            return material;
        }

        private Texture2D ImportTexture(
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

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            // A supported default for sampler tests: no mipmaps so the base
            // sampler is expressible unless a test opts into an unsupported mode.
            importer.mipmapEnabled = false;
            configure?.Invoke(importer);
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"Imported texture '{path}' must load.");
            return loaded;
        }

        private static string ExpectedToken(Texture texture)
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

        // --- Source identity ------------------------------------------------

        [Test]
        public void SourceId_AssignedAsset_MatchesUnityAssetTokenFormat()
        {
            var texture = ImportTexture("identity_format");

            var ok = PoiyomiMaterialSemantics.TryGetAssignedTextureSourceId(
                texture,
                out var sourceId);

            Assert.That(ok, Is.True);
            Assert.That(sourceId.Value, Is.EqualTo(ExpectedToken(texture)));
            Assert.That(sourceId.Value, Does.StartWith("unity-asset:"));
        }

        [Test]
        public void SourceId_SameAsset_IsStableAcrossCalls()
        {
            var texture = ImportTexture("identity_stable");

            PoiyomiMaterialSemantics.TryGetAssignedTextureSourceId(
                texture, out var first);
            PoiyomiMaterialSemantics.TryGetAssignedTextureSourceId(
                texture, out var second);

            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void SourceId_DistinctAssets_ProduceDistinctIds()
        {
            var a = ImportTexture("identity_a");
            var b = ImportTexture("identity_b");

            PoiyomiMaterialSemantics.TryGetAssignedTextureSourceId(a, out var idA);
            PoiyomiMaterialSemantics.TryGetAssignedTextureSourceId(b, out var idB);

            Assert.That(idA, Is.Not.EqualTo(idB));
        }

        [Test]
        public void SourceId_RenamedAsset_IsUnchanged()
        {
            var texture = ImportTexture("identity_before_rename");
            PoiyomiMaterialSemantics.TryGetAssignedTextureSourceId(
                texture, out var before);

            var moveError = AssetDatabase.MoveAsset(
                TempFolder + "/identity_before_rename.png",
                TempFolder + "/identity_after_rename.png");
            Assert.That(moveError, Is.Empty, "Rename must succeed for this probe.");

            PoiyomiMaterialSemantics.TryGetAssignedTextureSourceId(
                texture, out var after);

            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void SourceId_TransientTexture_IsRefusedWithoutFallback()
        {
            // A runtime texture with no backing asset must not be given an
            // identity from instance id, name, or reference; it is refused.
            var transient = new Texture2D(4, 4);
            _transient.Add(transient);

            var ok = PoiyomiMaterialSemantics.TryGetAssignedTextureSourceId(
                transient,
                out _);

            Assert.That(ok, Is.False);
        }

        // --- UV / ST mapping ------------------------------------------------

        [Test]
        public void Uv_ChannelValues0Through3_MapToChannels(
            [Values(0, 1, 2, 3)] int channel)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_MainTexUV", channel);

            var ok = PoiyomiMaterialSemantics.TryGetSupportedUvMapping(
                material, "_MainTex", "_MainTexUV", "_MainTexPan", out var mapping);

            Assert.That(ok, Is.True);
            Assert.That(mapping.Channel, Is.EqualTo(channel));
        }

        [Test]
        public void Uv_ScaleAndOffset_AreCaptured()
        {
            var material = NewFixtureMaterial();
            material.SetTextureScale("_MainTex", new Vector2(2f, 3f));
            material.SetTextureOffset("_MainTex", new Vector2(0.1f, 0.2f));

            PoiyomiMaterialSemantics.TryGetSupportedUvMapping(
                material, "_MainTex", "_MainTexUV", "_MainTexPan", out var mapping);

            Assert.That(mapping.Scale, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(mapping.Offset, Is.EqualTo(new Vector2(0.1f, 0.2f)));
        }

        [Test]
        public void Uv_UnsupportedChannelValue_IsRefused()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_MainTexUV", 4f);

            var ok = PoiyomiMaterialSemantics.TryGetSupportedUvMapping(
                material, "_MainTex", "_MainTexUV", "_MainTexPan", out _);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void Uv_NonIntegerChannel_IsRefused()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_MainTexUV", 1.5f);

            var ok = PoiyomiMaterialSemantics.TryGetSupportedUvMapping(
                material, "_MainTex", "_MainTexUV", "_MainTexPan", out _);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void Uv_NonFiniteChannel_IsRefusedNotThrown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_MainTexUV", float.NaN);

            var ok = PoiyomiMaterialSemantics.TryGetSupportedUvMapping(
                material, "_MainTex", "_MainTexUV", "_MainTexPan", out _);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void Uv_NonZeroPan_IsRefused()
        {
            var material = NewFixtureMaterial();
            material.SetVector("_MainTexPan", new Vector4(0.1f, 0f, 0f, 0f));

            var ok = PoiyomiMaterialSemantics.TryGetSupportedUvMapping(
                material, "_MainTex", "_MainTexUV", "_MainTexPan", out _);

            Assert.That(ok, Is.False);
        }

        // --- Exact-zero mode gate ------------------------------------------

        [Test]
        public void ModeGate_AllPropertiesZero_IsTrue()
        {
            var material = NewFixtureMaterial();

            var ok = PoiyomiMaterialSemantics.AreExactlyZero(
                material, "_MainTexStochastic", "_MainPixelMode");

            Assert.That(ok, Is.True);
        }

        [Test]
        public void ModeGate_NonZeroStochastic_IsFalse()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_MainTexStochastic", 1f);

            var ok = PoiyomiMaterialSemantics.AreExactlyZero(
                material, "_MainTexStochastic", "_MainPixelMode");

            Assert.That(ok, Is.False);
        }

        [Test]
        public void ModeGate_NonZeroPixelMode_IsFalse()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_MainPixelMode", 1f);

            var ok = PoiyomiMaterialSemantics.AreExactlyZero(
                material, "_MainTexStochastic", "_MainPixelMode");

            Assert.That(ok, Is.False);
        }

        [Test]
        public void ModeGate_MissingProperty_IsFalse()
        {
            var material = NewFixtureMaterial();

            var ok = PoiyomiMaterialSemantics.AreExactlyZero(
                material, "_ThisPropertyDoesNotExist");

            Assert.That(ok, Is.False);
        }

        // --- MainTex sampler ------------------------------------------------

        [Test]
        public void Sampler_BilinearRepeat_IsSupported()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("sampler_bilinear", i =>
            {
                i.filterMode = FilterMode.Bilinear;
                i.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            }));

            var ok = PoiyomiMaterialSemantics.TryGetMainTextureSampling(
                material, out var sampling);

            Assert.That(ok, Is.True);
            Assert.That(sampling.Filter, Is.EqualTo(TextureFilterMode.Bilinear));
            Assert.That(sampling.Wrap, Is.EqualTo(CoreWrapMode.Repeat));
        }

        [Test]
        public void Sampler_PointClamp_IsSupported()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("sampler_point", i =>
            {
                i.filterMode = FilterMode.Point;
                i.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            }));

            var ok = PoiyomiMaterialSemantics.TryGetMainTextureSampling(
                material, out var sampling);

            Assert.That(ok, Is.True);
            Assert.That(sampling.Filter, Is.EqualTo(TextureFilterMode.Point));
            Assert.That(sampling.Wrap, Is.EqualTo(CoreWrapMode.Clamp));
        }

        [Test]
        public void Sampler_Trilinear_IsUnsupported()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("sampler_trilinear", i =>
            {
                i.filterMode = FilterMode.Trilinear;
                i.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            }));

            var ok = PoiyomiMaterialSemantics.TryGetMainTextureSampling(
                material, out _);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void Sampler_MirrorWrap_IsUnsupported()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("sampler_mirror", i =>
            {
                i.filterMode = FilterMode.Bilinear;
                i.wrapMode = UnityEngine.TextureWrapMode.Mirror;
            }));

            var ok = PoiyomiMaterialSemantics.TryGetMainTextureSampling(
                material, out _);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void Sampler_PerAxisWrapMismatch_IsUnsupported()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("sampler_mismatch", i =>
            {
                i.filterMode = FilterMode.Bilinear;
                i.wrapModeU = UnityEngine.TextureWrapMode.Clamp;
                i.wrapModeV = UnityEngine.TextureWrapMode.Repeat;
            }));

            var ok = PoiyomiMaterialSemantics.TryGetMainTextureSampling(
                material, out _);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void Sampler_Mipmapped_IsUnsupported()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("sampler_mip", i =>
            {
                i.filterMode = FilterMode.Bilinear;
                i.wrapMode = UnityEngine.TextureWrapMode.Repeat;
                i.mipmapEnabled = true;
            }));

            var ok = PoiyomiMaterialSemantics.TryGetMainTextureSampling(
                material, out _);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void Sampler_UsesMainTexNotAuxiliaryTexture()
        {
            // The MainTex sampler is authoritative; an auxiliary texture's own
            // unsupported sampler state must not change the result.
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("sampler_main_ok", i =>
            {
                i.filterMode = FilterMode.Bilinear;
                i.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            }));
            material.SetTexture("_BumpMap", ImportTexture("sampler_aux_bad", i =>
            {
                i.filterMode = FilterMode.Trilinear;
                i.wrapMode = UnityEngine.TextureWrapMode.Mirror;
                i.mipmapEnabled = true;
            }));

            var ok = PoiyomiMaterialSemantics.TryGetMainTextureSampling(
                material, out var sampling);

            Assert.That(ok, Is.True);
            Assert.That(sampling.Filter, Is.EqualTo(TextureFilterMode.Bilinear));
            Assert.That(sampling.Wrap, Is.EqualTo(CoreWrapMode.Repeat));
        }

        [Test]
        public void Sampler_MissingMainTexture_IsUnsupported()
        {
            // An assigned auxiliary map cannot borrow a guessed sampler when the
            // MainTex slot is empty.
            var material = NewFixtureMaterial();

            var ok = PoiyomiMaterialSemantics.TryGetMainTextureSampling(
                material, out _);

            Assert.That(ok, Is.False);
        }

        // --- Color import interpretation -----------------------------------

        [Test]
        public void Color_SrgbImport_IsSrgb()
        {
            var texture = ImportTexture("color_srgb", i => i.sRGBTexture = true);

            var ok = PoiyomiMaterialSemantics.TryGetColorInterpretation(
                texture, out var interpretation);

            Assert.That(ok, Is.True);
            Assert.That(
                interpretation,
                Is.EqualTo(TextureColorInterpretation.Srgb));
        }

        [Test]
        public void Color_LinearImport_IsLinear()
        {
            var texture = ImportTexture("color_linear", i => i.sRGBTexture = false);

            var ok = PoiyomiMaterialSemantics.TryGetColorInterpretation(
                texture, out var interpretation);

            Assert.That(ok, Is.True);
            Assert.That(
                interpretation,
                Is.EqualTo(TextureColorInterpretation.Linear));
        }

        [Test]
        public void Color_MissingImporter_IsRefused()
        {
            var transient = new Texture2D(4, 4);
            _transient.Add(transient);

            var ok = PoiyomiMaterialSemantics.TryGetColorInterpretation(
                transient, out _);

            Assert.That(ok, Is.False);
        }

        // --- Sampled-alpha-one proof ---------------------------------------

        [Test]
        public void Alpha_SourceWithoutAlphaImportedAsNone_IsProvenOne()
        {
            var texture = ImportTexture(
                "alpha_none",
                i => i.alphaSource = TextureImporterAlphaSource.None,
                sourceHasAlpha: false);

            var proven =
                PoiyomiMaterialSemantics.TryProveSampledAlphaIsOne(texture);

            Assert.That(proven, Is.True);
        }

        [Test]
        public void Alpha_SourceWithAlpha_IsNotProvenOne()
        {
            var texture = ImportTexture(
                "alpha_present",
                i => i.alphaSource = TextureImporterAlphaSource.FromInput,
                sourceHasAlpha: true);

            var proven =
                PoiyomiMaterialSemantics.TryProveSampledAlphaIsOne(texture);

            Assert.That(proven, Is.False);
        }

        [Test]
        public void Alpha_MissingImporter_IsNotProvenOne()
        {
            var transient = new Texture2D(4, 4);
            _transient.Add(transient);

            var proven =
                PoiyomiMaterialSemantics.TryProveSampledAlphaIsOne(transient);

            Assert.That(proven, Is.False);
        }

        // --- Canonical normal-map import proof -----------------------------

        [Test]
        public void Normal_CanonicalNormalMapImport_IsRecognized()
        {
            var texture = ImportTexture("normal_ok", i =>
            {
                i.textureType = TextureImporterType.NormalMap;
                i.flipGreenChannel = false;
            });

            var ok = PoiyomiMaterialSemantics.IsCanonicalNormalMapImport(texture);

            Assert.That(ok, Is.True);
        }

        [Test]
        public void Normal_DefaultImport_IsNotCanonicalNormalMap()
        {
            var texture = ImportTexture("normal_default", i =>
            {
                i.textureType = TextureImporterType.Default;
            });

            var ok = PoiyomiMaterialSemantics.IsCanonicalNormalMapImport(texture);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void Normal_GreenChannelInverted_IsRejected()
        {
            var texture = ImportTexture("normal_flipped", i =>
            {
                i.textureType = TextureImporterType.NormalMap;
                i.flipGreenChannel = true;
            });

            var ok = PoiyomiMaterialSemantics.IsCanonicalNormalMapImport(texture);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void Normal_MissingImporter_IsNotCanonicalNormalMap()
        {
            var transient = new Texture2D(4, 4);
            _transient.Add(transient);

            var ok = PoiyomiMaterialSemantics.IsCanonicalNormalMapImport(transient);

            Assert.That(ok, Is.False);
        }
    }
}
