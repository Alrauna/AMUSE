using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Alrauna.Amuse.Editor.Semantics;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Tests.Editor.Shared;

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
        private const string TestContainerPath = "Assets/AmuseTests_PersistedIdentity.asset";

        private ScriptableObject _container;
        private ObjectRegistryGuard registryGuard;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_TexEvidence");
            }

            _container = ScriptableObject.CreateInstance<nadena.dev.ndmf.runtime.SubAssetContainer>();
            AssetDatabase.CreateAsset(_container, TestContainerPath);

            // Shared fixture seam: admission requires an injected admitted
            // version and an isolated registry, so every test starts from the
            // same admitted-session shape and nothing leaks between tests.
            ReplacementTextureAttestation.ResetForTests();
            registryGuard = new ObjectRegistryGuard();
            ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "0.9.0";
        }

        [TearDown]
        public void TearDown()
        {
            ReplacementTextureAttestation.ResetForTests();
            registryGuard.Dispose();
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(TestContainerPath) != null)
            {
                AssetDatabase.DeleteAsset(TestContainerPath);
            }

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

        private static Material ImportFixtureMaterialAsset(string marker)
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", Path.GetFileName(TempFolder));
            }
            var material = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest"));
            AssetDatabase.CreateAsset(
                material, $"{TempFolder}/{marker}.mat");
            return material;
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
        public void AssetBackedTextureNeverCarriesTheReplacementForm()
        {
            // --- Falsifier 7, first half: the shipped forms never cross. ---
            var texture = Import("forms", sourceHasAlpha: true);
            try
            {
                Assert.That(
                    UnityTextureEvidence.TryGetSourceId(texture, out var id),
                    Is.True);
                Assert.That(
                    id.Value.StartsWith("unity-asset:", StringComparison.Ordinal),
                    Is.True);
            }
            finally
            {
                // Existing fixture teardown handles the imported asset.
            }
        }

        [Test]
        public void AdmittedCopyCarriesTheReplacementFormThroughTryGetSourceId()
        {
            var registryGuard = new ObjectRegistryGuard();
            var previousVersion =
                ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull;
            var source = Import("replacement-source", sourceHasAlpha: true);
            var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copy.name = source.name + "_compressed";
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copy);
                ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
                ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "0.9.0";
                Assert.That(
                    UnityTextureEvidence.TryGetSourceId(copy, out var id),
                    Is.True);
                Assert.That(
                    id.Value.StartsWith(
                        "unity-replacement:", StringComparison.Ordinal),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
                ReplacementTextureAttestation.ResetForTests();
                ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                    previousVersion;
                registryGuard.Dispose();
            }
        }

        [Test]
        public void AdmittedAtlasWithCorroboratedOrigin_MintsTheAtlasIdentity()
        {
            var registryGuard = new ObjectRegistryGuard();
            var source = ImportFixtureMaterialAsset("atlas-origin");
            var clone = new Material(source.shader)
            {
                name = source.name + " build copy",
            };
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, clone);
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                var resolved = UnityTextureEvidence.TryGetSourceId(
                    atlas, out var id, originMaterial: clone);

                Assert.That(resolved, Is.True);
                Assert.That(
                    id.Value,
                    Is.EqualTo(
                        "unity-aao-atlas:" + atlas.GetInstanceID().ToString(
                            System.Globalization.CultureInfo.InvariantCulture)),
                    "an admitted atlas mints the per-build atlas identity");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(atlas);
                UnityEngine.Object.DestroyImmediate(clone);
                AaoAtlasTextureAttestation.ResetForTests();
                registryGuard.Dispose();
            }
        }

        [Test]
        public void AdmittedAtlasWithoutOriginMaterial_IsRefused()
        {
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            try
            {
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                Assert.That(
                    UnityTextureEvidence.TryGetSourceId(
                        atlas, out _, originMaterial: null),
                    Is.False,
                    "the shape alone never mints an identity");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(atlas);
                AaoAtlasTextureAttestation.ResetForTests();
            }
        }

        [Test]
        public void AdmittedAtlasWithUnregisteredOrigin_IsRefused()
        {
            var clone = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest"));
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            try
            {
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                Assert.That(
                    UnityTextureEvidence.TryGetSourceId(
                        atlas, out _, originMaterial: clone),
                    Is.False,
                    "an unregistered slot material corroborates nothing");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(atlas);
                UnityEngine.Object.DestroyImmediate(clone);
                AaoAtlasTextureAttestation.ResetForTests();
            }
        }

        [Test]
        public void TryGetSourceId_Null_IsRefused()
        {
            Assert.That(UnityTextureEvidence.TryGetSourceId(null, out _), Is.False);
        }

        [Test]
        public void TryGetSourceId_UncharacterizedSubAsset_IsRefused()
        {
            var containerPath = TempFolder + "/SubAssetContainer.asset";
            var container = ScriptableObject.CreateInstance<nadena.dev.ndmf.runtime.SubAssetContainer>();
            AssetDatabase.CreateAsset(container, containerPath);

            var uncharacterized = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            uncharacterized.name = "UncharacterizedSubTexture";
            AssetDatabase.AddObjectToAsset(uncharacterized, containerPath);
            AssetDatabase.SaveAssets();

            Assert.That(AssetDatabase.IsSubAsset(uncharacterized), Is.True);
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(uncharacterized, out _),
                Is.False);
        }

        [Test]
        public void TryGetSourceId_CharacterizedSubAsset_Succeeds()
        {
            var containerPath = TempFolder + "/SubAssetContainer2.asset";
            var container = ScriptableObject.CreateInstance<nadena.dev.ndmf.runtime.SubAssetContainer>();
            AssetDatabase.CreateAsset(container, containerPath);

            var characterized = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            characterized.name = "MainTex (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(characterized, containerPath);
            AssetDatabase.SaveAssets();

            Assert.That(AssetDatabase.IsSubAsset(characterized), Is.True);
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(characterized, out var sourceId),
                Is.True);
            Assert.That(sourceId.Value, Does.StartWith("unity-asset:"));
        }

        [Test]
        public void PersistedCompressedCopyResolvesOrdinaryAssetIdentityForm()
        {
            ReplacementTextureAttestation.ResetForTests();
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "0.9.0";
            var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            copy.name = "MainTex_compressed";
            AssetDatabase.AddObjectToAsset(copy, TestContainerPath);
            AssetDatabase.SaveAssets();

            var resolved = UnityTextureEvidence.TryGetSourceId(copy, out var sourceId);

            Assert.That(resolved, Is.True);
            Assert.That(sourceId.Value, Does.StartWith("unity-asset:"));
            Assert.That(sourceId.Value.Contains("unity-replacement"), Is.False);
        }

        [Test]
        public void TryGetSourceId_VrcFuryBuildContainerSubAsset_Succeeds()
        {
            // The play-mode build path of the VRCFury host clones every
            // avatar texture into one persisted binary container before any
            // NDMF plugin runs. The clones keep their original names, so a
            // name marker cannot characterize them. The characterization
            // pins the container object instead: its full type name and its
            // object name, exactly as VRCFury writes them.
            var containerPath = TempFolder + "/VRCFury Other.asset";
            var container = ScriptableObject.CreateInstance<VF.Utils.BinaryContainer>();
            AssetDatabase.CreateAsset(container, containerPath);

            var mask = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            mask.name = "SomeAvatar_Mask_1";
            AssetDatabase.AddObjectToAsset(mask, containerPath);
            AssetDatabase.SaveAssets();

            Assert.That(AssetDatabase.IsSubAsset(mask), Is.True);
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(mask, out var sourceId),
                Is.True);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                mask, out var guid, out long localId);
            Assert.That(
                sourceId,
                Is.EqualTo(new TextureSourceId(
                    "unity-asset:" + guid.ToLowerInvariant() + ":" + localId)));
        }

        [Test]
        public void TryGetColorInterpretation_VrcFurySubAsset_ReadsGraphicsFormat()
        {
            var containerPath = TempFolder + "/VrcFuryColorContainer.asset";
            var container = ScriptableObject.CreateInstance<VF.Utils.BinaryContainer>();
            container.name = "VRCFury Other";
            AssetDatabase.CreateAsset(container, containerPath);
            container.name = "VRCFury Other";

            var mask = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            mask.name = "SomeAvatar_Mask_2";
            AssetDatabase.AddObjectToAsset(mask, containerPath);
            AssetDatabase.SaveAssets();

            Assert.That(
                UnityTextureEvidence.TryGetColorInterpretation(
                    mask, out var interpretation),
                Is.True);
            Assert.That(
                interpretation,
                Is.EqualTo(TextureColorInterpretation.Srgb));
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

        /// <summary>
        /// The blanket mipmap refusal is gone: AlphaResolution now classifies every
        /// level of the captured chain, so "some level, bilinear within it" is
        /// exactly the model the conjunction covers. Unity's Bilinear filters within
        /// the selected level and selects a level without blending.
        /// </summary>
        [Test]
        public void TryGetSampling_MipmappedTexture_IsAdmitted()
        {
            var texture = Import(
                "mipped",
                sourceHasAlpha: true,
                importer => importer.mipmapEnabled = true);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = UnityEngine.TextureWrapMode.Repeat;

            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(
                UnityTextureEvidence.TryGetSampling(texture, out var sampling), Is.True);
            Assert.That(
                sampling,
                Is.EqualTo(new TextureSampling(
                    TextureFilterMode.Bilinear,
                    Alrauna.Amuse.Editor.Semantics.TextureWrapMode.Repeat)));
        }

        /// <summary>
        /// A7 widening. A nonzero mip bias only shifts which chain level the
        /// hardware selects, and the resolution classifies every level, so the
        /// bias changes which proofs run, never whether the conjunction holds.
        /// </summary>
        [Test]
        public void TryGetSampling_MipmappedWithBias_IsAdmitted()
        {
            var negative = Import(
                "mipped_biased_negative", sourceHasAlpha: true,
                importer => importer.mipmapEnabled = true);
            negative.mipMapBias = -1f;
            Assert.That(
                UnityTextureEvidence.TryGetSampling(negative, out var first),
                Is.True);
            Assert.That(
                first,
                Is.EqualTo(new TextureSampling(
                    TextureFilterMode.Bilinear,
                    Alrauna.Amuse.Editor.Semantics.TextureWrapMode.Repeat)));

            var positive = Import(
                "mipped_biased_positive", sourceHasAlpha: true,
                importer => importer.mipmapEnabled = true);
            positive.mipMapBias = 2f;
            Assert.That(
                UnityTextureEvidence.TryGetSampling(positive, out _), Is.True);
        }

        /// <summary>
        /// A7 widening. Trilinear interpolates the two adjacent levels the
        /// hardware selects; within a level its footprint is the bilinear one,
        /// and a monotone blend of two proven samples stays proven.
        /// </summary>
        [Test]
        public void TryGetSampling_TrilinearFilter_IsAdmitted()
        {
            var texture = Import("trilinear", sourceHasAlpha: true);
            texture.filterMode = FilterMode.Trilinear;

            Assert.That(
                UnityTextureEvidence.TryGetSampling(texture, out var sampling),
                Is.True);
            Assert.That(
                sampling.Filter, Is.EqualTo(TextureFilterMode.Trilinear));
        }

        /// <summary>
        /// A7 widening. Anisotropy averages an elongated footprint the
        /// classifier does not model, so it is admitted only where the proof
        /// no longer needs the footprint: the classifier's fully-opaque
        /// fast path answers every possible footprint at once. Admission here
        /// carries the state; the classifier refuses anisotropy on a level
        /// that is not fully opaque.
        /// </summary>
        [Test]
        public void TryGetSampling_Anisotropic_IsAdmitted()
        {
            var moderate = Import("aniso_moderate", sourceHasAlpha: true);
            moderate.anisoLevel = 2;
            Assert.That(
                UnityTextureEvidence.TryGetSampling(
                    moderate, out var sampling),
                Is.True);
            Assert.That(
                sampling.Aniso,
                Is.EqualTo(TextureAnisoMode.Anisotropic));
            Assert.That(sampling.Filter, Is.EqualTo(TextureFilterMode.Bilinear));

            var maximum = Import(
                "aniso_maximum", sourceHasAlpha: true,
                importer => importer.mipmapEnabled = true);
            maximum.anisoLevel = 16;
            Assert.That(
                UnityTextureEvidence.TryGetSampling(maximum, out _), Is.True);
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

        [Test]
        public void AdmittedCopyReadsColorInterpretationFromItsGraphicsFormat()
        {
            // Fixture mirrors AdmittedCopyCarriesTheReplacementForm's setup:
            // registered copy, admitted version injected. The copy keeps the
            // source's sRGB flag because LAC re-encodes with the source color
            // space.
            var copy = MakeRegisteredAdmittedCopy("colorspace");
            try
            {
                Assert.That(
                    UnityTextureEvidence.TryGetColorInterpretation(
                        copy, out var interpretation),
                    Is.True);
                Assert.That(
                    interpretation,
                    Is.EqualTo(TextureColorInterpretation.Srgb));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void PersistedCompressedCopyColorInterpretation_ReadsTheGraphicsFormat()
        {
            ReplacementTextureAttestation.ResetForTests();
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "0.9.0";
            var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true, true);
            copy.name = "MainTex_compressed";
            EditorUtility.CompressTexture(copy, TextureFormat.DXT5, TextureCompressionQuality.Normal);
            AssetDatabase.AddObjectToAsset(copy, TestContainerPath);
            AssetDatabase.SaveAssets();

            var proven = UnityTextureEvidence.TryGetColorInterpretation(
                copy, out var interpretation);

            Assert.That(proven, Is.True);
            Assert.That(interpretation, Is.EqualTo(TextureColorInterpretation.Linear));
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

        [Test]
        public void NoAlphaCopyProvesSampledAlphaExactlyOne()
        {
            var copy = MakeRegisteredAdmittedCopy("noalpha", TextureFormat.DXT1);
            try
            {
                Assert.That(
                    UnityTextureEvidence.TryProveSampledAlphaIsOne(copy), Is.True,
                    "a DXT1 copy carries no alpha channel, so the sampler " +
                    "answers exactly one");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void AdmittedDxt1AtlasProvesSampledAlphaExactlyOne()
        {
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            atlas.Apply(false, false);
            try
            {
                EditorUtility.CompressTexture(
                    atlas, TextureFormat.DXT1,
                    TextureCompressionQuality.Normal);
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";
                Assert.That(
                    UnityTextureEvidence.TryProveSampledAlphaIsOne(atlas),
                    Is.True,
                    "a DXT1 atlas the route admits carries no alpha " +
                    "channel, so the sampler answers exactly one");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(atlas);
                AaoAtlasTextureAttestation.ResetForTests();
            }
        }

        [Test]
        public void AlphaBearingCopyStaysUnprovenForSampledAlphaOne()
        {
            var copy = MakeRegisteredAdmittedCopy("withalpha", TextureFormat.BC7);
            try
            {
                Assert.That(
                    UnityTextureEvidence.TryProveSampledAlphaIsOne(copy), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void UnadmittedPathlessTextureProvesNothing()
        {
            var bare = new Texture2D(4, 4, TextureFormat.DXT1, false);
            try
            {
                Assert.That(
                    UnityTextureEvidence.TryProveSampledAlphaIsOne(bare), Is.False);
                Assert.That(
                    UnityTextureEvidence.TryGetColorInterpretation(
                        bare, out _),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bare);
            }
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

        private static Texture2D ImportSourceAsset(string name)
        {
            return Import(
                name, sourceHasAlpha: false,
                importer => importer.mipmapEnabled = true);
        }

        private static Texture2D MakeRegisteredAdmittedCopy(
            string sourceName, TextureFormat format = TextureFormat.RGBA32)
        {
            var source = ImportSourceAsset(sourceName);
            // Always create uncompressed, then compress: CompressTexture expects an
            // uncompressed input, and a native-DXT1 construction skips SetPixels.
            var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copy.name = source.name + "_compressed";
            if (format != TextureFormat.RGBA32)
            {
                EditorUtility.CompressTexture(
                    copy, format, TextureCompressionQuality.Normal);
            }
            ObjectRegistry.RegisterReplacedObject(source, copy);
            return copy;
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

/// <summary>
/// Stand-in for the pinned vendor container type of the VRCFury play-mode
/// build path. It carries the same full type name as the vendor container
/// and no vendor code, in the same spirit as the stand-in test shaders.
/// </summary>
namespace VF.Utils
{
    internal sealed class BinaryContainer : ScriptableObject
    {
    }
}
