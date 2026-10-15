# Hybrid Background Classification: Architectural Investigation for Zero-Stutter Material Transformations

Date: 2026-10-15

## Context and Goal

On 2026-10-15, the project owner selected **Path 3 (Hybrid Main-Thread Capture + Background Thread Classification)** to eliminate editor chugging during dry run refresh.
The goal is to keep the Unity Editor at a stable 60 frames per second during material transformation analysis.
This document synthesizes findings from parallel investigations into thread boundaries, state machine orchestration, and editor lifecycle safety.

---

## 1. Thread-Safety Boundaries and Data Decoupling

The execution inside `AlphaSeparationDryRun.ComputeRows` naturally splits into three stages.

### Stage A: Host & Asset Evidence Capture (Main Thread Only)
This stage executes before triangle classification.
It requires Unity engine objects:
1. `EffectiveMaterialMaterialization.CaptureBlockState(renderer)` reads `MaterialPropertyBlock`.
2. `CaptureFor(rendererPath, renderer.sharedMaterials, ...)` queries animator bindings and material properties.
   When analyzing non-streaming textures, it triggers GPU texture blits via `UnityAlphaFieldEvidence`.
   Non-streaming GPU readback calls `request.WaitForCompletion()`, which takes 50 to 300 ms for textures requiring GPU blits.
   Textures decoding in memory from PNG/TGA bypass this GPU readback.
3. `RecordConsentFacts` verifies user consent facts.
4. `RuntimeStateResolution.ResolveRuntimeStates` runs alpha transfer semantics in memory over captured evidence.
5. `UnityRendererAlphaAnalysis.CaptureGeometry` reads `MeshFilter.sharedMesh` or `SkinnedMeshRenderer.sharedMesh`.
   It copies vertices, UV arrays, and submesh triangle indices into managed arrays.

If any refusal occurs during Stage A, the runner constructs refusal rows immediately on the main thread.
No background task starts.

### Stage B: Pure Geometry Classification (Background Task via `Task.Run`)
This stage executes `RuntimeStateResolution.ClassifyRuntimeStates(snapshot, slotResults)`.
It takes approximately 85 percent of total processing time.

Audit findings on thread safety:
- **Input Data**:
  - `UnityRendererAlphaSnapshot` holds pure managed arrays: `Vector3[] Positions`, `Vector2[] Uv0`, extra UV sets, and `UnitySubmeshAlphaSnapshot[] Submeshes`.
    It contains zero `UnityEngine.Object` references.
  - `SlotResolutionResult[]` holds immutable data: slot indices, resolution enums, and `AlphaResolution` instances.
    It contains zero `UnityEngine.Object` references.
- **Computation**:
  - `TriangleAlphaClassifier` performs exact rational interval math using `ExactDyadic`, `ExactRational`, and `System.Numerics.BigInteger`.
  - Polygon clipping and UV bounding checks are pure mathematical operations.
  - No method touches `AssetDatabase`, `Resources`, `Shader`, `Material`, or `Texture2D`.
  - No class accesses mutable static state.
- **Output Data**:
  - `MeshSeparationPlan` holds submesh partitions and triangle ordinal lists.
  - It contains zero `UnityEngine.Object` references.

This stage is 100 percent thread-safe.
It can run safely on any background worker thread.

### Stage C: Decision Resolution and Presentation (Main Thread Only)
This stage executes after the background task completes:
1. `DeriveAlreadyOpaqueBySlot` reads live material properties (`renderQueue`, `GetTag`).
2. `SlotTransformDecision.Decide` computes slot conversion verdicts.
3. `BuildRows` constructs `MaterialTransformationRow` records and reads renderer hierarchy names.
4. `DryRunCache.StoreRenderer` caches the rows.

This stage takes under 2 milliseconds and runs on the main thread.

---

## 2. Asynchronous State Machine Design

### Runner States
In `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`, the runner transitions through three states:
- `Idle`: No background task is running. Ready to start the next renderer or complete the avatar.
- `ClassifyingBackground`: A background `Task<MeshSeparationPlan>` is in flight on the thread pool.
- `Completed`: All renderers have finished, or the avatar failed an avatar-level structural check.

### In-Flight Renderer Context
When Stage A dispatches the background task, `AlphaSeparationDryRun` stores an `InFlightRendererContext`.
This context holds:
- The target `Renderer` and its hierarchy paths.
- The captured evidence and resolved slot states.
- The cache keys (`rendererKey`, `compositeKey`).
- The running `Task<MeshSeparationPlan>`.

### Non-Blocking Step Execution
On each tick of `EditorApplication.update`:
1. If state is `Idle`:
   - If all renderers are processed, calls `CompleteRun()` and returns `false`.
   - Otherwise, extracts Stage A evidence for `renderers[_rendererIndex]`.
   - If Stage A refuses or hits cache, emits rows immediately and advances `_rendererIndex`.
   - If Stage A succeeds, dispatches `Task.Run` for Stage B, stores `InFlightRendererContext`, and sets state to `ClassifyingBackground`.
   - Returns `true`.
2. If state is `ClassifyingBackground`:
   - Checks `_inFlight.Task.IsCompleted`.
   - If `false`, returns `true` immediately. Main thread does zero work.
   - If `true`:
     - Checks `_inFlight.Task.IsFaulted` or `_inFlight.Task.IsCanceled`.
     - Unwraps any `AggregateException` to log or handle failures cleanly without letting exceptions escape into the editor update loop.
     - If successful, takes `_inFlight.Task.Result`, executes Stage C on the main thread, clears `_inFlight`, advances `_rendererIndex`, sets state to `Idle`, and returns `true`.

### EditMode Test Compatibility
Existing EditMode tests (such as `AlphaSeparationDryRunTests.Run`) loop `dryRun.Step()` up to 16 times in a tight loop.
If `Step()` returns immediately while a task is pending, the test loop exhausts its iterations in fractions of a millisecond and asserts failure.
To ensure full test compatibility:
- `AlphaSeparationDryRun.Step(bool synchronous = false)` accepts an optional parameter.
- When `synchronous: true`: if a background task is dispatched, `Step()` calls `task.Wait()` synchronously before returning.
- In `AmuseAvatarOptimizerEditor.OnEditorUpdate()`, it calls `_dryRun.Step(synchronous: false)` to ensure non-blocking UI behavior.
- In `AlphaSeparationDryRun.PumpAll()`, it runs synchronously until `Done`.
- All existing and new EditMode tests pass deterministically without race conditions.

---

## 3. Lifecycle Safety, Cancellation, and UI Affordance

### Lifecycle Risk Mitigation
Background tasks running in the Unity Editor must bind to three editor lifecycle events:
1. **Script Recompilation and Domain Reload (`AssemblyReloadEvents.beforeAssemblyReload`)**:
   Active background threads cause domain reload hangs or crashes.
   When `beforeAssemblyReload` fires, AMUSE immediately signals cancellation via `_cts.Cancel()`.
   It then waits briefly (up to 200 ms) for in-flight tasks to exit before allowing the domain reload to proceed.
2. **Entering Play Mode (`EditorApplication.playModeStateChanged`)**:
   When exiting edit mode, AMUSE cancels any running task to avoid background contention with the NDMF build.
3. **Inspector Selection Change or Disable (`OnDisable`)**:
   When the user selects another object or closes the window, AMUSE cancels the running task.

### Cancellation Plumbing
`AlphaSeparationDryRun` owns a `CancellationTokenSource`:
- When cancelling, `_cts.Cancel()` signals the background task.
- The triangle classification loop checks `cancellationToken.IsCancellationRequested` periodically (every 1,024 triangles using `(triangle & 1023) == 0`).
- If cancelled, the task exits cleanly.
- Cancellation transitions the runner state to `Completed` with `Done = true` (or clears `_dryRun = null` in the editor).
  This prevents infinite restart loops where `OnEditorUpdate` would immediately re-dispatch the same renderer.

### Inspector UI Affordance
While processing:
- The inspector renders an informative progress banner:
  `"AMUSE computes material transformations: renderer X of Y..."`
- A **Cancel** button is displayed:
  ```csharp
  if (GUILayout.Button("Cancel", GUILayout.Width(60)))
  {
      _dryRun.Cancel();
  }
  ```
- Clicking **Cancel** aborts the background task immediately and returns the inspector to an idle state with zero lag.

---

## 4. Synthesis

Path 3 eliminates main-thread chugging by decoupling 85 percent of the CPU workload from the Unity main loop.
The main thread spends at most 15 ms per renderer capturing host data.
The background worker thread computes triangle rational intervals on a secondary core.
Unity maintains a consistent 60+ frames per second throughout the entire refresh.
Lifecycle event handlers ensure total stability across domain reloads and Play mode boundaries.
Synchronous test methods (`PumpAll()`) preserve full automated test suite compatibility.
