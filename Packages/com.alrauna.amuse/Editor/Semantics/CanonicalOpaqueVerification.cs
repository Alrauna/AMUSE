using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// The one Unity-level canonical opaque verification seam. It owns the
    /// three render-state facts that Unity defines for every family, the one
    /// canonical-fact scan, and the one clone, write, verify, destroy
    /// skeleton.
    /// <para>
    /// The float tuples stay per family. lilToon and Poiyomi measure
    /// different recipes, so their tuples never merge. This class holds only
    /// what both share: the render queue, the RenderType tag, the read-back
    /// order, and the clone lifecycle.
    /// </para>
    /// <para>
    /// The skeleton never saves and never writes the source. It clones with
    /// <c>new Material(source)</c> and destroys the clone on every failure
    /// path. The shader-identity sentence stays per family through
    /// <paramref name="shaderIdentityFailureText"/>, because the two
    /// frontends refuse with different words.
    /// </para>
    /// </summary>
    internal static class CanonicalOpaqueVerification
    {
        internal const int CanonicalOpaqueRenderQueue = 2000;
        internal const string RenderTypeTagName = "RenderType";
        internal const string CanonicalOpaqueRenderType = "Opaque";

        /// <summary>
        /// Reports the first non-canonical fact in a deterministic order:
        /// tuple order, then the effective render queue, then the
        /// RenderType tag. A property the material does not declare is
        /// reported by name. The caller decides whether that is a refusal
        /// or a defect.
        /// </summary>
        internal static bool TryFindNonCanonicalFact(
            Material candidate,
            IReadOnlyList<(string Property, float Value)> tuple,
            out string factName)
        {
            foreach (var (property, value) in tuple)
            {
                if (!candidate.HasProperty(property) ||
                    candidate.GetFloat(property) != value)
                {
                    factName = property;
                    return true;
                }
            }

            EffectiveRenderState.ReadEffectiveRenderState(
                candidate, out var queue, out var renderType);
            if (queue != CanonicalOpaqueRenderQueue)
            {
                factName = nameof(Material.renderQueue);
                return true;
            }

            if (!string.Equals(
                    renderType, CanonicalOpaqueRenderType, StringComparison.Ordinal))
            {
                factName = RenderTypeTagName;
                return true;
            }

            factName = null;
            return false;
        }

        /// <summary>
        /// Runs the shared clone lifecycle. It clones the source, swaps the
        /// shader when <paramref name="attestedTarget"/> is non-null, writes
        /// the tuple, sets the queue and the tag, and then calls
        /// <paramref name="familyWrites"/>. It re-reads every canonical fact
        /// through the shared scan plus <paramref name="familyScan"/>. It
        /// verifies the shader identity against the attested target, or
        /// against the source shader when the target is null. It destroys
        /// the clone on every failure path.
        /// <para>
        /// <paramref name="familyWrites"/> carries the family keyword work
        /// and the name clearing. <paramref name="familyScan"/> returns the
        /// extra non-canonical fact name, or null when the family adds no
        /// fact.
        /// </para>
        /// </summary>
        internal static Material PrepareCanonicalClone(
            Material source,
            Shader attestedTarget,
            IReadOnlyList<(string Property, float Value)> tuple,
            Action<Material> familyWrites,
            Func<Material, string> familyScan,
            string shaderIdentityFailureText)
        {
            var clone = new Material(source);
            var completed = false;
            try
            {
                if (attestedTarget != null)
                {
                    clone.shader = attestedTarget;
                }

                foreach (var (property, value) in tuple)
                {
                    clone.SetFloat(property, value);
                }

                clone.renderQueue = CanonicalOpaqueRenderQueue;
                clone.SetOverrideTag(RenderTypeTagName, CanonicalOpaqueRenderType);
                familyWrites?.Invoke(clone);

                if (TryFindNonCanonicalFact(clone, tuple, out var fact))
                {
                    throw new InvalidOperationException(
                        "Generated opaque material did not read back canonical '" +
                        fact + "'.");
                }

                var familyFact = familyScan?.Invoke(clone);
                if (familyFact != null)
                {
                    throw new InvalidOperationException(
                        "Generated opaque material did not read back canonical '" +
                        familyFact + "'.");
                }

                if (clone.shader != (attestedTarget ?? source.shader))
                {
                    throw new InvalidOperationException(shaderIdentityFailureText);
                }

                completed = true;
                return clone;
            }
            finally
            {
                if (!completed)
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }
            }
        }
    }
}
