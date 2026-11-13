# Vendor-integrated test rework design

Status as of 2026-11-09. Design only. No test code, product code, or CI file changed under this record. Branch: `rework/vendor-integrated-tests`. Four adversarial reviewers checked this record on 2026-11-09. Section 11 lists what they changed.

Privacy note: this record names only AMUSE source and public vendor packages. It holds no private avatar, machine, account, port, instance, hash, or GUID. Vendor versions are public data. Digests appear nowhere in this record. All paths are relative to `<repo-root>`.

This record turns two inputs into a design:

- The owner decision of 2026-10-25: AMUSE tests run against the real vendor packages that the dev project installs.
- The refresh record `docs/superpowers/investigations/2026-11-09-vendor-test-rework-refresh-investigation.md`. Its section 12 holds the owner answers of 2026-11-09. Its section 13 holds the stage 0 result.

This record speaks for its date. It authorizes no new production subsystem and no commit.

## 1. Goal

A test that claims to prove behavior on a vendor surface must run that surface. A synthetic double stays only for the reasons that `AGENTS.md` lists. CI must run the real-vendor tests, or it must fail and say that it did not.

## 2. Evidence that shapes the design

The facts below were observed on 2026-11-09.

|Fact|Source|
|---|---|
|The full product assembly ran in the dev editor. 3241 leaf tests completed. 3221 passed. 1 failed. 19 skipped. 0 were inconclusive. All 19 skips are named opt-in skips: 15 for `AMUSE_REAL_THRY_LOCK`, 2 for `AMUSE_DAO_INTEGRATION`, 2 for `AMUSE_PRODUCTION_ORDER_DISPATCH`. The job total of 3399 counts suite nodes and is not a leaf count.|Stage 0 run and the suite run record. Refresh record section 13.|
|The one failure is `ReplacementTextureAttestationTests.ResetForTestsRestoresTheCharacterizedSetAndTheProductionProvider`. It asserts that the LAC version read fails. LAC 0.9.0 is installed, so the read succeeds.|Stage 0 run, test source.|
|`Packages/com.alrauna.amuse/Tests/Editor/Semantics/RealSourceGroundCheckTests.cs` holds ungated ground checks for lilToon 2.4.1 and the bundled Poiyomi shaders. They load by pinned GUID and run production attestation. They passed in the stage 0 run.|Test source. Stage 0 run.|
|`scripts/ci/check-lock.ps1` reports 2 differences. `scripts/ci/lock.json` holds d4rkAvatarOptimizer 4.7.1 and the manifest locks 4.7.2. LAC is in the manifest and absent from the lock.|Local run of `check-lock.ps1`.|
|`scripts/ci/lock.json` and `scripts/ci/sources.json` hold no LAC entry and no LAC source. The LAC repository listing is public. It has no `zipSHA256`. The 0.9.0 asset has a GitHub release digest.|Both files. The reviewers read the listing and the release data.|
|`scripts/ci/run-tests.ps1` unsets the three opt-in variables before every Editor run. Its comment says the gate keeps them unset. So no gated test runs in CI.|`scripts/ci/run-tests.ps1`, lines 73 to 106.|
|`Assert-TestRun` fails on zero tests, a failed test, an inconclusive test, zero passed tests, and a bad exit code. It never counts skipped tests. `Read-NUnitResult` returns counters, failed names, the first two message lines of each failed test, and assembly names. It reads no skip reason.|`scripts/ci/lib/Common.ps1`.|
|`pr.yml` and `canary.yml` call `run-tests.ps1` with the same arguments. Both workflows install the full lock. The lock has no LAC.|Workflow files.|
|`Start-AsLowUser` copies the caller environment to the Editor child process. It blanks the `GITHUB`, `RUNNER`, and `ACTIONS` prefixes, `ImageOS`, and `ImageVersion`.|`scripts/ci/lib/Isolation.ps1`.|

The dev project has LAC and CI had none. Until C1 lands, one test can pass in CI and fail in the dev project. This is the environment split.

## 3. Principles

1. **Real first.** A happy-path test uses the installed vendor shader, tool, or pass. It uses no injected version and no stand-in.
2. **Synthetic by reason.** A double stays when it fits one reason: an independent outcome oracle, an input no vendor produces, exact falsifier control, or a source the pinned environment cannot supply. Each kept double carries a one-line reason in its file header.
3. **Drift speaks by name.** A real-vendor test that fails on drift names the package, the locked version, and the observed version.
4. **An absent vendor skips by name.** The skip reason names the package and its locked version. This is the rule in `docs/development-setup.md`. A test whose LAC type does not resolve skips. It never fails.
5. **CI hides no skip.** Section 7.3 defines the guard.
6. **No vendor edits.** No stage edits a vendor package, a vendor asset, or a `.meta` file.
7. **A move keeps its falsifier.** A move from a stand-in to a real material never removes the synthetic falsifier for the same rule.

## 4. Test design

### 4.1 Ground checks (stage 1)

`RealSourceGroundCheckTests` already covers lilToon and the bundled Poiyomi rows. Stage 1 therefore has a small scope:

- Add a `LockedOriginalAdmitted` assertion for both bundled Poiyomi rows if the file lacks it. The author checks the file first.
- Add the LAC ground check. It runs the real pass on one fixture texture. It asserts that the copy attests under production admission. It asserts the whole copy shape: the name suffix, path-less state, the registry pair, and a desktop format. A version match alone proves nothing.
- Add the gate `AMUSE_REAL_LAC_PASS` in `Packages/com.alrauna.amuse/Tests/Editor/Build/VendorOptInGate.cs`. Extend `GateVariables` and `GatedFixtures` in `Packages/com.alrauna.amuse/Tests/Editor/Build/VendorRunRecord.cs`. Without the second change the run record stops being the single skip source.

Owner decisions 1 and 2 of 2026-11-09 stay as rules for a future drift. If a ground check later refuses a new vendor version, a dated admission follows as its own change. The admission has its own RED test. The RED test asserts that the measured digest is in the admitted set. No ground check is weakened to pass. A Poiyomi admission covers the bundled shape only. It never pins the project to 10.0.27. As of 2026-11-09 no admission is needed.

A ground check that passes on first run is recorded as characterization. It is never called RED.

### 4.2 LAC real pass harness (stage 2)

One helper, `RealLacPass`, in `Packages/com.alrauna.amuse/Tests/Editor/Build/`. Its rules:

- It finds the LAC component type by reflection with an assembly-qualified lookup. `MergedConsumptionFixture.cs` already uses this pattern for other vendors. A null result is a named skip.
- It asserts that the type comes from the LAC assembly and that the installed LAC version equals the pinned version. It asserts the field names it uses. A LAC release that renames a field fails with a named message.
- It adds the component and sets every field through `SerializedObject`. The fixture texture is larger than the minimum source size, so the pass always processes it.
- It freezes both LAC backend preferences in `EditorPrefs`: `analysisBackend` and `resizeBackend`. It restores both earlier values in `finally`. The format then does not vary with GPU analysis.
- It runs the avatar through NDMF. The LAC pass runs in the Optimizing phase. AMUSE runs in PlatformFinish, after it.
- It deletes only its own generated assets.

Reset rule. `ReplacementTextureAttestation.Versions.ResetForTests()` clears the version cache and the injected provider. A reset per test makes each test read the package version again, and the read can poll up to 30 seconds. So the rule has two parts:

- Classes that run the real pass reset once, in `OneTimeSetUp` and `OneTimeTearDown`. No test in them injects a provider.
- Falsifier classes keep their per-test reset. They inject providers.

Gating rule for seam-dropped tests:

- A test that runs the real LAC pass sits behind `AMUSE_REAL_LAC_PASS`. This follows owner decision 4.
- A test that only drops the version seam and reads the real version needs no gate. It skips by name when LAC is absent.
- Falsifier tests keep both seams and need no gate. The falsifiers stay synthetic: unregistered copy, unsuffixed copy, two claimants, form crossing, version refusals, foreign or derived containers. No vendor produces these states.

The stale premise test is the first item of stage 2, and it lands first (section 5):

- The test keeps its purpose. After `ResetForTests`, the production provider answers, not the injected `"9.9.9"`.
- The test stops asserting that LAC is absent. It compares the restored provider answer with an independent read of the installed package manifest. If LAC is absent it asserts that the read fails. If LAC is present it asserts that the read equals the independent value.
- RED check: a `ResetForTests` that leaves the injected provider in place must fail the test. The author observes that failure first.

The LAC comment fix. `Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs` and the 2026-10-04 LAC design say LAC 0.9.0 never registers a bake pair. The installed source does. The in-build `_baked` refusal still holds through the suffix gate. The stage corrects the comment, cites the date and the observed behavior, and says the dated record is superseded for this fact. The 2026-10-04 design stays unchanged.

### 4.3 lilToon real materials (stage 3)

- Build test materials at run time from the installed shaders, loaded by pinned GUID. No `.mat` file ships in lilToon. Name lookup is not used, because stand-ins mimic vendor names.
- Replace Multi profile rows with `AdmittedMultiRows()` where the test is a happy path.
- Keep stand-ins for exact keyword control, exact `ifex` text, refusal shapes, and the `ApprovedRangeVendorScope` package.
- The scouts counted about 150 movable tests, about 170 synthetic falsifiers, and about 35 exact-control tests. The counts are unreconciled. Stage 3 starts with a recount from source and records it.
- LTCGI affects lilToon canonicalization when it is installed. The CI lock includes LTCGI 1.7.4, so CI and the dev project agree.

### 4.4 Poiyomi real materials and the unlock window (stage 4)

About 60 transient-unlock tests build stand-in locked pairs. The stand-in original shader is a test shader. Production attestation, `LockedMaterialIdentity.OriginalShaderAttested`, refuses any original that is not an admitted Poiyomi name. So changing the `OriginalAttestation` default from always-true to production attestation breaks those tests in every order. The reviewers confirmed this. The design resolves it in four steps:

1. Measure the import cost of the Poiyomi bundle. Record it. It never gates a test.
2. Every transient-unlock test that keeps a stand-in pair sets `TransientUnlockTestKnobs.OriginalAttestation` explicitly to an always-true stub. The file header says why: these tests prove the window mechanics, not the identity of the original. Real-lock tests prove identity.
3. Change the default to production attestation. A test that forgets to choose then fails with a named refusal. The change touches three sites: `TransientUnlockTestLifecycle.cs`, `TransientUnlockTransformationTests.cs`, and `AlphaSeparationApplyTests.cs`, all under `Packages/com.alrauna.amuse/Tests/Editor/Build/`.
4. Add gated real-lock tests for the unlock window. Each bundled Poiyomi row gets one end-to-end transient unlock and apply test on a real locked original. They sit behind `AMUSE_REAL_THRY_LOCK`. Owner decision 4 covers real Poiyomi lock tests. Question 4 in section 10 asks the owner to confirm that this covers the unlock-window family.

Then move the Semantics happy paths from stand-in shaders to real materials. Keep exact-control stand-ins. `LockedSourceFixture` stays for per-edit knobs, because a real lock cannot change one lock edit at a time.

Step 2 adds no new proof for the stand-in tests. Step 4 adds the missing proof on the real path. The first step order is fixed: step 2 before step 3.

Stage 4 starts with a recount. The stage 0 run shows real Poiyomi tests already exist in `AlphaSeparationApplyTests`. The refresh record counts may be too high.

Real-lock hygiene: cleanup deletes materials before the vendor cache entries. The lock calls a vendor usage counter over the network once per session.

### 4.5 Bindings and marker route (stage 5)

- Run `CommittedControllerGraph.Enumerate` with both the stub bindings and the real NDMF `GenericPlatformAnimatorBindings`. The same assertions hold for both.
- Add one real Modular Avatar probe through the existing production-order fixture.
- `MarkerStubBindings` stays. Real bindings give no exact special-clip control.
- A VRCFury dispatch writes project state. It stays behind `AMUSE_PRODUCTION_ORDER_DISPATCH`. Write defaults stay off in the fixtures to avoid the blocking dialog.

### 4.6 AAO and d4rkAvatarOptimizer (stage 6)

- Add the AAO monotone-atlas case on the real AAO 1.9.21 route.
- `CreateGeneratedAtlas` stays for the persisted-route leak falsifier. Real AAO never persists the atlas.
- Correct the d4rk skip text in `DaoMergedConsumptionTests.cs`. It names 4.7.1 and the installed version is 4.7.2. The text reads the version from the lock data, so it cannot go stale again.
- The ambient `ProcessAvatar` case with a d4rk name does nothing. d4rk hooks only the SDK pre-process callback. The reviewers checked this in source on 2026-11-09. The stage renames the case or removes the d4rk claim from its name.
- Two README files disagree as of 2026-11-09. `Packages/com.alrauna.amuse/README.md` line 36 says the d4rk combination is not validated in 0.1.0. The root `README.md` line 36 says gated tests exercise the combination. Stage 7 aligns the root file with the package file. The package file is the shipped text. The gated tests are observation, not validation.

### 4.7 What stays synthetic

Reference fixtures and oracles, controlled pixel grids, refusal-shape blocks, exact keyword and `ifex` control, LAC and AAO falsifiers, `MarkerStubBindings`, `ApprovedRangeVendorScope`, and `LockedSourceFixture` for per-edit knobs. The refresh record section 6 holds the reasons.

### 4.8 Gates

|Variable|Scope|State on 2026-11-09|
|---|---|---|
|`AMUSE_DAO_INTEGRATION`|d4rk combined-sequence tests. They delete project-level d4rk output.|Exists.|
|`AMUSE_REAL_THRY_LOCK`|Real Thry lock tests. They write vendor cache data and call a vendor counter over the network.|Exists.|
|`AMUSE_PRODUCTION_ORDER_DISPATCH`|SDK dispatcher fixtures. VRCFury writes generated controllers.|Exists.|
|`AMUSE_REAL_LAC_PASS`|Real LAC pass tests. They write generated assets and touch `EditorPrefs`.|New. Stage 1 adds it to `VendorOptInGate.cs`, `VendorRunRecord.cs`, `docs/development-setup.md`, and `scripts/ci/run-tests.ps1`.|

All four gates guard tests that write state. This is why every gate is opt-in on a dev machine.

## 5. Stage order

|Stage|Content|Starts when|
|---|---|---|
|0|Baseline run. Result in the refresh record section 13.|Done on 2026-11-09.|
|2a|The stale-premise LAC test rewrite (section 4.2). It is the one failing test of stage 0.|Owner approves the plan.|
|C1|CI lock sync (section 7.1).|Stage 2a has landed or lands in the same change. Owner answers question 2.|
|1|LAC ground check, the missing Poiyomi assertion, and the new gate plumbing.|Stage 0 done.|
|2|LAC seam drop and the comment fix. All other tests of the LAC classes.|Stages 1, 2a, and C1 done.|
|3|lilToon real materials.|Stage 0 done.|
|4|Poiyomi real materials and the unlock window.|Stage 0 done.|
|5|Bindings and marker route.|Stage 0 done.|
|6|AAO monotone-atlas case and d4rk text.|Stage 0 done.|
|C2, C3|CI gates and the skip guard (section 7).|C1 done. C2 needs the gates that its tests use.|
|7|Documentation.|All other stages done.|

Order reason for C1 after 2a: C1 installs LAC in CI. The stale test fails whenever LAC is installed. Without 2a, every pull request goes red from C1 until 2a lands.

Stages 3, 4, 5, and 6 are independent. Ownership of shared files:

- Stage 1 owns `VendorOptInGate.cs` and `VendorRunRecord.cs`. Later stages ask the stage 1 owner for any gate change.
- Stages 3 and 4 both touch `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`. It holds many lilToon and Poiyomi references. Stage 3 edits it first. Stage 4 edits it after stage 3 merges.
- Stages 3 and 4 both touch `AlphaSeparationApplyTests.cs`, `AmusePlatformFinishPluginTests.cs`, and `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`. One integration owner holds these three files and merges stage 3 and stage 4 edits in sequence.
- `LockedSourceFixture` serves Poiyomi stand-ins. Only stage 4 edits it.
- The CI files `scripts/ci/*`, `.github/workflows/pr.yml`, and `.github/workflows/canary.yml` belong to one owner across C1, C2, and C3.

Rollback. Each stage is one change that can be reverted alone. A stage that turns CI red is reverted first and fixed second. C2 reverts by removing the `-OptIn` argument from `pr.yml`.

## 6. Verification rules per stage

- Run the focused EditMode filter first. A filtered run that reports 0 tests is a failure.
- Run both product and research assemblies at the end of each stage. Record the observed counts and the Console result. Compare the product counts with the stage 0 record: 3241 leaves, 3221 passed, 1 failed, 19 skipped.
- A behavior change needs a failing test observed first, for a named plausible wrong implementation. A test that passes on first run is recorded as characterization.
- Never run these tests in the Census Lab project. Verify `Application.dataPath` before any editor tool call.
- The stage report states the observed vendor versions. It names no machine, account, or instance.

## 7. CI design

The changes below keep the existing trust model. A maintainer approves each `unity-gate` run. The runner is disposable. No secret reaches test code.

### 7.1 C1: lock sync

- Add a `limitex` source to `scripts/ci/sources.json` with the public LAC repository listing. Map `dev.limitex.avatar-compressor` to it.
- Add the LAC entry and the d4rkAvatarOptimizer 4.7.2 entry to `scripts/ci/lock.json`. Run `scripts/ci/new-lock.ps1` to record the hashes. LAC ships as a GitHub release zip, so the GitHub release digest route applies. That route records the publisher digest. It is the same strength as the route for the other packages. The lock then pins the bytes.
- No Pester test reads the real lock or sources files. No test update is needed. `check-lock.ps1` already fails when a locked package has no source key.
- Acceptance: `check-lock.ps1` exits 0. The `scripts` job passes. The `unity` job installs LAC with a verified hash. LAC dependencies are already in the lock.

### 7.2 C2: opt-in gates in CI

As of 2026-11-09 the runner strips every gate variable. The design adds an explicit `-OptIn` parameter to `scripts/ci/run-tests.ps1`:

- The parameter takes names from a fixed allow list of the four gate variables. Any other name is an error.
- The script still unsets all four. Then it sets only the listed ones to 1 for the Editor child process. It restores the caller values after the run.
- `pr.yml` and `canary.yml` both change. Each passes `-OptIn` in its product test step.
- A Pester test for `run-tests.ps1` checks the allow list and the restore. A second Pester test reads `pr.yml` and fails if the PR gate loses its expected `-OptIn` names. This stops a silent removal.

Placement:

|Job|Gates set|Reason|
|---|---|---|
|PR gate (`unity`)|`AMUSE_REAL_LAC_PASS`|This gate guards the real path of code that a pull request changes. A gated test that never runs in CI protects nothing. Status as of 2026-11-11: the first PR run that also set `AMUSE_REAL_THRY_LOCK` failed 13 tests of the older real-lock suite on the hosted runner. That gate moved out of the PR step until the cause is known.|
|Weekly canary|All four|The canary never blocks a merge. It is the safe place for the tests that change more project state.|

The proving run is the pull request that carries C2. A maintainer approves its `unity-gate` run. That run is the trial. If it fails for a reason of the CI environment, such as no network for the Thry counter, the fix is one of two choices: move that gate to the canary only, or allow the network for the low user in a reviewed change. The PR gate keeps no unproven gate. The canary cannot prove the PR-gate tests, because it floats vendor versions while the tests pin them. So the canary plays no part in promotion.

`AMUSE_DAO_INTEGRATION` and `AMUSE_PRODUCTION_ORDER_DISPATCH` stay out of the PR gate. The canary runs them first. The owner may widen the PR gate later.

### 7.3 C3: skip guard

Step 0 of C3: observe one real `TestResults.xml` from a run with a known gate skip. Check that the file holds a skipped result and a reason text. Pin that shape in a Pester fixture. If Unity omits the reason, C3 stops and the design changes.

Then the checks:

- `Read-NUnitResult` gains the skipped cases and their reasons.
- `run-tests.ps1` passes the set gate names to `Complete-TestRun`.
- A skip fails the run when its reason says that a gate the run set is not set to 1. The gate did not reach the test.
- A skip fails the run when its reason names an absent vendor package. The lock installs every vendor. Absence is an install fault. This holds for the PR gate and for the canary.
- A skip fails the run when its reason matches neither of the two shapes above and names no unset gate. The first run lists any such skip. The maintainer reviews that list before C3 merges.
- A skip whose reason names a gate that the run did not set is expected and passes.
- The run prints the skipped count per reason. It prints no path.
- Acceptance: a Pester test feeds a result with a gate skip for a set gate and expects failure. A second feeds a result with an expected skip and expects success. A third feeds an unknown skip and expects failure.

### 7.4 C4: first runs

- The proving run is the C2 pull request, as section 7.2 says.
- The first canary run with all four gates records its duration and result. A red canary from vendor drift is the drift signal. It opens a dated admission and never weakens a test.
- Time budget. The dev suite took 1681 seconds in the dev editor on 2026-11-09. On 2026-11-13 the hosted PR run took about 38 minutes without the Thry gate and 52 minutes with it. The PR step waits 55 minutes and runs without the Thry gate. The canary step runs all four gates and waits 75 minutes, inside a 90 minute step limit. The canary duration is unmeasured. A timeout ends with exit code -1. It is neither a pass nor a vendor failure. Record it as a budget fault. Raise the limit only with a measured CI duration.

### 7.5 CI documentation

`docs/development-setup.md` gains the LAC repository registration, the fourth gate, and the rule that a gate skip is a CI failure when the run set the gate. The text follows the style of the existing CI section.

## 8. Risks

- **Environment split.** Dev has LAC and CI had none. Section 5 orders 2a, then C1, to close it.
- **Vendor drift between pin and run.** A drift fails with a named message. The canary is the early signal.
- **Static state.** `ReplacementTextureAttestation.Versions`, the NDMF plugin-disable key, and `EditorPrefs` need a reset in `finally`. `VendorSessionStateGuard` covers the NDMF key. The four LAC-shaped classes reset per test.
- **Scripting defines.** LTCGI and Red Sim Light Volumes write defines on load. A define change can force a recompile mid-run. No stage tries to prevent it.
- **Cold-session cost.** First use of real lilToon and Poiyomi shaders compiles variants. Stage 4 measures it.
- **LAC internal API.** The component type is internal. A LAC release can rename a field. The harness asserts the version and the field names.
- **Skip inflation.** More gates mean more skips on a dev machine. The run record block lists them by reason and stays the single source.
- **Strict skip guard.** The unknown-skip rule can fail a run for a harmless skip. The first-run review in C3 bounds this risk.
- **Admission scope.** A digest admission widens what AMUSE trusts. Each admission needs a dated record, a diff review, and a RED test.
- **Network in CI.** The real Thry lock calls a vendor counter over the network. The proving run observes whether the low user can reach it.

## 9. Out of scope

- No production change, except the corrected LAC comment and, if a future ground check demands it, a dated digest admission.
- No change to the research package.
- No test in the Census Lab project.
- No commit, push, or PR under this record. The owner authorizes those.
- No change to vendor packages.

## 10. Open decisions for the owner

1. CI placement. This record proposes the PR gate runs `AMUSE_REAL_LAC_PASS` and `AMUSE_REAL_THRY_LOCK`, proved by the C2 pull request itself. The other two gates stay canary only. Is that right?
2. May C1 land as its own change, after stage 2a, before the other test stages?
3. May an unknown or gate-unreached skip fail a CI run (section 7.3)? This is stricter than the rule as of 2026-11-09.
4. Does owner decision 4 ("real Poiyomi lock tests sit behind an opt-in switch") cover the new gated unlock-window tests of section 4.4 step 4? The design says yes.
5. Should the root `README.md` line about d4rk change to match the package README (section 4.6)?

## 11. Review log

Four reviewers checked this record on 2026-11-09. Each tested its claims against the repository. None found a documentation-rule hit in the paths, ports, hashes, or names. Fixes applied after review:

- Replaced the stage 0 counts. The earlier draft said 158 tests did not complete. The suite run record shows 3241 leaves and 19 gate skips. The 3399 total counts suite nodes. The refresh record section 13 has the same correction.
- Rebased stage 1 on the existing `RealSourceGroundCheckTests`. Dropped the open admission branch for lilToon 2.4.1 and Poiyomi 10.0.29. The tests passed.
- Added stage 2a and the C1 ordering edge. C1 alone would turn the PR gate red.
- Resolved the unlock-window default flip (section 4.4). The earlier draft hid a coverage change.
- Gave seam-dropped LAC tests a named skip and a gate rule.
- Split the reset rule so the harness avoids repeated 30 second polls.
- Named both LAC backend preferences.
- Listed the real shared files for stages 3 and 4, and the files that the new gate touches.
- Replaced the canary-first promotion with a proving run in the C2 pull request. The canary floats versions and cannot prove pinned tests.
- Made C3 specify its parser change, the workflow changes, a real-file observation step, and an unknown-skip rule. Removed the canary exception that no code supports.
- Removed the Pester update bullet. No Pester test reads the real lock files.
- Added the rollback and ownership notes.
- Replaced the unqualified time words with dates.
- Named the two README files and their disagreement.
