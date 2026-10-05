# Inspector rework for per-feature toggles: current-state characterization

Date: 2026-10-05. Branch: `feature/optimizer-ui-rework`, base `main` at
`f319186`.

Labels: `[SOURCE]` is a fact read in this repository. `[INFERENCE]` is a
deduction. `[RECOMMENDATION]` is a proposed next step.

## 1. Purpose

Five UI requests need a design:

1. Rename the "Advanced Settings" menu to "Settings".
2. Move the "Disable AMUSE" toggle to the top of that menu.
3. Rename the "Alpha Separator" menu to "Alpha Separator Settings".
4. Add a new "Alpha Separator" toggle, checked on by default. Off means the
   alpha separator functionality does not run. A later feature will add its
   own toggle beside it.
5. Hide the "Alpha Separator Settings" menu by default. A new "Advanced
   Settings" toggle inside the "Settings" menu, off by default, reveals it.

This note records the current state the design must touch. It changes
nothing.

## 2. The inspector today

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
draws, in order: the header, a placement help box (root object versus
child), the "Disable AMUSE" property field at top level, the "Alpha
Separator" foldout, the "Advanced Settings" foldout, and the last build
status box. `serializedObject.Update()` runs before the "Disable AMUSE"
field. Each foldout method ends with its own
`serializedObject.ApplyModifiedProperties()`.

`[SOURCE]` The "Disable AMUSE" field is the serialized property
`_amuseDisabled` drawn with `EditorGUILayout.PropertyField`. Its tooltip
says: "Treats this component as absent: nothing runs on build, in Play
mode, or anywhere else, and nothing is reported."

`[SOURCE]` The "Alpha Separator" foldout uses `EditorStyles.foldoutHeader`
with the private state field `_alphaSeparatorOpen`. It holds four controls
in draw order: "Smallest Tested Mipmap" (in `DrawMipCapPopup`), "Smallest
Tested Texture" (in `DrawMinTextureSizePopup`), "Minimum Opaque Coverage
(Per Material)" (in `DrawCoverageSlider`), and three alpha-policy sliders
in `DrawAlphaPolicyControls`: "Alpha Upper Clamp (Per Texture)", "Minimum
Opaque Coverage (Per Polygon)", and "Alpha Upper Clamp (Per Polygon)".

`[SOURCE]` The "Advanced Settings" foldout uses `_advancedOpen` the same
way. It holds two toggles: "Ignore Out-of-Range Material Slots"
(`_ignoreOutOfRangeMaterialSlots`) and "Allow Depth Test Change on Moved
Triangles" (`_allowDepthTestChange`).

`[SOURCE]` `_advancedOpen` and `_alphaSeparatorOpen` are plain private
`bool` fields with no `[SerializeField]`. Both start false. A custom
editor instance does not survive a domain reload, so every reload starts
with both foldouts collapsed. Neither state is persisted anywhere.

`[INFERENCE]` A reveal toggle for the "Alpha Separator Settings" menu can
use the same non-persisted private-`bool` pattern. It then resets to off
after every domain reload, which matches "hidden by default". A persisted
alternative would need component state or `SessionState`, both heavier
than the existing convention.

`[SOURCE]` The only automated editor test for this inspector is
`PolygonClampNormalizationKeepsTheInertSentinel` in
`Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`.
It tests the pure static `NormalizePolygonClamp` method. No test draws
the inspector. The 2026-09-10
settings plan validated foldout layout with an editor smoke check on the
dev editor instance instead.

## 3. The component state today

`[SOURCE]`
`Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs` stores
`_amuseDisabled` as a plain serialized `bool`, default false, exposed as
`AmuseDisabled`. The seven alpha-policy fields beside it carry defaults:
mip cap 4, minimum texture size 128, coverage 25, and the three alpha
bounds at their inert 100. `_allowDepthTestChange` defaults true and
`_ignoreOutOfRangeMaterialSlots` defaults true. The component lives in the
runtime assembly `Alrauna.Amuse.Runtime`, separate from the editor
assembly.

`[SOURCE]`
`Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`
pins some defaults and round-trips as separate tests, for example
`DefaultIgnoreOutOfRangeMaterialSlotsIsTrue`. The three alpha bounds share
one combined test, `AlphaPolicyFieldsRoundTripAndDefaultInert`. No test in
this file pins the defaults of `_amuseDisabled`,
`_preserveTransparencyMaxMipLevel`, or `_allowDepthTestChange`.

`[INFERENCE]` A per-feature toggle belongs beside `_amuseDisabled` with a
positive-polarity name such as `_alphaSeparatorEnabled`, default true,
because the requested toggle reads "checked on by default" and later
features add sibling toggles of the same shape. A negative-polarity
`_alphaSeparatorDisabled` would invert the checkbox the user sees.

`[INFERENCE]` No serialized field on this component carries UI-only state
today. The component serializes build policy only. The reveal toggle and
foldout states should therefore stay in the editor, not on the component.

## 4. How the build consumes the disable switch

`[SOURCE]` `TriggerActivated` in
`Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` is
the only consumer of `AmuseDisabled`. It returns true only when the avatar
root carries the component and the toggle is off. The barrier pass returns
silently when it returns false: no analysis, no mutation, no report.

`[SOURCE]` Everything in the barrier below the trigger gate is alpha
separation work: the consent layer, the transient-unlock window
eligibility and swap-in, and the per-renderer analysis loop.
`state.ReachedRendererAnalysis` becomes true only after every avatar-scope
gate passes, immediately before the renderer loop.

`[SOURCE]` The apply pass runs on every build. With a silent barrier
return, `state.Separation` stays null, `PrepareSurvivingSet` answers
`NoMutation`, and the summary line is suppressed because
`state.ReachedRendererAnalysis` is false. A gated barrier therefore needs
no matching change in the apply pass.

`[INFERENCE]` A gate that reads the new toggle right after
`TriggerActivated` reproduces the "Disable AMUSE" behavior exactly for the
alpha separator: nothing analyzed, nothing mutated, nothing reported. The
component still counts as present, so a later feature can run its own
passes under the same trigger.

`[SOURCE]` Characterization, not a target for this work: the
"AMUSE structural graph check" and "AMUSE animator bindings capture"
passes carry no trigger gate of their own. They run for every VRChat
avatar build, including one with "Disable AMUSE" checked, and the
structural pass can still report an avatar-scope animation refusal on
such a build. Any new toggle that gates only the barrier inherits this
existing asymmetry.

## 5. Constraints the design must keep

`[SOURCE]` The repo rules require plain technical English, ASD-STE100
style, for every new label and tooltip, and XML doc comments that state
why, not what, on load-bearing fields.

`[SOURCE]` `README.md` names the "Disable AMUSE" behavior in the 0.1.0
notes. Docs that name the old menus must move with the rename.

`[SOURCE]` The release workflow zips the package and hard-excludes
`Tests/`, so test files ride only in the repository.

`[SOURCE]` RED/GREEN is the required evidence shape for a behavior change:
a failing test observed first against a named plausible wrong
implementation. The plugin test `DisabledComponentDoesNotActivateThePipeline`
in `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`
is the pattern for the new gate test: set the serialized field, process a
fixture avatar, assert zero analyzed renderers and zero refusals.

`[RECOMMENDATION]` Store the switch as `_alphaSeparatorEnabled`, default
true, beside `_amuseDisabled`, with property `AlphaSeparatorEnabled`. Gate
the barrier immediately after `TriggerActivated`. Keep the reveal toggle
and both foldout states as private editor fields. No report fires when
the toggle is off; the toggle's own tooltip carries the explanation, the
same contract "Disable AMUSE" uses.

## 6. Rename blast radius

`[SOURCE]` One product file draws the current labels. The inspector file
holds "Disable AMUSE" at line 45, "Alpha Separator" at line 69, and
"Advanced Settings" at line 311.

`[SOURCE]` `README.md` names "Disable AMUSE" once, at line 23, in the
"Use" notes for 0.1.0. This is the only product document that must move
with the rename.

`[SOURCE]` Two tests touch `_amuseDisabled` as a serialized field.
`AaoMergedConsumptionTests.cs` pins the field name at line 783.
`DisabledComponentDoesNotActivateThePipeline` sets the field at line 260.
No test asserts a label or a layout.

`[INFERENCE]` A new `_alphaSeparatorEnabled` field may need the same
merged-consumption pin that `_amuseDisabled` has.

`[SOURCE]` Older records under `docs/superpowers/` name the old menus in
prose. The workflow README declares them dated history. History stays as
written. The rename does not edit them.

`[SOURCE]` The repository has no change log file. No GitHub workflow names
these labels.

## 7. Verification record

`[SOURCE]` On 2026-10-05 three parallel read-only scouts and one direct
read checked this note against the working tree. All claims held, with
three corrections, now applied:

- "Smallest Tested Mipmap" is drawn by `DrawMipCapPopup`. The first draft
  left the method unnamed. `DrawAlphaPolicyControls` draws three sliders,
  not two.
- The inspector test is `PolygonClampNormalizationKeepsTheInertSentinel`.
  `NormalizePolygonClamp` is the production method it tests, not a test.
- The runtime test file does not pin every default separately. The three
  alpha bounds share one combined test. `_amuseDisabled`,
  `_preserveTransparencyMaxMipLevel`, and `_allowDepthTestChange` have no
  default tests there.
