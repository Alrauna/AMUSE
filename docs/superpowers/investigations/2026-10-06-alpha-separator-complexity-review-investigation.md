# Alpha separator complexity review: branch characterization

Date: 2026-10-06. Branch: `feature/optimizer-ui-rework`, base `main` at
`f319186`, review point `7f7cd73`.

Labels: `[SOURCE]` is a fact read in this repository. `[INFERENCE]` is
a deduction. `[RECOMMENDATION]` is a proposed next step.

## 1. Purpose

The branch owner asked for a parallelized complexity review of all code
changes on the branch. The review used the ponytail-review rules. That
method looks for over-engineering only. Correctness, security, and
performance are out of scope. This note records what the review found.
It changes nothing.

This review is a second pass. A first pass ran on 2026-10-05 at review
point `0891286`. Section 6 states how the two records relate.

## 2. How the review ran

`[SOURCE]` The branch diff against `main` at `f319186` touches 61
files with about 6.6 thousand inserted lines at review point
`7f7cd73`. The review covered the code surface in four read-only
slices: the inspector rework, the presets module, the runtime
component and build wiring, and the tests. One independent reviewer
ran each slice. Metadata files, the lockfile, and the dated records
under `docs/superpowers/` were outside review scope by the method's
terms.

`[SOURCE]` The review point for every citation in this note is
`7f7cd73`.

## 3. Findings

Each finding names one cut and its replacement. The eight findings
together allow about 215 fewer lines with no behavior change. Section
7 records how this total differs from the reviewers' first estimate.

### 3.1 Inspector: a 44-line scope struct pins one label width

`[SOURCE]` `AmuseAvatarOptimizerEditor` holds
`SharedLabelWidthScope`, an `IDisposable` struct with a `Begin`
factory, plus a `SharedLabelWidth` helper and three doc comment
blocks. The struct pins `EditorGUIUtility.labelWidth` to the widest of
the six alpha separator row labels and restores it on dispose. One
call site uses the scope. The pin and the restore are load-bearing
behavior. The scope ceremony is not.

`[RECOMMENDATION]` `DrawAlphaSeparatorSettings` pins and restores the
width inline around the four row-draw methods inside its scope block:

```csharp
var widest = RowContents.Max(
    content => EditorStyles.label.CalcSize(content).x);
var previousWidth = EditorGUIUtility.labelWidth;
EditorGUIUtility.labelWidth = Mathf.Ceil(widest) + 2f;
// ... existing draw calls ...
EditorGUIUtility.labelWidth = previousWidth;
```

The `Ceil` and the 2 pixel padding stay, so the drawn width is
unchanged. The struct, the factory, and the helper go away. The
widest-label line is new and adds the file's `System.Linq` import.
About 40 lines go away.

### 3.2 Inspector: one slider write-back pasted four times

`[SOURCE]` `DrawCoverageSlider` and `DrawAlphaPolicyControls` carry
the same six-line pattern four times: clamp the stored value, draw an
`EditorGUILayout.IntSlider` over 0 to 100, write the result back to
the property. Only the property path and the row content differ.

`[RECOMMENDATION]` One helper absorbs the pattern:

```csharp
private int PercentSlider(string propertyPath, GUIContent content)
```

`DrawCoverageSlider` becomes one helper call. `DrawAlphaPolicyControls`
becomes three helper calls, and `NormalizePolygonClamp` reads the
helper returns. About 15 lines go away.

### 3.3 ToggleLeftDrawer: manual mixed-value state duplicates BeginProperty

`[SOURCE]` `ToggleLeftDrawer.OnGUI` sets
`EditorGUI.showMixedValue` from `property.hasMultipleDifferentValues`
at line 22 and resets it to false at line 25.
`EditorGUI.BeginProperty` already sets the mixed-value mark from the
property. `EditorGUI.EndProperty` already restores it. The Unity 2022.3
scripting reference states that the pair automatically handles
"setting `showMixedValue` to true if the values of the property are
different when multi-object editing". The two manual assignments
repeat platform behavior. The drawer doc comment at lines
11 and 12 describes the manual handling and goes away with it.

`[RECOMMENDATION]` Delete the two assignments and the doc sentence.
`BeginProperty` and `EndProperty` carry the behavior. Four lines go
away.

### 3.4 Preset data: two shipped presets byte-match the third

`[SOURCE]` `normal.json` and `aggressive.json` differ from
`safe.json` only in name and description. Both descriptions state
"The same safe checks for now". The preset row iterates the shipped
files, so the three buttons press identical values.

`[RECOMMENDATION]` Ship `safe.json` only, with three follow-on edits
the cut requires. `PresetFileStore.FileNames` shrinks from the three
literals to `{ "safe" }`, because a missing file fails the whole load
and turns the preset row off. The store test
`TheThreeShippedFilesParseInTheFixedOrder` shrinks to the one shipped
file, because it pins the count three and the order. The editor row
then renders one button from the loaded presets. Add a preset file
the day its values differ from the safe set. About 36 lines go away
with the two files and their metadata files.

### 3.5 Plugin: the feature gate re-fetches a proven component

`[SOURCE]` `AlphaSeparatorSwitchedOff` fetches
`AmuseAvatarOptimizer` from the avatar root and guards a null.
`TriggerActivated` does the same fetch with the same guard, both at
the barrier's call site in the pipeline and in the adjacent
definition below it. After the trigger has run, the component is
present and the root is non-null. The barrier re-does the fetch and
the guard.

`[RECOMMENDATION]` `TriggerActivated` returns the component instead of
a bool. The pipeline holds it, and the barrier collapses to one
property read. The duplicate method and its doc block go away. About
13 lines go away: 16 deleted, 3 added.

### 3.6 Plugin tests: one fixture copied for one property flip

`[SOURCE]` `AlphaSeparatorSwitchedOffDoesNotActivateThePipeline` at
line 280 copies `DisabledComponentDoesNotActivateThePipeline` at line
247. Both build the same fixture, run the same pass, and assert the
same two zero counters. Only the flipped serialized property differs.

`[RECOMMENDATION]` One `[TestCaseSource]` with rows
`("_amuseDisabled", true)` and `("_alphaSeparatorEnabled", false)`
drives the property flip. The message text that names the flipped
control becomes row data. About 25 lines go away.

### 3.7 Integration tests: one long fixture copied for one property flip

`[SOURCE]` `SerializedAlphaSeparatorSwitchKeepsTheBuildUntouched` at
line 851 copies `SerializedDisableControlKeepsTheBuildUntouched` at
line 769 through the whole setup: environment, folder, component,
serialized flip, animation fixture, texture, material, and renderer.
The assert tails have drifted. The alpha test reads
`CreatedClones` with a direct `Is.Null.Or.Empty` assert and uses the
renderer that `CreateSkinnedRenderer` returns. The disable test still
carries the `HashSet` wrapper and the hierarchy sweep that the
2026-10-05 review flagged. The drift is what unpruned duplication
does: one twin absorbed the older fixes, the other did not.

`[RECOMMENDATION]` One shared fixture helper takes the property name,
the value, and out parameters for the material and the renderer. The
setup lines collapse. The assert tails stay per test, and the disable
twin adopts the direct asserts while it is open. About 40 lines go
away.

### 3.8 Component tests: two field pairs repeat one shape

`[SOURCE]` `DefaultAlphaSeparatorEnabledIsTrue` at line 71 and
`AlphaSeparatorEnabledSerializedPropertyCanBeToggled` at line 86
repeat the pair shape of the `IgnoreOutOfRangeMaterialSlots` tests.
`DefaultAdvancedSettingsRevealedIsFalse` at line 234 and
`AdvancedSettingsRevealedSerializedPropertyCanBeToggled` at line 254
repeat it again. Each pair builds a GameObject, reads a default, then
toggles a serialized property and reads it back. Four tests carry the
same skeleton.

`[RECOMMENDATION]` One `[TestCaseSource]` of field name, default
value, and toggled value drives one defaults test and one round-trip
test. The Reset-behavior comment on the advanced settings row moves to
its source row. About 40 lines go away.

## 4. Slices with no findings

`[SOURCE]` The reviewers checked these and recorded no finding:

- The `ToggleLeftAttribute`, the drawer, and the runtime
  `InternalsVisibleTo`. Unity ships no built-in toggle-left attribute.
  All five attributed fields render through `PropertyField`.
- The Newtonsoft JSON dependency. The fail-closed `PresetParser` must
  reject unknown keys. Unity's `JsonUtility` cannot do that. The
  dependency is the minimal strict-JSON option.
- The Runtime assembly definition. It exists on `main`. This branch
  adds no assembly split.
- `_advancedSettingsRevealed`. The inspector foldout reads it. It is
  serialized so Reset and Undo treat it like every other control.
- The uncommitted `pr.yml` change. It is a comment-only rewrite that
  documents why the branch-protection gate must stay.
- `README.md` and `package.json`. Three lines each, and both document
  real new behavior.

## 5. Out-of-scope observations

No behavior question surfaced in this pass. The 2026-10-05 review
deferred one behavior question about the preset row lazy load. That
deferral still stands in its own record.

## 6. Relation to the 2026-10-05 review

`[SOURCE]` The 2026-10-05 review ran at review point `0891286` and
produced eight findings about the preset parser, the preset store, the
inspector header, and their tests. This pass produced eight findings
about the alpha separator feature surface: the inspector rows, the
runtime switch, the drawer, the plugin gate, the preset data files,
and their tests. No cut appears in both records. One target method is
shared: the 2026-10-05 findings 3.7 and 3.8 flag lines inside
`SerializedDisableControlKeepsTheBuildUntouched`, and this pass
section 3.7 names that same test as one half of a copied pair. The
older cuts are still unapplied at review point `7f7cd73`, and they
stack with section 3.7: the shared fixture helper rewrites exactly the
lines the older review wants gone. Each record speaks for its own
date.

## 7. Verification record

`[SOURCE]` On 2026-10-06 the four reviewer reports were checked
against the working tree at `7f7cd73`. The checks did the following:

- Compared the three preset JSON files byte for byte. They match
  except for name and description.
- Read the label-width scope, the slider blocks, and the drawer in
  full.
- Read the plugin gate and the trigger check above it.
- Located all eight named test methods at the cited lines and read
  both copied pairs in full.

Every citation matched the working tree. One reviewer draft lost the
`Ceil` in the label-width replacement. This note restores it, so the
recommendation keeps the drawn width unchanged.

`[SOURCE]` On 2026-10-06 an adversarial pass re-checked this note
against the ponytail findings and the working tree. It confirmed the
BeginProperty behavior against the Unity 2022.3 scripting reference.
It found and fixed six defects:

- The preset cut in section 3.4 was incomplete. Deleting the two
  files alone breaks `TryLoadAll` and two store tests, because
  `FileNames` hardcodes the three names. The replacement now names
  the `FileNames` edit and the store test edit.
- Section 3.7 claimed the copied tests differed only in property and
  name. The assert tails have drifted. The claim now states the
  shared setup and the per-test tails.
- Section 3.5 said the trigger check sat ten lines above. It sits at
  the barrier's call site and in the adjacent definition. The count
  moved from 17 to 13 with the arithmetic stated.
- The line totals were reviewer estimates. This pass recomputed each
  from line counts. The reviewer-reported total of about 269 moved to
  about 215.
- Section 6 claimed no finding appears in both records. One target
  method is shared with the 2026-10-05 review. The claim now states
  the shared method and the stacking cuts.
- Section 3.1 omitted the new `System.Linq` import its replacement
  needs. The replacement now names it.

`[RECOMMENDATION]` The findings stay unapplied. A design note and a
plan note can turn them into cuts, in the same shape as the
2026-10-05 complexity review pair under `specs/` and `plans/`.
