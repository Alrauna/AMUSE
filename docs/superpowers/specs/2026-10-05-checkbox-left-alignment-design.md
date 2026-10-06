# Checkbox Left Alignment Design

Date: 2026-10-05. Base: branch `feature/optimizer-ui-rework` at commit
`477c185`.

Investigation:
`docs/superpowers/investigations/2026-10-05-checkbox-left-alignment-investigation.md`.

## 1. Overview

The five checkmark rows of the component inspector move the checkbox to
the left of its label, in the AAO style. The mechanism copies AAO: a
marker attribute on each serialized bool, plus one property drawer that
draws `EditorGUI.ToggleLeft`. No inspector call site changes. No label,
tooltip, field name, default, or build behavior changes. The preset row
and the foldouts stay as they are.

## 2. The runtime attribute

New file `Packages/com.alrauna.amuse/Runtime/ToggleLeftAttribute.cs`,
namespace `Alrauna.Amuse.Runtime`:

```csharp
internal sealed class ToggleLeftAttribute : PropertyAttribute { }
```

The type is a marker. It carries no code and changes no serialization.
The runtime assembly compiles for players, so it must stay free of
`UnityEditor` types.

New file `Packages/com.alrauna.amuse/Runtime/AssemblyInfo.cs`:

```csharp
[assembly: InternalsVisibleTo("Alrauna.Amuse.Editor")]
```

Decision D1: the attribute is `internal`, with this one grant line, not
`public`. The repository convention keeps production types internal
where possible. The editor assembly already grants its own internals to
the test assemblies, and AAO uses the same shape for the same reason.
The public alternative adds surface for no gain.

## 3. The editor drawer

New file `Packages/com.alrauna.amuse/Editor/ToggleLeftDrawer.cs`,
namespace `Alrauna.Amuse.Editor`, at the editor root beside the
component editor:

```csharp
[CustomPropertyDrawer(typeof(ToggleLeftAttribute))]
internal sealed class ToggleLeftDrawer : PropertyDrawer
{
    public override void OnGUI(
        Rect position, SerializedProperty property, GUIContent label)
    {
        label = EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        var value = EditorGUI.ToggleLeft(position, label, property.boolValue);
        EditorGUI.showMixedValue = false;
        if (EditorGUI.EndChangeCheck())
            property.boolValue = value;
        EditorGUI.EndProperty();
    }
}
```

Decision D2: the drawer sets `EditorGUI.showMixedValue` from
`hasMultipleDifferentValues` and resets it. This keeps the mixed-value
mark that a multi-object selection shows before this change. AAO's
drawer omits this and loses the mark. The design preserves current
behavior instead of copying the gap.

`BeginProperty` and `EndProperty` keep the prefab override menu and the
override display. The write goes through the `SerializedProperty`, so
Undo and the existing `ApplyModifiedProperties` calls keep working.

## 4. The annotations

`Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs` marks five
fields with `[ToggleLeft]`:

1. `_alphaSeparatorEnabled`
2. `_amuseDisabled`
3. `_advancedSettingsRevealed`
4. `_ignoreOutOfRangeMaterialSlots`
5. `_allowDepthTestChange`

`EditorGUILayout.PropertyField` resolves the drawer from the attribute,
so `AmuseAvatarOptimizerEditor.cs` changes nothing. The labels and
tooltips keep coming from the `GUIContent` at each call site and pass
into the drawer.

## 5. Out of scope

Decision D3: three things stay out, per the investigation.

- The preset row buttons. They show a pressed state, not a checkmark.
- The foldout headers. They show an arrow, not a checkmark.
- The d4rk write path. Reflection with `SetDirty` bypasses the
  `SerializedObject` flow. AMUSE copies the AAO shape instead.

## 6. What does not change

Component serialization: field names, types, and defaults stay exactly
as they are. The build code, the labels, the tooltips, the menu order,
and the preset row all stay as they are. The two new types are the only
new surface, and the attribute is internal.

## 7. Test strategy

A pure layout change has no EditMode-testable output and no pure logic
seam, so there is no new automated test and RED/GREEN does not apply.
Assertions on source text or type names are rejected by the repository
conventions.

Validation, in order:

1. Unity refresh compiles both assemblies clean.
2. The full EditMode suites `Alrauna.Amuse.Tests.Editor` and
   `Alrauna.Amuse.Research.Tests.Editor` pass with their observed
   counts unchanged.
3. A smoke check on the dev editor instance:
   - all five rows show the checkbox left of the label;
   - the labels and tooltips read as before;
   - both foldouts open and close, and nested rows keep their indent;
   - the preset row looks unchanged;
   - a multi-object selection shows the mixed mark;
   - a prefab instance row still offers the override context menu;
   - one build runs and reports as before.

## 8. Documentation

None. The README describes behavior, not layout.

## 9. Risks

- The drawer changes how five fields render in every context that draws
  them, including multi-object selection and prefab overrides. The
  smoke check covers both contexts.
- A typo in the `InternalsVisibleTo` name breaks the editor compile.
  The Unity refresh catches it at step 1.
