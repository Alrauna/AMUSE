# Show Material Transformations Post-Play-Mode Build Callbacks Implementation Plan

Date: 2026-10-15

Base commit: `190f18bcab2f58380bef416b3b90de319760cad9` on branch `feat/show-material-transformation-build-callbacks`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Display the target mesh renderer and source material in the Show Material Transformations foldout after Play mode builds when objects are moved or modified by upstream build callbacks.

**Architecture:** Capture original scene hierarchy paths from NDMF `ObjectRegistry` into presentation records (`SerializedTransformationRow.rendererPath`) while preserving build clone paths for internal pipeline checks (`PreparedAlphaSeparation.RendererPath`). Resolve cloned materials back to project assets via `RegisteredSourceIdentity.Resolve`. Update `AlphaSeparationApply.cs` to match applied triangles by `(path, slotIndex)` tuples. Enhance `AmuseAvatarOptimizerEditor.DrawTransformationRow` to fall back to `SourceMaterialPath` when scene materials differ or are null.

**Tech Stack:** C#, Unity 2022.3 APIs, NDMF 1.14.4 API (`ObjectRegistry`), NUnit.

**Spec:** `docs/superpowers/specs/2026-10-15-material-transformations-build-callbacks-design.md`

## Global Constraints

- Simple English / ASD-STE100 rules bind all prose: short active sentences, no contractions, no semicolons, no em-dashes.
- Privacy rules: No private identifiers, machine roles only (the Census Lab editor instance, the dev editor instance), private test avatars characterized by role, counts replaced with ranges.
- Do not commit machine-specific paths, credentials, or private assets.
- Ponytail ladder: Reuse existing code (`RegisteredSourceIdentity.Resolve`, NDMF `ObjectRegistry`), no unrequested abstractions, minimal working diffs.
- Internal separation records (`PreparedAlphaSeparation.RendererPath`) must remain the build clone path.
- All `ObjectRegistry` queries must pass `create: false`.

---

### Task 1: Store Surviving Candidates on Build State for Slot Matching

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:210-215`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:580-595`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Produces: `AmusePlatformFinishState.SeparationSurvivingCandidates` (`List<List<PreparedSlotSeparation>>`).
- Consumes: `rendererSurvivors` from `AlphaSeparationApply.PrepareSurvivingSet`.

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`:
Add a test verifying that `PrepareSurvivingSet` stores `rendererSurvivors` into `state.SeparationSurvivingCandidates`:

```csharp
[Test]
public void PrepareSurvivingSetStoresSurvivingCandidatesOnState()
{
    var state = new AmusePlatformFinishState();
    var list = new List<List<PreparedSlotSeparation>>();
    state.SeparationSurvivingCandidates = list;
    Assert.That(state.SeparationSurvivingCandidates, Is.SameAs(list));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run test filter: `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests.PrepareSurvivingSetStoresSurvivingCandidatesOnState`
Expected: FAIL with compilation error: `'AmusePlatformFinishState' does not contain a definition for 'SeparationSurvivingCandidates'`.

- [ ] **Step 3: Write minimal implementation**

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`:
In `AmusePlatformFinishState` class (around line 215), add:
```csharp
internal List<List<PreparedSlotSeparation>> SeparationSurvivingCandidates { get; set; }
```

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`:
In `PrepareSurvivingSet`, right before creating `finalization = new AlphaSeparationFinalization(writes);` (around line 588), assign:
```csharp
state.SeparationSurvivingCandidates = rendererSurvivors;
```

- [ ] **Step 4: Run test to verify it passes**

Run test filter: `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs
git commit -m "feat: store surviving separation candidates on build state"
```

---

### Task 2: Record Scene Paths and Resolved Materials in `AmusePlatformFinishPlugin.cs`

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:1375-1545`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs`

**Interfaces:**
- Changes visibility: `RecordPlayModeRendererRefusal` and `RecordPlayModeSlots` become `internal static`.
- Consumes: `(ObjectRegistry.ActiveRegistry as IObjectRegistry)?.GetReference(renderer, false)?.Path`
- Consumes: `RegisteredSourceIdentity.Resolve(mat) as Material ?? mat`
- Produces: `SerializedTransformationRow` with scene path in `rendererPath` and resolved asset path in `sourceMaterialPath`.

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs`:
Add a roundtrip serialization test verifying that scene paths and material paths serialize and deserialize intact:

```csharp
[Test]
public void SerializedTransformationRowRoundtripsScenePathAndMaterialPath()
{
    var record = new PlayModeBuildRecord
    {
        avatarKey = "Scene::Avatar",
        policyFingerprint = "fp1",
        avatarRows = Array.Empty<SerializedTransformationRow>(),
        rendererRows = new[]
        {
            new SerializedTransformationRow
            {
                kind = 3,
                rendererPath = "SceneRoot/Accessory/Mesh",
                rendererTypeName = "UnityEngine.SkinnedMeshRenderer",
                sourceMaterialPath = "Assets/Materials/Accessory.mat",
                slotIndex = 0,
                movedTriangles = 10,
                provenTriangles = 10,
                totalTriangles = 20,
                sentence = "Splittable: 10 of out 20 polygons may be moved"
            }
        }
    };

    PlayModeBuildStore.Save(record);
    var loaded = PlayModeBuildStore.TryLoad("Scene::Avatar", "fp1");
    Assert.That(loaded, Is.Not.Null);
    Assert.That(loaded.rendererRows.Length, Is.EqualTo(1));
    Assert.That(loaded.rendererRows[0].rendererPath, Is.EqualTo("SceneRoot/Accessory/Mesh"));
    Assert.That(loaded.rendererRows[0].sourceMaterialPath, Is.EqualTo("Assets/Materials/Accessory.mat"));
}
```

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`:
Add a test verifying that `RecordPlayModeRendererRefusal` records the scene path from `ObjectRegistry` and resolves cloned materials:

```csharp
[Test]
public void RecordPlayModeRendererRefusalUsesScenePathAndResolvesMaterial()
{
    var state = new AmusePlatformFinishState();
    var sceneRoot = new GameObject("SceneRoot");
    const string testMatPath = "Assets/AmuseTests_RefusalTestMat.mat";

    try
    {
        var sceneChild = new GameObject("MyRenderer");
        sceneChild.transform.SetParent(sceneRoot.transform, false);
        var sourceMat = new Material(Shader.Find("Standard"));
        AssetDatabase.CreateAsset(sourceMat, testMatPath);

        var registry = new nadena.dev.ndmf.ObjectRegistry(sceneRoot.transform);
        using (new nadena.dev.ndmf.ObjectRegistryScope(registry))
        {
            var mr = sceneChild.AddComponent<MeshRenderer>();
            var cloneMat = new Material(sourceMat);
            mr.sharedMaterials = new[] { cloneMat };

            ((nadena.dev.ndmf.IObjectRegistry)registry).GetReference(mr, true);
            ((nadena.dev.ndmf.IObjectRegistry)registry).RegisterReplacedObject(sourceMat, cloneMat);

            AmusePlatformFinishPass.RecordPlayModeRendererRefusal(
                state, mr, "Armature/Hips/MyRenderer", typeof(MeshRenderer).FullName,
                RendererAnalysisRefusal.UnsupportedRendererType, 1, 1, null);

            Assert.That(state.PlayModeRendererRows.Count, Is.EqualTo(2));
            Assert.That(state.PlayModeRendererRows[0].rendererPath, Is.EqualTo("MyRenderer"));
            Assert.That(state.PlayModeRendererRows[1].rendererPath, Is.EqualTo("MyRenderer"));
            Assert.That(state.PlayModeRendererRows[1].sourceMaterialPath, Is.EqualTo(testMatPath));
        }
    }
    finally
    {
        AssetDatabase.DeleteAsset(testMatPath);
        UnityEngine.Object.DestroyImmediate(sceneRoot);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run test filter: `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.RecordPlayModeRendererRefusalUsesScenePathAndResolvesMaterial`
Expected: FAIL with compilation error: `AmusePlatformFinishPass.RecordPlayModeRendererRefusal is inaccessible due to its protection level`.

- [ ] **Step 3: Write minimal implementation**

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`:
Make `RecordPlayModeRendererRefusal` and `RecordPlayModeSlots` `internal static`.
Use `var effectivePath = scenePath != null ? scenePath : rendererPath;` so empty scene paths (`""`) for root renderers are preserved.
Keep `var key = AmuseReportStrings.RendererKey(refusal);`.

```csharp
internal static void RecordPlayModeRendererRefusal(
    AmusePlatformFinishState state,
    Renderer renderer,
    string rendererPath,
    string rendererTypeName,
    RendererAnalysisRefusal refusal,
    int meshSubMeshCount,
    int materialSlotCount,
    string detail)
{
    var scenePath = renderer != null
        ? (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
            .GetReference(renderer, false)?.Path
        : null;
    var effectivePath = scenePath != null ? scenePath : rendererPath;

    var key = AmuseReportStrings.RendererKey(refusal);
    var refusalSentence = AmuseReportStrings.Has(key)
        ? AmuseReportStrings.Format(
            key,
            AmuseReports.SlotCountPhrase(meshSubMeshCount),
            AmuseReports.SlotCountPhrase(materialSlotCount),
            renderer != null ? renderer.gameObject.name : "",
            string.IsNullOrEmpty(detail) ? "" : detail)
        : refusal.ToString();

    state.PlayModeRendererRows.Add(new SerializedTransformationRow
    {
        kind = 2,
        rendererPath = effectivePath,
        rendererTypeName = rendererTypeName,
        sentence = effectivePath + " (" + rendererTypeName + "): refused. " + refusalSentence
    });

    var materials = renderer != null ? renderer.sharedMaterials : null;
    var slotCount = materials != null ? materials.Length : (materialSlotCount > 0 ? materialSlotCount : 0);
    for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
    {
        var mat = materials != null && slotIndex < materials.Length ? materials[slotIndex] : null;
        var sourceMat = mat != null ? RegisteredSourceIdentity.Resolve(mat) as Material ?? mat : null;
        var sourcePath = sourceMat != null ? AssetDatabase.GetAssetPath(sourceMat) : string.Empty;
        state.PlayModeRendererRows.Add(new SerializedTransformationRow
        {
            kind = 3,
            rendererPath = effectivePath,
            rendererTypeName = rendererTypeName,
            sourceMaterialPath = sourcePath,
            slotIndex = slotIndex,
            movedTriangles = -1,
            provenTriangles = -1,
            totalTriangles = -1,
            sentence = "Refused: AMUSE currently does not support this material"
        });
    }
}

internal static void RecordPlayModeSlots(
    AmusePlatformFinishState state,
    Renderer renderer,
    string rendererPath,
    string rendererTypeName,
    MeshSeparationPlan plan,
    IReadOnlyList<SlotResolutionResult> slotResults,
    UnityRendererAlphaSnapshot snapshot)
{
    var scenePath = renderer != null
        ? (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
            .GetReference(renderer, false)?.Path
        : null;
    var effectivePath = scenePath != null ? scenePath : rendererPath;

    var materials = renderer != null ? renderer.sharedMaterials : null;
    var submeshesByBinding = new Dictionary<int, List<SubmeshSeparationPlan>>();
    foreach (var submesh in plan.Submeshes)
    {
        if (!submeshesByBinding.TryGetValue(submesh.SourceMaterialBindingIndex, out var list))
        {
            list = new List<SubmeshSeparationPlan>();
            submeshesByBinding.Add(submesh.SourceMaterialBindingIndex, list);
        }
        list.Add(submesh);
    }

    var slotCount = materials != null ? materials.Length : (slotResults != null ? slotResults.Count : 0);
    for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
    {
        var mat = materials != null && slotIndex < materials.Length ? materials[slotIndex] : null;
        var sourceMat = mat != null ? RegisteredSourceIdentity.Resolve(mat) as Material ?? mat : null;
        var sourcePath = sourceMat != null ? AssetDatabase.GetAssetPath(sourceMat) : string.Empty;

        if (slotResults != null && slotIndex < slotResults.Count && !slotResults[slotIndex].IsResolved)
        {
            var total = SubmeshPolygonCount(renderer, slotIndex);
            state.PlayModeRendererRows.Add(new SerializedTransformationRow
            {
                kind = 3,
                rendererPath = effectivePath,
                rendererTypeName = rendererTypeName,
                sourceMaterialPath = sourcePath,
                slotIndex = slotIndex,
                movedTriangles = -1,
                provenTriangles = -1,
                totalTriangles = total,
                sentence = "Refused: AMUSE currently does not support this material"
            });
            continue;
        }

        if (submeshesByBinding.TryGetValue(slotIndex, out var submeshes))
        {
            var proven = 0;
            var total = 0;
            var isCandidate = false;
            foreach (var s in submeshes)
            {
                proven += s.OpaqueTriangleOrdinals.Count;
                total += s.OpaqueTriangleOrdinals.Count + s.TransparentTriangleOrdinals.Count;
                if (s.Disposition == SubmeshSeparationDisposition.WhollyOpaqueCandidate ||
                    s.Disposition == SubmeshSeparationDisposition.Split)
                {
                    isCandidate = true;
                }
            }

            string sentence;
            if (isCandidate)
            {
                if (total > 0 && proven == total)
                {
                    sentence = "Convertable: " + total + " out of " + total + " polygons may be moved";
                }
                else if (proven > 0)
                {
                    sentence = "Splittable: " + proven + " of out " + total + " polygons may be moved";
                }
                else
                {
                    sentence = "Untouchable: 0 of out " + total + " polygons may be moved";
                }
            }
            else
            {
                sentence = "Untouchable: 0 of out " + total + " polygons may be moved";
            }

            state.PlayModeRendererRows.Add(new SerializedTransformationRow
            {
                kind = 3,
                rendererPath = effectivePath,
                rendererTypeName = rendererTypeName,
                sourceMaterialPath = sourcePath,
                slotIndex = slotIndex,
                movedTriangles = 0,
                provenTriangles = proven,
                totalTriangles = total,
                sentence = sentence
            });
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run test filter: `Alrauna.Amuse.Tests.Editor.Build.PlayModeBuildStoreTests` and `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs
git commit -m "feat: record scene paths and resolved materials in play mode rows"
```

---

### Task 3: Slot-Level Applied Matching and Fallback Updates in `AlphaSeparationApply.cs`

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:1050-1155`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Produces: `internal static void UpdateAppliedMovedTriangles(AmusePlatformFinishState state, HashSet<(string path, int slot)> appliedSlots)`
- Produces: `internal static HashSet<(string path, int slot)> CollectAppliedSlots(AmusePlatformFinishState state)`
- Consumes: `state.SeparationSurvivingCandidates`, `state.AppliedFinalization`
- Produces: Accurate `movedTriangles` in `state.PlayModeRendererRows` for moved renderers and multi-slot renderers.

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`:
Add a test verifying that `UpdateAppliedMovedTriangles` updates `movedTriangles` only for slots in `appliedSlots`:

```csharp
[Test]
public void UpdateAppliedMovedTrianglesUpdatesOnlyMatchingSlots()
{
    var state = new AmusePlatformFinishState();
    state.PlayModeRendererRows.Add(new SerializedTransformationRow
    {
        kind = 3,
        rendererPath = "SceneRoot/Renderer",
        slotIndex = 0,
        movedTriangles = 0,
        provenTriangles = 100,
        totalTriangles = 100
    });
    state.PlayModeRendererRows.Add(new SerializedTransformationRow
    {
        kind = 3,
        rendererPath = "SceneRoot/Renderer",
        slotIndex = 1,
        movedTriangles = 0,
        provenTriangles = 50,
        totalTriangles = 50
    });

    var appliedSlots = new HashSet<(string path, int slot)>
    {
        ("SceneRoot/Renderer", 0)
    };

    AlphaSeparationApply.UpdateAppliedMovedTriangles(state, appliedSlots);

    Assert.That(state.PlayModeRendererRows[0].movedTriangles, Is.EqualTo(100));
    Assert.That(state.PlayModeRendererRows[1].movedTriangles, Is.EqualTo(0));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run test filter: `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests.UpdateAppliedMovedTrianglesUpdatesOnlyMatchingSlots`
Expected: FAIL with compilation error: `'AlphaSeparationApply' does not contain a definition for 'UpdateAppliedMovedTriangles'`.

- [ ] **Step 3: Write minimal implementation**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`:
Add `CollectAppliedSlots` and `UpdateAppliedMovedTriangles`, and update `CaptureAndSavePlayModeBuildRecord`:

```csharp
internal static HashSet<(string path, int slot)> CollectAppliedSlots(
    AmusePlatformFinishState state)
{
    var appliedSlots = new HashSet<(string path, int slot)>();
    var finalization = state?.AppliedFinalization;
    if (finalization != null && state.Separation != null)
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
                if (scenePath != null)
                {
                    appliedSlots.Add((scenePath, slotIdx));
                }
                if (buildPath != null)
                {
                    appliedSlots.Add((buildPath, slotIdx));
                }
            }
        }
    }
    return appliedSlots;
}

internal static void UpdateAppliedMovedTriangles(
    AmusePlatformFinishState state,
    HashSet<(string path, int slot)> appliedSlots)
{
    if (state == null || appliedSlots == null) return;
    for (var i = 0; i < state.PlayModeRendererRows.Count; i++)
    {
        var row = state.PlayModeRendererRows[i];
        if (row.kind == 3 && row.provenTriangles > 0)
        {
            if (appliedSlots.Contains((row.rendererPath, row.slotIndex)))
            {
                row.movedTriangles = row.provenTriangles;
                state.PlayModeRendererRows[i] = row;
            }
        }
    }
}

internal static void CaptureAndSavePlayModeBuildRecord(
    BuildContext context,
    AmusePlatformFinishState state)
{
    if (context == null || state == null) return;

    if (state.Separation != null)
    {
        var appliedSlots = CollectAppliedSlots(state);

        if (state.PlayModeRendererRows.Count > 0)
        {
            UpdateAppliedMovedTriangles(state, appliedSlots);
        }
        else
        {
            foreach (var prepared in state.Separation.Renderers)
            {
                var renderer = prepared.Target.Renderer;
                var scenePath = renderer != null
                    ? (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
                        .GetReference(renderer, false)?.Path
                    : null;
                var effectivePath = scenePath != null ? scenePath : prepared.RendererPath;
                var rendererTypeName = prepared.RendererTypeName;
                var materials = renderer != null ? renderer.sharedMaterials : null;

                foreach (var candidate in prepared.CandidateSlots)
                {
                    var slotIndex = candidate.Plan.SourceMaterialBindingIndex;
                    var proven = candidate.Plan.OpaqueTriangleOrdinals.Count;
                    var total = candidate.Plan.OpaqueTriangleOrdinals.Count +
                        candidate.Plan.TransparentTriangleOrdinals.Count;
                    var isApplied = appliedSlots.Contains((effectivePath, slotIndex));
                    var moved = isApplied ? proven : 0;

                    string sourcePath = string.Empty;
                    foreach (var sourceMat in candidate.OpaqueOfAdmitted.Keys)
                    {
                        var resolvedSource = sourceMat != null
                            ? RegisteredSourceIdentity.Resolve(sourceMat) as Material ?? sourceMat
                            : null;
                        sourcePath = resolvedSource != null ? AssetDatabase.GetAssetPath(resolvedSource) : string.Empty;
                        if (!string.IsNullOrEmpty(sourcePath)) break;
                    }
                    if (string.IsNullOrEmpty(sourcePath) && materials != null &&
                        slotIndex < materials.Length && materials[slotIndex] != null)
                    {
                        var rawMat = materials[slotIndex];
                        var resolvedMat = rawMat != null
                            ? RegisteredSourceIdentity.Resolve(rawMat) as Material ?? rawMat
                            : null;
                        sourcePath = resolvedMat != null ? AssetDatabase.GetAssetPath(resolvedMat) : string.Empty;
                    }

                    string sentence;
                    if (total > 0 && proven == total)
                    {
                        sentence = "Convertable: " + total + " out of " + total + " polygons may be moved";
                    }
                    else if (proven > 0)
                    {
                        sentence = "Splittable: " + proven + " of out " + total + " polygons may be moved";
                    }
                    else
                    {
                        sentence = "Untouchable: 0 of out " + total + " polygons may be moved";
                    }

                    state.PlayModeRendererRows.Add(new SerializedTransformationRow
                    {
                        kind = 3,
                        rendererPath = effectivePath,
                        rendererTypeName = rendererTypeName,
                        sourceMaterialPath = sourcePath,
                        slotIndex = slotIndex,
                        movedTriangles = moved,
                        provenTriangles = proven,
                        totalTriangles = total,
                        sentence = sentence
                    });
                }
            }
        }
    }

    PlayModeBuildStore.SaveOutcome(context, state);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run test filter: `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs
git commit -m "feat: match applied slots by path and index in play mode build records"
```

---

### Task 4: Material Fallback and Test Seam in `AmuseAvatarOptimizerEditor.DrawTransformationRow`

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:34-40, 330-410`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**
- Produces: `AmuseAvatarOptimizerEditor.OnMaterialObjectFieldDrawnForTest`
- Consumes: `MaterialTransformationRow.RendererPath`, `MaterialTransformationRow.SourceMaterialPath`
- Produces: Correct side-by-side rendering of `Renderer` and `Material` fields in Edit mode after Play mode builds.

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`:
Add tests observing `OnMaterialObjectFieldDrawnForTest`:

```csharp
[Test]
public void DrawTransformationRowFallsBackToSourceMaterialPathWhenSlotMaterialNull()
{
    var go = new GameObject("TestAvatar");
    var child = new GameObject("RendererObj");
    child.transform.SetParent(go.transform, false);
    var mr = child.AddComponent<MeshRenderer>();
    mr.sharedMaterials = new Material[1]; // slot 0 is null

    const string fallbackMatPath = "Assets/AmuseTests_FallbackMat.mat";
    var fallbackMat = new Material(Shader.Find("Standard"));
    AssetDatabase.CreateAsset(fallbackMat, fallbackMatPath);

    UnityEngine.Object drawnMaterial = null;
    Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnMaterialObjectFieldDrawnForTest =
        (obj, type, allowScene) => drawnMaterial = obj;

    try
    {
        var row = MaterialTransformationRow.Slot(
            "RendererObj", typeof(MeshRenderer).FullName,
            0, 10, 10, "Splittable", fallbackMatPath);

        var drawMethod = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
            .GetMethod("DrawTransformationRow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        RenderInHostWindow(() =>
        {
            drawMethod.Invoke(null, new object[] { row, go.transform });
        });

        Assert.That(drawnMaterial, Is.SameAs(fallbackMat));
    }
    finally
    {
        Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnMaterialObjectFieldDrawnForTest = null;
        AssetDatabase.DeleteAsset(fallbackMatPath);
        UnityEngine.Object.DestroyImmediate(go);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run test filter: `Alrauna.Amuse.Tests.Editor.AmuseAvatarOptimizerEditorTests.DrawTransformationRowFallsBackToSourceMaterialPathWhenSlotMaterialNull`
Expected: FAIL with compilation error: `'AmuseAvatarOptimizerEditor' does not contain a definition for 'OnMaterialObjectFieldDrawnForTest'`.

- [ ] **Step 3: Write minimal implementation**

In `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`:
Add test hook delegate around line 34:
```csharp
internal static Action<UnityEngine.Object, Type, bool> OnMaterialObjectFieldDrawnForTest;
```

In `DrawTransformationRow`, case `MaterialTransformationRowKind.Slot`:
```csharp
case MaterialTransformationRowKind.Slot:
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

    int indentOffset = EditorGUI.indentLevel * 15;
    float halfWidth = Mathf.Max(60f, (EditorGUIUtility.currentViewWidth - indentOffset) / 2f - 20f);
    using (new EditorGUILayout.HorizontalScope())
    {
        if (renderer != null)
        {
            EditorGUILayout.ObjectField(renderer, typeof(Renderer), true, GUILayout.Width(halfWidth));
            OnRendererObjectFieldDrawnForTest?.Invoke(renderer, typeof(Renderer), true);
        }
        else
        {
            var pathLabel = row.RendererPath ?? string.Empty;
            EditorGUILayout.LabelField(pathLabel, EditorStyles.boldLabel, GUILayout.Width(halfWidth));
            OnRendererLabelDrawnForTest?.Invoke(pathLabel, EditorStyles.boldLabel);
        }

        int originalIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        EditorGUILayout.ObjectField(material, typeof(Material), false);
        OnMaterialObjectFieldDrawnForTest?.Invoke(material, typeof(Material), false);
        EditorGUI.indentLevel = originalIndent;
    }

    EditorGUI.indentLevel++;
    EditorGUILayout.LabelField(row.Sentence);
    EditorGUI.indentLevel--;
    return;
```

- [ ] **Step 4: Run test to verify it passes**

Run test filter: `Alrauna.Amuse.Tests.Editor.AmuseAvatarOptimizerEditorTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs
git commit -m "feat: display resolved materials and renderers with slot fallbacks"
```

---

### Task 5: Full Test Suite Verification and Census Lab Interactive Validation

**Files:**
- Inspect: All changed files
- Test: Full EditMode test run across `Alrauna.Amuse.Tests.Editor`

- [ ] **Step 1: Run complete EditMode test suite in the dev editor instance**

Run full test suite filter: `Alrauna.Amuse.Tests.Editor`
Expected: All tests PASS with 0 failures.

- [ ] **Step 2: Run Git diff check and identifier sweep**

Run `git diff --check` to verify no whitespace errors.
Run identifier sweep to verify no private names, instance hashes, ports, or drive paths exist.

- [ ] **Step 3: Interactive verification in the Census Lab editor instance**

Switch active instance to the Census Lab editor instance.
Trigger a Play mode build on the test avatar.
Verify that the NDMF console passes with all items resolving.
Exit Play mode.
Inspect the Show Material Transformations foldout on the avatar root.
Verify that all previously broken rows now display the target `Renderer` and source `Material` side by side.

- [ ] **Step 4: Commit and finalize**

```bash
git status
```
Verify clean working tree.
