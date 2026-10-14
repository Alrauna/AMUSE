# Ponytail Audit Findings Investigation

Date: 2026-10-13

Base: `e6f6c7e` on branch `ponytail-audit`. The audit record `docs/superpowers/audits/2026-10-13-ponytail-audit.md` sits at that commit. The upstream base of the branch is `8a2607b` on `main`.

This document characterizes the audit findings for the design round. It changes nothing.

Evidence labels:

- `[SOURCE]` A fact read in this repository at the base commit. The citation gives a repository-relative path and line numbers.
- `[INFERENCE]` A deduction from named facts.
- `[RECOMMENDATION]` A proposal for the design round.
- `[ESTIMATE]` A line count from the audit round. It is plausible and unverified to the line.

Paths under the AMUSE package drop the `Packages/com.alrauna.amuse/` prefix. Paths under the research package name it.

## 1. Purpose

The owner set this run: parallel ponytail audit, then this investigation, then a parallel adversarial accuracy review of the investigation, then the design spec, then a parallel adversarial review of the spec, then the implementation plan, then a parallel adversarial review of the plan, then a stop for owner review. Commits land between finalized changes. The run halts when a choice needs owner input.

The audit found 55 merged findings. The audit estimates a net cut of 3775 lines `[ESTIMATE]` and zero removable package dependencies. This investigation sorts the findings into decision classes, verifies the facts that drive the big decisions, records interactions and risks, and names the open points the design round must settle.

## 2. Verification of the load-bearing facts

### 2.1 The base color, emission, and normal pipelines have no production reader

[SOURCE] The production alpha path builds `MaterialSemantics` with constant Unknown in the three non-alpha slots. `Editor/Semantics/UnityMaterialSemantics.cs:1083-1089` constructs `SemanticOutput<ColorSemanticValue>.Unknown()` for base color, the resolved alpha, `SemanticOutput<ColorSemanticValue>.Unknown()` for emission, and `SemanticOutput<NormalSemanticValue>.Unknown()` for normal. `Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs:163-168` and `Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs:208-213` follow the same shape.

[SOURCE] A repository-wide grep for `.Semantics.BaseColor`, `.Semantics.Emission`, and `.Semantics.Normal` finds every reader inside test files: `Tests/Editor/Semantics/Characterization/`, `Tests/Editor/Semantics/LilToon/`, `Tests/Editor/Semantics/Poiyomi/`, and `Tests/Editor/Semantics/`. No file under `Editor/Build/`, `Editor/Host/`, `Editor/Analysis/`, or the research Editor sources reads them. The research census `Packages/com.alrauna.amuse.research/Editor/Collection/CensusShaderFamily.cs:57-66` calls `AnalyzeBaseMaterial` on both frontends and reads only `IsSupportedMaterial`.

[INFERENCE] A cut of the three pipelines must preserve the identity verification and the `IsSupportedMaterial` verdicts that the census consumes. The interpret code for the three outputs is the cut surface. The shader identity attestation is not.

[SOURCE] `docs/architecture/vision.md:48-50` states the long-term intent: "The current MaterialSemantics describes bounded forms of base color, alpha, emission, and normals." The 2026-08-22 audit finding F8 reaches the same conclusion for its date.

[INFERENCE] The three pipelines are production-dead at this date, and the vision document blesses the vocabulary as direction. Both facts are true. The conflict is an owner decision about scope and direction, not a defect in either record. The cut estimate of 1400 lines is an `[ESTIMATE]`.

### 2.2 The proof core witnesses and walks duplicate by construction

[SOURCE] `Editor/Analysis/TriangleAlphaClassifier.cs:469-518` shows `ClassifyMapped` dispatching to `HasMappedWitnessPointClamp`, `HasMappedWitnessPointRepeat`, `HasMappedWitnessBilinearClamp`, and `HasMappedWitnessBilinearRepeat`. Trilinear maps onto the bilinear walks. The audit read all four walks and the matching `Classify*` walks and reports identical bodies under density policy 0.

[INFERENCE] The duplication is real, but the classifier is the exact proof core. The repository treats proof-grade explicitness as a feature. A shrink here must preserve every classification outcome on the existing fixture matrix, and the design round may legitimately refuse the cut for explicitness reasons.

### 2.3 The display row drawer reads only kind and sentence

[SOURCE] `Editor/AmuseAvatarOptimizerEditor.cs:230-248` shows `DrawTransformationRow` reading `row.Kind` and `row.Sentence` and nothing else. `Editor/DryRun/MaterialTransformationRow.cs` carries eight further data fields with null and -1 sentinels, and the type comment says the sentinels exist so a drawer cannot read a stale count.

[SOURCE] The show-material-transformations feature landed on `main` in the merge `8a2607b` on 2026-10-13, earlier the same day as this record. The fields were designed for that feature in `docs/superpowers/specs/2026-10-13-show-material-transformations-design.md`.

[INFERENCE] The fields are unused by any drawer at this date. They are also brand-new designed surface for a feature that may grow count display. Ponytail discipline cuts them. Prudence notes the feature is one merge old. This is a scope call for the design round.

### 2.4 The research seam has a documented purpose

[SOURCE] The research package `Editor/Collection/RendererObservationBuilder.cs:41-63` documents the `ResearchMaterialSemanticsProvider` overload: the public development project installs no vendor shader, so `ProvenOpaque` and `MissingTextureEvidence` counting is otherwise unreachable in CI. The comment states it is a pass-through to a test shape the analysis class already ships, not an extension point.

[INFERENCE] The audit tagged this finding medium confidence, and the document confirms why. A cut requires the research census to reach those refusals through another route, or the census loses CI coverage for two refusal kinds. The design round must either scope the migration or drop this finding.

### 2.5 The shared test scope already exists

[SOURCE] `Tests/Editor/Shared/TestTransientScope.cs` provides `Track<T>` and a `TearDown` that null-checks every tracked object and deletes the owned temp folder.

[SOURCE] The same tracked-object pattern, a list field plus a teardown loop, exists in eleven Build test classes counting the shared fixture base, in nine Analysis and Host test classes, and in `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs` and `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs` (`docs/superpowers/audits/2026-10-13-ponytail-audit.md`, finding 3). The controller re-verified the counts by grep at base `e6f6c7e`. The copies drifted: some skip the null check, and some pass `allowDestroyingAssets: true` to `DestroyImmediate` while others do not.

[INFERENCE] Consolidation must preserve each suite's destroy semantics or test behavior changes. A shared tracker with an explicit destroy-assets flag, or per-site choice, is the safe shape. This constraint belongs in the plan.

## 3. Decision classes

The design round selects findings by class. The classes carry different risk and different process weight.

- **D1 - dead production symbols and production test seams.** Production symbols whose only callers are tests, plus plain-dead symbols with no callers: findings 11, 14, 19, 27, 31, 32, 34, 35, 41, 42, 43, 44, 45, 47, 50, 51, 52, 54, 55. Findings 45, 50, 54, and 55 have no callers at all, and finding 55 deletes an unread property in a research test file. Those four delete with no test-side edit. The rest move composition into the test assembly. Suite outcomes must stay identical.
- **D2 - dead flexibility with a document conflict.** Finding 1 (the three non-alpha semantic pipelines) and finding 13 (the eight row fields). Facts are verified. Direction or recency argues the other way. Owner decision.
- **D3 - test duplication collapses.** Findings 2, 3, 5, 6, 8, 12, 16, 17, 20, 21, 22, 23, 24, 25, 26, 30, 36, 37, 38, 39, 40, 48, 53. Mechanical, low risk, and the suites must pass unchanged.
- **D4 - production deduplication and shrink.** Findings 4, 9, 10, 15, 18, 28, 29, 33, 46, 49. Behavior-identical by argument, and each needs its own verification step. Findings 4 and 9 touch the proof core. Findings 9, 10, 15, 18, 28, and 49 are medium confidence in the audit. The audit caveat 4 also requires a verification step for finding 30 before any cut.
- **D5 - cross-package seam.** Finding 7. Requires a research-side migration with census coverage consequences. Section 2.4.

## 4. Interactions

- Same-file groups let one pass per file. Findings 4, 9, and 47 in `Editor/Analysis/TriangleAlphaClassifier.cs`. Findings 15, 44, 51, and 54 in `Editor/Semantics/LilToon/LilToonSourceAttestation.cs`. Findings 13 and 50 in `Editor/DryRun/MaterialTransformationRow.cs`. Findings 46 and 49 in `Editor/DryRun/DryRunCache.cs`. Findings 18 and 29 in `Editor/Build/AlphaSeparationPreparation.cs`.
- Findings 13 and 33 form the dry run display chain across `Editor/DryRun/` and `Editor/AmuseAvatarOptimizerEditor.cs`. Plan them together, row shape first.
- Findings 3 and 6 both land helpers under `Tests/Editor/Shared/`. Finding 3 routes suites to the existing `TestTransientScope`. Finding 6 adds one new pin type. One task can land both.
- The D1 deletions change no behavior. The D4 shrinks change implementation shape. Running the full product and research assemblies after each class, not after each finding, matches the validation policy.

## 5. Risk register

1. Proof-core shrinks (findings 4 and 9) risk the exactness culture. The falsifier suite and the fixture matrix must pass with unchanged outcomes. A single changed classification outcome is a stop condition.
2. Test tracker consolidation must preserve destroy semantics per call site. A wrong default can make tests destroy assets or leak objects. Section 2.5.
3. The attestation gather dedup (finding 15) must keep the fail-closed omissions of both entry points. Attestation is pinned data. A refactor that re-derives digests is a contract violation.
4. The dry run cache shrink (finding 49) touches a just-landed feature. The avatar-sweep tests must pass unchanged.
5. The research seam cut (finding 7) can silently drop census refusal coverage. The research assembly must keep counting both refusal kinds.
6. Findings 9, 10, 15, 18, 28, 30, and 49 are medium confidence in the audit or carry its verify-first caveat. Each needs its verification step before any cut.
7. All cuts touch no private data and no Unity asset. The whole run is source and test editing.

## 6. Validation shape

[SOURCE] The repository tests through the Unity Test Runner, EditMode mode, in two assemblies: `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor`. There is no CLI test path.

[INFERENCE] The work splits into behavior-identity changes. The validation is: a focused filter per touched suite, then the full product assembly, then the full research assembly, with observed counts recorded at each step. A filtered run that reports zero tests is a failure. RED/GREEN does not apply to pure deletions and behavior-identical refactors. The existing suites are the regression guard. New tests assert nothing new, because the contract does not change.

## 7. Open points for the design round

1. Finding 1: cut the three non-alpha pipelines, or keep them as blessed direction. This is the owner decision the spec must present with a recommendation.
2. Finding 13: cut the eight row fields on the one-merge-old display feature, or keep them as recent designed surface.
3. Findings 4 and 9: shrink the proof core, or decline for proof-grade explicitness.
4. Finding 7: migrate the research census and delete the seam, or keep the documented CI seam.
5. Scope: all D1, D3, and safe D4 findings form the mechanical core. The spec must state the selected finding list explicitly so the plan and the final owner review can check it.

## 8. Blast radius

[INFERENCE] The mechanical core (D1 plus D3) touches production symbols only through deletion and touches test files through consolidation. The D4 shrinks touch seven production areas: the classifier, the plugin barrier, the preparation refusal paths, the attestation gather, the dry run cache, the swap-in parameter shape, and the editor dry-run display. No public API changes. No package manifest changes. No asset changes. The research package changes through finding 55 in the mechanical core, and through finding 7 if it enters scope.

## 9. Verification record

Eight parallel read-only scouts gathered the audit facts on 2026-10-13. The controller verified the load-bearing facts in section 2 by direct reads and greps on the same date, at base `e6f6c7e`. Line counts in the audit table are `[ESTIMATE]`s. Three adversarial accuracy reviews ran on this document on 2026-10-13, covering citations, reasoning, and record rules. They found two wrong line citations, a false time-gap sentence, missing class members, false interaction claims, and unlabeled estimates. This document incorporates every confirmed fix.
