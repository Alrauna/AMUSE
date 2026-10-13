# Refusal reporting gaps design

Date: 2026-10-12. Status: design complete, adversarially reviewed. Branch:
`fix/refusal-reporting-gaps`. Base: `main` at `2e9bee9`. Spec authority for
the plan dated the same day.

Privacy note: this record is a source-level design. It names no private asset,
path, instance, or corpus fact. It quotes product report keys, refusal cause
names, and code symbols. These are public product data.

Review note: two adversarial reviews checked this design against the source
and the repo policy on 2026-10-12. The reviews found a missing design for G3,
refuted the D1 index-mapping premise, proved the first G6 destroy rule unsafe,
caught a double-report in the first G5 emission rule, and corrected the D2 key
assertion. This version carries those corrections.

## Problem

The investigation at
`docs/superpowers/investigations/2026-10-12-refusal-reporting-gaps-investigation.md`
found six refusal facts that never reach the NDMF console (G1 through G6),
plus reporting test holes and small hygiene defects. Every finding was
adversarially verified against the source. This design fixes all six gaps and
the hygiene items. No gap fix changes classification, planning, census, or
summary behavior. G1 through G5 are report-only. The hygiene section adds
throw guards and deletes a dead flag.

## Goals

1. A user reading the NDMF console can name why any proof was lost, why any
   conversion did not happen, and why any clone was retained.
2. One fact, one entry. No new duplicates. The fold principle of the
   2026-09-29 work extends to renderer scope.
3. Every new key, sentence builder, and emission gets string-table entries and
   tests. The completeness tests extend to the new vocabulary.
4. RED/GREEN evidence per fix, observed in the Unity Test Runner, EditMode.

## Non-goals

- No classification outcome changes. A refused resolution still classifies
  all-Unknown. A partial conversion still proceeds with the converted subset.
- No severity changes. Every new entry stays `ErrorSeverity.Information`.
- No census semantics changes. `RecordRendererRefusal` and `RecordSlotRefusal`
  keep their current meaning. New diagnostic entries write no census.
- No changes to existing refusal enums. G1 adds no enum value; it reports the
  existing `AlphaResolutionFailure` values.
- No renderer-scope behavior changes. A resolved slot never becomes refused.
- No clone lifecycle changes. The close pass keeps its destroy and retain
  rules exactly as they are.
- No NDMF or upstream changes. No research package changes.
- The existing all-fail conversion path keeps its single slot-level entry.
- No-alpha diagnostics that die on renderer-refused paths are in scope for D2;
  they ride the same emission rule, not a new one.

## D1. G1: name the lost proof reason on a resolved slot

Fact: a resolved slot's admitted material resolution can refuse with
`UnsupportedMultiplier`, `MissingTextureEvidence`, or `UnsupportedUvMapping`.
`ResolveSlot` promotes only `SemanticsUnknown`. The other three ride inside
the slot's resolutions as refused `AlphaResolution` records. The slot stays
resolved, the triangles classify all-Unknown, and no console entry names the
cause today.

Decision: report-only diagnostic. The alternative, promoting the slot to
refused, flips the renderer to refused when no sibling slot resolves
(`ResolveRuntimeStates`), which changes census buckets and summary counts.
Report-only adds zero aggression and is the conservative choice.

Carrier design, corrected by review:

- `ResolveRuntimeStates` stores `DistinctResolutions(resolved.Resolutions)`,
  which merges resolutions that are exactly interchangeable. The stored list
  therefore does not parallel `AdmittedMaterialIndices`: two materials
  refusing with the same failure merge into one entry, and index positions
  shift. Reading the stored list cannot attribute the offender material.
- `SlotResolutionResult` gains one additive field,
  `MaterialResolutions`, the raw pre-dedup list from `ResolveSlot`, parallel
  to `AdmittedMaterialIndices` by construction. The deduped `Resolutions`
  field keeps feeding its current consumers; `IntersectResolvedOutcomes` and
  the anisotropy scan are duplicate-idempotent, so no downstream outcome
  changes. `ResolveSlot` passes its own list; `ResolveRuntimeStates` passes
  the same list through. This is the only Analysis change in this design, and
  it is additive only.
- Offender indirection, pinned: resolution `i` names the material
  `evidence.AdmittedMaterials[slot.AdmittedMaterialIndices[i]]`. Admission
  indices are not contiguous, so the emission site must go through
  `AdmittedMaterialIndices`, never `AdmittedMaterials[i]`. A wrong
  implementation here compiles and misnames the material. The RED test
  includes non-contiguous admission indices to catch it.

Report shape:

- New report method `AmuseReports.SlotResolutionRefused(Renderer, int
  slotIndex, string rendererName, CapturedAlphaMaterial offenderMaterial,
  AlphaResolutionFailure failure)`. The doc comment states the contract: the
  slot stays resolved, nothing is refused at slot scope, the entry is a
  diagnostic. Context object: renderer.
- New key family `amuse.slotResolution.<Failure>`, two segments, matching the
  documented `amuse.<family>.<cause>` grammar. Three keys:
  `amuse.slotResolution.UnsupportedMultiplier`,
  `.MissingTextureEvidence`, `.UnsupportedUvMapping`.
- New key builder `AmuseReportStrings.SlotResolutionKey(AlphaResolutionFailure)`.
  It throws on `None` and on `SemanticsUnknown`. The comment states why:
  `None` never names a fact, and `SemanticsUnknown` promotes to a slot
  refusal at `ResolveSlot` and never reaches this path.
- String-table entries per failure: title, description, hint. Argument
  contract, pinned: title `{0}` is the slot index. Description `{0}` slot
  index, `{1}` renderer name, `{2}` the offender material description,
  `{3}` the failure sentence. Hint names the material setup to check.
  Sentence style follows the existing table: short active sentences, no
  semicolons, no contractions.
- New sentence builder `AmuseReports.ResolutionFailureSentence(
  AlphaResolutionFailure)` producing one plain sentence per value. It throws
  on `None`, on `SemanticsUnknown`, and on any unhandled value, matching the
  guard pattern.

Emission:

- Site: `AmusePlatformFinishPass.Execute`, between the out-of-range closure
  loop and the geometry-capture branch. The loop carries its own explicit
  renderer-refusal None gate; the fold loop above it has no gate to reuse.
- Walk: for each slot whose `SlotResolutionResult.IsResolved`, walk
  `MaterialResolutions` in order. For each entry with `IsResolved == false`,
  emit one `SlotResolutionRefused` with the failure and the offender material
  through the indirection above. Slots that are not resolved report through
  the fold loop and are skipped here.
- A later geometry extraction refusal does not suppress these entries. The
  lost-proof fact predates extraction, and no other entry carries it.
- Granularity: one entry per refused material resolution. Two materials
  refusing with the same failure emit two entries.
- Census and summary: untouched.

Fixture fact, verified by review: an out-of-range alpha multiplier on a
verified fixture material reaches `UnsupportedMultiplier` before every other
resolver gate, including with no captured chain. The multiplier path needs no
texture evidence, so the RED fixture is a verified material with a multiplier
above one.

Tests:

- Helper level: one test per failure value asserting key, slot index, renderer
  name, and material description. One test asserting the throw on `None` and
  on `SemanticsUnknown` in the key builder and the sentence builder.
- End to end, RED first: the multiplier fixture under
  `ErrorReport.CaptureErrors` asserts exactly one
  `amuse.slotResolution.UnsupportedMultiplier` entry, zero
  `amuse.slotAnalysis.Refusal` entries for that slot, and a summary count
  unchanged from the no-fixture baseline.
- Falsifier 1, marked and guarded: two admitted materials, same failure, in
  one slot. Asserts two entries with two offender names. A plausible wrong
  implementation reading the deduped `Resolutions` emits one.
- Falsifier 2, marked and guarded: non-contiguous admission indices. Asserts
  the entry names the material at the admitted index, not the material at the
  loop index.

## D2. G2: report per-slot evidence facts for renderer-refused renderers

Fact: when `ResolveRuntimeStates` refuses the renderer, `SlotResults` is
empty. The fold loop iterates nothing and the standalone texture and
no-alpha loops are gated off. Capture refusals, locked-identity pre-check
facts, and no-alpha diagnostics of that renderer die. When geometry
extraction refuses, refused slots have reported, but resolved slots lose
their standalone texture and no-alpha entries.

Decision: additive per-slot emissions on the renderer-refused path, using
existing report methods and keys. Composing evidence into the renderer
entry's detail argument was rejected: only one renderer template renders the
detail argument today, so composed evidence would be invisible for the other
16 causes.

Design:

- Emission site: the renderer-refused path in `AmusePlatformFinishPass.Execute`,
  before the `continue`. Both the `ResolveRuntimeStates` refusals and the
  extraction refusal report through this site, so one emission covers both.
- Slot rule. Emit for every slot whose facts did not surface elsewhere: when
  `SlotResults` is empty, every slot with facts; when `SlotResults` is
  populated, only resolved slots, because refused slots already folded their
  facts.
- Capture refusals: `AmuseReports.TextureCaptureRefusal` per stored refusal,
  the same method the standalone loop uses.
- No-alpha diagnostics: `AmuseReports.TextureAlphaMissing` per recorded
  property, added by review. These facts died on the same paths; the rule
  that restores capture refusals restores them.
- Locked identity: `AmuseReports.SlotAnalysisRefusal` with the recorded
  `LockedIdentityRefusal` cause. That method emits the fixed key
  `amuse.slotAnalysis.Refusal` and carries the cause as an argument, so the
  entry text names the shader fact. When one slot's materials carry
  different locked refusals, emit at the first carrier in material order,
  the same precedence `ResolveSlot` applies. One entry per slot. This is
  report-only: no `RecordSlotRefusal` call, so census keeps counting
  analyzed-slot refusals only. The emission site carries a comment stating
  that choice.
- No duplication. On a resolving renderer, the new emissions never run. On a
  refused renderer, the standalone loops never run, and the fold loop either
  did not run (empty results) or skipped resolved slots. Each fact surfaces
  exactly once. The review walked all four renderer-refusal shapes and found
  no overlap.
- Host-structural refusals continue before capture. No evidence exists, so
  no emission fires. The code states this tolerance in a comment.

Tests:

- End to end, RED first: the established additive-layer fixture, extended so
  one slot's texture capture refuses. Asserts the renderer refusal entry,
  exactly one `amuse.texture.*` entry for the refusing slot, where current
  code emits none.
- End to end for the locked-identity case: a locked material with an
  unattested original shader on a renderer that refuses at renderer scope.
  Asserts exactly one `amuse.slotAnalysis.Refusal` entry whose text names the
  unattested-original fact, where current code emits none.
- No-alpha case: same fixture shape, asserting the `amuse.texture.
  NoAlphaChannel` entry appears for renderer-refused renderers.
- Falsifier 3, marked and guarded: a fully resolving renderer with the same
  fixtures produces exactly the current entries, no more. A plausible wrong
  implementation that emits unconditionally fails this.

## D3. G3: carry the restore mismatch detail into the report

Fact: `TransientUnlockSwapIn.RestoreMismatchReason` computes a specific
broken fact: clone shader name or GUID mismatch, lock flag not zero, original
asset missing, or locked shader asset missing. `RecordRefusal` passes no
detail, so the entry names only the enum. The mismatch reason string is in
scope at the call site, `AmuseReports.SlotSeparationRefusal` supports a
detail argument, and the conversion path already uses that argument.

Design:

- `RecordRefusal` gains a detail parameter. Its callers pass the computed
  mismatch reason.
- Key and template unchanged: `amuse.slotSeparation.
  TransientUnlockRestoreMismatch` already renders the detail position.
- No other swap-in reporting changes.

Tests:

- Pass level, RED first: drive the swap-in pass with a forced restore
  mismatch and assert the rendered entry contains the mismatch fact sentence
  for that shape, where current code renders the bare cause. The mismatch
  detail rendering itself already exists in the template; the gap is that no
  caller supplies it.

## D4. G5: name a conversion refusal when a sibling converts

Fact: in a multi-material slot, a per-material conversion refusal is stored
in `last*` locals and the material stays on its live shader. The slot fires
the refusal only when every admitted material failed. When any sibling
converts, the failed material's refusal is never reported. The `last*`
locals also cannot express two refused materials.

Decision: collect per-material refusal records in the loop and emit them when
the slot continues.

Design:

- Replace the `lastConversionRefusal` / `lastRefusedMaterial` /
  `lastConversionRefusedDetail` locals with a per-material record list
  collected at the same site.
- Post-loop emission, corrected by review: when the slot refuses (the
  all-fail case), the existing single slot-level entry fires exactly as
  today, from the LAST collected refusal record. Today's `last*` locals
  overwrite per refused material and the code comment names the last
  conversion failure, so last-record is the behavior-preserving choice. When
  the slot continues (at least one sibling converted), emit one
  `AmuseReports.SlotSeparationRefusal` per collected record with its cause,
  offending material, and detail, with the same named arguments the all-fail
  branch passes: renderer, slot index, cause, `offendingMaterial`, `detail`.
  No renderer name and no proven-mapping argument, so the partial entries
  carry the same content shape as the single-material path.
- The two paths are mutually exclusive by construction, so no fact reports
  twice. The in-loop emission shape was rejected because it double-reports
  the all-fail case against the guard test below.
- Census: the partial path writes no `RecordSlotRefusal`, because the slot is
  not refused. The comment at the emission site states this.
- Key reuse: `amuse.slotSeparation.<cause>`. No new keys.

Tests:

- End to end, RED first: a two-material slot fixture where one material
  converts and one refuses conversion. Asserts exactly one
  `amuse.slotSeparation.*` entry naming the refusing material, where current
  code emits none.
- Falsifier 4, marked and guarded: the all-fail case still emits exactly one
  slot-level entry, not one per material and not one plus per-material
  entries. Both plausible wrong implementations fail this.

## D5. G4: name a close-time graph refusal

Fact: the close pass re-enumerates the committed graph. A refusal there
forces clone retention. The retention entries never name the cause, and no
avatar-scope entry exists. Three reviews confirmed the barrier's avatar
refusal always returns before the swap-in, the swap-in is the only producer
of open pairs, and the close pass exits when no pairs exist. An unconditional
close-time entry therefore can never duplicate the barrier report.

Design:

- Emission site: `TransientUnlockWindowClose`, immediately after the graph
  enumeration. When `graph.Refusal != AvatarAnimationRefusal.None`, emit
  `AmuseReports.AvatarRefusal(avatarRoot, graph.Refusal)` once. The null
  bindings sentinel maps to the same call.
- The retention entries stay unchanged. The avatar entry names the cause for
  the whole build; per-slot duplication of the cause adds nothing.
- The method's existing None guard applies: the emission only fires on a
  non-None refusal.

Tests:

- End to end, RED first: a build that opens a pair, then seeds
  `state.AnimatorBindings` to null so the close-time sentinel fires. Asserts
  exactly one `amuse.avatar.*` entry from the close pass, where current code
  emits none.
- Falsifier 5, marked and guarded: a build whose barrier reports an avatar
  refusal never reaches the close pass, asserted by entry counts. A plausible
  wrong implementation that reports in both passes fails this.

## D6. G6: settle the slot-less pair retention contract

Fact: the close pass class doc states the retention never passes silently.
The retention loop iterates `pair.Slots`, the slots whose swap succeeded. A
pair whose every swap attempt failed records no slots, so a retention on that
pair emits nothing. A pair is keyed per locked material across the whole
avatar, so a slot-less pair carries no slot index and no single renderer
scope. No existing report method fits it.

Decision, corrected by review: characterization and contract correction
only. The first draft prescribed destroying slot-less pairs on the retention
path. The review proved that unsafe: curve inversion rewrites whole-entry
curves regardless of slot success, so a slot-less pair can still carry live
references to the clone. Destroying it under unproven inversion is a more
aggressive transformation under more uncertainty, which the correctness
policy forbids. The close pass keeps its destroy and retain rules exactly as
they are.

Design:

- Step one, characterization: a test that drives a pair with no recorded
  slots through the close pass on both the success path and the retention
  path, and observes the pair's disposition. The test asserts what the code
  does, not what the doc says. It is permanent and pins the rule.
- Step two, doc correction: the class doc states the precise contract. The
  never-passes-silently promise covers pairs with recorded slots. A slot-less
  pair has no slot or renderer scope to report against, so its disposition is
  silent by contract, with the reason stated. In the refusal-driven case the
  close-time avatar entry from D5 already names the cause for the whole
  build.
- No code change to the close pass.

## D7. Hygiene and reporting test holes

1. Delete `AmusePlatformFinishState.ConsentDeclined`. It is write-only in
   production and its doc claims a consumer that does not exist. Seven test
   references exist (four in `AmusePlatformFinishPluginTests`, two in
   `VersionConsentTests`, one string use in `MergedConsumptionFixture`); all
   are mechanical removals. The decline report keeps its exactly-once test,
   and the decline-stops-work assertions survive through renderer-count
   checks, so no coverage is lost.
2. `AmuseBuildPath.Unknown` stays. It is the test seam that drives the
   `UnsupportedBuildPath` refusal. Its doc comment gains one sentence stating
   that production code never constructs it and tests use it to drive the
   refusal path.
3. None-guard symmetry. `AmuseReports.RendererRefusal` and
   `AmuseReports.TextureCaptureRefusal` gain the same None throw the other
   four refusal methods have, each with a comment naming the key that would
   otherwise form (`amuse.renderer.None`, `amuse.texture.None`). Tests cover
   the None throw for all six guarded methods, where today only two have one.
4. Missing reporting tests, RED where behavior is new, characterization where
   behavior exists: `SlotSeparationDivergence` and
   `SlotSeparationPremultiplyNormalization` helper tests;
   `amuse.consent.Subject` per-subject count and content. (`TextureAlphaMissing`
   coverage lands with D2, which exercises it end to end.)

## Cross-cutting rules

- Every new string-table entry follows the existing table: English, short
  active sentences, one idea per sentence, no semicolons, no contractions.
- Every new key lands in the string-table completeness loops, so a future
  cause without strings fails tests. The new `AlphaResolutionFailure` loop
  skips `SemanticsUnknown`, because the builder throws on it.
- Every new throw guard names the key or value it protects in its comment.
- Falsifier tests follow the repository convention: numbered adversarial
  cases marked `--- Falsifier N: ... ---` with a no-op guard, each asserting
  a behavior a plausible wrong implementation must fail.
- No absolute paths, no private identifiers, no machine names in any new
  file, comment, or test.
- Test folders mirror production folders. One test class per production type,
  existing classes extended where the type already has one.
- Every behavior change lands RED first: the test fails on current code for
  the named wrong implementation, then the minimal fix turns it green. The
  characterization tests in D6 and the D1 fixture step are exempt; they pin
  current behavior and say so.
- The design maps to the investigation one to one: D1 is G1, D2 is G2, D3 is
  G3, D4 is G5, D5 is G4, D6 is G6, D7 covers the investigation's residual
  smells and remaining test holes.

## Acceptance criteria

1. All six gaps have an observed RED test before their fix and an observed
   GREEN run after, with counts recorded.
2. The full `Alrauna.Amuse.Tests.Editor` assembly passes after the last task,
   with the run's observed counts recorded.
3. No existing test weakened or deleted except the `ConsentDeclined` flag
   assertions, whose replacement coverage is named above.
4. The investigation's gap list and this design stay consistent: any deviation
   found during execution is recorded as a ruling in the execution ledger.
