# Alpha Separator Settings Rows: Control-First Shrinking Design

Date: 2026-10-06. Base: branch `feature/optimizer-ui-rework` at commit
`6d9393c`. The working tree carries one unrelated modified file,
`.github/workflows/pr.yml`. This design does not touch it.

Investigation:
`docs/superpowers/investigations/2026-10-06-slider-dropdown-row-narrowing-investigation.md`.
The investigation owns the source citations. This spec owns the
decision and the change shape.

## 1. Overview

One change to the AMUSE Avatar Optimizer inspector: the six rows under
"Alpha Separator Settings" draw with a scoped label width override.
The label column becomes exactly as wide as the widest of the six
labels. Every pixel the inspector window loses comes out of the
control area, not out of the label text. The label text stays fully
readable at any window width where the row still fits.

The change alters layout only. No label text changes. No tooltip text
changes. No serialized field changes. No build pipeline change.

The owner picked the aligned variant: all six rows share one label
column, sized to the widest label, like the column Unity's fallback
formula draws in a wide window.

## 2. Behavior contract

For each of the six rows, with window width W:

1. The label column is `C = widest label text width + 2` pixels, the
   same for all six rows. On 2026-10-06 that measures 237 + 2 = 239
   pixels. The 2 pixel margin is insurance for the native label paint
   inset, which is not readable in the reference source.
2. The control area is `W - C - 2` pixels. The control gives up width
   first as the window narrows.
3. Slider rows keep track plus number field while the control area is
   105 pixels or more, that is down to W = 346. Below that, Unity's
   own two-stage collapse takes over: a bare 50 pixel number field
   from W = 346 to W = 294, then the row clips at the window edge.
4. Popup rows keep a usable button down to about W = 299 and clip at
   the row layout minimum near W = 294.
5. The label text never truncates inside the fitting range. The
   control is the part that clips at the floor.
6. The label width override is scoped. Every other row in the
   inspector, including the preset row and the Settings rows, draws
   exactly as today.

Unity's fallback formula `max(0.45 x W - 40, 120)` no longer applies
to these six rows. The 120 pixel floor and its truncation disappear
for them.

## 3. Implementation shape

All changes stay in
`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`.
No new file, no new assembly, no meta file churn. The helper types are
private and nested inside `AmuseAvatarOptimizerEditor`.

1. A private static `GUIContent[]` field holds the six row contents,
   label and tooltip, in draw order. The strings move verbatim from
   the six call sites into this one array. The array is built once,
   not per repaint. One source of truth feeds both the draw calls and
   the width measurement, so a label change cannot drift away from
   its measurement.
2. A private static method measures the shared width: the maximum of
   `EditorStyles.label.CalcSize` over the six contents, ceiling
   rounded, plus 2. It measures with the live label style on every
   call, so the editor skin and the editor font size setting are
   picked up automatically. No pixel width is hardcoded.
3. A private readonly struct `SharedLabelWidthScope : IDisposable`
   saves `EditorGUIUtility.labelWidth` in its constructor, sets the
   measured width, and restores the saved value in `Dispose`. The
   `using` form compiles to try and finally, so the process-wide
   static restores even on an exception.
4. `DrawAlphaSeparatorSettings` opens one scope after the foldout
   early return and closes it after the four draw calls. One scope
   covers all six rows.
5. The six draw calls take their `GUIContent` from the static array
   instead of building one per repaint.

`EditorGUIUtility.labelWidth` is process-wide static state. The scope
restore is the load-bearing rule, and the XML doc comment on the
scope struct states why: a missed restore leaks into later rows of
the same GUI pass.

## 4. What stays unchanged

- The foldout header, the preset row, the Settings rows, the header
  block, and the build status box draw exactly as today.
- Tooltips open from the same `GUIContent`. Tooltip windows size
  themselves at hover time and do not read the label column.
- The rows sit at indent level 0, so the label column equals the
  override value exactly. No indent math enters the design.
- Serialization, defaults, Undo, Reset, and the build pipeline are
  untouched. The rows already draw without `PropertyField` services,
  and they keep drawing without them.
- The README needs no change. No user-visible text moves.

## 5. Test strategy

RED/GREEN does not apply. No test draws the inspector, and a layout
change has no failing unit test to observe first. This mirrors the
2026-10-05 checkbox and UI rework records.

Validation:

1. Unity compiles the editor assembly after a refresh.
2. The full `Alrauna.Amuse.Tests.Editor` and
   `Alrauna.Amuse.Research.Tests.Editor` assemblies pass with zero
   failures. Nothing couples to this file's drawing today; the suites
   prove that stays true.
3. An editor smoke check on the dev editor instance, recorded with
   observed counts, at inspector widths near 450, 400, 350, and 300
   pixels:
   - all six labels are fully readable at every checked width;
   - the four slider rows show track plus number field at 450, 400,
     and 350;
   - near 300 the slider rows show the bare number field and the
     labels are still fully readable;
   - the two popups open, show options, and select at every checked
     width;
   - tooltips open on all six rows;
   - a value change survives selecting another object and back;
   - the preset row and the Settings rows look unchanged.
4. `git diff --check` stays clean.

## 6. Risks and answers

- The native label paint inset is not readable in the reference
  source. The 2 pixel margin covers it, and the smoke check confirms
  the drawn text. If a hairline truncation shows, the margin is the
  one constant to raise.
- The builtin skin's clipping mode for the label style is native and
  unreadable in the reference source. The drawn result is the oracle,
  and the smoke check owns it.
- A font size or skin change shifts the measured widths. The design
  measures live per repaint, so no hardcoded number can go stale.
  The numbers in this spec are 2026-10-06 measurements for smoke
  expectations only.

## 7. Out of scope

- The preset row and its fixed 50 pixel label.
- The checkbox rows and the foldout headers.
- The research package inspector tooling.
- Label-above-control stacking. The owner chose the aligned column,
  which keeps text readable and lets the control clip at the floor.
  If a later request wants text readable at every possible width,
  that is the investigation's Option B and a new decision round.
