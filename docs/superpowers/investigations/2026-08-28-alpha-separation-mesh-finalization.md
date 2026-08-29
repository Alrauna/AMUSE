# Alpha Separation — Mesh Finalization Prerequisite

**Investigation note. No production code. Not an implementation plan.**

Every claim below is tagged:

- **[SOURCE]** — read directly from pinned repository or vendor source.
- **[MEASURED]** — observed by running a characterization or probe in this repository.
- **[INFERENCE]** — architectural reasoning from the above.
- **[DECISION]** — a controller decision this note does not make.

**Recommendation: A — the native clone is sufficient**, with two named and measured
obligations (§7.2) and a narrowed clone trigger (§7.5). Details in §9.

**A note on wording.** Claims about the source mesh being unchanged are claims about the
**characterized logical state** — the fields listed in §6 and compared through the test's
`Describe` digest. `Describe` is a selected structural digest, **not** a byte comparison of the
mesh, and an earlier draft of this note wrongly said "byte-identical". Every such claim has
been narrowed.

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

`Packages/manifest.json` and `Packages/packages-lock.json` acquired the documented
host-generated churn during the compile/test cycles — **only**
`com.unity.toolchain.macos-arm64-linux-x86_64`, `com.unity.sysroot`, and
`com.unity.sysroot.linux-x86_64`. The complete diff was inspected, no intentional change
shares those files, and `CLAUDE.md`'s restore was applied.

No fetch, pull, push, stage, commit, PR, or history change occurred. No other branch was
touched.

---

## 2. The question

The prior investigation (`2026-08-28-alpha-separation-animation-rewrite.md`, §11) left one
unresolved feature-design blocker for Milestone B (`Split`):

> How does AMUSE retain or obtain full-fidelity mesh state for post-validation finalization?

Validation is **late** and **slot-local**: the surviving candidate set `S` is not known until
the second animator-services window has independently validated every candidate slot. The
output submesh set, and therefore the appended slot indices the surviving slots' own curves
name, are functions of `S`. So the mesh cannot be finalized during preparation — but the
fidelity-critical vertex data must not be lost on the way there either.

This note tests the prior investigation's **route 1** and nothing else:

1. before the second window, create an unassigned AMUSE-owned native clone of the effective
   build mesh;
2. mutate no source mesh, renderer, build avatar, or animation curve;
3. validate every candidate slot independently in the second window;
4. once `S` is known, finalize **only** the clone's submesh/index layout against `S`;
5. assign the finalized clone only at the final build-avatar mutation boundary.

---

## 3. Withdrawn requirement: non-readable mesh reconstruction

The prior investigation stated that a route which "works only for readable meshes is not a
solution for real avatars", and that non-readable meshes are "the sharpest test" of all three
routes.

**That statement is withdrawn.** Current VRChat SDK policy requires Mesh Read/Write to be
enabled for avatar upload, so a non-readable mesh is not a supported input to a VRChat avatar
build. Non-readable mesh compatibility is therefore **not an acceptance criterion** for this
feature.

What survives the withdrawal:

- `MeshReadabilityCharacterizationTests` remains valuable evidence that Editor analysis can
  incidentally read a mesh whose `isReadable` is false, and it remains the justification for
  `UnityRendererAlphaAnalysis`'s deliberate absence of a readability pre-check and exception
  handling. **[SOURCE]** That comment is still accurate and should not be edited.
- Incidental compatibility is preserved where Unity gives it for free. Nothing is built to
  obtain it.

**Not measured, and deliberately so:** whether `Object.Instantiate` on a non-readable mesh
preserves bone weights, bindposes and blend shape frames. Measuring it would require importing
an asset fixture for a property no supported input can exhibit. If a future requirement ever
admits non-readable meshes, that is the measurement to add.

**This correction applies only to meshes.** The established requirement to analyze ordinary
non-readable, mipmapped and compressed **textures** is untouched, and `AGENTS.md`'s texture
section still governs.

---

## 4. Repository grounding — what already exists

### 4.1 What exists, and what is genuinely missing

**Correcting an earlier draft of this note.** It claimed there was "no production hit for any
mesh operation" and — wrongly — that no mesh construction, index rebuilding or renderer
replacement existed "anywhere in AMUSE — production or test". The production half is right.
**The claim about tests was false**, and is withdrawn.

**[SOURCE] Production.** No AMUSE production code performs mesh cloning, submesh/index-layout
rebuilding, generated-mesh lifetime management, renderer mesh replacement, source-mesh
preservation, or attribute / skinning / blendshape copying. AMUSE production code creates
exactly one Unity object today, and it is a `Material` (§4.3). It has never created a `Mesh`.
That gap is real and is what this note addresses.

**[SOURCE] Tests.** Several existing test fixtures do construct meshes and write index data:

| Fixture | What it does |
|---|---|
| `UnityRendererAlphaAnalysisTests` | `new Mesh()`, `subMeshCount = 2`, `SetTriangles` per submesh, a `MeshTopology.Quads` case, `sharedMesh` assignment |
| `UnityRendererAlphaSnapshotTests` | private `Triangle()` / `Quad()` builders — positions, `uv`, `SetTriangles`, `sharedMesh` assignment |
| `RendererAlphaAnalysisIntegrationTests` | two-submesh meshes via `SetTriangles`, and an assertion that `subMeshCount` is unchanged after analysis |
| `UnityAnimationCharacterizationTests` | single-triangle meshes assigned to `sharedMesh` |
| `AmuseBuildOperationTests`, `AmusePlatformFinishPluginTests` | private `NewTriangleMesh()` / `BuildLowerMipBuildMesh()` |

**Is any of them reusable here? No, and none of them characterizes this requirement.**

1. **None is shared.** Every builder is a `private` method bound to its own test class, most of
   them to that class's own `Track` list. There is no shared mesh helper anywhere under
   `Tests/Editor` — the only shared directories are `ReferenceFixtures` (material/shader
   reference data) and `TestInfrastructureSmokeTests`.
2. **All are analysis *inputs*, not output construction.** They carry positions, `uv` and
   triangles, because that is exactly what `UnityRendererAlphaSnapshot` consumes. None carries
   normals, tangents, colors, multiple UV channels, bone weights, bindposes, blend shapes, a
   non-default index format, or a non-default submesh descriptor — so none can falsify a lossy
   clone or a lossy layout rewrite.
3. **None clones or finalizes anything.** No existing test calls `Object.Instantiate` on a
   mesh, reads a `SubMeshDescriptor`, or rewrites a submesh layout.

So the honest statement is: the repository has ample precedent for *building a small mesh in a
test*, and none for *cloning or finalizing one*. Reusing a three-vertex analysis fixture here
would have produced a test that passes under every implementation this note needed to
distinguish. A new adversarial fixture (§6) was written instead, and no shared abstraction was
introduced for it.

### 4.2 What is reused

| Existing mechanism | Role here |
|---|---|
| `UnityRendererAlphaSnapshot` / `UnitySubmeshAlphaSnapshot` | unchanged. Decides *which triangles go where*; the clone supplies the data to build with. Complementary, and the snapshot needs no new fields. |
| `MeshSeparationPlan.RequiresAnySplit` | **the clone trigger** (§7.5). Already exists; no new predicate is needed. |
| `UnityRendererMutationTarget` | unchanged. Still a reference-identity check on `Renderer` / `ExpectedMesh` / `ExpectedMaterialSlotCount` (§8.1). |
| `PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone` | the **precedent**, not a dependency. Same shape one level down: clone the source, write only what must change, never save, leave naming to the consumer, destroy the clone on refusal. |
| `AmuseBuildOperation` | unchanged, still zero production callers. Synchronous, so it cannot straddle passes — established by the prior investigation and not revisited. |
| `AmusePlatformFinishState` (NDMF `context.GetState<T>()`) | the carrier. `AnimatorBindings` is the existing precedent for a live transient host capability held there, explicitly outside the immutable evidence graph. **[SOURCE]** |
| `AnimatorServicesReactivationCharacterizationTests` | reused as evidence, not extended (§7.6). |
| `MeshReadabilityCharacterizationTests` | reused as evidence for §3. Not modified. |

### 4.3 Why the snapshot and mutation target have their current shapes

**[SOURCE]** `Editor/Host/UnityRendererAlphaSnapshot.cs` was introduced by commit `902fc3f`,
*"refactor: analyze renderers from immutable capture"* — its only commit. The shape is
deliberate: the snapshot is immutable, defensively copies every list, and carries only what
alpha *analysis* consumes. `UnityRendererMutationTarget` was split out in the same commit to
keep the live `Renderer` reference out of the immutable half.

**[INFERENCE]** That split is why route 1 fits without a redesign. The snapshot is not
under-built for mesh output; it was never intended to be an output-construction source. Adding
normals, tangents, colors, UV channels, bone weights, bindposes, blend shape frames **and now
submesh descriptors** to it would turn analysis evidence into a mesh IR — which `AGENTS.md`
names as a thing not to build — while a native clone already holds all of it in Unity's own
representation.

---

## 5. YAGNI ladder, evaluated in order

| # | Question | Answer |
|---|---|---|
| 1 | Does the repository already have the mechanism? | **No** for meshes; the material-level pattern and the `RequiresAnySplit` trigger already exist (§4.1, §4.2). |
| 2 | Can `Object.Instantiate(mesh)` preserve the effective build mesh without custom copying? | **Yes — measured** for the characterized fields, submesh descriptors included (§7.1). |
| 3 | Can the finalized layout be produced by changing only submesh/index data on that clone? | **Yes — measured**, with two named obligations (§7.2) and one intentional representation change (§7.4). |
| 4 | Can existing NDMF/build-state facilities carry and clean up the transient clone? | **Yes** — carry [SOURCE + MEASURED], cleanup [MEASURED] (§7.6, §7.7). |
| 5 | Does a concrete supported case fail? | **No supported case failed.** The ladder stops at step 3. |

---

## 6. The characterization

**File:** `Packages/com.alrauna.amuse/Tests/Editor/Host/MeshCloneFinalizationCharacterizationTests.cs`
(new, 6 tests). It characterizes **Unity, not AMUSE**: no AMUSE production type participates,
and nothing in it is a contract AMUSE offers. It exports no plugin, registers no platform, and
adds no global component, so it cannot perturb an unrelated test.

**Scope.** The characterized fields are exactly: vertex count; index format; submesh count;
mesh bounds; positions; normals; tangents; `colors32`; UV channels 0, 3 and 7; bone weights;
bindposes; per-submesh `SubMeshDescriptor` (topology, `indexStart`, `indexCount`, `baseVertex`,
`firstVertex`, `vertexCount`, per-submesh `bounds`); stored and effective indices; and blend
shape names, per-frame weights and per-frame delta vertices / normals / tangents. Claims in
this note are scoped to that list and no wider.

**The fixture is adversarial rather than broad** — one 9-vertex mesh built in memory:

| Fixture property | The wrong implementation it catches |
|---|---|
| `IndexFormat.UInt32` on a 9-vertex mesh | a copier that lets Unity pick the default `UInt16` — two defaults agreeing would prove nothing |
| UV channels 0, 3, 7 at **Vector2, Vector3, Vector4** | a copier that assumes `Vector2` everywhere, dropping components and channels |
| vertices 3 and 8 referenced by **no** triangle | a finalization that compacts or reindexes vertices |
| blend shape with **two frames**, plus a second shape | per-frame weights and deltas are stored separately from shape names |
| mesh bounds authored **last** and unrelated to the geometry | implicit mesh-bounds recalculation (§7.2) |
| **per-submesh bounds authored** via `SetSubMesh`, also unrelated to the geometry | implicit per-submesh bounds recalculation — the second obligation, invisible without this |
| **the split submesh carries `baseVertex 4`**, so its stored indices differ from its effective ones | an implementation that ignores base vertex and builds triangles over the wrong vertices |
| the untouched submesh is a *different* submesh from the split one | a rewrite that cannot leave a neighbouring submesh's descriptor alone |
| `colors32` with exact byte values | a float round-trip that quietly requantizes |

Vector comparisons use round-trippable `"R"` formatting, because Unity's default vector
`ToString` rounds to two decimals and would let a real difference compare equal.

Teardown destroys every tracked mesh **including after a failing assertion**. No asset is
imported, no importer setting is touched, and no file is written under `Assets/`.

---

## 7. Findings

### 7.1 Native cloning is lossless for the characterized state — **[MEASURED]**

`Object.Instantiate(mesh)` produced a distinct Unity object (different instance id) and
retained, exactly, every field listed in §6 — including the **complete submesh descriptors**:
the nonzero `baseVertex 4` on the split submesh, its stored-vs-effective index distinction, and
both authored per-submesh bounds.

The source mesh's characterized state was unchanged after cloning, after finalizing the clone,
and after destroying the clone.

The clone is named `"<source>(Clone)"`. **[MEASURED]** Naming a generated asset is a consumer
obligation — the rule `PrepareCanonicalOpaqueClone` already documents, because container
sub-asset names come from the object's own name and NDMF guarantees no determinism.

**Explicitly unmeasured:** modern variable bone weights (`GetAllBoneWeights` /
`SetBoneWeights`), UV channels other than 0, 3 and 7, and any other vertex layout the fixture
does not carry. **[INFERENCE]** `Object.Instantiate` copies the native vertex buffer and its
layout wholesale rather than field-by-field — which is why the mixed-dimension UV channels and
the descriptors survived — so per-attribute enumeration is not expected to matter. That is
inference, not measurement, and the descriptor characterization exposed no concrete need to
expand into a vertex-format matrix.

### 7.2 Layout-only finalization works, but "layout-only" is not free — **two obligations**

**This is the real finding. The first half was found RED.**

The first run of the characterization **failed**:

```
finalization recalculated bounds
  Expected: Center: (10.00, 20.00, 30.00), Extents: (20.00, 25.00, 30.00)
  But was:  Center: (7.00, 8.75, -3.00),   Extents: (7.00, 8.75, 10.00)
```

Isolating probes then pinned the complete behaviour, mesh level and submesh level:

| Operation | Mesh bounds | Per-submesh bounds |
|---|---|---|
| `Object.Instantiate(mesh)` | preserved | preserved |
| **raising `mesh.subMeshCount`** | **recalculated from the whole vertex buffer** | **recalculated for every existing submesh; the new submesh is created with zero bounds** |
| `SetIndices(..., calculateBounds: false)` | untouched | untouched — so a shrunken submesh keeps stale bounds and the appended submesh keeps **zero** bounds |
| assigning `mesh.bounds` / `SetSubMesh(..., DontRecalculateBounds)` afterwards | restored exactly | restored exactly |

So `calculateBounds: false` is **not sufficient**, and it is not even the operative call:
raising `subMeshCount` recalculates *both* levels before any index is written, and it
recalculates from *all* vertices, including ones no triangle references.

**Obligation 1 — mesh bounds.** Capture `mesh.bounds` before raising `subMeshCount`; write it
back after. One line each side.

**Obligation 2 — per-submesh bounds.** Capture each source submesh's descriptor `bounds` before
raising `subMeshCount`; write them back after via `SetSubMesh(..., DontRecalculateBounds)`. This
one is invisible unless the fixture authors per-submesh bounds that differ from the geometry,
which is why the earlier version of this characterization missed it entirely.

**The inheritance rule for the appended submesh.** It inherits its **source** submesh's bounds.
Its triangles are a subset of that submesh's, so the inherited bounds are a conservative
superset — the safe direction, since bounds that are too large cost culling while bounds that
are too small pop. The same rule covers the shrunken split submesh, whose remaining triangles
are also a subset. One rule, no per-case branching.

Neither obligation is a route failure, and neither changes the recommendation. Both are exactly
the kind of silent fidelity loss this investigation existed to find, and neither would have
been found by source reading.

The characterization now encodes both halves: `FinalizeLayout` performs the compensation, and a
dedicated test pins the underlying Unity behaviour — including that
`calculateBounds: false` leaves the appended submesh's bounds at zero — so that if Unity ever
changes, the compensation must be re-justified rather than silently kept.

### 7.3 Descriptor behaviour, before and after finalization — **[MEASURED]**

Source, as authored:

```
[0] indexStart=0 indexCount=3 baseVertex=0 firstVertex=0 vertexCount=3
    bounds=(100,200,300)/(1,1,1)  effective=[0,1,2]        stored=[0,1,2]
[1] indexStart=3 indexCount=6 baseVertex=4 firstVertex=0 vertexCount=4
    bounds=(101,201,301)/(1,1,1)  effective=[4,5,6,5,6,7]  stored=[0,1,2,1,2,3]
mesh.bounds=(10,20,30)/(20,25,30)
```

After finalization (submesh 1 split, its opaque triangle appended as submesh 2, submesh 0 never
rewritten):

```
[0] indexStart=0 indexCount=3 baseVertex=0 firstVertex=0 vertexCount=3
    bounds=(100,200,300)/(1,1,1)  effective=[0,1,2]  stored=[0,1,2]
[1] indexStart=3 indexCount=3 baseVertex=0 firstVertex=4 vertexCount=3
    bounds=(101,201,301)/(1,1,1)  effective=[4,5,6]  stored=[4,5,6]
[2] indexStart=6 indexCount=3 baseVertex=0 firstVertex=5 vertexCount=3
    bounds=(101,201,301)/(1,1,1)  effective=[5,6,7]  stored=[5,6,7]
mesh.bounds=(10,20,30)/(20,25,30)
```

Reading it off:

- **the untouched submesh retains its entire descriptor**, bit for bit including its authored
  per-submesh bounds — even though the rewrite shifted index data around it. In a variant probe
  where the untouched submesh sat *after* the split one, Unity relocated its index data and
  updated its `indexStart` automatically while its effective indices stayed correct;
- **the split and appended submeshes receive valid descriptors.** `indexCount` matches the
  indices written; `baseVertex + firstVertex` names the lowest vertex each submesh actually
  references (4 and 5); `vertexCount` spans the referenced range (3 and 3). Both are asserted
  as relationships, not hardcoded constants, so they stay meaningful if the fixture changes;
- **per-submesh bounds require explicit preservation** — see §7.2. Restoring `Mesh.bounds`
  alone is **not** sufficient;
- **`firstVertex` and `vertexCount` remain valid for CPU processing and skinning.** On the
  rewritten submesh they are in fact *tighter* than the source's, which described a 4-vertex
  span for what is now a 3-vertex triangle.

### 7.4 Base-vertex normalization is representation-only, and safe — **[MEASURED]**

Rewriting the split submesh with effective indices normalizes its `baseVertex` from 4 to 0, and
its `firstVertex` correspondingly from 0 to 4. This is an **intentional representation change**,
recorded as such because it was measured rather than assumed, and it is safe on two grounds
both asserted by the characterization:

1. **the effective referenced vertices are identical.** `Mesh.GetIndices(submesh)` applies the
   base vertex by default, and the finalized submesh returns exactly the preserved-alpha
   triangle the source resolved to;
2. **the invariant `baseVertex + firstVertex` is preserved.** Source: `4 + 0`. Finalized:
   `0 + 4`. Both name absolute vertex 4. The source spends the offset on `baseVertex`; the
   rewrite spends it on `firstVertex`. What CPU processing and skinning need is the sum.

Representation identity is therefore **not** required here, and is not asserted. What is
asserted is effective behaviour plus a truthful descriptor.

### 7.5 Clone only when the plan requires a split — **corrected**

An earlier version of this note recommended cloning during preparation for every candidate
renderer. **That was wrong, and it is corrected.**

**[SOURCE]** `MeshSeparationPlanner.Create` already computes `requiresAnySplit` as
`|= disposition == SubmeshSeparationDisposition.Split` across submeshes, and exposes it as
`MeshSeparationPlan.RequiresAnySplit`. `Unchanged` and `WhollyOpaqueCandidate` dispositions do
not set it.

**[INFERENCE]** A renderer whose submeshes are all `Unchanged` and/or
`WhollyOpaqueCandidate` needs **no geometry change at all** — a wholly opaque submesh is handled
by material assignment and curve rewriting, which is precisely why the prior investigation
called Milestone A "not a blocker". Cloning such a renderer's mesh would allocate a full copy
that finalization would never touch and the sweep would always destroy.

**The corrected trigger: create the transient clone only after planning has established
`plan.RequiresAnySplit == true`.** No new predicate is needed, and the check costs nothing —
the plan is already computed at that point in the barrier.

### 7.6 The second window does not obstruct the carry — **[SOURCE] + [MEASURED, existing]**

**[SOURCE]** NDMF passes are synchronous method calls within one `AvatarProcessor.ProcessAvatar`
invocation; `context.GetState<T>()` is a plain object held by the `BuildContext`; and Unity
objects created in the Editor are not garbage-collected — they live until an explicit destroy
or a domain reload, and no domain reload can occur inside a synchronous build.

**[MEASURED, by the existing `AnimatorServicesReactivationCharacterizationTests`]** A probe
object created in the first animator-services window is observed intact by the barrier and
again by the reactivated second window, across exactly the three-pass sequence this feature
uses. A transient `Mesh` reference is a field on such an object.

**[INFERENCE]** Nothing about the second window prevents carrying an unassigned transient clone
from preparation to finalization. `AmusePlatformFinishState.AnimatorBindings` is the existing
precedent for holding a live, transient host capability there.

**This was deliberately not re-measured.** Doing so would mean either a second exported plugin
and dedicated platform, or widening an existing focused characterization — the former is
ceremony for a fact source already settles, and the latter would blunt a test that proves one
thing well. If the controller wants it measured, the cheapest form is one extra field and one
extra assertion on the existing reactivation probe.

### 7.7 Cleanup follows the existing mechanism — **[MEASURED] + [SOURCE]**

**[MEASURED]** `Object.DestroyImmediate` on an abandoned, never-assigned clone destroys it
(Unity's overloaded equality then reports it null) and leaves the source mesh's characterized
state unchanged.

**[SOURCE]** `BuildContext.Serialize()` (called from `Finish()` after all extensions
deactivate) walks assets **reachable from the avatar root** and saves the non-persistent ones;
its cleanup pass skips anything that is not a `Component` or `GameObject`. **[INFERENCE]** Two
consequences, both matching the generated-**material** conclusions the prior investigation
already reached:

1. an unassigned transient clone is unreachable, so it is never persisted — but it is also
   never swept, so an abandoned clone survives in memory until domain reload and **must be
   destroyed explicitly**;
2. a clone assigned to `sharedMesh` at the apply boundary is trivially reachable and is
   persisted with **no `IAssetSaver.SaveAsset` call**, and none should be made — saving during
   preparation would weld an abandoned mesh permanently into the shipped container and make
   preparation observably mutating.

So the prior investigation's **single post-validation sweep** covers meshes unchanged: destroy
exactly the transient objects no surviving slot in `S` references, once `S` is fixed. **No
reference counting, and no new lifetime mechanism.**

---

## 8. Remaining concrete risks

### 8.1 Same-instance in-place mutation by another pass — **unchanged, and now smaller**

**[SOURCE]** `UnityRendererMutationTarget.ExpectedMesh` is a *reference identity* check. It
detects the renderer being pointed at a different `Mesh`; it cannot detect another NDMF pass
rewriting the same `Mesh` instance in place.

**[INFERENCE]** Route 1 **narrows** this risk rather than resolving it. The clone is taken
during the barrier, when the analysis it must agree with is taken, so the exposure window is
the barrier-to-clone gap — effectively zero — rather than the whole barrier-to-second-window
span a late live read would expose. A late read (route 3) would have needed a real guard this
check does not provide; route 1 does not.

The residual risk is the ordinary coexistence question of another optimizer mutating the same
renderer's mesh in the same phase, which `AGENTS.md` already treats as an ordering/exclusion
concern. **No new mechanism is proposed for it.**

### 8.2 Memory cost — **bounded by split-requiring renderers**

Not measured. The bound is **renderers whose plan has `RequiresAnySplit == true`** (§7.5), not
all candidate renderers — an earlier draft of this note overstated it. Renderers that are
wholly `Unchanged` / `WhollyOpaqueCandidate` are never cloned at all, so a build that finds no
splits allocates no mesh.

For the renderers that are cloned, one full copy is held from the barrier until the
post-validation sweep. **[INFERENCE]** It is transient, and route 2 (a reconstruction snapshot)
would hold the same data in a *less* compact managed representation, so this is not an argument
for another route.

### 8.3 Not measured

- non-readable meshes (§3 — deliberately out of scope);
- modern variable bone weights, and vertex layouts the fixture does not carry (§7.1);
- topologies other than `Triangles`. Renderer analysis already refuses non-triangle topology
  with `RendererAnalysisRefusal.UnsupportedTopology` before any of this is reached. **[SOURCE]**
- the apply boundary itself. This branch stops at finalization, as instructed.

---

## 9. Recommendation — **A. Native clone is sufficient**

The narrow production design, with the exact existing seams to use:

1. **Preparation (barrier pass, no extension).** For each candidate renderer, after
   `UnityRendererAlphaAnalysis.CaptureGeometry` accepts and `MeshSeparationPlanner.Create`
   reports **`RequiresAnySplit == true`**, create
   `Object.Instantiate(mutationTarget.ExpectedMesh)`. A renderer with no split-disposition
   submesh is **not** cloned (§7.5). This is transient-object construction, not build-avatar
   mutation. Do not name it, do not save it, do not assign it.
2. **Carry.** Hold the clone on the prepared alpha-separation record in
   `AmusePlatformFinishState`, alongside the per-slot planning information the prior
   investigation specified. Follow the `AnimatorBindings` precedent.
3. **Validation (second animator-services window).** Unchanged from the prior investigation:
   validate every candidate slot independently, reads only, producing `S`.
4. **Finalization against `S`.** On the clone only:
   - capture `mesh.bounds` **and** every source submesh's descriptor `bounds`;
   - raise `subMeshCount`;
   - `SetIndices(..., calculateBounds: false)` for each rewritten and appended submesh, leaving
     submeshes whose triangle set is unchanged un-rewritten;
   - write back per-submesh bounds via `SetSubMesh(..., MeshUpdateFlags.DontRecalculateBounds)`,
     each output submesh inheriting its **source** submesh's bounds;
   - write back `mesh.bounds`.

   Write nothing else. Base-vertex normalization on rewritten submeshes is expected and safe
   (§7.4). Index sets come from `MeshSeparationPlan` over `UnityRendererAlphaSnapshot`, which
   needs no new fields.
5. **Sweep.** Destroy every transient clone — mesh and material alike — that no surviving slot
   in `S` references. One sweep, no reference counting.
6. **Apply.** The single build-avatar mutation boundary: curve edits, then `sharedMesh` and
   `sharedMaterials`. NDMF's `Serialize()` persists the assigned clone with no `SaveAsset` call.

**No additional infrastructure is proposed.** `UnityRendererAlphaSnapshot`,
`UnityRendererMutationTarget`, `MeshSeparationPlan`, `MeshSeparationPlanner`,
`AmuseBuildOperation` and `MaterialSemantics` are all unchanged by this conclusion.

---

## 10. YAGNI — what was not built

| Not built | Why | Evidence that would justify revisiting |
|---|---|---|
| Generic mesh cloning service | one call site; `Object.Instantiate` is the whole mechanism | a second, materially different consumer with different fidelity needs |
| Custom full-fidelity mesh copier | measured unnecessary for the characterized state, descriptors included (§7.1) | a measured field that `Instantiate` does not preserve |
| Universal mesh snapshot / mesh IR | would turn analysis evidence into an output format; `AGENTS.md` names it as a thing not to build | a consumer that must reason about mesh data *after* the live object is gone |
| Exhaustive vertex-format matrix | the descriptor characterization exposed no concrete need (§7.1) | a measured loss in a layout the fixture omits |
| Mutation IR | nothing needs it | — |
| Mesh fingerprint / hash framework | the residual same-instance risk (§8.1) is narrowed by construction, not by detection | a real coexistence failure with a named optimizer, reduced to a public fixture |
| Generic late-live-read contract | route 3 was not needed, so its guard is not needed | route 1 failing a concrete supported case |
| Cache, registry, planner, transaction framework, reference counting | the single post-validation sweep already expresses the rule (§7.7) | — |
| A new "requires clone" predicate | `MeshSeparationPlan.RequiresAnySplit` already exists (§7.5) | — |
| Shared test mesh-builder helper | the existing per-class builders are analysis inputs and not reusable here (§4.1); one adversarial fixture serves one characterization | a second characterization needing the same adversarial mesh |
| Non-readable-mesh infrastructure | requirement withdrawn (§3) | VRChat policy changing to admit non-readable meshes |
| Expanding `UnityRendererAlphaSnapshot` | the clone holds the data in Unity's own representation (§4.3) | — |
| Production alpha separation | out of scope for this branch | controller approval |

"A more general mechanism may be useful later" was not treated as a reason to continue. The
ladder stopped at step 3.

---

## 11. Files, tests, console, git status

**Created (1 file + its meta):**

- `Packages/com.alrauna.amuse/Tests/Editor/Host/MeshCloneFinalizationCharacterizationTests.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Host/MeshCloneFinalizationCharacterizationTests.cs.meta`
  (Unity-generated, GUID `16b817da2724a412c8c2c692feceb1dc`, trailing whitespace stripped from
  the three empty-value lines to match the repository's committed metas)
- this note.

**No production file was modified.** No existing test was modified.

**Tests.**

| Run | Result |
|---|---|
| First version, focused | **1 failed** of 4: mesh bounds recalculated by `subMeshCount` (§7.2) |
| First version, after the mesh-bounds compensation | 5 passed |
| Full `Alrauna.Amuse.Tests.Editor` EditMode assembly, first version | **1316 passed, 0 failed, 0 skipped** (46.4 s) |
| **Descriptor version, focused** | **6 passed, 0 failed** (0.9 s) |

The broader assembly was **not** re-run after the descriptor rewrite. The rewrite is confined
to one test class that exports no plugin, registers no platform, adds no global component, and
touches no other file, so the earlier full-assembly result still stands for everything outside
it. If the controller prefers the belt-and-braces run, it is a 46-second job.

**Console.** No compile error. The only `InvalidOperationException` entries are the deliberate
`"synthetic preparation failure"` / `"synthetic post-mutation failure"` fixtures from
`AmuseBuildOperationTests`, plus `"Starting processing for avatar: …"` from an NDMF build
fixture. The `mprotect returned EACCES` exceptions and the `MCP-FOR-UNITY` port-fallback
warnings are Unity MCP tooling noise at domain reload, present before this branch's changes and
unrelated to AMUSE code. **No new unexplained warning or error.**

**Assets.** No temporary asset was created, and none survives. No importer setting was touched.

**Git.** Nothing staged, nothing committed, nothing pushed. `git diff --check` clean. The
host-generated manifest/lock churn was inspected in full and restored per policy.
