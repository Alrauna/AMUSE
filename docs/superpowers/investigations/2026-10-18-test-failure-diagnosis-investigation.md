# Test Failure Diagnosis on Branch feat/inspector-ux-indentation

Date: 2026-10-18.

Base: branch `feat/inspector-ux-indentation` at commit `fc52e33`. Environment: the dev editor instance, Unity 2022.3.22f1.

Privacy note: this investigation reads only product code, test fixtures, and git commit history in this repository. It reads no private avatar data and no Census data. Machine details are named by role only.

## 1. Symptom

On 2026-10-18, 24 EditMode tests fail deterministically in `Alrauna.Amuse.Tests.Editor`:
- 10 tests fail in `AlphaSeparationApplyTests.cs`.
- 5 tests fail in `AlphaSeparationPreparationTests.cs`.
- 1 test fails in `AmusePlatformFinishPluginTests.cs`.
- 2 tests fail in `AmuseReportsSlotTests.cs`.
- 6 tests fail in `AlphaSeparationDryRunTests.cs`.

All 144 tests in `Alrauna.Amuse.Research.Tests.Editor` pass or skip as expected (140 passed, 0 failed, 4 skipped because vendor packages are not installed).

The failures reproduce identically at the merge base `main` (`15a8db7`). None of the failures stem from the inspector indentation changes.

## 2. Primary Root Cause: Unpinned Cutout Minimum Texture Size in Test Helpers

The primary root cause explains all 24 failures.

### Mechanism

Commit `26d4491` introduced arm-scoped cutout settings on the component `AmuseAvatarOptimizer`. That change added the serialized property `_cutoutPreserveTransparencyMinTextureSize`. The property defaults to 128.

Two common test helpers pin transparency settings to admit all textures:
- `FixtureProofScope.PinAllSizes` in `Packages/com.alrauna.amuse/Tests/Editor/Build/FixtureProofScope.cs`.
- `AddOptimizer` in `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`.

Both helpers set `_preserveTransparencyMinTextureSize = -1`. Neither helper sets `_cutoutPreserveTransparencyMinTextureSize = -1`. The cutout field remains at the default value of 128.

In `UnityRendererAlphaAnalysis.GatherAlphaFields`, cutout materials evaluate `texture.CaptureThreshold < 1f`. The method sets `armMinTextureSize = cutoutMinTextureSize` (128). Test textures use synthetic dimensions of 4x4, 8x8, or 32x32. `TextureSizeScope` calls `MaximumLevelAtOrAbove(128)` on the texture mip chain. Because the texture dimensions are smaller than 128, the scope check returns -1. `GatherAlphaFields` skips the texture. The alpha field is not recorded in `AlphaFieldSet`.

When `AlphaSemanticsResolver.Resolve` resolves the slot, texture lookup fails. Resolution fails closed with `AlphaResolutionFailure.MissingTextureEvidence`. All triangles remain `Unknown`. Zero candidate triangles are produced.

### Relationship to Commit 191a1d7 and Theorem Shortcuts

In `LilToonAlphaInterpreter`, when `SampledAlphaIsProvenOne` evaluates to true, alpha collapses to a constant. This optimization bypasses texture sampling and `AlphaFieldSet`.

Commit `191a1d7` updated `LilToonCutoutConversionFixtures` in `AlphaSeparationPreparationTests.cs` to stage fully opaque sources with `sourceHasAlpha: false`. This enabled the `SampledAlphaIsOne` importer theorem shortcut. That shortcut masked the unpinned cutout minimum size for those specific tests.

Any test that cannot take the shortcut falls back to texture sampling. Fallback texture sampling requires the texture in `AlphaFieldSet`. Because `cutoutMinTextureSize` remains 128, all such tests fail closed.

### Affected Tests

This single defect causes all 24 test failures:

1. All six failures in `AlphaSeparationDryRunTests`:
   - `AnisotropyRefusalProducesRefusedSentence`: returns `Untouchable` because resolution fails before anisotropic footprint analysis.
   - `CutoutSplitSlotAllProvenProducesConvertableSentence`: returns `Untouchable` instead of `Convertable`.
   - `CutoutSplitSlotProducesSplittableSentence`: produces zero candidate triangles instead of one.
   - `DryRunVerdictsMatchTheBuildOnAMixedRenderer`: returns `Untouchable` instead of `Convertable`.
   - `FoldSlotProducesConvertableSentence`: returns `Untouchable` instead of `Convertable`.
   - `StreamingPngTextureDecodesInMemoryAndDoesNotRefuse`: returns `Untouchable` instead of `Convertable`.

2. Five failures in Preparation, Plugin, and Reports:
   - `AlphaSeparationPreparationTests.EachSlotSelectsItsOwnCoveragePair`: zero proven triangles prevents coverage calculation.
   - `AlphaSeparationPreparationTests.PoiyomiSiblingSlotsSelectDifferentPairs`: slot disposition stays `Unchanged` instead of `WhollyOpaqueCandidate`.
   - `AmusePlatformFinishPluginTests.PoiyomiSiblingsProveUnderDifferentPairs`: slot disposition stays `Unchanged`.
   - `AmuseReportsSlotTests.CutoutFoldReportsTheCutoutVariant`: preparation returns null because candidate count is zero.
   - `AmuseReportsSlotTests.TransparentFoldReportsByteIdenticalBaseOutput`: preparation returns null for the cutout arm.

3. Three tests in Preparation that fall back to texture sampling:
   - `AlphaSeparationPreparationTests.GeneratedSubAssetTexture_IsAdmittedAndProvenOpaqueEndToEnd`: 8x8 in-memory sub-asset texture dropped by size check.
   - `AlphaSeparationPreparationTests.PreserveTransparencyMipPolicyScopesTheProofEndToEnd`: 32x32 explicit mipmap texture dropped by size check.
   - `AlphaSeparationPreparationTests.RefusedTransparentSlotLeavesItsAdmittedSiblingsConverted`: 4x4 texture shared with transparent slot dropped by size check.

4. Five varying-alpha tests in Apply:
   - `AcceptedPolicyAppendsTheOpaqueSubmeshForACutoutMixedHalfSlot`: 8x8 varying-alpha texture dropped.
   - `ConservativePolicyKeepsTheCutoutMixedHalfSlotOriginal`: 8x8 varying-alpha texture dropped before anisotropy comparison.
   - `CutoutBuildCarriesTheFullArtifactEndToEnd`: slot 1 8x8 split-alpha texture dropped.
   - `CutoutSplitSlotRewritesCurvesOntoTheAppendedSlot`: 8x8 split-alpha texture dropped.
   - `TransparentSiblingKeepsPoiyomiAndCutoutResultsByteIdentical`: throws `KeyNotFoundException`.

5. Five fully opaque tests in Apply with duplicate helper:
   - `ACapturedTargetMismatchBetweenPrepareAndApplyRefusesEveryPreparedSlot`
   - `CutoutSwapToAttestedOpaqueMapsToItselfWithoutAClone`
   - `DesktopAcceptanceRevokedBetweenPrepareAndApplyRefusesEveryPreparedSlot`
   - `FullyOpaqueCutoutEvidenceConvertsWithoutAcceptance`
   - `TheApplyStalePolicyGuardHonorsThePinnedBuildTarget`

### The KeyNotFoundException Mechanism

In `TransparentSiblingKeepsPoiyomiAndCutoutResultsByteIdentical`, slot 0 uses an attested Poiyomi material. Slot 1 uses a cutout material with an 8x8 split texture. Slot 0 prepares successfully. Therefore `state.Separation` is not null. Slot 1 produces zero candidates because its 8x8 texture is dropped. The cutout material never converts. It is never registered into `state.Separation.OpaqueBySource`. Line 5667 indexes `state.Separation.OpaqueBySource[cutout]` and throws `KeyNotFoundException`.

## 3. Secondary Fixture Discrepancy: Duplicated Helper in Apply Tests

`AlphaSeparationApplyTests.cs` (lines 5837-5850) contains a duplicate definition of `LilToonCutoutConversionFixtures`. Commit `191a1d7` updated the definition in `AlphaSeparationPreparationTests.cs`, but missed the copy in Apply.

The Apply helper continues to stage fully opaque textures with an alpha chunk (`sourceHasAlpha: true`). `TryProveSampledAlphaIsOne` returns false, so these tests fall back to texture sampling. If `_cutoutPreserveTransparencyMinTextureSize` is pinned to -1, these tests pass through fallback texture sampling. Updating the helper is desirable cleanup so both test suites use identical staging patterns.

## 4. Resolution Path

The resolution path updates test helpers to pin cutout minimum texture size to -1:

1. Update `FixtureProofScope.PinAllSizes` in `Packages/com.alrauna.amuse/Tests/Editor/Build/FixtureProofScope.cs`:
   Pin `_cutoutPreserveTransparencyMinTextureSize = AllSizes` alongside `_preserveTransparencyMinTextureSize`.

2. Update `AlphaSeparationDryRunTests.AddOptimizer` in `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`:
   Pin `_cutoutPreserveTransparencyMinTextureSize = -1` alongside `_preserveTransparencyMinTextureSize`.

3. Update the duplicate helper in `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`:
   Stage theorem-admitting sources in `LilToonCutoutConversionFixtures` to match `AlphaSeparationPreparationTests.cs`.
