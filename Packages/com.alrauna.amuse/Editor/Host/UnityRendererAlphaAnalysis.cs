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
    /// this enum. Declaration order is mirrored by the research census
    /// vocabulary. Runtime refusal precedence is defined by the analysis
    /// pipeline, not this enum.
    /// </summary>
    internal enum RendererAnalysisRefusal
    {
        None,
        UnsupportedRendererType,
        MaterialPropertyOverridesPresent,
        MaterialDependencyClosureFailed,
        UnrecognizedAnimatedMaterialBinding,
        MissingMesh,
        UnprovenMaterialSlotMapping,
        UnsupportedTopology,
        MalformedMeshData,
        AnimatedMeshReplacement,
        AnimatedMaterialSlotCount,

        /// <summary>
        /// This renderer has a proof-relevant animated material property while
        /// the committed graph contains an additive layer. Captured graph facts
        /// carry no per-binding provenance, so V1 cannot prove that the additive
        /// contribution leaves this renderer's singleton property value intact.
        /// </summary>
        AdditiveLayerWithProofRelevantMaterialProperty,

        /// <summary>
        /// This renderer has a proof-relevant animated material property while
        /// the committed graph contains a Direct Blend Tree whose values are not
        /// normalized. Captured graph facts carry no per-binding provenance, so
        /// V1 cannot prove that the unbounded weighted sum leaves this renderer's
        /// singleton property value intact.
        /// </summary>
        UnnormalizedDirectBlendTreeWithProofRelevantMaterialProperty,

        AdmittedStateBudgetExceeded,

        /// <summary>
        /// A proof-relevant animated material property is absent from an
        /// admitted material. Task 5 observed that a bare
        /// <c>material.&lt;Property&gt;</c> curve for a property the material
        /// does not declare is still sampled into a non-empty renderer-wide
        /// <c>MaterialPropertyBlock</c>, but that observation cannot establish
        /// whether a shader that does not declare the property ignores the
        /// write when rendering. The design therefore takes the fail-closed
        /// branch and refuses rather than preserving absence and ignoring the
        /// animated value. Substitution preserving <c>HasValue == false</c> is
        /// a property of the evidence primitive, never authorization to ignore
        /// the binding: this refusal must be returned before any substitution
        /// for that admitted material is treated as authorized.
        /// </summary>
        AnimatedPropertyAbsentFromAdmittedMaterial,

        /// <summary>
        /// A proof-relevant animated property is driven by a curve whose exact
        /// value set cannot be enumerated. Finite-exactness is a precondition
        /// of admission rather than a tie-breaker among agreeing values: a
        /// curve whose sampled endpoints happen to equal the serialized default
        /// still passes through unproven intermediate values.
        /// </summary>
        UnsupportedAnimationCurveForm,

        /// <summary>
        /// The sources contributing to one proof-relevant animated property do
        /// not agree on a single exact value, so its admitted set is not a
        /// singleton. Under V1 the admitted set is
        /// the animated values together with that material's own captured default,
        /// so an animated value differing from the default is exactly this
        /// refusal: animation never overrides a differing default.
        /// </summary>
        AnimatedMaterialPropertyNotSingleton,

        /// <summary>
        /// At least one admitted material resolved to the all-Unknown alpha
        /// equation, so the slot has no attested alpha semantics to classify.
        /// </summary>
        AdmittedMaterialSemanticsUnknown,
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
                null,
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
            return Capture(renderer, null, null, out _);
        }

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
        /// Performs the host facts whose named renderer refusal must take
        /// precedence over animation-evidence closure. Geometry is deliberately
        /// excluded: runtime-state admission and its budget must run before any
        /// topology, vertex, UV, or index inspection.
        /// </summary>
        internal static RendererAnalysisRefusal HostStructuralRefusalFor(
            Renderer renderer)
        {
            var refusal = HostStructuralRefusalFor(renderer, out var mesh);
            if (refusal != RendererAnalysisRefusal.None)
            {
                return refusal;
            }

            var materials = renderer.sharedMaterials;
            return MaterialSlotMappingRefusalFor(
                mesh, materials == null ? -1 : materials.Length);
        }

        private static UnityRendererAlphaExtraction Capture(
            Renderer renderer,
            BaseMaterialSemanticsProvider legacySemanticsProvider,
            IReadOnlyList<CapturedAlphaMaterial> capturedMaterialSlots,
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
                return UnityRendererAlphaExtraction.Refused(structural);

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

            var capturedSlots = new CapturedAlphaMaterial[materialSlotCount];
            if (capturedMaterialSlots == null)
            {
                var captured =
                    UnityMaterialSemantics.CaptureAlphaMaterials(materials);
                for (var index = 0; index < captured.Count; index++)
                {
                    capturedSlots[index] = materials[index] == null
                        ? null
                        : captured[index];
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
                renderer, mesh, materialSlotCount);
            return UnityRendererAlphaExtraction.Accepted(snapshot, target);
        }

        private static RendererAnalysisRefusal HostStructuralRefusalFor(
            Renderer renderer,
            out Mesh mesh)
        {
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

            mesh = null;
            if (!IsSupportedRendererType(renderer))
                return RendererAnalysisRefusal.UnsupportedRendererType;

            // Presence only. Reading the block's contents would be
            // effective-state analysis, which this milestone does not do; a
            // block that overrides nothing alpha-relevant is refused anyway,
            // which is a false negative and therefore the safe direction.
            if (renderer.HasPropertyBlock())
                return RendererAnalysisRefusal.MaterialPropertyOverridesPresent;

            mesh = SharedMeshOf(renderer);
            return mesh == null
                ? RendererAnalysisRefusal.MissingMesh
                : RendererAnalysisRefusal.None;
        }

        private static RendererAnalysisRefusal MaterialSlotMappingRefusalFor(
            Mesh mesh,
            int materialSlotCount)
        {
            return materialSlotCount != mesh.subMeshCount
                ? RendererAnalysisRefusal.UnprovenMaterialSlotMapping
                : RendererAnalysisRefusal.None;
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
                 out AlphaMipChain chain) =>
                {
                    chain = null;
                    return channel == TextureChannel.Alpha &&
                           fields.TryGetValue(source, out chain);
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

        internal static IReadOnlyDictionary<TextureSourceId, AlphaMipChain>
            GatherAlphaFields(IReadOnlyList<CapturedAlphaMaterial> materials)
        {
            var fields = new Dictionary<TextureSourceId, AlphaMipChain>();
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
        internal static TriangleAlphaOutcome[] Classify(
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

        /// <summary>
        /// The proof intersection across every distinct admitted material
        /// state, one input array per state, all classifying the same ordered
        /// triangle set. A triangle is <c>ProvenOpaque</c> only when every
        /// state proves it opaque and <c>MustRemainTransparent</c> only when
        /// every state agrees on that; any disagreement — including agreement
        /// on a definite outcome spoiled by a single Unknown — is Unknown.
        /// This is consensus, not a severity ranking: no outcome outranks
        /// another, and nothing survives that one admitted state contradicts.
        /// <para>
        /// An empty outer list is a programming defect, not an unsupported
        /// input, and throws. Universal quantification over no states is
        /// vacuously true, so returning here would prove every triangle opaque
        /// under no evidence at all — the false-positive direction. Task 20's
        /// <c>DistinctResolutions</c> deliberately preserves an empty set so
        /// that this layer, and only this layer, rejects it.
        /// </para>
        /// <para>
        /// Nonempty states classifying zero triangles is a different thing
        /// entirely and is not a defect: an empty submesh is accepted upstream,
        /// and intersecting over an empty triangle domain yields no outcomes.
        /// </para>
        /// <para>
        /// Arrays of differing length mean the states did not classify the same
        /// triangles, so nothing can be intersected index-wise. Truncating,
        /// padding, or filling the gap with Unknown would each answer a
        /// question about triangles no caller established, so it throws.
        /// </para>
        /// <para>
        /// Duplicate inputs are harmless — intersection is idempotent, so
        /// under-deduplication upstream costs a pass and never a proof — and
        /// this method deliberately contains no equivalence heuristic of its
        /// own. The result is always freshly allocated; a single state is
        /// intersection with nothing, so its outcomes pass through by value
        /// without the caller receiving an alias of its input.
        /// </para>
        /// </summary>
        internal static TriangleAlphaOutcome[] IntersectOutcomes(
            IReadOnlyList<TriangleAlphaOutcome[]> perResolutionOutcomes)
        {
            if (perResolutionOutcomes == null)
            {
                throw new ArgumentNullException(nameof(perResolutionOutcomes));
            }

            if (perResolutionOutcomes.Count == 0)
            {
                throw new ArgumentException(
                    "Intersecting no admitted states would prove every " +
                    "triangle opaque by vacuous truth.",
                    nameof(perResolutionOutcomes));
            }

            // A missing array and a differing length are distinct defects and
            // are reported as such: a state that classified zero triangles is
            // legal, so "no outcomes" must never be how a null array reads.
            const string missing = "An admitted state supplied no outcome array.";
            var first = perResolutionOutcomes[0];
            if (first == null)
            {
                throw new ArgumentException(
                    missing, nameof(perResolutionOutcomes));
            }

            var triangleCount = first.Length;
            for (var state = 1; state < perResolutionOutcomes.Count; state++)
            {
                var outcomes = perResolutionOutcomes[state];
                if (outcomes == null)
                {
                    throw new ArgumentException(
                        missing, nameof(perResolutionOutcomes));
                }

                if (outcomes.Length != triangleCount)
                {
                    throw new ArgumentException(
                        "Every admitted state must classify the same ordered " +
                        "triangle set.",
                        nameof(perResolutionOutcomes));
                }
            }

            var intersected = new TriangleAlphaOutcome[triangleCount];
            for (var triangle = 0; triangle < triangleCount; triangle++)
            {
                // Accumulating the two definite outcomes separately, rather
                // than carrying the first state's answer forward as a
                // candidate, keeps an unexpected enum value out of the proof:
                // it agrees with neither quantifier and falls through to
                // Unknown instead of being preserved as consensus.
                var allOpaque = true;
                var allTransparent = true;
                for (var state = 0; state < perResolutionOutcomes.Count; state++)
                {
                    var outcome = perResolutionOutcomes[state][triangle];
                    allOpaque &= outcome == TriangleAlphaOutcome.ProvenOpaque;
                    allTransparent &=
                        outcome == TriangleAlphaOutcome.MustRemainTransparent;
                }

                intersected[triangle] = allOpaque
                    ? TriangleAlphaOutcome.ProvenOpaque
                    : allTransparent
                        ? TriangleAlphaOutcome.MustRemainTransparent
                        : TriangleAlphaOutcome.Unknown;
            }

            return intersected;
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
