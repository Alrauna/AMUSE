# The poiyomi rim lighting slots versus the liltoon rim light - characterization

## Privacy note

This record opens with a privacy statement. It describes private Census
Lab fixtures by role only. It names no avatar, renderer, material,
texture, mesh, hierarchy, scene, or folder. It records no machine
paths, no host names, no ports, and no instance names or hashes. It
records no GUIDs and no generated shader hash fragments. The
designated locked denim garment fixture appears by role, and its rim
facts appear as property values. Exact triangle counts appear as
ranges. Every status claim carries its date. Property names, keyword
names, enum values, and vendor line facts are public facts of the
pinned shader sources.

## 1. Scope and method

Date: 2026-09-22. Branch: `docs/poiyomi-rim-lighting-characterization`,
cut from `main` at the merge of the detail characterization pull
request. This session changes no production code. It produces this
record only.

The question, from the session brief: where do `_EnableRimLighting`
and `_EnableRim2Lighting` refuse as of 2026-09-22, what do the vendor
rim blocks really write, what is the liltoon counterpart, what
consumes the outputs the rim gates block, and what do the rim features
do on the designated fixture.

Sources and probes, all read or run on 2026-09-22:

- The pinned Poiyomi Toon Shader 9.3.64 source, read from the
  installed copy in the Census Lab project, read only. The main shader
  file carries the rim blocks; the Two Pass source carries the same
  blocks.
- The pinned lilToon 2.3.4 source, read from the same installed
  location, read only.
- The committed frontend code of this repository.
- One Lab probe run of the designated locked denim garment fixture
  through the project's own soundness probe on current `main`, after
  the identity check of the Census Lab editor instance. The probe made
  a snapshot prefab of the scene carrier with the editor prefab
  utility, built it through the SDK preprocess chain, and destroyed
  the build copy.
- One sanctioned clone-route probe: copy the locked material, unlock
  the copy with the vendor method the production unlock window calls,
  run the production frontend entry, read the rim properties, and
  destroy the copy in the same handler. The fixture stayed locked and
  untouched throughout.

Method notes.

- The NDMF records of the probe build were read through reflection on
  the internal error report list. The accumulating background reader
  of the two prior sessions produced no records this time: its message
  serialization calls a localizer-backed method that fails off the
  main thread. The main-thread read after the build supplied the
  records instead. The report list persisted beyond the build scope on
  this editor instance, so the accumulated reports stayed readable:
  the list held the fresh build's report plus two older ones, and all
  three carry the same six distinct record types.
- The clone-route destroy left an empty asset file behind in the Lab
  scratch folder. The shell sweep removed that file and the snapshot
  prefab files, and an asset database refresh confirmed none of them
  remained and no probe build copy stayed in the scene.

## 2. The fixture on current main, observed 2026-09-22

The probe ran on `main` at the detail merge commit. Observed outcome:

- The probe accepted the avatar. One renderer before, one after.
- The renderer kept everything original. The moved-triangle
  composition was zero in all three classes, over a plane mesh of
  roughly half a million triangles. No violations.
- The alpha evidence field report: the main texture's captured alpha
  mip chain is mixed at every reported level. Across the levels, about
  70 to 86 percent of sampled texels are opaque, zero are translucent,
  and 14 to 30 percent are transparent. No level is fully opaque or
  fully non-opaque. The bound alpha mask reads about 54 percent
  opaque, 10 percent translucent, and 35 percent transparent texels.
- Six distinct NDMF record types appeared. Four came from upstream
  optimizer passes (an update notice, two unknown-component warnings,
  one metrics line). The two AMUSE records were
  `amuse.renderer.AdmittedMaterialSemanticsUnknown` from the semantic
  barrier and `amuse.summary.Title` with the counts analyzed zero,
  moved zero, kept one.

The exact alpha diagnostic, proven through the sanctioned clone route:
the production frontend entry answered supported material, all four
outputs incomplete, and exactly four diagnostics. Base color, emission,
and normal each refuse naming `_DetailEnabled`. The alpha output
refuses naming `_EnableRimLighting`. The clone carried both rim
enables at one, so both rim slots are on. The build path resolves alpha
through the same `AlphaFeatureGates` array at its only consult site
(section 3), so the build's renderer refusal corresponds to the same
alpha unknown.

So the renderer outcome on the rim session's base is the same as on
2026-09-21 and 2026-09-22: refused as semantics unknown, nothing
moved. The alpha refusal has advanced through `_AlphaPremultiply`
(admitted by the premultiply slice), the decal slots (admitted inert by
the decal slice), and `_DetailEnabled` (never an alpha gate), and now
sits at the rim lighting gate, with `_EnableRim2Lighting` waiting
behind it in the same array.

## 3. Gate map, current `main`, 2026-09-22

All line numbers cite
`Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`.

- `AlphaFeatureGates`, declared at lines 244 to 263, carries
  `_EnableRimLighting` at line 257 and `_EnableRim2Lighting` at line
  258, immediately after `_EnableFlipbook` (line 256) and immediately
  before `_EnableDepthRimLighting` (line 259) and
  `_EnableEnvironmentalRim` (line 260). The alpha output consults the
  list at line 1149, after the forced-opaque short-circuit, on every
  non-forced alpha path of both families. The helper
  `FirstFailedZeroGate` (lines 2633 to 2675) walks the array in order
  and names the first property that is missing, non-finite, or
  nonzero, so a material with both rim floats on refuses naming
  `_EnableRimLighting`. That matches the observed diagnostic. The
  alpha evidence request unions the array at line 2500.
- `BaseColorFeatureGates`, declared at lines 152 to 201, carries the
  same four rim names at lines 175 to 178. The base color output
  consults that list at line 550. The emission output consults it at
  line 1908, before its own slot-0 modifier list.
- `NormalFeatureGates`, declared at lines 355 to 364, carries no rim
  name. The normal output consults it at line 2087.
- A search of the whole file finds the four rim enable names only at
  lines 175 to 178 and 257 to 260. The production frontend reads no
  other rim property: no style float, no apply-to-alpha scalar, no rim
  texture is read anywhere in production as of 2026-09-22.

## 4. The vendor mechanism, pinned 9.3.64 source

Line numbers cite the main shader file of the pinned package
(`_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader`), read from
the installed Census Lab copy.

### 4.1 The two slots

Slot 1: lock-strip directive at line 1565, block header at 1566,
`_EnableRimLighting` at 1567 under `[ThryToggle(_GLOSSYREFLECTIONS_OFF)]`
with declared default zero, and `_RimStyle` at 1569 as
`[KeywordEnum(Poiyomi, UTS2, LilToon)]` with declared default zero.

Slot 2: lock-strip directive at 1685, block header at 1686,
`_EnableRim2Lighting` at 1687 under `[ThryToggle(POI_RIM2)]`, and
`_Rim2Style` at 1689 with the same keyword enum.

The liltoon-style parameters per slot: `_RimColor` at 1628 (`_Rim2Color`
at 1749), `_RimColorTex` "Color / Mask" at 1629 (1750), its UV selector
at 1631 (1752), `_RimMainStrength` at 1633 (1754), `_RimBorder` at 1635
(1756), `_RimBlur` at 1636 (1757), `_RimFresnelPower` at 1637 (1758),
`_RimEnableLighting` at 1638 (1759), and `_RimBlendMode` at 1643 (1764)
with the value set Replace 0, Add 1, Screen 2, Multiply 3 and declared
default Add. The mask-only toggle and the direction controls
(`_RimDirStrength`, `_RimDirRange`, `_RimIndirRange`,
`_RimIndirBorder`, `_RimIndirBlur`) complete the style. The poiyomi
style adds its own mask group (`_RimMask` with pan, UV, channel,
invert, and bias intensity, around lines 1572 to 1579), rim texture,
color, brightness, blend-strength, and shape scalars. The UTS2 style
adds its own mask and power scalars.

Two properties deserve their own lines: `_RimApplyAlpha` ("Apply to
Alpha", Off 0, Add 1, Multiply 2, default 0) at line 1663 with
`_RimApplyAlphaBlend` (default one) at 1664, and `_Rim2ApplyAlpha`
("Intensity to Alpha", same value set, default 0) at line 1783 with
`_Rim2ApplyAlphaBlend` at 1784. Their inspector show conditions read
`_Rim2Style==0` in both slots, a vendor copy-paste defect that affects
the UI only.

### 4.2 Where rim runs in the base pass fragment

The fragment orders the chain this way:

1. Chain alpha starts at line 29780 as main texture alpha times color
   alpha (the Two Pass second-base arm sits at 29785).
2. The alpha mask block composes at 29864 to 29876.
3. The alpha options block runs at 29888.
4. The four decal slots run at 29976.
5. `applyMatcap` runs at 29988 (section 4.6).
6. The rim poi-style and UTS2-style arms run at 30020 to 30068 for
   slot 1 (keyword guards at 30021 and 30022, calls at 30057 and
   30065) and at 30070 to 30130 for slot 2.
7. The vendor premultiply runs at 30216 to 30218
   (`poiFragData.baseColor *= saturate(poiFragData.alpha)` inside
   `if (_AlphaPremultiply)`), and the base color becomes the final
   color at 30219.
8. The backlight runs at 30318 to 30321, then the rim liltoon-style
   arms at 30324 to 30350 (slot 1 keyword guard 30326, texture sample
   30328, call 30332; slot 2 keyword guard 30338, texture sample
   30340, call 30344), and the light add joins the final color at
   30348.
9. Force opaque overwrites alpha around 30366 to 30369, coverage and
   dithering follow, the clip runs at 30396, and cutout mode binarizes
   alpha at 30399 to 30402.

Every rim arm therefore runs before the premultiply, the coverage
decisions, and the clip.

### 4.3 The three style functions

The rim code compiles when either enable keyword is set (25758 to
25760); the two slots share one function per style.

`ApplyPoiyomiRimLighting`, 25761 to 25864, second copy at 42961 onward.
It computes the rim weight from the view-normal dot product, applies
the mask and global-mask blend, the shadow shaping, and the audio link
width adds, then writes three things: the base color through five
blend modes, the emission, and the chain alpha:

- `if (RimApplyAlpha == 1)` at 25837: `poiFragData.alpha +=
  lerp(0, saturate(rim), RimApplyAlphaBlend)` at 25838, then
  saturate at 25839.
- `if (RimApplyAlpha == 2)` at 25842: `poiFragData.alpha *=
  lerp(1, saturate(rim), RimApplyAlphaBlend)` at 25843.

The second copy repeats the writes at 43035 to 43043. `RimApplyAlpha`
is the cbuffer global `_RimApplyAlpha`, which is slot 1's property:
both slots' calls share the body, so slot 2's alpha behavior reads
slot 1's scalar. `_Rim2ApplyAlpha` and `_Rim2ApplyAlphaBlend` are
consumed nowhere in the file. They are dead properties in this pinned
version.

`ApplyUTS2RimLighting`, 25866 to 25891, second copy at 43066 onward.
It composes the UTS rim term and writes one line:
`poiFragData.baseColor += _RimLight_var` at 25888 (copy at 43088). No
alpha write.

`ApplyLiltoonRimLighting`, 25892 to 25963, second copy at 43092
onward. It computes the fresnel factor, shapes it by border and blur
through `poiEdgeLinear`, applies the optional direction and indirect
arms, and writes only two lines: `poiFragData.finalColor =
lilBlendColor(...)` at 25959 and 25960 (copies at 43159 and 43160).
It writes no `poiFragData.alpha` and no base color.

`ApplyDepthRimLighting`, 25986 to 26054 under the
`_POI_DEPTH_RIMLIGHT` keyword at 25967, writes the light add at 26048,
the emission at 26049, and the base color at 26050 to 26052. No alpha
write. The environmental rim module was not characterized this
session; it stays gated.

### 4.4 The alpha fact with line numbers

- The liltoon-style and UTS2-style arms never write the chain alpha.
  Their entire code writes the final color (25959 and 25960) and the
  base color (25888) respectively.
- The poi-style arm writes the chain alpha at 25838 to 25839 and 25843
  (add-pass copy 43038 to 43043), only when the global `_RimApplyAlpha`
  is one or two, weighted by `_RimApplyAlphaBlend`. The written value
  carries the rim weight, which is masked, shadow-shaped, smoothed,
  and saturate-bounded inside the body.
- A rim alpha write lands between the decal slots (29976) and the
  premultiply (30216), before force opaque and clip.
- The production forced-opaque short-circuit stays sound against rim:
  the vendor force-opaque write at 30366 to 30369 happens after every
  rim write, so the forced claim of constant one survives.

### 4.5 The keyword duality

The style dispatch is compile-time only: `#if defined(_RIMSTYLE_POIYOMI)`,
`_RIMSTYLE_UTS2`, and `_RIMSTYLE_LILTOON` (and the `_RIM2STYLE_*`
twins) select the arm. The floats `_RimStyle` and `_Rim2Style` never
appear in the fragment code. The enable toggles are float-plus-borrowed-
keyword pairs (1567 and 1687), like the decal and detail toggles.

The fixture carries the keywords `_RIMSTYLE_LILTOON`,
`_RIM2STYLE_LILTOON`, `_GLOSSYREFLECTIONS_OFF`, and `POI_RIM2`, and
its floats read `_RimStyle` two, `_Rim2Style` two, and both enables
one: float and keyword agree on this fixture.

For the decal and detail work the inert proof was style-independent,
so the duality stayed an open question. For rim it becomes
load-bearing. An admission that trusts the float `_RimStyle` while the
compiled arm is keyword-selected assumes float-keyword agreement. A
keyword that disagrees with its float would run a different alpha
behavior than the float claims. This needs an explicit design
decision before any production work.

### 4.6 Related finding, recorded not expanded: the matcap alpha writers

Reading the rim call sites surfaced an ungated alpha-writer family.
`applyMatcap` (defined at 24172, second copy at 42084) is called at
29988, between the decal slots and the rim arms. Each of its four
slots writes the chain alpha:

- `poiFragData.alpha *= lerp(1, matcapN.a, matcapNMask *
  _MatcapNAlphaOverride)` at 24272, 24379, 24493, and 24607. Identity
  at override zero, but the override is an authoring value, not a
  constant.
- `if (_MatcapNApplyToAlphaEnabled)` at 24274, 24387, 24501, and
  24615: the matcap grayscale adds to alpha through the lerp at about
  24285 and saturates, or multiplies alpha through the lerp at 24291,
  24404, 24518, and the slot-4 block after 24615.

No matcap property appears in `AlphaFeatureGates`, in the coverage or
texture-backed gate lists, or in the alpha evidence request. A
material with an enabled matcap and a nonzero apply-to-alpha enable or
alpha override would pass the current alpha gates and could complete
with a wrong claim, because the writer scales alpha by a
view-dependent sample. The fixture is not exposed: all four of its
matcap alpha enables and overrides read zero (section 7). This needs
its own owner decision and RED and GREEN obligations. It is recorded
here so it is not lost.

### 4.7 The Two Pass family

The Two Pass source carries the same mechanism: the poi-style alpha
writes at 25933 to 25940, the liltoon-style function at 25990 with
calls at 30451 and 30463, and the same dead `_Rim2ApplyAlpha` pair
(1809 to 1810). The family shares the mechanism, so any future
admission declares the family it covers.

## 5. The liltoon counterpart, pinned 2.3.4 source

lilToon's rim light lives in `lilGetRim` in the shared fragment include
(`lil_common_frag.hlsl` 1641 to 1735), compiled under the
`LIL_FEATURE_RIMLIGHT` keyword at 1642 and gated at runtime by the
float `_UseRim` at 1645.

One to one:

- The poiyomi liltoon-style function is a close port of the
  direction-equipped variant. The color pairs (`_RimColor`,
  `_RimIndirColor`, 1649 to 1650), the color/mask texture at uvMain
  (1651 to 1655), `_RimMainStrength` (1656), `_RimVRParallaxStrength`
  (1659), `_RimNormalStrength` (1664), `_RimDirRange` and
  `_RimIndirRange` (1670 to 1671), `_RimFresnelPower` (1672),
  `_RimBackfaceMask` (1673), `_RimBorder` and `_RimBlur` (1677 to
  1678), `_RimShadowMask` (1680 to 1681), `_RimEnableLighting` (1692
  to 1694), and `_RimBlendMode` (1696 to 1697) carry the same names,
  the same formulas, and the same blend value set (replace, add,
  screen, multiply).
- The poiyomi slots duplicate this port: slot 1 and slot 2 each run
  their own copy of the same math. lilToon has one rim light.

Not one to one:

- Compile gating. lilToon compiles under `LIL_FEATURE_RIMLIGHT` and
  checks the float `_UseRim` at runtime. Poiyomi selects the arm by
  style keyword only, with no runtime float check on the enable inside
  the arm.
- `_RimApplyTransparency`. lilToon multiplies the rim weight by the
  chain alpha `fd.col.a` when the toggle is set, only in transparent
  render modes (1683 to 1689 for the direction variant, 1720 to 1722
  for the simple variant). That is a read of alpha, never a write.
  Poiyomi's counterpart property is commented out in both slots, so
  the port omits the read.
- Poiyomi-only additions: the mask-only mode, hue shift, the global
  mask read, the apply-to-global-mask write, the audio link width,
  emission, and brightness adds, and two extra styles: the poi style
  with the alpha writers of section 4.3, and the UTS2 style.
- lilToon's separate rim-shade feature (`lilGetRimShade`, 1198 to
  1226, runtime float `_UseRimShade` at 1202) multiplies the color by
  a darker rim term. Poiyomi has no direct counterpart; its poi-style
  replace and multiply blends cover part of that space.
- Alpha on either side: neither lilToon variant writes `fd.col.a`.
  The only alpha interactions in the whole family are lilToon's
  transparency read and the poi-style writes. The production gate
  names the family's two enables, so the refusal as of 2026-09-22
  also covers the two alpha-inert styles.

## 6. Consumption, current code

The build consumes exactly one semantic output. Renderer admission and
runtime-state resolution read alpha-only semantics through
`UnityMaterialSemantics.AnalyzeAlphaMaterial` at
`Analysis/AdmittedMaterialStates.cs` line 221 and
`Host/UnityRendererAlphaAnalysis.cs` line 575; the capture path uses
the transferred mirror at `Build/AmusePlatformFinishPlugin.cs` line
666. The full material analysis has one production caller,
`UnityMaterialSemantics.AnalyzeBaseMaterial` at
`Semantics/UnityMaterialSemantics.cs` line 121, which serves the
research census family check; the test assemblies are the other
consumers. No build path consumes base color, emission, or normal.

Consequence: admitting an alpha-inert rim configuration changes the
renderer decision only through the alpha output. On this fixture the
base color, emission, and normal refusals at `_DetailEnabled` would
remain, but they do not block renderer admission, because admission
consumes the alpha output only.

## 7. Fixture facts, sanctioned clone route, 2026-09-22

The unlocked clone of the designated locked denim garment fixture
carried these rim facts:

- Both slots enabled: `_EnableRimLighting` one, `_EnableRim2Lighting`
  one. Both styles liltoon: `_RimStyle` two, `_Rim2Style` two. Both
  apply-to-alpha scalars zero: `_RimApplyAlpha` zero,
  `_Rim2ApplyAlpha` zero (the second is dead regardless, section
  4.3). Both blend weights one.
- Slot 1, liltoon arm: color (0.75, 0.75, 0.75, about 0.5); one bound
  color/mask texture with identity scale-offset and zero pan;
  mask-only mode one; main strength zero; normal strength 0.125;
  border 0.5; blur 0.25; fresnel power one; enable lighting zero;
  shadow mask zero; backface mask one; blend mode three (multiply);
  direction strength zero; indirect width zero; hue shift off; global
  mask off; apply-to-global-mask off; theme index zero.
- Slot 2, liltoon arm: color (0.95, 0.95, 0.95, one); the same texture
  bound again; mask-only zero; main strength 0.9; normal strength
  0.125; border 0.5; blur 0.25; fresnel power 2.5; enable lighting
  one; shadow mask 0.75; backface mask one; blend mode one (add);
  direction strength zero; hue shift off; global mask off.
- The poiyomi-style masks are unbound in both slots, and the
  poiyomi-style shape scalars sit at their declared defaults (width
  0.8, sharpness 0.25, power one, invert zero, light-direction mask
  off).
- Matcap alpha facts: matcap slots one and two enabled for visuals,
  slots three and four off; all four apply-to-alpha enables zero; all
  four alpha overrides zero.
- Keywords: `_RIMSTYLE_LILTOON`, `_RIM2STYLE_LILTOON`,
  `_GLOSSYREFLECTIONS_OFF`, `POI_RIM2`, `FINALPASS`, `POI_MATCAP0`,
  `COLOR_GRADING_HDR_3D`, and the rest of the build's keyword set.
  Floats and keywords agree on this fixture.
- The production frontend answer on the clone: supported material,
  four incomplete outputs, four diagnostics (section 2).

So on this fixture the rim features are visually live: both liltoon
arms compose color, slot 2 with lighting and a shadow-mask factor.
They are alpha-inert by construction: liltoon style on both slots, and
no poi-style alpha scalar anywhere reads nonzero.

## 8. Routing

- Defect: none for the rim gates. As of 2026-09-22 the refusal of
  `_EnableRimLighting` and `_EnableRim2Lighting` on the alpha output
  over-claims nothing. The poi-style arm can write the chain alpha
  (25838 to 25843), so a blind admission would be unsound, and the
  current gates refuse instead.
- Coverage gap, recorded not expanded: the refusal covers two
  alpha-inert styles together with one alpha-writing style. An
  admission slice therefore does not have the premultiply slice's
  shape. It needs the style dimension:
  1. Read the style float per slot. Liltoon (two) or UTS2 (one): the
     arm is alpha-inert by the pinned proof.
  2. Poiyomi (zero): require `_RimApplyAlpha` proven exactly zero. By
     the shared-body fact that single scalar covers both slots.
  3. A missing or non-finite style answer refuses naming the style
     property.
  4. The keyword question of section 4.5 needs a design decision
     before production work: the compiled arm follows the keyword,
     the evidence is the float.
- Honest expectation: the fixture gains nothing immediately from a
  sound rim admission. The alpha output's next blocker is the bound
  replace mask (the 2026-09-21 record's second gap; the frontend
  refuses any bound mask naming `_AlphaMask`), and the garment's main
  texture is mixed at every captured mip level (section 2), so the
  classifier would still prove nothing for this renderer. Judge the
  slice against the census corpus, where liltoon-style and UTS2-style
  rims without masks can complete, not against this one fixture.
- Related findings, recorded not expanded: the matcap alpha writers
  (section 4.6) need their own owner decision; the dead
  `_Rim2ApplyAlpha` pair and the shared-body behavior are vendor facts
  that any poi-style admission must encode.
- Nothing to do, decided for this session: no production change. The
  record is the deliverable. A later decision owns landing, as the
  detail record did.

## 9. Open questions

1. Keyword agreement for the style dispatch. Should an admission read
   the material's keyword set, or document the duality risk the decal
   design deferred?
2. The UTS2 style is alpha-inert by the same proof shape, but its
   base-color write would keep the color outputs refused. Whether any
   corpus material uses it decides whether the admission covers it at
   all.
3. The matcap alpha writers: gate scope (the eight scalars inside
   `AlphaFeatureGates`) versus an inert-check helper mirroring the
   decal slot helper.
4. Environmental rim: not characterized this session; it stays gated.
5. Two Pass family coverage: the mechanism is shared; the admission
   declares the family.

## 10. Validation of this session

No production code changed and no repository test changed. The
evidence is the pinned source reads cited by line, one probe build of
the snapshot prefab, one accumulating-reader attempt (failed; the
method note in section 1 records why), one main-thread record read,
and one clone-route probe. All Unity operations ran on 2026-09-22 in
the Census Lab editor instance after its identity check;
`Application.dataPath` matched the Census Lab project exactly before
every operation, and the intended instance was pinned while two
instances were reachable. The probe kept every triangle of the fixture
renderer and moved none. The clone probe unlocked a copy only; the
fixture stayed locked before and after. The snapshot prefab files and
the clone asset were deleted, the refresh confirmed none of them
remained, and no probe build copy stayed in the scene.
