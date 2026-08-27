# Alpha Separation Vertical Slice — Investigation

**Status: historical investigation. Several conclusions are materially superseded.**

This note recorded the first survey of what an alpha-separation vertical slice would
require. Its public-code, NDMF, mesh, slot, animation, lifecycle and mutation-boundary
findings remain useful and are preserved below. Three things about it are no longer valid:

1. **Its Census methodology was invalid.** It failed to locate the approved corpus and
   substituted an arbitrary project-wide, asset-level scan. Every result derived from that
   scan has been removed. **Those results are not valid evidence under current Census
   policy** and must not be cited, restored, or relied on. Questions they appeared to
   answer are open again until re-characterized against the approved corpus.
2. **Its `_CENSUSLAB` premise was wrong.** The note claimed no approved Census root
   existed. An approved root does exist (§9), and the claim has been corrected.
3. **Its headline recommendation is superseded.** Geometry-only separation applying the
   *same alpha material* to both slots does not accomplish the intended overdraw
   optimization (§4).

Census Lab observations are architectural pressure and validation evidence, never
correctness authority.

## 1. Repository baseline at the time

Branch `feat/alpha-separation-vertical-slice` from `main` at `b48b4ff`, working tree clean,
nothing committed. Unity 2022.3.22f1, NDMF 1.14.4, VRChat SDK 3.10.4 — all read from pinned
local source.

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

Three public-code findings shaped everything else, and all remain valid:

1. **The plan is computed and then thrown away.** `ClassifyRuntimeStates` builds a full
   `MeshSeparationPlan` and returns only its opaque triangle count. Nothing retains the
   plan at build scope. Retaining it is the first required change.
2. **The Prepare/Apply boundary already exists and is unwired.**
   `AmuseBuildOperation.Execute(lifecycle, assetSaver, prepare, apply)` is complete,
   documented and tested, with zero production callers.
3. **`UnityRendererMutationTarget` exists and is produced but never consumed**, carrying
   the renderer, expected mesh and expected material-slot count.

The merged upload-conditional authorization design and the general-purpose transformation
boundaries audit under `docs/superpowers/` remain the approved architecture for this area.

## 2. The proof-to-plan contract

`MeshSeparationPlan` preserves original submesh identity and is complete for its own
purpose. The captured snapshot feeding it holds only vertex count, positions, UV0,
per-submesh indices and the captured alpha materials. It therefore contains none of:

- vertex attributes beyond position/UV0 — normals, tangents, colors, UV1–UV7, bone weights,
  bindposes;
- blendshapes — names, frame counts, weights, per-frame deltas;
- mesh-level state — index format, bounds, base vertex, submesh descriptors, name;
- renderer-level state — bones, root bone, local bounds, blendshape weights, quality,
  probe/shadow settings;
- material identity beyond an opaque binding index (deliberately);
- **any description of what an opaque output material should be** (§4).

**Recommended boundary, still valid:** the plan stays proof/candidate output and feeds a
separate prepared application object. The merged design explicitly rejected plans carrying
host postconditions, and the boundaries audit requires the plan stay purpose-specific.
**Do not modify `MeshSeparationPlan`'s public contract.**

## 3. Proposed supported case

Structural constraints, still valid as a starting shape:

- exactly one `SkinnedMeshRenderer`, handled independently;
- slot count equal to submesh count (already enforced);
- triangle topology only (already enforced);
- **exactly one admitted material per affected slot** — a correctness requirement, not a
  simplification (§6);
- one source submesh dispositioned to split;
- blendshapes and skinning preserved;
- generated slot **appended at the end**, never inserted (§6);
- one generated replacement mesh; the renderer component itself not replaced.

Whether blendshaped and shared meshes are common enough to be mandatory in the first slice
was previously answered from the invalid scan. **That answer has been withdrawn.** The
conservative engineering position stands on its own: shared input meshes are possible, so
never mutate an input mesh; and blendshape/skinning preservation is required whenever
present.

## 4. Material/output strategy — the blocking gate

### What the proof actually establishes

`AdmittedMaterialStates` resolves only the alpha output. `MaterialSemantics` is exactly
`{ BaseColor, Alpha, Emission, Normal }` — **AMUSE models no render state at all**: no
render queue, depth write, blend factors, cull, alpha-to-coverage, stencil, keywords or
shader passes.

So a proven-opaque result means precisely: *the material's alpha equation evaluates to
opaque over these triangles under every admitted runtime state.* It does **not** establish
that the same material rendered in an opaque render mode produces the same image.

### Structural shader findings (public vendor source, still valid)

- **lilToon encodes render mode by switching the shader asset.** AMUSE's lilToon
  attestation accepts one shader name and its opaque pass, so it attests only already-opaque
  lilToon materials. Transparent and cutout lilToon variants — precisely the population
  alpha separation targets — resolve as semantics-unknown. Converting them would mean
  switching shader asset *and* reconciling a large set of render-state properties, none of
  which is modeled.
- **Poiyomi uses one shader with a mode property** plus depth-write, blend factors, blend
  op, premultiplied alpha and per-material render-queue overrides.
- **Locked Poiyomi is a correct, expected, conservative refusal**, never a defect: the
  identity check rejects the optimized/locked state outright. No unlocking experiment is
  needed and none was performed.

Corpus population figures that previously accompanied these findings came from the invalid
scan and have been removed.

### Conclusion — and what is superseded

Nothing in AMUSE at the time could justify a render-mode conversion; cloning a material and
flipping mode, depth-write and queue would have been an unproven transformation.

That part stands. **What is superseded is the recommendation that followed it:**

> ~~Geometry-only separation in the first slice: split the submesh into two output
> submeshes and assign the same original material to both slots.~~

**Superseded.** Splitting geometry while applying the *same alpha material* to both slots
does not accomplish the intended optimization. The target benefit is opaque rendering of
the proven-opaque triangles — moving them out of transparent queue/blend/depth behavior.
Two submeshes both drawn with the original alpha material render exactly as before, so the
overdraw benefit is zero, and the split can add a draw call. It exercises mutation
architecture, but it is not the optimization, and it should not be described as a
first increment of one.

The opaque-material conversion is a separate, independently-proven capability requiring
render-state understanding. See §11 for the current dependency direction.

## 5. Mesh transformation invariants

No existing cloning facility: NDMF has none and the repository has none outside tests.

**Preservation checklist** — still valid as an engineering requirement:

| State | Requirement |
|---|---|
| positions | copy exactly |
| normals, tangents | copy exactly — do not recompute |
| colors | copy exactly when present |
| UV0–UV7 | copy every present channel exactly; presence is per-channel |
| boneWeights, bindposes | copy exactly |
| blendshape names / frame count / frame weights | preserve exactly and in order |
| blendshape per-frame delta vertices/normals/tangents | copy exactly |
| index format | preserve, or promote 16→32 only if required; never demote |
| submesh count / descriptors | **changes by design** — this is the transformation |
| topology | triangles only (enforced) |
| baseVertex | index reads apply it by default; write absolute indices |
| bounds | recompute or copy — must be at least as large as the original |
| mesh name | deterministic generated name |

**Vertex duplication is not required.** Submeshes freely share vertices, so splitting an
index buffer needs no vertex changes. Keeping vertex arrays byte-identical also keeps
blendshapes and skinning trivially correct, because both are vertex-indexed. **Index-only
separation should be an explicit invariant.**

**Open again:** whether the full reconstruction input set — bindposes, bone weights and
blendshape frames — is reliably readable in the Editor from meshes marked non-readable. The
previous affirmative answer came from the invalid scan and has been withdrawn. The existing
public `MeshReadabilityCharacterizationTests` covers positions, UVs and index reads only.
This needs public characterization before it can be assumed.

## 6. Renderer, slot and animation invariants

If only the shared mesh and shared materials change, everything else is preserved for free.
Bones, root bone, local bounds, quality, blendshape weights, probe/shadow/light settings,
enabled state and sorting order must remain untouched.

**Do not replace the renderer component.** Assigning a generated mesh suffices; replacement
would break animation path bindings, component references and NDMF's object-registry
entries. Blendshape *weights* live on the renderer and survive a shared-mesh swap only if
blendshape order and count are preserved.

**Appended slots only — empirically demonstrated in the public project.** With a clip
binding a material-array element, appending a generated slot preserved the existing
addressing, while prepending redirected the existing material-swap animation onto AMUSE's
generated slot. **Inserting or prepending silently breaks material-swap animation.**

**Renderer-wide material property curves carry no per-slot information**, per the existing
AMUSE characterization that the generated material binding set does not vary with slot
count. Such a curve therefore also applies to any new slot.

**A slot with more than one admitted material cannot be safely separated.** The proof
constrains alpha only; base colour and emission are never compared across admitted states.
After separation the swap still addresses the original slot while the appended slot keeps
whatever AMUSE placed there, so the two halves diverge unless the admitted materials are
RGB-identical. Hence the single-admitted-material requirement in §3.

NDMF does not rescue this: replaced-object registration is error-report provenance only,
and the animator services context has already committed the graph before AMUSE's barrier
runs.

## 7. NDMF lifecycle findings (pinned source, still valid)

- **`IAssetSaver` is a plain `BuildContext` property, not an extension.** There is no
  extension lifetime to hold open, so no pass-topology change is needed. The previously
  suspected lifecycle issue dissolves.
- **`BuildContext.Serialize()` auto-persists referenced generated assets.** It walks assets
  reachable from the avatar root at build end; a skinned renderer's mesh and materials are
  traversed. A generated mesh or material assigned to the build renderer is therefore saved
  without an explicit call.
- **Cleanup is asymmetric, and this is the trap.** The end-of-build cleanup destroys only
  components and game objects among saved-but-unreferenced assets. A mesh or material that
  AMUSE eagerly saves and then abandons is never cleaned up and is permanently welded into
  the shipped generated-asset container.
- **Therefore: do not save assets during Prepare.** Let assignment plus auto-serialization
  own persistence, or save only after successful assignment. Saving also writes to the asset
  database, which would make Prepare observably mutating and contradict the boundary's
  purpose.
- **No deterministic naming guarantee.** Container paths are uniquified and the object's own
  name becomes the sub-asset name, so determinism is AMUSE's responsibility.
- **PlatformFinish is the correct mutation stage.** It is NDMF's last phase, and the only
  other passes there touch neither meshes nor material slots.
- **Replaced-object registration is error-report provenance only.** It rewrites no
  animations and no component references. Call it for diagnostics; never rely on it for
  correctness. No custom provenance framework is needed.

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

Constructing a mesh in memory is not observable mutation; saving an asset is, and is
excluded from Prepare.

**Failure taxonomy.** Unsupported renderer type, missing mesh, non-triangle topology,
property blocks, slot/submesh mismatch, animated mesh or slot count, locked or unattested
shaders, and transparent lilToon variants are all **expected renderer-scoped refusals**,
most already named. A slot with more than one admitted material is a **new** condition
needing a name. Having no opaque triangles is not a refusal at all.

**Defects**, which must reach NDMF as build-blocking errors: a plan referencing a triangle
outside its source; opaque and transparent ordinals not summing to the triangle count; a
generated index outside the vertex range; a missing asset saver despite lifecycle facts
claiming one; or a Unity API throwing after validated preconditions. Any failure after the
first Apply write must abort the build, because the clone may be partially transformed.

**Keep the no-catch policy in the dry analysis loop.** It already returns a named refusal
for every unsupported input, so an exception there *is* a defect. Converting defects into
skipped renderers would produce silent coverage loss — the exact failure mode the
proof-first model forbids.

**Do not add a refusal enum member yet.** Whether the multi-admitted-material condition
belongs in the analysis refusal vocabulary or a new transformation vocabulary depends on
decisions not yet made.

## 9. Census Lab — methodology correction

**The approved private root is `Assets/!CENSUSLAB/`** and **the authoritative corpus is
`Assets/!CENSUSLAB/Scenes/`**, with private launchers under
`Assets/!CENSUSLAB/Scripts/Editor/`. The Lab's location on disk is discovered at runtime and
is never recorded here.

This investigation searched for the root under a wrong name, concluded no approved Census
structure existed, and substituted an arbitrary project-wide, asset-level scan. Both the
premise and the substitution were errors:

- the approved root does exist — the earlier claim that it did not, and that the wrongly
  named folder was the proper location, are **corrected and withdrawn**;
- substituting a project-wide corpus when the approved corpus is required is **not
  permitted** under current Census policy.

**Every result derived from that scan has been removed from this note and is not valid
evidence.** That includes all renderer, prefab, mesh, material, texture, shader-family,
mode, slot, blendshape, index-format, sharing and candidate-population figures, and all
ratios and distributions built from them. They are not replaced with substitute metrics.
Any design question they appeared to settle is open until re-characterized against the
approved corpus.

Also recorded for accuracy: Census Lab content was **not modified** during that
investigation. No asset, scene, material, mesh, prefab or setting was written, no scene was
opened or saved, and no folder was created. Instance identity was confirmed by exact
data-path match rather than a hard-coded path. Package versions in the Lab matched AMUSE's
pins. No private observation was written to disk; no per-entity or per-avatar row was
produced; no new publishable Census metric was introduced.

Qualitative conclusions that survive, because they follow from public code and vendor source
rather than from the invalid scan: transparent and cutout lilToon variants are unattestable
today; locked Poiyomi materials are expected refusals; and real avatar content makes shared
meshes, blendshapes and skinning realistic constraints the slice must handle rather than
exclude.

## 10. Census automation — proposed, not implemented

The public research package already provides the observation, anonymization, aggregation and
guard infrastructure, with load-bearing privacy tests. **It was not modified and must not be
reinvented.** The intended division — reusable logic in the public research package, a thin
private launcher under `Assets/!CENSUSLAB/Scripts/Editor/`, and no Census dependency in the
product package — remains a proposal. No launcher was created, and creating one remains a
controller decision rather than an implementation detail.

No privacy-contract change was made, and none is proposed here.

## 11. Current dependency direction

The vertical slice is the *consumer*, not the next task. Current order:

1. **Poiyomi Replace / no-mask alpha semantics — merged (PR #22).**
2. **Pinned Poiyomi opaque conversion** — investigated, not implemented; see the
   render-state note.
3. **Real runtime texture-evidence investigation** — not started; gates assigned-mask alpha
   and any texture-backed triangle proof on real avatars.
4. **Alpha-separation vertical slice** — this note's subject; blocked on the above.

Nothing above item 1 is implemented, and this note should not be read as claiming otherwise.

## 12. Findings that remain valid

1. `MeshSeparationPlan` is host-independent, complete for its purpose, and must not change.
2. It carries enough to derive output index buffers.
3. The production pass discards the plan; retaining it is the first required change.
4. `AmuseBuildOperation`'s Prepare/Apply boundary exists, is tested, and has no production
   caller.
5. `IAssetSaver` is a `BuildContext` property — the suspected lifecycle issue dissolves.
6. `Serialize()` auto-persists referenced generated meshes and materials.
7. Eagerly saving then abandoning an asset leaks it permanently, so Prepare must not save.
8. PlatformFinish is the correct mutation stage; no other pass there touches meshes or slots.
9. Replaced-object registration is error-report provenance only, never an animation rewriter.
10. Appending a material slot preserves existing addressing; inserting or prepending breaks
    it — demonstrated in the public project.
11. Renderer-wide material property curves are slot-count-invariant.
12. A slot with more than one admitted material cannot be safely separated, because the
    proof constrains alpha only.
13. Index-only separation needs no vertex duplication, keeping skinning and blendshapes
    correct.
14. `MaterialSemantics` models no render state, so render-mode conversion was unprovable at
    the time of writing.
15. lilToon transparency is a different shader asset and is unattested today.
16. Locked Poiyomi is a correct expected refusal, not a defect.

## 13. Open questions

**Blocking:** the render-state and texture-evidence prerequisites in §11; the honest benefit
story for any first increment, given §4; whether non-readable meshes expose the full
reconstruction set (§5), which now needs public characterization.

**Deferrable to the slice's own design:** the widened-capture record shape, kept explicitly
slice-scoped rather than becoming a universal mesh domain; deterministic generated-asset
naming and logical output identity; bounds recompute versus copy; index-format promotion
policy; where the retained plan lives in build state; and which refusal vocabulary the
multi-admitted-material condition belongs to.

**Deferred beyond the slice:** lilToon transparent-variant attestation; multi-slot and
multi-submesh separation; wholly-opaque candidate handling; profitability and cost modelling;
cross-renderer planning; any Census launcher or schema extension.
