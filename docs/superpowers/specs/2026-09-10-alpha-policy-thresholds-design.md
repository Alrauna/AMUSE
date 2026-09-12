# Alpha Policy Thresholds Design

Date: 2026-09-10. Base: `main` at `64a71a0`. Branch: `feat/min-preserved-transparency`.

Investigation: `docs/superpowers/investigations/2026-09-10-minimum-preserved-transparency-value.md`. Spec self-review pass 1 applied.

Reviewed 2026-09-11. The review proposed collapsing the noise bound into the opaque bound (`n := O`) to reduce surface. Rejected after analysis: the noise set would become every non-opaque texel, leaving no configuration that erases sparse strays while respecting witness-band translucency, and duplicating the capture threshold for near-opaque texels. All three settings stand.

## 1. Overview

This design adds three user policy settings to the AMUSE avatar optimizer component:

1. **Minimum Opaque Alpha Percentage.** Texel alpha at or above this value counts as opaque evidence for sources without a shader cutoff.
2. **Transparency Noise Gate Percentage.** Texel alpha below this value is noise. Noise texels stop witnessing transparency when they are sparse.
3. **Maximum Noise Texel Percentage.** The sparsity bound from item 2. Noise substitution fires for a polygon only when its noise texels are strictly under this share of its consulted texels.

The settings are policy. They change the evidence the proof consults, never the classifier's arithmetic. A triangle the proof consults is classified exactly as before over the evidence it is handed. The origin request named one value, "Minimum Preserved Transparency Value". The grilling session split it into the pair above plus the density bound.

Defaults are inert. `100 / 0 / 2` reproduces the base behavior for every existing build. The report marker appears only when policy is active.

## 2. Exact byte bounds

- `O` (opaque bound) is the smallest byte `b` with `b * 100 >= V_a * 255`. `O = ceil(V_a * 255 / 100)` in exact integer arithmetic. `V_a = 100` gives `O = 255`, the base exact behavior. `V_a = 0` gives `O = 0`.
- `n` (noise bound) is the smallest byte `b` with `b * 100 >= V_b * 255`. A texel is noise exactly when `byte < n`. `V_b = 0` gives `n = 0`, so no texel is noise.
- The inspector clamps `V_b <= V_a - 1`. At `V_a = 0` the clamp forces `V_b = 0`. The clamp keeps the three-band model non-degenerate. The grilling session confirmed it prevents no reasonable use case: any `V_b >= V_a` configuration has a clamped twin that differs only on a witness sliver of at most one percent alpha, and only for sparse texels.

Worked bounds: `V_a = 99` gives `O = 253`. `V_b = 2` gives `n = 6`, so bytes `0` through `5` are noise. With the clamp, bytes in `n .. O-1` are the witness band, `>= O` is the opaque band, `< n` is the noise band.

`V_a = 0` gives `O = 0`. That is the maximum-aggression consent: every texel becomes opaque evidence and no witness band exists. It is allowed and disclosed, not clamped.

## 3. Capture realization

`[Base fact]` Evidence capture binarizes every texel against a `cutoffThreshold` float, `1.0f` by default, and shader frontends supply the material cutoff for cutout-style sources.

This design maps `V_a` to `O` and hands capture an exact byte comparison. For a source **without a shader cutoff**, a texel is opaque evidence exactly when `byte >= O`. For a cutout source, the shader cutoff stays authoritative and all three settings are inert for it. A texel between `V_b` and the shader cutoff is discarded at runtime. Claiming it opaque would restore geometry the author hid. That boundary is absolute.

All three capture paths take `O` and `n`:
- `SourceImageAlphaReader` builds chains from the authoring image. It owns its box filter, so it also owns masked averaging (section 4).
- `UnityStreamingTextureEvidence` tries the source-image path first. Its fallback reads published mip data and gets the limited realization of section 4.
- `UnityGeneratedTextureEvidence` reads published mip data and gets the same limited realization.

Corrected 2026-09-11 during implementation: the capture layer has a fourth route — the direct GPU predicate-shader path in `UnityAlphaFieldEvidence` for ordinary non-streaming, non-generated textures — and it is the primary path for real avatar content. The binding intent of this section is that the policy reaches every capture route. Task 3b of the implementation plan extends the direct route accordingly: bounds-aware predicate shaders with inert-byte-identical defaults.

Cache keys: the generated-path session key and the streaming-path disk key already carry a threshold slot. That slot carries `O`, and the keys gain `n`, because the erased mask changes the produced chain. The density percent never enters a key: it acts at classification time and never changes a chain.

## 4. Noise gate: erased mask and masked averaging

The two settings cover complementary failures. The gate alone converts polygons whose only fault is sparse strays on masked paths. The opaque bound alone handles uniform near-miss noise and near-opaque content. Dense faint content converts under neither unless the user accepts that flattening explicitly.

**Per-polygon density decision.** The density rule lives at classification time, per polygon, because sparsity is a property of the footprint, not the texture. For one polygon at one level, let `c` be the number of consulted texels (the support regions the scan already enumerates) and `e` the number of erased texels among them. The polygon treats its erased texels as opaque evidence exactly when `e * 100 < F * c` in exact integer arithmetic. Equality refuses. When the density rule does not fire, erased texels keep witnessing and the polygon fails closed.

`F` is the mapped byte-free percent from the component, compared as integers. `F = 0` means substitution never fires, so a raised `V_b` alone converts nothing.

**Fast paths.** `AlphaTextureData` whole-texture flags answer `ProvenOpaque` and `MustRemainTransparent` before any scan. When a texture has any erased texel at any consulted level, the fast paths are bypassed and the scans decide. The flags stay byte-based: a texture carrying any erased byte cannot satisfy the all-255 opaque flag, so a mixed texture always reaches the scans, and an all-noise texture answers `MustRemainTransparent` from the non-opaque flag with the same verdict the density rule would produce. A fully noise texture has every polygon at `e = c`, so no polygon converts.

**Limited realization on published chains.** The streaming fallback and the generated path read mip data Unity published. Their coarser levels arrive pre-averaged, so masked re-averaging is not available there. On those paths erased texels stop witnessing at their own level only, which is sound and nearly inert for the stray case. The source-image path, which the streaming path prefers when a source file exists, gets the full masked realization. This limitation is documented in the tooltip and revisited only on measured need.

## 5. Classifier integration

The per-filter support scans change one predicate: a texel passes when it is opaque evidence, which is `byte >= O`, or when it is erased and the polygon's density rule fires. The witness condition is `n <= byte < O`, or erased with the density rule not fired. The chain conjunction, the envelope arithmetic, and every degeneracy rule are unchanged. No new refusal enum member is needed: dense noise keeps triangles unproven through the normal outcomes, and nothing refuses that previously converted.

## 6. Inspector

The Alpha Separator foldout gains three controls under the existing three, in this order:

1. "Minimum Opaque Alpha Percentage", integer slider `0` through `100`, default `100`.
2. "Transparency Noise Gate Percentage", integer slider `0` through `100`, default `0`. The inspector clamps the drawn value to `V_a - 1` when `V_a > 0`, else `0`.
3. "Maximum Noise Texel Percentage", integer slider `0` through `100`, default `2`.

Every control gets a tooltip in plain technical English. The tooltips state the flattening: alpha between the opaque bound and full opacity renders fully opaque after conversion, and noise texels render fully opaque where the gate fires. The gate tooltip states the published-chain limitation from section 4.

**Corrected 2026-09-11 during the slider flip.** The inspector renamed
all six controls and inverted the per-polygon pair. The menu order is
now: "Smallest Tested Mipmap", "Smallest Tested Texture", "Minimum
Opaque Coverage (Per Material)", "Alpha Upper Clamp (Per Texture)",
"Minimum Opaque Coverage (Per Polygon)", "Alpha Upper Clamp (Per
Polygon)". "Alpha Upper Clamp (Per Texture)" is the old "Minimum
Opaque Alpha Percentage" with an accurate scope label. "Minimum Opaque
Coverage (Per Polygon)" is the old density cap with the value
inverted. The classifier cap is `100 - coverage`. "Alpha Upper Clamp
(Per Polygon)" is the old noise gate with the value inverted. The
clamp bounds the tolerated stray band from above, so a clamp of 100
maps to the inert noise bound 0. The density comparison flipped from a
strict share to a share at or under the cap, so a polygon now moves
when the erased share equals the cap. Defaults of 100 on every slider
reproduce the inert state the old `100 / 0 / 2` produced. Section 8's
byte-identity claim and its characterization guard keep holding at the
new defaults.

## 7. Report marker

`AvatarSummary` gains one figure: triangles moved while alpha policy was active. The figure appears when `V_a < 100` or `V_b > 0`, and is absent otherwise. Per-triangle attribution of which conversions relied on policy is out of scope; the wording says "while active", not "because of". This is the disclosure surface the grilling session chose over a per-build dialog.

## 8. Default behavior

Defaults `100 / 0 / 2` produce `O = 255`, `n = 0`, and an inert gate. The capture compares bytes exactly as the base commit does, the mask is empty, no fast path is bypassed, and the summary string is unchanged. A characterization guard test asserts this equivalence end to end.

## 9. Test strategy

RED first, each against a named wrong implementation. Falsifier numbering follows the house convention.

1. **Bound mapping.** `V_a = 100` maps to `255` exactly; `V_b = 0` maps to no noise; the ceil edges at `V_a = 99`, `V_b = 2`; the clamp at `V_a = 0` and at `V_a = 1`. Kills a rounding that shifts a bound by one byte, and a clamp that allows `V_b = V_a`.
2. **Masked averaging.** A 255 field with one byte-3 stray and `n = 6`: every level averages 255. An all-noise block erases wholly. Kills averaging before masking, the dilution wrong implementation from the investigation table.
3. **Density boundary.** One stray among 64 consulted texels with `F = 2` substitutes and proves. Ten among 64 does not. Exactly-at-bound refuses. Kills `>=` in place of `>`, and a per-texture density reading.
4. **Fast-path bypass.** A texture with one erased texel and otherwise all `O`-or-above must not answer from the whole-texture flags.
5. **End to end through the production barrier.** A fade material with a sparse-stray texture converts; the same texture with dense noise does not; a cutout material ignores both sliders at any values; `V_a = 99` converts a fade whose texels all sit at 254.
6. **Cache keys.** Changing `V_b` re-captures on the streaming and generated paths.
7. **Report marker.** The summary figure appears exactly when policy is active.
8. **Characterization guard.** Defaults reproduce base outcomes on the existing fixture set.
9. The full EditMode suites pass with zero failures.

## 10. Out of scope

- Mip-averaging tolerance, dropped in the grilling session: quantization noise is the opaque bound's territory, strays are the gate's, and forgiving real averaged transparency stays refused.
- Per-material or per-renderer slider overrides.
- Shader frontend changes. The design lives below the frontends, at capture.
- Re-averaging published mip data for generated textures, beyond the documented limitation.
