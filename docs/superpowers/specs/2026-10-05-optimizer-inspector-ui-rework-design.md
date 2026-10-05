# Optimizer Inspector UI Rework Design

Date: 2026-10-05. Base: `main` at `f319186`, branch `feature/optimizer-ui-rework`.

Investigation: `docs/superpowers/investigations/2026-10-05-optimizer-inspector-ui-rework.md`.

## 1. Overview

Five changes to the AMUSE Avatar Optimizer component:

1. The "Advanced Settings" menu becomes "Settings".
2. The "Disable AMUSE" toggle moves to the top of that menu.
3. The "Alpha Separator" menu becomes "Alpha Separator Settings".
4. A new "Alpha Separator" toggle joins the top level. It is on by default. Off stops the alpha separator feature. A later feature adds its own toggle beside it.
5. The "Alpha Separator Settings" menu hides by default. A new "Advanced Settings" toggle inside "Settings", off by default, reveals it.

The rework changes what the inspector shows and adds one feature switch. It changes no classification, no proof rule, and no mutation shape.

## 2. Component state

`AmuseAvatarOptimizer` gains `_alphaSeparatorEnabled`, a serialized `bool` beside `_amuseDisabled`. The default is `true`. The property is `AlphaSeparatorEnabled`.

Positive polarity: the checkbox shows the state the user reads. A negative name such as `_alphaSeparatorDisabled` would invert the drawn control.

Unity fills a missing serialized field from the field initializer. Avatars saved before this change therefore keep today's behavior. No migration code runs.

The XML doc on the field states why the switch exists: the alpha separator is the first AMUSE feature with its own switch. Later features add sibling switches. The master "Disable AMUSE" toggle stays above them as the total kill switch.

## 3. Pipeline gate

The barrier pass gains one silent gate directly after the V1 trigger. A private static helper reads the component and answers true when the switch is off. The barrier then returns.

The gate reproduces the "Disable AMUSE" contract for this feature only. Nothing is analyzed. Nothing is mutated. Nothing is reported. The consent layer sits below the gate, so a switched-off build asks no consent question.

The apply pass needs no change. The barrier leaves `state.Separation` null, and the apply pass already answers `NoMutation` for that state. The window-close pass already handles a build where nothing opened a window, as the "Disable AMUSE" path proves today.

Characterization kept: the structural graph check and the animator bindings capture carry no trigger gate. They run on every VRChat avatar build. A toggle that gates the barrier inherits this asymmetry.

## 4. Inspector

Draw order after the rework:

1. Header, unchanged.
2. Placement help box, unchanged.
3. "Alpha Separator" toggle. Serialized `_alphaSeparatorEnabled`, drawn at top level where the old foldout header sat. Future sibling switches join it there.
4. "Settings" foldout:
   - "Disable AMUSE" toggle at the top, unchanged tooltip.
   - "Ignore Out-of-Range Material Slots" toggle.
   - "Allow Depth Test Change on Moved Triangles" toggle.
   - "Advanced Settings" reveal toggle. Plain editor bool, default false, not serialized.
5. "Alpha Separator Settings" foldout, drawn only while the reveal toggle is on. Contents unchanged: the four policy controls.
6. Last build status box, unchanged.

Decisions:

- The revealed menu sits below "Settings" as a flat sibling. The file draws one level of foldouts today. A nested foldout would break that convention.
- The reveal state is a plain private bool. It resets after every domain reload. That matches "hidden by default" with no persistence cost.
- `_advancedOpen` renames to `_settingsOpen`. The field is private and not serialized, so the rename is free.
- `_alphaSeparatorOpen` keeps its name and still backs the alpha policy foldout.
- The reveal toggle keeps the freed "Advanced Settings" name. The user request names it exactly that.

New tooltips use plain technical English:

- "Alpha Separator": turn it off to skip alpha separation on this avatar. Nothing is analyzed, moved, or reported for the feature. A later feature adds its own switch beside this one.
- "Advanced Settings" reveal: shows the "Alpha Separator Settings" menu below. Most users never need it. The menu hides again after Unity reloads scripts.

## 5. Default behavior

No default behavior changes. The switch ships on. Builds behave exactly as today until a user turns the switch off.

## 6. Test strategy

- RED/GREEN plugin test: mirror `DisabledComponentDoesNotActivateThePipeline`. Set `_alphaSeparatorEnabled` false, process a fixture avatar, assert zero analyzed renderers, zero refused renderers, and a run that never reaches renderer analysis. RED against the missing gate: the reached-analysis flag turns true. GREEN with the gate.
- Characterization tests in the runtime suite: default true, serialized round-trip to false. They pin the field the way `DefaultIgnoreOutOfRangeMaterialSlotsIsTrue` pins its field. Both pass on first run, so the run is recorded as characterization.
- Merged-consumption test: mirror `SerializedDisableControlKeepsTheBuildUntouched` with the new switch. It proves the full NDMF pipeline leaves the avatar untouched and the build succeeds.
- The inspector rework is compile, suite, and an editor smoke check on the dev editor instance, mirroring the 2026-09-10 plan. No test draws the inspector.
- The full EditMode suite passes with zero failures. The research assembly is unaffected but runs too.

## 7. Documentation

The README "Use" section gains one bullet for the "Alpha Separator" toggle beside the "Disable AMUSE" bullet. The "Disable AMUSE" bullet stays correct, because only the control's location changes.

## 8. Follow-up, 2026-10-05: the advanced options gate

The first release placed the Advanced Settings toggle at the bottom of Settings. The follow-up request moves it directly under Disable AMUSE and hides the two tolerance consents behind it. On 2026-10-05 the Settings order became:

1. Disable AMUSE
2. Advanced Settings toggle (default off)
3. Ignore Out-of-Range Material Slots, shown only while Advanced Settings is on
4. Allow Depth Test Change on Moved Triangles, shown only while Advanced Settings is on
5. Alpha Separator Settings, shown only while Advanced Settings is on (unchanged)

Hiding is draw-time only. No serialized field changes, so saved avatars keep their stored consent values, and a hidden consent still applies at build time. The toggle tooltip now names both hidden groups. Validation mirrors section 6: compile, the editor test class, and the structural smoke check. No test draws the inspector.

## 9. Follow-up, 2026-10-05: the reveal toggle stores on the component

The defect: after the engine Reset command, the Advanced Settings toggle stayed checked until the user selected another object and selected the avatar again.

The cause: the reveal flag lived as a plain bool on the editor instance. Reset restores serialized data only, so the flag kept its value. Selecting away destroyed the editor, and selecting back created a fresh editor with the default. The same staleness would have applied to Undo.

The decision: the flag moved to the component as the serialized field `_advancedSettingsRevealed`, default false. Reset and Undo now treat it like every other control, and the inspector reads it from serialized data each repaint. This supersedes the session-only wording in sections 4 and 8: the toggle persists with the component instead of hiding after a script reload, and the tooltip no longer claims otherwise. The two foldout open states stay editor-session state, because they are navigation, not consent, and a Reset need not collapse them.

Validation: RED first. Two field-pin tests failed before the field existed: 2 tests, 2 failed, both on the named pin. After the change the same filter ran 3 tests, 3 passed, 0 failed. A smoke check set the flag true, applied a default-state copy in the manner of Reset, and read it back false; the editor-side field is gone. The engine Reset menu item itself cannot be clicked by tooling, so the menu path stays a manual check.
