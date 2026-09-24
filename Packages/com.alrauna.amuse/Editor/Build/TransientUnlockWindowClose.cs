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
    /// PlatformFinish pass, ordered after the apply pass. The close
    /// outcome is reference reversion on every build path: every open
    /// pair returns from its unlocked clone U to its untouched locked
    /// original L through the fallback machinery, and U is destroyed
    /// only after the inversion completes. No build path calls the
    /// vendor.
    /// <para>
    /// The pass runs after every <see cref="nadena.dev.ndmf.animator.AnimatorServicesContext"/>
    /// scope has closed, extension-free. NDMF commits the virtual animator
    /// graph when the extension deactivates, and the commit's controller
    /// assignment makes the editor animator rebind, which applies the
    /// pre-commit animation state over the renderer material arrays. The
    /// close pass is therefore the last writer of the shipped references:
    /// it reverts every untransformed slot to L, re-asserts apply's
    /// recorded canonical write for the slots apply transformed, and
    /// inverts the committed clips directly through
    /// <see cref="AnimationUtility"/>. It never depends on a deactivation
    /// side effect to produce the shipped state.
    /// </para>
    /// <para>
    /// The reversion inverts the whole swap-in remap, slot arrays first
    /// and then every recorded animation-closure binding, before any
    /// copy is destroyed, because a destroyed material still referenced
    /// by a rewritten curve would serialize as a missing reference. Only
    /// the close destroys copies, and it destroys only AMUSE-owned ones:
    /// the locked originals and every output AMUSE did not create are
    /// never destroyed by anything. When the close cannot prove the
    /// committed-curve inversion complete, it keeps the unlocked copies
    /// alive and records the named
    /// <see cref="AlphaSeparationSlotRefusal.TransientUnlockCloneRetained"/>
    /// refusal for every slot of the pair, so the retention never passes
    /// silently. A build that completes holds zero open pairs, and no
    /// pair ships unlocked.
    /// </para>
    /// </summary>
    internal static class TransientUnlockWindowClose
    {
        internal const string PassName = "AMUSE transient unlock window close";

        /// <summary>
        /// The production entry and the only close. The close never
        /// submits any material to the vendor on any path: every pair
        /// reverts to its locked original through the fallback
        /// machinery.
        /// </summary>
        internal static void Execute(BuildContext context)
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

            // The close outcome: every pair reverts to
            // its locked original through the same fallback machinery —
            // slot arrays first, then every recorded closure binding —
            // and only then is the clone destroyed. The shipped
            // references are written here, after every extension
            // deactivation, so no later commit or rebind can overwrite
            // them. For a slot apply transformed, apply's recorded
            // finalization write still wins: the phase-end animator
            // rebind re-applies the authored clip's stale t=0 value over
            // apply's slot write, and re-asserting the clone there would
            // clobber the canonical material apply derived from the
            // unlocked clone.
            var recorded = finishState.AppliedFinalization;
            foreach (var pair in window.OpenPairs.ToList())
            {
                InvertReferences(context, pair);

                ReassertShippedSlots(pair, recorded);

                DestroyPairCopiesOrNameRetention(context, finishState, pair);

                window.Remove(pair);
            }
        }

        /// <summary>
        /// Destroys the pair's unlocked clone, but only after the caller
        /// inverted every reference: the committed graph was enumerated,
        /// so no rewritten curve can name a destroyed material. A curve
        /// the close cannot enumerate keeps the clone alive, and every
        /// affected slot gets the named retained-copy refusal, because a
        /// destroyed material still referenced by a curve would serialize
        /// as a missing reference.
        /// </summary>
        private static void DestroyPairCopiesOrNameRetention(
            BuildContext context,
            AmusePlatformFinishState finishState,
            TransientUnlockWindowState.SwappedPair pair)
        {
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
        }

        /// <summary>
        /// Re-asserts apply's recorded finalization write for the slots
        /// apply transformed, value-level on a fresh live read, because
        /// the phase-end animator rebind may have overwritten such a slot
        /// with the authored clip's stale t=0 value. The reversion has
        /// already put L in every slot the clone held, and a slot a
        /// foreign pass filled with anything else is never stomped: the
        /// window reverts or re-asserts exactly its own substitution.
        /// </summary>
        private static void ReassertShippedSlots(
            TransientUnlockWindowState.SwappedPair pair,
            AlphaSeparationFinalization recorded)
        {
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
                var holdsLocked = ReferenceEquals(current, pair.LockedOriginal) ||
                    (current is Material lockedMaterial &&
                        lockedMaterial == pair.LockedOriginal);
                if (!holdsLocked)
                {
                    continue;
                }

                var write = RecordedWriteMaterial(
                    recorded, renderer, slot.SlotIndex);
                if (write == null || write == pair.UnlockedClone)
                {
                    // No recorded write, or the write is the clone the
                    // reversion just removed: L is the shipped state.
                    continue;
                }

                live[slot.SlotIndex] = write;
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
        /// Inverts the swap-in remap for one pair: every recorded slot
        /// back to L, then every recorded binding on the committed clips
        /// back to L. The inverted name is the pair's unlocked clone. The
        /// order inside this method is slot arrays before curves, and
        /// the caller destroys nothing until every pair has passed
        /// through here.
        /// </summary>
        private static void InvertReferences(
            BuildContext context,
            TransientUnlockWindowState.SwappedPair pair)
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
                    if (NamesSwappedCopy(live[index], clone))
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
        /// Whether one slot or keyframe value names the copy the
        /// reversion must invert: the pair's unlocked clone. Unity
        /// equality, not managed identity: values can come back as a
        /// second managed wrapper of the same native material.
        /// </summary>
        private static bool NamesSwappedCopy(
            UnityEngine.Object candidate, Material clone)
        {
            var candidateMaterial = candidate as Material;
            return candidateMaterial == clone;
        }

        /// <summary>
        /// Inverts committed material-slot curves on the recorded transform
        /// paths and compatible renderer types. This includes appended
        /// bindings that the split apply creates. The pass rewrites every
        /// keyframe that references the pair's unlocked clone back to L.
        /// The graph is committed by this pass, so these writes are final.
        /// </summary>
        private static void InvertCommittedCurves(
            BuildContext context,
            TransientUnlockWindowState.SwappedPair pair)
        {
            var clone = pair.UnlockedClone;
            var locked = pair.LockedOriginal;

            var rendererTypesByPath =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var rewritten in pair.Bindings)
            {
                if (!rendererTypesByPath.TryGetValue(
                        rewritten.Path, out var rendererTypes))
                {
                    rendererTypes = new List<string>();
                    rendererTypesByPath.Add(
                        rewritten.Path, rendererTypes);
                }

                if (!rendererTypes.Contains(rewritten.TypeName))
                {
                    rendererTypes.Add(rewritten.TypeName);
                }
            }

            foreach (var clip in CommittedClips(context))
            {
                foreach (var binding in AnimationUtility
                             .GetObjectReferenceCurveBindings(clip))
                {
                    if (!rendererTypesByPath.TryGetValue(
                            binding.path, out var rendererTypes) ||
                        !LiveAnimationObservation
                            .TryParseMaterialSlotBinding(
                                binding.propertyName, out _))
                    {
                        continue;
                    }

                    var compatibleRenderer = false;
                    foreach (var rendererType in rendererTypes)
                    {
                        if (UnityAnimationEvidenceCapture
                            .IsCompatibleRendererType(
                                binding.type.FullName, rendererType))
                        {
                            compatibleRenderer = true;
                            break;
                        }
                    }

                    if (!compatibleRenderer)
                    {
                        continue;
                    }

                    var curve = AnimationUtility.GetObjectReferenceCurve(
                        clip, binding);
                    if (curve == null)
                    {
                        continue;
                    }

                    var changed = false;
                    for (var index = 0; index < curve.Length; index++)
                    {
                        if (NamesSwappedCopy(curve[index].value, clone))
                        {
                            changed = true;
                            break;
                        }
                    }

                    if (!changed)
                    {
                        continue;
                    }

                    var mapped = new ObjectReferenceKeyframe[curve.Length];
                    for (var index = 0; index < curve.Length; index++)
                    {
                        mapped[index] = NamesSwappedCopy(
                                curve[index].value, clone)
                            ? new ObjectReferenceKeyframe
                            {
                                time = curve[index].time,
                                value = locked,
                            }
                            : curve[index];
                    }

                    AnimationUtility.SetObjectReferenceCurve(
                        clip, binding, mapped);
                }
            }
        }

        /// <summary>
        /// Whether the fallback could enumerate and invert the committed
        /// graph for this pair. The retained host bindings must exist and
        /// the graph must enumerate cleanly; anything else means a
        /// committed curve may still reference the pair's unlocked clone,
        /// and the caller must keep the clone alive instead of destroying
        /// it.
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
