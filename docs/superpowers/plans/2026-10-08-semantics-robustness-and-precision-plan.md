# Semantics Robustness and Precision Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ensure Multi container opaque clones set `_TransparentMode` to zero, add finiteness checks to alpha mask properties, and assign main texture sampler state to blend mask samples.

**Architecture:** Pure shader semantics in `Editor/Semantics/LilToon/`. Operates on captured material evidence and canonical opaque clones. Fail-closed guarantees are preserved.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode).

**Spec:** `docs/superpowers/specs/2026-10-08-semantics-robustness-and-precision-design.md`

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

### Task 1: Multi Container Mode Zero Property Write and Verification

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`

**Interfaces:**
- Consumes: `PrepareCanonicalOpaqueClone`, `TryFindNonCanonicalFact`
- Produces: Opaque clone with `_TransparentMode` set to zero and non-canonical detection for non-zero mode values

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`:
- `PrepareCanonicalOpaqueClone_MultiContainer_SetsTransparentModeZero`
- `TryFindNonCanonicalFact_MultiContainerWithNonZeroMode_IdentifiesTransparentMode`

- [ ] **Step 2: Run tests to verify they fail**

Run tests via Unity Test Runner. Verify failure on mode 0 check.

- [ ] **Step 3: Implement mode zero write and canonical check**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`:
- In `PrepareCanonicalOpaqueClone`, set `clone.SetFloat("_TransparentMode", 0f)` when `attestedTarget == source.shader`.
- In `TryFindNonCanonicalFact`, check `candidate.HasProperty("_TransparentMode") && candidate.GetFloat("_TransparentMode") != 0f`. Return `factName = "_TransparentMode"`.

- [ ] **Step 4: Run tests to verify they pass**

Run tests via Unity Test Runner. Verify all tests pass.

---

### Task 2: Finiteness Validation in LilToon Alpha Mask Interpretation

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs`

**Interfaces:**
- Consumes: `LilToonAlphaMaskSemantics.Interpret`
- Produces: Fail-closed refusal without exception when scale or offset contains NaN or infinity

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs`:
- `AlphaMask_WithNaNScale_RefusesWithoutException`
- `AlphaMask_WithInfiniteOffset_RefusesWithoutException`

- [ ] **Step 2: Run tests to verify they fail**

Run tests via Unity Test Runner. Verify unhandled `ArgumentException` is thrown before the fix.

- [ ] **Step 3: Implement finiteness checks**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs` in `Interpret`:
- Check `float.IsFinite(assignment.Scale.x)`, `float.IsFinite(assignment.Scale.y)`, `float.IsFinite(assignment.Offset.x)`, and `float.IsFinite(assignment.Offset.y)`.
- If any value is not finite, return `Refuse(diagnostics, LilToonSemanticDiagnosticCode.UnsupportedFeature, MaskProperty)`.

- [ ] **Step 4: Run tests to verify they pass**

Run tests via Unity Test Runner. Verify all tests pass.

---

### Task 3: Main Texture Sampler Assignment for Blend Masks

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonLayerAlphaTerm.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonLayerAlphaTermTests.cs`

**Interfaces:**
- Consumes: `LilToonLayerAlphaTerm.Interpret`
- Produces: Blend mask `TextureSample` using `_MainTex` sampler state, refusing fail-closed if `_MainTex` lacks sampling

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonLayerAlphaTermTests.cs`:
- `BlendMask_UsesMainTexSamplingSettings`
- `BlendMask_WhenMainTexLacksSampling_RefusesFailClosed`

- [ ] **Step 2: Run tests to verify they fail**

Run tests via Unity Test Runner.

- [ ] **Step 3: Implement sampler assignment and fail-closed check**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonLayerAlphaTerm.cs`:
- Check that `_MainTex` exists, is non-null, and has sampling evidence (`mainAssignment.Texture.HasSampling`).
- If missing or unassigned, return `Refuse(diagnostics, "_MainTex")`.
- Pass `mainAssignment.Texture.Sampling` to the blend mask `TextureSample`.

- [ ] **Step 4: Run tests to verify they pass**

Run tests via Unity Test Runner. Verify all tests pass.
