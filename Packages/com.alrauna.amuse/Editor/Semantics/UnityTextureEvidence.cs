using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Shader-independent Unity facts about one texture asset. Every method is a
    /// refusal predicate: it returns false whenever the fact cannot be proven
    /// from import state. The class holds no shader property names, no
    /// optimization policy, and no NDMF types, and it takes no
    /// <see cref="Material"/>; "which texture supplies this fact" is
    /// shader-specific knowledge that belongs in the frontend asking. It is not
    /// an extraction framework, and it exposes exactly the five facts that have
    /// two proven consumers.
    /// </summary>
    internal static class UnityTextureEvidence
    {
        /// <summary>
        /// Resolves the stable project identity of an assigned texture as
        /// <c>unity-asset:&lt;lowercase-guid&gt;:&lt;invariant-decimal-local-id&gt;</c>.
        /// Scene-only, generated, or otherwise unidentifiable textures are
        /// refused; identity is never fabricated from instance id, path, name,
        /// pixels, or reference equality.
        /// </summary>
        internal static bool TryGetSourceId(
            Texture texture,
            out TextureSourceId sourceId)
        {
            sourceId = default;
            if (texture == null)
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
        /// Extracts a texture's sampler state. Supported only for Point or
        /// Bilinear filtering with equal Clamp/Repeat wrap and no mip-biased or
        /// anisotropic sampling.
        /// <para>
        /// Mipmapped sampling is admitted because the resolver classifies every
        /// level of the captured chain, and Unity's Bilinear filters within the
        /// selected level and selects a level without blending - so "some level,
        /// bilinear within it" is exactly the model the conjunction covers.
        /// </para>
        /// <para>
        /// Nonzero mip bias stays refused as conservative deferred coverage: the
        /// conjunction would in fact cover it, since bias only shifts which level is
        /// selected. Trilinear likewise stays refused for scope rather than
        /// soundness - interpolating between two levels whose contributing samples
        /// are all exactly one is itself exactly one, but the sampling vocabulary
        /// does not express trilinear and widening it is a separate milestone.
        /// Anisotropy stays refused because it averages texels across an elongated
        /// footprint the classifier does not model at all.
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

            if (texture.mipMapBias != 0f ||
                texture.anisoLevel > 1)
            {
                return false;
            }

            sampling = new TextureSampling(filter, wrapU);
            return true;
        }

        /// <summary>
        /// Selects a color texture's linear/sRGB import interpretation from its
        /// <see cref="TextureImporter.sRGBTexture"/> flag. A texture with no
        /// importer (scene-only, generated) cannot prove a color meaning.
        /// </summary>
        internal static bool TryGetColorInterpretation(
            Texture texture,
            out TextureColorInterpretation interpretation)
        {
            interpretation = default;
            if (!TryGetTextureImporter(texture, out var importer))
            {
                return false;
            }

            interpretation = importer.sRGBTexture
                ? TextureColorInterpretation.Srgb
                : TextureColorInterpretation.Linear;
            return true;
        }

        /// <summary>
        /// Proves a sampled alpha of exactly one: the source carries no alpha
        /// channel and the importer imports none. Input or grayscale-derived
        /// alpha is not one and is therefore not proven.
        /// </summary>
        internal static bool TryProveSampledAlphaIsOne(Texture texture)
        {
            if (!TryGetTextureImporter(texture, out var importer))
            {
                return false;
            }

            return !importer.DoesSourceTextureHaveAlpha() &&
                   importer.alphaSource == TextureImporterAlphaSource.None;
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
