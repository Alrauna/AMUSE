# Vendor lock handover implementation plan

Date: 2026-09-24. The owner approved the direction in chat on 2026-09-24 after one grilling round with five settled decisions.

## Goal

Remove the apply-on-play relock machinery from the product. Locking of Poiyomi materials, including AMUSE-generated ones, becomes the lock tool's job in the SDK preprocess stage that runs after NDMF, on upload and on play entry alike. AMUSE keeps no vendor lock call of its own on any build path.

The in-memory reconstruction, the unlock window, and the reference reversion close from the hermeticity work stay unchanged. The build path classification stays for gating and reporting, including the play mode versus upload label on the summary report.

## Owner decisions, 2026-09-24

1. Full cutover. The vendor readiness seam dies with the relock, not just the relock call.
2. The trust basis is accepted with its named residual.
3. The build path classification and the report label stay.
4. No Lab tooling changes. Acceptance is one play mode run whose NDMF console shows exactly two AMUSE rows and no back-off row.
5. Same process as the hermeticity plan: dated plan, RED and GREEN tests, fresh implementer per task, controller runs Unity, no commits and no staging.


Status on 2026-09-24, complete. Task C1 ran five fix rounds. The contract tests were written first and observed RED against the pre-cutover production files: six failures, covering all three contract tests. The final focused run passed 108 of 108 with zero failures in 25.9 seconds. The full product and research assemblies completed 2,372 cases with exactly the five documented pre-existing failures and no new failure. The review found the work spec compliant and of good quality, with two findings resolved by rulings and deferrals in the session ledger. The Lab acceptance run on the same day observed exactly two AMUSE rows in the NDMF console with no relock row and no back-off row, and zero source drift afterward. The one new vendor-generated folder came from the lock tool's own post-NDMF stage, which is the designed behavior.

## Verified trust basis, 2026-09-24

The lock tool's upload hook is an SDK preprocess callback at order 100. NDMF, where AMUSE runs, executes in an earlier callback. The hook locks every shared material of every renderer on the avatar, including AMUSE-generated ones.

Play entry runs the same preprocess chain under apply on play. A live Lab play mode run on 2026-09-23 showed the hook touching materials during the build.

The hook skips its work when it runs mid play, because play mode is already active there. Preprocess-once patches in the ecosystem bind avatar processing to play entry, so AMUSE outputs appear only where the hook does run. Residual: in a mid-play reprocessing, generated outputs stay unlocked. The visual result is identical and the context is development only.

The same Lab play mode run recorded one TransientUnlockRelockFailed report row. The play path relock failed on at least one slot and the close backed the transformation off for it, leaving a mesh split behind. The removal is a correctness fix, not only noise reduction.

## Allowed mutations

Production, all under `Packages/com.alrauna.amuse/Editor/Build/`:

- `TransientUnlockWindowClose.cs`: delete the play path relock batch, its verification, and the TransientUnlockRelockFailed emission with its fallback reporting block.
- `AmusePlatformFinishPlugin.cs`: delete the path aware vendor answer selection.
- `TransientUnlockAvailability.cs`: delete `RelockVendorReady` and every vendor readiness consult. The original shader attestation gates stay.
- `TransientUnlockSwapIn.cs`: delete the play path relock requirement and the vendor readiness consult in availability resolution.
- `AlphaSeparationRecords.cs`: delete the `TransientUnlockRelockFailed` refusal member.
- `AmuseReportStrings.cs`: delete the three TransientUnlockRelockFailed string entries.

Tests, all under `Packages/com.alrauna.amuse/Tests/Editor/Build/`:

- `AmusePlatformFinishPluginTests.cs`: the play path refusal case becomes a play path admission case.
- `TransientUnlockTransformationTests.cs`: relock failure tests and play path canonical lock assertions are deleted or rewritten to assert that no lock call happens. Reversion and fallback assertions stay.
- `TransientUnlockWindowCloseTests.cs`: the non play null delegate case generalizes to all paths. Assertions that die with the refusal member are deleted.
- `TransientUnlockSwapInTests.cs`: availability cases drop the vendor readiness dimension.
- `TransientUnlockVendorStandIn.cs` and `TransientUnlockTestLifecycle.cs`: relock scripting surface shrinks to what the reversion close still needs.

Records: the hermeticity design gets a dated supersession note, the generated output locking design gets its second, and this plan records results.

No research package file changes. No Lab file changes. No shader frontend, classifier, or census changes.

## Task C1: contract flip and cutover

Write the failing contract tests first:

1. A play path build whose lifecycle facts carry no vendor seam admits a locked material, opens the window, and completes the transformation. It refuses today.
2. A play path close invokes no relock delegate, records no TransientUnlockRelockFailed refusal, and reverts every pair to the locked original.
3. Availability resolution on the play path no longer requires a vendor seam.

Then remove the production code listed under Allowed mutations, then bring every listed test file to the new contract, deleting tests that exist only to pin the removed behavior.

## Task C2: validation, Lab observation, records

The controller runs the focused filters and the full assemblies in the dev editor instance, observes one Lab play mode run in the Lab editor instance, checks the git boundary, sweeps the records, and writes the results into this plan and the two design records.

## Stop conditions

Stop if the reversion close cannot prove its inversion on the play path after the relock batch is gone, if the research assembly fails to compile, or if the Lab run shows any vendor generated write inside an AMUSE pass. Stop before touching shader frontends, the classifier, the census schema, or the Lab scripts.

## Expected report

The changed files, the observed RED and GREEN counts, the full assembly counts with every failure attributed, the Lab NDMF console observation with its date, the identifier sweep result, and the remaining limits. State that no upload ran and nothing was staged or committed.

## Self-review

The unlock window and the decoder are untouched, so the hermeticity contract holds on both paths. The refusal enum loses one member that no census consumer references, checked by search on 2026-09-24. The removal deletes a back-off path, so the transformation can only become more complete, never less.
