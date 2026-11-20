using System;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// The sampler state of one atlas route texture as authored facts:
    /// the filter mode, the wrap mode of each axis, the anisotropy
    /// level, and the mip bias. A page copies all five facts from its
    /// group, so textures that borrow the main sampler keep sampling
    /// the way their material expects.
    /// <para>
    /// These are recorded facts, not proofs. The alpha proof type
    /// <see cref="TextureSampling"/> stays unchanged, because its
    /// single wrap field and its anisotropy eligibility are proof
    /// facts and a change to them would change alpha refusals.
    /// </para>
    /// </summary>
    internal readonly struct AtlasSamplerFacts : IEquatable<AtlasSamplerFacts>
    {
        internal FilterMode Filter { get; }

        /// <summary>The wrap mode Unity samples along U.</summary>
        internal UnityEngine.TextureWrapMode WrapU { get; }

        /// <summary>The wrap mode Unity samples along V.</summary>
        internal UnityEngine.TextureWrapMode WrapV { get; }

        /// <summary>
        /// The authored anisotropy level. Zero means the texture asks
        /// for none. The page keeps the number, so a quality setting
        /// that raises it later still sees the same authored request.
        /// </summary>
        internal int AnisoLevel { get; }

        /// <summary>The authored mip bias as an exact float.</summary>
        internal float MipBias { get; }

        internal AtlasSamplerFacts(
            FilterMode filter,
            UnityEngine.TextureWrapMode wrapU,
            UnityEngine.TextureWrapMode wrapV,
            int anisoLevel,
            float mipBias)
        {
            Filter = filter;
            WrapU = wrapU;
            WrapV = wrapV;
            AnisoLevel = anisoLevel;
            MipBias = mipBias;
        }

        public bool Equals(AtlasSamplerFacts other)
        {
            return Filter == other.Filter &&
                   WrapU == other.WrapU &&
                   WrapV == other.WrapV &&
                   AnisoLevel == other.AnisoLevel &&
                   MipBias == other.MipBias;
        }

        public override bool Equals(object obj)
        {
            return obj is AtlasSamplerFacts other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Filter;
                hash = (hash * 397) ^ (int)WrapU;
                hash = (hash * 397) ^ (int)WrapV;
                hash = (hash * 397) ^ AnisoLevel;
                hash = (hash * 397) ^ MipBias.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(
            AtlasSamplerFacts left,
            AtlasSamplerFacts right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            AtlasSamplerFacts left,
            AtlasSamplerFacts right)
        {
            return !left.Equals(right);
        }
    }
}
