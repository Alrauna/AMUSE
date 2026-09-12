using System;
using System.Collections.Generic;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// One texture's ordered alpha mip chain: the existing per-level grids the
    /// classifier already consumes, mip 0 first, plus per-level provenance
    /// naming the levels the capture could not examine. A level flagged
    /// without evidence - a non-resident consulted mip under the active
    /// mipmap limit - has no effective alpha for playback, so no grid can
    /// stand in for the fact and the fold must degrade it to Unknown for
    /// every triangle. The flagged levels still carry their declared
    /// dimensions, so shape validation and the geometric size scope keep
    /// working unchanged.
    /// <para>
    /// It guarantees <em>shape</em> and nothing else: non-empty, ordered, no null
    /// element, and each level's width and height independently equal
    /// <c>max(1, previous &gt;&gt; 1)</c>. Non-emptiness is the load-bearing
    /// invariant: an empty chain would make "every level is ProvenOpaque"
    /// vacuously true and turn the conjunction in <see cref="AlphaResolution"/>
    /// into an unconditional proof of opacity.
    /// </para>
    /// <para>
    /// It deliberately does <strong>not</strong> require the chain to reach 1x1. A
    /// correctly shaped prefix is in-domain, because this type cannot see
    /// <c>mipmapCount</c> and so cannot prove completeness. That the chain is every
    /// level the sampler may select is the <see cref="AlphaFieldProvider"/>
    /// contract's obligation and the capture loop's, never this constructor's.
    /// </para>
    /// <para>
    /// It is not a texture IR: it carries no format, channel, colour space,
    /// sampler, source identity, or transformation, and cannot represent a
    /// magnitude or a non-alpha channel.
    /// </para>
    /// </summary>
    internal sealed class AlphaMipChain
    {
        private readonly AlphaTextureData[] _levels;
        private readonly bool[] _levelsWithoutEvidence;

        internal AlphaMipChain(IReadOnlyList<AlphaTextureData> levelsFromMipZero)
            : this(levelsFromMipZero, AllEvidence(levelsFromMipZero))
        {
        }

        /// <summary>
        /// The provenance overload: a level whose entry is true was captured
        /// with no usable evidence. Its grid is a placeholder that keeps the
        /// declared shape; consumers must consult the provenance before the
        /// grid, because the placeholder's bytes prove nothing.
        /// </summary>
        internal AlphaMipChain(
            IReadOnlyList<AlphaTextureData> levelsFromMipZero,
            IReadOnlyList<bool> levelsWithoutEvidenceFromMipZero)
        {
            if (levelsFromMipZero == null)
            {
                throw new ArgumentNullException(nameof(levelsFromMipZero));
            }
            if (levelsFromMipZero.Count == 0)
            {
                throw new ArgumentException(
                    "A mip chain must contain at least mip 0.",
                    nameof(levelsFromMipZero));
            }
            if (levelsWithoutEvidenceFromMipZero == null)
            {
                throw new ArgumentNullException(
                    nameof(levelsWithoutEvidenceFromMipZero));
            }
            if (levelsWithoutEvidenceFromMipZero.Count !=
                levelsFromMipZero.Count)
            {
                throw new ArgumentException(
                    "The per-level provenance must name every level.",
                    nameof(levelsWithoutEvidenceFromMipZero));
            }

            var levels = new AlphaTextureData[levelsFromMipZero.Count];
            var withoutEvidence = new bool[levelsFromMipZero.Count];
            for (var index = 0; index < levelsFromMipZero.Count; index++)
            {
                var level = levelsFromMipZero[index];
                if (level == null)
                {
                    throw new ArgumentNullException(
                        nameof(levelsFromMipZero),
                        "Mip level " + index + " is null.");
                }

                if (index > 0)
                {
                    var previous = levels[index - 1];
                    if (level.Width != Halved(previous.Width) ||
                        level.Height != Halved(previous.Height))
                    {
                        throw new ArgumentException(
                            "Mip level " + index + " must be " +
                            Halved(previous.Width) + "x" + Halved(previous.Height) +
                            "; each axis halves independently with a floor of one.",
                            nameof(levelsFromMipZero));
                    }
                }

                levels[index] = level;
                withoutEvidence[index] = levelsWithoutEvidenceFromMipZero[index];
            }

            _levels = levels;
            _levelsWithoutEvidence = withoutEvidence;
        }

        // The chaining argument of the evidence-default constructor: it must
        // reject a null list itself, because argument evaluation runs before
        // the chained constructor's own null check.
        private static bool[] AllEvidence(
            IReadOnlyList<AlphaTextureData> levelsFromMipZero)
        {
            if (levelsFromMipZero == null)
            {
                throw new ArgumentNullException(nameof(levelsFromMipZero));
            }

            return new bool[levelsFromMipZero.Count];
        }

        internal int Count => _levels.Length;

        internal AlphaTextureData this[int index] => _levels[index];

        /// <summary>
        /// Whether the capture could examine this level. A level without
        /// evidence - a non-resident consulted mip under the active mipmap
        /// limit - has no effective alpha for playback: the sampler may
        /// select it, so the fold degrades it to Unknown for every triangle
        /// and must never read the level's placeholder grid. The proof's
        /// geometric scope computations stay provenance-blind, because the
        /// flag says nothing about level dimensions.
        /// </summary>
        internal bool IsLevelWithoutEvidence(int index)
        {
            if (index < 0 || index >= _levels.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _levelsWithoutEvidence[index];
        }

        /// <summary>
        /// The prefix of this chain from mip 0 through
        /// <paramref name="maxMipLevel"/>, or this instance when the cap
        /// already covers the last level. A prefix is a valid chain: the
        /// constructor cannot prove completeness, and which levels the
        /// sampler may select is the provider contract's obligation. The
        /// user's "Preserve Transparency Maximum Mipmap" policy is exactly
        /// such a provider-side scope decision, so the proof over the
        /// prefix treats levels above the cap as outside the checked
        /// configuration set.
        /// </summary>
        internal AlphaMipChain LimitedTo(int maxMipLevel)
        {
            if (maxMipLevel < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxMipLevel),
                    "The mip cap must be a level index of at least zero.");
            }

            if (maxMipLevel >= _levels.Length - 1)
            {
                return this;
            }

            var prefix = new AlphaTextureData[maxMipLevel + 1];
            Array.Copy(_levels, prefix, prefix.Length);
            var prefixProvenance = new bool[prefix.Length];
            Array.Copy(_levelsWithoutEvidence, prefixProvenance,
                prefixProvenance.Length);
            return new AlphaMipChain(prefix, prefixProvenance);
        }

        /// <summary>
        /// The largest level index whose width and height are both at
        /// or above <paramref name="minimumSize"/>, or -1 when even
        /// mip 0 is smaller. Each level halves, so the qualifying
        /// levels always form a prefix of the chain and the returned
        /// index caps the proof's consulted prefix. This is the pure
        /// rule behind the user's "Preserve Transparency Minimum
        /// Texture Size" policy: the smaller dimension governs.
        /// </summary>
        internal int MaximumLevelAtOrAbove(int minimumSize)
        {
            if (minimumSize < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumSize),
                    "The minimum size must be at least one texel.");
            }

            var last = -1;
            for (var level = 0; level < _levels.Length; level++)
            {
                if (_levels[level].Width >= minimumSize &&
                    _levels[level].Height >= minimumSize)
                {
                    last = level;
                }
                else
                {
                    break;
                }
            }

            return last;
        }

        private static int Halved(int size)
        {
            var halved = size >> 1;
            return halved < 1 ? 1 : halved;
        }
    }
}
