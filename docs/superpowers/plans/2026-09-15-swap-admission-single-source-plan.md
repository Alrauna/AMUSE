# Animated material swap admission implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Date: 2026-09-15.
Base branch: `feat/animated-material-swap-alpha-optimization` at `7d48e2c`.
Spec: `docs/superpowers/specs/2026-09-15-swap-admission-single-source-design.md`.

**Goal:** Admit swap values from the AnimationIndex the apply pass validates against, so the barrier proves every material the apply can ever see.

**Lifecycle re-order (new):** the structural graph checks (animation events, synced layers, unresolvable motions, unknown behaviours, unsupported controller form) read real authored clips, and virtualization drops events. They must run before any extension scope virtualizes the graph. The plan adds one extension-free pass before the bindings pass for exactly those checks, then moves the barrier and apply into one shared AnimatorServicesContext scope so barrier admission and apply validation read one AnimationIndex.

**Tech Stack:** Unity 2022.3.22f1 editor APIs, NDMF 1.14.4, NUnit through Unity Test Framework (EditMode only).

## Global Constraints

- Compilation happens inside Unity. Refresh after file changes, then run EditMode filters. A filtered run that reports 0 tests is a failure. Record observed counts.
- File name equals type name. One type per file. Production types are internal. Namespaces mirror folders.
- Fail closed. Never weaken a valid test.
- Privacy: never record private names, machine paths, ports, or instance hashes. All prose follows ASD-STE100.

---

### Task 1: Observe virtual clips

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/LiveAnimationObservationTests.cs`

**Step 1 (RED):** test `ObserveVirtualClip_MatchesObserveClipSemantics`: build an AnimationClip with one exact float curve and one object curve on a slot binding. Virtualize it through a temp `VirtualControllerContext` (NDMF test seam). Observe both ways. Assert equal property names, finite-exact flags, and object values.
**Step 2 (GREEN):** add `internal static LiveClipObservation ObserveVirtualClip(VirtualClip clip, bool isSpecialMotion)` mirroring `ObserveClip` through `GetFloatCurveBindings`/`GetFloatCurve`/`GetObjectReferenceCurveBindings`/`GetObjectCurve`.

### Task 2: Structural checks move to a pre-virtualization pass

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (Configure: new first pass. The barrier drops its real-graph Enumerate)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/CommittedControllerGraph.cs` (keep Enumerate unchanged. It now serves the new pass)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishState.cs` (store the enumerated graph result)
- Test: existing structural refusal tests re-pointed at the new pass entry. Record counts.

**Step 1 (RED):** a test that fires the animation-event refusal through the new first pass entry and asserts the avatar refusal.
**Step 2 (GREEN):** new extension-free first pass runs `CommittedControllerGraph.Enumerate` and stores the result and refusal in `AmusePlatformFinishState`. The barrier stops calling Enumerate and reads the stored result.

### Task 3: Barrier admits from the AnimationIndex

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (Configure: barrier and apply share one `WithRequiredExtension(typeof(AnimatorServicesContext))` scope)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (Execute: build renderer-path observations from `AnimationIndex.GetClipsForObjectPath` through `ObserveVirtualClip`, union with the graph walk for float bindings, call the production observation-based capture entry)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs` (production `CaptureObserved` entry with real bounds, production selector and capturer)

**Step 1 (RED):** the falsifier from the spec: a swap clip reachable only through the index admits the variant and prepares. Fails on current code at apply with RuntimeMaterialValueNotMapped.
**Step 2 (GREEN):** implement the union and the production observation entry.

### Task 4: Full verification

- Full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` runs. Record counts.
- `git diff --check`. Identifier sweep over the branch diff.
- Census Lab characterization rerun: top garment renderers split. The skirt still refuses on its feature gate. Every refusal line names renderer, slot, and cause.
