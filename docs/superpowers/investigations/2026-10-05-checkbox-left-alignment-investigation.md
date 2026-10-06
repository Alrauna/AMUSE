# Checkbox alignment: current-state characterization

Date: 2026-10-05. Branch: `feature/optimizer-ui-rework` at commit `477c185`.
The working tree carries one unrelated modified file,
`.github/workflows/pr.yml`. This note does not touch it.

Labels: `[SOURCE]` is a fact read in this repository, in an installed
vendor package, or in a public primary source. `[INFERENCE]` is a
deduction. `[RECOMMENDATION]` is a proposed next step.

## 1. Purpose

The owner asked for another UI round: move all of the checkmark boxes
from the right side of their rows to the left, in the style of AAO
(Avatar Optimizer by anatawa12) and d4rkAvatarOptimizer. This note
records what draws the checkboxes on 2026-10-05, why they sit on the
right, and how the two reference products place them on the left. It
changes nothing.

## 2. The checkmark rows before the change

`[SOURCE]`
`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` draws
five checkmark rows, all through `EditorGUILayout.PropertyField` with a
`GUIContent` that carries a label and a tooltip:

1. "Alpha Separator" (`_alphaSeparatorEnabled`), top level, in
   `OnInspectorGUI`.
2. "Disable AMUSE" (`_amuseDisabled`), in `DrawSettings`.
3. "Advanced Settings" (`_advancedSettingsRevealed`), in `DrawSettings`.
4. "Ignore Out-of-Range Material Slots"
   (`_ignoreOutOfRangeMaterialSlots`), in `DrawSettings` under the
   reveal toggle.
5. "Allow Depth Test Change on Moved Triangles"
   (`_allowDepthTestChange`), in the same block.

`[SOURCE]` The preset row in `DrawPresetRow` draws three
`GUILayout.Toggle` controls with `GUI.skin.button`. A button toggle
shows a pressed state and no checkmark. d4rk draws its preset row the
same way (`Editor/d4rkAvatarOptimizerEditor.cs` line 99 at version
4.5.4). The request names checkmark boxes, so the preset row is out of
scope. The design round can confirm.

`[SOURCE]` The two menus ("Settings" and "Alpha Separator Settings") use
`EditorGUILayout.Foldout` with `EditorStyles.foldoutHeader`. They draw
an arrow, not a checkmark. They are out of scope.

## 3. Why the checkboxes sit on the right

`[SOURCE]` The public Unity C# reference source, read on 2026-10-05,
explains the rendering. `EditorGUILayout.PropertyField` for a bool
reaches `EditorGUI.DefaultPropertyField` (`Editor/Mono/EditorGUI.cs`,
line 7996). Its `SerializedPropertyType.Boolean` case calls
`EditorGUI.Toggle(position, label, property.boolValue)` (line 8108).

`[SOURCE]` `EditorGUI.Toggle(Rect, GUIContent, bool)` calls
`PrefixLabel(position, id, label)` and draws the checkbox in the rect
that `PrefixLabel` returns (line 1993). `PrefixLabel` draws the label in
the left label column. The checkbox therefore lands in the value column
on the right. This matches what the owner sees.

`[SOURCE]` `EditorGUI.ToggleLeft(Rect, GUIContent, bool)` reaches
`ToggleLeftInternal` (line 2004). Unity's own comment reads: "Make a
toggle with the label on the right." The checkbox draws at the row
start, before the label text.

`[INFERENCE]` The dev editor instance runs Unity 2022.3.22f1, and the
reference source read is the master branch. The two vendor packages ship
these same APIs across many Unity versions, so the semantics are stable.
The smoke check at implementation time can confirm the pin.

## 4. How AAO places checkboxes on the left

`[SOURCE]` Citations use the installed VPM dependency
`Packages/com.anatawa12.avatar-optimizer` at version 1.9.17.

`[SOURCE]` `Runtime/Attributes.cs` declares `internal class
ToggleLeftAttribute : PropertyAttribute` (lines 6 to 8).

`[SOURCE]` `Editor/Inspector/ToggleLeftDrawer.cs` declares
`ToggleLeftDrawer : PropertyDrawer` with
`[CustomPropertyDrawer(typeof(ToggleLeftAttribute))]`. `OnGUI` (lines 9
to 16) wraps `EditorGUI.ToggleLeft(position, label,
property.boolValue)` in `BeginProperty` and `EndProperty` plus a change
check, then writes `property.boolValue`.

`[SOURCE]` The component fields carry the attribute. `Runtime/TraceAndOptimize.cs`
marks about 30 feature flags with `[ToggleLeft]` (lines 26 to 127).

`[SOURCE]` The custom editor draws those fields with plain
`EditorGUILayout.PropertyField` (`Editor/Inspector/TraceAndOptimizeEditor.cs`,
lines 74 to 111). The drawer supplies the whole checkbox-left look. The
base editor class holds no toggle code of its own. AAO therefore changes
no call site to get the left look.

`[SOURCE]` `Runtime/assembly-info.cs` grants
`InternalsVisibleTo("com.anatawa12.avatar-optimizer.editor")`, so the
internal attribute compiles inside the editor drawer.

`[SOURCE]` The `PropertyField` label, tooltip included, passes into the
drawer, so tooltips survive. `BeginProperty` also keeps the prefab
override menu and the override display that `PropertyField` provides.

## 5. How d4rk places checkboxes on the left

`[SOURCE]` Citations use the installed VPM dependency
`Packages/d4rkpl4y3r.d4rkavataroptimizer` at version 4.5.4.

`[SOURCE]` The helper `ToggleOptimizerProperty` in
`Editor/d4rkAvatarOptimizerEditor.cs` (the `ToggleLeft` call sits at
line 1321) reflects the named bool property, draws
`EditorGUILayout.ToggleLeft(content, value)` inside a
`DisabledScope(!CanChangeSetting(...))`, paints a tooltip icon over the
last rect, and writes the value back with reflection and `SetDirty`.

`[SOURCE]` That helper works on live CLR properties, not on
`SerializedProperty`. It has no `SerializedObject`, no Undo
integration, and no prefab override menu.

`[SOURCE]` The separate settings window uses a second shape:
`BoolFieldLeft` in `Editor/AvatarOptimizerSettings.cs` (line 226)
reserves a fixed 35 pixel column and draws
`EditorGUILayout.ToggleLeft` inside it.

## 6. What the design must not copy

`[INFERENCE]` d4rk's reflection and `SetDirty` write path bypasses the
`SerializedObject` flow. The quality presets decision already fixed the
opposite convention for this component: one `SerializedObject` and one
`ApplyModifiedProperties`, so Undo behaves like every other control.
AMUSE should copy the look through `SerializedProperty`, not the d4rk
write path. AAO's drawer shape already does that.

`[INFERENCE]` d4rk's `CanChangeSetting` dependency web has no AMUSE
counterpart. AMUSE settings stay independent.

## 7. The decision space

`[RECOMMENDATION]` Option A: copy the AAO shape. Add a
`ToggleLeftAttribute` to the runtime assembly and a `ToggleLeftDrawer`
to the editor assembly. Annotate the five serialized bools. Change zero
call sites in `AmuseAvatarOptimizerEditor.cs`.

`[SOURCE]` Option A needs no assembly plumbing beyond two files. The
editor assembly already references the runtime assembly
(`Alrauna.Amuse.Editor.asmdef`). The runtime assembly carries no
`AssemblyInfo.cs` as of 2026-10-05, so AAO's `internal` attribute would
need a new `InternalsVisibleTo` grant. The attribute is a marker type
beside an already public component, so making it public avoids the new
grant file. The design picks one of the two shapes.

`[SOURCE]` File placement follows the package conventions: one type per
file, file name equals type name. `Runtime/ToggleLeftAttribute.cs` and
`Editor/ToggleLeftDrawer.cs`, each with its `.meta` file. The drawer
must live in the editor assembly, because `UnityEditor` types do not
compile into the runtime assembly. AAO splits the same way.

`[INFERENCE]` One fidelity gap needs a decision. `PropertyField` shows a
mixed value for a multi-object selection. AAO's drawer does not. A
three line addition restores it:
`EditorGUI.showMixedValue = property.hasMultipleDifferentValues` around
the `ToggleLeft`. Matching AAO exactly leaves the gap.

`[RECOMMENDATION]` Option B: swap the five `PropertyField` calls to
`EditorGUILayout.ToggleLeft(content, property.boolValue)` with a
write-back. This adds no new type. It also drops three `PropertyField`
services: the override menu from `BeginProperty`, the mixed-value
display, and the override highlight. Avatars are usually prefab
instances, so those services have real users. Option A is the faithful
and preferred shape.

`[INFERENCE]` Layout details hold under Option A. `ToggleLeftInternal`
applies `IndentedRect`, so the checkbox respects the indent level
inside the foldouts, the same way the `PropertyField` rows it replaces
do. The five tooltips travel with the labels into the drawer.

## 8. Blast radius

`[SOURCE]` The change touches `Runtime/AmuseAvatarOptimizer.cs` (five
attribute annotations) and adds the two new files. No label changes, no
tooltip changes, no build code changes.

`[SOURCE]` `AaoMergedConsumptionTests.cs` pins the serialized field
names. Annotations do not touch field names. No test asserts a label or
a layout, and no label moves. `README.md` names behaviors, not layout.

`[SOURCE]` The product package is the only scope. The research package
inspector tooling does not ship and stays out of this round.

## 9. Validation shape

`[SOURCE]` The only automated editor test for this inspector pins the
pure `NormalizePolygonClamp` method. No test draws the inspector, and a
drawer change has no EditMode-testable pixel output.

`[INFERENCE]` A pure layout change has no failing unit test to observe
first, so RED/GREEN does not apply here. The nearest automated
protection stays the runtime field tests, which keep the annotations
from changing serialization.

`[SOURCE]` Established practice validates inspector layout with an
editor smoke check on the dev editor instance, like the 2026-09-10
settings plan. The smoke check list: all five rows show the checkbox
left of the label, tooltips still open, both foldouts still open and
close, the preset row looks unchanged, a multi-object selection shows
the mixed mark if the fidelity fix lands, and a build still runs.

## 10. Constraints the design must keep

`[SOURCE]` Labels and tooltips keep their current plain technical
English wording. This round changes layout only. New code comments
state why, not what, on load-bearing rules.

`[SOURCE]` The runtime assembly compiles for players. Serialization must
not change: the five fields keep their names, types, and defaults.

## 11. Verification record

`[SOURCE]` On 2026-10-05 this note was checked against the working
tree: the five bool rows, the preset row, the foldouts, both asmdefs,
the AAO drawer files, and the d4rk helper files. All claims held. No
correction was needed.

`[SOURCE]` A parallel read-only scout read both public repositories on
2026-10-05. AAO public master reads version 1.9.21-beta.0, with release
v1.9.20 published 2026-10-01. d4rk public main reads version 4.6.0,
with release v4.6.0 published 2026-09-16. The installed copies read
1.9.17 and 4.5.4. Both versions of each package agree on every
mechanism described here.

`[SOURCE]` The Unity mechanism claims come from the public Unity C#
reference source, master branch, read on 2026-10-05. The 2022.3 pin is
an inference from vendor usage, marked as such in section 3.
