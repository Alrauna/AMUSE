# Atlasing Milestone 5: Release Qualification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Privacy note: This record contains public product planning only. It contains no private asset observations, machine identifiers, or vendor source excerpts.

Date: 2026-11-21.
Status on 2026-11-21: Executable plan for Milestone 5 of `docs/superpowers/plans/2026-11-19-0-2-0-minimal-atlasing-plan.md`. Milestone 4 merged into main on 2026-11-21. The owner asked on 2026-11-21 to go ahead with Milestone 5 and authorized subagents for it. The owner chose the three budget values on 2026-11-21.
Status on 2026-11-21: Implemented on branch `atlasing-m5-release-qualification`. The full run of the product and research EditMode assemblies in the dev editor instance on 2026-11-21 reported 3699 total, 3674 passed, 0 failed, and 25 skipped. The skips are the vendor opt-in gates. The relock regression fixture ran separately with its gate set. The 8192 upload facts of Milestone 2 are still open for the owner.

**Goal:** Replace the unlimited atlas budget with measured values, run the relock regression fixture and both full assemblies, and document atlasing in the README.

**Architecture:** `AtlasBudgetLimits.Provider` returns one fixed production value. The unlimited value and its skip branch go away, because no production or test caller needs them after this milestone. Tests that replace the provider restore the saved delegate.

**Tech Stack:** C#, Unity 2022.3.22f1, NDMF, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md`, sections 10 and 13.

## Global Constraints

- Package root for every code path below: `Packages/com.alrauna.amuse/`.
- Do not change the budget formulas. Only the limit values change.
- Every comment, report string, and document uses simple English.
- Before any Unity MCP call, confirm that `Application.dataPath` belongs to this project.

## Measurements

All byte numbers come from the production functions in `Editor/Analysis/AtlasBudget.cs` with the defaults: a maximum page size of 8192 and one protected level. The byte ledger throws when the pass holds more than the prediction, so the prediction is an upper bound on the bytes that the pass holds.

| Public fixture | Page bytes | Source bytes | Page over source | Peak prediction |
| --- | --- | --- | --- | --- |
| End-to-end pair: two 64 by 64 RGBA32 sources, one 132 by 66 page | 46,356 | 43,688 | 1.061 | 231,324 |
| End-to-end single source: one 64 by 64 RGBA32 source, one 66 by 66 page | 23,144 | 21,844 | 1.059 | 134,708 |
| Upload fixture: two 4096 by 4096 RGB24 sources, one 8192 by 4096 page | 134,217,729 | 134,217,726 | 1.00000002 | 816,491,180 |
| Largest single group: one 8192 by 8192 RGBA32 source and page | 357,913,940 | 357,913,940 | 1.0 | 2,147,483,644 |

Each further distinct 8192 by 8192 RGBA32 source on a page adds 536,870,912 bytes to the peak prediction.

## Plan Decisions

- **B1. Group factor 5/4.** A group may hold at most 25 percent more page bytes than source bytes. Borders and packing gaps are the only cause of a ratio above 1. The public fixtures measure at most 1.061. A group above 5/4 wastes more memory on borders than it places.
- **B2. Avatar retained limit 524,288,000 bytes (500 MiB).** This is the VRChat SDK limit for the uncompressed size of a PC avatar bundle. The SDK constant `CONTENT_AVATAR_UNCOMPRESSED_ASSET_BUNDLE_SIZE_LIMIT_PC` held this value in the dev project on 2026-11-21. Pages that alone pass this limit can never upload. The limit admits the upload fixture page.
- **B3. Peak temporary limit 4,294,967,296 bytes (4 GiB).** It admits every group that the measurements list. It admits a full 8192 page with up to five distinct 8192 by 8192 RGBA32 sources, at 4,294,967,292 bytes. A sixth such source refuses the group.
- **B4. One production value.** `AtlasBudgetLimits.Production` holds B1 to B3. `Provider` returns it. `Unlimited`, `IsUnlimited`, and the skip branch in `AtlasBudgetAdmission.Admit` are deleted. `AtlasEndToEndTests` saves the provider in its set up and restores it in its tear down.

## Review Focus

1. Each value must trip at its exact boundary. Task 1 owns one boundary test per number.
2. The avatar limit must use binary megabytes. `ProductionAvatarLimitAdmitsExactlyFiveHundredMebibytes` fails for a limit of 500,000,000 bytes.
3. No test may leave a replaced provider behind. Task 1 changes the `AtlasEndToEndTests` tear down.

---

### Task 1: Budget values

**Files:**
- Modify: `Editor/Build/AtlasBudgetLimits.cs`, `Editor/Build/AtlasBudgetAdmission.cs`
- Test: `Tests/Editor/Build/AtlasBudgetAdmissionTests.cs`, `Tests/Editor/Build/AtlasEndToEndTests.cs`

- [x] **Step 1: Write the failing tests.** All tests call `AtlasBudgetAdmission.Admit` with `AtlasBudgetLimits.Provider()`.
  - `ProductionGroupFactorAdmitsAQuarterOfBorder`: an 80 byte R8 source on a 100 byte page is admitted, and on a 101 byte page refuses with `GroupBudget`, value 101, and limit 100.
  - `ProductionAvatarLimitAdmitsExactlyFiveHundredMebibytes`: eight groups of 65,536,000 bytes are admitted. A ninth group of 1 byte refuses with `AvatarBudget`, value 524,288,001, and limit 524,288,000.
  - `ProductionPeakLimitAdmitsFiveFullSizeSources`: a full 8192 RGBA32 page with five distinct 8192 RGBA32 sources is admitted. With six sources the group refuses with `PeakBudget`.
  - `ProductionLimitsAdmitTheUploadFixtureShape` replaces `UnlimitedAdmitsAnEightKilotexelGroup`. It uses the RGB24 shape of the upload fixture. It passes on the unlimited value too, so it is a characterization test.
  - Delete `ProviderReturnsUnlimitedByDefault`. Its contract ends with this milestone.
- [x] **Step 2: Run the filter `AtlasBudgetAdmissionTests`. Expect the three boundary tests to fail.** Observed on 2026-11-21: 16 tests ran. The three boundary tests failed with `None` where each expected its budget refusal.
- [x] **Step 3: Implement B1 to B4.** Update the doc comments with the reasons.
- [x] **Step 4: Run the filters `AtlasBudgetAdmissionTests` and `AtlasEndToEndTests`. Every test passes.** The first run on 2026-11-21 failed four end-to-end tests that share the split fixture. Its source is 8 by 8 texels with anisotropy, so the page border of 16 texels puts the group far above 5/4. The fixture tests grouping and cleanup, not the budget. So `NewSplitFixture` widens only the group factor, and the tear down restores the provider. The README states this refusal for small textures. The rerun on 2026-11-21 covered every atlas test class: 241 of 241 passed.

### Task 2: Relock regression fixture

- [x] Set `AMUSE_REAL_THRY_LOCK` to `1` in the dev editor process. Run the gated classes `BundledLockedOriginalRealLockTests`, `RealThryLockCharacterizationTests`, and `TransientUnlockRealLockTests`. Clear the variable afterward. Record the counts. Expect the 17 gated tests to run and pass. Observed on 2026-11-21: the three classes ran 30 tests, with 30 passed, 0 failed, and 0 skipped, in about 21 minutes. No skip means that the 17 gated tests ran. The variable was cleared before the full run.

### Task 3: README

**Files:**
- Modify: `README.md`, `Packages/com.alrauna.amuse/README.md`, `Packages/com.alrauna.amuse/CHANGELOG.md`

- [x] Add an `Atlasing` section to both README files. It states the supported cases, the refusals, the settings, the budget values, the one-GPU anisotropy limit, and that 0.2.0 does not reduce draw calls or material slots.
- [x] Add a Milestone 5 entry to the changelog.
- [x] Sweep the changed documents for private identifiers. The sweep on 2026-11-21 found no hit.

### Task 4: Close

- [x] Run both full assemblies. Record the counts.
- [x] Update the master plan.
