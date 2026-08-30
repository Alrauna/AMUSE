# lilToon 2.3.4 cutout alpha semantics — B2 design

## 1. Question and bounded scope

Under exactly which captured material, texture, UV, animation, shader-source, and
compilation preconditions can AMUSE prove that a triangle rendered by the
supported cutout source is visually opaque and therefore eligible for the
already-characterized opaque conversion?

The only positive candidate is, exactly as characterized by B1:

- regular lilToon `jp.lilxyzw.liltoon` `2.3.4`;
- non-Lite, non-Tessellation, non-Multi;
- no outline;
- cutout source (`Hidden/lilToonCutout`);
- no-outline opaque target (`lilToon`).

This note is design and source investigation only. It creates no production
code, no tests, no implementation, and no source-attestation or callback
validation work. Transparent, one-pass/two-pass, outline, Lite, Tessellation,
Multi, Gem, Fur, Refraction, RefractionBlur, overlay, fakeshadow, and every
other family are out of scope and must refuse. Census Lab was not used.

Labels: `[MEASURED]` observed from executed commands or downloaded artifacts;
`[SOURCE]` read from the pinned source or current AMUSE code (cited);
`[INFERENCE]` bounded conclusion from those facts; `[DECISION NEEDED]` a choice
B2 cannot resolve. lilToon paths below are relative to
`Packages/jp.lilxyzw.liltoon/` (the installed package layout B1 measured);
line numbers refer to the pinned tag tree, which this note proves
byte-identical to the official release archive modulo CRLF (§2).

## 2. Verified environment and source pin

| Fact | Value | Evidence |
|---|---|---|
| Branch / base | `design/liltoon-cutout-alpha-semantics` at `0e90f014c30cb16cc2a9313d5a1258a5ef1ac423` | `[MEASURED]` `git rev-parse HEAD`; base is `origin/main` (PR #38 merge) and contains B1 `034dd52` |
| B1 note present on base | `docs/superpowers/investigations/2026-08-30-liltoon-opaque-characterization.md` | `[MEASURED]` `git cat-file -e origin/main:<path>` after `git fetch --prune origin` |
| Upstream clone | official GitHub `lilxyzw/lilToon`, shallow clone of tag `2.3.4` | `[MEASURED]` |
| Tag commit | `252fd8cfc46106d4967e95b3f2c788418502f227`, `git describe --tags --exact-match` = `2.3.4` | `[MEASURED]` |
| Package identity | `jp.lilxyzw.liltoon` `2.3.4`, release URL `…/releases/download/2.3.4/jp.lilxyzw.liltoon-2.3.4.zip?` | `[SOURCE]` `Assets/lilToon/package.json` |
| Release archive SHA-256 | `34d172761c51aa9469a904704109086aafa6125a4fa0e058766e2ddc73d3b303` | `[MEASURED]` downloaded archive — equals B1 §3's recorded hash |
| Archive ↔ tag tree identity | every decisive file (`lts_cutout.shader`, `ltspass_cutout.shader`, `lil_pass_forward_normal.hlsl`, `lil_common_frag.hlsl`, `lil_common_frag_alpha.hlsl`, `lil_common_vert.hlsl`, `lil_common_functions.hlsl`, `lilToonSetting.cs`, `lilOptimizer.cs`, `VRChatModule.cs`) byte-identical modulo CRLF line terminators | `[MEASURED]` `diff` after `tr -d '\r'` |
| Unity of record | `2022.3.22f1` with the embedded 2.3.4 install, from B1 §3 | `[MEASURED]` B1 (no Unity instance was launched for B2) |

`[INFERENCE]` Because the archive B1 digested is byte-identical (modulo
newlines, which AMUSE's canonicalization normalizes) to the tag tree cited
here, B1's installed-asset digests and this note's source-line citations
describe the same content. No scratch Unity project was needed: every
correctness-relevant question resolved from source.

## 3. Exact source equation and coverage path

### 3.1 Pass and shader identity

- The material-facing cutout shader is `Shader/lts_cutout.shader`
  (`RenderType=TransparentCutout`, `Queue=AlphaTest`,
  `lts_cutout.shader:640`); its only real passes are
  `UsePass "Hidden/ltspass_cutout/{FORWARD, FORWARD_ADD, SHADOW_CASTER, META}"`
  (`lts_cutout.shader:641-644`). The remaining `Pass` has
  `LightMode = Never` and never executes (`lts_cutout.shader:645-655`).
  The no-outline selection of exactly these four identities was measured by
  B1 §5 and matches the outline-bearing variant `lts_cutout_o.shader:641-646`,
  which additionally references `FORWARD_OUTLINE`/`SHADOW_CASTER_OUTLINE`.
- The pass asset defines `#define LIL_RENDER 1`
  (`Shader/ltspass_cutout.shader:639`) — the cutout render mode — and
  unconditionally defines **every** `LIL_FEATURE_*` block
  (`ltspass_cutout.shader:644-747`). There are no mode keywords.
- `FORWARD` and `FORWARD_ADD` both compile the single `frag` of
  `Includes/lil_pass_forward.hlsl` → `lil_pass_forward_normal.hlsl`
  (`lil_pass_forward.hlsl:4-8`; pass `HLSLPROGRAM` at
  `ltspass_cutout.shader:785-804, 886-905`). Render state:
  `FORWARD` has `Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha]
  [_DstBlendAlpha]`, `BlendOp [_BlendOp], [_BlendOpAlpha]`,
  `AlphaToMask [_AlphaToMask]`, `ZWrite [_ZWrite]`
  (`ltspass_cutout.shader:777-783`); `FORWARD_ADD` has `ZWrite Off`,
  `ZTest LEqual`, `Blend [_SrcBlendFA] [_DstBlendFA], Zero One`,
  `BlendOp [_BlendOpFA], [_BlendOpAlphaFA]`, `AlphaToMask [_AlphaToMask]`
  (`ltspass_cutout.shader:878-884`). Fresh cutout defaults were measured by
  B1 §9 (`_SrcBlend=1, _DstBlend=0, _AlphaToMask=1, _ZWrite=1`).

### 3.2 Vertex-stage coverage gates (regular, no-outline path)

1. `_Invisible` — when nonzero, `vert` returns the zero-initialized `v2f`
   (`LIL_VERTEX_CONDITION` = `_Invisible` for the non-outline non-shadow
   case, `lil_common_vert.hlsl:69-82`), collapsing the geometry. Default 0
   (`lts_cutout.shader:19`).
2. IDMask — compiled in (`ltspass_cutout.shader:682`) and gated only by
   platform support (`lil_common_vert.hlsl:362`). The resolved argument
   (`_IDMaskFrom`, default 8 = vertex id, `lts_cutout.shader:477`) is
   compared against `_IDMask1..8`; a masked vertex gets
   `positionCS = 0.0/0.0` (`lil_common_vert.hlsl:395-405`). When
   `_IDMaskControlsDissolve` is set, the ID comparison also drives the
   fragment dissolve gate (`lil_common_vert.hlsl:396-402`). All flags
   default 0 (`lts_cutout.shader:478-485`).
3. UDIM discard, vertex path —
   `if(_UDIMDiscardMode == 0 && _UDIMDiscardCompile == 1 &&
   LIL_CHECK_UDIMDISCARD(input))` collapses the vertex to NaN
   (`lil_common_vert.hlsl:413-423`). Both properties default 0
   (`lts_cutout.shader:508-510`).
4. AudioLink vertex displacement (`_UseAudioLink && _AudioLink2Vertex`,
   `lil_vert_audiolink.hlsl:9-58`) modifies position only — not alpha or
   rasterization coverage of the triangle's domain — and is identical on the
   opaque target, so it is outside the alpha proof `[SOURCE]`+`[INFERENCE]`.

### 3.3 Fragment alpha chain, in execution order

Shared by `FORWARD`, `FORWARD_ADD`, and `SHADOW_CASTER` (the shadow caster
runs the same chain through `Includes/lil_common_frag_alpha.hlsl`, included
at `lil_pass_shadowcaster.hlsl:67`).

1. **UDIM discard, pixel path** — `#if defined(LIL_FEATURE_UDIMDISCARD)`
   (defined) runs `OVERRIDE_UDIMDISCARD` = `if(_UDIMDiscardMode == 1 &&
   LIL_CHECK_UDIMDISCARD(fd)) discard;`
   (`lil_common_frag.hlsl:717-720`; forward call site
   `lil_pass_forward_normal.hlsl:154-157`; shadow path
   `lil_common_frag_alpha.hlsl:7-10`). Note the fragment path checks only
   `_UDIMDiscardMode == 1` at runtime; `_UDIMDiscardCompile` gates the
   vertex path (§3.2.3), not this one.
2. **Main UV** — `OVERRIDE_ANIMATE_MAIN_UV`
   (`lil_common_frag.hlsl:260-263`):
   `fd.uvMain = lilCalcDoubleSideUV(fd.uv0, fd.facing, _ShiftBackfaceUV)`
   (`lil_common_functions.hlsl:467-470`: backface gets `uv + (1,0)` when
   `_ShiftBackfaceUV == 1`; with `_ShiftBackfaceUV == 0` the condition
   `facing < -1.0` is never true for any `VFACE` value, so the mapping is
   facing-independent), then
   `lilCalcUV(fd.uvMain, _MainTex_ST, _MainTex_ScrollRotate)`
   (`lil_common_functions.hlsl:455-458`):
   `uv * st.xy + st.zw`, rotated by `sr.z + sr.w * LIL_TIME`, shifted by
   `frac(sr.xy * LIL_TIME)`; `LIL_TIME` is `_Time.y`
   (`lil_common_macro.hlsl:1931`). Scroll/rotate therefore make the sampled
   domain time-dependent unless the vector is `(0,0,0,0)`.
3. **Parallax** — `OVERRIDE_PARALLAX` (`lil_common_frag.hlsl:294-305`)
   invokes `lilPOM` (when `_UsePOM`) or `lilParallax`; both are internally
   gated on `useParallax` = `_UseParallax`
   (`lil_common_functions.hlsl:574-587`), so `_UseParallax == 0` neutralizes
   both regardless of `_UsePOM`. Active parallax offsets `fd.uvMain`
   per-pixel, changing which texels are sampled.
4. **Main color** — `OVERRIDE_MAIN` (`lil_common_frag.hlsl:353-357`):
   `LIL_GET_MAIN_TEX` samples `_MainTex` at `fd.uvMain`
   (`lil_common_frag.hlsl:311-312`; the POM-aware macro expands to a
   gradient sample whose footprint equals a normal sample,
   `lil_common_macro.hlsl:422-425`), then `LIL_APPLY_MAIN_TONECORRECTION`
   (`lil_common_frag.hlsl:336-343`) which assigns **only** `fd.col.rgb`
   (HSVG `:316-317`, gradation map `:323-325`, adjust-mask lerp `:343`),
   then `fd.col *= _Color` (`:357`). The only alpha operation on the
   unmodified chain is therefore `a₀ = tex2D(_MainTex, uvMain).a × _Color.a`.
5. **AudioLink fragment** — `lilAudioLinkFrag`
   (`lil_common_frag.hlsl:637-707`) writes only `fd.audioLinkValue`; it
   never writes `fd.col`. Not alpha-affecting `[SOURCE]`.
6. **Layer colors** — `lilGetMain2nd` / `lilGetMain3rd`
   (`lil_common_frag.hlsl:724-807, 820-903`) are gated by
   `_UseMain2ndTex` (`:740`) / `_UseMain3rdTex` (`:836`); their
   `fd.col.a` writes exist only for `_Main2nd/3rdTexAlphaMode` 1–4
   (`:801-804, :897-900`) and sit strictly inside those gates. The later
   layer RGB blending (`:463-468`) is RGB-only.
7. **Alpha mask** — `OVERRIDE_ALPHAMASK` (`lil_common_frag.hlsl:458-476`)
   is gated by `if(_AlphaMaskMode)`; modes 1–4 replace, multiply, add, or
   subtract a saturated mask sample into `fd.col.a`. Mode 0 is a no-op.
8. **Dissolve** — the block runs under
   `defined(LIL_FEATURE_DISSOLVE) && LIL_RENDER != 0`
   (`lil_pass_forward_normal.hlsl:368-383`) with the runtime gate
   `if(fd.dissolveActive)`. That flag is initialized `true`
   (`lil_common_vert.hlsl:55`; `lil_common.hlsl:163`) and is only ever set
   false by the IDMask path (`lil_common_vert.hlsl:396-402`) — **not** by
   `_DissolveParams`. The effective gate is inside
   `lilCalcDissolveWithNoise` / `lilCalcDissolve`
   (`lil_common_frag.hlsl:487-519`): after
   `dissolveParams.xy = round(dissolveParams.xy)` the entire body is
   `if(dissolveParams.r)` (`lil_common_functions.hlsl:684-686, 626-666`);
   mode 0 leaves `alpha` untouched. Active modes replace
   `alpha *= dissolveMaskVal` (after invert handling).
9. **Dither** — compiled for cutout
   (`defined(LIL_FEATURE_DITHER) && LIL_RENDER == 1`,
   `lil_pass_forward_normal.hlsl:387-390`). `OVERRIDE_DITHER`
   (`lil_common_frag.hlsl:524-549`; the forward pass takes the
   `LIL_FEATURE_DISTANCE_FADE` arm, `:533-539`) runs only when
   `_UseDither == 1` and then (a) replaces alpha via distance fade
   (`lilDistanceFadeAlphaOnly`, `:2013-2025`) and a binary
   `_DitherTex` screen threshold, and (b) — decisively — the standard
   cutoff transform itself is wrapped in `if(!_UseDither)`
   (`lil_pass_forward_normal.hlsl:399-401`). `_UseDither == 1` therefore
   replaces the entire cutout equation, not merely an add-on. Default 0
   (`lts_cutout.shader:36`).
10. **The cutout transform** —
    `fd.col.a = saturate((fd.col.a - _Cutoff) / max(fwidth(fd.col.a), 0.0001) + 0.5);`
    `if(fd.col.a == 0) discard;`
    (`lil_pass_forward_normal.hlsl:402-403`). This is the forward-pass
    cutout equation. It is **not** a plain clip: the kept/coverage boundary
    is analyzed in §3.4. The shadow caster instead ends
    `lil_common_frag_alpha.hlsl` with `clip(fd.col.a - _Cutoff);`
    (`lil_common_frag_alpha.hlsl:99`), which keeps fragments with
    `a ≥ _Cutoff` (equality kept).
11. **Compile-time-excluded transparent machinery** — the subpass dither /
    `_SubpassCutoff` block (`lil_common_frag_alpha.hlsl:100-115`),
    `LIL_PREMULTIPLY` (`lil_common_frag.hlsl:554-560`; empty for
    `LIL_RENDER != 2`), depth-fade alpha (`:2047-2049`, alpha write only in
    the `LIL_RENDER == 2` arm), and `_PreColor`/`_PreCutoff`
    (`lil_pass_forward_normal.hlsl:404-413`) are excluded for
    `LIL_RENDER 1`. `_AlphaBoostFA` is read only by the transparent FA
    premultiply (`lil_common_frag.hlsl:557`). The post-branch tail — rim
    shade, backlight, reflection, matcaps, rim light, glitter, emissions,
    dissolve-add emission, backface color, distance fade, deexposure, fog —
    writes only RGB or `fd.emissionColor` for `LIL_RENDER 1`
    (`lil_pass_forward_normal.hlsl:484-607`). No alpha write or discard
    exists after §3.3.10 `[SOURCE]`.

### 3.4 The cutout boundary

Let `c = _Cutoff` and `w = max(fwidth(a), 0.0001)` where `a` is the raw
chain alpha of §3.3.1–9 evaluated pre-transform (`fwidth` of a value that is
constant across the primitive is 0). The transform is
`T(a) = saturate((a − c)/w + 0.5)`:

| Condition (real arithmetic) | Rasterizer meaning |
|---|---|
| `a − c ≤ −0.5·w` | `T(a) = 0` → **discarded** (`== 0` compare, `:403`) |
| `−0.5·w < a − c < +0.5·w` | `0 < T(a) < 1` → kept; **partial** MSAA coverage under `AlphaToMask On` |
| `a − c ≥ +0.5·w` | `T(a) = 1` → kept, **full** coverage, identical to opaque under any `_AlphaToMask`/MSAA state |

`[SOURCE]` for the code, `[INFERENCE]` for the arithmetic reading. For a
constant raw alpha (the provable case), `w = 0.0001` exactly, so full
coverage holds iff `a ≥ c + 5×10⁻⁵`; the shadow caster keeps the fragment
whenever `a ≥ c`, which the full-coverage condition implies.
`[INFERENCE]` binary32 rounding of `(a − c)/0.0001` near the exact boundary
can move `T(a)` off 1.0 by an ulp; the theorem therefore uses a gate with
twice the exact margin (`_Cutoff ≤ 0.9999`, i.e. `1 − c ≥ 10⁻⁴`), which
makes `(a − c)/w ≥ 1` robust to rounding. `[DECISION NEEDED]` confirm the
0.9999 implementation bound (vs the exact 0.99995).

## 4. Complete alpha- and coverage-affecting inventory

Every branch below is classified against the pinned forward path. "Gate" is
the runtime material state that fully neutralizes the block; "observed" is
how AMUSE can know the state.

| # | Block / mechanism | Generator symbol / site | Runtime gate (OFF state) | Alpha/coverage effect when active | Off-state neutrality |
|---|---|---|---|---|---|
| 1 | Invisible | `_Invisible` (`vert:69-82`) | `== 0` | All geometry vanishes | No code executes; value unused elsewhere `[SOURCE]` |
| 2 | IDMask | `LIL_FEATURE_IDMASK` (`vert:362-409`) | `_IDMask1..8` all `== 0` | Vertex NaN collapse; can drive dissolve gate | `idMasked` false; `dissolveActive` untouched `[SOURCE]` |
| 3 | UDIM vertex collapse | `LIL_FEATURE_UDIMDISCARD` (`vert:413-423`) | `_UDIMDiscardCompile == 0` (with mode 0) | Whole-vertex NaN | Condition requires `compile == 1` `[SOURCE]` |
| 4 | UDIM pixel discard | `LIL_FEATURE_UDIMDISCARD` (`frag:717-720`) | `_UDIMDiscardMode == 0` | Per-pixel `discard` outside allowed tiles | Condition requires `mode == 1` `[SOURCE]` |
| 5 | Backface UV shift | `_ShiftBackfaceUV` (`functions:467-470`) | `== 0` | Backface samples `uv.x + 1` (different texels) | Comparison `facing < -1.0` unreachable for any `VFACE` `[SOURCE]`+`[INFERENCE]` |
| 6 | Main UV scroll/rotate | `LIL_FEATURE_ANIMATE_MAIN_UV` (`frag:260-263`, `functions:455-458`) | `_MainTex_ScrollRotate == (0,0,0,0)` | Time-dependent domain: rotate `sr.z + sr.w·t`, shift `frac(sr.xy·t)` | With all-zero vector both time terms vanish; domain = `uv0·ST + ST.zw` `[SOURCE]` |
| 7 | Parallax / POM | `LIL_FEATURE_PARALLAX`/`_POM` (`frag:294-305`, `functions:574-587`) | `_UseParallax == 0` | Per-pixel `uvMain` offset | Both functions return before touching `uvMain` `[SOURCE]` |
| 8 | Main tone correction / gradation / adjust mask | `LIL_FEATURE_MAIN_TONE_CORRECTION`, `_MAIN_GRADATION_MAP` (`frag:311-343`) | none needed | RGB only | Only `.rgb` assignments `[SOURCE]` |
| 9 | `_Color` multiply | (`frag:357`) | — | `a₀ = sample.a × _Color.a` | The theorem's own input `[SOURCE]` |
| 10 | AudioLink fragment | `LIL_FEATURE_AUDIOLINK` (`frag:637-707`) | none needed | none | Writes only `fd.audioLinkValue` `[SOURCE]` |
| 11 | Main 2nd layer | `LIL_FEATURE_MAIN2ND` (`frag:724-807`) | `_UseMain2ndTex == 0` | `fd.col.a` replaced/mul/add/sub by `_Main2ndTexAlphaMode` 1–4 (`:801-804`) | Function reduces to `color2nd = _Color2nd` (`:739`); `fd.col.a` untouched `[SOURCE]` |
| 12 | Main 3rd layer | `LIL_FEATURE_MAIN3RD` (`frag:820-903`) | `_UseMain3rdTex == 0` | same at `:897-900` | same (`:835-836`) `[SOURCE]` |
| 13 | Alpha mask | `LIL_FEATURE_ALPHAMASK` (`frag:458-476`) | `_AlphaMaskMode == 0` | replace/mul/add/sub with saturate | `if(_AlphaMaskMode)` false → no-op `[SOURCE]` |
| 14 | Dissolve | `LIL_FEATURE_DISSOLVE` + `LIL_FEATURE_DissolveNoiseMask` (`frag:487-519`, `functions:626-715`) | `_DissolveParams.x == 0` (shader rounds first) | `alpha *= maskVal` (+invert) | `round(x) == 0` → body skipped `[SOURCE]` |
| 15 | Dither | `LIL_FEATURE_DITHER`, `LIL_RENDER == 1` (`frag:524-549`; `forward:399-401`) | `_UseDither == 0` | Replaces distance-fade alpha AND disables the cutoff transform | `if(_UseDither)` false → no-op; `if(!_UseDither)` true → standard equation `[SOURCE]` |
| 16 | Cutout transform | `LIL_RENDER 1` (`forward:402-403`) | — | The equation under proof | core, cannot be stripped `[SOURCE]` |
| 17 | Subpass dither / `_SubpassCutoff` / premultiply / depth-fade alpha / `_Pre*` | `LIL_RENDER == 2` arms (`frag_alpha:100-115`, `frag:554-560`, `functions:2047-2049`) | compile-time excluded | — | Not compiled for cutout `[SOURCE]` |
| 18 | Distance fade (fragment) | `LIL_FEATURE_DISTANCE_FADE` (`functions:2028-2053`) | none needed | alpha only in `LIL_RENDER == 2` arm | Cutout takes the RGB-only arm `[SOURCE]` |
| 19 | Everything else RGB-lit | shadow/rim/emission/reflection/matcap/glitter/backlight/anisotropy/normals/VRCLV/fog/deexposure/backface color | none needed | none | No `fd.col.a` writes; audiolink value not consumed by alpha `[SOURCE]` |

`[INFERENCE]` This inventory is exhaustive for the pinned no-outline cutout
forward path: §3.3 walks the fragment function linearly, §3.2 the vertex
function's coverage sites, and §3.3.11 confirms the post-branch tail. The
outline-only sites (`LIL_OUTLINE` branches, `lts_cutout_o.shader` UsePasses)
are not referenced by the supported material shader.

## 5. Smallest positive theorem

**Theorem (restricted core).** Let `T` be a triangle of submesh `S` rendered
with material `M` under the attested regular no-outline lilToon 2.3.4 cutout
source. `T` is *proven opaque* iff all of the following hold; each clause is
checked against captured/admitted evidence, and unknown information fails
closed.

1. **Identity and source.** `M.shader` is the attested cutout asset — name
   `Hidden/lilToonCutout`, GUID `85d6126cae43b6847aff4b13f4adb8ec` — with
   pass `Hidden/ltspass_cutout` (GUID `ad219df2a46e841488aee6a013e84e36`),
   package `2.3.4`, format version 45, scanned `#define LIL_RENDER 1`, the
   canonical material/pass digests `c83d73a2…178` / `ecd1caed…e92`, and the
   37-file include-tree digest `6e2dce6c…fd46` (all values as measured by
   B1 §§5–6). The include tree and pass text satisfy AMUSE's existing
   canonicalization, and `ScanCompiledFeatures` output is recorded.
2. **Material scalars (captured, finite).**
   `_Color.a == 1`; `_Cutoff ≤ 0.9999` (§3.4 margin);
   `_Invisible == 0`; `_ShiftBackfaceUV == 0`; `_UseParallax == 0`;
   `_UseMain2ndTex == 0`; `_UseMain3rdTex == 0`; `_AlphaMaskMode == 0`;
   `_DissolveParams.x == 0`; `_UseDither == 0`;
   `_UDIMDiscardCompile == 0`; `_UDIMDiscardMode == 0`;
   `_IDMask1..8 == 0` (all eight). (`_UsePOM` need not be gated: it only
   selects which `_UseParallax`-gated function runs, §4 row 7.)
3. **UV domain (captured).** `_MainTex_ST == (1,1,0,0)` and
   `_MainTex_ScrollRotate == (0,0,0,0)`, so the sampled domain is exactly
   the triangle's `uv0` hull on channel 0 — the identity mapping that
   AMUSE's resolver admits today (`AlphaSemanticsResolver.cs:338-345`).
4. **Texture.** `_MainTex` is a `Texture2D` with: admitted format from the
   existing allowlist; full mip residency (no streaming/limit gaps);
   filter Point or Bilinear; `wrapModeU == wrapModeV` ∈ {Clamp, Repeat};
   and an alpha channel captured by the existing GPU-readback
   `AlphaFieldProvider` — measured decoded content of **every** mip level,
   never an import-setting inference.
5. **Per-triangle classification.** For every mip level, the exact
   classifier over the triangle's UV hull — including the bilinear/point
   footprint neighborhood and wrap normalization — finds every intersecting
   texel alpha `== 255`
   (`TriangleAlphaClassifier.Classify` → `ProvenOpaque`,
   `TriangleAlphaClassifier.cs:175-232, 285-335`).
6. **Animation closure.** Every property named in clauses 2–3 is part of the
   captured request; every animation binding reaching `M` on those
   properties is exact-singleton against the material's own serialized
   default; any unrecognized proof-relevant binding refuses the renderer
   batch (existing machinery, §6).

**Conclusion.** Under clauses 1–6 the raw chain alpha is `a ≡ 1` on every
fragment of `T` in `FORWARD`, `FORWARD_ADD`, and `SHADOW_CASTER`
(§3.3.4, gates neutralized per §4), hence `T(1) = saturate((1−c)/10⁻⁴ + 0.5) = 1`
with `c ≤ 0.9999`, the fragment is never discarded, MSAA coverage is full
under any `_AlphaToMask`, and the shadow caster's `clip(1 − c)` keeps. The
canonical opaque target renders the same geometry with full coverage and the
same RGB inputs, so per-triangle conversion is visually safe. **Verdict:
ProvenOpaque.**

**Worked positive example.** A fresh cutout material (B1 §9 defaults):
`_Color = (1,1,1,1)`, `_Cutoff = 0.5`, all gates at their defaults of 0,
`_MainTex` a fully-opaque 256×256 BC3 texture (`IsFullyOpaque` at every mip),
no animation. Every clause holds; `a ≡ 1`, `T(1) = saturate(5000.5) = 1`;
the triangle is proven opaque. `[SOURCE]` derivation.

**Boundary examples that refuse or stay unknown.**

- `_Cutoff = 0.99995`, `a ≡ 1`: mathematically full coverage with zero
  margin; implementation refuses (`> 0.9999`) — false negative by design
  margin.
- `_Cutoff = 1.0`, `a ≡ 1`: `T = saturate(0/10⁻⁴ + 0.5) = 0.5` → partial
  coverage under the cutout default `_AlphaToMask = 1` → **not proven**
  (conversion would change silhouette edge appearance).
- `_Cutoff = 1.001`, `a ≡ 1`: `T = saturate(−10 + 0.5) = 0` → the triangle
  is **fully discarded** in the source; converting it would make invisible
  geometry visible → refuse (never "opaque").
- Constant texture alpha `a ≡ 0.5`, `_Cutoff = 0.5`: `T = 0.5` → partial →
  not proven (the "cutoff equality" case).
- One texel with alpha `≤ 254` intersecting the hull's footprint in **any**
  mip → `MustRemainTransparent` per level-merge
  (`AlphaSemanticsResolver.cs:184-197`).
- `_Color.a = 0.8` (texture fully opaque): existing multiplier lemma →
  uniform `MustRemainTransparent`, soundly refusing a materially-lower
  alpha (`AlphaSemanticsResolver.cs:272-298`). The margin-based extension
  (constant `c ≥ cutoff + 5×10⁻⁵` ⇒ opaque) is deferred (§11 row 5).
- Trilinear or anisotropic filter, differing wrapU/wrapV, mirrored wrap →
  `UnsupportedSampling` refusal (`AlphaSemanticsResolver.cs:353-387`).
- Non-identity `_MainTex_ST` → `UnsupportedUvMapping` refusal today
  (`AlphaSemanticsResolver.cs:338-345`).

## 6. Evidence requirements and capture timing

The design reuses AMUSE's single closed capture at the platform-finish
barrier, the exact-singleton admission, and pass-3 revalidation unchanged
(controlling investigation §3; `AlphaSeparationPreparation.cs`,
`AlphaSeparationApply.cs`). No new capture mechanism is introduced.

**Extended lilToon alpha evidence request (cutout).** A cutout-family
request shaped exactly like `LilToonMaterialSemantics.AlphaEvidenceRequest`
today (`LilToonMaterialSemantics.cs:482-496`) but widened to the theorem's
inputs:

- scalars: `_lilToonVersion` (format pin), `_Invisible`, `_UDIMDiscardCompile`,
  **`_UDIMDiscardMode`** (the current request lacks it — required because the
  pixel path is runtime-gated independently of the compile flag, §4 row 4),
  `_ShiftBackfaceUV`, `_UseParallax`, `_UseMain2ndTex`, `_UseMain3rdTex`,
  `_AlphaMaskMode`, `_UseDither`, `_IDMask1..8`;
- colors: `_Color` (so per-component bindings, including `.a`, are
  recognized and singleton-admitted);
- vectors: `_DissolveParams` (`.x` gate), `_MainTex_ST` is **not** requested
  as a vector — it rides the texture request's `ScaleOffset` kind, which
  also derives the animatable `_MainTex_ST` binding name
  (`UnityAnimationEvidenceCapture.cs:480-495`);
  `_MainTex_ScrollRotate`;
- textures: `_MainTex` with
  `ScaleOffset | SourceIdentity | Sampling | AlphaChannel`
  (the same kind set Poiyomi's alpha request uses for `_MainTex`,
  `PoiyomiMaterialSemantics.cs:1511-1515`).

Why each fact and when it is captured:

| Fact | Timing | Why | Animation can reach it? |
|---|---|---|---|
| Shader name/GUID, pass identity, digests, `LIL_RENDER`, compiled features | barrier, from the then-installed package (`GatherSourceEvidence` pattern, `LilToonSourceAttestation.cs:1238-1328`) | establishes which equation compiled; mode carrier is the shader asset | No — not a material property (controlling §7) |
| All clause-2 scalars, `_Color`, `_DissolveParams`, `_MainTex_ScrollRotate` | closed pre-barrier capture | they are `material.<Property>` floats/colors/vectors; the theorem reads their values | **Yes** — all are ordinary animatable properties; singleton admission refuses non-singleton clips |
| `_MainTex_ST` | closed capture via `ScaleOffset` | the domain transform; the identity gate reads it | **Yes** — derived `_MainTex_ST` bindings are first-class (`AdmittedMaterialStates.cs:418-438`) |
| `_MainTex` alpha mips, sampling, source identity | barrier, GPU readback via `AlphaFieldProvider` | measured runtime content incl. compression decode; cannot be inferred from readability | No — texture content is not animatable (texture *swaps* are a separate, existing closed-capture concern) |
| Live queue + `RenderType` | during preparation, non-animatable | conversion-side render state, exactly `ReadEffectiveRenderState`'s justification (controlling §7) | No |
| Overwrite rule | preparation | recipe properties with admitted bindings must already equal canonical values | covered by existing mapping-based rule |
| Pass-3 validation | apply | unchanged, family-agnostic | — |

The ordinary (opaque) lilToon alpha request is **not** widened: the cutout
request is a separate object selected per family, preserving the
Poiyomi-style separation between alpha relevance and conversion evidence
(`PoiyomiOpaqueConversion.cs:204-219`).

## 7. Texture / UV / runtime-sampling constraints

- **Binary texel opacity vs the continuous cutout transform.** AMUSE's
  classifier proves "every texel in the domain is exactly 1.0". For the
  cutout equation this is *stronger than needed but exactly sound*: with
  `a ≡ 1` the transform saturates to exactly 1 regardless of
  `_AlphaToMask`, MSAA level, or the `fwidth` term (constant ⇒ `w = 10⁻⁴`).
  The proof never relies on texture readability alone — it relies on the
  measured readback grids.
- **Mips.** The resolver classifies the triangle against *every* level of a
  shape-complete chain and refuses incomplete chains at the provider
  contract (`AlphaMipChain.cs:10-23`,
  `AlphaSemanticsResolver.cs:34-39, 178-197`). Gradient sampling with
  explicit derivatives (`lil_common_macro.hlsl:422-425`) selects mips by the
  same footprint magnitudes as implicit sampling, so "all levels proven"
  covers any LOD the hardware may pick. Trilinear blending of two
  neighboring proven levels remains proven; the sampling vocabulary only
  admits Point/Bilinear anyway.
- **Filtering/wrap.** The four Point/Bilinear × Clamp/Repeat combinations
  are the only admitted configurations, each with exact domain arithmetic
  including the ±half-texel bilinear reach and repeat wrap normalization
  (`TriangleAlphaClassifier.cs:285-349`).
- **Compression and color space.** The capture path blits through a
  predicate shader into `R8_UNorm` and reads back the *decoded* values, so
  BC/ASTC block decode is measured, not modeled; the allowlist admits only
  formats with an authoritative decode rule (`UnityAlphaFieldEvidence.cs:473-477`
  documents BC3 alpha exactness).
- **UV transform.** Today only the identity mapping on UV0 is admitted
  (`AlphaSemanticsResolver.cs:327-345`); the theorem adopts that boundary as
  a gate. Extending to general affine `_MainTex_ST` requires proving in
  exact dyadic/rational arithmetic that the transformed hull is representable
  by the supplied binary32 UVs — an obligation the code already states and
  the exact-UV machinery (`ExactUvGeometry`) is built for.
- **Known domain.** Mesh-side unknowns (degenerate triangles, missing/NaN
  UVs, non-finite positions) classify as `Unknown`, never opaque
  (`TriangleAlphaClassifier.cs:189-200`).

## 8. Animation and overwrite implications

- Every proof-relevant property is in the cutout request, so any binding on
  it is *recognized*; the renderer-wide `UnrecognizedMaterialBinding`
  refusal still catches bindings on names outside the request
  (`UnityAnimationEvidenceCapture.cs:499-550`).
- Non-singleton clips on `_Color.a`, `_Cutoff`, any gate scalar,
  `_DissolveParams`, `_MainTex_ST`, or `_MainTex_ScrollRotate` refuse
  admission (`AdmittedMaterialStates.cs:279-376`), which refuses the slot's
  proof — the conservative direction. An animated `_Cutoff` can sweep across
  the discard boundary; an animated `_MainTex_ScrollRotate` moves the
  sampled domain continuously; neither is provable from static evidence.
- The overwrite rule (controlling §8) transfers unchanged: the conversion
  recipe writes only canonical render-state/recipe values, all ordinary
  animatable floats, validated by the existing mapping-based machinery. The
  proof-relevant set (`_Cutoff`, `_Color`, gates) is *not* written by the
  conversion, so it never enters the overwrite rule — it only constrains
  eligibility.
- Texture *swaps* by animation remain covered by the existing closed
  material-dependency capture (every reachable material is admitted or the
  renderer refuses); nothing lilToon-specific is added.

## 9. Callback-independence verdict

**Direct answer: NDMF-complete restricted core is feasible.** The restricted
theorem is invariant across callback-100 shader regeneration; the
previously recorded upload-time "Outcome B" is **not** a prerequisite.
Source-backed argument, per alpha/coverage-affecting compile-time feature:

- **Mechanism.** At avatar build, `VRChatModule` (callback order 100,
  `External/Editor/VRChatModule.cs:20-22`) calls
  `lilToonSetting.SetShaderSettingBeforeBuild(materials, clips)`
  (`VRChatModule.cs:47-70`), which expands material parents, collects the
  in-use shaders, derives the used-feature set from the materials **and
  their animation clips** (`lilToonSetting.cs:897-948`), re-unpacks each
  in-use shader from its `.lilinternal` container with only the used
  `LIL_FEATURE_*` defines (`lilToonSetting.cs:513-548`), and additionally
  rewrites `lil_common_input_opt.hlsl` down to the uniforms the avatar's
  materials and clips use (`lilOptimizer.cs:18-35, 191-198`).
  `SetShaderSettingAfterBuild` restores the all-on shader files and calls
  `lilOptimizer.ResetInputHLSL()` (`lilToonSetting.cs:984-1013`,
  `lilOptimizer.cs:313-318`), so capture-time assets are the stable
  committed ones. Avatar builds always set `forceOptimize = true`
  (`VRChatModule.cs:28-31`), so regeneration runs in the supported
  environment; the UsePass-bug workaround skips optimization only below
  Unity 2022.3.14 (`lilToonSetting.cs:877-895`) — 2022.3.22f1 is not
  skipped. `SetupMultiMaterial` touches only Multi materials
  (controlling §5) and is a no-op for this family.
- **What regeneration cannot change.** `LIL_RENDER` is fixed per pass asset
  (`ltspass_cutout.shader:639`) and never rewritten; the core equation —
  main sample, `_Color` multiply, cutout transform, shadow clip — is
  unconditional code with no `LIL_FEATURE` gate. The core uniforms
  (`_MainTex`, `_Color`, `_Cutoff`) are read by every cutout material and
  therefore always retained by the input-uniform usage scan.
- **Per-feature invariance.** For each optional alpha/coverage feature the
  stripped state differs from the committed state only by *not compiling a
  block that, at the theorem's gate-OFF state, executes nothing or writes
  nothing* (§4, last column, each verified at source level). Stripping a
  runtime-neutral block preserves the fragment result; conversely, if any
  *other* material on the avatar keeps a feature compiled in, this
  material's gate-OFF state still neutralizes it — the invariance does not
  depend on the avatar's other materials. The compile-time feature symbol
  is observed through the existing `ScanCompiledFeatures`
  (`LilToonSourceAttestation.cs:830-853`), and the theorem refuses any
  material whose gate is not provably OFF, so no case exists where an
  active feature's stripped/enabled ambiguity reaches the proof.
- **`LIL_INPUT_OPTIMIZED`** changes only VRCLightVolumes code paths
  (`lil_pass_forward_normal.hlsl:121-122`, `openlit_core.hlsl:17-21`) —
  not alpha.
- **Residual honesty.** The uploaded per-avatar shader artifact is not a
  stable attestable asset (it exists only inside the SDK build). The design
  therefore attests the committed source plus the semantic invariance
  argument above; it does **not** digest the uploaded artifact. Empirical
  confirmation of the regeneration behavior remains available through a
  future authorized Census validation and is not required for the design
  verdict. `[INFERENCE]` from the cited generator sources.

## 10. Explicit unknown / refusal table

| Condition | Verdict |
|---|---|
| Shader/pass/package/format/digest mismatch | Unsupported (attestation fails; renderer batch refuses) |
| `LIL_RENDER` scanned ≠ 1 | Unsupported (not the cutout identity) |
| Any clause-2 gate nonzero | Unsupported feature refusal (never partially proven) |
| `_Cutoff > 0.9999` (a `NaN` cutoff fails the `≤` comparison and is refused) | Eligibility refusal: no provable triangle (partial or fully discarded) |
| `_DissolveParams.x` nonzero after rounding semantics | Refusal (implement exact `== 0`, stricter than the shader's `round`) |
| Non-singleton animation on any requested property | Slot proof refused (admission failure) |
| `_Color.a ∈ (0,1)` | Uniform `MustRemainTransparent` (existing multiplier lemma) |
| `_Color.a > 1` or non-finite | `UnsupportedMultiplier` refusal |
| Non-identity `_MainTex_ST`, UV channel ≠ 0 | `UnsupportedUvMapping` refusal today |
| Trilinear/aniso filter, mismatched or unsupported wrap | `UnsupportedSampling` refusal |
| Format not in allowlist, non-Texture2D, mip streaming/limit gaps, readback failure | Missing texture evidence → refusal (never inferred from readability) |
| Transparent texel intersecting hull footprint in any mip | `MustRemainTransparent` |
| Degenerate triangle, missing/NaN UV | `Unknown` (acceptable false negative) |
| Exact-UV region complexity overflow (> 65536 cells) | `Unknown` (`MaxSupportRegions`) |
| UDIM discard in either path active | Refusal (neither path provable without tile/UV-domain analysis — deferred) |
| IDMask any flag nonzero | Refusal (vertex collapse not provable without mesh+ID analysis — deferred) |
| Dither `_UseDither == 1` | Refusal (replaces the cutoff equation entirely) |

## 11. Current AMUSE capability-gap classification

| # | Discovered gap | Class |
|---|---|---|
| 1 | `IdentifyFamily` matches only `"lilToon"`; cutout materials are `Unsupported` today and refuse the whole renderer (`UnityMaterialSemantics.cs:242-263`) | **B2 design requirement** — second pinned identity in `LilToonSourceAttestation` (digests already measured, B1 §6) + family branch |
| 2 | No cutout alpha interpretation; `InterpretAlpha` is the `LIL_RENDER 0` constant-1 premise (`LilToonMaterialSemantics.cs:472-533`) | **B2 design requirement** — cutout semantics returning `TextureSampleTimesConstant(_MainTex.a, Alpha, _Color.a)` behind the §5 gate set |
| 3 | Cutout `AlphaEvidenceRequest` extension (§6) — adds `_UDIMDiscardMode`, `_Color`, gates, `_MainTex` texture kinds | **B2 design requirement** |
| 4 | lilToon conversion core (eligibility incl. `_Cutoff ≤ 0.9999` gate mirroring Poiyomi's `ClipThresholdDiscardsOpaqueAlpha` at `PoiyomiOpaqueConversion.cs:346-356`, recipe application, re-read validation) | **Downstream R1–R6 conversion pressure** — explicitly out of B2 scope |
| 5 | Affine `_MainTex_ST` support in `AlphaSemanticsResolver.IsSupportedMapping` (`AlphaSemanticsResolver.cs:327-345`) | **Downstream R-pressure** (smallest theorem ships identity-ST; exact-arithmetic obligation already documented in code) |
| 6 | Cutoff-margin extension for constant raw alpha `c ∈ (0,1)` (would move `_Color.a ∈ (0,1)` materials from uniform-refused to provable when `c ≥ cutoff + 5×10⁻⁵`) | **Deferred unsupported feature** — soundness never requires it |
| 7 | Wider sampling vocabularies (trilinear/aniso/mirror), UDIM tile-aware proof, IDMask-aware proof | **Deferred unsupported feature** |
| 8 | Opaque lilToon request lacks `_UDIMDiscardMode` (pixel discard); value-side alpha claim is unaffected (surviving fragments still have alpha 1) — recorded observation, no action in B2 | **Speculative future pressure** (do not widen opaque capture now) |
| 9 | Mesh separation planning, appended-slot application, pass-3 revalidation, exact-singleton admission, capture/Combine machinery, GPU readback evidence | **No gap** — reused as-is (family-agnostic per controlling §3.6) |

## 12. Future falsifiers

Public synthetic tests (future implementation work; none written here) that
must fail plausible unsound implementations:

1. **Constant opaque core.** Fully-opaque `_MainTex` (all mips), `_Color.a = 1`,
   `_Cutoff = 0.5`, gates off → every triangle `ProvenOpaque`. Fails an
   implementation that ignores `_Color.a` or re-derives the boundary from
   `a > cutoff`.
2. **Cutoff boundary.** `_Cutoff = 0.9999` proven; `_Cutoff = 1.0` not proven;
   `_Cutoff = 1.001` refused (conversion would materialize invisible
   geometry). Fails implementations using Poiyomi's `cutoff ≤ 1` gate
   verbatim or plain `clip` semantics for the forward pass.
3. **Single transparent texel.** One texel `≤ 254` inside the hull footprint
   (point and bilinear; clamp and repeat) → `MustRemainTransparent`. Place
   the transparent texel so it exists **only in a high mip** (mip 0 fully
   opaque): fails implementations that check only mip 0.
4. **Bilinear footprint.** Transparent texel one half-texel outside the hull
   → `MustRemainTransparent` under bilinear; the same placement under point
   filtering outside the hull → `ProvenOpaque`. Fails hull-only
   implementations without footprint dilation.
5. **Wrap seam.** Hull crossing the repeat seam with a transparent texel at
   the wrapped coordinates → `MustRemainTransparent`; clamp with the same
   layout → outcome flips consistently with reachability.
6. **Animated proof-relevant properties.** Non-singleton clips on
   `_Color.a`, `_Cutoff`, `_AlphaMaskMode`, `_DissolveParams`,
   `_MainTex_ScrollRotate`, `_MainTex_ST` each refuse the slot. Fails
   implementations that read live values instead of captured evidence.
7. **Every optional path refuses.** Alpha mask mode 1–4, `_UseDither = 1`
   (including a variant where texture alpha is constant 1 — dither still
   replaces the equation), dissolve mode 1, 2nd/3rd layers with alpha modes,
   parallax, backface shift, UDIM (compile or mode), IDMask flag — each
   refuses. Fails implementations that gate only on compiled features
   (`ScanCompiledFeatures`) rather than runtime material state.
8. **Compilation-variant invariance.** The same gate-off material must
   receive the identical verdict whether the pass source defines all
   features (committed) or only a superset of unrelated ones (simulating a
   regeneration kept wide by another material). Fails implementations whose
   verdict depends on the feature-define set rather than the runtime gates.
9. **Unknown stays unknown.** Unsupported texture format, streaming mips,
   non-identity ST, trilinear filter → refusal, never `ProvenOpaque`.
   Fails implementations that default missing evidence to opaque.
10. **Ordinary opaque analysis unchanged.** Existing `LIL_RENDER 0` lilToon
    semantics and its capture request are byte-for-byte unaffected (opaque
    fixtures keep passing; the opaque request is not widened). Fails
    implementations that mutate the shared request.
11. **Opaque-lilToon path remains AlreadyOpaque-shaped.** An attested opaque
    lilToon material still resolves constant-1 and never enters the cutout
    classification. Fails family mixups.

## 13. What the design proves and does not prove

**Proves.** For the pinned regular no-outline lilToon 2.3.4 cutout source:
the exact alpha/coverage equation in all three visual passes; a complete,
source-cited inventory of every alpha- and coverage-affecting mechanism with
its neutralizing runtime gate; a restricted per-triangle positive theorem
that is invariant under callback-100 regeneration; the evidence schema and
capture timing using only existing AMUSE machinery; and falsifiable tests
for every plausible unsound implementation.

**Does not prove.** Anything about transparent, outline, Lite,
Tessellation, Multi, Gem, Fur, Refraction, overlay, or fakeshadow families;
constant-alpha below 1 (multiplier margin) opacity; non-identity UV
transforms; UDIM/IDMask tile-level proofs; dither-active materials; the
R1–R6 conversion refactor or the conversion implementation itself; source
attestation implementation (digests are already measured); any upload-time
validation of the regenerated shader artifact (argued invariant, not
digest-attested); and no lifecycle verdict beyond what the controlling
investigation already records.

## 14. Unresolved controller decisions

1. `[DECISION NEEDED]` Confirm the implementation cutoff gate at
   `≤ 0.9999` (twice the exact margin `0.99995`, absorbing binary32
   rounding). The exact-math number is the alternative.
2. `[DECISION NEEDED]` Accept the B2 citation basis: tag-tree sources proven
   byte-identical (modulo CRLF) to the B1-measured release archive (§2).
   A one-off byte-comparison of the archive against AMUSE's canonical
   digests can be added to the attestation implementation if desired.
3. `[DECISION NEEDED]` Sequence for the identity-ST restriction: ship the
   first cutout conversion with `_MainTex_ST == identity` (recommended) or
   schedule the exact-arithmetic affine-ST resolver extension (gap 5)
   before it.
4. `[DECISION NEEDED]` Future pressure, not B2: whether fully-discarded
   domains (texture alpha entirely below the cutoff) may eventually be
   *dropped* rather than kept on the cutout material — a mesh-level
   decision that must never reuse the `ProvenOpaque` verdict.

## 15. Verdict on B2

**B2 is discharged for the restricted candidate.** The cutout alpha and
coverage path is fully derived from pinned source with every branch
classified (§§3–4); the smallest positive theorem is stated with exact
preconditions, worked positive and boundary examples, and an explicit
unknown/refusal lattice (§§5, 10); evidence requirements reuse existing
capture/attestation/texture/animation machinery with no new framework
(§6); the equation is source-backed, the inventory complete, and the
callback-independence claim verified against the pinned generator with a
per-feature invariance argument — **NDMF-complete restricted core is
feasible; Outcome B is not a prerequisite** (§9). Nothing in this note
required production code, a Unity scratch project, or Census Lab.

## 16. Next recommended task

Authorize the R1–R6 conversion refactor sized for the second family
(controlling investigation §13), with the following B2-derived contents in
dependency order:

1. Extend `LilToonSourceAttestation` with the pinned cutout identity
   (name/GUID/pass/version/format/render-mode 1/digests from B1 §6) and
   cutout family selection (gap 1).
2. Implement the cutout alpha interpretation + extended request (gaps 2–3)
   behind the §5 theorem, with the falsifiers of §12 as the RED/GREEN
   public synthetic suite.
3. Implement the lilToon conversion core mirroring
   `PoiyomiOpaqueConversion` (gap 4), including the `_Cutoff ≤ 0.9999`
   eligibility gate and the lilToon-shaped re-read validation (§6 of the
   controlling investigation).
