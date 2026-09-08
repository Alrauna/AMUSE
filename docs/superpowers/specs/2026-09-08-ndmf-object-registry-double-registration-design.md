# NDMF ObjectRegistry Replacement Deduplication Design

Date: 2026-09-08
Status: implemented
Scope: NDMF object registry replacement registration during alpha separation finalization.

## 1. Problem

During avatar optimization builds, AMUSE separates proven opaque triangles from transparent or cutout materials.
When two or more renderers share a source material and both separate triangles, the NDMF build fails.
The build halts with this exception in the console:

```text
System.ArgumentException: RegisterReplacedObject must be called before GetReference is called on the new object
  at nadena.dev.ndmf.ObjectRegistry.nadena.dev.ndmf.IObjectRegistry.RegisterReplacedObject (nadena.dev.ndmf.ObjectReference oldObject, UnityEngine.Object newObject) [0x00015] in ./Packages/nadena.dev.ndmf/Editor/API/ObjectRegistry.cs:243
  at nadena.dev.ndmf.ObjectRegistry.RegisterReplacedObject (UnityEngine.Object oldObject, UnityEngine.Object newObject) [0x00000] in ./Packages/nadena.dev.ndmf/Editor/API/ObjectRegistry.cs:203
  at Alrauna.Amuse.Editor.Build.AlphaSeparationApply.PrepareSurvivingSet (nadena.dev.ndmf.BuildContext context, Alrauna.Amuse.Editor.Build.AmusePlatformFinishState state, Alrauna.Amuse.Editor.Build.AlphaSeparationFinalization& finalization) [0x00709] in ./Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:384
```

This failure happens under Mip 3 or higher mip limits on avatars with shared materials.

## 2. Root Cause Analysis

### 2.1 Avatar-Wide Deduplication of Canonical Opaque Materials

In `AlphaSeparationPreparation.cs:310`, AMUSE establishes this invariant:
"The generated opaque artifact is deduplicated avatar-wide by source material."

When two renderers use the same material, AMUSE creates one canonical opaque material clone.
AMUSE shares that clone across both renderers.

### 2.2 Unchecked Registration in PrepareSurvivingSet

In `AlphaSeparationApply.cs:373-388`:

```csharp
foreach (var survivors in rendererSurvivors)
{
    foreach (var slot in survivors)
    {
        foreach (var pair in slot.OpaqueOfAdmitted)
        {
            if (ReferenceEquals(pair.Key, pair.Value))
            {
                continue;
            }

            ObjectRegistry.RegisterReplacedObject(
                pair.Key, pair.Value);
        }
    }
}
```

This method iterates through all surviving renderer slots.
When Renderer A and Renderer B share a source material:
1. Renderer A encounters `(sourceMat, opaqueClone)`. AMUSE calls `RegisterReplacedObject(sourceMat, opaqueClone)`.
2. NDMF records `_obj2ref[opaqueClone] = sourceMatRef`.
3. Renderer B encounters the same `(sourceMat, opaqueClone)`. AMUSE calls `RegisterReplacedObject(sourceMat, opaqueClone)` again.
4. NDMF checks `self.GetReference(newObject, false) != null`.
5. Because `opaqueClone` exists in `_obj2ref`, `TryRegisterReplacedObject` returns `false`.
6. NDMF throws `ArgumentException: RegisterReplacedObject must be called before GetReference is called on the new object`.

A similar duplicate registration can occur if multiple renderers share a separated mesh clone.

## 3. Proposed Design

### 3.1 Track Registered Replacement Instances

In `AlphaSeparationApply.PrepareSurvivingSet`, introduce a set to track registered replacement objects:

```csharp
var registeredReplacements = new HashSet<UnityEngine.Object>();
```

### 3.2 Guard Material Replacement Registration

Before registering a material replacement, verify that the replacement object was not registered yet:

```csharp
foreach (var survivors in rendererSurvivors)
{
    foreach (var slot in survivors)
    {
        foreach (var pair in slot.OpaqueOfAdmitted)
        {
            if (ReferenceEquals(pair.Key, pair.Value) ||
                !registeredReplacements.Add(pair.Value))
            {
                continue;
            }

            ObjectRegistry.RegisterReplacedObject(
                pair.Key, pair.Value);
        }
    }
}
```

`HashSet.Add` returns `true` on the first insertion. It returns `false` on subsequent insertions.
This guarantees that AMUSE calls `RegisterReplacedObject` exactly once per unique replacement material.

### 3.3 Guard Mesh Replacement Registration

Apply the same deduplication guard to mesh replacements in lines 390-399:

```csharp
foreach (var write in writes)
{
    if (write.Mesh == null ||
        !registeredReplacements.Add(write.Mesh))
    {
        continue;
    }

    ObjectRegistry.RegisterReplacedObject(
        expectedMeshByRenderer[write.Renderer], write.Mesh);
}
```

## 4. Test Strategy

### 4.1 Unit Test

Add a unit test in `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`:
- Set up an active NDMF build context with an active `ObjectRegistry`.
- Configure two renderers that share the same source material and the same canonical opaque clone.
- Execute `PrepareSurvivingSet`.
- Assert that `PrepareSurvivingSet` completes without throwing an exception.
- Assert that `ObjectRegistry.GetReference(opaqueClone)` resolves correctly to the source material reference.

### 4.2 Full Suite Validation

Run the full EditMode test suite through Unity Test Runner. Prove that all existing tests continue to pass.

### 4.3 Census Lab Validation

Test the avatar build with Mip 3 in the Census Lab. Confirm that the NDMF build completes without `ArgumentException`.
