# Full Repo Audit Remediation — Design

Date: 2026-10-15

Base: `f929332` on branch `audit/full-repo-audit-2026-10-15`

Investigation: `docs/superpowers/investigations/2026-10-15-full-repo-audit.md`

Revision note: first issue 2026-10-15. Three parallel adversarial reviews the same day (accuracy against the investigation; safety, security, and latent bugs; architectural purity and ponytail adherence) rejected the first issue with two blockers and required fixes. Blocker one: the finding-22 rule rested on a falsified deserializer premise, which the review measured in the pinned editor. Blocker two: the finding-5 cut could not satisfy its own acceptance grep, because Host data types saturate the two Analysis files' signatures. This second issue restates both, adds the missing decisions for findings 7 and 32, and folds in every confirmed fix. The decision-to-finding tally at the end closes the mapping.

## Overview

The investigation reports 37 findings on 2026-10-15 source. This design selects the remediation scope and sets the rules every fix must obey. The owner selected the scope on 2026-10-15: every actionable finding. Per decision 1, that executes 32 findings, records finding 4 as deferred and findings 24 and 29 as no-change decisions, and puts findings 36 and 37 out of scope.

The round mixes three contracts, and each finding carries exactly one:

1. **Conservative behavior fixes** — findings 1, 2, 3, and 14. Every change must move outcomes toward refusal or Unknown, or make a documented consent-gated capability reachable. None may move a triangle toward conversion that the old code left alone, except finding 14, which makes a documented unlock reachable.
2. **Outcome-identity performance work** — findings 8, 9, 10, 11, 12, 30, 31, 32, and 33. Classification outcomes, refusal values, and report strings stay identical. Capture results are defined at the consumer boundary; invariant 2 states the one finding-owned exception.
3. **Reporting, contract, and hygiene fixes** — findings 5, 6, 7, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 25, 26, 27, 28, 34, and 35. They change what users read, what the code promises, or what tests pin, without changing proof outcomes.

Findings 4, 24, 29, and the dry-run half of 6 get recorded decisions with no code change.

## Decisions

These decisions close the investigation open points. The finding numbers are normative.

1. **Scope.** The round executes findings 1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 25, 26, 27, 28, 30, 31, 32, 33, 34, 35. Finding 4 is recorded as deferred (decision 2). Findings 24 and 29 are recorded as no-change decisions (decisions 3 and 4). Findings 36 and 37 are out of scope (research only). Any finding whose plan-time verification fails is dropped with the drop recorded, not forced.
2. **Finding 4: deferred by owner decision.** The owner chose deferral on 2026-10-15. The fix depends on an unverified external fact: whether NDMF can run another plugin's pass between the barrier and apply. Designing revalidation blind risks wasted cost or false security. This deferral is unblocked by a verified answer to that ordering question.
3. **Finding 24: no change.** The catch sites declare the mapping intentional and vendor-mirrored, and the request lists are generated from the consulted arrays, so the drift trap needs an edit to the shared source to fire. The investigation already records the trap. Code stays as written on 2026-10-15.
4. **Finding 29: no change.** The non-merge is documented and test-pinned as deliberate. Revisiting it belongs to a round that weighs the cost against the proof core's explicitness. Out of scope.
5. **Finding 2: add the IDMask gates.** The owner chose gates on 2026-10-15. The opaque frontend's `AlphaCoverageGates` gains the same IDMask entries its cutout and transparent siblings carry. A material with IDMask runtime values set answers Unknown instead of a constant opaque claim. Direction: conservative. Cost: possible over-refusal for IDMask materials, which is a coverage defect, not a visual one. The LIL_RENDER == 0 compile-out premise stays unverified per the investigation's finding 2 body; the gates make that premise unnecessary for safety.
6. **Finding 6: amend the contract, keep the route.** The owner chose amendment on 2026-10-15. The capture contract text at `UnityAlphaFieldEvidence.cs:21-22` names the streaming-clone route as a sanctioned exception, in the same style as the Semantics conversion recipes. The route gains crash-resilient cleanup: at capture start, leftover files in the pinned temp folder that match the clone naming pattern the route itself creates are deleted before a new clone is made. The cleanup never deletes content outside that pattern, so the route can never remove assets it does not own. The dry-run's refusal of this route stays as observed behavior and is recorded as a known preview limitation, not fixed here.
7. **Finding 5: cut the Host call direction, accept the data-type references.** The investigation's violation is that the proof core triggers live capture and calls Host orchestration. The cut, stated precisely: `Editor/Analysis/` ends with zero qualified calls into Host types, while the Host *record types* in Analysis signatures (`CapturedAnimationEvidence`, `CapturedFloatBinding`, `BlockStateEntry`, `AlphaFieldSet`, `UnityRendererAlphaSnapshot`, `RendererAnalysisRefusal`, and siblings) stay Host-namespaced and accepted. Concretely: the one Unity-touching call, `GatherAlphaFields`, hoists to the Build and DryRun orchestrators, which already hold the result's consumer boundary (`AlphaFieldSet` is already a `ResolveSlot` parameter); the pure Host members Analysis calls (`StructuralRefusalFor`, `ResolveProofRelevant` per binding with its out parameter, `IsUnrecognizedObjectMaterialBinding`, `Classify`, `IntersectOutcomes`) become delegates injected at their current statement positions by the two production injectors, Build and DryRun. Ordering requirements: the binding loops and first-unrecognized early refusal stay in Analysis; `GatherAlphaFields` injects eagerly at the same pre-slot-loop statement; each classify delegate allocates once per resolve call, never per submesh or per mip. Finding 5 lands before finding 8, or the delegate shape accepts precomputed invariants, so the two never rewrite each other's seam. The pure `TextureScaleOffsetSuffix` constant threads through as a value. No new production type, no assembly, no namespace relocation.
8. **Finding 1: exact-float cache key with route provenance.** The streaming cache key uses the exact cutoff float, matching the generated route and the shared field set. The quantized bucket disappears. The key also records the disk-fallback setting in force, so a request that disallows the fallback is never served evidence of fallback provenance: two chains that differ only by capture route are different cache entries. Clamping happens before keying, mirroring the generated route.
9. **Finding 3: conservative block test, proof scoped to the final store.** The source-image block rule becomes conservative for the byte the imported chain stores at the compared level. Erase requires the block average rounded up to still sit below the noise bound. Opaque witness requires the average rounded down to still reach the opaque bound. Blocks inside the rounding window resolve as mid, never erased and never an opaque witness. Integer arithmetic only; no float compares. Premise recorded as unverified: the imported chain's levels above the first are generated recursively by the importer, so its bytes can carry accumulated intermediate rounding outside the single-step band this rule covers. The plan may not claim multi-level identity, only final-store conservativeness.
10. **Findings 15, 16, 35: user-visible sentences and numbers change deliberately.** The play-mode rows describe the plan apply executes, with the folded numbers. Moved-triangle counts count changed slots only. The "of out" copy defect becomes "out of", and the test pins move with the text, recorded as a deliberate pin correction, never as a silent edit. The refusal-row mechanism of finding 26 is decision 22's; this decision owns only the folded numbers and the copy fix.
11. **Finding 22: corrupt data reads as no record; defects throw.** Measured in the pinned editor on 2026-10-15: `JsonUtility.FromJson` throws `ArgumentException` on malformed JSON, junk suffixes, array payloads, and the `"null"` literal, throws `NullReferenceException` on empty input, and returns a default record only for wrong-typed fields, which the existing fingerprint check already rejects. The rule follows the measured facts: parse-level failures (`ArgumentException`, plus the empty-string guard) are corrupt data; the loader erases the stored string and returns no record. Every other exception propagates as a defect. The RED corrupt-payload test lands before the split is chosen, and the plan records the parser's observed behavior so the split rests on pinned facts.
12. **Finding 25: typed signal first, partial remediation allowed.** The plan verifies whether NDMF 1.14.4 exposes a typed activity signal for the animator services extension. If it does, the message-text catch is replaced. If it does not, the finding is partially remediated: the catch stays, the exact-match coupling is documented at the site, and the drop is recorded. Either arm satisfies acceptance 1 for this finding.
13. **Finding 33: verify, then replace the spin; partial remediation allowed.** The plan verifies whether the Unity 2022.3 package request API offers a synchronous completion call. If it does, the sleep loop becomes it. If not, the loop stays with its bound, the comment is corrected to state the real reason, and the finding is partially remediated. The per-build cache clears stay: they are attestation safety against mid-session package swaps.
14. **Finding 9: cache and cap before readback, provenance in the key.** The GPU route gains a session cache keyed on instance identity, channel, threshold, bounds, active mipmap limit, update count, plus the raw user mip cap. The generated route's contents-hash element is dropped: the GPU route has no CPU-side pixels to hash without paying a readback, which would defeat the cache on every hit. This deviation is measured and recorded in the plan review of 2026-10-15. Capture reads back only levels at or below the raw policy cap. The cap reaches the capture entry through the shared request shape of decision 22. The cache joins both existing per-build clear sites as memory hygiene and route parity; the update count in the key carries correctness. Capture results are defined at the post-`LimitedTo` consumer boundary for this finding (invariant 2).
15. **Finding 10: memo the include-tree gather.** The lilToon gather result is memoized per shader asset identity and include-folder write times, mirroring the corrected Poiyomi memo shape (identity plus write times plus per-build clear), so finding 27's mtime-only trap is not reintroduced for lilToon. The memo joins the per-build clear list. Hash results stay identical because the inputs are identical.
16. **Findings 8, 11, 12, 31: hoist and pre-filter, never re-derive.** Triangle-invariant checks run once per triangle and thread through the mip loop. The point paths reuse the bilinear paths' conservative double-precision pre-filter. The density pre-pass merges into the main walk with a fallback only when substitution could flip a verdict; a single pass may not change which texels count as witnesses when substitution flips mid-walk. The affine mapping decodes once per resolution. All are outcome-identity; any outcome diff is a stop condition.
17. **Finding 19: the trap dies.** The unreachable fallback becomes a thrown invariant failure with a named message, matching the repo rule that invariant failures are defects.
18. **Finding 20: stop advertising TGA.** The source-image reader admits PNG only. The plan first verifies that the unadmitted path routes TGA to the readable-clone route, the behavior as observed on 2026-10-15, before claiming preservation; a comment states why.
19. **Finding 21: named refusal for malformed controllers.** A null layer state machine produces a new closed refusal value on `AvatarAnimationRefusal`, the enum the graph walk's result already carries, not on the renderer-analysis enum, and not an exception. The new member gains its report-table entries (`AmuseReportStrings.AvatarKey` builds keys from the member name) and its play-mode row behavior in the same task, using the existing corrupt-key-guard idiom where the row arm reads the table.
20. **Finding 23: one meaning for the flag.** Every arm of `IsAttestedAlphaMaterial` treats `verifyIdentity == false` as skip verification. Both current callers pass `true`, so behavior is unchanged as observed on 2026-10-15.
21. **Findings 13, 34: delete tests without a behavior sentence.** The rendering test duplicates a pin that lives in the right suite under the right name, and the signature pin asserts no behavior. Both die. No replacement tests.
22. **Findings 17, 18, 26, 27, 28, 30: smallest honest contract-3 fixes.** 17: apply reads the build target through the same seam the barrier uses, and the seam doc names both consumers. 18: the cancellation source is disposed by a continuation on the in-flight task when cancellation beat it. 26: play-mode slot refusal rows carry the named cause through the existing per-cause renderer table entries, the fixed generic sentence remains only as the guarded fallback, and the duplicated apply-side builders with the copy defect die. 27: the Poiyomi memo joins the per-build clear list. 28: the static disk-fallback switch is extracted at the `UnityMaterialEvidenceCapture` request boundary and threads through `UnityAlphaFieldEvidence.TryCapture` and `UnityStreamingTextureEvidence.TryCapture` to the existing gate, with a default that keeps behavior as observed on 2026-10-15; it composes with decision 8's key provenance and decision 14's request shape. 30: the parsed play record is cached in the editor keyed on the raw session record string, so any new build's record invalidates the parse; the fingerprint alone cannot, because it does not change between two builds under one policy.
23. **Finding 7: closed refusal value at the preparation boundary.** `AmusePreparationDecision` carries a closed refusal enum instead of a bare string. The apply caller passes the enum value it already holds instead of its `ToString()`. Human-readable detail stays available at the report edge as display text; decision equality and any consumer switching key on the enum.
24. **Finding 32: assert once per chain, not per renderer.** The red-field contract assertion moves to the point where a red chain is produced or cache-filled, so each unique chain is validated once per capture call instead of once per renderer. The assertion's semantics stay identical: a violation throws.

## Invariants

1. Conservative direction. Fixes in contract 1 never turn a refused or Unknown triangle into a converted one. Finding 14 is the single stated exception, and it only widens window eligibility; every swap still passes the swap-in's own identity and attestation re-checks, and the version-consent dialog still gates its subjects.
2. Outcome identity for contract 2, defined at the consumer boundary. Classification outcomes, refusal values, and report strings stay identical. Capture results are identical at the post-`LimitedTo` consumer boundary; finding 9 owns the one evidence-boundary change (levels above the cap become placeholders before readback). The ReferenceFixtures matrix and the existing suites are the oracle. Because the investigation records the fixture oracles as unaudited, the plan samples the fixture matrix for the touched outcome classes before relying on it. Any diff is a stop condition.
3. Attestation stays pinned data. No GUID, digest, version range, or attested name changes. Memoization may not change any hash result. A diff that touches a pinned value is a stop condition.
4. The proof core keeps its shape. Decision 7 adds injected delegates at the stated positions and moves no type, no namespace, and no assembly. The residual Host record-type references in Analysis are accepted and named in the decision.
5. No new dependencies. No package, no asset, no tooling joins the project.
6. Test deletions follow decision 21 only. A valid test that fails after a behavior change is updated to pin the new intended behavior with a recorded RED first, never deleted for failing.
7. No private data, no machine path, no identifier enters any file. The identifier sweep runs on every changed file before its commit.
8. Runtime sampling assumptions stay as declared. Fixes must not assume a readable texture guarantees correct runtime sampling; mipmaps, compression, filtering, wrapping, and color space caveats stand.

## Test strategy

Unity Test Runner, EditMode, through the editor instance pinned to this project. Validation order per touched suite: Unity refresh after new files, a focused filter per changed suite, then the full `Alrauna.Amuse.Tests.Editor` assembly. The research assembly runs only if research sources changed, which this scope does not do.

RED/GREEN applies to every contract-1 fix: findings 1, 2, 3, and 14 each observe a failing test that names the defect first, then the minimal fix, then the same test green. The finding-22 split lands its corrupt-payload RED test before the catch is narrowed. Falsifier-style guards carry no-op protection so later changes cannot silence them.

Contract-2 work proves outcome identity by running the Analysis and Host suites plus fixture-driven cases unchanged, after the fixture sampling of invariant 2. A cheap proxy count (capture calls, hash calls) may be asserted where a seam already exposes one; no new benchmark rig is built.

Reporting fixes update their sentence pins in the same task and record the pin movement in the task result.

A filtered run that reports zero tests is a failure. Observed counts are recorded at every run.

## Acceptance criteria

1. Every finding in decision 1 is executed or its plan-time verification failed with the drop recorded, including the partial-remediation arms of decisions 12 and 13. The executed list matches decision 1 by number and the tally below.
2. Findings 2, 4, 24, 29, and the dry-run half of 6 exist in the record as decisions with their rationale, dated 2026-10-15.
3. Every contract-1 fix has a RED observation recorded before its green, and finding 22's split rests on the recorded parser measurements.
4. Every contract-2 change runs the Analysis, Host, and fixture-sampled suites with zero outcome diffs at the stated boundaries, observed counts recorded.
5. The call-direction half of the finding-5 cycle is gone: a repository-wide grep finds no qualified call into a Host type under `Editor/Analysis/`, the injected-boundary list is stated in the record, and the accepted residual Host record-type references are named.
6. The play-mode surfaces report folded numbers, per-cause refusals keyed through the shared table, and "out of" copy, with moved pins listed.
7. No pinned attestation value changed. The diffs of the attestation files show moved lines, not changed constants.
8. Every changed file passes the identifier sweep before its commit.
9. The working tree holds only this round's changes at the end.

## Validation and stop conditions

The plan runs the test strategy in the stated order and records observed counts.

Stop and return evidence when:

- any classification outcome, refusal value, or report string changes outside the findings that own that change,
- any capture result changes at the post-`LimitedTo` consumer boundary outside finding 9's owned change,
- any conservative-direction fix makes a previously refused or Unknown case convert,
- any pinned attestation value would change,
- a plan-time verification of decisions 12, 13, or 18 fails in a way that blocks the finding,
- a fix needs a new abstraction or dependency to work,
- a valid test fails and the reason is not a finding-owned behavior change,
- the working tree holds an unexpected mutation.

## Authorization boundary

Dated owner authorizations, all 2026-10-15: the audit branch, the investigation-to-plan pipeline, and commits between finalized changes. Subagent execution is authorized for this round. No push, no pull request, no tag, no release, no publication is authorized. Stage only the paths a plan task names. Never stage generated Unity state or unrelated user-owned changes.

## Out of scope

Finding 4 code changes (deferred, decision 2). Findings 24 and 29 code changes (no-change decisions 3 and 4). Findings 36 and 37 (research only). The dry-run route mismatch of finding 6. Namespace or assembly relocation of Host record types (rejected by decision 7). Any refactor not owned by a listed finding. Any new capability, API, configuration, or dependency. Any change to the Census Lab project, Unity assets, CI workflows, or the bootstrap script.

## Decision-to-finding tally

| Finding | Decision | Contract | Disposition |
|---------|----------|----------|-------------|
| 1 | 8 | 1 | exact-float key + route provenance |
| 2 | 5 | 1 | IDMask gates |
| 3 | 9 | 1 | conservative block rule |
| 4 | 2 | — | deferred, owner decision |
| 5 | 7 | 3 | call-direction cut, residual types accepted |
| 6 | 6 | 3 | contract amendment + owned-pattern cleanup |
| 7 | 23 | 3 | closed refusal enum |
| 8 | 16 | 2 | hoist triangle invariants |
| 9 | 14 | 2 | cache + cap before readback |
| 10 | 15 | 2 | include-tree memo |
| 11 | 16 | 2 | point-path pre-filter |
| 12 | 16 | 2 | merge density pre-pass |
| 13 | 21 | 3 | delete misleading test |
| 14 | invariant 1, decision 22 note | 1 | widen eligibility to closure |
| 15 | 10 | 3 | folded-plan rows |
| 16 | 10 | 3 | changed-slot moved counts |
| 17 | 22 | 3 | shared target seam |
| 18 | 22 | 3 | continuation dispose |
| 19 | 17 | 3 | throw on unreachable fallback |
| 20 | 18 | 3 | PNG-only admission, verified routing |
| 21 | 19 | 3 | AvatarAnimationRefusal value + table entries |
| 22 | 11 | 3 | measured corrupt-data split |
| 23 | 20 | 3 | uniform flag meaning |
| 24 | 3 | — | no change, documented policy |
| 25 | 12 | 3 | typed signal or documented coupling |
| 26 | 22 | 3 | per-cause rows, builders die |
| 27 | 22 | 3 | memo joins clear list |
| 28 | 22, with 8 and 14 | 3 | request-bounded switch |
| 29 | 4 | — | no change, documented trade-off |
| 30 | 22 | 2 | raw-record parse cache |
| 31 | 16 | 2 | hoist mapping decode |
| 32 | 24 | 2 | assert once per chain |
| 33 | 13 | 2 | sync API or corrected comment |
| 34 | 21 | 3 | delete signature pin |
| 35 | 10 | 3 | "out of" copy + pins |
| 36 | — | — | out of scope, research |
| 37 | — | — | out of scope, research |
