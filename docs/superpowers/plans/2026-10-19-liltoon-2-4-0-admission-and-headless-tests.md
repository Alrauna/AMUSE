# lilToon 2.4.0 Admission and Headless Test Runs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make EditMode tests run and fail without modal dialogs, and admit lilToon 2.4.0 through measured pins with strengthened tests.

**Architecture:** A test-only consent presenter seam on `AmusePlatformFinishPass` removes the stall class. Source-scan tests pin the no-modal and consent-memory properties. lilToon 2.4.0 admission materializes the symbolic admitted row as a literal, adds the measured 2.4.0 row, and converts Multi admission to version-aware rows binding version, tree, and digest as a unit.

**Tech Stack:** Unity 2022.3.22f1, NUnit via Unity Test Framework (EditMode), NDMF 1.14.x, lilToon 2.4.0 installed.

**Spec:** `docs/superpowers/specs/2026-10-19-liltoon-2-4-0-admission-and-headless-tests-design.md`

**Investigation:** `docs/superpowers/investigations/2026-10-19-liltoon-2-4-0-attestation-and-test-stall-investigation.md`

## Global Constraints

- Paths below are relative to `Packages/com.alrauna.amuse/` unless they start with `Packages/` otherwise. Research package paths carry their package root.
- Tests run through the Unity Test Runner, EditMode, via Unity MCP on the dev editor instance for this repository. Before any Unity MCP use: read `mcpforunity://instances`, pin the instance whose `Application.dataPath` matches this repo root, and stop on any mismatch. Tests must never run in the Census Lab project.
- Start a run by writing `{"mode": "EditMode", "test_names": [...], "include_failed_tests": true}` to `xd://mcp__unitymcp_run_tests`, then poll `{"job_id": "<id>", "include_failed_tests": true}` on `xd://mcp__unitymcp_get_test_job` with `wait_timeout` at most 20. A filtered run reporting 0 tests is a failure. Record observed counts.
- Never weaken an existing assertion. A new test that passes on first run is characterization and is recorded as characterization, never dressed up as RED.
- No production consent behavior changes beyond the H1 seam. Decline stays a total no-op. The dialog keeps asking once per build.
- All halt points in the spec section "Halt points for the owner" stop the task, leave the tree untouched, and report.
- Commits: the owner authorized a commit per finalized change. Stage explicit paths only. Never stage `Packages/vpm-manifest.json` except in Task 1, and never the untracked `Assets/AmuseTests_Temp*` metas except through Task 4's AssetDatabase cleanup.
- Before finishing any document touched by a task, sweep it for semicolons, contractions, unqualified `today` or `currently`, and machine identifiers. Code blocks are exempt from the semicolon rule.

---

### Task 1: Align the project manifest (M1)

**Files:**
- Modify: `Packages/vpm-manifest.json` (`dependencies` block only)

**Interfaces:**
- Consumes: nothing.
- Produces: a manifest whose `dependencies` block equals the `locked` block for all four resolved packages. No code consumes this in Task 1. Later tasks rely on the installed set staying 2.4.0.

- [ ] **Step 1: Read the manifest**

Read `Packages/vpm-manifest.json`. Confirm the `locked` block still reads: `jp.lilxyzw.liltoon` 2.4.0, `d4rkpl4y3r.d4rkavataroptimizer` 4.6.0, `com.anatawa12.avatar-optimizer` 1.9.20, `nadena.dev.ndmf` 1.14.8. If the locked values changed, halt and report.

- [ ] **Step 2: Align the dependencies block**

Set the `dependencies` entries to the locked versions:

```json
"dependencies": {
    "nadena.dev.ndmf": { "version": "1.14.8" },
    "com.vrchat.base": { "version": "3.10.5" },
    "com.vrchat.avatars": { "version": "3.10.5" },
    "d4rkpl4y3r.d4rkavataroptimizer": { "version": "4.6.0" },
    "jp.lilxyzw.liltoon": { "version": "2.4.0" },
    "com.anatawa12.avatar-optimizer": { "version": "1.9.20" }
}
```

All four stale entries move, including NDMF to 1.14.8. The locked 1.14.8 is already the resolved and installed version, so no new resolver action results.

- [ ] **Step 3: Verify the edit**

Read the file back. Assert `dependencies` and `locked` agree on lilToon, d4rkAvatarOptimizer, Avatar Optimizer, and NDMF. Do not resolve packages or touch Unity in this task.

- [ ] **Step 4: Commit**

```bash
git add Packages/vpm-manifest.json
git commit -m "chore(manifest): align requested versions with locked resolutions

Removes the resolver-dialog trigger and the revert hazard recorded in
the 2026-10-19 investigation (M1). The requested-versus-locked split
itself was the named hazard, so all four entries align."
```

---

### Task 2: Headless consent seam (H1)

**Files:**
- Modify: `Editor/Build/AmusePlatformFinishPass.cs:300-326` (field next to the gate) and `:318-326` (chain)
- Modify: `Tests/Editor/Build/MergedConsumptionFixture.cs:120-145` (install and restore)
- Test: `Tests/Editor/Build/ConsentSeamTests.cs` (create)

**Interfaces:**
- Consumes: `VersionConsentPresenter` delegate declared at `Editor/Build/VersionConsentDialog.cs:11-12`, `ShouldProceed(subjects, isBatchMode, presenter)` at `:28-43`, fixture base `SetUp`/`TearDown` at `MergedConsumptionFixture.cs:120-145`.
- Produces: `internal static VersionConsentPresenter AmusePlatformFinishPass.PresenterForTests` (null in production). Test classes may set and clear it.

- [ ] **Step 1: Read the exact delegate signature and current chain**

Read `Editor/Build/VersionConsentDialog.cs:1-60` and `Editor/Build/AmusePlatformFinishPass.cs:280-350`. Confirm the presenter signature (expected `bool Invoke(IReadOnlyList<string> subjects)`) and the exact fallback line `consentPresenter ?? VersionConsentDialog.Present`. If the signature differs from the recorder code below, adapt the recorder to the real signature before writing it.

- [ ] **Step 2: Write the failing tests**

Create `Tests/Editor/Build/ConsentSeamTests.cs`. `MergedConsumptionFixture` is `public abstract` (`Tests/Editor/Build/MergedConsumptionFixture.cs:31`) with `protected abstract string TempFolder` (`:44`), its `[SetUp]` is `PinPerTextureAnisotropicFiltering` (`:120-123`), and its `[TearDown]` is `TearDown` (`:126-141`). Drive a minimal derived probe:

```csharp
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Build;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Pins the headless consent seam (H1). The seam is null in
    /// production, the recorder answers decline and records subjects,
    /// the gate consults the seam, and the fixture restores null before
    /// any fallible teardown step.
    /// Spec: docs/superpowers/specs/2026-10-19-liltoon-2-4-0-admission-
    /// and-headless-tests-design.md
    /// </summary>
    public sealed class ConsentSeamTests
    {
        private sealed class SeamProbeFixture : MergedConsumptionFixture
        {
            protected override string TempFolder =>
                "Assets/AmuseTests_SeamProbe";
        }

        [Test]
        public void Recorder_AnswersDeclineAndRecordsSubjects()
        {
            var recorder = new RecordingDeclineConsentPresenter();
            var subjects = new[] { "Shader 'Hidden/lilToonTransparent' is not a verified version" };

            var answer = recorder.Present(subjects);

            Assert.That(answer, Is.False, "the recorder always declines");
            Assert.That(recorder.SubjectsSeen, Is.EqualTo(subjects),
                "the recorder keeps every subject it was asked");
        }

        [Test]
        public void ArmedSeam_GateConsultsItAndDeclines()
        {
            var recorder = new RecordingDeclineConsentPresenter();
            AmusePlatformFinishPass.PresenterForTests = recorder.Present;
            try
            {
                var subjects = new[]
                {
                    "Shader 'Hidden/lilToonTransparent' is not a verified version",
                };

                var proceed = VersionConsentDialog.ShouldProceed(
                    subjects, isBatchMode: false, presenter: null);

                Assert.That(proceed, Is.False,
                    "the seam's decline answer must reach the gate");
                Assert.That(recorder.SubjectsSeen, Is.EqualTo(subjects),
                    "the gate must consult the seam, not only the recorder");
            }
            finally
            {
                AmusePlatformFinishPass.PresenterForTests = null;
            }
        }

        [Test]
        public void FixtureSetUpTearDown_LeavesTheSeamNull()
        {
            var fixture = new SeamProbeFixture();
            fixture.PinPerTextureAnisotropicFiltering();
            try
            {
                Assert.That(AmusePlatformFinishPass.PresenterForTests,
                    Is.Not.Null,
                    "set-up arms the seam for the merged-consumption suites");
            }
            finally
            {
                fixture.TearDown();
            }

            Assert.That(AmusePlatformFinishPass.PresenterForTests, Is.Null,
                "tear-down restores null before any fallible teardown step");
        }
    }
}
```

The `ArmedSeam_GateConsultsItAndDeclines` test routes a synthetic subject through the real `ShouldProceed` gate while the seam is armed, so the chain reading `PresenterForTests` is pinned directly, not only through heavyweight real-build suites. If the delegate or `ShouldProceed` signature differs from this shape (read `Editor/Build/VersionConsentDialog.cs:1-60` first), adapt the recorder and the call to the real signatures before writing.

- [ ] **Step 3: Run the tests and observe RED**

Run the three tests by name. Expected: compile error, because `RecordingDeclineConsentPresenter` and `AmusePlatformFinishPass.PresenterForTests` do not exist yet. Record the observed failure.

- [ ] **Step 4: Implement the seam**

In `Editor/Build/AmusePlatformFinishPass.cs`, add the field next to the gate (not at the top of the file):

```csharp
/// <summary>
/// Test seam for headless EditMode runs. Null in production. When set,
/// the consent gate consults it before the native dialog. Tests must
/// answer decline through this seam; acceptance keeps flowing through
/// the explicit presenter parameter. (H1, spec
/// docs/superpowers/specs/2026-10-19-liltoon-2-4-0-admission-and-
/// headless-tests-design.md)
/// </summary>
internal static VersionConsentPresenter PresenterForTests = null;
```

Change the fallback chain at the gate to:

```csharp
consentPresenter ?? PresenterForTests ?? VersionConsentDialog.Present
```

In `Tests/Editor/Build/MergedConsumptionFixture.cs`, add the recorder and wire the lifecycle:

```csharp
/// <summary>
/// Decline-only consent presenter for headless runs (H1). Records every
/// subject it was asked about so tests can assert on real subjects.
/// </summary>
internal sealed class RecordingDeclineConsentPresenter
{
    internal readonly List<string> SubjectsSeen = new List<string>();

    internal bool Present(IReadOnlyList<string> subjects)
    {
        SubjectsSeen.AddRange(subjects);
        return false;
    }
}
```

In the fixture `[SetUp]` method `PinPerTextureAnisotropicFiltering`: create the recorder into a field and set `AmusePlatformFinishPass.PresenterForTests = _consentSeam.Present;`. In `TearDown`: the FIRST statement must be `AmusePlatformFinishPass.PresenterForTests = null;`, before `_filteringPin.Dispose()`, `OptimizerMergeObservation.Reset()`, and `_scope.TearDown()`.

- [ ] **Step 5: Run the tests and observe GREEN**

Run the three seam tests. Expected: PASS, 3 of 3.

- [ ] **Step 6: Run the consent regression suite and one AAO case**

Run `VersionConsentTests` and `AmusePlatformFinishPluginTests` by name. Expected: all PASS with zero modal dialogs.

Then run one attestation-dependent `AaoMergedConsumptionTests` case (for example `PipelineWithoutOptimizerKeepsNonOpaquePolygonsOnTheirOriginalMaterials`). Pre-flip, installed 2.4.0 is unadmitted, so the expected outcome is: the case completes with zero modal dialogs and FAILS with the known decline-refusal message ("the fixture must complete the real build"), because the seam removes the dialog, not the refusal. The criterion at this stage is "no stall". Record the observed outcome. Any modal dialog is a halt.

- [ ] **Step 7: Commit**

```bash
git add Editor/Build/AmusePlatformFinishPass.cs Tests/Editor/Build/MergedConsumptionFixture.cs Tests/Editor/Build/ConsentSeamTests.cs
git commit -m "feat(build): test-only consent presenter seam for headless runs

Installs a decline-only recording presenter from the merged-consumption
fixture and restores null as the first teardown statement. Production
behavior is unchanged when the seam is null (H1)."
```

---

### Task 3: No-modal source-scan test (H2)

**Files:**
- Test: `Tests/Editor/Build/ModalUiBanTests.cs` (create)

**Interfaces:**
- Consumes: `PackageManager.PackageInfo.FindForAssembly` for the product root.
- Produces: a permanent architecture test. No other task consumes it.

- [ ] **Step 1: Write the scanner with the exemption missing, observe RED**

Create `Tests/Editor/Build/ModalUiBanTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Pins the one-modal rule (H2). The product package opens exactly
    /// one modal, the consent dialog. Banned literals are closed; the
    /// walker is duplicated from the research source-ban test on
    /// purpose, because no shared home exists without a product-to-
    /// research test dependency or production test machinery.
    /// </summary>
    public sealed class ModalUiBanTests
    {
        private static readonly string[] BannedLiterals =
        {
            "EditorUtility.DisplayDialog(",
            "EditorUtility.DisplayDialogComplex(",
            "EditorUtility.DisplayCancelableProgressBar(",
        };

        private static readonly string[] ExemptFiles =
        {
            "Build/VersionConsentDialog.cs",
        };

        [Test]
        public void NoProductEditorSourceOpensAModalOutsideTheConsentDialog()
        {
            var root = PackageInfo.FindForAssembly(typeof(ModalUiBanTests).Assembly)
                .assetPath + "/Editor";
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .ToList();

            Assert.That(files.Count, Is.GreaterThan(50),
                "the scan root must hold the real Editor tree, so the ban " +
                "cannot pass vacuously");

            var violations = new List<string>();
            foreach (var file in files)
            {
                var relative = file.Substring(root.Length + 1)
                    .Replace('\\', '/');
                if (ExemptFiles.Contains(relative))
                {
                    continue;
                }

                foreach (var literal in BannedLiterals)
                {
                    if (File.ReadAllText(file).Contains(literal))
                    {
                        violations.Add(Path.GetFileName(file) + " -> " + literal);
                    }
                }
            }

            Assert.That(violations, Is.Empty,
                "modal UI is sanctioned only in VersionConsentDialog.cs");
        }
    }
}
```

Run it BEFORE adding the exemption entry to `ExemptFiles` (write the array empty first, run, observe the violation naming `VersionConsentDialog.cs`), then add the exemption line and run again. Expected: RED names exactly `VersionConsentDialog.cs`, then GREEN.

- [ ] **Step 2: Commit**

```bash
git add Tests/Editor/Build/ModalUiBanTests.cs
git commit -m "test(build): ban modal UI outside the consent dialog (H2)"
```

---

### Task 4: Temp-folder creation hygiene and residue cleanup (H3)

**Files:**
- Modify: `Tests/Editor/Semantics/Poiyomi/PoiyomiAttestationTests.cs:18,49` (raw create, no cleanup)
- Modify: `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs:237-244,243,281,313` (raw creates. `:310-318` has no try/finally at all)
- Modify: `Tests/Editor/Semantics/Poiyomi/PoiyomiFixtureTestBase.cs:26-29` (expose the shared scope helper)
- Delete via AssetDatabase: `Assets/AmuseTests_Temp 1` through `Assets/AmuseTests_Temp 8` with their metas

**Interfaces:**
- Consumes: `TestTransientScope` at `Tests/Editor/Shared/TestTransientScope.cs:41-56` (`EnsureTempFolder` and `TearDown` are instance methods bound to the scope's own `TempFolder`).
- Produces: no residue. Nothing consumes it.

- [ ] **Step 1: Re-grep every raw-create site**

Search the product test tree for `Directory.CreateDirectory` together with `AmuseTests_Temp`, and for the `TempFolder` and `HashMemoFolder` constants that feed those calls. The literal string `Directory.CreateDirectory("Assets/AmuseTests_Temp")` appears nowhere. The real sites use the constants. Record every site. Known sites: `PoiyomiAttestationTests.cs:18,49` (a standalone sealed class with no scope, whose finally deletes only an inner file), `PoiyomiMaterialSemanticsTests.cs:243,281,313` (whose base owns a private scope on the same path), and `PoiyomiMaterialSemanticsTests.cs:310-318` (`CachedSourceHash_MissingFileThrows` creates the folder with no try/finally at all).

- [ ] **Step 2: Route creation and deletion through held scopes**

The seam between the raw creates and the AssetDatabase is the bug: Unity never imports the raw folder, and the next `AssetDatabase.CreateFolder` mints a suffixed duplicate that nothing deletes. A bare `TestTransientScope.EnsureTempFolder();` call is not valid here — `EnsureTempFolder` is an instance method and neither fixture holds a scope, so make the scope real:

1. `PoiyomiMaterialSemanticsTests`: the base class `PoiyomiFixtureTestBase` already holds a private `TestTransientScope(TempFolder)`. Expose it as `internal void EnsureSharedTempFolder() => scope.EnsureTempFolder();` (or a protected accessor) on the base, call it at `:243,281,313`, and confirm the base's own teardown deletes the folder. For `CachedSourceHash_MissingFileThrows` (`:310-318`), add the try/finally the test lacks: create through the shared helper in the try, and ensure the base teardown (or an explicit scope teardown) runs in the finally.
2. `PoiyomiAttestationTests`: add a `TestTransientScope("Assets/AmuseTests_Temp")` field, call `EnsureTempFolder()` where the raw create was, and call `_scope.TearDown()` in a finally.
3. Do NOT use a bare `AssetDatabase.CreateFolder` fallback without deletion: when the folder already exists, `CreateFolder` silently no-ops and the leak returns. If a site cannot hold a scope, guard `CreateFolder` with `IsValidFolder` and pair it with `AssetDatabase.DeleteAsset` in the same finally.

- [ ] **Step 3: Delete the residue through the AssetDatabase**

In the dev editor instance, execute (refresh BEFORE the loop, so on-disk unimported folders become valid. Enumerate file system entries AFTER, so empty directories count):

```csharp
AssetDatabase.Refresh();
for (var i = 1; i <= 8; i++)
{
    var path = "Assets/AmuseTests_Temp " + i;
    if (AssetDatabase.IsValidFolder(path))
    {
        AssetDatabase.DeleteAsset(path);
    }
}
AssetDatabase.Refresh();
var residue = Directory.GetFileSystemEntries(
    "Assets", "AmuseTests_Temp*", SearchOption.TopDirectoryOnly);
return string.Join("\n", residue);
```

Expected: empty string. `GetFileSystemEntries` is required: `GetFiles` would miss a leftover empty directory, which is exactly the raw-create artifact class this task eliminates.

- [ ] **Step 4: Run the touched fixtures**

Run `PoiyomiAttestationTests` and `PoiyomiMaterialSemanticsTests` by name. Expected: PASS, 0 failures, and no new `AmuseTests_Temp*` entries under `Assets/`.

- [ ] **Step 5: Commit**

```bash
git add Tests/Editor/Semantics/Poiyomi/PoiyomiAttestationTests.cs Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs Tests/Editor/Semantics/Poiyomi/PoiyomiFixtureTestBase.cs
git commit -m "test(poiyomi): create temp folders through held asset scopes

The raw Directory.CreateDirectory sites collided with the shared
scope's AssetDatabase.CreateFolder and minted suffixed duplicates that
no teardown deleted. One test lacked a finally entirely (H3)."
```

The `Assets/` cleanup is untracked residue removal. Nothing to commit.

---

### Task 5: Consent memory ban and re-ask pin (T3)

**Files:**
- Test: `Tests/Editor/Build/ConsentMemoryBanTests.cs` (create)
- Modify: `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (add the re-ask test next to the granted-run harness around `:427`)

**Interfaces:**
- Consumes: the granted-run end-to-end harness in `AmusePlatformFinishPluginTests` (`GrantedConsentLetsThePipelineRun` and its probe plugin).
- Produces: characterization coverage of the V8 never-persist rule.

- [ ] **Step 1: Write the source ban**

Create `Tests/Editor/Build/ConsentMemoryBanTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Pins decision V8 (T3): consent is never persisted. The consent-
    /// owned files may not touch a persistence store or any raw
    /// persistence API. EditorPrefs and PlayerPrefs had zero uses in
    /// product Editor source as of 2026-10-19, so those tokens are
    /// banned across the whole Editor tree. PlayModeBuildStore writes
    /// SessionState for build caching, including the declined-consent
    /// row, and is the named, justified carve-out outside the consent-
    /// owned scope. The scan asserts it saw the consent-owned files, so
    /// a rename or a drifted root fails instead of passing vacuously.
    /// </summary>
    public sealed class ConsentMemoryBanTests
    {
        private static readonly string[] ConsentOwnedFiles =
        {
            "VersionConsentDialog.cs",
            "AmusePlatformFinishPass.cs",
        };

        private static readonly string[] StoreTokens =
        {
            "SessionState.",
        };

        private static readonly string[] PrefsTokens =
        {
            "EditorPrefs.",
            "PlayerPrefs.",
        };

        private static readonly string[] RawPersistenceTokens =
        {
            "File.WriteAllText",
            "File.AppendAllText",
            "new FileStream",
        };

        [Test]
        public void ConsentOwnedFiles_NeverTouchAPersistenceStore()
        {
            var root = PackageInfo.FindForAssembly(typeof(ConsentMemoryBanTests).Assembly)
                .assetPath + "/Editor";
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            var seen = new HashSet<string>();
            var violations = new List<string>();

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                seen.Add(name);
                var text = File.ReadAllText(file);
                var isConsentOwned = ConsentOwnedFiles.Contains(name);

                foreach (var token in StoreTokens.Concat(PrefsTokens)
                             .Concat(RawPersistenceTokens))
                {
                    var bannedHere = isConsentOwned
                        || PrefsTokens.Contains(token);
                    if (bannedHere && text.Contains(token))
                    {
                        violations.Add(name + " -> " + token);
                    }
                }
            }

            Assert.That(seen, Is.SupersetOf(ConsentOwnedFiles),
                "the scan must have covered the consent-owned files, or " +
                "it proved nothing");
            Assert.That(violations, Is.Empty,
                "consent must never persist anywhere (decision V8)");
        }
    }
}
```

Run it. Expected: PASS on first run. Record as characterization (the V8 property already holds structurally. The prefs tokens were measured at zero uses across the whole Editor tree).

- [ ] **Step 2: Write the behavioral re-ask pin**

In `AmusePlatformFinishPluginTests`, add a test modeled on the granted-run harness: build once with a recording granting presenter, build again with the same subject present, assert the presenter was asked twice and each ask carried the subject. Drive the same public path the granted-run test uses. Run it. Expected: PASS on first run. Record as characterization with the observed ask count of 2. If it fails, a consent memory exists somewhere and that is a blocker: halt and report.

- [ ] **Step 3: Commit**

```bash
git add Tests/Editor/Build/ConsentMemoryBanTests.cs Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "test(build): pin the consent never-persist rule (T3)"
```

---

### Task 6: Enumeration accessor, boundary tests, drift tests, Multi cross tests

**Files:**
- Modify: `Editor/Semantics/LilToon/LilToonSourceAttestation.cs` (accessor near `:407-436`)
- Modify: `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs` (boundary TestCases near `:1697-1806`, drift tests)
- Modify: `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs` (Multi cross tests near `:348-404`)

**Interfaces:**
- Consumes: `AdmittedPackageVersions` at `LilToonSourceAttestation.cs:393-405`, `MultiContainerProfiles` at `:2313-2333`, `TryVerifyIncludeTreeForVersion` at `:2122-2153`.
- Produces: `internal static IReadOnlyList<(string Version, string IncludeDigest)> AdmittedRows()` and `internal static IReadOnlyList<(string Version, string MultiDigest, string MultiOutlineDigest)> AdmittedMultiRows()` on `LilToonSourceAttestation`. R1 (Task 9) consumes `AdmittedRows`. The flip (Task 8) updates the pinned data.

- [ ] **Step 1: Add the accessors**

```csharp
/// <summary>
/// Read-only snapshot of the admitted version rows for first-party
/// test and research consumers (T1, R1). Returns a copy, so a caller
/// can never observe or cause in-place mutation.
/// </summary>
internal static IReadOnlyList<(string Version, string IncludeDigest)> AdmittedRows()
{
    return AdmittedPackageVersions.ToArray();
}
```

Do not add the Multi accessor yet. Task 8 introduces `AdmittedMultiRows` together with the version-aware rows it exposes, so the accessor never exists without its data.

- [ ] **Step 2: Write the admitted-set enumeration test (pre-flip data)**

In `LilToonAttestationTests.cs`:

```csharp
[Test]
public void AdmittedVersionRows_AreTheMeasuredSet()
{
    var rows = LilToonSourceAttestation.AdmittedRows();

    Assert.That(rows.Select(row => row.Version).ToList(), Is.Unique);
    Assert.That(rows.Select(row => row.Version).ToList(), Is.EqualTo(
        new[] { "2.3.0", "2.3.1", "2.3.2", "2.3.3", "2.3.4" }));
    Assert.That(rows[1].IncludeDigest, Is.EqualTo(rows[2].IncludeDigest),
        "2.3.1 through 2.3.3 ship one shared tree");
    Assert.That(rows[3].IncludeDigest, Is.EqualTo(rows[2].IncludeDigest),
        "2.3.1 through 2.3.3 ship one shared tree");
}
```

Run it. Expected: PASS (characterization of the pre-flip set).

- [ ] **Step 3: Write the boundary refusal tests**

Add TestCases to the existing wrong-version test family:

```csharp
[TestCase("2.4.1")]
[TestCase("2.5.0")]
[TestCase("2.3.10")]
[TestCase("2.4")]
[TestCase("02.4.0")]
public void Verify_NonAdmittedVersionForms_Refuse(string version)
{
    // Build evidence exactly as Verify_WrongPackageVersion_IsRefused
    // does at :1758, substituting the case version.
    // Assert the refusal kind is UnsupportedVersion.
}
```

Reuse the existing evidence builder from the `:1758` test. Do not duplicate its setup inline. Run. Expected: PASS (characterization. Ordinal matching already refuses these).

- [ ] **Step 4: Write the drift tests**

In `LilToonAttestationTests.cs`:

```csharp
[Test]
public void GatheredTreeMatchingAnOlderRowWhileVersionIsNewer_IsModifiedSource()
{
    // Build a stand-in include tree on disk whose digest equals the
    // "2.3.0" admitted row, run the PRODUCTION gather over it (the way
    // LilToonIncludeTreeTests uses AttestationEnvironment.Tree()), and
    // verify with the package version set to the newest admitted.
    // Assert the mismatch code is ModifiedShaderSource, not
    // UnsupportedVersion. Synthetic evidence mutation does not satisfy
    // this test: the gather path itself must run.
}

[Test]
public void MutatedProfileDigestInput_ReportsModifiedSource()
{
    // For each regular family (opaque, cutout, transparent), take the
    // passing evidence built by the existing helpers, run the real
    // gather against a stand-in tree, change exactly one digest input,
    // and assert the mismatch code is ModifiedShaderSource. A purely
    // synthetic mutation of an in-memory evidence record does not
    // satisfy this test.
}
```

G2 names real gathers: every existing mismatch test feeds synthetic evidence, so the new tests must drive the production gather over a stand-in tree on disk and assert the diagnostic code the production path reports. The scratch-tree machinery exists (`Tests/Editor/Semantics/LilToon/LilToonIncludeTreeTests.cs:40-43`, `AttestationEnvironment.Tree()`). Do not use a `LilToonFamily` enum sketch. Write one test per family or parameterize over the real profile names. Run. Expected: PASS (the code path exists and is correct. This pins the diagnostic code, closing G2). If any case reports `UnsupportedVersion` instead of `ModifiedShaderSource`, that is a real finding: halt and report.

- [ ] **Step 5: Write the Multi cross-combination tests**

In `LilToonSourceAttestationTests.cs` near the Multi suite:

```csharp
[Test]
public void MultiContainer_VersionAndOutlineDigestFromDifferentVersions_Refuse()
{
    // Evidence A: version "2.3.4" (pre-flip newest) with the outline
    // digest swapped for an impossible value. Expect refusal.
    // Evidence B: version "2.3.5" with the fully correct pre-flip
    // outline digest. Expect refusal.
    // Assert both refuse and record the mismatch codes.
}
```

Post-flip (Task 8) this test gains its sharp form: correct pairing per version verifies, swapped pairings refuse. Pre-flip it passes because the const gate refuses everything else. Run. Expected: PASS.

- [ ] **Step 6: Run the full LilToon attestation suites**

Run `LilToonAttestationTests` and `LilToonSourceAttestationTests` by name. Expected: all PASS, 0 failures.

- [ ] **Step 7: Commit**

```bash
git add Editor/Semantics/LilToon/LilToonSourceAttestation.cs Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs Tests/Editor/Semantics/LilToonSourceAttestationTests.cs
git commit -m "test(semantics): pin admitted rows, boundaries, and drift codes

Adds the first-party accessor snapshot, the admitted-set enumeration,
upper-edge refusal witnesses, ModifiedShaderSource drift pins, and
Multi cross-combination coverage (T1, T2)."
```

---

### Task 7: Measurement protocol (haltable)

**Files:**
- No repo edits. Produce a measurement record in the task report and hand the values to Task 8.

**Interfaces:**
- Consumes: the installed lilToon 2.4.0 package in this project, a scratch clone of upstream tag `2.3.4` for the anchor reproduction, and the production digest functions.
- Produces: measured values for Task 8. Values in this plan marked `<measured-...>` are placeholders that Task 7 fills. Task 8 must not run without them.

- [ ] **Step 1: Reproduce the 2.3.4 anchors**

Scratch-clone upstream `https://github.com/lilxyzw/lilToon` at tag `2.3.4` outside the repository. Compute the include-tree digest of the tag's `Assets/lilToon/Shader/Includes` tree with the production `ComputeIncludeTreeDigest` (via `execute_code` in the dev editor, pointing the gather root at the scratch tree, or through the S11 test machinery in `LilToonAttestationTests.cs` — the doc comment sits at `:2383-2390` and the test method at `:2399`). Expected: `6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46`. HALT if it differs. This reproduction is also the license for step 2.7: the tag tree stands in for an installed 2.3.4 shape only because its digest reproduces the install-measured pin.

- [ ] **Step 2: Measure the 2.4.0 installed shape**

In the dev editor, against the installed 2.4.0 package (`Packages/jp.lilxyzw.liltoon/`), measure and record:

1. Include-tree digest of `Shader/Includes` (production digest function). Expected: differs from the 2.3.4 value. HALT if it equals any existing row digest.
2. The `_lilToonVersion` stamp from a real 2.4.0 material. Expected `45f`. HALT otherwise.
3. Asset GUIDs for `lts.shader`, `ltspass_opaque.shader`, `lts_cutout.shader`, `ltspass_cutout.shader`, `lts_trans.shader`, `ltspass_transparent.shader`, the one-pass and two-pass variants, and the outline wrappers. Expected: unchanged from the pinned constants. HALT on any mismatch.
4. Canonical digests for the regular profiles (opaque, cutout, transparent, wrappers, one and two pass). Expected: equal to the pinned constants. HALT on any mismatch.
5. Multi container digests for `lilToonMulti` and `lilToonMultiOutline`. Expected: the Multi outline digest differs from the pinned 2.3.4 value (queue tag change). The base Multi digest is expected unchanged. HALT is not automatic for the outline difference (it is the expected outcome). Record both values. HALT if the base Multi digest differs from its pinned value: an unexpected change is a measurement surprise, not an expected outcome.
6. `CanonicalOpaqueTuple`, queue, and render type per `LilToonOpaqueTarget.cs:26-30`. Expected: exact reproduction of all eighteen writes. HALT on any difference.
7. Byte comparison of `lil_common_frag_alpha.hlsl`, `lil_pass_forward_normal.hlsl`, and `lil_pass_depthonly.hlsl` between the scratch 2.3.4 tree and the installed 2.4.0 tree. Expected: identical. HALT on any difference, and record the comparison provenance (tag tree versus installed shape, licensed by step 1) in the report.
8. Vocabulary check: run the pinned `OfficialSettingIdentifiers` count test (`LilToonAttestationTests.cs:1242,1250`, count 109) and the skip-vocabulary closed-domain audit (`:913`) as they stand. Record the observed counts. The 2.4.0-generator confirmation completes through the real gathers in Task 8 step 6: HALT there on any unmapped token, and HALT here if either vocabulary test already fails pre-flip.

- [ ] **Step 3: Second shape where obtainable**

Repeat steps 2.1, 2.4, 2.5, and 2.6 against a second installed shape (a scratch throwaway project OUTSIDE this repository, with a different lilToon settings shape produced by lilToon's own tooling, per the Multi precedent `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2316-2325`). Never apply a second shape inside this project: rewriting the installed package bytes destroys the measurement source and desyncs the pinned digest from the running install. One digest per file across shapes is required for any digest whose value is new. HALT and surface to the owner if no second shape is obtainable.

- [ ] **Step 4: Emit the measurement record**

Produce a table: value, method, shape(s), expected, observed. Every `<measured-...>` placeholder for Task 8 must be filled or the task halts.

---

### Task 8: The flip (A1 + A2 + A3 + mirrors), one landing commit

**Files:**
- Modify: `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:361-405` (const and rows), `:2122-2153` context, `:2210-2251` (Multi verify), `:2313-2333` (Multi rows)
- Modify: `Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:20-25` (provenance comment)
- Modify: `Tests/Editor/Build/MergedConsumptionFixture.cs` (static subject collector and teardown assertion)
- Modify: `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1893-1907` (audit literal), `:2391-2412` (S11 TestCases)
- Modify: `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:190-195` (shared-tree fixture), `:220-221` (MultiEvidence default), `:350-360` (Multi message), `:216-247` (MultiEvidence both versions)
- Modify: `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:245-246`, `:1924-1925`, `:2009-2010` (Evidence, CutoutEvidence, TransparentEvidence defaults)
- Modify: `README.md:42` area and `CHANGELOG.md:10` area (version strings)

**Interfaces:**
- Consumes: Task 7 measurements and Task 6 tests.
- Produces: admitted set 2.3.0 through 2.4.0, version-aware Multi, and updated pins and mirrors.

- [ ] **Step 1: Update the rows and constants**

```csharp
internal const string PackageName = "jp.lilxyzw.liltoon";
internal const string PackageVersion = "2.4.0"; // newest admitted; the set is AdmittedPackageVersions (S11)
internal const float ShaderFormatVersion = 45f; // re-confirmed on installed 2.4.0, 2026-10-19
```

Rows (values from Task 7, with digests shown as the plan's placeholders only):

```csharp
// Admitted package versions and the include-tree digest each ships
// (S11). Three evidence sources, named per the file's comment
// discipline: 2.3.0-2.3.4 shader and pass identity rests on the
// per-tag raw sha256 comparison of the official tag artifacts
// (inherited from the original measurement); the 2.4.0 shader and
// pass digests reproduce the pinned constants on two installed 2.4.0
// shapes (2026-10-19); the three alpha-cited includes are byte-
// identical between the 2.3.4 tag tree and the installed 2.4.0 shape
// (2026-10-19, licensed by reproducing the pinned 2.3.4 tree digest
// from that tree). The include tree is the only per-version bytes the
// identity conjunction checks. 2.3.1-2.3.3 ship identical trees and
// share a row. The 2.4.0 tree differs by the VRC Light Volumes 3
// rewrite and the APV sampling refactor. Never re-derive these from
// the lilToon repository, whose committed generated shaders are stale
// relative to their own tag's generator.
private static readonly (string Version, string IncludeDigest)[]
    AdmittedPackageVersions =
    {
        ("2.3.0",
            "bba3205a08b2bd56b4d2c69b8de3144377ec0b4ca4c44cc9d71b1341e62c94a0"),
        ("2.3.1",
            "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416"),
        ("2.3.2",
            "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416"),
        ("2.3.3",
            "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416"),
        ("2.3.4",
            "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46"),
        ("2.4.0",
            "<measured-2.4.0-tree-digest>"),
    };
```

`IncludeTreeDigest` keeps its role as the newest admitted tree digest and takes the measured 2.4.0 value. The 2.3.4 anchor lives in the literal row and in the S11 test.

- [ ] **Step 2: Make Multi admission version-aware**

Replace the Multi verify's const comparisons (`:2216-2230`) with: version through `IsAdmittedPackageVersion`, tree through `TryVerifyIncludeTreeForVersion`, digest through a version-filtered Multi row lookup. Restructure the rows. The 2.3.4 row keeps the existing pinned literals VERBATIM (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2322-2330`, measured 2026-10-01 from installed 2.3.4 shapes). There is no measured-or-pinned choice: the 2.3.4 values are the pins, and substituting anything else would silently refuse every real 2.3.4 Multi material.

```csharp
// (A2) Version-aware Multi admission. Version, tree, and container
// digest bind as a unit. No cross combination attests. The 2.4.0
// outline digest differs from 2.3.4 because upstream fix 435 changed
// the ltsmulti_o queue tag; measured 2026-10-19 from installed shapes
// per the two-shape rule. The 2.3.4 row carries the 2026-10-01 pins
// unchanged.
private static readonly (string Version, string MultiDigest, string MultiOutlineDigest)[]
    AdmittedMultiVersions =
    {
        ("2.3.4",
            "346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f",
            "225af2c6eba7a017593ca8a0f38114a98580ed67844a4d8465fed2c96f7cb75f"),
        ("2.4.0",
            "<measured-2.4.0-multi>",
            "<measured-2.4.0-multi-outline>"),
    };
```

Loose installs (no package version) keep their documented design: the tree digest identifies the shipped version, and the container digest compares against that identified row's column. Implement the versionless branch as tree-to-row resolution followed by the row's per-container digest check, and mirror the regular path's wording at `:2129-2134`. `MultiContainerProfiles` keeps name and GUID resolution and maps each container to its column (base or outline). Add `AdmittedMultiRows()` returning a snapshot for the enumeration test. Preserve the existing refusal kinds and the loud single-version message at `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:350-360` only if it still states a true fact. Otherwise update it to name both admitted versions.

- [ ] **Step 3: Record the A3 re-measurement**

In `Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:20-25`, extend the provenance comment: tuple, queue, and render type re-measured on installed 2.4.0 on 2026-10-19 and reproduced exactly. Change no tuple value.

- [ ] **Step 4: Update every mirror**

1. `PinnedDigests_AreTheTask0Measurements` audit literal (`LilToonAttestationTests.cs:1893-1907`): the include-tree expectation becomes the measured 2.4.0 value with a comment naming the new anchor split (2.3.4 anchor lives in the row literal and the S11 tests). Extend the same audit to name both `AdmittedMultiVersions` rows verbatim, so a transcription error in either Multi row fails the audit independently of the rows themselves.
2. S11 TestCases (`:2391-2412`): add the `("2.4.0", <tree>)` case. Keep the 2.3.4 anchor case.
3. Shared-tree fixture constants (`LilToonSourceAttestationTests.cs:190-195`): the prior-tree literal stays 2.3.x-shared. Add the 2.4.0 pairing to whatever the fixture enumerates.
4. Helper defaults: all four `"2.3.4"` defaults mean "newest admitted" (each pairs with `UsePin`, which resolves to `IncludeTreeDigest`), so all four move to `LilToonSourceAttestation.PackageVersion`: `LilToonSourceAttestationTests.cs:220-221` (MultiEvidence) and `LilToon/LilToonAttestationTests.cs:245-246` (Evidence), `:1924-1925` (CutoutEvidence), `:2009-2010` (TransparentEvidence). A literal left behind after the flip becomes the exact cross combination A2 forbids and fails the acceptance tests. The one literal that must stay a pinned OLD version is the 2.3.4 Multi cross-test fixture data: its tree and digests come from the 2.3.4 row literals, because `UsePin` only pairs with the newest.
5. Multi cross tests from Task 6 step 5 gain the sharp form: correct pairing per version verifies, swapped pairing refuses. Add loose-install Multi TestCases carrying each admitted tree (the 2.3.4 tree and the 2.4.0 tree), each verifying against its identified row.
6. `AdmittedVersionRows_AreTheMeasuredSet` gains `"2.4.0"` as the sixth row and the new shared-row facts, and asserts `AdmittedMultiRows()` equals exactly the two version rows with their per-version digests, versions unique. This assertion is written inside the flip commit so the suite never sits red between commits.
7. Boundary witness: add `"2.4.0"` nowhere as a refusal. The upper-edge witnesses from Task 6 stay.
8. `README.md` supported-version strings: 2.3.0 through 2.4.0. `CHANGELOG.md`: add the entry describing the 2.4.0 admission.
9. `MultiEvidence` defaults (`:216-247`): parameterize over both versions, with the 2.3.4 case carrying the 2.3.4 row literals.
10. In `Tests/Editor/Build/MergedConsumptionFixture.cs`, promote the recorder's `SubjectsSeen` to a static collector cleared in the fixture `SetUp`, and add a `TearDown` assertion that it is empty: after the flip, a merged-consumption build must raise zero consent subjects. The assertion can only land in this flip commit, because pre-flip suites legitimately record subjects. NUnit instantiates a fresh fixture per test, so per-instance state is unobservable after a class run. The static collector with a per-test assertion is the in-repo pattern (`TransientUnlockTransformationTests.cs:159-163` with its reset at `:1596-1604`).

- [ ] **Step 5: Ground the Multi surface against the installed package**

Every Multi test vehicle is synthetic, so the rows would be self-consistent even if wrong. Add one real-gather check: `Shader.Find` the installed `lilToonMulti` and `lilToonMultiOutline` assets, resolve them through `TryResolveMultiContainer` with the production gather, and assert verify for the installed version and refused for a swapped digest. This gives the Multi half the same installed-bytes ground the AAO suite gives the regular path. Run it.

- [ ] **Step 6: Run the lilToon suites**

Run `LilToonAttestationTests`, `LilToonSourceAttestationTests`, `LilToonOpaqueTargetTests` by name. Expected: all PASS. `LilToonOpaqueTargetTests` passes because the installed 2.4.0 now attests. HALT on any unmapped vocabulary token or 109-count drift (A4's confirmation completes here through the real gathers).

- [ ] **Step 7: Run the real-build suite**

Run `AaoMergedConsumptionTests` by class. The observable signals: every fixture's complete-the-real-build assertion passes, the NDMF error report carries no `ConsentDeclined` entry, and the fixture's teardown subject assertion (step 4 item 10) holds. Any recorded subject means the attestation still refuses something: halt and report with the recorded subject text.

- [ ] **Step 8: Commit**

```bash
git add Editor/Semantics/LilToon/LilToonSourceAttestation.cs Editor/Semantics/LilToon/LilToonOpaqueTarget.cs Tests/Editor/Build/MergedConsumptionFixture.cs Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs Tests/Editor/Semantics/LilToonSourceAttestationTests.cs README.md CHANGELOG.md
git commit -m "feat(semantics): admit lilToon 2.4.0 with version-aware Multi

Materializes the 2.3.4 row, adds the measured 2.4.0 row, repoints the
newest-admitted constants, converts Multi admission to version-aware
rows binding version, tree, and digest as a unit, records the opaque
tuple re-measurement, and mirrors every changed literal (A1, A2, A3)."
```

---

### Task 9: Research gates measure the admitted set (R1)

**Files:**
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs:56-88` and `:373-399`
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/CensusVendorProbe.cs` only if the probe needs the accessor (it reads constants and auto-moves)

**Interfaces:**
- Consumes: `LilToonSourceAttestation.AdmittedRows()` from Task 6, and the internals grant already covering the research test assemblies (`Editor/AssemblyInfo.cs` grants, research-editor grant around line 10 and research-tests at line 20).
- Produces: both research gates pass with any admitted version installed.

- [ ] **Step 1: Convert gate 1 to membership, lilToon family only**

`AnInstalledFamilyMatchesTheVersionAmuseAttests` loops all vendor families. Convert ONLY the lilToon family to membership in `AdmittedRows()`. Families other than lilToon (Poiyomi as of 2026-10-19) keep equality with their own probe-pinned expected version, because `AdmittedRows()` is lilToon-only and converting them would silently drop the Poiyomi 9.3.64 pin in any project where Poiyomi is installed. Keep the probe-constant pin for lilToon: the probe constant equals the newest admitted. Update the doc comments that say `2.3.4` (`:59`, `:372-374`).

- [ ] **Step 2: Convert gate 2's precondition**

`LilToonReachesProvenOpaqueThroughTheProductionPath` (`:393-397`) asserts installed version membership instead of equality with the newest pin.

- [ ] **Step 3: Run the research suite**

Run `VendorReachabilityTests` by name. Expected: the two lilToon gates PASS with 2.4.0 installed. The four Poiyomi gates still skip (package absent). Record counts.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs
git add Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/CensusVendorProbe.cs
git commit -m "test(research): census gate admits the full measured set (R1)"
```

Stage `CensusVendorProbe.cs` only if step 1 or 2 actually modified it.

---

### Task 10: Full verification and record

**Files:**
- No repo edits unless a failure demands one.

- [ ] **Step 1: Full EditMode run**

Run both assemblies (`Alrauna.Amuse.Tests.Editor`, `Alrauna.Amuse.Research.Tests.Editor`) through the Unity Test Runner. Expected: 0 failures. The only non-passes are the env-gated DAO Inconclusive pair and the four Poiyomi skips. Confirm zero modal blocks and confirm `Assets/` holds no `AmuseTests_Temp*` entries.

- [ ] **Step 2: Record**

Report observed counts, the run duration, the measurement table from Task 7, and any characterization tests recorded during execution. Compare against the 2026-10-19 baseline (2735 passed and 0 failed product, 140 passed and 4 skipped research) and the post-update failure record (21 plus 2). Two verification limits belong in the record: AC1's unattested branch is fully exercised only pre-flip (Task 2 step 6), because the flip attests the installed version, and AC8's halt points are verifiable only for conditions that actually occur during the run.

---

## Self-Review

1. Spec coverage: M1 (Task 1), H1 (Task 2, with the re-ask pin in Task 5), H2 (Task 3), H3 (Task 4), A4 (Task 7 step 2.8 plus the Task 8 step 6 gather confirmation), T3 (Task 5), T1/T2 and the accessor (Task 6), A1/A2/A3 measurements (Task 7), the flip with mirrors (Task 8, including the Multi-row enumeration assertion and the static subject collector), R1 (Task 9, lilToon family scoped), acceptance run (Task 10). G1 through G5 all land. No spec decision lacks a task.
2. Placeholder scan: the `<measured-...>` placeholders are intentional and gated. Task 8 cannot run until Task 7 fills them, the 2.3.4 Multi row carries the existing pinned literals verbatim so it has no placeholder at all, and the halt list covers a failed fill.
3. Type consistency: `AdmittedRows()` (Task 6) is consumed by R1 (Task 9). `AdmittedMultiRows()` is introduced in Task 8 step 2 together with its data and consumed by the enumeration assertion in Task 8 step 4 item 6. `RecordingDeclineConsentPresenter` is defined in Task 2 and consumed by the fixture, the gate-routing test, and the flip's static collector. `PresenterForTests` names match across H1 tasks.
