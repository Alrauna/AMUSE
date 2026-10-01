# lilToonMulti full-parity support implementation plan

Date: 2026-09-30. Status: plan for review. Implementation has not
started.

Parent design: the lilToonMulti full-parity support design of this
date (`docs/superpowers/specs/2026-09-30-liltoon-multi-parity-design.md`).
Architecture decisions: the decision record of this date
(`docs/superpowers/investigations/2026-09-30-liltoon-multi-support-path.md`).

Every behavior change follows the repository RED/GREEN discipline. A
task names the plausible wrong implementation the red test must fail.
An assertion that passes on its first run is recorded as
characterization, never dressed up as red.

## Global constraints

- Unity 2022.3.22f1, one editor assembly `Alrauna.Amuse.Editor`, C#,
  file name equals type name, one public type per file, `internal`
  where possible.
- Vendor pin for every upstream fact: lilToon 2.3.4, commit
  `252fd8cfc46106d4967e95b3f2c788418502f227`, cites relative to
  `Assets/lilToon/`.
- No digest is measured from committed vendor tag bytes. Installed
  shapes only, throwaway project outside this repository.
- The three regular semantic types, their requests, and their
  attestation profiles do not change semantics. Only additive edits:
  the keyword evidence field, the Multi profiles, the hub branch.
- Test shaders stay under `Hidden/Alrauna/AmuseTests/*`. Tests create
  and delete folders under `Assets/`, so they never run in the
  Census Lab project.
- Refusals are named values in the closed Multi enum. No core
  refusal vocabulary widens.

---

## Task order

### Stage 0: pins

### Task 1: canonicalizer Multi region

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
  (region detection near the setting-run scan, `:989-1071`; no
  profile or regex vocabulary change)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToonSourceAttestationTests.cs`

- [ ] **Step 1: Red.** Fixture: two canonicalizations of a
  Multi-container-shaped source that differ only inside a
  `#define LIL_OPTIMIZE_*` block in the SubShader HLSL include
  region. Assert `ComputeNormalizedSourceHash` equal. Runs red
  against `main` at `728079c`: the region scan does not recognize
  the block, so the
  digests differ. The plausible wrong implementation is a file-wide
  removal of valueless defines, which also fails falsifier F2 of
  the 2026-09-07 spec (an unknown token elsewhere must stay hashed).
- [ ] **Step 2: Green.** Extend the setting-region detection to the
  Multi block position. Every existing regular digest test stays
  green unchanged.
- [ ] **Step 3: Run the attestation test class.** Zero tests
  reported is a failure.

### Task 2: digest measurement

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
  (two new profile constant groups, beside the existing ones)

- [ ] **Step 1:** In a throwaway Unity project outside this
  repository, install lilToon 2.3.4 twice with different real
  settings shapes (default, and one with an integration enabled).
  Measure the canonical digests of `ltsmulti.shader` and
  `ltsmulti_o.shader` through the production
  `ComputeNormalizedSourceHash`, and reproduce the existing regular
  pins in the same run to prove the extended canonicalizer did not
  disturb them.
- [ ] **Step 2:** Pin the four digest constants with a dated
  provenance comment naming both shapes, following the existing
  comment discipline. Never re-derive from the vendor repository.
- [ ] **Step 3:** The constants compile and the reproduction run is
  recorded with observed counts. The verifier tests that consume
  these constants land in Task 6, after the verify entry point
  exists.

### Task 3: clone inheritance characterization

**Files:**
- Create: `docs/superpowers/investigations/2026-09-30-multi-clone-inheritance-characterization.md`

- [ ] **Step 1:** In the throwaway project, characterize `new
  Material(shader)` for both containers: inherited queue and
  RenderType tag, inherited keyword state, and whether the
  pass-enable list copies. Dated, sanitized record, counts only.
- [ ] **Step 2:** Stop condition: if `new Material` drops the
  keyword list, the Stage A recipe already writes keywords
  explicitly, so the design holds; record the fact. If the
  pass-enable list does not copy, the `_AsOverlay` follow-up branch
  is blocked until it has a copy mechanism; record the fact. Neither
  outcome changes this slice.

### Stage A: mode 0, both containers, end to end

### Task 4: keyword evidence capture

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`
  (`MaterialEvidenceRequest`, `CapturedMaterialEvidence`, and the
  capture fill)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`

**Interfaces:**
- Produces: `MaterialEvidenceRequest` gains a `captureKeywords`
  constructor flag and `CapturedMaterialEvidence` gains
  `internal IReadOnlyList<string> Keywords` (sorted, immutable,
  empty when not requested). Every later task consumes exactly these.

- [ ] **Step 1: Red.** Fixture: a material with `UNITY_UI_CLIP_RECT`
  and `_COLOROVERLAY_ON` enabled, captured by a request with
  `captureKeywords: true`, must expose both keywords sorted; a
  request without the flag must expose an empty list. Plausible
  wrong implementation: reading `shaderKeywords` unconditionally,
  which breaks the closed-request rule, or returning unsorted order.
- [ ] **Step 2: Green.** Implement the field, the capture, and the
  immutability guard. Existing capture tests stay green.
- [ ] **Step 3:** Run the Host capture test class.

### Task 5: the mode gate

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiModeGate.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolutionRefusal.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiModeGateTests.cs`

**Interfaces:**
- Consumes: `CapturedMaterialEvidence.Keywords` (Task 4).
- Produces: `LilToonMultiResolutionRefusal` closed enum with the
  seven design values, and
  `LilToonMultiModeGate.Evaluate(evidence, mode, out refusal)`
  returning true when the captured keyword set is exactly a state
  the pinned derivation table produces for the captured scalars and
  mode, `_UseClippingCanceller == 0`, and `_AsOverlay == 0`. The
  derivation table is a static pinned array in the gate, one row per
  keyword with its producing condition, transcribed from
  `Editor/lilMaterialUtils.cs:397-468` at the pin.

- [ ] **Step 1: Red.** Falsifier fixtures: keyword set with
  `UNITY_UI_ALPHACLIP` while mode is 0 (mismatch refuses);
  `GEOM_TYPE_LEAF` present or absent (both admit); 
  `_UseClippingCanceller` 1 refuses; `_AsOverlay` 1 refuses; an
  empty keyword set at mode 0 admits. Plausible wrong
  implementation: a gate that checks only the two mode keywords, or
  one that treats the animation-derived color keywords as required.
- [ ] **Step 2: Green.** Implement the table and the rules.
- [ ] **Step 3:** Run the gate test class.

### Task 6: Multi attestation profiles and verify

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
  (verify entry points beside the existing profile verify methods,
  `:2039-2097`; no regular profile changes)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToonSourceAttestationTests.cs`

**Interfaces:**
- Consumes: the Task 2 constants.
- Produces: `TryVerifyMultiContainer(Shader, CapturedMaterialEvidence,
  out refusal)` admitting exactly the two container profiles: name,
  GUID, digest, package version 2.3.4, `_lilToonVersion` 45, shared
  include tree. The mode half of the profile is the Task 5 gate,
  not a `LIL_RENDER` scan.

- [ ] **Step 1: Red.** Falsifier: a stand-in shader with the right
  name and wrong GUID refuses; a 2.3.3 version stamp refuses;
  a mutated digest refuses; the base profile verify must not accept
  a Multi container and vice versa. Plausible wrong implementation:
  reusing the regular profile path, which fails on the missing
  `LIL_RENDER` scan.
- [ ] **Step 2: Green.** Implement the two profiles and the verify
  entry point.
- [ ] **Step 3:** Run the attestation test class.

### Task 7: resolver and hub routing

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/AlphaUnknownReason.cs`
  (one additive kind, `UnsupportedMultiState`, with its constructor
  and doc comment)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`
  (the one report sentence for the new kind; the report completeness
  tests gain it)
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
  (one enum member, one classifier branch near `:540-650`, one
  post-capture resolution call; no other switch gains an arm)
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
  (`ResolveCanonicalTargetShaderName`, `:1803-1806`: a Multi
  container resolves to itself)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`
  (the Multi near-miss entries near `:716-720` become admitted-path
  fixtures; add refusal-path fixtures)

**Interfaces:**
- Consumes: Task 4 keywords, Task 5 gate, Task 6 verify.
- Produces: `LilToonMultiResolution.Resolve(evidence, queue,
  renderType, out refusal)` returning the resolved regular family
  and the mode; capture stores the resolved family on
  `CapturedAlphaMaterial.Family` so every downstream switch sees
  only `LilToon`, `LilToonCutout`, `LilToonTransparent`. A refused
  material answers all-Unknown alpha with
  `AlphaUnknownReason.UnsupportedMultiState(words)` naming the
  refusal value, which the existing slot-report path renders
  through the new sentence.

- [ ] **Step 1: Red.** Fixtures: mode 0 on both containers resolves
  to the LilToon family; `_TransparentMode` 3 refuses with the mode
  value; `Hidden/lilToonMultiFur` refuses with the container value;
  missing keyword capture refuses; the resolver output feeds the
  regular `LilToonMaterialSemantics.InterpretVerifiedAlpha` and the
  proven-opaque result equals the same-shape regular material's
  result on identical evidence (falsifier: a resolver that trusts
  the name alone, or one that produces a different classification
  than the regular twin). The near-miss test entries flip from
  unsupported-expected to resolved-expected; the specialized
  containers keep refusing.
- [ ] **Step 2: Green.** Implement the resolver and the two hub
  edits. The full product suite runs here; this task touches the
  hub.
- [ ] **Step 3:** Run the whole `Alrauna.Amuse.Tests.Editor`
  assembly.

### Task 8: conversion recipe

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`
  (canonical clone: for a Multi container, write the explicit
  RenderType Opaque and queue 2000, and write the mode-0 keyword set
  through the keyword API)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`
  and the Build seam tests under
  `Packages/com.alrauna.amuse/Tests/Editor/Build/`

**Interfaces:**
- Consumes: Task 7 resolution; the existing 18-property canonical
  recipe, unchanged.
- Produces: clone on the source container, mode-0 keyword state,
  RenderType Opaque, queue 2000, on both containers.

- [ ] **Step 1: Red.** Fixtures: a base-container clone and an
  outline-container clone both land at Opaque/2000, not the outline
  shader default 2900 (plausible wrong implementation: inherits
  container tags); the clone's keyword set is exactly the mode-0
  set, with `_COLOROVERLAY_ON` off even when the source had it
  (plausible wrong implementation: copies source keywords); the
  clone renders mode 0 by keyword evidence, not by property reads.
- [ ] **Step 2: Green.** Implement the writes. The stage 0
  characterization record decides whether the keyword write needs a
  re-read guard.
- [ ] **Step 3:** Run the Build seam tests and the target tests.

### Task 9: Stage A checkpoint

- [ ] Full product suite green. Update the design's dated status
  line. Stop: any core-family test that changed meaning returns the
  branch to design review.

### Stage B: mode 1

### Task 10: cutout eligibility rows

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiSourceEligibility.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs`
  (mode 1 resolves to the cutout family)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiSourceEligibilityTests.cs`

**Interfaces:**
- Consumes: the regular cutout evaluator
  (`LilToonCutoutSourceEligibility.EvaluateVerifiedEligibility`)
  shape.
- Produces: `EvaluateVerifiedEligibility(evidence, queue, renderType,
  mode)` admitting the vendor Multi cutout state: blend One/Zero,
  `_AlphaToMask` 1, RenderType TransparentCutout, queue 2450
  explicit or default path; depth, offset, color mask, and cutoff
  gates reuse the cutout evaluator rows; the mode-1 keyword feature
  gates (`_COLOROVERLAY_ON`, `ETC1_EXTERNAL_ALPHA`,
  `GEOM_TYPE_BRANCH_DETAIL`) are runtime scalar gates plus Task 5
  consistency, never keyword gates alone.

- [ ] **Step 1: Red.** Fixtures, all parity oracles against the
  regular twin: a mode-1 base-container material with the same
  scalars as a supported regular cutout material admits to the same
  proven-opaque set; a mode-1 outline-container material whose
  outline alpha state is not proven constant refuses exactly as its
  regular `Hidden/lilToonCutoutOutline` twin refuses, through the
  same T2 outline gate rows (`_OutlineColor.a`, the outline texture
  alpha domain, `_OutlineWidth`, `_OutlineDeleteMesh`,
  `_OutlineDisableInVR`, `_OutlineZTest`, `_OutlineZWrite`,
  `_OutlineCull`, `_OutlineColorMask`); a gate copied from the
  regular family that treats the `_COLOROVERLAY_ON` keyword itself
  as proof of an enabled alpha mask, instead of the `_AlphaMaskMode`
  scalar, fails the disabled-mask fixture; a gate that accepts queue
  2000 in mode 1 refuses (the vendor writes 2450). The cutoff and
  `MaxProvableCutoff` premises carry over: a cutoff above the bound
  refuses with the cutout vocabulary.
- [ ] **Step 2: Green.** Implement the rows.
- [ ] **Step 3:** Run the new class plus the cutout family tests.

### Stage C: mode 2

### Task 11: transparent eligibility rows

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiSourceEligibility.cs`
  (mode 2 rows)
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs`
  (mode 2 resolves to the transparent family)
- Test: same class as Task 10.

- [ ] **Step 1: Red.** Falsifier: a gate that refuses the missing
  `FORWARD_BACK`/`_PreCutoff` state fails the Normal-class fixture;
  a gate that skips the distance-fade gate when `_FADING_ON` is
  present fails the fixture where `_DistanceFade.z` is non-zero
  (the keyword compiles the alpha-writing block at `LIL_RENDER 2`);
  queue rows admit 2460 and the worlds 3000 override, refuse
  others.
- [ ] **Step 2: Green.** Implement the rows.
- [ ] **Step 3:** Run the eligibility class plus the transparent
  family tests.

### Task 12: final validation

- [ ] Full product suite green. Research suite green. Identifier
  sweep on every file the branch touched. The product owner re-runs
  the Lab characterization as a regression check; expected result:
  the 136 mode-0 materials classify exactly as before, no new
  refusals, no previously-admitted material changed outcome.
- [ ] Update the design's status line with the date and the observed
  counts.

## Stop conditions

1. Stage 0 pins a derivation-table row that contradicts a gate
   premise in the design. Stop, dated design addendum first.
2. The digest measurement finds a varying region the canonicalizer
   cannot cover with a closed vocabulary. Stop; the version refuses
   until covered.
3. The hub change grows beyond one enum member, one classifier
   branch, and one resolution point. Stop and return to design.
4. Any new refusal needs to widen beyond its named value's scope.
   Stop and return to design.
5. A regular-family digest pin stops reproducing in any Task 2 run.
   Stop; the canonicalizer change is wrong.

## Git and review boundaries

1. Branch: `design/liltoon-multi-support`, base `main` at
   `728079c`. One pull request after Task 12 validation.
2. Per-task commits are authorized within this branch during the
   execution phase. No push, merge, or rebase of shared history
   without separate authorization.
3. Every task's commit message names the falsifier it makes green.
