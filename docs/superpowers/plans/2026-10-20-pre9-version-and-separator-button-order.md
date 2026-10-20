# Release 0.1.0-pre.9 and separator button order Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Flip the two separator sub-switch toggles below the Alpha Separator row, pin the new order with a RED/GREEN test, and bump the package version to `0.1.0-pre.9`.

**Architecture:** Two independent micro-changes. A draw-order swap inside one `IndentLevelScope` block in the optimizer custom inspector, covered by one order-pinning UnityTest. A two-line data bump in `package.json` and the package changelog. No production logic, serialization, preset, or workflow change.

**Tech Stack:** Unity 2022.3 EditMode tests through the Unity Test Runner, driven by Unity MCP tools. Plain C# file edits. Git commits with exact subjects.

**Spec:** `docs/superpowers/specs/2026-10-20-pre9-version-and-separator-button-order-design.md` (the spec is the binding authority; read it together with this plan)

## Global Constraints

- Draw order target: `Transparent Separator` above `Cutout Separator`, both one indent level below the `Alpha Separator` row, visible only while the master switch is on.
- No copy changes: the master row tooltip, runtime doc comments, changelog bullet, and report hint stay as they are.
- No change to `Runtime/AmuseAvatarOptimizer.cs` field declaration order, to preset schema 5, to the Settings submenu pair (`Editor/AmuseAvatarOptimizerEditor.cs:787-952`), or to any workflow file.
- The new test carries: a sibling-style doc comment naming the one-pair-per-pass ceiling, the repo falsifier marker with the next unused falsifier number in the file (3 at the base commit), and a `finally` block that nulls both static hooks.
- Commit subjects, exact: `feat: draw transparent separator above cutout separator` (flip plus test, one commit), then `chore(pkg): update version to 0.1.0-pre.9` (bump, alone, last commit on the branch).
- Bump strings, exact: `"version": "0.1.0-pre.9"` in `package.json:4`; `## [0.1.0-pre.9] - 2026-10-20` replacing `## [Unreleased]` at `Packages/com.alrauna.amuse/CHANGELOG.md:5`.
- The research package manifest (`Packages/com.alrauna.amuse.research/package.json`) stays at `0.0.1`.
- Stage explicit paths only. `ProjectSettings/QualitySettings.asset` is a pre-existing user-owned modification. Never stage or commit it.
- A test run that reports 0 tests is a failure. Record observed pass/fail counts.
- Before any Unity MCP operation beyond the read-only pre-flight: confirm the editor targets this project. Run `execute_code` with `action: "execute"` and `code: "return Application.dataPath;"` and require the result to end with `AMUSE/Assets`.
- All committed text obeys the repository document rules: short sentences, no machine paths, no private identifiers.

---

### Task 1: RED/GREEN draw order flip with order-pinning test

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs` (insert one test method after `SubSwitchHooksFireWithStoredValuesWhenMasterSwitchIsOn`, which closes at `:276`, before `SubSwitchHooksDoNotFireWhenMasterSwitchIsOff` at `:278`)
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:173-197` (swap the two statement pairs)

**Interfaces:**
- Consumes: existing static test hooks `OnCutoutSeparatorToggleDrawnForTest` and `OnTransparentSeparatorToggleDrawnForTest` (`Action<bool>`, `Editor/AmuseAvatarOptimizerEditor.cs:63-65`); existing helpers `NewHiddenComponent`, `SetSwitch`, `RepaintThroughHostWindow` in the test file.
- Produces: new test `TransparentSeparatorDrawsBeforeCutoutSeparator`; new draw order transparent-then-cutout inside the master-switch block. Nothing else consumes these.

- [ ] **Step 1: Unity MCP pre-flight (read-only), pin before probing**

The editor host may expose several connected editors. Order matters: a probe before pinning fails or, worse, invites pinning the wrong editor.

1. Read the resource `mcpforunity://instances` to enumerate reachable editors.
2. For each candidate: write the instance name to `xd://mcp__unitymcp_set_active_instance`, then write `{"action":"execute","code":"return Application.dataPath;"}` to `xd://mcp__unitymcp_execute_code`.
3. Accept the first instance whose data path ends with `AMUSE/Assets`. Leave that pin in place. If no candidate matches, stop and report.
4. Record which instance was pinned by role only, never by name, in any artifact this plan produces.

- [ ] **Step 2: Write the failing test**

Insert this method verbatim between the two existing sub-switch tests in `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`:

```csharp
        /// <summary>
        /// Known limit, recorded: each full GUI pass runs the whole
        /// OnInspectorGUI once, and both hooks sit outside PropertyField
        /// in the same unconditional block. Every pass therefore appends
        /// exactly one transparent and one cutout entry to the shared
        /// list. A harness change that breaks that ceiling fails the
        /// even-count assertion instead of passing silently. Each hook
        /// is invoked immediately after its own PropertyField, so hook
        /// fire order equals draw order.
        /// --- Falsifier 3: the base-commit draw order, which draws the
        /// cutout sub-switch first. Against that order the first pair
        /// assertion fails, because element 0 is cutout. An
        /// implementation that drops a hook fails the not-empty or the
        /// even-count assertion. ---
        /// </summary>
        [UnityTest]
        public IEnumerator TransparentSeparatorDrawsBeforeCutoutSeparator()
        {
            var component = NewHiddenComponent();
            SetSwitch(component, "_alphaSeparatorEnabled", true);
            SetSwitch(component, "_cutoutSeparatorEnabled", true);
            SetSwitch(component, "_transparentSeparatorEnabled", true);
            var drawn = new List<string>();
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnTransparentSeparatorToggleDrawnForTest = _ =>
                    drawn.Add("transparent");
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnCutoutSeparatorToggleDrawnForTest = _ =>
                    drawn.Add("cutout");
            try
            {
                yield return RepaintThroughHostWindow(component);

                Assert.That(drawn, Is.Not.Empty,
                    "no sub-switch hook ever fired");
                Assert.That(drawn.Count % 2, Is.EqualTo(0),
                    "every pass records one pair, so the count is even");
                for (var i = 0; i < drawn.Count; i += 2)
                {
                    Assert.That(drawn[i], Is.EqualTo("transparent"),
                        "the transparent sub-switch draws above the " +
                        "cutout sub-switch in every pass");
                    Assert.That(drawn[i + 1], Is.EqualTo("cutout"),
                        "the cutout sub-switch follows the transparent " +
                        "sub-switch in every pass");
                }
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnTransparentSeparatorToggleDrawnForTest = null;
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnCutoutSeparatorToggleDrawnForTest = null;
            }
        }
```

Before inserting, scan the test file for `--- Falsifier` occurrences. If a number higher than 2 exists at insertion time, renumber this marker to the next unused number.

- [ ] **Step 3: Refresh Unity and confirm a clean compile**

Write `{"mode":"force","scope":"scripts","compile":"request","wait_for_ready":true}` to `xd://mcp__unitymcp_refresh_unity`. If the refresh call itself errors across a domain reload, re-issue it once and probe readiness again before declaring a failure. Then write `{"action":"get","types":["error","warning"],"count":"20"}` to `xd://mcp__unitymcp_read_console`. Expected: no compile errors and no unexpected warnings. Record both counts. After `wait_for_ready` returns, re-run the Step 1 data path probe once and require the same `AMUSE/Assets` result before any test batch.

- [ ] **Step 4: Run the focused test and observe RED**

Write this to `xd://mcp__unitymcp_run_tests`:

```json
{"mode":"EditMode","test_names":["Alrauna.Amuse.Tests.Editor.AmuseAvatarOptimizerEditorTests.TransparentSeparatorDrawsBeforeCutoutSeparator"],"include_failed_tests":true}
```

Poll with `xd://mcp__unitymcp_get_test_job`, `{"job_id":"<id>","include_failed_tests":true,"wait_timeout":60}`. Expected: exactly 1 test, result Failed. The failure message names the first pair assertion, with content `Expected: "transparent"` and `But was: "cutout"`. Match the message by content, not byte for byte. Record the observed output verbatim. Any other failure mode is a defect to investigate before proceeding. If any test batch in this plan reports an orphaned or stuck job after a refresh, recover with `{"clear_stuck":true}` on `xd://mcp__unitymcp_run_tests`, re-run the batch, and record the counts.

- [ ] **Step 5: Swap the draw order**

In `Editor/AmuseAvatarOptimizerEditor.cs`, replace the block at `:173-197` with exactly:

```csharp
            if (masterSwitch.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("_transparentSeparatorEnabled"),
                        new GUIContent("Transparent Separator",
                            "Turn this off to skip separation for slots whose " +
                            "material uses blended alpha. Suppressed slots are " +
                            "reported as skipped."));
                    OnTransparentSeparatorToggleDrawnForTest?.Invoke(
                        serializedObject.FindProperty("_transparentSeparatorEnabled")
                            .boolValue);
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("_cutoutSeparatorEnabled"),
                        new GUIContent("Cutout Separator",
                            "Turn this off to skip separation for slots whose " +
                            "material uses cutout alpha. Suppressed slots are " +
                            "reported as skipped."));
                    OnCutoutSeparatorToggleDrawnForTest?.Invoke(
                        serializedObject.FindProperty("_cutoutSeparatorEnabled")
                            .boolValue);
                    serializedObject.ApplyModifiedProperties();
                }
            }
```

Touch nothing else in the file. The tooltips move with their toggles. The `ApplyModifiedProperties()` stays after both pairs.

- [ ] **Step 6: Refresh, rerun the focused test, observe GREEN**

Repeat Step 3, including the data path re-probe. Rerun the Step 4 test run. Expected: exactly 1 test, result Passed.

- [ ] **Step 7: Run both full test assemblies**

Run `{"mode":"EditMode","assembly_names":["Alrauna.Amuse.Tests.Editor"],"include_failed_tests":true}`, then `{"mode":"EditMode","assembly_names":["Alrauna.Amuse.Research.Tests.Editor"],"include_failed_tests":true}`. Poll each. Expected: both assemblies fully Passed, 0 failures, nonzero counts. The product assembly run includes the full `AmuseAvatarOptimizerEditorTests` class, so `SubSwitchHooksFireWithStoredValuesWhenMasterSwitchIsOn` and `SubSwitchHooksDoNotFireWhenMasterSwitchIsOff` must appear among the passed tests. Record both assembly counts. A 0-test run is a failure.

- [ ] **Step 8: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs
git diff --cached --check
git commit -m "feat: draw transparent separator above cutout separator"
```

Stage exactly those two paths. Never stage `ProjectSettings/QualitySettings.asset`. `git diff --cached --check` inspects exactly the staged set. If it ever names that user-owned file, stop and report instead of touching it.

---

### Task 2: Version bump to 0.1.0-pre.9

**Files:**
- Modify: `Packages/com.alrauna.amuse/package.json:4`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md:5`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: the release version `0.1.0-pre.9`; the changelog section header the release workflow tags from.

- [ ] **Step 1: Apply both bump lines**

1. In `Packages/com.alrauna.amuse/package.json`, change line 4 from `"version": "0.1.0-pre.8",` to `"version": "0.1.0-pre.9",`. Touch no other line.
2. In `Packages/com.alrauna.amuse/CHANGELOG.md`, change line 5 from `## [Unreleased]` to `## [0.1.0-pre.9] - 2026-10-20`. Touch no other line. The block keeps its existing bullets.

- [ ] **Step 2: Verify**

1. Re-run the Task 1 Step 1 pre-flight gate: enumerate, pin, and require the `AMUSE/Assets` data path before any editor operation.
2. Read back both lines and confirm the exact strings from the Global Constraints.
3. Refresh Unity: write `{"mode":"force","scope":"all","compile":"request","wait_for_ready":true}` to `xd://mcp__unitymcp_refresh_unity`. If the refresh errors across a reload, re-issue it once. Expected: the editor re-resolves the embedded package with no error. If the version probe below still returns the old value, re-run the refresh once before investigating.
4. Write `{"action":"get","types":["error","warning"],"count":"20"}` to `xd://mcp__unitymcp_read_console`. Expected: no package resolution errors and no unexpected warnings.
5. Confirm the editor resolved the package version by running `{"action":"execute","code":"var p = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor).Assembly); return p == null ? \"NULL\" : p.version;"}` through `xd://mcp__unitymcp_execute_code`. Expected output: `0.1.0-pre.9`.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/package.json Packages/com.alrauna.amuse/CHANGELOG.md
git diff --cached --check
git commit -m "chore(pkg): update version to 0.1.0-pre.9"
```

Stage exactly those two paths. This is the last commit on the branch. If the staged check names the user-owned file, stop and report.

---

## Self-review record

- Spec coverage: A1 maps to Task 1 Step 5. A2 maps to the Global Constraints and the "touch nothing else" steps. A3 maps to Task 1 Steps 2, 4, 6. A4 maps to Task 1 Step 8. B1 and B2 map to Task 2 Step 1. B3 and B4 map to Task 2 Step 3 and the ordering of tasks. C1 maps to Steps 4, 6, 7. C2 maps to Task 2 Step 2. C3 holds: no version test is added. D holds: no helper, no abstraction, no copy change.
- Placeholder scan: every code step carries the exact code or exact JSON to write. No TBD, no "similar to".
- Type consistency: hook names, helper names, test name, and commit subjects match the spec and the base file verbatim.

## Review findings integrated

- 2026-10-20: five parallel adversarial reviews ran against the first draft. Their axes were accuracy against the spec, safety and security, architectural purity, ponytail adherence, and latent bugs. Blocking fixes applied: the pre-flight now pins an instance before probing, because a probe before a pin errors on a multi-editor host and invites pinning the wrong editor. The data path gate re-runs after every refresh, and Task 2 re-runs the full gate. Both commit steps use `git diff --cached --check`, because a bare check after staging inspects the wrong diff and can name the user-owned file. The edit range citation moved from `:173-196` to `:173-197`, because the replacement block includes both closing braces. Minor fixes applied: the console reads use the real schema and check errors and warnings, the falsifier doc comment records that fire order equals draw order, the RED comparison matches by content, the stuck-job recovery is named, the refresh reload-error recovery is named, the version probe is null-safe, and the redundant full-class run merged into the assembly run. One finding is deferred: the class `TearDown` could also null the static test hooks to cover a timeout abort that abandons a `finally` block. That hardening is beyond the spec. Every consumer assigns its own hooks before use, so the impact is bounded. The final whole-branch review may weigh it.
