# Inspector UX Indentation, Out-of-Range Relabel, and Cutout Mip Defaults Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Indent the four inspector child regions, relabel the out-of-range slots toggle to the Allow voice, and move the cutout mip cap defaults to 3, 3, 3, and 2.

**Architecture:** Three small changes in two source files plus three preset JSONs and two docs. The indentation uses Unity's built-in `EditorGUI.IndentLevelScope` inside the existing draw methods, with one new internal static label-width helper. The relabel changes only the drawn label and tooltip through the existing `PropertyField` path, with one new `*ForTest` delegate that fires the drawn value. The fresh cutout default moves through the field initializer plus a full-field `Reset()` that also sets the backfill marker. No build pipeline, analysis, host, semantics, or report code changes.

**Tech Stack:** Unity 2022.3.22f1, C#, NDMF, NUnit via Unity Test Framework, EditMode only. No dotnet build, no Node.

**Spec:** `docs/superpowers/specs/2026-10-17-inspector-ux-indentation-design.md`
**Investigation:** `docs/superpowers/investigations/2026-10-17-inspector-ux-indentation-investigation.md`

## Working discipline

- Branch `feat/inspector-ux-indentation`, base `main` at `15a8db7`. Do not branch from anything else. The branch already exists; work on it. No separate worktree.
- Allowed mutations, exhaustive:
  - `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`
  - `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
  - `Packages/com.alrauna.amuse/Presets/safe.json`, `normal.json`, `aggressive.json`
  - `README.md`, `docs/architecture/vision.md`
  - `Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetApplierTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Presets/OptimizerPresetTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetFileStoreTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs` (doc-comment prose only)
- Never touch: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated `*.csproj`/`*.sln`, vendor packages under `Packages/` other than `com.alrauna.amuse`, the research package, and every file under `docs/superpowers/` dated before 2026-10-17.
- Every task ends with exactly one commit of the paths the task names. Never stage unrelated files.
- Plain English for every user-visible string: short active sentences, one idea per sentence, no contractions, no semicolons.
- A behavior change observes RED first against a named wrong implementation, records the observed failure, then applies the minimal production change and observes green. An assertion that passes on first run is recorded as characterization. Pure layout (the scope wrapping) has no RED; the repaint suite is its smoke validation, per the 2026-10-05 precedent.
- Falsifier tests carry the `--- Falsifier N: ... ---` block from the spec plus a no-op guard assertion, so a later change cannot silence them.
- A filtered run that reports zero tests is a failure.
- Ticking the checkboxes in this plan file is permitted progress tracking and is never committed.

## Unity validation procedure (every task)

1. Enumerate the reachable Unity instances read-only (resource `mcpforunity://instances`). For every reachable instance, read `mcpforunity://project/info` and pin with `set_active_instance` only the one whose `Application.dataPath` resolves to exactly `<repo-root>/Assets`. A case-only match is not identity. Stop when zero reachable instances match; do not launch, install, or configure anything.
2. After pinning, read `mcpforunity://editor/state` and proceed only when the editor is not playing and not compiling. Otherwise stop and report.
3. Refresh assets and wait for readiness (`refresh_unity`), then read the console for compile errors. A successful compile is never validation.
4. Run the named tests with `run_tests` (EditMode), passing the class-name regex in its group filter parameter (verify the exact parameter names in the tool docs, `xd://mcp__unitymcp_run_tests`, before first use; the tool takes test/group/assembly names, not NUnit `FullyQualifiedName~` strings). A filter that selects zero tests is a failure.
5. Record the observed pass and fail counts in the task report. Copy failure messages verbatim for any RED step.

## Global constraints

- Cutout mip caps: fresh default 3, Safe 3, Normal 3, Aggressive 2. The opacity arm keeps 4, 4, 4, 3 everywhere and its tooltip keeps Mip 4.
- Depths: Show Material Transformations body children 1 (flat, including the nested Alpha Separator view). Settings always-on children 1. Advanced-revealed children 2. Alpha Separator Settings header 2, contents 3. Everything else 0.
- The stored bool `_ignoreOutOfRangeMaterialSlots` keeps its name, type, default true, and meaning. The drawn value equals the stored value; only the drawn label and tooltip change.
- Legacy upgrade behavior is byte-identical: a payload without the cutout fields inherits its stored opacity values once. The backfill body does not change.
- `schemaVersion` stays 4. The parser range stays -1 and 0 through 10.
- New production members, exactly three: `private void Reset()` on the runtime component, `internal static float AlphaSeparatorLabelWidth(float)` and `internal static Action<bool> OnOutOfRangeToggleDrawnForTest` on the editor. Nothing else is added; nothing is removed.
- Existing tests named in a task's "must stay green" list are never weakened, skipped, or rewritten.

---

### Task 1: Runtime cutout default and full-field Reset

**Files:**
- Modify: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `AmuseAvatarOptimizer` with `_cutoutPreserveTransparencyMaxMipLevel` initializer 3 and a `private void Reset()` that restores every serialized field to its declared default and sets `_cutoutSettingsBackfilled = true`. Every later task relies on the fresh component reporting cutout 3 across a serialization round trip.

- [ ] **1.1 RED first.** In `AmuseAvatarOptimizerTests.cs`, rename `DefaultCutoutPreserveTransparencyMaxMipLevelIs4` (around `:487-498`) to `DefaultCutoutPreserveTransparencyMaxMipLevelIs3` and change the assertion to `Is.EqualTo(3)`, keeping the test's existing arrange style. Add this test beside it:

```csharp
/// <summary>
/// --- Falsifier 1: an implementation that changes only the field
/// initializer to 3 without Reset. The first serialization round trip
/// runs the upgrade backfill and copies the transparent value over
/// the fresh 3, so the post-round-trip assertion fails. ---
/// </summary>
[Test]
public void FreshCutoutDefaultSurvivesASerializationRoundTrip()
{
    var root = new GameObject("Root");
    try
    {
        var fresh = root.AddComponent<AmuseAvatarOptimizer>();
        Assert.That(fresh.CutoutPreserveTransparencyMaxMipLevel,
            Is.EqualTo(3), "the fresh default before the round trip");

        var json = JsonUtility.ToJson(fresh);
        var scratch = new GameObject("Scratch")
            .AddComponent<AmuseAvatarOptimizer>();
        try
        {
            JsonUtility.FromJsonOverwrite(json, scratch);
            Assert.That(scratch.CutoutPreserveTransparencyMaxMipLevel,
                Is.EqualTo(3), "the fresh default survives the round trip");
            // No-op guard: pinning the source component too, so a later
            // rewrite cannot silence this test by touching only one arm.
            Assert.That(fresh.CutoutPreserveTransparencyMaxMipLevel,
                Is.EqualTo(3));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(scratch);
        }
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(root);
    }
}
```

The test file imports both `System` and `UnityEngine`, so qualify `UnityEngine.Object` as every existing test in the file does. `JsonUtility` resolves through the existing `UnityEngine` using.

- [ ] **1.2 Run and record the RED.** Refresh Unity, run the group regex `AmuseAvatarOptimizerTests` (it matches the runtime fixture only; the editor fixture is `AmuseAvatarOptimizerEditorTests`). Expect both new-name tests to fail: the renamed default test on `EqualTo(3)` against the current 4, and the round-trip test before its second assertion. Copy the failure messages into the report.
- [ ] **1.3 Minimal production change.** In `AmuseAvatarOptimizer.cs`:
  - Change the initializer at `:139-140` to `private int _cutoutPreserveTransparencyMaxMipLevel = 3;`.
  - Replace the field doc comment sentence "The default of 4 matches the transparent setting." with "The default is 3. The transparent setting defaults to 4."
  - Update the marker doc comment (`:122-131`) second sentence to: "A legacy payload predates the cutout fields, so it deserializes with this marker false. The marker is also true after Reset establishes the factory defaults on a fresh component." Keep the rest of the comment.
  - Add this method after the serialized fields, before the properties:

```csharp
/// <summary>
/// Restores every serialized field to its declared default, the same
/// contract Unity's context-menu Reset offers when no Reset is
/// defined. It also sets the backfill marker, so the first
/// serialization round trip of a fresh component does not copy the
/// transparent values over the cutout defaults. Unity calls Reset
/// only in the editor, when the component is first added or the user
/// presses Reset.
/// </summary>
private void Reset()
{
    _amuseDisabled = false;
    _alphaSeparatorEnabled = true;
    _preserveTransparencyMaxMipLevel = 4;
    _preserveTransparencyMinTextureSize = 128;
    _minimumOpaqueCoveragePercent = 25;
    _minimumOpaqueCoverageToPreservePercent = 5;
    _minimumOpaqueAlphaPercent = 100;
    _polygonAlphaUpperClampPercent = 100;
    _polygonMinimumOpaqueCoveragePercent = 100;
    _cutoutSettingsBackfilled = true;
    _cutoutPreserveTransparencyMaxMipLevel = 3;
    _cutoutPreserveTransparencyMinTextureSize = 128;
    _cutoutMinimumOpaqueCoveragePercent = 25;
    _cutoutMinimumOpaqueCoverageToPreservePercent = 5;
    _allowDepthTestChange = true;
    _acceptDesktopAnisotropyDifferences = true;
    _acceptMobileAnisotropyDifferences = true;
    _ignoreOutOfRangeMaterialSlots = true;
    _advancedSettingsRevealed = false;
}
```

The right-hand values must equal each field's declared initializer. If any initializer differs from this list, stop and report instead of guessing.

- [ ] **1.4 GREEN.** Re-run the Task 1 filter. Expect the two tests green and the rest of the class untouched and green: `UpgradedComponentBackfillsCutoutValuesFromItsOwnTransparentValues`, `BackfillRunsOnceAndKeepsEditedCutoutValues`, `FreshComponentKeepsEditedCutoutValuesAcrossAReload`, `DefaultIgnoreOutOfRangeMaterialSlotsIsTrue`. Record counts.
- [ ] **1.5 Commit:** `git add Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs && git commit -m "feat: default the cutout mip cap to 3 with a full-field Reset"`.

### Task 2: Shipped preset values and every pinned test

**Files:**
- Modify: `Packages/com.alrauna.amuse/Presets/safe.json`, `Packages/com.alrauna.amuse/Presets/normal.json`, `Packages/com.alrauna.amuse/Presets/aggressive.json`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetApplierTests.cs`, `OptimizerPresetTests.cs`, `PresetParserTests.cs`, `PresetFileStoreTests.cs`

**Interfaces:**
- Consumes: Task 1's fresh default 3.
- Produces: shipped preset cutout caps 3, 3, 2 with every test pin agreeing. The Normal match canary `DefaultComponentMatchesTheShippedNormalPreset` is Falsifier 4's enforcement: an implementation that updates the JSONs but not the runtime default, or the reverse, fails it. Do not weaken it.

- [ ] **2.1 Change the three JSONs.** In `safe.json:16` and `normal.json:16`, change `"cutoutPreserveTransparencyMaxMipLevel": 4` to `3`. In `aggressive.json:16`, change `3` to `2`. Touch no other key. Keep each file's formatting byte-identical otherwise.
- [ ] **2.2 Run and record the RED.** Refresh, run the group regex `Preset` (it selects exactly the four preset fixtures; no other test full name contains it). Expect failures in: `ApplyingEachShippedPresetWritesItsCutoutValues` (JSONs 3, 3, 2 against table 4, 4, 3); `DefaultComponentMatchesTheShippedDefaults` (fresh component cutout 3 from Task 1 against helper cutout still 4); and `FlippingAnyOneFieldValueBreaksTheMatch` (its starts-matching assertion fails on the first row, fresh 3 against helper 4, before any flip runs). `ApplyWritesAllSixteenFields` stays green here, because it applies the in-memory helper object and never reads the JSONs; it changes only with 2.3's pin sync. `DefaultComponentMatchesTheShippedNormalPreset` must be green already, because Task 1's fresh 3 meets Normal's new 3. Record the failures verbatim.
- [ ] **2.3 Update the pins.**
  - `PresetApplierTests.cs:104-106`: table rows become `("Safe", 3, 128, 25, 0)`, `("Normal", 3, 128, 25, 5)`, `("Aggressive", 2, 128, 25, 10)`.
  - `PresetApplierTests.cs:242-243`: `ApplyWritesAllSixteenFields` asserts `Is.EqualTo(3)`.
  - `OptimizerPresetTests.cs:14`: `ShippedDefaults()` second positional argument becomes 3 (the first stays 4; it is the opacity arm).
  - `OptimizerPresetTests.cs:63`: flip row becomes `("_cutoutPreserveTransparencyMaxMipLevel", 4)`.
  - `PresetParserTests.cs`: the embedded `ValidJson` cutout line becomes `3`; the asserted value in `ValidSchemaParsesAllSixteenValues` becomes 3; and all four `string.Replace` needles move with it: `WithCutoutMipCap` plus `LegacyVersionTwoBody`, `LegacyVersionOneBody`, `LegacyVersionThreeBody`. Each needle must still match the new embedded text exactly; run the parser tests to prove no needle silently no-ops.
  - `PresetFileStoreTests.cs:28`: embedded `ValidBody` cutout becomes 3.
- [ ] **2.4 GREEN.** Re-run the Presets filter. Expect every test green, including `ApplyingShippedAggressiveSetsSmallestTestedMipmapToThree` (opacity arm, unchanged) and `DefaultComponentMatchesTheShippedDefaults` plus `DefaultComponentMatchesTheShippedNormalPreset`. Record counts.
- [ ] **2.5 Commit:** `git add Packages/com.alrauna.amuse/Presets/safe.json Packages/com.alrauna.amuse/Presets/normal.json Packages/com.alrauna.amuse/Presets/aggressive.json Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetApplierTests.cs Packages/com.alrauna.amuse/Tests/Editor/Presets/OptimizerPresetTests.cs Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetFileStoreTests.cs && git commit -m "feat: move shipped cutout mip caps to 3, 3, and 2"`.

### Task 3: Indentation scopes and the label-width helper

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `internal static float AlphaSeparatorLabelWidth(float widestLabelWidth)` on `AmuseAvatarOptimizerEditor`, returning `Mathf.Ceil(widestLabelWidth) + 2f - EditorGUI.indentLevel * 15f`. Depths per Global constraints.

- [ ] **3.1 Extract the helper, behavior-neutral.** In `AmuseAvatarOptimizerEditor.cs`, add:

```csharp
/// <summary>
/// The label width for one Alpha Separator Settings row. The widest
/// label text sets the base width, and each indent level subtracts
/// its 15-pixel offset, so the value column keeps its share of the
/// row at every depth.
/// </summary>
internal static float AlphaSeparatorLabelWidth(float widestLabelWidth)
{
    return Mathf.Ceil(widestLabelWidth) + 2f;
}
```

Then in `DrawAlphaSeparatorSettings`, replace only the assignment line `EditorGUIUtility.labelWidth = Mathf.Ceil(widest) + 2f;` (`:799`) with `EditorGUIUtility.labelWidth = AlphaSeparatorLabelWidth(widest);`. The `var previousWidth = EditorGUIUtility.labelWidth;` save line above it (`:798`) must survive; Task 3.5 restores from it. Nothing else changes yet, so the editor behaves exactly as at base.
- [ ] **3.2 RED first.** Add this test to `AmuseAvatarOptimizerEditorTests.cs`:

```csharp
/// <summary>
/// --- Falsifier 3: an implementation of AlphaSeparatorLabelWidth
/// that ignores EditorGUI.indentLevel and always returns
/// ceil(widest) + 2. The depth-3 assertion fails. ---
/// </summary>
[Test]
public void AlphaSeparatorLabelWidthSubtractsTheRowIndent()
{
    var original = EditorGUI.indentLevel;
    try
    {
        EditorGUI.indentLevel = 0;
        Assert.That(
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .AlphaSeparatorLabelWidth(200f),
            Is.EqualTo(202f));
        EditorGUI.indentLevel = 3;
        Assert.That(
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .AlphaSeparatorLabelWidth(200f),
            Is.EqualTo(157f));
        // No-op guard: the depth-1 case pins the per-level constant,
        // so a rewrite cannot silently redefine the helper.
        EditorGUI.indentLevel = 1;
        Assert.That(
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .AlphaSeparatorLabelWidth(50f),
            Is.EqualTo(37f));
    }
    finally
    {
        EditorGUI.indentLevel = original;
    }
}
```

- [ ] **3.3 Run and record the RED.** Run the group regex `AmuseAvatarOptimizerEditorTests`. Expect the depth-3 assertion to fail (202 against the expected 157). Record it.
- [ ] **3.4 Fix the helper.** Change the return to `return Mathf.Ceil(widestLabelWidth) + 2f - EditorGUI.indentLevel * 15f;`. Re-run the filter and observe green.
- [ ] **3.5 Wrap the scopes.** Pure layout; no RED.
  - `DrawMaterialTransformations`: after the foldout header draws and fires `OnFoldoutHeaderDrawnForTest`, wrap everything else in `using (new EditorGUI.IndentLevelScope()) { ... }`. The early returns (`:206-209`, `:235-238`, `:287`) stay; the scope disposes on each.
  - `DrawSettings`: after the closed-foldout early return, wrap the body in one `IndentLevelScope`. Inside, wrap the `if (reveal.boolValue)` block's two toggles in a nested `IndentLevelScope`. The `ApplyModifiedProperties` and the return stay outside the scopes, in their current order.
  - `DrawAlphaSeparatorSettings`: enter two nested `IndentLevelScope` instances before the foldout header draws. Inside a third `IndentLevelScope`, set `EditorGUIUtility.labelWidth = AlphaSeparatorLabelWidth(widest);`, draw the body (the two submenu calls and `DrawAcceptanceControls()`), restore `labelWidth` from the saved value, then close the third scope and the outer two.
  - Nothing outside these three methods changes. `EditorGUI.indentLevel` returns to its ambient value on every path.
- [ ] **3.6 Smoke validation.** Refresh, run the full `AmuseAvatarOptimizerEditorTests` group. Every existing test must stay green untouched: the foldout header assertions, the out-of-band repaint tests, the dry-run and play-record tests. Zero failures, nonzero test count. Record counts.
- [ ] **3.7 Commit:** `git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs && git commit -m "feat: indent inspector child menus with indent-level scopes"`.

### Task 4: Out-of-range toggle relabel

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**
- Consumes: Task 3's scoped `DrawSettings` (the toggle draws at depth 2).
- Produces: `internal static Action<bool> OnOutOfRangeToggleDrawnForTest` on `AmuseAvatarOptimizerEditor`, fired with the drawn Allow value.

- [ ] **4.1 Add the seam.** Add to the editor's `*ForTest` delegate block (`:55-62`), matching the house shape:

```csharp
internal static Action<bool> OnOutOfRangeToggleDrawnForTest;
```

- [ ] **4.2 RED first.** Add this `[UnityTest]`, mirroring the arrange of `SubmenuHeadersDrawUnderTheReveal` (`:154-175`): `NewHiddenComponent()` sets `_advancedSettingsRevealed` true, and `yield return RepaintThroughHostWindow(component)` repaints with the Settings foldout open. Reuse the file's existing `SetSwitch` and `Stored` helpers for the property access; add no new accessor helpers:

```csharp
/// <summary>
/// --- Falsifier 2: an implementation that inverts the drawn value
/// (draws the negation of the stored bool) or renames the stored
/// field. The drawn-value assertions fail against the inversion,
/// because stored true must draw checked. A rename fails the property
/// lookup outright. ---
/// </summary>
[UnityTest]
public IEnumerator OutOfRangeToggleDrawsTheStoredValueInTheAllowVoice()
{
    var component = NewHiddenComponent();
    var drawn = new List<bool>();
    Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
        .OnOutOfRangeToggleDrawnForTest = value => drawn.Add(value);
    try
    {
        SetSwitch(component, "_ignoreOutOfRangeMaterialSlots", true);
        drawn.Clear();
        yield return RepaintThroughHostWindow(component);
        Assert.That(drawn, Does.Contain(true),
            "stored true draws the Allow checkbox checked");
        Assert.That(
            Stored(component, "_ignoreOutOfRangeMaterialSlots"),
            Is.EqualTo(1), "a plain repaint writes nothing");

        SetSwitch(component, "_ignoreOutOfRangeMaterialSlots", false);
        drawn.Clear();
        yield return RepaintThroughHostWindow(component);
        Assert.That(drawn, Does.Contain(false),
            "stored false draws the Allow checkbox unchecked");
        Assert.That(
            Stored(component, "_ignoreOutOfRangeMaterialSlots"),
            Is.EqualTo(0), "a plain repaint writes nothing");
    }
    finally
    {
        Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
            .OnOutOfRangeToggleDrawnForTest = null;
    }
}
```

Reuse the file's existing `SetSwitch` (`:968-975`) and `Stored` (`:951-966`) helpers. `Stored` returns the int view of the bool property, so the stored-value assertions read `Is.EqualTo(1)` and `Is.EqualTo(0)`. Add no new accessor helpers.

- [ ] **4.3 Run and record the RED.** Run the `AmuseAvatarOptimizerEditorTests` group. Expect failure: the delegate never fires at base, so `drawn` stays empty and the first assertion fails. Record it.
- [ ] **4.4 Minimal production change.** In `DrawSettings`, inside the advanced block, replace only the `GUIContent` of the existing `EditorGUILayout.PropertyField` call for `_ignoreOutOfRangeMaterialSlots` (`:1210-1217`), and fire the new delegate right after it. The property, the drawer, and the drawn value are unchanged:

```csharp
EditorGUILayout.PropertyField(
    serializedObject.FindProperty("_ignoreOutOfRangeMaterialSlots"),
    new GUIContent(
        "Allow Out-of-Range Material Slots",
        "Some animation files animate material slots that do not " +
        "exist on this mesh. When this setting is on, AMUSE ignores " +
        "these extra slot animations and optimizes the valid slots. " +
        "Turn this setting off to refuse meshes with extra slot " +
        "animations."));
OnOutOfRangeToggleDrawnForTest?.Invoke(
    serializedObject.FindProperty("_ignoreOutOfRangeMaterialSlots")
        .boolValue);
```

The drawn value equals the stored value. There is no negation anywhere; "Allow" names the same disposition as the stored true.

- [ ] **4.5 Cutout tooltip sentence.** In `AmuseAvatarOptimizerEditor.cs:674-676`, change the fragment `"The default of Mip 4 fits most viewing "` to `"The default of Mip 3 fits most viewing "`. The neighboring fact sentence "renders at 128 by 128 at mip 4" and the opacity tooltip at `:508-511` stay untouched (invariant 4).
- [ ] **4.6 GREEN.** Re-run the `AmuseAvatarOptimizerEditorTests` group. The new test is green; every existing test stays green untouched. Record counts.
- [ ] **4.7 Commit:** `git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs && git commit -m "feat: relabel the out-of-range slots toggle and the cutout mip tooltip"`.

### Task 5: Documentation sentences

**Files:**
- Modify: `README.md`, `docs/architecture/vision.md`, `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs` (doc comment only)

**Interfaces:**
- Consumes: all prior tasks. Produces: no code.

- [ ] **5.1 README, sentence-scoped.** In the Safe bullet, replace exactly the sentence "It still disables **Ignore Out-of-Range Material Slots** and **Allow Depth Test Change on Moved Triangles**, and it never flattens a mixed material." with "It keeps **Allow Out-of-Range Material Slots** and **Allow Depth Test Change on Moved Triangles** off, and it never flattens a mixed material." The sentence after it begins with the word `Normal` on the same line and must survive untouched. In the Aggressive bullet, replace "Aggressive uses 10 percent and sets Smallest Tested Mipmap to Mip 3." with "Aggressive uses 10 percent and sets Smallest Tested Mipmap to Mip 3. Cutout textures use Mip 2."
- [ ] **5.2 Vision, sentence-scoped.** In `docs/architecture/vision.md`, replace only the sentence "The default alpha policy limits evidence to mip level 4 and a minimum size of 128 texture pixels." with "The default alpha policy limits evidence to mip level 4, and to mip level 3 for cutout sources, with a minimum size of 128 texture pixels." Every other sentence in the paragraph stays.
- [ ] **5.3 Fixture prose.** In `AlphaSeparationPreparationTests.cs`, update the doc comment around `:4917-4919` so its mip wording matches the cap-3 default (it currently says the proof runs "through mip 4" under the default cap). Assertions stay untouched.
- [ ] **5.4 Verify.** `grep -n "Ignore Out-of-Range" README.md` returns nothing. `grep -n "Allow Out-of-Range" README.md` finds the Safe sentence. The Mip 4 sentence survives in the editor opacity tooltip only (`AmuseAvatarOptimizerEditor.cs:508-511`), not in `README.md` or the cutout tooltip. Refresh Unity and run the `AlphaSeparationPreparationTests` group; expect green with a nonzero count, because only prose changed.
- [ ] **5.5 Commit:** `git add README.md docs/architecture/vision.md Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs && git commit -m "docs: split the mip default by arm and use the allow label"`.

### Task 6: Full validation

**Files:**
- Modify: nothing.

- [ ] **6.1 Refresh Unity, read the console, and confirm zero compile errors.**
- [ ] **6.2 Run the full `Alrauna.Amuse.Tests.Editor` assembly.** Expect zero failures, nonzero count. Record the observed totals.
- [ ] **6.3 Run the full `Alrauna.Amuse.Research.Tests.Editor` assembly.** Expect zero failures, nonzero count. Record the observed totals.
- [ ] **6.4 Falsifier presence check.** `grep -rn "Falsifier" Packages/com.alrauna.amuse/Tests/Editor/` finds the three new blocks (Falsifiers 1, 2, 3) with their no-op guards. Falsifier 4 rides the existing `DefaultComponentMatchesTheShippedNormalPreset`, which the full run proves green.
- [ ] **6.5 Identifier sweep.** Over every file this plan touched: no absolute paths, no drive letters, no home-directory paths, no four-digit ports, no at-sign joined to a hexadecimal hash, no private avatar or material names. Every hit is a defect; fix before reporting.
- [ ] **6.6 Report.** State the observed counts for both assemblies, the characterization runs (any assertion that passed on first run), the falsifier check, and any deviation from this plan.
