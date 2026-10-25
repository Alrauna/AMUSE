# Bundled Locked Original Admission Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Status: 2026-10-25, second revision. Three adversarial reviews of the first revision returned accept with fixes on 2026-10-25: a coverage review with 0 CRITICAL, 4 MAJOR, and 8 MINOR findings, a feasibility review with 1 CRITICAL, 4 MAJOR, and 5 MINOR findings, and cross-plan consistency items with 3 MINOR findings on this plan. This revision integrates them. A scoped re-review of this revision on 2026-10-25 returned accept with fixes with 0 CRITICAL, 1 MAJOR, and 1 MINOR. This revision integrates both.

**Goal:** Admit locked Poiyomi materials whose original is one of the two bundled 9.3 rows, after the block tie fix, the suffix fix, a measured real full swap-in per row, and the window and report pins. The flag change is the last code change.

**Architecture:** Stage order of the spec, one stage per task group. Stage 1 fixes the block tie as classes in the pure aligner, with a strict condition mode for zero-and-zero records. Stage 2 fixes the suffix run scanner, the suffix head boundary, and the Properties window counter. Stage 3 writes the release-row safety argument. Stage 4 runs a gated real-lock suite in the dev project and records times and refusal sentences. Stage 5 states the time budget. Stage 6 lands the pre-flip window and report pins. Stage 7 flips the flag and cuts over every surface.

**Tech Stack:** C# for Unity 2022.3, NUnit EditMode tests through Unity MCP `run_tests`, no new dependencies.

**Spec:** `docs/superpowers/specs/2026-10-25-bundled-locked-original-admission-design.md`. Decision IDs `Y1` to `Y13` and falsifier numbers refer to it. It amends `docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md` (decisions `A1` to `A12`) and decision `K8` of `docs/superpowers/specs/2026-10-23-poiyomi-per-shader-attestation-design.md`. Its basis is `docs/superpowers/investigations/2026-10-25-bundled-locked-original-readiness-investigation.md` (the record).

**Cross-plan order.** This plan and the vendor plan `docs/superpowers/plans/2026-10-25-vendor-integrated-tests.md` are siblings. Stages 1, 2, 3, 5, and 6 of this plan do not depend on the vendor plan. Stage 4 consumes vendor stage 1, which creates `Tests/Editor/Build/VendorOptInGate.cs` and lands the suite-start check. Stage 7 runs after stages 1 to 6. This plan executes on its own topic branch, created at execution time from `rework/vendor-integrated-tests` after vendor stage 1 lands. The plan never creates branches beyond this statement. After this plan's stage 7, the vendor plan's V12 synthetic-lock choice reopens. The vendor plan states that as its own note.

## Global Constraints

- All AMUSE paths below are relative to `Packages/com.alrauna.amuse/`, unless they start with `docs/` or `Packages/`.
- The aligner input stays exactly the original text, the generated text, and the original property table (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:185-188, 271-300`). The live values of the locked material never enter the aligner. This is A3.1 and it does not change.
- The aligner and the condition evaluator stay pure C# with no Unity object access.
- Fail closed. Unknown refuses. A refusal is a coverage loss, never a false positive.
- The A6 restore checks and the A8.3 seventh fact stay the soundness net for every record the aligner writes.
- No step refuses a material on elapsed time or on a step count. The only new cap is the Y2 end-of-the-original-file evidence cap. No time based refusal anywhere (spec non-goal).
- No material tag, material name, or defaults table is a truth source for a fact the generated text records.
- Never weaken, delete, skip, or rewrite a valid test to make the flag change pass. Tests that pin the old refusal are rewritten to pin the new decision, in the same change set as the flag (Y13).
- Owner item 4, the A10.4 curve rewrite, stays a recorded follow-up. This plan does not touch it.
- Simple English in every comment, test message, report string, record, and doc: short active sentences, one idea per sentence, no semicolons, no contractions. Owner decisions are paraphrased and never quoted.
- No machine path, host name, user name, port, GUID value, digest value, instance name, or private avatar or asset name in any file, comment, commit message, record, or test. No vendor shader text in tests. Synthetic shader texts only.
- One test class per production type, named `<Type>Tests`, in the folder that mirrors production. Method names are behavior sentences. Assertions use `Assert.That`.
- RED first: run each new test and observe the failure before the implementation. A test that passes on its first run is recorded as characterization, never dressed up as RED.
- Falsifiers are marked `// --- Falsifier N: ... ---` and carry a no-op guard that proves the fixture reaches the code path.
- Code comments that implement a decision cite `Y1` to `Y13` and point to the spec.
- Never stage or commit `Packages/manifest.json`, `Packages/packages-lock.json`, `Packages/vpm-manifest.json`, `ProjectSettings/*`, or `Assets/default.unity*`. Stage explicit paths only. Never push. Every commit in this plan happens only with explicit owner authorization.
- Unity runs only in the dev editor instance. Verify `Application.dataPath` of that instance matches the Assets folder of this repository before any MCP write or long call. Never run tests in the Census Lab project. A filtered run that reports 0 tests is a failure.
- After new files: refresh Unity and read the console for compile errors before the next step.
- Full validation runs both assemblies: `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor`. Task 1 records the baseline counts on their run date, before any code change. Never invent counts.
- Real vendor locks run only behind the `VendorOptInGate.RealThryLock` gate and only in the dev project (Y9).
- Every status claim in a record or comment carries its date.

## Review Focus

1. A tie block whose body text repeats later in the file. A person would expect the removal to be provable. The text cannot tell the two worlds apart, so the build must refuse undecided, never record `Kept`. Owner: Task 1, test `TieWhoseBodyIsDuplicatedAfterTheBlockRefusesUndecided` (Falsifier 1).
2. A kept wrapper whose inner removal emptied the body, with a real non-zero `Animated` tag on the condition property. A person would expect admission. The strict check must refuse at the condition check, because a relock of the clone would keep the body that the locked material never rendered. Owner: Task 3, test `KeptWrapperWithAnAnimatedTagRefusesUnderTheStrictCheck` (Falsifier 11).
3. A Properties display string with an unbalanced brace before a renamed Properties line. A person would expect the rename to resolve. The window must still yield the artifact. Owner: Task 7, test `DisplayStringWithUnbalancedBraceKeepsLaterPropertiesLines` (Falsifier 7).
4. A baked non-default literal such as `0.975` or `1E-06`, whose digit runs the original never holds as a whole token. A person would expect alignment. The run drop rule must remove digit-led runs, so alignment never refuses on them. Owner: Task 5, test `BakedDecimalAndExponentLiteralsDoNotBlockTheSuffix` (Falsifier 12).
5. A material name that embeds a declared property name, such as `Cutoff_X` or `Cut_Cutoff_On`. A person would expect one suffix. The head rule must derive the earliest boundary, and a wrong suffix must refuse at the walk, never accept. Owner: Task 6, tests `EmbeddedDeclaredNameSuffixDerivesFromTheEarliestBoundary` and `HeadAnchorsAtTheEarliestBoundaryNotALaterName` (Falsifiers 4 and 5).

---

### Task 1: Block tie M1 rule, the catch-up probe (Y2)

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs`
- Modify: `docs/superpowers/plans/2026-10-25-bundled-locked-original-admission.md` (baseline line only)
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`

**Interfaces:**
- Consumes: the existing `Align` and `AlignForEra` entries (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:185, 236`), the tie path at `:985`, the tie refusal sentence `BlockTieDetail` at `:84-85`, and the synthetic pair helpers of the test class.
- Produces: in the tie path, a classification by the generated lines each trial consumed. The consumed count is the trial's generated index delta across the block. Exactly one trial at zero runs a catch-up probe: `private enum CatchUpProbeOutcome { Died, CaughtUp, ReachedFileEnd }` and `private static CatchUpProbeOutcome ProbeCatchUp(WalkState zeroState, int rivalGeneratedIndex)`. The probe walks `zeroState` forward with the normal line emulation from its current original index. It clears the walking state's growth boundary and its `LookaheadRemaining` counter, because Y2 bypasses both, and caps at `zeroState.OriginalLines.Count` (Y10, evidence bound). It ends `Died` at the first line it cannot match, `CaughtUp` when the generated index reaches `rivalGeneratedIndex`, and `ReachedFileEnd` when the original lines run out. A nested undecided refusal inside the probe propagates as undecided (Y2 case-e note). Death records the rival trial's outcome. CaughtUp or ReachedFileEnd throws `BlockTieDetail` at the block start, undecided.
- Both trials nonzero keeps the status quo behavior as of 2026-10-25 (Y4): the existing lookahead loop stays for that class, decides when one candidate dies in the window, and refuses undecided when both reach the boundary alive. The new rules divert only the exactly-one-zero and both-zero classes. No synthetic fixture produces a both-nonzero tie through the vendor edit list, because a removed trial that consumes generated lines dies in an honest text, so the retained refusal is pinned by the existing end-of-file tie test and the characterization tests below. Task 2 adds the both-zero rule.

- [ ] **Step 1: Record the baseline.** Run Unity MCP `run_tests`, EditMode, no filter, in the dev editor instance after the `Application.dataPath` check. Write the observed total, passed, failed, and ignored counts into the Global Constraints baseline line of this plan file, with the run date. Expected: 0 failed before any change.
- [ ] **Step 2: Write the failing tests** in `PoiyomiLockedSourceAlignmentTests`. Synthetic pairs follow the vendor rules the existing block tests use: comments and pragmas dropped, ` #define OPTIMIZER_ENABLED ` after `CGPROGRAM`.
  - `TieWithAKeptGuardAndNoDuplicateAlignsAndRecordsKept`: the measured M1 shape. An `//ifex W==0` wrapper whose body holds an `//ifex A==0` block with a short surviving body of plain lines. Nothing survives between the inner block end and the wrapper end. One mismatching plain line sits after the wrapper end. Assert success, and both block records `Kept`. The inner block is what makes the tie reachable: its two candidates stop at the wrapper end boundary, and the code as of 2026-10-25 throws the undecided refusal there, so this test FAILS on its first run. After the fix the inner probe dies at the mismatching line and the outer probe dies the same way, so both records adopt `Kept`. This test is also the no-op guard of Falsifier 1.
  - `// --- Falsifier 1: a tie rule that decides kept whenever the kept trial consumed text, with no catch-up probe. ---` `TieWhoseBodyIsDuplicatedAfterTheBlockRefusesUndecided`: an `//ifex W==0` wrapper whose body holds an `//ifex A==0` block with a short surviving body of three lines. Nothing survives between the inner block end and the wrapper end. The three body lines repeat verbatim after the wrapper end. The kept world and the removed world of the inner block both reproduce the generated text, so the probe must catch up and refuse. Assert `Succeeded == false`, `RefusalDetail == "A block outcome stays undecided."`. No-op guard: the same pair without the duplicate is exactly `TieWithAKeptGuardAndNoDuplicateAlignsAndRecordsKept`, which aligns and records both blocks `Kept` after the fix.
  - `// --- Falsifier 9: a catch-up probe capped by a constant step or line count instead of the end of the file. ---` `CatchUpProbeWalksPastAnyConstantCapAndRefusesUndecided`: the Falsifier 1 fixture with a body of 61 surviving lines instead of three. The probe must walk all 61 duplicated lines to catch up, so a probe capped below 61 steps finds no death and no catch-up and wrongly decides `Kept`. Assert the undecided refusal. No-op guard: the Falsifier 1 guard pair aligns.
- [ ] **Step 3: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiLockedSourceAlignmentTests"]`. Expected: `TieWithAKeptGuardAndNoDuplicateAlignsAndRecordsKept` FAILS, because the code as of 2026-10-25 refuses the shape undecided at the growth boundary. The two falsifier tests PASS on their first run, because the current code refuses both shapes undecided already. Record both as characterization. They kill their falsifiers against the wrong implementations after the fix lands.
- [ ] **Step 4: Implement the classification and `ProbeCatchUp`** in `PoiyomiLockedSourceAlignment.cs`. In the tie path (`:985`), read each trial's generated index delta. One trial at zero: probe it. Death adopts the rival outcome. Otherwise throw the undecided refusal. Comment cites Y2 and Y10.
- [ ] **Step 5: Run** the same filter. Expected: PASS, including the two characterization tests, and the pre-existing block tests stay green.
- [ ] **Step 6: Commit, only with explicit owner authorization.** Stage `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`, and this plan file. Message: `feat(poiyomi): decide block ties with a catch-up probe capped at the end of the file`.

### Task 2: Block tie M2 rule, zero-and-zero adopts the removed trial (Y3 aligner half)

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs`, `Editor/Semantics/Poiyomi/LockedBlockRecord.cs`
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`, `Tests/Editor/Semantics/Poiyomi/LockedSourceReproductionTests.cs`

**Interfaces:**
- Consumes: Task 1's consumed-count classification and `RecordSkippedBlocks` (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:1095`).
- Produces: `LockedBlockRecord` gains `internal bool ZeroAndZero { get; }`. The constructor gains a fifth parameter `bool zeroAndZero` before `originalLine`. Every construction site updates: `ResolveBlock`, `RecordSkippedBlocks`, the test fixture builder at `Tests/Editor/Semantics/Poiyomi/LockedSourceReproductionTests.cs:316`, and any site the compiler lists. In the tie path, both trials at zero adopts the removed trial with no lookahead. The record carries the wrapper `Removed` with `ZeroAndZero == true` and `Kind == LockedBlockKind.Ifex`, and every nested block `NotEvaluated` through `RecordSkippedBlocks`. A kept `#ifdef` or `#ifndef` trial always consumes its directive line, so a zero-and-zero record implies an `//ifex` wrapper. The `Unobservable` branch at `:1062-1064` stays unchanged.

- [ ] **Step 1: Write the failing tests.**
  - `// --- Falsifier 2: an M2 resolution that runs the old lookahead or decides kept. ---` `ZeroAndZeroWrapperAlignsAndRecordsTheWrapperRemoved`: an `//ifex` wrapper over one removed `#ifndef K` guard, nothing surviving between the guard end and the enclosing end. Assert success, wrapper record `Removed` with `ZeroAndZero == true` and `Kind == LockedBlockKind.Ifex`, guard record `NotEvaluated`. No-op guard: the same wrapper with one surviving body line resolves through the Task 1 path and records `Kept`.
  - `CaseEWrapperResolvesAndTheLaterKeptGuardRecordsKept`: the measured case-e shape. The zero-and-zero wrapper first, then, later in the file, a kept `#ifdef` guard, then one mismatching plain line after the guard. The trailing line is load-bearing: without it the guard probe reaches the end of the original file and the shape refuses even after a correct fix. Assert success, wrapper `Removed` zero-and-zero, the later guard `Kept`. The wrapper runs no lookahead, so the later tie cannot throw through it (Y2 case-e note).
  - `CommentOnlyWrapperStaysUnobservable`: a wrapper whose body holds only comments. Assert the record is `Unobservable` and `ZeroAndZero == false`. Passes on its first possible run. Record it as characterization.
  - `RemovedIfdefRecordIsNeverZeroAndZero`: a `#ifdef` block whose generated text matches the removed outcome. Assert record `Removed` with `ZeroAndZero == false`. This pins the implication: zero-and-zero only ever carries `Kind == LockedBlockKind.Ifex`.
- [ ] **Step 2: Run** the class filter. Expected: compile failure first, because the new tests read `LockedBlockRecord.ZeroAndZero`, which does not exist yet. That is the RED observation of the house style.
- [ ] **Step 3: Implement** the both-zero rule and the `ZeroAndZero` record field. Comment cites Y3.
- [ ] **Step 4: Run** the class filter. Expected: PASS, Task 1 tests stay green. `CommentOnlyWrapperStaysUnobservable` and `RemovedIfdefRecordIsNeverZeroAndZero` pass on their first possible run. Record both as characterization.
- [ ] **Step 5: Commit, only with explicit owner authorization.** Stage the two production files and the two test files. Message: `feat(poiyomi): resolve zero-and-zero block ties as removed with a record marker`.

### Task 3: Strict condition mode and consistency selection (Y3 evaluator half)

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiLockCondition.cs`, `Editor/Semantics/Poiyomi/ILockedMaterialValues.cs`, `Editor/Semantics/Poiyomi/LockedSourceReproduction.cs`, `Editor/Build/UnityLockedMaterialValues.cs`, `Editor/DryRun/LockedPlanView.cs`
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiLockConditionTests.cs`, `Tests/Editor/Semantics/Poiyomi/LockedSourceReproductionTests.cs`, `Tests/Editor/DryRun/LockedPlanViewTests.cs`

**Interfaces:**
- Consumes: Task 2's `LockedBlockRecord.ZeroAndZero`.
- Produces:
  - `internal enum PoiyomiLockConditionMode { RemovalEvidence, Strict }` in `PoiyomiLockCondition.cs`.
  - `internal static PoiyomiLockConditionOutcome PoiyomiLockCondition.Evaluate(string conditionText, IPoiyomiLockConditionValues values, PoiyomiLockConditionMode mode)`. The two-argument overload goes. Call sites: `LockedSourceReproduction.cs:147` passes `RemovalEvidence`, `LockedSourceReproduction.cs:438` passes `block.ZeroAndZero ? PoiyomiLockConditionMode.Strict : PoiyomiLockConditionMode.RemovalEvidence`, and the test helper at `Tests/Editor/Semantics/Poiyomi/PoiyomiLockConditionTests.cs:290` passes `RemovalEvidence`.
  - `IPoiyomiLockConditionValues` gains `bool HasNonZeroAnimatedTag(string propertyName)`. In `Strict` mode an `isNotAnimated(P)` clause answers `False` when the tag read is true and `True` otherwise. In `RemovalEvidence` mode the rule as of 2026-10-25 stands (`Editor/Semantics/Poiyomi/PoiyomiLockCondition.cs:145-151`). Any false clause refuses through the existing condition sentence. No new refusal sentence exists.
  - `ILockedMaterialValues` gains `bool HasNonZeroAnimatedTag(string propertyName)`. `UnityLockedMaterialValues` reads the material override tag named `propertyName + "Animated"` and answers true when its text parses as an int other than 0 in the invariant culture. `LockedPlanView` reads the same tag on the locked material. The test stubs `FakeValues` at `Tests/Editor/Semantics/Poiyomi/LockedSourceReproductionTests.cs:61` and `StubValues` at `Tests/Editor/DryRun/LockedPlanViewTests.cs:145` implement the new member. `LockValuesAdapter` (`Editor/Semantics/Poiyomi/LockedSourceReproduction.cs:1061`) forwards to its `ILockedMaterialValues`. The aligner stays pure. The reads sit in the Unity view and the reproduction checks only.

- [ ] **Step 1: Write the failing tests.** In `PoiyomiLockConditionTests`, with a fake values type that now implements the new member:
  - `StrictModeReadsTheRealAnimatedTag`: condition `P==0 && isNotAnimated(P)`, value of `P` is 0, `HasNonZeroAnimatedTag("P")` is true. `RemovalEvidence` gives `True`. `Strict` gives `False`.
  - `LenientModeKeepsTheRemovalEvidenceRule`: same condition, `RemovalEvidence` mode, tag read true, outcome `True`. Passes on its first possible run. Record it as characterization.
  - In `LockedSourceReproductionTests`:
  - `// --- Falsifier 3: an M2 record that marks the inner guard Removed. ---` `LightVolumeWrapperRecordPassesTheConsistencyCheck`: wrapper `//ifex _LightingEnableLightVolumes==0 && isNotAnimated(_LightingEnableLightVolumes)` over `#ifndef VRC_LIGHT_VOLUMES_INCLUDED`, lock removes the wrapper body, record wrapper `Removed` zero-and-zero, guard `NotEvaluated`. The keyword sits in no pragma line. Clone values: `_LightingEnableLightVolumes` is 0, the tag read answers false. Assert `CheckConsistency` returns null while the clone keywords never hold the guard keyword. The wrong implementation marks the guard `Removed`, A6.1 demands the keyword, and the check refuses. No-op guard: the record reaches the removed-ifex branch, proven by the marker assertions of Task 2.
  - `// --- Falsifier 11: a zero-and-zero record that reuses the A6.2 isNotAnimated bypass. ---` `KeptWrapperWithAnAnimatedTagRefusesUnderTheStrictCheck`: wrapper `//ifex P==0 && isNotAnimated(P)` over inner `//ifex Q==1` whose body holds an alpha-relevant line. Both properties carry `Animated` tag 1, `P` is 0, `Q` is 1. The lock keeps the wrapper and removes the inner body, so both wrapper trials consume zero. Assert the record is wrapper `Removed` zero-and-zero, then `CheckConsistency` refuses with the condition sentence. No-op guard: the same pair with the `Animated` tag read false passes the check, which proves the fixture reaches the condition check.
- [ ] **Step 2: Run** `test_names: ["Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiLockConditionTests", "Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.LockedSourceReproductionTests", "Alrauna.Amuse.Tests.Editor.DryRun.LockedPlanViewTests"]`. Expected: compile failure first, because the new tests read the mode enum and the interface members do not exist yet. That is the RED observation of the house style.
- [ ] **Step 3: Implement** the mode parameter, the interface members, the two Unity view implementations, the two test stubs, and the `CheckConsistency` selection. Comments cite Y3.
- [ ] **Step 4: Run** the same filter. Expected: PASS, including the pre-existing evaluator and consistency tests. `LenientModeKeepsTheRemovalEvidenceRule` passes on its first possible run. Record it as characterization.
- [ ] **Step 5: Commit, only with explicit owner authorization.** Stage the five production files and the three test files. Message: `feat(poiyomi): check zero-and-zero records with a strict condition mode`.

### Task 4: Block extent learns block comments (Y4)

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs`
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`

**Interfaces:**
- Consumes: `FindBlockExtent` (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:797-845`) and the comment tracking of `RecordSkippedBlocks` (`:1095`) and `BodyHasSurvivingLine` (`:1134`).
- Produces: `FindBlockExtent` tracks `/*` to `*/` spans with an in-comment state, the same way the walk drops block comments. A directive shaped line inside a block comment never opens, closes, or nests a block. The vendor deletes block comments wholesale, so the generated text never shows one. The fix is behavior preserving wherever the old count was right.

- [ ] **Step 1: Write the failing test.**
  - `// --- Falsifier 8: an extent finder that counts directive shaped lines inside block comments. ---` `CommentedDirectiveInsideABlockNeverOpensABlock`: a block whose body carries a commented `//ifex` shaped line inside a `/*` to `*/` span, plus a real nested block. Assert success with the right nesting: the commented line records nothing, the real nested block records its own outcome. No-op guard: the same block without the commented line aligns.
- [ ] **Step 2: Run** the class filter. Expected: FAIL.
- [ ] **Step 3: Implement** the in-comment tracking in `FindBlockExtent`. Comment cites Y4.
- [ ] **Step 4: Run** the class filter. Expected: PASS.
- [ ] **Step 5: Commit, only with explicit owner authorization.** Stage the production file and the test file. Message: `feat(poiyomi): track block comments in the block extent finder`.

### Task 5: Suffix run scanner collects the full maximal run (Y5)

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs`
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`

**Interfaces:**
- Consumes: `CollectIdentifiers` (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:2368-2388`) and the whole-token filter `ContainsToken` (`:1453-1468`).
- Produces: `CollectIdentifiers` starts a run at any letter, digit, or underscore, so a run is the full maximal word character run. The artifact filter drops a collected run when either check holds: the original holds it as a whole token, or the run starts with a digit. Every other collected run stays an artifact, and an artifact with no declared name inside still yields an empty candidate set and refuses. Property names and bracket names keep their parsers and never pass through the run scanner. A digit-led run can never carry a rename, because every renamed form starts with a letter or an underscore.

- [ ] **Step 1: Write the failing tests.**
  - `// --- Falsifier 6: a run scanner that starts runs only at letters and underscores. ---` `DigitLedHexLiteralDoesNotBlockTheSuffix`: a generated text holding `0x7ffffff` inside a feature region, plus a rename. Assert success with the correct suffix. The wrong implementation collects `x7ffffff` and refuses on the empty intersection. No-op guard: the same pair without the literal aligns.
  - `// --- Falsifier 12: a run drop rule that relies on whole-token presence only. ---` `BakedDecimalAndExponentLiteralsDoNotBlockTheSuffix`: one generated text baking `0.975`, one baking `1E-06`, each digit run held nowhere in the original as a whole token. Assert success for both texts. No-op guard: the same pairs without the baked values align.
  - `NonDigitArtifactWithNoDeclaredNameStillRefuses`: a letter-led artifact the original never holds and that carries no declared name. Assert the suffix refusal sentence. This pins fail closed.
- [ ] **Step 2: Run** the class filter. Expected: `DigitLedHexLiteralDoesNotBlockTheSuffix` and `BakedDecimalAndExponentLiteralsDoNotBlockTheSuffix` FAIL, because the current scanner collects the letter part of each literal. `NonDigitArtifactWithNoDeclaredNameStillRefuses` passes on its first run. Record it as characterization.
- [ ] **Step 3: Implement** the run start rule and the digit-led drop. Comment cites Y5.
- [ ] **Step 4: Run** the class filter. Expected: PASS, and the admitted release-row era tests stay green.
- [ ] **Step 5: Commit, only with explicit owner authorization.** Stage the production file and the test file. Message: `feat(poiyomi): collect full maximal runs and drop digit-led suffix artifacts`.

### Task 6: Suffix head boundary at the earliest declared name end (Y6)

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs`
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`

**Interfaces:**
- Consumes: `SuffixCandidates` (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:483-519`) and `AddTailStripped` (`:521-551`).
- Produces: `SuffixCandidates` derives candidates only from the earliest position where a declared original name ends and an underscore follows. Later positions give nothing. `AddTailStripped` and its `_ST` and `_TexelSize` strip stay unchanged. In the test suffix strings below, the suffix string plays the role of the cleaned material name, because the aligner never sees a material name.

- [ ] **Step 1: Write the failing tests.** Each builds the renamed forms of the era with the given suffix. The declared set of a test is exactly the property list it passes to the aligner, so each fixture names every declared name, renamed or not. The unrenamed names are what make the wrong implementations fail.
  - `// --- Falsifier 4: a suffix split that tries every occurrence of a longest declared name. ---` `EmbeddedDeclaredNameSuffixDerivesFromTheEarliestBoundary`: declared `_Color`, renamed with suffix `Body_Color_1`. The artifact holds `_Color` twice, so the wrong implementation ties and refuses. Assert success, `Suffix == "_Body_Color_1"`.
  - `// --- Falsifier 5: a head rule that anchors anywhere but the earliest boundary. ---` `HeadAnchorsAtTheEarliestBoundaryNotALaterName`: declared `_Color`, renamed with suffix `Cutoff_X`, plus the unrenamed live float `_Cutoff`. The wrong implementation takes the longest occurring declared name `_Cutoff` and derives `_X`, and the walk refuses. Assert success, `Suffix == "_Cutoff_X"`.
  - `LongerEmbeddedNameStraddleDerivesOneSuffix`: declared `_Color` and `_EmissionColor`, both renamed with suffix `Cut_Cutoff_On`, plus the unrenamed live float `_Cutoff`. The wrong implementation gives disjoint candidate sets and refuses on the empty intersection. Assert success, `Suffix == "_Cut_Cutoff_On"`.
  - `EmbeddedNameNoLongerTiesTheIntersection`: declared `_Cutoff`, renamed with suffix `Body_Matcap_1`, plus the unrenamed live float `_Matcap`. The wrong implementation splits at both same-length names and ties. Assert success, `Suffix == "_Body_Matcap_1"`.
  - `PlainMaterialNameSuffixIsUnchanged`: declared `_Color`, suffix `Body`. Assert `Suffix == "_Body"`. A second case renames texture `_MainTex` with suffix `Body` and asserts `Suffix == "_Body"`. Passes on its first run. Record it as characterization. It is the no-op guard for both falsifiers above.
  - `RenamedTextureWithAnEmbeddedNameKeepsTheSuffix`: declared `_T` and `_Color`, with `_T` renamed and suffix `Body_Color_1`, and the `_ST` and `_TexelSize` renamed forms. The wrong implementation takes the longest occurring name `_Color` and derives `_1`, and the walk refuses. Assert success, `Suffix == "_Body_Color_1"`, and the renamed `_ST` form records `Renamed`. This pins the tail strip interplay.
- [ ] **Step 2: Run** the class filter. Expected: the five RED tests FAIL. `PlainMaterialNameSuffixIsUnchanged` passes on its first run, recorded as characterization.
- [ ] **Step 3: Implement** the earliest boundary rule in `SuffixCandidates`. Comment cites Y6.
- [ ] **Step 4: Run** the class filter. Expected: PASS.
- [ ] **Step 5: Commit, only with explicit owner authorization.** Stage the production file and the test file. Message: `feat(poiyomi): split rename suffixes only at the earliest vendor head boundary`.

### Task 7: Properties window counts braces outside strings and comments (Y7)

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs`
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`

**Interfaces:**
- Consumes: `PropertiesLineIndices` (`Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs:2333-2363`) and the inline Properties loop inside `GammaPropertyNames` (`:152-183`).
- Produces: one shared private helper `private static List<int> PropertiesWindow(IReadOnlyList<LineRef> lines)`. It returns the line indices inside the Properties block. Its brace counter skips the contents of double-quoted strings with backslash escapes, of `/*` to `*/` block comments, and of `//` to end of line. The window closes at the line that returns the depth to zero, as of 2026-10-25. `PropertiesLineIndices` returns the window. `GammaPropertyNames` drops its inline block loop and scans the window lines for `[Gamma]` with the existing name parse and the existing attribute-before-name rule.

- [ ] **Step 1: Write the failing tests.** The string rules the helper implements, stated once here: the string skip ends at the first unescaped double quote, and a backslash escapes the next character, so an escaped quote never ends the string. A string that never closes before the end of its line ends at that line end and never swallows following lines. The `//` skip ends at the end of the line. The block comment skip ends at the line that closes the comment.
  - `// --- Falsifier 7: a Properties window that counts braces inside display strings. ---` `DisplayStringWithUnbalancedBraceKeepsLaterPropertiesLines`: a Properties block whose display string holds an unbalanced `}` on a line before a renamed property line. An unbalanced `}` drives the count negative and closes the window early, which is the measured defect at the bundled Toon file. Assert success with the renamed artifact. The wrong implementation refuses or derives a wrong suffix. No-op guard: the same block without the brace string yields the artifact.
  - `EscapedQuoteInADisplayStringIsAccepted`: a display string with a backslash escaped quote, a `}` inside the string, then the real closing quote. Pin the exact outcome: the string skip ends at the real closing quote, and the `}` inside the string does not count, so the window stays open and the later renamed property line still yields its artifact. The wrong implementation counts the inner brace, closes the window early, and refuses.
  - `UnclosedDisplayStringIsAccepted`: a display string that holds a `}` and never closes before its line end. Pin the exact outcome: the skip ends at the line end, the `}` does not count, and the following Properties lines still count, so the window stays open past that line. The wrong implementation counts the inner brace and closes the window early.
  - `GammaMarkInsideAQuotedDisplayStringStopsCounting`: a `[Gamma]` mark inside a quoted display string records no gamma name. Passes on its first run. Record it as characterization. Skipping string contents can only remove gamma marks, and the vendor bakes with the real Unity flags, so the direction is safe.
  - `LineCommentBraceDoesNotCloseThePropertiesWindow`: a true `//` line comment holding a `}` inside the block. Assert the window still counts later renamed lines.
- [ ] **Step 2: Run** the class filter. Expected: the four new tests FAIL, the gamma characterization test passes.
- [ ] **Step 3: Implement** the shared helper and the two consumers. The duplicate loop goes. Comment cites Y7.
- [ ] **Step 4: Run** the class filter. Expected: PASS.
- [ ] **Step 5: Commit, only with explicit owner authorization.** Stage the production file and the test file. Message: `feat(poiyomi): count properties braces outside strings and comments`.

### Task 8: Era twins, full suite, and the release-row safety record (Y8, stage 3)

**Files:**
- Modify: `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`
- Create: `docs/superpowers/investigations/2026-10-25-bundled-locked-original-release-row-safety.md`
- No production change.

**Interfaces:**
- Consumes: the falsifier pairs of Tasks 1 to 7 and the per-era pattern of `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs:649-695`.
- Produces: the dated safety record the stage 7 gate cites.

- [ ] **Step 1: Write the era twins.** Six parameterized tests in `PoiyomiLockedSourceAlignmentTests`, each with `[TestCase(PoiyomiLockEra.Bundled9364)]`, `[TestCase(PoiyomiLockEra.Thry274To2742)]`, `[TestCase(PoiyomiLockEra.Thry2743)]`, each building the same synthetic pair as its all-era twin without era specific forms, that is no sampler collateral and no negative wrap, and driving `AlignForEra`:
  - `TheDuplicatedBodyTieRefusesInEveryEra` (Falsifiers 1 and 9): asserts the undecided refusal.
  - `TheZeroAndZeroWrapperResolvesInEveryEra` (Falsifiers 2, 3, and 11 record halves): asserts the wrapper `Removed` with `ZeroAndZero == true` and the guard `NotEvaluated`.
  - `TheDigitLedLiteralsDoNotBlockTheSuffixInEveryEra` (Falsifiers 6 and 12): asserts success with the correct suffix.
  - `TheEarliestHeadBoundarySuffixDerivesInEveryEra` (Falsifiers 4 and 5): asserts the earliest boundary suffix.
  - `TheBracedDisplayStringWindowYieldsTheArtifactInEveryEra` (Falsifier 7): asserts success with the renamed artifact.
  - `TheCommentedDirectiveNeverOpensABlockInEveryEra` (Falsifier 8): asserts success with the right nesting.
- [ ] **Step 2: Run** the class filter. Expected: PASS, all 18 era cases.
- [ ] **Step 3: Run** the full alignment suite and the full two-assembly suite in the dev editor instance. Expected: 0 failed. Record the counts in the safety record. Confirm the refusal sentence inventory (A11.1): every retained aligner refusal sentence keeps at least one refusal test. The new rules add no sentence.
- [ ] **Step 4: Write the safety record.** `docs/superpowers/investigations/2026-10-25-bundled-locked-original-release-row-safety.md`, in simple English, dated, with a privacy note. Contents: the Y8 argument. The new rules change behavior in exactly the ways their decisions state. Y2 and Y3 fire only where the old code refused undecided, with `Unobservable` untouched. Y5 widens acceptance for digit-led runs. Y6 narrows the split set. Y7 widens the Properties window. Each delta cannot turn an aligned release-row pair into a false positive: M1 records only what a grounding walk forced, M2 records a body that produced no generated text and its strict check refuses unless a vendor relock of the clone drops the same body, Y5 only drops runs the original holds or that start with a digit, Y6 wrong suffixes fail at the walk, Y7 can only add artifacts. The falsifiers ran under all three eras, release era included, with the observed counts. The release shader is not installed, so the safety of the two 9.3.64 release rows rests permanently on the synthetic era tests of the A11 pattern. Owner decision A3b, 2026-10-25: release package testing is dropped. The bundled 9.3 shader in Poiyomi 10 is the primary support path. The product keeps the two release rows. No release package test exists in this design.
- [ ] **Step 5: Commit, only with explicit owner authorization.** Stage the test file and the record. Message: `test(poiyomi): pin the tie and suffix rules in every era and record release row safety`.

### Task 9: Gated real-lock suite and the swap-in measurement record (Y9, stage 4)

**Files:**
- Create: `Tests/Editor/Build/BundledLockedOriginalRealLockTests.cs`
- Create: `docs/superpowers/investigations/2026-10-25-bundled-locked-original-swap-in-measurement.md`

**Interfaces:**
- Consumes: vendor plan stage 1, which creates `Tests/Editor/Build/VendorOptInGate.cs` with `internal const string RealThryLock = "AMUSE_REAL_THRY_LOCK";` and `internal static void Require(string variable, string disclosure)`, and lands the suite-start check. This task runs only after that stage landed. It also consumes the production swap-in service `TransientUnlockSwapIn.SwapIn` (`Editor/Build/TransientUnlockSwapIn.cs:61`), the reconstruction `LockedMaterialReconstruction.TryApply`, the memo `LockedSourceAlignmentMemo`, and the row evidence of `PoiyomiSourceAttestation.AdmittedSources()`.
- Produces: the dated measurement record the stage 7 gate and the row evidence comment cite. The suite class `BundledLockedOriginalRealLockTests` with ten test methods, one per case below.
- Vendor binding rule, identical to vendor plan Task 18: inside the test assembly the plain name `Thry.ThryEditor.ShaderOptimizer` binds to the test stand-in, and the test assembly references no vendor assembly. The suite resolves the real tool by assembly-qualified reflection, `Type.GetType("Thry.ThryEditor.ShaderOptimizer, ThryAssemblyDefinition")`, because the installed thryeditor Editor folder ships `Packages/com.poiyomi.thryeditor/Editor/ThryAssemblyDefinition.asmdef` whose name field is `ThryAssemblyDefinition`, verified on 2026-10-25. The no-op guard asserts the resolved type is non-null, its `Assembly` is not the test assembly, and its assembly name is `ThryAssemblyDefinition`. The real methods carry two parameters: `LockMaterials(IEnumerable<Material> materials, ProgressBar progressBarType)` at `Packages/com.poiyomi.thryeditor/Editor/ShaderOptimizer.cs:481` and `UnlockMaterials` in the same shape at `:493`, with the nested enum `ProgressBar` at `:459`. The suite resolves the nested enum from the vendor type, reads its `None` field, and invokes with both arguments.

- [ ] **Step 1: Write the gated suite.** Suite rules, each stated in the file header and in the gate message:
  - Gate. Every test starts with `VendorOptInGate.Require(VendorOptInGate.RealThryLock, disclosure)`. The disclosure names what the tests do, the disposable dev project requirement, and that they never run in the Census Lab project.
  - Network disclosure. The header and the gate message disclose that the vendor lock calls the vendor usage counter once per editor session unless the editor preferences already record the counts. Cite the spec Y9 item 7 for the details.
  - Vendor presence. Resolve the lock entry through the vendor binding rule above. Absent thryeditor or absent Poiyomi: `Assert.Ignore` with a named reason, in the pattern of `Tests/Editor/Build/DaoMergedConsumptionTests.cs:119-127`.
  - No version gate. The suite runs against whatever thryeditor is installed and never skips for a version reason (Y9 item 9).
  - Shader load. Resolve each bundled shader by the `AssetGuid` of its pinned row from `PoiyomiSourceAttestation.AdmittedSources()` through `AssetDatabase.GUIDToAssetPath`, never by name lookup. A missing bundled asset skips with a named reason.
  - Attestation seam. The suite passes a test scoped attestation delegate that answers the post-admission behavior for the bundled rows, because the row flag is still off until stage 7. The delegate calls the production check and overrides only the two bundled rows: it answers the production answer for every non-bundled row and for unrelated shaders, and answers true only for a locked material whose resolved original is one of the two bundled rows. One assertion per suite run pins the fidelity: for a material on a 9.3.64 release row the delegate answers exactly what `LockedMaterialIdentity.OriginalShaderAttested` answers.
  - Harness. The suite drives `TransientUnlockSwapIn.SwapIn` by direct call, never through an NDMF build (spec Validation 4). It builds the `BuildContext` with its public constructor as `Tests/Editor/Build/TransientUnlockWindowCloseTests.cs:672` does, activates the animator extension context on that context, sets the window state with consent granted, and passes `TransientUnlockAvailability.CreateProductionRestore()` as the restore delegate.
  - Cases. Toon row: all defaults, one renamed property, two renamed properties, a material name with spaces and symbols, light volumes on, light volumes off, AudioLink enabled. Two Pass row: all defaults, one renamed property, AudioLink enabled. Ten tests, named `Real<Row><Shape>AlignsAndRestores`. AudioLink keeps digit-led literals in the generated text and exercises Y5 on real text. Default textures only, per the record section 8 limits, stated in the test comments.
  - Assertions per case. The locked identity is recognized. The aligner result succeeds through `LockedSourceAlignmentMemo.GetOrAlign` on a cleared memo. `LockedMaterialReconstruction.TryApply` succeeds on a clone. `TransientUnlockSwapIn.SwapIn` returns a summary with `RestoreMismatchSlots == 0`, which proves the seventh fact and the consistency check passed. No slot refusal other than the expected named ones fired. Per-phase elapsed times print to the test output: a fresh `GetOrAlign` after `LockedSourceAlignmentMemo.Clear()`, the clone and restore around `TryApply`, and the consistency call.
  - Cleanup order. In a finally block: delete the materials the suite created first, then delete only the cache entries that appeared under `Assets/_LockedShaderCache/` during the test. Snapshot that folder set before the first lock and assert the set after cleanup equals the snapshot. Never delete an entry the suite did not create.
- [ ] **Step 2: Refresh Unity, read the console, confirm no compile errors.**
- [ ] **Step 3: Run the gated suite.** Verify `Application.dataPath`. Set `AMUSE_REAL_THRY_LOCK=1` in the dev editor instance process through `execute_code` with `Environment.SetEnvironmentVariable`. Run `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Build.BundledLockedOriginalRealLockTests"]`. Expected: 10 passed, 0 failed, 0 skipped. Clear the variable after the run.
- [ ] **Step 4: Write the measurement record.** `docs/superpowers/investigations/2026-10-25-bundled-locked-original-swap-in-measurement.md`, in simple English, dated, with a privacy note. Contents: per distinct generated shader, the align time of a full successful walk, the clone and restore time, and the dry-run align time. The dry-run align time is one fresh `LockedSourceAlignmentMemo.GetOrAlign` on the same pair after `LockedSourceAlignmentMemo.Clear()`, because the dry run reads the same memo and pays exactly that cost, file reads and digests included, once per distinct generated shader. Every observed refusal sentence. The observed per-lock total times against the baseline of record section 4.5. The limits: default textures, the dev project color space, the installed thryeditor version recorded as a fact with its date.
- [ ] **Step 5: Gate.** One real lock per bundled row aligned, cloned, restored, passed the seventh fact and the consistency check, with times and refusal sentences recorded. If any case refuses, the refusal sentence must be one the spec rules produce, or stage 4 does not hold and stage 7 stays out of reach.
- [ ] **Step 6: Commit, only with explicit owner authorization.** Stage the suite file and the record. Message: `test(build): gated real lock acceptance suite for the bundled rows with a dated measurement`.

### Task 10: Time budget statement (Y10, stage 5)

**Files:**
- Modify: `docs/superpowers/investigations/2026-10-25-bundled-locked-original-swap-in-measurement.md`

**Interfaces:**
- Consumes: the stage 4 record.
- Produces: the dated Y10 section that the stage 7 gate cites.

- [ ] **Step 1: Source scan.** Search `Editor/Semantics/Poiyomi/`, `Editor/Build/TransientUnlockSwapIn.cs`, and `Editor/Build/TransientUnlockAvailability.cs` for `Stopwatch`, `DateTime.Now`, `Environment.TickCount`, and `Time.realtimeSinceStartup`. Expected: no hit takes part in a refusal decision. Record the observed result in the statement.
- [ ] **Step 2: Append the Y10 section** to the measurement record, dated. It states: no step of the aligner or the swap-in refuses a material on elapsed time or on a step count. The only cap is the Y2 end-of-the-original-file evidence cap. Owner decision Q3, 2026-10-25: no time limit applies. Slow code stays unacceptable. The measured full walk costs and the dry-run cost from stage 4. The memo clears at finish pass entry and at the end of apply, and the A8.4 keying and lifetime stay unchanged. Each build therefore pays the full align cost again per distinct generated shader. Each dry run whose memo is cold pays it once per distinct generated shader. A second dry run in the same session without a build between pays nothing, because the memo still holds.
- [ ] **Step 3: Gate.** The record states the measured numbers. Stage 5 holds.
- [ ] **Step 4: Commit, only with explicit owner authorization.** Stage the record. Message: `docs: state the bundled locked original time budget from measured walks`.

### Task 11: Window and report pre-flip pins (Y11, Y12, stage 6)

**Files:**
- Modify: `Editor/Build/AmuseReportStrings.cs`
- Modify: `Tests/Editor/Build/AmuseReportStringsTests.cs`, `Tests/Editor/Build/TransientUnlockAvailabilityTests.cs`, `Tests/Editor/Build/LockedMaterialIdentityTests.cs`, `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`

**Interfaces:**
- Consumes: `WindowEligibleForConsent` (`Editor/Build/TransientUnlockAvailability.cs:52-78`), `PreCheckRefusal` (`Editor/Build/LockedMaterialIdentity.cs:134-161`), the `TransientUnlockRestoreMismatch` strings (`Editor/Build/AmuseReportStrings.cs:720-729`), and the bundled row pins in `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs:528, 541, 644-651`.
- Produces: the pre-flip pins and the Y11 strings that the stage 7 change set rewrites.

- [ ] **Step 1: Write the Y11 strings test first.** In `AmuseReportStringsTests`: `RestoreMismatchStringsDescribeAVerifiedOriginalThatFailedToAlign`. Assert the description carries, before the reason placeholder, the exact sentences "The material is locked and its original shader is verified." and "The locked shader text did not match what AMUSE can reconstruct.", and the hint ends with the exact sentence "The original shader is verified, so the miss is in the locked shader text.". Run it. Expected: FAIL, the sentences are not there yet.
- [ ] **Step 2: Edit the strings.** In `Editor/Build/AmuseReportStrings.cs:720-729`, insert the two description sentences after "the copy did not pass verification." and append the exact hint sentence above to the existing hint. No new refusal enum value. The reason field keeps carrying the aligner sentence, which A8.2 already prefers. Simple English, no contractions. Run the test. Expected: PASS.
- [ ] **Step 3: Write the Y12 pins.** In `TransientUnlockAvailabilityTests`: `BundledRowLockedMaterialIsNotWindowEligibleWhileTheFlagIsOff`. Fixture: a material with the locked identity signals, the original name tag set to the bundled row name, and the original GUID tag set to the row GUID. The material's shader needs the locked name prefix, and no in-memory shader can carry it, so the fixture writes one scratch stand-in shader asset in the existing pattern of `EnsureLockedPoiyomiStandInShader` at `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs:1295` and deletes it after the test. The original shader resolves by the pinned row GUID to the installed bundled file. A missing installed bundled asset skips with a named reason. Assert `WindowEligibleForConsent` with the production attestation answers false. Passes on its first run, recorded as characterization. In `LockedMaterialIdentityTests`: `BundledRowLockedMaterialPrecheckKeepsTheNamedRefusalWhileTheFlagIsOff` on the same stand-in fixture. Assert `PreCheckRefusal` returns `LockedPoiyomiOriginalShaderUnattested`. Same skip guard. Characterization. The eligibility gate is the only consent gate, so the false pin also pins the coupling: an ineligible build cannot open the window path.
- [ ] **Step 4: Write the Falsifier 10 pin.** Update the assertion messages of the bundled row pins in `PoiyomiMaterialSemanticsTests` at `:528, 541, 644-651` and of `BundledRowRefusesAsALockedOriginal` in `LockedMaterialIdentityTests` at `:197-220`. Each message names this design and its stage list: the bundled rows stay refused until stage 7 of the 2026-10-25 bundled locked original admission design, whose stages in order are block tie fix, suffix fix, release row safety, real lock measurement, time budget statement, window and report tests, flag change. An early flag flip fails these pins by name.
- [ ] **Step 5: Run** `test_names: ["Alrauna.Amuse.Tests.Editor.Build.AmuseReportStringsTests", "Alrauna.Amuse.Tests.Editor.Build.TransientUnlockAvailabilityTests", "Alrauna.Amuse.Tests.Editor.Build.LockedMaterialIdentityTests", "Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiMaterialSemanticsTests"]`. Expected: PASS.
- [ ] **Step 6: Commit, only with explicit owner authorization.** Stage the five files. Message: `test(build): pin pre-flip window and report behavior for bundled locked originals`.

### Task 12: Flag change and full cutover (Y13, stage 7)

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs`, `Editor/Build/LockedMaterialIdentity.cs`, `Editor/Build/AmuseReportStrings.cs`
- Modify: `Tests/Editor/Build/LockedMaterialIdentityTests.cs`, `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`, `Tests/Editor/Build/TransientUnlockAvailabilityTests.cs`, `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`
- Modify: `README.md`, `Packages/com.alrauna.amuse/README.md`, `AGENTS.md`, `docs/architecture/vision.md`, `Packages/com.alrauna.amuse/CHANGELOG.md`, `docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md`

**Interfaces:**
- Consumes: every earlier task. The gate is the Y13 checklist, all on dated evidence.
- Produces: the admitted rows, the rewritten pins, the docs cutover, and the post-flip observation record.

- [ ] **Step 1: Check the gate item for item, and stop on any miss.** Stages 1 and 2 GREEN, with every new falsifier either observed RED and then GREEN or recorded as characterization on its first run, per the per-test notes of Tasks 1 to 7, and the full alignment suite passing. Stage 3 record written, with the falsifiers run under all eras and the suite green. Stage 4 record written, with at least one real lock per bundled row through clone, restore, seventh fact, and consistency, every observed refusal sentence one the spec rules produce, and the times recorded. Stage 5 record section present. Stage 6 pins and the Y11 strings landed and passed. The positive eligibility pin lands in this change set.
- [ ] **Step 2: Flip the flag.** In `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:122-133`, set the `LockedOriginalAdmitted` argument of the two bundled rows from `false` to `true`. This is the only production behavior change of stage 7. Comment cites Y13 and K8.1 of the attestation design as amended.
- [ ] **Step 3: Rewrite the row admission test.** `BundledRowRefusesAsALockedOriginal` in `LockedMaterialIdentityTests` becomes `BundledRowWithAMatchingTagAttestsTheLockedOriginal`: the bundled row with a matching tag attests, assert `AttestsLockedOriginal` answers true. Keep the identity gate no-op guard. The helper `BundledMainRow` selects its row through the admission flag and matches no row after the flip, so rewrite it to select the bundled main row by its `ShaderName`. Falsifier 8, `ReleaseMainEvidenceWithAnotherAdmittedTagRefuses`, stays as is. The missing-tag tests stay as is. K8.2 keeps protecting a release lock whose GUID resolves to the bundled file through the same tag gate.
- [ ] **Step 4: Flip the row pins.** In `PoiyomiMaterialSemanticsTests`, set the bundled row assertions at `:528, 541, 644-651` from `Is.False` to `Is.True`, with messages that name this design and its date. No weakening anywhere.
- [ ] **Step 5: Flip the eligibility pin.** In `TransientUnlockAvailabilityTests`, the Y12 negative becomes `BundledRowLockedMaterialMakesTheWindowEligibleAfterAdmission`: the same fixture, assert `WindowEligibleForConsent` answers true. This is the Falsifier 10 positive half, in the same change set as the flag.
- [ ] **Step 6: Rewrite the dry-run pin.** `LockedMaterialTaggedWithTheBundledShaderIsNotDeferred` in `AlphaSeparationDryRunTests` becomes two tests: `BundledTaggedLockedSlotDefersBeforeTheMirrorDecides`, asserting `IsSlotDeferred` answers true through the name-tag arm while the pair is not yet mirrored, and `BundledTaggedLockedSlotWhoseMirrorRefusesIsNotDeferred`, asserting the locked mirror still refuses a pair that cannot align, with the bundled tag on the existing locked-mirror gate scene. The K8.4 seam at `Editor/DryRun/AlphaSeparationDryRun.cs:1663-1678` needs no code change.
- [ ] **Step 7: Drop the bundled clause from the report strings.** In `Editor/Build/AmuseReportStrings.cs:176-191`, remove the sentence "That includes the bundled 9.3 shader that Poiyomi 10.x packages ship." from the `LockedPoiyomiOriginalShaderUnattested` description. The refusal stays for the other named causes. The key presence pin in `AmuseReportStringsTests` stays green.
- [ ] **Step 8: Rewrite the K8 comments.** The K8.4 gate comment at `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:199-205` says the accepted set covers all four admitted rows as of the stage 7 date, names this design, states the primary support path as the bundled 9.3 shader in Poiyomi 10, and names no release package test. The K8.3 comment at `Editor/Build/LockedMaterialIdentity.cs:269-279` names this design as the amendment of its reason. The row evidence comment next to the row array in `PoiyomiSourceAttestation.cs` records this design, its date, and the stage 4 measurement record path.
- [ ] **Step 9: Update the docs, in simple English, no machine paths, no GUID values.** Both READMEs, the bundled locked sentence at line 42 of each: the locked bundled rows are admitted under the same reconstruction contract as the release rows. `AGENTS.md` overview line 7: the locked bundled sentence changes the same way. `docs/architecture/vision.md` line 55: same change. `Packages/com.alrauna.amuse/CHANGELOG.md` under `[Unreleased]`: rewrite the line 8 support bullet to cover locked bundled originals, replace the line 13 bundled refusal bullet with the admission statement, extend the line 21 rebuild bullet to the bundled rows, and add one admission bullet.
- [ ] **Step 10: Write the spec amendments.** In `docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md`, append dated amendment notes: A3.7 amended by Y2, Y3, and Y4 of the admission design, A4.2 amended by Y5 and Y6, A6.2 amended for zero-and-zero records by Y3. Each note names the decision and the design document.
- [ ] **Step 11: Run the full two-assembly suite** in the dev editor instance. Expected: 0 failed. Record the counts with the date. Run the identifier sweep over every changed file: an at sign joined to hexadecimal, drive-letter paths, home-directory paths, four-digit ports, and private names learned in the session. Expected: no hits.
- [ ] **Step 12: Commit, only with explicit owner authorization.** Stage exactly the files of this task. Message: `feat(poiyomi): admit the two bundled 9.3 rows as locked originals`.
- [ ] **Step 13: Post-flip observation, never a gate.** Ask the owner to run one play-mode build of the private avatar in the Census Lab editor instance. AMUSE tooling then reads the console of that instance once, read-only. It never enters play mode there, builds there, writes there, or runs tests there. Record the remaining locked-material refusal sentences as ranges, with the observation date, in a dated section appended to the stage 4 measurement record. The observation shows which suffix refusals survive the fixes, if any.
