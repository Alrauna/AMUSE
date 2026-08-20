using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// The closed set of facts that make a whole renderer unanalyzable. Each
    /// member is a renderer- or mesh-scoped condition; everything a single
    /// material or triangle can fail at is scoped narrower and never reaches
    /// this enum. Declaration order is the order the checks run in.
    /// </summary>
    internal enum RendererAnalysisRefusal
    {
        None,
        UnsupportedRendererType,
        MaterialPropertyOverridesPresent,
        MissingMesh,
        UnprovenMaterialSlotMapping,
        UnsupportedTopology,
        MalformedMeshData,
    }

    /// <summary>
    /// Why one submesh's alpha could or could not be proven at the material and
    /// resolver level. The failure vocabulary is the resolver's own, reused
    /// rather than duplicated; <see cref="HasMaterial"/> adds the one Unity fact
    /// the resolver cannot express, because an empty slot and an unattested
    /// shader both reduce to <c>SemanticsUnknown</c>.
    /// <para>
    /// It deliberately does <em>not</em> explain every preserved triangle. A
    /// triangle can be <c>Unknown</c> on a submesh whose failure is
    /// <c>None</c> — through unavailable UV0 under a UV-dependent equation, a
    /// non-finite position, degeneracy, or the classifier's own workload
    /// refusal — and no reason for that is recorded anywhere. The original
    /// per-triangle outcomes remain readable from
    /// <c>MeshSeparationPlan.Source</c>, so <c>Unknown</c> stays distinguishable
    /// from <c>MustRemainTransparent</c>, but their causes do not.
    /// </para>
    /// </summary>
    internal sealed class SubmeshAlphaAnalysis
    {
        internal int SubmeshIndex { get; }
        internal int MaterialSlotIndex { get; }
        internal bool HasMaterial { get; }
        internal AlphaResolutionFailure Failure { get; }

        internal SubmeshAlphaAnalysis(
            int submeshIndex,
            int materialSlotIndex,
            bool hasMaterial,
            AlphaResolutionFailure failure)
        {
            SubmeshIndex = submeshIndex;
            MaterialSlotIndex = materialSlotIndex;
            HasMaterial = hasMaterial;
            Failure = failure;
        }
    }

    /// <summary>
    /// One immutable renderer-level analysis: either a separation plan with one
    /// provenance record per submesh, or a named refusal and no plan. It holds
    /// no live Unity object; the caller supplied the renderer and still owns it.
    /// </summary>
    internal sealed class RendererAlphaAnalysis
    {
        internal RendererAnalysisRefusal Refusal { get; }
        internal MeshSeparationPlan Plan { get; }
        internal IReadOnlyList<SubmeshAlphaAnalysis> Submeshes { get; }

        private RendererAlphaAnalysis(
            RendererAnalysisRefusal refusal,
            MeshSeparationPlan plan,
            IReadOnlyList<SubmeshAlphaAnalysis> submeshes)
        {
            // A refusal has no plan and a plan has no refusal.
            if ((refusal == RendererAnalysisRefusal.None) != (plan != null))
            {
                throw new ArgumentException(
                    "A renderer analysis has a plan exactly when it has no refusal.",
                    nameof(refusal));
            }

            var copy = new SubmeshAlphaAnalysis[submeshes.Count];
            for (var index = 0; index < submeshes.Count; index++)
            {
                copy[index] = submeshes[index];
            }

            Refusal = refusal;
            Plan = plan;
            Submeshes = Array.AsReadOnly(copy);
        }

        internal static RendererAlphaAnalysis Refused(
            RendererAnalysisRefusal refusal)
        {
            return new RendererAlphaAnalysis(
                refusal, null, Array.Empty<SubmeshAlphaAnalysis>());
        }

        internal static RendererAlphaAnalysis Planned(
            MeshSeparationPlan plan,
            IReadOnlyList<SubmeshAlphaAnalysis> submeshes)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            return new RendererAlphaAnalysis(
                RendererAnalysisRefusal.None, plan, submeshes);
        }
    }

    /// <summary>
    /// Supplies normalized semantics for one base material. The production
    /// implementation is <see cref="UnityMaterialSemantics.AnalyzeBaseMaterial"/>;
    /// the parameter exists because the public development project installs no
    /// vendor shader and therefore cannot attest one, so deterministic tests
    /// substitute this single link through each frontend's existing
    /// verified-material seam. It mirrors the established
    /// <see cref="AlphaFieldProvider"/> precedent: one delegate, one production
    /// implementation, no registry.
    /// </summary>
    internal delegate MaterialSemantics BaseMaterialSemanticsProvider(
        Material material);

    /// <summary>
    /// Drives AMUSE's existing semantic, texture-evidence, exact-geometry, and
    /// separation-planning components over one Unity renderer's current base
    /// state, and produces an immutable plan describing which geometry is
    /// provably safe for opaque separation.
    /// <para>
    /// It reads only. It uses <c>sharedMesh</c> and <c>sharedMaterials</c>
    /// exclusively, because <c>MeshFilter.mesh</c> and <c>Renderer.materials</c>
    /// instantiate copies as a side effect of being read. It never bakes,
    /// imports, writes, or creates an asset, and it never calls
    /// <c>GetPropertyBlock</c>.
    /// </para>
    /// <para>
    /// It analyzes the current/base material state only. Animator state,
    /// animation clips, material swaps, and property-block contents are outside
    /// its claim — and because a property block can override the properties a
    /// proof rests on, a renderer that carries one is refused outright rather
    /// than analyzed under an assumption.
    /// </para>
    /// </summary>
    internal static class UnityRendererAlphaAnalysis
    {
        internal static RendererAlphaAnalysis Analyze(Renderer renderer)
        {
            return Analyze(renderer, UnityMaterialSemantics.AnalyzeBaseMaterial);
        }

        internal static RendererAlphaAnalysis Analyze(
            Renderer renderer,
            BaseMaterialSemanticsProvider semanticsProvider)
        {
            if (ReferenceEquals(renderer, null))
            {
                throw new ArgumentNullException(nameof(renderer));
            }
            if (semanticsProvider == null)
            {
                throw new ArgumentNullException(nameof(semanticsProvider));
            }

            // Unity's overloaded equality reports a destroyed object as null.
            if (renderer == null)
            {
                throw new ArgumentException(
                    "The renderer has been destroyed and cannot be analyzed.",
                    nameof(renderer));
            }

            if (!IsSupportedRendererType(renderer))
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.UnsupportedRendererType);
            }

            // Presence only. Reading the block's contents would be
            // effective-state analysis, which this milestone does not do; a
            // block that overrides nothing alpha-relevant is refused anyway,
            // which is a false negative and therefore the safe direction.
            if (renderer.HasPropertyBlock())
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.MaterialPropertyOverridesPresent);
            }

            var mesh = SharedMeshOf(renderer);
            if (mesh == null)
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.MissingMesh);
            }

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length != mesh.subMeshCount)
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.UnprovenMaterialSlotMapping);
            }

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                {
                    return RendererAlphaAnalysis.Refused(
                        RendererAnalysisRefusal.UnsupportedTopology);
                }
            }

            // Measured in this project on Unity 2022.3.22f1: vertices, uv, and
            // GetIndices all return complete data on a mesh whose isReadable is
            // false, because the Editor permits mesh access outside the
            // game/rendering loop. There is therefore no readability pre-check
            // and no exception handling here — catching what has been observed
            // not to throw would hide the very defect the characterization test
            // exists to find.
            var positions = mesh.vertices;
            var uv = mesh.uv;
            if (positions == null || positions.Length != mesh.vertexCount)
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.MalformedMeshData);
            }

            // A UV channel is either absent or complete; anything else is data
            // Unity should never produce.
            var hasUv0 = uv != null && uv.Length == mesh.vertexCount;
            if (!hasUv0 && uv != null && uv.Length != 0)
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.MalformedMeshData);
            }

            var evidence = new UnityAlphaFieldEvidence(
                GatherCandidateTextures(materials));
            var resolutions = new Dictionary<Material, AlphaResolution>();
            var submeshInputs = new List<SubmeshSeparationInput>(materials.Length);
            var records = new List<SubmeshAlphaAnalysis>(materials.Length);

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var indices = mesh.GetIndices(submesh);

                // MeshSeparationInput validates these too, but it throws where
                // renderer analysis must refuse, and a refusal is only
                // conservative if it happens before the throw.
                if (indices == null || indices.Length % 3 != 0)
                {
                    return RendererAlphaAnalysis.Refused(
                        RendererAnalysisRefusal.MalformedMeshData);
                }

                for (var index = 0; index < indices.Length; index++)
                {
                    if (indices[index] < 0 || indices[index] >= mesh.vertexCount)
                    {
                        return RendererAlphaAnalysis.Refused(
                            RendererAnalysisRefusal.MalformedMeshData);
                    }
                }

                var material = materials[submesh];
                var resolution = ResolveFor(
                    material, semanticsProvider, evidence, resolutions);
                var outcomes = Classify(
                    indices, positions, hasUv0 ? uv : null, resolution);

                submeshInputs.Add(
                    new SubmeshSeparationInput(submesh, indices, outcomes));
                records.Add(new SubmeshAlphaAnalysis(
                    submesh,
                    submesh,
                    material != null,
                    resolution.Failure));
            }

            var plan = MeshSeparationPlanner.Create(
                new MeshSeparationInput(mesh.vertexCount, submeshInputs));
            return RendererAlphaAnalysis.Planned(plan, records);
        }

        /// <summary>
        /// Every texture the renderer's own materials reference, read through
        /// each shader's declared texture properties so no AMUSE code names a
        /// property. The set is a superset of what the alpha semantics will ask
        /// for, which is correct and cheap: the provider stores identity only
        /// and reads pixels lazily, and a texture that was not gathered simply
        /// refuses with MissingTextureEvidence.
        /// <para>
        /// A null, destroyed, or shaderless material contributes nothing.
        /// Skipping the shaderless case is not a semantic decision — such a
        /// material is all-Unknown either way — it only keeps evidence gathering
        /// from turning that conservative result into an exception.
        /// </para>
        /// </summary>
        private static IEnumerable<Texture> GatherCandidateTextures(
            Material[] materials)
        {
            foreach (var material in materials)
            {
                if (material == null || material.shader == null)
                {
                    continue;
                }

                foreach (var propertyName in material.GetTexturePropertyNames())
                {
                    yield return material.GetTexture(propertyName);
                }
            }
        }

        /// <summary>
        /// One resolution per distinct material. Attestation hashes the whole
        /// shader source, and avatars repeat material references across slots,
        /// so this memo removes real repeated work. It is local to one analysis
        /// and is discarded when it returns.
        /// </summary>
        private static AlphaResolution ResolveFor(
            Material material,
            BaseMaterialSemanticsProvider semanticsProvider,
            UnityAlphaFieldEvidence evidence,
            Dictionary<Material, AlphaResolution> memo)
        {
            if (material != null && memo.TryGetValue(material, out var cached))
            {
                return cached;
            }

            var semantics = semanticsProvider(material)
                ?? UnityMaterialSemantics.AllUnknown();
            var resolution = AlphaSemanticsResolver.Resolve(
                semantics.Alpha, evidence.TryGetAlphaField);

            if (material != null)
            {
                memo[material] = resolution;
            }

            return resolution;
        }

        /// <summary>
        /// One outcome per triangle, in source order. A refused resolution
        /// yields Unknown without consulting geometry, because a refusal has no
        /// triangle outcome at all.
        /// <para>
        /// Unavailable UV0 — a mesh with no UV channel, or a triangle with a
        /// non-finite UV — is passed on as
        /// <see cref="TriangleAlphaInput.MissingUv0"/> rather than pre-empted as
        /// Unknown, so the resolution decides: a constant alpha of one is still
        /// proven, and only an equation that genuinely samples a texture becomes
        /// Unknown. Missing knowledge invalidates only what depends on it.
        /// </para>
        /// <para>
        /// A non-finite position is the one asymmetry: TriangleAlphaInput has no
        /// "positions unavailable" form and AlphaResolution does not expose
        /// whether it is uniform, so such a triangle is Unknown even under a
        /// resolution that would never have looked at geometry. That is a false
        /// negative on malformed data, which is the acceptable direction.
        /// </para>
        /// </summary>
        private static TriangleAlphaOutcome[] Classify(
            int[] indices,
            Vector3[] positions,
            Vector2[] uv,
            AlphaResolution resolution)
        {
            var outcomes = new TriangleAlphaOutcome[indices.Length / 3];
            if (!resolution.IsResolved)
            {
                for (var triangle = 0; triangle < outcomes.Length; triangle++)
                {
                    outcomes[triangle] = TriangleAlphaOutcome.Unknown;
                }

                return outcomes;
            }

            for (var triangle = 0; triangle < outcomes.Length; triangle++)
            {
                var a = indices[triangle * 3];
                var b = indices[triangle * 3 + 1];
                var c = indices[triangle * 3 + 2];

                if (!IsFinite(positions[a]) ||
                    !IsFinite(positions[b]) ||
                    !IsFinite(positions[c]))
                {
                    outcomes[triangle] = TriangleAlphaOutcome.Unknown;
                    continue;
                }

                var uvAvailable = uv != null &&
                                  IsFinite(uv[a]) &&
                                  IsFinite(uv[b]) &&
                                  IsFinite(uv[c]);

                outcomes[triangle] = resolution.Classify(
                    uvAvailable
                        ? TriangleAlphaInput.WithUv0(
                            positions[a], positions[b], positions[c],
                            uv[a], uv[b], uv[c])
                        : TriangleAlphaInput.MissingUv0(
                            positions[a], positions[b], positions[c]));
            }

            return outcomes;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsSupportedRendererType(Renderer renderer)
        {
            // ParticleSystemRenderer, LineRenderer, TrailRenderer,
            // SpriteRenderer, and BillboardRenderer derive from Renderer
            // directly, not from MeshRenderer, so this cannot capture them.
            return renderer is SkinnedMeshRenderer || renderer is MeshRenderer;
        }

        /// <summary>
        /// The one mesh a supported renderer contributes. Both paths converge on
        /// a shared reference; neither instantiates a copy.
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
}
