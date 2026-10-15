# Investigation: Alpha Proof Behavior on Meshes Toggled Off in Default State

Date: 2026-10-15

## Privacy Note

This record characterizes private assets in the Census Lab editor instance.
This document sanitizes all avatar, renderer, material, texture, animation, and hierarchy names.
It describes assets by role.
It expresses exact counts as ranges.

---

## 1. Executive Summary

On 2026-10-15, an investigation examined why AMUSE did not optimize certain meshes on a complex test avatar in the Census Lab editor instance.
These meshes have their renderer component toggled off in the default state and toggled on by animation clips.

In the NDMF Error Report window, the report window displayed `amuse.slotSeparation.OpaqueCoverageBelowMinimum` for material slots on these renderers.
The investigation established the following verified technical facts:

1. AMUSE does not read `renderer.enabled`, `Component.enabled`, `GameObject.activeSelf`, or `GameObject.activeInHierarchy`.
AMUSE enumerates all renderers with `GetComponentsInChildren<Renderer>(true)`.
AMUSE treats disabled renderers and enabled renderers in the same way.

2. AMUSE classifies animation curves that animate `m_Enabled` or `m_IsActive` as irrelevant.
These curves never cause refusals.
They do not invalidate alpha proof.

3. The refusal reason on these meshes is `OpaqueCoverageBelowMinimum`.
The default material alone achieves approximately 27 percent proven opaque coverage.
However, the avatar contains animation clips that swap four alternate color materials into the same material slot.
Under AMUSE conservative proof invariants, a triangle is proven opaque only if it is proven opaque in every reachable animation state.
The mathematical intersection of proven opaque triangles across all five reachable material states is approximately 17.5 percent.

4. The configured minimum opaque coverage threshold on the avatar is 25 percent.
Because 17.5 percent is less than 25 percent, AMUSE safely leaves the slot unchanged.

5. The default enabled mesh with the same materials also received the identical `OpaqueCoverageBelowMinimum` refusal.
The disabled state of the renderer component is a correlation and not the cause.

---

## 2. Evidence from the Census Lab Editor Instance

### The Observed Reports

In the Census Lab editor instance, an avatar build completed with the following summary:
- Between 35 and 45 renderers were analyzed.
- Between 55,000 and 65,000 triangles moved to opaque materials.
- Between 120 and 130 renderers kept their original materials.

In the NDMF Error Report window, the following reports appeared:
- The default main outfit pieces with transparent shaders were successfully optimized.
Several hundred unproven triangles moved to canonical opaque materials under the preserve threshold.
These renderers had their renderer component enabled in the default state.

- Five secondary outfit pieces (representing separated garments) were left unchanged.
For each of these pieces, AMUSE reported that the proven opaque share sat below the minimum opaque coverage setting.
The reported refusal reason was `OpaqueCoverageBelowMinimum`.
These five renderers had `renderer.enabled == false` in the default state.
They were animated to enabled by clothing toggle clips.

- The primary combined outfit piece also had its cutout flower slot left unchanged.
It reported the exact same `OpaqueCoverageBelowMinimum` refusal.
This renderer had `renderer.enabled == true` in the default state.

---

## 3. Codebase Analysis of Renderer Enable States

Three parallel audits investigated the AMUSE codebase on 2026-10-15.

### Audit 1: Renderer Pipeline Audit
The audit searched all production code in `Packages/com.alrauna.amuse/Editor/` for references to enabled or active flags.

Findings:
- Total references to `renderer.enabled`: 0.
- Total references to `Component.enabled`: 0.
- Total references to `GameObject.activeSelf`: 0.
- Total references to `GameObject.activeInHierarchy`: 0.

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` line 720:
```csharp
foreach (var renderer in context.AvatarRootObject
             .GetComponentsInChildren<Renderer>(true))
```
The boolean argument is `true`.
This argument forces Unity to return disabled renderers and renderers on inactive GameObjects.
Both disabled and enabled renderers enter the proof pipeline.

### Audit 2: Animation Curves on `m_Enabled`
The audit inspected how AMUSE processes animation curves that animate `m_Enabled` on a Renderer or `m_IsActive` on a GameObject.

Findings:
- In `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs` line 897, `ResolveProofRelevant` parses animated properties.
- Any property that does not address material properties is classified as `ProofRelevantBindingResolution.Irrelevant`.
- An animation curve targeting `m_Enabled` is marked irrelevant.
It is ignored during material evidence capture.
It never produces an unsupported animation refusal.

### Audit 3: Apply Phase and Curve Rewriting
The audit examined how `AlphaSeparationApply` treats float curves when a renderer is split.

Findings:
- In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` line 342, AMUSE queries live clips with `clip.GetObjectCurveBindings()`.
Float curves (`isPPtrCurve == false`) are never inspected or rewritten.
- When AMUSE splits a renderer, it appends a submesh to the existing `Mesh` and appends a material to `Renderer.sharedMaterials`.
The `Renderer` component itself does not change.
- Existing curves that animate `m_Enabled` on that renderer continue to toggle the whole renderer.
They toggle both the original submeshes and the appended opaque submesh together.

---

## 4. Root Cause of the Observed Refusal

### The Slot Structure
The five toggled-off renderers and the one default-enabled renderer share the same material configuration:
1. Slot 0 holds an opaque material with render queue 2000.
Because it is already opaque, AMUSE does not need to separate it.
2. Slot 1 holds a cutout outline material with render queue 2450.
This slot represents an inverted hull outline.
The mesh UV coordinates map to transparent texture texels.
All triangles are classified as `MustRemainTransparent`.
3. Slot 2 holds a cutout flower material with render queue 2450.
The texture has a transparent background with opaque petal regions.

### The Material Swap Set
The avatar animator carries four palette swap animation clips in an FX layer.
These clips animate `m_Materials.Array.data[2]` on these renderers.
They swap in four additional cutout materials.

In `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs` lines 1001 to 1016, `IntersectOutcomes` computes the triangle proof across all admitted material states:
```csharp
var allOpaque = true;
var allTransparent = true;
for (var state = 0; state < perResolutionOutcomes.Count; state++)
{
    var outcome = perResolutionOutcomes[state][triangle];
    allOpaque &= outcome == TriangleAlphaOutcome.ProvenOpaque;
    allTransparent &=
        outcome == TriangleAlphaOutcome.MustRemainTransparent;
}

intersected[triangle] = allOpaque
    ? TriangleAlphaOutcome.ProvenOpaque
    : allTransparent
        ? TriangleAlphaOutcome.MustRemainTransparent
        : TriangleAlphaOutcome.Unknown;
```

A triangle remains `ProvenOpaque` only if every admitted material state classifies it as `ProvenOpaque`.
The individual materials have the following proven opaque percentages on the secondary garment mesh (between 20,000 and 25,000 triangles in slot 2):
- Default material alone: 27.15 percent (between 6,000 and 6,500 triangles).
- Variant A: 18.54 percent.
- Variant B: 17.88 percent.
- Variant C: 17.88 percent.
- Variant D: 21.85 percent.

Because each variant texture has minor differences along petal contours and compression artifacts, the intersection across all five materials shrinks to 17.55 percent (between 4,000 and 4,500 triangles).

### The Coverage Threshold Gate
In `Packages/com.alrauna.amuse/Editor/Analysis/SlotTransformDecision.cs` line 155:
```csharp
internal static bool OpaqueCoverageBelow(
    int minimumOpaqueCoveragePercent,
    SubmeshSeparationPlan submesh)
{
    var totalTriangles =
        submesh.OpaqueTriangleOrdinals.Count +
        submesh.TransparentTriangleOrdinals.Count;
    return submesh.OpaqueTriangleOrdinals.Count * 100 <
           minimumOpaqueCoveragePercent * totalTriangles;
}
```

The avatar optimizer component on the avatar root sets `Minimum Opaque Coverage Percentage` to 25.
The calculation evaluates:
$17.55\% < 25\%$ (true).

Because the intersection falls below 25 percent, `AlphaSeparationPreparation.Prepare` refuses the candidate split.
AMUSE records `AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum` and leaves the slot unchanged.

---

## 5. Summary of Findings

1. AMUSE processes disabled renderers correctly and without errors.
2. Toggling `m_Enabled` on a renderer component does not hinder AMUSE from proving triangles.
3. The observed refusal occurred because multiple animation-reachable palette swaps reduced the proven opaque intersection from 27.15 percent to 17.55 percent.
4. The 17.55 percent intersection is strictly below the configured 25 percent threshold.
5. If the user sets `Minimum Opaque Coverage Percentage` to 15 percent, the 17.55 percent intersection satisfies the threshold.
All 4,000 to 4,500 opaque triangles then split successfully into canonical opaque materials.
