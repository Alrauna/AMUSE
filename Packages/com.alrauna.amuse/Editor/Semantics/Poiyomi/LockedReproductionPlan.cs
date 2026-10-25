using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The values that the reproduction writes on the unlocked clone
    /// (A5). Every write is by original property name. The plan is plain
    /// data. The applier of the build performs the writes after the
    /// shader swap, and the dry run renders the same facts (A8.1).
    /// </summary>
    internal sealed class LockedReproductionPlan
    {
        internal LockedReproductionPlan(
            IReadOnlyDictionary<string, float> floatWrites,
            IReadOnlyDictionary<string, Vector4> vectorWrites,
            IReadOnlyDictionary<string, Color> colorWrites,
            IReadOnlyDictionary<string, Vector4> textureScaleOffsetWrites,
            IReadOnlyDictionary<string, LockedTextureValue> textureWrites,
            IReadOnlyList<string> keywords,
            int renderQueue,
            string renderTypeTag,
            string vrcFallbackTag)
        {
            FloatWrites = floatWrites;
            VectorWrites = vectorWrites;
            ColorWrites = colorWrites;
            TextureScaleOffsetWrites = textureScaleOffsetWrites;
            TextureWrites = textureWrites;
            Keywords = keywords;
            RenderQueue = renderQueue;
            RenderTypeTag = renderTypeTag;
            VrcFallbackTag = vrcFallbackTag;
        }

        /// <summary>Float writes by original property name.</summary>
        internal IReadOnlyDictionary<string, float> FloatWrites { get; }

        /// <summary>Vector writes by original property name.</summary>
        internal IReadOnlyDictionary<string, Vector4> VectorWrites { get; }

        /// <summary>Color writes by original property name.</summary>
        internal IReadOnlyDictionary<string, Color> ColorWrites { get; }

        /// <summary>
        /// Texture scale and offset writes by original texture name. The
        /// vector packs the Unity ST order, so x and y are the scale and
        /// z and w are the offset (A5.4).
        /// </summary>
        internal IReadOnlyDictionary<string, Vector4> TextureScaleOffsetWrites { get; }

        /// <summary>
        /// Texture writes by original texture name. The value carries the
        /// identity token for the Unity view (A5.2).
        /// </summary>
        internal IReadOnlyDictionary<string, LockedTextureValue> TextureWrites { get; }

        /// <summary>
        /// The keywords of the clone. The list holds the keyword part of
        /// the define runs, plus the tag keywords outside the surviving
        /// text (A5.9).
        /// </summary>
        internal IReadOnlyList<string> Keywords { get; }

        /// <summary>The render queue the clone takes (A5.10).</summary>
        internal int RenderQueue { get; }

        /// <summary>The RenderType override tag the clone takes (A5.10).</summary>
        internal string RenderTypeTag { get; }

        /// <summary>The VRCFallback override tag the clone takes (A5.10).</summary>
        internal string VrcFallbackTag { get; }
    }
}
