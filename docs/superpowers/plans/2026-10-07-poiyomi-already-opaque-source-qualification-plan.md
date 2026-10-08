# Poiyomi AlreadyOpaque Source Qualification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Qualify authored opaque Poiyomi materials as `AlreadyOpaque` in `PoiyomiOpaqueConversion.cs` to prevent unnecessary material cloning during avatar builds.

**Architecture:** Separate source qualification from target clone validation. Source qualification checks essential opaque properties: `_Mode == 0` or `_AlphaForceOpaque == 1`, `_BlendOp == 0`, `_SrcBlend == 1`, `_DstBlend == 0`, `_ZWrite == 1`, `_ZTest == 4`, `_AddSrcBlend == 1`, `_AddDstBlend == 1`, `_AlphaToCoverage == 0`, `_AlphaPremultiply == 0`, `RenderType == "Opaque"`, and `renderQueue == 2000`. Inactive properties such as `_Cutoff` and alpha blending factors are ignored for source qualification while remaining strictly enforced for synthesized target clones.

**Tech Stack:** C# 9.0, Unity 2022.3, NUnit, NDMF.

**Spec:** `docs/superpowers/specs/2026-10-07-poiyomi-already-opaque-source-qualification-design.md`

## Global Constraints

- Date every status claim in a record. Never write an unqualified "today" or "currently".
- Every English text that a human reads uses simple english: short active sentences, one idea per sentence, no semicolons, and no contractions.
- Never record an absolute or machine-specific path in any document, comment, commit message, or test.
- Never expose private Census names, paths, GUIDs, per-avatar/per-renderer rows, or fingerprint-like identifiers.
- Name machines and instances by role only: "the dev editor instance" and "the Census Lab editor instance".
- Treat Unity assets and `.meta` files as one logical unit.
- Follow RED/GREEN discipline. Observe a failing test before implementing code.

---

### Task 1: Add Unit Tests for Authored Opaque Poiyomi Material Qualification

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`

**Interfaces:**
- Consumes: `PoiyomiOpaqueConversion.EvaluateVerifiedEligibility(CapturedMaterialEvidence, int, string, bool)`
- Produces: Test coverage proving that authored materials with shader defaults evaluate to `AlreadyOpaque`.

- [ ] **Step 1: Write the failing tests in PoiyomiOpaqueConversionTests.cs**

Add tests to `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`:
1. `AuthoredOpaqueMaterialWithShaderDefaults_EvaluatesToAlreadyOpaque`: Tests that a material with `_Mode = 0`, `_Cutoff = 0.5`, `_BlendOpAlpha = 0`, `_DstBlendAlpha = 10`, `_OutlineDstBlendAlpha = 10`, `_OutlineBlendOpAlpha = 0`, `renderQueue = 2000`, and `RenderType = "Opaque"` yields `PoiyomiOpaqueConversionOutcome.AlreadyOpaque`.
2. `AuthoredOpaqueMaterialWithForcedAlpha_EvaluatesToAlreadyOpaque`: Tests that a material with `_AlphaForceOpaque = 1` and base RGB replacement yields `AlreadyOpaque`.
3. `PerturbingEssentialOpaqueProperty_PreventsAlreadyOpaque`: Tests that perturbing essential properties (`_SrcBlend`, `_DstBlend`, `_BlendOp`, `_ZWrite`, `_ZTest`, `_AddSrcBlend`, `_AddDstBlend`, `_AlphaToCoverage`, `_AlphaPremultiply`, queue, or RenderType tag) prevents `AlreadyOpaque`.
4. Update `PerturbingAnyCanonicalProperty_PreventsAlreadyOpaque`: Split the existing test so that essential properties prevent `AlreadyOpaque`, while inactive properties (`_Cutoff`, `_BlendOpAlpha`, `_DstBlendAlpha`, `_OutlineDstBlendAlpha`, `_OutlineBlendOpAlpha`) retain `AlreadyOpaque`.

- [ ] **Step 2: Run test in Unity Test Runner to verify failure**

Filter: `PoiyomiOpaqueConversionTests.AuthoredOpaqueMaterialWithShaderDefaults_EvaluatesToAlreadyOpaque`
Expected: FAIL. The test fails because `IsCanonicalOpaque` requires `_Cutoff == 0`, `_BlendOpAlpha == 4`, and `_DstBlendAlpha == 1`.

---

### Task 2: Implement Functional Opaque Source Qualification in PoiyomiOpaqueConversion.cs

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:284-288, 420-438`

**Interfaces:**
- Consumes: `CapturedMaterialEvidence`, `ConversionSchema`, `effectiveRenderQueue`, `effectiveRenderType`
- Produces: `PoiyomiOpaqueConversionEligibility.AlreadyOpaque()` when an authored material is functionally opaque.

- [ ] **Step 1: Implement IsFunctionallyOpaque in PoiyomiOpaqueConversion.cs**

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`:
1. Define the essential opaque property indices or checks:
   - `_Mode == 0f` or `_AlphaForceOpaque == 1f`
   - `_BlendOp == 0f` (Add)
   - `_SrcBlend == 1f` (One)
   - `_DstBlend == 0f` (Zero)
   - `_ZWrite == 1f` (On)
   - `_ZTest == 4f` (LEqual)
   - `_AddSrcBlend == 1f` (One)
   - `_AddDstBlend == 1f` (One)
   - `_AlphaToCoverage == 0f` (Off)
   - `_AlphaPremultiply == 0f` (Off)
   - `effectiveRenderQueue == CanonicalOpaqueRenderQueue` (2000)
   - `effectiveRenderType == CanonicalOpaqueRenderType` ("Opaque")
2. In `EvaluateVerifiedEligibility`, replace `IsCanonicalOpaque` with `IsFunctionallyOpaque` for step 2.
3. Keep `CanonicalOpaqueTuple`, `CanonicalOpaqueProperties`, and `TryFindNonCanonicalFact` unchanged. Generated clones continue to receive and validate the full 23-property recipe.

- [ ] **Step 2: Run test in Unity Test Runner to verify passing**

Filter: `PoiyomiOpaqueConversionTests`
Expected: PASS. All tests in `PoiyomiOpaqueConversionTests` pass, including target validation and source qualification tests.

---

### Task 3: Verify AlphaSeparationPreparation and End-to-End Build Behavior

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: `AlphaSeparationPreparation.Prepare`
- Produces: Verification that authored opaque materials map to themselves without clone instantiation.

- [ ] **Step 1: Run preparation unit tests**

Filter: `AlphaSeparationPreparationTests`
Expected: PASS. Preparation tests continue to pass.

- [ ] **Step 2: Run all product EditMode tests**

Assembly: `Alrauna.Amuse.Tests.Editor`
Expected: PASS. All product tests pass.

- [ ] **Step 3: Characterize Census Lab Avatar under test**

In the Census Lab editor instance:
Execute read-only inspection script.
Verify that the 63 material slots and roughly 295,000 triangles of authored `_Mode = 0` Poiyomi materials evaluate to `AlreadyOpaque`.
Verify that zero material clones are generated for those slots.
Record observed counts in the investigation document.
