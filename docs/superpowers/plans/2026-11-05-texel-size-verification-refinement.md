# Texel Size Verification Refinement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Owner approval of an execution method is required before implementation.

**Goal:** Replace the baked texel size equality with a print self consistency check, add the snapshot texture identity fact, and recover the measured 12727 triangle coverage without weakening any other rule.

**Architecture:** One rule branch changes in the pure reproduction type. One new fixed sentence and one rename aware identity comparison land in the reconstruction entry, which gains the locked original through a widened restore delegate. The scratch probe measurement gates the rule change: the amendment ships only when the refusing build snapshot carries the same texture identity as the locked material.

**Tech Stack:** C# in Unity 2022.3, NUnit and Unity Test Framework EditMode, Unity MCP for dev-project validation, and the scratch mirror project for the probe and the locked cycle. No new dependencies.

**Spec:** `docs/superpowers/specs/2026-11-05-texel-size-verification-refinement-design.md`, decisions TX1–TX6 and falsifiers 1–6.

Privacy note. This plan contains public product paths and synthetic fixture requirements only. It contains no private observations, machine paths, editor identities, generated cache identifiers, or vendor shader copies.

Status: 2026-11-05. Revised after two independent adversarial reviews. Plan review awaits owner approval. No implementation, test run, Unity action, staging, or commit is authorized or claimed by this plan-writing task.

## Global Constraints

- Unity 2022.3 and C# in the existing production assemblies only. No new dependencies.
- The aligner stays pure. The reproduction stays pure. No aligner input changes.
- Unknown information refuses only the conclusion that depends on it. Programming and invariant failures still throw.
- Refusal sentences stay fixed and property free. One sentence per failure mode.
- Keep both bundled admission flags false throughout this work.
- No production or test execution is authorized by this document alone. Never stage, commit, push, publish, or deploy without explicit authorization.
- Never mutate source assets or vendor packages. Product tests run only in the verified dev project. Probe and cycle work runs only in the scratch mirror project, never in the Census Lab project.
- Human-readable text uses simple english. Records contain no private identifiers, machine paths, instance identities, ports, or vendor shader copies.
- There are no commit steps in this plan. Staging is a separate owner decision.

All package-relative paths start at `Packages/com.alrauna.amuse/`. Paths starting with `docs/` are repo-relative. Scratch references resolve inside the scratch mirror project.

## Review Focus

1. The build clone's snapshot resolved a texture fact that differs from both the scene material and a plain clone. Task 1 names it before any rule change, and the plan stops if identity differs.
2. A texture that fails to resolve or carries no token must keep the unit bake pass. Falsifier 4 pins this in Task 2.
3. The historical eras wrap negative float4 components. Texel components are never negative, so the reciprocal rule holds there unchanged. The installed era fixture pins this in Task 2.
4. A clone snapshot with a foreign texture identity must refuse with the new sentence, including a foreign identity on a renamed slot. Both falsifier 6 tests pin this in Task 4.
5. The scale offset and define rules share the texture read surface with the changed branch. Falsifier 5 and the full assembly pin this in Tasks 3 and 6.

## File map and task ownership

| File | Owner and responsibility |
|---|---|
| scratch `Assets/AmuseMovedCountReproProbe/Editor/AmuseMovedCountCaptureProbe.cs` | Task 1. One new hook generation on the reconstruction entry, reading only |
| scratch driver menu, existing | Task 1. One locked cycle through the existing reflection entry |
| `docs/superpowers/investigations/2026-11-05-locked-unlocked-moved-count-gap-investigation.md` | Task 1. New sanitized measurement section |
| `Editor/Semantics/Poiyomi/LockedSourceReproduction.cs` | Task 3. Shape rule replaces the equality. Deletions per the cutover. `TryTextureOnU` stays |
| `Editor/Build/LockedMaterialReconstruction.cs` | Task 4. The identity fact, the new locked original parameter, the new sentence |
| `Editor/Build/TransientUnlockAvailability.cs` | Task 4. The production restore delegate factory widens |
| `Editor/Build/TransientUnlockRestoreResult.cs` | Task 4. `TransientUnlockRestoreDelegate` takes clone and locked original |
| `Editor/Build/TransientUnlockSwapIn.cs` | Task 4. The restore call passes `locked` |
| `Tests/Editor/Build/TransientUnlockTestLifecycle.cs` | Task 4. The knob and wrapper lambda gain the second material |
| `Tests/Editor/Build/TransientUnlockAvailabilityTests.cs` | Task 4. Delegate calls widen |
| `Tests/Editor/Build/TransientUnlockSwapInTests.cs` | Task 4. Delegate calls widen |
| `Tests/Editor/Build/BundledLockedOriginalRealLockTests.cs` | Task 4. Direct `TryApply` calls gain the locked original |
| `Tests/Editor/Build/LockedMaterialReconstructionTests.cs` | Task 4. Identity fact red tests |
| `Tests/Editor/Semantics/Poiyomi/LockedSourceReproductionTests.cs` | Tasks 2 and 3. Shape rule falsifiers and the recast refusal test |
| `Editor/DryRun/LockedPlanView.cs` | Untouched. It keeps reading the texture value size fields |
| `docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md` | Task 5. Inline amendment note at A5.4 |
| `Packages/com.alrauna.amuse/CHANGELOG.md` | Task 6. Refusal behavior change entry without avatar counts |

No new production file or assembly is planned.

## Verification commands and result rules

Use the mounted Unity MCP test route with `mode: "EditMode"` and `include_failed_tests: true`, reading tool schemas at execution time. Every runtime action first pins and verifies the exact project: product tests against the dev project, probe and cycle work against the scratch project. A test name with zero matched cases is a failed filter, not a pass.

Focused filters used below:

- **Reproduction:** test class `Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.LockedSourceReproductionTests`.
- **Reconstruction:** test class `Alrauna.Amuse.Tests.Editor.Build.LockedMaterialReconstructionTests`.
- **Swap-in:** test class `Alrauna.Amuse.Tests.Editor.Build.TransientUnlockSwapInTests`.
- **Availability:** test class `Alrauna.Amuse.Tests.Editor.Build.TransientUnlockAvailabilityTests`.
- **Full product and research:** assemblies `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor`.

Record observed matched, passed, failed, skipped, and inconclusive counts. A filtered run that reports zero tests is a failure. For RED, run the new or changed behavior case before its production change and record the actual failure text and the wrong implementation it catches. A case that passes on first run is characterization and is recorded as such, never dressed up as RED.

Restore `ProjectSettings/QualitySettings.asset` only when the working tree diff shows the known test run mutation. Any other diff on that file is reported to the owner instead of restored.

---

### Task 1: Snapshot measurement gate

**Files:**
- Modify: scratch `Assets/AmuseMovedCountReproProbe/Editor/AmuseMovedCountCaptureProbe.cs`
- Modify: `docs/superpowers/investigations/2026-11-05-locked-unlocked-moved-count-gap-investigation.md`

**Interfaces:**
- Consumes: the existing probe arming pattern (`[InitializeOnLoad]` synchronous arm), the existing reflection entry `AmuseMovedCountScratch.AmuseMovedCountDriver.RunCycles`, and the production types `LockedMaterialReconstruction`, `LockedSourceAlignmentMemo`, and `UnityLockedMaterialValues` through reflection.
- Produces: a scratch capture file whose lines pair, by sequence marker, the clone snapshot facts and the scene material snapshot facts for every texture slot of every refused reconstruction, using rename aware reads. A dated sanitized measurement section with the gate verdict.

- [ ] **Step 1: Add the probe hook generation**

Two hooks. First, one hook on `ClonePersistRestoreVerify` in the swap-in type. Its prefix stores the `locked` argument and the `clone` argument in static state, keyed by material instance number. Its postfix binds the return value, which is a bindable type, and records the restore outcome for that key. Second, one hook on `LockedMaterialReconstruction.TryApply`. Its prefix stores the `clone` material argument and the clone's current generated shader in static state before the original method runs. Do not bind the `TryApply` result. The result type is internal and the known Harmony trap delivers null.

The `TryApply` postfix reads the stored state, clears it, and dumps facts under the same material instance key the swap-in hook recorded. The memo lookup uses the stored shader, never the clone's shader at postfix time, because a refusal after the shader swap leaves the clone mutated. The postfix resolves the original shader from the stored clone's recorded GUID tag chain through reflection, calls `PropertiesOf` for the typed property table, and enumerates every property of texture type from that table. Each read resolves the renamed name through the memo entry's aligned record, the same mapping `TryTextureOnU` uses, so renamed slots do not read as empty. The scene side of the comparison is the `locked` material the swap-in hook stored for the same key. Facts are written per texture slot with a material key and a slot role, unconditionally, with no verdict at line level.

- [ ] **Step 2: Arm and run one locked cycle**

Refresh the scratch, confirm the arm marker in `capture-probe.log`, then run one cycle through the existing driver reflection entry. Expect the cycle total to stay the locked baseline value and the two known refusal lines to print.

- [ ] **Step 3: Join, compare, and write the gate verdict**

Slice the probe log by the arm marker. Join capture lines to outcomes by material instance key, never by order. The swap-in hook already marks which reconstructions refused. Filter the cycle's console lines by the exact known texel refusal sentence and compare that count with the marked refusal count. A count mismatch stops this plan. Compare the clone and scene tokens inside the scratch capture file. The capture file stays in the scratch project and never enters the repository. Append a sanitized section to the investigation record: the count of refused reconstructions, the count of texture slots compared, the count whose identity agrees, the count that differs, the count left scene unknown, the count of refused reconstructions that could not be joined, and the gate verdict. The record carries slot roles, equality booleans, counts, and the verdict only. It carries no tokens, no property names, no texture sizes, and no GUIDs.

Gate rule. Every compared slot agrees on identity, no slot stays scene unknown, no refused reconstruction is unjoined, and the refusal sentence count matches the marked refusal count: proceed to Task 2. A slot that differs, a slot that stays scene unknown, a refused reconstruction that cannot be joined, or a count mismatch: stop this plan, update both dated records with the anomaly, and report to the owner. No product file changes before the gate passes.

### Task 2: Shape rule red tests

**Files:**
- Modify: `Tests/Editor/Semantics/Poiyomi/LockedSourceReproductionTests.cs`

**Interfaces:**
- Consumes: the existing fixture helpers `Source(...)`, `FakeValues`, `AssertRefusal(result, detail)`, the existing baked texel test with its local sentence constant, and the literal shape and round trip detail constants of the reproduction type.
- Produces: the recast test `BakedTexelSizeWhoseReciprocalDisagreesRefuses`, the new tests `WellFormedStaleBakePassesOnDifferentTextureSize`, `ZeroOrNegativeBakeSizeRefuses`, `BakeThatDoesNotRoundTripRefusesWithTheRoundTripDetail`, `NoTextureWithWellFormedBakePasses`, `NoTextureWithUnitBakePasses`, `InstalledEraReciprocalRuleHolds`, and an optional era parameter on the `Source` helper defaulting to the bundled era.

- [ ] **Step 1: Recast the existing refusal test as falsifier 1**

Rename `BakedTexelSizeThatDiffersFromTheTextureRefusesAndNamesSharing` to `BakedTexelSizeWhoseReciprocalDisagreesRefuses`. Keep its existing bake `float4(0.00390625,0.00390625,256,512)`, which fails the reciprocal relation, and change the expected sentence from the texel detail to the literal shape detail. Delete the local texel sentence constant.

- [ ] **Step 2: Write the acceptance red test**

`WellFormedStaleBakePassesOnDifferentTextureSize`: baked literal `float4(0.0009765625,0.0009765625,1024,1024)` on the source, texture values resolving to a different synthetic size with a token. Assert the build succeeds.

- [ ] **Step 3: Write the remaining falsifier tests**

`ZeroOrNegativeBakeSizeRefuses`: a bake with a zero size component refuses with the literal shape detail under the new rule. `BakeThatDoesNotRoundTripRefusesWithTheRoundTripDetail`: a float4 whose components do not print back refuses with the round trip detail. `NoTextureWithWellFormedBakePasses`: no texture on the slot with a well formed 512 print passes. `NoTextureWithUnitBakePasses`: no texture with the unit bake passes. `InstalledEraReciprocalRuleHolds`: a `Source` built with era `PoiyomiLockEra.Thry2743`, a well formed reciprocal bake print `float4(0.001953125,0.001953125,512,512)`, and no texture on the slot passes. This needs an optional era parameter on the `Source` helper defaulting to the bundled era.

- [ ] **Step 4: Run the reproduction class and record the red set**

Run the focused reproduction filter. Expected red under the current code: the acceptance test, the no texture 512 test, the installed era test, the recast falsifier 1 test on the sentence, the zero or negative size test on the sentence, and the non round trip test on the sentence. The no texture unit bake test passes first and is recorded as characterization. Any other failure is a fixture defect and stops the task. Record the observed counts.

### Task 3: Shape rule green and cutover deletions

**Files:**
- Modify: `Editor/Semantics/Poiyomi/LockedSourceReproduction.cs`
- Modify: `Tests/Editor/Semantics/Poiyomi/LockedSourceReproductionTests.cs`

**Interfaces:**
- Consumes: the existing `RoundTripFloat4` and its round trip detail constant, the literal shape detail constant, and the retained `TryTextureOnU`.
- Produces: the private shape check used by the texel branch. Deleted: `ExpectedTexelSize` and `InvertFloat` and the texel refusal detail constant. Kept: `TryTextureOnU`, the texture value size fields, and every reader of them.

- [ ] **Step 1: Implement the shape check**

One private static helper. Parse the literal through the existing float4 round trip. Then parse the third and fourth component texts with `int.TryParse` in the invariant culture, never a throwing parse, and require both greater than zero. A failed try parse or a non positive result takes the literal shape sentence. Then compare the first component text against the invariant culture default format print of `1f` divided by the parsed third component, and the second component text against the print of `1f` divided by the parsed fourth component, matching the deleted invert helper's expression exactly. The branch calls the helper and refuses with the mapped detail on failure: a malformed float4, a failed integer try parse, a non positive size, or a broken relation takes the literal shape sentence, and a round trip failure takes the round trip sentence. No texture read remains in the branch.

- [ ] **Step 2: Delete the dead members**

Delete `ExpectedTexelSize`, `InvertFloat`, and the texel detail constant. `TryTextureOnU` stays, because the identity fact in Task 4 uses its rename mapping. Delete or fix compile broken test readers of the deleted members without weakening a valid assertion. The texture value size fields stay, because the dry run plan view reads them.

- [ ] **Step 3: Run the reproduction class green**

Run the focused reproduction filter. Expected: every task 2 test passes, including the red tests now green, and no other reproduction test regresses. Record the observed counts.

### Task 4: Snapshot identity fact and delegate widening

**Files:**
- Modify: `Editor/Build/LockedMaterialReconstruction.cs`
- Modify: `Editor/Build/TransientUnlockRestoreResult.cs`
- Modify: `Editor/Build/TransientUnlockAvailability.cs`
- Modify: `Editor/Build/TransientUnlockSwapIn.cs`
- Modify: `Tests/Editor/Build/TransientUnlockTestLifecycle.cs`
- Modify: `Tests/Editor/Build/TransientUnlockAvailabilityTests.cs`
- Modify: `Tests/Editor/Build/TransientUnlockSwapInTests.cs`
- Modify: `Tests/Editor/Build/BundledLockedOriginalRealLockTests.cs`
- Modify: `Tests/Editor/Build/LockedMaterialReconstructionTests.cs`

**Interfaces:**
- Consumes: `UnityLockedMaterialValues`, the typed texture property names from `PropertiesOf`, and the retained rename aware texture read.
- Produces: `TransientUnlockRestoreDelegate(Material lockedClone, Material lockedOriginal)` in the restore result type. `TryApply(Material clone, Material lockedOriginal, Func<Shader, bool> originalShaderAttested)`. One new internal fixed sentence constant with the exact text "A texture of the rebuilt material is not the texture of the locked material." One rename aware identity comparison before the plan build: for every texture property of the typed table, the clone snapshot token must equal the locked original snapshot token through the aligned rename mapping, and a mismatch refuses with the new sentence. The tests `CloneTextureIdentityThatDiffersRefuses` and `RenamedSlotTextureIdentityThatDiffersRefuses`.

- [ ] **Step 1: Write the red tests**

In the reconstruction tests, build the existing fixture pair and change one texture identity on the clone side. Assert refusal with the new sentence. Add a second red test that changes the identity of a renamed texture slot, so the fact cannot ship without rename coverage. Run both and record the failures: today no such check exists, so the named wrong implementation is the current code without the fact.

- [ ] **Step 2: Widen the delegate and implement the fact**

Change `TransientUnlockRestoreDelegate` to take the clone and the locked original. `CreateProductionRestore` returns a lambda over both. The swap-in passes `locked` at its restore call. The test lifecycle knob and wrapper gain the second material, and the availability, swap-in, and real lock call sites widen. In `TryApply`, build one values snapshot of the locked original beside the clone snapshot before the plan build. Compare tokens per typed texture property through the aligned rename mapping. Refuse with the new sentence on the first mismatch. The sentence names nothing.

- [ ] **Step 3: Run the affected classes green**

Run the focused reconstruction, swap-in, and availability filters. Expected: the red tests pass and no test in those classes regresses. Record the observed counts.

### Task 5: Owning design amendment note

**Files:**
- Modify: `docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md`

- [ ] **Step 1: Owning design amendment note**

At the A5.4 texel item in the 2026-10-23 design, add the inline amendment note dated 2026-11-05 pointing at this design's TX1, exactly as the house convention records earlier amendments. The note covers the texel rule only, because the identity fact is not an A5.4 item.

### Task 6: Verification matrix

**Files:**
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`
- Modify: `docs/superpowers/investigations/2026-11-05-locked-unlocked-moved-count-gap-investigation.md`

- [ ] **Step 1: Focused classes**

Run the reproduction, reconstruction, swap-in, and availability filters together. Record counts.

- [ ] **Step 2: Full product and research assemblies**

Run both full assemblies. Expected zero failures. Record counts. Restore `ProjectSettings/QualitySettings.asset` only when the diff shows the known test run mutation, and report any other diff to the owner instead.

- [ ] **Step 3: Changelog entry**

One entry for the shipped refusal behavior change. It states the equality to print shape change and the snapshot identity fact. It carries no avatar specific counts and no recovery claim.

- [ ] **Step 4: Scratch locked cycle recovery check**

Refresh the scratch with the changed package, run one locked cycle, and read the cycle record the scratch driver writes. Expected: the two previously refused slots convert and the locked cycle reads the unlocked baseline total. If another refusal sentence appears, record it verbatim in the investigation record and stop for owner review. The coverage claim stays an expectation until this step passes, and the changelog gains the observed recovery result only after this step passes.

- [ ] **Step 5: Sweep and close out**

Run the privacy and style sweep over every changed record and source file. Report the change set with observed counts and leave staging as the owner's decision.
