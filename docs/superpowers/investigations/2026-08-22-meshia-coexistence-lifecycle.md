# AMUSE Meshia coexistence lifecycle investigation

## Status and scope

**Outcome A approved; durable investigation record finalized.**

This document records a source-first architectural investigation. It does not implement an NDMF plugin, AMUSE alpha mutation, Meshia integration, optimizer cooperation, a plugin registry, or a preservation theorem.

- Branch: `investigate/meshia-coexistence-lifecycle`
- Base: `origin/main` at `889a7b0b98199dcfc7778695645eadfb524af067`
- Required predecessors: the merged [single-stage optimization lifecycle investigation](2026-08-22-single-stage-optimization-lifecycle.md) and [coexisting-optimizer lifecycle investigation](2026-08-22-coexisting-optimizer-lifecycle.md) are present on the base
- Census Lab/private avatars: not used or modified
- Production code and architecture specifications: not modified
- External tools: inspected at exact public source revisions; not modified
- Unity probes: none required; exact pinned source distinguished the architectural hypotheses
- Performance evidence: source-derived tradeoff analysis, not a runtime benchmark

The approved result is **Outcome A — ordinary pre-AMUSE state producer**:

> For the current targeted alpha transformation and current state-based coexistence architecture, Meshia should finish before AMUSE. AMUSE should analyze the resulting ordinary Unity mesh state. No Meshia provenance, source-to-destination mapping, or Meshia-specific semantic cooperation is required.

This conclusion is intentionally scoped. It does not claim that no future AMUSE transformation could ever need Meshia provenance. A future transformation with a demonstrated dependency on transformation history, discarded intent, or source-to-destination correspondence must establish its own evidence.

Meshia provenance and Meshia lifecycle presence are also distinct:

- with an authoritative post-`Optimizing` barrier such as the current beginning-of-`PlatformFinish` candidate, AMUSE does not need to know Meshia specifically;
- with AMUSE in the same final `Optimizing` phase, Meshia is a concrete ordering dependency unless another authoritative generic mechanism is found.

## Decision summary

The preferred lifecycle is:

```text
prior generators
    -> Optimizing phase
         - Meshia simplification
         - AAO after Meshia when present
         - other applicable Optimizing work without an implied
           relative order unless separately constrained
    -> AMUSE semantic barrier
    -> eager immutable extraction
    -> interpretation, proof, and planning
    -> authorized AMUSE mutation
    -> DAO Candidate A/B cooperation
    -> bounded, modeled, irrelevant, or refused remainder
```

The current targeted alpha rule is:

> Meshia can change topology, triangle membership through removal, vertex indexing, positions, UVs, vertex attributes, skinning data, and blendshape data. Any AMUSE proof that depends on those facts must therefore be made against the post-Meshia mesh.

Once Meshia finishes, its generated mesh, submeshes, attributes, renderer binding, and unchanged materials are ordinary observable Unity state. The current alpha proof needs that final state, not the collapse history that produced it.

Meshia strengthens the case for a generic barrier after all NDMF `Optimizing` producers. It does not select `PlatformFinish` as the production mutation phase because NDMF still describes that phase as platform cleanup and validation territory. The future design must weigh that phase-intent cost against the correctness and scalability cost of same-phase known-plugin ordering.

## Question and decision criteria

The central question is:

> Can Meshia Mesh Simplification be treated entirely as an ordinary pre-AMUSE NDMF `Optimizing` state producer, and what does its existence imply for AMUSE's production semantic-barrier choice and optimizer ordering?

The secondary performance question is:

> Does requiring Meshia to finish before AMUSE materially reduce the best safely achievable avatar optimization compared with alternative ordering or cooperation arrangements?

The decision criteria, in order, are:

1. correctness and behavior preservation;
2. final practical avatar optimization quality;
3. automatic and comprehensible lifecycle behavior;
4. ecosystem compatibility and future optimizer scalability;
5. maintenance and coupling burden.

Unknown or unsupported evidence never increases optimization aggressiveness.

## Binding architectural inputs

This investigation consumes the approved coexistence architecture without reopening it:

- state-based composition is AMUSE's default interoperability model;
- the targeted alpha transformation is semantically NDMF-complete once every remaining proof-relevant later actor is ordered before AMUSE, modeled, proven irrelevant, or causes conservative refusal;
- the strongest discovered broad barrier candidate is the beginning of NDMF `PlatformFinish`, but it is not the selected production phase;
- AAO, Modular Avatar, and VRCFury normally belong before AMUSE and are consumed through resulting state;
- AMUSE must complete before cooperating DAO work;
- DAO Candidate A is the mandatory no-rewrite fallback;
- restricted DAO Candidate B is the recommended v1 semantic-preservation contract;
- Shader Toggles on AMUSE-protected geometry remain outside v1 Candidate B;
- no generalized interoperability framework is justified.

## Non-goals

This investigation does not:

- implement or modify AMUSE production code;
- implement Meshia integration or modify Meshia;
- modify AAO, DAO, VRCFury, Modular Avatar, NDMF, or lilToon;
- create a Meshia adapter, provenance store, callback inventory, optimizer registry, or generic cooperation API;
- define a source-to-destination vertex or triangle mapping;
- define a Meshia preservation theorem;
- implement alpha mutation;
- select `PlatformFinish` or final `Optimizing` as the production phase;
- start `design/coexisting-optimizer-lifecycle`;
- revise an existing architecture specification or investigation;
- use the Census Lab, private avatars, or uploads;
- make measured runtime-performance claims.

## Exact environment

| Component | Exact identity | Evidence |
|---|---|---|
| Repository base | `889a7b0b98199dcfc7778695645eadfb524af067` | branch creation and Git ancestry |
| Unity | `2022.3.22f1`, revision `887be4894c44` | `ProjectSettings/ProjectVersion.txt` and binding predecessor |
| NDMF | `1.14.4`, upstream `7cf8a13444ac19e46ac2b4146bad209de15dc42d` | embedded package and binding predecessor |
| Meshia Mesh Simplification | `3.2.0`, tag and upstream `3d592d7edcbe9af3a77e03300525cefa906d51aa` | exact public checkout and release tag |
| Avatar Optimizer | `1.9.18-beta.1`, upstream `6e6babc53c4086e7b1038b50dc01b1e36f065ef1` | binding predecessor and exact public checkout |
| Modular Avatar | `1.18.3`, upstream `f8c5fd98463e1024cae0608d5449b3c1fb6b6c84` | binding predecessor and exact public checkout |
| VRCFury | upstream `dd7b8c9b538f1ddbb8ed2b1c6060094b5103816f` | binding predecessor |
| d4rkAvatarOptimizer | `4.6.0`, upstream `b2e500869610f7eea7645c8384eaecdf00167be4` | binding predecessor |
| VRChat SDK Base/Avatars | `3.10.4` | binding predecessor |
| lilToon | `2.3.4`, upstream `252fd8cfc46106d4967e95b3f2c788418502f227` | binding single-stage record |

Meshia's public release and exact package manifest both identify version `3.2.0`; the `3.2.0` tag points to `3d592d7edcbe9af3a77e03300525cefa906d51aa`.

## Evidence hierarchy

- **Existing AMUSE contract:** behavior established by merged AMUSE code or an approved durable record.
- **Exact-version source fact:** behavior enforced by the exact pinned source; it is not generalized to another revision.
- **Source-derived projection:** a result derived from exact source control flow and explicitly stated input conditions.
- **Empirical evidence:** behavior observed through a controlled exact-version probe. No new empirical evidence was created here.
- **Inference:** a conclusion derived from source facts and labeled as such.
- **Unknown:** insufficiently established evidence; it cannot authorize mutation.

External optimizer intent is never promoted into AMUSE proof merely because an optimizer aims to preserve appearance.

## Sources inspected

### Existing AMUSE state and records

- [coexisting-optimizer lifecycle investigation](2026-08-22-coexisting-optimizer-lifecycle.md)
- [single-stage optimization lifecycle investigation](2026-08-22-single-stage-optimization-lifecycle.md)
- [SDK build-environment contract](2026-08-22-sdk-build-environment-contract.md)
- [analysis snapshot and ordering](../specs/2026-08-21-analysis-snapshot-ordering-design.md)
- [general-purpose transformation boundary audit](../audits/2026-08-22-general-purpose-transformation-boundaries.md)
- current `UnityRendererAlphaAnalysis`, `TriangleAlphaClassifier`, exact UV geometry, and immutable mesh-separation planner implementation

The current AMUSE implementation reads current renderer, mesh, submesh, material, position, UV0, and triangle state. It analyzes and plans but does not yet contain its production NDMF plugin or mutation executor.

### Exact Meshia source

The investigation inspected the exact `3d592d7...` revision of:

- [package manifest](https://github.com/RamType0/Meshia.MeshSimplification/blob/3d592d7edcbe9af3a77e03300525cefa906d51aa/package.json);
- [NDMF plugin definition](https://github.com/RamType0/Meshia.MeshSimplification/blob/3d592d7edcbe9af3a77e03300525cefa906d51aa/Ndmf/Editor/NdmfPlugin.cs);
- [direct simplifier component](https://github.com/RamType0/Meshia.MeshSimplification/blob/3d592d7edcbe9af3a77e03300525cefa906d51aa/Ndmf/Runtime/MeshiaMeshSimplifier.cs);
- [cascading simplifier and Modular Avatar references](https://github.com/RamType0/Meshia.MeshSimplification/blob/3d592d7edcbe9af3a77e03300525cefa906d51aa/Ndmf/Runtime/MeshiaCascadingAvatarMeshSimplifier.cs);
- [renderer mesh access and replacement](https://github.com/RamType0/Meshia.MeshSimplification/blob/3d592d7edcbe9af3a77e03300525cefa906d51aa/Ndmf/Runtime/RendererUtility.cs);
- [simplifier options](https://github.com/RamType0/Meshia.MeshSimplification/blob/3d592d7edcbe9af3a77e03300525cefa906d51aa/Runtime/MeshSimplifierOptions.cs);
- [core simplifier](https://github.com/RamType0/Meshia.MeshSimplification/blob/3d592d7edcbe9af3a77e03300525cefa906d51aa/Runtime/MeshSimplifier.cs);
- [simplification job](https://github.com/RamType0/Meshia.MeshSimplification/blob/3d592d7edcbe9af3a77e03300525cefa906d51aa/Runtime/Jobs/SimplifyJob.cs);
- [Smart Link filtering](https://github.com/RamType0/Meshia.MeshSimplification/blob/3d592d7edcbe9af3a77e03300525cefa906d51aa/Runtime/Jobs/RemoveHighCostSmartLinksJob.cs);
- [mesh writing and blendshape output](https://github.com/RamType0/Meshia.MeshSimplification/blob/3d592d7edcbe9af3a77e03300525cefa906d51aa/Runtime/Jobs/WriteToMeshDataJob.cs);
- previews, editor bake action, documentation, and tests.

### Exact ordering sources

The investigation also inspected:

- NDMF `1.14.4` plugin identity, phase order, `BeforePlugin`/`AfterPlugin` constraints, plugin phantoms, optional dependencies, resolver fallback ordering, and topological sort;
- [AAO's pinned NDMF plugin](https://github.com/anatawa12/AvatarOptimizer/blob/6e6babc53c4086e7b1038b50dc01b1e36f065ef1/Editor/OptimizerPlugin.cs);
- [Modular Avatar's pinned plugin phases](https://github.com/bdunderscore/modular-avatar/blob/f8c5fd98463e1024cae0608d5449b3c1fb6b6c84/Editor/PluginDefinition/PluginDefinition.cs);
- [Modular Avatar object references](https://github.com/bdunderscore/modular-avatar/blob/f8c5fd98463e1024cae0608d5449b3c1fb6b6c84/Runtime/AvatarObjectReference.cs);
- [Modular Avatar garbage collection](https://github.com/bdunderscore/modular-avatar/blob/f8c5fd98463e1024cae0608d5449b3c1fb6b6c84/Editor/OptimizationPasses/GCGameObjectsPass.cs);
- the binding VRCFury, DAO, SDK, and lilToon sources recorded by the predecessor investigations.

## Exact Meshia identity and NDMF lifecycle

Meshia exports the type:

```text
Meshia.MeshSimplification.Ndmf.Editor.NdmfPlugin
```

The type derives from `Plugin<NdmfPlugin>` and does not override `QualifiedName`. Under pinned NDMF `1.14.4`, `Plugin<T>.QualifiedName` defaults to `typeof(T).FullName`. Meshia's exact plugin qualified name is therefore:

```text
Meshia.MeshSimplification.Ndmf.Editor.NdmfPlugin
```

Its display name is:

```text
Meshia NDMF Mesh Simplifier
```

The plugin is exported through `ExportsPlugin`. When NDMF `>=1.8.0` is installed, its conditional `[RunsOnAllPlatforms]` attribute is enabled. The package does not declare NDMF as a VPM dependency; its NDMF editor assembly is conditionally enabled when NDMF is present. All ordering conclusions in this record apply to the exact NDMF `1.14.4` environment.

### Resolving

When Modular Avatar is installed, Meshia registers one `BuildPhase.Resolving` pass named `Resolve References`. It enumerates cascading simplifiers and calls `ResolveReferences()`, which resolves each `AvatarObjectReference` while hierarchy state is still early.

Meshia declares no explicit Resolving edge against Modular Avatar. Both passes complete before Modular Avatar's principal `Transforming` work. Their incidental relative fallback order is not required for the current conclusion: Meshia resolves its own cascading references, and actual simplification does not occur until `Optimizing`.

### Optimizing

Meshia performs its production mesh simplification in `BuildPhase.Optimizing`. Its sequence declares:

```text
BeforePlugin("com.anatawa12.avatar-optimizer")
```

The pass:

1. enumerates direct `MeshiaMeshSimplifier` components, including inactive descendants;
2. admits direct entries whose component is enabled and has a supported renderer;
3. when MA is installed, enumerates cascading simplifiers and valid enabled renderer entries;
4. reads each renderer's current mesh;
5. creates a new destination `Mesh` for every admitted entry;
6. simplifies all admitted entries through `MeshSimplifier.SimplifyBatch`;
7. adds each generated mesh to `context.AssetContainer` when it exists;
8. assigns each generated mesh to the `MeshFilter.sharedMesh` or `SkinnedMeshRenderer.sharedMesh`;
9. destroys all direct Meshia instruction components;
10. destroys all cascading Meshia instruction components.

Disabled direct simplifiers do not simplify but are still removed by the cleanup loop. Cascading build selection does not check the component's inherited enabled state; it checks entry validity and entry enablement. These exact preview/build selection details do not change the state-based conclusion but should not be generalized away.

### Preview, Play, bake, and public APIs

The Optimizing pass registers two NDMF render filters: one for direct simplifiers and one for cascading simplifiers when MA is installed. Preview simplification operates on proxy meshes, assigns a temporary simplified mesh to the proxy, and destroys that temporary mesh when the preview node is disposed. It does not persistently mutate the source renderer.

Preview targeting checks active/enabled state more strictly than the build enumeration. Therefore preview and build target selection are not certified as identical at the pinned revision.

NDMF Apply-on-Play or a normal NDMF build executes the same registered phases against the build clone. Meshia has no separate automatic Play-only simplification pass.

Outside its NDMF lifecycle, Meshia exposes synchronous, asynchronous, batch, and incremental simplifier APIs. Its inspector also exposes a user-invoked bake action that can create a persistent mesh asset. These are explicit caller/editor actions, not hidden later work following the NDMF Optimizing pass.

### Repeat execution and overlapping targets

The core API permits incremental repeated simplification through `ScheduleSimplify`. The automatic NDMF pass normally performs one batch operation per admitted component or cascading entry.

The pinned wrapper does not reject the same renderer appearing in both a direct simplifier and a cascading entry. In that case both outputs are independently computed from the renderer mesh observed while parameters are gathered, then assigned in wrapper iteration order; the last assignment becomes ordinary final state. This behavior does not require AMUSE provenance because AMUSE must analyze the final assigned mesh, but duplicate targeting is a future diagnostics and prevalence question.

No later automatic Meshia pass follows component cleanup.

## Combined lifecycle reconstruction

The strongest source-backed combined timeline is:

```text
NDMF early hook at -11000
    -> Resolving phase
         - Meshia cascading reference resolution when MA is installed
         - Modular Avatar Resolving work
         - their relative same-phase order is not relied upon
    -> Transforming phase
         - Modular Avatar principal transformation

VRCFury main hook at -10000
    -> proof-relevant VRCFury generation

NDMF optimize hook at -1025
    -> Optimizing phase
         - Meshia simplification before AAO when AAO is installed
         - Modular Avatar garbage collection at an otherwise
           unconstrained same-phase position
         - other Optimizing work without an implied relative order
           unless separately constrained
    -> beginning-of-PlatformFinish candidate AMUSE barrier
    -> NDMF completion

authoritatively ordered cooperating remainder
    -> AMUSE before DAO Candidate A/B
    -> modeled, irrelevant, or refused late actors
```

The diagram distinguishes guarantees from incidental order:

- phase edges are authoritative;
- Meshia before AAO is authoritative when AAO exists;
- MA Transforming before Meshia Optimizing is authoritative by phase;
- VRCFury main generation before NDMF Optimizing is binding exact-source evidence;
- the relative order of Meshia and MA's Optimizing garbage collection is not explicitly constrained;
- a beginning-of-`PlatformFinish` AMUSE barrier is after all of them by phase;
- same-phase AMUSE ordering is unresolved unless explicit authoritative edges are added.

### A. Meshia and AMUSE

At a beginning-of-`PlatformFinish` barrier, Meshia is authoritatively before AMUSE without an identity-specific edge. In same-phase final `Optimizing`, no Meshia-AMUSE edge currently exists, so type-name fallback would be deterministic but incidental and insufficient as proof authority.

### B. Meshia, AAO, and AMUSE

Meshia is authoritatively before AAO. AAO then reads current renderer/mesh state, performs its own mesh, material, slot, controller, hierarchy, and texture optimizations, and may replace or merge Meshia's generated mesh. AMUSE should analyze AAO's completed output:

```text
Meshia -> AAO -> AMUSE
```

If AMUSE has an authoritative after-AAO relationship, Meshia becomes transitively before AMUSE while AAO is installed. That transitive guarantee does not survive AAO's absence.

### C. Meshia, Modular Avatar, and AMUSE

Meshia's actual simplification occurs after MA's principal Transforming work. A moved surviving target remains reachable through the object reference or object identity. A target that another transformation destroys or replaces can become invalid and is skipped; this loses Meshia opportunity but leaves coherent ordinary state for AMUSE.

MA's Optimizing garbage collector has no explicit relative edge with Meshia. If it runs first, renderer components and Meshia references mark relevant target objects. If Meshia runs first, the remaining renderer component still marks the object. The pinned GC does not provide a later hidden mesh rewrite.

No MA-Meshia-AMUSE semantic contract is required for the current alpha path.

### D. Meshia, VRCFury, and AMUSE

VRCFury's main generation occurs after NDMF Transforming and before NDMF Optimizing. Meshia therefore sees the current surviving renderer and mesh state produced by VRCFury. If VRCFury destroys a component-bearing target, Meshia cannot simplify that removed target; it does not retain stale later work.

The pinned later VRCFury parameter compressor does not write the mesh/material/clip bindings relevant to the current targeted alpha theorem. The existing VRCFury conclusion remains ordinary-state composition with conservative refusal for unsupported reachable semantics.

### E. Meshia, DAO, and AMUSE

The preferred chain remains:

```text
Meshia -> AMUSE -> DAO Candidate A/B
```

Meshia produces ordinary mesh state. AMUSE proves and transforms that state. DAO receives the already simplified, AMUSE-transformed renderer and mesh state. Meshia adds no new protected semantic profile and no new later rewrite.

Without MA, DAO's callback remains tied numerically with NDMF's optimize hook at `-1025`; that existing ambiguity still requires the future authoritative AMUSE-before-DAO contract. Meshia neither causes nor resolves it.

### F. Meshia, AAO, DAO, and AMUSE

The source-compatible architecture is:

```text
Meshia -> AAO -> AMUSE -> DAO Candidate A/B
```

AAO can consume and subsequently replace or merge Meshia output. AMUSE analyzes AAO's final state. DAO cooperation remains after authorized AMUSE mutation.

### G. Realistic stress pipeline

For MA, VRCFury, Meshia, AAO, AMUSE, and DAO together:

```text
NDMF Resolving phase
    - Meshia cascading reference resolution when MA is installed
    - Modular Avatar Resolving work
    - their relative same-phase order is not relied upon
    -> Modular Avatar principal Transforming work
    -> VRCFury main generation
    -> NDMF Optimizing phase
         - Meshia before AAO when AAO is installed
         - MA garbage collection at an otherwise unconstrained
           same-phase position
         - other Optimizing work without an implied relative order
           unless separately constrained
    -> AMUSE barrier
    -> DAO Candidate A/B
    -> bounded remainder
```

MA garbage collection occurs during Optimizing at an otherwise unconstrained position but does not alter the conclusion. This combined pipeline is source-compatible, not an empirical certification of every version combination.

## Exact Meshia write-domain map

Meshia uses iterative quadric-error vertex merging. It can merge ordinary connected edge endpoints and, when Smart Link is enabled, close vertices that were not originally connected. It updates affected triangle references, discards triangles made degenerate by a merge, discards unreferenced vertices, compacts output vertices, and rewrites the mesh.

| Domain | Classification | Exact pinned behavior |
|---|---|---|
| Triangle count | simplified | reduced when valid merges discard degenerate triangles; target may remain unmet when no valid merge remains |
| Triangle topology | simplified/replaced | vertex references change and degenerate triangles are removed; no new triangle faces are synthesized |
| Vertex count | simplified | discarded vertices are omitted from output |
| Vertex positions | recomputed/interpolated | QEM-selected merge position, midpoint, or preserved endpoint depending on cost and preservation conditions |
| Vertex identity/indexing | replaced/reordered | surviving vertices are compacted in ascending source-index scan order and receive new indices |
| Normals | interpolated/recomputed | linear or barycentric interpolation followed by safe normalization |
| Tangents | interpolated/recomputed | XYZ interpolated and normalized; remaining component is survivor-dependent |
| Vertex colors | interpolated | linear or barycentric interpolation when present |
| UV0 | interpolated | linear or barycentric interpolation when present |
| UV1-UV7 | interpolated | same; all eight channels participate in pinned Smart Link UV-distance filtering |
| Bone weights | recomputed | weighted merge, strongest supported influences retained, then normalized |
| Bone indices | recomputed/reordered | selected with the retained merged weights |
| Bindposes | preserved exactly | source bindposes are copied to the new mesh |
| Overall mesh bounds | preserved exactly | source bounds are copied rather than recomputed |
| Submesh bounds | preserved exactly | source descriptor bounds are copied |
| Blendshape names | preserved exactly | names are copied |
| Blendshape frame weights | preserved exactly | frame weights are copied |
| Blendshape delta vertices | interpolated/simplified | merged or barycentrically interpolated and compacted to surviving vertices |
| Blendshape delta normals | interpolated/recomputed | interpolated and normalized |
| Blendshape delta tangents | interpolated/recomputed | interpolated and normalized |
| Submesh count | preserved exactly | destination count equals source count |
| Submesh ordering | preserved exactly | destination descriptors are written in source order |
| Triangle submesh membership | simplified but stable for survivors | a surviving source triangle remains in its original submesh; removed triangles disappear |
| Triangle ordering | preserved for survivors | surviving triangle order within each source submesh is retained |
| Material-slot relationship | unaffected/stable | renderer materials are untouched and submesh indices remain aligned |
| Index format | recomputed | writer chooses 16- or 32-bit output based on destination submesh vertex spans |
| Renderer mesh reference | replaced | `sharedMesh` is assigned the newly generated mesh |
| Mesh asset identity | replaced | each admitted entry receives a new `Mesh`; it is added to the NDMF asset container when available |
| Hierarchy | unaffected | simplifier itself does not add, remove, or move GameObjects |
| Renderer properties | unaffected except mesh reference | materials, enabled state, bounds properties, shadows, and other renderer configuration are not intentionally changed |
| Materials | unaffected | no material assignment or content rewrite |
| Shaders | unaffected | no shader rewrite |
| AnimatorControllers | unaffected | no controller rewrite |
| AnimationClips | unaffected | no clip rewrite |
| Material swaps | unaffected | no swap rewrite |
| Components | conditional cleanup | Meshia direct and cascading instruction components are destroyed after processing |

Non-triangle submesh data is copied/rebased rather than simplified as triangle faces. AMUSE's current renderer analysis refuses non-triangle topology, so this does not authorize broader support.

### Option consequences

`PreserveBorderEdges` prevents selected border vertices from being freely relocated or jointly removed. Cascading simplifiers can additionally preserve border vertices influenced by selected humanoid bones. Neither option proves preservation of general topology, UV domains, triangle identity, or alpha classification.

`PreserveSurfaceCurvature` contributes a curvature term to merge cost. It changes which merge is preferred, not the domains that may be written.

`UseBarycentricCoordinateInterpolation` selects barycentric rather than edge-linear interpolation for normals, tangents, colors, UV0-UV7, and blendshape deltas when a containing triangle is available. Bone weights still use the edge lerp factor.

Smart Link uses position distance to find candidates within each submesh span, then filters by normal dot, color distance, and all UV-channel distances present. Increasing those tolerances can admit merges across seams that stricter settings reject. These are visual-quality heuristics, not AMUSE semantic proof.

`MinNormalDot` rejects merges that would turn affected triangle normals beyond the configured threshold. It does not preserve exact geometry or orientation.

## Consequences for the current AMUSE alpha proof

Current `UnityRendererAlphaAnalysis` reads the renderer's current `sharedMesh` and `sharedMaterials`, verifies one material slot per submesh, requires triangle topology, reads vertex positions and UV0, reads submesh indices, and emits one exact alpha outcome per source-order triangle. `MeshSeparationPlanner` stores immutable copied indices, material binding indices, outcomes, and triangle ordinals.

Meshia can change each of the following proof inputs:

- vertex count and indices;
- triangle topology and count;
- triangle ordinals through removal;
- vertex positions used by exact degeneracy checks;
- UV0 values and therefore exact sampled texture domains;
- final triangle geometry within an unchanged material slot;
- deformation-relevant skinning and blendshape attributes;
- future proof-relevant vertex colors or identifier-sensitive attributes.

Therefore:

> Any AMUSE proof that depends on geometry, topology, UVs, vertex identity, deformation-relevant attributes, or triangle/material mapping must be made against the post-Meshia mesh.

A pre-Meshia per-triangle proof cannot safely be trusted after Meshia. Even though surviving triangles remain in their original submesh, their vertex indices, positions, UVs, and ordinals can change.

A narrow material-global fact independent of geometry and UV—such as an exactly supported equation that is proven opaque for every possible sample—could theoretically remain true. That does not preserve the per-triangle separation plan, and no preservation theorem is necessary when AMUSE can analyze the authoritative final mesh directly.

## Alpha-boundary adversarial cases

### Triangle wholly inside an opaque texture region

Meshia may leave it unchanged, move one or more vertices and UVs while retaining an opaque domain, move the domain into a non-opaque region, or discard the triangle. Post-Meshia AMUSE analysis classifies the actual result correctly. A pre-Meshia opaque result is insufficient.

### Triangle crossing an opaque/transparent boundary

Meshia may retain the crossing, move it, or eliminate it through an edge collapse. The final triangle can remain transparent/unknown or become provably opaque. This is legitimate input-state evolution, not proof preservation.

### Adjacent opaque and transparent triangles sharing vertices

A merge can alter attributes used by both triangles. Separating first could prevent cross-partition collapses, while simplifying first may reduce more geometry. Correctness requires proof after the last relevant simplification either way.

### UV interpolation and wrap behavior

Linear or barycentric interpolation can move a UV triangle across texture alpha regions. Repeat and clamp sampling can make a seam-crossing interpolation produce a materially different sampled domain. AMUSE's exact post-state classifier already models its supported repeat/clamp and point/bilinear semantics; no Meshia history is needed.

### Mirrored or discontinuous seams

Duplicate seam vertices normally expose distinct UV values. Smart Link can still consider geometrically close vertices and admits them only within configured UV-distance tolerances. User-selected broader tolerances can allow seam-changing merges. Final UVs remain observable.

### Border preservation

Preserving border endpoints can reduce topology change but does not establish alpha-boundary preservation. With preservation disabled, additional boundary-changing merges become possible. Both cases require final-state proof.

### Submesh and material boundaries

Meshia retains submesh count and order and does not move surviving triangles to another material slot. It can nevertheless remove triangles and change the geometry and UVs of surviving triangles within each slot. Final mapping is ordinary and sufficient.

### Blendshape and skinning behavior

Meshia rewrites weights, indices, and blendshape deltas. Any future AMUSE theorem sensitive to deformation must analyze those post-Meshia values or refuse. The current implementation's incomplete deformation/reachability coverage remains a general AMUSE limitation, not a reason for Meshia provenance.

### ID-sensitive and vertex-color-driven shaders

Meshia changes vertex indexing and interpolates colors. Any supported future shader semantics depending on those domains must read final state. Unsupported ID-sensitive or color-driven coverage remains a refusal. Meshia does not make it safer.

## State-based composition and provenance test

After Meshia completes, the resulting Unity state contains:

- the final vertex and index buffers;
- final positions, normals, tangents, colors, UV0-UV7, weights, and indices;
- final blendshape data and copied bindposes;
- final submesh descriptors and triangle/material-slot association;
- the renderer's final mesh reference;
- unchanged renderer material assignments and material semantics.

The destroyed Meshia component retains configuration and optimization intent, not runtime alpha semantics needed by the current proof. Meshia has no hidden later automatic pass that consumes that configuration after simplification.

For the current targeted alpha transformation, AMUSE does not need:

- source-to-destination vertex identity;
- source-to-destination triangle identity;
- edge-collapse order or cost;
- discarded source triangles;
- Meshia target counts or options;
- the fact that Meshia, rather than another producer, created the final mesh.

No concrete counterexample was found where current alpha correctness or planning requires Meshia provenance. The correct architecture is:

```text
Meshia transforms mesh A into mesh B
    -> AMUSE analyzes mesh B
```

It is not:

```text
AMUSE proves mesh A
    -> Meshia changes it
    -> AMUSE trusts the old proof or a collapse mapping
```

This provenance conclusion is limited to the current targeted alpha transformation and current state-based architecture. It is not a universal theorem about every future AMUSE transformation.

## Meshia and AAO ordering

Meshia's exact Optimizing sequence calls `BeforePlugin("com.anatawa12.avatar-optimizer")`. Under NDMF `1.14.4`, this places Meshia's sequence end before AAO's same-phase plugin-start phantom. When AAO is installed, AAO's Optimizing sequences are bounded by that plugin start and end, making the relationship authoritative:

```text
Meshia -> AAO
```

AAO constructs its mesh model from the current renderer mesh and can subsequently remove submeshes, edit geometry, merge skinned meshes and material slots, replace renderers, and write new mesh state. It therefore naturally consumes Meshia's simplified mesh. AMUSE then consumes AAO's ordinary final state.

No Meshia identity or mapping needs to survive through AAO.

## Meshia without AAO

When AAO is absent, Meshia's `BeforePlugin` call still creates or references AAO's phantom endpoint in the solver graph, but no installed AAO sequence connects its plugin start to plugin end. That phantom does not create an ordering relationship between Meshia and AMUSE.

If both Meshia and future AMUSE live in `Optimizing` without another edge, NDMF's resolver falls back to deterministic registration/type-name ordering. The fallback is implementation behavior, not an authoritative semantic contract.

Therefore final same-phase `Optimizing` would make Meshia a concrete known ordering dependency. An AMUSE-after-AAO edge would provide transitive Meshia ordering only when AAO is actually present; AAO cannot be a required bridge.

## Semantic-barrier consequence

### Candidate A: beginning of PlatformFinish

NDMF's built-in phase order guarantees that every `Optimizing` pass completes before any `PlatformFinish` pass. Placing the AMUSE semantic barrier at the beginning of `PlatformFinish` would therefore guarantee:

```text
Meshia and every other NDMF Optimizing producer
    -> AMUSE
```

AMUSE would not need to know Meshia's plugin identity. A future optimizer unknown when AMUSE was designed would receive the same ordering treatment.

This is a lifecycle conclusion, not a production phase selection. `PlatformFinish` remains documented for platform-specific cleanup and validation. Performing AMUSE mutation there carries an architectural phase-intent cost that the future design must address deliberately.

### Candidate B: final Optimizing with explicit constraints

Same-phase final `Optimizing` respects NDMF's nominal optimization phase but does not, at the pinned version, expose a generic authoritative “after all other Optimizing producers” primitive.

Available mechanisms constrain known plugin or pass identities. Type-name fallback is deterministic but incidental. Supporting Meshia would therefore require an explicit Meshia relationship unless another authoritative generic mechanism is discovered.

Meshia before AAO can make a future AMUSE-after-AAO edge transitive when AAO is installed. That does not handle Meshia without AAO. As more independent optimizers appear, one edge per known producer trends toward an allowlist and cannot automatically cover an optimizer that did not exist when AMUSE shipped.

### Consequence

Meshia **strengthens** the beginning-of-`PlatformFinish` barrier case because it is a real Optimizing producer with an incomplete incidental same-phase relationship to the rest of the ecosystem. It does not settle the choice because `PlatformFinish` phase intent remains a real cost.

No better authoritative generic same-phase NDMF `1.14.4` mechanism was found.

## Unknown-optimizer stress test

Meshia demonstrates the broader question:

> How does AMUSE safely compose with an NDMF Optimizing plugin that did not exist when AMUSE was designed?

Three models were compared:

1. **Explicit known-plugin edges:** workable for pinned known actors but scales as an allowlist and misses unknown producers.
2. **Generic later phase barrier:** automatically orders every completed Optimizing producer, including unknown ones, but may place AMUSE mutation in a phase documented for cleanup and validation.
3. **Another authoritative same-phase mechanism:** none was found in pinned NDMF source.

This result does not justify a plugin registry. The future design should select an authoritative lifecycle position, not model every producer's identity or implementation history.

## Performance ordering analysis

Correctness admits Meshia before AMUSE. The remaining question is whether that order materially reduces the best safe combined optimization.

### Meshia before AMUSE

Meshia simplifies original material regions and AMUSE then classifies the resulting triangles. This direction:

- minimizes geometry before AMUSE analysis;
- permits Meshia to collapse edges without AMUSE-created partition boundaries;
- can eliminate triangles AMUSE would otherwise move opaque;
- can move UVs into or out of provably opaque texture domains;
- can make the remaining proof easier or harder;
- ensures AMUSE authorizes exactly the geometry that will survive into its mutation.

A lower opaque-candidate count does not by itself indicate worse avatar performance. Meshia reduces geometry cost, while AMUSE targets transparency and overdraw cost.

### AMUSE before Meshia

Splitting opaque and transparent geometry first could prevent Meshia from collapsing across the semantic partition and could permit distinct target settings. It might preserve more opaque candidates in some boundary-heavy meshes.

It can also:

- introduce new boundaries and duplicate vertices;
- reduce available collapse opportunities;
- change Meshia target selection and component ownership;
- require Meshia to find and process AMUSE-generated outputs;
- still invalidate the original AMUSE proof through later topology, UV, geometry, skinning, or blendshape changes.

Any mesh Meshia changes after AMUSE would need a new final AMUSE proof. An AMUSE-first classification could guide partitioning or simplification constraints, but it could not remain the final mutation authority by itself.

### Fused or cooperating simplification

A hypothetical fused system could make supported alpha boundaries part of the simplifier's collapse constraints or cost function. It might trade geometry reduction against transparency opportunity more deliberately than either serial ordering.

Supporting that path would require a demonstrated benefit sufficient to justify some combination of:

- alpha-aware simplifier constraints;
- exact supported shader/texture semantics inside or alongside Meshia;
- partition or provenance exchange;
- topology, UV, deformation, and material-boundary postconditions;
- new versioned tests and coupling.

Pinned source establishes theoretical opportunity, not material practical value. No benchmark, prevalence evidence, or counterexample demonstrates a safe optimization deficit large enough to justify cooperation.

### Performance conclusion

Meshia-first is the safe v1 ordering. AMUSE-first or fused alpha-aware simplification remains a future performance pressure only. It should be reconsidered only if representative measurements show material combined performance that serial Meshia-before-AMUSE cannot recover.

## DAO consequence

Meshia does not revise the approved DAO hierarchy.

DAO receives ordinary renderer and mesh state. When ordered after AMUSE, it receives geometry that has already been simplified by Meshia and transformed by AMUSE. Meshia creates no protected semantic state that DAO must recognize, and Meshia has no later automatic pass that would rewrite DAO output.

Therefore:

- Candidate A remains the mandatory safe no-rewrite fallback;
- restricted Candidate B remains the recommended v1 preservation contract;
- protected Shader Toggles remain outside Candidate B;
- Candidate A/B does not need to mention Meshia;
- no Meshia-DAO-AMUSE three-way contract is justified.

The only relevant DAO issue remains authoritative AMUSE-before-DAO ordering, already established by the predecessor.

## Updated external-actor authority map

| Actor/pass | Exact identity | Relative order to AMUSE candidate barrier | Domains written | Result observable? | Producer provenance needed for current alpha? | Class | Evidence | Current alpha consequence |
|---|---|---|---|---|---|---|---|---|
| MA Resolving/Transforming | `1.18.3`, `f8c5fd9...` | before | hierarchy, mesh/bones, controllers, clips, materials/swaps | yes | no | ORDER | binding exact source | analyze resulting ordinary state |
| VRCFury main generation | `dd7b8c9...` | before Optimizing | controllers, clips, material state, hierarchy, generated assets | yes | no | ORDER | binding exact source | analyze reachable final state; unsupported reachability refuses |
| Meshia simplification | `3.2.0`, `3d592d7...`; plugin `Meshia.MeshSimplification.Ndmf.Editor.NdmfPlugin` | before by `PlatformFinish` phase; explicit edge required in same-phase final Optimizing | mesh topology, vertices, attributes, skinning, blendshapes, mesh reference, instruction components | yes | no for current targeted alpha transformation | ORDER | exact source | prove the post-Meshia mesh |
| MA Optimizing GC | `1.18.3`, `f8c5fd9...` | before PlatformFinish; same-phase relative order otherwise unconstrained | hierarchy deletion | yes | no | ORDER/IRRELEVANT to surviving target mesh semantics | exact source | removed objects cease to be candidates |
| AAO optimizer | `1.9.18-beta.1`, `6e6babc...`; `com.anatawa12.avatar-optimizer` | after Meshia when present; before PlatformFinish | meshes, materials, slots, controllers, hierarchy, textures | yes | no after completion | ORDER | exact source | consume AAO final state |
| Unknown completed NDMF optimizer | unknown | before by PlatformFinish phase | any ordinary supported domain | yes when supported | no when final state is sufficient | ORDER/REFUSE | phase fact plus semantic support | identity unnecessary at generic barrier; unsupported state refuses |
| AMUSE target mutation | not implemented | at selected future barrier | mesh/submesh/material target | planned | AMUSE-owned | ORDER | future requirement | one immutable authorized decision before mutation |
| DAO Candidate A/B work | `4.6.0`, `b2e5008...` plus future contract | must be after AMUSE when cooperating | renderer, mesh, materials, shaders, textures, animation depending on admitted profile | appears later | bilateral DAO postconditions, not Meshia provenance | ORDER/MODEL/REFUSE | binding predecessor | Candidate A fallback; restricted Candidate B only |
| lilToon callback 100 | `2.3.4`, `252fd8c...` | after | target shader realization | modeled | no | MODEL | binding record | exact target projection applies |
| Unknown later proof-relevant writer | unknown | after | unknown | insufficiently bounded | possibly | REFUSE | unknown | affected candidate remains preserved |

The expected Meshia classification is justified:

```text
Meshia simplification
    -> ORDER before AMUSE
    -> ordinary resulting state
    -> no Meshia-specific provenance or semantic cooperation
       for the current targeted alpha transformation
```

Meshia's presence is nevertheless lifecycle-relevant when the selected AMUSE position does not generically follow all Optimizing work.

## Outcome categories

### Outcome A — ordinary pre-AMUSE state producer

**Selected.** Meshia can safely finish before AMUSE. AMUSE can analyze the generated ordinary Unity mesh state. The current targeted alpha transformation needs no Meshia provenance or bilateral semantic cooperation.

### Outcome B — explicit ordering required, but no semantic cooperation

This is a conditional implementation consequence, not the selected broad outcome. If the future design chooses same-phase final `Optimizing`, Meshia requires a concrete authoritative edge unless a generic mechanism is found. With a post-Optimizing barrier, no Meshia-specific edge is necessary.

### Outcome C — narrow semantic cooperation justified

Not supported. Alternative ordering can theoretically change combined optimization opportunity, but no evidence demonstrates a material safe deficit sufficient to justify a preservation theorem or alpha-aware Meshia contract.

### Outcome D — existing architecture must be revised

Not supported. Meshia fits the existing state-based/barrier architecture and makes its lifecycle motivation more concrete.

## Architecture impact

No approved coexistence conclusion requires revision.

The future `design/coexisting-optimizer-lifecycle` branch should consume these additional constraints:

- Meshia is an exact real-world NDMF Optimizing state producer;
- AMUSE must be authoritatively after Meshia even when AAO is absent;
- a beginning-of-`PlatformFinish` barrier provides that order generically;
- same-phase final `Optimizing` needs a Meshia edge or another authoritative generic mechanism;
- an after-AAO edge alone is insufficient because AAO may be absent;
- deterministic type-name fallback is not proof authority;
- future unknown Optimizing plugins pose the same scalability question;
- no Meshia provenance, mapping, adapter, or semantic contract belongs in the current design scope;
- DAO Candidate A/B remains unchanged.

This sharpens the existing phase-selection question but does not expand it into a generalized interoperability system.

## Census Lab questions for later

The Census Lab was not used. Future privacy-reviewed aggregate evidence could measure:

- prevalence of direct and cascading Meshia components;
- prevalence of enabled, disabled, invalid, inactive, or overlapping targets;
- common relative and absolute triangle targets;
- which renderer categories are simplified;
- co-installation with AAO, DAO, MA, and VRCFury;
- number of AMUSE alpha candidates on Meshia-targeted renderers;
- candidate counts before and after simplification;
- how often final UV domains change alpha classification near boundaries or seams;
- geometry reduction, transparent-triangle reduction, and overdraw-related opportunity under serial orderings;
- whether a representative performance deficit remains after safe Meshia-before-AMUSE composition.

These questions inform prevalence, prioritization, and performance. They do not prove semantic safety or authorize cooperation.

## Remaining unknowns and risks

- production AMUSE phase selection remains unresolved;
- no authoritative generic same-phase final-Optimizing mechanism was found in NDMF `1.14.4`;
- future NDMF or Meshia revisions can change identities, phases, or write domains;
- real-world frequency and magnitude of alpha-candidate changes after simplification are unmeasured;
- no runtime comparison establishes whether partition-first or fused optimization is materially better;
- overlapping direct/cascading targets and preview/build selection differences were source-characterized but not empirically measured;
- arbitrary ID-sensitive, vertex-color-driven, deformation-dependent, or generated shader semantics remain unsupported until modeled;
- complete animation, material-swap, property-block, and deformation reachability remains an AMUSE implementation requirement;
- DAO without MA remains unsupported until authoritative AMUSE-before-DAO ordering exists;
- arbitrary multi-optimizer/version combinations are not certified by this source-only investigation.

Each unknown preserves or refuses affected AMUSE candidates rather than increasing aggressiveness.

## Probe decision

No Unity probe was required.

Exact pinned source directly established:

- Meshia's phases and plugin identity;
- the explicit before-AAO edge;
- NDMF's constraint and optional-phantom semantics;
- the absence of generic same-phase after-all ordering;
- mesh creation, replacement, asset ownership, and component cleanup;
- topology, UV0-UV7, color, tangent, normal, skinning, blendshape, submesh, bounds, and index-format behavior;
- the absence of a later automatic Meshia pass;
- final-state sufficiency for current AMUSE renderer analysis;
- the theoretical but unmeasured nature of alternative performance orderings.

A synthetic probe could illustrate one UV-boundary outcome, but it would not change the architectural conclusion or quantify representative performance. No disposable Unity assets or project state were created.

## Validation performed

Investigation validation consisted of:

- branch, base, status, log, and ancestry checks;
- confirmation that both required predecessor records were merged into the base;
- restoration of Unity-generated host-toolchain package churn after inspecting its complete diff, as required by repository policy;
- exact Meshia `3.2.0` tag, package version, and `3d592d7...` commit verification;
- exact Meshia NDMF wrapper, component, preview, editor, simplifier, jobs, blendshape, submesh, and test inspection;
- exact embedded NDMF `1.14.4` phase, identity, constraint, phantom, resolver, and fallback-order inspection;
- exact pinned AAO plugin and current-mesh consumption inspection;
- exact pinned Modular Avatar plugin, object-reference, and garbage-collection inspection;
- review of binding VRCFury, DAO, SDK, lilToon, and AMUSE lifecycle evidence;
- current AMUSE renderer extraction, exact alpha classification, and immutable separation-plan inspection;
- reconstruction of all requested combined timelines;
- adversarial write-domain and alpha-boundary analysis;
- source-derived comparison of Meshia-first, AMUSE-first, and fused/cooperating performance arrangements;
- line-by-line review against the approved preliminary findings and scope clarifications;
- working-tree review to confirm that only this investigation record was created.

No production tests were required or run because production code did not change. No Unity probe, Census Lab, private avatar, upload, external project mutation, or runtime benchmark was used. Meshia and every external tool remained unmodified.

## Recommended next branch

After this durable record is reviewed, approved, finalized, and merged, the recommended next branch remains:

```text
design/coexisting-optimizer-lifecycle
```

It should start from the then-current `origin/main` and consume this Outcome A together with the approved coexistence record. It should choose the AMUSE production phase and authoritative DAO ordering mechanism, require Meshia-before-AMUSE even without AAO, preserve Candidate A and restricted Candidate B, and define fail-closed validation without adding Meshia provenance or a generic optimizer registry.

No design branch is started by this investigation.
