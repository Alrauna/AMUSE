# Mesh Separation Plan Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` for inline implementation. Use `superpowers:subagent-driven-development` only if the user separately authorizes subagents. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a pure, immutable Editor-only planner that partitions normalized triangle groups into opaque-candidate or retained-transparent membership while preserving host-resolved source material-binding provenance.

**Architecture:** Host adapters are outside this milestone and will normalize Unity, Blender, or other source topology/material layouts into immutable triangle-only inputs. The core planner copies those inputs, validates universal triangle structure, scans outcomes once, and records source-local triangle ordinals plus the caller-supplied material-binding index. Every returned plan is valid; malformed normalized input throws, and no generic unsupported status exists.

**Tech Stack:** Unity 2022.3.22f1 Editor compilation, C#, `System.Collections.Generic`, `System.Collections.ObjectModel`, NUnit EditMode tests, and the merged internal `TriangleAlphaOutcome` contract. The planner production file uses no Unity types, NDMF APIs, assets, or host adapter APIs.

## Global Constraints

- Do not start implementation until the user explicitly approves `docs/superpowers/specs/2026-08-15-separation-plan-design.md` and this plan.
- Work on `feat/separation-plan`; recheck branch, status, merge base, and unrelated user changes before editing.
- Only `TriangleAlphaOutcome.ProvenOpaque` may enter opaque membership.
- `MustRemainTransparent` and `Unknown` remain retained-transparent; undefined outcome values throw.
- Treat every `SubmeshSeparationInput.Indices` array as already-normalized triangle triples. Do not add a topology enum.
- Preserve source vertex indices, triangle winding, repeated triangle occurrence identity, and source order through zero-based per-submesh triangle ordinals.
- Receive `SourceMaterialBindingIndex` explicitly, require it to be non-negative, preserve it exactly, and allow duplicate binding indices across submeshes.
- Never infer a material binding from the submesh index or validate it against a host material count.
- Copy caller-provided index, outcome, submesh, and plan lists; never expose a mutable backing array.
- Treat zero submeshes, empty submeshes, and inputs with no `ProvenOpaque` result as valid no-ops.
- Throw for malformed/null data, negative vertex/binding indices, incomplete triples, invalid vertex indices, result-count mismatch, or undefined classifier outcomes. Do not repair inputs.
- Keep production types internal in the existing Editor analysis namespace and reuse the existing `InternalsVisibleTo` boundary.
- Do not modify classifier behavior, fixture catalogs, asmdefs, package manifests/locks, dependencies, workflows, scenes, or private testbed content.
- Do not add Unity/Blender adapters, adapter interfaces, dependency injection, registries, serialization, public API, transformer abstractions, profitability policy, shader logic, cache, jobs/Burst/GPU code, or NDMF integration.
- Preserve Unity `.meta` file pairing for the two future C# assets and inspect GUID/scope changes before completion.
- Do not commit or push unless the user separately authorizes it.

---

## File map

**Create during approved implementation:**

- `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/MeshSeparationPlanner.cs` — normalized immutable inputs, immutable plan outputs, universal validation, and the linear planner.
- `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/MeshSeparationPlanner.cs.meta` — Unity identity paired with the production file.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/MeshSeparationPlannerTests.cs` — direct NUnit examples, explicit binding provenance, validation, ordering, mutation isolation, and metamorphic safety tests.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/MeshSeparationPlannerTests.cs.meta` — Unity identity paired with the test file.

**Read but do not modify:**

- `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/TriangleAlphaClassifier.cs`
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`
- `Packages/com.alrauna.alpha-material-optimizer/Editor/AssemblyInfo.cs`
- both package Editor/test asmdefs;
- both reference-fixture JSON catalogs and their support/tests;
- package metadata, project manifests/locks, NDMF/bootstrap files, workflows, and private testbed content.

Keeping all planner production types in one file and all planner tests in one file is the smallest coherent layout. Host adapters remain deferred rather than receiving speculative files or interfaces.

## Shared production interfaces

Keep these names and signatures consistent across tasks unless an approved failing test demonstrates a concrete correction:

```csharp
using System.Collections.Generic;

namespace Alrauna.AlphaMaterialOptimizer.Editor.Analysis
{
    internal enum SubmeshSeparationDisposition
    {
        Unchanged,
        WhollyOpaqueCandidate,
        Split
    }

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
}
```

Plan output constructors/factories remain private so callers cannot construct contradictory cached facts. Copy a caller list with this pattern rather than retaining or returning its array:

```csharp
private static IReadOnlyList<T> CopyReadOnly<T>(IReadOnlyList<T> source)
{
    var copy = new T[source.Count];
    for (var index = 0; index < copy.Length; index++)
        copy[index] = source[index];
    return System.Array.AsReadOnly(copy);
}
```

Keep the helper private in the production file. Do not create a general utility class.

## Shared test file and helpers

Create direct deterministic inputs in `MeshSeparationPlannerTests.cs`; do not read fixture JSON or create Unity assets:

```csharp
using System.Collections.Generic;
using System.Linq;
using Alrauna.AlphaMaterialOptimizer.Editor.Analysis;
using NUnit.Framework;

namespace Alrauna.AlphaMaterialOptimizer.Tests.Editor.Analysis
{
    public sealed class MeshSeparationPlannerTests
    {
        private static MeshSeparationInput OneSubmesh(
            params TriangleAlphaOutcome[] outcomes)
        {
            return OneSubmeshWithBinding(0, outcomes);
        }

        private static MeshSeparationInput OneSubmeshWithBinding(
            int sourceMaterialBindingIndex,
            params TriangleAlphaOutcome[] outcomes)
        {
            var indices = new int[outcomes.Length * 3];
            for (var index = 0; index < indices.Length; index++)
                indices[index] = index;

            return new MeshSeparationInput(
                indices.Length,
                new[]
                {
                    new SubmeshSeparationInput(
                        sourceMaterialBindingIndex,
                        indices,
                        outcomes)
                });
        }

        private static void AssertOrdinals(
            IReadOnlyList<int> actual,
            params int[] expected)
        {
            CollectionAssert.AreEqual(expected, actual);
        }
    }
}
```

Expected dispositions, bindings, and ordinal lists remain literal in assertions. No test helper may reproduce planner membership logic.

---

### Task 1: Immutable normalized boundary, validation, and empty no-op

**Files:**

- Create: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/MeshSeparationPlannerTests.cs`
- Create after red: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/MeshSeparationPlanner.cs`
- Add the matching `.meta` files when Unity imports each source file.

**Interfaces:** Produces every shared type/signature, defensive input copies, universal malformed-input validation, immutable empty plan factories, and a compiling planner that handles only the zero-submesh no-op.

- [ ] **Step 1: Reinspect the approved branch and contracts**

Run:

```powershell
git branch --show-current
git status --short --branch
git merge-base main HEAD
git diff --name-status main...HEAD
```

Expected: branch `feat/separation-plan`, merge base `7c9c20b` unless `main` has advanced through a separately verified merge, and only the approved design/plan documentation changes. Stop if unrelated work appears.

- [ ] **Step 2: Add the empty and copy tests before production types**

Add the shared helpers and:

```csharp
[Test]
public void EmptyMeshIsAValidNoOp()
{
    var input = new MeshSeparationInput(
        0,
        System.Array.Empty<SubmeshSeparationInput>());

    var plan = MeshSeparationPlanner.Create(input);

    Assert.That(plan.Submeshes, Is.Empty);
    Assert.That(plan.HasAnyOpaqueCandidates, Is.False);
    Assert.That(plan.RequiresAnySplit, Is.False);
    Assert.That(plan.OpaqueTriangleCount, Is.Zero);
    Assert.That(plan.TransparentTriangleCount, Is.Zero);
}

[Test]
public void InputCopiesCallerCollections()
{
    var indices = new[] { 0, 1, 2 };
    var outcomes = new[] { TriangleAlphaOutcome.Unknown };
    var submeshes = new[]
    {
        new SubmeshSeparationInput(4, indices, outcomes)
    };
    var input = new MeshSeparationInput(3, submeshes);

    indices[0] = 2;
    outcomes[0] = TriangleAlphaOutcome.ProvenOpaque;
    submeshes[0] = new SubmeshSeparationInput(
        9,
        System.Array.Empty<int>(),
        System.Array.Empty<TriangleAlphaOutcome>());

    Assert.That(input.Submeshes[0].SourceMaterialBindingIndex, Is.EqualTo(4));
    Assert.That(input.Submeshes[0].Indices[0], Is.Zero);
    Assert.That(input.Submeshes[0].Outcomes[0],
        Is.EqualTo(TriangleAlphaOutcome.Unknown));
}
```

- [ ] **Step 3: Add malformed normalized-input tests before production types**

```csharp
[Test]
public void NullAndNegativeMeshInputsThrow()
{
    Assert.Throws<System.ArgumentNullException>(() =>
        MeshSeparationPlanner.Create(null));
    Assert.Throws<System.ArgumentOutOfRangeException>(() =>
        new MeshSeparationInput(
            -1,
            System.Array.Empty<SubmeshSeparationInput>()));
    Assert.Throws<System.ArgumentNullException>(() =>
        new MeshSeparationInput(0, null));
    Assert.Throws<System.ArgumentNullException>(() =>
        new MeshSeparationInput(
            0,
            new SubmeshSeparationInput[] { null }));
}

[Test]
public void MalformedSubmeshInputsThrow()
{
    Assert.Throws<System.ArgumentOutOfRangeException>(() =>
        new SubmeshSeparationInput(
            -1,
            System.Array.Empty<int>(),
            System.Array.Empty<TriangleAlphaOutcome>()));
    Assert.Throws<System.ArgumentNullException>(() =>
        new SubmeshSeparationInput(
            0,
            null,
            System.Array.Empty<TriangleAlphaOutcome>()));
    Assert.Throws<System.ArgumentNullException>(() =>
        new SubmeshSeparationInput(
            0,
            System.Array.Empty<int>(),
            null));
    Assert.Throws<System.ArgumentException>(() =>
        new SubmeshSeparationInput(
            0,
            new[] { 0, 1 },
            System.Array.Empty<TriangleAlphaOutcome>()));
    Assert.Throws<System.ArgumentException>(() =>
        new SubmeshSeparationInput(
            0,
            new[] { 0, 1, 2 },
            System.Array.Empty<TriangleAlphaOutcome>()));
    Assert.Throws<System.ArgumentException>(() =>
        new SubmeshSeparationInput(
            0,
            new[] { 0, 1, 2 },
            new[]
            {
                TriangleAlphaOutcome.Unknown,
                TriangleAlphaOutcome.Unknown
            }));
    Assert.Throws<System.ArgumentOutOfRangeException>(() =>
        new SubmeshSeparationInput(
            0,
            new[] { 0, 1, 2 },
            new[] { (TriangleAlphaOutcome)999 }));
}

[TestCase(-1)]
[TestCase(3)]
public void OutOfRangeVertexIndicesThrow(int invalidIndex)
{
    var submesh = new SubmeshSeparationInput(
        0,
        new[] { 0, 1, invalidIndex },
        new[] { TriangleAlphaOutcome.Unknown });

    Assert.Throws<System.ArgumentOutOfRangeException>(() =>
        new MeshSeparationInput(3, new[] { submesh }));
}
```

- [ ] **Step 4: Observe the single permitted compile-red state**

Wait for Unity compilation and read error-level Console entries. Expected: compile errors name the absent separation-plan types. Record this red state; do not modify classifier or asmdef files to resolve it.

- [ ] **Step 5: Add the minimal immutable boundary and validation**

Create `MeshSeparationPlanner.cs`. `SubmeshSeparationInput` validates and copies in this order:

```csharp
if (sourceMaterialBindingIndex < 0)
    throw new System.ArgumentOutOfRangeException(nameof(sourceMaterialBindingIndex));
if (indices == null)
    throw new System.ArgumentNullException(nameof(indices));
if (outcomes == null)
    throw new System.ArgumentNullException(nameof(outcomes));
if (indices.Count % 3 != 0)
    throw new System.ArgumentException(
        "Triangle groups require complete index triples.",
        nameof(indices));
if (outcomes.Count != indices.Count / 3)
    throw new System.ArgumentException(
        "Outcome count must equal triangle count.",
        nameof(outcomes));

for (var index = 0; index < outcomes.Count; index++)
{
    if (!System.Enum.IsDefined(typeof(TriangleAlphaOutcome), outcomes[index]))
        throw new System.ArgumentOutOfRangeException(nameof(outcomes));
}
```

Set `TriangleCount` to `Indices.Count / 3` after copying. `MeshSeparationInput` validates non-negative `VertexCount`, non-null submeshes/elements, copies the list, and validates every copied vertex index:

```csharp
if (vertexIndex < 0 || vertexIndex >= vertexCount)
    throw new System.ArgumentOutOfRangeException(
        nameof(submeshes),
        "Every source index must reference an existing vertex.");
```

Create private plan constructors/factories that copy result lists. `MeshSeparationPlanner.Create(null)` throws. For zero submeshes, return a plan with the exact facts asserted by `EmptyMeshIsAValidNoOp`. For non-empty valid inputs, return a compiling shell with no submesh records so Task 2 still has an executable red.

- [ ] **Step 6: Run Task 1 focused tests green**

Use Unity MCP `run_tests` in EditMode with these test names and poll `get_test_job(wait_timeout: 60, include_failed_tests: true)`:

```text
Alrauna.AlphaMaterialOptimizer.Tests.Editor.Analysis.MeshSeparationPlannerTests.EmptyMeshIsAValidNoOp
Alrauna.AlphaMaterialOptimizer.Tests.Editor.Analysis.MeshSeparationPlannerTests.InputCopiesCallerCollections
Alrauna.AlphaMaterialOptimizer.Tests.Editor.Analysis.MeshSeparationPlannerTests.NullAndNegativeMeshInputsThrow
Alrauna.AlphaMaterialOptimizer.Tests.Editor.Analysis.MeshSeparationPlannerTests.MalformedSubmeshInputsThrow
Alrauna.AlphaMaterialOptimizer.Tests.Editor.Analysis.MeshSeparationPlannerTests.OutOfRangeVertexIndicesThrow
```

Expected: all focused cases pass, compilation is clean, and the existing classifier remains unchanged.

- [ ] **Step 7: Review Task 1 scope**

Run `git diff --check`, `git diff --stat`, and `git status --short`. Expected new implementation scope is only the two C# files and their `.meta` pairs, in addition to the approved docs.

---

### Task 2: Unchanged and empty-submesh behavior

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/MeshSeparationPlannerTests.cs`
- Modify after red: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/MeshSeparationPlanner.cs`

**Interfaces:** Produces one plan record per normalized source submesh, retains all non-opaque outcomes in source order, preserves an explicit binding, and represents empty submeshes unchanged.

- [ ] **Step 1: Add unchanged behavior tests**

```csharp
[Test]
public void MustRemainTransparentTrianglesKeepSubmeshUnchanged()
{
    var plan = MeshSeparationPlanner.Create(OneSubmesh(
        TriangleAlphaOutcome.MustRemainTransparent,
        TriangleAlphaOutcome.MustRemainTransparent,
        TriangleAlphaOutcome.MustRemainTransparent,
        TriangleAlphaOutcome.MustRemainTransparent));

    var submesh = plan.Submeshes[0];
    Assert.That(submesh.Disposition,
        Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
    AssertOrdinals(submesh.OpaqueTriangleOrdinals);
    AssertOrdinals(submesh.TransparentTriangleOrdinals, 0, 1, 2, 3);
}

[Test]
public void UnknownTrianglesKeepSubmeshUnchanged()
{
    var plan = MeshSeparationPlanner.Create(OneSubmesh(
        TriangleAlphaOutcome.Unknown,
        TriangleAlphaOutcome.Unknown,
        TriangleAlphaOutcome.Unknown,
        TriangleAlphaOutcome.Unknown));

    Assert.That(plan.HasAnyOpaqueCandidates, Is.False);
    AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals);
    AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 0, 1, 2, 3);
}

[Test]
public void MixedTransparentAndUnknownOutcomesStayInSourceOrder()
{
    var plan = MeshSeparationPlanner.Create(OneSubmesh(
        TriangleAlphaOutcome.MustRemainTransparent,
        TriangleAlphaOutcome.Unknown,
        TriangleAlphaOutcome.MustRemainTransparent,
        TriangleAlphaOutcome.Unknown));

    Assert.That(plan.Submeshes[0].Disposition,
        Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
    AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 0, 1, 2, 3);
}

[Test]
public void EmptySubmeshRemainsRepresentedWithItsBinding()
{
    var plan = MeshSeparationPlanner.Create(OneSubmeshWithBinding(7));

    Assert.That(plan.Submeshes, Has.Count.EqualTo(1));
    Assert.That(plan.Submeshes[0].SourceSubmeshIndex, Is.Zero);
    Assert.That(plan.Submeshes[0].SourceMaterialBindingIndex, Is.EqualTo(7));
    Assert.That(plan.Submeshes[0].Disposition,
        Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
    AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals);
    AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals);
}
```

- [ ] **Step 2: Run the four tests and verify red**

Expected: the production boundary compiles, but assertions fail because the Task 1 shell emits no per-submesh record.

- [ ] **Step 3: Implement only unchanged per-submesh planning**

For each source submesh in ascending input order, create transparent ordinals `0..TriangleCount-1`, an empty opaque list, and `Unchanged`. Set `SourceSubmeshIndex` from the input list ordinal and copy `SourceMaterialBindingIndex` from the corresponding input. Aggregate transparent counts. Do not add the `ProvenOpaque` branch in this task.

- [ ] **Step 4: Run unchanged tests green**

Expected: all four tests pass; `Unknown` and `MustRemainTransparent` remain retained, the empty record is present, and binding `7` is preserved without inference.

---

### Task 3: Wholly opaque and mixed split behavior

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/MeshSeparationPlannerTests.cs`
- Modify after red: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/MeshSeparationPlanner.cs`

**Interfaces:** Adds the sole opaque-membership branch and derives `WhollyOpaqueCandidate` versus `Split` from membership counts.

- [ ] **Step 1: Add literal opaque and mixed tests**

```csharp
[Test]
public void AllProvenOpaqueTrianglesBecomeWhollyOpaqueCandidate()
{
    var plan = MeshSeparationPlanner.Create(OneSubmesh(
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.ProvenOpaque));

    var submesh = plan.Submeshes[0];
    Assert.That(submesh.Disposition,
        Is.EqualTo(SubmeshSeparationDisposition.WhollyOpaqueCandidate));
    AssertOrdinals(submesh.OpaqueTriangleOrdinals, 0, 1, 2, 3);
    AssertOrdinals(submesh.TransparentTriangleOrdinals);
    Assert.That(plan.HasAnyOpaqueCandidates, Is.True);
    Assert.That(plan.RequiresAnySplit, Is.False);
}

[Test]
public void ProvenOpaqueAndTransparentTrianglesRequireSplit()
{
    var plan = MeshSeparationPlanner.Create(OneSubmesh(
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.MustRemainTransparent,
        TriangleAlphaOutcome.MustRemainTransparent));

    var submesh = plan.Submeshes[0];
    Assert.That(submesh.Disposition,
        Is.EqualTo(SubmeshSeparationDisposition.Split));
    AssertOrdinals(submesh.OpaqueTriangleOrdinals, 0, 1);
    AssertOrdinals(submesh.TransparentTriangleOrdinals, 2, 3);
    Assert.That(plan.RequiresAnySplit, Is.True);
}

[Test]
public void UnknownNeverEntersOpaqueMembership()
{
    var plan = MeshSeparationPlanner.Create(OneSubmesh(
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.Unknown,
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.MustRemainTransparent));

    AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0, 2);
    AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 1, 3);
}
```

- [ ] **Step 2: Run the three tests and verify red**

Expected: all-opaque and mixed membership assertions fail because the unchanged-only implementation retains `ProvenOpaque`.

- [ ] **Step 3: Add the sole opaque branch**

Replace the unconditional transparent append with:

```csharp
if (outcome == TriangleAlphaOutcome.ProvenOpaque)
    opaqueOrdinals.Add(triangleOrdinal);
else
    transparentOrdinals.Add(triangleOrdinal);
```

Do not use negated checks, numeric enum ordering, or a default promotion. Derive disposition only after the scan:

```csharp
var disposition = opaqueOrdinals.Count == 0
    ? SubmeshSeparationDisposition.Unchanged
    : transparentOrdinals.Count == 0
        ? SubmeshSeparationDisposition.WhollyOpaqueCandidate
        : SubmeshSeparationDisposition.Split;
```

Update aggregate counts and flags from the copied result lists.

- [ ] **Step 4: Run Tasks 2 and 3 green**

Expected: unchanged, all-opaque, mixed, and `Unknown` cases pass; no empty transparent output is represented for the wholly opaque case.

---

### Task 4: Explicit material-binding provenance and multiple submeshes

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/MeshSeparationPlannerTests.cs`
- Modify after red if binding is inferred or reordered: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/MeshSeparationPlanner.cs`

**Interfaces:** Confirms independent submesh planning and exact caller-supplied binding provenance, including reordered and duplicate binding indices.

- [ ] **Step 1: Add the approved multi-submesh case with non-identity bindings**

```csharp
[Test]
public void MultipleSubmeshesPreserveExplicitBindingsAndSourceOrder()
{
    var indices0 = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
    var indices1 = new[] { 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
    var indices2 = new[] { 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35 };
    var input = new MeshSeparationInput(
        36,
        new[]
        {
            new SubmeshSeparationInput(4, indices0, new[]
            {
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.MustRemainTransparent
            }),
            new SubmeshSeparationInput(2, indices1, new[]
            {
                TriangleAlphaOutcome.MustRemainTransparent,
                TriangleAlphaOutcome.MustRemainTransparent,
                TriangleAlphaOutcome.Unknown,
                TriangleAlphaOutcome.MustRemainTransparent
            }),
            new SubmeshSeparationInput(4, indices2, new[]
            {
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.ProvenOpaque
            })
        });

    var plan = MeshSeparationPlanner.Create(input);

    CollectionAssert.AreEqual(
        new[]
        {
            SubmeshSeparationDisposition.Split,
            SubmeshSeparationDisposition.Unchanged,
            SubmeshSeparationDisposition.WhollyOpaqueCandidate
        },
        plan.Submeshes.Select(item => item.Disposition));
    CollectionAssert.AreEqual(
        new[] { 0, 1, 2 },
        plan.Submeshes.Select(item => item.SourceSubmeshIndex));
    CollectionAssert.AreEqual(
        new[] { 4, 2, 4 },
        plan.Submeshes.Select(item => item.SourceMaterialBindingIndex));
    AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0, 1, 2);
    AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 3);
    AssertOrdinals(plan.Submeshes[1].TransparentTriangleOrdinals, 0, 1, 2, 3);
    AssertOrdinals(plan.Submeshes[2].OpaqueTriangleOrdinals, 0, 1, 2, 3);
}
```

This test proves source submesh 0 can bind to 4, source submesh 1 can bind to 2, and the duplicate binding 4 remains legal and unchanged.

- [ ] **Step 2: Run the binding test and verify its result**

Expected: it fails if production inferred `SourceMaterialBindingIndex` from `SourceSubmeshIndex`, rejected duplicate bindings, sorted bindings, or omitted provenance. If it already passes because Task 2 copied the field generically, record that the test characterizes the approved host-neutral contract.

- [ ] **Step 3: Add empty-middle-submesh provenance**

```csharp
[Test]
public void EmptyMiddleSubmeshDoesNotShiftBindingProvenance()
{
    var input = new MeshSeparationInput(
        3,
        new[]
        {
            new SubmeshSeparationInput(
                8,
                new[] { 0, 1, 2 },
                new[] { TriangleAlphaOutcome.ProvenOpaque }),
            new SubmeshSeparationInput(
                3,
                System.Array.Empty<int>(),
                System.Array.Empty<TriangleAlphaOutcome>()),
            new SubmeshSeparationInput(
                8,
                new[] { 0, 2, 1 },
                new[] { TriangleAlphaOutcome.Unknown })
        });

    var plan = MeshSeparationPlanner.Create(input);

    CollectionAssert.AreEqual(
        new[] { 0, 1, 2 },
        plan.Submeshes.Select(item => item.SourceSubmeshIndex));
    CollectionAssert.AreEqual(
        new[] { 8, 3, 8 },
        plan.Submeshes.Select(item => item.SourceMaterialBindingIndex));
    Assert.That(plan.Submeshes[1].Disposition,
        Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
    AssertOrdinals(plan.Submeshes[1].OpaqueTriangleOrdinals);
    AssertOrdinals(plan.Submeshes[1].TransparentTriangleOrdinals);
}
```

- [ ] **Step 4: Run both provenance tests green**

Expected: plan records stay in source-submesh order, empty groups remain represented, and bindings `4,2,4` and `8,3,8` are preserved exactly.

---

### Task 5: Triangle occurrence identity, winding, and submesh-local ordinals

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/MeshSeparationPlannerTests.cs`
- Modify only if a test demonstrates a production defect: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/MeshSeparationPlanner.cs`

**Interfaces:** No new API; proves that repeated index triples remain distinct occurrences and per-submesh arrays eliminate global boundary arithmetic.

- [ ] **Step 1: Add legal repeated-index and winding tests**

```csharp
[Test]
public void RepeatedVertexIndicesAndDuplicateTrianglesRemainDistinctOccurrences()
{
    var input = new MeshSeparationInput(
        2,
        new[]
        {
            new SubmeshSeparationInput(
                5,
                new[] { 0, 1, 1, 0, 1, 1 },
                new[]
                {
                    TriangleAlphaOutcome.ProvenOpaque,
                    TriangleAlphaOutcome.Unknown
                })
        });

    var plan = MeshSeparationPlanner.Create(input);

    AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0);
    AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 1);
    CollectionAssert.AreEqual(
        new[] { 0, 1, 1, 0, 1, 1 },
        plan.Source.Submeshes[0].Indices);
}

[Test]
public void SourceIndexTriplesPreserveWindingAndOrder()
{
    var input = new MeshSeparationInput(
        3,
        new[]
        {
            new SubmeshSeparationInput(
                0,
                new[] { 0, 1, 2, 2, 1, 0 },
                new[]
                {
                    TriangleAlphaOutcome.ProvenOpaque,
                    TriangleAlphaOutcome.MustRemainTransparent
                })
        });

    var plan = MeshSeparationPlanner.Create(input);

    CollectionAssert.AreEqual(
        new[] { 0, 1, 2, 2, 1, 0 },
        plan.Source.Submeshes[0].Indices);
    AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0);
    AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 1);
}
```

- [ ] **Step 2: Add submesh-local ordinal coverage**

```csharp
[Test]
public void TriangleOrdinalsRestartWithinEachSourceSubmesh()
{
    var input = new MeshSeparationInput(
        4,
        new[]
        {
            new SubmeshSeparationInput(
                6,
                new[] { 0, 1, 2 },
                new[] { TriangleAlphaOutcome.ProvenOpaque }),
            new SubmeshSeparationInput(
                6,
                new[] { 2, 3, 0 },
                new[] { TriangleAlphaOutcome.Unknown })
        });

    var plan = MeshSeparationPlanner.Create(input);

    AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0);
    AssertOrdinals(plan.Submeshes[1].TransparentTriangleOrdinals, 0);
    CollectionAssert.AreEqual(new[] { 0, 1, 2 }, plan.Source.Submeshes[0].Indices);
    CollectionAssert.AreEqual(new[] { 2, 3, 0 }, plan.Source.Submeshes[1].Indices);
}
```

There is deliberately no global offset/range field to overlap or corrupt. Each copied submesh array is one complete boundary.

- [ ] **Step 3: Run occurrence/provenance tests**

Expected: all three tests pass if ordinals are local, source append order is stable, and topology is copied unchanged. Preserve any observed red and correct only the demonstrated provenance defect.

---

### Task 6: Monotonic safety and immutability

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/MeshSeparationPlannerTests.cs`
- Modify only if a test demonstrates a production defect: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/MeshSeparationPlanner.cs`

**Interfaces:** No new API; adversarially proves conservative membership, absence of a profitability threshold, and caller-mutation protection.

- [ ] **Step 1: Add literal monotonic replacements and source-order coverage**

```csharp
[TestCase(TriangleAlphaOutcome.Unknown)]
[TestCase(TriangleAlphaOutcome.MustRemainTransparent)]
public void ReplacingProvenOpaqueCannotIncreaseOpaqueCount(
    TriangleAlphaOutcome replacement)
{
    var baseline = MeshSeparationPlanner.Create(OneSubmesh(
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.MustRemainTransparent,
        TriangleAlphaOutcome.Unknown));
    var uncertain = MeshSeparationPlanner.Create(OneSubmesh(
        TriangleAlphaOutcome.ProvenOpaque,
        replacement,
        TriangleAlphaOutcome.MustRemainTransparent,
        TriangleAlphaOutcome.Unknown));

    Assert.That(uncertain.OpaqueTriangleCount,
        Is.LessThanOrEqualTo(baseline.OpaqueTriangleCount));
    AssertOrdinals(uncertain.Submeshes[0].OpaqueTriangleOrdinals, 0);
    AssertOrdinals(uncertain.Submeshes[0].TransparentTriangleOrdinals, 1, 2, 3);
}

[Test]
public void AlternatingOpaqueAndUnknownPreservesBothSourceOrders()
{
    var plan = MeshSeparationPlanner.Create(OneSubmesh(
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.Unknown,
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.Unknown));

    AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0, 2);
    AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 1, 3);
}
```

- [ ] **Step 2: Add the no-profitability-threshold case**

```csharp
[Test]
public void OneOpaqueTriangleAmongOneThousandStillProducesAStructuralSplit()
{
    var outcomes = Enumerable.Repeat(
            TriangleAlphaOutcome.MustRemainTransparent,
            1000)
        .ToArray();
    outcomes[731] = TriangleAlphaOutcome.ProvenOpaque;

    var plan = MeshSeparationPlanner.Create(OneSubmesh(outcomes));

    Assert.That(plan.Submeshes[0].Disposition,
        Is.EqualTo(SubmeshSeparationDisposition.Split));
    AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 731);
    Assert.That(plan.Submeshes[0].TransparentTriangleOrdinals,
        Has.Count.EqualTo(999));
}
```

- [ ] **Step 3: Add post-plan caller mutation isolation**

```csharp
[Test]
public void CallerMutationAfterPlanCreationCannotChangePlan()
{
    var indices = new[] { 0, 1, 2, 2, 3, 0 };
    var outcomes = new[]
    {
        TriangleAlphaOutcome.ProvenOpaque,
        TriangleAlphaOutcome.Unknown
    };
    var input = new MeshSeparationInput(
        4,
        new[]
        {
            new SubmeshSeparationInput(11, indices, outcomes)
        });
    var plan = MeshSeparationPlanner.Create(input);

    indices[0] = 3;
    outcomes[0] = TriangleAlphaOutcome.Unknown;

    Assert.That(plan.Submeshes[0].SourceMaterialBindingIndex, Is.EqualTo(11));
    AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0);
    AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 1);
    CollectionAssert.AreEqual(
        new[] { 0, 1, 2, 2, 3, 0 },
        plan.Source.Submeshes[0].Indices);
}
```

- [ ] **Step 4: Add read-only view protection**

```csharp
[Test]
public void InputAndPlanViewsCannotBeMutatedByCallers()
{
    var plan = MeshSeparationPlanner.Create(OneSubmeshWithBinding(
        12,
        TriangleAlphaOutcome.ProvenOpaque));
    var mutableMembership = plan.Submeshes[0].OpaqueTriangleOrdinals
        as IList<int>;
    var mutableIndices = plan.Source.Submeshes[0].Indices
        as IList<int>;
    var mutableOutcomes = plan.Source.Submeshes[0].Outcomes
        as IList<TriangleAlphaOutcome>;

    if (mutableMembership != null)
        Assert.Throws<System.NotSupportedException>(() => mutableMembership[0] = 99);
    if (mutableIndices != null)
        Assert.Throws<System.NotSupportedException>(() => mutableIndices[0] = 99);
    if (mutableOutcomes != null)
        Assert.Throws<System.NotSupportedException>(() =>
            mutableOutcomes[0] = TriangleAlphaOutcome.Unknown);

    Assert.That(plan.Submeshes[0].SourceMaterialBindingIndex, Is.EqualTo(12));
    AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0);
    Assert.That(plan.Source.Submeshes[0].Indices[0], Is.Zero);
    Assert.That(plan.Source.Submeshes[0].Outcomes[0],
        Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
}
```

- [ ] **Step 5: Run adversarial tests and mutation-check the implementation**

Expected: all tests pass without production changes if the explicit `ProvenOpaque` branch, source-order append, binding copy, and defensive collections are correct. Verify mentally that changing the opaque equality branch, sorting an ordinal/binding sequence, retaining caller storage, inferring binding from submesh index, omitting an empty submesh, or adding a triangle threshold would fail at least one named test.

---

### Task 7: Complete validation and handoff

**Files:**

- Modify only for demonstrated defects: the two future separation-plan C# files.
- Do not modify approved design/plan semantics without returning to the user if implementation reveals a real contract conflict.

**Interfaces:** No new production API.

- [ ] **Step 1: Run the complete planner test class**

Use Unity MCP `run_tests` with:

```text
mode: EditMode
test_names: Alrauna.AlphaMaterialOptimizer.Tests.Editor.Analysis.MeshSeparationPlannerTests
include_failed_tests: true
```

Poll `get_test_job(wait_timeout: 60, include_failed_tests: true)`. Expected: zero failures and zero skipped planner tests.

- [ ] **Step 2: Run the upstream classifier and fixture suites unchanged**

Run these exact EditMode fixtures:

```text
Alrauna.AlphaMaterialOptimizer.Tests.Editor.Analysis.TriangleAlphaClassifierTests
Alrauna.AlphaMaterialOptimizer.Tests.Editor.ReferenceFixtures.ReferenceFixtureIntegrityTests
```

Expected: zero failures. This proves planner work did not change the upstream semantic boundary or fixture infrastructure.

- [ ] **Step 3: Run the complete EditMode suite**

Use Unity MCP `run_tests(mode: EditMode, include_failed_tests: true)` and poll the job. Record observed total, passed, failed, skipped, and duration. Expected: zero failures and no unexpected warnings/errors.

- [ ] **Step 4: Read the final public Unity baseline**

Read `mcpforunity://instances` first, select only the instance whose project root is `E:/AI/Git/alpha-material-optimizer-ndmf`, then read `project/info`, `editor/state`, EditMode test discovery, and Console errors/warnings. Do not select or inspect a private testbed beyond identity discovery.

Expected: Unity `2022.3.22f1`, package `0.0.1`, planner and classifier tests discovered, editor not compiling, zero compiler errors, and zero unexpected Console warnings/errors.

- [ ] **Step 5: Verify the portable source boundary**

Run:

```powershell
rg -n "UnityEngine|MeshTopology|MaterialSlotCount|MeshSeparationStatus|UnsupportedTopology|TooFewMaterialSlots|ExtraMaterialSlots|Renderer|GameObject|Texture2D|NDMF|BigInteger|Parallel|Job|Burst" Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/MeshSeparationPlanner.cs
```

Expected: no matches. Then run:

```powershell
rg -n "SourceMaterialBindingIndex|TriangleAlphaOutcome.ProvenOpaque" Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/MeshSeparationPlanner.cs
```

Expected: explicit binding provenance and the sole opaque-permission branch are present.

- [ ] **Step 6: Verify repository and Unity asset scope**

Run:

```powershell
git diff --check
git status --short
git diff --stat
git diff
git diff --cached --stat
git diff --cached
git diff --name-only main...HEAD
```

Confirm only the approved spec/plan, planner production/test files, and their `.meta` pairs changed. Confirm classifier code/tests, fixture JSON, asmdefs, package manifests/locks, dependencies, workflows, scenes, and private content are unchanged. Inspect each added `.meta` file for a unique stable GUID and correct asset pairing.

For any still-untracked intended file, run `git diff --no-index --check -- NUL <path>` on Windows. Exit `1` is expected because the new file differs from `NUL`; whitespace-error diagnostics are not expected.

- [ ] **Step 7: Review approved requirements line by line**

Compare implementation and observed tests with every section of `docs/superpowers/specs/2026-08-15-separation-plan-design.md`. Explicitly record:

- production API as implemented;
- triangle ordinal and binding-provenance representation;
- all outcome mappings and submesh dispositions;
- empty/no-op and malformed behavior;
- confirmation that no generic unsupported status or host policy entered the planner;
- determinism and complexity;
- validation run or skipped;
- whether the public MCP project or private testbed was modified.

- [ ] **Step 8: Commit only if separately authorized**

If and only if the user has authorized commits, stage only reviewed files and use coherent messages such as:

```text
test: specify host-neutral separation planning
feat: plan opaque triangle separation
```

Otherwise leave all changes unstaged and report commit status.

## Approval and execution gate

This plan is documentation only. Stop here until the user approves both `docs/superpowers/specs/2026-08-15-separation-plan-design.md` and this implementation plan. After approval, use inline `superpowers:executing-plans` by default, plus `superpowers:test-driven-development`, `ponytail:ponytail`, and `superpowers:verification-before-completion`. Do not dispatch subagents, commit, push, or open a PR without separate authorization.
