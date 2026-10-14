# Simplified Material Transformations User Experience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplify the user experience of the Show Material Transformations foldout into a two-line layout per slot with side-by-side mesh and material object fields and clear status sentences.

**Architecture:** `MaterialTransformationRow` continues to store pure records without live scene references to prevent cache leaks. `RowText` formats the four simplified status sentences. `AlphaSeparationDryRun.BuildRows()` emits the new status sentences and skips redundant normal renderer summary rows. `AmuseAvatarOptimizerEditor` resolves `Renderer` and `Material` dynamically at draw time with complete bounds checks and renders the two-line layout.

**Tech Stack:** C# against Unity 2022.3 APIs, UnityEditor IMGUI (`EditorGUILayout.ObjectField`, `EditorGUILayout.HorizontalScope`), NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-10-14-simplified-material-transformations-ux-design.md`

## Global Constraints

- Every English text that a human reads uses simple english or ASD-STE100 Simplified Technical English: short active sentences, one idea per sentence, no semicolons, and no contractions.
- Never record an absolute or machine-specific path in any document, comment, commit message, or test. Use `<repo-root>`-relative paths.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names and hashes. Name machines and instances by role only.
- Date every status claim in a record. Never write an unqualified "today" or "currently".
- `DryRunCache` never stores live `UnityEngine.Object` references.
- All four UI status sentences use the exact wording specified in the spec:
  - `Refused: AMUSE currently does not support this material`
  - `Untouchable: 0 of out X polygons may be moved`
  - `Splittable: Y of out X polygons may be moved`
  - `Convertable: X out of X polygons may be moved`

---

### Task 1: Update RowText with the Four Simplified Status Sentence Methods

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs:150-250`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs:20-130`

**Interfaces:**
- Consumes: `TotalTriangles`, `ProvenTriangles`
- Produces:
  - `RowText.RefusedSlotSentence()`: `string`
  - `RowText.UntouchableSlotSentence(int total)`: `string`
  - `RowText.SplittableSlotSentence(int proven, int total)`: `string`
  - `RowText.ConvertableSlotSentence(int total)`: `string`

- [ ] **Step 1: Write the failing tests in MaterialTransformationRowTests**

Update `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs` to test the four new status sentence methods:

```csharp
        [Test]
        public void RefusedSlotSentenceMatchesExpectedWording()
        {
            Assert.That(
                RowText.RefusedSlotSentence(),
                Is.EqualTo("Refused: AMUSE currently does not support this material"));
        }

        [Test]
        public void UntouchableSlotSentenceShowsZeroOutOfTotalPolygons()
        {
            Assert.That(
                RowText.UntouchableSlotSentence(100),
                Is.EqualTo("Untouchable: 0 of out 100 polygons may be moved"));
        }

        [Test]
        public void SplittableSlotSentenceShowsProvenOutOfTotalPolygons()
        {
            Assert.That(
                RowText.SplittableSlotSentence(40, 100),
                Is.EqualTo("Splittable: 40 of out 100 polygons may be moved"));
        }

        [Test]
        public void UntouchableSlotSentenceSupportsZeroPolygons()
        {
            Assert.That(
                RowText.UntouchableSlotSentence(0),
                Is.EqualTo("Untouchable: 0 of out 0 polygons may be moved"));
        }

        [Test]
        public void ConvertableSlotSentenceSupportsZeroPolygons()
        {
            Assert.That(
                RowText.ConvertableSlotSentence(0),
                Is.EqualTo("Convertable: 0 out of 0 polygons may be moved"));
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run Unity EditMode tests filtered to `Alrauna.Amuse.Tests.Editor.DryRun.MaterialTransformationRowTests`.
Expected: FAIL with compilation error (methods do not exist yet).

- [ ] **Step 3: Implement the four methods in RowText**

In `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs`:

```csharp
        internal static string RefusedSlotSentence()
        {
            return "Refused: AMUSE currently does not support this material";
        }

        internal static string UntouchableSlotSentence(int total)
        {
            return "Untouchable: 0 of out " + total + " polygons may be moved";
        }

        internal static string SplittableSlotSentence(int proven, int total)
        {
            return "Splittable: " + proven + " of out " + total + " polygons may be moved";
        }

        internal static string ConvertableSlotSentence(int total)
        {
            return "Convertable: " + total + " out of " + total + " polygons may be moved";
        }
```

Note: Retain the legacy sentence methods in `RowText` temporarily so `AlphaSeparationDryRun.cs` continues to compile during this task. They will be deleted in Task 2 during caller cutover.

- [ ] **Step 4: Run tests to verify they pass**

Run Unity EditMode tests filtered to `Alrauna.Amuse.Tests.Editor.DryRun.MaterialTransformationRowTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs
git commit -m "feat: add simplified transformation status sentence builders"
```

---

### Task 2: Update AlphaSeparationDryRun to Emit Simplified Status Sentences and Omit Normal Summary Rows

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs:1000-1180`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Consumes: `RowText.RefusedSlotSentence()`, `RowText.UntouchableSlotSentence()`, `RowText.SplittableSlotSentence()`, `RowText.ConvertableSlotSentence()`
- Produces: `IReadOnlyList<MaterialTransformationRow>` with simplified status sentences and without normal renderer summary rows.

- [ ] **Step 1: Write failing tests in AlphaSeparationDryRunTests**

Update `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs` to test the simplified sentence emission:

```csharp
        [Test]
        public void CutoutSplitSlotProducesSplittableSentence()
        {
            var root = NewAvatarRoot();
            var renderer = AddCutoutRenderer(root);
            var dryRun = NewDryRun(root);
            dryRun.Begin();
            while (!dryRun.Done)
            {
                dryRun.Step();
            }

            Assert.That(dryRun.RendererRows.Any(r =>
                r.Kind == MaterialTransformationRowKind.Slot &&
                r.Sentence.StartsWith("Splittable:")), Is.True);
        }

        [Test]
        public void HealthyRendererEmitsNoRendererSummaryRow()
        {
            var root = NewAvatarRoot();
            AddCutoutRenderer(root);
            var dryRun = NewDryRun(root);
            dryRun.Begin();
            while (!dryRun.Done)
            {
                dryRun.Step();
            }

            Assert.That(dryRun.RendererRows.Any(r =>
                r.Kind == MaterialTransformationRowKind.Renderer), Is.False);
        }
```

Update existing tests in `AlphaSeparationDryRunTests.cs` that asserted old sentence text (such as `slot 0: would split` or `Triangles moved:`) to assert against the new status sentences.

- [ ] **Step 2: Run test to verify it fails**

Run Unity EditMode tests filtered to `Alrauna.Amuse.Tests.Editor.DryRun.AlphaSeparationDryRunTests`.
Expected: FAIL.

- [ ] **Step 3: Update BuildRows and SlotSentence, and delete obsolete sentence methods**

In `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`:
1. In `BuildRows()`:
   - Remove dead local accumulator variables (`moved`, `proven`, `total`, `foldContributed`, `resolvedSlots`, `refusedSlots`).
   - For unresolved slots (`!slotResults[slotIndex].IsResolved`):
     Emit `MaterialTransformationRow.Slot(rendererPath, rendererTypeName, slotIndex, -1, -1, RowText.RefusedSlotSentence())`.
   - For resolved slots with verdicts:
     Emit `MaterialTransformationRow.Slot(rendererPath, rendererTypeName, verdict.SlotIndex, verdict.ProvenTriangles, verdict.TotalTriangles, SlotSentence(verdict))`.
   - Do not emit normal summary `Renderer` rows.
2. In `SlotSentence(SlotTransformVerdict verdict)`:
```csharp
        private static string SlotSentence(SlotTransformVerdict verdict)
        {
            switch (verdict.Outcome)
            {
                case SlotTransformOutcome.Split:
                    if (verdict.TotalTriangles > 0 && verdict.ProvenTriangles == verdict.TotalTriangles)
                    {
                        return RowText.ConvertableSlotSentence(verdict.TotalTriangles);
                    }
                    if (verdict.ProvenTriangles > 0)
                    {
                        return RowText.SplittableSlotSentence(
                            verdict.ProvenTriangles, verdict.TotalTriangles);
                    }
                    return RowText.UntouchableSlotSentence(verdict.TotalTriangles);
                case SlotTransformOutcome.Fold:
                    return RowText.ConvertableSlotSentence(verdict.TotalTriangles);
                case SlotTransformOutcome.AlreadyOpaque:
                case SlotTransformOutcome.CoverageBelowMinimum:
                    return RowText.UntouchableSlotSentence(verdict.TotalTriangles);
                case SlotTransformOutcome.AnisotropyDifferences:
                    return RowText.RefusedSlotSentence();
                default:
                    return RowText.UntouchableSlotSentence(verdict.TotalTriangles);
            }
        }
```
3. Update `RefusedSlotRow`:
Return `MaterialTransformationRow.Slot(rendererPath, rendererTypeName, slot.SlotIndex, -1, -1, RowText.RefusedSlotSentence())`.
4. Delete obsolete sentence methods in `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs`:
Delete `SplitRowSentence`, `FoldRowSentence`, `AlreadyOpaqueRowSentence`, `UnchangedRowSentence`, `RefusedRowSentence`, `RendererSummarySentence`, `RendererNoMovementSentence`, `RendererNoChangeSentence`.

- [ ] **Step 4: Run tests to verify they pass**

Run Unity EditMode tests filtered to `Alrauna.Amuse.Tests.Editor.DryRun`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs Packages/com.alrauna.amuse/Tests/Editor/DryRun/
git commit -m "feat: emit simplified transformation sentences in dry run"
```

---

### Task 3: Update AmuseAvatarOptimizerEditor to Render Two-Line Layout with Safe Dynamic Object Resolution

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:195-225`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**
- Consumes: `MaterialTransformationRow`, `target` (AmuseAvatarOptimizer)
- Produces: Two-line IMGUI rendering for `Slot` rows and warning help box for `Renderer` refusal rows.

- [ ] **Step 1: Write failing test in AmuseAvatarOptimizerEditorTests**

Update `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`:
Update `MaterialTransformationsFoldoutPaintsRows` to expect `MaterialTransformationRowKind.Slot` rows:

```csharp
        [UnityTest]
        public IEnumerator MaterialTransformationsFoldoutPaintsRows()
        {
            var component = NewHiddenComponent();
            var root = component.gameObject;
            var renderer = root.AddComponent<MeshRenderer>();
            var filter = root.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector3.right };
            mesh.triangles = new int[] { 0, 1, 2 };
            filter.sharedMesh = mesh;
            renderer.sharedMaterials = new Material[] { new Material(Shader.Find("Hidden/InternalErrorShader")) };

            yield return RepaintThroughHostWindow(component, DryRunFinished, true);

            var dryRun = ((Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)_hostedEditor).DryRun;
            Assert.That(dryRun, Is.Not.Null, "dry run must be instantiated");
            Assert.That(dryRun.RendererRows.Any(r => r.Kind == MaterialTransformationRowKind.Slot),
                Is.True, "must produce at least one slot row");
        }
```

- [ ] **Step 2: Run test to verify it fails or runs as characterization**

Run Unity EditMode tests filtered to `Alrauna.Amuse.Tests.Editor.AmuseAvatarOptimizerEditorTests`.

- [ ] **Step 3: Implement two-line layout and safe dynamic object resolution in AmuseAvatarOptimizerEditor.cs**

In `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`:
1. In `DrawMaterialTransformations()`:
Resolve `avatarRoot` once before the row loop:
```csharp
            var avatarRoot = ((AmuseAvatarOptimizer)target)?.transform;
            if (_dryRun != null)
            {
                foreach (var row in _dryRun.AvatarRows)
                {
                    DrawTransformationRow(row, avatarRoot);
                }

                foreach (var row in _dryRun.RendererRows)
                {
                    DrawTransformationRow(row, avatarRoot);
                }
            }
```
2. Update `DrawTransformationRow(MaterialTransformationRow row, Transform avatarRoot)`:
```csharp
        private static void DrawTransformationRow(
            MaterialTransformationRow row,
            Transform avatarRoot)
        {
            switch (row.Kind)
            {
                case MaterialTransformationRowKind.Banner:
                case MaterialTransformationRowKind.Errored:
                    EditorGUILayout.HelpBox(row.Sentence, MessageType.Warning);
                    return;
                case MaterialTransformationRowKind.Renderer:
                    EditorGUILayout.HelpBox(row.Sentence, MessageType.Warning);
                    return;
                case MaterialTransformationRowKind.Slot:
                    Renderer renderer = null;
                    if (avatarRoot != null)
                    {
                        renderer = string.IsNullOrEmpty(row.RendererPath)
                            ? avatarRoot.GetComponent<Renderer>()
                            : avatarRoot.Find(row.RendererPath)?.GetComponent<Renderer>();
                    }

                    Material material = null;
                    var materials = renderer != null ? renderer.sharedMaterials : null;
                    if (materials != null && row.SlotIndex >= 0 && row.SlotIndex < materials.Length)
                    {
                        material = materials[row.SlotIndex];
                    }

                    int indentOffset = EditorGUI.indentLevel * 15;
                    float halfWidth = Mathf.Max(60f, (EditorGUIUtility.currentViewWidth - indentOffset) / 2f - 20f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(renderer, typeof(Renderer), true, GUILayout.Width(halfWidth));
                        int originalIndent = EditorGUI.indentLevel;
                        EditorGUI.indentLevel = 0;
                        EditorGUILayout.ObjectField(material, typeof(Material), false);
                        EditorGUI.indentLevel = originalIndent;
                    }

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(row.Sentence);
                    EditorGUI.indentLevel--;
                    return;
                default:
                    EditorGUILayout.LabelField(row.Sentence);
                    return;
            }
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run Unity EditMode tests filtered to `Alrauna.Amuse.Tests.Editor.AmuseAvatarOptimizerEditorTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs
git commit -m "feat: render two-line material transformation layout in inspector"
```

### Task 4: Full Suite Verification and Documentation Sweep

**Files:**
- Modify: `Packages/com.alrauna.amuse/README.md` (if needed for description of the view)

- [ ] **Step 1: Run the full EditMode test suite**

Run all tests in `Alrauna.Amuse.Tests.Editor`.
Verify that all tests pass.

- [ ] **Step 2: Run identifier sweep on changed files**

Check all changed files for absolute paths, ports, `@` with hex hashes, and private names.
Verify that all files are clean.

- [ ] **Step 3: Commit any documentation updates**

```bash
git add Packages/com.alrauna.amuse/README.md
git commit -m "docs: update show material transformations user experience documentation"
```
