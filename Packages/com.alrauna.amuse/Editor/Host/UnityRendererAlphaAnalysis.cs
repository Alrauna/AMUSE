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
        UnrecognizedAnimatedMaterialBinding,
        MissingMesh,
        UnprovenMaterialSlotMapping,
        UnsupportedTopology,
        MalformedMeshData,
        AnimatedMeshReplacement,
        AnimatedMaterialSlotCount,
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

    internal delegate MaterialSemantics CapturedAlphaMaterialSemanticsResolver(
        CapturedAlphaMaterial material);

    // Kept temporarily for the separately packaged census collector. New proof
    // paths use CapturedAlphaMaterialSemanticsResolver exclusively.
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
            var extraction = Capture(renderer);
            return extraction.Refusal == RendererAnalysisRefusal.None
                ? Analyze(extraction.Snapshot)
                : RendererAlphaAnalysis.Refused(extraction.Refusal);
        }

        // Compatibility for the research package's current test seam. The
        // provider is evaluated during eager capture and only captured values
        // cross into proof and planning.
        internal static RendererAlphaAnalysis Analyze(
            Renderer renderer,
            BaseMaterialSemanticsProvider semanticsProvider)
        {
            if (semanticsProvider == null)
            {
                throw new ArgumentNullException(nameof(semanticsProvider));
            }

            var extraction = Capture(
                renderer,
                semanticsProvider,
                out var capturedSemantics);
            return extraction.Refusal == RendererAnalysisRefusal.None
                ? Analyze(
                    extraction.Snapshot,
                    material => capturedSemantics.TryGetValue(
                        material, out var semantics)
                            ? semantics
                            : UnityMaterialSemantics.AllUnknown())
                : RendererAlphaAnalysis.Refused(extraction.Refusal);
        }

        internal static UnityRendererAlphaExtraction Capture(Renderer renderer)
        {
            return Capture(renderer, null, out _);
        }

        private static UnityRendererAlphaExtraction Capture(
            Renderer renderer,
            BaseMaterialSemanticsProvider legacySemanticsProvider,
            out Dictionary<CapturedAlphaMaterial, MaterialSemantics>
                legacySemantics)
        {
            legacySemantics = null;
            if (ReferenceEquals(renderer, null))
            {
                throw new ArgumentNullException(nameof(renderer));
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
                return UnityRendererAlphaExtraction.Refused(
                    RendererAnalysisRefusal.UnsupportedRendererType);
            }

            // Presence only. Reading the block's contents would be
            // effective-state analysis, which this milestone does not do; a
            // block that overrides nothing alpha-relevant is refused anyway,
            // which is a false negative and therefore the safe direction.
            if (renderer.HasPropertyBlock())
            {
                return UnityRendererAlphaExtraction.Refused(
                    RendererAnalysisRefusal.MaterialPropertyOverridesPresent);
            }

            var mesh = SharedMeshOf(renderer);
            if (mesh == null)
            {
                return UnityRendererAlphaExtraction.Refused(
                    RendererAnalysisRefusal.MissingMesh);
            }

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length != mesh.subMeshCount)
            {
                return UnityRendererAlphaExtraction.Refused(
                    RendererAnalysisRefusal.UnprovenMaterialSlotMapping);
            }

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                {
                    return UnityRendererAlphaExtraction.Refused(
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
                return UnityRendererAlphaExtraction.Refused(
                    RendererAnalysisRefusal.MalformedMeshData);
            }

            // A UV channel is either absent or complete; anything else is data
            // Unity should never produce.
            var hasUv0 = uv != null && uv.Length == mesh.vertexCount;
            if (!hasUv0 && uv != null && uv.Length != 0)
            {
                return UnityRendererAlphaExtraction.Refused(
                    RendererAnalysisRefusal.MalformedMeshData);
            }

            var submeshes = new UnitySubmeshAlphaSnapshot[mesh.subMeshCount];
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var indices = mesh.GetIndices(submesh);

                // MeshSeparationInput validates these too, but it throws where
                // renderer analysis must refuse, and a refusal is only
                // conservative if it happens before the throw.
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

            var captured = UnityMaterialSemantics.CaptureAlphaMaterials(materials);
            var capturedSlots = new CapturedAlphaMaterial[captured.Count];
            for (var index = 0; index < captured.Count; index++)
            {
                capturedSlots[index] = materials[index] == null
                    ? null
                    : captured[index];
            }

            if (legacySemanticsProvider != null)
            {
                legacySemantics = new Dictionary<
                    CapturedAlphaMaterial, MaterialSemantics>();
                var byLiveMaterial = new Dictionary<Material, MaterialSemantics>();
                for (var index = 0; index < materials.Length; index++)
                {
                    var material = materials[index];
                    if (material == null || capturedSlots[index] == null)
                    {
                        continue;
                    }

                    if (!byLiveMaterial.TryGetValue(material, out var semantics))
                    {
                        semantics = legacySemanticsProvider(material)
                            ?? UnityMaterialSemantics.AllUnknown();
                        byLiveMaterial.Add(material, semantics);
                    }

                    legacySemantics.Add(capturedSlots[index], semantics);
                }
            }

            var snapshot = new UnityRendererAlphaSnapshot(
                mesh.vertexCount,
                positions,
                hasUv0 ? uv : Array.Empty<Vector2>(),
                hasUv0,
                submeshes,
                capturedSlots);
            var target = new UnityRendererMutationTarget(
                renderer, mesh, materials.Length);
            return UnityRendererAlphaExtraction.Accepted(snapshot, target);
        }

        internal static RendererAlphaAnalysis Analyze(
            UnityRendererAlphaSnapshot snapshot,
            CapturedAlphaMaterialSemanticsResolver resolveSemantics = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            resolveSemantics ??= UnityMaterialSemantics.AnalyzeAlphaMaterial;
            var fields = GatherAlphaFields(snapshot.Materials);
            return Analyze(
                snapshot,
                resolveSemantics,
                (TextureSourceId source,
                 TextureChannel channel,
                 out AlphaTextureData field) =>
                {
                    field = null;
                    return channel == TextureChannel.Alpha &&
                           fields.TryGetValue(source, out field);
                });
        }

        private static RendererAlphaAnalysis Analyze(
            UnityRendererAlphaSnapshot snapshot,
            CapturedAlphaMaterialSemanticsResolver resolveSemantics,
            AlphaFieldProvider alphaFields)
        {
            var resolutions = new Dictionary<
                CapturedAlphaMaterial, AlphaResolution>();
            var submeshInputs = new List<SubmeshSeparationInput>(
                snapshot.Submeshes.Count);
            var records = new List<SubmeshAlphaAnalysis>(
                snapshot.Submeshes.Count);

            foreach (var submesh in snapshot.Submeshes)
            {
                var material = snapshot.Materials[submesh.MaterialSlotIndex];
                var resolution = ResolveFor(
                    material, resolveSemantics, alphaFields, resolutions);
                var outcomes = Classify(
                    submesh.Indices,
                    snapshot.Positions,
                    snapshot.HasUv0 ? snapshot.Uv0 : null,
                    resolution);

                submeshInputs.Add(new SubmeshSeparationInput(
                    submesh.MaterialSlotIndex, submesh.Indices, outcomes));
                records.Add(new SubmeshAlphaAnalysis(
                    submesh.SubmeshIndex,
                    submesh.MaterialSlotIndex,
                    material != null,
                    resolution.Failure));
            }

            var plan = MeshSeparationPlanner.Create(
                new MeshSeparationInput(snapshot.VertexCount, submeshInputs));
            return RendererAlphaAnalysis.Planned(plan, records);
        }

        private static AlphaResolution ResolveFor(
            CapturedAlphaMaterial material,
            CapturedAlphaMaterialSemanticsResolver resolveSemantics,
            AlphaFieldProvider alphaFields,
            Dictionary<CapturedAlphaMaterial, AlphaResolution> memo)
        {
            if (material != null && memo.TryGetValue(material, out var cached))
            {
                return cached;
            }

            var semantics = material == null
                ? UnityMaterialSemantics.AllUnknown()
                : resolveSemantics(material) ?? UnityMaterialSemantics.AllUnknown();
            var resolution = AlphaSemanticsResolver.Resolve(
                semantics.Alpha, alphaFields);

            if (material != null)
            {
                memo[material] = resolution;
            }

            return resolution;
        }

        private static IReadOnlyDictionary<TextureSourceId, AlphaTextureData>
            GatherAlphaFields(IReadOnlyList<CapturedAlphaMaterial> materials)
        {
            var fields = new Dictionary<TextureSourceId, AlphaTextureData>();
            foreach (var material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                foreach (var texture in material.Evidence.Textures)
                {
                    if (texture.HasSourceIdentity &&
                        texture.HasAlphaChannel &&
                        !fields.ContainsKey(texture.SourceIdentity))
                    {
                        fields.Add(texture.SourceIdentity, texture.AlphaChannel);
                    }
                }
            }

            return fields;
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
            IReadOnlyList<int> indices,
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<Vector2> uv,
            AlphaResolution resolution)
        {
            var outcomes = new TriangleAlphaOutcome[indices.Count / 3];
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

        // The two renderer-local animation dimensions that invalidate the
        // premises of every later proof rather than any one value in it: the
        // mesh the proof is stated over, and the slot topology that maps
        // submeshes to materials.
        private const string AnimatedMeshProperty = "m_Mesh";
        private const string AnimatedMaterialSlotCountProperty =
            "m_Materials.Array.size";

        /// <summary>
        /// Whether renderer-local animation invalidates the structure the
        /// proof rests on. Presence of the dimension is the whole test: the
        /// replacement mesh is never inspected and animated slot counts are
        /// never compared against the current one, because V1 has no
        /// reconciliation theorem to apply to either.
        /// <para>
        /// Both captured categories are searched for both properties. Task 3
        /// observed that Unity generates neither binding: no <c>m_Mesh</c> for
        /// a <c>SkinnedMeshRenderer</c>, and no <c>m_Materials.Array.size</c>
        /// at all — an authored float curve targeting the slot count changed
        /// nothing when sampled, despite a working control. The category that
        /// can carry a <em>working</em> structural animation is therefore
        /// <em>unobserved</em>, in neither direction, for either property.
        /// Searching both is a hedge against externally authored evidence, not
        /// a claim about which category Unity emits.
        /// </para>
        /// <para>
        /// Matching is exact. An <c>m_Materials.Array.data[n]</c> binding is an
        /// ordinary material swap owned by the admitted-material machinery, and
        /// a prefix match would refuse every avatar that swaps a material.
        /// </para>
        /// </summary>
        internal static RendererAnalysisRefusal StructuralRefusalFor(
            IReadOnlyList<CapturedFloatBinding> floats,
            IReadOnlyList<CapturedObjectBinding> objects,
            string rendererPath)
        {
            if (NamesStructuralProperty(
                    floats, objects, rendererPath, AnimatedMeshProperty))
            {
                return RendererAnalysisRefusal.AnimatedMeshReplacement;
            }

            if (NamesStructuralProperty(
                    floats,
                    objects,
                    rendererPath,
                    AnimatedMaterialSlotCountProperty))
            {
                return RendererAnalysisRefusal.AnimatedMaterialSlotCount;
            }

            return RendererAnalysisRefusal.None;
        }

        // One property at a time, mesh first, so the reported reason follows
        // the enum's declaration order rather than the order capture happened
        // to record two structural bindings in.
        private static bool NamesStructuralProperty(
            IReadOnlyList<CapturedFloatBinding> floats,
            IReadOnlyList<CapturedObjectBinding> objects,
            string rendererPath,
            string structural)
        {
            foreach (var binding in objects)
            {
                if (IsOnRenderer(binding.Path, rendererPath) &&
                    IsProperty(binding.PropertyName, structural))
                {
                    return true;
                }
            }

            foreach (var binding in floats)
            {
                if (IsOnRenderer(binding.Path, rendererPath) &&
                    IsProperty(binding.PropertyName, structural))
                {
                    return true;
                }
            }

            return false;
        }

        // Ordinal path identity, matching the rest of this feature: a binding
        // on another path says nothing about this renderer.
        private static bool IsOnRenderer(string bindingPath, string rendererPath)
        {
            return string.Equals(
                bindingPath, rendererPath, StringComparison.Ordinal);
        }

        private static bool IsProperty(string propertyName, string structural)
        {
            return string.Equals(
                propertyName, structural, StringComparison.Ordinal);
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
