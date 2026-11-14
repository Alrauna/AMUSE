using System;
using Alrauna.Amuse.Editor.Host;
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
    /// The moved rows run their states through the production evidence path on
    /// the installed bundled main shader: material rows read the real material
    /// directly, and texture import rows ride the production capture over the
    /// real material's main texture slot.
    /// </summary>
    // Kept stand-in reasons: the transient textures, the out-of-set and
    // non-finite UV channels, the missing schema property, and the missing
    // importer rows need inputs no vendor run produces.
    public sealed class PoiyomiTextureEvidenceTests
        : PoiyomiFixtureTestBase
    {
        /// <summary>
        /// A fresh material on the installed bundled main shader loaded by
        /// its pinned GUID. The caller owns destruction through Track.
        /// </summary>
        private Material RealMainMaterial()
        {
            return Track(RealPoiyomiMaterial.Main());
        }

        /// <summary>
        /// The real main material with one staged texture assigned to
        /// <c>_MainTex</c>. The caller owns destruction through Track.
        /// </summary>
        private Material RealMainMaterialWithTexture(Texture texture)
        {
            var material = RealMainMaterial();
            material.SetTexture("_MainTex", texture);
            return material;
        }

        /// <summary>
        /// Captures the named texture import facts through the production
        /// capture over one real material's main texture slot. The row
        /// assigns the texture to <c>_MainTex</c> first. The request asks
        /// only for the import facts these rows assert, so the capture
        /// never reads more than the rows name.
        /// </summary>
        private static CapturedTextureEvidence CaptureMainTexture(
            Material material)
        {
            var request = new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: Array.Empty<string>(),
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        "_MainTex",
                        TextureEvidenceKinds.SourceIdentity |
                        TextureEvidenceKinds.Sampling |
                        TextureEvidenceKinds.ColorInterpretation |
                        TextureEvidenceKinds.SampledAlphaIsOne |
                        TextureEvidenceKinds.CanonicalNormalMap |
                        TextureEvidenceKinds.AlphaChannel),
                });
            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(material, request),
            });
            Assert.That(
                captured[0].TryGetTexture("_MainTex", out var assignment),
                Is.True,
                "fixture precondition: the main texture slot is captured");
            Assert.That(
                assignment.IsAssigned && assignment.Texture != null,
                Is.True,
                "fixture precondition: the row assigns the main texture");
            return assignment.Texture;
        }

        [Test]
        public void SourceId_AssignedAsset_MatchesUnityAssetTokenFormat()
        {
            var texture = ImportTexture("identity_format");
            var captured = CaptureMainTexture(RealMainMaterialWithTexture(texture));

            Assert.That(captured.HasSourceIdentity, Is.True);
            Assert.That(
                captured.SourceIdentity.Value,
                Is.EqualTo(ExpectedToken(texture)));
            Assert.That(
                captured.SourceIdentity.Value,
                Does.StartWith("unity-asset:"));
        }

        [Test]
        public void SourceId_SameAsset_IsStableAcrossCalls()
        {
            var texture = ImportTexture("identity_stable");
            var material = RealMainMaterialWithTexture(texture);

            var first = CaptureMainTexture(material);
            var second = CaptureMainTexture(material);

            Assert.That(
                first.SourceIdentity.Value,
                Is.EqualTo(second.SourceIdentity.Value));
        }

        [Test]
        public void SourceId_DistinctAssets_ProduceDistinctIds()
        {
            var a = ImportTexture("identity_a");
            var b = ImportTexture("identity_b");

            var idA = CaptureMainTexture(RealMainMaterialWithTexture(a));
            var idB = CaptureMainTexture(RealMainMaterialWithTexture(b));

            Assert.That(
                idA.SourceIdentity.Value,
                Is.Not.EqualTo(idB.SourceIdentity.Value));
        }

        [Test]
        public void SourceId_RenamedAsset_IsUnchanged()
        {
            var texture = ImportTexture("identity_before_rename");
            var material = RealMainMaterialWithTexture(texture);
            var before = CaptureMainTexture(material);

            var moveError = AssetDatabase.MoveAsset(
                TempFolder + "/identity_before_rename.png",
                TempFolder + "/identity_after_rename.png");
            Assert.That(moveError, Is.Empty, "Rename must succeed for this probe.");

            var after = CaptureMainTexture(material);

            Assert.That(
                after.SourceIdentity.Value,
                Is.EqualTo(before.SourceIdentity.Value));
        }

        [Test]
        public void SourceId_TransientTexture_IsRefusedWithoutFallback()
        {
            // A runtime texture with no backing asset must not be given an
            // identity from instance id, name, or reference; it is refused.
            var transient = new Texture2D(4, 4);
            Track(transient);

            var ok = UnityTextureEvidence.TryGetSourceId(
                transient,
                out _);

            Assert.That(ok, Is.False);
        }

        // --- UV / ST mapping ------------------------------------------------

        [Test]
        public void Uv_ChannelValues0Through3_MapToChannels(
            [Values(0, 1, 2, 3)] int channel)
        {
            var material = RealMainMaterial();
            material.SetFloat("_MainTexUV", channel);

            var ok = PoiyomiMaterialSemantics.TryGetSupportedUvMapping(
                material, "_MainTex", "_MainTexUV", "_MainTexPan", out var mapping);

            Assert.That(ok, Is.True);
            Assert.That(mapping.Channel, Is.EqualTo(channel));
        }

        [Test]
        public void Uv_ScaleAndOffset_AreCaptured()
        {
            var material = RealMainMaterial();
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
            var material = RealMainMaterial();
            material.SetVector("_MainTexPan", new Vector4(0.1f, 0f, 0f, 0f));

            var ok = PoiyomiMaterialSemantics.TryGetSupportedUvMapping(
                material, "_MainTex", "_MainTexUV", "_MainTexPan", out _);

            Assert.That(ok, Is.False);
        }

        // --- Exact-zero mode gate ------------------------------------------

        [Test]
        public void ModeGate_AllPropertiesZero_IsTrue()
        {
            var material = RealMainMaterial();

            var failed = EvidenceGates.FirstFailedZeroGate(
                material, "_MainTexStochastic", "_MainPixelMode");

            Assert.That(failed, Is.Null);
        }

        [Test]
        public void ModeGate_NonZeroStochastic_IsFalse()
        {
            var material = RealMainMaterial();
            material.SetFloat("_MainTexStochastic", 1f);

            var failed = EvidenceGates.FirstFailedZeroGate(
                material, "_MainTexStochastic", "_MainPixelMode");

            Assert.That(failed, Is.EqualTo("_MainTexStochastic"));
        }

        [Test]
        public void ModeGate_NonZeroPixelMode_IsFalse()
        {
            var material = RealMainMaterial();
            material.SetFloat("_MainPixelMode", 1f);

            var failed = EvidenceGates.FirstFailedZeroGate(
                material, "_MainTexStochastic", "_MainPixelMode");

            Assert.That(failed, Is.EqualTo("_MainPixelMode"));
        }

        [Test]
        public void ModeGate_MissingProperty_IsFalse()
        {
            var material = NewFixtureMaterial();

            var failed = EvidenceGates.FirstFailedZeroGate(
                material, "_ThisPropertyDoesNotExist");

            Assert.That(failed, Is.EqualTo("_ThisPropertyDoesNotExist"));
        }

        // --- MainTex sampler ------------------------------------------------

        [Test]
        public void Sampler_BilinearRepeat_IsSupported()
        {
            var material = RealMainMaterial();
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
            var material = RealMainMaterial();
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
        public void Sampler_MirrorWrap_IsUnsupported()
        {
            var material = RealMainMaterial();
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
            var material = RealMainMaterial();
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

        /// <summary>
        /// Mipmapped sampling is admitted now that the resolver classifies every
        /// level of the captured chain, so the frontend must stop refusing it.
        /// Trilinear remains unsupported and is pinned by
        /// <see cref="Sampler_Trilinear_IsUnsupported"/>.
        /// </summary>
        [Test]
        public void Sampler_Mipmapped_IsSupported()
        {
            var material = RealMainMaterial();
            material.SetTexture("_MainTex", ImportTexture("sampler_mip", i =>
            {
                i.mipmapEnabled = true;
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
        public void Sampler_UsesMainTexNotAuxiliaryTexture()
        {
            // The MainTex sampler is authoritative; an auxiliary texture's own
            // unsupported sampler state must not change the result.
            var material = RealMainMaterial();
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
            var material = RealMainMaterial();

            var ok = PoiyomiMaterialSemantics.TryGetMainTextureSampling(
                material, out _);

            Assert.That(ok, Is.False);
        }

        // --- Color import interpretation -----------------------------------

        [Test]
        public void Color_SrgbImport_IsSrgb()
        {
            var texture = ImportTexture("color_srgb", i => i.sRGBTexture = true);
            var captured = CaptureMainTexture(RealMainMaterialWithTexture(texture));

            Assert.That(captured.HasColorInterpretation, Is.True);
            Assert.That(
                captured.ColorInterpretation,
                Is.EqualTo(TextureColorInterpretation.Srgb));
        }

        [Test]
        public void Color_LinearImport_IsLinear()
        {
            var texture = ImportTexture("color_linear", i => i.sRGBTexture = false);
            var captured = CaptureMainTexture(RealMainMaterialWithTexture(texture));

            Assert.That(captured.HasColorInterpretation, Is.True);
            Assert.That(
                captured.ColorInterpretation,
                Is.EqualTo(TextureColorInterpretation.Linear));
        }

        [Test]
        public void Color_MissingImporter_IsRefused()
        {
            var transient = new Texture2D(4, 4);
            Track(transient);

            var ok = UnityTextureEvidence.TryGetColorInterpretation(
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
            var captured = CaptureMainTexture(RealMainMaterialWithTexture(texture));

            Assert.That(captured.SampledAlphaIsProvenOne, Is.True);
        }

        [Test]
        public void Alpha_SourceWithAlpha_IsNotProvenOne()
        {
            var texture = ImportTexture(
                "alpha_present",
                i => i.alphaSource = TextureImporterAlphaSource.FromInput,
                sourceHasAlpha: true);
            var captured = CaptureMainTexture(RealMainMaterialWithTexture(texture));

            Assert.That(captured.SampledAlphaIsProvenOne, Is.False);
        }

        [Test]
        public void Alpha_MissingImporter_IsNotProvenOne()
        {
            var transient = new Texture2D(4, 4);
            Track(transient);

            var proven =
                UnityTextureEvidence.TryProveSampledAlphaIsOne(transient);

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
            var captured = CaptureMainTexture(RealMainMaterialWithTexture(texture));

            Assert.That(captured.IsCanonicalNormalMap, Is.True);
        }

        [Test]
        public void Normal_DefaultImport_IsNotCanonicalNormalMap()
        {
            var texture = ImportTexture("normal_default", i =>
            {
                i.textureType = TextureImporterType.Default;
            });
            var captured = CaptureMainTexture(RealMainMaterialWithTexture(texture));

            Assert.That(captured.IsCanonicalNormalMap, Is.False);
        }

        [Test]
        public void Normal_GreenChannelInverted_IsRejected()
        {
            var texture = ImportTexture("normal_flipped", i =>
            {
                i.textureType = TextureImporterType.NormalMap;
                i.flipGreenChannel = true;
            });
            var captured = CaptureMainTexture(RealMainMaterialWithTexture(texture));

            Assert.That(captured.IsCanonicalNormalMap, Is.False);
        }

        [Test]
        public void Normal_MissingImporter_IsNotCanonicalNormalMap()
        {
            var transient = new Texture2D(4, 4);
            Track(transient);

            var ok = UnityTextureEvidence.IsCanonicalNormalMapImport(transient);

            Assert.That(ok, Is.False);
        }
    }
}
