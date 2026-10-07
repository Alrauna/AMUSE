# Way-4 Ordinal Scoping and Swap-Consent Subjects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A closed-batch capture failure refuses only the slots that hold the
failing material, and the D8 consent pre-scan offers subjects for
swap-reachable materials, not just assigned ones.

**Architecture:** The production capturers already compute per-material
results; their wrappers discard the failing index. Part A threads a
`ClosedAlphaCaptureOutcome` (survivors plus failing ordinals) through the
`ClosedAlphaMaterialCapturer` contract, maps failed ordinals onto the
existing per-material sentinel, and deletes the renderer-wide closure path
(cutover, no dormant code). Part B moves the consent block after the stored
committed-graph gate and feeds `CollectTransferConsent` from assigned
materials plus material keyframes on the graph's clips.

**Tech Stack:** C# against Unity 2022.3.22f1, editor-only assembly
`Alrauna.Amuse.Editor`, NUnit via Unity Test Framework (EditMode, run through
the Unity Test Runner in the dev editor instance; no CLI).

**Spec:** `docs/superpowers/specs/2026-10-07-way4-ordinal-scoping-and-swap-consent-design.md`
(decisions A1, A2, B1 approved on 2026-10-07).

## Global Constraints

- No CLI test run exists. Tests run through the Unity Test Runner, EditMode.
  A filtered run reporting 0 tests is a failure.
- RED before GREEN: each behavior change observes a failing test first, for a
  named plausible wrong implementation. An assertion that passes on first run
  is recorded as characterization, never dressed up as RED.
- One public type per file; file name equals type name; namespaces mirror
  folders; production types `internal`; `Editor/AssemblyInfo.cs` grants
  `InternalsVisibleTo`.
- Closed refusal enums per scope. Unsupported means a named refusal value.
  Programming defects throw and block the build. Fail closed;
  `MustRemainTransparent` is absorbing.
- Tests run on schema-only stand-in shaders under `Hidden/Alrauna/AmuseTests/*`.
  Vendor shaders are never installed.
- Assertions use the NUnit constraint model:
  `Assert.That(actual, Is.EqualTo(expected))`.
- No private identifiers, machine paths, ports, or instance names in any
  file, commit message, or report. Record observed counts.
- Every commit in this plan executes only on the session's explicit
  authorization. Stage explicit paths only.
- After each task: refresh the editor (compile request, wait for ready),
  read the console, and require zero CS errors before running tests.

---

### Task 1: `ClosedAlphaCaptureOutcome` and the wrapper cutover

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/ClosedAlphaCaptureOutcome.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:300-407`
  (`TryCaptureClosedAlphaMaterials`, `TryCaptureClosedAlphaMaterialsTransferred`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`

**Interfaces:**
- Consumes: existing `CaptureBatch`, `IsAttestedAlphaMaterial` (both private
  in `UnityMaterialSemantics`).
- Produces: `ClosedAlphaCaptureOutcome` with
  `IReadOnlyList<CapturedAlphaMaterial> Captured` (survivors in batch order)
  and `IReadOnlyList<int> UnattestedOrdinals` (batch positions that failed
  attestation). Invariant: survivor ordinal `k` maps to the `k`-th batch
  position not named in `UnattestedOrdinals`. Both wrappers change to
  `out ClosedAlphaCaptureOutcome outcome` and return true for every
  per-material outcome; `false` stays reserved for a failure that names no
  material.

- [ ] **Step 1: Write the failing wrapper tests**

Add to `UnityMaterialSemanticsTests.cs`, near the existing transferred-capture
tests, reusing that file's stand-in material fixtures:

```csharp
[Test]
public void MixedBatchReportsSurvivorsAndFailingOrdinals()
{
    using var scope = new UnattestedShaderScope(); // the file's existing
                                                   // stand-in shader fixture
    var healthy = HealthyStandInMaterial();        // existing fixture helper
    var poison = PoisonMaterialFrom(scope.Shader); // supported name,
                                                   // unattested source
    var names = new[] { healthy, poison };
    var families = new[] { FamilyOf(healthy), FamilyOf(poison) };
    var request = CombinedRequestFor(names);

    var ok = UnityMaterialSemantics.TryCaptureClosedAlphaMaterials(
        names, families, request, Bounds, out var outcome, null);

    Assert.That(ok, Is.True);
    Assert.That(outcome.UnattestedOrdinals, Is.EqualTo(new[] { 1 }));
    Assert.That(outcome.Captured.Count, Is.EqualTo(1));
    Assert.That(
        IsAttested(outcome.Captured[0]), Is.True);
}

[Test]
public void AllFailedBatchReportsEveryOrdinalAndNoSurvivors()
{
    // Both members poison. Outcome: Captured empty, ordinals {0, 1},
    // return true. Committed behavior: return false, no outcome.
}

[Test]
public void TransferredBatchGrantsTheNamedMemberAndReportsTheRest()
{
    // One granted-name poison, one ungranted poison. Granted member is
    // captured; ungranted ordinal reported. Committed behavior: false.
}
```

Fill the three bodies from the file's existing fixtures; the assertions above
are the contract under test.

- [ ] **Step 2: Run the new tests, verify RED**

Run via the Unity Test Runner (MCP `run_tests`), group
`Alrauna.Amuse.Tests.Editor.Semantics.UnityMaterialSemanticsTests`.
Expected: the three new tests fail on the missing `ClosedAlphaCaptureOutcome`
overload (compile error counts as RED for a contract change; observe it in
the console).

- [ ] **Step 3: Create the outcome type**

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// One closed batch capture's per-material outcome. Captured carries
    /// every surviving member's result in batch order. UnattestedOrdinals
    /// names the batch positions whose member failed source attestation and
    /// was not admitted. Survivor ordinal k addresses the k-th batch
    /// position outside UnattestedOrdinals; the two lists partition the
    /// batch. The capturer's bool stays false only for a failure that
    /// names no material, which no production capturer produces.
    /// </summary>
    internal sealed class ClosedAlphaCaptureOutcome
    {
        internal ClosedAlphaCaptureOutcome(
            IReadOnlyList<CapturedAlphaMaterial> captured,
            IReadOnlyList<int> unattestedOrdinals)
        {
            Captured = new ReadOnlyCollection<CapturedAlphaMaterial>(
                new List<CapturedAlphaMaterial>(captured));
            UnattestedOrdinals =
                new ReadOnlyCollection<int>(
                    new List<int>(unattestedOrdinals));
        }

        internal IReadOnlyList<CapturedAlphaMaterial> Captured { get; }
        internal IReadOnlyList<int> UnattestedOrdinals { get; }
    }
}
```

- [ ] **Step 4: Rewrite the two wrappers**

Replace the attestation loops in both entries
(`UnityMaterialSemantics.cs:319-325` and `:388-407`) with the partition:

```csharp
var result = CaptureBatch(
    materials, families, requests, bounds, resolveRegisteredSource);
var captured = new List<CapturedAlphaMaterial>();
var failed = new List<int>();
for (var index = 0; index < result.Count; index++)
{
    if (IsAttestedAlphaMaterial(result[index]))
    {
        captured.Add(result[index]);
        continue;
    }
    failed.Add(index);
}
outcome = new ClosedAlphaCaptureOutcome(captured, failed);
return true;
```

The transferred variant keeps its granted-name skip inside the loop: a
member whose shader name is in `grantedShaderNames` joins `captured`
instead of `failed` (`UnityMaterialSemantics.cs:392-402`).

- [ ] **Step 5: Fix every caller of the old signature to compile**

The delegate still has its old shape until Task 2, so keep temporary
adaptation local if needed, or land Tasks 1 and 2 as one commit. Preferred:
continue straight into Task 2 before compiling.

- [ ] **Step 6: Run the class, verify GREEN**

Run `Alrauna.Amuse.Tests.Editor.Semantics.UnityMaterialSemanticsTests`.
Expected: new tests pass; no other test in the class regresses. Record
observed counts.

- [ ] **Step 7: Commit (on authorization)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/ClosedAlphaCaptureOutcome.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs
git commit -m "feat: closed capture wrappers report surviving results and failing ordinals"
```

---

### Task 2: Capture maps failed ordinals to the sentinel

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs:87-98`
  (delegate), `:117-127` (`DefaultCapturer`), `:714-733` (batch call site),
  `:477-494` (`Failed` helper becomes unused this task; delete it)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `ClosedAlphaCaptureOutcome` from Task 1.
- Produces: delegate
  `bool ClosedAlphaMaterialCapturer(materials, families, request, bounds,
  out ClosedAlphaCaptureOutcome outcome)`. Capture-level: a batch with
  failures closes the renderer; failed members' slots refuse as
  `AdmittedMaterialSemanticsUnknown` with the named cause; survivors'
  slots analyze normally.

- [ ] **Step 1: Write the failing capture tests**

Anchor the fixture pattern on `ClosureFailureRetainsGraphFactsButNoPartialEvidence`
(`UnityAnimationEvidenceCaptureTests.cs:1179-1203`) and the
`UnattestedShader(fileName, shaderName)` helper (`:91`):

```csharp
[Test]
public void MixedBatchClosesWithHealthySlotAnalyzableAndPoisonSlotRefused()
{
    // Two-slot renderer. Slot 0: a supported stand-in material.
    // Slot 1: a material from UnattestedShader (supported shader name,
    // unattested source). Capture. Assert: evidence.IsClosed equivalent
    // (post-Task-3: no renderer-wide failure), slot 0's admitted material
    // carries real captured evidence, slot 1 refuses with the named cause.
    // Committed behavior: empty slot results, renderer-wide
    // UnattestedMaterial.
}

[Test]
public void AllPoisonBatchProducesAllSlotsRefusedAndNoRendererWideEntry()
{
    // Every slot poison. Assert: every slot refuses with the named cause;
    // the renderer-level entry, if any, is the all-slots-failed mirror
    // naming the first slot's way; no renderer-wide closure entry exists.
}
```

- [ ] **Step 2: Verify RED**

Run `Alrauna.Amuse.Tests.Editor.Host.UnityAnimationEvidenceCaptureTests`.
Expected: both new tests fail against the committed all-or-nothing return.
Record the failure text.

- [ ] **Step 3: Change the delegate and `DefaultCapturer`**

```csharp
internal delegate bool ClosedAlphaMaterialCapturer(
    IReadOnlyList<Material> materials,
    IReadOnlyList<CapturedAlphaMaterialFamily> families,
    MaterialEvidenceRequest request,
    AlphaPolicyBounds bounds,
    out ClosedAlphaCaptureOutcome outcome);
```

`DefaultCapturer` forwards `out` unchanged
(`UnityAnimationEvidenceCapture.cs:117-127`).

- [ ] **Step 4: Rewrite the batch call site**

Replace `UnityAnimationEvidenceCapture.cs:714-733`:

```csharp
if (!capturer(
        attestedMaterials,
        attestedFamilies,
        captureRequest,
        bounds,
        out var batchOutcome))
{
    throw new InvalidOperationException(
        "Closed material capture failed without naming a material.");
}
if (batchOutcome.Captured.Count
        + batchOutcome.UnattestedOrdinals.Count
    != attestedIndices.Count)
{
    throw new InvalidOperationException(
        "Closed material capture returned an invalid result count.");
}
var failedOrdinals = new HashSet<int>(batchOutcome.UnattestedOrdinals);
foreach (var ordinal in batchOutcome.UnattestedOrdinals)
{
    var admittedIndex = attestedIndices[ordinal];
    var lockedRefusal = lockedRefusalCheck?.Invoke(
        admitted[admittedIndex]) ?? RendererAnalysisRefusal.None;
    capturedByIndex[admittedIndex] =
        UnityMaterialSemantics.UnattestedMaterial(
            lockedRefusal,
            admitted[admittedIndex],
            resolveRegisteredSource);
}
var survivorOrdinal = 0;
for (var index = 0; index < attestedIndices.Count; index++)
{
    if (failedOrdinals.Contains(index))
    {
        continue;
    }
    capturedByIndex[attestedIndices[index]] =
        batchOutcome.Captured[survivorOrdinal];
    survivorOrdinal++;
}
```

Keep the sentinel construction consistent with the selection-failure path at
`:672-684`, including the locked-identity pre-check, so the refusal names the
cause. Delete the `Failed(MaterialDependencyClosureFailure.UnattestedMaterial)`
return; the `Failed` local function loses its last caller and is deleted.

- [ ] **Step 5: Verify GREEN, then the full capture class**

Run the capture test group. Expected: the two new tests pass; every S1-era
test that pinned the renderer-wide return now fails and is re-specified in
Task 3 — record which ones and continue to Task 3 before committing if any
pin the deleted path.

- [ ] **Step 6: Commit (on authorization, together with Task 3 if tests pin the deleted path)**

---

### Task 3: Renderer-wide closure cutover

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs`
  (ctor parameter and property `ClosureFailure`, `IsClosed`)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
  (every evidence construction passes no closure failure)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
  (gate at `:1170`, `ClosureFailureWayFor` at `:1353-1360`, renderer entry
  way at `:839-841`)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:420-438`
  (`ClosureFailureSentence` loses its `UnattestedMaterial` case)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs`
  (enum member `UnattestedMaterial` becomes dead and is removed)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`,
  `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Produces: `CapturedAnimationEvidence` without `ClosureFailure`/`IsClosed`;
  `MaterialDependencyClosureFailure` reduced to
  `None`, `MissingCurrentMaterial`, `SlotOutOfRange`, `InvalidSwapValue`
  (the slot-scoped ways S1 introduced). The sentinel path answers through
  `UnityMaterialSemantics.UnattestedMaterial`, which is a method and keeps
  its name.

- [ ] **Step 1: Re-specify the tests that pin the deleted path**

`FailedClosureExposesNoPartialEvidence` and
`ClosureFailureRetainsGraphFactsButNoPartialEvidence`
(`UnityAnimationEvidenceCaptureTests.cs:1179-1203`) pin the `Failed`
contract. Rewrite them to pin the Task 2 outcome: mixed batch closes with
per-slot refusals and retained graph facts. Any plugin test that drives
`!IsClosed` (search `AmusePlatformFinishPluginTests.cs` for closure
fixtures) becomes a mixed-batch per-slot test. Falsifier markers:

```csharp
// --- Falsifier 1: a capturer that reports no ordinals and fails the
// batch silently must throw the nameless-failure defect, never refuse
// the renderer. ---
// --- Falsifier 2: a sentinel that drops the locked-identity cause must
// fail the named-cause assertion. ---
```

- [ ] **Step 2: Verify RED**

Compile fails first (constructors still require the failure argument where
tests no longer pass it, and vice versa). Observe, then cut over.

- [ ] **Step 3: Cut over the evidence shape**

Remove the `closureFailure` constructor parameter, the `ClosureFailure`
property, and `IsClosed` from `CapturedAnimationEvidence`
(`CapturedAnimationEvidence.cs:184-220`). Remove the enum member
`UnattestedMaterial` from `MaterialDependencyClosureFailure`
(`:8-15`). Update every construction site (the capture success path, S1 slot
paths, tests).

- [ ] **Step 4: Cut over the plugin and reports**

Delete the `!IsClosed` gate and its empty `SlotResults` return
(`AmusePlatformFinishPlugin.cs:1166-1180` region). Delete
`ClosureFailureWayFor`'s renderer-wide branch and its renderer-entry caller
(`:839-841`, `:1353-1360`), keeping the slot-position way sentences
(`:727-756`). Remove the `UnattestedMaterial` case from
`ClosureFailureSentence` (`AmuseReports.cs:435-438`).

- [ ] **Step 5: Run the full Host and Build groups**

Groups: `Alrauna.Amuse.Tests.Editor.Host.UnityAnimationEvidenceCaptureTests`,
`Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests`,
`Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests`.
Expected: green, including both falsifiers and the appended-slot hazard test
from S1, which must still pass unchanged (the guard reads
`SlotClosureFailures`, which now also carries batch-failed slots' refusal
facts only if recorded — it does not; batch failures are sentinel refusals,
so the S1 hazard test pins that no new conjunct was needed).

- [ ] **Step 6: Full product assembly**

Run the full `Alrauna.Amuse.Tests.Editor` EditMode assembly.
Expected: all tests pass except the six documented environment-flake
behaviour-attach fixtures if the editor has not restarted cleanly; record
observed counts either way.

- [ ] **Step 7: Commit Part A (on authorization)**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/ClosedAlphaCaptureOutcome.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs \
  Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs \
  Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs \
  Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
  Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "feat: closed capture failures refuse their own slots; delete the renderer-wide closure path"
```

---

### Task 4: Consent subjects for swap-reachable materials

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
  (consent block relocation `:439-490` to after the graph gate; new
  enumerator next to `AllAssignedMaterials` at `:1080-1093`)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/VersionConsentTests.cs`,
  `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: stored `CommittedControllerGraphResult` (`state.StructuralGraph`),
  `LiveAnimationObservation`'s object-reference reading convention
  (`LiveAnimationObservation.cs:96-97`), `CollectTransferConsent`
  (`UnityMaterialSemantics.cs:481`).
- Produces: consent subjects and granted names now cover swap-only
  materials; the consent dialog runs only after the committed-graph gate
  passes.

- [ ] **Step 1: Write the failing consent tests**

In `VersionConsentTests.cs`, using its `SupportedFacts` fixtures and the
injectable `VersionConsentPresenter` seam:

```csharp
[Test]
public void SwapOnlyMaterialProducesSubjectAndGrantedName()
{
    // A material assigned nowhere, referenced only by an object-reference
    // curve on a stored-graph clip. Run Execute with the presenter recording
    // subjects. Assert: the subject list names the swap-only shader; the
    // granted set contains its name.
}

[Test]
public void GraphRefusedAvatarNeverOpensTheConsentDialog()
{
    // A committed-graph refusal fixture. Assert: the presenter records zero
    // presentations; the avatar refusal is unchanged.
}
```

Add a plugin-level end-to-end test: a granted swap-only name admits the
material through the transferred capture (poison ungranted slot still
refuses per slot from Task 2's behavior). Add the declined-path test: with
the presenter declining, nothing is granted. Consent is all-or-nothing, so
an ungranted build halts before capture; the per-slot refusal of the
ungranted swap-only material is pinned at the capture level by Task 2's
tests, not through Execute. Falsifier:

```csharp
// --- Falsifier 3: an implementation that grants by material reference
// instead of shader name must fail the distinct-materials-same-shader
// case: two materials, one shader, one grant covers both. ---
```

- [ ] **Step 2: Verify RED**

Expected: swap-only subject test fails (no subject today); dialog-order test
fails (dialog opens before the graph gate today).

- [ ] **Step 3: Move the consent block**

Relocate the block `AmusePlatformFinishPlugin.cs:439-490` (consent
collection, granted-aware attestation composition, window eligibility, the
dialog, and `ConsentDeclined`/`ConsentGranted` state writes) to directly
before the swap-in at `:527-537`, which already sits after the graph gate.
The `state.StructuralGraph` inline fallback stays where it is; the consent
block consumes the stored graph.

- [ ] **Step 4: Add the swap-curve enumerator**

```csharp
private static IEnumerable<Material> AssignedAndSwapCurvedMaterials(
    BuildContext context,
    CommittedControllerGraphResult graph)
{
    var seen = new HashSet<Material>();
    foreach (var material in AllAssignedMaterials(context))
    {
        if (seen.Add(material))
        {
            yield return material;
        }
    }
    foreach (var layer in graph.Layers)
    {
        foreach (var clip in layer.Clips)
        {
            foreach (var binding in AnimationUtility
                         .GetObjectReferenceCurveBindings(clip))
            {
                if (!binding.propertyName.StartsWith("m_Materials"))
                {
                    continue;
                }
                foreach (var key in AnimationUtility
                             .GetObjectReferenceCurve(clip, binding))
                {
                    if (key.value is Material material
                        && seen.Add(material))
                    {
                        yield return material;
                    }
                }
            }
        }
    }
}
```

Change the collection call to
`CollectTransferConsent(AssignedAndSwapCurvedMaterials(context, graph))`.
`CollectTransferConsent` already dedupes by shader instance, so the wider
walk cannot duplicate subjects.

- [ ] **Step 5: Verify GREEN**

Run `Alrauna.Amuse.Tests.Editor.Build.VersionConsentTests` and
`Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests`.
Expected: new tests pass; no lifecycle or window test regresses. Record
observed counts.

- [ ] **Step 6: Commit Part B (on authorization)**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/VersionConsentTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "feat: consent pre-scan covers swap-reachable materials and runs after the graph gate"
```

---

### Task 5: Final gates and record

- [ ] **Step 1: Full product assembly** — `Alrauna.Amuse.Tests.Editor`,
  EditMode. Expected: green, or green minus the six documented
  behaviour-attach environment flakes. Record observed counts.
- [ ] **Step 2: Research assembly** — `Alrauna.Amuse.Research.Tests.Editor`.
  Expected: 138 of 138.
- [ ] **Step 3: Identifier sweep** on every changed file: at sign joined to
  hex, drive-letter paths, home paths, four-digit ports, private asset
  names. Every hit is a defect.
- [ ] **Step 4: Update the spec's section 3.7/4.6 with observed RED/GREEN
  counts and append a dated line to
  `docs/superpowers/investigations/2026-10-07-census-refusal-coverage-investigation.md`**
  recording that decisions A1, A2, B1 were implemented, with observed counts.
- [ ] **Step 5: `git diff --check` and final status report.**
