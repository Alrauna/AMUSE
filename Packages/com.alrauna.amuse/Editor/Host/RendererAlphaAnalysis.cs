using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// The closed set of facts that make a whole renderer unanalyzable. Each
    /// member is a renderer- or mesh-scoped condition; everything a single
    /// material or triangle can fail at is scoped narrower and never reaches
    /// this enum. The research census mirror holds a snapshot of these names
    /// and may retain names the product has retired. Runtime refusal
    /// precedence is defined by the analysis pipeline, not this enum.
    /// </summary>
    internal enum RendererAnalysisRefusal
    {
        None,
        UnsupportedRendererType,
        MaterialDependencyClosureFailed,
        UnrecognizedAnimatedMaterialBinding,
        MissingMesh,
        UnprovenMaterialSlotMapping,
        UnsupportedTopology,
        MalformedMeshData,
        AnimatedMeshReplacement,
        AnimatedMaterialSlotCount,

        /// <summary>
        /// This renderer carries a proof-relevant float binding, or a
        /// material-slot object binding, whose clip occurrence was observed
        /// on an additive layer. The refusal follows the captured per-clip
        /// provenance. A graph-wide additive layer alone refuses nothing.
        /// Layer weight is not modeled.
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

        /// <summary>
        /// A supported vendor feature is active on an admitted material, and
        /// the family's rule retains the material. This is an expected
        /// outcome, not missing support. The report sentence names the
        /// feature and the property.
        /// </summary>
        AdmittedMaterialRetainedByFeature,

        /// <summary>
        /// This renderer holds a material whose serialization carries the
        /// Thry locked identity, and that identity names an original shader
        /// the AssetDatabase cannot resolve or that fails the pinned Poiyomi
        /// identity. The generic semantics-unknown refusal would hide a
        /// recognized lock behind an unnamed cause, so this refusal names
        /// the lock. A locked serialization that records no original shader
        /// stays on the 2026-09-19 orphan refusal
        /// <see cref="AdmittedMaterialSemanticsUnknown"/>.
        /// </summary>
        LockedPoiyomiOriginalShaderUnattested,
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
}
