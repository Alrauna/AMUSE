# Material Slot Mapping Report Counts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the `amuse.renderer.UnprovenMaterialSlotMapping` report state how many material
slots the mesh supports and how many the renderer carries, using the exact counts the refusal
was computed from, with the counts riding the refusal rather than a live re-read.

**Architecture:** The mapping refusal currently travels as a bare enum and the report emitter
passes no arguments. The counts gain a ride on the refused extraction record and on a new
`HostStructuralRefusalFor` overload, and the emitter renders them through a private noun-phrase
helper that owns pluralization and the unknown sentinel. Only the description string gains
placeholders; the title and hint keep their text.

**Tech Stack:** Unity 2022.3.22f1 editor assembly (`Alrauna.Amuse.Editor`), NUnit via Unity Test
Framework, EditMode only. Tests run in the dev editor instance through the Test Runner; there is
no CLI test runner. Reports are verified through NDMF's `ErrorReport.CaptureErrors`.

**Spec:** `docs/superpowers/specs/2026-09-28-slot-mapping-report-counts-design.md`

## Global Constraints

- Tests never run in the Census Lab project. Every test run happens in the dev editor instance
  against this repository, pinned by exact identity before any Unity MCP call
  (`Application.dataPath` must resolve to this repository's `Assets` folder).
- Never stage or commit without explicit authorization at execution time. Each commit step
  below carries the exact paths and message for when authorization is given.
- No new refusal enum values, no new report keys, no new emission sites. The title and hint
  strings keep their current text. Only the `UnprovenMaterialSlotMapping:description` string
  changes.
- User-visible report strings follow the house string style: short sentences, active voice, one
  idea per sentence, no contractions, no semicolons.
- XML doc comments on load-bearing rules state why, not what.
- A filtered test run that reports 0 tests is a failure — except where a step explicitly expects
  a compile failure as the recorded RED; record the compiler message as the observed evidence.
- Record observed pass and fail counts for every run.
- After the last code step, run `git diff --check` and sweep the changed files for identifiers:
  an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit
  ports, and private asset names learned in the session. Every hit is a defect.

---

### Task 1: The report side — description placeholders and the count phrase emitter

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` (the `UnprovenMaterialSlotMapping:description` entry, lines 61-64)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs` (the `RendererRefusal` helper, lines 308-318)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsRendererTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs` (one new test inside the existing class)

**Interfaces:**
- Consumes: the existing `AmuseReports.Localizer`, `AmuseReportStrings.RendererKey`, and the
  NDMF `ErrorReport.ReportError(Localizer, severity, key, params object[] args)` argument path.
- Produces: `AmuseReports.RendererRefusal(Renderer renderer, RendererAnalysisRefusal cause,
  int meshSubMeshCount = -1, int materialSlotCount = -1)`. Task 3's call sites rely on this
  exact signature. The description template consumes exactly two phrase arguments, in mesh-then-
  renderer order.

- [ ] **Step 1: Write the failing string test**

In `AmuseReportStringsTests.cs`, after the `EveryRendererRefusalCauseHasPlainEnglishStrings`
test, add:

```csharp
        [Test]
        public void MappingRefusalDescriptionFormatsBothSlotCounts()
        {
            var description = string.Format(
                AmuseReportStrings.Get(
                    "amuse.renderer.UnprovenMaterialSlotMapping:description"),
                "3 material slots",
                "1 material slot");

            // Falsifier: an implementation that rewords the sentences
            // without placeholders renders the old text, states no
            // count, and leaves the reader unable to tell which side
            // is wrong.
            Assert.That(
                description, Does.Contain("mesh supports 3 material slots"));
            Assert.That(
                description, Does.Contain("renderer has 1 material slot"));
        }
```

- [ ] **Step 2: Refresh Unity and run the filter to verify the RED**

Refresh the dev editor instance so the edited test file compiles, then run the EditMode filter
`AmuseReportStringsTests` (Unity MCP: `refresh_unity`, then `run_tests` in EditMode mode with
that filter, polled through `get_test_job`).

Expected: 8 tests total (7 pre-existing, 1 new). Exactly the new test fails on both assertions,
because the current description states no numbers. Record the observed counts and failure text.
A run that reports 0 tests is a failure, and a green run here means the RED was not observed.

- [ ] **Step 3: Change the description template**

In `AmuseReportStrings.cs`, replace lines 61-64:

```csharp
            ["amuse.renderer.UnprovenMaterialSlotMapping:description"] =
                "The mesh supports {0}. The renderer has {1}. AMUSE " +
                "only works when the numbers match.",
```

The title entry and the hint entry keep their current text. Nothing else in the file changes.

- [ ] **Step 4: Run the filter to verify the GREEN**

Refresh and run the EditMode filter `AmuseReportStringsTests` again.

Expected: 8 tests, all passing. Record the observed counts.

- [ ] **Step 5: Write the emitter tests**

Create `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsRendererTests.cs`:

```csharp
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The renderer-scoped refusal reports. The mapping refusal must name
    /// both sides of the mismatch with the counts the refusal used, so
    /// the tests capture through
    /// <see cref="ErrorReport.CaptureErrors(Action)"/> and read the
    /// rendered message.
    /// </summary>
    public sealed class AmuseReportsRendererTests
    {
        private GameObject _root;
        private MeshRenderer _renderer;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("AMUSE renderer report");
            _renderer = _root.AddComponent<MeshRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void MappingRefusalReportCarriesBothSlotCounts()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.RendererRefusal(
                    _renderer,
                    RendererAnalysisRefusal.UnprovenMaterialSlotMapping,
                    3,
                    1));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // Falsifier: an emitter that swaps the arguments reports the
            // mesh's count on the renderer and the renderer's count on
            // the mesh, so the reader fixes the wrong side.
            Assert.That(
                message, Does.Contain("mesh supports 3 material slots"));
            Assert.That(
                message, Does.Contain("renderer has 1 material slot"));
        }

        [Test]
        public void MappingRefusalWithUnknownCountStatesUnknownNotMinusOne()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.RendererRefusal(
                    _renderer,
                    RendererAnalysisRefusal.UnprovenMaterialSlotMapping,
                    2,
                    -1));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // Falsifier: an emitter that formats the raw sentinel prints
            // a negative count, and minus one is not a count a reader
            // can act on.
            Assert.That(
                message, Does.Contain("mesh supports 2 material slots"));
            Assert.That(message, Does.Contain(
                "an unknown number of material slots"));
            Assert.That(message, Does.Not.Contain("-1"));
        }
    }
}
```

- [ ] **Step 6: Run the filter and record the compile RED**

Refresh, then run the EditMode filter `AmuseReportsRendererTests`.

Expected: the assembly fails to compile, because no `RendererRefusal` overload takes four
arguments, so 0 tests run. This compile failure is the recorded RED for the emitter contract:
the report cannot yet carry counts. Record the exact compiler message. (This is the one
deliberate exception to the zero-tests-is-a-failure rule, and it is expected only in this step.)

- [ ] **Step 7: Add the emitter signature, the phrase helper, and the arguments**

In `AmuseReports.cs`, replace the `RendererRefusal` helper (lines 308-318) with:

```csharp
        /// <summary>
        /// One Information entry per refused renderer. The mapping refusal
        /// carries the two slot counts the refusal was computed from, so
        /// the reader can tell which side to change. The counts ride the
        /// refusal because the captured slot basis, not a live re-read,
        /// is the truth the decision used. Causes whose templates name no
        /// counts ignore the two arguments.
        /// </summary>
        internal static void RendererRefusal(
            Renderer renderer,
            RendererAnalysisRefusal cause,
            int meshSubMeshCount = -1,
            int materialSlotCount = -1)
        {
            using (ErrorReport.WithContextObject(renderer))
            {
                ErrorReport.ReportError(
                    Localizer,
                    ErrorSeverity.Information,
                    AmuseReportStrings.RendererKey(cause),
                    SlotCountPhrase(meshSubMeshCount),
                    SlotCountPhrase(materialSlotCount));
            }
        }

        /// <summary>
        /// A slot count renders as a noun phrase, so the sentence stays
        /// grammatical for zero, one, and many, and an unreadable count
        /// never prints as a number.
        /// </summary>
        private static string SlotCountPhrase(int count)
        {
            if (count < 0)
            {
                return "an unknown number of material slots";
            }

            return count == 1
                ? "1 material slot"
                : count + " material slots";
        }
```

Nothing else in the file changes. The other report helpers stay untouched.

- [ ] **Step 8: Run the filter to verify the GREEN**

Refresh and run the EditMode filter `AmuseReportsRendererTests`.

Expected: 2 tests, both passing. Record the observed counts.

- [ ] **Step 9: Run the full assemblies**

Refresh, then run the full `Alrauna.Amuse.Tests.Editor` assembly in EditMode mode, then the
full `Alrauna.Amuse.Research.Tests.Editor` assembly, which consumes the same internals through
`InternalsVisibleTo`.

Expected: both assemblies fully green. No test pins the old description text. The barrier
report path now renders "an unknown number of material slots" for mapping refusals until Task 3
wires the counts; no existing test asserts against that wording. Record the observed counts for
each assembly. Any failure stops the task: report it instead of widening the change.

- [ ] **Step 10: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsRendererTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsRendererTests.cs.meta
git commit -m "feat: state both slot counts in the mapping refusal report"
```

Unity generates the new test's `.meta` on first refresh. It is tracked in the
same unit as the test file; as executed, it landed in a follow-up chore
commit (`chore: track the renderer report test meta file`) — either shape is
acceptable, an untracked `.meta` is not.

Only with explicit authorization at execution time.

---

### Task 2: The evidence side — counts on the refused extraction and the host overload

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaSnapshot.cs` (the `UnityRendererAlphaExtraction` class, lines 114-152)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs` (the mapping check in `Capture`, lines 358-361; the one-argument `HostStructuralRefusalFor`, lines 308-322)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs` (four new tests near the existing mapping tests around lines 288-316)

**Interfaces:**
- Consumes: the existing fixture helpers in the test class — `NewSkinned(Mesh, params Material[])`,
  `NewMaterial()`, `Quad()`, `TwoSubmeshMesh()`, `Track()` — and the existing
  `UnityRendererAlphaAnalysis.Capture(Renderer)` overload.
- Produces: `UnityRendererAlphaExtraction.MeshSubMeshCount` and
  `UnityRendererAlphaExtraction.MaterialSlotCount` (both `internal int`, minus one when not
  recorded), the factory `Refused(RendererAnalysisRefusal refusal, int meshSubMeshCount = -1,
  int materialSlotCount = -1)`, and `HostStructuralRefusalFor(Renderer renderer, out int
  meshSubMeshCount, out int materialSlotCount)`. Task 3's call sites rely on these exact names.

- [ ] **Step 1: Write the failing evidence tests**

In `UnityRendererAlphaAnalysisTests.cs`, after the `FewerMaterialsThanSubmeshesRefusesTheWholeRenderer`
test, add:

```csharp
        [Test]
        public void MappingRefusalExtractionCarriesBothSlotCounts()
        {
            var renderer = NewSkinned(TwoSubmeshMesh(), NewMaterial());

            var extraction = UnityRendererAlphaAnalysis.Capture(renderer);

            Assert.That(
                extraction.Refusal,
                Is.EqualTo(
                    RendererAnalysisRefusal.UnprovenMaterialSlotMapping));
            // Falsifier: an extraction that records one count but not
            // the other, or the two in swapped order, renders a report
            // that names the wrong side.
            Assert.That(extraction.MeshSubMeshCount, Is.EqualTo(2));
            Assert.That(extraction.MaterialSlotCount, Is.EqualTo(1));
        }

        [Test]
        public void SurplusSlotRefusalExtractionCarriesBothSlotCounts()
        {
            var renderer = NewSkinned(
                Quad(), NewMaterial(), NewMaterial());

            var extraction = UnityRendererAlphaAnalysis.Capture(renderer);

            Assert.That(
                extraction.Refusal,
                Is.EqualTo(
                    RendererAnalysisRefusal.UnprovenMaterialSlotMapping));
            Assert.That(extraction.MeshSubMeshCount, Is.EqualTo(1));
            Assert.That(extraction.MaterialSlotCount, Is.EqualTo(2));
        }

        [Test]
        public void HostRefusalOverloadReportsBothSlotCounts()
        {
            var renderer = NewSkinned(TwoSubmeshMesh(), NewMaterial());

            var refusal = UnityRendererAlphaAnalysis
                .HostStructuralRefusalFor(
                    renderer,
                    out var meshSubMeshCount,
                    out var materialSlotCount);

            Assert.That(
                refusal,
                Is.EqualTo(
                    RendererAnalysisRefusal.UnprovenMaterialSlotMapping));
            Assert.That(meshSubMeshCount, Is.EqualTo(2));
            Assert.That(materialSlotCount, Is.EqualTo(1));
        }

        [Test]
        public void NonMappingExtractionLeavesSlotCountsUnrecorded()
        {
            var renderer = NewSkinned(null, NewMaterial());

            var extraction = UnityRendererAlphaAnalysis.Capture(renderer);

            Assert.That(
                extraction.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.MissingMesh));
            // Guard: a refusal that is not about the slot mapping must
            // not grow counts it never used.
            Assert.That(extraction.MeshSubMeshCount, Is.EqualTo(-1));
            Assert.That(extraction.MaterialSlotCount, Is.EqualTo(-1));
        }
```

- [ ] **Step 2: Run the filter and record the compile RED**

Refresh, then run the EditMode filter `UnityRendererAlphaAnalysisTests`.

Expected: the assembly fails to compile, because `UnityRendererAlphaExtraction` has no
`MeshSubMeshCount` or `MaterialSlotCount` and `HostStructuralRefusalFor` has no three-argument
overload. 0 tests run. This compile failure is the recorded RED; record the exact compiler
message.

- [ ] **Step 3: Add the counts to the extraction record**

In `UnityRendererAlphaSnapshot.cs`, replace the `UnityRendererAlphaExtraction` class
(lines 114-152) with:

```csharp
    internal sealed class UnityRendererAlphaExtraction
    {
        internal RendererAnalysisRefusal Refusal { get; }
        internal UnityRendererAlphaSnapshot Snapshot { get; }
        internal UnityRendererMutationTarget MutationTarget { get; }

        /// <summary>
        /// The slot facts behind a mapping refusal: the submesh count the
        /// mesh supports and the material slot count the refusal compared
        /// it against. The report renders these exact numbers, so they
        /// ride the refusal instead of a live re-read, which could
        /// disagree with the decision. Minus one means not recorded.
        /// </summary>
        internal int MeshSubMeshCount { get; }
        internal int MaterialSlotCount { get; }

        private UnityRendererAlphaExtraction(
            RendererAnalysisRefusal refusal,
            UnityRendererAlphaSnapshot snapshot,
            UnityRendererMutationTarget mutationTarget,
            int meshSubMeshCount,
            int materialSlotCount)
        {
            Refusal = refusal;
            Snapshot = snapshot;
            MutationTarget = mutationTarget;
            MeshSubMeshCount = meshSubMeshCount;
            MaterialSlotCount = materialSlotCount;
        }

        internal static UnityRendererAlphaExtraction Refused(
            RendererAnalysisRefusal refusal,
            int meshSubMeshCount = -1,
            int materialSlotCount = -1)
        {
            return new UnityRendererAlphaExtraction(
                refusal, null, null, meshSubMeshCount, materialSlotCount);
        }

        internal static UnityRendererAlphaExtraction Accepted(
            UnityRendererAlphaSnapshot snapshot,
            UnityRendererMutationTarget mutationTarget)
        {
            return new UnityRendererAlphaExtraction(
                RendererAnalysisRefusal.None,
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)),
                mutationTarget ?? throw new ArgumentNullException(
                    nameof(mutationTarget)),
                -1,
                -1);
        }
    }
```

- [ ] **Step 4: Pass the counts at the mapping check**

In `UnityRendererAlphaAnalysis.cs`, replace the mapping check inside `Capture` (lines 358-361):

```csharp
            structural = MaterialSlotMappingRefusalFor(mesh, materialSlotCount);
            if (structural != RendererAnalysisRefusal.None)
                return UnityRendererAlphaExtraction.Refused(
                    structural, mesh.subMeshCount, materialSlotCount);
```

Every other `Refused` call site keeps its current single-argument form, so those refusals stay
unrecorded.

- [ ] **Step 5: Add the host overload**

In `UnityRendererAlphaAnalysis.cs`, replace the one-argument `HostStructuralRefusalFor`
(lines 308-322) with:

```csharp
        internal static RendererAnalysisRefusal HostStructuralRefusalFor(
            Renderer renderer)
        {
            return HostStructuralRefusalFor(renderer, out _, out _);
        }

        /// <summary>
        /// The same host facts, plus the two slot counts a mapping
        /// refusal report must carry. The counts stay minus one unless
        /// the mapping check itself ran, so a report never shows a count
        /// that no decision used.
        /// </summary>
        internal static RendererAnalysisRefusal HostStructuralRefusalFor(
            Renderer renderer,
            out int meshSubMeshCount,
            out int materialSlotCount)
        {
            var refusal = HostStructuralRefusalFor(renderer, out var mesh);
            meshSubMeshCount = -1;
            materialSlotCount = -1;
            if (refusal != RendererAnalysisRefusal.None)
            {
                return refusal;
            }

            meshSubMeshCount = mesh.subMeshCount;
            var materials = renderer.sharedMaterials;
            materialSlotCount = materials == null ? -1 : materials.Length;
            return MaterialSlotMappingRefusalFor(mesh, materialSlotCount);
        }
```

The private two-argument overload and `MaterialSlotMappingRefusalFor` stay untouched.

- [ ] **Step 6: Run the filter to verify the GREEN**

Refresh and run the EditMode filter `UnityRendererAlphaAnalysisTests`.

Expected: the four new tests pass, and every pre-existing test in the class passes. Record the
observed total, pass, and fail counts.

- [ ] **Step 7: Run the full assemblies**

Refresh, then run the full `Alrauna.Amuse.Tests.Editor` assembly, then the full
`Alrauna.Amuse.Research.Tests.Editor` assembly.

Expected: both fully green. The research census collector maps refusals itself and reads counts
from the mesh, so it consumes none of the new members. Record the observed counts. Any failure
stops the task: report it instead of widening the change.

- [ ] **Step 8: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaSnapshot.cs Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs
git commit -m "feat: carry slot counts on the mapping refusal evidence"
```

Only with explicit authorization at execution time.

---

### Task 3: The wiring — both barrier emission sites pass the counts

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (barrier site 1, lines 557-566; the capture block and shared refusal block, lines 680-685 and 746-750)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (one new test after `DifferentRendererRefusalReasonsRemainDistinct`, around line 3313)

**Interfaces:**
- Consumes: the Task 1 emitter signature and the Task 2 extraction properties and host overload.
- Produces: nothing new. The counts now reach the rendered report at both emission sites.

- [ ] **Step 1: Write the failing path test**

In `AmusePlatformFinishPluginTests.cs`, after the `DifferentRendererRefusalReasonsRemainDistinct`
test, add:

```csharp
        [Test]
        public void MappingRefusalReportNamesBothSlotCounts()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE mapping report fixture");
            FixtureAvatarIdentity.AttachVrcDescriptor(root);
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            FixtureProofScope.PinAllSizes(root);
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(3f, 0f, 0f),
                new Vector3(2f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.1f, 0.1f),
                new Vector2(0.9f, 0.1f),
                new Vector2(0.1f, 0.9f),
                new Vector2(0.2f, 0.2f),
                new Vector2(0.8f, 0.2f),
                new Vector2(0.2f, 0.8f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            var slot = new GameObject("two parts one slot");
            slot.transform.SetParent(root.transform, false);
            var skinned = slot.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            skinned.sharedMaterials = new[]
            {
                new Material(Shader.Find("Unlit/Color"))
            };
            var context = (BuildContext)null;

            try
            {
                context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);

                var reports = ErrorReport.CaptureErrors(
                    () => AmusePlatformFinishPass.Execute(
                        context, SupportedFacts()));

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(
                    amuse.RendererRefusalCount(
                        RendererAnalysisRefusal.UnprovenMaterialSlotMapping),
                    Is.EqualTo(1),
                    "the two-part mesh with one slot must refuse by name");

                string message = null;
                foreach (var report in reports)
                {
                    var candidate = report.TheError.ToMessage();
                    if (candidate.Contains("do not match"))
                    {
                        message = candidate;
                    }
                }

                Assert.That(message, Is.Not.Null,
                    "the mapping refusal report must be emitted");
                // Falsifier: an emission site that drops the counts, or
                // passes them in swapped order, leaves the report without
                // one side of the mismatch or names the wrong side.
                Assert.That(
                    message,
                    Does.Contain("mesh supports 2 material slots"));
                Assert.That(
                    message,
                    Does.Contain("renderer has 1 material slot"));
                Assert.That(message, Does.Not.Contain("unknown number"));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }
```

- [ ] **Step 2: Refresh Unity and run the filter to verify the RED**

Refresh, then run the EditMode filter `MappingRefusalReportNamesBothSlotCounts`.

Expected: 1 test, failing on the first message assertion: the refusal fires and the report is
emitted, but the rendered text still says "an unknown number of material slots" because no
emission site passes counts yet. Record the observed counts and failure text. A green run here
means the RED was not observed.

- [ ] **Step 3: Wire site 1 to the host overload**

In `AmusePlatformFinishPlugin.cs`, replace the barrier loop's structural check and its refusal
block (lines 557-566):

```csharp
                var refusal = UnityRendererAlphaAnalysis.HostStructuralRefusalFor(
                    renderer,
                    out var structuralMeshSubMeshCount,
                    out var structuralMaterialSlotCount);
                if (refusal != RendererAnalysisRefusal.None)
                {
                    state.RecordRendererRefusal(refusal);
                    AmuseReports.RendererRefusal(
                        renderer,
                        refusal,
                        structuralMeshSubMeshCount,
                        structuralMaterialSlotCount);
                    continue;
                }
```

- [ ] **Step 4: Wire site 2 through the extraction**

In the same loop body, declare two locals immediately before the capture block and read them
from the extraction (lines 680-685):

```csharp
                var extractionMeshSubMeshCount = -1;
                var extractionMaterialSlotCount = -1;
                if (refusal == RendererAnalysisRefusal.None)
                {
                    var extraction = UnityRendererAlphaAnalysis.CaptureGeometry(
                        renderer, resolved.CurrentMaterials);
                    refusal = extraction.Refusal;
                    extractionMeshSubMeshCount = extraction.MeshSubMeshCount;
                    extractionMaterialSlotCount = extraction.MaterialSlotCount;
                    if (refusal == RendererAnalysisRefusal.None)
                    {
```

The locals use distinct names from site 1's `out var` declarations, because both sit in the same
loop body. The runtime-state refusal path above leaves them at minus one, and the resolution
causes' templates name no counts.

Then replace the shared refusal block's report call (lines 747-749):

```csharp
                    state.RecordRendererRefusal(refusal);
                    AmuseReports.RendererRefusal(
                        renderer,
                        refusal,
                        extractionMeshSubMeshCount,
                        extractionMaterialSlotCount);
                    continue;
```

- [ ] **Step 5: Run the filter to verify the GREEN**

Refresh and run the EditMode filter `MappingRefusalReportNamesBothSlotCounts`.

Expected: 1 test, passing. Record the observed counts.

- [ ] **Step 6: Run the full assemblies**

Refresh, then run the full `Alrauna.Amuse.Tests.Editor` assembly, then the full
`Alrauna.Amuse.Research.Tests.Editor` assembly.

Expected: both fully green. Record the observed counts. Any failure stops the task: report it
instead of widening the change.

- [ ] **Step 7: Whitespace and identifier sweep**

Run `git diff --check`. Then sweep every changed file for identifiers: an at sign joined to a
hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and every private
asset name known to the session. Every hit is a defect; fix the hits before reporting.

- [ ] **Step 8: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "feat: report slot counts at the mapping refusal sites"
```

Only with explicit authorization at execution time.

---

## Validation summary and stop conditions

- RED evidence: Task 1 records one assert-failure RED (string test) and one compile-failure RED
  (emitter overload missing), each with the observed text. Task 2 records the compile-failure
  RED with its compiler message. Task 3 records a clean assert-failure RED against the interim
  "unknown" wording. Every RED run's observed counts are recorded.
- GREEN evidence: each task's filter passes, then both full assemblies pass, with recorded
  counts.
- Interim commits tell the truth: after Task 1 the report renders the unknown phrase until
  Task 3 lands. No intermediate commit throws or prints a negative count.
- Stop and report if: any guard test fails (the non-mapping counts guard or the unknown-count
  falsifier), any pre-existing test fails after a fix, the Research assembly fails, or the
  rendered wording differs from the spec's expected sentences. Do not fix unrelated failures
  inside this branch; report them.
- Census Lab validation after the merge is a separate, read-only console observation, not a
  gate for this branch.
