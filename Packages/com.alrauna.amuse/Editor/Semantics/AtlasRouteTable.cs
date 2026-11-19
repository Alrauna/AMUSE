using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// One admitted shader row's atlas route: the member texture that
    /// moves into a page, the assigned textures no reachable code
    /// samples, the local feature keywords a use may hold, the
    /// condition properties no animation or property block may touch,
    /// and the required values a material must hold.
    /// <para>
    /// The keyword list is an allowed list, so it is complete by
    /// construction. A keyword that is not on the list refuses the use.
    /// A feature that a review missed therefore cannot pass. The source
    /// review of 2026-11-19 fixed the entries against the pinned shader
    /// digests, so a pinned row cannot gain an unlisted feature.
    /// </para>
    /// <para>
    /// <see cref="HoldsRequiredValues"/> covers the scalar required
    /// values only. The texture transform has its own capture step and
    /// its own refusal, because a moved texture with non-identity
    /// tiling needs a different hint than a wrong feature value. The
    /// transform properties are listed separately with the identity
    /// vector each must hold.
    /// </para>
    /// </summary>
    internal sealed class AtlasRouteTable
    {
        /// <summary>
        /// The Poiyomi 9.3 main color route. The four admitted rows of
        /// <see cref="Poiyomi.PoiyomiSourceAttestation"/> share it,
        /// because the release and bundled files declare the same route
        /// surface. Member: <c>_MainTex</c> on UV0. Neutral: the five
        /// vendor textures a fresh material assigns.
        /// </summary>
        internal static readonly AtlasRouteTable PoiyomiMainColor =
            new AtlasRouteTable(
                "_MainTex",
                new[]
                {
                    "_ToonRamp",
                    "_SkinLUT",
                    "_ClothDFG",
                    "_DistortionFlowTexture",
                    "_DistortionFlowTexture1",
                },
                new[]
                {
                    "BSSBLOOMFOGTYPE_HEIGHT",
                    "VIGNETTE_MASKED",
                    "_LIGHTINGMODE_FLAT",
                    "_RIMSTYLE_POIYOMI",
                    "_RIM2STYLE_POIYOMI",
                    "_STOCHASTICMODE_DELIOT_HEITZ",
                    "_STYLIZEDREFLECTIONMODE_UNITYCHAN",
                },
                new[]
                {
                    "_MainTexUV",
                    "_MainPixelMode",
                    "_MainTexStochastic",
                    "_AlphaSharpenedA2C",
                    "_AlphaToCoverage",
                    "_RenderingAOBlockerEnabled",
                    "_MainTex_ST",
                    "_MainTexPan",
                },
                new (string, Vector4)[]
                {
                    ("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f)),
                    ("_MainTexPan", new Vector4(0f, 0f, 0f, 0f)),
                },
                HoldsPoiyomiMainColorRequiredValues);

        internal string MemberProperty { get; }

        internal IReadOnlyCollection<string> NeutralProperties { get; }

        internal IReadOnlyCollection<string> AllowedKeywords { get; }

        /// <summary>
        /// The properties whose value is a route condition. No clip and
        /// no property block may target one. The list holds the scalar
        /// conditions and the two transform properties.
        /// </summary>
        internal IReadOnlyCollection<string> ConditionProperties { get; }

        /// <summary>
        /// The transform properties of the member texture with the exact
        /// identity vector each must hold. The capture reads each
        /// through <see cref="Material.GetVector"/> and compares with
        /// exact equality.
        /// </summary>
        internal IReadOnlyList<(string Property, Vector4 Identity)>
            TransformProperties { get; }

        private readonly Func<Material, bool> holdsRequiredValues;

        private AtlasRouteTable(
            string memberProperty,
            string[] neutralProperties,
            string[] allowedKeywords,
            string[] conditionProperties,
            (string, Vector4)[] transformProperties,
            Func<Material, bool> holdsRequiredValues)
        {
            MemberProperty = memberProperty;
            NeutralProperties = Array.AsReadOnly(neutralProperties);
            AllowedKeywords = Array.AsReadOnly(allowedKeywords);
            ConditionProperties = Array.AsReadOnly(conditionProperties);
            TransformProperties = Array.AsReadOnly(transformProperties);
            this.holdsRequiredValues = holdsRequiredValues;
        }

        /// <summary>
        /// Whether the material holds every scalar required value of the
        /// row. The Poiyomi row demands the UV selector at zero, pixel
        /// mode at zero, stochastic sampling at zero, the ambient
        /// occlusion blocker at zero, and the sharpened alpha-to-coverage
        /// pair with at least one side at zero. Every compare is exact,
        /// because these are authored flags and not measured values.
        /// </summary>
        internal bool HoldsRequiredValues(Material material)
        {
            return holdsRequiredValues(material);
        }

        private static bool HoldsPoiyomiMainColorRequiredValues(
            Material material)
        {
            return material.GetFloat("_MainTexUV") == 0f &&
                   material.GetFloat("_MainPixelMode") == 0f &&
                   material.GetFloat("_MainTexStochastic") == 0f &&
                   (material.GetFloat("_AlphaSharpenedA2C") == 0f ||
                    material.GetFloat("_AlphaToCoverage") == 0f) &&
                   material.GetFloat("_RenderingAOBlockerEnabled") == 0f;
        }
    }
}
