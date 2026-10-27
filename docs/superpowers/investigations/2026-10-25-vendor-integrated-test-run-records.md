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

## Run of 2026-10-26, Task 15 validation, product assembly

Environment. The working tree of the dev project after Task 15, the lilToon stand-in cutover of stage 3. The local work date of the run is 2026-10-26.

How the run was made. The callback recorder method of `.superpowers/sdd/full-run-recorder.md` ran the product assembly alone. The research assembly was not run, because Task 15 changes only product assembly files. The run delivered every leaf row exactly once, with no double delivery. The temporary recorder and its data file were deleted after the run, and a forced refresh then reported zero compile errors.

- Delivered leaf results: 3069, counted once per leaf row.
- Passed: 3067.
- Failed: 0.
- Skipped: 2.
- Inconclusive: 0.
- Duration from the test framework bookends: 1765.4 seconds.

Count changes against the Task 13 full run of this file. The leaf count went from 3060 to 3069. The nine Task 14 leaves carry the count to 3069 before the cutover. Task 15 retired twelve stand-in happy paths and added twelve real-material twins, one for one, so the cutover itself changed no leaf count. Every retired test is named in the cutover ledger with its replacement. The failed count stayed 0 and the skip set stayed the same two gated DAO tests.

### Skipped test names, 2 tests

Both are the gated opt-in skips of the Task 3 gate conversion. No opt-in gate was set for this run.

- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.AaoAndDaoCombinedKeepsTheMergedSoundness`
- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.DaoMergeRunsInTheSdkSequenceAndPreservesSoundness`

### Failed test names

None.

Cutover observation of the same date. All twelve twins passed on their first run, in the focused runs and in this full run. They are characterization of the pinned environment. The focused runs came first: the two touched Semantics classes passed 74 of 74, and the three touched Build and DryRun classes passed 199 of 199, zero failed in each. Every twin loads its shader by the production GUID pin and drives the production delegates with no seams. The production barrier presenter fails the build on any consent subject, and no subject appeared in any twin build.

### Side-effect observation, QualitySettings

The file showed a working-tree change during the run window and matched the committed file again after it, with `anisotropicTextures: 2` in every quality level. This is the known vendor editor reload hook shape recorded by the Task 6 and Task 10 runs.

## Run of 2026-10-26, Task 16 validation, product assembly

Environment. The working tree of the dev project after Task 16, the
Poiyomi bundled 9.3 stand-in cutover of stage 4. The local work date of
the run is 2026-10-26.

How the run was made. The callback recorder method of
`.superpowers/sdd/full-run-recorder.md` ran the product assembly alone.
The research assembly was not run, because Task 16 changes only product
assembly files. The run delivered every leaf row exactly once, with no
double delivery. The temporary recorder and its data file were deleted
after the run, and a forced refresh then reported zero compile errors.

- Delivered leaf results: 3069, counted once per leaf row.
- Passed: 3067.
- Failed: 0.
- Skipped: 2.
- Inconclusive: 0.
- Duration from the test framework bookends: 1610.0 seconds.

Count changes against the Task 15 full run of this file. The leaf count
stayed 3069. Task 16 retired five Poiyomi stand-in happy paths and added
five real-material twins, one for one, so the cutover itself changed no
leaf count. Every retired test is named in the cutover ledger with its
replacement. The failed count stayed 0 and the skip set stayed the same
two gated DAO tests.

### Skipped test names, 2 tests

Both are the gated opt-in skips of the Task 3 gate conversion. No
opt-in gate was set for this run.

- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.AaoAndDaoCombinedKeepsTheMergedSoundness`
- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.DaoMergeRunsInTheSdkSequenceAndPreservesSoundness`

### Failed test names

None.

Cutover observation of the same date. All five twins passed on their
first run. They are characterization of the pinned environment. An
earlier unrecorded full-assembly run of the same working tree, started
before the recorded run, also completed with zero failures over its
whole discovered set, and both touched classes passed inside it. The
touched classes in the recorded run passed as follows:
`AlphaSeparationPreparationTests` 93 of 93 leaves and
`PoiyomiOpaqueConversionTests` 115 of 115 leaves, zero failed in each.
Every twin loads the bundled 9.3 shader by the production GUID pin and
drives the production delegates with no seams. The production barrier
presenter fails the build on any consent subject, and no subject
appeared in any twin build.

### Poiyomi import and compile cost, 2026-10-26

Recorded data, never a gate (V5, V12). One forced synchronous reimport
per bundled 9.3 shader file, through `AssetDatabase.ImportAsset` with
`ForceUpdate` and `ForceSynchronousImport`, timed with a stopwatch in
the dev editor instance after the test run. The bundled main shader
took 1492 ms. The bundled two-pass shader took 1696 ms. The total wall
time for both files was 3188 ms. No vendor asset and no import setting
changed.

### Side-effect observation, QualitySettings

The file matched the committed file after this run. No working-tree
change appeared in the run window, unlike the transient changes seen by
the Task 6 and Task 10 runs.

### V15 finding of the Task 17 first run, 2026-10-26

The first class run of `VendorProductionOrderTests` in the dev editor
instance observed the V15 condition of the stage 5 plan. Each tier A
fixture builds through the SDK callback dispatcher, and the dispatcher
runs the installed VRCFury callback on every avatar. VRCFury saves the
controllers it rebuilds under `Packages/com.vrcfury.temp/Builds`, so a
tier A fixture writes project-level state beyond the ambient NDMF
generated output. Both tier A fixtures therefore moved behind
`VendorOptInGate.Require(VendorOptInGate.ProductionOrderDispatch)`
with a disclosure that names the disposable-project requirement. The
class teardown deletes `Packages/com.vrcfury.temp/Builds`. No other
project-level write appeared. The full-suite run records of this file
must list this gate among the opt-in gates that were set whenever tier
A results are part of a recorded run.

## Run of 2026-10-26, Task 18 validation, product assembly

Environment. The working tree of the dev project after Task 18, the
real Thry lock characterization and the stand-in cutover of stage 5.
The local work date of the run is 2026-10-26. The suite-start line of
the runs reported all 12 locked packages as Match, including
`com.poiyomi.thryeditor` locked 2.74.3 installed 2.74.3, and the
`AMUSE_VRCSDK3_AVATARS` define active.

How the runs were made. Filtered EditMode runs in the dev editor
instance. No full-suite run was made for Task 18. Before the first run
the vendor cache folder `Assets/_LockedShaderCache` did not exist, and
`git status` over `Assets/` was clean.

### Cleanup-order falsifier, RED then GREEN

The first run of `RealThryLockCharacterizationTests` used the named
wrong implementation, a planner that lists cache entries before
materials. The run completed 3 leaves. The falsifier
`ResidueCleanupPlanDeletesMaterialsBeforeCacheEntries` failed with
"Expected: less than 0, But was: 4", which names the cache-first order.
The apply binding test passed against the same wrong plan, because it
binds the deleter to whatever plan the planner returns. After the
minimal fix the same filter completed 3 leaves, passed 2, skipped 1,
failed 0. The skip was the gated real lock test with its named
opt-in message.

### Gated real lock characterization, 2026-10-26

The opt-in variable `AMUSE_REAL_THRY_LOCK` was set to 1 in the dev
editor process for one run and removed after it. The run completed 3
leaves, passed 3, failed 0, skipped 0, in about 10.6 seconds of test
time.

- The binding guard held. The assembly-qualified resolution reached
  the installed vendor assembly named `ThryAssemblyDefinition`, never
  the test stand-in.
- The real lock processed the synthetic probe material. The material
  took a locked shader under the vendor cache root, and the lock flag
  set to one.
- The production aligner aligned the real lock output in the same era
  as the emulated fixture pair. Both aligned clean, with no refusal.
- The named vendor delta of the same date: the real lock bakes every
  non-animated property into a constant, which the aligner reads as
  the Baked state, while the emulated fixture partner keeps every
  property live. The first gated run observed that difference at the
  property `_Color`. The comparison accepts exactly Live-to-Baked and
  calls any other state change a named difference. After the
  comparison was written to that rule, the run held. A vendor change
  that renames or drops a live property still fails the test.
- No rename appeared on the probe, which carries no animated tags.
- The real unlock restored the original shader and cleared the flag.
  The vendor kept its cache entry in place, as its cache design
  states. The teardown then deleted the test's own material and
  shader assets first and the test's own cache entries after them.
  After the run the vendor cache folder held only the vendor-created
  root with its README file, which the repository gitignore already
  ignores. No test residue stayed.
- The gated run made the vendor usage-counter network call by the
  disclosed design of the opt-in. No other vendor side effect appeared.

### Stand-in cutover, 2026-10-26

After the characterization existed, the stand-in type file and its
meta were deleted, and the five counter-reading classes lost 15 counter
assertions and 3 stand-in reset calls. The final filtered run covered
the five touched classes and `VendorLockToolSourceBanTests`.

- Completed: 166.
- Passed: 165.
- Failed: 0.
- Skipped: 1, the gated real lock test, because no opt-in gate was set
  for this run (V15).
- Inconclusive: 0.
- Duration: about 187.7 seconds of test time.

The source-scan tripwire passed. Production protection now rests on
the tripwire alone, because no stand-in type exists.

### Task 18 fix round 1, 2026-10-26

The review asked for a bake value proof, a teardown gone assert, and an
armed cache entry cleanup. All three landed in
`RealThryLockCharacterizationTests.cs`. The gated run set
`AMUSE_REAL_THRY_LOCK=1` in the editor process and removed it after.

- RED of the value proof. The assert ran against a deliberately wrong
  expected value and failed with "Expected: 0.5f, But was: 0.75f",
  which names the recorded literal 0.75 against the wrong expectation.
- GREEN gated run. Completed 3, passed 3, failed 0, about 11.5 seconds
  of test time. The proof parses the recorded float literal back to
  0.75 exactly and the recorded color literal back to the set linear
  color within 1e-5 per component.
- Ungated run after the gate was removed. Completed 3, passed 2,
  skipped 1, failed 0. The teardown asserts every planned path gone,
  and the cache folder held only the vendor created root with its
  README file.
- The armed cleanup binding caught a real defect during the round: the
  scan returned Windows separators while the exact entry path used
  forward ones. The scan normalizes separators now.

Two environment facts of the same date, recorded for the owner:

- The probe needed an HLSL program declaration for its float property.
  A property the program never declares is an undeclared identifier at
  its use. The probe carries `float _ColorThemeIndex;` now.
- The real vendor lock bakes a property use inside a local variable
  declaration initializer, for example `fixed4 color = tex2D(...) *
  _Color;` becomes a float4 literal there. The production aligner
  refuses that shape, because a declaration line keeps property
  identifiers (fact 4). The probe keeps its baked uses on the return
  line, which aligns clean. The aligner difference on declaration
  line initializer bakes stands unfixed in this task and is a record
  for an owner decision, because real Poiyomi programs declare locals
  with property initializers.

## Run of 2026-10-26, Task 19 canonical validation, both assemblies

Environment. The working tree of the dev project after Task 18, with the
Task 19 document edits in place. The locked vendor set is the one in
`Packages/vpm-manifest.json`. The local work date of the run is
2026-10-26. The block dates itself 2026-10-26 in UTC. The suite-start
line reported all 12 locked packages as Match and the
`AMUSE_VRCSDK3_AVATARS` define active.

How the runs were made. The callback recorder method of
`.superpowers/sdd/full-run-recorder.md` ran both test assemblies, one at
a time. The product assembly ran first, then the research assembly. Each
forced refresh timed out once during the reload and succeeded on the
retry, as the method expects. The product run delivered every row
exactly once: one RUN_STARTED bookend, one RUN_FINISHED bookend, and no
duplicate leaf name. The research run delivered the known double
delivery, four milliseconds apart, and both footer pairs agree on every
count. Every research leaf was counted once by full name. The printed
report block of the product run was captured from the editor log and
copied below. The temporary recorder and both data files were deleted
after the runs, and a forced refresh then reported zero compile errors.

The printed report block of the product run, copied from the editor log:

```text
=== AMUSE vendor run record ===
date: 2026-10-26
counts: discovered 3076, completed 3076, passed 3071, failed 0, skipped 5, inconclusive 0
discovered basis: the leaf count of this assembly node in the test tree the runner loaded for the run. The catalog is filter blind, so a filtered run completes fewer leaves by selection, not by loss.
durations: product 1672.7 s, research not part of this run, sum 1672.7 s
opt-in gates set: none
gated fixtures excluded: Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests (AMUSE_DAO_INTEGRATION unset)
skip reasons:
  - Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.AaoAndDaoCombinedKeepsTheMergedSoundness: The opt-in variable AMUSE_DAO_INTEGRATION is not set to 1. Set AMUSE_DAO_INTEGRATION=1 in a disposable public integration project to run these tests. They delete project-level d4rkAvatarOptimizer output and must never run elsewhere.
  - Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.DaoMergeRunsInTheSdkSequenceAndPreservesSoundness: The opt-in variable AMUSE_DAO_INTEGRATION is not set to 1. Set AMUSE_DAO_INTEGRATION=1 in a disposable public integration project to run these tests. They delete project-level d4rkAvatarOptimizer output and must never run elsewhere.
  - Alrauna.Amuse.Tests.Editor.Build.RealThryLockCharacterizationTests.RealThryLockOutputAlignsThroughTheProductionAligner: The opt-in variable AMUSE_REAL_THRY_LOCK is not set to 1. Set AMUSE_REAL_THRY_LOCK=1 in a disposable project to run the real lock. The lock writes vendor-owned cache data under Assets/_LockedShaderCache and calls the vendor usage counter over the network. It must never run in a project whose state matters.
  - Alrauna.Amuse.Tests.Editor.Build.VendorProductionOrderTests.ModularAvatarMergeAnimatorKeepsTrianglesSoundVendorVRChat: The opt-in variable AMUSE_PRODUCTION_ORDER_DISPATCH is not set to 1. Set AMUSE_PRODUCTION_ORDER_DISPATCH=1 in a disposable project to run this fixture. The SDK callback dispatcher runs VRCFury, and VRCFury writes generated controllers into Packages/com.vrcfury.temp during the build.
  - Alrauna.Amuse.Tests.Editor.Build.VendorProductionOrderTests.VRCFuryToggleKeepsTrianglesSoundVendorVRChat: The opt-in variable AMUSE_PRODUCTION_ORDER_DISPATCH is not set to 1. Set AMUSE_PRODUCTION_ORDER_DISPATCH=1 in a disposable project to run this fixture. The SDK callback dispatcher runs VRCFury, and VRCFury writes generated controllers into Packages/com.vrcfury.temp during the build.
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

- Discovered leaf results: 3076, measured from the assembly node of the
  loaded tree by the block.
- Completed leaf results: 3076, counted once per delivered leaf. No
  double delivery.
- Passed: 3071.
- Failed: 0.
- Skipped: 5.
- Inconclusive: 0.
- Duration from the test framework bookends: 1674.8 seconds. The block
  prints 1672.7 seconds, measured from the first test to the suite
  teardown.

### Research assembly run, `Alrauna.Amuse.Research.Tests.Editor`

- Discovered leaf results: 151, the unique delivered leaf names.
- Completed leaf results: 151.
- Passed: 151.
- Failed: 0.
- Skipped: 0.
- Inconclusive: 0.
- Duration from the test framework bookends: 5.4 seconds. The callback
  delivered the whole run twice, four milliseconds apart, and both
  footer pairs agree on every count.

### Both assemblies together

- Result total: 3227.
- Passed: 3222.
- Failed: 0.
- Skipped: 5.
- Inconclusive: 0.
- Duration of the product run: 1674.8 seconds.
- Duration of the research run: 5.4 seconds.
- Sum of both durations: 1680.2 seconds.

Exit rule of the plan. This is the canonical validation run of the
rework. Both assemblies are green with a named skip list of five tests.
The failed count is 0, and the discovered count equals the completed
count in both assemblies. For the product assembly the two numbers come
from independent sources, the loaded tree and the delivered leaf
results. For the research assembly both numbers come from the delivered
leaf results, deduplicated by full name.

Count changes against the Task 16 full run of this file, the last full
product run before this one. The product leaf count went from 3069 to
3076. Task 17 added the four `VendorProductionOrderTests` leaves, two
tier A fixtures and their two tier B twins. Task 18 added the three
`RealThryLockCharacterizationTests` leaves and changed no other leaf
count, because its stand-in cutover removed counter assertions and not
leaves. The failed count stayed 0.

### Skipped test names, 5 tests

No opt-in gate was set for this run, per V15. Every skip carries its
gate reason in the block above.

The gated DAO fixture, 2 tests:

- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.AaoAndDaoCombinedKeepsTheMergedSoundness`
- `Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests.DaoMergeRunsInTheSdkSequenceAndPreservesSoundness`

The gated real lock, 1 test:

- `Alrauna.Amuse.Tests.Editor.Build.RealThryLockCharacterizationTests.RealThryLockOutputAlignsThroughTheProductionAligner`

The gated production order, 2 tests:

- `Alrauna.Amuse.Tests.Editor.Build.VendorProductionOrderTests.ModularAvatarMergeAnimatorKeepsTrianglesSoundVendorVRChat`
- `Alrauna.Amuse.Tests.Editor.Build.VendorProductionOrderTests.VRCFuryToggleKeepsTrianglesSoundVendorVRChat`

### Failed test names

None.

### Side-effect observation, QualitySettings

Before the runs, on 2026-10-26, `ProjectSettings/QualitySettings.asset`
matched the committed file. After the runs the file still matched the
committed file. The block reports `changed setting lines: none` for the
two digested files.

### Correction note of 2026-10-26, run record gate maps

This note corrects the wording of the printed block above. It changes
no recorded fact. The `gated fixtures excluded` line of the Task 19
canonical run names one fixture class,
`Alrauna.Amuse.Tests.Editor.Build.DaoMergedConsumptionTests`. The run
actually excluded five gated tests of three gated fixtures: the two DAO
tests, the real lock test of `RealThryLockCharacterizationTests`, and
the two tier A tests of `VendorProductionOrderTests`. The
`VendorRunRecord.GateVariables` and `GatedFixtures` maps predate the
Task 17 and Task 18 gates, so the printed block under-reported the
excluded fixtures and would have under-reported a set
`AMUSE_PRODUCTION_ORDER_DISPATCH` gate. The `skip reasons` line of the
same block names all five skips with their gate variables and full
disclosures, so every recorded fact of the run stands. The fix round of
this date extends both maps, and a run record from this fix onward
names all three gate variables and all three gated fixtures. The fix
round test runs of the same date covered `VendorRunRecordTests` and
`VendorOptInGateTests` with 15 leaves, 15 passed, 0 failed.
