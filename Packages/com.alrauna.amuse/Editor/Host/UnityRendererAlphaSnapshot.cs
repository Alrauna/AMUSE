using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    internal sealed class UnityRendererAlphaSnapshot
    {
        internal int VertexCount { get; }
        internal IReadOnlyList<Vector3> Positions { get; }
        internal IReadOnlyList<Vector2> Uv0 { get; }
        internal bool HasUv0 { get; }

        /// <summary>
        /// The layer UV channels by HLSL uv set: index 0 is uv1 (Unity's
        /// mesh.uv2), index 1 is uv2, index 2 is uv3. A null entry means the
        /// channel is absent from the mesh, so a layer naming it cannot be
        /// sampled and stays unproven.
        /// </summary>
        internal IReadOnlyList<IReadOnlyList<Vector2>> ExtraUvSets { get; }
        internal IReadOnlyList<UnitySubmeshAlphaSnapshot> Submeshes { get; }
        internal IReadOnlyList<CapturedAlphaMaterial> Materials { get; }

        internal UnityRendererAlphaSnapshot(
            int vertexCount,
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<Vector2> uv0,
            bool hasUv0,
            IReadOnlyList<UnitySubmeshAlphaSnapshot> submeshes,
            IReadOnlyList<CapturedAlphaMaterial> materials,
            IReadOnlyList<IReadOnlyList<Vector2>> extraUvSets = null)
        {
            VertexCount = vertexCount;
            Positions = Copy(positions);
            Uv0 = Copy(uv0);
            HasUv0 = hasUv0;
            var sets = extraUvSets == null
                ? new List<IReadOnlyList<Vector2>> { null, null, null }
                : new List<IReadOnlyList<Vector2>>(extraUvSets);
            while (sets.Count < 3)
            {
                sets.Add(null);
            }

            ExtraUvSets = sets.AsReadOnly();
            Submeshes = Copy(submeshes);
            Materials = Copy(materials);
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new T[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }

    internal sealed class UnitySubmeshAlphaSnapshot
    {
        internal int SubmeshIndex { get; }
        internal int MaterialSlotIndex { get; }
        internal IReadOnlyList<int> Indices { get; }

        internal UnitySubmeshAlphaSnapshot(
            int submeshIndex,
            int materialSlotIndex,
            IReadOnlyList<int> indices)
        {
            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            var copy = new int[indices.Count];
            for (var index = 0; index < indices.Count; index++)
            {
                copy[index] = indices[index];
            }

            SubmeshIndex = submeshIndex;
            MaterialSlotIndex = materialSlotIndex;
            Indices = new ReadOnlyCollection<int>(copy);
        }
    }

    internal sealed class UnityRendererMutationTarget
    {
        internal Renderer Renderer { get; }
        internal Mesh ExpectedMesh { get; }
        internal int ExpectedMaterialSlotCount { get; }

        internal UnityRendererMutationTarget(
            Renderer renderer,
            Mesh expectedMesh,
            int expectedMaterialSlotCount)
        {
            Renderer = renderer;
            ExpectedMesh = expectedMesh;
            ExpectedMaterialSlotCount = expectedMaterialSlotCount;
        }
    }

    internal sealed class UnityRendererAlphaExtraction
    {
        internal RendererAnalysisRefusal Refusal { get; }
        internal UnityRendererAlphaSnapshot Snapshot { get; }
        internal UnityRendererMutationTarget MutationTarget { get; }

        private UnityRendererAlphaExtraction(
            RendererAnalysisRefusal refusal,
            UnityRendererAlphaSnapshot snapshot,
            UnityRendererMutationTarget mutationTarget)
        {
            Refusal = refusal;
            Snapshot = snapshot;
            MutationTarget = mutationTarget;
        }

        internal static UnityRendererAlphaExtraction Refused(
            RendererAnalysisRefusal refusal)
        {
            return new UnityRendererAlphaExtraction(refusal, null, null);
        }

        internal static UnityRendererAlphaExtraction Accepted(
            UnityRendererAlphaSnapshot snapshot,
            UnityRendererMutationTarget mutationTarget)
        {
            return new UnityRendererAlphaExtraction(
                RendererAnalysisRefusal.None,
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)),
                mutationTarget ?? throw new ArgumentNullException(
                    nameof(mutationTarget)));
        }
    }
}
