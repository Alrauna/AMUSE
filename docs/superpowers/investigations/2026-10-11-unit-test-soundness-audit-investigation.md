# Unit test soundness audit

- Date: 2026-10-11
- Status: investigation complete on 2026-10-11. No test or production file changed. The owner chose report-only on 2026-10-11.
- Branch: `fix/unit-test-soundness`, cut from `main` at commit `eb48e74`. Nothing is committed on it.
- Scope: every EditMode test in `Packages/com.alrauna.amuse/Tests/` and `Packages/com.alrauna.amuse.research/Tests/`.

## Privacy note

This document names only repository files, public synthetic fixtures, and test method names. No private avatar, renderer, material, animation, machine, instance, port, or identifier appears in it. Test results are recorded as observed counts.

## Question

Which tests are truly sound, and which are mock tests? A sound test drives real production logic with real inputs and fails on a plausible wrong implementation. A mock-ish test asserts a fixture against itself, asserts a substitute, asserts source text or call counts, or cannot fail.

## Method

Thirteen read-only scouts audited the corpus in parallel on 2026-10-11. Each scout read the production code under test before judging its tests. A fourteenth scout audited nine files that the first file listing had dropped; see the coverage section. A condensed record of all fourteen reports lives outside this repository for the review session; the named claims below all trace to those reports.

One baseline test run and one clean repeat run went through the dev editor instance on 2026-10-11. Before each run, `Application.dataPath` was checked against the repository `Assets` folder. The private Lab project was never touched.

## Baseline observations, 2026-10-11

| Observation | Value |
|---|---|
| Clean run result | 2847 passed, 0 failed, 0 skipped |
| Run duration | about 677 seconds |
| Inconclusive | 2, both in `DaoMergedConsumptionTests` |
| Advertised cases that never reported | 7 of 2856, in both runs |

Notes on the baseline.

- The first run lost its progress tally to an editor domain reload mid-run. Its final status was still success with no recorded failure. The clean repeat run confirms the result.
- The two inconclusive tests guard a disposal project with an environment gate. They demand an opt-in variable before they run. This is designed behavior, not a defect.
- The 7 unreported leaf cases are an open defect in test reporting or counting. Both runs advertised 2856 cases and reported 2849. No test failure is involved. This needs one focused look.

## Coverage incident

The first file listing for this audit hit a 200-entry display cap. It silently dropped 9 test files: 4 in `Tests/Editor/Analysis/` and 5 in `Tests/Editor/Host/`. The audit found this because one scout listed its directory itself and reported the extra files. A directory-scoped listing on 2026-10-11 confirmed 9 missed files, and a makeup scout audited them the same day. Their results are part of every table below. Lesson for future audits: scope file listings per directory, and never trust a capped listing as complete.

## Verdict table

Verdicts per test method, all areas, 2026-10-11. About 2255 test methods were classified. The Files column counts test-bearing source files only. Rows marked with `~` carry a small unresolved drift between a scout's per-file rows and its own summary; the drift is a few methods and touches no named defect. The Environment column counts tests that pass silently in this repository only because a vendor package is absent.

| Area | Files | Sound | Characterization | Tautology | Wiring only | Dead | Env-vacuous |
|---|---|---|---|---|---|---|---|
| Analysis | 11 | 341 | 17 | 0 | 0 | 0 | 0 |
| Build, passes and lifecycle | 11 | 288 | 11 | 0 | 2 | 0 | 0 |
| Build, unlock and merged consumption | 9 | 77 | 0 | 0 | 0 | 0 | 0 |
| Host, alpha evidence | 15 | 210 | 42 | 0 | 7 | 1 | 0 |
| Host, animation evidence | 5 | 92 | 5 | 0 | 1 | 0 | 0 |
| Semantics root | 11 | 168 | 10 | 0 | 2 | 0 | 0 |
| Poiyomi | 14 | ~312 | ~14 | 4 | 1 | 0 | 0 |
| LilToon, interpreters | 10 | ~181 | ~7 | 0 | 0 | 0 | 0 |
| LilToon, attestation and eligibility | 7 | ~222 | ~14 | 1 | 1 | 0 | 0 |
| Characterization | 5 | 22 | 2 | 1 | 0 | 0 | 0 |
| Presets, Runtime, optimizer, fixtures | 8 | 41 | 14 | 0 | 0 | 0 | 0 |
| Research, Collection and Calibration | 8 | 28 | 20 | 0 | 1 | 0 | 5 |
| Research, Census | 10 | 70 | 5 | 0 | 0 | 0 | 0 |

Bookkeeping notes. The LilToon interpreters row keeps the two alpha mask tests as sound after the refuted claim below; their defect classification was the scout's error. The LilToon eligibility scout leaves about 9 of 247 methods unclassified in its own summary; the audit could not resolve them and marks the row with `~`. The Research Collection characterization count includes 11 alpha evidence methods whose 22 parameterized cases all characterize the probe harness.

## Answer

The suite is not mock-heavy. No test substitutes the unit under test. No pipeline stubbing exists. The substitutions found are vendor attestation, vendor target-shader resolution, and a small set of caller-supplied seams that the production API accepts as parameters: the NDMF platform bindings interface, alpha field providers, semantics resolvers, and consent presenters. Every substitute of that kind still routes the assertion through real production logic. Falsifier discipline is strong. Asymmetric fixtures kill slot-swap, row-flip, and off-by-one implementations. Parity oracles compare two real production routes on identical evidence.

The defects are few and named.

- 6 tautologies that cannot fail
- 15 wiring-only tests that assert structure or source text
- 1 dead test that only proves Unity works
- 5 research gate tests that pass silently when the vendor shader is absent
- 2 mirror-oracle tests that run production code but carry a mirrored expectation with weak power
- 1 scout defect claim that observation refuted; see the refuted claim section

## Tautologies, 6

Each entry names the file, the method, why it cannot fail, and what a sound version asserts. An adversarial code review on 2026-10-11 confirmed all six and removed two earlier entries that can in fact fail; see the mirror-oracle section.

1. `Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`, `ExpectedCanonicalTuple_HasTwentyThreeProperties`. It asserts the length of the test's own literal array. No production call.
2. Same file, `ExpectedConversionSchema_HasTwentyThreeProperties`. Same defect for the second literal.
3. Same file, `FixtureShader_DeclaresEveryConversionReadProperty`. The fixture shader validates itself against test data. No production code runs.
4. Same file, `FixtureShader_DeclaresAddBlendOpForNonDependencyTests`. Same fixture self-check. The real non-dependency claim is proven behaviorally by `AddBlendOp_DoesNotAffectEligibility`.
5. `Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`, `ExpectedCanonicalTuple_HasEighteenProperties`. It asserts the test fixture's own array length. The real drift pin is `CanonicalOpaqueProperties_MatchTheIndependentlyStatedTuple` in the same file.
6. `Tests/Editor/Semantics/Characterization/SharedEvidenceAgreementTests.cs`, `BothFrontendsStillConsumeEverySharedFact`. It maps its own string array through its own private switch. Production is never consulted. Its stated guard purpose is already covered by the five `RefusedSharedFact_IsRefusedByBothFrontends` cases. A probe proves it dead: remove a real fact check from a frontend and this test stays green while the five sibling cases go red.

Evidence that entries 1 to 4 guard nothing comes from a scout probe: change one canonical value in production and the real drift pins fail while all four stay green. The code review also found that entries 1 to 4 are strict subsets of their neighboring drift pins, so deleting them loses zero assertions.

## Mirror-oracle tests, 2

These run production code on one side and a test-local restatement on the other. They can fail, so they are not tautologies. Their power is weak because a shared wrong assumption stays invisible.

1. `Tests/Editor/Analysis/ExactUvGeometryTests.cs`, `SharedDyadicToRationalMatchesDirectConstruction`. The actual side calls production `ToRational`. The expected side rebuilds the value with the `ExactRational` constructor and integer shifts. A shift inversion, sign flip, or swapped branch in production fails the test. What it cannot catch is a wrong rule adopted on both sides. Keep, or strengthen with independently derived rational literals.
2. `Tests/Editor/Analysis/AlphaPolicyBoundsTests.cs`, `PublishedFlagRuleResolvesTheThreeBands`. Production runs inside the assertion: `AlphaPolicyBounds.From` computes both bounds and `AlphaTextureData.ErasedFlag` is read, so a rounding regression flips a case. But the flag branching itself is a test-local mirror of the published-chain rule. One more risk: the mirror and the production chain agree only under the inspector clamp, which the tests assume and never assert. A sound version drives the real published-chain route with boundary samples 252, 3, and 253 under the 99 percent bound and asserts the three bands.

## Wiring-only tests, 15

These assert metadata, source text, or structure. Some are deliberate standing guards. The fix decision must respect that.

Source-text scans, 2. `Tests/Editor/Build/AlphaSeparationApplyTests.cs`, `ProductionEditorCodePersistsOnlyThroughTheNdmfAssetSaver`. And `Tests/Editor/Build/AlphaSeparationPersistenceTests.cs`, `AlphaSeparationProductionSourceNamesNoSaveAsset`. Both scan production source for API tokens. Their docstrings concede that dynamic tests cannot prove the persistence invariant. A behavioral anchor exists: `GeneratedClonesResolveToTheirSourcesThroughTheObjectRegistry` already proves registry behavior. Decide: keep as documented guards, or replace with a behavior test if one can be written.

Reflection-guard positive controls, 6. All in `Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs`: `GuardCatchesUnityObjectsNestedBelowTheFirstLevel`, `GuardCatchesUnityObjectsBehindAnArrayElementType`, `GuardCatchesUnityObjectsBehindADeclaredGenericArgument`, `GuardCatchesUnityObjectsInAPrivateBaseClassField`, `GuardRejectsASystemObjectField`, `GuardTerminatesOnACleanRecursiveTypeGraph`. They exercise only the test file's own type-walk instrument. They are instrument checks, and the production invariant tests beside them are sound.

Fixture metadata pins, 2. `Tests/Editor/Host/AnimatorServicesReactivationCharacterizationTests.cs`, `ReactivationPluginIsConfinedToItsDedicatedPlatform`. And `Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs`, `LifetimePluginIsConfinedToItsDedicatedPlatform`. Both reflect the fixture plugin's own platform attribute. A sound version asserts the production plugin's platform declarations, or proves the probe never runs under the default platform.

API surface pins, 3. `Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`, `SharedClass_ExposesExactlySixSemanticFacts`, a reflection count over declared method names. `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`, `PoiyomiSemanticOutput_DeclaresDeterministicDiagnosticOrder`, which pins enum ordinals that no production code consumes; diagnostic order comes from evaluation order, and reordering the enum breaks the test with unchanged behavior. `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`, `TryGetProfileForShaderName_TransparentShader_ReturnsTransparentProfile`, which reflects a private method and asserts it echoes its input; selection behavior is already covered by public verify tests.

Argument forwarding, 1. `Tests/Editor/Semantics/PackageVersionReaderTests.cs`, `ListPackagesReceivesIncludeIndirectTrue`. The whole file has no sound test. The reader's real logic, a bounded wait, failure refusal, and package matching, has no coverage. This is the weakest file in the suite and the best candidate for new sound tests.

Fixture hygiene, 1. `Tests/Editor/Calibration/VendorReachabilityTests.cs`, `TheSceneHelperTracksAndDestroysAnArbitraryShaderMaterial`. It verifies a test helper. Harness hygiene, not production.

## Dead test, 1

`Tests/Editor/Host/UnityAnimationCharacterizationTests.cs`, `AnimationUtilityRoundTripsAConstructedMaterialBinding`. It proves that Unity can store and read back a curve. It cannot fail unless Unity is broken. Drop it or fold it into a fixture precondition.

## Environment-vacuous passes, 5

`Tests/Editor/Calibration/VendorReachabilityTests.cs`. Five methods assert nothing when the vendor shader family is absent. A code review on 2026-10-11 reconciled the scout's own 4 versus 5 discrepancy: four use a literal early return, and the fifth silently continues inside its loop.

- Early return: `PoiyomiReachesProvenOpaqueThroughTheProductionPath`, `PoiyomiReachesMustRemainTransparentThroughTheProductionPath`, `ANonAssetTextureIsRefusedBySemanticsBeforeTheResolverSeesIt`, `ALockedPoiyomiMaterialIsUnattestedAndReportsSemanticsUnknown`.
- Silent loop continue: `AnInstalledFamilyMatchesTheVersionAmuseAttests`, which iterates installed families and does zero iterations in a clean checkout.

Not vacuous: the lilToon gate reports a loud named ignore, and the watchdog test prints that vendor reachability is not proven.

The file's own convention demands loud reporting. The probe helpers document that a silent skip reads as a pass. The fix is to convert the four early returns and the loop continue to named ignores, or to fail like `AlphaEvidenceCharacterizationTests` does.

## Refuted scout claim

One scout reported a real defect in `Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs`. It claimed `AlphaMask_WithNaNScale_RefusesWithoutException` and `AlphaMaskWithNonFiniteScaleOrOffsetRefusesWithoutException` pin a stale refusal detail and cannot pass. Observation on 2026-10-11 refutes this. A focused run of the fixture passed all 5 tests.

The static error: the fixture puts the NaN scale into the mask assignment tiling, not into the `_AlphaMaskScale` scalar. Production refuses a non-finite tiling at the assignment gate and names `_AlphaMask`, which is what the test asserts. Production refuses a non-finite scalar at the scale gate and names `_AlphaMaskScale`, a path this file does not exercise. The code review refined the coverage picture: only the transparent suite pins the `_AlphaMaskScale` scalar refusal end to end, the cutout suite pins the neighboring `_AlphaMaskValue` gate instead, and the alpha mask unit file covers neither scalar gate. The scale-specific refusal therefore has exactly one coverage point in the whole suite.

The residual finding is a name defect only. `AlphaMask_WithNaNScale_RefusesWithoutException` exercises tiling finiteness, so its name overstates what it covers. Rename it, or add a scalar-path test, at fix time.

## Characterization tests are healthy

About 160 methods are deterministic pins of real behavior. They are classified characterization, and most are load-bearing: component defaults are the Safe preset contract, enum snapshots are loud change detectors, platform pins fail loudly when a host stops conforming. None of them is dressed-up tautology. The one characterization-area defect is tautology 6 above.

## Weak spots worth strengthening at fix time

These are sound tests with thin power, not defects. This list is a selection of the highest-value items, not the full set. The reports name about 25 further weak methods, mostly flag-only or echo assertions, and the full lists live in the report record.

- `PackageVersionReaderTests`: the entire production reader path is uncovered. Highest value new tests in the audit.
- `AffineUvTransformTests.UvMappingIdentityMatchesPerComponentComparison`: one assertion reimplements the production identity expression verbatim, which makes it near-mirror. The literal rows in the same test carry the killing power.
- `PoiyomiOutlineAlphaSemanticsTests.OutlinesEnabled_UnprovenTexture_ChainAdmitted_AlphaIsTheMappedTerm`: asserts only completeness. An implementation that over-proves an unproven outline texture still passes. Add a hole-field classification row.
- `UnityAnimationCharacterizationTests.InterpolatingCurveProducesIntermediateValues`: any interpolation passes. The sibling tangent tests already pin exact outcomes.
- `UnityAnimationCharacterizationTests.UnityGeneratesTheMaterialBindingsWeParse`: the hard-coded binding count is a version tripwire with low information. The `Contains` assertions carry the real obligation.
- `MeshCloneFinalizationCharacterizationTests.DestroyingAnAbandonedCloneLeavesTheSourceIntact`: near-dead weight; redundant with the distinct-object test.
- Report and string content pins in `AmuseReportStringsTests` and `PoiyomiAlphaMaskTests` pin wording. They are deterministic and intentional. Keep, unless a behavioral equivalent exists.

## Probe catalog

Each scout produced one mutation probe per file. A probe is a small production change that sound tests must catch. Examples with named expected failures:

- Remove the early-return activation gate in `AmusePlatformFinishPass.Execute`. Three barrier tests must fail.
- Write `sharedMaterials` before the mesh in `ApplyFinalization`. The blocking-proof and split-order tests must fail.
- Change the path comparison in `AddressesAnalyzedRenderer` from ordinal to ignore-case. Five path tests must fail: `RendererPathAndPropertyComparisonsAreOrdinal`, the three `ForeignRenderer` tests, and `SameSlotPropertyOnAnotherPathDoesNotDisplaceThisRenderersOwnSwap`.
- Change `ForProof` to ignore anisotropy eligibility. Two policy tests must fail.
- Replace exact rounding with a ulp margin in the affine alpha map. Two exact-tie tests must fail.
- Delete `_DetailEnabled` from the Poiyomi normal gate list. One neutral-claim test must fail.
- Introduce a tolerance into the lilToon version comparison. The nearby-version refusal test must fail.

The full probe list per file lives in the scout transcripts for as long as the session store keeps them. The fix session can regenerate any probe from the named test in minutes.

## Unrelated findings, recorded only

These came out of the audit. None was acted on.

- `Packages/com.alrauna.amuse.research/Editor/Collection/ThryLockToolDigestRecord.cs` appears unreferenced. A package-wide search matches only its own declaration and comment. Dead-weight candidate.
- `Tests/Editor/Census/AnonymizedRecordTests.cs` builds a local `refusals` dictionary that never reaches the report. Dead fixture weight inside a sound test.
- `Tests/Editor/Collection/RendererRefusalCalibrationTests.cs` has a misindented test attribute on `UnprovenSlotMappingStillCountsTheMesh`. Cosmetic.
- `CollectorMutationSafetyTests` counts process-wide objects, so a background import during the run could perturb it. Flakiness surface only.

## Next actions when fixing starts

1. Delete or rewrite the 6 tautologies. Rewrites have named sound versions above. The four fixture self-checks can be deleted with zero assertion loss.
2. Convert the 5 silent research gates to loud named ignores. The count is resolved in this document.
3. Decide the disposition of the 15 wiring-only tests. Keep documented guards. Replace the rest where a behavioral equivalent exists. Give `PackageVersionReaderTests` real coverage either way.
4. Drop the dead test.
5. Rename the misnamed alpha mask test, and add a second end-to-end coverage point for the `_AlphaMaskScale` scalar refusal.
6. Explain the 7 unreported leaf cases from the baseline.
7. Run the full suite before and after, and record observed counts.

## Adversarial review record

Two independent reviewers audited this document against the scout reports and the repository code on 2026-10-11, and every finding below is applied in this revision.

- The code reviewer refuted two of the eight original tautology claims and moved them to the mirror-oracle section. The tautology count fell from 8 to 6, and the Analysis table row changed accordingly.
- The code reviewer resolved the research gate count to 5 and named the fifth mechanism: a silent loop continue, not an early return.
- The code reviewer corrected the alpha mask coverage statement: only the transparent suite pins the `_AlphaMaskScale` scalar refusal, and the cutout suite pins the `_AlphaMaskValue` gate.
- The code reviewer confirmed the remaining six tautologies, the dead test, the refuted scout claim, and three sound spot checks across Build, Host, and Analysis. It found one letter-level exception to the substitution claim, now stated precisely in the answer.
- The fidelity reviewer found and this revision fixed: the Research Collection table row that had copied the area total into the sound column, the wiring headline that said 14 while the list holds 15, a stale characterization prose count, a probe that understated its blast radius, a Files column with mixed counting conventions, and two scout-summary drifts that are now marked and documented instead of silently copied.
- The fidelity reviewer also required that the makeup scout's verdicts be part of the reviewable record. The record now carries all fourteen reports.
