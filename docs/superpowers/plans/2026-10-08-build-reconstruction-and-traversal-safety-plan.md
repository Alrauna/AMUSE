# Build Reconstruction and Traversal Safety Implementation Plan

Date: 2026-10-08. Branch: `fix/latent-bugs-and-impurities`. Base: `main` at `9eed463`.

Privacy note: this record is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, instance name, or test-job identifier. Aggregate counts appear as ranges. Machine references name roles only, for example the dev editor instance.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore serialized integer properties accurately during locked material reconstruction and prevent infinite recursion or duplicate walks during animator graph traversals in window close.

**Architecture:** Build lifecycle and transient unlock mechanics in `Editor/Build/`. Material mutations and animator traversals enforce fail-closed safety and cycle termination.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode).

**Spec:** `docs/superpowers/specs/2026-10-08-build-reconstruction-and-traversal-safety-design.md`

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

### Task 1: Integer Property Reading and Restoration in LockedMaterialReconstruction

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialReconstruction.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/LockedMaterialReconstructionTests.cs`

**Interfaces:**
- Consumes: `LockedMaterialReconstruction.ReadSavedProperties` and `LockedMaterialReconstruction.MoveSuffixedSavedValues`
- Produces: Complete restoration of integer shader properties using `clone.SetInteger`

- [x] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/LockedMaterialReconstructionTests.cs`, add unit tests using a fixture shader declaring an integer property:
- `MoveSuffixedSavedValues_RestoresIntegerProperties_ToClonedMaterial`
- `ReadSavedProperties_ReadsIntegerPropertyStorage`
Verify that serialized integer properties restore to the reconstructed material with correct integer values.

- [x] **Step 2: Run tests to verify they fail or exhibit defects**

Run the new unit tests via the Unity Test Runner.
Observe failures because integer properties are skipped during restoration and omitted from the collected numeric values.

- [x] **Step 3: Implement integer property reading and restoration**

In `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialReconstruction.cs`:
- Expose `ReadSavedProperties` and `MoveSuffixedSavedValues` as `internal static`.
- In `ReadSavedProperties`, find `m_SavedProperties.m_Ints`.
- Read elements with `SerializedPropertyType.Integer` and add them to the numeric values collection.
- In `MoveSuffixedSavedValues`, add `else if (type == ShaderPropertyType.Int)`.
- Write the value using `clone.SetInteger(plainName, Mathf.RoundToInt(entry.value))`.

- [x] **Step 4: Run tests to verify they pass**

Run unit tests via the Unity Test Runner on the dev editor instance.
Verify all tests pass.

---

### Task 2: Visited Motion and State Machine Traversal Guards

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`

**Interfaces:**
- Consumes: `TransientUnlockWindowClose.ClipsInMotion`, `TransientUnlockWindowClose.ClipsInStateMachine`, and `TransientUnlockWindowClose.CommittedClips`
- Produces: Stack-safe animator clip enumeration that terminates cleanly on cycles and shared motions

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`, add unit tests:
- `ClipsInMotion_TerminatesCleanly_WhenBlendTreeContainsCycle`
- `ClipsInStateMachine_TerminatesCleanly_WhenStateMachineContainsCycle`
- `CommittedClips_TerminatesCleanly_WhenSharedBlendTreeExistsAcrossLayers`
Construct cyclic BlendTree and AnimatorStateMachine graphs.
Assert that traversal methods terminate cleanly without throwing exceptions.

- [ ] **Step 2: Run tests to observe failure condition**

Run tests via the Unity Test Runner.
Observe compilation failure or assertion failure prior to adding visited tracking.

- [ ] **Step 3: Implement visited guards in animator traversals**

In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:
- Expose `ClipsInStateMachine` and `ClipsInMotion` as `internal static`.
- Add `HashSet<AnimatorStateMachine> visitedStateMachines` and `HashSet<Motion> visitedMotions` parameters.
- Track visited state machines in `ClipsInStateMachine`. Return early if already visited.
- Track visited motions in `ClipsInMotion`. Return early if already visited.
- In `CommittedClips`, instantiate shared `visitedStateMachines` and `visitedMotions` sets once per controller and pass them to each layer traversal.

- [ ] **Step 4: Run tests to verify they pass**

Run unit tests via the Unity Test Runner on the dev editor instance.
Verify all tests pass.
