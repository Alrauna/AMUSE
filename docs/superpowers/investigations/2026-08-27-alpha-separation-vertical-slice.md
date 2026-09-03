# Alpha Separation Vertical Slice — Investigation

**Status: historical investigation. Several conclusions are materially superseded.**

This note is the first survey of what an alpha-separation vertical slice needs. Its
findings about public code, NDMF, meshes, slots, animation, lifecycle, and the mutation
boundary stay useful. This note keeps them below. Three things in it are no longer valid:

1. **Its Census methodology was invalid.** It did not find the approved corpus. Instead, it
   used an arbitrary scan of the whole project at the asset level. This note removes every
   result from that scan. Under current Census policy, those results are not valid
   evidence. Do not cite them, restore them, or rely on them. The questions they seemed to
   answer stay open again until a new characterization uses the approved corpus.
2. **Its `_CENSUSLAB` premise was wrong.** The note claimed that no approved Census root
   existed. An approved root exists (see §9). This note corrects that claim.
3. **A later finding supersedes its headline recommendation.** Geometry-only separation
   that applies the *same alpha material* to both slots does not give the intended
   overdraw optimization (see §4).

Census Lab observations serve as architectural pressure and validation evidence. They are
never a correctness authority.

## 1. Repository baseline at the time

The branch is `feat/alpha-separation-vertical-slice`, from `main` at commit `b48b4ff`. The
working tree is clean, with nothing committed. Unity is 2022.3.22f1. NDMF is 1.14.4.
VRChat SDK is 3.10.4. This note reads all versions from pinned local source.

### Production alpha path as it actually exists

```
AmusePlatformFinishPlugin  (BuildPhase.PlatformFinish — NDMF's LAST phase)
  ├─ pass 1 "AMUSE animator bindings capture"  (inside AnimatorServicesContext)
  └─ pass 2 "AMUSE semantic barrier"           (no extension → graph committed)
        ├─ HostLifecycleCapability.CaptureAndEvaluate
        ├─ CommittedControllerGraph.Enumerate
        └─ for each Renderer:
             HostStructuralRefusalFor → UnityAnimationEvidenceCapture
             → ResolveRuntimeStates (AdmittedMaterialStates)
             → CaptureGeometry → ClassifyRuntimeStates
             → MeshSeparationPlanner.Create(...)  ← PLAN BUILT
             → returns plan.OpaqueTriangleCount   ← PLAN DISCARDED
```

Three findings from public code shaped everything else. All three remain valid:

1. **The build computes the plan, then discards it.** `ClassifyRuntimeStates` builds a
   full `MeshSeparationPlan` but returns only its opaque triangle count. Nothing keeps
   the plan at build scope. Keeping the plan is the first required change.
2. **The Prepare/Apply boundary exists but has no wiring.**
   `AmuseBuildOperation.Execute(lifecycle, assetSaver, prepare, apply)` is complete,
   documented, and tested. It has zero production callers.
3. **`UnityRendererMutationTarget` exists.** The build produces it but never consumes it.
   It carries the renderer, the expected mesh, and the expected material-slot count.

The merged upload-conditional authorization design and the general-purpose
transformation-boundaries audit under `docs/superpowers/` remain the approved
architecture for this area.

## 2. The proof-to-plan contract

`MeshSeparationPlan` preserves the original submesh identity. It is complete for its own
purpose. The captured snapshot that feeds it holds only the vertex count, positions, UV0,
per-submesh indices, and the captured alpha materials. So it holds none of the following:

- vertex attributes beyond position/UV0 — normals, tangents, colors, UV1–UV7, bone
  weights, bindposes
- blendshapes — names, frame counts, weights, per-frame deltas
- mesh-level state — index format, bounds, base vertex, submesh descriptors, name
- renderer-level state — bones, root bone, local bounds, blendshape weights, quality,
  probe/shadow settings
- material identity beyond an opaque binding index (deliberately)
- **any description of what an opaque output material should be** (§4)

**Recommended boundary, still valid:** the plan stays as proof/candidate output and feeds
a separate prepared application object. The merged design rejected plans that carry host
postconditions. The boundaries audit requires the plan to stay purpose-specific.
**Do not change `MeshSeparationPlan`'s public contract.**

## 3. Proposed supported case

Structural constraints, still valid as a starting shape:

- exactly one `SkinnedMeshRenderer`, handled independently
- slot count equal to submesh count (already enforced)
- triangle topology only (already enforced)
- **exactly one admitted material per affected slot** — a correctness requirement, not a
  simplification (§6)
- one source submesh set to split
- blendshapes and skinning preserved
- generated slot **appended at the end**, never inserted (§6)
- one generated replacement mesh. AMUSE does not replace the renderer component itself.

Whether blendshaped and shared meshes are common enough to require support in the first
slice was, before now, answered from the invalid scan. **This note withdraws that
answer.** The conservative engineering position stands on its own. Shared input meshes
are possible, so the slice must never change an input mesh. Blendshape and skinning
preservation is required whenever present.

## 4. Material/output strategy — the blocking gate

### What the proof actually establishes

`AdmittedMaterialStates` resolves only the alpha output. `MaterialSemantics` is exactly
`{ BaseColor, Alpha, Emission, Normal }`. **AMUSE models no render state at all: no
render queue, depth write, blend factors, cull, alpha-to-coverage, stencil, keywords, or
shader passes.**

So a proven-opaque result means exactly this: *the material's alpha equation evaluates
to opaque over these triangles, under every admitted runtime state.* It does **not** show
that the same material, rendered in an opaque render mode, produces the same image.

### Structural shader findings (public vendor source, still valid)

- **lilToon encodes the render mode by switching the shader asset.** AMUSE's lilToon
  attestation accepts one shader name and its opaque pass. So it attests only lilToon
  materials that are already opaque. Transparent and cutout lilToon variants resolve as
  semantics-unknown — and these variants are exactly the population that alpha separation
  targets. Converting them would mean switching the shader asset, and also reconciling a
  large set of render-state properties. AMUSE models none of these properties.
- **Poiyomi uses one shader with a mode property**, plus depth-write, blend factors,
  blend op, premultiplied alpha, and per-material render-queue overrides.
- **A locked Poiyomi material is a correct, expected, conservative refusal**, never a
  defect. The identity check rejects the optimized, locked state outright. No unlocking
  experiment is needed, and this investigation ran none.

This note removes the corpus population figures that once went with these findings,
because they came from the invalid scan.

### Conclusion — and what is superseded

At the time, nothing in AMUSE could justify a render-mode conversion. Cloning a material
and flipping its mode, depth-write, and queue would have been an unproven transformation.

That part stands. **A later finding supersedes the recommendation that followed it:**

> ~~Geometry-only separation in the first slice: split the submesh into two output
> submeshes and assign the same original material to both slots.~~

**Superseded.** Splitting the geometry while applying the *same alpha material* to both
slots does not give the intended optimization. The target benefit is opaque rendering of
the proven-opaque triangles — moving them out of transparent queue, blend, and depth
behavior. Two submeshes, both drawn with the original alpha material, render exactly as
before. So the overdraw benefit is zero, and the split can add a draw call. This approach
exercises the mutation architecture, but it is not the optimization. Do not describe it
as a first increment of the optimization.

The opaque-material conversion is a separate capability that needs its own proof and
needs render-state understanding. See §11 for the current dependency direction.

## 5. Mesh transformation invariants

No cloning facility exists yet: NDMF has none, and the repository has none outside tests.

**Preservation checklist** — still valid as an engineering requirement:

| State | Requirement |
|---|---|
| positions | copy exactly |
| normals, tangents | copy exactly — do not recompute |
| colors | copy exactly when present |
| UV0–UV7 | copy every present channel exactly, presence is per-channel |
| boneWeights, bindposes | copy exactly |
| blendshape names / frame count / frame weights | preserve exactly and in order |
| blendshape per-frame delta vertices/normals/tangents | copy exactly |
| index format | preserve, or promote 16→32 only if required, never demote |
| submesh count / descriptors | **changes by design** — this is the transformation |
| topology | triangles only (enforced) |
| baseVertex | index reads apply it by default. Write absolute indices. |
| bounds | recompute or copy — must be at least as large as the original |
| mesh name | deterministic generated name |

**Vertex duplication is not required.** Submeshes can freely share vertices, so splitting
an index buffer needs no vertex changes. Keeping the vertex arrays byte-identical also
keeps blendshapes and skinning correct, because both index by vertex. **Index-only
separation should be an explicit invariant.**

**Open again:** whether the Editor can reliably read the full reconstruction input set —
bindposes, bone weights, and blendshape frames — from a mesh marked non-readable. The
earlier yes answer came from the invalid scan. **This note withdraws that answer.** The
existing public `MeshReadabilityCharacterizationTests` covers only positions, UVs, and
index reads. This needs public characterization before anyone can assume it holds.

## 6. Renderer, slot and animation invariants

If only the shared mesh and shared materials change, everything else stays preserved for
free. Bones, root bone, local bounds, quality, blendshape weights, probe/shadow/light
settings, enabled state, and sorting order must stay untouched.

**Do not replace the renderer component.** Assigning a generated mesh is enough.
Replacement would break animation path bindings, component references, and NDMF's
object-registry entries. Blendshape *weights* live on the renderer. They survive a
shared-mesh swap only if the blendshape order and count stay the same.

**Appended slots only — shown by test in the public project.** With a clip that binds a
material-array element, appending a generated slot preserved the existing addressing.
Prepending redirected the existing material-swap animation onto AMUSE's generated slot
instead. **Inserting or prepending silently breaks material-swap animation.**

**Renderer-wide material property curves carry no per-slot information.** The existing
AMUSE characterization shows that the generated material binding set does not change
with slot count. So such a curve also applies to any new slot.

**A slot with more than one admitted material cannot be safely separated.** The proof
constrains alpha only. The proof never compares base color and emission across admitted
states. After separation, the swap still addresses the original slot, while the appended
slot keeps whatever AMUSE placed there. So the two halves diverge unless the admitted
materials are RGB-identical. This is why §3 requires a single admitted material per slot.

NDMF does not rescue this. Replaced-object registration serves error-report provenance
only. Also, the animator services context commits the graph before AMUSE's barrier runs.

## 7. NDMF lifecycle findings (pinned source, still valid)

- **`IAssetSaver` is a plain `BuildContext` property, not an extension.** No extension
  lifetime needs to stay open, so the pass topology needs no change. This resolves the
  lifecycle issue this investigation once suspected.
- **`BuildContext.Serialize()` auto-persists referenced generated assets.** At build end,
  it walks every asset reachable from the avatar root. This walk includes a skinned
  renderer's mesh and materials. So `Serialize()` saves a generated mesh or material
  assigned to the build renderer, with no explicit call needed.
- **Cleanup is asymmetric, and this is the trap.** Among saved-but-unreferenced assets,
  the end-of-build cleanup destroys only components and game objects. If AMUSE eagerly
  saves a mesh or material and then abandons it, cleanup never removes it. The asset
  stays permanently welded into the shipped generated-asset container.
- **Therefore: do not save assets during Prepare.** Let assignment plus
  auto-serialization own persistence, or save only after assignment succeeds. Saving also
  writes to the asset database. That write would make Prepare observably mutating, which
  contradicts the boundary's purpose.
- **No deterministic naming guarantee.** Unity uniquifies container paths, and the
  object's own name becomes the sub-asset name. So determinism is AMUSE's
  responsibility.
- **PlatformFinish is the correct mutation stage.** It is NDMF's last phase. The only
  other passes there touch neither meshes nor material slots.
- **Replaced-object registration is error-report provenance only.** It rewrites no
  animations and no component references. Call it for diagnostics. Never rely on it for
  correctness. AMUSE needs no custom provenance framework.

## 8. Prepare / Apply boundary and failure taxonomy

The existing scaffold matches the merged design:

```
retained plan + widened immutable geometry/renderer capture
      ↓ Prepare(assetSaver)         ← no Unity writes, no asset saving
   prepared object { generated Mesh (in memory), final Material[], logical identity }
      ↓ Apply()                     ← the ONLY mutation
   renderer.sharedMesh / sharedMaterials
      ↓ (build end)
   Serialize() auto-persists both
```

Constructing a mesh in memory is not observable mutation. Saving an asset is observable
mutation, and Prepare excludes it.

**Failure taxonomy.** The following are all **expected renderer-scoped refusals**, and
most already have a name: unsupported renderer type, missing mesh, non-triangle topology,
property blocks, slot/submesh mismatch, animated mesh or slot count, locked or
unattested shaders, and transparent lilToon variants. A slot with more than one admitted
material is a **new** condition that needs a name. Having no opaque triangles is not a
refusal at all.

**Defects** must reach NDMF as build-blocking errors. These include: a plan that
references a triangle outside its source, opaque and transparent ordinals that do not sum
to the triangle count, a generated index outside the vertex range, a missing asset saver
despite lifecycle facts that claim one, or a Unity API call that throws after validated
preconditions. Any failure after the first Apply write must abort the build, because the
clone may be only partly transformed.

**Keep the no-catch policy in the dry analysis loop.** It already returns a named refusal
for every unsupported input, so an exception there *is* a defect. Converting a defect
into a skipped renderer would cause silent coverage loss — the exact failure mode the
proof-first model forbids.

**Do not add a refusal enum member yet.** Whether the multi-admitted-material condition
belongs in the analysis refusal vocabulary or in a new transformation vocabulary depends
on decisions nobody has made yet.

## 9. Census Lab — methodology correction

**The approved private root is `Assets/!CENSUSLAB/`.** **The authoritative corpus is
`Assets/!CENSUSLAB/Scenes/`.** Private launchers live under
`Assets/!CENSUSLAB/Scripts/Editor/`. The Lab's location on disk is found at runtime, and
this note never records it.

This investigation searched for the root under the wrong name. It concluded that no
approved Census structure existed. Then it substituted an arbitrary project-wide scan at
the asset level. Both the premise and the substitution were errors:

- The approved root does exist. This note **corrects and withdraws** the earlier claim
  that it did not, and the earlier claim that the wrongly named folder was the proper
  location.
- Current Census policy **does not permit** substituting a project-wide corpus when the
  approved corpus is required.

**This note removes every result from that scan, and none of them is valid evidence.**
The removed results include every renderer, prefab, mesh, material, texture,
shader-family, mode, slot, blendshape, index-format, sharing, and candidate-population
figure, plus every ratio and distribution built from them. This note does not replace
them with substitute metrics. Any design question they seemed to settle stays open until
the team re-characterizes it against the approved corpus.

For the record: that investigation **did not change** any Census Lab content. It wrote no
asset, scene, material, mesh, prefab, or setting. It opened or saved no scene, and it
created no folder. It confirmed instance identity by an exact data-path match, not a
hard-coded path. The package versions in the Lab matched AMUSE's pins. It wrote no
private observation to disk, produced no per-entity or per-avatar row, and introduced no
new publishable Census metric.

Some qualitative conclusions survive, because they follow from public code and vendor
source, not from the invalid scan. Transparent and cutout lilToon variants are
unattestable today. Locked Poiyomi materials are expected refusals. Real avatar content
makes shared meshes, blendshapes, and skinning realistic constraints. The slice must
handle them, not exclude them.

## 10. Census automation — proposed, not implemented

The public research package already gives the observation, anonymization,
aggregation, and guard infrastructure, with load-bearing privacy tests. **This
investigation did not change it, and nobody should reinvent it.** The intended division
stays a proposal: reusable logic in the public research package, a thin private launcher
under `Assets/!CENSUSLAB/Scripts/Editor/`, and no Census dependency in the product
package. This investigation created no launcher. Creating one stays a controller
decision, not an implementation detail.

This note makes no privacy-contract change and proposes none.

## 11. Current dependency direction

The vertical slice is the *consumer*, not the next task. Current order:

1. **Poiyomi Replace / no-mask alpha semantics — merged (PR #22).**
2. **Pinned Poiyomi opaque conversion** — investigated, not implemented. See the
   render-state note.
3. **Real runtime texture-evidence investigation** — not started. It gates
   assigned-mask alpha and any texture-backed triangle proof on real avatars.
4. **Alpha-separation vertical slice** — this note's subject. It is blocked on the items
   above.

Nothing above item 1 is implemented. Do not read this note as a claim otherwise.

## 12. Findings that remain valid

1. `MeshSeparationPlan` is host-independent, complete for its purpose, and must not
   change.
2. It carries enough to derive output index buffers.
3. The production pass discards the plan. Keeping it is the first required change.
4. `AmuseBuildOperation`'s Prepare/Apply boundary exists, is tested, and has no
   production caller.
5. `IAssetSaver` is a `BuildContext` property — the suspected lifecycle issue dissolves.
6. `Serialize()` auto-persists referenced generated meshes and materials.
7. Eagerly saving then abandoning an asset leaks it permanently, so Prepare must not
   save.
8. PlatformFinish is the correct mutation stage. No other pass there touches meshes or
   slots.
9. Replaced-object registration is error-report provenance only, never an animation
   rewriter.
10. Appending a material slot preserves existing addressing. Inserting or prepending
    breaks it — shown by test in the public project.
11. Renderer-wide material property curves do not change with slot count.
12. A slot with more than one admitted material cannot be safely separated, because the
    proof constrains alpha only.
13. Index-only separation needs no vertex duplication, keeping skinning and blendshapes
    correct.
14. `MaterialSemantics` models no render state, so render-mode conversion was unprovable
    at the time of writing.
15. lilToon transparency is a different shader asset and is unattested today.
16. Locked Poiyomi is a correct expected refusal, not a defect.

## 13. Open questions

**Blocking:** the render-state and texture-evidence prerequisites in §11, the honest
benefit story for any first increment given §4, and whether non-readable meshes expose
the full reconstruction set (§5) — this last item now needs public characterization.

**Deferrable to the slice's own design:**

- the widened-capture record shape, kept explicitly slice-scoped rather than becoming a
  universal mesh domain
- deterministic generated-asset naming and logical output identity
- bounds recompute versus copy
- index-format promotion policy
- where the retained plan lives in build state
- which refusal vocabulary the multi-admitted-material condition belongs to

**Deferred beyond the slice:**

- lilToon transparent-variant attestation
- multi-slot and multi-submesh separation
- wholly-opaque candidate handling
- profitability and cost modeling
- cross-renderer planning
- any Census launcher or schema extension
</content>
