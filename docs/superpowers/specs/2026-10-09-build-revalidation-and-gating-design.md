# Build Revalidation and Gating: Design and Specification

Date: 2026-10-09.
Branch plan: fix/latent-bugs-and-impurities. Base: fae7e7e.
No source assets are modified.
All mutations remain inside the NDMF build copy.

Privacy note: this document contains no private avatar, renderer, material, scene, or machine identity. Public vendor shader identifiers stay exact.

Labels: `[SOURCE]` is a fact read from cited code lines in this repository.
`[INFERENCE]` is a conclusion from evidence.

---

## 1. Summary

This design fixes six build-order and gating defects in the PlatformFinish passes. The source record is the third latent-bugs investigation. This pair owns Findings 9, 11, 12, 13, 32, and 47.

Apply revalidates the mesh geometry with a cheap fingerprint instead of reference identity alone.
Apply guards the nullable object curve like its sibling loop.
Apply recalculates the renderer transform path and refuses the renderer on change.
The structural graph check keeps running and storing, and reports only behind the existing lifecycle consent gates.
The transient unlock destroy gate sweeps every renderer slot array, and the close removes a pair only after the clone is destroyed.
The window close passes its materialized clip list down instead of re-enumerating it per pair.

## 2. Background and Motivation

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:529-534` and `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:148-154`.
Preparation instantiates the mesh clone from the expected mesh at barrier time. Apply revalidates the renderer with reference equality against `prepared.Target.ExpectedMesh`, the slot array length, and the live submesh count. A same-phase pass can rewrite the shared mesh contents in place without replacing the reference. That edit passes every clause of the condition. Apply then finalizes the barrier-time clone.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:704-713`.
`FinalizeClone` reads the clone's source index arrays and states in its comment that nothing rewrote them because a replaced mesh was already refused. The comment itself names the gap. The code protects only the replacement case. A stale ordinal split ships the wrong triangles.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:651-657`, `:370-375`, `:244-263`.
The slot validation loop iterates `target.Curve` without a null or empty check. The curve-edit loop skips `target.Curve == null || target.Curve.Length == 0`. The discovery loop stores `clip.GetObjectCurve(binding)` unguarded. NDMF declares `GetObjectCurve` as nullable. The transient unlock swap-in guards the same call at `Editor/Build/TransientUnlockSwapIn.cs:184-187` and `:496-498` per the investigation record.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:209-211` and `Packages/com.alrauna.amuse/Editor/Build/AmuseStructuralGraphCheck.cs:15-30`.
The plugin registers `AMUSE structural graph check` first and unconditionally. The pass enumerates the committed controller graph and reports the avatar refusal with `AmuseReports.AvatarRefusal` whenever the graph refuses. This happens before every gate. An avatar that never opted in, or whose lifecycle refused, still receives the console entry. The plugin's own contract at `:424-437` says an avatar without the opt-in component or with the feature switch off gets nothing analyzed, nothing mutated, and nothing reported.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:241-243, 248-255, 380-389`.
Apply enumerates live clips for `prepared.RendererPath`, the barrier-time transform path, and parses slot bindings against that path and `prepared.RendererTypeName`. A same-phase reparent leaves the clips bound to the old path. Apply then rewrites material-swap curves, including one appended binding per surviving Split slot, through the stale path.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:118-128, 140-179, 359-412` and `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowState.cs:103-111`.
The close enumerates committed clips once at `:116`. The destroy gate `CurveInversionWasComplete` scans curves only. A foreign pass that assigned the unlocked clone to a renderer slot outside the recorded pairs passes the gate. The close then destroys the clone and the built avatar holds a missing reference. The same loop calls `window.Remove(pair)` unconditionally at `:127`, while the state class contract says `Remove` is for a pair whose close or fallback finished.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:316-323`.
`InvertCommittedCurves` calls `CommittedClips(context)` itself, although `Execute` materialized the same list at `:116`. The re-enumeration runs once per open pair. The pass also carries a second clip universe inside `CurveInversionWasComplete` at `:376-388`, which unions the materialized list with `AnimationUtility.GetAnimationClips`.

## 3. Detailed Design

### 3.1 Finding 9: Geometry Fingerprint at Apply Revalidation

Add a per-submesh index-count fingerprint to the prepared record and compare it at apply.

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`, add a constructor parameter and property `ExpectedSubmeshIndexCounts` of type `IReadOnlyList<int>` to `PreparedRendererSeparation`.

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`, build the fingerprint immediately before the `PreparedRendererSeparation` construction at `:513`:

```csharp
var expectedSubmeshIndexCounts = new int[target.ExpectedMesh.subMeshCount];
for (var submesh = 0; submesh < expectedSubmeshIndexCounts.Length; submesh++)
{
    expectedSubmeshIndexCounts[submesh] =
        (int)target.ExpectedMesh.GetIndexCount(submesh);
}
```

`Mesh.GetIndexCount(int)` returns a `uint` without copying the index buffer. Every prepared renderer carries a non-null `ExpectedMesh`, because the analyzer refuses mesh-less renderers before preparation. Thread the array through the constructor call at `:513-521`.

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`, extend the refusal condition at `:148-154` with one clause:

```csharp
!SubmeshIndexCountsMatch(
    currentMesh, prepared.ExpectedSubmeshIndexCounts)
```

The clause sits after the existing `currentMesh == null` clause, so the helper receives a non-null mesh in every reachable path. Add the private helper:

```csharp
private static bool SubmeshIndexCountsMatch(
    Mesh mesh, IReadOnlyList<int> expected)
{
    if (mesh == null || mesh.subMeshCount != expected.Count)
    {
        return false;
    }

    for (var submesh = 0; submesh < expected.Count; submesh++)
    {
        if (mesh.GetIndexCount(submesh) != (uint)expected[submesh])
        {
            return false;
        }
    }

    return true;
}
```

The refusal stays `AlphaSeparationSlotRefusal.RendererChangedSincePreparation` and the existing per-candidate report stays unchanged. The check runs for every prepared renderer, not only split ones. This matches the existing discipline, which refuses a replaced mesh for every renderer.

Document the threat model at the new clause:

```csharp
// Reference identity cannot see an in-place rewrite of the shared
// mesh by a same-phase pass. The per-submesh index counts captured
// at preparation detect any edit that changes a submesh's index
// length. An edit that preserves every count still passes, so the
// fingerprint narrows the window to count-preserving edits. It does
// not close it.
```

`[INFERENCE]` A count-preserving reordering of one submesh's index buffer still escapes this fingerprint. The shared decision for this pair pins the fingerprint to per-submesh index counts as the cheap guard. The spec records the residual openly rather than silently implying full coverage.

### 3.2 Finding 11: Nullable and Empty Curve Guard in Slot Validation

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`, `ValidateCandidateSlot`, add the guard as the second statement of the per-target loop:

```csharp
if (target.SlotIndex != slotIndex)
{
    continue;
}

if (target.Curve == null || target.Curve.Length == 0)
{
    continue;
}
```

The guard precedes the marker-clip check. This mirrors the sibling edit loop at `:370-375` exactly. The sibling skips a target with no keyframes because there is nothing to rewrite. Validation must predict the edit loop's behavior on the same target, so the skip applies to every later check, including the marker and captured-triple checks.

Replace the stale sentence in the comment above the keyframe loop at `:645-650`. The sentence claims an empty curve was never discovered. The new comment states the reason for the guard: NDMF declares `GetObjectCurve` nullable, the discovery loop stores it unguarded, and both consumers skip a null or empty curve.

### 3.3 Finding 12: The Structural Graph Check Becomes Gate-Observing

Shared decision for this pair, recorded as given: the structural graph check still runs, but reports only behind the existing lifecycle consent gate. Unopted avatars get no console output. Results stay stored for the gated path.

In `Packages/com.alrauna.amuse/Editor/Build/AmuseStructuralGraphCheck.cs`:

Delete the report block at `:24-28`. The method keeps both stores:

```csharp
state.StructuralGraph = graph;
state.AvatarRefusal = graph.Refusal;
```

Rewrite the class summary. The sentence "this pass itself changes nothing and reports only the avatar-scoped refusal when it fires" becomes a statement that the pass changes nothing and reports nothing, and that the barrier reports the stored refusal behind the lifecycle gates.

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`:

Update the registration comment at `:207-211`. The phrase "and reports the refusal when it fires" becomes "and reports nothing. The barrier reports the stored refusal behind the consent gates."

In `Execute`, add the report to the graph-refusal gate at `:462-471`:

```csharp
if (graph.Refusal != AvatarAnimationRefusal.None)
{
    state.AvatarRefusal = graph.Refusal;
    AmuseReports.AvatarRefusal(
        context.AvatarRootObject, graph.Refusal);
    return;
}
```

Update the gate comment. The current comment says the structural pass named the cause on the pass path and on the inline fallback path. The new comment says this gate names the cause exactly once, and only for avatars the gates above it admitted: positive lifecycle, the opt-in component on the avatar root, and the feature switch on.

The precise behavior change:

- An avatar without the opt-in component gets no structural console output.
- An avatar with the component and the switch off gets no structural console output.
- An avatar with a lifecycle refusal gets the lifecycle report at `:408-415` and no structural console output.
- An opted-in avatar with the switch on and a refusing graph gets exactly one `amuse.avatar.` console entry, now emitted by the gate instead of the pass.
- Both storage writes stay in the pass, so direct callers and the summary accounting keep their inputs.

`[INFERENCE]` The graph-refusal gate sits before the D8 consent dialog, and the D8 block is unreachable for a graph-refusing avatar by its own comment at `:473-479`. Placing the report at the graph-refusal gate is therefore the only position that keeps the entry for opted-in avatars while silencing unopted ones. The gate chain from `:408` through `:471` is the lifecycle consent surface the shared decision names.

### 3.4 Finding 13: Renderer Path Revalidation at Apply

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`, compute the live transform path inside the per-renderer loop of `PrepareSurvivingSet`, before the refusal condition at `:148`:

```csharp
var liveRendererPath = renderer == null
    ? null
    : AnimationUtility.CalculateTransformPath(
        renderer.transform, context.AvatarRootObject.transform);
```

This is the same computation the plugin performs at `AmusePlatformFinishPlugin.cs:595-596`, applied to the same renderer against the same root.

Extend the refusal condition at `:148-154` with one clause:

```csharp
!string.Equals(
    liveRendererPath,
    prepared.RendererPath,
    StringComparison.Ordinal)
```

An empty string is a valid path for a renderer on the avatar root, per the record comment at `AlphaSeparationRecords.cs:240-243`, so the comparison is ordinal with no null-coalescing. A renderer moved outside the avatar root yields a mismatched or null live path and refuses.

The refusal stays `RendererChangedSincePreparation` and reuses the existing per-candidate report block.

The discovery parse at `:248-255` already requires each binding's type to be compatible with `prepared.RendererTypeName`. With the path gate in place, apply can no longer enumerate or rewrite a foreign renderer's curves: the enumeration at `:241-243` still uses the prepared path, and a changed path refuses the renderer before any clip enumeration feeds validation or the appended write at `:380-389`.

`[INFERENCE]` The investigation keeps the cross-renderer rewrite reachability flag open as a runtime question. The refuse-on-change gate closes both named cases, the dead path and the foreign renderer on the old path, without needing that answer.

### 3.5 Finding 32: Slot Sweep in the Destroy Gate and Delayed Pair Removal

In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:

Extend `CurveInversionWasComplete` at `:359-412` with a renderer slot sweep after the curve loop:

```csharp
if (context != null && context.AvatarRootObject != null)
{
    foreach (var renderer in context.AvatarRootObject
                 .GetComponentsInChildren<Renderer>(true))
    {
        var materials = renderer.sharedMaterials;
        if (materials == null)
        {
            continue;
        }

        foreach (var material in materials)
        {
            if (NamesSwappedCopy(material, pair.UnlockedClone))
            {
                return false;
            }
        }
    }
}
```

The reversion inverts only the recorded slots. The gate must therefore scan every renderer on the avatar, including inactive ones, and any surviving clone reference keeps the clone alive. `NamesSwappedCopy` already implements Unity equality, which the slot values require. Update the method summary: the gate returns false when the committed graph refuses, when any keyframe names the clone, or when any renderer slot array names the clone.

Change `DestroyPairCopiesOrNameRetention` at `:140-179` to return `bool`. It returns true when it destroyed the clone and false when it recorded the retention refusals. Update its summary accordingly.

Change the pair loop at `:118-128`:

```csharp
foreach (var pair in window.OpenPairs.ToList())
{
    InvertReferences(context, pair);

    ReassertShippedSlots(pair, recorded);

    if (DestroyPairCopiesOrNameRetention(
            context, finishState, pair, committedClips, graph))
    {
        window.Remove(pair);
    }
}
```

The state class contract at `TransientUnlockWindowState.cs:103-111` says `Remove` is for a pair whose close or fallback finished. A retained pair is neither, so the pair stays in `window.OpenPairs`. The retained clone stays reachable through the open pair, and the named refusals keep the retention visible.

Two pinned tests bake the current removal-on-retention behavior. Updating them is mandated, not a test weakening:

- `AReversionWithoutProvenInversionNamesTheRetainedClone` at `Tests/Editor/Build/TransientUnlockWindowCloseTests.cs:460-518` asserts `TransientUnlockTestKnobs.Window.OpenPairs` is empty at `:516-517` after a retention.
- `APlayPathCloseThatCannotProveTheCurveInversionRetainsTheClone` at `Tests/Editor/Build/TransientUnlockTransformationTests.cs:877-880` asserts the same after its retention count at `:996-999`.

Both change to expect exactly one open pair, the retained one.

### 3.6 Finding 47: One Clip Universe per Close

In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:

`Execute` materializes `committedClips` once at `:116`. Pass it down:

- `InvertReferences` at `:258-260` gains an `IReadOnlyList<AnimationClip> committedClips` parameter and hands it to `InvertCommittedCurves`.
- `InvertCommittedCurves` at `:316-354` changes its signature from `(BuildContext, SwappedPair)` to `(IReadOnlyList<AnimationClip>, SwappedPair)`. The `BuildContext` parameter existed only for the `CommittedClips(context)` call at `:323`.
- The two test call sites at `Tests/Editor/Build/TransientUnlockWindowCloseTests.cs:848` and `:910` materialize the list with `TransientUnlockWindowClose.CommittedClips(context).ToList()` and pass it.

The verification universe inside `CurveInversionWasComplete` at `:376-388` stays as it is. The fix shape for this finding is the list hand-off only. `[INFERENCE]` Unifying the inversion universe with the verification universe is a separate semantic decision and stays out of scope.

### 3.7 Shared Files and Execution Order

The pair order for this audit is fixed: 1 multi-capture, 2 analysis, 3 host, 4 build, 5 poiyomi, 6 semantics, 7 presets-architecture. This pair is pair 4.

- Pair 7 touches `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs` after this pair lands. Its Finding 24 corrects the class summary ordinal at `:13-14`. This design rewrites behavior in `Execute`, the destroy gate, and `InvertCommittedCurves`, and leaves the class summary ordinal untouched. Pair 7 edits the summary on the post-pair-4 text.
- Pair 7 touches `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:201-243` for Finding 24, the same region as the registration comment this design rewrites in 3.3. Apply 3.3 first. Pair 7 then adjusts ordinal wording on the updated comment.
- Pair 7 owns the write-only `RewrittenBinding` record cleanup in `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowState.cs` as part of Finding 27. This design touches only the `Remove` contract documentation neighborhood at `:103-111` by honoring it, and never edits the file.
- The `AlphaSeparationApply.cs:14-15` class summary calls the pass third. Finding 24 assigns that correction to pair 7. This design does not touch the summary.
- Pair 7 co-edits `Tests/Editor/Build/` at position 7: it touches `AmuseReportStringsTests.cs`, `AmusePlatformFinishPluginTests.cs`, `VerifiedPoiyomiTestSeams.cs`, `VerifiedLilToonTestSeams.cs`, and `TransientUnlockWindowCloseTests.cs`. The shared file in this pair is `TransientUnlockWindowCloseTests.cs`. This pair lands first and rewrites the call sites around lines 848 and 910, so pair 7's later line anchors in that file can drift and must be re-read at execution time.

## 4. Verification and Test Plan

All tests run in the Unity Test Runner in EditMode. A filtered run that reports zero tests is a failure.

1. Test `AnInPlaceMeshEditBetweenBarrierAndApplyRefusesEverySlotOfTheRenderer` in `Tests/Editor/Build/AlphaSeparationApplyTests.cs`. Appends one triangle to submesh zero of the source mesh in place between barrier and apply. Pins the fingerprint refusal, no mutation, and the swept clone.
2. Test `NullAndEmptyObjectCurvesAreSkippedBySlotValidation` in `Tests/Editor/Build/AlphaSeparationApplyTests.cs`. Invokes `ValidateCandidateSlot` through reflection with a null curve and an empty curve. The null case throws a wrapped `NullReferenceException` today and must return `AlphaSeparationSlotRefusal.None` after the fix. The empty case pins the guard's second half.
3. Test `ARendererReparentedBetweenBarrierAndApplyRefusesEverySlotOfTheRenderer` in `Tests/Editor/Build/AlphaSeparationApplyTests.cs`. Reparents the renderer between barrier and apply. Pins the path refusal and no mutation.
4. Tests `AnUnoptedAvatarWithARefusingGraphStaysSilentButStoresTheRefusal` and `ASwitchedOffAvatarWithARefusingGraphStaysSilentButStoresTheRefusal` in `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`. Both run the full registered pipeline over a refusing graph. Both assert zero `amuse.avatar.` entries and a stored refusal. Today both yield one entry from the structural pass.
5. Existing guard tests `AvatarAnimationRefusalReportsExactlyOneEntry` at `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:1824` and `AvatarAnimationRefusalReportsPlainEnglishEntry` at `:1766` stay green. They prove the gated path still reports once after the report moves from the pass to the barrier gate.
6. Test `ACloneThatAnUnrecordedSlotNamesIsRetainedAndThePairStaysOpen` in `Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`. Assigns the unlocked clone to an unrecorded renderer slot before the close. Pins clone survival, the named retention, and one open pair. Today the close destroys the clone.
7. Updated pinned assertions in `AReversionWithoutProvenInversionNamesTheRetainedClone` and `APlayPathCloseThatCannotProveTheCurveInversionRetainsTheClone`. Both now expect the retained pair to stay in the window.
8. Test `InvertCommittedCurves_InvertsOnlyTheClipsItIsGiven` in `Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`. A clip outside the passed list keeps its clone reference. The updated call-site tests at `:806-917` pin the inversion behavior on the new signature.
