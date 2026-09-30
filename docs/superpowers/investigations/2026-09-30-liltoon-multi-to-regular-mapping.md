# Investigation: how lilToonMulti maps onto the regular lilToon family

Date: 2026-09-30. Status: characterization complete. No production code changed.
Work branch: `research/liltoon-multi`, cut from `main` at `8b89533`.

Companion note: `2026-09-30-liltoon-multi-outline-support-characterization.md`
(corpus counts, Lab console state, AMUSE-side handling).

Privacy note: this record names only public vendor facts, repository facts,
and aggregate corpus counts. It names no private avatar, renderer, material,
path, instance, port, or hash.

## 1. Question

Two mapping directions, from the controller:

1. How does lilToonMulti map onto normal lilToon? Which regular shader does a
   Multi material behave like, and where does the equivalence break?
2. How do lilToonMulti and its variants (base, outline, refraction, fur, gem)
   map onto the lilToon variants AMUSE supports today?

Labels: `[SOURCE]` marks a pinned upstream or repo fact with a file and line.
`[MEASURED]` marks the 2026-09-30 corpus observation. `[INFERENCE]` marks a
bounded conclusion. `[OPEN]` marks an unresolved question for future work.

## 2. Method and pins

Upstream pin: lilToon tag `2.3.4`, commit
`252fd8cfc46106d4967e95b3f2c788418502f227`. All upstream cites are relative to
`Assets/lilToon/` at that commit. Repo cites are repo-relative at `8b89533`.
The corpus observation is the 2026-09-30 scan recorded in the companion note
(2292 materials; 127 on `_lil/lilToonMulti`; 9 on
`Hidden/lilToonMultiOutline`; all mode 0, all queue 2000).

## 3. The five Multi containers `[SOURCE]`

| Asset | Shader name | GUID | Declared queue | Extra defines | Passes beyond the base four |
|---|---|---|---|---|---|
| `ltsmulti.shader` | `_lil/lilToonMulti` | `9294844b15dca184d914a632279b24e1` | Geometry (2000) | `LIL_MULTI` + 24 `LIL_MULTI_INPUTS_*` (`:648-672`) | none |
| `ltsmulti_o.shader` | `Hidden/lilToonMultiOutline` | `51b2dee0ab07bd84d8147601ff89e511` | Transparent-100 (2900) | + `LIL_MULTI_INPUTS_OUTLINE` (`:673`) | `FORWARD_OUTLINE`, `FORWARD_ADD_OUTLINE` (`LIL_OUTLINE` at `:826, :981`); the one `SHADOW_CASTER` compiles with `LIL_OUTLINE` (`:1042`) |
| `ltsmulti_ref.shader` | `Hidden/lilToonMultiRefraction` | `d7af54cdd86902d41b8c240e06b93009` | Transparent-100 (2900) | `LIL_REFRACTION` + `LIL_MULTI` (`:625-626`), 23 inputs, `GrabPass "_lilBackgroundTexture"` (`:657-658`) | none |
| `ltsmulti_fur.shader` | `Hidden/lilToonMultiFur` | `1e50f1bc4d1b0e34cbf16b82589f6407` | Transparent (3000) | `LIL_FUR` + `LIL_MULTI` (`:668-669`), `#pragma require geometry` (`:666`) | `FORWARD_FUR`, `FORWARD_ADD_FUR` with `#pragma geometry` (`:820, :967`) compiling `Includes/lil_pass_forward_fur.hlsl` (`:843, :990`) |
| `ltsmulti_gem.shader` | `Hidden/lilToonMultiGem` | `69f861c14129e724096c0955f8079012` | Transparent-100 (2900) | `LIL_GEM` + `LIL_MULTI` (`:632-633`), reduced 13-input set (`:634-646`), `GrabPass` (`:655-656`) | `FORWARD_PRE` with `LIL_GEM_PRE` compiling `Includes/lil_pass_forward_gem.hlsl` (`:687-692`); additive defaults `_DstBlend=1`, `_ZWrite=0`, `_Cull=0` (`:578-592`) |

Every container carries the same `For Multi` block: `_UseOutline`,
`_TransparentMode` (0 Opaque, 1 Cutout, 2 Transparent, 3 Refraction, 4 Fur,
5 FurCutout, 6 Gem), `_UseClippingCanceller`, `_AsOverlay`
(`ltsmulti.shader:559-563`).

## 4. How mode works in each family `[SOURCE]`

Regular family: the mode is compile-time per pass asset. `ltspass_opaque.shader`
declares `#define LIL_RENDER 0` (`:639`), `ltspass_cutout.shader` declares
`LIL_RENDER 1` (`:639`), `ltspass_transparent.shader` declares `LIL_RENDER 2`
(`:672`). A material picks a mode by holding a different shader asset. The
assets declare zero `shader_feature_local` mode keywords.

Multi family: the mode is keyword-derived at include time.
`Includes/lil_replace_keywords.hlsl:57-63` derives `LIL_RENDER` from material
keywords: `UNITY_UI_CLIP_RECT` gives 2, `UNITY_UI_ALPHACLIP` gives 1, neither
gives 0. `LIL_REFRACTION`, `LIL_FUR`, and `LIL_GEM` are compile-time container
defines, never keywords; no editor code enables them
(`lilMaterialUtils.cs` keyword writes are only the mode and feature keywords,
`:397-399, :455-468`). The editor derives the two mode keywords from
`_TransparentMode` at edit time (`:397-398`) and never re-derives them at
build.

Three container edges follow `[SOURCE]`:

1. Modes 3-6 never stay on the base or outline containers. The vendor mode map
   migrates them onto the dedicated assets: Refraction to `ltsmref` with
   RenderType empty and queue -1 (`lilMaterialUtils.cs:184-186`), Fur to
   `ltsmfur` at 3000 (`:205-207`), FurCutout to `ltsmfur` at 2450
   (`:224-226`), Gem to `ltsmgem` at -1 (`:253-255`). `RefractionBlur` and
   `FurTwoPass` have no Multi branch at all; the vendor always assigns the
   regular `ltsrefb` and `ltsfurtwo` assets (`:196-201, :240-249`).
2. The fur container's effective render mode is 2, not 1: mode 4 turns on
   `UNITY_UI_CLIP_RECT`, and the clip-rect branch of the derivation wins over
   the fur branch by `#if` order (`:57-60`). The fur layer passes are a
   separate question; the base layer renders as transparent-mode alpha.
3. Feature keywords ride the same derivation: `_COLOROVERLAY_ON` becomes the
   alpha-mask feature for modes 1, 2, 4, and 5 when a mask is enabled
   (`:455`), is forced off for gem (`:442`), and `ETC1_EXTERNAL_ALPHA` becomes
   the dither feature only for mode 1 with `_UseDither` (`:399`).

## 5. Code sharing between the families `[SOURCE]`

The terminal include chain is textually identical in both families: every
forward pass ends at `Includes/lil_pipeline_brp.hlsl` then
`Includes/lil_common.hlsl` then `Includes/lil_pass_forward.hlsl`
(`ltsmulti.shader:759-763`; `ltspass_opaque.shader:797-801`). The alpha blocks
that decide coverage live in that shared chain
(`Includes/lil_pass_forward_normal.hlsl:226-237` outline branch, `:394-413`
normal branch). Same chain plus same effective `LIL_RENDER` means the same
alpha branch compiles for both families.

Three deltas sit in front of that shared chain:

1. Every Multi pass prepends `Includes/lil_replace_keywords.hlsl`
   (`ltsmulti.shader:755`). No regular pass asset includes it anywhere.
2. The define sets differ: regular assets carry `LIL_RENDER` plus about one
   hundred `LIL_FEATURE_*` defines (`ltspass_opaque.shader:645-743`); Multi
   assets carry `LIL_MULTI` plus `LIL_MULTI_INPUTS_*` and hard-define their
   feature set.
3. The input buffer takes a different branch. `lil_common_input.hlsl` has a
   dedicated `LIL_MULTI` branch (`:215-815`) where uniforms are pruned per
   `LIL_MULTI_INPUTS_*` define, while the regular path defers to
   `lil_common_input_base.hlsl` (`:816-818`). Texture and sampler
   declarations are outside the branch and shared (`:832-897`). Under
   `LIL_MULTI`, `lil_common.hlsl` bakes about nineteen `_Use*` feature
   toggles to literal `true` (`:27-46`), so those toggles are not material
   state in the Multi family.

Two deltas inside the shared surface matter for alpha proofs:

- `_Cutoff` and `_SubpassCutoff` are unconditional in both families
  (`lil_common_input.hlsl:383-384`; base `:274, :278`). The alpha-mask
  uniforms move from a `LIL_FEATURE_ALPHAMASK` guard to a
  `LIL_MULTI_INPUTS_ALPHAMASK` guard, and the Multi assets hard-define that
  input, so the guard is equivalent (`:414-418, :676-678`).
- The regular transparent asset's `_PreCutoff` uniform and its `FORWARD_BACK`
  pre-pass (`LIL_TRANSPARENT_PRE`, `ltspass_transparent.shader:826-835`) have
  no Multi counterpart anywhere in the `LIL_MULTI` branch.

`[OPEN]` One citation conflict needs settling before any Multi cutout proof:
F0/B2 quote the regular cutout equation as `clip(fd.col.a - _Cutoff)` at
`lil_pass_forward_normal.hlsl:236`, while the pinned read of the outline
branch at `:226-237` quotes a screen-derivative rescale plus discard for
`LIL_RENDER 1`. Both families compile the same file, so the question is which
block the non-outline forward pass executes at `LIL_RENDER 1`. The mapping in
this note does not depend on the answer; a future Multi mode-1 proof must
resolve it against the source.

## 6. The mapping table

Effective mode for each container and mode value, its nearest regular
identity, and that identity's AMUSE status:

| Multi container, mode | Effective `LIL_RENDER` | Nearest regular identity | AMUSE today | Deltas that matter |
|---|---|---|---|---|
| `ltsmulti`, mode 0 | 0 | `lilToon` | supported (opaque family) | mode is keyword state; `_Use*` toggles baked true; vendor writes RenderType empty, queue -1 |
| `ltsmulti_o`, mode 0 | 0 | `Hidden/lilToonOutline` | supported (S8) | outline lives in the container; the single `SHADOW_CASTER` carries `LIL_OUTLINE` instead of a separate `SHADOW_CASTER_OUTLINE` pass; declared queue 2900 |
| `ltsmulti`, mode 1 | 1 | `Hidden/lilToonCutout` | supported (cutout family) | keyword-carried mode; `[OPEN]` alpha-block form above |
| `ltsmulti_o`, mode 1 | 1 | `Hidden/lilToonCutoutOutline` | supported (S8) | same container deltas as mode 0 |
| `ltsmulti`, mode 2 | 2 | `Hidden/lilToonTransparent` | supported (transparent family) | no `FORWARD_BACK` pre-pass and no `_PreCutoff`, so the two-pass transparent identity has no Multi counterpart; `FORWARD_ADD` always present, so the one-pass identity has no Multi counterpart either |
| `ltsmulti_o`, mode 2 | 2 | `Hidden/lilToonTransparentOutline` | supported (S9) | same two structural gaps |
| `ltsmulti_ref` | 2 via `LIL_REFRACTION` | `Hidden/lilToonRefraction` | refused (F0 R) | same GrabPass texture name, queue 2900 default; a representation refusal already covers it |
| `ltsmulti_fur`, mode 4 | 2 base layer, fur layer passes | `Hidden/lilToonFur` | refused (F0 R) | geometry shells compile from the same fur include family; a generated-geometry refusal already covers it |
| `ltsmulti_fur`, mode 5 | 1 | FurCutout | refused (F0 R) | same asset, 2450 queue |
| `ltsmulti_gem` | `LIL_GEM` | `Hidden/lilToonGem` | refused (F0 R) | additive `One/One`, `ZWrite 0`, `Cull 0`, GrabPass, `FORWARD_PRE`; the additive refusal already covers it |
| none | RefractionBlur | `Hidden/lilToonRefractionBlur` | refused | no Multi container exists |
| none | FurTwoPass | `Hidden/lilToonFurTwoPass` | refused | no Multi container exists |

`[INFERENCE]` The mapping closes cleanly: the modes AMUSE supports or
attests (opaque, cutout, transparent, and their outline forms) correspond
one-to-one with `ltsmulti` and `ltsmulti_o` at modes 0 through 2, and the
modes AMUSE refuses (refraction, fur, gem) correspond one-to-one with the
three dedicated containers. The observed corpus population (136 materials,
all mode 0 on the base and outline containers) maps exactly onto AMUSE's two
attested opaque identities. `[MEASURED 2026-09-30]`

## 7. Deltas that matter for support shaping

1. One asset, keyword-carried mode. The shader name proves the container, not
   the behavior. Any Multi proof needs the mode-consistency gate, which needs
   keyword evidence. This is the mode-keyed identity model.
2. `_UseOutline` is not a live toggle on the base container. It has exactly
   one reader: a one-shot startup migration from lilToon 1.2.7 that swaps the
   material between `ltsmulti` and `ltsmulti_o` by the flag
   (`lilStartup.cs:185-193`). The live inspector derives outline state from
   the shader name. A base-container material with `_UseOutline 1` renders no
   outline.
3. `_UseClippingCanceller` has a render-time consumer in the Multi family: it
   feeds `LIL_MULTI_SHOULD_CLIPPING` (`lil_common.hlsl:46`). It is real
   behavior state, not decoration, so a gate on it is warranted.
4. The two-pass and one-pass transparent distinctions do not exist in the
   Multi family. Multi transparent is always Normal-class: pre-pass absent,
   additive lighting present.
5. Queue edges differ. The vendor writes -1, 2450, 2460, or 3000 for Multi
   modes and skips its own preserve-custom-queue restore for Multi materials
   (`lilMaterialUtils.cs:266`), with the VRC-worlds 3000 override on top
   (`:339-348`). The outline container's declared default is 2900. Corpus
   materials override to 2000 `[MEASURED]`, which is the only queue where a
   proven-opaque submesh keeps the source's draw position.
6. The Multi containers are excluded from the vendor's per-material feature
   optimization path (`lilToonSetting.cs:1020`), and `lil_replace_keywords.hlsl`
   defines `LIL_IGNORE_SHADERSETTING` (`:6`). `[OPEN]` whether the Multi
   shader assets still regenerate under the global setting apply, which the
   digest measurement task must answer before pinning.
7. The variant containers prune their input sets (the gem container compiles
   thirteen inputs, ref and fur twenty-three, base twenty-four, outline
   twenty-five), so a profile pin is per container, not per family.

## 8. What this means for support shaping

`[INFERENCE]` The Multi family is not a new problem space. It is the regular
family's mode matrix collapsed into two containers for the supported range
and three containers for the refused range. A Multi slice inherits:

- the alpha theorems of the mapped identity (opaque constant-one, cutout
  proof, transparent proof),
- the eligibility shape of the mapped identity, adjusted for the container's
  queue defaults,
- one new shared obligation: the mode-consistency gate from keyword evidence,
- and the container deltas in section 7, each of which is small and
  enumerable.

The refused range inherits the F0 refusal rows without new work: the
dedicated containers carry the same defining behaviors (GrabPass, generated
fur geometry, additive gem) that produced those rows.

## 9. Open questions

1. `[OPEN]` Which alpha block runs at `LIL_RENDER 1` in the non-outline
   forward pass (section 5). Blocks any future Multi mode-1 proof until
   settled against the source.
2. `[OPEN]` Whether the Multi shader assets regenerate under the vendor's
   global setting apply (section 7 item 6). Decides the canonicalization
   shape for the container digests.
3. `[OPEN]` Whether any corpus population exists on the refraction, fur, or
   gem containers. The 2026-09-30 scan found none on the base and outline
   containers outside mode 0, and none on the specialized containers at all.
   A future census pass can close this with counts only.
