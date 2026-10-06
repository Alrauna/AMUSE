# Title Subtext Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the full product name as a subtext line under the "AMUSE" header title.

**Architecture:** One internal constant and one centered label draw inside the existing `DrawHeader` method. No component state, no build behavior, no package metadata change.

**Tech Stack:** Unity 2022.3 editor C#, IMGUI.

**Spec:** `docs/superpowers/specs/2026-10-05-title-subtext-design.md`

**Working discipline:** Branch `feature/optimizer-ui-rework` at `f319186`, cut from `main`. Allowed mutations: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` only. The uncommitted change to `.github/workflows/pr.yml` is user-owned; do not touch, stage, or absorb it. Commit steps run only with explicit session authorization. Stop and return evidence when the observed header differs from the investigation record.

## Global Constraints

- Labels and tooltips use plain technical English. Short active sentences. No contractions.
- No absolute or machine-specific paths in code, comments, docs, or commit messages.
- A filtered test run that reports 0 tests is a failure. Record observed counts.
- Unity MCP safety, before any tool use on the editor: read `mcpforunity://instances`; inspect `Application.dataPath` of the candidate; require an exact match to the repository `Assets` folder; pin the instance when more than one is connected. Tests never run in the Census Lab project.
- RED/GREEN applies to behavior changes. This slice changes layout only, so the evidence is compile, suite, and a smoke check.

---

### Task 1: Draw the subtext line

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `internal const string ProductSubtext` on `AmuseAvatarOptimizerEditor`. Nothing else reads it today.

- [ ] **Step 1: Add the constant**

In `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`, beside the two foldout fields at the top of the class (lines 16-17), add:

```csharp
        /// <summary>
        /// The full product name under the title. A proper name, not a
        /// sentence, so the plain-English sentence rules do not
        /// rewrite it.
        /// </summary>
        internal const string ProductSubtext =
            "Alrauna's Material Understanding and Simplification Engine";
```

- [ ] **Step 2: Draw the line in `DrawHeader`**

In `DrawHeader`, between the version label block (ends with the `right` style `GUI.Label`) and the `Report a bug` button, add:

```csharp
            var subtextStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                wordWrap = true,
            };
            EditorGUILayout.LabelField(ProductSubtext, subtextStyle);
```

The title row, the version label, the button, and the trailing `EditorGUILayout.Space(4)` keep their behavior.

- [ ] **Step 3: Compile and run the suite**

Refresh the dev editor instance, confirm zero compile errors, then run the full `Alrauna.Amuse.Tests.Editor` EditMode suite.
Expected: every test passes, 0 failed. Record the observed counts.

- [ ] **Step 4: Smoke check on the dev editor instance**

Following the Unity MCP safety gate: enumerate instances, verify `Application.dataPath` matches this repository's `Assets` folder exactly, and pin the instance. Select an avatar root that carries `AmuseAvatarOptimizer`.
Expected: the subtext sits directly under "AMUSE" and above "Report a bug". The version stays right-aligned in the title row. Narrow the inspector: the subtext wraps instead of clipping.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs
git commit -m "feat: draw the full product name under the header title"
```
