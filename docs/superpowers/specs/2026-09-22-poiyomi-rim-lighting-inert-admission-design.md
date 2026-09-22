# Poiyomi rim lighting inert admission - design

Date: 2026-09-22. Branch: `feat/poiyomi-rim-lighting`, cut from the
rim characterization branch at the characterization commit.

## Privacy note

This design describes private Census Lab fixtures by role only. The
designated locked denim garment fixture appears through its property
values. The design names no avatar, material, texture, or asset, and
no machine paths, host names, ports, or instance identifiers.

## 1. Scope

The characterization of the same date
(`docs/superpowers/investigations/2026-09-22-poiyomi-rim-lighting-vs-liltoon-rim.md`)
established the vendor facts. This design turns one slice of them into
a contract change:

The rim lighting features stop gating the alpha output by their enable
state. The alpha output instead requires the vendor alpha scalar
`_RimApplyAlpha` to prove exactly zero, on every non-forced alpha path,
whatever the enable floats say; at that scalar, every compiled rim arm
is an alpha identity (section 5). Every other rim effect on the alpha
output stays refused, and the color outputs keep their existing
refusals.

Out of scope: depth rim lighting and environmental rim lighting (both
stay gated), the matcap alpha writers (recorded defect-class finding
with its own future owner), any style-float reading, and the color
outputs.

## 2. The vendor facts the contract stands on

Pinned 9.3.64 source, read 2026-09-22, line numbers in the
characterization record:

- The two rim slots compile one shared function per style. The
  liltoon-style arm writes only the final color (25959 to 25960), and
  the UTS2-style arm writes only the base color (25888). Neither reads
  or writes the chain alpha.
- The poi-style arm is the only alpha writer in the family. It adds to
  or multiplies the chain alpha at 25838 to 25843, and only when the
  cbuffer scalar `_RimApplyAlpha` is one or two. At zero, neither
  write branch runs.
- Both slots' calls share that one body, so the single scalar
  `_RimApplyAlpha` governs both slots. `_Rim2ApplyAlpha` is dead in
  the pinned source.
- The style dispatch is compile-time keyword only. The enable floats
  never appear in the fragment; the call sites guard on the enable
  keywords (`_GLOSSYREFLECTIONS_OFF`, `POI_RIM2`), not on the floats.
- The Two Pass source carries the same mechanism (25933 to 25940,
  calls at 30451 and 30463), so the family shares the contract.

## 3. Why the alpha output may complete alone

The build's renderer admission consumes alpha-only semantics through
`AnalyzeAlphaMaterial` (characterization record, section 6). An alpha
output that completes admits the material to triangle classification
even when base color, emission, and normal keep refusing. This is the
path the premultiply and decal slices already opened.

## 4. The candidate designs and the choice

Three shapes were weighed.

Design A (chosen): move the gate. Remove `_EnableRimLighting` and
`_EnableRim2Lighting` from `AlphaFeatureGates` and add
`_RimApplyAlpha` in their place. The existing exact-zero gate
machinery then refuses any missing, non-finite, or nonzero scalar,
naming the property, on every non-forced alpha path of both families.
The evidence request needs no edit, because it unions the gate list.

Design B (the characterization record's sketch): remove the two
enables and add a helper that reads each enabled slot's style float
and, for a poi-style slot, the shared scalar. Sound only while the
style float is trusted over the keyword, and it captures two floats it
does not need: once `_RimApplyAlpha` must prove zero anyway (to stay
sound under keyword disagreement), the style floats answer no question
the alpha path asks.

Design C: require float-keyword agreement by reading material
keywords. A new evidence kind (keyword sets) with new capture and
resolution machinery, for no additional soundness over design A.

Design A is the smallest diff, adds no helper, captures one new
scalar through the existing union, and is sound under every
float-keyword combination. It is the choice.

## 5. Why the claim is independent of the keyword duality

The keyword duality burdened the characterization because an inert
proof that trusts the float can disagree with the compiled arm. This
design does not trust a float against a keyword; it makes the claim
true for every compiled arm at once:

- If the liltoon or UTS2 arm compiled, the arm never writes alpha.
- If the poi arm compiled, its only alpha writes are gated by
  `_RimApplyAlpha`, which the gate proves zero.

Whichever arm compiled, and whichever the enable floats say, the chain
alpha is untouched. The record's open question 1 (keyword agreement)
dissolves for this slice instead of being answered.

## 6. Why conversion needs no new gate

The canonical opaque clone copies the source material and rewrites
only the opaque preset tuple. The rim features survive the copy, so
the clone composes the same rim color as the source on the moved
triangles. On the clone, the opaque blend pair makes the rendered
color independent of the alpha value, so a rim write that never
happens, and a rim color that still does, cannot change what the moved
triangles look like. The framebuffer-alpha posture is the conversion's
existing, accepted one; this slice adds nothing to it.

## 7. The production change

All in `PoiyomiMaterialSemantics.cs`:

1. In `AlphaFeatureGates`, replace the entries `_EnableRimLighting`
   (line 257) and `_EnableRim2Lighting` (line 258) with
   `_RimApplyAlpha` at the same position. The list keeps
   `_EnableDepthRimLighting` and `_EnableEnvironmentalRim`.
2. No helper, no new arrays, no evidence-request edit. The consult at
   line 1149 and the union at line 2500 need no change.
3. The base color and emission gate arrays keep their rim entries, so
   those outputs keep refusing rim materials by name.

Refusal behavior: a rim material with a nonzero or absent
`_RimApplyAlpha` refuses naming `_RimApplyAlpha` instead of naming an
enable. That is a more precise diagnostic for the state that actually
blocks the proof, and it matches the decal slice's precedent of naming
the property that carries the decision.

## 8. The behavior change on already-admitted materials

One narrowing is deliberate. As of 2026-09-22, a material with both rim enable
floats at zero passes the rim gates even if `_RimApplyAlpha` is
nonzero, while the vendor call sites guard on keywords, not floats: a
keyword-compiled poi arm would write alpha behind a passing gate.
After this change, such a material refuses naming `_RimApplyAlpha`.
Every material the change newly refuses was either already refused
(enabled rims) or was admitted only through the float-keyword
disagreement the record could not close. The change strictly widens
admission overall and closes one latent soundness hole at the same
time.

## 9. The test change

The stand-in shaders `PoiyomiSemanticTest` and
`PoiyomiTwoPassSemanticTest` gain `_RimApplyAlpha` with the vendor
default of zero. The enable floats already exist on the stand-ins with
zero defaults.

A new `PoiyomiRimLightingAlphaTests` owns the new contract. The cases,
each with a falsifier comment naming the plausible wrong
implementation it kills:

1. Slot 1 enabled, `_RimApplyAlpha` zero: alpha completes.
2. Both slots enabled, `_RimApplyAlpha` zero: alpha completes.
   Cases 1 and 2 are the RED obligations against the refusal as of
   2026-09-22 naming the enables.
3. Slot 1 enabled, `_RimApplyAlpha` one (add): alpha refuses naming
   `_RimApplyAlpha`. RED.
4. Any rim state, `_RimApplyAlpha` two (multiply): alpha refuses
   naming `_RimApplyAlpha`. Falsifier for an implementation that
   special-cases the add mode only.
5. Both enable floats zero, `_RimApplyAlpha` one: alpha refuses naming
   `_RimApplyAlpha`. Falsifier for an implementation that binds the
   check on the enable floats; this is the closed soundness hole of
   section 8. RED, because the code as of 2026-09-22 completes it.
6. `_RimApplyAlpha` absent from the material: alpha refuses naming
   `_RimApplyAlpha`. Falsifier for an implementation that treats a
   missing property as inert. Fail-closed direction.
7. Forced opaque with `_RimApplyAlpha` one: alpha still claims
   constant one. No-op guard for the short-circuit precedence.
8. The mask-replace material keeps its existing exactly-one proof with
   a rim slot enabled and `_RimApplyAlpha` zero: no new diagnostic
   appears. Guard.
9. The Two Pass family runs cases 1 and 3 on its own stand-in.
   Falsifier for a plain-family-only edit.

The mirror of `AlphaFeatureGates` inside `PoiyomiBaseColorAlphaTests`
swaps the two enable names for `_RimApplyAlpha`, so the loop test
stays the authoritative gate spec.

No census rows change, because no new refusal member appears.

## 10. The honest expectation

The designated fixture gains nothing immediately. Its alpha output's
next blocker is the bound replace mask, and its main texture is mixed
at every captured mip level, so the classifier proves nothing for that
garment under any rim admission. The slice is worth its cost when the
census corpus holds liltoon-style or UTS2-style rim materials without
bound masks, which the corpus check should confirm before or during
implementation.

## 11. Stop conditions

The standing four, plus: a vendor fact contradicting the pinned line
facts of the characterization record, or a corpus check that shows the
slice admits nothing, stops production work and returns to the notes.

## 12. Future slices, recorded not expanded

Depth rim and environmental rim characterization, the matcap alpha
writer gate, and a style-aware refinement if the corpus ever shows a
liltoon-style material with a nonzero `_RimApplyAlpha` worth admitting.
