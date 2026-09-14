# Animation-commit property blocks and the swap-splitting gap

Privacy note: The motivating product observation comes from private avatars. Private objects appear only by role, and this record names no avatar, renderer, material, animation asset, machine, port, or instance identifier.

Date: 2026-09-14.
Status on 2026-09-14: Root cause proven. A fixture-side workaround is merged to `main` through PR #93 (merge commit `5bf3c48`). The approved design for the production fix is committed as `docs/superpowers/specs/2026-09-14-material-property-block-effective-state-design.md` on `feat/material-property-block-support`. The implementation plan is committed as `docs/superpowers/plans/2026-09-14-property-block-effective-state-plan.md`. No production code has changed yet.

Branch inspected: `fix/AAO-transparent-cutout-integration` at `81ae5b1`. Baseline for comparison: its parent `3703fdd`, checked out in a disposable worktree.

## Result

The thirteen animation-seam test failures are pre-existing at the baseline commit. They are not a regression of the cutoff-field fix on the branch.

The writer of the property block is Unity's animation runtime. During the NDMF animator commit, the committed controller is assigned back to the live Animator, the engine evaluates the `material._X` curves, and the sampled values land in renderer-wide property-block state. AMUSE's presence-only refusal then marks the renderer unanalyzable. No NDMF or AMUSE code writes a block.

The production consequence: every real build of an avatar whose renderers animate material properties reaches the PlatformFinish pass with a transient block and refuses wholesale. This mechanism is proven in test fixtures. The same refusal on a private build is inferred from the mechanism, not yet observed directly. The Census Lab characterization in the implementation plan closes that gap.

## Symptom and observed counts

Thirteen failures, ten in `AlphaSeparationPreparationTests` and three `RuntimeStateProductionEntry` cases in `AmusePlatformFinishPluginTests`. All thirteen animate a material property on a live Animator. Two signatures: a fixture precondition counts a renderer refusal that must be zero, and refusal accounting flips between an expected per-slot reason and the renderer-wide block refusal.

Observed runs on 2026-09-14:

- Fix tip `81ae5b1`, both fixture classes: 13 failures recorded within the first 105 of 2204 completed tests. The test job then lost tracking.
- Baseline `3703fdd`, same filter: the same 13 names, messages, and expected values. 105 of 2201 completed before the job lost tracking.
- Baseline `3703fdd`, plugin class only: the three accounting failures among 64 completed. The job reported the unfiltered total, 2201, so the filter also failed to apply to the reported count.

All 13 failures appeared before every interruption, so the comparison below is complete for the cluster.

## Comparison method and verdict

The baseline ran in a disposable worktree at `3703fdd`. The worktree restored every whitelisted package from git. Its editor Library was produced by an APFS copy-on-write clone of the main checkout Library, about 10 GB, in about 11 seconds of wall time, with no meaningful new disk usage. Unity accepted the cloned Library and recompiled scripts normally.

Verdict: the failure sets at `81ae5b1` and `3703fdd` are identical in names, messages, and expected values. The cutoff-field fix is exonerated. The fix commit message itself already listed the cluster as known unrelated failures with the same cause hypothesis.

## Writer trace

Static search found zero `MaterialPropertyBlock` references in NDMF 1.14.4 editor code and none in AMUSE production code. The writer had to be the engine.

Tagged probe logs, added and later removed, recorded three points on both fixture shapes:

- Before the build: `HasPropertyBlock()` false. Fixture setup does not write the block.
- Immediately after `AvatarProcessor.ProcessAvatar` returns: true. The build wrote it.
- At analysis: the production refusal fired by name.

The plugin fixture probe also recorded that the Animator still held the original controller instance after the build, so the commit reused the instance rather than installing a clone. The write therefore happens during the build window, on the commit's rebind.

Call path: test, then `AvatarProcessor.ProcessAvatar`, then the NDMF animator commit on extension deactivation, then `GenericPlatformAnimatorBindings.CommitControllers`, which assigns the committed controller to the live Animator, then the engine evaluates the `material._X` curves and stores the values as renderer-wide block state. Presence is timing dependent because the engine evaluation races the build's synchronous phases.

## Engine characterization relied on

Existing repository characterization on Unity 2022.3.22f1 established the mechanism before this investigation: an `AnimationMode` sample of a `material._Cutoff` curve appears in a renderer-wide block, per-material-index blocks stay empty, and `StopAnimationMode` removes the block. This investigation added one probe: `SetPropertyBlock(null)` removes the block, while an empty block still counts as present. The fixture helpers use the null form.

## Environment observations

EditMode test runs pause when the editor loses focus. Two runs stalled until the editor was fronted again.

The test-job tracking of the automation tooling died mid-run at the same test in three pre-fix runs across two editors, with the job reporting an early finished state and a frozen count. The defect was reported to the tooling channel on 2026-09-14. Post-fix runs, including a full assembly run of 2057 tests, completed normally, so the losses were tied to the failing pre-fix runs.

An editor launched outside the Hub logged MonoMod access errors from NDMF preview Harmony patches at startup. EditMode runs were unaffected.

## Decision chain

The original diagnosis task allowed three fix layers and named a change to the structural refusal contract a stop condition. Reading block contents was ruled out for the branch because it is effective-state analysis. The refusal stays and the tests pin it, which would gut the tests. The chosen branch measure was fixture-side: clear the commit-written block after the build and before the pass under test.

That measure shipped as a deliberate, temporary weakening. Commit `1e17d91` added `ClearCommitAnimationPropertyBlocks` helpers with the justification in the commit message, and PR #93 merged it on 2026-09-14. The design document records the re-strengthening debt.

The product gap remained. Renderers that swap materials through animation own admitted states, capture, and curve rewriting in AMUSE, but the transient block refused them before that machinery ran. The reported symptom, swap renderers that never split, matches that gate.

The user selected full effective-state analysis over a narrower closure-implied gate, and selected a staged delivery: materialize effective materials first, then move scalar entries into per-property value domains inside the analysis, gated on measured costs. The design was approved on 2026-09-14 and written to the spec referenced above.

## What remains

Stage 1 implementation per the plan: materialization in Host, capture and revalidation wiring, removal of `MaterialPropertyOverridesPresent` with its pins rewritten and justified, fixture re-strengthening, and full assembly validation. After stage 1, one read-only Census Lab characterization on a private swap avatar confirms the production hypothesis. The predicted outcome: the transient block is present, the renderer analyzes, and the triangles opaque under every admitted swap state split onto the canonical opaque slot with the swap curve rewritten.
