# Generic Core Hygiene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps
> use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the six do-now changes from the 2026-09-29 abstraction
investigation: two dead-code deletions, two mechanical collapses, and two
standard library replacements, with zero observable behavior change.

**Architecture:** All six changes live inside the existing production
assembly. The two collapses keep every public signature and every policy
loop in place and remove only copied middles. The plan never introduces an
interface, a registry, or a new type.

**Tech Stack:** Unity 2022.3.22f1, C# against the .NET Standard 2.1
profile, NUnit through the Unity Test Framework in EditMode mode, Unity
MCP for refresh, compilation, and test runs.

**Spec:** `docs/superpowers/specs/2026-09-29-generic-core-hygiene-design.md`

## Global Constraints

- Branch: `chore/generic-core-hygiene`, base `main` at `0af25ff`. Working
  tree only. No staging, no commits, no push without explicit user
  authorization.
- No observable behavior change. No report string, refusal name, build
  count, materializer output, or `AffineAlphaMap` equality result changes.
- Every deleted `.cs` file deletes its `.meta` file in the same task.
- Unity MCP discipline: before any tool use against an editor instance,
  enumerate reachable instances read-only, inspect `Application.dataPath`,
  and require the exact match `<repo-root>/Assets`. Stop on any mismatch.
- A focused filter that reports zero tests is a failure, with the one
  documented exception in Task 1.
- Record observed counts and dates in this file's Results section as each
  task completes. Attribute every failure against the Task 0 baseline's
  known environment list. Any unattributed failure stops the run.
- Run `git diff --check` before reporting.

---

## Task 0: baseline

**Files:** none modified.

- [ ] **Step 1: Refresh Unity and confirm a clean console**

Refresh the asset database and require compilation with zero errors in the
console.

- [ ] **Step 2: Run the full EditMode population**

Run both assemblies: `Alrauna.Amuse.Tests.Editor` and
`Alrauna.Amuse.Research.Tests.Editor`. Record both totals and every
failure with its message.

- [ ] **Step 3: Record the cutout class count**

Run the filter `ToonMaterialCutoutSemanticsTests`. Record the observed
count. Task 7 subtracts exactly this number from the product total.

- [ ] **Step 4: Write the environment failure list**

Write the failing test names from Step 2 into the Results section as the
known environment list. Every later task compares against this list.

## Task 1: delete the dead cutout pair

**Files:**
- Delete: `Packages/com.alrauna.amuse/Editor/Semantics/ToonMaterialCutoutSemantics.cs` and `.meta`
- Delete: `Packages/com.alrauna.amuse/Editor/Semantics/MaterialCutoutSpecification.cs` and `.meta`
- Delete: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ToonMaterialCutoutSemanticsTests.cs` and `.meta`

**Interfaces:** consumes nothing. Produces the absence of the three
records. No other file references them. The design's D1 carries the
verification.

- [ ] **Step 1: Confirm zero references one last time**

Search both packages for `ToonMaterialCutoutSemantics`,
`MaterialCutoutSpecification`, and `EvaluateCutoutSpecification`. Expected:
only the three files being deleted.

- [ ] **Step 2: Delete the three files and their `.meta` files**

- [ ] **Step 3: Refresh Unity and confirm compilation**

- [ ] **Step 4: Run the deletion proof**

Run the filter `ToonMaterialCutoutSemanticsTests`. Expected: zero tests.
This is the documented exception to the zero-test rule: the class is gone,
so the zero result together with the Step 1 search is the proof. Also run
`ToonMaterialCutoutSemantics` as a type search across
`Packages/com.alrauna.amuse` and require zero hits.

## Task 2: delete the unread family record

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs:296-306,340-350`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:339-341,415-419,462-465`

**Interfaces:** consumes nothing. Produces a `PreparedSlotSeparation`
constructor without the `familyOfAdmitted` parameter and without the
`FamilyOfAdmitted` property.

- [ ] **Step 1: Remove the record surface**

In `AlphaSeparationRecords.cs`, delete the `familyOfAdmitted` constructor
parameter, its null-guard assignment, and the `FamilyOfAdmitted` property
with its doc block. Keep every other parameter and guard byte for byte.

- [ ] **Step 2: Remove the population chain**

In `AlphaSeparationPreparation.cs`, delete the dictionary declaration, the
`familyOfAdmitted.Add(live, captured.Family);` call, and the constructor
argument. Reword the two-map comment block so it describes only the output
map: the same successfully mapped admitted source feeds the output map,
and the output map never names a source it lacks.

- [ ] **Step 3: Refresh Unity and confirm compilation**

The construction site count is one, so a missed site fails here.

- [ ] **Step 4: Run the focused filters**

Run `AlphaSeparationPreparationTests` and `AlphaSeparationApplyTests`.
Expected: green, unchanged counts. Record the counts.

## Task 3: collapse the twin dispatch

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
  (`AnalyzeAlphaMaterial`, `AnalyzeAlphaMaterialTransferred`)

**Interfaces:** consumes the existing family switch bodies. Produces:

```csharp
private static CapturedAlphaSemantics AnalyzeAlphaMaterialCore(
    CapturedAlphaMaterial captured,
    bool verifyIdentity)

internal static CapturedAlphaSemantics AnalyzeAlphaMaterial(
    CapturedAlphaMaterial captured)

internal static CapturedAlphaSemantics AnalyzeAlphaMaterialTransferred(
    CapturedAlphaMaterial captured)
```

- [ ] **Step 1: Rename the trusted body to the core and add the gate**

Rename `AnalyzeAlphaMaterial`'s body to `AnalyzeAlphaMaterialCore` with the
`bool verifyIdentity` parameter. Keep the null guard, the switch, the
`UnknownWithShaderReason` arms, and the `MaterialSemantics` construction
byte for byte. Change each verification gate to `verifyIdentity && ...`
in the two Poiyomi arms and to
`captured.LilToonEvidence == null || (verifyIdentity && !...)` in the
three lilToon arms, per the design's D3 code.

- [ ] **Step 2: Write the two wrappers**

`AnalyzeAlphaMaterial` calls the core with `true`. 
`AnalyzeAlphaMaterialTransferred` calls the core with `false`. Keep both
doc blocks. Add one sentence to the transferred doc: the verification gate
is skipped by the parameter, not by a second switch. Move the transferred
body's Poiyomi comments (the struct guarantee and the consent coverage)
into the core beside the arms they explain.

- [ ] **Step 3: Refresh Unity and confirm compilation**

- [ ] **Step 4: Run the focused filters**

Run `UnityMaterialSemanticsTests` and `AmusePlatformFinishPluginTests`.
Expected: green, unchanged counts. The class holds both the trusted-path
`UnattestedShader` cases and the transferred-path consent cases. Record
the counts.

## Task 4: collapse the capture batch core

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
  (`CaptureAlphaMaterials`, `TryCaptureClosedAlphaMaterials`,
  `TryCaptureClosedAlphaMaterialsTransferred`)

**Interfaces:** consumes `BuildCapturedAlphaMaterials` and
`AlphaPredicateRequestFor` unchanged. Produces:

```csharp
private static IReadOnlyList<CapturedAlphaMaterial> CaptureBatch(
    IReadOnlyList<Material> materials,
    IReadOnlyList<CapturedAlphaMaterialFamily> families,
    IReadOnlyList<MaterialEvidenceRequest> requests,
    AlphaPolicyBounds bounds,
    RegisteredSourceLookup resolveRegisteredSource)
```

- [ ] **Step 1: Write the core**

Move the shared middle into `CaptureBatch`: build `shaders` and `inputs`
with `AlphaPredicateRequestFor`, call
`UnityMaterialEvidenceCapture.Capture(inputs)` when `bounds` is null and
`Capture(inputs, bounds)` otherwise, then call
`BuildCapturedAlphaMaterials` and return its result.

- [ ] **Step 2: Rewire the three entries**

`CaptureAlphaMaterials` keeps its null guard and its classify loop that
fills `families` and per-material requests (`classified.alpha ?? 
EmptyEvidenceRequest`), then calls `CaptureBatch` with `bounds: null`.
`TryCaptureClosedAlphaMaterials` keeps its guards, fills both arrays from
its shared request and family parameters, calls `CaptureBatch`, and keeps
its all-must-attest loop. `TryCaptureClosedAlphaMaterialsTransferred`
keeps its guards, its granted-name loop, and its doc. No signature
changes. No attestation loop moves into the core.

- [ ] **Step 3: Refresh Unity and confirm compilation**

- [ ] **Step 4: Run the focused filters**

Run `UnityMaterialSemanticsTests`, `UnityAnimationEvidenceCaptureTests`,
and `AmusePlatformFinishPluginTests`. Expected: green, unchanged counts.
The cutout split cases in the Semantics assembly prove the predicate
request wiring survived: a core that drops it makes a sibling field
binarize and fails those cases. Record the counts.

## Task 5: replace the bit length helpers

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/AffineAlphaMap.cs:144`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs:387`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AffineUvTransform.cs:275`

**Interfaces:** consumes `BigInteger.GetBitLength()` from the
.NET Standard 2.1 profile. Produces the same values at every call site.
The design's D6a carries the per-call-site sign-domain argument.

- [ ] **Step 1: Replace the three bodies**

In each file, delete the private `BitLength` method and change its call
sites to `bitLength` of the same expression through
`BigInteger.GetBitLength(...)`. The calls already pass magnitudes or
positive denominators, so each site changes from `BitLength(x)` to
`x.GetBitLength()` with no added guard.

- [ ] **Step 2: Refresh Unity and confirm compilation**

If `GetBitLength` is unavailable, stop and record. The fallback is an
owner decision per the design's stop condition 2.

- [ ] **Step 3: Run the focused filters**

Run `AffineAlphaMapTests` and `AlphaEvidenceClassifierIntegrationTests`.
Expected: green, unchanged counts. Record the counts.

## Task 6: cast the blend and compare constants

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs`
  (`LilToonOpaqueConversionFactors`)
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`

**Interfaces:** consumes `UnityEngine.Rendering` enums. Produces the same
constant values. Enum-to-float casts are constant expressions, so every
`const` survives.

- [ ] **Step 1: Cast the lilToon table**

Replace the four blend factors, two blend operations, two depth
comparisons, the color mask, and the depth write flag with casts, for
example:

```csharp
internal const float BlendOpAdd = (float)UnityEngine.Rendering.BlendOp.Add;
internal const float BlendFactorSrcAlpha =
    (float)UnityEngine.Rendering.BlendFactor.SrcAlpha;
internal const float LEqualDepthComparison =
    (float)UnityEngine.Rendering.CompareFunction.LessEqual;
internal const float ColorMaskAll =
    (float)UnityEngine.Rendering.ColorWriteMask.All;
internal const float DepthWriteOn =
    (float)UnityEngine.Rendering.DepthWrite.On;
```

Delete the comment lines that spell the numeric values. Keep both
predicate methods and the file's scope doc unchanged.

- [ ] **Step 2: Cast the Poiyomi mirror**

Apply the same casts to the five blend and two comparison constants in
`PoiyomiOpaqueConversion.cs`. Delete the value-spelling comment. Keep the
two predicate methods unchanged.

- [ ] **Step 3: Refresh Unity and confirm compilation**

- [ ] **Step 4: Run the focused filters**

Run `LilToonOpaqueConversionResultTests`, `LilToonCutoutSourceEligibilityTests`,
`LilToonTransparentSourceEligibilityTests`, `LilToonOpaqueTargetTests`,
and `PoiyomiOpaqueConversionTests`. Expected: green, unchanged counts.
Record the counts.

## Task 7: full validation and records

**Files:** none modified beyond the tasks above.

- [ ] **Step 1: Run the full EditMode population**

Run both assemblies. Expected product delta from Task 0: minus exactly the
Task 1 Step 3 observed count, no other change. Research total unchanged.
Every failure must be on the Task 0 known environment list. Any other
failure stops the work.

- [ ] **Step 2: Check the diff**

Run `git diff --check`. Confirm the changed-file list contains exactly the
tasks' targets and no `.meta` orphan remains for any deleted file.
Confirm no file outside `Packages/com.alrauna.amuse/` changed.

- [ ] **Step 3: Sweep every changed file for identifiers**

Sweep for an at sign joined to a hexadecimal hash, drive-letter paths,
home-directory paths, four-digit ports, and every private asset name known
to the session. Every hit is a defect. Fix hits before reporting.

- [ ] **Step 4: Record the results**

Write the observed per-task counts, the full-suite delta, the diff check
result, and the sweep result into the Results section with the date.

## Stop conditions

The design's stop conditions apply, restated for the executor: stop on any
observable behavior change, on a `GetBitLength` compile failure, on a new
consumer of any deleted surface, on any full-suite failure outside the
Task 0 environment list, on any diff outside the target list, and on any
need for a Unity instance other than the dev editor instance.

## Expected report

The changed files per task, the observed focused and full counts with
dates, the Task 0 environment failure list, the deletion proof from Task
1, the diff check result, the identifier sweep result, and the remaining
limits. State that nothing was staged or committed and that no Unity
instance other than the dev editor instance was touched.

## Results

To record at execution time. The baseline counts, the per-task filter
counts, the full-suite delta, the diff check, and the sweep result go
here, each with its date.

### Task 0 baseline, 2026-09-29

The dev editor instance refreshed cleanly. The console held zero errors
after the refresh.

Full EditMode population, both assemblies in one run: 2437 tests, 2437
passed, 0 failed, 0 skipped.

- `Alrauna.Amuse.Tests.Editor`: 2299 tests, 2299 passed, 0 failed,
  0 skipped.
- `Alrauna.Amuse.Research.Tests.Editor`: 138 tests, 138 passed,
  0 failed, 0 skipped.

Each assembly also ran alone. The separate totals match the combined
total: 2299 + 138 = 2437.

Known environment failure list: empty. The full population ran with
zero failures, so there is no failing test name to record. Every later
task compares against this empty list. Any new failure stops the run.

Focused filter `ToonMaterialCutoutSemanticsTests` on 2026-09-29:
observed 4 tests, 4 passed, 0 failed. Task 7 subtracts this 4 from the
product total.

### Task 1, 2026-09-29

Step 1 search on 2026-09-29, before deletion: both packages searched for
`ToonMaterialCutoutSemantics`, `MaterialCutoutSpecification`, and
`EvaluateCutoutSpecification`. Every hit sat inside the three files
marked for deletion. No other file referenced any of the three names.

Step 2 on 2026-09-29: deleted the three source files and their three
`.meta` files. The working tree holds exactly those six deletions and no
other tracked change.

Step 3 on 2026-09-29: the dev editor instance was pinned after its
`Application.dataPath` matched this checkout's Assets folder. The refresh
completed and the editor sat idle. The console was cleared and a second
refresh ran. The console then held zero error entries. Compilation is
clean. Error entries from before the clear were environment noise from
an earlier reload plus one pre-existing warning. They held no compile
error and no reference to a deleted name.

Step 4 deletion proof on 2026-09-29:

- Filter `ToonMaterialCutoutSemanticsTests` in EditMode: observed
  0 tests, 0 passed, 0 failed, 0 skipped. This is the documented
  exception. Together with the Step 1 search it proves the class is gone.
- Type search for `ToonMaterialCutoutSemantics` across
  `Packages/com.alrauna.amuse`: 0 hits.
- Follow-up search for all three identifiers across both packages:
  0 hits.

`git diff --check` on 2026-09-29 reported no output. The changed-file
list holds only the six deletions. No `.meta` orphan remains. Nothing
was staged or committed.

### Task 2, 2026-09-29

Step 1 and Step 2 on 2026-09-29: deleted the unread family record and
its whole plumbing chain. In
`Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs` the
`PreparedSlotSeparation` constructor lost the `familyOfAdmitted`
parameter, its null-guard assignment, and the `FamilyOfAdmitted`
property with its doc block. In
`Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`
the dictionary declaration, the `familyOfAdmitted.Add(live,
captured.Family);` call, and the constructor argument are gone. The
comment above the kept output-map `Add` was reworded. It now says the
same successfully mapped admitted source feeds the output map, and the
output map never names a source it lacks. Every other parameter, guard,
doc block, and code path is byte for byte unchanged. The working tree
diff for this task holds the two Build files and nothing else.

Step 3 on 2026-09-29: the dev editor instance was pinned after its
`Application.dataPath` was read inside the editor and matched this
checkout's Assets folder. The other reachable instance was never used.
The refresh completed and left the editor idle with a clean compile.
The console held zero compile errors. Error entries were pre-existing
environment noise plus one pre-existing warning. None named a removed
symbol.

Step 4 on 2026-09-29, filter counts. Task 0 recorded assembly totals
only, so these are the first recorded per-fixture counts for the two
fixtures. Task 2 changed no test file.

- Both fixtures in one run: 82 tests, 82 passed, 0 failed, 0 skipped.
- `AlphaSeparationPreparationTests` alone: 56 tests, 56 passed,
  0 failed, 0 skipped.
- `AlphaSeparationApplyTests` alone: 26 tests, 26 passed, 0 failed,
  0 skipped.
- Split check: 56 + 26 = 82. The separate totals match the combined
  total. Every run ended with result state Passed.

The known environment failure list stays empty. Zero failures.

Post-deletion search on 2026-09-29: `familyOfAdmitted` and
`FamilyOfAdmitted` across `Packages/com.alrauna.amuse`: 0 hits.

`git diff --check` on 2026-09-29 reported no output. The changed-file
list holds the two modified Build files. Nothing was staged or
committed. No Unity instance other than the dev editor instance was
touched.

### Task 3, 2026-09-29

Step 1 and Step 2 on 2026-09-29: collapsed the twin dispatch in
`Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`.
The trusted body became `AnalyzeAlphaMaterialCore`, a private static
method with a `bool verifyIdentity` parameter. The null guard, the
family switch, every `UnknownWithShaderReason` arm, and the
`MaterialSemantics` construction stayed byte for byte. The two Poiyomi
gates gained the `verifyIdentity &&` prefix. The three lilToon gates
gained the `captured.LilToonEvidence == null ||` first operand they
already had, and their second operand became
`(verifyIdentity && !TryVerify...)`. The transferred body's two Poiyomi
comments moved into the core beside the arms they explain.
`AnalyzeAlphaMaterial` is now a wrapper that calls the core with
`true`. `AnalyzeAlphaMaterialTransferred` is now a wrapper that calls
the core with `false`. Its doc block gained one sentence: the
verification gate is skipped by the parameter, not by a second switch.
Both public signatures are unchanged.

Step 3 on 2026-09-29: the dev editor instance was pinned after its
`Application.dataPath` was read inside the editor and matched this
checkout's Assets folder. The other reachable instance was never used.
The console was cleared, then the refresh ran with a compile request.
A domain reload completed. The console then held zero compile errors.
Its error entries were pre-existing environment noise only. Reflection
inside the compiled domain found the private two-parameter core plus
both one-parameter public methods. Compilation is clean.

Step 4 on 2026-09-29, filter counts, EditMode:

- Both fixtures in one run: 104 tests, 104 passed, 0 failed, 0 skipped.
- `UnityMaterialSemanticsTests` alone: 28 tests, 28 passed, 0 failed,
  0 skipped.
- `AmusePlatformFinishPluginTests` alone: 76 tests, 76 passed,
  0 failed, 0 skipped.
- Split check: 28 + 76 = 104. The separate totals match the combined
  total. Every run ended with result state Passed.

The known environment failure list stays empty. Zero failures.

`git diff --check` on 2026-09-29 reported no output. The working tree
still holds the Task 1 deletions and the Task 2 modifications. This
task added the one modified Semantics file and no other change.
Nothing was staged or committed. No Unity instance other than the dev
editor instance was touched.

### Task 4, 2026-09-29

Step 1 and Step 2 on 2026-09-29: collapsed the capture batch core in
`Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`.
One private core now holds the shared middle of the three capture
entries. The core builds the shaders array and the inputs array with
`AlphaPredicateRequestFor`, runs one evidence capture, and builds the
captured batch. A null bounds selects the no-bounds capture overload,
so the ordinary capture keeps the inert bounds path it had. No
attestation loop moved into the core. All doc blocks survive.

The core is `CaptureBatch`, a private static method with five
parameters in the brief's order: materials, families, requests,
bounds, and the registered-source lookup. One deviation from the
brief's type text: the bounds parameter is `AlphaPolicyBounds?`, the
nullable form of the struct. The brief asks the core to treat a null
bounds as the no-bounds selection. `AlphaPolicyBounds` is a readonly
struct, so a plain struct parameter cannot hold null and cannot
compile the null check. The nullable form is the only C# shape that
keeps that dispatch. The entries pass their struct bounds unchanged,
and the ordinary entry passes null. No entry signature changed.

`CaptureAlphaMaterials` kept its null guard and its classify loop.
The loop fills the families array and a per-material requests array.
The per-material request stays `classified.alpha ?? EmptyEvidenceRequest`.
The entry then calls the core with a null bounds.
`TryCaptureClosedAlphaMaterials` kept its four guards. It fills the
requests array from its shared request parameter, calls the core with
its bounds, and keeps its all-must-attest loop.
`TryCaptureClosedAlphaMaterialsTransferred` kept its four guards, its
granted-name loop, and its doc block. The granted loop reads the
shader name from the material itself. That expression matches the
shader fill the core performs, so the admitted-name decision is
unchanged.

Step 3 on 2026-09-29: the dev editor instance was pinned after its
`Application.dataPath` was read inside the editor and matched this
checkout's Assets folder. The other reachable instance was never
routed to and never touched. The refresh ran with a compile request
and waited for readiness. A domain reload completed. The console held
zero compile errors. Its error entries were pre-existing environment
noise only, from the memory-protection exceptions and one
arm64-incompatible audio plugin. Reflection inside the compiled
domain found the private five-parameter core plus the three entries
with two, six, and seven parameters. Compilation is clean. The test
bridge dropped once during the runs and reconnected as the same dev
editor instance. The run that was in flight had already finished
before the drop.

Step 4 on 2026-09-29, filter counts, EditMode:

- `UnityMaterialSemanticsTests`: 28 tests, 28 passed, 0 failed,
  0 skipped, result state Passed.
- `UnityAnimationEvidenceCaptureTests`: 83 tests, 83 passed, 0 failed,
  0 skipped, result state Passed.
- `AmusePlatformFinishPluginTests`: 76 tests, 76 passed, 0 failed,
  0 skipped, result state Passed.

The cutout split cases in the Semantics assembly passed. The predicate
request wiring survived the collapse. Zero failures. The known
environment failure list stays empty.

`git diff --check` on 2026-09-29 reported no output. This task's diff
holds the one modified Semantics file and nothing else. The working
tree still holds the Task 1 deletions, the Task 2 modifications, and
the Task 3 modifications from their own tasks. Nothing was staged or
committed. No Unity instance other than the dev editor instance was
touched.

### Task 5, 2026-09-29

Step 1 on 2026-09-29: all three private `BitLength` helpers were
deleted and all nine call sites were swapped to `GetBitLength` on the
same argument expressions. `AffineAlphaMap.cs` lost its byte-array
variant. `ExactUvGeometry.cs` and `AffineUvTransform.cs` each lost a
shift loop. No guard was added. No other file changed. A package-wide
search after the edits found no reference to the old helper.

Step 2 on 2026-09-29: the compile gate failed. The dev editor instance
was pinned after an in-editor `Application.dataPath` check matched this
checkout's Assets folder. The other reachable instance was never routed
to and never touched. The refresh ran with a compile request and waited
for readiness. The compiler reported nine CS1061 errors on
`GetBitLength`, one per swapped call site, across the three files. The
project sets `apiCompatibilityLevel: 6`, yet Unity 2022.3.22f1 does not
expose the method. The observed result overrides the design's
availability claim. This triggers the design's stop condition 2. The
exact error list is in the task report.

The controller ruled under the SDD loop on 2026-09-29 to execute the
design's pre-declared fallback. The three files were restored with
`git restore`, a working-tree operation on exactly those paths. The
diff against `0af25ff` for those paths was empty after the restore.
One provenance comment line was added above each helper. The line
names the missing method, the measurement date of 2026-09-29, and a
revisit on a Unity upgrade. The final diff is three insertions, one
line per file, comments only.

Step 2 again on 2026-09-29: the refresh ran with a compile request.
The transport timed out once while a domain reload ran. The editor
state was polled after the reload and reported ready for tools. The
console held zero compile errors. Its entries were the known
environment noise only, the same list Task 4 recorded.

Step 3 on 2026-09-29, filter counts, EditMode:

- `AffineAlphaMapTests`: 9 tests, 9 passed, 0 failed, 0 skipped,
  result state Passed.
- `AlphaEvidenceClassifierIntegrationTests`: 5 tests, 5 passed,
  0 failed, 0 skipped, result state Passed.

Task 5 lands as a documented no-change with provenance comments. The
design's stdlib finding is falsified for this Unity profile. The
project sets `apiCompatibilityLevel: 6`, yet Unity 2022.3.22f1 does
not expose `BigInteger.GetBitLength`. The full details are in the
task report.

`git diff --check` on 2026-09-29 reported no output. Nothing was
staged or committed. No Unity instance other than the dev editor
instance was touched.

### Task 6, 2026-09-29

Step 1 and Step 2 on 2026-09-29: replaced the hand-copied blend and
compare literals with enum casts in the two frontend files. In
`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs`
the factors class now casts nine constants. `BlendOpAdd` and
`BlendOpMax` cast `UnityEngine.Rendering.BlendOp`. The four blend
factor constants cast `UnityEngine.Rendering.BlendMode`. The two
depth comparison constants cast `UnityEngine.Rendering.CompareFunction`.
`ColorMaskAll` casts `UnityEngine.Rendering.ColorWriteMask`. In
`Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`
the same casts landed on its seven private constants: one blend
operation, four blend factors, and the two depth comparisons. The
value-spelling comment blocks were deleted in both files. The
predicate methods and the class scope docs are unchanged. Every
constant kept its name, its `const` form, and its value. The enum
member values were read inside the editor and match the deleted
literals exactly, so the change is value-identical.

One deviation from the brief, endorsed by the controller under the
SDD loop. The brief names `UnityEngine.Rendering.BlendFactor` and
`UnityEngine.Rendering.DepthWrite`. Both are absent in Unity
2022.3.22f1. The first compile reported nine CS0234 errors, one per
use of the two missing names. A reflection search found `BlendMode`
with the same member names and the same numeric values, so the blend
casts name `BlendMode`. No depth write enum exists anywhere in the
profile, so `DepthWriteOn` stays the literal `1f` with a one-line
provenance comment that names the absent enum and the date. This
follows the ruled Task 5 fallback pattern.

Step 3 on 2026-09-29: the dev editor instance was pinned after an
in-editor `Application.dataPath` check matched this checkout's Assets
folder. The instance list held exactly one entry. The refresh ran
with a compile request. The transport timed out once while a domain
reload ran, and the editor state poll reported ready for tools. The
console held zero compile errors on the final text. Its entries were
the known environment noise only, the same list Tasks 4 and 5
recorded.

Step 4 on 2026-09-29, filter counts, EditMode:

- `LilToonOpaqueConversionResultTests`: 2 tests, 2 passed, 0 failed,
  0 skipped, result state Passed.
- `LilToonCutoutSourceEligibilityTests`: 40 tests, 40 passed,
  0 failed, 0 skipped, result state Passed.
- `LilToonTransparentSourceEligibilityTests`: 48 tests, 48 passed,
  0 failed, 0 skipped, result state Passed.
- `LilToonOpaqueTargetTests`: 16 tests, 16 passed, 0 failed,
  0 skipped, result state Passed.
- `PoiyomiOpaqueConversionTests`: 100 tests, 100 passed, 0 failed,
  0 skipped, result state Passed.

`git diff --check` on 2026-09-29 reported no output for the two
files. The change touches only the two constant blocks. Nothing was
staged or committed. No Unity instance other than the dev editor
instance was touched. The full details are in the task report.

### Task 7, 2026-09-29

Step 1 on 2026-09-29: the dev editor instance was pinned after an
in-editor `Application.dataPath` check matched this checkout's
Assets folder. The console was cleared and the refresh ran with a
compile request. A domain reload completed and the editor reported
ready for tools. The console then held seven error entries and no
compile error. The entries were the known environment noise only:
the memory-protection exceptions and the arm64-incompatible audio
plugin. The same noise list held after the full runs.

Full population, EditMode, 2026-09-29:

- Both assemblies in one run: 2433 tests, 2433 passed, 0 failed,
  0 skipped, result state Passed. The run took about 419 seconds.
- `Alrauna.Amuse.Tests.Editor` alone: 2295 tests, 2295 passed,
  0 failed, 0 skipped, result state Passed. The run took about
  393 seconds.
- `Alrauna.Amuse.Research.Tests.Editor` alone: 138 tests, 138
  passed, 0 failed, 0 skipped, result state Passed.
- Split check: 2295 + 138 = 2433. The separate totals match the
  combined total.

Delta against the Task 0 baseline of 2026-09-29: the product
assembly went from 2299 to 2295, exactly the four deleted cutout
tests of Task 1. The research assembly is unchanged at 138. The
combined total went from 2437 to 2433. The Task 0 known environment
failure list is empty and zero failures occurred, so no failure
needed attribution.

Step 2 on 2026-09-29: `git diff --check` reported no output. The
changed-file list holds exactly the task targets: the six deleted
paths of Task 1, the two modified Build files of Task 2, the one
modified Semantics hub of Tasks 3 and 4, the three comment-only
exact-math insertions of Task 5, and the two frontend constant
files of Task 6. No `.meta` orphan remains for any deleted file.
No file outside `Packages/com.alrauna.amuse/` changed except the
untracked plan, spec, and investigation record. Nothing was staged
or committed.

Step 3 on 2026-09-29: the eight modified production files were
swept for an at sign joined to a hexadecimal hash, drive-letter
paths, home-directory paths, four-digit ports, and every private
asset name known to the session. Zero hits. The six deleted paths
hold nothing to sweep.

Record corrections on 2026-09-29: the Task 5 report status line
now records the ruled completion. The Task 6 report summary now
counts nine of the ten lilToon constants as casts with one kept
literal. The investigation record carries three dated updates: the
falsified BitLength swap, the landed blend-constants replacement,
and the net estimate reduced from about -310 to about -295.

Transport notes: the test bridge dropped once after the combined
run and reconnected as the same dev editor instance. It was
re-pinned after a fresh in-editor `Application.dataPath` check.
One start request for the research run timed out at the transport
and left no job. The retry started the run. No Unity instance
other than the dev editor instance was touched. Nothing was staged
or committed. The full details are in the task report.
