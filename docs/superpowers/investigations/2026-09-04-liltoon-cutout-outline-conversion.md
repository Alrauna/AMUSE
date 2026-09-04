# T2 — lilToon 2.3.4 regular Cutout Outline alpha separation

## 1. Question and bounded scope

> Can AMUSE safely support triangle-level alpha separation for exactly
> `Hidden/lilToonCutoutOutline` (regular cutout + outline), moving triangles
> proven visually opaque onto an appended submesh whose material targets
> `Hidden/lilToonOutline` — the vendor's own outline-capable opaque target?

This note is roadmap row 3 of the merged family inventory
(`2026-08-30-liltoon-family-applicability.md` §11, cited as **F0**). F0 §7.3
classified the identity **C — conditional**. F0 §7.3 named four missing proofs:

1. Target attestation for a second target asset.
2. An outline-alpha theorem or gate.
3. The outline recipe delta.
4. Seam and draw-order characterization.

This note discharges all four with pinned-source reading and a scratch-editor
measurement. **This note designed and implemented no production behavior.**

**In scope — exactly one candidate:**

| Axis | Value |
|---|---|
| Package | `jp.lilxyzw.liltoon` `2.3.4` |
| Upstream pin | tag `2.3.4`, commit `252fd8cfc46106d4967e95b3f2c788418502f227` |
| Source shader | `Hidden/lilToonCutoutOutline` (`Shader/lts_cutout_o.shader`) |
| Target shader | `Hidden/lilToonOutline` (`Shader/lts_o.shader`) — a **second** target asset |
| Pipeline | regular — non-Lite, non-Tessellation, non-Multi |
| Source queue / `RenderType` | exactly 2450 / `TransparentCutout` |
| Target queue / `RenderType` | 2000 / `Opaque`, same as the first slice |

**Out of scope and refused.** The three transparent-outline variants (F0 §7.7,
strictly downstream). The three outline-only variants (F0 §7.8, refusal
candidates, no base surface exists). Lite and Tessellation outline. Multi.
Overlay. Refraction. Fur. Gem. FakeShadow. Poiyomi's `OutlinesEnabled` gate.
`.lilcontainer` identities. UV channels other than 0. Trilinear, anisotropic,
mirrored, and asymmetric sampling. Any generalized render-state IR, target
registry, adapter framework, planner, or third-party API.

**Verdict: GO-WITH-CONDITIONS.** Every one of the four proofs resolved
positively in this investigation. The conditions are production prerequisites
for the successor design branch, not open questions. They are listed in §12.

**Labels.** `[SOURCE]` marks a pinned upstream file at the commit above, or a
file of the checked-out AMUSE tree, cited by path and line. `[MEASURED]` marks
an executed observation of this session. `[INFERENCE]` marks a bounded
conclusion from those facts. `[DECISION]` marks a choice this note makes.
`[DECISION NEEDED]` marks a choice the controller must make.

**Privacy.** No Census Lab data and no private avatar data was used, inspected,
or modified. §15 records the full statement.

## 2. Repository and base state

| Fact | Value | Evidence |
|---|---|---|
| Base | `main` at `30cbfc2599a124d6a5854d03ebb0dfd4b2fe9c04` | `[MEASURED]` `git rev-parse HEAD` on `main` |
| PR #45 | **MERGED** — "transparent Normal" family. `main` has moved past `4cd73c0`. `30cbfc2` is the merge commit of PR #45 | `[MEASURED]` `git log --oneline -5` |
| Branch created | `investigate/liltoon-cutout-outline-conversion` from `30cbfc2` | `[MEASURED]` `git switch -c` |
| Pre-existing user-owned churn | `Packages/manifest.json` and `Packages/packages-lock.json`, modified before this session. Untouched, unstaged | `[MEASURED]` `git status --porcelain` |
| Staged content | none | `[MEASURED]` `git status` |
| End state of this run | the note added, the working tree otherwise as above. Nothing staged, committed, pushed, or merged | `[MEASURED]` final `git status` and `git diff --check` (§11) |

**Review provenance.** On 2026-09-04 the controller independently reproduced
this note's load-bearing claims against the pin and the current tree: all
eight source digests, the 4-hunk pass-asset diff, the seven byte-identical
pass declarations, the GUIDs, the alpha-chain line citations, the vendor
tail, the defaults, and all four expressibility citations. The review found
the note accurate. This amendment records that review, closes the one gap it
found (§5 rows 13-20, §9), and records the controller's three decisions
(§9). `[MEASURED]` `[DECISION]`

## 3. Source pin and method

### 3.1 Vendor pin

The investigation cloned the official repository **read-only** into a temporary
directory outside AMUSE. The directory is not a Unity project. Nothing was
installed into AMUSE.

- `git rev-parse HEAD` → `252fd8cfc46106d4967e95b3f2c788418502f227` `[MEASURED]`
- `git describe --tags --exact-match` → `2.3.4` `[MEASURED]`
- All `[SOURCE]` paths are relative to `Assets/lilToon/` at that commit. Line
  numbers are for the LF-normalized file. The cited shader assets are already
  LF in the tree. `[MEASURED]` (byte comparison below)

### 3.2 Digests of the cited source (SHA-256, raw file bytes)

| Path | SHA-256 |
|---|---|
| `Shader/lts_cutout_o.shader` | `9fa9e7e7be55d29851fe4dd5cf2078e259a0b439dd1b61075a7c0448c176a9ec` |
| `Shader/lts_o.shader` | `bbd886afd367d73ba3e2208aa42086e9149ccf826564ce4bb4f571d16861aa36` |
| `Shader/ltspass_cutout.shader` | `63e4dca74c4caeb714a1d4bf5cab23babd3fad9d42d4a7bb787dd415cea435e4` |
| `Shader/ltspass_opaque.shader` | `fe32b23b3b69d9d74a184c422e196a4e340e286ee7105697b5021695b9d61725` |
| `Editor/lilMaterialUtils.cs` | `0f95ca692bb4dafb772533ce6727ff83626d58acdbc1ef181f5d7e51f586e8ac` |
| `Shader/Includes/lil_common_frag.hlsl` | `96b1bbfecc32d16735db16b5a0c46db3bf81c8f28b9d247c3394ae3c6af84dc1` |
| `Shader/Includes/lil_pass_forward_normal.hlsl` | `f7a7c2900444d3f9b16fdf78e1ebe0d7765282f36cc3256d9cc7d506d3b54a47` |
| `Shader/Includes/lil_common_vert.hlsl` | `19c7764d77ad29f14f62b3e4e7458f6c30b9e518cc875d86354dacb82560c6ed` |

All rows `[MEASURED]` (`shasum -a 256` on the pinned clone). The last three
hashes equal the values in T1 §3.2, an independent cross-check of the pin.
`[MEASURED]`

### 3.3 Scratch probe `[MEASURED]`

Following the T1 §3.4 precedent, under the authorization of this run:

| Fact | Value |
|---|---|
| Editor | Unity `2022.3.22f1`, macOS, Metal, Apple M2, **Gamma** color space, batch mode |
| Project | a throwaway project under the OS temporary directory, **outside** AMUSE. Every probe asserted its own `Application.dataPath` in its output and aborted on any mismatch |
| Vendor package | official `jp.lilxyzw.liltoon-2.3.4.zip` from the vendor's GitHub release, SHA-256 `34d172761c51aa9469a904704109086aafa6125a4fa0e058766e2ddc73d3b303` — equals the archive hash recorded by B1 §3 and T1 §3.4 — embedded under `Packages/jp.lilxyzw.liltoon` |
| Digest algorithm | a **byte-identical copy** of AMUSE's `LilToonSourceAttestation.cs` (SHA-256 `06d1f2815df891ca4a0b2209b2b58ecea370e530e0a2c3f38cf1a383fc08d19b`, the current merged file), driven directly in the scratch project. Not a reimplementation |
| Fidelity gate | the probe first reproduced **all five** merged AMUSE pins through the real wrapper path. Five-for-five before any new value was read (§4.1.3) |
| Sessions | every measurement repeated in a second, independent Editor process. The two digest reports diffed **identical**. The two seam reports diffed **identical** |
| Teardown | the scratch project was deleted after recording. Nothing entered AMUSE |

One probe-support file was not a product file: a minimal evidence-container
shim (11 lines of members) replaced `CapturedMaterialEvidence`, because the
real capture type pulls the whole `MaterialSemantics` core into the scratch
assembly. The shim reproduces the exact member semantics the `Gather` path
reads: ordinal-name `TryGetScalar`, and the `HasShaderName`/`ShaderName`
passthrough. The measured code — `LilToonSourceAttestation.cs` — is
byte-identical with the merged product file. `[MEASURED]`

Two private members of the attestation type were driven through reflection:
the private `Gather`/`Verify` pair and the private 7-field
`LilToonSourceProfile`. This let the outline identities run the unmodified
conjunction machinery with their own profile (§4.1.4). `[MEASURED]`

### 3.4 Method

1. Closed enumeration of the source container, the target container, and both
   pass assets. Internal names, `.meta` GUIDs, tags, `UsePass` lists,
   `LIL_RENDER` defines, and property defaults.
2. **Whole-file diff of `ltspass_opaque.shader` against
   `ltspass_cutout.shader`** — the decisive structural experiment (§4.1.2),
   repeating the T1 §4.3 method on the outline-bearing pair.
3. Linear walk of the outline fragment branch
   (`lil_pass_forward_normal.hlsl:173-260`), the outline macros of
   `lil_common_frag.hlsl`, the shadow-caster chain with `LIL_OUTLINE` defined
   (`lil_common_frag_alpha.hlsl`), and the vertex outline path
   (`lil_common_vert.hlsl`, `lil_vert_outline.hlsl`,
   `lil_common_functions.hlsl:272-318`).
4. Exhaustive grep of the outline feature defines in both pass assets.
5. Reconciliation against the vendor mode map (`Editor/lilMaterialUtils.cs`,
   `SetupMaterialWithRenderingMode`, `:18-330`).
6. The scratch probe of §3.3: digests, recipe matrices, sentinel
   preservation, a vendor-path conversion run, and the rendered seam
   comparisons of §7.
7. Read-only scouts over the current AMUSE tree for the five seams this row
   stresses. Their findings are folded into §4 and §9 with citations.
8. Web research on neighbouring optimizers (§13). Pinned source outranks
   prose throughout.

## 4. The four proofs

### 4.1 Proof 1 — target attestation for a second target asset

#### 4.1.1 Identities

| Role | Shader name | Asset | GUID | Declared tags |
|---|---|---|---|---|
| Source container | `Hidden/lilToonCutoutOutline` | `Shader/lts_cutout_o.shader:1` | `3b4aa19949601f046a20ca8bdaee929f` | `RenderType=TransparentCutout`, `Queue=AlphaTest` (2450) — `:640` |
| Source pass asset | `Hidden/ltspass_cutout` | `Shader/ltspass_cutout.shader` | `ad219df2a46e841488aee6a013e84e36` | `#define LIL_RENDER 1` — `:639` |
| Target container | `Hidden/lilToonOutline` | `Shader/lts_o.shader:1` | `efa77a80ca0344749b4f19fdd5891cbe` | `RenderType=Opaque`, `Queue=Geometry` (2000) — `:640` |
| Target pass asset | `Hidden/ltspass_opaque` | `Shader/ltspass_opaque.shader` | `61b4f98a5d78b4a4a9d89180fac793fc` | `#define LIL_RENDER 0` — `:639` |

GUIDs `[MEASURED]` twice: from the `.meta` files of the pinned clone, and from
the installed package via `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` in
the probe. The source pass and target pass rows reproduce the merged AMUSE
pins exactly (`LilToonSourceAttestation.cs:355-357`, `:326-328`). `[SOURCE]`

Format stamp: `_lilToonVersion ("Version", Int) = 45` on both containers
(`lts_cutout_o.shader:570`, `lts_o.shader:570`), the value AMUSE already pins
(`ShaderFormatVersion = 45f`, `LilToonSourceAttestation.cs:331`). `[SOURCE]`

The vendor's own mode map selects `lts_o` for an outline material dispatched
to Opaque (`lilMaterialUtils.cs:58-62`, field `ltso` bound to
`Shader.Find("Hidden/lilToonOutline")` at `lilShaderManager.cs:14`), and
selects `ltsco` for Cutout+outline (`lilMaterialUtils.cs:91-95`). Converting a
cutout-outline source to plain `lilToon` would select the vendor's *no-outline*
asset and silently drop three outline passes. F0 §7.3 called this a false
positive. Confirmed at source. `[SOURCE]`

#### 4.1.2 Pass topology and the decisive structural result

Both outline containers `UsePass` the same six names from their respective
pass assets (`lts_cutout_o.shader:641-646`, `lts_o.shader:641-646`):
`FORWARD`, `FORWARD_OUTLINE`, `FORWARD_ADD`, `FORWARD_ADD_OUTLINE`,
`SHADOW_CASTER_OUTLINE`, `META`. Note the shadow-caster substitution: the
outline containers use `SHADOW_CASTER_OUTLINE` and declare no plain
`SHADOW_CASTER`. This is the "SHADOW_CASTER_OUTLINE divergence" F0 §7.3(a)
named. `[SOURCE]`

A whole-file diff of `ltspass_opaque.shader` against `ltspass_cutout.shader`
produces **exactly 4 hunks** `[MEASURED]`:

1. `Shader "…"` name (`:1`).
2. `_AlphaToMask` default `0` → `1` (`:604`).
3. `_OutlineAlphaToMask` default `0` → `1` (`:635`).
4. `#define LIL_RENDER 0` → `1` (`:639`).

**All seven pass declarations are byte-identical between the two assets** —
`FORWARD`, `FORWARD_OUTLINE`, `FORWARD_ADD`, `FORWARD_ADD_OUTLINE`,
`SHADOW_CASTER`, `SHADOW_CASTER_OUTLINE`, `META`, including every stencil,
`Cull`, `ZClip`, `ZWrite`, `ZTest`, `ColorMask`, `Offset`, `BlendOp`, `Blend`,
and `AlphaToMask` expression, and every `#pragma`. `[MEASURED]` `[SOURCE]`

Consequences, all `[INFERENCE]` from `[SOURCE]`:

- Source and target participate in **exactly the same six light modes**.
  Conversion neither adds nor removes a pass.
- Every render-state difference lives in **material property values**, never
  in pass declarations. Captured scalars plus effective queue plus
  `RenderType` describe the whole delta. The evidence model of AMUSE needs no
  pass model for this row.
- The single constant `LIL_RENDER` carries every behavioral difference, and
  AMUSE already scans it from the live pass (`TryScanRenderMode`,
  `LilToonSourceAttestation.cs:960-996`).

Outline-pass state is property-driven (`ltspass_opaque.shader:813-831`,
`:914-932`), with two hard exceptions in `FORWARD_ADD_OUTLINE`: `ZWrite Off`
and `ZTest LEqual` are literals there (`:926-927`), identical on both pass
assets by the diff. `SHADOW_CASTER_OUTLINE` is byte-identical to
`SHADOW_CASTER` except `#define LIL_OUTLINE` (`:1001-1039` vs `:961-998`), and
it uses the **base** stencil and base `Cull [_Cull]`, not the `_Outline*`
state. `[SOURCE]`

#### 4.1.3 Measured canonical digests `[MEASURED]`

The probe reproduced the five merged pins through the byte-identical
attestation copy before reading any new value. Five-for-five:

| Pinned constant | Reproduced |
|---|---|
| `ShaderCanonicalDigest` `5206bec2…9c704` | yes |
| `PassCanonicalDigest` `6b6c30c1…5eb14` | yes |
| `IncludeTreeDigest` `6e2dce6c…8fd46` | yes |
| `CutoutShaderCanonicalDigest` `c83d73a2…836178` | yes |
| `CutoutPassCanonicalDigest` `ecd1caed…1bfe92` | yes |

Then it measured the two new containers with the exact canonicalization and
hash path `Gather` uses (`AnalyzeCanonicalization` at
`LilToonSourceAttestation.cs:1488-1492`). Both containers reported
`removedRegions = 0` and `activators = 0`, and each canonical digest equals
the raw upstream tag-tree hash of §3.2:

| Constant this row needs | Measured value |
|---|---|
| `CutoutOutlineShaderCanonicalDigest` | `9fa9e7e7be55d29851fe4dd5cf2078e259a0b439dd1b61075a7c0448c176a9ec` |
| `OutlineShaderCanonicalDigest` | `bbd886afd367d73ba3e2208aa42086e9149ccf826564ce4bb4f571d16861aa36` |
| Include tree (shared, already pinned) | `6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46` |

Both reports were identical across two independent Editor sessions.
`[MEASURED]`

**The pass digests need no new pins at all.** The reflected `Gather` run
returned, for the cutout-outline source, `passDigest = ecd1caed…1bfe92` —
exactly the merged `CutoutPassCanonicalDigest`. For the outline target it
returned `passDigest = 6b6c30c1…5eb14` — exactly the merged
`PassCanonicalDigest`. `[MEASURED]` The outline identities reuse the two pass
assets AMUSE already attests.

#### 4.1.4 The attestation model accepts both identities

With the measured digests supplied as profile fields, the private `Verify`
conjunction accepted both identities: `reflectedVerify ok=True` for the source
(name, GUID, format 45, package, pass GUID `ad219df2…`, provenance gate, three
digests, `LIL_RENDER 1`) and for the target (pass GUID `61b4f98a…`,
`LIL_RENDER 0`). `[MEASURED]`

Cost of the two new profiles in `LilToonSourceAttestation.cs`, following the
Transparent precedent (`:363-383` consts, `:443-451` instance, `:1216-1222`
and `:1420-1426` wrappers): **7 consts + 1 static instance + 2 wrappers per
profile, roughly 30 to 40 lines each.** No refactor. The two profile-shared
hardcodes do not bind: the provenance gate's two-`HLSLINCLUDE` shape and the
single-pass resolution in `Gather` (`:1499`) already pass for both pass
assets, because both outline profiles reference the **already-attested** pass
assets. `[SOURCE]` (scout S2, current tree) `[INFERENCE]`

Target attestation is not yet a distinct concept in AMUSE.
`GatherOpaqueTargetSourceEvidence` (`:1433-1439`) reuses `OpaqueProfile` with
a live shader-name override, and `PrepareCanonicalOpaqueClone` resolves the
target by the single name constant (`LilToonOpaqueTarget.cs:318`). The
mechanism takes any live `Shader` and any profile — the `shaderNameOverride`
parameter exists for exactly this (`:1451`) — but the outline target needs its
own constants and profile instance. §9 records the pressure. `[SOURCE]`

**Proof 1 verdict: satisfied.** The target is attestable with the existing
attestation model. Two container digests measured. Zero new pass digests.
Stop condition 1 did not trigger.

### 4.2 Proof 2 — an outline-alpha theorem or gate

#### 4.2.1 The outline alpha dataflow

The outline fragment branch is separate code
(`lil_pass_forward_normal.hlsl:173-260`, behind `LIL_OUTLINE`). In execution
order:

| # | Step | Site | Alpha effect |
|---|---|---|---|
| 1 | Outline UV animation | `:176-177` → `lil_common_frag.hlsl:276-284` | `fd.uvMain = lilCalcUV(fd.uv0, _OutlineTex_ST, _OutlineTex_ScrollRotate)` — under `LIL_FEATURE_ANIMATE_OUTLINE_UV`, which **is defined** in both pass assets `[MEASURED]`. The outline sample domain is its own `ST`/scroll-rotate mapping on uv0 |
| 2 | Outline color | `:189-190` → `lil_common_frag.hlsl:394-399` | `fd.col = sample(_OutlineTex, sampler_OutlineTex, fd.uvMain)` (`:363-364`). Tone correction is RGB-only (`:369-374`). Then `fd.col.a *= _OutlineColor.a` (`:389`). The lit-factor term `_OutlineLitColor.a` (`:386`) is **RGB-only** (`:388`) |
| 3 | Alpha mask | `:194-197` | `LIL_FEATURE_ALPHAMASK && LIL_RENDER != 0`, runtime gate `_AlphaMaskMode` — the **same** writer as the base branch, applied to the outline alpha |
| 4 | Dissolve | `:201-216` | same wrapper as the base branch, driven by `fd.dissolveActive`/`dissolveInvert` from the vertex IDMask path |
| 5 | Dither | `:220-223` | `LIL_FEATURE_DITHER && LIL_RENDER == 1` — compiled for this source. `OVERRIDE_DITHER` (`lil_common_frag.hlsl:524-546`) replaces alpha with a distance-fade term and a binary `_DitherTex` threshold. Note: the outline branch does **not** wrap its cutoff transform in `if(!_UseDither)` (contrast the base branch, B2 §3.3.9) — dither composes before the transform here |
| 6 | The `LIL_RENDER` branch | `:227-237` | `LIL_RENDER 0`: `fd.col.a = 1.0` (`:229`) — **forced opaque**. `LIL_RENDER 1`: the cutout coverage transform `saturate((a − _Cutoff)/max(fwidth(a), 0.0001) + 0.5)` plus `if(a == 0) discard` (`:232-233`) — **on the outline alpha** |

So the outline chain alpha of the source is
`_OutlineTex.a × _OutlineColor.a`, modified by the shared alpha-mask,
dissolve, and dither writers, then cutout-transformed. `[SOURCE]`

The shadow caster repeats the trap. `SHADOW_CASTER_OUTLINE` runs
`lil_common_frag_alpha.hlsl` with `LIL_OUTLINE` defined: the alpha source is
`OVERRIDE_ANIMATE_OUTLINE_UV` plus `OVERRIDE_OUTLINE_COLOR`
(`lil_common_frag_alpha.hlsl:17-19`, `:27-29`) — the outline texture and
`_OutlineColor.a` again — then alphamask (`:58-61`), dissolve (`:65-81`),
dither (`:92-95`), then `clip(fd.col.a - _Cutoff)` (`:99`). For the target,
the whole `LIL_RENDER > 0` block compiles out (`:12`, `:116`): **the outline
target's `SHADOW_CASTER_OUTLINE` performs no alpha work and never discards.**
`[SOURCE]`

**The trap is real, exactly as F0 §7.3(d) stated.** A triangle whose base
texture is fully opaque can carry an outline whose alpha clips in the source
color pass, fades under MSAA coverage, or clips in the shadow caster. The
target forces that alpha to 1.0 in all three passes. The conversion would
solidify clipped or faded outline fragments. This investigation confirmed the
trap behaviorally (C2 control, §7). `[SOURCE]` `[MEASURED]`

#### 4.2.2 What the outline proof must establish

The outline proof is a **second coverage conjunction over the same triangle**,
structurally parallel to the base theorem of B2 §5, on `_OutlineTex`:

1. Outline texture coverage: `_OutlineTex` is an assigned `Texture2D` with the
   admitted format, full mip residency, Point or Bilinear filter, equal
   Clamp/Repeat wrap, and a measured per-mip alpha field. For every mip, the
   exact classifier over the uv0 hull of the triangle — with footprint and
   wrap arithmetic — finds every intersecting texel alpha `== 255`.
2. Outline multiplier: `_OutlineColor.a == 1` exactly (`lil_common_frag.hlsl:389`).
3. Outline UV identity: `_OutlineTex_ST == (1,1,0,0)` and
   `_OutlineTex_ScrollRotate == (0,0,0,0)` per binary32 component
   (`lil_common_frag.hlsl:276-284`). **These are outline-specific gates, not
   the base `_MainTex_ST` gates.** The scroll-rotate term compiles in on this
   family (`LIL_FEATURE_ANIMATE_OUTLINE_UV` defined, `[MEASURED]` grep of
   both pass assets), so the gate is load-bearing.
4. Shared alpha writers, already gated by the base theorem: `_AlphaMaskMode
   == 0` (applies to the outline branch, `lil_pass_forward_normal.hlsl:194-197`,
   and the shadow caster, `lil_common_frag_alpha.hlsl:58-61`),
   `_DissolveParams.x == 0` with `_IDMaskControlsDissolve == 0` and all
   `_IDMask* == 0` (drives `dissolveActive`/`dissolveInvert` for the outline
   branch too, `:201-216`), `_UseDither == 0` (compiled for `LIL_RENDER 1` in
   the outline branch, `:220-223`).
5. The shared `_Cutoff <= 0.9999` gate carries over unchanged: the outline
   branch applies the same coverage transform with the same `_Cutoff`
   (`:232-233`), and at outline `a ≡ 1` the B2 §3.4 margin argument holds
   identically. The shadow caster's plain `clip(1 − _Cutoff)`
   (`lil_common_frag_alpha.hlsl:99`) keeps under the same bound by the
   sign-preservation argument of T1 §9.2.

Under clauses 1 to 5, the outline alpha is `a ≡ 1` at every outline fragment
of the triangle in `FORWARD_OUTLINE`, `FORWARD_ADD_OUTLINE`, and
`SHADOW_CASTER_OUTLINE`. The source keeps those fragments at full coverage.
The target forces `a = 1.0` and performs no clip. Output is equal.
**`ProvenOpaque` must require the base conjunction AND this outline
conjunction.** The base-alpha theorem alone is not sufficient, and this note
confirms F0 §7.3's warning that it must not be reused as if it were.
`[SOURCE]` `[INFERENCE]`

#### 4.2.3 Not expressible today — the one real architectural event

A second independent coverage source per material is **not expressible** in
the current resolution model, at four layers (scout S3, current tree,
citations verbatim):

| Layer | Fact | Citation |
|---|---|---|
| Value | `ScalarSemanticValueKind` is a closed three-arm enum with exactly one `_sample` field. No composition node | `MaterialSemantics.cs:411-418` |
| Slot | `MaterialSemantics.Alpha` is one `SemanticOutput<ScalarSemanticValue>` | `MaterialSemantics.cs:726` |
| Resolution | `AlphaResolution` holds one `_chain`/`_sampling`/`_mapping`. `Resolve` maps one value to one resolution. The classifier takes exactly one `AlphaTextureData` | `AlphaSemanticsResolver.cs:54-58`, `:276-297`, `TriangleAlphaClassifier.cs:176-233` |
| Slot list | `AdmittedMaterialStates.ResolveSlot` adds exactly one resolution per admitted material | `AdmittedMaterialStates.cs:246-255` |

One collision site needs explicit care in any design:
`UnityRendererAlphaAnalysis.ResolveFor` memoizes one `AlphaResolution` per
`CapturedAlphaMaterial`, and `CapturedAlphaMaterial` has reference equality
(`UnityRendererAlphaAnalysis.cs:548-571`, `UnityMaterialSemantics.cs:20-45`).
A second request for the same material would receive the first cached
resolution. `[SOURCE]`

Capture itself is ready: requesting `_OutlineTex` with
`ScaleOffset | SourceIdentity | Sampling | AlphaChannel` is the existing
per-texture request mechanism. `GatherAlphaFields` is keyed on
`TextureSourceId`, so two distinct textures coexist
(`UnityRendererAlphaAnalysis.cs:573-593`). The bind is the one-value-per-
material interpretation and resolution chain, not the capture.
`[SOURCE]` `[INFERENCE]`

**Proof 2 verdict: satisfied as a gate design.** The outline proof is a
bounded conjunction whose every clause maps to captured, admitted evidence.
Stop condition 2 did not trigger: no fact lies outside what capture and
admission can express. The resolution model needs a second coverage chain per
material, which is design work recorded in §9, not a stop.

### 4.3 Proof 3 — the outline recipe delta

The first-slice recipe writes 18 properties. The outline target needs a
second, independent recipe group: **19 `_Outline*` properties**. The vendor's
own Opaque+outline conversion writes exactly this set (`lilMaterialUtils.cs`):
three in the Opaque branch (`:66-71`) and sixteen in the common tail
(`:294-312`).

| # | Property | Canonical value | Vendor write | Source default | Target default |
|---|---|---:|---|---:|---:|
| 1 | `_OutlineSrcBlend` | 1 (One) | `lilMaterialUtils.cs:68` | 1 (`lts_cutout_o.shader:610`) | 1 (`lts_o.shader:610`) |
| 2 | `_OutlineDstBlend` | 0 (Zero) | `:69` | 0 (`:611`) | 0 (`:611`) |
| 3 | `_OutlineAlphaToMask` | 0 | `:70` | **1** (`:635`) | 0 (`:635`) |
| 4 | `_OutlineCull` | 1 (Front) | `:296` | 1 (`:609`) | 1 (`:609`) |
| 5 | `_OutlineZWrite` | 1 | `:297` | 1 (`:623`) | 1 (`:623`) |
| 6 | `_OutlineZTest` | 2 (Less) | `:298` | 2 (`:624`) | 2 (`:624`) |
| 7 | `_OutlineOffsetFactor` | 0 | `:299` | 0 (`:632`) | 0 (`:632`) |
| 8 | `_OutlineOffsetUnits` | 0 | `:300` | 0 (`:633`) | 0 (`:633`) |
| 9 | `_OutlineColorMask` | 15 | `:301` | 15 (`:634`) | 15 (`:634`) |
| 10 | `_OutlineSrcBlendAlpha` | 1 | `:302` | 1 (`:612`) | 1 (`:612`) |
| 11 | `_OutlineDstBlendAlpha` | 10 (OneMinusSrcAlpha) | `:303` | 10 (`:613`) | 10 (`:613`) |
| 12 | `_OutlineBlendOp` | 0 (Add) | `:304` | 0 (`:614`) | 0 (`:614`) |
| 13 | `_OutlineBlendOpAlpha` | 0 (Add) | `:305` | 0 (`:615`) | 0 (`:615`) |
| 14 | `_OutlineSrcBlendFA` | 1 | `:306` | 1 (`:616`) | 1 (`:616`) |
| 15 | `_OutlineDstBlendFA` | 1 | `:307` | 1 (`:617`) | 1 (`:617`) |
| 16 | `_OutlineSrcBlendAlphaFA` | 0 | `:308` | 0 (`:618`) | 0 (`:618`) |
| 17 | `_OutlineDstBlendAlphaFA` | 1 | `:309` | 1 (`:619`) | 1 (`:619`) |
| 18 | `_OutlineBlendOpFA` | 4 (Max) | `:310` | 4 (`:620`) | 4 (`:620`) |
| 19 | `_OutlineBlendOpAlphaFA` | 4 (Max) | `:311` | 4 (`:621`) | 4 (`:621`) |

`_OutlineZTest` canonical is **2 (Less)**, deliberately not the base `_ZTest`
canonical of 4 (LEqual). The vendor resets it to 2 and F0 §6.5 recorded the
same fact. The outline target declares Queue `Geometry` and `RenderType
Opaque` (`lts_o.shader:640`), so the base recipe's queue and tag actions
carry over unchanged. `[SOURCE]`

**Measured vendor parity.** The probe set 37 distinct sentinel values on a
fresh cutout-outline material, ran the installed vendor
`SetupMaterialWithRenderingMode(mat, Opaque, Normal, isoutl: true, …)` through
reflection, and read back all values. The vendor produced exactly the 18 base
canonical values, exactly the 19 outline canonical values above, and queue
2000. Zero mismatches against the source-derived table. `[MEASURED]`

**Measured swap preservation.** The probe assigned `lts_o` to a sentinel-
valued clone. All 37 values survived the shader swap unchanged. The first
slice measured the same behavior for the 18 (B1 §9). `[MEASURED]`

**Proven-equal rather than written.** These stay cloned from the source and
must never enter the recipe:

- `_OutlineStencilRef`, `_OutlineStencilReadMask`, `_OutlineStencilWriteMask`,
  `_OutlineStencilComp`, `_OutlineStencilPass`, `_OutlineStencilFail`,
  `_OutlineStencilZFail` — user stencil state, driven identically by the
  byte-identical `FORWARD_OUTLINE` declarations (`ltspass_opaque.shader:813-822`
  equals `ltspass_cutout.shader:813-822` by the diff). The vendor writes none
  of them (`lilMaterialUtils.cs:294-312`).
- `_OutlineZClip` — same: property-driven in the byte-identical declarations
  (`:824`), not written by the vendor, cloned by `new Material(source)`.
- `_OutlineCull` is **written**, not proven-equal: the vendor writes it
  (`:296`), so the recipe follows the vendor. Source and target defaults
  agree at 1, so the write is a formality, but vendor parity wins.

The outline-affecting behavior properties `_OutlineWidth`, `_OutlineWidthMask`,
`_OutlineVectorTex`, `_OutlineVectorScale`, `_OutlineVectorUVMode`,
`_OutlineVertexR2Width`, `_OutlineFixWidth`, `_OutlineZBias`,
`_OutlineDeleteMesh`, `_OutlineDisableInVR`, `_OutlineTex`, `_OutlineColor`,
`_OutlineTexHSVG`, `_OutlineLitColor`, `_OutlineLitApplyTex`,
`_OutlineLitScale`, `_OutlineLitOffset`, `_OutlineLitShadowReceive`,
`_OutlineEnableLighting`, `_OutlineTex_ScrollRotate` are **content state, not
render state**. They are cloned like `_MainTex` and never written by the
recipe. Two of them matter to the gate set, not the recipe:
`_OutlineTex` (coverage source, §4.2) and `_OutlineDisableInVR`
(`lil_common_vert.hlsl:69-72`) plus `_OutlineDeleteMesh`
(`lil_common_vert.hlsl:352-357`) are cloned and preserve their behavior
unchanged. `[SOURCE]` `[INFERENCE]`

**Proof 3 verdict: satisfied.** The recipe is enumerable closed from pinned
source: 18 + 19 writes, 8 proven-equal clones, vendor parity measured. Stop
condition 3 did not trigger.

### 4.4 Proof 4 — seam and draw-order characterization

The vertex extrusion is per-vertex: `lilCalcOutlinePosition` displaces each
vertex along `outlineN` derived from `normalOS`, the optional
`_OutlineVectorTex` sample at the vertex uv, `_OutlineWidth`,
`_OutlineWidthMask`, `_OutlineVertexR2Width` (vertex color), and
`_OutlineFixWidth` (`lil_vert_outline.hlsl:20-26`,
`lil_common_functions.hlsl:298-310`). There is no per-triangle state and no
adjacent-vertex dependence in the extrusion. Appended submeshes reference the
same vertices (`AlphaSeparationApply.cs:547-620` rewrites index buffers only),
so identical `_Outline*` material state and identical vertex data should
reproduce the outline surface exactly across a submesh boundary. Unproven
until now. `[SOURCE]`

The probe rendered the comparison directly (§3.3 environment, §7 results):
one unsplit mesh with the source material (the reference), against the same
vertex and index data split into two submeshes — submesh 0 on the source
material, submesh 1 on the canonical 37-value outline clone (the converted
arrangement AMUSE would produce). Three geometry/state variants plus two
controls:

- **G1**: two coplanar quads, shared middle edge, straight-on camera.
- **G2**: a tent — two quads meeting at a ridge that **is** the submesh
  boundary, viewed so the ridge silhouette shows the outline meeting point.
  This is the continuity-critical case.
- **G3**: G2 plus a non-flat `_OutlineVectorTex` on both materials.
- **C1** (sensitivity): the reference against itself with `_OutlineWidth`
  raised from 0.08 to 4.0 on the tent. Must diverge.
- **C2** (the trap): the source's `_OutlineColor.a` set to 0 in both
  arrangements, the clone's restored to 1. Must diverge — this is the F0
  §7.3(d) failure mode made visible.

**Proof 4 verdict: satisfied in the measured scope.** G1, G2, G3 bit-identical.
C1 and C2 diverge. §7 records the numbers and the residual scope. Stop
condition 4 did not trigger.

## 5. Proposed gate set

A successor design must refuse the candidate triangle unless **every** row
holds. Rows marked B2/B1 reuse the shipped cutout gates unchanged. Unknown
information fails closed. Labels: all rows `[SOURCE]` at the pin, cited.

| # | Gate | Rule | Citation |
|---|---|---|---|
| 1 | Source identity | name `Hidden/lilToonCutoutOutline`, GUID `3b4aa199…e929f`, pass `Hidden/ltspass_cutout` GUID `ad219df2…84e36` (existing pin), package 2.3.4, `_lilToonVersion == 45`, canonical digest `9fa9e7e7…a9ec`, `LIL_RENDER 1` | §4.1, `[MEASURED]` |
| 2 | Target identity | name `Hidden/lilToonOutline`, GUID `efa77a80…91cbe`, pass `Hidden/ltspass_opaque` GUID `61b4f98a…3fc` (existing pin), canonical digest `bbd886af…aa36`, `LIL_RENDER 0` | §4.1, `[MEASURED]` |
| 3 | Base coverage conjunction | the full B2 §5 theorem, unchanged: clause 2 gates zero, identity `_MainTex_ST`/`_MainTex_ScrollRotate`, `_MainTex` coverage over every mip, `_Color.a == 1`, animation closure | B2 §5, sites cited there |
| 4 | Outline coverage | `_OutlineTex` admitted format, full mips, Point/Bilinear, equal wrap, measured alpha field, every intersecting texel `== 255` in every mip over the uv0 hull | `lil_common_frag.hlsl:363-364`, B2 §7 machinery |
| 5 | Outline multiplier | `_OutlineColor.a == 1` | `lil_common_frag.hlsl:389` |
| 6 | Outline UV identity | `_OutlineTex_ST == (1,1,0,0)` and `_OutlineTex_ScrollRotate == (0,0,0,0)` per binary32 component | `lil_common_frag.hlsl:276-284`, feature defined `[MEASURED]` |
| 7 | Shared alpha-mask gate | `_AlphaMaskMode == 0` (applies to outline branch and shadow caster) | `lil_pass_forward_normal.hlsl:194-197`, `lil_common_frag_alpha.hlsl:58-61` |
| 8 | Shared dissolve gates | `_DissolveParams.x == 0`, `_IDMaskControlsDissolve == 0`, `_IDMask1..8 == 0` (drives `dissolveActive`/`dissolveInvert` in the outline branch too) | `lil_pass_forward_normal.hlsl:201-216`, `lil_common_frag_alpha.hlsl:65-81` |
| 9 | Shared dither gate | `_UseDither == 0` (compiled for `LIL_RENDER 1` in the outline branch. It composes before the outline transform) | `lil_pass_forward_normal.hlsl:220-223`, `lil_common_frag.hlsl:524-546` |
| 10 | Shared cutoff bound | `_Cutoff <= 0.9999` (same transform and margin argument in base and outline branches. The shadow-caster clip keeps under it) | `lil_pass_forward_normal.hlsl:232-233`, B2 §3.4, T1 §9.2 |
| 11 | Source render state | effective queue 2450, `RenderType=TransparentCutout`, and the existing cutout blend/depth eligibility set | B1 §9, `LilToonCutoutSourceEligibility.cs` (current tree) |
| 12 | Recipe completeness | the clone carries all 18 base + 19 outline canonical values, queue 2000, `RenderType=Opaque`, read back exactly | §4.3, `[MEASURED]` |
| 13 | Outline depth comparison | `_OutlineZTest == 2` (Less), exact-canonical. Canonical is Less, not the base `_ZTest` LEqual. A different comparison changes outline visibility independently of alpha | `ltspass_cutout.shader:826`, shipped shape `LilToonCutoutSourceEligibility.cs:188-199` |
| 14 | Outline depth write | `_OutlineZWrite == 1` | `:825`, shipped shape `:201-208` |
| 15 | Outline colour mask | `_OutlineColorMask == 15` | `:827`, shipped shape `:210-217` |
| 16 | Outline depth offset | `_OutlineOffsetFactor == 0` and `_OutlineOffsetUnits == 0` | `:828`, shipped shape `:219-227` |
| 17 | Outline blend equation | `_OutlineBlendOp == Add` with a unit source factor and a zero destination factor at alpha 1 (`_OutlineSrcBlend`, `_OutlineDstBlend`) | `:829-830`, shipped shape `:229-244` |
| 18 | Outline alpha blend equation | `_OutlineBlendOpAlpha == Add` with a unit source factor and a destination factor that evaluates to zero **at alpha 1** (`_OutlineSrcBlendAlpha`, `_OutlineDstBlendAlpha`). The canonical `_OutlineDstBlendAlpha` is 10 (OneMinusSrcAlpha), which is zero only at alpha 1, so the rule is the shipped alpha-1 degeneration predicate, never a literal `== 0` | `:829-830`, shipped shape `:246-257` |
| 19 | Outline FA blend equation | `_OutlineSrcBlendFA` unit at alpha 1, `_OutlineDstBlendFA == 1`, `_OutlineBlendOpFA == Max`, `_OutlineBlendOpAlphaFA == Max` | `:930-931`, shipped shape `:259-281` |
| 20 | Outline cull | `_OutlineCull == 1` (Front), exact-canonical. The recipe writes this property, so a custom cull would be silently overwritten | `:823`, vendor tail `lilMaterialUtils.cs:296` |

Deliberately ungated, with reasons: `_OutlineDisableInVR` and
`_OutlineDeleteMesh` are cloned and preserve behavior exactly (§4.3).
`_OutlineLitColor.a` is RGB-only (`lil_common_frag.hlsl:386-388`). Stencil and
`_OutlineZClip` are proven-equal clones (§4.3). `_SubpassCutoff` is compiled
out for `LIL_RENDER 1` (`lil_common_frag_alpha.hlsl:100`). `[SOURCE]`

Rows 13-20 cover 16 of the 19 written properties: `_OutlineZTest`,
`_OutlineZWrite`, `_OutlineColorMask`, `_OutlineOffsetFactor`,
`_OutlineOffsetUnits`, `_OutlineSrcBlend`, `_OutlineDstBlend`,
`_OutlineBlendOp`, `_OutlineSrcBlendAlpha`, `_OutlineDstBlendAlpha`,
`_OutlineBlendOpAlpha`, `_OutlineSrcBlendFA`, `_OutlineDstBlendFA`,
`_OutlineBlendOpFA`, `_OutlineBlendOpAlphaFA`, `_OutlineCull`. Three are
written but deliberately ungated, by the same shipped argument
(`LilToonCutoutSourceEligibility.cs:296-303`). `_OutlineAlphaToMask` is
inert at outline `a ≡ 1`: the property modulates the coverage-mask
derivative, not the `a = 1` sample itself (B2 §3.4).
`_OutlineSrcBlendAlphaFA` and `_OutlineDstBlendAlphaFA` are never consumed
by any compiled outline pass: `FORWARD_ADD_OUTLINE` declares its alpha pair
as the literal `Zero One` (`ltspass_cutout.shader:930`), and the two
properties have zero pass usages in the asset (`:618-619`, declarations
only). `_OutlineZClip` stays a proven-equal clone (§4.3). The shipped
doctrine states why this gate group exists at all: "canonicalization changes
both facts and the alpha proof does not authorize erasing custom source
overrides" (`LilToonCutoutSourceEligibility.cs:126-127`), with the
exact-canonical rationale at
`2026-08-30-liltoon-cutout-opaque-conversion-design.md:523-526`. The eight
rows name one refusal concept per class: depth comparison, depth write,
colour mask, depth offset, blend equation, alpha blend equation, FA blend
equation, cull. `[SOURCE]` `[DECISION]`

**Stop-condition 5 check, after the second narrowing.** Would the enlarged
gate set refuse effectively every realistic outlined material? No. The
default `_OutlineTex` is `white` (fully opaque) and the default
`_OutlineColor.a` is 1 (`lts_cutout_o.shader:530-531`). The cutout
container's outline render-state defaults equal all 16 gated canonical
values (`lts_cutout_o.shader:609-635`, §4.3 table), so a material at vendor
defaults passes every outline gate. The one default that differs from a
canonical target value, `_OutlineAlphaToMask = 1`, is one of the three
exempt properties. The gate set therefore narrows coverage a second time.
It refuses materials that deliberately clip their outline alpha (first
narrowing, §4.2) and materials with custom outline render state (second
narrowing, rows 13-20). Both refused populations are self-selected and
small. `[SOURCE]` `[INFERENCE]` The row's product value does not collapse.

## 6. Recipe delta table

| Group | Properties | Action | Source-state authorization |
|---|---|---|---|
| Base render state (existing) | `_SrcBlend 1, _DstBlend 0, _AlphaToMask 0, _ZWrite 1, _ZTest 4, _OffsetFactor 0, _OffsetUnits 0, _ColorMask 15, _SrcBlendAlpha 1, _DstBlendAlpha 10, _BlendOp Add, _BlendOpAlpha Add, _SrcBlendFA 1, _DstBlendFA 1, _SrcBlendAlphaFA 0, _DstBlendAlphaFA 1, _BlendOpFA Max, _BlendOpAlphaFA Max` + queue 2000 + `RenderType=Opaque` | write, unchanged from `LilToonOpaqueTarget.CanonicalOpaqueTuple` (`LilToonOpaqueTarget.cs:55-83`) | gated by the existing base eligibility gates (`LilToonCutoutSourceEligibility.cs:130-306`) |
| Outline render state (new) | the 19 `_Outline*` values of §4.3 | write. Vendor parity measured | 16 of 19 gated by §5 rows 13-20. Three written but ungated, inert by construction: `_OutlineAlphaToMask` at outline `a ≡ 1`, and `_OutlineSrcBlendAlphaFA`/`_OutlineDstBlendAlphaFA` never consumed by any compiled outline pass (§5) |
| Proven-equal clones | `_OutlineStencilRef/ReadMask/WriteMask/Comp/Pass/Fail/ZFail`, `_OutlineZClip` | never write. Validate presence only | none required — cloned, never written |
| Content state | `_OutlineTex`, `_OutlineColor`, and the other outline appearance properties | clone, never write | none required — cloned, never written |

## 7. Measured seam and draw-order results

Environment of record: Unity 2022.3.22f1, macOS, **Metal**, **Gamma** color
space, batch mode, one directional light, shadows off, solid-color clear,
perspective camera, 512×512 render target, **MSAA 4×**, RGBA32 readback,
`filterMode Point` textures, repeat wrap. Both sessions produced byte-identical
reports. `[MEASURED]`

| Comparison | Differing bytes | Max delta | Verdict |
|---|---:|---:|---|
| G1 grid, split vs unsplit | **0** / 1,048,576 | 0 | conversion invisible |
| G2 tent, crease at submesh boundary | **0** / 1,048,576 | 0 | outline continuity preserved across the boundary |
| G3 tent + non-flat `_OutlineVectorTex` | **0** / 1,048,576 | 0 | no vector-tex discontinuity introduced |
| C1 sensitivity, `_OutlineWidth` 0.08 → 4.0 (tent) | 5,211 / 1,048,576 | 97 | harness detects outline geometry changes |
| C2 trap, source `_OutlineColor.a = 0`, clone `.a = 1` | 172,080 / 1,048,576 | 97 | the F0 §7.3(d) trap is behaviorally real |

Also `[MEASURED]` in the same sessions: the canonical clone read back
`Hidden/lilToonOutline`, queue 2000, `RenderType=Opaque`. All 37 recipe values
survived the shader swap on a sentinel material. The vendor-path conversion
matched the source-derived 37-value table with zero mismatches.

An honest note on C1: the first probe session used `_OutlineWidth = 0.16` on
the flat grid. That control produced **zero** difference, because the
extrusion stayed sub-pixel at that camera. The control was too weak, and the
run was discarded. The recorded C1 uses `_OutlineWidth = 4.0` on the tent and
diverges. The published claims rest on C2 and the strengthened C1, not on the
weak first control. `[MEASURED]`

**Residual scope.** These measurements cover: one graphics API (Metal), one
color space (Gamma), one editor version, one light without shadows, MSAA 4,
point filtering, two-material single-renderer arrangements, and outline state
at vendor defaults except where a control varies one value. They do not cover:
D3D11/Vulkan/other APIs, Linear color space, shadow interaction of the
outline shadow caster across the boundary, animated `_Outline*` properties
(refused by the animation-closure machinery as for any requested property),
outline widths beyond the two tested, or multi-renderer overlap of separate
outline hulls. Extrapolation beyond this scope is `[INFERENCE]`, grounded in
the per-vertex extrusion argument of §4.4, not in measurement.
The probe's vendor-default outline state needs no wider measurement. The §5
gate set admits only materials whose outline render state equals those
defaults, so the measured scope now coincides with the admitted population
instead of being a subset of it. `[INFERENCE]`

## 8. Falsifier table

A future design and its test suite must contain, at minimum, these public
deterministic tests. Each names the wrong implementation it fails. The design
brief for this row permits no production tests now, so none were written. The
table is the successor's obligation.

| # | Test | Fails |
|---|---|---|
| 1 | Outline-transparent texel in exactly one mip of `_OutlineTex` → `MustRemainTransparent`. All-opaque control → `ProvenOpaque` | classifying `_OutlineTex` at mip 0 only, or not at all |
| 2 | `_OutlineColor.a = 0.5` → refusal. `_OutlineColor.a = 1` control → `ProvenOpaque` | omitting the outline multiplier gate (§5 row 5) — the C2 trap |
| 3 | `_OutlineTex_ST = (1,1,0,0.0001)` → refusal. `_OutlineTex_ScrollRotate = (0,0,0.0001,0)` → refusal. Identity controls → `ProvenOpaque` | reusing the base `_MainTex_ST` gates for the outline domain (§5 row 6) |
| 4 | Source shader name `Hidden/lilToonCutoutOutline` admitted. `Hidden/lilToonCutout` still routes to the first slice. `Hidden/lilToonOutline` never admitted as a **source** | name matching by prefix. Grouping by shared pass asset (both outline identities share pass assets with existing families) |
| 5 | Target of the outline family's clone is `Hidden/lilToonOutline` with the 37-value recipe. The cutout family's clone stays `lilToon` with 18 | converging both families on one target constant (`AlphaSeparationPreparation.cs:749-753` today) |
| 6 | The refusal enum carries outline-specific members. An outline-gate failure reports that member | reusing `UnsupportedFamily` for an outline-alpha failure, hiding the gate that fired |
| 7 | A renderer with a cutout slot and a cutout-outline slot splits both. Each appended submesh carries its own family's target material | one-converted-material-per-source assumptions at the plan level (§9 row 3) — the flat per-renderer material array already supports this (`AlphaSeparationRecords.cs:305-345`) |
| 8 | A second coverage request for one material returns a second, independent resolution. The memo does not collapse them | the `ResolveFor` memo collision (`UnityRendererAlphaAnalysis.cs:548-571`) |
| 9 | Vendor-parity assertion: the clone's 37 values equal the vendor conversion's read-back on the same sentinel input | a recipe value that drifts from the vendor tail |
| 10 | A fully defaulted outlined cutout material with opaque `_MainTex` and `_OutlineTex` converts. A material with `_OutlineTex` alpha 254 anywhere in the hull's mip chain refuses | a gate set that collapses to refusal-all (stop condition 5) or proofs-all |
| 11 | A source with non-canonical outline render state — `_OutlineColorMask = 0` as the exemplar, since it suppresses all outline colour output on the retained submesh while the moved submesh renders — refuses with the named mask refusal. The canonical control converts | writing the outline recipe without gating the source's outline render state (§5 rows 13-20) — a between-submeshes render-state seam inside one material |

## 9. Architectural pressure — recorded, not resolved

This row is the point where a second target asset exists. The pressure is
real and measured. Per the run's scope fence, this note records it and
designs nothing.

**Review finding (2026-09-04), closed by §5 gates 13-20.** The controller's
review reproduced every load-bearing claim in this note. It found one gap.
The original gate set gated the source's base render state (row 11) but not
its outline render state, while §4.3's recipe writes 19 `_Outline*` values. A
material with, for example, `_OutlineColorMask = 0`, `_OutlineZTest = 8`, or
a custom `_OutlineSrcBlend`/`_OutlineDstBlend` pair would have kept its own
outline state on the retained submesh and received canonical state on the
moved submesh. The divergence sits between the two submeshes of one material,
so it renders as a visible seam — the exact failure Proof 4 exists to
exclude. This violates the shipped doctrine: "canonicalization changes both
facts and the alpha proof does not authorize erasing custom source overrides"
(`LilToonCutoutSourceEligibility.cs:126-127`, identically
`LilToonTransparentSourceEligibility.cs:167-168`). The base slice was silent
on outlines by scope alone ("no `_Outline*` property (this is the no-outline
slice)", `2026-08-30-liltoon-cutout-opaque-conversion-design.md:468-469`).
Its outline-vacuity argument — "no outline gate exists, because outline state
rides on shader asset identity" (`:542-543`) — expires the moment the target
is an outline-capable asset. T2 crosses that boundary and inherits the
obligation. `[SOURCE]` `[DECISION]`

1. **Target selection has exactly one hardcoded site.**
   `LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(Material,
   CapturedMaterialEvidence)` resolves the target by
   `Shader.Find(SupportedShaderName)` (`LilToonOpaqueTarget.cs:318`) and
   verifies against `OpaqueProfile` (`:330-331`). The module's own doc states
   the assumption: "one shader, one direction, one version" (`:28-30`).
   `[SOURCE]` A second target needs its own constants, its own profile
   instance, and a resolution parameter — or a second module shaped like the
   first. The explicit-`Shader` overload (`:241-306`) already takes the
   target as a parameter and hardcodes nothing but the 18-tuple, queue 2000,
   and the Opaque tag. The outline target declares all 18 recipe properties
   (`lts_o.shader` property block) and accepts queue 2000/Opaque. `[SOURCE]`
   `[INFERENCE]`
2. **The family maps converge on one recipe.**
   `CanonicalPropertiesForFamily` returns
   `LilToonOpaqueTarget.CanonicalOpaqueProperties` for both
   `LilToonCutout` and `LilToonTransparent`
   (`AlphaSeparationPreparation.cs:748-753`, quoted by scout S4). A third
   conversion family maps to a **different** recipe record. The two family
   maps (`ConversionRequestForFamily` `:719-734`,
   `CanonicalPropertiesForFamily` `:742-757`) and the dispatch switch
   (`:443-455`) each take one more arm — the established shape. `[SOURCE]`
3. **The refusal vocabulary has no outline members.**
   `LilToonOpaqueConversionRefusal` documents "there are no outline or
   premultiply members" (`LilToonOpaqueConversionResult.cs:50-56`). Outline
   gates need their own named refusals (falsifier 6). `[SOURCE]`
4. **A second coverage chain per material** is the Proof 2 requirement. Four
   layers would need a bounded extension (§4.2.3), and one memo site must not
   collapse two analyses of one material. `[SOURCE]` Resolved by
   `[DECISION]` D2 below: the outline-family semantics shape avoids the
   four-layer extension. The memo fix stands either way.
5. **Target attestation as a concept.** Today the target is verified through
   the source-profile machinery with a name override (§4.1.4). A second
   target makes "target profile" a real, if tiny, concept: one more instance,
   not a new mechanism. `[SOURCE]` `[INFERENCE]`

### Controller decisions recorded 2026-09-04

1. **Second-target shape — `[DECISION]` (D1).** A sibling module
   `LilToonOutlineTarget`, mirroring `LilToonOpaqueTarget`: its own target
   constants, its own target profile, its own resolution, referencing the
   existing base-18 canonical tuple constant rather than duplicating it, and
   owning the 19 outline writes. Rejected alternative: extending
   `LilToonOpaqueTarget` with a per-target data argument. Reason: the recipes
   share only queue 2000 and `RenderType=Opaque` and diverge by 19
   properties. Parameterizing would produce the mode-parameterized engine the
   scope fences ban, and it would re-litigate the ownership split PR #45 just
   performed.
2. **Second coverage chain ownership — `[DECISION]` (D2).** The
   outline-family semantics shape: the family's interpretation performs both
   classifications and emits one combined outcome. Rejected alternative: a
   second `AlphaResolution` per material. Reason: the chosen shape needs no
   new resolution-layer concept, and the four-layer extension would
   generalize the resolution model on the strength of a single consumer. The
   `ResolveFor` memo fix (`UnityRendererAlphaAnalysis.cs:548-571`, falsifier
   8) is required either way. If a third independent coverage source appears,
   generalize then, with two real cases.
3. **Sequencing — closed `[DECISION]` (D3).** F0 §11 row 3's prerequisite is
   row 2, "Regular no-outline cutout conversion implementation", which
   shipped in PR #41 (`89cc5be` merge, `1b6cf13` implementation,
   `LilToonCutoutSourceEligibility.cs` present in the tree). The prerequisite
   is discharged. Nothing gates this row. `[MEASURED]` by `git log` and tree
   inspection.

## 10. Stop conditions — status

| # | Stop condition | Status |
|---|---|---|
| 1 | `Hidden/lilToonOutline` cannot be attested with the existing attestation model | **Not triggered.** Both identities pass the unmodified `Verify` conjunction with their own profile values (§4.1.4) |
| 2 | The outline-alpha theorem needs a fact no capture or admission mechanism can express | **Not triggered.** Every outline clause maps to existing capture kinds. The resolution model needs a bounded extension, recorded as pressure, not a stop (§4.2.3) |
| 3 | The outline recipe cannot be enumerated closed from pinned source | **Not triggered.** 18 + 19 writes with per-property citations, vendor parity measured (§4.3) |
| 4 | Seam continuity or draw order measures as NOT preserved across a submesh boundary | **Not triggered.** Bit-identical in all three geometry/state variants (§7) |
| 5 | The gate set refuses effectively every realistic outlined material | **Not triggered.** Vendor-default outline state passes every gate (§5) |
| 6 | A required measurement cannot be scoped | **Not triggered.** Every measurement's residual scope is stated (§7) |
| 7 | Unity instance identity is ambiguous | **Not triggered.** No Unity MCP call was issued at all. The scratch probe asserted its own `Application.dataPath` and the AMUSE project was never opened |
| 8 | Answering would require production code changes | **Not triggered.** No production file changed |

## 11. What this investigation proves and does not prove

**Proves.**

- The exact identity, GUIDs, pass topology, and digests of
  `Hidden/lilToonCutoutOutline` and `Hidden/lilToonOutline`. Both pass assets
  are already-attested AMUSE pins. The byte-identical pass declarations make
  conversion participation-preserving (§4.1).
- The complete outline alpha dataflow in all three outline passes, with every
  writer classified and its gate named (§4.2).
- That the base-alpha theorem is insufficient and the outline conjunction is
  the required second proof (§4.2.2).
- The closed recipe: 18 + 19 writes, 8 proven-equal clones, vendor parity and
  swap preservation measured (§4.3, §7).
- Outline continuity and draw-order invariance across an appended-submesh
  boundary in the measured scope, with the trap confirmed behaviorally (§7).
- A complete gate set with per-gate citations, a falsifier matrix, and the
  recorded architectural pressure (§5, §8, §9).

**Does not prove.**

- Anything about transparent-outline, outline-only, Lite, Tessellation, Multi,
  Overlay, or any refused family.
- Behavior outside the residual scope of §7 (other APIs, color spaces,
  shadows, animation of `_Outline*`, multi-renderer hull overlap).
- That the coverage shape of §9 `[DECISION]` D2 is the right design — the
  controller chose it at review. The note establishes only that the memo
  collision site exists and must be fixed.
- Any production behavior. This note wrote none.

**No falsification test was necessary.** The run's budget allowed exactly one
test-writing subagent, only for a claim about AMUSE's current behavior that
reading could not settle. Every such claim settled by reading: the
single-target hardcode (`LilToonOpaqueTarget.cs:318`), the single-coverage
resolution chain (§4.2.3, four layers), the recipe convergence
(`AlphaSeparationPreparation.cs:748-753`), and the current refusal of
`Hidden/lilToonCutoutOutline` (exact-name classification with no arm for it,
already covered by existing negative fixtures,
`AlphaSeparationPreparationTests.cs:4680-4699`). The runtime facts the row
needed — digests, vendor parity, swap preservation, seam rendering — require a
real editor with lilToon installed, which is measurement, not EditMode tests.
The one test subagent was therefore never spawned. `[DECISION]`

## 12. Verdict and next recommendation

**Verdict: GO-WITH-CONDITIONS.**

The transformation is sound in every measured dimension, and the vendor's own
outline-capable opaque target preserves the outline. The evidence is unusually
clean: byte-identical pass declarations, pass digests already pinned, both new
container digests measured with zero canonicalization regions, vendor-parity
recipe measured, and bit-identical seam rendering across a crease boundary.
The gate set refuses a bounded, self-selected population, not the general
outlined population.

The conditions, each a production prerequisite for the successor design
branch:

1. A second target asset becomes first-class as a `LilToonOutlineTarget`
   sibling module: target constants, a target profile, its own resolution,
   an outline recipe group (18 + 19), and outline-specific refusal members
   (`[DECISION]` D1 in §9).
2. The outline-alpha conjunction of §4.2.2 ships as part of the theorem, with
   the outline UV identity gates. The base theorem must never be reused alone.
3. The source's outline render state passes the outline source-eligibility
   gates of §5 rows 13-20 before any outline recipe write. 16 gated, 3
   documented-inert, eight named refusal concepts (§5).
4. The coverage proof ships in the outline-family semantics shape of
   `[DECISION]` D2 in §9, with the `ResolveFor` memo fix (falsifier 8).
5. The falsifier matrix of §8 is the RED/GREEN suite for the successor branch.
6. Sequencing: discharged. The production cutout slice shipped in PR #41
   (`[DECISION]` D3 in §9). Nothing gates this row.

**Exact next recommendation:** write the design spec for the outline family,
carrying §5 (gates), §6 (recipe), and §8 (falsifiers) as normative content.
No production branch until the controller approves that design.

## 13. External research

Reputable-source findings, all `[INFERENCE]` by the run's rules (prose never
outranks pinned source):

- **d4rkAvatarOptimizer** hard-codes lilToon as unsupported for its shader
  manipulation ("Hard code that lilToon shaders are not supported as some of
  them don't get caught by the automated parsing", CHANGELOG and the v3.1.1 notes)
  and routes affected users to a preset that disables shader manipulation.
  It merges materials by rewriting shader variants with `//ifex` conditionals
  and passes. Its author documentation describes excluding an entire outline
  pass via a constant directive rather than per-triangle moves. No documented
  per-triangle outline hazard — because it does not do per-triangle moves.
  Its refusal of lilToon shader rewriting is free evidence that rewriting
  this shader family is hazardous. AMUSE does not rewrite shaders.
- **AAO (anatawa12/AvatarOptimizer)** deprecates `MergeToonLitMaterial` in
  favor of `MergeMaterial` with per-shader `ShaderInformation` (lilToon
  included). Its changelog records outline-shader support only for texture
  optimization (Toon Standard (Outline), VRCSDK 3.8.1) and a lint that flags
  same-material multi-pass rendering as a probable mistake. No documented
  submesh-level outline hazard. Neither tool documents the failure mode this
  row's Proof 2 guards: an outline-alpha change across a material move.

Neither neighbour's experience contradicts this row's findings. Neither
performs the transformation this row evaluates, so their refusals are only
weak positive evidence.

## 14. Citations

**Upstream** — all paths relative to `Assets/lilToon/` at tag `2.3.4`, commit
`252fd8cfc46106d4967e95b3f2c788418502f227`
(<https://github.com/lilxyzw/lilToon>).

- `Shader/lts_cutout_o.shader:1,19-22,32,44-46,529-549,570,578-635,638-646,718`
- `Shader/lts_o.shader:1,19-22,44-46,529-549,570,578-635,638-646,718`
- `Shader/ltspass_opaque.shader:639,760-806,808-854,861-906,909-955,961-998,1001-1039,1042-1066`
- `Shader/ltspass_cutout.shader` — byte-identical declarations per §4.1.2 diff
- `Shader/Includes/lil_pass_forward_normal.hlsl:154-157,173-260`
- `Shader/Includes/lil_common_frag.hlsl:276-284,353-358,362-399,524-546,554-560`
- `Shader/Includes/lil_common_frag_alpha.hlsl:1-117`
- `Shader/Includes/lil_common_vert.hlsl:69-72,119,152-156,339-358`
- `Shader/Includes/lil_vert_outline.hlsl:1-29`
- `Shader/Includes/lil_common_functions.hlsl:272-318`
- `Editor/lilMaterialUtils.cs:18-72,91-104,255-312`
- `Editor/lilShaderManager.cs:14-15,67-68`

**AMUSE current tree at `30cbfc2`** — paths relative to
`<repo-root>/Packages/com.alrauna.amuse/`.

- `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:323-344,346-362,363-383,388-451,960-996,1185-1222,1229-1356,1427-1439,1447-1543,1601-1607`
- `Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:28-30,55-83,135-144,153-183,241-306,311-341`
- `Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs:46-74,113-121`
- `Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs:45,58,83-142,183-220,225-365`
- `Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs:46,64,130-174,318-361,384-450`
- `Editor/Semantics/MaterialSemantics.cs:411-418,726`
- `Editor/Semantics/UnityMaterialSemantics.cs:11-18,20-45,218-243,261-308,311-317,318-335,350-360,364-381,382-406,416-467`
- `Editor/Analysis/AlphaSemanticsResolver.cs:54-58,117-136,276-297,304-347,412-417,440-451`
- `Editor/Analysis/TriangleAlphaClassifier.cs:8-13,171-233`
- `Editor/Analysis/AdmittedMaterialStates.cs:116-170,198-259`
- `Editor/Analysis/MeshSeparationPlanner.cs:104-133,136-163,186-198`
- `Editor/Host/UnityRendererAlphaAnalysis.cs:526-527,548-593,627-667,703-773`
- `Editor/Build/AlphaSeparationPreparation.cs:28-52,82,401-407,443-455,457-464,466-601,704-708,719-734,742-757,765-784`
- `Editor/Build/AlphaSeparationApply.cs:32,89-186,223-249,283-297,391-431,547-620,622-644`
- `Editor/Build/AlphaSeparationRecords.cs:86-186,246-268,271-369`
- `Editor/Build/AmusePlatformFinishPlugin.cs:11-166,313-431,584-590,619-621,664-678,725-758`
- `Editor/Host/UnityMaterialEvidenceCapture.cs:316-661`
- `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:4680-4699`
- `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs:516-527`

**Merged AMUSE documents.**

- F0 — `docs/superpowers/investigations/2026-08-30-liltoon-family-applicability.md` §4, §6.5, §7.3, §7.7, §7.8, §10, §11
- T1 — `docs/superpowers/investigations/2026-09-01-liltoon-transparent-normal-alpha-separation.md` (structure and method model)
- T1 design — `docs/superpowers/specs/2026-09-01-liltoon-transparent-normal-alpha-separation-design.md`
- B1 — `docs/superpowers/investigations/2026-08-30-liltoon-opaque-characterization.md`
- B2 — `docs/superpowers/investigations/2026-08-30-liltoon-cutout-alpha-semantics.md`
- `docs/architecture/shader-frontend-comparison.md`

**Scratch probe artifacts** (outside the repository, deleted after recording):
official vendor package `jp.lilxyzw.liltoon-2.3.4.zip`
(<https://github.com/lilxyzw/lilToon/releases/download/2.3.4/jp.lilxyzw.liltoon-2.3.4.zip>),
SHA-256 `34d172761c51aa9469a904704109086aafa6125a4fa0e058766e2ddc73d3b303`.
A byte-identical copy of the merged `LilToonSourceAttestation.cs` (SHA-256
`06d1f2815df891ca4a0b2209b2b58ecea370e530e0a2c3f38cf1a383fc08d19b`) drove the
digest measurement, with the probe-local evidence shim disclosed in §3.3.

**External.**

- d4rkAvatarOptimizer CHANGELOG and `Documentation~/ForShaderAuthors.md`
  (<https://github.com/d4rkc0d3r/d4rkAvatarOptimizer>) — `[INFERENCE]` source
- AAO AvatarOptimizer CHANGELOG and Merge Material reference
  (<https://github.com/anatawa12/AvatarOptimizer>,
  <https://vpm.anatawa12.com/avatar-optimizer/en/docs/reference/merge-material/>)
  — `[INFERENCE]` source

## 15. Privacy statement

No Census Lab data and no private avatar data was used, inspected, or
modified. No private names, paths, GUIDs, per-avatar rows, or fingerprint-like
identifiers appear in this document. Every GUID cited is a public lilToon
package asset GUID. No absolute or machine-specific path appears in this
document. Probe paths were under the OS temporary directory outside AMUSE and
are referred to only by role.

This session issued **no Unity MCP call** against any instance. No Unity
instance belonging to any project was enumerated, named, or targeted. All
Unity work ran in a throwaway project outside AMUSE, addressed by an explicit
project path, and was deleted after recording. The process did not install
lilToon into AMUSE and wrote to neither AMUSE Unity state nor the repository's
`Packages/` toolchain files. No vendor source entered the repository.

The work tree ends this run with exactly one new file (this note) plus the
pre-existing user-owned `Packages/manifest.json` and
`Packages/packages-lock.json` modifications, unstaged. Nothing was staged,
committed, pushed, stashed, rebased, or merged.
