# lilToon Alpha-Mask Composition Design

Date: 2026-09-07
Status: approved contract (user-authorized follow-on to the conversion-coverage work)
Scope: `Hidden/lilToonCutout` and `Hidden/lilToonTransparent` alpha frontends, the
shader-independent value algebra, and the host texture-field evidence.

## 1. Problem

Five of six garment materials on the reference avatar carry an assigned lilToon
`_AlphaMask`. The current theorems compose only `_MainTex.a × _Color.a`, so every
masked material answers Alpha `Unknown` and no triangle converts. The mask is the
dominant garment blocker. The mip tail and blend tolerance are separate work and
stay out of this design.

## 2. Pinned source equation (lilToon 2.3.4)

`Shader/Includes/lil_common_frag.hlsl:458-476`:

```hlsl
#define LIL_SAMPLE_AlphaMask alphaMask = LIL_SAMPLE_2D_ST(_AlphaMask, sampler_MainTex, fd.uvMain).r
if(_AlphaMaskMode)
{
    float alphaMask = 1.0;
    LIL_SAMPLE_AlphaMask;
    alphaMask = saturate(alphaMask * _AlphaMaskScale + _AlphaMaskValue);
    if(_AlphaMaskMode == 1) fd.col.a = alphaMask;                  // Replace
    if(_AlphaMaskMode == 2) fd.col.a = fd.col.a * alphaMask;       // Multiply
    if(_AlphaMaskMode == 3) fd.col.a = saturate(fd.col.a + alphaMask);
    if(_AlphaMaskMode == 4) fd.col.a = saturate(fd.col.a - alphaMask);
}
```

Supporting facts, all read from the installed vendor package:

- `fd.col *= _Color` runs before the mask block (`lil_common_frag.hlsl:357`), so the
  pre-mask alpha is `mainTex.a(uvMain) × _Color.a`.
- `LIL_SAMPLE_2D_ST(tex,samp,uv)` = `samp(tex, uv*tex##_ST.xy + tex##_ST.zw)`
  (`lil_common_macro.hlsl:272,341`). The mask applies **its own `_ST`** on top of
  `fd.uvMain`.
- `fd.uvMain = lilCalcUV(uv0, _MainTex_ST, _MainTex_ScrollRotate)`. Both families
  already gate `_MainTex_ST` and `_MainTex_ScrollRotate` to exact identity, so
  `uvMain = uv0` and the mask coordinate is the plain affine
  `uv0 × _AlphaMask_ST.xy + _AlphaMask_ST.zw`.
- The sampler is **`sampler_MainTex`**. Wrap, filter, and anisotropy come from the
  `_MainTex` import, not from the mask import.
- The declared default is `_AlphaMask ("AlphaMask", 2D) = "white" {}`. An unassigned
  slot samples the builtin white texture, so `mask.r` is exactly `1` at every mip.
- Modes 3 (add) and 4 (subtract) saturate the sum or difference; they are refused.

## 3. Admitted cases

The exact-one opacity bar is unchanged. `saturate(r·scale + value) == 1` is decided
per case, not by a tolerance:

| Mask state | `scale`, `value` | Mode 1 (Replace) | Mode 2 (Multiply) |
|---|---|---|---|
| unassigned | any finite | `Constant(saturate(s+v))` | term `== 1` → main-only value (existing shapes); term `< 1` → refusal naming `_AlphaMaskScale` |
| assigned | `s == 1`, `v == 0` | `Texture(mask, Red)` | `Product(main × _Color.a, mask.r)` |
| assigned | `s == 1`, `v >= 1` | `Constant(1)` | main-only value (existing shapes) |
| assigned | anything else | refusal naming `_AlphaMaskScale` or `_AlphaMaskValue` | same |

Binary32 exactness holds in every admitted cell:

- `s == 1, v == 0`: the term is the sampled `r` with no arithmetic.
- `v >= 1, s == 1`: `r·1 + v >= v >= 1` in real arithmetic; both FMA and mul-then-add
  round a value at or above one to at least one, so `saturate` is exactly one.
- unassigned: `r == 1`, so the term is `fl(s+v)` then `saturate`, both modeled in
  C# `float` with the same operands.
- product: `fl(fl(m·c)·t) == 1` iff `m == 1 ∧ c == 1 ∧ t == 1`. A float product of
  values in `[0,1]` is one only when both factors are one: a factor below one is at
  most `1 − 2^-24`, and monotone rounding keeps the product at or below it.

Everything outside the table refuses with one diagnostic naming the offending
property. Modes 3 and 4 always refuse.

**Slice decision.** General `(scale, value)` thresholds — `saturate(r·s + v) >= 1`
over a texel domain with `s ≠ 1` — are the deferred cutout-threshold-envelope
contract, not this one. `fl(r·s + v)` versus one can differ between FMA and
mul-then-add in the last ulp, so a threshold predicate needs its own rounding
argument. This design refuses those cases rather than guessing.

## 4. Value algebra

`ScalarSemanticValueKind` gains `ProductOfTextureSamples`: two
`TextureSample`s with their channels and one leading constant multiplier (the
`_Color.a`). Accessors mirror the existing pattern and throw on kind mismatch.
Validation: finite multiplier; both samples non-null.

The frontend emits `Product` only with multiplier in `(0, 1]`. A multiplier above
one is not constructible here because `_Color.a` above one is not admitted, but the
resolver still refuses the shape defensively.

## 5. Resolution

`AlphaSemanticsResolver.Resolve` handles the product kind:

1. multiplier `> 1` → `Refused(UnsupportedMultiplier)`.
2. multiplier `< 1` → `Uniform(MustRemainTransparent)`. The samples are bounded in
   `[0, 1]` by the field contract, so the product is below one at every reachable
   sample; the answer needs the range attestation but no texel.
3. multiplier `== 1` → resolve both factors as plain samples today. Any refusal
   propagates. Both classified → a **composite resolution**.

`AlphaResolution` gains an internal two-factor composite. `Classify` folds the two
per-triangle outcomes with the absorbing lattice:
`MustRemainTransparent` wins; else `Unknown` beats `ProvenOpaque`. Each factor keeps
its own mapping, so a non-identity `_AlphaMask_ST` flows through the existing
`AffineUvTransform` path while the main factor stays identity. A composite
resolution exposes no uniform outcome and therefore merges with nothing in
`DistinctResolutions`, extending the existing "classified never merges" rule.

## 6. Red-channel field evidence

`TextureEvidenceKinds` gains `RedChannel`. `CapturedTextureEvidence` gains
`HasRedChannel` and `RedChannel` beside the alpha pair. The lilToon requests ask for
`_AlphaMask` with `ScaleOffset | SourceIdentity | RedChannel` — deliberately **no
`Sampling`** kind: the sampler is `_MainTex`'s, so the frontend builds the mask
`TextureSample` from the `_MainTex` assignment's captured sampling facts. The
documented reason goes on the request.

`UnityAlphaFieldEvidence.TryCapture` takes a channel. The GPU route selects a
channel-specific predicate shader; the streaming-clone route extracts the requested
component, and the session cache key gains the channel. The provider lookup and
`GatherAlphaFields` key fields by `(TextureSourceId, TextureChannel)`, so one source
can serve alpha as a main texture and red as a mask.

`sampler_MainTex` filtering facts apply to the mask sample, and the mip chain comes
from the mask's own data — the split is faithful to the combined-sampler source.

### sRGB argument (the missing note, written down)

The old refusal said a colour channel "would need the sRGB transfer argument
written down". It is:

- The predicate is the 1-bit test `channel == 1.0`, and the production target is
  `R8_UNorm`.
- The sRGB decode is monotone and maps exactly `1.0 → 1.0`. So a stored byte `255`
  decodes to exactly one, and every other byte decodes strictly below one — whether
  or not the fetch decodes at all. `255 ⟺ exactly 1` survives every decode policy.
- Bilinear and trilinear filtering are convex combinations of decoded values, so an
  all-255 domain filters to exactly one.
- Quantizing any decoded value below one into `R8_UNorm` cannot reach 255: the
  largest value below one that a UNorm source can produce is `decode(254/255)`,
  whose `R8` image is far below 255.

So red fields are admissible for linear and sRGB imports alike, with the existing
format, build-target, and mip-limit gates unchanged.

## 7. Frontends

One shared helper interprets the mask for both families (mode/scale/value reads,
unassigned-white constant, assigned sample construction, refusal naming). Each
frontend keeps its family gates and cutoff bounds untouched.

- **Mode 1 (Replace)**: `_MainTex` alpha and `_Color.a` leave the alpha value. The
  main gates that still matter are the `_MainTex_ST` identity gate (it feeds the
  mask UV through `uvMain`) and the sampling gate (the shared sampler). The
  main-texture source-identity gate is skipped: no main texel is read.
- **Mode 2 (Multiply)**: all main gates stay, plus the mask gates.
- The mask `_ST` needs **no** identity gate: `LIL_SAMPLE_2D_ST` is a plain affine,
  not the rotation path that forced the main-ST family boundary.
- Cutout removes `_AlphaMaskMode` from its zero-gate array and requests
  `_AlphaMaskScale`/`_AlphaMaskValue` like the transparent family already does.
  The twice-margin cutoff bound and coverage transform are untouched; the mask
  composes before the transform in the source, and the classified value expresses
  exactly that.

Animation: `_AlphaMask_ST` rides the `ScaleOffset` kind and derives its animatable
binding name; `_AlphaMaskScale`/`_AlphaMaskValue` are scalars. Non-singleton
animation on any of them refuses through the existing admission machinery. No new
admission code.

## 8. Non-goals

- Modes 3 and 4.
- Threshold envelopes for general `(scale, value)` (deferred contract).
- Outline families (`Hidden/lilToonTransparentOutline` and siblings) — no attested
  frontend exists yet.
- The Poiyomi assigned `_AlphaMask` (separate family, separate equation).
- Mip-tail reachability and near-one tolerance (separate decisions).

## 9. Expected population effect

On the reference avatar: the masked transparent and cutout garment materials stop
refusing on `_AlphaMaskMode` and start classifying per triangle. Triangles whose
main and mask domains are all-255 across the whole mip chain convert; the rest stay
on the original material with an honest content-based refusal. The uniformly-opaque
masked effect material is expected to prove fully opaque. The mip tail keeps its
veto until the reachable-mip contract lands.

## 10. Tests

RED first, per family:

- Mode 1 with an all-white mask → proven opaque even with `_Color.a < 1` (falsifies
  a wrong implementation that composes the color into Replace mode).
- Mode 2 with a mask hole → the hole's triangles do not convert (falsifies
  mask-blind composition).
- Mode 2 product with sub-one `_Color.a` → uniform MustRemainTransparent.
- Unassigned mask, mode 1, `s == 0, v == 0.5` → constant `0.5`.
- Assigned mask with `s == 1, v == 0.5` → refusal naming `_AlphaMaskValue`.
- Modes 3 and 4 → refusal.
- `_AlphaMask_ST` non-identity is admitted through the affine path (mirrors the
  existing main-ST affine tests); non-singleton animation on `_AlphaMask_ST`,
  `_AlphaMaskScale`, `_AlphaMaskValue` refuses.
- Red-field capture: linear and sRGB fixtures; the `255 ⟺ exactly 1` predicate.
- Product resolution: conjunction lattice, missing red field →
  `MissingTextureEvidence`, refusal propagation.
- Schema pins updated to the new exact request lists (contract change, not a
  weakening: modes 1–2 leave the blanket-refusal test rows, which move to
  threshold and mode-3/4 rows).
