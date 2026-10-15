# Show Material Transformations Post-Play-Mode Build Callbacks Investigation

Date: 2026-10-15

Base commit: `190f18bcab2f58380bef416b3b90de319760cad9` on branch `feat/show-material-transformation-build-callbacks`.

> Privacy Note: This document characterizes a private test avatar by role. Private avatar, hierarchy, renderer, and material names are omitted. Exact counts are replaced with ranges. All instance references use role names.

---

## 1. Context and Scope

On 2026-10-15, the project owner requested support for showing the mesh renderer and material in the Show Material Transformations foldout after Play mode builds when meshes and materials are added or moved by other build callbacks.

The project owner noticed a discrepancy between two user interfaces:
1. The NDMF console in the Census Lab editor instance shows the mesh renderer and material for every reported item.
2. The AMUSE Avatar Optimizer component on the root of the avatar in the scene does not show the mesh renderer or material for items moved or added by other build callbacks.

This investigation analyzes the cause of this discrepancy in the Census Lab editor instance and in the AMUSE codebase.
It identifies why the inspector loses object references after Play mode exits.
It determines the minimal solution that follows the ponytail skill.

---

## 2. Observations in the Census Lab Editor Instance

An inspection of the Census Lab editor instance on 2026-10-15 revealed the following facts.

### 2.1 Test Avatar Configuration

The test avatar in the scene contains an avatar root object with an `AmuseAvatarOptimizer` component.
The avatar contains multiple accessory prefabs and clothing items.
These accessory objects use modular build components from other tools.
The tools include Modular Avatar bone proxies, armature mergers, and custom script behaviors.

### 2.2 Behavior in the NDMF Console

When an avatar build runs in Play mode, NDMF executes build passes from all registered plugins.
AMUSE reports information and refusal items to NDMF through `ErrorReport.ReportError`.
AMUSE attaches the target `Renderer` component as a context object with `ErrorReport.WithContextObject(renderer)`.

In the NDMF console window after the Play mode build:
1. NDMF lists all reported messages.
2. For each message, NDMF displays an object selector button.
3. The button displays the type and name of the component, such as `[SkinnedMeshRenderer]` or `[MeshRenderer]`.
4. When a user clicks the button, the Unity Editor selects and highlights the component on the scene avatar.
5. All reported items in the NDMF console resolve successfully to objects on the scene avatar.

### 2.3 Behavior in the AMUSE Avatar Optimizer Inspector

When the user exits Play mode, Unity returns to Edit mode.
The user selects the avatar root object in the hierarchy.
The user opens the Show Material Transformations foldout on the `AmuseAvatarOptimizer` component.

The inspector displays cached results from the Play mode build through `PlayModeBuildStore.TryLoad`:
1. The foldout shows an information box that states accurate results come from the Play mode build.
2. The avatar contains between 150 and 170 recorded slot rows.
3. For more than 120 slot rows, the inspector displays a two-line layout.
   The top line displays the target renderer object field and the source material object field side by side.
   The bottom line displays the status sentence.
4. For between 20 and 30 slot rows, the inspector does not display the renderer object field or the material object field.
   The top line displays only a bold text label that contains a hierarchy path.
   The right side of the top line is completely empty.
   No material object field appears.
   The bottom line displays the status sentence.

Among these affected renderers, some renderers were proven splittable by AMUSE during the build.
For these splittable renderers, AMUSE separated polygons onto canonical opaque materials.
However, the user cannot inspect the affected mesh renderer or material in the inspector after the build.

---

## 3. Code Analysis and Root Cause

### 3.1 Root Cause 1: Build-Time Hierarchy Path Invalidation

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`:
During an avatar build, `AmusePlatformFinishPass` runs in the NDMF `PlatformFinish` phase.
It inspects all renderers under `context.AvatarRootObject`.
`context.AvatarRootObject` is a temporary build clone of the avatar.

Before AMUSE runs, upstream build callbacks execute:
1. Modular Avatar armature merging passes reparent clothing and accessory renderers under the main armature bones.
2. Bone proxy passes move GameObjects from accessory roots into bone hierarchies.
3. Upstream optimization tools rename or merge GameObjects.

AMUSE calculates the renderer path at lines 723 to 724:
```csharp
var rendererPath = AnimationUtility.CalculateTransformPath(
    renderer.transform, context.AvatarRootObject.transform);
```

This calculation measures the path inside the modified build clone.
For example, an accessory renderer located at `AccessoryRoot/Item/Mesh` on the scene avatar moves to `Armature/.../Mesh` on the build clone.
AMUSE records `Armature/.../Mesh` into `SerializedTransformationRow.rendererPath`.

When Play mode exits, Unity destroys the build clone.
The scene avatar in Edit mode retains its original authoring hierarchy.
In `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`:
`DrawTransformationRow` tries to find the renderer on the scene avatar at line 358:
```csharp
renderer = string.IsNullOrEmpty(row.RendererPath)
    ? avatarRoot.GetComponent<Renderer>()
    : avatarRoot.Find(row.RendererPath)?.GetComponent<Renderer>();
```

Because `row.RendererPath` contains the build-clone path, `avatarRoot.Find` returns `null`.
Therefore, `renderer` is `null`.

### 3.2 Root Cause 2: Empty Source Material Paths for Cloned Materials

When `renderer` is `null`, `DrawTransformationRow` executes its fallback branch at lines 382 to 394:
```csharp
var pathLabel = row.RendererPath ?? string.Empty;
EditorGUILayout.LabelField(pathLabel, EditorStyles.boldLabel, GUILayout.Width(halfWidth));
if (!string.IsNullOrEmpty(row.SourceMaterialPath))
{
    var mat = AssetDatabase.LoadAssetAtPath<Material>(row.SourceMaterialPath);
    // Draw material ObjectField
}
```

The inspector attempts to load and show the material from `row.SourceMaterialPath`.
However, in all affected rows, `row.SourceMaterialPath` is an empty string.

In `AmusePlatformFinishPlugin.cs` lines 1409 to 1410 and 1475 to 1476:
```csharp
var mat = materials != null && slotIndex < materials.Length ? materials[slotIndex] : null;
var sourcePath = mat != null ? AssetDatabase.GetAssetPath(mat) : string.Empty;
```

And in `AlphaSeparationApply.cs` line 1120:
```csharp
sourcePath = AssetDatabase.GetAssetPath(materials[slotIndex]);
```

Upstream passes (such as NDMF animator cloning and material variant generators) clone materials in memory.
When a material is an in-memory clone, `AssetDatabase.GetAssetPath(mat)` returns an empty string.
AMUSE does not resolve cloned materials to their source assets when it writes `PlayModeRendererRows`.

Because `row.SourceMaterialPath` is empty, `LoadAssetAtPath` never executes.
The right side of the row remains completely blank.

### 3.3 Root Cause 3: Coupling in `AlphaSeparationApply.cs`

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`:
Lines 1064 to 1092 update `movedTriangles` for applied renderers:
```csharp
var path = AnimationUtility.CalculateTransformPath(
    writeRenderer.transform, context.AvatarRootObject.transform);
if (path == row.rendererPath || writeRenderer.gameObject.name == row.rendererPath)
{
    isApplied = true;
    break;
}
if (isApplied)
{
    row.movedTriangles = row.provenTriangles;
    state.PlayModeRendererRows[i] = row;
}
```

`writeRenderer` is an object on the build clone.
`AnimationUtility.CalculateTransformPath` measures the path on that build clone.
If `state.PlayModeRendererRows` changes `row.rendererPath` to the scene path, `path == row.rendererPath` evaluates to `false` for any renderer that upstream passes moved.
Then `isApplied` remains `false`.
`row.movedTriangles` never updates to `row.provenTriangles`.
In Edit mode, the inspector displays zero moved triangles for every moved renderer that AMUSE converted.

Therefore, `AlphaSeparationApply.cs` must match write renderers through the resolved scene path or renderer identity before it updates `movedTriangles`.

### 3.4 How NDMF Successfully Resolves Scene Objects

In `Packages/nadena.dev.ndmf/Editor/API/BuildContext.cs`:
At the start of every build, `BuildContext` initializes an `ObjectRegistry`.
Lines 181 to 192 register all initial GameObjects and Components on the clone before any pass executes:
```csharp
using (new ObjectRegistryScope(_registry))
{
    foreach (Transform xform in _avatarRootTransform.GetComponentsInChildren<Transform>(true))
    {
        ObjectRegistry.GetReference(xform.gameObject);
        foreach (Component c in xform.gameObject.GetComponents<Component>())
        {
            ObjectRegistry.GetReference(c);
        }
    }
}
```

Because NDMF creates the build clone before passes run, the relative hierarchy on the initial clone matches the scene avatar hierarchy.
When an initial component is registered, `ObjectRegistry` stores an `ObjectReference`.
The `ObjectReference` records:
1. `_path`: the relative hierarchy path on the initial clone, matching the scene avatar.
2. `_type`: the component `Type`.
3. `_obj`: the component instance.

When upstream passes move or replace objects:
1. Moving an object does not change its existing `ObjectReference` in the registry.
2. Replacing an object through `ObjectRegistry.RegisterReplacedObject` maps the new object to the original `ObjectReference`.

When the NDMF console runs `ObjectSelector.TryCreate`, it calls `ObjectReference.TryResolve`:
```csharp
public bool TryResolve(ErrorReport report, out Object obj)
{
    // ...
    if (!report.TryResolveAvatar(out var av)) return false;
    GameObject go = av.transform.Find(_path)?.gameObject;
    if (go == null) return false;
    obj = go.GetComponent(Type);
    return obj != null;
}
```

Because `_path` is the path on the scene avatar, `av.transform.Find(_path)` finds the GameObject.
Then `go.GetComponent(Type)` retrieves the exact `Renderer` component on the scene avatar.

Furthermore, AMUSE already includes `RegisteredSourceIdentity.Resolve(obj)`.
In `Packages/com.alrauna.amuse/Editor/Semantics/RegisteredSourceIdentity.cs`:
```csharp
internal static UnityEngine.Object Resolve(UnityEngine.Object source)
{
    if (source == null) return null;
    var reference = (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
        .GetReference(source, false);
    if (reference == null) return null;
    var resolved = reference.Object;
    return resolved != null && resolved != source ? resolved : null;
}
```

When given a cloned material, `RegisteredSourceIdentity.Resolve` returns the original project material asset.

---

## 4. Architectural Findings and Ponytail Evaluation

The Ponytail skill requires the simplest, shortest solution that fixes the root cause.
We evaluate the rungs of the Ponytail ladder:

1. **Rung 1: Does this need to exist?**
   Yes. Avatar creators need to see which renderers and materials will be transformed or refused after build passes run.
2. **Rung 2: Already in this codebase?**
   Yes. AMUSE already uses `RegisteredSourceIdentity.Resolve` to map build-time objects back to scene assets.
   AMUSE already stores `SerializedTransformationRow` records.
   AMUSE already contains drawing logic for renderers and materials.
3. **Rung 5: Already-installed dependency solves it?**
   Yes. NDMF is an existing dependency.
   Read-only queries to `ObjectRegistry` provide the exact scene path recorded before passes moved the object.
   `RegisteredSourceIdentity.Resolve(mat)` provides the exact source material asset before passes cloned it.

### 4.1 Separation of Pipeline Paths and Presentation Paths

In the build pipeline, `prepared.RendererPath` in `PreparedAlphaSeparation` must remain the build clone path.
Two operational checks require the build clone path:
1. `AlphaSeparationApply.cs:255` verifies that the renderer did not move between preparation and apply passes using `liveRendererPath`.
2. `AlphaSeparationApply.cs:338` queries the NDMF animation index using the build clone path.

Therefore, do not change `prepared.RendererPath`.
Change only the presentation path stored in `SerializedTransformationRow.rendererPath`.

### 4.2 Root Cause Fix Location

Resolve the root cause where code creates and consumes build records:
1. In `AmusePlatformFinishPlugin.cs`:
   When creating `SerializedTransformationRow` in `RecordPlayModeRendererRefusal` and `RecordPlayModeSlots`:
   - Query the scene path safely without creating registry entries:
     `var scenePath = (ObjectRegistry.ActiveRegistry as IObjectRegistry)?.GetReference(renderer, false)?.Path;`
   - If `scenePath` is not empty, assign `scenePath` to `row.rendererPath`.
   - If `scenePath` is null or empty, assign `AnimationUtility.CalculateTransformPath(renderer.transform, context.AvatarRootObject.transform)`.
   - Resolve the source material:
     `var sourceMat = RegisteredSourceIdentity.Resolve(mat) as Material ?? mat;`
   - Record `AssetDatabase.GetAssetPath(sourceMat)` into `row.sourceMaterialPath`.
2. In `AlphaSeparationApply.cs`:
   In `CaptureAndSavePlayModeBuildRecord`:
   - When matching `writeRenderer` against `state.PlayModeRendererRows`:
     Compute both the build clone path and the resolved scene path for `writeRenderer`.
     Match `row.rendererPath` against either path or GameObject name.
   - When creating fallback rows from `state.Separation.Renderers`:
     Query the scene path with `(ObjectRegistry.ActiveRegistry as IObjectRegistry)?.GetReference(renderer, false)?.Path`.
     Assign the scene path to `row.rendererPath`.
     Resolve candidate materials with `RegisteredSourceIdentity.Resolve(mat) as Material ?? mat`.
     Record `AssetDatabase.GetAssetPath(sourceMat)` into `row.sourceMaterialPath`.
3. In `AmuseAvatarOptimizerEditor.cs`:
   In `DrawTransformationRow`:
   - When `renderer != null`, retrieve the material from `renderer.sharedMaterials`.
   - If `material == null` and `!string.IsNullOrEmpty(row.SourceMaterialPath)`, load the material from `AssetDatabase.LoadAssetAtPath<Material>(row.SourceMaterialPath)`.
   - If `renderer == null`, render the path label and display the material loaded from `row.SourceMaterialPath`.

---

## 5. Potential Latent Risks and Mitigations

1. **Risk: Unsafe ObjectRegistry Query Pollutes Registry**
   - *Detail*: Calling `ObjectRegistry.GetReference(renderer)` with the default `create: true` creates a new entry on unregistered objects.
     This can prevent subsequent `RegisterReplacedObject` operations.
   - *Mitigation*: Always query `ObjectRegistry` through `(ObjectRegistry.ActiveRegistry as IObjectRegistry)?.GetReference(renderer, false)`.
     With `create: false`, the query is read-only.
2. **Risk: Missing ObjectRegistry during Dry Run or Tests**
   - *Detail*: Edit-mode dry runs and isolated unit tests do not run inside an NDMF build context with an active `ObjectRegistry`.
   - *Mitigation*: When `ObjectRegistry.ActiveRegistry` is null, the code must fall back to `AnimationUtility.CalculateTransformPath`.
3. **Risk: Applied Polygons Not Counted After Scene Path Update**
   - *Detail*: If `AlphaSeparationApply.cs` does not account for scene paths, `isApplied` remains false for moved renderers.
   - *Mitigation*: `AlphaSeparationApply.cs` must check both the build clone path and the scene path when it matches `writeRenderer`.

---

## 6. Recommendations

1. Keep `prepared.RendererPath` as the build clone path to preserve pipeline invariants and animation curve remapping.
2. In `AmusePlatformFinishPlugin.cs` and `AlphaSeparationApply.cs`, store the scene path from `(ObjectRegistry.ActiveRegistry as IObjectRegistry)?.GetReference(renderer, false)?.Path` in `SerializedTransformationRow.rendererPath`.
3. In `AmusePlatformFinishPlugin.cs` and `AlphaSeparationApply.cs`, resolve materials through `RegisteredSourceIdentity.Resolve` before recording `row.sourceMaterialPath`.
4. In `AlphaSeparationApply.cs`, match `writeRenderer` against both the scene path and the build clone path.
5. In `AmuseAvatarOptimizerEditor.DrawTransformationRow`, load `row.SourceMaterialPath` when `renderer != null` but `material == null`.
6. Add focused unit tests covering scene path recording, material resolution, applied triangle matching, and inspector material fallback.
