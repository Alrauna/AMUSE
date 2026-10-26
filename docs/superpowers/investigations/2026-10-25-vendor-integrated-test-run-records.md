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

## Run of 2026-10-25, Task 6 fix round 1, product assembly

Environment. The working tree of the dev project after the Task 6 fix round. The block counts each delivered leaf once and measures the discovered count from the assembly node of the tree the runner loaded. The block dates itself in UTC and prints 2026-10-26. The local work date of the run is 2026-10-25.

How the run was made. The callback recorder method of `.superpowers/sdd/full-run-recorder.md` ran the product assembly alone. The run delivered every row exactly once. The temporary recorder and its data file were deleted after the run, and a forced refresh then reported zero compile errors.

The printed report block of the run, copied from the console:

```text
=== AMUSE vendor run record ===
date: 2026-10-26
counts: discovered 3049, completed 3049, passed 3017, failed 30, skipped 2, inconclusive 0
discovered basis: the leaf count of this assembly node in the test tree the runner loaded for the run. The catalog is filter blind, so a filtered run completes fewer leaves by selection, not by loss.
durations: product 1559.6 s, research not part of this run, sum 1559.6 s
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

- Discovered leaf results: 3049, measured from the assembly node of the loaded tree.
- Completed leaf results: 3049, counted once per delivered leaf.
- Passed: 3017.
- Failed: 30.
- Skipped: 2.
- Inconclusive: 0.
- Duration from the test framework bookends: 1561.6 seconds. The block prints 1559.6 seconds, measured from the first test to the suite teardown. The research assembly was not part of this run.

Count changes against the Task 6 validation run of this file. The leaf count grew by 3, the fix round tests. The failed set is exactly the 30 names of the Task 4 run. No new failure appeared. The two skips are the same gated DAO tests. The discovered count and the completed count agree for this full run, and the two numbers now come from independent sources: the loaded tree and the delivered leaf results.

Side effects. `ProjectSettings/QualitySettings.asset` stayed at `anisotropicTextures: 2` before and after this run, unchanged against the committed file. The block reports `changed setting lines: none`.

## Run of 2026-10-25, Task 7 validation, product assembly

Environment. The working tree of the dev project after Task 7. The platform identity split renamed the eight platform finish tests onto a dedicated platform, added the mirrored five-pass plugin, and added the eight vendor VRChat tier A twins with their gate. This run crossed UTC midnight, so a dated console line would print 2026-10-26. The local work date of the run is 2026-10-25.

How the run was made. The callback recorder method of `.superpowers/sdd/full-run-recorder.md` ran the product assembly alone. The run delivered every row exactly once: one RUN_STARTED bookend, one RUN_FINISHED bookend, and no double delivery. The footer counts are the authoritative summary. The temporary recorder and its data file were deleted after the run, and a forced refresh then reported zero compile errors. The refresh cleared the editor console, so the printed report block of Task 6 was not captured for this run. This block records the footer counts and the delivered leaf rows instead.

- Delivered leaf results: 3061, counted once per delivered leaf.
- Passed: 3037.
- Failed: 22.
- Skipped: 2.
- Inconclusive: 0.
- Duration from the test framework bookends: 1638.5 seconds. The research assembly was not part of this run.

Count changes against the Task 6 fix round run of this file. The leaf count grew from 3049 to 3061, the twelve new tests: the four platform provider audits and the eight tier A twins. The failed set shrank from 30 to 22. The eight platform identity failures of `AmusePlatformFinishPluginTests` are gone. The remaining 22 are exactly the 20 `AaoMergedConsumptionTests` failures of the missing FX layer defect, which Task 9 owns, plus the two single failures that Tasks 8 and 9 own. Every remaining name matches the Task 4 failed list one to one. No new failure appeared. The two skips are the same gated DAO tests.

Tier A evidence. The eight tier A twins delivered as passed, so the `VendorVRChatPlatform` category was present in the run. The suite-start check passed, including the new tier A category assertion with the SDK installed.

## Run of 2026-10-25, Task 8 validation, product assembly

Environment. The working tree of the dev project after Task 8. The event scan of the committed graph now reads NDMF registry provenance, so an authored event that a plugin clone dropped refuses the build again. This run crossed UTC midnight, so a dated console line would print 2026-10-26. The local work date of the run is 2026-10-25.

How the run was made. The callback recorder method of `.superpowers/sdd/full-run-recorder.md` ran the product assembly alone. The run delivered every row exactly once, with one RUN_STARTED bookend and one RUN_FINISHED bookend. The temporary recorder and its data file were deleted after the run, and a forced refresh then reported zero compile errors.

- Delivered leaf results: 3063, counted once per delivered leaf.
- Passed: 3040.
- Failed: 21.
- Skipped: 2.
- Inconclusive: 0.
- Duration from the test framework bookends: 1647.2 seconds. The research assembly was not part of this run.

Count changes against the Task 7 validation run of this file. The leaf count grew from 3061 to 3063, the two new provenance tests of `CommittedControllerGraphTests`. The failed set shrank from 22 to 21. `TransientUnlockTransformationTests.ClosePassAddsNoAvatarEntryWhenTheBarrierAlreadyRefused` passed in this run, the Task 8 target. The remaining 21 failed names are the 20 `AaoMergedConsumptionTests` failures of the missing FX layer defect plus `AlphaSeparationApplyTests.MarkerClipRefusalIsSlotLocalAndWritesNothingForIt`, both owned by Task 9. Every remaining name matches the Task 4 failed list one to one. No new failure appeared. The two skips are the same gated DAO tests.

Focused evidence of the same date. Before the fix, a filtered run over `CommittedControllerGraphTests` observed both new tests fail with `Expected: AnimationEventPresent But was: None`, while the identity-committed event test passed. After the fix, the same class passed 22 of 22, and a filtered run over `TransientUnlockTransformationTests` passed 16 of 16, including the barrier-refused close fixture.

## Run of 2026-10-26, Task 10 stage 1 exit validation, both assemblies

Environment. The working tree of the dev project after Task 9. The locked vendor set is the one in `Packages/vpm-manifest.json`. The local work date of the run is 2026-10-26. The block dates itself 2026-10-26 in UTC.

How the runs were made. The callback recorder method of `.superpowers/sdd/full-run-recorder.md` ran both test assemblies, one at a time. The product assembly ran first, then the research assembly. The forced refresh before the recorder timed out once during the reload and succeeded on the retry, as the method expects. The product run delivered every row exactly once. The research run delivered every row exactly twice, the known double-delivery quirk of the callback. Every research leaf was counted once by full name. The console carried one report block for the product run, read before any refresh and copied below. The temporary recorder and both data files were deleted after the runs, and a forced refresh then reported zero compile errors.

The printed report block of the product run, copied from the console:

```text
=== AMUSE vendor run record ===
date: 2026-10-26
counts: discovered 3063, completed 3063, passed 3061, failed 0, skipped 2, inconclusive 0
discovered basis: the leaf count of this assembly node in the test tree the runner loaded for the run. The catalog is filter blind, so a filtered run completes fewer leaves by selection, not by loss.
durations: product 1601.7 s, research not part of this run, sum 1601.7 s
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

### Product assembly run, `Alrauna.Amuse.Tests.Editor`

- Discovered leaf results: 3063, measured from the assembly node of the loaded tree by the block.
- Completed leaf results: 3063, counted once per delivered leaf. No double delivery.
- Passed: 3061.
- Failed: 0.
- Skipped: 2.
- Inconclusive: 0.
- Duration from the test framework bookends: 1603.7 seconds. The block prints 1601.7 seconds, measured from the first test to the suite teardown.

### Research assembly run, `Alrauna.Amuse.Research.Tests.Editor`

- Discovered leaf results: 151, the unique delivered leaf names.
- Completed leaf results: 151.
- Passed: 151.
- Failed: 0.
- Skipped: 0.
- Inconclusive: 0.
- Duration from the test framework bookends: 6.7 seconds. The callback delivered the whole run twice, four milliseconds apart, and both footer pairs agree on every count.

### Both assemblies together

- Result total: 3214.
- Passed: 3212.
- Failed: 0.
- Skipped: 2.
- Inconclusive: 0.
- Duration of the product run: 1603.7 seconds.
- Duration of the research run: 6.7 seconds.
- Sum of both durations: 1610.4 seconds.

Exit rule of stage 1. Both assemblies are green with a named skip list of two tests. The completed count equals the discovered count in both assemblies. For the product assembly the two numbers come from independent sources, the loaded tree and the delivered leaf results. For the research assembly both numbers come from the delivered leaf results, deduplicated by full name.

Count changes against the Task 8 run of this file. The product leaf count stayed at 3063. Task 9 repaired the 21 remaining failures and added no test. The failed count went from 21 to 0. The skip set is the same two gated DAO tests. The research counts match every earlier research entry of this file: 151 leaves, all passed.

### Skipped test names, 2 tests

Both are the gated opt-in skips of the Task 3 gate conversion. No opt-in gate was set for this run. The fixture was excluded by selection, and each skip carries its gate reason in the block above.

- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.AaoAndDaoCombinedKeepsTheMergedSoundness`
- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.DaoMergeRunsInTheSdkSequenceAndPreservesSoundness`

### Failed test names

None.

### Console error observation, one entry

The console held exactly one logged error across both runs, `ArgumentOutOfRangeException` with parameter name `propertyIndex`. Both assembly footers report zero failed tests, so no test failed on it. The entry carried no stack trace. No cause is claimed here.

### Side-effect observation, QualitySettings

Before the runs, on 2026-10-26, `ProjectSettings/QualitySettings.asset` carried a working-tree change against the committed file. After the runs the file matched the committed file again and showed `anisotropicTextures: 2` in every quality level. The block reports `changed setting lines: none` for the two digested files. This matches the Task 6 observation shape of a vendor editor load hook that writes at reload time, outside the digested window.

## Run of 2026-10-26, Task 13 validation, product assembly

Environment. The working tree of the dev project after Task 13, the AAO atlas admission without a version pin. The local work date of the run is 2026-10-26. The suite-start line of the run reported all 12 locked packages as Match and the `AMUSE_VRCSDK3_AVATARS` define active.

How the run was made. The callback recorder method of `.superpowers/sdd/full-run-recorder.md` ran the product assembly alone. The research assembly was not run, because Task 13 changes only product assembly files. The run delivered every leaf row exactly once, with no double delivery. The temporary recorder and its data file were deleted after the run, and a forced refresh then reported zero compile errors. The printed report block of the suite was not captured from the console, which collapsed the run's repeated pass logs. This block records the footer counts and the delivered leaf rows.

- Delivered leaf results: 3060, counted once per leaf row.
- Passed: 3058.
- Failed: 0.
- Skipped: 2.
- Inconclusive: 0.
- Duration from the test framework bookends: 1597.5 seconds. The research assembly was not part of this run.

Count changes against the Task 10 stage 1 exit run of this file. The leaf count went from 3063 to 3060. One test joined, the Task 13 ground check `RealAaoAtlasIsAdmittedThroughTheGeneratedRoute`. Four version-seam tests retired with the deleted seam, three from `AaoAtlasTextureAttestationTests` and one from `GeneratedTextureAttestationTests`. The failed count stayed 0 and the skip set stayed the same two gated DAO tests.

### Skipped test names, 2 tests

Both are the gated opt-in skips of the Task 3 gate conversion. No opt-in gate was set for this run.

- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.AaoAndDaoCombinedKeepsTheMergedSoundness`
- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.DaoMergeRunsInTheSdkSequenceAndPreservesSoundness`

### Failed test names

None.

Focused evidence of the same date. Before the seam deletion, a filtered run over the new ground check observed it fail with the admission refusal, `Expected: True But was: False`, because conjunct 3 refused the installed 1.9.20. After the deletion, a filtered run over the nine touched fixture classes passed 347 of 347, and a filtered run over the two witness classes passed 21 of 21, with the corroboration refusal and the forged foreign texture refusal both green.

### Console error observation of the stage 1 exit run, closed

The `propertyIndex` console exception of the stage 1 exit run closes as vendor noise. The installed lilToon asset postprocessor walks a near-miss shader name around material creation and throws inside Unity, and the fixture fences exactly that exception with `LogAssert.Expect` at `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:4907-4921`. The product walk cannot raise it, because the capture resolves the shader property index and records a no-index fact for `propertyIndex < 0` at `Editor/Host/UnityMaterialEvidenceCapture.cs:258-266`.

### Side-effect observation, QualitySettings

Before and after this run, on 2026-10-26, `ProjectSettings/QualitySettings.asset` matched the committed file and showed `anisotropicTextures: 2` in every quality level. The file stayed unchanged.

## Focused run of 2026-10-26, Task 14 validation, product assembly

Environment. The working tree of the dev project after Task 14, the real-source ground checks and the source-scan tripwire. The local work date of the run is 2026-10-26. The suite-start line of the run reported all 12 locked packages as Match and the `AMUSE_VRCSDK3_AVATARS` define active.

How the run was made. A filtered EditMode run over the two new fixture classes, `Tests/Editor/Semantics/RealSourceGroundCheckTests.cs` and `Tests/Editor/Build/VendorLockToolSourceBanTests.cs`. No full-suite run was made for Task 14. The run-record block of the suite printed a discovered leaf count of 3069 for the assembly node. The Task 13 full run delivered 3060 leaves. The nine new leaves of this task account for the difference.

- Completed leaf results: 9, by selection.
- Passed: 9.
- Failed: 0.
- Skipped: 0.
- Inconclusive: 0.
- Duration: about 9.1 seconds.

All nine passed on their first run. They are characterization of the pinned environment, as the task brief expected. The source-scan tripwire found no product Editor file that names the vendor lock tool qualified type name.

### lilToon digest-equality outcome, V10.1

Settled clean on 2026-10-26. The installed lilToon 2.4.1 shape attested through the production conjunction for every checked family. The families are the main shader with its pass asset, the cutout pair, the transparent pair, the outline opaque target through the canonical opaque routing, and both Multi containers. The pinned regular shader, pass, cutout, transparent, and outline digests reproduce on the installed 2.4.1 bytes of this project. The open question at `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:419-424` closes for this shape. The second settings shape named there stays outstanding, because this project holds one shape.

### Name-map observation of the same date

`Shader.Find` resolved the same object as the pinned GUID load for all nine lilToon family names on this install. Both Multi containers are included. The committed test stand-ins share the two container names, so the agreement is a property of the current name map and not a guarantee. The Poiyomi release name resolved the 10.x root shader and disagreed with the bundled 9.3 GUID load. Falsifier 10 pinned that disagreement.

### Real unattested 10.x answer

`RealPoiyomiTenMaterialAnswersUnattested` held. A material on the 10.x root shader selected into the Poiyomi family by its admitted release name. The source attestation refused it at the GUID gate of the admitted rows. The closed capture named the material unattested by batch position, and no member was admitted.
