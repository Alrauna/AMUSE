# Build Revalidation and Gating Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix mesh revalidation by reference only, the nullable curve dereference, the structural check outside the consent gates, the stale renderer path, the curves-only destroy gate, and the redundant clip enumeration in the PlatformFinish build passes.

**Architecture:** Editor build passes under `Packages/com.alrauna.amuse/Editor/Build/`. The apply pass, the structural graph check pass, the barrier's graph-refusal gate, and the window close pass each carry one behavior change. The prepared record gains one captured fingerprint. No pass is added or removed, so the registration order stays five passes.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode), NDMF test platforms and fixtures.

**Spec:** `docs/superpowers/specs/2026-10-09-build-revalidation-and-gating-design.md`

## Global Constraints

- Use simple English. Short active sentences. One idea per sentence.
- No semicolons in prose.
- No contractions.
- Never record absolute or machine-specific paths. Use repository-relative paths only.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names.
- Date every status claim. As of 2026-10-09.
- Never stage, commit, push, or modify Git history without explicit user authorization.
- The dev editor instance with `Application.dataPath == <repo-root>/Assets` is the only authorized test instance.
- Vendor shaders are never installed. Tests use the `Hidden/Alrauna/AmuseTests/*` stand-ins and the verified seams in `Tests/Editor/Build/`.
- Every filtered test run must report more than zero tests. A run that reports zero tests is a failure.
- This plan is pair 4 of the audit. Pair 7 edits `TransientUnlockWindowClose.cs` and the plugin registration comment after this plan lands. Keep the class summary ordinals untouched.

---

### Task 1: Geometry Fingerprint at Apply Revalidation

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Consumes: `Mesh.GetIndexCount(int)`, `SharedMeshOf`, the split fixture helpers on `AlphaSeparationSplitTests`
- Produces: `PreparedRendererSeparation.ExpectedSubmeshIndexCounts` and the refusal clause in `PrepareSurvivingSet`

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`, add the test `AnInPlaceMeshEditBetweenBarrierAndApplyRefusesEverySlotOfTheRenderer`. Copy the harness of `PlannedSplitInvalidatedLateSweepsItsCloneWithoutAnyWrite` (line 1418). Build the split fixture with `AlphaSeparationSplitTests.EnsureSplitFolder`, `ImportSplitAlphaTexture`, `SplitAlphaMaterial`, `VerifiedTransparentMaterial`, and `CreateSplitSourceMesh`. Inside the `ProbeScope`, append one triangle to submesh zero of the source mesh in place:

```csharp
using (new ProbeScope(_ =>
{
    var indices = mesh.GetIndices(0);
    var grown = new int[indices.Length + 3];
    indices.CopyTo(grown, 0);
    grown[indices.Length] = indices[0];
    grown[indices.Length + 1] = indices[1];
    grown[indices.Length + 2] = indices[2];
    mesh.SetIndices(
        grown,
        MeshTopology.Triangles,
        0,
        calculateBounds: false);
}))
{
    var context = AvatarProcessor.ProcessAvatar(
        root, SeamTestPlatform.Instance);
    probe = context.GetState<AlphaSeparationSeamProbe>();
}
```

The mesh reference never changes, so today's checks all pass. Assert the expected post-fix behavior:

- `probe.Decision.IsPrepared` is true.
- `probe.Decision.HasMutation` is false.
- `probe.SlotRefusals(AlphaSeparationSlotRefusal.RendererChangedSincePreparation)` equals 1, the fixture's single candidate slot.
- `probe.RecordedMeshClones[0] == null` is true, so the sweep destroys the orphaned clone.

- [ ] **Step 2: Run the test to verify it fails**

Run the test in the Unity Test Runner in EditMode, filtered to the test name. The report must list exactly this test. A report of zero tests is a failure. Today the split applies, so `HasMutation` is true and the refusal count is zero. The test fails on the `HasMutation` assertion.

- [ ] **Step 3: Implement the fingerprint**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`, add the constructor parameter `IReadOnlyList<int> expectedSubmeshIndexCounts` and the property `internal IReadOnlyList<int> ExpectedSubmeshIndexCounts { get; }` to `PreparedRendererSeparation`. Guard the parameter with `ArgumentNullException` like its siblings.

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`, build the fingerprint before the constructor call at line 513:

```csharp
var expectedSubmeshIndexCounts = new int[target.ExpectedMesh.subMeshCount];
for (var submesh = 0; submesh < expectedSubmeshIndexCounts.Length; submesh++)
{
    expectedSubmeshIndexCounts[submesh] =
        (int)target.ExpectedMesh.GetIndexCount(submesh);
}
```

Thread `expectedSubmeshIndexCounts` through the `PreparedRendererSeparation` construction.

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`, extend the refusal condition at line 148 with the clause `!SubmeshIndexCountsMatch(currentMesh, prepared.ExpectedSubmeshIndexCounts)`. The clause follows the `currentMesh == null` clause. Add the private helper:

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

Add the threat model comment at the clause, exactly as spec section 3.1 shows.

- [ ] **Step 4: Run the tests to verify they pass**

Run the filtered selection in the Unity Test Runner in EditMode: the new test plus `PlannedSplitInvalidatedLateSweepsItsCloneWithoutAnyWrite`, `IdentityMappedSplitStillWritesItsAppendedCurve`, and `CutoutSplitSlotRewritesCurvesOntoTheAppendedSlot`. The report must list all four. A report of zero tests is a failure. All must pass. The sibling split tests prove the fingerprint accepts an untouched mesh.

---

### Task 2: Nullable and Empty Curve Guard in Slot Validation

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Consumes: the private `ValidateCandidateSlot`, `VirtualClip.Clone`, `PreparedSlotSeparation`, `SubmeshSeparationPlan`
- Produces: slot validation that skips a null or empty object curve exactly like the edit loop at lines 370 to 375

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`, add the test `NullAndEmptyObjectCurvesAreSkippedBySlotValidation`. The repo already invokes private production members through reflection in `RuntimeStatePureEntryAcceptsOnlyImmutableProofInputs`, which lives in `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` around line 3708 and reflects `AmusePlatformFinishPass.AnalyzeRuntimeStatesForTests`. Follow that precedent:

```csharp
var source = Track(VerifiedOpaqueMaterial());
var plan = new SubmeshSeparationPlan(
    0,
    0,
    new[] { 0 },
    new[] { 1 },
    SubmeshSeparationDisposition.WhollyOpaqueCandidate);
var candidate = new PreparedSlotSeparation(
    plan,
    new Dictionary<Material, Material> { [source] = source },
    false,
    false);
var binding = EditorCurveBinding.PPtrCurve(
    "Body",
    typeof(SkinnedMeshRenderer),
    "m_Materials.Array.data[0]");
var virtualClip = VirtualClip.Clone(
    new CloneContext(GenericPlatformAnimatorBindings.Instance),
    new AnimationClip());
var method = typeof(AlphaSeparationApply).GetMethod(
    "ValidateCandidateSlot",
    BindingFlags.NonPublic | BindingFlags.Static);
foreach (var curve in new ObjectReferenceKeyframe[][]
         {
             null,
             Array.Empty<ObjectReferenceKeyframe>(),
         })
{
    var targets = new List<(VirtualClip, EditorCurveBinding,
                            ObjectReferenceKeyframe[], int)>
    {
        (virtualClip, binding, curve, 0),
    };
    var arguments = new object[]
    {
        null,
        candidate,
        new Material[] { source },
        new HashSet<(string, string, string)>(),
        targets,
        0,
        null,
    };
    var refusal = default(object);
    Assert.DoesNotThrow(
        () => refusal = method.Invoke(null, arguments),
        "a null or empty curve must skip validation, not throw");
    Assert.That(
        (AlphaSeparationSlotRefusal)refusal,
        Is.EqualTo(AlphaSeparationSlotRefusal.None));
}
```

The first argument stays null because a wholly opaque candidate never reads `prepared`. The seventh array slot receives the `out` value. Add `using System.Reflection;` if the file lacks it.

- [ ] **Step 2: Run the test to verify it fails**

Run the test in the Unity Test Runner in EditMode, filtered to the test name. The report must list exactly this test. A report of zero tests is a failure. Today the null-curve case throws `TargetInvocationException` wrapping the `NullReferenceException` from line 651, so `Assert.DoesNotThrow` fails. The empty-curve case passes today. It pins the guard's second half and is characterization.

- [ ] **Step 3: Implement the guard**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` in `ValidateCandidateSlot`, add the guard as the second statement of the per-target loop, directly after the slot index check:

```csharp
if (target.Curve == null || target.Curve.Length == 0)
{
    continue;
}
```

The guard precedes the marker-clip check. The edit loop at lines 370 to 375 skips the same target, so validation must predict that skip on every later check. Replace the stale sentence in the comment above the keyframe loop at lines 645 to 650. The old sentence claims an empty curve was never discovered. The new sentence states that NDMF declares `GetObjectCurve` nullable, the discovery loop stores it unguarded, and both consumers skip a null or empty curve.

- [ ] **Step 4: Run the tests to verify they pass**

Run the filtered selection in the Unity Test Runner in EditMode: the new test, `MarkerClipRefusalIsSlotLocalAndWritesNothingForIt`, `UnmappedLiveValueInvalidatesOnlyItsSlot`, and `EverySlotIsValidatedBeforePrepareReturnsAndEveryDistinctReasonIsRecorded`. The report must list all four. A report of zero tests is a failure. All must pass.

---

### Task 3: Renderer Path Revalidation at Apply

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Consumes: `AnimationUtility.CalculateTransformPath`, `prepared.RendererPath`
- Produces: the apply pass refuses a renderer whose transform path changed since preparation

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`, add the test `ARendererReparentedBetweenBarrierAndApplyRefusesEverySlotOfTheRenderer`. Copy the harness of `CandidatePreparedUnderABlockingProofDoesNotApplyAfterTheBlockIsRemoved` (line 911): the `SingleTriangleMesh` fixture, one wholly opaque candidate, `ApplyTestPlatform.Instance`, and a `ProbeScope` around the platform run. Build the context after the `using` block and read the state from it. Inside the `ProbeScope`, reparent the renderer under a new child of the avatar root:

```csharp
using (new ProbeScope(_ =>
{
    var mover = new GameObject("AMUSE moved between passes");
    mover.transform.SetParent(root.transform, false);
    renderer.transform.SetParent(mover.transform, true);
}))
{
    context = AvatarProcessor.ProcessAvatar(
        root, ApplyTestPlatform.Instance);
}
```

Assert the expected post-fix behavior:

- `state.SlotRefusalCount(AlphaSeparationSlotRefusal.RendererChangedSincePreparation)` equals 1, the fixture's single candidate slot.
- `state.AppliedRendererCount` is 0.
- `renderer.sharedMaterials[0]` is `Is.SameAs(material)`, so nothing was written.

- [ ] **Step 2: Run the test to verify it fails**

Run the test in the Unity Test Runner in EditMode, filtered to the test name. The report must list exactly this test. A report of zero tests is a failure. Today the clip enumeration still finds the clips on the prepared path, so apply writes the material-swap curves. The refusal count stays zero and the renderer count becomes 1. The test fails on the refusal count assertion.

- [ ] **Step 3: Implement the path gate**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` in the per-renderer loop of `PrepareSurvivingSet`, compute the live path before the refusal condition at line 148:

```csharp
var liveRendererPath = renderer == null
    ? null
    : AnimationUtility.CalculateTransformPath(
        renderer.transform, context.AvatarRootObject.transform);
```

Extend the refusal condition with the clause:

```csharp
!string.Equals(
    liveRendererPath,
    prepared.RendererPath,
    StringComparison.Ordinal)
```

Add the comment at the clause. State that the empty string is the valid avatar root path, that a renderer moved outside the root refuses, and that the refusal closes both named cases of the investigation: the dead path and a foreign renderer that now owns the old path. The refusal stays `RendererChangedSincePreparation` and reuses the existing report block.

- [ ] **Step 4: Run the tests to verify they pass**

Run the filtered selection in the Unity Test Runner in EditMode: the new test, `WhollyOpaqueSlotReplacesOnlyItsMaterial`, `UnmappedReplacementBetweenPassesIsPreservedEntirely`, and `MappedReplacementBetweenPassesAppliesThatValue`. The report must list all four. A report of zero tests is a failure. All must pass. The replacement tests prove an untouched renderer still applies.

---

### Task 4: The Structural Graph Check Becomes Gate-Observing

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseStructuralGraphCheck.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: `AmuseReports.AvatarRefusal`, `CommittedControllerGraph.Enumerate`, the test fixtures `AttachProbeBehaviour`, `DestroyCommittedClone`, `DestroyControllerGraph`
- Produces: a silent structural pass with both state stores, and exactly one gate-emitted avatar refusal for opted-in avatars

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`, add two tests. Both share one fixture shape. Build an avatar root whose committed controller graph carries an unallowlisted state behaviour, following `UnallowlistedBehaviourRefusesTheWholeAvatarWithoutAnalysis` (line 1703) for the controller and the finally block. Count console entries with `ErrorReport.CaptureErrors` and the `SimpleError.TitleKey.StartsWith("amuse.avatar.")` loop from `AvatarAnimationRefusalReportsExactlyOneEntry` (line 1824).

Test one: `AnUnoptedAvatarWithARefusingGraphStaysSilentButStoresTheRefusal`. The root carries `FixtureAvatarIdentity.AttachVrcDescriptor` and a `LineRenderer` and no `AmuseAvatarOptimizer`, following `MissingComponentMeansThePipelineNeverObservesRenderers` (line 178). Run the full registered pipeline:

```csharp
var reports = ErrorReport.CaptureErrors(
    () => AvatarProcessor.ProcessAvatar(
        root, TestGenericPlatform.Instance));
var amuse = context.GetState<AmusePlatformFinishState>();
Assert.That(amuse.AvatarRefusal, Is.EqualTo(
    AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour));
Assert.That(CountAvatarEntries(reports), Is.Zero);
```

Extract the counting loop into one private static helper in the test class, because both tests count the same way.

Test two: `ASwitchedOffAvatarWithARefusingGraphStaysSilentButStoresTheRefusal`. The root carries the component with `_alphaSeparatorEnabled` flipped to false through `SerializedObject`, following `SwitchedGateKeepsThePipelineIdle` (line 260). Add the same refusing graph. Assert the same two outcomes.

- [ ] **Step 2: Run the tests to verify they fail**

Run both tests in the Unity Test Runner in EditMode, filtered to the two names. The report must list both. A report of zero tests is a failure. Today the registered structural pass reports the avatar refusal before every gate, so each fixture yields exactly one `amuse.avatar.` entry. Both tests fail on the zero-entry assertion.

- [ ] **Step 3: Move the report behind the gate**

In `Packages/com.alrauna.amuse/Editor/Build/AmuseStructuralGraphCheck.cs`, delete the report block at lines 24 to 28. Keep both stores:

```csharp
state.StructuralGraph = graph;
state.AvatarRefusal = graph.Refusal;
```

Rewrite the class summary. The pass changes nothing and reports nothing. The barrier reports the stored refusal behind the lifecycle gates.

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, update the registration comment at lines 207 to 211. The registration itself stays first and unconditional. In `Execute`, extend the graph-refusal gate at lines 462 to 471:

```csharp
if (graph.Refusal != AvatarAnimationRefusal.None)
{
    state.AvatarRefusal = graph.Refusal;
    AmuseReports.AvatarRefusal(
        context.AvatarRootObject, graph.Refusal);
    return;
}
```

Update the gate comment. This gate names the cause exactly once and only for avatars the gates above it admitted: positive lifecycle, the opt-in component on the avatar root, and the feature switch on.

- [ ] **Step 4: Run the tests to verify they pass**

Run the filtered selection in the Unity Test Runner in EditMode: the two new tests, `AvatarAnimationRefusalReportsExactlyOneEntry`, `AvatarAnimationRefusalReportsPlainEnglishEntry`, `UnallowlistedBehaviourRefusesTheWholeAvatarWithoutAnalysis`, and `MissingComponentMeansThePipelineNeverObservesRenderers`. The report must list all six. A report of zero tests is a failure. All must pass. The two report tests prove the gated path still emits exactly one entry after the move.

---

### Task 5: Slot Sweep in the Destroy Gate and Delayed Pair Removal

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs`

**Interfaces:**
- Consumes: `NamesSwappedCopy`, `Renderer.sharedMaterials`, `TransientUnlockTestKnobs.BeforeClose`, `TransientUnlockTestPlatform.Instance`
- Produces: `CurveInversionWasComplete` that scans every renderer slot array, a `DestroyPairCopiesOrNameRetention` that returns whether the clone was destroyed, and a pair loop that removes only destroyed pairs

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`, add the test `ACloneThatAnUnrecordedSlotNamesIsRetainedAndThePairStaysOpen`. Follow the harness of `AReversionWithoutProvenInversionNamesTheRetainedClone` (line 461): `BuildAvatarRoot`, `TransientUnlockTestLifecycle.LockedMaterial`, `OneSlotMesh`, `AddRenderer`, `AddMaterialSwapAnimation`, and `AvatarProcessor.ProcessAvatar(root, TransientUnlockTestPlatform.Instance)`. Inside `TransientUnlockTestKnobs.BeforeClose`, take the pair and assign its clone to a slot the pair never recorded:

```csharp
TransientUnlockTestKnobs.BeforeClose = context =>
{
    var pair = context
        .GetState<TransientUnlockWindowState>()
        .OpenPairs[0];
    cloneRef = pair.UnlockedClone;
    var holder = Track(new GameObject("AMUSE foreign holder"));
    holder.transform.SetParent(root.transform, false);
    var foreignRenderer = holder.AddComponent<MeshRenderer>();
    foreignRenderer.sharedMaterials = new[] { pair.UnlockedClone };
};
```

Assert after the run:

- `cloneRef == null` is false, so the clone survived.
- `state.SlotRefusalCount(AlphaSeparationSlotRefusal.TransientUnlockCloneRetained)` equals 1.
- `TransientUnlockTestKnobs.Window.OpenPairs` has count 1, the retained pair.

- [ ] **Step 2: Run the test to verify it fails**

Run the test in the Unity Test Runner in EditMode, filtered to the test name. The report must list exactly this test. A report of zero tests is a failure. Today the gate scans curves only, the committed curve was inverted, the gate returns true, and the close destroys the clone. The test fails on the clone survival assertion.

- [ ] **Step 3: Implement the sweep and the delayed removal**

In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:

Extend `CurveInversionWasComplete` after the curve loop with the renderer sweep from spec section 3.5. The sweep walks `context.AvatarRootObject.GetComponentsInChildren<Renderer>(true)` and returns false when any slot entry matches `pair.UnlockedClone` through `NamesSwappedCopy`. Update the method summary with the third false condition.

Change `DestroyPairCopiesOrNameRetention` to return `bool`. Return true on the destroy path and false on the retention path. Update the summary.

Change the pair loop at lines 118 to 128:

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

- [ ] **Step 4: Update the two pinned tests that bake removal on retention**

Updating these expectations is mandated. The old assertions bake the removal contract violation. They are not being weakened.

In `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`, in `AReversionWithoutProvenInversionNamesTheRetainedClone`, change the assertion at lines 516 to 517 from `Is.Empty` to:

```csharp
Assert.That(
    TransientUnlockTestKnobs.Window.OpenPairs,
    Has.Count.EqualTo(1),
    "the retained pair stays in the window until a close can " +
    "prove the inversion");
```

In `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs`, in `APlayPathCloseThatCannotProveTheCurveInversionRetainsTheClone`, change the assertion following the retention count at lines 996 to 999 the same way, with the same message.

- [ ] **Step 5: Run the tests to verify they pass**

Run the filtered selection in the Unity Test Runner in EditMode: the new test, both updated retention tests, `ABlendTreeResidentSwapClipRevertsToL` (line 363, a destroy path that asserts `OpenPairs` is empty), and `TheFallbackNeverDestroysTheLockedOriginal`. The report must list all five. A report of zero tests is a failure. All must pass. The destroy-path tests prove a proven close still drains the window.

---

### Task 6: One Clip Universe per Close

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`

**Interfaces:**
- Consumes: `CommittedClips`, `InvertReferences`, `InvertCommittedCurves`
- Produces: one materialized clip list handed from `Execute` through `InvertReferences` to `InvertCommittedCurves`

- [ ] **Step 1: Update the call-site tests and add the explicit-list pin**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`, change both call sites of `InvertCommittedCurves` to the new signature. At line 848 in `InvertCommittedCurves_RestoresKeyframesToLockedOriginal_EvenWhenPathNotRecorded` and at line 910 in `InvertCommittedCurves_RestoresKeyframesAcrossSkinnedMeshAndMeshRendererBindings`:

```csharp
var committedClips = TransientUnlockWindowClose
    .CommittedClips(context)
    .ToList();
TransientUnlockWindowClose.InvertCommittedCurves(
    committedClips, pair);
```

Add the test `InvertCommittedCurves_InvertsOnlyTheClipsItIsGiven`. Reuse the harness of the path-not-recorded test. Give the avatar one controller whose clip names the clone, and pass a list that excludes that clip. Assert the keyframe still names the clone after the call. Then pass the full list and assert the keyframe names the locked original.

- [ ] **Step 2: Run the tests to verify they fail**

This task changes a signature. The expected pre-change failure is the compile error of the two updated call sites and the new test against the old `(BuildContext, SwappedPair)` signature. Compile the test assembly and observe the three errors. The verification of this task is the compile error, then the post-change run.

- [ ] **Step 3: Pass the materialized list down**

In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:

Change `InvertCommittedCurves` at line 316 to `internal static void InvertCommittedCurves(IReadOnlyList<AnimationClip> committedClips, TransientUnlockWindowState.SwappedPair pair)`. Replace the `foreach (var clip in CommittedClips(context))` at line 323 with `foreach (var clip in committedClips)`. Delete the now unused `context` parameter.

Change `InvertReferences` at line 258 to accept `IReadOnlyList<AnimationClip> committedClips` and pass it to `InvertCommittedCurves`.

In `Execute`, pass the materialized list at line 116 into `InvertReferences`.

Leave `CurveInversionWasComplete` and its two-argument convenience overload untouched. Unifying the inversion universe with the verification universe is out of scope.

- [ ] **Step 4: Run the tests to verify they pass**

Run the filtered selection in the Unity Test Runner in EditMode: `InvertCommittedCurves_RestoresKeyframesToLockedOriginal_EvenWhenPathNotRecorded`, `InvertCommittedCurves_RestoresKeyframesAcrossSkinnedMeshAndMeshRendererBindings`, `InvertCommittedCurves_InvertsOnlyTheClipsItIsGiven`, `CommittedClips_IncludesVirtualizedAnimatorControllerOnChildTransform`, and `CommittedClips_HandlesSubStateMachinesAndBlendTreesWithoutCycles`. The report must list five. A report of zero tests is a failure. All must pass. Then run the full `TransientUnlockWindowCloseTests` class and confirm the whole class passes, because the close pass now shares one list across every pair.
