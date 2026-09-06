# S10 source facts - Poiyomi Two Pass

Date: 2026-09-06. Scope: roadmap slice S10
(`docs/superpowers/plans/2026-09-05-0.1.0-implementation-roadmap.md`).
Method: source read of pinned upstream. No Unity run, no production code
changed.

Source: github.com/poiyomi/PoiyomiToonShader tag `v9.3.64`, zip
downloaded to `/tmp/poiyomi-9.3.64.zip` (sha256
`ed91fb7ca23795664b35038fbb00f3b5b16f10d966804e889cd2f179521be13e`),
extracted to `/tmp/poiyomi-9.3.64-x/`. Tag verified:
`PoiyomiToonShader-9.3.64`. All paths below relative to the extraction.
Line numbers cite `Shaders/9.3/Toon/Poiyomi Toon Two Pass.shader`
(104560 lines) unless another file is named.

## 1. The shader and the pass structure

`.poiyomi/Poiyomi Toon Two Pass` is a separate 104560-line generated
shader beside `.poiyomi/Poiyomi Toon`. Pass tables:

| Pass | Two Pass | plain Toon |
|---|---|---|
| EarlyZ | :4757 | :4675 |
| Base (primary preset) | :9992 | :9910 |
| Add | :30554 | :30435 |
| Base again (`POI_PASS_BASETWO`) | :47578 | - |

The second Base pass carries LightMode `ForwardBase` (:47580) and its
own render state from the `2`-property family: `_Stencil2*` (:47589),
`_ZWrite2`/`_Cull2`/`_ZTest2`/`_ColorMask2` (:47626-47631),
`_BlendOp2`/`_SrcBlend2`/`_DstBlend2`/`_SrcBlendAlpha2`/`_DstBlendAlpha2`
(:47634-47636). [SOURCE]

## 2. The second preset is material data, not a separate mode

`_ModeTwoPass` ("Rendering Preset", :840) is a ThryWideEnum whose
`on_value_actions` write the `2`-family blend properties exactly like
the primary `_Mode` preset writes the unsuffixed ones: Opaque(0) sets
`_SrcBlend2=1 _DstBlend2=0 _ZWrite2=1 _AlphaForceOpaque2=1
_TwoPassAlphaCutoff=0 _AlphaPremultiply2=0`; Cutout(1) adds
`_TwoPassAlphaCutoff=.5`; Fade(2) sets `5/10` blend with `_ZWrite2=0`;
Transparent(3) sets `1/10` with `_AlphaPremultiply2=1`; plus Additive,
Soft Additive, Multiplicative, 2x Multiplicative, TransClipping. The
properties are ordinary material floats, so the editor preset button is
recorded evidence on the material itself. [SOURCE]

## 3. The second pass alpha chain is the SAME chain

The generated fragment source is shared between the passes and branches
on `POI_PASS_BASETWO`/`POI_PASS_ADDTWO` defines (:30483-30492): pass
one applies `_AlphaForceOpaque`, pass two applies `_AlphaForceOpaque2`.
The second Base pass's own fragment (:67926-68108):

1. `if (_AlphaPremultiply2)` premultiply branch (:67926).
2. `poiFragData.alpha = _AlphaForceOpaque2 ? 1 : poiFragData.alpha;`
   (:68078).
3. `float alphaToClip = abs(_TwoPassAlphaCutoffInvert -
   saturate(poiFragData.alpha)); clip(alphaToClip -
   _TwoPassAlphaCutoff);` (:68107-68108). `_TwoPassAlphaCutoffInvert`
   (:860, default 0) flips the kept side: invert 0 keeps
   `alpha >= cutoff`, invert 1 keeps `alpha <= 1 - cutoff` (up to
   saturation/abs rounding, which the exact algebra must model).
4. Blend by the `2`-preset properties of section 2.

Everything upstream of the final mapping - texture sampling, alpha
masks, global masks, audio-link alpha, dither - is the identical code
pass one runs. So no new evidence dimension is needed: the second pass
is the SAME captured alpha chain under a second final mapping
(force-opaque, cutoff with invert, premultiply, blend pair). [SOURCE]

## 4. The multipass rule, made precise

Roadmap rule: "a triangle moves only if every pass that draws it is
proven." For this shader the passes that draw a triangle with alpha
semantics are Base (primary preset) and Base (second preset); the Add
pass is ForwardAdd and the existing frontend's treatment of the primary
Add preset already applies unchanged. The rule therefore lands as a
second proof over the same per-texel alpha domain:

- pass 1 proof: existing classifier over `_Mode`/`_Cutoff`/blend
  properties;
- pass 2 proof: same classifier with the `2`-family mapping -
  force-opaque short-circuits to the trivially-opaque proof, the
  cutoff/invert pair becomes a second exact gate over the proven alpha
  bounds, and `_SrcBlend2=1 _DstBlend2=0 _ZWrite2=1` (or the family
  equivalent the primary rule already accepts) is the required blend
  state.

A triangle moves to the canonical opaque material only when both
proofs hold; otherwise it keeps the source material with the named
per-triangle refusal. Increased uncertainty from pass 2 can never make
the transformation more aggressive. [INFERENCE from cited source lines
and the existing `TriangleAlphaClassifier` contract]

## 5. What the implementation still needs (not yet done)

1. Attestation: measure the Two Pass shader's canonical digest and GUID
   from this pinned artifact the way `PoiyomiMaterialSemantics` pins
   the plain Toon shader, and admit the name in family selection.
2. A second evidence dimension in `GatherAlphaSourceEvidence` reading
   `_ModeTwoPass`-driven `2`-family floats, present only when the
   shader is the Two Pass identity.
3. The classifier/planner seam for the second gate, with RED first: an
   opaque-first-pass plus unproven-second-pass material must refuse
   per triangle against today's first-pass-only proof.
4. Falsifier rows: inverted cutoff, force-opaque2 with nonzero cutoff,
   premultiply2 with transparent 2-preset.
