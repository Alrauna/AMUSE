# lilToon Opaque Conversion — Investigation

## 1. Scope and question

This investigation asks one question. Can AMUSE add a lilToon opaque conversion to the
merged Poiyomi alpha-separation vertical slice in the smallest correct way? Does the
slice's declared extension boundary survive contact with the materially different
lilToon 2.3.4 family? The plan states that boundary as: "one lilToon conversion evidence
request in `CaptureRequestForFamily`, one lilToon conversion implementation, one new
`case`" (plan §Recorded future refactor pressure item 2).

This is an investigation, not an implementation plan. It records verified facts, the
pinned upstream recipe, demonstrated refactoring pressure, blockers, and the next
decision. It creates no design and no production code.

The text reconciles answered coordinator questions inline. §16 summarizes them.

Labels:
- `[SOURCE]` — read from pinned repository or upstream source.
- `[MEASURED]` — observed by an executed probe (prior recorded probes only; this
  investigation runs none).
- `[INFERENCE]` — a conclusion.
- `[DECISION]` — a recommended resolution for controller review.

## 2. Branch, base, repository state, and pinned upstream source

- Branch `investigate/liltoon-opaque-conversion`, base `main` fast-forwarded to
  `origin/main` at `efd5aa7734b5abec028f2574bcd073c942872051` (merge of PR #35,
  Poiyomi alpha-separation vertical slice). The working tree was clean at research
  start.
- Host-generated `Packages/manifest.json` and `packages-lock.json` toolchain churn
  appeared before setup. The investigation inspected the churn (it matched exactly the
  toolchain/sysroot package set of `.omp/AGENTS.md` §Unity package and MCP safety) and
  restored the files to HEAD with controller approval. The churn did not reappear
  during the investigation.
- The investigation inspected lilToon 2.3.4 from the official upstream repository, tag
  `2.3.4`, commit `252fd8cfc46106d4967e95b3f2c788418502f227` (`git describe --tags
  --exact-match` on a shallow clone; package `jp.lilxyzw.liltoon` version `2.3.4`,
  `Assets/lilToon/package.json:1-12`). An earlier in-repo investigation independently
  pinned the same commit
  (`docs/superpowers/specs/2026-08-21-liltoon-build-callback-handoff-design.md:87`).
  Reconciliation re-verified the decisive recipe lines against the pinned tag's raw
  source. No temporary checkout remains, and nothing was installed into AMUSE.
  The table below pins every source URL to commit
  `252fd8cfc46106d4967e95b3f2c788418502f227`, never to `master`.

  | What | Pinned location |
  |---|---|
  | Package identity | [`Assets/lilToon/package.json`](https://raw.githubusercontent.com/lilxyzw/lilToon/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/package.json) |
  | Mode enums | [`Assets/lilToon/Editor/lilEnumeration.cs`](https://raw.githubusercontent.com/lilxyzw/lilToon/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Editor/lilEnumeration.cs) |
  | Shader-asset family map (incl. outline identities and `ltspass_*` passes) | [`Assets/lilToon/Editor/lilShaderManager.cs`](https://raw.githubusercontent.com/lilxyzw/lilToon/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Editor/lilShaderManager.cs) |
  | Vendor mode conversion | [`Assets/lilToon/Editor/lilMaterialUtils.cs`](https://raw.githubusercontent.com/lilxyzw/lilToon/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Editor/lilMaterialUtils.cs) |
  | Opaque target asset | [`Assets/lilToon/Shader/lts.shader`](https://raw.githubusercontent.com/lilxyzw/lilToon/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Shader/lts.shader) |
  | Cutout source asset (first slice) | [`Assets/lilToon/Shader/lts_cutout.shader`](https://raw.githubusercontent.com/lilxyzw/lilToon/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Shader/lts_cutout.shader) |
  | Forward alpha path (feature-conditional alpha/clip blocks) | [`Assets/lilToon/Shader/Includes/lil_pass_forward_normal.hlsl`](https://raw.githubusercontent.com/lilxyzw/lilToon/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Shader/Includes/lil_pass_forward_normal.hlsl) |
  | SDK build callbacks (order 100) | [`Assets/lilToon/External/Editor/VRChatModule.cs`](https://raw.githubusercontent.com/lilxyzw/lilToon/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/External/Editor/VRChatModule.cs) |
  | Build processor | [`Assets/lilToon/Editor/lilToonEditorUtils.cs`](https://raw.githubusercontent.com/lilxyzw/lilToon/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Editor/lilToonEditorUtils.cs) |
  | Callback shader-setting methods | [`Assets/lilToon/Editor/lilToonSetting.cs`](https://raw.githubusercontent.com/lilxyzw/lilToon/252fd8cfc46106d4967e95b3f2c788418502f227/Assets/lilToon/Editor/lilToonSetting.cs) |
- The investigation used three bounded read-only OMP subagents, as authorized: a
  repository seam inventory (scout), an upstream lilToon 2.3.4 source characterization
  (librarian), and an adversarial architecture review (reviewer). The coordinator
  re-verified every consequential claim below against the repository and the pinned
  tag.

## 3. Current merged Poiyomi conversion flow

Verified at `efd5aa7`:

1. **Family selection.** `UnityMaterialSemantics.IdentifyFamily`
   (`Editor/Semantics/UnityMaterialSemantics.cs:242-264`) selects by exact shader name:
   Poiyomi's name or `LilToonSourceAttestation.SupportedShaderName` (`"lilToon"`).
   Every other name gets `Unsupported`. `TrySelectAlphaMaterialRequests` (`:158-168`)
   returns the family's alpha-relevance request and capture schema.
   `CaptureRequestForFamily` (`:290-302`) resolves that request: for Poiyomi it is
   `Combine(PoiyomiMaterialSemantics.AlphaEvidenceRequest, PoiyomiOpaqueConversion.ConversionEvidenceRequest)`;
   for lilToon it is `LilToonMaterialSemantics.AlphaEvidenceRequest` alone; for
   `Unsupported` it is null.
2. **Closed capture and attestation.** The renderer's material-dependency closure
   selects a request for every admitted material (current slot materials plus every
   reachable material-swap value). It fails the whole renderer batch on any
   unselectable family or unattested material
   (`Editor/Host/UnityAnimationEvidenceCapture.cs:334-374`,
   `MaterialDependencyClosureFailure.UnattestedMaterial`).
   `TryCaptureClosedAlphaMaterials` (`Editor/Semantics/UnityMaterialSemantics.cs:170-210`)
   performs the single capture and applies `IsAttestedAlphaMaterial` (`:304-319`). For
   lilToon this requires `LilToonSourceAttestation.TryVerifyLilToonIdentity`.
3. **Barrier.** The extension-free `AmusePlatformFinishPass.Execute`
   (`Editor/Build/AmusePlatformFinishPlugin.cs:313-423`) captures the committed graph,
   evidence, and admitted live materials. It resolves runtime states, classifies
   geometry, and calls `AlphaSeparationPreparation.Prepare`.
4. **Preparation.** `AlphaSeparationPreparation.Prepare`
   (`Editor/Build/AlphaSeparationPreparation.cs:65-331`):
   - re-runs `UnityAnimationEvidenceCapture.ResolveProofRelevant` against
     **`PoiyomiOpaqueConversion.ConversionEvidenceRequest`** (`:93-123`). Renderer-wide
     `UnrecognizedMaterialBinding`, additive-layer, and unnormalized-blend-tree
     failures refuse every candidate slot;
   - for each candidate slot and each admitted index,
     `ConvertAdmittedMaterial` (`:363-479`) runs the single family branch (`:378-382`).
     If `captured.Family != CapturedAlphaMaterialFamily.Poiyomi`, it returns
     `OpaqueConversionUnsupportedFamily`. Otherwise it runs
     `TryAdmitDerivedEvidence` against the Poiyomi conversion request (`:390-398`),
     the runtime-overwrite rule against `PoiyomiOpaqueConversion.CanonicalOpaqueProperties`
     (`:407-421`), `ReadEffectiveRenderState` (live queue plus `RenderType` tag,
     `:441-442`), `GatherConversionSourceEvidence` plus `TryVerifyPoiyomiIdentity`
     (`:446-454`), and `EvaluateVerifiedEligibility` (`:456-475`). That evaluation
     maps an `AlreadyOpaque` source to itself, sends a `Convertible` source to
     `PrepareCanonicalOpaqueClone`, and refuses every other case;
   - the avatar-wide dedup `RegisterPreparedOpaque` (`:490-503`) keys by source
     material. It creates the clone only after a slot's full admitted set maps. The
     decision is per renderer or slot; the artifact is shared (`:236-253`).
5. **Poiyomi conversion core.** `Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`
   defines a canonical tuple of 23 properties (`:142-167`), queue 2000, and
   `RenderType=Opaque` (`:173-175`). The conversion schema is the recipe plus
   `_EnableOutlines` (`:186-198,586-596`). The eligibility gates cover outlines,
   premultiply, alpha-to-coverage, `_ZTest` LEqual, the blend predicate, ForwardAdd
   factors, and `_Cutoff` ≤ 1 (`:244-359`). `PrepareCanonicalOpaqueClone`
   (`:531-561`) creates `new Material(source)` on the same shader, writes the 23
   properties plus queue plus tag, then re-reads 25 facts to validate. It destroys the
   clone and throws on any disagreement.
6. **Apply pass.** `AlphaSeparationApply`
   (`Editor/Build/AlphaSeparationApply.cs`) runs `PrepareSurvivingSet`, which validates
   every candidate slot against the live build avatar: the renderer is alive, the mesh
   is reference-equal, array lengths match, marker clips agree, binding identity
   matches evidence, and every keyframe value and live current material is in
   `OpaqueOfAdmitted`. It then finalizes against survivors and sweeps unreferenced
   transients. `ApplyFinalization` is the single mutation boundary. Everything
   downstream of the `Material → Material` mapping is shader-agnostic: mapping-based
   slot completeness, appended-slot indexing, curve rewriting, mesh finalization,
   sweep, and apply (`AlphaSeparationRecords.cs:99-365`,
   `AlphaSeparationApply.cs:81-430,475-550`).
7. **Test seam.** `VerifiedOpaqueConversion` (`AlphaSeparationPreparation.cs:26-31`)
   substitutes only the shader-family conversion step for public-package fixtures. It
   is typed with `PoiyomiOpaqueConversionRefusal` and reachable only through the
   internal fixture overload (`AmusePlatformFinishPlugin.cs:313-321`).

## 4. Existing lilToon support already present in AMUSE

All of it is **alpha analysis**, none of it conversion:

- `LilToonMaterialSemantics` (`Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`)
  defines `AnalyzeBaseMaterial` (`:127-148`), the verified-material seam
  `InterpretVerifiedMaterial` (`:158-207`), and `AlphaEvidenceRequest` (`:482-496`):
  shader name plus `_lilToonVersion` plus `_Invisible` plus `_UDIMDiscardCompile`.
  `InterpretVerifiedAlpha` (`:505-533`) returns **constant 1**, under an explicitly
  documented premise: on the attested opaque (`LIL_RENDER 0`) variant, every
  alpha-writing block forces alpha to one, and the subpass alpha path is compiled out
  (`:472-480`). `_Invisible` and `_UDIMDiscardCompile` remain as coverage gates
  because they remove fragments instead of changing the value.
- `LilToonSourceAttestation` (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs`)
  pins exactly one shader identity: `SupportedShaderName "lilToon"`,
  `SupportedShaderGuid df12117ecd77c31469c224178886498e`,
  `PassShaderName "Hidden/ltspass_opaque"`, `PackageVersion 2.3.4`,
  `ShaderFormatVersion 45`, `OpaqueRenderMode 0`, and three canonical digests. Task 0
  (2026-08-18) **measured the digests from a real scratch install**, and the
  investigation deliberately did not re-derive them from the upstream repository,
  because the tag's committed generated shaders are stale relative to its own
  generator (`:323-347,334-344`). `TryScanRenderMode` (`:852-890`) requires exactly
  one integer `#define LIL_RENDER`. `TryVerifyLilToonIdentity` (`:1078-1198`)
  conjuncts shader name, GUID, format version, package version, pass identity,
  digests, and render mode 0. `GatherSourceEvidence` (`:1238-1314`) resolves the
  fixed `Hidden/ltspass_opaque` pass.
- Fixtures: `LilToonFixtureTestBase.CreateVerifiedMaterial`
  (`Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs:15-23`) builds a
  schema-complete stand-in that never needs the pinned digests, because it exercises
  equations through the verified seam.

Consequence (`[SOURCE]`): today, a **cutout or transparent lilToon material is
`Unsupported`**. Its shader name is `Hidden/lilToonCutout`,
`Hidden/lilToonTransparent`, one-pass, or two-pass — none of which is `"lilToon"` — so
it fails family selection. The renderer's material-dependency closure then fails, and
**every slot on that renderer** is refused renderer-wide. This is design §2.6/§11's
recorded coverage pressure
(`docs/superpowers/specs/2026-08-28-alpha-separation-vertical-slice-design.md:891-895`),
confirmed in code.

## 5. Official lilToon 2.3.4 render-mode behavior

Every fact below is `[SOURCE]` unless labeled, pinned to tag `2.3.4` /
`252fd8cfc46106d4967e95b3f2c788418502f227`:

- **Representation.** `Editor/lilEnumeration.cs:11-27` defines `RenderingMode {
  Opaque, Cutout, Transparent, Refraction, RefractionBlur, Fur, FurCutout, FurTwoPass,
  Gem }` and `TransparentMode { Normal, OnePass, TwoPass }`. Mode identity is
  primarily a **shader-asset family**: `Editor/lilShaderManager.cs:8-46` maps, for the
  regular family, opaque `lilToon` (`lts`), cutout `Hidden/lilToonCutout` (`ltsc`),
  transparent `Hidden/lilToonTransparent` (`ltst`), one- and two-pass transparent, the
  **outline counterparts** `Hidden/lilToonOutline` (`ltso`),
  `Hidden/lilToonCutoutOutline` (`ltsco`), transparent-outline, and one- and
  two-pass-outline assets, plus the `Hidden/ltspass_opaque`, `Hidden/ltspass_cutout`,
  and `Hidden/ltspass_transparent` pass assets, plus Lite, Tessellation, and Multi
  counterparts. An outline source therefore targets a **different opaque asset** than
  its no-outline sibling: regular cutout without outline targets `lilToon`, and
  regular cutout with outline targets `Hidden/lilToonOutline`. Declared tags:
  `lts.shader:638-644` sets `RenderType=Opaque, Queue=Geometry`;
  `lts_cutout.shader:639-644` sets `TransparentCutout, Queue=AlphaTest`;
  `lts_trans.shader:672-676` sets `TransparentCutout, Queue=AlphaTest+10` (one- and
  two-pass the same). There are **no mode keywords**. For Multi, the numeric
  `_TransparentMode` property carries the mode.
- **Vendor conversion path.** `Editor/lilMaterialUtils.cs`,
  `SetupMaterialWithRenderingMode` (`:18-315`), is the edit-time conversion entry
  point. The opaque branch (`:38-70`) selects the family's opaque shader and writes
  `_SrcBlend=One`, `_DstBlend=Zero`, `_AlphaToMask=0`. Its outline counterpart writes
  `_OutlineSrcBlend=One`, `_OutlineDstBlend=Zero`, `_OutlineAlphaToMask=0`. Multi also
  writes the override tag `RenderType=""` and `renderQueue=-1`. The common tail
  (`:266-315`) applies to every non-Multi material: **it restores `renderQueue` to
  its pre-call value** — the vendor does *not* reset the queue on a mode change. It
  also sets `_ZWrite=1` (except Gem), `_ZTest=4` unless `transparentMode == TwoPass`,
  `_OffsetFactor`/`_OffsetUnits=0`, `_ColorMask=15`, `_SrcBlendAlpha=One`,
  `_DstBlendAlpha=OneMinusSrcAlpha`, `_BlendOp`/`_BlendOpAlpha=Add`, ForwardAdd
  `_SrcBlendFA`/`_DstBlendFA=One`, `_SrcBlendAlphaFA=Zero`, `_DstBlendAlphaFA=One`,
  and `_BlendOpFA`/`_BlendOpAlphaFA=Max`. It resets outline Cull, ZWrite, ZTest,
  offsets, alpha factors, and ops the same way. The coordinator spot-verified the
  opaque branch and the entire common tail against the pinned tag's raw source.
- **Shader identity.** Conversion **changes shader asset identity** — opaque to
  cutout or transparent is a shader swap on the converted material, not a
  property or keyword toggle. The vendor utility writes **no texture assignments, no
  feature-keyword changes, and no pass-enable changes**. `SetupMultiMaterial`
  (`:356-520`) independently derives feature keywords for the Multi family.
- **No vendor safety policy.** The conversion switch has no refusal or validation
  branch. Every safety or refusal rule is AMUSE policy, not vendor behavior.
  `[INFERENCE]`
- **Multi nuance.** For `ismulti`, the utility *derives* the mode from
  `material.GetFloat("_TransparentMode")` (`:25-35`) and ignores the passed argument.
  Converting a Multi material to opaque requires `_TransparentMode=0` first.

## 6. Exact proposed canonical opaque recipe, field by field

`[DECISION]` The canonical opaque state for the **first supported slice** — a pinned
lilToon 2.3.4 **regular, non-Lite, non-Tessellation, non-Multi, no-outline cutout**
material — is the vendor's own Opaque target state. AMUSE assembles it as a
version-pinned, AMUSE-owned recipe in the same shape as `PoiyomiOpaqueConversion`: one
shader asset, one direction, one version. Outline sources are **outside** this slice:
the vendor maps `Hidden/lilToonCutoutOutline` to the opaque-with-outline asset
`Hidden/lilToonOutline` (`lilShaderManager.cs:14-16`), not to `lilToon`, so converting
an outline source to `lilToon` would silently drop its outline pass. The first slice
refuses outline sources instead (§10). Outline support is later, separate work: a new
source-and-target attestation plus a recipe extension (§13, §15).

| Field | Value | Basis |
|---|---|---|
| shader | `lilToon` (the generated per-project no-outline opaque asset AMUSE already attests; the source's own outline state is irrelevant, because this slice's source has no outline) | `lilShaderManager.cs:8-16`; family selection in `SetupMaterialWithRenderingMode` opaque branch, `!isoutl` |
| `_SrcBlend` | 1 (One) | `lilMaterialUtils.cs` opaque branch |
| `_DstBlend` | 0 (Zero) | same |
| `_AlphaToMask` | 0 | same |
| `_ZWrite` | 1 | common tail |
| `_ZTest` | 4 (LessEqual) | common tail. The vendor skips this write only when `transparentMode == TwoPass`. An AMUSE recipe must not inherit that condition: it sets 4 unconditionally and refuses any material whose visibility intent depends on a different comparison, mirroring Poiyomi gate 7 |
| `_OffsetFactor` / `_OffsetUnits` | 0 | common tail |
| `_ColorMask` | 15 | common tail |
| `_SrcBlendAlpha` / `_DstBlendAlpha` | 1 (One) / 10 (OneMinusSrcAlpha) | common tail |
| `_BlendOp` / `_BlendOpAlpha` | Add (0) | common tail |
| `_SrcBlendFA` / `_DstBlendFA` | 1 / 1 | common tail |
| `_SrcBlendAlphaFA` / `_DstBlendAlphaFA` | 0 / 1 | common tail |
| `_BlendOpFA` / `_BlendOpAlphaFA` | Max / Max | common tail |
| `renderQueue` | `2000` (the opaque asset's declared `Geometry`) — AMUSE sets it explicitly | `[DECISION]`: the vendor restores the pre-call queue for non-Multi (`:266-267`), so its own utility does *not* produce a canonical queue. AMUSE's contract (`.omp/AGENTS.md` §Correctness and uncertainty authorizes a queue move under an opacity proof) wants the canonical value, and read-back validation must check it |
| `RenderType` override tag | `Opaque` — AMUSE sets it explicitly | `[DECISION]`: AMUSE sets and validates the canonical effective tag. `[MEASURED]` probe B1 found that, in Unity 2022.3.22f1 with the installed 2.3.4 package, assigning the opaque shader to a cloned material cleared a unique effective source override and read back the target's declared `Opaque` tag even on the vendor path. The explicit AMUSE write is still a deterministic canonical-state contract, not a fix for that measured stale override (`2026-08-30-liltoon-opaque-characterization.md` §§8, 10) |
| `_Cutoff` / `_AlphaMask*` / dither / dissolve properties | **not written** | `[SOURCE]`: the utility writes none of them. On the opaque asset, `LIL_RENDER 0` excludes the alpha/clip path at compile time (`LilToonMaterialSemantics.cs:472-480`), so these properties cannot affect the proven-opaque triangles. `[INFERENCE]` they are not recipe properties and must not enter the conversion evidence request as read-plus-write facts. Whether any of them is an eligibility gate is a §7 open question |

What the recipe deliberately does **not** do: change texture assignments, clear
feature keywords, or touch pass enables — the vendor does none of this either. It does
not convert cutout-outline (`Hidden/lilToonCutoutOutline`), any other outline family,
Multi, Lite, Tessellation, Fur, Refraction, RefractionBlur, Gem, one-pass or two-pass
transparent, or any overlay or fallback family. Each of those is a separate shader
identity and pipeline with no pinned conversion contract, so the recipe refuses them
(see §10). The recipe contains **no outline-only properties**: it removed every
`_Outline*` field with the outline scope.

`[INFERENCE]` The clone is `new Material(source)` (per the characterized
clone-fidelity recipe), followed by a shader-identity change to the attested opaque
asset plus the tuple above, then a full re-read validation of every written fact —
mirroring `PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone`, including the
throw-on-disagreement policy. Unlike Poiyomi, **the clone's shader differs from the
source's shader**. The Poiyomi clone's shader-preservation check is not portable
as-is, and the read-back validation must instead verify the expected opaque shader
identity.

## 7. Evidence capture and runtime-validation requirements

- **Conversion evidence request.** The request combines the recipe's scalar
  properties (above) with whatever eligibility gates the lilToon implementation
  defines. `[DECISION]` It is a lilToon-specific `MaterialEvidenceRequest` returned by
  `CaptureRequestForFamily` as `Combine(LilToonMaterialSemantics.AlphaEvidenceRequest,
  lilToon conversion request)`, exactly mirroring the Poiyomi combination. The capture
  union machinery already supports this
  (`UnityAnimationEvidenceCapture.cs:352-374`, `UnityMaterialSemantics.cs:279-302`).
- **Captured before the barrier (Q4).** Everything animation-reachable: every recipe
  scalar (each is `material.<Property>`-animatable) plus the eligibility-gate
  properties, captured in the closed batch and admitted through the existing
  `TryAdmitDerivedEvidence` (exact-singleton against the material's own serialized
  default). This preserves the Poiyomi design's separation: conversion evidence rides
  the closed capture schema, and ordinary alpha relevance stays narrow.
- **Read live during preparation (Q5).** Only facts animation cannot reach, with the
  same justification as `PoiyomiOpaqueConversion.ReadEffectiveRenderState`
  (`PoiyomiOpaqueConversion.cs:436-455`): `material.renderQueue`, the `RenderType`
  override tag, and — lilToon-specific — the **source shader asset identity**
  (name and GUID, the render-mode carrier). No `material.<Property>` binding syntax
  can address any of these. `[SOURCE]`+`[INFERENCE]`
- **Runtime validation.** Unchanged: pass 3 revalidates every live binding, keyframe
  value, and current material against the prepared mapping
  (`AlphaSeparationApply.cs:81-178,475-550`). The overwrite rule and
  `RuntimeMaterialValueNotMapped` machinery stay mapping-based and family-agnostic.

## 8. Animation and overwrite-rule implications

- The renderer-wide overwrite rule transfers unchanged in structure: for every recipe
  property carrying an admitted conversion binding at the renderer path, the admitted
  value must already equal the canonical value
  (`AlphaSeparationPreparation.cs:400-421`; design §7.3). `[INFERENCE]` For lilToon
  this covers `_SrcBlend`, `_DstBlend`, `_ZWrite`, `_ZTest`, `_ColorMask`, the blend
  ops, and the ForwardAdd factors — all ordinary material floats, all
  animation-reachable.
- `[INFERENCE]` LilToon has no property like `_Mode` that re-labels a mode on the same
  shader asset. For non-Multi materials, the shader asset itself carries the mode, and
  the shader asset is not animatable, so there is no animated mode control to refuse.
  `_Cutoff` animation behaves like the Poiyomi `_Cutoff` case: it is
  conversion-eligibility-relevant only insofar as the source-variant alpha proof reads
  it, and that proof does not exist yet (blocker B2).
- `[INFERENCE]` If the alpha proof for a non-opaque variant later depends on
  `_Cutoff`, `_AlphaMaskMode`, or `_UseDither`, those properties become
  proof-relevant for the existing admission machinery. This needs no new rule shape.

## 9. NDMF/lilToon callback and generated-shader lifecycle

- AMUSE's three passes run inside NDMF: an early hook at `-11000` through
  `PlatformFinish` at the late hook `-1025`. lilToon 2.3.4 acts **after** NDMF: the
  VRChat SDK preprocess callback at order `100` runs `SetShaderSettingBeforeBuild`
  (shader regeneration from per-project settings) and `SetupMultiMaterial`, then
  postprocess restores state (`External/Editor/VRChatModule.cs:18-106`;
  `lilToonEditorUtils.cs:774-788`; `lilToonSetting.cs:897-1011`). lilToon 2.3.4 has
  **no `IPreprocessShaders`** handler `[SOURCE]` (the CHANGELOG records its removal in
  an earlier release).
- `[MEASURED]` (prior recorded probe,
  `docs/superpowers/specs/2026-08-21-liltoon-build-callback-handoff-design.md:97-120,154-160`):
  in the characterized simple case, renderer paths and slots, materials, shaders,
  relevant values, and animation references stayed unchanged across callback `100`.
  Only generated shader files changed. `SetupMultiMaterial` was not exhaustively
  exercised.
- **Answer to Q9 — conditional, two cases.** The committed handoff investigation makes
  Outcome B (the upload-only late validation gate) mandatory for every positive proof
  **that depends on callback-`100`-generated shader state**. A proof complete from
  NDMF-visible evidence may use its independent lifecycle instead
  (`liltoon-build-callback-handoff-design.md:26`, `:325-345`). Which case a lilToon
  conversion falls into is decided by the cutout alpha proof (B2), which does not
  exist yet. `[SOURCE]`+`[INFERENCE]`
  1. **Recommended narrow NDMF-complete cutout slice.** The pinned forward pass makes
     the dependence concrete: the cutout alpha path sits behind compile-time feature
     conditionals — `LIL_FEATURE_ALPHAMASK` (`lil_pass_forward_normal.hlsl:362`, also
     `:195`), `LIL_FEATURE_DISSOLVE` (`:369`, `:202`), and `LIL_FEATURE_DITHER`
     (`:388`, `:221`) — whose active set `SetShaderSettingBeforeBuild` bakes into the
     generated shader at callback `100` (`lilToonSetting.cs:897-946`), and the clip
     itself is `LIL_RENDER`-branched (`:394-411`). The opaque theorem escapes this
     because alpha-1 holds for **every** compilation of `LIL_RENDER 0`. A cutout proof
     must earn the same compilation-invariance. **B2 design obligation:** the cutout
     proof may establish only a callback-independent core case — attested equations
     over captured material and texture evidence, for materials whose alpha path is
     feature-independent — and must conservatively refuse every optional generated
     alpha path: at minimum alpha mask, dither, dissolve, and any other
     alpha-affecting feature whose correctness depends on callback-generated
     compilation. Why callback `100` then cannot change the proof result: the
     regenerated shader changes only *which* optional feature blocks it compiles.
     With every alpha-affecting optional feature refused, the remaining equation
     (alpha from `_Color`/`_MainTex` evidence versus `_Cutoff`) stays identical under
     every compilation of the attested source, so the proven-opaque triangle set
     cannot change after the barrier. **If source review shows even this restricted
     core still depends on callback-generated source, Outcome B becomes a
     prerequisite instead** — the investigation must not force the NDMF-complete
     claim.
  2. **Future positive support for generated optional alpha features** (alpha-masked,
     dithered, dissolved cutouts): Outcome B remains **required**, unless separate
     evidence proves the result independent of callback-generated source.
  Until B2 discharges obligation 1, this note claims **no** lifecycle verdict for
  lilToon conversion. The residual facts hold either way:
  - `SetupMultiMaterial` rewrites keywords and pass enables for the **Multi** family.
    The recipe refuses Multi-family sources (§10), so no converted clone is Multi.
    `[INFERENCE]`
  - The converted clone references the same generated per-project opaque shader asset
    AMUSE already attests. Project-setting regeneration changes file contents, not
    the asset identity the clone references. `[INFERENCE]`
  - NDMF `Finish()` may deduplicate or rewrite object identity. Pass 3 already reads
    live state instead of barrier identity (falsifier 20). `[SOURCE]`
  - Apply-on-Play remains unavailable for positive mutation, per the
    coexisting-lifecycle design v1 (`coexisting-optimizer-lifecycle-design.md:542-548`)
    — unchanged for lilToon.

## 10. Refusal scope and unsupported cases

`[DECISION]` The lilToon implementation refuses these cases, using slot-local
refusals where the current architecture already expresses them:

- Cutout-outline (`Hidden/lilToonCutoutOutline`) and every other outline family
  (`Hidden/lilToonOutline`, transparent-outline, Lite/Tessellation outline variants):
  their vendor opaque target is an outline asset, not `lilToon`, so this slice's
  recipe does not apply to them. The first slice refuses them. Converting an outline
  source to `lilToon` would silently drop its outline pass and is not a preservation
  move.
- Multi (`Hidden/lilToonMulti*`), Lite, Tessellation, Fur, FurCutout, FurTwoPass,
  Refraction, RefractionBlur, Gem, transparent, and one-pass/two-pass transparent
  families: these are separate shader identities and pipelines with no pinned
  conversion contract. (Two-pass also breaks the vendor's own `_ZTest` reset
  condition, so the recipe refuses it regardless.)
- Materials whose effective blend state does not satisfy the alpha-1 degeneration
  predicate at the source variant, mirroring the Poiyomi gates. The exact gate set is
  part of the future implementation design, not fixed here.
- Coverage mechanisms the source-variant alpha proof cannot discharge (dither, alpha
  mask, dissolve) remain refused by that proof's unknown or unsupported outcomes.
- Renderer-scoped refusals (unrecognized conversion binding, additive layer,
  unnormalized direct blend tree) and slot-local refusals (marker clip, unmapped
  value, not-admitted state, overwritten canonical property) transfer unchanged.
- `[SOURCE]` Today's renderer-wide refusal of *unattestable* lilToon variants (§4)
  remains until attestation is extended. Extending attestation changes this coverage
  (see §11); that extension is scoped below, not here.

## 11. Mixed-family behavior

- Today, a mixed Poiyomi/lilToon admitted set refuses
  (`OpaqueConversionUnsupportedFamily`, falsifier 9): mixed families cannot all map,
  so the slot refuses (`AlphaSeparationPreparation.cs:375-382`).
- With a working no-outline-cutout lilToon conversion: `[INFERENCE]` a slot whose
  admitted set mixes attested Poiyomi and attested no-outline-cutout lilToon
  materials **can** map completely and becomes convertible. Each admitted value maps
  through its own family's conversion. This is a coverage expansion, not a blocker.
  Whether to enable it in the first slice, or to keep refusing mixed slots initially,
  is a controller decision — the mapping architecture does not force either answer.
- Same-renderer sibling behavior (a Poiyomi slot optimizes beside a refused lilToon
  slot) is already proven (falsifier 10) and stays unchanged. Within the first slice,
  a refused lilToon slot is any non-attestable, outline, or other-family lilToon
  material.

## 12. Smallest viable integration shape

`[DECISION]` The investigation's answer to Q10: the smallest verified shape is **not**
"one request plus one implementation plus one case," as literally declared. It is:

**First-slice scope.** Regular, non-Lite, non-Tessellation, non-Multi, **no-outline
cutout** sources only, converting to the attested `lilToon` opaque asset. Every other
lilToon family — outline, Multi, Lite, Tessellation, Fur, Refraction, Gem,
transparent, one-pass, two-pass — refuses (§10) and is later work (§13).

1. **lilToon conversion evidence request** added to `CaptureRequestForFamily` for
   `CapturedAlphaMaterialFamily.LilToon` — as declared.
2. **`LilToonOpaqueConversion`** — a new Poiyomi-opaque-conversion-shaped module:
   pinned recipe (§6), eligibility, and clone preparation with read-back validation.
3. **One new family case** in `ConvertAdmittedMaterial` — as declared.
4. **Required narrow refactors the current requirement demonstrably forces** (§13):
   - a per-family conversion request at the conversion-relevance resolution
     (`AlphaSeparationPreparation.cs:93-123`) and inside `ConvertAdmittedMaterial`
     (admission `:390-398`, overwrite rule `:407-421`) — today the Poiyomi request is
     hard-coded in all three places;
   - `CaptureRequestForFamily` returning the combined schema (item 1 covers this);
   - variant-aware family selection plus attestation for the supported non-opaque
     source assets (`IdentifyFamily`, `LilToonSourceAttestation`) — see the blockers
     below.
5. No change to: geometry planning, mesh finalization, appended-slot indexing,
   animation discovery and rewriting, pass-3 validation, the sweep, the apply
   boundary, the prepared-record shape, or the avatar-wide dedup. Every one of these
   stays mapping-based (`AlphaSeparationRecords.cs:106-259`,
   `AlphaSeparationApply.cs:81-430`). `[SOURCE]`

The plan needs no `IOpaqueConversion`, no adapter interface, no registry, no
conversion result hierarchy, no render-state IR, and no `SurfaceMode` classifier. The
second implementation demonstrates exactly the parameterization listed in item 4 —
three hard-coded Poiyomi facts become per-family facts — and nothing more.
`[INFERENCE]`

## 13. Required refactoring versus deferred architectural pressure

**Demonstrated now (the one-switch boundary does not survive literally):**

| # | Pressure | Evidence |
|---|---|---|
| R1 | Conversion-relevance resolution, derived admission, and the overwrite rule are Poiyomi-hard-coded **before and around** the family branch. A lilToon case cannot correct evidence routed under the wrong request | `AlphaSeparationPreparation.cs:93-123,390-421` `[SOURCE]` |
| R2 | Family selection is exact-name and duplicated at two sites (`IdentifyFamily` and `CaptureAlphaMaterials`/`UnityRendererAlphaAnalysis.cs:390-405`). Non-opaque lilToon sources are `Unsupported` | `UnityMaterialSemantics.cs:242-264,95-136` `[SOURCE]` |
| R3 | lilToon attestation pins exactly one opaque shader/pass identity and `OpaqueRenderMode 0`. `GatherSourceEvidence` resolves the fixed `Hidden/ltspass_opaque` pass | `LilToonSourceAttestation.cs:323-347,1078-1198,1238-1314` `[SOURCE]` |
| R4 | `InterpretVerifiedAlpha`'s constant-1 proof is valid **only** under its `LIL_RENDER 0` premise. It must not apply to cutout or transparent sources | `LilToonMaterialSemantics.cs:472-533` `[SOURCE]` |
| R5 | The Poiyomi clone's shader-preservation check is wrong for a family whose conversion changes shader identity. The read-back validation is per-family | §6 `[INFERENCE]` |
| R6 | The `VerifiedOpaqueConversion` test seam is typed `PoiyomiOpaqueConversionRefusal`. A lilToon verified seam needs its own refusal type or a family-agnostic shape | `AlphaSeparationPreparation.cs:26-31` `[SOURCE]` |

**Deferred (record, do not build):**

- Outline families: later, separate extension work requires **both** a source
  attestation (`Hidden/lilToonCutoutOutline` etc.) **and** a target attestation
  (`Hidden/lilToonOutline` etc., with its own pass identity and digests), plus an
  outline-aware recipe. This investigation records that need without designing it.
- Other lilToon families (Multi, Lite, Tessellation, transparent variants) and
  integration packages (LTCGI, AudioLink, VRC Light Volumes): each is separate
  attestation and conversion pressure. The official-integration matrix keeps them
  unimplemented.
- Callback-`100`-dependent positive proofs: Outcome B is required for them (§9 case
  2). It applies only if B2's restricted core cutout proof cannot be made
  callback-independent, or when AMUSE admits generated optional alpha features later.
- Per-slot durable diagnostics: unchanged from plan item 1.
- A third family, when it arrives: that is when a registry "earns its first honest
  argument" (`UnityMaterialSemantics.cs` doc comment; design §11). Two families
  parameterize three facts. They do not yet justify dynamic discovery.

## 14. Required falsifiers and vacuity guards

Public synthetic tests (no installed lilToon package), following the
`VerifiedPoiyomiTestSeams`/`LilToonFixtureTestBase` patterns:

1. Family selection returns the lilToon conversion-aware capture schema for an
   attested non-opaque stand-in. Ordinary alpha relevance for unrelated materials
   stays bit-for-bit unchanged. This guards against widening alpha relevance — the
   regression the merged capture-schema split exists to prevent.
2. A lilToon candidate slot prepares: a mixed admitted set maps, and `AlreadyOpaque`
   (a canonical lilToon source) maps to itself with no clone.
3. Conversion-only animation of a lilToon recipe property: away from canonical
   refuses (`ConversionPropertyOverwrittenAtRuntime`), and at canonical prepares
   (mirrors falsifier 11).
4. The generated clone carries the canonical recipe: every recipe fact reads back
   canonical, and the source material's full property set stays unchanged
   (persistence and preservation tests extended to the lilToon clone).
5. Same-renderer sibling: a Poiyomi slot optimizes beside a refused lilToon slot
   (renderer-scoped refusal scoping preserved) — re-run falsifier 10 against the new
   case.
6. Mixed Poiyomi/lilToon slot behavior matches the controller's §11 decision
   (convertible or explicitly refused).
7. Cutout-outline, Multi, Lite, Gem, and transparent stand-ins refuse with the family
   refusal — a vacuity guard that the recipe never silently converts an unattested
   family and never drops an outline pass by converting an outline source to
   `lilToon`.
8. **Per-family routing regression — the executable R1 boundary obligation.** A
   dedicated Poiyomi-focused regression proves that, after the R1 refactor, the
   Poiyomi path still routes through
   `PoiyomiOpaqueConversion.ConversionEvidenceRequest` (conversion-relevance
   resolution and derived admission), checks the runtime-overwrite rule against
   `PoiyomiOpaqueConversion.CanonicalOpaqueProperties`, and invokes the existing
   `PoiyomiOpaqueConversion` implementation — observable as identical Poiyomi
   eligibility and clone outcomes before and after the refactor. **Every existing
   Poiyomi-focused test must stay unchanged and green.** Separately, and optionally: a
   temporary mutation-sensitivity probe (delete the lilToon case locally, observe that
   Poiyomi routing is untouched, then restore it) may serve as evidence that the
   routing is genuinely family-selected. It is a probe, not this falsifier, and is
   never a committed test.
9. Structural guard: no `SaveAsset` call in the new files (mirrors
   `AlphaSeparationPersistenceTests`).
10. **Callback-independence boundary of the cutout proof** (§9 obligation; falsifies a
    proof that silently depends on generated compilation). For a stand-in whose alpha
    path is feature-conditional (alpha-mask, dither, or dissolve properties active),
    the cutout proof must return unknown or refuse — never a proven-opaque triangle.
    For the restricted feature-independent core, it must return the same triangle
    verdict for every attested compilation variant of the source, demonstrating that
    the verdict stays invariant under callback-`100` regeneration.

`[INFERENCE]` These falsifiers catch the plausible wrong implementations: widening
alpha relevance, skipping the per-family request routing (test 1), wrong queue, tag,
or blend state (test 4), silent family over-acceptance (test 7), Poiyomi mis-routing
or altered Poiyomi behavior after the R1 refactor (test 8), and proof dependence on
generated compilation (test 10).

## 15. Blockers or prerequisites

| # | Blocker | Nature |
|---|---|---|
| B1 | **One combined pre-design characterization prerequisite** (supersedes the former separate B1+B4). The investigation must **measure this before production design fixes the recipe and its validation contract**: a Task-0-style scratch `jp.lilxyzw.liltoon@2.3.4` install session measuring, for the regular **no-outline cutout source** and the **no-outline opaque target**: shader names, GUIDs, pass identities (`Hidden/ltspass_cutout` / `Hidden/ltspass_opaque`), render modes, and canonical digests. The digests must be **measured from a real install**, never taken from the upstream tag (`LilToonSourceAttestation.cs:334-344`). It must also measure: the actual result of assigning the opaque shader to a cloned cutout material; queue behavior (`renderQueue=-1`, explicit 2000, inherited or custom queues) and the effective `RenderType`; `SetOverrideTag("RenderType", …)` and its clearing behavior; and every canonical recipe property's read-back behavior. | Characterization prerequisite. It requires a real installed package session. |
| B2 | **Alpha proof for non-opaque variants, with an explicit callback-independence obligation.** `InterpretVerifiedAlpha` is an opaque-variant theorem. Cutout sources need their own conservative alpha evidence request and interpretation — `_Color.a`, `_MainTex` alpha, `_Cutoff`, `_AlphaMask`, dither, dissolve — or conversion has no triangle proof to build on. This sits upstream of conversion and is the largest missing piece. **Design obligation (§9 case 1):** the proof may establish only a callback-independent core cutout case, and it must conservatively refuse every optional generated alpha path: at minimum alpha mask, dither, dissolve, and any other alpha-affecting feature whose correctness depends on callback-generated compilation. Its falsifier is §14 test 10. **If even the restricted core turns out to depend on callback-generated source, Outcome B becomes a prerequisite instead of an NDMF-complete claim.** The proof's design is a separate controller-reviewed task; this investigation does not scope it. | Missing capability, not a refactor. |
| B3 | **Scope decision.** This revision confirms it: the first slice is regular no-outline cutout only (AlphaTest). Transparent (AlphaBlend, including premultiplied-adjacent states) and every outline family are materially larger, separate proofs. | Controller decision — this revision resolves it for the first slice. |

## 16. Recommendation and next decision

**Recommendation.** `[DECISION]` The extension boundary survives **structurally but
not literally**. The Poiyomi pipeline's shader-agnostic halves — mapping, records,
mesh, curves, validation, sweep, apply, dedup — carried the second family without
change. That is the design's real victory. What failed is the "one case and nothing
else" claim: three Poiyomi-hard-coded facts precede the family branch (R1), and the
lilToon source variants cannot even reach the branch today (R2, R3, R4). The
recommended first slice is **regular, non-Lite, non-Tessellation, non-Multi,
no-outline cutout only**, converting to the already-attested `lilToon` opaque asset.
This slice refuses every outline family and records it as a later, separate
source-and-target attestation plus recipe extension (§13). Its lifecycle verdict is
**conditional**: NDMF-complete only under B2's restricted callback-independent core
proof (§9 case 1), otherwise Outcome B applies. The honest shape is §12: the declared
request, implementation, and case, **plus** the per-family parameterization of
conversion relevance, admission, and the overwrite rule, **plus** the combined
pre-design characterization (B1) and the cutout alpha proof (B2). None of this
licenses a registry, an interface, or an IR at two families.

**Next decisions for the controller, in dependency order:**

1. First-slice scope (B3): **regular, non-Lite, non-Tessellation, non-Multi,
   no-outline cutout only**, per this revision. Outline, transparent, and every other
   family are refused and deferred (§10, §13).
2. Commission the **combined pre-design characterization** (B1): a scratch lilToon
   2.3.4 install session measuring the no-outline cutout source and no-outline opaque
   target identities and digests, **and** the queue and tag read-back behaviors,
   before production design fixes the recipe and validation contract.
3. Commission the cutout alpha-semantics design (B2) as its own reviewed task,
   carrying the §9 callback-independence obligation and its §14 falsifier. It is the
   largest prerequisite and gates everything else. If its restricted core cannot be
   made callback-independent, Outcome B becomes a prerequisite.
4. Only then design the conversion implementation against the fixed recipe of §6 and
   the falsifiers of §14.

---

### Coordinator Q&A summary

1. **Selected, captured, and attested lilToon today** — exact name `"lilToon"` only
   (the opaque asset). Alpha-relevance is `LilToonMaterialSemantics.AlphaEvidenceRequest`
   (shader name plus `_lilToonVersion` plus `_Invisible` plus `_UDIMDiscardCompile`).
   The capture schema is the same request (no conversion request). Attestation runs
   `TryVerifyLilToonIdentity` against the opaque pins of §4.
2. **Missing conversion evidence in `CaptureRequestForFamily`** — everything: there is
   no lilToon conversion request. The schema is the alpha request alone
   (`UnityMaterialSemantics.cs:297-298`).
3. **Canonical opaque recipe (first slice)** — §6: the `lilToon` opaque shader
   identity, the scalar resets (base blend, depth, offset, color-mask set, and the
   ForwardAdd set — **no outline properties**), the explicit queue 2000, and the
   `RenderType=Opaque` tag. No texture or keyword writes.
4. **Captured before the barrier** — all recipe scalars and eligibility-gate
   properties (animation-reachable), through the combined schema (§7).
5. **Read live during preparation** — `renderQueue`, the `RenderType` tag, and the
   source shader asset identity (name and GUID): none is animation-addressable (§7).
6. **Animated properties that make conversion unsafe** — any recipe property driven
   away from its canonical value (the existing overwrite rule), and `_Cutoff`-style
   proof-relevant properties once the cutout alpha proof exists (§8).
7. **Shared source material, two renderers** — yes: one opaque artifact exists
   avatar-wide, and each renderer or slot independently validates family, admission,
   and the overwrite rule (`AlphaSeparationPreparation.cs:236-253,490-503`).
   Unchanged for lilToon.
8. **Mixed admitted sets** — refused today. With a working no-outline-cutout
   conversion, they can map and convert; whether to enable that initially is a
   controller decision (§11).
9. **Callback/generated-shader conflict with the third pass** — conditional (§9). The
   recommended narrow cutout slice must restrict its proof to a callback-independent
   core (B2 obligation: refuse alpha mask, dither, dissolve, and every other
   compilation-dependent alpha path). Under that restriction, callback `100` cannot
   change the verdict, because the equation stays identical under every compilation
   of the attested source. Until B2 discharges that obligation, this note claims no
   lifecycle verdict. If the restricted core still depends on callback-generated
   source, Outcome B is required. Generated optional alpha features always require
   Outcome B unless proven independent.
10. **One request, one implementation, one case?** — the request, implementation, and
    case survive, but three Poiyomi-hard-coded facts (conversion relevance, derived
    admission, the overwrite rule) must be parameterized first. Attestation and an
    alpha proof for non-opaque sources are also prerequisites (§12, §13).
11. **Demonstrated refactoring pressure** — R1 through R6 in §13, all narrow, none
    licensing an abstraction.
12. **Hypothetical, not to generalize** — outline families, Lite/Tessellation/Multi/
    transparent variants, integration packages, callback-dependent proofs, per-slot
    diagnostics, and registries at two families (§13).
13. **Public falsifiers without installing lilToon** — §14's ten tests, built on the
    existing stand-in and verified-seam patterns.
14. **Real package or new characterization required?** — yes, one **combined
    pre-design characterization** (B1, former B1+B4): measured no-outline cutout
    source and no-outline opaque target names, GUIDs, pass identities, render modes,
    and digests, plus clone-assignment behavior, queue behavior (`-1`, explicit 2000,
    inherited, custom), the `RenderType` override tag, and per-recipe-property
    read-back behavior — measured before production design. The cutout
    alpha-semantics design (B2) is a separate prerequisite task, not installable
    here.
