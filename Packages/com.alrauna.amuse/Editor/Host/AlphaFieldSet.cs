using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// One gathered field's identity: the texture source and channel plus
    /// the capture predicate that gives the stored bytes their meaning.
    /// <para>
    /// The predicate is part of the key and never optional. A byte 255 in
    /// an alpha field means "the sampled value satisfied the capture's
    /// opaque test", and that test differs per material: a shader-cutoff
    /// source binarizes by its declared cutoff, so 255 means "alpha at or
    /// above the cutoff", while an exact source keeps 255 meaning "alpha
    /// exactly one". Two materials sharing one texture under different
    /// predicates therefore must never share one field, whichever was
    /// captured first - the capture layer already separates them, and
    /// this key keeps the separation alive through the gather.
    /// </para>
    /// </summary>
    internal readonly struct AlphaFieldKey : IEquatable<AlphaFieldKey>
    {
        internal TextureSourceId Source { get; }
        internal TextureChannel Channel { get; }
        internal float Threshold { get; }
        internal AlphaPolicyBounds Bounds { get; }

        internal AlphaFieldKey(
            TextureSourceId source,
            TextureChannel channel,
            float threshold,
            AlphaPolicyBounds bounds)
        {
            Source = source;
            Channel = channel;
            Threshold = threshold;
            Bounds = bounds;
        }

        /// <summary>
        /// The alpha arm of one captured texture assignment. The threshold
        /// is the assignment's declared cutoff; below one the capture kept
        /// the policy inert for the source, so the effective bounds are
        /// inert too, exactly as the capture computed them.
        /// </summary>
        internal static AlphaFieldKey ForAlpha(
            CapturedTextureEvidence texture)
        {
            return new AlphaFieldKey(
                texture.SourceIdentity,
                TextureChannel.Alpha,
                texture.CaptureThreshold,
                texture.CaptureThreshold < 1f
                    ? AlphaPolicyBounds.Inert
                    : texture.CaptureBounds);
        }

        /// <summary>
        /// The red arm of one captured texture assignment. Masks are always
        /// captured exact - a texel between a noise gate and a shader
        /// cutoff never applies to the mask product - so the arm's
        /// threshold is fixed at one and the capture's own bounds govern.
        /// </summary>
        internal static AlphaFieldKey ForRed(
            CapturedTextureEvidence texture)
        {
            return new AlphaFieldKey(
                texture.SourceIdentity,
                TextureChannel.Red,
                1f,
                texture.CaptureBounds);
        }

        public bool Equals(AlphaFieldKey other)
        {
            return Source.Equals(other.Source) &&
                   Channel == other.Channel &&
                   Threshold.Equals(other.Threshold) &&
                   Bounds.Equals(other.Bounds);
        }

        public override bool Equals(object obj)
        {
            return obj is AlphaFieldKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Source, Channel, Threshold, Bounds);
        }
    }

    /// <summary>
    /// The alpha and red fields of one renderer's admitted materials, keyed
    /// by their capture predicate. A consumer consults a field only through
    /// <see cref="TryGetFor"/> with the evidence whose request produced it,
    /// so a field captured under one material's predicate can never answer
    /// for another material's lookup.
    /// <para>
    /// A missing field fails closed (the caller's MissingTextureEvidence
    /// path). A material whose own request names one source and channel
    /// under two different predicates also fails closed: no single field
    /// carries both meanings, and guessing either would put one predicate's
    /// verdict bytes under the other's interpretation.
    /// </para>
    /// </summary>
    internal sealed class AlphaFieldSet
    {
        private readonly Dictionary<AlphaFieldKey, AlphaMipChain> _fields;

        internal AlphaFieldSet(
            Dictionary<AlphaFieldKey, AlphaMipChain> fields)
        {
            _fields = fields
                ?? throw new ArgumentNullException(nameof(fields));
        }

        /// <summary>
        /// One field under one explicit predicate. The test seam for a
        /// synthetic chain answering one captured texture assignment.
        /// </summary>
        internal static AlphaFieldSet WithField(
            TextureSourceId source,
            TextureChannel channel,
            float threshold,
            AlphaPolicyBounds bounds,
            AlphaMipChain chain)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return new AlphaFieldSet(
                new Dictionary<AlphaFieldKey, AlphaMipChain>
                {
                    [new AlphaFieldKey(source, channel, threshold, bounds)] =
                        chain,
                });
        }

        internal int Count => _fields.Count;

        /// <summary>
        /// Returns the field this evidence's own captured predicate names
        /// for the source and channel. <paramref name="predicateRequest"/>
        /// is the material's own family alpha request: the closed batch
        /// captures the union schema, so the evidence can carry sibling
        /// entries for the same texture source that this family never
        /// samples, and only the assignments its request names decide the
        /// predicate. A null request means the caller captured outside the
        /// closed batch, and every entry of the evidence decides.
        /// <para>
        /// Returns false - with no field - whenever the deciding entries
        /// disagree on the predicate, or the gather holds no chain for the
        /// resulting key. A malformed argument throws instead, because
        /// silence would hide a caller defect.
        /// </para>
        /// </summary>
        internal bool TryGetFor(
            CapturedMaterialEvidence evidence,
            MaterialEvidenceRequest predicateRequest,
            TextureSourceId source,
            TextureChannel channel,
            out AlphaMipChain chain)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            if (channel != TextureChannel.Alpha &&
                channel != TextureChannel.Red)
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            chain = null;
            var found = false;
            var key = default(AlphaFieldKey);
            foreach (var assignment in evidence.TextureAssignments)
            {
                if (!assignment.HasValue)
                {
                    continue;
                }

                // A property present in the shader with no texture assigned
                // captures a named assignment with no captured evidence.
                var texture = assignment.Value.Texture;
                if (texture == null)
                {
                    continue;
                }

                if (!texture.HasSourceIdentity ||
                    !texture.SourceIdentity.Equals(source))
                {
                    continue;
                }

                if (predicateRequest != null &&
                    !AsksChannel(predicateRequest, assignment.Name, channel))
                {
                    continue;
                }

                AlphaFieldKey candidate;
                if (channel == TextureChannel.Alpha)
                {
                    if (!texture.HasAlphaChannel)
                    {
                        continue;
                    }

                    candidate = AlphaFieldKey.ForAlpha(texture);
                }
                else
                {
                    if (!texture.HasRedChannel)
                    {
                        continue;
                    }

                    candidate = AlphaFieldKey.ForRed(texture);
                }

                if (found && !candidate.Equals(key))
                {
                    // The deciding assignments name this source under two
                    // different predicates. No single field answers both,
                    // so the lookup refuses rather than hand over either
                    // predicate's bytes under the other's meaning.
                    return false;
                }

                key = candidate;
                found = true;
            }

            return found &&
                _fields.TryGetValue(key, out chain) &&
                chain != null;
        }

        /// <summary>
        /// Whether the request asks this channel of this texture property.
        /// Naming a property without asking the channel - the cutout
        /// family asks only the mask's red channel - means the family
        /// never samples that channel, so the entry must not decide the
        /// predicate of a lookup for it.
        /// </summary>
        private static bool AsksChannel(
            MaterialEvidenceRequest request,
            string propertyName,
            TextureChannel channel)
        {
            var kind = channel == TextureChannel.Alpha
                ? TextureEvidenceKinds.AlphaChannel
                : TextureEvidenceKinds.RedChannel;
            foreach (var named in request.TextureProperties)
            {
                if (string.Equals(
                        named.PropertyName,
                        propertyName,
                        StringComparison.Ordinal))
                {
                    return (named.Evidence & kind) != 0;
                }
            }

            return false;
        }
    }
}
