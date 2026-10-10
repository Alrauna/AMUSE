# Build Transient Unlock Lifecycle Safety: Design and Specification

Date: 2026-10-08.
Branch plan: fix/latent-bugs-and-impurities. Base: main at c375101.
No source assets are modified.
All mutations remain inside the NDMF build copy.

Privacy note: this document contains no private avatar, renderer, material, scene, or machine identity. Public vendor shader identifiers stay exact.

Labels: `[SOURCE]` is a fact read from cited code lines in this repository.
`[INFERENCE]` is a conclusion from evidence.

---

## 1. Summary

This design resolves three latent bugs in the lifecycle cleanup of locked material transient unlock.
The changes ensure that animation clips on virtualized animator controllers invert correctly before transient clone destruction.
The changes eliminate restrictive path and type filtering during curve inversion.
The changes add comprehensive avatar-wide verification that zero animation keyframes reference destroyed unlocked clone materials.

The changes guarantee that transient material clones never leave dangling references in committed animation clips.

---

## 2. Background and Motivation

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockSwapIn.cs:445-465`.
During the swap-in pass, `TransientUnlockSwapIn` enumerates all animation clips across the entire avatar through NDMF `AnimationIndex`.
It rewrites curves on innate controllers and virtualized controllers on child transforms.
When a curve swaps a material slot, it replaces the original locked material with an unlocked clone material.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:424-452`.
During the window close pass, `CommittedClips` enumerates clips from `bindings.GetInnateControllers(context.AvatarRootObject)`.
It omits components that implement `IVirtualizeAnimatorController` on child transforms.
In contrast, `CommittedControllerGraph.Enumerate` in `Editor/Host/CommittedControllerGraph.cs:101-106` explicitly enumerates `GetComponentsInChildren<IVirtualizeAnimatorController>(true)`.
Because `CommittedClips` omits virtualized child controllers, `InvertCommittedCurves` never inverts their committed clips.
The pass then destroys the unlocked clone material.
The committed clips retain dangling references to the destroyed material asset.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:300-355`.
`InvertCommittedCurves` filters candidate bindings through `rendererTypesByPath` and `TryParseMaterialSlotBindingFor`.
If an upstream tool renamed a path or transformed binding types between swap-in and window close, the curve is skipped.
The keyframe value continues to point to the unlocked clone.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:402-416`.
`CurveInversionWasComplete` checks only whether `graph.Refusal` equals `AvatarAnimationRefusal.None`.
It never inspects clip keyframes to confirm that zero references to `pair.UnlockedClone` remain.
If `InvertCommittedCurves` skipped a curve, `CurveInversionWasComplete` still returns true.
`DestroyPairCopiesOrNameRetention` then destroys the clone immediately.
In addition, `CurveInversionWasComplete` re-enumerates the controller graph from scratch for every open material pair.

---

## 3. Detailed Design

### 3.1 Inclusion of Virtualized Animator Controllers in Committed Clips
In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:
Update `CommittedClips` to enumerate controllers from both innate bindings and `IVirtualizeAnimatorController` components.
Reuse the existing `ClipsInStateMachine` method (lines 455 to 520) which already handles sub-state machines, blend trees, and cycle detection:
```csharp
private static IEnumerable<AnimationClip> CommittedClips(BuildContext context)
{
    var bindings = context.GetState<AmusePlatformFinishState>().AnimatorBindings;
    if (bindings == null)
    {
        yield break;
    }

    var seenClips = new HashSet<AnimationClip>();
    var seenControllers = new HashSet<RuntimeAnimatorController>();

    foreach (var entry in bindings.GetInnateControllers(context.AvatarRootObject))
    {
        if (entry.Item2 is AnimatorController controller && seenControllers.Add(controller))
        {
            foreach (var layer in controller.layers)
            {
                foreach (var clip in ClipsInStateMachine(layer.stateMachine, seenClips))
                {
                    yield return clip;
                }
            }
        }
    }

    foreach (var source in context.AvatarRootObject
                 .GetComponentsInChildren<IVirtualizeAnimatorController>(true))
    {
        if (source.AnimatorController is AnimatorController controller && seenControllers.Add(controller))
        {
            foreach (var layer in controller.layers)
            {
                foreach (var clip in ClipsInStateMachine(layer.stateMachine, seenClips))
                {
                    yield return clip;
                }
            }
        }
    }
}
```
This guarantees that all virtualized animation clips, sub-state machines, and blend trees are enumerated.

### 3.2 Direct Curve Reference Restoration
In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:
Update `InvertCommittedCurves`.
Scan object reference curves across all committed clips.
When any keyframe value matches `pair.UnlockedClone`, replace the value with `pair.LockedOriginal`:
```csharp
foreach (var clip in CommittedClips(context))
{
    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
    {
        var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        if (curve == null || curve.Length == 0)
        {
            continue;
        }

        var modified = false;
        for (var index = 0; index < curve.Length; index++)
        {
            if (NamesSwappedCopy(curve[index].value, pair.UnlockedClone))
            {
                curve[index].value = pair.LockedOriginal;
                modified = true;
            }
        }

        if (modified)
        {
            AnimationUtility.SetObjectReferenceCurve(clip, binding, curve);
        }
    }
}
```
This removes rigid path and renderer type filters.
Any curve that acquired a reference to the unlocked clone is safely restored to `pair.LockedOriginal`.

### 3.3 Direct Keyframe Reference Verification and Avatar-Wide Check
In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:
Update `CurveInversionWasComplete`:
1. Check that the committed controller graph enumerates with `AvatarAnimationRefusal.None`.
2. Inspect all clips on the avatar root object via `AnimationUtility.GetAnimationClips(context.AvatarRootObject)` and all clips from `CommittedClips`.
3. If any keyframe in any clip references `pair.UnlockedClone`, return false to keep the clone alive.
```csharp
private static bool CurveInversionWasComplete(
    BuildContext context,
    TransientUnlockWindowState.SwappedPair pair,
    IReadOnlyList<AnimationClip> committedClips,
    CommittedControllerGraphResult graph)
{
    if (graph.Refusal != AvatarAnimationRefusal.None)
    {
        return false;
    }

    var allClips = new HashSet<AnimationClip>(committedClips);
    if (context.AvatarRootObject != null)
    {
        foreach (var clip in AnimationUtility.GetAnimationClips(context.AvatarRootObject))
        {
            if (clip != null)
            {
                allClips.Add(clip);
            }
        }
    }

    foreach (var clip in allClips)
    {
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (curve == null)
            {
                continue;
            }
            for (var index = 0; index < curve.Length; index++)
            {
                if (NamesSwappedCopy(curve[index].value, pair.UnlockedClone))
                {
                    return false;
                }
            }
        }
    }

    return true;
}
```
Cache the enumerated graph and clip list once during the window close pass.
Pass them to each pair check to eliminate redundant graph enumeration.

---

## 4. Verification and Test Plan

1. Integration test `VirtualizedChildControllerClipsInvertedBeforeCloneDestruction`:
   Create an avatar hierarchy with an `IVirtualizeAnimatorController` component on a child transform.
   Attach an animation clip with sub-state machines and blend trees animating a material slot.
   Run `TransientUnlockWindowClose`.
   Assert that keyframes in all nested clips point back to `pair.LockedOriginal`.
   Assert that zero dangling references remain.
2. Unit test `CurveInversionRestoresReferencesAcrossRenamedPaths`:
   Bind an object curve referencing `pair.UnlockedClone` with a path not present in `pair.Bindings`.
   Execute `InvertCommittedCurves`.
   Assert that the keyframe value is restored to `pair.LockedOriginal`.
3. Unit test `CurveInversionWasCompleteRejectsClipsHoldingCloneReference`:
   Simulate a clip that retains `pair.UnlockedClone`.
   Assert that `CurveInversionWasComplete` returns false and prevents clone destruction.
4. Unit test `CurveInversionWasCompleteRetainsCloneOnNonNoneRefusal`:
   Simulate a committed controller graph that returns `AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour`.
   Assert that `CurveInversionWasComplete` returns false.
5. Unit test `MultipleSwappedPairsInvertIndependently`:
   Configure an avatar with two distinct locked materials swapped to two unlocked clones.
   Run inversion and verify that both clones restore to their respective `LockedOriginal` materials.
