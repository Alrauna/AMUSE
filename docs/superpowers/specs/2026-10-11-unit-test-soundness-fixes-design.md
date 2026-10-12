# Unit test soundness fixes design

Privacy note: This document uses public source references and sanitized findings. It contains no private asset identifiers or machine identities.

Date: 2026-10-11.
Status on 2026-10-11: Design proposal complete. Awaiting user review before implementation.
Base: `fix/unit-test-soundness` from `main` at `eb48e74`. Nothing is committed on the branch.
Evidence base: `docs/superpowers/investigations/2026-10-11-unit-test-soundness-audit-investigation.md` (adversarially reviewed the same day).

## 1. Goal and approved contract

Repair every test the soundness audit classified as unable to guard production behavior. Keep every deliberate standing guard that the repository owns on purpose. Change no production behavior.

Contract:
- Strengthen first. Rewrite a test in place whenever it guards something real. Deletion is the last resort and requires the recorded argument that no rewrite adds value.
- The full EditMode suite stays green. The clean baseline on 2026-10-11 observed 2847 passed, 0 failed.
- No assertion loss without proof. Deletions happen only where a neighboring test already asserts the same fact, or where the test proves only Unity engine semantics that production tests already cover elsewhere.
- Deliberate guards survive. A deletion or rewrite must name its evidence in this design.
- Tests are deleted or repaired only in this list. Anything else discovered during execution is recorded, not acted on.

## 2. Dispositions

### 2.1 Tautologies: three deletions, two production-loop rewrites

Three are strict subsets of neighboring tests where no rewrite adds value. Deletion loses zero assertions.

| File | Method | Covered by |
|---|---|---|
| `Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs` | `ExpectedCanonicalTuple_HasTwentyThreeProperties` | `CanonicalOpaqueProperties_MatchTheIndependentlyStatedTuple` in the same file already asserts count 23 and set equality against the same literals |
| Same file | `ExpectedConversionSchema_HasTwentyThreeProperties` | `ConversionRequiredSchema_MatchesTheIndependentlyStatedSchema` in the same file |
| `Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs` | `ExpectedCanonicalTuple_HasEighteenProperties` | `CanonicalOpaqueProperties_MatchTheIndependentlyStatedTuple` in the same file |

Two fixture self-checks guard something real, so they are rewritten to run production code instead of the Unity property API:

- Rewrite `FixtureShader_DeclaresEveryConversionReadProperty`: drive the real capture and eligibility over the pristine fixture material and assert the outcome is not a `ConversionPropertyAbsent` refusal. A fixture shader that loses a declaration now fails through production with the same refusal the behavioral rows would hit. Stronger than the `material.HasProperty` loop it replaces, which production never consults.
- Rewrite `FixtureShader_DeclaresAddBlendOpForNonDependencyTests`: fold its two assertions into `AddBlendOp_DoesNotAffectEligibility` as fixture preconditions ahead of the vary-and-compare. The declaration guard rides inside the production-loop test that needs it, instead of standing alone against the engine API.

### 2.2 Tautology to delete with sibling coverage, 1

`Tests/Editor/Semantics/Characterization/SharedEvidenceAgreementTests.cs`, `BothFrontendsStillConsumeEverySharedFact`. It maps its own array through its own switch. The five `RefusedSharedFact_IsRefusedByBothFrontends` cases cover its stated purpose. Delete the method only. The private `ConsumingOutput` helper stays: those sibling cases and their fixture builders call it.

### 2.3 Mirror-oracle tests to strengthen, 2

These run production code and may stay, but their mirrored expectation weakens them.

1. `Tests/Editor/Analysis/ExactUvGeometryTests.cs`, `SharedDyadicToRationalMatchesDirectConstruction`. Replace the test-local restatement with independently derived rational literals for the same five cases. Keep both exponent branches.
2. `Tests/Editor/Analysis/AlphaPolicyBoundsTests.cs`, `PublishedFlagRuleResolvesTheThreeBands`. Replace the call through the test-local `PublishedFlag` mirror with the real published-chain route: run `SourceImageMaskedChain` band logic on boundary samples 252, 3, and 253 under the 99 percent bound and assert the three bands. Keep the existing test name intent in the new name.

### 2.4 Engine pins to delete, 2

| File | Method | Reason and no-rewrite argument |
|---|---|---|
| `Tests/Editor/Host/UnityAnimationCharacterizationTests.cs` | `AnimationUtilityRoundTripsAConstructedMaterialBinding` | Storage-only proof of Unity; the file concedes it kills nothing. A rewrite cannot add AMUSE value because the test never touches AMUSE code. |
| `Tests/Editor/Host/MeshCloneFinalizationCharacterizationTests.cs` | `DestroyingAnAbandonedCloneLeavesTheSourceIntact` | Its destroy call is the test's own `Object.DestroyImmediate`, so it characterizes Unity, not AMUSE. The production destroy-target failure mode is guarded by the Build sweep and window-close tests. A rewrite would either duplicate those tests or stay Unity-only; no rewrite adds value. |

### 2.5 Environment-vacuous gates to make loud, 5

`Tests/Editor/Calibration/VendorReachabilityTests.cs`. Convert to `Assert.Ignore` with a named message, matching the file's own lilToon gate style:

- `PoiyomiReachesProvenOpaqueThroughTheProductionPath` (early return)
- `PoiyomiReachesMustRemainTransparentThroughTheProductionPath` (early return)
- `ANonAssetTextureIsRefusedBySemanticsBeforeTheResolverSeesIt` (early return)
- `ALockedPoiyomiMaterialIsUnattestedAndReportsSemanticsUnknown` (early return)
- `AnInstalledFamilyMatchesTheVersionAmuseAttests` (silent loop continue; per-family continue becomes skip-and-report at the end, so a clean checkout reports one named ignore instead of a silent pass)

### 2.6 Wiring-only tests to delete, 2

| File | Method | Coverage that replaces it |
|---|---|---|
| `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs` | `PoiyomiSemanticOutput_DeclaresDeterministicDiagnosticOrder` | `MultipleFailingOutputs_GiveOneDiagnosticPerOutputInOutputOrder` in `PoiyomiAdversarialTests` pins the real ordering |
| `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs` | `TryGetProfileForShaderName_TransparentShader_ReturnsTransparentProfile` | `VerifyTransparent_CanonicalEvidence_Succeeds` and `Verify_UnsupportedShaderName_IsRefused` pin selection |

`ConsentSubjectForRefusedMultiMaterialNamesTheRefusal` stays. The review found it is the only test asserting a non-empty consent subject for a refused Multi material; the scout recorded that it still kills a zero-subject transport. It is weak, not dead. It moves to the kept list below.

### 2.7 Wiring-only tests to replace with a sound version, 2

`ReactivationPluginIsConfinedToItsDedicatedPlatform` in `Tests/Editor/Host/AnimatorServicesReactivationCharacterizationTests.cs` and `LifetimePluginIsConfinedToItsDedicatedPlatform` in `Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs`. Both reflect the fixture plugin's own attribute. Replace with a behavioral default-platform control run: arm the file's probe plugin, process an avatar without the dedicated platform, and assert the probe never runs. This kills a mutant the reflection version cannot see — a platform-pinned plugin that actually runs on the default platform. Keep the reflection version deleted.

### 2.8 Deliberate guards kept, 12

Kept as owned guards, with this design as their record:

- `ProductionEditorCodePersistsOnlyThroughTheNdmfAssetSaver` and `AlphaSeparationProductionSourceNamesNoSaveAsset` (source scans with anti-vacuity anchors).
- The six reflection-guard positive controls in `UnityRendererAlphaSnapshotTests.cs` (instrument checks for a production invariant).
- `SharedClass_ExposesExactlySixSemanticFacts` (six-fact contract guard).
- `ListPackagesReceivesIncludeIndirectTrue` (argument-forward pin). The coverage design adds the missing read-logic coverage beside it.
- `TheSceneHelperTracksAndDestroysAnArbitraryShaderMaterial` (harness hygiene).
- `ConsentSubjectForRefusedMultiMaterialNamesTheRefusal` (weak but load-bearing: the only test asserting a non-empty consent subject for a refused Multi material).

## 3. Naming and hygiene repairs

1. `Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs`: rename `AlphaMask_WithNaNScale_RefusesWithoutException` to `AlphaMask_WithNonFiniteTilingScale_RefusesWithoutException` and `AlphaMaskWithNonFiniteScaleOrOffsetRefusesWithoutException` to `AlphaMaskWithNonFiniteTilingScaleOrOffset_RefusesWithoutException`. They exercise mask tiling finiteness, not the `_AlphaMaskScale` scalar.
2. `Tests/Editor/Collection/RendererRefusalCalibrationTests.cs`: fix the misindented attribute on `UnprovenSlotMappingStillCountsTheMesh`.
3. `Tests/Editor/Census/AnonymizedRecordTests.cs`: remove the dead local `refusals` dictionary in `AReportCopiesItsCategoryDictionariesDefensively`.
4. `Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs`: replace the try/catch plus `Assert.Fail` wrapper in `ReplaceModeLayer_DiscardsTheMainChain` with direct assertions.

## 4. Testing strategy

This design changes tests only. The validation is:

1. Before any change: run the full EditMode suite. Record observed counts. Expected: 2847 passed, 0 failed, plus the environment-gated inconclusive set.
2. After each deletion: run the owning fixture. Expected: green with fewer tests, and the named replacement tests stay green.
3. For each rewrite: verify the new test fails against a temporary production mutation that mirrors the old blind spot, then restore production and show the test green. Record the mutant and the observed failure.
4. After the gate conversions: run `VendorReachabilityTests` in a clean checkout. Expected: five named ignores, no silent passes, watchdog still passes.
5. After all changes: run the full EditMode suite again. Expected: green, with eight fewer test methods and about eleven fewer runner cases (the published-chain rewrite shrinks five leaf rows to three and the blend-op fold merges two methods into one), six tests rewritten in place, plus five new named ignores.

## 5. Weak-spot scope statement

The investigations name about 25 further weak-power tests. They are not defects and this design does not repair them, except where a named task already touches the same fixture: the alpha mask renames in section 3, and the two mirror-oracle rewrites in section 2.3. The coverage design closes the audit's recorded coverage-shaped weak spots separately.

## 6. Stop conditions

Stop and return evidence if:

- A deletion target is not a strict subset of its named replacement. Then the subset proof is wrong and the method stays.
- A rewrite cannot be made to fail against its named mutant. Then the rewritten test needs redesign.
- The suite goes red for a reason this design does not name.

## 7. Adversarial review record

Three adversarial reviewers audited this design and its plan against the investigation, the fourteen scout reports, and the repository on 2026-10-11. Corrections applied in this revision:

- The `ConsumingOutput` helper stays. The five kept sibling cases and their fixture builders call it, so the original delete-both instruction would have broken compilation.
- `ConsentSubjectForRefusedMultiMaterialNamesTheRefusal` moved from deletion to the kept list. It is the only test asserting a non-empty consent subject for a refused Multi material, so deletion would lose a recorded kill.
- The deletion count is ten, and the kept-guard count is twelve; both were misstated earlier.
- The platform-confinement replacement now names the single production exported plugin; no capture plugin exists.
- A weak-spot scope statement records that the audit's remaining weak-power tests stay as recorded.
- The plan's replacement test name for diagnostic ordering was corrected to `MultipleFailingOutputs_GiveOneDiagnosticPerOutputInOutputOrder`, and the published-chain rewrite now uses the scout's boundary samples 252, 3, and 253 with fixed expected literals instead of a test-local re-derivation.

## 8. Strengthen-first revision, 2026-10-11

A second pass applied the strengthen-first policy: rewrite in place wherever a test guards something real. Changes:

- `FixtureShader_DeclaresEveryConversionReadProperty` and `FixtureShader_DeclaresAddBlendOpForNonDependencyTests` moved from deletion to production-loop rewrites (section 2.1).
- `DestroyingAnAbandonedCloneLeavesTheSourceIntact` keeps its deletion but now carries the recorded no-rewrite argument: it characterizes Unity's destroy semantics, and the production destroy-target failure mode is guarded by the Build sweep and window-close tests.
- Ledger: eight tests are deleted (three length canaries, the roster method, two engine pins, two wiring pins) and six are rewritten in place (two mirror-oracle tests, two fixture self-checks folded into one production-loop test plus preconditions, two fixture-metadata pins).
