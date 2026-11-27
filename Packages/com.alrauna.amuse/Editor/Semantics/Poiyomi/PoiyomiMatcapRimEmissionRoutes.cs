using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The feature group 'Matcap rim emission masks' of the reviewed Poiyomi 9.3
    /// route. The review record of 2026-11-23, sections 4.1, 4.3,
    /// 5, and 6, carries the rows of this group.
    /// <para>
    /// The group holds the four matcap slots, the rim and rim2
    /// features with their three styles each, the four emission
    /// slots, and the four global mask textures. The four matcap
    /// textures stay neutral, because their sample coordinates come
    /// from view vectors. The scrolling curves never move. Their
    /// routes keep the textures bound, and their rule refuses an
    /// assigned curve in use. The curves 1 to 3 have no own ST
    /// property, so their routes invent none.
    /// </para>
    /// <para>
    /// The table rules of record section 5 return the first failing
    /// sentence. The matcap blend rule needs the exact blend
    /// selector 1, 2, 3, 4, 5, 8, or 9, or a blend vector with x
    /// and y at zero. A fraction, a negative value, or a NaN reads
    /// UV0, the polar UV, or the distorted UV, so it fails closed.
    /// The center out rule refuses an enabled center out with an
    /// assigned map on selector 0. The scroll rule refuses an
    /// assigned curve in use. The split rule refuses a split read
    /// with a non-identity split transform or a non-zero split pan.
    /// </para>
    /// </summary>
    internal static class PoiyomiMatcapRimEmissionRoutes
    {
        /// <summary>
        /// The 21 local keywords of the group, record section 4.1.
        /// </summary>
        internal static readonly string[] AllowedKeywords =
        {
            "POI_GLOBALMASK_TEXTURES",
            "POI_MATCAP0",
            "POI_MATCAP0_CUSTOM_NORMAL",
            "COLOR_GRADING_HDR_3D",
            "POI_MATCAP1_CUSTOM_NORMAL",
            "POI_MATCAP2",
            "POI_MATCAP2_CUSTOM_NORMAL",
            "POI_MATCAP3",
            "POI_MATCAP3_CUSTOM_NORMAL",
            "_EMISSION",
            "POI_EMISSION_1",
            "POI_EMISSION_2",
            "POI_EMISSION_3",
            "_GLOSSYREFLECTIONS_OFF",
            "_RIMSTYLE_POIYOMI",
            "_RIMSTYLE_UTS2",
            "_RIMSTYLE_LILTOON",
            "POI_RIM2",
            "_RIM2STYLE_POIYOMI",
            "_RIM2STYLE_UTS2",
            "_RIM2STYLE_LILTOON",
        };

        /// <summary>
        /// The four neutral matcap textures of record sections 4.3
        /// and 9. Their transforms move the matcap coordinate, which
        /// no page ever moves. The blend rule guards the one UV read
        /// in the matcap path.
        /// </summary>
        internal static readonly string[] NeutralTextures =
        {
            "_Matcap",
            "_Matcap2",
            "_Matcap3",
            "_Matcap4",
        };

        /// <summary>
        /// The properties of the table rules beyond the route
        /// conditions. The blend selectors and blend vectors, the
        /// center out flags with their map selectors, the scroll use
        /// flags, and the split flags with their transforms and pans.
        /// The declaration sits above <see cref="ConditionProperties"/>,
        /// because a static field initializer runs in declaration
        /// order.
        /// </summary>
        private static readonly string[] RuleProperties =
        {
            "_MatcapUVToBlend", "_MatCapBlendUV1",
            "_Matcap2UVToBlend", "_MatCap2ndBlendUV1",
            "_Matcap3UVToBlend", "_MatCap3rdBlendUV1",
            "_Matcap4UVToBlend", "_MatCap4thBlendUV1",
            "_EmissionCenterOutEnabled", "_EmissionMapUV",
            "_EmissionCenterOutEnabled1", "_EmissionMap1UV",
            "_EmissionCenterOutEnabled2", "_EmissionMap2UV",
            "_EmissionCenterOutEnabled3", "_EmissionMap3UV",
            "_EmissionScrollingUseCurve", "_EmissionScrollingUseCurve1",
            "_EmissionScrollingUseCurve2", "_EmissionScrollingUseCurve3",
            "_GlobalMaskTexture0Split", "_GlobalMaskTexture1Split",
            "_GlobalMaskTexture2Split", "_GlobalMaskTexture3Split",
            "_GlobalMaskTexture0SplitTilingOffset_G",
            "_GlobalMaskTexture0SplitTilingOffset_B",
            "_GlobalMaskTexture0SplitTilingOffset_A",
            "_GlobalMaskTexture0SplitPan_G",
            "_GlobalMaskTexture0SplitPan_B",
            "_GlobalMaskTexture0SplitPan_A",
            "_GlobalMaskTexture1SplitTilingOffset_G",
            "_GlobalMaskTexture1SplitTilingOffset_B",
            "_GlobalMaskTexture1SplitTilingOffset_A",
            "_GlobalMaskTexture1SplitPan_G",
            "_GlobalMaskTexture1SplitPan_B",
            "_GlobalMaskTexture1SplitPan_A",
            "_GlobalMaskTexture2SplitTilingOffset_G",
            "_GlobalMaskTexture2SplitTilingOffset_B",
            "_GlobalMaskTexture2SplitTilingOffset_A",
            "_GlobalMaskTexture2SplitPan_G",
            "_GlobalMaskTexture2SplitPan_B",
            "_GlobalMaskTexture2SplitPan_A",
            "_GlobalMaskTexture3SplitTilingOffset_G",
            "_GlobalMaskTexture3SplitTilingOffset_B",
            "_GlobalMaskTexture3SplitTilingOffset_A",
            "_GlobalMaskTexture3SplitPan_G",
            "_GlobalMaskTexture3SplitPan_B",
            "_GlobalMaskTexture3SplitPan_A",
        };

        /// <summary>
        /// The route condition properties of the group plus the
        /// properties of the table rules. An animation or a property
        /// block that touches one refuses the use.
        /// </summary>
        internal static readonly string[] ConditionProperties =
            BuildConditionProperties();

        /// <summary>
        /// One route for every companion texture of the group in the
        /// record, section 4.3. Every row of the group carries the
        /// member sampler and no stochastic flag.
        /// </summary>
        internal static AtlasCompanionRoute[] Routes()
        {
            return new[]
            {
                // The matcap masks and custom normal maps. Each slot
                // names its own feature keyword.
                PoiyomiCompanionRoutes.Standard(
                    "_MatcapMask",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_MATCAP0" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Matcap0NormalMap",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_MATCAP0_CUSTOM_NORMAL" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Matcap2Mask",
                    AtlasCompanionSampler.Member,
                    new[] { "COLOR_GRADING_HDR_3D" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Matcap1NormalMap",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_MATCAP1_CUSTOM_NORMAL" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Matcap3Mask",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_MATCAP2" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Matcap2NormalMap",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_MATCAP2_CUSTOM_NORMAL" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Matcap4Mask",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_MATCAP3" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Matcap3NormalMap",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_MATCAP3_CUSTOM_NORMAL" },
                    false),

                // The rim and rim2 companions. The style keyword gates
                // the sample. The master keyword stays off the gate,
                // because a style keyword without its master samples
                // nothing.
                PoiyomiCompanionRoutes.Standard(
                    "_Set_RimLightMask",
                    AtlasCompanionSampler.Member,
                    new[] { "_RIMSTYLE_UTS2" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_RimMask",
                    AtlasCompanionSampler.Member,
                    new[] { "_RIMSTYLE_POIYOMI" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_RimTex",
                    AtlasCompanionSampler.Member,
                    new[] { "_RIMSTYLE_POIYOMI" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_RimColorTex",
                    AtlasCompanionSampler.Member,
                    new[] { "_RIMSTYLE_LILTOON" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Set_Rim2LightMask",
                    AtlasCompanionSampler.Member,
                    new[] { "_RIM2STYLE_UTS2" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Rim2Mask",
                    AtlasCompanionSampler.Member,
                    new[] { "_RIM2STYLE_POIYOMI" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Rim2Tex",
                    AtlasCompanionSampler.Member,
                    new[] { "_RIM2STYLE_POIYOMI" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Rim2ColorTex",
                    AtlasCompanionSampler.Member,
                    new[] { "_RIM2STYLE_LILTOON" },
                    false),

                // The emission masks and maps of the four slots.
                PoiyomiCompanionRoutes.Standard(
                    "_EmissionMask",
                    AtlasCompanionSampler.Member,
                    new[] { "_EMISSION" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_EmissionMap",
                    AtlasCompanionSampler.Member,
                    new[] { "_EMISSION" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_EmissionMask1",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_EMISSION_1" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_EmissionMap1",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_EMISSION_1" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_EmissionMask2",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_EMISSION_2" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_EmissionMap2",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_EMISSION_2" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_EmissionMask3",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_EMISSION_3" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_EmissionMap3",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_EMISSION_3" },
                    false),

                // The scrolling curves never move. The sample follows
                // the map selector, and the scroll rule refuses an
                // assigned curve in use. The route keeps the texture
                // bound while its keyword is on. The curve of slot 0
                // carries its own ST property. The curves 1 to 3 carry
                // no own ST property, so they invent none.
                CurveRoute(
                    "_EmissionScrollingCurve",
                    "_EmissionMapUV",
                    "_EmissionScrollingCurve_ST",
                    new[] { "_EMISSION" }),
                CurveRoute(
                    "_EmissionScrollingCurve1",
                    "_EmissionMap1UV",
                    null,
                    new[] { "POI_EMISSION_1" }),
                CurveRoute(
                    "_EmissionScrollingCurve2",
                    "_EmissionMap2UV",
                    null,
                    new[] { "POI_EMISSION_2" }),
                CurveRoute(
                    "_EmissionScrollingCurve3",
                    "_EmissionMap3UV",
                    null,
                    new[] { "POI_EMISSION_3" }),

                // The four global mask textures. The split transforms
                // and split pans are rule properties, not route
                // facts, because a split flag of zero makes them dead.
                PoiyomiCompanionRoutes.Standard(
                    "_GlobalMaskTexture0",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_GLOBALMASK_TEXTURES" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_GlobalMaskTexture1",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_GLOBALMASK_TEXTURES" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_GlobalMaskTexture2",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_GLOBALMASK_TEXTURES" },
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_GlobalMaskTexture3",
                    AtlasCompanionSampler.Member,
                    new[] { "POI_GLOBALMASK_TEXTURES" },
                    false),
            };
        }

        /// <summary>
        /// The table level required value function of the group. It
        /// checks the rules of record section 5 for the enabled
        /// keywords of the group and returns the first failing
        /// sentence, or null.
        /// </summary>
        internal static string UnheldRequiredValue(Material material)
        {
            return MatcapBlendSentence(material)
                ?? CenterOutSentence(material)
                ?? ScrollCurveSentence(material)
                ?? SplitSentence(material);
        }

        /// <summary>
        /// One dead value family per keyword of the group, record
        /// section 6. The only switch of a family is its keyword.
        /// </summary>
        internal static AtlasDeadValueFamily[] Families()
        {
            return new[]
            {
                KeywordFamily(
                    "POI_GLOBALMASK_TEXTURES",
                    MembersPOI_GLOBALMASK_TEXTURES),
                KeywordFamily("POI_MATCAP0", MembersPOI_MATCAP0),
                KeywordFamily(
                    "POI_MATCAP0_CUSTOM_NORMAL",
                    MembersPOI_MATCAP0_CUSTOM_NORMAL),
                KeywordFamily(
                    "COLOR_GRADING_HDR_3D",
                    MembersCOLOR_GRADING_HDR_3D),
                KeywordFamily(
                    "POI_MATCAP1_CUSTOM_NORMAL",
                    MembersPOI_MATCAP1_CUSTOM_NORMAL),
                KeywordFamily("POI_MATCAP2", MembersPOI_MATCAP2),
                KeywordFamily(
                    "POI_MATCAP2_CUSTOM_NORMAL",
                    MembersPOI_MATCAP2_CUSTOM_NORMAL),
                KeywordFamily("POI_MATCAP3", MembersPOI_MATCAP3),
                KeywordFamily(
                    "POI_MATCAP3_CUSTOM_NORMAL",
                    MembersPOI_MATCAP3_CUSTOM_NORMAL),
                KeywordFamily("_EMISSION", MembersEMISSION),
                KeywordFamily("POI_EMISSION_1", MembersPOI_EMISSION_1),
                KeywordFamily("POI_EMISSION_2", MembersPOI_EMISSION_2),
                KeywordFamily("POI_EMISSION_3", MembersPOI_EMISSION_3),
                KeywordFamily(
                    "_GLOSSYREFLECTIONS_OFF",
                    MembersGLOSSYREFLECTIONS_OFF),
                KeywordFamily(
                    "_RIMSTYLE_POIYOMI",
                    MembersRIMSTYLE_POIYOMI),
                KeywordFamily("_RIMSTYLE_UTS2", MembersRIMSTYLE_UTS2),
                KeywordFamily(
                    "_RIMSTYLE_LILTOON",
                    MembersRIMSTYLE_LILTOON),
                KeywordFamily("POI_RIM2", MembersPOI_RIM2),
                KeywordFamily(
                    "_RIM2STYLE_POIYOMI",
                    MembersRIM2STYLE_POIYOMI),
                KeywordFamily("_RIM2STYLE_UTS2", MembersRIM2STYLE_UTS2),
                KeywordFamily(
                    "_RIM2STYLE_LILTOON",
                    MembersRIM2STYLE_LILTOON),
            };
        }

        // --- the table rules ------------------------------------------

        private static readonly
            (string Keyword, string UvToBlend, string BlendVector)[]
            MatcapBlendRules =
        {
            ("POI_MATCAP0", "_MatcapUVToBlend", "_MatCapBlendUV1"),
            (
                "COLOR_GRADING_HDR_3D",
                "_Matcap2UVToBlend",
                "_MatCap2ndBlendUV1"),
            ("POI_MATCAP2", "_Matcap3UVToBlend", "_MatCap3rdBlendUV1"),
            ("POI_MATCAP3", "_Matcap4UVToBlend", "_MatCap4thBlendUV1"),
        };

        private static readonly
            (string Keyword, string CenterOut, string Map, string MapUv)[]
            CenterOutRules =
        {
            (
                "_EMISSION",
                "_EmissionCenterOutEnabled",
                "_EmissionMap",
                "_EmissionMapUV"),
            (
                "POI_EMISSION_1",
                "_EmissionCenterOutEnabled1",
                "_EmissionMap1",
                "_EmissionMap1UV"),
            (
                "POI_EMISSION_2",
                "_EmissionCenterOutEnabled2",
                "_EmissionMap2",
                "_EmissionMap2UV"),
            (
                "POI_EMISSION_3",
                "_EmissionCenterOutEnabled3",
                "_EmissionMap3",
                "_EmissionMap3UV"),
        };

        private static readonly
            (string Keyword, string UseCurve, string Curve)[]
            ScrollCurveRules =
        {
            (
                "_EMISSION",
                "_EmissionScrollingUseCurve",
                "_EmissionScrollingCurve"),
            (
                "POI_EMISSION_1",
                "_EmissionScrollingUseCurve1",
                "_EmissionScrollingCurve1"),
            (
                "POI_EMISSION_2",
                "_EmissionScrollingUseCurve2",
                "_EmissionScrollingCurve2"),
            (
                "POI_EMISSION_3",
                "_EmissionScrollingUseCurve3",
                "_EmissionScrollingCurve3"),
        };

        /// <summary>
        /// The split rule of the global masks. A split flag of zero
        /// makes the split transforms and split pans of the slot
        /// dead. A split read needs identity transforms and zero
        /// pans on all three channels.
        /// </summary>
        private static readonly (string Texture, string Split)[]
            SplitRules =
        {
            ("_GlobalMaskTexture0", "_GlobalMaskTexture0Split"),
            ("_GlobalMaskTexture1", "_GlobalMaskTexture1Split"),
            ("_GlobalMaskTexture2", "_GlobalMaskTexture2Split"),
            ("_GlobalMaskTexture3", "_GlobalMaskTexture3Split"),
        };

        private static readonly string[] SplitChannels = { "G", "B", "A" };

        private static string MatcapBlendSentence(Material material)
        {
            foreach (var rule in MatcapBlendRules)
            {
                if (!material.IsKeywordEnabled(rule.Keyword))
                {
                    continue;
                }

                var selector = material.GetFloat(rule.UvToBlend);
                if (PoiyomiCompanionRoutes.IsOtherCoordinate(selector))
                {
                    continue;
                }

                var blend = material.GetVector(rule.BlendVector);
                if (blend.x == 0f && blend.y == 0f)
                {
                    continue;
                }

                return "The property '" + rule.UvToBlend + "' is " +
                       Number(selector) + ". The route needs the" +
                       " exact blend selector 1, 2, 3, 4, 5, 8, or" +
                       " 9, or a blend vector with x and y at 0.";
            }

            return null;
        }

        private static string CenterOutSentence(Material material)
        {
            foreach (var rule in CenterOutRules)
            {
                if (!material.IsKeywordEnabled(rule.Keyword))
                {
                    continue;
                }

                var centerOut = material.GetFloat(rule.CenterOut);
                if (centerOut == 0f)
                {
                    continue;
                }

                if (material.GetTexture(rule.Map) == null)
                {
                    continue;
                }

                if (material.GetFloat(rule.MapUv) != 0f)
                {
                    continue;
                }

                return "The property '" + rule.CenterOut + "' is " +
                       Number(centerOut) + ". The route needs 0 when" +
                       " the map texture is assigned on selector 0.";
            }

            return null;
        }

        private static string ScrollCurveSentence(Material material)
        {
            foreach (var rule in ScrollCurveRules)
            {
                if (!material.IsKeywordEnabled(rule.Keyword))
                {
                    continue;
                }

                var useCurve = material.GetFloat(rule.UseCurve);
                if (useCurve == 0f)
                {
                    continue;
                }

                if (material.GetTexture(rule.Curve) == null)
                {
                    continue;
                }

                return "The property '" + rule.UseCurve + "' is " +
                       Number(useCurve) + ". The route needs 0, or" +
                       " the curve texture unassigned.";
            }

            return null;
        }

        private static string SplitSentence(Material material)
        {
            if (!material.IsKeywordEnabled("POI_GLOBALMASK_TEXTURES"))
            {
                return null;
            }

            foreach (var rule in SplitRules)
            {
                var split = material.GetFloat(rule.Split);
                if (split == 0f)
                {
                    continue;
                }

                foreach (var channel in SplitChannels)
                {
                    var transform = rule.Texture +
                                    "SplitTilingOffset_" + channel;
                    var value = material.GetVector(transform);
                    if (!HoldsIdentity(value))
                    {
                        return "The property '" + transform +
                               "' is " + Number(value) +
                               ". The route needs the identity" +
                               " transform, or the split flag 0.";
                    }

                    var pan = rule.Texture + "SplitPan_" + channel;
                    var panValue = material.GetVector(pan);
                    if (!HoldsZero(panValue))
                    {
                        return "The property '" + pan +
                               "' is " + Number(panValue) +
                               ". The route needs the zero pan, or" +
                               " the split flag 0.";
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The exact identity compare. Vector4 equality accepts a
        /// small distance, and a split read moves the coordinate by
        /// any non-zero component.
        /// </summary>
        private static bool HoldsIdentity(Vector4 value)
        {
            return value.x == 1f && value.y == 1f &&
                   value.z == 0f && value.w == 0f;
        }

        /// <summary>
        /// The exact zero compare. Vector4 equality accepts a small
        /// distance, and a split read pans by any non-zero
        /// component.
        /// </summary>
        private static bool HoldsZero(Vector4 value)
        {
            return value.x == 0f && value.y == 0f &&
                   value.z == 0f && value.w == 0f;
        }

        // --- the condition properties ---------------------------------

        private static string[] BuildConditionProperties()
        {
            var names = new List<string>();
            foreach (var route in Routes())
            {
                names.AddRange(route.ConditionProperties);
            }

            names.AddRange(RuleProperties);
            return names.Distinct().ToArray();
        }

        // --- the route helpers ----------------------------------------

        /// <summary>
        /// The route of one scrolling curve. The sample follows the
        /// map selector, and the scroll rule refuses an assigned
        /// curve in use. So the curve never moves in an admitted use
        /// and stays bound while its keyword is on. The texture
        /// property carries the transform of slot 0. The curves 1 to
        /// 3 read a uniform without a Properties entry, so they carry
        /// no transform fact.
        /// </summary>
        private static AtlasCompanionRoute CurveRoute(
            string texture,
            string mapSelectorProperty,
            string transformProperty,
            string[] featureKeywords)
        {
            var transforms = transformProperty == null
                ? Array.Empty<(string Property, Vector4 Identity)>()
                : new[]
                {
                    (
                        transformProperty,
                        new Vector4(1f, 1f, 0f, 0f)),
                };
            return new AtlasCompanionRoute(
                texture,
                AtlasCompanionSampler.Member,
                transforms,
                new[] { mapSelectorProperty },
                BoundUse(featureKeywords));
        }

        /// <summary>
        /// The use of one curve. No feature keyword on means the
        /// shader never samples the curve. A keyword on keeps the
        /// texture bound at any selector, because the curve never
        /// moves into a page.
        /// </summary>
        private static Func<Material, bool, AtlasRouteUse> BoundUse(
            string[] featureKeywords)
        {
            return (material, outlineRow) =>
                PoiyomiCompanionRoutes.AnyEnabled(
                    material, featureKeywords)
                    ? AtlasRouteUse.OtherCoordinates
                    : AtlasRouteUse.Inert;
        }

        private static AtlasDeadValueFamily KeywordFamily(
            string keyword,
            string[] members)
        {
            return new AtlasDeadValueFamily(
                keyword,
                new[] { keyword },
                (material, rowName) => !material.IsKeywordEnabled(keyword),
                members);
        }

        private static string Number(float value)
        {
            return value.ToString(
                "0.######",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Number(Vector4 value)
        {
            return "(" + Number(value.x) + ", " + Number(value.y) +
                   ", " + Number(value.z) + ", " + Number(value.w) + ")";
        }

        // --- the dead value members -----------------------------------

        /// <summary>
        /// The expanded member names of the families, from the
        /// prefix tokens of record section 6 against the Properties
        /// block of the bundled 9.3 rows. A prefix token stands for
        /// every property name that starts with it. A plain token
        /// stands for one name. Names the shader never reads stay in
        /// a family without harm.
        /// </summary>
        private static readonly string[] MembersPOI_GLOBALMASK_TEXTURES =
        {
            "_GlobalMaskTexture0", "_GlobalMaskTexture0Pan",
            "_GlobalMaskTexture0Split", "_GlobalMaskTexture0SplitPan_A",
            "_GlobalMaskTexture0SplitPan_B", "_GlobalMaskTexture0SplitPan_G",
            "_GlobalMaskTexture0SplitTilingOffset_A",
            "_GlobalMaskTexture0SplitTilingOffset_B",
            "_GlobalMaskTexture0SplitTilingOffset_G", "_GlobalMaskTexture0UV",
            "_GlobalMaskTexture0_ST", "_GlobalMaskTexture1",
            "_GlobalMaskTexture1Pan", "_GlobalMaskTexture1Split",
            "_GlobalMaskTexture1SplitPan_A", "_GlobalMaskTexture1SplitPan_B",
            "_GlobalMaskTexture1SplitPan_G",
            "_GlobalMaskTexture1SplitTilingOffset_A",
            "_GlobalMaskTexture1SplitTilingOffset_B",
            "_GlobalMaskTexture1SplitTilingOffset_G", "_GlobalMaskTexture1UV",
            "_GlobalMaskTexture1_ST", "_GlobalMaskTexture2",
            "_GlobalMaskTexture2Pan", "_GlobalMaskTexture2Split",
            "_GlobalMaskTexture2SplitPan_A", "_GlobalMaskTexture2SplitPan_B",
            "_GlobalMaskTexture2SplitPan_G",
            "_GlobalMaskTexture2SplitTilingOffset_A",
            "_GlobalMaskTexture2SplitTilingOffset_B",
            "_GlobalMaskTexture2SplitTilingOffset_G", "_GlobalMaskTexture2UV",
            "_GlobalMaskTexture2_ST", "_GlobalMaskTexture3",
            "_GlobalMaskTexture3Pan", "_GlobalMaskTexture3Split",
            "_GlobalMaskTexture3SplitPan_A", "_GlobalMaskTexture3SplitPan_B",
            "_GlobalMaskTexture3SplitPan_G",
            "_GlobalMaskTexture3SplitTilingOffset_A",
            "_GlobalMaskTexture3SplitTilingOffset_B",
            "_GlobalMaskTexture3SplitTilingOffset_G", "_GlobalMaskTexture3UV",
            "_GlobalMaskTexture3_ST",
        };

        private static readonly string[] MembersPOI_MATCAP0 =
        {
            "_MatCapBlendUV1", "_Matcap", "_Matcap0ALAlphaAdd",
            "_Matcap0ALAlphaAddBand", "_Matcap0ALChronoPanBand",
            "_Matcap0ALChronoPanSpeed", "_Matcap0ALChronoPanType",
            "_Matcap0ALEmissionAdd", "_Matcap0ALEmissionAddBand",
            "_Matcap0ALEnabled", "_Matcap0ALIntensityAdd",
            "_Matcap0ALIntensityAddBand", "_Matcap0CircleMaskBlur",
            "_Matcap0CircleMaskBorder", "_Matcap0CircleMaskEnabled",
            "_Matcap0NormalMap", "_Matcap0NormalMapPan", "_Matcap0NormalMapScale",
            "_Matcap0NormalMapUV", "_Matcap0NormalMap_ST", "_MatcapAdd",
            "_MatcapAddToLight", "_MatcapAlphaOverride",
            "_MatcapApplyToAlphaBlendType", "_MatcapApplyToAlphaBlending",
            "_MatcapApplyToAlphaEnabled", "_MatcapApplyToAlphaSourceBlend",
            "_MatcapBaseColorMix", "_MatcapBorder", "_MatcapColor",
            "_MatcapColorThemeIndex", "_MatcapEmissionStrength",
            "_MatcapHueSelectOrShift", "_MatcapHueShift",
            "_MatcapHueShiftColorSpace", "_MatcapHueShiftEnabled",
            "_MatcapHueShiftSpeed", "_MatcapIntensity", "_MatcapLightColorMix",
            "_MatcapLightMask", "_MatcapMask", "_MatcapMaskChannel",
            "_MatcapMaskGlobalMask", "_MatcapMaskGlobalMaskBlendType",
            "_MatcapMaskInvert", "_MatcapMaskPan", "_MatcapMaskSmoothnessApply",
            "_MatcapMaskSmoothnessChannel", "_MatcapMaskUV", "_MatcapMask_ST",
            "_MatcapMixed", "_MatcapMultiply", "_MatcapNormal", "_MatcapPan",
            "_MatcapReplace", "_MatcapRotation", "_MatcapScreen",
            "_MatcapSmoothness", "_MatcapTPSDepthEnabled", "_MatcapTPSMaskStrength",
            "_MatcapUVMode", "_MatcapUVToBlend",
        };

        private static readonly string[] MembersPOI_MATCAP0_CUSTOM_NORMAL =
        {
            "_Matcap0NormalMap", "_Matcap0NormalMapPan", "_Matcap0NormalMapScale",
            "_Matcap0NormalMapUV", "_Matcap0NormalMap_ST",
        };

        private static readonly string[] MembersCOLOR_GRADING_HDR_3D =
        {
            "_MatCap2ndBlendUV1", "_Matcap1ALAlphaAdd", "_Matcap1ALAlphaAddBand",
            "_Matcap1ALChronoPanBand", "_Matcap1ALChronoPanSpeed",
            "_Matcap1ALChronoPanType", "_Matcap1ALEmissionAdd",
            "_Matcap1ALEmissionAddBand", "_Matcap1ALEnabled",
            "_Matcap1ALIntensityAdd", "_Matcap1ALIntensityAddBand",
            "_Matcap1NormalMap", "_Matcap1NormalMapPan", "_Matcap1NormalMapScale",
            "_Matcap1NormalMapUV", "_Matcap1NormalMap_ST", "_Matcap2", "_Matcap2Add",
            "_Matcap2AddToLight", "_Matcap2AlphaOverride",
            "_Matcap2ApplyToAlphaBlendType", "_Matcap2ApplyToAlphaBlending",
            "_Matcap2ApplyToAlphaEnabled", "_Matcap2ApplyToAlphaSourceBlend",
            "_Matcap2BaseColorMix", "_Matcap2Border", "_Matcap2Color",
            "_Matcap2ColorThemeIndex", "_Matcap2EmissionStrength",
            "_Matcap2HueSelectOrShift", "_Matcap2HueShift",
            "_Matcap2HueShiftColorSpace", "_Matcap2HueShiftEnabled",
            "_Matcap2HueShiftSpeed", "_Matcap2Intensity", "_Matcap2LightColorMix",
            "_Matcap2LightMask", "_Matcap2Mask", "_Matcap2MaskChannel",
            "_Matcap2MaskGlobalMask", "_Matcap2MaskGlobalMaskBlendType",
            "_Matcap2MaskInvert", "_Matcap2MaskPan", "_Matcap2MaskSmoothnessApply",
            "_Matcap2MaskSmoothnessChannel", "_Matcap2MaskUV", "_Matcap2Mask_ST",
            "_Matcap2Mixed", "_Matcap2Multiply", "_Matcap2Normal", "_Matcap2Pan",
            "_Matcap2Replace", "_Matcap2Rotation", "_Matcap2Screen",
            "_Matcap2Smoothness", "_Matcap2TPSDepthEnabled",
            "_Matcap2TPSMaskStrength", "_Matcap2UVMode", "_Matcap2UVToBlend",
        };

        private static readonly string[] MembersPOI_MATCAP1_CUSTOM_NORMAL =
        {
            "_Matcap1NormalMap", "_Matcap1NormalMapPan", "_Matcap1NormalMapScale",
            "_Matcap1NormalMapUV", "_Matcap1NormalMap_ST",
        };

        private static readonly string[] MembersPOI_MATCAP2 =
        {
            "_MatCap3rdBlendUV1", "_Matcap2ALAlphaAdd", "_Matcap2ALAlphaAddBand",
            "_Matcap2ALChronoPanBand", "_Matcap2ALChronoPanSpeed",
            "_Matcap2ALChronoPanType", "_Matcap2ALEmissionAdd",
            "_Matcap2ALEmissionAddBand", "_Matcap2ALEnabled",
            "_Matcap2ALIntensityAdd", "_Matcap2ALIntensityAddBand",
            "_Matcap2NormalMap", "_Matcap2NormalMapPan", "_Matcap2NormalMapScale",
            "_Matcap2NormalMapUV", "_Matcap2NormalMap_ST", "_Matcap3", "_Matcap3Add",
            "_Matcap3AddToLight", "_Matcap3AlphaOverride",
            "_Matcap3ApplyToAlphaBlendType", "_Matcap3ApplyToAlphaBlending",
            "_Matcap3ApplyToAlphaEnabled", "_Matcap3ApplyToAlphaSourceBlend",
            "_Matcap3BaseColorMix", "_Matcap3Border", "_Matcap3Color",
            "_Matcap3ColorThemeIndex", "_Matcap3EmissionStrength",
            "_Matcap3HueSelectOrShift", "_Matcap3HueShift",
            "_Matcap3HueShiftColorSpace", "_Matcap3HueShiftEnabled",
            "_Matcap3HueShiftSpeed", "_Matcap3Intensity", "_Matcap3LightColorMix",
            "_Matcap3LightMask", "_Matcap3Mask", "_Matcap3MaskChannel",
            "_Matcap3MaskGlobalMask", "_Matcap3MaskGlobalMaskBlendType",
            "_Matcap3MaskInvert", "_Matcap3MaskPan", "_Matcap3MaskSmoothnessApply",
            "_Matcap3MaskSmoothnessChannel", "_Matcap3MaskUV", "_Matcap3Mask_ST",
            "_Matcap3Mixed", "_Matcap3Multiply", "_Matcap3Normal", "_Matcap3Pan",
            "_Matcap3Replace", "_Matcap3Rotation", "_Matcap3Screen",
            "_Matcap3Smoothness", "_Matcap3TPSDepthEnabled",
            "_Matcap3TPSMaskStrength", "_Matcap3UVMode", "_Matcap3UVToBlend",
        };

        private static readonly string[] MembersPOI_MATCAP2_CUSTOM_NORMAL =
        {
            "_Matcap2NormalMap", "_Matcap2NormalMapPan", "_Matcap2NormalMapScale",
            "_Matcap2NormalMapUV", "_Matcap2NormalMap_ST",
        };

        private static readonly string[] MembersPOI_MATCAP3 =
        {
            "_MatCap4thBlendUV1", "_Matcap3ALAlphaAdd", "_Matcap3ALAlphaAddBand",
            "_Matcap3ALChronoPanBand", "_Matcap3ALChronoPanSpeed",
            "_Matcap3ALChronoPanType", "_Matcap3ALEmissionAdd",
            "_Matcap3ALEmissionAddBand", "_Matcap3ALEnabled",
            "_Matcap3ALIntensityAdd", "_Matcap3ALIntensityAddBand",
            "_Matcap3NormalMap", "_Matcap3NormalMapPan", "_Matcap3NormalMapScale",
            "_Matcap3NormalMapUV", "_Matcap3NormalMap_ST", "_Matcap4", "_Matcap4Add",
            "_Matcap4AddToLight", "_Matcap4AlphaOverride",
            "_Matcap4ApplyToAlphaBlendType", "_Matcap4ApplyToAlphaBlending",
            "_Matcap4ApplyToAlphaEnabled", "_Matcap4ApplyToAlphaSourceBlend",
            "_Matcap4BaseColorMix", "_Matcap4Border", "_Matcap4Color",
            "_Matcap4ColorThemeIndex", "_Matcap4EmissionStrength",
            "_Matcap4HueSelectOrShift", "_Matcap4HueShift",
            "_Matcap4HueShiftColorSpace", "_Matcap4HueShiftEnabled",
            "_Matcap4HueShiftSpeed", "_Matcap4Intensity", "_Matcap4LightColorMix",
            "_Matcap4LightMask", "_Matcap4Mask", "_Matcap4MaskChannel",
            "_Matcap4MaskGlobalMask", "_Matcap4MaskGlobalMaskBlendType",
            "_Matcap4MaskInvert", "_Matcap4MaskPan", "_Matcap4MaskSmoothnessApply",
            "_Matcap4MaskSmoothnessChannel", "_Matcap4MaskUV", "_Matcap4Mask_ST",
            "_Matcap4Mixed", "_Matcap4Multiply", "_Matcap4Normal", "_Matcap4Pan",
            "_Matcap4Replace", "_Matcap4Rotation", "_Matcap4Screen",
            "_Matcap4Smoothness", "_Matcap4TPSDepthEnabled",
            "_Matcap4TPSMaskStrength", "_Matcap4UVMode", "_Matcap4UVToBlend",
        };

        private static readonly string[] MembersPOI_MATCAP3_CUSTOM_NORMAL =
        {
            "_Matcap3NormalMap", "_Matcap3NormalMapPan", "_Matcap3NormalMapScale",
            "_Matcap3NormalMapUV", "_Matcap3NormalMap_ST",
        };

        private static readonly string[] MembersEMISSION =
        {
            "_AudioLinkEmission0CenterOut", "_AudioLinkEmission0CenterOutBand",
            "_AudioLinkEmission0CenterOutDuration",
            "_AudioLinkEmission0CenterOutSize", "_EmissionAL0Enabled",
            "_EmissionAL0Multipliers", "_EmissionAL0MultipliersBand",
            "_EmissionAL0StrengthBand", "_EmissionAL0StrengthMod",
            "_EmissionBaseColorAsMap", "_EmissionBlinkingEnabled",
            "_EmissionBlinkingOffset", "_EmissionCenterOutEnabled",
            "_EmissionCenterOutSpeed", "_EmissionColor", "_EmissionColorThemeIndex",
            "_EmissionFluorescence", "_EmissionHueSelectOrShift",
            "_EmissionHueShift", "_EmissionHueShiftColorSpace",
            "_EmissionHueShiftEnabled", "_EmissionHueShiftSpeed", "_EmissionMap",
            "_EmissionMapPan", "_EmissionMapUV", "_EmissionMap_ST", "_EmissionMask",
            "_EmissionMask0GlobalMask", "_EmissionMask0GlobalMaskBlendType",
            "_EmissionMaskChannel", "_EmissionMaskInvert", "_EmissionMaskPan",
            "_EmissionMaskUV", "_EmissionMask_ST", "_EmissionReplace0",
            "_EmissionSaturation", "_EmissionScrollingCurve",
            "_EmissionScrollingCurve_ST", "_EmissionScrollingOffset",
            "_EmissionScrollingUseCurve", "_EmissionScrollingVertexColor",
            "_EmissionStrength", "_EmissiveBlink_Max", "_EmissiveBlink_Min",
            "_EmissiveBlink_Velocity", "_EmissiveScroll_Direction",
            "_EmissiveScroll_Interval", "_EmissiveScroll_Velocity",
            "_EmissiveScroll_Width", "_EnableGITDEmission",
            "_GITDEMaxEmissionMultiplier", "_GITDEMaxLight",
            "_GITDEMinEmissionMultiplier", "_GITDEMinLight", "_GITDEWorldOrMesh",
            "_ScrollingEmission",
        };

        private static readonly string[] MembersPOI_EMISSION_1 =
        {
            "_AudioLinkEmission1CenterOut", "_AudioLinkEmission1CenterOutBand",
            "_AudioLinkEmission1CenterOutDuration",
            "_AudioLinkEmission1CenterOutSize", "_EmissionAL1Enabled",
            "_EmissionAL1Multipliers", "_EmissionAL1MultipliersBand",
            "_EmissionAL1StrengthBand", "_EmissionAL1StrengthMod",
            "_EmissionBaseColorAsMap1", "_EmissionBlinkingEnabled1",
            "_EmissionBlinkingOffset1", "_EmissionCenterOutEnabled1",
            "_EmissionCenterOutSpeed1", "_EmissionColor1",
            "_EmissionColor1ThemeIndex", "_EmissionFluorescence1",
            "_EmissionHueSelectOrShift1", "_EmissionHueShift1",
            "_EmissionHueShiftColorSpace1", "_EmissionHueShiftEnabled1",
            "_EmissionHueShiftSpeed1", "_EmissionMap1", "_EmissionMap1Pan",
            "_EmissionMap1UV", "_EmissionMap1_ST", "_EmissionMask1",
            "_EmissionMask1Channel", "_EmissionMask1GlobalMask",
            "_EmissionMask1GlobalMaskBlendType", "_EmissionMask1Pan",
            "_EmissionMask1UV", "_EmissionMask1_ST", "_EmissionMaskInvert1",
            "_EmissionReplace1", "_EmissionSaturation1", "_EmissionScrollingCurve1",
            "_EmissionScrollingOffset1",
            "_EmissionScrollingUseCurve1", "_EmissionScrollingVertexColor1",
            "_EmissionStrength1", "_EmissiveBlink_Max1", "_EmissiveBlink_Min1",
            "_EmissiveBlink_Velocity1", "_EmissiveScroll_Direction1",
            "_EmissiveScroll_Interval1", "_EmissiveScroll_Velocity1",
            "_EmissiveScroll_Width1", "_EnableGITDEmission1",
            "_GITDEMaxEmissionMultiplier1", "_GITDEMaxLight1",
            "_GITDEMinEmissionMultiplier1", "_GITDEMinLight1", "_GITDEWorldOrMesh1",
            "_ScrollingEmission1",
        };

        private static readonly string[] MembersPOI_EMISSION_2 =
        {
            "_AudioLinkEmission2CenterOut", "_AudioLinkEmission2CenterOutBand",
            "_AudioLinkEmission2CenterOutDuration",
            "_AudioLinkEmission2CenterOutSize", "_EmissionAL2Enabled",
            "_EmissionAL2Multipliers", "_EmissionAL2MultipliersBand",
            "_EmissionAL2StrengthBand", "_EmissionAL2StrengthMod",
            "_EmissionBaseColorAsMap2", "_EmissionBlinkingEnabled2",
            "_EmissionBlinkingOffset2", "_EmissionCenterOutEnabled2",
            "_EmissionCenterOutSpeed2", "_EmissionColor2",
            "_EmissionColor2ThemeIndex", "_EmissionFluorescence2",
            "_EmissionHueSelectOrShift2", "_EmissionHueShift2",
            "_EmissionHueShiftColorSpace2", "_EmissionHueShiftEnabled2",
            "_EmissionHueShiftSpeed2", "_EmissionMap2", "_EmissionMap2Pan",
            "_EmissionMap2UV", "_EmissionMap2_ST", "_EmissionMask2",
            "_EmissionMask2Channel", "_EmissionMask2GlobalMask",
            "_EmissionMask2GlobalMaskBlendType", "_EmissionMask2Pan",
            "_EmissionMask2UV", "_EmissionMask2_ST", "_EmissionMaskInvert2",
            "_EmissionReplace2", "_EmissionSaturation2", "_EmissionScrollingCurve2",
            "_EmissionScrollingOffset2",
            "_EmissionScrollingUseCurve2", "_EmissionScrollingVertexColor2",
            "_EmissionStrength2", "_EmissiveBlink_Max2", "_EmissiveBlink_Min2",
            "_EmissiveBlink_Velocity2", "_EmissiveScroll_Direction2",
            "_EmissiveScroll_Interval2", "_EmissiveScroll_Velocity2",
            "_EmissiveScroll_Width2", "_EnableGITDEmission2",
            "_GITDEMaxEmissionMultiplier2", "_GITDEMaxLight2",
            "_GITDEMinEmissionMultiplier2", "_GITDEMinLight2", "_GITDEWorldOrMesh2",
            "_ScrollingEmission2",
        };

        private static readonly string[] MembersPOI_EMISSION_3 =
        {
            "_AudioLinkEmission3CenterOut", "_AudioLinkEmission3CenterOutBand",
            "_AudioLinkEmission3CenterOutDuration",
            "_AudioLinkEmission3CenterOutSize", "_EmissionAL3Enabled",
            "_EmissionAL3Multipliers", "_EmissionAL3MultipliersBand",
            "_EmissionAL3StrengthBand", "_EmissionAL3StrengthMod",
            "_EmissionBaseColorAsMap3", "_EmissionBlinkingEnabled3",
            "_EmissionBlinkingOffset3", "_EmissionCenterOutEnabled3",
            "_EmissionCenterOutSpeed3", "_EmissionColor3",
            "_EmissionColor3ThemeIndex", "_EmissionFluorescence3",
            "_EmissionHueSelectOrShift3", "_EmissionHueShift3",
            "_EmissionHueShiftColorSpace3", "_EmissionHueShiftEnabled3",
            "_EmissionHueShiftSpeed3", "_EmissionMap3", "_EmissionMap3Pan",
            "_EmissionMap3UV", "_EmissionMap3_ST", "_EmissionMask3",
            "_EmissionMask3Channel", "_EmissionMask3GlobalMask",
            "_EmissionMask3GlobalMaskBlendType", "_EmissionMask3Pan",
            "_EmissionMask3UV", "_EmissionMask3_ST", "_EmissionMaskInvert3",
            "_EmissionReplace3", "_EmissionSaturation3", "_EmissionScrollingCurve3",
            "_EmissionScrollingOffset3",
            "_EmissionScrollingUseCurve3", "_EmissionScrollingVertexColor3",
            "_EmissionStrength3", "_EmissiveBlink_Max3", "_EmissiveBlink_Min3",
            "_EmissiveBlink_Velocity3", "_EmissiveScroll_Direction3",
            "_EmissiveScroll_Interval3", "_EmissiveScroll_Velocity3",
            "_EmissiveScroll_Width3", "_EnableGITDEmission3",
            "_GITDEMaxEmissionMultiplier3", "_GITDEMaxLight3",
            "_GITDEMinEmissionMultiplier3", "_GITDEMinLight3", "_GITDEWorldOrMesh3",
            "_ScrollingEmission3",
        };

        private static readonly string[] MembersGLOSSYREFLECTIONS_OFF =
        {
            "_Add_Antipodean_RimLight", "_Ap_RimLightColor",
            "_Ap_RimLight_FeatherOff", "_Ap_RimLight_Power",
            "_AudioLinkRimBrightnessAdd", "_AudioLinkRimBrightnessBand",
            "_AudioLinkRimEmissionAdd", "_AudioLinkRimEmissionBand",
            "_AudioLinkRimWidthAdd", "_AudioLinkRimWidthBand",
            "_Is_LightColor_Ap_RimLight", "_Is_LightColor_RimLight",
            "_Is_NormalMapToRimLight", "_LightDirection_MaskOn",
            "_RimApColorThemeIndex", "_RimApplyAlpha", "_RimApplyAlphaBlend",
            "_RimApplyGlobalMaskBlendType", "_RimApplyGlobalMaskIndex",
            "_RimBackfaceMask", "_RimBaseColorMix", "_RimBiasIntensity",
            "_RimBlendMode", "_RimBlendStrength", "_RimBlur", "_RimBorder",
            "_RimBrightness", "_RimColor", "_RimColorTex", "_RimColorTexPan",
            "_RimColorTexUV", "_RimColorTex_ST", "_RimDirRange", "_RimDirStrength",
            "_RimEnableLighting", "_RimFresnelPower", "_RimGlobalMask",
            "_RimGlobalMaskBlendType", "_RimHueSelectOrShift", "_RimHueShift",
            "_RimHueShiftColorSpace", "_RimHueShiftEnabled", "_RimHueShiftSpeed",
            "_RimIndirBlur", "_RimIndirBorder", "_RimIndirColor", "_RimIndirRange",
            "_RimLightColor", "_RimLightColorThemeIndex", "_RimLight_FeatherOff",
            "_RimLight_InsideMask", "_RimLight_Power", "_RimLightingInvert",
            "_RimMainStrength", "_RimMask", "_RimMaskChannel", "_RimMaskInvert",
            "_RimMaskOnlyMask", "_RimMaskPan", "_RimMaskUV", "_RimMask_ST",
            "_RimNormalStrength", "_RimPoiBlendMode", "_RimPower", "_RimShadowAlpha",
            "_RimShadowMask", "_RimShadowMaskInvert", "_RimShadowMaskRampType",
            "_RimShadowMaskStrength", "_RimShadowToggle", "_RimShadowWidth",
            "_RimSharpness", "_RimStrength", "_RimTex", "_RimTexPan", "_RimTexUV",
            "_RimTex_ST", "_RimVRParallaxStrength", "_RimWidth", "_Set_RimLightMask",
            "_Set_RimLightMaskChannel", "_Set_RimLightMaskPan",
            "_Set_RimLightMaskUV", "_Set_RimLightMask_ST",
            "_Tweak_LightDirection_MaskLevel", "_Tweak_RimLightMaskLevel",
        };

        private static readonly string[] MembersRIMSTYLE_POIYOMI =
        {
            "_AudioLinkRimBrightnessAdd", "_AudioLinkRimBrightnessBand",
            "_AudioLinkRimEmissionAdd", "_AudioLinkRimEmissionBand",
            "_AudioLinkRimWidthAdd", "_AudioLinkRimWidthBand", "_RimApplyAlpha",
            "_RimApplyAlphaBlend", "_RimApplyGlobalMaskBlendType",
            "_RimApplyGlobalMaskIndex", "_RimBaseColorMix", "_RimBiasIntensity",
            "_RimBlendStrength", "_RimBrightness", "_RimLightingInvert", "_RimMask",
            "_RimMaskChannel", "_RimMaskInvert", "_RimMaskPan", "_RimMaskUV",
            "_RimMask_ST", "_RimPoiBlendMode", "_RimPower", "_RimShadowAlpha",
            "_RimShadowMaskInvert", "_RimShadowMaskRampType",
            "_RimShadowMaskStrength", "_RimShadowToggle", "_RimShadowWidth",
            "_RimSharpness", "_RimStrength", "_RimTex", "_RimTexPan", "_RimTexUV",
            "_RimTex_ST", "_RimWidth",
        };

        private static readonly string[] MembersRIMSTYLE_UTS2 =
        {
            "_Add_Antipodean_RimLight", "_Ap_RimLightColor",
            "_Ap_RimLight_FeatherOff", "_Ap_RimLight_Power",
            "_Is_LightColor_Ap_RimLight", "_Is_LightColor_RimLight",
            "_LightDirection_MaskOn", "_RimApColorThemeIndex",
            "_RimLight_FeatherOff", "_RimLight_InsideMask", "_RimLight_Power",
            "_Set_RimLightMask", "_Set_RimLightMaskChannel", "_Set_RimLightMaskPan",
            "_Set_RimLightMaskUV", "_Set_RimLightMask_ST",
            "_Tweak_LightDirection_MaskLevel", "_Tweak_RimLightMaskLevel",
        };

        private static readonly string[] MembersRIMSTYLE_LILTOON =
        {
            "_RimBackfaceMask", "_RimBlendMode", "_RimBlur", "_RimBorder",
            "_RimColor", "_RimColorTex", "_RimColorTexPan", "_RimColorTexUV",
            "_RimColorTex_ST", "_RimDirRange", "_RimDirStrength",
            "_RimEnableLighting", "_RimFresnelPower", "_RimIndirBlur",
            "_RimIndirBorder", "_RimIndirColor", "_RimIndirRange",
            "_RimMainStrength", "_RimMaskOnlyMask", "_RimNormalStrength",
            "_RimShadowMask", "_RimVRParallaxStrength",
        };

        private static readonly string[] MembersPOI_RIM2 =
        {
            "_Add_Antipodean_Rim2Light", "_Ap_Rim2LightColor",
            "_Ap_Rim2Light_FeatherOff", "_Ap_Rim2Light_Power",
            "_AudioLinkRim2BrightnessAdd", "_AudioLinkRim2BrightnessBand",
            "_AudioLinkRim2EmissionAdd", "_AudioLinkRim2EmissionBand",
            "_AudioLinkRim2WidthAdd", "_AudioLinkRim2WidthBand",
            "_Is_LightColor_Ap_Rim2Light", "_Is_LightColor_Rim2Light",
            "_Is_NormalMapToRim2Light", "_LightDirection_MaskOn2",
            "_Rim2ApColorThemeIndex", "_Rim2ApplyAlpha", "_Rim2ApplyAlphaBlend",
            "_Rim2ApplyGlobalMaskBlendType", "_Rim2ApplyGlobalMaskIndex",
            "_Rim2BackfaceMask", "_Rim2BaseColorMix", "_Rim2BiasIntensity",
            "_Rim2BlendMode", "_Rim2BlendStrength", "_Rim2Blur", "_Rim2Border",
            "_Rim2Brightness", "_Rim2Color", "_Rim2ColorTex", "_Rim2ColorTexPan",
            "_Rim2ColorTexUV", "_Rim2ColorTex_ST", "_Rim2DirRange",
            "_Rim2DirStrength", "_Rim2EnableLighting", "_Rim2FresnelPower",
            "_Rim2GlobalMask", "_Rim2GlobalMaskBlendType", "_Rim2HueSelectOrShift",
            "_Rim2HueShift", "_Rim2HueShiftColorSpace", "_Rim2HueShiftEnabled",
            "_Rim2HueShiftSpeed", "_Rim2IndirBlur", "_Rim2IndirBorder",
            "_Rim2IndirColor", "_Rim2IndirRange", "_Rim2LightColor",
            "_Rim2LightColorThemeIndex", "_Rim2Light_FeatherOff",
            "_Rim2Light_InsideMask", "_Rim2Light_Power", "_Rim2LightingInvert",
            "_Rim2MainStrength", "_Rim2Mask", "_Rim2MaskChannel", "_Rim2MaskInvert",
            "_Rim2MaskOnlyMask", "_Rim2MaskPan", "_Rim2MaskUV", "_Rim2Mask_ST",
            "_Rim2NormalStrength", "_Rim2Power", "_Rim2ShadowAlpha",
            "_Rim2ShadowMask", "_Rim2ShadowMaskInvert", "_Rim2ShadowMaskRampType",
            "_Rim2ShadowMaskStrength", "_Rim2ShadowToggle", "_Rim2ShadowWidth",
            "_Rim2Sharpness", "_Rim2Strength", "_Rim2Tex", "_Rim2TexPan",
            "_Rim2TexUV", "_Rim2Tex_ST", "_Rim2VRParallaxStrength", "_Rim2Width",
            "_RimPoi2BlendMode", "_Set_Rim2LightMask", "_Set_Rim2LightMaskChannel",
            "_Set_Rim2LightMaskPan", "_Set_Rim2LightMaskUV", "_Set_Rim2LightMask_ST",
            "_Tweak_LightDirection_MaskLevel2", "_Tweak_Rim2LightMaskLevel",
        };

        private static readonly string[] MembersRIM2STYLE_POIYOMI =
        {
            "_AudioLinkRim2BrightnessAdd", "_AudioLinkRim2BrightnessBand",
            "_AudioLinkRim2EmissionAdd", "_AudioLinkRim2EmissionBand",
            "_AudioLinkRim2WidthAdd", "_AudioLinkRim2WidthBand", "_Rim2ApplyAlpha",
            "_Rim2ApplyAlphaBlend", "_Rim2ApplyGlobalMaskBlendType",
            "_Rim2ApplyGlobalMaskIndex", "_Rim2BaseColorMix", "_Rim2BiasIntensity",
            "_Rim2BlendStrength", "_Rim2Brightness", "_Rim2LightingInvert",
            "_Rim2Mask", "_Rim2MaskChannel", "_Rim2MaskInvert", "_Rim2MaskPan",
            "_Rim2MaskUV", "_Rim2Mask_ST", "_Rim2Power", "_Rim2ShadowAlpha",
            "_Rim2ShadowMaskInvert", "_Rim2ShadowMaskRampType",
            "_Rim2ShadowMaskStrength", "_Rim2ShadowToggle", "_Rim2ShadowWidth",
            "_Rim2Sharpness", "_Rim2Strength", "_Rim2Tex", "_Rim2TexPan",
            "_Rim2TexUV", "_Rim2Tex_ST", "_Rim2Width", "_RimPoi2BlendMode",
        };

        private static readonly string[] MembersRIM2STYLE_UTS2 =
        {
            "_Add_Antipodean_Rim2Light", "_Ap_Rim2LightColor",
            "_Ap_Rim2Light_FeatherOff", "_Ap_Rim2Light_Power",
            "_Is_LightColor_Ap_Rim2Light", "_Is_LightColor_Rim2Light",
            "_LightDirection_MaskOn2", "_Rim2ApColorThemeIndex",
            "_Rim2Light_FeatherOff", "_Rim2Light_InsideMask", "_Rim2Light_Power",
            "_Set_Rim2LightMask", "_Set_Rim2LightMaskChannel",
            "_Set_Rim2LightMaskPan", "_Set_Rim2LightMaskUV", "_Set_Rim2LightMask_ST",
            "_Tweak_LightDirection_MaskLevel2", "_Tweak_Rim2LightMaskLevel",
        };

        private static readonly string[] MembersRIM2STYLE_LILTOON =
        {
            "_Rim2BackfaceMask", "_Rim2BlendMode", "_Rim2Blur", "_Rim2Border",
            "_Rim2Color", "_Rim2ColorTex", "_Rim2ColorTexPan", "_Rim2ColorTexUV",
            "_Rim2ColorTex_ST", "_Rim2DirRange", "_Rim2DirStrength",
            "_Rim2EnableLighting", "_Rim2FresnelPower", "_Rim2IndirBlur",
            "_Rim2IndirBorder", "_Rim2IndirColor", "_Rim2IndirRange",
            "_Rim2MainStrength", "_Rim2MaskOnlyMask", "_Rim2NormalStrength",
            "_Rim2ShadowMask", "_Rim2VRParallaxStrength",
        };
    }
}
