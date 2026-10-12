# Production test coverage fixes design

Privacy note: This document uses public source references and sanitized findings. It contains no private asset identifiers or machine identities.

Date: 2026-10-11.
Status on 2026-10-11: Design proposal complete. Awaiting user review before implementation.
Base: `fix/unit-test-soundness`, after the unit test soundness fixes plan lands on the same branch.
Evidence base: `docs/superpowers/investigations/2026-10-11-production-test-coverage-investigation.md` (static trace; adversarially verified samples).

## 1. Goal and approved contract

Close the named production coverage gaps from the 2026-10-11 coverage investigation, highest risk first. The suite stays green throughout. Every new test asserts observable production behavior through real fixtures or the established verified seams.

Contract:
- No production behavior change. Two small production seams are allowed where the design names them, because the production API already owns that seam shape.
- New tests must be green on current HEAD. Where a named mutant can prove the test bites, the plan verifies it with a temporary mutation and records the observed failure.
- Gaps not named here stay open and recorded. This design covers the 17 ranked gaps and the confirmed prior findings.

## 2. Decision points

### 2.1 Poiyomi `UnattestedMaterial` refusal arm

`PoiyomiOpaqueConversionRefusal.UnattestedMaterial` has no producer. `EvaluateVerifiedEligibility` has no attestation gate, and `TryMapPoiyomiOutcome` only forwards eligibility refusals. The lilToon twin emits its arm inside its own Multi eligibility evaluator.

Options:
- A. Delete the arm. Attestation for Poiyomi is enforced at the verified-entry layer before eligibility runs, so the member is unreachable vocabulary in a closed enum. The enum shrinks; any exhaustive string-table iteration adapts automatically.
- B. Add the gate to `EvaluateVerifiedEligibility` for symmetry with lilToon. This changes production behavior: an eligibility call on an unattested source returns a refusal instead of being unreachable.

Recommendation: A, delete. Evidence: no producer exists today, so option B adds a behavior change nobody depends on, and the verified-entry layer already refuses unattested Poiyomi sources with its own diagnostic.

This decision needs owner approval before the task runs. The plan gates the task on it.

### 2.2 Build-target seam

The unsupported-target disclosure never fires because no test leaves the Windows build target. Switching `EditorUserBuildSettings.activeBuildTarget` inside EditMode tests triggers slow asset-import work and risks polluting the session.

The sampling policy never reads the build target itself. `TrySelect` receives the target as a parameter, and the read happens at three call sites: `AmusePlatformFinishPlugin`, `AlphaSeparationApply`, and the optimizer editor. The disclosure path under test runs through the finish plugin.

Design: add one test-only seam at the finish plugin's read site:

```csharp
internal static Func<BuildTarget> ActiveBuildTargetForTests = null;
```

`AmusePlatformFinishPlugin` resolves the target it passes into the sampling policy through `ActiveBuildTargetForTests?.Invoke() ?? EditorUserBuildSettings.activeBuildTarget`. The field defaults to null, production behavior is unchanged, and the other two read sites keep the real setting. Tests set and restore it in try/finally, the pattern `PackageVersionReader.ListPackages` already owns.

The policy truth tables already assert the unsupported-target refusal directly for non-admitted targets, so the new seam tests add only the end-to-end disclosure and conservative-fallback behavior.

### 2.3 Optional instrumentation

Installing `com.unity.testtools.codecoverage` would give per-method line evidence for every gap in the investigation. It changes `Packages/manifest.json`. Out of scope for this design; a separate decision.

## 3. Phase structure

Phase 0, build-path risk. Closes gaps 1, 2, 3, and the contract decision 2.1.

Phase 1, reader and grammar logic. Closes gaps 4, 12, 16 and the parser edges.

Phase 2, guard batteries. Closes gaps 5, 9, 10, 13 and the named per-module guard arms.

Phase 3, semantics and research depth. Closes gaps 6, 7, 8, 11, 14, 15, 17 and the LilToon layer gaps.

## 4. Per-gap dispositions

| Gap (investigation rank) | Disposition |
|---|---|
| 1 Non-skinned mesh write | New apply test over a `MeshRenderer` fixture. |
| 2 Renderer-scoped conversion invalidation | New preparation test driving `RefuseEveryCandidateSlot` through an unrecognized conversion binding. |
| 3 Unsupported-target disclosure | Seam from 2.2 plus a barrier test asserting the report and the conservative policy. |
| 4 `PackageVersionReader` | Production extraction `VersionFor` (seam shape, no behavior change) plus the reader tests within seam reach: null-request refusal, name match, no-match null. The bounded wait and the failed-status arm need a constructible `ListRequest` and stay seam-limited and recorded. |
| 5 `MalformedMeshData` arms | Five table rows in the renderer analysis tests. |
| 6 Dead `UnattestedMaterial` arm | Gated on 2.1. |
| 7 `PremultiplyNormalization` | Three eligibility rows. |
| 8 Research transparent count | Seam counting test with constant sub-unit alpha semantics. |
| 9 Behaviour allowlist holes | Four behaviour rows plus the `com.vrchat.base@` prefix arm. |
| 10 `AlphaFieldSet.TryGetFor` separation | Three branch tests with hand-built evidence. |
| 11 Locked-material attested true path | Requires a fixture that materializes an attested original; if the verified seam cannot produce one, record as seam-limited and close the reconstruction sub-gaps only. |
| 12 Version grammar edges | Table rows over `HostLifecycleFacts`: no release suffix, trailing junk, build metadata, missing components, fourth component, empty component. |
| 13 `SlotResolutionResult` propagation and `ClosureWay` | Property tests plus one closure-sentence row. |
| 14 Streaming evidence ladder | Arms that a fixture can reach directly; unreachable arms recorded as seam-limited. |
| 15 Include-tree identity resolution | Temp-tree fixture through `Enumerate` and `TryGetRelativePath`. |
| 16 Preset store failure shapes | Three store rows plus the parser percent, object-level, and inert-clamp rows. |
| 17 Tier-1 census asset identity | Imported-asset fixture through `Collect`. |

The Poiyomi outline weak spot lands in the premultiply task: add the missing hole-field classification row for an unproven outline texture on a chain-admitted material.

## 5. Testing strategy

1. Coverage additions are green by construction on current HEAD. The plan marks each with the mutant that should kill it where one exists, verifies the mutant temporarily, and records the observed failure.
2. Every fixture follows the established patterns: public synthetic textures and meshes, verified seams for vendor attestation, no mocks.
3. Full EditMode suite before and after, with observed counts in the execution report.

## 6. Stop conditions

Stop and return evidence if:

- A new test cannot be made to fail against its named mutant and no weaker mutant works.
- The non-skinned fixture is refused by the analysis for a structural reason. Then gap 1 is a product limitation question, not a test task.
- The `UnattestedMaterial` decision is unresolved. Task gating blocks, the rest proceeds.
- The suite goes red for a reason this design does not name.

## 7. Adversarial review record

Three adversarial reviewers audited this design and its plan against the investigation, the coverage-scout findings, and the repository on 2026-10-11. Corrections applied in this revision:

- The build-target seam moved from the sampling policy, which takes the target as a parameter and never reads the setting, to the finish plugin's real read site. Without the move the seam was dead code and the disclosure test could never fire.
- The design now states the reader task's seam limit: the bounded wait and the failed-status arm need a constructible `ListRequest` and stay recorded instead of promised.
- The Poiyomi outline weak spot is now a real disposition in the premultiply task instead of a deferral to a scope no document defined.
- The version grammar edge list now carries all six edges, and the preset store disposition matches its three rows.
