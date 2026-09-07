# Play mode admission and lifecycle reports - slice plan

Date: 2026-09-06. Base: `main` at `d71116c` (merge of PR #71). Branch:
`feature/play-mode-admission`.

Authorization: the user chose option C after
`docs/superpowers/investigations/2026-09-06-play-mode-build-path.md`.
Option C admits Play mode (reverses the recorded decision V11) and reports
every lifecycle refusal (closes the V7 gap the investigation found). The
scope spec records the V11 reversal in its corrections section. The stable
0.1.0 cut has not happened, so anything merged to `main` before that flip
ships in 0.1.0.

## Contract

- `HostLifecycleCapability.Evaluate` admits `ApplyOnPlay` and
  `NonPlayNdmfBuild`. It refuses `Unknown` with
  `HostLifecycleRefusal.UnsupportedBuildPath`, which now means
  "unclassified build path", not "Play mode".
- The capability carries the `AmuseBuildPath` it decided on, so the
  inspector status can name the run kind.
- The barrier reports one Information entry per lifecycle refusal, with the
  avatar root as context object and the existing `amuse.host.<cause>`
  strings. A refusal of `None` alongside `MayUsePositiveMutation == false`
  is an invariant break and throws.
- The avatar summary status label says `Last play mode run:` on
  `ApplyOnPlay` and `Last upload:` otherwise. The report text is
  path-neutral already.
- The component inspector pre-build guidance says AMUSE runs at upload and
  in Play mode.
- Consent (V8) is unchanged: Play mode prompts like an upload, because the
  user is present at Play entry. If the modal dialog breaks Play entry in
  smoke validation, a follow-up switches Play mode to the batch
  decline-and-report path. That fallback is out of scope here.

Non-goals: no change to the trigger (component still required), no change
to coverage limits, no change to consent rules, no release dispatch.

## Tasks

1. **Gate flip (RED/GREEN).** Rewrite
   `HostLifecycleCapabilityTests.ApplyOnPlayRefusesWithLifecycleReason`
   into an admission test and add a pipeline-level test that runs the
   renderer loop under `ApplyOnPlay` facts. Both observed RED against the
   current refusal first. The `Unknown` row stays as the fail-closed
   guard. `AmuseBuildOperationTests.RefusedCapability` moves from
   `ApplyOnPlay` to `Unknown` facts so its `UnsupportedBuildPath` pin
   stays true for the right reason.
2. **Lifecycle reports (RED/GREEN).** New pipeline test: refusing facts
   produce exactly the plain English console entry. Observed RED against
   the silent return first. Implementation: `AmuseReports.LifecycleRefusal`
   plus the one call before the barrier's early return. The
   `UnsupportedBuildPath` strings are rewritten for the unclassified
   meaning.
3. **Run-kind status label (characterization).** `AmuseReports.AvatarSummary`
   takes the build path and writes the matching label. New assertions land
   with the implementation because the signature is new; recorded as
   characterization, not RED.
4. **Inspector guidance text.** Text-only change, editor smoke check.
5. **Spec correction.** Dated V11 reversal note in the scope spec
   corrections section.

## Validation

- Unity refresh, focused EditMode filters per task, then the full
  `Alrauna.Amuse.Tests.Editor` assembly. Observed counts recorded in the
  PR.
- RED runs observed for tasks 1 and 2 before their fixes.
- `git diff --check` before the PR.
- Post-merge, user-side Play mode smoke on the real avatar: summary report
  appears, the `Hidden/lilToonTransparent` mesh moves subject to the
  coverage limits, and the GPU readback route is thereby characterized
  under Play mode. If the consent modal blocks Play entry, stop and
  report.

## Stop conditions and boundaries

- Any test flip beyond the named pins stops the slice and reports.
- Commits stay on this branch; the PR goes through the automerge gate; no
  release dispatch.
