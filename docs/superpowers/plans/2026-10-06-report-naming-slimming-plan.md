# Report Naming Slimming Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the five complexity cuts from the 2026-10-06 branch
review. The branch loses about 40 lines. All cuts keep behavior. The
report text a build emits is byte-identical before and after.

**Architecture:** No architecture change. One report emitter loses an
unused parameter and its caller passes the same value inline. One Host
capture helper adopts the file's ternary idiom. Three test files
replace pasted blocks with row-driven or loop-driven forms.

**Tech Stack:** Unity 2022.3 editor C#, NUnit EditMode tests through
the Unity Test Runner.

**Spec:**
`docs/superpowers/specs/2026-10-06-report-naming-slimming-design.md`

**Working discipline:** Branch `fix/poiyomi-refusal-reporting`, base
`main` at `96b5705`. The reviewed work is uncommitted. Allowed
mutations: the seven files named in the tasks below, and nothing
else.
The uncommitted `.github/workflows/pr.yml` and `README.md` changes are
user-owned. Do not touch, stage, or absorb them. All tasks are
behavior-preserving, so there are no RED steps. The guard is the
existing suite, with observed counts recorded before and after every
task. Commit steps run only with explicit session authorization. Stop
and return evidence when a count changes unexpectedly, a test fails,
a cut turns out to be load-bearing, or a repository search finds a
caller this plan does not name.

## Global Constraints

- No behavior change. No report text, refusal value, template
  position, assert message, literal key, or test name changes. The
  alpha mask case count rises from 1 to 3 by design, so the expected
  suite total rises by exactly 2.
- Never weaken a test assertion. The rewrites change how an assertion
  is expressed, never what it requires.
- A filtered test run that reports 0 tests is a failure. Record
  observed counts for every filter run.
- Unity MCP safety, before any editor use: read
  `mcpforunity://instances`, inspect `Application.dataPath`, require
  an exact match to the repository `Assets` folder, and pin the
  instance when more than one is connected. Tests never run in the
  Census Lab project.
- No absolute or machine-specific paths in code, comments, docs, or
  commit messages.
- Dated records are history. Do not edit the investigation or the
  branch's own design and plan records.

---

### Task 1: The renderer refusal emitter loses the name parameter

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:383-408`
- Modify:
  `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:786-794`
- Modify:
  `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsRendererTests.cs:100-122`

**Interfaces:**
- Consumes: the existing `AmuseReports.ClosureFailureSentence`
  method, unchanged.
- Produces: `RendererRefusal(Renderer, RendererAnalysisRefusal, int,
  int, string detail = null)`. The `{2}` argument is the live
  renderer's name, or null for a null renderer. Tasks 2 through 5 are
  independent of this task.

- [ ] **Step 1: Confirm the caller set**

Search the repository for every caller of `RendererRefusal`. The set
must be exactly `AmusePlatformFinishPlugin.cs` lines 576 and 789 plus
the four calls inside `AmuseReportsRendererTests`. If the search
finds another caller, stop and report.

- [ ] **Step 2: Record the baseline**

Run the `AmuseReportsRendererTests` EditMode filter on the dev editor
instance. Record the observed case count and that all passed. Also
run the full `Alrauna.Amuse.Tests.Editor` assembly once and record
its observed total as the plan-level baseline for Task 6.

- [ ] **Step 3: Edit the emitter**

Apply spec section 3.1. Delete the `rendererName` parameter, the
four-line comment, and the reassignment. Pass
`renderer != null ? renderer.gameObject.name : null` as the `{2}`
argument. Keep the `string.IsNullOrEmpty(detail)` coercion and every
doc comment.

- [ ] **Step 4: Edit the caller**

Apply spec section 3.2 at `AmusePlatformFinishPlugin.cs` line 789.
Delete the `renderer.gameObject.name` argument, so the closure
sentence expression becomes the `detail` argument. The caller at
line 576 needs no edit.

- [ ] **Step 5: Edit the guard test**

Apply spec section 3.3. `ClosureRefusalNamesRendererAndFailureWay`
drops the `"Body"` argument and asserts
`Does.Contain("'AMUSE renderer report'")` plus
`Does.Contain("not a material")`. Both old asserts keep their
strength.

- [ ] **Step 6: Run the guards**

Refresh the dev editor instance. Confirm zero compile errors. Run the
`AmuseReportsRendererTests` filter. The observed count must equal the
Step 2 baseline and all must pass. Run the
`AmusePlatformFinishPluginTests` filter and record that all passed
with its observed count.

- [ ] **Step 7: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs \
  Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsRendererTests.cs
git commit -m "refactor: the renderer refusal emitter derives its own name"
```

---

### Task 2: The texture capture format name uses the ternary idiom

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:1441-1445`

- [ ] **Step 1: Record the baseline**

Run the `UnityMaterialEvidenceCaptureTests` EditMode filter. Record
the observed case count and that all passed.

- [ ] **Step 2: Apply the cut**

Apply spec section 2. Replace the five-line block with:

```csharp
var formatName = texture is Texture2D texture2D
    ? texture2D.format.ToString()
    : null;
```

The `textureName` line above it stays.

- [ ] **Step 3: Run the guard**

Refresh and confirm zero compile errors. Rerun the
`UnityMaterialEvidenceCaptureTests` filter. The observed count must
equal the baseline and all must pass.

- [ ] **Step 4: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs
git commit -m "refactor: derive the capture format name with the file idiom"
```

---

### Task 3: The slot test builds one refusal per reason

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs:136-184`

- [ ] **Step 1: Record the baseline**

Run the `AmuseReportsSlotTests` EditMode filter. Record the observed
case count and that all passed.

- [ ] **Step 2: Apply the cut**

Apply spec section 4. Replace the array of refusals with the
`reasons` array and move the construction into the loop. Keep every
property argument, literal, comment, and assert.

- [ ] **Step 3: Run the guard**

Rerun the `AmuseReportsSlotTests` filter. The observed count must
equal the baseline, the loop must still run three times, and all must
pass.

- [ ] **Step 4: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs
git commit -m "test: build the texture refusal once per reason"
```

---

### Task 4: The string test asserts from one key array

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs:181-218`

- [ ] **Step 1: Record the baseline**

Run the `AmuseReportStringsTests` EditMode filter. Record the
observed case count and that all passed.

- [ ] **Step 2: Apply the cut**

Apply spec section 5. Move the seven literal keys into the `keys`
array and assert in the loop, with the key as the assert message.
Keep the rationale comment and every literal.

- [ ] **Step 3: Run the guard**

Rerun the `AmuseReportStringsTests` filter. The observed count must
equal the baseline and all must pass.

- [ ] **Step 4: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs
git commit -m "test: pin the refusal keys from one array"
```

---

### Task 5: The alpha mask test runs three rows

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiAlphaMaskTests.cs:940-956`

- [ ] **Step 1: Record the baseline**

Run the `PoiyomiAlphaMaskTests` EditMode filter. Record the observed
case count and that all passed.

- [ ] **Step 2: Apply the cut**

Apply spec section 6. The three `[TestCase]` rows carry the three
property names. The body keeps the comment and the one assert.

- [ ] **Step 3: Run the guard**

Rerun the `PoiyomiAlphaMaskTests` filter. The observed count must
equal the baseline plus 2, because the one test becomes three cases.
All must pass.

- [ ] **Step 4: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiAlphaMaskTests.cs
git commit -m "test: label the alpha mask properties with rows"
```

---

### Task 6: Full-suite gate

**Files:**
- Modify: none.

- [ ] **Step 1: Run both test assemblies**

Run the full `Alrauna.Amuse.Tests.Editor` EditMode assembly. The
observed total must equal the plan-level baseline from Task 1 Step 2
plus 2, because Task 5 turns one test into three cases. All must
pass. Run the full
`Alrauna.Amuse.Research.Tests.Editor` assembly and record that all
passed.

- [ ] **Step 2: Check the tree**

`git diff --stat` must name exactly the seven files from Tasks 1
through 5. `git diff --check` must report no whitespace defects. The
user-owned `.github/workflows/pr.yml` and `README.md` changes must
still be present and untouched.

- [ ] **Step 3: Report**

State the five cuts as applied or refused, the observed counts from
every filter run, and any deviation from this plan with its evidence.
