# Alpha Separator Row Sizing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Alpha Separator Settings label budget always cover the widest label text at the deepest drawn row, and yield on narrow inspectors while the slider box and dropdown keep a text-sized minimum.

**Architecture:** One editor helper changes from the subtract-per-level rule to a two-term rule (text term versus control budget, floored at 1 pixel), one internal constant carries the popup minimum, and one call site feeds the helper a runtime-queried deepest-row indent and row width. The block's structure, rows, and widgets do not change. The old helper test is replaced one for one by the new falsifier test.

**Tech Stack:** Unity 2022.3.22f1, C#, NUnit via Unity Test Framework, EditMode only. No dotnet build, no Node.

**Spec:** `docs/superpowers/specs/2026-10-18-alpha-separator-row-sizing-design.md` (the spec's exact code is authoritative and this plan copies it verbatim)

## Working discipline

- Branch `ux/indent-alpha-separator-submenus`, base `main` at `03d46fe`. The branch exists. Work on it. No separate worktree.
- Allowed mutations, exhaustive:
  - `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`
  - `docs/superpowers/plans/2026-10-18-alpha-separator-row-sizing-plan.md` (checkbox ticks only, never committed)
- The finalized plan record lands in its own `docs:` commit on this branch before execution starts, matching house practice. The two source files land in the task's commit at step 7.
- Never touch: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated `*.csproj`/`*.sln`, vendor packages under `Packages/` other than `com.alrauna.amuse`, the research package, every file under `docs/superpowers/` dated before 2026-10-18, and the untracked `Assets/AmuseTests_Temp*` debris.
- The task ends with exactly one commit of the two named source files. Never stage unrelated files, and never stage the working tree's pre-existing uncommitted indent scopes separately — they are part of this branch's diff and ride along inside the same editor file when it is staged.
- New production members, exactly two: `internal const float AlphaPopupMinimum` and the three-parameter `internal static float AlphaSeparatorLabelWidth(float, float, float)`. The one-parameter helper it replaces is deleted with its doc comment. Nothing else is added.
- A behavior change observes RED first against the named wrong implementation, records the observed failure output, then applies the minimal production change and observes GREEN. An assertion that passes on first run is recorded as characterization, never dressed up as RED.
- The task report is the implementer's handoff message back to the orchestrator. No report file is created.
- A filtered run that reports zero tests is a failure. A successful compile is never validation.
- Never weaken, skip, or rewrite any other test. The draw tests, reflection-coupled tests, and host-window repaint suite are the smoke validation.

## Unity validation procedure (every run)

1. Enumerate the reachable Unity instances read-only (resource `mcpforunity://instances`). For each candidate instance, read `Application.dataPath` through `execute_code` with that instance's `unity_instance` parameter. Pin with `set_active_instance` only on an exact match to this repository's `Assets` path. A case-only match is not identity. Stop when zero instances match. Do not launch, install, or configure anything.
2. Read `mcpforunity://editor/state` and proceed only when the editor is not playing and not compiling. Otherwise stop and report.
3. Refresh assets and wait for readiness (`refresh_unity`), then read the console for compile errors. Copy any compile error verbatim and stop.
4. Before the first source edit, run the full `Alrauna.Amuse.Tests.Editor` group (EditMode) once and record the observed pass, fail, and skip counts. These counts are the baseline for step 6's comparison.
5. Verify the `run_tests` parameter names in the tool docs (`xd://mcp__unitymcp_run_tests`) before first use. The tool takes test, group, and assembly name filters, not NUnit `FullyQualifiedName~` strings.
6. Run the named tests, then poll `get_test_job` until the run finishes. Record observed pass and fail counts. Copy failure messages verbatim for the RED step.

## Global constraints

- The helper's rule, exact: `Mathf.Min(Mathf.Ceil(widestLabelWidth) + 2f + deepestRowIndent, Mathf.Max(deepestRowWidth - AlphaPopupMinimum - 2f, 1f))`.
- `AlphaPopupMinimum = 72f`, `internal const`, with the `ponytail:` ceiling comment from the spec.
- The call site queries `EditorGUI.IndentedRect` at `indentLevel + 1` and restores the indent on the same statement path.
- The save and restore of `EditorGUIUtility.labelWidth` keep their current shape. No `finally` is added.
- Test pins, exact: `(200f, 60f, 500f)` to `262f`. `(200f, 60f, 200f)` to `126f`. `(200f, 60f, 0f)` to `1f`. `(200f, 60f, -50f)` to `1f`.
- The falsifier block reads `--- Falsifier 1: ... ---` and names the three wrong implementations stated in the spec.
- Depths, structure, rows, widgets, tooltips: unchanged. No other statement of the block's drawing code may change.

---

### Task 1: Two-term label width helper, runtime indent query, and the replaced falsifier

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**
- Consumes: the editor's existing `widest` measurement (a local variable in `DrawAlphaSeparatorSettings`), the existing save and restore of `EditorGUIUtility.labelWidth` around the block body, and the existing test helpers of the editor test class (none of which change).
- Produces: `internal static float AlphaSeparatorLabelWidth(float widestLabelWidth, float deepestRowIndent, float deepestRowWidth)` and `internal const float AlphaPopupMinimum = 72f` on `AmuseAvatarOptimizerEditor`. The replaced test `AlphaSeparatorLabelWidthBalancesTextAndControlBudget` pins them. No later task exists. Nothing else consumes these members.

- [ ] **Step 1: Replace the old helper test with the new falsifier test.**

In `AmuseAvatarOptimizerEditorTests.cs`, delete the whole `AlphaSeparatorLabelWidthSubtractsTheRowIndent` method with its doc comment (the block spans lines 972-1005: doc comment, `[Test]` attribute, method body, `finally` restore, closing braces). In its place, add this method. It targets the three-parameter signature, so the test assembly does not compile against the old helper.

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

**No refresh and no test run happens between this step and step 2.** The broken compile state is deliberate. Unity's auto-refresh may compile on editor focus and report errors naming the missing three-parameter `AlphaSeparatorLabelWidth`. That is the expected state. Proceed directly to step 2. A test run in this state would load the last-good stale assemblies and could report a false green from the old test, so it is forbidden.

- [ ] **Step 2: Land the new helper signature and call site with the named wrong body (budget-ignoring).**

In `AmuseAvatarOptimizerEditor.cs`, replace the old helper with its doc comment (the block spans lines 821-831: doc comment, method, closing brace):

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
    return Mathf.Ceil(widestLabelWidth) + 2f + deepestRowIndent;
}
```

This body is the budget-ignoring wrong implementation named by the falsifier. It exists so the RED is behavioral, not a compile error. Replace the call site so the block reads:

```csharp
var previousWidth = EditorGUIUtility.labelWidth;
EditorGUI.indentLevel++;
var deepestRowIndent = EditorGUI.IndentedRect(new Rect(0f, 0f, 0f, 0f)).x;
var deepestRowWidth = EditorGUIUtility.currentViewWidth - deepestRowIndent;
EditorGUI.indentLevel--;
EditorGUIUtility.labelWidth = AlphaSeparatorLabelWidth(
    widest, deepestRowIndent, deepestRowWidth);
DrawTransparentSubmenu();
DrawCutoutSubmenu();
DrawAcceptanceControls();
EditorGUIUtility.labelWidth = previousWidth;
```

Keep the surrounding scopes and every other line of the method exactly as they are.

- [ ] **Step 3: Refresh, run the focused group, record the RED.**

Run the validation procedure steps 2, 3, and 6 with the group `AmuseAvatarOptimizerEditorTests` (EditMode). Expected: the new test fails on the second assertion with `Expected: 126f But was: 262f`. NUnit stops the test at the first failed constraint, so the later assertions are unreachable in this run. Every other test in the group passes. The budget-ignoring intermediate draws the block with a constant label width near 366 pixels, which starves the control columns at narrow host windows. That is the defect under fix, and the repaint tests hook drawn state, not layout, so they pass. If any unrelated test fails, rerun it once. A reproducible unrelated failure stops the task under the spec's stop conditions. A repaint failure reported as "the host window never painted" is traced before blame, because it would name an exception, not an assertion. Copy the failure message verbatim into the task report. A compile error here means step 2's edits were not applied as written. Fix and rerun before recording anything.

- [ ] **Step 4: GREEN. Apply the exact final helper and the constant.**

Add the constant directly below the helper method. Order matters: the doc comment stays bound to the method, and the constant carries only its `ponytail:` comment:

```csharp
// ponytail: AlphaPopupMinimum is sized for "All Sizes" plus the arrow
// at the default font. Measure the option strings at draw time when a
// larger editor font clips it.
internal const float AlphaPopupMinimum = 72f;
```

Change the helper body to the exact final rule:

```csharp
    var controlBudget = Mathf.Max(
        deepestRowWidth - AlphaPopupMinimum - 2f, 1f);
    // ponytail: the budget reads the row width from the view width and
    // leaves the few pixels of view inset unmodeled, so the popup floor
    // lands a few pixels under 72 at docked widths. Measure the level-4
    // row rect if that gap ever matters.
    return Mathf.Min(
        Mathf.Ceil(widestLabelWidth) + 2f + deepestRowIndent,
        controlBudget);
```

- [ ] **Step 5: Run the focused group, record GREEN.**

Run the validation procedure steps 2, 3, and 6 with the group `AmuseAvatarOptimizerEditorTests` (EditMode). Expected: the new test passes, all other tests in the group pass, nonzero count. Record counts.

- [ ] **Step 6: Run the full editor assembly, record counts against the baseline.**

Run the full `Alrauna.Amuse.Tests.Editor` group (EditMode). Expected: zero failures beyond the baseline recorded in validation procedure step 4, and a skip count unchanged from that baseline. The skips are runtime gates keyed to vendor packages and an environment variable, none of which this change touches. The research test assembly is out of scope for this run: it tests only research source files, and this change touches none of them. Record counts.

- [ ] **Step 7: Commit.**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs
git commit -m "feat: size alpha separator rows by label text and control budget"
```

- [ ] **Step 8: Hand the visual check to the owner.**

Report to the owner: the observed RED output from step 3, the GREEN counts from steps 5 and 6 against the step 4 baseline, and the three inspector widths to eyeball (about 700, 505, and 373 pixels) against acceptance criteria 3 through 6 of the spec. The commit has landed. If the owner's eye rejects the layout, the fix is a new dated change, not a rewrite of this one.

## Self-review notes

- Spec coverage: spec decisions 1 through 8 land in steps 2 and 4. Invariant 7 lands in step 2's bump-restore pair. Invariant 8 lands in step 1's one-for-one replacement. Acceptance criteria 1 and 2 land in steps 3, 5, and 6. Criteria 3 through 6 are the owner check in step 8. No open item.
- Placeholder scan: every code block is the exact final text from the spec, except the step 2 helper body, which is the named budget-ignoring wrong implementation and is replaced in step 4. Every command names its tool and filter. No TBD.
- Type consistency: the falsifier's three-parameter calls match step 2's signature. The pins match the spec's Global constraints verbatim. The test name matches the spec's test block.
