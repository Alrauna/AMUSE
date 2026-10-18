using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    internal static class UnityRendererGeometryExtractor
    {
        /// <summary>
        /// Captures only renderer and mesh geometry while carrying forward the
        /// already-closed immutable material slots. This path deliberately never
        /// reads <see cref="Renderer.sharedMaterials"/> or recaptures material
        /// semantics.
        /// </summary>
        internal static UnityRendererAlphaExtraction CaptureGeometry(
            Renderer renderer,
            IReadOnlyList<CapturedAlphaMaterial> capturedMaterialSlots)
        {
            if (capturedMaterialSlots == null)
            {
                throw new ArgumentNullException(nameof(capturedMaterialSlots));
            }

            return Capture(renderer, null, capturedMaterialSlots, out _);
        }

        /// <summary>
        /// The same host facts, plus the two slot counts a mapping
        /// refusal report must carry. The counts stay minus one unless
        /// the mapping check itself ran, so a report never shows a count
        /// that no decision used.
        /// </summary>
        internal static RendererAnalysisRefusal HostStructuralRefusalFor(
            Renderer renderer,
            out int meshSubMeshCount,
            out int materialSlotCount)
        {
            var refusal = HostStructuralRefusalFor(renderer, out var mesh);
            meshSubMeshCount = -1;
            materialSlotCount = -1;
            if (refusal != RendererAnalysisRefusal.None)
            {
                return refusal;
            }

            meshSubMeshCount = mesh.subMeshCount;
            var materials = renderer.sharedMaterials;
            materialSlotCount = materials == null ? -1 : materials.Length;
            return MaterialSlotMappingRefusalFor(mesh, materialSlotCount);
        }

        internal static UnityRendererAlphaExtraction Capture(
            Renderer renderer,
            ResearchMaterialSemanticsProvider legacySemanticsProvider,
            IReadOnlyList<CapturedAlphaMaterial> capturedMaterialSlots,
            out Dictionary<CapturedAlphaMaterial, MaterialSemantics>
                legacySemantics,
            RegisteredSourceLookup resolveRegisteredSource = null)
        {
            legacySemantics = null;
            if (ReferenceEquals(renderer, null))
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            if (renderer == null)
            {
                throw new ArgumentException(
                    "The renderer has been destroyed and cannot be analyzed.",
                    nameof(renderer));
            }

            var structural = HostStructuralRefusalFor(renderer, out var mesh);
            if (structural != RendererAnalysisRefusal.None)
                return UnityRendererAlphaExtraction.Refused(structural);

            Material[] materials = null;
            var materialSlotCount = capturedMaterialSlots == null
                ? -1
                : capturedMaterialSlots.Count;
            if (capturedMaterialSlots == null)
            {
                materials = renderer.sharedMaterials;
                materialSlotCount = materials == null ? -1 : materials.Length;
            }

            structural = MaterialSlotMappingRefusalFor(mesh, materialSlotCount);
            if (structural != RendererAnalysisRefusal.None)
                return UnityRendererAlphaExtraction.Refused(
                    structural, mesh.subMeshCount, materialSlotCount);

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                {
                    return UnityRendererAlphaExtraction.Refused(
                        RendererAnalysisRefusal.UnsupportedTopology);
                }
            }

            var positions = mesh.vertices;
            var uv = mesh.uv;
            if (positions == null || positions.Length != mesh.vertexCount)
            {
                return UnityRendererAlphaExtraction.Refused(
                    RendererAnalysisRefusal.MalformedMeshData);
            }

            var hasUv0 = uv != null && uv.Length == mesh.vertexCount;
            if (!hasUv0 && uv != null && uv.Length != 0)
            {
                return UnityRendererAlphaExtraction.Refused(
                    RendererAnalysisRefusal.MalformedMeshData);
            }

            var extraUvSets = new IReadOnlyList<Vector2>[3];
            for (var channel = 1; channel <= 3; channel++)
            {
                var set = new List<Vector2>();
                mesh.GetUVs(channel, set);
                if (set.Count == 0)
                {
                    extraUvSets[channel - 1] = null;
                    continue;
                }

                if (set.Count != mesh.vertexCount)
                {
                    return UnityRendererAlphaExtraction.Refused(
                        RendererAnalysisRefusal.MalformedMeshData);
                }

                extraUvSets[channel - 1] = set;
            }

            var submeshes = new UnitySubmeshAlphaSnapshot[mesh.subMeshCount];
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var indices = mesh.GetIndices(submesh);

                if (indices == null || indices.Length % 3 != 0)
                {
                    return UnityRendererAlphaExtraction.Refused(
                        RendererAnalysisRefusal.MalformedMeshData);
                }

                for (var index = 0; index < indices.Length; index++)
                {
                    if (indices[index] < 0 || indices[index] >= mesh.vertexCount)
                    {
                        return UnityRendererAlphaExtraction.Refused(
                            RendererAnalysisRefusal.MalformedMeshData);
                    }
                }

                submeshes[submesh] = new UnitySubmeshAlphaSnapshot(
                    submesh, submesh, indices);
            }

            var capturedSlots = new CapturedAlphaMaterial[materialSlotCount];
            if (capturedMaterialSlots == null)
            {
                var effectiveClones = new List<Material>();
                var effective = materials == null
                    ? materials
                    : EffectiveMaterialMaterialization.Materialize(
                        renderer, materials, out effectiveClones, textureOnly: true);
                try
                {
                    var captured =
                        UnityMaterialSemantics.CaptureAlphaMaterials(
                            effective,
                            resolveRegisteredSource);
                    for (var index = 0; index < captured.Count; index++)
                    {
                        capturedSlots[index] = effective[index] == null
                            ? null
                            : captured[index];
                    }
                }
                finally
                {
                    foreach (var clone in effectiveClones)
                    {
                        UnityEngine.Object.DestroyImmediate(clone);
                    }
                }
            }
            else
            {
                for (var index = 0; index < capturedSlots.Length; index++)
                    capturedSlots[index] = capturedMaterialSlots[index];
            }

            if (legacySemanticsProvider != null)
            {
                legacySemantics = new Dictionary<
                    CapturedAlphaMaterial, MaterialSemantics>();
                var effectiveClones = new List<Material>();
                var effective = materials == null
                    ? materials
                    : EffectiveMaterialMaterialization.Materialize(
                        renderer, materials, out effectiveClones);
                try
                {
                    var byLiveMaterial =
                        new Dictionary<Material, MaterialSemantics>();
                    for (var index = 0; index < effective.Length; index++)
                    {
                        var material = effective[index];
                        if (material == null || capturedSlots[index] == null)
                        {
                            continue;
                        }

                        if (!byLiveMaterial.TryGetValue(
                                material, out var semantics))
                        {
                            semantics = legacySemanticsProvider(material)
                                ?? EvidenceGates.AllUnknown();
                            byLiveMaterial.Add(material, semantics);
                        }

                        legacySemantics.Add(capturedSlots[index], semantics);
                    }
                }
                finally
                {
                    foreach (var clone in effectiveClones)
                    {
                        UnityEngine.Object.DestroyImmediate(clone);
                    }
                }
            }

            var snapshot = new UnityRendererAlphaSnapshot(
                mesh.vertexCount,
                positions,
                hasUv0 ? uv : Array.Empty<Vector2>(),
                hasUv0,
                submeshes,
                capturedSlots,
                extraUvSets);
            var target = new UnityRendererMutationTarget(
                renderer, mesh, materialSlotCount);
            return UnityRendererAlphaExtraction.Accepted(snapshot, target);
        }

        internal static RendererAnalysisRefusal HostStructuralRefusalFor(
            Renderer renderer,
            out Mesh mesh)
        {
            if (ReferenceEquals(renderer, null))
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            if (renderer == null)
            {
                throw new ArgumentException(
                    "The renderer has been destroyed and cannot be analyzed.",
                    nameof(renderer));
            }

            mesh = null;
            if (!IsSupportedRendererType(renderer))
                return RendererAnalysisRefusal.UnsupportedRendererType;

            mesh = SharedMeshOf(renderer);
            return mesh == null
                ? RendererAnalysisRefusal.MissingMesh
                : RendererAnalysisRefusal.None;
        }

        internal static RendererAnalysisRefusal MaterialSlotMappingRefusalFor(
            Mesh mesh,
            int materialSlotCount)
        {
            return materialSlotCount != mesh.subMeshCount
                ? RendererAnalysisRefusal.UnprovenMaterialSlotMapping
                : RendererAnalysisRefusal.None;
        }

        internal static bool IsSupportedRendererType(Renderer renderer)
        {
            return renderer is SkinnedMeshRenderer || renderer is MeshRenderer;
        }

        internal static Mesh SharedMeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            var filter = renderer.GetComponent<MeshFilter>();
            return filter == null ? null : filter.sharedMesh;
        }
    }
}
