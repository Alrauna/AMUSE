# Design: Animated material swap alpha optimization

Date: 2026-09-14. Status: design for review. Implementation has not started.

## Privacy note

This document contains design specifications derived from synthetic fixtures and characterization testing. It contains no private avatar, renderer, material, animation clip, or controller names. It contains no machine paths, network ports, or instance identifiers. Machines and instances are named by role only. All counts are aggregate numbers.

## Goal

Enable mesh separation and alpha optimization for mesh renderers that swap materials during runtime through the Unity animation system. Allow polygons that are visually opaque across all animation states to move to canonical opaque materials, matching the optimization quality of static material assignments.

## Non-goals

This design does not add support for un-attested shaders. This design does not change the exact rational-interval math in `TriangleAlphaClassifier`. This design does not alter the canonical opaque recipes for Poiyomi or lilToon.

## Architecture and semantic model

### 1. Per-material property scoping for animated float curves

In Unity, material float curves bind to the renderer under `material._PropertyName`. Unity applies these curves to whichever material currently occupies the renderer slots. If an active material does not declare `_PropertyName`, Unity ignores the property during rendering.

Under the new design:
1. When resolving a slot in `AdmittedMaterialStates.ResolveSlot`, AMUSE evaluates animated float curves against each admitted material individually.
2. If an admitted material does not declare `_PropertyName`, the curve cannot affect the appearance of that material. AMUSE treats the property as absent and inert for that material, rather than returning `AnimatedPropertyAbsentFromAdmittedMaterial`.
3. If an admitted material declares `_PropertyName`, AMUSE enforces the existing singleton domain rule:
   `Domain(P) = Serialized(P) union Block(P) union Curves(P)`.
   If this domain has multiple values, AMUSE refuses that specific material state.

This change isolates property declarations. A material swap between two different shaders with different property schemas can now resolve successfully.

### 2. Resilient conversion mapping with identity fallback

Today, `AlphaSeparationPreparation` requires every admitted material in a splitting slot to convert to a canonical opaque replacement. If one material cannot convert, AMUSE drops the entire slot.

Under the new design:
1. For each admitted material $M$ in a splitting slot:
   - If $M$ can convert to a canonical opaque material $M_{\text{opaque}}$, AMUSE maps $M \to M_{\text{opaque}}$.
   - If $M$ is already opaque, AMUSE maps $M \to M$ using the existing identity mapping rule.
   - If $M$ cannot convert to a canonical opaque material, but all triangles in the split submesh are visually opaque under $M$, AMUSE maps $M \to M$.
2. When the slot splits:
   - The original submesh retains triangles that require transparency in at least one animation state. The original slot continues to swap between the original materials.
   - The appended submesh contains triangles proven visually opaque in all animation states. The appended slot swaps between the mapped materials in `slot.OpaqueOfAdmitted`.
3. When state $M$ is active at runtime:
   - If $M$ mapped to $M_{\text{opaque}}$, the appended slot renders with the canonical opaque shader. This achieves draw call consolidation and fill rate reduction.
   - If $M$ mapped to $M$ (identity fallback), the appended slot renders with $M$. The visual appearance is identical to the un-split mesh.

This eliminates the all-or-nothing conversion gate. A slot can split as long as at least one admitted material converts to a canonical opaque material, and all other states are visually safe.

### 3. State-independent UV sampling verification

`IntersectResolvedOutcomes` continues to enforce strict geometric safety.

For each admitted material state in a slot:
1. AMUSE calculates the exact UV coordinates using that material's texture scale and offset.
2. AMUSE classifies every triangle against that material's alpha evidence.
3. AMUSE intersects the classification arrays:
   A triangle is `ProvenOpaque` only if it evaluates to `ProvenOpaque` in every admitted state.
   If a triangle is transparent or unknown in any state, the intersection verdict is transparent or unknown.

This guarantees visual appearance preservation under all runtime swap conditions.

## Contract changes

1. `RendererAnalysisRefusal.AnimatedPropertyAbsentFromAdmittedMaterial`:
   This refusal no longer fires if another admitted material in the slot declares the property. The refusal fires only if no material on the renderer declares the property.
2. `AlphaSeparationSlotRefusal.OpaqueConversionUnsupportedFamily`:
   This refusal no longer drops a multi-material slot if at least one admitted material converts and the remaining materials map to identity.
3. `AlphaSeparationApply`:
   The apply pass rewrites swap curves for appended slots using `slot.OpaqueOfAdmitted[value]`. Identity mappings write the original material into the appended curve keyframe.

## Falsifiers

1. A slot swaps between Material A (cutout) and Material B (opaque). Material A declares `_Cutoff`. Material B does not declare `_Cutoff`. A curve animates `material._Cutoff = 0.5`. Material A must resolve using `_Cutoff = 0.5`. Material B must resolve without refusal. Shared opaque triangles must split.
2. A slot swaps between Material A (convertible cutout) and Material B (unconvertible transparent). A triangle is opaque under Material A, but transparent under Material B. The intersection must classify the triangle as transparent. The triangle must not move to the opaque submesh.
3. A slot swaps between Material A (convertible cutout) and Material B (unconvertible transparent). A triangle is opaque under both Material A and Material B. Material A converts to $A_{\text{opaque}}$. Material B maps to $B$. The slot must split. The appended curve must swap between $A_{\text{opaque}}$ and $B$.
4. An animation curve sets a swap keyframe to a foreign material not present during capture. The apply pass must refuse with `RuntimeMaterialValueNotMapped`.
