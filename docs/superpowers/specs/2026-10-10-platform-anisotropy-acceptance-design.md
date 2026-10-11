# Platform anisotropy acceptance design

Privacy note: This document uses public source references and sanitized findings. It contains no private asset identifiers or machine identities.

Date: 2026-10-10.
Status on 2026-10-11: Implemented and reviewed. Full validation reported 2843 passed, zero failed, and two gated DAO cases inconclusive. Public NDMF smoke and inspector checks passed. Private Census and live VRChat validation did not run.
Base: `main` at `adfcd17`.
Topic branch: `feature/platform-anisotropy-proof`.

## 1. Goal and approved contract

Anisotropy extends the texture sampling footprint along a direction. A footprint is the texture region that contributes to a sample.

Restore useful local opacity proofs through an explicit user choice. Select that choice from the editor's active platform build target. Keep conservative anisotropic proofs available.

The user chose acceptance of filtering differences rather than a new bounded anisotropic footprint model on 2026-10-10.

AMUSE must distinguish two modes:

| Mode | Proof scope | User authorization |
|---|---|---|
| Conservative anisotropic proof | Any anisotropic footprint within each consulted mip level | Default. No additional acceptance. |
| Accepted non-anisotropic proof | Existing Point, Bilinear, or Trilinear local footprint | Explicit acceptance of possible alpha changes under runtime anisotropy. |

A mip level is a smaller version of a texture. Existing mip limits still bound both modes.

The accepted mode does not disable anisotropy in Unity or VRChat. It does not certify appearance under anisotropic filtering. Its result is a proof under the selected policy, not an unrestricted runtime guarantee.

Changing policy can legitimately change a triangle from `Unknown` to `ProvenOpaque`. Increased uncertainty under the same policy must never increase conversion.

## 2. Evidence and constraints

The investigation is the evidence record:

`docs/superpowers/investigations/2026-10-10-liltoon-opacity-conversion-regression-investigation.md`.

PR #139 made level-1 texture capture depend on `QualitySettings.anisotropicFiltering`. The investigation measured lost main-texture proofs under Forced On.

Sources:

- `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:148-170`.
- `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:346-365`.
- `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:374-451`.

The investigation did not isolate every contributor through a complete NDMF build. This repair must prove real conversion on public fixtures.

The [published VRChat profiles](https://docs.vrchat.com/docs/vrchat-configuration-window) show Forced On. The [mobile release notes](https://docs.vrchat.com/docs/vrchat-202433) describe avatar filtering overrides.

These sources do not establish a precise anisotropic footprint. They do not make the editor quality configuration a runtime authority.

The [Unity level-0 exception](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture-anisoLevel.html) supports a narrower desktop case. Mobile preservation of that exception remains unresolved.

Product constraints stay unchanged:

- Unity 2022.3.22f1 or newer admitted f-releases.
- NDMF `[1.14.4, 2.0.0)`.
- VRChat SDK Base and Avatars `[3.10.4, 4.0.0)`.
- No new dependencies.
- No source asset or editor quality mutations.
- No change to shader attestation, animation admission, mip scope, cutoff rules, or conversion eligibility.
- No research package content in the product or VPM package.

The texture capture gate admits only `BuildTarget.StandaloneWindows64` at this baseline.

Source: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs:874-877`.

Android and iOS selection must not bypass that gate. This design adds platform-specific acceptance selection, not mobile shader or texture-capture support.

## 3. Platform selection and persistent acceptance

Add two serialized fields to `AmuseAvatarOptimizer`:

```csharp
[SerializeField] private bool _acceptDesktopAnisotropyDifferences;
[SerializeField] private bool _acceptMobileAnisotropyDifferences;

public bool AcceptDesktopAnisotropyDifferences =>
    _acceptDesktopAnisotropyDifferences;
public bool AcceptMobileAnisotropyDifferences =>
    _acceptMobileAnisotropyDifferences;
```

Both fields default to false. Existing avatars therefore gain no silent acceptance. Neither field changes the other.

Use the exact `EditorUserBuildSettings.activeBuildTarget` value. Do not use the editor operating system, graphics API, selected quality level, or inspector state.

| Active build target | Configuration group | Acceptance false | Acceptance true |
|---|---|---|---|
| `StandaloneWindows64` | Desktop | `ConservativeDesktop` | `AcceptedNonAnisotropic` |
| `Android` | Mobile | `ConservativeMobile` | `AcceptedNonAnisotropic` |
| `iOS` | Mobile | `ConservativeMobile` | `AcceptedNonAnisotropic` |
| Every other target | Unsupported | Refuse sampling-policy selection | Refuse sampling-policy selection |

The Android and iOS rows define configuration selection only. Existing unsupported capture and shader refusals still take precedence over any positive proof.

Upload and NDMF Play builds use the same target selection. Editor Play appearance is not evidence of VRChat runtime filtering.

Add `Build/PlatformAlphaSamplingPolicy.cs` with this pure interface:

```csharp
internal static bool TrySelect(
    BuildTarget target,
    bool acceptDesktopDifferences,
    bool acceptMobileDifferences,
    out AlphaSamplingPolicy policy);
```

A failed selection returns false and a conservative output value. The caller records a named unsupported-target report before analysis. It performs no conversion.

## 4. Separate captured facts from proof policy

Capture authored texture facts without reading global quality configuration.

Retain `TextureSampling.Filter`, `Wrap`, and `Aniso`. Capture `Aniso` as `Anisotropic` only when the authored level exceeds one.

Add `TextureSampling.ForceAnisotropyEligible`. Capture it as `texture.anisoLevel != 0`.

Extend the existing three-argument constructor with an optional fourth argument, `bool forceAnisotropyEligible = true`. Existing synthetic samples represent eligible textures unless they explicitly specify false.

The two-argument constructor also uses eligibility true. Include eligibility in equality and hashing. Reject an anisotropic sample with eligibility false as an invariant error.

This preserves the distinction between authored levels zero and one. It avoids inventing a numeric hardware footprint or a new sampler framework.

Add `Analysis/AlphaSamplingPolicy.cs`:

```csharp
internal enum AlphaSamplingPolicy
{
    ConservativeDesktop = 0,
    ConservativeMobile = 1,
    AcceptedNonAnisotropic = 2
}
```

Add `ForProof(TextureSampling authored)` as an extension in the same file. It returns a value without changing captured evidence:

| Policy | Effective anisotropy |
|---|---|
| `ConservativeDesktop` | Anisotropic when eligibility is true. None for authored level zero. |
| `ConservativeMobile` | Anisotropic for every captured texture. |
| `AcceptedNonAnisotropic` | None for every captured texture, including authored levels above one. |

Mobile conservatism covers the unresolved client override of level zero. It does not claim that every mobile texture actually samples anisotropically.

The projection preserves filter and wrap values. The existing mip conjunction covers possible between-level blending. Unsupported filter and wrap capture remains unsupported.

Reject undefined policy values with `ArgumentOutOfRangeException`. Do not turn programming errors into unsupported results.

## 5. Apply one policy to every alpha term

Require an explicit policy at the normalized resolver boundary:

```csharp
internal static AlphaResolution Resolve(
    SemanticOutput<ScalarSemanticValue> alpha,
    AlphaFieldProvider fieldProvider,
    AlphaSamplingPolicy samplingPolicy,
    int maxNoiseTexelPercent);
```

Thread that value through plain samples, scaled samples, mapped samples, product chains, saturating sums, and saturating differences.

Project sampling before each `AlphaResolution.Classified` construction. Keep constant and range-based uniform decisions unchanged.

Thread the policy through `AdmittedMaterialStates.ResolveSlot` and `ResolveAdmittedMaterialSemantics`. Pass it through the build's `ResolveRuntimeStates` path.

Source boundaries:

- `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:234-344`.
- `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:559-569`.
- `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:1206-1214`.
- `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:1329-1337`.

The direct `UnityRendererAlphaAnalysis` probe defaults to `ConservativeDesktop`. An explicit overload can receive the accepted policy for controlled public probes.

Do not consult scene components from that probe. It lacks build-policy and animation closure authority. Do not change the research package to obtain conversion.

Keep the classifier's anisotropy gates. Mixed anisotropic fields remain `Unknown`. Fully opaque levels retain their proof. Fully nonopaque levels retain their transparent result.

Keep mapped-alpha rules, mip provenance, absorbing transparent outcomes, exact affine UV transforms, and per-material field scoping unchanged.

Every admitted animation or material-swap state receives the same captured build policy. A texture replacement must not silently restore editor-quality dependence.

## 6. Build lifetime and apply

Capture the active target and selected policy once before per-renderer analysis. Store both on `AmusePlatformFinishState`.

Read acceptance from the build avatar's optimizer component, not a cached inspector selection. Never use static mutable policy state.

Before apply, compare the live active target and effective selected policy with the captured values. Reject stale candidates if either differs.

Use a named `SamplingPolicyChanged` slot refusal. Reject the whole prepared set because every candidate depends on this build-wide fact.

Reuse the existing finalization and clone sweep with an empty survivor set. Do not write meshes, materials, or animation curves on this path.

Source: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:103-138`.

Keep transient unlock closure intact. Do not skip the pass that restores locked originals.

## 7. Inspector and disclosure

Place the controls inside Alpha Separator Settings. Show both persistent acceptance toggles and the selected target group.

Use these labels:

- `Accept Desktop Anisotropy Differences`.
- `Accept Mobile Anisotropy Differences`.
- `Active Sampling Proof`.

When acceptance is false, show `Conservative anisotropic proof`.

When acceptance is true, show `Accepted non-anisotropic proof` and this warning:

> AMUSE proves opacity without anisotropic footprints. VRChat can use anisotropic filtering. Converted parts can lose transparency near texture edges or at oblique views. This does not change texture filtering.

For Android and iOS, also show:

> This target selects the mobile acceptance setting. AMUSE texture capture does not support this target. This setting does not enable mobile conversion.

For other targets, show:

> AMUSE does not support sampling-policy selection for this build target.

Do not silently write either toggle when the target changes. A target switch only changes which configuration the build reads.

Extend the existing summary and inspector status with the captured target group and proof mode. Disclose accepted non-anisotropic proof even when zero triangles move.

Keep this disclosure separate from `AlphaPolicyActive`. That flag describes existing alpha tolerances and its text assumes moved triangles.

Sources:

- `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:520-560`.
- `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:390-393`.

Do not add per-triangle reporting or repair unrelated report wording in this change.

## 8. Presets and migration

Move the closed preset schema from version 1 to version 2. Add these required Boolean configuration keys:

```json
"acceptDesktopAnisotropyDifferences": false,
"acceptMobileAnisotropyDifferences": false
```

Preset amendment on 2026-10-11: Set both acceptance values to false in Safe and true in Normal and Aggressive. Safe also disables `ignoreOutOfRangeMaterialSlots` and `allowDepthTestChange`. All other values remain unchanged.

Update parser, preset values, matching, application, and tests together. Applying a shipped preset replaces both acceptance settings through the normal Undo path.

Read schema version before version-specific keys. Refuse version 1 with `UnknownSchemaVersion`. Do not retain a compatibility parser or silently invent acceptance values.

The package replaces its shipped preset files in the same release. Document the version-2 keys for custom preset authors.

## 9. Acceptance criteria

The implementation must satisfy these criteria:

1. Desktop acceptance restores eligible local proofs under Forced On editor quality.
2. The same synthetic regular lilToon cutout and transparent inputs remain unconverted without desktop acceptance when their mip fields are mixed.
3. Conservative mode still converts fully opaque consulted fields.
4. Every consulted mip must pass. Unknown provenance and later transparent mips still block conversion.
5. Plain, mapped, product, sum, and difference forms receive one policy without recursive resets.
6. Desktop level-zero textures retain the documented exception. Mobile conservatism does not assume that exception.
7. Changing editor anisotropy mode does not change captured sampling facts or selected proof outcomes.
8. Switching target groups selects independent stored acceptance without changing either value.
9. Unsupported targets remain refused. Mobile acceptance never bypasses the current capture gate.
10. A target or effective-policy change before apply prevents all stale writes and sweeps unused generated clones.
11. Reports name the selected proof mode, including accepted builds that move zero triangles.
12. Source assets and editor quality configuration remain unchanged.
13. Presets parse and apply both fields atomically. Safe clears both acceptance fields. Normal and Aggressive grant both.

## 10. Validation and stop conditions

Use public synthetic fixtures and the verified vendor seams. Run tests only in the dev editor instance after an exact project identity check.

Require observed RED/GREEN for plausible defects. Compilation and zero-test runs do not count as proof.

Run focused policy, resolver, preset, and build tests. Then run the product and research EditMode assemblies.

Run a separate public NDMF smoke build for regular cutout and transparent materials under both acceptance states. Inspect the actual generated slots and unchanged source references.

Exercise the inspector after changing target groups. If the required platform module is unavailable, report the unexercised target-switch surface.

Private validation is not authorized by this document. Any later Census Lab build requires separate authorization. Never run tests there.

Stop if implementation requires a numerical anisotropic footprint bound, mobile shader support, texture mutation, or new runtime filtering controls.

Stop if SDK export contradicts the desktop level-zero assumption. Preserve affected textures conservatively until that compatibility boundary is resolved.

## 11. Alternatives

A simple rollback restores the old assumption but hides runtime filtering risk. Reject that approach.

A bounded local anisotropic classifier can improve conservative coverage. The investigation does not establish the required footprint contract. It is outside this repair.

The selected approach reuses the existing local classifier and conservative fast paths. It adds only the facts and policy needed for explicit platform-specific acceptance.

## 12. Authorization boundary

The user authorized branch rename and subagent-driven implementation on 2026-10-11. This approval covers the defined public-project changes and validation.

Staging, commits, publication, and private asset changes remain unauthorized. Validation evidence must precede any claim that the regression is fixed.
