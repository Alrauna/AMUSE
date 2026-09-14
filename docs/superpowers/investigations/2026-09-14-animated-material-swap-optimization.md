# Investigation: Alpha material optimization for animated material swaps

Date: 2026-09-14. Status: completed investigation.

## Privacy note

This document contains observations from synthetic fixtures and characterization testing. It contains no private avatar, renderer, material, animation clip, or controller names. It contains no machine paths, network ports, or instance identifiers. Machines and instances are named by role only. All counts are aggregate numbers.

## Objective

Determine why renderers with animated material swaps do not achieve the same alpha optimization as renderers with static material assignments. Identify the architectural and semantic gaps that prevent triangles that are opaque across all swap states from being separated into canonical opaque materials.

## Background

AMUSE optimizes static materials by classifying mesh triangles into three categories:
1. Proven opaque
2. Must remain transparent
3. Unknown

Triangles proven visually opaque can move to an appended submesh with a canonical opaque material. Triangles not proven opaque stay on their original material slot.

For renderers with static material assignments, AMUSE analyzes one material per slot. If that material proves triangles opaque and has an opaque conversion recipe, AMUSE separates the mesh.

For renderers with runtime material swaps, Unity animation clips animate `m_Materials.Array.data[slotIndex]`. The renderer swaps whole materials during runtime. Triangles that are opaque in every swapped material can split onto an appended opaque slot. In practice, characterization shows that renderers with animated material swaps frequently fail to optimize.

## Identified gaps

### Gap 1: Renderer-wide float curve coupling across swapped materials

In Unity, material float and color curves bind to the renderer, not to a specific material slot. The binding path is `material._PropertyName`.

AMUSE captures all float curves on a renderer into a single renderer-wide list. During runtime state resolution in `AdmittedMaterialStates.ResolveSlot`, AMUSE evaluates this entire list against every admitted material in the slot.

This evaluation creates two failure modes on material swaps:

1. Missing property declaration:
   If clip 1 animates `material._Cutoff` for material A, but material B does not declare `_Cutoff`, `evidence.TryGetScalar` fails on material B. AMUSE refuses the entire slot with `AnimatedPropertyAbsentFromAdmittedMaterial`.
2. Conflicting default values:
   Material A and material B can both declare `_AlphaForceOpaque`. If material A has default 0 while material B has default 1, `AdmitScalar` detects a non-singleton domain. AMUSE refuses the entire slot with `AnimatedMaterialPropertyNotSingleton`.

Unity authors often pair a material swap with a property animation in the same clip. AMUSE currently has no clip-level association. AMUSE assumes every float curve applies to all swapped materials simultaneously.

### Gap 2: All-or-nothing opaque material conversion requirement
To optimize a slot, AMUSE must provide an opaque replacement material for the appended submesh.

In `AlphaSeparationPreparation.Prepare`, AMUSE iterates through every admitted material in the slot. AMUSE calls `ConvertAdmittedMaterial` for each material.

If any material in the swap fails conversion, AMUSE rejects the entire slot:
1. If material A converts to an opaque material, but material B belongs to an unsupported shader family, `ConvertAdmittedMaterial` returns `OpaqueConversionUnsupportedFamily`.
2. If material B has an animated property that violates canonical conversion rules, `ConvertAdmittedMaterial` returns `ConversionStateNotAdmitted`.

When one material fails conversion, AMUSE records a slot refusal, destroys all candidate clones, and drops the slot completely. The slot cannot split, even when triangles are proven opaque under all swap states.

### Gap 3: All-or-nothing shader attestation requirement

In `AdmittedMaterialStates.ResolveSlot`, AMUSE resolves alpha semantics for every admitted material in the slot.

If any material in the swap is unattested, `resolveSemantics` returns unknown semantics. AMUSE immediately halts slot resolution and returns `RendererAnalysisRefusal.AdmittedMaterialSemanticsUnknown`.

For example, an avatar can swap between a supported lilToon material and an un-attested custom material. Because the custom material is unattested, AMUSE rejects the entire slot immediately. AMUSE does not evaluate whether shared triangles are opaque.

### Gap 4: Multi-state geometric intersection constraints

For static materials, a triangle must be opaque under only one texture coordinate domain.

Material A can be opaque over a triangle while material B has transparency there. In that condition, the intersection verdict becomes transparent. The number of candidate triangles shrinks to the intersection of all opaque regions.

### Gap 5: Strict object-reference curve validation

An animation clip can swap to a material not present during capture. Curves can also contain null keyframes. In both cases, the apply pass refuses with `RuntimeMaterialValueNotMapped`.

`AlphaSeparationApply` requires that every keyframe value in every target swap curve exists as a key in `slot.OpaqueOfAdmitted`.
## Root cause summary

To achieve static-level optimization, AMUSE must isolate failures to individual states:
1. Float curve admission refuses if swapped materials have differing properties.
2. Semantic resolution refuses if any swapped material is unattested.
3. Preparation drops the slot if any swapped material cannot convert to an opaque recipe.
