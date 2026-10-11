# NDMF console info reporting for anisotropy refusals design

Privacy note: This document uses public source references and sanitized findings. It contains no private asset identifiers or machine identities.

Date: 2026-10-11.
Status on 2026-10-11: Design proposal complete. Awaiting user review before implementation plan creation.
Base: `feature/ndmf-anisotropy-reporting` at `4b6197c`.
Topic branch: `feature/ndmf-anisotropy-reporting`.

## 1. Goal and approved contract

Report material slot refusals caused by texture anisotropy differences in the NDMF console.

Use information severity for the report. A refusal is an ordinary AMUSE outcome and never a build failure.

Use a dedicated `AlphaSeparationSlotRefusal` member: `AnisotropyDifferences`.

Maintain architectural purity across the codebase:
- Keep the pure analysis core free of mutation and host dependencies.
- Keep immutable evidence free of live Unity object references.
- Fail closed when encountering unknown or unproven data.
- Do not use mock tests. Use synthetic public fixtures and established verified test seams.

## 2. Background and problem statement

AMUSE introduced platform anisotropy acceptance on 2026-10-10.

When the user leaves desktop or mobile acceptance disabled, AMUSE selects a conservative sampling policy:
- `StandaloneWindows64` selects `AlphaSamplingPolicy.ConservativeDesktop`.
- `Android` and `iOS` select `AlphaSamplingPolicy.ConservativeMobile`.

Under conservative sampling policies, textures with `anisoLevel > 0` project to `TextureAnisoMode.Anisotropic`.

In `TriangleAlphaClassifier`, an anisotropic texture with mixed alpha texels returns `TriangleAlphaOutcome.Unknown`. The classifier cannot bound an elongated anisotropic footprint on mixed texels without explicit user acceptance. Only fully opaque or fully non-opaque mip chains prove.

In `MeshSeparationPlanner`, triangles with `Unknown` outcome join the transparent partition.

When every triangle of a submesh remains `Unknown`, the planner marks the submesh with `SubmeshSeparationDisposition.Unchanged`.

Before this design:
- In `AlphaSeparationPreparation.Prepare`, an `Unchanged` submesh was skipped silently.
- If no submesh on a renderer had opaque candidates, `Prepare` returned `null` immediately.
- Zero slot refusals were recorded in `AmusePlatformFinishState`.
- Zero reports were emitted to the NDMF console.
- An avatar owner whose materials did not convert had no explanation in the NDMF console.

## 3. Dedicated slot refusal member

Add a dedicated member to `AlphaSeparationSlotRefusal` in `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`:

```csharp
/// <summary>
/// The slot was left unchanged because its texture uses anisotropic sampling
/// under the active conservative proof policy. The proof could not bound
/// the elongated anisotropic footprint on mixed alpha texels. Enabling
/// desktop or mobile anisotropy acceptance allows AMUSE to prove opacity
/// without anisotropic footprints.
/// </summary>
AnisotropyDifferences,
```

Place `AnisotropyDifferences` in the slot-scoped section of `AlphaSeparationSlotRefusal`.

## 4. Pure analysis detection

Maintain module layering. Keep `Analysis/` pure and immutable.

Expose anisotropic sampling presence on `AlphaResolution` in `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`:

```csharp
internal bool HasAnisotropicSampling { get; }
```

Evaluation rules for `HasAnisotropicSampling`:
- A classified resolution checks its effective sampling: `_sampling.Aniso == TextureAnisoMode.Anisotropic`.
- A product resolution recurses on its factors: `_firstFactor.HasAnisotropicSampling || _secondFactor.HasAnisotropicSampling`.
- A disjunction resolution recurses on its factors: `_firstFactor.HasAnisotropicSampling || _secondFactor.HasAnisotropicSampling`.
- Uniform resolutions and refused resolutions evaluate to false.

Expose anisotropic sampling presence on `SlotResolutionResult` in `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`:

```csharp
internal bool HasAnisotropicSampling
{
    get
    {
        if (Resolutions == null || Resolutions.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < Resolutions.Count; i++)
        {
            if (Resolutions[i].HasAnisotropicSampling)
            {
                return true;
            }
        }

        return false;
    }
}
```

This query requires zero memory allocation. It examines only existing immutable resolution records.

## 5. Preparation pass refusal and emission

Pass the slot resolution results of the renderer into `AlphaSeparationPreparation.Prepare` in `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`:

```csharp
internal static PreparedRendererSeparation Prepare(
    AmusePlatformFinishState state,
    UnityRendererMutationTarget target,
    string rendererPath,
    MeshSeparationPlan plan,
    CapturedAnimationEvidence evidence,
    IReadOnlyList<Material> admittedLiveMaterials,
    VerifiedPoiyomiConversion poiyomiConversion,
    VerifiedLilToonConversion lilToonConversion,
    int minimumOpaqueCoveragePercent,
    IReadOnlyList<SlotResolutionResult> slotResults,
    string rendererTypeName = null,
    bool allowDepthTestChange = false,
    IReadOnlyCollection<string> grantedShaderNames = null)
```

In `AlphaSeparationPreparation.Prepare`:

Do not return early before inspecting submeshes when `!plan.HasAnyOpaqueCandidates`.

For each submesh in `plan.Submeshes`:

1. Evaluate whether the submesh was left `Unchanged` because of anisotropy differences:
   - The submesh disposition is `SubmeshSeparationDisposition.Unchanged`.
   - The submesh material slot index is valid within `slotResults`.
   - The slot resolution is resolved (`slotResults[slotIndex].IsResolved`).
   - The slot resolution has anisotropic sampling (`slotResults[slotIndex].HasAnisotropicSampling`).
   - The submesh outcomes in `plan.Source` contain at least one `TriangleAlphaOutcome.Unknown`.

2. When these conditions match:
   - Record the refusal in state: `state.RecordSlotRefusal(AlphaSeparationSlotRefusal.AnisotropyDifferences)`.
   - Emit the NDMF console report:
     `AmuseReports.SlotSeparationRefusal(target.Renderer, submesh.SourceMaterialBindingIndex, AlphaSeparationSlotRefusal.AnisotropyDifferences)`.
   - Continue to the next submesh.

3. Evaluate whether a `Split` submesh below minimum coverage had its proof limited by anisotropy:
   - The submesh disposition is `SubmeshSeparationDisposition.Split`.
   - `OpaqueCoverageBelow(minimumOpaqueCoveragePercent, submesh)` is true.
   - If the slot has anisotropic sampling (`slotResults[slotIndex].HasAnisotropicSampling`) and contains `TriangleAlphaOutcome.Unknown` triangles:
     The lack of required opaque coverage was caused by unproven anisotropic triangles.
     Record `AlphaSeparationSlotRefusal.AnisotropyDifferences` and emit the refusal report for `AnisotropyDifferences`.
   - Otherwise, record and emit `AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum`.

4. After submesh evaluation, if `!plan.HasAnyOpaqueCandidates` or no candidate slots survived:
   Return `null` as before. Clones and separation records are created only when valid candidate slots survive.

## 6. String table and report formatting

Add report strings to `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`:

```csharp
["amuse.slotSeparation.AnisotropyDifferences"] =
    "AMUSE left material slot {0} of renderer '{2}' unchanged.",
["amuse.slotSeparation.AnisotropyDifferences:description"] =
    "AMUSE cannot prove opacity for slot {0} of renderer '{2}'. " +
    "Its texture uses anisotropic sampling under the active conservative proof policy. " +
    "Reason: {1}. The slot keeps its original material.",
["amuse.slotSeparation.AnisotropyDifferences:hint"] =
    "Enable 'Accept Desktop Anisotropy Differences' (or mobile) on the AMUSE component " +
    "if you want AMUSE to prove opacity without anisotropic footprints.",
```

Follow Simplified Technical English (ASD-STE100):
- Short active sentences.
- One idea per sentence.
- No semicolons.
- No contractions ("cannot", "its", "does not").
- Clear instructions in the hint.

The report helper `AmuseReports.SlotSeparationRefusal` already formats `{0}` as slot index, `{1}` as cause name, `{2}` as renderer name, `{3}` as offending material, and `{4}` as mapping names. It uses `ErrorSeverity.Information` and scopes the report under `ErrorReport.WithContextObject(renderer)`.

## 7. Testing strategy

Do not use mock tests. Test through synthetic public fixtures and verified seams.

1. String table completeness:
   - `AmuseReportsSlotTests.EverySlotSeparationCauseHasPlainEnglishStrings` runs automatically.
   - It asserts that title, description, and hint keys exist for every `AlphaSeparationSlotRefusal` member.

2. Typed report helper test in `AmuseReportsSlotTests`:
   - Captures errors with `ErrorReport.CaptureErrors`.
   - Invokes `AmuseReports.SlotSeparationRefusal` with `AlphaSeparationSlotRefusal.AnisotropyDifferences`.
   - Asserts that one error entry is reported.
   - Asserts that the title key matches `amuse.slotSeparation.AnisotropyDifferences`.
   - Asserts that the rendered message contains the slot index, renderer name, and hint text.

3. Pure analysis tests in `AlphaSemanticsResolverTests`:
   - Asserts that `AlphaResolution.Classified` with anisotropic sampling returns `HasAnisotropicSampling == true`.
   - Asserts that `AlphaResolution.Classified` with non-anisotropic sampling returns `HasAnisotropicSampling == false`.
   - Asserts that product and disjunction resolutions propagate `HasAnisotropicSampling`.

4. End-to-end integration tests in `AmusePlatformFinishPluginTests`:
   - Use the public synthetic mixed-field fixture (`ImportMixedHalfTexture`).
   - Run under `QualitySettings.anisotropicFiltering = ForceEnable` with `AcceptDesktopAnisotropyDifferences = false`.
   - Assert that `state.SlotRefusalCount(AlphaSeparationSlotRefusal.AnisotropyDifferences)` equals 1.
   - Assert that `ErrorReport.CaptureErrors` captures an entry with key `amuse.slotSeparation.AnisotropyDifferences`.
   - Assert that the report text names the renderer, the slot index, and the acceptance recommendation.
   - Run the same fixture with `AcceptDesktopAnisotropyDifferences = true`.
   - Assert that `state.SlotRefusalCount(AlphaSeparationSlotRefusal.AnisotropyDifferences)` is zero.
   - Assert that the opaque triangle is successfully proven and prepared.
