using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// The one Unity implementation of <see cref="AlphaFieldProvider"/>. It converts
    /// supported Unity texture state into the immutable
    /// <see cref="AlphaTextureData"/> the exact triangle alpha classifier already
    /// consumes, and refuses everything it cannot prove.
    /// <para>
    /// It lives outside <c>Analysis</c> because that namespace has no dependency on
    /// the <c>UnityEditor</c> namespace and must keep none, and outside
    /// <c>Semantics</c> because <c>Analysis</c> depends on <c>Semantics</c> and not
    /// the reverse. It reads only: it opens no <c>TextureImporter</c>, writes no
    /// asset, and never changes an import setting.
    /// </para>
    /// <para>
    /// The evidence it produces is <em>predicate-equivalent</em> to effective mip-0
    /// shader alpha, not byte-identical to GPU memory: byte 255 marks exactly the
    /// texels whose sampled alpha is exactly one, and every other byte marks a value
    /// strictly below one. That is the contract
    /// <see cref="AlphaFieldProvider"/> states and the only property
    /// <see cref="TriangleAlphaClassifier"/> reads. Every admitted format was
    /// measured against a real shader sample; formats whose CPU view can round a
    /// value below one up to 255 are refused.
    /// </para>
    /// </summary>
    internal sealed class UnityAlphaFieldEvidence
    {
        private readonly Dictionary<TextureSourceId, Texture2D> _texturesBySource;

        /// <summary>
        /// Resolves the supplied textures to their stable project identities through
        /// the existing <see cref="UnityTextureEvidence.TryGetSourceId"/>, so the
        /// identity rule can never disagree with the one the shader frontends used to
        /// build the <see cref="TextureSample"/>. The opaque source-id format is
        /// never parsed here.
        /// <para>
        /// Elements that are null, destroyed, not a <see cref="Texture2D"/>, or
        /// without a resolvable identity are skipped rather than rejected: an
        /// unassigned material slot yields a null texture and is an ordinary input,
        /// not a caller error. A later lookup for such a texture simply refuses.
        /// </para>
        /// </summary>
        internal UnityAlphaFieldEvidence(IEnumerable<Texture> textures)
        {
            if (textures == null)
            {
                throw new ArgumentNullException(nameof(textures));
            }

            _texturesBySource = new Dictionary<TextureSourceId, Texture2D>();
            foreach (var texture in textures)
            {
                // Unity's overloaded equality is required: it is true for a destroyed
                // object, where ReferenceEquals would be false. A non-Texture2D
                // (RenderTexture, Cubemap, array, 3D) yields a real null here and is
                // skipped for the same reason it would be refused at lookup.
                var texture2D = texture as Texture2D;
                if (texture2D == null)
                {
                    continue;
                }

                if (!UnityTextureEvidence.TryGetSourceId(texture2D, out var source))
                {
                    continue;
                }

                // Two textures resolving to one identity are the same asset, so the
                // first wins and the duplicate is not an error.
                if (_texturesBySource.ContainsKey(source))
                {
                    continue;
                }

                _texturesBySource.Add(source, texture2D);
            }
        }

        /// <summary>
        /// Signature-compatible with <see cref="AlphaFieldProvider"/>; pass it as a
        /// method group. Returns false, with no field, whenever the effective alpha
        /// cannot be proven. A malformed argument throws instead, because silence
        /// would hide a caller defect.
        /// </summary>
        internal bool TryGetAlphaField(
            TextureSourceId source,
            TextureChannel channel,
            out AlphaTextureData field)
        {
            field = null;

            if (string.IsNullOrWhiteSpace(source.Value))
            {
                throw new ArgumentException(
                    "Texture source identity must be initialized.",
                    nameof(source));
            }

            if (!Enum.IsDefined(typeof(TextureChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            // Only Alpha has a producer today. A colour channel would additionally
            // need the sRGB transfer argument written down, so it fails closed.
            if (channel != TextureChannel.Alpha)
            {
                return false;
            }

            if (!_texturesBySource.TryGetValue(source, out var texture))
            {
                return false;
            }

            // Destroyed between construction and this call.
            if (texture == null)
            {
                return false;
            }

            try
            {
                // Without a CPU copy there is no non-GPU route to the data.
                if (!texture.isReadable)
                {
                    return false;
                }

                // The classifier models exactly one texel grid, so a mipmapped
                // texture is outside the represented domain.
                if (texture.mipmapCount != 1)
                {
                    return false;
                }

                if (!IsSupportedFormat(texture.format))
                {
                    return false;
                }

                var width = texture.width;
                var height = texture.height;
                if (width <= 0 || height <= 0)
                {
                    return false;
                }

                Color32[] pixels;
                try
                {
                    pixels = texture.GetPixels32(0);
                }
                catch (ArgumentException)
                {
                    // Measured: raised when the data is not readable, corrupted, or
                    // absent, and for an invalid mip level. An unprovable read is a
                    // refusal, never a partial field.
                    return false;
                }

                if (pixels == null || pixels.Length != (long)width * height)
                {
                    return false;
                }

                // GetPixels32 is row-major bottom-to-top and so is AlphaTextureData,
                // so the alpha bytes copy straight across with no flip or transpose.
                var alpha = new byte[pixels.Length];
                for (var index = 0; index < pixels.Length; index++)
                {
                    alpha[index] = pixels[index].a;
                }

                field = new AlphaTextureData(width, height, alpha);
                return true;
            }
            catch (MissingReferenceException)
            {
                // Measured: raised by any member access on a destroyed object,
                // including isReadable, and its base type is SystemException rather
                // than UnityException. Guards every Unity-object read above.
                field = null;
                return false;
            }
        }

        /// <summary>
        /// The closed, measured allow-list. Each member was compared against a real
        /// shader sample at alpha 0, 128, 254, and 255, and agreed in every case.
        /// <para>
        /// The first three carry a native 8-bit UNorm alpha channel, which the GPU
        /// decodes as <c>b / 255</c>, so byte 255 is exactly one and nothing else is.
        /// <see cref="TextureFormat.RGB24"/> has no alpha channel at all, so the
        /// sampler returns exactly one and the CPU view reports 255 uniformly.
        /// </para>
        /// <para>
        /// Everything else is refused. Compressed formats were measured to turn a
        /// source alpha of 254 into 255, and float formats were measured to round an
        /// alpha of 0.999 up to 255 through the Color32 view — both are fabricated
        /// opacity. <c>BGRA32</c> is absent because <c>TextureImporterFormat</c>
        /// cannot request it in Unity 2022.3, so its equivalence could not be proven.
        /// </para>
        /// </summary>
        private static bool IsSupportedFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.Alpha8:
                case TextureFormat.RGB24:
                    return true;
                default:
                    return false;
            }
        }
    }
}
