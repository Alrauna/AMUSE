using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Shader-independent Unity facts about one texture asset. Every method is a
    /// refusal predicate: it returns false whenever the fact cannot be proven
    /// from import state. The class holds no shader property names, no
    /// optimization policy, and no NDMF types, and it takes no
    /// <see cref="Material"/>; "which texture supplies this fact" is
    /// shader-specific knowledge that belongs in the frontend asking. It is not
    /// an extraction framework, and it exposes exactly six facts, each with a
    /// proven consumer.
    /// </summary>
    internal static class UnityTextureEvidence
    {
        /// <summary>
        /// Resolves the stable project identity of an assigned texture as
        /// <c>unity-asset:&lt;lowercase-guid&gt;:&lt;invariant-decimal-local-id&gt;</c>.
        /// Scene-only or unidentifiable textures fail, except an admitted
        /// replacement copy, which resolves as
        /// <c>unity-replacement:&lt;lowercase-source-guid&gt;:&lt;source-local-id&gt;</c>
        /// when the session ledger names it usable.
        /// Characterized sub-assets resolve through their container asset identity.
        /// Identity is never fabricated from instance id, path, name, pixels, or reference equality.
        /// Exception, dated 2026-10-04 (spec section 4.3): an admitted
        /// in-build Avatar Optimizer atlas mints a per-build instance-id
        /// identity.
        /// </summary>
        internal static bool TryGetSourceId(
            Texture texture,
            out TextureSourceId sourceId,
            Material originMaterial = null)
        {
            sourceId = default;
            if (texture == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
            {
                if (texture is Texture2D copy &&
                    ReplacementTextureAttestation.TryIdentifyReplacement(
                        copy, out var replacementSource))
                {
                    return ReplacementTextureIdentity.TryMint(
                        replacementSource, copy.GetInstanceID(), out sourceId);
                }
                // The in-memory Avatar Optimizer atlas admits only with a
                // corroborated origin material (spec 2026-10-04, section
                // 4.1). The route-level shape predicate stays weaker on
                // purpose; this gate is the stricter one every capture
                // passes first.
                // Upstream path (spec 2026-10-04, section 8): an atlas
                // that Avatar Optimizer registers in the NDMF object
                // registry, or persists into a build container, retires
                // this arm and the corroboration threading with it.
                if (texture is Texture2D atlas &&
                    AaoAtlasTextureAttestation.TryIdentifyAtlas(
                        atlas, originMaterial))
                {
                    return AaoAtlasIdentity.TryMint(atlas, out sourceId);
                }
                return false;
            }

            if (AssetDatabase.IsSubAsset(texture) &&
                !GeneratedTextureAttestation.TryIdentifyProducer(texture))
            {
                return false;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    texture,
                    out var guid,
                    out long localId))
            {
                return false;
            }

            if (string.IsNullOrEmpty(guid) || IsAllZeroGuid(guid))
            {
                return false;
            }

            sourceId = new TextureSourceId(
                "unity-asset:" + guid.ToLowerInvariant() + ":" +
                localId.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        /// <summary>
        /// Extracts a texture's sampler state. Supported only for Point,
        /// Bilinear, or Trilinear filtering with equal Clamp/Repeat wrap.
        /// <para>
        /// Mipmapped sampling is admitted because the resolver classifies every
        /// level of the captured chain, and Unity's Bilinear filters within the
        /// selected level and selects a level without blending - so "some level,
        /// bilinear within it" is exactly the model the conjunction covers.
        /// Trilinear is the same within-level footprint plus a blend of the two
        /// adjacent selected levels; the conjunction supplies both levels'
        /// proofs, and a monotone blend of two proven samples stays proven. A
        /// nonzero mip bias only shifts which levels the hardware selects, so
        /// it changes which proofs run, never whether the conjunction holds.
        /// </para>
        /// <para>
        /// Anisotropy is carried as its own state because it averages texels
        /// across an elongated footprint the classifier does not model. It is
        /// provable only through the classifier's fully-opaque fast path,
        /// which answers every possible footprint at once; the classifier
        /// refuses an anisotropic sample on a level that is not fully opaque.
        /// Mirror wrap stays refused: the mirrored coordinate remap is not
        /// modeled.
        /// </para>
        /// </summary>
        internal static bool TryGetSampling(
            Texture texture,
            out TextureSampling sampling)
        {
            sampling = default;
            if (texture == null)
            {
                return false;
            }

            if (texture.dimension != UnityEngine.Rendering.TextureDimension.Tex2D)
            {
                return false;
            }

            if (!TryMapFilterMode(texture.filterMode, out var filter))
            {
                return false;
            }

            if (!TryMapWrapMode(texture.wrapModeU, out var wrapU) ||
                !TryMapWrapMode(texture.wrapModeV, out var wrapV) ||
                wrapU != wrapV)
            {
                return false;
            }

            var aniso = texture.anisoLevel > 1 ||
                        (texture.anisoLevel == 1 && LevelOneSamplesAnisotropically())
                ? TextureAnisoMode.Anisotropic
                : TextureAnisoMode.None;

            sampling = new TextureSampling(filter, wrapU, aniso);
            return true;
        }

        /// <summary>
        /// Answers whether Unity samples an anisoLevel 1 texture
        /// anisotropically under the current quality mode. Forced On maps
        /// to ForceEnable and forces every level. Per Texture maps to
        /// Enable and respects level 1 as off. Disable also respects
        /// level 1 as off. This Unity version defines no Enable On Build
        /// mode, so no other mode admits level 1.
        /// </summary>
        private static bool LevelOneSamplesAnisotropically()
        {
            var mode = QualitySettings.anisotropicFiltering;
            return mode == AnisotropicFiltering.ForceEnable;
        }

        /// <summary>
        /// Selects linear or sRGB color interpretation for a texture.
        /// Reads the import flag for imported textures.
        /// Reads the graphics format for characterized generated textures,
        /// admitted replacement copies, and in-memory admitted Avatar
        /// Optimizer atlases.
        /// Other textures cannot prove a color meaning.
        /// </summary>
        internal static bool TryGetColorInterpretation(
            Texture texture,
            out TextureColorInterpretation interpretation)
        {
            interpretation = default;
            if (texture == null)
            {
                return false;
            }

            if (TryGetTextureImporter(texture, out var importer))
            {
                interpretation = importer.sRGBTexture
                    ? TextureColorInterpretation.Srgb
                    : TextureColorInterpretation.Linear;
                return true;
            }

            if (texture is Texture2D texture2D &&
                GeneratedTextureAttestation.TryIdentifyRouteTexture(texture2D))
            {
                var isSrgb = UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat) ||
                             texture.isDataSRGB;
                interpretation = isSrgb
                    ? TextureColorInterpretation.Srgb
                    : TextureColorInterpretation.Linear;
                return true;
            }

            return false;
        }
        /// <summary>
        /// Proves a sampled alpha of exactly one: the source carries no alpha
        /// channel and the importer imports none, or an attested route texture
        /// (a container-backed generated texture, an admitted replacement
        /// copy, or an in-memory admitted Avatar Optimizer atlas) has a
        /// format that names no alpha component. Input or
        /// grayscale-derived alpha is not one and is therefore not proven.
        /// A normal-map import is never proven, because the conversion
        /// writes the normal x component into alpha.
        /// </summary>
        internal static bool TryProveSampledAlphaIsOne(Texture texture)
        {
            if (TryGetTextureImporter(texture, out var importer))
            {
                if (importer.textureType == TextureImporterType.NormalMap)
                {
                    // The desktop normal-map conversion swizzles the normal x
                    // component into the alpha channel. The sampled alpha is that
                    // component, not one, whatever the source alpha facts say.
                    return false;
                }

                return !importer.DoesSourceTextureHaveAlpha() &&
                       importer.alphaSource == TextureImporterAlphaSource.None;
            }

            // A replacement copy has no importer. Its format names the sampled
            // channels: a format with no alpha component samples exactly one, the
            // same fact the RGB24 exemption rests on. This covers DXT1 and RGB24
            // copies, and is equally sound for a BC5 copy, because missing channels
            // sample as one at runtime; the capture allowlist still refuses BC5 for
            // chain-based facts, which is a separate question.
            if (texture is Texture2D copy &&
                GeneratedTextureAttestation.TryIdentifyRouteTexture(copy) &&
                !UnityEngine.Experimental.Rendering.GraphicsFormatUtility
                    .HasAlphaChannel(copy.graphicsFormat))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Recognizes the canonical Unity tangent-space normal-map import: the
        /// normal-map texture type with no green-channel inversion. Any other
        /// import cannot be read as an unmodified tangent-space normal.
        /// </summary>
        internal static bool IsCanonicalNormalMapImport(Texture texture)
        {
            if (!TryGetTextureImporter(texture, out var importer))
            {
                return false;
            }

            return importer.textureType == TextureImporterType.NormalMap &&
                   !importer.flipGreenChannel;
        }

        /// <summary>
        /// Positively proves that every effective sampled colour value for this
        /// texture is finite and confined to [0,1]. The capture records this as
        /// a request-scoped fact beside the colour interpretation. The capture
        /// stores values, never texture references, so a consumer cannot run
        /// this predicate after the capture.
        /// <para>
        /// Only imported formats on the allow-list below succeed. Every other
        /// format — signed-normalized, half, float, shared-exponent, BC6H — and
        /// every texture whose importer cannot be read, refuses. A format Unity
        /// adds in a future version is not on the list and therefore refuses.
        /// Nothing is clamped, approximated, or assumed bounded. The fact has
        /// one lilToon consumer today: it names the range in which
        /// <c>lilToneCorrection</c> at <c>_MainTexHSVG = (0,1,1,1)</c> is the
        /// identity.
        /// </para>
        /// </summary>
        internal static bool TryProveColorValuesInUnitRange(Texture texture)
        {
            if (texture == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path) ||
                !(AssetImporter.GetAtPath(path) is TextureImporter))
            {
                return false;
            }

            return BoundedColorFormats.Contains(texture.graphicsFormat);
        }

        /// <summary>
        /// Unsigned-normalized and sRGB formats, whose decoded values are
        /// exactly the closed interval [0,1]. Enumerated rather than
        /// pattern-matched so an unrecognized format cannot pass by accident.
        /// </summary>
        private static readonly HashSet<GraphicsFormat> BoundedColorFormats =
            new HashSet<GraphicsFormat>
            {
                GraphicsFormat.R8_UNorm,
                GraphicsFormat.R8G8_UNorm,
                GraphicsFormat.R8G8B8_UNorm,
                GraphicsFormat.R8G8B8A8_UNorm,
                GraphicsFormat.R8G8B8_SRGB,
                GraphicsFormat.R8G8B8A8_SRGB,
                GraphicsFormat.B8G8R8_UNorm,
                GraphicsFormat.B8G8R8A8_UNorm,
                GraphicsFormat.B8G8R8_SRGB,
                GraphicsFormat.B8G8R8A8_SRGB,
                GraphicsFormat.R16_UNorm,
                GraphicsFormat.R16G16_UNorm,
                GraphicsFormat.R16G16B16_UNorm,
                GraphicsFormat.R16G16B16A16_UNorm,
                GraphicsFormat.R5G6B5_UNormPack16,
                GraphicsFormat.R4G4B4A4_UNormPack16,
                GraphicsFormat.R5G5B5A1_UNormPack16,
                GraphicsFormat.RGBA_DXT1_UNorm,
                GraphicsFormat.RGBA_DXT1_SRGB,
                GraphicsFormat.RGBA_DXT3_UNorm,
                GraphicsFormat.RGBA_DXT3_SRGB,
                GraphicsFormat.RGBA_DXT5_UNorm,
                GraphicsFormat.RGBA_DXT5_SRGB,
                GraphicsFormat.R_BC4_UNorm,
                GraphicsFormat.RG_BC5_UNorm,
                GraphicsFormat.RGBA_BC7_UNorm,
                GraphicsFormat.RGBA_BC7_SRGB,
                GraphicsFormat.RGB_ETC_UNorm,
                GraphicsFormat.RGB_ETC2_UNorm,
                GraphicsFormat.RGB_ETC2_SRGB,
                GraphicsFormat.RGB_A1_ETC2_UNorm,
                GraphicsFormat.RGB_A1_ETC2_SRGB,
                GraphicsFormat.RGBA_ETC2_UNorm,
                GraphicsFormat.RGBA_ETC2_SRGB,
                GraphicsFormat.RGBA_ASTC4X4_UNorm,
                GraphicsFormat.RGBA_ASTC4X4_SRGB,
                GraphicsFormat.RGBA_ASTC5X5_UNorm,
                GraphicsFormat.RGBA_ASTC5X5_SRGB,
                GraphicsFormat.RGBA_ASTC6X6_UNorm,
                GraphicsFormat.RGBA_ASTC6X6_SRGB,
                GraphicsFormat.RGBA_ASTC8X8_UNorm,
                GraphicsFormat.RGBA_ASTC8X8_SRGB,
                GraphicsFormat.RGBA_ASTC10X10_UNorm,
                GraphicsFormat.RGBA_ASTC10X10_SRGB,
                GraphicsFormat.RGBA_ASTC12X12_UNorm,
                GraphicsFormat.RGBA_ASTC12X12_SRGB,
            };

        private static bool TryGetTextureImporter(
            Texture texture,
            out TextureImporter importer)
        {
            importer = null;
            if (texture == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            importer = AssetImporter.GetAtPath(path) as TextureImporter;
            return importer != null;
        }

        private static bool TryMapFilterMode(
            FilterMode mode,
            out TextureFilterMode filter)
        {
            switch (mode)
            {
                case FilterMode.Point:
                    filter = TextureFilterMode.Point;
                    return true;
                case FilterMode.Bilinear:
                    filter = TextureFilterMode.Bilinear;
                    return true;
                case FilterMode.Trilinear:
                    filter = TextureFilterMode.Trilinear;
                    return true;
                default:
                    filter = default;
                    return false;
            }
        }

        private static bool TryMapWrapMode(
            UnityEngine.TextureWrapMode mode,
            out TextureWrapMode wrap)
        {
            switch (mode)
            {
                case UnityEngine.TextureWrapMode.Clamp:
                    wrap = TextureWrapMode.Clamp;
                    return true;
                case UnityEngine.TextureWrapMode.Repeat:
                    wrap = TextureWrapMode.Repeat;
                    return true;
                default:
                    wrap = default;
                    return false;
            }
        }

        private static bool IsAllZeroGuid(string guid)
        {
            foreach (var c in guid)
            {
                if (c != '0')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
