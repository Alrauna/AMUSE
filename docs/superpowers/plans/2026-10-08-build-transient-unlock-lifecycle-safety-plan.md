# Build Transient Unlock Lifecycle Safety Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ensure animation clips on virtualized child controllers are inverted during window close, restore curve references to `LockedOriginal` without restrictive path filters, and perform avatar-wide verification before clone destruction.

**Architecture:** NDMF PlatformFinish integration in `Editor/Build/TransientUnlockWindowClose.cs`. Coordinates with host animator bindings and controller graph enumeration.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode), NDMF.

**Spec:** `docs/superpowers/specs/2026-10-08-build-transient-unlock-lifecycle-safety-design.md`

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

### Task 1: Virtualized Controller Clip Enumeration in Window Close

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`

**Interfaces:**
- Consumes: `CommittedClips(BuildContext context)`
- Produces: Complete enumeration of committed animation clips from innate controllers and `IVirtualizeAnimatorController` components on child transforms

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`:
- `CommittedClips_IncludesVirtualizedAnimatorControllerOnChildTransform`
- `CommittedClips_HandlesSubStateMachinesAndBlendTreesWithoutCycles`

- [ ] **Step 2: Run tests to verify they fail**

Run tests via Unity Test Runner. Verify failure because child virtualized controllers are omitted.

- [ ] **Step 3: Implement enumeration**

In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs` in `CommittedClips`:
- Enumerate both innate controllers and `context.AvatarRootObject.GetComponentsInChildren<IVirtualizeAnimatorController>(true)`.
- Use `seenControllers` and `seenClips` with `ClipsInStateMachine` to prevent duplicate clip enumeration.

- [ ] **Step 4: Run tests to verify they pass**

Run tests via Unity Test Runner. Verify all tests pass.

---

### Task 2: Direct Curve Reference Restoration to LockedOriginal

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`

**Interfaces:**
- Consumes: `InvertCommittedCurves(BuildContext context, TransientUnlockWindowState.SwappedPair pair)`
- Produces: Direct replacement of `pair.UnlockedClone` keyframes with `pair.LockedOriginal` across all committed clips without path or type filtering

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`:
- `InvertCommittedCurves_RestoresKeyframesToLockedOriginal_EvenWhenPathNotRecorded`
- `InvertCommittedCurves_RestoresKeyframesAcrossSkinnedMeshAndMeshRendererBindings`

- [ ] **Step 2: Run tests to verify they fail**

Run tests via Unity Test Runner. Verify that curves with unrecorded paths are skipped before the fix.

- [ ] **Step 3: Implement direct restoration**

In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs` in `InvertCommittedCurves`:
- Iterate over all committed clips and all object reference curves.
- Replace keyframes matching `pair.UnlockedClone` with `pair.LockedOriginal`.

- [ ] **Step 4: Run tests to verify they pass**

Run tests via Unity Test Runner. Verify all tests pass.

---

### Task 3: Avatar-Wide Verification and Graph Caching in CurveInversionWasComplete

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`

**Interfaces:**
- Consumes: `CurveInversionWasComplete`
- Produces: Avatar-wide check ensuring zero animation clips on the avatar retain references to `pair.UnlockedClone`

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`:
- `CurveInversionWasComplete_ReturnsFalse_WhenAnyClipRetainsCloneReference`
- `CurveInversionWasComplete_ReturnsFalse_WhenCommittedGraphReturnsRefusal`

- [ ] **Step 2: Run tests to verify they fail**

Run tests via Unity Test Runner. Verify that retained clone references previously passed check.

- [ ] **Step 3: Implement avatar-wide verification and graph caching**

In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:
- Update `CurveInversionWasComplete` to inspect all clips from `AnimationUtility.GetAnimationClips(context.AvatarRootObject)` and `CommittedClips`.
- If any keyframe points to `pair.UnlockedClone`, return false to keep the clone alive.
- Cache the enumerated controller graph once during the window close pass to eliminate redundant walks.

- [ ] **Step 4: Run tests to verify they pass**

Run tests via Unity Test Runner. Verify all tests pass.
