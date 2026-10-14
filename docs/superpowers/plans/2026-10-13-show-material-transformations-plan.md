# Show Material Transformations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Show Material Transformations" foldout with a nested "Alpha Separation" foldout to the AmuseAvatarOptimizer inspector. It runs the real proof pipeline on the scene avatar at edit time and draws what would move, fold, stay, or refuse, per renderer and per slot.

**Architecture:** A new `Editor/DryRun/` module orchestrates read-only reuse of Host capture, Analysis classification, and Semantics frontends. The runtime-state pipeline, the policy mapping, and the slot verdict gates extract from the build pass and from preparation into `Editor/Analysis/`, so the build and the dry run share one classifier. A composite-fingerprint session cache keeps repeated opens cheap. The inspector draws pure row records and honest boundary disclosures.

**Tech Stack:** Unity 2022.3 editor APIs, IMGUI, NDMF 1.14.4 build state types, NUnit EditMode tests through the Unity Test Runner.

**Spec:** `docs/superpowers/specs/2026-10-13-show-material-transformations-design.md`

**Investigation:** `docs/superpowers/investigations/2026-10-13-show-material-transformations-investigation.md`

## Working discipline

Branch: `feat/show-material-transformations`. Before Task 1, record the execution-start head as the plan base: `git rev-parse HEAD`. Task 9 diffs against that recorded base.

Allowed mutations, exhaustive:

- `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (extraction retargeting and policy wrapper delegation only)
- `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs` (Task 1 caller retarget and Task 2 gate delegation only)
- `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` (one pure interpolation helper addition only)
- `Packages/com.alrauna.amuse/Editor/Analysis/` (new files `RuntimeStateResolution.cs`, `SlotTransformDecision.cs` plus `.meta` files)
- `Packages/com.alrauna.amuse/Editor/DryRun/` (new module, including its folder `.meta`)
- `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- `Packages/com.alrauna.amuse/README.md`
- `Packages/com.alrauna.amuse/Tests/Editor/` (new `DryRun/` folder including its folder `.meta`, new `Analysis/SlotTransformDecisionTests.cs` and `Analysis/RuntimeStateResolutionTests.cs`, new `Build/AmuseReportStringFormatTests.cs`, the named retargets, the host-window harness completion probe)

Never touch: anything under `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Packages/nadena.dev.ndmf/`, `Packages/com.alrauna.amuse.research/`, anything under `docs/superpowers/` dated before 2026-10-13, the two untracked `Assets/AmuseTests_Temp*.meta` files at the repository root, source meshes, materials, textures, import settings, prefabs, or scenes. Unity assets and `.meta` files move as one unit. A new `.cs` file gets its `.meta` from Unity refresh, and both land in the same commit. A new folder's `.meta` lands in the commit of the task that creates the folder.

Every task ends with exactly one commit of the paths the task names. Never stage an unlisted path.

Each task that creates files sweeps them before committing: no drive-letter paths, no home paths, no four-digit ports, no at sign joined to a hexadecimal hash, no private asset names. A hit is a defect. Fix it before the commit.

Stop and return evidence when: any existing test changes outcome after an extraction. Edit-time capture fails on the stand-in fixtures for an unnamed reason. The dry run cannot complete without persisting state beyond the named transients. A display row needs a sentence no existing report string covers and the report table would have to grow.

Unity validation discipline: after creating or changing files, call the Unity MCP `refresh_unity` tool and wait for readiness. Run tests through the Unity MCP `run_tests` tool and poll `get_test_job`. A filtered run that reports zero tests is a failure. Record observed pass and fail counts in the task report. RED means: run the new test, observe the failure, record it against the named wrong implementation, then apply the minimal production change. A first-run pass is recorded as characterization, never dressed up as RED. Compile failure because a new type does not exist yet is an honest RED for a new type.

Falsifier house style: a `--- Falsifier N: name ---` comment in the test file names the plausible wrong implementation the assertion kills, and the fixture carries a no-op guard assertion so later changes cannot silence it. Every falsifier in this plan carries its guard.

## Global constraints

- Editor-only production code. One production assembly, `Alrauna.Amuse.Editor`. New namespaces: `Alrauna.Amuse.Editor.Analysis` for the two extracted types, `Alrauna.Amuse.Editor.DryRun` for the new module.
- File name equals type name. One type per file, `internal` where possible. `Editor/AssemblyInfo.cs` already grants `InternalsVisibleTo` to the test assembly.
- Every label, tooltip, row sentence, disclosure sentence, and README sentence uses short active sentences. No contractions. One idea per sentence. No semicolons in prose. Count sentences avoid plural nouns, per the spec's Inspector section. Copy the spec's sentences verbatim.
- XML doc comments state why, not what.
- Fail closed through named values. No blanket catch converts a programming defect into a refusal row. An unexpected exception in the dry run surfaces as a console error and marks the errored display state.
- The dry run persists nothing. No asset, no import change, no scene write, no component write. It never calls `UnityStreamingTextureEvidence`, `AlphaSeparationPreparation.Prepare`, `AlphaSeparationApply`, or any clone builder.
- The dry run emits no NDMF report entry and adds no report string key. Rows that draw a table-sourced sentence, refusal and capture refusal rows, assert `AmuseReportStrings.Has(key)` before they render. Builder-local sentences from the spec's Inspector section carry no key and need no guard. An empty table-sourced sentence is a test failure, never a displayable row.
- The dry run never calls any symbol in the `Alrauna.Amuse.Editor.Build` namespace except the three the spec's decision 3 names: `AmuseReportStrings` with its key builders, `Get`, and `Has`, `PlatformAlphaSamplingPolicy.TrySelect`, and `LockedMaterialIdentity.PreCheckRefusal`. All three are pure reads.
- No test calls `AlphaSeparationPreparation.Prepare` directly. Build-side fixtures drive the plugin barrier with the `RunBarrier` harness and set component fields through the serialized object.
- No test switches the real active build target. The build target reaches the dry run through an injectable provider.
- Extraction tasks move code verbatim. Visibility promotion from `private` to `internal` is the only change inside moved bodies. Any logic edit inside a moved member is a defect.

---

### Task 1: Extract the runtime-state pipeline into Analysis

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs` (plus `.meta`)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/RuntimeStateResolutionTests.cs` (plus `.meta`)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs` (the `MaterialSlotsFor` caller retarget at line 358)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (retargets the `AnalyzeRuntimeStatesForTests` consumer)

**Interfaces:**

- Consumes: existing Host evidence records, `MeshSeparationPlan`, `SlotResolutionResult`, `AlphaSamplingPolicy`. All already referenced by the moved code. `AlphaPolicyBounds` is not part of the moved surface. It stays a call-site input.
- Produces: `internal static class RuntimeStateResolution` in namespace `Alrauna.Amuse.Editor.Analysis` exposing, all `internal static`: `ResolveRuntimeStates`, `ClassifyRuntimeStates`, `MaterialSlotsFor`, `ClosureWayForSlot`, and the internal nested type `ResolvedRuntimeStates`. Also exposes the policy mapping as seven `internal static` helpers that take plain values: `ProofMipCapFrom(int storedMaxMipLevel)`, `MinTextureSizeFrom(int storedMinTextureSize)`, `MinimumCoverageFrom(int storedCoveragePercent)`, `MinimumPreserveCoverageFrom(int storedPreservePercent)`, `OpaquePercentFrom(int storedOpaqueAlphaPercent)`, `PolygonClampPercentFrom(int storedClampPercent)`, `DensityCapFromCoveragePercent(int storedCoveragePercent)`. Bodies verbatim from the pass members after the field read.

- [ ] **Step 1: Move the pipeline members**

Create `RuntimeStateResolution.cs` with the namespace `Alrauna.Amuse.Editor.Analysis` and the using directives the moved bodies need. Move these members from `Editor/Build/AmusePlatformFinishPlugin.cs` verbatim, promoting `private` to `internal`:

- `ResolvedRuntimeStates` (nested class, line 1368)
- `ResolveRuntimeStates` (line 1402)
- `FilterBlockEntriesForSlot` (line 1594)
- `Refused` (line 1637)
- `ClosureWayForSlot` (line 1622)
- `ClassifyRuntimeStates` (line 1652)
- `IntersectResolvedOutcomes` (line 1723)
- `UnprovenOutcomes` (line 1764)
- `MaterialSlotsFor` (line 1780, already internal)
- `NoAlphaChannelPropertiesFor` (line 1846)
- `CaptureRefusalsFor` (line 1885)

Carry each member's XML doc comment with it. If a moved body references a pass member not on this list, stop and report.

- [ ] **Step 2: Split the policy helpers**

For each of the seven policy mapping helpers, move the mapping body verbatim into `RuntimeStateResolution` as an int-taking helper and leave an `internal static` wrapper on `AmusePlatformFinishPass` with the original name and original `AmuseAvatarOptimizer` parameter:

```csharp
internal static int ProofMipCapFrom(AmuseAvatarOptimizer optimizer)
{
    if (optimizer == null)
    {
        return int.MaxValue;
    }
    return RuntimeStateResolution.ProofMipCapFrom(
        optimizer.PreserveTransparencyMaxMipLevel);
}
```

The null branch and the field read stay in the wrapper exactly as today. The mapping body moves verbatim. Apply the same shape to all seven, preserving each helper's own null-to-inert value: `int.MaxValue`, `1`, `0`, `0`, `100`, `0`, and `0` respectively, exactly as the current bodies map them. The pass call sites at lines 605 to 613 keep calling the wrappers. The existing reflection tests `PolicyMappersAreInertWithoutAnOptimizerComponent`, `PolicyMappersClampOutOfRangeStoredValuesDefensively`, `InBandClampPairMapsToStoredClampUnchanged`, and `DensityMapperRejectsAboveRangePolicyAtWiringTime` keep passing through the wrappers.

- [ ] **Step 3: Retarget the pass and the other callers**

In `AmusePlatformFinishPlugin.cs`, delete the eleven moved pipeline members and add `using Alrauna.Amuse.Editor.Analysis;` if absent. Retarget `AnalyzeRuntimeStatesForTests` to the new home. Qualify the `AlphaSeparationPreparation.cs:358` call as `RuntimeStateResolution.MaterialSlotsFor`. Verify no definition remains outside the new file:

Run: `grep -rn "MaterialSlotsFor\|ResolveRuntimeStates\|ClassifyRuntimeStates\|ClosureWayForSlot\|FilterBlockEntriesForSlot\|IntersectResolvedOutcomes\|UnprovenOutcomes\|NoAlphaChannelPropertiesFor\|CaptureRefusalsFor\|ProofMipCapFrom\|MinTextureSizeFrom\|MinimumCoverageFrom\|MinimumPreserveCoverageFrom\|OpaquePercentFrom\|PolygonClampPercentFrom\|DensityCapFromCoveragePercent\|ResolvedRuntimeStates" Packages/com.alrauna.amuse/Editor Packages/com.alrauna.amuse/Tests | grep -v "Analysis/RuntimeStateResolution.cs"`

Expected: only call sites and the seven pass wrappers, no moved-body definitions elsewhere.

- [ ] **Step 4: Add the thin test class**

`RuntimeStateResolutionTests` with exactly two direct behavior asserts and no delegation asserts: one closure-refused slot resolves to a closure failure result, and one unproven slot resolves to unproven outcomes. Build inputs by hand from the Host evidence records, in the style of the existing resolution tests.

- [ ] **Step 5: Refresh and run the barrier suite**

Refresh Unity and wait for readiness. Run the full `Alrauna.Amuse.Tests.Editor` assembly through the Unity MCP test runner.

Expected: every previously passing test passes, including the four policy reflection tests. This is a pure move. Record observed counts. Any changed outcome is the plan's stop condition: stop and report.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs \
  Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs.meta \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/RuntimeStateResolutionTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/RuntimeStateResolutionTests.cs.meta \
  Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
  Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "refactor: extract runtime state resolution into analysis"
```

---

### Task 2: Extract the slot verdict function into Analysis

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/Analysis/SlotTransformDecision.cs` (plus `.meta`)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/SlotTransformDecisionTests.cs` (plus `.meta`)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`

**Interfaces:**

- Consumes: `MeshSeparationPlan`, `SubmeshSeparationPlan`, `SubmeshSeparationDisposition`, `MeshSeparationPlanner.Folded`, `MeshSeparationPlanner.WithFoldedSubmeshes`, `SlotResolutionResult` with its `HasAnisotropicSampling` property. All exist in Analysis today.
- Produces, in `Alrauna.Amuse.Editor.Analysis`:

```csharp
internal enum SlotTransformOutcome
{
    None,
    Split,
    Fold,
    CoverageBelowMinimum,
    AnisotropyDifferences,
    AlreadyOpaque,
}

internal readonly struct SlotTransformVerdict
{
    internal int SlotIndex { get; }
    internal SlotTransformOutcome Outcome { get; }
    internal int ProvenTriangles { get; }
    internal int TotalTriangles { get; }
    internal int FoldedUnprovenTriangles { get; }
}

internal static class SlotTransformDecision
{
    // One decision per material slot, in ascending slot index order.
    // Decision ladder, fixed: the preserve fold gate first. Then, for a
    // slot whose every submesh stayed Unchanged, AnisotropyDifferences
    // when the slot result carries HasAnisotropicSampling. Then, on a
    // slot with Split submeshes, CoverageBelowMinimum when the convert
    // coverage predicate refuses. Then Split. AlreadyOpaque comes from
    // the caller-supplied per-slot flag and wins over everything. A
    // slot with no plan entry and no flag gets None.
    internal static IReadOnlyList<SlotTransformVerdict> Decide(
        MeshSeparationPlan plan,
        IReadOnlyList<SlotResolutionResult> slotResults,
        IReadOnlyList<bool> alreadyOpaqueBySlot,
        int minimumOpaqueCoveragePercent,
        int minimumOpaqueCoverageToPreservePercent)

    // The preserve gate predicate, moved verbatim from preparation.
    internal static bool PreserveSlotQualifies(
        IReadOnlyList<SubmeshSeparationPlan> slotEntries,
        int minimumOpaqueCoverageToPreservePercent)

    // The convert coverage gate predicate, moved verbatim.
    internal static bool OpaqueCoverageBelow(
        int minimumOpaqueCoveragePercent,
        SubmeshSeparationPlan submesh)
}
```

- [ ] **Step 1: Write the failing tests**

Write `SlotTransformDecisionTests.cs` with behavior-sentence names, NUnit constraint model, the two falsifiers, and their no-op guards:

- `BelowConvertCoverageSlotRefusesWithCoverageBelowMinimum`: proven share strictly below the minimum yields `CoverageBelowMinimum`.
- `AtConvertCoverageSlotSplits`: a share exactly at the minimum yields `Split`. The predicate proceeds at the boundary.
- `PreserveQualifyingSlotFolds`: every submesh `Split` with unproven share strictly below the preserve value yields `Fold` with `FoldedUnprovenTriangles` set.
- `PreserveValueOfZeroQualifiesNothing`: preserve 0 yields `Split`, never `Fold`. `--- Falsifier 1: gate-skipping split ---` a Decide that skips the convert coverage gate labels the below-threshold fixture `Split`. `--- Falsifier 2: gate-skipping fold ---` a Decide that skips the preserve gate leaves the qualifying fixture on `Split`. Guard for both: assert a plan entry never yields `None`.
- `AnisotropyRefusesOnlyWhenEverySubmeshUnchanged`: `HasAnisotropicSampling` on a slot whose submeshes are all `Unchanged` yields `AnisotropyDifferences`. `HasAnisotropicSampling` on a slot with a `Split` submesh does not.
- `AlreadyOpaqueWinsOverSplit`: the caller flag true yields `AlreadyOpaque` even with a qualifying plan entry.

Mark the file header with the falsifier blocks per house style.

- [ ] **Step 2: Run the tests, observe RED**

Run the filter `SlotTransformDecisionTests`. Expected: compile failure, the type does not exist. Record the observed failure against the named wrong implementations.

- [ ] **Step 3: Write the minimal type**

Implement `SlotTransformDecision` in the order and with the precedence the interface comment fixes. Move the two predicates verbatim: the preserve condition from `AlphaSeparationPreparation.cs:142-160`, and `OpaqueCoverageBelow` from `:778-790`. `Decide` groups `plan.Submeshes` by `SourceMaterialBindingIndex` and applies the ladder. Reuse `MeshSeparationPlanner.Folded` semantics for `FoldedUnprovenTriangles` without mutating the plan. XML doc comments state why: the dry run and preparation must share one verdict so the dropdown can never drift from the build.

- [ ] **Step 4: Delegate preparation to the shared predicates**

In `AlphaSeparationPreparation.cs`, replace the inline preserve condition with a call to `SlotTransformDecision.PreserveSlotQualifies`, and every `OpaqueCoverageBelow` call site with `SlotTransformDecision.OpaqueCoverageBelow`. Delete the local `OpaqueCoverageBelow`. No other behavior edits. The `RunBarrier` suite is the behavior pin.

- [ ] **Step 5: Run the tests, observe GREEN**

Run `SlotTransformDecisionTests`, then the full `Alrauna.Amuse.Tests.Editor` assembly. Expected: new tests pass with observed counts, every previously passing test unchanged.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/SlotTransformDecision.cs \
  Packages/com.alrauna.amuse/Editor/Analysis/SlotTransformDecision.cs.meta \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/SlotTransformDecisionTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/SlotTransformDecisionTests.cs.meta \
  Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs
git commit -m "refactor: extract slot transform decision shared by prepare and dry run"
```

---

### Task 3: Add the pure interpolation helper to the report strings

**Files:**

- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringFormatTests.cs` (plus `.meta`)

**Interfaces:**

- Produces: `internal static string Format(string key, params string[] args)` on `AmuseReportStrings`. It returns the empty string when `Has(key)` is false. Otherwise it takes `Get(key)` and replaces each `{N}` token with `args[N]`, leaving unknown tokens untouched.

- [ ] **Step 1: Write the failing test**

`AmuseReportStringFormatTests` with two behavior sentences: `FormatReplacesNumberedTokens` and `FormatOfUnknownKeyIsEmpty`. Assert the empty result on an unknown key, because the dry run's `Has` guard depends on it. Run the filter, observe the compile failure, record RED.

- [ ] **Step 2: Implement the helper**

Add the helper beside `Get` in `AmuseReportStrings.cs`. Use `string.Replace` per token index actually supplied. No new string keys. XML doc states why: the console substitutes through SimpleError at emission and keeps that path unchanged. The dry run needs the same sentence text without an error object. This records the deliberate deviation from the spec's first-issue wording, corrected in the spec's second revision.

- [ ] **Step 3: Run GREEN and commit**

Run the new filter plus the existing `AmuseReportStringsTests`. Expected: all green, observed counts recorded.

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringFormatTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringFormatTests.cs.meta
git commit -m "feat: add pure report sentence formatting for display rows"
```

---

### Task 4: Add the display row model

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs` (plus `.meta`)
- Create: `Packages/com.alrauna.amuse/Editor/DryRun.meta` (folder meta)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs` (plus `.meta`)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/DryRun.meta` (folder meta)

**Interfaces:**

- Produces, in `Alrauna.Amuse.Editor.DryRun`:

```csharp
internal enum MaterialTransformationRowKind
{
    Banner,
    Disclosure,
    Note,
    Renderer,
    Slot,
    Errored,
}

internal sealed class MaterialTransformationRow
{
    internal MaterialTransformationRowKind Kind { get; }
    internal string RendererPath { get; }      // null on avatar-level rows
    internal string RendererTypeName { get; }  // null on avatar-level rows
    internal int SlotIndex { get; }            // -1 on non-slot rows
    internal int MovedTriangles { get; }       // -1 when not stated
    internal int ProvenTriangles { get; }      // -1 when not stated
    internal int TotalTriangles { get; }       // -1 when not stated
    internal int RefusedSlots { get; }         // -1 when not stated
    internal int SlotCount { get; }            // -1 when not stated
    internal string Sentence { get; }          // display text, never null

    internal static MaterialTransformationRow Banner(string sentence)
    internal static MaterialTransformationRow Note(string sentence)
    internal static MaterialTransformationRow Disclosure(string sentence)
    internal static MaterialTransformationRow Errored(string rendererPath, string rendererTypeName, string sentence)
    internal static MaterialTransformationRow Renderer(
        string path, string typeName, int moved, int proven, int total,
        int refusedSlots, int slotCount, string sentence)
    internal static MaterialTransformationRow Slot(
        string path, string typeName, int slotIndex,
        int proven, int total, string sentence)
}

internal static class RowText
{
    // Spec Inspector sentences, verbatim, counts interpolated.
    internal static string SplitRowSentence(int slotIndex, int proven, int total)
    internal static string FoldRowSentence(int slotIndex)
    internal static string AlreadyOpaqueRowSentence(int slotIndex)
    internal static string UnchangedRowSentence(int slotIndex, int proven, int total)
    internal static string RefusedRowSentence(int slotIndex, string refusalSentence)
    internal static string RendererSummarySentence(
        string path, string typeName, int moved, int proven, int total,
        int refusedSlots, int slotCount, bool foldContributed)
    internal static string RendererNoMovementSentence(
        string path, string typeName, int proven, int total)
    internal static string RendererNoChangeSentence(string path, string typeName)
    internal static string RendererRefusedSentence(string path, string typeName, string refusalSentence)
    internal static string StructuralRowSentence(string prefix, string sentence)
    internal static string BannerSentence(BannerKind kind)
    internal static string NoteSentence(NoteKind kind)
    internal static string DisclosureSentence(int index)
    internal static string ErroredSentence(string path, string typeName)
    internal static string NoRenderersSentence()
}
```

`RendererSummarySentence` appends the spec's fold sentence when `foldContributed` is true. `BannerKind` and `NoteKind` are internal enums listing the spec's banner and note rows. `DisclosureSentence(int)` returns the spec's disclosure rows by index.

- [ ] **Step 1: Write the failing tests**

`MaterialTransformationRowTests` covers, per the spec's test strategy: would-split, fold, already opaque, unchanged, refused, the mixed-refusal renderer summary, the no-movement renderer summary, the no-change renderer summary, the banner and note and disclosure and errored sentences, and the count fields on every factory. Factories never return a null sentence.

`--- Falsifier 3: total-instead-of-proven ---` a row builder fed by the orchestrator that passes total triangles where proven triangles belong renders a fully unproven slot as a split claim. Two assertions kill it. First, `SplitRowSentence(2, 0, 40)` returns the unchanged sentence, exactly as the spec words it. Second, `SplitRowSentence(2, 12, 40)` returns the split sentence with both counts in the right order. Guard: assert `RowText` never returns a null or empty sentence for any builder.

Run the filter, observe RED (the type does not exist), record it.

- [ ] **Step 2: Implement the model and sentence builders**

Minimal records and builders. Every refusal and capture refusal sentence goes through `AmuseReportStrings` with `Has` then `Format`, per the scoped global constraint. The banner, note, disclosure, fold, already opaque, unchanged share, no-renderer, and errored sentences are builder-local spec text. Copy them verbatim from the spec's Inspector section.

- [ ] **Step 3: Run GREEN, sweep, and commit**

Run `MaterialTransformationRowTests`. Expected: green, observed counts. Sweep the new files for identifiers.

```bash
git add Packages/com.alrauna.amuse/Editor/DryRun.meta \
  Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs \
  Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs.meta \
  Packages/com.alrauna.amuse/Tests/Editor/DryRun.meta \
  Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs.meta
git commit -m "feat: add pure transformation row model with spec sentences"
```

---

### Task 5: Add the fingerprint cache

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/DryRun/DryRunCache.cs` (plus `.meta`)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/DryRunCacheTests.cs` (plus `.meta`)

**Interfaces:**

- Produces, in `Alrauna.Amuse.Editor.DryRun`:

```csharp
// One immutable record of everything cheap the fingerprints read.
// Production fills it from the scene. Tests build it by hand.
internal readonly struct DryRunFingerprintInputs
{
    internal int AvatarRootInstanceId { get; }
    internal BuildTarget ActiveTarget { get; }
    internal string PolicyFingerprint { get; }      // all thirteen component values
    internal string ControllerFingerprint { get; }  // controller identities and ticks
    internal string GraphFingerprint { get; }       // structural graph outcome
    internal string RendererFingerprintBody { get; } // path, type, mesh, materials, textures
}

internal static class DryRunFingerprint
{
    internal static string Avatar(in DryRunFingerprintInputs inputs);
    internal static string Renderer(in DryRunFingerprintInputs inputs);
    internal static string Composite(string avatar, string renderer);
}

internal static class DryRunCache
{
    internal static bool TryGetAvatar(string avatarKey, out IReadOnlyList<MaterialTransformationRow> rows);
    internal static void StoreAvatar(string avatarKey, IReadOnlyList<MaterialTransformationRow> rows);
    internal static bool TryGetRenderer(string compositeKey, out IReadOnlyList<MaterialTransformationRow> rows);
    internal static void StoreRenderer(string avatarKey, string rendererKey,
        IReadOnlyList<MaterialTransformationRow> rows);
    internal static void MarkErrored(string compositeKey);
    internal static bool IsErrored(string compositeKey);
    internal static void Clear();
}
```

`PolicyFingerprint` concatenates the thirteen component values from the spec's component state section. `ActiveTarget` participates in the avatar fingerprint, so a platform switch invalidates everything. `ControllerFingerprint` concatenates, per controller asset: the asset GUID when the asset is persistent, otherwise the instance ID, plus the asset file ticks when a path exists. The builder never calls `File.GetLastWriteTimeUtc` on an empty path. It falls back to the instance ID for non-persistent assets. `RendererFingerprintBody` concatenates renderer path and type, mesh identity, each slot material identity, and each texture identity. Identity is asset GUID plus source and meta file ticks for persistent assets, instance ID plus update count otherwise. The orchestrator hands in the alpha-request selection so only alpha-relevant textures participate.

- [ ] **Step 1: Write the failing tests**

`DryRunCacheTests` with the falsifiers and their no-op guards:

- `AvatarSwapEvictsPreviousAvatarRows`. `--- Falsifier 4: cross-avatar serve ---` a cache keyed by renderer fingerprint alone serves avatar B the rows stored for avatar A when both share renderer paths. The test stores rows for avatar one, changes only the avatar root instance ID in the inputs, and asserts the miss. Guard: assert a same-fingerprint re-lookup hits.
- `PolicyChangeInvalidatesRendererRows`. `--- Falsifier 5: stale policy rows ---` a cache keyed without policy serves rows computed under the old slider. The test changes only `PolicyFingerprint` and asserts the miss.
- `TargetChangeInvalidatesAvatarRows`: the test changes only `ActiveTarget` and asserts the avatar key changes.
- `ErroredStateSurvivesUntilClear`: `MarkErrored` then `IsErrored` true, then `Clear` makes it false.
- `StoreRendererIsSingleSlotPerAvatar`: storing under avatar two removes avatar one's entries.

Run the filter, observe RED (types do not exist), record it.

- [ ] **Step 2: Implement**

`DryRunFingerprint` builds the key strings with a separator that cannot appear in a path (`"\u001f"`), so concatenations cannot collide. `DryRunCache` holds one static avatar key plus dictionaries for avatar rows and renderer rows keyed by the composite string. `StoreRenderer` under an avatar key different from the stored one clears everything first. XML doc comments state why: one avatar slot keeps memory bounded and makes cross-avatar serves impossible.

- [ ] **Step 3: Run GREEN, sweep, and commit**

Run `DryRunCacheTests`. Expected: green, observed counts. Sweep the new files.

```bash
git add Packages/com.alrauna.amuse/Editor/DryRun/DryRunCache.cs \
  Packages/com.alrauna.amuse/Editor/DryRun/DryRunCache.cs.meta \
  Packages/com.alrauna.amuse/Tests/Editor/DryRun/DryRunCacheTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/DryRun/DryRunCacheTests.cs.meta
git commit -m "feat: add composite fingerprint session cache for dry run"
```

---

### Task 6: Add the dry run orchestrator

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/DryRun/EditorBuildTargetProvider.cs` (plus `.meta`)
- Create: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs` (plus `.meta`)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs` (plus `.meta`)

**Interfaces:**

- Consumes: `RuntimeStateResolution`, `SlotTransformDecision`, `CommittedControllerGraph`, `UnityAnimationEvidenceCapture.Capture`, `UnityMaterialSemantics.TrySelectAlphaMaterialRequests`, `EffectiveMaterialMaterialization` (`CaptureBlockState` and `MaterializeAdmitted`), `UnityRendererAlphaAnalysis`, `AmuseReportStrings`, `PlatformAlphaSamplingPolicy.TrySelect`, `LockedMaterialIdentity.PreCheckRefusal`, `MaterialTransformationRow`, `RowText`, `DryRunFingerprint`, `DryRunCache`.
- Produces, in `Alrauna.Amuse.Editor.DryRun`:

```csharp
// EditorBuildTargetProvider.cs: one type per file, Func-based injection.
internal sealed class EditorBuildTargetProvider
{
    internal static readonly EditorBuildTargetProvider Instance =
        new EditorBuildTargetProvider();
    internal BuildTarget Get() => EditorUserBuildSettings.activeBuildTarget;
}

// AlphaSeparationDryRun.cs
internal sealed class AlphaSeparationDryRun
{
    // The inspector sets this so the next draw pass advances.
    // No-op when unset. In the host-window test harness the inspector
    // wires it to the host window's Repaint.
    internal Action RepaintRequested { get; set; }

    internal AlphaSeparationDryRun(AmuseAvatarOptimizer component,
        Func<BuildTarget> buildTargetProvider)

    internal bool Done { get; }
    internal IReadOnlyList<MaterialTransformationRow> AvatarRows { get; }
    internal IReadOnlyList<MaterialTransformationRow> RendererRows { get; }

    internal void Begin()   // avatar facts, banners, graph, avatar cache
    internal bool Step()    // one renderer, probe, cache or full compute
}
```

- [ ] **Step 1: Write the failing tests**

`AlphaSeparationDryRunTests` builds scene fixtures at test start on a hidden root object with `HideFlags.HideAndDontSave`, stand-in cutout shaders under `Hidden/Alrauna/AmuseTests/*` written through the existing `TestShaderWriter`, and runtime-created textures. Use the canonical `TestTransientScope` helper for any temp asset folder, so cleanup cannot leak an untracked meta. Behavior sentences:

- `ProvenTriangleDisplaysAsWouldSplit`: one renderer, one cutout slot with a partially proven texture, some texels opaque and some fully transparent, produces the split slot row. Assert the exact `ProvenTriangles` and `TotalTriangles` counts and the exact sentence text. A partially proven fixture is required, so a call-site swap of proven and total cannot satisfy the assertion.
- `UnknownStaysUnchanged`: a fully transparent texture never produces a split row. Guard: assert no row names a split.
- `RendererRefusalShowsNamedSentence`: a renderer the analysis refuses produces the refused renderer row whose sentence passes through `AmuseReportStrings` and is non-empty.
- `TextureRefusalAttachesToSlotRow`: a fixture whose texture capture refuses produces the refusal text inside the slot row.
- `StructuralRefusalStopsWithStructuralRow`: a fixture clip with an animation event produces the structural row and `Done` true with no renderer rows.
- `MasterSwitchOffProducesBannerAndNoCompute`: `_amuseDisabled` true produces the AMUSE off banner, `Done` true, and no renderer rows. Same shape for the separator switch. Guard: a renderer count probe proves no enumeration happened.
- `ChildPlacementProducesBanner`: the component on a child produces the root banner and no renderer rows.
- `NonWindowsTargetProducesBannerAndContinues`: the injected provider returns a supported non-Windows target. The avatar rows hold the non-Windows banner, and the run still computes rows.
- `SamplingBannerSuppressesResults`: the injected provider returns an unsupported target. The avatar rows hold the sampling banner, `Done` true, and no transformation rows.
- `PropertyBlockChangesVerdictLikeTheBuild`: a fixture renderer with a `MaterialPropertyBlock` overriding the cutoff classifies exactly as a build would with the same block, through the effective materials wrapper.
- `AvatarCacheHitServesRenderersWithoutRecompute`: a second run over an unchanged fixture serves identical rows, with a probe proving the full compute did not run again.

Run the filter, observe RED (the types do not exist), record it.

- [ ] **Step 2: Implement Begin**

`Begin` runs once per open, wrapped in the same catch shape as Step: on an unexpected exception, `Debug.LogException`, mark the errored avatar state, and stop.

1. Read the component switches. AMUSE off or separator off: banner row, `Done = true`, return.
2. Component on child: root banner, done.
3. `PlatformAlphaSamplingPolicy.TrySelect(provider(), desktop, mobile, out policy)`. Failure produces the sampling banner and done. Success on a non-Windows target appends the non-Windows banner as a non-stopping row and continues.
4. `CommittedControllerGraph.Enumerate(root, VRChatPlatformAnimatorBindings.Instance)`. A refusal produces the structural row through `RowText.StructuralRowSentence` with the renderer-name prefix rule and done.
5. Scan the hierarchy for EditorOnly objects and emit the EditorOnly note when found. Emitted disclosure rows are the editor's job, not the orchestrator's.
6. Build the avatar fingerprint from a `DryRunFingerprintInputs` filled from the component, the active target, the controller identities, and the graph outcome. `DryRunCache.TryGetAvatar` on hit materializes only the avatar rows. Renderer rows still flow through `Step` and their composite lookups. Store the avatar rows at run completion in `Step`, not here.

- [ ] **Step 3: Implement Step**

`Step` advances one renderer per call. The renderer list is re-enumerated each call from `root.GetComponentsInChildren<Renderer>()`. A destroyed renderer is skipped.

1. Take the next renderer after the internally tracked index. Build the cheap alpha-request selection with `UnityMaterialSemantics.TrySelectAlphaMaterialRequests` over the slot materials.
2. Probe pass: run `UnityAnimationEvidenceCapture.Capture` with a no-op probe capturer to enumerate the admitted materials and their textures cheaply, with no GPU work. Build the renderer fingerprint and the composite key from it. A live RenderTexture among the admitted textures produces the Refresh-only note and forces the full compute path with no fingerprint store.
3. `DryRunCache.TryGetRenderer` on the composite key. Hit: serve the stored rows, advance, return. Errored mark: serve the errored row through `RowText.ErroredSentence`, advance, return.
4. Miss: run the real `UnityAnimationEvidenceCapture.Capture` with the full production argument list: renderer path, `renderer.sharedMaterials`, graph, `VRChatPlatformAnimatorBindings.Instance`, the derived policy bounds, `out admittedLiveMaterials`, the capturer, `_ignoreOutOfRangeMaterialSlots`, the renderer type name, `LockedMaterialIdentity.PreCheckRefusal`, `null` for the unlock swap view, and a null registered source lookup.
5. The capturer is the dry run's own `ThroughEffectiveMaterials(renderer, inner)` wrapper, mirroring the build's effective materials route: it materializes each batch through `EffectiveMaterialMaterialization.MaterializeAdmitted` so the proof reads block-overridden values, and destroys every clone in a finally. Its inner capturer intercepts streaming-mipmap textures before any dispatch, skips them, and records the streaming fact for the avatar note. The wrapper lives in the dry run file set. `EffectiveMaterialMaterialization` is Host-owned, so this is legal.
6. `RuntimeStateResolution.ResolveRuntimeStates` with the block state from `EffectiveMaterialMaterialization.CaptureBlockState(renderer)`. Then `RuntimeStateResolution.MaterialSlotsFor`. Branch on `resolved.Refusal` for the renderer refused row. Then `UnityRendererAlphaAnalysis.CaptureGeometry(renderer, resolved.CurrentMaterials)` for the mesh validation refusals. Then `ClassifyRuntimeStates`.
7. Derive `alreadyOpaqueBySlot` from the captured materials through the same per-family eligibility entries the conversion path consults, read only. Call `SlotTransformDecision.Decide` and build rows through `RowText`. Slot texture capture refusals, including the mip residency refusal, fold into their slot rows through the existing texture capture strings and the `Has` guard.
8. Wrap steps 4 through 7 in one `try`/`catch` that logs with `Debug.LogException` and calls `DryRunCache.MarkErrored`, then builds the errored row. This is the one catch in the dry run. It never produces a refusal row.
9. Store rows under the composite key unless the live-texture rule applied. Accumulate the streaming and consent note facts at the avatar level. When no renderer remains, store the avatar rows including accumulated notes, set `Done`, and invoke `RepaintRequested` after every computing pass so the sequence advances.

The dry run references only the three permitted Build symbols. Any other `Alrauna.Amuse.Editor.Build` symbol in the dry run file set is a defect.

- [ ] **Step 4: Add the no-persistence guard fixture**

Add `DryRunPersistsNothing` to the same test file. The fixture holds one streaming-mipmap texture, created through `TestTransientScope` with the importer flag, and one renderer with a property block. The injected provider returns an admitted Windows target. Assert the renderer row set is non-empty before the deltas, so the guard cannot pass vacuously. Before and after a full run: equal `Resources.FindObjectsOfTypeAll<Material>().Length`, equal project-wide asset counts and paths, and no streaming readback folder. A leaked transient clone or a reached clone route fails the test. The guard cannot catch a clone route that completes and cleans perfectly, and the test does not claim to.

- [ ] **Step 5: Run GREEN, sweep, and commit**

Run the orchestrator tests. Expected: green, observed counts. Sweep the new files.

```bash
git add Packages/com.alrauna.amuse/Editor/DryRun/EditorBuildTargetProvider.cs \
  Packages/com.alrauna.amuse/Editor/DryRun/EditorBuildTargetProvider.cs.meta \
  Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs \
  Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs.meta \
  Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs.meta
git commit -m "feat: add edit-time alpha separation dry run"
```

---

### Task 7: Draw the foldouts in the inspector

**Files:**

- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**

- Consumes: `AlphaSeparationDryRun`, `MaterialTransformationRow`, `DryRunCache`.
- Produces: two session bools `_materialTransformationsOpen` and `_alphaSeparationTransformationsOpen`; a private `DrawMaterialTransformations()` called in `OnInspectorGUI` immediately after the alpha separator block, before the Advanced Settings reveal, so the foldouts sit outside it, and before the `AmuseBuildStatusStore` block; a field holding the current `AlphaSeparationDryRun` instance, which owns its own renderer index; the editor's `Repaint` wired into the orchestrator's `RepaintRequested`, targeting the hosting view, which is the inspector window in production and the host window in the test harness.

- [ ] **Step 1: Extend the host window harness**

In `AmuseAvatarOptimizerEditorTests`, extend `RepaintHostWindow` with a completion probe: the window's `OnGUI` keeps calling `HostedEditor.OnInspectorGUI()` and repainting across frames until a `Func<bool>` supplied by the test returns true, then sets `Painted`. The existing single-pass behavior stays for the old tests through a default always-true probe.

- [ ] **Step 2: Write the failing tests**

Two `[UnityTest]` behavior sentences:

- `MaterialTransformationsFoldoutPaintsRows`: a one-renderer fixture avatar with the stand-in cutout material, both foldouts forced open by reflection, repaints until the dry run reports done, then asserts `Painted` and that the editor exposed at least one renderer row. Expose the rows for the test through an internal read-only property on the editor.
- `SeparatorOffPaintsBannerOnly`: separator switch off, repaint once, assert `Painted` and the banner row, no renderer rows.

Run the filters, observe RED (the foldout does not exist), record it.

- [ ] **Step 3: Implement the drawing**

Add to `AmuseAvatarOptimizerEditor.cs`:

- The two session bools beside `_settingsOpen`.
- `DrawMaterialTransformations()`: the top foldout with `EditorStyles.foldoutHeader`. Inside, the Refresh button (`GUILayout.Button("Refresh")` right aligned, calls `DryRunCache.Clear()` and resets the run instance), the nested foldout, then the two disclosure rows drawn unconditionally under the nested title through `RowText.DisclosureSentence`, then the orchestrator drive: create the run on first open, wire `RepaintRequested`, and advance only on `Event.current.type == EventType.Repaint`, so a compute never mutates the row set between a frame's Layout and Repaint events and at most one renderer computes per repaint. Draw `AvatarRows` and `RendererRows` as `EditorGUILayout.LabelField` lines, slot rows indented one level under their renderer with `EditorGUI.indentLevel`. Banner and warning rows draw as `EditorGUILayout.HelpBox` with the spec's message types. The errored row draws as a warning with its sentence. The no-renderers empty state draws when the avatar has no renderer.
- The Refresh button clears errored marks too. `OnInspectorGUI` calls `DrawMaterialTransformations()` immediately after the alpha separator toggle block.
- XML doc comments state why: the view shows outcomes, not policy, so it sits outside Advanced Settings.

Copy every sentence from the spec's Inspector section verbatim through `RowText`.

- [ ] **Step 4: Run GREEN, sweep, and commit**

Run the two new filters plus the full existing `AmuseAvatarOptimizerEditorTests`. Expected: green, observed counts. Sweep the changed files.

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs \
  Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs
git commit -m "feat: draw show material transformations foldout in inspector"
```

---

### Task 8: Document the feature

**Files:**

- Modify: `Packages/com.alrauna.amuse/README.md`

- [ ] **Step 1: Add the README section**

Insert the exact section from the spec's Documentation section after the Alpha Separator settings section. No other README edits.

- [ ] **Step 2: Sweep and commit**

Sweep the changed file for identifiers. Then commit.

```bash
git add Packages/com.alrauna.amuse/README.md
git commit -m "docs: document the show material transformations foldout"
```

---

### Task 9: Full validation

**Files:** none. Validation only.

- [ ] **Step 1: Run the full product assembly**

Refresh Unity, run the full `Alrauna.Amuse.Tests.Editor` assembly. Record observed pass and fail counts. Expected: zero failures, with the new tests present in the count.

- [ ] **Step 2: Run the research assembly**

Run the full `Alrauna.Amuse.Research.Tests.Editor` assembly. Record observed counts. Expected: zero failures.

- [ ] **Step 3: Inspect the branch**

Run `git status --porcelain` and `git diff --check`. Expected: clean tree except the two user-owned untracked temp meta files. A new stray `Assets/AmuseTests_Temp*` artifact is a fixture cleanup defect from a test task: fix the teardown and remove the artifact, it is not a stop condition. Run `git log --oneline <recorded-base>..HEAD` with the base recorded before Task 1 and confirm one commit per task. Sweep every file the branch touched for identifiers.

Report the observed counts, the commit list, and any deferred minors to the controller. Do not commit in this task.
