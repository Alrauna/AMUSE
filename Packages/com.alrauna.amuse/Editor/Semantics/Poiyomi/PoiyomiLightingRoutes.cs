using System;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The feature group 'Lighting' of the reviewed Poiyomi 9.3
    /// route. The review record of 2026-11-23, sections 4.1, 4.3,
    /// 5, and 6, carries the rows of this group. The record at
    /// docs/superpowers/investigations/2026-11-23-poiyomi-reviewed-route-
    /// keywords.md is the source.
    /// <para>
    /// Four table rules guard the UV0 reads that sit outside the
    /// texture routes. Each rule runs when its own mode keyword is
    /// on, whatever other mode keyword sits on with it. A stale
    /// second mode keyword never excuses a broken rule.
    /// </para>
    /// <para>
    /// Every companion row of the group carries a route. The three
    /// irregular rows are hand written. The SDF shading texture
    /// never moves, because the sample flips its coordinate with
    /// the light. The enviro rim mask carries no transform
    /// property. The smoothness and reflection color textures
    /// sample raw UV0 with no selector and no transform.
    /// </para>
    /// <para>
    /// The dead value families hold the expanded exact names of the
    /// record prefixes of section 6, expanded against the property
    /// universe of the two bundled files on 2026-11-23.
    /// </para>
    /// </summary>
    internal static class PoiyomiLightingRoutes
    {
        /// <summary>The 17 keywords of the record, section 4.1.</summary>
        internal static readonly string[] AllowedKeywords =
        {
            "_LIGHTINGMODE_TEXTURERAMP",
            "_LIGHTINGMODE_MULTILAYER_MATH",
            "_LIGHTINGMODE_SHADEMAP",
            "_LIGHTINGMODE_SKIN",
            "_LIGHTINGMODE_CLOTH",
            "_LIGHTINGMODE_SDF",
            "POI_ANISOTROPICS",
            "_CUBEMAP",
            "POI_SUBSURFACESCATTERING",
            "MOCHIE_PBR",
            "GGX_ANISOTROPICS",
            "POI_CLEARCOAT",
            "POI_ENVIRORIM",
            "POI_STYLIZED_StylizedSpecular",
            "_STYLIZEDREFLECTIONMODE_UNITYCHAN",
            "_STYLIZEDREFLECTIONMODE_LILTOON",
            "POI_BACKLIGHT",
        };

        /// <summary>
        /// The record lists no neutral texture for this group. The
        /// three lookup textures of the lighting code sit in the
        /// KeyOnly group of the record.
        /// </summary>
        internal static readonly string[] NeutralTextures =
            Array.Empty<string>();

        /// <summary>
        /// Every property that a table rule of the group reads. The
        /// route conditions of the textures sit on the routes
        /// themselves, so the capture adds them per assigned
        /// texture.
        /// </summary>
        internal static readonly string[] ConditionProperties =
        {
            "_ToonRampCount",
            "_ToonRampUVSelector",
            "_ShadowBorderMaskLOD",
            "_SDFShadingTextureUV",
            "_PBRSplitMaskSample",
            "_MochieMetallicMasksUV",
            "_PBRMaskScaleTiling",
            "_MochieMetallicMasksPan",
            "_PBRSplitMaskStochastic",
        };

        // The feature gates of the textures. Each gate lists the
        // keywords whose sample lines reach the texture. A texture
        // with two keywords moves when one of them is on, because a
        // move that the shader never samples is invisible.

        private static readonly string[] MultilayerMath =
        {
            "_LIGHTINGMODE_MULTILAYER_MATH",
        };

        private static readonly string[] Shademap =
        {
            "_LIGHTINGMODE_SHADEMAP",
        };

        private static readonly string[] Skin = { "_LIGHTINGMODE_SKIN" };

        private static readonly string[] Cloth =
        {
            "_LIGHTINGMODE_CLOTH",
        };

        private static readonly string[] Sdf = { "_LIGHTINGMODE_SDF" };

        private static readonly string[] Anisotropics =
        {
            "POI_ANISOTROPICS",
        };

        private static readonly string[] Cubemap = { "_CUBEMAP" };

        private static readonly string[] Subsurface =
        {
            "POI_SUBSURFACESCATTERING",
        };

        private static readonly string[] MochiePbr = { "MOCHIE_PBR" };

        private static readonly string[] GgxMochie =
        {
            "GGX_ANISOTROPICS",
            "MOCHIE_PBR",
        };

        private static readonly string[] Clearcoat =
        {
            "POI_CLEARCOAT",
        };

        private static readonly string[] EnviroRim =
        {
            "POI_ENVIRORIM",
        };

        private static readonly string[] StylizedUnityChan =
        {
            "POI_STYLIZED_StylizedSpecular",
            "_STYLIZEDREFLECTIONMODE_UNITYCHAN",
        };

        private static readonly string[] StylizedLilToon =
        {
            "POI_STYLIZED_StylizedSpecular",
            "_STYLIZEDREFLECTIONMODE_LILTOON",
        };

        private static readonly string[] Backlight = { "POI_BACKLIGHT" };

        // The expanded members of the dead value families. The
        // record carries the prefix tokens. These lists hold the
        // exact names a material can save, expanded against the
        // property universe of the two bundled files on 2026-11-23.

        private static readonly string[] TextureRampMembers =
        {
            "_ShadowOffset", "_ToonRamp", "_ToonRampCount",
            "_ToonRampUVSelector",
        };

        private static readonly string[] MultilayerMathMembers =
        {
            "_LightingMulitlayerNonLinear", "_MultilayerMathBlurMap",
            "_MultilayerMathBlurMapPan", "_MultilayerMathBlurMapUV",
            "_MultilayerMathBlurMap_ST", "_MultilayerShadowStrength",
            "_Shadow2ndBlur", "_Shadow2ndBorder", "_Shadow2ndColor",
            "_Shadow2ndColorTex", "_Shadow2ndColorTexAddition",
            "_Shadow2ndColorTexPan", "_Shadow2ndColorTexUV",
            "_Shadow2ndColorTex_ST", "_Shadow2ndNormalStrength",
            "_Shadow2ndReceive", "_Shadow3rdBlur", "_Shadow3rdBorder",
            "_Shadow3rdColor", "_Shadow3rdColorTex",
            "_Shadow3rdColorTexAddition", "_Shadow3rdColorTexPan",
            "_Shadow3rdColorTexUV", "_Shadow3rdColorTex_ST",
            "_Shadow3rdNormalStrength", "_Shadow3rdReceive",
            "_ShadowAOShift", "_ShadowAOShift2", "_ShadowBlur",
            "_ShadowBorder", "_ShadowBorderColor", "_ShadowBorderMapToggle",
            "_ShadowBorderMask", "_ShadowBorderMaskLOD",
            "_ShadowBorderMaskPan", "_ShadowBorderMaskUV",
            "_ShadowBorderMask_ST", "_ShadowBorderRange", "_ShadowColor",
            "_ShadowColorTex", "_ShadowColorTexAddition",
            "_ShadowColorTexPan", "_ShadowColorTexUV", "_ShadowColorTex_ST",
            "_ShadowEnvStrength", "_ShadowFlatBlur", "_ShadowFlatBorder",
            "_ShadowMainStrength", "_ShadowMaskType",
            "_ShadowNormalStrength", "_ShadowPostAO", "_ShadowReceive",
            "_ShadowStrengthMask", "_ShadowStrengthMaskLOD",
            "_ShadowStrengthMaskPan", "_ShadowStrengthMaskUV",
            "_ShadowStrengthMask_ST",
        };

        private static readonly string[] ShademapMembers =
        {
            "_BaseColor_Step", "_BaseShade_Feather", "_ShadeColor_Step",
            "_ShadingShadeMapBlendType", "_Use_1stAs2nd",
            "_Use_1stShadeMapAlpha_As_ShadowMask",
            "_Use_2ndShadeMapAlpha_As_ShadowMask", "_Use_BaseAs1st",
        };

        private static readonly string[] SkinMembers =
        {
            "_SkinLUT", "_SkinThicknessMap", "_SkinThicknessMapInvert",
            "_SkinThicknessMapPan", "_SkinThicknessMapUV",
            "_SkinThicknessMap_ST", "_SkinThicknessPower", "_SssBumpBlur",
            "_SssScale", "_SssTransmissionAbsorption",
        };

        private static readonly string[] ClothMembers =
        {
            "_ClothDFG", "_ClothLerp", "_ClothMetallic",
            "_ClothMetallicSmoothnessMap",
            "_ClothMetallicSmoothnessMapInvert",
            "_ClothMetallicSmoothnessMapPan",
            "_ClothMetallicSmoothnessMapUV",
            "_ClothMetallicSmoothnessMap_ST", "_ClothReflectance",
            "_ClothSmoothness",
        };

        private static readonly string[] SdfMembers =
        {
            "_SDFBlur", "_SDFForward", "_SDFLeft", "_SDFShadingTexture",
            "_SDFShadingTexturePan", "_SDFShadingTextureUV",
            "_SDFShadingTexture_ST",
        };

        private static readonly string[] AnisotropicsMembers =
        {
            "_Aniso0Blur", "_Aniso0Edge", "_Aniso0Offset",
            "_Aniso0OffsetMapStrength", "_Aniso0Power", "_Aniso0Strength",
            "_Aniso0SwitchDirection", "_Aniso0Tint", "_Aniso0TintIndex",
            "_Aniso0ToonMode", "_Aniso1Blur", "_Aniso1Edge",
            "_Aniso1Offset", "_Aniso1OffsetMapStrength", "_Aniso1Power",
            "_Aniso1Strength", "_Aniso1SwitchDirection", "_Aniso1Tint",
            "_Aniso1TintIndex", "_Aniso1ToonMode", "_AnisoAdd",
            "_AnisoColorMap", "_AnisoColorMapPan", "_AnisoColorMapUV",
            "_AnisoColorMap_ST", "_AnisoFadeByLightDirection",
            "_AnisoGlobalMask", "_AnisoGlobalMaskBlendType",
            "_AnisoHideInShadow", "_AnisoNormalBlend", "_AnisoReplace",
            "_AnisoUseBaseColor", "_AnisoUseLightColor",
        };

        private static readonly string[] CubemapMembers =
        {
            "_CubeMap", "_CubeMapBlendAmount", "_CubeMapBrightness",
            "_CubeMapColor", "_CubeMapColorThemeIndex", "_CubeMapContrast",
            "_CubeMapEmissionStrength", "_CubeMapHueSelectOrShift",
            "_CubeMapHueShift", "_CubeMapHueShiftColorSpace",
            "_CubeMapHueShiftEnabled", "_CubeMapHueShiftSpeed",
            "_CubeMapIntensity", "_CubeMapLightMask", "_CubeMapMask",
            "_CubeMapMaskChannel", "_CubeMapMaskGlobalMask",
            "_CubeMapMaskGlobalMaskBlendType", "_CubeMapMaskInvert",
            "_CubeMapMaskPan", "_CubeMapMaskUV", "_CubeMapMask_ST",
            "_CubeMapRotation", "_CubeMapRotationPan", "_CubeMapSaturation",
            "_CubeMapSmoothness", "_CubeMapUVMode",
            "_CubeMapWorldNormalsStrength", "_CubemapBlendType",
        };

        private static readonly string[] SubsurfaceMembers =
        {
            "_SSSBaseColorMix", "_SSSColor", "_SSSDistortion", "_SSSSpread",
            "_SSSStrength", "_SSSThicknessMap", "_SSSThicknessMapPan",
            "_SSSThicknessMapUV", "_SSSThicknessMap_ST", "_SSSThicknessMod",
        };

        private static readonly string[] MochiePbrMembers =
        {
            "_Anisotropy", "_AnisotropyMap", "_AnisotropyMapChannel",
            "_AnisotropyMapPan", "_AnisotropyMapUV", "_AnisotropyMap_ST",
            "_BRDFTPSDepthEnabled", "_BRDFTPSReflectionMaskStrength",
            "_BRDFTPSSpecularMaskStrength", "_IgnoreCastedShadows",
            "_LTCGI_UseEO", "_MochieForceFallback", "_MochieGSAAEnabled",
            "_MochieLitFallback", "_MochieMetallicGlobalMask",
            "_MochieMetallicGlobalMaskBlendType",
            "_MochieMetallicMapInvert", "_MochieMetallicMaps",
            "_MochieMetallicMapsMetallicChannel", "_MochieMetallicMapsPan",
            "_MochieMetallicMapsReflectionMaskChannel",
            "_MochieMetallicMapsRoughnessChannel",
            "_MochieMetallicMapsSpecularMaskChannel",
            "_MochieMetallicMapsStochastic", "_MochieMetallicMapsUV",
            "_MochieMetallicMaps_ST", "_MochieMetallicMasksPan",
            "_MochieMetallicMasksUV", "_MochieMetallicMultiplier",
            "_MochieReflCube", "_MochieReflCube_HDR",
            "_MochieReflectionMaskInvert", "_MochieReflectionStrength",
            "_MochieReflectionStrengthGlobalMask",
            "_MochieReflectionStrengthGlobalMaskBlendType",
            "_MochieReflectionTint", "_MochieReflectionTintThemeIndex",
            "_MochieRoughnessMapInvert", "_MochieRoughnessMultiplier",
            "_MochieRoughnessMultiplier2", "_MochieSmoothnessGlobalMask",
            "_MochieSmoothnessGlobalMaskBlendType",
            "_MochieSpecularMaskInvert", "_MochieSpecularStrength",
            "_MochieSpecularStrength2", "_MochieSpecularStrengthGlobalMask",
            "_MochieSpecularStrengthGlobalMaskBlendType",
            "_MochieSpecularTint", "_MochieSpecularTintThemeIndex",
            "_PBRMapsStochastic", "_PBRMaskScaleTiling", "_PBRNormalSelect",
            "_PBRSplitMaskSample", "_PBRSplitMaskStochastic",
            "_PoiGSAAThreshold", "_PoiGSAAVariance",
            "_RefSpecFresnelStrength", "_ReflectionAnisotropicStretch",
            "_SFExposureOcclusion", "_Specular2ndLayer",
        };

        private static readonly string[] GgxAnisotropicsMembers =
        {
            "_Anisotropy", "_AnisotropyMap", "_AnisotropyMapChannel",
            "_AnisotropyMapPan", "_AnisotropyMapUV", "_AnisotropyMap_ST",
            "_ReflectionAnisotropicStretch", "_RoughnessAnisotropy",
        };

        private static readonly string[] ClearcoatMembers =
        {
            "_CCIgnoreCastedShadows", "_ClearCoatFallback",
            "_ClearCoatFallback_HDR", "_ClearCoatForceFallback",
            "_ClearCoatGSAAEnabled", "_ClearCoatGSAAThreshold",
            "_ClearCoatGSAAVariance", "_ClearCoatGlobalMask",
            "_ClearCoatGlobalMaskBlendType", "_ClearCoatLitFallback",
            "_ClearCoatMaps", "_ClearCoatMapsClearCoatMaskChannel",
            "_ClearCoatMapsPan", "_ClearCoatMapsReflectionMaskChannel",
            "_ClearCoatMapsRoughnessChannel",
            "_ClearCoatMapsSpecularMaskChannel", "_ClearCoatMapsStochastic",
            "_ClearCoatMapsUV", "_ClearCoatMaps_ST", "_ClearCoatMaskInvert",
            "_ClearCoatNormalSelect", "_ClearCoatReflectionMaskInvert",
            "_ClearCoatReflectionStrength",
            "_ClearCoatReflectionStrengthGlobalMask",
            "_ClearCoatReflectionStrengthGlobalMaskBlendType",
            "_ClearCoatReflectionTint",
            "_ClearCoatReflectionTintThemeIndex", "_ClearCoatSmoothness",
            "_ClearCoatSmoothnessGlobalMask",
            "_ClearCoatSmoothnessGlobalMaskBlendType",
            "_ClearCoatSmoothnessMapInvert", "_ClearCoatSpecularMaskInvert",
            "_ClearCoatSpecularStrength",
            "_ClearCoatSpecularStrengthGlobalMask",
            "_ClearCoatSpecularStrengthGlobalMaskBlendType",
            "_ClearCoatSpecularTint", "_ClearCoatSpecularTintThemeIndex",
            "_ClearCoatStrength", "_ClearCoatTPSDepthMaskEnabled",
            "_ClearCoatTPSMaskStrength", "_ClearcoatExposureOcclusion",
            "_ClearcoatFresnelStrength",
        };

        private static readonly string[] EnviroRimMembers =
        {
            "_RimEnviroBlur", "_RimEnviroChannel", "_RimEnviroIntensity",
            "_RimEnviroMask", "_RimEnviroMaskPan", "_RimEnviroMaskUV",
            "_RimEnviroMinBrightness", "_RimEnviroSharpness",
            "_RimEnviroWidth",
        };

        private static readonly string[] StylizedSpecularMembers =
        {
            "_ApplyReflection", "_ApplySpecular", "_ApplySpecularFA",
            "_GSAAStrength", "_HighColor", "_HighColorThemeIndex",
            "_HighColor_Power", "_HighColor_Tex", "_HighColor_TexPan",
            "_HighColor_TexUV", "_HighColor_Tex_ST",
            "_Is_BlendAddToHiColor", "_Is_SpecularToHighColor",
            "_Layer1Strength", "_Layer2Size", "_Layer2Strength",
            "_Metallic", "_MetallicGlossMap", "_Reflectance",
            "_ReflectionApplyTransparency", "_ReflectionBlendMode",
            "_ReflectionColor", "_ReflectionColorTex",
            "_ReflectionCubeColor", "_ReflectionCubeEnableLighting",
            "_ReflectionCubeOverride", "_ReflectionCubeTex",
            "_ReflectionCubeTex_HDR", "_SSIgnoreCastedShadows",
            "_Set_HighColorMask", "_Set_HighColorMaskChannel",
            "_Set_HighColorMaskPan", "_Set_HighColorMaskUV",
            "_Set_HighColorMask_ST", "_Smoothness", "_SmoothnessTex",
            "_SpecularBlur", "_SpecularBorder", "_SpecularNormalStrength",
            "_SpecularToon", "_StylizedSpecular2Feather",
            "_StylizedSpecularFeather", "_StylizedSpecularIgnoreNormal",
            "_StylizedSpecularIgnoreShadow", "_StylizedSpecularInvertMask",
            "_StylizedSpecularNormalStrength", "_StylizedSpecularStrength",
            "_Tweak_HighColorMaskLevel", "_UseLightColor", "_UseReflection",
        };

        private static readonly string[] UnityChanMembers =
        {
            "_HighColor", "_HighColorThemeIndex", "_HighColor_Power",
            "_HighColor_Tex", "_HighColor_TexPan", "_HighColor_TexUV",
            "_HighColor_Tex_ST", "_Is_BlendAddToHiColor",
            "_Is_SpecularToHighColor", "_Layer1Strength", "_Layer2Size",
            "_Layer2Strength", "_SSIgnoreCastedShadows",
            "_Set_HighColorMask", "_Set_HighColorMaskChannel",
            "_Set_HighColorMaskPan", "_Set_HighColorMaskUV",
            "_Set_HighColorMask_ST", "_StylizedSpecular2Feather",
            "_StylizedSpecularFeather", "_StylizedSpecularIgnoreNormal",
            "_StylizedSpecularIgnoreShadow", "_StylizedSpecularInvertMask",
            "_StylizedSpecularNormalStrength", "_StylizedSpecularStrength",
            "_Tweak_HighColorMaskLevel", "_UseLightColor",
        };

        private static readonly string[] LilToonMembers =
        {
            "_ApplyReflection", "_ApplySpecular", "_ApplySpecularFA",
            "_GSAAStrength", "_Metallic", "_MetallicGlossMap",
            "_Reflectance", "_ReflectionApplyTransparency",
            "_ReflectionBlendMode", "_ReflectionColor",
            "_ReflectionColorTex", "_ReflectionCubeColor",
            "_ReflectionCubeEnableLighting", "_ReflectionCubeOverride",
            "_ReflectionCubeTex", "_ReflectionCubeTex_HDR", "_Smoothness",
            "_SmoothnessTex", "_SpecularBlur", "_SpecularBorder",
            "_SpecularNormalStrength", "_SpecularToon", "_UseReflection",
        };

        private static readonly string[] BacklightMembers =
        {
            "_BacklightBackfaceMask", "_BacklightBlur", "_BacklightBorder",
            "_BacklightColor", "_BacklightColorTex",
            "_BacklightColorTexPan", "_BacklightColorTexUV",
            "_BacklightColorTex_ST", "_BacklightDirectivity",
            "_BacklightMainStrength", "_BacklightNormalStrength",
            "_BacklightReceiveShadow", "_BacklightViewStrength",
        };

        // The routes and the families, built once. The build touches
        // no other class, so the static initialization order stays
        // safe.
        private static readonly AtlasCompanionRoute[] routeCache =
            BuildRoutes();

        private static readonly AtlasDeadValueFamily[] familyCache =
            BuildFamilies();

        /// <summary>
        /// One companion route for every companion texture of the
        /// record, in record order. Twenty follow the standard
        /// pattern. Three are hand written.
        /// </summary>
        internal static AtlasCompanionRoute[] Routes()
        {
            return routeCache;
        }

        /// <summary>
        /// The first failing sentence of the four table rules, in
        /// rule order. Each rule runs when its own mode keyword is
        /// on, whatever other mode keyword sits on with it.
        /// </summary>
        internal static string UnheldRequiredValue(Material material)
        {
            return TextureRampRule(material) ??
                   BorderMaskLodRule(material) ??
                   SdfShadingRule(material) ??
                   MochieSplitRule(material);
        }

        /// <summary>
        /// One dead value family per keyword of the group. The only
        /// switch of a family is its keyword. The family is dead
        /// when the material does not enable the keyword.
        /// </summary>
        internal static AtlasDeadValueFamily[] Families()
        {
            return familyCache;
        }

        private static AtlasCompanionRoute[] BuildRoutes()
        {
            return new[]
            {
                PoiyomiCompanionRoutes.Standard(
                    "_ShadowColorTex",
                    AtlasCompanionSampler.Member,
                    MultilayerMath,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Shadow2ndColorTex",
                    AtlasCompanionSampler.Member,
                    MultilayerMath,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Shadow3rdColorTex",
                    AtlasCompanionSampler.Member,
                    MultilayerMath,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_ShadowStrengthMask",
                    AtlasCompanionSampler.Member,
                    MultilayerMath,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_ShadowBorderMask",
                    AtlasCompanionSampler.InlineLinearRepeat,
                    MultilayerMath,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_MultilayerMathBlurMap",
                    AtlasCompanionSampler.Member,
                    MultilayerMath,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_1st_ShadeMap",
                    AtlasCompanionSampler.Member,
                    Shademap,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_2nd_ShadeMap",
                    AtlasCompanionSampler.Member,
                    Shademap,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_SkinThicknessMap",
                    AtlasCompanionSampler.Member,
                    Skin,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_ClothMetallicSmoothnessMap",
                    AtlasCompanionSampler.Member,
                    Cloth,
                    false),
                SdfShadingRoute(),
                PoiyomiCompanionRoutes.Standard(
                    "_AnisoColorMap",
                    AtlasCompanionSampler.Member,
                    Anisotropics,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_CubeMapMask",
                    AtlasCompanionSampler.Member,
                    Cubemap,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_SSSThicknessMap",
                    AtlasCompanionSampler.Member,
                    Subsurface,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_MochieMetallicMaps",
                    AtlasCompanionSampler.Member,
                    MochiePbr,
                    true),
                PoiyomiCompanionRoutes.Standard(
                    "_AnisotropyMap",
                    AtlasCompanionSampler.Member,
                    GgxMochie,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_ClearCoatMaps",
                    AtlasCompanionSampler.Member,
                    Clearcoat,
                    true),
                RimEnviroRoute(),
                PoiyomiCompanionRoutes.Standard(
                    "_HighColor_Tex",
                    AtlasCompanionSampler.Member,
                    StylizedUnityChan,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_Set_HighColorMask",
                    AtlasCompanionSampler.Member,
                    StylizedUnityChan,
                    false),
                RawUv0Route(
                    "_SmoothnessTex",
                    AtlasCompanionSampler.InlineLinearRepeat,
                    StylizedLilToon),
                RawUv0Route(
                    "_ReflectionColorTex",
                    AtlasCompanionSampler.InlineLinearRepeat,
                    StylizedLilToon),
                PoiyomiCompanionRoutes.Standard(
                    "_BacklightColorTex",
                    AtlasCompanionSampler.Member,
                    Backlight,
                    false),
            };
        }

        /// <summary>
        /// The ramp row pick reads a mesh UV as data when the ramp
        /// count sits above one and the ramp selector sits at zero.
        /// The selector property doubles as the mesh UV channel, so
        /// a selector other than zero keeps the read away from UV0.
        /// </summary>
        private static string TextureRampRule(Material material)
        {
            if (!material.IsKeywordEnabled("_LIGHTINGMODE_TEXTURERAMP"))
            {
                return null;
            }

            var count = material.GetFloat("_ToonRampCount");
            var selector = material.GetFloat("_ToonRampUVSelector");
            if (count <= 1f || selector != 0f)
            {
                return null;
            }

            return "The property '_ToonRampCount' is " + Number(count) +
                   ". The route needs a count of at most 1, or a" +
                   " ramp row selector other than 0.";
        }

        /// <summary>
        /// The border mask holds two branches with two samplers. The
        /// branch with the explicit derivatives samples a trilinear
        /// inline sampler and reads the UV0 derivative. The route
        /// needs the plain branch, so the LOD must sit at zero.
        /// </summary>
        private static string BorderMaskLodRule(Material material)
        {
            if (!material.IsKeywordEnabled(
                    "_LIGHTINGMODE_MULTILAYER_MATH"))
            {
                return null;
            }

            var lod = material.GetFloat("_ShadowBorderMaskLOD");
            return lod == 0f
                ? null
                : "The property '_ShadowBorderMaskLOD' is " +
                  Number(lod) + ". The route needs 0, because the" +
                  " LOD branch samples with explicit derivatives.";
        }

        /// <summary>
        /// The SDF shading sample multiplies the coordinate x by a
        /// light driven flip. An assigned texture on UV0 refuses,
        /// because the texture must never move.
        /// </summary>
        private static string SdfShadingRule(Material material)
        {
            if (!material.IsKeywordEnabled("_LIGHTINGMODE_SDF"))
            {
                return null;
            }

            if (material.GetTexture("_SDFShadingTexture") == null)
            {
                return null;
            }

            var selector = material.GetFloat("_SDFShadingTextureUV");
            return selector != 0f
                ? null
                : "The property '_SDFShadingTextureUV' is " +
                  Number(selector) + ". The route needs a selector" +
                  " other than 0, because the sample flips the" +
                  " coordinate with the light.";
        }

        /// <summary>
        /// The split mask sample reads the same texture through its
        /// own selector, the merged transform, the split pan, and
        /// the split stochastic flag. All four facts must hold when
        /// the split sample runs and the maps move on UV0. Another
        /// main selector keeps the texture bound, so the split
        /// sample reads the bound texture and no fact matters.
        /// </summary>
        private static string MochieSplitRule(Material material)
        {
            if (!material.IsKeywordEnabled("MOCHIE_PBR"))
            {
                return null;
            }

            if (material.GetTexture("_MochieMetallicMaps") == null)
            {
                return null;
            }

            if (material.GetFloat("_PBRSplitMaskSample") == 0f)
            {
                return null;
            }

            if (material.GetFloat("_MochieMetallicMapsUV") != 0f)
            {
                return null;
            }

            var splitSelector =
                material.GetFloat("_MochieMetallicMasksUV");
            if (splitSelector != 0f)
            {
                return "The property '_MochieMetallicMasksUV' is " +
                       Number(splitSelector) + ". The route needs" +
                       " 0, because the split mask sample reads the" +
                       " same texture.";
            }

            var tiling = material.GetVector("_PBRMaskScaleTiling");
            if (tiling != IdentityScaleOffset)
            {
                return "The property '_PBRMaskScaleTiling' is " +
                       Vector(tiling) + ". The route needs (1, 1," +
                       " 0, 0), because the split mask sample reads" +
                       " the same texture.";
            }

            var pan = material.GetVector("_MochieMetallicMasksPan");
            if (pan.x != 0f || pan.y != 0f)
            {
                return "The property '_MochieMetallicMasksPan' is " +
                       Vector(pan) + ". The route needs zero," +
                       " because the split mask sample reads the" +
                       " same texture.";
            }

            var stochastic =
                material.GetFloat("_PBRSplitMaskStochastic");
            return stochastic == 0f
                ? null
                : "The property '_PBRSplitMaskStochastic' is " +
                  Number(stochastic) + ". The route needs 0," +
                  " because stochastic sampling moves the" +
                  " coordinate.";
        }

        /// <summary>
        /// The SDF shading texture never moves. The use stays on
        /// another coordinate whenever the mode keyword is on, so
        /// the texture stays bound and keyed. The selector refusal
        /// sits in the table rule.
        /// </summary>
        private static AtlasCompanionRoute SdfShadingRoute()
        {
            return new AtlasCompanionRoute(
                "_SDFShadingTexture",
                AtlasCompanionSampler.Member,
                new[]
                {
                    ("_SDFShadingTexture_ST", IdentityScaleOffset),
                    ("_SDFShadingTexturePan", ZeroVector),
                },
                new[]
                {
                    "_SDFShadingTextureUV",
                    "_SDFShadingTexture_ST",
                    "_SDFShadingTexturePan",
                },
                NeverMovedUse(Sdf));
        }

        /// <summary>
        /// The enviro rim mask carries no transform property. The
        /// sample reads the raw selected UV plus pan, so the pan
        /// must sit at zero on a UV0 use.
        /// </summary>
        private static AtlasCompanionRoute RimEnviroRoute()
        {
            return new AtlasCompanionRoute(
                "_RimEnviroMask",
                AtlasCompanionSampler.Member,
                new[] { ("_RimEnviroMaskPan", ZeroVector) },
                new[] { "_RimEnviroMaskUV", "_RimEnviroMaskPan" },
                PoiyomiCompanionRoutes.SelectorUse(
                    "_RimEnviroMaskUV", EnviroRim),
                material =>
                    PoiyomiCompanionRoutes.UnheldSelector(
                        material, "_RimEnviroMaskUV"));
        }

        /// <summary>
        /// A texture that samples raw UV0 with no selector and no
        /// transform. No route value exists, so the unheld
        /// function stays empty.
        /// </summary>
        private static AtlasCompanionRoute RawUv0Route(
            string texture,
            AtlasCompanionSampler sampler,
            string[] keywords)
        {
            return new AtlasCompanionRoute(
                texture,
                sampler,
                Array.Empty<(string, Vector4)>(),
                Array.Empty<string>(),
                SwitchedUse(keywords, AtlasRouteUse.Uv0));
        }

        /// <summary>
        /// The use of a texture that never moves. The mode keyword
        /// decides between inert and another coordinate.
        /// </summary>
        private static Func<Material, bool, AtlasRouteUse>
            NeverMovedUse(string[] keywords)
        {
            return SwitchedUse(keywords, AtlasRouteUse.OtherCoordinates);
        }

        private static Func<Material, bool, AtlasRouteUse> SwitchedUse(
            string[] keywords,
            AtlasRouteUse moved)
        {
            return (material, outlineRow) =>
                PoiyomiCompanionRoutes.AnyEnabled(material, keywords)
                    ? moved
                    : AtlasRouteUse.Inert;
        }

        private static AtlasDeadValueFamily[] BuildFamilies()
        {
            return new[]
            {
                Family("_LIGHTINGMODE_TEXTURERAMP", TextureRampMembers),
                Family(
                    "_LIGHTINGMODE_MULTILAYER_MATH",
                    MultilayerMathMembers),
                Family("_LIGHTINGMODE_SHADEMAP", ShademapMembers),
                Family("_LIGHTINGMODE_SKIN", SkinMembers),
                Family("_LIGHTINGMODE_CLOTH", ClothMembers),
                Family("_LIGHTINGMODE_SDF", SdfMembers),
                Family("POI_ANISOTROPICS", AnisotropicsMembers),
                Family("_CUBEMAP", CubemapMembers),
                Family("POI_SUBSURFACESCATTERING", SubsurfaceMembers),
                Family("MOCHIE_PBR", MochiePbrMembers),
                Family("GGX_ANISOTROPICS", GgxAnisotropicsMembers),
                Family("POI_CLEARCOAT", ClearcoatMembers),
                Family("POI_ENVIRORIM", EnviroRimMembers),
                Family(
                    "POI_STYLIZED_StylizedSpecular",
                    StylizedSpecularMembers),
                Family(
                    "_STYLIZEDREFLECTIONMODE_UNITYCHAN",
                    UnityChanMembers),
                Family(
                    "_STYLIZEDREFLECTIONMODE_LILTOON",
                    LilToonMembers),
                Family("POI_BACKLIGHT", BacklightMembers),
            };
        }

        /// <summary>
        /// One dead value family per keyword of the group. The only
        /// switch is the keyword. A NaN switch state never occurs,
        /// because the switch is a keyword.
        /// </summary>
        private static AtlasDeadValueFamily Family(
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
                "0.######", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Vector(Vector4 value)
        {
            return "(" + Number(value.x) + ", " + Number(value.y) + ", " +
                   Number(value.z) + ", " + Number(value.w) + ")";
        }

        private static Vector4 IdentityScaleOffset =>
            new Vector4(1f, 1f, 0f, 0f);

        private static Vector4 ZeroVector => new Vector4(0f, 0f, 0f, 0f);
    }
}
