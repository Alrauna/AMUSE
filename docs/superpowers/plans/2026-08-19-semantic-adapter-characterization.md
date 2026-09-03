# Semantic Adapter Characterization Implementation Plan

> **For the agent that runs this plan:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` by default. Use `superpowers:subagent-driven-development` only if the user separately authorizes subagents. Track execution with the checkboxes below.

**Goal:** Turn the Poiyomi↔lilToon comparison into durable knowledge and targeted regression coverage. Publish the behavior/support matrix outside a per-adapter spec. Record, as tests, the five invariants that only the comparison of the two frontends revealed. Resolve the one candidate correctness defect that the comparison found.

**Architecture:** Test-only and documentation-only, with one conditional production fix. No new abstraction, no new shared class, and no change to `MaterialSemantics`, `UnityTextureEvidence`, `LilToonSourceAttestation`, or any equation. New tests live in a `Characterization` folder. They reuse the existing fixture bases and the existing `InterpretVerifiedMaterial` seams unchanged.

**Tech Stack:** Unity 2022.3, C#, NUnit EditMode tests, and the existing `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor` assemblies. No new dependency, assembly, asmdef, or package metadata change.

## Global constraints

- The approved specification is `docs/superpowers/specs/2026-08-19-semantic-adapter-characterization-design.md`. Read it before Task 1. Read its two corrections to the lilToon design document, and its two fired stop conditions.
- Execute only after explicit design/plan approval, on `test/semantic-adapter-characterization` based on `9c37f22`.
- Use red/green TDD. Observe every focused red **for the intended reason** before you write production code. Then observe that same scope green. Expect Task 7 to go red in Poiyomi on the first run. That red is the deliverable, not a failure to work around.
- **Do not create:**
  - `IShaderAdapter`
  - an adapter registry or factory
  - `ShaderSchema`
  - serialized shader profiles
  - YAML/JSON shader definitions
  - a generic shader interpreter
  - an expression DAG
  - a feature graph
  - an HLSL parser
  - a shader transpiler
  - shader-portability code
  - feature transplantation
  - NDMF integration
  - animation or state tracing
  - atlasing
  - material combining
  - optimizer-planner changes
  - a shared adapter base class
  - a shared diagnostic framework
  - a property-based or combinatorial test harness
  - shader adapter #3
- **Do not modify:**
  - `MaterialSemantics.cs`
  - `UnityTextureEvidence.cs`
  - `LilToonSourceAttestation.cs`
  - `LilToonMaterialSemantics.cs`
  - any gate list
  - any equation
  - the reference fixtures
  - either stand-in fixture shader
  - asmdefs
  - `AssemblyInfo.cs`
  - package metadata
  - manifests or locks
  - workflows
  - project settings

  The one production exception is Task 8. Task 8 is conditional, and it reorders statements within exactly one method in `PoiyomiMaterialSemantics.cs`.
- **Do not weaken, delete, skip, rename, or rewrite any pre-existing test case or expectation.** Every test that exists at `9c37f22` must still exist, unaltered, and pass at the end of the plan. There is no exception to this constraint anywhere in the plan.
- Task 6 authorizes **adding** one new test method to an existing test file (`LilToonAdversarialTests.cs`). That addition is the only permitted edit to any existing test file. The pre-existing methods, their names, their fixtures, and their assertions are untouched.
- Never widen a claim under uncertainty. Every new assertion checks that an unproven case is `Unknown`, never that it becomes `Complete`.
- Treat each Unity asset and its `.meta` file as one unit. New `.cs` files and the new folder get their `.meta` from the Unity import. Inspect every new GUID. Do not hand-write, copy, or delete `.meta` files.
- Do not commit, push, open a PR, tag, publish, or change repository settings. Those require separate authorization. The plan ends at a review handoff.
- **Testbed policy.** The only Unity Editor reachable during design research was the **private avatar testbed** (78 packages including the VRChat SDK, VRCFury, and `jp.lilxyzw.liltoon 2.3.4`, with `com.alrauna.amuse` resolved as `Local`). This plan requires the **public development project's** Unity Editor, because the EditMode tests create and delete temporary asset folders under `Assets/`. Confirm the project root of the connected instance before you run any test. If only the private testbed is reachable, **stop and report**. Do not run the suite there.

---

## Planned files

**Create:**

- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization.meta`
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization/SamplerBlastRadiusTests.cs` (+ `.meta`)
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization/UncertaintyMonotonicityTests.cs` (+ `.meta`)
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization/SharedEvidenceAgreementTests.cs` (+ `.meta`)
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization/IrrelevantChangeInvarianceTests.cs` (+ `.meta`)
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization/NeutralClaimGatingTests.cs` (+ `.meta`)
- `docs/architecture/shader-frontend-comparison.md`

**Modify:**

- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAdversarialTests.cs` (Task 6 — **one added test method only**. No pre-existing method altered.)
- `docs/architecture/vision.md` (Task 2 — one added pointer line under "Semantic understanding". No other edit.)
- `docs/superpowers/specs/2026-08-19-semantic-adapter-characterization-design.md` (Task 1 verification subsection. Task 10 stop-condition outcomes.)
- `docs/superpowers/plans/2026-08-19-semantic-adapter-characterization.md` (execution checkboxes and observed-result notes)
- `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` (**Task 8 only**, conditional)

**Expected final working-tree scope.** At the review handoff, the working tree should contain exactly the six created test/doc paths and their `.meta` files, plus the five modified paths above. `PoiyomiMaterialSemantics.cs` is present only if Task 8 ran. Both milestone documents are expected to differ from their `9c37f22` state, because Tasks 1 and 10 update them. Anything else that changed is a finding to report, not something to silently restore.

### Test-class placement convention

Per-adapter characterization concerns get **two classes**, one per fixture base. For example: `PoiyomiSamplerBlastRadiusTests : PoiyomiFixtureTestBase` and `LilToonSamplerBlastRadiusTests : LilToonFixtureTestBase`, both in the same file. **Do not introduce a shared test base to unify them.** The duplication is deliberate. A shared base is the abstraction that this milestone exists to resist.

`SharedEvidenceAgreementTests` (Task 5) is the one class that needs both frontends. It inherits `PoiyomiFixtureTestBase` for the texture helpers and the temp-folder lifecycle. It reaches lilToon through `Shader.Find(...)` plus the static `LilToonMaterialSemantics.InterpretVerifiedMaterial` seam. No new base class.

---

## Task 1 — Resolve the Poiyomi neutral-claim gating question

**Blocking research. No code. This task gates Tasks 7 and 8.**

> **COMPLETE 2026-08-19 — DEFECT CONFIRMED.** Source provenance is verified independently (GUID and normalized hash both reproduce the pins of AMUSE). `_DetailEnabled` is a confirmed independent writer. The other seven gates are refuted. Full per-gate evidence is in the "Task 1 verification" section of the design doc. Task 7 proceeds, expecting RED at Poiyomi `Normal`. Task 8 runs, reordering only.

**Architectural question:** *Does `PoiyomiMaterialSemantics.InterpretNormal` produce a false positive when a normal-writer feature is enabled and `_BumpMap` is unassigned?*

- [x] Re-read the "Finding: the neutral-claim gating asymmetry" section of the design doc.
- [x] Confirm that the asymmetry still holds at HEAD. `PoiyomiMaterialSemantics.cs:754` returns `Complete(Unmodified())` as the first statement of `InterpretNormal`, before `NormalFeatureGates`. `LilToonMaterialSemantics.cs:823` evaluates `NormalWriterGates` before the neutral return at `:838`.
- [x] Get the pinned Poiyomi Toon Shader 9.3.64 source (tag commit `e125e1c33cbfb860f59330799dd4d10a1097242d`). It is not installed in this repository, and it was not present as a package in the reachable testbed. Reading upstream source for verification is fine. **Do not copy any of it into this repository.**
- [x] For each name in `NormalFeatureGates`, determine whether it can perturb the tangent-space normal **when `_BumpMap` is unassigned**. The names: `_DetailEnabled`, `_RGBMaskEnabled`, `_DecalEnabled`, `_DecalEnabled1`, `_DecalEnabled2`, `_DecalEnabled3`, `_PoiInternalParallax`, `_PoiParallax`. `_DetailEnabled` with a detail normal map is the leading candidate.
- [x] Record the finding, per gate, in the design doc under a new "Task 1 verification" subsection.

**Outcomes:**

- **Confirmed for at least one gate** → the defect is real. Proceed to Task 7 expecting red, then Task 8.
- **Refuted for every gate** → the ordering of Poiyomi is sound for its shader. Task 7 still runs, because it documents the behavior. Task 8 is skipped. The design doc records *why* the two shaders legitimately differ. That record is itself a valuable result.
- **Source not available** → **stop and report.** Do not guess, and do not "fix" the ordering defensively. Changing `InterpretNormal` without evidence would be a behavior change made on speculation. That is the mirror image of the defect under investigation.

---

## Task 2 — Publish the durable behavior/support matrix

**Architectural question:** *What have the two frontends actually taught us, in a form that survives adapter #3?*

- [x] Create `docs/architecture/shader-frontend-comparison.md`.
- [x] Carry over from the design doc, as the durable record:
  - the behavior/support matrix
  - the A–G concept classification
  - the repeated-pressure table
  - the "what should remain duplicated" table, with its triggers
  - the future-abstraction-candidates table, with the evidence each candidate still lacks
- [x] State explicitly that the two corrections to `2026-08-17-liltoon-semantics-adapter-design.md` supersede the claims of that document about `TryReadBinary` and about the uniformity of category D.
- [x] Add a one-line pointer from `docs/architecture/vision.md` under "Semantic understanding". Do not otherwise edit `vision.md`.
- [x] Do **not** copy the milestone-process sections (stop conditions, deferred work, task ordering). Those belong to the spec.

No test. Documentation task.

> **COMPLETE 2026-08-19.** Created `docs/architecture/shader-frontend-comparison.md` and added the single pointer line to `docs/architecture/vision.md`. The Task 1 per-gate finding and the neutral-claim gating rule are recorded there as durable architecture guidance.

---

## EXECUTION LOG

**Halted once** at the Task 2 -> Task 3 gate. Only the private avatar testbed was reachable. **Resumed and completed** the same day, once the public AMUSE development Editor was opened. The resume gate was satisfied by explicit instance selection of the public development instance and by verification of `Application.dataPath = <repo-root>/Assets`, product `AMUSE`, Unity 2022.3.22f1. No VRChat SDK, VRCFury, or lilToon package was present. The private testbed was never used to run tests or create assets.

| Stage | Observed |
| --- | --- |
| Baseline, before any change | 553 passed / 0 failed |
| Task 3 (sampler blast radius) | 4 passed |
| Task 4 (uncertainty monotonicity) | 13 passed |
| Task 5 (shared-evidence agreement) | 6 passed |
| Task 6 (lilToon public-entry parity) | 13 passed (12 pre-existing + 1 added) |
| Task 7 RED, before fix | 44 run, 36 passed, **8 failed**, all at Poiyomi Normal |
| Task 8 fix, Task 7 re-run | 44 passed / 0 failed |
| Task 9 (irrelevant-change invariance) | 8 passed |
| Task 10 final full suite | **629 passed / 0 failed** |

Unity console: 0 errors, 0 warnings. Nothing staged, committed, or pushed.

---

## Task 3 — Shared-evidence blast radius (T3)

**Architectural question:** *Which outputs does the import state of one texture invalidate, per adapter?* The two answers differ, and neither is currently pinned.

- [x] Create `Characterization/SamplerBlastRadiusTests.cs` with `PoiyomiSamplerBlastRadiusTests : PoiyomiFixtureTestBase` and `LilToonSamplerBlastRadiusTests : LilToonFixtureTestBase`.
- [x] **Red:** For each adapter, build a canonical material with `_MainTex`, the normal map, and the emission map all assigned, and with every output `Complete`. First assert that the baseline is fully `Complete`. A test that starts from an accidentally-`Unknown` baseline proves nothing.
- [x] **Red:** Re-import `_MainTex` with mipmaps enabled, so that `UnityTextureEvidence.TryGetSampling` refuses. Then assert per adapter:
  - **Poiyomi** — `BaseColor`, `Alpha`, `Emission`, and `Normal` all become `Unknown`. Each carries an `UnsupportedSampling` diagnostic that names `_MainTex`. Every sample routes through `TryGetMainTextureSampling`.
  - **lilToon** — `BaseColor` and `Normal` become `Unknown` with `UnsupportedSampling`. **`Emission` stays `Complete`**, because it uses `sampler_EmissionMap`. **`Alpha` stays `Complete`**, because it is attested from `LIL_RENDER`, not sampled.
- [x] **Green:** Both classes pass against unmodified production code. If either class does not, the production behavior differs from the reading in the design doc. **Stop and report.** Do not adjust the assertion to match.
- [x] Confirm `UnsupportedSharedMainSampler_InvalidatesEverySample_ConstantSurvives` still passes unmodified.

The lilToon half is the valuable half. It is the first test that asserts an output **survives** an evidence failure that invalidates its siblings.

---

## Task 4 — Uncertainty monotonicity (T2)

**Architectural question:** *Can removing evidence ever produce a more informative claim?* Nothing in the repository tests this today.

- [x] Create `Characterization/UncertaintyMonotonicityTests.cs` with one class per adapter.
- [x] **Red:** Start from a fully-`Complete` baseline. Apply one evidence-removal mutation at a time. Assert for **every** output: the value is either structurally equal to the baseline, or `Unknown`. Never a different `Complete` value. Never `Unknown → Complete`. Use the structural equality of `MaterialSemantics` directly.
- [x] Mutation list — explicit and hand-written, **not generated**:
  - replace an imported texture with a native `.asset` texture (no `TextureImporter`)
  - enable mipmaps on an assigned texture
  - flip the green channel of the normal map
  - import the emission map with a source alpha channel
  - **lilToon only:** remove each `LIL_FEATURE_*` symbol individually from the compiled set
  - **lilToon only:** pass the empty feature set
- [x] **Green:** Both classes pass unmodified. The lilToon empty-feature-set case is the sharpest. It must not turn any `Unknown` into `Complete`. `Alpha` must stay `Complete`, because it depends on no feature symbol.
- [x] Confirm `StrippedFeature_KeepsEmissionUnknown` and `StrippedFeature_KeepsNormalUnknown` still pass unmodified. This task generalizes them. It does not replace them.

**Do not** build a generator, a shrinker, or a property-based harness. Iterate an explicit list with `TestCaseSource`.

---

## Task 5 — Cross-adapter shared-evidence agreement (T5)

**Architectural question:** *Is `UnityTextureEvidence` genuinely one contract, or two coincidences that happen to match?*

- [x] Create `Characterization/SharedEvidenceAgreementTests.cs` as a single class that inherits `PoiyomiFixtureTestBase`. Reach lilToon via `Shader.Find` plus `LilToonMaterialSemantics.InterpretVerifiedMaterial`. **No new base class.**
- [x] **Red:** For each of the five shared facts, construct the texture state that makes it refuse. Feed that state through the slot in **each** adapter that consumes it:

  | Fact | Refusing state | Poiyomi slot | lilToon slot |
  | --- | --- | --- | --- |
  | `TryGetSourceId` | scene-only `Texture2D` | `_MainTex` | `_MainTex` |
  | `TryGetSampling` | mipmaps enabled | `_MainTex` | `_MainTex` |
  | `TryGetColorInterpretation` | native `.asset`, no importer | `_MainTex` | `_MainTex` |
  | `TryProveSampledAlphaIsOne` | source with alpha | `_EmissionMap` | `_EmissionMap` |
  | `IsCanonicalNormalMapImport` | green channel flipped | `_BumpMap` | `_BumpMap` |

- [x] Assert that **both** adapters refuse, each with its own diagnostic code. Do **not** assert that the two codes are equal. They legitimately are not. An equality assertion would be the first step toward a shared diagnostic framework, and this milestone forbids that framework.
- [x] **Green:** passes unmodified.
- [x] Confirm `SharedClass_ExposesExactlyFiveSemanticFacts` still passes unmodified.

This is the only test that crosses the frontend boundary. It exists to detect one specific failure. A future edit makes one frontend stop depending on a shared fact. `UnityTextureEvidence` is then left with one real consumer and a stale justification.

---

## Task 6 — lilToon unattested-entry refusal parity (T6)

**Architectural question:** *Can interpretation be reached without attestation?* Poiyomi proves it cannot. The nearest lilToon test fails at check 1 only, and proves much less.

- [x] **Red:** Add one test to the existing `LilToonAdversarialTests.cs`, mirroring `PublicEntry_UnattestedSchemaCompleteShader_IsRefusedBeforeInterpretation`. Use the stand-in fixture material. Its shader name and `_lilToonVersion` are already correct, but its GUID and digests are not. Call the **public** `AnalyzeBaseMaterial` entry, not the verified seam.
- [x] Assert these facts. `IsSupportedMaterial == false`. All four outputs are `Unknown`. There is **exactly one** diagnostic, scoped to `LilToonSemanticOutput.Material`. Exactly one is the load-bearing assertion. It proves that no output interpreter ran.
- [x] **Green:** passes unmodified.
- [x] Add no new file. Do not modify any existing lilToon test.

---

## Task 7 — Neutral-claim gating parity (T1)

**Depends on Task 1. Highest-value test in the milestone.**

**Architectural question:** *Is a neutral or zero claim ever made without proving the independent writers off?* Seven of eight sites gate first. One does not.

- [x] Create `Characterization/NeutralClaimGatingTests.cs` with one class per adapter.
- [x] **Red:** Write the test table-driven, over an **explicit test-local copy** of each reviewed gate-name list. Do **not** change the visibility of the production arrays. Do **not** add an accessor for them. Do **not** reach them by reflection merely to consume them. A test-local literal is the reviewed input. A test that reads the production list would pass vacuously if that list were emptied. For each output that can short-circuit to a neutral or zero claim: enable one writer gate, leave the texture of the slot unassigned, and assert the output is **not** `Complete`.
- [x] Expected first run:
  - **lilToon** — green at all four outputs. `EnabledNormalWriter_WithBumpMapDisabled_IsUnknown` and `EnabledSecondNormal_WithNoFirstTexture_IsUnknown` already cover it. This generalizes the property to every output.
  - **Poiyomi** — green at `BaseColor`, `Alpha`, and `Emission`. **Red at `Normal`.**
- [x] Record the observed red output verbatim in the execution notes. Do not proceed to Task 8 without it.
- [x] **If Task 1 refuted the defect:** convert the Poiyomi `Normal` case to an explicit documented-behavior assertion. The assertion states that an unassigned `_BumpMap` yields `Unmodified`, regardless of writer gates. Add a comment that cites the per-gate finding of Task 1 as the justification. Do **not** silently drop the case.

---

## Task 8 — Conditional fix: gate before the Poiyomi neutral normal claim

**Execute only if Task 1 confirmed the defect for at least one gate. Otherwise skip and record why.**

- [x] Confirm that the Poiyomi `Normal` red of Task 7 is observed and captured.
- [x] In `PoiyomiMaterialSemantics.InterpretNormal`, move the `NormalFeatureGates` check **above** the unassigned-`_BumpMap` short-circuit at `:754`. This matches the lilToon ordering at `:823`. Add a comment that states the invariant and cites this milestone.
- [x] Change nothing else in the method. The `_BumpScale`, UV, stochastic, sampler, identity, and import checks keep their current order and behavior.
- [x] **Green:** Task 7 passes.
- [x] Re-run the **complete** EditMode suite. `MissingBumpMap_IsUnmodified` uses a default material with every writer already off. It must still pass unmodified. If any existing test now fails, that test encoded the defect. **Stop and report.** Do not edit it without a separate decision.
- [x] Confirm that the fix is strictly conservative. It can only turn `Complete` into `Unknown`, never the reverse. This is the required direction under the AGENTS.md safety invariant.

---

## Task 9 — Irrelevant-change structural invariance (T4)

**Architectural question:** *Does irrelevant material state leak into semantic values?* One ad-hoc instance exists. The property is not stated generally.

- [x] Create `Characterization/IrrelevantChangeInvarianceTests.cs` with one class per adapter.
- [x] **Red:** Start from a canonical fully-`Complete` material. Mutate one property at a time. Take the properties from a **short, hand-picked, explicit** list. No gate list and no equation reads any property on that list. Assert that the whole `MaterialSemantics` compares **equal** to the baseline.
- [x] Choose the list by reading the gate arrays and equations, not by enumerating the fixture shader. Keep it under roughly a dozen entries per adapter. A generated sweep over every shader property is the combinatorial infrastructure that this milestone declines to build.
- [x] If a mutation **does** change the output, resolve it before you proceed. Either the property is genuinely read — remove it from the list and note why — or a gate list reads something it should not. The second case is a finding. **Stop and report.**
- [x] Confirm `RenderStateProperties_DoNotChangeAlpha` still passes unmodified. This task generalizes it.

Placed after Tasks 7–8, because its baseline must be the post-fix behavior.

---

## Task 10 — Architecture checkpoint and handoff

- [x] Run the **complete** EditMode suite and observe the result. Record the counts.
- [x] Verify that no forbidden artifact was created:
  - no `IShaderAdapter`
  - no registry
  - no shared adapter base
  - no shared diagnostic type
  - no schema
  - no property-based harness
  - no change to `MaterialSemantics`, `UnityTextureEvidence`, or `LilToonSourceAttestation`
- [x] Inspect the working-tree diff and the staged diff **separately**. Confirm that only the planned files changed. Confirm that `Packages/manifest.json`, `Packages/packages-lock.json`, `Packages/vpm-manifest.json`, `ProjectSettings/`, and `.github/workflows/` are untouched.
- [x] Inspect every new `.meta` GUID. Confirm that the GUID of no existing asset changed, and that no reference broke.
- [x] Update `docs/architecture/shader-frontend-comparison.md` with what execution actually established. Record in particular the per-gate finding of Task 1, and whether Task 8 ran.
- [x] Update the stop-condition table of the design doc with the observed outcomes.
- [x] Report:
  - what changed
  - what validation ran, and its observed result
  - what was skipped, and why
  - remaining risks
  - whether the private testbed was used, and whether it was modified
- [x] **Stop at review handoff.** Do not commit, push, or open a PR.

---

## Explicitly not planned

Each item was considered against the evidence of the design, and rejected. Listed so a later reader can see that the omissions were decisions.

| Not planned | Why |
| --- | --- |
| Extracting `RequireAnalyzableMaterial`, `FirstFailedZeroGate`, `TryReadBinary`, `AllUnknown` | Byte-identical and free of shader knowledge, so eligible on contract. But extraction on two producers creates the shared-utility surface that this milestone resists. The trigger is adapter #3, in one pass. |
| Extracting `ComputeNormalizedSourceHash` | Same rule, two producers, and zero consumers of a shared version. Extract it with the attestation-primitive cluster, or not at all. |
| Removing the four pure-delegation wrappers of Poiyomi | Real dead indirection, but unrelated to the question of this milestone. Separate task or branch. |
| Reconciling the assertion helpers of the two fixture bases | A genuine gap — each adapter is missing the check of the other. But this is test-infrastructure cleanup, not characterization. Deferred, and recorded in the design. |
| A differential-rendering proof-of-concept | Requires a reference evaluator, which is production code. The stop condition fired. The recommendation is to defer, with three named preconditions. |
| A compile + EditMode CI gate | The largest validation gap found. `.github/workflows/` has only `build-listing.yml` and `release.yml`. No test workflow exists. This is out of the scope of this milestone, and it deserves its own branch. Prioritize it **before** adapter #3. |
| Any change to `MaterialSemantics` for `rgb × a` or coverage semantics | Two independent producers each, and **zero consumers**. Documented as pressure. |
| Shader adapter #3 | Explicitly out of scope. The design records what to learn first. |
