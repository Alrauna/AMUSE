# Vendor-Integrated Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Status: 2026-10-25, third revision. Two parallel reviews of the first revision, one on coverage and test strength and one on feasibility, reported 1 CRITICAL, 3 MAJOR, and 9 MINOR findings and 2 CRITICAL, 3 MAJOR, and 2 MINOR findings. The cross-plan consistency review added 1 MAJOR and 2 MINOR vendor findings. The second revision integrated every finding. A scoped re-review of the second revision on 2026-10-25 reported 0 CRITICAL, 1 MAJOR, and 3 MINOR findings, and this revision integrates all four. Two citation remarks from the first round were verified as already correct and are stated in the reply to the owner.

**Goal:** Run the AMUSE EditMode suites green against the real installed vendor packages, convert silent vendor drift into named failures and named skips, and add production-order vendor coverage.

**Architecture:** Stage 1 lands the harness: a version and session-state gate at suite start, named-skip opt-in gates, the two-tier platform split, the registry-provenance event refusal, and the fixture fixes the 2026-10-24 run attributed. Stages 1b to 6 then add the falsifier survival audit, real-source ground checks, real-material cutovers, production-order fixtures, and the documentation updates. Every retired falsifier keeps a same-rule replacement.

**Tech Stack:** C# for Unity 2022.3, NUnit EditMode tests through the Unity Test Runner (Unity MCP `run_tests`), Newtonsoft JSON from the existing package dependency, no new dependencies.

**Spec:** `docs/superpowers/specs/2026-10-25-vendor-integrated-tests-design.md` (third revision). Decision IDs `V1` to `V15`, wrong implementations `W1` to `W10`, and falsifiers `F1` to `F14` refer to it. The investigation it closes is `docs/superpowers/investigations/2026-10-25-vendor-integrated-test-rework-investigation.md`.

**Cross-plan order.** The sibling plan `docs/superpowers/plans/2026-10-25-bundled-locked-original-admission.md` implements the admission design. Its stages 1, 2, 3, 5 (statement only), and 6 do not depend on this plan. Its stage 4 consumes `VendorOptInGate` from Task 3 and the suite-start check from Task 4, so it runs after all of vendor stage 1 has landed, through the Task 10 stage exit. Its stage 7 runs after its own stages 1 to 6. This plan states that order and creates no branch for it. Note, not a task: the V12 choice to keep locked reconstruction tests synthetic reopens when the admission plan's stage 7 flips the bundled rows, and not before.

**Branch.** This plan executes on `rework/vendor-integrated-tests`. The sibling plan creates its own topic branch at execution time from this branch after vendor stage 1 has landed, through the Task 10 stage exit. No task below creates a branch.

## Global Constraints

- All AMUSE paths below are relative to `Packages/com.alrauna.amuse/` unless they start with `Packages/`, `docs/`, or `.github/`. No absolute path appears in any file this plan writes.
- Simple English in every comment, test message, report string, and doc: short active sentences, one idea per sentence, no semicolons, no contractions, no em dashes. Owner decisions are paraphrased and never quoted.
- No machine path, host name, user name, port, GUID value, instance name, or private avatar or asset name in any file, comment, commit message, or test. Vendor roots keep the record placeholders `<poiyomi-toon>` and `<thryeditor>`.
- The canonical environment is the `locked` block of `Packages/vpm-manifest.json` (V2). The top-level `dependencies` block is not canonical. The locked Thry editor id is `com.poiyomi.thryeditor` at 2.74.3.
- Date every status claim 2026-10-25 or the true run date. Records in `docs/superpowers/` speak for their date.
- Never edit, stage, or commit `Packages/manifest.json`, `Packages/packages-lock.json`, `Packages/vpm-manifest.json`, `ProjectSettings/*`, or `Assets/default.unity*`. Stage explicit paths only. Never push. Every commit step runs only with explicit owner authorization.
- Unity runs only in the dev editor instance through Unity MCP. Before any MCP write or test run, read `Application.dataPath` and require an exact match to this repository root. Stop instead of guessing. Never run tests in the private Census Lab project.
- After new files: refresh Unity, then read the console for compile errors. A filtered run that reports 0 tests is a failure. A run whose completed count is below its discovered count is not green and not validation (V7.4).
- Test assemblies: `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor`.
- RED first per AGENTS.md: run each new behavior test, observe the failure against a named plausible wrong implementation, then apply the minimal fix. A first-run pass is recorded as characterization. Never weaken a valid test.
- Falsifier tests carry a `// --- Falsifier N: ... ---` doc comment and a no-op guard that proves the fixture reaches the code path.
- `Assume.That` is banned for environment gating in either test assembly (V1). `Assert.Inconclusive` is banned in tier A (V4). Blanket catches around vendor failures are banned (V8).
- The 30-minute suite duration is a goal and never a gate (V5, owner answer A2 of 2026-10-25). No stage, fixture, or report rule fails or skips on elapsed time. The Poiyomi import and compile cost is recorded data that never gates (V12).
- Every new project-level write sits behind a named opt-in environment variable whose message names the disposable-project requirement (V15). The canonical validation run sets no real-lock gate, because the gate writes project-level state and makes a vendor network call.
- Baseline counts are recorded, never invented. The investigation record section 3 holds the 2026-10-24 baseline for the pinned environment: run 2 gave 3138 passed, 31 failed, 2 inconclusive, 0 skipped, and about 1650 seconds. Task 1 records the fresh counts on the date it runs.
- Dated records under `docs/superpowers/` keep their text.

## Review Focus

1. A clone without every optional vendor package, for example without lilToon. A reasonable person expects a green run whose skips name the missing package and its locked version, and a tier B that stays green. Owner: Task 4, `AbsentOptionalVendorIsRecordedNotFailed`, and Task 7, `TierATwinsIgnoreWithTheMissingPackageNameAndLockedVersion`.
2. A legal SDK-less environment, for example a ChilloutVR or Basis host project. A reasonable person expects tier A to skip by name, tier B to stay green, and the suite-start check to pass. Owner: Task 7, `TierATwinsIgnoreWithTheMissingPackageNameAndLockedVersion`.
3. A Thry editor bump to 2.75.0 or a downgrade inside the 2.74.x line. A reasonable person expects a named suite-start failure that names both versions, never a skip. Owner: Task 4, `ThryeditorMinorBumpFails` and `ThryeditorDowngradeInsideTheLineFails`.
4. A fixture that throws between setting an NDMF plugin-disable key and restoring it. A reasonable person expects the key back at its entry value and the offender named at the base teardown, so one failed test cannot silence a vendor for the session. Owner: Task 5, `DisablePluginForFixtureRestoresTheKeyAfterTheActionThrows`.
5. A real-lock characterization that leaves residue under `Assets/_LockedShaderCache/` or deletes a cache entry a still-referencing material holds. A reasonable person expects the teardown to delete the test's own materials first and to assert a clean cache folder. Owner: Task 18, `ResidueCleanupPlanDeletesMaterialsBeforeCacheEntries`.

## Coverage map

| Item | Owning task | Named test or record |
|---|---|---|
| V1 obligations 1 and 2 | Task 4 | `VersionMismatchAgainstLockedBlockFails` |
| V1 obligation 3 | Task 6 | environment record block |
| V1 obligation 4, A3c | Task 4 | `ThryeditorPatchUpgradeInsideTheLineNeitherFailsNorSkips` |
| V2 | Task 4 | locked block as the only version source |
| V3 | Task 8 | `ClosePassAddsNoAvatarEntryWhenTheBarrierAlreadyRefused`, `EventRefusalSurvivesTwoHandRegisteredCloneHops` |
| V4 | Task 7 | `NoTestPlatformClaimsABuiltInPlatformName`, `EveryTierAAssertionHasATierBTwin` |
| V5 | Task 6, Task 16, Task 17 | duration fields, import cost record, dispatcher fixtures |
| V6 | Task 13 | `RealAaoAtlasIsAdmittedThroughTheGeneratedRoute` |
| V7 items 1, 3, 5, 6 | Task 5 | `DisablePluginForFixtureRestoresTheKeyAfterTheActionThrows`, `GeneratedOutputCleanupRemovesOnlyAmuseContainers`, Step 4 suite wiring |
| V7 item 2 | Task 4, Task 5 | suite-start fixture, known-key set wiring |
| V7 items 4, 7, 8 | Task 6 | `SettingsDiffReportsChangedLinesByName`, run record |
| V8 | Task 12 | verdict table in this plan |
| V9 item 1 | Task 2 | diagnosis record, `DecideRefusesABlockedLockedSlotWithCleanBindings` |
| V9 item 2 | Task 11 | attribution record |
| V9 item 3 | Task 3 | five gate conversions, `EnvironmentGatingNeverUsesAssumeThat` |
| V10 items 1 to 3 | Task 14 | ground checks, `ProductionSourceNeverCallsTheVendorLockTool` |
| V10 item 4 | Task 13 | `RealAaoAtlasIsAdmittedThroughTheGeneratedRoute` |
| V11 | Task 15 | real lilToon twins, cutover ledger |
| V12 | Task 16 | real bundled 9.3 twins, import cost record |
| V13 item 1 | Task 17 | production-order fixtures |
| V13 items 2 and 3 | Task 18 | real-lock characterization, `VendorOptInGate.RealThryLock` |
| V13 item 4 | Task 19 | VRCFury license note in the setup document |
| V14 | Task 19 | doc updates |
| V15 | Tasks 17, 18 | opt-in gates |
| W1 | Task 8 | F1 test |
| W2 | Task 8 | F2 tests |
| W3 | Task 7 | F3 test |
| W4 | Tasks 3, 7 | F4 and F13 tests |
| W5 | Task 13 | F5 and F6 named existing tests |
| W6 | Task 4 | F7 and F14 tests |
| W7 | Task 18 | F8 test |
| W8 | Task 5 | F9 test |
| W9 | Task 14 | F10 test |
| W10 | Task 12 | F11 audit rule |
| F12 | Task 2 | `DecideRefusesABlockedLockedSlotWithCleanBindings` |
| F13 | Task 3 | `EnvironmentGatingNeverUsesAssumeThat` |
| F14 | Task 4 | the thryeditor fixtures and the note test in Task 4 |

---

### Task 1: Baseline run record (stage 1)

**Files:**
- Create: `docs/superpowers/investigations/2026-10-25-vendor-integrated-test-run-records.md`

**Interfaces:**
- Produces: the dated run-record file that Tasks 8, 10, 12 to 16, and 19 append to. Every later validation run adds one dated block with the fields below.

Non-test task. It records the starting point and never gates anything.

- [ ] **Step 1: Verify the dev editor instance.** Read `Application.dataPath` through the MCP editor state and require an exact match to this repository root. Stop on any mismatch.
- [ ] **Step 2: Run the full two-assembly suite.** Run `run_tests` EditMode over `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` with no filter. Expected: the run completes with discovered equal to completed. On 2026-10-24 the same environment gave 3138 passed, 31 failed, 2 inconclusive, 0 skipped. This fresh run records its own observed counts.
- [ ] **Step 3: Write the run record.** One dated block per run with: discovered, completed, passed, failed, skipped, inconclusive, both run durations and their sum, the inconclusive test names, the failed test names, and the names of the gated tests that reported inconclusive (the V9 item 3 input). Open the file with a privacy note that names no machine, account, port, instance, or private avatar, and with the statement that product tests never run in the private Census Lab project.
- [ ] **Step 4: Commit, only with explicit owner authorization.**

```bash
git add docs/superpowers/investigations/2026-10-25-vendor-integrated-test-run-records.md
git commit -m "docs: record vendor-integrated baseline run"
```

### Task 2: Property-block writer diagnosis (stage 1, V9 item 1)

**Files:**
- Create: `docs/superpowers/investigations/2026-10-25-vendor-property-block-writer-diagnosis.md`
- Test: `Tests/Editor/Build/LockedMaterialAdmissionTests.cs`
- Modify after the finding: `Tests/Editor/Build/TransientUnlockSwapInTests.cs` (the fixture `ABindingOnAnotherRendererPathDoesNotRefuseThisLockedMaterial`)

**Interfaces:**
- Consumes: `LockedMaterialAdmission.Decide(AlignedLockedSource source, bool anyRendererHasPropertyBlock, IEnumerable<string> materialBindingPropertyNames)` at `Editor/Build/LockedMaterialAdmission.cs:30-47`.
- Produces: the dated diagnosis record that names the writer of the swap-in `MaterialPropertyBlock`, or the dated open-candidates list with the evidence for each.

The load-bearing invariant (V9.1): any outcome that touches the check order at `Editor/Build/LockedMaterialAdmission.cs:44-47` keeps refusing a locked slot that carries a live property block, unless the diagnosis first proves the writer cannot carry an alpha-relevant value on a proven slot. A reorder that only reports the more specific cause first is safe. A reorder that lets a blocked locked slot swap in is the declared false positive.

- [ ] **Step 1: Write the invariant guard test.** In `LockedMaterialAdmissionTests` add `DecideRefusesABlockedLockedSlotWithCleanBindings`: `Decide(source, true, Array.Empty<string>())` returns `AlphaSeparationSlotRefusal.LockedMaterialPropertyBlock` with no binding input at all. Mark it `// --- Falsifier 12: a reorder of the locked-slot checks admits a blocked locked slot with a clean binding set. ---`
- [ ] **Step 2: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Build.LockedMaterialAdmissionTests"]`. Expected: PASS on first run. Record it as characterization in the diagnosis record.
- [ ] **Step 3: Run the reproduction.** Build a minimal fixture outside the committed suites: one skinned renderer, one float material clip, one locked stand-in material from `TransientUnlockTestLifecycle`. Build it through `AvatarProcessor.ProcessAvatar(root, AmbientPlatform.DefaultPlatform)`. Probe `Renderer.HasPropertyBlock()` after each vendor pass and after each NDMF scope boundary, and probe it immediately before the swap-in reads the slot. Check the animator rebind first, because `Editor/Build/AmusePlatformFinishPlugin.cs:208-216` documents that the phase-end rebind re-applies stale curve values over the live renderer.
- [ ] **Step 4: Write the diagnosis record.** Date it 2026-10-25 or the run date. State the observed writer with its evidence, or the open candidates with the probe results. State the invariant outcome: either the writer cannot carry an alpha-relevant value on a proven slot, or the check order at `:44-47` stays. State the fixture consequence from Step 5.
- [ ] **Step 5: Rebuild the stage 1 fixture per the finding.** If the writer is the animator rebind, the fixture `ABindingOnAnotherRendererPathDoesNotRefuseThisLockedMaterial` clears any property block on its renderer immediately before the build with `SetPropertyBlock(null)`, asserts that precondition, and keeps expecting `LockedMaterialAnimationMismatch`. If the diagnosis proves the build itself writes a block on this fixture, the fixture asserts `LockedMaterialPropertyBlock` instead and cites the diagnosis record in its comment. Either way the expected refusal names one cause and the fixture asserts its planted state.
- [ ] **Step 6: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Build.TransientUnlockSwapInTests"]`. Expected: PASS for the rebuilt fixture and 0 failed for the class.
- [ ] **Step 7: Commit, only with explicit owner authorization.** Stage the diagnosis record, the guard test, and the fixture file.

```bash
git add docs/superpowers/investigations/2026-10-25-vendor-property-block-writer-diagnosis.md Tests/Editor/Build/LockedMaterialAdmissionTests.cs Tests/Editor/Build/TransientUnlockSwapInTests.cs
git commit -m "test: pin the blocked-slot refusal and diagnose the swap-in property block writer"
```

### Task 3: VendorOptInGate and the five named-skip conversions (stage 1, V9 item 3, V1)

**Files:**
- Create: `Tests/Editor/Build/VendorOptInGate.cs`
- Test: `Tests/Editor/Build/VendorOptInGateTests.cs`, `Tests/Editor/Build/EnvironmentGateAuditTests.cs`
- Modify: `Tests/Editor/Build/DaoMergedConsumptionTests.cs:106-118`, `Tests/Editor/Build/AaoMergedConsumptionTests.cs:102`, `Tests/Editor/Build/AaoMergedConsumptionTests.cs:155`, `Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs:1182-1185`

**Interfaces:**
- Produces, for Task 18 and for the sibling admission plan stage 4:
  - `internal static class VendorOptInGate` in namespace `Alrauna.Amuse.Tests.Editor.Build`
  - `internal const string RealThryLock` holding the exact string `AMUSE_REAL_THRY_LOCK`
  - `internal const string DaoIntegration` holding the exact string `AMUSE_DAO_INTEGRATION`
  - `internal static void Require(string variable, string disclosure)` that calls `Assert.Ignore` with the variable name and the disclosure when the variable does not equal `1`, and returns otherwise

The spec V9.3 cites the lilToon gate file as `Tests/Editor/Build/Semantics/LilToon/`. The checked-out file lives at `Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`. This plan uses the checked-out path. Plan call, recorded in the plan gap list of the final report.

The five existing `Assume.That` environment gates convert to named skips. A conversion keeps each site's disclosure text and adds the missing package and its locked version. The d4rk absence site names `d4rkpl4y3r.d4rkavataroptimizer` 4.6.0. The two AAO sites and the lilToon site name `jp.lilxyzw.liltoon` 2.4.1. The DAO gate site becomes `VendorOptInGate.Require(VendorOptInGate.DaoIntegration, <existing disclosure>)`.

- [ ] **Step 1: Write the failing gate tests.** In `VendorOptInGateTests`:
  - `GateConstantsMatchTheDocumentedVariableNames`: both constants hold their exact documented strings.
  - `RequireIgnoresWithANamedReasonWhenTheVariableIsUnset`: with the variable removed from the environment, `Require` throws `IgnoreException` whose message contains the variable name and the disclosure.
  - `RequireReturnsWhenTheVariableIsOne`: with the variable set to `1`, `Require` returns normally. Save and restore the environment variables around both tests.
- [ ] **Step 2: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Build.VendorOptInGateTests"]`. Expected: FAIL, the type does not exist.
- [ ] **Step 3: Implement `VendorOptInGate`.** One static class, two constants, one method. No other member.
- [ ] **Step 4: Run** the class filter. Expected: PASS.
- [ ] **Step 5: Convert the five gate sites.** Each converted site keeps its test's meaning: an unset gate still stops the test, now as a named skip that the report lists. The two d4rk gated tests are `DaoMergeRunsInTheSdkSequenceAndPreservesSoundness` and `AaoAndDaoCombinedKeepsTheMergedSoundness`. Confirm by reading their bodies that the gate variable is the only environment input.
- [ ] **Step 6: Write the audit test.** In `EnvironmentGateAuditTests` add `EnvironmentGatingNeverUsesAssumeThat` marked `// --- Falsifier 13: an environment gate that still uses Assume.That reports inconclusive instead of a named skip. ---`. The test scans every `.cs` file under `Tests/` of both test assemblies for the string `Assume.` and fails naming any file and line. The scan has exactly one exclusion, its own file, `EnvironmentGateAuditTests.cs`, skipped by name, because the falsifier comment quotes the banned words. On 2026-10-25 exactly five sites exist and Step 5 removed them all, so after that exclusion the scan admits no other allowlist. The research assembly has zero `Assume.` uses on 2026-10-25.
- [ ] **Step 7: Run** both new classes and the three converted classes. Expected: PASS, and the two d4rk tests report as skipped in an unset-gate run.
- [ ] **Step 8: Commit, only with explicit owner authorization.** Stage the four touched test files and the two new files.

```bash
git add Tests/Editor/Build/VendorOptInGate.cs Tests/Editor/Build/VendorOptInGateTests.cs Tests/Editor/Build/EnvironmentGateAuditTests.cs Tests/Editor/Build/DaoMergedConsumptionTests.cs Tests/Editor/Build/AaoMergedConsumptionTests.cs Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs
git commit -m "test: gate vendor integration through named skips instead of assumptions"
```

### Task 4: Vendor environment check and suite-start check (stage 1, V7 item 2, V1, V2)

**Files:**
- Create: `Tests/Editor/Build/VendorEnvironmentCheck.cs`
- Create: `Tests/Editor/Build/VendorSuiteStartCheck.cs`
- Test: `Tests/Editor/Build/VendorEnvironmentCheckTests.cs`
- Modify: `Tests/Editor/RunEndSceneHygiene.cs` (add a `[OneTimeSetUp]` method to the existing setup fixture, calling `VendorSuiteStartCheck.RunAll()`)

**Interfaces:**
- Produces, for Task 6 and Task 12:
  - `internal enum VendorVersionState { Match, Mismatch, Absent, ThryPatchUpgrade }`
  - `internal readonly struct VendorVersionEntry { string Package, string Locked, string Installed, VendorVersionState State, string Note }` with one constructor taking all fields. `Note` is null for every state except `ThryPatchUpgrade`, where it holds the not-yet-validated note text.
  - `internal static IReadOnlyList<VendorVersionEntry> VendorEnvironmentCheck.Evaluate(IReadOnlyDictionary<string, string> installed, IReadOnlyDictionary<string, string> locked)`
  - `internal static void VendorSuiteStartCheck.RunAll()`
  - `internal static Func<IReadOnlyList<VendorVersionEntry>> VendorSuiteStartCheck.EntriesOverride` with a null default. `RunAll` calls the override instead of the real sources when it is set, which is the seam the RunAll tests below inject through.

Rules pinned by the spec:

- The locked side reads the `locked` block of `Packages/vpm-manifest.json`. Never the top-level `dependencies` block (V2).
- The installed side reads the Unity package database.
- A locked package with no installed match is an `Absent` entry. Equal versions are `Match`. Any other difference is `Mismatch`.
- The one exception (V1 obligation 4, owner decision A3c): for `com.poiyomi.thryeditor`, when both versions sit in the 2.74.x line, same major and same minor, and the installed patch is newer than the locked patch, the entry is `ThryPatchUpgrade`. Owner decision A3c, 2026-10-25: future 2.74.x releases are treated as free of breaking changes for now. No precedent beyond that is assumed. Tests must catch a breaking change in an AMUSE dependency. A downgrade inside the line and a bump to 2.75.0 or later stay `Mismatch`.
- Suite start asserts: no known `nadena.dev.ndmf.plugin-disabled.*` key is set, zero `Mismatch` entries with both versions named in the failure, and, when `com.vrchat.avatars` is installed, the `AMUSE_VRCSDK3_AVATARS` define is active. The define check compiles in both define states: in the `#if !AMUSE_VRCSDK3_AVATARS` branch the code reads the installed packages and fails when `com.vrchat.avatars` is present, so a broken version define cannot silently drop the SDK compile path. `Absent` and `ThryPatchUpgrade` entries are recorded, never failed. The category-presence assertion over the discovered test set belongs to Task 7, which introduces the category and owns the wiring into `RunAll`, so no step of this task depends on it.

- [ ] **Step 1: Write the failing tests.** In `VendorEnvironmentCheckTests`, each with a hand-built installed and locked map:
  - `VersionMismatchAgainstLockedBlockFails`: locked `jp.lilxyzw.liltoon` 2.4.1, installed 2.5.0, gives `Mismatch`. Mark it `// --- Falsifier 7: a suite-start check that skips on a version mismatch reports green on an environment that is not the locked one. ---`
  - `ThryeditorMinorBumpFails`: locked `com.poiyomi.thryeditor` 2.74.3, installed 2.75.0, gives `Mismatch`. Second falsifier 7 witness.
  - `ThryeditorDowngradeInsideTheLineFails`: locked 2.74.3, installed 2.74.2, gives `Mismatch`.
  - `ThryeditorPatchUpgradeInsideTheLineNeitherFailsNorSkips`: locked 2.74.3, installed 2.74.4, gives `ThryPatchUpgrade`. Mark it `// --- Falsifier 14: a suite-start check that fails or skips a thryeditor upgrade inside the 2.74.x line misstates the A3c state. ---`
  - `DriftedPatchEntryCarriesTheNotYetValidatedNote`: the `ThryPatchUpgrade` entry exposes the recorded note text, which states that no gated real-lock run has yet exercised the drifted version and that the version counts as validated only after one dated gated real-lock pass against it (V1 obligation 4). Second falsifier 14 witness.
  - `AbsentOptionalVendorIsRecordedNotFailed`: a locked package with no installed match gives `Absent`.
  - `MatchingVersionsGiveMatchEntries`.
  - `RunAllFailsOnAnInjectedMismatchEntry`, marked `// --- Falsifier 7 second witness: a RunAll that calls the skip path on a mismatch reports green on a drifted environment. ---`: with `EntriesOverride` returning one `Mismatch` entry, `RunAll` throws the assertion failure that names both versions, so a skip-path implementation throws `IgnoreException` instead and fails this test.
  - `RunAllRecordsAnInjectedDriftedPatchWithoutFailing`: with `EntriesOverride` returning one `ThryPatchUpgrade` entry, `RunAll` returns normally.
- [ ] **Step 2: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Build.VendorEnvironmentCheckTests"]`. Expected: FAIL, the type does not exist.
- [ ] **Step 3: Implement** the evaluator and `RunAll`. The evaluator is pure string data. `RunAll` reads the two version sources, or the `EntriesOverride` when set, and applies the assert and record rules above. It reads the known disable keys through `internal static Func<IEnumerable<string>> VendorSuiteStartCheck.KnownDisableKeys` with a default that returns the AAO key. Task 5 owns replacing that default with the guard's known set, because Task 5 owns the session-state wiring.
- [ ] **Step 4: Run** the class filter. Expected: PASS.
- [ ] **Step 5: Run** the full product assembly once. Expected: the suite-start check passes in the pinned environment and reports one dated record line with the effective environment.
- [ ] **Step 6: Commit, only with explicit owner authorization.**

```bash
git add Tests/Editor/Build/VendorEnvironmentCheck.cs Tests/Editor/Build/VendorSuiteStartCheck.cs Tests/Editor/Build/VendorEnvironmentCheckTests.cs Tests/Editor/RunEndSceneHygiene.cs
git commit -m "test: assert the locked vendor environment at suite start"
```

### Task 5: Session-state guard, suite wiring, seam audit, generated-output cleanup, and failure attribution (stage 1, V7 items 1, 2 wiring, 3, 5, 6)

**Files:**
- Create: `Tests/Editor/Build/VendorSessionStateGuard.cs`
- Create: `Tests/Editor/Build/AmuseGeneratedOutputCleanup.cs`
- Test: `Tests/Editor/Build/VendorSessionStateGuardTests.cs`
- Modify: `Tests/Editor/Build/TransientUnlockTestLifecycle.cs:99-110` (the AAO pair delegates to the guard)
- Modify: `Tests/Editor/Build/MergedConsumptionFixture.cs:613-618` (the completion assertion names the failing pass)
- Modify: `Tests/Editor/Build/VendorSuiteStartCheck.cs` (replace the `KnownDisableKeys` default and call `AssertNoKnownPluginDisabled` at the start of `RunAll`)
- Modify: `Tests/Editor/RunEndSceneHygiene.cs` (the base teardown asserts touched keys and deletes the generated output)

**Interfaces:**
- Produces, for Task 12 and Task 18:
  - `internal static class VendorSessionStateGuard`
  - `internal static IDisposable DisablePluginForFixture(string pluginId)` that saves the prior `SessionState` bool of `nadena.dev.ndmf.plugin-disabled.<pluginId>`, sets it to true, and restores it on Dispose
  - `internal static void AssertAllTouchedKeysAtEntryValues()` that fails naming the key and both values when any key the suite touched this session is off its entry value
  - `internal static void AssertNoKnownPluginDisabled()` for suite start
  - `internal static IReadOnlyList<string> VendorSessionStateGuard.KnownPluginIds`, the exact known set. On 2026-10-25 it is exactly `["com.anatawa12.avatar-optimizer"]`, the only NDMF plugin-disable key the suite touches. The key format is `nadena.dev.ndmf.plugin-disabled.<pluginId>`. Task 12 extends the set when a scoping verdict disables another vendor, so the suite-start known set stays exact.
  - `internal static IReadOnlyList<string> AmuseGeneratedOutputCleanup.Plan(string generatedRoot)` returning the names it will delete, every name starting with `AMUSE ` or with `_AMUSE `. The underscore form is what NDMF's asset saver writes when its reserved-name filter matches a container name anywhere, case insensitively, verified at `Packages/nadena.dev.ndmf/Editor/API/Serialization/AssetSaver.cs:174-193`. Both prefixes stay inside the test-owned set.
  - `internal static void AmuseGeneratedOutputCleanup.Delete(string generatedRoot)` that deletes exactly the planned set

- [ ] **Step 1: Write the failing tests.** In `VendorSessionStateGuardTests`, with a test-local plugin id:
  - `DisablePluginForFixtureRestoresTheKeyAfterTheActionThrows`: open the guard, throw inside the scope, catch, and assert the key is back at its entry value and `AssertAllTouchedKeysAtEntryValues` passes. Mark it `// --- Falsifier 9: a fixture that restores its plugin-disable key after the assertions leaves the key set when an assertion throws. ---`
  - `AssertAllTouchedKeysAtEntryValuesNamesAnOffender`: with a key touched and then flipped outside the guard, the assertion fails with the key name and both values in the message.
  - `GeneratedOutputCleanupRemovesOnlyAmuseContainers`: in a temp sandbox root create `AMUSE one`, `_AMUSE two`, and `Keep Me`, run `Delete`, and assert both prefixed forms are gone and `Keep Me` survives. The underscore form is the test witness for the NDMF reserved-name filter, which current fixture names hit, for example a container whose name contains the fragment inside the word control.
- [ ] **Step 2: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Build.VendorSessionStateGuardTests"]`. Expected: FAIL, the types do not exist.
- [ ] **Step 3: Implement** the guard and the cleanup. The cleanup default root is the NDMF generated folder under `Packages/nadena.dev.ndmf/__Generated` (record section 7.3). Deletion stays inside the two pinned prefixes, `AMUSE ` and `_AMUSE `. `TransientUnlockTestLifecycle.DisableAaoForFixture` and `RestoreAaoAfterFixture` keep their names and call the guard, so the AAO key joins the touched registry. `RestoreAaoAfterFixture` becomes a no-op restore over the guard's saved value.
- [ ] **Step 4: Wire the suite-level call sites.** Replace the `VendorSuiteStartCheck.KnownDisableKeys` default with a delegate that reads `VendorSessionStateGuard.KnownPluginIds`, and call `AssertNoKnownPluginDisabled` at the start of `RunAll`. In the `[OneTimeTearDown]` of `RunEndSceneHygiene`, after the existing scene hygiene, call `AssertAllTouchedKeysAtEntryValues` first, so a failed key restore names the offender test immediately, and then `AmuseGeneratedOutputCleanup.Delete` with the default root. This step is the single owner of the suite-start known-key set, the base-teardown assertion, and the cleanup wiring.
- [ ] **Step 5: Extend the completion assertion.** When `context.Successful` is false, the message of `MergedConsumptionFixture.AssertBuiltTrianglesMatchAuthoring` appends the failing pass and plugin names from the NDMF error report, so a vendor failure is named a vendor failure (V7 item 6). Keep the existing message prefix.
- [ ] **Step 6: Run the static seam audit (V7 item 3).** For each seam, name the failure-safe restore site in the audit table inside the commit message body. The four seams and their verified 2026-10-25 restore sites: `AmusePlatformFinishPass.PresenterForTests` restores null at `Tests/Editor/Build/ConsentSeamTests.cs:59` and `Tests/Editor/Build/MergedConsumptionFixture.cs:136`, `SyntheticPluginScope.IsArmed` restores through the scope's Dispose at `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:8909-8930`, `TransientUnlockTestKnobs` restores through `TransientUnlockTestLifecycle.Reset`, and `MarkerStubBindings` arms through `SpecialClipNames` in `Tests/Editor/Build/AlphaSeparationApplyTests.cs`. If any audit step finds a seam without a failure-safe restore, add the missing finally path in the same commit.
- [ ] **Step 7: Run** the guard class, `VendorEnvironmentCheckTests`, `TransientUnlockTransformationTests`, and `TransientUnlockAvailabilityTests`. Expected: PASS, with `RunAll` reading the real known-key set.
- [ ] **Step 8: Commit, only with explicit owner authorization.**

```bash
git add Tests/Editor/Build/VendorSessionStateGuard.cs Tests/Editor/Build/AmuseGeneratedOutputCleanup.cs Tests/Editor/Build/VendorSessionStateGuardTests.cs Tests/Editor/Build/TransientUnlockTestLifecycle.cs Tests/Editor/Build/MergedConsumptionFixture.cs Tests/Editor/Build/VendorSuiteStartCheck.cs Tests/Editor/RunEndSceneHygiene.cs
git commit -m "test: restore vendor session state on failure and name failing vendor passes"
```

### Task 6: Side-effect record, duration record, and environment record (stage 1, V7 items 4, 7, 8, V1 obligation 3, V5)

**Files:**
- Create: `Tests/Editor/Build/VendorRunRecord.cs`
- Test: `Tests/Editor/Build/VendorRunRecordTests.cs`
- Modify: `Tests/Editor/RunEndSceneHygiene.cs` (capture before the first test, report after the last)

**Interfaces:**
- Consumes: `VendorEnvironmentCheck.Evaluate` from Task 4.
- Produces, for every later validation task:
  - `internal static void VendorRunRecord.CaptureBeforeRun()` that digests `ProjectSettings/ProjectSettings.asset` and `ProjectSettings/EditorSettings.asset` and records the observable scripting defines
  - `internal static IReadOnlyList<string> VendorRunRecord.DiffLines(string before, string after)` returning changed lines by name
  - `internal static string VendorRunRecord.BuildReport(int discovered, int completed, int passed, int failed, int skipped, int inconclusive, TimeSpan productDuration, TimeSpan researchDuration, IReadOnlyList<string> skipReasons, IReadOnlyList<VendorVersionEntry> entries)` returning the report block under the stable header `=== AMUSE vendor run record ===`. The block records both assembly durations and their sum, which the method computes.

The report block lists, per run: the counts, the duration, the opt-in gates that were set, the gated fixtures the run excluded, the per-test skip reasons with the named absent package and its locked version, the effective environment from the entries, the changed setting lines, and, for a `ThryPatchUpgrade` entry, the not-yet-validated note from Task 4. The harness records vendor load-hook writes such as the VRCFury settings writes and never prevents them (record section 7.3). No fixture asserts against the committed value of a vendor-written setting.

- [ ] **Step 1: Write the failing test.** `SettingsDiffReportsChangedLinesByName`: two short setting texts that differ in one value line give exactly that line, and equal texts give an empty list.
- [ ] **Step 2: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Build.VendorRunRecordTests"]`. Expected: FAIL.
- [ ] **Step 3: Implement.** The executor copies the printed report block into the dated run-record file of Task 1 after each validation run. The durations are the two assembly durations and their sum, recorded with the date (V5). No rule reads them for a decision.
- [ ] **Step 4: Run** the class filter, then the full product assembly once. Expected: PASS, and the console carries one report block per run.
- [ ] **Step 5: Commit, only with explicit owner authorization.**

```bash
git add Tests/Editor/Build/VendorRunRecord.cs Tests/Editor/Build/VendorRunRecordTests.cs Tests/Editor/RunEndSceneHygiene.cs
git commit -m "test: record the effective vendor environment and run side effects"
```

### Task 7: Platform identity audit and the two tiers (stage 1, V4)

**Files:**
- Modify: `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (rename `TestVrchatPlatform` at `:8932-8940`, rename `TestGenericPlatform` at `:8952-8959`, rename `ConsentTestGenericPlatform` at `:925-932`, add the mirrored five-pass plugin, add the eight tier A twins)
- Modify: `Tests/Editor/Build/AlphaSeparationPreparationTests.cs` (rename `PreparationTestPlatform`, class at `:13067`, qualified name at `:13072`)
- Modify: `Tests/Editor/Build/AmuseReportsSlotTests.cs` (rename `FoldReportPlatform`, class at `:1023`, qualified name at `:1028`)
- Modify: `Tests/Editor/Build/VersionConsentTests.cs` (rename `ConsentTestPlatform`, class at `:436`, qualified name at `:441`)
- Create: `Tests/Editor/Build/VendorVrChatTierGate.cs`
- Test: `Tests/Editor/Build/PlatformProviderAuditTests.cs`
- Modify: `Tests/Editor/Build/VendorSuiteStartCheck.cs` (add the category-presence assertion to `RunAll`)

**Interfaces:**
- Consumes: the named-skip pattern of `FixtureAvatarIdentity.RequireVrcDescriptorSupport` at `Tests/Editor/Build/FixtureAvatarIdentity.cs:47-56`.
- Produces:
  - Tier B: the eight platform tests of record section 4.1 run on a renamed dedicated provider whose `QualifiedName` is `com.alrauna.amuse.tests.platform-finish`. The class `TestVrchatPlatform` becomes `PlatformFinishTestPlatform`. Vendor VRChat passes never select there, because NDMF selects passes by qualified name.
  - Tier A: one twin per tier B test, same fixture body, built through `AmbientPlatform.DefaultPlatform`, which is the real VRChat platform instance in this environment (record section 4.2). Each twin carries `[Category("VendorVRChatPlatform")]` and the method name is the tier B name plus `VendorVRChat`. The twin pairs through that suffix rule, which the audit test below and the suite-start category check both read.
  - The six renamed claimers on 2026-10-25: `TestVrchatPlatform` to `com.alrauna.amuse.tests.platform-finish`, `TestGenericPlatform` to `com.alrauna.amuse.tests.generic-probe`, `ConsentTestGenericPlatform` to `com.alrauna.amuse.tests.swap-consent-generic`, `PreparationTestPlatform` to `com.alrauna.amuse.tests.alpha-separation-preparation`, `FoldReportPlatform` to `com.alrauna.amuse.tests.slot-report-fold`, and `ConsentTestPlatform` to `com.alrauna.amuse.tests.version-consent`.
  - The mirrored five-pass plugin `PlatformFinishTestPlugin` in `AmusePlatformFinishPluginTests.cs`, registered on the renamed tier B platform, configured with the five production pass bodies in the production order of `Editor/Build/AmusePlatformFinishPlugin.cs:262-305`, following the `AlphaSeparationApplyTestPlugin` precedent at `Tests/Editor/Build/AlphaSeparationApplyTests.cs:196-215`.
  - The tier A gate helper, pinned: `internal static class VendorVrChatTierGate` with `internal static void RequireInstalledVrcSdkPackages()`, backed by the seam `internal static Func<IEnumerable<string>> InstalledPackageIds` whose default reads the Unity package database. Without `com.vrchat.avatars` or `com.vrchat.base` it throws `IgnoreException` naming the absent package and its locked version 3.10.5. A version drift of an installed SDK package is a suite-start failure under V1, never a skip.
  - The suite-start category check, owned here: `internal static void VendorSuiteStartCheck.AssertTierACategoryPresent()`, called from `RunAll`, reflecting over the test assembly and failing when the SDK is installed and no `[Test]` method carries `[Category("VendorVRChatPlatform")]`.

The defect of record section 4.1: a test platform returned `WellKnownPlatforms.VRChatAvatar30` while being a different object than the real VRChat provider. NDMF selects passes by qualified name, so vendor VRChat passes ran and threw on their identity check. Observed 2026-10-24. The audit pins the rule. The eight tier B tests are its dated RED witnesses.

- [ ] **Step 1: Write the audit tests.** In `PlatformProviderAuditTests`:
  - `NoTestPlatformClaimsABuiltInPlatformName`, marked `// --- Falsifier 3: a test platform whose QualifiedName equals a name owned by another provider object selects that provider's passes and then fails their identity checks. ---`. The test enumerates every `INDMFPlatformProvider` type in both test assemblies through `TypeCache`, filtered by assembly name, takes each `Instance` singleton or a non-public constructor instance, and asserts the qualified name is none of the built-in names `WellKnownPlatforms.Generic`, `WellKnownPlatforms.VRChatAvatar30`, `WellKnownPlatforms.Resonite`, and that no two providers share a name. Expected before the renames: FAIL naming all six claimers, `TestVrchatPlatform`, `TestGenericPlatform`, `ConsentTestGenericPlatform`, `PreparationTestPlatform`, `FoldReportPlatform`, and `ConsentTestPlatform`, plus the duplicate-name assertion over the five generic claimers.
  - `EveryTierAAssertionHasATierBTwin`, marked `// --- Falsifier 4: a tier A test whose AMUSE behavior assertion has no tier B twin turns a product regression into a named skip on an SDK-less environment. ---`. The test reflects over every `[Test]` method carrying `[Category("VendorVRChatPlatform")]`, strips the `VendorVRChat` suffix, and asserts a `[Test]` method with the base name exists in the same assembly.
  - `TierATwinsIgnoreWithTheMissingPackageNameAndLockedVersion`: with `VendorVrChatTierGate.InstalledPackageIds` returning a source that lacks the SDK package ids, `RequireInstalledVrcSdkPackages` throws `IgnoreException` whose message names the absent package and its locked version.
  - `SuiteStartRequiresTheTierACategoryWhenTheSdkIsInstalled`: with the SDK installed, `VendorSuiteStartCheck.AssertTierACategoryPresent` passes once the twins exist. Expected before Step 5: FAIL, no test carries the category yet. This step also creates `AssertTierACategoryPresent` in `Tests/Editor/Build/VendorSuiteStartCheck.cs` as the reflection check over the test assembly, so the audit test compiles before the Step 2 run. Step 5 wires it into `RunAll`.
- [ ] **Step 2: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Build.PlatformProviderAuditTests"]`. Expected: the name audit FAILS on the six claimers and the category check FAILS. Record both as the observed RED.
- [ ] **Step 3: Apply the six renames.** Rename the qualified names and the `TestVrchatPlatform` class to `PlatformFinishTestPlatform`. Update its eight call sites at `:63`, `:2169`, `:2259`, `:2286`, `:2327`, `:2407`, `:2651`, and `:2705`.
- [ ] **Step 4: Add the mirrored five-pass plugin.** In `AmusePlatformFinishPluginTests` add `PlatformFinishTestPlugin`, a `Plugin<T>` carrying `[RunsOnPlatforms]` with the renamed platform name, whose `Configure` runs the five production pass bodies in the production order: the structural graph check, the bindings capture, the barrier, and the apply inside one `AnimatorServicesContext` scope, then the window close, mirroring `Editor/Build/AmusePlatformFinishPlugin.cs:262-305`. Register the plugin with an assembly-level `[assembly: ExportsPlugin(typeof(...PlatformFinishTestPlugin))]` line at the top of the file, next to the existing registrations at `:22-25`, because the NDMF resolver discovers plugins only through that attribute (`Packages/nadena.dev.ndmf/Editor/API/Attributes/ExportsPlugin.cs:14-29`, resolver read at `Packages/nadena.dev.ndmf/Editor/API/Solver/PluginResolver.cs:91-95`). The renamed tier B tests read `AmusePlatformFinishState` from this plugin, because the production plugin no longer selects on a dedicated name. Probe passes stay armed through `SyntheticPluginScope`.
- [ ] **Step 5: Add the eight tier A twins and the gate helper.** Create `VendorVrChatTierGate` with the pinned seam and behavior. Add the eight tier A twins with the category, the `RequireInstalledVrcSdkPackages` call, one assertion that the build completes, and the twin's core outcome assertion. No AMUSE behavior assertion exists only inside tier A. Wire `AssertTierACategoryPresent` into `RunAll`.
- [ ] **Step 6: Run** the audit class, the renamed tier B tests, and one tier A twin in the dev editor instance. Expected: PASS for the audits and tier B, and the tier A twin builds with the vendor VRChat passes running.
- [ ] **Step 7: Run** the full product assembly. Expected: 0 failed, the tier A category present in the discovered set, and the suite-start check from Task 4 green.
- [ ] **Step 8: Commit, only with explicit owner authorization.**

```bash
git add Tests/Editor/Build/AmusePlatformFinishPluginTests.cs Tests/Editor/Build/AlphaSeparationPreparationTests.cs Tests/Editor/Build/AmuseReportsSlotTests.cs Tests/Editor/Build/VersionConsentTests.cs Tests/Editor/Build/VendorVrChatTierGate.cs Tests/Editor/Build/PlatformProviderAuditTests.cs Tests/Editor/Build/VendorSuiteStartCheck.cs
git commit -m "test: split platform coverage into a dedicated tier and a vendor VRChat tier"
```

### Task 8: The event refusal reads registry provenance (stage 1, V3)

**Files:**
- Modify: `Editor/Host/CommittedControllerGraph.cs:136-161` (the provenance scan)
- Modify: `Editor/Build/AmusePlatformFinishPlugin.cs:199-204` and `:267-273` (the two retired-contract comments)
- Test: `Tests/Editor/Host/CommittedControllerGraphTests.cs`
- Modify: `Tests/Editor/Build/TransientUnlockTransformationTests.cs` (the event fixture comment at the planted event)

**Interfaces:**
- Consumes: `CommittedControllerGraph.Enumerate(GameObject avatarRoot, IPlatformAnimatorBindings bindings)` returning `CommittedControllerGraphResult` with `Refusal` of type `AvatarAnimationRefusal`, and the NDMF registry `ObjectRegistry.GetReference(UnityEngine.Object)` returning an `ObjectReference` whose `Object` yields the resolved original.
- Produces: no signature change. The scan per enumerated clip checks the committed clip and every distinct original the registry chain resolves to. Either carrying an animation event returns the existing `AnimationEventRefusal`, which is `AvatarAnimationRefusal.AnimationEventPresent`. The walk goes to a fixpoint with a visited set and a depth cap of 8, and the cap hit refuses with the same named refusal, because the existing comment at `Editor/Host/CommittedControllerGraph.cs:145-149` defines this refusal as the record that AMUSE cannot bound whether an event can execute.

RED state, already observed: `ClosePassAddsNoAvatarEntryWhenTheBarrierAlreadyRefused` at `Tests/Editor/Build/TransientUnlockTransformationTests.cs:1361` failed on 2026-10-24 with refusal `None` against expected `AnimationEventPresent`, because Modular Avatar virtualized the planted clip and NDMF's clone dropped the event (record section 4.3). Wrong implementation W1 reads only the committed clone's events and fails F1. Wrong implementation W2 resolves provenance outside the NDMF registry and fails both F2 fixtures.

- [ ] **Step 1: Write the failing chain test.** In `CommittedControllerGraphTests` add `EventRefusalSurvivesTwoHandRegisteredCloneHops`, marked `// --- Falsifier 2: a provenance read that reads only one registry hop where two plugins cloned in sequence misses the authored event. ---`. Build an authored `AnimationClip` carrying one event through the existing `AttachEvents` helper, two clone clips, a controller whose layer motion is the second clone, and the `StubBindings` host. Inside `new ObjectRegistryGuard()` from `Tests/Editor/Shared/ObjectRegistryGuard.cs`, call `ObjectRegistry.RegisterReplacedObject(authored, cloneOne)` and then `ObjectRegistry.RegisterReplacedObject(cloneOne, cloneTwo)`, the commit direction old object first. Call `Enumerate` and assert `result.Refusal` equals `AvatarAnimationRefusal.AnimationEventPresent` and `result.Layers` is empty. No-op guard: the same fixture without the registrations returns `None` before the fix, which is the observed RED.
- [ ] **Step 2: Write the cap-hit witness.** In the same class add `EventRefusalOnAChainDeeperThanTheCapStillRefuses`, the witness for the depth-cap branch: nine registered hops from the authored clip to the committed clone, the event on the authored clip, and the same assertion of `AnimationEventPresent`. The authored original sits past the cap of 8, so a walk that stops at the cap without refusing returns `None` and fails this test.
- [ ] **Step 3: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Host.CommittedControllerGraphTests"]`. Expected: both new tests FAIL, and the existing `EventBearingClipReturnsAvatarRefusalAndNoPartialGraph` passes, because an identity-committed clip carries its event on the committed clip itself. That existing test is the second falsifier 2 witness and stays green through the change.
- [ ] **Step 4: Implement the provenance scan.** In the clip loop of `Enumerate`, after the committed-clip event check, resolve `ObjectRegistry.GetReference(clip)` and walk `.Object` while it moves, skipping visited objects, capped at depth 8, and return `AnimationEventRefusal` when any distinct original carries events. On cap hit return the same refusal. Cite V3 in the method comment.
- [ ] **Step 5: Revise the two production comments.** At `Editor/Build/AmusePlatformFinishPlugin.cs:199-204` and `:267-273` replace the pre-virtualization statements with the committed-graph-plus-provenance statement, each citing V3. Update the fixture comment at the planted event in `TransientUnlockTransformationTests` the same way.
- [ ] **Step 6: Run** the graph test class and `TransientUnlockTransformationTests`. Expected: PASS for the chain test, the cap-hit witness, the identity test, and `ClosePassAddsNoAvatarEntryWhenTheBarrierAlreadyRefused`.
- [ ] **Step 7: Run** the full product assembly. Expected: 0 failed. Record the counts in the run-record file.
- [ ] **Step 8: Commit, only with explicit owner authorization.**

```bash
git add Editor/Host/CommittedControllerGraph.cs Editor/Build/AmusePlatformFinishPlugin.cs Tests/Editor/Host/CommittedControllerGraphTests.cs Tests/Editor/Build/TransientUnlockTransformationTests.cs
git commit -m "feat: read animation event provenance through the NDMF registry"
```

### Task 9: VRChat-shaped descriptors and the stale comment (stage 1)

**Files:**
- Modify: `Tests/Editor/Build/MergedConsumptionFixture.cs:270-291` (the base layer fixture)
- Modify: `Tests/Editor/Build/AlphaSeparationApplyTests.cs:332-335` (the stale comment)

**Interfaces:**
- Produces: `AttachAnimationFixture` materializes all five `VRCAvatarDescriptor.AnimLayerType` base entries, Base through FX, exactly as the VRChat SDK default fills a real descriptor. Base keeps the fixture controller. The other four carry the SDK-default empty layer with a null controller and `isEnabled` false. The existing `specialAnimationLayers` fallback precedent at `:283-291` stays.

The defect of record section 4.2: the fixture set a single Base layer with no FX entry. NDMF filled its controller map only from the listed layers, and Modular Avatar read FX without a presence check, so 20 ambient tests failed with the dictionary view exception. A real descriptor lists every base layer.

- [ ] **Step 1: Apply the fixture fix.** RED is dated: the 20 ambient tests failed on 2026-10-24. No new test is needed, the 20 tests are the falsifier.
- [ ] **Step 2: Refresh the stale comment.** At `Tests/Editor/Build/AlphaSeparationApplyTests.cs:332-335` the comment says the public project installs no VRChat SDK, so the live marker route is unreachable. The SDK is installed in this environment. The comment becomes: the marker stub gives the suite exact control over which clips count as special motions, and the live marker route is exercised by the survival audit of stage 1b. State the fact, cite no decision ID, keep it to short sentences.
- [ ] **Step 3: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests"]`. Expected: 0 failed, the 20 tests of record section 4.2 green.
- [ ] **Step 4: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests"]`. Expected: 0 failed.
- [ ] **Step 5: Commit, only with explicit owner authorization.**

```bash
git add Tests/Editor/Build/MergedConsumptionFixture.cs Tests/Editor/Build/AlphaSeparationApplyTests.cs
git commit -m "test: give ambient fixtures the full VRChat-shaped base layer set"
```

### Task 10: Stage 1 exit validation (stage 1)

**Files:**
- Modify: `docs/superpowers/investigations/2026-10-25-vendor-integrated-test-run-records.md`

- [ ] **Step 1: Run** the full two-assembly suite in the dev editor instance. Expected: a green run, or a named skip list plus a green remainder. The exit rule of spec stage 1. The completed count must equal the discovered count.
- [ ] **Step 2: Append** the dated run block with the counts, the durations, the gates set, the excluded fixtures, and the per-test skip reasons from the Task 6 report.
- [ ] **Step 3: Commit, only with explicit owner authorization.** Stage the run-record file only.

```bash
git add docs/superpowers/investigations/2026-10-25-vendor-integrated-test-run-records.md
git commit -m "docs: record the stage 1 exit run"
```

### Task 11: Marker-clip observation attribution (stage 1b, V9 item 2)

**Files:**
- Create: `docs/superpowers/investigations/2026-10-25-marker-clip-observation-attribution.md`

Non-test task. It attributes which vendor pass removes the marker-clip observation of record section 4.3, third row.

- [ ] **Step 1: Run the bisect.** For each of Modular Avatar and AAO first, then every other installed vendor with an NDMF plugin, run `AlphaSeparationApplyTests.MarkerClipRefusalIsSlotLocalAndWritesNothingForIt` at `Tests/Editor/Build/AlphaSeparationApplyTests.cs:1930` once with that vendor's plugin-disable key set through `VendorSessionStateGuard.DisablePluginForFixture`, one vendor at a time, and record which disabling restores the marker refusal count of 1. No test code changes.
- [ ] **Step 2: Write the record.** Date it. State the refusing pass, the evidence, and the scoping consequence for Task 12. Restore every key through the guard.
- [ ] **Step 3: Commit, only with explicit owner authorization.** Stage the record file only.

```bash
git add docs/superpowers/investigations/2026-10-25-marker-clip-observation-attribution.md
git commit -m "docs: attribute the marker clip observation loss"
```

### Task 12: Falsifier survival audit (stage 1b, V8)

**Files:**
- Modify: this plan document (the verdict table below Task 12)
- Modify: the dedicated-platform fixture files the audit scopes or arms

**Interfaces:**
- Consumes: `VendorSessionStateGuard.DisablePluginForFixture` from Task 5, and the attribution record from Task 11.
- Produces: one verdict per numbered falsifier in the dedicated-platform suites, as the table appended below, and the named survival assertions or scoping changes the verdicts call for.

The rule of V8: for each numbered falsifier in the about 150 dedicated-platform tests that run with the all-platform Modular Avatar passes active, the audit lands exactly one of two verdicts. A survival verdict adds an assertion that the planted state reaches the targeted pass, for example that the captured evidence contains the planted binding, green after the fix and RED against a fixture whose planted state a vendor pass destroyed. A scoping verdict sets the NDMF plugin-disable key of the vendor whose pass destroys the planted state by design, through the guard, with the V7 restore discipline. A catch-all around vendor failures is never a verdict (F11, W10).

- [ ] **Step 1: Enumerate.** Grep `--- Falsifier` across `Tests/Editor/` and list every falsifier whose fixture runs on a dedicated test platform. The ambient and direct-context fixtures are out of scope.
- [ ] **Step 2: For each listed falsifier, run its fixture once as is.** Expected green or the dated RED. A RED names the interaction.
- [ ] **Step 3: Land one verdict per falsifier.** Survival: add the named survival test next to the fixture, named for the planted state and ending with `ReachesTheTargetedPass`, with the captured-evidence assertion. Scoping: wrap the fixture body in the guard scope, note the vendor in the fixture comment, and add the scoped vendor's plugin id to `VendorSessionStateGuard.KnownPluginIds`, so the suite-start known set stays exact. No try and catch around the build.
- [ ] **Step 4: Verify the audit discipline.** Grep the touched fixture files for `catch` in the changed hunks. Expected: no new catch around a vendor build. This is the F11 check.
- [ ] **Step 5: Run** the touched classes. Expected: 0 failed, every survival assertion green.
- [ ] **Step 6: Append the verdict table** to this plan under Task 12, one row per falsifier with the verdict, the named test or the scoped vendor, and the date 2026-10-25 or the true date.
- [ ] **Step 7: Commit, only with explicit owner authorization.** Stage this plan and the touched fixture files.

```bash
git add docs/superpowers/plans/2026-10-25-vendor-integrated-tests.md
git commit -m "docs: record the falsifier survival audit"
```

Verdict table (filled by Step 6):

| Falsifier | Verdict | Named test or scoped vendor | Date |
|---|---|---|---|
| `AlphaSeparationApplyTests` falsifier 1, wholly opaque slot | Survival | `WhollyOpaqueSlotReplacesOnlyItsMaterial`. The `CreatedClones` count of 1 and `AppliedOpaqueTriangleCount` of 1 pin the planted conversion through the passes. | 2026-10-26 |
| `AlphaSeparationApplyTests` falsifier, prepared under a block then the block is removed | Survival | `CandidatePreparedUnderABlockingProofDoesNotApplyAfterTheBlockIsRemoved`. The `RuntimeMaterialValueNotMapped` count of 1 pins the prepared candidate. | 2026-10-26 |
| `AlphaSeparationApplyTests` falsifier, property block added after the swap-in | Survival | `PropertyBlockAddedAfterSwapInRefusesAtApply`. The fixture guard asserts the exact state string `pairs=1 slotIsLocked=False blocked=1 applied=0`. | 2026-10-26 |
| `AlphaSeparationApplyTests` falsifier, renderer reparented between passes | Survival | `ARendererReparentedBetweenBarrierAndApplyRefusesEverySlotOfTheRenderer`. The `RendererChangedSincePreparation` count of 1 pins the moved renderer. | 2026-10-26 |
| `AlphaSeparationApplyTests` falsifier, stale target or acceptance | Survival | `DesktopAcceptanceRevokedBetweenPrepareAndApplyRefusesEveryPreparedSlot`. The `SamplingPolicyChanged` count of 1 and the unchanged committed curve values pin the stale candidate. | 2026-10-26 |
| `AlphaSeparationApplyTests` falsifier 6, swap values map at identical times | Survival | `EveryMaterialSwapValueMapsAtIdenticalKeyframeTimes`. The committed curve times equal 0, 0.25, and 1.5, and every value is the planted clone. | 2026-10-26 |
| `AlphaSeparationApplyTests` falsifier 7, unmapped live value | Survival | `UnmappedLiveValueInvalidatesOnlyItsSlot`. The refusal count of 1 beside the applied sibling pins both planted slots. | 2026-10-26 |
| `AlphaSeparationApplyTests` falsifier 8, marker clip refusal | Survival | `MarkerClipRefusalIsSlotLocalAndWritesNothingForIt`. The `MarkerClipCarriesSlotBinding` count of 1 is the survival assertion of the Task 9 repair, read from the stored pre-scope graph. No scoping verdict, per the attribution record section 6. | 2026-10-26 |
| `AlphaSeparationApplyTests` falsifier 20, live current material | Survival | `UnmappedReplacementBetweenPassesIsPreservedEntirely`. The refusal count of 1 and the untouched foreign replacement pin the planted live state. | 2026-10-26 |
| `AlphaSeparationApplyTests` falsifier 4, planned split invalidated late | Survival | `PlannedSplitInvalidatedLateSweepsItsCloneWithoutAnyWrite`. The destroyed orphaned clones and the `RuntimeMaterialValueNotMapped` count of 1 pin the invalidated split. | 2026-10-26 |
| `AlphaSeparationApplyTests` falsifier 15, the sweep destroys exactly the unreferenced | Survival | `SweepDestroysExactlyTheUnreferencedClones`. Exactly the abandoned clone is destroyed and the referenced clone stays alive. | 2026-10-26 |
| `AlphaSeparationApplyTests` falsifier 17, every slot validated before any mutation | Survival | `EverySlotIsValidatedBeforePrepareReturnsAndEveryDistinctReasonIsRecorded`. Both planted refusal reasons record the count 1. | 2026-10-26 |
| `AlphaSeparationPersistenceTests` falsifier 16, assigned generated objects persist | Survival | `AssignedGeneratedObjectsPersistThroughNdmfSerialization`. The `CreatedClones` count of 3, `AppliedRendererCount` of 1, and the persistent curve-only clone pin the planted lifecycle. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 12, AlreadyOpaque maps without a clone | Survival | `AlreadyOpaqueMapsToItselfWithoutAClone`. The planted material maps to itself by reference. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 3, no split anywhere | Survival | `NoSplitAnywhereCreatesNoMeshClone`. The retained-candidate precondition fires first, then every `MeshClone` reads null. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 11, conversion-only animation | Survival | `ConversionOnlyAnimationAwayFromDefaultRefusesAndToDefaultPrepares`. The `ConversionStateNotAdmitted` count of 1 sits beside the converting baseline. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 3, slot twin narrowing | Survival | `ConversionStateOnOverrideLayerBesideAdditivePoseLayer_NarrowsRefusal`. The no-op guard counts the additive clip, then the refusal count of 1 pins the Override conversion state. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 9, mixed Poiyomi and lilToon slot | Survival | `MixedPoiyomiAndLilToonSlotMapsCompletely`. The mapping holds both planted values, one through a generated clone. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 4, the all-fail case | Survival | `AllFailSlotReportsOneEntryNamingTheLastRefusingMaterial`. The `OpaqueConversionRefused` count of 1 and the entry naming the last planted material pin the all-fail branch. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 10, Poiyomi beside an opaque lilToon | Survival | `PoiyomiSlotPreparesBesideAnOpaqueLilToonSlotOnTheSameRenderer`. Both planted slots prepare and exactly one clone is created. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 1, unproven region | Survival | `PremultiplyConversion_WithUnprovenTriangle_StaysRefused`. The opaque ordinals equal 0 and the transparent ordinals equal 1. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 2, disclosure count | Survival | `PremultiplyDisclosure_FiresOncePerPreparedSlot`. Exactly one normalization sentence reports the planted premultiply state. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 4, per-slot dedup | Survival | `ForcedOpaqueReport_FiresOncePerFoldedSubmeshWithItsOwnCount`. Exactly two forced events carry the planted distinct counts. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 7, fold-time emission | Survival | `ForcedOpaqueReport_MarkerClipRefusalSuppressesTheForcedEvents`. The `MarkerClipCarriesSlotBinding` count of 1 sits beside zero forced events. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier, dissolve refusal report | Survival | `DissolveRefusalReportNamesMaterialPathAndFeature`. The `AdmittedMaterialSemanticsUnknown` count of 1 and the line naming the planted path and feature pin the refused slot. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier, unattested shader report | Survival | `UnsupportedShaderRefusalReportNamesMaterialPathAndShader`. The refusal line names the planted material path and the shader no frontend attests. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier, enabled second main texture layer | Survival | `EnabledSecondLayer_MaterialPreparesAndSplits`. The opaque candidate count of 1 and the prepared separation pin the layered material. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier, layer UV mode | Survival | `SecondLayerAtMeshUvMode_ProvesThroughNamedChannel`. The opaque candidate count of 1 proves the triangle through the named channel, not UV0. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier, no-alpha report | Survival | `NoAlphaSourceTexture_ReportsTheMistakeAndStillSplits`. The theorem proves the planted triangle and the report names `_MainTex`. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier, retained distance fade | Survival | `TransparentDistanceFadeStrengthRefusesTheSlotAsRetainedByFeature`. The slot refuses as `AdmittedMaterialRetainedByFeature` with the `_DistanceFade` reason. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier, DXT1 sRGB white region | Survival | `Dxt1SrgbMask_WhiteRegionTriangleProvesOpaque`. The DXT1 format precondition and the opaque candidate count of 1 pin the planted compressed mask through the barrier. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier, DXT1 sRGB seam blend | Survival | `Dxt1SrgbMask_SeamBlendTriangleStaysUnproven`. The resolved-materials precondition and the candidate count of 0 pin the planted seam domain. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 3, preserve at threshold | Survival | `PreserveFold_AtThreshold_StaysSplit`. The plan entry stays Split with ordinals 0 and 1, and the plan still requires a split. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 5, unchanged rides along | Survival | `PreserveFold_UnchangedSibling_StaysUnchangedBesideTheFoldedSlot`. The sibling folds while the unchanged entry keeps its ordinal on the original material. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 1, strict numerator | Survival | `PreserveFold_UnknownHeavySubmesh_DoesNotQualify`. The transparent ordinal bucket holds exactly 99 triangles, pinning the planted unknown-heavy share. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 2, per-submesh fold | Survival | `PreserveFold_MixedSlot_FoldsOnlyTheQualifyingSlot`. Slot A folds all 50 ordinals while slot B keeps 25 opaque and 25 transparent. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier 8, coverage selection by membership | Survival | `PoiyomiSiblingSlotsSelectDifferentPairs`. The cutout preset slot folds under the cutout pair while the plain sibling stays a split with one and one ordinals. | 2026-10-26 |
| `AlphaSeparationPreparationTests` falsifier, anisotropic slot silence | Survival | `UnchangedSlotWithAnisotropicSampling_RecordsAnisotropyDifferencesRefusal`. The `AnisotropyDifferences` count of 1 pins the planted mixed half field under the conservative policy. | 2026-10-26 |
| `AlphaSeparationSplitTests` falsifier 2, the mixed split slot | Survival | `SplitSlotRetainsTransparentTriplesAndAppendsOpaqueTriples`. The exact retained and appended index triples pin the planted split. | 2026-10-26 |
| `AlphaSeparationSplitTests` falsifier 5, opaque survives a refused sibling | Survival | `WhollyOpaqueSurvivesWhileItsSplitSiblingRefuses`. The `OpaqueConversionRefused` count of 1 sits beside the applied surviving slot. | 2026-10-26 |
| `AlphaSeparationSplitTests` falsifier 14, mesh and submesh bounds | Survival | `MeshAndEverySubmeshBoundsSurviveSplitFinalization`. The authored bounds survive finalization of the planted split. | 2026-10-26 |
| `AlphaSeparationSplitTests` falsifier 18, deterministic appended indices | Survival | `TwoSurvivingSplitSlotsAppendDeterministicallyAndMatchBindingsByIdentity`. The deterministic appended indices and the unchanged authored curve digests pin both planted slots. | 2026-10-26 |
| `AmusePlatformFinishPluginTests` falsifier 3, grant by shader name | Survival | `AGrantedSwapOnlyShaderNameAdmitsTheSwapMaterialThroughTheTransferredCapture`. Two consent subjects and the opaque candidate count of 2 pin the granted shaders. | 2026-10-26 |
| `AmusePlatformFinishPluginTests` falsifier 3, emitting on a resolving renderer | Survival | `RendererRefusalFactsStaySilentOnResolvingRenderers`. The exact baseline entry set of the planted texture key pins the resolving renderer. | 2026-10-26 |
| `AmusePlatformFinishPluginTests` falsifier 1, the deduped list | Survival | `ResolutionRefusedSlotReportsOneEntryPerAdmittedMaterial`. Two entries name both planted refusing materials. | 2026-10-26 |
| `AmusePlatformFinishPluginTests` falsifier 2, indexing AdmittedMaterials | Survival | `ResolutionRefusedEntryNamesTheAdmittedIndexMaterial`. The entry names the planted offender at the admitted index. | 2026-10-26 |
| `AmusePlatformFinishPluginTests` falsifier 1 companion, all-slots-failed batch | Survival | `RuntimeStateProductionEntry_AllSlotsFailedBatchStillRefusesTheRenderer`. The renderer refusal count of 1 and the analyzed count of 0 pin the failed batch. | 2026-10-26 |
| `TransientUnlockSwapInTests` falsifier, shared locked material block | Scoping | `com.anatawa12.avatar-optimizer`. The existing guard scope of the transient unlock lifecycle covers the fixture, because the Avatar Optimizer passes rewrite the planted locked material state and clips before the window opens. | 2026-10-26 |
| `TransientUnlockSwapInTests` falsifier, curve on a renamed name | Scoping | `com.anatawa12.avatar-optimizer`. The existing guard scope of the transient unlock lifecycle covers the fixture. The `LockedMaterialAnimationMismatch` count of 2 and the clean swap guard pin the planted curve. | 2026-10-26 |
| `TransientUnlockSwapInTests` falsifier, binding on another renderer path | Scoping | `com.anatawa12.avatar-optimizer`. The existing guard scope of the transient unlock lifecycle covers the fixture. The asserted `LockedMaterialPropertyBlock` count of 1 pins the vendor animator rebind state at the gate, attributed by the writer diagnosis. | 2026-10-26 |
| `TransientUnlockTransformationTests` falsifier 5, avatar refusal reported once | Survival | `ClosePassAddsNoAvatarEntryWhenTheBarrierAlreadyRefused`. The `AnimationEventPresent` refusal fires through the registry provenance of V3 while the Modular Avatar clone pass runs, and the barrier entry count of 1 is the no-op guard. | 2026-10-26 |

Audit notes, 2026-10-26:

- Enumeration. A mechanical grep of `--- Falsifier` over the seven files counts 57 hits: apply 12, persistence 1, preparation 28, split 5, platform finish 7, swap-in 3, transformation 1. One split-file hit is the falsifier-infrastructure banner above the fixture scope, not a marker of a test, so the seven files hold 56 markers. Five markers sit on direct-context fixtures that build no avatar, so they are out of scope by the brief. They are the two runtime-state integration markers and the three Multi transport markers. The table holds the remaining 51 verdict rows, one per in-scope marker. Ambient and other direct-context suites are out of scope.
- Vendor basis. The clean baseline row of the attribution record section 5 lists the active passes on the apply platform. They are the main Modular Avatar `Clone animators` pass, both late Modular Avatar passes, and the Avatar Optimizer passes. Verdicts therefore name plugin ids, never single passes, and every scope opens and closes through the guard.
- Audit outcome. No fixture file changed. Every survival verdict names an assertion the fixture already carries, and each assertion reads the planted state in captured evidence, so a destroyed planted state turns it RED. The one scoped vendor was already scoped through the guard, so `VendorSessionStateGuard.KnownPluginIds` stays unchanged. No catch was added anywhere.
- Observed runs of 2026-10-26, EditMode, all green as is. Apply, persistence, and split classes together: 63 passed of 63. Preparation class: 93 passed of 93. Platform finish, swap-in, and transformation classes together: 155 passed of 155. The eight preparation fixtures that joined the table after the review ran again by name: 8 passed of 8. Zero failed and zero skipped in each run. No dated RED exists for any listed falsifier.

### Task 13: The AAO atlas admission drops the version pin (stage 2, V6, V10 item 4)

**Files:**
- Modify: `Editor/Semantics/AaoAtlasTextureAttestation.cs:28-102` (the version pin, the `Versions` field, and the three-conjunct shape predicate) and `:107-127` (the `TryIdentifyAtlas` entry that calls the shape predicate)
- Modify: `Editor/Build/AmuseSessionCaches.cs:34` (delete the `AaoAtlasTextureAttestation.Versions.ClearVersionCacheForSession()` line, comment count from eight to seven)
- Modify: the seven test files that set or read `AaoAtlasTextureAttestation.Versions`. On 2026-10-25 the seam comment names six of them: `AaoAtlasTextureAttestationTests`, `GeneratedTextureAttestationTests`, `UnityTextureEvidenceTests`, `UnityAlphaFieldEvidenceTests`, `UnityGeneratedTextureEvidenceTests`, `UnityMaterialEvidenceCaptureTests`. The compiler enumerates the rest.
- Test: `Tests/Editor/Build/AaoMergedConsumptionTests.cs` (the real-atlas ground check)

**Interfaces:**
- Consumes: `AaoAtlasTextureAttestation.TryIdentifyAtlas(Texture2D texture, Material originMaterial)` and the generated capture route through `GeneratedTextureAttestation.TryIdentifyProducer`.
- Produces: `TryIdentifyAtlasShape` keeps conjunct 1, a live `Texture2D` with no asset path, conjunct 2, the pinned name markers, and conjunct 4, the registry corroboration one hop to an asset-backed material. Conjunct 3, the admitted version, is gone with `ProductionAdmittedVersions`, `AaoAtlasTextureAttestation.Versions`, and the session-cache line.

Owner decision Q8, 2026-10-25: the atlas admission stops checking the AAO version. The installed AAO 1.9.20 fails the pin, so no real atlas enters the admission in this environment (record section 6.3). The pinned name markers stay exact: the suffix ` (AAO UV Packed)` and the prefix `AAO Monotone `.

- [ ] **Step 1: Write the failing ground check.** In `AaoMergedConsumptionTests` add `RealAaoAtlasIsAdmittedThroughTheGeneratedRoute`, the V10 item 4 ground check. The fixture runs the existing ambient build with `ConfigureTraceAndOptimize(optimizeTexture: true)` over one renderer, then asserts `TryIdentifyAtlas` accepts the produced atlas whose name carries the suffix and that the generated capture route admits it. Expected before Step 3: FAIL, because conjunct 3 refuses the installed 1.9.20. Record the RED.
- [ ] **Step 2: Name the stay-green witnesses.** `CorroborationRefusesAnUnregisteredOriginMaterial` at `Tests/Editor/Semantics/AaoAtlasTextureAttestationTests.cs:193` keeps failing an unregistered slot material, which is falsifier 6. `PersistedCompressedCopyInDerivedContainer_IsRefused` at `Tests/Editor/Host/GeneratedTextureAttestationTests.cs:226` keeps refusing a forged foreign texture, which is falsifier 5. Both run in Step 5 and must stay green.
- [ ] **Step 3: Delete the seam.** Remove conjunct 3, the table, the `Versions` field, and the cache line. Follow the compiler: delete the version-seam setups and the conjunct 3 refusal pins in the touched test files. The corroboration and forge refusal tests stay.
- [ ] **Step 4: Run** `run_tests` EditMode over the touched classes. Expected: PASS, the ground check green.
- [ ] **Step 5: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Host.GeneratedTextureAttestationTests"]` and the AAO attestation class. Expected: 0 failed, the two witnesses green.
- [ ] **Step 6: Commit, only with explicit owner authorization.** Stage the two production files, the ground-check test, and every touched version-seam test file explicitly. The seam comment names six of them on 2026-10-25 and the compiler enumerates the rest.

```bash
git add Editor/Semantics/AaoAtlasTextureAttestation.cs Editor/Build/AmuseSessionCaches.cs Tests/Editor/Build/AaoMergedConsumptionTests.cs
git commit -m "feat: admit AAO atlases by shape and corroboration without a version pin"
```

Commit note: the block stages the explicit-path minimum. Add the touched files under `Tests/Editor/Host/` and `Tests/Editor/Semantics/` that read the deleted `AaoAtlasTextureAttestation.Versions` seam to the same authorized commit, exactly as Tasks 15 and 16 stage their enumeration-dependent sets.

### Task 14: Real-source ground checks and the source-scan tripwire (stage 2, V10)

**Files:**
- Test: `Tests/Editor/Semantics/RealSourceGroundCheckTests.cs` (new)
- Test: `Tests/Editor/Build/VendorLockToolSourceBanTests.cs` (new)

**Interfaces:**
- Consumes: `LilToonSourceAttestation` pinned name and GUID constants, `PoiyomiSourceAttestation.CanonicalShaderGuid` and `TwoPassCanonicalShaderGuid`, the production attestation entry points, and the source-scan style of `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/ResearchSourceApiBanTests.cs`.
- Produces: one admitted ground check per attested family, one real unattested 10.x answer, and the source-scan tripwire.

Loading by pinned GUID is mandatory (V10.1): on a 10.x install the root Poiyomi shader carries the 9.3 release name, so `Shader.Find` by name returns the wrong shader (record section 6.2). The lilToon families come from the record section 6.1 nine pairs: the main shader and pass shader through `SupportedShaderName` and `PassShaderName`, the cutout pair, the transparent pair, the opaque target resolved through `ResolveCanonicalTargetShaderName`, and both Multi containers through the installed container GUID constants in `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:220-227`. The opaque target GUID is pinned as a test-local constant measured from the installed `.meta` file, following the installed container constant precedent.

- [ ] **Step 1: Write the tests.** In `RealSourceGroundCheckTests`, one test per lilToon family: `RealLilToonMainAndPassShadersAttestThroughProduction`, `RealLilToonCutoutShadersAttestThroughProduction`, `RealLilToonTransparentShadersAttestThroughProduction`, `RealLilToonOpaqueTargetAttestsThroughProduction`, and `RealLilToonMultiContainersAttestThroughProduction`. Each loads every shader of the family by pinned GUID with `AssetDatabase.GUIDToAssetPath`, asserts the load is non-null, and runs the production attestation end to end. These are characterization on first run: the expected result in the pinned environment is PASS, and a failure is a dated drift finding, never a staged RED. Each carries the assertion `Shader.Find(<name>)` resolves to the same object as the GUID load for that family, or, where a stand-in or a foreign shader shares the name, that the two loads disagree. Add `NameLookupCanDisagreeWithThePinnedGuidLoad`, marked `// --- Falsifier 10: a ground check that resolves the shader by release name loads the wrong bytes on a 10.x install. ---`: it asserts `Shader.Find(".poiyomi/Poiyomi Toon")` and the GUID load of `CanonicalShaderGuid` are different objects on this install. Add `RealPoiyomiBundledShadersAttestThroughProduction` for the two bundled 9.3 rows, and `RealPoiyomiTenMaterialAnswersUnattested`: a material on the 10.x root shader answers unattested through production selection, the strongest available drift falsifier.
- [ ] **Step 2: Write the tripwire test.** In `VendorLockToolSourceBanTests` add `ProductionSourceNeverCallsTheVendorLockTool`. The test scans every `.cs` file under `Editor/` for the vendor qualified type name `Thry.ThryEditor.ShaderOptimizer` and fails naming any file. It matches the qualified type name, never a name substring, because AMUSE property names contain the same word. The test-local stand-in type under `Tests/` is out of scope until Task 18 deletes it.
- [ ] **Step 3: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Semantics.RealSourceGroundCheckTests", "Alrauna.Amuse.Tests.Editor.Build.VendorLockToolSourceBanTests"]`. Expected: PASS in the pinned environment. A lilToon digest mismatch here is the digest-equality answer of V10.1: record it as a dated drift finding in the run-record file, never as a skip, and stop for an owner decision, because it invalidates a pinned row.
- [ ] **Step 4: Record** the lilToon digest-equality outcome in the run-record file with the date.
- [ ] **Step 5: Commit, only with explicit owner authorization.**

```bash
git add Tests/Editor/Semantics/RealSourceGroundCheckTests.cs Tests/Editor/Build/VendorLockToolSourceBanTests.cs docs/superpowers/investigations/2026-10-25-vendor-integrated-test-run-records.md
git commit -m "test: attest the installed shader sources by pinned GUID"
```

### Task 15: Real lilToon replaces stand-in happy paths (stage 3, V11)

**Files:**
- Create: `docs/superpowers/investigations/2026-10-25-real-material-cutover-ledger.md`
- Modify: the Build, DryRun, and Semantics fixture files the ledger enumerates

**Interfaces:**
- Consumes: the ground checks of Task 14, the stand-in inventory of record section 5.2, and the synthetic-retention list of record section 5.3.
- Produces: the cutover ledger and the real-material twins it names.

The retirement rule (V11): each retired stand-in test names its replacement test, or stays synthetic with a stated reason from record section 5.3. The exact keyword control at `Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs:138-302` and the refusal shapes no vendor ships at `Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs:715-841` stay synthetic. The pinned name and GUID pairs of lilToon 2.4.1 all match the installed package (record section 6.1).

- [ ] **Step 1: Enumerate.** Grep the lilToon stand-in shader names `Hidden/Alrauna/AmuseTests/` and `VerifiedLilToonTestSeams` across `Tests/Editor/`. List every happy-path test whose input a real installed material can supply. Falsifier fixtures and refusal shapes are not happy paths.
- [ ] **Step 2: Write the twins.** For each listed test add the real-material twin: a material on the GUID-loaded installed shader of the right family, driven through the same production path, with the same assertion. Twin method name: the retired name plus `WithRealLilToon`. Mark each twin with a comment naming the retired test it replaces.
- [ ] **Step 3: Run** the touched classes. Expected: twins PASS, 0 failed.
- [ ] **Step 4: Retire.** Delete each minted stand-in happy-path test and the stand-in shaders only it used, when its ledger row names the replacement. Keep every synthetic with its stated reason. Clean cutover, no dead fixtures left.
- [ ] **Step 5: Run** the full product assembly. Expected: 0 failed, no test count loss beyond the retired duplicates, and every retired test named in the ledger.
- [ ] **Step 6: Write the ledger.** One row per retired or retained stand-in test: the old name, the replacement name or the retention reason, and the date.
- [ ] **Step 7: Commit, only with explicit owner authorization.**

```bash
git add docs/superpowers/investigations/2026-10-25-real-material-cutover-ledger.md
git commit -m "test: replace lilToon stand-in happy paths with real materials"
```

Stage 3 commit note: stage the ledger plus the touched fixture and shader files explicitly. The Step 7 block stages the ledger only because the touched set is enumeration-dependent.

### Task 16: Real Poiyomi bundled 9.3 replaces its stand-ins (stage 4, V12)

**Files:**
- Modify: `docs/superpowers/investigations/2026-10-25-real-material-cutover-ledger.md`
- Modify: the fixture files the ledger enumerates

**Interfaces:**
- Consumes: `PoiyomiSourceAttestation.CanonicalShaderGuid` and `TwoPassCanonicalShaderGuid`, whose bundled 9.3 rows match the installed 10.0.27 package by name and GUID (record section 6.2).
- Produces: the bundled 9.3 cutover and the recorded import and compile cost.

Owner decision A3b, 2026-10-25: release package testing is dropped. The bundled 9.3 shader in Poiyomi 10 is the primary support path. No stage tests the 9.3.64 release package, which is not installed. The two release rows stay in `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:110-121`. Locked reconstruction tests stay synthetic in this environment, because the bundled rows carry `LockedOriginalAdmitted = false`.

- [ ] **Step 1: Enumerate.** Same rule as Task 15 over the Poiyomi stand-ins, the locked stand-ins excluded.
- [ ] **Step 2: Write the twins and retire** per the ledger rule, loading by pinned GUID only.
- [ ] **Step 3: Measure and record the import and compile cost.** Time a forced synchronous reimport of the two bundled 9.3 shader files with `AssetDatabase.ImportAsset` and `ForceSynchronousImport`, and record the per-file and total wall time with the date in the ledger and the run-record file. The measurement is recorded data and never a gate (V5, V12). There is no threshold and no failure branch.
- [ ] **Step 4: Run** the touched classes and then the full product assembly. Expected: 0 failed.
- [ ] **Step 5: Commit, only with explicit owner authorization.** Stage the ledger plus the touched files explicitly.

```bash
git add docs/superpowers/investigations/2026-10-25-real-material-cutover-ledger.md docs/superpowers/investigations/2026-10-25-vendor-integrated-test-run-records.md
git commit -m "test: replace Poiyomi bundled 9.3 stand-ins with real materials"
```

### Task 17: Production-order fixtures (stage 5, V13 item 1, V5, V15)

**Files:**
- Create: `Tests/Editor/Build/VendorProductionOrderTests.cs`

**Interfaces:**
- Consumes: the SDK callback dispatcher pattern `VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks.OnPreprocessAvatar(root)` at `Tests/Editor/Build/DaoMergedConsumptionTests.cs:181-186`, the soundness oracle `AssertBuiltTrianglesMatchAuthoring` from `MergedConsumptionFixture`, the tier A category and gate helper from Task 7, and, through reflection only, the installed components `ModularAvatarMergeAnimator`, `VRCFury`, `Toggle`, and `ObjectToggleAction`.
- Produces: two tier A fixtures and their tier B twins.

Reflection rule. The test assembly references no Modular Avatar or VRCFury assembly, and the VRCFury model types are internal in the vendor assembly, so every vendor type resolves by assembly-qualified `Type.GetType`, exactly as `Tests/Editor/Build/MergedConsumptionFixture.cs:36-40` resolves d4rk with `"d4rkAvatarOptimizer, d4rkpl4y3r.d4rkavataroptimizer.Editor"`. The pinned resolution strings: `"nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator, nadena.dev.modular-avatar.core"` (assembly from `Packages/nadena.dev.modular-avatar/Runtime/nadena.dev.modular-avatar.core.asmdef`), `"VF.Model.VRCFury, VRCFury"` (assembly from `Packages/com.vrcfury.vrcfury/Runtime/VRCFury.asmdef`, whose name field is `VRCFury`), `"VF.Model.Feature.Toggle, VRCFury"`, and `"VF.Model.StateAction.ObjectToggleAction, VRCFury"`. A null resolution is a named `Assert.Ignore` that names the absent package and its locked version, per V1. Instances of the internal types come from `Activator.CreateInstance(type, nonPublic: true)`.

One dispatcher call runs the real registered callbacks in production order: NDMF part one, VRCFury, NDMF part two, then d4rkAvatarOptimizer when installed (record section 4.5). The fixtures sit in tier A: the dispatcher requires the VRChat SDK, so each fixture gates on SDK presence with the Task 7 helper and skips by name without it. They write no project-level state beyond the ambient NDMF generated output that the Task 5 cleanup removes, so they need no opt-in gate. Plan call under V15: if execution shows either fixture writing project-level state, that fixture moves behind a `VendorOptInGate.Require` variable named for it and this plan gets one added line, stated in the run record of the first run that finds it.

V15 call resolved on 2026-10-26: the dispatch runs VRCFury on every avatar, and VRCFury saves the controllers it rebuilds under `Packages/com.vrcfury.temp` during the build. Both tier A fixtures move behind `VendorOptInGate.Require(VendorOptInGate.ProductionOrderDispatch, disclosure)`, and the class teardown deletes the vendor build output folder.

- [ ] **Step 1: Write the tier A fixtures.** The names follow the Task 7 pairing rule, each tier B name plus `VendorVRChat`. `ModularAvatarMergeAnimatorKeepsTrianglesSoundVendorVRChat`: the standard fixture avatar with one real lilToon material, a reflected `ModularAvatarMergeAnimator` component merging one swap controller, built through the dispatcher, asserted with the soundness oracle. Survival assertion: the committed graph contains the merged layer, so a silent vendor skip cannot turn green. `VRCFuryToggleKeepsTrianglesSoundVendorVRChat`: the same avatar with a reflected `VRCFury` component carrying one reflected `Toggle` feature. The installed model shape lives at `Packages/com.vrcfury.vrcfury/Runtime/Model/Feature/Toggle.cs` and `Packages/com.vrcfury.vrcfury/Runtime/Model/StateAction/ObjectToggleAction.cs`. The fixture sets the feature fields by name through reflection: `name` to a fixture string, `defaultOn` to true, and `state.actions` to a list holding one `ObjectToggleAction` whose `obj` is the fixture renderer holder and whose `mode` is `TurnOn`. The feature attaches to the component's `[SerializeReference] public FeatureModel content` field, set by reflection, one feature per component, per the installed model comment at `Packages/com.vrcfury.vrcfury/Runtime/Model/VRCFury.cs:19-22`. The obsolete `config` field stays untouched. The build then runs through the dispatcher and asserts with the soundness oracle. Survival assertion: the build output carries the VRCFury-generated layer or parameter.
- [ ] **Step 2: Write the tier B twins.** `ModularAvatarMergeAnimatorKeepsTrianglesSound` and `VRCFuryToggleKeepsTrianglesSound` inject the equivalent animation input by hand through `AlphaSeparationTestLifecycle.NewSwapClip`, because VRCFury runs only through SDK callbacks. Both carry the soundness oracle and the survival assertion against the hand-built input. Every tier A assertion has its tier B twin, and the strict-suffix audit of Task 7 pairs each tier A name with its tier B base name.
- [ ] **Step 3: Run** the class with the SDK installed. Expected: tier A builds run the full production order and PASS, twins PASS.
- [ ] **Step 4: Run** the class from Task 7's audit. Expected: `EveryTierAAssertionHasATierBTwin` stays green.
- [ ] **Step 5: Commit, only with explicit owner authorization.**

```bash
git add Tests/Editor/Build/VendorProductionOrderTests.cs
git commit -m "test: drive vendor fixtures through the production callback order"
```

### Task 18: Real Thry lock characterization and the stand-in cutover (stage 5, V13 items 2 and 3, V10 item 3 cutover)

**Files:**
- Create: `Tests/Editor/Build/RealThryLockCharacterizationTests.cs`
- Delete: `Tests/Editor/Build/TransientUnlockVendorStandIn.cs` and its `.meta`
- Modify: the five files that read the counters on 2026-10-25: `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`, `Tests/Editor/Build/TransientUnlockAvailabilityTests.cs`, `Tests/Editor/Build/TransientUnlockTestLifecycle.cs`, `Tests/Editor/Build/TransientUnlockTransformationTests.cs`, `Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`

**Interfaces:**
- Consumes: `VendorOptInGate.Require(VendorOptInGate.RealThryLock, disclosure)` from Task 3, `LockedSourceFixture` at `Tests/Editor/Build/LockedSourceFixture.cs`, and the aligner `PoiyomiLockedSourceAlignment.Align`.
- Produces: the dated real-lock characterization bound to the installed vendor assembly, and the deleted stand-in.

Vendor binding rule, the chosen fix for the vacuous-lock risk: inside the test assembly the plain name `Thry.ThryEditor.ShaderOptimizer` binds to the test stand-in at `Tests/Editor/Build/TransientUnlockVendorStandIn.cs:8-41`, and the test assembly references no vendor assembly. The characterization therefore resolves the real tool by assembly-qualified reflection, `Type.GetType("Thry.ThryEditor.ShaderOptimizer, ThryAssemblyDefinition")`, because the installed thryeditor Editor folder ships `Packages/com.poiyomi.thryeditor/Editor/ThryAssemblyDefinition.asmdef` whose name field is `ThryAssemblyDefinition`, verified on 2026-10-25. The no-op guard asserts the resolved type is non-null, its `Assembly` is not the test assembly, and its assembly name is `ThryAssemblyDefinition`, so a resolution to the stand-in can never pass. Both vendor members are public static with the material list first, each with one optional second parameter: `LockMaterials(IEnumerable<Material> materials, ProgressBar progressBarType = ProgressBar.None)` at `<thryeditor>/Editor/ShaderOptimizer.cs:481` and `UnlockMaterials(IEnumerable<Material> materials, ProgressBar progressBarType = ProgressBar.None)` at `:493`. Reflection `Invoke` passes both arguments on every lock and unlock call. The second argument is the `None` member of the vendor nested enum `Thry.ThryEditor.ShaderOptimizer.ProgressBar` at `:459`, resolved through `Enum.Parse` on the reflected nested type, never through a locally declared enum, so the call reaches the vendor overload the build path uses.

The gate disclosure names both costs, so a runner opts in knowingly: the lock writes vendor-owned cache data under `Assets/_LockedShaderCache/` and calls the vendor usage counter over the network. The canonical validation run sets no real-lock gate (V15).

Cleanup order, load-bearing (V13.2): delete the test's own locked materials first, then the test's own entries under `Assets/_LockedShaderCache/`, because the vendor documents that deleting a cache entry other locked materials hold breaks those materials. The teardown deletes exactly the planner output in order, so the wrong implementation that deletes cache first is observable.

- [ ] **Step 1: Write the order tests.** `ResidueCleanupPlanDeletesMaterialsBeforeCacheEntries`, marked `// --- Falsifier 8: a teardown that deletes a shared cache entry before the test's own materials breaks that material. ---`. The cleanup is a pure planner `internal static IReadOnlyList<string> ResidueCleanupPlan(IReadOnlyList<string> materialPaths, IReadOnlyList<string> cacheEntryPaths)` returning the deletion order, and the test asserts every material path precedes every cache entry path. `TeardownAppliesThePlannerOutputInOrder` binds the teardown to the planner: the apply entry `internal static IReadOnlyList<string> ResidueCleanupApply(IReadOnlyList<string> materialPaths, IReadOnlyList<string> cacheEntryPaths, Action<string> delete)` computes the plan, invokes `delete` once per path in order, and returns the plan. The test installs a recording deleter, asserts the invoked order equals the plan, and the characterization teardown passes `AssetDatabase.DeleteAsset`. After both, the teardown asserts the cache folder holds no test residue.
- [ ] **Step 2: Write the characterization.** Under the gate, resolve the real tool through the vendor binding rule above and run the guard first. The real lock then locks a synthetic stand-in material written under the test temp folder, so no generated asset lands in the vendor tree. The test compares the real lock result with the `LockedSourceFixture` output through the aligner and records the outcome with the date. Expected on first run: PASS or a dated named difference, which is characterization either way.
- [ ] **Step 3: Run** the class with `AMUSE_REAL_THRY_LOCK=1` in the dev editor instance. Expected: the guard passes, the characterization runs, and the teardown leaves no residue. Then run the class without the gate. Expected: a named skip.
- [ ] **Step 4: Delete the stand-in.** Remove the type file and every counter assertion and `Reset()` call in the five files. The protection moves to the Task 14 source-scan tripwire. The V10.3 cutover lands here, after the real-lock characterization exists, which is why the deletion follows Steps 2 and 3 rather than preceding them.
- [ ] **Step 5: Run** the five touched classes and `VendorLockToolSourceBanTests`. Expected: 0 failed and the tripwire green.
- [ ] **Step 6: Commit, only with explicit owner authorization.**

```bash
git add Tests/Editor/Build/RealThryLockCharacterizationTests.cs Tests/Editor/Build/TransientUnlockVendorStandIn.cs.meta
git commit -m "test: characterize the real Thry lock and retire the stand-in counters"
```

Commit note: stage the deletion explicitly in the same authorized commit through `git rm` for `Tests/Editor/Build/TransientUnlockVendorStandIn.cs` and its `.meta`, plus the five touched files. The block above stages the new test and the `.meta` as the explicit-path minimum.

### Task 19: Documentation updates and final validation (stage 6, V14)

**Files:**
- Modify: `AGENTS.md:17` (the pass 1 sentence)
- Modify: `README.md:36` (the d4rk validation sentence)
- Modify: `docs/architecture/shader-frontend-comparison.md:74` and `:196-197` and `:243-247`
- Modify: `docs/development-setup.md`
- Modify: `.gitignore` and `Packages/.gitignore:12-13`
- Modify: `docs/superpowers/investigations/2026-10-25-vendor-integrated-test-run-records.md`

- [ ] **Step 1: AGENTS.md.** The sentence at line 17 says pass 1 runs extension-free on real authored clips before animator virtualization. Replace it with the committed-graph-plus-provenance statement citing V3. Short sentences.
- [ ] **Step 2: README.md.** Update the vendor statements. The sentence at `README.md:36` that combining AMUSE with d4rkAvatarOptimizer is not validated in 0.1.0 becomes the statement the gated tests support: the combined sequence is exercised by the opt-in gated integration tests in a disposable project, and the default run leaves it out by name. Keep the shader support statements aligned with the bundled 9.3 path.
- [ ] **Step 3: shader-frontend-comparison.md.** Align the three cited passages with the vendor-integrated reality: the stand-in fixture convention at line 74, the duplication rows at `:196-197`, and the fixture-shader guidance at `:243-247` now describe real-material twins with named synthetic retentions.
- [ ] **Step 4: development-setup.md.** Add the extra VPM repositories a fresh clone needs, the vendor global side effects, the VRCFury license note for commercial users with the official-channel statement, the two opt-in gates with the network disclosure for the real lock, and the duration goal that never gates.
- [ ] **Step 5: The two gitignore files.** The root comment says the vendor packages are installed for local testing only. The `Packages/.gitignore:12-13` comment says the same. Both describe the permanent dev environment after this rework and get current wording.
- [ ] **Step 6: Run** the full two-assembly suite. Expected: 0 failed, discovered equal to completed. Append the dated run block with counts, durations, gates set, excluded fixtures, and skip reasons. This is the canonical validation run of the rework. Duration is recorded, never judged.
- [ ] **Step 7: Run the identifier sweep** over every file this plan changed: an at sign joined to hexadecimal, drive-letter paths, home-directory paths, four-digit ports, and private names learned in the session. Expected: no hits. Fix every hit before Step 8.
- [ ] **Step 8: Commit, only with explicit owner authorization.** Stage the seven files explicitly.

```bash
git add AGENTS.md README.md docs/architecture/shader-frontend-comparison.md docs/development-setup.md .gitignore Packages/.gitignore docs/superpowers/investigations/2026-10-25-vendor-integrated-test-run-records.md
git commit -m "docs: align the repository documents with vendor-integrated testing"
```

## Plan notes

- Stages 1b tasks 11 and 12 may run in parallel with stage 2 tasks 13 and 14. Stage 5 depends on stage 1b and stage 2, because its soundness oracle reads ground-checked real sources. Stage 6 is last.
- A spec citation in V9.3 names the lilToon gate file under `Tests/Editor/Build/Semantics/LilToon/`. The checked-out file lives under `Tests/Editor/Semantics/LilToon/`. Task 3 uses the checked-out path. Plan call, not a silent fix.
- The V12 synthetic-lock choice reopens when the sibling admission plan's stage 7 flips the bundled rows. Until that flip this plan changes nothing about it. This is a note, not a task.
- Second revision choices, stated: the real-lock characterization binds to the installed vendor assembly through the Task 18 reflection guard, and the stand-in deletion stays after the characterization, because V10.3 requires the stand-in to survive until the characterization exists. The falsifier 13 source scan excludes its own file by name, because the falsifier comment quotes the banned words.
- Second revision citation checks that found no defect: the plan cites `ConsentTestGenericPlatform` at `:925-932`, which covers the class declaration at `:925` and its qualified name at `:930`, and the plan carries no citation for the vendor usage-counter line that the spec pins one line off.
