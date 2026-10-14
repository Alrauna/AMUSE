# Simplified Material Transformations User Experience Investigation

Date: 2026-10-14

## Context and Goal

On 2026-10-14, the owner requested a simplified user experience for the Show Material Transformations foldout.
The current implementation renders long text sentences for each renderer and slot.
The requested layout gives each transformation two lines.
The top line shows two object selection boxes side by side.
The first box selects the mesh renderer component.
The second box selects the source material asset.
The bottom line shows one of four status sentences:
- "Refused: AMUSE currently does not support this material"
- "Untouchable: 0 of out X polygons may be moved"
- "Splittable: Y of out X polygons may be moved"
- "Convertable: X out of X polygons may be moved"

The owner requested inspiration from d4rkAvatarOptimizer for this display.

## Investigation of Current Implementation

### Current Structure in AMUSE

AMUSE implements the dry run in `Packages/com.alrauna.amuse/Editor/DryRun/`.
The inspector drawer lives in `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`.

1. `AmuseAvatarOptimizerEditor.cs`:
`DrawMaterialTransformations()` draws the outer foldout.
Inside, it draws the Refresh button and the nested Alpha Separation foldout.
Inside the nested foldout, it draws two disclosure sentences.
Then it iterates through `_dryRun.AvatarRows` and `_dryRun.RendererRows`.
`DrawTransformationRow()` renders each row as text:
- `Banner` and `Errored` draw as `EditorGUILayout.HelpBox`.
- `Slot` indents by one level and draws `EditorGUILayout.LabelField(row.Sentence)`.
- `Renderer` draws `EditorGUILayout.LabelField(row.Sentence)`.

2. `MaterialTransformationRow.cs`:
`MaterialTransformationRow` stores immutable display data.
It stores `Kind`, `RendererPath`, `RendererTypeName`, `SlotIndex`, `MovedTriangles`, `ProvenTriangles`, `TotalTriangles`, `RefusedSlots`, `SlotCount`, and `Sentence`.
On 2026-10-14, it does not store live references to `Renderer` or `Material` objects.

3. `AlphaSeparationDryRun.cs`:
`AlphaSeparationDryRun` executes the analysis across renderers.
In `ComputeRows()`, it inspects `Renderer renderer` and `renderer.sharedMaterials`.
It captures evidence, classifies runtime states, and computes verdicts.
In `BuildRows()`, it iterates over `slotResults` and `verdicts`.
It creates a `MaterialTransformationRow.Slot()` row for each slot.
It also creates a `MaterialTransformationRow.Renderer()` summary row for each renderer.

4. `DryRunCache.cs`:
`DryRunCache` stores `MaterialTransformationRow` lists in memory during the editor session.
It keys rows by composite fingerprints of avatar and renderer state.

### Shortcomings of Current Implementation

The current display uses long technical sentences:
- "slot 0: would split. Triangles proven: 120 of 500."
- "slot 1: already opaque. It stays as it is."
- "Body (SkinnedMeshRenderer): Triangles moved: 120. Triangles proven: 120 of 500. 0 of 2 slots refused."

This text layout has several drawbacks:
- It does not let the user click or ping the mesh object in the hierarchy.
- It does not let the user click or ping the material in the project.
- It requires reading dense text to understand what will happen.
- It takes many lines of text when avatars have multiple slots.

## Investigation of d4rkAvatarOptimizer Precedent

The repository includes `Packages/d4rkpl4y3r.d4rkavataroptimizer/`.
In `Packages/d4rkpl4y3r.d4rkavataroptimizer/Editor/d4rkAvatarOptimizerEditor.cs`:

Lines 1393-1405 define `DrawMatchedMaterialSlot`:
```csharp
private void DrawMatchedMaterialSlot(MaterialSlot slot, int indent)
{
    indent *= 15;
    using (new EditorGUILayout.HorizontalScope())
    {
        GUILayout.Space(indent);
        EditorGUILayout.ObjectField(
            slot.renderer,
            typeof(Renderer),
            true,
            GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 20 - (indent)));
        int originalIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        EditorGUILayout.ObjectField(slot.material, typeof(Material), false);
        EditorGUI.indentLevel = originalIndent;
    }
}
```

Key observations from d4rkAvatarOptimizer:
1. Two `EditorGUILayout.ObjectField` controls sit side by side in an `EditorGUILayout.HorizontalScope`.
2. The left control displays `slot.renderer` with `typeof(Renderer)` and `allowSceneObjects: true`.
This displays the GameObject name and renderer type.
Users can click the target icon or label to ping and select the renderer in the scene hierarchy.
3. The right control displays `slot.material` with `typeof(Material)` and `allowSceneObjects: false`.
This displays the material name and icon.
Users can click to select and inspect the material in the Project view.
4. In d4rkAvatarOptimizer, the mesh field width subtracts twenty pixels from half the view width.
This subtraction accommodates an adjacent material group selection button ("S") that AMUSE does not need.
For AMUSE, dividing the available width evenly between the two boxes produces a balanced layout.
5. Indentation handling: `d4rkAvatarOptimizer` resets `EditorGUI.indentLevel = 0` before drawing the second object field.
Without this reset, Unity indents the second column within its horizontal partition and disrupts alignment.
6. Object field role: the object fields are read-only inspection previews.
Their return values are discarded.
7. In d4rkAvatarOptimizer, the mesh box uses `typeof(Renderer)`.
In Unity scene hierarchies, the renderer component represents the mesh instance on the avatar.
A `typeof(Mesh)` field would show only the shared asset without indicating which avatar part it belongs to.
Using `typeof(Renderer)` allows scene navigation.
## Transformation Mapping

Each material slot on an avatar renderer represents one transformation candidate.
Let `X` be the total polygon count (`TotalTriangles`).
Let `Y` be the proven polygon count (`ProvenTriangles`).

### 1. Refused
Trigger condition:
- The slot has `result.Refusal != RendererAnalysisRefusal.None`.
- Or the slot verdict is `SlotTransformOutcome.AnisotropyDifferences`.
- Or the material cannot be analyzed by AMUSE.

Displayed sentence:
`Refused: AMUSE currently does not support this material`

### 2. Untouchable
Trigger condition:
- The slot is resolved without refusal, but zero polygons may be moved.
- This includes `SlotTransformOutcome.AlreadyOpaque` where the material is already opaque.
- This includes `SlotTransformOutcome.CoverageBelowMinimum` where proven coverage is below the threshold.
- This includes any non-refused slot where `ProvenTriangles == 0`.

Displayed sentence:
`Untouchable: 0 of out X polygons may be moved`

### 3. Splittable
Trigger condition:
- The slot verdict is `SlotTransformOutcome.Split` and `0 < verdict.ProvenTriangles < verdict.TotalTriangles`.
- A non-zero proper subset of polygons (`Y`) is proven opaque and can be separated into an opaque copy.
- The remaining `X - Y` polygons remain on the original transparent material.

Displayed sentence:
`Splittable: Y of out X polygons may be moved`

### 4. Convertable
Trigger condition:
- All `X` polygons in the slot may be moved.
- This includes `SlotTransformOutcome.Fold` where unproven polygons fold onto the opaque copy.
- This includes `SlotTransformOutcome.Split` when all polygons are proven (`verdict.ProvenTriangles == verdict.TotalTriangles`).

Displayed sentence:
`Convertable: X out of X polygons may be moved`

## Architectural and Cache Safety

1. Dynamic reference resolution at draw time:
`DryRunCache` stores transformation rows in static collections across editor frames.
To avoid scene object memory leaks and stale references after scene reload or undo, cached rows must never hold live `Renderer` or `Material` references.
`MaterialTransformationRow` continues to store `RendererPath` and `SlotIndex`.
At draw time, the inspector drawer resolves `Renderer` from the avatar root transform and `RendererPath`.
It resolves `Material` from `renderer.sharedMaterials[row.SlotIndex]`.
If the renderer or material is missing or destroyed, the drawer handles null safely.

2. Renderer-level and geometry extraction refusals:
When a renderer fails geometry extraction (`CaptureGeometry`), zero slot rows are emitted.
Only a renderer refusal row is created.
Therefore, renderer refusal rows and banner rows must remain visible as warning boxes.
Only successful renderer summary rows (which duplicated slot details) are omitted in favor of the two-line slot view.

3. Animated material swaps:
For slots with multiple admitted materials from animation clips, `renderer.sharedMaterials[row.SlotIndex]` reflects the active authored scene material.
The drawer displays this authored material in the source material field.
## Questions and Choices for User Review

1. Phrasing of polygon counts:
The prompt specifies:
- `Untouchable: 0 of out X polygons may be moved`
- `Splittable: Y of out X polygons may be moved`
- `Convertable: X out of X polygons may be moved`
Notice that the first two sentences write `of out`, while the third writes `out of`.
`of out` is a typing transposition of `out of`.
Does the owner want the exact literal text `0 of out X` and `Y of out X`, or the standard `0 out of X` and `Y out of X`?

2. Object field type for mesh:
The prompt specifies `first one is a select box for the mesh`.
d4rkAvatarOptimizer uses `EditorGUILayout.ObjectField(renderer, typeof(Renderer), true)`.
In Unity, `Renderer` represents the mesh renderer component in the scene.
Should the field accept `Renderer`, or `Mesh`, or `UnityEngine.Object` (accepting either)?
Following d4rkAvatarOptimizer directly means using `Renderer`.

3. Renderer summary rows:
The current view renders a summary row for each renderer above its slots.
Does the owner want to retain the renderer summary line, or should only the two-line slot transformations be displayed?
Under the simplified layout, removing the redundant renderer summary line creates a cleaner list of transformations.

4. Banners and disclosures:
The view currently displays status banners (for example when AMUSE or the separator is disabled) and disclosure notes.
These banners provide safety context when the optimizer is disabled or running on non-Windows platforms.
We recommend keeping the top-level banners above the transformation list.
