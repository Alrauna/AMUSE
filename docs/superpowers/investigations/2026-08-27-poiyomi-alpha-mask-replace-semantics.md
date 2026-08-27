# Poiyomi Alpha Mask Replace Semantics — Investigation

**Status: implemented and merged.** The bounded Replace / no-assigned-mask case described
here was implemented and merged through PR #22 (merge commit `11765f4`).

Still unsupported, deliberately:

- **Assigned `_AlphaMask`** — deferred to the real texture-evidence investigation, which
  is a prerequisite rather than a detail of this work (§5).
- **Multiply, Add and Subtract mask modes** — each combines a mask term the closed scalar
  vocabulary cannot express.
- **Affine texture expressions** such as `saturate(mask.r + offset)` — the vocabulary has
  a multiplied texture form but no additive one, and no additive form was introduced.

Census Lab observations in this note are architectural pressure and validation evidence.
They are not correctness authority: the supported cases are established by the pinned
shader source and pinned by public deterministic tests.

## 1. The pinned Replace equation

From the attested Poiyomi Toon 9.3.64 source — a public, pinned identity already recorded
in AMUSE's production attestation, re-verified by asset GUID and normalized source hash
before any fact was read from it:

```hlsl
if (_MainAlphaMaskMode)
{
    float alphaMask = POI2D_SAMPLER_PAN(_AlphaMask, _MainTex,
                          poiUV(poiMesh.uv[_AlphaMaskUV], _AlphaMask_ST),
                          _AlphaMaskPan.xy).r;
    alphaMask = saturate(alphaMask * _AlphaMaskBlendStrength
                         + (_AlphaMaskInvert ? _AlphaMaskValue * -1 : _AlphaMaskValue));
    if (_AlphaMaskInvert) alphaMask = 1 - alphaMask;
    if (_MainAlphaMaskMode == 1) poiFragData.alpha = alphaMask;      // Replace
    ...
}
```

In Replace mode the mask expression **is** the alpha; it does not combine with `_Color.a`
or `_MainTex.a` at all. Declared defaults: `_AlphaMask = "white"`,
`_AlphaMaskBlendStrength = 1`, `_AlphaMaskValue = 0`, `_AlphaMaskInvert = 0`,
`_AlphaMaskUV = 0`, `_AlphaMaskPan = (0,0,0,0)`.

## 2. Does existing texture evidence reproduce the sampling domain?

This was the load-bearing question before designing the assigned-mask path. The answer
split: the coordinate model fits, the data acquisition does not.

### 2a. Coordinate and sampler model — fits the existing vocabulary

| Shader fact | AMUSE model | Verdict |
|---|---|---|
| `poiUV(uv, ST) = uv * ST.xy + ST.zw` | `UvMapping(channel, scale, offset)` | exact match |
| `poiMesh.uv[_AlphaMaskUV]`, channel 0–3 | `UvMapping.Channel`, integer 0–3 | exact match |
| Higher `_AlphaMaskUV` modes — Panosphere, World, Polar, Distorted, Local, Matcap | computed, not a mesh UV channel | must refuse |
| `POI_PAN_UV(uv,pan) = uv + _Time.x * pan` | time-dependent | must refuse unless pan is exactly zero |
| `UNITY_SAMPLE_TEX2D_SAMPLER(_AlphaMask, _MainTex, …)` | sampler is **`_MainTex`'s**, not the mask's | see note |
| `applyParallax` may overwrite `poiMesh.uv[_ParallaxUV]` | perturbs the sample coordinate | must gate `_PoiParallax` |

`TryGetSupportedUvMapping` already enforces exactly this shape for `_MainTex`, so
extending it to `_AlphaMask` would parameterize an existing helper rather than introduce a
framework.

**Sampler note — non-obvious and load-bearing.** The mask is sampled through `_MainTex`'s
sampler, so its wrap and filter state come from `_MainTex`'s import settings, not the
mask's own. Reusing the existing main-texture sampling helper for a mask sample would
therefore be *correct*, not a shortcut. Corollary: when `_MainTex` is unassigned the
sampler is Unity's default-texture sampler, which AMUSE does not capture.

### 2b. Data acquisition — the actual blocker

`UnityAlphaFieldEvidence` is the only producer of the field the classifier consumes, and
it requires all of:

- a readable texture — the only route to the data is `GetPixels32`;
- a single mip level — the classifier models one texel grid;
- an uncompressed format — compressed formats were measured to round a source alpha of 254
  up to 255, i.e. fabricated opacity;
- the alpha channel. The source states why: *"Only Alpha has a producer today. A colour
  channel would additionally need the sRGB transfer argument written down, so it fails
  closed."*

There is no readable-copy machinery anywhere in the package — no blit, raw-data, render-
texture, or re-import path. Textures are read as-is.

**Ordinary imported avatar textures do not satisfy these preconditions.** In the
authorized corpus, the textures reachable through supported Poiyomi materials were
routinely non-readable, mipmapped and compressed, and none of them could satisfy the field
evidence. This was not a property of unusual assets; it is what normal avatar import
settings produce.

Two consequences follow, and the second is the larger one:

1. A red-channel producer alone would unlock nothing. The `TextureChannel.Red` gap is real
   but is not the binding constraint.
2. **The same limitation constrains the already-merged `_MainTex` texture-backed alpha
   path.** Every texture-backed proof that exists is exercised only by synthetic test
   fixtures, which import textures readable, unmipped and uncompressed.

## 3. Implemented case — Replace with no assigned mask

With `_AlphaMask` unassigned the shader binds its declared `"white"` default, so the
sampled `.r` is exactly 1 and the expression collapses to a constant:

```
raw       = saturate(1 * BS + (invert ? -V : V))
alphaMask = invert ? (1 - raw) : raw
alpha     = alphaMask                                (Replace)
```

Admission conditions, all proven rather than assumed: the mode is exactly 1; `_AlphaMask`
is unassigned; blend strength and value are finite; invert reads as an exact binary; and
the computed intermediate and result are finite.

The result is `ScalarSemanticValue.Constant(...)`, already in the vocabulary.

**Floating-point exactness is provable here**, which is why this case is safe while the
general one is not. Because the sampled mask is exactly `1`, the shader's fused
multiply-add and a C# multiply-then-add agree bit-for-bit: `1 * BS` is exact, so
`fma(1, BS, V) == BS + V` under either rounding. No such argument is available when the
mask is a texture sample.

This case admits `_AlphaMaskInvert = 1` naturally — inverting a constant is arithmetic on
a constant and needs no new abstraction. Invert is refused only on the assigned-mask path,
where `1 - x` over a sample is not expressible.

Sampling and UV facts are irrelevant on this path: with no texture there is no sample, so
the mask's UV channel, panning and scale/offset cannot affect the result.

**Replace with no assigned mask occurs in realistic material use.** It is not a synthetic
shape invented to make the feature land, and it yields materials whose alpha is provably
constant.

## 4. Parallax

`applyParallax` overwrites `poiMesh.uv[_ParallaxUV]` before both the `_MainTex` sample and
the mask sample. `_PoiParallax` appeared in the base-colour and normal feature gates but
not in the alpha gates, so an enabled parallax previously allowed a texture-backed alpha
claim describing a view-dependent sampling domain — a false positive, the dangerous
direction.

The merged fix proves `_PoiParallax` off immediately before a texture-backed `_MainTex`
alpha claim and nowhere earlier. **Parallax invalidates texture-coordinate-dependent
evidence, not constants:** forced-opaque, mask-off constant, ignored-main-texture and
Replace constants all remain complete when parallax is enabled.

`_PoiInternalParallax` was checked separately and is *not* alpha-relevant — it takes the
mesh by value and writes only base colour — so its absence from the alpha gates is
correct.

Enabled parallax was not observed to be common in the authorized corpus, so this was a
latent rather than actively firing false positive.

## 5. Deferred — Replace with an assigned mask

Blocked on a host-capability problem, not on semantics. Delivering real coverage needs all
of:

1. a colour-channel field producer, including the sRGB/linear transfer argument written
   down and measured the way the alpha allow-list was;
2. a texture-evidence request kind for the red channel;
3. **a way to read texture data that ordinary avatar textures can satisfy** — the binding
   constraint from §2b;
4. `_MainTex` assigned, since the mask borrows its sampler.

(3) is the substantial one: a build-time readable/uncompressed copy, or reading the source
asset rather than the runtime texture. Either touches the exactness argument the whole
triangle classifier rests on, so it belongs in its own investigation rather than here.

**Assigned masks do occur in realistic material use**, so this deferral is a genuine
coverage gap rather than a theoretical one.

## 6. Census Lab findings

Census Lab was used **read-only**; nothing in it was modified. The authorized private root
is `Assets/!CENSUSLAB/` and the authoritative corpus is `Assets/!CENSUSLAB/Scenes/`. The
Lab's location on disk is discovered at runtime and is never recorded here. Findings below
are qualitative; no corpus counts, ratios or per-entity observations are published, and no
new publishable Census metric was introduced.

- Both Replace with no assigned mask and Replace with an assigned mask occur among
  supported Poiyomi materials. The implemented case is therefore useful, and the deferred
  case is a real gap.
- Materials that never reach the alpha feature gates — because they force opacity earlier
  — are unaffected by this work either way.
- Mask configurations were not exotic: inversion and non-default blend strengths were not
  what stood in the way.
- Textures reachable through these materials carry ordinary avatar import settings and do
  not satisfy the field-evidence preconditions (§2b).
- Enabled parallax was not observed to be common, making the §4 defect latent.

**Success-criterion answer.** A texture-backed Replace case flowing through the classifier
to produce a *mixture* of proven-opaque and non-opaque triangles is achievable on synthetic
fixtures but not on the authorized corpus, because those textures cannot be read. The
implemented case produces uniformly constant-alpha materials — a valid and useful outcome,
but not the mixture. **Real texture-backed triangle separation is not available as a
result of this work.**

## 7. What shipped

Changes were confined to the Poiyomi frontend and its evidence request:

1. `_MainAlphaMaskMode` removed from the alpha feature gates and interpreted instead,
   while remaining explicitly requested.
2. `_PoiParallax` proven off immediately before texture-backed `_MainTex` alpha only.
3. The mask scalars and `_AlphaMask` (assignment only, no further texture facts) added to
   the alpha evidence request — which also makes them proof-relevant for animation through
   the existing admitted-state machinery, with no new animation code.
4. Mask-off behavior preserved unchanged.

`TriangleAlphaClassifier` stays shader-independent and untouched, as do
`AlphaSemanticsResolver`, texture field production and NDMF integration. No render-state
work, opaque conversion, mesh mutation or new semantic vocabulary was introduced.

Known conservative limitation carried forward: the relevance schema is static, so animating
`_PoiParallax` is treated as proof-relevant even on constant-alpha paths where it cannot
affect the result. False negative only.
