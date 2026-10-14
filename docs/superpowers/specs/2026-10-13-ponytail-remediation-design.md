# Ponytail Remediation — Design

Date: 2026-10-13

Base: `93358a4` on branch `ponytail-audit`

Audit: `docs/superpowers/audits/2026-10-13-ponytail-audit.md`

Investigation: `docs/superpowers/investigations/2026-10-13-ponytail-audit-investigation.md`

Revision note: first issue 2026-10-13. Four adversarial reviews ran the same day against the first issue: accuracy against the audit and investigation, safety and security, architectural purity and ponytail adherence, and latent bugs. They found one self-contradiction on the new-type count, an unowned amendment to the finding 1 cut column, reflection-pinned wrappers inside finding 10, a test oracle inside finding 42, an ill-defined deletion check, and unnamed test readers of the finding 13 fields. This revision incorporates every confirmed fix. The finding 10 rescope and the decision 6 grep rule are the load-bearing changes. A controller inventory check the same day restored audit row 56, which the audit integration pass had dropped, and corrected the scope arithmetic to 54 of 56 findings. The plan review found decision 8's finding 18 clause impossible as written, because a private helper in one class cannot serve the eleven two-step sites that span three classes. Decision 8 now places that helper as internal on the existing `AmuseReports` type. In the owner's plan review the same day, the owner declined findings 1, 4, and 13. Decisions 2, 3, and 5 are now exclusion records, the scope is 51 of 56 findings, and the display chain of decision 9 carries finding 50 without the row fields. Four parallel scouts then audited the remaining 51 findings for future-work debt, per the owner's request. All 51 returned keep. Their adjustments are folded into the plan: the finding 11 positional-argument call sites, the finding 45 stale doc comment, the finding 51 prose-comment collision, and execution notes for findings 10, 41, 42, and 50.

## Overview

The audit found 55 over-engineering findings and estimates a net cut of 3775 lines `[ESTIMATE]`. This design selects the remediation scope and sets the rules every cut must obey.

The round is behavior-identity work. No user-visible change. No contract change. No new capability. No new abstraction. The product does exactly what it did before, in less code.

The scope has three parts.

1. Delete the dead production symbols and production test seams of class D1.
2. Consolidate the test duplication of class D3 into the existing `TestTransientScope`, one new shared pin, and the audit-named shared helper classes.
3. Apply the selected production shrinks of class D4.

The investigation classes name the exact finding numbers. This design includes or excludes each class and each exceptional finding by number.

## Decisions

These decisions close the investigation open points.

1. **Scope.** The round executes 51 of the 56 findings: all of D1, all of D3, and eight of the ten D4 findings. It excludes findings 1, 4, and 13 per the owner's decision of 2026-10-13, and findings 7 and 9 per decisions 4 and 5. The selected finding list is normative. The plan carries it verbatim, and the final owner review checks the plan against it.
2. **Finding 1: excluded by owner decision.** The owner declined the cut on 2026-10-13 while reviewing this design. The facts stand: production builds the base color, emission, and normal outputs as constant Unknown and never reads them (investigation section 2.1). The three interpret pipelines, their helpers, and the value records stay in the tree. The audit's 1400-line estimate stays unclaimed. The shape-preserving analysis this decision once carried is on record for any future round that revisits the question: preserve the `MaterialSemantics` slots, the `ColorSemanticValue`, `NormalSemanticValue`, and `TextureSample` records, and every identity attestation and `IsSupportedMaterial` verdict the census consumes.
3. **Finding 13: excluded by owner decision.** The owner declined the row-field cut on 2026-10-13, on the recency argument: the display feature is days old and the fields are its designed surface. The eight fields, their sentinels, and the widened builders stay. The drawer still reads only `Kind` and `Sentence`. Finding 50, the unused `Disclosure` kind, is a separate audit finding and stays in scope; it dies with no emitter and no drawer case, and the feature's own plan states disclosure rows are the editor's labels, not row kinds.
4. **Finding 7: excluded.** The `ResearchMaterialSemanticsProvider` seam is documented deliberate: it lets the census count `ProvenOpaque` and `MissingTextureEvidence` refusals in CI without vendor shaders (investigation section 2.4). Removing it requires a research migration with census coverage risk. That risk has no place in a behavior-identity round. The seam stays on record as a candidate for the next research change that touches it.
5. **Findings 4 and 9: excluded.** The owner declined finding 4 on 2026-10-13, joining finding 9, which this design already declined. The witness-walk delegation would have saved 230 lines by reusing proven code, and the audit facts for it stand. The owner's call keeps the proof core byte-explicit this round: every walk stays written out. Finding 9 stays declined for the stated reason: parameterizing the erased-texel pre-passes on an interval-function value inserts an indirection into the proof core. Both stay on record as candidates if a later round weighs the lines over the explicitness.
6. **Verification before cut.** Findings 10, 15, 18, 28, 30, and 49 are medium confidence in the audit or carry its verify-first caveat. The plan verifies each claim with caller greps before its task exists. A verification that fails drops the finding and records the drop. The greps match deleted names as bare tokens across all three source roots, because reflection call sites invoke by string literal and a qualified-symbol grep misses them. The medium-confidence D3 findings 21, 36, 38, and 39 verify in-task instead: their claims are byte-equality and duplication facts, a drifted literal surfaces as a failing consolidation, and the failing consolidation drops the finding with the drop recorded.
7. **Equivalence contract.** Every change in this round is behavior-identical. The observable contract: every existing test outcome stays as it is, except tests whose subject a selected finding deletes. No NDMF report string changes. No public API changes. No serialized field changes. No classification outcome changes. No refusal value changes. No report changes. A diff that shows any of these is a stop condition.
8. **Shared helper surface.** New test types exist only where the audit names them: `AnisotropicFilteringPin`, an `IDisposable` that saves the filter mode on construction and restores it on disposal, under `Tests/Editor/Shared/` (finding 6), the NDMF apply-lifecycle helper class under `Tests/Editor/Build/` (finding 2), and internal static helpers beside their suites for findings 17 and 24. No other new test type. No new production type. The tracked-object consolidation routes to the existing `TestTransientScope`. That scope gains one optional constructor flag, `allowDestroyingAssets`, defaulting to false, so the four suites that pass the destroy-assets flag keep their semantics and every other suite keeps its own. Findings 10 and 28 route callers to logic that already exists. Finding 18 adds one internal static helper on `AmuseReports`, which already owns `SlotSeparationRefusal`, shaped like the existing `TransientUnlockSwapIn.RecordRefusal`. The eleven two-step sites span three classes, so the helper must be internal to serve them, and `AmuseReports` is an existing type, so no new type appears. The helper changes no visibility of `TransientUnlockSwapIn.RecordRefusal`.
9. **The dry run display chain.** Findings 50, 33, 46, and 49 land in one sequence: the unused disclosure kind (50), then the property seam (33), then the fingerprint wrapper (46), then the cache slot (49). The eight row data fields stay exactly as they are, per decision 3. The avatar-sweep cache tests must pass unchanged through every step.
10. **Attestation safety.** Finding 15 deduplicates the gather front half of `LilToonSourceAttestation.cs`. It keeps both entry points, their fail-closed omissions, and every pinned digest. No digest is re-derived. A diff that touches a pinned value is a stop condition.
11. **No documentation round.** The README, the report strings, and the presets describe user-visible behavior. This round changes none. No document changes.
12. **Simple English and record rules.** The run binds every new sentence they bind. This round writes no user-facing sentence.
13. **Finding 10: rescoped to four of the seven wrappers.** `OpaquePercentFrom`, `PolygonClampPercentFrom`, and `DensityCapFromCoveragePercent` are pinned by reflection: tests resolve them by string name on the pass type and assert they exist. Those pins guard the wiring-time policy rejection, and they are falsifier-style guards on kept surface, so invariant 7 keeps them and the three wrappers stay. The round cuts the four wrappers without reflection callers: `ProofMipCapFrom`, `MinTextureSizeFrom`, `MinimumCoverageFrom`, and `MinimumPreserveCoverageFrom`. The finding 10 estimate drops from 60 to about 30 lines `[ESTIMATE]`.

## Invariants

1. No behavior change. The equivalence contract of decision 7 is the definition.
2. No new production type. No new production abstraction. New test types follow decision 8's list, and nothing else is new.
3. The exact classifier's outcomes are untouched. Every witness walk stays written out, per decision 5. Findings 41, 42, and 47 delete overloads and seams with no production caller. Finding 42's equivalence test keeps a local `BigInteger` floor-mod reference as its oracle, the same keep-a-local-copy rule finding 41 states for `HalfUlp`.
4. Attestation stays pinned data. No GUID, digest, version, or name in the attestation tables changes.
5. The research package changes only through finding 55, an unread property in a research test file.
6. No private data, no machine path, no identifier enters any file. The identifier sweep runs on every changed file before its commit.
7. Every deleted test deleted because its subject is deleted, never because it fails. A valid test that fails after a change is a stop condition, not a deletion candidate.

## Test strategy

No new tests. The contract does not change, so the existing suites are the whole guard.

The deletion tasks update test call sites in the same task: a D1 deletion moves test-side composition into the test assembly, and the touched suite passes unchanged.

The consolidation tasks keep every suite green across each individual step, because the drift facts of investigation section 2.5 make one big-bang merge unsafe.

Validation order: Unity refresh after new files, a focused filter per touched suite, the full `Alrauna.Amuse.Tests.Editor` assembly, then the full `Alrauna.Amuse.Research.Tests.Editor` assembly. Observed counts are recorded at every run. A filtered run that reports zero tests is a failure. Deleted suites shrink the expected total, and the plan states the expected count delta per task.

## Acceptance criteria

1. Every selected finding is executed, rescope applied, or its verification failed and the drop is recorded. The executed list matches the decision 1 list by number, with decision 13's finding 10 rescope and decision 6's recorded drops applied.
2. Every existing test outcome is unchanged, except the suites whose subject a selected finding removed. Observed counts for the full assemblies are recorded, with the expected delta explained.
3. Every deleted symbol is verifiably gone, by symbol class. Unique names grep bare repository-wide. Overload deletions verify at the declaration site by signature. Qualified-member deletions grep as qualified names.
4. The new test helper types exist at the paths decision 8 states, and no production type and no unplanned test type exists.
5. No pinned attestation value changed. The diff of `LilToonSourceAttestation.cs` shows moved lines, not changed constants.
6. The working tree holds only this round's changes plus the user-owned untracked temp meta files.
7. Every changed file passes the identifier sweep before its commit.

## Validation and stop conditions

The plan runs the validation of the test strategy section in the stated order and records observed counts.

Stop and return evidence when:

- any test outcome changes for a reason other than a deleted subject,
- any classification outcome, refusal value, or report string changes,
- any pinned attestation value would change,
- a verification of decision 6 fails,
- a cut needs a new abstraction to work,
- the working tree holds an unexpected mutation.

## Authorization boundary

Dated owner authorizations, all 2026-10-13: the `ponytail-audit` branch and commits between finalized changes. The full run order: audit, investigation, review, spec, review, plan, review, stop for owner review. Implementation starts only after the owner reviews the finalized plan. No push, no pull request, no tag, no release, no publication is authorized. Stage only the paths a plan task names. The untracked `Assets/AmuseTests_Temp N.meta` files are user owned. Never stage them.

## Out of scope

Findings 1, 4, and 13, declined by the owner on 2026-10-13. Findings 7 and 9, per decisions 4 and 5. Any refactor not listed in the audit. Any performance work. Any change to the vision conversation about the semantics vocabulary. Any new capability, API, or configuration. Any change to the Census Lab project or to Unity assets.
