# Semantics Two-Pass, Texture Dimension, and Keyword Safety Implementation Plan

Date: 2026-10-08. Branch: `fix/latent-bugs-and-impurities`. Base: `main` at `9eed463`.

Privacy note: this record is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, instance name, or test-job identifier. Aggregate counts appear as ranges. Machine references name roles only, for example the dev editor instance.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix two-pass Poiyomi shader misinterpretation, guard texture dimensions in sampler extraction, and strip mode-specific keywords during regular lilToon opaque conversions.

**Architecture:** Shader frontends and material evidence in `Editor/Semantics/`. The architecture preserves fail-closed guarantees across all shader properties and conversions.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode).

**Spec:** `docs/superpowers/specs/2026-10-08-semantics-two-pass-and-keyword-safety-design.md`

## Global Constraints

- Use simple English. Short active sentences. One idea per sentence.
- No semicolons in prose.
- No contractions.
- Never record absolute or machine-specific paths. Use repository-relative paths only.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names.
- Date every status claim. As of 2026-10-08.
- Never stage, commit, push, or modify Git history without explicit user authorization.
- The dev editor instance with `Application.dataPath == <repo-root>/Assets` is the only authorized test instance.

---

### Task 1: Two-Pass Poiyomi Evidence Request and Analysis

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`

**Interfaces:**
- Consumes: `PoiyomiMaterialSemantics.AnalyzeBaseMaterial` and `PoiyomiMaterialSemantics.FullTwoPassMaterialEvidenceRequest`
- Produces: Complete semantic interpretation capturing second-pass alpha properties for Two-Pass Poiyomi shaders

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`, add unit tests:
- `FullTwoPassMaterialEvidenceRequest_ContainsAllRequiredSchemaPropertiesAndTwoPassAlphaProperties`
  Verify that the request includes schema properties (`_BumpMap`, `_EmissionMap`) and two-pass alpha properties (`_AlphaForceOpaque2`, `_ModeTwoPass`).
- `InterpretVerifiedTwoPassMaterial_CapturesSecondPassAlphaParameters`
  Verify that two-pass interpretation captures second-pass alpha inputs rather than single-pass defaults.

- [ ] **Step 2: Run tests to verify they fail or exhibit defects**

Run the new unit tests via the Unity Test Runner.
Observe the failure because `FullTwoPassMaterialEvidenceRequest` does not exist yet and `AnalyzeBaseMaterial` captures single-pass evidence.

- [ ] **Step 3: Implement two-pass evidence capture and analysis**

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`:
- Declare `internal static MaterialEvidenceRequest FullTwoPassMaterialEvidenceRequest { get; } = CreateTwoPassAlphaEvidenceRequest(FullMaterialEvidenceRequest);`.
- In `AnalyzeBaseMaterial`, detect whether `material.shader.name` equals `PoiyomiTwoPassShaderName`.
- Select `FullTwoPassMaterialEvidenceRequest` when `isTwoPass` is true.
- Pass `isTwoPass` into `AlphaPredicateRequestFor` and `InterpretVerifiedMaterial`.

- [ ] **Step 4: Run tests to verify they pass**

Run unit tests via the Unity Test Runner on the dev editor instance.
Verify all tests pass.

---

### Task 2: Texture Dimension Guard in Sampler Extraction

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`

**Interfaces:**
- Consumes: `UnityTextureEvidence.TryGetSampling`
- Produces: Fail-closed sampling extraction that rejects non-2D textures

- [x] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`, add unit test:
- `TryGetSampling_Non2DTexture_ReturnsFalse`
Verify that `Cubemap`, `Texture3D`, or `RenderTexture` with non-2D dimension returns false.

- [x] **Step 2: Run tests to verify they fail or exhibit defects**

Run the new test via the Unity Test Runner.
Observe failure because the existing code does not check `texture.dimension`.

- [x] **Step 3: Implement texture dimension guard**

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`:
- In `TryGetSampling`, check `if (texture.dimension != UnityEngine.Rendering.TextureDimension.Tex2D) return false;`.

- [x] **Step 4: Run tests to verify they pass**

Run unit tests via the Unity Test Runner on the dev editor instance.
Verify all tests pass.

---

### Task 3: LilToon Mode Keyword Normalization on Regular Opaque Conversions

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`

**Interfaces:**
- Consumes: `LilToonOpaqueTarget.PrepareCanonicalOpaqueClone`
- Produces: Normalized canonical opaque material clones without leaked cutout, clip, dither, or alpha mask keywords

- [x] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`, add unit tests:
- `PrepareCanonicalOpaqueClone_RegularCutoutConversion_DisablesAllModeKeywords`
  Enable `UNITY_UI_ALPHACLIP`, `UNITY_UI_CLIP_RECT`, `ETC1_EXTERNAL_ALPHA`, and `_COLOROVERLAY_ON` on the source material. Assert that all four are disabled on the clone.
- `PrepareCanonicalOpaqueClone_PreservesFeatureKeywords`
  Enable `_NORMALMAP` and `_EMISSION` on the source material. Assert that both remain enabled on the clone.

- [x] **Step 2: Run tests to verify they fail or exhibit defects**

Run the new tests via the Unity Test Runner.
Observe failure because the existing regular conversion branch does not normalize keywords on `clone`.

- [x] **Step 3: Implement keyword normalization on regular conversions**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`:
- In `PrepareCanonicalOpaqueClone`, in the `else` branch (when `attestedTarget != source.shader`):
  - Call `clone.DisableKeyword("UNITY_UI_ALPHACLIP")`.
  - Call `clone.DisableKeyword("UNITY_UI_CLIP_RECT")`.
  - Call `clone.DisableKeyword("ETC1_EXTERNAL_ALPHA")`.
  - Call `clone.DisableKeyword("_COLOROVERLAY_ON")`.

- [x] **Step 4: Run tests to verify they pass**

Run unit tests via the Unity Test Runner on the dev editor instance.
Verify all tests pass.
