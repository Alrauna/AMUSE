# Build Reconstruction and Traversal Safety Design Specification

Date: 2026-10-08. Branch: `fix/latent-bugs-and-impurities`. Base: `main` at `9eed463`.

Privacy note: this record is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, instance name, or test-job identifier. Aggregate counts appear as ranges. Machine references name roles only, for example the dev editor instance.

---

## 1. Overview

This design addresses two defects in the Build subsystem:
1. `LockedMaterialReconstruction` skips declared integer properties during material property restoration.
2. `TransientUnlockWindowClose` lacks recursion guards in animator traversals.

Both changes make sure that materials reconstruct completely.
The changes also prevent stack overflows on cyclic animator controllers.

---

## 2. Requirements and Invariants

1. `LockedMaterialReconstruction.ReadSavedProperties` must read `m_SavedProperties.m_Ints`.
2. `LockedMaterialReconstruction.MoveSuffixedSavedValues` must restore `ShaderPropertyType.Int` properties.
3. Integer properties must use `clone.SetInteger` with rounded integer values.
4. `TransientUnlockWindowClose.ClipsInMotion` must track visited `Motion` objects to prevent recursion cycles.
5. `TransientUnlockWindowClose.ClipsInStateMachine` must track visited `AnimatorStateMachine` objects to prevent cycles.
6. `TransientUnlockWindowClose.CommittedClips` must share visited sets across all layers.
7. Traversal methods must use internal static visibility so test fixtures can invoke them.

---

## 3. Detailed Architecture and Implementation

### 3.1 Integer Property Restoration in LockedMaterialReconstruction

In `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialReconstruction.cs`:

In `ReadSavedProperties`, read `m_SavedProperties.m_Ints`:

```csharp
var serializedInts = serialized.FindProperty(
    "m_SavedProperties.m_Ints");
if (serializedInts != null)
{
    for (var index = 0; index < serializedInts.arraySize; index++)
    {
        var element = serializedInts.GetArrayElementAtIndex(index);
        var name = ElementKeyName(element);
        var second = element.FindPropertyRelative("second");
        if (name == null || second == null ||
            second.propertyType != SerializedPropertyType.Integer)
        {
            continue;
        }

        floats.Add((name, second.intValue));
    }
}
```

In `MoveSuffixedSavedValues`, restore integer properties using `clone.SetInteger`:

```csharp
if (type == ShaderPropertyType.Float ||
    type == ShaderPropertyType.Range)
{
    clone.SetFloat(plainName, entry.value);
}
else if (type == ShaderPropertyType.Int)
{
    clone.SetInteger(plainName, Mathf.RoundToInt(entry.value));
}
```

This restores culling modes, stencil operations, and enum selections.

### 3.2 Cycle and Visited Guards in Animator Traversal

In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:

Expose traversal methods as internal static and track visited objects:

```csharp
internal static IEnumerable<AnimationClip> ClipsInStateMachine(
    AnimatorStateMachine stateMachine,
    HashSet<AnimationClip> seenClips,
    HashSet<AnimatorStateMachine> visitedStateMachines = null,
    HashSet<Motion> visitedMotions = null)
{
    if (stateMachine == null)
    {
        yield break;
    }

    visitedStateMachines ??= new HashSet<AnimatorStateMachine>();
    visitedMotions ??= new HashSet<Motion>();

    if (!visitedStateMachines.Add(stateMachine))
    {
        yield break;
    }

    foreach (var state in stateMachine.states)
    {
        if (state.state == null)
        {
            continue;
        }

        foreach (var clip in ClipsInMotion(state.state.motion, seenClips, visitedMotions))
        {
            yield return clip;
        }
    }

    foreach (var child in stateMachine.stateMachines)
    {
        if (child.stateMachine == null)
        {
            continue;
        }

        foreach (var clip in ClipsInStateMachine(
                     child.stateMachine, seenClips, visitedStateMachines, visitedMotions))
        {
            yield return clip;
        }
    }
}

internal static IEnumerable<AnimationClip> ClipsInMotion(
    Motion motion,
    HashSet<AnimationClip> seenClips,
    HashSet<Motion> visitedMotions = null)
{
    if (motion == null)
    {
        yield break;
    }

    visitedMotions ??= new HashSet<Motion>();
    if (!visitedMotions.Add(motion))
    {
        yield break;
    }

    if (motion is AnimationClip clip)
    {
        if (seenClips.Add(clip))
        {
            yield return clip;
        }

        yield break;
    }

    if (motion is BlendTree blendTree)
    {
        foreach (var child in blendTree.children)
        {
            foreach (var found in ClipsInMotion(
                         child.motion, seenClips, visitedMotions))
            {
                yield return found;
            }
        }
    }
}
```

In `CommittedClips`, instantiate shared visited sets once and pass them to every layer traversal:

```csharp
var visitedStateMachines = new HashSet<AnimatorStateMachine>();
var visitedMotions = new HashSet<Motion>();

foreach (var layer in controller.layers)
{
    foreach (var clip in ClipsInStateMachine(
                 layer.stateMachine, seenClips, visitedStateMachines, visitedMotions))
    {
        yield return clip;
    }
}
```

This prevents duplicate walks across layers and eliminates recursion overflow risks.

---

## 4. Verification Plan

1. In `LockedMaterialReconstructionTests.cs`:
   - `MoveSuffixedSavedValues_RestoresIntegerProperties_ToClonedMaterial`
   - `ReadSavedProperties_ReadsIntegerPropertyStorage`
2. In `TransientUnlockWindowCloseTests.cs`:
   - `ClipsInMotion_TerminatesCleanly_WhenBlendTreeContainsCycle`
   - `ClipsInStateMachine_TerminatesCleanly_WhenStateMachineContainsCycle`
   - `ClipsInMotion_DoesNotDuplicateClips_WhenBlendTreeIsSharedAcrossLayers`
