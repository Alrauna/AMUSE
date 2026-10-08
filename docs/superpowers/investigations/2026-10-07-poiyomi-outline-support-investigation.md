# Poiyomi outline support investigation

Date: 2026-10-07. Status: characterization complete. No production code changed.
Branch: `feat/census-refusal-coverage`, base commit `ca1684e`, clean tree.

Privacy note: this record is sanitized. It names no private avatar, renderer,
material, texture, scene, asset folder, lock, instance, port, or test-job
identifier. The avatar is "the avatar under test". Material and renderer
references are by role. All corpus observations are aggregate counts at
project level, never per-avatar or per-renderer rows. Public vendor shader
names, property names, versions, GUIDs of admitted public shader assets, and
report keys stay exact.

Labels: `[SOURCE]` marks a fact read at cited lines of this tree, or of the
public vendor repository at a pinned tag. `[MEASURED]` marks a read-only
observation in the Census Lab editor instance on 2026-10-07, identity-verified
before the read. `[INFERENCE]` is a bounded conclusion. `[UNPINNED]` marks a
vendor fact that could not be read verbatim at the pinned tag and must be
re-pinned before implementation. `[DECISION NEEDED]` marks a controller choice.

## 1. Question and scope

The NDMF console on the avatar under test now names its dominant conversion
blocker: `OpaqueConversionRefused` with the outlines detail. The question:
what must AMUSE add to convert proven-opaque triangles of current Poiyomi
materials that enable outlines? This note collects the refusal's origin, the
vendor outline semantics at the pinned version, the corpus shape, and the
support design space. It does not change production code and does not design
the full RED/GREEN task list.

Out of scope: the alpha classification itself (outline state never enters
triangle classification today), the `.poiyomi/Poiyomi Toon Outline Early`
companion shader (an unsupported name today, adjacent coverage item), and
every non-Poiyomi family.

## 2. Method and sources

- Vendor pin: tag `v9.3.64` of the public Poiyomi Toon Shader repository,
  commit `e125e1c33cbfb860f59330799dd4d10a1097242d`. This matches the package
  version `9.3.64` pinned in `PoiyomiMaterialSemantics.cs` (`:28-34`). One
  deep-code cite series comes from the `v9.2.79...v9.3.63` compare diff,
  carried into the tag because the `v9.3.64` commit changed only the version
  label line. Facts readable only on `master` (10.0.24) are marked
  `[10.x drift risk]`.
- The generated monolithic shader is about 2.6 megabytes with inlined HLSL.
  Verbatim reads of the property block, enums, and the `EarlyZ` pass head
  succeeded. The outline pass state block, the extrusion body, the
  distance-fade body, and the zero-width clip guard sit past the fetch
  truncation point and are `[UNPINNED]` at 9.3.64; the 10.x modular templates
  pin their shape.
- Repo state: branch and commit above. All production cites re-located by
  symbol.
- Lab observation, 2026-10-07: read-only reflection over the retained NDMF
  build report in the Census Lab editor instance, reduced in-editor to
  aggregate keys and token counts, so raw substitution text with private
  names never left the editor process. A separate read-only asset scan
  measured the Poiyomi material population. Instance identity was verified
  against the expected project assets path before every probe; two editor
  instances were reachable and every call pinned its target explicitly.

## 3. Console evidence `[MEASURED]`, 2026-10-07

The retained build reports 83 entries, 76 Information and 7 NonFatal. All
AMUSE entries are Information; the NonFatal entries are a third-party
optimizer plugin's warnings.

| Key | Entries |
|---|---|
| `amuse.slotSeparation.OpaqueConversionRefused` | 13 |
| `amuse.slotAnalysis.Refusal` | 28 |
| `amuse.renderer.AdmittedMaterialSemanticsUnknown` | 20 |
| `amuse.renderer.UnprovenMaterialSlotMapping` | 8 |
| `amuse.renderer.MaterialDependencyClosureFailed` | 4 |
| `amuse.summary.Title` | 1 |

Across the 13 conversion-refused entries, the `OutlinesEnabled` token appears
36 times in their substitution slots; 3 bare `OpaqueConversionRefused` token
strings appear elsewhere. Every conversion refusal on this avatar names the
outlines arm through the per-gate detail shipped on this branch. The
`UnprovenMaterialSlotMapping` key is the structural slot-count versus
submesh-count refusal (`UnityRendererAlphaAnalysis.cs:601-604`) and is
unrelated to outlines. The renderer-level closure count dropped from five in
the 2026-10-06 audit to four, consistent with the landed slot-scoping of the
three slot-local closure ways.

## 4. The refusal today `[SOURCE]`

- Gate: `PoiyomiOpaqueConversion.cs:318-323` refuses
  `PoiyomiOpaqueConversionRefusal.OutlinesEnabled` when `_EnableOutlines`
  is nonzero. The enum member sits under "Effective render-state eligibility"
  (`:44`).
- Rationale: `EnableOutlinesProperty`'s doc comment (`:211-219`): the vendor
  outline pass opens with `clip(_EnableOutlines - 0.01)`, then replaces or
  multiplies alpha from outline texture, outline color, and an optional
  distance fade - none of which AMUSE models - before Opaque mode forces that
  alpha to 1 ahead of the outline clip. Conversion therefore requires
  outlines disabled whenever it would mutate the material.
- The gate is the only transformation gate that can fail on an otherwise
  canonical material, because `_EnableOutlines` is the only conversion-read
  property the recipe does not write (`:300-303`).
- The 23-property canonical recipe already normalizes the outline blend state
  (`:173-198`): `_OutlineSrcBlend` 1, `_OutlineDstBlend` 0,
  `_OutlineSrcBlendAlpha` 1, `_OutlineDstBlendAlpha` 0, `_OutlineBlendOp` 0,
  `_OutlineBlendOpAlpha` 4. The recipe doc names `_OutlineDstBlendAlpha` as
  the field a copied-preset recipe would get wrong. `_OutlineZWrite`,
  `_OutlineZTest`, and `_OutlineCull` are deliberately excluded from both
  recipe and evidence request because conversion leaves them untouched, so
  they cancel.
- The clone keeps the source shader: `PrepareCanonicalOpaqueClone`
  (`:543-561`) is `new Material(source)` plus the recipe writes, queue, and
  `RenderType` tag. Unlike lilToon, whose conversion swaps shader identity
  and therefore needs a separate outline-capable target asset, the Poiyomi
  outline pass survives conversion structurally. The outline question is
  purely outline alpha semantics on the normalized state. `[INFERENCE]`
- Origin: the gate arrived with the Poiyomi opaque conversion itself
  (`89bf95e feat(poiyomi): add opaque material conversion`, designed in the
  2026-08 design and plan docs); the S6 slice `baac999` added the per-gate
  detail emission that now names it in the console.
- Report path: the slot entry template renders renderer, slot, refused
  material, and reason (`AmuseReportStrings.cs:428-436`).
- Tests pinning the refusal: `PoiyomiOpaqueConversionTests.cs:356-357`,
  `:492-493`, and `EnabledOutlines_RefuseEvenWhenBaseAlphaIsExactlyOne`
  (`:503-514`), whose doc comment names the hazard: Opaque mode forces alpha
  to 1 before the outline clip, "resurrecting outline fragments the author
  faded away" (`:496-501`). `AlreadyOpaque_EvenWithOutlinesEnabled`
  (`:956-960`) pins that a canonical material with outlines short-circuits at
  step 2 and refuses nothing.

## 5. Vendor outline semantics at 9.3.64 `[SOURCE]`

Property names (all from the pinned property block, `Poiyomi Toon.shader`
2118-2212; Two Pass identical at offset +26):

- The 8.x folklore names do not exist. `_OutlineMode`, `_OutlineWidth`,
  `_OutlineColor`, `_OutlineVectorTex`, and any delete-mesh property return
  no match in the 9.3.64 property block. The 9.x names are
  `_OutlineExpansionMode` (Basic 1, Rim Light 2, Directional 3, DropShadow 4;
  the enum starts at 1), `_LineWidth`, `_LineColor`, and
  `_OutlineSpace` (Local 0, World 1).
- Alpha-relevant properties: `_OutlineTexture` (default white),
  `_LineColor` (default alpha 1), `_OutlineOverrideAlpha` ("Override Base
  Alpha", default 0), and the `_OutlineAlphaDistanceFade` family with
  `_OutlineAlphaDistanceFadeType` and Min/Max alpha and Min/Max distance
  (2152-2158). There is no outline alpha-to-coverage property; coverage is
  the main-pass `_AlphaToCoverage` block only (165-170).
- Geometry-relevant: `_LineWidth`, `_OutlineMask` plus channel (per-vertex
  width mask), `_OutlineClipAtZeroWidth` ("Clip 0 Width", default 1),
  `_OutlineFixedSize`, `_OutlineFixWidth`, `_OutlinesMaxDistance`,
  `_OutlineZOffset*`, and the AudioLink outline size family. Smoothed outline
  normals come from baked vertex colors (`_OutlineUseVertexColorNormals` +
  the vendor Outline Vertex Color Baker), not a vector texture.
- Color-only: `_OutlineEmission`, `_OutlineTintMix`, UTS-style blend, hue,
  saturation, value, gamma, lighting (`_OutlineLit`,
  `_OutlineShadowStrength`), and the AudioLink color family.
- Render state: `_OutlineCull` (default 1, Front), `_OutlineZWrite`
  (default 1), `_OutlineZTest` (default 4, LEqual), the
  `_OutlineBlendOp*`/`_OutlineSrcBlend`/`_OutlineDstBlend` tuple, and the
  `_OutlineStencil*` family.

Pass structure: the outline is an inverted-hull second pass of the same
shader, not separate geometry. The pass template pins `Name "Outline"`,
ForwardBase light mode, `ZTest [_OutlineZTest]`, `ZWrite [_OutlineZWrite]`,
`Cull [_OutlineCull]`, the outline blend tuple, `AlphaToMask
[_AlphaToCoverage]`, and the outline stencil block - `[10.x drift risk]` for
the template source, `[UNPINNED]` verbatim at 9.3.64. The first pass is
`EarlyZ` and draws no color (`Poiyomi Toon.shader:4675-4701`). The "Two
Pass" admitted variant exposes the outline as a second Unity pass with the
same property block.

Outline fragment alpha, in template order `[10.x drift risk]`:

1. `clip(_EnableOutlines - 0.01)` - corroborated at 9.3.64 by this repo's
   attested-source characterization stand-ins
   (`PoiyomiSemanticTest.shader:179-181`).
2. Optional zero-width clip under `_OutlineClipAtZeroWidth`.
3. `col.a` from `_OutlineTexture`, then `col.a *= _LineColor.a`.
4. `_OutlineOverrideAlpha` selects replace versus multiply against the base
   fragment alpha. Default 0 multiplies, so the outline alpha rides the main
   alpha chain by default.
5. `_OutlineAlphaDistanceFade` multiplies a distance-driven lerp of Min/Max
   alpha.

Below-1 paths are exactly: outline texture alpha, `_LineColor.a`, the base
alpha chain (when not overridden), and distance fade. `_Mode == Opaque`
sets `_AlphaForceOpaque` 1, which forces the alpha to 1 ahead of the outline
clip, so a faded outline fragment resurrects as a solid fragment in Opaque
mode. The non-Opaque presets additionally set translucent outline blend
factors with `_OutlineDstBlendAlpha` 1 (`Poiyomi Toon.shader:46-54`); the
Opaque preset sets 1/0 with `_OutlineDstBlendAlpha` 0.

`_Mode` at 9.3.64 (`ThryWideEnum`, `Poiyomi Toon.shader:45`): 0 Opaque,
1 Cutout, 9 TransClipping (queue 2460), 2 Fade, 3 Transparent (queue 3000,
premultiply), 4-7 additive and multiplicative forms. The values observed in
the corpus (0, 3, 9) are Opaque, Transparent, and TransClipping.

Main-alpha coupling verdict: by default (`_OutlineOverrideAlpha` 0) the
outline alpha multiplies the base alpha chain, so a per-triangle base
opacity proof is a factor of the outline alpha. It is not sufficient: line
color alpha, outline texture alpha, and distance fade remain. `[SOURCE]`
for the factor chain, `[INFERENCE]` for the sufficiency wording.

Geometry: the shell is a per-vertex extrusion of the same draw's mesh
(offset = normal times width times enable times mask, plus distance and
AudioLink terms) `[10.x drift risk]`. Because the shell belongs to the
owning material's pass and the renderer's mesh, triangles that move to the
canonical clone carry their outline shell with them; the clone's own outline
pass draws it. `[INFERENCE]` from the single-mesh multi-pass structure.
Width, masks, offsets, and AudioLink terms are alpha-irrelevant and ride the
cloned properties identically on both sides.

Vendor tooling: no vendor material-conversion surface touches outline
properties at this tag; the only outline editor tool is the vertex-color
mesh baker, which rewrites meshes. `[SOURCE]` for the tool listing,
`[INFERENCE]` that no vendor precedent constrains the recipe.

A third shader asset, `.poiyomi/Poiyomi Toon Outline Early`, exists in the
vendor tree as a companion container. AMUSE's exact-name map does not admit
it, so its materials answer all-Unknown today. Adjacent coverage item, not
this slice.

## 6. The hazard, stated precisely

Conversion rewrites the material into the Opaque preset state. For a
triangle proven opaque on the base chain:

- Its base alpha is exactly 1 in the source and forced to 1 on the clone.
- Its outline fragment alpha in the source is base(1) times the outline
  inputs. If any outline input is below 1, the source outline fragment was
  faded or translucent; on the clone, Opaque forcing and the normalized
  blend tuple make it solid and opaque.
- Faded-to-invisible outline fragments would resurrect. That is the
  visibility form of the cardinal sin, and it is what
  `EnabledOutlines_RefuseEvenWhenBaseAlphaIsExactlyOne` pins.

The refusal is therefore fail-closed correct as written: the missing fact is
the outline alpha inputs, not the main alpha. `[SOURCE]` for the mechanism,
cited above; the necessity wording is `[INFERENCE]`.

The gate design that closes the gap is order-insensitive: if every outline
alpha input is proven to contribute a factor of exactly 1, the outline
fragment alpha equals the proven base alpha on both sides, regardless of the
order in which the vendor multiplies them. This mirrors the premultiply
admission, whose scoping argument already lives in the gate comments
(`PoiyomiOpaqueConversion.cs:325-343`).

## 7. Corpus fit of the alpha gates `[MEASURED]`, 2026-10-07

Project-wide scan of the Census Lab corpus, aggregate: 2337 materials, 28
current-Poiyomi assets, 9 with outlines enabled, 19 with outlines disabled.
Legacy 8.x outline property names are absent on all nine, matching section 5.

For the 9 outline-enabled materials:

| Fact | Value |
|---|---|
| `_Mode` | 0 x2, 3 x3, 9 x4 |
| `_LineColor.a` | exactly 1 on all 9 |
| `_OutlineAlphaDistanceFade` | 0 on all 9 |
| `_OutlineOverrideAlpha` | 0 on all 9 |
| `_OutlineTexture` | assigned on 1, unassigned on 8 |
| `_OutlineExpansionMode` / `_OutlineSpace` / `_OutlineLit` | Basic / Local / lit on all 9 |
| `_LineWidth` | 0.02 to 0.1 |
| `_OutlineMask` assigned | 3 of 9 |
| `_OutlineClipAtZeroWidth` | 0 on 6, 1 on 3 |
| `_OutlineUseVertexColorNormals` | 0 on all 9 |

Reading: under the gates `_LineColor.a` exactly 1, distance fade off, and
outline texture unassigned or alpha-proven, 8 of 9 observed materials pass
without any texture evidence read; the ninth needs exactly the texture alpha
route the capture machinery already provides. The 2 opaque-mode materials
never reach the gate (already-opaque short-circuit). The distance-fade
endpoints observed are the disabled-state defaults. `[MEASURED]` for all
counts; the pass/fail projection is `[INFERENCE]`.

## 8. Support design space

Option A - admit `_EnableOutlines` unconditionally. Unsound: resurrects
faded outline fragments and hides unmodeled texture and line-color alpha.
Rejected.

Option B - admit with outline-alpha gates. Extend the conversion evidence
request and eligibility with the outline alpha inputs: `_LineColor` alpha
exactly 1, `_OutlineAlphaDistanceFade` 0 (or both fade endpoint alphas
exactly 1, which makes the distance term a factor of 1 at every distance),
`_OutlineOverrideAlpha` admitted at either value once the texture and color
gates hold, and `_OutlineTexture` unassigned or alpha-proven through the
existing texture evidence route. Geometry state (`_LineWidth`, mask, offsets,
AudioLink size, vertex-color normals, `_OutlineClipAtZeroWidth`) needs no
gate: it is alpha-irrelevant and rides the clone identically. Render state
that conversion leaves untouched (`_OutlineZWrite`, `_OutlineZTest`,
`_OutlineCull`) cancels exactly as the recipe doc already argues.
`[INFERENCE]` on sufficiency, grounded in sections 5 and 6.

Option C - named sub-state refusals. Replace the single `OutlinesEnabled`
answer with closed-refusal members for the specific unsupported outline
states (for example an unproven outline texture alpha, distance fade on), so
an unsupported outline mode is a named refusal value. Composable with B and
consistent with the closed-refusal-enum policy. `[INFERENCE]`.

Option D - write `_EnableOutlines` to 0 on the clone. Deletes the author's
outline shells from moved triangles and violates preservation. Rejected;
matches the test doc's resurrection framing.

Scope notes for any admission design:

- Classification never changes. The outline enters only at conversion
  eligibility, exactly like `_ZTest` and blend gates today. `[INFERENCE]`
- Animation: `_EnableOutlines` toggling clips the whole outline pass and
  scales shell width to zero; the property is conversion-read today. Animated
  outline width or AudioLink terms are geometry-only and apply identically to
  source and clone because the clone shares the material state on the same
  renderer. The design must still verify the animation-closure treatment of
  `_EnableOutlines` and the AudioLink size properties before implementation.
  `[INFERENCE]`
- The Two Pass admitted variant shares the property block and needs the same
  gates; its schema already mirrors the plain schema.
- Version drift: the property names and preset actions are 9.3.64-exact; the
  fragment template order and pass state block are `[UNPINNED]` at 9.3.64 or
  `[10.x drift risk]`. The alpha gates are order-insensitive, which makes the
  unpinned ordering facts non-blocking for design, but the pass state block
  and distance-fade body should be re-pinned from the installed attested
  source before any pin-adjacent change. `[INFERENCE]`

Tests, RED-first shape: the three refusal pins at
`PoiyomiOpaqueConversionTests.cs:356`, `:492`, and `:503-514` flip from
refusal assertions to gated-admission assertions only for rows that satisfy
the gates; rows violating each new gate keep a refusal assertion with the new
named value. `AlreadyOpaque_EvenWithOutlinesEnabled` stays. Falsifier
candidates: an outline texture with a hole against a proven triangle (must
stay refused or classify through texture evidence), a line-color alpha of
0.5 (must refuse), distance fade on with endpoint alphas below 1 (must
refuse), and an override-alpha 1 material with proven texture alpha (must
admit). All stand-in-shader based, per the existing test pattern.

## 9. Decisions needed

1. `[DECISION NEEDED]` Slice scope: Option B alone, or B plus the Option C
   named-refusal split. B alone is the smallest complete solution; C is
   report-quality work that can ride the same slice or follow it.
2. `[DECISION NEEDED]` Whether the outline-texture arm uses the existing
   texture alpha evidence route in this slice or refuses with a named value
   and defers. One observed material exercises it. The conservative default
   is a named refusal now and texture evidence later.
3. `[DECISION NEEDED]` Re-pin depth: implement against the current
   order-insensitive gate design and re-pin the pass state block from the
   installed source as part of the slice, or re-pin first. The repo's
   attestation discipline favors re-pinning within the slice, before any
   attestation-adjacent constant changes.

## 10. Limits

All work this session was read-only. No Unity state was modified; no build
ran; no production code or test changed. The two vendor records this note
consolidates agree on every overlapping fact. `[UNPINNED]` vendor facts are
listed in section 5. The corpus projection in section 7 is inference from
measured aggregates, not a build observation; the next authorized Census Lab
build after an implementation would exercise it end to end.
