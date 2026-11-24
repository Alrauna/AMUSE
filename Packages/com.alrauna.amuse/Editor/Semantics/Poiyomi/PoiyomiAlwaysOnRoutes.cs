using System;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The feature group 'Always on' of the reviewed Poiyomi 9.3
    /// route. The review record of 2026-11-23, sections 4.1, 4.3,
    /// and 6, carries the rows of this group. The group routes the
    /// seven companions that the shader samples whenever their
    /// texture is assigned, plus the light data map that never
    /// samples in an admitted use. The table rule of the group
    /// requires <c>_LightingMapMode</c> not 4 while
    /// <c>PROP_LIGHTDATASDFMAP</c> is on.
    /// <para>
    /// A <c>PROP_&lt;texture&gt;</c> keyword never gates a family or
    /// a route. The Thry editor can leave it out of step with its
    /// texture, so the bump map route reads no keyword at all.
    /// </para>
    /// </summary>
    internal static class PoiyomiAlwaysOnRoutes
    {
        /// <summary>
        /// The four declared keywords of the group. The three
        /// <c>PROP_LIGHTING*</c> keywords are Companion verdicts.
        /// <c>PROP_LIGHTDATASDFMAP</c> is Conditional and owns the
        /// light map mode rule.
        /// </summary>
        internal static readonly string[] AllowedKeywords =
        {
            "PROP_LIGHTINGAOMAPS",
            "PROP_LIGHTINGDETAILSHADOWMAPS",
            "PROP_LIGHTINGSHADOWMASKS",
            "PROP_LIGHTDATASDFMAP",
        };

        /// <summary>
        /// The group owns no neutral texture. The member texture
        /// stays with the main color route.
        /// </summary>
        internal static readonly string[] NeutralTextures =
            Array.Empty<string>();

        /// <summary>
        /// The property of the group table rule. It guards the light
        /// data map sample, which reads UV0 derivatives in mode 4.
        /// </summary>
        internal static readonly string[] ConditionProperties =
        {
            "_LightingMapMode",
        };

        /// <summary>
        /// One route per companion texture of the group in the
        /// record, section 4.3. The order follows the declaration
        /// lines of the record.
        /// </summary>
        internal static AtlasCompanionRoute[] Routes()
        {
            return new[]
            {
                // The bump map has no declared texture keyword, so
                // its texture assignment is the only gate. The
                // record names _BumpMapStochastic for this texture.
                PoiyomiCompanionRoutes.Standard(
                    "_BumpMap",
                    AtlasCompanionSampler.Member,
                    Array.Empty<string>(),
                    true),

                // The alpha mask samples only while
                // _MainAlphaMaskMode is not 0. The vendor default is
                // 2, so the assignment gates the sample there too.
                AlphaMaskRoute(),

                PoiyomiCompanionRoutes.Standard(
                    "_LightingAOMaps",
                    AtlasCompanionSampler.Member,
                    new[] { "PROP_LIGHTINGAOMAPS" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_LightingDetailShadowMaps",
                    AtlasCompanionSampler.Member,
                    new[] { "PROP_LIGHTINGDETAILSHADOWMAPS" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_LightingShadowMasks",
                    AtlasCompanionSampler.Member,
                    new[] { "PROP_LIGHTINGSHADOWMASKS" },
                    false),

                // The light data map samples only in light map mode
                // 4. The table rule refuses mode 4, so the texture
                // never samples in an admitted use. It moves into no
                // page and leaves the material key. It carries no
                // conditions, because no material value can turn its
                // sample on.
                LightDataSdfMapRoute(),

                // The outline mask compiles only in the outline
                // pass. It samples the mesh UV0 there with the
                // inline linear repeat sampler, so selector 0 moves
                // it beside the main page.
                PoiyomiCompanionRoutes.Standard(
                    "_OutlineMask",
                    AtlasCompanionSampler.InlineLinearRepeat,
                    Array.Empty<string>(),
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_OutlineTexture",
                    AtlasCompanionSampler.Member,
                    Array.Empty<string>(),
                    false),
            };
        }

        /// <summary>
        /// The table rule of the group. The light data map sample
        /// branch reads UV0 derivatives in light map mode 4. The
        /// route refuses that mode while the keyword is on. Every
        /// other mode keeps the map uncompiled or unread.
        /// </summary>
        internal static string UnheldRequiredValue(Material material)
        {
            if (!material.IsKeywordEnabled("PROP_LIGHTDATASDFMAP"))
            {
                return null;
            }

            var mode = material.GetFloat("_LightingMapMode");
            return mode != 4f
                ? null
                : "The property '_LightingMapMode' is " +
                  Number(mode) + ". The route needs a mode other" +
                  " than 4, because the light data map samples UV0" +
                  " derivatives in mode 4.";
        }

        /// <summary>
        /// One dead value family per keyword of the group, with the
        /// exact members of the record, section 6. The keyword is
        /// the only switch of each family. The strength reads of the
        /// lighting data maps sit outside the texture pragma, but
        /// they read the constant 1 there, so the render holds while
        /// the keyword is off.
        /// </summary>
        internal static AtlasDeadValueFamily[] Families()
        {
            return new[]
            {
                Family(
                    "PROP_LIGHTINGAOMAPS",
                    LightingAOMapsMembers),
                Family(
                    "PROP_LIGHTINGDETAILSHADOWMAPS",
                    LightingDetailShadowMapsMembers),
                Family(
                    "PROP_LIGHTINGSHADOWMASKS",
                    LightingShadowMasksMembers),
                Family(
                    "PROP_LIGHTDATASDFMAP",
                    LightDataSdfMembers),
            };
        }

        /// <summary>
        /// The alpha mask route. It follows the standard pattern and
        /// adds the blend mode gate of the record. Mode 0 compiles
        /// the sample out, so the use is inert there.
        /// </summary>
        private static AtlasCompanionRoute AlphaMaskRoute()
        {
            var selectorUse = PoiyomiCompanionRoutes.SelectorUse(
                "_AlphaMaskUV", Array.Empty<string>());

            Func<Material, bool, AtlasRouteUse> use =
                (material, outlineRow) =>
                {
                    if (material.GetFloat("_MainAlphaMaskMode") == 0f)
                    {
                        return AtlasRouteUse.Inert;
                    }

                    return selectorUse(material, outlineRow);
                };

            return new AtlasCompanionRoute(
                "_AlphaMask",
                AtlasCompanionSampler.Member,
                new[]
                {
                    ("_AlphaMask_ST", IdentityScaleOffset),
                    ("_AlphaMaskPan", ZeroVector),
                },
                new[] { "_AlphaMaskUV", "_AlphaMask_ST", "_AlphaMaskPan" },
                use,
                material =>
                    use(material, false) == AtlasRouteUse.Uv0
                        ? PoiyomiCompanionRoutes.UnheldSelector(
                            material, "_AlphaMaskUV")
                        : null);
        }

        /// <summary>
        /// The light data map route. Its use is inert in every
        /// admitted state, so the texture joins no page and no
        /// material value decides it.
        /// </summary>
        private static AtlasCompanionRoute LightDataSdfMapRoute()
        {
            return new AtlasCompanionRoute(
                "_LightDataSDFMap",
                AtlasCompanionSampler.InlineLinearRepeat,
                new[]
                {
                    ("_LightDataSDFMap_ST", IdentityScaleOffset),
                    ("_LightDataSDFMapPan", ZeroVector),
                },
                Array.Empty<string>(),
                (material, outlineRow) => AtlasRouteUse.Inert,
                material => null);
        }

        /// <summary>
        /// One keyword gated family. The family is off exactly when
        /// the material does not enable its keyword.
        /// </summary>
        private static AtlasDeadValueFamily Family(
            string keyword, string[] members)
        {
            return new AtlasDeadValueFamily(
                keyword,
                new[] { keyword },
                (material, rowName) =>
                    !material.IsKeywordEnabled(keyword),
                members);
        }

        private static readonly string[] LightingAOMapsMembers =
        {
            "_LightingAOMaps",
            "_LightingAOMapsPan",
            "_LightingAOMapsUV",
            "_LightDataAOStrengthR",
            "_LightDataAOStrengthG",
            "_LightDataAOStrengthB",
            "_LightDataAOStrengthA",
        };

        private static readonly string[] LightingDetailShadowMapsMembers =
        {
            "_LightingDetailShadowMaps",
            "_LightingDetailShadowMapsPan",
            "_LightingDetailShadowMapsUV",
            "_LightingDetailShadowStrengthR",
            "_LightingDetailShadowStrengthG",
            "_LightingDetailShadowStrengthB",
            "_LightingDetailShadowStrengthA",
            "_LightingAddDetailShadowStrengthR",
            "_LightingAddDetailShadowStrengthG",
            "_LightingAddDetailShadowStrengthB",
            "_LightingAddDetailShadowStrengthA",
        };

        private static readonly string[] LightingShadowMasksMembers =
        {
            "_LightingShadowMasks",
            "_LightingShadowMasksPan",
            "_LightingShadowMasksUV",
            "_LightingShadowMaskStrengthR",
            "_LightingShadowMaskStrengthG",
            "_LightingShadowMaskStrengthB",
            "_LightingShadowMaskStrengthA",
        };

        private static readonly string[] LightDataSdfMembers =
        {
            "_LightDataSDFMap",
            "_LightDataSDFMapPan",
            "_LightDataSDFMapUV",
            "_LightDataSDFMapLOD",
            "_LightDataSDFBlendY",
        };

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
