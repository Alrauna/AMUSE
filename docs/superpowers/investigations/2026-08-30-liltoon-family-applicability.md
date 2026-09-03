# F0 — official lilToon 2.3.4 shader-family applicability for AMUSE alpha separation

## 1. Question and product requirement

> Which official lilToon 2.3.4 material-shader families are meaningful candidates for AMUSE's
> triangle-level alpha separation? Which families need a separate semantic or representation
> proof? Which families are already-opaque or target-only states? Which families are internal
> helpers? Which families need explicit refusal, because opaque separation cannot preserve their
> behavior?

The product requirement: every official lilToon material-shader family gets an evidence-backed
applicability disposition. A family does not need a positive optimization result. A family that
cannot preserve its intended behavior needs an explicit, justified refusal or a clearly scoped
future investigation.

## 2. Bounded non-implementation scope

This note closes the inventory. It sets the investigation roadmap. It does not:

- design family alpha semantics (B2 remains the cutout-alpha-semantics task).
- pin attestation digests for any family beyond the already-measured B1 set.
- implement support, refactor production code, or design the conversion recipe.
- decide product support policy where `[DECISION NEEDED]` is marked.

The existing B1 scope stays unchanged: regular, non-Lite, non-Tessellation, non-Multi, no-outline
cutout. It remains the only positively scoped first slice.

Labels: `[SOURCE]` marks a pinned-package fact (commit-pinned paths in §3). `[MEASURED]` marks a
prior executed observation from the merged B1 note
(`2026-08-30-liltoon-opaque-characterization.md`). `[INFERENCE]` marks a bounded conclusion.
`[DECISION NEEDED]` marks an unresolved product or architecture choice.

## 3. Source pin and method

- Upstream pin: lilToon tag `2.3.4`, commit `252fd8cfc46106d4967e95b3f2c788418502f227`
  (`Assets/lilToon/package.json:2` declares `"version": "2.3.4"`). Every `[SOURCE]` path below is
  relative to `Assets/lilToon/` at that commit. The pin was cloned read-only into a temporary
  directory outside AMUSE. It is not a Unity project. Nothing was installed into AMUSE.
- Merged repository base: `main` at `0e90f01` (merge of PR #38, B1 characterization), branch
  `investigate/liltoon-family-applicability`. This investigation read the controlling
  investigation (`2026-08-30-liltoon-opaque-conversion.md`) and B1
  (`2026-08-30-liltoon-opaque-characterization.md`) in full and treated them as claims to verify.
  It found no contradiction with the pinned source. The unmerged
  `design/liltoon-cutout-alpha-semantics` branch (cutout alpha-semantics design) is visible
  repository reality, but it is not merged context. F0's roadmap references it only as in-flight
  work.
- Method: closed-asset enumeration of every `*.shader` file in the pinned package (`Shader/` —
  no `.shader` asset exists anywhere else in the package). Mechanical extraction of each asset's
  internal `Shader "…"` name. Reconciliation against every field of `Editor/lilShaderManager.cs`
  and the vendor mode map in `Editor/lilMaterialUtils.cs` (`SetupMaterialWithRenderingMode`,
  `:18-330`). Per-container pass/`UsePass`/`GrabPass` extraction. And targeted reads of pass
  includes for alpha, refraction, fur, gem, and lite behavior.
- This investigation made no new Unity measurements. Every runtime-behavior claim reuses B1
  `[MEASURED]` rows only.

## 4. Closed official shader inventory

65 `.shader` assets exist in the pinned package, all under `Shader/`. Classification:

- **52 material-entry shaders** — identities a material can carry on an avatar renderer.
- **13 internal shaders** — 9 hidden pass assets consumed via `UsePass`/includes, 4 editor
  bake/support shaders. These are not avatar-material families.

### 4.1 Material-entry shaders (52)

| Group | Asset(s) (`Shader/`) | Shader name(s) | Manager field(s) | Declared tag/queue | Pass carrier |
|---|---|---|---|---|---|
| Regular opaque | `lts.shader` | `lilToon` | `lts` | Opaque / Geometry (2000) | `Hidden/ltspass_opaque` (LIL_RENDER 0) |
| Regular cutout | `lts_cutout.shader` | `Hidden/lilToonCutout` | `ltsc` | TransparentCutout / AlphaTest (2450) | `Hidden/ltspass_cutout` (LIL_RENDER 1) |
| Regular transparent Normal | `lts_trans.shader` | `Hidden/lilToonTransparent` | `ltst` | TransparentCutout / AlphaTest+10 (2460) | `Hidden/ltspass_transparent` (LIL_RENDER 2) |
| Regular transparent OnePass | `lts_onetrans.shader` | `Hidden/lilToonOnePassTransparent` | `ltsot` | TransparentCutout / AlphaTest+10 (2460) | same, no FORWARD_ADD |
| Regular transparent TwoPass | `lts_twotrans.shader` | `Hidden/lilToonTwoPassTransparent` | `ltstt` | TransparentCutout / AlphaTest+10 (2460) | same, + FORWARD_BACK |
| Regular opaque outline | `lts_o.shader` | `Hidden/lilToonOutline` | `ltso` | Opaque / Geometry | `Hidden/ltspass_opaque`, outline passes |
| Regular cutout outline | `lts_cutout_o.shader` | `Hidden/lilToonCutoutOutline` | `ltsco` | TransparentCutout / AlphaTest | `Hidden/ltspass_cutout`, outline passes |
| Regular transparent outline ×3 | `lts_trans_o.shader`, `lts_onetrans_o.shader`, `lts_twotrans_o.shader` | `Hidden/lilToon(OnePass|TwoPass)?TransparentOutline` | `ltsto`, `ltsoto`, `ltstto` | TransparentCutout / AlphaTest+10 | `Hidden/ltspass_transparent`, outline passes |
| Outline-only ×3 | `lts_oo.shader`, `lts_cutout_oo.shader`, `lts_trans_oo.shader` | `_lil/[Optional] lilToonOutlineOnly(Cutout|Transparent)?` | `ltsoo`, `ltscoo`, `ltstoo` | Opaque+Geometry / cutout+AlphaTest / trans+AlphaTest+10 | outline `UsePass` subset of `ltspass_opaque`/`_cutout`/`_transparent` |
| Tessellation ×10 | `lts_tess{,_cutout,_trans,_onetrans,_twotrans}{,_o}.shader` | `Hidden/lilToonTessellation…` | `ltstess`, `ltstessc`, `ltstesst`, `ltstessot`, `ltstesstt`, `ltstesso`, `ltstessco`, `ltstessto`, `ltstessoto`, `ltstesstto` | mirror regular queues | `Hidden/ltspass_tess_{opaque,cutout,transparent}` |
| Lite ×10 | `ltsl{,_cutout,_trans,_onetrans,_twotrans}{,_o}.shader` | `Hidden/lilToonLite…` | `ltsl`, `ltslc`, `ltslt`, `ltslot`, `ltsltt`, `ltslo`, `ltslco`, `ltslto`, `ltsloto`, `ltsltto` | mirror regular queues | `Hidden/ltspass_lite_{opaque,cutout,transparent}` |
| Lite overlay ×2 | `ltsl_overlay.shader`, `ltsl_overlay_one.shader` | `_lil/[Optional] lilToonLiteOverlay(OnePass)` | `ltslover`, `ltsloover` | TransparentCutout / AlphaTest+10 | `Hidden/ltspass_lite_transparent` FORWARD (+FORWARD_ADD except overlay_one) |
| Refraction | `lts_ref.shader` | `Hidden/lilToonRefraction` | `ltsref` | Opaque / Transparent−100 (2900) | inline, LIL_RENDER 2, `GrabPass "_lilBackgroundTexture"` |
| RefractionBlur | `lts_ref_blur.shader` | `Hidden/lilToonRefractionBlur` | `ltsrefb` | Opaque / Transparent−100 | inline, LIL_RENDER 2, two `GrabPass` + FORWARD_BLUR |
| Fur | `lts_fur.shader` | `Hidden/lilToonFur` | `ltsfur` | TransparentCutout / Transparent (3000) | inline, LIL_RENDER 2, FORWARD/FORWARD_FUR/ADD variants |
| FurCutout | `lts_fur_cutout.shader` | `Hidden/lilToonFurCutout` | `ltsfurc` | TransparentCutout / AlphaTest | inline, LIL_RENDER 1 |
| FurTwoPass | `lts_fur_two.shader` | `Hidden/lilToonFurTwoPass` | `ltsfurtwo` | TransparentCutout / Transparent | inline, LIL_RENDER 2, FORWARD_FUR_PRE + FORWARD_FUR |
| Fur-only ×3 | `lts_furonly{,_cutout,_two}.shader` | `_lil/[Optional] lilToonFurOnly(Transparent|Cutout|TwoPass)` | `ltsfuro`, `ltsfuroc`, `ltsfurotwo` | Transparent/3000, AlphaTest, Transparent | `UsePass` of `Hidden/lilToonFur(Cutout|TwoPass)` fur passes only |
| Gem | `lts_gem.shader` | `Hidden/lilToonGem` | `ltsgem` | Opaque / Transparent−100 | inline, LIL_RENDER 2, GrabPass, FORWARD_PRE |
| Overlay ×2 | `lts_overlay.shader`, `lts_overlay_one.shader` | `_lil/[Optional] lilToonOverlay(OnePass)` | `ltsover`, `ltsoover` | TransparentCutout / AlphaTest+10 | `Hidden/ltspass_transparent` FORWARD (+FORWARD_ADD except overlay_one) |
| Fake shadow | `lts_fakeshadow.shader` | `_lil/[Optional] lilToonFakeShadow` | `ltsfs` | Transparent / AlphaTest+55 (2505) | inline minimal pass |
| Multi ×5 | `ltsmulti{,_o,_ref,_fur,_gem}.shader` | `_lil/lilToonMulti`, `Hidden/lilToonMulti(Outline|Refraction|Fur|Gem)` | `ltsm`, `ltsmo`, `ltsmref`, `ltsmfur`, `ltsmgem` | Opaque/Geometry (base), variant queues (outline 2900, fur 3000, gem/ref 2900) | inline passes. Mode via `_TransparentMode` + keywords |

### 4.2 Internal, helper, and pass shaders (13)

| Asset | Shader name | Manager field | Consumer | Class |
|---|---|---|---|---|
| `ltspass_opaque.shader` | `Hidden/ltspass_opaque` | `ltspo` | regular and regular-outline containers `UsePass`. AMUSE attestation pin | pass asset, LIL_RENDER 0, 7 declared passes |
| `ltspass_cutout.shader` | `Hidden/ltspass_cutout` | `ltspc` | cutout containers | pass asset, LIL_RENDER 1 |
| `ltspass_transparent.shader` | `Hidden/ltspass_transparent` | `ltspt` | transparent/overlay containers | pass asset, LIL_RENDER 2, incl. FORWARD_BACK |
| `ltspass_tess_opaque.shader` | `Hidden/ltspass_tess_opaque` | `ltsptesso` | tess containers | pass asset |
| `ltspass_tess_cutout.shader` | `Hidden/ltspass_tess_cutout` | `ltsptessc` | tess containers | pass asset |
| `ltspass_tess_transparent.shader` | `Hidden/ltspass_tess_transparent` | `ltsptesst` | tess containers | pass asset |
| `ltspass_lite_opaque.shader` | `Hidden/ltspass_lite_opaque` | — (no field) | lite containers `UsePass` | pass asset, LIL_RENDER 0 |
| `ltspass_lite_cutout.shader` | `Hidden/ltspass_lite_cutout` | — | lite containers | pass asset, LIL_RENDER 1 |
| `ltspass_lite_transparent.shader` | `Hidden/ltspass_lite_transparent` | — | lite containers | pass asset, LIL_RENDER 2 |
| `ltspass_baker.shader` | `Hidden/ltsother_baker` | `ltsbaker` | editor bake tools (`lilTextureUtils.cs:384`, `lilToonEditorUtils.cs:110`, `lilInspector/lilEditorTextureBaker.cs:55`) | editor helper |
| `ltspass_bakeramp.shader` | `Hidden/ltsother_bakeramp` | — | `Editor/lilToon2Ramp.cs:12` | editor helper (ramp baking) |
| `ltspass_proponly.shader` | `Hidden/ltspass_proponly` | — | `Editor/lilOptimizer.cs:206` | editor helper (mesh/material baking) |
| `ltspass_dummy.shader` | `Hidden/ltspass_dummy` | — | none in package code. Placeholder pass for container authors | placeholder |

### 4.3 Reconciliation and closure

- `Editor/lilShaderManager.cs:8-79` declares 60 fields: 52 material fields map 1:1 to 52 material
  assets by internal name (verified mechanically), plus 6 pass fields, 1 baker field, and
  `mtoon = Shader.Find("VRM/MToon")` (`:79`), which references an **external** VRM shader, not a
  package asset (used by editor conversion utilities only).
- 6 package assets have no manager field: the three `ltspass_lite_*` passes (consumed by lite
  containers via `UsePass`), `ltsother_bakeramp`, `ltspass_proponly`, `ltspass_dummy`.
- 65 equals 52 plus 13. This investigation classified every discovered `Shader` asset. No manager
  field is unaccounted.
- Officially generated identities: the scripted importer compiles `.lilcontainer` assets to
  Shader assets (`Editor/lilShaderContainerImporter.cs:23-25`,
  `Editor/lilMaterialUtils.cs:717-730` accepts them as lilToon). These are user-authored
  integration identities, not official package assets. They fall outside the official inventory.
  They are recorded as out of F0 scope (`[DECISION NEEDED]` for any future support policy).
- `CheckShaderIslilToon` also accepts the legacy name substring `lts_pass`
  (`lilMaterialUtils.cs:717-730`). No such asset exists in the pinned package.

**Total inventory count: 65 official shader assets (52 material-entry + 13 internal).**

## 5. Taxonomy and applicability rubric

Fixed disposition vocabulary:

- **P** — Primary support candidate: strong fit for triangle-to-opaque separation.
- **C** — Conditional support candidate: plausible, blocked by a separate semantic, geometry,
  pass, lifecycle, or attestation investigation.
- **A** — Already opaque / target-only: relevant to mapping and mixed states, not a source.
- **R** — Explicit refusal candidate: evidence shows the transformation would discard or alter
  defining behavior.
- **I** — Internal/helper shader: evidence dependency or editor/build tool.
- **U** — Unknown / decision needed.

Each disposition below states the source evidence, the transformation opportunity, the
preservation risk, the exact missing proof, and the next action. Under the falsifiability
requirements:

- each grouping carries a counterexample.
- §7.0 covers alpha-one sufficiency per mode class.
- each positive or conditional candidate carries a target-preservation check.
- each refusal carries a reconsideration condition.
- no disposition became more positive because evidence was missing. Increased uncertainty never
  produced a more positive disposition.

## 6. Vendor facts the analysis rests on

All `[SOURCE]` at the pinned commit:

1. **Mode is a shader-asset identity for non-Multi.** `SetupMaterialWithRenderingMode`
   (`Editor/lilMaterialUtils.cs:18-330`) selects the target asset per
   RenderingMode × TransparentMode × {outline, lite, tess, multi}. Conversion is a shader swap
   (`material.shader = …`), never a property or keyword toggle. The vendor writes blend/depth
  state (`:63-71,96-104,171-179,192-194,213-219,232-238,242-248,261-263`), restores the pre-call
   queue for non-Multi (`:266`), and resets the common tail (`:268-329`). Gem gets
   `Cull=0`/`ZWrite=0` from that tail. The code skips `_ZTest=4` only when
   `transparentMode == TwoPass` (`:277-280`).
2. **Mode map coverage:** Opaque/Cutout/Transparent select lite/tess/outline variants.
   **Refraction selects only `ltsref`/`ltsmref`. RefractionBlur selects only `ltsrefb`.
   Fur/FurCutout select only `ltsfur`/`ltsfurc`/`ltsmfur`. FurTwoPass selects only `ltsfurtwo`.
   Gem selects only `ltsgem`/`ltsmgem`** (`:181-264`). The specialized modes have no
   outline/lite/tessellation variants reachable through the vendor conversion.
3. **Multi mode is `_TransparentMode`** (0..6 enum, `ltsmulti.shader:561`), read only by editor
   tooling (`lilMaterialUtils.cs:24-37,353+`). No HLSL include reads it (`[SOURCE]` — a grep of
   `Shader/Includes/` and `ltsmulti.shader` HLSL finds only the property declaration). Keywords
   derived at edit time carry the effective rendering mode
   (`UNITY_UI_ALPHACLIP` for cutout, `UNITY_UI_CLIP_RECT` for transparent/fur, `:412-414`), plus
   feature keywords and `_AsOverlay`-driven pass enables (`:415-420`).
4. **Callback 100 runs after NDMF:** `External/Editor/VRChatModule.cs:22` declares
   `callbackOrder 100`. `:60-61`/`:85-86` run `SetShaderSettingBeforeBuild(materials, clips)` and
   `SetupMultiMaterial(materials, clips)`. The clips variant (`lilMaterialUtils.cs:476-495`) scans
   animation curve bindings and **enables keywords on all Multi materials** for three features
   (`_RimDirStrength`→`GEOM_TYPE_LEAF`, `_MainTexHSVG`/`_MainGradationStrength`→
   `EFFECT_HUE_VARIATION`). It does **not** re-derive mode keywords or pass enables at build.
5. **Outline is a set of full passes** (`FORWARD_OUTLINE`, `FORWARD_ADD_OUTLINE`,
   `SHADOW_CASTER_OUTLINE` in each pass asset, e.g. `ltspass_opaque.shader:810-861`) with a
   complete independent `_Outline*` state (stencil, cull, ZClip, ZWrite, ZTest, ColorMask,
   Offset, BlendOp, Blend, AlphaToMask). The vendor tail resets `_OutlineZTest=2` (Less)
   (`lilMaterialUtils.cs:294-312`).
6. **TwoPass transparency adds `FORWARD_BACK`** (`ltspass_transparent.shader:795-843`), a
   back-face pre-pass with its own `_Pre*` state. The TwoPass container `UsePass`es it first
   (`lts_twotrans.shader:674`).
7. **Refraction samples the background:** `GrabPass {"_lilBackgroundTexture"}`
   (`lts_ref.shader:738`), queue `Transparent-100`. The fragment computes
   `refractUV = uvScn + fresnel·_RefractionStrength·N.xy` and
   `fd.col.rgb = lerp(refractCol, fd.col.rgb, fd.col.a)`
   (`Shader/Includes/lil_common_frag.hlsl:1272-1292`). RefractionBlur adds a second `GrabPass {}`
   and a FORWARD_BLUR pass. Gem is additive: `Blend One One, Zero One`, `ZWrite=0`, `Cull=0`,
   GrabPass, FORWARD_PRE pass (`lts_gem.shader:578-592,628,745-790`).
8. **Fur generates shells:** the fur pass extrudes vertices by `_FurVector` (optionally blended
   from vertex color/texture, with gravity) and emits `_FurLayerNum`-scaled shell strips
   (1–3, default 2) from a **geometry shader** — `AppendFur`/`outStream` strips, with a final
   shell appended unconditionally after the layer branches
   (`Shader/Includes/lil_common_vert_fur.hlsl:169-195,477-502`, `lts_fur.shader:619`). Fur blends
   `SrcAlpha/OneMinusSrcAlpha` with `_FurZWrite=0`. FurCutout is LIL_RENDER 1 with `One/Zero` +
   `_FurZWrite=1`. FurTwoPass adds `_PRE` shell passes (`lilMaterialUtils.cs:213-248`,
   `lts_furonly_two.shader` `UsePass` list).
9. **Lite shares the alpha equation shape:** `Shader/Includes/lil_pass_forward_lite.hlsl`
   contains the same `fd.col.a = 1.0` (opaque) / `clip(fd.col.a - _Cutoff)` (cutout) structure
   (`:133-139,175-181`) in its own pass assets with `LIL_RENDER 0/1/2` per asset
   (`ltspass_lite_cutout.shader:155`).
10. **Regular cutout alpha path:** `clip(fd.col.a - _Cutoff)` behind `LIL_RENDER` branching and
    optional feature conditionals (`LIL_FEATURE_ALPHAMASK/DISSOLVE/DITHER`) baked at callback 100
    (`Shader/Includes/lil_pass_forward_normal.hlsl:236,362,369,388,394-411`) — the controlling
    investigation's §9 basis, re-verified.

## 7. Applicability analysis

### 7.0 Alpha-one sufficiency per mode class

| Mode class | Is alpha ≡ 1 sufficient? | Reason |
|---|---|---|
| Cutout (any representation, no outline) | Necessary, and sufficient **for the fragment equation** only with the B2 proof | Above-cutoff fragments are not automatically opaque: `_AlphaToMask=1` on cutout (`lilMaterialUtils.cs:98`) makes alpha feed per-sample coverage under MSAA, and optional alpha mask/dither/dissolve paths modify coverage. The proof must establish alpha ≡ 1 over the whole sampled domain and refuse feature-dependent paths. |
| Transparent Normal | Necessary for blend degeneration, not sufficient alone | At `a≡1`, `One/OneMinusSrcAlpha ≡ One/Zero`. Remaining questions are depth/queue/order and alpha-path features (fade/dither/dissolve). |
| Transparent OnePass / TwoPass | Necessary, not sufficient | OnePass has no FORWARD_ADD (target changes lighting participation). TwoPass adds FORWARD_BACK participation and the vendor skips `_ZTest=4` for it (`lilMaterialUtils.cs:277-280`). |
| Refraction / RefractionBlur | Necessary, not sufficient | At internal `a≡1` the lerp discards the background sample (`lil_common_frag.hlsl:1292`), but queue 2900 + GrabPass/order semantics and the LIL_RENDER 2 alpha machinery remain. |
| Fur, FurCutout, FurTwoPass | Not sufficient, irrelevant | Defining geometry is generated shells. Base alpha does not describe appearance. |
| Gem | Not sufficient, irrelevant | `Blend One One` additive background sparkle is defining. Alpha does not gate it. |
| FakeShadow | Not sufficient, irrelevant | `SrcBlend=DstColor` multiplicative shadowing is defining (`lts_fakeshadow.shader:18-19`). |
| Multi | Necessary per its effective mode, not sufficient | Effective mode is keyword-carried. Lifecycle proof needed (§7.13). |

### 7.1 Regular opaque (`lilToon`) — **A: Already opaque / target-only**

- Evidence: the LIL_RENDER 0 pass asset forces alpha to one and compiles out the alpha path
  (B1 `[MEASURED]` pass define. `LilToonMaterialSemantics` premise in the controlling
  investigation §4). Queue 2000, `RenderType=Opaque` (`lts.shader:638-644`).
- Role in mixed animation-reachable sets: an attested `lilToon` material is `AlreadyOpaque` and
  maps to itself with no clone (existing preparation machinery, controlling investigation §3 item
  4). In a slot that mixes a cutout source with an opaque sibling, the opaque sibling joins the
  admitted-set completeness check without conversion. **Target-only**: it is the conversion target
  of the first slice.
- Missing proof: none. B1 already measured its identity and digests. It is never a separation
  source.

### 7.2 Regular cutout, no outline (`Hidden/lilToonCutout`) — **P: Primary support candidate**

- Evidence: B1 measured the exact installed source/target identities, digests, pass
  relationships (`Hidden/ltspass_cutout` with `LIL_RENDER 1`), clone/assignment behavior, and
  queue and recipe read-backs (`[MEASURED]`, B1 §§5-9). The cutout equation is
  `clip(fd.col.a - _Cutoff)` (`lil_pass_forward_normal.hlsl:236`).
- Opportunity: triangles whose entire sampled alpha domain is 1 move to the appended submesh on
  the attested `lilToon` opaque target.
- Preservation risk: alpha-mask/dither/dissolve generated paths and `_AlphaToMask` coverage
  (§7.0).
- Exact missing proof: **B2** — the conservative cutout alpha theorem with its
  callback-independence boundary (refuse alpha mask/dither/dissolve unless made
  compilation-invariant). This is the only gate between P status and the first production slice.
- Target preservation check: the vendor's own Opaque branch maps `ltsc → lts`
  (`lilMaterialUtils.cs:58-62`). B1 measured the assignment preserves all 18 recipe properties
  and reads back the canonical state (`[MEASURED]`).
- Next action: B2 characterization and semantic design (already commissioned, in flight on an
  unmerged design branch).

### 7.3 Regular cutout + outline (`Hidden/lilToonCutoutOutline`) — **C: Conditional**

- Evidence: the vendor opaque target is `Hidden/lilToonOutline`, not `lilToon`
  (`lilMaterialUtils.cs:93`). Outline passes are full independent passes with complete
  `_Outline*` state (§6.5). Converting an outline source to plain `lilToon` silently drops the
  outline pass (controlling investigation §10) — this is not a preservation move.
- Opportunity: the cutout alpha theorem shape transfers (same pass asset `ltspass_cutout`, same
  clip site). The appended submesh would carry an outline-capable opaque target.
- Preservation risks: (a) target attestation does not exist (`Hidden/lilToonOutline` + its
  `SHADOW_CASTER_OUTLINE`-differing pass set, `lts_o.shader:641-646`). (b) outline state recipe
  extension (`_Outline*` fields are absent from the first-slice recipe by design).
  (c) submesh-boundary seams: appended submeshes reference the same vertices/normals, so vertex-
  extrapolated outline surfaces should stay continuous **if** the outline properties clone
  identically — but this needs proof, including per-pass draw order across submeshes and
  `_OutlineVectorTex`-style discontinuities.
  (d) **the outline pass has its own alpha channel**: outline fragments take alpha from
  `_OutlineTex` and `fd.col.a *= _OutlineColor.a`
  (`Shader/Includes/lil_common_frag.hlsl:362-398`), the cutout outline path clips/discards on it
  (`lil_pass_forward_normal.hlsl:232-234`), and the opaque target forces `fd.col.a = 1.0`
  (`:228-229`) — so a triangle proven opaque on its **base** texture can still carry a clipped
  or alpha-faded outline that becomes solid after conversion. The outline proof needs its own
  alpha theorem or gate, not just cloned `_Outline*` scalars.
- Grouping note: the cutout theorem is shared with §7.2, but this identity is **not** grouped into
  the first slice — the counterexample is the target: no official opaque target preserves outline
  without its own attestation.
- Exact missing proofs: source attestation (`lts_cutout_o`), target attestation (`lts_o`),
  outline-aware recipe, seam/pass characterization, **and an outline-alpha theorem/gate covering
  `_OutlineTex.a`/`_OutlineColor.a` coverage semantics**.
- Next action: separate outline source/target attestation + seam characterization investigation.

### 7.4 Regular transparent Normal (`Hidden/lilToonTransparent`) — **C: Conditional**

- Evidence: LIL_RENDER 2, blend `One/OneMinusSrcAlpha`, queue 2460, `_ZWrite` default 1, in a
  four-pass container (FORWARD/FORWARD_ADD/SHADOW_CASTER/META)
  (`lts_trans.shader:579-592,673`).
  At `a≡1`, `One/OneMinusSrcAlpha` degenerates exactly to `One/Zero` (§7.0), so a
  proven-opaque triangle's own output is blend-equivalent to opaque.
- Preservation risks: depth/queue/order — moving triangles from 2460 to a 2000 canonical submesh
  changes draw order relative to other 2460+ materials (guarded by refusing non-canonical blend/
  depth states, as Poiyomi does). The transparent alpha equation is richer (fade/dither/dissolve
  layers, `fd.col.a = 1.0` branches) than cutout. FORWARD_ADD participation must carry over (it
  exists on both source and plain-opaque target).
- Vendor-target check: the vendor maps `ltst → lts` for Opaque (`lilMaterialUtils.cs:130-132`) —
  the official target exists and preserves lighting passes. Alpha-one blend degeneration is
  arithmetic, but the **proof** (texture/animation evidence over the LIL_RENDER 2 alpha path) is
  strictly larger than B2.
- Exact missing proof: a transparent alpha semantics investigation (a B2 successor), plus the
  blend/depth gate set and callback independence for the same optional alpha features.
- Next action: regular transparent Normal semantics investigation, after B2.

### 7.5 Regular transparent OnePass (`Hidden/lilToonOnePassTransparent`) — **C: Conditional (target-identity blocked)**

- Evidence: the container `UsePass`es only FORWARD/SHADOW_CASTER/META — **no FORWARD_ADD**
  (`lts_onetrans.shader:674-676`). OnePass intentionally removes per-pixel additive lighting.
- Counterexample to grouping with Normal: the official opaque target `lilToon` **has**
  FORWARD_ADD. Moving proven triangles to it would **add** additive lighting the source
  deliberately lacks — a visual change under multi-light conditions that no official no-additive
  opaque asset preserves (none exists in the inventory).
- Exact missing proofs: alpha semantics shared with §7.4, **plus** a target decision:
  `[DECISION NEEDED]` — either accept `lilToon` (documenting additive-light divergence as within
  policy, which contradicts current preservation rules), or authorize an AMUSE-generated
  no-additive opaque target (new attestation surface), or refuse this identity.
- Next action: fold into the transparent investigation as an explicit target-identity
  sub-question.

### 7.6 Regular transparent TwoPass (`Hidden/lilToonTwoPassTransparent`) — **C: Conditional, likely refusal-leaning**

- Evidence: this identity adds the FORWARD_BACK back-face pre-pass (§6.6). The vendor skips
  `_ZTest=4` reset for TwoPass (`lilMaterialUtils.cs:277-280`), so the vendor itself treats
  TwoPass `_ZTest` as non-canonical. Queue 2460.
- Preservation risk: proven triangles that leave the source submesh also leave FORWARD_BACK
  participation. Interactions between the remaining back-face pass and the moved opaque triangles
  (self-occlusion order) change. The first-slice recipe's unconditional `_ZTest=4` conflicts with
  a source whose visibility may depend on a different comparison (the controlling investigation
  §6 already refuses this).
- Exact missing proof: a pass-level characterization that shows moved-triangle behavior is
  invisible under FORWARD_BACK removal. Absent that proof, formal refusal stands.
- Next action: include as a scope row in the transparent investigation. Default outcome refusal.

### 7.7 Regular transparent outline variants (`Hidden/lilToonTransparentOutline`, OnePass/TwoPass outline) — **C: Conditional, deferred behind 7.3 and 7.4-7.6**

- Evidence: outline passes compose with transparent passes
  (`lts_trans_o.shader:674-680`). Outline blend defaults `SrcAlpha/OneMinusSrcAlpha` on
  transparent-outline containers (`lts_trans.shader` `_OutlineSrcBlend=5`).
- Missing proofs: everything in §7.3 plus everything in the corresponding transparent
  investigation. Outline alpha behavior (`_OutlineAlphaToMask`, `_OutlineSrcBlend`) becomes
  proof-relevant.
- Next action: strictly downstream of the outline (7.3) and transparent (7.4-7.6) investigations.

### 7.8 Outline-only ×3 (`_lil/[Optional] lilToonOutlineOnly*`) — **A/R split**

- `lts_oo` (opaque outline-only): renders only `ltspass_opaque` FORWARD_OUTLINE +
  FORWARD_ADD_OUTLINE at Geometry queue (LIL_RENDER 0). This is already-opaque rendering, not an
  alpha-separation source. **A** (target-only relevance: none. Mapping: itself).
- `lts_cutout_oo`, `lts_trans_oo`: outline passes clipped by the cutout/transparent alpha path —
  the alpha carve is the defining appearance. **R** (refusal candidate): no base surface exists
  to separate. The material *is* outline geometry.
- Reconsideration condition: only if AMUSE ever defines an outline-only representation concept —
  out of current scope.

### 7.9 Lite ×10 (`Hidden/lilToonLite…`) — **C: Conditional (per mode), opaque mapping A**

- Evidence: Lite shares the alpha equation shape in its own pipeline (§6.9): opaque/cutout/
  transparent modes with the same `LIL_RENDER` semantics, a reduced feature set, and separate
  pass assets (`ltspass_lite_*`) that are **not** manager fields and carry their own digests.
- Counterexample to "Lite ≡ regular": different pass assets and a reduced feature set mean
  AMUSE's existing `lilToon` attestation does not cover any Lite identity. Alike equations do not
  make the attestations interchangeable.
- Which differences need independent proof: the Lite feature set actually compiled (which
  optional alpha paths exist in Lite), Lite source/target digests, and whether any Lite-only
  simplification removes alpha-affecting paths regular has.
- Disposition per mode: Lite opaque **A** (target mapping under a future Lite attestation).
  Lite cutout **C** (theorem shape shared. Attestation + recipe separate). Lite transparent
  **C** (inherits §7.4-7.6 questions in the Lite pipeline). Lite outline **C** deferred behind
  7.3.
- Next action: single bounded "Lite pressure" investigation after the regular cutout slice
  exists.

### 7.10 Tessellation ×10 (`Hidden/lilToonTessellation…`) — **C: Conditional (geometry preservation)**

- Evidence: tess containers `UsePass` `ltspass_tess_*` assets. Hull/domain stages and
  displacement are in the tess pipeline. Declared queues mirror regular exactly (§4.1).
- Analysis: base-triangle classification transfers to subdivided fragments if displacement is
  identical between the cutout source and opaque target variants (same tessellation feature code
  and properties. A subdivided fragment's UV domain sits inside its base triangle's UV domain, so
  a base-domain alpha proof covers subdivided samples). The correct target is a tessellation
  opaque shader (`ltstess`), which preserves tessellation on the appended submesh.
- Counterexample to "tess ≡ regular + tess": the appended-submesh machinery must reference the
  same vertices while the tess shader displaces per-vertex — this needs proof that submesh
  splitting does not change hull adjacency/winding behavior at the split boundary.
- Exact missing proofs: tess source+target attestation, a displacement-equality argument,
  geometry/pass characterization.
- Next action: Tessellation geometry-preservation investigation, independent of cutout rollout
  sequencing but after B2 establishes the theorem template.

### 7.11 Refraction and RefractionBlur (`Hidden/lilToonRefraction`, `Hidden/lilToonRefractionBlur`) — **R: Explicit refusal candidates (default), narrow-state reconsideration path**

- Evidence: LIL_RENDER 2 + GrabPass + queue 2900 (§6.7). The fragment output is
  `lerp(refractCol, baseColor, alpha)` — **at internal alpha ≡ 1 the background term is
  mathematically discarded**, so a proven-opaque triangle's output converges to the base color.
  This is a real narrow state, not a guess. However: (a) proving internal alpha ≡ 1 needs the
  full LIL_RENDER 2 alpha machinery (heavier than cutout). (b) the material renders at queue 2900
  (`Transparent-100`), **before** Transparent-queue (3000) materials but after every ≤2460 queue,
  and the `GrabPass` semantics (what the sampled background contains, ordering against other
  2900+ materials and other avatars' GrabPass materials) stay representation-relevant even when
  individual triangles are opaque. (c) RefractionBlur adds a second GrabPass and a blur pass
  whose only purpose is to feed refraction sampling.
- Is the narrow state meaningful? Unknown without characterization — avatar refraction exists
  precisely to show a distorted background (alpha < 1 in practice). `[INFERENCE]` the population
  of alpha≡1 refraction triangles is likely small, but this is not evidence.
- Exact missing proofs: LIL_RENDER 2 alpha-equivalence semantics. Queue/GrabPass ordering
  characterization. RefractionBlur blur-pass invariance. Absent all three, refusal stands.
- `[DECISION NEEDED]` whether the controller ever wants this investigation commissioned. F0's
  recommendation is formal refusal unless a real-avatar need appears.
- Reconsideration condition: a characterized population of alpha≡1 refraction materials plus an
  ordering-safety proof.

### 7.12 Fur, FurCutout, FurTwoPass (+ Fur-only ×3, `ltsmfur`) — **R: Explicit refusal candidates**

- Evidence: appearance is generated shell geometry — `_FurLayerNum` (1–3) geometry-shader-emitted
  shells extruded along `_FurVector` with gravity (§6.8). Fur mode blends
  `SrcAlpha/OneMinusSrcAlpha` with `_FurZWrite=0` at queue 3000. FurCutout is LIL_RENDER 1, but
  the shells still define the look. FurTwoPass adds `_PRE` shell passes.
- Why alpha separation does not apply: moving base triangles to an appended submesh **removes
  them from the fur pass's source triangles** — their shells disappear, leaving a visible fur
  gap. Proving the base surface opaque does not describe fur appearance at all.
- Counterexample to "FurCutout is cutout": identical LIL_RENDER 1 alpha machinery, yet refused —
  render mode alone does not decide disposition.
- Reconsideration condition: a future representation proof that can reason about the generated
  shell geometry **as rendered** (for example, separating whole-fur renderers as a unit) — a
  separate, explicitly scoped investigation, not a byproduct of the cutout work. There is no
  shell-free material state to prove: `_FurLayerNum` is `Range(1,3)`, and the geometry shader
  appends a final shell unconditionally (`lil_common_vert_fur.hlsl:477-502`), so even zero-length
  fur still renders overlapping blended shells whose removal is not provably invisible.
- Fur-only ×3: shell-only materials (no base pass). Same refusal basis, stronger (they render
  shells only, so there is no base surface at all).

### 7.13 Multi ×5 (`_lil/lilToonMulti`, Outline/Refraction/Fur/Gem) — **C: Conditional (lifecycle-gated)**

- Evidence: one shader asset carries all modes. The effective mode is keyword-carried, derived at
  edit time from `_TransparentMode` (§6.3). Callback 100 also enables three
  animation-derived keywords (`§6.4`) — those three are color features (rim direction, hue
  variation, gradation), **not alpha-affecting** `[INFERENCE]`. Mode keywords are not re-derived
  at build. `_TransparentMode` itself is animatable as a float, but no runtime HLSL reads it.
- Consequences: (a) an NDMF-time observation of a Multi material's keywords and blend state sits
  close to the upload state — mode keywords seen at NDMF persist to upload. (b) but keyword
  derivation is *behavior that happened before NDMF ran*, and `_AsOverlay`-driven pass enables
  (`§6.3`) may disable shadow/depth passes — AMUSE must capture pass-enable state as material
  state. (c) Multi Fur/Refraction/Gem inherit their families' refusals (§7.11, §7.12, Gem §7.14).
- Lifecycle classification: cutout-mode Multi **may** be NDMF-provable (mode keywords persist.
  Callback-100 additions are alpha-irrelevant) — this is a bounded `[INFERENCE]` that must be
  earned by characterization, not assumed. If any alpha-relevant keyword or pass-enable proves
  callback-derived, upload-time late validation (Outcome B) becomes a prerequisite.
- Exact missing proofs: Multi keyword/pass-enable lifecycle characterization. Multi source
  attestation (single asset, no per-asset LIL_RENDER — a different attestation shape). Mode-
  consistency gates (keyword state must agree with `_TransparentMode` and blend state).
- Next action: Multi lifecycle and mode-behavior investigation, after the regular slice.

### 7.14 Gem (`Hidden/lilToonGem`, `ltsmgem`) — **R: Explicit refusal candidate**

- Evidence: additive `Blend One One, Zero One`, `ZWrite=0`, `Cull=0`, GrabPass, FORWARD_PRE pass
  (§6.7). The gem look is additive accumulation of background/sparkle over whatever is behind. No
  alpha state degenerates `One/One` to `One/Zero` while preserving the effect (the added
  contribution IS the appearance).
- Reconsideration condition: none realistic under the current AMUSE concept. This would need a
  different transformation class (for example, proving an additive contribution is zero and
  dropping the material — a different feature).
- Vendor note: the vendor's own Opaque branch writes `_ZWrite=1`/skips for Gem handling
  (`lilMaterialUtils.cs:268-272`) — Gem's depth state is exceptional even to the vendor.

### 7.15 Fake shadow (`_lil/[Optional] lilToonFakeShadow`) — **R: Explicit refusal candidate**

- Evidence: `SrcBlend=DstColor(2), DstBlend=Zero(0)` — multiplicative darkening, queue 2505,
  minimal single pass (`lts_fakeshadow.shader:18-19,56`). The material exists to multiply the
  framebuffer beneath a shadow blob.
- Why refusal: multiplicative blending is the entire behavior. Alpha separation has no opaque
  degeneration (a "proven opaque" fake-shadow triangle is black-by-multiplication, not opaque).
- Reconsideration condition: none under the current concept.

### 7.16 Overlay ×4 (`_lil/[Optional] lilToonOverlay(OnePass)`, LiteOverlay(OnePass)) — **C: Conditional, deferred, refusal-leaning**

The four identities are not one condition — the pass sets and pipelines differ exactly where the
transparent family's target problems live:

- `ltsover` (regular overlay): `UsePass`es `Hidden/ltspass_transparent` FORWARD + FORWARD_ADD at
  queue 2460 (`lts_overlay.shader:673-676`) — Normal-transparent-class. It inherits §7.4's alpha
  semantics and gate work. Missing proofs: §7.4's theorem plus an overlay-purpose target decision
  (`[DECISION NEEDED]` §13.4).
- `ltsoover` (regular overlay OnePass): FORWARD only, **no FORWARD_ADD**
  (`lts_overlay_one.shader:673-674`) — OnePass-class. It inherits §7.5's additive-light target
  problem on top of §7.4. Missing proofs: §7.5's target decision plus §7.4's theorem.
- `ltslover`, `ltsloover` (Lite overlays): consume `Hidden/ltspass_lite_transparent` FORWARD
  (+FORWARD_ADD for `ltslover`. FORWARD only for `ltsloover`) — the Lite attestation surface per
  §7.9 **and** the corresponding Normal/OnePass class questions.
- All four: the intended behavior is overlay layering over the base avatar surface (typically on
  separate meshes). Whether overlays are ever separable base surfaces is a product question.
- Next action: fold into the transparent investigation's scope rows split by class. Default
  outcome "not a support target" without an explicit controller decision.

### 7.17 Editor/support shaders (baker, bakeramp, proponly, dummy) and pass assets — **I: Internal/helper**

- `Hidden/ltsother_baker`, `Hidden/ltsother_bakeramp`, `Hidden/ltspass_proponly` are consumed
  only by editor bake/optimization tools (§4.2). `Hidden/ltspass_dummy` is a trivial placeholder
  pass for container authors with no package consumer. The nine pass assets are evidence
  dependencies (attestation targets), not materials.
- None is an avatar-material family. None receives an applicability disposition beyond I.
- AMUSE implication: source-evidence capture must treat pass assets as attestation evidence
  (as `Hidden/ltspass_opaque` already is), never as conversion sources or targets.

### 7.18 Disposition summary

Exact per-identity enumeration (52 material-entry identities):

- **P (1):** `ltsc`.
- **C (29):** `ltsco`, `ltst`, `ltsot`, `ltstt`, `ltsto`, `ltsoto`, `ltstto`, `ltslc`,
  `ltslt`, `ltslot`, `ltsltt`, `ltslco`, `ltslto`, `ltsloto`, `ltsltto`, `ltstessc`,
  `ltstessco`, `ltstesst`, `ltstessot`, `ltstesstt`, `ltstessoto`, `ltstessto`, `ltstesstto`,
  `ltsover`, `ltsoover`, `ltslover`, `ltsloover`, `ltsm`, `ltsmo`.
- **A (7):** `lts`, `ltso`, `ltsoo`, `ltstess`, `ltstesso`, `ltsl`, `ltslo`.
- **R (15):** `ltsref`, `ltsrefb` (formal-refusal default per §7.11), `ltsfur`, `ltsfurc`,
  `ltsfurtwo`, `ltsfuro`, `ltsfuroc`, `ltsfurotwo`, `ltsgem`, `ltscoo`, `ltstoo`, `ltsfs`,
  `ltsmref`, `ltsmfur`, `ltsmgem`.
- **I (13):** all internal/pass/helper shaders (§4.2).

Reconciliation: 1 + 29 + 7 + 15 = 52 material-entry identities. +13 internal = 65 total.
**U (0)** — no family lacks a disposition. Two `[DECISION NEEDED]` markers live inside C/R rows
(OnePass target §7.5, refraction commissioning §7.11), plus the scope confirmations in §13.

## 8. Source-to-opaque-target map

For each material-entry identity: the vendor's official opaque target
(`lilMaterialUtils.cs:38-264`) and whether that target preserves the family's defining behavior.

| Source identity | Vendor opaque target | Preserves defining behavior? |
|---|---|---|
| `lilToon` | itself | — (already opaque) |
| `Hidden/lilToonCutout` | `lilToon` | Yes — B1-measured. The first slice's target. |
| `Hidden/lilToonTransparent` (+OnePass/TwoPass) | `lilToon` | Blend/depth only at vendor level. OnePass loses its no-additive property. TwoPass loses back-pass interaction. Not preserving as-is. |
| `Hidden/lilToonCutoutOutline` | `Hidden/lilToonOutline` | Outline preserved only via the outline target asset + outline recipe. Requires attestation. |
| `Hidden/lilToonOutline` (opaque outline) | itself | — (already opaque, outline-capable target) |
| Transparent-outline ×3 | transparent-outline opaque counterpart (`Hidden/lilToonOutline` family per mode map) | Same conditions as §7.7. |
| Lite ×10 | Lite opaque counterpart (`ltsl`/`ltslo`) | Preserves Lite pipeline. Requires Lite attestation. |
| Tessellation ×10 | Tess opaque counterpart (`ltstess`/`ltstesso`) | Preserves tessellation. Requires geometry proof + attestation. |
| `Hidden/lilToonRefraction` / `RefractionBlur` | plain opaque asset via the Opaque branch (`lilMaterialUtils.cs:38-62` — no specialized identity exists for these modes) | **Not preserving**: the Opaque dispatch leaves the refraction pipeline entirely — background sampling, blur pass, queue 2900 all discarded. Refused. |
| Fur / FurCutout / FurTwoPass | plain opaque asset via the Opaque branch (`lilMaterialUtils.cs:38-62`) | **Not preserving**: drops the fur shell pipeline wholesale. Refused. |
| `Hidden/lilToonGem` | plain opaque asset via the Opaque branch (`lilMaterialUtils.cs:38-62`) | **Not preserving**: drops the additive gem pipeline. Refused. |
| Multi | `ltsm`/`ltsmo` with `RenderType=""`/queue −1 (`:46-52`) | Multi "opaque" is keyword/property state on the same asset — a Multi cutout proof would target AMUSE canonical state, not a different asset. |
| Outline-only / Fur-only / Overlay / FakeShadow | no vendor mode-conversion entry | n/a — not conversion sources. |

Key structural fact: **for specialized modes (Refraction, RefractionBlur, Fur, Gem), the vendor's
own Opaque dispatch (target mode = Opaque, `lilMaterialUtils.cs:38-62`) selects the plain opaque
asset for the material's representation axes — discarding the specialized pipeline entirely.
The vendor assigns the specialized assets only when the *target* mode is specialized (`:181-264`).
The vendor's opaque target exists, then, but it is not preservation-safe for any specialized
source. AMUSE cannot borrow it, and no official specialized-preserving opaque target exists.
Any future positive support would need AMUSE-generated targets, which the current policy does
not authorize (`[DECISION NEEDED]` only if a family ever graduates from refusal).**

## 9. Callback and lifecycle pressure

- Callback 100 (after NDMF. `VRChatModule.cs:22,60-61`) regenerates shader files per project
  settings and runs Multi keyword derivation from animation clips. Consequences per family:
  - **Cutout/transparent proofs** must stay invariant under `LIL_FEATURE_*` regeneration — the
    controlling investigation's §9 obligation transfers unchanged to every non-opaque family's
    alpha proof (F0 adds: it also applies to Lite and Tess, whose pass assets carry the same
    feature conditionals).
  - **Multi**: mode keywords persist from edit time. Callback adds only the three
    animation-derived color-feature keywords (`[SOURCE]` §6.4), which are alpha-irrelevant
    `[INFERENCE]`. Multi cutout may be NDMF-provable, but the claim must be earned by
    characterization of `_AsOverlay` pass-enables and keyword/material consistency.
  - **Already-opaque mapping** is callback-safe: identity/queue/tag of attested assets stay
    stable. Regeneration changes file contents, not asset identity (B1/controlling §9).
- No family disposition in §7 assumes behavior after callback 100 that NDMF-time evidence cannot
  support. Where it could matter (Multi), the row says so explicitly.

## 10. Current AMUSE capability pressure

From the merged controlling investigation (§4, §12, §13), unchanged by F0:

- Family selection is exact-shader-name. Only `"lilToon"` is attested. Every other lilToon
  identity fails the renderer's material-dependency closure and refuses renderer-wide today.
- Attestation pins one pass identity (`Hidden/ltspass_opaque`) and `OpaqueRenderMode 0`.
  `InterpretVerifiedAlpha`'s constant-1 theorem holds only under LIL_RENDER 0.
- The conversion core, evidence-request routing, admission, and overwrite rule are
  Poiyomi-hard-coded (R1). The test seam is Poiyomi-typed (R6).
- B2 (cutout alpha semantics) is the single gate to the first lilToon production slice. F0 does
  not change its scope or priority — it confirms it.

## 11. Dependency-ordered roadmap

Derived from the matrix. Bounded tasks, no mega-task. "Gates a vertical increment" means
production work cannot start for that family before the row's deliverable exists.

| # | Task | Family scope | Prerequisite | Question to answer | Why independent | Gates production? | Durable deliverable |
|---|---|---|---|---|---|---|---|
| 1 | **B2 cutout alpha semantics** (in-flight, unmerged) | regular no-outline cutout | B1 (done) | Conservative cutout alpha theorem. Callback-independence boundary | The template for every later alpha proof | Yes — first lilToon slice | Reviewed semantic design + falsifiers |
| 2 | Regular no-outline cutout conversion implementation | same | B2 | — (production, not F0 scope) | — | — | `LilToonOpaqueConversion` + R1-R6 parameterization |
| 3 | Cutout-outline attestation + seam characterization | `lts_cutout_o` → `lts_o` | Task 2 | Does submesh splitting preserve outline continuity. Outline-alpha theorem/gate (`_OutlineTex.a`×`_OutlineColor.a` coverage vs the opaque target's forced alpha 1). Outline source/target attestation. Recipe extension | Needs the cutout theorem but not transparent work | Yes — outline vertical increment | Attestation pins, seam characterization, outline-alpha gate, outline recipe extension design |
| 4 | Regular transparent Normal semantics | `lts_trans`. Overlay `ltsover` (Normal-class, §7.16, also gated on §13.4) | Task 1 (theorem template) | LIL_RENDER 2 alpha evidence. Blend/depth/queue gates. Callback independence | Independent of outline. Shares only the theorem shape | Yes — transparent vertical increment | Transparent semantic design + gate set |
| 5 | OnePass/TwoPass transparent target + pass characterization | `lts_onetrans`, `lts_twotrans`. Overlay `ltsoover` (OnePass-class, §7.16, also gated on §13.4) | Task 4 | OnePass target identity (additive divergence vs AMUSE target). TwoPass FORWARD_BACK invariance or refusal | Needs Normal's alpha work. Adds pass-structure questions | Only after a `[DECISION NEEDED]` on the OnePass target | Target decision record. TwoPass characterization (expected refusal) |
| 6 | Lite cutout/opaque pressure | `ltsl`, `ltslc` | Task 1/2 (template) | Which Lite alpha paths exist. Lite attestation shape. Recipe deltas | Own pipeline assets. Independent of outline/regular-transparent timing | Yes for Lite cutout later | Lite attestation pins + delta analysis |
| 6b | Lite transparent + outline pressure | `ltslt`, `ltslto`, `ltslot`, `ltsloto`, `ltsltt`, `ltsltto`. Lite overlays `ltslover`, `ltsloover` (§7.16, also gated on §13.4) | Tasks 4/5 **and** 6 | Lite OnePass no-additive target problem (`ltsl_onetrans.shader`: FORWARD/SHADOW_CASTER/META, no FORWARD_ADD). Lite TwoPass back-pass. Lite outline composition | Inherits regular transparent pass/target semantics. Cannot precede them | Yes for Lite transparent later | Lite transparent target decision + pass characterization |
| 7 | Multi lifecycle and mode behavior | `ltsm`, `ltsmo` | Task 2 | NDMF-time keyword/pass-enable persistence. Mode-consistency gates. Outcome B need | Single-asset attestation shape differs from all others | Yes for any Multi support | Lifecycle characterization + gate design |
| 8 | Tessellation geometry preservation | `ltstess*` cutout first | Task 1 | Displacement equality across variants. Submesh-split boundary behavior under hull/domain | Independent of transparent/outline | Yes for Tess support | Geometry characterization + tess attestation plan |
| 9 | Specialized-mode formal refusals | Gem, Fur ×3, fur-only ×3, fake shadow, multi fur/gem/ref | none (source-only) | Record refusal basis with reconsideration conditions as policy entries | Pure documentation. No measurement needed | No — documents non-support | Refusal records in support-policy doc |
| 10 | Refraction narrow-state decision | `ltsref`, `ltsrefb` | controller `[DECISION NEEDED]` | Whether to characterize the alpha≡1 narrow state + GrabPass ordering, or formalize refusal | Independent of all other rows | No unless commissioned | Either a characterization note or a formal refusal record |

Ordering rationale: alpha-proof machinery must exist before it can be reused (1 → 4/6/6b/8).
Outline needs target attestation work unrelated to transparent ordering (3 is independent of 4).
Lite transparent (6b) must follow regular transparent (4/5), because it inherits their target and
pass questions. Multi lifecycle sits orthogonal to alpha equations (7 follows 2 only because
Multi cutout would reuse its recipe). Refusals (9) have no upstream dependency and can land at
any time.

## 12. Likely refusals and reconsideration conditions (consolidated)

| Family | Refusal basis | Reconsideration condition |
|---|---|---|
| Gem (+Multi Gem) | additive `One/One` background accumulation is defining. `ZWrite=0`, GrabPass | New transformation class for additive-contribution proofs |
| Fur/FurCutout/FurTwoPass (+fur-only ×3, Multi Fur) | generated geometry-shader shell geometry. Separation strips shells from proven triangles | Representation proof for the shell geometry as rendered (explicitly scoped future work) |
| FakeShadow | multiplicative `DstColor` blending is defining | None under current concept |
| Outline-only cutout/transparent | alpha-carved outline is the entire material | Outline-only representation concept |
| Refraction/RefractionBlur (default) | queue-2900 GrabPass representation + unproven alpha≡1 narrow state | Characterized narrow-state population + ordering-safety proof |
| TwoPass transparent (expected) | FORWARD_BACK participation + vendor `_ZTest` exception | Pass-invariance characterization proving invisibility |

## 13. Unresolved controller decisions

1. `[DECISION NEEDED]` OnePass transparent target (§7.5): accept additive-light divergence,
   authorize an AMUSE no-additive opaque target, or refuse.
2. `[DECISION NEEDED]` Refraction narrow-state investigation: commission it or formalize refusal
   (§7.11, roadmap row 10).
3. `[DECISION NEEDED]` `.lilcontainer`-generated shader identities: confirm they stay out of
   official support scope indefinitely, or schedule a policy investigation (§4.3).
4. `[DECISION NEEDED]` Overlay ×4 (§7.16): confirm "not a support target" or schedule the
   transparent follow-on row.

None of these blocks roadmap rows 1-3 or 9.

## 14. What F0 proves and does not prove

**Proves:** the closed 65-asset official inventory and its classification. Per-family
dispositions grounded in pinned source. The non-preservation-safety of the vendor's opaque
targets for specialized modes. The Multi callback keyword facts. The
outline/two-pass/refraction/fur/gem structural facts that shape future proofs. A
dependency-ordered roadmap with bounded tasks.

**Does not prove:** any non-cutout family's alpha semantics. Any attestation digest beyond B1's.
Multi's lifecycle claims (bounded inference, uncharacterized). Tessellation displacement equality
(argument, not measurement). Refraction narrow-state population. That any C-family will ever
reach positive support.

## 15. Is F0 discharged?

**Yes.** The inventory is closed and mechanically reconciled (52 + 13 = 65. The count accounts
for every manager field and every asset). Every material-entry family carries an evidence-backed
disposition with explicit missing proofs or refusal bases. The roadmap is dependency-ordered and
bounded. This investigation performed no production design, digest pinning, or implementation.
F0 does not gate on the `[DECISION NEEDED]` items, which are future-scope choices, not inventory
or roadmap gaps.

## 16. Exact next recommended investigation

**B2 completion** — the cutout alpha-semantics design (already commissioned, in flight on the
unmerged `design/liltoon-cutout-alpha-semantics` branch), carrying the callback-independence
obligation and its falsifier from the controlling investigation §9/§14. F0 adds one requirement
to it: state explicitly whether it records its theorem template, so roadmap rows 4, 6, 6b, and 8
can reuse the shape without re-deriving it. No other row should start first: every other positive
path depends on the theorem template or on production prerequisites.
