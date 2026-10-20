# Release 0.1.0-pre.9 and separator button order design

Status as of 2026-10-20. Branch: `feature/pre9-version-and-separator-order`. This version integrates five parallel adversarial reviews. Their axes: accuracy against the investigation, safety and security, architectural purity, ponytail adherence, and latent bugs. Their findings and the corrections are listed at the end.

Evidence base: `docs/superpowers/investigations/2026-10-20-pre9-version-and-separator-button-order-investigation.md` (referenced as "the investigation", as corrected on 2026-10-20). This document argues from the investigation and decides what it left open.

## Owner decisions recorded 2026-10-20

1. The package release version becomes `0.1.0-pre.9`.
2. The Cutout Separator and Transparent Separator toggles below the Alpha Separator switch swap draw order.

The three open questions from the investigation (section 6) are decided in this document. None needs a new owner decision. The owner instructed the run to halt on material choices only.

## Goals

1. The Transparent Separator toggle draws above the Cutout Separator toggle, one indent level below the Alpha Separator row, when the master switch is on.
2. The package version declares `0.1.0-pre.9` in `package.json`, and the changelog promotes the `Unreleased` block to a dated `0.1.0-pre.9` section.
3. No other behavior changes: serialization, presets, schema 5, build passes, reports, and the dry run stay byte-for-byte at base-commit behavior.

## Non-goals

1. No copy changes. The master row tooltip, the runtime doc comments, the changelog bullet, and the report hint name both controls without a positional claim between them. They stay as they are (investigation section 4.5).
2. No changes to the Alpha Separator Settings submenus. That second Transparent-first pair is a different surface (investigation section 4.3).
3. No preset schema work. The flip touches draw order only (investigation section 4.2).
4. No research package version change. `com.alrauna.amuse.research` stays at `0.0.1` and never releases (investigation section 3.1).
5. No workflow changes. The release workflow reads the version at run time. The listing workflow takes no version input (investigation section 3.2).

## Decisions

### A. The draw order flip

A1. Site: `Editor/AmuseAvatarOptimizerEditor.cs`, `OnInspectorGUI()`, inside the `IndentLevelScope` block at `:175-196`. The two statement pairs swap. Each `GUIContent` and each test hook call moves with its own toggle. The shared `serializedObject.ApplyModifiedProperties()` stays after both pairs. The block reads, in full:

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

A2. Nothing else in the file moves. Field declaration order in `Runtime/AmuseAvatarOptimizer.cs` stays. Serialized data binds by name, so saved components and prefabs are unaffected.

A3. RED/GREEN test. New `[UnityTest]` method `TransparentSeparatorDrawsBeforeCutoutSeparator` in `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`, placed beside `SubSwitchHooksFireWithStoredValuesWhenMasterSwitchIsOn` (`:237-276`). The setup matches that test in mechanics: a hidden component, `SetSwitch` for the three fields, both hooks subscribed, `RepaintThroughHostWindow` invoked, and both hooks reset to null in a `finally` block, as in the sibling sub-switch tests. The stored values differ by design: all three switches set true, where the sibling stores the transparent switch false. Both hooks append their name to one shared ordered `List<string>`. The test carries a doc comment in the sibling style. It names the repaint-behavior ceiling: each full GUI pass runs the whole `OnInspectorGUI` once and appends exactly one transparent and one cutout entry, because both hooks sit outside `PropertyField` in the same unconditional block. It also carries the repo falsifier marker, `--- Falsifier N: base-commit draw order ---`, with N the next unused falsifier number in the file. Assertions:

1. The list is not empty.
2. The count is even.
3. For every index pair, element `2i` is `"transparent"` and element `2i + 1` is `"cutout"`.

The named plausible wrong implementation is the base-commit draw order. It fails assertion 3 at the first pair. After the flip the test passes. The existing two sub-switch tests keep passing unchanged because they record per-toggle lists and assert order-insensitive `(value, indent)` tuples (investigation section 4.4). If a future harness change ever breaks the one-pair-per-pass ceiling, the fallback assertion is "the first element is `transparent` and every `cutout` is immediately preceded by a `transparent`", which names the same order.

A4. Commit: the flip and its test land as one commit, subject `feat: draw transparent separator above cutout separator`.

### B. The version bump

B1. `package.json:4` becomes `"version": "0.1.0-pre.9"`.

B2. The changelog header `## [Unreleased]` (`CHANGELOG.md:5`) becomes `## [0.1.0-pre.9] - 2026-10-20`. The header date is the bump date. The block gains no new bullet: the flip amends the not-yet-released sub-switch feature, and no bullet states an order between the two controls. Promotion of an `Unreleased` block is a new convention choice for this bump. The pre.8 commit renamed an already dated header instead (investigation section 3.4).

B3. Commit: the bump lands alone as one commit, subject `chore(pkg): update version to 0.1.0-pre.9`, following the newest bump subject form (investigation section 3.4). The bump is the last commit on the branch, so a release cut from the branch tip carries the flip.

B4. Ordering: the flip commit lands before the bump commit.

### C. Verification

C1. Flip: refresh Unity, run the focused EditMode filter `AmuseAvatarOptimizerEditorTests` and observe the new test fail before the flip and pass after. Then run the full `Alrauna.Amuse.Tests.Editor` assembly and the full `Alrauna.Amuse.Research.Tests.Editor` assembly, following documented in-repo practice. A filtered run that reports 0 tests is a failure.

C2. Bump: read back `package.json` and the changelog header. `git diff --check` on the branch must stay clean. Unity re-resolves the embedded package without error after refresh. Operational note for release time: dispatching the release workflow must set its `prerelease` input to true (`.github/workflows/release.yml:5-11`), or GitHub publishes `0.1.0-pre.9` as a full release.

C3. No permanent test pins the version string (investigation section 3.2). No new test is added for the bump.

### D. Ponytail posture

Three files change: one statement-pair swap and two one-line data edits, plus one test. No new helper, no abstraction, no copy churn. Nothing speculative.

## Risk register

| Risk | Mitigation |
|---|---|
| Pair-per-pass test ceiling breaks under a future harness change | Doc comment names the ceiling; fallback assertion recorded in A3 |
| Keyboard tab order now reaches the transparent toggle first | Accepted consequence of the requested UX change; no focus-sensitive code exists in the file |
| The flip strays into the Settings submenu pair | The spec pins the exact block; the submenu pair is a non-goal |
| Bump date drifts from the commit date | B2 pins the header date to the bump date, 2026-10-20 |
| Release dispatch forgets the prerelease flag | Operational note in C2 |
| Release tooling picks up a mid-flip state | B3 and B4 make the bump the final commit |

## Review findings integrated

- 2026-10-20: five parallel adversarial reviews ran against the first draft. Accuracy found the A3 setup wording overstated the match with the sibling test, and the repaint attribution stated an inference as a recorded fact. Both are corrected in A3. Safety verified serialization, undo grouping, hook values, the version string consumers, and the tag step. It added the tab-order risk row and the prerelease dispatch note in C2. Architecture added the `finally` teardown, the falsifier marker, and the research assembly run in C1. Ponytail confirmed minimal scope and corrected the file count in D. It proposed promoting the fallback assertion to primary. The latent-bug review proved strict alternation deterministic in this harness, so alternation stays primary. That ruling is recorded here. The latent-bug review also confirmed the stateless drawer, the sequential test runner, the lock-file safety, and the absence of a README contradiction.
