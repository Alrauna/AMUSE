using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Why a single alpha-separation candidate slot was not transformed.
    /// <para>
    /// Deliberately a separate vocabulary from
    /// <see cref="RendererAnalysisRefusal"/>, which belongs to renderer-scoped
    /// alpha analysis: merging them would put transformation conditions where
    /// analysis reads them, so unknown transformation state would start refusing
    /// analysis that does not depend on it. This is the same separation
    /// <c>PoiyomiOpaqueConversionRefusal</c> already makes.
    /// </para>
    /// </summary>
    internal enum AlphaSeparationSlotRefusal
    {
        None,
    }

    /// <summary>
    /// What the extension-free barrier prepared for the whole avatar, carried to
    /// the later animator-services pass.
    /// <para>
    /// This is a live, transient host capability, NOT proof evidence: it holds
    /// live Unity objects and is deliberately outside the captured-evidence
    /// graph's no-live-Unity-object guarantee, exactly as
    /// <c>AmusePlatformFinishState.AnimatorBindings</c> is.
    /// </para>
    /// </summary>
    internal sealed class PreparedAlphaSeparation
    {
        private readonly List<PreparedRendererSeparation> renderers =
            new List<PreparedRendererSeparation>();

        // Default comparer, deliberately: the keys are live, non-destroyed
        // materials held for the duration of one synchronous build, so Unity's
        // overloaded Equals cannot collapse two distinct keys, and the sweep
        // that destroys them runs after every lookup.
        private readonly Dictionary<Material, Material> opaqueBySource =
            new Dictionary<Material, Material>();

        private readonly List<Material> createdClones = new List<Material>();

        /// <summary>Renderers with at least one candidate slot, in the order the
        /// barrier's renderer loop visited them.</summary>
        internal IReadOnlyList<PreparedRendererSeparation> Renderers => renderers;

        /// <summary>
        /// Avatar-scoped source material to opaque result. An
        /// <c>AlreadyOpaque</c> source maps to itself, so not every value here is
        /// a clone; <see cref="CreatedClones"/> is the set that is.
        /// </summary>
        internal IReadOnlyDictionary<Material, Material> OpaqueBySource =>
            opaqueBySource;

        /// <summary>
        /// Only the materials AMUSE itself instantiated. A source material can
        /// therefore never be swept, because it can never appear here.
        /// </summary>
        internal IReadOnlyList<Material> CreatedClones => createdClones;

        internal void Add(PreparedRendererSeparation renderer)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            renderers.Add(renderer);
        }
    }

    /// <summary>
    /// One renderer's prepared separation. The existing
    /// <see cref="UnityRendererMutationTarget"/> and
    /// <see cref="MeshSeparationPlan"/> are held whole rather than copied from,
    /// so nothing here duplicates a field of an existing type.
    /// </summary>
    internal sealed class PreparedRendererSeparation
    {
        internal PreparedRendererSeparation(
            UnityRendererMutationTarget target,
            string rendererPath,
            MeshSeparationPlan plan,
            CapturedAnimationEvidence evidence,
            IReadOnlyList<PreparedSlotSeparation> candidateSlots)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            // Empty is the avatar root's animation path and is valid; only an
            // absent path is a caller defect.
            RendererPath = rendererPath
                ?? throw new ArgumentNullException(nameof(rendererPath));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Evidence = evidence
                ?? throw new ArgumentNullException(nameof(evidence));
            if (candidateSlots == null)
                throw new ArgumentNullException(nameof(candidateSlots));

            var copy = new PreparedSlotSeparation[candidateSlots.Count];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = candidateSlots[index];

            CandidateSlots = Array.AsReadOnly(copy);
        }

        internal UnityRendererMutationTarget Target { get; }
        internal string RendererPath { get; }
        internal MeshSeparationPlan Plan { get; }
        internal CapturedAnimationEvidence Evidence { get; }

        /// <summary>
        /// The unassigned native clone this renderer's geometry mutation will be
        /// finalized on, or null when no surviving slot requires one.
        /// </summary>
        internal Mesh MeshClone { get; set; }

        /// <summary>Candidate slots in ascending material-slot order.</summary>
        internal IReadOnlyList<PreparedSlotSeparation> CandidateSlots { get; }
    }

    /// <summary>
    /// One candidate slot. The existing <see cref="SubmeshSeparationPlan"/> is
    /// held whole: it already carries the slot index
    /// (<c>SourceMaterialBindingIndex</c>), the disposition and both triangle
    /// ordinal lists, so none of them is copied out.
    /// </summary>
    internal sealed class PreparedSlotSeparation
    {
        internal PreparedSlotSeparation(
            SubmeshSeparationPlan plan,
            IReadOnlyDictionary<Material, Material> opaqueOfAdmitted)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            OpaqueOfAdmitted = opaqueOfAdmitted
                ?? throw new ArgumentNullException(nameof(opaqueOfAdmitted));
        }

        internal SubmeshSeparationPlan Plan { get; }

        /// <summary>
        /// This slot's admitted source materials mapped to their opaque results.
        /// The domain is exactly this slot's admitted set, never the renderer's
        /// whole admitted list. Uses the default comparer for the reason
        /// <see cref="PreparedAlphaSeparation"/> documents.
        /// </summary>
        internal IReadOnlyDictionary<Material, Material> OpaqueOfAdmitted { get; }
    }
}
