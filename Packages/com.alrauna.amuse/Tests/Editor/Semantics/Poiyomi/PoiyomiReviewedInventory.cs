using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The classified keyword and texture inventory of the review
    /// record of 2026-11-23, pinned as test data. The record at
    /// docs/superpowers/investigations/2026-11-23-poiyomi-reviewed-route-
    /// keywords.md is the source. The inventory tests compare it
    /// against the installed shader keyword space and property list,
    /// so a vendor update that adds a keyword or a texture fails the
    /// pin before any route reads it.
    /// <para>
    /// The keyword list holds the 104 local keywords of the base
    /// pass. The two engine keywords of the record stay separate.
    /// The texture list holds the 138 unique texture names of the
    /// 139 declaration lines, including the commented declaration of
    /// _Udon_VideoTex. The sampler column carries the verified
    /// sampler of record section 4.3 for the 107 companion rows,
    /// with the <c>_MainTex</c> sampler mapped to
    /// <see cref="AtlasCompanionSampler.Member"/>, and null for every
    /// row that is not a companion. The stochastic column is true
    /// when the record names a stochastic property for the texture.
    /// </para>
    /// </summary>
    internal static class PoiyomiReviewedInventory
    {
        internal static readonly IReadOnlyList<
            (string Texture, string Group, string Class,
                AtlasCompanionSampler? Sampler, bool Stochastic)> Textures =
            new (string, string, string, AtlasCompanionSampler?, bool)[]
            {
            ("_MainTex", "Always on", "member", null, true),
            ("_BumpMap", "Always on", "companion", AtlasCompanionSampler.Member, true),
            ("_AlphaMask", "Always on", "companion", AtlasCompanionSampler.Member, false),
            ("_MainColorAdjustTexture", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_MainGradationTex", "KeyOnly", "neutral", null, false),
            ("_DetailMask", "Color effects UV", "companion", AtlasCompanionSampler.Member, true),
            ("_DetailTex", "Color effects UV", "companion", AtlasCompanionSampler.Member, true),
            ("_DetailNormalMap", "Color effects UV", "companion", AtlasCompanionSampler.Member, true),
            ("_DecalMask", "Refused", "refused feature", null, false),
            ("_Udon_VideoTex", "Refused", "refused feature", null, false),
            ("_DecalTexture", "Refused", "refused feature", null, false),
            ("_DecalTexture1", "Refused", "refused feature", null, false),
            ("_DecalTexture2", "Refused", "refused feature", null, false),
            ("_DecalTexture3", "Refused", "refused feature", null, false),
            ("_BackFaceTexture", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_BackFaceMask", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_RGBMask", "Color effects UV", "companion", AtlasCompanionSampler.InlineTrilinearRepeat, false),
            ("_RGBAMetallicMaps", "Color effects UV", "companion", AtlasCompanionSampler.InlineTrilinearRepeat, true),
            ("_RGBASmoothnessMaps", "Color effects UV", "companion", AtlasCompanionSampler.InlineTrilinearRepeat, true),
            ("_RedTexture", "Color effects UV", "companion", AtlasCompanionSampler.InlineTrilinearRepeat, true),
            ("_RgbNormalR", "Color effects UV", "companion", AtlasCompanionSampler.InlineTrilinearRepeat, true),
            ("_GreenTexture", "Color effects UV", "companion", AtlasCompanionSampler.InlineTrilinearRepeat, true),
            ("_RgbNormalG", "Color effects UV", "companion", AtlasCompanionSampler.InlineTrilinearRepeat, true),
            ("_BlueTexture", "Color effects UV", "companion", AtlasCompanionSampler.InlineTrilinearRepeat, true),
            ("_RgbNormalB", "Color effects UV", "companion", AtlasCompanionSampler.InlineTrilinearRepeat, true),
            ("_AlphaTexture", "Color effects UV", "companion", AtlasCompanionSampler.InlineTrilinearRepeat, true),
            ("_RgbNormalA", "Color effects UV", "companion", AtlasCompanionSampler.InlineTrilinearRepeat, true),
            ("_LightingAOMaps", "Always on", "companion", AtlasCompanionSampler.Member, false),
            ("_LightingDetailShadowMaps", "Always on", "companion", AtlasCompanionSampler.Member, false),
            ("_LightingShadowMasks", "Always on", "companion", AtlasCompanionSampler.Member, false),
            ("_LightDataSDFMap", "Always on", "companion", AtlasCompanionSampler.InlineLinearRepeat, false),
            ("_ToonRamp", "KeyOnly", "neutral", null, false),
            ("_ShadowColorTex", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_Shadow2ndColorTex", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_Shadow3rdColorTex", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_ShadowStrengthMask", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_ShadowBorderMask", "Lighting", "companion", AtlasCompanionSampler.InlineLinearRepeat, false),
            ("_MultilayerMathBlurMap", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_1st_ShadeMap", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_2nd_ShadeMap", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_SkinLUT", "KeyOnly", "neutral", null, false),
            ("_SkinThicknessMap", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_ClothDFG", "KeyOnly", "neutral", null, false),
            ("_ClothMetallicSmoothnessMap", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_SDFShadingTexture", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_AnisoColorMap", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_Matcap", "KeyOnly", "neutral", null, false),
            ("_MatcapMask", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_Matcap0NormalMap", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_Matcap2", "KeyOnly", "neutral", null, false),
            ("_Matcap2Mask", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_Matcap1NormalMap", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_Matcap3", "KeyOnly", "neutral", null, false),
            ("_Matcap3Mask", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_Matcap2NormalMap", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_Matcap4", "KeyOnly", "neutral", null, false),
            ("_Matcap4Mask", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_Matcap3NormalMap", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_CubeMap", "KeyOnly", "neutral", null, false),
            ("_CubeMapMask", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_Set_RimLightMask", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_RimMask", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_RimTex", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_RimColorTex", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_Set_Rim2LightMask", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_Rim2Mask", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_Rim2Tex", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_Rim2ColorTex", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_SSSThicknessMap", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_MochieMetallicMaps", "Lighting", "companion", AtlasCompanionSampler.Member, true),
            ("_MochieReflCube", "KeyOnly", "neutral", null, false),
            ("_AnisotropyMap", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_ClearCoatMaps", "Lighting", "companion", AtlasCompanionSampler.Member, true),
            ("_ClearCoatFallback", "KeyOnly", "neutral", null, false),
            ("_RimEnviroMask", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_HighColor_Tex", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_Set_HighColorMask", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_SmoothnessTex", "Lighting", "companion", AtlasCompanionSampler.InlineLinearRepeat, false),
            ("_MetallicGlossMap", "KeyOnly", "neutral", null, false),
            ("_ReflectionColorTex", "Lighting", "companion", AtlasCompanionSampler.InlineLinearRepeat, false),
            ("_ReflectionCubeTex", "KeyOnly", "neutral", null, false),
            ("_BacklightColorTex", "Lighting", "companion", AtlasCompanionSampler.Member, false),
            ("_OutlineMask", "Always on", "companion", AtlasCompanionSampler.InlineLinearRepeat, false),
            ("_OutlineTexture", "Always on", "companion", AtlasCompanionSampler.Member, false),
            ("_DepthBulgeMask", "Color effects UV", "companion", AtlasCompanionSampler.InlineLinearRepeat, false),
            ("_DissolveEdgeGradient", "KeyOnly", "neutral", null, false),
            ("_DissolveToTexture", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_DissolveNoiseTexture", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_DissolveDetailNoise", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_DissolveMask", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_FlipbookTexArray", "Refused", "refused feature", null, false),
            ("_FlipbookMask", "Refused", "refused feature", null, false),
            ("_EmissionMask", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_EmissionMap", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_EmissionScrollingCurve", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_EmissionMask1", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_EmissionMap1", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_EmissionScrollingCurve1", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_EmissionMask2", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_EmissionMap2", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_EmissionScrollingCurve2", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_EmissionMask3", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_EmissionMap3", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_EmissionScrollingCurve3", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_GlitterTexture", "Refused", "refused feature", null, false),
            ("_GlitterColorMap", "Refused", "refused feature", null, false),
            ("_GlitterMask", "Refused", "refused feature", null, false),
            ("_PathingMap", "Color effects UV", "companion", AtlasCompanionSampler.InlinePointRepeat, false),
            ("_PathingColorMap", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_MirrorTexture", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_DepthMask", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_DepthTexture", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_TextGlyphs", "KeyOnly", "neutral", null, false),
            ("_ParallaxInternalMap", "Refused", "refused feature", null, false),
            ("_ParallaxInternalMapMask", "Refused", "refused feature", null, false),
            ("_VideoPixelTexture", "Color effects UV", "companion", AtlasCompanionSampler.Own, false),
            ("_VideoMaskTexture", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_VideoGameboyRamp", "KeyOnly", "neutral", null, false),
            ("_VoronoiMask", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_VoronoiNoise", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_TruchetTex", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_TruchetMask", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_ALDecalColorMask", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_VertexBasicsMask", "Color effects UV", "companion", AtlasCompanionSampler.InlineLinearRepeat, false),
            ("_VertexManipulationHeightMask", "Color effects UV", "companion", AtlasCompanionSampler.InlineLinearRepeat, false),
            ("_LookAtMask", "Color effects UV", "companion", AtlasCompanionSampler.InlineLinearRepeat, false),
            ("_VertexGlitchMap", "KeyOnly", "neutral", null, false),
            ("_UzumoreMask", "Color effects UV", "companion", AtlasCompanionSampler.InlineClamp, false),
            ("_GlobalMaskTexture0", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_GlobalMaskTexture1", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_GlobalMaskTexture2", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_GlobalMaskTexture3", "Matcap rim emission masks", "companion", AtlasCompanionSampler.Member, false),
            ("_DistortionMask", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_DistortionFlowTexture", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_DistortionFlowTexture1", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_HeightMap", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),
            ("_Heightmask", "Color effects UV", "companion", AtlasCompanionSampler.InlineLinearRepeat, false),
            ("_PPMask", "Color effects UV", "companion", AtlasCompanionSampler.Member, false),

            };

        internal static readonly IReadOnlyList<
            (string Keyword, string Group, string Verdict)> Keywords =
            new (string, string, string)[]
            {
            ("PROP_LIGHTINGAOMAPS", "Always on", "Companion"),
            ("PROP_LIGHTINGDETAILSHADOWMAPS", "Always on", "Companion"),
            ("PROP_LIGHTINGSHADOWMASKS", "Always on", "Companion"),
            ("PROP_LIGHTDATASDFMAP", "Always on", "Conditional"),
            ("COLOR_GRADING_HDR", "Color effects UV", "Companion"),
            ("PROP_MAINGRADATIONTEX", "KeyOnly", "KeyOnly"),
            ("POI_GLOBALMASK_TEXTURES", "Matcap rim emission masks", "Conditional"),
            ("USER_LUT", "Color effects UV", "Conditional"),
            ("POI_UDIMDISCARD", "Color effects UV", "Conditional"),
            ("POI_PARALLAX", "Color effects UV", "Conditional"),
            ("POI_AUDIOLINK", "Color effects UV", "Conditional"),
            ("POI_BLACKLIGHTMASKING", "KeyOnly", "KeyOnly"),
            ("FINALPASS", "Color effects UV", "Companion"),
            ("AUTO_EXPOSURE", "Color effects UV", "Companion"),
            ("POI_VERTEX_LOOKAT", "Color effects UV", "Companion"),
            ("POI_VERTEX_GLITCHING", "KeyOnly", "KeyOnly"),
            ("POI_VERTEX_GLITCHING_TEXTURE", "KeyOnly", "KeyOnly"),
            ("POI_DEPTHBULGE", "Color effects UV", "Conditional"),
            ("POI_BACKFACE", "Color effects UV", "Companion"),
            ("VIGNETTE", "Color effects UV", "Companion"),
            ("GEOM_TYPE_MESH", "KeyOnly", "KeyOnly"),
            ("PROP_RGBMASK", "Color effects UV", "Companion"),
            ("PROP_RGBAMETALLICMAPS", "Color effects UV", "Companion"),
            ("PROP_RGBASMOOTHNESSMAPS", "Color effects UV", "Companion"),
            ("PROP_REDTEXTURE", "Color effects UV", "Companion"),
            ("PROP_RGBNORMALR", "Color effects UV", "Companion"),
            ("PROP_GREENTEXTURE", "Color effects UV", "Companion"),
            ("PROP_RGBNORMALG", "Color effects UV", "Companion"),
            ("PROP_BLUETEXTURE", "Color effects UV", "Companion"),
            ("PROP_RGBNORMALB", "Color effects UV", "Companion"),
            ("PROP_ALPHATEXTURE", "Color effects UV", "Companion"),
            ("PROP_RGBNORMALA", "Color effects UV", "Companion"),
            ("POI_LTCGI", "KeyOnly", "KeyOnly"),
            ("VIGNETTE_MASKED", "KeyOnly", "KeyOnly"),
            ("_LIGHTINGMODE_TEXTURERAMP", "Lighting", "Conditional"),
            ("_LIGHTINGMODE_MULTILAYER_MATH", "Lighting", "Companion"),
            ("_LIGHTINGMODE_SHADEMAP", "Lighting", "Companion"),
            ("_LIGHTINGMODE_REALISTIC", "KeyOnly", "KeyOnly"),
            ("_LIGHTINGMODE_WRAPPED", "KeyOnly", "KeyOnly"),
            ("_LIGHTINGMODE_SKIN", "Lighting", "Companion"),
            ("_LIGHTINGMODE_FLAT", "KeyOnly", "KeyOnly"),
            ("_LIGHTINGMODE_CLOTH", "Lighting", "Companion"),
            ("_LIGHTINGMODE_SDF", "Lighting", "Conditional"),
            ("GEOM_TYPE_BRANCH", "Refused", "Refuse"),
            ("GEOM_TYPE_BRANCH_DETAIL", "Refused", "Refuse"),
            ("GEOM_TYPE_FROND", "Refused", "Refuse"),
            ("DEPTH_OF_FIELD_COC_VIEW", "Refused", "Refuse"),
            ("PROP_DECALMASK", "Refused", "Refuse"),
            ("DISTORT", "Color effects UV", "Conditional"),
            ("POI_ANISOTROPICS", "Lighting", "Companion"),
            ("POI_MATCAP0", "Matcap rim emission masks", "Conditional"),
            ("POI_MATCAP0_CUSTOM_NORMAL", "Matcap rim emission masks", "Companion"),
            ("COLOR_GRADING_HDR_3D", "Matcap rim emission masks", "Conditional"),
            ("POI_MATCAP1_CUSTOM_NORMAL", "Matcap rim emission masks", "Companion"),
            ("POI_MATCAP2", "Matcap rim emission masks", "Conditional"),
            ("POI_MATCAP2_CUSTOM_NORMAL", "Matcap rim emission masks", "Companion"),
            ("POI_MATCAP3", "Matcap rim emission masks", "Conditional"),
            ("POI_MATCAP3_CUSTOM_NORMAL", "Matcap rim emission masks", "Companion"),
            ("_CUBEMAP", "Lighting", "Companion"),
            ("POI_AL_DECAL", "Color effects UV", "Conditional"),
            ("POI_AL_VOLUMECOLOR", "Color effects UV", "Conditional"),
            ("_SUNDISK_HIGH_QUALITY", "Refused", "Refuse"),
            ("_EMISSION", "Matcap rim emission masks", "Conditional"),
            ("POI_EMISSION_1", "Matcap rim emission masks", "Conditional"),
            ("POI_EMISSION_2", "Matcap rim emission masks", "Conditional"),
            ("POI_EMISSION_3", "Matcap rim emission masks", "Conditional"),
            ("_GLOSSYREFLECTIONS_OFF", "Matcap rim emission masks", "Conditional"),
            ("_RIMSTYLE_POIYOMI", "Matcap rim emission masks", "Companion"),
            ("_RIMSTYLE_UTS2", "Matcap rim emission masks", "Companion"),
            ("_RIMSTYLE_LILTOON", "Matcap rim emission masks", "Companion"),
            ("POI_RIM2", "Matcap rim emission masks", "Conditional"),
            ("_RIM2STYLE_POIYOMI", "Matcap rim emission masks", "Companion"),
            ("_RIM2STYLE_UTS2", "Matcap rim emission masks", "Companion"),
            ("_RIM2STYLE_LILTOON", "Matcap rim emission masks", "Companion"),
            ("_POI_DEPTH_RIMLIGHT", "KeyOnly", "KeyOnly"),
            ("_SUNDISK_SIMPLE", "Refused", "Refuse"),
            ("POI_SUBSURFACESCATTERING", "Lighting", "Companion"),
            ("MOCHIE_PBR", "Lighting", "Companion"),
            ("GGX_ANISOTROPICS", "Lighting", "Companion"),
            ("POI_CLEARCOAT", "Lighting", "Companion"),
            ("POI_ENVIRORIM", "Lighting", "Companion"),
            ("POI_STYLIZED_StylizedSpecular", "Lighting", "Companion"),
            ("_STYLIZEDREFLECTIONMODE_UNITYCHAN", "Lighting", "Companion"),
            ("_STYLIZEDREFLECTIONMODE_LILTOON", "Lighting", "Companion"),
            ("POI_PATHING", "Color effects UV", "Conditional"),
            ("POI_MIRROR", "KeyOnly", "KeyOnly"),
            ("POI_MIRROR_TEXTURE", "Color effects UV", "Companion"),
            ("GRAIN", "Color effects UV", "Companion"),
            ("EFFECT_BUMP", "Color effects UV", "Conditional"),
            ("POSTPROCESS", "Color effects UV", "Companion"),
            ("PROP_PPMASK", "Color effects UV", "Companion"),
            ("POI_INTERNALPARALLAX", "Refused", "Refuse"),
            ("POI_NORMALCORRECT", "KeyOnly", "KeyOnly"),
            ("POI_VIDEO_EFFECTS", "Color effects UV", "Conditional"),
            ("POI_BACKLIGHT", "Lighting", "Companion"),
            ("POIBS_ENABLE", "KeyOnly", "KeyOnly"),
            ("POIBS_BLOOMFOG", "KeyOnly", "KeyOnly"),
            ("BSSBLOOMFOGTYPE_HEIGHT", "KeyOnly", "KeyOnly"),
            ("_STOCHASTICMODE_DELIOT_HEITZ", "Color effects UV", "Conditional"),
            ("_STOCHASTICMODE_HEXTILE", "Color effects UV", "Conditional"),
            ("_STOCHASTICMODE_NONE", "KeyOnly", "KeyOnly"),
            ("POI_VORONOI", "Color effects UV", "Conditional"),
            ("POI_TRUCHET", "Color effects UV", "Conditional"),
            ("POI_UZUMORE", "Color effects UV", "Conditional"),

            };

        /// <summary>
        /// The two engine keywords of the base pass pragma. They sit
        /// outside the allowed and refused lists. A material that
        /// enables one refuses the use with
        /// <c>KeywordNotAllowed</c>.
        /// </summary>
        internal static readonly IReadOnlyCollection<string>
            EngineKeywords = new string[]
            {
            "FOG_EXP2",
            "VERTEXLIGHT_ON",

            };

        /// <summary>
        /// The shared sampler and stochastic pin of the reviewed
        /// route. Every group test calls it from its inventory test.
        /// For every route of the group, the record must class the
        /// texture as a companion of that group, the route must name
        /// the verified sampler of the record, and the route must
        /// carry the stochastic condition exactly when the record
        /// names a stochastic property. A wrong sampler kind is a
        /// false positive, so a mismatch fails the group inventory
        /// test.
        /// </summary>
        internal static void AssertRoutesMatchInventory(
            string group,
            IReadOnlyList<AtlasCompanionRoute> routes)
        {
            var byName = new Dictionary<string,
                (string Group, string Class,
                    AtlasCompanionSampler? Sampler, bool Stochastic)>();
            foreach (var row in Textures)
            {
                byName[row.Texture] =
                    (row.Group, row.Class, row.Sampler, row.Stochastic);
            }

            Assert.That(
                routes, Is.Not.Empty,
                "fixture precondition: the group carries routes");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var route in routes)
            {
                Assert.That(
                    seen.Add(route.Property), Is.True,
                    "the route '" + route.Property +
                    "' appears twice in the group '" + group + "'");
                Assert.That(
                    byName.TryGetValue(
                        route.Property,
                        out var row), Is.True,
                    "the route '" + route.Property +
                    "' has no inventory row");
                Assert.That(
                    row.Class, Is.EqualTo("companion"),
                    "the route '" + route.Property +
                    "' must be a companion in the record");
                Assert.That(
                    row.Group, Is.EqualTo(group),
                    "the route '" + route.Property +
                    "' must belong to the group '" + group + "'");
                Assert.That(
                    row.Sampler, Is.Not.Null,
                    "the companion '" + route.Property +
                    "' carries a verified sampler in the record");
                Assert.That(
                    route.Sampler, Is.EqualTo(row.Sampler),
                    "the route '" + route.Property +
                    "' must use the verified sampler of the record");
                var stochasticCondition =
                    route.Property + "Stochastic";
                Assert.That(
                    route.ConditionProperties.Contains(
                        stochasticCondition),
                    Is.EqualTo(row.Stochastic),
                    "the route '" + route.Property +
                    "' carries the stochastic condition exactly when" +
                    " the record names " + stochasticCondition);
            }
        }
    }
}
