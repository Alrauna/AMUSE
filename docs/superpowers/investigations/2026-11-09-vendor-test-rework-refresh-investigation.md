# Vendor test rework refresh investigation

Status as of 2026-11-09. Read-only investigation. No product code, test code, or package changed. No test ran. Branch: `rework/vendor-integrated-tests`.

Privacy note: this record covers AMUSE source and public vendor packages in the dev project. It names no private avatar, machine, account, port, instance, or GUID. GUID and digest comparisons appear as match or no match only. AMUSE paths are relative to `Packages/com.alrauna.amuse/` unless they start with `Packages/` or `docs/`. Vendor paths start with their package directory under `Packages/`.

Every claim has a tag. `[SOURCE]` means a scout read it in code. `[INFERENCE]` means reasoning without observation. No claim here is `[MEASURED]`, because no run happened.

## 1. Purpose

The owner decided on 2026-10-25 that AMUSE tests must run against the real vendor packages that the dev project installs. The 2026-10-25 record `2026-10-25-vendor-integrated-test-rework-investigation.md` is the baseline. This record refreshes the baseline after three changes:

- Limitex Avatar Compressor (LAC) 0.9.0 is installed.
- Installed vendor versions moved on.
- The repository moved on after the baseline. Part of the rework landed.

The record decides nothing. Section 8 lists open decisions.

## 2. Method

Six read-only scouts ran in parallel. Each owned one slice: LAC, lilToon, Poiyomi with Thry, Modular Avatar with VRCFury, AAO with d4rkAvatarOptimizer, and attestation drift. Each scout read the baseline first. No scout edited a file or ran Unity.

## 3. Installed versions on 2026-11-09

Source: `Packages/vpm-manifest.json` locked block, matched to each installed `package.json`. `[SOURCE]`

|Package|Locked version|
|---|---|
|NDMF|1.14.8|
|VRChat SDK|3.10.6|
|lilToon|2.4.1|
|Poiyomi Toon (bundles 9.3)|10.0.29|
|Thry editor|2.74.4|
|Modular Avatar|1.18.7|
|AAO|1.9.21|
|d4rkAvatarOptimizer|4.7.2|
|VRCFury|1.1442.0|
|LAC|0.9.0|

The `dependencies` block of the manifest lags the locked block for five families by the record's grouping. The VRChat SDK counts as one family. Poiyomi reads 10.0.27 there. The suite-start check reads only the locked block, so the gate is not affected. `[SOURCE]`

The locked block also holds LTCGI 1.7.4 and Red Sim Light Volumes 3.0.1. The table above leaves them out because no AMUSE test double imitates them. LTCGI still matters. The lilToon canonicalization strips the LTCGI define and tag token only when LTCGI is installed. So LTCGI state can change lilToon digest results. `[SOURCE]` `Editor/Semantics/LilToon/LilToonSourceAttestation.cs`.

## 4. Corrections to the baseline

The baseline is stale in these places. Do not plan from the stale text.

|Baseline claim|Observed on 2026-11-09|
|---|---|
|A test-local `Thry.ThryEditor.ShaderOptimizer` shadows the real type|It is gone. The 2026-10-26 cutover deleted it. `VendorLockToolSourceBanTests` guards production source. Real-tool suites resolve the vendor type by assembly-qualified reflection and assert its assembly. `[SOURCE]`|
|Bundled Poiyomi rows do not admit locked originals|`LockedOriginalAdmitted` is true for the bundled rows. `[SOURCE]`|
|Stage 5 (production-order fixtures) is unwritten|`VendorProductionOrderTests`, the tier gates, the V3 provenance read, and a five-layer descriptor fixture exist. `[SOURCE]`|
|lilToon Multi containers have only stand-ins|`LilToonMultiSourceEligibilityTests` loads the installed containers by pinned GUID. `[SOURCE]`|
|`RealThryLockCharacterizationTests` is a plan|It exists and has a gated real lock. `BundledLockedOriginalRealLockTests` also locks the two bundled rows with the real tool. `[SOURCE]`|
|The AAO plugin-disable key can leak|A guard (`VendorSessionStateGuard`) restores it. The suite start fails on a preset key. The suite end fails on an unrestored key. `[SOURCE]`|
|LAC has no installed source|LAC 0.9.0 is installed. `[SOURCE]`|
|Baseline section 4.1: a test platform claims the VRChat name|The type is gone. Every test platform has a `com.alrauna.amuse.tests.*` name. `PlatformProviderAuditTests.NoTestPlatformClaimsABuiltInPlatformName` guards this. `[SOURCE]`|
|Baseline section 4.2: missing FX layer in the fixture|The fixture fills all five base layers. `[SOURCE]`|
|Baseline section 4.3: V3 provenance, property-block writer, marker route|The V3 registry read landed. The property-block writer has a diagnosis record and the fixture plants the block on purpose. The marker route is covered by the stage 1b audit. `[SOURCE]`|
|Baseline section 7.2: development setup registers only the NDMF repository|`docs/development-setup.md` registers all seven vendor repositories and documents three opt-in gates. It also makes an absent vendor a named skip. `[SOURCE]`|
|Baseline open questions Q1, Q2, Q4|The source shows they were decided. `[SOURCE]`|

One production comment is wrong too. The 2026-10-04 LAC design and `ReplacementTextureAttestation.cs` say LAC 0.9.0 never registers a bake pair. The installed source registers it at `Packages/dev.limitex.avatar-compressor/.../LilToonTextureBaker.cs:908-909`. `[SOURCE]` The in-build `_baked` refusal still holds, through the suffix gate and not through missing registration. `[SOURCE]` The consequence: the admitted version string 0.9.0 stayed the same while the behavior moved from the characterized behavior. So a version match alone does not prove the copy shape. The LAC ground check must verify the whole shape. `[INFERENCE]`

## 5. Findings per slice

### 5.1 LAC 0.9.0

- The shape of the real copy and the synthetic `<source>_compressed` copy agree on these facts: name suffix, path-less state, registry pair, desktop formats DXT1, DXT5, BC7, BC5. `[SOURCE]` The real copy also forces the streaming-mips flag. Only the persisted-copy fixtures set that flag. The in-memory synthetic helper does not. No admission conjunct reads the flag, so no outcome changes. `[SOURCE]`
- About 43 LAC-shaped tests use the synthetic copy and injected versions. They sit in `ReplacementTextureAttestationTests` (13), `ReplacementTextureIdentityTests` (4), `GeneratedTextureAttestationTests` (15), `UnityTextureEvidenceTests` (about 7), and `UnityAlphaFieldEvidenceTests` (4). `[SOURCE]`
- The real pass can run in an EditMode test. It is NDMF plugin `dev.limitex.avatar-compressor.texture` in the Optimizing phase. It has no platform restriction. It needs a `TextureCompressor` component. A texture is processed when its larger side is at or above the preset `MinSourceSize` and above `SkipIfSmallerThan`. The default `MinSourceSize` is 256. Presets raise it. `[INFERENCE]`
- The component type is internal and the LAC assemblies grant `InternalsVisibleTo` only to LAC assemblies. A test adds the component through a reflected type. A test can set fields through `SerializedObject`. The repo does this for other vendor components in `Tests/Editor/Build/MergedConsumptionFixture.cs`. `[SOURCE]`
- Happy-path tests can drop both version seams. The production reader answers 0.9.0 and the admitted set holds 0.9.0. They must keep `ResetForTests`, which also clears the per-session version cache. Without it, a dropped seam can serve an earlier injected answer. `[SOURCE]`
- Falsifiers stay synthetic: unregistered copy, unsuffixed copy, two claimants, form crossing, version refusals, foreign or derived containers. No real package produces these states.
- Risks: `TextureCompressorPreferences` backends live in `EditorPrefs`. The format can vary with GPU analysis, so a test should force a format with frozen settings. The offline version read can poll up to 30 seconds.

### 5.2 lilToon 2.4.1

- Installed version, format stamp, and pinned GUIDs match production. `[SOURCE]`
- No `.mat` asset ships in the lilToon package. Cutout, transparent, outline, and Multi materials must be built from the installed shaders at test time. `[SOURCE]`
- Load shaders by pinned GUID. Name lookup is unreliable because stand-ins mimic vendor names. `[SOURCE]`
- The regular-shader and pass digests for the 2.4.1 shape are unmeasured. A real-shader ground check can settle this. A refusal there is a drift signal, not a defect. `[SOURCE]`
- Approximate groups, from scout counts: about 150 Semantics happy paths can move to real materials. About 170 tests are synthetic falsifiers or refusal shapes. About 35 need exact keyword control. The baseline counted about 190 tests on stand-in shaders and about 100 on the verified seams in Build and DryRun. The scouts did not reconcile these with the new counts. A recount is needed.

### 5.3 Poiyomi and Thry

- Names and GUIDs of both bundled 9.3 rows match the installed 10.0.29 bundle. Digest equality is unverified. The recorded measurements stop at 10.0.27. No dated 10.0.28 or 10.0.29 admission record exists. `[SOURCE]`
- Release rows one and two match the 10.x asset names but not the GUIDs. They fail closed by design. `[SOURCE]`
- About 200 Semantics tests use the Poiyomi stand-in shaders. About 30 tests use `LockedSourceFixture`. About 60 transient-unlock tests default `OriginalAttestation` to always true. `[SOURCE]`
- The real lock writes vendor-owned cache entries. Cleanup must delete materials before cache entries. It calls a vendor usage counter over the network once per session. Real-lock tests sit behind `AMUSE_REAL_THRY_LOCK=1`. `[SOURCE]`
- Poiyomi has no assembly definition. Tests can use it only at shader and material level. `[SOURCE]`
- Import and compile cost of the Poiyomi bundle is unmeasured.

### 5.4 Modular Avatar and VRCFury

- About 60 tests use hand-built controllers and swap clips. About 24 and about 8 tests use two `StubBindings` copies. About 10 tests use `MarkerStubBindings`. `[SOURCE]`
- `CommittedControllerGraph.Enumerate` accepts any `IPlatformAnimatorBindings`. A test can run the same enumeration with the real NDMF `GenericPlatformAnimatorBindings`. `[SOURCE]`
- The real marker route needs `VRChatPlatformAnimatorBindings` on the VRChat platform. The exact special-clip control of `MarkerStubBindings` has no vendor equivalent and stays. `[SOURCE]`
- The production order is NDMF part one, VRCFury, NDMF part two with AMUSE, then d4rk. One SDK dispatcher call reproduces it. `AvatarProcessor.ProcessAvatar` alone skips VRCFury. `[SOURCE]`
- A VRCFury dispatch writes project state. It creates `Packages/com.vrcfury.temp`, saves generated controllers, and sets project settings at load. The tier A gate `AMUSE_PRODUCTION_ORDER_DISPATCH` exists. One fixture avatar supports one dispatch. `[SOURCE]`
- A blocking vendor dialog can appear on write-defaults mismatch. The existing tier A fixtures set write defaults off to prevent it. `[SOURCE]` See `PinProbeWriteDefaultsOff` in `Tests/Editor/Build/VendorProductionOrderTests.cs`.

### 5.5 AAO and d4rkAvatarOptimizer

- AAO 1.9.21 still mints the atlas names that the admission wants. The in-memory state and registry resolve match. The version conjunct is retired by design. The real route runs in two ambient tests. `[SOURCE]`
- `CreateGeneratedAtlas` fakes a persisted atlas. Real AAO never persists the atlas. It must stay for the persisted-route leak falsifier. `[SOURCE]`
- `ZzzAnonymousOptimizingProducerPlugin` serves about 20 fixtures on dedicated tiers. Real AAO covers produced-state in the ambient tier. The synthetic plugin keeps exact Poiyomi property control. `[SOURCE]`
- Two gated d4rk tests drive the real d4rk 4.7.2 through the SDK dispatcher. d4rk writes under `Assets/d4rkAvatarOptimizer/TrashBin/`. The teardown deletes it. The skip text names 4.7.1 and the installed version is 4.7.2. `[SOURCE]`
- A third d4rk-named case runs in an ambient `ProcessAvatar`. d4rk does nothing there, because it hooks only the SDK callback. `[INFERENCE]`
- The README says the d4rk combination is not validated in 0.1.0. The gated tests must not contradict that sentence.

### 5.6 Attestation drift

|Family|Result on 2026-11-09|
|---|---|
|lilToon|Version, stamp, and 8 pinned GUIDs match. Shader and pass digests for 2.4.1 unmeasured.|
|Poiyomi|Names and GUIDs match. Byte equality for 10.0.29 unverified.|
|Thry|2.74.4 is newer than the recorded 2.74.3. The accepted patch-upgrade state covers it.|
|NDMF, SDK, Unity, AAO define|Match.|
|LAC|Admitted set 0.9.0 matches 0.9.0.|
|Modular Avatar, d4rk, VRCFury|No version pin exists.|

## 6. What stays synthetic

These doubles have no vendor equivalent, or they give exact control that a vendor run cannot give. They fit one reason from `AGENTS.md`: an independent outcome oracle, an input no vendor produces, exact falsifier control, or a source the pinned environment cannot supply.

- Reference fixtures and analysis oracles with independent outcomes.
- Controlled pixel grids that pin Unity decode boundaries.
- Refusal-shape shader blocks and evidence shapes that no vendor produces.
- Exact keyword states and exact `ifex` condition text.
- LAC and AAO falsifiers for unregistered, unsuffixed, persisted, foreign, and derived states.
- `MarkerStubBindings` for exact special-clip control.
- The `ApprovedRangeVendorScope` embedded package. It represents an approved lilToon version that no vendor ships.
- `LockedSourceFixture`, for per-edit knobs that a real lock cannot give. The fixture lets a test change one lock edit at a time. A real lock cannot do that. It stays only for that exact-control use. Plain lock coverage moves to the real tool.

## 7. Cross-slice risks

- Teardown order. Several slices write under `Assets/` and under the NDMF generated folder. The suite-end hygiene deletes only AMUSE-owned files.
- Session state. NDMF plugin-disable keys, `EditorPrefs` for LAC, and `ReplacementTextureAttestation.Versions` static state need reset in `finally` paths.
- Scripting defines. LTCGI and Red Sim Light Volumes write scripting defines on load. A define change can force a recompile in the middle of a run. This is baseline section 7.3. `[SOURCE]`
- Real-package tests can fail on vendor drift. The failure text must name the package and version.
- Cold-session cost. First use of real lilToon and Poiyomi shaders compiles variants.
- `PackageVersionReader` can poll up to 30 seconds per seam read.
- Product tests must never run in the private Census Lab project.

## 8. Open decisions for the owner

1. Poiyomi 10.0.29 digests. A real-shader ground check will either confirm byte equality or refuse. Does a refusal start the dated K5 admission, or does the owner pin the project to 10.0.27?
2. lilToon 2.4.1 shader and pass digests. The same question for the first ground check.
3. How does a LAC test add and set the component? The type is internal. A reflected type can add it. A `SerializedObject` can set its fields. Or AMUSE can ask LAC for a public test entry.
4. Should real LAC and real Poiyomi lock tests join the existing opt-in gates, or run in the default suite?
5. Should the `dependencies` block of `Packages/vpm-manifest.json` be brought up to the locked block?
6. Where do the stage 1 ground checks and the real-material tests run? In the default suite, or behind a gate?

## 9. Proposed stage order

This order is a proposal. Three adversarial reviewers checked it on 2026-11-09. The owner still decides the items in section 8.

0. Baseline run. Run the full `Alrauna.Amuse.Tests.Editor` assembly once in the dev editor instance. Record observed counts. No later stage starts before this.
1. Ground checks. One test per family loads the installed shader or runs the real pass, and runs production attestation with no injected seam. Families: lilToon, Poiyomi bundled rows, LAC. These tests settle the two digest questions. The LAC check also re-verifies the full copy shape and not only version admission. The LAC part waits on decisions 3, 4, and 6. LTCGI changes lilToon canonicalization when installed, so the lilToon check runs in the dev project as installed.
2. LAC seam drop. Happy-path tests use the real reader and the real pass. Fix the stale bake-pair comment. Waits on decisions 3, 4, and 6. Without LAC installed, every seam-dropped test would fail. The setup document makes an absent vendor a named skip. The tests must follow that rule.
3. lilToon real materials for happy paths. Replace Multi profile rows with `AdmittedMultiRows()`. Keep falsifiers. Waits on decision 2.
4. Poiyomi real materials, after an import-cost measurement. Waits on decision 1. Production admits locked originals by admitted name only. The locked fixtures sit over a stand-in shader. If `OriginalAttestation` points at production attestation, every stand-in locked pair refuses. So the locked fixtures move to real locked sources first. That move needs decision 4. Only then does the `OriginalAttestation` default change.
5. Bindings and marker route. Run enumerations with both stub and real bindings. Add one real Modular Avatar probe.
6. AAO monotone-atlas case. Fix the d4rk skip text.
7. Documentation. Update `README.md`, `docs/architecture/shader-frontend-comparison.md`, and the vendor comments in the two `.gitignore` files.

## 10. Limits of this record

- No scout ran a test. Every pass or fail claim about the real route is `[INFERENCE]`.
- The record does not say whether the 31 failures of baseline section 4 pass. No run happened. The stage order starts from a suite state that nobody observed. Stage 0 of the plan must run the full suite first.
- Scouts reported approximate counts. The counts for stand-in shader users and verified-seam users do not agree with the baseline and are not reconciled. A plan must recount before it sizes a stage.
- The scouts did not hash vendor files. Digest drift is unverified.
- This record was reviewed by three adversarial reviewers on 2026-11-09. Section 11 lists what they changed.

## 11. Review log

Three reviewers checked section 3 and 4, the vendor claims, and the stage order with the documentation rules. All three said ship after fixes. Applied fixes:

- Added corrections for baseline sections 4.1 to 4.3, stage 1 and 1b, `docs/development-setup.md`, and baseline decisions Q1, Q2, and Q4.
- Added LTCGI and Red Sim Light Volumes to section 3.
- Narrowed the LAC claims: size threshold, component field access, and streaming flag parity.
- Moved `LockedSourceFixture` out of the no-equivalent list. It carries an exact-control reason.
- Added decision links to the stage order.
- Added the scripting-define risk and the ground-check gate decision.
- The reviewers found no documentation rule hit in the first draft.

## 12. Owner decisions on 2026-11-09

The owner answered the section 8 questions on 2026-11-09. The owner sent two answers numbered 3. This record reads the second one as decision 4.

|Decision|Answer|
|---|---|
|1. Poiyomi 10.0.29 digests|If a ground check refuses, measure 10.0.29 and add it through a dated admission. Do not pin the project to 10.0.27.|
|2. lilToon 2.4.1 digests|If a ground check refuses, measure and add them.|
|3. LAC component access|Add the component through a reflected type. Set its fields through `SerializedObject`.|
|4. Opt-in gates|Real LAC tests and real Poiyomi lock tests sit behind an opt-in switch.|
|5. Manifest `dependencies` block|Bring it up to the locked block.|
|6. Ground check placement|The owner answered "go for it". This record reads that as: accept the default and start stage 0. The default is the normal run for lilToon and Poiyomi ground checks, and an opt-in switch for the LAC ground check.|

Decision 5 is applied on 2026-11-09. The `dependencies` block of `Packages/vpm-manifest.json` matches the locked block for the VRChat SDK, d4rkAvatarOptimizer, AAO, VRCFury, and Poiyomi. The file has other uncommitted owner changes. Nothing is committed.

## 13. Stage 0 result on 2026-11-09

The full `Alrauna.Amuse.Tests.Editor` assembly ran in the dev editor instance in EditMode. The editor project path matched this repository before the run. The run took about 28 minutes.

|Observed|Value|
|---|---|
|Leaf tests completed (run record)|3241|
|Passed|3221|
|Failed|1|
|Skipped|19. All 19 are named opt-in skips. 15 name `AMUSE_REAL_THRY_LOCK`. 2 name `AMUSE_DAO_INTEGRATION`. 2 name `AMUSE_PRODUCTION_ORDER_DISPATCH`. `[MEASURED]`|
|Inconclusive|0|
|Job total of 3399|This count includes suite nodes of the test tree. It is not a leaf count. An earlier version of this section said 158 tests did not complete. That was a misreading. The run record shows 3241 leaves discovered and 3241 completed. `[MEASURED]`|

The skip counts above are the run record lines of the suite itself. The suite printed the run record block to the Console at the end of the run. The block also shows every locked vendor version equal to its installed version, for 13 packages including LAC 0.9.0 and d4rkAvatarOptimizer 4.7.2. `[MEASURED]`

The one failure is `ReplacementTextureAttestationTests.ResetForTestsRestoresTheCharacterizedSetAndTheProductionProvider`. The assertion `TryReadInstalledProducerVersion` is `Is.False` failed. The test comment says a plan-conformant environment never installs LAC. LAC 0.9.0 is installed, so the production reader answers 0.9.0. `[SOURCE]` The failure agrees with this cause. The test encodes a premise that the owner changed on 2026-11-09. This test is the first item of the LAC seam drop.

The run shows that the 31 failures of baseline section 4 are not present in that run. Only the one LAC failure remains. `[MEASURED]`

The run also shows a test named `WhollyOpaqueSlotReplacesOnlyItsMaterialWithRealPoiyomi`. Real Poiyomi tests exist in `AlphaSeparationApplyTests` in that run. The section 5.3 counts for Poiyomi stand-in users may be too high. A recount in the plan must check this.

Correction on 2026-11-09, after the design review: `Tests/Editor/Semantics/RealSourceGroundCheckTests.cs` holds ungated ground checks for lilToon 2.4.1 and the bundled Poiyomi shaders. They loaded by pinned GUID and passed through production attestation in the stage 0 run. So the digest questions of sections 5.2, 5.3, and 5.6 and owner decisions 1 and 2 have a measured answer: the installed bytes match the pins for both families. Both decisions stay valid as rules for a future drift. No admission is needed now. `[MEASURED]` `[SOURCE]`

## 14. lilToon stand-in recount on 2026-11-10

This section answers plan task 3.1. It is the reconciliation of the unreconciled design counts. Installed lilToon is 2.4.1. Source: `Packages/vpm-manifest.json` locked block, matched to the installed package manifest. `[SOURCE]`

Method. A source scan of 2026-11-10 listed every test method under `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/` and every lilToon stand-in use in the Build and DryRun test folders. A leaf is one NUnit case. One method can hold several leaves through `TestCase` rows. The scan found 416 methods and 577 leaves in the LilToon folder. The classification of each leaf follows three buckets from the plan.

- Movable happy path. The test drives a plausible vendor material state through the material interpreter or an eligibility gate. A real material on the installed shader, loaded by the production GUID pin, gives the same proof. The interpreter entries and capture admission accept a real material unchanged.
- Synthetic falsifier. The test carries a Falsifier marker in its source, or it builds a synthetic evidence record by hand. It asserts an exact outcome or refusal member for an adversarial input no vendor produces.
- Exact-control. The test needs an exact keyword state, exact `ifex` condition text, an evidence-level absence or value injection, a schema-lacking stand-in, a refusal shape no vendor produces, a pinned attestation data row, or the `ApprovedRangeVendorScope` package.

Two facts from the body reads correct the section 5.2 scout guess. The design guess of about 150 movable leaves is close to the recount. The design guess of about 170 falsifier leaves is low, because the design counts only the marker falsifiers and the recount finds more exact-control rows than the guess. `[SOURCE]`

### 14.1 Counts per file

The table counts leaves. Every class in the folder holds one NUnit fixture.

|File|Movable|Synthetic falsifier|Exact-control|Leaves|
|---|---|---|---|---|
|`LilToonAdversarialTests.cs`|3|4|2|9|
|`LilToonAlphaMaskSemanticsTests.cs`|0|0|9|9|
|`LilToonAlphaTests.cs`|6|0|1|7|
|`LilToonAttestationTests.cs`|0|12|203|215|
|`LilToonCutoutAlphaTests.cs`|12|2|61|75|
|`LilToonCutoutSourceEligibilityTests.cs`|33|1|9|43|
|`LilToonIncludeTreeTests.cs`|0|0|3|3|
|`LilToonLayerAlphaTermTests.cs`|0|0|13|13|
|`LilToonMultiModeGateTests.cs`|0|1|6|7|
|`LilToonMultiResolutionTests.cs`|0|9|0|9|
|`LilToonMultiSourceEligibilityTests.cs`|0|21|0|21|
|`LilToonOpaqueConversionResultTests.cs`|0|0|2|2|
|`LilToonOpaqueTargetTests.cs`|0|4|22|26|
|`LilToonTransparentAlphaTests.cs`|11|6|73|90|
|`LilToonTransparentSourceEligibilityTests.cs`|42|0|6|48|
|Total|107|60|410|577|

Per-test class source. Each file names its own classes this way. The movable lists name every leaf. The falsifier lists are exactly the leaves whose method or its direct comment carries a Falsifier marker or a hand-built evidence record. The exact-control remainder follows the bucket rule above.

- `LilToonAdversarialTests.cs`. Movable: `DefaultMaterial_AlphaIsComplete`, `UnknownAlpha_StaysUnknown`, `Diagnostics_AreImmutable`. Falsifier: `AnalyzeBaseMaterial_Null_Throws`, `AnalyzeBaseMaterial_DestroyedMaterial_Throws`, `InterpretVerifiedMaterial_NullFeatures_Throws`, `InterpretVerifiedMaterial_NullMaterial_Throws`. Exact-control: `AnalyzeBaseMaterial_NonLilToonShader_IsUnsupported` and `PublicEntry_RefusesTheSameMaterialTheSeamFullyInterprets`, which need a shader that mimics the vendor name and fails attestation. No vendor produces that shader.
- `LilToonAlphaTests.cs`. Movable: `OpaqueVariant_IsConstantOne`, `OpaqueVariant_IgnoresColorAlphaAndMainTexAlpha`, `CoverageMechanism_KeepsAlphaUnknown` (2 leaves), `OpaqueMaterialWithIdMaskValueSetAnswersUnknownNotConstantOpaque`, `NonFiniteCoverageProperty_KeepsAlphaUnknown`. Falsifier: `MainTexture_RequestedButAbsentFromEvidence_RefusesInsteadOfDeclaredDefault`, a hand-built evidence record.
- `LilToonCutoutAlphaTests.cs`. Movable: `AlphaMaskMode1_IgnoresTheMainTextureAlphaChannel`, `AlphaMaskUnassigned_Mode1_Scale1Value0_IsUniformProvenOpaque`, `AlphaMaskUnassigned_Mode1_Scale0ValueHalf_IsUniformMustRemainTransparent`, `BilinearFootprintReachesHalfATexelOutsideTheHull_PointDoesNot`, `RepeatWrapCatchesATransparentTexelAcrossTheSeam_ClampFlipsConsistently`, `MirrorWrapImport_RefusesSamplingAtTheInterpretation`, `MismatchedWrapImport_RefusesSamplingAtTheInterpretation`, `TextureWithoutResolvableSourceIdentity_IsRefusedAndNeverBecomesAConstant`, `UnassignedMainTex_AtUnitTint_ProvesTheCornerTriangleOpaque`, `UnassignedMainTex_BelowCutoff_IsUniformMustRemainTransparent`, `AlphaPath_SceneOnlyMainTex_IsUnstableIdentity`, `AlphaPath_SceneOnlyAlphaMask_IsUnstableIdentity`. Exact-control: 61 leaves of the keyword-gate, `ifex`, marker falsifier, and uniform-grid rows per the bucket rule. Falsifier: 2 leaves.
- `LilToonCutoutSourceEligibilityTests.cs`. Movable: `DistanceFadeNonFinite_RefusesConversionPropertyNotFinite`, `DepthTestLessWithPolicyConvertsAndFlagsDivergence`, `DepthTestLessWithoutPolicyStillRefuses`, `DepthTestGreaterWithPolicyStillRefuses`, `DepthTestLEqualWithPolicyCarriesNoDivergence`, `CustomRenderQueue_RefusesBeforeCanonicalization`, `CustomRenderType_RefusesBeforeCanonicalization`. Exact-control: 9 leaves. They are the six request and constant rows that read no material, the fresh-defaults admission row `CanonicalDefaultStandIn_IsConvertibleWithRealLilToon`, which runs the installed shader and is not a moved gate row, and the two absence rows `DistanceFadeAbsent_RefusesConversionPropertyAbsent` and `MaterialWithoutTheConversionSchema_RefusesAsAbsent`. A compiled shader cannot lack a declared property or a missing schema, so those two stay. Falsifier: 1 leaf, `DistanceFadeStrengthOn_RefusesUnsupportedDistanceFade`. The 2026-11-10 part B review moved the two queue and render-type rows from exact-control to movable. The 2026-11-10 part C review found the criterion sentence over-claimed: 26 gate leaves still sat exact-control while their transparent twins ran on the real shader. Those 26 leaves are the four clip-threshold rows, the 15 leaves of `NonCanonicalRenderState_RefusesWithTheNamedGate`, the 3 leaves of `OpaqueEquivalentBlendState_IsConvertible`, `NonFiniteConversionProperty_RefusesAsNotFinite`, and the 3 leaves of `UngatedProperties_DoNotAffectEligibility`. A 2026-11-10 probe checked every property those rows set against the installed cutout shader. Each property is declared, and SetFloat round-trips on each. All 26 leaves moved to `ConversionEligibleRealSource` and passed. The criterion for both eligibility files reads: an eligibility gate row is movable when every material state it drives is reachable on the installed shader through the properties the row names, and exact-control when the row needs a state the pinned environment cannot supply. Reachability is probed before the move, not assumed.
- `LilToonTransparentAlphaTests.cs`. Movable: `CutoutFamily_ComposesTheReplaceLayerIdentically`, `NoAlphaSourceImport_CollapsesTheTextureArmToTheTintConstant`, `PointProvesAndBilinearRefusesTheHalfTexelOutsideHull`, `RepeatWrapsIntoTheTransparentTexelAndClampDoesNot`, `AlphaMaskMode1_IgnoresTheMainTextureAlphaChannel`, `AlphaMaskUnassigned_Mode1_Scale1Value0_IsUniformProvenOpaque`, `AlphaMaskUnassigned_Mode1_Scale0ValueHalf_IsUniformMustRemainTransparent`, `MissingMainChain_Mode2SaturatedValue_IsUnknownAndNeverProvesTheHole`, `MissingMainChain_Mode2SampledValue_IsUnknownAndNeverProvesTheHole`, `UnassignedMainTex_AtUnitTint_ProvesTheCornerTriangleOpaque`, `UnassignedMainTex_SubUnitTint_IsUniformMustRemainTransparent`. Exact-control: 73 leaves. Falsifier: 6 leaves.
- `LilToonTransparentSourceEligibilityTests.cs`. Movable: all 42 leaves of its 23 gate-row methods, from `CustomRenderQueue_RefusesUnsupportedRenderQueue` through `CutoutEligibility_IsUnchangedByTheTransparentFamily`. Every row sets a queue, a tag, a scalar, or a render-state value on the source material and expects one named eligibility answer. All states are reachable on a real material. Exact-control: 6 leaves. Falsifier: 0.
- `LilToonAttestationTests.cs`. Exact-control: 203 leaves of keyword-grant parse rows, `ifex` condition rows, canonical digest rows, GUID pin rows, and version rows. Falsifier: 12 marker leaves. Movable: 0. The file reads attestation data. No material state appears.
- `LilToonMultiResolutionTests.cs` and `LilToonMultiSourceEligibilityTests.cs`. All leaves are marker falsifiers (9 and 21). They inject Multi keyword sets and container states no vendor write produces.
- `LilToonMultiModeGateTests.cs`, `LilToonOpaqueTargetTests.cs`, `LilToonLayerAlphaTermTests.cs`, `LilToonAlphaMaskSemanticsTests.cs`, `LilToonIncludeTreeTests.cs`, `LilToonOpaqueConversionResultTests.cs`. Each stays whole. The mode-gate and layer and mask rows need exact keyword states. The opaque-target rows pin the canonical conversion tuple against a stand-in target. The include-tree rows parse source text. The two result rows pin an exact conversion record.

Two tests in the folder run on the installed shaders and are not stand-in users. They are `CanonicalDefaultStandIn_IsConvertibleWithRealLilToon` in `LilToonCutoutSourceEligibilityTests.cs` and `FreshTransparentStandIn_IsConvertibleWithRealLilToon` in `LilToonTransparentSourceEligibilityTests.cs`. They prove the GUID-load helper pattern this stage follows. `[SOURCE]`

### 14.2 lilToon stand-in users outside the LilToon folder

|File|Stand-in leaves|Class summary|Batch|
|---|---|---|---|
|`Tests/Editor/Build/AlphaSeparationPreparationTests.cs`|39|37 methods drive the cutout and transparent conversion fixtures through preparation. Most are happy-path movable candidates. Some set keyword states.|Part A, after the LilToon folder|
|`Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`|15|Family selection and interpreter rows on a name-mimic stand-in. Shared with stage 4.|Part B|
|`Tests/Editor/Build/AlphaSeparationApplyTests.cs`|9|Conversion route rows on the cutout stand-in. Shared with stage 4.|Part B|
|`Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`|2|Two rows. Shared with stage 4.|Part B|
|`Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`|0|No lilToon stand-in use.|None|

### 14.3 Batch order, lowest risk first

|Batch|File|Move count|Reason for the order|
|---|---|---|---|
|1|`LilToonAlphaTests.cs`|6 leaves|Smallest file. One helper swap per method. No render state.|
|2|`LilToonAdversarialTests.cs`|3 leaves|Same entry as batch 1.|
|3|`LilToonCutoutSourceEligibilityTests.cs`|5 leaves|One eligibility entry. The file holds the GUID-load precedent.|
|4|`LilToonCutoutAlphaTests.cs`|12 leaves|Texture and scalar states on the cutout family.|
|5|`LilToonTransparentAlphaTests.cs`|11 leaves|Same rows on the transparent family.|
|6|`LilToonTransparentSourceEligibilityTests.cs`|42 leaves|Render-state rows. Larger surface.|
|7|`AlphaSeparationPreparationTests.cs`|39 leaves|Conversion route through preparation.|
|8|Part B shared files|26 leaves|`UnityMaterialSemanticsTests.cs`, `AlphaSeparationApplyTests.cs`, `AmusePlatformFinishPluginTests.cs`. Stage 4 shares them.|

Batch rows 1 to 5 are the part A scope of the plan on 2026-11-10. Batch row 6 closed on 2026-11-10 in part B. Batch row 7 opened in part B on 2026-11-10 and its first sub-batch closed the same day. Batch row 8 stays open. The counts above are the synthetic falsifier baseline. A move must not change the falsifier count of its file. Stage 3 task 3.5 needs one RED per moved file and one negative assertion per moved file.

### 14.4 Part B batch record on 2026-11-10

This subsection records batch 6, the batch 7 first sub-batch, and the part A carry-overs. All runs are focused EditMode runs on the dev editor instance. The active instance answered `Application.dataPath` equal to this repository Assets folder before every run. All counts are observed. `[MEASURED]`

Batch 6, `LilToonTransparentSourceEligibilityTests.cs`. All 42 movable leaves moved. Every gate-row method builds its material through one `RealTransparentSource` helper that loads the installed transparent shader by the production GUID pin through `RealLilToonMaterial`, and `CutoutEligibility_IsUnchangedByTheTransparentFamily` builds the installed cutout shader material the same way. The six exact-control leaves keep their stand-ins: the five schema and constant rows read no material, and `MaterialMissingTheRecipe_RefusesConversionPropertyAbsent` needs a schema-lacking source. The kept-stand-in reason line sits above the class. Focused run of three classes together: 181 leaves, 181 passed, 0 failed, 144.3 seconds. That run also covers the cutout eligibility class with its two moved rows, and it is the post-revert rerun of `LilToonTransparentAlphaTests` that closed the part A concern. RED: a scratch mutation disabled the render-queue gate in `LilToonTransparentSourceEligibility.cs`. The named failure set is `CustomRenderQueue_RefusesUnsupportedRenderQueue` and `QueuesOutsideTheTransparentBucket_StillRefuse`, both moved rows, message `Expected: Refused, But was: Convertible`, over a 48-leaf class run. Reverted. The `Editor/Semantics/LilToon` diff is clean. The negative not-Unknown guard is the refusal-carrying message on `AssertConvertible` plus the divergence-flag assert on `DepthTestLessWithPolicyConvertsAndFlagsDivergence`, the same shape the cutout eligibility file uses.

Batch 7 first sub-batch, `AlphaSeparationPreparationTests.cs`. Five methods moved, 7 leaves: `EnabledSecondLayer_MaterialPreparesAndSplits`, `SecondLayerAtMeshUvMode_ProvesThroughNamedChannel` (3 leaves), `NoAlphaSourceTexture_ReportsTheMistakeAndStillSplits`, `StreamingMipmapProvesThroughTheReadableClone`, and `GeneratedSubAssetTexture_IsAdmittedAndProvenOpaqueEndToEnd`. Each row builds its material on the installed cutout shader by the GUID pin and drives `RunProductionBarrier`, the no-seam production route. The texture staging helpers stay. Focused run by test name: 7 leaves, 7 passed, 0 failed, 11.3 seconds. RED: a scratch mutation made `ConvertAdmittedMaterial` in `AlphaSeparationMaterialConverter` refuse the whole cutout family as unsupported at conversion admission. The named failure set is `EnabledSecondLayer_MaterialPreparesAndSplits`, `GeneratedSubAssetTexture_IsAdmittedAndProvenOpaqueEndToEnd`, and `StreamingMipmapProvesThroughTheReadableClone`, all moved rows, over the same 7-leaf filter. Reverted. The converter diff is clean. A kept-stand-in reason line sits above the class.

Route decision for batch 7. A preparation row moves when two facts hold. First, every material state the row drives is reachable on the installed shader. Second, the row can run the production delegates with no injected seam, because the verified fixture seams dispatch by fixture-shader reference and select nothing for a real shader. The moved rows therefore ride `RunProductionBarrier`, which asserts that the installed attestation raises no consent subjects. The two part A exemplar rows followed this form.

Batch 7 census, corrected by the part C recount on 2026-11-10. By actual source count, 44 leaves over 42 methods in `AlphaSeparationPreparationTests.cs` create the cutout or transparent conversion fixtures. Three of those methods are the part A exemplar rows that run real materials: `CutoutFixtureSlotPreparesAndCarriesTheOpaqueMappingWithRealLilToon`, `TransparentFixtureSlotPreparesAndCarriesTheOpaqueCloneContractWithRealLilToon`, and `CutoutSharedSourceReusesOneAvatarWideCloneWithRealLilToon`. The part B sub-batch moved four fixture methods with 6 leaves: `EnabledSecondLayer_MaterialPreparesAndSplits`, `SecondLayerAtMeshUvMode_ProvesThroughNamedChannel` (3 leaves), `NoAlphaSourceTexture_ReportsTheMistakeAndStillSplits`, and `StreamingMipmapProvesThroughTheReadableClone`. The fifth part B method, `GeneratedSubAssetTexture_IsAdmittedAndProvenOpaqueEndToEnd`, builds its material directly on the installed shader and sits outside this census. Nine real leaves over seven methods, plus the 35 leaves of the full remaining list below, add to 44 over 42. This census supersedes the 39-leaf figure in section 14.2. The part C first sub-batch moved the five policy rows of group 3 on 2026-11-10. After that sub-batch, the remaining 30 leaves over 30 methods still build stand-in conversion materials, in the three groups that stay open.

1. Arm-fixture rows, 16 leaves over 16 methods. `CutoutArmFixture` serves 9: `LilToonCutoutNonIdentityStSlotPreparesNoOpaqueCandidates`, `CutoutCutoffSlotOutcomesThroughTheFullPath`, `CutoutOptionalCoveragePathsRefuseEndToEnd`, `UnsupportedCutoutTextureEvidenceRefusesEndToEnd`, `DissolveRefusalReportNamesMaterialPathAndFeature`, `CutoutNonSingletonAnimationRefusesAtItsRelevanceLayer`, `CutoutAnimationAtTheSerializedValuePrepares`, `CutoutAnimationOfDegenerateStComponentVectorRefuses`, and `CutoutConversionRecipeAnimationRefusesWhenOverwrittenAndPreparesAtCanonical`. `TransparentArmFixture` serves 2: `TransparentNonSingletonAnimationRefusesAtItsRelevanceLayer` and `TransparentDistanceFadeStrengthRefusesTheSlotAsRetainedByFeature`. `DepthTestPolicyArmFixture` serves `DepthTestDivergencePolicyConvertsWholeAndMixedSplits`. `TransparentDitherScenario` serves `AnimatedUseDither_IsIgnoredAsProvablyInert`. `LocalityArmFixture` serves 3: `RefusedTransparentSlotLeavesItsAdmittedSiblingsConverted`, `ConversionRefusedSlotNamesItsMaterial`, and `ConversionRefusedSlotNamesItsPerGateDetail`. Work: one installed-shader mode at each arm `Create` that swaps the material to the installed shader by the production GUID pin and sends `Run` to `RunProductionBarrier`.
2. Direct conversion-fixture rows, 5 leaves over 5 methods: `AllFailSlotReportsOneEntryNamingTheLastRefusingMaterial`, `TransparentAllUnknownOutcomesNeverBecomeProvenOpaque`, `Dxt1SrgbMask_WhiteRegionTriangleProvesOpaque`, `Dxt1SrgbMask_SeamBlendTriangleStaysUnproven`, and `CutoutRefusedSiblingLeavesTheConvertibleRendererUncorrupted`. Work: the same material swap and production route the moved part B rows use.
3. Policy rows, 5 leaves over 5 methods, moved by the part C first sub-batch on 2026-11-10: `PreserveTransparencyMipPolicyScopesTheProofEndToEnd`, `PreserveTransparencySizePolicyScopesTheProofEndToEnd`, `TransparencyNoiseGatePolicyDrivesTheProofEndToEnd`, `ShaderCutoffSourcesStayPolicyInertEndToEnd`, and `AlphaOpaqueBoundScalesTheProofEndToEnd`. `RunPolicyArm` builds the cutout and alpha arm materials on the installed shaders by the production GUID pin, rides `RunProductionBarrier`, and no longer passes the Alpha kind's `VerifiedFamilyConversion` argument. The fixtures stay for texture staging. Focused run by test name: 5 leaves, 5 passed, 0 failed, 10.1 seconds. `[MEASURED]` RED: a scratch mutation made `ProofMipCapFrom` in `RuntimeStateResolution` ignore the stored cap and always answer the full chain. The named failure is `PreserveTransparencyMipPolicyScopesTheProofEndToEnd`, message `a fade above the mip cap must not stop the proof`, `Expected: 1, But was: 0`, over the same 5-leaf filter. Reverted. The `Editor/Analysis` diff is clean. Post-revert rerun over the same filter after the revert compile: 5 leaves, 5 passed, 0 failed, 18.0 seconds. `[MEASURED]`
4. Stays, 9 leaves over 9 methods, exact-control: `ModeOneMultiMaterialReachesResolutionAndRefusesClosedUntilRowsLand`, `ModeZeroMultiMaterialReachesResolutionAndRefusesClosedUntilRowsLand`, `ByteChangedInRangeMultiKeepsItsRowsAndNeverSeparates`, and `ModeTwoMultiMaterialWithDistanceFadeStrengthRetainsThroughDelegation` hold Multi keyword schema stand-ins. `ByteChangedInRangeInstallSeparatesSilentlyThroughTheRealPipeline` and `OutOfRangeInstallStillAsksThroughTheRealPipeline` need the `ApprovedRangeVendorScope` package state. `UnsupportedLilToonFamilyRefusesOnlyItsOwnSlot` needs a shader that mimics the vendor name and fails attestation. `CutoutSlotPreparesBesideAPoiyomiSlotWithIndependentClones` and `MixedPoiyomiAndCutoutAdmittedSetMapsThroughEachOwnConversion` keep Poiyomi stand-in siblings owned by stage 4.

Groups 1 and 2 closed in part D on 2026-11-10, all 21 leaves over 21 methods. The five arm classes took a clean cutover instead of a flag: every call site of `CutoutArmFixture`, `TransparentArmFixture`, `DepthTestPolicyArmFixture`, `TransparentDitherScenario`, and `LocalityArmFixture` belonged to a moved row, so each class builds its material on the installed shader by the production GUID pin and every `Run` rides `RunProductionBarrier`, the pattern the part C `RunPolicyArm` cutover set. The three `LocalityArmFixture` slots swap to real materials the same way, the Poiyomi slot through `NewRealPoiyomiOpaqueMaterial`. The dead `VerifiedFamilyConversion` helper is removed. Three row-level adaptations carry their oracles on the production route: the dither row's clone-shader assertion names the installed main shader the way the part A clone-contract row does, the distance-fade row's slot-level precondition uses the production selection and closed capture, and the conversion-recipe row's counting seam becomes the named-refusal observable, because a conversion-first ordering lands the refusal at the conversion gate instead of the overwrite gate. Focused run by test name over all 21 moved leaves: 21 passed, 0 failed, 98.1 seconds. `[MEASURED]` RED for the arm group: a scratch mutation dropped `UseDither` from the cutout interpreter's coverage-gate list in `LilToonCutoutMaterialSemantics.cs`. Observed by name over a two-row filter: `CutoutOptionalCoveragePathsRefuseEndToEnd`, a moved row, message `_UseDither: an active optional coverage path must refuse at alpha resolution`, `Expected: 1, But was: 0`. The dither scenario row passed under the same mutation, because its cutout control refuses through the animation-relevance request, not the coverage gate. Reverted. RED for the direct group: a scratch mutation built the all-fail slot entry from the first collected record in `AlphaSeparationPreparation.cs`. Observed by name: `AllFailSlotReportsOneEntryNamingTheLastRefusingMaterial`, message `Expected: String containing "AMUSE all-fail last refusing", But was: ... (AMUSE all-fail first refusing) ...`. Reverted. Both reverts verified by an empty `Editor/Semantics/LilToon` and `Editor/Build` diff. Post-revert rerun over the three RED rows: 3 passed, 0 failed, 19.3 seconds, so the dev editor holds the reverted production state. `[MEASURED]` The not-Unknown guards ride the moved rows unchanged: the cutoff row's unresolved-alpha refusal with zero candidates, the non-singleton rows' named refusals with zero candidates, the locality alpha-resolution refusal with `the Unknown transparent triangle must not become ProvenOpaque`, and the direct group's `TransparentAllUnknownOutcomesNeverBecomeProvenOpaque` and DXT1 seam rows. The nine stay rows carry per-row kept-stand-in reason lines dated 2026-11-10, grouped as exact falsifier control (the four Multi rows), a source the pinned environment cannot supply (the two `ApprovedRangeVendorScope` rows), an input no vendor produces (the name-mimic row), and stage 4 scope coupling (the two Poiyomi sibling rows).

Corrections against the part B list of 37 remaining rows. That list included `PreserveFold_RebuiltPlan_RequiresAnySplitTracksSurvivingSplits` and `EachSlotSelectsItsOwnCoveragePair`, whose materials come from `AlphaSeparationSplitTests.SplitAlphaMaterial`, a Poiyomi split stand-in, and not from a lilToon conversion stand-in. They stay with the stage 4 scope. The same list omitted the `Dxt1SrgbMask` pair, which group 2 above carries. The corrected census supersedes the 37-row figure.

Carry-over 1, cutout alpha RED by name. The part A mutation went back in: `LilToonAlphaMaskSemantics.cs` treats mode 1 as off through one local flag at the five mode dispatch sites. Focused class run: 75 leaves completed, 3 failures, all by name: `AlphaMaskMode1_IgnoresTheMainTextureAlphaChannel`, which is a moved row, message `Expected: ProvenOpaque, But was: MustRemainTransparent`, `AlphaMaskMode1_WithAllWhiteMask_ProvesTriangleDespiteSubUnitColorAlpha`, and `AlphaMaskMode1_WithSaturatedValue_IsUniformProvenOpaque`, whose message `the verdict must be uniform, needing no triangle` is the not-Unknown guard. Part A observed 4 failures by count. This rerun observed 3 by name. The difference stands as observed. Reverted. The `Editor/Semantics/LilToon` diff is clean.

Carry-over 2, kept-stand-in reason lines. Nine whole-stay LilToon files carry one principle-2 reason line above the class, each checked against the file before the write. `LilToonAlphaMaskSemanticsTests`, `LilToonMultiResolutionTests`, and `LilToonMultiSourceEligibilityTests` carry an input no vendor produces. `LilToonAttestationTests`, `LilToonLayerAlphaTermTests`, and `LilToonMultiModeGateTests` carry exact falsifier control. `LilToonIncludeTreeTests`, `LilToonOpaqueConversionResultTests`, and `LilToonOpaqueTargetTests` carry an independent outcome oracle.

Carry-over 3, the queue row classification and the criterion correction. The recount classified the transparent queue row movable and the analogous cutout queue and render-type rows exact-control. The cutout `CustomRenderQueue_RefusesBeforeCanonicalization` and `CustomRenderType_RefusesBeforeCanonicalization` rows moved to the real cutout source helper and passed in the batch 6 run. The first criterion sentence said one criterion, reachability, covers both files. The 2026-11-10 part C review found that sentence over-claimed: 26 cutout gate leaves still sat exact-control while their transparent twins moved. A 2026-11-10 probe checked the properties those 26 leaves set against the installed cutout shader. Every property is declared and SetFloat round-trips, so all 26 leaves moved to the real cutout source and passed in a 43-leaf class run on 2026-11-10. The corrected criterion reads: an eligibility gate row is movable when every material state it drives is reachable on the installed shader through the properties the row names, and exact-control when the row needs a state the pinned environment cannot supply. The exact-control stays in the cutout file are the absence and schema rows, which a compiled shader cannot express. Section 14.1 and the per-test list carry the new bucket split.

Carry-over 4, the post-revert transparent alpha rerun. The 181-leaf batch 6 run includes the 90 leaves of `LilToonTransparentAlphaTests` on the post-revert tree. It passed 181, 0 failed. The part A concern is closed.

Leaf counts did not change. The `[Test]` and `[TestCase` attribute counts equal `HEAD` for all three edited test files: 22 and 26, 22 and 21, and 88 and 3.

## 15. Poiyomi transient-unlock stand-in recount on 2026-11-10

This section answers plan task 4.1. It is the reconciliation of the design section 4.4 guess that about 60 transient-unlock tests build stand-in locked pairs. A source scan of 2026-11-10 listed every test under `Packages/com.alrauna.amuse/Tests/Editor/Build/` that builds a stand-in locked pair through `TransientUnlockTestLifecycle.LockedMaterial`, `LockedMaterialWithOriginal`, `LockedConvertibleMaterialWithOriginal`, `LockConfiguredMaterial`, `LockedSourceFixture.Locked`, or the plugin-test local `LockedMaterial` helper. A leaf is one NUnit case. The same day measured the exact flip-failing set: a scratch edit dropped the `?? (_ => true)` fallback at the three `TransientUnlockTestKnobs.OriginalAttestation` sites, so a null knob ran the production `LockedMaterialIdentity.OriginalShaderAttested`, and four focused class runs observed the failures. Every run is a focused EditMode run on the dev editor instance, which answered `Application.dataPath` equal to this repository Assets folder before every run. `[MEASURED]`

### 15.1 The stand-in pair census

A stand-in pair is a locked material whose recorded original is the fixture stand-in shader (`Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest`, the written partner of the fixture lock shader). The production attestation refuses that original, so a run that consults the attestation with a stand-in pair fails under the flip.

|File|Pair-keeping leaves|Pair-keeping methods|Fail under the flip|Green under the flip|
|---|---|---|---|---|
|`TransientUnlockSwapInTests.cs`|12|12|10|2|
|`TransientUnlockWindowCloseTests.cs`|16|16|7|9|
|`TransientUnlockTransformationTests.cs`|16|14|12|4|
|`AlphaSeparationApplyTests.cs`|1|1|1|0|
|`AmusePlatformFinishPluginTests.cs`|1|1|0|1|
|Total|46|44|30|16|

Rows of these files that hold no stand-in pair are not in the table: 2 swap-in leaves (the stale-tag unlocked stand-in and the direct availability probe), 5 window-close leaves (the committed-clip and cycle fixtures), 55 apply leaves (the separation source fixtures, the split stand-in, and the real lilToon and real Poiyomi rows), and the plugin-test rows that pin production attestation refusals on locked-looking stand-in materials. The plugin refusal rows deliberately run the production default, so the flip does not touch them.

### 15.2 Why the 16 green stand-in leaves stay green under the flip

Each green row exits before the attestation gate, or its assertions hold with and without an open pair. The flip-failing run observed every one of them passing.

- `TransientUnlockSwapInTests.NoCycleStepDeletesOrMutatesTheLockedOriginal`. The swap-in consults the gate, but the assertions only demand that no cycle step deletes or mutates the locked original. They hold when no pair opens.
- `TransientUnlockSwapInTests.WindowWithoutConsentGrantNeverOpens`. The consent gate returns before the attestation gate. The pair is never consulted.
- `TransientUnlockWindowCloseTests.TheFallbackNeverDestroysTheLockedOriginal`. No `BeforeClose` callback exists in that test. Under the production attestation the stand-in pair never opens, so the survival assertions hold trivially. The correction was recorded on 2026-11-10.
- `TransientUnlockWindowCloseTests.CloseWithoutOpenPairsIsANoOp`. `SkipSwapIn` skips the swap-in, so no gate runs.
- The seven direct window-close fixtures (`InvertCommittedCurves_*`, `CurveInversionWasComplete_*`, `ExceptionDuringPairReversionDoesNotAbortSubsequentPairs`, `RunAbortSweepRevertsOpenPairAndSweepsSeparationClones`). They call the close statics directly on fixture pairs. No build runs, so no gate consults.
- `TransientUnlockTransformationTests.SuffixedBindingsSurviveSwapInTransformationAndReversion`. Its assertions cover committed-graph binding survival and hold without an open pair.
- `TransientUnlockTransformationTests.SlotLessPairDispositionIsPinned` (2 leaves). A locked material on no renderer and no clip is never grouped, so the gate never consults.
- `TransientUnlockTransformationTests.ClosePassAddsNoAvatarEntryWhenTheBarrierAlreadyRefused`. The barrier refusal holds with and without an open pair, so the outcome is invariant.
- `AmusePlatformFinishPluginTests.APlayPathBuildAdmitsALockedMaterialAndOpensTheWindowWithoutAnyVendor`. It passed its attestation explicitly before this stage, so the flip never reached it.

### 15.3 The measured flip-failing set, 30 leaves

The four class runs reported `failures_capped` false in every run, so these lists are complete. The combined first run over all four classes is superseded by them, because its failure list was capped.

`TransientUnlockSwapInTests`, 10: `ABindingOnAnotherRendererPathDoesNotRefuseThisLockedMaterial`, `AlignmentRefusalDetailReachesTheSlotRefusal`, `AnimationMismatchRecordsTheRefusalOnEverySlot`, `CloneKeepsTheExactNameOfTheOriginal`, `NoOpRestoreIsCaughtByTheVerificationContract`, `OneCloneServesEverySlotHoldingTheSameLockedMaterial`, `PartialRestoreProducesTheNamedRefusal`, `PropertyBlockOnOneRendererRefusesEverySlotOfThatLockedMaterial`, `SeventhFactCatchesAConsistencyBreakAfterRestore`, `TheRestoreConsumesAPersistedClone`.

`TransientUnlockWindowCloseTests`, 7: `ABlendTreeResidentSwapClipRevertsToL`, `ACloneThatAnUnrecordedSlotNamesIsRetainedAndThePairStaysOpen`, `AnAlreadyLockedLookingCloneStillRevertsToTheLockedOriginal`, `AReversionWithoutProvenInversionNamesTheRetainedClone`, `CloseInvertsTheRemapBackToL`, `CloseRevertsEveryOpenPairToTheLockedOriginal`, `LockedMaterialShipsTheUntouchedLockedOriginal`.

`TransientUnlockTransformationTests`, 12: `AClipOnlyLockedMaterialGetsItsCloneAndRemaps`, `AMixedClosedSetDegradesPerSlot`, `AnUnlockFailureNeverWidensRefusalBeyondTheOwningSlot`, `APlayPathCloseNeverCallsTheVendorAndLeavesTheShippedStateAlone`, `APlayPathCloseThatCannotProveTheCurveInversionRetainsTheClone`, `ASweptCanonicalOutputStillRevertsThePairToL`, `CloseTimeGraphRefusalReportsOneAvatarEntry`, `LockedMaterialReferencedOnlyByAClipMakesTheWindowEligible`, `NonPlayPathAdmitsLockedMaterialAndOpensTheWindowWithoutVendor`, `TransientUnlockRestoreMismatchEntryCarriesTheMismatchFact`, `UnlockedSlotRunsTheWholePipelineInsideTheWindow(False)`, `UnlockedSlotRunsTheWholePipelineInsideTheWindow(True)`.

`AlphaSeparationApplyTests`, 1: `PropertyBlockAddedAfterSwapInRefusesAtApply`.

Per-class counts: swap-in 14 leaves, 10 failed, 4 passed. Window close 21 leaves, 7 failed, 14 passed. Transformation 16 leaves, 12 failed, 4 passed. Apply 56 leaves, 1 failed, 55 passed. A control run held the availability pair (2 leaves) and the plugin window row (1 leaf) at 3 passed. `[MEASURED]`

### 15.4 Real Poiyomi tests in the unlock family

These rows ran the installed Poiyomi shader before this stage and need no stand-in pair. They stayed green under the flip and after it.

- `AlphaSeparationApplyTests.WhollyOpaqueSlotReplacesOnlyItsMaterialWithRealPoiyomi` and `RefusedTextureSharedByTwoAdmittedSwapValuesYieldsOneSlotRefusalRecord` drive real bundled Poiyomi source materials.
- `TransientUnlockAvailabilityTests.BundledRowLockedMaterialMakesTheWindowEligibleAfterAdmission` locks a fixture around the installed bundled row and pins the production eligibility answer.
- `LockedMaterialIdentityTests.BundledRowLockedMaterialPrecheckPassesAfterAdmission` pins the production precheck on the installed bundled row.
- `BundledLockedOriginalRealLockTests` and `RealThryLockCharacterizationTests` run the real lock behind `AMUSE_REAL_THRY_LOCK`. They pass explicit attestations and are outside the stand-in family.

### 15.5 Resolution of the design count

The design section 4.4 guess of about 60 stand-in pair tests was high. The recount finds 46 leaves over 44 methods. Of those, 30 leaves consult the default and fail under the flip, and 16 leaves never consult it. The 30 rows are the rows the explicit stubs must cover, and they do.
