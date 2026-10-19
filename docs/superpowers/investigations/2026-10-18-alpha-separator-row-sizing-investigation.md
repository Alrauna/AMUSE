# Alpha Separator row sizing: label fit and control minimum

Date: 2026-10-18. Branch: `ux/indent-alpha-separator-submenus` on top of `main`. The working tree carries the two submenu indent scopes that this session added on 2026-10-18, uncommitted. The submenus themselves landed in commits `9bf2cb4` and `65015a2` on 2026-10-16. This note changes nothing.

Privacy note: this record names no machine, host, account, port, or instance. The Unity editor used for measurements is named by role, "the dev editor instance".

Labels: `[SOURCE]` is a fact read in this repository or in the public Unity C# reference source on 2026-10-18. `[PROBE]` is a value read from the dev editor instance with a read-only editor script on 2026-10-18. `[MEASURED]` is a value read from owner-provided screenshots on 2026-10-18. `[INFERENCE]` is a deduction made on 2026-10-18.

## 1. Purpose

The owner asked for two sizing changes in the Alpha Separator Settings block. One, every child row label of the Transparent and Cutout submenus must render in full. Two, when the inspector is narrow, the slider value box and the dropdown must stay wide enough to show their text, while the slider track may vanish. The owner excluded structural change: the same foldouts, the same row order, the same widgets. This note records the current draw pipeline, the IMGUI layout rules that govern it, why the text truncates, and the requirement facts. It makes no design choice.

## 2. The rows and the sizing pipeline

`[SOURCE]` All policy and acceptance rows live in `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`. One `RowContents` array of fourteen `GUIContent` entries is the single source of truth for the row labels of this block. The first seven entries feed the Transparent submenu. The next three feed the acceptance rows. The last four feed the Cutout submenu.

`[SOURCE]` Every property row label drawn inside the Alpha Separator Settings block is one of these fourteen constants. Four draw calls are popups. Seven are integer sliders. Two are toggle property fields. One is the proof row. The same inspector draws other labels outside this block from inline content, for example the Settings rows. Those rows sit outside the width scope, so the widest-label measurement does not need them.

`[PROBE]` The widest entry measures 304.0 pixels at the default label style. The two widest entries are the two "Minimum Opaque Coverage to Preserve (Per Material)" labels, the transparent one and the cutout one. The two "to Convert (Per Material)" labels measure 299.0 pixels and rank next.

`[SOURCE]` The indent scope chain: `DrawSettings` opens one scope for the Settings body and one for the Advanced reveal block. Both close before `DrawAlphaSeparatorSettings` runs. Inside `DrawAlphaSeparatorSettings`, scope one opens before the block and scope two wraps the foldout header. The widest measurement runs at indent level 2. Scope three wraps the block body. The acceptance rows draw at level 3. The submenu drawers open a fourth scope for their rows, so the submenu rows draw at level 4.

`[SOURCE]` The width scope also draws content that is not a row:

- The two submenu foldout headers draw at level 3, before their submenu's fourth scope.
- The popup drawers draw conditional HelpBoxes. HelpBoxes ignore the label width and draw full width.
- The proof row is a two-argument label field. Its value string draws in the control area, at `rowRect.x + labelWidth + 2`.
- The two acceptance toggles draw their checkbox in the control area.

A wider label therefore shifts the proof value string and the checkboxes to the right. The record states this consequence here and the design round must weigh it.

`[SOURCE]` The assignment `EditorGUIUtility.labelWidth = AlphaSeparatorLabelWidth(widest);` sits inside the third scope. It saves the previous width, sets the new width, draws the body, and restores the previous width with a plain statement inside the third scope. The restore is not a `finally` block. The 2026-10-06 investigation warned that the label width is process-wide static state and that a missed restore leaks into later rows of the same pass. Any design that extends the assignment inherits that constraint.

`[SOURCE]` The helper and its formula:

```csharp
internal static float AlphaSeparatorLabelWidth(float widestLabelWidth)
{
    return Mathf.Ceil(widestLabelWidth) + 2f
        - EditorGUI.indentLevel * 15f;
}
```

`[PROBE]` The helper evaluates at indent level 3 on 2026-10-18. The label width is `ceil(304) + 2 - 45`. That is 261 pixels.

## 3. IMGUI layout rules for one labeled row

All rules in this section come from the Unity C# reference source, branch `2022.3`, the same version stream as the dev editor instance. The indentation arithmetic was confirmed at runtime on 2026-10-18.

`[SOURCE]` Every labeled row routes through `EditorGUI.PrefixLabel`. It splits the row rect exactly once:

- The control starts at `rowRect.x + labelWidth + 2`. The 2 is `kPrefixPaddingRight`. The divide does not depend on the indent level.
- The label band starts at `rowRect.x + 15 * indentLevel`. Its width is `labelWidth - 15 * indentLevel`. Its right edge stays pinned at the divide.
- The text inside the band starts after the label style's left padding of 1 pixel. The text budget before truncation is therefore `labelWidth - 15 * indentLevel - 1`.

`[PROBE]` `EditorGUI.IndentedRect` shifts x by exactly 15 pixels per indent level at every level from 0 to 4. The per-level inset is queryable at runtime. No hardcoded 15 is required.

`[SOURCE]` The slider control has a two-stage collapse. At a control width of 105 pixels or more it paints a track of `width - 55` plus a right-aligned number field of exactly `fieldWidth`, which defaults to 50 pixels. Below 105 pixels it removes the track entirely and paints only a right-aligned number field of `min(fieldWidth, width)`. There is no width where a partial track shares space with a partial field.

`[SOURCE]` The popup has no floor and no arrow-only branch. It draws into whatever rect it receives. The selected option text clips when the rect is too small.

`[SOURCE]` The layout system refuses to lay a labeled slider row below `labelWidth + fieldWidth + 5`. Below that row width the row overflows the window instead of shrinking further.

## 4. Why the text truncates, and what a full-fit width costs

`[INFERENCE]` With the label width of 261 pixels on 2026-10-18:

- Acceptance rows at level 3 have a text budget of `261 - 45 - 1`, which is 215 pixels.
- Submenu rows at level 4 have a text budget of `261 - 60 - 1`, which is 200 pixels.

The widest label needs 304 pixels. The submenu labels therefore lose about 104 pixels. The owner's screenshots show exactly this cut in both submenus.

`[SOURCE]` The subtraction term is deliberate. The 2026-10-17 indentation design states the helper subtracts the full row indent, 45 pixels at depth 3, to preserve the label-to-value proportion at every depth on that date. The submenu scopes added on 2026-10-18 raised the deepest drawn rows to level 4 while the helper still computes at level 3. The subtraction is the root cause of the truncation.

`[MEASURED]` An intermediate fix of `ceil(widest) + 17` on 2026-10-18 produced 321 pixels and still truncated the same labels. The level-4 budget was `321 - 60 - 1`, which is 260 pixels, about 44 pixels short. The screenshots confirm a cut of about that size.

`[INFERENCE]` A label width that fits the widest label at level 4 needs `304 + 1 + 60 + 2` pixels, about 367. The same arithmetic at the acceptance level needs about 352. The design must size the budget from the deepest drawn level, level 4, because one shared label width serves all rows.

`[SOURCE]` A labeled slider row refuses to lay out below `labelWidth + fieldWidth + 5`. At a label width of 367 that row floor sits near 422 pixels of row width.

`[INFERENCE]` Docked inspector panes commonly sit between 330 and 380 pixels. At such widths, full labels and control minimums cannot both hold.

`[INFERENCE]` This is the central tension. The owner's wide screenshot is 705 pixels and leaves about 330 pixels for the control area at a 367 label width. The owner's narrow screenshot is 373 pixels. At 373 pixels the row at level 4 is about 307 pixels wide after the indent, so a 367 label width cannot fit. Requirement 1, full labels, and requirements 3 and 4, minimum control widths, cannot coexist below roughly 430 pixels of window width. A design must pick the yield order below that width, and section 6 records the owner's statements about that order.

## 5. Control behavior on a narrow inspector

`[MEASURED]` The owner's narrow screenshot is 373 pixels wide. It shows the end state as of 2026-10-18: slider tracks and handles gone, the slider number fields surviving at readable width, the dropdowns collapsed to stubs where the option text is unreadable, and the long labels still truncated.

`[INFERENCE]` The screenshot agrees with the source rules. The slider rows sit below the 105 pixel control threshold, so the track is removed and the field keeps 50 pixels. The popup rows receive too little width and clip their option text, because the popup has no floor.

`[INFERENCE]` The popup is the binding case for the owner's control minimum. A minimum sized to the longest popup option text plus its arrow satisfies it. The slider needs no new mechanism, because its field already survives on its own.

`[INFERENCE]` Divide consistency: within one row type, every divide sits at `rowRect.x + labelWidth + 2`. Across row types, the acceptance divide sits 15 pixels left of the submenu divide, because those rows sit one indent level shallower. That step is part of the preserved structure. The popup box edge sits a few pixels right of the divide, because the popup style's border and padding paint inside the control rect. Those few pixels are native style behavior.

`[SOURCE]` The width is assigned whenever the Alpha Separator Settings foldout is open. Each submenu returns early when closed. The acceptance rows draw whenever the block is open, regardless of the submenu states. A width basis that uses the deepest visible row would therefore change the divide when a submenu folds. A width basis that uses a fixed level 4 keeps the divide stable across foldout states. Both submenu states default open, and no screenshot covers the collapsed state.

## 6. Requirements as stated by the owner

`[SOURCE]` The owner's words across three messages on 2026-10-18, condensed into testable statements:

1. Every child row label of the Transparent and Cutout submenus renders in full at ordinary inspector widths.
2. The label budget grows as a budget increase. Rows do not move, restructure, or relabel.
3. On a narrow inspector the slider value box keeps enough width to show its number.
4. On a narrow inspector the dropdown keeps enough width to show the selected option text, not just the arrow.
5. The slider track has no minimum and may vanish.
6. The divide between text and control is one shared position for all rows.
7. Text takes precedence over the controls when width is insufficient.
8. The menu structure does not change: same foldouts, same nesting, same row order, same widgets. Small adjustments only.

`[INFERENCE]` Statement 1 has no number for "ordinary". Section 4 puts full label fit at a window width of roughly 430 pixels and above. Below that width the owner's statements 3 and 4 describe the accepted end state, and the narrow screenshot shows the owner accepts truncated labels there.

`[INFERENCE]` Statement 7, text precedence, and statements 3 and 4, control minimums, divide the width ladder into regimes. The ladder under width pressure, from wide to narrow, has four stages. One, the label holds its full width while the slider track shrinks. Two, at a control width of 105 pixels the track vanishes while the label still holds its full width. Three, at the row floor of roughly 422 pixels the label budget must yield, so the boxes and the dropdown keep their minimums. Four, the number field and the popup option text shrink last. The yield stage requires a clamp on the label width. Without a clamp, a fixed label width never yields and the row overflows the window instead, which contradicts the owner's narrow-width statements.

## 7. Prior attempts in this session

`[SOURCE]` Two attempts preceded this note. Both are recorded here as history. Neither is part of the working tree.

1. On 2026-10-18 a label width of `ceil(widest) + 17` still truncated the submenu labels. It ignored the 15 pixel per level band inset that section 3 states. Section 4 gives the arithmetic.
2. On 2026-10-18 an explicit two-column row layout fixed the geometry but restructured every row. The owner rejected it. This session then reverted the working tree to the indent-only state.

## 8. Open points for the design round

1. The label width formula at a fixed deepest level of 4, and whether the per-level inset is queried at runtime through `EditorGUI.IndentedRect` or expressed as a depth count.
2. The clamp that caps the label width at `row width at level 4 minus the control minimum`. Section 6 states the yield order that requires it. Section 4 states the floor arithmetic that makes it mandatory at ordinary docked widths.
3. The control minimum for the popup rows: a calibrated constant sized to the longest option text plus arrow, or a runtime measurement of the option strings. Note for this decision: the 304 pixel probe is a default-font value, so a constant does not scale with editor font size, while a runtime measurement does.
4. The test contract. The current helper test pins 202, 157, and 37 and frames its falsifier as the subtract-per-level rule. A rewrite turns that rule into the named wrong implementation and must replace the falsifier. About eight draw tests reach the block through reflection on field names, `RepaintThroughHostWindow` draws at an uncontrolled default window size, and no test draws with submenus collapsed. The design and plan must state which of these move.
5. Supersessions. This change reverses three statements of the 2026-10-17 design: its decision 3 says no floor exists and adds none; its falsifier 3 and helper test pin the subtract-per-level rule; its depth table fixes the block contents at level 3, while the restored submenu scopes draw submenu rows at level 4. The design round must state these supersessions explicitly, because dated records never retro-edit.
6. The restore of the label width is not a `finally` block. Any design that keeps or extends the assignment should weigh the leak-on-exception warning from the 2026-10-06 investigation.
7. The residuals that stay out of scope: the 15 pixel indent step between acceptance rows and submenu rows, the few pixels of native popup style padding, and the wider proof value string clipping at the shifted control position.

## 9. Verification record

`[SOURCE]` On 2026-10-18 the code facts in section 2 were checked against the working tree: the `RowContents` array and its fourteen constants, the scope chain and its levels, the widest measurement line, the helper and its call site, the restore statements, and the two submenu indent scopes.

`[SOURCE]` On 2026-10-18 the IMGUI rules in section 3 were read from the Unity C# reference source, branch `2022.3`, `EditorGUI.cs` and `EditorGUILayout.cs`: `PrefixLabel` for the row split, `DoSlider` for the collapse ladder, `DoPopup` for the no-floor popup, `IndentedRect` for the per-level inset, and the layout minimum for the row floor.

`[PROBE]` On 2026-10-18 a read-only script in the dev editor instance measured the widest label at 304.0 pixels, the next labels at 299.0 pixels, and the per-level inset at 15 pixels, after a project identity check. The script changed no asset and no scene.

`[MEASURED]` The two screenshots came from the owner on 2026-10-18, taken at window widths 705 and 373 pixels.

`[SOURCE]` On 2026-10-18 three parallel adversarial reviews checked this record for technical accuracy against the code and the Unity source, for completeness against the design needs, and for repo document compliance. The accuracy review verified every structural, arithmetic, and source claim and reported two composition corrections, which section 2 incorporates. The compliance and completeness findings are incorporated throughout.
