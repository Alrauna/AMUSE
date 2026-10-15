# Show Material Transformations UX Phase 2 Investigation: Text Changes, Play Mode Piping, and Lazy Loading

Date: 2026-10-14

## Context and Scope

On 2026-10-14, the project owner requested three improvements to the Show Material Transformations foldout in AMUSE:
1. Update two warning and disclosure sentences to guide users to Play mode builds, using ASD-STE100 Simplified Technical English.
2. Pipe build outcome information from NDMF Play mode builds into the Show Material Transformations inspector after exiting Play mode.
3. Add lazy loading to edit-mode material transformation computations so the editor does not stall, and withhold display of transformation rows until processing completes.

This document records the findings from parallel codebase investigations and adversarial reviews.

---

## 1. Text Changes and ASD-STE100 Review

### Item 1: State Machine Behaviour Warning
- **File**: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:215-216`
- **Report Key**: `amuse.avatar.UnrecognizedStateMachineBehaviour`
- **Original Text on 2026-10-14**:
  ```csharp
  ["amuse.avatar.UnrecognizedStateMachineBehaviour"] =
      "This avatar has a state behaviour AMUSE does not know.",
  ```
- **Requested Core Change**:
  Inform the user that the avatar has an unknown state behavior and direct them to run a Play mode build for accurate information.
- **Affected Tests on 2026-10-14**:
  `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:2108` asserts the exact string:
  ```csharp
  Assert.That(reported, Has.Some.Contains("This avatar has a state behaviour AMUSE does not know"));
  ```
- **ASD-STE100 Analysis**:
  - The informal abbreviation `info` is not approved in ASD-STE100. The approved term is `information`.
  - The courtesy word `Please` is not approved in technical procedures. The imperative form `Run a Play mode build` must be used.
  - The American spelling `behavior` replaces the British spelling `behaviour`.
  - Sentences must end with a period.
- **Approved ASD-STE100 Sentence**:
  `"This avatar has a state behavior that AMUSE does not know. Run a Play mode build for accurate information."`

### Item 2: Alpha Separation Disclosure Sentence 0
- **File**: `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs:268-275`
- **Method**: `RowText.DisclosureSentence(0)`
- **Original Text on 2026-10-14**:
  `"This view runs the same proof on the avatar in the scene. Other tools change the avatar later at build time. This view cannot see those changes. A tool can also add animations the build must obey. Then the build proves less than this view shows. A run in Play mode or an upload shows the true result."`
- **Requested Core Change**:
  State that the view runs the same proof as a build on the scene avatar. Note that other tools can change the avatar during build time. Direct the user to run a Play mode build and exit for more accurate information.
- **Affected Tests on 2026-10-14**:
  `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs:124-131` asserts exact equality with the sentence.
- **ASD-STE100 Analysis**:
  - The noun `proofing` is unapproved in technical English for this context. The approved noun is `proof`.
  - The modal verb `may` is strictly forbidden in ASD-STE100. The approved modal is `can`.
  - The word `Please` is unapproved courtesy filler.
  - The informal abbreviation `info` must be `information`.
  - The casing for the Unity mode is `Play mode`.
- **Approved ASD-STE100 Sentence**:
  `"This view runs the same proof as a build on the avatar in the scene. Other tools can change the avatar during build time. Run a Play mode build and exit to see accurate information."`

---

## 2. Piping Play Mode Build Information

### Build Lifecycle in NDMF
AMUSE runs during `BuildPhase.PlatformFinish` in `AmusePlatformFinishPlugin.cs`.
During an NDMF build, the pipeline executes:
1. `AmuseStructuralGraphCheck`: Records authored controller graphs before virtualization.
2. `AmuseAnimatorBindingsCapture`: Records live animator bindings.
3. `AmusePlatformFinishPass`: Identifies `AmuseBuildPath.ApplyOnPlay` when entering Play mode. It captures evidence, evaluates alpha properties, proves triangle opacity, and builds `PreparedAlphaSeparation`.
4. `AlphaSeparationApply`: Finalizes separation decisions, writes mesh mutations, applies material slots, and emits summary reports.
5. `TransientUnlockWindowClose`: Reverts temporary unlocked materials.

### Capturing Transformation Outcomes at Build Time
`PreparedAlphaSeparation` retains only renderers with surviving candidate slots.
Renderers refused by structural graph checks, material dependency checks, or runtime state resolution do not enter `PreparedAlphaSeparation`.
To retain full reporting for the inspector:
- `AmusePlatformFinishPass` and `AlphaSeparationApply` record both surviving decisions and refusals into a serializable build outcome capture structure.
- This capture records:
  - Every analyzed renderer path and type.
  - Every slot index, source material path, disposition, and polygon counts.
  - Refusal causes for refused renderers and refused slots.
  - Avatar-level notes and banners.

### Serialization and SessionState Persistence
`UnityEditor.SessionState` stores strings, integers, and booleans across AppDomain reloads and Play mode boundaries.
`MaterialTransformationRow` contains private constructors and immutable get-only properties.
To persist the records cleanly:
1. We define a lightweight `[Serializable]` Data Transfer Object (`PlayModeBuildRecord` containing `SerializedTransformationRow[]`).
2. `PlayModeBuildRecord` serializes to a JSON string using `JsonUtility.ToJson`.
3. The JSON string is saved into `UnityEditor.SessionState.SetString("Amuse_PlayMode_" + avatarKey, json)`.
4. When returning to Edit mode, `AmuseAvatarOptimizerEditor` reads the string, deserializes with `JsonUtility.FromJson`, and maps records into `MaterialTransformationRow` instances.

### Renderer Resolution Fallback in Inspector
Upstream tools like Avatar Optimizer (AAO) can merge or rename renderers during a Play mode build.
When `AmuseAvatarOptimizerEditor.DrawTransformationRow` renders a row from Play mode, `avatarRoot.Find(row.RendererPath)` may return null.
If `renderer == null`, passing null to `EditorGUILayout.ObjectField` displays an empty `None (Renderer)` field and loses the path text.
The inspector must check: if `renderer == null`, it draws a text label with `row.RendererPath` instead of an empty `ObjectField`.
### Persistence Across Play Mode Exit
When the user exits Play mode in Unity:
- Objects in the transient Play mode scene are destroyed.
- Unity often initiates a C# AppDomain reload based on editor settings.
- Standard static in-memory fields reset to default or null.
- File writes to disk create unwanted temporary artifacts and cleanup debt.

`UnityEditor.SessionState` is Unity's official mechanism for session-scoped data.
- It survives Play mode entry and exit.
- It survives C# AppDomain reloads.
- It automatically purges when the Unity Editor closes.
- It operates entirely in memory without writing to the disk.

### Avatar Identity Across the Play Mode Boundary
In Play mode, NDMF clones the scene avatar.
The clone name typically appends `(Clone)`.
In Edit mode, the original avatar resides in the open scene.

To match the Play mode build record to the Edit mode avatar:
- We compute a normalized avatar key: `scenePath + "::" + normalizedHierarchyPath`.
- In Play mode, we strip any trailing `(Clone)` suffix from the root name when building the hierarchy path.
- In Edit mode, `AmuseAvatarOptimizerEditor` calculates the identical key from the selected avatar component.

### Cache Invalidation Rules
The cached Play mode record stores a policy fingerprint computed from:
- The active build target (`EditorUserBuildSettings.activeBuildTarget`).
- The avatar optimizer policy fields (preservation thresholds, coverage percentages, depth test settings).

The stored Play mode data is invalidated when:
1. The user clicks the **Refresh** button in the inspector.
2. The avatar optimizer policy settings change.
3. The active build target changes.
4. The Unity Editor closes.

---

## 3. Lazy Loading in Edit Mode

### Root Causes of Current Inspector Lag
1. **Synchronous Execution on Repaint**:
   `AmuseAvatarOptimizerEditor.OnInspectorGUI` currently invokes `_dryRun.Step()` directly inside `EventType.Repaint`. It immediately requests another repaint when stepping. This locks the Unity Editor UI event loop.
2. **Synchronous GPU Pipeline Stalls**:
   In `UnityAlphaFieldEvidence.TryAcquireLevelCore`, texture analysis allocates a `RenderTexture`, issues `Graphics.Blit`, and starts `AsyncGPUReadback.Request()`. It immediately calls `request.WaitForCompletion()` on the main thread. This forces a complete CPU-GPU pipeline stall of 50 to 200 milliseconds per texture.
3. **Premature Partial Rendering**:
   The inspector currently renders partial row lists while computation is still in progress. This causes visual flicker and repeated IMGUI layout calculations.

### Lazy Loading Requirements
1. **Withhold Display Until Processing Completes**:
   While computation is running, the inspector must not render partial renderer rows, slot rows, or summary notes.
   The inspector renders only a status message: `"Computing material transformations..."`.
2. **Decouple Stepping from Repaint**:
   Stepping moves out of `EventType.Repaint` in `OnInspectorGUI` and into `EditorApplication.update`.
   The editor hooks `EditorApplication.update` in `OnEnable` and unhooks in `OnDisable` to avoid delegate leaks.
   Each update tick advances at most one renderer step, preventing consecutive frame drops.
   The inspector calls `Repaint()` only when stepping updates progress or completes.
3. **Test Harness Compatibility**:
   Automated test harnesses (such as `RepaintThroughHostWindow`) drive inspector passes synchronously.
   The dry run driver retains a synchronous stepping API (`Step()` / `PumpAll()`) so tests can evaluate runs deterministically without waiting for asynchronous editor update ticks.
4. **Ponytail Simplicity**:
   Spawning background `System.Threading.Tasks.Task` worker threads is unsafe because Unity Object APIs (such as `Mesh`, `Material`, `Renderer`, and `Graphics`) can only be called from the main thread.
   Cooperative time-sliced execution on `EditorApplication.update` avoids all threading hazards and requires zero multi-threaded synchronization.
---

## 4. Adversarial Review Summary

An adversarial review was conducted across technical accuracy, policy compliance, and simplicity.

### Finding 1: Shared Report Strings Contract
`AmuseReportStrings.cs` provides localized strings for both NDMF build reports and the inspector dry run.
Changing `amuse.avatar.UnrecognizedStateMachineBehaviour` modifies NDMF console logs during full builds.
*Resolution*: The user specifically requested this text to guide avatar creators. The sentence is valid for all interactive and Play mode builds. We update both the string table and the corresponding unit test.

### Finding 2: Upstream NDMF Mesh Merging and Missing ObjectField Labels
Upstream tools like Avatar Optimizer (AAO) can merge renderers in Play mode.
A Play mode build record might reference a renderer path that does not exist as an individual renderer on the scene avatar in Edit mode.
Passing null to `EditorGUILayout.ObjectField` draws an empty field and loses the path information.
*Resolution*: Update `DrawTransformationRow` in `AmuseAvatarOptimizerEditor.cs` to render a text label containing `row.RendererPath` whenever `renderer == null`.

### Finding 3: Threading Safety and Test Pumps
Background worker threads cannot access Unity scene objects or graphics APIs without throwing exceptions.
Editor update loops must not break synchronous EditMode test harnesses.
*Resolution*: Use cooperative single-threaded stepping on `EditorApplication.update`. Expose an explicit step/pump seam so tests can advance runs synchronously.
### Finding 4: ASD-STE100 Purity
Literal user text contained informal terms (`info`, `proofing`, `Please`, `may`).
*Resolution*: Adopt ASD-STE100 compliant equivalents approved by the project owner (`information`, `proof`, direct imperatives, and `can`).

---

## 5. Architectural Synthesis

The three improvements combine into a clean flow:
1. When the user opens the foldout, the editor checks `SessionState` for a Play mode build record for this avatar.
2. If a valid Play mode record exists, the inspector displays it immediately with a `"Source: Play mode build"` status banner. No Edit mode computation runs.
3. If no Play mode record exists:
   - The inspector displays `"Computing material transformations..."`.
   - Transformation rows remain hidden.
   - An `EditorApplication.update` hook steps `AlphaSeparationDryRun` incrementally.
   - When finished, the inspector paints the complete table.
4. When the user clicks **Refresh**, the editor clears `SessionState` and runs a fresh dry run.
5. All displayed texts use approved ASD-STE100 wording.
