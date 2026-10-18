# Ponytail Line Count Reduction Investigation

Status as of 2026-10-18.

## Privacy note

This investigation reads only public repository source. It records no private avatar, no private identifier, and no machine-specific fact.

## Evidence labels

- `[SOURCE]` A fact read in this repository at the base commit. The citation gives a repository-relative path.
- `[INFERENCE]` A deduction from named facts.
- `[RECOMMENDATION]` A proposal for the design round.
- `[ESTIMATE]` A line count from the scan round. It is plausible and unverified to the line.

Paths under the AMUSE package drop the `Packages/com.alrauna.amuse/` prefix. Paths under the research package name it.

## 1. Purpose

The owner set this run: a parallel audit scan, this investigation, a parallel adversarial review of this investigation, the design spec, a parallel adversarial review of the spec, the implementation plan, a parallel adversarial review of the plan, then subagent execution with a commit after each finalized change. The run halts when a choice needs owner input.

The run reduces repository line count. The previous refactor branch split large files into focused ones. It kept forwarding members for compatibility. Those forwarders, the dead data they left behind, and the unapplied remainder of the 2026-10-13 ponytail audit are the raw material for this run.

Method: six parallel read-only scans covered disjoint slices on 2026-10-18. The slices were `Editor/Build/`, `Editor/Analysis/`, `Editor/Semantics/` core plus Poiyomi, `Editor/Semantics/LilToon/`, editor root plus DryRun plus Presets plus Runtime, and test helpers plus the research package. Every dead, unused, or one-caller claim carries a repository-wide grep across product, tests, and research. The controller re-verified the load-bearing claims by direct reads and greps at this base. The merged audit lists 30 ranked findings.

## 2. Relation to the 2026-10-13 ponytail audit

[SOURCE] `docs/superpowers/audits/2026-10-13-ponytail-audit.md` recorded 56 findings with an estimated net cut of 3775 lines `[ESTIMATE]` at base `8a2607b`.

[SOURCE] At this base dated 2026-10-18, the symbols behind audit findings 41, 42, 45, and 56 no longer exist. `ExactUvGeometry.HalfUlp` and the `FloorMod(BigInteger, int)` overload are gone. `AaoAtlasTextureAttestation.SetAdmittedVersionsForTests` is gone. `DeleteTempFolder` lives once on `TransientUnlockTestLifecycle`. [SOURCE] The earlier rounds applied the main bodies of findings 2, 3, 6, 16, 23, 25, and 26. `NewSwapClip` lives on `AlphaSeparationTestLifecycle`. 28 test files use the shared `TestTransientScope`. The shared `AnisotropicFilteringPin` and the shared `OddBoundaryPixels` and `EvidenceNamingAlphaSource` statics carry the former copies.

[SOURCE] Audit finding 50 is applied at this base. The `MaterialTransformationRowKind.Disclosure` member and its builder no longer exist. [SOURCE] Audit finding 13 is stale. As of 2026-10-18 the drawer reads `SlotIndex` and `SourceMaterialPath` (`Editor/AmuseAvatarOptimizerEditor.cs:355-385`). Serialized rows map every field back (`Tests/Editor/Build/PlayModeBuildStoreTests.cs:257-283`).

[INFERENCE] This investigation carries forward only the old findings that its greps still confirm at this base. It drops the applied and the stale ones. The carried findings are the three policy-arm runners, the remaining tracked-object copies, the container-texture fixture repetition, the arm fixture base classes, the `CurveMaterials` twin, and the corner-triangle fixture repetition.

## 3. Verification of the load-bearing facts

### 3.1 The three non-alpha semantic pipelines have no production reader

[SOURCE] Every reader of `.Semantics.BaseColor`, `.Semantics.Emission`, and `.Semantics.Normal` is a test file. The grep covered `Packages/` at this base. Production constructs these slots as constant Unknown (`Editor/Semantics/UnityMaterialSemantics.cs:1145-1149`). The research census calls `AnalyzeBaseMaterial` and reads only `IsSupportedMaterial`.

[SOURCE] `docs/architecture/vision.md` blesses the four-output vocabulary as current design and warns against freezing it (lines 49 and 115). Its long-term goal is material combining, not the four-output model.

[INFERENCE] This is the same conflict the 2026-10-13 audit recorded. Facts and direction disagree. The cut is an owner decision, not a defect. Estimated cut 1400 `[ESTIMATE]`.

### 3.2 PoiyomiMaterialSemantics carries dead gate data and a forwarding facade

[SOURCE] The private arrays `AlphaCoverageGates`, `AlphaFeatureGates`, and `TextureBackedAlphaGates` at `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:203-282` have zero readers in their own file. `PoiyomiAlphaInterpreter` defines and reads its own copies (`PoiyomiAlphaInterpreter.cs:60-94`, used at lines 218-220, 345, 517, 602, 1078, 1158). The controller confirmed this by grep.

[SOURCE] The same file holds dead property and detail consts. The scout grepped each and found zero live readers: 12 blend consts, the alpha-mask const block, `AlphaMaskExpressionDetail`, and others. `PoiParallaxProperty` and two alpha-mask consts are referenced only inside the dead gate array and the dead detail initializer. The interpreter defines its own copies where needed.

[SOURCE] The same class forwards to `PoiyomiSourceAttestation` with six expression-bodied members and to `UnityTextureEvidence` with block-bodied members (`Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1144-1330`). The controller read this range. `TryGetSupportedUvMapping` carries real validation logic and is not a pure forward. It stays. `TryGetMainTextureSampling` carries one Poiyomi-specific property read. The rest are pure delegates.

[INFERENCE] Deleting the dead data changes no behavior. Retargeting the pure forwards moves call sites to the owning types. Estimated combined cut about 200 `[ESTIMATE]`.

### 3.3 Two attestation types forward the shared version seam

[SOURCE] `Editor/Semantics/AaoAtlasTextureAttestation.cs:36-100` and `Editor/Semantics/ReplacementTextureAttestation.cs:35-107` forward members of the shared `AdmittedProducerVersions` seam. The forwarded members on the atlas type are `ReadInstalledPackageVersionOrNull`, `AdmittedVersions`, `ResetForTests`, `TryReadInstalledProducerVersion`, `ClearVersionCacheForSession`, and `IsVersionAdmitted`. The replacement type additionally forwards `SetAdmittedVersionsForTests`. Grep counts about 118 call sites outside the three definition files: 6 production and 112 test-side.

[SOURCE] Each forward carries a doc comment that names the addressing tests. The comments say "Forwarded seam".

[INFERENCE] Exposing the `Versions` field and retargeting callers removes about 113 lines `[ESTIMATE]`. Behavior is identical. The spec must restate the doc-comment contract at the new location. Attestation data itself is untouched. No digest is re-derived and no GUID changes.

### 3.4 The classifier duplicates window prologs and preambles

[SOURCE] Each `ClassifyXxx` arm and its `HasMappedWitnessXxx` mirror open with an identical candidate-window prolog. The prologs build the domain, resolve the window, and guard overflow and the support-region budget. Only the give-up value differs. The scan cites eight sites in `Editor/Analysis/TriangleAlphaClassifier.cs`.

[SOURCE] `Classify` and `ClassifyMapped` duplicate their preamble and their filter-by-wrap dispatch. The trilinear arms alias the bilinear walks.

[INFERENCE] Consolidation is mechanical in shape but touches the exact proof core. The repository treats proof-grade explicitness as a feature. A single changed classification outcome is a stop condition. Combined estimate about 175 `[ESTIMATE]`. The design round weighs explicitness against line count. The owner decides.

### 3.5 AdmittedMaterialStates forwards to AnimatedPropertyAdmission

[SOURCE] `Editor/Analysis/AdmittedMaterialStates.cs:437-495` re-exports `TryAdmitDerivedEvidence`, `AdmitScalar`, `AdmitColor`, and `AdmitVector` as pure forwards. Grep finds 2 production callers (`Editor/Build/AlphaSeparationMaterialConverter.cs:312`, `Editor/DryRun/AlphaSeparationDryRun.cs:1303`) and 20 test calls in one test class.

[INFERENCE] Migration is mechanical. Estimated cut 58 `[ESTIMATE]`.

### 3.6 Preparation forwards to AlphaSeparationMaterialConverter

[SOURCE] `Editor/Build/AlphaSeparationPreparation.cs:805-880` holds four one-call forwards into `AlphaSeparationMaterialConverter` plus the dead private `IsGrantedShader`. Seven call sites exist.

[INFERENCE] Retarget and delete. Estimated cut 55 `[ESTIMATE]`.

### 3.7 A preparation refusal value and its detail channel are dead

[SOURCE] Every `OpaqueConversionRefused` reference outside its declaration is on `AlphaSeparationSlotRefusal`, a different and alive enum. The `PreparationRefusal.OpaqueConversionRefused` member at `Editor/Build/AmusePreparationDecision.cs:25` has zero producers and zero consumers. The `RefusalDetail` property and the detail parameter on the same type have zero readers and zero passing callers. The controller confirmed this by grep.

[SOURCE] Recorder behavior throws on `None`. This finding removes a named value that nothing names. Test loops that enumerate all refusal values skip it after removal.

[INFERENCE] Removal is a no-behavior change. Estimated cut 24 `[ESTIMATE]`.

### 3.8 The lilToon eligibility evaluators duplicate their render-state gate block

[SOURCE] Gates 5 through 13 of `Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs:186-289` and `Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs:267-370` are near-verbatim. Bodies and comments match except the cutoff bound and the cutoff refusal value. Each file also carries a private `Read` helper. The general form lives in `LilToonMultiSourceEligibility.cs:442-465`.

[SOURCE] Six files hand-roll NaN and Infinity sweeps while `EvidenceGates.IsFinite` exists. Four sites hand-roll loops with standard library equivalents: `KeywordIsPresent`, `IsSpecializedContainerShaderName`, `CountCharacter`, and `BuildRecipeSchema`. `KeywordSetReadsBack` hand-rolls set equality that `HashSet.SetEquals` states in one line. Three files redeclare the same lilToon property-name consts. `TryVerifyIdentityForShaderName` re-encodes the name dispatch that `TryGetProfileForShaderName` already owns.

[INFERENCE] One shared evaluator keyed by cutoff bound and refusal value collapses the gate block. The rest are mechanical replacements. Combined estimate about 300 `[ESTIMATE]`.

### 3.9 Preset and dry-run shapes

[SOURCE] `Editor/Presets/OptimizerPreset.cs:10-119` is a hand-written immutable class with two construction sites. The repository already uses positional records (`Editor/Semantics/AlphaUnknownReason.cs:55`). [SOURCE] `Editor/DryRun/AlphaSeparationDryRun.cs` holds three one-call wrappers around `DryRunRowBuilder`. `Editor/DryRun/DryRunRowBuilder.cs:187-196` duplicates `AmuseReports.SlotCountPhrase`. `Editor/DryRun/DryRunCache.cs:22-45` holds `RendererFingerprintBody`, which production writes but never joins into a fingerprint key. Only two test reads touch it.

[INFERENCE] The record conversion preserves the constructor and property shapes. The rest delete or inline. Estimated cut about 107 `[ESTIMATE]`.

### 3.10 Test-side remainders

[SOURCE] Three mirror policy-arm runners live in `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:5295-5430`. Five test classes still carry a private `Track<T>` list and teardown loop beside an existing `TestTransientScope` field. `AlphaSeparationTestLifecycle.cs:226-254` holds `CommittedClipCarrying`, a byte-identical twin of `CommittedClipWithObjectBinding`, with two callers. The two transient-unlock suites duplicate `CurveMaterials`. `TransientUnlockTestLifecycle.cs:479-745` duplicates the lock-tag block and the animation builder scaffolding. The research probe `AlphaEvidenceProbe.cs:167-341` duplicates its blit and readback scaffolding across two entry points. `Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs` repeats a container-texture fixture block nine times. Four files copy a corner-triangle fixture. Two fixture classes repeat an arm-fixture skeleton.

[SOURCE] The research file `ThryLockToolDigestRecord.cs` has zero references repository-wide. Its own header states it holds no consumer by design and calls deletion a reviewed evidence decision. Estimated cut 30 `[ESTIMATE]`.

[INFERENCE] Test consolidation must keep each suite's outcomes and destroy semantics identical. The digest record cut requires the owner to record that evidence decision. Combined estimate about 460 `[ESTIMATE]`.

### 3.11 Smaller production cuts

[SOURCE] Each claim below carries its citation and caller count from the scan round.

- `TransientUnlockSwapIn.RecordRefusal` forwards to `AmuseReports.RecordRefusal` with one call site (`Editor/Build/TransientUnlockSwapIn.cs:508-518`). Estimated cut 11 `[ESTIMATE]`.
- `SlotTransformVerdict.FoldedUnprovenTriangles` is produced but never read in production. Three test assertions read it. The build recomputes the count from the triangle ordinal list (`Editor/Analysis/SlotTransformDecision.cs:20-25`). Estimated cut 6 `[ESTIMATE]`.
- The `TryTransform(UvMapping, ...)` decode-and-go overload has zero production callers and 17 test callers (`Editor/Analysis/AffineUvTransform.cs:54-65`). Estimated cut 12 `[ESTIMATE]`.
- `SerializedTransformationRow.refusedSlots` and `slotCount` have zero writers. The dry-run bridge reads them as constant zero (`Editor/Build/PlayModeBuildStore.cs:20-21`). Estimated cut 8 `[ESTIMATE]`.
- `AmuseReports.RecordRefusal` and `AmuseReports.SlotSeparationRefusal` declare optional `rendererName` parameters. No caller of these two methods passes the name. The two `rendererName:` test hits target `SlotAnalysisRefusal`, a different method with a live parameter. The fallback inside the two methods already supplies the renderer name (`Editor/Build/AmuseReports.cs:220-260`). Estimated cut 7 `[ESTIMATE]`.
- `HostLifecycleCapability.EqualsOrdinal` wraps `string.Equals` and has two in-file callers. Inlining it is a shrink, not a deletion (`Editor/Build/HostLifecycleCapability.cs:256-259`). Estimated cut 4 `[ESTIMATE]`.

[INFERENCE] Combined estimate about 48 `[ESTIMATE]`. All are behavior-identical.

## 4. Decision classes

- **D1 dead production symbols and data.** No behavior change. Every entry cites section 3. The dead gate arrays and consts (3.2), the dead refusal value and detail channel (3.7), `IsGrantedShader` (3.6), the `RecordRefusal` wrapper, `FoldedUnprovenTriangles`, `RendererFingerprintBody`, the test-only `TryTransform` overload, the never-written serialized row fields, and the `rendererName` parameters on the two separation-refusal methods (3.11), plus the dry-run delegation wrappers and the `SlotCountPhrase` duplicate (3.9).
- **D2 owner decisions.** The three non-alpha pipelines against the vision document. The two proof-core classifier shrinks against proof-grade explicitness. The `ThryLockToolDigestRecord` deletion that its own header gates on a recorded decision.
- **D3 production deduplication and shrink.** Behavior-identical by argument. The forwarding facades, the version-seam exposure, the lilToon evaluator collapse, the standard library replacements, the preset record, the play-mode row helpers, the preparation wrapper layer, and the `EqualsOrdinal` inline (3.11).
- **D4 test consolidation.** No new failures versus the recorded baseline (section 7). Destroy semantics stay per site. The policy-arm runner merge, the tracked-object remainders, the fixture-helper extractions, the lifecycle builders, and the research probe core.

## 5. Interactions

- `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` hosts three findings from 3.2. One ordered pass per change class avoids line-shift conflicts.
- The version-seam exposure touches two attestation files, their doc comments, and about 118 call sites. The `GeneratedTextureAttestation` production path and the two session-reset sites move in the same change.
- Both classifier findings touch one file. Run the full classifier and resolver suites after.
- The lilToon evaluator collapse touches two files that also carry the `Read` triplication. Plan one change per file with the shared helper landing first.
- Test consolidation groups: the Build lifecycle group, the policy-arm group, the Host fixture group, and the research probe group.

## 6. Risk register

1. Proof-core shrinks risk the exactness culture. The falsifier suite and the fixture matrix must produce the same outcomes as the recorded baseline. One changed classification outcome is a stop condition. The baseline itself carries 24 known deterministic failures (section 7), so gates compare against the baseline and not against zero.
2. Attestation relocation must not churn GUIDs, re-derive digests, or drop the fail-closed gather omissions. Only member locations move.
3. Test tracker consolidation must preserve the `allowDestroyingAssets` choice per site. Several suites destroy assets and several must not.
4. Facade retargets touch many call sites. Compilation and the suites catch misses.
5. Removing the never-written serialized row fields changes the persisted shape. Old saves carry the extra fields. Unity ignores unknown fields on deserialize. Record this in the spec.
6. The refusal enum removal changes the value lists that enumerating tests walk. Those tests must stay green.
7. The research package stays read-only. All research findings are in its test assembly except the digest record deletion, which removes a file.
8. All cuts are source and test edits. No Unity asset, no import setting, no scene changes.
9. The run overlaps a same-day fixture-scope repair track with a plan on record. That track fixes the baseline's root cause. If it lands first, the baseline turns green and every gate in this run tightens to zero. The design round must coordinate ordering before execution.

## 7. Validation shape

[SOURCE] On 2026-10-18, 24 EditMode tests fail deterministically in `Alrauna.Amuse.Tests.Editor` at the recorded base of the diagnosis investigation: 10 in the apply suite, 5 in the preparation suite, 1 in the finish-plugin suite, 2 in the report-strings suite, and 6 in the dry-run suite. The failures reproduce at the merge base. The research assembly passes with 140 passed, 0 failed, and 4 skipped. The same-day diagnosis record names one root cause and a repair plan (`docs/superpowers/investigations/2026-10-18-test-failure-diagnosis-investigation.md`).

[SOURCE] The repository tests through the Unity Test Runner, EditMode mode, in two assemblies: `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor`. There is no CLI test path.

[INFERENCE] The work splits into behavior-identity changes. Every gate in this run compares against the recorded baseline and not against zero. The gate is: no new failures in the touched suite beyond the recorded set. The validation is a focused filter per touched suite, then the full product assembly, then the full research assembly. The controller records observed counts at each step. A filtered run that reports zero tests is a failure. RED/GREEN does not apply to pure deletions and behavior-identical refactors. If the fixture-scope repair track lands first, the baseline turns green and the gates tighten to zero.

## 8. Open points for the design round

1. The three non-alpha pipelines: cut them, or keep them as declared direction. Owner decision with a recommendation.
2. The two proof-core classifier shrinks: cut them, or keep proof-grade explicitness. Owner decision.
3. `ThryLockToolDigestRecord`: delete it and record the evidence decision its header demands, or keep it. Owner decision.
4. Scope: the spec must state the selected finding list explicitly so the plan and the final owner review can check it.

## 9. Blast radius

[INFERENCE] The mechanical core (D1 plus D3 plus D4) touches production through deletion and call-site retargets and tests through consolidation. No public API changes. The runtime component and its serialized preset data stay untouched. No package manifest changes. No asset changes. Both packages change. The itemized estimates sum to about 1360 lines `[ESTIMATE]` for the mechanical core. The three owner items sum to about 1605 lines `[ESTIMATE]`: the pipelines 1400, the classifier shrinks 175, and the digest record 30. The all-in total is about 2965 lines `[ESTIMATE]`. No package dependencies change.

## 10. Verification record

Six parallel read-only scans gathered the audit facts on 2026-10-18, one per module slice. The controller verified the load-bearing facts in section 3 by direct reads and greps on the same date at this base. The controller also re-verified the 2026-10-13 audit symbols at this base and dropped the applied and stale findings. Three parallel adversarial reviews of this document ran on 2026-10-18: citation accuracy, reasoning and completeness, and record rules. They reported five fact defects, four must-fix reasoning defects, and eight rule violations. The controller confirmed each defect against the sources and incorporated the corrections above.
