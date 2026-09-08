using System;
using System.Collections.Generic;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// One texture's ordered alpha mip chain: the existing per-level grids the
    /// classifier already consumes, mip 0 first.
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

        internal AlphaMipChain(IReadOnlyList<AlphaTextureData> levelsFromMipZero)
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

            var levels = new AlphaTextureData[levelsFromMipZero.Count];
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
            }

            _levels = levels;
        }

        internal int Count => _levels.Length;

        internal AlphaTextureData this[int index] => _levels[index];

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
            return new AlphaMipChain(prefix);
        }

        private static int Halved(int size)
        {
            var halved = size >> 1;
            return halved < 1 ? 1 : halved;
        }
    }
}
