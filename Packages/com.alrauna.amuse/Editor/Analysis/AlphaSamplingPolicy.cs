using System;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// The closed proof policies a build can select. A policy is not
    /// captured evidence. It says which captured sampling facts a proof
    /// may use. ConservativeDesktop trusts the documented authored
    /// level-zero exception. ConservativeMobile does not, because the
    /// mobile client override of level zero is unresolved. Accepted
    /// NonAnisotropic drops every anisotropic footprint after explicit
    /// user acceptance of possible filtering differences.
    /// </summary>
    internal enum AlphaSamplingPolicy
    {
        ConservativeDesktop = 0,
        ConservativeMobile = 1,
        AcceptedNonAnisotropic = 2
    }

    /// <summary>
    /// Value-only projection from captured authored sampling to the
    /// effective proof sampling for one policy. The projection never
    /// changes captured evidence.
    /// </summary>
    internal static class AlphaSamplingPolicyProjection
    {
        /// <summary>
        /// Projects captured authored sampling into the effective proof
        /// sampling for this policy. The projection keeps filter and wrap
        /// and rewrites only anisotropy. The result is effective proof
        /// sampling, not authored capture, so its own eligibility keeps
        /// the default true because no downstream capture reads this
        /// projection.
        /// </summary>
        internal static TextureSampling ForProof(
            this AlphaSamplingPolicy policy, TextureSampling authored)
        {
            TextureAnisoMode aniso;
            switch (policy)
            {
                case AlphaSamplingPolicy.ConservativeDesktop:
                    aniso = authored.ForceAnisotropyEligible
                        ? TextureAnisoMode.Anisotropic
                        : TextureAnisoMode.None;
                    break;
                case AlphaSamplingPolicy.ConservativeMobile:
                    aniso = TextureAnisoMode.Anisotropic;
                    break;
                case AlphaSamplingPolicy.AcceptedNonAnisotropic:
                    aniso = TextureAnisoMode.None;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(policy));
            }
            return new TextureSampling(authored.Filter, authored.Wrap, aniso);
        }
    }
}
