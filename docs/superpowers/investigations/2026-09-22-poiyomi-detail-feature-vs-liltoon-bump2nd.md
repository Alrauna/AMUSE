# The poiyomi detail feature versus the liltoon second normal map - characterization

## Privacy note

This record opens with a privacy statement. It describes private Census
Lab fixtures by role only. It names no avatar, renderer, material,
texture, mesh, hierarchy, scene, or folder. It records no machine
paths, no host names, no ports, and no instance names or hashes. It
records no GUIDs and no generated shader hash fragments. The
designated locked denim garment fixture appears by role, and its
detail facts appear as property values. Exact triangle counts appear
as ranges. Every status claim carries its date. Property names, enum
values, and vendor line facts are public facts of the pinned shader
sources.

## 1. Scope and method

Date: 2026-09-22. Branch: `feat/poiyomi-detail-feature`, cut from
`main` at the merge of the decal slot pull request. This session
changes no production code. It produces this record only.

The question, from the session brief: where does `_DetailEnabled`
refuse as of 2026-09-22, what does the vendor detail block really do, what is the
liltoon counterpart, who consumes the outputs it blocks, and what does
the feature do to the designated fixture. The brief also asks whether
the decal slice of the same date changed the fixture's renderer
outcome, and to prove which claim is true.

Sources and probes, all read or run on 2026-09-22:

- The pinned Poiyomi Toon Shader 9.3.64 source, read from the
  installed copy in the Census Lab project, read only. The main
  shader file carries the detail block; the Two Pass source carries
  the same block.
- The pinned lilToon 2.3.4 source, read from the same installed
  location, read only.
- The committed frontend code of this repository.
- One Lab probe run of the designated locked denim garment fixture
  through the project's own soundness probe on current `main`, after
  the identity check of the Census Lab editor instance. The probe made
  a snapshot prefab of the scene carrier, built it through the SDK
  preprocess chain, and destroyed the build copy.
- One sanctioned clone-route probe: copy the locked material, unlock
  the copy with the vendor method the production unlock window calls,
  read the detail properties, run the production frontend entry, and
  destroy the copy in the same handler. The fixture stayed locked and
  untouched throughout. All temporary assets were deleted and the
  deletion was swept.

The NDMF records of the probe build were read through reflection on
the NDMF error report list. The report exists only inside the build
scope, so the reader accumulated every record it observed during the
whole preprocess and wrote the accumulated set out afterwards. The
AMUSE renderer record of the run carries no substitution strings, so
the exact alpha diagnostic does not appear in the NDMF record itself.
The clone-route frontend answer supplies that diagnostic, and the gate
code shows both paths consult the same gate array, which carries the
answer over.

## 2. The fixture on current main, observed 2026-09-22

The probe ran on `main` at the decal merge commit. Observed outcome:

- The probe accepted the avatar. One renderer before, one after.
- The renderer kept everything original. The moved-triangle
  composition was zero in all three classes, over a plane mesh of
  roughly half a million triangles. No violations.
- Six distinct NDMF records appeared. Four came from upstream
  optimizer passes (an update notice, two unknown-component
  warnings, one metrics line). The two AMUSE records were
  `amuse.renderer.AdmittedMaterialSemanticsUnknown` from the semantic
  barrier and `amuse.summary.Title` with the counts analyzed zero,
  moved zero, kept one.

So the renderer outcome after the decal slice is the same as on
2026-09-21: refused as semantics unknown, nothing moved. What changed
is which alpha feature the refusal has reached. On 2026-09-21 the
alpha output refused naming `_AlphaPremultiply`. The premultiply and
mask slices removed that refusal, and the decal slice admitted inert
decal slots and gated `_ALDecalControlsAlpha`, which the fixture
passes. On 2026-09-22 the alpha output refuses naming
`_EnableRimLighting`.

The proof for that diagnostic: the sanctioned clone-route probe ran
the production frontend entry `AnalyzeBaseMaterial` on the unlocked
copy. The result was a supported material with all four outputs
incomplete, and exactly four diagnostics: base color, emission, and
normal each refuse naming `_DetailEnabled`, and the alpha output
refuses naming `_EnableRimLighting`. The clone carried
`_EnableRimLighting` one and `_EnableRim2Lighting` one, so both rim
features are on. The build path resolves alpha through the same
`AlphaFeatureGates` array at its only consult site (section 3), so the
build's renderer refusal corresponds to the same alpha unknown. The
NDMF record of the build agrees on the refusal name; only the finer
diagnostic stays out of the record.

Answer to the brief's Step 0 question: the decal design's claim that
renderer admission consumes the alpha output only is code-true, but
for this fixture it has not yet become the deciding fact. The alpha
output still refuses, now at the rim lighting gate. The detail feature
is not the blocker and never was: it appears in no alpha gate array
(section 3) and writes no alpha in the vendor source (section 4). The
fixture needs a rim lighting admission before renderer admission can
consume the alpha output at all.

## 3. Gate map, current `main`, 2026-09-22

All line numbers cite
`Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`.

- `_DetailEnabled` sits in `BaseColorFeatureGates`, declared at lines
  152 to 201, entry at line 158. The base color output consults the
  list at line 550.
- The emission output consults the same `BaseColorFeatureGates` at
  line 1908, before its own `EmissionSlot0Modifiers` consult at line
  1947. This is why emission was observed refusing detail by name.
- `_DetailEnabled` sits in `NormalFeatureGates`, declared at lines 355
  to 364, entry at line 357. The normal output consults the list at
  line 2087.
- `AlphaFeatureGates`, declared at lines 244 to 263, does not contain
  `_DetailEnabled`. The alpha output consults that list at line 1149,
  on every non-forced alpha path. Nothing about the detail feature
  reaches the alpha output through the frontend.
- The alpha evidence request unions the alpha gates, the coverage
  gates, the parallax gates, and the decal slot names at lines 2498 to
  2503. The base color and normal gate arrays ride the full material
  request and the direct material reads, not the alpha capture.

## 4. The vendor mechanism, pinned 9.3.64 source

Line numbers cite the main shader file
(`_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader` of the pinned
package).

### 4.1 Properties, lines 126 to 165

Line 126 carries the lock-strip directive `ifex _DetailEnabled==0`.
Line 128 declares `_DetailEnabled` as a float with
`[ThryToggle(FINALPASS)]`, so the block compiles under the borrowed
`FINALPASS` keyword and the serialized float is the feature's enable
state. The same float-keyword duality as the decal slots exists here;
the decal design of 2026-09-22, section 5, already fixed the trust
boundary: the float is the evidence.

The block declares `_DetailMask` at line 129 (an RGBA pack: red is the
texture mask, green the normal mask), `_DetailTex` at line 136 with
the declared default `gray`, `_DetailTexIntensity` at line 141
(default one), `_DetailBrightness` at line 142 (default one), and
`_DetailNormalMap` at line 148 (declared default `bump`) with
`_DetailNormalMapScale` at line 149 (default one). All samplers carry
their own UV selector and pan.

### 4.2 The two functions, lines 20105 to 20151

Both functions sit inside `#ifdef FINALPASS`.

`ApplyDetailColor`, lines 20105 to 20115, samples `_DetailTex.rgb`
through the detail UV, multiplies by the theme tint, and writes one
line at line 20113: the base color's rgb is multiplied by
`LerpWhiteTo(detailTexture * _DetailBrightness *
unity_ColorSpaceDouble.rgb, detailMask.r * _DetailTexIntensity)`. The
write touches only `poiFragData.baseColor.rgb`.

`ApplyDetailNormal`, lines 20116 to 20151, reads `_DetailMask.rg`
into the detail mask (lines 20120 to 20124), applies the back-face
intensity under the back-face feature, applies two global-mask blend
branches onto the mask itself (lines 20132 to 20139), and writes the
normal at lines 20141 to 20143: the tangent-space normal becomes
`BlendNormals(unpacked _DetailNormalMap at _DetailNormalMapScale *
detailMask.g, previous tangent-space normal)`.

### 4.3 The alpha fact

Across lines 20105 to 20151 there is no read or write of
`poiFragData.alpha`. The chain alpha is written at lines 29780 to
29781 as the main texture alpha times the color alpha, consumed by the
alpha mask block at 29864 to 29876, the alpha options at 29888, the
decal slots at 29976, the vendor premultiply at 30216, and the clip at
30396 to 30401. The detail calls do not sit in that chain:

- `ApplyDetailNormal` is called at line 29159, under the guard at line
  29158 (`FINALPASS`, and not in the shadow caster or outline passes),
  right after the main normal unpack. It composes normals only.
- `ApplyDetailColor` is called at line 29898, under the guard at line
  29897, inside the lighting-stage color composition, before the
  decal call at 29976. It composes base color only.

So the alpha fact with line numbers: the detail feature's entire code,
lines 20105 to 20151, writes only base color rgb (line 20113) and the
tangent-space normal (lines 20141 to 20143), and never touches the
chain alpha written at 29780 and later consumed at 29888, 29976,
30216, and 30396 to 30401. This matches the frontend's gate lists: no
alpha gate names detail.

The Two Pass source carries the same block with the same shape
(`ApplyDetailColor` defined at line 20203, called at line 30017), so
the family shares the mechanism. A future admission must declare the
family it covers, as the decal work noted.

### 4.4 The default shape of the texture arm

With `_DetailTex` unbound, the sample is the declared `gray` default.
In a linear-space project the gray half times the color-space double
of two composes to one, so the texture arm is an identity multiply at
the vendor defaults (tint one, theme index zero, brightness one,
intensity one), whatever the mask weight is. The `[sRGBWarning]` on
the property marks exactly this dependence. A texture-backed detail
claim would need a texture envelope; an unbound default claim would
need the color-space premise stated.

## 5. The liltoon counterpart, pinned 2.3.4 source

lilToon has no feature named detail. The keyword replacement file
undefines the borrowed Unity detail keywords, and the shared fragment
include contains no detail feature. The counterpart map:

One to one, the detail normal:

- `lil_common_frag.hlsl` lines 577 to 597 define the second normal
  map. The runtime float `_UseBump2ndMap` gates it (line 588), the UV
  mode selects UV0 to UV3 (lines 589 to 591), the sample happens at
  line 592, the scale composes with the red channel of
  `_Bump2ndScaleMask` at uvMain (lines 579, 593 to 594), and line 595
  blends it into the normal.
- The correspondence: `_DetailNormalMap` versus `_Bump2ndMap`;
  `_DetailNormalMapScale * detailMask.g` versus
  `_Bump2ndScale * _Bump2ndScaleMask.r`; `_DetailNormalMapUV` versus
  `_Bump2ndMap_UVMode`; `BlendNormals` versus `lilBlendNormal`.

Not one to one:

- Poiyomi packs both masks into one texture: red weights the texture
  arm, green the normal arm. lilToon has no texture arm and a separate
  scale-mask texture read at uvMain.
- Poiyomi adds brightness and intensity scalars, the back-face
  intensity, and the global-mask blend branches. lilToon has none of
  these on the second normal.
- The detail texture arm, a mask-weighted multiply into the base
  color, has no dedicated lilToon feature. The nearest instrument is
  the main second layer's multiply blend, which the decal
  characterization of 2026-09-22 already mapped to the poiyomi decal
  slots.
- Both sides share the compile duality: poiyomi compiles the block
  under `FINALPASS`, lilToon under its `LIL_FEATURE` keyword, and both
  keep a runtime float as the read evidence.
- Neither feature touches the alpha value on either side, so the
  alpha parity of the inert shape holds trivially.

## 6. Consumption, current code

The build consumes exactly one semantic output. Renderer admission and
runtime-state resolution read alpha-only semantics through
`UnityMaterialSemantics.AnalyzeAlphaMaterial` at
`Analysis/AdmittedMaterialStates.cs` line 221 and
`Host/UnityRendererAlphaAnalysis.cs` line 575. The full material
analysis (`AnalyzeBaseMaterial`) has two consumers outside the tests:
the research census family check, which reads only
`IsSupportedMaterial`, and the test assemblies. No build path consumes
base color, emission, or normal.

Consequence: as of 2026-09-22, admitting the detail feature would buy
nothing. It would change no rendered decision, because the outputs it
blocks have no consumer in the build. The fixture's real blocker is the rim
lighting gate on the alpha output, which is a different feature with
its own vendor mechanism and its own future envelope.

## 7. Fixture facts, sanctioned clone route, 2026-09-22

The unlocked clone of the designated locked denim garment fixture
carried 22 detail properties:

- `_DetailEnabled` one.
- `_DetailMask` unbound, pan zero, UV zero, stochastic off.
- `_DetailTint` one, theme index zero.
- `_DetailTex` unbound, pan zero, UV zero, stochastic off, intensity
  one, brightness one, global mask off, blend type two.
- `_DetailNormalMap` bound, scale one, pan zero, UV zero, stochastic
  off, global mask off, blend type two.
- `_BackFaceDetailIntensity` one; the back-face feature itself is off.

So on this fixture the detail feature is not visually inert: the bound
normal map at scale one with mask weight one genuinely perturbs the
normal. The texture arm composes to the identity shape of section 4.4
at these values. The alpha output is untouched by all of it, which the
build's own admission behavior of 2026-09-21 and 2026-09-22 already
demonstrated live.

Other clone facts read the same day, for the record: premultiply one,
mask mode one (replace), mode preset three, cutoff 0.001, decal slot
zero enabled with override alpha zero, decal controls-alpha zero,
force opaque zero, back-face off, RGB mask off, audio link off, rim
lighting one, rim lighting two one.

## 8. Routing

- Defect: none. As of 2026-09-22 the detail feature over-claims
  nothing. The alpha path never consults it and the vendor source never
  lets it reach alpha, so no gate is missing and no gate is wrong.
- Coverage gap, recorded not expanded: the fixture's alpha refusal
  advanced to `_EnableRimLighting`, with `_EnableRim2Lighting` waiting
  behind it. That is the rim lighting feature's gap, not the detail
  feature's. It needs its own owner decision and envelope before this
  fixture can be admitted.
- Nothing to do, recommended for the detail feature: no production
  change. The outputs the detail gates block have no build consumer,
  so an unnecessary-refusal defect cannot materialize yet. If a future
  consumer of base color or normal appears, the design starts from the
  facts here: an unbound texture arm with a stated color-space premise,
  a bound normal arm that needs a normal-envelope shape, and the
  float-keyword trust boundary the decal design already fixed.

A correction to the decal record of the same date, section 5: the
sentence that the detail feature still blocks the renderer through its
own gates is imprecise as of 2026-09-22. The detail gates block the
base color, emission, and normal outputs only. The renderer refusal of
the fixture comes from the alpha path, which refuses at rim lighting.
The decal record's decal facts are unaffected.

## 9. Open questions

1. Rim lighting. Whether any provable sub-domain exists for the rim
   terms, and what the fixture's rim configuration costs. This is the
   next real blocker for the fixture and needs its own
   characterization.
2. Keyword duality. Whether admission should require the
   `FINALPASS`-style keyword to agree with the float. The decal design
   kept the float as the evidence; nothing here changes that.
3. Normal envelope shape. If the normal output ever gains a consumer,
   a bound detail normal needs the same kind of exact envelope the
   alpha texture claims have. No such shape exists yet.
4. Two Pass family. The detail block is present there; any future
   admission declares the family.

## 10. Validation of this session

No production code changed and no repository test changed. The
evidence is the pinned source reads cited by line, one probe build of
the snapshot prefab, and two clone-route probes. All probes ran on
2026-09-22 in the Census Lab editor instance after its identity check.
The probe kept every triangle of the fixture renderer and moved none.
The clone probes unlocked copies only; the fixture stayed locked
before and after. The snapshot prefab and both clone assets were
deleted, and a sweep confirmed none of the three remained and no probe
build copy stayed in the scene.
