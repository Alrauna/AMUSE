# End-to-End Alpha Analysis Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Compose AMUSE's existing semantic, texture-evidence, exact-geometry, and separation-planning components into one Editor-only, read-only analysis that turns a supported Unity `Renderer` into an immutable `MeshSeparationPlan` plus per-submesh refusal provenance.

**Architecture:** One static Host entry point (`UnityRendererAlphaAnalysis.Analyze`) validates the renderer, refuses outright when a `MaterialPropertyBlock` could invalidate base-material proofs, resolves alpha semantics once per distinct material through a two-branch shader-frontend dispatch, classifies each triangle through the existing exact classifier, and hands every submesh — analyzable or not — to the unchanged `MeshSeparationPlanner`. Nothing upstream of `Host` changes. Nothing mutates.

**Tech Stack:** Unity 2022.3.22f1 Editor, C#, NUnit EditMode tests, NDMF 1.14.4 (resolved, unused by this code).

**Design:** `docs/superpowers/specs/2026-08-20-end-to-end-alpha-analysis-design.md`, revision 2 — read it before Task 1. It is the authority for every decision below.

**Status: executed and complete (2026-08-20). Awaiting Git/PR finalization.** Tasks 0–7 all
ran against the positively identified public Unity project. Baseline 666 / 666; final
**695 / 695 passed, 0 failed, 0 skipped, 0 console errors**, 29 tests added. Task 1 measured
that a non-readable imported mesh reads completely in the Editor; Task 3 measured that
`HasPropertyBlock()` covers index-scoped blocks; Task 5 passed on its first run as a
composition test, recorded as such rather than as a RED/GREEN cycle; Task 6 confirmed a
non-readable texture refusal stays inside its own submesh. No stop condition fired, and no
frozen component changed.

**Plan revision 2** applied the architectural review: the mesh-readability characterization now runs *first*, per-task commits are removed, the property-block guard is added, UV dependency semantics are corrected, the extra-material rationale is corrected, the manufactured Task 5 RED is removed, `.meta` files are in scope, and the proposed architecture guard is dropped because an equivalent already exists.

## Global Constraints

- Base commit: `7f37b11`. Branch: `feat/end-to-end-alpha-analysis`.
- **Nothing is staged, committed, pushed, or opened as a PR at any point in this plan.** All implementation work stays unstaged through the final review gate. Git finalization requires separate, explicit authorization and is not implied by implementation authorization.
- Production code is **observational only**: no `MeshFilter.mesh`, no `Renderer.materials`, no `Renderer.GetPropertyBlock`, no `BakeMesh`, no importer writes, no `AssetDatabase` writes, no `SaveAssets`, no scene mutation.
- **Never write `catch (Exception)`.** Do not add exception handling around Unity mesh reads at all unless Task 1 observes a throw, and in that case stop for review rather than choosing a policy.
- Do not modify `MeshSeparationPlanner`, `TriangleAlphaClassifier`, `ExactUvGeometry`, `AlphaSemanticsResolver`, `MaterialSemantics`, `UnityTextureEvidence`, `UnityAlphaFieldEvidence`, or either shader frontend. If a change to any of them appears necessary, **stop and escalate**.
- Do not add project dependencies, vendor shader packages, or VPM repositories. Do not touch `Packages/manifest.json`, `Packages/packages-lock.json`, or `Packages/vpm-manifest.json`.
- Do not use the private avatar testbed. Every Unity MCP operation whose result is reported must first confirm, by read-only discovery, that the instance's normalized `Application.dataPath` equals `<repo-root>/Assets`.
- `Alrauna.Amuse.Editor.Analysis` must gain no `UnityEditor` dependency. Do **not** add a guard test for this — one already exists in `UnityAlphaFieldEvidenceTests`.
- Proof-first: only `TriangleAlphaOutcome.ProvenOpaque` may become an opaque candidate. Uncertainty always yields `Unknown`, never a wider claim.
- Every new test class is `public sealed`, lives in `Alrauna.Amuse.Tests.Editor.*`, and cleans up every object and asset folder it creates in `[TearDown]`.
- Tests run through the Unity Test Runner (EditMode). Where MCP is available: `run_tests` with `mode: "EditMode"`, then `get_test_job`. Never report a result that was not observed.

## File Structure

**Create (production):**

| File | Responsibility |
|---|---|
| `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs` | The whole of shader-frontend selection: Poiyomi, then lilToon, then all-Unknown. ~40 lines. |
| `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs` | Renderer validation and the property-block guard, mesh extraction, per-material resolution, per-triangle classification, planner call, and the immutable result types. |

**Create (tests):**

| File | Responsibility |
|---|---|
| `Packages/com.alrauna.amuse/Tests/Editor/Host/MeshReadabilityCharacterizationTests.cs` | Records whether `vertices`, `uv`, and `GetIndices` succeed on a non-readable imported mesh in the Editor. |
| `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs` | Dispatch refusal behaviour, and an explicit statement of what the public project cannot exercise. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs` | Contract and refusal matrix over procedurally built meshes. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/RendererAlphaAnalysisIntegrationTests.cs` | The vertical slice, the UV-dependency cases that need a real sampled equation, the non-readable-texture characterization, and source immutability. |

**Create (Unity metadata) — in scope, Unity-generated, never hand-written:**

```
Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs.meta
Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs.meta
Packages/com.alrauna.amuse/Tests/Editor/Host/MeshReadabilityCharacterizationTests.cs.meta
Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs.meta
Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs.meta
Packages/com.alrauna.amuse/Tests/Editor/Host/RendererAlphaAnalysisIntegrationTests.cs.meta
```

**Never** hand-write, copy, clone, or invent a GUID. Let Unity import each new `.cs` and
generate its `.meta`, then inspect. No new directory is introduced by this plan, so no
folder `.meta` should appear; if one does, stop and investigate.

**Modify:** nothing.

**Explicitly not created:** `Tests/Editor/ArchitectureGuardTests.cs`. An equivalent
non-vacuous guard already exists in
`Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`
(`AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace`, with the permanent positive
control `UnityEditorDetector_ReportsADirectoryThatDoesDependOnIt`). Do not duplicate it.

## Metadata checkpoint

Run this after **every** task that creates a `.cs` file, before moving on:

- [ ] Trigger a Unity asset refresh so the new script imports and its `.meta` is generated.
- [ ] `git status --short` — every new `.cs` has exactly one matching `.cs.meta`, both
      untracked (`??`). **No pre-existing `.meta` appears as modified (` M`) or deleted.**
- [ ] Each new `.meta` carries a `guid:` line, and every new GUID is unique **across this
      repository's own asset set** — not merely among the new files. The invariant is that
      every `.meta` GUID under `Assets/` and `Packages/com.alrauna.amuse/` is distinct, so
      a newly generated GUID must collide with neither another new one nor any pre-existing
      one:

```bash
find "$(git rev-parse --show-toplevel)/Assets" "$(git rev-parse --show-toplevel)/Packages/com.alrauna.amuse" -name '*.meta' -print0 | xargs -0 grep -h '^guid:' | sort | uniq -d
```

Expected: no output. The `find` walks tracked and untracked `.meta` files alike, so it
covers both halves of the invariant in one pass. The roots come from
`git rev-parse --show-toplevel`, never from a hard-coded checkout path, and the command
uses only `find`, `xargs`, `grep`, `sort`, and `uniq`, which behave identically on macOS,
Linux, and Git Bash.

Scope note, verified on this branch's base commit: restricting the walk to those two roots
is not a convenience. Scanning all of `Packages/` reports twelve pre-existing duplicates,
every one of them a pair of `Packages/nadena.dev.ndmf/Dependencies~/X.meta` and
`Packages/nadena.dev.ndmf/Dependencies/X.meta` — the NDMF standalone bootstrap copies that
directory verbatim, GUIDs included. Unity never sees the `Dependencies~` original (a
trailing tilde hides a directory from the asset database), so it is not a real collision,
it is third-party vendored content, and it is outside anything this milestone may touch.
Including it would make the check permanently red and therefore useless. `Library/`,
`Temp/`, `Logs/`, and `UserSettings/` fall outside both roots already.

Any duplicate GUID within the two roots means a `.meta` was copied rather than generated.
**Delete the new `.meta` and re-import it.** Never edit or regenerate a pre-existing `.meta`
to resolve a collision.

---

### Task 0: Establish the Unity baseline

**Files:** none created or modified.

**Interfaces:**
- Consumes: nothing.
- Produces: a recorded baseline EditMode pass/fail count, or a recorded statement that no eligible Unity instance was reachable.

- [ ] **Step 1: Confirm the branch and a clean tree**

Run:

```bash
git rev-parse --abbrev-ref HEAD && git status --short && git log --oneline -1
```

Expected: `feat/end-to-end-alpha-analysis`, only the two design/plan documents listed, and
the base commit at `HEAD`.

- [ ] **Step 2: Identify the Unity instance, read-only**

Enumerate reachable Unity MCP instances and read `Application.dataPath` from each. The
eligible instance is the one whose normalized `dataPath` equals `<repo-root>/Assets`, where
`<repo-root>` comes from `git rev-parse --show-toplevel`. Normalize by resolving relative
and symbolic segments, unifying separators to `/`, and dropping a trailing separator, then
compare exactly. A case-only match is **not** a match: stop and report.

If no instance is reachable, record "Editor validation blocked — no Unity instance" and
stop. **Task 1 requires a Unity instance and gates the production read path**, so
implementation cannot proceed past it without one.

- [ ] **Step 3: Run the full EditMode suite as a baseline**

Run the Unity Test Runner in EditMode over `Alrauna.Amuse.Tests.Editor` and record the
observed total, passed, and failed counts.

Expected: zero failures. **Do not assume the historical count of 666.** If anything fails on
untouched `main`, stop and report before writing code.

- [ ] **Step 4: Record the baseline**

Write the observed numbers into the task notes. Nothing is created, staged, or committed.

---

### Task 1: Mesh readability characterization — runs before any production read path

**Files:**
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Host/MeshReadabilityCharacterizationTests.cs` (+ Unity-generated `.meta`)

**Interfaces:**
- Consumes: nothing. **No AMUSE production code is involved** — this characterizes Unity itself.
- Produces: the observed fact that decides whether the production read path needs any
  unreadable-mesh handling at all.

**Architectural question this task answers:** whether `Mesh.vertices`, `Mesh.uv`, and
`Mesh.GetIndices` succeed in the Editor on an imported mesh with
`ModelImporter.isReadable == false` — the default that real avatar models ship with.

**This is characterization, not RED/GREEN.** Unity documents that mesh data access is
permitted from Editor code outside the game/rendering loop regardless of `isReadable`, so
this is expected to pass on first run. Record that truthfully; do not manufacture a failing
version.

- [ ] **Step 1: Write the characterization test**

Create `Packages/com.alrauna.amuse/Tests/Editor/Host/MeshReadabilityCharacterizationTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// Records whether a mesh imported with Read/Write Enabled off can be read
    /// from Editor code. That default is what real avatar models ship with, so
    /// the answer bounds how much of a real avatar renderer analysis can reach,
    /// and it decides whether the production read path needs any unreadable-mesh
    /// handling at all.
    /// <para>
    /// Unity documents that mesh data access is allowed from the Editor outside
    /// the game/rendering loop even when <c>Mesh.isReadable</c> is false, which
    /// is the regime both EditMode tests and a build-time NDMF pass run in. This
    /// test observes that claim in this project rather than assuming it, and it
    /// runs before any production read path is written so the policy follows the
    /// observation rather than the reverse.
    /// </para>
    /// <para>
    /// Each operation is exercised exactly once, inside its own
    /// <c>Assert.DoesNotThrow</c>, so a failure names the exact operation and
    /// NUnit reports the exact exception type. No AMUSE production code is
    /// involved.
    /// </para>
    /// </summary>
    public sealed class MeshReadabilityCharacterizationTests
    {
        private const string TempFolder = "Assets/AmuseTests_MeshReadability";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_MeshReadability");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        /// <summary>
        /// A one-triangle Wavefront OBJ with UV0. Generated rather than
        /// committed: deterministic, redistributable, and it leaves no binary
        /// fixture behind.
        /// </summary>
        private static Mesh ImportNonReadableTriangle()
        {
            var path = TempFolder + "/triangle.obj";
            File.WriteAllText(
                path,
                "o amuse_triangle\n" +
                "v 0.0 0.0 0.0\n" +
                "v 1.0 0.0 0.0\n" +
                "v 0.0 1.0 0.0\n" +
                "vt 0.6 0.6\n" +
                "vt 0.9 0.6\n" +
                "vt 0.6 0.9\n" +
                "f 1/1 2/2 3/3\n");

            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        [Test]
        public void NonReadableImportedMeshCanBeReadFromEditorCode()
        {
            var mesh = ImportNonReadableTriangle();
            Assert.That(mesh, Is.Not.Null, "The generated OBJ must import.");
            Assert.That(
                mesh.isReadable,
                Is.False,
                "The fixture must actually be non-readable, or it proves nothing.");

            var vertexCount = mesh.vertexCount;
            Assert.That(vertexCount, Is.EqualTo(3));

            Vector3[] positions = null;
            Vector2[] uv = null;
            int[] indices = null;

            Assert.DoesNotThrow(
                () => positions = mesh.vertices,
                "Mesh.vertices threw on a non-readable mesh in the Editor.");
            Assert.DoesNotThrow(
                () => uv = mesh.uv,
                "Mesh.uv threw on a non-readable mesh in the Editor.");
            Assert.DoesNotThrow(
                () => indices = mesh.GetIndices(0),
                "Mesh.GetIndices threw on a non-readable mesh in the Editor.");

            Assert.That(
                positions, Is.Not.Null.And.Length.EqualTo(vertexCount),
                "A non-readable mesh must yield complete positions or throw, " +
                "never a short or empty array.");
            Assert.That(
                uv, Is.Not.Null.And.Length.EqualTo(vertexCount),
                "The OBJ carries UV0, so a complete UV array is expected.");
            Assert.That(
                indices, Is.Not.Null.And.Length.EqualTo(3));

            TestContext.WriteLine(
                "Observed: isReadable=False, vertices=" + positions.Length +
                ", uv=" + uv.Length + ", indices=" + indices.Length);
        }
    }
}
```

- [ ] **Step 2: Run the test and record the observed behaviour**

Run the EditMode suite filtered to `MeshReadabilityCharacterizationTests`.

**Expected: PASS.** Copy the `TestContext.WriteLine` output into the task notes verbatim.

**If any `Assert.DoesNotThrow` fails: STOP.** Record the exact operation named in the
assertion message and the exact exception type NUnit reports, and escalate for
architectural review before writing any production read path. **Do not add a `catch`, and
never `catch (Exception)`.**

**If an operation returns a short or empty array without throwing: STOP** and escalate the
same way — that changes what `MalformedMeshData` means.

- [ ] **Step 3: Metadata checkpoint**

Run the Metadata checkpoint above for `MeshReadabilityCharacterizationTests.cs`.

- [ ] **Step 4: Record the finding in the design document**

Replace the expectation wording in the design's "Mesh readability and exception policy"
subsection with the observed result, and update Risks item 3 to state the measured
behaviour. Change no other design decision on the strength of it.

- [ ] **Step 5: Checkpoint (no commit)**

```bash
git diff --check && git status --short
```

Expected: the two documents plus the new test and its `.meta`, all untracked. Nothing
staged.

---

### Task 2: Shader-frontend dispatch

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs` (+ `.meta`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `PoiyomiMaterialSemantics.AnalyzeBaseMaterial(Material) → PoiyomiSemanticResult`, `LilToonMaterialSemantics.AnalyzeBaseMaterial(Material) → LilToonSemanticResult`, both with `bool IsSupportedMaterial` and `MaterialSemantics Semantics`.
- Produces: `internal static MaterialSemantics UnityMaterialSemantics.AnalyzeBaseMaterial(Material material)` and `internal static MaterialSemantics UnityMaterialSemantics.AllUnknown()`, both in namespace `Alrauna.Amuse.Editor.Semantics`.

**Architectural question this task proves:** that frontend selection needs no interface,
registry, or dispatch table — each frontend's own attestation is sufficient and exclusive.

- [ ] **Step 1: Write the failing test**

Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`:

```csharp
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// Frontend selection, and an explicit record of its public-project limit.
    /// <para>
    /// The public development project installs neither Poiyomi nor lilToon, so
    /// no material here can pass either frontend's source attestation. These
    /// tests therefore exercise the real refusal path on real Unity objects and
    /// make no claim about vendor dispatch, which remains a production
    /// capability the public suite cannot observe.
    /// </para>
    /// </summary>
    public sealed class UnityMaterialSemanticsTests
    {
        private Material _material;

        [TearDown]
        public void TearDown()
        {
            if (_material != null)
            {
                Object.DestroyImmediate(_material);
            }

            _material = null;
        }

        private static void AssertAllUnknown(MaterialSemantics semantics)
        {
            Assert.That(semantics, Is.Not.Null);
            Assert.That(semantics.BaseColor.IsComplete, Is.False);
            Assert.That(semantics.Alpha.IsComplete, Is.False);
            Assert.That(semantics.Emission.IsComplete, Is.False);
            Assert.That(semantics.Normal.IsComplete, Is.False);
        }

        [Test]
        public void NullMaterialIsAllUnknownRatherThanAThrow()
        {
            AssertAllUnknown(UnityMaterialSemantics.AnalyzeBaseMaterial(null));
        }

        [Test]
        public void DestroyedMaterialIsAllUnknown()
        {
            var material = new Material(Shader.Find("Unlit/Color"));
            Object.DestroyImmediate(material);

            AssertAllUnknown(UnityMaterialSemantics.AnalyzeBaseMaterial(material));
        }

        [Test]
        public void MaterialNeitherFrontendAttestsIsAllUnknown()
        {
            _material = new Material(Shader.Find("Unlit/Color"));

            AssertAllUnknown(
                UnityMaterialSemantics.AnalyzeBaseMaterial(_material));
        }

        [Test]
        public void AllUnknownIsUnknownInEveryOutput()
        {
            AssertAllUnknown(UnityMaterialSemantics.AllUnknown());
        }
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the EditMode suite filtered to `UnityMaterialSemanticsTests`.
Expected: compile failure — `UnityMaterialSemantics` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`:

```csharp
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Selects the shader frontend for one base material. Each frontend attests
    /// its own source identity, and no material can be attested by both, so
    /// selection is an exclusive trial rather than a dispatch table: a second
    /// place deciding "is this a Poiyomi material" could only disagree with the
    /// first. This is deliberately not an adapter interface, a registry, or a
    /// provider framework; with a third family it becomes a third branch, and
    /// that is when a registry earns its first honest argument.
    /// </summary>
    internal static class UnityMaterialSemantics
    {
        /// <summary>
        /// Analyzes the current values of one supplied base material. It makes
        /// no claim about later animation, material swaps, property blocks, or
        /// modifier processing. A material no frontend attests is all-Unknown,
        /// which is the conservative answer and never a refusal to answer.
        /// </summary>
        internal static MaterialSemantics AnalyzeBaseMaterial(Material material)
        {
            // The frontends throw for these; the correct answer is Unknown, and
            // an unassigned or destroyed slot is an ordinary input.
            if (material == null || material.shader == null)
            {
                return AllUnknown();
            }

            var poiyomi = PoiyomiMaterialSemantics.AnalyzeBaseMaterial(material);
            if (poiyomi.IsSupportedMaterial)
            {
                return poiyomi.Semantics;
            }

            // An unsupported lilToon result is itself all-Unknown, which is
            // exactly the answer for a material neither frontend attests.
            return LilToonMaterialSemantics
                .AnalyzeBaseMaterial(material)
                .Semantics;
        }

        internal static MaterialSemantics AllUnknown()
        {
            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<ScalarSemanticValue>.Unknown(),
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run the EditMode suite filtered to `UnityMaterialSemanticsTests`.
Expected: 4 passed, 0 failed.

- [ ] **Step 5: Metadata checkpoint**

Run the Metadata checkpoint for `UnityMaterialSemantics.cs` and
`UnityMaterialSemanticsTests.cs`.

- [ ] **Step 6: Checkpoint (no commit)**

```bash
git diff --check && git status --short
```

Expected: documents, Task 1 files, and the two new files with their `.meta`s — all
untracked. Nothing staged.

---

### Task 3: Renderer validation and the property-block guard

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs` (+ `.meta`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `UnityMaterialSemantics.AnalyzeBaseMaterial` (Task 2).
- Produces, in namespace `Alrauna.Amuse.Editor.Host`:
  - `internal enum RendererAnalysisRefusal { None, UnsupportedRendererType, MaterialPropertyOverridesPresent, MissingMesh, UnprovenMaterialSlotMapping, UnsupportedTopology, MalformedMeshData }`
  - `internal sealed class SubmeshAlphaAnalysis { int SubmeshIndex; int MaterialSlotIndex; bool HasMaterial; AlphaResolutionFailure Failure; }`
  - `internal sealed class RendererAlphaAnalysis { RendererAnalysisRefusal Refusal; MeshSeparationPlan Plan; IReadOnlyList<SubmeshAlphaAnalysis> Submeshes; }` with `Refused(...)` and `Planned(...)` factories
  - `internal delegate MaterialSemantics BaseMaterialSemanticsProvider(Material material);`
  - `internal static RendererAlphaAnalysis UnityRendererAlphaAnalysis.Analyze(Renderer renderer)`
  - `internal static RendererAlphaAnalysis UnityRendererAlphaAnalysis.Analyze(Renderer renderer, BaseMaterialSemanticsProvider semanticsProvider)`

**Architectural questions this task proves:** that every unproven Unity renderer state can
be refused *before* `MeshSeparationInput` would throw; that a `MaterialPropertyBlock`
cannot silently invalidate a base-material proof; and that `HasPropertyBlock()` actually
covers per-material-index blocks.

- [ ] **Step 1: Write the failing refusal tests**

Create `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`:

```csharp
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// Renderer-level contract and refusal matrix. Meshes are built
    /// procedurally, so they are always readable and no asset is imported;
    /// import behaviour is characterized separately in
    /// <see cref="MeshReadabilityCharacterizationTests"/>.
    /// </summary>
    public sealed class UnityRendererAlphaAnalysisTests
    {
        private readonly List<Object> _transient = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _transient)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _transient.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            _transient.Add(obj);
            return obj;
        }

        /// <summary>Two triangles over four vertices, UV0 present.</summary>
        private Mesh Quad(MeshTopology topology = MeshTopology.Triangles)
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(0f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.6f, 0.6f),
                new Vector2(0.9f, 0.6f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.6f, 0.9f)
            };
            if (topology == MeshTopology.Triangles)
            {
                mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            }
            else
            {
                mesh.SetIndices(new[] { 0, 1, 2, 3 }, MeshTopology.Quads, 0);
            }

            return mesh;
        }

        private Material NewMaterial()
        {
            return Track(new Material(Shader.Find("Unlit/Color")));
        }

        private SkinnedMeshRenderer NewSkinned(Mesh mesh, params Material[] slots)
        {
            var gameObject = Track(new GameObject("amuse-test"));
            var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = slots;
            return renderer;
        }

        [Test]
        public void UnsupportedRendererTypeRefusesWithoutAPlan()
        {
            var gameObject = Track(new GameObject("amuse-test-line"));
            var renderer = gameObject.AddComponent<LineRenderer>();

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.UnsupportedRendererType));
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Submeshes, Is.Empty);
        }

        /// <summary>
        /// A property block can override the very properties the shader
        /// frontends read to prove alpha, so a base-material ProvenOpaque
        /// conclusion could be false for this renderer. The guard reads the
        /// presence bit only; the block's contents are never inspected.
        /// </summary>
        [Test]
        public void APropertyBlockRefusesTheWholeRenderer()
        {
            var renderer = NewSkinned(Quad(), NewMaterial());
            var block = new MaterialPropertyBlock();
            block.SetFloat("_AmuseTestOverride", 0.5f);
            renderer.SetPropertyBlock(block);

            Assert.That(
                renderer.HasPropertyBlock(),
                Is.True,
                "The fixture must attach a real block, or it proves nothing.");

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(
                    RendererAnalysisRefusal.MaterialPropertyOverridesPresent));
            Assert.That(result.Plan, Is.Null);
        }

        /// <summary>
        /// Verifies the guard is not blind to a block attached to one material
        /// index rather than the whole renderer. If HasPropertyBlock() does not
        /// report this, the guard has a hole and implementation must stop for
        /// architectural review rather than reach for a wider API.
        /// </summary>
        [Test]
        public void APerMaterialIndexPropertyBlockAlsoRefuses()
        {
            var renderer = NewSkinned(Quad(), NewMaterial());
            var block = new MaterialPropertyBlock();
            block.SetFloat("_AmuseTestOverride", 0.5f);
            renderer.SetPropertyBlock(block, 0);

            Assert.That(
                renderer.HasPropertyBlock(),
                Is.True,
                "STOP CONDITION: HasPropertyBlock() does not report a " +
                "per-material-index block, so the guard has a hole. Escalate " +
                "for architectural review; do not widen the API here.");

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(
                    RendererAnalysisRefusal.MaterialPropertyOverridesPresent));
        }

        [Test]
        public void SkinnedRendererWithoutAMeshRefusesWithMissingMesh()
        {
            var renderer = NewSkinned(null, NewMaterial());

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.MissingMesh));
            Assert.That(result.Plan, Is.Null);
        }

        [Test]
        public void MeshRendererWithoutAMeshFilterRefusesWithMissingMesh()
        {
            var gameObject = Track(new GameObject("amuse-test-mesh"));
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { NewMaterial() };

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.MissingMesh));
        }

        /// <summary>
        /// Unity's behaviour for surplus materials is documented — the last
        /// submesh is drawn again for each one — but SubmeshSeparationInput
        /// carries exactly one SourceMaterialBindingIndex per source submesh, so
        /// AMUSE cannot represent those extra passes. Refusal, not a guess.
        /// </summary>
        [Test]
        public void MoreMaterialsThanSubmeshesRefusesTheWholeRenderer()
        {
            var renderer = NewSkinned(Quad(), NewMaterial(), NewMaterial());

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.UnprovenMaterialSlotMapping));
            Assert.That(result.Plan, Is.Null);
        }

        [Test]
        public void FewerMaterialsThanSubmeshesRefusesTheWholeRenderer()
        {
            var renderer = NewSkinned(Quad());

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.UnprovenMaterialSlotMapping));
            Assert.That(result.Plan, Is.Null);
        }

        [Test]
        public void NonTriangleTopologyRefusesTheWholeRenderer()
        {
            var renderer = NewSkinned(Quad(MeshTopology.Quads), NewMaterial());

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.UnsupportedTopology));
            Assert.That(result.Plan, Is.Null);
        }

        [Test]
        public void NullRendererThrowsBecauseItIsACallerDefect()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => UnityRendererAlphaAnalysis.Analyze(null));
        }
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the EditMode suite filtered to `UnityRendererAlphaAnalysisTests`.
Expected: compile failure — `UnityRendererAlphaAnalysis` and `RendererAnalysisRefusal` do
not exist.

- [ ] **Step 3: Write the result types and the validation path**

Create `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`:

```csharp
using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// The closed set of facts that make a whole renderer unanalyzable. Each
    /// member is a renderer- or mesh-scoped condition; everything a single
    /// material or triangle can fail at is scoped narrower and never reaches
    /// this enum. Declaration order is the order the checks run in.
    /// </summary>
    internal enum RendererAnalysisRefusal
    {
        None,
        UnsupportedRendererType,
        MaterialPropertyOverridesPresent,
        MissingMesh,
        UnprovenMaterialSlotMapping,
        UnsupportedTopology,
        MalformedMeshData,
    }

    /// <summary>
    /// Why one submesh's alpha could or could not be proven at the material and
    /// resolver level. The failure vocabulary is the resolver's own, reused
    /// rather than duplicated; <see cref="HasMaterial"/> adds the one Unity fact
    /// the resolver cannot express, because an empty slot and an unattested
    /// shader both reduce to <c>SemanticsUnknown</c>.
    /// <para>
    /// It deliberately does <em>not</em> explain every preserved triangle. A
    /// triangle can be <c>Unknown</c> on a submesh whose failure is
    /// <c>None</c> — through unavailable UV0 under a UV-dependent equation, a
    /// non-finite position, degeneracy, or the classifier's own workload
    /// refusal — and no reason for that is recorded anywhere. The original
    /// per-triangle outcomes remain readable from
    /// <c>MeshSeparationPlan.Source</c>, but their causes do not.
    /// </para>
    /// </summary>
    internal sealed class SubmeshAlphaAnalysis
    {
        internal int SubmeshIndex { get; }
        internal int MaterialSlotIndex { get; }
        internal bool HasMaterial { get; }
        internal AlphaResolutionFailure Failure { get; }

        internal SubmeshAlphaAnalysis(
            int submeshIndex,
            int materialSlotIndex,
            bool hasMaterial,
            AlphaResolutionFailure failure)
        {
            SubmeshIndex = submeshIndex;
            MaterialSlotIndex = materialSlotIndex;
            HasMaterial = hasMaterial;
            Failure = failure;
        }
    }

    /// <summary>
    /// One immutable renderer-level analysis: either a separation plan with one
    /// provenance record per submesh, or a named refusal and no plan. It holds
    /// no live Unity object; the caller supplied the renderer and still owns it.
    /// </summary>
    internal sealed class RendererAlphaAnalysis
    {
        internal RendererAnalysisRefusal Refusal { get; }
        internal MeshSeparationPlan Plan { get; }
        internal IReadOnlyList<SubmeshAlphaAnalysis> Submeshes { get; }

        private RendererAlphaAnalysis(
            RendererAnalysisRefusal refusal,
            MeshSeparationPlan plan,
            IReadOnlyList<SubmeshAlphaAnalysis> submeshes)
        {
            // A refusal has no plan and a plan has no refusal.
            if ((refusal == RendererAnalysisRefusal.None) != (plan != null))
            {
                throw new ArgumentException(
                    "A renderer analysis has a plan exactly when it has no refusal.",
                    nameof(refusal));
            }

            var copy = new SubmeshAlphaAnalysis[submeshes.Count];
            for (var index = 0; index < submeshes.Count; index++)
            {
                copy[index] = submeshes[index];
            }

            Refusal = refusal;
            Plan = plan;
            Submeshes = Array.AsReadOnly(copy);
        }

        internal static RendererAlphaAnalysis Refused(
            RendererAnalysisRefusal refusal)
        {
            return new RendererAlphaAnalysis(
                refusal, null, Array.Empty<SubmeshAlphaAnalysis>());
        }

        internal static RendererAlphaAnalysis Planned(
            MeshSeparationPlan plan,
            IReadOnlyList<SubmeshAlphaAnalysis> submeshes)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            return new RendererAlphaAnalysis(
                RendererAnalysisRefusal.None, plan, submeshes);
        }
    }

    /// <summary>
    /// Supplies normalized semantics for one base material. The production
    /// implementation is <see cref="UnityMaterialSemantics.AnalyzeBaseMaterial"/>;
    /// the parameter exists because the public development project installs no
    /// vendor shader and therefore cannot attest one, so deterministic tests
    /// substitute this single link through each frontend's existing
    /// verified-material seam. It mirrors the established
    /// <see cref="AlphaFieldProvider"/> precedent: one delegate, one production
    /// implementation, no registry.
    /// </summary>
    internal delegate MaterialSemantics BaseMaterialSemanticsProvider(
        Material material);

    /// <summary>
    /// Drives AMUSE's existing semantic, texture-evidence, exact-geometry, and
    /// separation-planning components over one Unity renderer's current base
    /// state, and produces an immutable plan describing which geometry is
    /// provably safe for opaque separation.
    /// <para>
    /// It reads only. It uses <c>sharedMesh</c> and <c>sharedMaterials</c>
    /// exclusively, because <c>MeshFilter.mesh</c> and <c>Renderer.materials</c>
    /// instantiate copies as a side effect of being read. It never bakes,
    /// imports, writes, or creates an asset, and it never calls
    /// <c>GetPropertyBlock</c>.
    /// </para>
    /// <para>
    /// It analyzes the current/base material state only. Animator state,
    /// animation clips, material swaps, and property-block contents are outside
    /// its claim — and because a property block can override the properties a
    /// proof rests on, a renderer that carries one is refused outright rather
    /// than analyzed under an assumption.
    /// </para>
    /// </summary>
    internal static class UnityRendererAlphaAnalysis
    {
        internal static RendererAlphaAnalysis Analyze(Renderer renderer)
        {
            return Analyze(renderer, UnityMaterialSemantics.AnalyzeBaseMaterial);
        }

        internal static RendererAlphaAnalysis Analyze(
            Renderer renderer,
            BaseMaterialSemanticsProvider semanticsProvider)
        {
            if (ReferenceEquals(renderer, null))
            {
                throw new ArgumentNullException(nameof(renderer));
            }
            if (semanticsProvider == null)
            {
                throw new ArgumentNullException(nameof(semanticsProvider));
            }

            // Unity's overloaded equality reports a destroyed object as null.
            if (renderer == null)
            {
                throw new ArgumentException(
                    "The renderer has been destroyed and cannot be analyzed.",
                    nameof(renderer));
            }

            if (!IsSupportedRendererType(renderer))
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.UnsupportedRendererType);
            }

            // Presence only. Reading the block's contents would be
            // effective-state analysis, which this milestone does not do; a
            // block that overrides nothing alpha-relevant is refused anyway,
            // which is a false negative and therefore the safe direction.
            if (renderer.HasPropertyBlock())
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.MaterialPropertyOverridesPresent);
            }

            var mesh = SharedMeshOf(renderer);
            if (mesh == null)
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.MissingMesh);
            }

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length != mesh.subMeshCount)
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.UnprovenMaterialSlotMapping);
            }

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                {
                    return RendererAlphaAnalysis.Refused(
                        RendererAnalysisRefusal.UnsupportedTopology);
                }
            }

            throw new NotImplementedException(
                "The analysis path is implemented in the next task.");
        }

        private static bool IsSupportedRendererType(Renderer renderer)
        {
            // ParticleSystemRenderer, LineRenderer, TrailRenderer,
            // SpriteRenderer, and BillboardRenderer derive from Renderer
            // directly, not from MeshRenderer, so this cannot capture them.
            return renderer is SkinnedMeshRenderer || renderer is MeshRenderer;
        }

        /// <summary>
        /// The one mesh a supported renderer contributes. Both paths converge on
        /// a shared reference; neither instantiates a copy.
        /// </summary>
        private static Mesh SharedMeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            var filter = renderer.GetComponent<MeshFilter>();
            return filter == null ? null : filter.sharedMesh;
        }
    }
}
```

The `NotImplementedException` is deliberate staging: no test in this task reaches the
success path, and Task 4 replaces it. It fails loudly rather than returning a misleading
refusal value.

- [ ] **Step 4: Run the tests and verify they pass**

Run the EditMode suite filtered to `UnityRendererAlphaAnalysisTests`.
Expected: 9 passed, 0 failed.

**If `APerMaterialIndexPropertyBlockAlsoRefuses` fails on its `HasPropertyBlock()`
assertion, STOP** and escalate: the guard has a hole and choosing a wider API is an
architectural decision, not an implementation one.

- [ ] **Step 5: Metadata checkpoint**

Run the Metadata checkpoint for `UnityRendererAlphaAnalysis.cs` and
`UnityRendererAlphaAnalysisTests.cs`.

- [ ] **Step 6: Checkpoint (no commit)**

```bash
git diff --check && git status --short
```

Nothing staged.

---

### Task 4: Extraction, classification, and planning

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs` (append)

**Interfaces:**
- Consumes: everything from Task 3, plus `UnityAlphaFieldEvidence(IEnumerable<Texture>)` and its `TryGetAlphaField` method group, `AlphaSemanticsResolver.Resolve(SemanticOutput<ScalarSemanticValue>, AlphaFieldProvider) → AlphaResolution`, `AlphaResolution.IsResolved/Failure/Classify(TriangleAlphaInput)`, `TriangleAlphaInput.WithUv0/MissingUv0`, `SubmeshSeparationInput(int, IReadOnlyList<int>, IReadOnlyList<TriangleAlphaOutcome>)`, `MeshSeparationInput(int, IReadOnlyList<SubmeshSeparationInput>)`, `MeshSeparationPlanner.Create`.
- Produces: `Analyze` returning `RendererAlphaAnalysis.Planned(...)` for every supported renderer.

**Architectural questions this task proves:** that classifier outcomes compose into the
unchanged planner without an impedance mismatch; that uncertainty stays scoped to the
submesh or triangle that owns it; and that **unavailable UV0 invalidates only the
conclusions that actually depend on it**.

- [ ] **Step 1: Write the failing success-path, granularity, and UV-dependency tests**

Append to `UnityRendererAlphaAnalysisTests`. Add these helpers first:

```csharp
        /// <summary>
        /// A provider that proves constant alpha 1 for one nominated material
        /// and knows nothing about any other. A constant alpha is
        /// geometry-independent, so it isolates composition from evidence — and
        /// it is exactly the resolution that must still prove opacity when UV0
        /// is unavailable.
        /// </summary>
        private static BaseMaterialSemanticsProvider OpaqueFor(Material supported)
        {
            return material => ReferenceEquals(material, supported)
                ? new MaterialSemantics(
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    SemanticOutput<ScalarSemanticValue>.Complete(
                        ScalarSemanticValue.Constant(1f)),
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    SemanticOutput<NormalSemanticValue>.Unknown())
                : UnityMaterialSemantics.AllUnknown();
        }

        /// <summary>Two submeshes: one triangle, then two triangles.</summary>
        private Mesh TwoSubmeshMesh()
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(2f, 0f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.6f, 0.6f),
                new Vector2(0.9f, 0.6f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.6f, 0.9f),
                new Vector2(0.7f, 0.7f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 0, 2, 3, 1, 4, 2 }, 1);
            return mesh;
        }
```

Then the tests:

```csharp
        [Test]
        public void ConstantOpaqueAlphaMakesTheWholeSubmeshAnOpaqueCandidate()
        {
            var material = NewMaterial();
            var renderer = NewSkinned(Quad(), material);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, OpaqueFor(material));

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(result.Plan, Is.Not.Null);
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(2));
            Assert.That(result.Plan.RequiresAnySplit, Is.False);
            Assert.That(
                result.Plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.WhollyOpaqueCandidate));
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(result.Submeshes[0].HasMaterial, Is.True);
        }

        [Test]
        public void MeshRendererPathReachesTheSamePlanAsTheSkinnedPath()
        {
            var material = NewMaterial();
            var gameObject = Track(new GameObject("amuse-test-mesh"));
            gameObject.AddComponent<MeshFilter>().sharedMesh = Quad();
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material };

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, OpaqueFor(material));

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(2));
        }

        [Test]
        public void AnUnsupportedSlotDoesNotPoisonItsSupportedNeighbour()
        {
            var supported = NewMaterial();
            var unsupported = NewMaterial();
            var renderer = NewSkinned(TwoSubmeshMesh(), unsupported, supported);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, OpaqueFor(supported));

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown));
            Assert.That(
                result.Plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                result.Plan.Submeshes[1].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.WhollyOpaqueCandidate));
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(2));
            Assert.That(result.Plan.HasAnyOpaqueCandidates, Is.True);
        }

        [Test]
        public void SubmeshAndMaterialSlotIndicesAgreeWithTheSourceOrder()
        {
            var supported = NewMaterial();
            var renderer = NewSkinned(TwoSubmeshMesh(), NewMaterial(), supported);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, OpaqueFor(supported));

            for (var index = 0; index < result.Submeshes.Count; index++)
            {
                Assert.That(result.Submeshes[index].SubmeshIndex, Is.EqualTo(index));
                Assert.That(
                    result.Submeshes[index].MaterialSlotIndex, Is.EqualTo(index));
                Assert.That(
                    result.Plan.Submeshes[index].SourceMaterialBindingIndex,
                    Is.EqualTo(index));
            }
        }

        [Test]
        public void ARepeatedMaterialIsAnalyzedIdenticallyInEverySubmesh()
        {
            var material = NewMaterial();
            var renderer = NewSkinned(TwoSubmeshMesh(), material, material);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, OpaqueFor(material));

            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(3));
        }

        [Test]
        public void ANullMaterialSlotIsRecordedAndPreserved()
        {
            var renderer = NewSkinned(Quad(), null);

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(result.Submeshes[0].HasMaterial, Is.False);
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown));
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(0));
            Assert.That(result.Plan.HasAnyOpaqueCandidates, Is.False);
        }

        [Test]
        public void AnEmptySubmeshIsRepresentedWithoutShiftingItsNeighbour()
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.6f, 0.6f),
                new Vector2(0.9f, 0.6f),
                new Vector2(0.9f, 0.9f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new int[0], 0);
            mesh.SetTriangles(new[] { 0, 1, 2 }, 1);
            var supported = NewMaterial();
            var renderer = NewSkinned(mesh, NewMaterial(), supported);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, OpaqueFor(supported));

            Assert.That(result.Plan.Submeshes[0].OpaqueTriangleOrdinals, Is.Empty);
            Assert.That(
                result.Plan.Submeshes[0].TransparentTriangleOrdinals, Is.Empty);
            Assert.That(
                result.Plan.Submeshes[1].SourceMaterialBindingIndex, Is.EqualTo(1));
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(1));
        }

        /// <summary>
        /// The dependency rule: a constant alpha of exactly one cannot vary
        /// across a surface, so it needs no UV at all. Turning it into Unknown
        /// merely because the mesh carries no UV0 would discard a conclusion
        /// that never depended on the missing knowledge.
        /// </summary>
        [Test]
        public void MissingUv0DoesNotBlockUvIndependentConstantProof()
        {
            var mesh = Quad();
            mesh.uv = null;
            var material = NewMaterial();
            var renderer = NewSkinned(mesh, material);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, OpaqueFor(material));

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                result.Plan.OpaqueTriangleCount,
                Is.EqualTo(2),
                "A constant alpha of one is provable without UV0.");
            Assert.That(
                result.Plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.WhollyOpaqueCandidate));
        }

        /// <summary>
        /// The same dependency rule, at triangle scope: a non-finite UV makes
        /// UV0 unavailable for that one triangle, and a UV-independent proof
        /// must survive it. The finiteness screen exists because the classifier
        /// throws on non-finite UVs, not because non-finite implies Unknown.
        /// </summary>
        [Test]
        public void ANonFiniteUvDoesNotBlockUvIndependentConstantProof()
        {
            var mesh = TwoSubmeshMesh();
            var uv = mesh.uv;
            uv[4] = new Vector2(float.NaN, 0f);
            mesh.uv = uv;
            var material = NewMaterial();
            var renderer = NewSkinned(mesh, material, material);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, OpaqueFor(material));

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Plan.OpaqueTriangleCount,
                Is.EqualTo(3),
                "Every triangle stays provable: none of them needed UV0.");
            Assert.That(result.Plan.TransparentTriangleCount, Is.EqualTo(0));
        }

        [Test]
        public void AnalyzingTheSameRendererTwiceProducesTheSameResult()
        {
            var material = NewMaterial();
            var renderer = NewSkinned(TwoSubmeshMesh(), material, material);

            var first = UnityRendererAlphaAnalysis.Analyze(
                renderer, OpaqueFor(material));
            var second = UnityRendererAlphaAnalysis.Analyze(
                renderer, OpaqueFor(material));

            Assert.That(
                second.Plan.OpaqueTriangleCount,
                Is.EqualTo(first.Plan.OpaqueTriangleCount));
            Assert.That(
                second.Plan.TransparentTriangleCount,
                Is.EqualTo(first.Plan.TransparentTriangleCount));
            Assert.That(
                second.Submeshes.Count, Is.EqualTo(first.Submeshes.Count));
            for (var index = 0; index < first.Submeshes.Count; index++)
            {
                Assert.That(
                    second.Submeshes[index].Failure,
                    Is.EqualTo(first.Submeshes[index].Failure));
                Assert.That(
                    second.Plan.Submeshes[index].Disposition,
                    Is.EqualTo(first.Plan.Submeshes[index].Disposition));
            }
        }
```

The complementary half of the dependency rule — that a **texture-sampled** equation *does*
become `Unknown` without UV0 — needs a real imported texture and a real sampled resolution,
so it lives in Task 5's integration file. Neither half alone proves the rule.

- [ ] **Step 2: Run the tests and verify they fail**

Run the EditMode suite filtered to `UnityRendererAlphaAnalysisTests`.
Expected: the eleven new tests fail with `NotImplementedException`. The nine Task 3 tests
still pass.

- [ ] **Step 3: Replace the placeholder with extraction, classification, and planning**

In `UnityRendererAlphaAnalysis.cs`, replace the `throw new NotImplementedException(...)` in
`Analyze` with the real body, and add the private helpers:

```csharp
            // Unity's documented Editor behaviour is that mesh data is
            // accessible outside the game/rendering loop regardless of
            // Mesh.isReadable, and that was observed for vertices, uv, and
            // GetIndices in this project before this path was written. There is
            // therefore no readability pre-check and no exception handling here:
            // catching what has not been seen thrown would hide the defect.
            var positions = mesh.vertices;
            var uv = mesh.uv;
            if (positions == null || positions.Length != mesh.vertexCount)
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.MalformedMeshData);
            }

            var hasUv0 = uv != null && uv.Length == mesh.vertexCount;
            if (!hasUv0 && uv != null && uv.Length != 0)
            {
                return RendererAlphaAnalysis.Refused(
                    RendererAnalysisRefusal.MalformedMeshData);
            }

            var evidence = new UnityAlphaFieldEvidence(
                GatherCandidateTextures(materials));
            var resolutions = new Dictionary<Material, AlphaResolution>();
            var submeshInputs = new List<SubmeshSeparationInput>(materials.Length);
            var records = new List<SubmeshAlphaAnalysis>(materials.Length);

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var indices = mesh.GetIndices(submesh);
                if (indices == null || indices.Length % 3 != 0)
                {
                    return RendererAlphaAnalysis.Refused(
                        RendererAnalysisRefusal.MalformedMeshData);
                }

                for (var index = 0; index < indices.Length; index++)
                {
                    if (indices[index] < 0 || indices[index] >= mesh.vertexCount)
                    {
                        return RendererAlphaAnalysis.Refused(
                            RendererAnalysisRefusal.MalformedMeshData);
                    }
                }

                var material = materials[submesh];
                var resolution = ResolveFor(
                    material, semanticsProvider, evidence, resolutions);
                var outcomes = Classify(
                    indices, positions, hasUv0 ? uv : null, resolution);

                submeshInputs.Add(
                    new SubmeshSeparationInput(submesh, indices, outcomes));
                records.Add(new SubmeshAlphaAnalysis(
                    submesh,
                    submesh,
                    material != null,
                    resolution.Failure));
            }

            var plan = MeshSeparationPlanner.Create(
                new MeshSeparationInput(mesh.vertexCount, submeshInputs));
            return RendererAlphaAnalysis.Planned(plan, records);
        }

        /// <summary>
        /// Every texture the renderer's own materials reference, read through
        /// each shader's declared texture properties so no AMUSE code names a
        /// property. The set is a superset of what the alpha semantics will ask
        /// for, which is correct and cheap: the provider stores identity only
        /// and reads pixels lazily, and a texture that was not gathered simply
        /// refuses with MissingTextureEvidence.
        /// </summary>
        private static IEnumerable<Texture> GatherCandidateTextures(
            Material[] materials)
        {
            foreach (var material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                foreach (var propertyName in material.GetTexturePropertyNames())
                {
                    yield return material.GetTexture(propertyName);
                }
            }
        }

        /// <summary>
        /// One resolution per distinct material. Attestation hashes the whole
        /// shader source, and avatars repeat material references across slots,
        /// so this memo removes real repeated work. It is local to one analysis
        /// and is discarded when it returns.
        /// </summary>
        private static AlphaResolution ResolveFor(
            Material material,
            BaseMaterialSemanticsProvider semanticsProvider,
            UnityAlphaFieldEvidence evidence,
            Dictionary<Material, AlphaResolution> memo)
        {
            if (material != null && memo.TryGetValue(material, out var cached))
            {
                return cached;
            }

            var semantics = semanticsProvider(material)
                ?? UnityMaterialSemantics.AllUnknown();
            var resolution = AlphaSemanticsResolver.Resolve(
                semantics.Alpha, evidence.TryGetAlphaField);

            if (material != null)
            {
                memo[material] = resolution;
            }

            return resolution;
        }

        /// <summary>
        /// One outcome per triangle, in source order. A refused resolution
        /// yields Unknown without consulting geometry, because a refusal has no
        /// triangle outcome at all.
        /// <para>
        /// Unavailable UV0 — a mesh with no UV channel, or a triangle with a
        /// non-finite UV — is passed on as
        /// <see cref="TriangleAlphaInput.MissingUv0"/> rather than pre-empted as
        /// Unknown, so the resolution decides: a constant alpha of one is still
        /// proven, and only an equation that genuinely samples a texture becomes
        /// Unknown. Missing knowledge invalidates only what depends on it.
        /// </para>
        /// <para>
        /// A non-finite position is the one asymmetry: TriangleAlphaInput has no
        /// "positions unavailable" form and AlphaResolution does not expose
        /// whether it is uniform, so such a triangle is Unknown even under a
        /// resolution that would never have looked at geometry. That is a false
        /// negative on malformed data, which is the acceptable direction.
        /// </para>
        /// </summary>
        private static TriangleAlphaOutcome[] Classify(
            int[] indices,
            Vector3[] positions,
            Vector2[] uv,
            AlphaResolution resolution)
        {
            var outcomes = new TriangleAlphaOutcome[indices.Length / 3];
            if (!resolution.IsResolved)
            {
                for (var triangle = 0; triangle < outcomes.Length; triangle++)
                {
                    outcomes[triangle] = TriangleAlphaOutcome.Unknown;
                }

                return outcomes;
            }

            for (var triangle = 0; triangle < outcomes.Length; triangle++)
            {
                var a = indices[triangle * 3];
                var b = indices[triangle * 3 + 1];
                var c = indices[triangle * 3 + 2];

                if (!IsFinite(positions[a]) ||
                    !IsFinite(positions[b]) ||
                    !IsFinite(positions[c]))
                {
                    outcomes[triangle] = TriangleAlphaOutcome.Unknown;
                    continue;
                }

                var uvAvailable = uv != null &&
                                  IsFinite(uv[a]) &&
                                  IsFinite(uv[b]) &&
                                  IsFinite(uv[c]);

                outcomes[triangle] = resolution.Classify(
                    uvAvailable
                        ? TriangleAlphaInput.WithUv0(
                            positions[a], positions[b], positions[c],
                            uv[a], uv[b], uv[c])
                        : TriangleAlphaInput.MissingUv0(
                            positions[a], positions[b], positions[c]));
            }

            return outcomes;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
```

- [ ] **Step 4: Run the whole file's tests and verify they pass**

Run the EditMode suite filtered to `UnityRendererAlphaAnalysisTests`.
Expected: 20 passed, 0 failed.

- [ ] **Step 5: Checkpoint (no commit)**

```bash
git diff --check && git status --short
```

No new file was created in this task, so no new `.meta` should appear. Nothing staged.

---

### Task 5: The vertical-slice integration fixture

**Files:**
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Host/RendererAlphaAnalysisIntegrationTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `UnityRendererAlphaAnalysis.Analyze(Renderer, BaseMaterialSemanticsProvider)` (Task 4), `PoiyomiMaterialSemantics.InterpretVerifiedMaterial(Material, ColorSpace)`, the `PoiyomiSemanticTest.shader` stand-in, and the texture-import recipe from `AlphaEvidenceClassifierIntegrationTests`.
- Produces: nothing consumed by later tasks.

**Architectural question this task proves:** the milestone's primary success criterion —
that a real renderer, mesh, material slots, and texture asset drive real semantics, real
evidence, the real exact classifier, and the real planner to an immutable plan that
separates provably-opaque geometry from geometry that must be preserved.

**This is a composition test and it may pass on its first run.** The exact stand-in
material state it needs is written out below, read from repository source
(`PoiyomiMaterialSemantics.InterpretAlpha`, `TryInterpretMainSample`,
`TryGetSupportedUvMapping`, `TryGetMainTextureSampling`, `FirstFailedZeroGate`,
`TryReadBinary`, `PoiyomiSemanticTest.shader`, `PoiyomiAlphaTests`). **Do not
underconfigure the fixture to manufacture a RED step** — fixture misconfiguration is not a
useful failing test. Record a first-run pass truthfully.

**The exact stand-in state required** (everything else is already correct at the shader's
declared default):

| Property | Shader default | Required | Action |
|---|---|---|---|
| `_AlphaForceOpaque` | `1` | `0` | `SetFloat("_AlphaForceOpaque", 0f)` |
| `_MainAlphaMaskMode` | `2` | `0` | `SetFloat("_MainAlphaMaskMode", 0f)` |
| `_Color.a` | `1` | exactly `1` | set explicitly — any value below one yields `TextureTimesConstant`, which the resolver answers `Uniform(MustRemainTransparent)` without reading a texel |
| `_MainTex` | `"white"`, unassigned | an imported texture with a resolvable asset identity | `SetTexture("_MainTex", texture)` |
| texture scale / offset | `(1,1)` / `(0,0)` | `(1,1)` / `(0,0)` | set explicitly |
| `_MainTexUV`, `_MainTexPan`, `_MainPixelMode`, `_MainTexStochastic`, `_MainIgnoreTexAlpha` | `0` / `(0,0,0,0)` / `0` / `0` / `0` | unchanged | none |
| the five `AlphaCoverageGates` and the 23 `AlphaFeatureGates` | all `0` | unchanged | none |

**Truthfulness requirement:** the fixture substitutes exactly one link — vendor shader
*attestation*. The semantics it uses are genuine `PoiyomiMaterialSemantics` interpreter
output over a real `Material`. The test class summary must say so. **Do not claim a vendor
frontend was dispatched.**

- [ ] **Step 1: Write the integration test**

```csharp
using System.Collections.Generic;
using System.IO;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// The renderer-to-plan vertical slice: a real Renderer, a real Mesh with
    /// two submeshes and two material slots, a real imported Texture2D, the real
    /// UnityAlphaFieldEvidence provider, the real AlphaSemanticsResolver, the
    /// real exact TriangleAlphaClassifier, and the real MeshSeparationPlanner.
    /// <para>
    /// Exactly one link is substituted: vendor shader source attestation. The
    /// public development project installs neither Poiyomi nor lilToon, so no
    /// material here can be attested. The semantics used are nonetheless
    /// genuine PoiyomiMaterialSemantics output, obtained over a real Material
    /// through that frontend's existing InterpretVerifiedMaterial seam. This
    /// test does not exercise, and does not claim to exercise, vendor frontend
    /// dispatch.
    /// </para>
    /// <para>
    /// The fixture is asymmetric on every axis that could silently compensate
    /// for a wiring error: the opaque-looking geometry sits on the
    /// <em>unsupported</em> slot, the two submeshes have different triangle
    /// counts, and the single non-opaque texel sits in a corner, so a swapped
    /// slot mapping, an off-by-one submesh index, or a flipped row order each
    /// change the expected result.
    /// </para>
    /// </summary>
    public sealed class RendererAlphaAnalysisIntegrationTests
    {
        private const string TempFolder = "Assets/AmuseTests_RendererIntegration";
        private const string FixtureShaderName =
            "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest";
        private const int Size = 4;

        private readonly List<Object> _transient = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_RendererIntegration");
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _transient)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _transient.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        private T Track<T>(T obj) where T : Object
        {
            _transient.Add(obj);
            return obj;
        }

        /// <summary>
        /// 4x4 RGBA32, uncompressed, no mips, Point/Clamp, with exactly one
        /// non-opaque texel at (0,0). Under Point/Clamp that texel owns
        /// UV [0, 0.25) x [0, 0.25). A uniform texture would short-circuit on
        /// IsFullyOpaque before geometry was examined and would pass even if the
        /// wiring were wrong.
        /// </summary>
        private static Texture2D ImportTexture(string name, bool readable)
        {
            var path = TempFolder + "/" + name + ".png";
            var staging = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(64, 32, 16, 255);
            }

            pixels[0] = new Color32(64, 32, 16, 128);
            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.isReadable = readable;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"'{path}' must import.");
            return loaded;
        }

        /// <summary>
        /// The exact state PoiyomiMaterialSemantics.InterpretAlpha requires to
        /// reach ScalarSemanticValue.Texture(sample, Alpha): off the forced
        /// path, mask mode off, full colour alpha, and an assigned _MainTex with
        /// an identity-resolvable asset. Every other gate is already zero at the
        /// stand-in shader's declared default.
        /// </summary>
        private Material NewSampledAlphaMaterial(Texture2D mainTex)
        {
            var shader = Shader.Find(FixtureShaderName);
            Assert.That(
                shader, Is.Not.Null, $"'{FixtureShaderName}' must import.");
            var material = Track(new Material(shader));
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetTexture("_MainTex", mainTex);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            return material;
        }

        /// <summary>
        /// Genuine Poiyomi interpreter output for the nominated material, and
        /// all-Unknown for anything else. Only attestation is bypassed.
        /// </summary>
        private static BaseMaterialSemanticsProvider VerifiedSemanticsFor(
            params Material[] supported)
        {
            return material =>
            {
                foreach (var candidate in supported)
                {
                    if (ReferenceEquals(material, candidate))
                    {
                        return PoiyomiMaterialSemantics
                            .InterpretVerifiedMaterial(material, ColorSpace.Linear)
                            .Semantics;
                    }
                }

                return UnityMaterialSemantics.AllUnknown();
            };
        }

        /// <summary>
        /// Submesh 0: two triangles whose UVs lie in the fully opaque region,
        /// bound to the unsupported slot. Submesh 1: three triangles, two in the
        /// opaque region and one wholly inside the non-opaque texel, bound to
        /// the supported slot.
        /// </summary>
        private Mesh BuildFixtureMesh()
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),   // 0: opaque region
                new Vector3(1f, 0f, 0f),   // 1
                new Vector3(1f, 1f, 0f),   // 2
                new Vector3(0f, 1f, 0f),   // 3
                new Vector3(2f, 0f, 0f),   // 4
                new Vector3(2f, 1f, 0f),   // 5
                new Vector3(3f, 0f, 0f),   // 6: non-opaque texel
                new Vector3(3f, 1f, 0f),   // 7
                new Vector3(4f, 0f, 0f)    // 8
            };
            mesh.uv = new[]
            {
                new Vector2(0.55f, 0.55f),
                new Vector2(0.9f, 0.55f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.55f, 0.9f),
                new Vector2(0.6f, 0.5f),
                new Vector2(0.85f, 0.8f),
                new Vector2(0.01f, 0.01f),
                new Vector2(0.2f, 0.01f),
                new Vector2(0.01f, 0.2f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.SetTriangles(new[] { 0, 4, 5, 6, 7, 8, 1, 2, 4 }, 1);
            return mesh;
        }

        private SkinnedMeshRenderer NewRenderer(Mesh mesh, params Material[] slots)
        {
            var gameObject = Track(new GameObject("amuse-integration"));
            var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = slots;
            return renderer;
        }

        [Test]
        public void RendererToPlanSeparatesProvenOpaqueGeometryFromPreservedGeometry()
        {
            var texture = ImportTexture("mixed", readable: true);
            var unsupported = Track(new Material(Shader.Find("Unlit/Color")));
            var supported = NewSampledAlphaMaterial(texture);
            var renderer = NewRenderer(
                BuildFixtureMesh(), unsupported, supported);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, VerifiedSemanticsFor(supported));

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.None),
                "The fixture renderer must be fully supported.");
            Assert.That(result.Plan, Is.Not.Null);

            // Slot 0 carries the opaque-looking geometry but no provable
            // semantics; a swapped slot mapping would turn this into a Split.
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown));
            Assert.That(
                result.Plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
            Assert.That(
                result.Plan.Submeshes[0].OpaqueTriangleOrdinals, Is.Empty);

            // Slot 1 is the proven one, and it must split.
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                result.Plan.Submeshes[1].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Split));
            Assert.That(
                result.Plan.Submeshes[1].OpaqueTriangleOrdinals,
                Is.EqualTo(new[] { 0, 2 }),
                "Triangles 0 and 2 lie wholly in the opaque region.");
            Assert.That(
                result.Plan.Submeshes[1].TransparentTriangleOrdinals,
                Is.EqualTo(new[] { 1 }),
                "Triangle 1 lies wholly inside the one non-opaque texel; " +
                "proving it opaque would be a false positive.");

            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(2));
            Assert.That(result.Plan.TransparentTriangleCount, Is.EqualTo(3));
            Assert.That(result.Plan.HasAnyOpaqueCandidates, Is.True);
            Assert.That(result.Plan.RequiresAnySplit, Is.True);
        }

        /// <summary>
        /// The other half of the UV dependency rule: this equation genuinely
        /// samples a texture, so without UV0 the proof cannot be completed. The
        /// unit suite proves the constant-alpha half, where UV0 is irrelevant;
        /// neither half alone establishes the rule.
        /// <para>
        /// Note precisely what does and does not happen. The
        /// <c>AlphaResolution</c> stays <em>resolved</em> and
        /// <c>SubmeshAlphaAnalysis.Failure</c> stays <c>None</c>: the material
        /// and its evidence were proven perfectly well. Each triangle is then
        /// classified through <c>MissingUv0</c>, and the sampled classifier
        /// returns <c>TriangleAlphaOutcome.Unknown</c> because it has no
        /// coordinates to evaluate its predicate at. This is triangle-local
        /// uncertainty, not a resolution refusal, and the assertions below pin
        /// exactly that distinction.
        /// </para>
        /// </summary>
        [Test]
        public void MissingUv0MakesAUvDependentSampledProofUnknown()
        {
            var texture = ImportTexture("sampled_no_uv", readable: true);
            var supported = NewSampledAlphaMaterial(texture);
            var mesh = BuildFixtureMesh();
            mesh.uv = null;
            var renderer = NewRenderer(mesh, supported, supported);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, VerifiedSemanticsFor(supported));

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.None),
                "The material still resolves; only the geometry lacks UV0.");
            Assert.That(
                result.Plan.OpaqueTriangleCount,
                Is.EqualTo(0),
                "A sampled alpha cannot be proven without the UVs it samples " +
                "at; the proof is blocked triangle-locally, not by a refusal.");
            Assert.That(
                result.Plan.Submeshes[1].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
        }

        /// <summary>
        /// The same, at triangle scope: one non-finite UV removes UV0 for that
        /// triangle alone, and under a sampled equation only that triangle
        /// becomes Unknown.
        /// </summary>
        [Test]
        public void ANonFiniteUvMakesOnlyItsOwnSampledTriangleUnknown()
        {
            var texture = ImportTexture("sampled_nan_uv", readable: true);
            var supported = NewSampledAlphaMaterial(texture);
            var mesh = BuildFixtureMesh();
            var uv = mesh.uv;
            uv[5] = new Vector2(float.NaN, 0f);   // used only by submesh 1, triangle 0
            mesh.uv = uv;
            var renderer = NewRenderer(mesh, supported, supported);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, VerifiedSemanticsFor(supported));

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Plan.Submeshes[1].OpaqueTriangleOrdinals,
                Is.EqualTo(new[] { 2 }),
                "Triangle 0 lost its UVs; triangle 2 kept them and stays proven.");
        }
    }
}
```

- [ ] **Step 2: Run the tests**

Run the EditMode suite filtered to `RendererAlphaAnalysisIntegrationTests`.
Expected: 3 passed, 0 failed, with the exact ordinals asserted above.

**Record truthfully whether this passed on the first run.** It is a composition test over
behaviour Tasks 2–4 already built, so a first-run pass is the expected and correct outcome.

If it fails, diagnose before changing anything. **If making it pass appears to require
changing any production file outside `Editor/Host/`, stop and escalate** — that would mean
the composition genuinely does not fit, which is an architecture question, not a fixture
question.

- [ ] **Step 3: Metadata checkpoint**

Run the Metadata checkpoint for `RendererAlphaAnalysisIntegrationTests.cs`.

- [ ] **Step 4: Checkpoint (no commit)**

```bash
git diff --check && git status --short
```

Nothing staged.

---

### Task 6: Non-readable texture characterization and source immutability

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/RendererAlphaAnalysisIntegrationTests.cs`

**Interfaces:**
- Consumes: the Task 5 fixture helpers.
- Produces: the evidence that bears on whether `feat/non-readable-alpha-evidence` should be the next branch.

**Architectural question this task answers:** whether a non-readable alpha texture poisons
only its own submesh, and whether an otherwise-useful partial separation plan survives it.

`UnityAlphaFieldEvidence` is **not** modified. No importer state is toggled to work around
the refusal; the fixture deliberately *creates* the refusal.

- [ ] **Step 1: Write the characterization and immutability tests**

Add to `RendererAlphaAnalysisIntegrationTests`:

```csharp
        /// <summary>
        /// The characterization the next branch's prioritization depends on. One
        /// slot's alpha texture is non-readable and the other's is not. The
        /// refusal must stay inside the slot that owns it, and a useful partial
        /// plan must survive.
        /// </summary>
        [Test]
        public void ANonReadableAlphaTextureRefusesOnlyItsOwnSubmesh()
        {
            var nonReadable = ImportTexture("non_readable", readable: false);
            var readable = ImportTexture("readable", readable: true);
            var blocked = NewSampledAlphaMaterial(nonReadable);
            var proven = NewSampledAlphaMaterial(readable);
            var renderer = NewRenderer(BuildFixtureMesh(), blocked, proven);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer, VerifiedSemanticsFor(blocked, proven));

            // Where the refusal emerges, and its shape.
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.None),
                "A non-readable texture must not refuse the whole renderer.");
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.MissingTextureEvidence));

            // Its blast radius: that submesh only.
            Assert.That(
                result.Plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
            Assert.That(
                result.Plan.Submeshes[0].OpaqueTriangleOrdinals, Is.Empty);

            // What survives it.
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                result.Plan.Submeshes[1].OpaqueTriangleOrdinals,
                Is.EqualTo(new[] { 0, 2 }));
            Assert.That(result.Plan.HasAnyOpaqueCandidates, Is.True);
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(2));
        }

        /// <summary>
        /// Analysis is observational. Every source object must be structurally
        /// identical afterwards, and the imported texture asset must not have
        /// been re-imported or rewritten.
        /// </summary>
        [Test]
        public void AnalysisLeavesEverySourceObjectUnchanged()
        {
            var texture = ImportTexture("immutable", readable: true);
            var texturePath = AssetDatabase.GetAssetPath(texture);
            var supported = NewSampledAlphaMaterial(texture);
            var mesh = BuildFixtureMesh();
            var renderer = NewRenderer(mesh, supported, supported);

            var beforeAssetHash =
                AssetDatabase.GetAssetDependencyHash(texturePath);
            var beforeReadable = texture.isReadable;
            var beforePixels = texture.GetPixels32(0);
            var beforeVertices = mesh.vertices;
            var beforeUv = mesh.uv;
            var beforeSubmesh0 = mesh.GetIndices(0);
            var beforeSubmesh1 = mesh.GetIndices(1);
            var beforeVertexCount = mesh.vertexCount;
            var beforeSubMeshCount = mesh.subMeshCount;
            var beforeMaterials = renderer.sharedMaterials;
            var beforeSharedMesh = renderer.sharedMesh;
            var beforeMainTex = supported.GetTexture("_MainTex");
            var beforeColor = supported.GetColor("_Color");

            UnityRendererAlphaAnalysis.Analyze(
                renderer, VerifiedSemanticsFor(supported));

            Assert.That(
                AssetDatabase.GetAssetDependencyHash(texturePath),
                Is.EqualTo(beforeAssetHash),
                "Analysis must not re-import or rewrite the texture asset.");
            Assert.That(texture.isReadable, Is.EqualTo(beforeReadable));
            Assert.That(texture.GetPixels32(0), Is.EqualTo(beforePixels));
            Assert.That(mesh.vertexCount, Is.EqualTo(beforeVertexCount));
            Assert.That(mesh.subMeshCount, Is.EqualTo(beforeSubMeshCount));
            Assert.That(mesh.vertices, Is.EqualTo(beforeVertices));
            Assert.That(mesh.uv, Is.EqualTo(beforeUv));
            Assert.That(mesh.GetIndices(0), Is.EqualTo(beforeSubmesh0));
            Assert.That(mesh.GetIndices(1), Is.EqualTo(beforeSubmesh1));
            Assert.That(renderer.sharedMaterials, Is.EqualTo(beforeMaterials));
            Assert.That(
                renderer.sharedMesh, Is.SameAs(beforeSharedMesh),
                "Reading a mesh must not have instantiated a copy.");
            Assert.That(
                renderer.HasPropertyBlock(),
                Is.False,
                "Analysis must not have attached a property block.");
            Assert.That(
                supported.GetTexture("_MainTex"), Is.SameAs(beforeMainTex));
            Assert.That(supported.GetColor("_Color"), Is.EqualTo(beforeColor));
        }
```

- [ ] **Step 2: Run the whole integration file**

Run the EditMode suite filtered to `RendererAlphaAnalysisIntegrationTests`.
Expected: 5 passed, 0 failed.

If `ANonReadableAlphaTextureRefusesOnlyItsOwnSubmesh` fails because the whole renderer
refused, that is a real defect in refusal granularity — fix the production scoping, not the
assertion. If slot 0 reports `SemanticsUnknown` rather than `MissingTextureEvidence`, the
stand-in material's alpha state is wrong, not the production code.

- [ ] **Step 3: Record the characterization in the design document**

Fill the design's "Questions 10 & 28" section with the observed values, and confirm in
"Criteria for choosing the next branch" that the public suite established the mechanism
only, and that frequency on real content is still unmeasured.

- [ ] **Step 4: Checkpoint (no commit)**

```bash
git diff --check && git status --short
```

Nothing staged.

---

### Task 7: Full validation and scope review

**Files:** possibly `docs/superpowers/specs/2026-08-20-end-to-end-alpha-analysis-design.md`.

**Interfaces:**
- Consumes: every preceding task.
- Produces: the completion report.

- [ ] **Step 1: Run the complete EditMode suite**

Confirm the Unity instance identity again (normalized `Application.dataPath` equals
`<repo-root>/Assets`), then run the whole `Alrauna.Amuse.Tests.Editor` assembly.

Expected: zero failures, and a total equal to the Task 0 baseline plus the tests added
here. Record the observed numbers. If no instance is reachable, record the validation as
blocked — **do not infer a result.**

- [ ] **Step 2: Inspect the working tree**

```bash
git diff --check && git diff --stat && git status --short
```

Expected: the two documents, the two production files, the four test files, and the six new
`.meta` files — **all untracked, nothing staged, nothing committed.** No `.meta` shows as
modified or deleted. Nothing under `Library/`, `Temp/`, `Logs/`, or `UserSettings/`.

- [ ] **Step 3: Handle known macOS package churn, if it appears**

If `Packages/manifest.json` or `Packages/packages-lock.json` changed, inspect the diffs in
full. If and only if they contain nothing but the previously characterized machine-generated
entries — `com.unity.toolchain.macos-arm64-linux-x86_64`, `com.unity.sysroot`,
`com.unity.sysroot.linux-x86_64` — restore just those two files:

```bash
git checkout HEAD -- Packages/manifest.json Packages/packages-lock.json
```

Report the occurrence. **If anything else changed in them, stop.**

- [ ] **Step 4: Review against the design, line by line**

Walk the design's 31 answered questions and confirm each is honoured by the code as
written. Confirm specifically that:

- no stop condition was crossed;
- no test claims a vendor frontend was exercised;
- production contains no write, no `BakeMesh`, no `materials`, no `MeshFilter.mesh`, no
  `GetPropertyBlock`, no global or static cache;
- **no `try`/`catch` was added around any Unity mesh read, and `catch (Exception)` appears
  nowhere**;
- no new architecture guard duplicates the existing one in `UnityAlphaFieldEvidenceTests`;
- unavailable UV0 is passed through as `MissingUv0` rather than pre-empted as `Unknown`.

- [ ] **Step 5: Write the completion report**

State: what changed; which tests ran and their observed results, including which passed on
first run as characterization or composition rather than through a RED/GREEN cycle; what
validation was skipped and why; remaining risks and unsupported cases; whether the private
Unity MCP testbed was used (it must not have been); and the evidence bearing on the next
branch choice — without making that choice.

**The plan ends here, with everything unstaged and uncommitted. Git finalization — staging,
commit, push, review, PR, merge — is a separate step requiring its own explicit
authorization.**

---

## Self-review notes

- **Spec coverage:** every design section maps to a task. Questions 1–2 → Tasks 3–4;
  Q3–Q4, Q9–Q10, Q15 → Tasks 3–4; Q6 → Task 1; Q5, Q16–Q17 → Task 2; Q11–Q14 → Task 4 and
  Task 5; Q18–Q19 → Task 5; Q20–Q21 → Task 4; Q7, Q22–Q24 → Tasks 4, 6; Q8, Q25–Q26 →
  Task 3; Q27 → Task 3 (the `SubmeshAlphaAnalysis` summary states the limit); Q10, Q28 →
  Task 6; Q29 → Task 6; Q30–Q31 → Task 7. The property-block guard → Task 3.
- **Pass-immediately work, declared honestly:** Task 1 (characterization of Unity itself),
  Task 5 (composition over behaviour Tasks 2–4 already built), and within Task 3 the
  topology and unsupported-type refusals. Genuine RED/GREEN applies to Task 2 and to the
  Task 3 → Task 4 staging, where the `NotImplementedException` makes the gap explicit
  rather than disguising it as a refusal.
- **Type consistency:** `RendererAnalysisRefusal`, `MaterialPropertyOverridesPresent`,
  `SubmeshAlphaAnalysis`, `RendererAlphaAnalysis`, `BaseMaterialSemanticsProvider`,
  `UnityMaterialSemantics`, `AnalyzeBaseMaterial`, `AllUnknown`, `ImportTexture`,
  `NewSampledAlphaMaterial`, `VerifiedSemanticsFor`, `BuildFixtureMesh`, and `NewRenderer`
  are spelled identically everywhere they appear.
- **No git mutation anywhere:** every task ends in a read-only checkpoint. There is no
  `git add`, no `git commit`, no `git push`, and no PR step in this plan.
