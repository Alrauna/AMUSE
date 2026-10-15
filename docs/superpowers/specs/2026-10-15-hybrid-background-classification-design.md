# Hybrid Background Classification: Zero-Stutter Material Transformations Design

Date: 2026-10-15

Base: `a062f63` on branch `feat/simplify-material-transformations-ux`

Investigation: `docs/superpowers/investigations/2026-10-15-hybrid-background-classification-investigation.md`

## Overview

This design removes main-thread editor lag during material transformation analysis in AMUSE.
It decouples CPU-intensive triangle classification from the main thread of the Unity Editor.
The main thread performs host and asset capture in under 15 milliseconds.
A background worker task evaluates polygon intervals on the thread pool using `System.Threading.Tasks.Task.Run`.
The Unity Editor main thread runs smoothly at a stable 60 frames per second throughout the analysis.

---

## Decisions

### 1. Three-Stage Thread Boundary Separation

Inside `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`, renderer analysis splits into three distinct stages:

#### Stage A: Host & Asset Capture (Main Thread Only)
The main thread captures engine objects:
- `EffectiveMaterialMaterialization.CaptureBlockState(renderer)` reads `MaterialPropertyBlock`.
- `CaptureFor` captures animation closures and material properties.
- `RecordConsentFacts` records consent facts.
- `RuntimeStateResolution.ResolveRuntimeStates` evaluates alpha semantics.
- `UnityRendererAlphaAnalysis.CaptureGeometry` reads `MeshFilter.sharedMesh` or `SkinnedMeshRenderer.sharedMesh` and extracts vertex arrays, UV arrays, and triangle index buffers.
- If any stage refuses or hits cache, AMUSE emits rows immediately on the main thread.
- If Stage A succeeds, it creates immutable snapshot records and dispatches Stage B.

#### Stage B: Triangle Alpha Classification (Background Thread via `Task.Run`)
The background task executes pure mathematics:
```csharp
Task.Run(() => RuntimeStateResolution.ClassifyRuntimeStates(
    snapshot,
    slotResults,
    cancellationToken),
    cancellationToken);
```
- **Inputs**: `UnityRendererAlphaSnapshot` and `SlotResolutionResult[]`.
  Both inputs contain only managed arrays (`Vector3[]`, `Vector2[]`, `int[]`) and immutable enums.
  They contain zero `UnityEngine.Object` references.
- **Computation**: Exact rational interval arithmetic, UV bounding envelopes, and polygon clipping.
  Zero Unity C++ engine APIs are called.
  Zero mutable static states are accessed.
- **Cancellation**: The triangle loop in `UnityRendererAlphaAnalysis.Classify` checks `cancellationToken.IsCancellationRequested` every 1,024 triangles:
  ```csharp
  if ((triangle & 1023) == 0 && cancellationToken.IsCancellationRequested)
  {
      cancellationToken.ThrowIfCancellationRequested();
  }
  ```
- **Output**: `MeshSeparationPlan`.
  Contains submesh partitions and triangle ordinal lists.
  Zero `UnityEngine.Object` references.

#### Stage C: Decision Resolution and Presentation (Main Thread Only)
When the background task finishes, the main thread reaps the result:
- `DeriveAlreadyOpaqueBySlot` reads live material properties (`renderQueue`, `GetTag`).
- `SlotTransformDecision.Decide` computes slot conversion verdicts.
- `BuildRows` constructs `MaterialTransformationRow` records and reads renderer hierarchy names.
- `DryRunCache.StoreRenderer` caches the rows.
- Advances to the next renderer.

---

### 2. State Machine in AlphaSeparationDryRun

`AlphaSeparationDryRun` manages in-flight execution using an explicit state enum:

```csharp
internal enum DryRunState
{
    Idle,
    ClassifyingBackground,
    Completed
}
```
#### Context Storage on AlphaSeparationDryRun
To follow the Ponytail principle and eliminate unnecessary heap allocations, the runner stores in-flight fields directly as private instance fields on `AlphaSeparationDryRun`:
- `private Task<MeshSeparationPlan> _inFlightTask;`
- `private Renderer _inFlightRenderer;`
- `private string _inFlightRendererPath;`
- `private string _inFlightRendererTypeName;`
- `private string _inFlightRendererName;`
- `private IReadOnlyList<Material> _inFlightAdmittedLive;`
- `private IReadOnlyList<BlockStateEntry> _inFlightBlockState;`
- `private CapturedAnimationEvidence _inFlightEvidence;`
- `private RuntimeStateResolution.ResolvedRuntimeStates _inFlightResolved;`
- `private IReadOnlyList<CapturedMaterialSlotEvidence> _inFlightSlots;`
- `private string _inFlightRendererKey;`
- `private string _inFlightCompositeKey;`
- `private bool _inFlightRefreshOnly;`

#### Step Execution Contract
```csharp
internal bool Step(bool synchronous = true)
```
1. **Default Value**: `synchronous` defaults to `true`.
   Existing EditMode tests call `Step()` without arguments in tight loops.
   Defaulting to synchronous execution guarantees that all existing unit tests pass without modifying test callers.
   The Unity inspector update loop in `AmuseAvatarOptimizerEditor.OnEditorUpdate()` explicitly passes `synchronous: false`.
2. If `_state == DryRunState.Idle`:
   - If `_rendererIndex >= renderers.Length`: calls `CompleteRun()`, sets `_state = DryRunState.Completed`, and returns `false`.
   - Fetches `renderer = renderers[_rendererIndex]`.
   - Runs Stage A capture.
   - If cache hit or refusal: emits rows, advances `_rendererIndex`, and returns `true`.
   - If successful: dispatches `Task.Run`, sets `_state = DryRunState.ClassifyingBackground`, and stores context in the private fields.
   - If `synchronous` is `true`: calls `_inFlightTask.Wait()`, executes Stage C, advances `_rendererIndex`, sets `_state = DryRunState.Idle`, and returns `true`.
   - If `synchronous` is `false`: returns `true` immediately to keep the main thread unblocked.
3. If `_state == DryRunState.ClassifyingBackground`:
   - If `synchronous` is `true`: calls `_inFlightTask.Wait()` inside a try-catch block.
   - If `!_inFlightTask.IsCompleted`: returns `true` (main thread does zero work).
   - If `_inFlightTask.IsCompleted`:
     - If `_inFlightTask.IsCanceled`: clears in-flight fields, sets `_state = DryRunState.Completed`, sets `Done = true`, and returns `false`.
     - If `_inFlightTask.IsFaulted`: logs the inner exception from `_inFlightTask.Exception`, records a `MaterialTransformationRow.Errored` row, clears in-flight fields, advances `_rendererIndex`, sets `_state = DryRunState.Idle`, and returns `true`. (Does not abort remaining renderers).
     - If successful: executes Stage C on `_inFlightTask.Result`, clears in-flight fields, advances `_rendererIndex`, sets `_state = DryRunState.Idle`, and returns `true`.

### 3. Editor Lifecycle Safety and Cancellation

#### Event Subscriptions in AmuseAvatarOptimizerEditor
The inspector registers and unregisters lifecycle events in `OnEnable` and `OnDisable` to prevent memory leaks:
- **Registration in `OnEnable()`**:
  ```csharp
  AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
  EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
  EditorApplication.update += OnEditorUpdate;
  ```
- **Cleanup in `OnDisable()`**:
  ```csharp
  AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
  EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
  EditorApplication.update -= OnEditorUpdate;
  _dryRun?.Cancel(timeoutMs: 200);
  _dryRun = null;
  ```
- **Assembly Reload Handler**:
  `OnBeforeAssemblyReload` calls `_dryRun?.Cancel(timeoutMs: 200)` and clears the runner.
  Waiting up to 200 ms ensures worker threads exit before Unity unloads the AppDomain.
- **Play Mode Handler**:
  When `stateChange == PlayModeStateChange.ExitingEditMode`, calls `_dryRun?.Cancel(timeoutMs: 100)` and clears the runner.

#### Cancellation Implementation in AlphaSeparationDryRun
```csharp
internal void Cancel(int timeoutMs = 0)
{
    if (_cts != null && !_cts.IsCancellationRequested)
    {
        _cts.Cancel();
    }

    if (timeoutMs > 0 && _inFlightTask != null && !_inFlightTask.IsCompleted)
    {
        try
        {
            _inFlightTask.Wait(timeoutMs);
        }
        catch (Exception)
        {
            // Suppress AggregateException / OperationCanceledException during shutdown wait
        }
    }

    ClearInFlight();
    _state = DryRunState.Completed;
    Done = true;
    IsCancelled = true;
    RepaintRequested?.Invoke();
}
```

---

### 4. Inspector UI Progress and Cancellation Handling

In `AmuseAvatarOptimizerEditor.DrawMaterialTransformations()`:
1. **Cancelled State Handling**:
   If `_dryRun != null && _dryRun.IsCancelled`:
   - Renders an informative message:
     `EditorGUILayout.HelpBox("AMUSE material transformation analysis was cancelled.", MessageType.Info);`
   - Renders a **Compute** button:
     ```csharp
     if (GUILayout.Button("Compute", GUILayout.Width(80)))
     {
         _dryRun = null;
         _dryRunCompletedLatched = false;
     }
     ```
   - Returns early. This prevents an infinite restart loop.
2. **In-Progress State Handling**:
   While `!_dryRunCompletedLatched && !_dryRun.IsCancelled`:
   - Renders progress:
     ```csharp
     var text = _dryRun != null && _dryRun.TotalRendererCount > 0
         ? $"AMUSE computes material transformations: renderer {_dryRun.CurrentRendererIndex + 1} of {_dryRun.TotalRendererCount}."
         : "AMUSE computes material transformations.";
     EditorGUILayout.HelpBox(text, MessageType.Info);
     ```
   - Renders a **Cancel** button:
     ```csharp
     if (GUILayout.Button("Cancel", GUILayout.Width(60)))
     {
         _dryRun?.Cancel();
         GUIUtility.ExitGUI();
     }
     ```
   - Clicking **Cancel** marks `_dryRun.IsCancelled = true` without nulling `_dryRun`, stopping the background task instantly and preventing restart.

## Verification Plan

### Automated Tests
1. **Thread-Safety Test**:
   Verify that `RuntimeStateResolution.ClassifyRuntimeStates` executes identically on a background thread and returns a valid `MeshSeparationPlan`.
2. **Cancellation Test**:
   Verify that signaling `CancellationTokenSource.Cancel()` causes `ClassifyRuntimeStates` to throw `OperationCanceledException` and abort promptly.
3. **Async State Machine Test**:
   Verify that `Step(synchronous: false)` transitions through `Idle` -> `ClassifyingBackground` -> `Idle`, and that `Step(synchronous: true)` completes synchronously.
4. **PumpAll Test**:
   Verify that `PumpAll()` completes all renderers synchronously on multi-renderer avatar fixtures.
5. **Inspector Cancel Test**:
   Verify that calling `Cancel()` transitions the runner to `Done = true` without throwing unhandled exceptions.
