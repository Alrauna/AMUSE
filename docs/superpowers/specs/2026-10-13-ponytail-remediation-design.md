# Ponytail Remediation — Design

Date: 2026-10-13

Base: `93358a4` on branch `ponytail-audit`

Audit: `docs/superpowers/audits/2026-10-13-ponytail-audit.md`

Investigation: `docs/superpowers/investigations/2026-10-13-ponytail-audit-investigation.md`

Revision note: first issue 2026-10-13. Four adversarial reviews ran the same day against the first issue: accuracy against the audit and investigation, safety and security, architectural purity and ponytail adherence, and latent bugs. They found one self-contradiction on the new-type count, an unowned amendment to the finding 1 cut column, reflection-pinned wrappers inside finding 10, a test oracle inside finding 42, an ill-defined deletion check, and unnamed test readers of the finding 13 fields. This revision incorporates every confirmed fix. The finding 10 rescope and the decision 6 grep rule are the load-bearing changes. A controller inventory check the same day restored audit row 56, which the audit integration pass had dropped, and corrected the scope arithmetic to 54 of 56 findings.

## Overview

The audit found 55 over-engineering findings and estimates a net cut of 3775 lines `[ESTIMATE]`. This design selects the remediation scope and sets the rules every cut must obey.

The round is behavior-identity work. No user-visible change. No contract change. No new capability. No new abstraction. The product does exactly what it did before, in less code.

The scope has four parts.

1. Delete the dead production symbols and production test seams of class D1.
2. Consolidate the test duplication of class D3 into the existing `TestTransientScope`, one new shared pin, and the audit-named shared helper classes.
3. Apply the selected production shrinks of class D4.
4. Cut the dead flexibility of class D2 per decisions 2 and 3.

The investigation classes name the exact finding numbers. This design includes or excludes each class and each exceptional finding by number.

## Decisions

These decisions close the investigation open points.

1. **Scope.** The round executes 54 of the 56 findings: all of D1, all of D3, nine of the ten D4 findings, and both D2 findings. It excludes finding 7 (the research provider seam) per decision 4 and finding 9 (the erased-texel walk parameterization) per decision 5. The selected finding list is normative. The plan carries it verbatim, and the final owner review checks the plan against it.
2. **Finding 1: cut the three non-alpha interpret pipelines, shape-preserving.** The facts are verified: production builds the base color, emission, and normal outputs as constant Unknown and never reads them (investigation section 2.1). The cut removes the interpret code, its private helpers, and the diagnostics vocabulary members that only those interpreters produce, in both shader frontends. Tests whose subject is removed go with the code. The cut preserves: the `MaterialSemantics` record with its four output slots, the `ColorSemanticValue`, `NormalSemanticValue`, and `TextureSample` records, and every frontend identity attestation and `IsSupportedMaterial` verdict that the research census consumes. `TextureSample` must survive regardless, because the alpha path's `ScalarSemanticValue` consumes it. Decision 2 narrows the audit's finding 1 cut column: despite the verbatim rule of decision 1, the plan must not delete the four slots or the value records. The residue is owned here: after the cut, `ColorSemanticValue` and `NormalSemanticValue` have zero producers and roughly 200 lines of dead vocabulary remain. They stay as the blessed shape the vision describes, and the first real consumer reintroduces their behavior. The vision document blesses the four-output vocabulary as direction. Direction does not require dead equations. The line estimate for this finding drops from 1400 to roughly 900 `[ESTIMATE]`, refined at plan time.
3. **Finding 13: cut the eight row data fields.** The drawer reads only `Kind` and `Sentence` (investigation section 2.3). The fields and their sentinel plumbing are flexibility with no reader. Two test files read the fields. `MaterialTransformationRowTests` pins every sentinel value, and its sentinel pins are the deleted fields' subject, so invariant 7 removes them with the fields. `AlphaSeparationDryRunTests.ProvenTriangleDisplaysAsWouldSplit` asserts `SlotIndex`, `ProvenTriangles`, and `TotalTriangles` beside its sentence pin, and its subject survives the cut. That test rewrites to sentence-based assertion only: the sentence already encodes the slot index and the proven and total counts, so the field assertions are redundant. The cache and fingerprint code of findings 46 and 49 is independent of the row field shape.
4. **Finding 7: excluded.** The `ResearchMaterialSemanticsProvider` seam is documented deliberate: it lets the census count `ProvenOpaque` and `MissingTextureEvidence` refusals in CI without vendor shaders (investigation section 2.4). Removing it requires a research migration with census coverage risk. That risk has no place in a behavior-identity round. The seam stays on record as a candidate for the next research change that touches it.
5. **Finding 9: excluded. Finding 4: included.** The distinction is delegation versus new abstraction. Finding 4 replaces each `HasMappedWitness*` body with a one-line predicate that delegates to the very `Classify*` walk the classifier matrix already exercises. Reuse of proven code with loud failure modes. Finding 9 would parameterize the erased-texel pre-passes on an interval-function value, which inserts an indirection into the proof core. The house treats proof-grade explicitness as a feature. Finding 9 stays on record as a candidate if the owner weighs the 60 lines over the explicitness.
6. **Verification before cut.** Findings 10, 15, 18, 28, 30, and 49 are medium confidence in the audit or carry its verify-first caveat. The plan verifies each claim with caller greps before its task exists. A verification that fails drops the finding and records the drop. The greps match deleted names as bare tokens across all three source roots, because reflection call sites invoke by string literal and a qualified-symbol grep misses them. The medium-confidence D3 findings 21, 36, 38, and 39 verify in-task instead: their claims are byte-equality and duplication facts, a drifted literal surfaces as a failing consolidation, and the failing consolidation drops the finding with the drop recorded.
7. **Equivalence contract.** Every change in this round is behavior-identical. The observable contract: every existing test outcome stays as it is, except tests whose subject a selected finding deletes. No NDMF report string changes. No public API changes. No serialized field changes. No classification outcome changes. No refusal value changes. No report changes. A diff that shows any of these is a stop condition.
8. **Shared helper surface.** New test types exist only where the audit names them: `AnisotropicFilteringPin`, an `IDisposable` that saves the filter mode on construction and restores it on disposal, under `Tests/Editor/Shared/` (finding 6), the NDMF apply-lifecycle helper class under `Tests/Editor/Build/` (finding 2), and internal static helpers beside their suites for findings 17 and 24. No other new test type. No new production type. The tracked-object consolidation routes to the existing `TestTransientScope`. That scope gains one optional constructor flag, `allowDestroyingAssets`, defaulting to false, so the four suites that pass the destroy-assets flag keep their semantics and every other suite keeps its own. Findings 10 and 28 route callers to logic that already exists. Finding 18 extracts one private static helper inside `AlphaSeparationPreparation`, shaped like the existing `TransientUnlockSwapIn.RecordRefusal`, with no cross-class seam and no visibility change to that method.
9. **The dry run display chain.** Findings 13, 33, 46, and 49 land in one sequence: row shape first (13 and 50), then the property seam (33), then the fingerprint wrapper (46), then the cache slot (49). The avatar-sweep cache tests must pass unchanged through every step.
10. **Attestation safety.** Finding 15 deduplicates the gather front half of `LilToonSourceAttestation.cs`. It keeps both entry points, their fail-closed omissions, and every pinned digest. No digest is re-derived. A diff that touches a pinned value is a stop condition.
11. **No documentation round.** The README, the report strings, and the presets describe user-visible behavior. This round changes none. No document changes.
12. **Simple English and record rules.** The run binds every new sentence they bind. This round writes no user-facing sentence.
13. **Finding 10: rescoped to four of the seven wrappers.** `OpaquePercentFrom`, `PolygonClampPercentFrom`, and `DensityCapFromCoveragePercent` are pinned by reflection: tests resolve them by string name on the pass type and assert they exist. Those pins guard the wiring-time policy rejection, and they are falsifier-style guards on kept surface, so invariant 7 keeps them and the three wrappers stay. The round cuts the four wrappers without reflection callers: `ProofMipCapFrom`, `MinTextureSizeFrom`, `MinimumCoverageFrom`, and `MinimumPreserveCoverageFrom`. The finding 10 estimate drops from 60 to about 30 lines `[ESTIMATE]`.

## Invariants

1. No behavior change. The equivalence contract of decision 7 is the definition.
2. No new production type. No new production abstraction. New test types follow decision 8's list, and nothing else is new.
3. The exact classifier's outcomes are untouched. Finding 4 delegates to the same walks. Findings 41, 42, and 47 delete overloads and seams with no production caller. Finding 42's equivalence test keeps a local `BigInteger` floor-mod reference as its oracle, the same keep-a-local-copy rule finding 41 states for `HalfUlp`.
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
2. Every existing test outcome is unchanged, except the suites whose subject a selected finding removed and the sentence-based rewrite decision 3 sanctions. Observed counts for the full assemblies are recorded, with the expected delta explained.
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

Finding 7 and finding 9, per decisions 4 and 5. Any refactor not listed in the audit. Any performance work. Any change to the vision conversation about the semantics vocabulary. Any new capability, API, or configuration. Any change to the Census Lab project or to Unity assets.
