using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The unlock window's per-build state: the consent grant and the open
    /// swapped pairs. One unlocked clone exists per locked material across
    /// the whole avatar, so opening a pair for a material that already has
    /// one returns the open pair. The window close drains the state, so a
    /// build that completes holds zero open pairs.
    /// <para>
    /// Like <c>AmusePlatformFinishState.AnimatorBindings</c> this carries
    /// live Unity objects. It is transient host capability, not proof
    /// evidence, and deliberately stays outside the captured-evidence
    /// graph's no-live-Unity-object guarantee.
    /// </para>
    /// </summary>
    internal sealed class TransientUnlockWindowState
    {
        /// <summary>
        /// True only when this build's consent granted the unlock window.
        /// The swap-in service refuses to open the window without it, so
        /// the gate holds even when a caller reaches the service directly.
        /// </summary>
        internal bool ConsentGranted { get; set; }

        private readonly List<SwappedPair> openPairs =
            new List<SwappedPair>();

        /// <summary>The open pairs, in swap-in order.</summary>
        internal IReadOnlyList<SwappedPair> OpenPairs => openPairs;

        /// <summary>
        /// The open pair for one locked material, or null when no pair is
        /// open for it.
        /// </summary>
        internal SwappedPair PairFor(Material lockedOriginal)
        {
            foreach (var pair in openPairs)
            {
                if (ReferenceEquals(pair.LockedOriginal, lockedOriginal))
                {
                    return pair;
                }
            }

            return null;
        }

        /// <summary>
        /// The material the swapped world shows for one observed material:
        /// the open pair's unlocked clone for a locked original, otherwise
        /// the material itself. The capture reads the world through this
        /// view while the window is open, so a material-swap keyframe read
        /// from the pre-swap committed graph resolves to the same one
        /// clone the slot arrays and the live clips hold. Reads live state
        /// and mutates nothing.
        /// </summary>
        internal Material SwappedView(Material material)
        {
            if (material == null)
            {
                return null;
            }

            foreach (var pair in openPairs)
            {
                if (ReferenceEquals(pair.LockedOriginal, material) ||
                    (material is Material asMaterial &&
                        asMaterial == pair.LockedOriginal))
                {
                    return pair.UnlockedClone;
                }
            }

            return material;
        }

        /// <summary>
        /// Opens the one pair for a locked material. One clone per locked
        /// material across the avatar: an existing pair for the same
        /// material wins, and a second call never clones again.
        /// </summary>
        internal SwappedPair OpenPair(Material lockedOriginal)
        {
            if (lockedOriginal == null)
            {
                throw new ArgumentNullException(nameof(lockedOriginal));
            }

            var existing = PairFor(lockedOriginal);
            if (existing != null)
            {
                return existing;
            }

            var pair = new SwappedPair(lockedOriginal);
            openPairs.Add(pair);
            return pair;
        }

        /// <summary>
        /// Removes one pair whose window close or fallback finished. An
        /// absent pair is a caller defect: removing it twice means the
        /// close pass ran its fallback twice against the same references.
        /// </summary>
        internal void Remove(SwappedPair pair)
        {
            if (!openPairs.Remove(pair))
            {
                throw new InvalidOperationException(
                    "The unlock window removed a pair it did not hold.");
            }
        }

        /// <summary>
        /// One swapped pair: the untouched locked original L and its
        /// unlocked clone U, plus every reference the swap-in rewrote, so
        /// the fallback can invert exactly the substitution and nothing a
        /// later pass added. L is never destroyed. U is the only
        /// destroyable side of the whole window, and only the fallback
        /// destroys it.
        /// </summary>
        internal sealed class SwappedPair
        {
            internal Material LockedOriginal { get; }
            internal Material UnlockedClone { get; set; }

            private readonly List<SlotSwap> slots = new List<SlotSwap>();
            private readonly List<RewrittenBinding> bindings =
                new List<RewrittenBinding>();

            /// <summary>Every slot that held L and now holds U.</summary>
            internal IReadOnlyList<SlotSwap> Slots => slots;

            /// <summary>
            /// Every object curve the swap-in rewrote from L to U.
            /// </summary>
            internal IReadOnlyList<RewrittenBinding> Bindings => bindings;

            internal SwappedPair(Material lockedOriginal)
            {
                LockedOriginal = lockedOriginal
                    ?? throw new ArgumentNullException(
                        nameof(lockedOriginal));
            }

            /// <summary>
            /// Records one slot substitution. A slot the pair already
            /// records is a caller defect: it would make the fallback
            /// restore the same array twice.
            /// </summary>
            internal void AddSlot(Renderer renderer, int slotIndex)
            {
                foreach (var existing in slots)
                {
                    if (ReferenceEquals(existing.Renderer, renderer) &&
                        existing.SlotIndex == slotIndex)
                    {
                        throw new InvalidOperationException(
                            "The unlock window recorded one slot twice.");
                    }
                }

                slots.Add(new SlotSwap(renderer, slotIndex));
            }

            /// <summary>
            /// Records one rewritten object curve, keyed by binding
            /// identity: transform path, material-slot property name,
            /// renderer type full name, and parsed slot index. The record
            /// holds no live clip reference, so the fallback can match the
            /// same binding on the committed clips after the animator
            /// extension has closed.
            /// </summary>
            internal void AddBinding(EditorCurveBinding binding, int slotIndex)
            {
                bindings.Add(new RewrittenBinding(
                    binding.path,
                    binding.propertyName,
                    binding.type.FullName,
                    slotIndex));
            }
        }

        /// <summary>One slot that held L and holds U while the window is open.</summary>
        internal readonly struct SlotSwap
        {
            internal Renderer Renderer { get; }
            internal int SlotIndex { get; }

            internal SlotSwap(Renderer renderer, int slotIndex)
            {
                Renderer = renderer;
                SlotIndex = slotIndex;
            }
        }

        /// <summary>
        /// One object curve the swap-in rewrote, recorded as binding
        /// identity: the transform path, the material-slot property name,
        /// the renderer type full name the binding carries, and the parsed
        /// slot index. The fallback inverts the committed clips after the
        /// animator extension has closed, so the record holds no live clip
        /// reference; matching proceeds by binding identity, never by clip
        /// name.
        /// </summary>
        internal readonly struct RewrittenBinding
        {
            internal string Path { get; }
            internal string PropertyName { get; }
            internal string TypeName { get; }
            internal int SlotIndex { get; }

            internal RewrittenBinding(
                string path,
                string propertyName,
                string typeName,
                int slotIndex)
            {
                Path = path;
                PropertyName = propertyName;
                TypeName = typeName;
                SlotIndex = slotIndex;
            }
        }
    }
}
