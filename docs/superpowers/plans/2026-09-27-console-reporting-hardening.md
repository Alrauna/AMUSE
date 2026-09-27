# Console Reporting Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make AMUSE's NDMF console reports truthful about build-copy objects: a summary whose untouched count includes fully slot-refused renderers, a last-build status that survives on every build path, a no-identity hint that names the upstream replacement mechanism, and material lines that name the authoring asset when a producer registered the replacement.

**Architecture:** Four independent report-path changes. The summary gains one computed invariant on `AmusePlatformFinishState` and one changed argument at the apply call site. The status store drops instance-ID keying for a single latest-build session slot. The no-identity hint is a string-table change. Material naming gains a `RegisteredSourceLookup` delegate threaded through the capture seam, backed by a read-only NDMF registry lookup in a small Build-module helper. No classifier, capture, or mutation change.

**Tech Stack:** Unity 2022.3.22f1 editor assembly (`Alrauna.Amuse.Editor`), NUnit via Unity Test Framework, EditMode only. Tests run in the dev editor instance through the Test Runner; there is no CLI test runner.

**Spec:** `docs/superpowers/specs/2026-09-27-console-reporting-hardening-design.md`

## Global Constraints

- Tests never run in the Census Lab project. Every test run happens in the dev editor instance against this repository.
- Never stage or commit without explicit authorization at execution time. Commit steps below carry exact messages for when authorization is given.
- Registry rule from the spec: resolve registered sources only through `IObjectRegistry.GetReference(source, false)`. Never use the static creating `ObjectRegistry.GetReference` for this check; a created entry blocks a later `RegisterReplacedObject` for that object.
- No new refusal enum values. No change to classification, capture evidence schema, or the mutation path. Evidence records stay strings-only.
- User-visible report strings follow the house string style: short sentences, active voice, one idea per sentence, no contractions, no semicolons.
- XML doc comments on load-bearing rules state why, not what.
- A filtered test run that reports 0 tests is a failure. Record observed counts.
- After the last code task, run `git diff --check` and sweep changed files for identifiers (at-sign joined to a hex hash, drive-letter paths, home-directory paths, four-digit ports, private asset names).

---

### Task 1: The untouched-renderer invariant

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (the `AmusePlatformFinishState` class, after the `AppliedOpaqueTriangleCount` property around line 46)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` (the `AvatarSummary` call site around lines 85 to 91)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: existing `AmusePlatformFinishState` counters `AnalyzedRendererCount`, `AppliedRendererCount`, and `SemanticallyRefusedRendererCount`, and the existing `RecordRendererRefusal(RendererAnalysisRefusal)` writer.
- Produces: `internal int UntouchedRendererCount` on `AmusePlatformFinishState`. Task 2 and Task 3 do not depend on it; the apply call site is its only production consumer.

- [ ] **Step 1: Write the failing tests**

Add to `AmusePlatformFinishPluginTests.cs`, next to the existing `AvatarSummary` tests:

```csharp
        [Test]
        public void UntouchedRendererCount_CountsFullySlotRefusedRenderersAsOriginal()
        {
            var state = new AmusePlatformFinishState();
            state.AnalyzedRendererCount = 2;
            state.AppliedRendererCount = 1;

            // The second analyzed renderer kept everything original
            // through slot-level refusals, which never reach
            // RecordRendererRefusal. The summary must still count it.
            Assert.That(state.UntouchedRendererCount, Is.EqualTo(1));
        }

        [Test]
        public void UntouchedRendererCount_IncludesRendererScopeRefusals()
        {
            var state = new AmusePlatformFinishState();
            state.AnalyzedRendererCount = 2;
            state.AppliedRendererCount = 1;
            state.RecordRendererRefusal(
                RendererAnalysisRefusal.AnimatedMaterialPropertyNotSingleton);

            // Falsifier: an implementation that computes only
            // analyzed minus applied reads 1 here and hides the
            // renderer-scoped refusal from the summary.
            Assert.That(state.UntouchedRendererCount, Is.EqualTo(2));
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

In the dev editor instance, run the EditMode filter `AmusePlatformFinishPluginTests`. Expected: compile failure, `UntouchedRendererCount` is not defined. That compile failure is the observed RED; record it.

- [ ] **Step 3: Add the property**

In `AmusePlatformFinishState`, after `AppliedOpaqueTriangleCount`:

```csharp
        /// <summary>
        /// How many processed renderers kept everything original.
        /// Analyzed plus renderer-refused renderers split exactly into
        /// applied and untouched, so the summary's renderer numbers
        /// cannot hide a refused renderer. The assumption is that
        /// AppliedRendererCount counts only renderers that
        /// AnalyzedRendererCount also counts; the pass structure
        /// guarantees that today, and a future pass reorder must
        /// re-read this comment.
        /// </summary>
        internal int UntouchedRendererCount =>
            AnalyzedRendererCount + SemanticallyRefusedRendererCount
            - AppliedRendererCount;
```

- [ ] **Step 4: Change the summary call site**

In `AlphaSeparationApply.Execute`, replace the third argument of the `AvatarSummary` call:

```csharp
                AmuseReports.AvatarSummary(
                    context.AvatarRootObject,
                    state.AnalyzedRendererCount,
                    state.AppliedOpaqueTriangleCount,
                    state.UntouchedRendererCount,
                    lifecycle.BuildPath,
                    state.AlphaPolicyActive);
```

- [ ] **Step 5: Run the tests to verify they pass**

Run the EditMode filter `AmusePlatformFinishPluginTests`. Expected: PASS, including the pre-existing `AvatarSummary` tests. Record observed counts.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "feat: count fully refused renderers as untouched in the summary"
```

---

### Task 2: The latest-build status store

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs` (the `AmuseBuildStatusStore` class and the `Record` call inside `AvatarSummary`)
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` (the `TryGet` call around lines 53 to 57)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` (the `amuse.summary.Title:hint` string around line 366)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `AmuseBuildStatusStore.Record(string)`, `AmuseBuildStatusStore.TryGet(out string)`, `AmuseBuildStatusStore.Forget()`. `AvatarSummary` keeps its full signature; only its body changes. Later tasks consume nothing from this one.

- [ ] **Step 1: Rewrite the store tests to the new contract**

Replace the existing `AvatarSummary` and store tests around lines 440 to 490 of `AmusePlatformFinishPluginTests.cs` (the tests that call `AmuseBuildStatusStore.TryGet(root.GetInstanceID(), ...)`) with:

```csharp
        [Test]
        public void LatestBuildRecord_ReplacesThePreviousBuildStatus()
        {
            AmuseBuildStatusStore.Forget();
            AmuseReports.AvatarSummary(
                root, 1, 2, 3, AmuseBuildPath.ApplyOnPlay, false);
            AmuseReports.AvatarSummary(
                root, 4, 5, 6, AmuseBuildPath.NonPlayNdmfBuild, false);

            // Falsifier: a keyed-per-path leftover would still return
            // the play text. The store describes the session's most
            // recent AMUSE build.
            Assert.That(AmuseBuildStatusStore.TryGet(out var status), Is.True);
            Assert.That(status, Does.Contain("Last upload"));
        }

        [Test]
        public void Forget_ClearsTheStatusSlot()
        {
            AmuseReports.AvatarSummary(
                root, 1, 2, 3, AmuseBuildPath.NonPlayNdmfBuild, false);
            AmuseBuildStatusStore.Forget();

            Assert.That(AmuseBuildStatusStore.TryGet(out _), Is.False);
        }
```

Match the surrounding fixture: `root` is the file's existing avatar-root field or local; reuse whatever setup the replaced tests used.

- [ ] **Step 2: Run the store tests to verify they fail**

Run the EditMode filter `AmusePlatformFinishPluginTests`. Expected: FAIL, the one-argument `TryGet` overload does not exist. That is the observed RED; record it.

- [ ] **Step 3: Replace the store and its writer**

In `AmuseReports.cs`, replace the `AmuseBuildStatusStore` class:

```csharp
    /// <summary>
    /// Session-scoped last-build status for the component inspector.
    /// The slot is intentionally not keyed by avatar or build path:
    /// the processed build copy never survives to be inspected, so an
    /// object-identity key could never be read. The store describes
    /// the editor session's most recent AMUSE build, and the recorded
    /// text names the build path.
    /// </summary>
    internal static class AmuseBuildStatusStore
    {
        private static string Status;

        internal static void Record(string summary)
        {
            Status = summary;
        }

        internal static bool TryGet(out string summary)
        {
            summary = Status;
            return !string.IsNullOrEmpty(summary);
        }

        /// <summary>Clears the slot. Tests use this so one run cannot
        /// leak its status into the next.</summary>
        internal static void Forget()
        {
            Status = null;
        }
    }
```

In `AvatarSummary`, replace the keyed record call:

```csharp
            AmuseBuildStatusStore.Record(summary);
```

The `avatarRoot` parameter stays; the `ErrorReport.WithContextObject(avatarRoot)` block below still uses it.

- [ ] **Step 4: Update the inspector read**

In `AmuseAvatarOptimizerEditor.cs`, replace the keyed read:

```csharp
            if (AmuseBuildStatusStore.TryGet(out var status))
            {
                EditorGUILayout.HelpBox(status, MessageType.None);
            }
```

- [ ] **Step 5: Update the hint string**

In `AmuseReportStrings.cs`, replace the hint:

```csharp
            ["amuse.summary.Title:hint"] =
                "This status shows in every AMUSE Avatar Optimizer " +
                "inspector in this editor session.",
```

- [ ] **Step 6: Update the editor test and run**

In `AmuseAvatarOptimizerEditorTests.cs`, update whatever store usage the test file has to the unkeyed API: `AmuseBuildStatusStore.Record("...")` followed by an assertion through `TryGet(out ...)`. Run the EditMode filters `AmusePlatformFinishPluginTests` and `AmuseAvatarOptimizerEditorTests`. Expected: PASS. Record observed counts.

- [ ] **Step 7: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs
git commit -m "feat: show the latest build status in every AMUSE inspector"
```

---

### Task 3: The no-identity capture hint

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` (the `amuse.texture.UnavailableCapture:hint` string around lines 306 to 312)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs`

**Interfaces:**
- Consumes: nothing from Tasks 1 and 2.
- Produces: the corrected hint text only. No signature changes.

- [ ] **Step 1: Write the failing tests**

Add to `AmuseReportStringsTests.cs`, matching the file's existing test style:

```csharp
        [Test]
        public void UnavailableCaptureHint_NamesTheUpstreamReplacement()
        {
            var hint = AmuseReportStrings.Get(
                "amuse.texture.UnavailableCapture:hint");

            Assert.That(hint, Does.Contain(
                "replaced the material texture with an in-memory copy"));
            Assert.That(hint, Does.Contain("Trace and Optimize"));
        }

        [Test]
        public void UnavailableCaptureHint_DropsTheFalseImportAdvice()
        {
            var hint = AmuseReportStrings.Get(
                "amuse.texture.UnavailableCapture:hint");

            // Falsifier: the old advice sent readers hunting for an
            // import problem that does not exist for a texture an
            // upstream build step created.
            Assert.That(hint, Does.Not.Contain(
                "Check that the texture is a real imported asset"));
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode filter `AmuseReportStringsTests`. Expected: FAIL, the current hint contains the old advice and neither new phrase. Record the RED.

- [ ] **Step 3: Replace the hint**

In `AmuseReportStrings.cs`:

```csharp
            ["amuse.texture.UnavailableCapture:hint"] =
                "When the report says the texture has no source " +
                "identity, an upstream build step replaced the " +
                "material texture with an in-memory copy. The known " +
                "producer is the texture-atlas setting of the Trace " +
                "and Optimize component of Avatar Optimizer. AMUSE " +
                "kept the affected triangles on the original material. " +
                "When the report says the texture has a source " +
                "identity, make sure that the texture is a real " +
                "imported asset and that the project supports texture " +
                "capture.",
```

Leave the `NonResidentMips` and `UnsupportedFormat` hints untouched.

- [ ] **Step 4: Run the tests to verify they pass**

Run the EditMode filter `AmuseReportStringsTests`. Expected: PASS. Record observed counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs
git commit -m "feat: point the no-identity capture hint at the upstream replacement"
```

---

### Task 4: Registered-source naming for materials

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Build/RegisteredSourceIdentity.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs` (the delegate, the `TryCaptureClosedAlphaMaterials` construction around lines 477 to 519, and the unsupported-material construction around lines 913 to 931)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs` (the `SlotSeparationRefusal` entry)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/RegisteredSourceIdentityTests.cs` (create)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`

**Interfaces:**
- Consumes: `nadena.dev.ndmf.ObjectRegistry.ActiveRegistry` and `nadena.dev.ndmf.IObjectRegistry.GetReference(Object, bool)` from NDMF 1.14.4; the existing `CapturedAlphaMaterial` constructor with `materialPath`/`materialName`.
- Produces: `internal delegate UnityEngine.Object RegisteredSourceLookup(UnityEngine.Object source)` in the `Alrauna.Amuse.Editor.Semantics` namespace; `RegisteredSourceIdentity.Resolve(UnityEngine.Object)` in `Alrauna.Amuse.Editor.Build`. Both `TryCaptureClosedAlphaMaterials` overloads gain an optional trailing `RegisteredSourceLookup resolveRegisteredSource = null` parameter, so the verified test seams compile unchanged.

- [ ] **Step 1: Write the failing helper tests**

Create `RegisteredSourceIdentityTests.cs`:

```csharp
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    public sealed class RegisteredSourceIdentityTests
    {
        [Test]
        public void Resolve_ReturnsTheRegisteredSourceObject()
        {
            var source = new GameObject("source");
            var clone = new GameObject("clone");
            var previous = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, clone);

                Assert.That(
                    RegisteredSourceIdentity.Resolve(clone),
                    Is.EqualTo(source));
            }
            finally
            {
                ObjectRegistry.ActiveRegistry = previous;
                Object.DestroyImmediate(clone);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void Resolve_ReturnsNullForAnUnregisteredObject()
        {
            // Falsifier: the creating static lookup would fabricate an
            // entry here and later block a RegisterReplacedObject for
            // the same object. The read-only lookup must answer null.
            var loner = new GameObject("loner");
            var previous = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
            try
            {
                Assert.That(
                    RegisteredSourceIdentity.Resolve(loner), Is.Null);
            }
            finally
            {
                ObjectRegistry.ActiveRegistry = previous;
                Object.DestroyImmediate(loner);
            }
        }
    }
}
```

Match the file's namespace and fixture conventions to the neighboring Build tests if they differ.

- [ ] **Step 2: Run the helper tests to verify they fail**

Run the EditMode filter `RegisteredSourceIdentityTests`. Expected: compile failure, the helper does not exist. Record the RED.

- [ ] **Step 3: Create the helper**

```csharp
using nadena.dev.ndmf;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Resolves a build-copy object to the source object a producer
    /// registered in NDMF's object registry. The lookup is read-only:
    /// with create:false an unregistered object answers null, while a
    /// creating lookup would fabricate an entry and block a later
    /// RegisterReplacedObject for that object, the failure class the
    /// 2026-09-08 registry design fixed inside AMUSE.
    /// </summary>
    internal static class RegisteredSourceIdentity
    {
        internal static UnityEngine.Object Resolve(
            UnityEngine.Object source)
        {
            if (source == null)
            {
                return null;
            }
            var reference = (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
                .GetReference(source, false);
            if (reference == null)
            {
                return null;
            }
            var resolved = reference.Object;
            return resolved != null && resolved != source ? resolved : null;
        }
    }
}
```

- [ ] **Step 4: Run the helper tests to verify they pass**

Run the EditMode filter `RegisteredSourceIdentityTests`. Expected: PASS. Record observed counts.

- [ ] **Step 5: Write the failing capture tests**

Add to `UnityMaterialSemanticsTests.cs`, using the file's existing fixture pattern for building a material and running the capture entry the file already uses. The new lookup argument rides on the existing call:

```csharp
        [Test]
        public void RegisteredCloneMaterial_ReportsTheSourceAssetPath()
        {
            var source = NewFixtureMaterialAssetWithBrokenAlpha();
            var clone = new Material(source.shader)
            {
                name = source.name + " build copy",
            };
            // The clone stays in memory, so its own project path is
            // empty. Only the registry can name the authoring asset.
            var previous = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, clone);

                var captured = RunExistingCaptureEntry(
                    clone,
                    RegisteredSourceIdentity.Resolve);

                Assert.That(
                    captured.MaterialPath,
                    Is.EqualTo(AssetDatabase.GetAssetPath(source)));
            }
            finally
            {
                ObjectRegistry.ActiveRegistry = previous;
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void UnregisteredCloneMaterial_KeepsItsOwnIdentity()
        {
            var source = NewFixtureMaterialAssetWithBrokenAlpha();
            var clone = new Material(source.shader)
            {
                name = source.name + " build copy",
            };
            var previous = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
            try
            {
                // Falsifier: an implementation that resolves through
                // the creating static lookup, or copies identity from
                // names, reports the source's path for a clone nothing
                // registered.
                var captured = RunExistingCaptureEntry(
                    clone,
                    RegisteredSourceIdentity.Resolve);

                Assert.That(captured.MaterialPath, Is.Null.Or.Empty);
                Assert.That(captured.MaterialName, Is.EqualTo(clone.name));
            }
            finally
            {
                ObjectRegistry.ActiveRegistry = previous;
                Object.DestroyImmediate(clone);
            }
        }
```

`NewFixtureMaterialAssetWithBrokenAlpha` and `RunExistingCaptureEntry` are thin wrappers over this test file's existing fixture-material helper and its existing capture call; keep the real material configuration identical to what the file's capture tests already build, and give the source material a real `AssetDatabase.CreateAsset` location so its path exists, deleting the asset in teardown.

- [ ] **Step 6: Run the capture tests to verify they fail**

Run the EditMode filter `UnityMaterialSemanticsTests`. Expected: FAIL, the capture entry has no lookup parameter yet. Record the RED.

- [ ] **Step 7: Thread the delegate through the capture**

In `UnityMaterialSemantics.cs`, declare the delegate beside `CapturedAlphaMaterial`:

```csharp
    /// <summary>
    /// Maps a live material to the source object a producer registered
    /// as its replacement, or null when nothing registered one. The
    /// capture stores the resolved object's path and name as plain
    /// strings, so evidence records never hold live Unity objects.
    /// </summary>
    internal delegate UnityEngine.Object RegisteredSourceLookup(
        UnityEngine.Object source);
```

Add `RegisteredSourceLookup resolveRegisteredSource = null` as an optional trailing parameter on `TryCaptureClosedAlphaMaterials` and on the construction path behind the unsupported-material site. At both `new CapturedAlphaMaterial(...)` sites, resolve before reading identity:

```csharp
                var source = materials[index];
                var namedSource = namedSourceFor(resolveRegisteredSource, source);
```

with one private helper beside the sites:

```csharp
        private static UnityEngine.Object namedSourceFor(
            RegisteredSourceLookup lookup,
            UnityEngine.Object source)
        {
            return lookup != null ? (lookup(source) ?? source) : source;
        }
```

and use `namedSource` for both the `materialPath` and `materialName` arguments. The `shaderName` argument keeps reading the live material, because the shader travels with the build copy.

- [ ] **Step 8: Resolve in the separation-refusal report**

In `AmuseReports.SlotSeparationRefusal`, before the report is emitted:

```csharp
            offendingMaterial =
                RegisteredSourceIdentity.Resolve(offendingMaterial)
                ?? offendingMaterial;
```

This one site covers every apply and preparation call site that passes a live material.

- [ ] **Step 9: Run the focused tests to verify they pass**

Run the EditMode filters `RegisteredSourceIdentityTests`, `UnityMaterialSemanticsTests`, `AmuseReportsSlotTests`, and `AlphaSeparationApplyTests`. Expected: PASS. `AlphaSeparationApplyTests` guards the report-path change. Record observed counts.

- [ ] **Step 10: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/RegisteredSourceIdentity.cs Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Tests/Editor/Build/RegisteredSourceIdentityTests.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs
git commit -m "feat: name the authoring asset when a producer registered the replacement"
```

---

### Task 5: Reporting documentation and final validation

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: the shipped behavior of Tasks 1 to 4.
- Produces: user-facing documentation only.

- [ ] **Step 1: Add the reporting section**

Append a `## Reading the console reports` section to `README.md` with three short paragraphs:

> One material on several slots prints one report line per slot. The repetition is deliberate, so a manual test can read the full list.
>
> NDMF may print "changed outside of NDMF animator services; cloning a second time" between AMUSE pass lines. Those lines are NDMF re-cloning controllers that an upstream tool changed. They are not AMUSE failures, and AMUSE reads the re-cloned state, so its evidence stays current.
>
> The last build status shows in every AMUSE Avatar Optimizer inspector in the editor session. It describes the session's most recent AMUSE build, and the text names the build path.

- [ ] **Step 2: Run the full assembly**

In the dev editor instance, run the full `Alrauna.Amuse.Tests.Editor` EditMode assembly. Expected: PASS. Record observed counts. A run that reports zero tests is a failure.

- [ ] **Step 3: Whitespace and identifier sweep**

```bash
git diff --check
git status --porcelain
```

Sweep every changed file for identifiers: an at-sign joined to a hex hash, drive-letter paths, home-directory paths, four-digit ports, and every private asset name the session learned. Every hit is a defect; fix the hits, then state that the sweep ran.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: explain the console reports and the re-clone notices"
```

---

## Manual validation after implementation

The animator re-clone writer attribution from the accuracy investigation stays an operator action outside this plan: one traced play-mode build with NDMF's animator debugging identifies which upstream writer churns the descriptor. Record the outcome as a dated follow-up to `docs/superpowers/investigations/2026-09-27-console-reporting-accuracy-investigation.md`. The Census Lab embedded package copy is synced by hand; ask before assuming it matches any commit, and never edit it.
