using System;
using System.Collections.Generic;

namespace Alrauna.Amuse.Editor.Analysis
{
    internal enum SubmeshSeparationDisposition
    {
        Unchanged,
        WhollyOpaqueCandidate,
        Split
    }

    internal sealed class SubmeshSeparationInput
    {
        internal int SourceMaterialBindingIndex { get; }
        internal IReadOnlyList<int> Indices { get; }
        internal IReadOnlyList<TriangleAlphaOutcome> Outcomes { get; }
        internal int TriangleCount { get; }

        internal SubmeshSeparationInput(
            int sourceMaterialBindingIndex,
            IReadOnlyList<int> indices,
            IReadOnlyList<TriangleAlphaOutcome> outcomes)
        {
            if (sourceMaterialBindingIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceMaterialBindingIndex));
            if (indices == null)
                throw new ArgumentNullException(nameof(indices));
            if (outcomes == null)
                throw new ArgumentNullException(nameof(outcomes));
            if (indices.Count % 3 != 0)
            {
                throw new ArgumentException(
                    "Triangle groups require complete index triples.",
                    nameof(indices));
            }
            if (outcomes.Count != indices.Count / 3)
            {
                throw new ArgumentException(
                    "Outcome count must equal triangle count.",
                    nameof(outcomes));
            }

            var copiedIndices = new int[indices.Count];
            for (var index = 0; index < indices.Count; index++)
                copiedIndices[index] = indices[index];

            var copiedOutcomes = new TriangleAlphaOutcome[outcomes.Count];
            for (var index = 0; index < outcomes.Count; index++)
            {
                if (!Enum.IsDefined(typeof(TriangleAlphaOutcome), outcomes[index]))
                    throw new ArgumentOutOfRangeException(nameof(outcomes));

                copiedOutcomes[index] = outcomes[index];
            }

            SourceMaterialBindingIndex = sourceMaterialBindingIndex;
            Indices = Array.AsReadOnly(copiedIndices);
            Outcomes = Array.AsReadOnly(copiedOutcomes);
            TriangleCount = copiedIndices.Length / 3;
        }
    }

    internal sealed class MeshSeparationInput
    {
        internal int VertexCount { get; }
        internal IReadOnlyList<SubmeshSeparationInput> Submeshes { get; }

        internal MeshSeparationInput(
            int vertexCount,
            IReadOnlyList<SubmeshSeparationInput> submeshes)
        {
            if (vertexCount < 0)
                throw new ArgumentOutOfRangeException(nameof(vertexCount));
            if (submeshes == null)
                throw new ArgumentNullException(nameof(submeshes));

            var copiedSubmeshes = new SubmeshSeparationInput[submeshes.Count];
            for (var submeshIndex = 0; submeshIndex < submeshes.Count; submeshIndex++)
            {
                var submesh = submeshes[submeshIndex];
                if (submesh == null)
                    throw new ArgumentNullException(nameof(submeshes));

                for (var index = 0; index < submesh.Indices.Count; index++)
                {
                    var vertexIndex = submesh.Indices[index];
                    if (vertexIndex < 0 || vertexIndex >= vertexCount)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(submeshes),
                            "Every source index must reference an existing vertex.");
                    }
                }

                copiedSubmeshes[submeshIndex] = submesh;
            }

            VertexCount = vertexCount;
            Submeshes = Array.AsReadOnly(copiedSubmeshes);
        }
    }

    internal sealed class SubmeshSeparationPlan
    {
        internal int SourceSubmeshIndex { get; }
        internal int SourceMaterialBindingIndex { get; }
        internal IReadOnlyList<int> OpaqueTriangleOrdinals { get; }
        internal IReadOnlyList<int> TransparentTriangleOrdinals { get; }
        internal SubmeshSeparationDisposition Disposition { get; }

        internal SubmeshSeparationPlan(
            int sourceSubmeshIndex,
            int sourceMaterialBindingIndex,
            IReadOnlyList<int> opaqueTriangleOrdinals,
            IReadOnlyList<int> transparentTriangleOrdinals,
            SubmeshSeparationDisposition disposition)
        {
            SourceSubmeshIndex = sourceSubmeshIndex;
            SourceMaterialBindingIndex = sourceMaterialBindingIndex;
            OpaqueTriangleOrdinals = Copy(opaqueTriangleOrdinals);
            TransparentTriangleOrdinals = Copy(transparentTriangleOrdinals);
            Disposition = disposition;
        }

        private static IReadOnlyList<int> Copy(IReadOnlyList<int> source)
        {
            var copy = new int[source.Count];
            for (var index = 0; index < source.Count; index++)
                copy[index] = source[index];

            return Array.AsReadOnly(copy);
        }
    }

    internal sealed class MeshSeparationPlan
    {
        internal MeshSeparationInput Source { get; }
        internal IReadOnlyList<SubmeshSeparationPlan> Submeshes { get; }
        internal bool HasAnyOpaqueCandidates { get; }
        internal bool RequiresAnySplit { get; }
        internal int OpaqueTriangleCount { get; }
        internal int TransparentTriangleCount { get; }

        internal MeshSeparationPlan(
            MeshSeparationInput source,
            IReadOnlyList<SubmeshSeparationPlan> submeshes,
            bool hasAnyOpaqueCandidates,
            bool requiresAnySplit,
            int opaqueTriangleCount,
            int transparentTriangleCount)
        {
            var copiedSubmeshes = new SubmeshSeparationPlan[submeshes.Count];
            for (var index = 0; index < submeshes.Count; index++)
                copiedSubmeshes[index] = submeshes[index];

            Source = source;
            Submeshes = Array.AsReadOnly(copiedSubmeshes);
            HasAnyOpaqueCandidates = hasAnyOpaqueCandidates;
            RequiresAnySplit = requiresAnySplit;
            OpaqueTriangleCount = opaqueTriangleCount;
            TransparentTriangleCount = transparentTriangleCount;
        }
    }

    internal static class MeshSeparationPlanner
    {
        internal static MeshSeparationPlan Create(MeshSeparationInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            var submeshPlans = new List<SubmeshSeparationPlan>(input.Submeshes.Count);
            var opaqueTriangleCount = 0;
            var transparentTriangleCount = 0;
            var requiresAnySplit = false;
            for (var sourceSubmeshIndex = 0;
                 sourceSubmeshIndex < input.Submeshes.Count;
                 sourceSubmeshIndex++)
            {
                var sourceSubmesh = input.Submeshes[sourceSubmeshIndex];
                var opaqueOrdinals = new List<int>(sourceSubmesh.TriangleCount);
                var transparentOrdinals = new List<int>(sourceSubmesh.TriangleCount);
                for (var triangleOrdinal = 0;
                     triangleOrdinal < sourceSubmesh.TriangleCount;
                     triangleOrdinal++)
                {
                    var outcome = sourceSubmesh.Outcomes[triangleOrdinal];
                    if (outcome == TriangleAlphaOutcome.ProvenOpaque)
                        opaqueOrdinals.Add(triangleOrdinal);
                    else
                        transparentOrdinals.Add(triangleOrdinal);
                }

                var disposition = opaqueOrdinals.Count == 0
                    ? SubmeshSeparationDisposition.Unchanged
                    : transparentOrdinals.Count == 0
                        ? SubmeshSeparationDisposition.WhollyOpaqueCandidate
                        : SubmeshSeparationDisposition.Split;
                submeshPlans.Add(new SubmeshSeparationPlan(
                    sourceSubmeshIndex,
                    sourceSubmesh.SourceMaterialBindingIndex,
                    opaqueOrdinals,
                    transparentOrdinals,
                    disposition));
                opaqueTriangleCount += opaqueOrdinals.Count;
                transparentTriangleCount += transparentOrdinals.Count;
                requiresAnySplit |= disposition == SubmeshSeparationDisposition.Split;
            }

            return new MeshSeparationPlan(
                input,
                submeshPlans,
                opaqueTriangleCount > 0,
                requiresAnySplit,
                opaqueTriangleCount,
                transparentTriangleCount);
        }
    }
}
