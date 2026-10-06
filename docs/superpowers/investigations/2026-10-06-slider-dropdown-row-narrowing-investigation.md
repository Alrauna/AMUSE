# Slider and dropdown rows: narrowing behavior and shrink-the-control feasibility

Date: 2026-10-06. Branch: `feature/optimizer-ui-rework` at commit `6d9393c`.
The working tree carries one unrelated modified file,
`.github/workflows/pr.yml`. This note does not touch it.

Labels: `[SOURCE]` is a fact read in this repository or in a public
primary source. `[MEASURED]` is a value read from the dev editor
instance with a read-only editor script on 2026-10-06. `[INFERENCE]`
is a deduction. `[RECOMMENDATION]` is a proposed next step.

## 1. Purpose

The owner asked for another UI round: as the inspector window gets
narrower, the slider and dropdown controls should give up width before
the label text does. Then the text stays readable at small window
sizes. This note records how the six rows under "Alpha Separator
Settings" work on 2026-10-06, why the text truncates first today, and
whether the requested behavior is possible. It changes nothing.

Scope: the six rows under "Alpha Separator Settings". The checkbox
rows, the preset row, and the foldout headers keep their own behavior.
The checkbox round of 2026-10-05 covers those.

## 2. The six rows now

`[SOURCE]`
`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
draws the "Alpha Separator Settings" foldout in
`DrawAlphaSeparatorSettings` (line 126). It calls four draw methods in
a fixed order. Each row is one plain `EditorGUILayout` call with a
`GUIContent` that carries the label and the tooltip:

1. "Smallest Tested Mipmap", `EditorGUILayout.Popup` (line 158, in
   `DrawMipCapPopup`, line 143).
2. "Smallest Tested Texture", `EditorGUILayout.Popup` (line 219, in
   `DrawMinTextureSizePopup`, line 192).
3. "Minimum Opaque Coverage (Per Material)", `EditorGUILayout.IntSlider`
   (line 248, in `DrawCoverageSlider`, line 244).
4. "Alpha Upper Clamp (Per Texture)", `EditorGUILayout.IntSlider`
   (line 270, in `DrawAlphaPolicyControls`, line 266).
5. "Minimum Opaque Coverage (Per Polygon)", `EditorGUILayout.IntSlider`
   (line 293).
6. "Alpha Upper Clamp (Per Polygon)", `EditorGUILayout.IntSlider`
   (line 315).

`[SOURCE]` The rows sit at indent level 0. The file calls no
`IndentLevel` method.

`[SOURCE]` These six calls are plain layout calls, not
`PropertyField`. They get no prefab override menu, no mixed-value
mark, and no override highlight. Any fix keeps or changes that
consciously.

## 3. How Unity sizes a labeled row in 2022.3

`[SOURCE]` The public Unity C# reference source explains the layout.
Citations pin branch `2022.3` at commit `a322ce5` of
Unity-Technologies/UnityCsReference. That commit equals tag
`2022.3.76f1`. The dev editor instance runs Unity 2022.3.22f1.
`[INFERENCE]` The layout semantics are stable across the 2022.3
patch stream. The smoke check at implementation time can confirm the
pin.

`[SOURCE]` Both row types reserve a label column first.
`EditorGUILayout.Popup` gets a control rect and forwards to
`EditorGUI.Popup` (`Editor/Mono/EditorGUILayout.cs`, lines 900 to
927). `EditorGUI.Popup` calls `PrefixLabel` and paints the popup
button in the rect that remains (`Editor/Mono/EditorGUI.cs`, lines
3662 to 3668). `EditorGUILayout.IntSlider` forwards to
`EditorGUI.IntSlider` the same way (lines 830 to 834), and
`DoSlider` paints track and number field in the remaining rect
(`Editor/Mono/EditorGUI.cs`, lines 3459 to 3603).

`[SOURCE]` The label column width comes from the
`EditorGUIUtility.labelWidth` getter (`Editor/Mono/EditorGUIUtility.cs`,
lines 1406 to 1419):

```csharp
if (s_LabelWidth > 0)
    return s_LabelWidth;

if (hierarchyMode)
    return Mathf.Max(contextWidth * EditorGUI.kLabelWidthRatio - EditorGUI.kLabelWidthMargin, EditorGUI.kMinLabelWidth);
return 150;
```

`[SOURCE]` The constants are `kLabelWidthRatio = 0.45`,
`kLabelWidthMargin = 40`, `kMinLabelWidth = 120`,
`kSpacing = 5`, `kSliderMinW = 50`, `kPrefixPaddingRight = 2`
(`Editor/Mono/EditorGUI.cs`, lines 101 to 116).

`[SOURCE]` Nothing sets a label width for the inspector. The IMGUI
container resets `s_LabelWidth` to 0 on every GUI pass
(`EditorGUIUtility.ResetGUIState`, lines 1258 to 1276), sets
`hierarchyMode = true` (`UIElements/Inspector/InspectorElement.cs`,
lines 677 to 689), and sets `contextWidth` to the inspector width
(lines 803 to 807). `Editor.DrawHeader` also zeroes the statics after
the header (`Editor/Mono/Inspector/Editor.cs`, lines 941 to 950). So
inside the inspector body the fallback formula applies:

- label column = `max(0.45 x windowWidth - 40, 120)`
- control area = `windowWidth - label column - 2`

`[SOURCE]` `PrefixLabel` draws the label inside the label column and
returns the rest to the control (`Editor/Mono/EditorGUI.cs`, lines
6659 to 6672). The label paints with `EditorStyles.label`, the skin
style "ControlLabel" (line 526). The skin data is native, so its
clipping mode is not readable in the reference source. There is no
ellipsis helper in managed code.

`[SOURCE]` The layout system never lays the row out below
`labelWidth + fieldWidth + kSpacing` (`EditorGUILayout.cs`, lines 19
to 21 and 2106 to 2125). `fieldWidth` falls back to 50
(`EditorGUIUtility.cs`, lines 1421 to 1431).

`[MEASURED]` In the dev editor instance, the six label texts measure
these widths with `EditorStyles.label.CalcSize` and
`CalcMinMaxWidth`. Minimum equals maximum for all six, so no label
can wrap or shrink on its own:

| Label | Text width in pixels |
|---|---|
| Smallest Tested Texture | 138 |
| Smallest Tested Mipmap | 140 |
| Alpha Upper Clamp (Per Texture) | 186 |
| Alpha Upper Clamp (Per Polygon) | 189 |
| Minimum Opaque Coverage (Per Material) | 237 |
| Minimum Opaque Coverage (Per Polygon) | 237 |

`[MEASURED]` The popup style paints "Mip 10" at 58 pixels including
the arrow. `[MEASURED]` The `labelWidth` and `fieldWidth` statics read
100 and 50 outside a GUI pass. Those statics reflect the last finished
GUI pass, not the inspector body. The inspector body value comes from
the formula above.

## 4. Why the text truncates first

`[INFERENCE]` The label column grows only as 45 percent of the window
grows. A label is fully readable from this window width upward
(`width >= (textWidth + 40) / 0.45`):

| Label | Fully readable from |
|---|---|
| Smallest Tested Texture | 396 pixels |
| Smallest Tested Mipmap | 400 pixels |
| Alpha Upper Clamp (Per Texture) | 503 pixels |
| Alpha Upper Clamp (Per Polygon) | 509 pixels |
| Minimum Opaque Coverage (Per Material) | 616 pixels |
| Minimum Opaque Coverage (Per Polygon) | 616 pixels |

`[INFERENCE]` At the window widths an inspector usually has, 300 to
450 pixels, the four slider labels truncate hard and the two popup
labels truncate mildly. The control area at the same widths is about
`0.55 x windowWidth + 36`, so 201 pixels at 300 and 283 pixels at
450. The control never gives up width in that range. This matches the
owner's report: the text gives up space first, the control keeps a
large fixed share.

`[SOURCE]` The slider collapses in two stages. `DoSlider` needs
`kSliderMinW + kSpacing + fieldWidth`, that is 105 pixels of control
area, to paint a track plus a number field. Below 105 pixels it paints
only a right-aligned number field of `min(fieldWidth, width)`
(`Editor/Mono/EditorGUI.cs`, lines 3459 to 3462 and 3578 to 3594).
The popup has no such floor; the button paints at any width.

`[INFERENCE]` The full collapse ladder for one row, with the fallback
formula, is:

1. Window shrinks from a wide value: label truncates first, control
   area stays `windowWidth - label column - 2`.
2. Window at 356 pixels or below: label column pins at 120 pixels,
   control area keeps `windowWidth - 122`.
3. Slider rows at a control area below 105 pixels, window near 227:
   the track vanishes, a bare number field remains.
4. Window near 175 pixels and below: the row layout minimum
   `120 + 50 + 5` overflows the window, and the row clips at the
   window edge.

## 5. Can the controls shrink first? Yes

`[RECOMMENDATION]` Option C: scoped label width override. Set
`EditorGUIUtility.labelWidth` to the measured text width before each
of the six calls, and restore it after, with a try/finally or a small
scope type. The getter returns the override as-is, so the label
column becomes exactly the text width and the control area becomes
`windowWidth - textWidth - 2`. The control then shrinks one-for-one
with the window while the text never truncates.

`[INFERENCE]` Option C numbers, longest label 237 pixels: the slider
row keeps track plus number field down to a 344 pixel window, then a
bare number field down to a 294 pixel window, and only the control
clips below that. The popup rows keep a usable button down to about
300 pixels. The short labels 138 and 140 free about 100 extra pixels
for their controls compared with the current formula at 400 pixels.

`[INFERENCE]` Option C has one UX decision. Measured widths per row
give a ragged control column: 138 against 237. One shared width of
237 for all six rows keeps the control column aligned as today and
still keeps every label readable; it wastes up to 99 pixels for the
short labels. Both are one-line variants of the same helper. The
design round picks one.

`[RECOMMENDATION]` Option B: manual row layout. Take the row rect
from `EditorGUILayout.GetControlRect`, split it by the measured text
width, and stack the label above the control when the window is
narrower than `textWidth + control minimum`. Label-less
`EditorGUI.Popup` and `EditorGUI.IntSlider` overloads paint in a given
rect, and `GUIContent` tooltips survive a plain `GUI.Label`. Text
never truncates at any width. Cost: a custom row helper with two
layout modes, roughly 60 to 80 lines, used by six call sites. This is
the shape Unity itself uses for its narrow mode
(`MultiFieldPrefixLabel`, `Editor/Mono/EditorGUI.cs`, lines 6693 to
6719), but that mode does not reach plain Popup and IntSlider calls
(see below).

`[SOURCE]` Unity's narrow mode does not apply here. `wideMode` is
read only in the `PropertyField` paths (`MultiFieldPrefixLabel` and
the vector field height logic). The popup and slider paths contain no
`wideMode` or `contextWidth` reads (`Editor/Mono/EditorGUI.cs`, lines
3384 to 3603 and 3918 to 3979). So no built-in switch stacks these
six rows.

`[SOURCE]` A `PropertyAttribute` plus `PropertyDrawer` route exists
in principle, as the checkbox round used for toggles. It adds runtime
attributes and a drawer, and the label width problem stays identical
inside the drawer. `[INFERENCE]` It buys nothing for this layout
problem over Option C and touches more assemblies. Not recommended.

`[INFERENCE]` Option C is the smallest complete change: one helper
type, six call sites wrapped, no other file. Option B is the only
shape where text never truncates at any window width. The owner's
ask, "controls collapse in more than the text", holds under both.
The design round picks the readability floor.

## 6. Blast radius

`[SOURCE]` The change would touch only
`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` and
possibly one new helper file. File name equals type name, one public
type per file. No serialization change, no label or tooltip wording
change, no build code change.

`[SOURCE]` The tooltip of each row travels in the same `GUIContent`
under every option. Tooltip windows size themselves at hover time and
do not use the label column.

`[SOURCE]` `EditorGUIUtility.labelWidth` is process-wide static state.
A missed restore leaks into later rows in the same GUI pass. The next
pass resets it, and the header resets it, but the helper must restore
in a finally block.

`[SOURCE]` The only automated editor test for this inspector pins the
pure `NormalizePolygonClamp` method. No test draws the inspector. A
layout change has no failing unit test to observe first, so RED/GREEN
does not apply. This mirrors the 2026-10-05 checkbox note.

## 7. Validation shape

`[INFERENCE]` Established practice validates inspector layout with an
editor smoke check on the dev editor instance. The smoke check list
for this change, at inspector widths near 450, 400, 350, and 300
pixels: all six labels stay fully readable, the four sliders keep
track plus number field down to the expected width, the two popups
still open and select, tooltips still open, the foldout still opens
and closes, and a value change survives an inspector focus change.

`[MEASURED]` The measurement script for this note ran read-only in
the dev editor instance on 2026-10-06. It read `Application.dataPath`,
confirmed the project match, and called only style measurement
methods. It changed no asset and no scene.

## 8. Open points for the design round

`[RECOMMENDATION]` Pick Option C or Option B, and pick the ragged or
the aligned label column under Option C.

`[INFERENCE]` The builtin skin's clipping mode for the label style is
native and not readable in the reference source. The observed ellipsis
is native behavior. The smoke check covers the paint result, so no
source gap blocks the design.

`[INFERENCE]` The preset row draws its own fixed 50 pixel label and
stays out of scope. If the owner wants the same behavior there, that
is a separate row type with its own decision.

## 9. Verification record

`[SOURCE]` On 2026-10-06 this note was checked against the working
tree at commit `6d9393c`: the six row call sites, the foldout, the
indent level, and the test file claim. All claims held.

`[SOURCE]` The Unity mechanism claims were read twice: first by a
read-only research agent, then re-read in place at the pinned commit
for the label width getter, the layout constants, and both slider
branches. The quoted code matched.

`[MEASURED]` All editor measurements ran in the dev editor instance
on 2026-10-06, Unity 2022.3.22f1, with the project identity check
before the script.
