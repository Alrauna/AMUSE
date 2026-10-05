using System;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Reads the effective render state every opaque-conversion family
    /// compares against: the render queue and the <c>RenderType</c> tag.
    /// </summary>
    internal static class EffectiveRenderState
    {
        /// <summary>
        /// The two canonical facts that are not shader properties. Neither is
        /// animation-reachable - Unity's material binding syntax is
        /// <c>material.&lt;PropertyName&gt;</c>, and no binding form addresses a
        /// material's render queue or an override tag - so neither belongs in
        /// the evidence request, whose job is to close the animation-relevant
        /// set. <c>renderQueue</c> already resolves an absent override to the
        /// shader's declared queue, so "an override exists" is an
        /// implementation detail this design does not model.
        /// </summary>
        internal static void ReadEffectiveRenderState(
            Material material,
            out int renderQueue,
            out string renderType)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            renderQueue = material.renderQueue;
            renderType = material.GetTag("RenderType", false);
        }
    }
}
