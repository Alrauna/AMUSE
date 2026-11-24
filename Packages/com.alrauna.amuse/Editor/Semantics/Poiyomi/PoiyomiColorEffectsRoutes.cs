using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The feature group 'Color, effects, and UV' of the reviewed Poiyomi 9.3
    /// route. The review record of 2026-11-23, sections 4.1, 4.3, 5, and 6,
    /// carries the rows of this group.
    /// <para>
    /// The presence keywords of the Thry editor never gate a route or a
    /// family. The feature keyword gates. A texture whose feature never
    /// samples it at a mesh UV stays bound through an always bound route.
    /// The packed RGBA maps carry the split per channel sample conditions of
    /// record section 4.3. The table rules of record section 5 sit in
    /// <see cref="UnheldRequiredValue"/>. Each failure returns the first
    /// failing sentence.
    /// </para>
    /// </summary>
    internal static class PoiyomiColorEffectsRoutes
    {
        /// <summary>
        /// The feature gate of the color adjust texture.
        /// </summary>
        private static readonly string[] ColorGradingKeywords =
        {
            "COLOR_GRADING_HDR",
        };

        /// <summary>
        /// The feature gate of the distortion textures.
        /// </summary>
        private static readonly string[] UserLutKeywords =
        {
            "USER_LUT",
        };

        /// <summary>
        /// The feature gate of the detail layer textures.
        /// </summary>
        private static readonly string[] FinalPassKeywords =
        {
            "FINALPASS",
        };

        /// <summary>
        /// The feature gate of the vertex effect textures.
        /// </summary>
        private static readonly string[] AutoExposureKeywords =
        {
            "AUTO_EXPOSURE",
        };

        /// <summary>
        /// The feature gate of the look at mask.
        /// </summary>
        private static readonly string[] VertexLookAtKeywords =
        {
            "POI_VERTEX_LOOKAT",
        };

        /// <summary>
        /// The feature gate of the depth bulge mask.
        /// </summary>
        private static readonly string[] DepthBulgeKeywords =
        {
            "POI_DEPTHBULGE",
        };

        /// <summary>
        /// The feature gate of the back face textures.
        /// </summary>
        private static readonly string[] BackFaceKeywords =
        {
            "POI_BACKFACE",
        };

        /// <summary>
        /// The feature gate of the color and alpha textures. The presence
        /// keywords of the Thry editor never gate a route.
        /// </summary>
        private static readonly string[] VignetteKeywords =
        {
            "VIGNETTE",
        };

        /// <summary>
        /// The feature gate of the dissolve textures.
        /// </summary>
        private static readonly string[] DistortKeywords =
        {
            "DISTORT",
        };

        /// <summary>
        /// The feature gate of the audio link decal mask.
        /// </summary>
        private static readonly string[] AlDecalKeywords =
        {
            "POI_AL_DECAL",
        };

        /// <summary>
        /// The feature gate of the pathing textures.
        /// </summary>
        private static readonly string[] PathingKeywords =
        {
            "POI_PATHING",
        };

        /// <summary>
        /// The feature gate of the mirror texture.
        /// </summary>
        private static readonly string[] MirrorTextureKeywords =
        {
            "POI_MIRROR_TEXTURE",
        };

        /// <summary>
        /// The feature gate of the grain textures.
        /// </summary>
        private static readonly string[] GrainKeywords =
        {
            "GRAIN",
        };

        /// <summary>
        /// The feature gate of the post process mask.
        /// </summary>
        private static readonly string[] PostProcessKeywords =
        {
            "POSTPROCESS",
        };

        /// <summary>
        /// The feature gate of the video textures.
        /// </summary>
        private static readonly string[] VideoKeywords =
        {
            "POI_VIDEO_EFFECTS",
        };

        /// <summary>
        /// The feature gate of the voronoi textures.
        /// </summary>
        private static readonly string[] VoronoiKeywords =
        {
            "POI_VORONOI",
        };

        /// <summary>
        /// The feature gate of the truchet textures.
        /// </summary>
        private static readonly string[] TruchetKeywords =
        {
            "POI_TRUCHET",
        };

        /// <summary>
        /// The feature gate of the parallax textures.
        /// </summary>
        private static readonly string[] ParallaxKeywords =
        {
            "POI_PARALLAX",
        };

        /// <summary>
        /// The feature gate of the push mask.
        /// </summary>
        private static readonly string[] UzumoreKeywords =
        {
            "POI_UZUMORE",
        };

        /// <summary>
        /// The 37 allowed keywords of the group in record section 4.1 order.
        /// </summary>
        internal static readonly string[] AllowedKeywords =
        {
            "COLOR_GRADING_HDR",
            "USER_LUT",
            "POI_UDIMDISCARD",
            "POI_PARALLAX",
            "POI_AUDIOLINK",
            "FINALPASS",
            "AUTO_EXPOSURE",
            "POI_VERTEX_LOOKAT",
            "POI_DEPTHBULGE",
            "POI_BACKFACE",
            "VIGNETTE",
            "PROP_RGBMASK",
            "PROP_RGBAMETALLICMAPS",
            "PROP_RGBASMOOTHNESSMAPS",
            "PROP_REDTEXTURE",
            "PROP_RGBNORMALR",
            "PROP_GREENTEXTURE",
            "PROP_RGBNORMALG",
            "PROP_BLUETEXTURE",
            "PROP_RGBNORMALB",
            "PROP_ALPHATEXTURE",
            "PROP_RGBNORMALA",
            "DISTORT",
            "POI_AL_DECAL",
            "POI_AL_VOLUMECOLOR",
            "POI_PATHING",
            "POI_MIRROR_TEXTURE",
            "GRAIN",
            "EFFECT_BUMP",
            "POSTPROCESS",
            "PROP_PPMASK",
            "POI_VIDEO_EFFECTS",
            "_STOCHASTICMODE_DELIOT_HEITZ",
            "_STOCHASTICMODE_HEXTILE",
            "POI_VORONOI",
            "POI_TRUCHET",
            "POI_UZUMORE",
        };

        /// <summary>
        /// The record classes no neutral texture for this group.
        /// </summary>
        internal static readonly string[] NeutralTextures =
            Array.Empty<string>();

        /// <summary>
        /// Every property that a route or a table rule of this group reads.
        /// No clip and no property block may target one.
        /// </summary>
        internal static readonly string[] ConditionProperties =
        {
            "_MainColorAdjustTextureUV",
            "_MainColorAdjustTexture_ST",
            "_MainColorAdjustTexturePan",
            "_DetailMaskUV",
            "_DetailMask_ST",
            "_DetailMaskPan",
            "_DetailMaskStochastic",
            "_DetailTexUV",
            "_DetailTex_ST",
            "_DetailTexPan",
            "_DetailTexStochastic",
            "_DetailNormalMapUV",
            "_DetailNormalMap_ST",
            "_DetailNormalMapPan",
            "_DetailNormalMapStochastic",
            "_BackFaceTextureUV",
            "_BackFaceTexture_ST",
            "_BackFaceTexturePan",
            "_BackFaceMaskUV",
            "_BackFaceMask_ST",
            "_BackFaceMaskPan",
            "_RGBMaskUV",
            "_RGBMask_ST",
            "_RGBMaskPan",
            "_RGBAMetallicMapsUV",
            "_RGBAMetallicMaps_ST",
            "_RGBAMetallicMapsPan",
            "_RGBAMetallicMapsStochastic",
            "_RGBARedPBRUV",
            "_RGBARedPBRMasksPan",
            "_RGBARedPBRSplitMaskStochastic",
            "_RGBARedPBRMaskScaleTiling",
            "_RGBARedScaleOffset",
            "_RGBAGreenPBRUV",
            "_RGBAGreenPBRMasksPan",
            "_RGBAGreenPBRSplitMaskStochastic",
            "_RGBAGreenPBRMaskScaleTiling",
            "_RGBAGreenScaleOffset",
            "_RGBABluePBRUV",
            "_RGBABluePBRMasksPan",
            "_RGBABluePBRSplitMaskStochastic",
            "_RGBABluePBRMaskScaleTiling",
            "_RGBABlueScaleOffset",
            "_RGBAAlphaPBRUV",
            "_RGBAAlphaPBRMasksPan",
            "_RGBAAlphaPBRSplitMaskStochastic",
            "_RGBAAlphaPBRMaskScaleTiling",
            "_RGBAAlphaScaleOffset",
            "_RGBASmoothnessMapsUV",
            "_RGBASmoothnessMaps_ST",
            "_RGBASmoothnessMapsPan",
            "_RGBASmoothnessMapsStochastic",
            "_RedTextureUV",
            "_RedTexture_ST",
            "_RedTexturePan",
            "_RedTextureStochastic",
            "_RgbNormalRUV",
            "_RgbNormalR_ST",
            "_RgbNormalRPan",
            "_RgbNormalRStochastic",
            "_GreenTextureUV",
            "_GreenTexture_ST",
            "_GreenTexturePan",
            "_GreenTextureStochastic",
            "_RgbNormalGUV",
            "_RgbNormalG_ST",
            "_RgbNormalGPan",
            "_RgbNormalGStochastic",
            "_BlueTextureUV",
            "_BlueTexture_ST",
            "_BlueTexturePan",
            "_BlueTextureStochastic",
            "_RgbNormalBUV",
            "_RgbNormalB_ST",
            "_RgbNormalBPan",
            "_RgbNormalBStochastic",
            "_AlphaTextureUV",
            "_AlphaTexture_ST",
            "_AlphaTexturePan",
            "_AlphaTextureStochastic",
            "_RgbNormalAUV",
            "_RgbNormalA_ST",
            "_RgbNormalAPan",
            "_RgbNormalAStochastic",
            "_DepthBulgeMaskUV",
            "_DepthBulgeMask_ST",
            "_DissolveToTextureUV",
            "_DissolveToTexture_ST",
            "_DissolveToTexturePan",
            "_DissolveNoiseTextureUV",
            "_DissolveNoiseTexture_ST",
            "_DissolveNoiseTexturePan",
            "_DissolveDetailNoiseUV",
            "_DissolveDetailNoise_ST",
            "_DissolveDetailNoisePan",
            "_DissolveMaskUV",
            "_DissolveMask_ST",
            "_DissolveMaskPan",
            "_PathingMapUV",
            "_PathingMap_ST",
            "_PathingMapPan",
            "_PathingColorMapUV",
            "_PathingColorMap_ST",
            "_PathingColorMapPan",
            "_MirrorTextureUV",
            "_MirrorTexture_ST",
            "_MirrorTexturePan",
            "_DepthMaskUV",
            "_DepthMask_ST",
            "_DepthMaskPan",
            "_DepthTextureUV",
            "_DepthTexture_ST",
            "_DepthTexturePan",
            "_VideoPixelTextureUV",
            "_VideoPixelTexture_ST",
            "_VideoMaskTextureUV",
            "_VideoMaskTexture_ST",
            "_VideoMaskTexturePan",
            "_VoronoiMaskUV",
            "_VoronoiMask_ST",
            "_VoronoiMaskPan",
            "_VoronoiNoiseUV",
            "_VoronoiNoise_ST",
            "_VoronoiNoisePan",
            "_TruchetMaskUV",
            "_TruchetMask_ST",
            "_TruchetMaskPan",
            "_ALDecalColorMaskUV",
            "_ALDecalColorMask_ST",
            "_ALDecalColorMaskPan",
            "VertexBasicsMaskUV",
            "_VertexBasicsMask_ST",
            "_VertexBasicsMaskUVPan",
            "_VertexManipulationHeightMaskUV",
            "_VertexManipulationHeightMask_ST",
            "_VertexManipulationHeightMaskPan",
            "_LookAtMaskUV",
            "_LookAtMask_ST",
            "_LookAtMaskPan",
            "_UzumoreMaskUV",
            "_DistortionMaskUV",
            "_DistortionMaskPan",
            "_DistortionFlowTextureUV",
            "_DistortionFlowTexture_ST",
            "_DistortionFlowTexturePan",
            "_DistortionFlowTexture1UV",
            "_DistortionFlowTexture1_ST",
            "_DistortionFlowTexture1Pan",
            "_PPMaskUV",
            "_PPMask_ST",
            "_PPMaskPan",
            "_DistortionUvToDistort",
            "_UVTileDissolveEnabled",
            "_UVTileDissolveUV",
            "_TextFPSUV",
            "_TextPositionUV",
            "_TextTimeUV",
            "_TextNumericUV",
            "_ParallaxUV",
            "_HeightMapUV",
            "_HeightmaskUV",
            "_TruchetUV",
            "_VoronoiSpace",
            "_UDIMDiscardMode",
            "_UDIMDiscardUV",
            "_UDIMDiscardRow0_0",
            "_UDIMDiscardRow0_1",
            "_UDIMDiscardRow0_2",
            "_UDIMDiscardRow0_3",
            "_UDIMDiscardRow1_0",
            "_UDIMDiscardRow1_1",
            "_UDIMDiscardRow1_2",
            "_UDIMDiscardRow1_3",
            "_UDIMDiscardRow2_0",
            "_UDIMDiscardRow2_1",
            "_UDIMDiscardRow2_2",
            "_UDIMDiscardRow2_3",
            "_UDIMDiscardRow3_0",
            "_UDIMDiscardRow3_1",
            "_UDIMDiscardRow3_2",
            "_UDIMDiscardRow3_3",
            "_EnableUDIMFaceDiscardOptions",
            "_UDIMFaceDiscardUV",
            "_UDIMFaceDiscardRow0_0",
            "_UDIMFaceDiscardRow0_1",
            "_UDIMFaceDiscardRow0_2",
            "_UDIMFaceDiscardRow0_3",
            "_UDIMFaceDiscardRow1_0",
            "_UDIMFaceDiscardRow1_1",
            "_UDIMFaceDiscardRow1_2",
            "_UDIMFaceDiscardRow1_3",
            "_UDIMFaceDiscardRow2_0",
            "_UDIMFaceDiscardRow2_1",
            "_UDIMFaceDiscardRow2_2",
            "_UDIMFaceDiscardRow2_3",
            "_UDIMFaceDiscardRow3_0",
            "_UDIMFaceDiscardRow3_1",
            "_UDIMFaceDiscardRow3_2",
            "_UDIMFaceDiscardRow3_3",
            "_PathSource",
            "_PathingUVSelect",
            "_PathPointSampling",
            "_VideoPixelateToResolution",
            "_VideoType",
            "_AudioLinkCCStripY",
            "_DebugWaveform",
            "_DebugDFT",
            "_DebugCCStrip",
            "_DebugCCLights",
            "_DebugAutocorrelator",
            "_ALDecalUV",
            "_ALVolumeColorUV",
            "_ALVolumeColorDirection",
            "_VertexSpectrumMotion",
            "_VertexSpectrumUV",
            "_VertexSpectrumUVDirection",
            "_VertexWindEnabled",
        };
        /// <summary>
        /// The 44 companion routes of the group in record section 4.3
        /// order. The standard pattern routes use the shared builder.
        /// Every exception is written by hand.
        /// </summary>
        internal static AtlasCompanionRoute[] Routes()
        {
            return new[]
            {
                PoiyomiCompanionRoutes.Standard(
                    "_MainColorAdjustTexture",
                    AtlasCompanionSampler.Member,
                    ColorGradingKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_DetailMask",
                    AtlasCompanionSampler.Member,
                    FinalPassKeywords,
                    true),
                PoiyomiCompanionRoutes.Standard(
                    "_DetailTex",
                    AtlasCompanionSampler.Member,
                    FinalPassKeywords,
                    true),
                PoiyomiCompanionRoutes.Standard(
                    "_DetailNormalMap",
                    AtlasCompanionSampler.Member,
                    FinalPassKeywords,
                    true),
                PoiyomiCompanionRoutes.Standard(
                    "_BackFaceTexture",
                    AtlasCompanionSampler.Member,
                    BackFaceKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_BackFaceMask",
                    AtlasCompanionSampler.Member,
                    BackFaceKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_RGBMask",
                    AtlasCompanionSampler.InlineTrilinearRepeat,
                    VignetteKeywords,
                    false),
                PackedRgbMapRoute("_RGBAMetallicMaps"),
                PackedRgbMapRoute("_RGBASmoothnessMaps"),
                MergedTransformRoute("_RedTexture", "_RGBARedScaleOffset"),
                MergedTransformRoute("_RgbNormalR", "_RGBARedScaleOffset"),
                MergedTransformRoute("_GreenTexture", "_RGBAGreenScaleOffset"),
                MergedTransformRoute("_RgbNormalG", "_RGBAGreenScaleOffset"),
                MergedTransformRoute("_BlueTexture", "_RGBABlueScaleOffset"),
                MergedTransformRoute("_RgbNormalB", "_RGBABlueScaleOffset"),
                MergedTransformRoute("_AlphaTexture", "_RGBAAlphaScaleOffset"),
                MergedTransformRoute("_RgbNormalA", "_RGBAAlphaScaleOffset"),
                DepthBulgeMaskRoute(),
                PoiyomiCompanionRoutes.Standard(
                    "_DissolveToTexture",
                    AtlasCompanionSampler.Member,
                    DistortKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_DissolveNoiseTexture",
                    AtlasCompanionSampler.Member,
                    DistortKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_DissolveDetailNoise",
                    AtlasCompanionSampler.Member,
                    DistortKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_DissolveMask",
                    AtlasCompanionSampler.Member,
                    DistortKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_PathingMap",
                    AtlasCompanionSampler.InlinePointRepeat,
                    PathingKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_PathingColorMap",
                    AtlasCompanionSampler.Member,
                    PathingKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_MirrorTexture",
                    AtlasCompanionSampler.Member,
                    MirrorTextureKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_DepthMask",
                    AtlasCompanionSampler.Member,
                    GrainKeywords,
                    false),
                // The depth gradient coordinate of selector 8 is not a
                // mesh UV, so the standard selector rule admits it.
                PoiyomiCompanionRoutes.Standard(
                    "_DepthTexture",
                    AtlasCompanionSampler.Member,
                    GrainKeywords,
                    false),
                VideoPixelTextureRoute(),
                PoiyomiCompanionRoutes.Standard(
                    "_VideoMaskTexture",
                    AtlasCompanionSampler.Member,
                    VideoKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_VoronoiMask",
                    AtlasCompanionSampler.Member,
                    VoronoiKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_VoronoiNoise",
                    AtlasCompanionSampler.Member,
                    VoronoiKeywords,
                    false),
                // The truchet texture samples the pattern coordinate with
                // explicit gradients. It never moves and never joins a page.
                AlwaysBoundRoute(
                    "_TruchetTex",
                    AtlasCompanionSampler.Member,
                    TruchetKeywords),
                PoiyomiCompanionRoutes.Standard(
                    "_TruchetMask",
                    AtlasCompanionSampler.Member,
                    TruchetKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_ALDecalColorMask",
                    AtlasCompanionSampler.Member,
                    AlDecalKeywords,
                    false),
                VertexBasicsMaskRoute(),
                PoiyomiCompanionRoutes.Standard(
                    "_VertexManipulationHeightMask",
                    AtlasCompanionSampler.InlineLinearRepeat,
                    AutoExposureKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_LookAtMask",
                    AtlasCompanionSampler.InlineLinearRepeat,
                    VertexLookAtKeywords,
                    false),
                UzumoreMaskRoute(),
                DistortionMaskRoute(),
                PoiyomiCompanionRoutes.Standard(
                    "_DistortionFlowTexture",
                    AtlasCompanionSampler.Member,
                    UserLutKeywords,
                    false),
                PoiyomiCompanionRoutes.Standard(
                    "_DistortionFlowTexture1",
                    AtlasCompanionSampler.Member,
                    UserLutKeywords,
                    false),
                // The parallax marches the height at the parallax
                // coordinates. The textures never move. The table rule
                // keeps their selectors off UV0.
                AlwaysBoundRoute(
                    "_HeightMap",
                    AtlasCompanionSampler.Member,
                    ParallaxKeywords),
                AlwaysBoundRoute(
                    "_Heightmask",
                    AtlasCompanionSampler.InlineLinearRepeat,
                    ParallaxKeywords),
                PoiyomiCompanionRoutes.Standard(
                    "_PPMask",
                    AtlasCompanionSampler.Member,
                    PostProcessKeywords,
                    false),
            };
        }

        /// <summary>
        /// One packed RGBA map. The merged sample follows the standard
        /// pattern. The four split per channel samples read the same
        /// texture, so their selectors join the route as conditions at
        /// UV0, their pans and stochastic flags as zero conditions, and
        /// their merged transforms as identity demands. A split read on
        /// another mesh UV would sample the page outside its region.
        /// The channel prefixes of the packed RGBA maps sit below, in
        /// red, green, blue, alpha order.
        /// </summary>
        private static readonly string[] RgbaChannelPrefixes =
        {
            "Red",
            "Green",
            "Blue",
            "Alpha",
        };

        private static AtlasCompanionRoute PackedRgbMapRoute(string texture)
        {
            var selectorProperty = texture + "UV";
            var stochasticProperty = texture + "Stochastic";
            var use = PoiyomiCompanionRoutes.SelectorUse(
                selectorProperty, VignetteKeywords);
            var conditions = new List<string>
            {
                selectorProperty,
                texture + "_ST",
                texture + "Pan",
                stochasticProperty,
            };
            var transforms = new List<(string, Vector4)>
            {
                (texture + "_ST", IdentityScaleOffset),
                (texture + "Pan", ZeroVector),
            };
            foreach (var prefix in new[] { "Red", "Green", "Blue", "Alpha" })
            {
                var channel = "_RGBA" + prefix;
                conditions.Add(channel + "PBRUV");
                conditions.Add(channel + "PBRMasksPan");
                conditions.Add(channel + "PBRSplitMaskStochastic");
                conditions.Add(channel + "PBRMaskScaleTiling");
                conditions.Add(channel + "ScaleOffset");
                transforms.Add((channel + "PBRMaskScaleTiling", IdentityScaleOffset));
                transforms.Add((channel + "ScaleOffset", IdentityScaleOffset));
                transforms.Add((channel + "PBRMasksPan", ZeroVector));
            }

            return new AtlasCompanionRoute(
                texture,
                AtlasCompanionSampler.InlineTrilinearRepeat,
                transforms.ToArray(),
                conditions.ToArray(),
                use,
                UnheldPackedSplitValues(
                    selectorProperty, stochasticProperty, use));
        }

        /// <summary>
        /// The unheld sentences of a packed RGBA map on the UV0 use.
        /// Beside the merged selector and the merged stochastic flag,
        /// every split per channel read needs its selector on UV0 and
        /// its stochastic flag at 0, because the split read samples the
        /// same texture as the merged read. The split pans join the
        /// identity demands of the route.
        /// </summary>
        private static Func<Material, string> UnheldPackedSplitValues(
            string selectorProperty,
            string stochasticProperty,
            Func<Material, bool, AtlasRouteUse> use)
        {
            return material =>
            {
                if (use(material, false) != AtlasRouteUse.Uv0)
                {
                    return null;
                }

                var selector = PoiyomiCompanionRoutes.UnheldSelector(
                    material, selectorProperty);
                if (selector != null)
                {
                    return selector;
                }

                var mergedStochastic =
                    material.GetFloat(stochasticProperty);
                if (mergedStochastic != 0f)
                {
                    return "The property '" + stochasticProperty +
                           "' is " + Number(mergedStochastic) +
                           ". The route needs 0, because stochastic" +
                           " sampling moves the coordinate.";
                }

                foreach (var prefix in RgbaChannelPrefixes)
                {
                    var channel = "_RGBA" + prefix;
                    var splitSelector =
                        material.GetFloat(channel + "PBRUV");
                    if (splitSelector != 0f)
                    {
                        return "The property '" + channel + "PBRUV" +
                               "' is " + Number(splitSelector) +
                               ". The route needs 0, because the" +
                               " split mask sample reads the same" +
                               " texture.";
                    }

                    var splitStochastic = material.GetFloat(
                        channel + "PBRSplitMaskStochastic");
                    if (splitStochastic != 0f)
                    {
                        return "The property '" + channel +
                               "PBRSplitMaskStochastic' is " +
                               Number(splitStochastic) +
                               ". The route needs 0, because" +
                               " stochastic sampling moves the" +
                               " coordinate of the split mask" +
                               " sample.";
                    }
                }

                return null;
            };
        }

        /// <summary>
        /// One channel texture or channel normal map of the color and
        /// alpha feature. The sample merges the texture transform with
        /// the scale and offset of its channel, so the merged transform
        /// joins the identity demands beside the standard pattern.
        /// </summary>
        private static AtlasCompanionRoute MergedTransformRoute(
            string texture, string mergedTransform)
        {
            var selectorProperty = texture + "UV";
            var stochasticProperty = texture + "Stochastic";
            var use = PoiyomiCompanionRoutes.SelectorUse(
                selectorProperty, VignetteKeywords);
            return new AtlasCompanionRoute(
                texture,
                AtlasCompanionSampler.InlineTrilinearRepeat,
                new[]
                {
                    (texture + "_ST", IdentityScaleOffset),
                    (texture + "Pan", ZeroVector),
                    (mergedTransform, IdentityScaleOffset),
                },
                new[]
                {
                    selectorProperty,
                    texture + "_ST",
                    texture + "Pan",
                    stochasticProperty,
                    mergedTransform,
                },
                use,
                UnheldUv0SelectorAndStochastic(
                    selectorProperty, stochasticProperty, use));
        }

        /// <summary>
        /// The vertex basics mask. The selector property has no leading
        /// underscore and the pan property carries the UV infix, so the
        /// standard builder cannot serve it. The pan advances with time.
        /// </summary>
        private static AtlasCompanionRoute VertexBasicsMaskRoute()
        {
            const string selectorProperty = "VertexBasicsMaskUV";
            var use = PoiyomiCompanionRoutes.SelectorUse(
                selectorProperty, AutoExposureKeywords);
            return new AtlasCompanionRoute(
                "_VertexBasicsMask",
                AtlasCompanionSampler.InlineLinearRepeat,
                new[]
                {
                    ("_VertexBasicsMask_ST", IdentityScaleOffset),
                    ("_VertexBasicsMaskUVPan", ZeroVector),
                },
                new[]
                {
                    selectorProperty,
                    "_VertexBasicsMask_ST",
                    "_VertexBasicsMaskUVPan",
                },
                use,
                UnheldUv0SelectorAndStochastic(selectorProperty, null, use));
        }

        /// <summary>
        /// The depth bulge mask. No pan property exists. The vertex
        /// stage sample reads the selected mesh UV at LOD 0, so the
        /// identity transform serves a page.
        /// </summary>
        private static AtlasCompanionRoute DepthBulgeMaskRoute()
        {
            const string selectorProperty = "_DepthBulgeMaskUV";
            var use = PoiyomiCompanionRoutes.SelectorUse(
                selectorProperty, DepthBulgeKeywords);
            return new AtlasCompanionRoute(
                "_DepthBulgeMask",
                AtlasCompanionSampler.InlineLinearRepeat,
                new[] { ("_DepthBulgeMask_ST", IdentityScaleOffset) },
                new[] { selectorProperty, "_DepthBulgeMask_ST" },
                use,
                UnheldUv0SelectorAndStochastic(selectorProperty, null, use));
        }

        /// <summary>
        /// The distortion mask. No transform property exists. The sample
        /// reads the raw selected mesh UV plus the pan, so the route
        /// demands the zero pan beside the selector.
        /// </summary>
        private static AtlasCompanionRoute DistortionMaskRoute()
        {
            const string selectorProperty = "_DistortionMaskUV";
            var use = PoiyomiCompanionRoutes.SelectorUse(
                selectorProperty, UserLutKeywords);
            return new AtlasCompanionRoute(
                "_DistortionMask",
                AtlasCompanionSampler.Member,
                new[] { ("_DistortionMaskPan", ZeroVector) },
                new[] { selectorProperty, "_DistortionMaskPan" },
                use,
                UnheldUv0SelectorAndStochastic(selectorProperty, null, use));
        }

        /// <summary>
        /// The video pixel texture. The sample scales the coordinate by
        /// the video resolution, so a page can never serve it. No pan
        /// property exists. A selector of 0 refuses through the route.
        /// </summary>
        private static AtlasCompanionRoute VideoPixelTextureRoute()
        {
            const string selectorProperty = "_VideoPixelTextureUV";
            var use = PoiyomiCompanionRoutes.SelectorUse(
                selectorProperty, VideoKeywords);
            return new AtlasCompanionRoute(
                "_VideoPixelTexture",
                AtlasCompanionSampler.Own,
                new[] { ("_VideoPixelTexture_ST", IdentityScaleOffset) },
                new[] { selectorProperty, "_VideoPixelTexture_ST" },
                use,
                UnheldVideoPixelSelector(selectorProperty, use));
        }

        /// <summary>
        /// The push mask. The clamp inline sampler cannot serve a page,
        /// so the route refuses the UV0 use. The property doubles as the
        /// channel index. No transform or pan exists.
        /// </summary>
        private static AtlasCompanionRoute UzumoreMaskRoute()
        {
            const string selectorProperty = "_UzumoreMaskUV";
            var use = PoiyomiCompanionRoutes.SelectorUse(
                selectorProperty, UzumoreKeywords);
            return new AtlasCompanionRoute(
                "_UzumoreMask",
                AtlasCompanionSampler.InlineClamp,
                Array.Empty<(string, Vector4)>(),
                new[] { selectorProperty },
                use,
                material =>
                {
                    if (use(material, false) != AtlasRouteUse.Uv0)
                    {
                        return null;
                    }

                    return "The texture '_UzumoreMask' samples with a" +
                           " clamp sampler on UV0. The route needs a" +
                           " selector other than 0.";
                });
        }

        /// <summary>
        /// One texture that no page can ever serve. The texture stays
        /// bound and keyed while its feature keyword is on. Its own
        /// transform and pan properties stay live, because no read of
        /// them can move the render.
        /// </summary>
        private static AtlasCompanionRoute AlwaysBoundRoute(
            string texture,
            AtlasCompanionSampler sampler,
            string[] keywords)
        {
            return new AtlasCompanionRoute(
                texture,
                sampler,
                Array.Empty<(string, Vector4)>(),
                Array.Empty<string>(),
                (material, outlineRow) =>
                    PoiyomiCompanionRoutes.AnyEnabled(material, keywords)
                        ? AtlasRouteUse.OtherCoordinates
                        : AtlasRouteUse.Inert);
        }

        /// <summary>
        /// The unheld sentences of a standard UV0 use: the selector and
        /// the stochastic flag. A null stochastic property skips that
        /// check.
        /// </summary>
        private static Func<Material, string> UnheldUv0SelectorAndStochastic(
            string selectorProperty,
            string stochasticProperty,
            Func<Material, bool, AtlasRouteUse> use)
        {
            return material =>
            {
                if (use(material, false) != AtlasRouteUse.Uv0)
                {
                    return null;
                }

                var selector = PoiyomiCompanionRoutes.UnheldSelector(
                    material, selectorProperty);
                if (selector != null)
                {
                    return selector;
                }

                if (stochasticProperty == null)
                {
                    return null;
                }

                var stochastic = material.GetFloat(stochasticProperty);
                return stochastic == 0f
                    ? null
                    : "The property '" + stochasticProperty +
                      "' is " + Number(stochastic) +
                      ". The route needs 0, because stochastic" +
                      " sampling moves the coordinate.";
            };
        }

        /// <summary>
        /// The unheld sentence of the video pixel texture. Every UV0 use
        /// refuses, because the sample scales the coordinate by the
        /// video resolution.
        /// </summary>
        private static Func<Material, string> UnheldVideoPixelSelector(
            string selectorProperty,
            Func<Material, bool, AtlasRouteUse> use)
        {
            return material =>
            {
                if (use(material, false) != AtlasRouteUse.Uv0)
                {
                    return null;
                }

                var selector = material.GetFloat(selectorProperty);
                return "The property '" + selectorProperty +
                       "' is " + Number(selector) +
                       ". The route needs another coordinate, because" +
                       " the sample scales the coordinate by the video" +
                       " resolution.";
            };
        }

        // --- the table rules ------------------------------------------

        /// <summary>
        /// The table rules of record section 5 for this group. Each rule
        /// fires only while its feature keyword is on. The first failing
        /// sentence wins. A NaN flag fails closed where the shader would
        /// read UV0, and passes where the shader compares with not equal
        /// to zero.
        /// </summary>
        internal static string UnheldRequiredValue(Material material)
        {
            return UnheldDistortionTarget(material)
                ?? UnheldDissolveUvTile(material)
                ?? UnheldTextSelectors(material)
                ?? UnheldParallaxChannels(material)
                ?? UnheldTruchetChannel(material)
                ?? UnheldVoronoiSpace(material)
                ?? UnheldUdimDiscardGrids(material)
                ?? UnheldPathingSource(material)
                ?? UnheldPathingPointSampling(material)
                ?? UnheldUzumoreChannel(material)
                ?? UnheldVideoModes(material)
                ?? UnheldAudioLinkUvReads(material)
                ?? UnheldAlDecalChannel(material)
                ?? UnheldAlVolumeColorChannel(material)
                ?? UnheldVertexSpectrum(material)
                ?? UnheldVertexWind(material);
        }

        /// <summary>
        /// The distortion target writes the coordinate slot 7. Slot 7 is
        /// UV0 data, so the target must sit on another coordinate.
        /// </summary>
        private static string UnheldDistortionTarget(Material material)
        {
            if (!material.IsKeywordEnabled("USER_LUT"))
            {
                return null;
            }

            var target = material.GetFloat("_DistortionUvToDistort");
            if (target == 1f || target == 2f || target == 3f)
            {
                return null;
            }

            return "The property '_DistortionUvToDistort' is " +
                   Number(target) +
                   ". The route needs 1, 2, or 3, because the" +
                   " distortion writes the coordinate slot 7.";
        }

        /// <summary>
        /// The UV tile dissolve builds its coordinate from the selected
        /// mesh UV. The tile grid on UV0 reads UV0 as data.
        /// </summary>
        private static string UnheldDissolveUvTile(Material material)
        {
            if (!material.IsKeywordEnabled("DISTORT"))
            {
                return null;
            }

            if (material.GetFloat("_UVTileDissolveEnabled") == 0f)
            {
                return null;
            }

            var selector = material.GetFloat("_UVTileDissolveUV");
            if (selector == 1f || selector == 2f || selector == 3f)
            {
                return null;
            }

            return "The property '_UVTileDissolveUV' is " +
                   Number(selector) +
                   ". The route needs another coordinate, or the tile" +
                   " dissolve off.";
        }

        /// <summary>
        /// Every text overlay reads its selected mesh UV as data. The
        /// first selector that sits on UV0 names the refusal.
        /// </summary>
        private static string UnheldTextSelectors(Material material)
        {
            if (!material.IsKeywordEnabled("EFFECT_BUMP"))
            {
                return null;
            }

            foreach (var selector in TextSelectorProperties)
            {
                if (material.GetFloat(selector) == 0f)
                {
                    return "The property '" + selector + "' is 0." +
                           " The route needs another coordinate," +
                           " because the text overlay reads the mesh" +
                           " UV as data.";
                }
            }

            return null;
        }

        /// <summary>
        /// The parallax marches the height at the selected coordinates
        /// with UV0 gradients. Every channel must sit on another
        /// coordinate. The defaults refuse.
        /// </summary>
        private static string UnheldParallaxChannels(Material material)
        {
            if (!material.IsKeywordEnabled("POI_PARALLAX"))
            {
                return null;
            }

            foreach (var channel in ParallaxChannelProperties)
            {
                if (material.GetFloat(channel) == 0f)
                {
                    return "The property '" + channel + "' is 0." +
                           " The route needs another coordinate," +
                           " because the parallax marches the height" +
                           " with UV0 gradients.";
                }
            }

            return null;
        }

        /// <summary>
        /// The truchet pattern builds its coordinate from the selected
        /// mesh UV. The default channel reads UV0 as data.
        /// </summary>
        private static string UnheldTruchetChannel(Material material)
        {
            if (!material.IsKeywordEnabled("POI_TRUCHET"))
            {
                return null;
            }

            var channel = material.GetFloat("_TruchetUV");
            if (channel != 0f)
            {
                return null;
            }

            return "The property '_TruchetUV' is 0. The route needs" +
                   " another coordinate, because the pattern reads the" +
                   " mesh UV as data.";
        }

        /// <summary>
        /// The voronoi space value 2 reads the mesh UV as data.
        /// </summary>
        private static string UnheldVoronoiSpace(Material material)
        {
            if (!material.IsKeywordEnabled("POI_VORONOI"))
            {
                return null;
            }

            var space = material.GetFloat("_VoronoiSpace");
            if (space != 2f)
            {
                return null;
            }

            return "The property '_VoronoiSpace' is 2. The route" +
                   " needs 0 or 1, because the UV space reads the mesh" +
                   " UV as data.";
        }

        /// <summary>
        /// Each UV tile discard grid discards the mesh UV tiles its row
        /// flags select. The tile grid and the face discard grid each
        /// refuse on UV0 when one of their 16 row flags is set. The face
        /// grid reads run only while the face options sit enabled.
        /// </summary>
        private static string UnheldUdimDiscardGrids(Material material)
        {
            if (!material.IsKeywordEnabled("POI_UDIMDISCARD"))
            {
                return null;
            }

            if (material.GetFloat("_UDIMDiscardUV") == 0f)
            {
                foreach (var flag in UdimDiscardRowFlags)
                {
                    if (material.GetFloat(flag) != 0f)
                    {
                        return "The property '" + flag + "' is " +
                               Number(material.GetFloat(flag)) +
                               ". The route needs all row flags 0, or" +
                               " the selector another coordinate.";
                    }
                }
            }

            if (material.GetFloat("_EnableUDIMFaceDiscardOptions") == 0f)
            {
                return null;
            }

            if (material.GetFloat("_UDIMFaceDiscardUV") != 0f)
            {
                return null;
            }

            foreach (var flag in UdimFaceDiscardRowFlags)
            {
                if (material.GetFloat(flag) != 0f)
                {
                    return "The property '" + flag + "' is " +
                           Number(material.GetFloat(flag)) +
                           ". The route needs all row flags 0, or the" +
                           " selector another coordinate.";
                }
            }

            return null;
        }

        /// <summary>
        /// The UV gradient path source reads the selected mesh UV as
        /// data. Source 1 on UV0 refuses.
        /// </summary>
        private static string UnheldPathingSource(Material material)
        {
            if (!material.IsKeywordEnabled("POI_PATHING"))
            {
                return null;
            }

            if (material.GetFloat("_PathSource") != 1f)
            {
                return null;
            }

            var selector = material.GetFloat("_PathingUVSelect");
            if (selector != 0f)
            {
                return null;
            }

            return "The property '_PathSource' is 1. The route needs" +
                   " the tex gradient source, or the selector" +
                   " another coordinate.";
        }

        /// <summary>
        /// The point sampling flag selects the sampler of the pathing
        /// map. The recorded sampler facts are the point repeat ones, so
        /// an assigned map on UV0 needs the flag at 1.
        /// </summary>
        private static string UnheldPathingPointSampling(Material material)
        {
            if (!material.IsKeywordEnabled("POI_PATHING"))
            {
                return null;
            }

            if (material.GetTexture("_PathingMap") == null)
            {
                return null;
            }

            if (material.GetFloat("_PathingMapUV") != 0f)
            {
                return null;
            }

            var sampling = material.GetFloat("_PathPointSampling");
            if (sampling == 1f)
            {
                return null;
            }

            return "The property '_PathPointSampling' is " +
                   Number(sampling) +
                   ". The route needs 1, because the recorded sampler" +
                   " facts are the point repeat ones.";
        }

        /// <summary>
        /// The push mask channel doubles as the coordinate selector. The
        /// clamp sampler cannot serve UV0.
        /// </summary>
        private static string UnheldUzumoreChannel(Material material)
        {
            if (!material.IsKeywordEnabled("POI_UZUMORE"))
            {
                return null;
            }

            var channel = material.GetFloat("_UzumoreMaskUV");
            if (channel != 0f)
            {
                return null;
            }

            return "The property '_UzumoreMaskUV' is 0. The route" +
                   " needs another channel, because the clamp sampler" +
                   " cannot serve UV0.";
        }

        /// <summary>
        /// The video effects read UV0 in three modes. The pixelate
        /// resample and the CRT screen read the mesh UV as data. The
        /// pixel texture scales the coordinate by the video resolution,
        /// so it never moves.
        /// </summary>
        private static string UnheldVideoModes(Material material)
        {
            if (!material.IsKeywordEnabled("POI_VIDEO_EFFECTS"))
            {
                return null;
            }

            var pixelate = material.GetFloat("_VideoPixelateToResolution");
            if (pixelate != 0f)
            {
                return "The property '_VideoPixelateToResolution' is " +
                       Number(pixelate) +
                       ". The route needs 0, because the pixelate" +
                       " resample reads UV0.";
            }

            if (material.GetFloat("_VideoType") == 2f)
            {
                return "The property '_VideoType' is 2. The route" +
                       " needs another screen type, because the CRT" +
                       " mode reads UV0.";
            }

            if (material.GetTexture("_VideoPixelTexture") == null)
            {
                return null;
            }

            var selector = material.GetFloat("_VideoPixelTextureUV");
            if (selector != 0f)
            {
                return null;
            }

            return "The property '_VideoPixelTextureUV' is 0. The" +
                   " route needs another coordinate, because the pixel" +
                   " texture scales the coordinate by the video" +
                   " resolution.";
        }

        /// <summary>
        /// Every audio link UV debug display reads the mesh UV as data.
        /// The flags are uniforms without Properties entries. A missing
        /// name reads 0, which is the safe state.
        /// </summary>
        private static string UnheldAudioLinkUvReads(Material material)
        {
            if (!material.IsKeywordEnabled("POI_AUDIOLINK"))
            {
                return null;
            }

            foreach (var flag in AudioUvFlagProperties)
            {
                // A saved name reads through GetFloat or GetInteger.
                // A name the row does not carry reads 0, which is the
                // safe state. HasFloat and HasInteger stay silent;
                // a bare GetFloat would log an error for a missing
                // name.
                var value = material.HasFloat(flag)
                    ? material.GetFloat(flag)
                    : material.HasInteger(flag)
                        ? material.GetInteger(flag)
                        : 0f;
                if (value != 0f)
                {
                    return "The property '" + flag + "' is " +
                           Number(value) +
                           ". The route needs 0, because the audio UV" +
                           " debug reads the mesh UV as data.";
                }
            }

            return null;
        }

        /// <summary>
        /// The audio link decal reads the selected mesh UV as data. The
        /// default channel refuses.
        /// </summary>
        private static string UnheldAlDecalChannel(Material material)
        {
            if (!material.IsKeywordEnabled("POI_AL_DECAL"))
            {
                return null;
            }

            var channel = material.GetFloat("_ALDecalUV");
            if (channel != 0f)
            {
                return null;
            }

            return "The property '_ALDecalUV' is 0. The route needs" +
                   " another coordinate, because the decal reads the" +
                   " mesh UV as data.";
        }

        /// <summary>
        /// The audio link volume color reads the selected mesh UV as
        /// data. Either selector at 0 refuses.
        /// </summary>
        private static string UnheldAlVolumeColorChannel(
            Material material)
        {
            if (!material.IsKeywordEnabled("POI_AL_VOLUMECOLOR"))
            {
                return null;
            }

            foreach (var selector in AlVolumeColorSelectors)
            {
                if (material.GetFloat(selector) == 0f)
                {
                    return "The property '" + selector + "' is 0." +
                           " The route needs another coordinate," +
                           " because the volume color reads the mesh" +
                           " UV as data.";
                }
            }

            return null;
        }

        /// <summary>
        /// The vertex spectrum motion reads the selected mesh UV as
        /// data. Motion on with the selector on UV0 refuses.
        /// </summary>
        private static string UnheldVertexSpectrum(Material material)
        {
            if (!material.IsKeywordEnabled("AUTO_EXPOSURE"))
            {
                return null;
            }

            if (material.GetFloat("_VertexSpectrumMotion") == 0f)
            {
                return null;
            }

            var selector = material.GetFloat("_VertexSpectrumUV");
            if (selector != 0f)
            {
                return null;
            }

            return "The property '_VertexSpectrumUV' is 0. The route" +
                   " needs another coordinate, because the spectrum" +
                   " reads the mesh UV as data.";
        }

        /// <summary>
        /// The vertex wind reads world coordinates. It must stay off
        /// while the vertex basics mask is a companion page, because the
        /// wind branch reads world coordinates.
        /// </summary>
        private static string UnheldVertexWind(Material material)
        {
            if (!material.IsKeywordEnabled("AUTO_EXPOSURE"))
            {
                return null;
            }

            if (material.GetTexture("_VertexBasicsMask") == null)
            {
                return null;
            }

            if (material.GetFloat("VertexBasicsMaskUV") != 0f)
            {
                return null;
            }

            var wind = material.GetFloat("_VertexWindEnabled");
            if (wind == 0f)
            {
                return null;
            }

            return "The property '_VertexWindEnabled' is " +
                   Number(wind) +
                   ". The route needs 0, because the wind reads world" +
                   " coordinates.";
        }


        // --- the dead value families ----------------------------------

        /// <summary>
        /// One dead value family per keyword of the group. The only
        /// switch is the keyword. The members are the exact property
        /// and uniform names of record section 6. Every read of every
        /// member sits inside a compile block that requires the
        /// keyword. When two keywords gate one read, the member sits in
        /// both families.
        /// </summary>
        internal static AtlasDeadValueFamily[] Families()
        {
            return new[]
            {
                KeywordFamily("COLOR_GRADING_HDR", ColorGradingHdrMembers),
                KeywordFamily("USER_LUT", UserLutMembers),
                KeywordFamily("POI_UDIMDISCARD", UdimDiscardMembers),
                KeywordFamily("POI_PARALLAX", ParallaxMembers),
                KeywordFamily("POI_AUDIOLINK", AudioLinkMembers),
                KeywordFamily("FINALPASS", FinalPassMembers),
                KeywordFamily("AUTO_EXPOSURE", AutoExposureMembers),
                KeywordFamily("POI_VERTEX_LOOKAT", VertexLookAtMembers),
                KeywordFamily("POI_DEPTHBULGE", DepthBulgeMembers),
                KeywordFamily("POI_BACKFACE", BackFaceMembers),
                KeywordFamily("VIGNETTE", VignetteMembers),
                KeywordFamily("PROP_RGBMASK", RgbMaskMembers),
                KeywordFamily("PROP_RGBAMETALLICMAPS", RgbaMetallicMembers),
                KeywordFamily("PROP_RGBASMOOTHNESSMAPS", RgbaSmoothnessMembers),
                KeywordFamily("PROP_REDTEXTURE", RedTextureMembers),
                KeywordFamily("PROP_RGBNORMALR", RgbNormalRMembers),
                KeywordFamily("PROP_GREENTEXTURE", GreenTextureMembers),
                KeywordFamily("PROP_RGBNORMALG", RgbNormalGMembers),
                KeywordFamily("PROP_BLUETEXTURE", BlueTextureMembers),
                KeywordFamily("PROP_RGBNORMALB", RgbNormalBMembers),
                KeywordFamily("PROP_ALPHATEXTURE", AlphaTextureMembers),
                KeywordFamily("PROP_RGBNORMALA", RgbNormalAMembers),
                KeywordFamily("DISTORT", DistortMembers),
                KeywordFamily("POI_AL_DECAL", AlDecalMembers),
                KeywordFamily("POI_AL_VOLUMECOLOR", AlVolumeColorMembers),
                KeywordFamily("POI_PATHING", PathingMembers),
                KeywordFamily("POI_MIRROR_TEXTURE", MirrorTextureMembers),
                KeywordFamily("GRAIN", GrainMembers),
                KeywordFamily("EFFECT_BUMP", EffectBumpMembers),
                KeywordFamily("POSTPROCESS", PostProcessMembers),
                KeywordFamily("PROP_PPMASK", PpMaskMembers),
                KeywordFamily("POI_VIDEO_EFFECTS", VideoMembers),
                KeywordFamily("_STOCHASTICMODE_DELIOT_HEITZ", StochasticDeliotMembers),
                KeywordFamily("_STOCHASTICMODE_HEXTILE", StochasticHextileMembers),
                KeywordFamily("POI_VORONOI", VoronoiMembers),
                KeywordFamily("POI_TRUCHET", TruchetMembers),
                KeywordFamily("POI_UZUMORE", UzumoreMembers),
            };
        }

        /// <summary>
        /// One keyword gated family. The family is dead when the
        /// material does not enable the keyword.
        /// </summary>
        private static AtlasDeadValueFamily KeywordFamily(
            string keyword, string[] members)
        {
            return new AtlasDeadValueFamily(
                keyword,
                new[] { keyword },
                (material, rowName) => !material.IsKeywordEnabled(keyword),
                members);
        }

        /// <summary>
        /// The hue shift, gamma, brightness, gradation, and saturation values.
        /// </summary>
        private static readonly string[] ColorGradingHdrMembers =
        {
            "_MainHueALCTEnabled",
            "_MainHueALMotionSpeed",
            "_MainHueGlobalMask",
            "_MainHueGlobalMaskBlendType",
            "_MainHueShift",
            "_MainHueShiftColorSpace",
            "_MainHueShiftReplace",
            "_MainHueShiftSelectOrShift",
            "_MainHueShiftSpeed",
            "_MainHueShiftToggle",
            "_MainColorAdjustTexture",
            "_MainColorAdjustTexturePan",
            "_MainColorAdjustTextureUV",
            "_MainColorAdjustTexture_ST",
            "_MainGamma",
            "_MainGammaGlobalMask",
            "_MainGammaGlobalMaskBlendType",
            "_MainBrightness",
            "_MainBrightnessGlobalMask",
            "_MainBrightnessGlobalMaskBlendType",
            "_MainALHueShiftBand",
            "_MainALHueShiftCTIndex",
            "_MainGradationStrength",
            "_MainGradationTex",
            "_MainSaturationGlobalMask",
            "_MainSaturationGlobalMaskBlendType",
            "_Saturation",
            "_ColorGradingToggle",
        };

        /// <summary>
        /// The distortion flow, strength, mask, and target values.
        /// </summary>
        private static readonly string[] UserLutMembers =
        {
            "_DistortionFlowTexture",
            "_DistortionFlowTexture1",
            "_DistortionFlowTexture1Pan",
            "_DistortionFlowTexture1UV",
            "_DistortionFlowTexture1_ST",
            "_DistortionFlowTexturePan",
            "_DistortionFlowTextureUV",
            "_DistortionFlowTexture_ST",
            "_DistortionStrength",
            "_DistortionStrength1",
            "_DistortionStrength1AudioLink",
            "_DistortionStrength1AudioLinkBand",
            "_DistortionStrengthAudioLink",
            "_DistortionStrengthAudioLinkBand",
            "_DistortionMaskUV",
            "_DistortionMaskPan",
            "_DistortionMaskChannel",
            "_DistortionUvToDistort",
            "_EnableDistortionAudioLink",
            "_DistortionMask",
        };

        /// <summary>
        /// The UV tile discard grids and the face discard grid.
        /// </summary>
        private static readonly string[] UdimDiscardMembers =
        {
            "_UDIMFaceDiscardFace",
            "_UDIMFaceDiscardRow0_0",
            "_UDIMFaceDiscardRow0_1",
            "_UDIMFaceDiscardRow0_2",
            "_UDIMFaceDiscardRow0_3",
            "_UDIMFaceDiscardRow1_0",
            "_UDIMFaceDiscardRow1_1",
            "_UDIMFaceDiscardRow1_2",
            "_UDIMFaceDiscardRow1_3",
            "_UDIMFaceDiscardRow2_0",
            "_UDIMFaceDiscardRow2_1",
            "_UDIMFaceDiscardRow2_2",
            "_UDIMFaceDiscardRow2_3",
            "_UDIMFaceDiscardRow3_0",
            "_UDIMFaceDiscardRow3_1",
            "_UDIMFaceDiscardRow3_2",
            "_UDIMFaceDiscardRow3_3",
            "_UDIMFaceDiscardUV",
            "_UDIMDiscardRow0_0",
            "_UDIMDiscardRow0_1",
            "_UDIMDiscardRow0_2",
            "_UDIMDiscardRow0_3",
            "_UDIMDiscardRow1_0",
            "_UDIMDiscardRow1_1",
            "_UDIMDiscardRow1_2",
            "_UDIMDiscardRow1_3",
            "_UDIMDiscardRow2_0",
            "_UDIMDiscardRow2_1",
            "_UDIMDiscardRow2_2",
            "_UDIMDiscardRow2_3",
            "_UDIMDiscardRow3_0",
            "_UDIMDiscardRow3_1",
            "_UDIMDiscardRow3_2",
            "_UDIMDiscardRow3_3",
            "_UDIMDiscardUV",
            "_UDIMDiscardMode",
            "_EnableUDIMFaceDiscardOptions",
        };

        /// <summary>
        /// The parallax height, curve, and coordinate values.
        /// </summary>
        private static readonly string[] ParallaxMembers =
        {
            "_HeightMap",
            "_HeightMapPan",
            "_HeightMapUV",
            "_HeightMap_ST",
            "_HeightOffset",
            "_HeightStepsMax",
            "_HeightStepsMin",
            "_HeightStrength",
            "_Heightmask",
            "_HeightmaskChannel",
            "_HeightmaskInvert",
            "_HeightmaskPan",
            "_HeightmaskUV",
            "_Heightmask_ST",
            "_CurvFix",
            "_CurvatureU",
            "_CurvatureV",
            "_ParallaxUV",
            "_ParallaxMipBias",
            "_ParallaxConeRatio",
            "_ParallaxRelaxedCone",
            "_ParallaxAnisotropic",
            "_ParallaxBinarySteps",
            "_ParallaxAnisotropyScale",
        };

        /// <summary>
        /// The audio link feature values, including the UV debug flags.
        /// </summary>
        private static readonly string[] AudioLinkMembers =
        {
            "_AudioLinkDecal0Alpha",
            "_AudioLinkDecal0AlphaBand",
            "_AudioLinkDecal0ChannelSeparation",
            "_AudioLinkDecal0ChannelSeparationBand",
            "_AudioLinkDecal0Emission",
            "_AudioLinkDecal0EmissionBand",
            "_AudioLinkDecal0Rotation",
            "_AudioLinkDecal0RotationBand",
            "_AudioLinkDecal0Scale",
            "_AudioLinkDecal0ScaleBand",
            "_AudioLinkDecal0SideBand",
            "_AudioLinkDecal0SideMax",
            "_AudioLinkDecal0SideMin",
            "_AudioLinkDecal1Alpha",
            "_AudioLinkDecal1AlphaBand",
            "_AudioLinkDecal1ChannelSeparation",
            "_AudioLinkDecal1ChannelSeparationBand",
            "_AudioLinkDecal1Emission",
            "_AudioLinkDecal1EmissionBand",
            "_AudioLinkDecal1Rotation",
            "_AudioLinkDecal1RotationBand",
            "_AudioLinkDecal1Scale",
            "_AudioLinkDecal1ScaleBand",
            "_AudioLinkDecal1SideBand",
            "_AudioLinkDecal1SideMax",
            "_AudioLinkDecal1SideMin",
            "_AudioLinkDecal2Alpha",
            "_AudioLinkDecal2AlphaBand",
            "_AudioLinkDecal2ChannelSeparation",
            "_AudioLinkDecal2ChannelSeparationBand",
            "_AudioLinkDecal2Emission",
            "_AudioLinkDecal2EmissionBand",
            "_AudioLinkDecal2Rotation",
            "_AudioLinkDecal2RotationBand",
            "_AudioLinkDecal2Scale",
            "_AudioLinkDecal2ScaleBand",
            "_AudioLinkDecal2SideBand",
            "_AudioLinkDecal2SideMax",
            "_AudioLinkDecal2SideMin",
            "_AudioLinkDecal3Alpha",
            "_AudioLinkDecal3AlphaBand",
            "_AudioLinkDecal3ChannelSeparation",
            "_AudioLinkDecal3ChannelSeparationBand",
            "_AudioLinkDecal3Emission",
            "_AudioLinkDecal3EmissionBand",
            "_AudioLinkDecal3Rotation",
            "_AudioLinkDecal3RotationBand",
            "_AudioLinkDecal3Scale",
            "_AudioLinkDecal3ScaleBand",
            "_AudioLinkDecal3SideBand",
            "_AudioLinkDecal3SideMax",
            "_AudioLinkDecal3SideMin",
            "_AudioLinkDecalCC0",
            "_AudioLinkDecalCC1",
            "_AudioLinkDecalCC2",
            "_AudioLinkDecalCC3",
            "_PathALAutoCorrelator",
            "_PathALAutoCorrelatorA",
            "_PathALAutoCorrelatorB",
            "_PathALAutoCorrelatorG",
            "_PathALAutoCorrelatorMode",
            "_PathALAutoCorrelatorR",
            "_PathALAutoCorrelatorRangeA",
            "_PathALAutoCorrelatorRangeB",
            "_PathALAutoCorrelatorRangeG",
            "_PathALAutoCorrelatorRangeR",
            "_PathALCCA",
            "_PathALCCB",
            "_PathALCCG",
            "_PathALCCR",
            "_PathALChrono",
            "_PathALColorChord",
            "_PathALEmissionOffset",
            "_PathALHistory",
            "_PathALHistoryA",
            "_PathALHistoryB",
            "_PathALHistoryBandA",
            "_PathALHistoryBandB",
            "_PathALHistoryBandG",
            "_PathALHistoryBandR",
            "_PathALHistoryG",
            "_PathALHistoryMode",
            "_PathALHistoryR",
            "_PathALHistoryRangeA",
            "_PathALHistoryRangeB",
            "_PathALHistoryRangeG",
            "_PathALHistoryRangeR",
            "_PathALTimeOffset",
            "_PathALWidthOffset",
            "_AudioLinkPathEmissionAddA",
            "_AudioLinkPathEmissionAddB",
            "_AudioLinkPathEmissionAddBandA",
            "_AudioLinkPathEmissionAddBandB",
            "_AudioLinkPathEmissionAddBandG",
            "_AudioLinkPathEmissionAddBandR",
            "_AudioLinkPathEmissionAddG",
            "_AudioLinkPathEmissionAddR",
            "_AudioLinkPathTimeOffsetA",
            "_AudioLinkPathTimeOffsetB",
            "_AudioLinkPathTimeOffsetBandA",
            "_AudioLinkPathTimeOffsetBandB",
            "_AudioLinkPathTimeOffsetBandG",
            "_AudioLinkPathTimeOffsetBandR",
            "_AudioLinkPathTimeOffsetG",
            "_AudioLinkPathTimeOffsetR",
            "_AudioLinkPathWidthOffsetA",
            "_AudioLinkPathWidthOffsetB",
            "_AudioLinkPathWidthOffsetBandA",
            "_AudioLinkPathWidthOffsetBandB",
            "_AudioLinkPathWidthOffsetBandG",
            "_AudioLinkPathWidthOffsetBandR",
            "_AudioLinkPathWidthOffsetG",
            "_AudioLinkPathWidthOffsetR",
            "_AudioLinkVoronoiChronoSpeedXBand",
            "_AudioLinkVoronoiChronoSpeedXSpeed",
            "_AudioLinkVoronoiChronoSpeedXType",
            "_AudioLinkVoronoiChronoSpeedYBand",
            "_AudioLinkVoronoiChronoSpeedYSpeed",
            "_AudioLinkVoronoiChronoSpeedYType",
            "_AudioLinkVoronoiChronoSpeedZBand",
            "_AudioLinkVoronoiChronoSpeedZSpeed",
            "_AudioLinkVoronoiChronoSpeedZType",
            "_AudioLinkVoronoiGradientMaxAdd",
            "_AudioLinkVoronoiGradientMaxAddBand",
            "_AudioLinkVoronoiGradientMinAdd",
            "_AudioLinkVoronoiGradientMinAddBand",
            "_AudioLinkVoronoiInnerEmission",
            "_AudioLinkVoronoiInnerEmissionBand",
            "_AudioLinkVoronoiOuterEmission",
            "_AudioLinkVoronoiOuterEmissionBand",
            "_GlitterALAlphaAdd",
            "_GlitterALAlphaAddBand",
            "_GlitterALChronoRotationSpeed",
            "_GlitterALChronoRotationSpeedBand",
            "_GlitterALChronoRotationSpeedType",
            "_GlitterALChronoSparkleSpeed",
            "_GlitterALChronoSparkleSpeedBand",
            "_GlitterALChronoSparkleSpeedType",
            "_GlitterALEnabled",
            "_GlitterALMaxBrightnessAdd",
            "_GlitterALMaxBrightnessBand",
            "_GlitterALMinBrightnessAdd",
            "_GlitterALMinBrightnessBand",
            "_GlitterALSizeAdd",
            "_GlitterALSizeAddBand",
            "_PathChronoBandA",
            "_PathChronoBandB",
            "_PathChronoBandG",
            "_PathChronoBandR",
            "_PathChronoSpeedA",
            "_PathChronoSpeedB",
            "_PathChronoSpeedG",
            "_PathChronoSpeedR",
            "_PathChronoTypeA",
            "_PathChronoTypeB",
            "_PathChronoTypeG",
            "_PathChronoTypeR",
            "_AudioLinkRim2BrightnessAdd",
            "_AudioLinkRim2BrightnessBand",
            "_AudioLinkRim2EmissionAdd",
            "_AudioLinkRim2EmissionBand",
            "_AudioLinkRim2WidthAdd",
            "_AudioLinkRim2WidthBand",
            "_AudioLinkRimBrightnessAdd",
            "_AudioLinkRimBrightnessBand",
            "_AudioLinkRimEmissionAdd",
            "_AudioLinkRimEmissionBand",
            "_AudioLinkRimWidthAdd",
            "_AudioLinkRimWidthBand",
            "_DecalRotationCTALBand0",
            "_DecalRotationCTALBand1",
            "_DecalRotationCTALBand2",
            "_DecalRotationCTALBand3",
            "_DecalRotationCTALSpeed0",
            "_DecalRotationCTALSpeed1",
            "_DecalRotationCTALSpeed2",
            "_DecalRotationCTALSpeed3",
            "_DecalRotationCTALType0",
            "_DecalRotationCTALType1",
            "_DecalRotationCTALType2",
            "_DecalRotationCTALType3",
            "_DebugAutocorrelator",
            "_DebugBass",
            "_DebugCCColors",
            "_DebugCCLights",
            "_DebugCCStrip",
            "_DebugChronotensity",
            "_DebugDFT",
            "_DebugHighMids",
            "_DebugLowMids",
            "_DebugTreble",
            "_DebugWaveform",
            "_ALDecalVolume",
            "_ALDecalVolumeClipMax",
            "_ALDecalVolumeClipMin",
            "_ALDecalVolumeColorHigh",
            "_ALDecalVolumeColorHighThemeIndex",
            "_ALDecalVolumeColorLow",
            "_ALDecalVolumeColorLowThemeIndex",
            "_ALDecalVolumeColorMid",
            "_ALDecalVolumeColorMidThemeIndex",
            "_ALDecalVolumeColorSource",
            "_ALDecalVolumeStep",
            "_ALVolumeColorBlendAlpha",
            "_ALVolumeColorBlendType",
            "_ALVolumeColorDirection",
            "_ALVolumeColorHigh",
            "_ALVolumeColorHighThemeIndex",
            "_ALVolumeColorLow",
            "_ALVolumeColorLowThemeIndex",
            "_ALVolumeColorMid",
            "_ALVolumeColorMidThemeIndex",
            "_ALVolumeColorUV",
            "_AudioLinkFlipbookAlpha",
            "_AudioLinkFlipbookAlphaBand",
            "_AudioLinkFlipbookEmission",
            "_AudioLinkFlipbookEmissionBand",
            "_AudioLinkFlipbookFrame",
            "_AudioLinkFlipbookFrameBand",
            "_AudioLinkFlipbookScale",
            "_AudioLinkFlipbookScaleBand",
            "_AudioLinkOutlineColor",
            "_AudioLinkOutlineColorBand",
            "_AudioLinkOutlineColorMod",
            "_AudioLinkOutlineEmission",
            "_AudioLinkOutlineEmissionBand",
            "_AudioLinkOutlineSize",
            "_AudioLinkOutlineSizeBand",
            "_VertexLocalRotationCTALBandX",
            "_VertexLocalRotationCTALBandY",
            "_VertexLocalRotationCTALBandZ",
            "_VertexLocalRotationCTALSpeed",
            "_VertexLocalRotationCTALTypeX",
            "_VertexLocalRotationCTALTypeY",
            "_VertexLocalRotationCTALTypeZ",
            "_TruchetALAlpha",
            "_TruchetALAlphaBand",
            "_TruchetALAlphaToggle",
            "_TruchetALEmission",
            "_TruchetALEmissionBand",
            "_TruchetALEmissionToggle",
            "_VertexSpectrumMotion",
            "_VertexSpectrumOffsetMax",
            "_VertexSpectrumOffsetMin",
            "_VertexSpectrumUV",
            "_VertexSpectrumUVDirection",
            "_ALUVPosition",
            "_ALUVRotation",
            "_ALUVRotationSpeed",
            "_ALUVScale",
            "_ALDecalColorMask",
            "_ALDecalColorMaskPan",
            "_ALDecalColorMaskUV",
            "_ALDecalColorMask_ST",
            "_TruchetChronoPanBand",
            "_TruchetChronoPanSpeed",
            "_TruchetChronoPanToggle",
            "_TruchetChronoPanType",
            "_FlipbookChronoType",
            "_FlipbookChronotensityBand",
            "_FlipbookChronotensityEnabled",
            "_FlipbookChronotensitySpeed",
            "_AudioLinkDissolveAlpha",
            "_AudioLinkDissolveAlphaBand",
            "_AudioLinkDissolveDetail",
            "_AudioLinkDissolveDetailBand",
            "_AudioLinkSmoothingBass",
            "_AudioLinkSmoothingHighMid",
            "_AudioLinkSmoothingLowMid",
            "_AudioLinkSmoothingTreble",
            "_AlphaAudioLinkAddBand",
            "_AlphaAudioLinkAddRange",
            "_AlphaAudioLinkEnabled",
            "_ALDecalBandClipMax",
            "_ALDecalBandClipMin",
            "_ALDecalBandStep",
            "_VertexWorldTranslationALBand",
            "_VertexWorldTranslationALMax",
            "_VertexWorldTranslationALMin",
            "_ALDecalShapeClip",
            "_ALDecalShapeClipBandWidth",
            "_ALDecalShapeClipVolumeWidth",
            "_VertexLocalScaleALBand",
            "_VertexLocalScaleALMax",
            "_VertexLocalScaleALMin",
            "_VertexGlitchingAudioLinkBand",
            "_VertexGlitchingAudioLinkEnabled",
            "_VertexGlitchingAudiolinkOverride",
            "_VertexLocalTranslationALBand",
            "_VertexLocalTranslationALMax",
            "_VertexLocalTranslationALMin",
            "_MainALHueShiftBand",
            "_MainALHueShiftCTIndex",
            "_ALDecalUV",
            "_ALDecalUVMode",
            "_MainHueALCTEnabled",
            "_MainHueALMotionSpeed",
            "_ALDecalBlendAlpha",
            "_ALDecalBlendType",
            "_AudioLinkBandOverrideSliders",
            "_AudioLinkBandOverridesEnabled",
            "_ALDecalGlobalMask",
            "_ALDecalGlobalMaskBlendType",
            "_VertexLookAtAudioLinkBand",
            "_VertexLookAtAudioLinkEnabled",
            "_VertexRoundingRangeAL",
            "_VertexRoundingRangeBand",
            "_DistortionStrengthAudioLink",
            "_DistortionStrengthAudioLinkBand",
            "_DistortionStrength1AudioLink",
            "_DistortionStrength1AudioLinkBand",
            "_ALLowEmission",
            "_ALMidEmission",
            "_ALHighEmission",
            "_ALDecalLineWidth",
            "_ALDecalBaseBoost",
            "_AudioLinkCCStripY",
            "_ALDecalLowEmission",
            "_ALDecalMidEmission",
            "_ALDecalTrebleBoost",
            "_ALDecalHighEmission",
            "_TruchetHideWhenNoAL",
            "_AudioLinkAnimToggle",
            "_ALDecalControlsAlpha",
            "_OutlineALColorEnabled",
            "_VertexAudioLinkEnabled",
            "_EnableDissolveAudioLink",
            "_ALDecaldCircleDimensions",
            "_EnableDistortionAudioLink",
            "_VertexManipulationHeightAL",
            "_VertexLookAtAlphaAudiolink",
            "_VertexManipulationHeightBand",
            "_VertexLocalRotationAL",
        };

        /// <summary>
        /// The detail layer values.
        /// </summary>
        private static readonly string[] FinalPassMembers =
        {
            "_DetailTex",
            "_DetailTexGlobalMask",
            "_DetailTexGlobalMaskBlendType",
            "_DetailTexIntensity",
            "_DetailTexPan",
            "_DetailTexStochastic",
            "_DetailTexUV",
            "_DetailTex_ST",
            "_DetailNormalGlobalMask",
            "_DetailNormalGlobalMaskBlendType",
            "_DetailNormalMap",
            "_DetailNormalMapPan",
            "_DetailNormalMapScale",
            "_DetailNormalMapStochastic",
            "_DetailNormalMapUV",
            "_DetailNormalMap_ST",
            "_DetailMask",
            "_DetailMaskPan",
            "_DetailMaskStochastic",
            "_DetailMaskUV",
            "_DetailMask_ST",
            "_DetailTint",
            "_DetailTintThemeIndex",
            "_DetailBrightness",
            "_BackFaceDetailIntensity",
        };

        /// <summary>
        /// The vertex effect values.
        /// </summary>
        private static readonly string[] AutoExposureMembers =
        {
            "_VertexWindDetailAmplitude",
            "_VertexWindDetailDirection",
            "_VertexWindDetailFrequency",
            "_VertexWindDetailSpeed",
            "_VertexWindEnabled",
            "_VertexWindMaskChannel",
            "_VertexWindNoiseChannel",
            "_VertexWindNoiseScale",
            "_VertexWindNoiseSpeed",
            "_VertexWindNoiseStrength",
            "_VertexWindPrimaryAmplitude",
            "_VertexWindPrimaryDirection",
            "_VertexWindPrimaryFrequency",
            "_VertexWindPrimarySpeed",
            "_VertexManipulationHeight",
            "_VertexManipulationHeightAL",
            "_VertexManipulationHeightBand",
            "_VertexManipulationHeightBias",
            "_VertexManipulationHeightMapChannel",
            "_VertexManipulationHeightMask",
            "_VertexManipulationHeightMaskChannel",
            "_VertexManipulationHeightMaskPan",
            "_VertexManipulationHeightMaskUV",
            "_VertexManipulationHeightMask_ST",
            "_VertexTornadoBaseHeight",
            "_VertexTornadoFunnelShape",
            "_VertexTornadoIntensity",
            "_VertexTornadoMaskChannel",
            "_VertexTornadoMode",
            "_VertexTornadoRadius",
            "_VertexTornadoSpeed",
            "_VertexTornadoTopHeight",
            "_VertexTornadoVerticalPull",
            "_VertexLocalRotationCTALBandX",
            "_VertexLocalRotationCTALBandY",
            "_VertexLocalRotationCTALBandZ",
            "_VertexLocalRotationCTALSpeed",
            "_VertexLocalRotationCTALTypeX",
            "_VertexLocalRotationCTALTypeY",
            "_VertexLocalRotationCTALTypeZ",
            "_VertexSphereAlpha",
            "_VertexSphereCenter",
            "_VertexSphereHeight",
            "_VertexSphereMaskChannel",
            "_VertexSphereMode",
            "_VertexSphereRadius",
            "_VertexRoundingDivision",
            "_VertexRoundingEnabled",
            "_VertexRoundingMaskChannel",
            "_VertexRoundingRangeAL",
            "_VertexRoundingRangeBand",
            "_VertexRoundingSpace",
            "_VertexBarrelAlpha",
            "_VertexBarrelHeight",
            "_VertexBarrelMaskChannel",
            "_VertexBarrelMode",
            "_VertexBarrelWidth",
            "_VertexSpectrumMotion",
            "_VertexSpectrumOffsetMax",
            "_VertexSpectrumOffsetMin",
            "_VertexSpectrumUV",
            "_VertexSpectrumUVDirection",
            "_VertexBasicsMask",
            "_VertexBasicsMaskChannel",
            "_VertexBasicsMaskUVPan",
            "_VertexBasicsMask_ST",
            "_VertexManipulationLocalRotation",
            "_VertexManipulationLocalRotationSpeed",
            "_VertexManipulationLocalScale",
            "_VertexManipulationLocalTranslation",
            "_VertexWorldTranslationALBand",
            "_VertexWorldTranslationALMax",
            "_VertexWorldTranslationALMin",
            "_VertexLocalScaleALBand",
            "_VertexLocalScaleALMax",
            "_VertexLocalScaleALMin",
            "_VertexLocalTranslationALBand",
            "_VertexLocalTranslationALMax",
            "_VertexLocalTranslationALMin",
            "_VertexAudioLinkEnabled",
            "_VertexManipulationWorldTranslation",
            "_VertexLocalRotationAL",
        };

        /// <summary>
        /// The vertex look at values.
        /// </summary>
        private static readonly string[] VertexLookAtMembers =
        {
            "_LookAtAlphaAlpha",
            "_LookAtAlphaDistanceFalloff",
            "_LookAtAlphaFoV",
            "_LookAtAlphaForwardDirection",
            "_LookAtAlphaMaskEnabled",
            "_LookAtAlphaMaxPitchAngle",
            "_LookAtAlphaMaxRollAngle",
            "_LookAtAlphaMaxYawAngle",
            "_LookAtAlphaOriginOffset",
            "_LookAtAlphaPivotOffset",
            "_LookAtAlphaTargetUp",
            "_LookAtAlphaUpDirection",
            "_LookAtBlueAlpha",
            "_LookAtBlueDistanceFalloff",
            "_LookAtBlueFoV",
            "_LookAtBlueForwardDirection",
            "_LookAtBlueMaskEnabled",
            "_LookAtBlueMaxPitchAngle",
            "_LookAtBlueMaxRollAngle",
            "_LookAtBlueMaxYawAngle",
            "_LookAtBlueOriginOffset",
            "_LookAtBluePivotOffset",
            "_LookAtBlueTargetUp",
            "_LookAtBlueUpDirection",
            "_LookAtGreenAlpha",
            "_LookAtGreenDistanceFalloff",
            "_LookAtGreenFoV",
            "_LookAtGreenForwardDirection",
            "_LookAtGreenMaskEnabled",
            "_LookAtGreenMaxPitchAngle",
            "_LookAtGreenMaxRollAngle",
            "_LookAtGreenMaxYawAngle",
            "_LookAtGreenOriginOffset",
            "_LookAtGreenPivotOffset",
            "_LookAtGreenTargetUp",
            "_LookAtGreenUpDirection",
            "_LookAtMask",
            "_LookAtMaskPan",
            "_LookAtMaskUV",
            "_LookAtMask_ST",
            "_LookAtRedAlpha",
            "_LookAtRedDistanceFalloff",
            "_LookAtRedFoV",
            "_LookAtRedForwardDirection",
            "_LookAtRedMaskEnabled",
            "_LookAtRedMaxPitchAngle",
            "_LookAtRedMaxRollAngle",
            "_LookAtRedMaxYawAngle",
            "_LookAtRedOriginOffset",
            "_LookAtRedPivotOffset",
            "_LookAtRedTargetUp",
            "_LookAtRedUpDirection",
            "_LookAtVisibilityEnabled",
            "_LookAtVisibilityMirror",
            "_LookAtVisibilityMode",
            "_LookAtVisibilityVRCCameraDesktop",
            "_LookAtVisibilityVRCCameraScreenshot",
            "_LookAtVisibilityVRCCameraVR",
            "_LookAtVisibilityVRCMirrorDesktop",
            "_LookAtVisibilityVRCMirrorVR",
            "_LookAtVisibilityVRCRegular",
            "_VertexLookAtAudioLinkBand",
            "_VertexLookAtAudioLinkEnabled",
            "_VertexLookAtAlphaAudiolink",
        };

        /// <summary>
        /// The depth bulge values.
        /// </summary>
        private static readonly string[] DepthBulgeMembers =
        {
            "_DepthBulgeMask",
            "_DepthBulgeMaskChannel",
            "_DepthBulgeMaskUV",
            "_DepthBulgeMask_ST",
            "_DepthBulgeHeight",
            "_DepthBulgeFadeLength",
        };

        /// <summary>
        /// The back face values.
        /// </summary>
        private static readonly string[] BackFaceMembers =
        {
            "_BackFaceMask",
            "_BackFaceMaskChannel",
            "_BackFaceMaskPan",
            "_BackFaceMaskUV",
            "_BackFaceMask_ST",
            "_BackFaceHueSelectOrShift",
            "_BackFaceHueShift",
            "_BackFaceHueShiftEnabled",
            "_BackFaceHueShiftSpeed",
            "_BackFaceTexture",
            "_BackFaceTexturePan",
            "_BackFaceTextureUV",
            "_BackFaceTexture_ST",
            "_BackFaceColor",
            "_BackFaceColorThemeIndex",
            "_BackFaceEmissionLimiter",
            "_BackFaceEmissionStrength",
            "_BackFaceReplaceAlpha",
            "_BackFaceShiftColorSpace",
            "_BackFaceDetailIntensity",
        };

        /// <summary>
        /// The color and alpha feature values.
        /// </summary>
        private static readonly string[] VignetteMembers =
        {
            "_RGBAAlphaBlendType",
            "_RGBAAlphaEmissionStrength",
            "_RGBAAlphaEnable",
            "_RGBAAlphaMetallicInvert",
            "_RGBAAlphaPBRMaskScaleTiling",
            "_RGBAAlphaPBRMasksPan",
            "_RGBAAlphaPBRSplitMaskSample",
            "_RGBAAlphaPBRSplitMaskStochastic",
            "_RGBAAlphaPBRUV",
            "_RGBAAlphaScaleOffset",
            "_RGBAAlphaSmoothnessInvert",
            "_RGBABlueBlendType",
            "_RGBABlueEmissionStrength",
            "_RGBABlueEnable",
            "_RGBABlueMetallicInvert",
            "_RGBABluePBRMaskScaleTiling",
            "_RGBABluePBRMasksPan",
            "_RGBABluePBRSplitMaskSample",
            "_RGBABluePBRSplitMaskStochastic",
            "_RGBABluePBRUV",
            "_RGBABlueScaleOffset",
            "_RGBABlueSmoothnessInvert",
            "_RGBAGreenBlendType",
            "_RGBAGreenEmissionStrength",
            "_RGBAGreenEnable",
            "_RGBAGreenMetallicInvert",
            "_RGBAGreenPBRMaskScaleTiling",
            "_RGBAGreenPBRMasksPan",
            "_RGBAGreenPBRSplitMaskSample",
            "_RGBAGreenPBRSplitMaskStochastic",
            "_RGBAGreenPBRUV",
            "_RGBAGreenScaleOffset",
            "_RGBAGreenSmoothnessInvert",
            "_RGBAMetallicAlphaEnabled",
            "_RGBAMetallicBlueEnabled",
            "_RGBAMetallicGreenEnabled",
            "_RGBAMetallicMaps",
            "_RGBAMetallicMapsPan",
            "_RGBAMetallicMapsStochastic",
            "_RGBAMetallicMapsUV",
            "_RGBAMetallicMaps_ST",
            "_RGBAMetallicRedEnabled",
            "_RGBAPBRAlphaEnabled",
            "_RGBAPBRBlueEnabled",
            "_RGBAPBRGreenEnabled",
            "_RGBAPBRRedEnabled",
            "_RGBARedBlendType",
            "_RGBARedEmissionStrength",
            "_RGBARedEnable",
            "_RGBARedMetallicInvert",
            "_RGBARedPBRMaskScaleTiling",
            "_RGBARedPBRMasksPan",
            "_RGBARedPBRSplitMaskSample",
            "_RGBARedPBRSplitMaskStochastic",
            "_RGBARedPBRUV",
            "_RGBARedScaleOffset",
            "_RGBARedSmoothnessInvert",
            "_RGBASmoothnessMaps",
            "_RGBASmoothnessMapsPan",
            "_RGBASmoothnessMapsStochastic",
            "_RGBASmoothnessMapsUV",
            "_RGBASmoothnessMaps_ST",
            "_RedAlphaAdd",
            "_RedColor",
            "_RedColorThemeIndex",
            "_RedTexture",
            "_RedTexturePan",
            "_RedTextureStochastic",
            "_RedTextureUV",
            "_RedTexture_ST",
            "_BlueAlphaAdd",
            "_BlueColor",
            "_BlueColorThemeIndex",
            "_BlueTexture",
            "_BlueTexturePan",
            "_BlueTextureStochastic",
            "_BlueTextureUV",
            "_BlueTexture_ST",
            "_GreenAlphaAdd",
            "_GreenColor",
            "_GreenColorThemeIndex",
            "_GreenTexture",
            "_GreenTexturePan",
            "_GreenTextureStochastic",
            "_GreenTextureUV",
            "_GreenTexture_ST",
            "_AlphaTexture",
            "_AlphaTexturePan",
            "_AlphaTextureStochastic",
            "_AlphaTextureUV",
            "_AlphaTexture_ST",
            "_RgbRedGlobalMaskBlendType",
            "_RgbRedGlobalMaskChannel",
            "_RgbRedMaskChannel",
            "_RgbBlueGlobalMaskBlendType",
            "_RgbBlueGlobalMaskChannel",
            "_RgbBlueMaskChannel",
            "_RgbAlphaGlobalMaskBlendType",
            "_RgbAlphaGlobalMaskChannel",
            "_RgbAlphaMaskChannel",
            "_RgbGreenGlobalMaskBlendType",
            "_RgbGreenGlobalMaskChannel",
            "_RgbGreenMaskChannel",
            "_AlphaColor",
            "_AlphaColorThemeIndex",
            "_RgbNormalBGlobalMaskBlendType",
            "_RgbNormalBGlobalMaskChannel",
            "_RgbNormalGGlobalMaskBlendType",
            "_RgbNormalGGlobalMaskChannel",
            "_RgbNormalAGlobalMaskBlendType",
            "_RgbNormalAGlobalMaskChannel",
            "_RgbNormalRGlobalMaskBlendType",
            "_RgbNormalRGlobalMaskChannel",
            "_RGBMaskUV",
            "_RGBMaskPan",
            "_RGBMaskType",
            "_RgbNormalBUV",
            "_RgbNormalAUV",
            "_RgbNormalGUV",
            "_RgbNormalRUV",
            "_AlphaAlphaAdd",
            "_RgbNormalBPan",
            "_RgbNormalGPan",
            "_RgbNormalRPan",
            "_RgbNormalAPan",
            "_RgbNormalAScale",
            "_RgbNormalBScale",
            "_RgbNormalRScale",
            "_RgbNormalGScale",
            "_RgbNormalAStochastic",
            "_RgbNormalBStochastic",
            "_RgbNormalRStochastic",
            "_RgbNormalGStochastic",
            "_RgbNormalBMaskChannel",
            "_RgbNormalAMaskChannel",
            "_RgbNormalRMaskChannel",
            "_RgbNormalGMaskChannel",
            "_RgbNormalABlendSources",
            "_RgbNormalGBlendSources",
            "_RgbNormalRBlendSources",
            "_RgbNormalBBlendSources",
            "_RGBMask",
            "_RGBMask_ST",
            "_RgbNormalA",
            "_RgbNormalA_ST",
            "_RgbNormalB",
            "_RgbNormalB_ST",
            "_RgbNormalG",
            "_RgbNormalG_ST",
            "_RgbNormalR",
            "_RgbNormalR_ST",
        };

        /// <summary>
        /// The RGB mask selector, pan, and transform.
        /// </summary>
        private static readonly string[] RgbMaskMembers =
        {
            "_RGBMaskUV",
            "_RGBMaskPan",
            "_RGBMask",
            "_RGBMask_ST",
        };

        /// <summary>
        /// The packed metallic map and its invert flags.
        /// </summary>
        private static readonly string[] RgbaMetallicMembers =
        {
            "_RGBAMetallicAlphaEnabled",
            "_RGBAMetallicBlueEnabled",
            "_RGBAMetallicGreenEnabled",
            "_RGBAMetallicMaps",
            "_RGBAMetallicMapsPan",
            "_RGBAMetallicMapsStochastic",
            "_RGBAMetallicMapsUV",
            "_RGBAMetallicMaps_ST",
            "_RGBAMetallicRedEnabled",
            "_RGBARedMetallicInvert",
            "_RGBABlueMetallicInvert",
            "_RGBAGreenMetallicInvert",
            "_RGBAAlphaMetallicInvert",
        };

        /// <summary>
        /// The packed smoothness map and its invert flags.
        /// </summary>
        private static readonly string[] RgbaSmoothnessMembers =
        {
            "_RGBASmoothnessMaps",
            "_RGBASmoothnessMapsPan",
            "_RGBASmoothnessMapsStochastic",
            "_RGBASmoothnessMapsUV",
            "_RGBASmoothnessMaps_ST",
            "_RGBARedSmoothnessInvert",
            "_RGBABlueSmoothnessInvert",
            "_RGBAGreenSmoothnessInvert",
            "_RGBAAlphaSmoothnessInvert",
        };

        /// <summary>
        /// The red channel texture and its sampling values.
        /// </summary>
        private static readonly string[] RedTextureMembers =
        {
            "_RedTexture",
            "_RedTexturePan",
            "_RedTextureStochastic",
            "_RedTextureUV",
            "_RedTexture_ST",
        };

        /// <summary>
        /// The red channel normal map and its sampling values.
        /// </summary>
        private static readonly string[] RgbNormalRMembers =
        {
            "_RgbNormalRUV",
            "_RgbNormalRPan",
            "_RgbNormalRScale",
            "_RgbNormalRStochastic",
            "_RgbNormalRBlendSources",
            "_RgbNormalR",
            "_RgbNormalR_ST",
        };

        /// <summary>
        /// The green channel texture and its sampling values.
        /// </summary>
        private static readonly string[] GreenTextureMembers =
        {
            "_GreenTexture",
            "_GreenTexturePan",
            "_GreenTextureStochastic",
            "_GreenTextureUV",
            "_GreenTexture_ST",
        };

        /// <summary>
        /// The green channel normal map and its sampling values.
        /// </summary>
        private static readonly string[] RgbNormalGMembers =
        {
            "_RgbNormalGUV",
            "_RgbNormalGPan",
            "_RgbNormalGScale",
            "_RgbNormalGStochastic",
            "_RgbNormalGBlendSources",
            "_RgbNormalG",
            "_RgbNormalG_ST",
        };

        /// <summary>
        /// The blue channel texture and its sampling values.
        /// </summary>
        private static readonly string[] BlueTextureMembers =
        {
            "_BlueTexture",
            "_BlueTexturePan",
            "_BlueTextureStochastic",
            "_BlueTextureUV",
            "_BlueTexture_ST",
        };

        /// <summary>
        /// The blue channel normal map and its sampling values.
        /// </summary>
        private static readonly string[] RgbNormalBMembers =
        {
            "_RgbNormalBUV",
            "_RgbNormalBPan",
            "_RgbNormalBScale",
            "_RgbNormalBStochastic",
            "_RgbNormalBBlendSources",
            "_RgbNormalB",
            "_RgbNormalB_ST",
        };

        /// <summary>
        /// The alpha channel texture and its sampling values.
        /// </summary>
        private static readonly string[] AlphaTextureMembers =
        {
            "_AlphaTexture",
            "_AlphaTexturePan",
            "_AlphaTextureStochastic",
            "_AlphaTextureUV",
            "_AlphaTexture_ST",
        };

        /// <summary>
        /// The alpha channel normal map and its sampling values.
        /// </summary>
        private static readonly string[] RgbNormalAMembers =
        {
            "_RgbNormalAUV",
            "_RgbNormalAPan",
            "_RgbNormalAScale",
            "_RgbNormalAStochastic",
            "_RgbNormalABlendSources",
            "_RgbNormalA",
            "_RgbNormalA_ST",
        };

        /// <summary>
        /// The dissolve values and the UV tile dissolve.
        /// </summary>
        private static readonly string[] DistortMembers =
        {
            "_DissolveAlpha",
            "_DissolveAlpha0",
            "_DissolveAlpha1",
            "_DissolveAlpha2",
            "_DissolveAlpha3",
            "_DissolveAlpha4",
            "_DissolveAlpha5",
            "_DissolveAlpha6",
            "_DissolveAlpha7",
            "_DissolveAlpha8",
            "_DissolveAlpha9",
            "_DissolveApplyGlobalMaskBlendType",
            "_DissolveApplyGlobalMaskIndex",
            "_DissolveDetailEdgeSmoothing",
            "_DissolveDetailNoise",
            "_DissolveDetailNoisePan",
            "_DissolveDetailNoiseUV",
            "_DissolveDetailNoise_ST",
            "_DissolveDetailStrength",
            "_DissolveEdgeColor",
            "_DissolveEdgeColorThemeIndex",
            "_DissolveEdgeEmission",
            "_DissolveEdgeGradient",
            "_DissolveEdgeGradientPan",
            "_DissolveEdgeGradientUV",
            "_DissolveEdgeGradient_ST",
            "_DissolveEdgeHardness",
            "_DissolveEdgeHueSelectOrShift",
            "_DissolveEdgeHueShift",
            "_DissolveEdgeHueShiftColorSpace",
            "_DissolveEdgeHueShiftEnabled",
            "_DissolveEdgeHueShiftSpeed",
            "_DissolveEdgeWidth",
            "_DissolveEmission1Side",
            "_DissolveEmissionSide",
            "_DissolveEndPoint",
            "_DissolveHueSelectOrShift",
            "_DissolveHueShift",
            "_DissolveHueShiftColorSpace",
            "_DissolveHueShiftEnabled",
            "_DissolveHueShiftSpeed",
            "_DissolveInverseApplyGlobalMaskBlendType",
            "_DissolveInverseApplyGlobalMaskIndex",
            "_DissolveInvertDetailNoise",
            "_DissolveInvertNoise",
            "_DissolveInvertShape",
            "_DissolveMask",
            "_DissolveMaskGlobalMask",
            "_DissolveMaskGlobalMaskBlendType",
            "_DissolveMaskInvert",
            "_DissolveMaskPan",
            "_DissolveMaskUV",
            "_DissolveMask_ST",
            "_DissolveNoiseTexture",
            "_DissolveNoiseTexturePan",
            "_DissolveNoiseTextureUV",
            "_DissolveNoiseTexture_ST",
            "_DissolveP2PClamp",
            "_DissolveP2PEdgeLength",
            "_DissolveP2PWorldLocal",
            "_DissolveShapeEdgeLength",
            "_DissolveShapePosition",
            "_DissolveShapeRotation",
            "_DissolveShapeScale",
            "_DissolveStartPoint",
            "_DissolveTextureColor",
            "_DissolveTextureColorThemeIndex",
            "_DissolveToEmissionStrength",
            "_DissolveToTexture",
            "_DissolveToTexturePan",
            "_DissolveToTextureUV",
            "_DissolveToTexture_ST",
            "_DissolveType",
            "_DissolveUseVertexColors",
            "_DissolveWorldShape",
            "_UVTileDissolveAlpha_Row0_0",
            "_UVTileDissolveAlpha_Row0_1",
            "_UVTileDissolveAlpha_Row0_2",
            "_UVTileDissolveAlpha_Row0_3",
            "_UVTileDissolveAlpha_Row1_0",
            "_UVTileDissolveAlpha_Row1_1",
            "_UVTileDissolveAlpha_Row1_2",
            "_UVTileDissolveAlpha_Row1_3",
            "_UVTileDissolveAlpha_Row2_0",
            "_UVTileDissolveAlpha_Row2_1",
            "_UVTileDissolveAlpha_Row2_2",
            "_UVTileDissolveAlpha_Row2_3",
            "_UVTileDissolveAlpha_Row3_0",
            "_UVTileDissolveAlpha_Row3_1",
            "_UVTileDissolveAlpha_Row3_2",
            "_UVTileDissolveAlpha_Row3_3",
            "_UVTileDissolveDiscardAtMax",
            "_UVTileDissolveEnabled",
            "_UVTileDissolveUV",
            "_CenterOutDissolveDirection",
            "_CenterOutDissolveInvert",
            "_CenterOutDissolveMode",
            "_CenterOutDissolveNormals",
            "_CenterOutDissolvePower",
            "_SphericalDissolveCenter",
            "_SphericalDissolveClamp",
            "_SphericalDissolveInvert",
            "_SphericalDissolveRadius",
            "_AudioLinkDissolveAlpha",
            "_AudioLinkDissolveAlphaBand",
            "_AudioLinkDissolveDetail",
            "_AudioLinkDissolveDetailBand",
            "_ContinuousDissolve",
            "_EnableDissolveAudioLink",
        };

        /// <summary>
        /// The audio link decal values.
        /// </summary>
        private static readonly string[] AlDecalMembers =
        {
            "_ALDecalVolume",
            "_ALDecalVolumeClipMax",
            "_ALDecalVolumeClipMin",
            "_ALDecalVolumeColorHigh",
            "_ALDecalVolumeColorHighThemeIndex",
            "_ALDecalVolumeColorLow",
            "_ALDecalVolumeColorLowThemeIndex",
            "_ALDecalVolumeColorMid",
            "_ALDecalVolumeColorMidThemeIndex",
            "_ALDecalVolumeColorSource",
            "_ALDecalVolumeStep",
            "_ALUVPosition",
            "_ALUVRotation",
            "_ALUVRotationSpeed",
            "_ALUVScale",
            "_ALDecalColorMask",
            "_ALDecalColorMaskPan",
            "_ALDecalColorMaskUV",
            "_ALDecalColorMask_ST",
            "_ALDecalBandClipMax",
            "_ALDecalBandClipMin",
            "_ALDecalBandStep",
            "_ALDecalShapeClip",
            "_ALDecalShapeClipBandWidth",
            "_ALDecalShapeClipVolumeWidth",
            "_ALDecalUV",
            "_ALDecalUVMode",
            "_ALDecalBlendAlpha",
            "_ALDecalBlendType",
            "_ALDecalGlobalMask",
            "_ALDecalGlobalMaskBlendType",
            "_ALDecalBaseBoost",
            "_ALDecalLineWidth",
            "_ALDecalMidEmission",
            "_ALDecalTrebleBoost",
            "_ALDecalLowEmission",
            "_ALDecalHighEmission",
            "_ALDecalControlsAlpha",
            "_ALDecaldCircleDimensions",
        };

        /// <summary>
        /// The audio link volume color values.
        /// </summary>
        private static readonly string[] AlVolumeColorMembers =
        {
            "_ALVolumeColorBlendAlpha",
            "_ALVolumeColorBlendType",
            "_ALVolumeColorDirection",
            "_ALVolumeColorHigh",
            "_ALVolumeColorHighThemeIndex",
            "_ALVolumeColorLow",
            "_ALVolumeColorLowThemeIndex",
            "_ALVolumeColorMid",
            "_ALVolumeColorMidThemeIndex",
            "_ALVolumeColorUV",
            "_ALLowEmission",
            "_ALMidEmission",
            "_ALHighEmission",
        };

        /// <summary>
        /// The pathing values.
        /// </summary>
        private static readonly string[] PathingMembers =
        {
            "_PathALAutoCorrelator",
            "_PathALAutoCorrelatorA",
            "_PathALAutoCorrelatorB",
            "_PathALAutoCorrelatorG",
            "_PathALAutoCorrelatorMode",
            "_PathALAutoCorrelatorR",
            "_PathALAutoCorrelatorRangeA",
            "_PathALAutoCorrelatorRangeB",
            "_PathALAutoCorrelatorRangeG",
            "_PathALAutoCorrelatorRangeR",
            "_PathALCCA",
            "_PathALCCB",
            "_PathALCCG",
            "_PathALCCR",
            "_PathALChrono",
            "_PathALColorChord",
            "_PathALEmissionOffset",
            "_PathALHistory",
            "_PathALHistoryA",
            "_PathALHistoryB",
            "_PathALHistoryBandA",
            "_PathALHistoryBandB",
            "_PathALHistoryBandG",
            "_PathALHistoryBandR",
            "_PathALHistoryG",
            "_PathALHistoryMode",
            "_PathALHistoryR",
            "_PathALHistoryRangeA",
            "_PathALHistoryRangeB",
            "_PathALHistoryRangeG",
            "_PathALHistoryRangeR",
            "_PathALTimeOffset",
            "_PathALWidthOffset",
            "_PathChronoBandA",
            "_PathChronoBandB",
            "_PathChronoBandG",
            "_PathChronoBandR",
            "_PathChronoSpeedA",
            "_PathChronoSpeedB",
            "_PathChronoSpeedG",
            "_PathChronoSpeedR",
            "_PathChronoTypeA",
            "_PathChronoTypeB",
            "_PathChronoTypeG",
            "_PathChronoTypeR",
            "_PathColorA",
            "_PathColorAThemeIndex",
            "_PathColorB",
            "_PathColorBThemeIndex",
            "_PathColorG",
            "_PathColorGThemeIndex",
            "_PathColorR",
            "_PathColorRThemeIndex",
            "_PathEmissionStrength",
            "_PathGapLengths",
            "_PathGradientType",
            "_PathOffset",
            "_PathPointSampling",
            "_PathRemapA",
            "_PathRemapB",
            "_PathRemapG",
            "_PathRemapR",
            "_PathSegments",
            "_PathSoftness",
            "_PathSource",
            "_PathSourceDirA",
            "_PathSourceDirB",
            "_PathSourceDirG",
            "_PathSourceDirR",
            "_PathSpeed",
            "_PathSurfaceBlendMode",
            "_PathTime",
            "_PathTypeA",
            "_PathTypeB",
            "_PathTypeG",
            "_PathTypeR",
            "_PathWidth",
            "_PathingColorMap",
            "_PathingColorMapPan",
            "_PathingColorMapUV",
            "_PathingColorMap_ST",
            "_PathingMap",
            "_PathingMapPan",
            "_PathingMapUV",
            "_PathingMap_ST",
            "_PathingMap_TexelSize",
            "_PathingOverrideAlpha",
            "_PathingUVSelect",
            "_AudioLinkPathEmissionAddA",
            "_AudioLinkPathEmissionAddB",
            "_AudioLinkPathEmissionAddBandA",
            "_AudioLinkPathEmissionAddBandB",
            "_AudioLinkPathEmissionAddBandG",
            "_AudioLinkPathEmissionAddBandR",
            "_AudioLinkPathEmissionAddG",
            "_AudioLinkPathEmissionAddR",
            "_AudioLinkPathTimeOffsetA",
            "_AudioLinkPathTimeOffsetB",
            "_AudioLinkPathTimeOffsetBandA",
            "_AudioLinkPathTimeOffsetBandB",
            "_AudioLinkPathTimeOffsetBandG",
            "_AudioLinkPathTimeOffsetBandR",
            "_AudioLinkPathTimeOffsetG",
            "_AudioLinkPathTimeOffsetR",
            "_AudioLinkPathWidthOffsetA",
            "_AudioLinkPathWidthOffsetB",
            "_AudioLinkPathWidthOffsetBandA",
            "_AudioLinkPathWidthOffsetBandB",
            "_AudioLinkPathWidthOffsetBandG",
            "_AudioLinkPathWidthOffsetBandR",
            "_AudioLinkPathWidthOffsetG",
            "_AudioLinkPathWidthOffsetR",
            "_EnablePathRemapping",
        };

        /// <summary>
        /// The mirror texture values.
        /// </summary>
        private static readonly string[] MirrorTextureMembers =
        {
            "_MirrorColor",
            "_MirrorColorThemeIndex",
            "_MirrorTextureUV",
            "_MirrorTexturePan",
            "_MirrorTextureBlendType",
            "_MirrorTextureForceEnabled",
            "_MirrorTexture",
            "_MirrorTexture_ST",
        };

        /// <summary>
        /// The grain depth values.
        /// </summary>
        private static readonly string[] GrainMembers =
        {
            "_DepthColor",
            "_DepthColorBlendMode",
            "_DepthColorMaxDepth",
            "_DepthColorMaxValue",
            "_DepthColorMinDepth",
            "_DepthColorMinValue",
            "_DepthColorThemeIndex",
            "_DepthColorToggle",
            "_DepthMask",
            "_DepthMaskChannel",
            "_DepthMaskGlobalMask",
            "_DepthMaskGlobalMaskBlendType",
            "_DepthMaskPan",
            "_DepthMaskUV",
            "_DepthMask_ST",
            "_DepthAlphaMaxDepth",
            "_DepthAlphaMaxValue",
            "_DepthAlphaMinDepth",
            "_DepthAlphaMinValue",
            "_DepthAlphaToggle",
            "_DepthTexture",
            "_DepthTexturePan",
            "_DepthTextureUV",
            "_DepthTexture_ST",
            "_DepthEmissionStrength",
        };

        /// <summary>
        /// The text overlay values.
        /// </summary>
        private static readonly string[] EffectBumpMembers =
        {
            "_TextNumericColor",
            "_TextNumericColorThemeIndex",
            "_TextNumericDecimalDigits",
            "_TextNumericEmissionStrength",
            "_TextNumericEnabled",
            "_TextNumericOffset",
            "_TextNumericPadding",
            "_TextNumericRotation",
            "_TextNumericScale",
            "_TextNumericTrimZeroes",
            "_TextNumericUV",
            "_TextNumericValue",
            "_TextNumericWholeDigits",
            "_TextTimeColor",
            "_TextTimeColorThemeIndex",
            "_TextTimeEmissionStrength",
            "_TextTimeEnabled",
            "_TextTimeOffset",
            "_TextTimePadding",
            "_TextTimeRotation",
            "_TextTimeScale",
            "_TextTimeUV",
            "_TextPositionColor",
            "_TextPositionColorThemeIndex",
            "_TextPositionEmissionStrength",
            "_TextPositionEnabled",
            "_TextPositionOffset",
            "_TextPositionPadding",
            "_TextPositionRotation",
            "_TextPositionScale",
            "_TextPositionUV",
            "_TextPositionVertical",
            "_TextFPSColor",
            "_TextFPSColorThemeIndex",
            "_TextFPSUV",
            "_TextFPSScale",
            "_TextFPSOffset",
            "_TextFPSEnabled",
            "_TextPixelRange",
            "_TextFPSPadding",
            "_TextFPSRotation",
            "_TextFPSEmissionStrength",
            "_TextGlyphs",
        };

        /// <summary>
        /// The post process values.
        /// </summary>
        private static readonly string[] PostProcessMembers =
        {
            "_PPMask",
            "_PPMaskChannel",
            "_PPMaskInvert",
            "_PPMaskPan",
            "_PPMaskUV",
            "_PPMask_ST",
            "_PPHue",
            "_PPHueShiftColorSpace",
            "_PPPosterization",
            "_PPPosterizationAmount",
            "_PPHDR",
            "_PPRGB",
            "_PPTint",
            "_PPContrast",
            "_PPLightness",
            "_PPSaturation",
            "_PPBrightness",
            "_ppHueSelectOrShift",
        };

        /// <summary>
        /// The post process mask and its sampling values.
        /// </summary>
        private static readonly string[] PpMaskMembers =
        {
            "_PPMask",
            "_PPMaskChannel",
            "_PPMaskInvert",
            "_PPMaskPan",
            "_PPMaskUV",
            "_PPMask_ST",
        };

        /// <summary>
        /// The video effect values.
        /// </summary>
        private static readonly string[] VideoMembers =
        {
            "_VideoBacklight",
            "_VideoCRTPixelEnergizedTime",
            "_VideoCRTRefreshRate",
            "_VideoContrast",
            "_VideoEffectsEnable",
            "_VideoEmissionEnabled",
            "_VideoGameboyRamp",
            "_VideoMaskPanning",
            "_VideoMaskTexture",
            "_VideoMaskTextureChannel",
            "_VideoMaskTexturePan",
            "_VideoMaskTextureUV",
            "_VideoMaskTexture_ST",
            "_VideoPixelTexture",
            "_VideoPixelTextureUV",
            "_VideoPixelTexture_ST",
            "_VideoPixelateToResolution",
            "_VideoRepeatVideoTexture",
            "_VideoResolution",
            "_VideoSaturation",
            "_VideoType",
        };

        /// <summary>
        /// The Deliot Heitz stochastic density.
        /// </summary>
        private static readonly string[] StochasticDeliotMembers =
        {
            "_StochasticDeliotHeitzDensity",
        };

        /// <summary>
        /// The hex tile stochastic values.
        /// </summary>
        private static readonly string[] StochasticHextileMembers =
        {
            "_StochasticHexFallOffContrast",
            "_StochasticHexFallOffPower",
            "_StochasticHexGridDensity",
            "_StochasticHexRotationStrength",
        };

        /// <summary>
        /// The voronoi values.
        /// </summary>
        private static readonly string[] VoronoiMembers =
        {
            "_AudioLinkVoronoiChronoSpeedXBand",
            "_AudioLinkVoronoiChronoSpeedXSpeed",
            "_AudioLinkVoronoiChronoSpeedXType",
            "_AudioLinkVoronoiChronoSpeedYBand",
            "_AudioLinkVoronoiChronoSpeedYSpeed",
            "_AudioLinkVoronoiChronoSpeedYType",
            "_AudioLinkVoronoiChronoSpeedZBand",
            "_AudioLinkVoronoiChronoSpeedZSpeed",
            "_AudioLinkVoronoiChronoSpeedZType",
            "_AudioLinkVoronoiGradientMaxAdd",
            "_AudioLinkVoronoiGradientMaxAddBand",
            "_AudioLinkVoronoiGradientMinAdd",
            "_AudioLinkVoronoiGradientMinAddBand",
            "_AudioLinkVoronoiInnerEmission",
            "_AudioLinkVoronoiInnerEmissionBand",
            "_AudioLinkVoronoiOuterEmission",
            "_AudioLinkVoronoiOuterEmissionBand",
            "_VoronoiNoise",
            "_VoronoiNoiseChannel",
            "_VoronoiNoiseIntensity",
            "_VoronoiNoisePan",
            "_VoronoiNoiseUV",
            "_VoronoiNoise_ST",
            "_VoronoiMask",
            "_VoronoiMaskChannel",
            "_VoronoiMaskPan",
            "_VoronoiMaskUV",
            "_VoronoiMask_ST",
            "_VoronoiInnerColor",
            "_VoronoiInnerEmissionStrength",
            "_VoronoiOuterColor",
            "_VoronoiOuterEmissionStrength",
            "_VoronoiRandomMinMaxBrightness",
            "_VoronoiRandomMinMaxSaturation",
            "_VoronoiGlobalMask",
            "_VoronoiGlobalMaskBlendType",
            "_VoronoiType",
            "_VoronoiPower",
            "_VoronoiSpace",
            "_VoronoiScale",
            "_VoronoiSpeed",
            "_VoronoiBlend",
            "_VoronoiGradient",
            "_VoronoiAffectsMaterialAlpha",
            "_VoronoiEnableRandomCellColor",
        };

        /// <summary>
        /// The truchet values.
        /// </summary>
        private static readonly string[] TruchetMembers =
        {
            "_TruchetALAlpha",
            "_TruchetALAlphaBand",
            "_TruchetALAlphaToggle",
            "_TruchetALEmission",
            "_TruchetALEmissionBand",
            "_TruchetALEmissionToggle",
            "_TruchetAlpha",
            "_TruchetBlendType",
            "_TruchetChronoPanBand",
            "_TruchetChronoPanSpeed",
            "_TruchetChronoPanToggle",
            "_TruchetChronoPanType",
            "_TruchetColor",
            "_TruchetColorThemeIndex",
            "_TruchetDensity",
            "_TruchetEdgeSharpness",
            "_TruchetEmissionStrength",
            "_TruchetHideWhenNoAL",
            "_TruchetMask",
            "_TruchetMaskChannel",
            "_TruchetMaskGlobalMask",
            "_TruchetMaskGlobalMaskBlendType",
            "_TruchetMaskIntensity",
            "_TruchetMaskInvert",
            "_TruchetMaskPan",
            "_TruchetMaskUV",
            "_TruchetMask_ST",
            "_TruchetOverrideAlpha",
            "_TruchetPanOffset",
            "_TruchetTex",
            "_TruchetTexPan",
            "_TruchetTex_ST",
            "_TruchetThiccness",
            "_TruchetUV",
        };

        /// <summary>
        /// The uzumore values.
        /// </summary>
        private static readonly string[] UzumoreMembers =
        {
            "_UzumoreMask",
            "_UzumoreMaskUV",
            "_UzumoreBias",
            "_UzumoreAmount",
            "_UzumoreEnabled",
        };

        // --- the shared bits ------------------------------------------

        /// <summary>
        /// The four text overlay selectors of record section 5.
        /// </summary>
        private static readonly string[] TextSelectorProperties =
        {
            "_TextFPSUV",
            "_TextPositionUV",
            "_TextTimeUV",
            "_TextNumericUV",
        };

        /// <summary>
        /// The three parallax coordinate selectors of record section 5.
        /// </summary>
        private static readonly string[] ParallaxChannelProperties =
        {
            "_ParallaxUV",
            "_HeightMapUV",
            "_HeightmaskUV",
        };

        /// <summary>
        /// The audio link CC strip flag and the five UV debug uniforms.
        /// The uniforms have no Properties entries, so the reads go
        /// through GetFloat. A missing name reads 0.
        /// </summary>
        private static readonly string[] AudioUvFlagProperties =
        {
            "_AudioLinkCCStripY",
            "_DebugWaveform",
            "_DebugDFT",
            "_DebugCCStrip",
            "_DebugCCLights",
            "_DebugAutocorrelator",
        };

        /// <summary>
        /// The two audio link volume color selectors of record section 5.
        /// </summary>
        private static readonly string[] AlVolumeColorSelectors =
        {
            "_ALVolumeColorUV",
            "_ALVolumeColorDirection",
        };

        /// <summary>
        /// The 16 row flags of the tile discard grid in row and column
        /// order.
        /// </summary>
        private static readonly string[] UdimDiscardRowFlags = UdimRowFlagNames("_UDIMDiscardRow");

        /// <summary>
        /// The 16 row flags of the face discard grid in row and column
        /// order.
        /// </summary>
        private static readonly string[] UdimFaceDiscardRowFlags =
            UdimRowFlagNames("_UDIMFaceDiscardRow");

        private static string[] UdimRowFlagNames(string prefix)
        {
            var names = new string[16];
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    names[(row * 4) + column] =
                        prefix + row + "_" + column;
                }
            }

            return names;
        }

        private static string Number(float value)
        {
            return value.ToString(
                "0.######", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static readonly Vector4 IdentityScaleOffset =
            new Vector4(1f, 1f, 0f, 0f);

        private static readonly Vector4 ZeroVector =
            new Vector4(0f, 0f, 0f, 0f);
    }
}
