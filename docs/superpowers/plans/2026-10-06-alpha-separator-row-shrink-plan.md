# Alpha Separator Row Shrink Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The six rows under "Alpha Separator Settings" keep their label text fully readable while the inspector window narrows, by pinning one shared label column to the widest label and letting the controls absorb all lost width.

**Architecture:** A private nested `SharedLabelWidthScope : IDisposable` sets `EditorGUIUtility.labelWidth` to `ceil(max label text width over the six rows) + 2` inside one scope in `DrawAlphaSeparatorSettings`, and restores the previous value on dispose. The six row `GUIContent`s move verbatim from the call sites into one static array that feeds both the draw calls and the live measurement. No serialization, no wording, no build changes.

**Tech Stack:** Unity 2022.3 IMGUI (`EditorGUILayout`, `EditorGUIUtility`), C# 9, editor-only assembly `Alrauna.Amuse.Editor`. Validation runs through Unity MCP against the dev editor instance.

**Spec:** `docs/superpowers/specs/2026-10-06-alpha-separator-row-shrink-design.md`
Investigation (source citations): `docs/superpowers/investigations/2026-10-06-slider-dropdown-row-narrowing-investigation.md`

## Global Constraints

- One production file changes: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`. No new production files. Helper types are private and nested.
- Label and tooltip strings move verbatim. No wording change anywhere.
- No serialized field, default, property, or build pipeline change.
- Shared column = `Mathf.Ceil(max EditorStyles.label.CalcSize over the six row contents) + 2f`. Nothing hardcoded; measured live with the active skin.
- The scope must restore `EditorGUIUtility.labelWidth` on Dispose even on exception (`using` compiles to try/finally). This restore is load-bearing.
- RED/GREEN does not apply: no test draws the inspector (spec section 5, precedent from the 2026-10-05 records). Both EditMode suites must pass with zero failures and unchanged tests.
- Repo policy: comments use plain technical English; XML doc comments state why, not what. One public type per file stays true; nested helpers are private.
- Unity MCP safety: before every editor write, assert `Application.dataPath` ends with `<repo-root>/Assets` and abort on mismatch. Name machines by role only. Never write instance names, hashes, ports, or machine paths into any file.
- No absolute paths in any file. Scratch editor scripts are created under `Assets/Editor/`, used, and deleted (script plus `.meta`) before any commit.
- Commits are authorized for this execution. Style: `feat:` for the product change, `docs:` for the records. Commit only the files the plan names.
- Validation truth: observed tool output only. A filtered run reporting 0 tests is a failure. A successful compile is never validation.

---

### Task 1: Shared label column scope for the six alpha separator rows

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` (all product changes)
- Create then delete: `Assets/Editor/AmuseRowProbe.cs` and its `.meta` (throwaway probe, never committed)
- Commit: `docs/superpowers/investigations/2026-10-06-slider-dropdown-row-narrowing-investigation.md`, `docs/superpowers/specs/2026-10-06-alpha-separator-row-shrink-design.md`, `docs/superpowers/plans/2026-10-06-alpha-separator-row-shrink-plan.md`

**Interfaces:**
- Consumes: the file state at commit `6d9393c`. The six draw methods `DrawMipCapPopup`, `DrawMinTextureSizePopup`, `DrawCoverageSlider`, `DrawAlphaPolicyControls`, and `DrawAlphaSeparatorSettings` keep their names and signatures.
- Produces: nothing consumed by later tasks. This is the only implementation task.

- [ ] **Step 1: Move the six row contents into static fields**

In `AmuseAvatarOptimizerEditor.cs`, add one private static readonly `GUIContent` field per row, in this order and with these field names, placed directly above `DrawAlphaSeparatorSettings`. Move each label and tooltip string verbatim from the call site listed. Do not edit a single character of the strings.

1. `MipCapRowContent` — label "Smallest Tested Mipmap", tooltip from `DrawMipCapPopup` (current lines 158 to 189).
2. `MinTextureRowContent` — label "Smallest Tested Texture", tooltip from `DrawMinTextureSizePopup` (current lines 219 to 242).
3. `MaterialCoverageRowContent` — label "Minimum Opaque Coverage (Per Material)", tooltip from `DrawCoverageSlider` (current lines 248 to 263).
4. `TextureClampRowContent` — label "Alpha Upper Clamp (Per Texture)", tooltip from `DrawAlphaPolicyControls` (current lines 270 to 290).
5. `PolygonCoverageRowContent` — label "Minimum Opaque Coverage (Per Polygon)", tooltip from `DrawAlphaPolicyControls` (current lines 293 to 312).
6. `PolygonClampRowContent` — label "Alpha Upper Clamp (Per Polygon)", tooltip from `DrawAlphaPolicyControls` (current lines 315 to 338).

Each field keeps the call site's exact string concatenation shape. Example for the first field (the tooltip body is the verbatim string from the current call site, lines 159 to 189):

```csharp
        private static readonly GUIContent MipCapRowContent = new GUIContent(
            "Smallest Tested Mipmap",
            "A mipmap is a smaller copy of a texture. Each " +
            // ... verbatim tooltip text from the current call site ...
            "triangles.");
```

Then add the measurement source with this doc comment:

```csharp
        /// <summary>
        /// The six Alpha Separator Settings row contents in draw order.
        /// One source of truth feeds both the draw calls and the shared
        /// label width, so a label change cannot drift away from its
        /// measurement.
        /// </summary>
        private static readonly GUIContent[] RowContents =
        {
            MipCapRowContent,
            MinTextureRowContent,
            MaterialCoverageRowContent,
            TextureClampRowContent,
            PolygonCoverageRowContent,
            PolygonClampRowContent,
        };
```

- [ ] **Step 2: Add the scope and the measurement helper**

Add these two private nested members inside `AmuseAvatarOptimizerEditor`, directly below the `RowContents` array:

```csharp
        /// <summary>
        /// Pins the label column of the Alpha Separator Settings rows to
        /// the widest of the six labels and restores the previous
        /// process-wide value on dispose. The restore is load-bearing:
        /// EditorGUIUtility.labelWidth is process-wide static state, and
        /// a missed restore leaks into later rows of the same GUI pass.
        /// The pin exists so the controls give up width before the label
        /// text: the column stays fixed while the window shrinks, so the
        /// label text never truncates inside the row's fitting range.
        /// </summary>
        private readonly struct SharedLabelWidthScope : IDisposable
        {
            private const float LabelWidthPadding = 2f;

            private readonly float _previous;

            internal SharedLabelWidthScope()
            {
                _previous = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = SharedLabelWidth();
            }

            public void Dispose()
            {
                EditorGUIUtility.labelWidth = _previous;
            }
        }

        /// <summary>
        /// Measures the shared label column live with the active skin,
        /// so editor font size and skin changes are picked up. The 2
        /// pixel padding is insurance for the native label paint inset.
        /// </summary>
        private static float SharedLabelWidth()
        {
            var widest = 0f;
            foreach (var content in RowContents)
            {
                widest = Mathf.Max(
                    widest, EditorStyles.label.CalcSize(content).x);
            }

            return Mathf.Ceil(widest) + 2f;
        }
```

- [ ] **Step 3: Open one scope in DrawAlphaSeparatorSettings**

Replace the body of `DrawAlphaSeparatorSettings` between the foldout early return and `serializedObject.ApplyModifiedProperties();`:

```csharp
            using (new SharedLabelWidthScope())
            {
                DrawMipCapPopup();
                DrawMinTextureSizePopup();
                DrawCoverageSlider();
                DrawAlphaPolicyControls();
            }
            serializedObject.ApplyModifiedProperties();
```

- [ ] **Step 4: Switch the six call sites to the static contents**

Replace each inline `new GUIContent(...)` argument with the matching static field. The value, index, and options arguments stay exactly as they are:

1. `DrawMipCapPopup`: `EditorGUILayout.Popup(MipCapRowContent, index, options)`
2. `DrawMinTextureSizePopup`: `EditorGUILayout.Popup(MinTextureRowContent, sizeIndex, sizeOptions)`
3. `DrawCoverageSlider`: `EditorGUILayout.IntSlider(MaterialCoverageRowContent, Mathf.Clamp(coverageProperty.intValue, 0, 100), 0, 100)`
4. `DrawAlphaPolicyControls` first slider: `EditorGUILayout.IntSlider(TextureClampRowContent, Mathf.Clamp(alphaProperty.intValue, 0, 100), 0, 100)`
5. `DrawAlphaPolicyControls` second slider: `EditorGUILayout.IntSlider(PolygonCoverageRowContent, Mathf.Clamp(coverageProperty.intValue, 0, 100), 0, 100)`
6. `DrawAlphaPolicyControls` third slider: `EditorGUILayout.IntSlider(PolygonClampRowContent, Mathf.Clamp(clampProperty.intValue, 0, 100), 0, 100)`

- [ ] **Step 5: Compile**

Request an asset database refresh with script compilation and wait for readiness through `xd://mcp__unitymcp_refresh_unity` (read the device path first for its schema). Then read the console through `xd://mcp__unitymcp_read_console` filtered to errors.

Expected: zero compile errors, zero new errors.

- [ ] **Step 6: Run both full EditMode suites**

Start a test run through `xd://mcp__unitymcp_run_tests` in EditMode mode covering both test assemblies, then poll `xd://mcp__unitymcp_get_test_job` until the job finishes.

Expected: zero failures, zero errors. Record the observed passed and skipped counts. Existing tests must be unchanged; if any test fails, stop and report instead of editing tests.

- [ ] **Step 7: Run the numeric contract probe in a real GUI pass**

First run this non-GUI probe through `xd://mcp__unitymcp_execute_code` (method body, `return` the string). It verifies the measurement and the override mechanism on the live skin:

```csharp
var labels = new string[] {
    "Smallest Tested Mipmap", "Smallest Tested Texture",
    "Minimum Opaque Coverage (Per Material)", "Alpha Upper Clamp (Per Texture)",
    "Minimum Opaque Coverage (Per Polygon)", "Alpha Upper Clamp (Per Polygon)" };
var widest = 0f;
foreach (var text in labels) {
    var w = EditorStyles.label.CalcSize(new GUIContent(text)).x;
    if (w > widest) widest = w;
}
var shared = Mathf.Ceil(widest) + 2f;
var previous = EditorGUIUtility.labelWidth;
EditorGUIUtility.labelWidth = shared;
var readBack = EditorGUIUtility.labelWidth;
EditorGUIUtility.labelWidth = previous;
var restored = EditorGUIUtility.labelWidth == previous;
var longest = 0f;
foreach (var text in labels) {
    var w = EditorStyles.label.CalcSize(new GUIContent(text)).x;
    if (w > longest) longest = w;
}
return "dataPath=" + Application.dataPath
    + " widest=" + widest
    + " shared=" + shared
    + " readBack=" + readBack
    + " restored=" + restored
    + " columnCoversLongest=" + (shared >= longest);
```

Expected: `shared` equals 239 on the 2026-10-06 skin (widest 237 plus 2), `readBack` equals `shared`, `restored` true, `columnCoversLongest` true. If `shared` differs from 239 because the skin changed, keep going: the contract is the measured value, not 239.

Then create the throwaway real-pass probe. Create `Assets/Editor/AmuseRowProbe.cs` with exactly this content (plus its auto-generated `.meta`):

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Throwaway validation probe for the alpha separator row shrink.
// Created and deleted by the validation run; never committed.
public static class AmuseRowProbe
{
    private static readonly string[] Labels =
    {
        "Smallest Tested Mipmap",
        "Smallest Tested Texture",
        "Minimum Opaque Coverage (Per Material)",
        "Alpha Upper Clamp (Per Texture)",
        "Minimum Opaque Coverage (Per Polygon)",
        "Alpha Upper Clamp (Per Polygon)",
    };

    private static readonly List<ProbeWindow> Windows =
        new List<ProbeWindow>();

    private class ProbeWindow : EditorWindow
    {
        internal float recordedColumn = -1f;
        internal float recordedRowWidth = -1f;
        internal bool recordedRestore = false;
        internal bool done;
        private int passes;

        private void OnGUI()
        {
            if (done)
            {
                return;
            }

            var previous = EditorGUIUtility.labelWidth;
            float widest = 0f;
            foreach (var label in Labels)
            {
                var w = EditorStyles.label.CalcSize(
                    new GUIContent(label)).x;
                if (w > widest) widest = w;
            }

            EditorGUIUtility.labelWidth = Mathf.Ceil(widest) + 2f;
            var row = EditorGUILayout.GetControlRect(
                true, 18f, EditorStyles.popup);
            recordedColumn = EditorGUIUtility.labelWidth;
            recordedRowWidth = row.width;
            EditorGUI.Popup(
                row, new GUIContent(Labels[0]), 0,
                new[] { "All Mips", "Mip 0" });
            EditorGUILayout.IntSlider(
                new GUIContent(Labels[2]), 50, 0, 100);
            EditorGUIUtility.labelWidth = previous;
            recordedRestore =
                EditorGUIUtility.labelWidth == previous;
            if (++passes >= 3)
            {
                done = true;
                Close();
            }
        }
    }

    public static void Begin()
    {
        CloseAll();
        var widths = new[] { 450, 400, 350, 300 };
        var y = 40f;
        foreach (var width in widths)
        {
            var window = CreateInstance<ProbeWindow>();
            window.position = new Rect(60f, y, width, 120f);
            window.ShowUtility();
            Windows.Add(window);
            y += 140f;
        }
    }

    public static string Result()
    {
        var lines = new List<string>();
        foreach (var window in Windows)
        {
            var controlArea =
                window.recordedRowWidth - window.recordedColumn - 2f;
            var stage = controlArea >= 105f
                ? "track+field"
                : "bare-field";
            lines.Add(
                "window=" + Mathf.RoundToInt(window.position.width)
                + " column=" + Mathf.RoundToInt(window.recordedColumn)
                + " rowWidth=" + Mathf.RoundToInt(window.recordedRowWidth)
                + " controlArea=" + Mathf.RoundToInt(controlArea)
                + " stage=" + stage
                + " restored=" + window.recordedRestore
                + " done=" + window.done);
        }

        return string.Join("\n", lines);
    }

    public static void CloseAll()
    {
        foreach (var window in Windows)
        {
            if (window != null)
            {
                window.Close();
            }
        }

        Windows.Clear();
    }
}
```

Refresh Unity and wait for readiness, then run through `xd://mcp__unitymcp_execute_code`:

Call A: `return AmuseRowProbe.Begin() == null ? "started" : "started";` — verify `Application.dataPath` first inside the same call and return an abort string on mismatch instead of starting.

Wait at least 10 seconds (editor idle repaints run the passes), then Call B: `return AmuseRowProbe.Result();`

Expected at the 2026-10-06 skin: `column=239` for every window; `restored=True` and `done=True` for every window; `stage=track+field` for the 450 and 400 windows (control area 205 and 155); `stage=bare-field` for the 300 window (control area is negative to 57, below 105); the 350 window is the borderline case and may report either stage, because the real content width sits near the 346 pixel threshold. The contract is the 105 pixel threshold on the recorded control area, not the nominal window width.

Then delete `Assets/Editor/AmuseRowProbe.cs` and `Assets/Editor/AmuseRowProbe.meta`, refresh again, and confirm through a project file listing that neither file exists.

- [ ] **Step 8: Structural smoke on the real component inspector**

Through `xd://mcp__unitymcp_execute_code`, in one call: assert `Application.dataPath` matches the repo project, create a scratch `GameObject` named `AmuseRowSmokeTemp` at the scene root with an `AmuseAvatarOptimizer` component, set `Selection.activeGameObject` to it, and return the created instance id. This opens the real inspector on the real rows.

Read the console filtered to errors. Expected: no exceptions from `AmuseAvatarOptimizerEditor` or its drawing path.

Delete the scratch object in a second call (`Object.DestroyImmediate`) and confirm the console stays clean.

Not agent-observable, reported to the owner as manual residue: tooltip hover rendering, popup open and option selection, the visual readability impression at each width, and the editor font size preference change.

- [ ] **Step 9: Sweep and diff checks**

Run the identifier sweep over every file changed or created in this task:

```bash
git status --porcelain
grep -nE '@[0-9a-fA-F]{6,}|[A-Za-z]:[\\/]|/Users/|/home/|d5617927|13495ff7|Census-Lab|6401|6402' Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs
git diff --check
```

Expected: the sweep finds nothing (exit 1), `git diff --check` clean, and `git status` shows exactly the editor file plus the three uncommitted doc files. Any hit is a defect: fix it before the commit step.

- [ ] **Step 10: Commit the product change**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs
git commit -m "feat: keep alpha separator labels readable as the inspector narrows"
```

- [ ] **Step 11: Commit the design records**

```bash
git add docs/superpowers/investigations/2026-10-06-slider-dropdown-row-narrowing-investigation.md docs/superpowers/specs/2026-10-06-alpha-separator-row-shrink-design.md docs/superpowers/plans/2026-10-06-alpha-separator-row-shrink-plan.md
git commit -m "docs: add alpha separator row shrink investigation, spec, and plan"
```

Expected afterwards: `git status --porcelain` shows only the pre-existing unrelated `.github/workflows/pr.yml` modification.

## Verification record for the plan

The plan author verified on 2026-10-06 against commit `6d9393c`: the six call-site line ranges in Step 1, the draw method names, the foldout body shape in Step 3, and the value arguments in Step 4. The probe code avoids internal Unity members (`EditorGUI.kSingleLineHeight` is internal and replaced by the literal `18f`).
