# Investigation: Safely ignoring missing animation keys and components during renderer proofing

Date: 2026-09-16. Status: completed investigation.

## Privacy note

This document contains observations from synthetic fixtures and characterization testing. It contains no private avatar, renderer, material, animation clip, or controller names. It contains no machine paths, network ports, or instance identifiers. Machines and instances are named by role only. All counts are aggregate numbers.

## Objective

Determine why animations with missing targets cause renderer analysis refusals in AMUSE. Evaluate whether AMUSE can safely ignore missing animation keys, missing components, and missing transforms when proving animated renderers. Define the safety rules and architectural changes needed to allow valid renderers to optimize.

## Background

AMUSE analyzes avatar renderers during the NDMF `PlatformFinish` phase. When a renderer has animations, AMUSE captures closed animation evidence in `UnityAnimationEvidenceCapture`.

The capture process examines two data sources:
1. Every layer and clip in the committed animator controller graph.
2. Every virtual clip that the NDMF `AnimationIndex` associates with the renderer object path.

The capture process must achieve closure. If closure fails, `evidence.IsClosed` is `false`. In `AmusePlatformFinishPlugin.ResolveRuntimeStates`, an unclosed capture causes an immediate renderer refusal:

```csharp
if (!evidence.IsClosed)
{
    return Refused(
        RendererAnalysisRefusal.MaterialDependencyClosureFailed);
}
```

The NDMF console reports this refusal to the user:
"AMUSE could not read this renderer's animations. An animation on this avatar swaps a material on this renderer, and AMUSE cannot prove what that animation shows. The renderer keeps its original materials."

When this refusal occurs, AMUSE halts analysis for the renderer. The renderer keeps all original materials. No triangles move to canonical opaque materials.

## Observed problem

Testing on the Census Lab editor instance identified an avatar renderer refusal. The avatar uses an animation clip designed for use across several different avatar models.

When users inspect this animation clip in the Unity animation window, many curves show yellow warnings labeled "Missing!". The curves target components or child transforms that do not exist on this specific avatar.

AMUSE failed closure on the character body skinned mesh renderer with `MaterialDependencyClosureFailed`.

Investigation of the AMUSE codebase revealed the root cause. AMUSE filters object bindings in `UnityAnimationEvidenceCapture.CaptureObserved` with a single check:

```csharp
if (!LiveAnimationObservation.TryParseMaterialSlotBinding(
        binding.PropertyName, out var slot) ||
    !AddressesAnalyzedRenderer(binding.Path, rendererPath))
{
    continue;
}
```

The method `AddressesAnalyzedRenderer` performs only an ordinal string comparison on the path:

```csharp
private static bool AddressesAnalyzedRenderer(
    string bindingPath,
    string rendererPath)
{
    return string.Equals(
        bindingPath, rendererPath, StringComparison.Ordinal);
}
```

AMUSE does not check `binding.TypeName`. AMUSE does not check whether the target component exists on the target GameObject.

This logic causes three failure modes when an animation contains keys for other avatar setups.

### Failure mode 1: Missing component type on the GameObject

An animation clip can contain curves for different component types at the same transform path. For example, a universal clothing toggle can animate:
- `path = "Body"`, `type = typeof(MeshRenderer)`, `propertyName = "m_Materials.Array.data[0]"`
- `path = "Body"`, `type = typeof(SkinnedMeshRenderer)`, `propertyName = "m_Materials.Array.data[0]"`

On an avatar where the body is a `SkinnedMeshRenderer`, no `MeshRenderer` component exists.

At runtime, Unity's native animation engine tries to bind the curve to a component on the GameObject. The method `gameObject.GetComponent(binding.type)` returns `null`. Unity cannot bind the curve. The curve is completely inert. The curve never modifies the `SkinnedMeshRenderer` at runtime.

However, AMUSE inspects both curves. AMUSE matches the path `"Body"`. AMUSE treats the inert `MeshRenderer` curve as an active material swap on the `SkinnedMeshRenderer`.

If the inert curve has an unassigned material asset, `binding.Values` contains `null`. AMUSE executes this check:

```csharp
foreach (var value in binding.Values)
{
    if (!(value is Material material) ||
        !TryAdmit(material, out _))
    {
        return Failed(
            MaterialDependencyClosureFailure.InvalidSwapValue);
    }
}
```

The `null` value causes `MaterialDependencyClosureFailure.InvalidSwapValue`. Closure fails. AMUSE refuses the entire renderer.

### Failure mode 2: Out-of-range material slots from multi-avatar curves

Different avatar bases have different material slot counts. A universal animation often animates material slots 0 through 4 to support models with up to five slots.

On an avatar with only two material slots, curves for slots 2, 3, and 4 target non-existent slots.

Unity's runtime animation engine does not resize material arrays when an animation addresses an out-of-range index. The assignment is an inert no-op in Unity.

In AMUSE, `UnityAnimationEvidenceCapture` checks slot indices against `currentSlots.Count`:

```csharp
if (slot >= currentSlots.Count)
{
    if (ignoreOutOfRangeSlots)
    {
        ignoredOutOfRangeSlots.Add(slot);
        continue;
    }

    return Failed(
        MaterialDependencyClosureFailure.SlotOutOfRange);
}
```

AMUSE supports the `ignoreOutOfRangeSlots` parameter. However, this parameter is disabled by default in `AmuseAvatarOptimizer`. When the option is disabled, any out-of-range slot curve causes `SlotOutOfRange`. Closure fails. AMUSE refuses the entire renderer.

### Failure mode 3: Float curve coupling on missing components

In `UnityAnimationEvidenceCapture.ResolveProofRelevant`, AMUSE resolves float curves:

```csharp
if (!string.Equals(
        binding.Path, rendererPath, StringComparison.Ordinal))
{
    return ProofRelevantBindingResolution.Irrelevant;
}
```

AMUSE checks only the path string. If an animation contains float curves for `material._Cutoff` on a missing `MeshRenderer` or a missing script on the same GameObject, AMUSE treats the property as a renderer-wide float binding on the analyzed renderer.

If the active materials do not declare that property, AMUSE refuses the slot with `AnimatedPropertyAbsentFromAdmittedMaterial`.

## Analysis of animation binding targets

Unity defines an animation curve with three fields in `EditorCurveBinding`:
1. `path`: the relative hierarchy path from the animated root GameObject.
2. `type`: the `System.Type` of the target component.
3. `propertyName`: the serialized property path on the component.

The table below classifies how Unity and AMUSE handle different categories of missing targets:

| Category | Unity runtime behavior | Current AMUSE behavior | Can AMUSE safely ignore? |
|---|---|---|---|
| Missing hierarchy path | Unity drops the curve. The curve is inert. | Ignored if path does not equal `rendererPath`. | Yes. The curve cannot affect any object on the avatar. |
| Missing component type on GameObject | Unity cannot find the component. The curve is inert. | Evaluated as active curve on the renderer. Causes closure failure. | Yes. The curve cannot bind to the renderer at runtime. |
| Non-renderer component on same GameObject | Unity binds to the other component (for example, `Transform`). | Can trigger false unrecognized property refusals. | Yes. The curve does not target the renderer. |
| Material slot index out of range | Unity ignores the assignment. The array does not resize. | Fails closed with `SlotOutOfRange` unless an advanced toggle is active. | Yes, provided no appended slot receives the ignored index. |
| Null material keyframe on missing component | Unity cannot bind the curve. The keyframe never applies. | Fails closed with `InvalidSwapValue`. Refuses the entire renderer. | Yes. The curve is inert. |
| Null material keyframe on active renderer slot | Unity assigns `null`. The submesh renders with error color. | Fails closed with `InvalidSwapValue`. Refuses the entire renderer. | No for that slot. Yes for unrelated sibling slots on the renderer. |

## Safety evaluation

### Safety rule 1: Component type verification

At runtime, Unity binds an animation curve to a component using the equivalent of `targetGameObject.GetComponent(binding.type)`.

A curve cannot affect a renderer if:
1. `binding.type` does not match the renderer component type.
2. `binding.type` is not assignable to `UnityEngine.Renderer`.
3. `renderer.gameObject.GetComponent(binding.type)` returns `null`.

Therefore, AMUSE can safely ignore any curve whose target component does not exist on the analyzed renderer's GameObject.

Ignoring these curves introduces zero visual divergence. Unity's runtime animation engine never applies these curves.

### Safety rule 2: Out-of-range slot protection

When AMUSE separates opaque triangles, AMUSE appends new material slots to the renderer.

If AMUSE ignores an out-of-range slot binding at index 2, and AMUSE appends a new opaque slot at index 2, a collision occurs. The previously inert curve would now bind to the newly created slot.

AMUSE already protects against this hazard in `AlphaSeparationApply.ValidateCandidateSlot`:

```csharp
var appendedIndex = live.Length + currentSplitCount;
if (prepared.Evidence.IgnoredOutOfRangeSlots.Contains(appendedIndex) ||
    targetBindings.Any(target => target.SlotIndex == appendedIndex))
{
    return AlphaSeparationSlotRefusal
        .SlotBindingAbsentFromEvidence;
}
```

Because this guard already exists, ignoring out-of-range slot bindings during capture is completely safe. The apply pass prevents any slot split that would collide with an ignored slot index.

### Safety rule 3: Per-slot localization for invalid material values

When an animation contains an authentic `null` keyframe targeting an existing slot on an active renderer, that slot cannot separate. AMUSE has no canonical opaque replacement for a `null` material.

However, an invalid keyframe on slot 1 does not alter the geometry or materials of slot 0.

Failing the entire renderer when one slot has an invalid swap value is unnecessary. Localizing the failure to the specific slot allows other valid slots on the renderer to optimize.

## Technical options

### Option A: Component type filtering during evidence capture

Pass the target `Renderer` component into `UnityAnimationEvidenceCapture`. During capture, verify that each binding targeting `rendererPath` matches the renderer:

```csharp
private static bool AddressesAnalyzedRenderer(
    string bindingPath,
    string bindingTypeName,
    Renderer renderer,
    string rendererPath)
{
    if (!string.Equals(bindingPath, rendererPath, StringComparison.Ordinal))
    {
        return false;
    }

    if (string.IsNullOrEmpty(bindingTypeName))
    {
        return true;
    }

    var component = renderer.GetComponent(bindingTypeName);
    return component == renderer;
}
```

Advantages:
- Completely eliminates false closure failures caused by curves on missing components.
- Requires no changes to downstream analysis or separation planning.
- Directly aligns AMUSE capture with Unity runtime binding mechanics.

Tradeoffs:
- Requires access to the live `Renderer` component or its verified component type name during capture.

### Option B: Safe out-of-range slot tolerance by default

Change `ignoreOutOfRangeSlots` to default to `true` during evidence capture, rather than requiring an opt-in toggle on `AmuseAvatarOptimizer`.

Advantages:
- Universal multi-avatar animation clips immediately succeed on avatars with fewer material slots.
- The existing collision check in `AlphaSeparationApply` guarantees safety against index collisions.

Tradeoffs:
- Authors who unintentionally animate non-existent slots will not receive a renderer refusal warning. AMUSE can report an informational notice instead.

### Option C: Per-slot attribution for invalid swap values

Change `MaterialDependencyClosureFailure.InvalidSwapValue` from a renderer-wide failure to a per-slot refusal:

```csharp
internal sealed class CapturedMaterialSlotEvidence
{
    internal bool HasInvalidSwapValue { get; }
}
```

When a slot has an invalid swap value, mark that slot as unprovable. Allow sibling slots to continue to classification and separation.

Advantages:
- A broken animation on one accessory slot does not prevent the character body or face from optimizing.
- Preserves the fail-closed invariant on the affected slot.

Tradeoffs:
- Requires minor updates to `AdmittedMaterialStates.ResolveSlot` and slot reporting.

## Recommendations

1. Implement Option A first. Verify the component type of animation bindings during evidence capture. Ignore curves whose component type does not exist on the analyzed renderer.
2. Implement Option B. Enable out-of-range slot tolerance by default because `AlphaSeparationApply` already guards against appended slot collisions.
3. Consider Option C as a future improvement to maximize optimization yield on avatars with partially broken animations.

## Next steps

1. Author a formal design specification: `docs/superpowers/specs/YYYY-MM-DD-ignore-missing-animation-bindings-design.md`.
2. Create synthetic test fixtures with cross-avatar animation clips that contain missing components, missing transforms, and out-of-range slots.
3. Write failing RED tests verifying that mismatched component curves are safely ignored and do not trigger `MaterialDependencyClosureFailed`.
4. Implement the component type filter in `UnityAnimationEvidenceCapture`.
5. Verify on the Census Lab editor instance that the character body skinned mesh renderer optimizes successfully.
