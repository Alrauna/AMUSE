# Show Material Transformations UX: Text Changes, Play Mode Piping, and Lazy Loading — Design

Date: 2026-10-14

Base: `df5a6f5` on branch `feat/simplify-material-transformations-ux`

Investigation: `docs/superpowers/investigations/2026-10-14-material-transformations-play-mode-piping-lazy-loading-investigation.md`

## Overview

This design simplifies and accelerates the Show Material Transformations foldout on the AmuseAvatarOptimizer component.
It delivers three improvements:
1. Updates warning and disclosure texts to use approved ASD-STE100 Simplified Technical English and guide users to Play mode builds.
2. Pipes exact build transformation outcomes from NDMF Play mode builds into the inspector across the Play mode exit boundary.
3. Implements lazy loading for edit-mode transformation analysis to prevent editor stalls and withhold display until processing finishes.

---

## Decisions

### 1. ASD-STE100 Text Updates

#### State Machine Behaviour Warning
- **File**: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`
- **Key**: `amuse.avatar.UnrecognizedStateMachineBehaviour`
- **Updated Text**:
  `"This avatar has a state behavior that AMUSE does not know. Run a Play mode build for accurate information."`
- **Justification**:
  Replaces British `behaviour` with American `behavior`.
  Replaces informal abbreviation `info` with `information`.
  Replaces courtesy filler `Please` with an imperative verb.
  Provides a clear action for avatar creators.
- **Affected Tests**:
  `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:2108`.

#### Disclosure Sentence 0
- **File**: `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs`
- **Method**: `RowText.DisclosureSentence(0)`
- **Updated Text**:
  `"This view runs the same proof as a build on the avatar in the scene. Other tools can change the avatar during build time. Run a Play mode build and exit to see accurate information."`
- **Justification**:
  Replaces unapproved noun `proofing` with `proof`.
  Replaces forbidden modal `may` with `can`.
  Replaces informal abbreviation `info` with `information`.
  Replaces courtesy filler `Please` with an imperative verb.
- **Affected Tests**:
  `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs:124-131`.

---

### 2. Piping Play Mode Build Information

#### Build Capture Seam
In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`:
- AMUSE evaluates whether the active build path is Play mode: `state.Lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay`.
- This reads exclusively from `state.Lifecycle.BuildPath`. It never calls `EditorApplication.isPlayingOrWillChangePlaymode` directly, which preserves pure test seams.
- After applying writes and compiling summary counters, AMUSE captures the transformation outcome:
  1. Avatar banner rows and notes.
  2. Every analyzed renderer path and type.
  3. Every slot index, source material asset path, disposition, moved triangles, proven triangles, and total triangles.
  4. Renderer-level and slot-level refusals.
- AMUSE packages these records into a serializable Data Transfer Object (`PlayModeBuildRecord`).
- `Editor/Build/` contains no reference to `Editor/DryRun/`. The serializable DTO lives in `Editor/Build/` or a shared contracts file.

#### Persistence via SessionState
Unity destroys transient scene objects and reloads AppDomains on Play mode exit.
Disk files create unwanted project churn and cleanup debt.
AMUSE persists the build record using `UnityEditor.SessionState`:
- Stored as a JSON string under key `"Amuse_PlayMode_" + avatarKey`.
- `UnityEditor.SessionState` survives AppDomain reloads and Play mode exit.
- `UnityEditor.SessionState` automatically clears when Unity exits.
- Persistence routines (saving, loading, erasing) are encapsulated within `DryRunCache` to keep `AlphaSeparationApply` focused on build mutation.

#### Avatar Key Calculation
- In Play mode: compute `scenePath + "::" + normalizedHierarchyPath`.
- Strip any trailing `"(Clone)"` suffix from the root GameObject name.
- In Edit mode: `AmuseAvatarOptimizerEditor` calculates the identical composite key from `target`.

#### Data Transfer Object Schema
```csharp
[Serializable]
internal struct SerializedTransformationRow
{
    public int kind;
    public string rendererPath;
    public string rendererTypeName;
    public string sourceMaterialPath;
    public int slotIndex;
    public int movedTriangles;
    public int provenTriangles;
    public int totalTriangles;
    public int refusedSlots;
    public int slotCount;
    public string sentence;
}

[Serializable]
internal sealed class PlayModeBuildRecord
{
    public string avatarKey;
    public string policyFingerprint;
    public SerializedTransformationRow[] avatarRows;
    public SerializedTransformationRow[] rendererRows;
}
```

#### Inspector Consumption, Invalidation, and Error Handling
In `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`:
- When opening the foldout, the editor queries `DryRunCache` for a cached `PlayModeBuildRecord`.
- JSON deserialization wraps in a try-catch block. If deserialization fails or the record is corrupt, `DryRunCache` erases the `SessionState` key and falls back to Edit mode analysis.
- If a valid record exists and its `policyFingerprint` matches the current avatar settings:
  - It renders an informative banner: `"This view shows accurate results from the Play mode build."`
  - It converts the DTO rows into `MaterialTransformationRow` instances via an internal mapper.
  - It paints the rows immediately.
  - It bypasses Edit mode dry-run execution completely.
- If the user clicks **Refresh**:
  - The editor erases the `SessionState` key for this avatar.
  - It clears `DryRunCache`.
  - It begins a fresh Edit mode dry run.

#### Renderer and Material Fallback in Inspector
Upstream tools can merge renderers during Play mode.
When `AmuseAvatarOptimizerEditor.DrawTransformationRow` renders a row from Play mode:
- If `avatarRoot.Find(row.RendererPath)` returns `null`, the editor draws an `EditorGUILayout.LabelField(row.RendererPath)` instead of an empty `ObjectField`.
- If `renderer == null` and `row.sourceMaterialPath` exists, the editor loads and displays the source material from `AssetDatabase.LoadAssetAtPath<Material>(row.sourceMaterialPath)`.
- This ensures merged or renamed renderers and materials remain legible in the inspector.
---

### 3. Lazy Loading in Edit Mode

#### Withholding Display While Processing
- The inspector latches display state during `EventType.Layout` so control counts do not diverge during `EventType.Repaint`.
- While processing is in progress (`_dryRun != null && !_dryRun.Done`):
  - All material rows, renderer rows, and reporting notes remain hidden.
  - The inspector displays an information box: `"AMUSE computes material transformations."`
- When `_dryRun.Done` becomes true:
  - The inspector paints the completed transformation table.

#### Decoupled Stepping on EditorApplication.update
- Stepping is removed from `EventType.Repaint` inside `OnInspectorGUI`.
- `AmuseAvatarOptimizerEditor` registers an update delegate:
  - Subscribes in `OnEnable()`.
  - Unsubscribes in `OnDisable()`.
- During update:
  - If target is destroyed or null, unsubscribes and early exits.
  - If the foldout is closed or Unity is in Play mode, early exits.
  - If `_dryRun != null && !_dryRun.Done`:
    - Calls `_dryRun.Step()`.
    - If `_dryRun.Done` becomes true, calls `Repaint()`.
- This ensures single-threaded safety, prevents UI event loop locking, and eliminates editor hitches.

#### Test Harness Support
- EditMode automated tests often execute synchronously without spinning the editor update loop.
- `AlphaSeparationDryRun.Step()` remains accessible for tests.
- `AlphaSeparationDryRun.PumpAll()` executes remaining steps in a loop, enabling deterministic test assertions.

## Verification Plan

### Automated Tests
1. **Report Strings Tests**:
   - `AmusePlatformFinishPluginTests.cs`: Update assertion for `amuse.avatar.UnrecognizedStateMachineBehaviour`.
   - `MaterialTransformationRowTests.cs`: Update assertion for `RowText.DisclosureSentence(0)`.
2. **Play Mode Build Piping Tests**:
   - Add unit tests verifying `PlayModeBuildRecord` serialization and deserialization roundtrip.
   - Test avatar key normalization with and without `"(Clone)"` suffix.
   - Verify that `SessionState` records are loaded and converted into `MaterialTransformationRow` instances.
   - Verify that clicking Refresh clears the session record.
   - Verify that `DrawTransformationRow` renders a text label when the target renderer is null.
3. **Lazy Loading Tests**:
   - Verify that `AmuseAvatarOptimizerEditor` withholds rows while `_dryRun.Done` is false.
   - Verify that rows render when `_dryRun.Done` is true.
   - Verify that `PumpAll()` completes all steps synchronously.
