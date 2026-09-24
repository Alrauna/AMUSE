# Locked Poiyomi Build Hermeticity Implementation Plan

> **For implementation:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Follow the approved tasks in order. Review each task before proceeding. Do not stage or commit.

**Goal:** Make the transient unlock window hermetic. Remove every vendor lock and unlock call for the swapped source pairs, revert references to the untouched locked original at close, and give the Census Lab a corpus-reset preflight.

**Architecture:** The restore becomes an AMUSE-owned in-memory reconstruction that reads the locked serialization and the recorded tags, so no vendor call and no asset write happen before apply. The close pass makes the existing fallback its primary outcome: references revert to the locked original, and the vendor relock survives only for generated canonical outputs on the play path, after all evidence capture. The Lab preflight snapshots the corpus, restores drift before each probe run, and fails on drift after it.

**Tech Stack:** Unity 2022.3 Editor, C#, NDMF, NUnit EditMode tests, the existing test-only Thry stand-in, and Lab-private editor scripts.

**Spec:** `docs/superpowers/specs/2026-09-23-locked-poiyomi-build-hermeticity-design.md`

## Global Constraints

- Production code stays under `Packages/com.alrauna.amuse/Editor/`. Tests stay under `Packages/com.alrauna.amuse/Tests/Editor/`.
- Phase A creates no repository file. The preflight script and baseline live only in the private Census Lab project.
- Do not add packages, dependencies, shader conversion families, public APIs, or census schema changes. No refusal enum member is added or removed.
- Do not change source assets, authoring materials, shader attestation pins, the semantic frontends, the classifier, or apply's mutation sequence.
- Product tests run only in the dev editor instance. Before each run, verify `Application.dataPath` equals `<repo-root>/Assets`. Never use the Census Lab editor instance for product tests.
- Lab work runs only in the Census Lab editor instance after the same identity check against that project. The Lab work is read-only over the product package.
- Do not upload, use the network, or report an uploaded-bundle result.
- Do not stage, commit, push, create a pull request, or change branches.
- Use RED/GREEN for every product behavior change. Record observed test counts. A filtered run with zero tests is a failure.
- Use fresh isolated implementer agents for the product tasks. Run one implementer at a time. The controller runs every RED and GREEN check.
- Use only repository-relative paths in records. Do not record editor instance names, hashes, ports, machine paths, GUIDs, or private asset names.

---

## Plan Record

Privacy note: This record contains no private avatar, scene, material, renderer, or asset identifiers. It describes product code, synthetic tests, and Lab tooling by role.

Date: 2026-09-23.

Status on 2026-09-23: Phase A is complete. The Lab preflight script and its baseline exist only in the private Lab project. The baseline covers about 3,900 watched files and byte-backs about 3,700. The first restore removed 27 vendor-generated locked shader folders across vendor source areas. A material survey found 12 materials with unresolved shaders out of more than 2,000, and their shader references have zero overlap with the deleted files, so that breakage predates the restore. A repeat restore changed nothing. One probe run on the locked-material test avatar, whose slot materials currently sit in their unlocked state, reported accepted=True with probeVersion=v7-preflight and a preflight PASS with zero drift. No generated folder appeared in any vendor source area after the run. Two minor script findings are parked for the Phase B final review. Phase B did not start. Its stop condition holds because branch `fix/poiyomi-generated-material-locking` has not landed on `main`.

Base branch: `main`, after branch `fix/poiyomi-generated-material-locking` lands. Phase A has no product dependency and may start immediately. Stop Phase B if that branch is not yet on `main`.

Owner override on 2026-09-23: Phase B proceeds on branch `fix/poiyomi-generated-material-locking` with its uncommitted changes present. The dependency is on that branch's behavior, not on `main`. Phase B edits to files that carry uncommitted changes stay additive in non-overlapping regions. The pre-Phase-B dirty file set is recorded in the session ledger and frames the Task B4 diff check.

Status on 2026-09-23, Phase B complete. All four tasks ran on the working branch under the owner override. The subsystem runs 47 of 47 across the five transient-unlock and reconstruction classes. The full product and research assemblies completed 2,381 cases with six failures: the five known pre-existing failures (four Avatar Optimizer merge cases and one degenerate scale-offset case) and one gating-contract test, which was updated to the narrowed play-path refusal and passes. The Census Lab probe with the Phase B product code reported accepted=True, a preflight restore of one externally drifted material before the run, and a preflight PASS with zero drift after. The session log shows zero vendor-generated locked shader writes anywhere in the run and no vendor call inside any AMUSE pass. The one ShaderOptimizer frame in the window is the SDK upload callback after NDMF, which is the documented ecosystem residue. Scope amendments are in the session ledger: the reference-ripple rename in the swap-in, the polarity correction, and three unowned test files updated under ruling.

## Allowed Mutations

Phase A, Lab project only, no repository file:

- Create the preflight script and its baseline snapshot under the Lab's private launcher folder.

Phase B, repository files:

- Create `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialReconstruction.cs`.
- Create `Packages/com.alrauna.amuse/Tests/Editor/Build/LockedMaterialReconstructionTests.cs`.
- Modify `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockAvailability.cs`.
- Modify `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`.
- Modify `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`.
- Modify `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockVendorStandIn.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTestLifecycle.cs`.
- Update the investigation, the spec, and this plan record.

No `.meta` file, shader asset, or fixture content changes except the new `.meta` files Unity creates for the two new scripts.

---

## Phase A: Census Lab corpus-reset preflight

### Task A1: Baseline snapshot and manifest

**Files:**

- Create, Lab project only: a preflight script under the private launcher folder.
- Create, Lab project only: a baseline snapshot folder and a digest manifest inside the Lab's private area.

**Consumes:** The Lab's authoritative scene corpus folder, test prefab corpus folder, and the vendor material folders the corpus references.

**Produces:** `CorpusResetPreflight.Snapshot()`, a Lab-private static method that writes the baseline and the manifest, and `CorpusResetPreflight.Restore(out List<string> restored)`, which restores every drifted or unexpected file and returns what it restored.

- [x] Enumerate the reachable Unity editor instances read-only. Require exactly the Census Lab instance. Verify its normalized `Application.dataPath` equals that project's `Assets` folder. Stop on any mismatch or on a second reachable instance. A second instance was reachable, so the exact Lab instance was pinned and its identity verified before every Lab call.
- [x] Write the preflight script. The manifest covers the corpus folders and stores relative paths with file digests. The baseline copies file bytes. Both live under the Lab's private area.
- [x] Take the baseline. Record the file count as a range. The baseline excludes vendor-generated locked shader folders everywhere, so the known source-area pollution of 2026-09-23 stays outside the manifest and the first restore removes it. Observed about 3,900 files, about 3,700 byte-backed.
- [x] Run `Restore` against the current state. Observe it remove the generated locked shader folder and its `.meta` files from the vendor area and report it in the restored list. Record the observed restored count. Observed restored=0 removed=27 unrestorable=0. The pollution was systemic across many vendor areas, not one folder. A shader-reference survey proved the removal broke nothing.
- [x] Re-run `Restore`. Observe an empty restored list. Observed restored=0 removed=0 and a clean assert.

### Task A2: Preflight around the probe run

**Consumes:** Task A1's snapshot and restore, plus the existing Lab probe entry point.

**Produces:** `CorpusResetPreflight.AssertUnchanged(out List<string> drifted)`, which re-digests the corpus and names every drift. The probe launcher calls `Restore` before a run and `AssertUnchanged` after it.

- [x] Wire `Restore` before and `AssertUnchanged` after the probe run in the Lab launcher. The probe version moved to v7-preflight.
- [x] Run the probe once on the locked-material test avatar. Observe the probe summary report its accepted result and `AssertUnchanged` report zero drift. Observed accepted=True and preflight PASS with zero drift, about 150 seconds for the run.
- [x] Confirm by folder listing that no generated locked shader folder appeared inside any vendor source area after the run. The NDMF generated container is out of scope for this assertion. Confirmed, zero matches.
- [x] Record the observed outcomes in this plan record, dated. Note that this observation predates the Phase B product change, so container writes from the vendor relock may still occur. The assertion scope is the corpus folders only.

---

## Phase B: Hermetic window in the product

### Task B1: In-memory reconstruction

**Files:**

- Create `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialReconstruction.cs`.
- Create `Packages/com.alrauna.amuse/Tests/Editor/Build/LockedMaterialReconstructionTests.cs`.

**Consumes:** `LockedMaterialIdentity` tag and flag names, the existing original shader pins, and the era's characterized rename rule: the suffix is the cleaned material name and suffixed entries carry the values of the plain names.

**Produces:** `LockedMaterialReconstruction.TryApply(Material clone, Func<Shader, bool> originalShaderAttested, out string refusalDetail)`, a pure editor-API transform that returns `true` only on a complete reconstruction. It reads the clone's own serialization and tags, resolves the original shader through the recorded GUID, assigns it, clears the optimizer flag, restores the keyword set from the recorded keywords tag, and moves every rename-suffixed serialized value onto the plain property name the original shader declares.

- [x] Write the failing tests first. The eight required cases were written first with falsifier comments. The plan's compile-fail RED gate was structurally unavailable because implementer and test runner are split across agents. The observed RED came from the first focused run: four of eight failed, including two production defects in the serialized-property reader. The fixture guards failed loudly rather than false-passing.
- [x] Run the focused test. Observed RED: four of eight failed. Two fixture failures were typed-setter writes that no-op on undeclared names in Unity 2022.3. Two were production NREs: the vectors container is absent on this serialization and element keys exist in two historical shapes.
- [x] Implement `LockedMaterialReconstruction`. Serialized saved properties only, no vendor types, no writes. A review fix round routed colors-container entries onto Vector-declared plain names because Vector4 values share the colors storage on this serialization.
- [x] Run the focused test. Observed GREEN: eight of eight passed, zero failed, zero skipped, 1.04 seconds.
- [x] Add the characterization guards. The unresolvable GUID and failed attestation cases refuse with distinct fixed details.

### Task B2: Production restore and narrowed gating

**Files:**

- Modify `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockAvailability.cs`.
- Modify `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTestLifecycle.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs`.

**Consumes:** Task B1's `TryApply`, the existing `TransientUnlockDelegate` signature, `HostLifecycleCapability` with its build path, and the existing `WindowEligibleForConsent` and `RendererPreCheckRefusal` seams.

**Produces:** `TransientUnlockAvailability.CreateProductionRestore()` returns a delegate that runs `LockedMaterialReconstruction.TryApply` and converts its result to the vendor outcome enum. `TransientUnlockAvailability.RelockVendorReady()` answers the vendor question for the relock only. The pass supplies a path-aware vendor answer: `false` without consulting the vendor seam on non-play paths, the seam answer on the play path.

- [x] Write the failing gating tests first. Case one admits without a vendor on a non-play path. Case two refuses on the play path and is marked characterization.
- [x] Run both. Case one's own assertion read the pair state after the close drained it, so it failed for a test defect first. The controller ruling corrected the polarity the plan stated: the path-aware answer is true on non-play, the seam answer on play.
- [x] Replace the production restore body. `CreateProductionRestore` runs the reconstruction with a constant-true shader attestation, documented as relying on the admission gates that enforce the material-level pins.
- [x] Split the vendor readiness answer. `RelockVendorReady` answers the relock question. The pass supplies the path-aware answer. A ruled token rename in the swap-in covered the reference ripple.
- [x] Run both gating cases. Observed across the full transformation class after one fix round: 12 of 12 passed, zero failed, zero skipped, 11.04 seconds.
- [x] Run the full transient-unlock transformation tests. Observed RED: nine of twelve failed. Five pinned the vendor restore call count, two scripted a dead stand-in restore mode, and the two gating tests had their own defects. All were fixed under one root cause. A later review pass moved two swap-in restore-mode tests to real refusal injection, reaching 36 of 36 across the four classes.

### Task B3: Reference reversion as the primary close

**Files:**

- Modify `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`.
- Modify `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs`.
- Modify `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockVendorStandIn.cs`.

**Consumes:** The existing per-pair fallback machinery for slot arrays and committed curves, the 2026-09-23 play-path canonical output relock, and Task B2's path-aware vendor answer.

**Produces:** A close pass whose relock batch contains only live canonical outputs on the play path. Every open pair reverts its references to `L`, inverts its recorded closure writes, and destroys `U` only after the inversion completes. `TransientUnlockRelockFailed` reports only a failed canonical output lock.

- [x] Write the failing close tests first. Case one pins the reversion outcome on a non-play path with the delegate never invoked. Case two pins the play-path canonical lock with no unlocked clone in the batch.
- [x] Run both. Both failed against the old close, which relocked the unlocked clone, the expected RED.
- [x] Reorder the close. The canonical relock batch runs first on the play path, then every pair reverts through the fallback machinery, now the primary. The destroy-after-inversion rule and the retention refusal are intact.
- [x] Update the superseded tests. Relocked-clone assertions now assert the locked original. Every fallback, retention, and swept-output case is kept. The 2026-09-23 canonical lock tests keep their intent.
- [x] Narrow the report strings. The vendor-unavailable refusal names the play path and the canonical output lock. The relock-failure wording names generated outputs only.
- [x] Run the close and transformation classes. Observed across all five classes: 47 of 47 passed, zero failed, zero skipped, 18.33 seconds.

### Task B4: Validation and records

**Files:**

- Update `docs/superpowers/investigations/2026-09-23-locked-poiyomi-build-hermeticity.md`.
- Update the spec's status section and this plan record.

- [x] Enumerate the Unity editor instances. The dev editor instance was pinned and its normalized `Application.dataPath` verified as `<repo-root>/Assets` before every run. The Lab instance was pinned and verified separately for the Lab step.
- [x] Read the Unity console. Zero C# compiler errors after each compile.
- [x] Run the focused filters. Reconstruction, gating, close, swap-in, and availability classes: 47 of 47 passed.
- [x] Run the full assemblies. 2,381 cases completed with six failures: the five known pre-existing failures (four Avatar Optimizer merge cases, one degenerate scale-offset case) and one gating-contract test. The gating test was updated to the narrowed play-path refusal and passes one of one. The research assembly listed no failures.
- [x] Run the Lab preflight around one probe run. Accepted=True, probeVersion v7-preflight, one externally drifted material restored before the run, preflight PASS with zero drift after. The session log shows zero vendor-generated writes in the run and no vendor call inside any AMUSE pass. The single ShaderOptimizer frame is the SDK upload callback after NDMF, the documented residue.
- [x] Inspect the unstaged diff and `git status`. Every change sits inside the pre-Phase-B dirty set plus the ruled task files, the two new scripts with Unity-created `.meta` files, and the six record documents. `git diff --check` is clean.
- [x] Sweep the changed documents. The identifier sweep found no hits. The records use short active sentences with no semicolons or contractions.

---

## Stop Conditions

Stop and return evidence if the reconstruction cannot pass the verification contract on the synthetic era fixture, if the reversion close cannot prove the committed-curve inversion complete, or if the play-path canonical lock regresses on its 2026-09-23 tests. Stop before adding a second vendor era, changing the semantic frontends or the classifier, widening census schema, or moving the preflight into the product or research packages. Do not run product tests in the Census Lab. If the Lab identity check finds more than one reachable editor instance, stop and report.

## Expected Report

Report the changed files, the observed RED and GREEN results per task, the focused and full EditMode test counts, the Lab preflight observations with dates, the identifier-sweep result, and any remaining limit. State that no upload ran. Do not stage or commit changes.

## Self-Review

- The reconstruction requirement maps to Task B1. The gating narrowing maps to Task B2. The reversion close and the surviving play-path output lock map to Task B3.
- The corpus preflight maps to Phase A, which has no product dependency and lands first.
- No refusal enum member changes, so no census row changes.
- The swap-in, the unlocked world, the object registry provenance, and the fallback discipline of the 2026-09-20 design are preserved. Only the two superseded vendor invocations are removed.
- The plan adds no dependency, public API, conversion family, or new product subsystem.
