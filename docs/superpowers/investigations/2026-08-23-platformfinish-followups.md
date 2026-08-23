# PlatformFinish follow-ups (I1 / I4 / I5)

Date: 2026-08-23
Source: final review of `feat/platformfinish-lifecycle-foundation` (PR #19, merged as `c6d65a3`).

The PlatformFinish lifecycle branch parked three findings that were real but out of that
branch's scope. They existed only in the ignored Superpowers SDD workspace
(`.superpowers/sdd/2026-08-22-platformfinish-lifecycle-foundation/progress.md`), not in Git
history. This file carries them forward. It is a record of open questions, not a design.

## I1 — release packaging includes test-only NDMF plugins

Evidence in the repository at `c6d65a3`:

- The editor test assembly `Alrauna.Amuse.Tests.Editor` references `nadena.dev.ndmf` directly
  (`Packages/com.alrauna.amuse/Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef`).
- It exports three synthetic NDMF plugins through assembly attributes:
  - `AmusePlatformFinishPluginTests.ZzzAnonymousOptimizingProducerPlugin`
  - `AmusePlatformFinishPluginTests.AfterAmusePlatformFinishObserverPlugin`
  - `AmuseBuildOperationTests.AfterAmuseOperationPlugin`
- `.github/workflows/release.yml` packaged the package root recursively
  (`zip -r … .` at line 59) and built the `.unitypackage` file list from an unfiltered
  `find "$packagePath/" -name \*.meta` (line 63).
- Measured on a clean `git archive` of `c6d65a3`: 108 of 162 zip entries and 54 of 81
  metaList entries were under `Tests/`.

Consumers installing a release would therefore receive the editor test assembly and three
inert extra NDMF passes. This does not change avatar semantics — the synthetic passes are
gated/no-op when unarmed — but test scaffolding must not ship in production packages.

Disposition: fix before first release.

Resolved on branch `fix/release-test-packaging` by excluding `Tests/` and `Tests.meta` from
both release artifacts at the packaging boundary, rather than by weakening test coverage or
adding runtime guards to make shipped test code harmless.

## I4 — dry analysis failure boundary

The PlatformFinish dry-analysis loop runs before AMUSE has mutated the avatar. It has no
per-renderer failure boundary, so an unexpected exception from renderer extraction or
analysis aborts the user's build even though nothing has been mutated.

The merged plan's propagate-uncaught rule was written about preparation and apply, not about
dry analysis, so this is genuinely undecided rather than settled.

`catch Exception -> silently skip renderer` is explicitly **not** the prescribed answer. The
future alpha-mutation work must distinguish at least conceptually between:

- an expected unsupported or conservatively unprovable renderer → skip/refuse safely;
- an unexpected AMUSE implementation defect → surface as a genuine failure.

The appropriate per-renderer failure boundary should be decided when implementing the
concrete alpha-mutation vertical slice, because that work establishes the meaningful
mutation and failure scopes.

Disposition: decide during concrete alpha-mutation implementation.

No production behavior was changed for I4 on the `fix/release-test-packaging` branch.

## I5 — obsolete `UnityAlphaFieldEvidence` instance surface

After the immutable evidence migration (task 5 of the PlatformFinish branch), the
instance-oriented `UnityAlphaFieldEvidence` API — constructor, backing dictionary, and
`TryGetAlphaField` lookup — appears to have lost its production consumer. The static capture
path remains in use.

Potential future cleanup, to be chosen deliberately:

- remove the now test-only constructor/dictionary/lookup surface and retarget the tests that
  still exercise it; or
- retain it deliberately, with an explicit recorded reason.

Disposition: cleanup/debt; does not block alpha-mutation work.

The API was not removed on the `fix/release-test-packaging` branch.

## Deferred minor review findings

The PlatformFinish final whole-branch review triaged a ledger of ten Minor findings raised
during tasks 5 and 6. Finding 2 was ruled *must fix before PR* and landed in the final fix
wave (`f47bb5f`, `Execute` now rejects a reason-less refusal). The other **nine** were ruled
*defer is correct* and are recorded here so the ignored SDD workspace is no longer their only
home. Ledger source:
`.superpowers/sdd/2026-08-22-platformfinish-lifecycle-foundation/progress.md`.

They are preserved at the weight the reviewer gave them: none is an architectural
requirement, none is resolved or dismissed here, and none was implemented on this branch.

### 1. Snapshot Unity-object field guard is shallow (Task 5)

Observed: `AssertHasNoUnityObjectFields` in
`Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs` checks a
type's own fields plus their direct generic arguments. It does not recursively walk the whole
captured graph, so a live `UnityEngine.Object` reaching the snapshot through a nested captured
type would not be caught.

Minor because the immutability property it defends is currently held by construction across a
small, hand-audited set of capture types; the guard is a safety net, not the mechanism.

Disposition: reconsider during the alpha-mutation work — that branch grows the captured graph,
which is exactly when a shallow net stops covering it.

### 2. Refusal test dereferences `operation.Result` without the `PrepareInvoked` guard (Task 6)

Observed: in `AmuseBuildOperationTests.cs`, the refusal test reads `operation.Result` without
the `PrepareInvoked` precondition assertion the sibling integration tests use, so a gating
regression surfaces as a bare `NullReferenceException` rather than a named failure.

Minor because it degrades failure legibility only; the test still fails when the behavior
regresses.

Disposition: ordinary test hygiene.

### 3. Plan-mandated assertions inside a `finally` can mask the original diagnosis (Task 6)

Observed: assertions placed in a `finally` block can throw over an in-flight failure from the
`try` body, replacing the original diagnosis with a secondary one.

Minor because it affects diagnosis quality on an already-failing run, not pass/fail
correctness; the structure was mandated by the plan.

Disposition: ordinary test hygiene.

### 4. Lifecycle-capability saver facts are never cross-checked against the handed `IAssetSaver` (Task 6)

Observed: the injected capability asserts `hasAssetSaver: true` / `hasAssetContainer: true`
while three integration tests actually run under NDMF's `NullAssetSaver`, and
`AmuseBuildOperation.Execute` never cross-checks the capability's saver facts against the
`IAssetSaver` it is handed. The facts and the reality can disagree undetected.

Minor because nothing on this branch persists anything, so the disagreement has no observable
consequence yet. The final review explicitly noted this one should be settled in the branch
that first wires a production caller.

Disposition: reconsider during the alpha-mutation work — that is the branch that first makes
saver facts load-bearing.

### 5. `RecordingAssetSaver`'s recording is never observed (Task 6)

Observed: the test double in `AmuseBuildOperationTests.cs` records saver activity that no test
asserts against.

Minor because it is unused test scaffolding, not a coverage gap in any claimed behavior.

Disposition: ordinary cleanup — either assert on the recording or drop it. Note that the
recording becomes genuinely useful if finding 4 is taken up.

### 6. Near-duplicated `TestVrchatPlatform` and supported-facts builder (Task 6)

Observed: `TestVrchatPlatform` and the supported-facts builder exist in near-identical form in
both `AmuseBuildOperationTests.cs` and `AmusePlatformFinishPluginTests.cs`. The duplication
was mandated by the plan.

Minor because both copies are small and correct; the cost is drift risk, not defect.

Disposition: ordinary cleanup/test hygiene.

### 7. A second unconditional `.AfterPlugin("com.alrauna.amuse")` sibling is order-unconstrained (Task 6)

Observed: two test plugins declare `.AfterPlugin("com.alrauna.amuse")` unconditionally
(`AmuseBuildOperationTests.cs:504`, `AmusePlatformFinishPluginTests.cs:206`). Their order
relative to each other is unconstrained, and no test currently arms both scopes at once.

Minor because the latent nondeterminism is unreachable while no test arms both.

Disposition: known implementation debt — becomes real only if a future test arms both scopes.

### 8. Baseline Console noise in the full suite (Task 6)

Observed: a complete public EditMode run retains three NDMF Harmony
`Exception: mprotect returned EACCES` entries. The console is an environment baseline, not
pristine, so "no unexpected Console errors" must be read against that baseline.

Minor because it originates in NDMF/Harmony on this host, not in AMUSE, and fails nothing.

Disposition: known environment debt — record the baseline rather than chase it.

### 9. Refusal-test non-vacuousness depends on NDMF's exact log prefix (Task 6, post-fix)

Observed: after the fix wave, the refusal test proves "no error was reported" by capturing
`Application.logMessageReceived` and relying on NDMF logging every `ReportError` either as an
exception or with the exact prefix `"[NDMF] Error Reported: "`
(`AmuseBuildOperationTests.cs:28`). A future NDMF logging change would silently weaken it back
to `Successful`-level coverage without failing.

Minor because it is the strongest mechanism available under this branch's no-reflection and
no-asmdef-change constraints, and the reviewer confirmed no better route existed.

Disposition: known implementation debt — revisit only if the asmdef/reference constraints are
ever relaxed, which would allow asserting `ErrorReport.Errors` directly.
