# Poiyomi Generated Material Locking Implementation Plan

> **For implementation:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Follow the approved tasks in order. Review each task before proceeding. Do not stage or commit.

**Goal:** Lock AMUSE-generated Poiyomi opaque materials during Play mode builds.

**Architecture:** Carry each admitted source family beside the prepared source-to-output mapping. The existing close pass adds only Poiyomi outputs from open transient pairs to the Play mode relock batch. A failed output lock uses the existing per-pair fallback and restores references to the original locked material.

**Tech Stack:** Unity 2022.3 Editor, C#, NDMF, NUnit EditMode tests, and the existing test-only Thry stand-in.

**Spec:** `docs/superpowers/specs/2026-09-23-poiyomi-generated-material-locking-design.md`

## Global Constraints

- Production code stays under `Packages/com.alrauna.amuse/Editor/`.
- Tests stay under `Packages/com.alrauna.amuse/Tests/Editor/`.
- Do not add packages, dependencies, or shader-conversion families.
- Do not change source assets, authoring materials, the upload callback, or the Census Lab project.
- Run tests only in the dev editor instance. Before each run, verify `Application.dataPath` equals `<repo-root>/Assets` and pin that editor instance.
- Do not upload, use the network, or report an uploaded-bundle result.
- Do not stage, commit, push, create a pull request, or change branches.
- Use RED/GREEN for both behavior changes. Record observed test counts. A filtered run with zero tests is a failure.
- Use fresh isolated implementer agents for the plan tasks. Run one implementer at a time. The controller runs every RED/GREEN test and every validation command.
- Use only repository-relative paths in records. Do not record editor instance names, hashes, ports, machine paths, GUIDs, or private asset names.

---

## Plan Record

Privacy note: This record contains no private avatar, scene, material, renderer, or asset identifiers. It describes product code and synthetic tests.

Date: 2026-09-23.

Status on 2026-09-23: The user approved this plan and requested subagent-driven development. The first test filter matched zero tests. The corrected filter ran two cases. The Play case failed at the expected locked-shader assertion. The non-Play case had no reported failure.
Status on 2026-09-23 (Task 1): The focused GREEN run executed two cases and both passed. The review found two low-priority documentation findings. Both were corrected. The console query found zero C# compile errors.
Status on 2026-09-23 (Task 2 RED): Two focused cases ran before production edits. The enumerated-graph case failed because a committed keyframe still held C. Its renderer-slot assertion passed. The graph-unavailable case passed. It is characterization, not RED.
Status on 2026-09-23 (Task 2 RED and GREEN): The appended-curve test failed before the fix. Fallback destroyed C while the committed curve still named it. The swept-output test also failed before its fix. The close batch included destroyed C. Both tests passed after their fixes. The focused run executed 19 tests. All passed. None were skipped.
Status on 2026-09-23 (Task 3): The final product run followed the last close-pass optimization. It completed 2,230 cases and listed five failures. The tool did not report pass or skip counts. The research run passed all 138 cases. None failed or were skipped. An earlier combined run reported 2,368 of 2,375 completed cases. It listed the same five failures.

Base branch: `main`.

Base commit: `b7d405c891212555390e5a7e334c3b1a51ca4692`.

Working branch: `fix/poiyomi-generated-material-locking`.

## Allowed Mutations

This work may change these existing files:

- `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`
- `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`
- `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`
- `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTestLifecycle.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockVendorStandIn.cs`
- `docs/superpowers/investigations/2026-09-23-poiyomi-generated-material-locking.md`
- This spec and plan.

The existing `LockedStandIn.shader` already uses a locked shader name and declares `_ShaderOptimizerEnabled`. Do not change it or its `.meta` file.

The transient-unlock tests use their test-owned folder `Assets/AmuseTests_TransientUnlock` and clean it in teardown. Run them only in the public project.

## Task 1: Lock generated Poiyomi outputs in Play mode

**Files:**

- Modify `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`.
- Modify `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`.
- Modify `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTestLifecycle.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockVendorStandIn.cs`.

**Consumes:** `PreparedSlotSeparation.OpaqueOfAdmitted`, `CapturedAlphaMaterial.Family`, the lifecycle build path, the existing lock verifier, and the existing test-only locked shader.

**Produces:** Each prepared slot carries `FamilyOfAdmitted`, keyed by the same source material as `OpaqueOfAdmitted`. The close pass can identify the Poiyomi canonical output derived from an open pair's `U`.

- [x] Add a build-path control to `TransientUnlockTestKnobs`. Reset it to `AmuseBuildPath.NonPlayNdmfBuild`. Pass its value from `TransientUnlockTestLifecycle.SupportedFacts()`.
- [x] Parameterize `UnlockedSlotRunsTheWholePipelineInsideTheWindow` for `NonPlayNdmfBuild` and `ApplyOnPlay`. For the Play case, assert that the assigned canonical output has a locked shader name and a lock flag of `1`. For the non-Play case, assert that the AMUSE close pass leaves the canonical output unlocked.
- [x] Run the focused EditMode test before production changes. The corrected run executed two cases. The Play case failed at the expected locked-shader assertion. The non-Play case had no reported failure.
- [x] Add `FamilyOfAdmitted` to `PreparedSlotSeparation` as an `IReadOnlyDictionary<Material, CapturedAlphaMaterialFamily>`. In `AlphaSeparationPreparation`, record `captured.Family` for each successfully mapped admitted source. Pass this map with the existing output map when constructing the prepared slot.
- [x] In `TransientUnlockWindowClose`, add a Poiyomi output to the relock batch only when the build path is `ApplyOnPlay`. Find it through the prepared slot's source-to-output map and family map. Deduplicate outputs. Keep every `U` in the batch.
- [x] Verify `U` and every added Poiyomi output with the existing locked shader name and lock-flag checks. A failed Poiyomi output makes its whole transient pair fail verification.
- [x] Update the test stand-in. It must restore the remembered shader for `U` and assign the existing `LockedStandIn` shader to a new generated output. It must set `_ShaderOptimizerEnabled` to `1` for both.
- [x] Refresh the dev editor after the script edits. Read the Unity console and confirm there are no compile errors. The corrected focused GREEN run executed two cases, and both passed.

## Task 2: Restore references when a generated output cannot lock

**Files:**

- Modify `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`.
- Modify `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`.
- Modify `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockVendorStandIn.cs` only if its current override cannot express a failed output lock.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTestLifecycle.cs` for configured locked materials and pre-apply test control.

**Consumes:** Task 1's prepared source-family and source-to-output maps, plus the existing slot and committed-curve fallback. The close pass looks up C from each pair's U.

**Produces:** A failed `C` lock restores every reference to `U` and `C` to `L`. The fallback keeps any clone alive if the committed animation graph cannot be enumerated.

- [x] Add `PlayModeCanonicalRelockFailureRestoresOriginalReferences`. Use `BeforeClose` to capture `U` and `C`. Assert that `C` is not `U`. Use `RelockOverride` to lock `U` but leave `C` unlocked.
- [x] Assert that the final renderer slot and every relevant committed object-reference keyframe hold `L`. Assert that `TransientUnlockRelockFailed` is recorded for the slot and that the close window has no open pairs.
- [x] When graph enumeration succeeds, assert AMUSE-owned `U` and `C` are destroyed after inversion.
- [x] Add a Play-mode retention case with committed graph enumeration unavailable. Require `L` in the renderer slot, `C` in the committed curve, `U` and `C` alive, the named retained-clone refusal, and an empty close window.
- [x] Run the enumerated-graph case before fallback edits. It failed because a committed keyframe still held `C`. Its renderer-slot assertion passed.
- [x] Run the graph-unavailable case before fallback edits. It passed before production edits. Keep it as characterization that protects `C` while a curve may still reference it.
- [x] Add a locked source fixture that retains a configured Poiyomi split material. Use the existing synthetic split texture and mesh helpers. Keep shader assets unchanged.
- [x] Add `PlayModeCanonicalRelockFailureInvertsAppendedSplitCurve`. Require a Split plan and C in appended material slot 2 before close. Require L in that renderer slot and committed curve after fallback.
- [x] Run this test before the curve-inversion fix. It failed because fallback destroyed C while the appended committed curve still named it.
- [x] Add `PlayModeRelockDoesNotRefuseSweptCanonicalOutput`. Change the renderer mesh before apply. Require apply to refuse and sweep C. Require close to batch only U, keep the replacement mesh, retain U in the slot, and record no relock-failure refusal.
- [x] Run this test before the destroyed-output fix. It failed because the close batch included swept C.
- [x] Skip destroyed Poiyomi outputs when collecting outputs from prepared slots.
- [x] Extend the per-pair fallback to replace references to U and its Poiyomi outputs with L in renderer arrays and committed object-reference curves.
- [x] Destroy AMUSE-owned U and C only after the committed graph was enumerated and references were inverted. If graph enumeration fails, keep the possible references alive and record `TransientUnlockCloneRetained` for affected slots.
- [x] Update the refusal comments and report strings. State that a lock failure can involve U or a generated Poiyomi output. State that retained build copies can include either material.
- [x] Read the Unity console and run both relock-failure cases, the Play success case, the non-Play case, and `TransientUnlockWindowCloseTests`. The focused run executed 19 tests. All passed. None were skipped.

## Task 3: Run final validation and update records

**Files:**

- Update `docs/superpowers/investigations/2026-09-23-poiyomi-generated-material-locking.md` with the dated behavior decision and observed implementation result.
- Update the task checkboxes in this plan only after each step passes.

- [x] Enumerate Unity editor instances. Select the dev editor instance. Read its project info. Require the normalized `Application.dataPath` to equal `<repo-root>/Assets`. Stop if it does not. Never select the Census Lab editor instance.
- [x] Read the Unity console. Confirm the changed scripts compile before starting tests. The console returned zero C# compiler errors.
- [x] Run EditMode tests for `TransientUnlockTransformationTests` and `TransientUnlockWindowCloseTests` in `Alrauna.Amuse.Tests.Editor`. The run executed 19 tests. All passed. None were skipped.
- [x] Run the full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` EditMode assemblies in the dev editor. The final product run followed the last close-pass optimization. It completed 2,230 cases and listed five failures. Pass and skip counts were not returned. The research run passed 138 cases. None failed or were skipped. An earlier combined run reported 2,368 of 2,375 completed cases. It listed the same five failures.
- [x] Inspect the unstaged diff and `git status`. Confirm that only the allowed files changed. Run `git diff --check`.
- [x] Sweep the changed documents for private asset names, `@` followed by a hexadecimal hash, drive-letter paths, home-directory paths, and four-digit ports. Fix every hit. Confirm the documents use short active sentences and contain no semicolons or contractions.

## Stop Conditions

Stop and return evidence if the first RED failure is not the expected unlocked canonical output, if the public project path cannot be verified, or if the fallback needs behavior beyond restoring references to `L`. Stop before adding lilToon behavior, changing the upload path, adding dependencies, or creating a new subsystem. Do not run tests in the Census Lab.

## Expected Report

Report the changed files, the observed RED and GREEN results, the focused and full EditMode test counts, any skipped validation, the identifier-sweep result, and any remaining limit. State that no upload ran. Do not stage or commit changes.

## Self-Review

- The Play mode lock requirement maps to Task 1.
- The output-lock failure fallback maps to Task 2.
- The dev-project identity check, focused tests, full assemblies, privacy sweep, and Git boundary map to Task 3.
- The normal SDK preprocess path remains unchanged.
- The plan adds no conversion family, dependency, public API, or shader asset.
