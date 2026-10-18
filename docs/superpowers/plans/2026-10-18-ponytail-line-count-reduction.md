# Ponytail Line Count Reduction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut about 2790 lines `[ESTIMATE]` of dead code, forwarding facades, and duplication with zero behavior change.

**Architecture:** Pure deletions and call-site retargets across `Editor/`, `Tests/`, and the research test assembly. One new shared file per spec rule 6. The pipeline cut (G10) lands last because it is the largest surface.

**Tech Stack:** C# against Unity 2022.3. Compilation and EditMode tests run inside Unity through the Unity MCP tools (`refresh_unity`, `read_console`, `run_tests`, `get_test_job`). There is no CLI test path.

**Spec:** `docs/superpowers/specs/2026-10-18-ponytail-line-count-reduction-design.md`

## Global Constraints

1. **Baseline gate.** The operative baseline is the observed failing-test name set recorded by Task 0, which must equal or be reconciled against the diagnosis record's named set: 6 failures in `AlphaSeparationDryRunTests` (`AnisotropyRefusalProducesRefusedSentence`, `CutoutSplitSlotAllProvenProducesConvertableSentence`, `CutoutSplitSlotProducesSplittableSentence`, `DryRunVerdictsMatchTheBuildOnAMixedRenderer`, `FoldSlotProducesConvertableSentence`, `StreamingPngTextureDecodesInMemoryAndDoesNotRefuse`), 5 in `AlphaSeparationPreparationTests` (`EachSlotSelectsItsOwnCoveragePair`, `PoiyomiSiblingSlotsSelectDifferentPairs`, `GeneratedSubAssetTexture_IsAdmittedAndProvenOpaqueEndToEnd`, `PreserveTransparencyMipPolicyScopesTheProofEndToEnd`, `RefusedTransparentSlotLeavesItsAdmittedSiblingsConverted`), 1 in `AmusePlatformFinishPluginTests` (`PoiyomiSiblingsProveUnderDifferentPairs`), 2 in `AmuseReportsSlotTests` (`CutoutFoldReportsTheCutoutVariant`, `TransparentFoldReportsByteIdenticalBaseOutput`), and 10 in `AlphaSeparationApplyTests` (`AcceptedPolicyAppendsTheOpaqueSubmeshForACutoutMixedHalfSlot`, `ConservativePolicyKeepsTheCutoutMixedHalfSlotOriginal`, `CutoutBuildCarriesTheFullArtifactEndToEnd`, `CutoutSplitSlotRewritesCurvesOntoTheAppendedSlot`, `TransparentSiblingKeepsPoiyomiAndCutoutResultsByteIdentical`, `ACapturedTargetMismatchBetweenPrepareAndApplyRefusesEveryPreparedSlot`, `CutoutSwapToAttestedOpaqueMapsToItselfWithoutAClone`, `DesktopAcceptanceRevokedBetweenPrepareAndApplyRefusesEveryPreparedSlot`, `FullyOpaqueCutoutEvidenceConvertsWithoutAcceptance`, `TheApplyStalePolicyGuardHonorsThePinnedBuildTarget`). Research assembly is green (140 passed, 0 failed, 4 skipped).
2. **Gate for every commit:** observed failing set is a subset of the baseline name set, and passed count per assembly is at or above the floor. The floor is baseline passed minus the passing tests the task's disposition deletes, itemized by full name in the ledger before the gate run. Task 12 deletes whole passing suites. Tasks 2 and 9 shrink or rename assertions. A filtered run that reports 0 tests is a failure. Record observed counts in the ledger.
3. **Scope guards:** no edit inside `Editor/Analysis/TriangleAlphaClassifier.cs`. No edit to `docs/architecture/vision.md`, the runtime component, assets, prefabs, scenes, import settings, package manifests. No new dependency. No GUID churn. No digest re-derivation. Treat every Unity asset and `.meta` file as one unit.
4. **Research production code stays read-only.** `ResearchSourceApiBanTests` must stay green. Only the research test assembly and the dead digest file may change.
5. **Unity instance identity:** before the first MCP write of the run, read the Unity MCP instances resource, pin the matching instance, and read `Application.dataPath` through an in-editor query. `dataPath` ends with an `Assets` segment. The path with that segment stripped must equal this repository root. Stop on mismatch.
6. **Commit cadence:** the owner authorized one commit per finalized change. Every task below ends with its own commit after the full-assembly gate passes. Staging is by explicit path.
7. **Doc records:** every new or edited English document uses simple english. No machine paths, no semicolons, no contractions.
8. **Spec rule 6** permits exactly two new files: `Editor/Semantics/LilToon/LilToonProperties.cs` and `Editor/Semantics/LilToon/LilToonSharedEligibility.cs`.

---

### Task 0: Establish the baseline

**Files:**
- Create: `.superpowers/sdd/<plan>/progress.md` (ledger, via the SDD workspace script)

**Interfaces:**
- Produces: the operative baseline name set and passed counts every later task gates against.

- [ ] **Step 1: Verify the Unity instance identity** (Global Constraint 5). Record the check result in the ledger. Do not write before this passes.
- [ ] **Step 2: Run the full EditMode product assembly and the research assembly.** Record per-assembly passed/failed/skipped counts and the exact failing test names in the ledger.
- [ ] **Step 3: Reconcile.** If the observed set equals the diagnosis record's 24 names, record `baseline reconciled`. If it differs, record the delta in the ledger with `Ruling: operative baseline is the observed set because <reason>`, and continue. A delta that adds failures outside the five named suites is a stop condition: report instead of proceeding.
- [ ] **Step 4: Record the line-count baseline:** `git ls-files '*.cs' | xargs wc -l | tail -1` output in the ledger.

No commit (no repo change).

---

### Task 1 (G5): Delete the dead preparation refusal channel

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePreparationDecision.cs`

**Interfaces:**
- Produces: `PreparationRefusal` without `OpaqueConversionRefused`; `AmusePreparationDecision` without `RefusalDetail` and without the detail parameter on its factory.

- [ ] **Step 1:** Delete the `OpaqueConversionRefused` member from `PreparationRefusal` (investigation 3.7: zero producers, zero consumers). Delete the `RefusalDetail` property and the `detail` parameter on the refusal factory. Fix the construction sites that passed no detail (compile errors list them).
- [ ] **Step 2:** Confirm by grep that no `amuse.preparation.` report key or test enumeration references the removed member.
- [ ] **Step 3:** Refresh Unity, confirm zero compile errors, run both full assemblies, apply the gate.
- [ ] **Step 4: Commit** `refactor: drop the dead preparation refusal channel`.

---

### Task 2 (G9): Smaller production cuts

**Files:**
- Modify: `Editor/Build/TransientUnlockSwapIn.cs`, `Editor/Analysis/SlotTransformDecision.cs`, `Editor/Analysis/AffineUvTransform.cs`, `Editor/Build/PlayModeBuildStore.cs`, `Editor/DryRun/MaterialTransformationRow.cs`, `Editor/Build/AmuseReports.cs`, `Editor/Build/HostLifecycleCapability.cs`
- Modify tests: `Tests/Editor/Analysis/SlotTransformDecisionTests.cs`, `Tests/Editor/Analysis/AffineUvTransformTests.cs`, `Tests/Editor/Build/PlayModeBuildStoreTests.cs`, `Tests/Editor/DryRun/MaterialTransformationRowTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `SlotTransformVerdict` without `FoldedUnprovenTriangles`; `AffineUvTransform` without the `UvMapping` overload; `SerializedTransformationRow` without `refusedSlots`/`slotCount`; `AmuseReports.RecordRefusal`/`SlotSeparationRefusal` without `rendererName`.

- [ ] **Step 1:** Delete `TransientUnlockSwapIn.RecordRefusal` (one call site calls `AmuseReports.RecordRefusal` directly with the same arguments).
- [ ] **Step 2:** Delete `SlotTransformVerdict.FoldedUnprovenTriangles`: remove the property, its constructor parameter, and the `outcome == Fold ? unproven : 0` argument. The build already recomputes the count from `AlphaSeparationPreparation.cs:182-185`. Update the 3 test assertions that read the property.
- [ ] **Step 3:** Delete the `TryTransform(UvMapping, ...)` overload (`AffineUvTransform.cs:54-65`). Retarget its 17 test callers to `TryTransform(AffineUvTransform.Decode(mapping), ...)`.
- [ ] **Step 4:** Delete `SerializedTransformationRow.refusedSlots` and `slotCount` (`PlayModeBuildStore.cs:20-21`) and the two bridge arguments that read them as constant zero (`MaterialTransformationRow.cs:165-167`). Drop the two corresponding assertions and initializers in `PlayModeBuildStoreTests.FromSerializedMapsAllFieldsToMaterialTransformationRow`; rename the test to `FromSerializedMapsTheKeptFieldsToMaterialTransformationRow`. Old saves carry the extra fields and Unity ignores them on deserialize.
- [ ] **Step 5:** Delete the optional `rendererName` parameters on `AmuseReports.RecordRefusal` and `SlotSeparationRefusal` (`AmuseReports.cs:220-260`). The in-method fallback already supplies the renderer name. The two `rendererName:` test hits target `SlotAnalysisRefusal` and stay.
- [ ] **Step 6:** Inline `HostLifecycleCapability.EqualsOrdinal` at its 2 in-file callers as `string.Equals(a, b, StringComparison.Ordinal)`; delete the wrapper.
- [ ] **Step 7:** Refresh Unity, confirm zero compile errors, run both full assemblies, apply the gate.
- [ ] **Step 8: Commit** `refactor: cut dead production members and unused parameters`.

---

### Task 3 (G4): Delete the preparation converter-wrapper layer

**Files:**
- Modify: `Editor/Build/AlphaSeparationPreparation.cs`, tests in `Tests/Editor/Build/` that call the forwards

**Interfaces:**
- Produces: `AlphaSeparationPreparation` without `ConvertAdmittedMaterial`, `TryMapLilToonOutcome`, `TryMapPoiyomiOutcome`, `ConversionRequestForFamily`, and the dead private `IsGrantedShader`.

- [ ] **Step 1:** Delete the four one-call forwards (`AlphaSeparationPreparation.cs:805-880`) and the dead `IsGrantedShader`. Retarget the 7 call sites (4 in-file at `:252`, `:275`, `:354`, `:541`; 3 test-side) to `AlphaSeparationMaterialConverter` directly. Keep the live `IsGrantedShader` on the converter (`AlphaSeparationMaterialConverter.cs:378`).
- [ ] **Step 2:** Refresh Unity, confirm zero compile errors, run both full assemblies, apply the gate.
- [ ] **Step 3: Commit** `refactor: point preparation callers at the material converter`.

---

### Task 4 (G3): Delete the AdmittedMaterialStates facade

**Files:**
- Modify: `Editor/Analysis/AdmittedMaterialStates.cs`, `Editor/Build/AlphaSeparationMaterialConverter.cs:312`, `Editor/DryRun/AlphaSeparationDryRun.cs:1303`, `Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Produces: `AnimatedPropertyAdmission` as the only home of `TryAdmitDerivedEvidence`, `AdmitScalar`, `AdmitColor`, `AdmitVector`.

- [ ] **Step 1:** Delete the four forwards (`AdmittedMaterialStates.cs:437-495`). Retarget the 2 production and 20 test callers to `AnimatedPropertyAdmission`.
- [ ] **Step 2:** Refresh Unity, confirm zero compile errors, run both full assemblies, apply the gate.
- [ ] **Step 3: Commit** `refactor: admit animated properties directly`.

---

### Task 5 (G7): Preset record and dry-run shapes

**Files:**
- Modify: `Editor/Presets/OptimizerPreset.cs`, `Editor/DryRun/AlphaSeparationDryRun.cs`, `Editor/DryRun/DryRunRowBuilder.cs`, `Editor/DryRun/DryRunCache.cs`, `Tests/Editor/Presets/OptimizerPresetTests.cs`, `Tests/Editor/DryRun/DryRunCacheTests.cs`

**Interfaces:**
- Produces: `OptimizerPreset` as a positional record; `DryRunRowBuilder` calls at the wrapper sites; one `SlotCountPhrase`.

- [ ] **Step 1:** Convert `OptimizerPreset` (`OptimizerPreset.cs:10-119`) to `internal sealed record OptimizerPreset(...)` carrying every existing property, same name, same type, same order, with the existing constructor validation preserved (guards move to the primary constructor body or `init` accessors, identical checks and messages). The 2 construction sites (`PresetParser.cs:166`, `OptimizerPresetTests.cs:12`) compile unchanged. The type is editor-only JSON state and is never Unity-serialized.
- [ ] **Step 2:** Inline the three one-call wrappers `BuildResolvedRefusalRows`, `BuildRows`, `RendererRefusedRow` in `AlphaSeparationDryRun.cs` (`:842-849`, `:1360-1379`): call `DryRunRowBuilder.X(...)` at each single site and delete the wrappers.
- [ ] **Step 3:** Delete the duplicate `SlotCountPhrase` in `DryRunRowBuilder.cs:187-196`; call `AmuseReports.SlotCountPhrase` (the `using Alrauna.Amuse.Editor.Build;` already exists).
- [ ] **Step 4:** Delete the write-only `DryRunFingerprintInputs.RendererFingerprintBody` (`DryRunCache.cs:22-45`) and its single production write in `AlphaSeparationDryRun.cs:1398-1408`. Retarget the 2 test reads to their own literal key.
- [ ] **Step 5:** Refresh Unity, confirm zero compile errors, run both full assemblies, apply the gate. Preset suites and dry-run suites are in the gate scope; the dry-run suite carries 6 named baseline failures.
- [ ] **Step 6: Commit** `refactor: record the preset and flatten the dry-run shapes`.

---

### Task 6 (G1): Poiyomi dead data and forwarding facade

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`, callers in `Editor/Semantics/UnityMaterialSemantics.cs`, `Editor/Build/`, `Editor/DryRun/`, and ~25 test call sites

**Interfaces:**
- Produces: `PoiyomiMaterialSemantics` reduced to the base interpret entry, the two kept texture members, and the result types.

- [ ] **Step 1:** Delete the dead arrays `AlphaCoverageGates`, `AlphaFeatureGates`, `TextureBackedAlphaGates` (`:203-282`) and the dead consts listed in investigation 3.2 (12 blend consts, the alpha-mask block, `AlphaMaskExpressionDetail`, `PoiParallaxProperty`, and the rest with zero live readers). `PoiyomiAlphaInterpreter` owns the live copies.
- [ ] **Step 2:** Delete the six `PoiyomiSourceAttestation` forwards and the five `UnityTextureEvidence` forwards that are pure delegates (`:1144-1330`). Keep `TryGetSupportedUvMapping` (real validation logic) and `TryGetMainTextureSampling` (Poiyomi-specific property read). Retarget every caller to the owning type.
- [ ] **Step 3:** Refresh Unity, confirm zero compile errors, run both full assemblies, apply the gate. The Poiyomi suites and `UnityMaterialSemanticsTests` are in scope.
- [ ] **Step 4: Commit** `refactor: point Poiyomi callers at the owning types`.

---

### Task 7 (G2): Expose the attestation version seam

**Files:**
- Modify: `Editor/Semantics/AaoAtlasTextureAttestation.cs`, `Editor/Semantics/ReplacementTextureAttestation.cs`, production callers (`AlphaSeparationApply.cs:1057-1058`, `AmusePlatformFinishPass.cs:188-189`, `GeneratedTextureAttestation.cs:79-81`), ~112 test call sites across 8 test files

**Interfaces:**
- Produces: `AaoAtlasTextureAttestation.Versions` and `ReplacementTextureAttestation.Versions` as internal fields; both types carry no forwards.

- [ ] **Step 1:** Make each type's `private static readonly AdmittedProducerVersions Versions` internal. Delete all forwards on both types (6 on the atlas type, 7 on the replacement type). Move each forward's "Forwarded seam" doc-comment contract onto the exposed field, restated for the new location.
- [ ] **Step 2:** Retarget the ~118 call sites to `<AttestationType>.Versions.<member>`. Mechanical rename; the pinned admitted sets and session behavior stay byte-identical.
- [ ] **Step 3:** Refresh Unity, confirm zero compile errors, run both full assemblies, apply the gate.
- [ ] **Step 4: Commit** `refactor: expose the attestation version seam directly`.

---

### Task 8 (G6): lilToon deduplication

**Files:**
- Create: `Editor/Semantics/LilToon/LilToonProperties.cs`, `Editor/Semantics/LilToon/LilToonSharedEligibility.cs`
- Modify: `Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs`, `LilToonTransparentSourceEligibility.cs`, `LilToonMultiSourceEligibility.cs`, `LilToonDistanceFadeSemantics.cs`, `LilToonLayerAlphaTerm.cs`, `LilToonAlphaMaskSemantics.cs`, `LilToonMultiModeGate.cs`, `LilToonMultiResolution.cs`, `LilToonSourceAttestation.cs`, `LilToonOpaqueTarget.cs`, `LilToonCutoutMaterialSemantics.cs`, `LilToonTransparentMaterialSemantics.cs`, `LilToonAlphaEvidenceRequests.cs`, `Editor/Semantics/EvidenceGates.cs`

**Interfaces:**
- Produces: `LilToonProperties.<PropertyName>` consts; `LilToonSharedEligibility.Read(schema, values, property)` (general form moved verbatim from `LilToonMultiSourceEligibility.cs:442-465`); `LilToonSharedEligibility.EvaluateRenderStateGates(values, schema, allowDepthTestChange, maxProvableCutoff)` covering gates 5-12. The shared cutoff gate refuses with the constant `LilToonOpaqueConversionRefusal.ClipThresholdDiscardsOpaqueAlpha`, which both families already use.

- [ ] **Step 1:** Create `LilToonProperties` with each shared lilToon property spelling once. Retarget the three redeclaring files (`LilToonCutoutMaterialSemantics.cs:25-40`, `LilToonTransparentMaterialSemantics.cs:27-46`, `LilToonAlphaEvidenceRequests.cs:20-49`). Mechanical rename; spellings identical.
- [ ] **Step 2:** Create `LilToonSharedEligibility`. Move the general `Read` form verbatim from the multi evaluator; delete all three private copies and retarget.
- [ ] **Step 3:** Extract the shared render-state evaluator: move the verbatim gates 5-12 block out of `LilToonCutoutSourceEligibility.cs:186-289` into `LilToonSharedEligibility.EvaluateRenderStateGates(values, schema, allowDepthTestChange, maxProvableCutoff)`. Gate 5 branches on the depth-test policy flag both evaluators already receive, so the evaluator takes it. Call it from both evaluators, passing each family's cutoff bound. The shared cutoff gate keeps the one refusal value both families use today (`ClipThresholdDiscardsOpaqueAlpha`). Gate order, per-gate detail strings, and refusal values stay byte-identical. The per-family tails stay per family: cutout keeps its distance-fade arms (gate 13), transparent keeps its alpha-boost and subpass-cutoff arms (gates 13-15) including its comment that non-finite components are already refused at gate 2.
- [ ] **Step 4:** Replace the six hand-rolled NaN/Infinity sweeps and private `IsFinite` copies with `EvidenceGates.IsFinite` (`LilToonDistanceFadeSemantics.cs:50-53`, `LilToonTransparentSourceEligibility.cs:229-232`, `LilToonCutoutSourceEligibility.cs:319-322`, `LilToonMultiSourceEligibility.cs:318-340`, `LilToonLayerAlphaTerm.cs:431-441`, `LilToonAlphaMaskSemantics.cs:361-364`).
- [ ] **Step 5:** Standard library swaps: `KeywordIsPresent` to `keywords.Contains(keyword, StringComparer.Ordinal)`; `IsSpecializedContainerShaderName` loop to `Array.Exists`; `CountCharacter` to LINQ `Count`; `BuildRecipeSchema` to `CanonicalOpaqueTuple.Select(t => t.Property).ToArray()`; `KeywordSetReadsBack` (`LilToonOpaqueTarget.cs:345-385`) to `new HashSet<string>(written, StringComparer.Ordinal).SetEquals(readBack)` (the written set is duplicate-free per its own doc).
- [ ] **Step 6:** `TryVerifyIdentityForShaderName` (`LilToonSourceAttestation.cs:1907-1961`): share only the literal name-to-family-set dispatch with `TryGetProfileForShaderName`. Verification stays family-set based: opaque names verify against the opaque family set, cutout names against the cutout family set, every other name falls through to the transparent family set, and null evidence still throws `ArgumentNullException`.
- [ ] **Step 7 (verify gate):** Grep `Tests/` for `BuildMultiStateScalarSchema` and for assertions on the Multi request's scalar array contents. Shader fixtures and keyword lists are not pins. Cut the `OutlineScalarSchema` array and its concatenation only if that grep finds no assertion. Record the grep result in the ledger either way.
- [ ] **Step 8:** Refresh Unity, confirm zero compile errors, run both full assemblies, apply the gate. The LilToon suites, adversarial suites, and multi suites are in scope; the parity contract in `LilToonMultiSourceEligibility.cs:356-362` must hold.
- [ ] **Step 9: Commit** `refactor: share the lilToon eligibility machinery`.

---

### Task 9 (G8a): Build test consolidation

**Files:**
- Modify: `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`, `AlphaSeparationApplyTests.cs`, `AlphaSeparationPersistenceTests.cs`, `AlphaSeparationSplitTests.cs`, `LockedMaterialReconstructionTests.cs`, `TransientUnlockSwapInTests.cs`, `TransientUnlockTransformationTests.cs`, `TransientUnlockWindowCloseTests.cs`, `MeshCloneFinalizationCharacterizationTests.cs`, `AlphaSeparationTestLifecycle.cs`, `TransientUnlockTestLifecycle.cs`

**Interfaces:**
- Produces: one parameterized policy-arm runner; `TestTransientScope` routed tracking; lifecycle-owned helpers.

- [ ] **Step 1:** Merge `RunCutoutPolicyArm`, `RunMipPolicyArm`, `RunAlphaPolicyArm` (`AlphaSeparationPreparationTests.cs:5295-5430`) into one parameterized runner that takes the arm's optimizer field assignments. Case inventory: every call site maps to the merged runner with identical field values; the 5 named baseline failures in this file keep their test method names and outcomes.
- [ ] **Step 2:** Route the `Track<T>` list-and-teardown remainders in the 7 listed classes through the existing `TestTransientScope` field, preserving each class's `allowDestroyingAssets` value (`AlphaSeparationApplyTests.cs:49-58` true, `AlphaSeparationPersistenceTests.cs:45-56` false, `AlphaSeparationSplitTests.cs:310-321` false, `LockedMaterialReconstructionTests.cs:74-85` true, `TransientUnlockSwapInTests.cs:23-46` true, `TransientUnlockTransformationTests.cs:33-58` true, `TransientUnlockWindowCloseTests.cs:25-48` true). `MeshCloneFinalizationCharacterizationTests` already routes through its scope. Leave it untouched. The spec and investigation say five classes and the code shows seven. The list is authoritative. Record the delta in the ledger.
- [ ] **Step 3:** Delete `AlphaSeparationTestLifecycle.CommittedClipCarrying` (`:226-254`); retarget its 2 callers to `CommittedClipWithObjectBinding`. Move the duplicated `CurveMaterials` onto one suite-visible helper.
- [ ] **Step 4:** Extract the duplicated lock-tag block in `TransientUnlockTestLifecycle.cs:479-580` into one private `ApplyLockTags(material, originalShader)`. Extract the shared animation-builder skeleton in `:664-745` into one builder differing only in the state's motion.
- [ ] **Step 5:** Refresh Unity, confirm zero compile errors, run both full assemblies, apply the gate against the named baseline (this task touches four suites carrying baseline failures).
- [ ] **Step 6: Commit** `test: consolidate the build test scaffolding`.

---

### Task 10 (G8b): Research probe and Host fixture consolidation

**Files:**
- Modify: `com.alrauna.amuse.research/Tests/Editor/Calibration/AlphaEvidenceProbe.cs`, `com.alrauna.amuse.research/Tests/Editor/Calibration/AlphaEvidenceCharacterizationTests.cs`, `Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs`, `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`, corner-triangle fixture copies

**Interfaces:**
- Produces: one private probe blit core; shared fixture helpers. Both public probe entries stay.

- [ ] **Step 1:** Extract the duplicated blit/readback scaffolding in `AlphaEvidenceProbe.cs:167-247` and `:250-341` into one private core taking the graphics format and the pixel decoder. Both public entry points keep their signatures.
- [ ] **Step 2:** Extract `QuadrantPixels(size)` from the verbatim `Quadrants`/`CompressedQuadrants` pair (`AlphaEvidenceCharacterizationTests.cs:24-71`).
- [ ] **Step 3:** Verify the container-texture block repetition by grep in `UnityGeneratedTextureEvidenceTests.cs`. If nine repetitions survive, extract one local `AddContainerTexture` helper. Record the observed count.
- [ ] **Step 4:** Verify the corner-triangle fixture copies (4 files claimed) by grep. Extract one shared `TriangleAlphaInput` fixture helper if they survive. Record the observed count.
- [ ] **Step 5:** Refresh Unity, confirm zero compile errors, run both full assemblies, apply the gate. The research assembly must stay at 140 passed, 0 failed, 4 skipped or better.
- [ ] **Step 6: Commit** `test: share the probe core and host fixtures`.

---

### Task 11 (G11): Delete the digest record

**Files:**
- Delete: `Packages/com.alrauna.amuse.research/Editor/Collection/ThryLockToolDigestRecord.cs` and its `.meta`

**Interfaces:**
- Produces: nothing. Zero references exist (grep re-verified at execution).

- [ ] **Step 1:** Re-run the zero-reference grep. Delete the file and its `.meta` as one unit. The evidence decision is recorded in spec section 1.
- [ ] **Step 2:** Refresh Unity, confirm zero compile errors, run the research full assembly (must stay 140/0/4 or better) and the product assembly, apply the gate.
- [ ] **Step 3: Commit** `refactor: remove the unreferenced digest record`.

---

### Task 12 (G10): Cut the three non-alpha pipelines

**Files:**
- Modify: `Editor/Semantics/MaterialSemantics.cs`, `Editor/Semantics/UnityMaterialSemantics.cs`, `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`, `Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs` (output-scoped helpers only), `Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`, `Editor/Semantics/EvidenceGates.cs`, plus the research fixture `com.alrauna.amuse.research/Tests/Editor/Collection/CollectorSeamCountingTests.cs`, plus every test file referencing the removed outputs
- Delete: `Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs`, `LilToonEmissionTests.cs`, `LilToonNormalTests.cs` (with `.meta` files)

**Interfaces:**
- Produces: `MaterialSemantics` with one alpha slot and a single-argument constructor; `LilToonSemanticOutput`/`PoiyomiSemanticOutput` with `Material` and `Alpha` only; `SemanticOutput<T>`, `AlphaSemanticValue`, `EvidenceGates.AllUnknown()` unchanged.

This task runs on the most capable model. It is one atomic change: the repository does not compile between sub-steps.

- [ ] **Step 1:** Reduce `MaterialSemantics` to the alpha slot. Constructor becomes single-argument. Update all production construction sites (6 per the safety review) and the 3 research fixture constructions (`CollectorSeamCountingTests.cs:44-101`) to the alpha-only form.
- [ ] **Step 2:** Delete `ColorSemanticValue`, `ColorSemanticValueKind`, `NormalSemanticValue`, `NormalSemanticValueKind`, and their vocabularies. Keep `SemanticOutput<T>`, `AlphaSemanticValue`, and everything the alpha path reads. Production has zero readers of the removed slots (investigation 3.1).
- [ ] **Step 3:** Remove the three-output interpret paths from both frontends and from `UnityMaterialSemantics`. Keep `AnalyzeBaseMaterial` names, identity attestation, `IsSupportedMaterial`, the alpha interpret paths, alpha gates, and alpha diagnostics. The output enums keep `Material` and `Alpha` only; output-scoped diagnostic codes and members for the removed outputs go.
- [ ] **Step 4:** Test disposition, per file: delete the three named LilToon suites entirely. In every other file, grep `\.Semantics\.(BaseColor|Emission|Normal)` and `ColorSemanticValue|NormalSemanticValue`: delete tests whose purpose is a removed output (including the characterization suites that pin the Unknown-claiming behavior of removed outputs), and reduce four-output assertions in mixed suites (adversarial unsupported-shader tests become two-output). Tests that pin alpha behavior stay untouched. Record every deleted test's full name in the ledger before the gate run. The gate floor drops by the ledgered deletions (Global Constraint 2).
- [ ] **Step 5:** Confirm no report key names a removed output (verified in the spec review). Confirm the research census compiles (it reads only `IsSupportedMaterial`).
- [ ] **Step 6:** Refresh Unity, confirm zero compile errors, run both full assemblies, apply the gate. The alpha suites, falsifier suites, and the research assembly are the stop-condition guards: any alpha-path behavior change stops the run.
- [ ] **Step 7: Commit** `refactor: cut the unread non-alpha semantic pipelines`.

---

### Task 13: Measure and close

- [ ] **Step 1:** Record the final line count in the ledger (`git ls-files '*.cs' | xargs wc -l | tail -1`) and the net against Task 0.
- [ ] **Step 2:** The controller performs the wrap-up (final review, identifier sweep, report). Not a subagent task.

---

## Self-Review

**Spec coverage:** G1-T6, G2-T7, G3-T4, G4-T3, G5-T1, G6-T8, G7-T5, G8-T9+T10, G9-T2, G10-T12, G11-T11. All eleven groups have a task. Baseline establishment is Task 0 per spec section 7.
**Placeholder scan:** Step texts name exact symbols, files, and call-site counts from the investigation and the four spec reviews. The `OptimizerPreset` conversion intentionally instructs verbatim member preservation rather than transcribing 30 members, with the two construction sites named.
**Type consistency:** `LilToonSharedEligibility.Read` matches the general form the multi evaluator already exposes. The gate evaluator signature carries the depth-test policy flag gate 5 branches on. The `Versions` fields match the spec's corrected G2 wording. `AnimatedPropertyAdmission` and `AlphaSeparationMaterialConverter` names match Tasks 3 and 4.

**Review record:** four parallel adversarial reviews ran on 2026-10-18: accuracy against the spec and code, safety and latent bugs, architecture and ponytail adherence, and executability. The controller resolved one reviewer conflict by direct grep: the preparation forward count is 7 across 4 in-file and 3 test-side sites. The controller incorporated the corrections above and corrected the spec's G6 and G8 wordings plus the investigation's tracked-object count in the same pass.
