# Reviewed route keywords and textures for Poiyomi 9.3

Privacy note: This record contains facts about an installed vendor shader and product design. It names vendor shader properties, keywords, and line numbers. It quotes no vendor source text. It holds no private avatar observations, no machine identifiers, no host names, and no paths outside the repository.

Date: 2026-11-23.
Status on 2026-11-23: Complete. The reviewed table build (Tasks 2 to 6 of `docs/superpowers/plans/2026-11-23-poiyomi-reviewed-atlas-route.md`) reads this record instead of the five session review reports of 2026-11-23.

## 1. Question

Which Poiyomi 9.3 keywords and texture properties can the reviewed main color atlas route admit, under which conditions, and which must it refuse? A keyword is admissible only when a use that enables it renders the same after the atlas build. The coordinate rule of the spec holds for every texture with a selector: selector 0 moves the texture into a companion page under identity transform, zero pan, and no stochastic flag. Selectors 1, 2, 3, 4, 5, 8, 9 keep the texture bound and keyed. Selectors 6 and 7, every other value, and NaN refuse with `RequiredValueNotHeld`.

## 2. Method

The review read the base ForwardBase pass of the installed bundled shader file `Poiyomi Toon.shader` in the vendor package folder `com.poiyomi.toon`, subfolder `_PoiyomiShaders/Shaders/9.3/Toon`. All line numbers name that file. The record was assembled on 2026-11-23 from five read-only session review reports (coordinates and always-on reads, lighting, matcap rim emission masks, color post-process decals, UV geometry vertex audio). Every report fact that this record states was re-checked against the shader source on 2026-11-23. Where a report and the shader disagreed, the shader won. Section 3 lists each correction.

The keyword set is the set of `shader_feature` and `multi_compile` tokens of the base pass pragma lines 9979 to 10247. The texture set is the set of texture typed declarations (2D, 3D, Cube, 2DArray) in the Properties block, lines 3 to 4678. Both sets were extracted mechanically on 2026-11-23 and counted. Every companion texture row was verified by re-reading its sample line in the base pass. The verified column of the texture table marks that re-read.

Sampler words used in this record:

- `Member`: the sample passes `_MainTex` as the sampler state, so the sample reads the sampler state that `sampler_MainTex` resolves to.
- `Own`: the sample uses the sampler state bound to the texture itself.
- `sampler_linear_repeat`, `sampler_trilinear_repeat`, `sampler_point_repeat`, `sampler_linear_clamp`, `sampler_point_clamp`: the named inline sampler states of the shader, read at the sample line.

## 3. Corrections against the reports

1. `KEYWORD` is not a declared keyword of the base pass. Its pragma is a comment at 9995. The reports and the spec list it under KeyOnly. The keyword space holds 104 local keywords without it. This record carries no row for it. `GEOM_TYPE_MESH` is declared, at 10054.
2. `PROP_BUMPMAP`, `PROP_ALPHAMASK`, `PROP_MAINCOLORADJUSTTEXTURE`, `PROP_DETAILTEX`, `PROP_DETAILMASK`, `PROP_DETAILNORMALMAP`, `PROP_MULTILAYERMATHBLURMAP`, `PROP_SHADOWCOLORTEX`, `PROP_SHADOW2NDCOLORTEX`, `PROP_SHADOW3RDCOLORTEX`, `PROP_SHADOWSTRENGTHMASK`, and `PROP_SHADOWBORDERMASK` are not declared keywords. Their branches test the presence of the texture keyword or the absence of the optimizer, so the branch compiles in every shipped variant. Texture assignment is the real gate. The reports name some of them as sample gates. This record carries no rows for them. The declared texture keywords of the base pass are the 18 `PROP_` rows of section 4.
3. Sample line corrections, report value then verified value: `_AlphaMask` 29860 to 29877. `_LightingAOMaps` 29260 to 29257. `_MainColorAdjustTexture` 29828 to 29807. `_PPMask` 28224 to 28208. `_RimColorTex` 30300 to 30338. `_Rim2Mask` 30103 to 30084. `_Rim2Tex` 30112 to 30098. `_Set_RimLightMask` 30073 to 30071. `_Set_Rim2LightMask` 30129 to 30120. `_Rim2ColorTex` 30321 to 30350. `_RimEnviroMask` 26997 to 27004. `_AnisotropyMap` 26715 to 26733. `_VoronoiMask` 28733 to 28783. `_VoronoiNoise` 28744 to 28794. `_TruchetTex` 28977 to 28966. `_TruchetMask` 28911 to 28910. `_Heightmask` 19848 to 19832. `_HeightMap` 19856 to the call at 20017. `_ALDecalColorMask` 24746 to 24747. `_PathingColorMap` 27440 to 27438. `_ParallaxInternalMap` 28237 to 28236. `_ParallaxInternalMapMask` 28269 to 28270. `_LightDataSDFMap` 29577 to 29562 and 29566. `_CubeMapMask` 24705 to 24707.
4. `_ShadowBorderMask` has two branches with two samplers. The LOD branch at 22375 uses `sampler_trilinear_repeat` with explicit derivatives. The plain branch at 22379 uses `sampler_linear_repeat`. The route must model both. The condition `_ShadowBorderMaskLOD` is 0 keeps the derivative branch off.
5. `_PathingMap` has two branches. `_PathPointSampling` 1 reads `sampler_point_repeat` at 27392. `_PathPointSampling` 0 reads `sampler_linear_repeat` at 27396. Both are repeat samplers, so both allow selector 0.
6. `_UzumoreMask` reads `sampler_point_clamp` at 18081. It is the clamp case of the spec. Its property `_UzumoreMaskUV` is a Float, and the same value selects the sampled channel and the mesh UV channel. Default 3 reads UV3 and channel A.
7. The vertex basics selector property has no leading underscore: it is `VertexBasicsMaskUV`, declared at 3571. The pan property is `_VertexBasicsMaskUVPan`, declared at 3570. The reports wrote mixed forms.
8. `_EmissionScrollingCurve1` to `_EmissionScrollingCurve3` sample through the uniform names `_EmissionScrollingCurve_ST__1` to `_EmissionScrollingCurve_ST__3`, declared at 14123 to 14265. No material property of those names exists. These textures never move, so the route needs no transform property for them.
9. The matcap textures sample through `sampler_trilinear_repeat` with an explicit LOD, at 24233, 24352, 24466, 24580. The reports said sampler state sharing for the masks only. The matcap textures themselves stay unmoved neutrals.
10. `_OutlineMask` samples `sampler_linear_repeat` in the vertex stage at 18637. `_OutlineTexture` samples through `sampler_MainTex` at 54423 in the outline pass. The reports named neither sampler.
11. The base pass declares 106 pragma keywords: 104 local shader feature keywords and the 2 engine keywords. This matches the spec and the plan.
12. The texture typed declarations number 139 lines per bundled file. Two are comments: `_Udon_VideoTex` at 242 and a duplicate `_VertexGlitchMap` at 3830 next to the active declaration at 3836. Unique names number 138. The Two Pass file carries the same 139 declarations and the same 106 keywords. The texture table below carries one row per unique name, 138 rows. The two rows for the commented lines are the refused `_Udon_VideoTex` row and the shared `_VertexGlitchMap` row that names both lines.
13. `_MetallicGlossMap` is dead code. Its only sample site sits behind a never defined test at 27315. The record classes it neutral. `_VideoGameboyRamp` samples a brightness scalar at 28394, never a mesh UV. The record classes it neutral. Both facts are additions to the spec neutral list, which names 15 of the 17 neutrals.

## 4. Decision

The reviewed table admits the 104 local keywords of the base pass except the 8 refused keywords of section 4.1. The allow list holds the KeyOnly, Companion, and Conditional rows. A keyword outside the list refuses the use with `KeywordNotAllowed`. The two engine keywords stay outside the lists. A material that enables one refuses as it does today.

Texture keywords take the verdict of their texture. The route treats an assigned texture of a live feature as sampled in every case, so a texture keyword that decides a branch in either direction stays exact.

### 4.1 Keyword table

Columns: keyword, group, verdict, gate lines. The gate names the base pass pragma line and the property that drives the keyword. All 104 local keywords appear. The Two Pass file declares the same keywords.

| keyword | group | verdict | gate lines |
|---|---|---|---|
| PROP_LIGHTINGAOMAPS | Always on | Companion | pragma 9985, texture 844 |
| PROP_LIGHTINGDETAILSHADOWMAPS | Always on | Companion | pragma 9986, texture 853 |
| PROP_LIGHTINGSHADOWMASKS | Always on | Companion | pragma 9987, texture 866 |
| PROP_LIGHTDATASDFMAP | Always on | Conditional | pragma 9988, texture 881 |
| COLOR_GRADING_HDR | Color effects UV | Companion | pragma 9991, toggle 83 |
| PROP_MAINGRADATIONTEX | KeyOnly | KeyOnly | pragma 9992, texture 109 |
| POI_GLOBALMASK_TEXTURES | Matcap rim emission masks | Conditional | pragma 10004, toggle 3942 |
| USER_LUT | Color effects UV | Conditional | pragma 10008, toggle 4402 |
| POI_UDIMDISCARD | Color effects UV | Conditional | pragma 10012, toggle 2220 |
| POI_PARALLAX | Color effects UV | Conditional | pragma 10016, toggle 4450 |
| POI_AUDIOLINK | Color effects UV | Conditional | pragma 10020, toggle 3459 |
| POI_BLACKLIGHTMASKING | KeyOnly | KeyOnly | pragma 10024, toggle 3878 |
| FINALPASS | Color effects UV | Companion | pragma 10028, toggle 128 |
| AUTO_EXPOSURE | Color effects UV | Companion | pragma 10032, toggle 3566 |
| POI_VERTEX_LOOKAT | Color effects UV | Companion | pragma 10036, toggle 3729 |
| POI_VERTEX_GLITCHING | KeyOnly | KeyOnly | pragma 10040, toggle 3829 |
| POI_VERTEX_GLITCHING_TEXTURE | KeyOnly | KeyOnly | pragma 10041, toggle 3835 |
| POI_DEPTHBULGE | Color effects UV | Conditional | pragma 10045, toggle 2283 |
| POI_BACKFACE | Color effects UV | Companion | pragma 10049, toggle 630 |
| VIGNETTE | Color effects UV | Companion | pragma 10053, toggle 661 |
| GEOM_TYPE_MESH | KeyOnly | KeyOnly | pragma 10054, no toggle, no code reads it |
| PROP_RGBMASK | Color effects UV | Companion | pragma 10055, texture 664 |
| PROP_RGBAMETALLICMAPS | Color effects UV | Companion | pragma 10056, texture 668 |
| PROP_RGBASMOOTHNESSMAPS | Color effects UV | Companion | pragma 10057, texture 677 |
| PROP_REDTEXTURE | Color effects UV | Companion | pragma 10058, texture 694 |
| PROP_RGBNORMALR | Color effects UV | Companion | pragma 10059, texture 703 |
| PROP_GREENTEXTURE | Color effects UV | Companion | pragma 10060, texture 730 |
| PROP_RGBNORMALG | Color effects UV | Companion | pragma 10061, texture 739 |
| PROP_BLUETEXTURE | Color effects UV | Companion | pragma 10062, texture 766 |
| PROP_RGBNORMALB | Color effects UV | Companion | pragma 10063, texture 775 |
| PROP_ALPHATEXTURE | Color effects UV | Companion | pragma 10064, texture 802 |
| PROP_RGBNORMALA | Color effects UV | Companion | pragma 10065, texture 811 |
| POI_LTCGI | KeyOnly | KeyOnly | pragma 10069, toggle 2105 |
| VIGNETTE_MASKED | KeyOnly | KeyOnly | pragma 10073, toggle 925 |
| _LIGHTINGMODE_TEXTURERAMP | Lighting | Conditional | pragma 10074, enum driver 926 |
| _LIGHTINGMODE_MULTILAYER_MATH | Lighting | Companion | pragma 10074, enum driver 926 |
| _LIGHTINGMODE_SHADEMAP | Lighting | Companion | pragma 10074, enum driver 926 |
| _LIGHTINGMODE_REALISTIC | KeyOnly | KeyOnly | pragma 10074, enum driver 926 |
| _LIGHTINGMODE_WRAPPED | KeyOnly | KeyOnly | pragma 10074, enum driver 926 |
| _LIGHTINGMODE_SKIN | Lighting | Companion | pragma 10074, enum driver 926 |
| _LIGHTINGMODE_FLAT | KeyOnly | KeyOnly | pragma 10074, enum driver 926 |
| _LIGHTINGMODE_CLOTH | Lighting | Companion | pragma 10074, enum driver 926 |
| _LIGHTINGMODE_SDF | Lighting | Conditional | pragma 10074, enum driver 926 |
| GEOM_TYPE_BRANCH | Refused | Refuse | pragma 10084, toggle 255 |
| GEOM_TYPE_BRANCH_DETAIL | Refused | Refuse | pragma 10087, toggle 348 |
| GEOM_TYPE_FROND | Refused | Refuse | pragma 10090, toggle 441 |
| DEPTH_OF_FIELD_COC_VIEW | Refused | Refuse | pragma 10093, toggle 534 |
| PROP_DECALMASK | Refused | Refuse | pragma 10095, texture 237 |
| DISTORT | Color effects UV | Conditional | pragma 10098, toggle 2295 |
| POI_ANISOTROPICS | Lighting | Companion | pragma 10102, toggle 1110 |
| POI_MATCAP0 | Matcap rim emission masks | Conditional | pragma 10106, toggle 1158 |
| POI_MATCAP0_CUSTOM_NORMAL | Matcap rim emission masks | Companion | pragma 10107, parent toggle 1158 |
| COLOR_GRADING_HDR_3D | Matcap rim emission masks | Conditional | pragma 10110, toggle 1250 |
| POI_MATCAP1_CUSTOM_NORMAL | Matcap rim emission masks | Companion | pragma 10111, parent toggle 1250 |
| POI_MATCAP2 | Matcap rim emission masks | Conditional | pragma 10114, toggle 1342 |
| POI_MATCAP2_CUSTOM_NORMAL | Matcap rim emission masks | Companion | pragma 10115, parent toggle 1342 |
| POI_MATCAP3 | Matcap rim emission masks | Conditional | pragma 10118, toggle 1434 |
| POI_MATCAP3_CUSTOM_NORMAL | Matcap rim emission masks | Companion | pragma 10119, parent toggle 1434 |
| _CUBEMAP | Lighting | Companion | pragma 10123, toggle 1525 |
| POI_AL_DECAL | Color effects UV | Conditional | pragma 10127, toggle 3482 |
| POI_AL_VOLUMECOLOR | Color effects UV | Conditional | pragma 10131, toggle 3543 |
| _SUNDISK_HIGH_QUALITY | Refused | Refuse | pragma 10135, toggle 2447 |
| _EMISSION | Matcap rim emission masks | Conditional | pragma 10139, toggle 2527 |
| POI_EMISSION_1 | Matcap rim emission masks | Conditional | pragma 10143, toggle 2618 |
| POI_EMISSION_2 | Matcap rim emission masks | Conditional | pragma 10147, toggle 2708 |
| POI_EMISSION_3 | Matcap rim emission masks | Conditional | pragma 10151, toggle 2798 |
| _GLOSSYREFLECTIONS_OFF | Matcap rim emission masks | Conditional | pragma 10155, toggle 1568 |
| _RIMSTYLE_POIYOMI | Matcap rim emission masks | Companion | pragma 10156, enum driver 1569 |
| _RIMSTYLE_UTS2 | Matcap rim emission masks | Companion | pragma 10156, enum driver 1569 |
| _RIMSTYLE_LILTOON | Matcap rim emission masks | Companion | pragma 10156, enum driver 1569 |
| POI_RIM2 | Matcap rim emission masks | Conditional | pragma 10159, toggle 1688 |
| _RIM2STYLE_POIYOMI | Matcap rim emission masks | Companion | pragma 10160, enum driver 1689 |
| _RIM2STYLE_UTS2 | Matcap rim emission masks | Companion | pragma 10160, enum driver 1689 |
| _RIM2STYLE_LILTOON | Matcap rim emission masks | Companion | pragma 10160, enum driver 1689 |
| _POI_DEPTH_RIMLIGHT | KeyOnly | KeyOnly | pragma 10164, toggle 1807 |
| _SUNDISK_SIMPLE | Refused | Refuse | pragma 10168, toggle 2888 |
| POI_SUBSURFACESCATTERING | Lighting | Companion | pragma 10172, toggle 1836 |
| MOCHIE_PBR | Lighting | Companion | pragma 10176, toggle 1854 |
| GGX_ANISOTROPICS | Lighting | Companion | pragma 10177, toggle 1889 |
| POI_CLEARCOAT | Lighting | Companion | pragma 10180, toggle 1937 |
| POI_ENVIRORIM | Lighting | Companion | pragma 10184, toggle 1996 |
| POI_STYLIZED_StylizedSpecular | Lighting | Companion | pragma 10188, toggle 2011 |
| _STYLIZEDREFLECTIONMODE_UNITYCHAN | Lighting | Companion | pragma 10189, enum driver 2013 |
| _STYLIZEDREFLECTIONMODE_LILTOON | Lighting | Companion | pragma 10189, enum driver 2013 |
| POI_PATHING | Color effects UV | Conditional | pragma 10193, toggle 2979 |
| POI_MIRROR | KeyOnly | KeyOnly | pragma 10197, toggle 3123 |
| POI_MIRROR_TEXTURE | Color effects UV | Companion | pragma 10198, toggle 3134 |
| GRAIN | Color effects UV | Companion | pragma 10202, toggle 3149 |
| EFFECT_BUMP | Color effects UV | Conditional | pragma 10206, toggle 3186 |
| POSTPROCESS | Color effects UV | Companion | pragma 10210, toggle 4494 |
| PROP_PPMASK | Color effects UV | Companion | pragma 10211, texture 4495 |
| POI_INTERNALPARALLAX | Refused | Refuse | pragma 10215, toggle 3267 |
| POI_NORMALCORRECT | KeyOnly | KeyOnly | pragma 10219, toggle 834 |
| POI_VIDEO_EFFECTS | Color effects UV | Conditional | pragma 10223, toggle 3314 |
| POI_BACKLIGHT | Lighting | Companion | pragma 10227, toggle 2087 |
| POIBS_ENABLE | KeyOnly | KeyOnly | pragma 10231, toggle 4527 |
| POIBS_BLOOMFOG | KeyOnly | KeyOnly | pragma 10233, toggle 4541 |
| BSSBLOOMFOGTYPE_HEIGHT | KeyOnly | KeyOnly | pragma 10234, toggle 4546 |
| _STOCHASTICMODE_DELIOT_HEITZ | Color effects UV | Conditional | pragma 9983, enum driver 4384 |
| _STOCHASTICMODE_HEXTILE | Color effects UV | Conditional | pragma 9983, enum driver 4384 |
| _STOCHASTICMODE_NONE | KeyOnly | KeyOnly | pragma 9983, enum driver 4384 |
| POI_VORONOI | Color effects UV | Conditional | pragma 10239, toggle 3347 |
| POI_TRUCHET | Color effects UV | Conditional | pragma 10243, toggle 3411 |
| POI_UZUMORE | Color effects UV | Conditional | pragma 10247, toggle 3865 |

Group counts on 2026-11-23: Always on 4, Lighting 17, Matcap rim emission masks 21, Color effects UV 37, KeyOnly 17, Refused 8. Sum 104. Verdict counts: Companion 49, Conditional 30, KeyOnly 17, Refuse 8.

### 4.2 Engine keywords

| keyword | gate line | treatment |
|---|---|---|
| FOG_EXP2 | pragma 9979 | Engine keyword. Outside both lists. An enabled material refuses as today. |
| VERTEXLIGHT_ON | pragma 9980 | Engine keyword. Outside both lists. An enabled material refuses as today. |

### 4.3 Texture table

Columns: texture, declaration line, group, class, sampler, selector property, transform property, pan property, stochastic property, exceptions, sample lines, verified. Class values are member, member copy, neutral, companion, refused feature. No texture of this shader is a member copy. The sample lines name the base pass unless the text says otherwise. The verified column marks the re-read of the sample line and its sampler on 2026-11-23.

| texture | line | group | class | sampler | selector | transform | pan | stochastic | exceptions | sample lines | verified |
|---|---|---|---|---|---|---|---|---|---|---|---|
| _MainTex | 61 | Always on | member | Member | _MainTexUV | _MainTex_ST | _MainTexPan | _MainTexStochastic | Pixel mode and sharpened alpha to coverage read the texel size. Stochastic moves coordinates. The video pixelate resample at 28419 refuses through the video rule. | 29149, 28419 | yes |
| _BumpMap | 67 | Always on | companion | Member | _BumpMapUV | _BumpMap_ST | _BumpMapPan | _BumpMapStochastic | None. Assignment is the gate. | 29162 | yes |
| _AlphaMask | 72 | Always on | companion | Member | _AlphaMaskUV | _AlphaMask_ST | _AlphaMaskPan | none | Samples only when _MainAlphaMaskMode is not 0. Unassigned stays keyed. | 29877 | yes |
| _MainColorAdjustTexture | 84 | Color effects UV | companion | Member | _MainColorAdjustTextureUV | _MainColorAdjustTexture_ST | _MainColorAdjustTexturePan | none | Gate COLOR_GRADING_HDR. Unassigned skips the sample. | 29807 | yes |
| _MainGradationTex | 109 | KeyOnly | neutral | sampler_linear_clamp | none | none | none | none | Sampled by color channel value through POI_SAMPLE_1D_X, never by UV. | 29858 | yes |
| _DetailMask | 129 | Color effects UV | companion | Member | _DetailMaskUV | _DetailMask_ST | _DetailMaskPan | _DetailMaskStochastic | Gate FINALPASS. Runs only outside outline and shadow caster. | 20129 | yes |
| _DetailTex | 137 | Color effects UV | companion | Member | _DetailTexUV | _DetailTex_ST | _DetailTexPan | _DetailTexStochastic | Gate FINALPASS. | 20118 | yes |
| _DetailNormalMap | 148 | Color effects UV | companion | Member | _DetailNormalMapUV | _DetailNormalMap_ST | _DetailNormalMapPan | _DetailNormalMapStochastic | Gate FINALPASS. Skipped in outline and shadow caster. | 20151 | yes |
| _DecalMask | 237 | Refused | refused feature | Member | _DecalMaskUV | _DecalMask_ST | _DecalMaskPan | none | Serves the four refused decal keywords. | 23059 | yes |
| _Udon_VideoTex | 242 | Refused | refused feature | none | none | none | none | none | Commented out declaration. Only the refused decal code reads it, at 23051 and 23178. | none | yes |
| _DecalTexture | 270 | Refused | refused feature | Own | _DecalTextureUV | _DecalTexture_ST | _DecalTexturePan | none | Decal projection reads the coordinate as data. | 22861 | yes |
| _DecalTexture1 | 363 | Refused | refused feature | Own | _DecalTexture1UV | _DecalTexture1_ST | _DecalTexture1Pan | none | Same decal math, slot 1. | 22861 family | yes |
| _DecalTexture2 | 456 | Refused | refused feature | Own | _DecalTexture2UV | _DecalTexture2_ST | _DecalTexture2Pan | none | Same decal math, slot 2. | 22861 family | yes |
| _DecalTexture3 | 549 | Refused | refused feature | Own | _DecalTexture3UV | _DecalTexture3_ST | _DecalTexture3Pan | none | Same decal math, slot 3. | 22861 family | yes |
| _BackFaceTexture | 634 | Color effects UV | companion | Member | _BackFaceTextureUV | _BackFaceTexture_ST | _BackFaceTexturePan | none | Gate POI_BACKFACE. No UV flip on back faces. | 20181 | yes |
| _BackFaceMask | 637 | Color effects UV | companion | Member | _BackFaceMaskUV | _BackFaceMask_ST | _BackFaceMaskPan | none | Gate POI_BACKFACE. | 20191 | yes |
| _RGBMask | 664 | Color effects UV | companion | sampler_trilinear_repeat | _RGBMaskUV | _RGBMask_ST | _RGBMaskPan | none | _RGBMaskType 1 replaces the mask with vertex color and removes the sample. | 20258, 20357 | yes |
| _RGBAMetallicMaps | 668 | Color effects UV | companion | sampler_trilinear_repeat | _RGBAMetallicMapsUV | _RGBAMetallicMaps_ST | _RGBAMetallicMapsPan | _RGBAMetallicMapsStochastic | Split samples at 20425 to 20437 add _RGBAxPBRUV selectors, merged transforms _RGBAxPBRMaskScaleTiling with _RGBAxScaleOffset, pans _RGBAxPBRMasksPan, and _RGBAxPBRSplitMaskStochastic flags. All must hold identity, zero, and 0 when the split sample runs on selector 0. | 20420 | yes |
| _RGBASmoothnessMaps | 677 | Color effects UV | companion | sampler_trilinear_repeat | _RGBASmoothnessMapsUV | _RGBASmoothnessMaps_ST | _RGBASmoothnessMapsPan | _RGBASmoothnessMapsStochastic | Split samples at 20395 to 20407 with the same split properties as the metallic maps. | 20390 | yes |
| _RedTexture | 694 | Color effects UV | companion | sampler_trilinear_repeat | _RedTextureUV | _RedTexture_ST | _RedTexturePan | _RedTextureStochastic | Merged transform _RGBARedScaleOffset must also hold identity. | 20367 | yes |
| _RgbNormalR | 703 | Color effects UV | companion | sampler_trilinear_repeat | _RgbNormalRUV | _RgbNormalR_ST | _RgbNormalRPan | _RgbNormalRStochastic | Merged transform _RGBARedScaleOffset. Runtime gate _RgbNormalRScale above 0 and _RGBARedEnable. | 20290 | yes |
| _GreenTexture | 730 | Color effects UV | companion | sampler_trilinear_repeat | _GreenTextureUV | _GreenTexture_ST | _GreenTexturePan | _GreenTextureStochastic | Merged transform _RGBAGreenScaleOffset. | 20370 | yes |
| _RgbNormalG | 739 | Color effects UV | companion | sampler_trilinear_repeat | _RgbNormalGUV | _RgbNormalG_ST | _RgbNormalGPan | _RgbNormalGStochastic | Merged transform _RGBAGreenScaleOffset. | 20301 | yes |
| _BlueTexture | 766 | Color effects UV | companion | sampler_trilinear_repeat | _BlueTextureUV | _BlueTexture_ST | _BlueTexturePan | _BlueTextureStochastic | Merged transform _RGBABlueScaleOffset. | 20373 | yes |
| _RgbNormalB | 775 | Color effects UV | companion | sampler_trilinear_repeat | _RgbNormalBUV | _RgbNormalB_ST | _RgbNormalBPan | _RgbNormalBStochastic | Merged transform _RGBABlueScaleOffset. | 20314 | yes |
| _AlphaTexture | 802 | Color effects UV | companion | sampler_trilinear_repeat | _AlphaTextureUV | _AlphaTexture_ST | _AlphaTexturePan | _AlphaTextureStochastic | Merged transform _RGBAAlphaScaleOffset. | 20376 | yes |
| _RgbNormalA | 811 | Color effects UV | companion | sampler_trilinear_repeat | _RgbNormalAUV | _RgbNormalA_ST | _RgbNormalAPan | _RgbNormalAStochastic | Merged transform _RGBAAlphaScaleOffset. | 20329 | yes |
| _LightingAOMaps | 844 | Always on | companion | Member | _LightingAOMapsUV | _LightingAOMaps_ST | _LightingAOMapsPan | none | None. | 29257 | yes |
| _LightingDetailShadowMaps | 853 | Always on | companion | Member | _LightingDetailShadowMapsUV | _LightingDetailShadowMaps_ST | _LightingDetailShadowMapsPan | none | None. | 29270 | yes |
| _LightingShadowMasks | 866 | Always on | companion | Member | _LightingShadowMasksUV | _LightingShadowMasks_ST | _LightingShadowMasksPan | none | None. | 29287 | yes |
| _LightDataSDFMap | 881 | Always on | companion | sampler_linear_repeat | _LightDataSDFMapUV | _LightDataSDFMap_ST | _LightDataSDFMapPan | none | Samples only in light map mode 4. The table rule requires _LightingMapMode not 4, so the texture never samples in an admitted use. The LOD branch at 29562 reads UV0 derivatives. | 29562, 29566 | yes |
| _ToonRamp | 932 | KeyOnly | neutral | sampler_linear_clamp | none | none | none | none | The ramp row pick reads a mesh UV as data when _ToonRampCount is above 1 and _ToonRampUVSelector is 0. The table rule of section 5 excludes that state. | 22315 | yes |
| _ShadowColorTex | 941 | Lighting | companion | Member | _ShadowColorTexUV | _ShadowColorTex_ST | _ShadowColorTexPan | none | Samples only when the alpha of _ShadowColor is above 0. | 22440 | yes |
| _Shadow2ndColorTex | 955 | Lighting | companion | Member | _Shadow2ndColorTexUV | _Shadow2ndColorTex_ST | _Shadow2ndColorTexPan | none | Samples only when the alpha of _Shadow2ndColor is above 0. | 22450 | yes |
| _Shadow3rdColorTex | 967 | Lighting | companion | Member | _Shadow3rdColorTexUV | _Shadow3rdColorTex_ST | _Shadow3rdColorTexPan | none | Samples only when the alpha of _Shadow3rdColor is above 0. | 22462 | yes |
| _ShadowStrengthMask | 985 | Lighting | companion | Member | _ShadowStrengthMaskUV | _ShadowStrengthMask_ST | _ShadowStrengthMaskPan | none | The pan property is a float4. | 22417 | yes |
| _ShadowBorderMask | 995 | Lighting | companion | sampler_trilinear_repeat | _ShadowBorderMaskUV | _ShadowBorderMask_ST | _ShadowBorderMaskPan | none | LOD branch uses sampler_trilinear_repeat at 22375. Plain branch uses sampler_linear_repeat at 22379. _ShadowBorderMaskLOD must be 0, and _ShadowBorderMapToggle 1 turns the sample on. | 22375, 22379 | yes |
| _MultilayerMathBlurMap | 1004 | Lighting | companion | Member | _MultilayerMathBlurMapUV | _MultilayerMathBlurMap_ST | _MultilayerMathBlurMapPan | none | Always sampled while _LIGHTINGMODE_MULTILAYER_MATH is on. The selector property sits at 947. | 22321 | yes |
| _1st_ShadeMap | 1022 | Lighting | companion | Member | _1st_ShadeMapUV | _1st_ShadeMap_ST | _1st_ShadeMapPan | none | None. | 22050 | yes |
| _2nd_ShadeMap | 1029 | Lighting | companion | Member | _2nd_ShadeMapUV | _2nd_ShadeMap_ST | _2nd_ShadeMapPan | none | None. | 22057 | yes |
| _SkinLUT | 1044 | KeyOnly | neutral | sampler_linear_clamp | none | none | none | none | Sampled at a lighting lookup coordinate, never at a mesh UV. | 22641 | yes |
| _SkinThicknessMap | 1046 | Lighting | companion | Member | _SkinThicknessMapUV | _SkinThicknessMap_ST | _SkinThicknessMapPan | none | None. | 22613 | yes |
| _ClothDFG | 1063 | KeyOnly | neutral | Own | none | none | none | none | Sampled at a view and roughness coordinate. | 21845 | yes |
| _ClothMetallicSmoothnessMap | 1064 | Lighting | companion | Member | _ClothMetallicSmoothnessMapUV | _ClothMetallicSmoothnessMap_ST | _ClothMetallicSmoothnessMapPan | none | None. | 22525 | yes |
| _SDFShadingTexture | 1076 | Lighting | companion | Member | _SDFShadingTextureUV | _SDFShadingTexture_ST | _SDFShadingTexturePan | none | The sample multiplies the coordinate x by a light driven flip, so the texture must never move. Selector 0 refuses when _LIGHTINGMODE_SDF is on and the texture is assigned. | 22654 | yes |
| _AnisoColorMap | 1111 | Lighting | companion | Member | _AnisoColorMapUV | _AnisoColorMap_ST | _AnisoColorMapPan | none | None. | 24032 | yes |
| _Matcap | 1162 | KeyOnly | neutral | sampler_trilinear_repeat | none | none | _MatcapPan | none | Matcap coordinates come from view vectors with an explicit LOD. The UV blend at 24169 refuses through the matcap rule of section 5. | 24233 | yes |
| _MatcapMask | 1175 | Matcap rim emission masks | companion | Member | _MatcapMaskUV | _MatcapMask_ST | _MatcapMaskPan | none | Second sample at 24229 for the smoothness path. | 24261 | yes |
| _Matcap0NormalMap | 1199 | Matcap rim emission masks | companion | Member | _Matcap0NormalMapUV | _Matcap0NormalMap_ST | _Matcap0NormalMapPan | none | Sampled through calculateNormal, which reads sampler_MainTex. Gate POI_MATCAP0_CUSTOM_NORMAL under POI_MATCAP0. | 24214 | yes |
| _Matcap2 | 1254 | KeyOnly | neutral | sampler_trilinear_repeat | none | none | _Matcap2Pan | none | Matcap 1 slot texture. Same matcap blend rule. | 24352 | yes |
| _Matcap2Mask | 1267 | Matcap rim emission masks | companion | Member | _Matcap2MaskUV | _Matcap2Mask_ST | _Matcap2MaskPan | none | Second sample at 24348. | 24373 | yes |
| _Matcap1NormalMap | 1291 | Matcap rim emission masks | companion | Member | _Matcap1NormalMapUV | _Matcap1NormalMap_ST | _Matcap1NormalMapPan | none | Sampled through calculateNormal. | 24325 | yes |
| _Matcap3 | 1346 | KeyOnly | neutral | sampler_trilinear_repeat | none | none | _Matcap3Pan | none | Matcap 2 slot texture. Same matcap blend rule. | 24466 | yes |
| _Matcap3Mask | 1359 | Matcap rim emission masks | companion | Member | _Matcap3MaskUV | _Matcap3Mask_ST | _Matcap3MaskPan | none | Second sample at 24462. | 24487 | yes |
| _Matcap2NormalMap | 1383 | Matcap rim emission masks | companion | Member | _Matcap2NormalMapUV | _Matcap2NormalMap_ST | _Matcap2NormalMapPan | none | Sampled through calculateNormal. | 24439 | yes |
| _Matcap4 | 1438 | KeyOnly | neutral | sampler_trilinear_repeat | none | none | _Matcap4Pan | none | Matcap 3 slot texture. Same matcap blend rule. | 24580 | yes |
| _Matcap4Mask | 1451 | Matcap rim emission masks | companion | Member | _Matcap4MaskUV | _Matcap4Mask_ST | _Matcap4MaskPan | none | Second sample at 24576. | 24601 | yes |
| _Matcap3NormalMap | 1475 | Matcap rim emission masks | companion | Member | _Matcap3NormalMapUV | _Matcap3NormalMap_ST | _Matcap3NormalMapPan | none | Sampled through calculateNormal. | 24553 | yes |
| _CubeMap | 1530 | KeyOnly | neutral | Own | none | none | none | none | Cube sampled by view, reflection, or normal vector. | 24674 | yes |
| _CubeMapMask | 1542 | Lighting | companion | Member | _CubeMapMaskUV | _CubeMapMask_ST | _CubeMapMaskPan | none | None. | 24707 | yes |
| _Set_RimLightMask | 1571 | Matcap rim emission masks | companion | Member | _Set_RimLightMaskUV | _Set_RimLightMask_ST | _Set_RimLightMaskPan | none | Gate _RIMSTYLE_UTS2. | 30071 | yes |
| _RimMask | 1575 | Matcap rim emission masks | companion | Member | _RimMaskUV | _RimMask_ST | _RimMaskPan | none | Gate _RIMSTYLE_POIYOMI. | 30034 | yes |
| _RimTex | 1583 | Matcap rim emission masks | companion | Member | _RimTexUV | _RimTex_ST | _RimTexPan | none | Gate _RIMSTYLE_POIYOMI. | 30048 | yes |
| _RimColorTex | 1630 | Matcap rim emission masks | companion | Member | _RimColorTexUV | _RimColorTex_ST | _RimColorTexPan | none | Gate _RIMSTYLE_LILTOON. | 30338 | yes |
| _Set_Rim2LightMask | 1691 | Matcap rim emission masks | companion | Member | _Set_Rim2LightMaskUV | _Set_Rim2LightMask_ST | _Set_Rim2LightMaskPan | none | Gate _RIM2STYLE_UTS2. | 30120 | yes |
| _Rim2Mask | 1695 | Matcap rim emission masks | companion | Member | _Rim2MaskUV | _Rim2Mask_ST | _Rim2MaskPan | none | Gate _RIM2STYLE_POIYOMI. | 30084 | yes |
| _Rim2Tex | 1703 | Matcap rim emission masks | companion | Member | _Rim2TexUV | _Rim2Tex_ST | _Rim2TexPan | none | Gate _RIM2STYLE_POIYOMI. | 30098 | yes |
| _Rim2ColorTex | 1751 | Matcap rim emission masks | companion | Member | _Rim2ColorTexUV | _Rim2ColorTex_ST | _Rim2ColorTexPan | none | Gate _RIM2STYLE_LILTOON. | 30350 | yes |
| _SSSThicknessMap | 1839 | Lighting | companion | Member | _SSSThicknessMapUV | _SSSThicknessMap_ST | _SSSThicknessMapPan | none | The selector enum is UV0 to UV3 plus DistortedUV1 at 4. Value 4 reads a distorted coordinate built from UV1, never UV0. | 26348 | yes |
| _MochieMetallicMaps | 1862 | Lighting | companion | Member | _MochieMetallicMapsUV | _MochieMetallicMaps_ST | _MochieMetallicMapsPan | _MochieMetallicMapsStochastic | The split sample at 26600 adds _MochieMetallicMasksUV, merged transform _PBRMaskScaleTiling, _MochieMetallicMasksPan, and _PBRSplitMaskStochastic. All must hold when the split sample runs on selector 0. | 26596, 26600 | yes |
| _MochieReflCube | 1880 | KeyOnly | neutral | Own | none | none | none | none | Cube sampled by view. | 26580 family | yes |
| _AnisotropyMap | 1891 | Lighting | companion | Member | _AnisotropyMapUV | _AnisotropyMap_ST | _AnisotropyMapPan | none | None. | 26733 | yes |
| _ClearCoatMaps | 1949 | Lighting | companion | Member | _ClearCoatMapsUV | _ClearCoatMaps_ST | _ClearCoatMapsPan | _ClearCoatMapsStochastic | None. | 26851 | yes |
| _ClearCoatFallback | 1962 | KeyOnly | neutral | Own | none | none | none | none | Fallback cube sampled by the reflection vector. | 26880 family | yes |
| _RimEnviroMask | 1997 | Lighting | companion | Member | _RimEnviroMaskUV | none | _RimEnviroMaskPan | none | No transform property exists. The sample reads the raw selected UV plus pan. | 27004 | yes |
| _HighColor_Tex | 2016 | Lighting | companion | Member | _HighColor_TexUV | _HighColor_Tex_ST | _HighColor_TexPan | none | Gate _STYLIZEDREFLECTIONMODE_UNITYCHAN body under POI_STYLIZED_StylizedSpecular. | 27020 | yes |
| _Set_HighColorMask | 2021 | Lighting | companion | Member | _Set_HighColorMaskUV | _Set_HighColorMask_ST | _Set_HighColorMaskPan | none | Same gate. | 27039 | yes |
| _SmoothnessTex | 2054 | Lighting | companion | sampler_linear_repeat | none | none | none | none | Samples raw UV0 with no transform. Gate _STYLIZEDREFLECTIONMODE_LILTOON body under POI_STYLIZED_StylizedSpecular. | 27307 | yes |
| _MetallicGlossMap | 2057 | KeyOnly | neutral | sampler_linear_repeat | none | none | none | none | Dead code. The only sample site sits behind a never defined test at 27315 and never compiles. | none | yes |
| _ReflectionColorTex | 2073 | Lighting | companion | sampler_linear_repeat | none | none | none | none | Samples raw UV0 with no transform. Same gate as _SmoothnessTex. | 27323 | yes |
| _ReflectionCubeTex | 2074 | KeyOnly | neutral | Own | none | none | none | none | Cube sampled by view and normal. | 27346 | yes |
| _BacklightColorTex | 2089 | Lighting | companion | Member | _BacklightColorTexUV | _BacklightColorTex_ST | _BacklightColorTexPan | none | None. | 28515 | yes |
| _OutlineMask | 2127 | Always on | companion | sampler_linear_repeat | _OutlineMaskUV | _OutlineMask_ST | _OutlineMaskPan | none | Vertex stage sample at LOD 0. Compiles only in the outline pass. | 18637 | yes |
| _OutlineTexture | 2133 | Always on | companion | Member | _OutlineTextureUV | _OutlineTexture_ST | _OutlineTexturePan | none | Outline pass sample. | 54423 | yes |
| _DepthBulgeMask | 2285 | Color effects UV | companion | sampler_linear_repeat | _DepthBulgeMaskUV | _DepthBulgeMask_ST | none | none | Vertex stage sample at LOD 0. Enum covers UV0 to UV3. No pan property exists. Selector 0 refuses, or the mask joins the page. | 18037 | yes |
| _DissolveEdgeGradient | 2303 | KeyOnly | neutral | Member | none | none | none | none | Sampled by the dissolve alpha value, never by a mesh UV. | 23868 | yes |
| _DissolveToTexture | 2310 | Color effects UV | companion | Member | _DissolveToTextureUV | _DissolveToTexture_ST | _DissolveToTexturePan | none | Pan advances with time, so zero pan is mandatory. | 23623 | yes |
| _DissolveNoiseTexture | 2317 | Color effects UV | companion | Member | _DissolveNoiseTextureUV | _DissolveNoiseTexture_ST | _DissolveNoiseTexturePan | none | Same pan rule. | 23629 | yes |
| _DissolveDetailNoise | 2321 | Color effects UV | companion | Member | _DissolveDetailNoiseUV | _DissolveDetailNoise_ST | _DissolveDetailNoisePan | none | Same pan rule. | 23679 | yes |
| _DissolveMask | 2332 | Color effects UV | companion | Member | _DissolveMaskUV | _DissolveMask_ST | _DissolveMaskPan | none | Same pan rule. | 23607 | yes |
| _FlipbookTexArray | 2451 | Refused | refused feature | Own | _FlipbookTexArrayUV | _FlipbookTexArray_ST | _FlipbookTexArrayPan | none | Flipbook frac, rotation, remap, and derivative reads refuse. | 24974, 24984 | yes |
| _FlipbookMask | 2456 | Refused | refused feature | Member | _FlipbookMaskUV | _FlipbookMask_ST | _FlipbookMaskPan | none | Belongs to the refused flipbook feature. | 24883 | yes |
| _EmissionMask | 2529 | Matcap rim emission masks | companion | Member | _EmissionMaskUV | _EmissionMask_ST | _EmissionMaskPan | none | Gate _EMISSION. | 25200 | yes |
| _EmissionMap | 2539 | Matcap rim emission masks | companion | Member | _EmissionMapUV | _EmissionMap_ST | _EmissionMapPan | none | Center-out branch at 25152 reads the whole 0 to 1 space. _EmissionCenterOutEnabled must be 0 when the map moves. | 25148, 25152 | yes |
| _EmissionScrollingCurve | 2585 | Matcap rim emission masks | companion | Member | none | _EmissionScrollingCurve_ST | none | none | Never moves. The sample offset adds a position term. _EmissionScrollingUseCurve must be 0 or the texture unassigned. | 25179 | yes |
| _EmissionMask1 | 2619 | Matcap rim emission masks | companion | Member | _EmissionMask1UV | _EmissionMask1_ST | _EmissionMask1Pan | none | Gate POI_EMISSION_1. | 25378 | yes |
| _EmissionMap1 | 2629 | Matcap rim emission masks | companion | Member | _EmissionMap1UV | _EmissionMap1_ST | _EmissionMap1Pan | none | Center-out at 25330. _EmissionCenterOutEnabled1 must be 0 when the map moves. | 25326 | yes |
| _EmissionScrollingCurve1 | 2675 | Matcap rim emission masks | companion | Member | none | _EmissionScrollingCurve_ST__1 uniform only | none | none | Never moves. _EmissionScrollingUseCurve1 0 or unassigned. | 25357 | yes |
| _EmissionMask2 | 2709 | Matcap rim emission masks | companion | Member | _EmissionMask2UV | _EmissionMask2_ST | _EmissionMask2Pan | none | Gate POI_EMISSION_2. | 25556 | yes |
| _EmissionMap2 | 2719 | Matcap rim emission masks | companion | Member | _EmissionMap2UV | _EmissionMap2_ST | _EmissionMap2Pan | none | Center-out at 25508. _EmissionCenterOutEnabled2 must be 0 when the map moves. | 25504 | yes |
| _EmissionScrollingCurve2 | 2765 | Matcap rim emission masks | companion | Member | none | _EmissionScrollingCurve_ST__2 uniform only | none | none | Never moves. _EmissionScrollingUseCurve2 0 or unassigned. | 25535 | yes |
| _EmissionMask3 | 2799 | Matcap rim emission masks | companion | Member | _EmissionMask3UV | _EmissionMask3_ST | _EmissionMask3Pan | none | Gate POI_EMISSION_3. | 25734 | yes |
| _EmissionMap3 | 2809 | Matcap rim emission masks | companion | Member | _EmissionMap3UV | _EmissionMap3_ST | _EmissionMap3Pan | none | Center-out at 25686. _EmissionCenterOutEnabled3 must be 0 when the map moves. | 25682 | yes |
| _EmissionScrollingCurve3 | 2855 | Matcap rim emission masks | companion | Member | none | _EmissionScrollingCurve_ST__3 uniform only | none | none | Never moves. _EmissionScrollingUseCurve3 0 or unassigned. | 25713 | yes |
| _GlitterTexture | 2897 | Refused | refused feature | sampler_linear_clamp | none | _GlitterTexture_ST | _GlitterTexturePan | none | Glitter cells read the UV as data and the sample gradients track UV0. | 26276 | yes |
| _GlitterColorMap | 2898 | Refused | refused feature | Member | _GlitterColorMapUV | _GlitterColorMap_ST | _GlitterColorMapPan | none | Belongs to the refused glitter feature. | 26249 | yes |
| _GlitterMask | 2936 | Refused | refused feature | Member | _GlitterMaskUV | _GlitterMask_ST | _GlitterMaskPan | none | Belongs to the refused glitter feature. | 26283 | yes |
| _PathingMap | 2986 | Color effects UV | companion | sampler_point_repeat | _PathingMapUV | _PathingMap_ST | _PathingMapPan | none | _PathPointSampling 1 reads sampler_point_repeat at 27392. _PathPointSampling 0 reads sampler_linear_repeat at 27396. Both are repeat samplers. | 27392, 27396 | yes |
| _PathingColorMap | 2989 | Color effects UV | companion | Member | _PathingColorMapUV | _PathingColorMap_ST | _PathingColorMapPan | none | None. | 27438 | yes |
| _MirrorTexture | 3138 | Color effects UV | companion | Member | _MirrorTextureUV | _MirrorTexture_ST | _MirrorTexturePan | none | Gate POI_MIRROR_TEXTURE. | 27737 | yes |
| _DepthMask | 3151 | Color effects UV | companion | Member | _DepthMaskUV | _DepthMask_ST | _DepthMaskPan | none | Gate GRAIN. | 27779 | yes |
| _DepthTexture | 3161 | Color effects UV | companion | Member | _DepthTextureUV | _DepthTexture_ST | _DepthTexturePan | none | Selector 8 is a depth gradient coordinate, not a mesh UV. Selector 0 or 8 both admit. | 27803 | yes |
| _TextGlyphs | 3187 | KeyOnly | neutral | Own | none | none | none | none | The text overlay reads the chosen mesh UV as data. The table rule requires the four text selectors not 0. | 27912 | yes |
| _ParallaxInternalMap | 3271 | Refused | refused feature | Member | none | _ParallaxInternalMap_ST | _ParallaxInternalMapPan | none | Internal parallax offsets hard-coded UV0. No selector exists. | 28236 | yes |
| _ParallaxInternalMapMask | 3276 | Refused | refused feature | Member | _ParallaxInternalMapMaskUV | _ParallaxInternalMapMask_ST | _ParallaxInternalMapMaskPan | none | Belongs to the refused feature. | 28270 | yes |
| _VideoPixelTexture | 3318 | Color effects UV | companion | Own | _VideoPixelTextureUV | _VideoPixelTexture_ST | none | none | The sample coordinate multiplies by the video resolution, so the texture must never move. _VideoPixelTextureUV must not be 0 when assigned. | 28425 | yes |
| _VideoMaskTexture | 3320 | Color effects UV | companion | Member | _VideoMaskTextureUV | _VideoMaskTexture_ST | _VideoMaskTexturePan | none | Gate POI_VIDEO_EFFECTS. | 28492 | yes |
| _VideoGameboyRamp | 3339 | KeyOnly | neutral | sampler_linear_clamp | none | none | none | none | Sampled by a brightness scalar, never by a mesh UV. | 28394 | yes |
| _VoronoiMask | 3353 | Color effects UV | companion | Member | _VoronoiMaskUV | _VoronoiMask_ST | _VoronoiMaskPan | none | _VoronoiSpace must not be 2. | 28783 | yes |
| _VoronoiNoise | 3361 | Color effects UV | companion | Member | _VoronoiNoiseUV | _VoronoiNoise_ST | _VoronoiNoisePan | none | Same space rule. | 28794 | yes |
| _TruchetTex | 3418 | Color effects UV | companion | Member | none | _TruchetTex_ST | _TruchetTexPan | none | Sampled at the pattern coordinate with explicit gradients. Never moves. _TruchetUV must not be 0. No selector property exists. | 28966 | yes |
| _TruchetMask | 3421 | Color effects UV | companion | Member | _TruchetMaskUV | _TruchetMask_ST | _TruchetMaskPan | none | Same pattern rule. | 28910 | yes |
| _ALDecalColorMask | 3519 | Color effects UV | companion | Member | _ALDecalColorMaskUV | _ALDecalColorMask_ST | _ALDecalColorMaskPan | none | _ALDecalUV must not be 0. | 24747 | yes |
| _VertexBasicsMask | 3569 | Color effects UV | companion | sampler_linear_repeat | VertexBasicsMaskUV | _VertexBasicsMask_ST | _VertexBasicsMaskUVPan | none | Vertex stage sample at LOD 0. The selector property has no leading underscore. _VertexWindEnabled must be 0 when the mask is a page, because the wind branch at 18348 reads world coordinates. | 18241, 18348 | yes |
| _VertexManipulationHeightMask | 3581 | Color effects UV | companion | sampler_linear_repeat | _VertexManipulationHeightMaskUV | _VertexManipulationHeightMask_ST | _VertexManipulationHeightMaskPan | none | Vertex stage sample at LOD 0. Enum covers UV0 to UV3. | 18282 | yes |
| _LookAtMask | 3731 | Color effects UV | companion | sampler_linear_repeat | _LookAtMaskUV | _LookAtMask_ST | _LookAtMaskPan | none | Vertex stage sample at LOD 0. Gate POI_VERTEX_LOOKAT. Enum covers UV0 to UV3. | 18444 | yes |
| _VertexGlitchMap | 3836 | KeyOnly | neutral | sampler_linear_repeat | none | none | none | none | Sampled at world height coordinates. A commented duplicate declaration sits at 3830. | 18761 | yes |
| _UzumoreMask | 3869 | Color effects UV | companion | sampler_point_clamp | _UzumoreMaskUV | none | none | none | The clamp sampler refuses selector 0. The property doubles as the channel index. Default 3 reads UV3 and channel A. No transform or pan exists. | 18081 | yes |
| _GlobalMaskTexture0 | 3944 | Matcap rim emission masks | companion | Member | _GlobalMaskTexture0UV | _GlobalMaskTexture0_ST | _GlobalMaskTexture0Pan | none | Split mode adds _GlobalMaskTexture0SplitTilingOffset_G, _B, and _A transforms plus _GlobalMaskTexture0SplitPan_G, _B, and _A. Split on requires identity transforms and zero split pans. | 19265 | yes |
| _GlobalMaskTexture1 | 3958 | Matcap rim emission masks | companion | Member | _GlobalMaskTexture1UV | _GlobalMaskTexture1_ST | _GlobalMaskTexture1Pan | none | Split properties as for slot 0. | 19278 family | yes |
| _GlobalMaskTexture2 | 3972 | Matcap rim emission masks | companion | Member | _GlobalMaskTexture2UV | _GlobalMaskTexture2_ST | _GlobalMaskTexture2Pan | none | Split properties as for slot 0. | 19291 family | yes |
| _GlobalMaskTexture3 | 3986 | Matcap rim emission masks | companion | Member | _GlobalMaskTexture3UV | _GlobalMaskTexture3_ST | _GlobalMaskTexture3Pan | none | Split properties as for slot 0. | 19304 family | yes |
| _DistortionMask | 4404 | Color effects UV | companion | Member | _DistortionMaskUV | none | _DistortionMaskPan | none | No transform property exists. Gate USER_LUT. | 19693 | yes |
| _DistortionFlowTexture | 4408 | Color effects UV | companion | Member | _DistortionFlowTextureUV | _DistortionFlowTexture_ST | _DistortionFlowTexturePan | none | Fresh default is unassigned. Gate USER_LUT. Distortion writes slot 7, so no moved texture may select 7 under this keyword. | 19681 | yes |
| _DistortionFlowTexture1 | 4411 | Color effects UV | companion | Member | _DistortionFlowTexture1UV | _DistortionFlowTexture1_ST | _DistortionFlowTexture1Pan | none | Same rules. | 19687 | yes |
| _HeightMap | 4453 | Color effects UV | companion | Member | _HeightMapUV | _HeightMap_ST | _HeightMapPan | none | Marched at the parallax coordinates with explicit gradients. Never moves. _ParallaxUV and _HeightMapUV must not be 0. | 20017 | yes |
| _Heightmask | 4457 | Color effects UV | companion | sampler_linear_repeat | _HeightmaskUV | _Heightmask_ST | _HeightmaskPan | none | The pan joins the coordinate before the sample, and the sampler macro pan argument is zero. _HeightmaskUV must not be 0, or the mask stays unassigned. | 19832 | yes |
| _PPMask | 4495 | Color effects UV | companion | Member | _PPMaskUV | _PPMask_ST | _PPMaskPan | none | Gate POSTPROCESS. The selector property sits at 4499. | 28208 | yes |

Class counts on 2026-11-23, over the 138 rows that cover the 139 declaration lines: member 1, member copy 0, neutral 17, companion 107, refused feature 13. Unique texture names 138. The commented duplicate declaration of `_VertexGlitchMap` at 3830 shares the row of the active declaration at 3836. The commented `_Udon_VideoTex` at 242 has its own refused row. The companion class covers every texture with a route entry. The route decision between `Uv0` and `OtherCoordinates` follows the selector at capture time.

### 4.4 Sampler verification summary

Every companion row and the member row carries a verified sampler read at the sample lines of the table, re-read from the base pass on 2026-11-23. Observed counts over the 107 companion rows: Member 83, `sampler_trilinear_repeat` 12, `sampler_linear_repeat` 9, `sampler_point_repeat` 1, `sampler_point_clamp` 1, Own 1. The one Own companion is `_VideoPixelTexture`, which never moves. The member row samples through `sampler_MainTex`.

Neutral rows carry their own verified samplers: `sampler_linear_clamp` 4, Own 6, `sampler_trilinear_repeat` 4, `sampler_linear_repeat` 2, Member 1. Refused rows: Member 6, Own 5, `sampler_linear_clamp` 1, no sample 1. One companion row uses a clamp sampler. `_UzumoreMask` reads `sampler_point_clamp` and refuses at selector 0. The other clamp reads sit on neutral rows and on the refused `_GlitterTexture`. The route builder reads every sampler from this table. A wrong sampler kind is a false positive.

## 5. Conditional rules

The table level required value function checks these rules. Each failure returns the first failing sentence. Every property named here exists on both bundled rows with the declared type. The main color rule carries over from `PoiyomiMainColor`.

| rule | keywords | properties read | declared types | safe state |
|---|---|---|---|---|
| Main transform | none | _MainTex_ST, _MainTexPan | Vector, Vector2 | Transform identity (1,1,0,0) and pan zero |
| Main selector and mode | none | _MainTexUV, _MainPixelMode, _MainTexStochastic | Int, Float, Float | 0, 0, 0 |
| Sharpened alpha to coverage | none | _AlphaSharpenedA2C, _AlphaToCoverage | Float, Float | First 0 or second 0 |
| Renderer blocker | none | _RenderingAOBlockerEnabled | Float | 0 |
| Light map mode | PROP_LIGHTDATASDFMAP | _LightingMapMode | Int | Not 4 |
| Texture ramp row pick | _LIGHTINGMODE_TEXTURERAMP | _ToonRampCount, _ToonRampUVSelector | Int, Int | Count at most 1, or selector not 0 |
| Multilayer border map LOD | _LIGHTINGMODE_MULTILAYER_MATH | _ShadowBorderMaskLOD, _ShadowBorderMapToggle | Range, Float | LOD 0. Toggle 1 turns the sample on, and the companion route then applies |
| SDF shading flip | _LIGHTINGMODE_SDF | _SDFShadingTextureUV | Int | Not 0 when the texture is assigned. The texture never moves |
| Matcap blend, slot 0 | POI_MATCAP0 | _MatcapUVToBlend, _MatCapBlendUV1 | Int, Vector | Selector not 0, 6, or 7, or blend vector zero |
| Matcap blend, slot 1 | COLOR_GRADING_HDR_3D | _Matcap2UVToBlend, _MatCap2ndBlendUV1 | Int, Vector | Same |
| Matcap blend, slot 2 | POI_MATCAP2 | _Matcap3UVToBlend, _MatCap3rdBlendUV1 | Int, Vector | Same |
| Matcap blend, slot 3 | POI_MATCAP3 | _Matcap4UVToBlend, _MatCap4thBlendUV1 | Int, Vector | Same |
| Emission center out, slot 0 | _EMISSION | _EmissionCenterOutEnabled | Float | 0 when the map is assigned on selector 0 |
| Emission center out, slots 1 to 3 | POI_EMISSION_1 to POI_EMISSION_3 | _EmissionCenterOutEnabled1, 2, 3 | Float | Same |
| Emission scroll curve, slots 0 to 3 | _EMISSION and POI_EMISSION_1 to 3 | _EmissionScrollingUseCurve, 1, 2, 3 | Float | 0 or the curve texture unassigned |
| Global mask split, slots 0 to 3 | POI_GLOBALMASK_TEXTURES | _GlobalMaskTexture0Split to 3Split, the 12 _SplitTilingOffset_G, _B, _A vectors, the 12 _SplitPan_G, _B, _A vectors | Int, Vector, Vector2 | Split 0, or every split transform identity and every split pan zero |
| Distortion target | USER_LUT | _DistortionUvToDistort | Int | 1, 2, or 3. Never 0 |
| Dissolve UV tile | DISTORT | _UVTileDissolveEnabled, _UVTileDissolveUV | Float, Int | Enabled 0, or selector not 0 |
| Text overlay selectors | EFFECT_BUMP | _TextFPSUV, _TextPositionUV, _TextTimeUV, _TextNumericUV | Int | Every enabled sub-overlay selector not 0 |
| Parallax channels | POI_PARALLAX | _ParallaxUV, _HeightMapUV, _HeightmaskUV | Int, Int, Int | All three not 0. Defaults refuse |
| Truchet channel | POI_TRUCHET | _TruchetUV | Int | Not 0. Default refuses |
| Voronoi space | POI_VORONOI | _VoronoiSpace | Int | Not 2 |
| UV tile discard grids | POI_UDIMDISCARD | _UDIMDiscardMode, _UDIMDiscardUV, the 16 _UDIMDiscardRow vectors, _EnableUDIMFaceDiscardOptions, _UDIMFaceDiscardUV, the 16 _UDIMFaceDiscardRow vectors | Int, Int, Float, Float, Int, Float | Per grid, all row flags 0, or the grid selector not 0 |
| Pathing source | POI_PATHING | _PathSource, _PathingUVSelect | Float, Int | Source not 1, or selector not 0 |
| Uzumore channel | POI_UZUMORE | _UzumoreMaskUV | Float | Not 0. Default 3 holds |
| Video modes | POI_VIDEO_EFFECTS | _VideoPixelateToResolution, _VideoType, _VideoPixelTextureUV | Float, Int, Int | Pixelate 0, type not 2, pixel texture selector not 0 when assigned |
| AudioLink UV reads | POI_AUDIOLINK | _AudioLinkCCStripY, the uniform flags _DebugWaveform, _DebugDFT, _DebugCCStrip, _DebugCCLights, _DebugAutocorrelator | Float, uniforms without Properties entries | All 0. Defaults hold |
| AL decal channel | POI_AL_DECAL | _ALDecalUV | Int | Not 0. Default refuses |
| AL volume color channel | POI_AL_VOLUMECOLOR | _ALVolumeColorUV, _ALVolumeColorDirection | Int, Int | Selector not 0. Default refuses |
| Vertex spectrum | AUTO_EXPOSURE | _VertexSpectrumMotion, _VertexSpectrumUV, _VertexSpectrumUVDirection | Float, Int, Int | Motion 0, or the selector not 0 |
| Vertex wind | AUTO_EXPOSURE | _VertexWindEnabled | Float | 0 when _VertexBasicsMask is a companion page |

Notes on the conditional rules:

- The audio debug flags are shader uniforms without Properties block entries, declared at 5638 to 5651. A material can carry saved values for them. The route reads them as floats. A missing name reads 0, which is the safe state.
- `_DepthTextureUV` admits 0 and 8. Value 8 is a depth gradient coordinate, not a mesh UV.
- The coordinate rule of the spec applies to every companion selector value. Selectors 6 and 7 refuse, every value above 9 and every NaN refuse with `RequiredValueNotHeld`.
- The stochastic rule is per texture: every companion route with a stochastic property requires that flag 0 on selector 0. The `_STOCHASTICMODE_*` keywords stay Conditional because their mode macros move coordinates only when a flag is 1.

## 6. Dead value families

One row per keyword. The properties listed here are read only by the code of that keyword. When the keyword is off, these values are dead and leave the material key. Task 3 turns each row into an `AtlasDeadValueFamily` with `IsOff` reading the keyword. The words `family` and `group` mean every property name that starts with the named prefix in that section of the property block.

| keyword | dead properties |
|---|---|
| PROP_LIGHTINGAOMAPS | _LightDataAOStrengthR to _LightDataAOStrengthA, _LightDataAOGlobalMaskR, _LightDataAOGlobalMaskBlendTypeR |
| PROP_LIGHTINGDETAILSHADOWMAPS | _LightingDetailShadowStrengthR to _LightingDetailShadowStrengthA, _LightingAddDetailShadowStrengthR to _LightingAddDetailShadowStrengthA, _LightDataDetailShadowGlobalMaskR, _LightDataDetailShadowGlobalMaskBlendTypeR |
| PROP_LIGHTINGSHADOWMASKS | _LightingShadowMaskStrengthR to _LightingShadowMaskStrengthA, _LightDataShadowMaskGlobalMaskR, _LightDataShadowMaskGlobalMaskBlendTypeR |
| PROP_LIGHTDATASDFMAP | _LightDataSDFMapLOD, _LightDataSDFBlendY, the map transform properties |
| COLOR_GRADING_HDR | _MainColorAdjustTexture and its pan, transform, selector, _Saturation, _MainBrightness, _MainGamma, _MainHueShift family, the four main color global mask blend properties |
| PROP_MAINGRADATIONTEX | _MainGradationTex, the texture effect of _MainGradationStrength |
| POI_GLOBALMASK_TEXTURES | _GlobalMaskTexture0 to 3 sections, 3942 to 4001, including split and pan properties |
| USER_LUT | _Distortion family, _DistortionUvToDistort, the audio link distortion strength pairs |
| POI_UDIMDISCARD | _UDIMDiscardMode, _UDIMDiscardUV, _UDIMDiscardRow0_0 to Row3_3, _EnableUDIMFaceDiscardOptions, _UDIMFaceDiscardFace, _UDIMFaceDiscardUV, _UDIMFaceDiscardRow0_0 to Row3_3 |
| POI_PARALLAX | _HeightMap family, _Heightmask family, _HeightStrength, _HeightOffset, _HeightSteps family, _Parallax quality and advanced values, _CurvatureU, _CurvatureV, _CurvFix, _ParallaxRelaxedCone, _ParallaxConeRatio, _ParallaxAnisotropic, _ParallaxAnisotropyScale, _ParallaxMipBias, _ParallaxBinarySteps |
| POI_AUDIOLINK | _AudioLinkDelay, _AudioLinkAnimToggle, _AudioLinkSmoothing family, the audio debug flags, _AudioLinkCCStripY, the band override sliders |
| POI_BLACKLIGHTMASKING | _BlackLightMasking0 to 3 Key, Range, GlobalMaskIndex, and GlobalMaskBlendType sets |
| FINALPASS | _DetailTex family, _DetailMask family, _DetailNormalMap family, _DetailTint family, _DetailBrightness, _DetailTexIntensity, the detail global mask properties |
| AUTO_EXPOSURE | _VertexBasicsMask family, _VertexManipulation family, _VertexRounding family, _VertexBarrel family, _VertexSphere family, _VertexTornado family, _VertexWind family, _VertexSpectrum family, the vertex audio link properties |
| POI_VERTEX_LOOKAT | _LookAtMask family, _VertexLookAt family, _LookAtVisibility family, _LookAtRed, Green, Blue, Alpha groups |
| POI_VERTEX_GLITCHING | _VertexGlitchThreshold, _VertexGlitchFrequency, _VertexGlitchStrength, _VertexGlitchDensity, _VertexGlitchMirror family, _VertexGlitchingAudioLink family |
| POI_VERTEX_GLITCHING_TEXTURE | _VertexGlitchMap family, _VertexGlitchMapPanSpeed |
| POI_DEPTHBULGE | _DepthBulgeFadeLength, _DepthBulgeHeight, _DepthBulgeMask family |
| POI_BACKFACE | _BackFaceColor, _BackFaceColorThemeIndex, _BackFaceDetailIntensity, _BackFaceTexture family, _BackFaceMask family, _BackFaceHueShift family, _BackFaceShiftColorSpace, _BackFaceHueSelectOrShift, _BackFaceEmissionStrength, _BackFaceEmissionLimiter, _BackFaceReplaceAlpha, _BackFacePanning |
| VIGNETTE | _RGBMask and its pan, transform, selector, _RGBMaskType, the _Rgb mask channel family, the channel invert and emission settings |
| GEOM_TYPE_MESH | none. The keyword gates no code |
| PROP_RGBMASK | _RGBMask, its pan, transform, selector, and the mask channel properties of the RGB mask section |
| PROP_RGBAMETALLICMAPS | The per channel metallic enable, UV, scale offset, mask scale tiling, pan, stochastic, and invert properties |
| PROP_RGBASMOOTHNESSMAPS | The per channel smoothness enable, UV, scale offset, mask scale tiling, pan, stochastic, and invert properties |
| PROP_REDTEXTURE | _RedTexture, its pan, transform, selector, stochastic flag, _RedAlphaAdd, the red channel invert and emission settings |
| PROP_RGBNORMALR | _RgbNormalR, its scale, pan, transform, selector, stochastic flag, the red normal blend source vector and mask channel properties |
| PROP_GREENTEXTURE | _GreenTexture family as for red, with the _RGBAGreenScaleOffset merge |
| PROP_RGBNORMALG | _RgbNormalG family as for red |
| PROP_BLUETEXTURE | _BlueTexture family as for red, with the _RGBABlueScaleOffset merge |
| PROP_RGBNORMALB | _RgbNormalB family as for red |
| PROP_ALPHATEXTURE | _AlphaTexture family as for red, with the _RGBAAlphaScaleOffset merge |
| PROP_RGBNORMALA | _RgbNormalA family as for red |
| POI_LTCGI | _LTCGI_DiffuseColor and theme index, _LTCGI_SpecularColor and theme index, _LTCGI_Smoothness, _LTCGI_Metallic, _LTCGI_UsePBR, _LTCGI_UseEO, _LTCGI_AnimToggle |
| VIGNETTE_MASKED | _ShadingEnabled and every shading module property the lighting modes do not own |
| _LIGHTINGMODE_TEXTURERAMP | _ShadowOffset, _ToonRampCount, _ToonRampUVSelector |
| _LIGHTINGMODE_MULTILAYER_MATH | The multilayer math uniform family, 12898 to 12967, including _ShadowBorder, _ShadowBlur, _ShadowReceive, _Shadow2nd family, _Shadow3rd family, _ShadowAOShift, _ShadowAOShift2, _ShadowPostAO, and the mask and color settings |
| _LIGHTINGMODE_SHADEMAP | _BaseColor_Step, _BaseShade_Feather, _ShadeColor_Step, _1st2nd_Shades_Feather, _Use_BaseAs1st, _Use_1stAs2nd, _ShadingShadeMapBlendType, _Use_1stShadeMapAlpha_As_ShadowMask, _1stShadeMapMask_Inverse, _Use_2ndShadeMapAlpha_As_ShadowMask, _2ndShadeMapMask_Inverse, the shade map transforms |
| _LIGHTINGMODE_REALISTIC | _PPLightingMultiplier, _PPLightingAddition |
| _LIGHTINGMODE_WRAPPED | _LightingWrappedWrap, _LightingWrappedNormalization, _LightingWrappedColor, the wrapped gradient properties |
| _LIGHTINGMODE_SKIN | _SssScale, _SkinThicknessPower, _SkinThicknessMapInvert, _SssBumpBlur, _SssTransmissionAbsorption, _SssColorBleedAoWeights, the map transforms |
| _LIGHTINGMODE_FLAT | _ForceFlatRampedLightmap |
| _LIGHTINGMODE_CLOTH | _ClothLerp, _ClothMetallic, _ClothReflectance, _ClothSmoothness, the map transform and invert properties |
| _LIGHTINGMODE_SDF | The _SDFShadingTexture transforms, _SDFBlur, _SDFForward, _SDFLeft |
| GEOM_TYPE_BRANCH | The decal slot 0 family: _DecalTexture, _DecalColor, _DecalType, _DecalBlendType, _DecalPosition, _DecalRotation, _DecalScale, _DecalMirror family, the _Decal0 group |
| GEOM_TYPE_BRANCH_DETAIL | The decal slot 1 family: _DecalTexture1, _DecalColor1, the _Decal1 group |
| GEOM_TYPE_FROND | The decal slot 2 family: _DecalTexture2, _DecalColor2, the _Decal2 group |
| DEPTH_OF_FIELD_COC_VIEW | The decal slot 3 family: _DecalTexture3, _DecalColor3, the _Decal3 group |
| PROP_DECALMASK | _DecalMask, _DecalMaskPan, _DecalMaskUV |
| DISTORT | The _Dissolve family, _UVTileDissolve family, _SphericalDissolve family, _ContinuousDissolve |
| POI_ANISOTROPICS | The _Aniso0 and _Aniso1 layer families, the offset map strengths, the toon mode, edge, blur, and switch direction values, the tint and theme properties, the aniso shadow mask properties |
| POI_MATCAP0 | The _Matcap and _Matcap0 families of the Matcap 0 section, 1150 to 1246, and _MatCapBlendUV1 |
| POI_MATCAP0_CUSTOM_NORMAL | _Matcap0NormalMap, _Matcap0NormalMapPan, _Matcap0NormalMapUV, _Matcap0NormalMapScale |
| COLOR_GRADING_HDR_3D | The _Matcap2 and _Matcap1 families of the Matcap 1 section, and _MatCap2ndBlendUV1 |
| POI_MATCAP1_CUSTOM_NORMAL | _Matcap1NormalMap, _Matcap1NormalMapPan, _Matcap1NormalMapUV, _Matcap1NormalMapScale |
| POI_MATCAP2 | The _Matcap3 and _Matcap2 families of the Matcap 2 section, and _MatCap3rdBlendUV1 |
| POI_MATCAP2_CUSTOM_NORMAL | _Matcap2NormalMap, _Matcap2NormalMapPan, _Matcap2NormalMapUV, _Matcap2NormalMapScale |
| POI_MATCAP3 | The _Matcap4 and _Matcap3 families of the Matcap 3 section, and _MatCap4thBlendUV1 |
| POI_MATCAP3_CUSTOM_NORMAL | _Matcap3NormalMap, _Matcap3NormalMapPan, _Matcap3NormalMapUV, _Matcap3NormalMapScale |
| _CUBEMAP | _CubeMapColor and theme index, _CubeMapIntensity, _CubeMapSmoothness, _CubeMapRotation and pan, _CubeMapUVMode, _CubeMapWorldNormalsStrength, the mask channel, invert, and global mask properties, the hue shift and color adjust properties, _CubemapBlendType, _CubeMapLightMask, _CubeMapEmissionStrength, _CubeMapBlendAmount |
| POI_AL_DECAL | _ALDecal family, _ALUVPosition, _ALUVRotation, _ALUVRotationSpeed, _ALUVScale, the _AudioLinkDecal0 to 3 groups |
| POI_AL_VOLUMECOLOR | _ALVolumeColor family, _ALLowEmission, _ALMidEmission, _ALHighEmission |
| _SUNDISK_HIGH_QUALITY | The flipbook family, 2447 to 2490: frame, scale offset, rotation, tiled, crossfade, and chronotensity properties, _FlipbookTexArray family, _FlipbookMask family |
| _EMISSION | The emission 0 section, 2520 to 2602, and the helper properties 25271 to 25300 |
| POI_EMISSION_1 | The emission 1 section families |
| POI_EMISSION_2 | The emission 2 section families |
| POI_EMISSION_3 | The emission 3 section families |
| _GLOSSYREFLECTIONS_OFF | The rim 0 section, 1566 to 1686, and the _Set_RimLightMask family |
| _RIMSTYLE_POIYOMI | _RimMask family, _RimTex family, _RimWidth, _RimSharpness, _RimPower, _RimLightingInvert, _RimPoiBlendMode, _RimBlendStrength, _RimBaseColorMix, _RimBrightness, _RimStrength, _RimBiasIntensity, _RimMaskChannel, _RimMaskInvert, the rim shadow mask values, the apply alpha and apply global mask values |
| _RIMSTYLE_UTS2 | _Set_RimLightMask family, _RimLight_Power, _RimLight_InsideMask, _RimLight_FeatherOff, _Tweak_RimLightMaskLevel, _Is_LightColor_RimLight, _LightDirection_MaskOn, _Tweak_LightDirectionMaskLevel, _Add_Antipodean_RimLight, _Is_LightColor_Ap_RimLight, _Ap_RimLightColor, _Ap_RimLight_Power, _Ap_RimLight_FeatherOff, _RimHueShift family |
| _RIMSTYLE_LILTOON | _RimColorTex family, _RimMainStrength, _RimNormalStrength, _RimDirRange, _RimIndirRange, _RimFresnelPower, _RimBackfaceMask, _RimDirStrength, _RimBorder, _RimBlur, _RimIndirBorder, _RimIndirBlur, _RimShadowMask, _RimEnableLighting, _RimVRParallaxStrength, _RimBlendMode, _RimMaskOnlyMask |
| POI_RIM2 | The rim 2 section, 1684 to 1804, and the _Set_Rim2LightMask family |
| _RIM2STYLE_POIYOMI | _Rim2Mask family, _Rim2Tex family, and the rim 2 Poiyomi style values |
| _RIM2STYLE_UTS2 | _Set_Rim2LightMask family and the rim 2 UTS values |
| _RIM2STYLE_LILTOON | _Rim2ColorTex family and the rim 2 lilToon values |
| _POI_DEPTH_RIMLIGHT | _DepthRimColor, _DepthRimColorThemeIndex, _DepthRimBrightness, _DepthRimWidth, _DepthRimSharpness, _DepthRimType, _DepthRimNormalToUse, _DepthRimHideInShadow, _DepthRimEmission, _DepthRimReplace, _DepthRimAdd, _DepthRimMultiply, _DepthRimAdditiveLighting, _DepthRimMixBaseColor, _DepthRimMixLightColor |
| _SUNDISK_SIMPLE | The glitter family: layers, shape, size, frequency, random rotation, texture rotation, audio link properties, _GlitterTexture family, _GlitterColorMap family, _GlitterMask family, and the texture transforms |
| POI_SUBSURFACESCATTERING | _SSSColor, _SSSThicknessMod, _SSSStrength, _SSSSpread, _SSSDistortion, _SSSBaseColorMix, the map transforms |
| MOCHIE_PBR | _MochieRoughnessMultiplier and its second form, _MochieMetallicMultiplier, the tint and theme properties, the four invert flags, _PBRNormalSelect, the GSAA properties, the TPS properties, _MochieReflCube and its HDR, the fallback properties, the global mask properties |
| GGX_ANISOTROPICS | _Anisotropy, _ReflectionAnisotropicStretch, _RoughnessAnisotropy, the map transforms |
| POI_CLEARCOAT | _ClearCoatStrength, _ClearCoatSmoothness, the four map channel selectors, the four strength properties, the four invert flags, the _ClearCoatBRDF fallbacks, the tint and theme properties, the TPS mask properties, the GSAA properties, the global mask properties |
| POI_ENVIRORIM | _RimEnviroSharpness, _RimEnviroWidth, _RimEnviroBlur, _RimEnviroMinBrightness, _RimEnviroChannel, _RimEnviroIntensity, the mask transforms |
| POI_STYLIZED_StylizedSpecular | _Smoothness, _Metallic, _Reflectance, _UseReflection, _ApplySpecular family, and the _Stylized properties of the section |
| _STYLIZEDREFLECTIONMODE_UNITYCHAN | The UnityChan specular body values: the _HighColor family and the _Set_HighColorMask family |
| _STYLIZEDREFLECTIONMODE_LILTOON | The lilToon reflection body values: the _SmoothnessTex and _ReflectionColorTex settings and the reflection properties |
| POI_PATHING | _PathingMap family, _PathingColorMap family, _PathingUVSelect, _PathPointSampling, _PathSource family, _PathRemap family, _PathGapLengths, _PathType family, _PathGradientType, _PathWidth, _PathTime, _PathOffset, _PathSpeed, _PathColor family, _PathEmissionStrength, _PathSoftness, _PathSegments, _PathSurfaceBlendMode, the _Path theme index properties, the _PathAL and _AudioLinkPath groups |
| POI_MIRROR | _VisibilityMode, the _VisibilityVRC properties, _MirrorColor, _MirrorColorThemeIndex |
| POI_MIRROR_TEXTURE | _MirrorTexture family, _MirrorTextureBlendType, _MirrorTextureForceEnabled |
| GRAIN | _DepthMask family, _DepthTexture family, _DepthColor family, _DepthAlpha family, _DepthEmissionStrength, the depth blend modes |
| EFFECT_BUMP | _TextGlyphs, _TextPixelRange, and every _Text property of the four sub-overlays |
| POSTPROCESS | _PPMask and its pan, transform, selector, channel, invert, _PPHue family, _PPTint, _PPRGB, _PPContrast, _PPSaturation, _PPBrightness, _PPLightness, _PPHDR, _PPPosterization family |
| PROP_PPMASK | _PPMask, its pan, transform, and selector |
| POI_INTERNALPARALLAX | The _ParallaxInternal family: map, mask, iterations, depth range, fades, colors, blend modes, and the hue shift group |
| POI_NORMALCORRECT | _NormalCorrectAmount, _NormalCorrectOrigin |
| POI_VIDEO_EFFECTS | _VideoPixelTexture family, _VideoMaskTexture family, _VideoType, _VideoResolution, _VideoGameboyRamp, _VideoBacklight, _VideoCRT family, _VideoRepeatVideoTexture, _VideoPixelateToResolution, _VideoMaskPanning, _VideoSaturation, _VideoContrast, _VideoEmissionEnabled |
| POI_BACKLIGHT | _BacklightColor, _BacklightMainStrength, _BacklightNormalStrength, _BacklightBorder, _BacklightBlur, _BacklightDirectivity, _BacklightViewStrength, _BacklightReceiveShadow, _BacklightBackfaceMask, the texture transforms |
| POIBS_ENABLE | _CustomColors and the Beat Saber color behavior properties |
| POIBS_BLOOMFOG | _FogStartOffset, _FogScale, and the bloomfog state |
| BSSBLOOMFOGTYPE_HEIGHT | _FogHeightOffset, _FogHeightScale |
| _STOCHASTICMODE_DELIOT_HEITZ | _StochasticDeliotHeitzDensity |
| _STOCHASTICMODE_HEXTILE | The _StochasticHex family |
| _STOCHASTICMODE_NONE | none. The no-op macros compile no stochastic code |
| POI_VORONOI | The _Voronoi family: colors, emission strengths, scale, speed, gradient, power, blend, the random cell group, the audio link modulators, and the _AudioLinkVoronoi family |
| POI_TRUCHET | _TruchetUV, _TruchetDensity, _TruchetThiccness, _TruchetColor family, _TruchetTex family, _TruchetPanOffset, _TruchetAlpha, _TruchetEdgeSharpness, _TruchetEmissionStrength, _TruchetHideWhenNoAL, _TruchetMask family, _TruchetBlendType, _TruchetOverrideAlpha, the _TruchetAL and _TruchetChronoPan families |
| POI_UZUMORE | _UzumoreEnabled, _UzumoreAmount, _UzumoreBias, _UzumoreMask, _UzumoreMaskUV |

The textures of a keyword join the dead family when the keyword is off. A texture the material does not assign is inert for its own transform, pan, selector, and stochastic properties. The route reads them only while the texture is assigned.

## 7. The Two Pass finding

Finding of the plan, dated 2026-11-23. The bundled `Poiyomi Toon Two Pass.shader` differs from the bundled `Poiyomi Toon.shader` in three ways. It adds a second ForwardBase and ForwardAdd pass pair that copies the base pair under the `SecondPassRunning` define. It adds scalar two-pass, blend, and stencil properties. It adds two global mask blocks under `SecondPassRunning`. It adds no texture property and no keyword. Both files declare the same 106 keywords and the same 139 texture typed declarations. The color only difference reads `_TwoPassColor` instead of `_Color` when `_TwoPassOverrideFirstPassColor` is on. No extra reachable UV read exists in the Two Pass file. Every verdict, condition, and route of this record applies to both bundled rows unchanged.

## 8. Falsifiers

Each item is a state a plausible wrong implementation admits or refuses wrongly. A test per item protects the record.

1. A stale second lighting mode keyword on the material, for example `_LIGHTINGMODE_SHADEMAP` next to `_LIGHTINGMODE_MULTILAYER_MATH`. The route applies the conditions of every enabled keyword and stays exact. The page material copies the keywords verbatim.
2. Selector 6 on a companion. Refuses with `RequiredValueNotHeld`.
3. Selector 7 on a companion. Refuses with `RequiredValueNotHeld`.
4. A non-identity companion transform, including every split and merged transform. Refuses with `NonIdentityTransform`.
5. A non-zero companion pan. Refuses with `NonIdentityTransform`.
6. A stochastic flag of 1 on a moving texture. Refuses with `RequiredValueNotHeld`.
7. `_LightingMapMode` 4. The light data map sample would read UV0 derivatives. Refuses.
8. `_ToonRampCount` above 1 with `_ToonRampUVSelector` 0. Refuses.
9. `_MatcapUVToBlend` 0 with a non-zero `_MatCapBlendUV1`. Refuses. Same per slot.
10. `_EmissionCenterOutEnabled` 1 with an assigned map on selector 0. Refuses.
11. `_EmissionScrollingUseCurve` 1 with an assigned curve texture. Refuses.
12. `_DistortionUvToDistort` 0. Refuses.
13. `_UVTileDissolveEnabled` 1 with `_UVTileDissolveUV` 0. Refuses.
14. Any of the four text selectors at 0. Refuses.
15. `_ParallaxUV` 0, `_HeightMapUV` 0, or `_HeightmaskUV` 0 with the texture assigned. Refuses.
16. `_TruchetUV` 0. Refuses.
17. `_VoronoiSpace` 2. Refuses.
18. A UV tile discard grid with a row flag set and its selector 0. Refuses.
19. `_PathSource` 1 with `_PathingUVSelect` 0. Refuses.
20. `_UzumoreMaskUV` 0. Refuses, because the clamp sampler cannot serve a page.
21. `_VideoPixelateToResolution` 1, `_VideoType` 2, or an assigned `_VideoPixelTexture` with selector 0. Refuses.
22. Any audio UV debug flag or `_AudioLinkCCStripY` at 1. Refuses.
23. `_ALDecalUV` 0 or `_ALVolumeColorUV` 0. Refuses.
24. Each refused keyword of section 4.1. Refuses with `KeywordNotAllowed`.
25. Each engine keyword `FOG_EXP2` or `VERTEXLIGHT_ON`. Refuses as today.
26. `_SDFShadingTexture` assigned on selector 0 with `_LIGHTINGMODE_SDF`. Refuses.
27. A NaN or out of range companion selector. Refuses with `RequiredValueNotHeld`.

## 9. Limits

- The review covers the bundled 9.3 rows only. The release 9.3.64 rows keep `PoiyomiMainColor` until a review reads their source.
- Line numbers name the installed bundled files of 2026-11-23. A vendor update can move lines. The completeness tests of the plan pin the keyword and texture sets against the installed source at test time.
- The dead value families are keyword gated only. A feature gated by a float switch with no keyword stays live. This matches the first cut of the live value model.
- `_Udon_VideoTex` has no active declaration. The decal code reads it only under refused keywords. A future shader that declares it needs a new review.
- The record classes the four matcap textures and the three ramp and lookup textures as neutrals because their sample coordinates never track UV0. The matcap blend rule guards the one UV read in the matcap path. The texture ramp rule guards the row pick.
- The audit of the four feature groups derives from the base pass. The other passes repeat the same code. The Add pass adds additive shadow strengths, the outline pass gates shading behind `_OutlineLit`, and the shadow caster skips lighting, rim, matcap, decal, emission, and LTCGI sections. No pass adds a UV0 read that the base pass lacks.
