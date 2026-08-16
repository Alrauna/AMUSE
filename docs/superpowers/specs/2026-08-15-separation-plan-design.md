# Mesh Separation Plan Design

**Date:** 2026-08-15

**Status:** Awaiting approval

## Problem statement

The merged triangle classifier decides whether each source triangle is `ProvenOpaque`, `MustRemainTransparent`, or `Unknown`, but no pure layer yet turns those results into a deterministic description of a future mesh split. The next milestone needs that description without mutating a Unity `Mesh`, creating materials, touching a renderer, or coupling planning to NDMF.

The planner must preserve source topology and already-resolved material-binding provenance while enforcing one safety rule:

> Only `ProvenOpaque` triangles may become eligible to leave their original transparent rendering path.

`MustRemainTransparent` and `Unknown` always remain on the retained-transparent side. A plan is a candidate transformation request, not proof that a later material stage can produce an opaque counterpart.

## Goals

- Consume immutable submesh index topology and one classifier outcome per source triangle.
- Produce an immutable, deterministic per-submesh partition expressed by source triangle ordinals.
- Preserve the source submesh index and caller-supplied material-binding index explicitly.
- Distinguish unchanged, wholly opaque-candidate, and mixed/split submeshes.
- Distinguish a valid structural no-op from malformed normalized input.
- Validate enough topology to prevent a later transformer from reading incomplete or out-of-range triangle indices.
- Keep the planner `O(number of indices + number of triangles)` with small copied arrays.
- Leave host topology interpretation, material-layout support, final output numbering, and all mutation decisions to host adapters and later milestones.

## Non-goals

This milestone does not design or implement:

- Unity `Mesh` or renderer mutation;
- new meshes, vertex duplication, vertex remapping, index-buffer rewriting, or compaction;
- material creation, shader conversion, material-property rewriting, or material support analysis;
- NDMF passes, avatar traversal, animation/material-swap tracing, or third-party integration;
- mipmap or texture/shader analysis;
- draw-call, triangle-count, GPU-cost, memory, or avatar profitability heuristics;
- jobs, Burst, GPU work, caching frameworks, or parallelism;
- new fixture catalogs, committed Unity mesh assets, CI, release, or private-testbed behavior.

The planner depends on neither MCP nor NDMF and never reads a live `Mesh` or `Renderer`.

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

An undefined enum value is malformed input and throws. It never defaults to either membership. The plan does not merge `Unknown` with `MustRemainTransparent` semantically; it merely gives both the same conservative transformation membership.

## Considered approaches

### 1. Immutable topology snapshot plus source triangle ordinals — selected

Copy each submesh's raw index array and outcome array into an immutable input. The plan retains that input and stores two ordered lists of zero-based triangle ordinals per submesh. Ordinal `n` refers to source index entries `3n`, `3n + 1`, and `3n + 2`.

This avoids copying every index triple into two result collections, preserves winding and source order, and makes an ordinal unambiguous because the plan owns a reference to the immutable topology snapshot against which it was created.

### 2. Copy full index triples into the plan

This makes each result independently readable but duplicates topology, obscures whether identical triples are distinct source occurrences, and increases the chance that future code treats a copied triple as identity. It is unnecessary while the plan retains an immutable input.

### 3. Store only ordinals and require the caller to retain the original arrays

This is the fewest fields, but mutable caller arrays could make a previously valid plan stale. Adding a topology fingerprint or version token would be more code than copying the arrays once. This option is rejected.

## Input model

The production boundary remains internal and Editor-only:

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

`MeshSeparationInput` deliberately contains only `VertexCount`, not positions, normals, tangents, colors, UVs, bone weights, blend shapes, or other vertex attributes. The planner moves triangle references conceptually and never reads or changes vertex data. `VertexCount` exists only to reject negative or out-of-range indices.

Each input submesh is an already-normalized triangle group. It carries a non-negative source material-binding index, its raw triangle index sequence, and outcomes together. The input contract itself states that every consecutive three indices are one triangle; there is no topology enum. Outcome ordinal `n` corresponds exactly to index triple `n`.

There are no caller-supplied global index-buffer offsets or submesh boundary ranges. Invalid or overlapping boundary arithmetic is therefore unrepresentable in this API; each copied submesh array is its own complete boundary. Tests instead validate per-submesh result counts and that triangle ordinals restart at zero for each source submesh.

`SourceMaterialBindingIndex` is opaque provenance to the planner. It identifies the source draw/material binding that a later stage must consult; it does not imply a Unity material-slot array or equality with the source submesh index. Negative binding indices are malformed. Duplicate non-negative binding indices are valid because multiple geometry groups may legitimately share one material binding. The planner preserves, but never invents, validates against a host material count, deduplicates, sorts, or otherwise interprets these identifiers.

`SourceSubmeshIndex` remains the output name for the normalized group ordinal. “Submesh” is the clearest term for the repository's mesh-oriented pipeline and is readily supplied by Unity or Blender adapters; renaming the entire model to generic groups would add abstraction without changing behavior. `SourceMaterialBindingIndex` is deliberately more neutral than “slot” because its meaning is host-resolved.

No Unity namespace, `MeshTopology`, `Mesh`, `Renderer`, `Material`, asset, or GameObject reference enters the planner boundary.

## Future host-adapter boundary

Host adapters normalize source-specific geometry and draw/material semantics before the core planner is called:

```text
Unity Mesh / Renderer / Material[]
        ↓
future Unity separation-input adapter
        ↓
generic MeshSeparationInput
        ↓
MeshSeparationPlanner
```

The future Unity adapter owns reading `Mesh.subMeshCount`, reading each submesh topology and index buffer, reading renderer material slots, interpreting unequal material-slot behavior, deciding whether the renderer layout is supported, resolving each source submesh's material-binding index, and constructing immutable core inputs. Lines, points, quads, unsupported topologies, missing material associations, extra-material layering, and null material semantics are Unity-adapter concerns. None produces a core planner status.

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

That adapter owns its host's polygon triangulation and material-assignment rules. Neither adapter is implemented in this milestone, and no adapter interface, registry, or dependency-injection layer is introduced.

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

The types and names are proposed internal production API. TDD may refine a name if a compiling test demonstrates a concrete usability problem, but it must preserve the approved semantics and boundary.

Every returned plan is valid. `Submeshes` contains exactly one record for every normalized source submesh, including empty submeshes. Malformed API input throws before a plan exists. There is no generic unsupported state because all host-specific support decisions happen before normalization. `HasAnyOpaqueCandidates == false` identifies a valid structural no-op.

## Per-submesh membership and disposition

The planner scans each triangle once in source ordinal order.

### No opaque candidates

For `T T U T`:

- opaque ordinals: empty;
- transparent ordinals: `0, 1, 2, 3`;
- disposition: `Unchanged`.

`MustRemainTransparent` and `Unknown` retain their source order and are not distinguishable from membership alone. Their original outcomes remain in the immutable `Source` input if diagnostics genuinely need them; the plan does not copy or summarize those outcomes again.

### Wholly opaque candidate

For `O O O O`:

- opaque ordinals: `0, 1, 2, 3`;
- transparent ordinals: empty;
- disposition: `WhollyOpaqueCandidate`.

A later transformer may omit a transparent output for this source submesh. The planner does not create or require an empty transparent submesh.

### Mixed

For `O O T U O`:

- opaque ordinals: `0, 1, 4`;
- transparent ordinals: `2, 3`;
- disposition: `Split`.

The plan states that two logical outputs would be required for the same source material binding. It does not assign final host output-group numbers or create an opaque material.

### Empty submesh

An empty triangle submesh with zero outcomes is valid and produces an `Unchanged` record with two empty ordinal lists. Keeping the record preserves exact source submesh/material-binding correspondence and deterministic source ordering. Omitting it would shift later provenance; rejecting it would turn a valid normalized empty group into an unnecessary failure.

## Multiple submeshes and output identity

Every source submesh is planned independently. Given:

```text
submesh 0: O O O T
submesh 1: T T U T
submesh 2: O O O O
```

the dispositions are `Split`, `Unchanged`, and `WhollyOpaqueCandidate` in that order. Each record carries both source indices, even though both are equal under the supported mapping.

Final transformed group/submesh numbering is deferred. A later transformer has enough deterministic provenance to choose a mapping because it knows the source record order, the caller-supplied material binding for every candidate request, and the exact source triangle ordinals in each logical side.

## Malformed and no-op behavior

Malformed normalized data violates the planner API contract and throws synchronously. Valid data without opaque candidates produces a no-op plan. Host-specific unsupported source layouts never reach this layer.

### Malformed and throws

- null input, submesh collection, submesh element, index list, or outcome list;
- negative vertex count;
- negative source material-binding index;
- any negative vertex index or index greater than or equal to `VertexCount`;
- an index array whose length is not divisible by three;
- an outcome count different from `Indices.Count / 3`;
- an undefined `TriangleAlphaOutcome` value.

These conditions are not repaired, truncated, padded, or converted to `Unknown`.

### Valid no-op

- every outcome is `MustRemainTransparent` or `Unknown`;
- every source submesh is empty;
- the mesh has zero vertices and zero submeshes;
- duplicate vertex indices inside a triangle, repeated identical index triples, or duplicate non-negative material-binding indices, provided indices are in range and classifier results cover every triangle occurrence.

Repeated indices are topology, not proof of malformed data. Geometry degeneracy has already been expressed conservatively by the classifier outcome; the planner must not reclassify it.

## Immutability and plan lifetime

Both input constructors copy caller-provided lists. They expose read-only wrappers rather than raw arrays. The mesh input copies the submesh reference list; each submesh input is itself sealed and immutable.

Each plan copies its ordinal lists and plan-record list and retains the immutable `MeshSeparationInput` as `Source`. Mutating the caller's original index, outcome, or submesh arrays after construction cannot change the input or plan. Mutating the caller's original lists after `Create` likewise cannot invalidate cached counts, dispositions, or opportunity flags.

The plan retains upstream outcomes only once through `Source`; it does not duplicate them in result records. Transformation logic uses membership lists and must not rerun classifier semantics. Diagnostics may consult the source outcome by ordinal until a separate diagnostic model is justified.

## Determinism and topology preservation

Observable order is fixed:

1. submesh plan records follow ascending source submesh index;
2. opaque triangle ordinals follow their order in the source submesh;
3. retained-transparent ordinals follow their order in the source submesh.

No dictionary or hash-set enumeration contributes to observable output. A single linear scan appends each ordinal to exactly one list.

Because ordinal `n` always resolves to source entries `3n..3n+2`, the plan preserves vertex indices, triangle winding, and relative triangle order within each logical side. It does not copy, remap, compact, or inspect vertex attributes. An identical index triple appearing twice remains two distinct triangle occurrences with two ordinals.

## Structural facts and profitability boundary

The plan may cache only facts derived during the same linear scan:

- opaque-candidate count;
- retained-transparent count;
- whether any opaque candidate exists;
- whether any submesh requires a split;
- per-submesh disposition.

`HasAnyOpaqueCandidates == false` is the only structural no-op signal. The planner applies no threshold or cost model and does not decide whether a candidate is profitable.

## Future transformation and material boundary

Each opaque ordinal list is a request that source material binding `N` may need an opaque counterpart for those triangle occurrences. It is not a command to mutate.

If a later material/shader stage cannot prove that binding `N` has a behavior-preserving opaque conversion, it may reject that request. The original source topology and outcomes remain available, so the pipeline can conservatively retain all triangles from that source submesh on the original transparent path. Rejecting one binding does not corrupt or require rewriting the immutable plan and need not prevent independent supported bindings from proceeding.

The planner does not know shader names, conversion support, material properties, render queues, blend state, keywords, or clone mechanics. The material/shader analysis and host-specific transformation milestones own those decisions, final output numbering, actual index buffers, empty-output omission, material reconstruction, source-asset preservation, and transactional fallback.

## Wider optimizer composition

The normalized boundary can compose into a later all-in-one optimizer without making this milestone a framework:

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

The separation plan remains one immutable candidate-analysis result. A future combined optimization plan may accept, reject, or correlate its candidates with shader/material and animation facts. No optimizer interface, registry, orchestration graph, or serialization is introduced now.

## Complexity

Construction copies each supplied index and outcome once. Planning scans each triangle once and appends one ordinal once:

- time: `O(number of indices + number of triangles)`;
- space: `O(number of indices + number of outcomes + number of triangles + number of submeshes)` for immutable snapshots and plan membership.

There are no geometry algorithms, spatial structures, arbitrary-precision numbers, caches, jobs, or parallel execution.

## Test strategy

Ordinary direct NUnit EditMode tests are clearer than another machine-readable fixture catalog. The planner has a small structural state space, no cross-language numeric semantics, and no asset construction. Test inputs should be literal index arrays and outcome arrays, with hand-authored expected ordinal lists.

TDD proceeds in narrow red/green slices:

1. immutable input/result boundary, malformed validation, and a valid empty/no-op plan;
2. one-submesh unchanged cases (`TTTT`, `UUUU`, `TUTU`);
3. wholly opaque (`OOOO`) and mixed (`OOTT`, `OUOT`) membership;
4. `Unknown` conservative behavior and monotonic uncertainty tests;
5. multiple submeshes with explicit, reordered, and duplicate material-binding provenance;
6. empty-submesh provenance, occurrence identity, winding, and deterministic source order;
7. mutation isolation, duplicate-index legality, adversarial ordering, and complete validation.

Every production behavior begins with a focused test that fails for the absent or incorrect behavior. Tests exercise real production types without mocks or Unity assets.

## Adversarial self-review

The design was challenged against these cases:

- all `Unknown` produces a valid unchanged plan with no opaque ordinals;
- alternating `O/U` moves only the `O` ordinals and keeps both lists in source order;
- one opaque triangle among thousands is a split, without a profitability threshold;
- all opaque produces no forced transparent output;
- zero triangles is a valid no-op;
- an empty submesh among populated submeshes remains in its exact source position;
- outcome arrays one shorter or longer throw before planning;
- invalid or incomplete triangle indices throw; legal repeated indices do not;
- replacing `O` with `U` or `T` can only remove opaque ordinals;
- grouping never sorts or changes source order;
- caller mutation cannot change plan facts;
- source submesh 0 may bind to material binding 4 while source submesh 1 binds to 2;
- duplicate non-negative material-binding indices remain valid and unchanged;
- the planner never infers a binding from a submesh index or host material count;
- a later material rejection abandons that candidate and preserves the original transparent path.

There is no path where `Unknown` enters opaque membership: the only opaque branch is an explicit equality check for `TriangleAlphaOutcome.ProvenOpaque`; the two other defined values share the retained-transparent branch; undefined values throw during input construction.

## Known risks

- The planner trusts host adapters to provide triangle-only normalized groups and meaningful non-negative material-binding identifiers. Adapter contract defects remain possible and require host-adapter tests when those adapters exist.
- A plain integer binding identifier carries provenance without host semantics. This is intentional; richer material identity or draw-call models should be added only when a real adapter cannot express its required mapping.
- Retaining outcomes through `MeshSeparationPlan.Source` exposes more upstream context than transformation needs, but avoids duplicating topology and keeps diagnostics available without a second model. Downstream transformation must use plan membership rather than reclassify.
- `IReadOnlyList<T>` alone does not imply immutable storage; production constructors must clone inputs and return read-only wrappers, and mutation tests must enforce this.

## Explicitly deferred work

- Unity, Blender, and other host adapters, including topology/material-layout support and normalized input construction;
- final host-specific transformed group/submesh numbering;
- material object identity, null-material behavior, shader support, and opaque counterpart generation;
- partial acceptance/rejection orchestration across material bindings;
- Unity renderer layouts with unequal material/submesh counts or layered extra-material behavior;
- mesh/index mutation, vertex duplication/remapping/compaction, and generated assets;
- diagnostics beyond status, offending topology submesh, provenance, and disposition;
- real profitability policy and performance acceleration;
- NDMF, avatar, animation, material-swap, third-party, private-testbed, and CI integration.

## Verified design-phase baseline

- Git: classifier PR #4 is merged into `main` at `7c9c20b`; `feat/separation-plan` was created from that updated commit with no pre-existing changes.
- Public Unity project: `E:/AI/Git/alpha-material-optimizer-ndmf`.
- Connected instances: exactly one, the public project; no private testbed instance was selected or accessed.
- Unity: `2022.3.22f1`.
- Embedded package: `com.alrauna.alpha-material-optimizer@0.0.1`.
- Test discovery: 66 EditMode tests (67 tests across all modes); `TriangleAlphaClassifierTests` cases are present in EditMode discovery.
- Editor: idle and not compiling when observed.
- Console: zero current error entries and zero current warning entries.
- Recent test result: the MCP last-run snapshot reported no usable counts/result, so this design phase does not claim a fresh passing run. Tests were not rerun because MCP use was required to remain read-only and only documentation changes are planned.
- Private testbed: not modified.
