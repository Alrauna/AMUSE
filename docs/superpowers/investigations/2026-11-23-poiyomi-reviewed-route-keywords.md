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

The dead family gate walk of section 6 ran on 2026-11-23. A script walked every conditional compilation directive of both bundled files and recorded, for each code line, the stack of enclosing conditions. An else branch carries the negated condition of its if branch. An elif branch carries its own condition. The property universe held 3113 names: every Properties block entry, every Unity managed transform entry of the form `<texture>_ST`, and the audio debug uniforms of section 5. A property entered the family of a keyword only when every one of its read lines in both files carries a stack that requires that keyword. A stack requires the keyword when one condition names it in an and form without an or and without a negation of the keyword. A stack that names the keyword only inside an or form, or in a negated form, or that misses the keyword, rejects the property. Pass defines such as `POI_PASS_BASE` are neutral facts and never gate a family. The corrected walk kept 3183 keyword property memberships over the 104 families and compressed them to the prefix rows of section 6. The first walk of 2026-11-23 did not negate else branches and wrongly kept four memberships. The correction removed `_FogScale` and `_FogStartOffset` from `BSSBLOOMFOGTYPE_HEIGHT` and `_VertexGlitchStrength` and `_VertexGlitchThreshold` from `POI_AUDIOLINK`. No other membership changed. Where a session report claimed a property that the gate walk rejects, this record drops the property. Section 3 item 14 lists the notable removals.

## 3. Corrections against the reports

1. `KEYWORD` is not a declared keyword of the base pass. Its pragma is a comment at 9995. The reports and the spec list it under KeyOnly. The keyword space holds 104 local keywords without it. This record carries no row for it. `GEOM_TYPE_MESH` is declared, at 10054.
2. `PROP_BUMPMAP`, `PROP_ALPHAMASK`, `PROP_MAINCOLORADJUSTTEXTURE`, `PROP_DETAILTEX`, `PROP_DETAILMASK`, `PROP_DETAILNORMALMAP`, `PROP_MULTILAYERMATHBLURMAP`, `PROP_SHADOWCOLORTEX`, `PROP_SHADOW2NDCOLORTEX`, `PROP_SHADOW3RDCOLORTEX`, `PROP_SHADOWSTRENGTHMASK`, and `PROP_SHADOWBORDERMASK` are not declared keywords. Their branches test the presence of the texture keyword or the absence of the optimizer, so the branch compiles in every shipped variant. Texture assignment is the real gate. The reports name some of them as sample gates. This record carries no rows for them. The declared texture keywords of the base pass are the 18 `PROP_` rows of section 4.
3. Sample line corrections, report value then verified value: `_AlphaMask` 29860 to 29877. `_LightingAOMaps` 29260 to 29257. `_MainColorAdjustTexture` 29828 to 29807. `_PPMask` 28224 to 28208. `_RimColorTex` 30300 to 30338. `_Rim2Mask` 30103 to 30084. `_Rim2Tex` 30112 to 30098. `_Set_RimLightMask` 30073 to 30071. `_Set_Rim2LightMask` 30129 to 30120. `_Rim2ColorTex` 30321 to 30350. `_RimEnviroMask` 26997 to 27004. `_AnisotropyMap` 26715 to 26733. `_VoronoiMask` 28733 to 28783. `_VoronoiNoise` 28744 to 28794. `_TruchetTex` 28977 to 28966. `_TruchetMask` 28911 to 28910. `_Heightmask` 19848 to 19832. `_HeightMap` 19856 to the call at 20017. `_ALDecalColorMask` 24746 to 24747. `_PathingColorMap` 27440 to 27438. `_ParallaxInternalMap` 28237 to 28236. `_ParallaxInternalMapMask` 28269 to 28270. `_LightDataSDFMap` 29577 to 29562 and 29566. `_CubeMapMask` 24705 to 24707.
4. `_ShadowBorderMask` has two branches with two samplers. The LOD branch at 22375 uses `sampler_trilinear_repeat` with explicit derivatives. The plain branch at 22379 uses `sampler_linear_repeat`. The route must model both. The condition `_ShadowBorderMaskLOD` is 0 keeps the derivative branch off.
5. `_PathingMap` has two branches. `_PathPointSampling` 1 reads `sampler_point_repeat` at 27392. `_PathPointSampling` 0 reads `sampler_linear_repeat` at 27396. The rule of section 5 requires `_PathPointSampling` 1 when the texture moves on selector 0, so the admitted mover carries the point repeat sampler. A use on another selector keeps the texture bound and needs no pin.
6. `_UzumoreMask` reads `sampler_point_clamp` at 18081. It is the clamp case of the spec. Its property `_UzumoreMaskUV` is a Float, and the same value selects the sampled channel and the mesh UV channel. Default 3 reads UV3 and channel A.
7. The vertex basics selector property has no leading underscore: it is `VertexBasicsMaskUV`, declared at 3571. The pan property is `_VertexBasicsMaskUVPan`, declared at 3570. The reports wrote mixed forms.
8. `_EmissionScrollingCurve1` to `_EmissionScrollingCurve3` sample through the uniform names `_EmissionScrollingCurve_ST__1` to `_EmissionScrollingCurve_ST__3`, declared at 14123 to 14265. No material property of those names exists. These textures never move, so the route needs no transform property for them.
9. The matcap textures sample through `sampler_trilinear_repeat` with an explicit LOD, at 24233, 24352, 24466, 24580. The reports said sampler state sharing for the masks only. The matcap textures themselves stay unmoved neutrals.
10. `_OutlineMask` samples `sampler_linear_repeat` in the vertex stage at 18637. `_OutlineTexture` samples through `sampler_MainTex` at 54423 in the outline pass. The reports named neither sampler.
11. The base pass declares 106 pragma keywords: 104 local shader feature keywords and the 2 engine keywords. This matches the spec and the plan.
12. The texture typed declarations number 139 lines per bundled file. Two are comments: `_Udon_VideoTex` at 242 and a duplicate `_VertexGlitchMap` at 3830 next to the active declaration at 3836. Unique names number 138. The Two Pass file carries the same 139 declarations and the same 106 keywords. The texture table below carries one row per unique name, 138 rows. The two rows for the commented lines are the refused `_Udon_VideoTex` row and the shared `_VertexGlitchMap` row that names both lines.
13. `_MetallicGlossMap` is dead code. Its only sample site sits behind a never defined test at 27315. The record classes it neutral. `_VideoGameboyRamp` samples a brightness scalar at 28394, never a mesh UV. The record classes it neutral. Both facts are additions to the spec neutral list, which names 15 of the 17 neutrals.
14. The gate walk of section 6 removed report claims that the gates reject. The three global mask pairs of the lighting texture keywords stay live, because their reads sit outside the texture gates at 29263 to 29266, 29280 to 29283, and 29292 to 29295. `_MainGradationStrength` stays live, because both its reads at 29849 and 29866 sit outside the gradation texture gate. The shared RGBA values `_RGBARedScaleOffset`, the red enable, and the channel emission and blend settings stay live, because their reads sit outside the texture keyword gates. `_PPLightingMultiplier` and `_PPLightingAddition` stay live, because a copy of their reads at 29621 sits outside the realistic mode gate. `_CustomColors` has no code read at all and stays keyed. The RGB mask channel properties and the `_Rgb*AlphaAdd` and blend settings stay live for the same reason.

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
| POI_MATCAP0_CUSTOM_NORMAL | Matcap rim emission masks | Companion | pragma 10107, toggle 1198 |
| COLOR_GRADING_HDR_3D | Matcap rim emission masks | Conditional | pragma 10110, toggle 1250 |
| POI_MATCAP1_CUSTOM_NORMAL | Matcap rim emission masks | Companion | pragma 10111, toggle 1290 |
| POI_MATCAP2 | Matcap rim emission masks | Conditional | pragma 10114, toggle 1342 |
| POI_MATCAP2_CUSTOM_NORMAL | Matcap rim emission masks | Companion | pragma 10115, toggle 1382 |
| POI_MATCAP3 | Matcap rim emission masks | Conditional | pragma 10118, toggle 1434 |
| POI_MATCAP3_CUSTOM_NORMAL | Matcap rim emission masks | Companion | pragma 10119, toggle 1474 |
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
| _ShadowBorderMask | 995 | Lighting | companion | sampler_linear_repeat | _ShadowBorderMaskUV | _ShadowBorderMask_ST | _ShadowBorderMaskPan | none | The plain branch samples `sampler_linear_repeat` at 22379 and is the admitted branch. The LOD branch at 22375 samples `sampler_trilinear_repeat` with explicit derivatives and refuses through the rule `_ShadowBorderMaskLOD` 0. `_ShadowBorderMapToggle` 1 turns the sample on. | 22375, 22379 | yes |
| _MultilayerMathBlurMap | 1004 | Lighting | companion | Member | _MultilayerMathBlurMapUV | _MultilayerMathBlurMap_ST | _MultilayerMathBlurMapPan | none | Always sampled while _LIGHTINGMODE_MULTILAYER_MATH is on. The selector property sits at 947. | 22321 | yes |
| _1st_ShadeMap | 1022 | Lighting | companion | Member | _1st_ShadeMapUV | _1st_ShadeMap_ST | _1st_ShadeMapPan | none | None. | 22050 | yes |
| _2nd_ShadeMap | 1029 | Lighting | companion | Member | _2nd_ShadeMapUV | _2nd_ShadeMap_ST | _2nd_ShadeMapPan | none | None. | 22057 | yes |
| _SkinLUT | 1044 | KeyOnly | neutral | sampler_linear_clamp | none | none | none | none | Sampled at a lighting lookup coordinate, never at a mesh UV. | 22641 | yes |
| _SkinThicknessMap | 1046 | Lighting | companion | Member | _SkinThicknessMapUV | _SkinThicknessMap_ST | _SkinThicknessMapPan | none | None. | 22613 | yes |
| _ClothDFG | 1063 | KeyOnly | neutral | Own | none | none | none | none | Sampled at a view and roughness coordinate. | 21845 | yes |
| _ClothMetallicSmoothnessMap | 1064 | Lighting | companion | Member | _ClothMetallicSmoothnessMapUV | _ClothMetallicSmoothnessMap_ST | _ClothMetallicSmoothnessMapPan | none | None. | 22525 | yes |
| _SDFShadingTexture | 1076 | Lighting | companion | Member | _SDFShadingTextureUV | _SDFShadingTexture_ST | _SDFShadingTexturePan | none | The sample multiplies the coordinate x by a light driven flip, so the texture must never move. Selector 0 refuses when _LIGHTINGMODE_SDF is on and the texture is assigned. | 22654 | yes |
| _AnisoColorMap | 1111 | Lighting | companion | Member | _AnisoColorMapUV | _AnisoColorMap_ST | _AnisoColorMapPan | none | None. | 24032 | yes |
| _Matcap | 1162 | KeyOnly | neutral | sampler_trilinear_repeat | none | _Matcap_ST | _MatcapPan | none | Matcap coordinates come from view vectors with an explicit LOD. The transform applies to the matcap coordinate, not to a mesh UV. The UV blend at 24169 refuses through the matcap rule of section 5. | 24233 | yes |
| _MatcapMask | 1175 | Matcap rim emission masks | companion | Member | _MatcapMaskUV | _MatcapMask_ST | _MatcapMaskPan | none | Second sample at 24229 for the smoothness path. | 24261 | yes |
| _Matcap0NormalMap | 1199 | Matcap rim emission masks | companion | Member | _Matcap0NormalMapUV | _Matcap0NormalMap_ST | _Matcap0NormalMapPan | none | Sampled through calculateNormal, which reads sampler_MainTex. Gate POI_MATCAP0_CUSTOM_NORMAL under POI_MATCAP0. | 24214 | yes |
| _Matcap2 | 1254 | KeyOnly | neutral | sampler_trilinear_repeat | none | _Matcap2_ST | _Matcap2Pan | none | Matcap 1 slot texture. The transform applies to the matcap coordinate. Same matcap blend rule. | 24352 | yes |
| _Matcap2Mask | 1267 | Matcap rim emission masks | companion | Member | _Matcap2MaskUV | _Matcap2Mask_ST | _Matcap2MaskPan | none | Second sample at 24348. | 24373 | yes |
| _Matcap1NormalMap | 1291 | Matcap rim emission masks | companion | Member | _Matcap1NormalMapUV | _Matcap1NormalMap_ST | _Matcap1NormalMapPan | none | Sampled through calculateNormal. | 24325 | yes |
| _Matcap3 | 1346 | KeyOnly | neutral | sampler_trilinear_repeat | none | _Matcap3_ST | _Matcap3Pan | none | Matcap 2 slot texture. The transform applies to the matcap coordinate. Same matcap blend rule. | 24466 | yes |
| _Matcap3Mask | 1359 | Matcap rim emission masks | companion | Member | _Matcap3MaskUV | _Matcap3Mask_ST | _Matcap3MaskPan | none | Second sample at 24462. | 24487 | yes |
| _Matcap2NormalMap | 1383 | Matcap rim emission masks | companion | Member | _Matcap2NormalMapUV | _Matcap2NormalMap_ST | _Matcap2NormalMapPan | none | Sampled through calculateNormal. | 24439 | yes |
| _Matcap4 | 1438 | KeyOnly | neutral | sampler_trilinear_repeat | none | _Matcap4_ST | _Matcap4Pan | none | Matcap 3 slot texture. The transform applies to the matcap coordinate. Same matcap blend rule. | 24580 | yes |
| _Matcap4Mask | 1451 | Matcap rim emission masks | companion | Member | _Matcap4MaskUV | _Matcap4Mask_ST | _Matcap4MaskPan | none | Second sample at 24576. | 24601 | yes |
| _Matcap3NormalMap | 1475 | Matcap rim emission masks | companion | Member | _Matcap3NormalMapUV | _Matcap3NormalMap_ST | _Matcap3NormalMapPan | none | Sampled through calculateNormal. | 24553 | yes |
| _CubeMap | 1530 | KeyOnly | neutral | sampler_linear_clamp | none | none | none | none | Cube sampled by view, reflection, or normal vector. | 24698 | yes |
| _CubeMapMask | 1542 | Lighting | companion | Member | _CubeMapMaskUV | _CubeMapMask_ST | _CubeMapMaskPan | none | None. | 24707 | yes |
| _Set_RimLightMask | 1571 | Matcap rim emission masks | companion | Member | _Set_RimLightMaskUV | _Set_RimLightMask_ST | _Set_RimLightMaskPan | none | Gate _RIMSTYLE_UTS2. | 30071 | yes |
| _RimMask | 1575 | Matcap rim emission masks | companion | Member | _RimMaskUV | _RimMask_ST | _RimMaskPan | none | Gate _RIMSTYLE_POIYOMI. | 30034 | yes |
| _RimTex | 1583 | Matcap rim emission masks | companion | Member | _RimTexUV | _RimTex_ST | _RimTexPan | none | Gate _RIMSTYLE_POIYOMI. | 30048 | yes |
| _RimColorTex | 1630 | Matcap rim emission masks | companion | Member | _RimColorTexUV | _RimColorTex_ST | _RimColorTexPan | none | Gate _RIMSTYLE_LILTOON. | 30338 | yes |
| _Set_Rim2LightMask | 1691 | Matcap rim emission masks | companion | Member | _Set_Rim2LightMaskUV | _Set_Rim2LightMask_ST | _Set_Rim2LightMaskPan | none | Gate _RIM2STYLE_UTS2. | 30120 | yes |
| _Rim2Mask | 1695 | Matcap rim emission masks | companion | Member | _Rim2MaskUV | _Rim2Mask_ST | _Rim2MaskPan | none | Gate _RIM2STYLE_POIYOMI. | 30084 | yes |
| _Rim2Tex | 1703 | Matcap rim emission masks | companion | Member | _Rim2TexUV | _Rim2Tex_ST | _Rim2TexPan | none | Gate _RIM2STYLE_POIYOMI. | 30098 | yes |
| _Rim2ColorTex | 1751 | Matcap rim emission masks | companion | Member | _Rim2ColorTexUV | _Rim2ColorTex_ST | _Rim2ColorTexPan | none | Gate _RIM2STYLE_LILTOON. | 30350 | yes |
| _SSSThicknessMap | 1839 | Lighting | companion | Member | _SSSThicknessMapUV | _SSSThicknessMap_ST | _SSSThicknessMapPan | none | The selector enum labels value 4 DistortedUV1, but the code reads mesh slot 4, which is the panosphere slot. Value 4 never reads UV0. | 26348 | yes |
| _MochieMetallicMaps | 1862 | Lighting | companion | Member | _MochieMetallicMapsUV | _MochieMetallicMaps_ST | _MochieMetallicMapsPan | _MochieMetallicMapsStochastic | The split sample at 26600 adds _MochieMetallicMasksUV, merged transform _PBRMaskScaleTiling, _MochieMetallicMasksPan, and _PBRSplitMaskStochastic. All must hold when the split sample runs on selector 0. | 26596, 26600 | yes |
| _MochieReflCube | 1880 | KeyOnly | neutral | Own | none | none | none | none | Cube sampled by view. The texture enters GetReflections as an argument at 26824. | 26824 | yes |
| _AnisotropyMap | 1891 | Lighting | companion | Member | _AnisotropyMapUV | _AnisotropyMap_ST | _AnisotropyMapPan | none | None. | 26733 | yes |
| _ClearCoatMaps | 1949 | Lighting | companion | Member | _ClearCoatMapsUV | _ClearCoatMaps_ST | _ClearCoatMapsPan | _ClearCoatMapsStochastic | None. | 26851 | yes |
| _ClearCoatFallback | 1962 | KeyOnly | neutral | Own | none | none | none | none | Fallback cube sampled by the reflection vector. The texture enters GetReflections as an argument at 26957. | 26957 | yes |
| _RimEnviroMask | 1997 | Lighting | companion | Member | _RimEnviroMaskUV | none | _RimEnviroMaskPan | none | No transform property exists. The sample reads the raw selected UV plus pan. | 27004 | yes |
| _HighColor_Tex | 2016 | Lighting | companion | Member | _HighColor_TexUV | _HighColor_Tex_ST | _HighColor_TexPan | none | Gate _STYLIZEDREFLECTIONMODE_UNITYCHAN body under POI_STYLIZED_StylizedSpecular. | 27020 | yes |
| _Set_HighColorMask | 2021 | Lighting | companion | Member | _Set_HighColorMaskUV | _Set_HighColorMask_ST | _Set_HighColorMaskPan | none | Same gate. | 27039 | yes |
| _SmoothnessTex | 2054 | Lighting | companion | sampler_linear_repeat | none | none | none | none | Samples raw UV0 with no transform. Gate _STYLIZEDREFLECTIONMODE_LILTOON body under POI_STYLIZED_StylizedSpecular. | 27307 | yes |
| _MetallicGlossMap | 2057 | KeyOnly | neutral | sampler_linear_repeat | none | none | none | none | Dead code. The only sample site sits behind a never defined test at 27315 and never compiles. | none | yes |
| _ReflectionColorTex | 2073 | Lighting | companion | sampler_linear_repeat | none | none | none | none | Samples raw UV0 with no transform. Same gate as _SmoothnessTex. | 27323 | yes |
| _ReflectionCubeTex | 2074 | KeyOnly | neutral | Own | none | none | none | none | Cube sampled by view and normal through lilCustomReflection. | 27354 | yes |
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
| _EmissionScrollingCurve | 2585 | Matcap rim emission masks | companion | Member | _EmissionMapUV | _EmissionScrollingCurve_ST | none | none | Never moves. The sample follows the map selector `_EmissionMapUV`. The sample offset adds a position term. _EmissionScrollingUseCurve must be 0 or the texture unassigned. | 25179 | yes |
| _EmissionMask1 | 2619 | Matcap rim emission masks | companion | Member | _EmissionMask1UV | _EmissionMask1_ST | _EmissionMask1Pan | none | Gate POI_EMISSION_1. | 25378 | yes |
| _EmissionMap1 | 2629 | Matcap rim emission masks | companion | Member | _EmissionMap1UV | _EmissionMap1_ST | _EmissionMap1Pan | none | Center-out at 25330. _EmissionCenterOutEnabled1 must be 0 when the map moves. | 25326 | yes |
| _EmissionScrollingCurve1 | 2675 | Matcap rim emission masks | companion | Member | _EmissionMap1UV | _EmissionScrollingCurve_ST__1 uniform only | none | none | Never moves. The sample follows the map selector `_EmissionMap1UV`. _EmissionScrollingUseCurve1 0 or unassigned. | 25357 | yes |
| _EmissionMask2 | 2709 | Matcap rim emission masks | companion | Member | _EmissionMask2UV | _EmissionMask2_ST | _EmissionMask2Pan | none | Gate POI_EMISSION_2. | 25556 | yes |
| _EmissionMap2 | 2719 | Matcap rim emission masks | companion | Member | _EmissionMap2UV | _EmissionMap2_ST | _EmissionMap2Pan | none | Center-out at 25508. _EmissionCenterOutEnabled2 must be 0 when the map moves. | 25504 | yes |
| _EmissionScrollingCurve2 | 2765 | Matcap rim emission masks | companion | Member | _EmissionMap2UV | _EmissionScrollingCurve_ST__2 uniform only | none | none | Never moves. The sample follows the map selector `_EmissionMap2UV`. _EmissionScrollingUseCurve2 0 or unassigned. | 25535 | yes |
| _EmissionMask3 | 2799 | Matcap rim emission masks | companion | Member | _EmissionMask3UV | _EmissionMask3_ST | _EmissionMask3Pan | none | Gate POI_EMISSION_3. | 25734 | yes |
| _EmissionMap3 | 2809 | Matcap rim emission masks | companion | Member | _EmissionMap3UV | _EmissionMap3_ST | _EmissionMap3Pan | none | Center-out at 25686. _EmissionCenterOutEnabled3 must be 0 when the map moves. | 25682 | yes |
| _EmissionScrollingCurve3 | 2855 | Matcap rim emission masks | companion | Member | _EmissionMap3UV | _EmissionScrollingCurve_ST__3 uniform only | none | none | Never moves. The sample follows the map selector `_EmissionMap3UV`. _EmissionScrollingUseCurve3 0 or unassigned. | 25713 | yes |
| _GlitterTexture | 2897 | Refused | refused feature | sampler_linear_clamp | none | _GlitterTexture_ST | _GlitterTexturePan | none | Glitter cells read the UV as data and the sample gradients track UV0. | 26276 | yes |
| _GlitterColorMap | 2898 | Refused | refused feature | Member | _GlitterColorMapUV | _GlitterColorMap_ST | _GlitterColorMapPan | none | Belongs to the refused glitter feature. | 26249 | yes |
| _GlitterMask | 2936 | Refused | refused feature | Member | _GlitterMaskUV | _GlitterMask_ST | _GlitterMaskPan | none | Belongs to the refused glitter feature. | 26283 | yes |
| _PathingMap | 2986 | Color effects UV | companion | sampler_point_repeat | _PathingMapUV | _PathingMap_ST | _PathingMapPan | none | `_PathPointSampling` 1 selects `sampler_point_repeat` at 27392. `_PathPointSampling` 0 selects `sampler_linear_repeat` at 27396. The rule of section 5 requires `_PathPointSampling` 1 when the texture moves on selector 0, so the admitted sampler is the point repeat one. | 27392, 27396 | yes |
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
| _TruchetTex | 3418 | Color effects UV | companion | Member | none | _TruchetTex_ST | _TruchetTexPan | none | Stays bound. Never moves and never joins a page. The sample reads the pattern coordinate with explicit gradients, not a mesh UV. `_TruchetUV` must not be 0. No selector property exists. | 28966 | yes |
| _TruchetMask | 3421 | Color effects UV | companion | Member | _TruchetMaskUV | _TruchetMask_ST | _TruchetMaskPan | none | Same pattern rule. | 28910 | yes |
| _ALDecalColorMask | 3519 | Color effects UV | companion | Member | _ALDecalColorMaskUV | _ALDecalColorMask_ST | _ALDecalColorMaskPan | none | _ALDecalUV must not be 0. | 24747 | yes |
| _VertexBasicsMask | 3569 | Color effects UV | companion | sampler_linear_repeat | VertexBasicsMaskUV | _VertexBasicsMask_ST | _VertexBasicsMaskUVPan | none | Vertex stage sample at LOD 0. The selector property has no leading underscore. _VertexWindEnabled must be 0 when the mask is a page, because the wind branch at 18348 reads world coordinates. | 18241, 18348 | yes |
| _VertexManipulationHeightMask | 3581 | Color effects UV | companion | sampler_linear_repeat | _VertexManipulationHeightMaskUV | _VertexManipulationHeightMask_ST | _VertexManipulationHeightMaskPan | none | Vertex stage sample at LOD 0. Enum covers UV0 to UV3. | 18282 | yes |
| _LookAtMask | 3731 | Color effects UV | companion | sampler_linear_repeat | _LookAtMaskUV | _LookAtMask_ST | _LookAtMaskPan | none | Vertex stage sample at LOD 0. Gate POI_VERTEX_LOOKAT. Enum covers UV0 to UV3. | 18444 | yes |
| _VertexGlitchMap | 3836 | KeyOnly | neutral | sampler_linear_repeat | none | none | none | none | Sampled at world height coordinates. A commented duplicate declaration sits at 3830. | 18761 | yes |
| _UzumoreMask | 3869 | Color effects UV | companion | sampler_point_clamp | _UzumoreMaskUV | none | none | none | The clamp sampler refuses selector 0. The property doubles as the channel index. Default 3 reads UV3 and channel A. No transform or pan exists. | 18081 | yes |
| _GlobalMaskTexture0 | 3944 | Matcap rim emission masks | companion | Member | _GlobalMaskTexture0UV | _GlobalMaskTexture0_ST | _GlobalMaskTexture0Pan | none | Split mode adds _GlobalMaskTexture0SplitTilingOffset_G, _B, and _A transforms plus _GlobalMaskTexture0SplitPan_G, _B, and _A. Split on requires identity transforms and zero split pans. | 19265 | yes |
| _GlobalMaskTexture1 | 3958 | Matcap rim emission masks | companion | Member | _GlobalMaskTexture1UV | _GlobalMaskTexture1_ST | _GlobalMaskTexture1Pan | none | Split properties as for slot 0. | 19283 | yes |
| _GlobalMaskTexture2 | 3972 | Matcap rim emission masks | companion | Member | _GlobalMaskTexture2UV | _GlobalMaskTexture2_ST | _GlobalMaskTexture2Pan | none | Split properties as for slot 0. | 19301 | yes |
| _GlobalMaskTexture3 | 3986 | Matcap rim emission masks | companion | Member | _GlobalMaskTexture3UV | _GlobalMaskTexture3_ST | _GlobalMaskTexture3Pan | none | Split properties as for slot 0. | 19319 | yes |
| _DistortionMask | 4404 | Color effects UV | companion | Member | _DistortionMaskUV | none | _DistortionMaskPan | none | No transform property exists. Gate USER_LUT. | 19693 | yes |
| _DistortionFlowTexture | 4408 | Color effects UV | companion | Member | _DistortionFlowTextureUV | _DistortionFlowTexture_ST | _DistortionFlowTexturePan | none | Fresh default is unassigned. Gate USER_LUT. Distortion writes slot 7, so no moved texture may select 7 under this keyword. | 19681 | yes |
| _DistortionFlowTexture1 | 4411 | Color effects UV | companion | Member | _DistortionFlowTexture1UV | _DistortionFlowTexture1_ST | _DistortionFlowTexture1Pan | none | Same rules. | 19687 | yes |
| _HeightMap | 4453 | Color effects UV | companion | Member | _HeightMapUV | _HeightMap_ST | _HeightMapPan | none | Marched at the parallax coordinates with explicit gradients. Never moves. _ParallaxUV and _HeightMapUV must not be 0. | 20017 | yes |
| _Heightmask | 4457 | Color effects UV | companion | sampler_linear_repeat | _HeightmaskUV | _Heightmask_ST | _HeightmaskPan | none | The pan joins the coordinate before the sample, and the sampler macro pan argument is zero. _HeightmaskUV must not be 0, or the mask stays unassigned. | 19832 | yes |
| _PPMask | 4495 | Color effects UV | companion | Member | _PPMaskUV | _PPMask_ST | _PPMaskPan | none | Gate POSTPROCESS. The selector property sits at 4499. | 28208 | yes |

Class counts on 2026-11-23, over the 138 rows that cover the 139 declaration lines: member 1, member copy 0, neutral 17, companion 107, refused feature 13. Unique texture names 138. The commented duplicate declaration of `_VertexGlitchMap` at 3830 shares the row of the active declaration at 3836. The commented `_Udon_VideoTex` at 242 has its own refused row. The companion class covers every texture with a route entry. The route decision between `Uv0` and `OtherCoordinates` follows the selector at capture time.

### 4.4 Sampler verification summary

Every companion row and the member row carries a verified sampler read at the sample lines of the table, re-read from the base pass on 2026-11-23. Observed counts over the 107 companion rows: Member 83, `sampler_trilinear_repeat` 11, `sampler_linear_repeat` 10, `sampler_point_repeat` 1, `sampler_point_clamp` 1, Own 1. The one Own companion is `_VideoPixelTexture`, which never moves. The member row samples through `sampler_MainTex`.

Neutral rows carry their own verified samplers: `sampler_linear_clamp` 5, Own 5, `sampler_trilinear_repeat` 4, `sampler_linear_repeat` 2, Member 1. Refused rows: Member 6, Own 5, `sampler_linear_clamp` 1, no sample 1. One companion row uses a clamp sampler. `_UzumoreMask` reads `sampler_point_clamp` and refuses at selector 0. The other clamp reads sit on neutral rows and on the refused `_GlitterTexture`. The route builder reads every sampler from this table. A wrong sampler kind is a false positive.

## 5. Conditional rules

The table level required value function checks these rules. Each failure returns the first failing sentence. Every property named here exists on both bundled rows with the declared type. The main color rule carries over from `PoiyomiMainColor`.

| rule | keywords | properties read | declared types | safe state |
|---|---|---|---|---|
| Main transform | none | _MainTex_ST, _MainTexPan | Vector, Vector | Transform identity (1,1,0,0) and pan zero |
| Main selector and mode | none | _MainTexUV, _MainPixelMode, _MainTexStochastic | Int, Float, Float | 0, 0, 0 |
| Sharpened alpha to coverage | none | _AlphaSharpenedA2C, _AlphaToCoverage | Float, Float | First 0 or second 0 |
| Renderer blocker | none | _RenderingAOBlockerEnabled | Float | 0 |
| Light map mode | PROP_LIGHTDATASDFMAP | _LightingMapMode | Int | Not 4 |
| Texture ramp row pick | _LIGHTINGMODE_TEXTURERAMP | _ToonRampCount, _ToonRampUVSelector | Int, Int | Count at most 1, or selector not 0 |
| Multilayer border map LOD | _LIGHTINGMODE_MULTILAYER_MATH | _ShadowBorderMaskLOD, _ShadowBorderMapToggle | Float, Float | LOD 0. Toggle 1 turns the sample on, and the companion route then applies |
| SDF shading flip | _LIGHTINGMODE_SDF | _SDFShadingTextureUV | Int | Not 0 when the texture is assigned. The texture never moves |
| Matcap blend, slot 0 | POI_MATCAP0 | _MatcapUVToBlend, _MatCapBlendUV1 | Int, Vector | Selector not 0, 6, or 7, or blend vector zero |
| Matcap blend, slot 1 | COLOR_GRADING_HDR_3D | _Matcap2UVToBlend, _MatCap2ndBlendUV1 | Int, Vector | Same |
| Matcap blend, slot 2 | POI_MATCAP2 | _Matcap3UVToBlend, _MatCap3rdBlendUV1 | Int, Vector | Same |
| Matcap blend, slot 3 | POI_MATCAP3 | _Matcap4UVToBlend, _MatCap4thBlendUV1 | Int, Vector | Same |
| Emission center out, slot 0 | _EMISSION | _EmissionCenterOutEnabled | Float | 0 when the map is assigned on selector 0 |
| Emission center out, slots 1 to 3 | POI_EMISSION_1 to POI_EMISSION_3 | _EmissionCenterOutEnabled1, 2, 3 | Float | Same |
| Emission scroll curve, slots 0 to 3 | _EMISSION and POI_EMISSION_1 to 3 | _EmissionScrollingUseCurve, 1, 2, 3 | Float | 0 or the curve texture unassigned |
| Global mask split, slots 0 to 3 | POI_GLOBALMASK_TEXTURES | _GlobalMaskTexture0Split to 3Split, the 12 _SplitTilingOffset_G, _B, _A vectors, the 12 _SplitPan_G, _B, _A vectors | Int, Vector | Split 0, or every split transform identity and every split pan zero |
| Distortion target | USER_LUT | _DistortionUvToDistort | Int | 1, 2, or 3. Never 0 |
| Dissolve UV tile | DISTORT | _UVTileDissolveEnabled, _UVTileDissolveUV | Float, Int | Enabled 0, or selector not 0 |
| Text overlay selectors | EFFECT_BUMP | _TextFPSUV, _TextPositionUV, _TextTimeUV, _TextNumericUV | Int | Every enabled sub-overlay selector not 0 |
| Parallax channels | POI_PARALLAX | _ParallaxUV, _HeightMapUV, _HeightmaskUV | Int, Int, Int | All three not 0. Defaults refuse |
| Truchet channel | POI_TRUCHET | _TruchetUV | Int | Not 0. Default refuses |
| Voronoi space | POI_VORONOI | _VoronoiSpace | Int | Not 2 |
| UV tile discard grids | POI_UDIMDISCARD | _UDIMDiscardMode, _UDIMDiscardUV, the 16 _UDIMDiscardRow vectors, _EnableUDIMFaceDiscardOptions, _UDIMFaceDiscardUV, the 16 _UDIMFaceDiscardRow vectors | Int, Int, Float, Float, Int, Float | Per grid, all row flags 0, or the grid selector not 0 |
| Pathing source | POI_PATHING | _PathSource, _PathingUVSelect | Float, Int | Source not 1, or selector not 0 |
| Pathing point sampling | POI_PATHING | _PathPointSampling | Float | 1 when _PathingMap is assigned on selector 0. Default 1 holds |
| Uzumore channel | POI_UZUMORE | _UzumoreMaskUV | Float | Not 0. Default 3 holds |
| Video modes | POI_VIDEO_EFFECTS | _VideoPixelateToResolution, _VideoType, _VideoPixelTextureUV | Float, Int, Int | Pixelate 0, type not 2, pixel texture selector not 0 when assigned |
| AudioLink UV reads | POI_AUDIOLINK | _AudioLinkCCStripY, the uniform flags _DebugWaveform, _DebugDFT, _DebugCCStrip, _DebugCCLights, _DebugAutocorrelator | Float, uniforms without Properties entries | All 0. Defaults hold |
| AL decal channel | POI_AL_DECAL | _ALDecalUV | Int | Not 0. Default refuses |
| AL volume color channel | POI_AL_VOLUMECOLOR | _ALVolumeColorUV, _ALVolumeColorDirection | Int, Int | Selector not 0. Default refuses |
| Vertex spectrum | AUTO_EXPOSURE | _VertexSpectrumMotion, _VertexSpectrumUV, _VertexSpectrumUVDirection | Float, Int, Int | Motion 0, or the selector not 0 |
| Vertex wind | AUTO_EXPOSURE | _VertexWindEnabled | Float | 0 when _VertexBasicsMask is a companion page |

Notes on the conditional rules:

- Two keywords with verdict Companion own rules. `AUTO_EXPOSURE` owns the vertex spectrum and vertex wind rules. `_LIGHTINGMODE_MULTILAYER_MATH` owns the border map LOD rule. A use that enables such a keyword with a broken rule value refuses with `RequiredValueNotHeld`.
- Two keywords with verdict Conditional own no table rule. `_GLOSSYREFLECTIONS_OFF` and `POI_RIM2` are safe exactly when the companion conditions of the active style keyword in section 4.3 hold. The texture routes carry those conditions, so no table rule is needed.
- `_MainTex_ST` has no Properties block entry. It is the Unity managed tiling and offset of `_MainTex`. `_MainTexPan` is declared Vector at 63. The 12 split pans are declared Vector at 3948 and their siblings.
- The audio debug flags are shader uniforms without Properties block entries, declared at 5638 to 5651. A material can carry saved values for them. The route reads them as floats. A missing name reads 0, which is the safe state.
- `_DepthTextureUV` admits 0 and 8. Value 8 is a depth gradient coordinate, not a mesh UV.
- The coordinate rule of the spec applies to every companion selector value. Selectors 6 and 7 refuse, every value above 9 and every NaN refuse with `RequiredValueNotHeld`.
- The stochastic rule is per texture: every companion route with a stochastic property requires that flag 0 on selector 0. The `_STOCHASTICMODE_*` keywords stay Conditional because their mode macros move coordinates only when a flag is 1.

## 6. Dead value families

One row per keyword. A property enters the family of a keyword only when every read of it in both bundled files sits inside a conditional compilation block that requires that keyword. Section 2 records the gate walk that verified all 104 rows on 2026-11-23. A name that ends in a star is a prefix. It stands for every property whose name starts with that prefix. A plain name stands for one property. An empty family means the keyword gates no property reads. Some prefix tokens cover saved names that the shader never reads, such as the tiling and offset entry of a texture that no gate reads and a few UI header names. Those names carry no render effect, so keeping them in a family is harmless. When two keywords gate one read, the property appears in both families.

| keyword | dead properties |
|---|---|
| PROP_LIGHTINGAOMAPS | _LightingAO*, _LightDataAOStrength* |
| PROP_LIGHTINGDETAILSHADOWMAPS | _LightingDetail*, _LightingAddDetail* |
| PROP_LIGHTINGSHADOWMASKS | _LightingShadowMask* |
| PROP_LIGHTDATASDFMAP | _LightDataSDF* |
| COLOR_GRADING_HDR | _MainHue*, _MainColorAdjustTexture*, _MainGamma*, _MainBrightness*, _MainAL*, _MainGradation*, _MainSaturation*, _Saturation*, _ColorGradingToggle* |
| PROP_MAINGRADATIONTEX | _MainGradationTex* |
| POI_GLOBALMASK_TEXTURES | _GlobalMaskTexture0*, _GlobalMaskTexture1*, _GlobalMaskTexture2*, _GlobalMaskTexture3* |
| USER_LUT | _DistortionFlow*, _DistortionStrength*, _DistortionMaskUV*, _DistortionMaskPan*, _DistortionMaskChannel*, _DistortionUvToDistort*, _EnableDistortionAudioLink*, _DistortionMask |
| POI_UDIMDISCARD | _UDIMFace*, _UDIMDiscardRow0*, _UDIMDiscardRow1*, _UDIMDiscardRow2*, _UDIMDiscardRow3*, _UDIMDiscardUV*, _UDIMDiscardMode*, _EnableUDIMFaceDiscardOptions* |
| POI_PARALLAX | _Height*, _Curv*, _ParallaxUV*, _ParallaxMipBias*, _ParallaxConeRatio*, _ParallaxRelaxedCone*, _ParallaxAnisotropic*, _ParallaxBinarySteps*, _ParallaxAnisotropyScale* |
| POI_AUDIOLINK | _AudioLinkDecal*, _PathAL*, _AudioLinkPath*, _AudioLinkVoronoi*, _GlitterAL*, _PathChrono*, _AudioLinkRim*, _DecalRotationCTAL*, _Debug*, _ALDecalVolume*, _ALVolume*, _AudioLinkFlipbook*, _AudioLinkOutline*, _VertexLocalRotationCTAL*, _TruchetAL*, _VertexSpectrum*, _ALUV*, _ALDecalColor*, _TruchetChrono*, _FlipbookChrono*, _AudioLinkDissolve*, _AudioLinkSmoothing*, _AlphaAudio*, _ALDecalBand*, _VertexWorld*, _ALDecalShape*, _VertexLocalScale*, _VertexGlitchingAudio*, _VertexLocalTranslation*, _MainAL*, _ALDecalUV*, _MainHueAL*, _ALDecalBlend*, _AudioLinkBand*, _ALDecalGlobal*, _VertexLookAtAudio*, _VertexRoundingRange*, _DistortionStrengthAudio*, _DistortionStrength1Audio*, _ALLowEmission*, _ALMidEmission*, _ALHighEmission*, _ALDecalLineWidth*, _ALDecalBaseBoost*, _AudioLinkCCStripY*, _ALDecalLowEmission*, _ALDecalMidEmission*, _ALDecalTrebleBoost*, _ALDecalHighEmission*, _TruchetHideWhenNoAL*, _AudioLinkAnimToggle*, _ALDecalControlsAlpha*, _OutlineALColorEnabled*, _VertexAudioLinkEnabled*, _EnableDissolveAudioLink*, _ALDecaldCircleDimensions*, _EnableDistortionAudioLink*, _VertexManipulationHeightAL*, _VertexLookAtAlphaAudiolink*, _VertexManipulationHeightBand*, _VertexLocalRotationAL |
| POI_BLACKLIGHTMASKING | _BlackLightMasking0*, _BlackLightMasking1*, _BlackLightMasking2Global*, _BlackLightMasking3Global*, _BlackLightMasking2Key*, _BlackLightMasking3Key* |
| FINALPASS | _DetailTex*, _DetailNormal*, _DetailMask*, _DetailTint*, _DetailBrightness*, _BackFaceDetailIntensity* |
| AUTO_EXPOSURE | _VertexWind*, _VertexManipulationHeight*, _VertexTornado*, _VertexLocalRotationCTAL*, _VertexSphere*, _VertexRounding*, _VertexBarrel*, _VertexSpectrum*, _VertexBasics*, _VertexManipulationLocal*, _VertexWorld*, _VertexLocalScale*, _VertexLocalTranslation*, _VertexAudioLinkEnabled*, _VertexManipulationWorldTranslation*, _VertexLocalRotationAL |
| POI_VERTEX_LOOKAT | _Look*, _VertexLookAtAudio*, _VertexLookAtAlphaAudiolink* |
| POI_VERTEX_GLITCHING | _VertexGlitchingAudio*, _VertexGlitchMirror*, _VertexGlitchDensity*, _VertexGlitchStrength*, _VertexGlitchThreshold*, _VertexGlitchFrequency*, _VertexGlitchMapPanSpeed*, _VertexGlitchMap |
| POI_VERTEX_GLITCHING_TEXTURE | _VertexGlitchDensity*, _VertexGlitchMapPanSpeed*, _VertexGlitchMap |
| POI_DEPTHBULGE | _DepthBulgeMask*, _DepthBulgeHeight*, _DepthBulgeFadeLength* |
| POI_BACKFACE | _BackFaceMask*, _BackFaceHue*, _BackFaceTexture*, _BackFaceColor*, _BackFaceEmission*, _BackFaceReplaceAlpha*, _BackFaceShiftColorSpace*, _BackFaceDetailIntensity* |
| VIGNETTE | _RGBA*, _Red*, _Blue*, _Green*, _AlphaTexture*, _RgbRed*, _RgbBlue*, _RgbAlpha*, _RgbGreen*, _AlphaColor*, _RgbNormalBGlobal*, _RgbNormalGGlobal*, _RgbNormalAGlobal*, _RgbNormalRGlobal*, _RGBMaskUV*, _RGBMaskPan*, _RGBMaskType*, _RgbNormalBUV*, _RgbNormalAUV*, _RgbNormalGUV*, _RgbNormalRUV*, _AlphaAlphaAdd*, _RgbNormalBPan*, _RgbNormalGPan*, _RgbNormalRPan*, _RgbNormalAPan*, _RgbNormalAScale*, _RgbNormalBScale*, _RgbNormalRScale*, _RgbNormalGScale*, _RgbNormalAStochastic*, _RgbNormalBStochastic*, _RgbNormalRStochastic*, _RgbNormalGStochastic*, _RgbNormalBMaskChannel*, _RgbNormalAMaskChannel*, _RgbNormalRMaskChannel*, _RgbNormalGMaskChannel*, _RgbNormalABlendSources*, _RgbNormalGBlendSources*, _RgbNormalRBlendSources*, _RgbNormalBBlendSources*, _RGBMask, _RGBMask_ST, _RgbNormalA, _RgbNormalA_ST, _RgbNormalB, _RgbNormalB_ST, _RgbNormalG, _RgbNormalG_ST, _RgbNormalR, _RgbNormalR_ST |
| GEOM_TYPE_MESH | none |
| PROP_RGBMASK | _RGBMaskUV*, _RGBMaskPan*, _RGBMask, _RGBMask_ST |
| PROP_RGBAMETALLICMAPS | _RGBAMetallic*, _RGBARedMetallicInvert*, _RGBABlueMetallicInvert*, _RGBAGreenMetallicInvert*, _RGBAAlphaMetallicInvert* |
| PROP_RGBASMOOTHNESSMAPS | _RGBASmoothness*, _RGBARedSmoothnessInvert*, _RGBABlueSmoothnessInvert*, _RGBAGreenSmoothnessInvert*, _RGBAAlphaSmoothnessInvert* |
| PROP_REDTEXTURE | _RedTexture* |
| PROP_RGBNORMALR | _RgbNormalRUV*, _RgbNormalRPan*, _RgbNormalRScale*, _RgbNormalRStochastic*, _RgbNormalRBlendSources*, _RgbNormalR, _RgbNormalR_ST |
| PROP_GREENTEXTURE | _GreenTexture* |
| PROP_RGBNORMALG | _RgbNormalGUV*, _RgbNormalGPan*, _RgbNormalGScale*, _RgbNormalGStochastic*, _RgbNormalGBlendSources*, _RgbNormalG, _RgbNormalG_ST |
| PROP_BLUETEXTURE | _BlueTexture* |
| PROP_RGBNORMALB | _RgbNormalBUV*, _RgbNormalBPan*, _RgbNormalBScale*, _RgbNormalBStochastic*, _RgbNormalBBlendSources*, _RgbNormalB, _RgbNormalB_ST |
| PROP_ALPHATEXTURE | _AlphaTexture* |
| PROP_RGBNORMALA | _RgbNormalAUV*, _RgbNormalAPan*, _RgbNormalAScale*, _RgbNormalAStochastic*, _RgbNormalABlendSources*, _RgbNormalA, _RgbNormalA_ST |
| POI_LTCGI | _LTCGI_AnimToggle, _LTCGI_DiffuseColor, _LTCGI_DiffuseColorThemeIndex, _LTCGI_Metallic, _LTCGI_Smoothness, _LTCGI_SpecularColor, _LTCGI_SpecularColorThemeIndex, _LTCGI_UseEO, _LTCGI_UsePBR |
| VIGNETTE_MASKED | _Shadow*, _Cloth*, _SDF*, _Skin*, _Multilayer*, _LightingWrapped*, _Base*, _LightingGradient*, _LightingAdditiveGradient*, _SssScale*, _OutlineLit*, _SssBumpBlur*, _ToonRampCount*, _ToonRampUVSelector*, _LightingShadowColor*, _LightingAdditiveType*, _OutlineShadowStrength*, _ForceFlatRampedLightmap*, _ShadingShadeMapBlendType*, _SssTransmissionAbsorption*, _LightingIgnoreAmbientColor*, _LightingMulitlayerNonLinear*, _ShadeColor_Step, _Use_1stAs2nd, _Use_1stShadeMapAlpha_As_ShadowMask, _Use_2ndShadeMapAlpha_As_ShadowMask, _Use_BaseAs1st |
| _LIGHTINGMODE_TEXTURERAMP | _Toon*, _ShadowOffset* |
| _LIGHTINGMODE_MULTILAYER_MATH | _Shadow3rd*, _Shadow2nd*, _ShadowBorder*, _ShadowColor*, _Multilayer*, _ShadowStrengthMask*, _ShadowAO*, _ShadowFlat*, _ShadowBlur*, _ShadowPostAO*, _ShadowReceive*, _ShadowMaskType*, _ShadowEnvStrength*, _ShadowMainStrength*, _ShadowNormalStrength*, _LightingMulitlayerNonLinear* |
| _LIGHTINGMODE_SHADEMAP | _Base*, _ShadingShadeMapBlendType*, _ShadeColor_Step, _Use_1stAs2nd, _Use_1stShadeMapAlpha_As_ShadowMask, _Use_2ndShadeMapAlpha_As_ShadowMask, _Use_BaseAs1st |
| _LIGHTINGMODE_REALISTIC | none |
| _LIGHTINGMODE_WRAPPED | _LightingWrapped*, _LightingGradient* |
| _LIGHTINGMODE_SKIN | _Skin*, _SssScale*, _SssBumpBlur*, _SssTransmissionAbsorption* |
| _LIGHTINGMODE_FLAT | _ForceFlatRampedLightmap* |
| _LIGHTINGMODE_CLOTH | _Cloth* |
| _LIGHTINGMODE_SDF | _SDF* |
| GEOM_TYPE_BRANCH | _AudioLinkDecal0*, _Decal0Channel*, _Decal0Video*, _Decal0Apply*, _Decal0Global*, _Decal0Depth*, _DecalTextureUV*, _Decal0FaceMask*, _DecalTexturePan*, _Decal0OnlyVideo*, _Decal0MaskChannel*, _AudioLinkDecalCC0*, _Decal0UseDecalAlpha*, _DecalColorThemeIndex*, _Decal0HueAngleStrength*, _DecalRotationCTALType0*, _DecalRotationCTALBand0*, _DecalRotationCTALSpeed0*, _Decal0OverrideAlphaMode*, _DecalAlphaIntensity, _DecalBlendAlpha, _DecalBlendType, _DecalColor, _DecalEmissionStrength, _DecalHueShift, _DecalHueShiftColorSpace, _DecalHueShiftEnabled, _DecalHueShiftSelectOrShift, _DecalHueShiftSpeed, _DecalMirroredUVMode, _DecalOverrideAlpha, _DecalPosition, _DecalRotation, _DecalRotationSpeed, _DecalScale, _DecalSideOffset, _DecalSymmetryMode, _DecalTexture, _DecalTexture_ST, _DecalTiled |
| GEOM_TYPE_BRANCH_DETAIL | _AudioLinkDecal1*, _Decal1Channel*, _Decal1Video*, _DecalTexture1*, _Decal1Apply*, _DecalColor1*, _Decal1Global*, _DecalScale1*, _DecalTiled1*, _Decal1Depth*, _DecalRotation1*, _Decal1FaceMask*, _DecalHueShift1*, _DecalPosition1*, _Decal1OnlyVideo*, _DecalBlendType1*, _DecalSideOffset1*, _DecalBlendAlpha1*, _AudioLinkDecalCC1*, _Decal1MaskChannel*, _DecalSymmetryMode1*, _DecalHueShiftSpeed1*, _DecalOverrideAlpha1*, _DecalRotationSpeed1*, _Decal1UseDecalAlpha*, _DecalMirroredUVMode1*, _DecalAlphaIntensity1*, _DecalHueShiftEnabled1*, _DecalEmissionStrength1*, _DecalRotationCTALType1*, _DecalRotationCTALBand1*, _Decal1HueAngleStrength*, _DecalRotationCTALSpeed1*, _Decal1OverrideAlphaMode*, _DecalHueShiftColorSpace1*, _DecalHueShiftSelectOrShift1* |
| GEOM_TYPE_FROND | _AudioLinkDecal2*, _Decal2Channel*, _Decal2Video*, _DecalTexture2*, _DecalColor2*, _Decal2Apply*, _Decal2Global*, _Decal2Depth*, _DecalTiled2*, _DecalScale2*, _DecalRotation2*, _DecalHueShift2*, _Decal2FaceMask*, _DecalPosition2*, _Decal2OnlyVideo*, _DecalBlendType2*, _DecalSideOffset2*, _DecalBlendAlpha2*, _AudioLinkDecalCC2*, _Decal2MaskChannel*, _DecalSymmetryMode2*, _DecalHueShiftSpeed2*, _Decal2UseDecalAlpha*, _DecalRotationSpeed2*, _DecalOverrideAlpha2*, _DecalMirroredUVMode2*, _DecalAlphaIntensity2*, _DecalHueShiftEnabled2*, _DecalRotationCTALType2*, _Decal2HueAngleStrength*, _DecalEmissionStrength2*, _DecalRotationCTALBand2*, _Decal2OverrideAlphaMode*, _DecalRotationCTALSpeed2*, _DecalHueShiftColorSpace2*, _DecalHueShiftSelectOrShift2* |
| DEPTH_OF_FIELD_COC_VIEW | _AudioLinkDecal3*, _Decal3Channel*, _Decal3Video*, _DecalTexture3*, _DecalColor3*, _Decal3Apply*, _Decal3Global*, _DecalScale3*, _Decal3Depth*, _DecalTiled3*, _DecalHueShift3*, _Decal3FaceMask*, _DecalPosition3*, _DecalRotation3*, _Decal3OnlyVideo*, _DecalBlendType3*, _DecalBlendAlpha3*, _DecalSideOffset3*, _Decal3MaskChannel*, _AudioLinkDecalCC3*, _DecalSymmetryMode3*, _DecalHueShiftSpeed3*, _DecalRotationSpeed3*, _DecalOverrideAlpha3*, _Decal3OverrideAlphaMode*, _Decal3UseDecalAlpha*, _DecalMirroredUVMode3*, _DecalAlphaIntensity3*, _DecalHueShiftEnabled3*, _DecalRotationCTALType3*, _Decal3HueAngleStrength*, _DecalEmissionStrength3*, _DecalRotationCTALBand3*, _DecalRotationCTALSpeed3*, _DecalHueShiftColorSpace3*, _DecalHueShiftSelectOrShift3* |
| PROP_DECALMASK | _DecalMask* |
| DISTORT | _Dissolve*, _UVTile*, _Center*, _Spherical*, _AudioLinkDissolve*, _ContinuousDissolve*, _EnableDissolveAudioLink* |
| POI_ANISOTROPICS | _Aniso0*, _Aniso1*, _AnisoColor*, _AnisoUse*, _AnisoGlobal*, _AnisoAdd*, _AnisoReplace*, _AnisoNormalBlend*, _AnisoHideInShadow*, _AnisoFadeByLightDirection* |
| POI_MATCAP0 | _Matcap0AL*, _MatcapMask*, _MatcapHue*, _Matcap0Normal*, _MatcapApply*, _Matcap0Circle*, _MatcapUV*, _MatcapTPS*, _MatcapAdd*, _MatcapLight*, _MatcapColor*, _MatcapPan*, _MatcapMixed*, _MatcapScreen*, _MatcapBorder*, _MatcapNormal*, _MatcapReplace*, _MatcapMultiply*, _MatCapBlendUV1*, _MatcapRotation*, _MatcapIntensity*, _MatcapSmoothness*, _MatcapBaseColorMix*, _MatcapAlphaOverride*, _MatcapEmissionStrength*, _Matcap |
| POI_MATCAP0_CUSTOM_NORMAL | _Matcap0Normal* |
| COLOR_GRADING_HDR_3D | _Matcap1AL*, _Matcap2Mask*, _Matcap2Hue*, _Matcap1Normal*, _Matcap2Apply*, _Matcap2UV*, _Matcap2TPS*, _Matcap2Add*, _Matcap2Light*, _Matcap2Color*, _Matcap2Pan*, _Matcap2Mixed*, _Matcap2Border*, _Matcap2Screen*, _Matcap2Replace*, _Matcap2Multiply*, _Matcap2Rotation*, _Matcap2Intensity*, _Matcap2Smoothness*, _MatCap2ndBlendUV1*, _Matcap2BaseColorMix*, _Matcap2AlphaOverride*, _Matcap2EmissionStrength*, _Matcap2, _Matcap2Normal |
| POI_MATCAP1_CUSTOM_NORMAL | _Matcap1Normal* |
| POI_MATCAP2 | _Matcap2AL*, _Matcap3Mask*, _Matcap3Hue*, _Matcap2NormalMap*, _Matcap3Apply*, _Matcap3UV*, _Matcap3Add*, _Matcap3TPS*, _Matcap3Light*, _Matcap3Color*, _Matcap3Pan*, _Matcap3Mixed*, _Matcap3Screen*, _Matcap3Border*, _Matcap3Replace*, _Matcap3Multiply*, _Matcap3Rotation*, _Matcap3Intensity*, _Matcap3Smoothness*, _MatCap3rdBlendUV1*, _Matcap3BaseColorMix*, _Matcap3AlphaOverride*, _Matcap3EmissionStrength*, _Matcap3, _Matcap3Normal |
| POI_MATCAP2_CUSTOM_NORMAL | _Matcap2NormalMap* |
| POI_MATCAP3 | _Matcap3AL*, _Matcap4Mask*, _Matcap4Hue*, _Matcap3NormalMap*, _Matcap4Apply*, _Matcap4UV*, _Matcap4TPS*, _Matcap4Add*, _Matcap4Light*, _Matcap4Color*, _Matcap4Pan*, _Matcap4Mixed*, _Matcap4Screen*, _Matcap4Border*, _Matcap4Normal*, _Matcap4Replace*, _Matcap4Multiply*, _Matcap4Rotation*, _Matcap4Intensity*, _Matcap4Smoothness*, _MatCap4thBlendUV1*, _Matcap4BaseColorMix*, _Matcap4AlphaOverride*, _Matcap4EmissionStrength*, _Matcap4 |
| POI_MATCAP3_CUSTOM_NORMAL | _Matcap3NormalMap* |
| _CUBEMAP | _CubeMapMask*, _CubeMapHue*, _CubeMapColor*, _CubeMapRotation*, _CubeMapUVMode*, _CubeMapContrast*, _CubeMapIntensity*, _CubeMapLightMask*, _CubemapBlendType*, _CubeMapSaturation*, _CubeMapSmoothness*, _CubeMapBrightness*, _CubeMapBlendAmount*, _CubeMapEmissionStrength*, _CubeMapWorldNormalsStrength*, _CubeMap |
| POI_AL_DECAL | _ALDecalVolume*, _ALUV*, _ALDecalColor*, _ALDecalBand*, _ALDecalShape*, _ALDecalUV*, _ALDecalBlend*, _ALDecalGlobal*, _ALDecalBaseBoost*, _ALDecalLineWidth*, _ALDecalMidEmission*, _ALDecalTrebleBoost*, _ALDecalLowEmission*, _ALDecalHighEmission*, _ALDecalControlsAlpha*, _ALDecaldCircleDimensions* |
| POI_AL_VOLUMECOLOR | _ALVolume*, _ALLowEmission*, _ALMidEmission*, _ALHighEmission* |
| _SUNDISK_HIGH_QUALITY | _AudioLinkFlipbook*, _FlipbookMask*, _FlipbookHue*, _FlipbookChrono*, _FlipbookColor*, _FlipbookStart*, _FlipbookRotation*, _FlipbookCrossfade*, _FlipbookFPS*, _FlipbookTiled*, _FlipbookReplace*, _FlipbookPanning*, _FlipbookEndFrame*, _FlipbookBlendType*, _FlipbookTexArrayUV*, _FlipbookSideOffset*, _FlipbookTexArrayPan*, _FlipbookFrameOffset*, _FlipbookScaleOffset*, _FlipbookCurrentFrame*, _FlipbookEmissionStrength*, _FlipbookManualFrameControl*, _FlipbookIntensityControlsAlpha*, _FlipbookAlphaControlsFinalAlpha*, _FlipbookTexArray |
| _EMISSION | _EmissionAL0*, _AudioLinkEmission0*, _EmissionMask0*, _EmissionMapUV*, _EmissionMapPan*, _EmissionMaskUV*, _EmissionMaskPan*, _EmissionReplace0*, _EmissionMaskChannel*, _EmissionColorThemeIndex*, _EmissionBaseColorAsMap, _EmissionBlinkingEnabled, _EmissionBlinkingOffset, _EmissionCenterOutEnabled, _EmissionCenterOutSpeed, _EmissionColor, _EmissionFluorescence, _EmissionHueSelectOrShift, _EmissionHueShift, _EmissionHueShiftColorSpace, _EmissionHueShiftEnabled, _EmissionHueShiftSpeed, _EmissionMap, _EmissionMap_ST, _EmissionMask, _EmissionMaskInvert, _EmissionMask_ST, _EmissionSaturation, _EmissionScrollingCurve, _EmissionScrollingCurve_ST, _EmissionScrollingOffset, _EmissionScrollingUseCurve, _EmissionScrollingVertexColor, _EmissionStrength, _EmissiveBlink_Max, _EmissiveBlink_Min, _EmissiveBlink_Velocity, _EmissiveScroll_Direction, _EmissiveScroll_Interval, _EmissiveScroll_Velocity, _EmissiveScroll_Width, _EnableGITDEmission, _GITDEMaxEmissionMultiplier, _GITDEMaxLight, _GITDEMinEmissionMultiplier, _GITDEMinLight, _GITDEWorldOrMesh, _ScrollingEmission |
| POI_EMISSION_1 | _EmissionMask1*, _EmissionAL1*, _EmissionMap1*, _AudioLinkEmission1*, _EmissionColor1*, _GITDEMaxLight1*, _GITDEMinLight1*, _EmissionReplace1*, _EmissionStrength1*, _EmissionHueShift1*, _GITDEWorldOrMesh1*, _ScrollingEmission1*, _EmissionMaskInvert1*, _EmissionSaturation1*, _EnableGITDEmission1*, _EmissionFluorescence1*, _EmissionHueShiftSpeed1*, _EmissionBlinkingOffset1*, _EmissionCenterOutSpeed1*, _EmissionScrollingCurve1*, _EmissionBaseColorAsMap1*, _EmissionHueShiftEnabled1*, _EmissionBlinkingEnabled1*, _EmissionScrollingOffset1*, _EmissionCenterOutEnabled1*, _EmissionHueSelectOrShift1*, _EmissionScrollingUseCurve1*, _GITDEMaxEmissionMultiplier1*, _GITDEMinEmissionMultiplier1*, _EmissionHueShiftColorSpace1*, _EmissionScrollingVertexColor1*, _EmissiveBlink_Max1, _EmissiveBlink_Min1, _EmissiveBlink_Velocity1, _EmissiveScroll_Direction1, _EmissiveScroll_Interval1, _EmissiveScroll_Velocity1, _EmissiveScroll_Width1 |
| POI_EMISSION_2 | _EmissionMask2*, _EmissionAL2*, _EmissionMap2*, _AudioLinkEmission2*, _EmissionColor2*, _GITDEMinLight2*, _GITDEMaxLight2*, _EmissionReplace2*, _GITDEWorldOrMesh2*, _EmissionStrength2*, _EmissionHueShift2*, _ScrollingEmission2*, _EmissionSaturation2*, _EmissionMaskInvert2*, _EnableGITDEmission2*, _EmissionFluorescence2*, _EmissionHueShiftSpeed2*, _EmissionBlinkingOffset2*, _EmissionCenterOutSpeed2*, _EmissionBaseColorAsMap2*, _EmissionScrollingCurve2*, _EmissionHueShiftEnabled2*, _EmissionScrollingOffset2*, _EmissionBlinkingEnabled2*, _EmissionCenterOutEnabled2*, _EmissionHueSelectOrShift2*, _EmissionScrollingUseCurve2*, _GITDEMinEmissionMultiplier2*, _EmissionHueShiftColorSpace2*, _GITDEMaxEmissionMultiplier2*, _EmissionScrollingVertexColor2*, _EmissiveBlink_Max2, _EmissiveBlink_Min2, _EmissiveBlink_Velocity2, _EmissiveScroll_Direction2, _EmissiveScroll_Interval2, _EmissiveScroll_Velocity2, _EmissiveScroll_Width2 |
| POI_EMISSION_3 | _EmissionMask3*, _EmissionAL3*, _EmissionMap3*, _AudioLinkEmission3*, _EmissionColor3*, _GITDEMinLight3*, _GITDEMaxLight3*, _EmissionReplace3*, _EmissionHueShift3*, _GITDEWorldOrMesh3*, _EmissionStrength3*, _ScrollingEmission3*, _EmissionSaturation3*, _EnableGITDEmission3*, _EmissionMaskInvert3*, _EmissionFluorescence3*, _EmissionHueShiftSpeed3*, _EmissionCenterOutSpeed3*, _EmissionBlinkingOffset3*, _EmissionBaseColorAsMap3*, _EmissionScrollingCurve3*, _EmissionScrollingOffset3*, _EmissionBlinkingEnabled3*, _EmissionHueShiftEnabled3*, _EmissionHueSelectOrShift3*, _EmissionCenterOutEnabled3*, _EmissionScrollingUseCurve3*, _GITDEMaxEmissionMultiplier3*, _EmissionHueShiftColorSpace3*, _GITDEMinEmissionMultiplier3*, _EmissionScrollingVertexColor3*, _EmissiveBlink_Max3, _EmissiveBlink_Min3, _EmissiveBlink_Velocity3, _EmissiveScroll_Direction3, _EmissiveScroll_Interval3, _EmissiveScroll_Velocity3, _EmissiveScroll_Width3 |
| _GLOSSYREFLECTIONS_OFF | _RimMask*, _RimShadow*, _RimLight*, _RimAp*, _RimHue*, _RimColor*, _RimTex*, _RimIndir*, _RimDir*, _RimBlend*, _RimGlobal*, _AudioLinkRimWidth*, _AudioLinkRimEmission*, _AudioLinkRimBrightness*, _RimBlur*, _RimPower*, _RimWidth*, _RimBorder*, _RimStrength*, _RimSharpness*, _RimBrightness*, _RimBaseColorMix*, _RimMainStrength*, _RimFresnelPower*, _RimPoiBlendMode*, _RimBackfaceMask*, _RimBiasIntensity*, _RimEnableLighting*, _RimNormalStrength*, _RimVRParallaxStrength*, _Add_Antipodean_RimLight, _Ap_RimLightColor, _Ap_RimLight_FeatherOff, _Ap_RimLight_Power, _Is_LightColor_Ap_RimLight, _Is_LightColor_RimLight, _Is_NormalMapToRimLight, _LightDirection_MaskOn, _Set_RimLightMask, _Set_RimLightMaskChannel, _Set_RimLightMaskPan, _Set_RimLightMaskUV, _Set_RimLightMask_ST, _Tweak_LightDirection_MaskLevel, _Tweak_RimLightMaskLevel |
| _RIMSTYLE_POIYOMI | _RimTex*, _RimApply*, _AudioLinkRimWidth*, _AudioLinkRimEmission*, _AudioLinkRimBrightness*, _RimWidth*, _RimPower*, _RimMaskUV*, _RimMaskPan*, _RimStrength*, _RimSharpness*, _RimMaskInvert*, _RimBrightness*, _RimShadowWidth*, _RimShadowAlpha*, _RimMaskChannel*, _RimShadowToggle*, _RimBaseColorMix*, _RimPoiBlendMode*, _RimBiasIntensity*, _RimBlendStrength*, _RimLightingInvert*, _RimShadowMaskInvert*, _RimShadowMaskStrength*, _RimShadowMaskRampType*, _RimMask, _RimMask_ST |
| _RIMSTYLE_UTS2 | _RimApColorThemeIndex*, _Add_Antipodean_RimLight, _Ap_RimLightColor, _Ap_RimLight_FeatherOff, _Ap_RimLight_Power, _Is_LightColor_Ap_RimLight, _Is_LightColor_RimLight, _LightDirection_MaskOn, _RimLight_FeatherOff, _RimLight_InsideMask, _RimLight_Power, _Set_RimLightMask, _Set_RimLightMaskChannel, _Set_RimLightMaskPan, _Set_RimLightMaskUV, _Set_RimLightMask_ST, _Tweak_LightDirection_MaskLevel, _Tweak_RimLightMaskLevel |
| _RIMSTYLE_LILTOON | _RimColor*, _RimIndir*, _RimDir*, _RimBlur*, _RimBorder*, _RimBlendMode*, _RimMaskOnlyMask*, _RimMainStrength*, _RimFresnelPower*, _RimBackfaceMask*, _RimEnableLighting*, _RimNormalStrength*, _RimVRParallaxStrength*, _RimShadowMask |
| POI_RIM2 | _Rim2Mask*, _Rim2Shadow*, _Rim2Light*, _AudioLinkRim2*, _Rim2Ap*, _Rim2Hue*, _Rim2Color*, _Rim2Tex*, _Rim2Indir*, _Rim2Dir*, _Rim2Blend*, _Rim2Global*, _Rim2Blur*, _Rim2Power*, _Rim2Width*, _Rim2Border*, _Rim2Strength*, _Rim2Sharpness*, _Rim2Brightness*, _Rim2FresnelPower*, _Rim2BaseColorMix*, _Rim2MainStrength*, _RimPoi2BlendMode*, _Rim2BackfaceMask*, _Rim2BiasIntensity*, _Rim2EnableLighting*, _Rim2NormalStrength*, _Rim2VRParallaxStrength*, _Add_Antipodean_Rim2Light, _Ap_Rim2LightColor, _Ap_Rim2Light_FeatherOff, _Ap_Rim2Light_Power, _Is_LightColor_Ap_Rim2Light, _Is_LightColor_Rim2Light, _Is_NormalMapToRim2Light, _LightDirection_MaskOn2, _Set_Rim2LightMask, _Set_Rim2LightMaskChannel, _Set_Rim2LightMaskPan, _Set_Rim2LightMaskUV, _Set_Rim2LightMask_ST, _Tweak_LightDirection_MaskLevel2, _Tweak_Rim2LightMaskLevel |
| _RIM2STYLE_POIYOMI | _AudioLinkRim2*, _Rim2Tex*, _Rim2Apply*, _Rim2Power*, _Rim2Width*, _Rim2MaskUV*, _Rim2MaskPan*, _Rim2Strength*, _Rim2Sharpness*, _Rim2MaskInvert*, _Rim2Brightness*, _Rim2ShadowAlpha*, _Rim2MaskChannel*, _Rim2ShadowWidth*, _RimPoi2BlendMode*, _Rim2BaseColorMix*, _Rim2ShadowToggle*, _Rim2BlendStrength*, _Rim2BiasIntensity*, _Rim2LightingInvert*, _Rim2ShadowMaskInvert*, _Rim2ShadowMaskStrength*, _Rim2ShadowMaskRampType*, _Rim2Mask, _Rim2Mask_ST |
| _RIM2STYLE_UTS2 | _Rim2ApColorThemeIndex*, _Add_Antipodean_Rim2Light, _Ap_Rim2LightColor, _Ap_Rim2Light_FeatherOff, _Ap_Rim2Light_Power, _Is_LightColor_Ap_Rim2Light, _Is_LightColor_Rim2Light, _LightDirection_MaskOn2, _Rim2Light_FeatherOff, _Rim2Light_InsideMask, _Rim2Light_Power, _Set_Rim2LightMask, _Set_Rim2LightMaskChannel, _Set_Rim2LightMaskPan, _Set_Rim2LightMaskUV, _Set_Rim2LightMask_ST, _Tweak_LightDirection_MaskLevel2, _Tweak_Rim2LightMaskLevel |
| _RIM2STYLE_LILTOON | _Rim2Color*, _Rim2Indir*, _Rim2Dir*, _Rim2Blur*, _Rim2Border*, _Rim2BlendMode*, _Rim2MaskOnlyMask*, _Rim2FresnelPower*, _Rim2MainStrength*, _Rim2BackfaceMask*, _Rim2NormalStrength*, _Rim2EnableLighting*, _Rim2VRParallaxStrength*, _Rim2ShadowMask |
| _POI_DEPTH_RIMLIGHT | _DepthRim* |
| _SUNDISK_SIMPLE | _GlitterAL*, _GlitterMask*, _GlitterColor*, _GlitterHue*, _GlitterRandom*, _GlitterMin*, _GlitterTexture*, _GlitterUV*, _GlitterUse*, _GlitterSize*, _GlitterMode*, _GlitterBias*, _GlitterSpeed*, _GlitterShape*, _GlitterLayers*, _GlitterJaggyFix*, _GlitterContrast*, _GlitterFrequency*, _GlitterBlendType*, _GlitterBrightness*, _GlitterAngleRange*, _GlitterCenterSize*, _GlitterHideInShadow*, _GlitterScaleWithLighting* |
| POI_SUBSURFACESCATTERING | _SSSColor*, _SSSSpread*, _SSSStrength*, _SSSDistortion*, _SSSThicknessMod*, _SSSBaseColorMix*, _SSSThicknessMapUV*, _SSSThicknessMapPan*, _SSSThicknessMap, _SSSThicknessMap_ST |
| MOCHIE_PBR | _MochieMetallic*, _MochieRefl*, _MochieSpecular*, _Anisotropy*, _PBR*, _BRDFTPS*, _MochieRoughness*, _PoiGSAA*, _MochieSmoothness*, _Specular2ndLayer*, _MochieLitFallback*, _MochieGSAAEnabled*, _MochieForceFallback*, _IgnoreCastedShadows*, _SFExposureOcclusion*, _RefSpecFresnelStrength*, _ReflectionAnisotropicStretch*, _LTCGI_UseEO |
| GGX_ANISOTROPICS | _Anisotropy*, _RoughnessAnisotropy*, _ReflectionAnisotropicStretch* |
| POI_CLEARCOAT | _ClearCoatMaps*, _ClearCoatSpecular*, _ClearCoatReflection*, _ClearCoatSmoothness*, _ClearCoatGSAA*, _Clearcoat*, _ClearCoatTPS*, _ClearCoatGlobal*, _ClearCoatFallback*, _ClearCoatStrength*, _ClearCoatMaskInvert*, _ClearCoatLitFallback*, _CCIgnoreCastedShadows*, _ClearCoatNormalSelect*, _ClearCoatForceFallback* |
| POI_ENVIRORIM | _RimEnviroBlur*, _RimEnviroWidth*, _RimEnviroMaskUV*, _RimEnviroMaskPan*, _RimEnviroChannel*, _RimEnviroSharpness*, _RimEnviroIntensity*, _RimEnviroMinBrightness*, _RimEnviroMask |
| POI_STYLIZED_StylizedSpecular | _High*, _ReflectionCube*, _Apply*, _Layer2*, _Metallic*, _Smoothness*, _ReflectionColor*, _StylizedSpecularIgnore*, _Reflectance*, _GSAAStrength*, _SpecularBlur*, _SpecularToon*, _UseLightColor*, _UseReflection*, _SpecularBorder*, _Layer1Strength*, _ReflectionBlendMode*, _SSIgnoreCastedShadows*, _SpecularNormalStrength*, _StylizedSpecularFeather*, _StylizedSpecular2Feather*, _StylizedSpecularStrength*, _StylizedSpecularInvertMask*, _ReflectionApplyTransparency*, _StylizedSpecularNormalStrength*, _Is_BlendAddToHiColor, _Is_SpecularToHighColor, _Set_HighColorMask, _Set_HighColorMaskChannel, _Set_HighColorMaskPan, _Set_HighColorMaskUV, _Set_HighColorMask_ST, _Tweak_HighColorMaskLevel |
| _STYLIZEDREFLECTIONMODE_UNITYCHAN | _High*, _Layer2*, _StylizedSpecularIgnore*, _UseLightColor*, _Layer1Strength*, _SSIgnoreCastedShadows*, _StylizedSpecularFeather*, _StylizedSpecularStrength*, _StylizedSpecular2Feather*, _StylizedSpecularInvertMask*, _StylizedSpecularNormalStrength*, _Is_BlendAddToHiColor, _Is_SpecularToHighColor, _Set_HighColorMask, _Set_HighColorMaskChannel, _Set_HighColorMaskPan, _Set_HighColorMaskUV, _Set_HighColorMask_ST, _Tweak_HighColorMaskLevel |
| _STYLIZEDREFLECTIONMODE_LILTOON | _ReflectionCube*, _Apply*, _Metallic*, _Smoothness*, _ReflectionColor*, _Reflectance*, _GSAAStrength*, _SpecularBlur*, _SpecularToon*, _UseReflection*, _SpecularBorder*, _ReflectionBlendMode*, _SpecularNormalStrength*, _ReflectionApplyTransparency* |
| POI_PATHING | _Path*, _AudioLinkPath*, _EnablePathRemapping* |
| POI_MIRROR | _Visibility*, _MirrorColor*, _MirrorTextureUV*, _MirrorTexturePan*, _MirrorTextureBlendType*, _MirrorTextureForceEnabled*, _Mirror, _MirrorTexture, _MirrorTexture_ST |
| POI_MIRROR_TEXTURE | _MirrorColor*, _MirrorTextureUV*, _MirrorTexturePan*, _MirrorTextureBlendType*, _MirrorTextureForceEnabled*, _MirrorTexture, _MirrorTexture_ST |
| GRAIN | _DepthColor*, _DepthMask*, _DepthAlpha*, _DepthTexture*, _DepthEmissionStrength* |
| EFFECT_BUMP | _TextNumeric*, _TextTime*, _TextPosition*, _TextFPSColor*, _TextFPSUV*, _TextFPSScale*, _TextFPSOffset*, _TextFPSEnabled*, _TextPixelRange*, _TextFPSPadding*, _TextFPSRotation*, _TextFPSEmissionStrength*, _TextGlyphs |
| POSTPROCESS | _PPMask*, _PPHue*, _PPPosterization*, _PPHDR*, _PPRGB*, _PPTint*, _PPContrast*, _PPLightness*, _PPSaturation*, _PPBrightness*, _ppHueSelectOrShift* |
| PROP_PPMASK | _PPMask* |
| POI_INTERNALPARALLAX | _ParallaxInternalHue*, _ParallaxInternalMapMask*, _ParallaxInternalMax*, _ParallaxInternalMin*, _ParallaxInternalHeight*, _ParallaxInternalMapPan*, _ParallaxInternalBlendMode*, _ParallaxInternalIterations*, _ParallaxInternalPanDepthSpeed*, _ParallaxInternalSurfaceBlendMode*, _ParallaxInternalMap |
| POI_NORMALCORRECT | _NormalCorrectAmount*, _NormalCorrectOrigin* |
| POI_VIDEO_EFFECTS | _Video* |
| POI_BACKLIGHT | _BacklightColor*, _BacklightBlur*, _BacklightBorder*, _BacklightDirectivity*, _BacklightMainStrength*, _BacklightViewStrength*, _BacklightBackfaceMask*, _BacklightReceiveShadow*, _BacklightNormalStrength* |
| POIBS_ENABLE | _Fog* |
| POIBS_BLOOMFOG | _Fog* |
| BSSBLOOMFOGTYPE_HEIGHT | _FogHeight* |
| _STOCHASTICMODE_DELIOT_HEITZ | _StochasticDeliotHeitzDensity* |
| _STOCHASTICMODE_HEXTILE | _StochasticHex* |
| _STOCHASTICMODE_NONE | none |
| POI_VORONOI | _AudioLinkVoronoi*, _VoronoiNoise*, _VoronoiMask*, _VoronoiInner*, _VoronoiOuter*, _VoronoiRandom*, _VoronoiGlobal*, _VoronoiType*, _VoronoiPower*, _VoronoiSpace*, _VoronoiScale*, _VoronoiSpeed*, _VoronoiBlend*, _VoronoiGradient*, _VoronoiAffectsMaterialAlpha*, _VoronoiEnableRandomCellColor* |
| POI_TRUCHET | _Truchet* |
| POI_UZUMORE | _UzumoreMask*, _UzumoreBias*, _UzumoreAmount*, _UzumoreEnabled* |

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
20. `_PathPointSampling` 0 with an assigned `_PathingMap` on selector 0. Refuses, because the admitted branch would sample bilinear while the route records point facts.
21. `_UzumoreMaskUV` 0. Refuses, because the clamp sampler cannot serve a page.
22. `_VideoPixelateToResolution` 1, `_VideoType` 2, or an assigned `_VideoPixelTexture` with selector 0. Refuses.
23. Any audio UV debug flag or `_AudioLinkCCStripY` at 1. Refuses.
24. `_ALDecalUV` 0 or `_ALVolumeColorUV` 0. Refuses.
25. Each refused keyword of section 4.1. Refuses with `KeywordNotAllowed`.
26. Each engine keyword `FOG_EXP2` or `VERTEXLIGHT_ON`. Refuses as today.
27. `_SDFShadingTexture` assigned on selector 0 with `_LIGHTINGMODE_SDF`. Refuses.
28. A NaN or out of range companion selector. Refuses with `RequiredValueNotHeld`.

## 9. Limits

- The review covers the bundled 9.3 rows only. The release 9.3.64 rows keep `PoiyomiMainColor` until a review reads their source.
- Line numbers name the installed bundled files of 2026-11-23. A vendor update can move lines. The completeness tests of the plan pin the keyword and texture sets against the installed source at test time.
- The dead value families are keyword gated only. A feature gated by a float switch with no keyword stays live. This matches the first cut of the live value model.
- `_Udon_VideoTex` has no active declaration. The decal code reads it only under refused keywords. A future shader that declares it needs a new review.
- The record classes the four matcap textures and the three ramp and lookup textures as neutrals because their sample coordinates never track UV0. The matcap blend rule guards the one UV read in the matcap path. The texture ramp rule guards the row pick.
- The audit of the four feature groups derives from the base pass. The other passes repeat the same code. The Add pass adds additive shadow strengths, the outline pass gates shading behind `_OutlineLit`, and the shadow caster skips lighting, rim, matcap, decal, emission, and LTCGI sections. No pass adds a UV0 read that the base pass lacks.
