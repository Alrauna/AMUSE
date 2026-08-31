using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf.animator;
using UnityEditor;
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

        // --- Slot-scoped members. A failing slot keeps its original submesh,
        // its original material assignment and its original material-swap
        // curve; independently valid slots on the same renderer continue.

        /// <summary>An admitted material of this slot belongs to a family
        /// with no opaque conversion — every non-`lilToon` lilToon identity
        /// (outline, transparent, Lite, Tessellation, Multi, Gem, Fur,
        /// Refraction, overlay, fake shadow, container) and every mixed
        /// admitted set containing one: not every admitted runtime value can
        /// be mapped to an opaque result, so the whole slot is refused. The
        /// attested opaque `lilToon` and the attested cutout identity are
        /// conversion families and never refuse here.</summary>
        OpaqueConversionUnsupportedFamily,

        /// <summary><c>PoiyomiOpaqueConversion</c> refused an admitted
        /// material of this slot, either at source attestation or at verified
        /// eligibility.</summary>
        OpaqueConversionRefused,

        /// <summary>A conversion-relevant animated property is not an exact
        /// singleton, is not finite-exact, or is absent from an admitted
        /// material of this slot.</summary>
        ConversionStateNotAdmitted,

        /// <summary>A canonical recipe property carrying an admitted
        /// conversion binding does not hold its canonical value, so the recipe
        /// AMUSE would write is provably overwritten at runtime and the tuple
        /// the proof reasons about would be fiction.</summary>
        ConversionPropertyOverwrittenAtRuntime,

        /// <summary>A special/marker motion carries this slot's
        /// material-swap binding. Marker clips cannot be edited —
        /// <c>VirtualClip.SetObjectCurve</c> silently no-ops on them — so the
        /// slot must be refused rather than edited.</summary>
        MarkerClipCarriesSlotBinding,

        /// <summary>A live runtime material value for this slot is absent
        /// from its mapping — whether it arrives from a curve keyframe or
        /// from the live current assignment. One member, because the
        /// condition is identical and the arrival route carries no
        /// information.</summary>
        RuntimeMaterialValueNotMapped,

        /// <summary>A target binding exists live that the captured evidence
        /// did not record, so the slot's runtime material behavior is not
        /// fully described by what was proven.</summary>
        SlotBindingAbsentFromEvidence,

        // --- Renderer-scoped members. Applied to all candidate slots of one
        // renderer and to nothing else: never to that renderer's alpha
        // analysis, never to another renderer.

        /// <summary>A material binding that could address a
        /// conversion-relevant property is not recognized by conversion's own
        /// relevance request. The binding is renderer-wide, so it invalidates
        /// every candidate slot on the renderer.</summary>
        ConversionBindingUnrecognized,

        /// <summary>The renderer has conversion-relevant animated material
        /// state while the committed graph contains an additive layer.</summary>
        ConversionStateUnderAdditiveLayer,

        /// <summary>The renderer has conversion-relevant animated material
        /// state while the committed graph contains an unnormalized direct
        /// blend tree.</summary>
        ConversionStateUnderUnnormalizedDirectBlendTree,

        /// <summary>The renderer, its mesh or its material-slot count changed
        /// between preparation and the apply pass. This is an ordinary
        /// refusal, not a defect: another pass in this phase may legitimately
        /// have replaced the mesh or the slot array.</summary>
        RendererChangedSincePreparation,
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

        /// <summary>
        /// The already-prepared opaque result for this source material, or
        /// false when none exists yet. Clones are deduplicated avatar-wide by
        /// source material through this map, so two renderers referencing the
        /// same source material share one clone.
        /// </summary>
        internal bool TryGetOpaque(Material source, out Material opaque)
        {
            return opaqueBySource.TryGetValue(source, out opaque);
        }

        /// <summary>
        /// Registers one prepared opaque result. An <c>AlreadyOpaque</c>
        /// identity is recorded in the mapping but never enters
        /// <see cref="CreatedClones"/>, so the sweep can never destroy a
        /// source asset — a categorical guarantee, not a check. A clone is
        /// named <c>"&lt;source.name&gt; (AMUSE Opaque &lt;n&gt;)"</c> with
        /// <c>n</c> its zero-based position in <see cref="CreatedClones"/>,
        /// which is appended to in the barrier's deterministic order.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The source material already has a registered result. Callers
        /// consult <see cref="TryGetOpaque"/> first; a duplicate registration
        /// would silently fork the avatar-wide deduplication.
        /// </exception>
        internal void RegisterOpaque(Material source, Material opaque)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (opaque == null) throw new ArgumentNullException(nameof(opaque));

            opaqueBySource.Add(source, opaque);
            if (ReferenceEquals(opaque, source))
            {
                return;
            }

            opaque.name = source.name + " (AMUSE Opaque " +
                          createdClones.Count + ")";
            createdClones.Add(opaque);
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

    /// <summary>
    /// The finalized writes produced by validating the prepared record against
    /// live build state. A plain record of what will be written; not a mutation
    /// IR and not a transaction. Everything here is computed against the
    /// surviving set and the validated live <c>sharedMaterials</c> snapshot,
    /// never against barrier-time current state.
    /// </summary>
    internal sealed class AlphaSeparationFinalization
    {
        internal AlphaSeparationFinalization(
            IReadOnlyList<AlphaSeparationRendererWrite> writes)
        {
            if (writes == null) throw new ArgumentNullException(nameof(writes));

            var copy = new AlphaSeparationRendererWrite[writes.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                if (writes[index] == null)
                {
                    throw new ArgumentException(
                        "The finalization writes cannot contain null.",
                        nameof(writes));
                }

                copy[index] = writes[index];
            }

            Writes = Array.AsReadOnly(copy);
        }

        internal IReadOnlyList<AlphaSeparationRendererWrite> Writes { get; }
    }

    /// <summary>
    /// One renderer's finalized write, in deterministic apply order: curve
    /// edits first, then <c>sharedMesh</c>, then <c>sharedMaterials</c>.
    /// </summary>
    internal sealed class AlphaSeparationRendererWrite
    {
        internal AlphaSeparationRendererWrite(
            Renderer renderer,
            Mesh mesh,
            Material[] materials,
            IReadOnlyList<AlphaSeparationCurveEdit> curveEdits)
        {
            Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            Mesh = mesh;
            Materials = materials
                ?? throw new ArgumentNullException(nameof(materials));
            if (curveEdits == null)
            {
                throw new ArgumentNullException(nameof(curveEdits));
            }

            var copy = new AlphaSeparationCurveEdit[curveEdits.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = curveEdits[index];
            }

            CurveEdits = Array.AsReadOnly(copy);
        }

        internal Renderer Renderer { get; }

        /// <summary>
        /// The finalized mesh clone to assign, or null when no surviving
        /// <c>Split</c> slot requires one — a wholly-opaque-only write leaves
        /// the mesh and the submesh layout untouched.
        /// </summary>
        internal Mesh Mesh { get; }

        /// <summary>
        /// The complete new <c>sharedMaterials</c> array, built from the
        /// validated live snapshot. Unrelated slots and foreign assignments are
        /// carried through untouched.
        /// </summary>
        internal Material[] Materials { get; }

        internal IReadOnlyList<AlphaSeparationCurveEdit> CurveEdits { get; }
    }

    /// <summary>
    /// One material-swap curve rewrite: the live clip, the exact binding
    /// identity to write, and the complete keyframe curve with every time
    /// preserved and every value mapped. Nothing is written until apply.
    /// </summary>
    internal readonly struct AlphaSeparationCurveEdit
    {
        internal AlphaSeparationCurveEdit(
            VirtualClip clip,
            EditorCurveBinding binding,
            ObjectReferenceKeyframe[] curve)
        {
            Clip = clip ?? throw new ArgumentNullException(nameof(clip));
            Binding = binding;
            Curve = curve ?? throw new ArgumentNullException(nameof(curve));
        }

        internal VirtualClip Clip { get; }
        internal EditorCurveBinding Binding { get; }
        internal ObjectReferenceKeyframe[] Curve { get; }
    }
}
