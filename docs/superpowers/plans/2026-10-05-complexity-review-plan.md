# Complexity Review Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the eight complexity cuts from the 2026-10-05 branch
review. The branch loses about 95 lines and keeps its behavior.

**Architecture:** No architecture change. Two production files shrink
their internals, one production file loses a constant and a dead
disjunct, two test files lose repeated scaffolding, one field loses
unused visibility.

**Tech Stack:** Unity 2022.3 editor C#, Newtonsoft JSON, NUnit EditMode
tests.

**Spec:** `docs/superpowers/specs/2026-10-05-complexity-review-design.md`

**Working discipline:** Branch `feature/optimizer-ui-rework`, review
point `0891286`. Allowed mutations: the five files named in the tasks
below. The uncommitted change to `.github/workflows/pr.yml` is user
-owned. Do not touch, stage, or absorb it. Commit steps run only with
explicit session authorization. Every cut is behavior-preserving, so
there are no RED steps. The guard is the existing suite. Record
observed test counts before and after each task. Stop and return
evidence when a count changes, a test fails, or a cut turns out to be
load-bearing.

## Global Constraints

- No behavior change. No refusal value, schema value, label text, or
  assert message text changes.
- The deferred behavior observation in investigation section 6 is out
  of scope. Do not fix it here.
- A filtered test run that reports 0 tests is a failure. Record
  observed counts.
- Unity MCP safety, before any editor use: read
  `mcpforunity://instances`, inspect `Application.dataPath`, require an
  exact match to the repository `Assets` folder, and pin the instance
  when more than one is connected. Tests never run in the Census Lab
  project.
- No absolute or machine-specific paths in code, comments, docs, or
  commit messages.
- Never weaken or delete a test assertion. The cuts rewrite how an
  assertion is expressed, never what it requires.

---

### Task 1: Collapse the preset parser readers

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs`

**Interfaces:**
- Produces: `private static bool TryRead<T>(JObject obj, string key,
  JTokenType expected, ref PresetLoadRefusal refusal, out T value)`.
- Deletes: `TryReadObject`, `TryReadInt`, `TryReadBool`, and the dead
  refusal reset on the success path.

- [ ] **Step 1: Record the baseline**

Run the `PresetParserTests` EditMode filter on the dev editor
instance. Record the observed test count and that all passed.

- [ ] **Step 2: Delete the dead reset**

Remove the `refusal = PresetLoadRefusal.None;` line immediately before
the success `return true` in `TryParse`. The top-of-method assignment
stays.

- [ ] **Step 3: Add the generic reader**

Add `TryRead<T>` with the shared presence and type-refusal shape from
spec section 2. Rebuild `TryReadNonEmptyString` on top of
`TryRead<string>` plus its empty check. Convert every call site.
Delete `TryReadObject`, `TryReadInt`, and `TryReadBool`.

- [ ] **Step 4: Run the guard**

Rerun the `PresetParserTests` filter. The observed count must equal
the baseline count and all must pass. Any difference stops the task.

- [ ] **Step 5: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs
git commit -m "refactor: collapse the preset parser typed readers"
```

---

### Task 2: Tighten the file store visibility

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Presets/PresetFileStore.cs`

- [ ] **Step 1: Make FileNames private**

Change `internal static readonly string[] FileNames` to `private`. No
other edit.

- [ ] **Step 2: Compile and run the preset folder**

Refresh the dev editor instance. Confirm zero compile errors. Run the
`Tests/Editor/Presets` EditMode folder filter. Record the observed
count and that all passed.

- [ ] **Step 3: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/Presets/PresetFileStore.cs
git commit -m "refactor: keep the preset file list private"
```

---

### Task 3: Cut the header constant and the dead disjunct

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`

**Interfaces:**
- Deletes: `ProductSubtext` and the `_presets == null` disjunct in
  `DrawPresetRow`.

- [ ] **Step 1: Inline the subtext**

Move the literal into the `DrawHeader` call site with the one-line
plain-English comment from spec section 4. Delete the constant and its
doc comment block.

- [ ] **Step 2: Shrink the guard**

Change the `DrawPresetRow` guard to the refusal comparison only. The
lazy load above it stays.

- [ ] **Step 3: Smoke check the inspector**

Open the `AmuseAvatarOptimizer` inspector on the dev editor instance.
Confirm the subtext line under the title reads the same text as before
and the version label is unchanged. Set one component field so a
preset mismatch shows the preset row, then confirm the row draws. Do
not edit preset JSON files during this check.

- [ ] **Step 4: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs
git commit -m "refactor: inline the subtext and drop a dead guard"
```

---

### Task 4: Extract the refusal test helper

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs`

- [ ] **Step 1: Add the helper**

Add `private static void Refuses(string json, PresetLoadRefusal
expected)` from spec section 5.

- [ ] **Step 2: Convert the fifteen refusal tests**

Replace each repeated assert pair with one `Refuses` call. Keep every
test method name and every expected refusal value. Convert the five
range tests to reuse `WithMipCap` and `WithMinTextureSize` in place of
hand-rolled `Replace` calls.

- [ ] **Step 3: Run the guard**

Run the `PresetParserTests` filter. The observed count must still be
17 and all must pass.

- [ ] **Step 4: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs
git commit -m "refactor: extract the preset refusal test helper"
```

---

### Task 5: Shrink the switched-off assertions

**Files:**
- Modify:
  `Packages/com.alrauna.amuse/Tests/Editor/Build/AaoMergedConsumptionTests.cs`

- [ ] **Step 1: Replace the set assert**

Assert `state.Separation?.CreatedClones` with `Is.Null.Or.Empty` and
the verbatim message from spec section 6. Delete the
`HashSet<Material>` wrapper.

- [ ] **Step 2: Assert the fixture renderer directly**

Capture the `CreateSkinnedRenderer` return value. Assert its
`sharedMaterials` with the existing message text. Delete the
`GetComponentsInChildren` sweep and the null-mesh guard.

- [ ] **Step 3: Run the guard**

Run the `AaoMergedConsumptionTests` filter. Record the observed count
and that all passed.

- [ ] **Step 4: Commit (authorized sessions only)**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AaoMergedConsumptionTests.cs
git commit -m "refactor: assert the switched-off state directly"
```

---

### Task 6: Full validation

- [ ] **Step 1: Run the product suite**

Run the full `Alrauna.Amuse.Tests.Editor` assembly in EditMode on the
dev editor instance. Record the observed count and that all passed.

- [ ] **Step 2: Check the diff**

Run `git diff --check`. Confirm the branch diff touches only the five
allowed files after `0891286`. Confirm `pr.yml` is untouched.

- [ ] **Step 3: Report**

Report the observed counts, the net line change against the review
estimate of about 95, and any cut that was not applied with the
reason.
