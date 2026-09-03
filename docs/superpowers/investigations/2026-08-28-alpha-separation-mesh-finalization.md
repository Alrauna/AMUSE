# Alpha Separation — Mesh Finalization Prerequisite

This is an investigation note. It contains no production code and no implementation plan.

Each claim below carries a tag:

- **[SOURCE]** — read directly from pinned repository or vendor source.
- **[MEASURED]** — observed from a characterization or probe run in this repository.
- **[INFERENCE]** — architectural reasoning built on the tags above.
- **[DECISION]** — a controller decision. This note does not make one.

**Recommendation: A. The native clone is sufficient.** Two obligations apply, named and
measured in §7.2. One clone trigger is narrowed in §7.5. See §9 for full detail.

**A note on wording.** Claims that the source mesh stays unchanged mean the mesh's
**characterized logical state** stays unchanged. That state is the field list in §6, compared
through the test's `Describe` digest. `Describe` is a selected structural digest. It is not a
byte comparison of the mesh. An earlier draft of this note wrongly called this state
"byte-identical". This note narrows every such claim.

---

## 1. Branch, base, repository state

| | |
|---|---|
| Branch | `investigate/alpha-separation-mesh-finalization` |
| Created from | `main`, verified equal to `origin/main` (0 ahead, 0 behind) |
| Base SHA | `5c413532137c7eb62de8d6164f404f645f112c82` |
| Working tree at branch creation | clean |
| Unity / NDMF | 2022.3.22f1 / NDMF 1.14.4 (pinned, embedded) |
| Census Lab | **not used, not inspected, not modified** |

`Packages/manifest.json` and `Packages/packages-lock.json` changed with the documented
host-generated churn during the compile and test cycles. Only three packages changed:
`com.unity.toolchain.macos-arm64-linux-x86_64`, `com.unity.sysroot`, and
`com.unity.sysroot.linux-x86_64`. The complete diff was inspected. No intentional change
touches those files. The restore that `.omp/AGENTS.md` §Unity package and MCP safety
prescribes was applied.

No fetch, pull, push, stage, commit, PR, or history change happened. No other branch was
touched.

---

## 2. The question

The prior investigation (`2026-08-28-alpha-separation-animation-rewrite.md`, §11) left one
feature-design blocker open for Milestone B (`Split`):

> How does AMUSE retain or obtain full-fidelity mesh state for post-validation finalization?

Validation happens **late**, and it is **slot-local**. The surviving candidate set `S` stays
unknown until the second animator-services window validates every candidate slot on its own.
The output submesh set is a function of `S`. The appended slot indices that the surviving
slots' own curves name are also a function of `S`. So the mesh cannot be finalized during
preparation. But the fidelity-critical vertex data must survive the wait.

This note tests the prior investigation's **route 1**, and nothing else:

1. Before the second window, create an unassigned AMUSE-owned native clone of the effective
   build mesh.
2. Mutate no source mesh, renderer, build avatar, or animation curve.
3. Validate every candidate slot independently in the second window.
4. Once `S` is known, finalize **only** the clone's submesh/index layout against `S`.
5. Assign the finalized clone at the final build-avatar mutation boundary, and only there.

---

## 3. Withdrawn requirement: non-readable mesh reconstruction

The prior investigation said a route that "works only for readable meshes is not a solution
for real avatars". It called non-readable meshes "the sharpest test" of all three routes.

**This note withdraws that statement.** Current VRChat SDK policy requires Mesh Read/Write to
stay enabled for avatar upload. So a non-readable mesh is not a supported input to a VRChat
avatar build. Non-readable mesh compatibility is therefore **not an acceptance criterion** for
this feature.

What survives the withdrawal:

- `MeshReadabilityCharacterizationTests` still shows that Editor analysis can read a mesh
  whose `isReadable` is false, by accident of how Unity works. It still justifies why
  `UnityRendererAlphaAnalysis` has no readability pre-check and no exception handling.
  **[SOURCE]** That comment stays accurate. Do not edit it.
- Incidental compatibility stays wherever Unity gives it for free. Nothing is built to get
  it on purpose.

**Not measured, on purpose:** whether `Object.Instantiate` on a non-readable mesh preserves
bone weights, bindposes, and blend shape frames. A measurement would need an asset fixture for
a property no supported input can show. If a future requirement admits non-readable meshes,
add that measurement then.

**This correction covers meshes only.** The established requirement to analyze ordinary
non-readable, mipmapped, and compressed **textures** stays untouched. `.omp/AGENTS.md`
§Alpha and texture direction still governs.

---

## 4. Repository grounding — what already exists

### 4.1 What exists, and what is genuinely missing

**Correction to an earlier draft of this note.** That draft claimed "no production hit for any
mesh operation", and wrongly claimed that no mesh construction, index rebuilding, or renderer
replacement existed "anywhere in AMUSE — production or test". The production half is right.
**The claim about tests was false.** This note withdraws it.

**[SOURCE] Production.** No AMUSE production code clones a mesh, rebuilds a submesh or index
layout, manages a generated mesh's lifetime, replaces a renderer's mesh, preserves a source
mesh, or copies attributes, skinning, or blend shapes. AMUSE production code creates exactly
one Unity object today: a `Material` (§4.3). It has never created a `Mesh`. That gap is real,
and this note addresses it.

**[SOURCE] Tests.** Several existing test fixtures build meshes and write index data:

| Fixture | What it does |
|---|---|
| `UnityRendererAlphaAnalysisTests` | `new Mesh()`, `subMeshCount = 2`, `SetTriangles` per submesh, a `MeshTopology.Quads` case, `sharedMesh` assignment |
| `UnityRendererAlphaSnapshotTests` | private `Triangle()` / `Quad()` builders — positions, `uv`, `SetTriangles`, `sharedMesh` assignment |
| `RendererAlphaAnalysisIntegrationTests` | two-submesh meshes via `SetTriangles`, and an assertion that `subMeshCount` stays unchanged after analysis |
| `UnityAnimationCharacterizationTests` | single-triangle meshes assigned to `sharedMesh` |
| `AmuseBuildOperationTests`, `AmusePlatformFinishPluginTests` | private `NewTriangleMesh()` / `BuildLowerMipBuildMesh()` |

**Can this note reuse any of them? No. None of them characterizes this requirement.**

1. **None is shared.** Every builder is a `private` method bound to its own test class. Most
   bind to that class's own `Track` list too. `Tests/Editor` has no shared mesh helper
   anywhere. The only shared directories are `ReferenceFixtures` (material and shader
   reference data) and `TestInfrastructureSmokeTests`.
2. **Every one is an analysis *input*, not output construction.** They carry positions, `uv`,
   and triangles, because that is exactly what `UnityRendererAlphaSnapshot` consumes. None
   carries normals, tangents, colors, multiple UV channels, bone weights, bindposes, blend
   shapes, a non-default index format, or a non-default submesh descriptor. So none can catch
   a lossy clone or a lossy layout rewrite.
3. **None clones or finalizes anything.** No existing test calls `Object.Instantiate` on a
   mesh, reads a `SubMeshDescriptor`, or rewrites a submesh layout.

So the honest statement is this: the repository has ample precedent for *building a small mesh
in a test*, and none for *cloning or finalizing one*. Reusing a three-vertex analysis fixture
here would have produced a test that passes under every implementation this note needed to
tell apart. This note instead wrote a new adversarial fixture (§6), and introduced no shared
abstraction for it.

### 4.2 What is reused

| Existing mechanism | Role here |
|---|---|
| `UnityRendererAlphaSnapshot` / `UnitySubmeshAlphaSnapshot` | Unchanged. Decides *which triangles go where*. The clone supplies the data to build with. Complementary. The snapshot needs no new fields. |
| `MeshSeparationPlan.RequiresAnySplit` | **The clone trigger** (§7.5). Already exists. No new predicate is needed. |
| `UnityRendererMutationTarget` | Unchanged. Still a reference-identity check on `Renderer` / `ExpectedMesh` / `ExpectedMaterialSlotCount` (§8.1). |
| `PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone` | The **precedent**, not a dependency. Same shape, one level down: clone the source, write only what must change, never save, leave naming to the consumer, destroy the clone on refusal. |
| `AmuseBuildOperation` | Unchanged. Still zero production callers. Synchronous, so it cannot straddle passes — the prior investigation established this, and this note does not revisit it. |
| `AmusePlatformFinishState` (NDMF `context.GetState<T>()`) | The carrier. `AnimatorBindings` is the existing precedent for a live transient host capability held there, explicitly outside the immutable evidence graph. **[SOURCE]** |
| `AnimatorServicesReactivationCharacterizationTests` | Reused as evidence, not extended (§7.6). |
| `MeshReadabilityCharacterizationTests` | Reused as evidence for §3. Not modified. |

### 4.3 Why the snapshot and mutation target have their current shapes

**[SOURCE]** Commit `902fc3f`, *"refactor: analyze renderers from immutable capture"*,
introduced `Editor/Host/UnityRendererAlphaSnapshot.cs`. It is the file's only commit. The shape
is deliberate. The snapshot is immutable. It defensively copies every list. It carries only
what alpha *analysis* consumes. The same commit split out `UnityRendererMutationTarget`, to
keep the live `Renderer` reference out of the immutable half.

**[INFERENCE]** That split is why route 1 fits without a redesign. The snapshot is not
under-built for mesh output. It was never meant to be an output-construction source. Adding
normals, tangents, colors, UV channels, bone weights, bindposes, blend shape frames, **and now
submesh descriptors**, would turn analysis evidence into a mesh IR (intermediate
representation). `.omp/AGENTS.md` §Repository reality and scope forbids broad
infrastructure without a present requirement, and a general mesh IR is that kind of
infrastructure. A native clone already holds all of that data, in Unity's own
representation.

---

## 5. YAGNI ladder, evaluated in order

| # | Question | Answer |
|---|---|---|
| 1 | Does the repository already have the mechanism? | **No** for meshes. The material-level pattern and the `RequiresAnySplit` trigger already exist (§4.1, §4.2). |
| 2 | Can `Object.Instantiate(mesh)` preserve the effective build mesh without custom copying? | **Yes — measured** for the characterized fields, submesh descriptors included (§7.1). |
| 3 | Can the finalized layout come from changing only submesh/index data on that clone? | **Yes — measured**, with two named obligations (§7.2) and one intentional representation change (§7.4). |
| 4 | Can existing NDMF/build-state facilities carry and clean up the transient clone? | **Yes.** Carry: [SOURCE + MEASURED]. Cleanup: [MEASURED] (§7.6, §7.7). |
| 5 | Does a concrete supported case fail? | **No supported case fails.** The ladder stops at step 3. |

---

## 6. The characterization

**File:** `Packages/com.alrauna.amuse/Tests/Editor/Host/MeshCloneFinalizationCharacterizationTests.cs`
(new, 6 tests). It characterizes **Unity, not AMUSE**. No AMUSE production type takes part.
Nothing in it is a contract AMUSE offers. It exports no plugin, registers no platform, and adds
no global component, so it cannot disturb an unrelated test.

**Scope.** The characterized fields are exactly these:

- vertex count, index format, submesh count, mesh bounds
- positions, normals, tangents, `colors32`
- UV channels 0, 3, and 7
- bone weights and bindposes
- per-submesh `SubMeshDescriptor` (topology, `indexStart`, `indexCount`, `baseVertex`,
  `firstVertex`, `vertexCount`, per-submesh `bounds`)
- stored and effective indices
- blend shape names, per-frame weights, and per-frame delta vertices, normals, and tangents

Claims in this note reach only that list, and no wider.

**The fixture is adversarial, not broad.** It is one 9-vertex mesh, built in memory:

| Fixture property | The wrong implementation it catches |
|---|---|
| `IndexFormat.UInt32` on a 9-vertex mesh | a copier that lets Unity pick the default `UInt16` — two matching defaults would prove nothing |
| UV channels 0, 3, 7 at **Vector2, Vector3, Vector4** | a copier that assumes `Vector2` everywhere, and drops components and channels |
| vertices 3 and 8, referenced by **no** triangle | a finalization step that compacts or reindexes vertices |
| a blend shape with **two frames**, plus a second shape | per-frame weights and deltas that store separately from shape names |
| mesh bounds authored **last**, unrelated to the geometry | implicit mesh-bounds recalculation (§7.2) |
| **per-submesh bounds authored** via `SetSubMesh`, also unrelated to the geometry | implicit per-submesh bounds recalculation — the second obligation, invisible without this |
| **the split submesh carries `baseVertex 4`**, so its stored indices differ from its effective ones | an implementation that ignores base vertex and builds triangles over the wrong vertices |
| the untouched submesh is a *different* submesh from the split one | a rewrite that cannot leave a neighboring submesh's descriptor alone |
| `colors32` with exact byte values | a float round-trip that quietly requantizes |

Vector comparisons use round-trippable `"R"` formatting. Unity's default vector `ToString`
rounds to two decimals, and a real difference could then compare equal by accident.

Teardown destroys every tracked mesh, including after a failing assertion. No test imports an
asset, touches an importer setting, or writes a file under `Assets/`.

---

## 7. Findings

### 7.1 Native cloning is lossless for the characterized state — **[MEASURED]**

`Object.Instantiate(mesh)` produced a distinct Unity object, with a different instance ID. It
kept every field listed in §6, exactly, including the **complete submesh descriptors**: the
nonzero `baseVertex 4` on the split submesh, its stored-versus-effective index distinction, and
both authored per-submesh bounds.

The source mesh's characterized state stayed unchanged after cloning, after finalizing the
clone, and after destroying the clone.

The clone carries the name `"<source>(Clone)"`. **[MEASURED]** Naming a generated asset is a
consumer obligation. `PrepareCanonicalOpaqueClone` already documents this rule, because
container sub-asset names come from the object's own name, and NDMF guarantees no determinism.

**Explicitly unmeasured:** modern variable bone weights (`GetAllBoneWeights` /
`SetBoneWeights`), UV channels other than 0, 3, and 7, and any other vertex layout the fixture
does not carry. **[INFERENCE]** `Object.Instantiate` copies the native vertex buffer and its
layout as a whole, not field by field. That is why the mixed-dimension UV channels and the
descriptors survived. So per-attribute enumeration is not expected to matter. That claim is
inference, not measurement. The descriptor characterization exposed no concrete need to expand
into a vertex-format matrix.

### 7.2 Layout-only finalization works, but "layout-only" is not free — two obligations

**This is the real finding. The first half came back RED.**

The first run of the characterization **failed**:

```
finalization recalculated bounds
  Expected: Center: (10.00, 20.00, 30.00), Extents: (20.00, 25.00, 30.00)
  But was:  Center: (7.00, 8.75, -3.00),   Extents: (7.00, 8.75, 10.00)
```

Isolating probes then pinned the full behavior, at both mesh level and submesh level:

| Operation | Mesh bounds | Per-submesh bounds |
|---|---|---|
| `Object.Instantiate(mesh)` | preserved | preserved |
| **raising `mesh.subMeshCount`** | **recalculated from the whole vertex buffer** | **recalculated for every existing submesh. The new submesh starts with zero bounds.** |
| `SetIndices(..., calculateBounds: false)` | untouched | untouched — so a shrunken submesh keeps stale bounds, and the appended submesh keeps **zero** bounds |
| assigning `mesh.bounds` / `SetSubMesh(..., DontRecalculateBounds)` afterwards | restored exactly | restored exactly |

So `calculateBounds: false` is **not enough**. It is not even the call that matters. Raising
`subMeshCount` recalculates *both* levels before any index gets written, and it recalculates
from *all* vertices, including ones no triangle references.

**Obligation 1 — mesh bounds.** Capture `mesh.bounds` before raising `subMeshCount`. Write it
back after. One line on each side.

**Obligation 2 — per-submesh bounds.** Capture each source submesh's descriptor `bounds`
before raising `subMeshCount`. Write them back after, via `SetSubMesh(..., DontRecalculateBounds)`.
This obligation stays invisible unless the fixture authors per-submesh bounds that differ from
the geometry. That is why the earlier version of this characterization missed it completely.

**The inheritance rule for the appended submesh.** It inherits its **source** submesh's
bounds. Its triangles are a subset of that submesh's, so the inherited bounds form a
conservative superset. That is the safe direction: bounds that are too large cost culling
performance, but bounds that are too small make geometry pop. The same rule covers the
shrunken split submesh, whose remaining triangles are also a subset. One rule, no per-case
branching.

Neither obligation is a route failure. Neither changes the recommendation. Both are exactly the
kind of silent fidelity loss this investigation set out to find, and source reading alone would
not have found either one.

The characterization now encodes both halves. `FinalizeLayout` performs the compensation. A
dedicated test pins the underlying Unity behavior, including the fact that
`calculateBounds: false` leaves the appended submesh's bounds at zero. If Unity ever changes
this behavior, the compensation must earn re-justification. It cannot stay silently in place.

### 7.3 Descriptor behavior, before and after finalization — **[MEASURED]**

Source, as authored:

```
[0] indexStart=0 indexCount=3 baseVertex=0 firstVertex=0 vertexCount=3
    bounds=(100,200,300)/(1,1,1)  effective=[0,1,2]        stored=[0,1,2]
[1] indexStart=3 indexCount=6 baseVertex=4 firstVertex=0 vertexCount=4
    bounds=(101,201,301)/(1,1,1)  effective=[4,5,6,5,6,7]  stored=[0,1,2,1,2,3]
mesh.bounds=(10,20,30)/(20,25,30)
```

After finalization (submesh 1 split, its opaque triangle appended as submesh 2, submesh 0
never rewritten):

```
[0] indexStart=0 indexCount=3 baseVertex=0 firstVertex=0 vertexCount=3
    bounds=(100,200,300)/(1,1,1)  effective=[0,1,2]  stored=[0,1,2]
[1] indexStart=3 indexCount=3 baseVertex=0 firstVertex=4 vertexCount=3
    bounds=(101,201,301)/(1,1,1)  effective=[4,5,6]  stored=[4,5,6]
[2] indexStart=6 indexCount=3 baseVertex=0 firstVertex=5 vertexCount=3
    bounds=(101,201,301)/(1,1,1)  effective=[5,6,7]  stored=[5,6,7]
mesh.bounds=(10,20,30)/(20,25,30)
```

Here is what the numbers show:

- **The untouched submesh keeps its whole descriptor**, bit for bit, including its authored
  per-submesh bounds, even though the rewrite shifted index data around it. In a variant probe,
  where the untouched submesh sat *after* the split one, Unity moved its index data and updated
  its `indexStart` on its own, while its effective indices stayed correct.
- **The split and appended submeshes get valid descriptors.** `indexCount` matches the indices
  written. `baseVertex + firstVertex` names the lowest vertex each submesh actually
  references (4 and 5). `vertexCount` spans the referenced range (3 and 3). Both checks assert
  a relationship, not a hardcoded constant, so they stay meaningful if the fixture changes.
- **Per-submesh bounds need explicit preservation** — see §7.2. Restoring `Mesh.bounds` alone
  is **not** enough.
- **`firstVertex` and `vertexCount` stay valid for CPU processing and skinning.** On the
  rewritten submesh, they are in fact *tighter* than the source's, which described a 4-vertex
  span for what is now a 3-vertex triangle.

### 7.4 Base-vertex normalization is representation-only, and safe — **[MEASURED]**

Rewriting the split submesh with effective indices normalizes its `baseVertex` from 4 to 0,
and its `firstVertex` correspondingly from 0 to 4. This is an **intentional representation
change**. This note records it as intentional because it was measured, not assumed. Two
grounds make it safe, and the characterization asserts both:

1. **The effective referenced vertices stay identical.** `Mesh.GetIndices(submesh)` applies
   the base vertex by default. The finalized submesh returns exactly the preserved-alpha
   triangle the source resolved to.
2. **The invariant `baseVertex + firstVertex` stays preserved.** Source: `4 + 0`. Finalized:
   `0 + 4`. Both name absolute vertex 4. The source spends the offset on `baseVertex`. The
   rewrite spends it on `firstVertex`. CPU processing and skinning need only the sum.

Representation identity is therefore **not** required here, and this note does not assert it.
What it asserts is effective behavior, plus a truthful descriptor.

### 7.5 Clone only when the plan requires a split — corrected

An earlier version of this note recommended cloning every candidate renderer during
preparation. **That recommendation was wrong. This note corrects it.**

**[SOURCE]** `MeshSeparationPlanner.Create` already computes `requiresAnySplit` as
`|= disposition == SubmeshSeparationDisposition.Split` across submeshes, and exposes it as
`MeshSeparationPlan.RequiresAnySplit`. The `Unchanged` and `WhollyOpaqueCandidate`
dispositions never set it.

**[INFERENCE]** A renderer whose submeshes are all `Unchanged` and/or `WhollyOpaqueCandidate`
needs **no geometry change at all**. Material assignment and curve rewriting handle a wholly
opaque submesh, which is exactly why the prior investigation called Milestone A "not a
blocker". Cloning such a renderer's mesh would allocate a full copy that finalization would
never touch, and that the sweep would always destroy.

**The corrected trigger: create the transient clone only after planning establishes
`plan.RequiresAnySplit == true`.** This needs no new predicate, and the check costs nothing.
The plan is already computed at that point in the barrier.

### 7.6 The second window does not obstruct the carry — **[SOURCE] + [MEASURED, existing]**

**[SOURCE]** NDMF passes are synchronous method calls within one
`AvatarProcessor.ProcessAvatar` invocation. `context.GetState<T>()` is a plain object held by
the `BuildContext`. Unity objects created in the Editor are not garbage-collected. They live
until an explicit destroy or a domain reload, and no domain reload can happen inside a
synchronous build.

**[MEASURED, by the existing `AnimatorServicesReactivationCharacterizationTests`]** A probe
object created in the first animator-services window stays intact through the barrier, and
again through the reactivated second window, across exactly the three-pass sequence this
feature uses. A transient `Mesh` reference is a field on such an object.

**[INFERENCE]** Nothing about the second window blocks carrying an unassigned transient clone
from preparation to finalization. `AmusePlatformFinishState.AnimatorBindings` is the existing
precedent for holding a live, transient host capability there.

**This note deliberately does not re-measure this fact.** Re-measuring it would mean either a
second exported plugin and a dedicated platform, or widening an existing focused
characterization. The first option is ceremony for a fact the source already settles. The
second would blunt a test that proves one thing well. If the controller wants it measured, the
cheapest form is one extra field and one extra assertion on the existing reactivation probe.

### 7.7 Cleanup follows the existing mechanism — **[MEASURED] + [SOURCE]**

**[MEASURED]** `Object.DestroyImmediate` on an abandoned, never-assigned clone destroys it.
Unity's overloaded equality then reports it null. The source mesh's characterized state stays
unchanged.

**[SOURCE]** `BuildContext.Serialize()` runs from `Finish()`, after every extension
deactivates. It walks assets **reachable from the avatar root** and saves the non-persistent
ones. Its cleanup pass skips anything that is not a `Component` or `GameObject`.
**[INFERENCE]** Two consequences follow, both matching the generated-**material** conclusions
the prior investigation already reached:

1. An unassigned transient clone is unreachable, so it never gets persisted. But it also never
   gets swept, so an abandoned clone survives in memory until domain reload, and code **must
   destroy it explicitly**.
2. A clone assigned to `sharedMesh` at the apply boundary is trivially reachable, and gets
   persisted with **no `IAssetSaver.SaveAsset` call**. Code should make none. Saving during
   preparation would weld an abandoned mesh permanently into the shipped container, and would
   make preparation observably mutating.

So the prior investigation's **single post-validation sweep** covers meshes without change:
once `S` is fixed, destroy exactly the transient objects no surviving slot in `S` references.
**No reference counting. No new lifetime mechanism.**

---

## 8. Remaining concrete risks

### 8.1 Same-instance in-place mutation by another pass — unchanged, and now smaller

**[SOURCE]** `UnityRendererMutationTarget.ExpectedMesh` is a *reference identity* check. It
detects a renderer pointed at a different `Mesh`. It cannot detect another NDMF pass
rewriting the same `Mesh` instance in place.

**[INFERENCE]** Route 1 **narrows** this risk. It does not resolve it. The clone is taken
during the barrier, at the same point as the analysis it must agree with. So the exposure
window is the barrier-to-clone gap, which is effectively zero, rather than the whole
barrier-to-second-window span a late live read would expose. A late read (route 3) would have
needed a real guard this check does not give. Route 1 does not need one.

The residual risk is the ordinary coexistence question of another optimizer mutating the same
renderer's mesh in the same phase. `.omp/AGENTS.md` §NDMF and mutation boundary already
treats that as an ordering and exclusion concern. **This note proposes no new mechanism for
it.**

### 8.2 Memory cost — bounded by split-requiring renderers

Not measured. The bound is **renderers whose plan has `RequiresAnySplit == true`** (§7.5), not
all candidate renderers. An earlier draft of this note overstated it. Renderers that are
wholly `Unchanged` or `WhollyOpaqueCandidate` are never cloned at all, so a build that finds no
splits allocates no mesh.

For the renderers that do get cloned, one full copy stays held from the barrier until the
post-validation sweep. **[INFERENCE]** It is transient. Route 2 (a reconstruction snapshot)
would hold the same data in a *less* compact managed representation. So this cost is not an
argument for another route.

### 8.3 Not measured

- Non-readable meshes (§3, deliberately out of scope).
- Modern variable bone weights, and vertex layouts the fixture does not carry (§7.1).
- Topologies other than `Triangles`. Renderer analysis already refuses non-triangle topology
  with `RendererAnalysisRefusal.UnsupportedTopology`, before the build reaches any of this.
  **[SOURCE]**
- The apply boundary itself. This branch stops at finalization, as instructed.

---

## 9. Recommendation — A. Native clone is sufficient

The narrow production design, with the exact existing seams to use:

1. **Preparation (barrier pass, no extension).** For each candidate renderer, after
   `UnityRendererAlphaAnalysis.CaptureGeometry` accepts and `MeshSeparationPlanner.Create`
   reports **`RequiresAnySplit == true`**, create
   `Object.Instantiate(mutationTarget.ExpectedMesh)`. A renderer with no split-disposition
   submesh is **not** cloned (§7.5). This step builds a transient object. It does not mutate
   the build avatar. Do not name the clone, do not save it, do not assign it.
2. **Carry.** Hold the clone on the prepared alpha-separation record in
   `AmusePlatformFinishState`, alongside the per-slot planning information the prior
   investigation specified. Follow the `AnimatorBindings` precedent.
3. **Validation (second animator-services window).** Unchanged from the prior investigation:
   validate every candidate slot independently, reads only, and produce `S`.
4. **Finalization against `S`.** On the clone only:
   - Capture `mesh.bounds`, **and** every source submesh's descriptor `bounds`.
   - Raise `subMeshCount`.
   - Call `SetIndices(..., calculateBounds: false)` for each rewritten and appended submesh.
     Leave submeshes whose triangle set stays unchanged un-rewritten.
   - Write back per-submesh bounds via `SetSubMesh(..., MeshUpdateFlags.DontRecalculateBounds)`.
     Each output submesh inherits its **source** submesh's bounds.
   - Write back `mesh.bounds`.

   Write nothing else. Base-vertex normalization on rewritten submeshes is expected and safe
   (§7.4). Index sets come from `MeshSeparationPlan` over `UnityRendererAlphaSnapshot`, which
   needs no new fields.
5. **Sweep.** Destroy every transient clone, mesh and material alike, that no surviving slot
   in `S` references. One sweep, no reference counting.
6. **Apply.** The single build-avatar mutation boundary: curve edits, then `sharedMesh` and
   `sharedMaterials`. NDMF's `Serialize()` persists the assigned clone with no `SaveAsset` call.

**This note proposes no additional infrastructure.** `UnityRendererAlphaSnapshot`,
`UnityRendererMutationTarget`, `MeshSeparationPlan`, `MeshSeparationPlanner`,
`AmuseBuildOperation`, and `MaterialSemantics` all stay unchanged by this conclusion.

---

## 10. YAGNI — what was not built

| Not built | Why | Evidence that would justify revisiting |
|---|---|---|
| Generic mesh cloning service | one call site. `Object.Instantiate` is the whole mechanism | a second, materially different consumer with different fidelity needs |
| Custom full-fidelity mesh copier | measured unnecessary for the characterized state, descriptors included (§7.1) | a measured field that `Instantiate` does not preserve |
| Universal mesh snapshot / mesh IR | would turn analysis evidence into an output format. `.omp/AGENTS.md` §Repository reality and scope forbids broad infrastructure without a present requirement | a consumer that must reason about mesh data *after* the live object is gone |
| Exhaustive vertex-format matrix | the descriptor characterization exposed no concrete need (§7.1) | a measured loss in a layout the fixture omits |
| Mutation IR | nothing needs it | — |
| Mesh fingerprint / hash framework | the residual same-instance risk (§8.1) is narrowed by construction, not by detection | a real coexistence failure with a named optimizer, reduced to a public fixture |
| Generic late-live-read contract | route 3 was not needed, so its guard is not needed | route 1 failing a concrete supported case |
| Cache, registry, planner, transaction framework, reference counting | the single post-validation sweep already expresses the rule (§7.7) | — |
| A new "requires clone" predicate | `MeshSeparationPlan.RequiresAnySplit` already exists (§7.5) | — |
| Shared test mesh-builder helper | the existing per-class builders are analysis inputs and not reusable here (§4.1). One adversarial fixture serves one characterization | a second characterization needing the same adversarial mesh |
| Non-readable-mesh infrastructure | requirement withdrawn (§3) | VRChat policy changing to admit non-readable meshes |
| Expanding `UnityRendererAlphaSnapshot` | the clone holds the data in Unity's own representation (§4.3) | — |
| Production alpha separation | out of scope for this branch | controller approval |

This note does not treat "a more general mechanism may prove useful later" as a reason to keep
climbing the ladder. The ladder stops at step 3.

---

## 11. Files, tests, console, git status

**Created (1 file plus its meta):**

- `Packages/com.alrauna.amuse/Tests/Editor/Host/MeshCloneFinalizationCharacterizationTests.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Host/MeshCloneFinalizationCharacterizationTests.cs.meta`
  (Unity-generated, GUID `16b817da2724a412c8c2c692feceb1dc`, with trailing whitespace stripped
  from the three empty-value lines, to match the repository's committed metas)
- This note.

**No production file changed. No existing test changed.**

**Tests.**

| Run | Result |
|---|---|
| First version, focused | **1 failed** of 4: mesh bounds recalculated by `subMeshCount` (§7.2) |
| First version, after the mesh-bounds compensation | 5 passed |
| Full `Alrauna.Amuse.Tests.Editor` EditMode assembly, first version | **1316 passed, 0 failed, 0 skipped** (46.4 s) |
| **Descriptor version, focused** | **6 passed, 0 failed** (0.9 s) |

The broader assembly did **not** re-run after the descriptor rewrite. The rewrite touches one
test class only. That class exports no plugin, registers no platform, adds no global
component, and touches no other file. So the earlier full-assembly result still stands for
everything outside it. If the controller wants the belt-and-braces run, it costs 46 seconds.

**Console.** No compile error appeared. The only `InvalidOperationException` entries are the
deliberate `"synthetic preparation failure"` / `"synthetic post-mutation failure"` fixtures
from `AmuseBuildOperationTests`, plus `"Starting processing for avatar: …"` from an NDMF build
fixture. The `mprotect returned EACCES` exceptions and the `MCP-FOR-UNITY` port-fallback
warnings are Unity MCP tooling noise at domain reload. They predate this branch's changes and
have no link to AMUSE code. **No new warning or error appeared unexplained.**

**Assets.** No test created a temporary asset, and none survives. No test touched an importer
setting.

**Git.** Nothing staged, nothing committed, nothing pushed. `git diff --check` came back
clean. The host-generated manifest and lock churn was inspected in full and restored per
policy.
