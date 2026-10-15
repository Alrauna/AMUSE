# Investigation: Audit of Skipped and Unanalyzable Renderers in the Census Lab Scene

Date: 2026-10-15

## Privacy Note

This record characterizes private assets in the Census Lab editor instance.
This document sanitizes all avatar, renderer, material, texture, animation, and hierarchy names.
It describes assets by role.
It expresses exact counts as ranges.

---

## 1. Executive Summary

On 2026-10-15, an investigation audited all renderers in the Census Lab scene.
The investigation determined why certain renderers were skipped or unanalyzable during the avatar build.

The scene contains between 145 and 155 total renderers on one test avatar.
The audit categorizes the renderers into five groups:

1. Renderers with supported shaders that AMUSE analyzed and optimized:
Between 35 and 45 renderers belong to this group.
These renderers use supported lilToon opaque or transparent shaders.
AMUSE analyzed their geometry and moved tens of thousands of triangles to canonical opaque materials.

2. Renderers with supported shaders that received coverage refusals:
Between 5 and 10 renderers belong to this group.
These renderers use supported lilToon cutout shaders.
Palette swap animation clips reduced the proven opaque triangle intersection across all states to 17.55 percent.
Because 17.55 percent is less than the 25 percent threshold, AMUSE left them unchanged with `OpaqueCoverageBelowMinimum`.

3. Renderers with supported shader families that use unsupported specialty passes:
A small number (between 1 and 3) of accessory renderers belong to this group.
Each renderer carries one material slot that uses the lilToon gem pass shader (`Hidden/lilToonGem`).
AMUSE does not attest or support the gem pass shader.
AMUSE recorded `AdmittedMaterialSemanticsUnknown` for those slots.

4. Renderers that AMUSE skipped because of their component type:
Between 45 and 55 renderers belong to this group.
These renderers are particle system renderers and trail renderers.
AMUSE only optimizes mesh geometry on `MeshRenderer` and `SkinnedMeshRenderer` components.
AMUSE recorded `UnsupportedRendererType` for every non-mesh renderer.

5. Renderers that AMUSE could not analyze because of unadmitted shaders:
Between 48 and 55 renderers belong to this group.
These renderers include both mesh renderers and skinned mesh renderers.
They represent props, eye lens overlays, watch accessories, and interface elements.
They use shaders that AMUSE does not attest, including internal error shaders, particle shaders, and custom screen effect shaders.
AMUSE marked all triangles as unknown and recorded `AdmittedMaterialSemanticsUnknown`.

---

## 2. Analysis of Renderers with Supported Materials

### Specialty Pass Refusal (`Hidden/lilToonGem`)
AMUSE supports standard lilToon opaque, cutout, and transparent shaders.
AMUSE excludes specialty shader passes such as fur and gems from its attestation registry.
In `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, `ClassifyShaderName` recognizes only canonical lilToon shader names.
When a material uses an unlisted shader such as `Hidden/lilToonGem`, `ClassifyShaderName` returns `CapturedAlphaMaterialFamily.Unsupported`.
The material resolves as unattested, and AMUSE records `AdmittedMaterialSemanticsUnknown`.

In the Census Lab scene, two accessory renderers carry material slots using `Hidden/lilToonGem`:
- A chain accessory renderer has one opaque lilToon slot and one gem slot.
- A pair of glasses has one opaque frame slot, one transparent lens slot, and one gem slot.
AMUSE proved nothing for the gem slots.
AMUSE reported `AdmittedMaterialSemanticsUnknown` for each gem slot.
The renderers kept their original materials.

### Coverage Threshold Refusal on Cutout Materials
Between 5 and 10 renderers carry cutout materials that AMUSE recognizes.
These renderers include secondary clothing items, one primary clothing item, and one hat item.
All of these renderers carry cutout flower petals.
The animator contains four palette swap animation clips that swap four alternate color materials into the same slot.
Across all five reachable animation states, the intersection of proven opaque triangles is 17.55 percent.
The avatar optimizer component specifies a 25 percent minimum opaque coverage threshold.
Because 17.55 percent is less than 25 percent, AMUSE left all of these slots unchanged with `OpaqueCoverageBelowMinimum`.
Lowering the threshold to 15 percent allows all of these slots to separate and optimize.

---

## 3. Analysis of Skipped Renderers

The Census Lab console reported between 45 and 55 instances of `amuse.renderer.UnsupportedRendererType`.
The message states:
`AMUSE only reads Mesh Renderer and Skinned Mesh Renderer components. This renderer keeps its original materials.`

All skipped renderers in this group are particle system renderers and trail renderers.
Examples in the scene include:
- Smoke effects.
- Flame and spark effects.
- Gun muzzle flash and spark effects.
- Portal and marker effects.
AMUSE deliberately ignores these components because they generate dynamic particles rather than static mesh triangles.

---

## 4. Analysis of Unanalyzable Renderers

The Census Lab console reported between 45 and 55 instances of `amuse.renderer.AdmittedMaterialSemanticsUnknown`.
The message states:
`The materials resolved, but every triangle answer came back unknown. The renderer keeps its original materials and nothing was proven wrong.`

All renderers in this group use shaders that AMUSE does not support:
1. Between 10 and 15 gun prop renderers use `Hidden/InternalErrorShader`.
The original shader assets for the weapon model are not installed in the project.
Unity automatically substitutes the internal pink error shader.
2. Between 1 and 3 eye reflection overlay renderers use `Particles/Standard Surface`.
3. One watch renderer uses a custom clock shader.
4. Several user interface and screen effect renderers use mobile particle shaders, screen effect shaders, and custom shadow shaders.

Because none of these shaders exist in the AMUSE verified attestation registry, AMUSE conservatively fails closed.
AMUSE leaves all of these renderers untouched.

---

## 5. Summary

The audit categorizes the skipped and unanalyzable renderers into distinct groups:
1. AMUSE skips between 45 and 55 non-mesh renderers because AMUSE optimizes only mesh geometry.
2. AMUSE leaves between 48 and 55 renderers unanalyzed because their shaders are unadmitted or missing.
3. AMUSE leaves between 1 and 3 renderers unanalyzed on specific slots because they use the unadmitted `Hidden/lilToonGem` pass.
4. AMUSE recognizes between 5 and 10 renderers with lilToon cutout materials, but refuses separation because palette swap animations reduce their opaque share below the 25 percent threshold.
