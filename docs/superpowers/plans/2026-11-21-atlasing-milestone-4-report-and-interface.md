# Atlasing Milestone 4: Report and Interface Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Privacy note: This record contains public product planning only. It contains no private asset observations, machine identifiers, or vendor source excerpts.

Date: 2026-11-21.
Status on 2026-11-21: Executable plan for Milestone 4 of `docs/superpowers/plans/2026-11-19-0-2-0-minimal-atlasing-plan.md`. Milestone 3 merged into main on 2026-11-21. The owner asked on 2026-11-21 to go ahead with Milestone 4. The execution is inline.
Status on 2026-11-21: Implemented on branch `atlasing-m4-report-and-interface`. The first full run of the product and research EditMode assemblies in the dev editor instance failed 2 tests: both cases of `TransientUnlockTransformationTests.SlotLessPairDispositionIsPinned`. They counted every console entry of a build, and the atlas pass now adds its use lines to that build. The test now leaves `amuse.atlas.` entries out of the window close counts. The second full run on 2026-11-21 reported 3697 total, 3672 passed, 0 failed, and 25 skipped. The skips are the vendor opt-in gates.

**Goal:** The atlas pass reports its results. The build console gets one line per page set and one line per refused use. The inspector shows the three atlas settings. The material transformation view states that it excludes atlasing.

**Architecture:** The atlas pass records one page set entry per built group and keeps its use refusals in state. One report step at the end of the pass emits the lines. Budget admission returns the value and the limit with each refusal, so a budget line can state them. The inspector draws the settings through the existing serialized-property pattern.

**Tech Stack:** C#, Unity 2022.3.22f1, NDMF, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md`, sections 1, 10, and 11. Milestone 1 decision D5 moved the inspector work to this milestone.

## Global Constraints

- Package root for every path below: `Packages/com.alrauna.amuse/`.
- Every report string, label, tooltip, and comment uses simple English.
- Every report entry has Information severity. A refusal is a normal outcome.
- Do not change the alpha separation reports or the dry run rows.
- Before any Unity MCP call, confirm that `Application.dataPath` belongs to this project.

## Plan Decisions

- **R1. Page set line.** Key `amuse.atlas.PageSet`. One entry per group that kept its pages. The arguments are the page set number, the shader row, the page count, the use count, the page bytes, the distinct source count, and the source bytes. The line states:
  - The merge-ready result. Every use of the set shares one group key, and each page has one material. So one material configuration serves the whole set, and only the page texture differs between pages. The result is always "ready" for a reported set, because a set that is not ready never builds (design section 6).
  - The allocation change and its cause. The pages add their bytes. The sources stay in the avatar, because AMUSE cannot prove that nothing else uses them (design section 10).
  - The fact that 0.2.0 does not reduce draw calls or material slots (design section 1).
- **R2. Use lines.** Each recorded use refusal gets one entry with the renderer as its context object. The use-scope titles name the slot number and the renderer name, in the shape of the alpha separation slot lines. The three avatar-scope causes keep their existing entry.
- **R3. Budget value and limit.** `AtlasBudgetAdmission.Admit` returns one `AtlasBudgetVerdict` per group: the refusal, the value, and the limit in bytes. For the group number, the limit is the floor of the source bytes times the factor. The trip rule compares integers, so the floor states the limit exactly. Each use line of a budget refusal states the value and the limit of its group. Design section 10 asks for one group line with these numbers. Section 11 asks for one line per refused use. The use lines carry the numbers, so the reader gets both without a second line.
- **R4. Order.** The use lines come first, in record order. The page set lines follow, in plan order. The report runs after the write. A thrown invariant failure reports nothing.
- **R5. Inspector.** An `Atlasing` toggle draws below the alpha separation switches. An `Atlas Settings` foldout draws below `Alpha Separator Settings`, under the Advanced Settings reveal. It holds `Maximum Page Size`, a delayed integer field clamped to 1 to 8192, and `Protected Mip Levels`, a slider from 1 to 14. A stored value outside its range draws the existing out-of-band warning. The control writes only when the user changes it.
- **R6. View label.** A third disclosure sentence states that the view excludes atlasing and that the build console lists the atlas results. The Play mode view shows the same label and no atlas rows. Milestone 1 decision D5 named Play-mode banner rows. Design section 11 asks only for the label. The view sits under the Alpha Separator foldout, so atlas rows do not belong there.

## Review Focus

1. A budget-refused use must state the value and the limit. Task 1 owns `GroupBudgetVerdictStatesTheFlooredLimit` and Task 2 owns `BudgetRefusalLineStatesTheValueAndTheLimit`.
2. Every built page set must report exactly once, and a withdrawn group must report no page set line. Task 2 owns `WithdrawnGroupReportsNoPageSetLine`.
3. A refused use must name its slot and renderer. Task 2 owns `RefusedUseReportsOneLineWithItsSlot`.
4. The inspector must never write a stored value that the user did not change. Task 3 owns `OutOfRangePageSizeDrawsAWarningAndKeepsTheValue`.

---

### Task 1: Budget verdicts

**Files:**
- Create: `Editor/Build/AtlasBudgetVerdict.cs`
- Modify: `Editor/Build/AtlasBudgetAdmission.cs`, `Editor/Build/AtlasPass.cs`
- Test: `Tests/Editor/Build/AtlasBudgetAdmissionTests.cs`

- [x] **Step 1: Write the failing test.** `GroupBudgetVerdictStatesTheFlooredLimit`, `PeakBudgetVerdictStatesThePrediction`, and `AvatarBudgetVerdictStatesTheRunningSum`. Move the existing assertions to `.Refusal`.
- [x] **Step 2: Run the filter `AtlasBudgetAdmissionTests`. Expect compile errors.** The tests and R3 landed in one edit, so no separate RED run exists for this task. The three verdict tests are recorded as characterization. The Task 2 RED run on 2026-11-21 passed them, and `BudgetRefusalLineStatesTheValueAndTheLimit` read the verdict value and limit from state before it failed on the missing report line.
- [x] **Step 3: Implement R3.**
- [x] **Step 4: Run the filter. Every test passes.**

### Task 2: Report lines

**Files:**
- Create: `Editor/Build/AtlasPageSet.cs`
- Modify: `Editor/Build/AtlasPass.cs`, `Editor/Build/AmuseReports.cs`, `Editor/Build/AmuseReportStrings.cs`, `Editor/Build/AmusePlatformFinishPlugin.cs`
- Test: `Tests/Editor/Build/AtlasEndToEndTests.cs`

- [x] **Step 1: Write the failing tests.**
  - `TwoMaterialsReportOnePageSetLine`: the Poiyomi end-to-end fixture reports one `amuse.atlas.PageSet` entry. The line states one page, two uses, the page bytes, two sources, and their bytes. The byte oracle reads the live textures.
  - `RefusedUseReportsOneLineWithItsSlot`: a scaled member texture reports one `amuse.atlas.NonIdentityTransform` entry with the slot number and the renderer name.
  - `WithdrawnGroupReportsNoPageSetLine`: an injected acquisition failure reports the use line and no page set line for the withdrawn group.
  - `BudgetRefusalLineStatesTheValueAndTheLimit`: an injected group limit reports `GroupBudget` with the value and the limit.
- [x] **Step 2: Run the filters. Expect failures.** Observed on 2026-11-21: 34 tests ran. The four report tests failed, each with 0 report lines where it expected 1.
- [x] **Step 3: Implement R1, R2, and R4.**
- [x] **Step 4: Run the filters. Every test passes.** Observed on 2026-11-21: the atlas end-to-end, budget, pass, and report string classes ran 83 tests with 83 passed.

### Task 3: Inspector and view label

**Files:**
- Modify: `Editor/AmuseAvatarOptimizerEditor.cs`, `Editor/DryRun/MaterialTransformationRow.cs`
- Test: `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`, `Tests/Editor/DryRun/MaterialTransformationRowTests.cs`

- [x] **Step 1: Write the failing tests.**
  - `AtlasSettingsHeaderDrawsUnderTheReveal`.
  - `AtlasingToggleDrawsTheStoredValue`.
  - `OutOfRangePageSizeDrawsAWarningAndKeepsTheValue`. It also covers a stored protected count of 20.
  - `DisclosureSentencesMatchTheSpec` gains the third sentence. The Play mode view draws the same disclosure rows before it reads the record, so this sentence test covers both views.
- [x] **Step 2: Run the filters. Expect failures.** Observed on 2026-11-21: 66 tests ran. Five failed: the three inspector tests, and two sentence tests that threw on the missing third sentence.
- [x] **Step 3: Implement R5 and R6.**
- [x] **Step 4: Run the filters. Every test passes.** Observed on 2026-11-21: 66 tests ran with 66 passed.
- [x] **Step 5: Open the real inspector in the dev editor instance and capture it.** Observed on 2026-11-21 on a temporary component that was removed afterward. The Atlasing switch draws below the separator switches. The Atlas Settings menu draws under Advanced Settings with both labels in full. A stored page size of 9000 draws the out-of-band warning and keeps the value. The transformation view shows the third label.

### Task 4: Close

- [x] Run both full assemblies. Record the counts.
- [x] Update the master plan and `CHANGELOG.md`.
