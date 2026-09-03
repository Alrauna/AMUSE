# PlatformFinish follow-ups (I1 / I4 / I5)

Date: 2026-08-23
Source: final review of `feat/platformfinish-lifecycle-foundation` (PR #19, merged as `c6d65a3`).

The PlatformFinish lifecycle branch set aside three real findings that were outside that branch's scope. They existed only in the ignored Superpowers SDD workspace
(`.superpowers/sdd/2026-08-22-platformfinish-lifecycle-foundation/progress.md`), not in Git
history. This file carries them forward. It records open questions, not a design.

## I1 — release packaging includes test-only NDMF plugins

Evidence in the repository at `c6d65a3`:

- The editor test assembly `Alrauna.Amuse.Tests.Editor` references `nadena.dev.ndmf` directly
  (`Packages/com.alrauna.amuse/Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef`).
- It exports three synthetic NDMF plugins through assembly attributes:
  - `AmusePlatformFinishPluginTests.ZzzAnonymousOptimizingProducerPlugin`
  - `AmusePlatformFinishPluginTests.AfterAmusePlatformFinishObserverPlugin`
  - `AmuseBuildOperationTests.AfterAmuseOperationPlugin`
- `.github/workflows/release.yml` packaged the package root recursively
  (`zip -r … .` at line 59). It built the `.unitypackage` file list from an unfiltered
  `find "$packagePath/" -name \*.meta` (line 63).
- A clean `git archive` of `c6d65a3` showed that 108 of 162 zip entries and 54 of 81
  metaList entries were under `Tests/`.

Consumers who install a release would therefore receive the editor test assembly and three
inert extra NDMF passes. This does not change avatar semantics because the synthetic passes are
gated/no-op when unarmed. However, test scaffolding must not ship in production packages.

Disposition: fix before first release.

The `fix/release-test-packaging` branch resolved this finding. It excluded `Tests/` and `Tests.meta` from
both release artifacts at the packaging boundary. It did not weaken test coverage or
add runtime guards to make shipped test code harmless.

## I4 — dry analysis failure boundary

The PlatformFinish dry-analysis loop runs before AMUSE has mutated the avatar. It has no
per-renderer failure boundary. Thus, an unexpected exception from renderer extraction or
analysis aborts the user's build even though nothing has been mutated.

The merged plan's propagate-uncaught rule applied to preparation and apply, not to
dry analysis. Thus, this issue is undecided rather than settled.

`catch Exception -> silently skip renderer` is explicitly **not** the prescribed answer. The
future alpha-mutation work must distinguish at least conceptually between:

- an expected unsupported or conservatively unprovable renderer → skip/refuse safely;
- an unexpected AMUSE implementation defect → surface as a genuine failure.

Decide the correct per-renderer failure boundary when you implement the concrete
alpha-mutation vertical slice. That work establishes the meaningful mutation and failure scopes.

Disposition: decide during concrete alpha-mutation implementation.

No production behavior changed for I4 on the `fix/release-test-packaging` branch.

## I5 — obsolete `UnityAlphaFieldEvidence` instance surface

After the immutable evidence migration (task 5 of the PlatformFinish branch), the
instance-oriented `UnityAlphaFieldEvidence` API appears to have lost its production consumer.
This API includes the constructor, backing dictionary, and `TryGetAlphaField` lookup. The static capture
path remains in use.

Choose one of these potential future cleanup options:

- remove the now test-only constructor/dictionary/lookup surface and retarget the tests that
  still exercise it; or
- retain it deliberately, with an explicit recorded reason.

Disposition: cleanup/debt. It does not block alpha-mutation work.

The API was not removed on the `fix/release-test-packaging` branch.

## Deferred minor review findings

The PlatformFinish final whole-branch review triaged a ledger of ten Minor findings raised
during tasks 5 and 6. Finding 2 was ruled *must fix before PR* and landed in the final fix
wave (`f47bb5f`, `Execute` now rejects a reason-less refusal). The other **nine** were ruled
*defer is correct*. This file records them so the ignored SDD workspace is no longer their only
home. Ledger source:
`.superpowers/sdd/2026-08-22-platformfinish-lifecycle-foundation/progress.md`.

This file preserves the weight that the reviewer gave them. None is an architectural
requirement. This file does not resolve or dismiss any of them. This branch implemented none of them.

### 1. Snapshot Unity-object field guard is shallow (Task 5)

Observed: `AssertHasNoUnityObjectFields` in
`Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs` checks a
type's own fields and their direct generic arguments. It does not recursively walk the whole
captured graph. Thus, it would not catch a live `UnityEngine.Object` that reaches the snapshot through a nested captured
type.

This finding is Minor because construction currently maintains the immutability property across a
small, hand-audited set of capture types. The guard is a safety net, not the mechanism.

Disposition: reconsider during the alpha-mutation work. That branch grows the captured graph,
which is exactly when a shallow net stops covering it.

### 2. Refusal test dereferences `operation.Result` without the `PrepareInvoked` guard (Task 6)

Observed: in `AmuseBuildOperationTests.cs`, the refusal test reads `operation.Result` without
the `PrepareInvoked` precondition assertion that the sibling integration tests use. Thus, a gating
regression causes a bare `NullReferenceException` instead of a named failure.

This finding is Minor because it only makes failures less clear. The test still fails when the behavior
regresses.

Disposition: ordinary test hygiene.

### 3. Plan-mandated assertions inside a `finally` can mask the original diagnosis (Task 6)

Observed: assertions in a `finally` block can throw while the `try` body has an in-flight failure.
This replaces the original diagnosis with a secondary diagnosis.

This finding is Minor because it affects diagnosis quality only during an already-failing run. It does not affect pass/fail
correctness. The plan required this structure.

Disposition: ordinary test hygiene.

### 4. Lifecycle-capability saver facts are never cross-checked against the handed `IAssetSaver` (Task 6)

Observed: the injected capability asserts `hasAssetSaver: true` / `hasAssetContainer: true`.
However, three integration tests run under NDMF's `NullAssetSaver`.
`AmuseBuildOperation.Execute` never compares the capability's saver facts with the
`IAssetSaver` that it receives. The facts and the actual state can disagree without detection.

This finding is Minor because this branch does not persist anything. Thus, the disagreement has no observable
consequence yet. The final review explicitly said that the branch with the first production caller should settle this issue.

Disposition: reconsider during the alpha-mutation work. That branch first makes
saver facts load-bearing.

### 5. `RecordingAssetSaver`'s recording is never observed (Task 6)

Observed: the test double in `AmuseBuildOperationTests.cs` records saver activity that no test
checks.

This finding is Minor because it is unused test scaffolding, not a coverage gap in any claimed behavior.

Disposition: ordinary cleanup. Either check the recording or remove it. The
recording becomes useful if finding 4 is addressed.

### 6. Near-duplicated `TestVrchatPlatform` and supported-facts builder (Task 6)

Observed: `TestVrchatPlatform` and the supported-facts builder have nearly identical forms in
both `AmuseBuildOperationTests.cs` and `AmusePlatformFinishPluginTests.cs`. The plan
required this duplication.

This finding is Minor because both copies are small and correct. The cost is a risk of drift, not a defect.

Disposition: ordinary cleanup/test hygiene.

### 7. A second unconditional `.AfterPlugin("com.alrauna.amuse")` sibling is order-unconstrained (Task 6)

Observed: two test plugins declare `.AfterPlugin("com.alrauna.amuse")` unconditionally
(`AmuseBuildOperationTests.cs:504`, `AmusePlatformFinishPluginTests.cs:206`). Their order
relative to each other is not constrained. No test currently arms both scopes at once.

This finding is Minor because the latent nondeterminism is unreachable while no test arms both.

Disposition: known implementation debt. It becomes real only if a future test arms both scopes.

### 8. Baseline Console noise in the full suite (Task 6)

Observed: a complete public EditMode run retains three NDMF Harmony
`Exception: mprotect returned EACCES` entries. The console is an environment baseline, not
pristine. Thus, read "no unexpected Console errors" against that baseline.

This finding is Minor because it comes from NDMF/Harmony on this host, not from AMUSE, and causes no failures.

Disposition: known environment debt. Record the baseline instead of trying to remove it.

### 9. Refusal-test non-vacuousness depends on NDMF's exact log prefix (Task 6, post-fix)

Observed: after the fix wave, the refusal test captures `Application.logMessageReceived` to prove
"no error was reported". It relies on NDMF logging every `ReportError` as an exception or with the exact prefix
`"[NDMF] Error Reported: "` (`AmuseBuildOperationTests.cs:28`). A future NDMF logging change would silently reduce it to
`Successful`-level coverage without causing a failure.

This finding is Minor because this is the strongest mechanism available under this branch's no-reflection and
no-asmdef-change constraints. The reviewer confirmed that no better route existed.

Disposition: known implementation debt. Revisit it only if the asmdef/reference constraints are
ever relaxed. That change would permit direct checks of `ErrorReport.Errors`.
