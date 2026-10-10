# Analysis Mathematical Safety Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix coordinate arithmetic overflow, infinite loop rollover on boundary texels, array bounds safety, native integer FloorMod, and rational orientation consolidation in the Analysis module.

**Architecture:** Pure, headless mathematics in `Editor/Analysis/`. All methods execute without Unity Editor dependencies. Fail-closed guarantees are preserved.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode).

**Spec:** `docs/superpowers/specs/2026-10-08-analysis-mathematical-safety-design.md`

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

### Task 1: Repeat Coordinate Overflow Protection and Rollover Guards

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`

**Interfaces:**
- Consumes: `ClassifyBilinearRepeat`, `HasMappedWitnessBilinearRepeat`, `ClassifyPointRepeat`, `HasMappedWitnessPointRepeat`
- Produces: Safe fail-closed classification and witness checking on extreme coordinates without 32-bit overflow or infinite loops

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`, add tests verifying that extreme repeat coordinates and boundaries return `Unknown` (or `true` for witness methods) without hanging:
- `RepeatCoordinateDifferenceOverflow_ReturnsUnknown`
- `WitnessRepeatCoordinateOverflow_ReturnsTrue`
- `RepeatBoundaryCoordinates_PreventLoopRollover`

- [ ] **Step 2: Run tests to verify they fail or exhibit defects**

Run the new tests via Unity Test Runner. Verify that overflow coordinates fail or hang prior to the fix.

- [ ] **Step 3: Implement overflow protection and rollover guards**

In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`:
- In `ClassifyBilinearRepeat` and `HasMappedWitnessBilinearRepeat`, cast coordinate differences to `long` before computing candidate count.
- In `ClassifyPointRepeat` and `HasMappedWitnessPointRepeat`, cast coordinate differences to `long` before computing candidate count.
- Add boundary guards checking `minimumX <= int.MinValue || maximumX >= int.MaxValue || minimumY <= int.MinValue || maximumY >= int.MaxValue` before loop execution in all four methods.
- Return `TriangleAlphaOutcome.Unknown` for classification methods and `true` for witness methods on overflow or boundary violations.

- [ ] **Step 4: Run tests to verify they pass**

Run tests via Unity Test Runner. Verify all new and existing tests pass.

---

### Task 2: Extra UV Channel Array Bounds Validation

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`

**Interfaces:**
- Consumes: `TriangleAlphaInput.TryGetUvSet(int index, out Vector2 a, out Vector2 b, out Vector2 c)`
- Produces: `bool` indicating whether the UV channel set exists and contains all three vertex indices

- [x] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`, add tests for `TryGetUvSet`:
- `TryGetUvSet_WithTruncatedChannelBuffer_ReturnsFalseWithoutThrowing`
- `TryGetUvSet_WithNegativeVertexIndices_ReturnsFalseWithoutThrowing`

- [x] **Step 2: Run tests to verify they fail**

Run tests via Unity Test Runner. Verify that accessing truncated buffers throws `ArgumentOutOfRangeException`.

- [x] **Step 3: Implement bounds checks**

In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` in `TryGetUvSet`:
- Validate `(uint)_indexA >= (uint)uvList.Count || (uint)_indexB >= (uint)uvList.Count || (uint)_indexC >= (uint)uvList.Count`.
- Return false when any index is out of bounds.

- [x] **Step 4: Run tests to verify they pass**

Run tests via Unity Test Runner. Verify all tests pass.

---

### Task 3: Native 32-Bit FloorMod and Rational Orientation in CreateHull

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/ExactUvGeometryTests.cs`

**Interfaces:**
- Consumes: `ExactUvGeometry.FloorMod(int value, int modulus)` and `ExactUvGeometry.CreateHull`
- Produces: Fast integer floor modulo and exact rational orientation for collinearity and winding order

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/ExactUvGeometryTests.cs`:
- Test `FloorMod_IntegerOverloadMatchesBigIntegerOverload` across negative and positive integer ranges.
- Test `CreateHull_CollinearAndWindingPoints_UsesRationalOrientation`.

- [ ] **Step 2: Run tests to verify they fail**

Run tests via Unity Test Runner.

- [ ] **Step 3: Implement native FloorMod and update CreateHull**

In `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`:
- Add `internal static int FloorMod(int value, int modulus)`.
- In `CreateHull`, call `Orientation(unique[0], unique[1], unique[2])`. Check for zero equality and negative sign.
- Delete the private `Cross` method on lines 718 to 725.

- [ ] **Step 4: Run tests to verify they pass**

Run tests via Unity Test Runner. Verify all tests pass.
