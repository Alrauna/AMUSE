# Opaque Coverage Preserve Slider Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to execute this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the `Minimum Opaque Coverage to Preserve (Per Material)` slider, the four `to Convert` label renames, the slot-level preserve gate with honest reporting, and the version 3 preset values.

**Architecture:** Runtime component holds the setting. The plugin maps it into preparation. Preparation decides the fold per material slot and discloses after survival. Analysis owns the plan rebuild. Apply and later passes see ordinary `WhollyOpaqueCandidate` entries.

**Tech Stack:** Unity 2022.3.22f1, C#, NDMF, NUnit via Unity Test Framework, EditMode only. No dotnet build, no Node.

**Spec:** `docs/superpowers/specs/2026-10-13-opaque-coverage-preserve-slider-design.md`

**Investigation:** `docs/superpowers/investigations/2026-10-13-opaque-coverage-preserve-slider-investigation.md`

## Working discipline

- Branch: `feat/min-opaque-coverage-preserve`. Base: the amended spec commit (`6b3a7b1`) on that branch. Do not branch from anything else.
- Allowed mutations, exhaustive: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`, `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`, `Packages/com.alrauna.amuse/Editor/Presets/OptimizerPreset.cs`, `Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs`, `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs`, `Packages/com.alrauna.amuse/Presets/safe.json`, `Packages/com.alrauna.amuse/Presets/normal.json`, `Packages/com.alrauna.amuse/Presets/aggressive.json`, `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`, `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`, `Packages/com.alrauna.amuse/Editor/Analysis/MeshSeparationPlanner.cs`, `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs`, `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`, `README.md`, and test files under `Packages/com.alrauna.amuse/Tests/Editor/` that mirror the changed production folders. `Tests/Editor/Analysis/MeshSeparationPlannerTests.cs` already exists; this plan extends it and creates no new test file.
- Never touch: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated `.csproj`/`.sln`, `Packages/nadena.dev.ndmf/`, `Packages/com.alrauna.amuse.research/`, anything under `docs/superpowers/` dated before 2026-10-13, and any file not listed above.
- Every task ends with exactly one commit of the paths the task names. Do not stage unrelated files. The owner authorized one commit per finalized task on 2026-10-13.
- Stop and return evidence when: a step cannot be completed as written because the code differs from this plan; a test fails for a reason the task does not name; compilation fails and the fix is not mechanical; or a discovery matches the spec's stop conditions.
- Unity validation: request an asset refresh through the Unity MCP refresh tool and wait for readiness before running tests. Run tests through the Unity MCP test runner. A filtered run that reports zero tests is a failure. Record observed pass and fail counts in the task report.
- Falsifier markers follow the house style at `AlphaSeparationPreparationTests.cs:2185`: a `--- Falsifier N: ... ---` comment in the test's doc comment block, plus a no-op guard assertion so a later change cannot silence the test.

## Global constraints

- Editor-only production code. Runtime file changes are data and doc comments only.
- Every label, tooltip, report sentence, description, and README sentence uses short active sentences. No contractions. One idea per sentence.
- XML doc comments state why, not what.
- Fail closed through named values. No new refusal values are needed anywhere in this plan.
- RED/GREEN: for each behavior change, write the test first, run it, and observe the failure against the named wrong implementation. Record the observed failure. Then apply the minimal production change and observe green. A first-run pass is recorded as characterization, never dressed up as RED.
- No test calls `AlphaSeparationPreparation.Prepare` directly. Preparation fixtures drive the plugin barrier with the `RunBarrier` harness and set component fields through the serialized object, exactly like the existing fixtures, then inspect `AmusePlatformFinishState` and its prepared separation records.

## Task 1: Rename the four slider labels to Convert

Display strings and doc comments only. No serialized name changes. No behavior change.

- [ ] 1.1 In `Editor/AmuseAvatarOptimizerEditor.cs`, change the four label literals in place, keeping each tooltip unchanged:
  - `"Minimum Opaque Coverage (Per Material)"` to `"Minimum Opaque Coverage to Convert (Per Material)"` (in `MaterialCoverageRowContent`).
  - `"Alpha Upper Clamp (Per Texture)"` to `"Alpha Upper Clamp to Convert (Per Texture)"` (in `TextureClampRowContent`).
  - `"Minimum Opaque Coverage (Per Polygon)"` to `"Minimum Opaque Coverage to Convert (Per Polygon)"` (in `PolygonCoverageRowContent`).
  - `"Alpha Upper Clamp (Per Polygon)"` to `"Alpha Upper Clamp to Convert (Per Polygon)"` (in `PolygonClampRowContent`).
- [ ] 1.2 In `Runtime/AmuseAvatarOptimizer.cs`, update the four doc-comment quotes to the new labels: the coverage doc's `The inspector / shows it as "Minimum Opaque Coverage (Per Material)"`, the per-texture clamp doc quote, the per-polygon clamp doc quote, and the per-polygon coverage doc quote `shows it as "Minimum Opaque Coverage / (Per Polygon)"`.
- [ ] 1.3 In `Editor/Build/AmusePlatformFinishPlugin.cs`, update the label-quoting sites: the coverage mapper doc paraphrase `Minimum Opaque Coverage Percentage` becomes `Minimum Opaque Coverage to Convert (Per Material)`, the per-polygon clamp mapper doc quote, the per-polygon coverage mapper doc quote split across two lines, and the exception message string `"AMUSE Minimum Opaque Coverage (Per Polygon) is "` becomes `"AMUSE Minimum Opaque Coverage to Convert (Per Polygon) is "`.
- [ ] 1.4 In `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`, update the `OutOfBandWarning` input string `"Alpha Upper Clamp (Per Texture)"` to the new label. Leave the popup label inputs unchanged.
- [ ] 1.5 Refresh Unity, then run the `Alrauna.Amuse.Tests.Editor` assembly filtered to `AmuseAvatarOptimizerEditorTests` and `AmuseAvatarOptimizerTests`. Expect all green with a nonzero count. Record the counts.
- [ ] 1.6 Commit: `git add Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs && git commit -m "feat: rename the alpha separator sliders to convert"`

## Task 2: Add the runtime preserve field

- [ ] 2.1 In `Runtime/AmuseAvatarOptimizer.cs`, directly after the `_minimumOpaqueCoveragePercent` field, add:

```csharp
/// <summary>
/// The largest unproven share, in percent, a material slot's
/// submeshes can hold and still skip the split. A slot whose every
/// submesh sits strictly below this value flattens onto the opaque
/// material without proof, which can lose transparency as a visible
/// artifact. The value 0 never flattens, so avatars saved before
/// this field gain no silent flattening. The inspector shows it as
/// "Minimum Opaque Coverage to Preserve (Per Material)".
/// </summary>
[SerializeField]
[Range(0, 100)]
private int _minimumOpaqueCoverageToPreservePercent = 0;
```

- [ ] 2.2 Directly after the `MinimumOpaqueCoveragePercent` property, add:

```csharp
/// <summary>
/// The largest unproven triangle share, in percent, a slot's
/// submeshes can hold and still flatten onto the opaque material.
/// The value 0 never flattens.
/// </summary>
public int MinimumOpaqueCoverageToPreservePercent =>
    _minimumOpaqueCoverageToPreservePercent;
```

- [ ] 2.3 In `Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`, add the two mirrored tests: `DefaultMinimumOpaqueCoverageToPreservePercentIs0` and `MinimumOpaqueCoverageToPreservePercentSerializedPropertyRoundTrips`, copying the shape of the coverage pair at lines 172 and 188. Both pass on first run; record them as characterization in the task report.
- [ ] 2.4 Refresh Unity and run the `AmuseAvatarOptimizerTests` filter. Expect green, nonzero count, two new tests present. Record counts.
- [ ] 2.5 Commit: `git add Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs && git commit -m "feat: add the preserve coverage setting to the optimizer component"`

## Task 3: Draw the preserve slider

- [ ] 3.1 In `Editor/AmuseAvatarOptimizerEditor.cs`, add a `private static readonly GUIContent PreserveCoverageRowContent` directly after `MaterialCoverageRowContent`, with the label `Minimum Opaque Coverage to Preserve (Per Material)` and the exact tooltip text from the spec's Inspector section.
- [ ] 3.2 Insert `PreserveCoverageRowContent` into `RowContents` between `MaterialCoverageRowContent` and `TextureClampRowContent`.
- [ ] 3.3 In `DrawAlphaSeparatorSettings`, insert one direct call between the `DrawCoverageSlider()` call and the `DrawAlphaPolicyControls()` call: `PercentSlider("_minimumOpaqueCoverageToPreservePercent", PreserveCoverageRowContent);`
- [ ] 3.4 Refresh Unity and run the `AmuseAvatarOptimizerEditorTests` filter. Expect green, nonzero count. Record counts.
- [ ] 3.5 Commit: `git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs && git commit -m "feat: draw the preserve coverage slider under the convert slider"`

## Task 4: Cut the preset schema over to version 3

One atomic migration: the parser, the record, the applier, the shipped files' schema version and new key, and every fixture that pins the version. The Safe acceptance flip and the descriptions are Task 5 and stay out of this task.

- [ ] 4.1 RED first: in `Tests/Editor/Presets/PresetParserTests.cs`, add `SchemaVersionTwoRefusesAsUnknown`, mirroring `SchemaVersionOneRefusesAsUnknown`. Run the `PresetParserTests` filter and observe the failure, because the parser still accepts version 2. Record the observed failure.
- [ ] 4.2 In `Editor/Presets/PresetParser.cs`: set `CurrentSchemaVersion` to 3; add `"minimumOpaqueCoverageToPreservePercent"` to `SettingKeys` directly after `"minimumOpaqueCoveragePercent"`; read it with `TryReadPercent`; thread the value into the `OptimizerPreset` constructor call as the seventh argument.
- [ ] 4.3 In `Editor/Presets/OptimizerPreset.cs`: add the `MinimumOpaqueCoverageToPreservePercent` property; grow the constructor from thirteen to fourteen parameters with the new `int minimumOpaqueCoverageToPreservePercent` as the seventh argument, directly after `minimumOpaqueCoveragePercent`; add the assignment and the `Matches` clause directly after the coverage clause; update the `Matches` doc comment at line 59 from `all eleven mapped fields` to `all twelve mapped fields`. The class doc carries no count and stays unchanged.
- [ ] 4.4 In `Editor/Presets/PresetApplier.cs`: write the new serialized field directly after the `_minimumOpaqueCoveragePercent` write, and update the doc comment at line 8 from `eleven mapped fields` to `twelve mapped fields`.
- [ ] 4.5 In `Editor/AmuseAvatarOptimizerEditor.cs`, update the preset-row doc comment at lines 73 to 78 from `all eleven fields` to `all twelve fields`. No other editor change in this task.
- [ ] 4.6 Edit the three shipped files in `Packages/com.alrauna.amuse/Presets/` for schema only: set `"schemaVersion": 3` in each and add `"minimumOpaqueCoverageToPreservePercent"` directly after `"minimumOpaqueCoveragePercent"` with value 0 in `safe.json`, 5 in `normal.json`, and 10 in `aggressive.json`. Change nothing else in this step: no acceptance flips, no description changes.
- [ ] 4.7 Test fixtures, all in this task so every gate goes green together:
  - `PresetParserTests`: the `ValidJson` fixture becomes version 3 and gains the new key; `ValidSchemaParsesAllElevenValues` becomes `ValidSchemaParsesAllTwelveValues` and asserts the new value; add `WithPreserveCoverage`; add `PreserveCoverageAboveHundredRefuses` and `MissingPreserveCoverageRefuses` mirroring the existing per-key refusals. Every helper call site that anchors a string replacement on the literal `"schemaVersion": 2` retargets to the new current-version literal, and `WrongSchemaVersionRefuses` replaces the current version with `4`, so it still asserts `UnknownSchemaVersion` for a version above current. `LegacyVersionOneBody` stays at version 1.
  - `OptimizerPresetTests`: `ShippedDefaults` gains the literal `0` directly after the coverage literal; the flip table gains a row for the new field.
  - `PresetApplierTests`: `ApplyWritesAllElevenFields` becomes `ApplyWritesAllTwelveFields`, with the setup write and the assertion for the new field.
  - `PresetFileStoreTests`: `ValidBody` becomes `"schemaVersion": 3` and gains the new key. Grep the file for a remaining `"schemaVersion": 2` literal before declaring the step done.
- [ ] 4.8 Re-run the RED test from 4.1 and observe green. Run the four preset test filters. Expect green, nonzero counts, including the real-file store and applier tests against the shipped files. Record counts.
- [ ] 4.9 Commit: `git add Packages/com.alrauna.amuse/Editor/Presets/OptimizerPreset.cs Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Presets/safe.json Packages/com.alrauna.amuse/Presets/normal.json Packages/com.alrauna.amuse/Presets/aggressive.json Packages/com.alrauna.amuse/Tests/Editor/Presets && git commit -m "feat: cut the preset schema over to version 3"`

## Task 5: Ship the Safe acceptance flip and descriptions

- [ ] 5.1 RED first: edit only `safe.json`'s two anisotropy keys to `true` in this step's parent step 5.2, then run `PresetApplierTests`. `ApplyingShippedSafeRevokesBothAcceptanceSettings` loads the real shipped file successfully, because Task 4 already made it parse, and fails on its two revoke assertions. Record that observed failure as this task's RED.
- [ ] 5.2 Edit the three shipped files for values and descriptions only, with the spec's Data files and Documentation sections as the source of truth: `safe.json` flips both anisotropy keys to true and takes the Safe description; `normal.json` and `aggressive.json` take the Normal and Aggressive descriptions. Their new keys and schema versions landed in Task 4.
- [ ] 5.3 In `Editor/AmuseAvatarOptimizerEditor.cs`, replace both occurrences of the sentence `A shipped preset turns this setting off.` (the desktop tooltip's ending and the mobile tooltip's ending) with `The Safe, Normal, and Aggressive presets turn this setting on.`
- [ ] 5.4 Rewrite `ApplyingShippedSafeRevokesBothAcceptanceSettings` as `ApplyingShippedSafeGrantsBothAcceptanceSettings`: load the real shipped `safe.json`, apply it, and assert both acceptance fields are true. Keep the structure of the old test. Observe green.
- [ ] 5.5 Run the four preset test filters. Expect green, nonzero counts. Record counts.
- [ ] 5.6 Commit: `git add Packages/com.alrauna.amuse/Presets/safe.json Packages/com.alrauna.amuse/Presets/normal.json Packages/com.alrauna.amuse/Presets/aggressive.json Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Tests/Editor/Presets && git commit -m "feat: ship the preserve acceptance values in the presets"`

## Task 6: Add the Analysis fold and rebuild entry point

Pure analysis. No Unity types. No policy.

- [ ] 6.1 Extend the existing `Tests/Editor/Analysis/MeshSeparationPlannerTests.cs`.
- [ ] 6.2 RED first: write the aggregate tests against the not-yet-existing API and observe the compile or test failure. Record it.
- [ ] 6.3 In `Editor/Analysis/MeshSeparationPlanner.cs`, add two internal static members:
  - `Folded(SubmeshSeparationPlan entry)` returning a new entry with disposition `WhollyOpaqueCandidate`, an opaque list that merges the entry's opaque and transparent ordinals into one ascending list, and an empty transparent list.
  - `WithFoldedSubmeshes(MeshSeparationPlan plan, IReadOnlyList<SubmeshSeparationPlan> foldedEntries)` taking the folded replacements only. It matches each folded entry to the plan entry with the same `SourceSubmeshIndex` and equal `SourceMaterialBindingIndex`, carries every unmatched plan entry over unchanged, and returns a new `MeshSeparationPlan` with recomputed `HasAnyOpaqueCandidates`, `RequiresAnySplit`, and both triangle counts. It throws `ArgumentNullException` for a null argument, following `Create`'s existing style, and `ArgumentException` when a folded entry's `SourceSubmeshIndex` matches no plan entry or appears twice, or when a matched entry's `SourceMaterialBindingIndex` differs.
- [ ] 6.4 Mark the aggregate tests with `--- Falsifier 6: stale aggregates ---` and a no-op guard assertion that `WithFoldedSubmeshes` exists. A wrong implementation that returns the original aggregates fails the `RequiresAnySplit`-after-full-fold and count assertions.
- [ ] 6.5 Run the `MeshSeparationPlannerTests` filter. Expect green, nonzero count. Record counts.
- [ ] 6.6 Commit: `git add Packages/com.alrauna.amuse/Editor/Analysis/MeshSeparationPlanner.cs Packages/com.alrauna.amuse/Tests/Editor/Analysis/MeshSeparationPlannerTests.cs && git commit -m "feat: fold submesh plans through the analysis planner"`

## Task 7: Gate and fold qualifying slots in preparation

- [ ] 7.1 RED first: in `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`, add the fold suite fixtures from the spec's Test strategy that do not need the report event. Each fixture is a `RunBarrier` scene fixture that sets `_minimumOpaqueCoverageToPreservePercent` on the component's serialized object, exactly as the apply-side coverage suite writes `_minimumOpaqueCoveragePercent`, then asserts the per-renderer plan through `state.Separation`'s `PreparedRendererSeparation.Plan` records:
  - Below threshold, one slot, one submesh: folds, disposition `WhollyOpaqueCandidate`, ordinals folded, one forced event asserted only after Task 8 lands, so this task asserts dispositions only.
  - At threshold: stays `Split`. Mark `--- Falsifier 3: at-or-below boundary ---`, guard: an at-or-below gate implementation flips this fixture to folded and fails.
  - Zero: never folds; entries and aggregates equal the planner output. This is the invariant 1 pin, not a falsifier.
  - `Unchanged` sibling blocks the slot. Mark `--- Falsifier 5: unchanged rides along ---`, guard: the sibling stays `Unchanged` and nothing folds.
  - Unknown-heavy submesh: 1 proven-opaque, 4 must-remain-transparent, 95 unknown at preserve 5. Share 99, does not qualify. Mark `--- Falsifier 1: strict numerator ---`, guard: the fixture's bucket share is the assertion target.
  - Mixed slot: submesh A at 98 percent proven-opaque and submesh B at 50 percent on one slot at preserve 5. Nothing folds. Mark `--- Falsifier 2: per-submesh fold ---`, guard: A's shared slot material and curve stay live.
  - Rebuilt plan: `RequiresAnySplit` false after a full fold, true when a genuine split survives.

  Write the tests, run the `AlphaSeparationPreparationTests` filter, and observe the failures: the stored value is ignored and nothing folds. Record the observed failure counts.
- [ ] 7.2 In `Editor/Build/AmusePlatformFinishPlugin.cs`, add `MinimumPreserveCoverageFrom` beside `MinimumCoverageFrom` at line 1140, mapping a stored value below zero to 0 and above 100 to 100, with a doc comment stating the high-side banding reason: the high side is the aggressive direction for this gate.
- [ ] 7.3 Thread the value through the plugin in three named edits: call `MinimumPreserveCoverageFrom(optimizer)` beside the existing `MinimumCoverageFrom` call at line 607; add `int minimumOpaqueCoverageToPreservePercent` to `RetainPreparedSeparation`'s parameters directly after its `minimumOpaqueCoveragePercent` parameter at line 1667 and pass it at the internal call around line 911; pass it into `Prepare` at line 1673 directly after `minimumOpaqueCoveragePercent`.
- [ ] 7.4 In `Editor/Build/AlphaSeparationPreparation.cs`, extend `Prepare` with `int minimumOpaqueCoverageToPreservePercent = 0` directly after `int minimumOpaqueCoveragePercent`. No test calls `Prepare`, so no caller update exists; the plugin call from 7.3 is the only call site.
- [ ] 7.5 At the top of `Prepare`, before the anisotropy reporting loop, add the fold pass: group `plan.Submeshes` by `SourceMaterialBindingIndex`; a slot qualifies when every entry has disposition `Split` and `entry.TransparentTriangleOrdinals.Count * 100 < minimumOpaqueCoverageToPreservePercent * (entry.OpaqueTriangleOrdinals.Count + entry.TransparentTriangleOrdinals.Count)`; for each qualifying slot, build `MeshSeparationPlanner.Folded(entry)` for every entry of the slot and collect them as the folded replacement list; record `SourceSubmeshIndex to unproven count` in a local dictionary for each folded entry; then reassign `plan = MeshSeparationPlanner.WithFoldedSubmeshes(plan, foldedEntries)`. When nothing qualifies, leave `plan` untouched so the original plan object flows through, preserving today's identity behavior at preserve 0.
- [ ] 7.6 Run the `AlphaSeparationPreparationTests` filter. Expect the new tests green and every pre-existing test green, nonzero count. Record counts. If a pre-existing test fails, stop and report: a folded entry reached a consumer the reviews did not cover.
- [ ] 7.7 Commit: `git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs && git commit -m "feat: flatten qualifying material slots in preparation"`

## Task 8: Report flattened slots honestly

- [ ] 8.1 Read `Editor/Build/AmuseReportStrings.cs` around the `amuse.slotSeparation` entries first, and match the exact three-part entry shape, key property, formatting conventions, and Localizer source-list registration of `PremultiplyNormalization` before writing strings. Every new key registers in the Localizer source list at the top of `AmuseReports.cs`.
- [ ] 8.2 RED first, split by fixture home. Message-level pins live in `Tests/Editor/Build/AmuseReportsSlotTests.cs` and `Tests/Editor/Build/AmuseReportStringsTests.cs`: the forced-opaque message names the renderer, slot, unproven count, cause, and setting name. Pipeline fixtures live in `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`, beside the premultiply disclosure fixtures:
  - Two folded submeshes of one slot produce two forced events, each with its own unproven count. Mark `--- Falsifier 4: per-slot dedup ---`, guard: the events' counts differ when the fixtures' unproven counts differ.
  - A folded slot refused by a marker clip produces the marker refusal and zero forced events. Mark `--- Falsifier 7: fold-time emission ---`, guard: the same fixture with the marker clip removed produces exactly one event per folded submesh.
  - A folded slot with premultiply normalization produces the `PremultiplyNormalizationFolded` variant, not the exact-color sentence.

  Observe the failures and record them.
- [ ] 8.3 In `Editor/Build/AmuseReports.cs`, add `SlotSeparationForcedOpaque(Renderer renderer, int slotIndex, int unprovenTriangleCount, string rendererName = null)` mirroring the premultiply method's context-object shape, and extend `SlotSeparationPremultiplyNormalization` with an optional `bool slotHasFoldedSubmeshes = false` parameter that selects the folded entries.
- [ ] 8.4 In `Editor/Build/AmuseReportStrings.cs`, add the `SlotSeparationForcedOpaqueKey` property for the literal key `amuse.slotSeparation.ForcedOpaque` with the three-part entry: base message names renderer, slot, and unproven count; `:description` states the triangles moved onto the opaque material without opacity proof because the unproven share sits below the `Minimum Opaque Coverage to Preserve` setting; `:hint` tells the user to lower that setting on the AMUSE Avatar Optimizer component to keep this material's transparency.
- [ ] 8.5 Reword the shared premise: in the `DepthTestDivergence` and `PremultiplyNormalization` `:description` entries, replace `proven-opaque triangles` with `opaque triangles` wherever the sentence describes the moved set. Add the folded premultiply entries under the literal key `amuse.slotSeparation.PremultiplyNormalizationFolded` with a `SlotSeparationPremultiplyNormalizationFoldedKey` property: same base shape, `:description` and `:hint` naming the preserve setting instead of claiming exact colors. Keep the genuine wording for genuinely proven slots. The refusal entry `OpaqueCoverageBelowMinimum` keeps `proven-opaque`, because a refused slot moves nothing.
- [ ] 8.6 In `Editor/Build/AlphaSeparationPreparation.cs`, emit `SlotSeparationForcedOpaque` in the candidate loop beside the divergence and premultiply disclosures, after the entry joins `candidateSlots`, guarded by the local fold map, once per folded surviving submesh. Pass the folded flag into the premultiply call from the same map.
- [ ] 8.7 Apply-side behavior and counters. In `Tests/Editor/Build/AlphaSeparationApplyTests.cs`, RED first, add three fixtures: a fully folded renderer swaps materials in place, rewrites its curves, adds no appended slot, and creates no clone; one folded slot beside one genuine split slot still clones for the split and keeps both materials honest; two folded submeshes of one changed slot both add to the summary count. The last fixture fails against the current `CountOpaqueTriangles`, which breaks at the first matching candidate. Record the observed failure. Then in `Editor/Build/AlphaSeparationApply.cs`: change `CountOpaqueTriangles` to accumulate over every candidate whose `SourceMaterialBindingIndex` matches a changed slot instead of breaking at the first; update its doc comment from `proven-opaque rendering` to `opaque rendering`. Note for the completion summary: this counter fix is unconditional, so at preserve 0 a changed slot that binds several candidates can report a higher count than the base commit, which undercounted it; the spec's invariant 1 documents this exception.
- [ ] 8.8 In `Editor/Build/AmusePlatformFinishPlugin.cs`, update the `AppliedOpaqueTriangleCount` declaration doc at line 67 from `proven-opaque rendering` to `opaque rendering`.
- [ ] 8.9 Run the `AmuseReportsSlotTests`, `AmuseReportStringsTests`, `AlphaSeparationPreparationTests`, `AlphaSeparationApplyTests`, and `MeshSeparationPlannerTests` filters. Expect green, nonzero counts. Record counts.
- [ ] 8.10 Commit: `git add Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Build && git commit -m "feat: report flattened slots with forced opaque events"`

## Task 9: Update the README

- [ ] 9.1 Apply the spec's Documentation section: the Use-section preset bullet replacement, the proof modes replacement, the preset migration paragraph replacement, and no other prose.
- [ ] 9.2 Sweep the changed files for identifiers: an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and private names learned in the session. Every hit is a defect; fix it before the commit.
- [ ] 9.3 Run `git diff --check` on the working tree. Expect empty output.
- [ ] 9.4 Commit: `git add README.md && git commit -m "docs: document the preserve slider and preset changes"`

## Task 10: Full validation

- [ ] 10.1 Refresh Unity and wait for readiness.
- [ ] 10.2 Run the full `Alrauna.Amuse.Tests.Editor` assembly. Record the observed pass and fail counts. Any failure: stop and report.
- [ ] 10.3 Run the full `Alrauna.Amuse.Research.Tests.Editor` assembly. Record counts.
- [ ] 10.4 Inspect `git log --oneline 6b3a7b1..HEAD` and `git status --short`. Expect exactly the nine task commits after the amended spec commit, and a clean working tree.
- [ ] 10.5 Report the observed counts, the characterization runs, the invariant 1 counting exception named in 8.7, and any deviation from this plan in the completion summary.
