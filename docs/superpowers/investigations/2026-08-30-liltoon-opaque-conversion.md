# lilToon Opaque Conversion — Investigation

## 1. Scope and question

Can a lilToon opaque conversion be added to the merged Poiyomi alpha-separation vertical
slice in the smallest correct way, and does the slice's declared extension boundary
("one lilToon conversion evidence request in `CaptureRequestForFamily`, one lilToon
conversion implementation, one new `case`" — plan §Recorded future refactor pressure item 2)
survive contact with the materially different lilToon 2.3.4 family?

This is an investigation, not an implementation plan. It records verified facts, the
pinned upstream recipe, demonstrated refactoring pressure, blockers, and the next
decision. It creates no design, no plan, no production code.

Answered coordinator questions are reconciled inline and summarized in §16.

Labels: `[SOURCE]` read from pinned repository/upstream source; `[MEASURED]` observed by
an executed probe (prior recorded probes only; none run here); `[INFERENCE]` conclusion;
`[DECISION]` recommended resolution for controller review.

## 2. Branch, base, repository state, and pinned upstream source

- Branch `investigate/liltoon-opaque-conversion`, base `main` fast-forwarded to
  `origin/main` at `efd5aa7734b5abec028f2574bcd073c942872051` (merge of PR #35,
  Poiyomi alpha-separation vertical slice). Working tree clean at research start.
- Host-generated `Packages/manifest.json` / `packages-lock.json` toolchain churn appeared
  before setup, was inspected (exactly the toolchain/sysroot package set of
  `.omp/AGENTS.md` §Unity package integrity), and was restored to HEAD with controller
  approval. It did not reappear during the investigation.
- lilToon 2.3.4 was inspected from the official upstream repository, tag `2.3.4`, commit
  `252fd8cfc46106d4967e95b3f2c788418502f227` (`git describe --tags --exact-match` on a
  shallow clone; package `jp.lilxyzw.liltoon` version `2.3.4`,
  `Assets/lilToon/package.json:1-12`). This is the same commit an earlier in-repo
  investigation independently pinned
  (`docs/superpowers/specs/2026-08-21-liltoon-build-callback-handoff-design.md:87`).
  Decisive recipe lines were re-verified from the pinned tag's raw source during
  reconciliation. No temporary checkout remains; nothing was installed into AMUSE.
  Pinned authoritative source index (all URLs pinned to commit
  `252fd8cfc46106d4967e95b3f2c788418502f227`, never `master`):

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
- Three bounded read-only OMP subagents were used as authorized: a repository seam
  inventory (scout), an upstream lilToon 2.3.4 source characterization (librarian), and
  an adversarial architecture review (reviewer). All consequential claims below were
  re-verified against the repository and the pinned tag by the coordinator.

## 3. Current merged Poiyomi conversion flow

Verified at `efd5aa7`:

1. **Family selection** — `UnityMaterialSemantics.IdentifyFamily`
   (`Editor/Semantics/UnityMaterialSemantics.cs:242-264`) selects by exact shader name:
   Poiyomi's name or `LilToonSourceAttestation.SupportedShaderName` (`"lilToon"`),
   otherwise `Unsupported`. `TrySelectAlphaMaterialRequests` (`:158-168`) hands back the
   family's alpha-relevance request and capture schema.
   `CaptureRequestForFamily` (`:290-302`): Poiyomi =
   `Combine(PoiyomiMaterialSemantics.AlphaEvidenceRequest, PoiyomiOpaqueConversion.ConversionEvidenceRequest)`;
   lilToon = `LilToonMaterialSemantics.AlphaEvidenceRequest` only; `Unsupported` = null.
2. **Closed capture and attestation** — the renderer's material-dependency closure
   selects a request for every admitted material (current slot materials plus every
   reachable material-swap value) and fails the whole renderer batch on any
   unselectable family or unattested material
   (`Editor/Host/UnityAnimationEvidenceCapture.cs:334-374`,
   `MaterialDependencyClosureFailure.UnattestedMaterial`).
   `TryCaptureClosedAlphaMaterials` (`Editor/Semantics/UnityMaterialSemantics.cs:170-210`)
   performs the single capture and applies `IsAttestedAlphaMaterial` (`:304-319`),
   which for lilToon requires `LilToonSourceAttestation.TryVerifyLilToonIdentity`.
3. **Barrier** — the extension-free `AmusePlatformFinishPass.Execute`
   (`Editor/Build/AmusePlatformFinishPlugin.cs:313-423`) captures committed graph,
   evidence, and admitted live materials, resolves runtime states, classifies geometry,
   and calls `AlphaSeparationPreparation.Prepare`.
4. **Preparation** — `AlphaSeparationPreparation.Prepare`
   (`Editor/Build/AlphaSeparationPreparation.cs:65-331`):
   - re-runs `UnityAnimationEvidenceCapture.ResolveProofRelevant` against
     **`PoiyomiOpaqueConversion.ConversionEvidenceRequest`** (`:93-123`) — renderer-wide
     `UnrecognizedMaterialBinding`, additive-layer, and unnormalized-blend-tree failures
     refuse every candidate slot;
   - per candidate slot, per admitted index:
     `ConvertAdmittedMaterial` (`:363-479`) — the single family branch (`:378-382`):
     `captured.Family != CapturedAlphaMaterialFamily.Poiyomi` →
     `OpaqueConversionUnsupportedFamily`;
     then `TryAdmitDerivedEvidence` against the Poiyomi conversion request (`:390-398`),
     the runtime-overwrite rule against `PoiyomiOpaqueConversion.CanonicalOpaqueProperties`
     (`:407-421`), `ReadEffectiveRenderState` (live queue + `RenderType` tag, `:441-442`),
     `GatherConversionSourceEvidence` + `TryVerifyPoiyomiIdentity` (`:446-454`),
     `EvaluateVerifiedEligibility` (`:456-475`: `AlreadyOpaque` → map source to itself;
     `Convertible` → `PrepareCanonicalOpaqueClone`; else refusal);
   - avatar-wide dedup `RegisterPreparedOpaque` (`:490-503`): keyed by source material;
     created only after a slot's full admitted set mapped; decision is per
     renderer/slot, artifact is shared (`:236-253`).
5. **Poiyomi conversion core** — `Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`:
   canonical tuple of 23 properties (`:142-167`), queue 2000 and `RenderType=Opaque`
   (`:173-175`), conversion schema = recipe + `_EnableOutlines` (`:186-198,586-596`),
   eligibility gates (`:244-359`: outlines, premultiply, alpha-to-coverage, `_ZTest`
   LEqual, blend predicate, ForwardAdd factors, `_Cutoff` ≤ 1),
   `PrepareCanonicalOpaqueClone` (`:531-561`): `new Material(source)` — same shader —
   23 writes, queue, tag, then a 25-fact re-read validation that destroys the clone and
   throws on disagreement.
6. **Apply pass** — `AlphaSeparationApply`
   (`Editor/Build/AlphaSeparationApply.cs`): `PrepareSurvivingSet` validates every
   candidate slot against the live build avatar (renderer alive, mesh reference-equal,
   array lengths, marker clips, binding identity vs evidence, every keyframe value and
   live current material ∈ `OpaqueOfAdmitted`), finalizes against survivors, sweeps
   unreferenced transients; `ApplyFinalization` is the single mutation boundary.
   Everything downstream of the `Material → Material` mapping is shader-agnostic:
   mapping-based slot completeness, appended-slot indexing, curve rewriting, mesh
   finalization, sweep, and apply (`AlphaSeparationRecords.cs:99-365`,
   `AlphaSeparationApply.cs:81-430,475-550`).
7. **Test seam** — `VerifiedOpaqueConversion` (`AlphaSeparationPreparation.cs:26-31`)
   substitutes only the shader-family conversion step for public-package fixtures; it is
   typed with `PoiyomiOpaqueConversionRefusal` and reachable only through the internal
   fixture overload (`AmusePlatformFinishPlugin.cs:313-321`).

## 4. Existing lilToon support already present in AMUSE

All of it is **alpha analysis**, none of it conversion:

- `LilToonMaterialSemantics` (`Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`):
  `AnalyzeBaseMaterial` (`:127-148`), the verified-material seam
  `InterpretVerifiedMaterial` (`:158-207`), and `AlphaEvidenceRequest` (`:482-496`) —
  shader name + `_lilToonVersion` + `_Invisible` + `_UDIMDiscardCompile`.
  `InterpretVerifiedAlpha` (`:505-533`) returns **constant 1**, with an explicitly
  documented premise: *on the attested opaque (`LIL_RENDER 0`) variant alpha is forced to
  one after every alpha-writing block and the subpass alpha path is compiled out*
  (`:472-480`); `_Invisible`/`_UDIMDiscardCompile` remain as coverage gates because they
  remove fragments rather than change the value.
- `LilToonSourceAttestation` (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs`):
  pins exactly one shader identity — `SupportedShaderName "lilToon"`,
  `SupportedShaderGuid df12117ecd77c31469c224178886498e`,
  `PassShaderName "Hidden/ltspass_opaque"`, `PackageVersion 2.3.4`,
  `ShaderFormatVersion 45`, `OpaqueRenderMode 0`, and three canonical digests
  **measured from a real scratch install** (Task 0, 2026-08-18), deliberately never
  re-derived from the upstream repository because the tag's committed generated shaders
  are stale relative to its own generator (`:323-347,334-344`).
  `TryScanRenderMode` (`:852-890`) requires exactly one integer `#define LIL_RENDER`;
  `TryVerifyLilToonIdentity` (`:1078-1198`) conjuncts shader name/GUID, format/package
  version, pass identity, digests, and render mode 0; `GatherSourceEvidence`
  (`:1238-1314`) resolves the fixed `Hidden/ltspass_opaque` pass.
- Fixtures: `LilToonFixtureTestBase.CreateVerifiedMaterial`
  (`Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs:15-23`) builds a
  schema-complete stand-in that never needs the pinned digests because it exercises
  equations through the verified seam.

Consequence (`[SOURCE]`): a **cutout or transparent lilToon material is
`Unsupported` today** — its shader name is `Hidden/lilToonCutout` /
`Hidden/lilToonTransparent` / one-pass / two-pass, which is not `"lilToon"` — so it
fails family selection, the renderer's material-dependency closure fails, and **every
slot on that renderer** is refused renderer-wide. This is design §2.6/§11's recorded
coverage pressure
(`docs/superpowers/specs/2026-08-28-alpha-separation-vertical-slice-design.md:891-895`),
confirmed in code.

## 5. Official lilToon 2.3.4 render-mode behavior

All `[SOURCE]` unless labeled, all pinned to tag `2.3.4` /
`252fd8cfc46106d4967e95b3f2c788418502f227`:

- **Representation.** `Editor/lilEnumeration.cs:11-27`: `RenderingMode { Opaque, Cutout,
  Transparent, Refraction, RefractionBlur, Fur, FurCutout, FurTwoPass, Gem }`,
  `TransparentMode { Normal, OnePass, TwoPass }`. Mode identity is primarily a
  **shader-asset family**: `Editor/lilShaderManager.cs:8-46` maps, for the regular
  family, opaque `lilToon` (`lts`), cutout `Hidden/lilToonCutout` (`ltsc`), transparent
  `Hidden/lilToonTransparent` (`ltst`), one-/two-pass transparent, the **outline
  counterparts** `Hidden/lilToonOutline` (`ltso`) / `Hidden/lilToonCutoutOutline`
  (`ltsco`) / transparent-outline and one-/two-pass-outline assets, and the
  `Hidden/ltspass_opaque` / `Hidden/ltspass_cutout` / `Hidden/ltspass_transparent` pass
  assets, plus Lite/Tessellation/Multi counterparts. An outline source therefore has a
  **different opaque target asset** than its no-outline sibling: regular cutout without
  outline targets `lilToon`, regular cutout with outline targets
  `Hidden/lilToonOutline`. Declared tags:
  `lts.shader:638-644` `RenderType=Opaque, Queue=Geometry`;
  `lts_cutout.shader:639-644` `TransparentCutout, Queue=AlphaTest`;
  `lts_trans.shader:672-676` `TransparentCutout, Queue=AlphaTest+10` (one-/two-pass the
  same). There are **no mode keywords**; for Multi the mode is the numeric
  `_TransparentMode` property.
- **Vendor conversion path.** `Editor/lilMaterialUtils.cs`,
  `SetupMaterialWithRenderingMode` (`:18-315`), the edit-time conversion entry point.
  Opaque branch (`:38-70`): selects the family's opaque shader; writes
  `_SrcBlend=One`, `_DstBlend=Zero`, `_AlphaToMask=0`; outline counterpart writes
  `_OutlineSrcBlend=One`, `_OutlineDstBlend=Zero`, `_OutlineAlphaToMask=0`.
  Multi additionally writes override tag `RenderType=""` and `renderQueue=-1`.
  Common tail (`:266-315`): **if non-Multi, `renderQueue` is restored to its pre-call
  value** — the vendor does *not* reset the queue on a mode change;
  `_ZWrite=1` (except Gem), `_ZTest=4` unless `transparentMode == TwoPass`,
  `_OffsetFactor/_OffsetUnits=0`, `_ColorMask=15`,
  `_SrcBlendAlpha=One`, `_DstBlendAlpha=OneMinusSrcAlpha`,
  `_BlendOp/_BlendOpAlpha=Add`, ForwardAdd `_SrcBlendFA/_DstBlendFA=One`,
  `_SrcBlendAlphaFA=Zero`, `_DstBlendAlphaFA=One`, `_BlendOpFA/_BlendOpAlphaFA=Max`;
  outline resets Cull/ZWrite/ZTest/offsets/alpha factors/ops analogously.
  Coordinator spot-verified the Opaque branch and the entire common tail against the
  pinned tag's raw source.
- **Shader identity.** Conversion **changes shader asset identity** — opaque↔cutout/
  transparent is a shader swap on the converted material, not a property/keyword toggle.
  The vendor utility writes **no texture assignments, no feature-keyword changes, and no
  pass-enable changes**; feature keywords are independently derived for the Multi family
  by `SetupMultiMaterial` (`:356-520`).
- **No vendor safety policy.** The conversion switch has no refusal/validation branch —
  every safety/refusal rule is AMUSE policy, not vendor behavior. `[INFERENCE]`
- **Multi nuance.** For `ismulti` the utility *derives* the mode from
  `material.GetFloat("_TransparentMode")` (`:25-35`) and ignores the passed argument;
  converting a Multi material to opaque requires `_TransparentMode=0` first.

## 6. Exact proposed canonical opaque recipe, field by field

`[DECISION]` The canonical opaque state for the **first supported slice** — a pinned
lilToon 2.3.4 **regular, non-Lite, non-Tessellation, non-Multi, no-outline cutout**
material — is the vendor's own Opaque target state, assembled as an AMUSE-owned
version-pinned recipe in the same shape as `PoiyomiOpaqueConversion` — one shader asset,
one direction, one version. Outline sources are **outside** this slice: the vendor maps
`Hidden/lilToonCutoutOutline` to the opaque-with-outline asset `Hidden/lilToonOutline`
(`lilShaderManager.cs:14-16`), not to `lilToon`, so converting an outline source to
`lilToon` would silently drop its outline pass; the first slice refuses them instead
(§10). Outline support is a later, separate source-and-target attestation plus recipe
extension (§13, §15).

| Field | Value | Basis |
|---|---|---|
| shader | `lilToon` (the generated per-project no-outline opaque asset AMUSE already attests; the source's own outline state is irrelevant because this slice's source has no outline) | `lilShaderManager.cs:8-16`; family selection in `SetupMaterialWithRenderingMode` opaque branch, `!isoutl` |
| `_SrcBlend` | 1 (One) | `lilMaterialUtils.cs` opaque branch |
| `_DstBlend` | 0 (Zero) | same |
| `_AlphaToMask` | 0 | same |
| `_ZWrite` | 1 | common tail |
| `_ZTest` | 4 (LessEqual) | common tail (vendor skips only when transparentMode == TwoPass; an AMUSE recipe must not inherit that conditional — set 4 unconditionally and refuse materials whose visibility intent depends on a different comparison, mirroring Poiyomi gate 7) |
| `_OffsetFactor` / `_OffsetUnits` | 0 | common tail |
| `_ColorMask` | 15 | common tail |
| `_SrcBlendAlpha` / `_DstBlendAlpha` | 1 (One) / 10 (OneMinusSrcAlpha) | common tail |
| `_BlendOp` / `_BlendOpAlpha` | Add (0) | common tail |
| `_SrcBlendFA` / `_DstBlendFA` | 1 / 1 | common tail |
| `_SrcBlendAlphaFA` / `_DstBlendAlphaFA` | 0 / 1 | common tail |
| `_BlendOpFA` / `_BlendOpAlphaFA` | Max / Max | common tail |
| `renderQueue` | `2000` (the opaque asset's declared `Geometry`) — set explicitly | `[DECISION]`: the vendor restores the pre-call queue for non-Multi (`:266-267`), so its own utility does *not* produce a canonical queue; AMUSE's contract (queue move authorized by the opacity proof, root `AGENTS.md` §Correctness policy) wants the canonical value, and read-back validation must check it |
| `RenderType` override tag | `Opaque` — set explicitly | `[DECISION]`: set and validate the canonical effective tag. `[MEASURED]` B1 found that, in Unity 2022.3.22f1 with the installed 2.3.4 package, assigning the opaque shader cleared a unique effective source override and read back the target's declared `Opaque` tag even on the vendor path; the explicit AMUSE write remains a deterministic canonical-state contract, not remediation for a measured stale override (`2026-08-30-liltoon-opaque-characterization.md` §§8, 10). |
| `_Cutoff` / `_AlphaMask*` / dither / dissolve properties | **not written** | `[SOURCE]`: the utility writes none of them. On the opaque asset `LIL_RENDER 0` excludes the alpha/clip path at compile time (`LilToonMaterialSemantics.cs:472-480`), so they cannot affect the proven-opaque triangles. `[INFERENCE]` they are not recipe properties and must not enter the conversion evidence request as read+write facts; whether any is an eligibility gate is a §7 open question |

What the recipe deliberately does **not** do: change texture assignments, clear feature
keywords, or touch pass enables (the vendor does none of this); convert cutout-outline
(`Hidden/lilToonCutoutOutline`), any other outline family, Multi, Lite,
Tessellation, Fur, Refraction, RefractionBlur, Gem, one-pass/two-pass transparent, or any
overlay/fallback family (separate shader identities and pipelines with no pinned
conversion contract — refuse; see §10). It contains **no outline-only properties**:
every `_Outline*` field was removed with the outline scope.

`[INFERENCE]` The clone is `new Material(source)` (per the characterized clone-fidelity
recipe) followed by a shader-identity change to the attested opaque asset plus the tuple
above, then a full re-read validation of every written fact, mirroring
`PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone` including the throw-on-disagreement
policy. Unlike Poiyomi, **the clone's shader differs from the source's shader** — the
Poiyomi clone's shader-preservation check is not portable as-is and the read-back
validation must instead verify the expected opaque shader identity.

## 7. Evidence capture and runtime-validation requirements

- **Conversion evidence request.** The recipe's scalar properties (above) plus whatever
  eligibility gates the lilToon implementation defines. `[DECISION]` it is a
  lilToon-specific `MaterialEvidenceRequest` returned by
  `CaptureRequestForFamily` as `Combine(LilToonMaterialSemantics.AlphaEvidenceRequest,
  lilToon conversion request)`, exactly mirroring the Poiyomi combination — the capture
  union machinery already supports this
  (`UnityAnimationEvidenceCapture.cs:352-374`, `UnityMaterialSemantics.cs:279-302`).
- **Captured before the barrier (Q4).** Everything animation-reachable: every recipe
  scalar (they are `material.<Property>`-animatable), plus the eligibility-gate
  properties, captured in the closed batch and admitted via the existing
  `TryAdmitDerivedEvidence` (exact-singleton against the material's own serialized
  default). The Poiyomi design's separation is preserved: conversion evidence rides the
  closed capture schema; ordinary alpha relevance stays narrow.
- **Read live during preparation (Q5).** Only facts animation cannot reach, same
  justification as `PoiyomiOpaqueConversion.ReadEffectiveRenderState`
  (`PoiyomiOpaqueConversion.cs:436-455`): `material.renderQueue`, the `RenderType`
  override tag, and — lilToon-specific — the **source shader asset identity**
  (name/GUID, i.e. the render-mode carrier). None is addressable by
  `material.<Property>` binding syntax. `[SOURCE]`+`[INFERENCE]`
- **Runtime validation.** Unchanged: pass 3 revalidates every live binding, keyframe
  value, and current material against the prepared mapping (`AlphaSeparationApply.cs:81-178,475-550`);
  the overwrite rule and `RuntimeMaterialValueNotMapped` machinery are mapping-based and
  family-agnostic.

## 8. Animation and overwrite-rule implications

- The renderer-wide overwrite rule transfers unchanged in structure: for every recipe
  property carrying an admitted conversion binding at the renderer path, the admitted
  value must already equal the canonical value
  (`AlphaSeparationPreparation.cs:400-421`; design §7.3). `[INFERENCE]` For lilToon this
  covers `_SrcBlend`/`_DstBlend`/`_ZWrite`/`_ZTest`/`_ColorMask`/blend ops/ForwardAdd
  factors etc. — all ordinary material floats, all animation-reachable.
- `[INFERENCE]` LilToon has no property like `_Mode` that re-labels a mode on the same
  shader asset: for non-Multi materials the mode carrier is the shader asset itself,
  which is not animatable, so there is no animated mode control to refuse. `_Cutoff`
  animation behaves like the Poiyomi `_Cutoff` case — conversion-eligibility-relevant
  only insofar as the source-variant alpha proof reads it; that proof does not exist yet
  (blocker B2).
- `[INFERENCE]` If the alpha proof for a non-opaque variant later depends on
  `_Cutoff`/`_AlphaMaskMode`/`_UseDither`, those become proof-relevant properties for
  the existing admission machinery — no new rule shape required.

## 9. NDMF/lilToon callback and generated-shader lifecycle

- AMUSE's three passes run inside NDMF (`-11000` early hook through `PlatformFinish` at
  the `-1025` late hook). lilToon 2.3.4 acts **after** NDMF: VRChat SDK preprocess
  callback at order `100` runs `SetShaderSettingBeforeBuild(materials, clips)` (shader
  regeneration from per-project settings) and `SetupMultiMaterial(materials, clips)`;
  postprocess restores (`External/Editor/VRChatModule.cs:18-106`;
  `lilToonEditorUtils.cs:774-788`; `lilToonSetting.cs:897-1011`). lilToon 2.3.4 has **no
  `IPreprocessShaders`** handler `[SOURCE]` (CHANGELOG records its removal in an earlier
  release).
- `[MEASURED]` (prior recorded probe,
  `docs/superpowers/specs/2026-08-21-liltoon-build-callback-handoff-design.md:97-120,154-160`):
  in the characterized simple case, renderer paths/slots, materials, shaders, relevant
  values, and animation references were unchanged across callback `100`; only generated
  shader files changed. `SetupMultiMaterial` was not exhaustively exercised.
- **Answer to Q9 — conditional, two cases.** The committed handoff investigation makes
  Outcome B (the upload-only late validation gate) mandatory for every positive proof
  **depending on callback-`100`-generated shader state**, while a proof complete from
  NDMF-visible evidence may use its independent lifecycle
  (`liltoon-build-callback-handoff-design.md:26`, `:325-345`). Which case a lilToon
  conversion falls into is decided by the cutout alpha proof (B2), which does not exist
  yet. `[SOURCE]`+`[INFERENCE]`
  1. **Recommended narrow NDMF-complete cutout slice.** The pinned forward pass makes
     the dependence concrete: the cutout alpha path sits behind compile-time feature
     conditionals — `LIL_FEATURE_ALPHAMASK` (`lil_pass_forward_normal.hlsl:362`, also
     `:195`), `LIL_FEATURE_DISSOLVE` (`:369`, `:202`), `LIL_FEATURE_DITHER` (`:388`,
     `:221`) — whose active set is baked into the generated shader by
     `SetShaderSettingBeforeBuild` at callback `100`
     (`lilToonSetting.cs:897-946`), and the clip itself is `LIL_RENDER`-branched
     (`:394-411`). The opaque theorem escapes this because alpha-1 holds for **every**
     compilation of `LIL_RENDER 0`; a cutout proof must earn the same
     compilation-invariance. **B2 design obligation:** the cutout proof may establish
     only a callback-independent core case — attested equations over captured
     material/texture evidence for materials whose alpha path is feature-independent —
     and must conservatively refuse every optional generated alpha path, at minimum
     alpha mask, dither, dissolve, and any other alpha-affecting feature whose
     correctness depends on callback-generated compilation. Why callback `100` then
     cannot change the proof result: the regenerated shader changes only *which*
     optional feature blocks are compiled; with every alpha-affecting optional feature
     refused, the remaining equation (alpha from `_Color`/`_MainTex` evidence versus
     `_Cutoff`) is identical under every compilation of the attested source, so the
     proven-opaque triangle set cannot change after the barrier. **If source review
     shows even this restricted core still depends on callback-generated source, Outcome
     B becomes a prerequisite instead** — the NDMF-complete claim must not be forced.
  2. **Future positive support for generated optional alpha features** (alpha-masked,
     dithered, dissolved cutouts): Outcome B remains **required** unless separate
     evidence proves the result independent of callback-generated source.
  Until B2 discharges obligation 1, this note claims **no** lifecycle verdict for
  lilToon conversion. Residual facts, recorded either way:
  - `SetupMultiMaterial` rewrites keywords/pass enables for the **Multi** family; the
    recipe refuses Multi-family sources (§10), so no converted clone is Multi.
    `[INFERENCE]`
  - The converted clone references the same generated per-project opaque shader asset
    AMUSE already attests; project-setting regeneration changes file contents, not the
    asset identity the clone references. `[INFERENCE]`
  - NDMF `Finish()` may deduplicate/rewrite object identity; pass 3 already reads live
    state rather than barrier identity (falsifier 20). `[SOURCE]`
  - Apply-on-Play remains unavailable for positive mutation per the coexisting-lifecycle
    design v1 (`coexisting-optimizer-lifecycle-design.md:542-548`) — unchanged for
    lilToon.

## 10. Refusal scope and unsupported cases

`[DECISION]` The lilToon implementation refuses, with slot-local refusals where the
current architecture already expresses them:

- Cutout-outline (`Hidden/lilToonCutoutOutline`) and every other outline family
  (`Hidden/lilToonOutline`, transparent-outline, Lite/Tessellation outline variants):
  their vendor opaque target is an outline asset, not `lilToon`, so this slice's recipe
  does not apply to them; the first slice refuses them. Converting an outline source to
  `lilToon` would silently drop its outline pass and is not a preservation move.
- Multi (`Hidden/lilToonMulti*`), Lite, Tessellation, Fur, FurCutout, FurTwoPass,
  Refraction, RefractionBlur, Gem, transparent, and one-pass/two-pass transparent
  families: separate shader identities/pipelines; no pinned conversion contract.
  (Two-pass additionally breaks the vendor's own `_ZTest` reset conditional — the
  recipe refuses it regardless.)
- Materials whose effective blend state does not satisfy the alpha-1 degeneration
  predicate at the source variant, mirroring Poiyomi gates — exact gate set is part of
  the future implementation design, not fixed here.
- Coverage mechanisms the source-variant alpha proof cannot discharge (dither, alpha
  mask, dissolve) remain refused by that proof's unknown/unsupported outcomes.
- Renderer-scoped refusals (unrecognized conversion binding, additive layer,
  unnormalized direct blend tree) and slot-local refusals (marker clip, unmapped value,
  not-admitted state, overwritten canonical property) transfer unchanged.
- `[SOURCE]` Today's renderer-wide refusal of *unattestable* lilToon variants
  (§4) remains until attestation is extended; extending attestation changes this
  coverage (see §11) and is scoped below.

## 11. Mixed-family behavior

- Today: mixed Poiyomi/lilToon admitted sets refuse (`OpaqueConversionUnsupportedFamily`,
  falsifier 9); mixed families cannot all map, so the slot refuses
  (`AlphaSeparationPreparation.cs:375-382`).
- With a working no-outline-cutout lilToon conversion: `[INFERENCE]` a slot whose
  admitted set mixes attested Poiyomi and attested no-outline-cutout lilToon materials
  **can** map completely and becomes convertible — each admitted value maps through its
  own family's conversion. This is a coverage expansion, not a blocker; whether to
  enable it in the first slice or to keep refusing mixed slots initially is a controller
  decision, but the mapping architecture does not force either answer.
- Same-renderer sibling behavior (Poiyomi slot optimizes beside a refused lilToon slot)
  is already proven (falsifier 10) and is unchanged; within the first slice a refused
  lilToon slot is any non-attestable or outline/other-family lilToon material.

## 12. Smallest viable integration shape

`[DECISION]` The investigation's answer to Q10: **not** "one request + one
implementation + one case" as literally declared. The verified smallest shape is:
   **First-slice scope:** regular, non-Lite, non-Tessellation, non-Multi, **no-outline
   cutout** sources only, converting to the attested `lilToon` opaque asset. Every other
   lilToon family — outline, Multi, Lite, Tessellation, Fur, Refraction, Gem,
   transparent, one-/two-pass — refuses (§10) and is later work (§13).

1. **lilToon conversion evidence request** added to `CaptureRequestForFamily` for
   `CapturedAlphaMaterialFamily.LilToon` — as declared.
2. **`LilToonOpaqueConversion`** — new Poiyomi-opaque-conversion-shaped module: pinned
   recipe (§6), eligibility, clone preparation with read-back validation.
3. **One new family case** in `ConvertAdmittedMaterial` — as declared.
4. **Required narrow refactors the current requirement demonstrably forces** (§13):
   - a per-family conversion request at the conversion-relevance resolution
     (`AlphaSeparationPreparation.cs:93-123`) and inside `ConvertAdmittedMaterial`
     (admission `:390-398`, overwrite rule `:407-421`) — the Poiyomi request is
     hard-coded in all three places;
   - `CaptureRequestForFamily` returning the combined schema (item 1 covers this);
   - variant-aware family selection + attestation for the supported non-opaque source
     assets (`IdentifyFamily`, `LilToonSourceAttestation`) — see blockers.
5. No change to: geometry planning, mesh finalization, appended-slot indexing,
   animation discovery/rewriting, pass-3 validation, the sweep, the apply boundary,
   the prepared-record shape, or the avatar-wide dedup — all mapping-based
   (`AlphaSeparationRecords.cs:106-259`, `AlphaSeparationApply.cs:81-430`).
   `[SOURCE]`

No `IOpaqueConversion`, no adapter interface, no registry, no conversion result
hierarchy, no render-state IR, no `SurfaceMode` classifier. The second implementation
demonstrates exactly the parameterization listed in item 4 — three hard-coded
Poiyomi facts become per-family facts — and nothing more. `[INFERENCE]`

## 13. Required refactoring versus deferred architectural pressure

**Demonstrated now (the one-switch boundary does not survive literally):**

| # | Pressure | Evidence |
|---|---|---|
| R1 | Conversion-relevance resolution, derived admission, and the overwrite rule are Poiyomi-hard-coded **before/around** the family branch; a lilToon case cannot correct evidence routed under the wrong request | `AlphaSeparationPreparation.cs:93-123,390-421` `[SOURCE]` |
| R2 | Family selection is exact-name and duplicated at two sites (`IdentifyFamily` and `CaptureAlphaMaterials`/`UnityRendererAlphaAnalysis.cs:390-405`); non-opaque lilToon sources are `Unsupported` | `UnityMaterialSemantics.cs:242-264,95-136` `[SOURCE]` |
| R3 | lilToon attestation pins exactly one opaque shader/pass identity and `OpaqueRenderMode 0`; `GatherSourceEvidence` resolves the fixed `Hidden/ltspass_opaque` pass | `LilToonSourceAttestation.cs:323-347,1078-1198,1238-1314` `[SOURCE]` |
| R4 | `InterpretVerifiedAlpha`'s constant-1 proof is valid **only** under its `LIL_RENDER 0` premise; it must not be applied to cutout/transparent sources | `LilToonMaterialSemantics.cs:472-533` `[SOURCE]` |
| R5 | The Poiyomi clone's shader-preservation check is wrong for a family whose conversion changes shader identity; the read-back validation is per-family | §6 `[INFERENCE]` |
| R6 | The `VerifiedOpaqueConversion` test seam is typed `PoiyomiOpaqueConversionRefusal`; a lilToon verified seam needs its own refusal type or a family-agnostic shape | `AlphaSeparationPreparation.cs:26-31` `[SOURCE]` |

**Deferred (record, do not build):**

- Outline families: a later, separate extension requiring **both** a source attestation
  (`Hidden/lilToonCutoutOutline` etc.) **and** a target attestation
  (`Hidden/lilToonOutline` etc., with its own pass identity and digests) plus an
  outline-aware recipe — recorded, not designed here.
- Other lilToon families (Multi, Lite, Tessellation, transparent variants) and
  integration packages (LTCGI/AudioLink/VRC Light Volumes) — each is separate
  attestation + conversion pressure; the official-integration matrix keeps them
  unimplemented.
- Callback-`100`-dependent positive proofs — Outcome B is required for them (§9 case 2);
  it applies only if B2's restricted core cutout proof cannot be made
  callback-independent, or when generated optional alpha features are admitted later.
- Per-slot durable diagnostics — unchanged from plan item 1.
- A third family, when it arrives — that is when a registry "earns its first honest
  argument" (`UnityMaterialSemantics.cs` doc comment; design §11). Two families
  parameterize three facts; they do not yet justify dynamic discovery.

## 14. Required falsifiers and vacuity guards

Public synthetic tests (no installed lilToon package), following the
`VerifiedPoiyomiTestSeams` / `LilToonFixtureTestBase` patterns:

1. Family selection returns the lilToon conversion-aware capture schema for an attested
   non-opaque stand-in; ordinary alpha relevance for unrelated materials is bit-for-bit
   unchanged (guards against widening alpha relevance — the regression the merged
   capture-schema split exists to prevent).
2. A lilToon candidate slot prepares: mixed admitted set maps; `AlreadyOpaque`
   (canonical lilToon source) maps to itself with no clone.
3. Conversion-only animation of a lilToon recipe property: away from canonical refuses
   (`ConversionPropertyOverwrittenAtRuntime`), at canonical prepares (mirrors
   falsifier 11).
4. The generated clone carries the canonical recipe: every recipe fact reads back
   canonical; the source material's full property set is unchanged (persistence/
   preservation tests extended to the lilToon clone).
5. Same-renderer sibling: Poiyomi slot optimizes beside a refused lilToon slot
   (renderer-scoped refusal scoping preserved) — re-run falsifier 10 against the new
   case.
6. Mixed Poiyomi/lilToon slot behavior matches the controller's §11 decision
   (convertible or explicitly refused).
7. Cutout-outline, Multi/Lite/Gem/transparent stand-ins refuse with the family refusal
   — vacuity guard that the recipe does not silently convert unattested families and
   never drops an outline pass by converting an outline source to `lilToon`.
8. **Per-family routing regression — the executable R1 boundary obligation.** A
   dedicated Poiyomi-focused regression proving that after the R1 refactor the Poiyomi
   path still routes through `PoiyomiOpaqueConversion.ConversionEvidenceRequest`
   (conversion-relevance resolution and derived admission), checks the runtime-overwrite
   rule against `PoiyomiOpaqueConversion.CanonicalOpaqueProperties`, and invokes the
   existing `PoiyomiOpaqueConversion` implementation — observable as identical Poiyomi
   eligibility/clone outcomes before and after the refactor. **All existing
   Poiyomi-focused tests must remain unchanged and green.** Separately, and optionally:
   a temporary mutation-sensitivity probe (delete the lilToon case locally, observe
   Poiyomi routing is untouched, restore) may be recorded as evidence that the routing
   is genuinely family-selected; it is a probe, not this falsifier, and is never a
   committed test.
9. Structural guard: no `SaveAsset` in the new files (mirrors
   `AlphaSeparationPersistenceTests`).
10. **Callback-independence boundary of the cutout proof (§9 obligation, falsifies a
   proof that silently depends on generated compilation):** for a stand-in whose alpha
   path is feature-conditional (alpha-mask/dither/dissolve properties active), the
   cutout proof must return unknown/refuse — never a proven-opaque triangle — and for
   the restricted feature-independent core it must return the same triangle verdict for
   every attested compilation variant of the source, demonstrating the verdict is
   invariant under callback-`100` regeneration.

`[INFERENCE]` These falsify the plausible wrong implementations: widening alpha
relevance, skipping the per-family request routing (test 1), wrong queue/tag/blend state
(test 4), silent family over-acceptance (test 7), Poiyomi mis-routing or altered
Poiyomi behavior after the R1 refactor (test 8), and proof dependence on generated
compilation (test 10).

## 15. Blockers or prerequisites

| # | Blocker | Nature |
|---|---|---|
| B1 | **One combined pre-design characterization prerequisite** (supersedes the former separate B1+B4; must be **measured before production design fixes the recipe and its validation contract**): a Task-0-style scratch `jp.lilxyzw.liltoon@2.3.4` install session measuring, for the regular **no-outline cutout source** and the **no-outline opaque target**: shader names, GUIDs, pass identities (`Hidden/ltspass_cutout` / `Hidden/ltspass_opaque`), render modes, and canonical digests — digests must be **measured from a real install**, never from the upstream tag (`LilToonSourceAttestation.cs:334-344`); the actual result of assigning the opaque shader to a cloned cutout material; queue behavior (`renderQueue=-1`, explicit 2000, inherited/custom queues) and effective `RenderType`; `SetOverrideTag("RenderType", …)` and clearing behavior; and every canonical recipe property's read-back behavior. | Characterization prerequisite; requires a real installed package session. |
| B2 | **Alpha proof for non-opaque variants, with an explicit callback-independence obligation.** `InterpretVerifiedAlpha` is an opaque-variant theorem. Cutout sources need their own conservative alpha evidence request and interpretation — `_Color.a`, `_MainTex` alpha, `_Cutoff`, `_AlphaMask`, dither, dissolve — or conversion has no triangle proof to build on; this is upstream of conversion and is the largest missing piece. **Design obligation (§9 case 1):** the proof may establish only a callback-independent core cutout case and must conservatively refuse every optional generated alpha path — at minimum alpha mask, dither, dissolve, and any other alpha-affecting feature whose correctness depends on callback-generated compilation. Its falsifier is §14 test 10. **If even the restricted core turns out to depend on callback-generated source, Outcome B becomes a prerequisite instead of an NDMF-complete claim.** The proof's design is a separate controller-reviewed task; this investigation does not scope it. | Missing capability, not a refactor. |
| B3 | **Scope decision.** Confirmed by this revision: the first slice is regular no-outline cutout only (AlphaTest). Transparent (AlphaBlend, incl. premultiplied-adjacent states) and every outline family are materially larger, separate proofs. | Controller decision — this revision resolves it for the first slice. |

## 16. Recommendation and next decision

**Recommendation.** `[DECISION]` The extension boundary survives **structurally but not
literally**. The Poiyomi pipeline's shader-agnostic halves (mapping, records, mesh,
curves, validation, sweep, apply, dedup) carried the second family without change — that
is the design's real victory. What failed is the "one case and nothing else" claim: the
family branch is preceded by three Poiyomi-hard-coded facts (R1) and the lilToon source
variants cannot even reach the branch today (R2/R3/R4). The recommended first slice is
**regular, non-Lite, non-Tessellation, non-Multi, no-outline cutout only**, converting to
the already-attested `lilToon` opaque asset; every outline family is refused in this
slice and recorded as a later separate source-and-target attestation plus recipe
extension (§13). Its lifecycle verdict is **conditional**: NDMF-complete only under B2's
restricted callback-independent core proof (§9 case 1); otherwise Outcome B applies. The
honest shape is §12: the declared request + implementation + case, **plus** the
per-family parameterization of conversion relevance/admission/overwrite, **plus** the
combined pre-design characterization (B1) and the cutout alpha proof (B2) — none of
which licenses a registry, interface, or IR at two families.

**Next decisions for the controller (in dependency order):**

1. First-slice scope (B3): **regular, non-Lite, non-Tessellation, non-Multi, no-outline
   cutout only**, per this revision. Outline, transparent, and all other families are
   refused and deferred (§10, §13).
2. Commission the **combined pre-design characterization** (B1): a scratch lilToon 2.3.4
   install session measuring the no-outline cutout source and no-outline opaque target
   identities/digests **and** the queue/tag/read-back behaviors — before production
   design fixes the recipe and validation contract.
3. Commission the cutout alpha-semantics design (B2) as its own reviewed task, carrying
   the §9 callback-independence obligation and its §14 falsifier; it is the largest
   prerequisite and gates everything else. If its restricted core cannot be made
   callback-independent, Outcome B becomes a prerequisite.
4. Only then design the conversion implementation against the fixed recipe of §6 and
   the falsifiers of §14.

---

### Coordinator Q&A summary

1. **Selected/captured/attested lilToon today** — exact name `"lilToon"` only (the
   opaque asset); alpha-relevance = `LilToonMaterialSemantics.AlphaEvidenceRequest`
   (shader name + `_lilToonVersion` + `_Invisible` + `_UDIMDiscardCompile`); capture
   schema is the same request (no conversion request); attested via
   `TryVerifyLilToonIdentity` against the opaque pins of §4.
2. **Missing conversion evidence in `CaptureRequestForFamily`** — everything: there is
   no lilToon conversion request; the schema is the alpha request alone
   (`UnityMaterialSemantics.cs:297-298`).
3. **Canonical opaque recipe (first slice)** — §6: `lilToon` opaque shader identity +
   the scalar resets (base blend/depth/offset/color-mask set and ForwardAdd set — **no
   outline properties**) + explicit queue 2000 + `RenderType=Opaque` tag; no
   texture/keyword writes.
4. **Captured before the barrier** — all recipe scalars and eligibility-gate
   properties (animation-reachable), via the combined schema (§7).
5. **Read live during preparation** — `renderQueue`, `RenderType` tag, source shader
   asset identity (name/GUID): not animation-addressable (§7).
6. **Animated properties making conversion unsafe** — any recipe property driven away
   from its canonical value (existing overwrite rule); `_Cutoff`-style proof-relevant
   properties once the cutout alpha proof exists (§8).
7. **Shared source material, two renderers** — yes: one opaque artifact avatar-wide,
   each renderer/slot independently validating family/admission/overwrite
   (`AlphaSeparationPreparation.cs:236-253,490-503`); unchanged for lilToon.
8. **Mixed admitted sets** — currently refused; with a working no-outline-cutout
   conversion they can map and convert; controller decision whether to enable initially
   (§11).
9. **Callback/generated-shader conflict with the third pass** — conditional (§9): the
   recommended narrow cutout slice must restrict its proof to a callback-independent
   core (B2 obligation: refuse alpha mask/dither/dissolve and every other
   compilation-dependent alpha path), in which case callback `100` cannot change the
   verdict because the equation is identical under every compilation of the attested
   source. Until B2 discharges that obligation no lifecycle verdict is claimed; if the
   restricted core still depends on callback-generated source, Outcome B is required.
   Generated optional alpha features always require Outcome B unless proven independent.
10. **One request + one implementation + one case?** — the request/implementation/case
    survive, but three Poiyomi-hard-coded facts (conversion relevance, derived
    admission, overwrite rule) must be parameterized first, and attestation + alpha
    proof for non-opaque sources are prerequisites (§12, §13).
11. **Demonstrated refactoring pressure** — R1-R6 in §13; all narrow, none licensing an
    abstraction.
12. **Hypothetical, not to generalize** — outline families, Lite/Tess/Multi/transparent
    variants, integration packages, callback-dependent proofs, per-slot diagnostics,
    registries at two families (§13).
13. **Public falsifiers without installing lilToon** — §14's ten, on the existing
    stand-in/verified-seam patterns.
14. **Real package / new characterization required?** — yes, one **combined pre-design
    characterization** (B1, former B1+B4): measured no-outline cutout source and
    no-outline opaque target names/GUIDs/pass identities/render modes/digests, plus
    clone-assignment, queue (`-1`/explicit 2000/inherited/custom), `RenderType`
    override-tag, and per-recipe-property read-back behavior — measured before
    production design. The cutout alpha-semantics design (B2) is a separate
    prerequisite task, not installable here.
