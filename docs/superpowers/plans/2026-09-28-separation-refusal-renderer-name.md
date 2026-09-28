# Separation Refusal Renderer Name Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every slot-separation refusal report name the renderer it refused whenever that renderer's name is available, by resolving an omitted `rendererName` from the `renderer` argument inside the single report helper, so no report ever renders NDMF's `<missing>` placeholder for a live renderer.

**Architecture:** One fallback expression inside `AmuseReports.SlotSeparationRefusal`. Explicit names keep winning; a null renderer keeps the honest placeholder. Three preparation-pass emission sites (`AlphaSeparationPreparation.cs:270-274`, `:330-334`, `:438-441`) and the apply pass's renderer-changed site (`AlphaSeparationApply.cs:164-168`) heal with no call-site edits, and future emission sites cannot reintroduce the defect.

**Tech Stack:** Unity 2022.3.22f1 editor assembly (`Alrauna.Amuse.Editor`), NUnit via Unity Test Framework, EditMode only. Tests run in the dev editor instance through the Test Runner; there is no CLI test runner.

**Spec:** `docs/superpowers/specs/2026-09-28-separation-refusal-renderer-name-design.md`

## Global Constraints

- Tests never run in the Census Lab project. Every test run happens in the dev editor instance against this repository, pinned by exact identity before any call.
- Never stage or commit without explicit authorization at execution time. The commit step below carries the exact paths and message for when authorization is given.
- No new refusal enum values. No change to report strings, keys, emission sites, signatures, classification, capture, or the mutation path.
- User-visible report strings follow the house string style: short sentences, active voice, one idea per sentence, no contractions, no semicolons. This fix adds no strings.
- XML doc comments on load-bearing rules state why, not what.
- A filtered test run that reports 0 tests is a failure. Record observed counts for every run.
- After the last code step, run `git diff --check` and sweep the changed files for identifiers: an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and private asset names. Every hit is a defect.

---

### Task 1: The renderer-name fallback in the separation refusal helper

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs` (the `SlotSeparationRefusal` body, right after the `AlphaSeparationSlotRefusal.None` guard, around line 160)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs` (after the existing `SlotSeparationRefusalReportsRendererAndMaterial` test, around line 153)

**Interfaces:**
- Consumes: the existing `AmuseReports.SlotSeparationRefusal(Renderer renderer, int slotIndex, AlphaSeparationSlotRefusal cause, string rendererName = null, Material offendingMaterial = null, IReadOnlyDictionary<Material, Material> provenMapping = null, string detail = null)`. The signature does not change.
- Produces: nothing new. Every existing call site keeps compiling unchanged; only the rendered text moves for omitted-name emissions over live renderers.

- [ ] **Step 1: Write the failing tests**

In `AmuseReportsSlotTests.cs`, after the `SlotSeparationRefusalReportsRendererAndMaterial` test and before the `NewFixtureMaterial` helper, add:

```csharp
        [Test]
        public void SlotSeparationRefusalNamesTheRendererWithoutAnExplicitName()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationRefusal(
                    _renderer,
                    0,
                    AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // After play mode ends the build-copy renderer is destroyed
            // and the console's object link is dead, so the report text
            // is the only place that still names the renderer. NDMF
            // renders a null substitution as the literal "<missing>".
            Assert.That(message, Does.Contain("AMUSE slot report"),
                "the report must name the refused renderer");
            Assert.That(message, Does.Not.Contain("<missing>"),
                "a named renderer must never render the missing " +
                "placeholder");
        }

        [Test]
        public void SlotSeparationRefusalEmptyRendererNameRendersEmptyNotMissing()
        {
            _root.name = "";

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationRefusal(
                    _renderer,
                    0,
                    AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // Falsifier: an implementation that treats an empty name as
            // a missing name prints the placeholder for a renderer that
            // has a name, however short. An empty string is not null.
            Assert.That(message, Does.Not.Contain("<missing>"));
        }

        [Test]
        public void SlotSeparationRefusalWithoutRendererKeepsTheMissingPlaceholder()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationRefusal(
                    null,
                    0,
                    AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // Falsifier: the fallback must not throw on an absent
            // renderer, and the placeholder stays the truthful rendering
            // when no renderer exists to name.
            Assert.That(message, Does.Contain("<missing>"));
        }
```

The fixture's `[SetUp]` already names the root `"AMUSE slot report"`, so the first test reads the name straight from the fixture and no new fixture state is added.

- [ ] **Step 2: Refresh Unity and run the filter to verify the RED**

Refresh the dev editor instance so the edited test file compiles, then run the EditMode filter `AmuseReportsSlotTests` (Unity MCP: `refresh_unity`, then `run_tests` in EditMode mode with that filter, polled through `get_test_job`).

Expected: 10 tests total (7 pre-existing, 3 new). Exactly the two new name tests fail — `SlotSeparationRefusalNamesTheRendererWithoutAnExplicitName` on both assertions (the message carries `<missing>` and not `AMUSE slot report`), and `SlotSeparationRefusalEmptyRendererNameRendersEmptyNotMissing` on its one assertion. `SlotSeparationRefusalWithoutRendererKeepsTheMissingPlaceholder` passes: it is the no-op guard. Every pre-existing test passes. Record the observed counts and the failure text; a run that reports 0 tests is a failure, and a green run here means the RED was not observed.

- [ ] **Step 3: Add the fallback**

In `AmuseReports.cs`, inside `SlotSeparationRefusal`, immediately after the `AlphaSeparationSlotRefusal.None` guard and before the `RegisteredSourceIdentity.Resolve` line:

```csharp
            // The separation refusals must stay readable after play mode
            // ends: the build-copy renderer is destroyed then, the
            // console's object link is dead, and the report text is the
            // only place that still names the renderer. NDMF renders a
            // null substitution as the literal "<missing>", so an
            // omitted name falls back to the live renderer's own name.
            // An explicit name keeps winning, and a null renderer keeps
            // the honest placeholder.
            rendererName = rendererName
                ?? (renderer != null ? renderer.gameObject.name : null);
```

Nothing else in the file changes. The ReportError argument list, the template strings, and every other helper stay untouched.

- [ ] **Step 4: Run the filter to verify the GREEN**

Refresh the dev editor instance, then run the EditMode filter `AmuseReportsSlotTests` again.

Expected: 10 tests, all passing, including the pre-existing `SlotSeparationRefusalReportsRendererAndMaterial` test that pins explicit-name precedence and must not have changed. Record the observed counts.

- [ ] **Step 5: Run the full assemblies**

Refresh, then run the full `Alrauna.Amuse.Tests.Editor` assembly in EditMode mode, then the full `Alrauna.Amuse.Research.Tests.Editor` assembly, which consumes the same internals through `InternalsVisibleTo`.

Expected: both assemblies fully green. Record the observed pass and fail counts for each. Any failure stops the task: do not widen the fallback to other helpers to make an unrelated test pass, and report the failure instead.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs
git commit -m "fix: name the renderer in separation refusal reports"
```

Only with explicit authorization at execution time.

---

## Validation summary and stop conditions

- RED evidence: the two new tests fail on current code with the placeholder text observed in the sanitized follow-up record, at the `AmuseReportsSlotTests` filter, with recorded observed counts.
- GREEN evidence: the same filter fully passes after the one-expression change, then both full assemblies pass, with recorded counts.
- Stop and report if: the RED run shows the guard test failing (the placeholder contract moved), any pre-existing test fails after the fix, or the Research assembly fails. Do not fix unrelated failures inside this branch; report them.
- The Census Lab embedded package copy is synced by hand; ask before assuming it matches any commit, and never edit it. Census Lab validation after the merge is a separate, read-only console observation, not a gate for this branch.
