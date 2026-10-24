using System;
using System.Collections.Generic;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;

namespace Alrauna.Amuse.Editor.DryRun
{
    /// <summary>
    /// The <see cref="ILockedMaterialValues"/> view of one locked
    /// material with its <see cref="LockedReproductionPlan"/> applied
    /// (A8.5). The dry-run mirror checks the consistency rules against
    /// this view, so the check reads the values the finished clone would
    /// hold while nothing is written. The view mirrors the plan applier
    /// of the build: every write lands on the plain original name, and
    /// the texture scale and offset write rides the plain original name
    /// too (A5). A name the plan does not write falls through to the
    /// locked material, which is the shader default for an unsaved slot.
    /// </summary>
    internal sealed class LockedPlanView : ILockedMaterialValues
    {
        private readonly ILockedMaterialValues locked;
        private readonly LockedReproductionPlan plan;

        internal LockedPlanView(
            ILockedMaterialValues locked,
            LockedReproductionPlan plan)
        {
            this.locked = locked
                ?? throw new ArgumentNullException(nameof(locked));
            this.plan = plan
                ?? throw new ArgumentNullException(nameof(plan));
        }

        public bool TryGetFloat(string propertyName, out float value)
        {
            if (plan.FloatWrites.TryGetValue(propertyName, out value))
            {
                return true;
            }

            return locked.TryGetFloat(propertyName, out value);
        }

        public bool TryGetVector(string propertyName, out Vector4 value)
        {
            if (plan.VectorWrites.TryGetValue(propertyName, out value))
            {
                return true;
            }

            return locked.TryGetVector(propertyName, out value);
        }

        public bool TryGetColor(string propertyName, out Color value)
        {
            if (plan.ColorWrites.TryGetValue(propertyName, out value))
            {
                return true;
            }

            return locked.TryGetColor(propertyName, out value);
        }

        public bool TryGetTexture(
            string propertyName, out LockedTextureValue value)
        {
            // The written texture carries its own token, size, and
            // scale and offset (A5.4). A texture the plan only moves
            // keeps the locked token and size, and takes the plan
            // scale and offset under the plain name.
            var hasWrittenTexture =
                plan.TextureWrites.TryGetValue(propertyName, out value);
            var hasScaleOffsetWrite =
                plan.TextureScaleOffsetWrites.TryGetValue(
                    propertyName, out var scaleOffset);

            if (!hasWrittenTexture && !hasScaleOffsetWrite)
            {
                return locked.TryGetTexture(propertyName, out value);
            }

            if (!hasWrittenTexture)
            {
                if (!locked.TryGetTexture(propertyName, out value))
                {
                    return false;
                }
            }

            if (hasScaleOffsetWrite)
            {
                value = new LockedTextureValue(
                    value.IdentityToken,
                    value.Width,
                    value.Height,
                    new Vector2(scaleOffset.x, scaleOffset.y),
                    new Vector2(scaleOffset.z, scaleOffset.w));
            }

            return true;
        }

        public int RenderQueue => plan.RenderQueue;

        public string RenderTypeTag => plan.RenderTypeTag;

        public string VrcFallbackTag => plan.VrcFallbackTag;

        public IReadOnlyList<string> OriginalKeywordsTag =>
            locked.OriginalKeywordsTag;

        public IReadOnlyList<string> Keywords => plan.Keywords;

        public bool ProjectIsLinear => locked.ProjectIsLinear;
    }
}
