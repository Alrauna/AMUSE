using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// A test fixture that captures fields eagerly through the production
    /// static capture engine and answers later queries from storage. It
    /// moved from the production host file on 2026-09-24. Production builds
    /// its own adapters and never constructs this type.
    /// </summary>
    internal sealed class CapturedAlphaFieldBag
    {
        private readonly Dictionary<
            (TextureSourceId source, TextureChannel channel),
            AlphaMipChain> _fieldsBySource;

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
        internal CapturedAlphaFieldBag(
            IEnumerable<(Texture texture, TextureChannel channel)> requests)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            _fieldsBySource = new Dictionary<
                (TextureSourceId, TextureChannel), AlphaMipChain>();
            foreach (var (texture, channel) in requests)
            {
                if (!UnityAlphaFieldEvidence.TryCapture(
                        texture, channel, out var source, out var chain))
                {
                    continue;
                }

                // Two textures resolving to one identity are the same asset, so the
                // first wins and the duplicate is not an error. The channel is
                // part of the key, so one asset can serve its alpha field as a
                // main texture and its red field as a mask.
                if (_fieldsBySource.ContainsKey((source, channel)))
                {
                    continue;
                }

                _fieldsBySource.Add((source, channel), chain);
            }
        }

        /// <summary>
        /// Captures the alpha field of each supplied texture. Kept for the
        /// historical call shape, which never needed a second channel.
        /// </summary>
        internal CapturedAlphaFieldBag(IEnumerable<Texture> textures)
            : this(textures?.Select(WithAlphaChannel))
        {
        }

        private static (Texture, TextureChannel) WithAlphaChannel(
            Texture texture)
        {
            return (texture, TextureChannel.Alpha);
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
            out AlphaMipChain chain)
        {
            chain = null;

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

            // Alpha and Red have producers. The red predicate is decode-proof
            // by the monotone-transfer argument (see TryCapture), so the same
            // lookup serves both; any other channel still fails closed.
            if (!_fieldsBySource.TryGetValue((source, channel), out chain))
            {
                return false;
            }

            return true;
        }
    }
}
