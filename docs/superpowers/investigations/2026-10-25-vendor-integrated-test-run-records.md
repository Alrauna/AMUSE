# Vendor-integrated test run records

Privacy note. This record names no machine, account, port, editor instance, or private avatar. Test names and package names are product content. Every run in this file ran in the dev editor instance of this repository. Product tests never run in the private Census Lab project.

Later validation runs append one dated block per run below. Each block records what its own run observed.

## Run of 2026-10-25, stage 1 baseline

Environment. The working tree of the dev project on 2026-10-25. The locked vendor set is the one in `Packages/vpm-manifest.json`.

How the runs were made. One `run_tests` EditMode call over both test assemblies ran first. Its progress counter reported 3268 catalog nodes and 3171 completed results. Its job report caps the failure list at 25 entries and carries no count summary. Two callback runs, one per assembly, then recorded every leaf result. The counts and names below come from those callback runs. The catalog counter counts 97 more nodes than the runs report leaf results. The same gap shape appears in the 2026-10-24 baseline and in the 2026-10-11 audit record. No cause is claimed here.
A second `run_tests` call scoped to the product assembly completed 3020 results. It confirmed the assembly split of the first call.

### Product assembly run, `Alrauna.Amuse.Tests.Editor`

- Discovered leaf results: 3020.
- Completed leaf results: 3020.
- Passed: 2987.
- Failed: 31.
- Skipped: 0.
- Inconclusive: 2.
- Duration: 1598.7 seconds.

### Research assembly run, `Alrauna.Amuse.Research.Tests.Editor`

- Discovered leaf results: 151.
- Completed leaf results: 151.
- Passed: 151.
- Failed: 0.
- Skipped: 0.
- Inconclusive: 0.
- Duration: 7.1 seconds.

### Both assemblies together

- Passed: 3138.
- Failed: 31.
- Skipped: 0.
- Inconclusive: 2.
- Result total: 3171.
- Duration of the product run: 1598.7 seconds.
- Duration of the research run: 7.1 seconds.
- Sum of both durations: 1605.8 seconds.

The fresh counts match the 2026-10-24 baseline of the investigation record section 3.

### Inconclusive test names, 2 tests

Both reported inconclusive because the opt-in environment variable `AMUSE_DAO_INTEGRATION` was unset. The gate uses `Assume.That` in `DaoMergedConsumptionTests`. These two are also the gated tests that reported inconclusive, the input for design item V9 number 3. This attribution is measured by this run.

- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.AaoAndDaoCombinedKeepsTheMergedSoundness`
- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.DaoMergeRunsInTheSdkSequenceAndPreservesSoundness`

### Failed test names, 31 tests

All 31 failed tests sit in the product assembly. Eight sit in `AmusePlatformFinishPluginTests`. Twenty sit in `AaoMergedConsumptionTests`. Three are single failures. The research assembly holds none.

Platform identity, 8 tests:

- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.AnimatorServicesContextIsInactiveOnceTheAmusePassesHaveRun`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.AnUnoptedAvatarWithARefusingGraphStaysSilentButStoresTheRefusal`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.ASwitchedOffAvatarWithARefusingGraphStaysSilentButStoresTheRefusal`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.CapturePassRetainsTheHostsExactAnimatorBindings`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.PlatformFinishBarrierRunsAfterAnonymousOptimizingProducer`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.RealBuildReachesTheLifecycleGateWithoutTrippingTheInactivityInvariant`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.RetainedAnimatorBindingsRemainOperationalAfterDeactivation`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.TheBarrierStillExecutesAlongsideTheNewCapturePass`

Merged consumption fixture, 20 tests:

- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.AaoMergeCombinesRenderersAndPreservesSoundness`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.AaoTexturePackingTakesTheGeneratedRouteAndRefusesConservatively`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.AmuseComponentSurvivesUntilItsPlatformFinishPass`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.CutoutFamilyPresenceKeepsTheTransparentSlotOutcome`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.ManySlotMergedRendererKeepsMixedAlphaTrianglesOnTheirSlots(False)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.ManySlotMergedRendererKeepsMixedAlphaTrianglesOnTheirSlots(True)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MaterialAlternativeWithHigherCutoffStaysWithinItsOwnPredicate`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MaterialMaskDarkeningKeepsTheDarkenedTriangles`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MaterialSwapKeepsTheConservativeStateOutcome`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MergedCutoutSlotSharingAGeneratedAtlasKeepsTheTransparentPredicate`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MixedSlotRendererKeepsTransparentPredicateWhenCutoutSharesThePipeline(False,False)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MixedSlotRendererKeepsTransparentPredicateWhenCutoutSharesThePipeline(True,False)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MixedSlotRendererKeepsTransparentPredicateWhenCutoutSharesThePipeline(False,True)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MixedSlotRendererKeepsTransparentPredicateWhenCutoutSharesThePipeline(True,True)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MultiBoneRigMergesAndPreservesSoundness`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.PipelineWithoutOptimizerKeepsNonOpaquePolygonsOnTheirOriginalMaterials`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.SerializedAlphaSeparatorSwitchKeepsTheBuildUntouched`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.SerializedDisableControlKeepsTheBuildUntouched`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.SharedTextureAcrossRenderersKeepsTransparentPredicate`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.StreamingTextureRefusesItsRendererConservatively`

Single failures, 3 tests:

- `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests.MarkerClipRefusalIsSlotLocalAndWritesNothingForIt`
- `Alrauna.Amuse.Tests.Editor.Build.TransientUnlockSwapInTests.ABindingOnAnotherRendererPathDoesNotRefuseThisLockedMaterial`
- `Alrauna.Amuse.Tests.Editor.Build.TransientUnlockTransformationTests.ClosePassAddsNoAvatarEntryWhenTheBarrierAlreadyRefused`

## Run of 2026-10-25, Task 4 suite-start validation, product assembly

Environment. The working tree of the dev project after Task 4. The suite-start check ran for the first time in a full run. It passed. The console carried one dated record line that listed the effective environment. All 12 locked packages matched their installed versions. The `AMUSE_VRCSDK3_AVATARS` define was active.

How the run was made. The callback recorder method of `.superpowers/sdd/full-run-recorder.md` ran the product assembly alone. The research assembly was not run, because Task 4 changes only the product assembly. The run delivered every row exactly once. The temporary recorder and its data file were deleted after the run, and a forced refresh then reported zero compile errors.

- Discovered leaf results: 3034.
- Completed leaf results: 3034.
- Passed: 3002.
- Failed: 30.
- Skipped: 2.
- Inconclusive: 0.
- Duration: 1556.9 seconds. The research assembly was not part of this run, so no sum applies.

Count changes against the stage 1 baseline of this file. The leaf count grew by 14. One test came with the Task 2 commit, four tests with the Task 3 commit, and nine tests with Task 4. The two baseline inconclusive results now report as named skips, the Task 3 gate conversion. One baseline failure passed in this run: `TransientUnlockSwapInTests.ABindingOnAnotherRendererPathDoesNotRefuseThisLockedMaterial`. The Task 2 commit repaired that test after the baseline was recorded. No new failure appeared. Every failed name sits in the baseline list.

### Inconclusive test names

None.

### Skipped test names, 2 tests

Both are the gated opt-in skips of the Task 3 gate conversion. No gated test reported inconclusive in this run.

- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.AaoAndDaoCombinedKeepsTheMergedSoundness`
- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.DaoMergeRunsInTheSdkSequenceAndPreservesSoundness`

### Failed test names, 30 tests

Eight sit in `AmusePlatformFinishPluginTests`. Twenty sit in `AaoMergedConsumptionTests`. Two are single failures.

Platform identity, 8 tests:

- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.AnimatorServicesContextIsInactiveOnceTheAmusePassesHaveRun`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.AnUnoptedAvatarWithARefusingGraphStaysSilentButStoresTheRefusal`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.ASwitchedOffAvatarWithARefusingGraphStaysSilentButStoresTheRefusal`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.CapturePassRetainsTheHostsExactAnimatorBindings`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.PlatformFinishBarrierRunsAfterAnonymousOptimizingProducer`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.RealBuildReachesTheLifecycleGateWithoutTrippingTheInactivityInvariant`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.RetainedAnimatorBindingsRemainOperationalAfterDeactivation`
- `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.TheBarrierStillExecutesAlongsideTheNewCapturePass`

Merged consumption fixture, 20 tests:

- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.AaoMergeCombinesRenderersAndPreservesSoundness`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.AaoTexturePackingTakesTheGeneratedRouteAndRefusesConservatively`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.AmuseComponentSurvivesUntilItsPlatformFinishPass`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.CutoutFamilyPresenceKeepsTheTransparentSlotOutcome`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.ManySlotMergedRendererKeepsMixedAlphaTrianglesOnTheirSlots(False)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.ManySlotMergedRendererKeepsMixedAlphaTrianglesOnTheirSlots(True)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MaterialAlternativeWithHigherCutoffStaysWithinItsOwnPredicate`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MaterialMaskDarkeningKeepsTheDarkenedTriangles`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MaterialSwapKeepsTheConservativeStateOutcome`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MergedCutoutSlotSharingAGeneratedAtlasKeepsTheTransparentPredicate`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MixedSlotRendererKeepsTransparentPredicateWhenCutoutSharesThePipeline(False,False)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MixedSlotRendererKeepsTransparentPredicateWhenCutoutSharesThePipeline(True,False)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MixedSlotRendererKeepsTransparentPredicateWhenCutoutSharesThePipeline(False,True)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MixedSlotRendererKeepsTransparentPredicateWhenCutoutSharesThePipeline(True,True)`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.MultiBoneRigMergesAndPreservesSoundness`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.PipelineWithoutOptimizerKeepsNonOpaquePolygonsOnTheirOriginalMaterials`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.SerializedAlphaSeparatorSwitchKeepsTheBuildUntouched`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.SerializedDisableControlKeepsTheBuildUntouched`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.SharedTextureAcrossRenderersKeepsTransparentPredicate`
- `Alrauna.Amuse.Tests.Editor.Build.AaoMergedConsumptionTests.StreamingTextureRefusesItsRendererConservatively`

Single failures, 2 tests:

- `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests.MarkerClipRefusalIsSlotLocalAndWritesNothingForIt`
- `Alrauna.Amuse.Tests.Editor.Build.TransientUnlockTransformationTests.ClosePassAddsNoAvatarEntryWhenTheBarrierAlreadyRefused`
