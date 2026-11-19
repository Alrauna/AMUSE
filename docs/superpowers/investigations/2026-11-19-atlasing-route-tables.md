# Route tables for the main color route

Privacy note: This record contains product design and facts about installed vendor shaders. It names vendor shader properties and keywords. It contains no vendor source excerpts, no private avatar observations, and no machine identifiers.

Date: 2026-11-19.
Status on 2026-11-19: This record closes Gate 0 item 1 of `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md`. It changes no product code. User approval of the design remains open.

## 1. Question

The atlas pass rewrites one UV channel and moves the main color texture into a page. Which material states make that change safe? A state is safe when every shader read of the rewritten channel is one of these:

- a sample of a member texture
- a sample of a texture that the use does not assign

Reads of UV values as data, reads of texture size, and moved coordinates are not safe.

## 2. Method

The review read the installed vendor shader source of the pinned versions. A throwaway scan helped the reading. It did not replace the reading. The scan stayed outside the repository.

- The scan removed comments with a small lexer. A first version that used a pattern match removed real code. That version was discarded.
- It followed the preprocessor branches. It treated a feature keyword that the row declares as off, unless the keyword was on the allowed list.
- It marked every line that reads a UV channel, a texture size, or a screen derivative. It also marked every call of a function whose body does so. The function set was closed by a fixed-point search.
- A keyword passed when turning it on added no marked line.

The review then read every marked line that the allowed state can reach.

## 3. Decision: an allowed list, not a ban list

Poiyomi 9.3 gates its features with 96 local feature keywords. An allowed list of keywords is complete by construction. A keyword that is not on the list refuses the use, so a feature that the review missed cannot pass. A ban list is complete only when the review finds every unsafe feature.

lilToon gates its features with uniform switches. It has no local feature keywords in the reviewed rows. The review read every switch whose code can read UV values. The lilToon table is therefore a list of required values.

Both tables use the same rule for textures. An assigned texture that is on neither the member list nor the neutral list refuses the use. This rule covers every mask, map, and level floor that reads the route channel through a texture. The older ban list entries for the lilToon shadow-mask level floor and for texture-driven features are therefore not needed.

The route channel is UV0 for both families in 0.2.0. A Poiyomi main texture on another channel refuses. Design section 12 already defers spare channel routes.

## 4. Poiyomi 9.3 rows

Rows: Poiyomi Toon and Poiyomi Toon Two Pass. Each row covers the 9.3.64 release shader and the 9.3 shader that Poiyomi 10.x packages bundle. Both shader files declare the same local feature keywords, except for inert keywords that read no UV. The Two Pass file has no extra reachable UV read.

- Member: `_MainTex` on UV0.
- Neutral: `_ToonRamp`, `_SkinLUT`, `_ClothDFG`, `_DistortionFlowTexture`, and `_DistortionFlowTexture1`. A fresh material assigns these five textures. No code that the allowed state reaches samples them.
- Allowed local feature keywords: `BSSBLOOMFOGTYPE_HEIGHT`, `VIGNETTE_MASKED`, `_LIGHTINGMODE_FLAT`, `_RIMSTYLE_POIYOMI`, `_RIM2STYLE_POIYOMI`, `_STOCHASTICMODE_DELIOT_HEITZ`, and `_STYLIZEDREFLECTIONMODE_UNITYCHAN`. These are the keywords of a fresh material. A use may have any subset of them. Any other local feature keyword of the row refuses the use. Keywords that the row does not declare have no effect and are ignored.
- Required values:
  - `_MainTexUV` is 0.
  - `_MainPixelMode` is 0. Pixel mode reads the main texture size.
  - `_MainTexStochastic` is 0. Stochastic sampling moves the coordinates.
  - `_AlphaSharpenedA2C` is 0 or `_AlphaToCoverage` is 0. Sharpened alpha to coverage reads UV0 with the main texture size.
  - `_RenderingAOBlockerEnabled` is 0. The blocker reads UV values as data in the vertex stage.
  - `_MainTex_ST` is identity and `_MainTexPan` is zero. Design section 4 condition 7 already requires this.

Coverage limit: a Poiyomi material that turns on any feature beyond the fresh defaults refuses in 0.2.0. Examples are matcap, emission, rim, other lighting modes, and decals. Each later feature is one keyword entry, its required values, and one falsifier.

## 5. lilToon rows

Rows: `lilToon`, `Hidden/lilToonCutout`, `Hidden/lilToonTransparent`, `Hidden/lilToonOutline`, `Hidden/lilToonCutoutOutline`, `Hidden/lilToonTransparentOutline`, `Hidden/lilToonOnePassTransparent`, `Hidden/lilToonTwoPassTransparent`, `Hidden/lilToonOnePassTransparentOutline`, and `Hidden/lilToonTwoPassTransparentOutline`. lilToon Multi has no route table in 0.2.0 and refuses with a name.

The main texture always reads UV0 in these rows. The outline pass samples the outline texture through the outline transform. It does not sample `_MainTex`.

- Member: `_MainTex` on UV0.
- Neutral: none. A fresh material assigns no texture.
- Required values:
  - `_ShiftBackfaceUV` is 0. The shift adds one to U on back faces.
  - `_UseParallax` is 0. Parallax and occlusion parallax move the coordinates without a texture.
  - `_UseMain2ndTex` and `_UseMain3rdTex` are 0. Decal mode reads UV values as data.
  - `_UseGlitter` is 0. Glitter reads UV values as positions.
  - `_UseAudioLink` is 0. AudioLink can read UV values as data in the fragment and vertex stages.
  - The rounded first component of `_DissolveParams` is 0. Dissolve reads UV values as data.
  - Every `_UDIMDiscardRow` flag is 0, or `_UDIMDiscardUV` is not 0. The pixel mode does not check the compile switch.
  - Every `_IDMask` and `_IDMaskPrior` flag is 0, or `_IDMaskFrom` is not 0. IDMask reads UV values as identifiers.
  - `_MainTex_ST` is identity and `_MainTex_ScrollRotate` is zero. Design section 4 condition 7 already requires this.

## 6. Static condition properties

Every property in the required values above is a condition property. No clip may animate a condition property. No property block entry may set one. A violation refuses the affected uses, as design section 4 condition 5 states for route properties.

## 7. Falsifiers

Each entry gets one falsifier that a plausible wrong implementation fails:

- One non-allowed Poiyomi keyword, for example `POI_UDIMDISCARD`, refuses the use.
- A Poiyomi main texture on UV1 refuses the use.
- One falsifier per required value, with the value set to its unsafe state.
- An assigned non-member texture that is not neutral refuses the use.
- A fresh Poiyomi material with its five default textures is admitted.
- A lilToon Multi material refuses with its own name.
- An animated condition property refuses the use.

## 8. Limits

- The scan is a reading aid. It treats compile-time lilToon feature defines as possibly on. The pinned shader digest fixes the reviewed source.
- The review covers the forward, outline, shadow caster, early depth, and meta passes of the rows above. It does not cover fur, gem, refraction, tessellation, lite, or Multi shaders.
- Locked Poiyomi materials refuse in 0.2.0 under design section 4 condition 4. This review covers unlocked materials only.
