# Vendor-integrated test rework plan

Status as of 2026-11-09. Plan only. No test code, product code, or CI file changed under this record. Branch: `rework/vendor-integrated-tests`.

Privacy note: this record names only AMUSE source and public vendor packages. It holds no private avatar, machine, account, port, instance, hash, or GUID. All paths are relative to `<repo-root>`.

Design: `docs/superpowers/specs/2026-11-09-vendor-integrated-test-rework-design.md`. Refresh record: `docs/superpowers/investigations/2026-11-09-vendor-test-rework-refresh-investigation.md`.

This record speaks for its date. It authorizes no new production subsystem and no commit.

## Assumptions

The owner has not answered the five open questions of design section 10. This plan uses the design defaults. If an answer differs, the named tasks change.

|Question|Default used|Tasks that depend on it|
|---|---|---|
|1. CI placement|PR gate sets `AMUSE_REAL_LAC_PASS` and `AMUSE_REAL_THRY_LOCK`. Canary sets all four.|C2|
|2. C1 as its own change|Yes, after stage 2a.|C1|
|3. Strict skip guard|Yes.|C3|
|4. Decision 4 covers unlock-window real-lock tests|Yes.|Stage 4 task 4.5|
|5. Root README d4rk line|Align to the package README.|Stage 7|

## Rules for every task

- No commit, stage, push, or PR without explicit owner authorization. Each stage is written to be one revertable change.
- Verify `Application.dataPath` equals this repository before any Unity MCP write or test run. Stop on a mismatch. Never run in the Census Lab project.
- Never edit a vendor package, a vendor asset, or a `.meta` file.
- A filtered run that reports 0 tests is a failure. Record observed counts and Console output.
- A new test that passes on first run is recorded as characterization. It is never called RED.
- Moving a test from a stand-in to a real material keeps the synthetic falsifier for the same rule.
- End of every stage: run the full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies. Compare with the stage 0 baseline: 3241 leaves, 3221 passed, 1 failed, 19 skipped. A passed count that falls without a named reason is a defect. A removed or merged test needs a line in the stage report.
- Document sweep at the end of every stage that changes a doc: semicolons, contractions, unqualified time words, drive letters, home paths, four-digit ports, hex strings joined to an at sign, GUIDs, private names.
- Tests create assets under `Assets/`. They delete only what they create.

## Stage 2a: stale LAC premise test

Goal: the one stage 0 failure passes in an environment with LAC and in one without.

Files:

- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ReplacementTextureAttestationTests.cs`

Tasks:

- [ ] 2a.1 Read the test `ResetForTestsRestoresTheCharacterizedSetAndTheProductionProvider` and `ReplacementTextureAttestation.Versions` in `Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs`. Find how the installed package version is read and by which seam.
- [ ] 2a.2 Rewrite the assertion. After `ResetForTests`, the provider must not answer the injected `"9.9.9"`. Compare the restored provider answer with an independent read of the installed LAC `package.json` through `System.IO` and `JsonUtility`. If the file is absent, assert that the production read fails. If the file is present, assert equality with the independent value.
- [ ] 2a.3 Run the rewritten test. It must pass.
- [ ] 2a.4 RED. Apply a scratch edit to the production `ResetForTests` that leaves the injected provider in place. Run the rewritten test. Observe that it fails. Record the failure text. Revert the scratch edit. Run the test again and observe green. If the rewrite cannot fail under this mutation, the rewrite is wrong.
- [ ] 2a.5 Run the focused filter for the test class. Then run the full product assembly.

Acceptance: the focused run reports at least 1 test and 0 failures. The full run shows 0 failures. The skip count stays 19.

Verification caveat: the dev project has LAC, so the no-LAC branch cannot run there. Until C1 lands, CI installs no LAC. So the pre-C1 `unity` run of the 2a pull request runs the file-absent branch. Record that run as the no-LAC proof. After C1, no environment runs that branch. Record the branch as unproved from then on.

## Stage C1: CI lock sync

Starts after stage 2a lands or lands in the same change. Owner answer 2 applies.

Files:

- `scripts/ci/sources.json`
- `scripts/ci/lock.json`
- Read only: `scripts/ci/new-lock.ps1`, `scripts/ci/check-lock.ps1`, `scripts/ci/install-packages.ps1`, `.github/workflows/pr.yml`

Tasks:

- [ ] C1.1 Run `pwsh -NoProfile -File scripts/ci/check-lock.ps1` from the repo root. Record the two known differences as the RED state: d4rk lock 4.7.1 against manifest 4.7.2, and LAC missing from the lock.
- [ ] C1.2 Read `new-lock.ps1` and `install-packages.ps1`. Learn which source types and digest routes exist. Confirm that the GitHub release digest route accepts the LAC 0.9.0 asset. Confirm by reading the public release data. This step needs owner authorization for network access. If the owner declines, C1 stops here and the reason is reported.
- [ ] C1.3 Add a `limitex` source to `sources.json` with the public LAC repository listing. Map `dev.limitex.avatar-compressor` to it.
- [ ] C1.4 Run `new-lock.ps1` to add the LAC entry and the d4rk 4.7.2 entry. Review the diff. Each entry must hold a recorded digest. No entry may hold a placeholder.
- [ ] C1.5 GREEN. Run `check-lock.ps1`. It must exit 0.
- [ ] C1.6 Run the Pester suite for `scripts/ci`. Record counts. It must pass.
- [ ] C1.7 Read `pr.yml` and confirm that its `unity` job installs from the lock without a package list that needs a LAC edit.

Acceptance: `check-lock.ps1` exits 0. The Pester suite passes. The C1 pull request's own approved `unity` run installs LAC with a verified hash. That run is the observation point. If C1 merges without its own `unity` run, the C2 proving run is the first observation.

## Stage 1: LAC ground check and the fourth gate

Files:

- `Packages/com.alrauna.amuse/Tests/Editor/Build/VendorOptInGate.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Build/VendorRunRecord.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/RealSourceGroundCheckTests.cs`
- New: `Packages/com.alrauna.amuse/Tests/Editor/Build/RealLacPass.cs` (the harness, shared with stage 2)
- New: `Packages/com.alrauna.amuse/Tests/Editor/Build/RealLacPassGroundCheckTests.cs`
- `docs/development-setup.md`
- `scripts/ci/run-tests.ps1` (the strip list only, task 1.7. C2 changes the rest.)

Tasks:

- [ ] 1.1 Read `RealSourceGroundCheckTests.cs`. List which Poiyomi rows assert `LockedOriginalAdmitted`. Add the assertion for any bundled row that lacks it. A pass on first run is characterization.
- [ ] 1.2 Read `VendorOptInGate.cs` and `VendorRunRecord.cs` (`GatedFixtures` near line 60, private `GateVariables` near line 50). Add `AMUSE_REAL_LAC_PASS` to both. RED: copy the pattern of `BuildReportListsAGateThatWasSetAndExcludesNothing` in `VendorRunRecordTests.cs`. Set the variable to 1, call `VendorRunRecord.BuildReport`, and assert the opt-in gates line names it. Observe the failure first.
- [ ] 1.3 Write `RealLacPass`. Read the LAC source in the installed package to learn the field names and the preference keys. Do not copy LAC source. The harness follows these rules from design section 4.2:
  - Find the component type by reflection with an assembly-qualified lookup.
  - A null result is a named skip. This stage defines one fixed prefix for every vendor-absent skip text. The prefix names the package and its locked version. Define it as one constant in `RealLacPass`.
  - Assert the type's assembly and the pinned version.
  - Assert the field names.
  - Set fields through `SerializedObject`.
  - Freeze `analysisBackend` and `resizeBackend` in `EditorPrefs`. Restore both in `finally`.
  - Run the avatar through NDMF.
  - Delete only own assets.
- [ ] 1.4 Write the ground check behind `AMUSE_REAL_LAC_PASS`. Use one fixture texture larger than `MinSourceSize` (256). Run the real pass. Assert the copy attests under production admission. Assert the whole copy shape: name suffix, path-less state, registry pair, desktop format. RED for a plausible wrong harness: a harness that never runs the pass must fail the shape assertions. Observe that failure by temporarily skipping the pass call.
- [ ] 1.5 Run with the gate set to 1 in the dev editor. Record the pass. Run with the gate unset. Record that the new test skips by name.
- [ ] 1.6 Add the fourth gate to `docs/development-setup.md`.
- [ ] 1.7 Add `AMUSE_REAL_LAC_PASS` to the `$fixtureVariables` strip list in `scripts/ci/run-tests.ps1`. Update the header comment that says three. Reason: without it, a gate exported on a dev machine leaks into an ungated CI-style run.

Acceptance: the gated run passes with at least 1 new test executed. The ungated run shows the new tests as named skips. The run record block lists the new gate. The full suite has 0 failures.

Risk: LAC field names or the preference keys may differ from the design. The harness asserts them, so a mismatch fails with a named message. Fix the harness, not the assertion.

## Stage 2: LAC seam drop and comment fix

Starts after stages 1, 2a, and C1.

Files:

- The four LAC-shaped test classes that inject the version seam: `ReplacementTextureAttestationTests`, `GeneratedTextureAttestationTests`, `UnityTextureEvidenceTests`, and `UnityAlphaFieldEvidenceTests`. The seam doc comment in `ReplacementTextureAttestation.cs` names the same four. A related class, `ReplacementTextureIdentityTests`, does not use the seam. Classify it too, and add header reason lines to its synthetic copies. Search the whole `Packages/com.alrauna.amuse/Tests/Editor/` tree.
- `Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs` (comment only)

Tasks:

- [ ] 2.1 List every test that injects a version provider. Classify each: real-pass candidate, version-seam-only candidate, or falsifier. Record the table in the stage report.
- [ ] 2.2 For each real-pass candidate, replace the stand-in copy with the real LAC pass through `RealLacPass`. Gate it with `AMUSE_REAL_LAC_PASS`. Reset the version cache once in `OneTimeSetUp` and `OneTimeTearDown` for that class. No test in that class injects a provider.
- [ ] 2.3 For each version-seam-only candidate, drop the injected provider. Read the real version. Skip by name when LAC is absent. The skip text starts with the stage 1 vendor-absent prefix. No gate.
- [ ] 2.4 Falsifiers keep both seams and keep the per-test reset. Do not edit them except to add a header reason line.
- [ ] 2.5 Fix the comment that says LAC 0.9.0 never registers a bake pair. Cite the date and the observed registration in the installed source. State that the 2026-10-04 design is superseded for this fact. Do not edit the 2026-10-04 design.
- [ ] 2.6 RED for each moved test. Name a plausible wrong production behavior, for example admitting a copy without the suffix. Apply it as a scratch edit to production. Observe that the moved test fails. Revert. A test with no RED is characterization.
- [ ] 2.7 Run with the gate set and unset. Compare the passed plus skipped total with the baseline for these classes.

Acceptance: no moved test passes by skipping when the gate is set. The falsifier count for LAC is unchanged. The run takes no more than the stage 0 time plus the real-pass cost. Record both.

## Stage 3: lilToon real materials

Starts after stage 0. Independent of stages 4 to 6.

Files: lilToon tests under `Packages/com.alrauna.amuse/Tests/Editor/Semantics/`, `UnityMaterialSemanticsTests.cs`, and the shared files named in design section 5.

Tasks:

- [ ] 3.1 Recount from source. List every test that uses a lilToon stand-in shader under `Hidden/Alrauna/AmuseTests/*` or a verified seam. Classify each as: movable happy path, synthetic falsifier, exact-control (keyword, `ifex`, refusal shape, `ApprovedRangeVendorScope`). Write the table in the investigation record as a new dated section. The design counts (about 150, 170, 35) are unreconciled.
- [ ] 3.2 Add a helper that builds a lilToon material at run time from the installed shader. Load by pinned GUID. Never load by name. Create the material in memory. Destroy it in teardown.
- [ ] 3.3 Move one movable test first. Run it. Observe it pass on the real shader. Then move the rest in batches by file. Run the focused filter after each batch.
- [ ] 3.4 Replace Multi rows with `AdmittedMultiRows()` where the test is a happy path.
- [ ] 3.5 RED for each moved file: apply a named wrong production behavior as a scratch edit and observe that a moved test in that file fails. The file also needs one negative assertion that checks the outcome is not Unknown by accident.
- [ ] 3.6 Edit `UnityMaterialSemanticsTests.cs` first. Stage 4 edits it after this stage merges.
- [ ] 3.7 Add a header reason line to each file whose lilToon stand-ins stay. The reason is one of the four in design principle 2.

Acceptance: every moved test runs on the real shader. The synthetic falsifier count is unchanged. The stage report lists counts of moved, kept, and the reason for each kept group.

## Stage 4: Poiyomi real materials and the unlock window

Starts after stage 0. Edits `UnityMaterialSemanticsTests.cs` after stage 3 merges.

Files: `TransientUnlockTestLifecycle.cs`, `TransientUnlockTransformationTests.cs`, `AlphaSeparationApplyTests.cs`, `AmusePlatformFinishPluginTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`, `LockedSourceFixture`, and the Poiyomi Semantics tests. One integration owner holds the shared test files with stage 3 (design section 5).

Tasks:

- [ ] 4.1 Recount. The stage 0 run shows real Poiyomi tests in `AlphaSeparationApplyTests`. List stand-in tests and real tests. Record the table.
- [ ] 4.2 Measure the Poiyomi bundle import and first-use cost in the dev editor. Record it. It gates nothing.
- [ ] 4.3 Set `TransientUnlockTestKnobs.OriginalAttestation` explicitly to an always-true stub in every transient-unlock test that keeps a stand-in pair. Include the explicit `_ => true` site in `AmusePlatformFinishPluginTests.cs`. Add the file-header reason: these tests prove window mechanics, not original identity. Run the focused filter. Expect 0 behavior change. This is a refactor and has no RED.
- [ ] 4.4 Flip the default at the three sites to production attestation. The sites are `TransientUnlockTestLifecycle.cs`, `TransientUnlockTransformationTests.cs`, and `AlphaSeparationApplyTests.cs`. Procedure: first apply the flip alone as a scratch edit. Record the exact set of tests that fail. That set must equal the 4.1 list of stand-in pairs. A difference in either direction is a defect. Revert the scratch flip. Do 4.3. Apply the flip again and observe green. The count of header reason lines must equal the stand-in list count.
- [ ] 4.5 Add gated real-lock tests. One end-to-end transient unlock and apply test per bundled Poiyomi row on a real locked original. Gate with `AMUSE_REAL_THRY_LOCK`. Delete materials before vendor cache entries in cleanup. RED: a production apply that skips the revert in `TransientUnlockWindowClose` must fail these tests. Observe it as a scratch edit.
- [ ] 4.6 Move the Poiyomi Semantics happy paths from stand-in shaders to real materials. Keep `LockedSourceFixture` for per-edit knobs.

Acceptance: no unlock-window test depends on a silent default. Each bundled Poiyomi row has one gated real-lock end-to-end test. Counts are recorded.

Risk: the real lock writes vendor cache data and calls a vendor counter over the network. The gate keeps it off by default.

## Stage 5: Bindings and marker route

Files: tests for `CommittedControllerGraph`, the production-order fixture, and `MarkerStubBindings`.

Tasks:

- [ ] 5.1 Read the existing `CommittedControllerGraph.Enumerate` tests. Add a parameterized case that runs the same assertions with the real NDMF `GenericPlatformAnimatorBindings`. A pass on first run is characterization.
- [ ] 5.2 Add one real Modular Avatar probe through the existing production-order fixture. Write defaults stay off.
- [ ] 5.3 Keep `MarkerStubBindings`. It is a private nested class in two files: `AlphaSeparationApplyTests.cs` and `AlphaSeparationPreparationTests.cs`. Add a reason line above each class. The first file is a shared file of stages 3 and 4. Stage 5 edits it only after the earlier stage that owns it has merged.
- [ ] 5.4 VRCFury dispatch stays behind `AMUSE_PRODUCTION_ORDER_DISPATCH`. Add no new dispatch test unless a gap is found. Record a found gap as a finding, not a new scope.

Acceptance: the same assertions hold for stub and real bindings. Counts are recorded.

## Stage 6: AAO and d4rk

Files: AAO monotone-atlas tests, `DaoMergedConsumptionTests.cs`, and `Packages/com.alrauna.amuse/Tests/Editor/Build/AaoMergedConsumptionTests.cs`.

Tasks:

- [ ] 6.1 Add the AAO monotone-atlas case on the real AAO 1.9.21 route. Keep `CreateGeneratedAtlas` for the persisted-route leak falsifier.
- [ ] 6.2 Fix the skip text at `DaoMergedConsumptionTests.cs` lines 113 to 116. It names 4.7.1. Read the version from the lock data (`Packages/vpm-manifest.json` locked block) so that it cannot go stale.
- [ ] 6.3 Locate the ambient d4rk case. The review found it as the `TestCase(true)` arm of `ManySlotMergedRendererKeepsMixedAlphaTrianglesOnTheirSlots(bool includeDao)` in `AaoMergedConsumptionTests.cs`. The d4rk claim sits in the `includeDao` parameter and the root name, not in the method name. Verify in the installed d4rk source that d4rk hooks only the SDK pre-process callback. Then drop the arm, or stop attaching the d4rk component on it. Record the removed leaf in the stage report. If the case is not found, record that and stop.

Acceptance: the AAO case runs. The skip text matches the lock. The d4rk arm is dropped or no longer attaches d4rk, and the stage report names the removed leaf.

## Stage C2: opt-in gates in CI

Starts after C1. Needs the gates from stage 1.

Files: `scripts/ci/run-tests.ps1`, `scripts/ci/lib/Common.ps1`, `.github/workflows/pr.yml`, `.github/workflows/canary.yml`, new Pester tests under `scripts/ci/`.

Tasks:

- [ ] C2.1 Read `run-tests.ps1` lines 73 to 130 and the existing Pester layout. The script body sits behind a dot-source guard, and existing tests call only helpers. Find the seam for the env test.
- [ ] C2.2 Choose the seam. Extract the env handling into a dot-sourceable helper in the style of `Get-UnityTestArguments`. The helper unsets all four names, sets the listed ones, and returns the saved caller values. The script body and the Pester test both call it. A second helper restores the saved values.
- [ ] C2.3 RED. Write Pester tests against the helper. Test one: a name outside the allow list throws a named error text. The test asserts that text. A test that asserts only a throw passes on a missing parameter and proves nothing. Test two: with a valid name, the variable is 1, the other three are unset, and the restore helper returns the caller values. Observe both fail before the change.
- [ ] C2.4 GREEN. Add the `-OptIn` parameter and the helpers. The script unsets all four. Then it sets only the listed ones. Restore in `finally` (lines 126 to 129).
- [ ] C2.5 Add Pester tests that read `pr.yml` and `canary.yml`. One fails if the PR product step loses `AMUSE_REAL_LAC_PASS` or `AMUSE_REAL_THRY_LOCK`. One fails if the canary product step loses any of the four names. RED: run both before editing the workflows.
- [ ] C2.6 Edit `pr.yml` to pass the two names. Edit `canary.yml` to pass all four. Run the Pester suite and `check-lock.ps1`.
- [ ] C2.7 The proving run. The C2 pull request carries the change. The owner approves the `unity-gate` run. Record the result, the duration, and any network fault. If a gate fails for a CI reason, apply the design 7.2 choice and ask the owner which one. The PR gate keeps no unproven gate.
- [ ] C2.8 After the first canary run with all four gates, record its duration and result.

Acceptance: the Pester tests pass. The proving run passes. The product run prints `skipped 4`, which is the 2 `AMUSE_DAO_INTEGRATION` skips and the 2 `AMUSE_PRODUCTION_ORDER_DISPATCH` skips. A higher number means a gate did not reach the Editor, and the stage is not done. The research run is unchanged. This acceptance can be observed only after the owner authorizes a push and a run. Until then the stage is not done.

## Stage C3: skip guard

Starts after C2.

Files: `scripts/ci/lib/Common.ps1`, `scripts/ci/run-tests.ps1`, Pester tests and fixtures.

Tasks:

- [x] C3.1 Step 0, changed on 2026-11-14. No real `TestResults.xml` with a skip was available, and CI uploads none. The reader was run on a real Unity file of an older run. It parsed the counts and the assembly names. The skip shape follows the NUnit 3 format. The first PR run is the real check.
- [x] C3.2 Changed on 2026-11-14. The rule needs no vendor wording. Any skip that is not a gate skip fails, so a vendor-absent skip fails with no prefix pin.
- [x] C3.3 RED and GREEN done on 2026-11-14, in one change. Pester tests cover a set gate, a vendor-absent skip, a skip with no reason, and a reader that finds fewer skips than the run counts. They also cover the printed line per unset gate.
- [x] C3.4 `Read-NUnitResult` returns the skipped cases with their reasons. `Complete-TestRun` passes the set gates to `Assert-SkipsAreKnown`. The run prints the skip count per unset gate.
- [ ] C3.5 Open. The first PR run after 2026-11-14 must print the skip count per unset gate. The sum must match the skipped count of the run, which was 22 on the dev editor run of 2026-11-13. A mismatch or an unknown skip fails the run, and the owner reviews it.

Acceptance: the Pester tests pass. A real run prints the per-reason skip counts, and the counts match the run record.

## Stage 7: documentation

Files: `docs/development-setup.md`, root `README.md`, `docs/superpowers/README.md` if it names the stand-in rule.

Tasks:

- [ ] 7.1 Align root `README.md` line 36 with the package README line 36 (owner answer 5). The gated tests are observation, not validation.
- [ ] 7.2 `docs/development-setup.md`: LAC repository registration, the fourth gate, and the rule that a gate skip is a CI failure when the run set the gate.
- [ ] 7.3 Add a dated status line to the refresh record and to the design: which stages landed and with what observed counts.
- [ ] 7.4 Run the identifier and style sweep over every changed doc. State that the sweep ran.

Acceptance: sweep has 0 hits. Docs state dates for every status claim.

## Stage order and dependencies

CI gates come before stages 2 to 6. Reason: until C2 and C3 land, a PR that adds gated tests shows green in CI while every new gated test skips, and `Assert-TestRun` never counts a skip. C2 needs only C1 and stage 1.

|Order|Stage|Needs|
|---|---|---|
|1|2a|none|
|2|C1|2a, owner answer 2, network authorization|
|3|1|stage 0|
|4|C2|C1, stage 1|
|5|C3|C2|
|6|2|1, 2a, C1|
|7|3, 5, 6 (any order, but 3 and 5 and 6 each wait for any earlier stage that edits the same file)|stage 0|
|8|4|stage 0, stage 3 merged, stage 5 merged if it edited a shared file|
|9|7|all others|

## Open risks

- The no-LAC branch of the stage 2a test has no run to prove it, except the pre-C1 CI run of the 2a pull request (see 2a acceptance).
- The real Thry lock needs the network. The C2 proving run observes whether it works for the low user.
- The CI duration is unmeasured. The 55 minute product timeout may be too short after the gates add work. A timeout ends with exit code -1. It is a budget fault.
- Stage 3 and 4 counts are unreconciled until their recount tasks run.
- Scripting defines from LTCGI and Red Sim Light Volumes can force a recompile mid-run.
- Stage 3 moves can pass for the wrong reason. A moved test that classifies every triangle as Unknown still passes a negative assertion. Each moved file needs one RED, not one per batch (task 3.5).

## Review log

Four read-only reviewers checked this plan on 2026-11-09. Fixes applied:

- Moved C2 and C3 before stages 2 to 6. Before this fix, every PR in the middle of the rework showed green with all new gated tests skipped.
- Added a canary guard to C2.4. Without it the canary can lose its gates and stay green.
- Made C3 fail when it parses zero skips from a run that left gates unset. This stops an inert parser.
- Named the seam for C2 Pester tests. Pinned the first RED test to a named error text. The unpinned test passed on a missing parameter.
- Added a C2 acceptance value: the product run prints `skipped 4`.
- Added the `run-tests.ps1` strip-list task to stage 1. Cited the restore block lines.
- Reordered 2a: rewrite first, then mutate, then observe RED.
- Recorded the 2a pre-C1 CI run as the no-LAC proof.
- Corrected the LAC class count to four and added `ReplacementTextureIdentityTests` as a related fifth class.
- Named the real `MarkerStubBindings` homes and the real d4rk test (`includeDao` arm). Task 6.3 locates the case first and stops if it is absent.
- Added the two missing stage 4 shared files and the plugin-test `_ => true` site.
- Required that the stage 4 flip failure set equals the stand-in list.
- Added a vendor-absent skip prefix, canary first-run record, vendor versions in stage reports, and header reasons for kept lilToon stand-ins.
- Changed 1.2 RED to use the run-record report line. `GateVariables` is private.
- Corrected two long sentences and one time word.

## Execution status as of 2026-11-10

Nothing is committed, staged, or pushed. All work sits in the working tree.

|Stage|State as of 2026-11-10|
|---|---|
|2a|Done. The stale LAC test is rewritten. A mutation of `ResetForTests` fails it.|
|C1|Done as of 2026-11-11. The lock holds the Avatar Compressor 0.9.0 and d4rkAvatarOptimizer 4.7.2. Each hash came from the GitHub release digest and matched the downloaded zip. `check-lock.ps1` exits 0. Pester passes 156. The first CI install of the new lock is not observed yet.|
|1|Done. The LAC ground check and the fourth gate exist. The gated run passes. The ungated run skips by name.|
|C2|Done with one gate off. The PR step runs `AMUSE_REAL_LAC_PASS` only. On 2026-11-13 a PR run with the Thry gate also set passed 3266 of 3271 tests with 5 skips, in 52 minutes of a 55 minute budget. That margin is too small, so the Thry gate stays in the canary only. As of 2026-11-13 the canary step waits 75 minutes inside a 90 minute step limit. A canary start from the branch failed at once, because the `unity-canary` environment allows only `main`. The first canary run is possible only after a merge to `main`.|
|C3|Built as of 2026-11-14, with one change from the plan. Task C3.5 stays open. `Read-NUnitResult` returns every skipped test with its reason. `Assert-SkipsAreKnown` fails a run for a skip of a set gate, a skip of an absent vendor, a skip with another reason or none, and a count mismatch between the reader and the run. It prints the skip count per unset gate. Pester has tests for each failure. The plan wanted a real `TestResults.xml` first. None was needed, because the rule has no vendor-specific wording and the reader parsed a real Unity file of an older run. The canary run on 2026-11-14 ran the earlier guard on real output and passed. A real file that holds a skip is still unseen. The first PR run shows whether the count check holds on real output.|
|2|Done. 11 tests read the real LAC version. Two gated real-output tests are new. No test was removed.|
|3|Done for the lilToon test files and for `AlphaSeparationPreparationTests`. The shared Semantics and Apply rows moved under stage 4.|
|4|Done as of 2026-11-11. Tasks 4.1 to 4.4 are done. Task 4.5 is done: two gated real-lock tests (one per bundled Poiyomi row) passed with `AMUSE_REAL_THRY_LOCK` set, and a scratch mutation that skips the revert made both fail. Task 4.6 is done: 7 Semantics rows and 12 Apply rows moved under 4c, and about 400 leaves in the Poiyomi Semantics folder moved to real unlocked bundled materials in six batches, each reviewed. The recount found no movable row in the dry-run file (28 rows use another shader family, 2 are exact-control, 1 needs a locked material), so that file stays as it is.|
|5|Done. The controller-graph tests run on stub and real NDMF bindings. The gated Modular Avatar probe and its tier B twin pass.|
|6|Done. The AAO monotone-atlas case, the d4rk skip text, and the d4rk arm removal are done.|
|7|Done as of 2026-11-14. The root README line and this status table are done. In `docs/development-setup.md` the fourth gate, the CI opt-in section, the Limitex repository row, and the strict skip rule are done. The identifier sweep ran on 2026-11-13 and again on 2026-11-14.|

Full run on 2026-11-11 after the stage 4 batches and the tier B fix: the product assembly completed 3269 leaves with 3246 passed, 0 failed, and 23 skipped. The research assembly passed 151 of 151. The 23 skips are the named gate and vendor-absent skips. The progress total of the MCP test job reads 3429 for every filtered or full run. It counts tree nodes and not leaves, so it does not match the leaf count.
