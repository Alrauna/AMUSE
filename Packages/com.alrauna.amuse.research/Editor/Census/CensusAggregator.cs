using System;
using System.Collections.Generic;

namespace Alrauna.Amuse.Research.Census
{
    /// <summary>
    /// Stage three of the census: a pure function from anonymized records to a
    /// publishable aggregate.
    /// <para>
    /// It runs on tier 2 only. It knows nothing of Unity, of AMUSE types, of
    /// renderers, materials, or shaders as objects — only of the categories
    /// tier 2 records and the numbers attached to them. That is what lets the
    /// counting be checked independently of everything that produced it.
    /// </para>
    /// </summary>
    public static class CensusAggregator
    {
        public static CensusAggregateReport Aggregate(AnonymizedCensus census)
        {
            if (census == null)
                throw new ArgumentNullException(nameof(census));

            var refusals = EmptyCounts<RendererRefusal, int>();
            var kinds = EmptyCounts<RendererKind, int>();
            var submeshesByFailure = EmptyCounts<AlphaResolutionFailure, int>();
            var trianglesByFailure = EmptyCounts<AlphaResolutionFailure, long>();
            var submeshesByDisposition = EmptyCounts<SeparationDisposition, int>();
            var trianglesByDisposition = EmptyCounts<SeparationDisposition, long>();

            // Family keys come from observation rather than a fixed vocabulary,
            // so an unobserved family gets no row instead of a row of zeroes.
            var materialsByFamily = new Dictionary<string, int>();
            var trianglesByFamily = new Dictionary<string, long>();

            var rendererCount = 0;
            var submeshRecordCount = 0;
            var distinctMaterialCount = 0;
            var submeshesWithoutMaterial = 0;

            var renderersWithKnownTriangles = 0;
            var renderersWithUnknownTriangles = 0;
            long totalTriangles = 0;

            var renderersWithKnownSubmeshes = 0;
            var renderersWithUnknownSubmeshes = 0;
            long totalSubmeshes = 0;

            long provenOpaque = 0;
            long mustRemainTransparent = 0;
            long unknown = 0;

            var blindSpotSubmeshes = 0;
            long blindSpotTriangles = 0;

            var avatarsWithOpaqueCandidate = 0;

            foreach (var avatar in census.Avatars)
            {
                // Material identity is avatar-scoped, so distinctness is only
                // meaningful within an avatar. Counting across the corpus would
                // require the cross-avatar identity the anonymizer withholds.
                var materialsInAvatar = new List<string>();
                var avatarHasOpaqueCandidate = false;

                foreach (var renderer in avatar.Renderers)
                {
                    rendererCount++;
                    refusals[renderer.Refusal]++;
                    kinds[renderer.Kind]++;

                    if (renderer.TriangleCount.HasValue)
                    {
                        renderersWithKnownTriangles++;
                        totalTriangles += renderer.TriangleCount.Value;
                    }
                    else
                    {
                        renderersWithUnknownTriangles++;
                    }

                    if (renderer.SubmeshCount.HasValue)
                    {
                        renderersWithKnownSubmeshes++;
                        totalSubmeshes += renderer.SubmeshCount.Value;
                    }
                    else
                    {
                        renderersWithUnknownSubmeshes++;
                    }

                    foreach (var submesh in renderer.Submeshes)
                    {
                        submeshRecordCount++;

                        submeshesByFailure[submesh.AlphaFailure]++;
                        trianglesByFailure[submesh.AlphaFailure] += submesh.TriangleCount;
                        submeshesByDisposition[submesh.Disposition]++;
                        trianglesByDisposition[submesh.Disposition] += submesh.TriangleCount;

                        provenOpaque += submesh.ProvenOpaqueTriangleCount;
                        mustRemainTransparent += submesh.MustRemainTransparentTriangleCount;
                        unknown += submesh.UnknownTriangleCount;

                        if (submesh.ProvenOpaqueTriangleCount > 0)
                            avatarHasOpaqueCandidate = true;

                        // Unknown triangles on a submesh that resolved without
                        // failure: AMUSE records no reason for these anywhere,
                        // so all the census can honestly report is how many
                        // there are.
                        if (submesh.AlphaFailure == AlphaResolutionFailure.None
                            && submesh.UnknownTriangleCount > 0)
                        {
                            blindSpotSubmeshes++;
                            blindSpotTriangles += submesh.UnknownTriangleCount;
                        }

                        if (!submesh.HasMaterial)
                        {
                            submeshesWithoutMaterial++;
                            continue;
                        }

                        var family = submesh.ShaderFamily;
                        Add(trianglesByFamily, family, submesh.TriangleCount);

                        if (!materialsInAvatar.Contains(submesh.MaterialId))
                        {
                            materialsInAvatar.Add(submesh.MaterialId);
                            distinctMaterialCount++;
                            Add(materialsByFamily, family, 1);
                        }
                    }
                }

                if (avatarHasOpaqueCandidate)
                    avatarsWithOpaqueCandidate++;
            }

            return new CensusAggregateReport(
                census.Avatars.Count,
                rendererCount,
                submeshRecordCount,
                distinctMaterialCount,
                refusals,
                kinds,
                renderersWithKnownTriangles,
                renderersWithUnknownTriangles,
                // No renderer with a known count means no denominator, and
                // therefore no total. Zero would claim something different.
                renderersWithKnownTriangles == 0 ? (long?)null : totalTriangles,
                renderersWithKnownSubmeshes,
                renderersWithUnknownSubmeshes,
                renderersWithKnownSubmeshes == 0 ? (long?)null : totalSubmeshes,
                submeshesByFailure,
                trianglesByFailure,
                submeshesByDisposition,
                trianglesByDisposition,
                materialsByFamily,
                trianglesByFamily,
                provenOpaque,
                mustRemainTransparent,
                unknown,
                blindSpotSubmeshes,
                blindSpotTriangles,
                submeshesWithoutMaterial,
                avatarsWithOpaqueCandidate);
        }

        /// <summary>
        /// Every member of the category, at zero. An observed zero is a
        /// measurement rather than missing information, and a stable key set
        /// keeps reports comparable between runs.
        /// </summary>
        private static Dictionary<TKey, TValue> EmptyCounts<TKey, TValue>()
            where TKey : struct, Enum
            where TValue : struct
        {
            var counts = new Dictionary<TKey, TValue>();
            foreach (TKey value in Enum.GetValues(typeof(TKey)))
                counts[value] = default;

            return counts;
        }

        private static void Add(Dictionary<string, int> counts, string key, int amount)
        {
            counts[key] = counts.TryGetValue(key, out var existing)
                ? existing + amount
                : amount;
        }

        private static void Add(Dictionary<string, long> counts, string key, long amount)
        {
            counts[key] = counts.TryGetValue(key, out var existing)
                ? existing + amount
                : amount;
        }
    }
}
