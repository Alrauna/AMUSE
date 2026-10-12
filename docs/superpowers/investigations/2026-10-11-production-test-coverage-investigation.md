# Production test coverage investigation

- Date: 2026-10-11
- Status: investigation complete on 2026-10-11. No production or test file changed.
- Branch: `fix/unit-test-soundness`. Nothing is committed on it.
- Follow-up to: `2026-10-11-unit-test-soundness-audit-investigation.md`. That audit asked which tests are sound. This one asks the inverse question: which production code do the tests actually reach.

## Privacy note

This document names only repository files, types, and test names. No private avatar, renderer, material, machine, instance, or identifier appears in it.

## Method and limits

Ten read-only scouts mapped the production code in parallel on 2026-10-11, one per module. Each scout enumerated its production files, listed the meaningful members and branches of each type, and traced test-corpus references through public entry points.

Limits of this method.

- The mapping is static. A member counts as covered only when a test names it or a traced public path reaches it. Runtime-only coincidences are not counted.
- No instrumentation ran. The Unity code coverage package is not installed, and installing it would change `Packages/manifest.json`. That needs an explicit decision. With the package, a full EditMode run would produce per-method line evidence and could confirm or shrink every list below.
- Zero production types came back completely uncovered. The gaps are members and branches inside otherwise covered types.

## Module results

| Module | Files | Full types | Partial types | Uncovered types |
|---|---|---|---|---|
| Analysis | 10 | 26 | 4 | 0 |
| Build, passes and lifecycle | 6 | 8 | 5 | 0 |
| Build, separation and unlock | 18 | 10 | 9 | 0 |
| Host, alpha evidence | 19 | 10 | 17 | 0 |
| Host, animation evidence | 6 | 20 | 4 | 0 |
| Semantics root | 20 | 29 | 11 | 0 |
| Poiyomi | 2 | 5 | 5 | 0 |
| LilToon | 18 | 27 | 10 | 0 |
| Presets, editor root | 12 | 5 | 5 | 0 |
| Research | 14 | 16 | 7 | 0 |

Notes on the table.

- The runtime component lives at `Packages/com.alrauna.amuse/Runtime/`, not under `Editor/Runtime/`. It is covered in full by component contract tests.
- No production type named `AlphaSeparationSplit` exists. Split behavior lives in preparation and apply, and it is covered by dedicated tests.
- The research test corpus references none of the Semantics root types. All their coverage flows through the product test assembly.

## Top gaps, ranked by risk

Each row names the uncovered code, why it matters, and the shape of the test that closes it.

1. Mesh mutation on non-skinned renderers. Every build fixture uses a `SkinnedMeshRenderer`, so the `MeshRenderer` plus `MeshFilter` branch of the apply-time mesh write never executes. That is the only mesh write path for static-renderer avatars. Test: apply pass over a `MeshRenderer` avatar; assert the filter's mesh swap, materials, and curve rewrite.
2. Renderer-scoped conversion invalidation. `RefuseEveryCandidateSlot` and its three arms (`ConversionBindingUnrecognized`, `ConversionStateUnderAdditiveLayer`, `ConversionStateUnderUnnormalizedDirectBlendTree`) have zero executed coverage. This layer invalidates every candidate when conversion-relevant bindings re-resolve renderer-wide. Test: barrier run whose clip animates a conversion-relevant property the union request cannot recognize; assert one refusal per candidate slot.
3. Unsupported build-target disclosure. The barrier's missing-sampling-policy gate and the `UnsupportedSamplingTarget` report never fire, because no test leaves the Windows build target. A regression that drops the conservative-fallback disclosure would pass. Test: switch the active target or add a captured-target seam; assert the report entry and the conservative policy.
4. `PackageVersionReader` read logic. The success match, failed-status refusal, null-result refusal, no-match null, and the bounded spin wait have no assertions. Every producer-version admission sits on this reader. This confirms and extends the soundness-audit finding.
5. Malformed mesh data refusals. All five `MalformedMeshData` arms in the renderer capture (short positions, partial uv0, wrong-count extra UVs, indices not a multiple of 3, out-of-range index) are never emitted by any test. The non-finite-position classification arm is also untested; only the non-finite-UV arm is pinned.
6. Dead or missing refusal arm. `PoiyomiOpaqueConversionRefusal.UnattestedMaterial` has no production producer. The evaluator has no attestation gate, and the outcome mapper only forwards eligibility refusals. Either the arm is dead vocabulary, or the gate is missing. The lilToon twin emits its arm inside its own evaluator. This needs a design decision, not just a test.
7. Premultiply flag never observed. `PremultiplyNormalization` is computed and forwarded into separation reporting, but no test asserts it in any state. A stuck-false flag ships unreported premultiply normalization. Test: one premultiply-on eligibility row asserting the flag true, plus false rows for refused and already-opaque outcomes.
8. Research transparent count. `RendererObservationBuilder.Build` never counts a `MustRemainTransparent` triangle in CI. The transparent half of the headline split is guarded only by a Lab-conditional vendor gate. Test: seam counting test with constant sub-unit alpha semantics.
9. Behaviour allowlist holes. 4 of 8 neutral VRChat behaviours (`LocomotionControl`, `TemporaryPoseSpace`, `JumpAndGravity`, `PlayAudio`) and the `com.vrchat.base@` prefix arm are never asserted. A dropped entry would silently re-refuse avatars.
10. Consumer-side field separation. `AlphaFieldSet.TryGetFor` never runs its predicate-disagreement and channel-filter branches. A silent pick would read one material's cutoff verdicts as another material's exact-one facts.
11. Locked-material attested path. `ResolvesToAttestedPoiyomi` true is never produced, so a fail-closed regression to always-false would pass. The renamed color and vector property moves and the scale/offset restore in reconstruction are also untested.
12. Version grammar edges. `HostLifecycleCapability` never sees versions without a release suffix, with trailing junk, with four components, with build metadata, or with an empty component.
13. Anisotropy and closure plumbing. `AlphaResolution.HasAnisotropicSampling` is pinned at the resolution level, but the `SlotResolutionResult` propagation of that fact and `ClosureWay` are never asserted, yet they gate the anisotropy-cause branch and the closure-failure report sentences.
14. Streaming evidence fail-closed ladder. The no-asset, not-main-asset, copy, importer, clone, dimension, and pixel failure arms of the streaming route have no direct tests, and the clone-route policy-erase arm never executes.
15. Include-tree identity resolution. `LilToonIncludeTree.Enumerate` and `TryGetRelativePath` are never driven. This is the defense that stops a redirected include from canonicalizing to a trusted one. Only the test seam and the digest math are covered.
16. Preset store failure shapes. The missing-file route to `PresetLoadRefusal.FileMissing`, the null-folder gate, and the direct-disk fallback branch are unreached. A broken install's refusal behavior has zero coverage.
17. Tier-1 asset identity. `CensusAssetIdentity` non-null path and GUID arms never fire in CI. All fixtures are runtime objects. The identity of a real project asset is never observed end to end.

## Per-module gap lists

### Analysis

- `ExactRational.Divide`, `Abs`, `Maximum` have no direct tests. The envelope tests even reimplement `Abs` locally, so a regression in the real member passes.
- `SlotResolutionResult.HasAnisotropicSampling` and `ClosureWay` never asserted.
- Admitted block-state admission is proven only for float scalars. Color and vector mismatch refusals, their substitution arms, and the non-scalar escape arms are untested.
- `AlphaSemanticsResolver.ResolveMappedSample`: the uniform proven-opaque arm and the channel-4 unsupported-mapping refusal are unpinned.
- `SaturatingDifference` zero-via-zero-scaled-sample arm untested. Only the constant-zero arm is pinned.
- `Resolve` fail-closed default arm for future scalar kinds is unpinned, against the closed-refusal convention.
- The published flag rule stays mirror-pinned, and no assertion ties the noise clamp to a byte-order invariant. Carried from the soundness audit.

### Build, passes and lifecycle

- Unsupported build-target disclosure chain, see top gap 3.
- `AmuseReports.SlotSeparationDivergence` is never positively asserted. Only its absence is.
- `SlotSeparationRefusal` proven-mapping keys-naming arm has no production caller and no test.
- Report state None-guards are tested asymmetrically: the renderer-refusal and lifecycle guards are pinned; the slot-analysis, slot-separation, avatar-refusal, and slot-refusal guards are not.
- Minor: summary arms for other platforms, `ShouldProceed(null)`, default-throw arms of summary keys, and null arms of three policy mappers.

### Build, separation and unlock

- Renderer-scoped conversion invalidation, see top gap 2.
- Non-skinned mesh mutation, see top gap 1.
- The granted-shader bypass of lilToon attestation never runs. The only granted-shader test uses the Poiyomi arm, which ignores granted names.
- The all-admitted-unconverted multi-material slot fallback is unexercised. Only the partial identity fallback is covered.
- `TransientUnlockWindowClose.ReassertShippedSlots` re-assignment of apply's recorded writes has no staging test.
- `LockedMaterialIdentity` attested true path, see top gap 11.
- `TransientUnlockWindowState` defensive throws and swap-view branches unpinned. `WindowClose`'s two-argument inversion check and `ExecuteClose` are caller-less surface.

### Host, alpha evidence

- Malformed mesh data refusals, see top gap 5.
- Streaming evidence ladder, see top gap 14.
- `AlphaFieldSet.TryGetFor` separation branches, see top gap 10.
- `CapturedTextureEvidence` derived facts (`SampledAlphaIsProvenOne`, `ColorValuesProvenInUnitRange`, full-opaque alpha channel) are asserted nowhere by name, and three evidence kinds are never requested in any capture test.
- `SourceImageAlphaReader` dimension-mismatch and alpha-source-None arms are uncovered on the disk route. The GPU route pins both.
- Materialization equality members and the multi-slot material corner have no direct Host tests.
- A guard cluster has no falsifier anywhere: capture-refusal constructors, combine guards, unrequested-name throws, gather argument guards, capture-engine invariant, and missing-reference catch arms.
- Registered-source analysis overloads and the snapshot forwarder are never called by tests.

### Host, animation evidence

- Closure-failure bookkeeping: dedupe and multi-failure sort branches never exercised. Duplicates would double-refuse a slot in reporting and apply.
- Closed-capturer count-invariant branch unexercised. A count-drifting capturer would mis-slot evidence instead of throwing.
- `LockedMaterialRefusalCheck` failed-ordinal invocation and the observed-material mapper run only inside full builds; their contracts are pinned nowhere directly.
- `CaptureWithAnimationIndex` guard and merge arms have no seam-level test.
- `TryParseMaterialSlotBindingFor` and `IsUnrecognizedObjectMaterialBinding` are production-only members with end-to-end coverage only.
- Proof-relevance residual syntax arms: component suffix variants and malformed material-index prefixes.

### Semantics root

- `PackageVersionReader`, see top gap 4.
- `UnityTextureEvidence.TryProveColorValuesInUnitRange` has zero behavioral coverage. The format allowlist is the sole basis of the identity-range proof.
- `CollectTransferConsent`: the granted-name set is only ever asserted empty. Its population, dedup, and skip arms are untested. This pairs with the granted-shader bypass gap in Build.
- `ClassifyShaderName` outline-wrapper arms, two capture-gather arms, and the Poiyomi-admitted branch are unexercised through the entry points.
- `ScalarProductFold` and `ScalarSemanticValue` fail-closed edges (non-finite multipliers, malformed chains, wrong-kind accessors) are unpinned.
- `AffineAlphaMap.Evaluate` non-finite input refusal and subnormal or overflow rounding arms lack directed tests.

### Poiyomi

- `PremultiplyNormalization`, see top gap 7.
- `UnattestedMaterial` dead-or-missing arm, see top gap 6.
- `AnalyzeBaseMaterial` runs under test only to its attestation refusal. The verified dispatch, request selection, capture, and four-output interpretation never execute under test.
- The alpha shared clip gate `_Cutoff > 1` arm is untested on plain and forced-opaque paths.
- The first-family non-binary force-opaque gate arm is untested.
- Minor fail-closed arms: unstable mask identity, absent or non-finite mask ST, missing or non-finite decal enable, source-gather IO catch arms.

### LilToon

- Alpha mask scalar gates: `_AlphaMaskValue` has zero rows anywhere, `_AlphaMaskScale` has exactly one. Confirms and extends the soundness audit.
- Alpha-path `UnstableTextureIdentity` refusals are uncovered. That diagnostic is asserted only for base color.
- The third main layer is never composed, so every per-layer refusal arm (decal flags, MSDF, audio link, cull, per-layer dissolve and distance fade, out-of-range alpha mode, non-finite layer alpha) is unexercised.
- Include-tree identity resolution, see top gap 15.
- `TryResolveMultiContainer` and the Multi-container target-name mapping arm run only behind integration.
- Multi eligibility lacks transparent-mode boost and subpass-cutoff writer rows, any RenderType refusal row, and a non-finite distance-fade row.
- Fold and interaction refusals uncovered: fold over a saturating chain, sampled mask with unassigned main, non-finite color alpha, requested-but-absent mask entries.
- The distance-fade absent arm and the label table beyond one entry are unasserted.

### Presets and editor root

- Preset store failure shapes, see top gap 16.
- `TryReadPercent` lower bound untested. A parser that admits negative percents would pass.
- The inert-clamp branch that maps an inert clamp to an effective 100 percent target is untested. This extends the known untested-clamp finding to the parser.
- Editor static predicates: texture-size out-of-band edges and nearest-legal-size arms unpinned.
- Dependency-collection semantics of the registration helper are asserted nowhere.
- Parser object-level refusals (missing or wrongly typed features and settings objects) and the null-input coercion are untested.
- The preset warning gate in the inspector carries user-facing fail-closed behavior and is unreached. Other GUI branches stay boilerplate.

### Research

- Transparent count in the builder, see top gap 8.
- `SkinnedMeshRenderer` kind arm and its mesh branch have no test. No research test instantiates the most common avatar renderer.
- Attested shader-family arms and the memo-hit branch are CI-uncovered. Only the none arm runs without vendor packages.
- Tier-1 asset identity, see top gap 17.
- The shared checked-copy null-element refusal never fires.
- Tier-2 numeric and enum guards are untested while the identical tier-1 checks are tested. Tier 2 is publicly constructible.
- Accepted unreachable: the builder's cross-check throws and four default-throw drift arms cannot be reached through production today. Record them as drift-only guards.

## Confirmed prior findings

The 2026-10-11 soundness audit named four coverage-shaped findings. This investigation confirms all four and sharpens two.

- `PackageVersionReader` read logic: confirmed. The gap is total, not partial.
- Alpha mask scalar refusals: confirmed and sharpened. `_AlphaMaskScale` has one coverage point. `_AlphaMaskValue` has none.
- Published-chain flag rule mirror: confirmed. The parser-side inert-clamp branch adds a second unpinned clamp path.
- Poiyomi outline unproven-texture row: confirmed. It remains the one missing classification row in an otherwise covered file.

## Next actions when fixing starts

1. Decide the `UnattestedMaterial` question first. It is a contract decision, not a test task.
2. Add the two highest-risk build tests: non-skinned mesh mutation, and renderer-scoped conversion invalidation.
3. Add a build-target seam or a target-switching fixture to light the unsupported-target disclosure.
4. Give `PackageVersionReader` its first real tests.
5. Add the cheap guard tests: version grammar edges, report None-guards, percent lower bound, preset store failure shapes, behaviour allowlist completions, malformed mesh data arms.
6. Close the LilToon scalar-gate holes with one transparent-style row for `_AlphaMaskValue` and one unit-level row for each scalar.
7. If per-method evidence is wanted, install the Unity code coverage package and rerun the full suite with coverage. That needs a manifest decision first.
