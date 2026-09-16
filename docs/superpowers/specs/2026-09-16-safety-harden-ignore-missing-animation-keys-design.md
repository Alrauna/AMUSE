# Design: Safety hardening for missing animation keys and components during renderer proofing

Date: 2026-09-16. Status: draft specification.

## Privacy note

This document contains observations from synthetic fixtures and characterization testing. It contains no private avatar, renderer, material, animation clip, or controller names. It contains no machine paths, network ports, or instance identifiers. Machines and instances are named by role only. All counts are aggregate numbers.

## Objective

Prevent false renderer analysis refusals when avatar animations contain curves that target missing components, mismatched renderer types, or out-of-range material slots. Ensure AMUSE ignores inert animation curves safely and preserves visual and animation correctness.

## Architectural context

AMUSE captures animation evidence in `UnityAnimationEvidenceCapture` during the `PlatformFinish` phase. The captured evidence informs runtime material state resolution, classification, and separation preparation.

In the current implementation, AMUSE filters animation curves by transform path only (`AddressesAnalyzedRenderer`). AMUSE does not check whether the target component type in `EditorCurveBinding.type` matches the analyzed renderer.

When an avatar uses animation clips created for multiple avatar bases:
1. The clips contain material swap curves for component types that do not exist on the active avatar (for example, `MeshRenderer` curves on a `SkinnedMeshRenderer` GameObject).
2. The clips contain out-of-range material slot curves (for example, curves for slot indices 2 through 4 on a mesh with two slots).
3. The inert curves often carry unassigned material references (`null` keyframe values).

AMUSE currently evaluates these inert curves as active material swaps on the analyzed renderer. This causes `MaterialDependencyClosureFailure.InvalidSwapValue` or `MaterialDependencyClosureFailure.SlotOutOfRange`. The entire renderer refuses optimization with `MaterialDependencyClosureFailed`.

## Core requirements

1. **Component type compatibility filter:**
   AMUSE must verify the component type of animation bindings during evidence capture. A binding targeting `rendererPath` is admitted only if its component type is compatible with the analyzed renderer.
2. **Inert curve discarding:**
   Curves targeting component types not present on the analyzed renderer's GameObject must be discarded. They must not cause closure failures, slot refusals, or structural refusals.
3. **Safe out-of-range slot tolerance:**
   AMUSE must enable out-of-range slot tolerance by default during evidence capture. The existing collision guard in `AlphaSeparationApply` ensures that AMUSE never appends a split slot at an index targeted by an ignored curve.
4. **Consistency across capture and apply:**
   The component type filter must apply identically in `UnityAnimationEvidenceCapture` (evidence capture), `UnityRendererAlphaAnalysis` (structural checks), and `AlphaSeparationApply` (live revalidation and curve rewriting).
5. **No live Unity object leakage:**
   Captured evidence records (`CapturedAnimationEvidence`, `PreparedRendererSeparation`) must retain only immutable component type strings. They must never hold references to live `UnityEngine.Object` instances.

## Detailed design

### 1. Component type compatibility check

Introduce a helper method `IsCompatibleRendererType` in `UnityAnimationEvidenceCapture`:

```csharp
internal static bool IsCompatibleRendererType(
    string bindingTypeName,
    string rendererTypeName)
{
    if (string.IsNullOrEmpty(rendererTypeName) ||
        string.IsNullOrEmpty(bindingTypeName))
    {
        return true;
    }

    if (string.Equals(bindingTypeName, rendererTypeName, StringComparison.Ordinal))
    {
        return true;
    }

    if (string.Equals(bindingTypeName, typeof(Renderer).FullName, StringComparison.Ordinal) ||
        string.Equals(bindingTypeName, nameof(Renderer), StringComparison.Ordinal))
    {
        return true;
    }

    var bindingShort = ShortName(bindingTypeName);
    var rendererShort = ShortName(rendererTypeName);
    return string.Equals(bindingShort, rendererShort, StringComparison.Ordinal) ||
           string.Equals(bindingShort, nameof(Renderer), StringComparison.Ordinal);
}

private static string ShortName(string typeName)
{
    var lastDot = typeName.LastIndexOf('.');
    return lastDot >= 0 && lastDot + 1 < typeName.Length
        ? typeName.Substring(lastDot + 1)
        : typeName;
}
```

This helper accepts:
- Exact type name matches (such as `UnityEngine.SkinnedMeshRenderer` matching `UnityEngine.SkinnedMeshRenderer`).
- Base `Renderer` bindings (such as `UnityEngine.Renderer` or `Renderer`).
- Short name matches (supporting test fixtures using `nameof(SkinnedMeshRenderer)`).

This helper rejects:
- Mismatched renderer types (such as `UnityEngine.MeshRenderer` when the renderer is `UnityEngine.SkinnedMeshRenderer`).
- Non-renderer component types (such as `UnityEngine.Transform`, `UnityEngine.Cloth`, `UnityEngine.MeshFilter`, or custom scripts).

### 2. Evidence capture updates

Update `AddressesAnalyzedRenderer` in `UnityAnimationEvidenceCapture`:

```csharp
private static bool AddressesAnalyzedRenderer(
    string bindingPath,
    string bindingTypeName,
    string rendererPath,
    string rendererTypeName)
{
    return string.Equals(bindingPath, rendererPath, StringComparison.Ordinal) &&
           IsCompatibleRendererType(bindingTypeName, rendererTypeName);
}
```

Update `Capture`, `CaptureWithAnimationIndex`, `CaptureGraph`, `CaptureObserved`, and `CaptureGraphForTests`:
- Add `string rendererTypeName = null` parameter.
- Default `ignoreOutOfRangeSlots` to `true` in `AmusePlatformFinishPlugin` and default parameter signatures.
- Pass `rendererTypeName` into `AddressesAnalyzedRenderer`.

In `CaptureObserved`:
1. When iterating `observation.Objects`:
   Filter using `AddressesAnalyzedRenderer(binding.Path, binding.TypeName, rendererPath, rendererTypeName)`.
   Bindings for incompatible component types are skipped immediately. They do not enter the `currentSlots` range check or the `TryAdmit` loop.
2. In `observation.Floats`:
   Filter float bindings so that only bindings compatible with the renderer type are captured for the renderer.

### 3. Proof-relevant float property resolution

Update `UnityAnimationEvidenceCapture.ResolveProofRelevant`:
- Accept `string rendererTypeName = null`.
- Return `ProofRelevantBindingResolution.Irrelevant` when `!AddressesAnalyzedRenderer(binding.Path, binding.TypeName, rendererPath, rendererTypeName)`.
- This ensures float curves on missing components do not cause `AnimatedPropertyAbsentFromAdmittedMaterial` or singleton refusals.

### 4. Structural property resolution

Update `UnityRendererAlphaAnalysis.StructuralRefusalFor`:
- Accept `string rendererTypeName = null`.
- In `NamesStructuralProperty`, verify `AddressesAnalyzedRenderer(binding.Path, binding.TypeName, rendererPath, rendererTypeName)`.
- This ensures an `m_Mesh` curve on a missing `MeshFilter` does not refuse a `SkinnedMeshRenderer` with `AnimatedMeshReplacement`.

### 5. Separation preparation and apply pass consistency

In `AlphaSeparationRecords.cs`:
- Store the renderer component type name string property in `PreparedRendererSeparation`.
- Pass `rendererTypeName` from preparation into `PreparedRendererSeparation`.

In `AlphaSeparationApply.cs`:
- When discovering `targetBindings` from `targetClips`:
  ```csharp
  if (!LiveAnimationObservation.TryParseMaterialSlotBinding(
          binding.propertyName, out var slotIndex) ||
      !AddressesAnalyzedRenderer(
          binding.path,
          binding.type.FullName,
          prepared.RendererPath,
          prepared.RendererTypeName))
  {
      continue;
  }
  ```
- This ensures `AlphaSeparationApply` only rewrites curves that actually bound to the renderer. It never attempts to rewrite inert curves targeting missing components.

### 6. Default out-of-range slot tolerance

In `AmusePlatformFinishPlugin.Execute`:
- Pass `ignoreOutOfRangeSlots: true` into `UnityAnimationEvidenceCapture.CaptureWithAnimationIndex`.
- Retain the collision check in `AlphaSeparationApply.ValidateCandidateSlot`:
  If an appended split slot index matches an ignored out-of-range slot index, refuse the split with `SlotBindingAbsentFromEvidence`.
- This provides automatic tolerance for multi-avatar animation clips with complete collision safety.

## Verification plan

### Unit and characterization tests

1. **Incompatible component material swap curve is ignored:**
   An animation clip has a material swap on `path = "Body"`, `type = typeof(MeshRenderer)` with `value = null`.
   The analyzed renderer has `path = "Body"`, `type = typeof(SkinnedMeshRenderer)`.
   Verify:
   - Capture succeeds with `ClosureFailure == MaterialDependencyClosureFailure.None`.
   - `MaterialDependencyClosureFailed` is zero.
   - The renderer is analyzed successfully.
2. **Compatible component material swap curve is admitted:**
   An animation clip has a material swap on `path = "Body"`, `type = typeof(SkinnedMeshRenderer)`.
   Verify:
   - The swapped material is admitted into the slot evidence.
3. **Out-of-range slots ignored by default:**
   An animation clip targets slot index 3 on a renderer with 1 material slot.
   Verify:
   - Capture succeeds with `ClosureFailure == MaterialDependencyClosureFailure.None`.
   - The ignored slot is recorded in `IgnoredOutOfRangeSlots`.
4. **Appended slot collision safety:**
   An animation clip targets slot 1 on a 1-slot renderer.
   AMUSE attempts to split slot 0 and append at slot 1.
   Verify:
   - `ValidateCandidateSlot` returns `AlphaSeparationSlotRefusal.SlotBindingAbsentFromEvidence`.
   - The slot does not split.
5. **Float curve on incompatible component is ignored:**
   An animation clip has a float curve for `material._Cutoff` on `type = typeof(MeshRenderer)` where the material does not declare `_Cutoff`.
   The analyzed renderer is `SkinnedMeshRenderer`.
   Verify:
   - `ResolveProofRelevant` returns `Irrelevant`.
   - No slot refusal occurs.

### Integration and regression validation

- Run all existing tests in `Alrauna.Amuse.Tests.Editor` to confirm no regressions in existing static or animated renderer tests.
- Re-run characterization on the Census Lab editor instance to verify that the character body renderer optimizes without `MaterialDependencyClosureFailed`.
