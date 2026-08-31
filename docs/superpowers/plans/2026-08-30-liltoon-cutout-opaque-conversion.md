# lilToon Regular Cutout → Canonical Opaque Conversion — Implementation Plan

| | |
|---|---|
| Spec | `docs/superpowers/specs/2026-08-30-liltoon-cutout-opaque-conversion-design.md` |
| Branch | `feature/liltoon-cutout-opaque-conversion` |
| Base SHA | `a0b46e716e811ab5010dc33c6f805b55463b7e53` (verified equal to `origin/main`) |
| Tech Stack | Unity 2022.3.22f1, NDMF (pinned, embedded), NUnit EditMode, pinned lilToon 2.3.4 facts (vendor package **absent** from this project) |
| Census Lab | not used |
| Planning-session note | no C# LSP server was available; every caller of every renamed/re-typed symbol was enumerated by repository search during planning and is listed in the touched task. If an LSP server is available during implementation, re-run references before each rename |

Line numbers below cite the base SHA and drift with edits; re-read before editing.

---

## Global Constraints

1. **Scope.** Production changes are limited to the files in the File map. No Unity
   asset, `.meta`, scene, prefab, material, mesh, texture, import setting, animation
   asset, package manifest, or lockfile is created or modified — with one explicit
   exception: the two new public synthetic fixture shaders added in Task 2 Step 1
   (`Tests/Editor/Semantics/LilToon/LilToonCutoutConversionTest.shader` and
   `LilToonOpaqueConversionTest.shader`, each committed together with its
   Unity-generated `.meta` as one unit, minimal/deterministic/redistributable per
   the repository fixture policy, following the checked-in
   `LilToonSemanticTest.shader` precedent). The pre-existing user-owned
   working-tree changes (`Packages/manifest.json`, `Packages/packages-lock.json` —
   toolchain package churn, inspected additive-only) are never staged, restored,
   or absorbed.
2. **No vendor package.** Every test runs on public synthetic fixtures and the
   verified seams. No test may require an installed lilToon package.
3. **RED/GREEN.** Every behavioral step lands as: failing test observed for the named
   plausible wrong implementation, then minimal implementation, then the same tests
   green. No step claims green without an observed run. Any behavioral assertion
   that passes on its first run — because the code under test was already
   implemented in an earlier step of the same task (e.g. Task 2's gate matrix
   against the scaffold, Task 3's tuple/clone assertions) — has no observed RED by
   construction and MUST be mutation-verified instead: introduce the named wrong
   implementation deliberately, observe the assertion fail, restore, and record
   that verification in the step's checkpoint report. Unverified immediately-green
   assertions may not be cited as falsifiers.
4. **No placeholders.** Any scaffold used to obtain a behavioral RED (Task 1 step 1's
   deliberate opaque-profile mis-wiring, Task 3 step 1's refuse-all eligibility
   scaffold) is replaced in the same task; the final tree contains no
   `NotImplemented`, stub, or dead scaffold.
5. **Poiyomi is untouchable.** `PoiyomiMaterialSemantics.cs` and
   `PoiyomiOpaqueConversion.cs` are read-only throughout. All existing Poiyomi-focused
   tests must remain unmodified and green (Task 4 regression).
6. **Ordinary opaque lilToon is untouchable.** `LilToonMaterialSemantics.cs`
   production code is not modified in this plan; `LilToonAttestationTests`,
   `LilToonAlphaTests`, `LilToonBaseColorTests`, `LilToonEmissionTests`,
   `LilToonNormalTests`, `LilToonAdversarialTests` must remain green unmodified.
7. **Unity MCP gate.** Every Unity MCP operation begins with: enumerate instances
   read-only; read `Application.dataPath`; require exactly
   `Application.dataPath == <repo-root>/Assets`; no case-only match; pin the exact
   instance when more than one is reachable. Failing the gate stops the session.
8. **No git mutations.** Nothing is staged, committed, pushed, tagged, or published.
   Final phase inspects `git status`, unstaged diff, and `git diff --check`, and
   reports.
9. **Baseline counts.** At Task 1 step 1, record the full-suite passed/failed counts
   as the baseline. Every later full run reports *baseline + newly added tests,
   0 failed*.

## File map

Production (all under `Packages/com.alrauna.amuse/Editor/`):

| File | Tasks | Change |
|---|---|---|
| `Semantics/LilToon/LilToonSourceAttestation.cs` | 1 | Add cutout pins + `LilToonSourceProfile`; profile-parameterize `GatherSourceEvidence`/identity conjunction; add `GatherCutoutSourceEvidence`, `TryVerifyLilToonCutoutIdentity`; opaque public surface unchanged |
| `Semantics/LilToon/LilToonCutoutMaterialSemantics.cs` | 2 | **New.** Cutout alpha evidence request + `InterpretVerifiedCutoutAlpha` (+ verified-material seam for feature-variation tests) |
| `Semantics/LilToon/LilToonOpaqueConversion.cs` | 3 | **New.** Outcome/refusal/eligibility types, 18-property tuple, queue/tag constants, `ConversionEvidenceRequest`, `EvaluateVerifiedEligibility`, `ReadEffectiveRenderState`, `PrepareCanonicalOpaqueClone(source[, attestedTarget])`, production target resolution |
| `Semantics/UnityMaterialSemantics.cs` | 4 | `CapturedAlphaMaterialFamily.LilToonCutout`; private `ClassifyShaderName`; update `CaptureAlphaMaterials`, `IdentifyFamily`, `AlphaRequestForFamily`, `CaptureRequestForFamily`, `IsAttestedAlphaMaterial`, `AnalyzeAlphaMaterial`, `BuildCapturedAlphaMaterials` |
| `Build/AlphaSeparationPreparation.cs` | 4 | Rename delegate → `VerifiedPoiyomiConversion`; add `VerifiedLilToonConversion`; per-family conversion facts; union relevance loop with per-family buckets; family switch in `ConvertAdmittedMaterial` |
| `Build/AmusePlatformFinishPlugin.cs` | 4 | Seam-overload signatures carry both delegates (`:223`, `:320`, `:693`) |
| `Build/AlphaSeparationRecords.cs` | 4 | Doc-comment correction on `OpaqueConversionUnsupportedFamily` (`:30-34`) only |
| `Semantics/LilToon/LilToonMaterialSemantics.cs` | — | **Unmodified** (constraint 6) |

Tests (all under `Packages/com.alrauna.amuse/Tests/Editor/`):

| File | Tasks | Change |
|---|---|---|
| `Semantics/LilToon/LilToonAttestationTests.cs` | 1 | Cutout profile verify matrix; opaque matrix untouched |
| `Semantics/LilToon/LilToonCutoutAlphaTests.cs` | 2, 6 | **New.** Gate matrix, multiplier, sampling/ST boundaries, feature-variation invariance |
| `Semantics/LilToon/LilToonOpaqueConversionTests.cs` | 3, 6 | **New.** Tuple shape, eligibility matrix, clone recipe read-back, source preservation, throw-on-wrong-target |
| `Semantics/UnityMaterialSemanticsTests.cs` | 4 | Cutout family selection + routing; opaque/Poiyomi selection unchanged |
| `Build/VerifiedPoiyomiTestSeams.cs` | 4 | Delegate type rename only |
| `Build/VerifiedLilToonTestSeams.cs` | 4 | **New.** lilToon fixture request/capture/conversion seams |
| `Build/AlphaSeparationPreparationTests.cs` | 4, 5, 6 | Test-local request selector extended; lilToon candidate-slot preparation tests |
| `Build/AlphaSeparationApplyTests.cs` | 4, 5 | Delegate rename; lilToon apply-path tests |
| `Build/AmusePlatformFinishPluginTests.cs` | 5 | End-to-end NDMF tests through the seams |
| `Build/AlphaSeparationPersistenceTests.cs` | 6 | `AuditedProductionFiles` extended; source-preservation digest scenario extended |
| `Semantics/LilToon/LilToonFixtureTestBase.cs` | 2 | Mipmap-capable texture importer helper |
| `Semantics/LilToon/LilToonCutoutConversionTest.shader` (+ `.meta`) | 2 | **New asset** (Constraint 1 exception). Cutout-shaped schema-complete stand-in |
| `Semantics/LilToon/LilToonOpaqueConversionTest.shader` (+ `.meta`) | 2 | **New asset** (Constraint 1 exception). Distinct opaque-shaped schema-complete stand-in carrying the full 18-property tuple |

## Falsifier map (spec §16 and task coverage items)

| # | Coverage item | Task |
|---|---|---|
| 1 | Attested regular cutout family selection | 4 |
| 2 | Unchanged ordinary opaque-lilToon analysis; opaque map-to-self | 4, 5 |
| 3 | Unchanged Poiyomi routing/conversion after R1 | 4 (+ full regression) |
| 4 | Fully opaque texture, every mip, `_Color.a = 1` → proven | 2 |
| 5 | `_Color.a = 0.8` → uniform transparent/refused | 2 |
| 6 | Cutoff `0.9999` proven / `1.0` & `1.001` no provable triangle / non-finite refuses | 2 (alpha gate), 3 (eligibility), 5 item 4 (slot outcomes) |
| 7 | Non-finite cutoff refusal | 3 |
| 8 | Transparent texel only in a high mip | 2 item 7 (resolver seam + imported-mip integration) |
| 9 | Bilinear footprint dilation vs point | 2 |
| 10 | Repeat seam vs clamp | 2 |
| 11 | Non-singleton animation on every proof/conversion-relevant property | 5, 6 |
| 12 | Each optional alpha/coverage path refuses independently | 2 (gate matrix), 6 (end-to-end) |
| 13 | `_IDMaskControlsDissolve` counterexample | 2, 6 |
| 14 | Compilation-variant / callback-100 invariance | 2 (feature-variation seam), 6 |
| 15 | Unsupported texture evidence never opaque | 2 |
| 16 | Identity-ST positive; non-identity/animated-ST refusal | 2 (mapping), 5/6 (animated) |
| 17 | Canonical clone recipe read-back | 3, 5 |
| 18 | Source material unchanged | 3 (unit), 6 (structural digest across real build) |
| 19 | Poiyomi + lilToon slots coexist; mixed supported sets map | 5 |
| 20 | Representative unsupported lilToon families refuse | 6 |
| 21 | End-to-end NDMF preparation/persistence/slot/apply | 4 (RED), 5 (full artifacts) |
| 22 | No `SaveAsset` / authoring-asset mutation | 6 (structural audit) |

## Test execution

`run_tests` is asynchronous: it returns a `job_id` and every read of results MUST
poll `get_test_job` to a terminal state and assert its counts before reading the
console or starting another run. Never overlap two test jobs.

Focused (per step):

```
<Unity MCP gate — Global Constraint 7>
read_console clear
job = run_tests EditMode group_names:["<full test class name>"]
get_test_job job_id=<job>   # poll to terminal state; assert passed/failed counts
read_console get types:[error,warning]
```

Full (Task 4, and final phase):

```
<Unity MCP gate>
read_console clear
job = run_tests EditMode assembly_names:["Alrauna.Amuse.Tests.Editor"]
get_test_job job_id=<job>     # poll to terminal; assert baseline + new, 0 failed
job = run_tests EditMode assembly_names:["Alrauna.Amuse.Research.Tests.Editor"]
get_test_job job_id=<job>     # poll to terminal; assert 0 failed
read_console get types:[error,warning]
```

If Unity is not running in this environment, the implementing session stops at the
gate and reports; no validation may be claimed without an observed run.

## Atomicity

Tasks 1–3 add production code with **no production callers** (compile-safe,
behavior-neutral; the full suite stays at baseline). Task 4 lands as two atomic
increments — 4a (family, capture, analysis wiring) and 4b (preparation routing,
seams R6) — each preceded by its own observed behavioral RED; after 4b the feature
is live end-to-end behind the seams. Tasks 5–6 are test-only. Task 7 is verification
and cleanup.

---

## Task 1: Cutout attestation profile (R3)

**Files:** `Editor/Semantics/LilToon/LilToonSourceAttestation.cs`;
`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`.

**Observable contract.** `LilToonSourceAttestation` verifies exactly two pinned
identities: the existing opaque one (byte-for-byte unchanged verdicts) and
`Hidden/lilToonCutout` / `Hidden/ltspass_cutout` / `LIL_RENDER 1` / digests
`c83d73a2…178` / `ecd1caed…e92` under the shared package/format/include-tree pins.
Any mismatch fails closed with a diagnostic. No name-only fallback.

### Step 1 — Cutout pins and profile scaffold (behavior-neutral)

Add the seven cutout constants (spec §6 R3 table) with the B1 provenance remark, a
private readonly `LilToonSourceProfile` record, `OpaqueProfile` built from the
existing pins, `CutoutProfile` from the new pins. Refactor mechanically:
`GatherSourceEvidence(shader, evidence)` delegates to a private
`Gather(shader, evidence, profile)` (pass resolution uses `profile.PassShaderName` at
the current `:1283`); `TryVerifyLilToonIdentity(evidence, out diag)` delegates to a
private `Verify(evidence, profile, out diag)`. Add `GatherCutoutSourceEvidence` and
`TryVerifyLilToonCutoutIdentity` delegating **deliberately to `OpaqueProfile`** — a
temporary mis-wiring that exists only so Step 2's positive assertion fails
behaviorally against live code rather than by compilation. No observable behavior
of any existing entry point changes.

Run: focused `LilToonAttestationTests` + `LilToonAlphaTests` + `LilToonBaseColorTests`
+ `LilToonEmissionTests` + `LilToonNormalTests` + `LilToonAdversarialTests`.
Expected: all green, unmodified.

### Step 2 — RED

Add to `LilToonAttestationTests` a cutout-evidence builder (mirroring the existing
`Evidence(...)` helper: shaderName `Hidden/lilToonCutout`, assetGuid
`85d6126c…`, passGuid `ad219df2…`, renderMode 1, shader/pass digests
`c83d73a2…`/`ecd1caed…`, same include digest, same canonicalization provenance
analyses) and tests asserting:

- `TryVerifyLilToonCutoutIdentity(cutoutEvidence)` is **true**, diagnostic null —
  fails against the step-1 scaffold (it still verifies against the opaque profile,
  which rejects the cutout name). Falsifies: reusing the opaque verifier, or
  name-only recognition.
- The full mismatch matrix refuses: each of wrong name, wrong GUID, wrong pass GUID,
  `LIL_RENDER 0` or 2 or unreadable, wrong shader digest, wrong pass
  digest, wrong include digest, wrong format stamp, wrong package version, mutated
  canonicalization provenance → false + expected diagnostic code (mirrors the existing
  opaque refusal tests; the matrix is restricted to facts `LilToonSourceEvidence`
  actually carries — it has no pass-name field). Falsifies: partial conjunctions.
- Gather-level fail-closed: `GatherCutoutSourceEvidence` in this project (no
  resolvable `Hidden/ltspass_cutout` pass asset) produces evidence that refuses
  verification rather than a name-only pass.
- `TryVerifyLilToonIdentity(cutoutEvidence)` stays **false** (opaque verifier rejects
  the cutout identity) and every existing opaque test still passes unmodified —
  guards profile leakage.

Run: focused `LilToonAttestationTests`. Expected: new tests FAIL behaviorally (never
compile), existing green.

### Step 3 — GREEN

Replace the scaffold delegation with real profile-parameterized verify/gather. All
cutout tests green; opaque tests untouched and green.

Run: focused suite of step 1. Expected: 0 failed.

**Self-review checkpoint.** Diff contains only `LilToonSourceAttestation.cs` +
`LilToonAttestationTests.cs`; no public opaque signature changed; digests entered as
measured constants with the never-re-derive remark; canonicalization provenance
expectations for the cutout pass (two removed regions, official setting record) are
pinned by the true-case test and, if the B2 §5 clause 1 premise fails here, STOP and
report (spec §17).

## Task 2: Cutout alpha semantics (R4)

**Files:** new `Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs`;
new `Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs`;
`Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs`.

**Observable contract.** `InterpretVerifiedCutoutAlpha(CapturedMaterialEvidence)`
returns Unknown-with-diagnostic on the first failed §8.1-2 gate or non-identity
`_MainTex_ScrollRotate`; otherwise the same texture-backed value shape Poiyomi's
interpretation produces (`PoiyomiMaterialSemantics.cs:740-755`): texture sample of
`_MainTex` alpha at identity UV0, times `_Color.a` (plain `Texture` term when
`_Color.a == 1`). A verified-material seam accepting `(material, colorSpace,
compiledFeatures)` mirrors `LilToonMaterialSemantics.InterpretVerifiedMaterial` so
tests can vary the feature set. `LilToonMaterialSemantics.cs` is not modified.

### Step 1 — Fixture support

Create the two schema-complete stand-in shaders following the checked-in
`LilToonSemanticTest.shader` pattern (same folder, `Hidden/Alrauna/AmuseTests/…`
names, minimal Properties blocks):
`LilToonCutoutConversionTest.shader` (cutout-shaped: the alpha-request properties
including `_Cutoff`, the gate scalars, `_Color`, `_DissolveParams`,
`_MainTex_ScrollRotate`, `_MainTex`, plus fixture-only `_IDMaskPrior8` default 0 —
required by the B2 counterexample and deliberately NOT added to
`AlphaEvidenceRequest`) and `LilToonOpaqueConversionTest.shader`
(distinct name, same schema — both expose the full 18-property conversion tuple
including `_SrcBlendFA`/`_DstBlendFA`/`_BlendOpFA`/`_BlendOpAlphaFA`, which no
existing fixture shader carries). Each asset lands together with its
Unity-generated `.meta`. Then extend `LilToonFixtureTestBase` with an importer
helper that writes explicit RGBA32 pixel grids with a configurable mip count
(wrapping the existing `ImportTexture` pattern: `mipmapEnabled = true`,
`mipMapBias` unset, streaming off) and with filter and wrap-mode configuration.
These are the only asset changes in the plan (Global Constraint 1 exception);
everything else is code.

### Step 2 — Request + interpretation scaffold (behavior-neutral)

Create `LilToonCutoutMaterialSemantics` with the spec §8.2 evidence request exactly
as specified (including `_Cutoff`) and `InterpretVerifiedCutoutAlpha` initially
structured as: gates evaluated, then `_Color`/`_MainTex` reads, then — temporarily —
`Unknown` for the texture-backed arm. Reuse `LilToonSemanticDiagnostic`/
`LilToonSemanticDiagnosticCode`/`LilToonSemanticOutput` from the lilToon namespace;
add gate-scalar arrays, the `_DissolveParams.x` exact-zero check (exact `== 0`,
stricter than the shader's `round` [B2 §10]), the `_Cutoff <= 0.9999` gate
(non-finite refuses), and the finite `_Color.a` check.

### Step 3 — RED

New `LilToonCutoutAlphaTests` (fixtures built like `LilToonFixtureTestBase`, evidence
captured via `UnityMaterialEvidenceCapture.Capture` with the cutout request; texture
fields supplied through the resolver seam with synthetic `AlphaMipChain`s exactly as
`AlphaSemanticsResolverTests` does):

1. Constant-opaque core (coverage 4): all gates off, `_Color.a = 1`, fully opaque
   chain at every mip → interpretation completes a texture-backed alpha term and
   `AlphaSemanticsResolver.Resolve` yields `ProvenOpaque` for a representative
   triangle. Falsifies: re-deriving the boundary from `a > cutoff`, or an opaque-only
   constant-1 interpretation.
2. Multiplier (coverage 5): `_Color.a = 0.8` variant → uniform
   `MustRemainTransparent`; `_Color.a = 1.2` → `UnsupportedMultiplier` refusal;
   `_Color.a` NaN / +inf / −inf → Unknown via the interpretation's finite check —
   never the resolver's uniform-transparent `< 1` fallthrough (B2 §10 names
   non-finite as unsupported-multiplier refusal, and the fallthrough would wrongly
   yield a uniform transparent *verdict*). Falsifies: ignoring `_Color.a`, and
   relying on the resolver fallthrough for non-finite values.
3. Cutoff boundary at the classification layer (coverage 6, 7): `_Cutoff = 0.9999`
   completes and resolves `ProvenOpaque` against the opaque chain; `1.0` and `1.001`
   → Unknown → slot refusal `AdmittedMaterialSemanticsUnknown` — no provable
   triangle (B2 §10), so a fully discarded domain is never classified proven;
   non-finite `_Cutoff` → Unknown. Falsifies: Poiyomi's `<= 1` rule reused at either
   layer, plain-`clip` semantics, and cutoff gated only at conversion eligibility.
4. Gate matrix (coverage 12): each of `_Invisible`, `_UDIMDiscardCompile`,
   `_UDIMDiscardMode`, `_ShiftBackfaceUV`, `_UseParallax`, `_UseMain2ndTex`,
   `_UseMain3rdTex`, `_AlphaMaskMode` (1–4), `_UseDither`, each `_IDMask1..8`,
   `_IDMaskControlsDissolve` set to its active value → Unknown with a diagnostic
   naming that property; the counterexample test asserts the fixture declares
   `_IDMaskPrior8` before setting it. Includes the B2 counterexample:
   `_IDMaskControlsDissolve = 1` with `_IDMaskPrior8 = 1` and all `_IDMask* = 0` —
   a material that renders nothing must never resolve to any `ProvenOpaque`
   triangle (coverage 13). Falsifies: gating on `ScanCompiledFeatures` alone, or
   omitting `_UDIMDiscardMode` / `_IDMaskControlsDissolve`.
5. Dissolve mode 1 (`_DissolveParams.x = 1`) refuses; `_DissolveParams.x = 0` admits.
6. `_MainTex_ScrollRotate ≠ (0,0,0,0)` → Unknown (coverage 12). Falsifies: treating
   scroll/rotate as RGB-only.
7. Texture-evidence boundaries at the resolver seam (coverage 8, 9, 10, 15): one
   transparent texel only in a high mip → `MustRemainTransparent` (fails mip-0-only
   checks); bilinear footprint dilation vs point placement; repeat seam vs clamp;
   unsupported format/filter/wrap → refusal, never `ProvenOpaque`. Then the
   imported-mip integration variant (coverage 8, end-to-end evidence layer), using
   the existing odd-boundary construction from the `UnityAlphaFieldEvidence`
   integration pattern: importer-generated lower mips are downsampled from mip 0,
   so an all-opaque mip 0 cannot generate a transparent lower mip — instead place
   transparent base-level texels **outside** the tested triangle's mip-0 footprint
   such that downsampling makes the covering higher-mip texel non-opaque, and
   assert the triangle's mip-0 support is opaque before the higher mip flips the
   verdict to `MustRemainTransparent`, through the cutout frontend's capture
   (`ScaleOffset | SourceIdentity | Sampling | AlphaChannel`) and GPU readback.
   The literal all-opaque-at-every-mip case stays at the synthetic `AlphaMipChain`
   seam, where chains are constructed directly. Falsifies: implementations that
   classify correctly on synthetic chains but infer from import settings on real
   assets.
8. Feature-variation invariance (coverage 14): through the verified-material seam,
   the same gate-off material produces the identical alpha output under compiled
   feature sets {all features}, {unrelated superset}, {minimal} — the verdict
   depends on runtime gates, never the define set (callback-100 argument's executable
   form). Falsifies: feature-set-dependent verdicts.

Run: focused `LilToonCutoutAlphaTests`. Expected: the texture-backed-arm tests FAIL
behaviorally (scaffold returns Unknown); gate-matrix tests may already pass where
the scaffold covers them — those are mutation-verified per Global Constraint 3
(drop each gate check, observe its assertion fail, restore) and recorded in the
checkpoint.

### Step 4 — GREEN

Implement the texture-backed arm mirroring Poiyomi's construction: read `_Color.a`
(finite), read `_MainTex` captured texture evidence, build the sample from source
identity, UV0 identity mapping from captured scale/offset, and the captured sampling
vocabulary; return `Texture(sample, Alpha)` when `.a == 1`, else
`TextureTimesConstant(sample, Alpha, .a)`. Add the verified-material seam
(delegating to the shared evidence capture with the cutout request).

Run: focused `LilToonCutoutAlphaTests` + `AlphaSemanticsResolverTests` +
`LilToonAlphaTests`. Expected: 0 failed.

**Self-review checkpoint.** `LilToonMaterialSemantics.cs` shows no diff; the cutout
request contains exactly the §8.2 names (no more, no fewer — a widening here is the
exact regression the capture-schema split exists to prevent); all new diagnostics use
the existing vocabulary; every immediately-green gate-matrix assertion's
mutation verification is recorded.

## Task 3: `LilToonOpaqueConversion` module (R5)

**Files:** new `Editor/Semantics/LilToon/LilToonOpaqueConversion.cs`;
new `Tests/Editor/Semantics/LilToon/LilToonOpaqueConversionTests.cs`.

**Observable contract.** Spec §9: 18-property tuple, queue 2000, `RenderType=Opaque`,
conversion request (18 + `_Cutoff`), twelve eligibility gates in order, no
`AlreadyOpaque` outcome, clone preparation with family-specific source attestation
and read-back validation (expected target identity, not source preservation),
throw-on-disagreement with destroy-first, source never written, nothing saved, clone
left unnamed.

### Step 1 — Types, tuple, request, eligibility scaffold

Create the module: `LilToonOpaqueConversionOutcome { Refused, Convertible }`;
`LilToonOpaqueConversionRefusal` with the twelve gate-refusal members of spec §9.3
plus `None`, `UnattestedMaterial`; eligibility struct mirroring
`PoiyomiOpaqueConversionEligibility`; `CanonicalOpaqueTuple` (18 entries, B1 §9
values); `CanonicalOpaqueProperties`; `CanonicalOpaqueRenderQueue = 2000`;
`RenderTypeTagName`; `CanonicalOpaqueRenderType = "Opaque"`;
`SupportedCutoutRenderQueue = 2450`; `SupportedCutoutRenderType =
"TransparentCutout"`; `MaxProvableCutoff = 0.9999f`; `ConversionSchema` (18 +
`_Cutoff`, derived from the tuple so they cannot drift);
`ConversionEvidenceRequest`; `ReadEffectiveRenderState` (queue + tag);
`TryFindNonCanonicalFact`; `EvaluateVerifiedEligibility` scaffold returning
`Refused(ConversionPropertyAbsent)` for every input after the schema check.
`PrepareCanonicalOpaqueClone(source, attestedTarget)` and the production
`PrepareCanonicalOpaqueClone(source, evidence)` wrapper implemented in full in this
step. The wrapper gathers the target's opaque source evidence and requires the full
name/GUID/package/source-digest/pass/include/render-mode attestation before cloning.

### Step 2 — RED

`LilToonOpaqueConversionTests`, evidence captured with the conversion request from
stand-in materials:

1. Tuple/request shape: `CanonicalOpaqueProperties` count 18 with the exact B1 §9
   values; `ConversionEvidenceRequest` scalars = 18 + `_Cutoff`, no colors/vectors/
   textures (mirrors `PoiyomiOpaqueConversionTests` `:83-153` style). Guards drift.
2. Eligibility matrix (coverage 6, 7 and §9.3): a canonical-default cutout material
   (`_Cutoff = 0.5`, all else default — fresh-cutout defaults are canonical except
   `_AlphaToMask`, which is ungated) → **Convertible** — RED: scaffold refuses.
   Falsifies: a gate set that refuses the default material, and any Poiyomi `<= 1`
   cutoff reuse (`_Cutoff = 0.9999` → Convertible; `1.0` → Refused; `1.001` →
   Refused; `NaN` → Refused).
3. Each gate refuses independently: effective queue `3000`; effective `RenderType`
   `"Transparent"`; `_ZTest = 3`; `_ZWrite = 0`; `_ColorMask = 14`;
   `_OffsetFactor = 1`; `_SrcBlend = 2 (DstColor)`; `_DstBlend = 5`; `_BlendOp = 1`;
   `_BlendOpAlpha = 1`; `_DstBlendAlpha = 1`; `_SrcBlendFA = 2`; `_DstBlendFA = 0`;
   `_BlendOpFA = 0`; `_BlendOpAlphaFA = 0`; non-finite any conversion scalar — each
   with its named refusal member; `_AlphaToMask = 1` and arbitrary
   `_SrcBlendAlphaFA/_DstBlendAlphaFA` do **not** refuse (ungated, spec §9.3).
4. Clone recipe read-back AND shader swap (coverage 17): the cutout fixture
   shader's material with scrambled property values, `targetShader` = the
   **distinct** opaque fixture shader reference (`opaqueFixtureMaterial.shader`;
   Task 2 Step 1's two schema-complete
   stand-ins — both carry the full 18-property tuple) →
   `PrepareCanonicalOpaqueClone(source, targetShader)` yields a clone whose shader
   reference equals `targetShader` and **differs from the source's** (the swap is
   asserted, not preservation); 18 scalars, queue, tag read back canonical;
   the source material's complete property set is unchanged (coverage 18, unit
   level); the clone is unnamed; no asset is saved. Falsifies: shader-preservation
   validation (the Poiyomi check R5 retires), in-place mutation, missing writes,
   wrong queue/tag.
5. Validation failure policy, destroy-first: (a) a fixture shader missing one recipe
   property (e.g. `_BlendOpFA`) — the write no-ops, read-back disagrees →
   `InvalidOperationException` and the clone destroyed (no leak); (b) the production
   wrapper with an unresolvable or source-unattested `Shader.Find("lilToon")` target
   throws before any clone exists. Falsifies: silently converting with a wrong,
   modified, or null target, and validation that leaks the failed clone.

Run: focused `LilToonOpaqueConversionTests`. Expected: eligibility tests FAIL
behaviorally (scaffold `Refused`); tuple/clone tests may already pass — those are
mutation-verified per Global Constraint 3 (e.g. corrupt one tuple value, observe
the shape test fail; skip one recipe write, observe the read-back test fail;
restore) and recorded in the checkpoint.

### Step 3 — GREEN

Implement `EvaluateVerifiedEligibility` gates 1–10 in the load-bearing order (schema
→ finiteness → gates). Reuse the blend-factor predicate shape of Poiyomi
(`IsUnitSourceFactorAtAlphaOne` / `IsZeroDestinationFactorAtAlphaOne` equivalents as
private helpers — copied, not referenced cross-family).

Run: focused `LilToonOpaqueConversionTests` + `PoiyomiOpaqueConversionTests`.
Expected: 0 failed (Poiyomi untouched).

**Self-review checkpoint.** No reference from the new file to any `Poiyomi*` type;
no `AlreadyOpaque` member exists; ungated properties documented with their B2 §3.1 /
§3.4 citations; diff limited to the two files; every immediately-green tuple/clone
assertion's mutation verification is recorded per the Step 2 run note.

## Task 4: Two-family wiring (R1 + R2 + R6) — two RED/GREEN phases

**Files:** `Editor/Semantics/UnityMaterialSemantics.cs`;
`Editor/Build/AlphaSeparationPreparation.cs`;
`Editor/Build/AmusePlatformFinishPlugin.cs`;
`Editor/Build/AlphaSeparationRecords.cs` (doc only);
new `Tests/Editor/Build/VerifiedLilToonTestSeams.cs` (built incrementally: request/
capture seams in Step 3, conversion seam in Step 4);
`Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`;
`Tests/Editor/Build/AlphaSeparationPreparationTests.cs`;
`Tests/Editor/Build/AlphaSeparationApplyTests.cs`;
`Tests/Editor/Build/VerifiedPoiyomiTestSeams.cs` (rename only).

**Observable contract.** Family selection admits exactly two lilToon names; the
capture schema for the cutout family is `Combine(cutoutAlphaRequest, cutoutConversionRequest)`;
ordinary opaque lilToon and Poiyomi requests are reference-identical to today;
preparation routes conversion relevance, derived admission, the overwrite rule, and
the conversion step per family; attested opaque lilToon maps to itself; production
passes both seams null. Every caller enumerated during planning is updated; no
stale references remain.

### Step 1 — Compile scaffold (behavior-neutral)

Add `CapturedAlphaMaterialFamily.LilToonCutout` to the enum and nothing else. No
production code reads the new member yet: name matching still returns
`Unsupported` for every lilToon name except `"lilToon"`, and the existing
`captured.Family != CapturedAlphaMaterialFamily.Poiyomi` branch already refuses it.
This exists only so Step 2's tests compile while failing behaviorally. Existing
`Enum.IsDefined` validation in `CapturedAlphaMaterial` accepts the member.

Run: focused `UnityMaterialSemanticsTests`. Expected: all green, unmodified
(baseline).

### Step 2 — RED (selection + analysis surface)

`UnityMaterialSemanticsTests` additions — all compile against the Step 1 scaffold
and fail behaviorally:

- A shader asset named exactly `Hidden/lilToonCutout` (temp stand-in; no attestation
  involved): `TrySelectAlphaMaterialRequests` returns true, family `LilToonCutout`,
  alpha request same-as the cutout request, capture schema the combined one. Fails
  today: selection returns false (name unselected). Falsifies: widened recognition
  (paired refusal rows: `Hidden/lilToonCutoutOutline`- and
  `Hidden/lilToonTransparent`-shaped names stay false).
- Invariance rows: opaque lilToon selection returns reference-equal
  `LilToonMaterialSemantics.AlphaEvidenceRequest` and its alpha-request-only schema;
  Poiyomi rows reference-equal as today.
- Routing (R4): a captured cutout-family material routed through
  `AnalyzeAlphaMaterial` never yields `ScalarSemanticValue.Constant(1f)` — asserted
  on a gate-active evidence (Unknown) and on a texture-backed evidence (texture
  shape). Fails family mixups.
- `CaptureAlphaMaterials` classifies the cutout name identically to
  `TrySelectAlphaMaterialRequests` (fails surviving duplication).

Run: focused `UnityMaterialSemanticsTests`. Expected: new tests FAIL behaviorally,
existing green.

### Step 3 — GREEN, phase 4a: family, capture, and analysis wiring (R2 + R4)

The Step 1 enum member gains its readers. Extract
`private static (CapturedAlphaMaterialFamily family, MaterialEvidenceRequest alpha)`
`ClassifyShaderName(string shaderName)` implementing the exact-name map
(Poiyomi name / `"lilToon"` / `"Hidden/lilToonCutout"` / else Unsupported);
`IdentifyFamily` (`:242-263`) and `CaptureAlphaMaterials` (`:114-129`) both consume
it. Extend `AlphaRequestForFamily` (cutout → cutout request), add static
`LilToonCaptureRequest = MaterialEvidenceRequest.Combine(cutoutAlphaRequest,
LilToonOpaqueConversion.ConversionEvidenceRequest)` beside `PoiyomiCaptureRequest`
(`:285-288`), extend `CaptureRequestForFamily`, `IsAttestedAlphaMaterial`
(cutout → `TryVerifyLilToonCutoutIdentity`), `BuildCapturedAlphaMaterials`
(cutout → `GatherCutoutSourceEvidence`), and `AnalyzeAlphaMaterial`
(cutout → `InterpretVerifiedCutoutAlpha`; the opaque arm untouched — R4 by
construction).

Run: focused `UnityMaterialSemanticsTests` green; full suite stays at baseline.

### Step 4 — RED (preparation, end-to-end)

Create `VerifiedLilToonTestSeams` as one family-aware composite — the capture loop
invokes the selector once per admitted material and uses its returned family
(`UnityAnimationEvidenceCapture.cs:338-348`), so a single-family selector cannot
drive the mixed and map-to-self scenarios Task 5 requires. Distinguish fixtures by
shader reference:
`SelectVerifiedLilToonFixtureRequest` returns `LilToonCutout` for the cutout
fixture shader (alpha relevance = cutout request; capture schema = combined),
`LilToon` for the opaque fixture shader (alpha relevance =
`LilToonMaterialSemantics.AlphaEvidenceRequest`; capture schema = that request
only), `Poiyomi` for the Poiyomi stand-in shader (existing Poiyomi requests), false
otherwise;
`CaptureVerifiedLilToonFixtureMaterials` (mirrors
`VerifiedPoiyomiTestSeams.CaptureVerifiedFixtureMaterials` `:86-112`) branches per
family when building each `CapturedAlphaMaterial` — Poiyomi members gather through
`PoiyomiMaterialSemantics.GatherAlphaSourceEvidence`, `LilToonCutout` members
through `GatherCutoutSourceEvidence`, `LilToon` members through
`GatherSourceEvidence` — all with vendor attestation bypassed; and
`VerifiedAlphaOnly` routes per family: `LilToonCutout` →
`LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutAlpha(captured.Evidence)`,
`LilToon` → `LilToonMaterialSemantics.InterpretVerifiedAlpha(captured.Evidence)`
(constant-1, its own premise intact), `Poiyomi` →
`PoiyomiMaterialSemantics.InterpretVerifiedAlpha` — the attestation substitution,
because no stand-in can pass vendor source attestation and the production
`AnalyzeAlphaMaterial` would return all-Unknown, refusing the slot as
`AdmittedMaterialSemanticsUnknown` before preparation. No conversion seam yet —
the end-to-end test exercises the real production conversion path. Add the
end-to-end test in `AlphaSeparationPreparationTests`
(existing `AvatarProcessor.ProcessAvatar` harness `:897-901`, passing the three
seams as `selectRequest`/`capturer`/`resolveSemantics` exactly as the Poiyomi
preparation tests do): renderer with a cutout stand-in material (fresh defaults:
`_Cutoff = 0.5`, `_AlphaToMask = 1`, gates off) and a fully-opaque mipmap texture;
assert the slot prepares and the prepared record carries the mapping. Fails
behaviorally against real production code: `ConvertAdmittedMaterial` refuses
`CapturedAlphaMaterialFamily.LilToonCutout` → `OpaqueConversionUnsupportedFamily`
refusal counters, `amuse.Separation` null. Falsifies: the missing per-family
branch, not a test seam.

Run: focused `AlphaSeparationPreparationTests`. Expected: the new test FAILS
behaviorally; existing tests green.

### Step 5 — GREEN, phase 4b: preparation routing (R1 + R6) — atomic

In `AlphaSeparationPreparation.cs`: rename the delegate (`:26-31`) to
`VerifiedPoiyomiConversion`; add `VerifiedLilToonConversion` (same shape, typed
`LilToonOpaqueConversionRefusal`); `Prepare` (`:72`) and `ConvertAdmittedMaterial`
(`:370`) take both. Add `ConversionRequestForFamily` and
`CanonicalPropertiesForFamily` per spec §6 R1. Rework the relevance loop (`:93-123`):
compute the set of conversion-capable families present among
`evidence.AdmittedMaterials`; empty → skip; else resolve every binding against the
`Combine`d union request (renderer-wide refusals unchanged), and bucket each
recognized binding by family via `ResolveProofRelevant` against that family's own
request. `ConvertAdmittedMaterial`: `case LilToon: opaque = live; return None;`
before admission; `case LilToonCutout:` admission against the lilToon request with
the family bucket, overwrite rule against `LilToonOpaqueConversion.CanonicalOpaqueProperties`
with the family bucket's property names, then seam or production sequence
(`ReadEffectiveRenderState` → `GatherCutoutSourceEvidence` + `TryVerifyLilToonCutoutIdentity`
→ `EvaluateVerifiedEligibility` → `preparedOpaque ?? PrepareCanonicalOpaqueClone(live)`);
`case Poiyomi:` byte-equivalent to today's body with the Poiyomi bucket. Update the
class doc (`:33-47`). Update `AmusePlatformFinishPlugin.cs` (`:223`, `:320`, `:693`)
and `VerifiedPoiyomiTestSeams.cs` (`:34`) for the rename; correct the
`OpaqueConversionUnsupportedFamily` doc comment in `AlphaSeparationRecords.cs:30-34`.
Add `VerifiedLilToonConversion` to the seam file (real `ReadEffectiveRenderState`,
real `EvaluateVerifiedEligibility`, real `PrepareCanonicalOpaqueClone(live, opaqueFixtureShader)`
— substituting only the source-identity check and the target-asset resolution, the
exact analog of the Poiyomi seam's documented substitution). Extend the
`AlphaSeparationPreparationTests` test-local request selector (`:854-856`) to mirror
the new production shape for `LilToonCutout`.

Run: Step 4's end-to-end test green; focused suites of Steps 2–4 green.

### Step 6 — Poiyomi routing regression + checkpoint

Run the full suite (both assemblies, with `get_test_job` polling) — **all previously
green tests must remain green, unmodified**; this is the executable R1 boundary
(controlling §14 test 8). Record the mutation-sensitivity probe as a manual note
(delete the `LilToonCutout` case locally, observe Poiyomi routing tests unaffected,
restore; never committed).

**Self-review checkpoint.** `PoiyomiMaterialSemantics.cs`/`PoiyomiOpaqueConversion.cs`
and `LilToonMaterialSemantics.cs` show no diff; grep finds zero remaining references
to `VerifiedOpaqueConversion`; `LilToonMaterialSemantics.AlphaEvidenceRequest` is
reference-identical (no new Combine on the opaque path); pure-LilToon renderers skip
the conversion loop (asserted by a unit test on the family-set computation if it is
factored as an internal pure function). Both REDs (Steps 2 and 4) were observed
failing against production code, not against broken seams.

## Task 5: Integration coverage — end-to-end NDMF scenarios

**Files:** `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`;
`Tests/Editor/Build/AlphaSeparationApplyTests.cs`;
`Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`. Test-only.

**Observable contract.** Through the verified seams (production code in admission,
relevance, planning, validation, finalization, sweep, apply), a lilToon-cutout
candidate slot prepares, converts, separates, applies, and persists exactly as the
Poiyomi slice does; mixed and sibling behaviors match spec §10.

### Step 1 — Coverage scenarios (against the Task 4-routed production code)

All drive `AvatarProcessor.ProcessAvatar` with `PreparationTestPlatform` and the
Task 4 seams. These are new-coverage tests, not REDs; each names in a comment the
plausible wrong implementation it would fail, and any that passes without ever
having been observed failing must be shown to fail against a deliberately broken
variant before its assertion is trusted:

1. Full-artifact end-to-end (coverage 21): renderer with a cutout stand-in material
   and a fully-opaque mipmap texture; assert the build result carries: the generated
   clone (canonical recipe read back; named `"... (AMUSE Opaque 0)"`), the split mesh
   with the appended submesh assigned the clone, proven triangles moved, remaining
   triangles on the source material, source material unchanged, generated material
   persistent through NDMF serialization, sweep leaving no orphans.
2. Curve rewrite + appended-slot indexing (coverage 21): a material-swap clip
   alternating the cutout source with a second admitted value; assert every
   object-reference keyframe is rewritten to the mapped result and the appended
   slot carries the clone.
3. Avatar-wide dedup: two renderers sharing one cutout source material (both
   convertible; also one variant with reversed renderer order and one variant with
   a refused sibling renderer) → exactly one clone exists, shared by reference, no
   mapping corruption.
4. Cutoff slot outcomes through the full path (coverage 6): `_Cutoff = 0.9999`
   prepares; `1.0`, `1.001`, and non-finite refuse at alpha resolution with
   `AdmittedMaterialSemanticsUnknown` — no provable triangle, nothing applied
   (spec §8.3); the conversion-stage `ClipThresholdDiscardsOpaqueAlpha` gate is
   asserted separately in Task 3's unit matrix.
5. Non-singleton animation (coverage 11): clips animating `_Cutoff`, `_Color.a`,
   `_AlphaMaskMode`, `_DissolveParams`, `_MainTex_ScrollRotate`, `_MainTex_ST`
   non-singleton each refuse — `_Cutoff` at alpha admission
   (`AnimatedMaterialPropertyNotSingleton`), the rest per their relevance layer;
   singleton-at-serialized versions prepare. Falsifies live-value reads.
6. Conversion-only recipe animation (controlling falsifier 3): `_ZWrite` animated
   away from canonical refuses `ConversionPropertyOverwrittenAtRuntime`; animated at
   canonical prepares.
7. Mixed/sibling (coverage 19): sibling slots (one Poiyomi stand-in, one lilToon
   cutout) convert independently; a mixed supported admitted set (material-swap clip
   alternating Poiyomi and cutout stand-ins) maps completely, each through its own
   conversion; an unsupported-family member (any non-allowlisted lilToon identity)
   is unselectable at family selection and refuses renderer-wide through
   material-dependency closure (`MaterialDependencyClosureFailure`) — assert that
   scope, plus a sibling renderer with only supported materials still preparing
   with its prepared mappings uncorrupted (spec §10; the slot-level formulation
   would be unreachable through this harness).
8. Attested-opaque map-to-self (coverage 2, pipeline level): a cutout slot whose
   swap set reaches a stand-in marked as the opaque lilToon family maps that value
   to itself with no clone.

### Step 2 — Verification

Run: focused three test classes (`get_test_job` polling). Expected: 0 failed;
fixture/teardown clean; console clean of errors/warnings.

### Step 3 — Fixes + checkpoint

Fix whatever the scenarios expose (expected: naming/ordering/fixture issues, not
design changes; any design-level failure stops for controller review per the spec's
stop conditions).

Run: full suite. Expected: baseline + all new, 0 failed.

**Self-review checkpoint.** No production diff in this task except fixes traced to a
failing scenario; console clean after runs; every new test names the falsified
implementation in a comment; every coverage test that never failed was
mutation-verified per Step 1's protocol and the verification is recorded.


## Task 6: Refusal hardening and structural guards

**Files:** `Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs`,
`Tests/Editor/Semantics/LilToon/LilToonOpaqueConversionTests.cs`,
`Tests/Editor/Build/AlphaSeparationPreparationTests.cs`,
`Tests/Editor/Build/AlphaSeparationPersistenceTests.cs`. Test-only.

1. End-to-end optional-path matrix (coverage 12, 13): through the seams, each active
   gate (`_UseDither = 1` even with constant-opaque texture alpha — dither replaces
   the equation; alpha-mask modes 1–4; dissolve mode 1; parallax; backface shift; UDIM
   compile and mode; IDMask flag; `_IDMaskControlsDissolve` counterexample) yields a
   refusal or a no-candidate outcome, never a proven triangle.
2. Animated `_MainTex_ST` non-identity refusal (coverage 16, animated half); identity
   ST positive already covered in Task 2 (resolver seam).
3. Unsupported-family vacuity matrix (coverage 20): stand-in shaders named for
   cutout-outline, transparent Normal/OnePass/TwoPass, Lite, Tessellation, Multi, Gem,
   Fur, Refraction, fake shadow, outline-only — each is unselectable and refuses
   renderer-wide via closure (assert per name at the selection seam; one
   end-to-end representative per refusal scope).
4. Unsupported texture evidence end-to-end (coverage 15): trilinear, mismatched wrap,
   streaming/limited-mip, non-Texture2D-shaped evidence → refusal, never opaque.
5. Structural guards (coverage 22, 18): extend `AuditedProductionFiles`
   (`AlphaSeparationPersistenceTests.cs:438+`) with
   `LilToonOpaqueConversion.cs` and `LilToonCutoutMaterialSemantics.cs` (anchor:
   type name); confirm the guard FAILS by temporarily introducing a `SaveAsset` call,
   then restore; extend the structural-digest scenario to a lilToon-cutout build
   (source mesh + source material property state unchanged across a real build).
6. Import pipeline check: runtime-created temp textures/materials and temporary
   selection shaders are deleted on teardown; the two checked-in fixture
   shader/`.meta` units remain (Global Constraint 1 exception) and show no
   incidental metadata churn.

Run: focused suites per file + `AlphaSeparationPersistenceTests`. Expected: 0 failed
after the deliberate guard-failure demonstration is restored.

**Self-review checkpoint.** Every matrix row names its refusal scope; the vacuity
matrix enumerates all twelve spec §2 refusal families (plus container identities via
name-based rows); no test inspects source text except the established structural
guards.

## Task 7: Final verification and cleanup

1. Full runs (both assemblies), console clean; compare counts against the Task 1
   baseline: baseline + new, 0 failed.
2. Task 5's end-to-end scenarios (Step 1 items 1 and 2 — full-artifact build and
   material-swap curve rewrite) **are** the repository's
   public-fixture smoke path; re-run it standalone and record the observed artifact
   (clone recipe, appended submesh material, curve rewrite, source preservation).
3. `git status --porcelain`; `git diff --stat`; `git diff --check`; separate review
   of the complete diff against the File map (7 production files; 11 test C#
   files; 2 shader/`.meta` fixture asset units — 15 test-side physical files);
   confirm the two user-owned package files are untouched by this work.
4. Confirm nothing staged: `git diff --cached` empty.
5. Cleanup: remove any scratch files; verify no placeholder/stub text remains
   (`grep -n "NotImplemented\|TODO" <changed files>` returns nothing).
6. Report per the controller's expected-report checklist. No commit, no push.

---

## Recorded future refactor pressure — not solved here

- Affine `_MainTex_ST` support (exact-arithmetic obligation,
  `AlphaSemanticsResolver.cs:327-337`) and the cutoff-margin extension [B2 §11 rows
  5–6] — the first two candidates when the next lilToon increment is commissioned.
- A third conversion family (transparent Normal is the likely next): when it arrives,
  `ConversionRequestForFamily`/`CanonicalPropertiesForFamily` become three-case
  functions and the "registry earns its first honest argument" question
  (`UnityMaterialSemantics.cs:44-52`) re-opens for controller review.
- Outline families require a separate source-and-target attestation plus an
  outline-alpha theorem [F0 §7.3]; generated optional alpha features require Outcome
  B unless independently proven [controlling §9 case 2].
- Opaque lilToon alpha request lacks `_UDIMDiscardMode` (value-side claim unaffected)
  — recorded observation, do not widen opaque capture now [B2 §11 row 8].
