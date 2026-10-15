# Toggled-Off Renderer Alpha Proof Characterization and Regression Defense Design

Date: 2026-10-15

Base: `main` at `be8ec93`. Work branch: `fix/toggled-off-renderer-alpha-proof`.

Investigation: `docs/superpowers/investigations/2026-10-15-toggled-off-renderer-alpha-proof-investigation.md`

## Privacy Note

This record characterizes private assets in the Census Lab editor instance.
This document sanitizes all avatar, renderer, material, texture, animation, and hierarchy names.
It describes assets by role.
It expresses exact counts as ranges.

---

## 1. Context and Problem Statement

On 2026-10-15, an investigation examined avatar builds in the Census Lab editor instance.
An observer saw that AMUSE left certain secondary clothing pieces unchanged with the refusal reason `OpaqueCoverageBelowMinimum`.
These clothing pieces had their `Renderer.enabled` property toggled off in the default authored state.
Animation clips toggled them on at runtime.

The investigation proved that AMUSE checks none of `renderer.enabled`, `Component.enabled`, `GameObject.activeSelf`, or `GameObject.activeInHierarchy`.
AMUSE enumerates all renderers with `GetComponentsInChildren<Renderer>(true)`.
AMUSE treats disabled renderers and enabled renderers in the same way.

The investigation also identified a primary control renderer with `renderer.enabled == true` in the default state.
This control renderer carried the identical material slot configuration.
It received the exact same `OpaqueCoverageBelowMinimum` refusal.
This control renderer proved that the disabled state was a correlation, not the root cause.

The true root cause was an animation-driven material swap.
An FX layer on the avatar contained palette swap animation clips that swapped four alternate color materials into the same material slot.
The mathematical intersection of proven opaque triangles across all five reachable material states dropped to 17.55 percent.
Because 17.55 percent is less than the configured 25 percent minimum opaque coverage threshold, AMUSE safely left the slot unchanged.
Lowering the threshold to 15 percent allows the 17.55 percent intersection to pass and optimize all opaque triangles.

On 2026-10-15, the repository lacks an explicit regression test covering disabled renderers that animations enable.
Without regression coverage, future changes to renderer enumeration can introduce regressions against disabled renderers.

---

## 2. Goals and Non-Goals

### Goals
1. Preserve the existing pipeline behavior where AMUSE analyzes and optimizes disabled renderers identically to enabled renderers.
2. Add a permanent, deterministic EditMode regression test in `AlphaSeparationApplyTests.cs` using the established `RunConversionBuild` helper.
3. Verify that the committed animation clip on the processed avatar preserves float curves targeting `m_Enabled`.
4. Verify that the authored source animation clip on disk remains untouched.

### Non-Goals
1. No changes to the conservative animation state intersection rule.
A triangle must remain opaque across all reachable animation states to prove a triangle opaque.
2. No changes to default preset coverage thresholds.
3. No speculative animator reachability solvers or mutual exclusion assumptions.
4. No production code changes, because production code already treats disabled renderers correctly.

---

## 3. Architectural Design and Invariants

### Invariant 1: Equal Treatment of Disabled Renderers
AMUSE must continue to enumerate renderers using `GetComponentsInChildren<Renderer>(true)`.
No pass in `AmusePlatformFinishPlugin` can filter renderers by `renderer.enabled`, `Component.enabled`, `GameObject.activeSelf`, or `GameObject.activeInHierarchy`.

### Invariant 2: Preservation of `m_Enabled` Curves
Animation curves targeting `m_Enabled` on a Renderer or `m_IsActive` on a GameObject are float curves.
AMUSE only parses and rewrites material slot object reference curves (`m_Materials.Array.data[i]`).
Float curves remain untouched on the component.
When AMUSE appends an opaque submesh and material slot, the existing `m_Enabled` curve continues to toggle the whole renderer.

### Invariant 3: Minimal Implementation (Ponytail)
The ponytail skill requires the simplest solution that works.
Because the production code already handles disabled renderers correctly, AMUSE introduces no speculative abstractions or production refactors.
The defense consists of locking this behavior into the permanent test suite by extending the existing `AlphaSeparationApplyTests.cs` suite.

---

## 4. Test Specification

One focused EditMode test will be added to `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`:

### Test Method: `DisabledRendererWithToggledOnAnimationIsAdmittedAndOptimized`
- **Fixture**:
  - Build an avatar fixture using `AlphaSeparationTestLifecycle.CreateAvatarWithDescriptor`.
  - Add a child GameObject with a `SkinnedMeshRenderer` whose `enabled` property is `false`.
  - The renderer carries a cutout material with verified opaque triangles exceeding 25 percent coverage (using `CreateVerifiedCutoutMaterial()`).
  - An `AnimatorController` attached via `VRCAvatarDescriptor.baseAnimationLayers[0]` (using `AlphaSeparationTestLifecycle.AttachDescriptorController`).
  - An animation clip animating `SkinnedMeshRenderer.m_Enabled` from 0 to 1 on the disabled renderer path.
- **Execution**:
  - Run the build through `RunConversionBuild(root)`.
  - `RunConversionBuild` executes the complete NDMF pipeline on an isolated build clone inside an active `AnimatorServicesContext`.
- **Verification**:
  - Assert that `state.AnalyzedRendererCount` equals 1.
  - Assert that `state.AppliedRendererCount` equals 1.
  - Assert that `state.AppliedOpaqueTriangleCount` equals the proven opaque triangle count.
  - Assert that the processed clone renderer has `sharedMesh.subMeshCount` increased by 1.
  - Assert that the processed clone renderer has `sharedMaterials.Length` increased by 1 with the canonical opaque material.
  - Assert that the committed clip on the runtime controller preserves the `m_Enabled` curve intact.
  - Assert that the authored source animation clip on disk undergoes zero modifications.
- **Falsifier Hypothesis**:
  - If a developer introduces an erroneous filter in `AmusePlatformFinishPlugin` (such as `if (!renderer.enabled) continue;`), this test fails immediately because `state.AppliedRendererCount` drops to 0.

---

## 5. Security, Safety, and Privacy

1. **Safety**: No production changes to asset mutation boundaries.
Source assets are never modified.
`RunConversionBuild` mutates only the NDMF build clone.
2. **Privacy**: Tests use minimal, synthetic, in-memory schemas under `Hidden/Alrauna/AmuseTests/*`.
No private assets, textures, or vendor shaders are used.
3. **Determinism**: Tests run in EditMode and produce deterministic counts.
