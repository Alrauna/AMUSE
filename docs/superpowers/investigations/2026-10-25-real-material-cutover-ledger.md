# Real-material cutover ledger, lilToon stand-in happy paths

Status as of 2026-10-26. Stage 3 of the vendor-integrated test plan, design
decision V11. The installed lilToon package is 2.4.1 and every checked family
attests through production on this install, settled by the Task 14 ground
checks of 2026-10-26 (`Tests/Editor/Semantics/RealSourceGroundCheckTests.cs`).

## Rule

Each retired stand-in test names its replacement test. Every other stand-in
test stays synthetic with a stated reason. The reasons cite the
test-double inventory of the rework investigation, sections 5.2 and 5.3
(`docs/superpowers/investigations/2026-10-25-vendor-integrated-test-rework-investigation.md`).
Falsifier fixtures and refusal shapes are not happy paths, so they never
enter the retirement list. Nothing here weakens a test.

## Enumeration method

The Step 1 grep ran over `Tests/Editor/` for the stand-in shader names under
`Hidden/Alrauna/AmuseTests/` and for `VerifiedLilToonTestSeams`, then followed
the fixture helpers each file wraps them in (`NewCutoutMaterial`,
`NewComputeFixture`, `NewCutoutFixtureMaterial`,
`LilToonFixtureTestBase.Create*`, the file-local selector and capturer
wrappers). The sweep covered 26 files in Build, DryRun, Host, and Semantics.
A test joined the retirement list only when both hold:

1. The stand-in material's role is the vendor material itself on an admitted
   route, at vendor-default state or at a state a fresh installed material
   can carry.
2. The asserted outcome is the clean admitted result, with no planted
   refusal, junk state, or exact control the vendor surface cannot carry.

Every twin loads its shader by the production GUID pin
(`LilToonSourceAttestation.CutoutShaderGuid`, `TransparentShaderGuid`,
`SupportedShaderGuid`), never by name, and drives the production path with no
seam delegates. First-run green on every twin is characterization of the
pinned environment, in the shape of the Task 14 ground checks.

## Retired stand-in tests

| Old test | Replacement | Date |
|---|---|---|
| `LilToonOpaqueTargetTests.PreparedClone_SwapsTheShaderToTheAttestedTarget` | `PreparedClone_SwapsTheShaderToTheAttestedTargetWithRealLilToon`. Real cutout source by GUID, real main-shader target by GUID, same swap, read-back, and source-preservation assertions. | 2026-10-26 |
| `LilToonOpaqueTargetTests.PreparedClone_IsNotPersisted` | `PreparedClone_IsNotPersistedWithRealLilToon`. Real cutout source and real main-shader target, same no-persistence assertion. | 2026-10-26 |
| `LilToonTransparentSourceEligibilityTests.FreshTransparentStandIn_IsConvertible` | `FreshTransparentStandIn_IsConvertibleWithRealLilToon`. Fresh material on the GUID-loaded transparent shader, same eligibility assertion. | 2026-10-26 |
| `AlphaSeparationPreparationTests.CutoutFixtureSlotPreparesAndCarriesTheOpaqueMapping` | `CutoutFixtureSlotPreparesAndCarriesTheOpaqueMappingWithRealLilToon`. Real cutout material, production barrier with no seams, same preparation and mapping assertions. | 2026-10-26 |
| `AlphaSeparationPreparationTests.TransparentFixtureSlotPreparesAndCarriesTheOpaqueCloneContract` | `TransparentFixtureSlotPreparesAndCarriesTheOpaqueCloneContractWithRealLilToon`. Real transparent material and production barrier for part (a), with the installed main-shaper target read-back. Part (b), the missing-recipe-property target, rides along unchanged: it mints its own refusal-shape shader and no vendor ships it. | 2026-10-26 |
| `AlphaSeparationPreparationTests.CutoutSharedSourceReusesOneAvatarWideClone` | `CutoutSharedSourceReusesOneAvatarWideCloneWithRealLilToon`. Real cutout source shared by two renderers in both orders, production barrier, same one-clone mapping assertions. | 2026-10-26 |
| `AlphaSeparationApplyTests.CutoutSwapToAttestedOpaqueMapsToItselfWithoutAClone` | `CutoutSwapToAttestedOpaqueMapsToItselfWithoutACloneWithRealLilToon`. Real cutout slot and real plain lilToon swap value, apply lifecycle through the production capture arm (`ProductionCaptureScope`), same map-to-self assertions. | 2026-10-26 |
| `AlphaSeparationDryRunTests.CutoutSplitSlotProducesSplittableSentence` | `CutoutSplitSlotProducesSplittableSentenceWithRealLilToon`. Real cutout material, production family map and plain capturer, same sentence. | 2026-10-26 |
| `AlphaSeparationDryRunTests.UnknownStaysUntouchable` | `UnknownStaysUntouchableWithRealLilToon`. Real cutout material over a fully transparent texture, same unproven row. | 2026-10-26 |
| `AlphaSeparationDryRunTests.CutoutSplitSlotAllProvenProducesConvertableSentence` | `CutoutSplitSlotAllProvenProducesConvertableSentenceWithRealLilToon`. Real cutout material over a fully opaque texture, same convertable row. | 2026-10-26 |
| `AlphaSeparationDryRunTests.FoldSlotProducesConvertableSentence` | `FoldSlotProducesConvertableSentenceWithRealLilToon`. Real cutout material under the same cutout preserve pair, same folded row. | 2026-10-26 |
| `AlphaSeparationDryRunTests.OpaqueFamilySlotShowsUntouchable` | `OpaqueFamilySlotShowsUntouchableWithRealLilToon`. Real plain lilToon material, same untouchable row. The dead stand-in helper `NewOpaqueMaterial` was removed with it. | 2026-10-26 |

## Retained stand-in tests with stated reasons

| Stand-in test | Retention reason | Date |
|---|---|---|
| `LilToonOpaqueTargetTests.PreparedClone_RewritesEveryScrambledFactToCanonical` | Exact junk-value control across all eighteen recipe facts. The assertion targets the recipe's writes over planted non-vendor state, so a fresh installed material cannot supply the input. | 2026-10-26 |
| `LilToonOpaqueTargetTests.PrepareCanonicalOpaqueClone_RegularCutoutConversion_DisablesAllModeKeywords` | Exact keyword-set control. The fixture enables four keywords no fresh vendor material carries. Record section 5.3, keyword states the caller must control exactly. | 2026-10-26 |
| `LilToonOpaqueTargetTests.PrepareCanonicalOpaqueClone_PreservesFeatureKeywords` | Exact keyword-set control, same reason as the row above. | 2026-10-26 |
| `LilToonOpaqueTargetTests.PrepareCanonicalOpaqueClone_WithTargetAttestationSeam_Succeeds` | The test verifies the seam itself. It overrides the target verifier, so its subject is the seam plumbing, not the vendor route. | 2026-10-26 |
| `LilToonTransparentSourceEligibilityTests.TransparentBucketQueueOffsets_AreConvertible` | Authored dress render queues 3000 to 3099 are exact queue control on the eligibility matrix. The fresh-defaults admission now runs on the real twin of the retired fresh-material row. | 2026-10-26 |
| `LilToonTransparentSourceEligibilityTests.TransparentDstBlendDefault_IsAdmittedByGateNine` | Gate-matrix row with exact scalar control. The vendor-default admission it names is covered end to end by the real twins of the preparation and dry-run rows. | 2026-10-26 |
| `LilToonTransparentSourceEligibilityTests.DepthTestLEqualWithPolicyCarriesNoDivergence` | Exact depth-test state plus the policy arm. Matrix control a fresh material cannot carry. | 2026-10-26 |
| `AlphaSeparationPreparationTests.BothSwitchesOnReproducesBasePreparation` | Policy-arm comparison whose base arm plants a below-minimum coverage refusal as a fixture precondition. Not a clean happy path, and the switch policy is AMUSE state, not vendor input. | 2026-10-26 |
| `AlphaSeparationPreparationTests.GeneratedSubAssetTexture_IsAdmittedAndProvenOpaqueEndToEnd` | The subject is the generated sub-asset texture's identity, a controlled texture grid. Record section 5.3, controlled pixel grids. The material only carries the texture. | 2026-10-26 |
| `AlphaSeparationPreparationTests.StreamingMipmapProvesThroughTheReadableClone` | Streaming decode mechanics over a controlled mipmap grid. Record section 5.3, controlled pixel grids. | 2026-10-26 |
| `AlphaSeparationPreparationTests.AlphaOpaqueBoundScalesTheProofEndToEnd` | Policy-bound scaling of the proof. The asserted rule is the policy arm, and the fixture plants exact coverage bounds. | 2026-10-26 |
| `AlphaSeparationPreparationTests.ModeOneMultiMaterialReachesResolutionAndRefusesClosedUntilRowsLand` | Multi container stand-ins cannot pass the pinned Multi attestation rows, and the fixture needs exact mode scalars. Record sections 5.2 injected rows and 5.3 exact keyword and mode control. | 2026-10-26 |
| `AlphaSeparationPreparationTests.ModeZeroMultiMaterialReachesResolutionAndRefusesClosedUntilRowsLand` | Same reason as the mode-one row. | 2026-10-26 |
| `AlphaSeparationPreparationTests.ByteChangedInRangeMultiKeepsItsRowsAndNeverSeparates` | Byte-changed in-range Multi control. No single installed package can supply a byte-changed install. | 2026-10-26 |
| `AlphaSeparationPreparationTests.ModeTwoMultiMaterialWithDistanceFadeStrengthRetainsThroughDelegation` | Exact distance-fade strength plus the Multi mode control. | 2026-10-26 |
| `AlphaSeparationPreparationTests.MixedPoiyomiAndLilToonSlotMapsCompletely` | The mixed fixture's Poiyomi half rides the same single build. Stage 4 (V12) owns the Poiyomi stand-ins. | 2026-10-26 |
| `AlphaSeparationPreparationTests.PoiyomiSlotPreparesBesideAnOpaqueLilToonSlotOnTheSameRenderer` | Stage 4 (V12) owns the Poiyomi stand-in half of the mixed renderer. | 2026-10-26 |
| `AlphaSeparationPreparationTests.CutoutSlotPreparesBesideAPoiyomiSlotWithIndependentClones` | Stage 4 (V12) owns the Poiyomi stand-in half of the mixed renderer. | 2026-10-26 |
| `AlphaSeparationPreparationTests.MixedPoiyomiAndCutoutAdmittedSetMapsThroughEachOwnConversion` | Stage 4 (V12) owns the Poiyomi stand-in half of the admitted set. | 2026-10-26 |
| `AlphaSeparationApplyTests.TransparentSiblingKeepsPoiyomiAndCutoutResultsByteIdentical` | Cross-build byte parity needs the identical Poiyomi and cutout fixtures in both builds. Stage 4 (V12) owns the Poiyomi half. | 2026-10-26 |
| `AlphaSeparationPersistenceTests.SourceAssetsRemainUnchangedAfterSuccessfulFeatureBuild` | One build carries the Poiyomi opaque pair, the split material, and the transparent extension. Stage 4 (V12) owns the Poiyomi and split stand-ins. | 2026-10-26 |
| `AlphaSeparationDryRunTests.DryRunVerdictsMatchTheBuildOnAMixedRenderer` | The mixed renderer's slots are Poiyomi split stand-ins. Stage 4 (V12). | 2026-10-26 |
| `AlphaSeparationDryRunTests` approved-range rows (`ApprovedRangeInstallAdmitsThroughTheVersionKeyedArm` and siblings) | The embedded vendor package presents an approved, non-row version with stand-in digests: a byte-changed in-range install. No single installed package can supply that state. | 2026-10-26 |
| `Host/UnityAlphaFieldEvidenceTests.AdmittedAtlasCapturesWithCorroboratedOriginMaterial` | The subject is the AAO atlas admission over a controlled pixel grid. The origin material is a registry token for the corroboration gate. Record section 5.3, controlled pixel grids. The real AAO atlas ground check of Task 13 covers the installed route. | 2026-10-26 |
| `Host/UnityAlphaFieldEvidenceTests.StreamingFlagOnAdmittedAtlasStillTakesTheGeneratedRoute` | Same controlled-grid subject and registry token reason. | 2026-10-26 |
| `Host/UnityMaterialEvidenceCaptureTests` capture-mechanics rows (`SharedCutoutTexture*`, `ChangingTheCutoutCapLeavesTheExactArmsChainUnchanged`, `RegisteredCloneOverAdmittedAtlas_ProvesOpaqueAtItsOwnCutoff`, `UnregisteredCloneOverAdmittedAtlas_StaysUnknown`, `MenuMembershipRecordsPerMaterial`) | Exact cutoff scalars and controlled isolation grids pin the capture merge rules. Record section 5.3, controlled pixel grids. | 2026-10-26 |

## Stand-in suites outside the retirement list

These never joined the list, because falsifier fixtures, refusal shapes, and
exact-control matrices are not happy paths. They stay as they are:

- The interpreter equation suites (`LilToonAlphaTests`,
  `LilToonCutoutAlphaTests`, `LilToonTransparentAlphaTests`,
  `LilToonAlphaMaskSemanticsTests`, `LilToonLayerAlphaTermTests`) and the
  characterization suites. They pin traced equations over exact property and
  feature states through the verified-material seam. Record section 5.3,
  analysis oracles with independent outcomes.
- The eligibility refusal matrices of
  `LilToonCutoutSourceEligibilityTests` and
  `LilToonTransparentSourceEligibilityTests`. Planted refusals and exact
  scalar states.
- The refusal-shape shader blocks no vendor ships
  (`LilToonOpaqueTargetTests` temp targets,
  `LilToonCutoutSourceEligibilityTests.MaterialWithoutTheConversionSchema_RefusesAsAbsent`).
  Record section 5.3, refusal-shape shader blocks.
- The exact keyword control of `LilToonFixtureTestBase` lines 138 to 302 and
  every Multi container and mode-gate fixture that mints through it.
  Record section 5.3, keyword states the caller must control exactly.
- The attestation pin suites over synthetic include trees
  (`LilToonAttestationTests` rows that use `AttestationEnvironment`).
  Record section 5.3, synthetic include trees.
- The falsifier-marked fixtures of the dedicated-platform suites, with their
  survival verdicts recorded in the plan's Task 12 table of 2026-10-26.
- The dry-run policy, cache, cancel, and banner mechanics. Their subjects are
  AMUSE state machines, and most assert the counting selector and capturer
  call counts, which need the file-local counting wrappers.
- The Poiyomi and locked-source stand-ins (`PoiyomiSemanticTest`,
  `PoiyomiTwoPassSemanticTest`, `LockedStandInOriginal`,
  `LockedStandInOriginalRich`). Stage 4 (V12) and stage 5 (V13) own them.
- The unattested-shader payload stand-in of
  `UnityAnimationEvidenceCaptureTests`. It carries a supported name over
  non-vendor bytes for binding-shape falsifiers, the exact refusal direction
  it exists to plant.

## Stand-in shader assets after the cutover

No stand-in shader file was deleted. Every checked-in lilToon stand-in
remains in use by retained suites or by the fixture seams themselves:
`LilToonSemanticTest.shader` (fixture selection, apply swap fixtures),
`LilToonCutoutConversionTest.shader` (retained matrices, seams, approved-range
scope), `LilToonTransparentConversionTest.shader` (retained matrices),
`LilToonOpaqueConversionTest.shader` (retained keyword rows, seam conversion
target), `LilToonMultiContainerTest.shader`,
`LilToonMultiOutlineContainerTest.shader`, and
`LilToonMultiModeGateTest.shader` (retained Multi fixtures). The one dead
helper the retirement created, `NewOpaqueMaterial` of the dry-run suite, was
deleted.
