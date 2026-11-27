using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The route builder of the reviewed Poiyomi 9.3 route. It
    /// expands one Poiyomi texture name into one companion route
    /// under the coordinate rule of the 2026-11-23 review, spec
    /// section 6. A texture with selector 0 moves into a companion
    /// page under an identity transform, a zero pan, and a zero
    /// stochastic flag. A texture with selector 1, 2, 3, 4, 5, 8, or
    /// 9 stays bound and keyed. Selector 6 and 7, every other value,
    /// and NaN refuse, because the shader builds those coordinates
    /// from UV0 data.
    /// <para>
    /// The group files call <see cref="Standard"/> for every texture
    /// of the pattern. They write every irregular texture by hand as
    /// a plain <c>AtlasCompanionRoute</c>, in the style of the
    /// lilToon companion routes. There is no second way to declare a
    /// route.
    /// </para>
    /// </summary>
    internal static class PoiyomiCompanionRoutes
    {
        /// <summary>
        /// One companion route for a texture that follows the
        /// standard Poiyomi pattern. The route reads
        /// <c>&lt;T&gt;UV</c>, <c>&lt;T&gt;_ST</c>,
        /// <c>&lt;T&gt;Pan</c>, and <c>&lt;T&gt;Stochastic</c> when
        /// <paramref name="hasStochastic"/> is true.
        /// </summary>
        internal static AtlasCompanionRoute Standard(
            string texture,
            AtlasCompanionSampler sampler,
            string[] featureKeywords,
            bool hasStochastic)
        {
            var stochasticProperty = texture + "Stochastic";
            return Build(
                texture,
                sampler,
                featureKeywords,
                hasStochastic ? stochasticProperty : null,
                null);
        }

        /// <summary>
        /// One companion route for a texture that a clamp inline
        /// sampler reads. The route refuses its UV0 use, because a
        /// page texture needs Repeat on both axes.
        /// </summary>
        internal static AtlasCompanionRoute ClampSampled(
            string texture,
            string[] featureKeywords,
            bool hasStochastic)
        {
            var stochasticProperty = texture + "Stochastic";
            return Build(
                texture,
                AtlasCompanionSampler.InlineClamp,
                featureKeywords,
                hasStochastic ? stochasticProperty : null,
                UnheldClampSample(texture));
        }

        /// <summary>
        /// The use of one texture with a selector property. No
        /// feature keyword on means the shader never samples the
        /// texture. The selector values 1, 2, 3, 4, 5, 8, and 9 are
        /// the mesh channels UV0 to UV3, the panosphere, the world
        /// position, the local position, and the matcap view
        /// projection. The compare is exact, so a NaN or a fraction
        /// counts as UV0 and the route fails closed.
        /// </summary>
        internal static Func<Material, bool, AtlasRouteUse> SelectorUse(
            string selectorProperty,
            string[] featureKeywords)
        {
            return (material, outlineRow) =>
            {
                if (!AnyEnabled(material, featureKeywords))
                {
                    return AtlasRouteUse.Inert;
                }

                var selector = material.GetFloat(selectorProperty);
                if (IsOtherCoordinate(selector))
                {
                    return AtlasRouteUse.OtherCoordinates;
                }

                return AtlasRouteUse.Uv0;
            };
        }

        /// <summary>
        /// Whether a selector value names a mesh coordinate that the
        /// coordinate rule admits: 1, 2, 3, 4, 5, 8, or 9. The compare
        /// is exact. Selector 0 reads UV0, selectors 6 and 7 read UV0
        /// data, and a NaN or a fraction counts as UV0, so every other
        /// value refuses.
        /// </summary>
        internal static bool IsOtherCoordinate(float selector)
        {
            return selector == 1f || selector == 2f || selector == 3f ||
                   selector == 4f || selector == 5f || selector == 8f ||
                   selector == 9f;
        }

        /// <summary>
        /// The sentence for a selector that must be 0 for a UV0 use
        /// and is not. A NaN selector counts as UV0 in the shader,
        /// so it refuses here.
        /// </summary>
        internal static string UnheldSelector(
            Material material, string selectorProperty)
        {
            var selector = material.GetFloat(selectorProperty);
            return selector == 0f
                ? null
                : "The property '" + selectorProperty + "' is " +
                  Number(selector) + ". The route needs 0.";
        }

        /// <summary>
        /// Whether one feature keyword of the list is on. An empty
        /// list means the feature is always on, because its texture
        /// assignment is the gate.
        /// </summary>
        internal static bool AnyEnabled(
            Material material, string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
            {
                return true;
            }

            foreach (var keyword in keywords)
            {
                if (material.IsKeywordEnabled(keyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static AtlasCompanionRoute Build(
            string texture,
            AtlasCompanionSampler sampler,
            string[] featureKeywords,
            string stochasticProperty,
            Func<Material, string> clampSentence)
        {
            var selectorProperty = texture + "UV";
            var transformProperty = texture + "_ST";
            var panProperty = texture + "Pan";

            var conditions = new List<string>
            {
                selectorProperty,
                transformProperty,
                panProperty,
            };
            if (stochasticProperty != null)
            {
                conditions.Add(stochasticProperty);
            }

            return new AtlasCompanionRoute(
                texture,
                sampler,
                new[]
                {
                    (transformProperty, IdentityScaleOffset),
                    (panProperty, ZeroVector),
                },
                conditions.ToArray(),
                SelectorUse(selectorProperty, featureKeywords),
                UnheldValue(
                    selectorProperty,
                    stochasticProperty,
                    clampSentence,
                    SelectorUse(selectorProperty, featureKeywords)));
        }

        private static Func<Material, string> UnheldValue(
            string selectorProperty,
            string stochasticProperty,
            Func<Material, string> clampSentence,
            Func<Material, bool, AtlasRouteUse> use)
        {
            return material =>
            {
                // The route values guard a UV0 use only. Another
                // coordinate keeps the texture bound and keyed, so it
                // holds no route value.
                if (use(material, false) != AtlasRouteUse.Uv0)
                {
                    return null;
                }

                if (clampSentence != null)
                {
                    var clamp = clampSentence(material);
                    if (clamp != null)
                    {
                        return clamp;
                    }
                }

                var selector = UnheldSelector(
                    material, selectorProperty);
                if (selector != null)
                {
                    return selector;
                }

                if (stochasticProperty == null)
                {
                    return null;
                }

                var stochastic =
                    material.GetFloat(stochasticProperty);
                return stochastic == 0f
                    ? null
                    : "The property '" + stochasticProperty +
                      "' is " + Number(stochastic) +
                      ". The route needs 0, because stochastic" +
                      " sampling moves the coordinate.";
            };
        }

        private static Func<Material, string> UnheldClampSample(
            string texture)
        {
            return material =>
                "The texture '" + texture +
                "' samples with a clamp sampler on UV0. The route" +
                " needs a selector other than 0.";
        }

        private static string Number(float value)
        {
            return value.ToString(
                "0.######", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Vector4 IdentityScaleOffset =>
            new Vector4(1f, 1f, 0f, 0f);

        private static Vector4 ZeroVector => new Vector4(0f, 0f, 0f, 0f);
    }
}
