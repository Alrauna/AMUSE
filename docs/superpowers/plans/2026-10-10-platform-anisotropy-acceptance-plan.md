# Platform Anisotropy Acceptance Implementation Plan

> For agentic workers: REQUIRED SUB-SKILL: Use `executing-plans` for inline work. Use `subagent-driven-development` only with user authorization. Steps use checkbox syntax.

Goal: Restore policy-authorized local opacity proofs through independent desktop and mobile acceptance settings.

Architecture: Capture authored texture facts without editor quality dependence. Select an immutable proof policy from the active target and the build avatar. Pass it through every alpha resolution and reject stale policy before apply.

Tech Stack: Unity 2022.3 C#, NDMF, NUnit EditMode tests, existing Newtonsoft.Json preset parsing.

Spec: `docs/superpowers/specs/2026-10-10-platform-anisotropy-acceptance-design.md`.

Privacy note: This plan uses public source paths and synthetic fixtures. It contains no private asset identifiers or machine identities.

Date: 2026-10-10.
Status on 2026-10-11: Tasks 1 through 6 and final validation are complete. Full validation reported 2843 passed, zero failed, and two gated DAO cases inconclusive. Changes remain uncommitted.
Base: `main` at `adfcd17`.
Topic branch: `feature/platform-anisotropy-proof`.

## Global constraints

- Unity 2022.3.22f1 or newer admitted f-releases.
- NDMF `[1.14.4, 2.0.0)`.
- VRChat SDK Base and Avatars `[3.10.4, 4.0.0)`.
- No new dependencies.
- No source asset or editor quality mutations in production.
- No change to shader attestation, animation admission, mip scope, cutoff rules, or conversion eligibility.
- No research package content in the product or VPM package.
- Both acceptance fields default to false.
- Preset amendment on 2026-10-11: Safe clears both acceptance fields, `ignoreOutOfRangeMaterialSlots`, and `allowDepthTestChange`. Normal and Aggressive grant both acceptance fields. All other values remain unchanged.
- Only `StandaloneWindows64` admits product texture capture at this base.
- Android and iOS policy selection does not enable mobile conversion.
- Preserve the classifier's conservative anisotropy gates.
- Do not stage, commit, push, publish, or change private assets without separate authorization.

## Execution brief and stop conditions

The allowed implementation touches only the files and interfaces below. Rebase the plan against live source before execution if the base changes.

Inspect existing changes without absorbing them. The investigation document and unrelated temporary metadata remain outside implementation staging.

Use exact project identity checks before Unity operations. Enumerate instances, inspect `Application.dataPath`, normalize it, and pin the exact `<repo-root>/Assets` match.

Never run tests in the Census Lab. Public test assets must have paired metadata. Remove temporary synthetic assets through the existing fixture teardown.

Observe a failing behavior assertion before each production change when practical. Missing-symbol compilation failures are not behavioral RED evidence.

For new interfaces, first observe the existing public resolver's wrong outcome. Then add the interface test and implement the behavior.

Record observed counts, failures, and Console categories. A filter that runs zero tests fails validation.

Stop if the repair requires bounded hardware footprints, mobile shader support, texture mutations, or runtime filtering controls.

Stop if SDK export contradicts the desktop level-zero assumption. Keep the affected proof conservative and return evidence.

## File map

Create only these production units:

| New file | Responsibility |
|---|---|
| `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSamplingPolicy.cs` | Closed proof policies and value-only sampling projection. |
| `Packages/com.alrauna.amuse/Editor/Build/PlatformAlphaSamplingPolicy.cs` | Exact active-target selection without Unity state access. |

Modify these existing boundaries:

| Existing unit | Change |
|---|---|
| `Semantics/MaterialSemantics.cs` | Capture force eligibility as a sampler fact. |
| `Semantics/UnityTextureEvidence.cs` | Remove global quality dependence from authored capture. |
| `Analysis/AlphaSemanticsResolver.cs` | Project every sampled term with one explicit policy. |
| `Analysis/AdmittedMaterialStates.cs` | Pass the policy through each admitted state. |
| `Host/UnityRendererAlphaAnalysis.cs` | Keep direct probes conservative by default. |
| `Runtime/AmuseAvatarOptimizer.cs` | Store independent acceptance fields. |
| `Editor/AmuseAvatarOptimizerEditor.cs` | Show target selection and explicit risk. |
| `Editor/Build/AmusePlatformFinishPlugin.cs` | Capture target and policy once. |
| `Editor/Build/AlphaSeparationApply.cs` | Refuse stale policy before any write. |
| `Editor/Build/AlphaSeparationRecords.cs` | Add the named stale-policy refusal. |
| `Editor/Build/AmuseReports.cs` | Disclose selected mode in summary and status. |
| `Editor/Build/AmuseReportStrings.cs` | Provide mode and refusal text. |
| `Editor/Presets/OptimizerPreset.cs` | Carry and compare both acceptance values. |
| `Editor/Presets/PresetParser.cs` | Parse the closed version-2 schema. |
| `Editor/Presets/PresetApplier.cs` | Apply both values through serialization and Undo. |
| `Presets/safe.json`, `normal.json`, `aggressive.json` | Migrate shipped presets with no implicit acceptance. |
| `README.md` | Describe proof modes, risk, platform limits, and preset migration. |

All shortened paths in this table are relative to `Packages/com.alrauna.amuse/`. `Semantics/`, `Analysis/`, and `Host/` are under its `Editor/` folder.

No root changelog exists at this base. Update the existing README rather than creating an unrelated release record.

## Task 1: Capture authored sampling facts and select the proof footprint

Files:

- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs:143-199`.
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:121-170`.
- Create: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSamplingPolicy.cs` and its Unity metadata.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/MaterialSemanticsTests.cs`.
- Create test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSamplingPolicyTests.cs` and its Unity metadata.

Interfaces:

- Add `TextureSampling.ForceAnisotropyEligible`.
- Add the optional fourth constructor argument `bool forceAnisotropyEligible = true`.
- Add `AlphaSamplingPolicy` with values `ConservativeDesktop`, `ConservativeMobile`, and `AcceptedNonAnisotropic`.
- Add `TextureSampling ForProof(this AlphaSamplingPolicy policy, TextureSampling authored)`.

- [x] Add a capture regression that compares the same level-1 texture under all three editor quality modes.

Use the existing `Import` helper. Save and restore global quality configuration in `finally`.

```csharp
var original = QualitySettings.anisotropicFiltering;
try
{
    var texture = Import("sampling_authored", sourceHasAlpha: true);
    texture.anisoLevel = 1;
    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
    Assert.That(UnityTextureEvidence.TryGetSampling(texture, out var first), Is.True);
    QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
    Assert.That(UnityTextureEvidence.TryGetSampling(texture, out var second), Is.True);
    Assert.That(second, Is.EqualTo(first));
}
finally
{
    QualitySettings.anisotropicFiltering = original;
}
```

- [x] Run `UnityTextureEvidenceTests` in the dev editor instance and observe the equality failure.
- [x] Add level-zero and above-one cases that exercise effective proof outcomes, not only field copies.
- [x] Add projection tests for undefined policy values and impossible anisotropic eligibility.
- [x] Implement authored capture and delete `LevelOneSamplesAnisotropically`.

```csharp
var aniso = texture.anisoLevel > 1
    ? TextureAnisoMode.Anisotropic
    : TextureAnisoMode.None;
sampling = new TextureSampling(
    filter, wrapU, aniso, texture.anisoLevel != 0);
```

- [x] Implement the policy projection in the new analysis file.

```csharp
internal static TextureSampling ForProof(
    this AlphaSamplingPolicy policy, TextureSampling authored)
{
    TextureAnisoMode aniso;
    switch (policy)
    {
        case AlphaSamplingPolicy.ConservativeDesktop:
            aniso = authored.ForceAnisotropyEligible
                ? TextureAnisoMode.Anisotropic : TextureAnisoMode.None;
            break;
        case AlphaSamplingPolicy.ConservativeMobile:
            aniso = TextureAnisoMode.Anisotropic;
            break;
        case AlphaSamplingPolicy.AcceptedNonAnisotropic:
            aniso = TextureAnisoMode.None;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(policy));
    }
    return new TextureSampling(authored.Filter, authored.Wrap, aniso);
}
```

The result is effective proof sampling, not authored capture. Its constructor uses eligible true because no downstream capture reads this projection.

- [x] Include eligibility in sampler equality and hashing.
- [x] Replace the old quality-dependent capture assertions with the new authored-fact contract.

Do not delete hardware characterization findings. The old tests described effective sampling, which now belongs to policy projection.

- [x] Run focused capture and projection tests and observe GREEN.

## Task 2: Carry one policy through every normalized alpha expression

Files:

- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`.
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`.
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`.

Interfaces:

- Require `AlphaSamplingPolicy samplingPolicy` before `maxNoiseTexelPercent` in `AlphaSemanticsResolver.Resolve`.
- Require the policy before the density argument in both state-admission resolution methods.
- Default direct host analysis to `ConservativeDesktop` and expose an explicit policy argument for controlled probes.
- Remove the old resolver signature and migrate every caller, including tests and any research caller.

- [x] Add an existing-API RED case with explicitly anisotropic sampling and a mixed field whose opaque corner needs local proof.
- [x] Run that test and observe `Unknown` instead of the requested policy-authorized `ProvenOpaque`.
- [x] Add a paired policy test using the existing `Sample`, `Providing`, `MixedField`, and `OpaqueCornerTriangle` helpers.

```csharp
[TestCase(AlphaSamplingPolicy.ConservativeDesktop, TriangleAlphaOutcome.Unknown)]
[TestCase(AlphaSamplingPolicy.AcceptedNonAnisotropic, TriangleAlphaOutcome.ProvenOpaque)]
public void SelectedPolicyControlsMixedFieldProof(
    AlphaSamplingPolicy policy, TriangleAlphaOutcome expected)
{
    var alpha = SemanticOutput<ScalarSemanticValue>.Complete(
        ScalarSemanticValue.Texture(Sample(), TextureChannel.Alpha));
    var resolution = AlphaSemanticsResolver.Resolve(
        alpha, Providing(MixedField()), policy, 0);
    Assert.That(resolution.Classify(OpaqueCornerTriangle()), Is.EqualTo(expected));
}
```

- [x] Implement policy propagation through every recursive expression branch.

At both plain and mapped `Classified` factories, use:

```csharp
samplingPolicy.ForProof(sample.Sampling)
```

In scaled samples, product chains, sums, and differences, pass the same `samplingPolicy` to every nested resolver.

- [x] Migrate existing local-footprint tests with an explicit accepted policy where that is their stated precondition.

Do not grant acceptance in a global test setup. Keep every expected geometry, cutoff, animation, and refusal assertion intact.

- [x] Add paired tests for these plausible propagation failures:

| Case | Required assertion |
|---|---|
| Mapped sample | Accepted local opaque corner passes. Conservative mixed field stays unknown. |
| Product with mixed main or mask | Either anisotropic factor blocks conservative proof. Acceptance reaches both factors. |
| Sum and zero-subtrahend difference | Nested samples retain policy instead of reverting to a default. |
| Fully opaque chain | Both modes remain opaque. |
| Unknown first mip and transparent later mip | Transparent result wins in both modes. |
| Missing mip provenance | Acceptance does not authorize fabricated evidence. |
| Different admitted materials | Each material uses its own field provider under the same policy. |

- [x] Run resolver, admission, and host tests and observe GREEN.
- [x] Find all resolver references. The environment had no language server. Reference searches covered product, tests, and research.

## Task 3: Select persistent acceptance by active build target

Files:

- Modify: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`.
- Create: `Packages/com.alrauna.amuse/Editor/Build/PlatformAlphaSamplingPolicy.cs` and its Unity metadata.
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`.
- Create test: `Packages/com.alrauna.amuse/Tests/Editor/Build/PlatformAlphaSamplingPolicyTests.cs` and its Unity metadata.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`.

Interfaces:

```csharp
internal static bool TrySelect(
    BuildTarget target, bool acceptDesktopDifferences,
    bool acceptMobileDifferences, out AlphaSamplingPolicy policy);
```

Add `SamplingTarget`, `SamplingPolicy`, and `HasSamplingPolicy` to `AmusePlatformFinishState`. Capture them before renderer analysis.

- [x] Add a real build regression where desktop acceptance requests a mixed-field conversion under Forced On quality.
- [x] Run the regression and observe that no opaque slot appears before production integration.
- [x] Implement the serialized fields and getters from spec section 3.
- [x] Implement the exact target map.

```csharp
policy = AlphaSamplingPolicy.ConservativeMobile;
switch (target)
{
    case BuildTarget.StandaloneWindows64:
        policy = acceptDesktopDifferences
            ? AlphaSamplingPolicy.AcceptedNonAnisotropic
            : AlphaSamplingPolicy.ConservativeDesktop;
        return true;
    case BuildTarget.Android:
    case BuildTarget.iOS:
        policy = acceptMobileDifferences
            ? AlphaSamplingPolicy.AcceptedNonAnisotropic
            : AlphaSamplingPolicy.ConservativeMobile;
        return true;
    default:
        return false;
}
```

- [x] Add the named report key `amuse.sampling.UnsupportedBuildTarget` for failed selection.
- [x] Pass the captured policy through `ResolveRuntimeStates` and all direct diagnostic entry points.
- [x] Exercise desktop and mobile choices with opposite stored acceptance values.
- [x] Assert that unsupported targets return false regardless of acceptance.
- [x] Assert that Android and iOS still fail the existing product texture-capture admission gate.
- [x] Run target-selection and plugin regressions and observe GREEN.

## Task 4: Refuse stale target or acceptance before apply

Files:

- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`.
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`.
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`.

Interfaces:

- Add `AlphaSeparationSlotRefusal.SamplingPolicyChanged`.
- Use the stored target and policy from Task 3.
- Reuse `SweepUnreferencedClones` and existing refusal reporting.

- [x] Use the existing between-pass `ProbeScope` to revoke desktop acceptance after preparation.

```csharp
BuildContext builtContext;
using (new ProbeScope(context =>
{
    var optimizer = context.AvatarRootObject.GetComponent<AmuseAvatarOptimizer>();
    var serialized = new SerializedObject(optimizer);
    serialized.FindProperty("_acceptDesktopAnisotropyDifferences").boolValue = false;
    serialized.ApplyModifiedPropertiesWithoutUndo();
}))
{
    builtContext = AvatarProcessor.ProcessAvatar(root, ApplyTestPlatform.Instance);
}
```

Use the existing test lifecycle invocation in `AlphaSeparationApplyTests`. Do not add a fake application path.

- [x] Assert exact unchanged live mesh, materials, and curve values, plus the named refusal and destroyed unused clones.
- [x] Run the case and observe stale writes before adding the guard.
- [x] Before survivor validation, select the current effective policy and compare target and policy with captured state.
- [x] If selection fails or either value differs, record `SamplingPolicyChanged` for every prepared candidate.
- [x] Supply empty survivor lists to the existing clone sweep and return a refused preparation decision.
- [x] Preserve unlock closure and the zero-write summary path.
- [x] Add a target-mismatch case by seeding a mismatched captured target in the existing between-pass probe.

This falsifies the guard without installing platform modules or changing global build target inside a unit test.

- [x] Run apply and transient-unlock tests and observe GREEN.

## Task 5: Expose acceptance and migrate presets atomically

Files:

- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`.
- Modify: `Packages/com.alrauna.amuse/Editor/Presets/OptimizerPreset.cs`.
- Modify: `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs`.
- Modify: `Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs`.
- Modify: `Packages/com.alrauna.amuse/Presets/safe.json`.
- Modify: `Packages/com.alrauna.amuse/Presets/normal.json`.
- Modify: `Packages/com.alrauna.amuse/Presets/aggressive.json`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/OptimizerPresetTests.cs`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetApplierTests.cs`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetFileStoreTests.cs`.

Interfaces:

- Add the two acceptance values to `OptimizerPreset` construction, properties, and matching.
- Set `PresetParser.CurrentSchemaVersion = 2`.
- Require both Boolean keys in the closed settings object.

- [x] Add a preset application regression with both acceptance fields initially true.
- [x] Apply Safe and assert that both values become false through the normal serialized apply path.
- [x] Run the test and observe retained acceptance before the preset migration.
- [x] Migrate all preset fixtures and shipped JSON to version 2.

```json
"schemaVersion": 2
```

Add these entries inside `settings`:

```json
"acceptDesktopAnisotropyDifferences": false,
"acceptMobileAnisotropyDifferences": false
```

- [x] Parse version before version-specific keys and return `UnknownSchemaVersion` for version 1.
- [x] Reject missing keys, non-Boolean values, and unknown keys without partial application.
- [x] Apply both fields with `SerializedObject.FindProperty` and include them in preset matching.
- [x] Draw both toggles with the exact labels and warnings in spec section 7.
- [x] Use `TrySelect` for inspector mode display without persisting the selection.
- [x] Run preset tests and observe GREEN.
- [x] Exercise Undo, preset reset, and target-group display in the actual inspector.

Do not add permanent tests that pin warning text or inspector source structure. Manual surface observation proves the presentation.

## Task 6: Disclose the selected proof and prove real lilToon conversion

Files:

- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs`.
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`.
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` summary call.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationSplitTests.cs`.
- Modify documentation: `README.md`.

Interfaces:

- Extend `AmuseReports.AvatarSummary` with the captured target and sampling policy.
- Keep sampling disclosure separate from `AlphaPolicyActive`.
- Preserve summary argument structure and existing applied counts.

- [x] Add a report behavior regression for accepted non-anisotropic mode with zero moved triangles.
- [x] Assert the selected policy category is present, without pinning the full wording.
- [x] Run the case and observe missing disclosure before the report change.
- [x] Add summary mode descriptions and preserve both existing tolerance disclosure and new sampling disclosure.
- [x] Reuse verified lilToon seams for regular cutout and transparent materials.
- [x] Construct a public 8-by-8 mixed alpha texture with Bilinear filtering, authored level one, and no mip chain.

Use an opaque left half and a nonopaque right half. Place one triangle well inside each half, away from the bilinear boundary.

```csharp
for (var y = 0; y < 8; y++)
{
    for (var x = 0; x < 8; x++)
    {
        pixels[y * 8 + x] = new Color32(64, 32, 16, (byte)(x < 4 ? 255 : 0));
    }
}
```

Use `TestTextureImport.WritePng`, uncompressed alpha-bearing import, Clamp wrap, and `FixtureProofScope.PinAllSizes`.

For the opaque triangle, use UV coordinates `(0.125, 0.25)`, `(0.25, 0.25)`, and `(0.125, 0.5)`.

For the transparent triangle, use `(0.75, 0.25)`, `(0.875, 0.25)`, and `(0.75, 0.5)`.

- [x] Keep all non-sampling conversion preconditions satisfied through the established verified seams.
- [x] Run both source modes with acceptance false and true under Forced On quality.
- [x] Assert an actual appended opaque submesh and material only with acceptance true.
- [x] Assert that the transparent triangle keeps its original material in both states.
- [x] Repeat with fully opaque evidence and assert conversion without acceptance.
- [x] Repeat the accepted case with a later transparent mip and assert that it does not convert.
- [x] Keep editor quality restoration and fixture deletion in `finally` or existing teardown.
- [x] Run focused build and report tests and observe GREEN.

Update the README after smoke proof. Explain that acceptance changes proof scope, not runtime texture filtering. Document the unsupported mobile capture boundary and custom preset migration.

## Final validation and delivery

- [x] Refresh the dev editor and inspect Console for compilation errors.
- [x] Run all focused tests from Tasks 1 through 6 and record observed counts.
- [x] Run `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` and record observed counts.
- [x] Launch a separate public synthetic NDMF smoke build with cutout and transparent lilToon materials under both desktop acceptance states.
- [x] Inspect generated slots and triangle placement, rather than only test assertions or preparation counters.
- [x] Compare original mesh, material, texture, import, and animation references before and after the smoke build.
- [x] Observe the actual inspector under desktop and an installed mobile target group.
- [x] The installed Android target module allowed the interactive target-switch observation. No module blocker applies.
- [x] Make sure that unsupported targets still refuse and no mobile conversion claim appears.
- [x] Remove throwaway smoke assets and retain only public regression fixtures with paired metadata.
- [x] Sweep changed documents for private names, instance hashes, absolute paths, and network identifiers.
- [x] Run whitespace checks and inspect intended changes separately from pre-existing changes.
- [x] Report changes, observed RED/GREEN, smoke results, remaining platform limits, and the Git authorization boundary.

No private Census build is part of this plan. No live VRChat rendering comparison is implied by synthetic conversion proof.

## Spec coverage map

| Spec requirement | Plan task |
|---|---|
| Explicit accepted proof contract | Tasks 2, 5, and 6 |
| Independent platform selection | Task 3 |
| Authored sampling facts | Task 1 |
| Every normalized alpha form | Task 2 |
| Immutable build policy and stale apply refusal | Tasks 3 and 4 |
| Inspector and disclosure | Tasks 5 and 6 |
| Preset version-2 migration | Task 5 |
| Conversion, mip, and source-preservation criteria | Task 6 and final validation |
| Existing platform gates and authorization | Global constraints and Task 3 |

## Execution evidence on 2026-10-11

The six focused task checkpoints passed 80, 462, 105, 87, 33, and 203 tests. Each reported zero failures and zero skips. These runs preceded the final test-helper consolidation.

The final independent four-build NDMF smoke followed that consolidation. It exercised both shared helpers. Conservative mode kept both triangles on the source. Accepted mode moved only the opaque triangle for cutout and transparent materials. It preserved the source mesh, material, texture import settings, and authored animation curve. The appended curve mapped the converted state to its generated clone and kept the already-opaque swap state unchanged. Teardown restored quality and deleted the fixture folder.

The actual inspector showed the desktop and Android proof groups. Safe reset both acceptance fields. Undo restored them. The target switch preserved the stored values. The original target and selection were restored.

Both final reviewers approved the corrected feature with no open findings. The fixture cleanup reused existing helpers. The paired stale-policy falsifiers stayed explicit.

Three initial full-suite attempts failed initialization and ran zero tests. The native save prompt blocked the runner on the existing unsaved dev scene. The controller first selected Cancel. The user later authorized Don't Save. The final run proceeded after that continuation.

Native plugin and memory-protection errors remained in Console. No C# compilation error was observed after the final refresh. The final smoke loaded and exercised the changed helpers.

No Census Lab build or VRChat rendering comparison ran. No staging, commit, push, or publication was authorized or performed.

On 2026-10-11, the identifier sweep checked 33 changed documents and workspace records with zero hits. New code and metadata passed whitespace checks. All four new code files have paired metadata. The final working tree had no staged changes or editor settings drift. The earlier investigation and temporary metadata remained untouched.

On 2026-10-11, the user authorized Don't Save for the test-runner prompt. The controller continued the prompt in the exact dev editor window without writing a scene asset. The scene then reported clean.

The first complete run exposed fixture preconditions and a stale serialized-object ordering defect in a test helper. Two workers corrected the four affected test files without changing production behavior or assertions. The final scoped review found no open defects.

The corrected four-class integration run passed 101 tests with zero failures and zero skips. The final complete product and research run reported 2843 passed, zero failed, and zero skipped. Two DAO cases were inconclusive because they require a disposable integration project and an explicit environment gate. That gate was not enabled in this project.

The runner catalog and progress counters include different case sets from the result summary. The delivered count uses the observed result summary and lists the two inconclusive cases separately.

Final cleanup removed six empty fixture folders and their metadata created by these validation runs. It left the sixteen pre-existing temporary metadata files untouched. No editor settings drift or staged changes remained.

## Preset amendment evidence on 2026-10-11

The user requested both anisotropy acceptance settings on in Normal and Aggressive. The user also requested slot and depth-test exceptions off in Safe.

Before the edit, the real preset loader and applier failed all three requested state transitions on a temporary component. After the edit and asset refresh, all three transitions passed. Each transition began with the four fields set opposite to the required values.

The amendment changes preset data, descriptions, and documentation. It does not change proof logic, runtime defaults, the schema version, or mobile capture support.

The first correctly filtered preset run exposed `EveryShippedFileHoldsTheShippedDefaults`. That test required every preset to share the same values. The user amendment invalidates that incidental-default assertion, so it was removed rather than updated to pin new values.

The retained preset suite then passed 32 tests with zero failures and zero skips. Two earlier filter attempts selected zero tests and are not validation evidence. The final Console query found no C# compiler errors.

The document identifier sweep and tracked whitespace check passed. No source asset or metadata change was needed for this amendment. Changes remain uncommitted.
