# Analysis Secondary Channel Safety Implementation Plan

Date: 2026-10-08. Branch: `fix/latent-bugs-and-impurities`. Base: `main` at `9eed463`.

Privacy note: this record is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, instance name, or test-job identifier. Aggregate counts appear as ranges. Machine references name roles only, for example the dev editor instance.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent unhandled `ArgumentException` crashes when secondary UV channels (UV1 to UV3) contain non-finite coordinates.

**Architecture:** Pure mathematics in `Editor/Analysis/`. `TriangleAlphaInput.TryGetUvSet` performs coordinate validation and fails closed to `Unknown`. The architecture preserves fail-closed guarantees across all triangle operations.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode).

**Spec:** `docs/superpowers/specs/2026-10-08-analysis-secondary-channel-safety-design.md`

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

### Task 1: Coordinate Finiteness and Channel Bounds in TryGetUvSet

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`
- Test:
  - `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`
  - `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`

**Interfaces:**
- Consumes: `TriangleAlphaInput.TryGetUvSet`
- Produces: Safe fail-closed extraction that returns false on non-finite coordinates, invalid channels, or missing buffers

- [x] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`, add unit tests:
- `TryGetUvSet_ReturnsTrue_ForValidPrimaryChannelZero`
- `TryGetUvSet_ReturnsFalse_WhenPrimaryChannelContainsNaN`
- `TryGetUvSet_ReturnsFalse_WhenPrimaryChannelContainsInfinity`
- `TryGetUvSet_ReturnsFalse_WhenChannelIndexIsNegative`
- `TryGetUvSet_ReturnsFalse_WhenChannelIndexExceedsAvailableSets`
- `TryGetUvSet_ReturnsFalse_WhenSecondaryChannelContainsNaN`
- `TryGetUvSet_ReturnsFalse_WhenSecondaryChannelContainsPositiveInfinity`
- `TryGetUvSet_ReturnsFalse_WhenSecondaryChannelContainsNegativeInfinity`
- `TryGetUvSet_ReturnsTrue_WhenSecondaryChannelContainsValidCoordinates`

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`, add integration test:
- `LayerSamplingSecondaryChannelWithNaN_YieldsUnknownOutcomeWithoutThrowing`

- [x] **Step 2: Run tests to verify they fail or exhibit defects**

Run the new unit and integration tests via the Unity Test Runner.
Observe failures on non-finite primary and secondary channel tests because the existing method does not validate finiteness.
Observe unhandled `ArgumentException` in the integration test prior to the fix.

- [x] **Step 3: Implement finiteness guards and bounds in TryGetUvSet**

In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`:
In `TriangleAlphaInput`:
- In `TryGetUvSet`, preserve the `channel == 0` check.
- When `channel == 0`, verify `IsFinite` on `Uv0`, `Uv1`, and `Uv2`. Reset `a = default; b = default; c = default;` and return false if any coordinate is non-finite.
- When `channel > 0`, check bounds on `_extraUvSets`.
- Retrieve `a`, `b`, and `c` from the secondary UV set.
- Check `IsFinite` on `a`, `b`, and `c`. Reset `a = default; b = default; c = default;` and return false if any coordinate is non-finite.
- Add private helper `IsFinite(Vector2 value)`.

- [x] **Step 4: Run tests to verify they pass**

Run the unit and integration tests via the Unity Test Runner on the dev editor instance.
Verify all new and existing tests pass.
