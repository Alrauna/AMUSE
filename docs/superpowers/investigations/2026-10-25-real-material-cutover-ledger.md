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
| `LilToonCutoutSourceEligibilityTests.CanonicalDefaultStandIn_IsConvertible` | `CanonicalDefaultStandIn_IsConvertibleWithRealLilToon`. Fresh material on the GUID-loaded cutout shader, same eligibility assertion. Added in fix round 1 of 2026-10-26, when the review caught the row the first enumeration pass missed. | 2026-10-26 |

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
| `LilToonCutoutSourceEligibilityTests.ClipThresholdAtMaxProvableCutoff_IsConvertible` | Gate-matrix row planted at the exact 0.9999 bound. Exact scalar control. | 2026-10-26 |
| `LilToonCutoutSourceEligibilityTests.OpaqueEquivalentBlendState_IsConvertible` | Gate-matrix rows planted at the exact blend-equivalent scalars. Exact scalar control. | 2026-10-26 |
| `LilToonCutoutSourceEligibilityTests.UngatedProperties_DoNotAffectEligibility` | Gate-matrix rows planted at exact scalars to prove the deliberate absence of a gate. | 2026-10-26 |
| `LilToonCutoutSourceEligibilityTests.DepthTestLessWithPolicyConvertsAndFlagsDivergence` | Exact depth-test state plus the policy arm. | 2026-10-26 |
| `LilToonCutoutSourceEligibilityTests.DepthTestLEqualWithPolicyCarriesNoDivergence` | Exact depth-test state plus the policy arm. Matrix control a fresh material cannot carry. | 2026-10-26 |
| `LilToonTransparentSourceEligibilityTests.CutoffAtOrBelowOne_IsConvertible` | Gate-matrix row planted at the exact bound scalar. | 2026-10-26 |
| `LilToonTransparentSourceEligibilityTests.AlphaBoostFaAtOrAboveOne_IsConvertible` | Gate-matrix row planted at the exact bound scalar. | 2026-10-26 |
| `LilToonTransparentSourceEligibilityTests.SubpassCutoffAtOrBelowOne_IsConvertible` | Gate-matrix row planted at the exact bound scalar. | 2026-10-26 |
| `LilToonTransparentSourceEligibilityTests.DepthTestLessWithPolicyConvertsAndFlagsDivergence` | Exact depth-test state plus the policy arm. | 2026-10-26 |
| `LilToonTransparentSourceEligibilityTests.DeliberatelyUngatedProperty_StaysConvertible` | Gate-matrix rows planted at exact scalars to prove the deliberate absence of a gate. | 2026-10-26 |
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
| `AlphaSeparationPreparationTests.ByteChangedInRangeInstallSeparatesSilentlyThroughTheRealPipeline` | The embedded vendor package presents an approved, non-row version with stand-in digests: a byte-changed in-range install. No single installed package can supply that state. | 2026-10-26 |
| `AlphaSeparationPreparationTests.OutOfRangeInstallStillAsksThroughTheRealPipeline` | The embedded vendor package presents an out-of-range version. Version-range control no single installed package can supply. | 2026-10-26 |
| `AlphaSeparationDryRunTests.ApprovedVersionStaysUnknownInDryRun` | Byte-changed in-range install over the embedded vendor package. The dry run keeps strict attestation, and the fixture pins that conservative view. | 2026-10-26 |
| `AlphaSeparationDryRunTests.ApprovedAndOutOfRangeAvatarsRouteTheirNotes` | Approved and out-of-range embedded installs in one avatar. Version-range control. | 2026-10-26 |
| `AlphaSeparationDryRunTests.CacheHitServesTheApprovedNoteOnce` | Cache-note routing over the approved and out-of-range embedded installs. Version-range control. | 2026-10-26 |
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

## Stage 4: Poiyomi bundled 9.3 cutover, 2026-10-26

Status as of 2026-10-26. Stage 4 of the vendor-integrated test plan,
design decision V12. The installed package is Poiyomi 10.0.27. Its
bundled 9.3 shaders attest through production on this install, settled
by the Task 14 ground checks of 2026-10-26
(`Tests/Editor/Semantics/RealSourceGroundCheckTests.cs`). Owner
decision A3b of 2026-10-25 drops release package testing. The two
9.3.64 release rows stay in the product data and no test loads them,
because the release package is not installed. The locked stand-ins
(`LockedStandInOriginal`, `LockedStandInOriginalRich`, and every locked
row through `LockedSourceFixture`) never joined this enumeration. The
bundled rows carry `LockedOriginalAdmitted = false`, so no locked
original can attest in this environment. Stage 5 (V13) owns them.

### Enumeration method, stage 4

The Step 1 grep ran over `Tests/Editor/` for the stand-in shader names
under `Hidden/Alrauna/AmuseTests/` that carry the Poiyomi property
schema, for `VerifiedPoiyomiTestSeams`, and for the
`PoiyomiFixtureTestBase.Create*` and `InterpretVerified*` helpers, then
followed the fixture wrappers each file builds on them
(`SplitAlphaMaterial`, `SampledAlphaMaterial`,
`VerifiedForceOpaqueMaterial`). The sweep covered the Poiyomi Semantics
suites and the Poiyomi halves of Analysis, Build, DryRun, and Host. A
test joined the retirement list only when both stage 3 criteria hold:

1. The stand-in material's role is the vendor material itself on an
   admitted route, at vendor-default state or at a state a fresh
   installed material can carry.
2. The asserted outcome is the clean admitted result, with no planted
   refusal, junk state, or exact control the vendor surface cannot
   carry.

Falsifier-marked fixtures and refusal shapes never joined. Every twin
loads the bundled 9.3 shader by the production GUID pin
(`PoiyomiSourceAttestation.CanonicalShaderGuid`), never by name, and
drives the production path with no seam delegates. The conversion
eligibility over the real material uses the vendor Fade preset render
state and the vendor force-opaque flag, both states a fresh installed
material can carry. First-run green on every twin is characterization
of the pinned environment, in the shape of the Task 14 ground checks.

### Retired Poiyomi stand-in tests

| Old test | Replacement | Date |
|---|---|---|
| `AlphaSeparationPreparationTests.CandidateRendererProducesARetainedRecord` | `CandidateRendererProducesARetainedRecordWithRealPoiyomi`. Real bundled 9.3 material by GUID, production barrier with no seams, same retained-record assertions. | 2026-10-26 |
| `AlphaSeparationPreparationTests.PreparationDoesNotMutateTheBuildAvatar` | `PreparationDoesNotMutateTheBuildAvatarWithRealPoiyomi`. Real bundled 9.3 material, production barrier, same non-mutation assertions. | 2026-10-26 |
| `AlphaSeparationPreparationTests.RendererWithoutAnOpaqueCandidateRetainsNothing` | `RendererWithoutAnOpaqueCandidateRetainsNothingWithRealPoiyomi`. Real transparent material, production barrier, same no-candidate assertions. | 2026-10-26 |
| `AlphaSeparationPreparationTests.SharedSourceMaterialReusesOneAvatarWideClone` | `SharedSourceMaterialReusesOneAvatarWideCloneWithRealPoiyomi`. Real bundled 9.3 source shared by two slots, production barrier, same one-clone mapping assertions. | 2026-10-26 |
| `PoiyomiOpaqueConversionTests.ReadEffectiveRenderState_ReportsTheDeclaredQueueWithoutAnOverride` | `ReadEffectiveRenderState_ReportsTheDeclaredQueueWithoutAnOverrideWithRealPoiyomi`. Fresh material on the GUID-loaded bundled shader, same declared queue and RenderType assertions. The vendor's own declared SubShader tags answer, not the stand-in transcription. | 2026-10-26 |
| `AlphaSeparationApplyTests.WhollyOpaqueSlotReplacesOnlyItsMaterial` | `WhollyOpaqueSlotReplacesOnlyItsMaterialWithRealPoiyomi`. Real bundled 9.3 material by GUID through the apply suite's `ProductionCaptureScope`, same Falsifier 1 marker and same wholly-opaque apply assertions. Added in fix round 1 of 2026-10-26, when the review caught the row the first apply-file pass missed. | 2026-10-26 |

Fix round 1 re-scan of the apply suite, 2026-10-26. Every other row
that mints a Poiyomi stand-in material in `AlphaSeparationApplyTests`
was checked against the two criteria. Each plants an AMUSE-side arm or
builds its subject directly: animation curves and marker clips, property
blocks, reparenting, replacement and sweep mechanics, coverage policy
arms, hand-built prepared records, or the retained parity fixture. None
asserts the plain admitted apply result over the vendor material, so no
further row joined this table.

### Retained Poiyomi stand-ins with stated reasons

| Stand-in test or suite | Retention reason | Date |
|---|---|---|
| The interpreter equation suites of `Semantics/Poiyomi/` (`PoiyomiBaseColorAlphaTests`, `PoiyomiAlphaMaskTests`, `PoiyomiTwoPassAlphaTests`, `PoiyomiRimLightingAlphaTests`, `PoiyomiOutlineAlphaSemanticsTests`, `PoiyomiDecalSlotAlphaTests`, `PoiyomiCutoutSplitTests`, `PoiyomiMultipassRuleTests`, `PoiyomiTextureEvidenceTests`) and the characterization suites | They pin traced equations over exact property and feature states through the verified-material seam. Record section 5.3, analysis oracles with independent outcomes. | 2026-10-26 |
| `PoiyomiOpaqueConversionTests` gate, preset, tuple, and pin rows (`ConvertibleFadeBaseline_IsConvertible`, the `Preset*` rows, the gate matrices, the canonical-tuple pins) | Exact control over the 23 conversion-read properties through `MakeCanonical` and the `Preset` helper. The rows assemble vendor preset facts onto the canonical tuple, which is not fresh vendor state. Record section 5.3, exact scalar control. | 2026-10-26 |
| `PoiyomiOpaqueConversionTests.FixtureMaterial_ConvertsThroughProductionWithoutAMissingPropertyRefusal` | The row guards the stand-in fixture itself. The retained behavioral rows trust its precondition. Its subject is the fixture, not the vendor route. The retired barrier twins ground the installed shader's conversion schema end to end, because a missing conversion property would refuse their preparation assertions. | 2026-10-26 |
| The falsifier-marked rows (`AlphaSeparationPreparationTests.AlreadyOpaqueMapsToItselfWithoutAClone`, `NoSplitAnywhereCreatesNoMeshClone`, the conversion-only animation row, `MixedPoiyomiAndLilToonSlotMapsCompletely`, `PoiyomiSlotPreparesBesideAnOpaqueLilToonSlotOnTheSameRenderer`, `PoiyomiSiblingSlotsSelectDifferentPairs`, the premultiply and forced-opaque report falsifiers) | Falsifier fixtures never enter the retirement list. Their planted contrasts need the stand-in state the marker names. | 2026-10-26 |
| `AlphaSeparationPreparationTests.CutoutSlotPreparesBesideAPoiyomiSlotWithIndependentClones`, `MixedPoiyomiAndCutoutAdmittedSetMapsThroughEachOwnConversion`, `SwappedMaterialWithUnconvertedSiblingMapsToIdentityAndPrepares`, `AlphaSeparationApplyTests.TransparentSiblingKeepsPoiyomiAndCutoutResultsByteIdentical`, `AlphaSeparationPersistenceTests.SourceAssetsRemainUnchangedAfterSuccessfulFeatureBuild`, `AlphaSeparationDryRunTests.DryRunVerdictsMatchTheBuildOnAMixedRenderer` | These stage 3 deferral rows stay synthetic. One build carries a retained lilToon stand-in half or a controlled-grid split half that needs the seam route, and the asserted subject is family routing, cross-build parity, verdict parity, or the preservation audit, not the vendor route. A production no-seam run would refuse the retained stand-in half at family selection. | 2026-10-26 |
| `AlphaSeparationPreparationTests.PoiyomiNonIdentityStSlotProvesOpaqueAndMigrates`, `PoiyomiStAnimatedAtSerializedDefaultPrepares`, `PoiyomiNonSingletonStAnimationRefuses`, `PoiyomiNonIdentityStPreparationLeavesSourceAssetsUnchanged` | Controlled pixel grids and exact UV envelopes, plus animation singleton mechanics. Record section 5.3, controlled pixel grids. | 2026-10-26 |
| `AlphaSeparationPreparationTests.SharedSourceStillValidatesConversionPerRenderer` and the refusal rows of the Poiyomi suites | Planted refusals. Refusal shapes never enter the retirement list. | 2026-10-26 |
| The runtime-state, report, cache, and policy rows (`AdmittedMaterialStatesTests`, the `RuntimeStateProductionEntry_*` rows, `AmuseReportsSlotTests`, `AmusePlatformFinishPluginTests` report and pair rows, the dry-run sentence mechanics) | Their subjects are AMUSE state machines and report strings over controlled textures. The stand-in material is a vehicle, and the rows assert call counts or exact policy pairs. Record section 5.3, analysis oracles with independent outcomes. | 2026-10-26 |
| The Host capture and materialization rows (`UnityMaterialEvidenceCaptureTests`, `EffectiveMaterialMaterializationTests`, `RendererAlphaAnalysisIntegrationTests`) | Capture and materialization mechanics with exact block-entry control. Record section 5.3. | 2026-10-26 |
| `AlphaSeparationPreparationTests.TwoPass_ConversionRefusesByName` and the Two Pass equation suites | Production conversion never admits the Two Pass family, so no Two Pass happy path exists. The row is a refusal shape and the suites are equation oracles. The real Two Pass attestation is covered by the Task 14 ground checks. | 2026-10-26 |

### Stage 4 stand-in shader assets

No stand-in shader file was deleted. `PoiyomiSemanticTest.shader`
serves the retained equation suites, the fixture seams, and the locked
source fixture. `PoiyomiTwoPassSemanticTest.shader` serves the Two Pass
equation suites and the multipass rule tests. No dead helper was
created: the retired rows used the shared
`PoiyomiFixtureTestBase.Create*` helpers, which the retained rows keep.

### Stage 4 import and compile cost, 2026-10-26

The measurement ran one forced synchronous reimport of each bundled
9.3 shader file in the dev editor instance, through
`AssetDatabase.ImportAsset` with `ForceUpdate` and
`ForceSynchronousImport`. The shaders resolved inside the vendor's
bundled 9.3 folder by their shader names, never by a written GUID. The
wall time came from a stopwatch around each call, with the total
around the loop. This is recorded data and never a gate (V5, V12).
There is no threshold and no failure branch.

| Bundled shader file | Forced synchronous reimport wall time |
|---|---|
| `.poiyomi/Old Versions/9.3/Poiyomi Toon` | 1492 ms |
| `.poiyomi/Old Versions/9.3/Poiyomi Toon Two Pass` | 1696 ms |
| Total, both files | 3188 ms |

The same numbers are dated in the run-record file. No vendor asset and
no import setting changed. The reimport writes only the editor's own
import artifacts.
