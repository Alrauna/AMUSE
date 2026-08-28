# Alpha Separation — Material-Swap Animation Rewrite

**Investigation note. No production code. Not an implementation plan.**

Every claim below is tagged:

- **[SOURCE]** — read directly from pinned repository source.
- **[MEASURED]** — observed by running a characterization in this repository.
- **[INFERENCE]** — architectural reasoning from the above.
- **[DECISION]** — a controller decision this note does not make.

---

## 1. Branch, base, repository state

| | |
|---|---|
| Branch | `investigate/alpha-separation-animation-rewrite` |
| Created from | `main`, verified equal to `origin/main` (0 ahead, 0 behind) |
| Base SHA | `dcfffb7d7fa811df8bc68fcd21fcf2ef886403e1` |
| Working tree at branch creation | clean |
| Pre-existing diff | `Packages/manifest.json`, `Packages/packages-lock.json` — **only** `com.unity.toolchain.macos-arm64-linux-x86_64`, `com.unity.sysroot`, `com.unity.sysroot.linux-x86_64` |
| Unity / NDMF | 2022.3.22f1 / NDMF 1.14.4 (pinned, embedded) |

The complete manifest diff was inspected and contains nothing but the documented macOS
toolchain/sysroot churn. No intentional change shared those files, so `CLAUDE.md`'s restore
was applied. Unity re-added the churn during the compile/test cycles below; it was restored
again at the end.

No local or remote branch named `investigate/alpha-separation-animation-rewrite` existed.
No fetch, pull, push, stage, commit, PR, or history change occurred. Census Lab was neither
used nor inspected.

---

## 2. Verification of the handoff's stated facts

All ten were checked against source. **Nine are confirmed. Fact 2 is partly false, and the
way it is false is itself a defect** — see §2.2.

| # | Claim | Verdict |
|---|---|---|
| 1 | Bindings captured inside `AnimatorServicesContext`; analysis only after deactivation | **Confirmed** |
| 2 | Swap closure discovers current + animated materials per slot | **Partly false — closure is graph-wide, not per renderer.** See §2.2 |
| 3 | Outcomes intersected across admitted materials and property states | **Confirmed** |
| 4 | Planner distinguishes `Unchanged` / `WhollyOpaqueCandidate` / `Split` | **Confirmed** |
| 5 | Production discards the separation plan | **Confirmed** |
| 6 | `CapturedAnimationEvidence` holds no live Unity references | **Confirmed** |
| 7 | Derived `CapturedMaterialEvidence` from `ResolveSlot` is discarded | **Confirmed** |
| 8 | No same-capture live-material / evidence / render-state tuple | **Confirmed, and worse than stated** |
| 9 | Editing committed clips outside NDMF violates the lifecycle | **Confirmed** |
| 10 | NDMF supports reactivation but may reuse or re-clone virtual nodes | **Confirmed** |

### 2.1 Details

**[SOURCE] (1)** `AmusePlatformFinishPlugin.Configure` declares exactly two passes: a
capture pass under `WithRequiredExtension(typeof(AnimatorServicesContext), …)`, then
`sequence.Run(BarrierPassName, …)` with no extension.
`AmusePlatformFinishPass.RequireAnimatorServicesContextInactive` asserts the declaration was
not lost, matching NDMF's exact inactive-extension signal string.

**[SOURCE] (2) — see §2.2.** Closure does discover current and swapped materials, but its
scope is **graph-wide relative to the current renderer capture**, not per renderer and not
per slot. This is a defect, and it is treated as a separate discovered prerequisite.

**[SOURCE] (3)** `ClassifyRuntimeStates` classifies per `AlphaResolution` and folds with
`UnityRendererAlphaAnalysis.IntersectOutcomes(perResolution)` before constructing
`SubmeshSeparationInput`. `ResolveSlot` refuses the whole slot on any admitted material's
failure with no partial prefix.

**[SOURCE] (5)** `ClassifyRuntimeStates` ends `var plan = MeshSeparationPlanner.Create(…);
return plan.OpaqueTriangleCount;`. The plan dies with the local.
`UnityRendererAlphaExtraction.MutationTarget` — a live `Renderer` plus expected mesh and slot
count — is produced by `CaptureGeometry` and likewise never read by the barrier.

**[SOURCE] (7)** In `AdmittedMaterialStates.ResolveSlot`, the derived `admitted`
`CapturedAlphaMaterial` (carrying evidence with animated property values substituted in) is
constructed, passed to `resolveSemantics`, and dropped. Only `AlphaResolution` escapes.

**[SOURCE] (8) — worse than stated.** Two independent gaps, not one:

1. `CaptureObserved` holds `List<Material> admitted` and the returned
   `IReadOnlyList<CapturedAlphaMaterial> capturedMaterials` **in the same scope, index-aligned**
   — and lets the live half fall out of scope. The pairing exists for one statement and is
   then unrecoverable.
2. The closed request is built **only** from `UnityMaterialSemantics.TrySelectAlphaMaterialRequest`.
   `PoiyomiOpaqueConversion.ConversionEvidenceRequest` — a *deliberately separate* 24-property
   render-state schema — never enters the capture. So the captured evidence does not merely
   lack a live-material pairing; **it does not contain the properties conversion eligibility
   reads at all.**

Also: `PoiyomiOpaqueConversion` currently has **zero production consumers** (grep across
`Editor/`). Alpha separation is its first.

**[SOURCE] (9)** `VirtualClip.FromMarker` sets `_clip = oldClip` — the *original* asset — and
`VirtualClip.Commit` returns early for marker clips. Editing a committed clip reached that way
would write a source asset. See §7.3.

### 2.2 Material-swap closure is graph-wide — a discovered prerequisite

**[SOURCE]** `UnityAnimationEvidenceCapture.CaptureObserved` **has no renderer-path
parameter.** It receives `observations` built from *every clip in the whole committed graph*
(`CaptureGraph` walks `graph.Layers` → `layer.Clips`), together with `currentSlots` for the
**one renderer currently being analyzed**. Its admission loop is:

```
foreach (var observation in observations)          // every clip in the graph
    foreach (var binding in observation.Objects)   // every object curve, ANY path
        if (!TryParseMaterialSlotBinding(binding.PropertyName, out var slot)) continue;
        if (slot >= currentSlots.Count)            // ← foreign slot index vs THIS renderer
            return Failed(SlotOutOfRange);
        foreach (var value in binding.Values)
            if (!(value is Material material) || !TryAdmit(material, out _))
                return Failed(InvalidSwapValue);
```

There is **no `binding.Path` comparison anywhere in this method.** Path filtering happens only
later and elsewhere: `AmusePlatformFinishPass.MaterialSlotsFor` filters object bindings on
`binding.Path == rendererPath`, and `ResolveRuntimeStates` filters float bindings via
`ResolveProofRelevant`'s own path check.

So `AdmittedMaterials` is **graph-wide relative to the current renderer capture**: it is a
strict superset of the materials `MaterialSlotsFor` will ever reference for this renderer.

**Consequences — all renderer-scoped, all false positives against local refusal:**

1. **An unrelated renderer's swap material enters this renderer's admitted set.** It is
   admitted, request-selected, captured, and attested as if it were this renderer's evidence.
2. **An unrelated renderer with a higher material-slot index causes `SlotOutOfRange` against
   this renderer's slot count.** Renderer B animating `m_Materials.Array.data[1]` makes a
   one-slot renderer A fail closure — `evidence.IsClosed == false` →
   `RendererAnalysisRefusal.MaterialDependencyClosureFailed`. Renderer A's own animation is
   irrelevant to this.
3. **An unrelated unattested shader family can refuse this renderer's capture.** Every admitted
   material must pass `selectRequest`, and the whole batch must pass the closed capturer;
   either failure yields `UnattestedMaterial` → `MaterialDependencyClosureFailed` for the
   renderer being analyzed. So a locked Poiyomi or transparent lilToon material swapped by an
   unrelated renderer can refuse **unrelated renderer captures**, not merely its own.
   **[SOURCE]** establishes that mechanism; it does **not** establish literal universal
   refusal. Which renderers are actually affected depends on which closure condition fires
   first for each capture — `MissingCurrentMaterial` and `SlotOutOfRange` are both checked
   before request selection, and a renderer refused structurally by
   `HostStructuralRefusalFor` never reaches capture at all. The evidenced claim is
   "can refuse unrelated renderer captures", and that is already a local-refusal violation.
4. **Capture cost, and any future source→opaque mapping, include irrelevant materials.** The
   closed request is the *union* of every graph-wide admitted material's family request, so
   capture gathers evidence no local proof consumes. A naive mapping built over the whole
   `AdmittedMaterials` list would generate opaque clones for materials this renderer never
   references.
5. **It violates AMUSE's local-refusal direction.** `AGENTS.md`: "Unknown information should
   invalidate only conclusions that depend on it… Do not escalate one unsupported fact into
   renderer-wide or avatar-wide refusal unless the dependency genuinely requires that scope."
   Here an unrelated renderer's state escalates into a different renderer's refusal, and the
   dependency does not exist.

**[INFERENCE]** A sixth, compounding effect: because the closed request is a union, a foreign
material of another supported family widens `evidence.RelevanceRequest`, which
`ResolveProofRelevant` and `IsUnrecognizedObjectMaterialBinding` use to decide relevance. A
wider request means more property names can be judged proof-relevant or unrecognized on
*this* renderer, so foreign materials can also enlarge this renderer's refusal surface.

**This is a newly discovered, independently mergeable prerequisite. It is not implemented
here.** Recommended future branch: **`fix/scope-material-swap-closure-to-renderer`**.

**Minimum future falsifiers** (these are the failing tests that branch should add, not tests
added on this branch):

- renderer A has one slot; renderer B has a material-swap curve for slot 1 — analyzing A must
  **not** report `SlotOutOfRange`;
- renderer B swaps an otherwise unattested or simply distinct material at slot 0 — analyzing A
  must **neither admit nor attest** B's material;
- analyzing B must still close over **its own** current and swapped materials (the fix must
  narrow scope, not lose coverage).

---

## 3. Current call flow and the exact missing seams

```
AmusePlatformFinishPlugin.Configure                       [BuildPhase.PlatformFinish]
 ├─ pass 1  "AMUSE animator bindings capture"   WithRequiredExtension(AnimatorServicesContext)
 │     └─ state.AnimatorBindings = ctx.Extension<AnimatorServicesContext>()
 │                                    .ControllerContext.PlatformBindings
 └─ pass 2  "AMUSE semantic barrier"            (no extension → NDMF deactivated + committed)
       ├─ RequireAnimatorServicesContextInactive
       ├─ HostLifecycleCapability.CaptureAndEvaluate     (gates on HasAssetSaver)
       ├─ CommittedControllerGraph.Enumerate(root, bindings)
       └─ foreach Renderer:
            HostStructuralRefusalFor
            UnityAnimationEvidenceCapture.Capture(sharedMaterials, graph, bindings)
              │   NB: `graph` is graph-wide and no path filter is applied here (§2.2)
              └─ live `admitted` List<Material>  ─────────────────► DROPPED   ← SEAM A
            ResolveRuntimeStates
              └─ AdmittedMaterialStates.ResolveSlot
                   └─ derived CapturedAlphaMaterial/Evidence ────► DROPPED   ← SEAM B
            UnityRendererAlphaAnalysis.CaptureGeometry
              └─ UnityRendererMutationTarget ─────────────────────► DROPPED   ← SEAM C
            ClassifyRuntimeStates
              └─ MeshSeparationPlan ──────────────────────────────► DROPPED   ← SEAM D
            state.OpaqueCandidateTriangleCount += plan.OpaqueTriangleCount
```

**Missing seams, in dependency order:**

- **A** — the live build-copy `Material` paired to its admitted index.
- **B** — the derived per-(slot, material) `CapturedMaterialEvidence`.
- **C** — the live renderer mutation target.
- **D** — the separation plan and its dispositions.
- **E** — *conversion* evidence is not captured at all (§2.1).
- **F** — **there is no third pass.** Nothing in `Configure` reactivates
  `AnimatorServicesContext`, so no virtual clip is reachable for editing.
- **G** — `CapturedObjectBinding.TypeName` stores `binding.type.FullName` as a **string**
  (`LiveAnimationObservation.ObserveClip`). An `EditorCurveBinding` needs a real `Type`, and
  `Type` is part of its equality (`ECBComparator.Equals` compares `x.type`). Evidence alone
  cannot rebuild the binding. See §6.2 for why this does *not* require carrying `Type`.

---

## 4. Pinned NDMF findings

All file references are under `Packages/nadena.dev.ndmf/Editor/`.

### 4.1 Extension instances survive deactivation — reactivation is re-entry, not re-creation

**[SOURCE]** `API/BuildContext.cs:504-540` — `ActivateExtensionContext` looks the type up in
`_extensions` and constructs **only if absent**; it then adds to `_activeExtensions`.
`DeactivateExtensionContext` (`:295-320`) calls `OnDeactivate` and removes from
`_activeExtensions` **only** — `_extensions` is never touched.

So a second activation re-enters the *same* `VirtualControllerContext` instance with its
`_layerStates` and `_cloneContext` intact.

### 4.2 `VirtualControllerContext` explicitly anticipates reactivation

**[SOURCE]** `API/AnimatorServices/VirtualControllerContext.cs`, class doc:

> "After deactivating this context, you must not modify the virtual controllers or their
> animations. […] when the virtual controller context is reactivated, it may or may not reuse
> the same virtual nodes as before."

`LayerState.Revalidate(CloneContext, RuntimeAnimatorController)` (`:104-124`) is the
re-entry path, with two branches:

- **Reuse** — `LastCommit == newController && VirtualController != null`: strips
  `ac.layers` from the committed controller and calls `VirtualController.Reactivate()`.
- **Re-clone** — otherwise: logs *"was changed outside of NDMF animator services; cloning a
  second time"*, resets `OriginalObject` to the committed controller and re-clones.

`LayerState.MarkCommitted` (`:64-80`) installs an `OnAnimatorControllerDirty` hook that nulls
`LastCommit` when the committed controller is dirtied. **[INFERENCE]** This makes the branch
self-correcting: if anything mutates the committed controller between the windows, NDMF
re-clones from what is actually there rather than reusing stale virtual nodes. Both branches
therefore represent the committed graph.

### 4.3 `AnimatorServicesContext` rebuilds its index on every activation

**[SOURCE]** `API/AnimatorServices/AnimatorServicesContext.cs` — `OnActivate` constructs a
**fresh** `AnimationIndex` and a **fresh** `ObjectPathRemapper` each time; `OnDeactivate`
calls `AnimationIndex.RewritePaths(ObjectPathRemapper.GetVirtualToRealPathMap())` and nulls
all three. The second window therefore gets a new index over the revalidated controllers, not
a stale one.

`VirtualControllerContext.OnDeactivate` performs the commit: `commitContext.CommitObject(…)`
per controller, `MarkCommitted`, `_platformBindings.CommitControllers(root, controllers)`,
then `context.AssetSaver.SaveAsset(obj)` for every `commitContext.AllObjects` inside
`context.OpenSerializationScope()`.

### 4.4 The pass solver permits the sequence, and preferred adjacency in the measured run

**[SOURCE]** `API/Solver/PluginResolver.cs:241-277` (`ToConcretePasses`) is a linear scan: for
each pass, deactivate every active extension the pass is not compatible with, then activate
every required one. Nothing tracks or forbids a type that was previously deactivated, so
require → drop → require re-activates.

**[SOURCE]** More strongly: consecutive passes inside one plugin `Sequence` get a
`ConstraintType.Sequence` constraint (`API/Fluent/Sequence/Sequence.cs:319-331`), and
`TopoSort.DoSort` (`API/Solver/TopoSort.cs`) maintains a `priorityStack`: while the top pass
`CanRetire == false` (it still has non-`WeakOrder` successors), the next pass is chosen by
`NextPriorityPass()`, which prefers a ready successor joined by a non-`WeakOrder` constraint.

**[MEASURED]** On the dedicated characterization platform, the three AMUSE-sequence passes
were scheduled back to back with nothing interleaved:

```
Processed pass first animator window in 35 ms
Processed pass committed-graph barrier in 32 ms
Processed pass second animator window in 3 ms
Processed pass Close extensions in 9 ms
```

**Scope of this claim — deliberately narrow.** What is established is that pinned NDMF's
solver *strongly prefers* consecutive sequence successors, and that this preference produced
adjacency in the measured configuration. What is **not** established is a universal guarantee
that no foreign plugin can ever interleave under every ecosystem configuration: the
characterization ran on a dedicated platform with no third-party plugins, `NextPriorityPass`
only selects successors that are already `IsReady`, and it falls back to `ready.Min` when none
is, so a differently-constrained pass graph could in principle schedule otherwise.

**[INFERENCE]** Therefore: **production correctness must not depend on adjacency.** Adjacency
is a helpful property that reduces exposure, not a premise. The second window must validate
every relevant live binding and value against the prepared mapping regardless (§8) — which
is exactly what `Revalidate`'s re-clone branch and its `OnAnimatorControllerDirty` hook exist
to accommodate. No additional scheduling infrastructure is proposed, and no further
characterization is warranted for this narrower statement.

### 4.5 `AnimationIndex` offers exactly the operations needed — and one to avoid

**[SOURCE]** `API/AnimatorServices/AnimationIndex.cs`:

- `GetClipsForBinding(EditorCurveBinding)` — binding-keyed, returns a `HashSet<VirtualClip>`.
- `GetClipsForObjectPath(string)` — path-keyed.
- `EditClipsByBinding(IEnumerable<EditorCurveBinding>, Action<VirtualClip>)` — the narrow
  edit operation; materializes `binding.SelectMany(GetClipsForBinding).ToHashSet()` first, and
  re-caches only the clips the callback actually invalidated.
- `RewriteObjectCurves(Func<Object, Object>)` — **a global rewrite of every object curve in
  the avatar.** This is the operation the handoff correctly forbids: it would remap a material
  everywhere it appears, including slots AMUSE proved nothing about.

Clip identity, never name: `EnumerateClips` dedupes by node identity through a `visited` set,
and both dictionaries are keyed by `VirtualClip` reference / `EditorCurveBinding`. **Clip
`Name` participates in no lookup anywhere in the index.**

### 4.6 Generated-material persistence needs no explicit `IAssetSaver` call

**[SOURCE]** `API/BuildContext.cs:205-258` (`Serialize`, called from `Finish()` after all
extensions deactivate) walks `_avatarRootObject.ReferencedAssets(traverseSaved: true,
includeScene: false)` and saves every reachable non-persistent asset.

**[SOURCE]** `API/Util/VisitAssets.cs`, `ObjectReferences`, has an explicit
`case AnimationClip clip:` arm that enumerates `GetObjectReferenceCurveBindings` →
`GetObjectReferenceCurve` → `frame.value`. There are matching arms for `AnimatorState`
(→ `motion`) and `BlendTree` (→ children); `AnimatorController` falls to the
`SerializedObject` default.

**[INFERENCE]** A generated `Material` referenced **only** by a material-swap object curve is
therefore reachable from the avatar root and is persisted by `Serialize()`. Renderer
`sharedMaterials` references are reachable trivially. **No `IAssetSaver.SaveAsset` call is
required for generated materials, and none should be made** — see §9.

---

## 5. Characterization: setup, RED, GREEN, cleanup

Source inspection made the lifecycle look sound, but the handoff correctly refuses "the API
appears to allow it". A characterization was added.

**Artifact (retained):**
`Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorServicesReactivationCharacterizationTests.cs`

It reuses the repository's established pattern — a synthetic NDMF plugin confined to its own
`INDMFPlatformProvider` (as `AnimatorBindingsLifetimeGateTests` and
`BarrierUnderActiveExtensionProbePlugin` already do), driven by
`AvatarProcessor.ProcessAvatar(root, platform)` under
`OverrideTemporaryDirectoryScope(null)`.

**Setup.** A synthetic avatar: `Animator` on the root, two `SkinnedMeshRenderer` children, an
in-memory `AnimatorController`, and **two `AnimationClip`s deliberately sharing the name
`"shared clip name"`** — one carrying `m_Materials.Array.data[0]` on `swapped renderer`, the
decoy carrying the same property name on `other renderer`. The swap curve has three keyframes
at times `0, 0.25, 1.5` with values `alpha A, alpha B, alpha A`. The plugin declares three
passes: window → extension-free barrier → **window again**.

### RED

The third pass's `WithRequiredExtension` was temporarily removed
(`sequence.Run("second animator window", SecondWindow)`):

```
FAILED ReactivatedAnimatorServicesObservesTheCommittedGraphAndCommitsMappedObjectCurves
  the characterization plugin threw: System.Exception:
    Extension nadena.dev.ndmf.animator.AnimatorServicesContext not active
  at nadena.dev.ndmf.BuildContext.Extension[T] () BuildContext.cs:112
```

**This is what falsification looks like:** without reactivation there is no `AnimationIndex`,
no virtual clip, and no rewrite. The second window is load-bearing, not incidental.

### GREEN

Declaration restored. `2 passed, 0 failed`. Recorded output:

```
Authored curve:            0=>alpha A|0.25=>alpha B|1.5=>alpha A
Committed at barrier:      0=>alpha A|0.25=>alpha B|1.5=>alpha A
Observed in second window: 0=>alpha A|0.25=>alpha B|1.5=>alpha A
Final committed curve:     0=>opaque A|0.25=>opaque B|1.5=>opaque A
Clips edited by binding:   shared clip name
```

**[MEASURED] What this proves:**

1. The three-pass sequence runs, with NDMF deactivating/committing between windows and
   reactivating for the third pass.
2. The extension-free barrier reads the committed clip with the authored keyframes.
3. **The reactivated `AnimationIndex` observes the committed material-slot binding with its
   exact object-reference keyframes and exact times** — the second window semantically
   represents the graph the barrier analyzed.
4. Mapping through a prepared `Material → Material` dictionary and committing a second time
   yields the mapped values with **times preserved bit-identically** (`"R"` round-trip
   formatting).
5. **Exactly one clip was edited** despite two clips sharing a name — association is by
   binding, never by display name.
6. Source `AnimationClip`, source `AnimatorController`, and source `Material`s are all
   asserted unmodified after the build; the committed clip is asserted *not* reference-equal
   to the source clip.

**Cleanup.** All fixture objects are tracked and `DestroyImmediate`d in `Fixture.Dispose`,
reached through `using var` so it runs on assertion failure too. Nothing is written to the
asset database: `OverrideTemporaryDirectoryScope(null)` disables asset saving, so **no test
folder was created and no project asset was touched**. The plugin is confined to
`com.alrauna.amuse.tests.animator-reactivation`, which no other fixture uses, and a second
test pins that confinement.

### What the characterization does *not* prove

- Nothing about **marker/special motions** — `GenericPlatformAnimatorBindings.IsSpecialMotion`
  always returns `false`, so the generic platform cannot exercise them. §7.3 rests on source.
- Nothing about **persistence** — `OverrideTemporaryDirectoryScope(null)` makes `Serialize()`
  early-return. §4.6 rests on source.
- Nothing about foreign plugins mutating controllers between the windows.
- Nothing about VRChat innate controllers, synced layers, or override controllers.

---

## 6. Comparison of the three lifecycle approaches

### A. Analyze committed graph, then reactivate for validated virtual edits — **RECOMMENDED**

- Preserves the existing, deliberate, tested post-commit barrier and every avatar/renderer
  refusal already built on it.
- Edits go through `VirtualClip`, so NDMF owns cloning, committing, and asset saving.
- **[MEASURED]** proven end to end, including exact keyframe preservation and
  identity-based clip association.
- **[MEASURED]** the passes were scheduled adjacently in the characterization run, which
  reduces exposure to foreign mutation between the windows — but correctness does not
  rest on that (§4.4); the second-window validation does.
- Cost: one extra pass, and an entry validation (§8).

### B. One window: analyze and mutate before the first commit

- **Rejected.** It destroys the property the barrier exists for. `AmusePlatformFinishPass`
  documents it explicitly: "NDMF commits the virtualized controllers when
  `AnimatorServicesContext` deactivates, so a barrier running while that extension is still
  active would read pre-commit controller state." `RequireAnimatorServicesContextInactive`
  actively guards against it, and `BarrierRefusesToRunWhileAnimatorServicesContextIsStillActive`
  tests that guard.
- `CommittedControllerGraph.Enumerate` reads `bindings.GetInnateControllers(avatarRoot)` — the
  controllers *assigned to the avatar*. Inside an active context those are the pre-virtualization
  originals, so AMUSE would analyze upstream tools' un-applied input and then mutate the
  virtual graph. That is a false-positive generator, not a shortcut.

### C. Analyze after commit, then edit committed `AnimationClip` objects directly

- **Rejected.** `VirtualControllerContext`'s own doc forbids it: "subsequent NDMF processing
  steps may directly modify the serialized animator controllers."
- Worse, it is unsound for **marker clips**: `FromMarker` commits the *original SDK asset* by
  identity, so a direct `AnimationUtility.SetObjectReferenceCurve` would mutate a source
  asset — exactly the boundary `AGENTS.md` and `CLAUDE.md` prohibit.
- And it is fragile against reactivation: if any later context reactivates,
  `Revalidate`'s dirty hook fires and NDMF re-clones, silently discarding or duplicating the
  edit depending on ordering.

**Recommendation: A.** The evidence supports the handoff's existing recommendation rather than
contradicting it.

### Why *not* Avatar Optimizer's animation dependency graph

**[INFERENCE]** AAO maintains a whole-avatar animator reachability and dependency graph because
its transformations are avatar-global (component removal, merging, GC of unreachable state).
AMUSE's transformation here is the opposite shape: **one binding, on one renderer path, whose
complete value set is already closed by existing evidence.** `AnimationIndex` already provides
binding-keyed and path-keyed lookup with identity semantics and incremental re-caching — the
exact query AMUSE needs, maintained by NDMF.

Building a general curve-remapping framework would (a) duplicate `AnimationIndex`,
(b) violate the YAGNI ladder in `AGENTS.md` with one consumer and no materially different
second case, and (c) expand the proof surface from "these material values on this binding" to
"the avatar's animation graph." It is not justified. AAO and d4rk remain useful as evidence
that post-commit animation rewriting is an accepted ecosystem practice — no framework of
theirs should be copied.

---

## 7. Exact curve transformations

Shared preconditions for both dispositions, per renderer slot `i`:

- the slot's admitted set is closed (`evidence.IsClosed`);
- every admitted material for slot `i` is attested and eligible (`AlreadyOpaque` or
  `Convertible`);
- every affected triangle is `ProvenOpaque` across **all** admitted resolutions for slot `i`
  (already the existing `IntersectOutcomes` contract);
- a prepared mapping `opaqueOf : admittedIndex → Material` covers every admitted material
  **of slot `i`** — that is, exactly `CapturedMaterialSlotEvidence.AdmittedMaterialIndices`
  for that slot, as computed by the path-filtered `MaterialSlotsFor`. An `AlreadyOpaque`
  material maps to **itself** — no clone (`PoiyomiOpaqueConversion` documents `AlreadyOpaque`
  as a successful no-op).

**The mapping domain is per affected renderer slot, joined through admitted indices. It is
NOT the whole `AdmittedMaterials` list.** Per §2.2 that list is graph-wide, so mapping over it
would generate opaque clones for materials this renderer never references. The admitted index
is the join key into the shared evidence; the *domain* is the per-slot index set.

**One clone per distinct admitted source material, never per Animator state.** Deduplication
is by admitted-material index — already reference-deduped by `CaptureObserved`'s
`materialIndices` dictionary — so a material shared by several slots or several clips yields
one clone.

### 7.1 Wholly opaque slot

1. `renderer.sharedMaterials[i] := opaqueOf(currentMaterialIndex[i])`.
2. For every virtual clip carrying a binding with `path == rendererPath` and
   `propertyName == "m_Materials.Array.data[i]"`, for every keyframe: replace `value` with
   `opaqueOf(value)`; **do not touch `time`**; write back with `SetObjectCurve(binding, curve)`.
3. No submesh is added, no mesh is generated, no draw call is added.

### 7.2 Mixed (`Split`) slot

Let `j` be the appended slot index (`= current slot count`).

1. Leave slot `i`'s curve **and** `sharedMaterials[i]` **completely unchanged**.
2. Append slot `j`: `sharedMaterials := sharedMaterials + [ opaqueOf(currentMaterialIndex[i]) ]`.
   **Append only.** The prior investigation demonstrated in this project that prepending or
   inserting silently redirects existing material-swap animation.
3. For every virtual clip carrying the slot-`i` binding, construct a **new** binding identical
   except `propertyName = "m_Materials.Array.data[j]"`, and set on it a curve with
   **identical keyframe times** and values `opaqueOf(originalValue)`. Write with
   `SetObjectCurve`.
4. Generated submesh `j` carries the proven-opaque triangles; submesh `i` keeps the rest.

**[INFERENCE]** This is exactly what overturns the prior note's §6 claim. That claim —
"a slot with more than one admitted material cannot be safely separated" — was reasoned on
the premise that the appended slot keeps a single static material while the original slot
continues swapping, so the two halves diverge. Rewriting the appended slot's curve removes
that premise. **The prior claim is conditionally correct only in the absence of curve
rewriting**, as the handoff states.

### 7.3 Behaviour of each named form

| Form | Behaviour | Basis |
|---|---|---|
| **Multiple controllers** | Handled. `AnimationIndex` spans `GetAllControllers()`; lookups are global across controllers. | [SOURCE] |
| **Clips with identical names** | Irrelevant by construction. Name participates in no index lookup. | [SOURCE] + [MEASURED] |
| **Same clip from multiple graph locations** | Edited exactly once. `EnumerateClips` dedupes by node identity; `GetClipsForBinding` returns a `HashSet<VirtualClip>`. | [SOURCE] |
| **Binding typed `Renderer` vs `SkinnedMeshRenderer`** | Both are distinct bindings; `ECBComparator.Equals` compares `x.type`. **Both must be rewritten if both exist.** See §7.4. | [SOURCE] |
| **Special / marker motions** | **MUST REFUSE.** `VirtualClip.SetObjectCurve` begins `if (IsMarkerClip) return;` — a **silent no-op**. `Commit` also returns early, and `FromMarker` keeps the original SDK asset. Attempting the edit would appear to succeed while the avatar keeps swapping to the alpha material. Evidence already carries `CapturedClipEvidence.IsSpecialMotion`. | [SOURCE] |
| **Empty object curve** | `GetObjectCurve` may return `null` or a zero-length array. Zero keyframes contribute no values; treat as no-op for that clip, not a refusal. | [SOURCE] |
| **`null` keyframe value** | Already a **closure failure**: `CaptureObserved` returns `InvalidSwapValue` for any non-`Material` (including `null`) value, so the renderer never reaches mutation. | [SOURCE] |
| **Non-material / destroyed value** | Same as above — `!(value is Material)` fails closure. A *destroyed* `Material` compares equal to `null` under Unity's operator, so it also fails closure. | [SOURCE] |
| **Unadmitted or newly observed value** | **MUST REFUSE** at second-window validation, before any mutation. See §8. | [INFERENCE] |
| **Several eligible slots in one clip** | `EditClipsByBinding` dedupes clips across the supplied bindings, so the callback is invoked **once per clip** even when several of its bindings match. The callback must therefore handle *all* target bindings of that clip itself. | [SOURCE] |
| **Same source material in several slots** | Correct by construction: one clone per distinct admitted material, shared across slots. Each slot is independently proven; sharing the clone does not share the proof. | [INFERENCE] |
| **Synced layers** | Already an avatar-scoped refusal upstream: `CommittedControllerGraph.Enumerate` returns `UnsupportedSyncedLayerOverrides` for any `layer.syncedLayerIndex >= 0`. | [SOURCE] |
| **Virtualized motions** | Already refused: any `IVirtualizeMotion` with non-null `Motion` yields `UnresolvedVirtualizedMotionContext`. | [SOURCE] |
| **Override controllers** | Already refused: `TryAddController` rejects any `RuntimeAnimatorController` that is not an `AnimatorController` → `UnsupportedAnimatorControllerForm`. | [SOURCE] |
| **Animation events** | Already an avatar-scoped refusal. | [SOURCE] |

### 7.4 Resolving seam G without carrying `Type`

**[INFERENCE]** `CapturedObjectBinding` stores only `TypeName` as a string, and
`Type.GetType(fullName)` is unreliable without assembly qualification. But the `Type` need not
be carried: in the second window, real `EditorCurveBinding`s with real `Type`s are available
from `clip.GetObjectCurveBindings()`.

Recommended query shape, which is binding-driven and type-agnostic:

```
index.GetClipsForObjectPath(rendererPath)   → materialize to a list first
  → per clip: clip.GetObjectCurveBindings()
      → keep those where path == rendererPath
        and TryParseMaterialSlotBinding(propertyName) == target slot
```

`GetClipsForObjectPath` returns the index's own live `HashSet`, so it **must** be materialized
before editing (`EditClipsByBinding` sets that precedent by calling `.ToHashSet()`). Deriving
the appended slot-`j` binding is then `binding` with `propertyName` replaced — the `Type` is
inherited from the observed binding, which is exactly right.

This also handles the `Renderer`-vs-`SkinnedMeshRenderer` case naturally: every observed
binding at that path and slot is rewritten, whatever its declared type.

---

## 8. Consistency between analysis and mutation

The handoff proposes a narrow rule. **It is sound, and needs three additions.**

Proposed rule, verified:

- inspect the real virtual curve that will be edited — **yes**, and it must be the actual
  `ObjectReferenceKeyframe[]` from `GetObjectCurve`, not a re-read of evidence;
- every non-null material value must exist in the source→opaque mapping — **yes**;
- an unrecognized value refuses before any curve or renderer mutation — **yes**;
- fewer reachable values than the captured set may remain safe — **yes**. The captured set is
  conservative (structural enumeration, no reachability solver), so a subset is strictly less
  than what was proven. Missing values invalidate nothing.
- timing changes do not affect correctness if every value remains covered — **yes**. The proof
  quantifies over the *set* of admitted materials, never over time. `AlphaResolution` and
  `IntersectOutcomes` carry no temporal component.

**Additions required:**

1. **Marker clips are a hard refusal, checked before mutation.** A `VirtualClip` with
   `IsMarkerClip` silently swallows `SetObjectCurve`. Validation must reject any target clip
   that is a marker rather than discover the no-op afterwards. This is the single most
   dangerous silent-failure mode in the whole design.
2. **Validate every slot the feature will touch, across every target clip, before mutating
   any of them.** Per §7.3, one clip can carry several target bindings.
3. **A newly-appeared binding at a target path/slot that was absent from evidence is a
   refusal**, not merely an unrecognized value. Its presence means the graph changed after
   analysis, which invalidates the slot's proof, not just the mapping.

**Scope of all three: the slot, not the renderer.** Each refusal above invalidates only the
slot whose binding produced it. The failed slot keeps its original submesh, material
assignment and swap curve; independently valid slots on the same renderer survive. §11 sets
out the ordering that makes this safe — validate every candidate slot first, then finalize
against the survivors, then apply once.

**[INFERENCE]** No live Unity object need enter the immutable proof graph for any of this.
Validation compares *live curve values* against a *host-side prepared mapping*; the immutable
evidence supplies the admitted indices, and the mapping is a transient host artifact (§9).

---

## 9. Same-capture material / evidence / render-state carrier

### Recommendation: a **transient host-side sibling**, not an extension of the evidence graph

**[INFERENCE]** The evidence graph's no-live-Unity-object guarantee is load-bearing and
already documented in three places (`LiveAnimationObservation`'s file header,
`CommittedControllerGraph`'s type comments, `AmusePlatformFinishState.AnimatorBindings`).
`AmusePlatformFinishState.AnimatorBindings` is the **existing precedent**: a live, transient
host capability held on build state, explicitly outside the evidence graph. The carrier should
follow that pattern exactly.

Smallest shape that closes seams A, B and E, per renderer:

| Field | Why |
|---|---|
| admitted-material index | the join key to `CapturedAnimationEvidence.AdmittedMaterials` |
| live build-copy `Material` | required by `PrepareCanonicalOpaqueClone` and by slot assignment |
| captured source attestation | already on `CapturedAlphaMaterial` — referenced, not copied |
| derived admitted `CapturedMaterialEvidence` | conversion eligibility is evaluated on *admitted* evidence, so animated render-state properties are accounted for. Under shape 2 below this is the **conversion-admitted** derivation, produced only for slots that yielded opaque candidates |
| effective render queue | `ReadEffectiveRenderState` out-param |
| effective `RenderType` | `ReadEffectiveRenderState` out-param |
| alpha resolution | already produced by `ResolveSlot`, currently only aggregated |
| conversion eligibility | `EvaluateVerifiedEligibility` result |

It satisfies every prohibition: it recaptures nothing later (queue and `RenderType` are read
once, in the same pass as the evidence); it does not duplicate admission (it *indexes* the
existing admitted list); it puts no live object in `MaterialSemantics`; it does not touch
`CapturedAnimationEvidence`; it is not a material IR, a shader-family registry, or a planner
framework.

### The blocking problem this exposes — **[DECISION] required**

**[SOURCE]** Conversion eligibility reads 24 properties that the alpha capture never gathers
(§2.1). Closing seam E means the closed request must include
`PoiyomiOpaqueConversion.ConversionEvidenceRequest`. But `evidence.RelevanceRequest` is *also*
what `ResolveProofRelevant` and `IsUnrecognizedObjectMaterialBinding` use to decide which
animated bindings are proof-relevant.

Widening the closed request therefore makes animating any of those 24 render-state properties
(e.g. `_Cutoff`, `_ZWrite`, `_Mode`) proof-relevant, and an animated property that cannot be
admitted to a singleton becomes a **renderer-scoped refusal** — including for renderers where
alpha analysis alone would have succeeded and no conversion was ever going to be attempted.

`PoiyomiOpaqueConversion`'s own doc comment anticipates and warns against exactly this:

> "Folding conversion-only render state into the alpha request would make ordinary alpha
> analysis refuse on state alpha does not depend on — a coverage regression, not a safety
> improvement."

### The root cause: capture schema and proof relevance are conflated

**[SOURCE]** `CapturedAnimationEvidence.RelevanceRequest` is a single `MaterialEvidenceRequest`
serving **two different roles at once**:

- **capture schema** — what `UnityMaterialEvidenceCapture.Capture` gathers from each material;
- **proof relevance** — which animated bindings `ResolveProofRelevant` and
  `IsUnrecognizedObjectMaterialBinding` treat as proof-relevant or unrecognized.

Those roles have genuinely different correct answers. Capture wants the **union** of
everything any decision might read. Relevance wants **only what the decision being made
actually depends on**. Conflating them is what makes widening capture widen refusal.

`MaterialEvidenceRequest.Combine` makes the union mechanically trivial, which is precisely why
this needs a decision rather than an implementation reflex.

### Three shapes

**1. One combined capture-and-relevance request.**
Sound — animating `_Cutoff`, `_ZWrite` or `_Mode` would be admitted or refused, never ignored.
But it unnecessarily widens *ordinary alpha* refusal: a renderer that was never going to be
converted still refuses on render state its alpha proof does not depend on. This is the
coverage regression `PoiyomiOpaqueConversion`'s doc comment warns about.

**2. One combined capture schema, two decision-specific relevance/admission paths —
RECOMMENDED.**

- capture alpha **plus** conversion evidence once, in the existing single closed capture;
- ordinary alpha proof resolves **only alpha-relevant** bindings, exactly as today, so alpha
  coverage is unchanged;
- **only after a slot produces opaque candidates**, conversion admission resolves the
  **conversion-relevant** bindings for that slot's materials;
- conversion eligibility consumes the resulting derived evidence, so animated `_Cutoff`,
  `_ZWrite` or `_Mode` is properly admitted rather than ignored;
- a conversion failure **preserves that slot** and does not retroactively invalidate unrelated
  alpha analysis on the same renderer.

This keeps both properties that matter: conversion is never decided on unadmitted render
state, and alpha analysis never refuses on state it does not depend on. It requires separating
the two roles above narrowly — an evidence request that is captured, and a per-decision
relevance set that is resolved — **not** a generic evidence framework or a universal request
system. `AdmittedMaterialStates.Admit` already performs exactly this "group bindings, admit
against this material's own captured default, accumulate derived evidence" operation; the
prerequisite is to let it run a second time against a different relevance set, not to invent a
new mechanism.

**[INFERENCE]** This also composes correctly with §2.2: once closure is renderer-scoped, the
conversion admission pass runs over a materially smaller, genuinely local material set.

**3. A later second material capture.**
**Rejected.** It rereads mutable material state after the evidence a decision depends on, and
duplicates closure and admission. `GatherConversionSourceEvidence` deliberately offers no
live-`Material` overload for exactly this reason.

**[DECISION]** Shape 2 is the recommended direction and nothing in the repository contradicts
it; the controller should confirm it and confirm where the two roles are separated. This
remains an **independently mergeable prerequisite** — sequenced *after* §2.2 (§14–§15).

---

## 10. Generated-material lifecycle

**[SOURCE]** Normal references plus `BuildContext.Serialize()` are sufficient (§4.6). No
`IAssetSaver` interaction is required — for renderer slots *or* for curve-only references.

**Do not save during preparation.** Two independent reasons:

1. The prior investigation established that `Serialize()`'s cleanup destroys only components
   and GameObjects among saved-but-unreferenced assets — a saved-then-abandoned `Material` is
   welded permanently into the shipped container. That reading is confirmed here:
   `BuildContext.cs:270-277` skips anything that is not a `Component` or `GameObject`.
2. Saving writes to the asset database, which would make preparation observably mutating and
   contradict the prepare/apply boundary's whole purpose.

`PrepareCanonicalOpaqueClone` already matches this: *"Nothing is saved. Persistence belongs to
assignment, which is the consumer's job, so this method takes no asset saver and cannot
persist anything by accident."* It also leaves the clone unnamed, and documents that naming is
a consumer obligation because sub-asset names come from the object's own name and NDMF
guarantees no determinism.

**Cleanup on refusal.** **[INFERENCE]** A clone abandoned after a later refusal is an
unreferenced in-memory `Material` that survives until domain reload, so abandoned clones must
be destroyed. `PrepareCanonicalOpaqueClone` sets the precedent — it destroys its own clone
before throwing on a read-back disagreement.

But cleanup **must not be "destroy everything on any refusal"**: refusals here are slot-local,
and a clone can be shared by several slots, so destroying eagerly would strip a surviving slot
of a material it still needs. The rule is the single post-validation sweep of §11 step 3 —
destroy exactly those clones no **surviving** slot references, once the surviving slot set is
fixed. That covers preparation-time slot drops and second-window validation failures with one
mechanism, and it needs no reference counting.

---

## 11. Preparation, validation, atomicity, and local refusal

### `AmuseBuildOperation` cannot span the two passes — and does not need to

**[SOURCE]** `AmuseBuildOperation.Execute(lifecycle, assetSaver, prepare, apply)` invokes
`prepare(assetSaver)` and then `apply()` **synchronously, within one call**. It provides
lifecycle gating, a preparation phase that must return an explicit named refusal, an explicit
no-mutation outcome, a single `apply()` mutation boundary, no catch around either phase, and a
documented no-rollback contract. It has zero production callers.

Because the call is synchronous, it **cannot itself straddle the barrier pass and the second
animator-services pass.** An earlier draft of this note claimed both that it was sufficient
as-is and that it must span two passes; those cannot both be true, and the second is the false
one.

**Minimum honest design:**

- the **barrier pass** creates a feature-specific *prepared alpha-separation record* and stores
  it in build state (`AmusePlatformFinishState`, where `AnimatorBindings` already lives). It
  may hold **per-slot planning information** — dispositions, the per-slot source→opaque
  mapping, the separation plan, the geometry snapshot, the mutation target — and
  **candidate-independent preparation** such as generated opaque clones. It may **not** hold a
  finalized combined mesh or shared-material layout, because those are functions of the
  surviving candidate set, which is unknown until second-window validation (§11 ordering);
- the **second animator-services pass** validates every live virtual binding and value against
  that record, finalizes against the surviving slot set, and only then applies to the build
  avatar;
- `AmuseBuildOperation`'s existing semantics **may be reused for the second window's
  validate/apply boundary** if that fits cleanly — its prepare/apply split maps onto
  "validate live bindings" / "mutate", and its no-catch, no-rollback contract is exactly right
  there;
- otherwise use an equally narrow feature-specific call path.

**Do not change `AmuseBuildOperation`, and do not design a generic cross-pass transaction.**
Whether the second window reuses it or uses a narrow feature-specific path is an
implementation-time judgement, not something this investigation should settle.

### Ordering that satisfies the atomicity requirement

Validation is **late** — it happens in the second window, after preparation — and its failures
are **slot-local**. Those two facts together force a step that a naive prepare/apply split does
not have: **finalization after validation**, because what gets built depends on which slots
survive.

**Mutation terminology, used precisely below.** Three different things are easy to conflate:

- **Source-asset mutation** — writing to the authoring inputs (source meshes, materials,
  clips, controllers, prefabs). **Never happens, at any step.**
- **Build-avatar mutation** — writing to the NDMF build copy: renderer `sharedMesh` /
  `sharedMaterials`, and virtual clip curves. This is the boundary that matters for atomicity.
- **Constructing AMUSE-owned transient objects** — creating or editing generated `Material`
  and `Mesh` instances that are not yet referenced by the build avatar. This is **allowed
  during preparation and finalization and is not build-avatar mutation**; nothing observes
  those objects until they are assigned.

So "analysis, validation and finalization happen before any build-avatar mutation" is the
claim — not "before anything is created".

```
BARRIER PASS (no extension) — analysis and per-slot preparation
                              no source mutation, no build-avatar mutation
  per renderer: evidence → resolutions → geometry snapshot → plan
  per CANDIDATE slot i: conversion eligibility, opaque clone(s), per-slot mapping
    (creating clones is transient-object construction, not build-avatar mutation)
    a preparation refusal on slot i drops slot i ONLY; other slots continue
  → prepared record: per-slot planning info + candidate-independent preparation
    (NO finalized combined mesh, shared-material layout, or curve-edit set — see below)

SECOND WINDOW (AnimatorServicesContext active)

  1. VALIDATE — every candidate slot; reads only, nothing created or written
       for EACH candidate slot i, independently:
         discover this slot's live bindings from the real virtual clips
         marker clip / value absent from slot i's mapping / newly-appeared binding
           → invalidate slot i ONLY
       → surviving slot set S      (S may be empty ⇒ nothing is applied at all)

  2. FINALIZE against S — may construct transient objects;
                          still no source or build-avatar mutation
       derive from S: appended slot indices, the combined generated mesh,
       shared-material layout, and the complete curve-edit set

  3. SWEEP — destroy every generated transient clone no slot in S references

  4. APPLY — the first build-avatar mutation: curve edits, then
             sharedMesh / sharedMaterials
```

- **Every candidate slot is validated before any build-avatar mutation.** Step 4 is the first
  write the build avatar can observe; steps 1–3 leave the build avatar and every source asset
  byte-for-byte as they were.
- **A validation failure invalidates only that slot's transformation.** The failed slot keeps
  its **original submesh, its original material assignment, and its original material-swap
  curve** — AMUSE writes nothing for it. Other independently valid slots survive. Slots are
  already independent in `ResolveSlot`, and §7's per-slot mapping domain keeps them so.
- **A mesh prepared for the original candidate set is not valid once `S` differs from it, and
  must not be applied unchanged.** Dropping one splitting slot changes the output submesh set,
  and therefore the appended slot indices — which the surviving slots' own slot-`j` curves
  name. This is why appended indices, the combined mesh, the shared-material layout, and the
  curve-edit set are all products of step 2 rather than of preparation.
- **Clone cleanup is the single sweep in step 3.** A clone is destroyed exactly when no
  surviving slot references it, which naturally handles a clone shared by slots `i` and `k`
  when only `i` fails. **No reference counting** — the sweep already expresses the rule.
- An unexpected defect during apply propagates and is build-fatal — the existing no-catch
  policy, and the correct outcome given a possibly half-mutated build avatar. A *validation*
  failure is not a defect: it is an ordinary slot-scoped refusal reached before any
  build-avatar write.
- **Renderer-wide material-property curves** remain governed by the existing opaque-conversion
  overwrite rule. The prior investigation's finding that such curves are slot-count-invariant
  means a renderer-wide curve also applies to appended slot `j` — which is consistent, since
  `j` holds the opaque counterpart of the same source material and the property was already
  admitted for it.

### Can step 2 be done narrowly today? — **unresolved feature-design blocker for `Split`**

**The open question is: how does AMUSE retain or obtain full-fidelity mesh state for
post-validation finalization?** It is deliberately phrased that way rather than as "the second
window needs a late live mesh read" — a late read is one candidate answer, not the problem
statement.

**Milestone A (wholly opaque) — not a blocker.** There is no generated mesh. Finalizing against
`S` is choosing which slot assignments and which curve edits survive, both already per-slot
data in the prepared record. No new capability is needed.

**Milestone B (`Split`) — unresolved.**

**[SOURCE]** The barrier's immutable `UnityRendererAlphaSnapshot` carries only `VertexCount`,
`Positions`, `Uv0`/`HasUv0`, per-submesh `Indices`, and `Materials`. It deliberately does
**not** carry normals, tangents, colors, UV1–UV7, bone weights, bindposes, or blendshape
frames — all of which the prior investigation's preservation checklist requires an output mesh
to copy exactly. So the carried evidence is sufficient to decide *which triangles go where*,
but **not** to construct the output mesh.

**[INFERENCE]** That establishes a gap; it does **not** establish which way the gap is closed.
At least three routes are open, and this investigation selects none of them:

1. **Prepare a candidate-independent full-fidelity generated clone before the second window,
   then finalize only its submesh/index layout against `S`.** Attractive because copying every
   vertex attribute and blendshape is candidate-*independent* — only the index/submesh layout
   depends on `S` — so the expensive, fidelity-critical part could happen in the barrier while
   the `S`-dependent part stays trivial. Needs design: what exactly is candidate-independent,
   and whether a layout-only rewrite of an already-built mesh is clean.
2. **Retain a sufficiently complete immutable reconstruction snapshot.** Keeps finalization
   purely on immutable evidence, consistent with AMUSE's existing preference. Needs design and
   a decision about cost and scope, and this note does **not** propose expanding
   `UnityRendererAlphaSnapshot` to do it.
3. **Perform a guarded late read of the live build mesh in the second window.** Simplest to
   state, but it reads mutable state after the evidence a decision depends on, which is the
   pattern AMUSE elsewhere avoids. Needs a real guard — see below.

**Every route needs characterization or design before it is chosen, and non-readable meshes are
the sharpest test of all three.** The existing public `MeshReadabilityCharacterizationTests`
covers positions, UVs and index reads only; whether bone weights, bindposes and blendshape
frames are reliably obtainable from a non-readable mesh in the Editor is an open question the
prior investigation already reopened. A route that works only for readable meshes is not a
solution for real avatars.

**[SOURCE] + [INFERENCE] — the identity check is not the guard.**
`UnityRendererMutationTarget` carries `Renderer`, `ExpectedMesh` and
`ExpectedMaterialSlotCount`, and it is tempting to read that as already licensing route 3. It
does not. `ExpectedMesh` is a **reference identity** check: it detects the renderer being
pointed at a *different* `Mesh`, and nothing else. Another NDMF pass can mutate the *same*
`Mesh` instance in place — rewriting vertices, blendshapes or submesh descriptors — with its
identity unchanged, and the check would still pass while the analyzed content is gone.
**Whatever contract route 3 would need, the existing identity check does not establish it.**

**Recorded as an unresolved feature-design blocker, deliberately not resolved here.** What this
note explicitly does **not** do about it:

- it does **not** broaden the refusal to renderer scope to make the problem go away — that
  would trade a design question for a coverage regression and violate local refusal;
- it does **not** select among the three routes, or design one;
- it does **not** propose a generic cross-pass transaction, a mesh-rebuild framework, an
  expanded snapshot, a mesh cloner, or a new prerequisite branch.

It is scoped to Milestone B and is not a blocker for Milestone A, so it does not gate starting
the feature.

---

## 12. Supported and refused animation forms

**Supported (first feature):**

- material-swap object curves on `m_Materials.Array.data[i]` at the renderer's path, any
  declared binding type, in any number of non-marker clips, across any number of
  `AnimatorController`s, reached from any number of graph locations;
- clips sharing display names;
- the same admitted material appearing in several slots or several clips;
- slots with a single admitted material (unchanged from today) **and** slots with several
  admitted materials (**new** — this is what the rewrite unlocks).

**Refused:**

| Refusal | Scope | Status |
|---|---|---|
| Special / marker motion carrying a target binding | slot | **new — must be named** |
| Curve value absent from the prepared mapping | slot | **new — must be named** |
| Target binding present in the second window but absent from evidence | slot | **new — must be named** |
| Admitted material not conversion-eligible | slot | existing `PoiyomiOpaqueConversionRefusal` |
| Synced layers, override controllers, virtualized motions, animation events | avatar | existing |
| Additive layer / unnormalized direct blend tree with proof-relevant property | renderer | existing |
| Unrecognized animated material binding | renderer | existing |

**[DECISION]** Which vocabulary the three new refusals belong to —
`RendererAnalysisRefusal`, `PoiyomiOpaqueConversionRefusal`, or a new transformation-scoped
enum — is unresolved. The prior investigation deferred the same question for the
multi-admitted-material condition and explicitly said not to add an enum member yet. That
still holds.

---

## 13. YAGNI review of every proposed new type or helper

| Proposal | Current consumer | What existing code cannot express | Second concrete consumer? | Can the feature ship without it? |
|---|---|---|---|---|
| **Third pass reactivating `AnimatorServicesContext`** | this feature | there is no way to reach a `VirtualClip` after the barrier — RED-proven | no | **No.** Load-bearing. |
| **Transient host-side material carrier** (§9) | this feature | live `Material` ↔ admitted evidence ↔ render state pairing is destroyed in `CaptureObserved`'s local scope | no | **No.** Without it, conversion cannot be attempted at all. |
| **Retained `MeshSeparationPlan` + `UnityRendererMutationTarget` on build state** | this feature | both are computed and dropped today | no | **No.** Already identified as the first required change by the prior investigation. |
| **Prepared alpha-separation record carried in build state** | this feature | `AmuseBuildOperation` is synchronous and cannot straddle passes; nothing today carries a prepared result between passes | no | **No**, but its *shape* is reviewable — it could be a plain field on `AmusePlatformFinishState`, not a new type. `AmuseBuildOperation` itself is unchanged. |
| **Three new refusal names** (§12) | this feature | today the conditions are unrepresentable | no | **No**, but see the [DECISION] on vocabulary. |
| **Combined / split evidence request** (§9) | this feature | conversion properties are not captured at all | no | **No** — but which of the three shapes is a [DECISION]. |
| **Clone reference-counting by admitted index** | *none* | nothing — the post-validation sweep (§11 step 3) already destroys exactly the clones no surviving slot references | no | **Yes.** **Do not build.** |
| **Post-validation finalization of the surviving slot set** (§11 step 2) | this feature | validation is slot-local and late, so appended slot indices, the combined mesh, the shared-material layout and the curve-edit set are not knowable during preparation | no | **No.** Without it a slot-local failure would have to escalate to renderer scope. Narrow for Milestone A; **unresolved for `Split`** (§11). |
| **General curve-remapping framework** | *none* | nothing — `AnimationIndex` already does this | no | **Yes.** **Do not build.** |
| **Animation dependency / reachability graph** | *none* | nothing this feature needs | no | **Yes.** **Do not build.** §8 shows reachability is not required. |
| **Changes to `MeshSeparationPlan`'s contract** | *none* | nothing | no | **Yes.** **Do not change.** |
| **Changes to `MaterialSemantics`** | *none* | nothing | no | **Yes.** **Do not change.** |

---

## 14. Remaining blockers for the first alpha-separation feature

**Feature scope reminder.** The approved product feature covers **both**
`WhollyOpaqueCandidate` **and** `Split`. Nothing in this investigation narrows that. The
sequencing below is about *internal implementation milestones*, not about redefining what the
feature is.

**Pre-existing, unchanged:**

1. Runtime texture evidence for texture-backed triangle proof on real avatars.
2. Mesh cloning — no facility exists in NDMF or this repository outside tests. *Interface with
   this result:* mesh cloning is required **only** for the `Split` disposition;
   `WhollyOpaqueCandidate` needs no mesh work, being a slot assignment plus a curve rewrite.
   That asymmetry makes wholly-opaque a sensible **first internal milestone**, not a smaller
   feature.
3. Whether non-readable meshes expose the full reconstruction set (`Split` only).

**Raised by this note:**

4. **Renderer-scoped material-swap closure (§2.2).** Blocking, and first: while closure is
   graph-wide, an unrelated renderer can refuse this one, and any mapping built over
   `AdmittedMaterials` would be wrong.
5. **The conversion-evidence capture/relevance boundary (§9).** Blocking: conversion
   eligibility cannot be evaluated at all until it is settled.
6. **How AMUSE retains or obtains full-fidelity mesh state for post-validation finalization
   (§11).** Because validation is late and slot-local, a dropped slot changes the output
   submesh set and the appended slot indices, so a mesh prepared for the original candidate set
   cannot be applied; and the carried snapshot lacks the vertex attributes needed to build the
   mesh against the surviving set. Three candidate routes are recorded in §11 and **none is
   selected**. **Unresolved feature-design blocker for Milestone B only** — not a blocker for
   Milestone A, which generates no mesh.
7. Refusal-vocabulary placement (§12).
8. The prepared-record hand-off between the barrier and the second window (§11) — a shape
   question, not a blocker.

---

## 15. Proposed next branches and milestone order

**[DECISION]** Recommended sequencing. Deliberately *not* elaborated into a plan.

**Prerequisite 1 — `fix/scope-material-swap-closure-to-renderer` (§2.2).** Independently
mergeable: it narrows `CaptureObserved`'s admission to the renderer being analyzed, it has a
self-contained correctness question, and it has the three falsifiers listed in §2.2. It comes
first because it changes what the admitted set *is*, which the conversion boundary then
operates on.

**Prerequisite 2 — the conversion-evidence capture/relevance boundary (§9).** Independently
mergeable: it separates capture schema from proof relevance narrowly (shape 2), so conversion
evidence is captured once without widening ordinary alpha refusal. Every later increment
depends on it.

Per `CLAUDE.md`'s prerequisite rule, each should be started from fresh `main`, completed and
reviewed, and the consumer resumed from updated `main` afterwards.

**Then the first alpha-separation product feature**, whose approved scope is
`WhollyOpaqueCandidate` **and** `Split`. Suggested internal milestone order within it:

- **Milestone A — wholly opaque slots**, including material-swap curve rewriting. Exercises
  the whole lifecycle, carrier, preparation, late slot-local validation, finalization against
  the surviving slot set, the clone sweep, and the single apply boundary — without needing
  mesh cloning. Finalization is trivial here because no mesh is generated: it selects which
  slot assignments and curve edits survive.
- **Milestone B — `Split`**, adding the appended submesh, the generated mesh, and the appended
  slot-`j` curve. Depends on the mesh-cloning prerequisite **and** on resolving how AMUSE
  retains or obtains full-fidelity mesh state for post-validation finalization (§11,
  blocker 6) — that question should be settled before Milestone B starts, not discovered
  inside it.

**Milestone A is not the completed feature and must not be declared or enabled as one.**
Shipping wholly-opaque support alone would be a product-scope change, and this investigation
does not make it. If the controller wants that, it needs separate approval.

**Not in this feature at all:** lilToon opaque conversion, UV repacking, texture modulation,
material simplification, profitability planning, Android texture evidence, generic optimizer
cooperation.

---

## 16. Files, tests, console, git status

**Created — three files, all untracked and uncommitted:**

1. `docs/superpowers/investigations/2026-08-28-alpha-separation-animation-rewrite.md` — this
   note.
2. `Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorServicesReactivationCharacterizationTests.cs`
   — the retained characterization.
3. `Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorServicesReactivationCharacterizationTests.cs.meta`
   — its Unity-generated importer meta, kept as one logical unit with the `.cs`. GUID
   `19ff73e891b7f473c8a770370019aa95`. Trailing whitespace was stripped from its three empty
   scalar lines to match the neighbouring committed `AnimatorBindingsLifetimeGateTests.cs.meta`;
   the GUID is unchanged.

**Modified:** none. No production file was touched.

**Tests actually run (Unity 2022.3.22f1 EditMode, instance identity confirmed by exact
normalized `Application.dataPath` match to `/Users/user/Documents/AMUSE/Assets`):**

| Run | Result |
|---|---|
| `AnimatorServicesReactivationCharacterizationTests` (initial) | **2 passed, 0 failed** |
| Same, with reactivation removed (**RED probe**) | **1 failed** — `Extension … not active` |
| Same, declaration restored (**GREEN**) | **2 passed, 0 failed** |
| `Alrauna.Amuse.Tests.Editor` (full product suite) | **1298 passed, 0 failed** (46.8 s) |
| `Alrauna.Amuse.Research.Tests.Editor` | **138 passed, 0 failed** |

The full product suite was run because a durable test artifact is retained and because the new
file adds an assembly-level `ExportsPlugin`, which registers globally. The build log confirms
the new plugin appears as `Incompatible:` on every other platform, and no existing test's pass
set changed.

**Not rerun after the review revisions, deliberately.** Those revisions changed only this
note and the `.meta`'s trailing whitespace. The characterization's `.cs` was verified
byte-identical to the file that produced the GREEN run above
(`sha256 5add06ac9c28b9d65991f5525bbe4f343a63694ea68851fc798037e6686ee878`, unchanged), so its
executable content is unchanged and the recorded results still stand.

**Console:** no unexpected errors. Present entries are `InvalidOperationException: synthetic
preparation failure` / `synthetic post-mutation failure` (deliberate, from
`AmuseBuildOperationTests`) and `mprotect returned EACCES` (a macOS Mono artifact of the MCP
code-execution channel, unrelated to AMUSE).

**Census Lab:** not used, not opened, not modified. The second Unity instance
(`AMUSE-Census-Lab`, port 6402) was identified during instance disambiguation and deliberately
never targeted.

**Final `git status --porcelain`:**

```
?? Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorServicesReactivationCharacterizationTests.cs
?? Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorServicesReactivationCharacterizationTests.cs.meta
?? docs/superpowers/investigations/2026-08-28-alpha-separation-animation-rewrite.md
```

No staging, commit, push, PR, branch deletion, or history change occurred.
