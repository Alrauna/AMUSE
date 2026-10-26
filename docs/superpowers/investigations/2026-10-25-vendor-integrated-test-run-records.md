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

## Run of 2026-10-25, Task 6 validation, product assembly

Environment. The working tree of the dev project after Task 6. The run record block of Task 6 printed for the first time in full runs. The block dates itself in UTC. This run crossed UTC midnight, so the block prints 2026-10-26. The local work date of the run is 2026-10-25.

How the run was made. The callback recorder method of `.superpowers/sdd/full-run-recorder.md` ran the product assembly alone. The research assembly was not run, because Task 6 changes only the product assembly. The run delivered every row exactly once, with no double delivery. The temporary recorder and its data file were deleted after the run, and a forced refresh then reported zero compile errors. Two focused runs before it, one over the new record test class and one over the environment and gate test classes, printed one report block each. The console carried one report block per run.

The printed report block of the full run, copied from the console:

```text
=== AMUSE vendor run record ===
date: 2026-10-26
counts: discovered 3046, completed 3046, passed 3014, failed 30, skipped 2, inconclusive 0
durations: product 1557.4 s, research 0.0 s, sum 1557.4 s
opt-in gates set: none
gated fixtures excluded: Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests (AMUSE_DAO_INTEGRATION unset)
skip reasons:
  - Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.AaoAndDaoCombinedKeepsTheMergedSoundness: The opt-in variable AMUSE_DAO_INTEGRATION is not set to 1. Set AMUSE_DAO_INTEGRATION=1 in a disposable public integration project to run these tests. They delete project-level d4rkAvatarOptimizer output and must never run elsewhere.
  - Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.DaoMergeRunsInTheSdkSequenceAndPreservesSoundness: The opt-in variable AMUSE_DAO_INTEGRATION is not set to 1. Set AMUSE_DAO_INTEGRATION=1 in a disposable public integration project to run these tests. They delete project-level d4rkAvatarOptimizer output and must never run elsewhere.
effective environment:
  - at.pimaker.ltcgi locked 1.7.4, installed 1.7.4, Match
  - com.anatawa12.avatar-optimizer locked 1.9.20, installed 1.9.20, Match
  - com.poiyomi.thryeditor locked 2.74.3, installed 2.74.3, Match
  - com.poiyomi.toon locked 10.0.27, installed 10.0.27, Match
  - com.vrcfury.vrcfury locked 1.1434.0, installed 1.1434.0, Match
  - com.vrchat.avatars locked 3.10.5, installed 3.10.5, Match
  - com.vrchat.base locked 3.10.5, installed 3.10.5, Match
  - d4rkpl4y3r.d4rkavataroptimizer locked 4.6.0, installed 4.6.0, Match
  - jp.lilxyzw.liltoon locked 2.4.1, installed 2.4.1, Match
  - nadena.dev.modular-avatar locked 1.18.7, installed 1.18.7, Match
  - nadena.dev.ndmf locked 1.14.8, installed 1.14.8, Match
  - red.sim.lightvolumes locked 3.0.1, installed 3.0.1, Match
scripting defines: UNITY_POST_PROCESSING_STACK_V2;VRC_SDK_VRCSDK3;LTCGI_INCLUDED;VRC_LIGHT_VOLUMES;LTCGI_VRC_LIGHT_VOLUMES
changed setting lines:
  none
```

- Discovered leaf results: 3046.
- Completed leaf results: 3046.
- Passed: 3014.
- Failed: 30.
- Skipped: 2.
- Inconclusive: 0.
- Duration from the test framework bookends: 1559.3 seconds. The block prints 1557.4 seconds, measured from the first test to the suite teardown. The research assembly was not part of this run, so no sum applies beyond the product duration.

Count changes against the Task 4 run of this file. The leaf count grew by 12. Four tests came with the Task 5 commit and eight tests with Task 6. The failed set is exactly the 30 names of the Task 4 run. No new failure appeared. The two skips are the same gated DAO tests, now recorded with their gate reason by the block.

### Side-effect observation, QualitySettings

Before this run, on 2026-10-25, `ProjectSettings/QualitySettings.asset` showed `anisotropicTextures: 2` in every quality level and no working-tree change against the committed file. After the run the file was unchanged and still showed 2. The block of this run reports `changed setting lines: none` for `ProjectSettings/ProjectSettings.asset` and `ProjectSettings/EditorSettings.asset`, the two files the harness digests.

The earlier observation of 2026-10-25, a change from 2 to 1 after one run and a return to 2 after a later run, stayed unconfirmed by this measurement. A vendor editor load hook stays the likely writer and this run cannot rule it out. A load hook writes at editor load, before the first test, so the digest never sees its write. The harness records such writes and never prevents them. `QualitySettings.asset` sits outside the two digested files of the Task 6 interface, so a settings write to it never appears in a block's changed lines. No fixture asserts against the committed value of a vendor-written setting.
