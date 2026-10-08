# Poiyomi outline alpha conversion: design

Date: 2026-10-07. Revised twice on 2026-10-07 after controller review: the
outline texture moved from a whole-texture proof to the two-stage proof, and
the outline alpha handling then moved entirely into the alpha semantics, so
the conversion layer carries no outline machinery at all (section 5).
Branch plan: a child of `feat/census-refusal-coverage`, or `main` once that
branch merges. All mutations stay inside the NDMF build copy. Source assets
are never mutated.

Privacy note: this spec names no private avatar, renderer, material, scene,
or machine identity. Poiyomi shader and property names are public vendor
identifiers. The avatar in prior records is "the avatar under test".

Labels: `[SOURCE]` is a fact read at cited lines of this tree, at the public
vendor repository tag `v9.3.64` (commit `e125e1c33cbfb860f59330799dd4d10a1097242d`),
or in the installed attested vendor source of the Census Lab project, whose
normalized bytes are the pin target of `CanonicalNormalizedSourceHash`.
Installed-source cites name the file `Poiyomi Toon.shader (installed 9.3.64)`
with its own line numbers. `[MEASURED]` marks a 2026-10-07 read-only Census
Lab observation, aggregate level. `[INFERENCE]` is a bounded conclusion.
`[UNPINNED]` marks a vendor fact not verbatim-readable at the pinned version.

## 1. Summary and contract

Today AMUSE refuses to convert any Poiyomi material whose outlines are
enabled. The controller contract:

> A triangle moves to the canonical opaque material only when both its base
> surface and its outline shell are proven opaque. If either the base
> polygon or the outline polygon is transparent or unproven, the triangle
> keeps its original material.

The outline shell's alpha is a visual fact about the material, so its proof
belongs in the alpha semantics, beside the mask and the main texture. When
the semantics prove the outline shell per triangle, the existing classified
set is already the intersection the contract asks for, and the conversion
layer needs no outline knowledge at all.

## 2. Ground truth `[SOURCE]`

- Gate: `PoiyomiOpaqueConversion.cs:318-323` refuses
  `PoiyomiOpaqueConversionRefusal.OutlinesEnabled` whenever `_EnableOutlines`
  is nonzero. `EnableOutlinesProperty`'s doc (`:211-219`) records the
  hazard: outline alpha inputs are unmodeled, and Opaque mode forces alpha
  to 1 ahead of the outline clip, so faded outline fragments would
  resurrect.
- The 23-property canonical recipe normalizes the outline blend state,
  including `_OutlineDstBlendAlpha` 0 (`:173-198`).
- The clone keeps the source shader: `new Material(source)` plus the recipe
  writes (`:543-561`). The outline pass survives conversion structurally.
- The refusal detail flows from `eligibility.Refusal.ToString()` at
  `AlphaSeparationPreparation.cs:915-919`.
- The blanket refusal is the dominant live blocker: 13 conversion-refused
  slots on the avatar under test, all naming the outlines arm
  `[MEASURED]`, per `docs/superpowers/investigations/2026-10-07-poiyomi-
  outline-support-investigation.md` section 3.
- The boundary rule this design relies on cuts the other way now: the
  conversion layer keeps a separate vocabulary from analysis so conversion
  state never refuses analysis that does not depend on it
  (`PoiyomiOpaqueConversion.cs:30-38`). Outline alpha is analysis state the
  conversion previously duplicated; this design removes the duplication.

## 3. The pinned outline chain `[SOURCE]`

All cites below are `Poiyomi Toon.shader (installed 9.3.64)` unless marked
otherwise. The installed bytes are the attestation pin target, so these
lines are characterization of the pinned source, not of a remote tag.

- Enable clip: `clip(_EnableOutlines - 0.01)` is the first statement of the
  outline color function (`:54390`), before every outline alpha input.
- Zero-width clip under `_OutlineClipAtZeroWidth` (`:54391-54411`).
- Texture sample (`:54413`):
  `POI2D_SAMPLER_PAN(_OutlineTexture, _MainTex, poiUV(poiMesh.uv[_OutlineTextureUV], _OutlineTexture_ST), _OutlineTexturePan)`,
  guarded by `PROP_OUTLINETEXTURE`; a locked material with the feature
  stripped takes the `#else` white constant (`:54415`), which matches the
  unassigned semantics.
- Affine: `poiUV(uv, st) = uv * st.xy + st.zw` (`:6544-6547`);
  `_OutlineTexture_ST` is a declared `float4` (`:49139`).
- Pan: `POI_PAN_UV(uv, pan) = uv + _Time.x * pan` (`:4890`); pan is a
  `float2` (`:49140`, property `:2133`), applied additively after the ST
  affine. The pan is time-driven, so any nonzero pan sweeps the sampled
  domain across the whole image over time; a static per-triangle patch
  proof is only sound at exactly zero pan.
- UV channel: `_OutlineTextureUV` is
  `ThryWideEnum(UV0,0, UV1,1, UV2,2, UV3,3, Panosphere,4, World Pos,5,
  Local Pos,8, Polar UV,6, Distorted UV,7, Matcap,9)`, default 0
  (`:2134`). Values 0 to 3 are mesh UV channels; 4, 5, 6, 7, 8, and 9 are
  derived coordinate spaces a plain mesh-UV affine cannot model.
- Sampler: the sample rides the `_MainTex` sampler states (`:54413`,
  `POI2D_SAMPLER_PAN` at `:4891`). The repo already pins this as
  Poiyomi-specific knowledge: "the sampler always comes from `_MainTex`"
  (`PoiyomiMaterialSemantics.cs:2650-2667`,
  `TryGetMainTextureSampling`).
- Alpha composition, in order: `col.a` from the outline texture, then
  `col.a *= outlineColor.a` where `outlineColor` is `_LineColor` — except
  that `_OutlineALColorEnabled` with audio link available replaces the
  color, alpha included, with a smoothed lerp toward
  `_AudioLinkOutlineColor` (`:54416-54422`) — then the override replace or
  multiply against the base alpha (`:54430-54437`), then the distance fade
  (`:54439-54444`). `_Mode == Opaque` forces alpha to 1 ahead of the
  outline clip, which is the resurrection hazard the semantics prevent.

The property block is identical in the tag's blob (`Poiyomi Toon.shader:
2131-2135` at `v9.3.64`), so the installed-source and tag reads agree. A
parallel public-repo corroboration pass re-read the chain from the tag's
property block, the `v9.2.79...v9.3.63` compare diff, and the tag's macro
include; it found no drift against the installed source on any chain fact.
The 10.x modular templates agree in shape and add a `Screen Space` UV value
at 10 that does not exist at 9.3.64, which the unsupported-form gate
refuses with the other derived modes.

## 4. The factor-1 theorem, as composed semantics `[INFERENCE]`

The outline fragment alpha composes the base alpha with four inputs: the
outline texture alpha, the line color alpha, an optional replace arm, and
an optional distance fade, with the AudioLink color able to replace the
line color at runtime. The alpha semantics compose the same factors, in the
same order, as conditional terms on the existing chain:

- Outlines disabled: no outline terms at all. The semantics are byte-for-
  byte today's for every outline-disabled material.
- Outlines enabled: the chain multiplies the outline factor through the
  exact product machinery (`ScalarProductFold.Fold`). The controller
  contract requires that both the base surface and the outline shell are
  proven opaque before a triangle can move to the canonical opaque
  material. In Poiyomi, `_OutlineOverrideAlpha` alters alpha only in the
  outline pass. It does not alter alpha in the base surface pass.
  Replacing the base chain would discard base opacity and resurrect
  transparent base surfaces. The conjunction of base opacity and outline
  opacity is the product `base_alpha * outline_factor`. That product equals
  one if and only if both factors equal one. `_OutlineOverrideAlpha` is
  captured and verified finite, but it never replaces the base chain.
- Line color alpha exactly 1 and fade off and AudioLink off leave the
  texture factor as the only unknown. A whole-texture-proven texture is a
  proven constant one (the fast path). Otherwise the term is a plain
  texture sample (`ScalarSemanticValue.Texture` over a `TextureSample` with
  `TextureChannel.Alpha`), which the existing classifier resolves per
  triangle over the snapshot's UV sets. (`ScalarSemanticValue.MappedTexture`
  applies only to affine color maps and is not used here).
- Any unprovable factor - a line color below one, a fade enabled, the
  AudioLink switch enabled, a derived UV mode, a nonzero pan, a missing
  alpha capture - records the mask-style named diagnostic (`RecordUnknown`
  with `UnsupportedFeature` and the offending property,
  `PoiyomiMaterialSemantics.cs:1246-1252`), and the triangles stay
  unproven.

Because the composed semantics mirror the fragment order, a triangle is
classified proven exactly when its base surface and its outline shell are
both proven opaque, which is the controller contract. The resurrection
hazard cannot occur: an unproven outline factor keeps the triangle off the
moved set, so conversion never forces a faded fragment back to solid.

Preservation: `new Material(source)` shares every property, so outline
width, masks, offsets, color, and AudioLink terms are identical on both
sides; vertex-extrapolated shells stay continuous across the appended
submesh boundary by construction.

## 5. Design

### 5.1 Evidence re-homing

- The alpha evidence request (`CreateAlphaEvidenceRequest`,
  `PoiyomiMaterialSemantics.cs:2499-2570`) gains: the scalars
  `_EnableOutlines`, `_OutlineOverrideAlpha`, `_OutlineAlphaDistanceFade`,
  `_OutlineALColorEnabled`, `_OutlineTextureUV`; the vector
  `_OutlineTexturePan` (the pan properties
  already ride `vectorProperties` as `MainTexPanProperty` and
  `_OutlineTexture` with `TextureEvidenceKinds.ScaleOffset |
  TextureEvidenceKinds.SourceIdentity |
  TextureEvidenceKinds.SampledAlphaIsOne |
  TextureEvidenceKinds.AlphaChannel`, mirroring the mask entry plus the
  alpha-channel field the patch proof reads. Sampling is not asked of the
  outline texture: it rides the main sampler, whose state the main-texture
  entry already carries.
- The conversion evidence request shrinks: `ConversionSchema` drops
  `_EnableOutlines` and returns to the 23 recipe properties;
  `ConversionRequiredSchemaProperties` returns those 23;
  `GatherConversionSourceEvidence` passes them; the request carries no
  color or texture entries again. Every outline fact now has exactly one
  owner: the alpha semantics.

### 5.2 The outline terms in the alpha semantics

In the alpha output construction (`PoiyomiMaterialSemantics.cs`), the
analyzer first evaluates the non-forced base alpha (`maskReplacement` if
present, or `baseChain` folded with `maskMultiplier`). For non-forced
materials (`_AlphaForceOpaque == 0`), outline evaluation runs across all
paths before the analyzer returns. If `_EnableOutlines` is missing or
non-finite, it fails closed with a diagnostic on `_EnableOutlines`. If
`_EnableOutlines <= 0`, the vendor outline pass is clipped and disabled, so
no outline terms are composed. When `_EnableOutlines > 0`, the semantics
compose the outline factor into the base alpha:

| Order | Fact | Unprovable form |
|---|---|---|
| 1 | `_EnableOutlines` captured, finite, greater than zero | disabled (<= 0): no terms |
| 2 | `_OutlineOverrideAlpha` captured, finite | diagnostic `UnsupportedFeature` on `_OutlineOverrideAlpha` |
| 3 | `_LineColor` captured, finite, `.a == 1` | diagnostic `UnsupportedFeature` on `_LineColor` |
| 4 | `_OutlineAlphaDistanceFade == 0` | diagnostic on `_OutlineAlphaDistanceFade` |
| 5 | `_OutlineALColorEnabled == 0` | diagnostic on `_OutlineALColorEnabled` |
| 6 | `_OutlineTexture` unassigned, or stage-1 proven, or chain admitted with captured alpha channel | diagnostic on the offending property |

The outline factor always multiplies into the base alpha through
`ScalarProductFold.Fold`. The whole-texture fast path is
`SampledAlphaIsProvenOne` (constant one). The patch proof creates a
`ScalarSemanticValue.Texture` sample over a `TextureSample` with source
identity, mapping, and the main sampler (`mainTexture.Texture.Sampling`).
The supported mapping requires exact integer channel 0 to 3, zero pan on
both axes, finite scale and offset, and a valid alpha channel on the
texture assignment.

To support extra UV channels correctly, `AlphaSemanticsResolver` must
select the extra UV channel before it evaluates `AffineUvTransform`. This
ensures that scale and offset apply to the selected channel coordinates, and
that the bounding envelope is computed from the selected channel.
Feature-label map entries for the new properties keep the diagnostic text
readable, the same way the 2026-10-07 slice labeled the mask properties.

### 5.3 The conversion cutover

The conversion layer loses all outline machinery, cleanly:

- `PoiyomiOpaqueConversionRefusal.OutlinesEnabled` is deleted, with no
  replacement member: an unprovable outline is an analysis diagnostic, in
  the analysis vocabulary, per the mask precedent.
- The step-4 outline gate, the `EnableOutlinesProperty` constant, and the
  `_EnableOutlines` schema entry are deleted. `ConversionSchema` returns to
  the 23 recipe properties, and the presence-superset machinery is deleted
  rather than kept dormant.
- `AlreadyOpaque` still short-circuits before any outline question, so a
  canonical material with outlines never changes behavior.
- Report text needs no table change: the analysis diagnostics flow through
  the existing per-slot refusal and unknown paths, and the conversion
  refusal family simply stops naming outlines.

### 5.4 Compatibility

- Locked materials: capture runs through the transient-unlock clone, whose
  restored values carry the alpha request schema. A locked material with
  the outline texture feature stripped takes the vendor's own white
  constant (`:54415`), so its outline texture behaves as unassigned.
  Outline-disabled locked materials take no outline terms.
- Two Pass: the shared alpha request and both stand-in fixtures carry the
  same additions; no family-specific work.
- Animation: the outline properties join the alpha request, so their
  animated bindings resolve through the existing alpha-relevance
  machinery. An animated pan binding therefore cannot evade the zero-pan
  form silently. `_EnableOutlines` animation behavior is unchanged, and
  the clone shares every property, so toggling clips the outline pass
  identically on source and clone.
- Classification outcomes for outline-enabled materials become more
  conservative, and more honest: a polygon whose outline shell is
  unprovable is no longer reported as proven. This changes coverage
  counts, never what moves incorrectly.

## 6. Deferred arms, dated 2026-10-07

1. Distance-fade endpoint arm: fade enabled refuses through a diagnostic;
   admitting it means modeling the distance-driven lerp, whose body is now
   pinned (`:54439-54444`) - a small follow-up slice.
2. AudioLink color: the enabled branch replaces the line color including
   alpha with a runtime-driven color; proving it would need runtime facts,
   so it stays a diagnostic.
3. Derived UV modes (Panosphere, World Pos, Local Pos, Polar, Distorted,
   Matcap): each would need its own coordinate model; they refuse through
   the unsupported-form diagnostic.

## 7. Falsifiers

1. A NaN line-color alpha produces the named diagnostic and leaves the
   triangles unproven.
2. The texture path distinguishes four states: unassigned admits through
   the white default; assigned and whole-texture proven admits through the
   fast path with no chain fact; assigned, unproven, unsupported chain
   forms produce their named diagnostics; assigned, unproven, chain
   admitted leaves the per-triangle verdict to the mapped term, so a
   polygon over a hole stays and a polygon over a solid region moves.
3. Outlines disabled never changes the semantics, whatever the outline
   state.
4. The conversion layer contains no outline symbol: a grep-level falsifier
   test asserts the conversion request and schema carry no outline
   property, so the duplication cannot silently return.
5. A pan of exactly zero on one axis and nonzero on the other is
   unsupported: the gate tests both components.

## 8. Decisions taken

- D1, revised twice on 2026-10-07: outline alpha handling lives entirely
  in the alpha semantics as composed conditional terms with mask-style
  named diagnostics. The conversion layer's outline gate, refusal member,
  schema entry, and presence machinery are deleted, not adapted.
- D2: the outline texture proof is two-stage - the whole-texture fast
  path, then the per-triangle patch proof through the mapped term. The
  chain is pinned to the same degree as the supported features: exact
  integer channel, zero pan, finite affine, main-texture sampler,
  captured alpha channel.
- D3: distance fade refuses through a diagnostic while enabled; endpoint
  modeling is deferred (section 6.1).
- D4: AudioLink color refuses through a diagnostic while enabled; the
  installed source proves the enabled branch replaces the line color
  including alpha, so the refusal is necessary, not conservative.
- D5: In Poiyomi, `_OutlineOverrideAlpha` alters alpha only in the outline
  pass, not the base surface pass. To preserve the controller contract
  (both base surface and outline shell must be opaque), the outline factor
  always multiplies into the base alpha. Replacing the base chain with the
  outline factor is rejected because it would resurrect transparent base
  surfaces. `_OutlineOverrideAlpha` is captured and verified finite, but
  never replaces the base chain.
- D6, superseding the earlier integration decision: no conversion-side
  intersection exists. The classified set is the intersection because the
  semantics compose the outline factor into classification itself.

## 9. Limits

The outline pass's shader state block (its declared Blend, ZWrite, and
stencil lines) remains `[UNPINNED]` verbatim; no gate reads pass state -
the recipe's property writes and the clone's inherited state cover it.
Every fragment fact the semantics depend on is pinned from the installed
attested source (section 3). Classification outcomes for outline-enabled
materials shift conservatively (section 5.4); the corpus projection - nine
of nine observed outline materials convert when their textures prove, and
polygons under non-solid outline regions keep their original material - is
`[INFERENCE]` from 2026-10-07 `[MEASURED]` aggregates; the next authorized
Census Lab build observes it end to end.
