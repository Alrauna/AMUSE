# Show Material Transformations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Show Material Transformations" foldout with a nested "Alpha Separation" foldout to the AmuseAvatarOptimizer inspector. It runs the real proof pipeline on the scene avatar at edit time and draws what would move, fold, stay, or refuse, per renderer and per slot.

**Architecture:** A new `Editor/DryRun/` module orchestrates read-only reuse of Host capture, Analysis classification, and Semantics frontends. The runtime-state pipeline, the policy mapping, and the slot verdict gates extract from the build pass and from preparation into `Editor/Analysis/`, so the build and the dry run share one classifier. A composite-fingerprint session cache keeps repeated opens cheap. The inspector draws pure row records and honest boundary disclosures.

**Tech Stack:** Unity 2022.3 editor APIs, IMGUI, NDMF 1.14.4 build state types, NUnit EditMode tests through the Unity Test Runner.

**Spec:** `docs/superpowers/specs/2026-10-13-show-material-transformations-design.md`

**Investigation:** `docs/superpowers/investigations/2026-10-13-show-material-transformations-investigation.md`

## Working discipline

Branch: `feat/show-material-transformations`. Base commit for this plan: the head of the branch when execution starts. Record it as `git rev-parse HEAD` before Task 1.

Allowed mutations, exhaustive:

- `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (extraction retargeting only)
- `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs` (gate delegation only)
- `Packages/com.alrauna.amuse/Editor/Analysis/` (new files `RuntimeStateResolution.cs`, `SlotTransformDecision.cs` plus their `.meta` files)
- `Packages/com.alrauna.amuse/Editor/DryRun/` (new module)
- `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` (one pure interpolation helper addition only)
- `Packages/com.alrauna.amuse/README.md`
- `Packages/com.alrauna.amuse/Tests/Editor/` (new `DryRun/` folder, the named retargets, the host-window harness completion probe)

Never touch: anything under `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Packages/nadena.dev.ndmf/`, `Packages/com.alrauna.amuse.research/`, anything under `docs/superpowers/` dated before 2026-10-13, the two untracked `Assets/AmuseTests_Temp*.meta` files at the repository root, source meshes, materials, textures, import settings, prefabs, or scenes. Unity assets and `.meta` files move as one unit. A new `.cs` file gets its `.meta` from Unity refresh, and both land in the same commit.

Every task ends with exactly one commit of the paths the task names. Never stage an unlisted path.

Stop and return evidence when: any existing test changes outcome after an extraction. Edit-time capture fails on the stand-in fixtures for an unnamed reason. The dry run cannot complete without persisting state beyond the named transients. A display row needs a sentence no existing report string covers.

Unity validation discipline: after creating or changing files, call the Unity MCP `refresh_unity` tool and wait for readiness. Run tests through the Unity MCP `run_tests` tool and poll `get_test_job`. A filtered run that reports zero tests is a failure. Record observed pass and fail counts in the task report. RED means: run the new test, observe the failure, record it against the named wrong implementation, then apply the minimal production change. A first-run pass is recorded as characterization, never dressed up as RED.

Falsifier house style: a `--- Falsifier N: name ---` comment in the test file names the plausible wrong implementation the assertion kills, and the fixture carries a no-op guard assertion so later changes cannot silence it.

## Global constraints

- Editor-only production code. One production assembly, `Alrauna.Amuse.Editor`. New namespaces: `Alrauna.Amuse.Editor.Analysis` for the two extracted types, `Alrauna.Amuse.Editor.DryRun` for the new module.
- File name equals type name. One public type per file, `internal` where possible. `Editor/AssemblyInfo.cs` already grants `InternalsVisibleTo` to the test assembly.
- Every label, tooltip, row sentence, disclosure sentence, and README sentence uses short active sentences. No contractions. One idea per sentence. No semicolons in prose. Count sentences avoid plural nouns, per the spec's Inspector section. Copy the spec's sentences verbatim.
- XML doc comments state why, not what.
- Fail closed through named values. No blanket catch converts a programming defect into a refusal row. An unexpected exception in the dry run surfaces as a console error and marks the errored display state.
- The dry run persists nothing. No asset, no import change, no scene write, no component write. It never calls `UnityStreamingTextureEvidence`, `AlphaSeparationPreparation.Prepare`, `AlphaSeparationApply`, or any clone builder.
- The dry run emits no NDMF report entry and adds no report string key. Display rows read `AmuseReportStrings` through its pure key builders, `Get`, and `Has`, plus the one interpolation helper this plan adds. A row asserts `Has(key)` before it renders.
- The dry run never calls any symbol in the `Alrauna.Amuse.Editor.Build` namespace except `AmuseReportStrings`.
- No test calls `AlphaSeparationPreparation.Prepare` directly. Build-side fixtures drive the plugin barrier with the `RunBarrier` harness and set component fields through the serialized object.
- No test switches the real active build target. The build target reaches the dry run through an injectable provider.
- Extraction tasks move code verbatim. Visibility promotion from `private` to `internal` is the only change. Any logic edit inside a moved member is a defect.

---

### Task 1: Extract the runtime-state pipeline into Analysis

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs` (plus `.meta`)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:358` (caller retarget only)
- Modify: the test files that reference the moved test seam or moved internals (grep decides, expected under `Packages/com.alrauna.amuse/Tests/Editor/Build/`)

**Interfaces:**

- Consumes: existing Host evidence records, `MeshSeparationPlan`, `SlotResolutionResult`, `AlphaSamplingPolicy`, `AlphaPolicyBounds`. All already referenced by the moved code.
- Produces: `internal static class RuntimeStateResolution` in namespace `Alrauna.Amuse.Editor.Analysis` exposing, all `internal static`: `ResolveRuntimeStates`, `ClassifyRuntimeStates`, `MaterialSlotsFor`, `ClosureWayForSlot`, and the internal nested type `ResolvedRuntimeStates`. Also exposes the policy mapping helpers `ProofMipCapFrom`, `MinTextureSizeFrom`, `MinimumCoverageFrom`, `MinimumPreserveCoverageFrom`, `OpaquePercentFrom`, `PolygonClampPercentFrom`, `DensityCapFromCoveragePercent` as `internal static`. Signatures and bodies identical to the pass members they replace.

- [ ] **Step 1: Move the members**

Create `RuntimeStateResolution.cs` with the namespace `Alrauna.Amuse.Editor.Analysis` and the same using directives the pass members need. Move these members from `Editor/Build/AmusePlatformFinishPlugin.cs` verbatim, promoting `private` to `internal`:

- `ResolvedRuntimeStates` (nested class, around line 1368)
- `ResolveRuntimeStates` (around line 1402)
- `FilterBlockEntriesForSlot` (around line 1594)
- `Refused` (around line 1637)
- `ClosureWayForSlot` (around line 1622)
- `ClassifyRuntimeStates` (around line 1652)
- `IntersectResolvedOutcomes` (around line 1723)
- `UnprovenOutcomes` (around line 1764)
- `MaterialSlotsFor` (around line 1780, already internal)
- `NoAlphaChannelPropertiesFor` (around line 1846)
- `CaptureRefusalsFor` (around line 1885)
- `ProofMipCapFrom` (around line 1105)
- `MinTextureSizeFrom` (around line 1124)
- `MinimumCoverageFrom` (around line 1143)
- `MinimumPreserveCoverageFrom` (around line 1165)
- `OpaquePercentFrom` (around line 1185)
- `PolygonClampPercentFrom` (around line 1216)
- `DensityCapFromCoveragePercent` (around line 1247)

Carry each member's XML doc comment with it. If a moved member references another pass member not on this list, stop and report: the list is wrong and the controller must rule.

The policy mapping helpers take the optimizer component as input. To keep `Analysis` free of a dependency on the runtime component type, change their parameter to the plain values they read (the component field reads stay at the call site in the pass). The bodies otherwise stay verbatim. For example `ProofMipCapFrom(AmuseAvatarOptimizer optimizer)` becomes `ProofMipCapFrom(int storedMaxMipLevel)` with the same body after the parameter read. If a helper reads more than one field, it takes exactly those values. This is the only permitted signature change, and it changes no logic.

- [ ] **Step 2: Retarget the pass**

In `AmusePlatformFinishPlugin.cs`, delete the moved members and add `using Alrauna.Amuse.Editor.Analysis;` if absent. Every former internal call site now resolves to `RuntimeStateResolution.*`. Keep call sites unmodified otherwise. Retarget `AnalyzeRuntimeStatesForTests` to call the new home.

- [ ] **Step 3: Retarget the other callers**

`Editor/Build/AlphaSeparationPreparation.cs:358` calls `MaterialSlotsFor`. Qualify it as `RuntimeStateResolution.MaterialSlotsFor`. Grep `Tests/` for the moved member names and retarget hits the same way. Verify no production or test reference to the old pass members remains:

Run: `grep -rn "MaterialSlotsFor\|ResolveRuntimeStates\|ClassifyRuntimeStates\|ProofMipCapFrom" Packages/com.alrauna.amuse/Editor Packages/com.alrauna.amuse/Tests | grep -v Analysis/RuntimeStateResolution`

Expected: only call sites, no definitions outside the new file.

- [ ] **Step 4: Refresh and run the barrier suite**

Refresh Unity and wait for readiness. Run the full `Alrauna.Amuse.Tests.Editor` assembly through the Unity MCP test runner.

Expected: every previously passing test passes. This is a pure move. Record observed counts. Any changed outcome is the spec's stop condition: stop and report.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs \
  Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs.meta \
  Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
  Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs \
  Packages/com.alrauna.amuse/Tests/Editor
git commit -m "refactor: extract runtime state resolution into analysis"
```

---

### Task 2: Extract the slot verdict function into Analysis

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/Analysis/SlotTransformDecision.cs` (plus `.meta`)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/SlotTransformDecisionTests.cs` (plus `.meta`)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`

**Interfaces:**

- Consumes: `MeshSeparationPlan`, `SubmeshSeparationPlan`, `SubmeshSeparationDisposition`, `MeshSeparationPlanner.Folded`, `MeshSeparationPlanner.WithFoldedSubmeshes`, `SlotResolutionResult` (all Analysis types that exist today).
- Produces, in namespace `Alrauna.Amuse.Editor.Analysis`:

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
    // A slot with no plan entry and no refusal gets None.
    internal static IReadOnlyList<SlotTransformVerdict> Decide(
        MeshSeparationPlan plan,
        IReadOnlyList<SlotResolutionResult> slotResults,
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

Write `SlotTransformDecisionTests.cs` with behavior-sentence names, NUnit constraint model, and the two falsifiers:

- `BelowConvertCoverageSlotRefusesWithCoverageBelowMinimum`: a plan whose slot holds proven share strictly below the minimum yields `CoverageBelowMinimum`. Guard: assert `SlotTransformOutcome.None` is never returned for a plan entry.
- `AtConvertCoverageSlotSplits`: a share exactly at the minimum yields `Split`, because the predicate proceeds at the boundary.
- `PreserveQualifyingSlotFolds`: every submesh `Split` with unproven share strictly below the preserve value yields `Fold` with `FoldedUnprovenTriangles` set.
- `PreserveValueOfZeroQualifiesNothing`: preserve 0 yields `Split`, never `Fold`. `--- Falsifier 1: gate-skipping split ---` a Decide that skips the convert coverage gate labels the below-threshold fixture `Split`. The below-threshold assertion kills it. `--- Falsifier 2: gate-skipping fold ---` a Decide that skips the preserve gate leaves the qualifying fixture on `Split`. The fold assertion kills it.
- `AnisotropySlotRefusesWhenSlotResultNamesIt`: a `SlotResolutionResult` carrying the anisotropy refusal yields `AnisotropyDifferences`.

Mark the file header `--- Falsifier 1 ---` and `--- Falsifier 2 ---` blocks per house style with no-op guard assertions.

- [ ] **Step 2: Run the tests, observe RED**

Run the filter `SlotTransformDecisionTests`. Expected: compile failure, the type does not exist. Record the observed failure against the named wrong implementations.

- [ ] **Step 3: Write the minimal type**

Implement `SlotTransformDecision` in `Editor/Analysis/SlotTransformDecision.cs`. Move the two predicates verbatim from `AlphaSeparationPreparation.cs`: the fold gate condition inside the preserve loop (the comparison `entry.TransparentTriangleOrdinals.Count * 100 >= minimumOpaqueCoverageToPreservePercent * totalTriangles` plus the disposition check, around lines 130-160), and `OpaqueCoverageBelow` (around lines 779-791). `Decide` groups `plan.Submeshes` by `SourceMaterialBindingIndex`, applies `PreserveSlotQualifies` per group, applies `OpaqueCoverageBelow` per submesh, reads anisotropy from `slotResults`, and returns ascending slot verdicts. Reuse `MeshSeparationPlanner.Folded` semantics for `FoldedUnprovenTriangles` without mutating the plan. XML doc comments state why: the dry run and preparation must share one verdict so the dropdown can never drift from the build.

- [ ] **Step 4: Delegate preparation to the shared predicates**

In `AlphaSeparationPreparation.cs`, replace the inline preserve condition and the `OpaqueCoverageBelow` body with calls to `SlotTransformDecision.PreserveSlotQualifies` and `SlotTransformDecision.OpaqueCoverageBelow`. Delete the local `OpaqueCoverageBelow`. No other behavior edits. The `RunBarrier` suite is the behavior pin.

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

Add the helper beside `Get` in `AmuseReportStrings.cs`. Use `string.Replace` per token index actually supplied. No new string keys. XML doc states why: the console substitutes through SimpleError at emission, and the dry run needs the same sentence text without an error object.

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
- Create: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs` (plus `.meta`)

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
    internal static MaterialTransformationRow Errored(string rendererPath, string rendererTypeName)
    internal static MaterialTransformationRow Renderer(
        string path, string typeName, int moved, int proven, int total,
        int refusedSlots, int slotCount, string sentence)
    internal static MaterialTransformationRow Slot(
        string path, string typeName, int slotIndex,
        int proven, int total, string sentence)
}
```

- [ ] **Step 1: Write the failing tests**

`MaterialTransformationRowTests` covers: the renderer factory sets every count field; the slot factory sets the slot index; factories never return a null sentence. `--- Falsifier 3: total-instead-of-proven ---` a row builder fed by the orchestrator that passes total triangles where proven triangles belong renders a fully unproven slot as a split claim. The test pins the contract at the boundary: a helper `RowText.ForSplitSlot(slotIndex, proven, total)` returns the sentence only when `proven > 0`, and the test asserts the no-split sentence text for `proven == 0`:

```csharp
[Test]
public void SplitRowRefusesZeroProvenTriangles()
{
    var sentence = SplitRowSentence(2, 0, 40);
    Assert.That(sentence, Is.EqualTo(
        "slot 2: unchanged. Triangles proven: 0 of 40. " +
        "The share is below the convert setting."));
}
```

The sentence builders live as internal statics on a `RowText` static class in the same file set: `SplitRowSentence`, `FoldRowSentence`, `AlreadyOpaqueRowSentence`, `UnchangedRowSentence`, `RefusedRowSentence`, `RendererSummarySentence`, `RendererNoMovementSentence`, `RendererRefusedSentence`. Each builds exactly the spec's Inspector sentences with the counts interpolated. Plural-free forms: `Triangles proven: 3 of 5.` and `Triangles moved: 12.` Copy the spec text verbatim. Run the filter, observe RED (the type does not exist), record it.

- [ ] **Step 2: Implement the model and sentence builders**

Minimal records and builders. Every sentence goes through `AmuseReportStrings` where a key exists, via `Has` then `Format`; builder-local text only for the spec's display-only sentences (fold, already opaque, unchanged share, banner and note texts, which are spec text and not report table entries).

- [ ] **Step 3: Run GREEN and commit**

Run `MaterialTransformationRowTests`. Expected: green, observed counts.

```bash
git add Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs \
  Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs.meta \
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
// Production fills it from the scene; tests build it by hand.
internal readonly struct DryRunFingerprintInputs
{
    internal int AvatarRootInstanceId { get; }
    internal string PolicyFingerprint { get; }      // all thirteen component values
    internal string ControllerFingerprint { get; }  // controller GUIDs plus file ticks
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

`PolicyFingerprint` concatenates the thirteen component values from the spec's component state section. `ControllerFingerprint` concatenates `AssetDatabase.GetAssetPath` plus `System.IO.File.GetLastWriteTimeUtc(...).Ticks` for each controller asset and its `.meta`. `RendererFingerprintBody` concatenates renderer path and type, mesh identity (asset GUID plus source and meta ticks when the mesh is an asset, else instance ID plus update count), each slot material identity the same way, and each admitted texture identity the same way. Live RenderTexture inputs never enter the fingerprint: the orchestrator detects them and routes them to the Refresh-only note instead.

- [ ] **Step 1: Write the failing tests**

`DryRunCacheTests` with the two falsifiers and their guards:

- `AvatarSwapEvictsPreviousAvatarRows`. `--- Falsifier 4: cross-avatar serve ---` a cache keyed by renderer fingerprint alone serves avatar B the rows stored for avatar A when both share renderer paths. The test stores rows for avatar one, changes only the avatar root instance ID in the inputs, and asserts the miss.
- `PolicyChangeInvalidatesRendererRows`. `--- Falsifier 5: stale policy rows ---` a cache keyed without policy serves rows computed under the old slider. The test changes only `PolicyFingerprint` and asserts the miss.
- `ErroredStateSurvivesUntilClear`: `MarkErrored` then `IsErrored` true, then `Clear` makes it false.
- `StoreRendererIsSingleSlotPerAvatar`: storing under avatar two removes avatar one's entries.

Run the filter, observe RED (types do not exist), record it.

- [ ] **Step 2: Implement**

`DryRunFingerprint` builds the three key strings with a separator that cannot appear in a path (`"\u001f"`), so concatenations cannot collide. `DryRunCache` holds one static avatar key plus dictionaries for avatar rows and renderer rows keyed by the composite string. `StoreRenderer` under an avatar key different from the stored one clears everything first. XML doc comments state why: one avatar slot keeps memory bounded and makes cross-avatar serves impossible.

- [ ] **Step 3: Run GREEN and commit**

Run `DryRunCacheTests`. Expected: green, observed counts.

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

- Create: `Packages/com.alrauna.amuse/Editor/DryRun/AmuseBuildTargetProvider.cs` (plus `.meta`)
- Create: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs` (plus `.meta`)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs` (plus `.meta`)

**Interfaces:**

- Consumes: `RuntimeStateResolution`, `SlotTransformDecision`, `CommittedControllerGraph`, `UnityAnimationEvidenceCapture.Capture`, `UnityRendererAlphaAnalysis`, `EffectiveMaterialMaterialization.CaptureBlockState`, `LockedMaterialIdentity.PreCheckRefusal`, `PlatformAlphaSamplingPolicy.TrySelect`, `AmuseReportStrings`, `MaterialTransformationRow`, `RowText`, `DryRunFingerprint`, `DryRunCache`.
- Produces, in `Alrauna.Amuse.Editor.DryRun`:

```csharp
internal delegate BuildTarget ActiveBuildTargetProvider();

// Reads the real active build target. Tests inject their own.
internal sealed class EditorBuildTargetProvider
{
    internal static readonly EditorBuildTargetProvider Instance =
        new EditorBuildTargetProvider();
    internal BuildTarget Get() => EditorUserBuildSettings.activeBuildTarget;
}

// Computes one dry run for one optimizer component.
// Step() advances one renderer per call and returns true when done.
internal sealed class AlphaSeparationDryRun
{
    internal AlphaSeparationDryRun(AmuseAvatarOptimizer component,
        ActiveBuildTargetProvider buildTargetProvider)

    internal bool Done { get; }
    internal IReadOnlyList<MaterialTransformationRow> AvatarRows { get; }
    internal IReadOnlyList<MaterialTransformationRow> RendererRows { get; }

    internal void Begin()   // avatar facts, sampling, graph, cache lookup
    internal bool Step()    // one renderer, cache or compute
}
```

- [ ] **Step 1: Write the failing tests**

`AlphaSeparationDryRunTests` builds scene fixtures at test start on a hidden root object with `HideFlags.HideAndDontSave`, stand-in cutout shaders under `Hidden/Alrauna/AmuseTests/*`, and runtime-created textures, in the style of the existing evidence tests. Behavior sentences:

- `ProvenTriangleDisplaysAsWouldSplit`: one renderer, one cutout slot with an opaque texture region, produces the split slot row with `ProvenTriangles` above zero.
- `UnknownStaysUnchanged`: an unproven texture region never produces a split row. Guard: assert no row for the fixture names a split when the texture is fully transparent.
- `RendererRefusalShowsNamedSentence`: a renderer the analysis refuses produces the refused renderer row whose sentence passes through `AmuseReportStrings` and is non-empty.
- `StructuralRefusalStopsWithStructuralRow`: a fixture clip with an animation event produces the structural row and `Done` true with no renderer rows.
- `MasterSwitchOffProducesBannerAndNoCompute`: `_amuseDisabled` true produces the AMUSE off banner, `Done` true, and no renderer rows. Same shape for the separator switch. Guard: assert the compute never enumerated a renderer by keeping a renderer count probe on the fixture.
- `SamplingBannerSuppressesResults`: the injected provider returns an unsupported target and the avatar rows hold the sampling banner, with no transformation rows.
- `PropertyBlockChangesVerdictLikeTheBuild`: a fixture renderer with a `MaterialPropertyBlock` overriding the cutoff proves nothing where the block forces transparency, matching a build classification of the same state.

Run the filter, observe RED (the type does not exist), record it.

- [ ] **Step 2: Implement the orchestrator**

`Begin`:

1. Read the component switches. AMUSE off or separator off: banner row, `Done = true`, return.
2. Component on child: root banner, done.
3. `PlatformAlphaSamplingPolicy.TrySelect(provider.Get(), desktop, mobile, out policy)`: failure produces the sampling banner and done.
4. Build the avatar fingerprint from a `DryRunFingerprintInputs` filled from the component, the controller assets, and the structural graph. `CommittedControllerGraph.Enumerate(root, VRChatPlatformAnimatorBindings.Instance)`: refusal produces the structural row through `AmuseReportStrings.Has` plus `Format` and done.
5. Check the streaming texture and EditorOnly and consent note conditions across admitted materials, produce the note rows.
6. Try the avatar cache; on hit, materialize rows and done.

`Step`, one renderer per call:

1. Re-enumerate `root.GetComponentsInChildren<Renderer>()`, take the next renderer after the last consumed index. A destroyed renderer is skipped.
2. Composite key lookup: hit serves rows. Errored mark serves the errored row.
3. Compute: `CaptureBlockState`, `UnityAnimationEvidenceCapture.Capture` with the production arguments (renderer path, `renderer.sharedMaterials`, graph, `VRChatPlatformAnimatorBindings.Instance`, policy bounds from the extracted helpers, `CaptureThroughEffectiveMaterials` is build-owned, so the dry run passes its capturer delegate directly and keeps the property block input through the block state and through `ResolveRuntimeStates`), `LockedMaterialIdentity.PreCheckRefusal`, `_ignoreOutOfRangeMaterialSlots`, the renderer type name, and a null registered source lookup. Analysis refusal: refused row through the sentence table. Otherwise `CaptureGeometry`, `ResolveRuntimeStates`, `MaterialSlotsFor`, `ClassifyRuntimeStates`, then `SlotTransformDecision.Decide` and row construction through `RowText`.
4. Wrap the compute in a `try`/`catch` that logs the exception with `Debug.LogException` and calls `DryRunCache.MarkErrored`, then builds the errored row. This is the one catch in the dry run, it rethrows nothing, and it exists so a defect cannot loop the repaint driver. It never produces a refusal row.
5. Store rows and the fingerprint. A live RenderTexture among the admitted textures produces the Refresh-only note and skips the fingerprint store for that renderer.
6. Request `Repaint()` on the hosting editor through a delegate the inspector sets, so the next pass advances. Return whether renderers remain.

The dry run references `AmuseReportStrings` only. Any other `Alrauna.Amuse.Editor.Build` symbol in this file is a defect.

- [ ] **Step 3: Run GREEN, then the no-persistence guard fixture**

Add to the same test file: `DryRunPersistsNothing`. The fixture holds one streaming-mipmap texture (importer flag set on a runtime-created asset under a temp folder the test creates and deletes) and one renderer with a property block. Before and after a full run: assert equal `Resources.FindObjectsOfTypeAll<Material>().Length`, equal asset counts under the temp folder, and that the streaming readback folder does not exist. Run it with the orchestrator tests.

Expected: all green, observed counts recorded.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/DryRun/AmuseBuildTargetProvider.cs \
  Packages/com.alrauna.amuse/Editor/DryRun/AmuseBuildTargetProvider.cs.meta \
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
- Produces: two session bools `_materialTransformationsOpen` and `_alphaSeparationTransformationsOpen`; a private `DrawMaterialTransformations()` called in `OnInspectorGUI` between the alpha separator block and the status HelpBox; a field holding the current `AlphaSeparationDryRun` instance and its last consumed renderer index; a `Repaint` delegate wired into the orchestrator.

- [ ] **Step 1: Extend the host window harness**

In `AmuseAvatarOptimizerEditorTests`, extend `RepaintHostWindow` with a completion probe: the window's `OnGUI` keeps calling `HostedEditor.OnInspectorGUI()` across frames until a `Func<bool>` supplied by the test returns true, then sets `Painted`. The existing single-pass behavior stays for the old tests.

- [ ] **Step 2: Write the failing tests**

Two `[UnityTest]` behavior sentences:

- `MaterialTransformationsFoldoutPaintsRows`: a one-renderer fixture avatar with the stand-in cutout material, both foldouts forced open by reflection, repaints until the dry run reports done, then asserts `Painted` and that the editor exposed at least one renderer row. Expose the rows for the test through an internal read-only property on the editor.
- `SeparatorOffPaintsBannerOnly`: separator switch off, repaint once, assert `Painted` and the banner row, no renderer rows.

Run the filters, observe RED (the foldout does not exist), record it.

- [ ] **Step 3: Implement the drawing**

Add to `AmuseAvatarOptimizerEditor.cs`:

- The two session bools beside `_settingsOpen`.
- `DrawMaterialTransformations()`: the top foldout with `EditorStyles.foldoutHeader`; inside, the Refresh button (`GUILayout.Button("Refresh")` right aligned, calls `DryRunCache.Clear()` plus resets the run), the nested foldout, then the disclosure rows, then the orchestrator drive: create the run on first open, call `Step()` once per `OnInspectorGUI`, draw `AvatarRows` and `RendererRows` as `EditorGUILayout.LabelField` lines, indenting slot rows one level with `EditorGUI.indentLevel`.
- Banner, note, and disclosure rows draw as `EditorGUILayout.HelpBox(sentence, MessageType.None)`, with warnings as `MessageType.Warning` for the banner set named in the spec. Slot rows indented under their renderer. An errored row draws as `MessageType.Warning` with the errored sentence.
- The Refresh button also clears errored marks. `OnInspectorGUI` calls `DrawMaterialTransformations()` after the alpha separator block and before the `AmuseBuildStatusStore` block.
- XML doc comments state why: the view shows outcomes, not policy, so it sits outside Advanced Settings.

Copy every sentence from the spec's Inspector section verbatim.

- [ ] **Step 4: Run GREEN and commit**

Run the two new filters plus the full existing `AmuseAvatarOptimizerEditorTests`. Expected: green, observed counts.

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

Sweep the changed files: no drive-letter paths, no home paths, no four-digit ports, no at sign joined to a hexadecimal hash, no private asset names. Then commit.

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

Run `git status --porcelain` and `git diff --check`. Expected: clean tree except the two user-owned untracked temp meta files, no whitespace errors. Run `git log --oneline main..HEAD` and confirm one commit per task.

Report the observed counts, the commit list, and any deferred minors to the controller. Do not commit in this task.
