# Simplified Material Transformations User Experience — Design

Date: 2026-10-14

Base: `75dbd56` on branch `feat/material-transformations-ux`

Investigation: `docs/superpowers/investigations/2026-10-14-simplified-material-transformations-ux-investigation.md`

## Overview

This design simplifies the user experience of the Show Material Transformations foldout on the AmuseAvatarOptimizer component.
The current view renders technical sentences for renderers and slots.
This change replaces those sentences with a visual two-line layout for each material transformation candidate.

Each transformation displays:
1. Top line: two selection boxes side by side.
The left box displays the target mesh renderer.
The right box displays the source material in question.
2. Bottom line: a status label that reports one of four states:
- `Refused: AMUSE currently does not support this material`
- `Untouchable: 0 of out X polygons may be moved`
- `Splittable: Y of out X polygons may be moved`
- `Convertable: X out of X polygons may be moved`

The layout takes inspiration from d4rkAvatarOptimizer's material slot preview.

## Decisions

### 1. Two-Line Layout Per Transformation
Each material slot on an avatar renderer represents one transformation candidate.
The inspector renders each transformation as a two-line block.
The top line draws two `EditorGUILayout.ObjectField` controls side by side within an `EditorGUILayout.HorizontalScope`.
The bottom line draws an `EditorGUILayout.LabelField` with the status sentence.
This replaces the old text format (`slot N: would split...`).
Successful renderer summary lines are omitted, because the two-line slot transformations present the necessary information clearly.

### 2. Object Field Properties and Indentation
In Unity IMGUI, the two controls share the available width following the d4rkAvatarOptimizer pattern:
- Left box: `EditorGUILayout.ObjectField(renderer, typeof(Renderer), true, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 20 - indent))`
Allows scene objects (`true`) so users can ping and highlight the renderer in the scene hierarchy.
- Indentation reset: `int originalIndent = EditorGUI.indentLevel; EditorGUI.indentLevel = 0;` before drawing the right box.
This prevents Unity from applying an inner indent offset to the second column.
- Right box: `EditorGUILayout.ObjectField(material, typeof(Material), false)`
Does not specify a hardcoded width so it naturally fills the remaining horizontal scope width.
Does not allow scene objects (`false`) because materials are project assets.
- Indentation restore: `EditorGUI.indentLevel = originalIndent;` after drawing the right box.
- Bottom line: the status sentence draws as `EditorGUILayout.LabelField(row.Sentence)` with standard indent.

### 3. Cache Purity and Safe Dynamic Reference Resolution
`DryRunCache` stores transformation rows in static collections across editor frames.
To prevent scene object memory leaks across scene loads, domain reloads, or undo actions:
- `MaterialTransformationRow` continues to store only pure data (`RendererPath`, `RendererTypeName`, `SlotIndex`, counts, and sentence).
- It never stores live `UnityEngine.Object` references.
- At draw time, `AmuseAvatarOptimizerEditor` resolves the live `Renderer` safely:
  - If `string.IsNullOrEmpty(row.RendererPath)`: resolve via `avatarRoot.GetComponent<Renderer>()`.
  - Else: resolve via `avatarRoot.transform.Find(row.RendererPath)?.GetComponent<Renderer>()`.
- It resolves the live `Material` with complete bounds and null checks:
  - If `renderer == null` or `renderer.sharedMaterials == null` or `row.SlotIndex < 0` or `row.SlotIndex >= renderer.sharedMaterials.Length`: resolve to `null`.
  - Else: resolve via `renderer.sharedMaterials[row.SlotIndex]`.
- For slots with animated material swaps, `renderer.sharedMaterials[row.SlotIndex]` reflects the active authored scene material.
- The drawer passes resolved objects safely to `EditorGUILayout.ObjectField`. Missing objects show as `None` without throwing exceptions.
### 4. Transformation Status Mapping
Let `X` be the total polygon count (`TotalTriangles`).
Let `Y` be the proven polygon count (`ProvenTriangles`).

The second line displays exactly one of four status sentences:

1. `Refused: AMUSE currently does not support this material`
Trigger condition:
- The slot has `result.Refusal != RendererAnalysisRefusal.None`.
- Or the slot verdict is `SlotTransformOutcome.AnisotropyDifferences`.
- Or the slot cannot be transformed by AMUSE.

2. `Untouchable: 0 of out X polygons may be moved`
Trigger condition:
- The slot is resolved without refusal, but zero polygons can be moved.
- This includes `SlotTransformOutcome.AlreadyOpaque` (already opaque).
- This includes `SlotTransformOutcome.CoverageBelowMinimum` (proven coverage below threshold).
- This includes any non-refused slot where `ProvenTriangles == 0`.

3. `Splittable: Y of out X polygons may be moved`
Trigger condition:
- The slot verdict is `SlotTransformOutcome.Split` and `0 < verdict.ProvenTriangles < verdict.TotalTriangles`.
- A non-zero proper subset `Y` of polygons is proven opaque and can be moved.

4. `Convertable: X out of X polygons may be moved`
Trigger condition:
- All `X` polygons in the slot may be moved.
- This includes `SlotTransformOutcome.Fold` where unproven polygons fold onto the opaque copy.
- This includes `SlotTransformOutcome.Split` when all polygons are proven opaque (`verdict.ProvenTriangles == verdict.TotalTriangles`).

### 5. Preserving Safety Banners and Refusal Alerts
Top-level banners (AMUSE disabled, separator disabled, root misplaced, non-Windows build target) remain visible as help boxes above the transformations.
When geometry extraction fails for a renderer (`CaptureGeometry` refusal), no slot rows exist.
In that case, the renderer refusal row remains visible as a warning box so geometry defects are never hidden.
Normal renderer summary rows are no longer emitted by `AlphaSeparationDryRun.BuildRows()`, because the two-line slot layout clearly communicates every transformation.
Any remaining `MaterialTransformationRowKind.Renderer` row (such as a geometry extraction refusal) is drawn as `EditorGUILayout.HelpBox(row.Sentence, MessageType.Warning)`.

### 6. Ponytail Adherence and Clean Cutover
The implementation minimizes changes and avoids new abstractions:
- Reuses existing `MaterialTransformationRow` data structures without adding new types.
- Updates `RowText` to define the four status sentence builders:
  - `RefusedSlotSentence()`
  - `UntouchableSlotSentence(int total)`
  - `SplittableSlotSentence(int proven, int total)`
  - `ConvertableSlotSentence(int total)`
- Deletes obsolete sentence builders in `RowText` that are no longer used (`SplitRowSentence`, `FoldRowSentence`, `AlreadyOpaqueRowSentence`, `UnchangedRowSentence`, `RefusedRowSentence`).
- Updates `AlphaSeparationDryRun.BuildRows()` to call the new sentence builders and removes unused accumulator variables.
- Updates `AmuseAvatarOptimizerEditor.DrawTransformationRow()` to draw the two-line layout.

## Test Strategy and Migration

1. `MaterialTransformationRowTests`:
- Migrate existing tests to assert against the four new status sentences:
  - `RefusedSlotRow`: asserts `Refused: AMUSE currently does not support this material`.
  - `UntouchableRow`: asserts `Untouchable: 0 of out X polygons may be moved`.
  - `SplittableRow`: asserts `Splittable: Y of out X polygons may be moved`.
  - `ConvertableRow`: asserts `Convertable: X out of X polygons may be moved`.
- Delete obsolete sentence tests for removed `RowText` methods.

2. `AlphaSeparationDryRunTests`:
- Update existing assertions that check for slot sentences to verify the new simplified sentences.
- Verify that dry run classification assigns the correct sentence for:
  - Cutout split slots (`Splittable`).
  - Fold slots (`Convertable`).
  - 100% proven split slots (`Convertable`).
  - Already opaque slots (`Untouchable`).
  - Coverage below threshold slots (`Untouchable`).
  - Refused slots (`Refused`).

3. `AmuseAvatarOptimizerEditorTests`:
- Update `MaterialTransformationsFoldoutPaintsRows` to expect slot rows without normal summary rows.
- Verify that the hosted editor repaints and draws the two object fields and status label without exceptions.
- Verify that missing or destroyed renderer references do not throw exceptions.
## Invariants

1. `DryRunCache` never stores live `UnityEngine.Object` references.
2. The dry run persists nothing to disk or assets.
3. Every slot row maps deterministically to one of the four status sentences.
4. If a renderer fails geometry extraction, the refusal reason remains visible as a warning box.
5. All UI sentences use the exact wording specified by the owner.
6. No absolute paths, ports, or private identifiers appear in code or tests.

## Test Strategy

1. `MaterialTransformationRowTests`:
- Test that each of the four status sentences formats with the expected wording and counts.
- Test `Refused: AMUSE currently does not support this material`.
- Test `Untouchable: 0 of out X polygons may be moved`.
- Test `Splittable: Y of out X polygons may be moved`.
- Test `Convertable: X out of X polygons may be moved`.
- Test edge cases where `X == 0`.

2. `AlphaSeparationDryRunTests`:
- Verify that dry run classification assigns the correct sentence for:
  - Cutout split slots (`Splittable`).
  - Fold slots (`Convertable`).
  - 100% proven split slots (`Convertable`).
  - Already opaque slots (`Untouchable`).
  - Coverage below threshold slots (`Untouchable`).
  - Refused slots (`Refused`).

3. `AmuseAvatarOptimizerEditorTests`:
- Verify that the hosted editor repaints and draws the two object fields and the status label.
- Verify that missing or null renderer references do not throw exceptions.
