# Reviewed Poiyomi 9.3 atlas route

Privacy note: This record contains product design only. It names vendor shader properties, keywords, and line numbers. It quotes no vendor source text. It describes one private test avatar by role, and it gives its counts as ranges. It contains no machine identifiers.

Date: 2026-11-23.
Status on 2026-11-23: Approved by the user on 2026-11-23. The plan is `docs/superpowers/plans/2026-11-23-poiyomi-reviewed-atlas-route.md`.
Status on 2026-11-24: Implementation and verification are in progress.
Status on 2026-11-27: The route is implemented.

## 1. Intent

AMUSE 0.2.0 atlases the main color texture of Poiyomi 9.3 materials through the route table `AtlasRouteTable.PoiyomiMainColor`. That table admits only the seven keywords that a fresh material carries. Real Poiyomi materials turn on more features. On 2026-11-23 a read-only analysis of a private mixed Poiyomi and lilToon test avatar found that most of its Poiyomi slots refuse with `KeywordNotAllowed`, in the range of 80 to 90 slots. Only one use was admitted.

PR #207 and PR #208 solved the same problem for lilToon 2.4.1. They added companion pages for textures that read UV0, and a model that keeps unused values out of the material key. This change gives Poiyomi 9.3 the same treatment.

Why now: the page set must be merge-ready before 0.3.0 can merge materials. A route that refuses almost every real Poiyomi material gives 0.3.0 nothing to merge.

What success looks like: on the test avatar, the Poiyomi slots that use only reviewed-safe features atlas. Each remaining refusal names a feature that the review proved unsafe, or a fact outside this route, such as a locked material.

## 2. User decisions on 2026-11-23

1. Scope: every feature that the source review proves safe. The scope is not limited to the features of the test avatars.
2. Approach A in section 5: one reviewed table that is built per feature group, with the same route types as lilToon.
3. Subagents and parallel work are authorized for the implementation.

## 3. Goals, non-goals, and anti-goals

Goals:

1. A new table `AtlasRouteTable.PoiyomiReviewedMainColor`. It admits every keyword that the review classes as key-only, companion, or conditional.
2. One companion route for each texture that a reviewed feature samples at plain UV0. Section 7 lists about 95 textures.
3. A Poiyomi live value model `PoiyomiLiveValues`. A value whose feature keyword is off leaves the material key.
4. A committed keyword review record under `docs/superpowers/investigations/`. It gives one verdict per keyword with its source lines.
5. README and CHANGELOG text that states the new Poiyomi support.

Non-goals:

1. Locked Poiyomi materials. The atlas pass runs after `TransientUnlockWindowClose`. So it always reads the locked original, and the use refuses with `LockedShader`. A window for the atlas pass is a separate design.
2. The Poiyomi 10.x shader. It is not attested, and it keeps its `UnattestedShader` refusal.
3. The release Poiyomi 9.3.64 rows. The dev project installs only the 9.3 shader that the 10.x package bundles. The review read only that file. The release rows keep `PoiyomiMainColor` until a review reads their source.
4. Decals, internal parallax, flipbook, and glitter at UV0. They read UV0 as data. They stay refused.
5. Material merging. Atlasing in 0.2.0 still does not reduce draw calls or material slots.
6. Changes to the lilToon routes.

Anti-goals. Each of these makes the change a failure, even if it technically works:

1. A false positive: an admitted use renders differently after the build.
2. A table that admits a keyword whose code the review did not read.
3. A second way to declare routes next to the lilToon pattern.
4. A refusal of the whole avatar or renderer for a fact of one slot.

## 4. Constraints

- The correctness policy of `AGENTS.md` holds. Unknown facts refuse the dependent use only. Programming and invariant failures throw.
- The allowed keyword list stays an allow list. A keyword that the review did not class refuses with `KeywordNotAllowed`.
- Keywords are fixed for one build. A material swap refuses with `AnimatedMaterialSlot`. A property block cannot set a keyword. An animation cannot set a keyword. So a family whose gate is a keyword needs no dynamic guard.
- Tests use the installed bundled 9.3 shader by its pinned GUID, as `AtlasUseCaptureTests.LoadInstalledPoiyomiShader` does today. Stand-ins are allowed only where the vendor shader cannot reach a state.
- No vendor source text enters the repository. Property names, keyword names, and line numbers are allowed.
- The analysis, planning, and page build code is vendor-agnostic today. This change must keep it that way.

## 5. Approaches

| Approach | What it is | Trade-off |
|---|---|---|
| A. One reviewed table, built per feature group | A new table next to `PoiyomiMainColor`. A small helper expands the Poiyomi texture name pattern into an `AtlasCompanionRoute`. A new `PoiyomiLiveValues` model. | It reuses the lilToon pattern exactly. The four feature groups split cleanly for parallel work. The route data is long, but each entry is plain. |
| B. A feature descriptor type | A new `PoiyomiFeature` type holds the keyword, textures, selectors, and conditions. It generates the routes and the live model together. | Less repeated data. But it adds a second way to declare routes next to the lilToon one. The repository rules forbid a second convention. |
| C. Staged delivery | First the allow list and the key-only features. Then the companions in a second change. | The first step is small. But most real features assign a texture, so the first step admits little on real avatars. |

The user chose A on 2026-11-23. Reason: it reuses the lilToon pattern, it splits for parallel work, and every texture gets a test.

## 6. The coordinate rule

Facts from the source review of 2026-11-23:

- Poiyomi fills ten coordinate slots per fragment. Slots 0 to 3 are the mesh channels UV0 to UV3. Slot 4 is the panosphere, slot 5 is world position, slot 8 is local position or vertex color, and slot 9 is the matcap view projection. These never read UV0.
- Slot 6 is the polar coordinate. It is built from the channel that `_PolarUV` selects. The default is UV0.
- Slot 7 is the distorted coordinate. It is a copy of UV0. Under `USER_LUT` it is UV0 plus a flow offset.
- Almost every texture has a selector property `<T>UV`, a transform `<T>_ST`, a pan `<T>Pan`, and often a stochastic flag `<T>Stochastic`.

The rule for one assigned texture `<T>` whose feature is live:

| Selector value | Use | Condition |
|---|---|---|
| 0 | `Uv0`. The texture moves into a companion page. | `<T>_ST` is (1,1,0,0). `<T>Pan` is zero. `<T>Stochastic` is 0 when it exists. |
| 1, 2, 3, 4, 5, 8, 9 | `OtherCoordinates`. The texture stays bound and stays in the key. | None. |
| 6 or 7 | Refuse with `RequiredValueNotHeld`. | The coordinate is UV0 data. Neither a page nor an unmoved texture keeps it correct. |
| Any other value, or NaN | Refuse with `RequiredValueNotHeld`. | Fail closed. |

The selector, transform, pan, and stochastic properties of every assigned companion texture become condition properties. So no clip and no property block may touch them.

Some textures differ from the pattern. Section 7 lists each one. Examples: `_SmoothnessTex` and `_ReflectionColorTex` sample raw UV0 with no selector or transform. `_RimEnviroMask` has a selector and a pan but no transform. The global mask and RGBA textures have extra split transforms that must also be identity.

## 7. Feature verdicts

The full record with source lines and per-texture conditions becomes the committed investigation `docs/superpowers/investigations/2026-11-23-poiyomi-reviewed-route-keywords.md`. Its input is the five read-only review reports of 2026-11-23. Those reports are session artifacts and are not committed. The first task of the plan writes the record from them. Later tasks read the record, not the reports.

Coverage check on 2026-11-23: the base pass of the bundled 9.3 Toon shader declares 106 keywords. The reports class 104 of them. The other two, `FOG_EXP2` and `VERTEXLIGHT_ON`, are global keywords that the engine sets at render time. The shader declares 139 texture properties. The reports class all of them. Four are not named in this document: `_ClearCoatFallback` is a neutral cube, the glitter color map and glitter mask belong to a refused feature, and `_Udon_VideoTex` is a commented-out property that only the refused decals read. The record carries the complete classified inventory.

The verdict classes:

- KeyOnly: the feature never reads UV0. The keyword is allowed. Its textures stay bound and keyed (neutral).
- Companion: the feature reads UV0 only to sample textures. The keyword is allowed. Its textures get companion routes under section 6.
- Conditional: safe only under named values. The keyword is allowed, and a required-value rule refuses the use when the values do not hold.
- Refuse: the keyword stays off the allowed list.

Texture keywords. Poiyomi declares a `PROP_<texture>` keyword for some textures, for example `PROP_BUMPMAP` or `PROP_LIGHTINGAOMAPS`. The Thry editor turns it on when the texture is assigned. Each such keyword takes the verdict of its texture. It is allowed when its texture is the member, a neutral, or a companion. It is refused when its texture belongs to a refused feature. In some branches the keyword decides whether the sample runs. The route treats an assigned texture of a live feature as sampled in every case. That is safe whichever way the keyword decides.

### 7.1 KeyOnly

`_LIGHTINGMODE_FLAT`, `_LIGHTINGMODE_REALISTIC`, `_LIGHTINGMODE_WRAPPED`, `POI_LTCGI`, `_POI_DEPTH_RIMLIGHT`, `POI_BLACKLIGHTMASKING`, `POI_MIRROR`, `POI_VERTEX_GLITCHING`, `POI_VERTEX_GLITCHING_TEXTURE`, `POI_NORMALCORRECT`, `VIGNETTE_MASKED`, `POIBS_ENABLE`, `POIBS_BLOOMFOG`, `BSSBLOOMFOGTYPE_HEIGHT`, `_STOCHASTICMODE_NONE`, `KEYWORD`, `GEOM_TYPE_MESH`, `PROP_MAINGRADATIONTEX`.

Neutral textures that these and other features sample at coordinates other than UV0: the four matcap textures, `_CubeMap`, `_ReflectionCubeTex`, `_MochieReflCube`, `_ClearCoatFallback`, `_MainGradationTex`, `_ToonRamp`, `_SkinLUT`, `_ClothDFG`, `_VertexGlitchMap`, `_DissolveEdgeGradient`, `_TextGlyphs`. The record gives the complete list.

### 7.2 Companion and Conditional, by feature group

| Group | Keywords | Companion textures | Extra conditions |
|---|---|---|---|
| Always on | none | `_BumpMap`, `_AlphaMask`, `_LightingAOMaps`, `_LightingDetailShadowMaps`, `_LightingShadowMasks`, `_OutlineMask`, `_OutlineTexture` | `_LightingMapMode` is not 4. Light map mode 4 samples `_LightDataSDFMap` with explicit gradients from the UV0 derivative. This texture is not `_SDFShadingTexture`, which belongs to the SDF lighting mode in the next row. |
| Lighting | the six other `_LIGHTINGMODE_*` modes, `POI_BACKLIGHT`, `POI_SUBSURFACESCATTERING`, `POI_CLEARCOAT`, `MOCHIE_PBR`, `GGX_ANISOTROPICS`, `POI_ANISOTROPICS`, `_CUBEMAP`, `POI_STYLIZED_StylizedSpecular`, both `_STYLIZEDREFLECTIONMODE_*`, `POI_ENVIRORIM` | `_MultilayerMathBlurMap`, `_ShadowBorderMask`, `_ShadowStrengthMask`, `_ShadowColorTex`, `_Shadow2ndColorTex`, `_Shadow3rdColorTex`, `_1st_ShadeMap`, `_2nd_ShadeMap`, `_SkinThicknessMap`, `_ClothMetallicSmoothnessMap`, `_BacklightColorTex`, `_SSSThicknessMap`, `_ClearCoatMaps`, `_MochieMetallicMaps`, `_AnisoColorMap`, `_AnisotropyMap`, `_CubeMapMask`, `_HighColor_Tex`, `_Set_HighColorMask`, `_SmoothnessTex`, `_ReflectionColorTex`, `_RimEnviroMask` | Texture ramp: `_ToonRampCount` at most 1, or `_ToonRampUVSelector` not 0. SDF mode: `_SDFShadingTexture` unassigned, or its selector not 0. Multilayer border mask: `_ShadowBorderMaskLOD` is 0. Mochie split mask: its own selector, merged transform, pan, and stochastic rules. |
| Matcap, rim, emission, masks | `POI_MATCAP0`, `COLOR_GRADING_HDR_3D`, `POI_MATCAP2`, `POI_MATCAP3`, the four `POI_MATCAP*_CUSTOM_NORMAL`, `_GLOSSYREFLECTIONS_OFF`, the three `_RIMSTYLE_*`, `POI_RIM2`, the three `_RIM2STYLE_*`, `_EMISSION`, `POI_EMISSION_1` to `_3`, `POI_GLOBALMASK_TEXTURES` | four matcap masks, four matcap custom normal maps, `_RimMask`, `_RimTex`, `_Set_RimLightMask`, `_RimColorTex`, the four rim 2 equivalents, four emission maps, four emission masks, four global mask textures | Matcap blend: the `UVToBlend` selector is not 0, 6, or 7, or the blend vector is zero. Emission: center-out off when the map is on selector 0. Scrolling curve off or unassigned. Global masks: split transforms identity and split pans zero. |
| Color, effects, UV | `COLOR_GRADING_HDR`, `POSTPROCESS`, `PROP_PPMASK`, `VIGNETTE` with its `PROP_*` keywords, `FINALPASS`, `GRAIN`, `AUTO_EXPOSURE`, `DISTORT`, `USER_LUT`, `EFFECT_BUMP`, `_STOCHASTICMODE_DELIOT_HEITZ`, `_STOCHASTICMODE_HEXTILE`, `POI_BACKFACE`, `POI_MIRROR_TEXTURE`, `POI_VERTEX_LOOKAT`, `POI_DEPTHBULGE`, `POI_PARALLAX`, `POI_TRUCHET`, `POI_VORONOI`, `POI_UDIMDISCARD`, `POI_PATHING`, `POI_UZUMORE`, `POI_VIDEO_EFFECTS`, `POI_AUDIOLINK`, `POI_AL_DECAL`, `POI_AL_VOLUMECOLOR` | `_MainColorAdjustTexture`, `_PPMask`, `_RGBMask`, the four channel textures, the four channel normal maps, `_RGBAMetallicMaps`, `_RGBASmoothnessMaps`, `_DetailTex`, `_DetailMask`, `_DetailNormalMap`, `_DepthMask`, `_DepthTexture`, `_VertexBasicsMask`, `_VertexManipulationHeightMask`, the four dissolve textures, the two distortion flow textures, `_DistortionMask`, `_BackFaceTexture`, `_BackFaceMask`, `_MirrorTexture`, `_LookAtMask`, `_DepthBulgeMask`, `_TruchetMask`, `_VoronoiMask`, `_VoronoiNoise`, `_PathingMap`, `_PathingColorMap`, `_VideoMaskTexture`, `_ALDecalColorMask` | Each feature that reads UV0 as data is safe only when its selector leaves UV0. Distortion: `_DistortionUvToDistort` not 0. Dissolve: UV tile dissolve off or not on UV0. Text overlay: all four text selectors not 0. Vertex wind off when `_VertexBasicsMask` is a companion. Parallax, truchet, AL decal, AL volume color: their UV selectors not 0. Voronoi: space is not UV. UV tile discard: all row flags zero or the selector not 0. Pathing: source is not UV on UV0. Video: pixelate off, CRT off, pixel texture not on UV0. AudioLink: the UV debug modes and the CC strip Y off. |

Several conditional features refuse at their default values, for example parallax, truchet, and the AL decal. A user who keeps the default gets a refusal that names the selector.

### 7.3 Refuse

`GEOM_TYPE_BRANCH`, `GEOM_TYPE_BRANCH_DETAIL`, `GEOM_TYPE_FROND`, `DEPTH_OF_FIELD_COC_VIEW` (the four decals), `PROP_DECALMASK`, `POI_INTERNALPARALLAX`, `_SUNDISK_HIGH_QUALITY` (flipbook), `_SUNDISK_SIMPLE` (glitter).

Flipbook and glitter are Conditional in the review when their selector is 1 to 3. The first cut refuses them as whole keywords, because the glitter texture also reads the UV0 derivative. This is my call. It loses little coverage.

A refused feature still needs a live value family. Without one, an assigned texture of the feature counts as live while its keyword is off, and the use refuses with `UnlistedTexture`. With the family, a material that keeps an old decal texture but has the decal turned off still atlases.

## 8. The table and its gate

- `AtlasRouteTable.PoiyomiReviewedMainColor` is a superset of `PoiyomiMainColor`. It keeps the member `_MainTex`, the main transform, and every main condition and required value of the old table.
- `PoiyomiAdmittedSource` gets a flag that marks the rows whose source the review read. The flag is true on the two bundled 9.3 rows only. `AtlasUseCapture.AttestRow` picks the reviewed table when the matched row carries the flag. It picks `PoiyomiMainColor` otherwise. This mirrors the lilToon version gate.
- A helper builds one Poiyomi companion route from a base name, a sampler, a feature gate, and the optional extra properties. It applies section 6. The use is `Inert` when the feature keyword is off. The exceptions in section 7 are written out by hand.
- The table-level required-value function checks the Conditional rules of section 7.2. Each failure returns the first failing sentence, as the lilToon route does.
- The four feature groups keep their route data in separate source files, so parallel tasks do not edit one file. The plan picks the file names inside the folder conventions.

## 9. Samplers

Most Poiyomi textures sample through `sampler_MainTex`. Those routes use `AtlasCompanionSampler.Member`. Some textures use named inline samplers: `sampler_linear_repeat`, `sampler_trilinear_repeat`, `sampler_point_repeat`, and the clamp variants. Today the enum has only `Own`, `Member`, and `InlineTrilinearRepeat`.

- The change adds `InlineLinearRepeat` and `InlinePointRepeat`, with the same handling as `InlineTrilinearRepeat`.
- A companion that a clamp inline sampler reads refuses at selector 0. This is my call. The known case is `_UzumoreMask` with selector 0. Its default selector is 3, so the default state stays admitted.
- The builder must read the sampler of each companion from the source. A wrong sampler kind is a false positive.

## 10. Live values

`PoiyomiLiveValues.Model` follows `LilToonLiveValues`:

- One family per feature keyword. `IsOff` is true when the keyword is not enabled on the material. `Switches` names the keyword for the record. The dead value lists come from the review reports.
- Families gated by a float switch with no keyword stay live. This first cut models keyword gates only. This is my call. Keyword gates cover the large families.
- Stale enum keywords. A real material can hold two keywords of one enum set, for example two lighting modes. The model keeps every enabled keyword live, and the route applies the conditions of every enabled keyword. Both possible variants are then safe. The page material copies the keywords verbatim, so it compiles the same variant as the source. This is my call. It admits more than a refusal would, and it stays exact.
- `DeclaredUniforms` lists every uniform name that any compile branch of the two bundled rows declares, plus every property name. A data test checks the pinned list against the installed shader source. A saved name outside the list is stale, and it leaves the key.
- The model has a family for every keyword in section 7, including the refused keywords of section 7.3.
- The VRChat fallback rules stay as they are. The shader tag `VRCFallback` is `Standard`, so `FallbackPropertyNames` stay keyed. A material tag that selects a toon standard fallback keeps every value live.

## 11. Verification

Every test runs against the installed bundled 9.3 shader, loaded by GUID.

1. Completeness tests:
   - Every texture property of both bundled rows is the member, a member copy, a neutral, a companion route, or a texture of a refused keyword. A new texture in a vendor update fails this test.
   - Every keyword in the shader keyword space is on the allowed list or on the refused list.
   - Every property that a route or a table rule reads exists on both bundled rows with the expected type. This covers companion properties and every property of a table-level required-value rule, for example `_LightingMapMode`. `Material.GetFloat` returns 0 for a missing name, so a misspelled rule would otherwise pass in silence.
   - The keyword test covers the local keywords of the shader keyword space. Global engine keywords such as `FOG_EXP2` stay outside both lists. A material that enables one refuses as it does today.
2. Capture tests per feature group: one admitted case per feature, and one falsifier per condition. Required falsifiers: selector 6, selector 7, a non-identity transform, a non-zero pan, a stochastic flag of 1, a stale second lighting mode keyword, each named Conditional rule, and each refused keyword.
3. Live value tests: model data tests and a render oracle per family, as `LilToonLiveValuesRenderTests` does. Each dead case has no-op guards. Each family has a liveness guard.
4. Render-backed end-to-end tests: one per feature group. Two materials with companion textures share pages, and the build renders the same as the source within 1 per channel.
5. Full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` runs before each push.
6. A read-only rerun of the atlas analysis on the Census Lab test avatar. The main agent runs it through the Census Lab editor instance after the full test runs pass. The record gives counts per cause as ranges.

## 12. What the builder decides

- File names and the exact helper signature.
- The task split of the plan, inside the four feature groups.
- The text of each new required-value sentence.
- Whether a family needs a fixture override in the render oracle, as `LilToonLiveValuesRenderTests.FamilyFixtureOverrides` does.

## 13. Calls made by the designer

These were not user decisions. The user can change any of them.

1. The reviewed table covers only the two bundled 9.3 rows (section 3, non-goal 3).
2. Flipbook and glitter refuse as whole keywords (section 7.3).
3. Clamp inline sampled companions refuse at selector 0 (section 9).
4. The live model covers keyword gates only (section 10).
5. Stale enum keywords are admitted with the union of their conditions, not refused (section 10).
6. Selector 6 and selector 7 refuse even when `_PolarUV` is not 0 or `USER_LUT` is off (section 6). Both cases are rare.
