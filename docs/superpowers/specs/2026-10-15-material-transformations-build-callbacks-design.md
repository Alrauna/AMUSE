# Show Material Transformations Post-Play-Mode Build Callbacks Design

Date: 2026-10-15

Base commit: `190f18bcab2f58380bef416b3b90de319760cad9` on branch `feat/show-material-transformation-build-callbacks`.

Investigation: `docs/superpowers/investigations/2026-10-15-material-transformations-build-callbacks-investigation.md`.

> Privacy Note: This document characterizes a private test avatar by role. Private avatar, hierarchy, renderer, and material names are omitted. Exact counts are replaced with ranges. All instance references use role names.

---

## 1. Overview

This design enables the Show Material Transformations foldout to display mesh renderers and materials after Play mode builds when other build callbacks add or move objects.

On 2026-10-15, the project owner reported that the NDMF console in the Census Lab editor instance shows the mesh renderer and material for every item, but the AMUSE inspector on the avatar root displays raw paths and missing materials.
The investigation traced this failure to three causes:
1. AMUSE records build-clone hierarchy paths into presentation records instead of scene hierarchy paths.
2. AMUSE records empty material asset paths for in-memory material clones instead of resolving original project assets.
3. `AlphaSeparationApply.cs` checks only build-clone paths at the renderer level when it updates moved triangle counts.

This design fixes these issues using existing NDMF capabilities and existing AMUSE semantics utilities.

---

## 2. Architectural Boundaries and Principles

1. **Pipeline Paths vs Presentation Paths**:
   Internal separation records (`PreparedAlphaSeparation.RendererPath`) must remain the build-clone hierarchy paths.
   The apply pass requires build-clone paths to verify that objects did not move between passes and to query the NDMF animation index.
   Only presentation records (`SerializedTransformationRow.rendererPath`) receive scene hierarchy paths.
2. **Read-Only ObjectRegistry Queries**:
   NDMF provides `(ObjectRegistry.ActiveRegistry as IObjectRegistry)?.GetReference(target, false)`.
   Code must pass `create: false` so that queries never pollute the registry or block subsequent object registrations.
3. **Ponytail Ladder Adherence**:
   The design creates no new classes or abstractions.
   The implementation reuses `RegisteredSourceIdentity.Resolve` and the NDMF `ObjectRegistry`.
   Code changes occur directly at the points of record generation, record update, and row drawing.

---

## 3. Decisions

### Decision 1: Record Scene Hierarchy Paths for Presentation Records

- **Files**:
  `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
  `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`
- **Details**:
  In `RecordPlayModeRendererRefusal` and `RecordPlayModeSlots`:
  1. Guard against null renderer:
     ```csharp
     var scenePath = renderer != null
         ? (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
             .GetReference(renderer, false)?.Path
         : null;
     var effectivePath = !string.IsNullOrEmpty(scenePath) ? scenePath : rendererPath;
     ```
  2. Use `effectivePath` when creating `SerializedTransformationRow`:
     Assign `rendererPath = effectivePath`.
  3. In `RecordPlayModeRendererRefusal`, use `effectivePath` when constructing the refusal sentence:
     `effectivePath + " (" + rendererTypeName + "): refused. " + refusalSentence`.
  4. In `AlphaSeparationApply.CaptureAndSavePlayModeBuildRecord`, update the fallback loop over `state.Separation.Renderers`:
     Query `scenePath` for each `renderer` with `create: false`.
     Assign `effectivePath` to `row.rendererPath`.
- **Justification**:
  When Play mode exits, Unity destroys the build clone.
  The scene avatar retains its original authoring hierarchy.
  Storing the scene path enables `avatarRoot.Find(row.RendererPath)` in Edit mode to find the target `Renderer`.

### Decision 2: Resolve Cloned Materials to Source Project Assets

- **Files**:
  `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
  `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`
- **Details**:
  When recording `sourceMaterialPath` for `SerializedTransformationRow`:
  1. Resolve the material using existing AMUSE semantics:
     ```csharp
     var sourceMat = mat != null
         ? RegisteredSourceIdentity.Resolve(mat) as Material ?? mat
         : null;
     ```
  2. If `sourceMat` is not null, query `AssetDatabase.GetAssetPath(sourceMat)`.
  3. Assign the resulting path to `row.sourceMaterialPath`.
### Decision 3: Update Applied Triangle Matching in `AlphaSeparationApply.cs`

- **Files**:
  `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
  `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`
- **Details**:
  1. In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`:
     In `AmusePlatformFinishState`, add:
     ```csharp
     internal List<List<PreparedSlotSeparation>> SeparationSurvivingCandidates { get; set; }
     ```
  2. In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`:
     In `PrepareSurvivingSet`, before returning the decision, store the surviving slots into state:
     ```csharp
     state.SeparationSurvivingCandidates = rendererSurvivors;
     ```
  3. In `CaptureAndSavePlayModeBuildRecord`, lines 1057 to 1092 update `row.movedTriangles = row.provenTriangles`.
     Replace coarse renderer-level matching with exact slot-level matching:
     Precompute applied slots before the row loop:
     ```csharp
     var appliedSlots = new HashSet<(string path, int slot)>();
     var finalization = state.AppliedFinalization;
     if (finalization != null)
     {
         var writesByRenderer = new HashSet<Renderer>(
             finalization.Writes.Where(w => w.Renderer != null).Select(w => w.Renderer));

         for (var rIdx = 0; rIdx < state.Separation.Renderers.Count; rIdx++)
         {
             var prepared = state.Separation.Renderers[rIdx];
             var renderer = prepared.Target.Renderer;
             if (renderer == null || !writesByRenderer.Contains(renderer)) continue;

             var scenePath = (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
                 .GetReference(renderer, false)?.Path;
             var buildPath = prepared.RendererPath;

             var survivors = state.SeparationSurvivingCandidates != null &&
                             rIdx < state.SeparationSurvivingCandidates.Count
                 ? state.SeparationSurvivingCandidates[rIdx]
                 : prepared.CandidateSlots;

             foreach (var candidate in survivors)
             {
                 var slotIdx = candidate.Plan.SourceMaterialBindingIndex;
                 if (!string.IsNullOrEmpty(scenePath))
                 {
                     appliedSlots.Add((scenePath, slotIdx));
                 }
                 if (!string.IsNullOrEmpty(buildPath))
                 {
                     appliedSlots.Add((buildPath, slotIdx));
                 }
             }
         }
     }
     ```
  4. When iterating over `state.PlayModeRendererRows`:
     ```csharp
     if (row.kind == 3 && row.provenTriangles > 0)
     {
         if (appliedSlots.Contains((row.rendererPath, row.slotIndex)))
         {
             row.movedTriangles = row.provenTriangles;
             state.PlayModeRendererRows[i] = row;
         }
     }
     ```
  5. When iterating over `state.Separation.Renderers` in the fallback branch (lines 1095 to 1150):
     Compute `isApplied` per slot instead of per renderer:
     ```csharp
     var isApplied = appliedSlots.Contains((effectivePath, slotIndex));
     var moved = isApplied ? proven : 0;
     ```
- **Justification**:
  Matching by both scene path and build clone path ensures moved renderers match correctly.
  Matching by `(path, slotIndex)` prevents multi-slot renderers from marking unseparated slots as applied.
  Precomputing paths in a set eliminates $O(N \times M)$ redundant path calculations during outcome recording.
  Updating the fallback branch ensures consistency across both recording branches.
### Decision 4: Material Fallback in `AmuseAvatarOptimizerEditor.DrawTransformationRow`

- **File**:
  `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- **Details**:
  In `DrawTransformationRow`, unify material resolution before rendering:
  ```csharp
  Renderer renderer = null;
  if (avatarRoot != null)
  {
      renderer = string.IsNullOrEmpty(row.RendererPath)
          ? avatarRoot.GetComponent<Renderer>()
          : avatarRoot.Find(row.RendererPath)?.GetComponent<Renderer>();
  }

  Material material = null;
  var materials = renderer != null ? renderer.sharedMaterials : null;
  if (materials != null && row.SlotIndex >= 0 && row.SlotIndex < materials.Length)
  {
      material = materials[row.SlotIndex];
  }

  if (!string.IsNullOrEmpty(row.SourceMaterialPath))
  {
      var currentPath = material != null ? AssetDatabase.GetAssetPath(material) : null;
      if (currentPath != row.SourceMaterialPath)
      {
          var loaded = AssetDatabase.LoadAssetAtPath<Material>(row.SourceMaterialPath);
          if (loaded != null)
          {
              material = loaded;
          }
      }
  }
  ```
  Then draw line one:
  - If `renderer != null`, draw `EditorGUILayout.ObjectField(renderer, typeof(Renderer), true, GUILayout.Width(halfWidth))`.
  - If `renderer == null`, draw `EditorGUILayout.LabelField(row.RendererPath ?? string.Empty, EditorStyles.boldLabel, GUILayout.Width(halfWidth))`.
  - Draw `EditorGUILayout.ObjectField(material, typeof(Material), false)`.
- **Justification**:
  Unifying material resolution removes duplicated branches.
  If an upstream pass reordered or added material slots on the build clone, checking `currentPath != row.SourceMaterialPath` ensures that the editor displays the analyzed source material.
  If the renderer was destroyed or only existed at build time, the material still displays next to the path label.

---

## 4. Verification Plan

### 4.1 Automated EditMode Tests

1. **`PlayModeBuildStoreTests.cs`**:
   - Verify that `SerializedTransformationRow` roundtrips scene hierarchy paths and resolved source material paths.
2. **`AmusePlatformFinishPluginTests.cs`**:
   - Create a test where an upstream pass reparents a renderer before `AmusePlatformFinishPass`.
   - Verify that `state.PlayModeRendererRows` records the scene path from `ObjectRegistry`.
   - Create a test where an upstream pass clones a material.
   - Verify that `state.PlayModeRendererRows` records the resolved asset path of the cloned material.
3. **`AlphaSeparationApplyTests.cs`**:
   - Create a test where a moved renderer separates polygons.
   - Verify that `CaptureAndSavePlayModeBuildRecord` marks the moved renderer as applied and updates `movedTriangles`.
   - Create a test with a multi-slot renderer where slot 0 converts and slot 1 refuses during apply.
   - Verify that `movedTriangles` updates only for slot 0.
4. **`AmuseAvatarOptimizerEditorTests.cs`**:
   - Verify that `DrawTransformationRow` renders both the renderer object field and material object field for a moved renderer.
   - Verify that `DrawTransformationRow` falls back to `SourceMaterialPath` when `renderer != null` but `materials[slotIndex]` does not match `SourceMaterialPath`.
   - Verify that `DrawTransformationRow` renders the material object field when `renderer == null` and `SourceMaterialPath` is valid.

### 4.2 Interactive Verification in the Census Lab Editor Instance

1. Refresh Unity in the Census Lab editor instance.
2. Trigger an avatar Play mode build on the test avatar.
3. Open the NDMF console and verify that all reported items resolve.
4. Exit Play mode.
5. Select the scene avatar root and open the Show Material Transformations foldout.
6. Verify that previously broken rows now display the target `SkinnedMeshRenderer` or `MeshRenderer` and the source `Material` side by side.
