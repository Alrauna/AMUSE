# Test Fixture Cutout Minimum Texture Size and Staging Scope Repair: Design

Date: 2026-10-18.

Base: branch `feat/inspector-ux-indentation` at commit `b5f5277`.

Investigation: `docs/superpowers/investigations/2026-10-18-test-failure-diagnosis-investigation.md`.

Revision note: 2026-10-18, after a three-lens parallel adversarial review (accuracy and safety, architectural purity and ponytail adherence, potential latent bugs). Incorporated: Decision 3 (duplicating `191a1d7` fixture changes into Apply tests) was evaluated and rejected. The review proved that Decisions 1 and 2 completely resolve all 24 test failures. Keeping the Apply fixtures unchanged preserves end-to-end test coverage for the fallback texture-sampling path and eliminates unnecessary code bloat.

Privacy note: this design reads only product code, test fixtures, and git commit history in this repository. It reads no private avatar data and no Census data. Machine details are named by role only.

## 1. Overview

On 2026-10-18, 24 EditMode tests fail in `Alrauna.Amuse.Tests.Editor`. The failures reproduce identically at `main` (`15a8db7`).

The failures share one common mechanism: test helpers configure `_preserveTransparencyMinTextureSize = -1` (All Sizes), but omit setting `_cutoutPreserveTransparencyMinTextureSize = -1`. The cutout field remains at its default value of 128. In `UnityRendererAlphaAnalysis.GatherAlphaFields`, cutout materials check `CaptureThreshold < 1f` and select `armMinTextureSize = cutoutMinTextureSize` (128). Synthetic test textures use dimensions smaller than 128 (4x4, 8x8, or 32x32). `GatherAlphaFields` skips these textures because their sizes are below 128. Resolution fails closed with `AlphaResolutionFailure.MissingTextureEvidence`.

This design repairs the two test helpers. It makes zero changes to production code.

## 2. Architectural Principles and Ponytail Ladder

The design applies the ponytail ladder:

1. **Does new production code need to exist?** No. Product code behaves as designed. The cutout minimum texture size is a valid arm-scoped threshold introduced in commit `26d4491`.
2. **Reuse existing test infrastructure**: Update existing test helper methods where test policies are established.
3. **Shortest working diff**:
   - Add one line to `FixtureProofScope.PinAllSizes`.
   - Add one line to `AlphaSeparationDryRunTests.AddOptimizer`.
4. **No production weakening**: AMUSE safety theorems and production classification remain untouched.

## 3. Decisions

### Decision 1: Pin Cutout Minimum Texture Size in FixtureProofScope

In `Packages/com.alrauna.amuse/Tests/Editor/Build/FixtureProofScope.cs`, `PinAllSizes` pins the serialized property `_preserveTransparencyMinTextureSize` to `AllSizes` (-1).

The method will also pin `_cutoutPreserveTransparencyMinTextureSize` to `AllSizes` (-1).

This change ensures that all test fixtures using `FixtureProofScope.PinAllSizes` admit small textures on cutout arms as well as transparent arms. This directly fixes 18 test failures across preparation, apply, plugin, and report suites.

### Decision 2: Pin Cutout Minimum Texture Size in DryRun AddOptimizer

In `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`, `AddOptimizer` pins `_preserveTransparencyMinTextureSize` to -1.

The method will also pin `_cutoutPreserveTransparencyMinTextureSize` to -1.

This change ensures that DryRun test fixtures admit 8x8 synthetic textures on cutout materials. This directly fixes all six test failures in the dry-run suite.

### Decision 3: Reject Modification of Apply Test Fixtures (Ponytail Adherence)

An earlier proposal considered updating `LilToonCutoutConversionFixtures` in `AlphaSeparationApplyTests.cs` to mirror commit `191a1d7`.

That proposal is rejected.

Decisions 1 and 2 resolve all 24 test failures without editing `AlphaSeparationApplyTests.cs`. Keeping `AlphaSeparationApplyTests.cs` unchanged preserves end-to-end test verification of fallback texture sampling in avatar builds. Avoiding this edit prevents code duplication and keeps the diff minimal.

## 4. Detailed File-by-File Changes

### File 1: `Packages/com.alrauna.amuse/Tests/Editor/Build/FixtureProofScope.cs`

In `FixtureProofScope.PinAllSizes(GameObject root)`:

```csharp
var serialized = new SerializedObject(optimizer);
serialized.FindProperty("_preserveTransparencyMinTextureSize")
    .intValue = AllSizes;
serialized.FindProperty("_cutoutPreserveTransparencyMinTextureSize")
    .intValue = AllSizes;
serialized.ApplyModifiedPropertiesWithoutUndo();
```

### File 2: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

In `AlphaSeparationDryRunTests.AddOptimizer(GameObject owner)`:

```csharp
var serialized = new SerializedObject(optimizer);
serialized.FindProperty("_preserveTransparencyMinTextureSize")
    .intValue = -1;
serialized.FindProperty("_cutoutPreserveTransparencyMinTextureSize")
    .intValue = -1;
serialized.FindProperty("_acceptDesktopAnisotropyDifferences")
    .boolValue = true;
serialized.ApplyModifiedPropertiesWithoutUndo();
```

## 5. Non-Goals

1. No changes to production code in `Packages/com.alrauna.amuse/Editor/` or `Runtime/`.
2. No modification to `AmuseAvatarOptimizer` component defaults.
3. No relaxation of alpha proof theorems or conversion eligibility.
4. No changes to vendor attestation or schema files.
5. No unnecessary changes to test fixture staging in `AlphaSeparationApplyTests.cs`.

## 6. Verification Strategy

1. Run the 24 failing tests in `Alrauna.Amuse.Tests.Editor` and observe that all 24 pass.
2. Run the full `Alrauna.Amuse.Tests.Editor` assembly and observe zero failures.
3. Run the `Alrauna.Amuse.Research.Tests.Editor` assembly and observe zero failures.
4. Run `git diff --check` to verify clean whitespace.
