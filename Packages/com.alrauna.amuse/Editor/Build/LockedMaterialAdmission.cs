using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The pure admission decision for one locked material (A9, A10).
    /// The method takes the aligned facts, one renderer fact, and the raw
    /// material binding names. It never touches a Unity object. The
    /// decision runs per locked material, over every collected slot,
    /// closure slots included. A refused material opens no pair.
    /// </summary>
    internal static class LockedMaterialAdmission
    {
        // The derived texture names the lock renames with the suffix
        // before their tail (fact 7, A10.2).
        private const string ScaleSuffix = "_ST";

        private const string TexelSizeSuffix = "_TexelSize";

        /// <summary>
        /// Returns the refusal for the locked material, or <see
        /// cref="AlphaSeparationSlotRefusal.None"/> when it may open a
        /// pair. <paramref name="materialBindingPropertyNames"/> are the
        /// raw EditorCurveBinding property names of the float and object
        /// reference bindings on the renderer paths of the slots. A
        /// property block wins over an animation mismatch (A9).
        /// </summary>
        internal static AlphaSeparationSlotRefusal Decide(
            AlignedLockedSource source,
            bool anyRendererHasPropertyBlock,
            IEnumerable<string> materialBindingPropertyNames)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // A9. The block would give the locked original and the
            // unlocked copy different values, whatever the animations
            // carry.
            if (anyRendererHasPropertyBlock)
            {
                return AlphaSeparationSlotRefusal
                    .LockedMaterialPropertyBlock;
            }

            if (materialBindingPropertyNames == null)
            {
                return AlphaSeparationSlotRefusal.None;
            }

            foreach (var raw in materialBindingPropertyNames)
            {
                if (BindingRefuses(source, raw))
                {
                    return AlphaSeparationSlotRefusal
                        .LockedMaterialAnimationMismatch;
                }
            }

            return AlphaSeparationSlotRefusal.None;
        }

        /// <summary>
        /// Applies A10.2 to one raw binding name. The method strips the
        /// material prefix with the shared helpers and the component
        /// suffix, and refuses when the name is a renamed name, names a
        /// property that is baked, unused, or renamed, or is a derived
        /// scale-offset or texel-size name of such a texture. A name
        /// outside these forms passes.
        /// </summary>
        private static bool BindingRefuses(
            AlignedLockedSource source,
            string raw)
        {
            if (raw == null)
            {
                return false;
            }

            if (!AnimationBindingResolution.TryStripPrefix(
                    raw,
                    AnimationBindingResolution.MaterialPrefix,
                    out var property) &&
                !AnimationBindingResolution.TryStripIndexedMaterialPrefix(
                    raw, out property))
            {
                // Only a material binding names a property of the locked
                // material (A10.2).
                return false;
            }

            // The component suffix of a color, vector, or scale-offset
            // binding names no property of its own (A10.2).
            if (AnimationBindingResolution.TrySplitComponent(
                    property, "xyzw", out var stem, out _) ||
                AnimationBindingResolution.TrySplitComponent(
                    property, "rgba", out stem, out _))
            {
                property = stem;
            }

            return NameRefuses(source, property);
        }

        private static bool NameRefuses(
            AlignedLockedSource source,
            string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            // A renamed name is a name the locked material answers only
            // under the renamed spelling (A10.2).
            foreach (var renamed in source.RenamedNames.Values)
            {
                if (string.Equals(renamed, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            if (source.States.TryGetValue(name, out var state) &&
                state != LockedPropertyState.Live)
            {
                // Baked, unused, and renamed all change what the locked
                // material reads (A10.2).
                return true;
            }

            // A derived name follows its texture (A10.2).
            if (name.EndsWith(ScaleSuffix, StringComparison.Ordinal))
            {
                return TextureRefuses(
                    source,
                    name.Substring(0, name.Length - ScaleSuffix.Length));
            }

            if (name.EndsWith(TexelSizeSuffix, StringComparison.Ordinal))
            {
                return TextureRefuses(
                    source,
                    name.Substring(
                        0, name.Length - TexelSizeSuffix.Length));
            }

            return false;
        }

        private static bool TextureRefuses(
            AlignedLockedSource source,
            string texture)
        {
            return source.States.TryGetValue(texture, out var state) &&
                state != LockedPropertyState.Live;
        }
    }
}
