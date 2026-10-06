# Checkbox Left Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the five inspector checkmarks from the right of their rows to the left, using the AAO attribute-plus-drawer mechanism.

**Architecture:** A marker `PropertyAttribute` in the runtime assembly and a `PropertyDrawer` in the editor assembly. `EditorGUILayout.PropertyField` resolves the drawer from the attribute, so the component editor changes nothing.

**Tech Stack:** Unity 2022.3.22f1, C#, IMGUI (`UnityEditor`), Unity Test Framework (EditMode).

**Spec:** `docs/superpowers/specs/2026-10-05-checkbox-left-alignment-design.md`

## Global Constraints

- Attribute type: `internal sealed class ToggleLeftAttribute : PropertyAttribute` in namespace `Alrauna.Amuse.Runtime`.
- Grant line, exact: `[assembly: InternalsVisibleTo("Alrauna.Amuse.Editor")]`.
- Drawer sets `EditorGUI.showMixedValue` from `hasMultipleDifferentValues` and resets it (Decision D2).
- Zero changes to `AmuseAvatarOptimizerEditor.cs`. Zero label, tooltip, field name, field type, or default changes. Zero build code changes.
- One type per file, file name equals type name. New code comments state why, not what.
- Tests never run in the private Lab project. Before any Unity MCP write or reported validation: enumerate instances read-only, verify `Application.dataPath` equals the current project's `Assets` folder exactly, and pin the dev editor instance when more than one is reachable. Name instances by role only; never write an instance name, hash, or port into any file.
- Never modify `.github/workflows/pr.yml`; it carries a user-owned modification. Stage explicit paths only.
- No commit without the step ordering below; commit messages follow the repository's `type: sentence` style.

---

### Task 1: Attribute, grant, drawer, annotations, and validation

**Files:**
- Create: `Packages/com.alrauna.amuse/Runtime/ToggleLeftAttribute.cs`
- Create: `Packages/com.alrauna.amuse/Runtime/AssemblyInfo.cs`
- Create: `Packages/com.alrauna.amuse/Editor/ToggleLeftDrawer.cs`
- Modify: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs` (five `[ToggleLeft]` annotations)

**Interfaces:**
- Consumes: nothing new; `UnityEngine.PropertyAttribute`, `UnityEditor.PropertyDrawer`, `UnityEditor.EditorGUI.ToggleLeft(Rect, GUIContent, bool)`.
- Produces: `Alrauna.Amuse.Runtime.ToggleLeftAttribute` (internal, referenced by the drawer's `[CustomPropertyDrawer(typeof(ToggleLeftAttribute))]`), `Alrauna.Amuse.Editor.ToggleLeftDrawer` (internal).

- [ ] **Step 1: Create the runtime attribute**

`Packages/com.alrauna.amuse/Runtime/ToggleLeftAttribute.cs`, entire content:

```csharp
using UnityEngine;

namespace Alrauna.Amuse.Runtime
{
    /// <summary>
    /// Marks a bool field so the inspector draws the checkbox left of
    /// its label, in the style of the optimizers users already know.
    /// The drawer lives in the editor assembly, because the runtime
    /// assembly must stay free of UnityEditor types.
    /// </summary>
    internal sealed class ToggleLeftAttribute : PropertyAttribute
    {
    }
}
```

- [ ] **Step 2: Create the InternalsVisibleTo grant**

`Packages/com.alrauna.amuse/Runtime/AssemblyInfo.cs`, entire content:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Alrauna.Amuse.Editor")]
```

- [ ] **Step 3: Create the editor drawer**

`Packages/com.alrauna.amuse/Editor/ToggleLeftDrawer.cs`, entire content:

```csharp
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor
{
    /// <summary>
    /// Draws a marked bool as a toggle whose checkbox sits left of its
    /// label. BeginProperty keeps the prefab override menu and the
    /// override display that a plain PropertyField provides. The
    /// mixed-value mark stays on, because a multi-object selection
    /// must keep the fidelity it had before this drawer existed.
    /// </summary>
    [CustomPropertyDrawer(typeof(ToggleLeftAttribute))]
    internal sealed class ToggleLeftDrawer : PropertyDrawer
    {
        public override void OnGUI(
            Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            var value = EditorGUI.ToggleLeft(position, label,
                property.boolValue);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                property.boolValue = value;
            }

            EditorGUI.EndProperty();
        }
    }
}
```

- [ ] **Step 4: Annotate the five fields**

In `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`, add
`[ToggleLeft]` directly above each of these five field declarations,
keeping every other line untouched:

1. `_alphaSeparatorEnabled`
2. `_amuseDisabled`
3. `_advancedSettingsRevealed`
4. `_ignoreOutOfRangeMaterialSlots`
5. `_allowDepthTestChange`

The attribute type sits in the runtime namespace, so the component file
needs no using directive. The editor drawer names the attribute type
across the assembly boundary and needs
`using Alrauna.Amuse.Runtime;` at the top of its file. Do not rename,
reorder, retype, or re-default anything.

- [ ] **Step 5: Unity refresh and compile check**

Follow the Global Constraints identity protocol first: enumerate Unity
instances read-only, verify `Application.dataPath` equals this
repository's `Assets` folder exactly, pin the dev editor instance if
more than one is reachable. Then request an asset database refresh with
compilation and wait for readiness. Read the console filtered to
errors. Expected: no compile errors.

- [ ] **Step 6: Drawer resolution probe (best effort)**

Run this as editor code and expect `True`. If the internal reflection
surface differs in this Unity version, adapt the probe with the Unity
reflection tooling; if it cannot run at all, report the probe as
skipped and rely on Step 7 plus the manual visual check.

```csharp
using System.Reflection;
using UnityEditor;
using UnityEngine;

var componentType = typeof(Alrauna.Amuse.Runtime.AmuseAvatarOptimizer);
var field = componentType.GetField("_amuseDisabled",
    BindingFlags.NonPublic | BindingFlags.Instance);
var utilityType = typeof(Editor).Assembly
    .GetType("UnityEditor.ScriptAttributeUtility");
var handler = utilityType.GetMethod("GetHandler",
        BindingFlags.NonPublic | BindingFlags.Static, null,
        new[] { typeof(FieldInfo) }, null)
    .Invoke(null, new object[] { field });
var drawer = handler.GetType().GetProperty("drawer")
    .GetValue(handler) as PropertyDrawer;
return drawer != null && drawer.GetType().Name == "ToggleLeftDrawer";
```

Clean up any temporary object the probe creates. The probe asserts
nothing about source text; it asserts the runtime wiring the suites
cannot see.

- [ ] **Step 7: Run both EditMode suites**

Start an EditMode test run for the full `Alrauna.Amuse.Tests.Editor`
assembly, poll to completion, and record the observed counts. Repeat
for `Alrauna.Amuse.Research.Tests.Editor`. Expected: both pass, with
counts unchanged from the pre-change baseline. A filtered run that
reports 0 tests is a failure.

- [ ] **Step 8: Commit**

```bash
git add Packages/com.alrauna.amuse/Runtime/ToggleLeftAttribute.cs \
  Packages/com.alrauna.amuse/Runtime/ToggleLeftAttribute.cs.meta \
  Packages/com.alrauna.amuse/Runtime/AssemblyInfo.cs \
  Packages/com.alrauna.amuse/Runtime/AssemblyInfo.cs.meta \
  Packages/com.alrauna.amuse/Editor/ToggleLeftDrawer.cs \
  Packages/com.alrauna.amuse/Editor/ToggleLeftDrawer.cs.meta \
  Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs \
  Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs.meta
git commit -m "feat: draw the inspector checkboxes left of their labels"
```

The Unity refresh in Step 5 generates the new `.meta` files. Confirm
`git status` shows no other modified file before staging. Never stage
`.github/workflows/pr.yml`.

## Validation Record Shape

The report must record, with observed values: refresh outcome, console
error count, probe result, both suite counts, and the commit hash.
Remaining manual validation, reported to the owner afterward: the
checkbox side, tooltips, foldout indent, preset row, mixed mark on a
multi-object selection, the override context menu on a prefab instance,
and one build, observed on the dev editor instance.
