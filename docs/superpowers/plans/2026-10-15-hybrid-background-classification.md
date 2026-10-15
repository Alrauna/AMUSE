# Hybrid Background Classification: Zero-Stutter Material Transformations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate main-thread editor lag during material transformation analysis in AMUSE by offloading CPU-intensive triangle classification to a background task using `Task.Run` with full lifecycle cancellation and smooth 60+ FPS editor responsiveness.

**Architecture:**
1. Pipeline Cancellation: forward `CancellationToken` into `RuntimeStateResolution.ClassifyRuntimeStates` and periodic checks in `UnityRendererAlphaAnalysis.Classify`.
2. Asynchronous State Machine: `AlphaSeparationDryRun` implements `DryRunState` (`Idle`, `ClassifyingBackground`, `Completed`). It runs Stage A (capture) on the main thread, Stage B (classification) via `Task.Run`, and Stage C (verdict & rows) on the main thread upon completion. `Step(bool synchronous = true)` defaults to synchronous execution for test compatibility.
3. Editor Lifecycle & UI: `AmuseAvatarOptimizerEditor` drives `Step(synchronous: false)` on `EditorApplication.update`, wires cancellation to `beforeAssemblyReload`, `playModeStateChanged`, and `OnDisable`, and provides an interactive progress banner with a **Cancel** button.

**Tech Stack:** C# against Unity 2022.3 APIs, `System.Threading.Tasks.Task.Run`, `System.Threading.CancellationTokenSource`, UnityEditor IMGUI, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-10-15-hybrid-background-classification-design.md`

## Global Constraints

- Every English text that a human reads uses simple english or ASD-STE100 Simplified Technical English: short active sentences, one idea per sentence, no semicolons, and no contractions.
- Never record an absolute or machine-specific path in any document, comment, commit message, or test. Use `<repo-root>`-relative paths.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names and hashes. Name machines and instances by role only.
- Date every status claim in a record. Never write an unqualified "today" or "currently".
- Zero `UnityEngine.Object` access on background threads.
- `Step()` must default to `synchronous = true` to preserve compatibility with existing EditMode test suites.

---

### Task 1: CancellationToken Plumbing in Triangle Classification Pipeline

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/ClassificationCancellationTests.cs`

- [ ] **Step 1: Write unit tests for cancellation in classification**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/ClassificationCancellationTests.cs`:
- Test that passing an already cancelled `CancellationToken` to `RuntimeStateResolution.ClassifyRuntimeStates` immediately throws `OperationCanceledException`.
- Test that passing `default(CancellationToken)` completes normally and produces identical classification results.

- [ ] **Step 2: Add CancellationToken to RuntimeStateResolution and UnityRendererAlphaAnalysis**

In `Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs`:
- Update `ClassifyRuntimeStates`:
  ```csharp
  internal static MeshSeparationPlan ClassifyRuntimeStates(
      UnityRendererAlphaSnapshot snapshot,
      IReadOnlyList<SlotResolutionResult> slotResults,
      CancellationToken cancellationToken = default)
  ```
- Forward `cancellationToken` into `IntersectResolvedOutcomes`.

In `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`:
- Update `Classify`:
  ```csharp
  internal static TriangleAlphaOutcome[] Classify(
      IReadOnlyList<int> indices,
      IReadOnlyList<Vector3> positions,
      IReadOnlyList<Vector2> uv,
      AlphaResolution resolution,
      IReadOnlyList<IReadOnlyList<Vector2>> extraUvSets,
      CancellationToken cancellationToken = default)
  ```
- In the triangle evaluation loop:
  ```csharp
  for (var triangle = 0; triangle < outcomes.Length; triangle++)
  {
      if ((triangle & 1023) == 0 && cancellationToken.IsCancellationRequested)
      {
          cancellationToken.ThrowIfCancellationRequested();
      }
      ...
  }
  ```

- [ ] **Step 3: Run EditMode tests and verify PASS**

Run EditMode tests for `ClassificationCancellationTests` and `RuntimeStateResolutionTests`. Verify all pass.

---

### Task 2: Background Task State Machine in AlphaSeparationDryRun

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

- [ ] **Step 1: Write unit tests for async state machine and cancellation in DryRun**

In `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`:
- Test that `Step(synchronous: true)` executes to completion on a fixture avatar.
- Test that `Step(synchronous: false)` transitions through `Idle` -> `ClassifyingBackground` -> `Idle`.
- Test that `Cancel()` aborts an in-flight run and sets `IsCancelled` to `true`.
- Test that `PumpAll()` completes multi-renderer avatars synchronously.

- [ ] **Step 2: Implement DryRunState, private context fields, and async Step() in AlphaSeparationDryRun**

In `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`:
- Add enum:
  ```csharp
  internal enum DryRunState
  {
      Idle,
      ClassifyingBackground,
      Completed
  }
  ```
- Add properties:
  ```csharp
  internal DryRunState State => _state;
  internal bool IsCancelled { get; private set; }
  internal int TotalRendererCount { get; private set; }
  internal int CurrentRendererIndex => _rendererIndex;
  ```
- Cache `TotalRendererCount` in `Begin()`:
  ```csharp
  var renderers = _avatarRoot.GetComponentsInChildren<Renderer>(true);
  TotalRendererCount = renderers.Length;
  ```
- Add private fields on `AlphaSeparationDryRun`:
  ```csharp
  private DryRunState _state = DryRunState.Idle;
  private CancellationTokenSource _cts;
  private Task<MeshSeparationPlan> _inFlightTask;
  private Renderer _inFlightRenderer;
  private string _inFlightRendererPath;
  private string _inFlightRendererTypeName;
  private string _inFlightRendererName;
  private IReadOnlyList<Material> _inFlightAdmittedLive;
  private IReadOnlyList<BlockStateEntry> _inFlightBlockState;
  private CapturedAnimationEvidence _inFlightEvidence;
  private RuntimeStateResolution.ResolvedRuntimeStates _inFlightResolved;
  private IReadOnlyList<CapturedMaterialSlotEvidence> _inFlightSlots;
  private string _inFlightRendererKey;
  private string _inFlightCompositeKey;
  private bool _inFlightRefreshOnly;
  ```
- Implement unified `Step(bool synchronous = true)`:
  - If `_state == DryRunState.Idle`:
    - Checks completion.
    - Runs Stage A capture.
    - If cache hit or refusal: emits rows, advances `_rendererIndex`, returns `true`.
    - If proceeding: creates `_cts = new CancellationTokenSource()`, dispatches `Task.Run` for Stage B, stores in-flight fields, sets `_state = DryRunState.ClassifyingBackground`.
    - If `!synchronous`: returns `true` immediately.
  - If `_state == DryRunState.ClassifyingBackground`:
    - If `synchronous && !_inFlightTask.IsCompleted`:
      - Waits on `_inFlightTask` in a try-catch block suppressing `AggregateException`.
    - If `!_inFlightTask.IsCompleted`: returns `true`.
    - If `_inFlightTask.IsCompleted`:
      - If `_inFlightTask.IsCanceled`: clears in-flight fields, sets `_state = DryRunState.Completed`, sets `Done = true`, returns `false`.
      - If `_inFlightTask.IsFaulted`: logs exception, emits `MaterialTransformationRow.Errored` row, clears fields, advances `_rendererIndex`, sets `_state = DryRunState.Idle`, calls `RepaintRequested?.Invoke()`, returns `true`.
      - If successful: executes Stage C in try-catch on `_inFlightTask.Result`, emits rows, clears fields, advances `_rendererIndex`, sets `_state = DryRunState.Idle`, calls `RepaintRequested?.Invoke()`, returns `true`.
- Implement `Cancel(int timeoutMs = 0)`:
  - Cancels `_cts`.
  - If `timeoutMs > 0 && _inFlightTask != null && !_inFlightTask.IsCompleted`, waits up to `timeoutMs` in try-catch.
  - Clears in-flight fields, sets `_state = DryRunState.Completed`, sets `Done = true`, sets `IsCancelled = true`, invokes `RepaintRequested?.Invoke()`.
- Update `PumpAll()`:
  - Runs `while (!Done && Step(synchronous: true)) { }`.
- [ ] **Step 3: Run EditMode tests and verify PASS**

Run EditMode tests for `AlphaSeparationDryRunTests`. Verify all existing and new tests pass.

---

### Task 3: Inspector Lifecycle Wiring, Non-Blocking Stepping, and Cancel UI

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

- [ ] **Step 1: Write unit tests for inspector cancellation and lifecycle in AmuseAvatarOptimizerEditorTests**

In `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`:
- Test that when `_dryRun.IsCancelled` is true, the inspector displays the cancelled message and a Compute button.
- Test that clicking Compute resets `_dryRun` and begins a fresh run.
- Test that `OnEditorUpdate` calls `Step(synchronous: false)` without throwing exceptions.

- [ ] **Step 2: Wire lifecycle events, RequestHostRepaint, and Cancel UI in AmuseAvatarOptimizerEditor**

In `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`:
- In `OnEnable()`:
  ```csharp
  AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
  EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
  EditorApplication.update += OnEditorUpdate;
  ```
- In `OnDisable()`:
  ```csharp
  AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
  EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
  EditorApplication.update -= OnEditorUpdate;
  _dryRun?.Cancel(timeoutMs: 200);
  _dryRun = null;
  ```
- Implement `OnBeforeAssemblyReload`:
  ```csharp
  private void OnBeforeAssemblyReload()
  {
      _dryRun?.Cancel(timeoutMs: 200);
      _dryRun = null;
  }
  ```
- Implement `OnPlayModeStateChanged`:
  ```csharp
  private void OnPlayModeStateChanged(PlayModeStateChange stateChange)
  {
      if (stateChange == PlayModeStateChange.ExitingEditMode)
      {
          _dryRun?.Cancel(timeoutMs: 100);
          _dryRun = null;
      }
  }
  ```
- In `OnEditorUpdate()`:
  - If `_dryRun != null && !_dryRun.Done`:
    ```csharp
    _dryRun.Step(synchronous: false);
    if (_dryRun.Done)
    {
        RequestHostRepaint();
    }
    ```
- In `DrawMaterialTransformations()`:
  - Check cancelled state:
    ```csharp
    if (_dryRun != null && _dryRun.IsCancelled)
    {
        EditorGUILayout.HelpBox("AMUSE material transformation analysis was cancelled.", MessageType.Info);
        OnHelpBoxDrawnForTest?.Invoke("AMUSE material transformation analysis was cancelled.", MessageType.Info);
        if (GUILayout.Button("Compute", GUILayout.Width(80)))
        {
            _dryRun = null;
            _dryRunCompletedLatched = false;
        }
        return;
    }
    ```
  - While computing (`!_dryRunCompletedLatched`):
    ```csharp
    var text = _dryRun != null && _dryRun.TotalRendererCount > 0
        ? $"AMUSE computes material transformations: renderer {_dryRun.CurrentRendererIndex + 1} of {_dryRun.TotalRendererCount}."
        : "AMUSE computes material transformations.";
    EditorGUILayout.HelpBox(text, MessageType.Info);
    OnHelpBoxDrawnForTest?.Invoke(text, MessageType.Info);
    if (GUILayout.Button("Cancel", GUILayout.Width(60)))
    {
        _dryRun?.Cancel();
        GUIUtility.ExitGUI();
    }
    return;
    ```
- [ ] **Step 1: Run all product EditMode tests in Alrauna.Amuse.Tests.Editor**
- [ ] **Step 2: Run research EditMode tests in Alrauna.Amuse.Research.Tests.Editor**
- [ ] **Step 3: Run git diff --check for whitespace**
- [ ] **Step 4: Sweep changed files for identifiers (ports, hashes, machine paths, user names)**
