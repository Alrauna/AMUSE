# T1 — lilToon 2.3.4 regular Transparent Normal alpha separation

## 1. Question and bounded scope

> Can AMUSE safely support triangle-level alpha separation for exactly
> `Hidden/lilToonTransparent` (regular Transparent **Normal**), moving triangles proven
> visually opaque onto an appended submesh carrying the already-attested regular
> `lilToon` opaque target?

This is roadmap row 4 of the merged family inventory
(`2026-08-30-liltoon-family-applicability.md` §11), which classified the identity **C —
conditional** (§7.4) and named the missing proof: "transparent alpha semantics
investigation (a B2 successor), plus the blend/depth gate set and callback independence".

**In scope — exactly one candidate:**

| Axis | Value |
|---|---|
| Package | `jp.lilxyzw.liltoon` `2.3.4` |
| Upstream pin | tag `2.3.4`, commit `252fd8cfc46106d4967e95b3f2c788418502f227` |
| Source shader | `Hidden/lilToonTransparent` (`Shader/lts_trans.shader`) |
| Transparent mode | **Normal** only |
| Outline | none |
| Pipeline | regular — non-Lite, non-Tessellation, non-Multi |
| Target | regular `lilToon` (`Shader/lts.shader`), already attested by AMUSE |

**Out of scope and refused:** Transparent OnePass, Transparent TwoPass, every outline
variant, OutlineOnly, Lite, Tessellation, Multi, Overlay, Fur (all variants), Gem,
FakeShadow, Refraction, RefractionBlur, `.lilcontainer` identities, non-identity
`_MainTex_ST`, UV channels other than 0, trilinear/anisotropic/mirrored/asymmetric
sampling, and any generalized render-state IR, adapter framework, planner, or
third-party API. **No production behavior was designed or implemented here.**

**Verdict: GO.** Both prerequisites this note originally opened (P1, P2) were discharged
by a controller-authorized scratch measurement on the same day; see §3.4, §9.4, §12, §17.

**Labels.** `[SOURCE]` read from the pinned upstream tree or from the checked-out AMUSE
tree (cited); `[MEASURED]` executed observation in this session or a prior merged note
that measured it; `[INFERENCE]` bounded conclusion; `[DECISION]` a choice this note
makes; `[DECISION NEEDED]` a choice the controller must make. The `[PREREQUISITE]` label
appeared in this note's first draft for two facts that could not then be obtained; both
are now `[CLOSED — MEASURED]` (§12).

**Census Lab and private avatar data were not used, not inspected, and not modified.**
Two existing Unity instances were enumerated read-only and never targeted. No Unity MCP
call was issued against either.
All Unity work ran in a throwaway project outside AMUSE (§3.4), now deleted.

## 2. Repository and base state

| Fact | Value | Evidence |
|---|---|---|
| Repository | `/Users/user/Documents/AMUSE`, remote `https://github.com/Alrauna/AMUSE.git` | `[MEASURED]` `git remote -v` |
| Base | `main` = `origin/main` = `a3c547b6064b20709289a1062c11b7fd72818568` | `[MEASURED]` `git rev-parse HEAD origin/main` after `git fetch origin --prune` |
| PR #42 | **MERGED** — "Support affine `_MainTex_ST` mappings"; `a3c547b` is its merge commit, parents `89cc5be` (previous `main`) and `a804b1d` (`feature/affine-maintex-st-support` head) | `[MEASURED]` `pr://Alrauna/AMUSE/42` state `MERGED`; `git show --no-patch --format='%H %P'` |
| Local `main` update | none needed — local `main` already equalled `origin/main` (0 ahead, 0 behind) | `[MEASURED]` `git status -sb` |
| Branch created | `investigate/liltoon-transparent-normal-alpha-separation` from `a3c547b` | `[MEASURED]` `git switch -c` |
| Stacking | **not** stacked on `feature/affine-maintex-st-support` (that remote branch was deleted by the merge; the local branch's base is the merge commit on `main`) | `[MEASURED]` `git fetch --prune` reported `[deleted] origin/feature/affine-maintex-st-support` |
| Pre-existing user-owned churn | `Packages/manifest.json` (+`com.unity.toolchain.macos-arm64-linux-x86_64` 2.0.5) and `Packages/packages-lock.json` (+`com.unity.sysroot` 2.0.10, `com.unity.sysroot.linux-x86_64` 2.0.9, the toolchain entry) — additive only | `[MEASURED]` `git diff` on those two paths |
| Staged content | none | `[MEASURED]` `git diff --cached --stat` empty |

The two package files were inspected and **left untouched**. Nothing was staged,
committed, pushed, rebased, stashed, or published.

## 3. Source pins and methodology

### 3.1 Vendor pin

The official repository was cloned **read-only** into
`/tmp/liltoon-pin-UjXw/lilToon` — outside AMUSE, not a Unity project, nothing installed
into AMUSE, no package manifest touched.

- URL: `https://github.com/lilxyzw/lilToon.git`
- `git rev-parse HEAD` → `252fd8cfc46106d4967e95b3f2c788418502f227` `[MEASURED]`
- `git describe --tags --exact-match` → `2.3.4` `[MEASURED]`
- All `[SOURCE]` paths below are relative to `Assets/lilToon/` at that commit; all line
  numbers are for the **LF-normalized** file (the tree ships CRLF; every file cited here
  is byte-identical with and without CRLF stripping except for line terminators — see
  §3.2, where raw and LF SHA-256 coincide for every cited file, i.e. the files are
  already LF in the tree).

### 3.2 Digests of the cited source (SHA-256, LF-normalized)

| Path | SHA-256 |
|---|---|
| `Shader/lts_trans.shader` | `ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b457624097f2372ba13` |
| `Shader/ltspass_transparent.shader` | `f99549d936adfed259bbb20e8f99b2d564250c7ae4ab19fc27f4e748ed56795c` |
| `Shader/lts.shader` | `5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704` |
| `Shader/ltspass_opaque.shader` | `fe32b23b3b69d9d74a184c422e196a4e340e286ee7105697b5021695b9d61725` |
| `Shader/lts_cutout.shader` | `c83d73a26ab86e933f8cacb8c71307d8715fcc1693cdc08d209011bb0f836178` |
| `Shader/Includes/lil_pass_forward_normal.hlsl` | `f7a7c2900444d3f9b16fdf78e1ebe0d7765282f36cc3256d9cc7d506d3b54a47` |
| `Shader/Includes/lil_common_frag.hlsl` | `96b1bbfecc32d16735db16b5a0c46db3bf81c8f28b9d247c3394ae3c6af84dc1` |
| `Shader/Includes/lil_common_frag_alpha.hlsl` | `a9dfad250f2e21b9142297d261c1ad75391632139667f9a0859a202c85b13572` |
| `Shader/Includes/lil_common_macro.hlsl` | `49b4c364f1bd2f46a4dcb34921512c13473c03abb055428ee4da19dcce461802` |
| `Shader/Includes/lil_common_functions.hlsl` | `daee7c7dc133d85eb8096fe465e208d21361a4e6a570af1b2fe37c8b7bd296ed` |
| `Editor/lilMaterialUtils.cs` | `0f95ca692bb4dafb772533ce6727ff83626d58acdbc1ef181f5d7e51f586e8ac` |

All rows `[MEASURED]` (`sha256sum` on the pinned clone).

**Cross-check that anchors these digests to AMUSE's merged attestation.** The LF SHA-256
of `Shader/lts.shader` (`5206bec2…a9c704`) is **exactly** AMUSE's merged
`LilToonSourceAttestation.ShaderCanonicalDigest`
(`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:339-340`),
and the LF SHA-256 of `Shader/lts_cutout.shader` (`c83d73a2…836178`) is exactly the
merged `CutoutShaderCanonicalDigest` (`:359-360`). `[MEASURED]` Two independent
material-shader assets reproduce their installed-measured pins from the tag tree, which
means AMUSE's canonicalization is a **no-op for material-entry shader assets** (they
contain no generator-varying region and no include line that canonicalization rewrites).
`[INFERENCE]`

Consequence: `ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b457624097f2372ba13` was a
strongly-supported candidate for the transparent material-shader canonical digest, and
the scratch probe (§3.4) has since **confirmed it as the measured installed value**. The
*pass* asset digest could not be derived this way — `ltspass_opaque.shader`'s raw LF hash
(`fe32b23b…`) differs from its merged pin `6b6c30c1…` `[MEASURED]`, because
canonicalization removes the generator-varying regions — so it was measured directly
(§3.4).

### 3.3 Method

1. Closed enumeration of the transparent container and its pass asset; extraction of
   internal names, GUIDs (from `.meta`), `UsePass` lists, tags, and `LIL_RENDER`.
2. **Whole-file diff of `ltspass_opaque.shader` against `ltspass_transparent.shader`**
   — the decisive structural experiment (§4.3).
3. Diff of the compiled `LIL_FEATURE_*` / `LIL_OPTIMIZE_*` define sets across the three
   regular pass assets.
4. Linear walk of `lil_pass_forward_normal.hlsl`'s fragment for `LIL_RENDER 2`, and of
   `lil_common_frag_alpha.hlsl` (the SHADOW_CASTER chain) and `lil_pass_meta.hlsl`.
5. Exhaustive grep of every `LIL_RENDER` conditional in `Shader/Includes/*.hlsl`, then
   inspection of each `LIL_RENDER == 2` site.
6. Reconciliation against the vendor mode map (`Editor/lilMaterialUtils.cs`).
7. Reconstruction of the current AMUSE implementation and merged design record from the
   checked-out tree at `a3c547b`.
8. One web search, for the **Unity** built-in `_DitherMaskLOD` texture (§9.4). Its
   result is explicitly **not** treated as sufficient primary evidence; §3.4 replaced it
   with a direct measurement.
9. A controller-authorized scratch Unity probe (§3.4) that discharged both prerequisites.

### 3.4 Scratch probe `[MEASURED]`

Executed 2026-09-01 under explicit controller authorization, after the first draft of this
note recorded P1 and P2 as open.

| Fact | Value |
|---|---|
| Editor | Unity `2022.3.22f1` (`887be4894c44`), macOS, Metal, Apple M2, Gamma color space, batch mode |
| Project | a throwaway `-createProject` at `/private/tmp/amuse-scratch-liltoon-FTtl/proj`, **outside** AMUSE; `Application.dataPath` asserted in every probe's own output |
| Vendor package | official `jp.lilxyzw.liltoon-2.3.4.zip` from the vendor's GitHub release, SHA-256 `34d172761c51aa9469a904704109086aafa6125a4fa0e058766e2ddc73d3b303`, 777 files, embedded under `Packages/jp.lilxyzw.liltoon` |
| Digest algorithm | AMUSE's **own** `LilToonSourceAttestation`, copied byte-for-byte (source SHA-256 `70140d4da04ecf27d852d2519cfa88129a59a2e5ba839ef029dc585ed5783ed5`) into the scratch project and driven directly — not reimplemented |
| Routing | no Unity MCP call was issued; two existing Unity instances were enumerated read-only and never targeted. The probe ran through a direct CLI `-projectPath` naming the scratch project |
| Sessions | every measurement repeated in a second, independent Editor process; both reports diffed **identical** |

**Faithfulness check.** Before reading any new value, the probe reproduced **all five**
digests AMUSE already has pinned, from this independent install:

| Pinned constant | Reproduced |
|---|---|
| `ShaderCanonicalDigest` `5206bec2…9c704` | yes |
| `PassCanonicalDigest` `6b6c30c1…5eb14` | yes |
| `IncludeTreeDigest` `6e2dce6c…8fd46` | yes |
| `CutoutShaderCanonicalDigest` `c83d73a2…836178` | yes |
| `CutoutPassCanonicalDigest` `ecd1caed…1bfe92` | yes |

Five-for-five against constants measured on 2026-08-18 from a *different* install proves
three things at once: the copied algorithm is faithful, this install is equivalent to the
one the merged pins came from, and the generated pass output is reproducible across
installs and across two weeks. `[MEASURED]`

**Regeneration is real, and confined.** Comparing the imported package against the
pristine extracted zip: the material-entry shaders (`lts.shader`, `lts_trans.shader`,
`lts_cutout.shader`) are byte-identical to the upstream tag after LF normalization, while
every `ltspass_*.shader` **is rewritten at import**. The transparent pass diff versus
upstream is exactly three `#pragma skip_variants` lines plus one
`#define LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE` — i.e. per-project variant stripping
driven by which packages are installed (`Editor/CurrentRP.txt` = `BRP` / `Metal`).
AMUSE's canonicalizer reports exactly `removedRegions = 2` on every `ltspass_*` asset and
`0` on every material-entry asset, which is why the pass digest is stable across installs
despite the rewrite. This is the concrete vindication of the merged design's refusal to
hash pass assets whole. `[MEASURED]` `[INFERENCE]`

## 4. Complete pass and source inventory

### 4.1 Identities

| Role | Shader name | Asset | GUID | Declared tags |
|---|---|---|---|---|
| Source container | `Hidden/lilToonTransparent` | `Shader/lts_trans.shader:1` | `165365ab7100a044ca85fc8c33548a62` | `RenderType=TransparentCutout`, `Queue=AlphaTest+10` (2460) — `:673` |
| Source pass asset | `Hidden/ltspass_transparent` | `Shader/ltspass_transparent.shader` | `2683fad669f20ec49b8e9656954a33a8` | `#define LIL_RENDER 2` — `:672` |
| Target container | `lilToon` | `Shader/lts.shader:638-644` | `df12117ecd77c31469c224178886498e` | `RenderType=Opaque`, `Queue=Geometry` (2000) — `:640` |
| Target pass asset | `Hidden/ltspass_opaque` | `Shader/ltspass_opaque.shader` | `61b4f98a5d78b4a4a9d89180fac793fc` | `#define LIL_RENDER 0` — `:639` |

All `[SOURCE]` / `[MEASURED]` (GUIDs read from the `.meta` files in the pinned clone).
The target row reproduces the merged AMUSE pins exactly
(`LilToonSourceAttestation.cs:323-344`).

Format stamp: `_lilToonVersion ("Version", Int) = 45` (`lts_trans.shader:570`), the same
value AMUSE already pins (`LilToonSourceAttestation.ShaderFormatVersion = 45f`).
`[SOURCE]`

**Measured canonical digests** `[MEASURED]` (§3.4; two sessions, identical):

| Constant the implementation needs | Measured value |
|---|---|
| `TransparentShaderCanonicalDigest` | `ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b457624097f2372ba13` |
| `TransparentPassCanonicalDigest` | `700a607661f2cc43550452795d8eae0634509dbd07b4e8c381d9412fcc52517f` |
| `IncludeTreeDigest` (shared, already pinned) | `6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46` |

The material digest equals the upstream tag's LF hash because material-entry assets are
not regenerated; the pass digest does **not** equal any upstream hash, because the pass
asset is rewritten at import and then canonicalized (§3.4).

### 4.2 Pass list and ordering

`lts_trans.shader:671-677`:

```
SubShader { Tags {"RenderType" = "TransparentCutout" "Queue" = "AlphaTest+10"}
  UsePass "Hidden/ltspass_transparent/FORWARD"
  UsePass "Hidden/ltspass_transparent/FORWARD_ADD"
  UsePass "Hidden/ltspass_transparent/SHADOW_CASTER"
  UsePass "Hidden/ltspass_transparent/META"
  Pass { Tags { "LightMode" = "Never" } … }   // IN-60271 UV-channel workaround; never executes
```

The pass asset declares **eight** passes: `FORWARD_BACK` (`:795`), `FORWARD` (`:844`),
`FORWARD_OUTLINE` (`:892`), `FORWARD_ADD` (`:945`), `FORWARD_ADD_OUTLINE` (`:993`),
`SHADOW_CASTER` (`:1045`), `SHADOW_CASTER_OUTLINE` (`:1085`), `META` (`:1126`). The
no-outline Normal container references exactly four of them, in the order above.
`[SOURCE]`

**Normal vs OnePass vs TwoPass — the counterexample to grouping by `LIL_RENDER 2`:**

| Container | `UsePass` list |
|---|---|
| `lts_trans.shader:674-677` (Normal) | FORWARD, **FORWARD_ADD**, SHADOW_CASTER, META |
| `lts_onetrans.shader:674-676` (OnePass) | FORWARD, SHADOW_CASTER, META — **no FORWARD_ADD** |
| `lts_twotrans.shader:674-678` (TwoPass) | **FORWARD_BACK**, FORWARD, FORWARD_ADD, SHADOW_CASTER, META |

All three declare identical tags and share the same pass asset, so the shader-asset
identity — not `LIL_RENDER`, not the queue, not `RenderType` — is what distinguishes
them. `[SOURCE]` This is exactly why family selection stays an **exact shader-name
allowlist**: OnePass and TwoPass are indistinguishable from Normal by every other
declared fact.

`LIL_TRANSPARENT_PRE`, which activates the `_PreColor`/`_PreCutoff`/`_PreOutType`
fragment arm (`lil_pass_forward_normal.hlsl:405-410`), is defined **only** inside
`FORWARD_BACK` (`ltspass_transparent.shader:831`). Normal never compiles that arm.
`[SOURCE]`

### 4.3 Decisive structural result: the two pass assets differ in seven places

A whole-file diff of `ltspass_opaque.shader` against `ltspass_transparent.shader`
produces exactly 108 diff lines in seven hunks `[MEASURED]`:

1. `Shader "…"` name (`:1`).
2. `_Cull` / `_OutlineCull` inspector **label strings** only (`:578`, `:609`) — cosmetic.
3. `_DstBlend` default `0` → `10` (`:580`).
4. `_OutlineSrcBlend` `1` → `5`, `_OutlineDstBlend` `0` → `10` (`:610-611`) — outline
   family, not referenced by the no-outline container.
5. The `_Pre*` property block (`:636-668`), consumed only by `FORWARD_BACK`.
6. `#define LIL_RENDER 0` → `#define LIL_RENDER 2` (`:639` / `:672`).
7. The added `FORWARD_BACK` pass (`:792-840`).

**Every declaration of `FORWARD`, `FORWARD_ADD`, `SHADOW_CASTER`, and `META` is
byte-identical between the two assets** — stencil block, `Cull`/`ZClip`/`ZWrite`/`ZTest`/
`ColorMask`/`Offset`/`BlendOp`/`Blend`/`AlphaToMask` expressions, and the `#pragma`
sets. `[MEASURED]` `[SOURCE]`

The compiled feature sets are also identical: both assets (and `ltspass_cutout.shader`)
declare the **same 103** `LIL_FEATURE_*`/`LIL_OPTIMIZE_*` symbols, with a `diff` of the
sorted define lists returning empty. `[MEASURED]`

Consequences, all `[INFERENCE]` from `[SOURCE]`:

- Source and target participate in **exactly the same four light modes**; conversion
  neither adds nor removes a pass. (Contrast OnePass, where the target *adds*
  ForwardAdd — F0 §7.5.)
- Every render-state difference between source and target is carried by **material
  property values**, never by pass declarations. AMUSE's existing evidence model
  (captured scalars + effective queue + `RenderType`) is therefore sufficient to describe
  the whole render-state delta; no pass model, no render IR.
- Every *behavioral* difference is carried by the single compile-time constant
  `LIL_RENDER`, whose value AMUSE already reads from the live pass
  (`LilToonSourceAttestation.TryScanRenderMode`, `:929`).

### 4.4 Declared render state, source vs target

Pass declarations (identical in both assets):

| Pass | Declared state |
|---|---|
| `FORWARD` (`:845-861` / opaque equivalent) | `Stencil{Ref/ReadMask/WriteMask/Comp/Pass/Fail/ZFail = [_Stencil*]}`, `Cull [_Cull]`, `ZClip [_ZClip]`, `ZWrite [_ZWrite]`, `ZTest [_ZTest]`, `ColorMask [_ColorMask]`, `Offset [_OffsetFactor],[_OffsetUnits]`, `BlendOp [_BlendOp],[_BlendOpAlpha]`, `Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]`, `AlphaToMask [_AlphaToMask]` |
| `FORWARD_ADD` (`:946-965`) | same stencil, `Cull [_Cull]`, `ZClip [_ZClip]`, **`ZWrite Off`**, **`ZTest LEqual`**, `ColorMask [_ColorMask]`, `Offset […]`, **`Blend [_SrcBlendFA] [_DstBlendFA], Zero One`**, `BlendOp [_BlendOpFA],[_BlendOpAlphaFA]`, `AlphaToMask [_AlphaToMask]` |
| `SHADOW_CASTER` (`:1046-1060`) | same stencil, `Offset 1, 1`, `Cull [_Cull]` |
| `META` (`:1126-1128`) | `Cull Off` |

Property **defaults** that differ between the containers (`lts_trans.shader:578-604` vs
`lts.shader` / `ltspass_opaque.shader:578-604`):

| Property | `lts_trans` default | `lts` default | AMUSE canonical opaque recipe |
|---|---:|---:|---:|
| `_SrcBlend` | 1 | 1 | 1 |
| `_DstBlend` | **10** (OneMinusSrcAlpha) | 0 | 0 |
| `_SrcBlendAlpha` | 1 | 1 | 1 |
| `_DstBlendAlpha` | 10 | 10 | 10 |
| `_BlendOp` / `_BlendOpAlpha` | 0 / 0 | 0 / 0 | 0 / 0 |
| `_SrcBlendFA` / `_DstBlendFA` | 1 / 1 | 1 / 1 | 1 / 1 |
| `_SrcBlendAlphaFA` / `_DstBlendAlphaFA` | 0 / 1 | 0 / 1 | 0 / 1 |
| `_BlendOpFA` / `_BlendOpAlphaFA` | 4 / 4 (Max) | 4 / 4 | 4 / 4 |
| `_ZWrite` | 1 | 1 | 1 |
| `_ZTest` | 4 (LEqual) | 4 | 4 |
| `_ColorMask` | 15 | 15 | 15 |
| `_AlphaToMask` | **0** | 0 | 0 |
| `_OffsetFactor` / `_OffsetUnits` | 0 / 0 | 0 / 0 | 0 / 0 |
| `_Cull` | 2 | 2 | not in recipe (cloned) |

`[SOURCE]` The **only** canonical-recipe property whose transparent default is
non-canonical is `_DstBlend` (10 → 0). Note this differs from cutout, whose one
non-canonical default was `_AlphaToMask` (1 → 0, B1 §9): transparent already defaults
`_AlphaToMask` to 0.

## 5. Complete alpha and RGB dataflow (`LIL_RENDER 2`, no outline, no refraction, no fur)

### 5.1 Vertex stage — identical to cutout

`lil_common_vert.hlsl` contains **no** `LIL_RENDER` conditional `[MEASURED]` (exhaustive
grep of `Shader/Includes/*.hlsl`). The three vertex coverage sites are therefore exactly
the ones B2 established for cutout:

1. `_Invisible` — `LIL_VERTEX_CONDITION` (`:74`, `:78-84`) returns a zeroed `v2f`.
   Default 0 (`lts_trans.shader:19`).
2. IDMask — `#if defined(LIL_FEATURE_IDMASK) && !defined(LIL_NOT_SUPPORT_VERTEXID) && !defined(LIL_LITE)`
   (`:362`); a masked vertex collapses, and `_IDMaskControlsDissolve` drives
   `dissolveActive = idMasked != priorIdMasked` / `dissolveInvert = priorIdMasked`
   (`:399-400`). All flags default 0 (`lts_trans.shader:478-485`, `:496-504`).
3. UDIM vertex discard — `if(_UDIMDiscardMode == 0 && _UDIMDiscardCompile == 1 && LIL_CHECK_UDIMDISCARD(input))`
   (`:414`). Both default 0 (`lts_trans.shader:508-510`).

`dissolveActive`/`dissolveInvert` are initialized `true`/`false` (`:55-56`) and packed
into the v2f (`:426`).

### 5.2 Fragment chain, `FORWARD` and `FORWARD_ADD` (one shared `frag`)

`lil_pass_forward_normal.hlsl`, in execution order, non-outline branch:

| # | Step | Site | `LIL_RENDER 2` behavior |
|---|---|---|---|
| 1 | UDIM pixel discard | `:154-157` → `lil_common_frag.hlsl:718-720` | `if(_UDIMDiscardMode == 1 && LIL_CHECK_UDIMDISCARD(fd)) discard;` |
| 2 | Main UV | `:264-265` → `lil_common_frag.hlsl:261-263` | `fd.uvMain = lilCalcDoubleSideUV(fd.uv0, fd.facing, _ShiftBackfaceUV)`, then `lilCalcUV(uvMain, _MainTex_ST, _MainTex_ScrollRotate)` |
| 3 | Parallax | `:271-273` → `lil_common_frag.hlsl:296-305` | `lilPOM`/`lilParallax`, both internally gated on `_UseParallax` |
| 4 | Main color | `:278-279` → `lil_common_frag.hlsl:354-358` | `LIL_GET_MAIN_TEX`, RGB-only tone correction, then `fd.col *= _Color`. **The only alpha write on the unmodified chain: `a₀ = tex2D(_MainTex, uvMain).a × _Color.a`** |
| 5 | AudioLink | `:333-335` | writes `fd.audioLinkValue` only |
| 6 | Layer 2nd / 3rd | `:344-360` → `lil_common_frag.hlsl:798-806`, `:894-902` | `#if LIL_RENDER != 0`: alpha modes 1–4 replace/multiply/add/subtract `fd.col.a`, inside `if(_UseMain2ndTex)` / `if(_UseMain3rdTex)` |
| 7 | Alpha mask | `:361-364` → `lil_common_frag.hlsl:465-476` | `#if defined(LIL_FEATURE_ALPHAMASK) && LIL_RENDER != 0`; body wrapped in `if(_AlphaMaskMode)` |
| 8 | Dissolve | `:369-385` → `lil_common_frag.hlsl:488-519` | `#if defined(LIL_FEATURE_DISSOLVE) && LIL_RENDER != 0`, wrapper `if(fd.dissolveActive){ prior=a; a=1; OVERRIDE_DISSOLVE; if(dissolveInvert) a=1-a; a*=prior; }` |
| 9 | Dither | `:388-390` | **`#if defined(LIL_FEATURE_DITHER) && LIL_RENDER == 1`** — *never compiled for transparent* |
| 10 | **Alpha** | `:394-416` | `#elif LIL_RENDER == 2 && !defined(LIL_REFRACTION)`: `clip(fd.col.a - _Cutoff);` (`:411`). Plain clip — **not** the cutout `fwidth` coverage transform |
| 11 | Depth fade | `:417-421` → `lil_common_frag.hlsl:1980-2009` | `#if defined(LIL_FEATURE_DEPTH_FADE) && LIL_RENDER == 2 …` — **`LIL_FEATURE_DEPTH_FADE` is never defined anywhere in the package** (§5.5) |
| 12 | Shading tail | `:424-590` | RGB / `fd.emissionColor` only; several `LIL_RENDER == 2` arms multiply by `fd.col.a` (§5.4) |
| 13 | Premultiply | `:502` → `lil_common_frag.hlsl:554-560` | base: `fd.col.rgb *= fd.col.a`; ForwardAdd: `fd.col.rgb *= saturate(fd.col.a * _AlphaBoostFA)` |
| 14 | Distance fade | `:592-594` → `lil_common_frag.hlsl:2028-2053` | `#if LIL_RENDER == 2` arm (`:2047-2049`) writes **both** `fd.col.rgb` and **`fd.col.a`** — the only alpha write after the clip |
| 15 | Fog | `:604` → `lil_common_macro.hlsl:978-983` / `:1865-1871` | `LIL_RENDER == 2` fogs toward `unity_FogColor * col.a` |

**No alpha write exists between step 10 (the clip) and step 14** in the pinned build,
because step 11 never compiles. `[SOURCE]`

### 5.3 Premultiplication (`lil_common_frag.hlsl:554-560`)

```hlsl
#if LIL_RENDER != 2
    #define LIL_PREMULTIPLY
#elif defined(LIL_PASS_FORWARDADD) && !defined(LIL_REFRACTION)
    #define LIL_PREMULTIPLY fd.col.rgb *= saturate(fd.col.a * _AlphaBoostFA);
#else
    #define LIL_PREMULTIPLY fd.col.rgb *= fd.col.a;
#endif
```

- Base pass at `a ≡ 1`: `rgb *= 1` — exact identity, matching the target's empty macro.
- **ForwardAdd at `a ≡ 1`: `rgb *= saturate(_AlphaBoostFA)`.** This is **not** an
  identity unless `_AlphaBoostFA >= 1`. The property is `Range(1,100)` with default 10
  (`lts_trans.shader:32`), so the inspector cannot produce a value below 1 — but a
  serialized or animated value can, and `saturate` of a value in `[0,1)` scales the
  additive light contribution down. The opaque target has **no** premultiply, so a
  material with `_AlphaBoostFA < 1` would get *brighter* additive lighting after
  conversion. `[SOURCE]` This is a load-bearing, transparent-only gate that has no cutout
  analogue.

### 5.4 The `LIL_RENDER == 2` RGB arms are all `× fd.col.a`

Exhaustive enumeration of every `LIL_RENDER == 2` site in `lil_common_frag.hlsl`
`[MEASURED]`:

| Site | Expression | At `a ≡ 1` |
|---|---|---|
| `:1452-1454` | `if(_ReflectionApplyTransparency) reflectionColor.a *= fd.col.a;` | identity |
| `:1551-1553` | `if(_MatCapApplyTransparency) matCapColor.a *= fd.col.a;` | identity |
| `:1619-1621` | `if(_MatCap2ndApplyTransparency) matCap2ndColor.a *= fd.col.a;` | identity |
| `:1682-1688` | `if(_RimApplyTransparency){ rimDir *= fd.col.a; rimIndir *= fd.col.a; }` | identity |
| `:1717-1719` | `if(_RimApplyTransparency) rim *= fd.col.a;` | identity |
| `:1788-1790` | `if(_GlitterApplyTransparency) glitterColor.a *= fd.col.a;` | identity |
| `:1862-1864` | `emissionBlend *= fd.col.a;` | identity |
| `:1946-1948` | `emission2ndBlend *= fd.col.a;` | identity |
| `:1969-1975` | `OVERRIDE_BLEND_EMISSION` = `fd.col.rgb += fd.emissionColor * fd.col.a;` (target: `+= fd.emissionColor`) | identity |
| `:2047-2049` | distance fade: `rgb = lerp(rgb, fadeColor * _DistanceFadeColor.a, distFade); a = lerp(a, a * _DistanceFadeColor.a, distFade);` (target arm: `rgb = lerp(rgb, fadeColor, distFade)`) | **identity only when `distFade ≡ 0`** |
| `lil_common_macro.hlsl:978-983`, `:1865-1871` | fog color `× col.a` (target: no `× col.a`) | identity |

So, with the single exception of distance fade, **every RGB divergence between
`LIL_RENDER 2` and `LIL_RENDER 0` is a multiplication by `fd.col.a` and collapses to the
target's expression at `a ≡ 1`.** `[SOURCE]` `[INFERENCE]`

Distance fade is different because its two arms are *structurally* different, not merely
scaled: the transparent arm fades toward `fadeColor * _DistanceFadeColor.a` and also
writes alpha, while the opaque arm fades toward `fadeColor`. `[SOURCE]`
`lil_common_frag.hlsl:2030-2036`:

```hlsl
float distFade = saturate((depth - _DistanceFade.x) / (_DistanceFade.y - _DistanceFade.x));
#if defined(LIL_OUTLINE) || defined(LIL_PASS_FORWARD_FUR_INCLUDED)
    distFade = distFade * _DistanceFade.z;
#else
    distFade = fd.facing < (_DistanceFade.w-1.0) ? _DistanceFade.z : distFade * _DistanceFade.z;
#endif
```

Both arms are multiplied by `_DistanceFade.z`, so `_DistanceFade.z == 0` forces
`distFade ≡ 0` on front and back faces alike and makes both `lerp`s exact identities.
Default `_DistanceFade = (0.1, 0.01, 0, 0)` (`lts_trans.shader:430`), i.e. off by
default. `[SOURCE]` In `FORWARD_ADD` the function takes the `#if defined(LIL_PASS_FORWARDADD)`
arm (`lil_common_frag.hlsl:2045-2046`), which writes RGB only.

`lilDistanceFadeAlphaOnly` (`:2014-2026`) writes alpha only under `#if LIL_RENDER == 1`
and is reachable only from `OVERRIDE_DITHER`, which transparent never compiles. Inert
here. `[SOURCE]`

### 5.5 Depth fade is dead code in 2.3.4

`LIL_FEATURE_DEPTH_FADE` appears at exactly three sites in the entire package
(`lil_pass_forward_normal.hlsl:242`, `:418`; `lil_common_frag.hlsl:1980`) and is
**never defined** — not by any pass asset (the 103-symbol lists in §4.3 do not contain
it) and not by `Editor/lilToonSetting.cs`. The `_DepthFade*` uniforms are not declared in
`lil_common_input_base.hlsl` / `lil_common_input_opt.hlsl` / `lil_common_input.hlsl`
either; they survive only as commented-out lines inside the function body
(`lil_common_frag.hlsl:1999-2003`), and `LIL_ENABLED_DEPTH_TEX` is likewise undefined
anywhere. `[MEASURED]` (exhaustive grep over `*.hlsl`, `*.shader`, `*.cs`.)

**Verdict:** "depth fade to alpha" is unreachable in the pinned version. It requires no
gate and no captured property. If a future lilToon defines the symbol, the pass digest
and the include-tree digest both change and attestation fails closed. `[INFERENCE]`

### 5.6 `SHADOW_CASTER` (`lil_common_frag_alpha.hlsl`, included at `lil_pass_shadowcaster.hlsl:67`)

The whole file body is wrapped in `#if LIL_RENDER > 0` (`:12`, `:116`), so the **opaque
target's shadow caster performs no alpha work and never discards.** `[SOURCE]`

For `LIL_RENDER 2` the chain is: UDIM discard (`:8-10`) → main UV (`:21-22`) → main color
(`:31-32`) → layers (`:41-54`) → alpha mask (`:58-61`) → dissolve (`:65-81`) → fur
(absent) → dither (`LIL_RENDER == 1` only, `:93-95`) → then:

```hlsl
clip(fd.col.a - _Cutoff);                                  // :99
#if LIL_RENDER == 2 && !defined(SHADER_API_GLES)           // :100
    float alphaRef = fd.col.a;
    #if LIL_SUBPASS_TRANSPARENT_MODE == 1 || defined(SHADERPASS) && (SHADERPASS == SHADERPASS_SHADOWS)
        alphaRef = lilSampleDither(_DitherMaskLOD, input.positionCS.xy, fd.col.a);
    #elif LIL_SUBPASS_TRANSPARENT_MODE == 0 && defined(LIL_PASS_SHADOWCASTER_INCLUDED)
        #if defined(SHADOWS_DEPTH)
            if(LIL_MATRIX_P._m33 != 0.0)
        #endif
        alphaRef = lilSampleDither(_DitherMaskLOD, input.positionCS.xy, fd.col.a);
    #endif
    clip(alphaRef - _SubpassCutoff);                       // :114
#endif
```

- There is no parallax step in this chain, so with `_UseParallax == 0` the sampled domain
  is identical to the forward pass's. `[SOURCE]`
- `LIL_SUBPASS_TRANSPARENT_MODE` is `0` (`lil_common_macro.hlsl:16`) and `SHADERPASS` is
  an SRP symbol not defined in the BRP passes, so the **second** branch applies.
- On D3D11-class APIs the sampler is the texel-load form
  (`lil_common_macro.hlsl:394-399`): `uint3 uv = uint3(positionCS, alpha*0.9375*16); uv.xy %= 4; return tex[uv].a;`
  At `alpha = 1` that is slice `uint(15.0) = 15`, point-loaded, xy modulo 4. On
  D3D9/surface-analysis it is the filtered form (`:316-319`),
  `tex3D(tex, float3(positionCS*0.25, 0.9375)).a`, whose w coordinate falls exactly
  between the slice-14 and slice-15 centres. `[SOURCE]`
- `_SubpassCutoff` is `Range(0,1)` with default `0.5` (`lts_trans.shader:22`).

**This is the one place where the transparent source's coverage at `a ≡ 1` is not
decidable from lilToon source.** It was resolved by measurement — see §9.4.

### 5.7 `META` (`lil_pass_meta.hlsl:51-102`)

`OVERRIDE_MAIN` (so `col.a = sample.a × _Color.a`), no clip, emission, then
`OVERRIDE_BLEND_EMISSION` — which for `LIL_RENDER 2` multiplies by `fd.col.a`
(`lil_common_frag.hlsl:1969-1975`). At `a ≡ 1` the meta output equals the target's.
`[SOURCE]` (META is lightmap-baking only and does not participate in VRChat avatar
rendering; it is covered here for completeness, not because it is load-bearing.)

## 6. Complete alpha-, coverage-, and RGB-affecting inventory

"Gate" is the captured runtime material state that fully neutralizes the mechanism.
Every row is `[SOURCE]` unless noted.

| # | Mechanism | Site | Gate (OFF state) | Effect when active | Same as cutout? |
|---|---|---|---|---|---|
| 1 | `_Invisible` | `vert:74,78-84` | `== 0` | geometry collapses | yes |
| 2 | IDMask vertex collapse / dissolve control | `vert:362-411` | `_IDMask1..8 == 0` **and** `_IDMaskControlsDissolve == 0` | NaN vertex; `dissolveInvert` can force chain alpha to 0 even at dissolve mode 0 | yes |
| 3 | UDIM vertex discard | `vert:414` | `_UDIMDiscardCompile == 0` | NaN vertex | yes |
| 4 | UDIM pixel discard | `frag:718-720` | `_UDIMDiscardMode == 0` | per-pixel `discard` | yes |
| 5 | Backface UV shift | `functions:467-470` | `_ShiftBackfaceUV == 0` | backface samples `uv.x + 1` | yes |
| 6 | Main UV scroll/rotate | `functions:455-460` | `_MainTex_ScrollRotate == (0,0,0,0)` per binary32 component | time-varying sample domain | yes |
| 7 | Parallax / POM | `frag:296-305`, `functions` | `_UseParallax == 0` | per-pixel `uvMain` offset | yes |
| 8 | `_MainTex_ST` | `functions:455-460` | exact identity `(1,1,0,0)` | affine domain change; and lilToon's **unbounded zero-angle rotate round trip** (`lilRotateUV(uv,0)` has no early-out, `functions:424-435`) | yes — the family boundary PR #42 preserved |
| 9 | `_Color.a` multiplier | `frag:354-358` | — | `a₀ = sample.a × _Color.a` | yes |
| 10 | Main tone correction / gradation | `frag:311-343` | none needed | RGB only | yes |
| 11 | AudioLink fragment | `frag:638-711` | none needed | writes `fd.audioLinkValue` only | yes |
| 12 | Main 2nd layer | `frag:798-806` | `_UseMain2ndTex == 0` | alpha replace/mul/add/sub | yes |
| 13 | Main 3rd layer | `frag:894-902` | `_UseMain3rdTex == 0` | alpha replace/mul/add/sub | yes |
| 14 | Alpha mask | `frag:465-476` | `_AlphaMaskMode == 0` | alpha replace/mul/add/sub | yes |
| 15 | Dissolve | `frag:488-519`, `functions:626-700` | `_DissolveParams.x == 0` | `alpha *= maskVal` | yes |
| 16 | **Dither** | `forward:388-390`, `frag_alpha:93-95` | **none needed — compiled only for `LIL_RENDER == 1`** | none on this family | **no — inert here** |
| 17 | **Transparent clip** | `forward:411` | — | `clip(a - _Cutoff)`; needs `_Cutoff <= 1` | **no — plain clip, no `fwidth`** |
| 18 | **Depth fade to alpha** | `forward:417-421` | **none — `LIL_FEATURE_DEPTH_FADE` undefined in 2.3.4** | none | **no — dead code** |
| 19 | **Base-pass premultiply** | `frag:559` | — | `rgb *= a`; identity at `a ≡ 1` | **no — cutout has none** |
| 20 | **ForwardAdd premultiply** | `frag:557` | **`_AlphaBoostFA >= 1`** (finite) | `rgb *= saturate(a · _AlphaBoostFA)` — scales additive light | **no** |
| 21 | **Distance fade** | `frag:2028-2053` | **`_DistanceFade.z == 0`** | writes RGB *and* alpha with a different formula than the target | **no — cutout takes the RGB-only arm** |
| 22 | **`…ApplyTransparency` / emission / fog `× a`** | 11 sites, §5.4 | none needed | identity at `a ≡ 1` | **no — cutout has none** |
| 23 | **Subpass shadow clip** | `frag_alpha:99-115` | `_Cutoff <= 1` **and** the `_SubpassCutoff`/dither question (§9.4) | source may discard shadow fragments the target casts | **no — compiled out for cutout** |
| 24 | `_PreColor`/`_PreCutoff`/`_PreOutType` | `forward:405-410` | compile-excluded (`LIL_TRANSPARENT_PRE` is FORWARD_BACK-only) | — | n/a |
| 25 | Everything else in the tail | `forward:424-590` | none needed | RGB / `fd.emissionColor` only | yes |

`[INFERENCE]` The inventory is exhaustive for the pinned no-outline Transparent Normal
path: §5.1 walks the vertex function's coverage sites, §5.2 walks the fragment linearly,
§5.4 enumerates every `LIL_RENDER == 2` conditional found by exhaustive grep, §5.6 walks
the subpass chain, and §5.7 the meta chain. Outline-only sites (`LIL_OUTLINE`) are not
referenced by the supported container.

**Neutral-claim rule check** (`docs/architecture/shader-frontend-comparison.md`, "The
neutral-claim gating rule"): the theorem in §9 asserts `a ≡ 1`, a neutral claim. Every
independent writer in rows 1–23 is proven off, dead, or an identity at `a ≡ 1` *before*
the claim is made. Rows 19–23 are the writers that do **not** exist in the cutout
frontend, and they are exactly where a copy-paste of the cutout gate list would be
unsound.

## 7. Render-state characterization and source eligibility

Canonical authored source state for a fresh Transparent Normal material (§4.4) is:

```
queue 2460 (AlphaTest+10), RenderType "TransparentCutout"
_SrcBlend 1, _DstBlend 10, _SrcBlendAlpha 1, _DstBlendAlpha 10, _BlendOp 0, _BlendOpAlpha 0
_SrcBlendFA 1, _DstBlendFA 1, _SrcBlendAlphaFA 0, _DstBlendAlphaFA 1, _BlendOpFA 4, _BlendOpAlphaFA 4
_ZWrite 1, _ZTest 4, _ColorMask 15, _AlphaToMask 0, _OffsetFactor 0, _OffsetUnits 0
```

Measured against AMUSE's merged eligibility gates
(`LilToonOpaqueConversion.EvaluateVerifiedEligibility`,
`LilToonOpaqueConversion.cs:263-432`) `[SOURCE]`:

| Gate | Merged rule | Transparent canonical value | Passes? |
|---|---|---|---|
| effective queue | `== 2450` | 2460 | **no — constant must become family-specific** |
| effective `RenderType` | `== "TransparentCutout"` | `"TransparentCutout"` | yes (same string) |
| `_ZTest` | `== 4` | 4 | yes |
| `_ZWrite` | `== 1` | 1 | yes |
| `_ColorMask` | `== 15` | 15 | yes |
| `_OffsetFactor`/`_OffsetUnits` | `== 0` | 0 | yes |
| base RGB blend | `_BlendOp == Add`, `_SrcBlend ∈ {One, SrcAlpha}`, `_DstBlend ∈ {Zero, OneMinusSrcAlpha}` | Add, One, **OneMinusSrcAlpha** | **yes — the existing rule already admits it** |
| base alpha blend | same shape | Add, One, OneMinusSrcAlpha | yes |
| ForwardAdd blend | `_SrcBlendFA ∈ {One, SrcAlpha}`, `_DstBlendFA == One`, `_BlendOpFA == Max`, `_BlendOpAlphaFA == Max` | One, One, Max, Max | yes |
| clip threshold | `_Cutoff <= 0.9999` | `_Cutoff <= 1` suffices here (§9.2) | over-strict but sound |

`[INFERENCE]` **The merged blend-degeneration lemma already covers Transparent Normal.**
`IsZeroDestinationFactorAtAlphaOne` was written to accept `OneMinusSrcAlpha`
(`LilToonOpaqueConversion.cs:459-463`) precisely because at `a = 1` it evaluates to 0, so
`One/OneMinusSrcAlpha ≡ One/Zero`. Only two constants (`SupportedCutoutRenderQueue`,
and the cutoff bound) and three new gates (`_AlphaBoostFA`, `_DistanceFade.z`,
`_SubpassCutoff`) separate the transparent source from the merged eligibility function.

**Queue/order.** Moving proven triangles from 2460 to 2000 changes draw order. Both
2000 and 2460 are below Unity's 2501 transparent-sorting threshold, so both are
front-to-back opaque-sorted, and the source already declares `_ZWrite 1` (required by
the gate). The moved triangles are depth-determined, not order-determined.
`[INFERENCE]` This is the same representation change the merged cutout slice already
accepted (2450 → 2000) and is explicitly permitted by the Conservative policy
("transparent-vs-opaque implementation when a surface is proven visually opaque", vision
§3.1, §16.3). Custom queues and custom `RenderType` overrides fail closed, unchanged.
`[DECISION]`

**Stencil, `_Cull`, `_ZClip`.** These are `[_Property]`-driven in both pass assets and
are copied by `new Material(source)`; the recipe does not write them, exactly as in the
cutout slice. `[SOURCE]` `[INFERENCE]`

## 8. Animation and callback lifecycle

### 8.1 Proof-relevant animatable properties

Every gate in §6 is an ordinary `material.<Property>` float/color/vector and is therefore
animation-reachable; `_MainTex_ST` rides the texture request's `ScaleOffset` kind, whose
derived binding name AMUSE already recognizes. The exact-singleton admission machinery
(`AdmittedMaterialStates`) expresses the complete requirement: a non-singleton or
disagreeing binding on any of them refuses the slot, and a binding on a name outside the
request refuses the renderer batch. `[SOURCE]` (current tree)

Relative to the merged cutout request
(`LilToonCutoutMaterialSemantics.AlphaEvidenceRequest`,
`LilToonCutoutMaterialSemantics.cs:100-142`) the transparent request needs
**three additions** — `_AlphaBoostFA` (scalar), `_SubpassCutoff` (scalar),
`_DistanceFade` (vector) — and could drop `_UseDither` (inert, §6 row 16). Everything
else is identical.

`[INFERENCE]` No new host capability, capture stage, or admission concept is required.

### 8.2 Non-animatable facts

Shader identity, pass identity, digests, `LIL_RENDER`, and the compiled feature set are
not material properties and are gathered at the barrier by the existing
`GatherSourceEvidence` pattern. Effective queue and `RenderType` are read live during
preparation, as the merged design already does — Unity's material binding syntax cannot
address either. `[SOURCE]`

### 8.3 Callback 100 (`External/Editor/VRChatModule.cs`)

The mechanism is unchanged from B2 §9: at avatar build the module (callback order 100,
after NDMF) calls `lilToonSetting.SetShaderSettingBeforeBuild(materials, clips)`, which
re-unpacks each in-use shader from its `.lilinternal` container with only the used
`LIL_FEATURE_*` defines, rewrites `lil_common_input_opt.hlsl`, then restores the
committed files afterwards. `SetupMultiMaterial` touches Multi materials only.

Per-feature invariance for **this** family:

- `LIL_RENDER` is fixed per pass asset (`ltspass_transparent.shader:672`) and is never
  rewritten by the generator. `[SOURCE]`
- The core equation — main sample, `_Color` multiply, `clip(a - _Cutoff)`, premultiply,
  emission blend, fog — is unconditional code with no `LIL_FEATURE` gate.
  `LIL_PREMULTIPLY`, `OVERRIDE_BLEND_EMISSION`, and the fog macros are selected by
  `LIL_RENDER`, not by a feature symbol. `[SOURCE]`
- Every optional mechanism in §6 is either proven off at runtime (rows 1–15) or dead
  (rows 16, 18, 24). Stripping a block whose runtime gate is off preserves the fragment
  result; keeping it compiled likewise changes nothing. The verdict therefore cannot
  depend on the define set. `[INFERENCE]`
- **One new dependency vs cutout:** `LIL_FEATURE_DISTANCE_FADE` gates the only
  post-clip alpha writer. Stripping it removes the writer entirely; keeping it compiled
  leaves it neutral at `_DistanceFade.z == 0`. Invariant either way. `[SOURCE]`
  `[INFERENCE]`
- `LIL_INPUT_OPTIMIZED` affects only VRCLightVolumes paths, not alpha. `[SOURCE]`

**Verdict: NDMF-complete for this family too.** Upload-time ("Outcome B") validation is
not a prerequisite. Residual honesty is unchanged from B2: the uploaded per-avatar shader
artifact is not a stable attestable asset, so the design attests the committed source
plus the invariance argument, and does not digest the uploaded artifact. `[INFERENCE]`

### 8.4 Pass enables

`Material.SetShaderPassEnabled` state is serialized on the material and is copied by
`new Material(source)`, so it transfers to the clone. This is identical to the merged
cutout slice's treatment and introduces nothing new. `[INFERENCE]` `_AsOverlay`-driven
pass enables are a Multi-only mechanism (F0 §6.3) and are out of scope.

## 9. The theorem

### 9.1 Statement

**Theorem (Transparent Normal restricted core).** Let `T` be a triangle of submesh `S`
rendered with material `M` under the attested regular no-outline lilToon 2.3.4
Transparent **Normal** source. `T` is *proven opaque* iff all of the following hold.
Every clause is checked against captured, admitted evidence; unknown information fails
closed.

1. **Identity.** `M.shader` is `Hidden/lilToonTransparent`, GUID
   `165365ab7100a044ca85fc8c33548a62`, resolving pass `Hidden/ltspass_transparent`, GUID
   `2683fad669f20ec49b8e9656954a33a8`, package `jp.lilxyzw.liltoon` `2.3.4`, format stamp
   `_lilToonVersion == 45`, scanned `#define LIL_RENDER 2`, canonical material digest
   `ea247d3c…2ba13` and canonical pass digest `700a6076…52517f` *(both `[MEASURED]`, §3.4)*,
   under the shared 37-file include-tree digest `6e2dce6c…fd46`, with the canonicalization
   provenance the merged verifier already requires.
2. **Cutout-shared gates, captured and finite, exactly zero.** `_Invisible`,
   `_UDIMDiscardCompile`, `_UDIMDiscardMode`, `_ShiftBackfaceUV`, `_UseParallax`,
   `_UseMain2ndTex`, `_UseMain3rdTex`, `_AlphaMaskMode`, `_IDMask1`…`_IDMask8`,
   `_IDMaskControlsDissolve`; and `_DissolveParams.x == 0` with a finite
   `_DissolveParams`.
3. **Transparent-only gates.** `_Cutoff` finite and `<= 1` (§9.2);
   `_AlphaBoostFA` finite and `>= 1` (§5.3); `_DistanceFade` finite with
   `_DistanceFade.z == 0` (§5.4); and the `SHADOW_CASTER` subpass condition of §9.4.
4. **UV domain.** `_MainTex_ScrollRotate == (0,0,0,0)` per binary32 component and
   `_MainTex_ST == (1,1,0,0)` per binary32 component — the family's exact-identity
   boundary (§9.3).
5. **Texture.** `_MainTex` is an assigned `Texture2D` with an admitted format, full mip
   residency, filter Point or Bilinear, `wrapU == wrapV ∈ {Clamp, Repeat}`, and a
   measured GPU-readback alpha field for **every** mip level.
6. **Per-triangle classification.** For every mip level, the exact classifier over the
   triangle's UV hull — including the point/bilinear footprint neighbourhood and wrap
   normalization — finds every intersecting texel alpha `== 255`, i.e.
   `TriangleAlphaClassifier` yields `ProvenOpaque`; and `_Color.a == 1` (a multiplier
   below 1 yields uniform `MustRemainTransparent`, above 1 or non-finite refuses).
7. **Animation closure.** Every property named in clauses 2–4 and 6 is in the captured
   request, every binding reaching `M` on those properties is exact-singleton against the
   material's serialized default, and any unrecognized proof-relevant binding refuses the
   renderer batch.

**Conclusion.** Under clauses 1–7, chain alpha is `a ≡ 1` at the clip on every fragment
of `T` in `FORWARD`, `FORWARD_ADD`, `SHADOW_CASTER`, and `META`. Then:

- `clip(1 - _Cutoff)` keeps (clause 3);
- base premultiply `rgb *= 1` is the identity, and ForwardAdd premultiply
  `rgb *= saturate(1 · _AlphaBoostFA)` is the identity (clause 3);
- every `…ApplyTransparency`, emission-blend, and fog `× a` term equals the target's
  expression (§5.4);
- distance fade is the identity in both channels (clause 3);
- depth fade does not exist (§5.5);
- output alpha is 1, so `Blend One OneMinusSrcAlpha` degenerates exactly to
  `Blend One Zero`, and `_AlphaToMask` at any value yields full coverage;
- the ForwardAdd `Max`/`One One` state is already the target's canonical state (§4.4);
- the target renders the same geometry through byte-identical pass declarations
  (§4.3).

Therefore the converted triangle's `FORWARD`, `FORWARD_ADD`, and `META` output is
**arithmetically equal** to the source's. `SHADOW_CASTER` is equal too: the subpass clip
argument at `a ≡ 1` was measured to be exactly `1 - _SubpassCutoff` (§9.4), so under the
clause-3 gate `_SubpassCutoff <= 1` the fragment survives, matching the target's
unconditional shadow coverage. **Verdict: `ProvenOpaque` across `FORWARD`,
`FORWARD_ADD`, `SHADOW_CASTER`, and `META`.**

### 9.2 The cutoff bound is `<= 1`, not `<= 0.9999`

Transparent uses `clip(fd.col.a - _Cutoff)` (`forward:411`), which discards iff the
argument is `< 0`. At `a = 1` the evaluated argument is `fl(1 - c)` in IEEE-754 binary32
round-to-nearest. **Exact representability of `1 - c` is neither claimed nor needed** — it
does not hold across the whole declared range `c ∈ [-0.001, 1.001]` (`lts_trans.shader:21`).
Only the *sign* is load-bearing, and correctly-rounded subtraction preserves it:

- For finite binary32 `c <= 1` the exact difference `1 - c` is **nonnegative**.
  Round-to-nearest maps a nonnegative exact value to a nonnegative float, so
  `fl(1 - c) >= 0` and `clip` **keeps** the fragment. (Sterbenz additionally makes the
  subtraction exact on `c ∈ [0.5, 2]`, but that is a bonus, not a premise.)
- At `c == 1` the exact difference is `0`, `fl(0) = +0`, and `clip(+0)` keeps — discard is
  strictly `< 0`.
- For finite binary32 `c > 1` in the declared range the exact difference lies in
  `[-0.001, -2^-24]`: strictly negative, and orders of magnitude above the binary32
  underflow threshold, so no rounding can flush it to zero. `fl(1 - c) < 0` and `clip`
  **discards**.

So at `a ≡ 1` the fragment survives iff `c <= 1`. `[SOURCE]` `[INFERENCE]`

This is materially different from cutout, whose forward pass uses the *coverage*
transform `saturate((a - c)/max(fwidth(a), 0.0001) + 0.5)` and therefore needed the
`0.9999` twice-margin bound (B2 §3.4). Reusing `0.9999` here would be sound but would
refuse the exact band `c ∈ (0.9999, 1]` for no reason. `[DECISION]` The design should
carry a **family-specific** bound of `<= 1`, and the falsifier suite must contain a
`_Cutoff = 1.0` positive case that fails an implementation which copies the cutout
constant, plus a `_Cutoff = 1.001` negative case (the declared range's upper end, where
the source discards the triangle entirely and conversion would materialize invisible
geometry).

### 9.3 `_MainTex_ST` stays exact-identity, by family gate

PR #42 (`docs/superpowers/specs/2026-08-31-affine-maintex-st-support-design.md`) added a
**family-blind** affine resolver; it explicitly did **not** authorize lilToon, because
lilToon's fragment applies `lilRotateUV(outuv, 0)` with no zero-angle early-out
(`lil_common_functions.hlsl:424-435`, re-verified at this pin), evaluating
`fl(fl(fl(t−0.5)·co − fl(t'−0.5)·si) + 0.5)` rather than the identity (that spec §3.1,
§5-G, §11). Transparent Normal executes the *same* `lilCalcUV` overload
(`functions:455-460`) through the *same* `OVERRIDE_ANIMATE_MAIN_UV`
(`lil_common_frag.hlsl:261-263`). `[SOURCE]`

Therefore the transparent family inherits the identity-only boundary unchanged, and it
also inherits the affine spec's **C6 baseline clause**: the identity slice absorbs the
rotate-at-zero fragment noise as a declared abstraction, exactly as the shipped cutout
and opaque slices do. No new modelling, no new risk, no parity break. `[SOURCE]`
`[DECISION]`

### 9.4 `RESOLVED (was P2)` — the `SHADOW_CASTER` subpass dither at `a ≡ 1`

At `a ≡ 1` the transparent shadow caster evaluates
`clip(lilSampleDither(_DitherMaskLOD, positionCS.xy, 1.0) - _SubpassCutoff)`
(`lil_common_frag_alpha.hlsl:107/112,114`), i.e. on modern APIs
`clip(_DitherMaskLOD[uint3(x%4, y%4, 15)].a - _SubpassCutoff)`. The opaque target's
shadow caster performs no clip at all (`frag_alpha:12,116`).

Equivalence therefore requires: **every texel of `_DitherMaskLOD` slice 15, alpha
channel, is `>= _SubpassCutoff`.** `_DitherMaskLOD` is a Unity engine built-in binary
resource. Its contents are not in lilToon source, not in Unity's shipped shader source,
and not stated in Unity's documentation.

What is available:

- Unity's own built-in shader uses the identical sampling expression and clips at `0.01`
  (`UnityStandardParticleShadow.cginc`, "Our dither texture is 4x4x16":
  <https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/CGIncludes/UnityStandardParticleShadow.cginc>).
  That proves the sampling convention, **not** the slice-15 values, and Unity's `0.01`
  threshold would pass values that lilToon's default `0.5` would reject.
- A well-known secondary source (Catlike Coding, *Rendering 12*,
  <https://catlikecoding.com/unity/tutorials/rendering/part-12/>) describes the texture
  as 16 patterns of 4×4 where pattern 0 is empty and the sequence ends with **all pixels
  filled**, with alpha zero meaning "discard". If accurate, slice 15 is all-ones and the
  gate reduces to `_SubpassCutoff <= 1`, which every in-range value satisfies.

**This session treated the secondary source as insufficient** and measured the texture
instead. The claim is load-bearing: if slice 15 contained a zero texel, a fully-opaque
transparent material's shadow would be dithered and conversion would *add* shadow
coverage.

**Measurement** `[MEASURED]` (§3.4). The engine texture is `UnityDitherMask3D`,
`4 × 4 × 16`, format `Alpha8`, `filterMode = Point`, `wrapMode = Repeat`, `mipmapCount = 1`.
It was located among the loaded `Texture3D` objects and sampled through a probe shader
using the same `tex3D(...).a` expression lilToon uses:

| Sample | Result |
|---|---|
| slice 15, all 16 texels, alpha | `1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1` |
| slice 14, all 16 texels, alpha | `0 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1` — **not** all ones |
| **`lilSampleDither(…, alpha = 1)`**, i.e. `z = 1 × 0.9375`, all 16 texels | `1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1` |
| slice 0 (**control**), all 16 texels | `0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0` |
| R channel at every slice (**control**) | `0` everywhere — the data is in alpha only |
| `lilSampleDither(…, alpha = 1 − 1 ulp)` (**control**) | `0 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1` — selects slice 14's pattern |

The slice-0 control proves the probe reads the intended slice, the R-channel control
proves it reads the intended channel, and the monotone fill from slice 0 to slice 15
matches the documented dither ladder. The `1 − 1 ulp` control proves the measurement is
sensitive at exactly the boundary the theorem depends on, and is itself a warning: the
exact-`1` requirement in clause 1 is load-bearing, not cosmetic — one ulp below `1` lands
on slice 14, whose first texel is `0`.

**Scope of the measurement.** The installed texture's `filterMode` is `Point`, and the
exact `lilSampleDither(…, alpha = 1)` expression returned `1` at all 16 positions on
Metal / Gamma. That is the whole of what was observed. No claim is made that the result is
independent of filter mode: slice 14 is *not* all-ones, so a filtering mode that blended
neighbouring slices is not covered by this measurement.

**Result:** `alphaRef = 1` exactly, so the subpass clip is `clip(1 - s)`. By the same
sign-preservation argument as §9.2, the fragment survives iff `s <= 1`.

**Gate:** finite `_SubpassCutoff <= 1` — controller-approved option (a). The two
zero-measurement fallbacks below are **not** shipped; they are retained only as the record
of what would have been sound had the measurement failed.

| Option | Status |
|---|---|
| **(a) Measure** slice 15 | **executed; adopted** — reduces to `_SubpassCutoff <= 1` |
| (b) Gate `_SubpassCutoff <= 0` | not shipped (near-zero coverage; the default is 0.5) |
| (c) Gate `shadowCastingMode == Off` | not shipped |

**Direct behavioural confirmation** `[MEASURED]` (§3.4). Independently of the texel
readback, a rendered comparison was run: a `Hidden/lilToonTransparent` material at its
declared defaults with a fully-opaque `_MainTex` and `_Color.a = 1`, on a shadow-casting
cube lit by a hard-shadow directional light over a Standard receiver, versus **AMUSE's
own** `LilToonOpaqueConversion.PrepareCanonicalOpaqueClone` output for that same material.

| Comparison | Differing pixels (> 1/255) | Max channel difference |
|---|---|---|
| opaque transparent source vs canonical clone | **0** of 65536 | **0.00000000** |
| negative control, same source with `_Color.a = 0.5` | 3954 of 65536 | 0.49514250 |

The clone was produced by the unmodified merged code path, which throws rather than
returns on a read-back disagreement; it accepted a *transparent* source and reported
`lilToon`, queue 2000, `RenderType = Opaque`, `_DstBlend 10 → 0`. That is direct evidence
that the target module is source-independent (§11, §13.4) and that the converted material
is pixel-identical in both the forward and the shadow-receiving result. The negative
control proves the comparison would have detected a difference. `[MEASURED]`

### 9.5 Worked positive example

A fresh Transparent Normal material at its declared defaults — `_Color = (1,1,1,1)`,
`_Cutoff = 0.5`, `_SubpassCutoff = 0.5`, `_AlphaBoostFA = 10`,
`_DistanceFade = (0.1, 0.01, 0, 0)`, `_MainTex_ST = (1,1,0,0)`,
`_MainTex_ScrollRotate = (0,0,0,0)`, every gate in clause 2 at 0 — with a fully-opaque
`_MainTex` at every mip and no animation: `a ≡ 1`, `clip(0.5)` keeps, both premultiplies
are identities, distance fade is the identity, and the blend degenerates. Every colour
pass matches the target exactly, and with slice 15 measured all-ones (§9.4) the shadow
pass matches too. This exact material was rendered against its real canonical clone and
came out bit-identical (§9.4). **`ProvenOpaque`.** `[SOURCE]` derivation, `[MEASURED]`
confirmation.

## 10. Explicit refusal matrix

| Condition | Verdict |
|---|---|
| Shader/pass/GUID/package/format/digest/canonicalization mismatch | Unsupported — attestation fails, renderer batch refuses |
| Scanned `LIL_RENDER != 2` | Unsupported — not this identity |
| `Hidden/lilToonOnePassTransparent` | Unsupported — different shader name; **no FORWARD_ADD in the container** (§4.2) |
| `Hidden/lilToonTwoPassTransparent` | Unsupported — different shader name; adds `FORWARD_BACK` with the `LIL_TRANSPARENT_PRE` arm |
| Any transparent-outline, Lite, Tessellation, Multi, Overlay, Fur, Gem, Refraction, FakeShadow, OutlineOnly, `.lilcontainer` identity | Unsupported |
| Any clause-2 gate nonzero or non-finite | Unsupported feature refusal |
| `_DissolveParams.x != 0` (exact test, stricter than the shader's `round`) or non-finite | Refusal |
| `_Cutoff > 1` or non-finite | Refusal — the source discards or partially discards |
| `_AlphaBoostFA < 1` or non-finite | Refusal — ForwardAdd premultiply is not an identity |
| `_DistanceFade.z != 0` or non-finite | Refusal — post-clip alpha writer and divergent RGB arm |
| `_SubpassCutoff` outside the resolution chosen in §9.4 | Refusal |
| `_Color.a ∈ (0,1)` | Uniform `MustRemainTransparent` (existing multiplier lemma) |
| `_Color.a > 1` or non-finite | `UnsupportedMultiplier` / interpretation refusal |
| Non-identity `_MainTex_ST`, nonzero `_MainTex_ScrollRotate`, UV channel ≠ 0 | `UnsupportedUv` refusal (family gate, §9.3) |
| Trilinear/anisotropic filter, mirrored wrap, `wrapU != wrapV` | `UnsupportedSampling` refusal |
| Format outside the allowlist, non-`Texture2D`, mip streaming/limit gaps, readback failure | Missing texture evidence → refusal, never inferred from readability |
| Any transparent texel intersecting the hull footprint in **any** mip | `MustRemainTransparent` |
| Degenerate triangle, missing/NaN UV, non-finite position | `Unknown` |
| Exact-UV region complexity overflow | `Unknown` |
| Non-singleton / disagreeing animation on any requested property | Slot proof refused |
| Binding on a proof-relevant name outside the request | Renderer batch refused |
| Effective queue ≠ 2460, or effective `RenderType` ≠ `TransparentCutout` | Conversion refusal — custom ordering/classification intent |
| `_ZWrite != 1`, `_ZTest != 4`, `_ColorMask != 15`, nonzero offsets, non-degenerating blend factors/ops | Conversion refusal |
| `_UseDither == 1` | **Admitted** — provably inert on this family (§6 row 16) `[DECISION]` |

Uncertainty stays local: every row above refuses the affected candidate or slot, never a
whole avatar, and no row makes a transformation *more* aggressive.

## 11. Source-to-target preservation analysis

| Question | Answer | Evidence |
|---|---|---|
| Does the official target exist? | Yes — the vendor's Opaque branch maps regular/no-outline/non-Lite/non-Tess/non-Multi to `lilShaderManager.lts` | `[SOURCE]` `lilMaterialUtils.cs:38-60` |
| Does the target preserve pass participation? | **Exactly** — the four referenced pass declarations are byte-identical, and the target container `UsePass`es the same four names | `[MEASURED]` §4.3; `[SOURCE]` `lts.shader:641-644` |
| Is ForwardAdd preserved? | Yes — present on both, identical declaration, identical canonical FA tuple | `[SOURCE]` §4.3, §4.4 |
| Is the canonical recipe the same one AMUSE already implements? | **Yes, exactly.** The vendor Opaque branch writes `_SrcBlend=1, _DstBlend=0, _AlphaToMask=0` (`:63-65`) and the common tail writes `_ZWrite=1` (`:277`), `_ZTest=4` (`:280-282`), `_OffsetFactor=0`, `_OffsetUnits=0`, `_ColorMask=15`, `_SrcBlendAlpha=1`, `_DstBlendAlpha=10`, `_BlendOp=Add`, `_BlendOpAlpha=Add`, `_SrcBlendFA=1`, `_DstBlendFA=1`, `_SrcBlendAlphaFA=0`, `_DstBlendAlphaFA=1`, `_BlendOpFA=Max`, `_BlendOpAlphaFA=Max` (`:283-292`) — the same 18-tuple as `LilToonOpaqueConversion.CanonicalOpaqueTuple` | `[SOURCE]` `lilMaterialUtils.cs`; current tree `LilToonOpaqueConversion.cs:154-174` |
| Does the vendor skip `_ZTest = 4` for this mode? | No — the skip applies only to `transparentMode == TwoPass` (`:280`), a further reason TwoPass is excluded | `[SOURCE]` |
| Does target preparation need shader rewriting or a generated shader? | **No** — clone, swap to the attested `lilToon` asset, write the existing tuple, assign queue 2000 and `RenderType=Opaque`, re-read and validate. `PrepareCanonicalOpaqueClone` is reusable **unchanged** | `[SOURCE]` `LilToonOpaqueConversion.cs:602-700` |
| Does anything about the target change for this source? | No. Target attestation, recipe, clone validation, and the target-identity check are source-independent | `[INFERENCE]` |

**Conclusion.** The target side needs **zero** new work. That is the single strongest
architectural result of this investigation and it directly shapes §13.

## 12. Prerequisites and stop conditions — all closed

Checked against the task's stop-condition list:

| Stop condition | Status |
|---|---|
| A relevant pass produces alpha below one from state AMUSE cannot close | No. All writers are gated, dead, or identities at `a ≡ 1` (§5, §6); the shadow pass's coverage question is measured and closed (§9.4) |
| RGB premultiplication prevents opaque equivalence | No — base is an identity; ForwardAdd is an identity under the `_AlphaBoostFA >= 1` gate (§5.3) |
| ForwardAdd cannot be preserved by the target | No — identical pass declaration, identical canonical FA state (§4.3, §11) |
| Callback 100 can change an alpha-relevant fact after AMUSE's evidence boundary | No — per-feature invariance holds (§8.3) |
| Source/target generated shader identity cannot be pinned | No — both digests measured from an installed package, two sessions, in an install that reproduced all five existing pins (§3.4) |
| The official target changes pass participation materially | No (§4.3) |
| Target preparation would require shader rewriting or generated shaders | No (§11); confirmed behaviourally by running the real clone path (§9.4) |
| Support requires OnePass/TwoPass assumptions | No — both are excluded by exact-name allowlist with a structural counterexample (§4.2) |
| Required evidence needs a new host capability | No (§8) |
| A new generic framework is required first | No (§13) |
| The environment cannot provide a load-bearing measurement | No — the authorized scratch project supplied both (§3.4) |

**No stop condition is active. Both former prerequisites are closed:**

### P1 — transparent source canonical digests `[CLOSED — MEASURED]`

`TransparentShaderCanonicalDigest = ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b457624097f2372ba13`,
`TransparentPassCanonicalDigest = 700a607661f2cc43550452795d8eae0634509dbd07b4e8c381d9412fcc52517f`,
shared `IncludeTreeDigest = 6e2dce6c…8fd46`. Measured with AMUSE's own canonicalizer in a
scratch install that first reproduced all five existing pins; identical across two Editor
sessions. See §3.4.

### P2 — `_DitherMaskLOD` slice 15 / `_SubpassCutoff` `[CLOSED — MEASURED]`

Slice 15 is uniformly alpha `1`, with slice-0, channel, and 1-ulp controls; independently
confirmed by a bit-identical rendered shadow comparison against AMUSE's real clone. Gate:
finite `_SubpassCutoff <= 1`. See §9.4.

## 13. Current architecture pressure

Mapped against the checked-out tree at `a3c547b`.

### 13.1 Reused unchanged — the large majority

| Component | Why unchanged |
|---|---|
| `LilToonOpaqueConversion` **target side**: `CanonicalOpaqueTuple`, `CanonicalOpaqueRenderQueue`, `RenderTypeTagName`, `TryFindNonCanonicalFact`, both `PrepareCanonicalOpaqueClone` overloads, `ReadEffectiveRenderState` | Target-only, source-independent, and the recipe is bit-for-bit the vendor's Opaque tuple for this source too (§11) |
| `AlphaSemanticsResolver`, `TriangleAlphaClassifier`, `ExactUvGeometry`, `AffineUvTransform`, `AlphaMipChain` | Family-blind; the theorem's value shape is `Texture(_MainTex.a)` or `TextureTimesConstant(…)`, identical to cutout |
| `AdmittedMaterialStates` exact-singleton admission, `UnityAnimationEvidenceCapture` closed capture, `MaterialEvidenceRequest.Combine` | Only the request *contents* change |
| `MeshSeparationPlanner`, `AlphaSeparationRecords`, `AlphaSeparationApply`, dedup, curve rewrite, sweep | Operate on `Material → Material`; already family-agnostic |
| `LilToonSourceAttestation` canonicalization, include-tree digest, `TryScanRenderMode`, `Verify`, `Gather` | Profile-parameterized already |
| `MaterialSemantics` / `SemanticOutput` / value types | No new vocabulary; coverage-versus-value remains the documented, unpromoted core gap |

### 13.2 The attestation profile model accepts a third profile cleanly

`LilToonSourceProfile` (`LilToonSourceAttestation.cs:373-400`) is a private record of
`(shaderName, shaderGuid, passShaderName, passShaderGuid, renderMode, shaderDigest,
passDigest)`, with `OpaqueProfile` (`:402-410`) and `CutoutProfile` (`:412-420`) as static
instances and `Verify`/`Gather` taking the profile as a parameter (`:970`, `:1183`,
`:1388`). A `TransparentProfile` is **six new constants plus one static instance plus two
thin wrappers** (`TryVerifyLilToonTransparentIdentity`,
`GatherTransparentSourceEvidence`). **`[INFERENCE]` No refactor, no interface, no
registry.** This directly answers the task's question: yes, the private profile model
accepts a transparent profile cleanly, and it was evidently designed to.

### 13.3 What must be added

| # | Addition | Shape |
|---|---|---|
| A1 | `TransparentProfile` + two wrappers in `LilToonSourceAttestation` | ~40 lines, mirrors the cutout addition exactly |
| A2 | `CapturedAlphaMaterialFamily.LilToonTransparent` + a fourth arm in `ClassifyShaderName`, `AlphaRequestForFamily`, `CaptureRequestForFamily`, `IsAttestedAlphaMaterial`, `AnalyzeAlphaMaterial` | Exact-name allowlist; `UnityMaterialSemantics.cs:244-417`. The type's own doc comment ("with a third family it becomes a third branch, and that is when a registry earns its first honest argument") is about **shader frontends**, and lilToon Transparent is not a third frontend — it is a second identity inside the existing lilToon frontend, just as cutout was. **No registry.** |
| A3 | New `LilToonTransparentMaterialSemantics` (alpha-only frontend + its evidence request) | A sibling of `LilToonCutoutMaterialSemantics`, not a parameterization of it |
| A4 | New transparent **source eligibility** function | See §13.4 |
| A5 | A fourth `case` in `AlphaSeparationPreparation.ConvertAdmittedMaterial`, plus the two family maps (`ConversionRequestForFamily`, `CanonicalPropertiesForFamily`) | `AlphaSeparationPreparation.cs:455-573,703-735` — the file already has one `switch` arm per family; a fourth arm is the established shape |
| A6 | Test seam parity: the existing `VerifiedLilToonConversion` delegate (`AlphaSeparationPreparation.cs:35-45`) returns `out LilToonOpaqueConversionRefusal` and stays explicit and typed | No change if A4 shares the refusal enum (§13.4) |

### 13.4 The `LilToonOpaqueConversion` split `[DECISION]`

`LilToonOpaqueConversion` currently holds three responsibilities: (i) target attestation,
recipe, clone preparation and validation; (ii) the cutout source's eligibility gate set;
(iii) the shared blend/depth/queue lemmas, result types, and Unity enum constants.
Responsibility (i) is proven source-independent (§11); responsibilities (ii) and (iii) are
not target concerns at all.

One concrete symptom of the current conflation: `ConversionEvidenceRequest` carries **19**
properties — the 18 recipe properties **plus `_Cutoff`**, concatenated by
`BuildConversionSchema` (`LilToonOpaqueConversion.cs:199-214,703-712`). `_Cutoff` is never
written by the recipe and is read only by the source clip gate; it is source-eligibility
evidence living in the target's schema. Both alpha requests already capture it
independently (`LilToonCutoutMaterialSemantics.cs:97-126` and the transparent sibling), so
it can move without losing capture or animation closure.

Adding transparent eligibility as a mode branch inside (ii) is **not acceptable** — it is
exactly the "another large mode switch inside cutout-specific eligibility" the task
forbids, and it would put the cutout `0.9999` bound and the transparent `<= 1` bound, and
the queue constants 2450 and 2460, in one function.

The recommended split separates target from source **by ownership**, not by file count:

```
LilToonOpaqueTarget            ← (i) recipe, queue/RenderType constants, ReadEffectiveRenderState,
                                   TryFindNonCanonicalFact, PrepareCanonicalOpaqueClone ×2,
                                   RecipeEvidenceRequest (18 properties, projected from the tuple)
LilToonOpaqueConversionResult  ← (iii) outcome/refusal/eligibility types, the two blend-factor
                                   lemmas, the Unity BlendOp/BlendFactor/Compare/ColorMask constants
LilToonCutoutSourceEligibility      ← (ii)  cutout gates: queue 2450, _Cutoff <= 0.9999,
                                            SourceEvidenceRequest { _Cutoff }
LilToonTransparentSourceEligibility ← (ii') transparent gates: queue 2460, _Cutoff <= 1,
                                            _AlphaBoostFA >= 1, _DistanceFade.z == 0,
                                            the §9.4 subpass condition,
                                            SourceEvidenceRequest { _Cutoff, _AlphaBoostFA,
                                            _SubpassCutoff, _DistanceFade }
```

The target module therefore keeps **no** source predicate, constant, result type, or
request property. The shared support file exists because both source modules consume the
same exact, source-independent predicates and the same Unity enum constants — two real
consumers today. It must remain a constants-and-result-types file; a mode parameter, a gate
table, or a shared `Evaluate*` body in it would be the framework this note refuses.
Duplicating the two four-line predicates instead would also have been acceptable;
duplicating the constant table would not.

Both eligibility functions keep returning the **existing** `LilToonOpaqueConversionOutcome` /
`LilToonOpaqueConversionRefusal` vocabulary (extended with three members:
`UnsupportedForwardAddAlphaBoost`, `UnsupportedDistanceFade`, `UnsupportedSubpassCutoff`),
so the typed test seam and the preparation call sites do not change shape. Preparation's
`ConversionRequestForFamily` returns a per-family
`Combine(RecipeEvidenceRequest, SourceEvidenceRequest)`, which preserves both the derived
evidence subset and the renderer-wide animation-singleton buckets it currently drives
(`AlphaSeparationPreparation.cs:140-142,212-215,476-479`).

This is a **file/namespace split of existing code plus one sibling**, not an abstraction:
no interface, no registry, no `RenderSemantics`, no pass IR, no declarative gate schema,
no conversion framework. Two source families with genuinely different gate sets and one
shared target is precisely the "two producers, one shared concept" evidence bar the
frontend-comparison record requires — and it is the *target* that is shared, which is a
measured fact (§11), not a hoped-for generality. `[DECISION]`

**Second-consumer audit of every abstraction that could be proposed here:**

| Candidate abstraction | Second consumer? | Verdict |
|---|---|---|
| `LilToonOpaqueTarget` extraction | Yes — cutout and transparent, both measured against the same 18-tuple | Extract |
| Shared result types, blend lemmas, Unity enum constants | Yes — both source-eligibility modules | Extract into one small support file, **not** into the target module |
| Declarative gate schema for source eligibility | No — the two gate sets share three predicates and differ in five | Refuse |
| `ISourceEligibility` / registry / adapter | No polymorphic call site; preparation dispatches on a closed enum | Refuse |
| Generic `RenderSemantics` / surface-mode type | No consumer; the two facts needed (queue, `RenderType`) are already read directly | Refuse |
| Pass model / pass IR | No — §4.3 proves the pass declarations are identical, so there is nothing to model | Refuse |
| Coverage-versus-value core concept | Still 2 producers, 0 consumers (unchanged from the frontend record) | Document, do not promote |

## 14. Candidate comparison against neighbouring families

| Family | What this investigation changes |
|---|---|
| **Transparent OnePass** (`ltsot`) | Unblocked *alpha* work — the equation, gates, premultiply, and distance-fade analysis transfer verbatim. Still blocked on its target-identity decision: its container omits `FORWARD_ADD` (§4.2), so `lilToon` would **add** additive lighting. F0 §7.5's `[DECISION NEEDED]` stands, now with the extra fact that the *pass asset* declares FORWARD_ADD identically — the divergence is purely the container's `UsePass` list, so no official no-additive opaque target exists. |
| **Transparent TwoPass** (`ltstt`) | Confirmed refusal-leaning. It adds `FORWARD_BACK`, whose fragment takes the `LIL_TRANSPARENT_PRE` arm — `fd.col *= _PreColor; clip(a - _PreCutoff); if(_PreOutType) return …` (`forward:405-410`) — an entirely separate colour and coverage equation, plus the vendor's `_ZTest` exception. Nothing here reduces that work. |
| **Overlay** (`ltsover`) | Normal-class: consumes `Hidden/ltspass_transparent` FORWARD + FORWARD_ADD at queue 2460, so this theorem's fragment analysis applies directly. Remains gated on F0 §13.4's product decision about whether overlays are separable base surfaces. |
| **Lite** (`ltslt`) | Not advanced. Lite has its own pass assets and its own `lil_pass_forward_lite.hlsl` alpha structure (`:131-139`, `:173-181`), separate digests, and a reduced feature set. The *shape* of the transparent theorem transfers; no attestation or equation does. |
| **Outline transparent** (`ltsto`) | Not advanced. Still needs the outline target attestation, the outline recipe extension, seam characterization, and an outline-alpha theorem (F0 §7.3, §7.7) — none of which this note touches. |
| **Tessellation transparent** (`ltstesst`) | Not advanced; the displacement-equality and hull-adjacency questions are orthogonal. |
| **Multi** (`ltsm`) | Not advanced. Mode is keyword-carried on a single asset, a different attestation shape entirely. |
| **Cutout** (`ltsc`, shipped) | Unchanged. This note adds one observation about it: the cutout evidence request contains `_UseDither`, correctly, because dither *is* compiled for `LIL_RENDER 1`. |

**Why Transparent Normal was the right next candidate:** it is the only remaining family
whose official target requires **no new attestation, no new recipe, and no new
preparation code** (§11), so it isolates the alpha-semantics question from every other
variable. That isolation is what made §4.3's byte-identical-pass result decisive.

## 15. Decisions — resolved

All five open decisions were resolved by the controller on 2026-09-01; nothing in this
note is now blocking.

1. **P2 resolution** (§9.4): **option (a), measure.** Executed. Gate is finite
   `_SubpassCutoff <= 1`. Fallbacks (b) and (c) are not shipped. `[DECISION]`
2. **P1 sequencing** (§3.4): **one combined scratch probe, authorized and executed.**
   Both digests are measured, not pre-registered. `[DECISION]`
3. **Cutoff bound** (§9.2): **family-specific `<= 1` adopted**, after the proof was
   repaired from an exact-representability claim to a sign-preservation argument.
   `[DECISION]`
4. **`_UseDither`** (§6 row 16): **omitted from the transparent request** — the pinned
   `LIL_RENDER 2` source compiles the path out. The positive `_UseDither = 1` falsifier is
   retained. `[DECISION]`
5. **Module split** (§13.4): **approved**, subject to the ownership repair recorded in
   §13.4 — the target module keeps no source predicate, constant, result type, or request
   property, and `_Cutoff` moves out of the target's schema. `[DECISION]`
6. Carried forward, unchanged and not blocking: F0 §13's four open decisions (OnePass
   target, refraction commissioning, `.lilcontainer` policy, overlay scope).

## 16. What this investigation proves and does not prove

**Proves.**

- The exact pass topology and identity of Transparent Normal, and the structural
  counterexamples that separate it from OnePass and TwoPass (§4.2).
- That `ltspass_transparent` and `ltspass_opaque` differ in exactly seven places, none of
  which touches the four referenced pass declarations or the compiled feature set
  (§4.3) — so conversion preserves pass participation exactly.
- The complete `LIL_RENDER 2` alpha and RGB dataflow across `FORWARD`, `FORWARD_ADD`,
  `SHADOW_CASTER`, and `META`, with every mechanism classified and its neutralizing gate
  named (§5, §6).
- That depth-fade-to-alpha is dead code in 2.3.4 (§5.5).
- That three transparent-only writers exist which have no cutout analogue and which a
  copied cutout gate list would miss: ForwardAdd premultiply, distance fade, and the
  subpass shadow clip (§5.3, §5.4, §5.6).
- That the cutoff bound for this family is `<= 1`, not `<= 0.9999` (§9.2), by
  sign preservation rather than exact representability.
- That the colour-pass output at `a ≡ 1` is arithmetically equal to the target's (§9.1).
- That `_DitherMaskLOD` slice 15 is uniformly alpha 1, so the `SHADOW_CASTER` subpass
  clip reduces to `_SubpassCutoff <= 1` (§9.4) `[MEASURED]`.
- That the two transparent canonical digests are `ea247d3c…2ba13` and `700a6076…52517f`,
  stable across two sessions, measured by AMUSE's own algorithm in an install that
  reproduced all five existing pins (§3.4) `[MEASURED]`.
- That a fully-opaque Transparent Normal material and AMUSE's canonical clone of it
  render bit-identically, shadows included (§9.4) `[MEASURED]`.
- That the official target, its recipe, and its preparation code are reusable unchanged
  (§11), and that the attestation profile model takes a third profile without refactor
  (§13.2).
- Callback-100 invariance for this family (§8.3).

**Does not prove.**

- Anything about OnePass, TwoPass, outline, Lite, Tessellation, Multi, Overlay, or any
  refused family.
- Non-identity `_MainTex_ST`, wider sampling vocabularies, UDIM/IDMask-aware proofs,
  dissolve/alpha-mask-active proofs, or constant-alpha-below-one margins.
- Any behavior of the regenerated uploaded shader artifact (argued invariant, not
  digest-attested).
- Behaviour on graphics APIs other than Metal, in Linear color space, or under a dither
  sampler filter mode other than the installed `Point`. The §9.4 measurement covered one
  configuration. The dither texture is an engine asset with fixed contents and the
  `Alpha8` values measured are exact `0`/`1`, so no colour-space or API dependence is
  expected — but that expectation is `[INFERENCE]`. Filter independence specifically is
  **not** established: slice 14 is `0 1 1 1 …`, not all-ones.

## 17. Verdict and next recommendation

**Verdict: GO — the positive contract holds for every relevant pass, and both
prerequisites are discharged by measurement (§3.4, §9.4).**

The transformation is sound and unusually cheap for `FORWARD`, `FORWARD_ADD`, and
`META`; the target side needs no behavioral work at all; `SHADOW_CASTER` is now measured
rather than assumed; and the architecture takes one sibling module, one small shared
support file, and one enum member.

The strongest single piece of evidence is not any one digest: it is that AMUSE's
**unmodified** clone path, given a defaults-plus-opaque-texture Transparent Normal
material, produced a canonical `lilToon` clone whose rendered output — forward shading and
cast shadow together — was bit-identical over 65536 pixels, while a `_Color.a = 0.5`
control diverged on 3954 of them (§9.4).

**Exact next recommendation, in dependency order:**

1. Controller reviews the companion design
   (`docs/superpowers/specs/2026-09-01-liltoon-transparent-normal-alpha-separation-design.md`),
   now carrying the measured constants and the repaired module ownership.
2. On acceptance, write the implementation plan. No production branch until the plan is
   approved.
3. Implement with the §14 falsifier matrix as the RED/GREEN suite.

No other lilToon row should start first: OnePass depends on this theorem plus its own
target decision, Overlay depends on this theorem plus a product decision, and Lite
transparent depends on both.

## 18. Citations

All lilToon paths are relative to `Assets/lilToon/` in
`https://github.com/lilxyzw/lilToon` at tag `2.3.4`, commit
`252fd8cfc46106d4967e95b3f2c788418502f227`. Digests in §3.2.

**Shader assets** — `Shader/lts_trans.shader:1,19,21,22,24,32,36,44-46,54,85,116,421,429-433,467,476-504,508-510,570,578-604,671-677`;
`Shader/ltspass_transparent.shader:578-611,636-668,672,676-778,792-840,831,842-889,890-941,943-990,991-1041,1043-1082,1083-1122,1124-1140`;
`Shader/lts.shader:638-644`; `Shader/ltspass_opaque.shader:578-611,639`;
`Shader/lts_cutout.shader`; `Shader/lts_onetrans.shader:674-676`;
`Shader/lts_twotrans.shader:674-678`.

**Includes** — `Shader/Includes/lil_pass_forward_normal.hlsl:154-157,194-217,221-223,227-244,260,264-265,271-273,278-279,333-335,344-390,394-421,424-432,502,581-582,592-594,604`;
`lil_common_frag.hlsl:257-271,294-305,311-343,353-358,465-476,483-519,524-549,554-560,638-711,718-720,793-807,889-903,1452-1454,1551-1553,1619-1621,1682-1688,1717-1719,1788-1790,1862-1864,1946-1948,1968-1976,1980-2009,2014-2059`;
`lil_common_frag_alpha.hlsl:1-117`; `lil_common_macro.hlsl:16,316-319,394-399,978-983,1865-1871`;
`lil_common_functions.hlsl:424-435,444-460,467-470`; `lil_common_vert.hlsl:55-56,70-84,362-411,414,426`;
`lil_pass_shadowcaster.hlsl:55-70`; `lil_pass_meta.hlsl:51-102`;
`lil_pass_forward_lite.hlsl:131-139,173-181`.

**Vendor editor code** — `Editor/lilMaterialUtils.cs:18-70,120-200,260-292`.

**AMUSE current tree at `a3c547b`** —
`Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:11-17,244-417`;
`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:323-364,373-420,929,970,1154-1173,1183,1344-1392`;
`Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs:24-142,211-366`;
`Editor/Semantics/LilToon/LilToonOpaqueConversion.cs:22-106,124,136-238,263-432,434-463,485-547,602-700`;
`Editor/Analysis/AlphaSemanticsResolver.cs:304-452`;
`Editor/Build/AlphaSeparationPreparation.cs:20-45,114-138,455-573,703-735`.

**Merged AMUSE documents** —
`docs/superpowers/investigations/2026-08-30-liltoon-opaque-characterization.md` (B1) §§3-10;
`docs/superpowers/investigations/2026-08-30-liltoon-cutout-alpha-semantics.md` (B2) §§2-16;
`docs/superpowers/investigations/2026-08-30-liltoon-family-applicability.md` (F0) §§4-16;
`docs/superpowers/specs/2026-08-30-liltoon-cutout-opaque-conversion-design.md`;
`docs/superpowers/specs/2026-08-31-affine-maintex-st-support-design.md` §3.1, §5-G, C6, §11;
`docs/superpowers/plans/2026-08-31-affine-maintex-st-support.md`;
`docs/architecture/shader-frontend-comparison.md`.

**External** —
Unity built-in shaders, `CGIncludes/UnityStandardParticleShadow.cginc`
(<https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/CGIncludes/UnityStandardParticleShadow.cginc>) —
sampling convention only;
Catlike Coding, *Rendering 12: Semitransparent Shadows*
(<https://catlikecoding.com/unity/tutorials/rendering/part-12/>) — **secondary**, cited in
§9.4 and explicitly rejected as sufficient evidence.

**Scratch probe artifacts** (§3.4; outside the repository, deleted after recording) —
official vendor package `jp.lilxyzw.liltoon-2.3.4.zip`
(<https://github.com/lilxyzw/lilToon/releases/download/2.3.4/jp.lilxyzw.liltoon-2.3.4.zip>),
SHA-256 `34d172761c51aa9469a904704109086aafa6125a4fa0e058766e2ddc73d3b303`;
vendor VPM index <https://lilxyzw.github.io/vpm-repos/vpm.json>;
NDMF `1.14.8` was fetched during an abandoned intermediate approach and **removed** before
the measurements were taken — no NDMF code participated in any recorded result.
The digest measurement was driven by a byte-identical copy of
`LilToonSourceAttestation.cs` (SHA-256
`70140d4da04ecf27d852d2519cfa88129a59a2e5ba839ef029dc585ed5783ed5`), not by a
reimplementation.

## 19. Privacy statement

Census Lab and private avatar data were not used, inspected, referenced, or modified. No
private names, paths, GUIDs, per-avatar rows, or fingerprint-like identifiers appear in
this document. Every GUID cited is a public lilToon package asset GUID.

No Unity MCP call was issued in this session. Two existing Unity instances were
enumerated read-only and never targeted. All Unity work
ran in a throwaway project created outside AMUSE under the OS temporary directory,
addressed by an explicit `-projectPath`, with `Application.dataPath` asserted in every
probe's own output. lilToon was **not** installed into AMUSE, and neither AMUSE project
was written to.

No vendor source was vendored into the repository; the read-only clone and the scratch
project both live outside AMUSE and the scratch project was deleted after its results were
recorded here.
