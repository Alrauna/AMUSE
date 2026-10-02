using System;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>The outcome of the shared distance fade strength rule.
    /// </summary>
    internal enum LilToonDistanceFadeAnswer
    {
        /// <summary>The shader carries no such property.</summary>
        Absent,

        /// <summary>Finite vector with a zero strength: exact no-op.</summary>
        Inert,

        /// <summary>Finite vector with a nonzero strength.</summary>
        Retained,

        /// <summary>Any non-finite component.</summary>
        NonFinite,
    }

    /// <summary>
    /// The shared distance fade strength rule for every lilToon donor
    /// family. The vendor fragment block scales every arm by
    /// <c>_DistanceFade.z</c> (lil_common_frag.hlsl:2017-2064 at tag
    /// 2.3.4), so a finite zero strength is an exact no-op and a finite
    /// nonzero strength retains the material: the color arm runs at
    /// every render mode, so a moved triangle would lose its fade at
    /// some camera distance. A non-finite component is a broken capture
    /// and keeps the non-finite vocabulary.
    /// </summary>
    internal static class LilToonDistanceFadeSemantics
    {
        internal const string DistanceFadeProperty = "_DistanceFade";

        internal static LilToonDistanceFadeAnswer Evaluate(
            CapturedMaterialEvidence evidence)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            if (!evidence.TryGetVector(DistanceFadeProperty, out var fade))
            {
                return LilToonDistanceFadeAnswer.Absent;
            }

            if (float.IsNaN(fade.x) || float.IsInfinity(fade.x) ||
                float.IsNaN(fade.y) || float.IsInfinity(fade.y) ||
                float.IsNaN(fade.z) || float.IsInfinity(fade.z) ||
                float.IsNaN(fade.w) || float.IsInfinity(fade.w))
            {
                return LilToonDistanceFadeAnswer.NonFinite;
            }

            return fade.z != 0f
                ? LilToonDistanceFadeAnswer.Retained
                : LilToonDistanceFadeAnswer.Inert;
        }
    }
}
