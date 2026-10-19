# Alpha Separator row sizing design

Date: 2026-10-18. Base: the working tree of branch `ux/indent-alpha-separator-submenus`, which carries the 2026-10-18 uncommitted submenu indent scopes on top of `main` at `03d46fe`. The investigation record `docs/superpowers/investigations/2026-10-18-alpha-separator-row-sizing-investigation.md` is the fact base for this spec.

Revision note: incorporates the five-lens adversarial review of 2026-10-18 (accuracy against the investigation, safety and security, architectural purity, ponytail adherence, latent bugs). The review corrected the full-fit width from roughly 430 to roughly 500 pixels, moved the control floor from 0 to 1 pixel because Unity treats a non-positive label width as unset, moved the yield onset from the row floor to the popup minimum clamp, fixed the supersession citations, renumbered the falsifier, and reordered the doc comments. One fact-base correction lands here: the investigation dates the submenu commits 2026-10-16, and git records both author and committer dates as 2026-10-17 for `9bf2cb4` and `65015a2`. The later date is correct.

## Overview

The Alpha Separator Settings block keeps its exact structure. One helper changes its rule so the label budget always covers the widest label text at the deepest drawn row, and so the label yields on a narrow inspector while the slider box and the dropdown keep a text-sized minimum. The slider track has no minimum and vanishes on a narrow inspector, as it does today.

The change touches two files: the editor and its test class. It adds no type, no file, no serialized field, and no build-path code.

## Requirements

The investigation states them as eight numbered owner statements. This design binds to those statements as written. The two that shape every decision below are statement 1, full labels at ordinary widths, and statements 3 and 4, box and dropdown minimums on narrow widths.

The design resolves the tension between them with one number: the text term needs a label width of 366 pixels, the control budget starts reserving 74 pixels of popup minimum and padding at once, so the crossover sits at a view width of about 500 pixels. Full labels hold from about 500 pixels upward. Below that, the label yields and the boxes hold. The owner's narrow screenshot of 373 pixels accepts that end state. Docked panes commonly sit below the crossover, so the yield path is the ordinary path at docked widths, not an edge case.

## Decisions

The investigation's section 8 lists seven open points. This spec closes them.

1. **Formula.** The helper returns the smaller of two terms: a text term and a control budget. The text term is `ceil(widestLabelWidth) + 2 + deepestRowIndent`. The helper derives the budget from the deepest row width: `deepestRowWidth - AlphaPopupMinimum - 2`, floored at 1 pixel. The text term wins whenever the row can afford it, which is statement 1 and statement 7. The budget caps the term when the row cannot, which is statements 3 and 4. Both `2` literals are Unity's `kPrefixPaddingRight`: one pads the label text budget, one shifts the control start past the divide. The helper owns the whole yield arithmetic, so no call site repeats it.
2. **Deepest row indent at runtime.** The call site queries `EditorGUI.IndentedRect` at indent level 4, one level past its own level 3, by bumping the indent around the query. The investigation verified that `IndentedRect` shifts by exactly 15 pixels per level at every level from 0 to 4. The query reads the editor's live per-level constant, so it survives any future change of that constant, where a hardcoded 15 would drift.
3. **Fixed level-4 basis.** The formula always sizes for level 4, the submenu rows, even when both submenus are closed. The acceptance rows then draw at level 3 with a budget sized for the deeper level, which only makes them safer. A deepest-visible-row basis would move the divide when a submenu folds; statement 6, one shared divide position, and statement 2, rows do not move, both oppose that visible shift.
4. **Control minimum, constant.** The popup rows are the binding case. One constant, `AlphaPopupMinimum = 72f`, sized to the longest option text plus the arrow at the default label font: the mip options reach "Mip 10", the size options include "All Sizes", and the value fields show at most four digits such as "8192". The slider needs no minimum of its own, because its field keeps 50 pixels on its own below the 105 pixel collapse threshold. Known ceiling: the constant does not scale with editor font size, while a runtime measurement would. If an owner font makes "All Sizes" clip, the upgrade path is to measure the longest option string at draw time with `CalcSize` and widen the constant. The constant carries a `ponytail:` comment naming that ceiling.
5. **Clamp arithmetic.** The helper derives the budget from the row width, so the four-stage ladder lands exactly: label holds while the track shrinks; track vanishes at a 105 pixel control width while the label still holds; label yields when the control area reaches the 72 pixel popup minimum, at a row width near 440 pixels; the number field and the popup option text shrink last. The 422 pixel layout floor of the investigation never binds under this clamp, because the label starts shrinking before the row reaches it. Below the crossover of about 500 pixels of view width, the label truncates while the boxes hold their minimums, which is the end state the owner's narrow screenshot accepts.
6. **Test contract.** The helper test moves from the subtract-per-level rule to the new two-term rule. The subtract rule becomes the named wrong implementation of the new falsifier, because it produced the truncation. The falsifier also names two further wrong implementations: one that ignores the budget, because it starves the controls at narrow widths, and one that drops the 1 pixel floor, because a non-positive label width resets Unity's override to the default column and starves the controls anyway. The draw tests, the reflection-coupled tests, and the host-window repaint suite stay untouched and serve as the smoke validation, per the 2026-10-05 and 2026-10-17 precedents for pure layout work.
7. **Supersessions.** This spec supersedes these statements of the 2026-10-17 indentation design, by name: decision 1's depth enumeration, decision 3 ("no floor is added"), Inspector changes item 3, invariant 8 ("Depths are exactly: ... Alpha Separator Settings header 2 and contents 3"), falsifier 3 and the helper test that pins the subtract-per-level rule, and the out-of-scope bullet that forbids a fourth depth level. The restored submenu scopes draw submenu rows at level 4, and this design adopts that depth as the basis. Dated records are never retro-edited, so the supersession lives here.
8. **Restore discipline.** The label width restore inside the third scope stays a plain statement, not a `finally` block. That is today's shape, this change does not extend the assignment, and the investigation records the pre-existing leak-on-exception risk as unchanged. Adding a `finally` would be an unrelated repair outside this change.

## The exact change

`AmuseAvatarOptimizerEditor.cs`. First the helper, with its doc comment bound to it:

```csharp
/// <summary>
/// The label width for one Alpha Separator Settings row. The widest
/// label text plus the deepest row indent sets the text term, so the
/// longest label never truncates while the row affords it. The row
/// width minus the popup minimum caps that term, so the slider box
/// and the dropdown keep their minimum on a narrow inspector, and a
/// 1 pixel floor keeps the cap honored, because a non-positive label
/// width resets Unity's override. (Row sizing design, 2026-10-18,
/// decisions 1 and 5.)
/// </summary>
internal static float AlphaSeparatorLabelWidth(
    float widestLabelWidth, float deepestRowIndent, float deepestRowWidth)
{
    var controlBudget = Mathf.Max(
        deepestRowWidth - AlphaPopupMinimum - 2f, 1f);
    // ponytail: the budget reads the row width from the view width and
    // leaves the few pixels of view inset unmodeled, so the popup floor
    // lands a few pixels under 72 at docked widths. Measure the level-4
    // row rect if that gap ever matters.
    return Mathf.Min(
        Mathf.Ceil(widestLabelWidth) + 2f + deepestRowIndent,
        controlBudget);
}
```

Then the constant, with its own comment and no doc summary, since the constant carries the `ponytail:` ceiling note:

```csharp
// ponytail: AlphaPopupMinimum is sized for "All Sizes" plus the arrow
// at the default font. Measure the option strings at draw time when a
// larger editor font clips it.
internal const float AlphaPopupMinimum = 72f;
```

The call site in `DrawAlphaSeparatorSettings`, inside the third scope, replaces the current assignment:

```csharp
EditorGUI.indentLevel++;
var deepestRowIndent = EditorGUI.IndentedRect(new Rect(0f, 0f, 0f, 0f)).x;
var deepestRowWidth = EditorGUIUtility.currentViewWidth - deepestRowIndent;
EditorGUI.indentLevel--;
EditorGUIUtility.labelWidth = AlphaSeparatorLabelWidth(
    widest, deepestRowIndent, deepestRowWidth);
```

The save and restore lines around the assignment stay exactly as they are. Nothing else in the method, the submenus, or the acceptance drawer changes.

## Component and build impact

None. The runtime component, the presets, the parser, the build passes, the host capture, the semantics, the analysis, and the reports have no dependency on the editor's label width. The change draws nothing outside the inspector and writes no serialized state.

## Invariants

1. The block's structure is byte-identical: same foldouts, same nesting, same row order, same widgets, same tooltips. The only visible changes are the label column's width and the control positions that follow it.
2. At a view width of about 500 pixels or more, every child row label of both submenus draws in full. Below that width the label column yields, while the slider box keeps 50 pixels and the dropdown keeps up to 72 pixels.
3. The label width never drops below 1 pixel, so Unity's override stays honored at every width.
4. The slider track has no minimum. It vanishes below a 105 pixel control width, exactly as today.
5. Within one row type, every row splits at one divide position. The divide sits 15 pixels left on acceptance rows than on submenu rows, which is the preserved indent structure.
6. The foldout states of the two submenus never move the divide, because the basis is a fixed level 4.
7. Every `EditorGUI.indentLevel` bump restores on the same statement path, including around the new `IndentedRect` query. No throwing call sits between the bump and the restore.
8. No existing test is weakened or skipped. The one replaced helper test asserts the new rule and names the old rule as its wrong implementation.

## Test strategy

EditMode, Unity Test Runner, after an asset refresh. A filtered run that reports zero tests is a failure. The helper change observes RED first, against a named wrong implementation, then GREEN with the exact code above. An assertion that passes on first run is recorded as characterization.

`AmuseAvatarOptimizerEditorTests.cs`: replace `AlphaSeparatorLabelWidthSubtractsTheRowIndent` with:

```csharp
/// <summary>
/// --- Falsifier 1: an implementation that keeps the subtract rule,
/// `ceil(widest) + 2 - indentLevel * 15`. It produced the truncation
/// the owner reported, and it ignores the budget and the floor, so
/// every assertion fails against it. An implementation that ignores
/// the budget returns the text term everywhere and fails the last
/// three. An implementation that drops the 1 pixel floor fails the
/// third and fourth assertions with a zero or negative result. ---
/// </summary>
[Test]
public void AlphaSeparatorLabelWidthBalancesTextAndControlBudget()
{
    Assert.That(
        Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
            .AlphaSeparatorLabelWidth(200f, 60f, 500f),
        Is.EqualTo(262f),
        "a roomy row must fit the widest label plus the indent");
    Assert.That(
        Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
            .AlphaSeparatorLabelWidth(200f, 60f, 200f),
        Is.EqualTo(126f),
        "a narrow row must cap the label at the control budget");
    Assert.That(
        Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
            .AlphaSeparatorLabelWidth(200f, 60f, 0f),
        Is.EqualTo(1f),
        "a starved row must keep the floor, so the width stays valid");
    // No-op guard: a negative budget must also floor, so a rewrite
    // cannot silently drop the floor term.
    Assert.That(
        Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
            .AlphaSeparatorLabelWidth(200f, 60f, -50f),
        Is.EqualTo(1f),
        "a negative budget must never produce an invalid width");
}
```

The pinned values, computed against the exact code: budget `500 - 72 - 2` = 426 beats the text term `ceil(200) + 2 + 60` = 262, so 262 wins. Budget `200 - 74` = 126 caps the label at 126. Budgets 0 and -50 floor at 1. The old subtract rule runs at the ambient level 0 in a plain test and returns 202 for all four calls, so every assertion fails against it. An implementation that ignores the budget returns 262 for all four. An implementation that floors at 0 instead of 1 fails the third and fourth assertions.

After the helper test passes: run the full `Alrauna.Amuse.Tests.Editor` group and record counts. Then the owner performs a visual check at three inspector widths, roughly 700, 505, and 373 pixels, against the acceptance criteria. The 505 pixel check must show full labels, because it sits above the crossover. The 373 pixel check must show full option text in the dropdowns, full numbers in the slider boxes, and may show truncated labels and vanished tracks. The owner's eye is the oracle for the layout claim, as in the 2026-10-05 and 2026-10-06 rounds.

## Documentation

None. The README describes proof modes, the preset schema, and the acceptance toggles. It says nothing about label sizing, so nothing goes stale.

## Acceptance criteria

1. The helper's new rule passes its falsifier test RED first, then GREEN, with the old subtract rule named as the wrong implementation.
2. The full `Alrauna.Amuse.Tests.Editor` assembly passes with observed counts.
3. At an inspector width near 700 pixels, every child row label of both submenus draws in full, verified by the owner.
4. At an inspector width near 505 pixels, every child row label still draws in full, verified by the owner. This pins the crossover.
5. At an inspector width near 373 pixels, the dropdown shows its selected option text, the slider box shows its number, and the track may be gone, verified by the owner.
6. The divide position is stable within each row type and does not move when a submenu folds, verified by the owner.
7. `git diff` shows exactly two changed files: the editor and the test class.

## Validation and stop conditions

Compile through an asset refresh, then the focused helper test, then the full editor test group. Record observed counts. Stop and return evidence if any of these appears: an assertion that the investigation's numbers do not predict, a visible structural change against invariant 1, a divide that moves on a submenu fold, or a failing test outside the replaced one.

## Authorization boundary

On 2026-10-18 the owner authorized this feature, the work branch, and one commit per finalized change: the investigation, this spec, the plan, and each implemented plan task. Staging is limited to the files a step names. Pushing, pull requests, branch deletion, and history changes stay unauthorized.

## Out of scope

The proof row's value string clipping at the shifted control position, the few pixels of native popup style padding, the `finally` repair of the label width restore, runtime measurement of the popup option strings, per-row ragged label columns, changes to any drawer outside the width assignment, changes to the research package, and a later-round unification of the transformations drawer's `indentLevel * 15` arithmetic with the `IndentedRect` query this design introduces.
