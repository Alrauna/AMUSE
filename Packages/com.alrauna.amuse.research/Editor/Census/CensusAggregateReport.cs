using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Alrauna.Amuse.Research.Census
{
    /// <summary>
    /// Tier 3: the only tier meant to be published.
    /// <para>
    /// Distributions only. There is deliberately no per-avatar, per-renderer,
    /// or per-material row anywhere in this type, because an exact renderer or
    /// triangle count is a strong fingerprint for anyone already holding the
    /// avatar. Avatar-level variation appears as a count against a stated
    /// denominator and never as a list.
    /// </para>
    /// <para>
    /// It carries no numeric buckets, histograms, percentiles, or ranges.
    /// Bucket boundaries must be chosen against real distributions under
    /// privacy review, not invented before any distribution is known, and a
    /// bucket narrow enough to hold one avatar identifies it regardless of the
    /// label on it.
    /// </para>
    /// </summary>
    public sealed class CensusAggregateReport
    {
        public int AvatarCount { get; }
        public int RendererCount { get; }

        /// <summary>
        /// Submeshes AMUSE actually analyzed. Distinct from
        /// <see cref="TotalRendererSubmeshCount"/>, which includes the
        /// submeshes of refused renderers that carry no analysis.
        /// </summary>
        public int SubmeshRecordCount { get; }

        /// <summary>
        /// Materials counted distinctly within each avatar and then summed.
        /// Identity is avatar-scoped by design, so a corpus-wide distinct total
        /// is unavailable — recording cross-avatar sharing would disclose
        /// purchased-asset patterns and, through them, creators.
        /// </summary>
        public int DistinctMaterialCount { get; }

        public IReadOnlyDictionary<RendererRefusal, int> RendererCountByRefusal { get; }
        public IReadOnlyDictionary<RendererKind, int> RendererCountByKind { get; }

        public int RenderersWithKnownTriangleCount { get; }
        public int RenderersWithUnknownTriangleCount { get; }

        /// <summary>
        /// All geometry the census could see, whether or not AMUSE classified
        /// it. Null when no renderer had a known count: the denominator is
        /// unavailable, so there is no honest total, and zero would be a
        /// different and false claim.
        /// </summary>
        public long? TotalRendererTriangleCount { get; }

        public int RenderersWithKnownSubmeshCount { get; }
        public int RenderersWithUnknownSubmeshCount { get; }

        /// <summary>Null when no renderer had a known count. See above.</summary>
        public long? TotalRendererSubmeshCount { get; }

        public IReadOnlyDictionary<AlphaResolutionFailure, int>
            SubmeshCountByAlphaFailure { get; }

        public IReadOnlyDictionary<AlphaResolutionFailure, long>
            TriangleCountByAlphaFailure { get; }

        public IReadOnlyDictionary<SeparationDisposition, int>
            SubmeshCountByDisposition { get; }

        public IReadOnlyDictionary<SeparationDisposition, long>
            TriangleCountByDisposition { get; }

        /// <summary>
        /// Keyed by shader family. Unlike the enum-keyed distributions, these
        /// keys come from observation, so an unobserved family has no row
        /// rather than a row of zeroes.
        /// </summary>
        public IReadOnlyDictionary<string, int> DistinctMaterialCountByShaderFamily { get; }

        public IReadOnlyDictionary<string, long> TriangleCountByShaderFamily { get; }

        public long ProvenOpaqueTriangleCount { get; }
        public long MustRemainTransparentTriangleCount { get; }
        public long UnknownTriangleCount { get; }

        /// <summary>
        /// Triangles AMUSE classified, and therefore the honest denominator for
        /// the three counts above. The gap between this and
        /// <see cref="TotalRendererTriangleCount"/> is the coverage story:
        /// refused renderers contribute geometry to that total and nothing
        /// here.
        /// </summary>
        public long ClassifiedTriangleCount { get; }

        /// <summary>
        /// Submeshes AMUSE resolved without failure that still produced unknown
        /// triangles. AMUSE records no reason for those, so the census cannot
        /// explain them; measuring how large the gap is lets AMUSE decide on
        /// its own merits whether recording reasons is worth it. The census
        /// does not ask production analysis to change so that it can measure
        /// more.
        /// </summary>
        public int UnknownBlindSpotSubmeshCount { get; }

        public long UnknownBlindSpotTriangleCount { get; }

        public int SubmeshesWithoutMaterialCount { get; }

        /// <summary>
        /// Avatars with at least one triangle AMUSE proved opaque, against
        /// <see cref="AvatarCount"/>. A count, never a list.
        /// </summary>
        public int AvatarsWithAtLeastOneOpaqueCandidate { get; }

        public CensusAggregateReport(
            int avatarCount,
            int rendererCount,
            int submeshRecordCount,
            int distinctMaterialCount,
            IReadOnlyDictionary<RendererRefusal, int> rendererCountByRefusal,
            IReadOnlyDictionary<RendererKind, int> rendererCountByKind,
            int renderersWithKnownTriangleCount,
            int renderersWithUnknownTriangleCount,
            long? totalRendererTriangleCount,
            int renderersWithKnownSubmeshCount,
            int renderersWithUnknownSubmeshCount,
            long? totalRendererSubmeshCount,
            IReadOnlyDictionary<AlphaResolutionFailure, int> submeshCountByAlphaFailure,
            IReadOnlyDictionary<AlphaResolutionFailure, long> triangleCountByAlphaFailure,
            IReadOnlyDictionary<SeparationDisposition, int> submeshCountByDisposition,
            IReadOnlyDictionary<SeparationDisposition, long> triangleCountByDisposition,
            IReadOnlyDictionary<string, int> distinctMaterialCountByShaderFamily,
            IReadOnlyDictionary<string, long> triangleCountByShaderFamily,
            long provenOpaqueTriangleCount,
            long mustRemainTransparentTriangleCount,
            long unknownTriangleCount,
            int unknownBlindSpotSubmeshCount,
            long unknownBlindSpotTriangleCount,
            int submeshesWithoutMaterialCount,
            int avatarsWithAtLeastOneOpaqueCandidate)
        {
            AvatarCount = avatarCount;
            RendererCount = rendererCount;
            SubmeshRecordCount = submeshRecordCount;
            DistinctMaterialCount = distinctMaterialCount;
            RendererCountByRefusal = Freeze(rendererCountByRefusal);
            RendererCountByKind = Freeze(rendererCountByKind);
            RenderersWithKnownTriangleCount = renderersWithKnownTriangleCount;
            RenderersWithUnknownTriangleCount = renderersWithUnknownTriangleCount;
            TotalRendererTriangleCount = totalRendererTriangleCount;
            RenderersWithKnownSubmeshCount = renderersWithKnownSubmeshCount;
            RenderersWithUnknownSubmeshCount = renderersWithUnknownSubmeshCount;
            TotalRendererSubmeshCount = totalRendererSubmeshCount;
            SubmeshCountByAlphaFailure = Freeze(submeshCountByAlphaFailure);
            TriangleCountByAlphaFailure = Freeze(triangleCountByAlphaFailure);
            SubmeshCountByDisposition = Freeze(submeshCountByDisposition);
            TriangleCountByDisposition = Freeze(triangleCountByDisposition);
            DistinctMaterialCountByShaderFamily =
                Freeze(distinctMaterialCountByShaderFamily);
            TriangleCountByShaderFamily = Freeze(triangleCountByShaderFamily);
            ProvenOpaqueTriangleCount = provenOpaqueTriangleCount;
            MustRemainTransparentTriangleCount = mustRemainTransparentTriangleCount;
            UnknownTriangleCount = unknownTriangleCount;
            ClassifiedTriangleCount =
                provenOpaqueTriangleCount
                + mustRemainTransparentTriangleCount
                + unknownTriangleCount;
            UnknownBlindSpotSubmeshCount = unknownBlindSpotSubmeshCount;
            UnknownBlindSpotTriangleCount = unknownBlindSpotTriangleCount;
            SubmeshesWithoutMaterialCount = submeshesWithoutMaterialCount;
            AvatarsWithAtLeastOneOpaqueCandidate = avatarsWithAtLeastOneOpaqueCandidate;
        }

        private static IReadOnlyDictionary<TKey, TValue> Freeze<TKey, TValue>(
            IReadOnlyDictionary<TKey, TValue> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var copied = new Dictionary<TKey, TValue>(source.Count);
            foreach (var entry in source)
                copied.Add(entry.Key, entry.Value);

            return new ReadOnlyDictionary<TKey, TValue>(copied);
        }
    }
}
