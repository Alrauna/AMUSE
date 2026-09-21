using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Opens the transient unlock window. For every recognized locked
    /// material on the build avatar that passes every precondition of spec
    /// section 5, the service clones it, persists the clone through the
    /// NDMF asset saver, restores the clone through the vendor delegate,
    /// verifies the restore against the recorded serialization facts, and
    /// then swaps the clone into the slot arrays, the animation-closure
    /// curves, and NDMF's object registry. One unlocked clone exists per
    /// locked material across the whole avatar.
    /// <para>
    /// The locked original L is never mutated and never destroyed. The
    /// clone U is AMUSE-owned container content and the only destroyable
    /// side. This increment applies no transformation: material selection
    /// stays closed, no material is admitted for analysis, and the
    /// pipeline body is untouched, so the window is behavior-neutral by
    /// construction.
    /// </para>
    /// </summary>
    internal static class TransientUnlockSwapIn
    {
        /// <summary>
        /// The machine-side availability the swap-in consults: the vendor
        /// readiness flag and the two delegates. Production builds it from
        /// <see cref="TransientUnlockAvailability"/>. Tests inject
        /// attested and unattested values through it, so the vendor never
        /// has to be installed.
        /// </summary>
        internal sealed class Availability
        {
            /// <summary>
            /// Whether the Thry side is ready on this machine: the source
            /// digest attests and the seam resolves. When false, the
            /// service opens nothing and creates no clone.
            /// </summary>
            internal bool ThryAttested { get; }

            internal TransientUnlockDelegate Restore { get; }

            internal TransientRelockDelegate Relock { get; }

            /// <summary>The production availability.</summary>
            internal static Availability FromProduction()
            {
                if (!TransientUnlockAvailability.WindowVendorReady())
                {
                    return new Availability(false, null, null);
                }

                return new Availability(
                    true,
                    TransientUnlockAvailability.CreateProductionRestore(),
                    TransientUnlockAvailability.CreateProductionRelock());
            }

            internal Availability(
                bool thryAttested,
                TransientUnlockDelegate restore,
                TransientRelockDelegate relock)
            {
                ThryAttested = thryAttested;
                Restore = restore;
                Relock = relock;
            }
        }

        /// <summary>
        /// What one swap-in did, in counts a caller can observe without
        /// touching live objects. The counts are the falsifier surface:
        /// clones created, slots swapped, and the slots that took a named
        /// refusal instead of a swap.
        /// </summary>
        internal sealed class Summary
        {
            /// <summary>
            /// False when the per-build consent gate stopped the swap-in
            /// before anything else ran. No grant, no window.
            /// </summary>
            internal bool ConsentGatePassed { get; set; }

            internal int ClonesCreated { get; set; }

            internal int SlotsSwapped { get; set; }

            internal int RestoreMismatchSlots { get; set; }
        }

        /// <summary>
        /// Runs the swap-in against the build avatar. The consent gate is
        /// first and absolute: without this build's grant the window does
        /// not open, whatever the avatar carries.
        /// </summary>
        internal static Summary SwapIn(
            BuildContext context,
            TransientUnlockWindowState window,
            Availability availability,
            Func<Material, bool> originalShaderAttested = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (availability == null)
            {
                throw new ArgumentNullException(nameof(availability));
            }

            var summary = new Summary();
            if (!window.ConsentGranted)
            {
                // The per-build consent gate. A window that opens without
                // a grant is the named defect this guard exists to kill.
                return summary;
            }

            // The vendor-side precondition comes before any clone step by
            // contract: an unattested tool refuses through the renderer
            // pre-check and the service creates nothing at all.
            if (!availability.ThryAttested ||
                availability.Restore == null ||
                availability.Relock == null)
            {
                return summary;
            }

            summary.ConsentGatePassed = true;

            var attests = originalShaderAttested ??
                LockedMaterialIdentity.OriginalShaderAttested;
            var finishState = context.GetState<AmusePlatformFinishState>();

            // Pass one: group every locked slot by its locked material, in
            // deterministic renderer-then-slot order. The identity gate is
            // the classifier's own two-signal rule, so an unlocked
            // material with stale tags never enters the window.
            var lockedSlots = new List<LockedSlots>();
            foreach (var renderer in context.AvatarRootObject
                         .GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var slotIndex = 0;
                     slotIndex < materials.Length;
                     slotIndex++)
                {
                    var candidate = materials[slotIndex];
                    if (candidate == null ||
                        !LockedMaterialIdentity.RecognizedLockedIdentity(
                            candidate))
                    {
                        continue;
                    }

                    var entry = lockedSlots.Find(found =>
                        ReferenceEquals(found.Locked, candidate));
                    if (entry == null)
                    {
                        entry = new LockedSlots(candidate);
                        lockedSlots.Add(entry);
                    }

                    entry.Add(renderer, slotIndex);
                }
            }

            // The virtual clips are the only writable curve view while the
            // animator extension is active, so the closure remap reads and
            // writes through the same AnimationIndex the apply pass does.
            var animationIndex = context
                .Extension<AnimatorServicesContext>().AnimationIndex;

            // Pass two: one clone per locked material, then the whole
            // substitution for it. Registration precedes mutation, exactly
            // as the apply pass registers before it writes.
            foreach (var entry in lockedSlots)
            {
                // The original-shader precondition. A locked material
                // whose original does not attest is not window material;
                // the selection pre-check already named that refusal.
                if (!attests(entry.Locked))
                {
                    continue;
                }

                var pair = window.OpenPair(entry.Locked);
                if (pair.UnlockedClone == null)
                {
                    var clone = ClonePersistRestoreVerify(
                        context, entry.Locked, availability, attests,
                        out var mismatchReason);
                    summary.ClonesCreated++;
                    if (clone == null)
                    {
                        // The clone never swapped in and nothing
                        // references it, so destroying it now leaves no
                        // missing reference behind. The slot refusals are
                        // recorded before the destroy, in accounting
                        // order, and every slot keeps L.
                        foreach (var slot in entry.Slots)
                        {
                            RecordRefusal(
                                finishState, renderer: slot.Renderer,
                                slot.SlotIndex,
                                AlphaSeparationSlotRefusal
                                    .TransientUnlockRestoreMismatch);
                            summary.RestoreMismatchSlots++;
                        }

                        window.Remove(pair);
                        continue;
                    }

                    pair.UnlockedClone = clone;
                    ObjectRegistry.RegisterReplacedObject(
                        entry.Locked, clone);
                }

                foreach (var slot in entry.Slots)
                {
                    if (SwapSlot(entry, pair, slot))
                    {
                        summary.SlotsSwapped++;
                    }
                }

                RemapClosureCurves(animationIndex, context, entry, pair);
            }

            return summary;
        }

        /// <summary>
        /// Clones one locked material, persists the clone through the
        /// build's asset saver, restores it through the delegate, and
        /// verifies the restore against the recorded facts. The delegate's
        /// own outcome is never proof: a no-op restore and a partial
        /// restore both fail on facts. Returns the verified clone, or null
        /// with the first broken fact.
        /// </summary>
        private static Material ClonePersistRestoreVerify(
            BuildContext context,
            Material locked,
            Availability availability,
            Func<Material, bool> attests,
            out string mismatchReason)
        {
            // Exact name parity is load-bearing: the rename-animated
            // property suffix derives from the material name, so a clone
            // renamed for diagnostics would orphan every suffixed binding
            // at the re-lock.
            var clone = UnityEngine.Object.Instantiate(locked);
            clone.name = locked.name;
            context.AssetSaver.SaveAsset(clone);

            var outcome = availability.Restore(clone);
            mismatchReason = RestoreMismatchReason(locked, clone, attests);
            if (outcome == TransientUnlockVendorOutcome.Succeeded &&
                mismatchReason == null)
            {
                mismatchReason = null;
                return clone;
            }

            UnityEngine.Object.DestroyImmediate(clone, true);
            mismatchReason ??= "the vendor restore reported failure";
            return null;
        }

        /// <summary>
        /// The restore verification contract, all facts required before
        /// any swap: the clone shader name equals the recorded original
        /// shader name, the clone shader GUID equals the recorded GUID,
        /// the lock flag equals zero, the original shader asset still
        /// exists and passes its pins, and the generated locked shader
        /// asset still exists untouched, because L still needs it if the
        /// fallback fires. Returns null when every fact holds, else the
        /// first broken fact.
        /// </summary>
        private static string RestoreMismatchReason(
            Material locked,
            Material clone,
            Func<Material, bool> attests)
        {
            if (clone == null || clone.shader == null)
            {
                return "the clone has no shader after restore";
            }

            var recordedName = locked.GetTag(
                LockedMaterialIdentity.OriginalShaderTagName, true, null);
            if (string.IsNullOrEmpty(recordedName) ||
                !string.Equals(
                    clone.shader.name,
                    recordedName,
                    StringComparison.Ordinal))
            {
                return "the clone shader name does not match the " +
                    "recorded original shader name";
            }

            var recordedGuid = locked.GetTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                true,
                null);
            if (string.IsNullOrEmpty(recordedGuid) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    clone.shader, out var cloneGuid, out long _) ||
                !string.Equals(
                    cloneGuid,
                    recordedGuid.ToLowerInvariant(),
                    StringComparison.Ordinal))
            {
                return "the clone shader GUID does not match the " +
                    "recorded original shader GUID";
            }

            var flagName =
                LockedMaterialIdentity.OptimizerEnabledPropertyName;
            if (!clone.HasProperty(flagName) ||
                clone.GetFloat(flagName) != 0f)
            {
                return "the clone lock flag is not zero after restore";
            }

            var originalPath = AssetDatabase.GUIDToAssetPath(recordedGuid);
            var original = string.IsNullOrEmpty(originalPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Shader>(originalPath);
            if (original == null || !attests(locked))
            {
                return "the recorded original shader asset is missing " +
                    "or does not pass its pins";
            }

            if (string.IsNullOrEmpty(
                    AssetDatabase.GetAssetPath(locked.shader)))
            {
                return "the generated locked shader asset is missing";
            }

            return null;
        }

        /// <summary>
        /// Puts U into one slot, value-level on a fresh live read, so a
        /// foreign same-length assignment between passes carries through
        /// untouched. Returns false when the slot no longer holds L.
        /// </summary>
        private static bool SwapSlot(
            LockedSlots entry,
            TransientUnlockWindowState.SwappedPair pair,
            TransientUnlockWindowState.SlotSwap slot)
        {
            var renderer = slot.Renderer;
            if (renderer == null)
            {
                return false;
            }

            var live = renderer.sharedMaterials;
            if (slot.SlotIndex >= live.Length ||
                !ReferenceEquals(live[slot.SlotIndex], entry.Locked))
            {
                return false;
            }

            live[slot.SlotIndex] = pair.UnlockedClone;
            renderer.sharedMaterials = live;
            pair.AddSlot(renderer, slot.SlotIndex);
            return true;
        }

        /// <summary>
        /// Remaps every animation-closure material reference from L to U
        /// through the live curve-rewrite machinery, in deterministic
        /// clip-then-binding order, and records every rewrite on the pair
        /// as binding identity, so the close pass can invert exactly these
        /// bindings on the committed clips. A marker
        /// clip silently no-ops the write, which leaves the curve on L;
        /// L stays alive through the whole window, so that residue is
        /// harmless and never becomes a missing reference.
        /// </summary>
        private static void RemapClosureCurves(
            AnimationIndex animationIndex,
            BuildContext context,
            LockedSlots entry,
            TransientUnlockWindowState.SwappedPair pair)
        {
            foreach (var slot in entry.Slots)
            {
                var renderer = slot.Renderer;
                if (renderer == null)
                {
                    continue;
                }

                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    context.AvatarRootObject.transform);
                var rendererTypeName = renderer.GetType().FullName;

                foreach (var clip in animationIndex
                             .ClipsWithObjectCurves
                             .ToList())
                {
                    foreach (var binding in clip.GetObjectCurveBindings())
                    {
                        if (!LiveAnimationObservation
                                .TryParseMaterialSlotBinding(
                                    binding.propertyName,
                                    out var slotIndex) ||
                            slotIndex != slot.SlotIndex ||
                            !string.Equals(
                                binding.path,
                                rendererPath,
                                StringComparison.Ordinal) ||
                            !UnityAnimationEvidenceCapture
                                .IsCompatibleRendererType(
                                    binding.type.FullName,
                                    rendererTypeName))
                        {
                            continue;
                        }

                        RewriteCurve(entry, pair, clip, binding,
                            slot.SlotIndex);
                    }
                }
            }
        }

        /// <summary>
        /// Rewrites one object curve from L to U with every time preserved
        /// exactly, and records the rewrite on the pair. A curve whose
        /// every value already maps to itself is never written and never
        /// recorded, so the fallback inverts only real rewrites. Returns
        /// false when nothing changed.
        /// </summary>
        private static void RewriteCurve(
            LockedSlots entry,
            TransientUnlockWindowState.SwappedPair pair,
            VirtualClip clip,
            EditorCurveBinding binding,
            int slotIndex)
        {
            var curve = clip.GetObjectCurve(binding);
            if (curve == null || curve.Length == 0)
            {
                return;
            }

            var mapped = new ObjectReferenceKeyframe[curve.Length];
            var changed = false;
            for (var index = 0; index < curve.Length; index++)
            {
                // Unity equality, not managed identity: keyframe values
                // can come back as a second managed wrapper of the same
                // native material across the virtual clip boundary.
                if (ReferenceEquals(curve[index].value, entry.Locked) ||
                    (curve[index].value is Material keyframeMaterial &&
                     keyframeMaterial == entry.Locked))
                {
                    mapped[index] = new ObjectReferenceKeyframe
                    {
                        time = curve[index].time,
                        value = pair.UnlockedClone,
                    };
                    changed = true;
                }
                else
                {
                    mapped[index] = curve[index];
                }
            }

            if (!changed)
            {
                return;
            }

            clip.SetObjectCurve(binding, mapped);
            pair.AddBinding(binding, slotIndex);
        }

        private static void RecordRefusal(
            AmusePlatformFinishState finishState,
            Renderer renderer,
            int slotIndex,
            AlphaSeparationSlotRefusal cause)
        {
            finishState.RecordSlotRefusal(cause);
            AmuseReports.SlotSeparationRefusal(
                renderer, slotIndex, cause,
                renderer != null ? renderer.gameObject.name : null);
        }

        /// <summary>
        /// One locked material and every slot that holds it, discovered in
        /// deterministic renderer-then-slot order.
        /// </summary>
        private sealed class LockedSlots
        {
            internal Material Locked { get; }

            private readonly List<TransientUnlockWindowState.SlotSwap>
                slots = new List<TransientUnlockWindowState.SlotSwap>();

            internal IReadOnlyList<TransientUnlockWindowState.SlotSwap>
                Slots => slots;

            internal LockedSlots(Material locked)
            {
                Locked = locked;
            }

            internal void Add(Renderer renderer, int slotIndex)
            {
                slots.Add(new TransientUnlockWindowState.SlotSwap(
                    renderer, slotIndex));
            }
        }
    }
}
