using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The third PlatformFinish pass: validates every prepared candidate slot
    /// against live build state, finalizes against the surviving set, sweeps
    /// every transient no surviving slot references, and performs the single
    /// build-avatar mutation through <see cref="AmuseBuildOperation"/>.
    /// <para>
    /// It requires an active <see cref="AnimatorServicesContext"/> so it can
    /// reach the reactivated <c>AnimationIndex</c>: validation reads live
    /// binding identity, real binding type and parsed slot, which only a
    /// reactivated extension observes. Correctness never depends on pass
    /// adjacency — every live fact is revalidated here and the material
    /// arrays are built from a fresh live snapshot, never from barrier-time
    /// state.
    /// </para>
    /// </summary>
    internal static class AlphaSeparationApply
    {
        internal const string PassName = "AMUSE alpha separation apply";

        internal static void Execute(BuildContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var state = context.GetState<AmusePlatformFinishState>();
            AlphaSeparationFinalization finalization = null;
            AmuseBuildOperation.Execute(
                state.Lifecycle,
                context.AssetSaver,
                _ => PrepareSurvivingSet(context, state, out finalization),
                () => ApplyFinalization(finalization, state));
        }

        /// <summary>
        /// Validates every candidate slot, finalizes against the surviving
        /// set, and sweeps every transient no surviving slot references.
        /// Reads and AMUSE-owned transient objects only: no renderer, no clip
        /// and no source asset is written. This is the method
        /// <see cref="Execute"/> passes to <see cref="AmuseBuildOperation"/>
        /// as its prepare delegate.
        /// </summary>
        internal static AmusePreparationDecision PrepareSurvivingSet(
            BuildContext context,
            AmusePlatformFinishState state,
            out AlphaSeparationFinalization finalization)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (state == null) throw new ArgumentNullException(nameof(state));

            finalization = new AlphaSeparationFinalization(
                Array.Empty<AlphaSeparationRendererWrite>());

            var separation = state.Separation;
            if (separation == null || separation.Renderers.Count == 0)
            {
                return AmusePreparationDecision.NoMutation();
            }

            // The reactivated extension owns the only live view of the
            // committed animator graph. Validation reads binding identity,
            // real binding type and parsed slot through it, never clip names.
            var animationIndex = context.Extension<AnimatorServicesContext>()
                .AnimationIndex;

            // --- Validation. Every renderer and every candidate slot is
            // validated; no result short-circuits a sibling, and every
            // applicable refusal is recorded before anything is finalized.
            // Reads only: no renderer, no clip and no source asset is
            // written anywhere in this pass until apply.
            var rendererSurvivors =
                new List<List<PreparedSlotSeparation>>(separation.Renderers.Count);
            var rendererLive = new List<Material[]>(separation.Renderers.Count);
            var rendererTargetBindings = new List<
                List<(VirtualClip Clip,
                      EditorCurveBinding Binding,
                      ObjectReferenceKeyframe[] Curve,
                      int SlotIndex)>>(separation.Renderers.Count);

            foreach (var prepared in separation.Renderers)
            {
                var survivors = new List<PreparedSlotSeparation>();
                var targetBindings =
                    new List<(VirtualClip, EditorCurveBinding,
                              ObjectReferenceKeyframe[], int)>();
                var renderer = prepared.Target.Renderer;
                var currentMesh = SharedMeshOf(renderer);
                if (renderer == null ||
                    !ReferenceEquals(currentMesh, prepared.Target.ExpectedMesh) ||
                    renderer.sharedMaterials.Length !=
                        prepared.Target.ExpectedMaterialSlotCount ||
                    currentMesh == null ||
                    currentMesh.subMeshCount !=
                        prepared.Target.ExpectedMaterialSlotCount)
                {
                    // An ordinary refusal, not a defect: another pass in this
                    // phase may legitimately have replaced the mesh or the
                    // slot array. Every candidate slot of this renderer is
                    // dropped and nothing else is affected.
                    foreach (var candidate in prepared.CandidateSlots)
                    {
                        state.RecordSlotRefusal(
                            AlphaSeparationSlotRefusal
                                .RendererChangedSincePreparation);
                    }

                    rendererSurvivors.Add(survivors);
                    rendererLive.Add(null);
                    rendererTargetBindings.Add(targetBindings);
                    continue;
                }

                // The authoritative live statement of what the renderer
                // currently holds. Read exactly once; validation,
                // finalization and apply all work from this snapshot, and
                // unrelated same-length assignments by foreign passes are
                // carried through untouched.
                var live = renderer.sharedMaterials;

                // The captured object bindings of this renderer, keyed by the
                // exact triple a live binding is compared against. A target
                // binding whose triple is absent was not recorded by the
                // evidence, so the slot's runtime material behavior is not
                // fully described by what was proven.
                var capturedBindings = new HashSet<(string, string, string)>();
                foreach (var clipEvidence in prepared.Evidence.Clips)
                {
                    foreach (var objectBinding in clipEvidence.ObjectBindings)
                    {
                        capturedBindings.Add((objectBinding.Path,
                            objectBinding.TypeName,
                            objectBinding.PropertyName));
                    }
                }

                // Live target bindings, discovered by binding identity and
                // parsed slot. GetClipsForObjectPath returns the index's own
                // live set, so it is materialized before anything else reads
                // it; clips sharing a display name remain distinct objects
                // because the name participates in no lookup.
                var targetClips = animationIndex
                    .GetClipsForObjectPath(prepared.RendererPath)
                    .ToList();
                foreach (var clip in targetClips)
                {
                    foreach (var binding in clip.GetObjectCurveBindings())
                    {
                        if (!LiveAnimationObservation
                                .TryParseMaterialSlotBinding(
                                    binding.propertyName, out var slotIndex) ||
                            !string.Equals(
                                binding.path,
                                prepared.RendererPath,
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        targetBindings.Add((clip, binding,
                            clip.GetObjectCurve(binding), slotIndex));
                    }
                }

                foreach (var candidate in prepared.CandidateSlots)
                {
                    var refusal = ValidateCandidateSlot(
                        prepared, candidate, live, capturedBindings,
                        targetBindings);
                    if (refusal != AlphaSeparationSlotRefusal.None)
                    {
                        state.RecordSlotRefusal(refusal);
                        continue;
                    }

                    survivors.Add(candidate);
                }

                rendererSurvivors.Add(survivors);
                rendererLive.Add(live);
                rendererTargetBindings.Add(targetBindings);
            }

            // --- Finalization against the complete surviving set. Appended
            // indexing, the generated mesh layout, the material arrays and
            // the curve-edit set are all products of this step, computed from
            // the validated live snapshots.
            var writes = new List<AlphaSeparationRendererWrite>();
            for (var rendererIndex = 0;
                 rendererIndex < separation.Renderers.Count;
                 rendererIndex++)
            {
                var prepared = separation.Renderers[rendererIndex];
                var survivors = rendererSurvivors[rendererIndex];
                if (survivors.Count == 0)
                {
                    continue;
                }

                var live = rendererLive[rendererIndex];
                var targetBindings = rendererTargetBindings[rendererIndex];
                var splitSurvivors = survivors
                    .Where(slot => slot.Plan.Disposition ==
                                   SubmeshSeparationDisposition.Split)
                    .ToList();
                var originalCount = live.Length;

                // The complete new sharedMaterials array. Surviving Split
                // slots, unexamined slots and foreign assignments keep their
                // validated live entry; surviving WhollyOpaqueCandidate slots
                // and one appended slot per surviving Split slot carry the
                // mapped opaque results. Every lookup here was validated
                // above, so none can fail.
                var materials = new Material[originalCount + splitSurvivors.Count];
                Array.Copy(live, materials, originalCount);
                var materialChanged = splitSurvivors.Count > 0;
                foreach (var slot in survivors)
                {
                    if (slot.Plan.Disposition !=
                        SubmeshSeparationDisposition.WhollyOpaqueCandidate)
                    {
                        continue;
                    }

                    var slotIndex = slot.Plan.SourceMaterialBindingIndex;
                    var opaque = slot.OpaqueOfAdmitted[live[slotIndex]];
                    materialChanged |=
                        !ReferenceEquals(opaque, materials[slotIndex]);
                    materials[slotIndex] = opaque;
                }

                for (var appended = 0;
                     appended < splitSurvivors.Count;
                     appended++)
                {
                    var slot = splitSurvivors[appended];
                    materials[originalCount + appended] =
                        slot.OpaqueOfAdmitted[
                            live[slot.Plan.SourceMaterialBindingIndex]];
                }

                // Curve edits, in deterministic slot-then-binding order. A
                // surviving Split slot keeps its own curve and gains one new
                // binding per observed binding, each carrying identical times
                // and mapped values; a surviving WhollyOpaqueCandidate slot
                // has its own curves rewritten, and an edit whose every value
                // already maps to itself is skipped rather than written.
                var curveEdits = new List<AlphaSeparationCurveEdit>();
                foreach (var slot in survivors)
                {
                    var slotIndex = slot.Plan.SourceMaterialBindingIndex;
                    var split = slot.Plan.Disposition ==
                                SubmeshSeparationDisposition.Split;
                    foreach (var target in targetBindings)
                    {
                        if (target.SlotIndex != slotIndex ||
                            target.Curve == null ||
                            target.Curve.Length == 0)
                        {
                            continue;
                        }

                        var mappedCurve = MapCurve(
                            target.Curve,
                            slot.OpaqueOfAdmitted,
                            out var mappingChanged);
                        if (split)
                        {
                            // A Split slot's appended binding is new, so its
                            // curve is written even when every mapped value
                            // is the identity: "no value changes" never
                            // applies to a curve that does not exist yet.
                            curveEdits.Add(new AlphaSeparationCurveEdit(
                                target.Clip,
                                EditorCurveBinding.PPtrCurve(
                                    target.Binding.path,
                                    target.Binding.type,
                                    "m_Materials.Array.data[" +
                                    (originalCount +
                                     splitSurvivors.IndexOf(slot)) +
                                    "]"),
                                mappedCurve));
                        }
                        else if (mappingChanged)
                        {
                            curveEdits.Add(new AlphaSeparationCurveEdit(
                                target.Clip, target.Binding, mappedCurve));
                        }
                    }
                }

                // The finalized mesh layout, on the clone only, exactly per
                // the characterized recipe.
                Mesh mesh = null;
                if (splitSurvivors.Count > 0)
                {
                    if (prepared.MeshClone == null)
                    {
                        throw new InvalidOperationException(
                            "A surviving Split slot has no prepared mesh " +
                            "clone.");
                    }

                    mesh = FinalizeClone(
                        prepared,
                        splitSurvivors,
                        originalCount,
                        rendererIndex);
                }

                // A write exists only when at least one observable fact
                // changes; an all-AlreadyOpaque surviving set produces none.
                if (mesh != null || materialChanged || curveEdits.Count > 0)
                {
                    writes.Add(new AlphaSeparationRendererWrite(
                        prepared.Target.Renderer,
                        mesh,
                        materials,
                        curveEdits));
                }
            }

            // --- Sweep. One pass, after the surviving set is fixed: every
            // AMUSE-created transient no surviving slot references is
            // destroyed, and nothing else ever is. No reference counting.
            var referencedMaterials = new HashSet<Material>();
            var referencedMeshes = new HashSet<Mesh>();
            foreach (var survivors in rendererSurvivors)
            {
                foreach (var slot in survivors)
                {
                    foreach (var mapped in slot.OpaqueOfAdmitted.Values)
                    {
                        referencedMaterials.Add(mapped);
                    }
                }
            }

            for (var rendererIndex = 0;
                 rendererIndex < separation.Renderers.Count;
                 rendererIndex++)
            {
                if (rendererSurvivors[rendererIndex].Any(slot =>
                        slot.Plan.Disposition ==
                        SubmeshSeparationDisposition.Split))
                {
                    referencedMeshes.Add(
                        separation.Renderers[rendererIndex].MeshClone);
                }
            }

            foreach (var clone in separation.CreatedClones)
            {
                if (!referencedMaterials.Contains(clone))
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }
            }

            foreach (var prepared in separation.Renderers)
            {
                if (prepared.MeshClone != null &&
                    !referencedMeshes.Contains(prepared.MeshClone))
                {
                    UnityEngine.Object.DestroyImmediate(prepared.MeshClone);
                }
            }

            finalization = new AlphaSeparationFinalization(writes);
            return writes.Count > 0
                ? AmusePreparationDecision.Ready()
                : AmusePreparationDecision.NoMutation();
        }

        /// <summary>
        /// The single build-avatar mutation boundary: curve edits, then
        /// <c>sharedMesh</c>, then <c>sharedMaterials</c>, per renderer in
        /// deterministic order. An unexpected exception here is not caught
        /// and is build-fatal: the avatar may be half-mutated, which is
        /// precisely why it must not continue.
        /// </summary>
        internal static void ApplyFinalization(
            AlphaSeparationFinalization finalization,
            AmusePlatformFinishState state)
        {
            if (finalization == null)
                throw new ArgumentNullException(nameof(finalization));
            if (state == null) throw new ArgumentNullException(nameof(state));

            foreach (var write in finalization.Writes)
            {
                // The live array is read before anything is assigned so the
                // applied opaque-triangle accounting can see which surviving
                // slots' results actually differ from what the renderer held.
                var live = write.Renderer.sharedMaterials;

                foreach (var edit in write.CurveEdits)
                {
                    edit.Clip.SetObjectCurve(edit.Binding, edit.Curve);
                }

                if (write.Mesh != null)
                {
                    if (write.Renderer is SkinnedMeshRenderer skinned)
                    {
                        skinned.sharedMesh = write.Mesh;
                    }
                    else
                    {
                        write.Renderer.GetComponent<MeshFilter>()
                            .sharedMesh = write.Mesh;
                    }
                }

                write.Renderer.sharedMaterials = write.Materials;

                state.AppliedRendererCount++;
                state.AppliedOpaqueTriangleCount +=
                    CountOpaqueTriangles(write, live, state);
            }
        }

        /// <summary>
        /// Maps every keyframe value through the slot's prepared mapping with
        /// every time preserved exactly, reporting whether any value changed
        /// so an all-identity mapping can skip rewriting an existing curve —
        /// but never a Split slot's new appended binding, which must be
        /// written regardless.
        /// </summary>
        private static ObjectReferenceKeyframe[] MapCurve(
            ObjectReferenceKeyframe[] curve,
            IReadOnlyDictionary<Material, Material> mapping,
            out bool changed)
        {
            changed = false;
            foreach (var keyframe in curve)
            {
                var source = (Material)keyframe.value;
                if (!ReferenceEquals(mapping[source], source))
                {
                    changed = true;
                    break;
                }
            }

            var mapped = new ObjectReferenceKeyframe[curve.Length];
            for (var index = 0; index < curve.Length; index++)
            {
                mapped[index] = new ObjectReferenceKeyframe
                {
                    time = curve[index].time,
                    value = mapping[(Material)curve[index].value],
                };
            }

            return mapped;
        }

        /// <summary>
        /// Validates one candidate slot against live build state and returns
        /// the first applicable refusal, or <see
        /// cref="AlphaSeparationSlotRefusal.None"/> when the slot survives.
        /// </summary>
        private static AlphaSeparationSlotRefusal ValidateCandidateSlot(
            PreparedRendererSeparation prepared,
            PreparedSlotSeparation candidate,
            Material[] live,
            HashSet<(string, string, string)> capturedBindings,
            List<(VirtualClip Clip,
                  EditorCurveBinding Binding,
                  ObjectReferenceKeyframe[] Curve,
                  int SlotIndex)> targetBindings)
        {
            var slotIndex = candidate.Plan.SourceMaterialBindingIndex;
            foreach (var target in targetBindings)
            {
                if (target.SlotIndex != slotIndex)
                {
                    continue;
                }

                // The live clip is the authoritative marker check: editing
                // one would silently no-op, so the slot must be refused.
                if (target.Clip.IsMarkerClip)
                {
                    return AlphaSeparationSlotRefusal
                        .MarkerClipCarriesSlotBinding;
                }

                var triple = (target.Binding.path,
                    target.Binding.type.FullName,
                    target.Binding.propertyName);
                if (!capturedBindings.Contains(triple))
                {
                    return AlphaSeparationSlotRefusal
                        .SlotBindingAbsentFromEvidence;
                }

                // Every keyframe value must be a key in the slot's mapping.
                // A null or non-Material value cannot occur for a closed
                // renderer, and an empty curve was never discovered, but a
                // value this slot did not prove cannot be mapped.
                foreach (var keyframe in target.Curve)
                {
                    if (!(keyframe.value is Material source) ||
                        !candidate.OpaqueOfAdmitted.ContainsKey(source))
                    {
                        return AlphaSeparationSlotRefusal
                            .RuntimeMaterialValueNotMapped;
                    }
                }
            }

            // The live current material must also map, whether it arrives
            // from the current assignment or a curve keyframe: the condition
            // is identical and the arrival route carries no information. Fewer
            // live values than the captured admitted set is not a refusal.
            if (!(live[slotIndex] is Material current) ||
                !candidate.OpaqueOfAdmitted.ContainsKey(current))
            {
                return AlphaSeparationSlotRefusal.RuntimeMaterialValueNotMapped;
            }

            return AlphaSeparationSlotRefusal.None;
        }

        /// <summary>
        /// Finalizes the clone's layout exactly per the characterized recipe:
        /// both bounds levels are captured first, the submesh count is raised
        /// (which recalculates both), the surviving split slots' transparent
        /// triples stay on their submesh while their opaque triples move to
        /// one appended submesh each, then every submesh — untouched, split
        /// and appended alike — is restored to its source submesh's bounds,
        /// and the mesh bounds are restored last. Base-vertex normalization
        /// on the rewritten submeshes is the characterized representation
        /// change.
        /// </summary>
        private static Mesh FinalizeClone(
            PreparedRendererSeparation prepared,
            List<PreparedSlotSeparation> splitSurvivors,
            int appendedStart,
            int rendererOrdinal)
        {
            var clone = prepared.MeshClone;
            var meshBounds = clone.bounds;
            var sourceSubmeshBounds = new Bounds[clone.subMeshCount];
            for (var submesh = 0; submesh < clone.subMeshCount; submesh++)
            {
                sourceSubmeshBounds[submesh] = clone.GetSubMesh(submesh).bounds;
            }

            // The source index arrays are read before any write: nothing
            // rewrote them — a replaced mesh was already refused — and the
            // sharedMesh identity check proved the clone still mirrors the
            // mesh the barrier analyzed.
            var sourceIndices = new int[splitSurvivors.Count][];
            for (var appended = 0; appended < splitSurvivors.Count; appended++)
            {
                sourceIndices[appended] = clone.GetIndices(
                    splitSurvivors[appended].Plan.SourceSubmeshIndex);
            }

            clone.subMeshCount = appendedStart + splitSurvivors.Count;
            for (var appended = 0; appended < splitSurvivors.Count; appended++)
            {
                var slot = splitSurvivors[appended];
                var indices = sourceIndices[appended];
                clone.SetIndices(
                    OrdinalIndices(
                        indices, slot.Plan.TransparentTriangleOrdinals),
                    MeshTopology.Triangles,
                    slot.Plan.SourceSubmeshIndex,
                    calculateBounds: false);
                clone.SetIndices(
                    OrdinalIndices(
                        indices, slot.Plan.OpaqueTriangleOrdinals),
                    MeshTopology.Triangles,
                    appendedStart + appended,
                    calculateBounds: false);
            }

            // Output submesh -> the source submesh whose bounds it inherits:
            // the rewritten split submesh and its appended sibling inherit
            // their source submesh's bounds, and every untouched submesh is
            // restored to its own.
            var inherited = new Bounds[clone.subMeshCount];
            for (var submesh = 0; submesh < appendedStart; submesh++)
            {
                inherited[submesh] = sourceSubmeshBounds[submesh];
            }

            for (var appended = 0; appended < splitSurvivors.Count; appended++)
            {
                inherited[appendedStart + appended] = sourceSubmeshBounds[
                    splitSurvivors[appended].Plan.SourceSubmeshIndex];
            }

            for (var submesh = 0; submesh < clone.subMeshCount; submesh++)
            {
                var descriptor = clone.GetSubMesh(submesh);
                descriptor.bounds = inherited[submesh];
                clone.SetSubMesh(
                    submesh, descriptor,
                    UnityEngine.Rendering.MeshUpdateFlags
                        .DontRecalculateBounds);
            }

            clone.bounds = meshBounds;
            clone.name = prepared.Target.ExpectedMesh.name +
                         " (AMUSE Separated " + rendererOrdinal + ")";
            return clone;
        }

        /// <summary>
        /// Expands triangle ordinals into the absolute index triples they
        /// address. The snapshot's indices are absolute
        /// (<c>Mesh.GetIndices</c> applies the base vertex), and the
        /// characterized base-vertex normalization on the rewritten submesh
        /// preserves <c>baseVertex + firstVertex</c>.
        /// </summary>
        private static int[] OrdinalIndices(
            int[] indices,
            System.Collections.Generic.IReadOnlyList<int> ordinals)
        {
            var result = new int[ordinals.Count * 3];
            for (var triangle = 0; triangle < ordinals.Count; triangle++)
            {
                var first = ordinals[triangle] * 3;
                result[triangle * 3] = indices[first];
                result[triangle * 3 + 1] = indices[first + 1];
                result[triangle * 3 + 2] = indices[first + 2];
            }

            return result;
        }

        /// <summary>
        /// Counts the triangles an applied write actually moved to
        /// proven-opaque rendering: the opaque triangles of surviving slots
        /// whose material result differs from what the renderer held, plus
        /// the appended submeshes' triangles for surviving Split slots.
        /// </summary>
        private static int CountOpaqueTriangles(
            AlphaSeparationRendererWrite write,
            Material[] live,
            AmusePlatformFinishState state)
        {
            var originalCount = live.Length;
            PreparedRendererSeparation prepared = null;
            foreach (var candidate in state.Separation.Renderers)
            {
                if (ReferenceEquals(candidate.Target.Renderer, write.Renderer))
                {
                    prepared = candidate;
                    break;
                }
            }

            if (prepared == null)
            {
                throw new InvalidOperationException(
                    "An applied alpha-separation write has no prepared " +
                    "renderer record.");
            }

            var total = 0;
            for (var slot = 0; slot < originalCount; slot++)
            {
                if (ReferenceEquals(write.Materials[slot], live[slot]))
                {
                    continue;
                }

                foreach (var candidate in prepared.CandidateSlots)
                {
                    if (candidate.Plan.SourceMaterialBindingIndex == slot)
                    {
                        total += candidate.Plan.OpaqueTriangleOrdinals.Count;
                        break;
                    }
                }
            }

            if (write.Mesh != null)
            {
                for (var appended = originalCount;
                     appended < write.Materials.Length;
                     appended++)
                {
                    total += write.Mesh.GetIndices(appended).Length / 3;
                }
            }

            return total;
        }

        /// <summary>
        /// The live shared mesh of a prepared renderer, read the same way the
        /// analysis reads it, so the identity check compares like with like.
        /// </summary>
        private static Mesh SharedMeshOf(Renderer renderer)
        {
            if (renderer == null)
            {
                return null;
            }

            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }
    }
}
