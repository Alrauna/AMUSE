# Poiyomi decal inert slots - design

Date: 2026-09-22. Branch: `feat/poiyomi-decal-slots`.

## Privacy note

This design describes private Census Lab fixtures by role only. The
designated locked denim garment fixture appears through its property
values. The record names no avatar, material, texture, or asset, and
no machine paths, host names, ports, or instance identifiers.

## 1. Scope

The investigation of the same date
(`docs/superpowers/investigations/2026-09-22-poiyomi-decal-slot-vs-liltoon-layer-alpha.md`)
established the vendor facts. This design turns one slice of them into
a contract change:

An enabled poiyomi decal slot is an alpha identity exactly when its
`OverrideAlpha` mode is proven zero. The alpha output may complete for
such a material. Every other decal effect on the alpha output stays
refused, and the color outputs keep their existing refusals.

The separate audio link decal module writes the chain alpha on its
own, so its controls-alpha weight joins the alpha gate set in the same
change. This is required for the soundness of the new admission, not a
separate feature.

Out of scope: the override-alpha writer modes one to six, the min and
max modes, decal color or normal admission, and the audio link
decal module's color effects.

## 2. The vendor facts the contract stands on

Pinned 9.3.64 source, read 2026-09-22, line numbers in the
investigation record:

- The four slots run once per pixel in the main fragment at line
  29976, after the alpha options and before the vendor premultiply at
  30216 and the force-opaque forcing near line 30366.
- A slot writes the chain alpha only inside its override-alpha block
  at lines 22958 to 22978, and only when the slot's `OverrideAlpha`
  float is nonzero. Six modes exist: replace, multiply, add,
  subtract, min, max, each composed through a mask-weighted lerp.
- With `OverrideAlpha` zero, the slot composes the base color, the
  emission, and a global mask write, and writes no alpha.
- The audio link decal module writes the chain alpha at line 24833
  through `lerp(alpha, alpha * audioLinkValue, _ALDecalControlsAlpha)`.
  A zero weight is an identity. The module runs independently of the
  four slots.

The lilToon equivalence that gives this slice its shape: a liltoon
layer at alpha mode zero and a poiyomi slot at `OverrideAlpha` zero
are the same object, an inert term. Production already models inert
terms for liltoon in `LilToonLayerAlphaTerm`.

## 3. Why the alpha output may complete alone

The build's renderer admission for alpha separation consumes
alpha-only semantics. `UnityMaterialSemantics.AnalyzeAlphaMaterial`
runs only `InterpretVerifiedAlpha` for the poiyomi families. The base
color, emission, and normal outputs feed other consumers, not the
classification.

So an alpha output that completes admits the material to
classification even when the color outputs refuse. A material whose
classification then proves opaque triangles offers them to conversion.
This is the path the parity premultiply slice already opened for
premultiply materials.

## 4. Why conversion needs no new gate

The canonical opaque clone copies the source material and rewrites
only the opaque preset tuple (`_Mode` 0, `_AlphaPremultiply` 0, the
blend and clip state). The decal features survive the copy, so the
clone composes the same decal color as the source on the moved
triangles. On the clone, the opaque blend pair makes the rendered
color independent of the alpha value, so the decal's alpha behavior
cannot change what the moved triangles look like.

The framebuffer alpha channel of the clone differs from the source at
alpha one when the decal or the chain itself renders a sub-unit
alpha. That divergence is the conversion's existing, accepted posture:
the contract preserves visibility, not framebuffer identity under
every hypothetical read of the alpha channel. This slice adds nothing
to it.

## 5. The keyword duality

Each slot toggle is a float plus a borrowed builtin keyword, and the
vendor compiles the slot's block under the keyword with no runtime
float check. A keyword set with a float of zero would run the block
while the float says off. Production reads floats for every feature
toggle, including the existing keyword-toggled `_BSSEnabled` alpha
coverage gate. This design keeps that established trust boundary: the
serialized float is the evidence. The float-keyword duality stays an
open question in the investigation record, and the inert proof itself
is independent of it, because `OverrideAlpha` zero keeps the block
harmless even when it runs.

## 6. The production change

All in `PoiyomiMaterialSemantics.cs`:

1. Remove the four `_DecalEnabled` entries from `AlphaFeatureGates`.
2. Add `_ALDecalControlsAlpha` to `AlphaFeatureGates`. The existing
   zero-gate machinery then refuses any non-finite or nonzero weight,
   naming the property, on every non-forced alpha path of both
   families.
3. Add two arrays: the four slot enable names and the four slot
   `OverrideAlpha` names. Slot zero's names are the unsuffixed vendor
   properties.
4. Union both arrays in `CreateAlphaEvidenceRequest`. The plain and
   Two Pass requests and the live-material full request all derive
   from it, so the capture and the animation closure cover the new
   scalars without further wiring.
5. Add a helper that walks slots zero to three and returns the first
   refusal property name or null:
   - enable scalar absent or non-finite: refuse naming the enable.
   - enable zero: the slot is inert, continue.
   - enable nonzero: `OverrideAlpha` scalar absent, non-finite, or
     nonzero: refuse naming that slot's `OverrideAlpha`. Proven zero:
     inert, continue.
6. Call the helper inside `InterpretSingleFamilyAlpha` immediately
   after the `AlphaFeatureGates` check, so precedence, the
   forced-opaque short-circuit, and the cutout route keep their
   current shape.

Refusal names are the actual vendor property names, so the renderer
report tells the user which slot's alpha mode blocks the proof. A
refusal that used to name `_DecalEnabled` now names
`_DecalOverrideAlpha` when a mode is the cause.

The base color, normal, and emission gate arrays keep their decal
entries. A material with an enabled decal still refuses those outputs;
the renderer conversion path and the full material analysis are
unchanged.

## 7. The test change

The stand-in shaders `PoiyomiSemanticTest` and
`PoiyomiTwoPassSemanticTest` gain the five new floats with vendor
defaults of zero: the four `OverrideAlpha` properties and
`_ALDecalControlsAlpha`.

`PoiyomiBaseColorAlphaTests` mirrors the production `AlphaFeatureGates`
list; its mirror drops the four decal entries so the loop test stays
the authoritative gate spec.

A new `PoiyomiDecalSlotAlphaTests` owns the new contract. The cases,
each with a falsifier comment naming the plausible wrong
implementation it kills:

1. Slot 0 enabled, `OverrideAlpha` zero: alpha completes.
2. Slot 0 enabled, each mode one to six: alpha refuses naming
   `_DecalOverrideAlpha`.
3. Slots 1 to 3 enabled with a mode: each refuses naming that slot's
   own property.
4. Slot disabled with a mode set: alpha completes, no decal
   diagnostic.
5. `_ALDecalControlsAlpha` nonzero with every decal off: alpha refuses
   naming the weight.
6. Forced opaque with an enabled decal: alpha still claims constant
   one. No-op guard for the short-circuit precedence.
7. The inert admission keeps a mask-replace material's existing
   exactly-one proof intact: no new diagnostic appears.

Cases 1 to 5 are the RED obligations. Cases 6 and 7 are guards that
must pass before and after.

## 8. Stop conditions

The standing four, plus: a need for a shared inert-term abstraction
across frontends, or a vendor fact contradicting the pinned line
facts, stops production work and returns to the notes.
