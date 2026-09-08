# NDMF ObjectRegistry Double Registration on Shared Materials

Date: 2026-09-08. Base: `main` at `41f1c91`.
Labels: `[SOURCE]` is a fact read in this repository or in pinned upstream code.
`[MEASURED]` is a fact produced by a test or execution in this investigation.
`[INFERENCE]` is a deduction. `[RECOMMENDATION]` is a proposed next step.

## 1. Executive summary

When a user selects Mip 3 or a higher mip limit, the NDMF build fails with
`System.ArgumentException: RegisterReplacedObject must be called before GetReference is called on the new object`. `[MEASURED]`

The failure occurs in `AlphaSeparationApply.PrepareSurvivingSet`. It occurs when
multiple renderers or slots share the same source material. `[MEASURED]` `[SOURCE]`

AMUSE deduplicates canonical opaque material clones avatar-wide by source material.
When multiple renderers separate opaque triangles from the same source material,
AMUSE assigns the same replacement material instance to every renderer. `[SOURCE]`

`PrepareSurvivingSet` loops through all surviving renderer slots. It calls
`ObjectRegistry.RegisterReplacedObject` for each slot without checking if the
replacement material was registered already. `[SOURCE]`

NDMF tracks replacements in a dictionary. When AMUSE calls `RegisterReplacedObject`
a second time with the same replacement instance, NDMF detects the existing entry.
NDMF rejects the call and throws `ArgumentException`. `[MEASURED]` `[SOURCE]`

## 2. The symptom and reported exception

The user reported that Mip 4 works without error. When the user sets the policy to
Mip 3 or higher, the build halts with this console stack trace: `[MEASURED]`

```text
System.ArgumentException: RegisterReplacedObject must be called before GetReference is called on the new object
  at nadena.dev.ndmf.ObjectRegistry.nadena.dev.ndmf.IObjectRegistry.RegisterReplacedObject (nadena.dev.ndmf.ObjectReference oldObject, UnityEngine.Object newObject) [0x00015] in ./Packages/nadena.dev.ndmf/Editor/API/ObjectRegistry.cs:243 
  at nadena.dev.ndmf.ObjectRegistry.RegisterReplacedObject (UnityEngine.Object oldObject, UnityEngine.Object newObject) [0x00000] in ./Packages/nadena.dev.ndmf/Editor/API/ObjectRegistry.cs:203 
  at Alrauna.Amuse.Editor.Build.AlphaSeparationApply.PrepareSurvivingSet (nadena.dev.ndmf.BuildContext context, Alrauna.Amuse.Editor.Build.AmusePlatformFinishState state, Alrauna.Amuse.Editor.Build.AlphaSeparationFinalization& finalization) [0x00709] in ./Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:384 
  at Alrauna.Amuse.Editor.Build.AlphaSeparationApply+<>c__DisplayClass1_0.<Execute>b__0 (nadena.dev.ndmf.IAssetSaver _) [0x00000] in ./Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:41 
  at Alrauna.Amuse.Editor.Build.AmuseBuildOperation.Execute (Alrauna.Amuse.Editor.Build.HostLifecycleCapability lifecycle, nadena.dev.ndmf.IAssetSaver assetSaver, Alrauna.Amuse.Editor.Build.PrepareAmuseMutation prepare, Alrauna.Amuse.Editor.Build.ApplyAmuseMutation apply) [0x00049] in ./Packages/com.alrauna.amuse/Editor/Build/AmuseBuildOperation.cs:126 
  at Alrauna.Amuse.Editor.Build.AlphaSeparationApply.Execute (nadena.dev.ndmf.BuildContext context) [0x00038] in ./Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:38
```

## 3. Empirical reproduction

The investigation tested an active `nadena.dev.ndmf.ObjectRegistry` instance in
the Unity Editor: `[MEASURED]`

```csharp
var oldMat = new Material(Shader.Find("lilToon"));
var newMat = new Material(Shader.Find("lilToon"));
var reg = new ObjectRegistry((Transform)null);
var ireg = (IObjectRegistry)reg;
var oldRef = ireg.GetReference(oldMat, true);

// First registration succeeds:
ireg.RegisterReplacedObject(oldRef, newMat);

// Second registration with the same newMat throws:
ireg.RegisterReplacedObject(oldRef, newMat);
```

The test threw the exact reported exception: `[MEASURED]`

```text
System.ArgumentException: RegisterReplacedObject must be called before GetReference is called on the new object
  at nadena.dev.ndmf.ObjectRegistry.nadena.dev.ndmf.IObjectRegistry.RegisterReplacedObject (nadena.dev.ndmf.ObjectReference oldObject, UnityEngine.Object newObject) [0x00015] in ./Packages/nadena.dev.ndmf/Editor/API/ObjectRegistry.cs:243
```

This confirms the error condition. When `RegisterReplacedObject` receives an object
that already exists in the registry, NDMF throws this exception. `[MEASURED]` `[SOURCE]`

## 4. Root cause analysis

### 4.1. Deduplication of canonical opaque materials

In `AlphaSeparationPreparation.cs:310`, AMUSE establishes an avatar-wide contract:
`[SOURCE]`

> "Admitted derived evidence are renderer-specific, while the generated opaque
> artifact is deduplicated avatar-wide by source material."

When multiple renderers share a source material, AMUSE generates only one
canonical opaque clone for that material. AMUSE reuses that single clone for all
renderers that convert triangles on that material. `[SOURCE]`

### 4.2. Unchecked registration loop in PrepareSurvivingSet

In `AlphaSeparationApply.cs:373-388`, AMUSE registers surviving replacements with
NDMF: `[SOURCE]`

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

The loop processes each renderer survivor group. Inside each group, it processes
each slot. For each slot, it reads the `(sourceMaterial, opaqueMaterial)` pairs.
`[SOURCE]`

When two renderers share a source material and both separate triangles:
1. The first renderer registers `pair.Value` with `pair.Key`. NDMF stores the
   mapping `_obj2ref[newObject] = oldObject`. `[SOURCE]`
2. The second renderer encounters the same `pair.Value`. AMUSE calls
   `RegisterReplacedObject` again. `[SOURCE]`
3. NDMF calls `TryRegisterReplacedObject`. Line 253 checks
   `self.GetReference(newObject, false) != null`. Because the object exists in
   `_obj2ref`, this check returns false. `[SOURCE]`
4. NDMF throws `ArgumentException`. `[MEASURED]` `[SOURCE]`

The mesh registration loop in lines 390-399 carries the same vulnerability if two
renderers share a separated mesh instance. `[SOURCE]`

### 4.3. Why Mip 4 succeeded while Mip 3 failed

On the test avatar, two separate renderers share the same dress material.
`[MEASURED]`

Under Mip 4, the transparency check was more strict. At most one of the sharing
renderers satisfied the opaque threshold. Only one renderer survived into
`rendererSurvivors`. The loop executed once for that material. `[INFERENCE]`

Under Mip 3, the transparency check permitted both renderers to prove triangles
opaque. Both renderers survived into `rendererSurvivors`. The second renderer
executed the loop with the shared replacement material. This triggered the duplicate
registration exception. `[INFERENCE]`

## 5. Remediation plan

### 5.1. Deduplicate replacement registrations

Add a set of registered replacement objects in `PrepareSurvivingSet`: `[RECOMMENDATION]`

```csharp
var registeredReplacements = new HashSet<UnityEngine.Object>();
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

`registeredReplacements.Add` returns false when an object was added before. This
ensures that AMUSE calls `RegisterReplacedObject` exactly once per unique replacement
instance. `[RECOMMENDATION]`

### 5.2. Verification plan

1. Add a unit test in `AlphaSeparationApplyTests.cs`. Create two renderers that
   share the same material and the same canonical opaque clone. Prove that
   `PrepareSurvivingSet` registers without exception and returns
   `AmusePreparationDecision.Ready()`. `[RECOMMENDATION]`
2. Run the complete EditMode test suite to prove zero regressions.
   `[RECOMMENDATION]`
3. Re-run avatar optimization on the test avatar in the Census Lab with Mip 3.
   Prove that the build completes without the NDMF console error.
   `[RECOMMENDATION]`
