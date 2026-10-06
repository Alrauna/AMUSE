# Alpha Separator Complexity Review Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the eight complexity cuts from the 2026-10-06 branch
review. The branch loses about 215 lines. Seven cuts keep behavior;
the preset cut ships one preset instead of three identical ones.

**Architecture:** No architecture change. One editor file inlines a
scope struct and adds a slider helper. One drawer loses redundant
platform calls. The plugin trigger returns its component. The preset
store ships one file. Four test files consolidate repeated fixtures
into row-driven sources and one shared helper.

**Tech Stack:** Unity 2022.3 editor C#, Newtonsoft JSON, NUnit
EditMode tests.

**Spec:** `docs/superpowers/specs/2026-10-06-alpha-separator-complexity-review-design.md`

**Working discipline:** Branch `feature/optimizer-ui-rework`, review
point `7f7cd73`. Allowed mutations: the files named in the tasks
below, and nothing else. The uncommitted change to
`.github/workflows/pr.yml` is user-owned. Do not touch, stage, or
absorb it. Commit steps run only with explicit session authorization.
Tasks 1 through 7 are behavior-preserving: their guard is the existing
suite, and there are no RED steps. Task 8 is the one RED/GREEN task.
Record observed test counts before and after every task. Stop and
return evidence when a count changes unexpectedly, a test fails, or a
cut turns out to be load-bearing.

This plan supersedes Task 5 of
`docs/superpowers/plans/2026-10-05-complexity-review-plan.md`: Task 3
here rewrites the same lines through the shared fixture helper and
adopts that task's two cuts.

## Global Constraints

- No behavior change except the preset row in Task 8. No refusal
  value, schema value, drawn order, label text, or assert message text
  changes. The fixture object name drift in Task 3 is not a behavior
  change, because no assert reads a fixture object name.
- Never weaken a test assertion. The consolidations rewrite how an
  assertion is expressed, never what it requires. Task 1 adds one
  field-pin assert; that is a strengthening.
- A filtered test run that reports 0 tests is a failure. Record
  observed counts for every filter run.
- Row-driven sources keep the case counts: Task 1 stays at 2 cases,
  Task 4 stays at 4 cases.
- Unity MCP safety, before any editor use: read
  `mcpforunity://instances`, inspect `Application.dataPath`, require
  an exact match to the repository `Assets` folder, and pin the
  instance when more than one is connected. Tests never run in the
  Census Lab project.
- No absolute or machine-specific paths in code, comments, docs, or
  commit messages.
- Dated records are history. Do not edit the investigation or the
  2026-10-05 plan.

---

### Task 1: Rows for the plugin gate tests

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Produces: `private static IEnumerable<TestCaseData> GateSwitchRows()`
  with rows `("_amuseDisabled", true, "a disabled component")` and
  `("_alphaSeparatorEnabled", false, "a switched-off alpha
  separator")`, and the test body
  `SwitchedGateKeepsThePipelineIdle(string, bool, string)`. Task 2
  relies on these two cases as its guard.
- Deletes: `DisabledComponentDoesNotActivateThePipeline` and
  `AlphaSeparatorSwitchedOffDoesNotActivateThePipeline` as separate
  methods. `SetName` preserves both names as case display names.

- [ ] **Step 1: Record the baseline**

Run the `AmusePlatformFinishPluginTests` EditMode filter on the dev
editor instance. Record the observed case count and that all passed.

- [ ] **Step 2: Add the source and the body**

Add `GateSwitchRows` and `SwitchedGateKeepsThePipelineIdle` exactly as
spec section 7 shows. Both rows carry `SetName` with the two old test
names. The body carries the fixture from the old tests, the field pin
assert, the property flip, the pass execution, and the two zero
counter asserts composed from the row's subject string.

- [ ] **Step 3: Delete the two old tests**

Remove both old test methods. Their fixture and asserts now live in
the body from Step 2.

- [ ] **Step 4: Run the guard**

Rerun the `AmusePlatformFinishPluginTests` filter. The observed gate
case count must be 2, with both old names visible as case names, and
all must pass. The rest of the filter is unchanged.

- [ ] **Step 5: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "test: drive both plugin gate switches from one source"
```

---

### Task 2: The trigger returns the component

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`

**Interfaces:**
- Consumes: the two gate cases from Task 1.
- Produces: `private static
  Alrauna.Amuse.Runtime.AmuseAvatarOptimizer
  ActivatedOptimizer(BuildContext context)`.
- Deletes: `TriggerActivated` and `AlphaSeparatorSwitchedOff`.

- [ ] **Step 1: Record the baseline**

Run the `AmusePlatformFinishPluginTests` filter. Record the observed
count and that all passed.

- [ ] **Step 2: Rename and retarget the trigger**

Rename `TriggerActivated` to `ActivatedOptimizer`. Change the return
type to `Alrauna.Amuse.Runtime.AmuseAvatarOptimizer`. Return `null`
for the missing root and the missing or disabled component. Keep the
V1 trigger doc comment and add one sentence: the return carries the
component so downstream feature switches read their own property.

- [ ] **Step 3: Rewrite the call sites**

At the pipeline check, hold the return:

```csharp
var optimizer = ActivatedOptimizer(context);
if (optimizer == null)
{
    return;
}
```

Replace the `AlphaSeparatorSwitchedOff(context)` call with:

```csharp
if (!optimizer.AlphaSeparatorEnabled)
{
    return;
}
```

Keep the feature-switch comment block above it verbatim.

- [ ] **Step 4: Delete the duplicate gate**

Remove `AlphaSeparatorSwitchedOff` and its doc block. There are no
other callers; a repository search confirms this before deletion.

- [ ] **Step 5: Run the guard**

Refresh the dev editor instance. Confirm zero compile errors. Rerun
the `AmusePlatformFinishPluginTests` filter. The observed count must
equal the Task 1 baseline and all must pass.

- [ ] **Step 6: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs
git commit -m "refactor: the trigger returns the optimizer component"
```

---

### Task 3: One gated-switch fixture for the integration tests

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Tests/Editor/Build/AaoMergedConsumptionTests.cs`

**Interfaces:**
- Produces: `private static (GameObject root, Material transparent,
  SkinnedMeshRenderer renderer) CreateGatedSwitchFixture(string
  flipProperty, bool flipValue, string animationTag, string
  textureTag, string displayName)` as spec section 8 shows.

This task supersedes Task 5 of the 2026-10-05 complexity review plan
and adopts its two cuts.

- [ ] **Step 1: Record the baseline**

Run the `AaoMergedConsumptionTests` EditMode filter. Record the
observed case count and that all passed.

- [ ] **Step 2: Add the helper**

Add `CreateGatedSwitchFixture` exactly as spec section 8 shows. The
renderer and mesh names derive from `displayName`. The helper stops
after renderer creation, so each test keeps its own `ProcessAvatar`
call and assert messages.

- [ ] **Step 3: Convert the disable twin**

`SerializedDisableControlKeepsTheBuildUntouched` calls the helper
with `("_amuseDisabled", true, "disabled", "banded_disabled",
"AMUSE disabled")` and keeps its try/finally, `ProcessAvatar`, state
asserts, and messages. Replace the clone-list assert with the direct
`Is.Null.Or.Empty` form and the sweep with a direct assert on the
returned renderer's `sharedMaterials`, message texts verbatim from
spec section 8. Delete the `HashSet<Material>` wrapper and the
`GetComponentsInChildren` sweep.

- [ ] **Step 4: Convert the alpha twin**

`SerializedAlphaSeparatorSwitchKeepsTheBuildUntouched` calls the
helper with `("_alphaSeparatorEnabled", false, "alpha-switch",
"banded_alpha_switch", "AMUSE alpha switch")` and keeps its tail
unchanged. Delete its inlined setup.

- [ ] **Step 5: Run the guard**

Rerun the `AaoMergedConsumptionTests` filter. The observed count must
equal the baseline and all must pass.

- [ ] **Step 6: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AaoMergedConsumptionTests.cs
git commit -m "test: share the gated-switch integration fixture"
```

---

### Task 4: Rows for the component switch fields

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`

**Interfaces:**
- Produces: `private static IEnumerable<TestCaseData>
  SwitchFieldRows()` with one row per field, each row carrying the
  field name, default value, toggled value, and a reader delegate,
  as spec section 9 shows.

- [ ] **Step 1: Record the baseline**

Run the `AmuseAvatarOptimizerTests` EditMode filter. Record the
observed case count and that all passed.

- [ ] **Step 2: Add the source and the two bodies**

Add `SwitchFieldRows`, `SwitchFieldReadsItsInitializerDefault`, and
`SwitchFieldRoundTripsThroughTheSerializedProperty` exactly as spec
section 9 shows. The Reset comment sits on the reveal row in the
source.

- [ ] **Step 3: Delete the four old tests**

Remove `DefaultAlphaSeparatorEnabledIsTrue`,
`AlphaSeparatorEnabledSerializedPropertyCanBeToggled`,
`DefaultAdvancedSettingsRevealedIsFalse`, and
`AdvancedSettingsRevealedSerializedPropertyCanBeToggled`.

- [ ] **Step 4: Run the guard**

Rerun the `AmuseAvatarOptimizerTests` filter. The four new cases must
appear, two through each body, and the observed total must equal the
baseline. All must pass.

- [ ] **Step 5: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs
git commit -m "test: drive the switch fields from one source"
```

---

### Task 5: The drawer trusts BeginProperty

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/ToggleLeftDrawer.cs`

- [ ] **Step 1: Delete the manual mixed-value state**

Remove `EditorGUI.showMixedValue = property.hasMultipleDifferentValues;`
and `EditorGUI.showMixedValue = false;`. The result is spec section
4's body. Rewrite the doc comment: keep the BeginProperty sentence,
delete the mixed-value sentence.

- [ ] **Step 2: Compile**

Refresh the dev editor instance. Confirm zero compile errors.

- [ ] **Step 3: Smoke check multi-object behavior**

On the dev editor instance, select one avatar root with the
`AmuseAvatarOptimizer` component. Duplicate it, set the Disable AMUSE
toggle on the duplicate only, and select both. Confirm the toggle
draws with the mixed-value mark. Set both to the same value and
confirm the mark clears. Undo the changes.

- [ ] **Step 4: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/ToggleLeftDrawer.cs
git commit -m "refactor: BeginProperty carries the mixed value"
```

---

### Task 6: The label width pins inline

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`

**Interfaces:**
- Deletes: `SharedLabelWidthScope`, `SharedLabelWidthScope.Begin`,
  `SharedLabelWidth`, and their doc comment blocks.
- Adds: the file's `using System.Linq;` import.

- [ ] **Step 1: Inline the pin**

Add `using System.Linq;` to the imports. Replace the scope block in
`DrawAlphaSeparatorSettings` with spec section 2's inline pin, four
draw calls, and restore. Keep the `Ceil` and the 2 pixel padding.

- [ ] **Step 2: Delete the scope**

Remove the `SharedLabelWidthScope` struct, the `Begin` factory, the
`SharedLabelWidth` helper, and their doc comment blocks. A repository
search confirms no other reference.

- [ ] **Step 3: Compile and smoke check**

Refresh the dev editor instance. Confirm zero compile errors. Open
the `AmuseAvatarOptimizer` inspector, open the Alpha Separator
Settings foldout, and narrow the window. Confirm the label column
stays pinned and no label truncates, the same behavior the scope
struct produced.

- [ ] **Step 4: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs
git commit -m "refactor: pin the label width inline"
```

---

### Task 7: One slider helper

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`

**Interfaces:**
- Consumes: the inspector shape after Task 6.
- Produces: `private int PercentSlider(string propertyPath,
  GUIContent content)`.

- [ ] **Step 1: Add the helper**

Add `PercentSlider` exactly as spec section 3 shows.

- [ ] **Step 2: Convert the call sites**

Rewrite `DrawCoverageSlider` and `DrawAlphaPolicyControls` as spec
section 3 shows. The drawn order stays: alpha percent, polygon
coverage, polygon clamp. The clamp property still ends at
`NormalizePolygonClamp`'s value.

- [ ] **Step 3: Compile and smoke check**

Refresh the dev editor instance. Confirm zero compile errors. Move
each of the four sliders in the Alpha Separator Settings foldout and
confirm each writes its value back across a selection change and an
undo. Move the polygon clamp to 100 and confirm the inert sentinel
behavior still holds.

- [ ] **Step 4: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs
git commit -m "refactor: one helper for the percent sliders"
```

---

### Task 8: One shipped preset

This is the RED/GREEN task. The behavior change: the preset row shows
one button, `Safe`.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Presets/PresetFileStore.cs`
- Modify:
  `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetFileStoreTests.cs`
- Modify:
  `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Modify: `README.md`
- Delete: `Packages/com.alrauna.amuse/Presets/normal.json`,
  `Packages/com.alrauna.amuse/Presets/normal.json.meta`,
  `Packages/com.alrauna.amuse/Presets/aggressive.json`,
  `Packages/com.alrauna.amuse/Presets/aggressive.json.meta`

**Interfaces:**
- Produces: `private static readonly string[] FileNames =
  { "safe" };` in `PresetFileStore`.

- [ ] **Step 1: Record the baseline**

Run the `Tests/Editor/Presets` EditMode folder filter on the dev
editor instance. Record the observed case count and that all passed.

- [ ] **Step 2: Rewrite the store test first (RED)**

Replace `TheThreeShippedFilesParseInTheFixedOrder` with:

```csharp
[Test]
public void TheShippedFileParses()
{
    Assert.That(PresetFileStore.TryLoadAll(
        out var presets, out var failedFile, out var refusal),
        Is.True, failedFile + ": " + refusal);
    Assert.That(refusal, Is.EqualTo(PresetLoadRefusal.None));
    Assert.That(presets.Count, Is.EqualTo(1));
    Assert.That(presets[0].Name, Is.EqualTo("Safe"));
}
```

Refresh, run the `Tests/Editor/Presets` folder filter, and observe
`TheShippedFileParses` fail on the count assert, because three files
still ship. Record the observed failure. That is the RED. No other
case may fail.

- [ ] **Step 3: Shrink the store list and delete the files**

Change `FileNames` to `{ "safe" }`. Delete `normal.json`,
`aggressive.json`, and their `.meta` files. Treat each `.json` and
its `.meta` as one unit.

- [ ] **Step 4: Update the doc comment and the README**

In `AmuseAvatarOptimizerEditor`, shrink the preset row doc comment to
"One button per shipped preset file." In `README.md`, replace the
sentence that names Safe, Normal, and Aggressive with:

```markdown
- The preset row under the header picks the shipped Safe preset. It
  holds the shipped defaults.
```

- [ ] **Step 5: Run the guard (GREEN)**

Rerun the `Tests/Editor/Presets` folder filter. All cases pass,
including `TheShippedFileParses` and
`EveryShippedFileHoldsTheShippedDefaults`. Record the observed count.

- [ ] **Step 6: Smoke check the inspector**

On the dev editor instance, open the `AmuseAvatarOptimizer`
inspector. Confirm the preset row draws one `Safe` button, it reads
pressed on the defaults, and pressing it applies the shipped values.

- [ ] **Step 7: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/Presets/PresetFileStore.cs Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetFileStoreTests.cs Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs README.md Packages/com.alrauna.amuse/Presets/
git commit -m "feat: ship one preset until the presets differ"
```

---

### Task 9: Full validation

- [ ] **Step 1: Run the product suite**

Run the full `Alrauna.Amuse.Tests.Editor` assembly in EditMode on the
dev editor instance. Record the observed count and that all passed.

- [ ] **Step 2: Check the diff**

Run `git diff --check`. Confirm the working diff touches only the
allowed files. Confirm `pr.yml` is untouched.

- [ ] **Step 3: Report**

Report the observed counts per filter, the RED and GREEN evidence
from Task 8, the smoke check results from Tasks 5 through 8, and the
net line change against the review estimate of about 215. List any
cut that was not applied, with the reason.
