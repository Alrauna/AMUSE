using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Closes the transient unlock window: the fourth and last
    /// PlatformFinish pass, ordered after the apply pass. It re-locks every
    /// open swapped pair through one batched vendor call, verifies the
    /// batch against the serialization facts after the call, and takes the
    /// per-pair fallback for every pair that did not lock.
    /// <para>
    /// The pass runs after every <see cref="nadena.dev.ndmf.animator.AnimatorServicesContext"/>
    /// scope has closed, extension-free. NDMF commits the virtual animator
    /// graph when the extension deactivates, and the commit's controller
    /// assignment makes the editor animator rebind, which applies the
    /// pre-commit animation state over the renderer material arrays. The
    /// close pass is therefore the last writer of the shipped references:
    /// it re-asserts the re-locked clone into every verified pair's slots,
    /// and on the fallback it re-asserts L explicitly and inverts the
    /// committed clips directly through <see cref="AnimationUtility"/>. It
    /// never depends on a deactivation side effect to produce the shipped
    /// state.
    /// </para>
    /// <para>
    /// The fallback inverts the whole swap-in remap, slot arrays first and
    /// then every recorded animation-closure binding, before any clone is
    /// destroyed, because a destroyed clone still referenced by a
    /// rewritten curve would serialize as a missing reference. Only the
    /// fallback destroys clones, and it destroys only the clones: the
    /// locked originals are never destroyed by anything. When the
    /// fallback cannot prove the committed-curve inversion complete, it
    /// keeps the unlocked clone alive and records the named
    /// <see cref="AlphaSeparationSlotRefusal.TransientUnlockCloneRetained"/>
    /// refusal for every slot of the pair, so the retention never passes
    /// silently. A build that completes holds zero open pairs, and no
    /// pair ships unlocked.
    /// </para>
    /// </summary>
    internal static class TransientUnlockWindowClose
    {
        internal const string PassName = "AMUSE transient unlock window close";

        /// <summary>The production entry: re-lock through the vendor seam.</summary>
        internal static void Execute(BuildContext context)
        {
            Execute(context, TransientUnlockAvailability.CreateProductionRelock());
        }

        /// <summary>
        /// The seam entry. A null re-lock delegate means the vendor seam
        /// stopped resolving, so every pair takes the fallback: the
        /// shipped state degrades to the locked originals, never to
        /// unlocked clones.
        /// </summary>
        internal static void Execute(
            BuildContext context, TransientRelockDelegate relock)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var window = context.GetState<TransientUnlockWindowState>();
            if (window.OpenPairs.Count == 0)
            {
                return;
            }

            // Open pairs can only exist when the window opened under a
            // positive lifecycle and a per-build consent grant. Reaching
            // the close pass with open pairs and anything else is an
            // integration defect, because a refused build must never have
            // opened the window at all.
            var finishState = context.GetState<AmusePlatformFinishState>();
            if (finishState.Lifecycle == null ||
                !finishState.Lifecycle.MayUsePositiveMutation)
            {
                throw new InvalidOperationException(
                    "The unlock window holds open pairs without a " +
                    "positive lifecycle, which the swap-in consent gate " +
                    "never allows.");
            }

            // One batched re-lock covers every open pair. The call runs
            // once; per-pair truth comes only from the verification after
            // it, so an already-locked-looking pair is never skipped on
            // its pre-call appearance.
            var clones = window.OpenPairs
                .Select(pair => pair.UnlockedClone)
                .ToList();
            if (relock != null)
            {
                relock(clones);
            }

            // Batch verification, every pair, no skips: the lock flag
            // equals one and the shader name is a locked form.
            var verified = new List<TransientUnlockWindowState.SwappedPair>();
            var failed = new List<TransientUnlockWindowState.SwappedPair>();
            foreach (var pair in window.OpenPairs)
            {
                if (PairRelocked(pair.UnlockedClone))
                {
                    verified.Add(pair);
                }
                else
                {
                    failed.Add(pair);
                }
            }

            // The shipped references are written here, after every
            // extension deactivation, so no later commit or rebind can
            // overwrite them. Verified pairs ship the re-locked clone;
            // failed pairs revert to the locked original. For a pair
            // whose slots apply transformed, the recorded finalization
            // write wins: the phase-end animator rebind re-applies the
            // authored clip's stale t=0 value over apply's slot write,
            // and re-asserting the clone there would clobber the
            // canonical material apply derived from the unlocked clone.
            var recorded = finishState.AppliedFinalization;
            foreach (var pair in verified)
            {
                ReassertShippedSlots(pair, recorded);
            }

            foreach (var pair in failed)
            {
                InvertReferences(context, pair);
            }

            foreach (var pair in failed)
            {
                foreach (var slot in pair.Slots)
                {
                    finishState.RecordSlotRefusal(
                        AlphaSeparationSlotRefusal
                            .TransientUnlockRelockFailed);
                    AmuseReports.SlotSeparationRefusal(
                        slot.Renderer,
                        slot.SlotIndex,
                        AlphaSeparationSlotRefusal
                            .TransientUnlockRelockFailed,
                        slot.Renderer != null
                            ? slot.Renderer.gameObject.name
                            : null);
                }

                // The clone is the only destroyable side of the window,
                // and only this path destroys it, only after every
                // reference is back on L. It is AMUSE-owned container
                // content, so the asset flag is required. A committed
                // curve the fallback could not enumerate keeps the clone
                // alive instead: a destroyed but still referenced clone
                // would serialize as a missing reference. That retention
                // is a named outcome, never a silent one: every slot of
                // the pair records that the unlocked clone stays alive
                // and that an animation may still apply it.
                if (CurveInversionWasComplete(context, pair))
                {
                    UnityEngine.Object.DestroyImmediate(
                        pair.UnlockedClone, true);
                    pair.UnlockedClone = null;
                }
                else
                {
                    foreach (var slot in pair.Slots)
                    {
                        finishState.RecordSlotRefusal(
                            AlphaSeparationSlotRefusal
                                .TransientUnlockCloneRetained);
                        AmuseReports.SlotSeparationRefusal(
                            slot.Renderer,
                            slot.SlotIndex,
                            AlphaSeparationSlotRefusal
                                .TransientUnlockCloneRetained,
                            slot.Renderer != null
                                ? slot.Renderer.gameObject.name
                                : null);
                    }
                }

                window.Remove(pair);
            }

            foreach (var pair in window.OpenPairs.ToList())
            {
                // A verified pair is closed: the re-locked clone ships in
                // the slot, and the untouched original stays alive and
                // unreferenced, exactly as the window contract requires.
                window.Remove(pair);
            }
        }

        /// <summary>
        /// The closed-pair facts, after the batch call: the lock flag
        /// equals one and the shader name is a locked form. Both, never
        /// either.
        /// </summary>
        private static bool PairRelocked(Material clone)
        {
            if (clone == null || clone.shader == null)
            {
                return false;
            }

            return clone.shader.name.StartsWith(
                       LockedMaterialIdentity.LockedShaderNamePrefix,
                       StringComparison.Ordinal) &&
                   clone.HasProperty(
                       LockedMaterialIdentity
                           .OptimizerEnabledPropertyName) &&
                   clone.GetFloat(
                       LockedMaterialIdentity
                           .OptimizerEnabledPropertyName) == 1f;
        }

        /// <summary>
        /// Restores every recorded slot of a verified pair to what the
        /// build intends it to hold, value-level on a fresh live read.
        /// For a slot the apply pass transformed, the recorded
        /// finalization write is the intent and is re-asserted, because
        /// the phase-end animator rebind may have overwritten it with the
        /// authored clip's stale t=0 value. For any other slot holding L,
        /// the re-locked clone is re-asserted, because the rebind leaves L
        /// where the swap-in put the clone. A slot holding the clone, and
        /// a slot a foreign pass filled with anything else, are never
        /// stomped: the window reverts or re-asserts exactly its own
        /// substitution.
        /// </summary>
        private static void ReassertShippedSlots(
            TransientUnlockWindowState.SwappedPair pair,
            AlphaSeparationFinalization recorded)
        {
            var clone = pair.UnlockedClone;
            foreach (var slot in pair.Slots)
            {
                var renderer = slot.Renderer;
                if (renderer == null)
                {
                    continue;
                }

                var live = renderer.sharedMaterials;
                if (slot.SlotIndex >= live.Length)
                {
                    continue;
                }

                var current = live[slot.SlotIndex];
                var holdsClone = ReferenceEquals(current, clone) ||
                    (current is Material cloneMaterial && cloneMaterial == clone);
                if (holdsClone)
                {
                    continue;
                }

                var holdsLocked = ReferenceEquals(current, pair.LockedOriginal) ||
                    (current is Material lockedMaterial &&
                        lockedMaterial == pair.LockedOriginal);
                if (!holdsLocked)
                {
                    continue;
                }

                live[slot.SlotIndex] =
                    RecordedWriteMaterial(recorded, renderer, slot.SlotIndex)
                    ?? clone;
                renderer.sharedMaterials = live;
            }
        }

        /// <summary>
        /// The material apply's recorded finalization intended one renderer
        /// slot to hold, or null when apply wrote nothing for that slot.
        /// Reads build state only; never the live array, which the
        /// animator rebind may already have clobbered.
        /// </summary>
        private static Material RecordedWriteMaterial(
            AlphaSeparationFinalization recorded,
            Renderer renderer,
            int slotIndex)
        {
            if (recorded == null)
            {
                return null;
            }

            foreach (var write in recorded.Writes)
            {
                if (!ReferenceEquals(write.Renderer, renderer) ||
                    slotIndex >= write.Materials.Length)
                {
                    continue;
                }

                return write.Materials[slotIndex];
            }

            return null;
        }

        /// <summary>
        /// Inverts the swap-in remap for one failed pair: every recorded
        /// slot back to L, then every recorded binding on the committed
        /// clips back to L. The order inside this method is slot arrays
        /// before curves, and the caller destroys nothing until every
        /// pair has passed through here.
        /// </summary>
        private static void InvertReferences(
            BuildContext context, TransientUnlockWindowState.SwappedPair pair)
        {
            var clone = pair.UnlockedClone;
            var locked = pair.LockedOriginal;

            foreach (var slot in pair.Slots)
            {
                var renderer = slot.Renderer;
                if (renderer == null)
                {
                    continue;
                }

                var live = renderer.sharedMaterials;
                var changed = false;
                for (var index = 0; index < live.Length; index++)
                {
                    if (ReferenceEquals(live[index], clone) ||
                        (live[index] is Material slotMaterial &&
                         slotMaterial == clone))
                    {
                        live[index] = locked;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = live;
                }
            }

            InvertCommittedCurves(context, pair);
        }

        /// <summary>
        /// Inverts every recorded binding on the committed clips: walks
        /// the committed controller graph through the host bindings the
        /// capture pass retained, matches recorded binding identity by
        /// transform path, property name, and renderer type, and rewrites
        /// every keyframe that references the clone back to L through
        /// <see cref="AnimationUtility"/>. The graph is committed by the
        /// time this pass runs, so these writes are final.
        /// </summary>
        private static void InvertCommittedCurves(
            BuildContext context, TransientUnlockWindowState.SwappedPair pair)
        {
            var clone = pair.UnlockedClone;
            var locked = pair.LockedOriginal;

            var recorded = new HashSet<(string, string)>();
            foreach (var rewritten in pair.Bindings)
            {
                recorded.Add((rewritten.Path, rewritten.PropertyName));
            }

            foreach (var clip in CommittedClips(context))
            {
                foreach (var binding in AnimationUtility
                             .GetObjectReferenceCurveBindings(clip))
                {
                    if (!recorded.Contains(
                            (binding.path, binding.propertyName)) ||
                        !LiveAnimationObservation
                            .TryParseMaterialSlotBinding(
                                binding.propertyName, out var slotIndex) ||
                        !UnityAnimationEvidenceCapture
                            .IsCompatibleRendererType(
                                binding.type.FullName,
                                RecordedTypeName(pair, slotIndex)))
                    {
                        continue;
                    }

                    var curve = AnimationUtility.GetObjectReferenceCurve(
                        clip, binding);
                    if (curve == null)
                    {
                        continue;
                    }

                    var mapped = new ObjectReferenceKeyframe[curve.Length];
                    var changed = false;
                    for (var index = 0; index < curve.Length; index++)
                    {
                        // Unity equality, not managed identity: keyframe
                        // values can come back as a second managed wrapper
                        // of the same native material.
                        if (ReferenceEquals(curve[index].value, clone) ||
                            (curve[index].value is Material keyframeMaterial &&
                             keyframeMaterial == clone))
                        {
                            mapped[index] = new ObjectReferenceKeyframe
                            {
                                time = curve[index].time,
                                value = locked,
                            };
                            changed = true;
                        }
                        else
                        {
                            mapped[index] = curve[index];
                        }
                    }

                    if (changed)
                    {
                        AnimationUtility.SetObjectReferenceCurve(
                            clip, binding, mapped);
                    }
                }
            }
        }

        private static string RecordedTypeName(
            TransientUnlockWindowState.SwappedPair pair, int slotIndex)
        {
            foreach (var rewritten in pair.Bindings)
            {
                if (rewritten.SlotIndex == slotIndex)
                {
                    return rewritten.TypeName;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether the fallback could enumerate and invert the committed
        /// graph for this pair. The retained host bindings must exist and
        /// the graph must enumerate cleanly; anything else means a
        /// committed curve may still reference the clone, and the caller
        /// must keep the clone alive instead of destroying it.
        /// </summary>
        private static bool CurveInversionWasComplete(
            BuildContext context, TransientUnlockWindowState.SwappedPair pair)
        {
            var bindings = context
                .GetState<AmusePlatformFinishState>().AnimatorBindings;
            if (bindings == null)
            {
                return false;
            }

            var graph = CommittedControllerGraph.Enumerate(
                context.AvatarRootObject, bindings);
            return graph.Refusal ==
                AvatarAnimationRefusal.None;
        }

        /// <summary>
        /// Every committed clip the avatar's innate animator sources
        /// currently carry, in deterministic source order. The host
        /// bindings re-read the controllers the extension committed, so
        /// this is the graph the build ships, not the pre-build graph.
        /// </summary>
        private static IEnumerable<AnimationClip> CommittedClips(
            BuildContext context)
        {
            var bindings = context
                .GetState<AmusePlatformFinishState>().AnimatorBindings;
            if (bindings == null)
            {
                yield break;
            }

            var seen = new HashSet<AnimationClip>();
            foreach (var entry in bindings.GetInnateControllers(
                         context.AvatarRootObject))
            {
                if (!(entry.Item2 is AnimatorController controller))
                {
                    continue;
                }

                foreach (var layer in controller.layers)
                {
                    foreach (var clip in ClipsInStateMachine(
                                 layer.stateMachine, seen))
                    {
                        yield return clip;
                    }
                }
            }
        }

        private static IEnumerable<AnimationClip> ClipsInStateMachine(
            AnimatorStateMachine stateMachine, HashSet<AnimationClip> seen)
        {
            if (stateMachine == null)
            {
                yield break;
            }

            foreach (var state in stateMachine.states)
            {
                if (state.state == null)
                {
                    continue;
                }

                foreach (var clip in ClipsInMotion(state.state.motion, seen))
                {
                    yield return clip;
                }
            }

            foreach (var child in stateMachine.stateMachines)
            {
                if (child.stateMachine == null)
                {
                    continue;
                }

                foreach (var clip in ClipsInStateMachine(
                             child.stateMachine, seen))
                {
                    yield return clip;
                }
            }
        }

        private static IEnumerable<AnimationClip> ClipsInMotion(
            Motion motion, HashSet<AnimationClip> seen)
        {
            if (motion is AnimationClip clip)
            {
                if (seen.Add(clip))
                {
                    yield return clip;
                }

                yield break;
            }

            // The swap-in remaps through the animation index, and that
            // index walks the whole virtual node graph, including blend
            // tree children. A material swap can therefore live under a
            // blend tree, and the committed walk must cover it or the
            // fallback leaves a surviving clone reference behind.
            if (motion is BlendTree blendTree)
            {
                foreach (var child in blendTree.children)
                {
                    foreach (var found in ClipsInMotion(
                                 child.motion, seen))
                    {
                        yield return found;
                    }
                }
            }
        }
    }
}
