using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using UnityEditor;
using UnityEngine;
using Census = Alrauna.Amuse.Research.Census;

namespace Alrauna.Amuse.Research.Collection
{
    /// <summary>
    /// One Unity renderer to one tier 1 <c>ObservedRenderer</c>.
    /// <para>
    /// Every count on the analyzed path comes from the plan AMUSE returned;
    /// nothing is recomputed from geometry. The only Unity state read beyond
    /// what <c>Analyze</c> reads is the shared mesh, for the counts a refused
    /// renderer would otherwise lose, and the shared materials, for tier 1
    /// identity.
    /// </para>
    /// <para>
    /// It never catches an analysis exception. <c>Analyze</c> throws only for a
    /// null or destroyed renderer, neither of which hierarchy traversal can
    /// produce, so an exception means a collector defect - and a census that
    /// records its own defects as data produces a confident wrong number.
    /// </para>
    /// </summary>
    internal static class RendererObservationBuilder
    {
        /// <summary>The production path.</summary>
        internal static Census.ObservedRenderer Build(
            Renderer renderer,
            string hierarchyPath,
            CensusShaderFamily families)
        {
            return Build(renderer, hierarchyPath, families, null);
        }

        /// <summary>
        /// The same observation, with AMUSE's own semantics seam substituted.
        /// <para>
        /// This overload exists because the public development project installs
        /// no vendor shader, so <c>ProvenOpaque</c> and
        /// <c>MissingTextureEvidence</c> are otherwise unreachable in CI and the
        /// collector's counting of them could not be validated at all. It is a
        /// straight pass-through to the two-overload shape
        /// <c>UnityRendererAlphaAnalysis.Analyze</c> already ships for its own
        /// integration tests - not a new extension point. It is internal, no
        /// public caller can name it, and nothing in the collector's own public
        /// surface carries a provider parameter.
        /// </para>
        /// </summary>
        internal static Census.ObservedRenderer Build(
            Renderer renderer,
            string hierarchyPath,
            CensusShaderFamily families,
            BaseMaterialSemanticsProvider semanticsProvider)
        {
            var analysis = semanticsProvider == null
                ? UnityRendererAlphaAnalysis.Analyze(renderer)
                : UnityRendererAlphaAnalysis.Analyze(
                    renderer, semanticsProvider);

            var kind = CensusVocabulary.KindOf(renderer);
            var refusal = CensusVocabulary.ToCensus(analysis.Refusal);

            if (analysis.Refusal != RendererAnalysisRefusal.None)
            {
                CountRefusedMesh(
                    SharedMeshOf(renderer),
                    out var submeshCount,
                    out var triangleCount);

                return new Census.ObservedRenderer(
                    hierarchyPath,
                    renderer.gameObject.name,
                    renderer.GetType().Name,
                    kind,
                    refusal,
                    submeshCount,
                    triangleCount,
                    Array.Empty<Census.ObservedSubmesh>());
            }

            var plan = analysis.Plan;

            // Index-parallel by construction. Asserted rather than assumed,
            // because every count below indexes all three.
            if (plan.Submeshes.Count != analysis.Submeshes.Count ||
                plan.Source.Submeshes.Count != analysis.Submeshes.Count)
            {
                throw new InvalidOperationException(
                    "AMUSE returned mismatched submesh lists; the census cannot "
                    + "align them without guessing.");
            }

            var materials = renderer.sharedMaterials;
            var submeshes = new List<Census.ObservedSubmesh>(
                analysis.Submeshes.Count);
            var totalTriangles = 0;
            var totalOpaque = 0;
            var totalNonOpaque = 0;

            for (var index = 0; index < analysis.Submeshes.Count; index++)
            {
                var record = analysis.Submeshes[index];
                var outcomes = plan.Source.Submeshes[index].Outcomes;

                var opaque = 0;
                var transparent = 0;
                var unknown = 0;
                for (var i = 0; i < outcomes.Count; i++)
                {
                    switch (outcomes[i])
                    {
                        case TriangleAlphaOutcome.ProvenOpaque:
                            opaque++;
                            break;
                        case TriangleAlphaOutcome.MustRemainTransparent:
                            transparent++;
                            break;
                        case TriangleAlphaOutcome.Unknown:
                            unknown++;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(renderer),
                                "Unmapped AMUSE triangle outcome: "
                                + outcomes[i]);
                    }
                }

                var material =
                    record.MaterialSlotIndex >= 0 &&
                    record.MaterialSlotIndex < materials.Length
                        ? materials[record.MaterialSlotIndex]
                        : null;

                submeshes.Add(new Census.ObservedSubmesh(
                    record.SubmeshIndex,
                    record.MaterialSlotIndex,
                    record.HasMaterial,
                    material == null ? null : material.name,
                    CensusAssetIdentity.PathOf(material),
                    CensusAssetIdentity.GuidOf(material),
                    material == null || material.shader == null
                        ? null
                        : material.shader.name,
                    families.Of(material),
                    CensusVocabulary.ToCensus(record.Failure),
                    CensusVocabulary.ToCensus(plan.Submeshes[index].Disposition),
                    outcomes.Count,
                    opaque,
                    transparent,
                    unknown));

                totalTriangles += outcomes.Count;
                totalOpaque += opaque;
                totalNonOpaque += transparent + unknown;
            }

            // The load-bearing invariant: the census's own tally, derived from
            // per-triangle outcomes, against a number MeshSeparationPlanner
            // computed independently. A misattribution bug cannot agree with
            // itself across both. Note the asymmetry - AMUSE's transparent count
            // is everything that is not ProvenOpaque, so Unknown is on that side.
            if (totalOpaque != plan.OpaqueTriangleCount ||
                totalNonOpaque != plan.TransparentTriangleCount)
            {
                throw new InvalidOperationException(
                    "Census triangle tally disagrees with the AMUSE separation "
                    + "plan: counted " + totalOpaque + " opaque and "
                    + totalNonOpaque + " non-opaque against the plan's "
                    + plan.OpaqueTriangleCount + " and "
                    + plan.TransparentTriangleCount + ".");
            }

            return new Census.ObservedRenderer(
                hierarchyPath,
                renderer.gameObject.name,
                renderer.GetType().Name,
                kind,
                refusal,
                submeshes.Count,
                totalTriangles,
                submeshes);
        }

        /// <summary>
        /// The counts a refused renderer would otherwise lose. Unknown is
        /// recorded as null and never as zero: zero understates avatar
        /// complexity and overstates coverage in every aggregate downstream.
        /// <para>
        /// A non-triangle submesh has no triangle count, so a mesh containing
        /// one yields a known submesh count and an unknown triangle count rather
        /// than an invented number.
        /// </para>
        /// </summary>
        private static void CountRefusedMesh(
            Mesh mesh, out int? submeshCount, out int? triangleCount)
        {
            if (mesh == null)
            {
                submeshCount = null;
                triangleCount = null;
                return;
            }

            submeshCount = mesh.subMeshCount;

            var triangles = 0;
            for (var index = 0; index < mesh.subMeshCount; index++)
            {
                if (mesh.GetTopology(index) != MeshTopology.Triangles)
                {
                    triangleCount = null;
                    return;
                }

                // GetIndexCount rather than GetIndices: the count is all that is
                // wanted and GetIndices would allocate the whole index buffer.
                triangles += (int)(mesh.GetIndexCount(index) / 3);
            }

            triangleCount = triangles;
        }

        /// <summary>
        /// The one mesh a renderer contributes, reached exactly as AMUSE reaches
        /// it. Never through the instantiating <c>MeshFilter</c> property, which
        /// silently creates a copy as a side effect of being read.
        /// </summary>
        private static Mesh SharedMeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            var filter = renderer.GetComponent<MeshFilter>();
            return filter == null ? null : filter.sharedMesh;
        }
    }

    /// <summary>
    /// Tier 1 asset identity. Only two AssetDatabase members are used and both
    /// are pure reads that import nothing, create nothing, and dirty nothing -
    /// the narrowed form of the harness's banned-API rule, enforced by
    /// <c>ResearchSourceApiBanTests</c>.
    /// <para>
    /// A runtime-constructed or embedded object has no asset path; Unity returns
    /// an empty string there and this normalizes it to null, so a missing
    /// identity reads as missing rather than as an empty asset.
    /// </para>
    /// </summary>
    internal static class CensusAssetIdentity
    {
        internal static string PathOf(UnityEngine.Object target)
        {
            if (target == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(target);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        internal static string GuidOf(UnityEngine.Object target)
        {
            var path = PathOf(target);
            if (path == null)
            {
                return null;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? null : guid;
        }
    }
}
