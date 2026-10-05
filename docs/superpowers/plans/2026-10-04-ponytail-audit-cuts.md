# Ponytail audit cuts implementation plan

Date: 2026-10-04. Executes the design
`docs/superpowers/specs/2026-10-04-ponytail-audit-cuts-design.md` from the
audit `docs/superpowers/investigations/2026-10-04-ponytail-repo-wide-audit.md`.

Branch: `chore/ponytail-audit-cuts`, base `main` at `4a4ba3f`. One branch
carries every task. No staging, no commits, no push. The controller runs
Unity in the dev editor instance only. No task needs the Census Lab
project.

Unity MCP discipline: before any tool use against an editor instance,
enumerate reachable instances read-only, inspect `Application.dataPath`,
and require the exact match `<repo-root>/Assets`. Stop on any mismatch.

Symbol anchors below were verified against the tree of 2026-10-04 with
line numbers. Lines drift with every task. Relocate by symbol first,
line second. Every deleted `.cs` file deletes its `.meta` file in the
same task. Every new `.cs` file takes its `.meta` from the Unity refresh.
Run `git diff --check` before reporting.

A filtered run that reports zero tests is a failure. Record every count
with its date.

## Task 0: baseline

1. Refresh Unity. Confirm the console holds no compile errors.
2. Run the full EditMode population of both assemblies,
   `Alrauna.Amuse.Tests.Editor` and
   `Alrauna.Amuse.Research.Tests.Editor`. Record both counts. This run is
   the reference baseline. Every failure in it is attributed and written
   down. Any later task that produces a failure not on this list stops
   the work.

No production file changes in this task.

## Task 1: dead members and wrappers

Mechanical deletes. Every cut is verified dead by the audit and re-verified
by the anchor pass of 2026-10-04.

1. F19, `OptimizerMergeObservation`: delete `RendererSnapshot.GameObjectName`,
   `RendererSnapshot.SlotTextureNames`, `RendererSnapshot.HasUv0`,
   `ContainsAll`, `MainTextureName`, and their writes inside `Observe`
   (`Packages/com.alrauna.amuse/Tests/Editor/Build/OptimizerMergeObservation.cs`
   lines 42, 45 to 46, 48 to 53, 111 to 124, 143 to 145, 158 to 159).
   Keep `RendererName`, `TriangleKeys`, `SlotShaderNames`, and the phase
   snapshot. Do not touch `UnityRendererAlphaSnapshot.HasUv0`. It is alive.
2. F21, `LockedMaterialIdentity`: record the count of
   `LockedMaterialIdentityTests` before editing. Then delete
   `AllLockedGuids`,
   `HasGeneratedShaderAsset`, the two `Serialization` members, the
   constructor plumbing, the `Classify` forwarding arguments, and the dead
   reads in `SerializationOf` (`Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs`
   lines 49 to 64, 77 to 96, 130 to 132, 237 to 238). Rewrite the class
   doc sentence at lines 20 to 23. Keep the tag-name constant. In
   `LockedMaterialIdentityTests`, drop the two named constructor
   arguments at all four construction sites and delete the dead-fact
   assertions at lines 91 to 96 and 117 to 118 with their premise
   comments.
3. F30, `BehaviourIdentity`: delete `AllowedIdentityValues`,
   `AllowedIdentities`, and the loop in `IsAllowed`
   (`Packages/com.alrauna.amuse/Editor/Host/BehaviourIdentity.cs` lines
   52 to 60, 82 to 87). Delete `AllowlistStartsEmpty` from
   `BehaviourIdentityTests` (lines 35 to 39).
4. F32, `LilToonFixtureTestBase`: delete `MipCount` and `ReadMipLevel`
   with their docs (lines 447 to 460). Zero callers on the tree.
5. F40, `PoiyomiMaterialSemantics`: delete `AreExactlyZero` (lines 2701
   to 2712). Widen `FirstFailedZeroGate` from private to internal (lines
   2714 to 2737). In `PoiyomiTextureEvidenceTests`, flip the four call
   sites at lines 277 to 315 to `FirstFailedZeroGate` and assert the
   returned property name or null.
6. F43, `CensusVendorPresence`: delete the `Family` property, its
   constructor parameter, the assignment, and the argument at the
   construction site (`Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/CensusVendorProbe.cs`
   lines 25, 42, 50, 102 to 108). The `CensusVendorFamily` enum and the
   `Probe` parameter stay.
7. F44, `LilToonIncludeTree`: delete `RootFullPath`, its assignment, and
   the constructor or factory parameter that exists only to feed it
   (`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
   lines 27, 30 to 38). Check `ForTests` at edit time. If a parameter has
   another use, cut only the property and assignment. Update the two
   `Tree()` builders if the factory signature changes.
8. F47, `UnityAlphaFieldEvidence`: delete the parameterless
   `HostCapabilityCheckPasses` (lines 631 to 638). In
   `UnityAlphaFieldEvidenceTests`, `TheHostCapabilityCheckPassesOnThisHost`
   passes `TextureChannel.Alpha` explicitly (lines 1728 to 1732).
9. F48, `TransientUnlockSwapIn`: delete the dead
   `mismatchReason = null` at line 324.
10. Refresh Unity. Confirm both assemblies compile.
11. GREEN, in order, recording each count: `AaoMergedConsumptionTests`,
    `DaoMergedConsumptionTests`, `LockedMaterialIdentityTests`,
    `BehaviourIdentityTests`, `LilToonMultiResolutionTests`,
    `LilToonCutoutAlphaTests`, `LilToonTransparentAlphaTests`,
    `PoiyomiTextureEvidenceTests`, `VendorReachabilityTests`,
    `LilToonSourceAttestationTests`, `LilToonAttestationTests`,
    `UnityAlphaFieldEvidenceTests`, `TransientUnlockSwapInTests`,
    `TransientUnlockTransformationTests`.

Plain green run: every deleted member is dead by search, so no test can
fail by design beyond the edited pins.

## Task 2: standard-library swaps

1. F38, `EffectiveMaterialMaterialization.BlockStateEquals`: keep the
   guards, replace the index loop with `Enumerable.SequenceEqual`
   (`Packages/com.alrauna.amuse/Editor/Host/EffectiveMaterialMaterialization.cs`).
2. F39, `UnityAnimationEvidenceCapture.ContainsOrdinal`: replace the
   method body and its four call sites with
   `Enumerable.Contains(source, value, StringComparer.Ordinal)` and delete
   the helper (call sites at lines 976, 986, 994, 1002).
3. F41, `UnityAlphaFieldEvidence.MatchesExpectedPattern`: keep the null
   guard, replace the loop with `Enumerable.SequenceEqual`.
4. F45, `CensusAggregateReport.Freeze`: replace the capacity and add-loop
   with the `Dictionary` copy constructor behind the existing null guard
   (`Packages/com.alrauna.amuse.research/Editor/Census/CensusAggregateReport.cs`
   lines 177 to 190).
5. F46, `ReferenceFixtureData.IsFinite`: delete the helper and call
   `float.IsFinite` at its two call sites, lines 283 and 302.
6. F34, `AvatarCensusCollector.RelativePath`: keep the root early return
   and the segment walk. Replace the reverse StringBuilder loop with
   `string.Join` over the reversed segments (lines 110 to 119).
7. F49, `PoiyomiMaterialSemantics.IsProvenOpaqueBlend`: cast the literals
   at lines 1437 to 1443 to `UnityEngine.Rendering.BlendMode` and
   `BlendOp`. Mapping: `BlendMode.Zero` is 0, `One` is 1, `SrcAlpha` is
   5, `OneMinusSrcAlpha` is 10. `BlendOp.Add` is 0, `Max` is 4.
8. Refresh Unity. Confirm compilation.
9. GREEN: `UnityMaterialEvidenceCaptureTests`,
   `UnityAnimationEvidenceCaptureTests`, `UnityAlphaFieldEvidenceTests`,
   `CensusAggregatorTests`, `CensusAggregatorEmptyTests`,
   `CensusAggregatorNullVersusZeroTests`, `CensusAggregateReportPrivacyTests`,
   `ReferenceFixtureIntegrityTests`, `AvatarCensusCollectorTests`,
   `PoiyomiMaterialSemanticsTests`, `PoiyomiOpaqueConversionTests`,
   `PoiyomiTextureEvidenceTests`, `PoiyomiCutoutSplitTests`.

Plain green run: every swap is semantics-identical by construction.

## Task 3: dead channel, truthful comment, equality helper

1. F35, `HostLifecycleCapability`: delete `hasPrereleaseSuffix`, its
   output parameter, the branch at lines 438 to 442, the conjunction at
   line 394, the conjunctions at lines 486 to 487, and the disjunct at
   line 561 (`Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs`).
   Rewrite the parser doc sentence at lines 403 to 405 and sweep the two
   summary sentences at lines 385 to 389 and 475 to 480. The four pinning
   tests do not change.
2. F26, `.github/workflows/pr.yml`: reword the header comment per design
   D17. The workflow keeps its name, trigger, permissions, and step.
3. F42, equality double check: add one private static
   `SameMaterial(UnityEngine.Object candidate, Material material)` beside
   its first consumer in `TransientUnlockSwapIn` with the wrapper comment
   from lines 517 to 519. Use it at `RewriteCurve` line 520, and in
   `TransientUnlockWindowClose.ReassertShippedSlots` and
   `TransientUnlockWindowState.SwappedView` where the value is untyped.
   Where the value is already `Material`, use plain Unity equality. Do
   not touch the intentional `ReferenceEquals` in `PairFor`.
4. Refresh Unity. Confirm compilation.
5. GREEN: `HostLifecycleCapabilityTests`, `VersionConsentTests`,
   `TransientUnlockSwapInTests`, `TransientUnlockWindowCloseTests`,
   `TransientUnlockWindowStateTests`, `TransientUnlockTransformationTests`.

Plain green run for steps 1 and 3. Step 2 is comment only.

## Task 4: Host dedup

1. F25, default capturer: add one private static
   `DefaultCapturer(RegisteredSourceLookup)` factory. Replace the three
   lambda copies at `UnityAnimationEvidenceCapture` lines 158 to 167,
   225 to 234, and 352 to 361.
2. F27, expected predicates: delete the three copies in
   `UnityGeneratedTextureEvidence` lines 304 to 320. Re-point the call
   sites at lines 186 to 210 to the `UnityAlphaFieldEvidence`
   originals. In `UnityGeneratedTextureEvidenceTests`, re-point the three
   tests at lines 525, 540, and 551 to the originals. Keep the
   3-argument `HostCapabilitiesPass`. It is a different gate.
3. F31, reference comparer: create
   `Packages/com.alrauna.amuse/Editor/Host/ReferenceEqualityComparer.cs`
   with the internal static comparer and its doc sentence about Unity
   equality collapsing destroyed objects. Delete the two nested copies at
   `CommittedControllerGraph` lines 293 to 308 and
   `UnityAnimationEvidenceCapture` lines 1122 to 1138.
4. F22, render-state helper: create
   `Packages/com.alrauna.amuse/Editor/Semantics/EffectiveRenderState.cs`
   with the shared internal `ReadEffectiveRenderState`. Delete the copies
   at `LilToonOpaqueTarget` lines 125 to 144 and
   `PoiyomiOpaqueConversion` lines 491 to 510. Update the four production
   callers in `AlphaSeparationPreparation` at lines 682, 752, and 891 and
   the two test seams in `VerifiedLilToonTestSeams` at lines 336 and 381.
   Leave both `TryFindNonCanonicalFact` members alone. They differ.
5. F23, observation twins: extract one private core into
   `LiveAnimationObservation` taking the binding sets and accessor
   delegates. `ObserveClip` and `ObserveVirtualClip` become two-line
   adapters (lines 85 to 183).
6. F13, readback core: add one internal acquisition core as a sibling of
   `TryAcquireLevel` on `UnityAlphaFieldEvidence` per design D4. It takes
   the target format, a decode delegate over the readback, and a
   validation delegate. `TryAcquireLevel` (lines 486 to 570) and the
   per-mip loop of `UnityGeneratedTextureEvidence.TryCapture` (lines 155
   to 253) call it. The generated route keeps its inert-bounds logic,
   residency gate, placeholder fallback, session cache, and
   `MissingReferenceException` catch. The green-arm gap stays: validation
   arrives as a delegate, so the generated route validates only in the
   red arm, exactly as today. The render-target divergence stays,
   deliberately: the core takes a save-restore flag, `TryAcquireLevel`
   saves and restores `RenderTexture.active`, the generated route does
   not, exactly as today. The core doc states the green-arm reason and
   the render-target divergence with its reason.
7. Refresh Unity. Confirm compilation.
8. GREEN: `UnityAlphaFieldEvidenceTests`, including
   `TheActiveRenderTargetIsRestoredAcrossACapture`, which pins the
   save-restore arm, then `UnityAnimationEvidenceCaptureTests`,
   `UnityGeneratedTextureEvidenceTests`, `CommittedControllerGraphTests`,
   `AnimatorServicesReactivationCharacterizationTests`,
   `LiveAnimationObservationVirtualClipTests`,
   `LilToonCutoutSourceEligibilityTests`,
   `LilToonTransparentSourceEligibilityTests`,
   `LilToonMultiSourceEligibilityTests`, `PoiyomiOpaqueConversionTests`,
   `AlphaSeparationPreparationTests`, `AlphaSeparationApplyTests`.

Plain green run: no test file changes except the F27 re-point.

## Task 5: Build dedup

1. F33, admission prologue: extract one private
   `TryAdmitConversionEvidence` in `AlphaSeparationPreparation` returning
   the slot refusal with the derived state out. Call it once inside
   each converting arm, the lilToon arm at lines 630 to 665 and the
   Poyiomi arm at 816 to 853. The LilToon family gains no admission: it
   returns opaque equal to live before any admission runs. Carry the
   runtime-overwrite doc sentence once.
2. F14, slot filter: add one internal
   `TryParseMaterialSlotBindingFor(bindingPath, bindingTypeFullName,
   propertyName, rendererPath, rendererTypeFullName, out slotIndex)`
   beside the existing members on `UnityAnimationEvidenceCapture`. It
   wraps the existing
   `LiveAnimationObservation.TryParseMaterialSlotBinding`, the ordinal
   path check, and `IsCompatibleRendererType`. Replace the six inline
   filters: `AmusePlatformFinishPass.MaterialSlotsFor` and the renderer
   loop in `AmusePlatformFinishPlugin` (lines 1409 to 1434), the marker
   check in `AlphaSeparationPreparation` (lines 296 to 312), the
   `targetBindings` walk in `AlphaSeparationApply` (lines 248 to 257),
   both passes in `TransientUnlockSwapIn` (lines 191 to 202 and 470 to
   481, the second behind one shared private renderer-and-clips walk),
   and the committed-curve inversion in `TransientUnlockWindowClose`
   (lines 322 to 340). Site six resolves path membership through its
   dictionary and calls the helper once per candidate type with the
   resolved path, so the helper's path check is the gate there.
3. Refresh Unity. Confirm compilation.
4. GREEN: `AmusePlatformFinishPluginTests`,
   `AlphaSeparationPreparationTests`, `AlphaSeparationApplyTests`,
   `TransientUnlockSwapInTests`, `TransientUnlockWindowCloseTests`,
   `UnityAnimationEvidenceCaptureTests`.

Plain green run: filters are re-expressions of the same three checks.

## Task 6: Semantics shared helpers

1. F5, evidence gates: create
   `Packages/com.alrauna.amuse/Editor/Semantics/EvidenceGates.cs` per
   design D6. Move both `FirstFailedZeroGate` overloads, both
   `TryReadBinary` overloads, the `IsFinite` trio,
   `RequireAnalyzableMaterial`, the lilToon `RecordUnknown`, and
   `AllUnknown`. Re-point the copies in `LilToonMaterialSemantics`
   (lines 1159 to 1234), `LilToonCutoutMaterialSemantics` (lines 775 to
   789), `LilToonTransparentMaterialSemantics` (lines 839 to 853),
   `PoiyomiMaterialSemantics` (lines 2719 to 2755 and 1920 to 1954), and
   `UnityMaterialSemantics`. Parameterize `BuildEligibilitySchema` over
   the recipe and source arrays and join it in one place for the three
   eligibility files. The Poyiomi `RecordUnknown` analog stays local.
   Re-point the four `PoiyomiTextureEvidenceTests` call sites from
   Task 1 step 5 to the shared gate.
2. F6, product fold: add the static fold beside `ScalarSemanticValue` in
   `MaterialSemantics.cs` per design D7. Replace `Multiply`,
   `CollectFactors`, `HasAnyMap` in both lilToon files (cutout 668 to
   768, transparent 732 to 832) and `MultiplyAlphaValues`,
   `CollectProductFactors` in `PoiyomiMaterialSemantics` (lines 1837 to
   1915). `ComposeLayer` stays.
3. F28, source hash: add the shared normalize-and-hash helper to
   `Editor/Semantics`. Both `ComputeNormalizedSourceHash` copies delegate
   to it (`PoiyomiMaterialSemantics` lines 2292 to 2318,
   `LilToonSourceAttestation` lines 946 to 978). The private `Normalize`
   and `Sha256` of `LilToonSourceAttestation` keep their other callers.
   `PoiyomiMaterialSemantics` has neither helper. Drop the hand-held
   invariant sentence in the lilToon doc.
4. F9, version seam: create
   `Editor/Semantics/AdmittedProducerVersions.cs` per design D8. Both
   attestation classes construct it with their pins and keep the
   same-named internal forwards for all seven test-addressed members,
   each forward carrying its retention doc sentence.
5. F17, conversion factors: create
   `Packages/com.alrauna.amuse/Editor/Semantics/OpaqueConversionFactors.cs`
   holding the union type. Move the class out of
   `LilToonOpaqueConversionResult.cs` lines 128 to 166. Re-point the two
   lilToon eligibility files and the Poyiomi block at lines 410 to 438.
   Edit the `AuditedProductionFiles` pin in
   `AlphaSeparationPersistenceTests` lines 685 to 686 twice in the same
   step: re-anchor the existing entry to the surviving result type and
   add a new entry pairing the new factors file with the moved class.
   Widen the factors doc to name four consumers.
6. F37, rational shims: delete the `Add` and `Multiply` delegates in
   `AffineUvTransform` (lines 248 to 256) and call `ExactRational`
   directly. Move `Abs` and `Maximum` onto `ExactRational` in
   `ExactUvGeometry.cs` as internal static members, referencing
   `System.Numerics.BigInteger` directly there. The file alias in
   `AffineUvTransform.cs` stays, because live uses remain. Leave the
   dyadic `Add` and `Multiply`
   overloads alone.
7. Refresh Unity. Confirm compilation.
8. GREEN: `LilToonAlphaTests`, `LilToonCutoutAlphaTests`,
   `LilToonTransparentAlphaTests`, `PoiyomiMaterialSemanticsTests`,
   `PoiyomiAlphaMaskTests`, `PoiyomiCutoutSplitTests`,
   `PoiyomiTwoPassAlphaTests`, `PoiyomiTextureEvidenceTests`,
   `ReplacementTextureAttestationTests`,
   `AaoAtlasTextureAttestationTests`,
   `GeneratedTextureAttestationTests`, `UnityTextureEvidenceTests`,
   `UnityAlphaFieldEvidenceTests`,
   `UnityGeneratedTextureEvidenceTests`,
   `UnityMaterialEvidenceCaptureTests`, `LilToonAttestationTests`,
   `LilToonSourceAttestationTests`,
   `LilToonCutoutSourceEligibilityTests`,
   `LilToonTransparentSourceEligibilityTests`,
   `LilToonMultiSourceEligibilityTests`,
   `PoiyomiOpaqueConversionTests`, `LilToonOpaqueConversionResultTests`,
   `AlphaSeparationPersistenceTests`, `AffineUvTransformTests`,
   `ExactUvEnvelopeTests`, `AlphaSemanticsResolverTests`.

Plain green run: moves and parameterizations only.

## Task 7: the lilToon twin merge

1. Baseline first. Run and record the counts of:
   `LilToonCutoutAlphaTests`, `LilToonTransparentAlphaTests`,
   `LilToonNeutralClaimGatingTests`, `LilToonTransparentNeutralClaimGatingTests`,
   `LilToonIrrelevantChangeInvarianceTests`,
   `LilToonSamplerBlastRadiusTests`,
   `LilToonUncertaintyMonotonicityTests`, `SharedEvidenceAgreementTests`.
2. F1: create `LilToonAlphaInterpreter.cs` and
   `LilToonAlphaEvidenceRequests.cs` in `Editor/Semantics/LilToon/` per
   design D5. The interpreter receives the cutoff bound, the two
   transparent gates and bounds, and the coverage-gate array as inputs.
   The per-family constants stay declared in their files.
3. Replace `InterpretCutoutAlpha` (cutout lines 293 to 604) and
   `InterpretTransparentAlpha` (transparent lines 324 to 668) with calls
   into the shared interpreter. Replace both request constructions
   (cutout lines 99 to 214, transparent lines 128 to 243) with calls into
   the shared builder. Both class declarations stay for the persistence
   pin. The main-family request in `LilToonMaterialSemantics` is
   outside F1 and keeps its own construction.
4. Refresh Unity. Confirm compilation.
5. GREEN: re-run every baseline filter. The counts must match the
   baseline exactly. The cutoff falsifiers, the dither rows, the
   AlphaBoostFA and SubpassCutoff falsifiers, and the distance-fade row
   are the wrong-implementation guards.
6. Stop condition: if a pinned bound or gate membership cannot survive
   without a new abstraction layer, stop per design D5 and return the
   attempt.

## Task 8: the producer enum

1. Characterize: run and record `GeneratedTextureAttestationTests`.
2. F24: change both methods in `GeneratedTextureAttestation` to `bool`
   returns. Delete the enum at lines 10 to 16. Drop the `out _` at the
   five production sites and the one test site:
   `UnityTextureEvidence` lines 71, 176 to 177, and 212,
   `UnityAlphaFieldEvidence` line 242, `UnityGeneratedTextureEvidence`
   line 90, and `UnityMaterialEvidenceCaptureTests` line 265.
3. Rewrite the fourteen member-reading assertions in
   `GeneratedTextureAttestationTests` to bool assertions. The two
   route-texture setups with their admitted-version gates stay, and
   their two producer reads at lines 295 and 318 rewrite with the
   fourteen.
4. Refresh Unity. Confirm compilation.
5. GREEN: `GeneratedTextureAttestationTests`,
   `UnityTextureEvidenceTests`, `UnityMaterialEvidenceCaptureTests`.
   The count stays equal. No test is added or deleted.

## Task 9: TextureSampling pass-through

1. F12: delete `AlphaSamplingSettings` from
   `TriangleAlphaClassifier.cs` lines 23 to 63. Store `TextureSampling`
   in `AlphaSemanticsResolver` and delete the two copy constructions at
   lines 621 to 624 and 854 to 857. Re-point the classifier reads to
   `.Filter`, `.Wrap`, `.Aniso`.
2. Re-type the construction sites in the six test files:
   `TriangleAlphaClassifierTests`, `AlphaSemanticsResolverTests`,
   `AdmittedMaterialStatesTests`, `AlphaSeparationPreparationTests`,
   `UnityMaterialEvidenceCaptureTests`, and
   `UnityRendererAlphaAnalysisTests`. Find every site by searching the
   assembly for `AlphaSamplingSettings` and re-type each to
   `TextureSampling`.
3. Refresh Unity. Confirm compilation.
4. GREEN: `TriangleAlphaClassifierTests`,
   `AlphaSemanticsResolverTests`, `AdmittedMaterialStatesTests`,
   `AlphaSeparationPreparationTests`, `UnityMaterialEvidenceCaptureTests`,
   `UnityRendererAlphaAnalysisTests`.

Plain green run after mechanical re-typing. No assertion changes.

## Task 10: the shared texture import

1. F2: create `Packages/com.alrauna.amuse/Tests/Editor/Shared/` with
   `TestTextureImport.cs` per design D13. The `.meta` files come from
   the refresh.
2. Replace the 21 PNG staging blocks with builder calls. Files and
   anchors: `AaoMergedConsumptionTests` 1844 to 1870,
   `AlphaSeparationApplyTests` 3726 to 3790,
   `AlphaSeparationPreparationTests` 3465 to 3480, 6218 to 6231, 6366 to
   6379, 8146 to 8280, `AlphaSeparationSplitTests` 61 to 96,
   `AmusePlatformFinishPluginTests` 3819 to 3850,
   `DaoMergedConsumptionTests` 504 to 540,
   `AlphaEvidenceClassifierIntegrationTests` 54 to 85 and 242 to 270,
   `RendererAlphaAnalysisIntegrationTests` 89 to 131,
   `SourceImageAlphaReaderTests` 35 to 80 and 83 to 110,
   `UnityAlphaFieldEvidenceTests` 79 to 111,
   `UnityMaterialEvidenceCaptureTests` 1229 to 1276,
   `UnityStreamingTextureEvidenceTests` 49 to 92. The EXR site at 95 to
   138 stays local with a one-sentence retention comment.
3. Refresh Unity. Confirm compilation.
4. GREEN, one suite per file, in the order above. Counts stay equal.

Migration only. No assertion changes.

## Task 11: the shared transient scope

1. F3: create `TestTransientScope.cs`, `ObjectRegistryGuard.cs`, and
   `AttestationEnvironment.cs` in `Tests/Editor/Shared/`.
2. Both fixture bases consume `TestTransientScope` for the tracked list,
   temp folder, and `Track<T>` (`LilToonFixtureTestBase` 45 to 77,
   `PoiyomiFixtureTestBase` 32 to 64). The two multi suites follow
   (`LilToonMultiResolutionTests` 49 to 79, `LilToonMultiModeGateTests`
   45 to 57). `LilToonMultiModeGateTests` keeps its teardown-only shape.
   Both bases also replace their private PNG importers with
   `TestTextureImport.WritePng`, which retires the eight Semantics-side
   staging copies. Verify which bases carry a native-asset importer and
   move the survivors into `TestTextureImport.ImportNative` per design
   D13.
3. Derive `PoiyomiTextureEvidenceTests` from `PoiyomiFixtureTestBase`.
   Delete its private scaffolding at lines 23 to 130.
4. Replace the registry boilerplate with `ObjectRegistryGuard` at every
   class that carries the save-and-swap pattern. The four named classes
   are `ReplacementTextureIdentityTests`,
   `AaoAtlasTextureAttestationTests`,
   `ReplacementTextureAttestationTests`, and
   `UnityTextureEvidenceTests`. Search the assembly for the pattern and
   adopt the guard at each further hit, among them
   `UnityAlphaFieldEvidenceTests` at five sites,
   `UnityMaterialEvidenceCaptureTests`, and
   `AmusePlatformFinishPluginTests`.
5. Replace the duplicated `ProjectRoot`, `ShaderDir`, `Tree`, `Canon`
   with `AttestationEnvironment` in `LilToonAttestationTests` and
   `LilToonSourceAttestationTests`.
6. Refresh Unity. Confirm compilation.
7. GREEN: `PoiyomiTextureEvidenceTests`, `LilToonMultiResolutionTests`,
   `LilToonMultiModeGateTests`, `LilToonAttestationTests`,
   `LilToonSourceAttestationTests`,
   `ReplacementTextureIdentityTests`,
   `AaoAtlasTextureAttestationTests`,
   `ReplacementTextureAttestationTests`, `UnityTextureEvidenceTests`,
   `AmusePlatformFinishPluginTests`, `UnityAlphaFieldEvidenceTests`,
   and one characterization suite per fixture base,
   `PoiyomiIrrelevantChangeInvarianceTests` and
   `LilToonIrrelevantChangeInvarianceTests`.

Migration only. No assertion changes.

## Task 12: test seams and small helpers

1. F8: add the protected `Interpret` seam with the optional
   color-space parameter and `InterpretTwoPass` to
   `PoiyomiFixtureTestBase`. Delete the thirteen local wrappers:
   `PoiyomiAdversarialTests`, `PoiyomiAlphaMaskTests`, the two classes
   of `PoiyomiBaseColorAlphaTests`, `PoiyomiDecalSlotAlphaTests`,
   `PoiyomiEmissionTests`, `PoiyomiNormalTests`,
   `PoiyomiRimLightingAlphaTests`, `PoiyomiTwoPassAlphaTests`, and the
   four Poiyomi characterization suites
   `PoiyomiIrrelevantChangeInvarianceTests`,
   `PoiyomiNeutralClaimGatingTests`, `PoiyomiSamplerBlastRadiusTests`,
   `PoiyomiUncertaintyMonotonicityTests`. The direct production-seam
   calls in
   `PoiyomiCutoutSplitTests`, `SharedEvidenceAgreementTests`, and
   `PoiyomiMultipassRuleTests` stay.
2. F10: create `TestShaderWriter.cs` in `Tests/Editor/Shared/`. Replace
   the ten writer copies. The two pinned-GUID `.meta` writes stay at
   their call sites.
3. F11: `RunEndSceneHygiene.LeaveNoModifiedSceneOpen` delegates to
   `TransientUnlockTestLifecycle.AssertNoSavedSceneDirty`. Keep the
   entry point and the tripwire comment naming the shared static. The
   research package carries a third near-verbatim copy in its own test
   assembly. It cannot delegate across assemblies, so it stays with one
   retention sentence naming that reason.
4. F16: create `CurveDescription.cs` in `Tests/Editor/Shared/`. Delete
   the `DescribeCommittedCurve` twin in `AlphaSeparationSplitTests` and
   re-point its five callers. Adopt the shared describer in
   `AlphaSeparationApplyTests` and
   `AnimatorServicesReactivationCharacterizationTests`.
5. F18: widen the shader-name constants on both fixture bases to
   internal. Delete the four const-only alias classes:
   `LilToonFixtureShaderNames` and `PoiyomiFixtureShaderNames` in
   `VerifiedLilToonTestSeams` (lines 424 to 444),
   `LilToonConversionShaderNames` in `AlphaSeparationApplyTests` (3804
   to 3811), `LilToonFixtureNames` in `AlphaSeparationPreparationTests`
   (7844 to 7852). The behavioral fixture subclasses stay.
6. F20: parameterize the seam twin in `VerifiedLilToonTestSeams` over
   the eligibility call. Keep both public names as one-line forwards.
7. F36: create `FloatUlp.cs` in `Tests/Editor/Shared/` with the plus-one
   and N-step forms. Delete the three local copies.
8. Refresh Unity. Confirm compilation.
9. GREEN: the thirteen F8 suites,
   `AlphaSeparationPreparationTests`, `AlphaSeparationApplyTests`,
   `AlphaSeparationSplitTests`, `AmusePlatformFinishPluginTests`,
   `UnityAnimationEvidenceCaptureTests`, `UnityMaterialSemanticsTests`,
   `LilToonAttestationTests`, `LilToonMultiResolutionTests`,
   `LilToonOpaqueTargetTests`, `PoiyomiCutoutSplitTests`,
   `LilToonTransparentAlphaTests`,
   `LilToonTransparentSourceEligibilityTests`,
   `TransientUnlockSwapInTests`, `TransientUnlockWindowCloseTests`,
   `TransientUnlockTransformationTests`,
   `AnimatorServicesReactivationCharacterizationTests`.

Migration only. No assertion changes.

## Task 13: the merged-consumption fixture

1. F7: create
   `Packages/com.alrauna.amuse/Tests/Editor/Build/MergedConsumptionFixture.cs`
   per design D13. Move, by symbol rather than by line range, the band
   model, the authored-triangle registry, the banded, solid, and
   quadrant texture imports, the stand-in material builders,
   `RequireLilToonEnvironment`, `AttachAnimationFixture`, the reflected
   optimizer configuration, the renderer factory
   `CreateSkinnedRenderer`, and the triangle-match oracle out of
   `AaoMergedConsumptionTests`. `DaoMergedConsumptionTests` deletes its
   re-declarations, including its own `AttachAnimationFixture` and
   `CreateSkinnedRenderer`, and consumes the fixture. The fixture
   exposes the AAO member names for the expectation enum and the
   authored-triangle record, and the DAO call sites re-point to them
   with assertion semantics unchanged. The DAO env-var gate, the SDK
   dispatcher call, and the d4rk cleanup stay in the DAO file.
2. Refresh Unity. Confirm compilation.
3. GREEN: `AaoMergedConsumptionTests`, `DaoMergedConsumptionTests`.
   Counts stay equal.

## Task 14: research dedup

1. F15: create
   `Packages/com.alrauna.amuse.research/Editor/Census/CensusCopy.cs`
   with the internal `CheckedCopy<T>` helper. The null-propagating
   element check moves into it. Replace the six constructor loops
   (`CensusObservation.cs` 162 to 167, 247 to 253, 275 to 280,
   `AnonymizedCensus.cs` 160 to 165, 198 to 203, 223 to 228). The
   per-type invariant checks stay.
2. F29: add one local `ProbeInstalled` helper to
   `VendorReachabilityTests` that probes and asserts the absent-shader
   case, returning the presence. The four byte-identical preambles and
   the lilToon variant call it. The lilToon site keeps its
   `Assert.Ignore` discharge.
3. Refresh Unity. Confirm compilation.
4. GREEN: `CensusObservationTests`, `CensusAggregatorTests`,
   `CensusAggregatorEmptyTests`, `CensusAggregatorNullVersusZeroTests`,
   `CensusAggregateReportPrivacyTests`, `VendorReachabilityTests`.

## Task 15: full validation and records

1. Run the full EditMode population of both assemblies. Expected: the
   Task 0 baseline, plus or minus the designed deltas. Designed deltas:
   `AllowlistStartsEmpty` deleted in Task 1. The F21 dead-fact
   assertions delete inside surviving tests, so no whole test goes on
   the anchored tree. Every other count stays. Attribute
   every remaining failure against the Task 0 list. Any other failure
   stops the work.
2. `git diff --check`.
3. Inspect the full diff. Confirm: no research file ships into the
   product package, no `.meta` orphan remains for any deleted file,
   every new `.cs` file carries its `.meta`, and no asset or manifest
   changed.
4. Identifier sweep over every changed file: an at sign joined to a
   hexadecimal hash, drive-letter paths, home-directory paths, four-digit
   ports, and every private asset name from the session. Zero hits.
5. Record the observed counts and the sweep result in this plan.

## Stop conditions

The design's stop conditions apply. In addition, stop when a filter
returns zero tests, when a suite count moves without a designed delta, or
when the diff contains any file outside the target list of this plan.

## Expected report

The changed files per task, the observed focused and full counts with
dates, the Task 0 baseline list, the Task 7 and Task 8 baseline and
after counts, the diff check result, the identifier sweep result, and the
remaining limits. State that no upload ran, no Unity instance outside the
dev editor instance was touched, and nothing was staged or committed.

## Results, 2026-10-04

Executed on branch `chore/ponytail-audit-cuts`, working tree only, in the
dev editor instance after data-path verification. Nothing staged, nothing
committed. One mid-run editor disconnect recovered by re-enumeration and
identity pinning; the Census Lab instance was never touched.

Observed counts:

- Task 0 baseline: 2562 passed, 0 failed, plus the two AMUSE_DAO_INTEGRATION
  gated tests Inconclusive by design. Environment list: exactly those two.
- Task 7 baseline and post-merge: 218 passed on the eight falsifier and
  characterization suites, exact match.
- Focused GREEN totals per task: 582, 434, 85, 551, 266, 1096, 218, 87,
  414, 368, 655, 968, 22, 51. All zero failures. Tasks 1, 10, and 13
  completed after one or more fix rounds; Task 13 took four.
- Task 15 full population: 2561 passed, 0 failed, 0 skipped, plus the same
  two gated Inconclusive. The delta against baseline is exactly the designed
  minus one: AllowlistStartsEmpty deleted in Task 1. No other count moved.

Diff check: clean. One hundred seventeen tracked files modified and seventeen
new source files, each with its editor-generated .meta. No deleted file, so
no orphan .meta. No asset, manifest, or ProjectSettings file changed. No
research file entered the product package.

Identifier sweep over all one hundred thirty-four changed and new files:
one pattern hit, a vendor shader line citation in a doc comment, present
verbatim at the base commit and untouched by this work. Zero new hits.

Limits: no upload ran. No Unity instance outside the dev editor instance was
touched. Nothing was staged or committed. The two gated DAO tests remain
Inconclusive by their env-var design and were never run with the gate set.
