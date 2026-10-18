# Test Fixture Cutout Minimum Texture Size and Staging Scope Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair test helpers to pin cutout minimum texture size to All Sizes (-1), resolving all 24 failing EditMode tests.

**Architecture:** Pure test-helper repair across two test files. In `FixtureProofScope.PinAllSizes`, pin `_cutoutPreserveTransparencyMinTextureSize = AllSizes` (-1). In `AlphaSeparationDryRunTests.AddOptimizer`, pin `_cutoutPreserveTransparencyMinTextureSize = -1`. Zero changes to production code.

**Tech Stack:** Unity 2022.3.22f1, C#, NDMF, NUnit via Unity Test Framework, EditMode only.

**Spec:** `docs/superpowers/specs/2026-10-18-test-fixture-cutout-scope-repair-design.md`
**Investigation:** `docs/superpowers/investigations/2026-10-18-test-failure-diagnosis-investigation.md`

## Working discipline

- Branch `feat/inspector-ux-indentation`. Work directly on this branch.
- Allowed mutations, exhaustive:
  - `Packages/com.alrauna.amuse/Tests/Editor/Build/FixtureProofScope.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`
- Never touch:
  - Production code in `Packages/com.alrauna.amuse/Editor/` or `Runtime/`
  - `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated `*.csproj`/`*.sln`
  - Vendor packages under `Packages/` other than `com.alrauna.amuse`
  - The research package
  - Prior superpower documents dated before 2026-10-18
- Every task ends with exactly one commit of the paths the task names.
- Simple English for every document, comment, and commit message: short active sentences, no contractions, no semicolons.

## Unity validation procedure (every task)

1. Enumerate the reachable Unity instances read-only (resource `mcpforunity://instances`). Pin the dev editor instance where `Application.dataPath` resolves to `<repo-root>/Assets`.
2. Inspect `mcpforunity://editor/state` and verify the editor is not playing and not compiling.
3. Refresh assets with `refresh_unity`, then inspect console for errors.
4. Run named tests with `run_tests` (EditMode). Record observed pass and fail counts.

## Global constraints

- No changes to production code or component defaults.
- All 24 tests that failed on 2026-10-18 must pass.
- Full test suites `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` must be green.

---

### Task 1: Pin Cutout Minimum Texture Size in FixtureProofScope

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/FixtureProofScope.cs:31-35`
- Test:
  - `AlphaSeparationPreparationTests`
  - `AlphaSeparationApplyTests`
  - `AmusePlatformFinishPluginTests`
  - `AmuseReportsSlotTests`

**Interfaces:**
- Consumes: `FixtureProofScope.AllSizes` (-1)
- Produces: `FixtureProofScope.PinAllSizes(GameObject root)` sets both `_preserveTransparencyMinTextureSize` and `_cutoutPreserveTransparencyMinTextureSize` to -1.

- [ ] **Step 1: Verify current failing tests**

Run the 18 failing tests covered by `FixtureProofScope.PinAllSizes`:
- 10 in `AlphaSeparationApplyTests`
- 5 in `AlphaSeparationPreparationTests`
- 1 in `AmusePlatformFinishPluginTests`
- 2 in `AmuseReportsSlotTests`

Observe 18 failures with `MissingTextureEvidence`, null candidates, or `KeyNotFoundException`.

- [ ] **Step 2: Implement minimal change in FixtureProofScope.cs**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/FixtureProofScope.cs`, inside `PinAllSizes`:

```csharp
            var serialized = new SerializedObject(optimizer);
            serialized.FindProperty("_preserveTransparencyMinTextureSize")
                .intValue = AllSizes;
            serialized.FindProperty("_cutoutPreserveTransparencyMinTextureSize")
                .intValue = AllSizes;
            serialized.ApplyModifiedPropertiesWithoutUndo();
```

- [ ] **Step 3: Run the 18 tests to verify they pass**

Run `run_tests` on the four test fixtures.
Expected: PASS (all 18 tests pass).

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/FixtureProofScope.cs
git commit -m "test: pin cutout minimum texture size in FixtureProofScope"
```

---

### Task 2: Pin Cutout Minimum Texture Size in DryRun AddOptimizer

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs:85-98`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Consumes: None
- Produces: `AlphaSeparationDryRunTests.AddOptimizer(GameObject owner)` sets both `_preserveTransparencyMinTextureSize` and `_cutoutPreserveTransparencyMinTextureSize` to -1.

- [ ] **Step 1: Verify current failing dry run tests**

Run the six failing tests in `AlphaSeparationDryRunTests`:
- `AnisotropyRefusalProducesRefusedSentence`
- `CutoutSplitSlotAllProvenProducesConvertableSentence`
- `CutoutSplitSlotProducesSplittableSentence`
- `DryRunVerdictsMatchTheBuildOnAMixedRenderer`
- `FoldSlotProducesConvertableSentence`
- `StreamingPngTextureDecodesInMemoryAndDoesNotRefuse`

Observe six failures returning `"Untouchable: 0 out of N polygons may be moved"`.

- [ ] **Step 2: Implement minimal change in AlphaSeparationDryRunTests.cs**

In `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`, inside `AddOptimizer`:

```csharp
            var serialized = new SerializedObject(optimizer);
            serialized.FindProperty("_preserveTransparencyMinTextureSize")
                .intValue = -1;
            serialized.FindProperty("_cutoutPreserveTransparencyMinTextureSize")
                .intValue = -1;
            serialized.FindProperty("_acceptDesktopAnisotropyDifferences")
                .boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
```

- [ ] **Step 3: Run dry run tests to verify they pass**

Run `run_tests` on `AlphaSeparationDryRunTests`.
Expected: PASS (all tests pass).

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs
git commit -m "test: pin cutout minimum texture size in dry run test helper"
```

---

### Task 3: Full Test Suite Verification

**Files:**
- Test: Full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies.

- [ ] **Step 1: Run full product test assembly**

Run `run_tests` for mode `EditMode` and assembly `Alrauna.Amuse.Tests.Editor`.
Expected: All tests pass (0 failures).

- [ ] **Step 2: Run full research test assembly**

Run `run_tests` for mode `EditMode` and assembly `Alrauna.Amuse.Research.Tests.Editor`.
Expected: 140 passed, 0 failed, 4 skipped.

- [ ] **Step 3: Whitespace and identifier sweep**

Run `git diff --check` and verify clean whitespace.
Verify zero private identifiers in changed files.
