# Unit Test Soundness Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair every test the 2026-10-11 soundness audit classified as unable to guard production behavior, while keeping every deliberate standing guard.

**Architecture:** Test-only changes, strengthen-first. Eight no-longer-guarding tests are deleted, each with a recorded no-rewrite argument; six tests are rewritten in place (two mirror-oracle tests over independent literals and real production routes, two fixture self-checks folded into one production-loop test plus preconditions, two fixture-metadata pins replaced by default-platform control runs). Five environment gates become loud named ignores. No production code changes.

**Tech Stack:** NUnit EditMode tests in Unity 2022.3.22f1, run through the Unity Test Runner.

**Spec:** `docs/superpowers/specs/2026-10-11-unit-test-soundness-fixes-design.md`

## Global Constraints

- Base branch `fix/unit-test-soundness`. No staging, committing, or pushing without explicit authorization.
- The full EditMode suite stays green. Baseline on 2026-10-11: 2847 passed, 0 failed.
- Deletion only with the named replacement test green in the same run.
- No production file edits in this plan. A rewrite that seems to need one is a stop condition.
- A filtered run that reports 0 tests is a failure. Record observed counts for every step.
- Never record absolute paths, machine names, ports, or hex hashes.

---

### Task 1: Baseline

**Files:** none.

- [ ] **Step 1: Run the full EditMode suite**

Run both assemblies `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` in EditMode.
Expected: 2847 passed, 0 failed, plus the environment-gated inconclusive set. Record the observed counts.

- [ ] **Step 2: Record the deletion targets**

Confirm each target in section 2 of the spec exists at the named file and method. If any is missing or renamed, stop and report.

---

### Task 2: Delete the three subset tautologies

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`

**Interfaces:**
- Consumes: the drift-pin tests named in spec section 2.1, which must stay green.

- [ ] **Step 1: Delete the three methods**

Delete `ExpectedCanonicalTuple_HasTwentyThreeProperties` and `ExpectedConversionSchema_HasTwentyThreeProperties` from `PoiyomiOpaqueConversionTests.cs`. Delete `ExpectedCanonicalTuple_HasEighteenProperties` from `LilToonOpaqueTargetTests.cs`. Remove helpers only those methods used. The two fixture self-checks in the same file are rewrites, not deletions; Task 3 handles them.

- [ ] **Step 2: Run the owning fixtures**

Run `PoiyomiOpaqueConversionTests` and `LilToonOpaqueTargetTests`.
Expected: green. Two and one fewer tests respectively. The replacement drift pins named in the spec stay green.

---

### Task 3: Rewrite the two fixture self-checks through production

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`

**Interfaces:**
- Consumes: `ExpectedConversionSchema` (the fixture's own literal list, unchanged), the verified conversion seam used by the behavioral rows, `PoiyomiOpaqueConversionRefusal.ConversionPropertyAbsent`.

- [ ] **Step 1: Rewrite the conversion-read check through production**

Replace `FixtureShader_DeclaresEveryConversionReadProperty` with a test that runs the real capture and eligibility over the pristine fixture material and fails when production reports a missing conversion property:

```csharp
        [Test]
        public void FixtureMaterial_ConvertsThroughProductionWithoutAMissingPropertyRefusal()
        {
            var material = NewFixtureMaterial();

            // Drive the same verified conversion route the behavioral rows
            // use, on a pristine fixture material with no perturbations.
            var outcome = <the file's verified-conversion seam call>(
                material, <the seam's standard arguments>);

            // A fixture shader that lost a conversion-read declaration
            // refuses right here with ConversionPropertyAbsent. The refusal
            // is kind-only: to identify the property, loop the schema in
            // the failure message if wanted.
            Assert.That(
                outcome.Refusal,
                Is.Not.EqualTo(PoiyomiOpaqueConversionRefusal
                    .ConversionPropertyAbsent),
                "the fixture shader must declare every conversion-read " +
                "property, or the behavioral rows cannot trust their " +
                "preconditions");
        }
```

Fill the seam call from the neighboring behavioral conversion rows; the verified seam is `EvaluateFor(Material material)` and takes exactly one argument. Note the refusal is kind-only and does not name the property.

- [ ] **Step 2: Fold the blend-op guard into the behavioral test**

Delete `FixtureShader_DeclaresAddBlendOpForNonDependencyTests` and add its two assertions as preconditions at the top of `AddBlendOp_DoesNotAffectEligibility`:

```csharp
            // Fixture precondition: this shader declares _AddBlendOp so the
            // variation below is real, and the schema deliberately omits it.
            Assert.That(material.HasProperty("_AddBlendOp"), Is.True);
            Assert.That(ExpectedConversionSchema, Has.No.Member("_AddBlendOp"));
```

- [ ] **Step 3: Verify the rewrite bites**

Temporarily remove one conversion-read property declaration from the fixture shader source. Run the rewritten test.
Expected: FAIL with the `ConversionPropertyAbsent` refusal. Restore the shader. Run again.
Expected: PASS.

- [ ] **Step 4: Run the fixture**

Run `PoiyomiOpaqueConversionTests`.
Expected: green. Two self-check methods are removed and one production-loop test is added, so the fixture shows one fewer method. `AddBlendOp_DoesNotAffectEligibility` keeps its five rows plus the new preconditions.

---

### Task 4: Delete the local-roster tautology

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Characterization/SharedEvidenceAgreementTests.cs`

- [ ] **Step 1: Delete the method**

Delete `BothFrontendsStillConsumeEverySharedFact` only. Keep the private `ConsumingOutput` helper and every fixture builder: the five `RefusedSharedFact_IsRefusedByBothFrontends` cases call them. Keep those five cases untouched.

- [ ] **Step 2: Run the fixture**

Run `SharedEvidenceAgreementTests`.
Expected: green with one fewer test. The five sibling cases stay green.

---

### Task 5: Rewrite the UV geometry mirror-oracle test

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/ExactUvGeometryTests.cs`

**Interfaces:**
- Consumes: `ExactUvGeometry.ToRational` (production, unchanged).
- Produces: `SharedDyadicToRationalMatchesIndependentRationals` replacing `SharedDyadicToRationalMatchesDirectConstruction`.

- [ ] **Step 1: Write the rewritten test**

The existing test builds five `ExactDyadic` inputs with `(significand, exponent)` pairs `(0,-140)`, `(1,-20)`, `(-3,-20)`, `(7,0)`, `(12345,20)` and calls production `ExactUvGeometry.ToRational(ExactDyadic)`. Keep those five inputs and that construction. Replace the mirrored `direct` computation with literal expected `ExactRational` values, hand-derived: `0` is `0/1`, `1*2^-20` is `1/1048576`, `-3*2^-20` is `-3/1048576`, `7*2^0` is `7/1`, and `12345*2^20` is `12944670720/1`.

```csharp
        [TestCase(0, -140, 0L, 1L)]
        [TestCase(1, -20, 1L, 1048576L)]
        [TestCase(-3, -20, -3L, 1048576L)]
        [TestCase(7, 0, 7L, 1L)]
        [TestCase(12345, 20, 12944670720L, 1L)]
        public void SharedDyadicToRationalMatchesIndependentRationals(
            int significand, int exponent, long expectedNumerator,
            long expectedDenominator)
        {
            var input = BuildDyadic(significand, exponent); // the same
            // ExactDyadic construction the replaced test used, kept as-is.

            var actual = ExactUvGeometry.ToRational(input);

            Assert.That(
                actual,
                Is.EqualTo(new ExactRational(
                    new System.Numerics.BigInteger(expectedNumerator),
                    new System.Numerics.BigInteger(expectedDenominator))));
        }
```

Verify the real `ExactRational` equality and constructor shape before relying on the snippet; if the type exposes no public two-argument constructor, assert the numerator and denominator through whatever public representation it offers, with the same literals. Recompute `12345 * 2^20 = 12944670720` by hand before trusting it.

- [ ] **Step 2: Verify the rewrite kills a named mutant**

Temporarily invert the shift direction in `ExactUvGeometry.ToRational`. Run the rewritten test.
Expected: FAIL for at least one case. Restore production. Run again.
Expected: PASS.

- [ ] **Step 3: Run the fixture**

Run `ExactUvGeometryTests`.
Expected: green with the same test count (one replaced).

---

### Task 6: Rewrite the published flag rule test through the real chain

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaPolicyBoundsTests.cs`

**Interfaces:**
- Consumes: `AlphaPolicyBounds.From`, `SourceImageMaskedChain` band logic (production, unchanged).

- [ ] **Step 1: Write the rewritten test**

Replace the `PublishedFlag` mirror call with the real published-chain route. `AlphaPolicyBounds.From(99, 2)` yields `OpaqueBound` 253 and `NoiseBound` 6, and `SourceImageMaskedChain` resolves a byte below the noise bound to the erased flag, a byte at or above the opaque bound to 255, and everything between to 0. Use the samples 252, 3, and 253, which land one in each band:

```csharp
        [TestCase(252, 0)]                      // witness band
        [TestCase(253, 255)]                    // opaque band
        public void PublishedChainRoute_ResolvesWitnessAndOpaqueBands(
            int sampleByte, byte expectedFlag)
        {
            // Drive the one-byte published-chain route that
            // SourceImageMaskedChainTests.OddWidthPartitionPlacesEachTexelInItsBlock
            // already drives, with bounds From(99, 2) and the single sample.
            byte actual = RunPublishedChainOnOneByte(99, 2, sampleByte);

            Assert.That(actual, Is.EqualTo(expectedFlag));
        }

        [Test]
        public void PublishedChainRoute_ErasesBelowTheNoiseBound()
        {
            byte actual = RunPublishedChainOnOneByte(99, 2, 3);

            Assert.That(
                actual,
                Is.EqualTo(AlphaTextureData.ErasedFlag),
                "sample 3 sits below the noise bound 6, so the byte " +
                "erases");
        }
```

`RunPublishedChainOnOneByte` is the smallest real route that consumes one byte under bounds — copy its construction from the neighboring odd-width partition test. Assert three fixed literals (0, the erased flag, 255); never re-derive the band rule inside the test.

- [ ] **Step 2: Verify the rewrite kills a named mutant**

Temporarily change the ceiling computation `(p * 255 + 99) / 100` to a floor `p * 255 / 100`. Run the rewritten test.
Expected: FAIL on the 99 percent bound cases. Restore production. Run again.
Expected: PASS.

- [ ] **Step 3: Run the fixture**

Run `AlphaPolicyBoundsTests`.
Expected: green with the same test count.

---

### Task 7: Delete the two engine pins

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/MeshCloneFinalizationCharacterizationTests.cs`

- [ ] **Step 1: Delete both methods**

Delete `AnimationUtilityRoundTripsAConstructedMaterialBinding` from the animation characterization file. Delete `DestroyingAnAbandonedCloneLeavesTheSourceIntact` from the mesh clone characterization file. Remove helpers only they used.

No rewrite for either: the first never touches AMUSE code, and the second characterizes Unity's destroy semantics while the production destroy-target failure mode is guarded by the Build sweep and window-close tests (spec section 2.4).

- [ ] **Step 2: Run both fixtures**

Expected: green with one fewer test each. `NativeCloneIsADistinctObjectAndLeavesTheSourceUnchanged` stays green.

---

### Task 8: Make the five vendor gates loud

**Files:**
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs`

**Interfaces:**
- Produces: five named `Assert.Ignore` outcomes in a vendor-free checkout. The watchdog test keeps passing with its NOT-PROVEN message.

- [ ] **Step 1: Convert the four early returns**

In `PoiyomiReachesProvenOpaqueThroughTheProductionPath`, `PoiyomiReachesMustRemainTransparentThroughTheProductionPath`, `ANonAssetTextureIsRefusedBySemanticsBeforeTheResolverSeesIt`, and `ALockedPoiyomiMaterialIsUnattestedAndReportsSemanticsUnknown`, replace the silent `if (!presence.IsInstalled) return;` with:

```csharp
            if (!presence.IsInstalled)
            {
                Assert.Ignore(
                    presence.ExpectedPackageName
                    + " is not installed in this project. The Poiyomi " +
                    "reachability gate needs the attested vendor package; " +
                    "install it to prove this reach.");
            }
```

Use each method's own family name in the message. Match the message style of the lilToon gate in the same file.

- [ ] **Step 2: Convert the silent loop continue**

In `AnInstalledFamilyMatchesTheVersionAmuseAttests`, collect every absent family during the loop and, after the loop, ignore when nothing was proven:

```csharp
            var provenFamilies = 0;
            foreach (var presence in families)
            {
                if (!presence.IsInstalled)
                {
                    continue;   // now counted, reported after the loop
                }
                provenFamilies++;
                // ... existing version assertions unchanged ...
            }

            if (provenFamilies == 0)
            {
                Assert.Ignore(
                    "No attested vendor family is installed in this " +
                    "project. The version match needs an installed " +
                    "family; install one to prove this reach.");
            }
```

- [ ] **Step 3: Run the fixture in a vendor-free checkout**

Run `VendorReachabilityTests`.
Expected: five ignores with the new named messages, the lilToon gate ignore unchanged, the watchdog passes, zero silent passes. Record the observed counts.

---

### Task 9: Delete the two no-behavior pins

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`

- [ ] **Step 1: Delete the two methods**

Delete `PoiyomiSemanticOutput_DeclaresDeterministicDiagnosticOrder` and `TryGetProfileForShaderName_TransparentShader_ReturnsTransparentProfile`. Remove helpers only they used. `ConsentSubjectForRefusedMultiMaterialNamesTheRefusal` stays, per the design.

- [ ] **Step 2: Run the named replacements**

Run `PoiyomiAdversarialTests.MultipleFailingOutputs_GiveOneDiagnosticPerOutputInOutputOrder`, `LilToonAttestationTests.VerifyTransparent_CanonicalEvidence_Succeeds`, `Verify_UnsupportedShaderName_IsRefused`, and the Multi consent family tests.
Expected: all green, proving the replacement coverage named in the spec.

---

### Task 10: Replace the two fixture-metadata pins with behavior versions

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorServicesReactivationCharacterizationTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs`

**Interfaces:**
- Consumes: each file's existing probe plugin and its run-counter knob.
- Produces: `ProbePluginStaysIdleOnTheDefaultPlatform` in each file.

- [ ] **Step 1: Write the replacement tests**

Both files already run `AvatarProcessor.ProcessAvatar` on a dedicated test platform with an armed probe. Add the default-platform control run. Example shape, reusing each file's existing probe and fixture helpers:

```csharp
        [Test]
        public void ProbePluginStaysIdleOnTheDefaultPlatform()
        {
            using var scope = <the file's existing scene/fixture scope>;
            var root = <the file's existing minimal avatar root>;

            <arm the file's probe plugin; reset its run counter to zero>;

            AvatarProcessor.ProcessAvatar(root);   // default platform, no RunsOnPlatforms pin

            Assert.That(
                <probe run counter>,
                Is.EqualTo(0),
                "the probe plugin declared for the dedicated platform " +
                "must never run on the default platform");
        }
```

Fill the three placeholder expressions from the same file's existing dedicated-platform test, which already arms the probe and reads its counter. Keep the old reflection assertions deleted; do not keep both.

- [ ] **Step 2: Verify the new test can fail**

Temporarily remove the dedicated-platform attribute from the file's probe plugin. Run the new test.
Expected: FAIL, because the probe now runs on the default platform. Restore the attribute. Run again.
Expected: PASS.

- [ ] **Step 3: Run both fixtures**

Expected: green with equal test counts (one replaced in each).

---

### Task 11: Naming and hygiene repairs

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs`
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/RendererRefusalCalibrationTests.cs`
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Census/AnonymizedRecordTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs`

- [ ] **Step 1: Rename the two alpha mask tests**

Rename `AlphaMask_WithNaNScale_RefusesWithoutException` to `AlphaMask_WithNonFiniteTilingScale_RefusesWithoutException`. Rename `AlphaMaskWithNonFiniteScaleOrOffsetRefusesWithoutException` to `AlphaMaskWithNonFiniteTilingScaleOrOffset_RefusesWithoutException`. No assertion changes.

- [ ] **Step 2: Fix the hygiene items**

Fix the misindented `[Test]` attribute on `UnprovenSlotMappingStillCountsTheMesh`. Delete the dead local `refusals` dictionary in `AReportCopiesItsCategoryDictionariesDefensively` and its count assertion on it. Replace the try/catch plus `Assert.Fail` wrapper in `ReplaceModeLayer_DiscardsTheMainChain` with its inner assertions directly.

- [ ] **Step 3: Run the four fixtures**

Expected: green, unchanged counts.

---

### Task 12: Full-suite validation

- [ ] **Step 1: Run the full EditMode suite**

Expected: green. Eight fewer test methods than the 2847 baseline (about eleven fewer runner cases), six tests rewritten in place, five new named ignores in `VendorReachabilityTests`, every replacement test green. Record the observed counts.

- [ ] **Step 2: Record the report**

Record: deletions applied, rewrites with their mutant-verification outcomes, ignore conversions with observed skip counts, and the final suite counts.
