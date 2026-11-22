using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// One texture property that a row samples at the main UV0
    /// coordinate under a material condition. An assigned texture on
    /// a UV0 route moves into a companion page. The page uses the main
    /// page layout, scaled by a power of two.
    /// <para>
    /// The transform properties must hold their identity, because the
    /// page moves one UV region. The condition properties decide the
    /// use, so no clip and no property block may touch them while the
    /// texture is assigned.
    /// </para>
    /// </summary>
    internal sealed class AtlasCompanionRoute
    {
        internal string Property { get; }

        internal AtlasCompanionSampler Sampler { get; }

        internal IReadOnlyList<(string Property, Vector4 Identity)>
            TransformProperties { get; }

        /// <summary>
        /// The material properties that decide <see cref="UseOf"/> or
        /// <see cref="UnheldRequiredValue"/>.
        /// </summary>
        internal IReadOnlyCollection<string> ConditionProperties { get; }

        private readonly Func<Material, bool, AtlasRouteUse> use;

        private readonly Func<Material, string> unheldRequiredValue;

        internal AtlasCompanionRoute(
            string property,
            AtlasCompanionSampler sampler,
            (string, Vector4)[] transformProperties,
            string[] conditionProperties,
            Func<Material, bool, AtlasRouteUse> use,
            Func<Material, string> unheldRequiredValue = null)
        {
            Property = property;
            Sampler = sampler;
            TransformProperties = Array.AsReadOnly(transformProperties);
            ConditionProperties = Array.AsReadOnly(conditionProperties);
            this.use = use;
            this.unheldRequiredValue = unheldRequiredValue;
        }

        /// <summary>
        /// How the material uses the texture. The outline flag is true
        /// for a row whose shader has an outline pass.
        /// </summary>
        internal AtlasRouteUse UseOf(Material material, bool outlineRow)
        {
            return use(material, outlineRow);
        }

        /// <summary>
        /// The sentence for a route value the material does not hold
        /// on a UV0 use, or null.
        /// </summary>
        internal string UnheldRequiredValue(Material material)
        {
            return unheldRequiredValue?.Invoke(material);
        }
    }
}
