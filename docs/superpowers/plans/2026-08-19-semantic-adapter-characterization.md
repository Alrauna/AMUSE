# Semantic Adapter Characterization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` by default. Use `superpowers:subagent-driven-development` only if the user separately authorizes subagents. Track execution with the checkboxes below.

**Goal:** Convert the Poiyomi↔lilToon comparison into durable knowledge plus targeted regression coverage: publish the behavior/support matrix outside a per-adapter spec, codify five invariants the two frontends only revealed by comparison, and resolve the one candidate correctness defect the comparison exposed.

**Architecture:** Test-only and documentation-only, with one conditional production fix. No new abstraction, no new shared class, no change to `MaterialSemantics`, `UnityTextureEvidence`, `LilToonSourceAttestation`, or any equation. New tests live in a `Characterization` folder and reuse the existing fixture bases and the existing `InterpretVerifiedMaterial` seams unchanged.

**Tech Stack:** Unity 2022.3, C#, NUnit EditMode tests, existing `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor` assemblies. No new dependency, assembly, asmdef, or package metadata change.

## Global constraints

- The approved specification is `docs/superpowers/specs/2026-08-19-semantic-adapter-characterization-design.md`. Read it before Task 1, including its two corrections to the lilToon design document and its two fired stop conditions.
- Execute only after explicit design/plan approval, on `test/semantic-adapter-characterization` based on `9c37f22`.
- Use red/green TDD. Observe every focused red **for the intended reason** before writing production code, then observe that same scope green. Task 7 is expected to go red in Poiyomi on first run; that red is the deliverable, not a failure to work around.
- **Do not create:** `IShaderAdapter`, an adapter registry or factory, `ShaderSchema`, serialized shader profiles, YAML/JSON shader definitions, a generic shader interpreter, an expression DAG, a feature graph, an HLSL parser, a shader transpiler, shader-portability code, feature transplantation, NDMF integration, animation or state tracing, atlasing, material combining, optimization-planner changes, a shared adapter base class, a shared diagnostic framework, a property-based or combinatorial test harness, or shader adapter #3.
- **Do not modify:** `MaterialSemantics.cs`, `UnityTextureEvidence.cs`, `LilToonSourceAttestation.cs`, `LilToonMaterialSemantics.cs`, any gate list, any equation, the reference fixtures, either stand-in fixture shader, asmdefs, `AssemblyInfo.cs`, package metadata, manifests or locks, workflows, or project settings. The one production exception is Task 8, which is conditional and reorders statements within exactly one method in `PoiyomiMaterialSemantics.cs`.
- **Do not weaken, delete, skip, rename, or rewrite any pre-existing test case or expectation.** Every test that exists at `9c37f22` must still exist, unaltered, and pass at the end of the plan. There is no exception to this constraint anywhere in the plan.
- Task 6 authorizes **adding** one new test method to an existing test file (`LilToonAdversarialTests.cs`). Adding a method to that file is the only permitted edit to any existing test file; the pre-existing methods, their names, their fixtures, and their assertions are untouched.
- Never widen a claim under uncertainty. Every new assertion checks that an unproven case is `Unknown`, never that it becomes `Complete`.
- Treat each Unity asset and its `.meta` file as one unit. New `.cs` files and the new folder must get their `.meta` from Unity import; inspect every new GUID; do not hand-write, copy, or delete `.meta` files.
- Do not commit, push, open a PR, tag, publish, or change repository settings. Those require separate authorization; the plan ends at a review handoff.
- **Testbed policy.** The only Unity Editor reachable during design research was the **private avatar testbed** (78 packages including the VRChat SDK, VRCFury, and `jp.lilxyzw.liltoon 2.3.4`, with `com.alrauna.amuse` resolved as `Local`). Executing this plan requires the **public development project's** Unity Editor, because the EditMode tests create and delete temporary asset folders under `Assets/`. Confirm the connected instance's project root before running any test. If only the private testbed is reachable, **stop and report** rather than running the suite there.

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

- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAdversarialTests.cs` (Task 6 — **one added test method only**; no pre-existing method altered)
- `docs/architecture/vision.md` (Task 2 — one added pointer line under "Semantic understanding"; no other edit)
- `docs/superpowers/specs/2026-08-19-semantic-adapter-characterization-design.md` (Task 1 verification subsection; Task 10 stop-condition outcomes)
- `docs/superpowers/plans/2026-08-19-semantic-adapter-characterization.md` (execution checkboxes and observed-result notes)
- `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` (**Task 8 only, conditional**)

**Expected final working-tree scope.** At the review handoff the working tree should
contain exactly: the six created test/doc paths and their `.meta` files, plus the five
modified paths above (with `PoiyomiMaterialSemantics.cs` present only if Task 8 ran).
Both milestone documents are expected to differ from their `9c37f22` state, because
Tasks 1 and 10 update them. Anything else that changed is a finding to report, not to
silently restore.

### Test-class placement convention

Per-adapter characterization concerns get **two classes**, one per fixture base — for
example `PoiyomiSamplerBlastRadiusTests : PoiyomiFixtureTestBase` and
`LilToonSamplerBlastRadiusTests : LilToonFixtureTestBase`, both in the same file.
**Do not introduce a shared test base to unify them.** The duplication is deliberate;
a shared base is the abstraction this milestone exists to resist.

`SharedEvidenceAgreementTests` (Task 5) is the one class needing both frontends. It
inherits `PoiyomiFixtureTestBase` for the texture helpers and temp-folder lifecycle,
and reaches lilToon through `Shader.Find(...)` plus the static
`LilToonMaterialSemantics.InterpretVerifiedMaterial` seam. No new base class.

---

## Task 1 — Resolve the Poiyomi neutral-claim gating question

**Blocking research. No code. This task gates Tasks 7 and 8.**

> **COMPLETE 2026-08-19 — DEFECT CONFIRMED.** Source provenance independently verified
> (GUID and normalized hash both reproduce AMUSE's pins). `_DetailEnabled` is a
> confirmed independent writer; the other seven gates are refuted. Full per-gate
> evidence is in the design doc's "Task 1 verification" section. Task 7 proceeds
> expecting RED at Poiyomi `Normal`; Task 8 runs, reordering only.

**Architectural question:** *Does `PoiyomiMaterialSemantics.InterpretNormal` produce a
false positive when a normal-writer feature is enabled and `_BumpMap` is unassigned?*

- [x] Re-read the design doc's "Finding: the neutral-claim gating asymmetry" section.
- [x] Confirm the asymmetry still holds at HEAD: `PoiyomiMaterialSemantics.cs:754` returns `Complete(Unmodified())` as `InterpretNormal`'s first statement, before `NormalFeatureGates`; `LilToonMaterialSemantics.cs:823` evaluates `NormalWriterGates` before the neutral return at `:838`.
- [x] Obtain the pinned Poiyomi Toon Shader 9.3.64 source (tag commit `e125e1c33cbfb860f59330799dd4d10a1097242d`). It is not installed in this repository and was not present as a package in the reachable testbed. Reading upstream source for verification is fine; **do not copy any of it into this repository.**
- [x] For each name in `NormalFeatureGates` — `_DetailEnabled`, `_RGBMaskEnabled`, `_DecalEnabled`, `_DecalEnabled1`, `_DecalEnabled2`, `_DecalEnabled3`, `_PoiInternalParallax`, `_PoiParallax` — determine whether it can perturb the tangent-space normal **when `_BumpMap` is unassigned**. `_DetailEnabled` with a detail normal map is the leading candidate.
- [x] Record the finding, per gate, in the design doc under a new "Task 1 verification" subsection.

**Outcomes:**

- **Confirmed for at least one gate** → the defect is real. Proceed to Task 7 expecting red, then Task 8.
- **Refuted for every gate** → Poiyomi's ordering is sound for its shader. Task 7 still runs (it documents the behavior); Task 8 is skipped; the design doc records *why* the two shaders legitimately differ, which is itself a valuable result.
- **Cannot obtain the source** → **stop and report.** Do not guess, and do not "fix" the ordering defensively: changing `InterpretNormal` without evidence would be a behavior change made on speculation, which is the mirror image of the defect being investigated.

---

## Task 2 — Publish the durable behavior/support matrix

**Architectural question:** *What have the two frontends actually taught us, in a form that survives adapter #3?*

- [x] Create `docs/architecture/shader-frontend-comparison.md`.
- [x] Carry over from the design doc, as the durable record: the behavior/support matrix; the A–G concept classification; the repeated-pressure table; the "what should remain duplicated" table with its triggers; the future-abstraction-candidates table with the evidence each still lacks.
- [x] State explicitly that the two corrections to `2026-08-17-liltoon-semantics-adapter-design.md` supersede that document's claims about `TryReadBinary` and about category D's uniformity.
- [x] Add a one-line pointer from `docs/architecture/vision.md` under "Semantic understanding". Do not otherwise edit `vision.md`.
- [x] Do **not** copy the milestone-process sections (stop conditions, deferred work, task ordering). Those belong to the spec.

No test. Documentation task.

> **COMPLETE 2026-08-19.** Created `docs/architecture/shader-frontend-comparison.md`
> and added the single pointer line to `docs/architecture/vision.md`. The Task 1
> per-gate finding and the neutral-claim gating rule are recorded there as durable
> architecture guidance.

---

## EXECUTION LOG

**Halted once** at the Task 2 -> Task 3 gate: only the private avatar testbed was
reachable. **Resumed and completed** the same day once the public AMUSE development
Editor was opened. Resume gate satisfied by explicit instance selection
(`AMUSE@aec22723`) and verification that `Application.dataPath = E:/AI/Git/AMUSE/Assets`,
product `AMUSE`, Unity 2022.3.22f1, with no VRChat SDK, VRCFury, or lilToon package
present. The private testbed was never used to run tests or create assets.

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

**Architectural question:** *Which outputs does one texture's import state invalidate, per adapter?* The two answers differ and neither is currently pinned.

- [x] Create `Characterization/SamplerBlastRadiusTests.cs` with `PoiyomiSamplerBlastRadiusTests : PoiyomiFixtureTestBase` and `LilToonSamplerBlastRadiusTests : LilToonFixtureTestBase`.
- [x] **Red:** For each adapter, build a canonical material with `_MainTex`, the normal map, and the emission map all assigned and every output `Complete`. Assert the baseline is fully `Complete` first — a test that starts from an accidentally-`Unknown` baseline proves nothing.
- [x] **Red:** Re-import `_MainTex` with mipmaps enabled so `UnityTextureEvidence.TryGetSampling` refuses, then assert per adapter:
  - **Poiyomi** — `BaseColor`, `Alpha`, `Emission`, and `Normal` all become `Unknown`, each with an `UnsupportedSampling` diagnostic naming `_MainTex`. Every sample routes through `TryGetMainTextureSampling`.
  - **lilToon** — `BaseColor` and `Normal` become `Unknown` with `UnsupportedSampling`; **`Emission` stays `Complete`** because it uses `sampler_EmissionMap`; **`Alpha` stays `Complete`** because it is attested from `LIL_RENDER`, not sampled.
- [x] **Green:** Both classes pass against unmodified production code. If either does not, the production behavior differs from the design doc's reading — **stop and report** rather than adjusting the assertion to match.
- [x] Confirm `UnsupportedSharedMainSampler_InvalidatesEverySample_ConstantSurvives` still passes unmodified.

The lilToon half is the valuable half: it is the first test asserting that an output
**survives** an evidence failure that invalidates its siblings.

---

## Task 4 — Uncertainty monotonicity (T2)

**Architectural question:** *Can removing evidence ever produce a more informative claim?* Nothing in the repository tests this today.

- [x] Create `Characterization/UncertaintyMonotonicityTests.cs` with one class per adapter.
- [x] **Red:** From a fully-`Complete` baseline, apply one evidence-removal mutation at a time and assert for **every** output: the value is either structurally equal to the baseline, or `Unknown`. Never a different `Complete` value; never `Unknown → Complete`. Use `MaterialSemantics`' structural equality directly.
- [x] Mutation list — explicit and hand-written, **not generated**:
  - replace an imported texture with a native `.asset` texture (no `TextureImporter`);
  - enable mipmaps on an assigned texture;
  - flip the normal map's green channel;
  - import the emission map with a source alpha channel;
  - **lilToon only:** remove each `LIL_FEATURE_*` symbol individually from the compiled set;
  - **lilToon only:** pass the empty feature set.
- [x] **Green:** Both classes pass unmodified. The lilToon empty-feature-set case is the sharpest: it must not turn any `Unknown` into `Complete`, and `Alpha` must stay `Complete` because it depends on no feature symbol.
- [x] Confirm `StrippedFeature_KeepsEmissionUnknown` and `StrippedFeature_KeepsNormalUnknown` still pass unmodified; this task generalizes them, it does not replace them.

**Do not** build a generator, a shrinker, or a property-based harness. An explicit list, iterated with `TestCaseSource`.

---

## Task 5 — Cross-adapter shared-evidence agreement (T5)

**Architectural question:** *Is `UnityTextureEvidence` genuinely one contract, or two coincidences that happen to match?*

- [x] Create `Characterization/SharedEvidenceAgreementTests.cs` as a single class inheriting `PoiyomiFixtureTestBase`, reaching lilToon via `Shader.Find` plus `LilToonMaterialSemantics.InterpretVerifiedMaterial`. **No new base class.**
- [x] **Red:** For each of the five shared facts, construct the texture state that makes it refuse and feed it through the slot in **each** adapter that consumes it:
  | Fact | Refusing state | Poiyomi slot | lilToon slot |
  | --- | --- | --- | --- |
  | `TryGetSourceId` | scene-only `Texture2D` | `_MainTex` | `_MainTex` |
  | `TryGetSampling` | mipmaps enabled | `_MainTex` | `_MainTex` |
  | `TryGetColorInterpretation` | native `.asset`, no importer | `_MainTex` | `_MainTex` |
  | `TryProveSampledAlphaIsOne` | source with alpha | `_EmissionMap` | `_EmissionMap` |
  | `IsCanonicalNormalMapImport` | green channel flipped | `_BumpMap` | `_BumpMap` |
- [x] Assert **both** adapters refuse, each with its own diagnostic code. Do **not** assert the two codes are equal — they legitimately are not, and asserting equality would be the first step toward a shared diagnostic framework this milestone forbids.
- [x] **Green:** passes unmodified.
- [x] Confirm `SharedClass_ExposesExactlyFiveSemanticFacts` still passes unmodified.

This is the only test crossing the frontend boundary. It exists to detect one specific
failure: a future edit that makes one frontend stop depending on a shared fact, leaving
`UnityTextureEvidence` with one real consumer and a stale justification.

---

## Task 6 — lilToon unattested-entry refusal parity (T6)

**Architectural question:** *Can interpretation be reached without attestation?* Poiyomi proves it cannot; lilToon's nearest test fails at check 1 only and proves much less.

- [x] **Red:** Add one test to the existing `LilToonAdversarialTests.cs`, mirroring `PublicEntry_UnattestedSchemaCompleteShader_IsRefusedBeforeInterpretation`. Use the stand-in fixture material — whose shader name and `_lilToonVersion` are already correct but whose GUID and digests are not — and call the **public** `AnalyzeBaseMaterial` entry, not the verified seam.
- [x] Assert: `IsSupportedMaterial == false`; all four outputs `Unknown`; **exactly one** diagnostic, scoped to `LilToonSemanticOutput.Material`. Exactly one is the load-bearing assertion — it proves no output interpreter ran.
- [x] **Green:** passes unmodified.
- [x] Add no new file; do not modify any existing lilToon test.

---

## Task 7 — Neutral-claim gating parity (T1)

**Depends on Task 1.** Highest-value test in the milestone.

**Architectural question:** *Is a neutral or zero claim ever made without proving the independent writers off?* Seven of eight sites gate first; one does not.

- [x] Create `Characterization/NeutralClaimGatingTests.cs` with one class per adapter.
- [x] **Red:** Table-driven over an **explicit test-local copy** of each reviewed gate-name list. Do **not** change the production arrays' visibility, do **not** add an accessor for them, and do **not** reach them by reflection merely to consume them. A test-local literal is the reviewed input; a test that reads the production list would pass vacuously if that list were emptied. For each output that can short-circuit to a neutral or zero claim: enable one writer gate, leave the slot's texture unassigned, assert the output is **not** `Complete`.
- [x] Expected first run:
  - **lilToon** — green at all four outputs. Already covered by `EnabledNormalWriter_WithBumpMapDisabled_IsUnknown` and `EnabledSecondNormal_WithNoFirstTexture_IsUnknown`; this generalizes the property to every output.
  - **Poiyomi** — green at `BaseColor`, `Alpha`, and `Emission`; **red at `Normal`**.
- [x] Record the observed red output verbatim in the execution notes. Do not proceed to Task 8 without it.
- [x] **If Task 1 refuted the defect:** convert the Poiyomi `Normal` case to an explicit documented-behavior assertion — that an unassigned `_BumpMap` yields `Unmodified` regardless of writer gates — with a comment citing Task 1's per-gate finding as the justification. Do **not** silently drop the case.

---

## Task 8 — Conditional fix: gate before the Poiyomi neutral normal claim

**Execute only if Task 1 confirmed the defect for at least one gate. Otherwise skip and record why.**

- [x] Confirm Task 7's Poiyomi `Normal` red is observed and captured.
- [x] In `PoiyomiMaterialSemantics.InterpretNormal`, move the `NormalFeatureGates` check **above** the unassigned-`_BumpMap` short-circuit at `:754`, matching lilToon's ordering at `:823`. Add a comment stating the invariant and citing this milestone.
- [x] Change nothing else in the method: `_BumpScale`, UV, stochastic, sampler, identity, and import checks keep their current order and behavior.
- [x] **Green:** Task 7 passes.
- [x] Re-run the **complete** EditMode suite. `MissingBumpMap_IsUnmodified` uses a default material with every writer already off and must still pass unmodified. If any existing test now fails, that test encoded the defect — **stop and report**; do not edit it without a separate decision.
- [x] Confirm the fix is strictly conservative: it can only turn `Complete` into `Unknown`, never the reverse. This is the required direction under AGENTS.md's safety invariant.

---

## Task 9 — Irrelevant-change structural invariance (T4)

**Architectural question:** *Does irrelevant material state leak into semantic values?* One ad-hoc instance exists; the property is not stated generally.

- [x] Create `Characterization/IrrelevantChangeInvarianceTests.cs` with one class per adapter.
- [x] **Red:** From a canonical fully-`Complete` material, mutate one property at a time from a **short, hand-picked, explicit** list of properties that no gate list and no equation reads, and assert the whole `MaterialSemantics` compares **equal** to the baseline.
- [x] Choose the list by reading the gate arrays and equations, not by enumerating the fixture shader. Keep it under roughly a dozen entries per adapter. A generated sweep over every shader property is the combinatorial infrastructure this milestone declines to build.
- [x] If a mutation **does** change the output, resolve it before proceeding: either the property is genuinely read — remove it from the list and note why — or a gate list is reading something it should not, which is a finding to **stop and report**.
- [x] Confirm `RenderStateProperties_DoNotChangeAlpha` still passes unmodified; this task generalizes it.

Placed after Tasks 7–8 because its baseline must be the post-fix behavior.

---

## Task 10 — Architecture checkpoint and handoff

- [x] Run the **complete** EditMode suite and observe the result. Record the counts.
- [x] Verify no forbidden artifact was created: no `IShaderAdapter`, no registry, no shared adapter base, no shared diagnostic type, no schema, no property-based harness, no change to `MaterialSemantics`, `UnityTextureEvidence`, or `LilToonSourceAttestation`.
- [x] Inspect the working-tree and staged diffs **separately**. Confirm only the planned files changed, and that `Packages/manifest.json`, `Packages/packages-lock.json`, `Packages/vpm-manifest.json`, `ProjectSettings/`, and `.github/workflows/` are untouched.
- [x] Inspect every new `.meta` GUID; confirm no existing asset's GUID changed and no reference broke.
- [x] Update `docs/architecture/shader-frontend-comparison.md` with what execution actually established — in particular Task 1's per-gate finding and whether Task 8 ran.
- [x] Update the design doc's stop-condition table with the observed outcomes.
- [x] Report: what changed; what validation ran and its observed result; what was skipped and why; remaining risks; whether the private testbed was used and whether it was modified.
- [x] **Stop at review handoff.** Do not commit, push, or open a PR.

---

## Explicitly not planned

Each was considered against the design's evidence and rejected. Listed so a later
reader can see the omissions were decisions.

| Not planned | Why |
| --- | --- |
| Extracting `RequireAnalyzableMaterial`, `FirstFailedZeroGate`, `TryReadBinary`, `AllUnknown` | Byte-identical and shader-knowledge-free, so eligible on contract — but extracting on two producers creates the shared-utility surface this milestone resists. Trigger is adapter #3, in one pass. |
| Extracting `ComputeNormalizedSourceHash` | Same rule, two producers, zero consumers of a shared version. Extract with the attestation-primitive cluster or not at all. |
| Removing Poiyomi's four pure-delegation wrappers | Real dead indirection, but unrelated to this milestone's question. Separate task or branch. |
| Reconciling the two fixture bases' assertion helpers | A genuine gap — each adapter is missing the other's check — but it is test-infrastructure cleanup, not characterization. Deferred, recorded in the design. |
| A differential-rendering proof-of-concept | Requires a reference evaluator, which is production code. Stop condition fired; recommendation is defer with three named preconditions. |
| A compile + EditMode CI gate | The largest validation gap found — `.github/workflows/` has only `build-listing.yml` and `release.yml`, no test workflow. It is out of this milestone's scope and deserves its own branch, but it should be prioritized **before** adapter #3. |
| Any change to `MaterialSemantics` for `rgb × a` or coverage semantics | Two independent producers each, **zero consumers**. Documented as pressure. |
| Shader adapter #3 | Explicitly out of scope. The design records what should be learned first. |
