# Locked Source Transform Conformance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Owner approval of an execution method is required before implementation.

**Goal:** Correct the three proved source-model gaps without guessing the later stochastic outcome or admitting bundled locked originals.

**Architecture:** Keep the existing pure aligner interface. Add an installed-era non-removal proof, a shared Properties lexical scan, and tentative occurrence inference backed by exact installed-era replay. Retain the existing reproduction and residual checks. Characterize the frontier separately from completing these rules and from admission.

**Tech Stack:** C# in Unity 2022.3, NUnit and Unity Test Framework EditMode, the installed Thry optimizer, and Unity MCP for dev-project validation. No new dependencies.

**Spec:** `docs/superpowers/specs/2026-10-28-locked-source-transform-conformance-design.md`, decisions LA1–LA10 and falsifiers LA-F1–LA-F12.

Privacy note. This plan contains public product paths and synthetic fixture requirements only. It contains no private observations, machine paths, editor identities, generated cache identifiers, or vendor shader copies.

Status: 2026-10-28. Reviewed plan awaiting owner approval for implementation. Independent adversarial reviews and scoped re-reviews accepted the saved corrections. No implementation, test run, Unity action, staging, or commit is authorized or claimed by this plan-writing task.

## Global Constraints

- Unity 2022.3 and C# in the existing production assemblies only. No new dependencies.
- The aligner stays pure. Its input remains original text, generated text, and the original property table.
- Original shader bytes must pass attestation before product use. Generated text and live locked values remain the truth for reconstruction.
- Unknown information refuses only the conclusion that depends on it. Programming and invariant failures still throw.
- Preserve exact kept text, literal round trips, mixed-use refusals, full generated consumption, and agreement among successful eras.
- Keep both bundled admission flags false throughout this work.
- No production or test execution is authorized by this document alone. Never stage, commit, push, publish, or deploy without explicit authorization.
- Never mutate source assets or vendor packages. Unity asset writes and tests may run only in the verified dev project, never in the Census Lab project.
- Human-readable text uses simple English. Records contain no private identifiers, machine paths, instance identities, ports, or vendor shader copies.

All package-relative paths in the task lists start at `Packages/com.alrauna.amuse/`. Paths starting with `docs/` are repo-relative. Do not create or switch branches until workspace ownership and implementation authorization allow it. There are no commit steps in this plan.

## Review Focus

1. A retained frame runs inside another trial's lookahead. It must preserve cursor and growth ownership. Task 2 pins this with `RetainedFrameInsideLookaheadDoesNotAdoptPastItsOwner`.
2. A display string contains fake declarations or Gamma attributes on continuation lines. None can enter the property table. Task 3 pins this with `MultilineDisplayTextCannotCreateGammaProperties`.
3. Final hint deletion exposes a property-like spelling that the vendor never tokenized. It must not become a bake. Task 4 pins this with `HintDeletionCannotCreateABakedPropertyUse`.
4. A losing trial visits an integer declaration. It must not change the winning literal format. Task 4 pins this with `LosingTrialDoesNotLeakDeclarationFormat`.
5. A vendor cache hit or queued save changes data after cleanup. Runtime work must stop before reuse and finish queued work before deletion. Task 1 pins the preflight decisions and the final byte-equality smoke proof.

## File map and task ownership

| File | Owner and responsibility |
|---|---|
| `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs` | Tasks 2–4. Eligibility, lexical state, occurrence replay, and candidate-local format state |
| `Editor/Build/LockedSourceAlignmentMemo.cs` | Task 3 only if needed to propagate the invalid attested-original Gamma invariant. No memo key change |
| `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs` | Tasks 2–4. Precise synthetic transform falsifiers |
| `Tests/Editor/Semantics/Poiyomi/LockedSourceReproductionTests.cs` | Tasks 3–5. Gamma plan values, literal associations, and residual check limits |
| `Tests/Editor/Build/LockedMaterialReconstructionTests.cs` | Tasks 3 and 5. Original property-table integration and actual restored values |
| `Tests/Editor/Build/TransientUnlockSwapInTests.cs` | Task 5. Retain and exercise existing seventh-fact protection |
| `Tests/Editor/Build/BundledLockedOriginalRealLockTests.cs` | Tasks 1 and 6. Safe owned lifecycle, deferred-work handling, and assigned-texture cases |
| `docs/superpowers/investigations/2026-10-28-locked-source-transform-conformance-measurement.md` | Tasks 1 and 6. New sanitized evidence, not copied earlier results |
| `CHANGELOG.md` | Task 6. Observed coverage changes without an admission claim |

No new production file or assembly is planned. Do not edit `PoiyomiSourceAttestation.cs`. Do not rewrite the older admission plan as if its failed gate passed.

## Verification commands and result rules

Use `run_tests` through the mounted Unity MCP route, with `mode: "EditMode"` and `include_failed_tests: true`. Read the tool schemas at execution time. Every runtime action first pins and verifies the exact dev project. A test name with zero matched cases is a failed filter, not a pass.

Focused filters used below:

- **Alignment:** `test_names: ["Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiLockedSourceAlignmentTests"]`.
- **Reproduction:** `test_names: ["Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.LockedSourceReproductionTests"]`.
- **Reconstruction:** `test_names: ["Alrauna.Amuse.Tests.Editor.Build.LockedMaterialReconstructionTests"]`.
- **Swap-in:** `test_names: ["Alrauna.Amuse.Tests.Editor.Build.TransientUnlockSwapInTests"]`.
- **Real-lock cases:** the twelve named real-lock methods in Task 6, with `AMUSE_REAL_THRY_LOCK` explicitly enabled only for that operation.
- **Full product and research:** `assembly_names: ["Alrauna.Amuse.Tests.Editor", "Alrauna.Amuse.Research.Tests.Editor"]`, with the real-lock gate unset.

Record observed matched, passed, failed, skipped, and inconclusive counts. Report a named missing-vendor skip as unexercised, never as proof. Use the existing asynchronous test-job mechanism. Do not rerun a user-reported failure merely to confirm it.

For RED, run the newly added or changed behavior case before its production fix. Record the actual failure and the plausible wrong implementation it catches. Cases that pass first are characterization. A missing-symbol compile error does not by itself prove the intended behavior is wrong. Add only the smallest declarations needed to compile before observing the behavior failure.

After each production task, refresh Unity if needed, read the console, run that task's focused filters once after integration, then run its named throwaway smoke. Do not run broad builds or formatters between partial edits. Do not treat tests alone as smoke proof. Keep throwaway drivers in memory where possible and remove all disposable assets.

Every production implementation step must add a short load-bearing comment that cites its LA decision and the spec path. Do not add comments that merely restate code. Review each comment against the actual proof obligation before closing that task.

---

## Task 1: Establish safe real-lock ownership and trace the frontier

**Spec:** LA5 and LA9. No production change.

**Files:** Modify `Tests/Editor/Build/BundledLockedOriginalRealLockTests.cs`. Create the dated measurement record only after an observation exists.

**Interfaces:** Keep the existing `RunRealLockCase(LockCase lockCase)` behavior assertions. Add private test-only `LockIsolationSnapshot`, `CaptureLockIsolation()`, and `EvaluateLockIsolation(LockIsolationSnapshot snapshot, bool fixtureExists, bool unrelatedDirtyAssets)`, returning private enum `LockIsolationRefusal`. Its values are `None`, `CacheNotEmpty`, `MetadataMismatch`, `FixtureExists`, `UnrelatedDirtyAssets`, and `BlockedOwnership`. Add `HasPendingVendorWork(Type vendorTool)`, returning `bool`. Use `[UnityTearDown] public IEnumerator FinishOwnedLockCase()` for bounded editor-tick drainage, then a finally-protected deletion and verification phase. Retain sticky active-case ownership and a blocked flag until cleanup succeeds. Pure preflight tests leave ownership empty and never call the vendor.

- [ ] **Step 1: Inspect runtime prerequisites without writing.** Pin the dev instance and verify its Assets folder. Check the vendor assembly, existing opt-in mechanism, fixture and metadata absence, cache and root metadata state, unrelated dirty persistent assets, effective teardown timeout, and absence of another build or test operation. Do not enable the gate if any ownership check fails. Record the missing prerequisite without asset identifiers.
- [ ] **Step 2: Write deterministic preflight tests in the existing suite.** `ExistingCacheEntryRefusesLockIsolation` asserts `CacheNotEmpty` for one synthetic non-README entry. `OnlyReadmePairAllowsLockIsolation` asserts `None` for the exact paired baseline. `AbsentCacheWithoutMetadataAllowsLockIsolation` asserts `None`. `CacheDirectoryRefusesLockIsolation` and `CaseChangedReadmeIsNotTheOwnedBaseline` assert `CacheNotEmpty`. Orphan root metadata and a root without metadata assert `MetadataMismatch`. An existing fixture directory or orphan fixture metadata asserts `FixtureExists`. An unrelated dirty persistent asset asserts `UnrelatedDirtyAssets`. Sticky ownership asserts `BlockedOwnership` and is not cleared by evaluating a later case. These tests use plain snapshot records, never the real cache. Observe behavior failures before implementing the decision. Do not test exact prose or use bare not-throw as the positive assertion.
- [ ] **Step 3: Implement the owned lifecycle.** Add `[NonParallelizable]` to the suite. Enforce the preflight verdict before the vendor call. Capture root existence, root metadata existence and bytes, relative entry sets, and file digests. Move the `OverrideTemporaryDirectoryScope` and `BuildContext` output to an owned `BuildOutput` child under `FixtureFolder`. Remove this suite's unconditional deletion of `TransientUnlockTestLifecycle.TempFolder`. Refuse existing fixture paths and orphan metadata instead of reusing them. Move real-case cache deletion out of the runner's immediate finally path and into `FinishOwnedLockCase`, so callbacks can finish while materials still exist. Keep transient clone destruction and context disposal exception-safe. Never suppress the original test failure or overwrite active ownership with a later case.
- [ ] **Step 4: Define deferred-work observation and its watchdog.** Verify the Unity update delegate interface through reflection. Observe the installed vendor's `VerifyLockedShaders`, `QueueSaveAfterLockUnlock`, and `SaveAfterLockUnlock` subscriptions, and shader compilation. The installed test framework has a 180-second default teardown timeout, but an inherited nonzero timeout wins. Its timeout exit does not dispose the inner teardown iterator. Use a ten-second editor-time drain watchdog and refuse runtime preflight unless the effective teardown timeout is strictly longer. Do not add an unverified timeout attribute. While pending, yield editor ticks. On watchdog expiration, set sticky blocked ownership, fail the test operation, and prohibit later locks. Do not delete assets referenced by pending work. Resume only after observing callback removal, then run explicit cleanup and snapshot proof. This watchdog never selects an aligner outcome. Never invoke, unregister, or clear vendor callbacks yourself. If subscription state cannot be observed, stop before locking.
- [ ] **Step 5: Run the preflight tests ungated.** Use their exact fully qualified method names under `Alrauna.Amuse.Tests.Editor.Build.BundledLockedOriginalRealLockTests`. Expected: each named preflight test matches and passes after the change. Before the change, the new unsafe-state expectations must have an observed behavior failure once compiled.
- [ ] **Step 6: Perform one disposable lifecycle smoke.** After runtime authorization and safe preflight, lock one synthetic material with the installed vendor. Let editor callbacks finish. In the teardown's finally-protected cleanup, delete owned materials and textures, fixture folder, and only newly created cache entries. If the root was absent initially, remove the new root and its metadata. Observe exact snapshot equality and fixture absence. Do not run all ten known failing cases for this step.
- [ ] **Step 7: Attempt the read-only frontier trace.** On fresh in-memory captured text, reproduce the prior diagnostic guard normalization and, for the renamed case, the display-string brace normalization. Do not change source or generated shader assets. Use a debugger or in-memory development trace to capture the first inner comparison failure and its trial ownership. Record original and generated cursors, comparison category, block source line, growth boundary, and lookahead state. Capture no vendor text in the durable record. If the debugger or in-memory trace is unavailable, record exactly that prerequisite. This is not proof of the cause. W1–W3 can proceed, but no fourth rule can.
- [ ] **Step 8: Close this task with evidence.** Record what actually ran and the final snapshot proof in the dated record. A missing trace is a separate unresolved frontier milestone, not successful characterization. Remove the development trace and all created assets. Do not add a permanent diagnostic interface.

## Task 2: Prove installed-era guard non-removal

**Spec:** LA2. **Falsifiers:** LA-F1–LA-F4 and the guard half of LA-F12.

**Files:** Modify `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs` and `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`.

**Interfaces:** Preserve `Align(string originalText, string generatedText, IReadOnlyList<LockedSourceProperty> properties)` and `AlignForEra(..., PoiyomiLockEra era)`. Add private `BuildPotentialPragmaIndex(IReadOnlyList<LineRef> lines)`, returning `Dictionary<string, int>`, and `HasProvedNonInliningIncludes(IReadOnlyList<LineRef> lines)`, returning `bool`. Store their immutable results on `WalkState` and share them across clones. Add private `MustRetainGuard(WalkState state, int originalIndex, string vendorKeyword)`, returning `bool`. Factor the existing kept-frame traversal so forced retention and speculative kept trials use the same nesting rules.

- [ ] **Step 1: Add installed-era behavior tests.** `GuardBeforeItsPragmaKeepsTheCompilerFrame` uses a nested retained-frame fixture that the existing speculative resolver refuses. Assert success, outer `Kept`, exact surviving frame text, and no invented keyword demand. `HarvestedNonPropertyGuardCanStillBeRemoved` places the pragma first and asserts the correct removed record. `ExemptKeywordKeepsBothCompilerBranches` harvests an exception keyword but retains both branches. Mutate one body line in each positive shape and require refusal. Observe the actual RED case rather than assuming every new test fails.
- [ ] **Step 2: Add ownership and conservative-proof cases.** `RetainedFrameInsideLookaheadDoesNotAdoptPastItsOwner` nests a retained frame inside an enclosing hypothesis and puts a contradicting line after that owner's extent. Assert correct outcome and final full consumption. `RetainedOuterFrameKeepsRemovedInnerFacts` checks a removable inner guard, outer else body, and exact baked property association. `ExcludedPragmaDoesNotProveActualHarvest` preserves the overapproximation corner and rejects a record that invents a forced outcome. `UnprovedIncludeCannotEnableForcedRetention` and its case-changed-operand variant use an otherwise ambiguous retained-frame fixture that must not gain acceptance through this optimization. Include a positive control with the exact proved include spelling. `RepeatedGuardTextDoesNotWaiveAmbiguity` keeps the existing duplicate-body protection.
  - Add `SuccessfulErasWithDifferentGuardFactsRefuseSharedAlignment`. In a normal valid synthetic Properties and Pass scaffold, put `#pragma shader_feature K` before `#ifdef K // synthetic note`, then a plain body line, `#endif`, and a distinct plain tail line. Body and tail contain no property reads. Generated text drops the pragma, inserts the enabled `K` define and optimizer define immediately after `CGPROGRAM`, and retains the full guard comment and body. Assert that direct historical and installed-era calls both succeed. Assert their kept block operands are exactly `K // synthetic note` and `K`. Then assert shared `Align` refuses with `Source == null`. Keep the existing equal-facts `ErasThatAlignMustAgree` test too. This is a new disagreement test, not a claim that one already exists.
- [ ] **Step 3: Run Alignment.** Record the failing behavior cases and the characterization cases separately. Do not rewrite a failed expectation into whatever the current resolver reports without checking the fixture's vendor explanation.
- [ ] **Step 4: Implement the prefix proof and retained walk.** Follow LA2's exact ordinal prefix extraction and strictly earlier-index test. Initially prove only the two exact include operands in the spec. Disable the optimization for an unproved operand. Apply it only in `Thry2743`. If forced retained matching fails, propagate refusal. Never retry that outer frame as removed. Restore inherited counters and boundaries when leaving the frame. Keep `RecordSkippedBlocks` behavior for a removed parent.
- [ ] **Step 5: Run Alignment after integration.** Expected: no failures, including historical-era and existing ambiguity cases. Use the distinguishing retained-frame fixture in direct per-era calls to prove the historical guard contracts remain unchanged. Run the new `SuccessfulErasWithDifferentGuardFactsRefuseSharedAlignment` case and the existing equal-facts control.
- [ ] **Step 6: Smoke the changed path directly.** Through an in-memory call to the actual `AlignForEra`, run the positive nested retained-frame pair and its one-line mutation. Observe the exact outer and inner facts for the positive pair and refusal for the mutation. This is a disposable driver, not another permanent copy of the same tests.

## Task 3: Share validated multiline Properties state

**Spec:** LA3. **Falsifiers:** LA-F5–LA-F7.

**Files:** Modify the aligner and its tests. Use `Tests/Editor/Semantics/Poiyomi/LockedSourceReproductionTests.cs` and `Tests/Editor/Build/LockedMaterialReconstructionTests.cs` for the value and property-table checks. Modify `Editor/Build/LockedSourceAlignmentMemo.cs` only if needed for explicit invariant propagation.

**Interfaces:** Preserve `GammaPropertyNames(string originalText)` and `LockedSourceAlignmentMemo.PropertiesOf(Shader original, string originalText)`. Add private `ScanProperties(IReadOnlyList<LineRef> lines)` returning a private `PropertiesScan` result with validity, opening and failure line, window indices, and lexically valid declaration locations. All three consumers use this result. Invalid direct alignment input returns `LockedSourceAlignmentResult` failure. Invalid Gamma extraction throws `InvalidOperationException`. The memo caller must not catch that invariant into defaults.

- [ ] **Step 1: Write the valid-continuation falsifier.** `MultilineDisplayStringKeepsDuplicateRenamedProperty` has a multiline string with continuation braces, then a plain-plus-renamed duplicate and a later actual block close. Assert success, exact suffix, exact renamed name, and full consumption. Mutate only the duplicate and require refusal. Add an extra generated line and require refusal.
- [ ] **Step 2: Write metadata and malformed-input cases.** `MultilineDisplayTextCannotCreateGammaProperties` places fake Gamma and property text on a continuation line, then a real Gamma color. Assert only the real property name is returned. In reproduction, assert that property's planned RGB conversion and exact alpha, using the existing linear-color-space fixture convention. `MalformedOriginalPropertiesRefuses` and `MalformedGeneratedPropertiesRefuses` assert failure with no source record. `MalformedGammaSourceThrows` asserts the invariant exception. In reconstruction tests, pass malformed text to `PropertiesOf` with an existing valid synthetic shader and require the invariant to propagate. No invalid shader asset needs importing for this check.
- [ ] **Step 3: Replace the explicitly obsolete contract case.** Remove `UnclosedDisplayStringIsAccepted` as superseded by LA3's documented Y7 amendment. Keep valid escaped-quote, single-line brace, Gamma, rename, and duplicate tests. Add keyword-and-brace same-line and separate-line cases. Add comment markers inside a multiline string and a terminal-backslash continuation case. Require a malformed unfinished comment or block to refuse. Do not repin incidental wording tests.
- [ ] **Step 4: Run Alignment, Reproduction, and Reconstruction.** Record behavior failures before changing the scanner. The pre-existing escaped-quote cases are controls, not new RED evidence.
- [ ] **Step 5: Implement the shared scan and both error paths.** Carry quote and block-comment state across physical lines. Reset line comments and same-line escape state at newline. Find actual structural braces outside strings and comments. Extract Gamma attributes only from actual declarations outside those states. Align malformed scan results with the fixed spec detail and related original opening line. Never return an empty Gamma set for malformed attested-original capture.
- [ ] **Step 6: Run the same focused filters after integration.** Expected: no failures. Verify source and generated windows both use the shared scanner and no duplicate line-local scanner remains. This is code review, not a source-text test.
- [ ] **Step 7: Smoke the value path.** With a disposable synthetic valid shader pair, capture `PropertiesOf`, align, build the reproduction plan, and inspect the real later Gamma color's plan value. Compare against the explicit expected conversion and alpha. Clean the fixture through its existing lifecycle. Do not mutate a shipped source shader.

## Task 4: Correct installed-era occurrence matching and replay

**Spec:** LA4. **Falsifiers:** LA-F8–LA-F10 and the occurrence half of LA-F12.

**Files:** Modify the aligner, its tests, and `LockedSourceReproductionTests.cs` where exact plan values are asserted.

**Interfaces:** Keep all external-to-module interfaces unchanged. Add private `MatchInstalledProgramLine(WalkState state, string renamedPreHintLine, int originalNumber)` and a private proposal representation that holds only touched facts and declaration-format changes. Add private `ReplayInstalledProgramLine(string renamedPreHintLine, string generatedLine, WalkState state, ProgramLineProposal proposal)`, returning `bool`. The names pin the task's internal handoff, not a new test seam. Store installed-era last declaration types in candidate-owned state. Clone and adopt that map at block trials. Preserve historical `IntDeclared` behavior only for historical-era callers.

- [ ] **Step 1: Add the initializer regression and its plan assertion.** `LocalInitializerBakesWhileUniformDeclarationStays` keeps a true property declaration and bakes a read in a typed local initializer. Assert success, the identifier in the declaration, `BakedLiterals["_P"] == "0.5"`, and the reproduction plan's float write equal to `0.5f`. A changed initializer literal at another use must refuse as conflicting evidence. An edited declaration name must refuse.
  - Add `InitializerBakingIsLimitedToTheInstalledEra`. Run the same baked initializer pair directly under each era. Require `Thry2743` success and both historical-era refusals. The unchanged identifier initializer control must succeed under each historical era. Do not use shared `Align` as the oracle because installed-era success could hide a historical behavior change.
- [ ] **Step 2: Add ordering and token counterexamples.** `HintDeletionCannotCreateABakedPropertyUse` joins the branch-hint substring to the property-like spelling in an initializer. The vendor-valid output retains the exposed identifier, not a literal. Assert refusal for the alleged baked output, with an ordinary initializer acceptance control. `RepeatedTokenVisitCannotHideARevisitedDeclaration` uses a repeated property token on one line so the later vendor token pass revisits an earlier exempt declaration. Assert the exact vendor result if it fits the closed record contract, otherwise refusal. `NonVendorSeparatorCannotCreateABake` uses a non-word neighbor outside the separator set and requires unchanged text, while a normal separator control bakes. Add a recognized inline-comment skip case and retained mixed-use and two-literal cases.
- [ ] **Step 3: Add format-state regressions.** `RemovedIntegerDeclarationDoesNotSetLiteralFormat` puts an integer declaration in a removed region and later uses a fractional decimal literal. `LosingTrialDoesNotLeakDeclarationFormat` requires the winning record's decimal literal despite the losing declaration. `WinningTrialDeclarationFormatControlsLaterUse` puts `int _P` inside a kept exclusion block, then twelve distinct ordinary surviving lines with no property reads outside it, then the first `_P` use with integer literal `2`. A distinct tail kills the removed trial early. The first bake is beyond the normal lookahead window, so missing format-map adoption cannot hide behind lookahead. Assert literal `2` and planned value `2f`. `LaterSurvivingDeclarationControlsFirstBake` uses an integer declaration in one program block and a float declaration in a later block before the first bake. Assert literal `0.5` and plan value `0.5f`. Keep the direct surviving-integer control and refusal when one property needs two literal texts. Run these under the installed era.
- [ ] **Step 4: Run Alignment and Reproduction.** Observe the initializer and stripped-declaration behavior failures before the fix. Some negative-order cases may already refuse. Record them as characterization. Do not count a helper compilation failure as the intended RED result.
- [ ] **Step 5: Implement tentative inference and exact replay.** Pass the renamed line before hint deletion into the installed path. Infer only the candidate identifier and literal facts needed for that line, without committing them. Replay the exact vendor token array and mutable occurrence searches using the proposed literal values, declaration exception, inline-comment skip, format state, and last hint deletion. Require exact generated equality. Commit the proposal and format state only after replay succeeds. On failure, throw the named alignment refusal without leaking the proposal. Reuse existing literal readers and round-trip rules. Do not clone the whole accumulated `WalkState` per ordinary line. Candidate-local small deltas are sufficient.
- [ ] **Step 6: Run Alignment and Reproduction after integration.** Expected: no failures. Exercise `InitializerBakingIsLimitedToTheInstalledEra`, both historical live-initializer controls, the winning and losing format-state cases, and `SuccessfulErasWithDifferentGuardFactsRefuseSharedAlignment`. Remove only declaration helpers with no remaining historical caller. Do not delete era support or add a fallback that bypasses replay.
- [ ] **Step 7: Smoke the exact changed path.** Invoke the actual installed-era aligner and reproduction builder with the initializer pair. Observe the planned `0.5f` write. Then invoke the branch-hint counterexample and observe refusal. Inspect both facts and value, not just `Succeeded`.

## Task 4A: Restore sampler declaration facts

**Spec:** LA4 sampler-declaration amendment. This clears the Task 5 integration gate before Task 6.

**Files:** Modify `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs` and `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`. Preserve the existing reconstruction fixture and expectations. Update the dated measurement and changelog only after smoke evidence.

**Interfaces:** Keep module entry points unchanged. If a private declaration predicate is needed, use `IsTextureSamplerDeclaration(WalkState state, string text, int occurrence, string name)`. Both inference and replay use the same occurrence decision. No declaration-format fact is recorded for the texture name.

- [ ] **Step 1: Review the amendment before code.** Verify candidate scope against the installed vendor texture constant builder and rename loop. Reject a blanket texture skip or an enlarged constant declaration list.
- [ ] **Step 2: Add focused falsifiers.** `SamplerDeclarationRemainsUnusedAcrossEras` requires a plain sampler declaration to be Unused under all three direct era calls and shared alignment. Include a storage-prefix control. `SamplerReadRemainsLiveAcrossEras` adds a separate texture read and requires Live under every era. `RenamedSamplerDeclarationStaysVerified` asserts the exact renamed name and suffix, then refuses a changed generated name or alleged literal with null source. `TextureDerivedInitializerStillRecordsBothLiterals` requires installed-era initializer bakes for `_ST` and `_TexelSize`, with exact `float4(2,0.5,0.25,0.125)` and `float4(0.25,0.5,4,2)` facts.
  - The same-line variant places a texture read after the sampler declaration's semicolon and requires the direct installed-era result to be Live. It pins occurrence-specific recognition. It does not run shared `Align` or assert changed historical facts for that form.
- [ ] **Step 3: Observe the focused RED before implementation.** The earlier unchanged-mask failure is already observed and must not be rerun only to confirm it. Run the new focused cases. Distinguish expected failures from passed-first controls.
- [ ] **Step 4: Correct only the installed sampler declarator handling.** Verify spelling and rename, record no use or format entry, and retain exact replay. A later read and derived texture constants stay on their existing paths. Keep the constant type list and all-era agreement unchanged.
- [ ] **Step 5: Run Alignment, Reproduction, Reconstruction, and Swap-in.** Require matched tests and zero failures. The unchanged mask case must reach its missing-define refusal after passing its guard. Preserve repeated-token, lone-declaration, mixed-use, two-literal, hint-order, and historical controls.
- [ ] **Step 6: Smoke and review the corrected path.** Invoke the actual aligner on sampler-only and sampled-texture pairs and inspect their exact facts across eras. Re-run the native clone restoration and checked-contradiction smoke. Verify owned fixture removal and exact cache equality. Obtain a scoped adversarial implementation review before closing Task 5 or starting Task 6.

Status: 2026-10-29. Adversarial review accepted this amendment after requiring the explicit same-line read control above. It does not reopen unrelated completed work or authorize a fourth vendor edit rule.

## Task 5: Verify the residual check boundary and restored values

**Spec:** LA1 and LA7. **Falsifier:** LA-F11. No new production check is authorized.

**Files:** `Tests/Editor/Semantics/Poiyomi/LockedSourceReproductionTests.cs`, `Tests/Editor/Build/LockedMaterialReconstructionTests.cs`, and existing `TransientUnlockSwapInTests.cs` only where a missing behavior case needs coverage.

**Interfaces:** Consume `LockedSourceReproduction.Build(AlignedLockedSource source, IReadOnlyList<LockedSourceProperty> properties, ILockedMaterialValues locked)` and `CheckConsistency(..., ILockedMaterialValues reproduced)`. Consume the existing `LockedMaterialReconstruction.TryApply(Material clone, Func<Shader, bool> originalShaderAttested)` interface and the existing production swap-in test harness. Do not change production to use a material predicate. Do not add a test-only production delegate or a new restoration guarantee.

- [ ] **Step 1: Inspect existing behavior coverage before adding tests.** Reuse existing keyword-loss, removed-condition, texture-define, and zero-and-zero strictness cases. Add a new case only if it protects a missing invariant rather than duplicating the same path.
- [ ] **Step 2: Add the explicit boundary characterization.** Build a valid aligned scalar record and successful plan. Apply the plan to the test view, then alter a baked scalar that no recorded removed condition or define reads. Assert the changed scalar value and that `CheckConsistency` still succeeds. Separately change a removed-condition operand to make its condition false and assert refusal. Preserve the existing production seventh-fact keyword-loss test. These are characterizations if they pass first, not a request to strengthen production consistency checks.
- [ ] **Step 3: Verify actual reconstruction.** Use a hand-built synthetic generated partner for the initializer result, not the fixture helper's broad declaration bake rule as its own oracle. Reuse the reconstruction fixture lifecycle and attestation seam. Run production reconstruction on a clone and assert the real restored scalar and correct original shader. Keep the source material and shader unchanged. If this requires a helper extension, limit it to writing the explicit independent generated text. Do not teach a second vendor emulator to the test helper.
- [ ] **Step 4: Run Reproduction, Reconstruction, and Swap-in.** Expected: no failures. Record characterization honestly. No production change is expected in this task.
- [ ] **Step 5: Smoke restoration and a checked contradiction.** On a disposable clone, observe the restored scalar. Then change a required keyword or recorded removed-condition operand and call the existing consistency or swap-in path that owns that check. Observe the named refusal. Clean all created objects and fixture assets.

## Task 6: Extend the fixed real-lock matrix and record the bounded result

**Spec:** LA5, LA8–LA10. No admission or fourth widening.

**Files:** Modify `BundledLockedOriginalRealLockTests.cs`, append observed results to the new dated measurement record, and update `CHANGELOG.md` after smoke evidence. Do not modify the older measurement to erase its failures.

**Interfaces:** Add `bool AssignedTexture` to the suite's private `LockCase`. Add `RealToonAssignedTextureAlignsAndRestores` and `RealTwoPassAssignedTextureAlignsAndRestores` using the existing runner. Task 1's lifecycle owns the generated texture asset too. Preserve all existing success assertions and the test-scoped attestation seam.

- [ ] **Step 1: Add the two texture cases with the spec's exact values.** Create one linear 4-by-2 RGBA texture per case. Alternate zero and one red and alpha values. Assign it to `_MainTex` and `_AlphaMask`, both with scale `(2, 0.5)` and offset `(0.25, 0.125)`. Set `_MainPixelMode = 1`, `_MainAlphaMaskMode = 2`, and `_AlphaForceOpaque = 0`. Persist only the created texture under the owned fixture folder. Do not change import settings of any existing texture.
- [ ] **Step 2: Assert the fixture reaches the intended vendor outputs before alignment.** In memory, observe `PROP_ALPHAMASK`, both non-declaration transformed-coordinate expressions with the expected `_MainTex` and `_AlphaMask` scale-and-offset substitutions, and the main texture texel-size expression corresponding to `(0.25, 0.5, 4, 2)`. Check the relevant expressions, not an unrelated occurrence of the same literal. If a positive guard fails, fix the fixture without changing the production rule. On alignment success, assert the main texel-size baked fact, both transform facts, and successful reproduction `Build` validation with the created texture dimensions. Assert both planned transforms and texture identities. Keep the align-success assertion for any remaining frontier refusal.
- [ ] **Step 3: Run the twelve named real-lock cases once with the gate explicitly enabled for the operation.** Use the exact method list below under `Alrauna.Amuse.Tests.Editor.Build.BundledLockedOriginalRealLockTests`. Record observed leaf results, phase times, source refusal locations, and cleanup checks. A failed acceptance test stays failed. Do not skip it or alter its expectation merely to claim rule completion.
  - `RealToonAllDefaultsAlignsAndRestores`
  - `RealToonOneRenamedPropertyAlignsAndRestores`
  - `RealToonTwoRenamedPropertiesAlignsAndRestores`
  - `RealToonSymbolMaterialNameAlignsAndRestores`
  - `RealToonLightVolumesOnAlignsAndRestores`
  - `RealToonLightVolumesOffAlignsAndRestores`
  - `RealToonAudioLinkEnabledAlignsAndRestores`
  - `RealTwoPassAllDefaultsAlignsAndRestores`
  - `RealTwoPassOneRenamedPropertyAlignsAndRestores`
  - `RealTwoPassAudioLinkEnabledAlignsAndRestores`
  - `RealToonAssignedTextureAlignsAndRestores`
  - `RealTwoPassAssignedTextureAlignsAndRestores`
- [ ] **Step 4: Follow each successful case through the real paths already in the runner.** Observe production swap-in, clone and restore, the seventh fact through swap-in, and explicit consistency. Clear the memo and measure a fresh alignment call. Label it shared cold alignment cost, not full dry-run time. For mirror proof, a throwaway reflection driver may invoke the actual private `AlphaSeparationDryRun.LockedMirrorRefuses(Material, Action<NoteKind>)` and inspect its reproduction and plan-view outcome. Add no production seam. Separately run normal `AlphaSeparationDryRun.Begin()` and `Step()` to completion on the disposable avatar. With flags still false, require the unchanged named bundled-original refusal and no deferred conversion. The normal gate runs before the mirror. Do not claim a positive deferred row from mirror success or change flags to get one. Capture no source asset edits.
- [ ] **Step 5: Classify any remaining refusal.** If it is the traced stochastic frontier, record it as the remaining coverage limit. If a different unexplained inner failure appears, trace it read-only and do not claim rule completion until its cause is understood. Stop before implementing any fourth rule. A new acceptance rule or a resolver invariant fix needs a reviewed spec and plan amendment. Keep both bundled flags false even if all twelve happen to pass.
- [ ] **Step 6: Restore the gate state and prove final cleanup.** Use Task 1's bounded drain and sticky blocked-ownership rules. Only after callbacks finish, assert cache file bytes, entry sets, root existence, root metadata bytes and existence, owned build-output absence, and fixture and fixture-metadata absence against the initial snapshot. Confirm no trace callback remains. Restore the exact prior gate value, including absence, in the outer operation finally path even on a test failure or blocked drain. An unresolved drain blocks completion and another lock. Record only observed sanitized results.
- [ ] **Step 7: Run full product and research assemblies with the gate unset.** Expected: matched tests, zero failed, and only named intentional skips. A real-lock failure from Step 3 is reported separately and is not hidden by this ungated result. Read the dev console for errors. Inspect tracked and untracked changes, then run `git diff --check`. Do not stage anything.
- [ ] **Step 8: Update the product changelog and the dated record.** State the implemented rule changes and measured limits. Do not claim bundled admission. Map each spec decision and falsifier to its observed proof or explicit unresolved milestone. Sweep every changed document and comment for forbidden identifiers, paths, private names, and unqualified status words. Remove temporary drivers. Deliver the implementation diff and evidence for owner review without committing.

## Task 6A: Crossing control span ownership

**Spec:** LA11. **Falsifiers:** LA-F13–LA-F20. Task 6 stays blocked until this task closes. This task has no admission change, no watchdog change, and no matrix repeat.

**Files:** Modify `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs` and `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`. Update the changelog and the dated measurement record only after smoke evidence.

**Interfaces:** Keep module entry points unchanged. Change only the delimiter steps of `WalkKeptFrame` for `PoiyomiLockEra.Thry2743`. Use the existing walk state field for the last consumed original line number. Add no new state field. Both historical eras keep their saved delimiter steps. The removed-frame trial in `ResolveBlock` stays unchanged.

- [ ] **Step 1: Build the public witnesses by hand.** Write a reduced original with this shape. A frame opener and an else line sit inside a first wrapper. The frame close sits inside a second wrapper. Both wrappers carry one condition. Apply the vendor stream rules from `ShaderOptimizer.cs:2053-2242` by hand to get each generated text. Do not run a lock. Record which vendor rule produces each generated line. Build the both-kept, both-removed, first-kept second-removed, and else-line-in-wrapper variants. Add a retained keyword frame and a removable keyword frame with no wrapper, and a non-crossing frame with a missing or changed close.
- [ ] **Step 2: Add the falsifiers.** `CrossingWrapperClosesFrameOnce` covers LA-F13 and LA-F14 on the retained path, including the duplicated, changed, and missing close mutations. `SpeculativeCrossingFrameClosesOnce` covers the same crossing when an early pragma makes the frame speculative, as in both bundled originals. `CrossingWrapperElseLineIsConsumedOnce` covers LA-F15. `BothCrossingWrappersRemovedKeepsMiddleLines` covers LA-F16 and requires the both-kept case to keep Kept records. `RemovedSecondWrapperRefusesUnbalancedFrame` covers LA-F17 and asserts null source. `LosingCrossingTrialCommitsNoFacts` is characterization for LA-F18, because a refusal always returns a null source. `HistoricalErasKeepCrossingResults` covers LA-F19 by direct era calls. `NonCrossingFramesKeepPriorResults` covers LA-F20. Add a positive guard to each that proves the crossing path ran, for example the block record count and outcomes.
- [ ] **Step 3: Observe RED.** Run the new focused cases before any production edit. Expect the both-kept and else-line cases to fail with the block alignment refusal. Record passed-first cases as characterization. Do not claim RED for cases that pass first.
- [ ] **Step 4: Change the delimiter steps.** In the installed era, when a wrapper that opened after the frame start is still open at a delimiter and the cursor is beyond it, skip the else walk when the else line counts as consumed or jumped. Walk the rest of the else range from the cursor to the close index. Never start that walk at the else index plus one when the cursor is beyond it. Keep the growth boundary reset to the close index on that walk, also when the else line is skipped, so nested lookahead stays bounded. Skip the close walk when it counts as consumed. Throw the existing block alignment refusal when the cursor is beyond the close and the close does not count as consumed. A delimiter counts as consumed when the last consumed original line number is at or beyond its line number. A delimiter that no wrapper crosses keeps its saved step, for the speculative walk and the retained walk alike. Never move the cursor backward. Add a short comment that cites LA11 and the spec path. Do not change the removed-frame trial, the lookahead, the tie rules, or the Y2 probe.
- [ ] **Step 5: Run the focused suites.** Run Alignment, Reproduction, Reconstruction, and Swap-in. Require matched tests and zero failures. Existing characterization for retained frames, nested guards, ties, and Y2 must stay green without edits to valid tests.
- [ ] **Step 6: Smoke and review.** Call the actual aligner on the witnesses in memory. Inspect block records and last consumed numbers for each variant. Run one scoped adversarial implementation review. Only after both, run only the default Toon real-lock case behind the existing gate under the Task 1 ownership and cleanup rules. If it still refuses, use a temporary in-memory capture and stop for a new amendment. Verify owned fixture removal, exact cache equality, gate removal, and no pending vendor work.

Status: 2026-10-30. Steps 1 to 5 ran. RED was observed for the retained and the speculative witnesses before the fix. A first fix covered all frame walks and broke four existing LA2 and LA-F4 tests, so the rule now depends on a static wrapper crossing test. The four suites then matched 217 cases and passed all 217. Step 6 and the real-lock result are recorded in the dated measurement record.

## Spec coverage and execution gates

| Spec item | Owning task |
|---|---|
| LA1 residual-check boundary and actual values | Task 5 |
| LA2 installed-era prefix proof and frame ownership | Task 2 |
| LA3 lexical state and Gamma propagation | Task 3 |
| LA4 exact occurrence replay and format state | Task 4 |
| LA5 trace and no fourth widening | Task 1, then Task 6 if a new frontier appears. LA11 and Task 6A cover the first traced frontier |
| LA6 existing module interfaces | File map and Tasks 2–5 |
| LA7 falsifiers | Tasks 2–5, with LA-F1–LA-F12 named above |
| LA8 matrix and distinct completion milestones | Task 6 |
| LA9 ownership, callback drainage, and byte equality | Task 1 and every Task 6 case |
| LA10 comments, changelog, record, and review | Every production task and Task 6 |
| LA11 crossing control span ownership and LA-F13–LA-F20 | Task 6A |

Tasks 2–4 share the aligner and must execute in order under one integration owner. Task 5 follows their combined behavior. Task 6 follows all rule and safety work. Do not parallelize edits to this shared state. Independent read-only adversarial reviews may run in parallel when authorized.

Implementation must not start from this plan-writing approval. After owner approval, use native execution unless the owner chooses subagent-driven execution. The plan does not pre-authorize agents, commits, network access, or package installation for that later session.

## Plan review record

Status: 2026-10-28. Two independent adversarial reviews required corrections to format-state adoption tests, cross-era disagreement and historical controls, the reconstruction delegate type, ownership of temporary build output, bounded teardown drainage, dry-run proof labels, and texture guards. This revision implements those document corrections and adds explicit LA proof comments to production steps. The final scoped proof and safety re-reviews accepted the saved corrections with no remaining blocking findings. No RED, GREEN, test count, runtime result, or cleanup result in these steps is an observed outcome of writing this plan.
