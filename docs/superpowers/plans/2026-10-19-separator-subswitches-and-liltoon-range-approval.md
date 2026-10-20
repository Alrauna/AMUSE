# Separator Sub-Switches and lilToon Range Approval Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two inspector sub-switches below the Alpha Separator checkbox that gate the cutout and transparent arms, preset schema 5, and silent approval of every lilToon version below 2.5.

**Architecture:** The switches are serialized component policy, read at the barrier into the captured policy and baked into the prepared plan (scalar-knob pattern). The verdict ladder gains one policy outcome produced in the shared `DecideSlot`, so build and dry run cannot drift. Approval is a version-keyed predicate on the attestation class, admitted at the three version gates, paired to the nearest row digest, and routed only through the build's consent-owned capturer, never through the name set and never through the plain capturer the dry run uses.

**Tech Stack:** C# against Unity 2022.3 APIs. Two production assemblies (`Alrauna.Amuse.Editor`, `Alrauna.Amuse.Runtime`). NUnit through the Unity Test Framework, EditMode only.

**Spec:** `docs/superpowers/specs/2026-10-19-separator-subswitches-and-liltoon-range-approval-design.md` (decision IDs A1-A5, B1-B4, C1-C11, D1-D7). Evidence base: `docs/superpowers/investigations/2026-10-19-separator-subswitches-and-liltoon-range-approval-investigation.md`.

## Global Constraints

- Product paths below are relative to `Packages/com.alrauna.amuse/`.
- Tests run through the Unity Test Runner, EditMode mode, in the dev editor instance for this repository. Refresh assets and wait for compilation before running. A filtered run reporting 0 tests is a failure. Record observed counts. If the implementer has no editor access, the controller runs the stated filter and returns the counts.
- The spec's Landing Order groups are binding, and the task boundaries below encode them: Task 3 is schema group 1, Task 4 is enum-member group 2 (member, sentence, and fingerprints in one commit), Task 6 is approval group 3 (predicate, gates, pairing, routing, and every D6 retarget in one commit). Each task is exactly one commit.
- Never weaken a test. Re-pinning a behavior the spec changes is expected; weakening an assertion is halt point 1.
- Assertion style: `Assert.That(actual, Is.EqualTo(expected))`. Test classes: one per production type, `<Type>Tests`. Method names are behavior sentences.
- Simple english in every new comment: short active sentences, one idea per sentence, no semicolons, no contractions.
- No absolute paths, no machine identifiers, no host names anywhere in code, comments, or commit messages.

---

### Task 1: Runtime sub-switch fields

**Files:**
- Modify: `Runtime/AmuseAvatarOptimizer.cs` (fields after `:30-32`, properties near `:256-262`, `Reset()` `:226-247`, doc comment `:22-29`)
- Test: `Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`

**Interfaces:**
- Produces: `public bool CutoutSeparatorEnabled => _cutoutSeparatorEnabled;` and `public bool TransparentSeparatorEnabled => _transparentSeparatorEnabled;` on `AmuseAvatarOptimizer`, plus serialized fields `_cutoutSeparatorEnabled`, `_transparentSeparatorEnabled`. Public, matching `AlphaSeparatorEnabled` (`:256-262`), because the test assembly has no `InternalsVisibleTo` grant from the Runtime assembly. Every later task consumes these property names verbatim.

- [ ] **Step 1: Write the failing tests**

The test file creates components inline (`new GameObject` + `AddComponent<AmuseAvatarOptimizer>()` in try/finally, as `DefaultIgnoreOutOfRangeMaterialSlotsIsTrue` at `:35-53` does) and round trips through `EditorJsonUtility`. There is no helper method for either. Write, in that inline style:

```csharp
[Test]
public void DefaultSeparatorSubSwitchesAreTrue()
{
    var root = new GameObject("Root");
    try
    {
        var component = root.AddComponent<AmuseAvatarOptimizer>();
        Assert.That(component.CutoutSeparatorEnabled, Is.True);
        Assert.That(component.TransparentSeparatorEnabled, Is.True);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(root);
    }
}
```

For the legacy payload, follow the file's `EditorJsonUtility` round-trip pattern (see the comment convention at `:443-449`) but serialize a JSON body that contains only pre-existing keys, so the payload genuinely lacks the new fields:

```csharp
[Test]
public void LegacyPayloadWithoutSubSwitchFieldsDeserializesTrue()
{
    var root = new GameObject("Root");
    try
    {
        var component = root.AddComponent<AmuseAvatarOptimizer>();
        var legacyJson = "{\"_alphaSeparatorEnabled\": true}";
        EditorJsonUtility.FromJsonOverwrite(legacyJson, component);
        Assert.That(component.CutoutSeparatorEnabled, Is.True,
            "a payload that predates the fields must deserialize true");
        Assert.That(component.TransparentSeparatorEnabled, Is.True);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(root);
    }
}
```

Add the reset test in the same inline style, mutating both fields through a `SerializedObject` to false, calling `component.Reset()`, and asserting both properties true. Finally, extend the `SwitchFieldRows` TestCaseSource (`:72-105`) with both new field names, so the existing round-trip row test covers them.

- [ ] **Step 2: Run the tests, observe RED**

Run the EditMode filter `Alrauna.Amuse.Tests.Editor.AmuseAvatarOptimizerTests`. Expected: compile failure (the properties do not exist). Compile failure is the RED state for this task.

- [ ] **Step 3: Implement**

In `Runtime/AmuseAvatarOptimizer.cs`:

1. After `_alphaSeparatorEnabled` (`:30-32`), add the two fields with the doc comments from spec A1 verbatim.
2. In `Reset()` (`:226-247`), after the `_alphaSeparatorEnabled = true;` line, add `_cutoutSeparatorEnabled = true;` and `_transparentSeparatorEnabled = true;`.
3. Beside the `AlphaSeparatorEnabled` property (`:256-262`), add the two public expression-bodied properties, each doc-commented in the same style with the default-true upgrade rule from spec A1.
4. Update the class doc comment and `_alphaSeparatorEnabled`'s doc comment (`:22-29`) per spec A4: the promised later switches now exist, draw below the master switch, indented, visible only when it is on.

- [ ] **Step 4: Run the tests, observe GREEN**

Same filter. Expected: all tests pass, including the pre-existing backfill pins (`:338-434`) untouched.

- [ ] **Step 5: Commit**

```bash
git add Runtime/AmuseAvatarOptimizer.cs Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs
git commit -m "feat(runtime): cutout and transparent separator sub-switches"
```

---

### Task 2: Inspector pair

**Files:**
- Modify: `Editor/AmuseAvatarOptimizerEditor.cs` (draw site after `:167-175`, hooks beside `:55-63`, master tooltip `:170-174`)
- Test: `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**
- Consumes: the Task 1 properties and serialized names.
- Produces: `internal static Action<bool> OnCutoutSeparatorToggleDrawnForTest` and `OnTransparentSeparatorToggleDrawnForTest` on the editor class. No other task consumes them.

- [ ] **Step 1: Write the failing tests**

The capture pattern is `[UnityTest] public IEnumerator` driving `yield return RepaintThroughHostWindow(...)` (the out-of-range toggle test at `:189-221` is the model; `SetSwitch` at `:1042-1048`; hook assign, assert, null in `finally`). Known limit, recorded: `SetSwitch` applies modified properties, so the C# property and the serialized value agree in every state these tests arrange. The spec's gate-source rule (serialized property, not the C# property) is enforced by transcribing the spec A3 code block verbatim; no test can discriminate the one-repaint lag without synthesizing a click through the drawn row, which this task does not do.

```csharp
[UnityTest]
public IEnumerator SubSwitchHooksFireWithStoredValuesWhenMasterSwitchIsOn()
{
    // SetSwitch the component: master on, cutout on, transparent off.
    // Assign both hooks to record (value, EditorGUI.indentLevel) tuples.
    // yield return RepaintThroughHostWindow(editor).
    // Assert: cutout hook fired once with (true, 1), transparent once
    // with (false, 1). Null both hooks in finally.
}

[UnityTest]
public IEnumerator SubSwitchHooksDoNotFireWhenMasterSwitchIsOff()
{
    // SetSwitch master off.
    // Assert: neither hook fired; indent recording unused.
}
```

The indent value 1 in the first test is the spec C11.2 draw-depth pin: the hooks fire inside the gated block, one level below the ambient depth.

- [ ] **Step 2: Run the tests, observe RED**

Run the EditMode filter `Alrauna.Amuse.Tests.Editor.AmuseAvatarOptimizerEditorTests`. Expected: compile failure (hooks do not exist).

- [ ] **Step 3: Implement**

1. Beside the hook declarations (`:55-63`), add the two `internal static Action<bool>` delegates.
2. Rewrite the master-switch block (`:167-175`) by transcribing the spec A3 code block verbatim: hoisted property local, `ApplyModifiedProperties`, gate on `masterSwitch.boolValue`, `IndentLevelScope`, both `PropertyField` draws with the pinned tooltips, hooks fired with the stored values, `ApplyModifiedProperties` inside the block.
3. Update the master switch tooltip copy per spec A4.

- [ ] **Step 4: Run the tests, observe GREEN**

Same filter, full class. Expected: all pass, including the pre-existing header assertions (`:131-180`) and label-width tests (`:982-1006`).

- [ ] **Step 5: Commit**

```bash
git add Editor/AmuseAvatarOptimizerEditor.cs Tests/Editor/AmuseAvatarOptimizerEditorTests.cs
git commit -m "feat(editor): separator sub-switch checkboxes under the master switch"
```

---

### Task 3: Presets, schema 5 (atomic)

**Files:**
- Modify: `Editor/Presets/PresetParser.cs` (`:18`, `:23`, `:104-106` area)
- Modify: `Editor/Presets/OptimizerPreset.cs` (`:10-28`, `Matches` `:69-102`, `Matches` doc comment `:69-72`)
- Modify: `Editor/Presets/PresetApplier.cs` (`:19-20` area, doc comment `:8-11`)
- Modify: `Presets/safe.json`, `Presets/normal.json`, `Presets/aggressive.json` (features block, `alphaSeparator` sits at line 6)
- Test: `Tests/Editor/Presets/OptimizerPresetTests.cs`, `Tests/Editor/Presets/PresetApplierTests.cs`, `Tests/Editor/Presets/PresetParserTests.cs`, `Tests/Editor/Presets/PresetFileStoreTests.cs`

**Interfaces:**
- Consumes: the two component properties (Task 1).
- Produces: `OptimizerPreset.CutoutSeparatorEnabled` and `.TransparentSeparatorEnabled` members, matched and applied like `AlphaSeparatorEnabled`. Schema 5. Shipped JSON keys exactly `"cutoutSeparator"` and `"transparentSeparator"`.

- [ ] **Step 1: Update the pins first (RED)**

1. `OptimizerPresetTests.cs`: `ShippedDefaults()` (`:10-16`) gains the two members as `true`; the flip table (`:59-77`) gains two rows flipping each new member.
2. `PresetApplierTests.cs`: rename `ApplyWritesAllSixteenFields` (`:175`) to `ApplyWritesAllEighteenFields` and extend fixture and assertion.
3. `PresetParserTests.cs`: `ValidSchemaParsesAllSixteenValues` (`:99`) renames to `...AllEighteenValues` and its JSON gains both keys; `ValidJson` gains both lines; the future-schema needle (`:160-161`, pinning refusal of schema 5) moves to 6. The legacy-body builders (`LegacyVersionOneBody`/`TwoBody`/`ThreeBody`, `:42-83`) need no change: they strip only settings keys, and the schema gate at `:81-86` refuses before any key check, so legacy bodies carrying the new keys still refuse with `UnknownSchemaVersion`.
4. `PresetFileStoreTests.cs`: `ValidBody` (`:12`) gains both keys.

Run the four Presets test classes. Expected: compile or assertion failures, because production does not know the keys yet. This is RED.

- [ ] **Step 2: Implement (one commit)**

1. `PresetParser.cs`: `CurrentSchemaVersion = 5` (`:18`); `FeatureKeys` becomes `{ "alphaSeparator", "cutoutSeparator", "transparentSeparator" }` (`:23`); two `TryRead<bool>` calls beside the `"alphaSeparator"` read (`:104-106` pattern), so a missing key refuses `MissingField` and an unknown key refuses through `EnsureClosedKeySet`.
2. `OptimizerPreset.cs`: record gains both members beside `AlphaSeparatorEnabled` (`:10-28`); `Matches` gains both terms (`:69-102`); the `Matches` doc comment's "sixteen mapped fields" wording becomes eighteen.
3. `PresetApplier.cs`: write both switches directly after the master switch write (`:19-20`); doc comment says eighteen mapped fields.
4. All three shipped JSONs gain `"cutoutSeparator": true,` and `"transparentSeparator": true,` in the `features` block.

- [ ] **Step 3: Run, observe GREEN**

Run the four Presets test classes, then a `Tests/Editor/Presets` folder filter. Expected: all pass with observed counts.

- [ ] **Step 4: Commit**

```bash
git add Editor/Presets Presets Tests/Editor/Presets
git commit -m "feat(presets): separator sub-switches join presets at schema 5"
```

---

### Task 4: Ladder outcome, dry-run sentence, and fingerprints (atomic, Landing Order group 2)

**Files:**
- Modify: `Editor/Analysis/SlotTransformDecision.cs` (enum `:12-20`, ladder comment `:53-67`, `Decide` `:69+`, `CoverageVectorsBySlot` `:157-192`, `DecideSlot` `:262-317`)
- Modify: `Editor/DryRun/AlphaSeparationDryRun.cs` (the only production `Decide` call, `:800`; captured knob fields and `BuildPolicyFingerprint` `:1379-1401`)
- Modify: `Editor/DryRun/DryRunRowBuilder.cs` (`SlotSentence` `:138-163`)
- Modify: `Editor/DryRun/MaterialTransformationRow.cs` (`RowText`, beside `:216-240`)
- Modify: `Editor/Build/PlayModeBuildStore.cs` (`ComputePolicyFingerprint` `:50-73`)
- Test: `Tests/Editor/Analysis/SlotTransformDecisionTests.cs`, `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`, `Tests/Editor/Build/PlayModeBuildStoreTests.cs`

**Interfaces:**
- Produces: `SlotTransformOutcome.SeparatorOffByPolicy`; `Decide(..., IReadOnlyList<bool> alreadyOpaqueBySlot, IReadOnlyList<bool> separatorOffBySlot = null, ...)` with the new optional parameter placed directly after `alreadyOpaqueBySlot` (optional, so the ten existing positional call sites in `SlotTransformDecisionTests.cs` compile untouched); static helper `SlotTransformDecision.SeparatorOffBySlot(IReadOnlyList<bool> cutoutMembershipBySlot, bool cutoutSeparatorEnabled, bool transparentSeparatorEnabled)` returning `bool[]`; dry-run sentence "Skipped: the matching separator switch is off."; both fingerprints sensitive to both switches.

- [ ] **Step 1: Write the failing tests**

In `Tests/Editor/Analysis/SlotTransformDecisionTests.cs`, reusing the file's plan and result builders, and passing the new argument explicitly:

```csharp
[Test]
public void SeparatorOffSlotYieldsSeparatorOffByPolicyAheadOfOtherOutcomes()
{
    // One slot with a Split submesh whose coverage is below the
    // minimum, separatorOffBySlot[slot] = true.
    // Assert: SeparatorOffByPolicy, not CoverageBelowMinimum.
}

[Test]
public void FoldArmOffSlotYieldsSeparatorOffByPolicy()
{
    // A preserve-qualifying slot with the arm off.
    // Assert: SeparatorOffByPolicy, not Fold.
}

[Test]
public void AlreadyOpaqueWinsOverSeparatorOff()
{
    // Both flags true for the slot.
    // Assert: AlreadyOpaque.
}

[Test]
public void SeparatorOffVectorEqualToNullBaselineChangesNoOtherOutcome()
{
    // The file's existing verdict table re-run twice: once with the
    // parameter omitted, once with an all-false array.
    // Assert: outcomes identical between the two runs.
}

[Test]
public void UnchangedSlotOutcomeMatchesItsBaselineUnderSeparatorOff()
{
    // A slot whose every submesh is Unchanged, arm off.
    // Capture the verdicts with the vector all-false, then with the
    // slot's arm true.
    // Assert: identical outcomes for that slot between the runs.
}
```

In `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`: a run with a cutout-arm slot and Cutout Separator off renders "Skipped: the matching separator switch is off." for that slot (this is also the case-removal guard: the `default:` arm would render the Untouchable sentence instead, so the test fails if the case is deleted). In `Tests/Editor/Build/PlayModeBuildStoreTests.cs`: extend `ComputePolicyFingerprintProducesConsistentString` (`:59-73`) to expect 20 `;`-separated terms (was 18), and extend the sensitivity test (`:92-97` pattern) with each new switch. In `Tests/Editor/DryRun/DryRunCacheTests.cs` the fingerprint harness reflects `BuildPolicyFingerprint` over a throwaway component with no term-count pin, so the new terms flow in; extend `FingerprintChangesWhenACutoutValueChanges` (`:115-131`) pattern with a switch case.

- [ ] **Step 2: Run, observe RED**

Run `Alrauna.Amuse.Tests.Editor.Analysis.SlotTransformDecisionTests`, then the dry-run and play-mode store classes. Expected: compile failures (member, parameter, and sentence do not exist).

- [ ] **Step 3: Implement**

1. `SlotTransformDecision.cs`: add `SeparatorOffByPolicy` to the enum with the doc comment "A slot the owner excluded by a separator sub-switch. A policy fact, not a proof fact. Ranked after AlreadyOpaque." Extend the pinned ladder comment (`:53-67`) with: "SeparatorOffByPolicy sits after AlreadyOpaque and before the preserve fold for slots with a candidate plan entry. An unchanged slot never reports it." Add the optional parameter to `Decide` and `DecideSlot`, placed after `alreadyOpaqueBySlot`. In `DecideSlot`, between the `AlreadyOpaque` branch and the `Fold` branch, insert:

```csharp
else if (hasEntries &&
         !everySubmeshUnchanged &&
         separatorOffBySlot != null &&
         slotIndex < separatorOffBySlot.Count &&
         separatorOffBySlot[slotIndex])
{
    outcome = SlotTransformOutcome.SeparatorOffByPolicy;
}
```

2. Add the `SeparatorOffBySlot` helper from the spec C4 shape:

```csharp
/// <summary>
/// Whether each slot's arm is disabled by the separator sub-switches.
/// A slot whose current material declares cutoff follows the Cutout
/// Separator. Every other slot follows the Transparent Separator.
/// Both switches on answers false everywhere.
/// </summary>
internal static bool[] SeparatorOffBySlot(
    IReadOnlyList<bool> cutoutMembershipBySlot,
    bool cutoutSeparatorEnabled,
    bool transparentSeparatorEnabled)
{
    if (cutoutMembershipBySlot == null)
    {
        throw new ArgumentNullException(nameof(cutoutMembershipBySlot));
    }

    var separatorOff = new bool[cutoutMembershipBySlot.Count];
    for (var slot = 0; slot < separatorOff.Length; slot++)
    {
        separatorOff[slot] = cutoutMembershipBySlot[slot]
            ? !cutoutSeparatorEnabled
            : !transparentSeparatorEnabled;
    }

    return separatorOff;
}
```

3. `MaterialTransformationRow.cs`: add the sentence constant in `RowText`. `DryRunRowBuilder.cs`: add the hand-written case in `SlotSentence`:

```csharp
case SlotTransformOutcome.SeparatorOffByPolicy:
    return "Skipped: the matching separator switch is off.";
```

4. Dry-run value path: the dry-run call site reads pre-captured knob fields, not the component. Add two captured bool fields beside the existing knob captures, populated where `_cutoutPreserveCoverage` is set, from the component's two properties. Build `separatorOffBySlot` with the helper from `cutoutMembershipBySlot` and the two fields, and pass it to `Decide` at `:800`.
5. Fingerprints: `BuildPolicyFingerprint` (`:1379-1401`) and `ComputePolicyFingerprint` (`:50-73`) each gain the two switch values as terms in the same style as the master switch term (`:1382-1383`, `:57`). Update the `DryRunFingerprintInputs.PolicyFingerprint` doc comment's stale component-count wording to the new count.

- [ ] **Step 4: Run, observe GREEN**

Run `Tests/Editor/Analysis`, `Tests/Editor/DryRun`, and `Tests/Editor/Build` folder filters. Expected: all green with the new tests passing. The build-side `Prepare` path does not consume the vector yet; that lands in Task 5, and both switches on behave identically to base in the meantime.

- [ ] **Step 5: Commit**

```bash
git add Editor/Analysis/SlotTransformDecision.cs Editor/DryRun Editor/Build/PlayModeBuildStore.cs Tests/Editor/Analysis Tests/Editor/DryRun Tests/Editor/Build
git commit -m "feat(analysis): separator-off outcome with sentence and fingerprint terms"
```

---

### Task 5: Build refusal and preparation wiring

**Files:**
- Modify: `Editor/Build/AlphaSeparationRecords.cs` (refusal enum `:23-150`, after `AnisotropyDifferences` `:84-93`)
- Modify: `Editor/Build/AmuseReportStrings.cs` (beside `:564-580`)
- Modify: `Editor/Build/AlphaSeparationPreparation.cs` (`Prepare` signature `:105-119`, fold gate `:146-169`, `reportedAnisotropySlots` `:186`, the three gates `:299/:325/:336`, `RefuseEveryCandidateSlot` definition `:783-810`, candidate loop `:379+` with the Unchanged `continue` `:381-388` and the existing `var slotIndex` at `:427`)
- Modify: `Editor/Build/AmusePlatformFinishPass.cs` (`RetainPreparedSeparation` signature `:1051-1083`, the `Prepare` call at `:1068`; the vector is built beside `CoverageVectorsBySlot` at `:771` and threaded through)
- Test: `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`; the completeness test `Tests/Editor/Build/AmuseReportsSlotTests.cs:423-447` (`EverySlotSeparationCauseHasPlainEnglishStrings`) is satisfied in-commit by the member plus its three strings and must stay green

**Interfaces:**
- Consumes: `SeparatorOffBySlot` and the component properties (Tasks 1 and 4).
- Produces: `AlphaSeparationSlotRefusal.SeparatorOffByPolicy`; `Prepare` and `RetainPreparedSeparation` gain an optional `bool[] separatorOffBySlot = null` parameter (optional, because `cutoutMembershipBySlot = null` is already optional and a required parameter may not follow it). The report keys `amuse.slotSeparation.SeparatorOffByPolicy`, `.description`, `.hint`.

- [ ] **Step 1: Write the failing tests**

In `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`, reusing the file's fixture builders (the split-size tests near `:4929-4986` and the anisotropy tests show the machinery):

```csharp
[Test]
public void DisabledCutoutArmRecordsSeparatorOffRefusalForCutoutSlots()
{
    // One cutout slot and one transparent slot, both Split candidates.
    // Cutout Separator off, Transparent on.
    // Assert: the cutout slot is absent from the prepared set and the
    // state carries SeparatorOffByPolicy for it. The transparent slot
    // prepares exactly as with both on.
}

[Test]
public void DisabledTransparentArmMirrorsTheCutoutBehavior() { ... }

[Test]
public void DisabledArmSkipsThePreserveFoldDisclosure()
{
    // A preserve-qualifying cutout slot with Cutout Separator off.
    // Assert: no ForcedOpaque* disclosure fires and the slot records
    // SeparatorOffByPolicy.
}

[Test]
public void AdditiveLayerGateSparesDisabledArmSlots()
{
    // Conversion-state additive bindings whose candidate slots are all
    // in a disabled arm.
    // Assert: those slots record SeparatorOffByPolicy. Enabled-arm
    // candidates, if any, receive the additive refusal.
}

[Test]
public void BothSwitchesOnReproducesBasePreparation()
{
    // The file's existing preparation table re-run with both on.
    // Assert: identical prepared plans and refusals.
}
```

- [ ] **Step 2: Run, observe RED**

Run `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationPreparationTests`. Expected: compile failure (the enum member and parameter do not exist).

- [ ] **Step 3: Implement**

1. `AlphaSeparationRecords.cs`: add `SeparatorOffByPolicy` after `AnisotropyDifferences` with the doc comment "A policy refusal, not a proof fact. The slot's arm is disabled by a separator sub-switch, so it never prepared."
2. `AmuseReportStrings.cs`: the three strings per spec C7, verbatim: title and description "The matching separator switch is off, so this slot did not separate." and hint "Enable 'Cutout Separator' or 'Transparent Separator' on the AMUSE component.", following the `OpaqueCoverageBelowMinimum` key structure (`:564-577`). The generic census and key building (`SlotSeparationKey` at `:841-844`) pick the member up without further change.
3. `AlphaSeparationPreparation.cs`: `Prepare` gains `bool[] separatorOffBySlot = null` as the parameter directly after `cutoutMembershipBySlot`, null-checked inside. Then:
   - Fold gate loop (`:146-169`): skip a slot group when the vector says its arm is off, before `PreserveSlotQualifies`. No fold entry, no disclosure count.
   - The anisotropy scan (`:172-206`) needs no exclusion: it reports only `Unchanged`-disposition slots (`:222-241`), so disabled-arm candidates never reach it, and unchanged slots keep their base reporting.
   - `RefuseEveryCandidateSlot` (`:783-810`) gains the vector parameter. It records `SeparatorOffByPolicy` for disabled-arm candidate slots and the original refusal only for enabled-arm candidates. The three call sites (`:299`, `:325`, `:336`) pass it.
   - Candidate loop: declare `var reportedSeparatorOffSlots = new HashSet<int>();` beside `reportedAnisotropySlots` (`:186`). After the Unchanged `continue` and before the coverage gate, insert:

```csharp
var slotIndex = submesh.SourceMaterialBindingIndex;
if (separatorOffBySlot != null &&
    slotIndex < separatorOffBySlot.Length &&
    separatorOffBySlot[slotIndex])
{
    if (reportedSeparatorOffSlots.Add(slotIndex))
    {
        AmuseReports.RecordRefusal(
            state,
            target.Renderer,
            slotIndex,
            AlphaSeparationSlotRefusal.SeparatorOffByPolicy);
    }

    continue;
}
```

   and remove the now-duplicate `var slotIndex = ...` declaration that follows at `:427`.

4. `AmusePlatformFinishPass.cs`: build the vector with the helper from the component's two properties (read beside the existing policy reads, `:406-461` area) and `cutoutMembershipBySlot`, and thread it through `RetainPreparedSeparation` (`:1051-1083`, optional parameter at the same position) into the `Prepare` call at `:1068`.

- [ ] **Step 4: Run, observe GREEN**

Run a `Tests/Editor/Build` folder filter. Expected: all green, new tests passing, `EverySlotSeparationCauseHasPlainEnglishStrings` green, and both-switches-on behavior identical to base.

- [ ] **Step 5: Commit**

```bash
git add Editor/Build Tests/Editor/Build
git commit -m "feat(build): separator sub-switches suppress their arm at prepare"
```

---

### Task 6: Summary disclosure

**Files:**
- Modify: `Editor/Build/AmusePlatformFinishPlugin.cs` (build-state fields `:43-61`; `ReachedRendererAnalysis` already exists at `:27` and stays the reach flag)
- Modify: `Editor/Build/AmusePlatformFinishPass.cs` (capture the two component values at the barrier policy capture, `:406-461` area)
- Modify: `Editor/Build/AmuseReports.cs` (`AvatarSummary` `:716-782`, `PolicyActive` plumbing `:744-766`)
- Modify: `Editor/Build/AmuseReportStrings.cs` (beside `amuse.summary.PolicyActive` `:407-419`)
- Modify: `Editor/Build/AlphaSeparationApply.cs` (the sole production `AvatarSummary` caller, `:90-98`, passes the new fact)
- Test: `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (about ten existing `AvatarSummary` call sites at `:1165-1415` and `:6748` gain the new argument), `Tests/Editor/Build/AmuseReportStringsTests.cs`

**Interfaces:**
- Consumes: the component properties (Task 1).
- Produces: one build-state bool pair (`CutoutSeparatorOff`, `TransparentSeparatorOff`; no third flag, because `ReachedRendererAnalysis` already encodes reach) and the summary key `amuse.summary.SeparatorSwitchOff` with description "Some slots did not separate because a separator switch is off."

- [ ] **Step 1: Write the failing tests**

In the summary test home (`AmusePlatformFinishPluginTests.cs`, where the existing `AvatarSummary` pins live):

```csharp
[Test]
public void SummaryNamesDisabledSeparatorSwitchesWhenAnalysisWasReached()
{
    // AvatarSummary with ReachedRendererAnalysis true and
    // CutoutSeparatorOff true.
    // Assert: the output contains the new sentence and does not gain
    // the PolicyActive tolerance sentence from this fact alone.
}

[Test]
public void SummaryStaysSilentAboutSeparatorSwitchesWhenBothAreOn()
{
    // ReachedRendererAnalysis true, both switch bools false.
    // Assert: no separator sentence.
}

[Test]
public void SummaryOmitsSeparatorSentenceWhenAnalysisWasNotReached()
{
    // ReachedRendererAnalysis false, a switch off.
    // Assert: no separator sentence.
}
```

Update the ten existing `AvatarSummary` call sites in the file for the new argument (mechanical, compile-driven).

- [ ] **Step 2: Run, observe RED**

Run `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests`. Expected: compile failure (the new argument does not exist).

- [ ] **Step 3: Implement**

1. Build state gains `CutoutSeparatorOff` and `TransparentSeparatorOff`, captured at the barrier policy capture. `AlphaPolicyActive` (`:435-438`) is untouched: it stays the two tolerance terms.
2. `AmuseReportStrings.cs`: key `amuse.summary.SeparatorSwitchOff`, description "Some slots did not separate because a separator switch is off.", following the `PolicyActive` structure (`:407-419`).
3. `AmuseReports.AvatarSummary` gains one bool parameter (`anySeparatorSwitchOff`) and appends the new sentence when it is true. The reach gating stays with the existing caller condition.
4. `AlphaSeparationApply.cs` (`:90-98`) passes `state.CutoutSeparatorOff || state.TransparentSeparatorOff`.

- [ ] **Step 4: Run, observe GREEN**

Run `Tests/Editor/Build` folder filter. Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add Editor/Build Tests/Editor/Build
git commit -m "feat(reports): summary names builds a separator switch silenced"
```

---

### Task 7: Approval group (atomic, Landing Order group 3)

Everything in this task is one commit: predicate, gates, pairing, routing, Multi version carriage, the dry-run gate, and every test retarget.

**Files:**
- Modify: `Editor/Semantics/LilToon/LilToonSourceAttestation.cs` (constants near `:362-364`, accessors `:423-457`, `Verify` gate `:2039-2046`, `TryVerifyIncludeTreeForVersion` `:2144-2178` (no change, verify by test), `TryVerifyMultiContainer` `:2231-2243`, `TryResolveMultiRowDigest` `:2390-2395`, `TryMultiColumnDigest` `:2499-2530`)
- Modify: `Editor/Semantics/LilToon/LilToonMultiResolution.cs` (the refused record carries the captured package version; `:447-451` area)
- Modify: `Editor/Semantics/UnityMaterialSemantics.cs` (`CollectTransferConsent` `:477-534` plus a new internal core overload, `TryCaptureClosedAlphaMaterialsTransferred` `:359-383`, the plain wrapper `:288-306`)
- Modify: `Editor/Build/AmusePlatformFinishPass.cs` (`:318-330`, `:375`, `:518-536`, `:611-614`)
- Modify: `Editor/Build/AlphaSeparationMaterialConverter.cs` (`:130-133`, `:375-386`)
- Test: `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`, `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs`, `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`, `Tests/Editor/Build/VersionConsentTests.cs`, `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`, `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `LilToonSourceAttestation.ApprovedVersionFloor`, `ApprovedVersionUpperBound`, `IsApprovedPackageVersion(string)`, `IsApprovedPackageEvidence(bool hasPackage, string packageVersion)`; `CollectTransferConsent` returns `(Subjects, GrantedShaderNames, AnyApprovedVersions)` with an internal core overload taking index-aligned captured materials; `TryCaptureClosedAlphaMaterialsTransferred` gains `bool admitApprovedVersions = false`; refused Multi records carry `CapturedHasPackage`/`CapturedPackageVersion`; the converter boundary accepts approved evidence.

- [ ] **Step 1: Write the failing tests and the retargets**

Attestation half, in `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`:

1. Retarget `Verify_WrongPackageVersion_IsRefused` (`:1802-1808`) and `VerifyCutout_WrongPackageVersion_IsRefused` (`:2564-2575`) from `"2.3.5"` to `"2.2.0"`, and add `"2.5.0"` cases. These stay green under old code (out-of-range refusal is base behavior) and pin the new floor and bound edges. Rewrite the body comment of `Verify_NonAdmittedVersionForms_Refuse` (`:1813-1817`), which says "The comparison is ordinal": the new text states that non-canonical and out-of-range forms refuse.
2. `Verify_NonAdmittedVersionForms_Refuse` (`:1809-1826`): remove `"2.4.1"` and `"2.3.10"`; keep `"2.5.0"`, `"2.4"`, `"02.4.0"`.
3. New tests, in the file's evidence-fixture style (`Evidence(packageVersion:)`):

```csharp
[TestCase("2.4.1")]
[TestCase("2.4.9")]
public void ApprovedRangePatchWithTheNearestRowTreeVerifies(string version)
{
    // Evidence with the 2.4.0 gathered tree and the given version.
    // Assert: TryVerifyLilToonIdentity true.
}

[Test]
public void ApprovedRangeVersionWithAForeignTreeReportsModifiedSource()
{
    // version "2.4.1" over the 2.3.4 tree.
    // Assert: false, LilToonSemanticDiagnosticCode.ModifiedShaderSource.
}

[Test]
public void TwoPointThreeTenPairsTheTwoPointThreeFourRow()
{
    // "2.3.10" with the 2.3.4 tree verifies. With the 2.3.1 shared
    // tree it refuses ModifiedShaderSource. This is the numeric
    // comparison pin, because ordinal order would pair 2.3.1.
}

[TestCase("2.10.0")]
[TestCase("2.3.100")]
[TestCase("2.4.4294967300")]
public void VersionsTheRangeCannotContainStayUnapproved(string version)
{
    // The first two are numerically outside. The third overflows a
    // naive parser into range. All three answer false from
    // IsApprovedPackageVersion and refuse UnsupportedVersion.
}

[TestCase("2.4.0.1")]
[TestCase("02.4.0")]
[TestCase(" 2.4.0 ")]
[TestCase("2.4.0-preview")]
public void NonCanonicalFormsStayUnapproved(string version)
{
    // Assert: IsApprovedPackageVersion false; identity refuses.
}

[Test]
public void ApprovalGrammarMatchesTheHostGrammarOnSharedCanonicalForms()
{
    // Drive HostLifecycleCapability.TryParsePackageVersion by
    // reflection, the house pattern for private members, over a shared
    // table of canonical M.m.p strings. Assert: parse success agrees
    // with the attestation parser on every canonical form. The
    // attestation parser is stricter by design on non-canonical forms.
}
```

4. `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs`: the Multi fixtures pinned to `"2.3.5"` refusing `AttestationFailed` (`:470-484`) gain in-range twins: `"2.3.10"` with the `2.3.4` Multi columns verifies, cross-version column pairings still refuse. `Verify_AcceptsAdmittedPatchVersionWithItsOwnTree` and `Verify_VersionWithAnotherVersionsTree_IsModifiedSource` (`LilToonAttestationTests.cs:2595-2639`) gain in-range non-row twins.

Routing half:

5. `UnityMaterialSemanticsTests.cs`: add a core-overload test family. Because the production capture derives `PackageVersion` from the installed package, the version-shaped subject cases run against the new internal core with fabricated captured materials (the file's existing fixtures, like `ConsentSubjectForRefusedMultiMaterialNamesTheRefusal` at `:864-876`, show the construction pattern):

```csharp
[Test]
public void ApprovedVersionProducesNoSubjectOnTheConsentCore()
{
    // A lilToon-family captured material whose LilToonEvidence carries
    // HasPackage true and PackageVersion "2.4.1", identity failing.
    // Assert: zero subjects, AnyApprovedVersions true.
}

[Test]
public void ApprovedByteChangedMultiProducesNoSubject()
{
    // A Multi captured material whose resolution refused
    // AttestationFailed and whose captured package version is "2.4.1".
    // Assert: zero subjects, AnyApprovedVersions true.
}

[Test]
public void InRangeMultiStateRefusalStillProducesItsSubject()
{
    // A Multi refused on a state word, captured version "2.4.0".
    // Assert: one subject naming the refusal words.
}

[Test]
public void LooseInstallStillProducesASubject()
{
    // HasPackage false. Assert: one subject, AnyApprovedVersions false.
}

[Test]
public void OutOfRangeVersionStillProducesASubject()
{
    // "2.5.0", unattested. Assert: one subject.
}

[Test]
public void ApprovedAndUnapprovedSameNamedMaterialsProduceOnlyTheOutOfRangeSubject()
{
    // An approved "2.4.1" material and a same-named "2.5.0" material.
    // Assert: exactly one subject, for the 2.5.0 material.
}

[Test]
public void ApprovedOnlyBuildGrantsNoShaderNames()
{
    // An approved material alone.
    // Assert: GrantedShaderNames empty, AnyApprovedVersions true.
}
```

6. Pipeline tests: a full-pipeline case with a byte-changed in-range install through the verified seams (`Tests/Editor/Build/VerifiedLilToonTestSeams.cs` shapes the vendor evidence; set its package version to `"2.4.1"` and its digests to mismatch): zero subjects, no presenter call, slots separate, no `SourceIdentity` refusal. A dry-run case with the same fixture: rows stay unknown (the dry run's plain capturer must not admit the material). A build case for a byte-changed in-range Multi: zero subjects and `UnsupportedMultiState` slot rows.
7. The lilToon stand-in subject fixtures that exercise the version path through the seams (the single-subject tests near `UnityMaterialSemanticsTests.cs:862-877`, the D8 pre-scan fixture near `AlphaSeparationPreparationTests.cs:3097-3110`, and the plugin's swap-consent subject tests) re-pin to `"2.5.0"` so the subject path keeps a versioned witness. The Poiyomi zero-subject pins (`PoiyomiMaterials_NeverProduceTransferConsentSubjects`, `Legacy90Material_ProducesZeroTransferConsentSubjects`, `UnverifiedPoiyomi_ProducesZeroTransferConsentSubjects`) and the explanatory comment at `AlphaSeparationPreparationTests.cs:3028-3032` stay untouched: they pin family confinement, which this change must not move. `BatchModeRefusesWithoutAsking` (`VersionConsentTests.cs:190-208`) stays untouched.

- [ ] **Step 2: Run, observe RED**

Run the touched test classes. Expected: the new approve-path tests fail (the predicate, pairing, and routing do not exist) and the fabricating tests fail to compile. The retargeted refuse pins and the re-pinned `"2.5.0"` subject fixtures stay green through this step; they are regression guards, not red drivers.

- [ ] **Step 3: Implement (one commit)**

1. `LilToonSourceAttestation.cs`:
   - Constants and `IsApprovedPackageVersion` per spec D1 with the spec's doc comment. The strict canonical parser per spec D1 with two corrections the reviews required: the digit-run guard is `digits > 9` (nine digits, at most 999,999,999, cannot overflow an int), and a component longer than one digit may not start with `'0'`. Comparison is the numeric triple against the parsed floor and bound, never ordinal.
   - `internal static bool IsApprovedPackageEvidence(bool hasPackage, string packageVersion)` beside it: `hasPackage && IsApprovedPackageVersion(packageVersion)`. All three routing sites call this one helper.
   - Private `NearestAdmittedRow` over the regular rows and the same shape over `AdmittedMultiVersions`, both numeric-triple scans. `IncludeDigestForVersion` (`:444-457`) falls back to the nearest regular row when approved; `TryMultiColumnDigest` (`:2499-2530`) falls back over the Multi rows when approved.
   - The three gates gain the disjunct `IsAdmittedPackageVersion(v) || IsApprovedPackageVersion(v)`: `Verify` (`:2039-2046`), `TryVerifyMultiContainer` step 3 (`:2231-2243`), `TryResolveMultiRowDigest` (`:2390-2395`). `IsAdmittedPackageVersion`, the rows, and every digest constant are unchanged. `TryVerifyIncludeTreeForVersion` needs no change; its packaged path reads the now-pairing `IncludeDigestForVersion`.
2. `LilToonMultiResolution.cs`: the refused Multi record gains `CapturedHasPackage` and `CapturedPackageVersion`, populated at the resolution point from the gather that `TryResolveMultiContainer` currently discards on failure (`LilToonSourceAttestation.cs:2355-2371`), without touching `IsResolved`'s definition. Resolved records carry the same values from their own evidence.
3. `UnityMaterialSemantics.cs`:
   - `CollectTransferConsent` (`:477-534`) becomes a thin wrapper that captures and delegates to a new internal core `CollectTransferConsentCore(IReadOnlyList<Material> materials, IReadOnlyList<CapturedAlphaMaterial> captured)`, returning the three-tuple. The suppression in the core, after `IsAttestedAlphaMaterial` fails:

```csharp
var approved = captured.Family == CapturedAlphaMaterialFamily.LilToonMulti
    ? captured.MultiResolution != null &&
      captured.MultiResolution.CapturedHasPackage &&
      LilToonSourceAttestation.IsApprovedPackageVersion(
          captured.MultiResolution.CapturedPackageVersion)
    : captured.LilToonEvidence != null &&
      LilToonSourceAttestation.IsApprovedPackageEvidence(
          captured.LilToonEvidence.HasPackage,
          captured.LilToonEvidence.PackageVersion);
var identityRefused = captured.MultiResolution == null ||
    captured.MultiResolution.Refusal ==
        LilToonMultiResolutionRefusal.AttestationFailed;
if (approved && identityRefused)
{
    anyApproved = true;
    continue;
}
```

   The Multi state-refusal branch (`:515-524`) stays exactly as is. `names.Add` stays only on subject paths, so `GrantedShaderNames` is never populated by approval.
   - `TryCaptureClosedAlphaMaterialsTransferred` (`:359-383`) gains `bool admitApprovedVersions = false`. When true, after the granted-name check fails, admit a member whose evidence is approved, reading `LilToonEvidence` for regular families and `MultiResolution.CapturedPackageVersion` for refused Multi members, so a byte-changed in-range Multi reaches the refused-Multi transport and reports `UnsupportedMultiState` slot rows. The plain wrapper `TryCaptureClosedAlphaMaterials` (`:288-306`) passes false, so the dry run (`DefaultInnerCapturer`, `AlphaSeparationDryRun.cs:1027-1040`), the build's no-consent fallback, and the host capture default keep strict attestation.
4. `AmusePlatformFinishPass.cs`: the capturer-swap condition (`:375`) becomes `grantedShaderNames != null || shaderTransfer.AnyApprovedVersions`; the `effectiveCapturer` lambda (`:518-536`) passes `admitApprovedVersions: true`; the resolver selection (`:611-614`) rides the same flag.
5. `AlphaSeparationMaterialConverter.cs` (`:130-133`): extend the boundary with `!IsApprovedSource(sourceEvidence)`, where `IsApprovedSource` calls `LilToonSourceAttestation.IsApprovedPackageEvidence(sourceEvidence.HasPackage, sourceEvidence.PackageVersion)`.

- [ ] **Step 4: Run, observe GREEN**

Run `Tests/Editor/Semantics`, `Tests/Editor/Build`, and `Tests/Editor/DryRun` folder filters. Expected: all green, including the untouched row pins (`:1764-1800`), the drift pins (`:1901-1931`), the fixture mirrors (`LilToonSourceAttestationTests.cs:190-216`), `BatchModeRefusesWithoutAsking`, and the consent memory bans.

- [ ] **Step 5: Commit**

```bash
git add Editor Tests/Editor
git commit -m "feat(semantics): approve lilToon versions below 2.5 end to end"
```

---

### Task 8: Research gate range-awareness

**Files:**
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs` (`:79-135`; the file aliases the attestation class, so the new code uses the existing alias)

**Interfaces:**
- Consumes: `AdmittedRows()` and `IsApprovedPackageVersion` (Task 7), via the research assembly's `InternalsVisibleTo`.

- [ ] **Step 1: Write the failing test**

Beside the membership gate (`:101-113`), using the file's alias for the attestation class:

```csharp
[Test]
public void AnInRangeVersionCountsAsReachable()
{
    // VersionReachable-style helper: "2.4.0" (row member) true,
    // "2.4.9" (in range, no row) true, "2.2.0" (below floor) false,
    // "2.5.0" (at bound) false.
}
```

- [ ] **Step 2: Run, observe RED**

Run the research test class. Expected: compile failure (no shared helper exists).

- [ ] **Step 3: Implement**

Extract the gate's version acceptance into one private helper used by both `AnInstalledFamilyMatchesTheVersionAmuseAttests` (`:79-135`) and the new test:

```csharp
private static bool VersionReachable(string installedVersion)
{
    foreach (var row in LilToonPins.AdmittedRows())
    {
        if (row.Version == installedVersion)
        {
            return true;
        }
    }

    return LilToonPins.IsApprovedPackageVersion(installedVersion);
}
```

Keep the probe pin (`:80-86`) untouched.

- [ ] **Step 4: Run, observe GREEN**

Run the research EditMode suite. Expected: all green. The environment-gated tests that need installed vendor shaders keep their existing skip behavior. The new test must not be environment-gated.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse.research
git commit -m "test(research): census gate admits approved in-range versions"
```

---

### Task 9: Docs

**Files:**
- Modify: `Packages/com.alrauna.amuse/README.md` (`:42` supported sentence; `:70-84` preset schema section)
- Modify: `README.md` (`:42`)
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`
- Modify: `docs/architecture/vision.md` (`:55`)
- Modify: `AGENTS.md` (`:7` supported-shaders sentence)

**Interfaces:** none.

- [ ] **Step 1: Edit the six surfaces**

1. Package README `:42`: "Supported shaders: Poiyomi Toon 9.3.64 (including Two Pass) and lilToon 2.3.0 and newer patches below 2.5, which are approved without a consent prompt. The build asks once per build for versions outside that range." Keep the variant list.
2. Package README `:70-84`: schema 5, the two new feature keys, and the note that schema 4 files refuse with the schema message.
3. Repo-root README `:42`: the same supported-shaders sentence in that file's voice.
4. Package CHANGELOG: the two sub-switches, the schema bump, and the approval rule.
5. `docs/architecture/vision.md:55`: replace "lilToon 2.3.0 through 2.3.4" with the approval rule in the same voice.
6. `AGENTS.md:7`: replace the stale "2.3.0 to 2.3.4" sentence with "lilToon 2.3.0 through versions below 2.5 (approved range), exact rows through 2.4.0".

- [ ] **Step 2: Verify**

No tests cover docs. Run `git diff --check`. Sweep every changed file for identifiers: at sign joined to hexadecimal, drive-letter paths, home paths, four-digit ports, and private asset names.

- [ ] **Step 3: Commit**

```bash
git add README.md AGENTS.md docs/architecture/vision.md Packages/com.alrauna.amuse/README.md Packages/com.alrauna.amuse/CHANGELOG.md
git commit -m "docs: separator sub-switches, schema 5, and the lilToon approval rule"
```

---

## Final verification

1. Full EditMode product suite (`Alrauna.Amuse.Tests.Editor`) and research suite (`Alrauna.Amuse.Research.Tests.Editor`). Record observed counts. Zero modals during the run.
2. `git diff --check` clean across the branch.
3. Every acceptance criterion in the spec maps to a passing named test from Tasks 1 through 8.
