# Mesh Separation Plan Design

**Date:** 2026-08-15

**Status:** Awaiting approval

## Problem statement

The merged triangle classifier assigns each source triangle one result: `ProvenOpaque`, `MustRemainTransparent`, or `Unknown`. No pure layer yet turns these results into a deterministic description of a future mesh split. The next milestone needs that description. It must not mutate a Unity `Mesh`, create materials, touch a renderer, or couple the planning to NDMF.

The planner must preserve the source topology and the material-binding provenance that upstream stages already resolved. It must also enforce one safety rule:

> Only `ProvenOpaque` triangles can become eligible to leave the original transparent rendering path.

`MustRemainTransparent` and `Unknown` always stay on the retained-transparent side. A plan is a candidate transformation request. It is not proof that a later material stage can produce an opaque counterpart.

## Goals

- Accept immutable submesh index topology and one classifier outcome per source triangle.
- Produce an immutable, deterministic per-submesh partition of source triangle ordinals.
- Preserve the source submesh index and the caller-supplied material-binding index explicitly.
- Distinguish unchanged, wholly opaque-candidate, and mixed/split submeshes.
- Distinguish a valid structural no-op from malformed normalized input.
- Validate enough topology to prevent a later transformer from reading incomplete or out-of-range triangle indices.
- Keep the planner at `O(number of indices + number of triangles)` with small copied arrays.
- Leave host topology interpretation, material-layout support, final output numbering, and all mutation decisions to host adapters and later milestones.

## Non-goals

This milestone does not design or implement:

- Unity `Mesh` or renderer mutation
- new meshes, vertex duplication, vertex remapping, index-buffer rewriting, or compaction
- material creation, shader conversion, material-property rewriting, or material support analysis
- NDMF passes, avatar traversal, animation/material-swap tracing, or third-party integration
- mipmap or texture/shader analysis
- draw-call, triangle-count, GPU-cost, memory, or avatar profitability heuristics
- jobs, Burst, GPU work, caching frameworks, or parallelism
- new fixture catalogs, committed Unity mesh assets, CI, release, or private-testbed behavior

The planner does not depend on MCP or NDMF. It never reads a live `Mesh` or `Renderer`.

## Upstream classifier contract

The implemented `TriangleAlphaOutcome` enum in `TriangleAlphaClassifier.cs` is the upstream semantic boundary:

```csharp
internal enum TriangleAlphaOutcome
{
    ProvenOpaque,
    MustRemainTransparent,
    Unknown
}
```

The planner does not call the classifier and does not reinterpret its results:

| Classifier outcome | Plan membership |
|---|---|
| `ProvenOpaque` | opaque-candidate side |
| `MustRemainTransparent` | retained-transparent side |
| `Unknown` | retained-transparent side |

An undefined enum value is malformed input and throws. The planner never defaults it to either membership. The plan does not merge `Unknown` with `MustRemainTransparent` semantically. It only gives both values the same conservative transformation membership.

## Considered approaches

### 1. Immutable topology snapshot plus source triangle ordinals — selected

The planner copies the raw index array and the outcome array of each submesh into an immutable input. The plan retains that input. It stores two ordered lists of zero-based triangle ordinals per submesh. Ordinal `n` refers to source index entries `3n`, `3n + 1`, and `3n + 2`.

The plan does not copy every index triple into two result collections. It preserves winding and source order. An ordinal is unambiguous because the plan holds a reference to the immutable topology snapshot that produced it.

### 2. Copy full index triples into the plan

This option makes each result independently readable. But it duplicates topology. It hides whether identical triples are distinct source occurrences. It also raises the risk that future code treats a copied triple as identity. The plan already retains an immutable input, so this extra copy adds nothing.

### 3. Store only ordinals and require the caller to retain the original arrays

This option stores the fewest fields. But mutable caller arrays could make a previously valid plan stale. A topology fingerprint or version token would add more code than one copy of the arrays. The design rejects this option.

## Input model

The production boundary stays internal and Editor-only:

```csharp
internal sealed class SubmeshSeparationInput
{
    internal int SourceMaterialBindingIndex { get; }
    internal IReadOnlyList<int> Indices { get; }
    internal IReadOnlyList<TriangleAlphaOutcome> Outcomes { get; }
    internal int TriangleCount { get; }

    internal SubmeshSeparationInput(
        int sourceMaterialBindingIndex,
        IReadOnlyList<int> indices,
        IReadOnlyList<TriangleAlphaOutcome> outcomes);
}

internal sealed class MeshSeparationInput
{
    internal int VertexCount { get; }
    internal IReadOnlyList<SubmeshSeparationInput> Submeshes { get; }

    internal MeshSeparationInput(
        int vertexCount,
        IReadOnlyList<SubmeshSeparationInput> submeshes);
}
```

`MeshSeparationInput` contains only `VertexCount` by design. It contains no positions, normals, tangents, colors, UVs, bone weights, blend shapes, or other vertex attributes. The planner moves triangle references in concept only. It never reads or changes vertex data. `VertexCount` exists only to reject negative or out-of-range indices.

Each input submesh is an already-normalized triangle group. It carries a non-negative source material-binding index, its raw triangle index sequence, and the outcomes together. The input contract states that every consecutive three indices form one triangle. There is no topology enum. Outcome ordinal `n` corresponds exactly to index triple `n`.

There are no caller-supplied global index-buffer offsets and no submesh boundary ranges. Invalid or overlapping boundary arithmetic is therefore not representable in this API. Each copied submesh array is its own complete boundary. Tests instead validate the per-submesh result counts and that triangle ordinals restart at zero for each source submesh.

`SourceMaterialBindingIndex` is opaque provenance to the planner. It identifies the source draw/material binding that a later stage must consult. It does not imply a Unity material-slot array and does not equal the source submesh index. Negative binding indices are malformed.

Duplicate non-negative binding indices are valid because multiple geometry groups can legitimately share one material binding. The planner preserves these identifiers. It never invents them, checks them against a host material count, removes duplicates, sorts them, or otherwise interprets them.

`SourceSubmeshIndex` stays the output name for the normalized group ordinal. The word “Submesh” is the clearest term for the mesh-oriented pipeline of this repository, and Unity or Blender adapters can supply it directly. A rename of the whole model to generic groups would add abstraction with no behavior change. `SourceMaterialBindingIndex` is deliberately more neutral than “slot” because its meaning is host-resolved.

No Unity namespace, `MeshTopology`, `Mesh`, `Renderer`, `Material`, asset, or GameObject reference enters the planner boundary.

## Future host-adapter boundary

Host adapters normalize source-specific geometry and draw/material semantics before they call the core planner:

```text
Unity Mesh / Renderer / Material[]
        ↓
future Unity separation-input adapter
        ↓
generic MeshSeparationInput
        ↓
MeshSeparationPlanner
```

The future Unity adapter owns these tasks:

- reads `Mesh.subMeshCount`
- reads each submesh topology and index buffer
- reads the renderer material slots
- interprets unequal material-slot behavior
- decides whether the renderer layout is supported
- resolves the material-binding index of each source submesh
- constructs the immutable core inputs

Lines, points, quads, unsupported topologies, missing material associations, extra-material layering, and null material semantics are Unity-adapter concerns. None of them produces a core planner status.

A Blender or other-host adapter follows the same seam:

```text
Blender mesh / material assignments
        ↓
future Blender separation-input adapter
        ↓
the same MeshSeparationInput
        ↓
MeshSeparationPlanner
```

That adapter owns the polygon triangulation and the material-assignment rules of its host. This milestone implements neither adapter. It introduces no adapter interface, registry, or dependency-injection layer.

## Output plan model

```csharp
internal enum SubmeshSeparationDisposition
{
    Unchanged,
    WhollyOpaqueCandidate,
    Split
}

internal sealed class SubmeshSeparationPlan
{
    internal int SourceSubmeshIndex { get; }
    internal int SourceMaterialBindingIndex { get; }
    internal IReadOnlyList<int> OpaqueTriangleOrdinals { get; }
    internal IReadOnlyList<int> TransparentTriangleOrdinals { get; }
    internal SubmeshSeparationDisposition Disposition { get; }
}

internal sealed class MeshSeparationPlan
{
    internal MeshSeparationInput Source { get; }
    internal IReadOnlyList<SubmeshSeparationPlan> Submeshes { get; }
    internal bool HasAnyOpaqueCandidates { get; }
    internal bool RequiresAnySplit { get; }
    internal int OpaqueTriangleCount { get; }
    internal int TransparentTriangleCount { get; }
}

internal static class MeshSeparationPlanner
{
    internal static MeshSeparationPlan Create(MeshSeparationInput input);
}
```

The types and names are a proposed internal production API. TDD can refine a name if a compiling test shows a concrete usability problem. The refinement must keep the approved semantics and the approved boundary.

Every returned plan is valid. `Submeshes` contains exactly one record for every normalized source submesh, including empty submeshes. Malformed API input throws before a plan exists. There is no generic unsupported state because all host-specific support decisions happen before normalization. `HasAnyOpaqueCandidates == false` identifies a valid structural no-op.

## Per-submesh membership and disposition

The planner scans each triangle once in source ordinal order.

### No opaque candidates

For `T T U T`:

- opaque ordinals: empty
- transparent ordinals: `0, 1, 2, 3`
- disposition: `Unchanged`

`MustRemainTransparent` and `Unknown` keep their source order. Membership alone does not distinguish them. Their original outcomes stay in the immutable `Source` input if diagnostics genuinely need them. The plan does not copy or summarize those outcomes again.

### Wholly opaque candidate

For `O O O O`:

- opaque ordinals: `0, 1, 2, 3`
- transparent ordinals: empty
- disposition: `WhollyOpaqueCandidate`

A later transformer can omit the transparent output for this source submesh. The planner does not create and does not require an empty transparent submesh.

### Mixed

For `O O T U O`:

- opaque ordinals: `0, 1, 4`
- transparent ordinals: `2, 3`
- disposition: `Split`

The plan states that the same source material binding would need two logical outputs. It does not assign the final output-group numbers for the host and does not create an opaque material.

### Empty submesh

An empty triangle submesh with zero outcomes is valid. It produces an `Unchanged` record with two empty ordinal lists. The record keeps the exact source submesh/material-binding correspondence and the deterministic source ordering. If the planner omitted the record, later provenance would shift. If it rejected the submesh, a valid normalized empty group would become an unnecessary failure.

## Multiple submeshes and output identity

The planner plans each source submesh independently. Given:

```text
submesh 0: O O O T
submesh 1: T T U T
submesh 2: O O O O
```

the dispositions are `Split`, `Unchanged`, and `WhollyOpaqueCandidate` in that order. Each record carries both source indices, even though the supported mapping makes them equal.

This milestone defers the final transformed group/submesh numbering. A later transformer knows the source record order and the caller-supplied material binding for every candidate request. It also knows the exact source triangle ordinals in each logical side. These facts give it enough deterministic provenance to choose a mapping.

## Malformed and no-op behavior

Malformed normalized data violates the planner API contract and throws synchronously. Valid data with no opaque candidates produces a no-op plan. Host-specific unsupported source layouts never reach this layer.

### Malformed and throws

- null input, submesh collection, submesh element, index list, or outcome list
- negative vertex count
- negative source material-binding index
- any negative vertex index or index greater than or equal to `VertexCount`
- an index array whose length is not divisible by three
- an outcome count different from `Indices.Count / 3`
- an undefined `TriangleAlphaOutcome` value

The planner does not repair, truncate, pad, or convert these conditions to `Unknown`.

### Valid no-op

- every outcome is `MustRemainTransparent` or `Unknown`
- every source submesh is empty
- the mesh has zero vertices and zero submeshes
- duplicate vertex indices inside a triangle, repeated identical index triples, or duplicate non-negative material-binding indices
- these cases stay valid when the indices are in range and the classifier results cover every triangle occurrence

Repeated indices are topology, not proof of malformed data. The classifier outcome already expresses geometry degeneracy conservatively. The planner must not reclassify it.

## Immutability and plan lifetime

Both input constructors copy the caller's lists. They expose read-only wrappers, not raw arrays. The mesh input copies the submesh reference list. Each submesh input is itself sealed and immutable.

Each plan copies its ordinal lists and its plan-record list. It retains the immutable `MeshSeparationInput` as `Source`. If the caller changes the original index, outcome, or submesh arrays after construction, the input and plan do not change. If the caller changes the original lists after `Create`, the cached counts, dispositions, and opportunity flags stay valid.

The plan retains the upstream outcomes once, through `Source`. It does not duplicate them in result records. Transformation logic uses the membership lists and must not rerun classifier semantics. Diagnostics can read the source outcome by ordinal until a separate diagnostic model becomes justified.

## Determinism and topology preservation

Observable order is fixed:

1. submesh plan records follow ascending source submesh index
2. opaque triangle ordinals follow their order in the source submesh
3. retained-transparent ordinals follow their order in the source submesh

No dictionary or hash-set enumeration contributes to observable output. A single linear scan appends each ordinal to exactly one list.

Ordinal `n` always resolves to source entries `3n..3n+2`. The plan therefore preserves vertex indices, triangle winding, and relative triangle order within each logical side. It does not copy, remap, compact, or inspect vertex attributes. An identical index triple that appears twice stays two distinct triangle occurrences with two ordinals.

## Structural facts and profitability boundary

The plan can cache only facts from the same linear scan:

- opaque-candidate count
- retained-transparent count
- whether any opaque candidate exists
- whether any submesh requires a split
- per-submesh disposition

`HasAnyOpaqueCandidates == false` is the only structural no-op signal. The planner applies no threshold and no cost model. It does not decide whether a candidate is profitable.

## Future transformation and material boundary

Each opaque ordinal list is a request. It states that source material binding `N` may need an opaque counterpart for those triangle occurrences. It is not a command to mutate.

If a later material/shader stage cannot prove that binding `N` has a behavior-preserving opaque conversion, it can reject that request. The original source topology and outcomes stay available. The pipeline can then conservatively keep all triangles of that source submesh on the original transparent path. The rejection does not corrupt the immutable plan and does not force a rewrite of it. It also does not need to stop independent supported bindings from proceeding.

The planner does not know shader names, conversion support, material properties, render queues, blend state, keywords, or clone mechanics. The material/shader analysis and host-specific transformation milestones own those decisions. They also own the final output numbering, the actual index buffers, empty-output omission, material reconstruction, source-asset preservation, and transactional fallback.

## Wider optimizer composition

A later all-in-one optimizer can use this normalized boundary. This milestone does not become a framework:

```text
host adapters
      ↓
normalized analysis inputs
      ↓
alpha classifier
      ↓
separation planner
      ↓
other analyzers / shader support / animation analysis
      ↓
combined optimization plan
      ↓
host-specific transformation
```

The separation plan stays one immutable candidate-analysis result. A future combined optimization plan can accept, reject, or correlate its candidates with shader/material and animation facts. This milestone introduces no optimizer interface, registry, orchestration graph, or serialization.

## Complexity

Construction copies each supplied index and outcome once. Planning scans each triangle once and appends each ordinal once:

- time: `O(number of indices + number of triangles)`
- space: `O(number of indices + number of outcomes + number of triangles + number of submeshes)` for immutable snapshots and plan membership

There are no geometry algorithms, spatial structures, arbitrary-precision numbers, caches, jobs, or parallel execution.

## Test strategy

Ordinary direct NUnit EditMode tests are clearer than another machine-readable fixture catalog. The planner has a small structural state space, no cross-language numeric semantics, and no asset construction. Test inputs should be literal index arrays and outcome arrays, with hand-authored expected ordinal lists.

TDD proceeds in narrow red/green slices:

1. immutable input/result boundary, malformed validation, and a valid empty/no-op plan
2. one-submesh unchanged cases (`TTTT`, `UUUU`, `TUTU`)
3. wholly opaque (`OOOO`) and mixed (`OOTT`, `OUOT`) membership
4. `Unknown` conservative behavior and monotonic uncertainty tests
5. multiple submeshes with explicit, reordered, and duplicate material-binding provenance
6. empty-submesh provenance, occurrence identity, winding, and deterministic source order
7. mutation isolation, duplicate-index legality, adversarial ordering, and complete validation

Every production behavior starts with a focused test that fails for the absent or incorrect behavior. Tests exercise real production types without mocks or Unity assets.

## Adversarial self-review

These cases challenged the design:

- all `Unknown` produces a valid unchanged plan with no opaque ordinals
- alternating `O/U` moves only the `O` ordinals and keeps both lists in source order
- one opaque triangle among thousands is a split, without a profitability threshold
- all opaque produces no forced transparent output
- zero triangles is a valid no-op
- an empty submesh among populated submeshes stays in its exact source position
- outcome arrays one shorter or longer throw before planning
- invalid or incomplete triangle indices throw, but legal repeated indices do not
- replacing `O` with `U` or `T` can only remove opaque ordinals
- grouping never sorts and never changes source order
- caller mutation cannot change plan facts
- source submesh 0 can bind to material binding 4 while source submesh 1 binds to 2
- duplicate non-negative material-binding indices stay valid and unchanged
- the planner never infers a binding from a submesh index or a host material count
- a later material rejection abandons that candidate and preserves the original transparent path

No path puts `Unknown` into opaque membership. The only opaque branch is an explicit equality check for `TriangleAlphaOutcome.ProvenOpaque`. The two other defined values share the retained-transparent branch. Undefined values throw during input construction.

## Known risks

- The planner trusts the host adapters to supply triangle-only normalized groups and meaningful non-negative material-binding identifiers. Adapter contract defects stay possible. Host-adapter tests become necessary when those adapters exist.
- A plain integer binding identifier carries provenance with no host semantics. This is intentional. Richer material identity or draw-call models should come only when a real adapter cannot express its required mapping.
- Retaining the outcomes through `MeshSeparationPlan.Source` exposes more upstream context than transformation needs. But it avoids a duplicate copy of the topology and keeps diagnostics available without a second model. Downstream transformation must use plan membership and must not reclassify.
- `IReadOnlyList<T>` alone does not imply immutable storage. Production constructors must clone inputs and return read-only wrappers. Mutation tests must enforce this.

## Explicitly deferred work

- Unity, Blender, and other host adapters, including topology/material-layout support and normalized input construction
- final host-specific transformed group/submesh numbering
- material object identity, null-material behavior, shader support, and opaque counterpart generation
- partial acceptance/rejection orchestration across material bindings
- Unity renderer layouts with unequal material/submesh counts or layered extra-material behavior
- mesh/index mutation, vertex duplication/remapping/compaction, and generated assets
- diagnostics beyond status, offending topology submesh, provenance, and disposition
- real profitability policy and performance acceleration
- NDMF, avatar, animation, material-swap, third-party, private-testbed, and CI integration

## Verified design-phase baseline

- Git: the classifier PR #4 is merged into `main` at `7c9c20b`. The branch `feat/separation-plan` was created from that updated commit with no pre-existing changes.
- Public Unity project: `<repo-root>`
- Connected instances: exactly one, the public project. No private testbed instance was selected or accessed.
- Unity: `2022.3.22f1`
- Embedded package: `com.alrauna.alpha-material-optimizer@0.0.1`
- Test discovery: 66 EditMode tests (67 tests across all modes). The `TriangleAlphaClassifierTests` cases are present in EditMode discovery.
- Editor: idle and not compiling when observed
- Console: zero current error entries and zero current warning entries
- Recent test result: the MCP last-run snapshot reported no usable counts or result. This design phase therefore does not claim a fresh passing run. The tests were not rerun because MCP use had to stay read-only and only documentation changes were planned.
- Private testbed: not modified
