# Investigation: refusal reporting gaps in the NDMF console

Date: 2026-10-12. Branch: `fix/refusal-reporting-gaps`. Base: `main` at `2e9bee9`.
Status: source audit complete, adversarially reviewed. No code change on this
branch yet.

Privacy note: this record is a pure source audit. It names no avatar, renderer,
material, texture, path, instance, port, or hash from the private corpus. It
quotes product report keys, refusal cause names, and commit subjects. These are
public product data.

Review note: three independent adversarial reviews checked this record against
the source on 2026-10-12. The review corrected the G2 storage type, narrowed
the G2 geometry-extraction case, refuted the first G4 caveat, added gaps G5
and G6, corrected three test-coverage items, and resolved the
`RefusalReason` and report-service reachability questions. This version carries
those corrections.

## Question

For every refusal cause in AMUSE, does the cause reach the NDMF console? The
branch name says the goal: find the refusal facts that stay silent, and the
facts that reach the console in a weaker form than they deserve.

## Method

Four parallel read-only source audits ran on 2026-10-12 against this branch:

1. Refusal taxonomy. Every refusal enum, every value, every production site.
2. Reporting surface. Every `AmuseReports` method, key, severity, and caller.
3. Flow gap analysis. Every pass decision point: reported, stored only, or
   dropped.
4. Prior art and tests. The 2026-09-27 and 2026-09-29 records checked against
   current code, plus test coverage of reporting.

Three adversarial reviews then re-walked every load-bearing claim against the
source. All file and line references come from this checkout. Line numbers
drift.

## Answer in one paragraph

Every closed refusal enum value has a report path today. Every finding from the
2026-09-27 record (findings 1, 2, 3, 6, 7) and every family from the
2026-09-29 record (a, b, c) is fixed on this base. Six silent paths remain.
They share one shape: a refusal fact is captured or computed, and then the fact
dies inside a larger outcome instead of being named. The console never shows
why a proof was lost in these cases.

## The reporting channel today

- `Build/AmuseReports.cs` is the only consumer of `ErrorReport.ReportError`
  in production code. `HostLifecycleCapability.cs` reads the report service
  but never reports through it.
- Every message uses `ErrorSeverity.Information`. A refusal never blocks a
  build by policy.
- Renderer-scoped reports wrap in `ErrorReport.WithContextObject`, so the NDMF
  console links to the live object.
- Keys follow `amuse.<family>.<cause>`. Verified family literals are
  `amuse.host.*`, `amuse.avatar.*`, `amuse.renderer.*`,
  `amuse.slotAnalysis.Refusal`, `amuse.slotSeparation.<cause>`,
  `amuse.texture.<reason>`, `amuse.consent.*`, `amuse.sampling.*`, and
  `amuse.summary.*`.
- The vocabularies pair one to one: `RendererAnalysisRefusal` 17 non-None
  values, `HostLifecycleRefusal` 7, `AvatarAnimationRefusal` 5,
  `AlphaSeparationSlotRefusal` 16, `TextureCaptureRefusalReason` 3.
- Four refusal report methods throw on the None member of their enum:
  `SlotAnalysisRefusal`, `SlotSeparationRefusal`, `LifecycleRefusal`, and
  `AvatarRefusal`. Two do not: `RendererRefusal` and `TextureCaptureRefusal`
  would build the keys `amuse.renderer.None` and `amuse.texture.None` and emit
  an empty-titled entry. No production caller passes None today. This
  asymmetry is a hardening nit, not a live gap.
- The deliberate build-blocking channel is the uncaught exception. The renderer
  loop has no try or catch by design. An exception there is an implementation
  defect and surfaces as an NDMF internal error.
- Editor-session refusals (`PresetLoadRefusal`, 8 values) report through the
  component inspector HelpBox. That is the correct channel. They are not build
  facts.

## Prior findings, all verified fixed on this base

Three adversarial reviews confirmed every commit attribution and current
shape below.

| Prior finding | Fix evidence |
|---|---|
| 2026-09-29 family a: slot refusal plus texture refusal read as duplicates | Folded capture facts live in the slot entry. Standalone texture entries fire only for resolved slots. Commits `d2ba6f87`, `3a07bb6a` |
| 2026-09-29 family b: consent decline reported twice | Single report site at `AmusePlatformFinishPlugin.cs:567`. Commit `e08a865c` |
| 2026-09-29 family c: avatar animation refusal reported twice | The structural pass stores state and reports nothing. One site at `AmusePlatformFinishPlugin.cs:521`. Commit `44a8ee39` |
| 2026-09-27 finding 1: summary sentence overstated coverage | `UntouchedRendererCount` counts fully refused renderers. Commits `a1eed776`, `646f8534` |
| 2026-09-27 finding 2: status store could not surface on play and upload | Store is keyless now. Commits `dc695163`, `96b914ae` |
| 2026-09-27 finding 3: report text named build-copy objects | `RegisteredSourceIdentity.Resolve` names the authoring asset. Commits `ef2f7c71`, `348bf36b`, `646f8534` |
| 2026-09-27 finding 7: no-identity capture hint gave false advice | Hint names the in-memory-copy mechanism and the known producers. Commits `aa2e4b38` through `0c04efcd` |
| 2026-09-27 finding 6: avatar-scope `AmusePreparationDecision.Refused` had no call site | Redesigned struct now has one call site at `AlphaSeparationApply.cs:187` |

Anisotropy reporting landed after these fixes (`e1a4daf6`, 2026-10-11). It
reports one `amuse.slotSeparation.AnisotropyDifferences` entry per affected
slot, tested at three levels (state, helper, end to end). Its end-to-end test
`AnisotropyDifferencesRefusalReportsToNdmfConsole` is the model for the fixes
below.

## Open gaps: refusal facts that never reach the console

### G1. A lost proof reason dies inside triangle-level Unknown

`Analysis/AlphaSemanticsResolver.cs` refuses a material resolution with one of
`UnsupportedMultiplier`, `MissingTextureEvidence`, or `UnsupportedUvMapping`.
`Analysis/AdmittedMaterialStates.cs:ResolveSlot` promotes only the
`SemanticsUnknown` failure to a reported slot refusal. The other three fall
through into a resolved slot. `Host/UnityRendererAlphaAnalysis.cs:Classify`
then answers `TriangleAlphaOutcome.Unknown` for every triangle of that
material.

The gap is total. The review confirmed the slot produces no console entry of
any kind today:

- The refused-slot loop skips it, because the slot `IsResolved`.
- No standalone texture entry applies, because no capture refused.
- No policy entry fires later. All-Unknown triangles give the submesh zero
  opaque candidates, so the planner leaves it `Unchanged` and preparation
  continues past the coverage branch. `OpaqueCoverageBelowMinimum` fires only
  for split submeshes with partial candidates.
- The renderer is analyzed, so the summary counts it as analyzed and moved
  zero triangles from it. No entry names why.

Fix constraints established by review:

- A naive promotion of the slot to refused changes renderer-level behavior.
  When no other slot resolves, `ResolveRuntimeStates` returns a refused
  renderer, which changes census buckets, summary counts, and skips geometry
  capture. Classification outcomes would not change (the refused resolution
  already yields all-Unknown), but the reporting side effects would.
- A report-only shape must carry `AlphaResolutionFailure` out of `Analysis/`
  onto the slot result. Today the failure travels only on `AlphaResolution`,
  and `SlotResolutionResult` has no field for it. `capturedSemantics`
  carries an `AlphaUnknownReason` only on the SemanticsUnknown paths.
- No existing sentence builder consumes `AlphaResolutionFailure`.
  `FeatureSentence` switches on `AlphaUnknownKind`; `CaptureSentence` formats
  texture refusals. A new builder and new string-table entries are required,
  and the completeness tests force them.

### G2. Per-slot evidence facts die for renderers refused at renderer scope

The review corrected the storage type and narrowed the scope. Capture refusals
live per slot on `CapturedMaterialSlotEvidence.CaptureRefusals`
(`Host/CapturedAnimationEvidence.cs:164`). The locked-identity pre-check fact
(`LockedIdentityRefusal`) is recorded at capture time the same way.

These facts surface only through two loops in `AmusePlatformFinishPlugin.cs`:

- The refused-slot entry folds them, but only from `SlotResults`.
- The standalone `amuse.texture.*` loop runs only when the renderer analysis
  found no renderer-scope refusal.

Two distinct silent scenarios survive review:

1. When `ResolveRuntimeStates` refuses the renderer (`UnrecognizedAnimatedMaterialBinding`,
   additive layer, unnormalized direct blend tree, and the other early
   refusals), it returns empty `SlotResults`. No slot entry fires at all. Every
   capture refusal and every locked-identity fact of that renderer dies. The
   swap-in attestation skip claims the pre-check "reports later", but that
   deferral holds only when slot resolution runs. On these renderers nothing
   reports later.
2. When geometry extraction refuses, the refused-slot fold loop has already
   run and reports refused slots, but the resolved slots lose their standalone
   texture entries, because that loop sits inside the no-refusal branch.

Host-structural refusals continue before capture, so no evidence exists there.
Any fix must tolerate that absence.

Fix shape confirmed by review: folding per-slot evidence into the
renderer-scope entry follows the 2026-09-29 fold principle at renderer scope.
`AmuseReports.RendererRefusal` already carries a `detail` argument rendered as
`{3}`, and the closure sentence already uses it at the post-analysis report
site. The fix composes the capture-refusal and locked-identity summary into
that detail, or emits per-slot entries before the continue.

### G3. The restore mismatch detail is computed and dropped

`Build/TransientUnlockSwapIn.cs:RestoreMismatchReason` computes a specific
broken fact: clone shader name or GUID mismatch, lock flag not zero, original
asset missing, or locked shader asset missing. `RecordRefusal` passes no
detail, although `AmuseReports.SlotSeparationRefusal` supports a detail
argument, and the conversion path already uses that argument for
`OpaqueConversionRefused`. The mismatch reason string is in scope at the call
site.

Fix shape: thread the mismatch reason into the report as the detail argument.

### G4. A close-time graph refusal forces retention without being named

`Build/TransientUnlockWindowClose.cs` re-enumerates the committed graph. When
that second enumeration refuses, or the sentinel fires on null bindings, the
refusal forces clone retention. The console shows only
`amuse.slotSeparation.TransientUnlockCloneRetained` entries, never the refusal
cause.

The first version of this record carried a duplicate-risk caveat. All three
reviews refuted it with the same chain: a barrier avatar refusal returns at
`AmusePlatformFinishPlugin.cs:518-526` before consent and before the swap-in
call, the swap-in is the only producer of open pairs, and the close pass exits
when no pairs exist. So on every reachable close-time retention,
`state.AvatarRefusal` is None, and an unconditional close-time avatar entry can
never duplicate the barrier report. The caveat is dead reasoning; the gap is
the only reachable case.

Fix shape: emit one `AmuseReports.AvatarRefusal` entry when the close-time
enumeration refuses. Folding the cause name into the retention detail is
optional locality, not a guard.

### G5. A conversion refusal dies when a sibling material converts

New gap from the completeness review. In a multi-material slot
(`AlphaSeparationPreparation.cs`, conversion loop), a per-material conversion
refusal is stored as `lastConversionRefusal` and the material stays on its
live shader. The slot fires the refusal only when every admitted material
failed to convert. When at least one sibling converts, the failed material's
refusal is never reported: its triangles silently stay unconverted with no
entry naming why.

Same user-visible class as G1: a partial outcome with no named reason for the
part that did not convert.

### G6. Retention with an empty slot list passes silently

New gap from the completeness review. The close pass class doc states the
retention never passes silently. The retention reporting loop iterates
`pair.Slots`. A pair whose every swap attempt failed records no slots. On such
a pair, a close-time retention emits zero entries, against the class contract.

Reachability is narrow: the swap-in must open a pair and then fail every slot.
The contract violation is the finding; the fix is either an entry for the
slot-less pair or a corrected contract.

## Residual smells, no console hole

1. `AmusePreparationDecision.RefusalReason` is stringly typed. The one
   production `Refused()` call smuggles the enum name `SamplingPolicyChanged`
   through a string. The review resolved a conflict here: the property IS
   read in production, at `AlphaSeparationApply.cs:60`, for the non-empty
   invariant. The string is validated but never rendered. The smell is the
   enum-name smuggling, not a dead field.
2. `AmusePlatformFinishState.ConsentDeclined` is write-only in production. Its
   doc claims a report consumer. Delete the flag or fix the doc.
3. The `MissingBuildContextServices` self-referential channel is unreachable
   in practice. The review verified NDMF's `BuildContext` always creates its
   own report, so `context.ErrorReport` is never null on a real build. If it
   ever were, `ErrorReport.CurrentReport?.AddError` would silently no-op. Downgraded
   to a hardening note.
4. `AmuseBuildPath.Unknown` has no production construction site. It is
   reachable only from hand-built facts in tests. Dead value.

## Deliberate silences, recorded so review does not call them gaps

- No AMUSE component on the avatar root, and the feature switch off, return
  with no report. Documented total no-ops.
- An unlock window that never opened reports nothing. No refusal fact exists.
- A plan with no opaque candidates returns null from preparation. Nothing
  would have changed.
- Swap-in skips a destroyed renderer or a changed slot without a record. The
  attestation skip defers to the capture pre-check. The review flagged the
  deferral's boundary: it holds only when slot resolution runs, which is G2
  scenario 1.
- Success paths stay silent: sweep of unreferenced clones, reversion writes.
- One shared material on two slots prints one line per slot. Stated intent.
- Census buckets (`RecordRendererRefusal`, `RecordSlotRefusal`) have no
  production readers. They feed the research collection surface, not the
  console.

## Reporting test coverage

Three reviews corrected this list. Corrections: the avatar-refusal
exactly-once count, the renderer-refusal end-to-end capture, and the
registered-source report-level test all exist and moved to the with list.

With coverage:

- The slot fold at helper and end-to-end level, including the inverse pair for
  resolved slots.
- The consent decline exactly-once count.
- The avatar animation refusal exactly-once count
  (`AvatarAnimationRefusalReportsExactlyOneEntry`).
- The anisotropy entry at three levels.
- The summary and status store, including `UntouchedRendererCount` semantics.
- The lifecycle refusal end to end.
- A renderer-refusal end-to-end message capture
  (`MappingRefusalReportNamesBothSlotCounts`).
- Registered-source naming at report level
  (`LockedRegisteredBuildCopy_NamesTheAuthoringAssetInItsSentinelReport`).
- String-table completeness for every vocabulary.

Without coverage:

1. `AmuseReports.TextureAlphaMissing` and the `NoAlphaChannel` end-to-end
   emission. Zero test references.
2. `AmuseReports.SlotSeparationDivergence` and
   `SlotSeparationPremultiplyNormalization`. No test call sites.
3. The per-subject `amuse.consent.Subject` entries. Key pinned, count and
   content unpinned.
4. None-guard throw tests exist only for `LifecycleRefusal` and
   `ClosureFailureSentence`. The other guarded methods are untested for None,
   and the two unguarded methods are untested for the None-key behavior.

## Recommendation for this branch

Order the work by user value:

1. G1. The lost proof reason is the highest-value silent fact. The design must
   pick the report-only shape: a new diagnostic entry for resolved slots whose
   material resolution refused, carrying the failure name and offender
   material. Report-only avoids the renderer-flip side effects of promotion.
2. G2, including the locked-identity extension. Restores the fold principle at
   renderer scope.
3. G5. Same silent partial-outcome class as G1, in the conversion path.
4. G3 and G4. Small evidence threading. G4 is an unconditional close-time
   avatar entry.
5. G6. Narrow edge, one contract decision.
6. Residual smells. One doc fix, one flag deletion, one dead value. The
   None-guard asymmetry can ride along as hardening.

RED/GREEN evidence per fix: a captured-report test through
`ErrorReport.CaptureErrors` that fails on current code for the named wrong
behavior, then the minimal reporter change. The anisotropy end-to-end test is
the pattern to copy. G1 needs a new fixture twist: no existing fixture
produces a non-SemanticsUnknown resolution failure, so the test must build one
(for example an out-of-range alpha multiplier on a verified material), and the
fixture work must confirm which resolver gate fires first.

## Scope and limits

This record ran no build and changed no code. Every claim rests on source
reading of this checkout at base `2e9bee9`, cross-checked by four independent
audits and three adversarial reviews. Two key-literal paraphrases from the
audits were corrected against `AmuseReportStrings.cs` before publication. The
reviews refuted four claims from the first version of this record; this
version carries the corrections with review attribution. Line numbers drift as
code moves. G6 reachability is code-read, not measured on a build. The G1
fixture feasibility is analysis, to be proven by the RED test itself.
