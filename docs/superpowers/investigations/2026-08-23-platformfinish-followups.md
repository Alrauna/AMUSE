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
