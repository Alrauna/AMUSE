# Show Material Transformations UX: Text Changes, Play Mode Piping, and Lazy Loading Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplify and accelerate Show Material Transformations in AMUSE through ASD-STE100 text updates, Play mode build outcome piping through `SessionState`, and non-blocking lazy loading in Edit mode.

**Architecture:**
1. Text changes: update `AmuseReportStrings` and `MaterialTransformationRow.DisclosureSentence` to compliant ASD-STE100 sentences.
2. Policy fingerprint: share policy fingerprint computation in `Editor/Build/AmusePolicyFingerprint.cs`.
3. Play Mode Piping: `AmusePlatformFinishPass` and `AlphaSeparationApply` record transformation outcomes into `PlayModeBuildStore` in `Editor/Build/`. The store persists records to `UnityEditor.SessionState` under a normalized avatar key. `DryRunCache` and `AmuseAvatarOptimizerEditor` query `PlayModeBuildStore`, render Play mode results immediately, and bypass Edit mode computation.
4. Lazy Loading: `AmuseAvatarOptimizerEditor` latches display state during `EventType.Layout`, withholds rows while processing is incomplete, displays an informative status box, and drives stepping cooperatively on `EditorApplication.update`.

**Tech Stack:** C# against Unity 2022.3 APIs, UnityEditor IMGUI, UnityEditor.SessionState, NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-10-14-material-transformations-play-mode-piping-lazy-loading-design.md`

## Global Constraints

- Every English text that a human reads uses simple english or ASD-STE100 Simplified Technical English: short active sentences, one idea per sentence, no semicolons, and no contractions.
- Never record an absolute or machine-specific path in any document, comment, commit message, or test. Use `<repo-root>`-relative paths.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names and hashes. Name machines and instances by role only.
- Date every status claim in a record. Never write an unqualified "today" or "currently".
- `Editor/Build/` never depends on `Editor/DryRun/`.
- `DryRunCache` never stores live `UnityEngine.Object` references.
- Never record an absolute or machine-specific path in any document, comment, commit message, or test. Use `<repo-root>`-relative paths.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names and hashes. Name machines and instances by role only.
- Date every status claim in a record. Never write an unqualified "today" or "currently".
- `DryRunCache` never stores live `UnityEngine.Object` references.

---

### Task 1: ASD-STE100 Text Updates and Test Alignment

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:215-224`
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs:268-275`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:2105-2115`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs:120-135`

- [ ] **Step 1: Write failing tests for the new text strings**

Update `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs`:
```csharp
Assert.That(
    RowText.DisclosureSentence(0),
    Is.EqualTo(
        "This view runs the same proof as a build on the avatar in the " +
        "scene. Other tools can change the avatar during build time. " +
        "Run a Play mode build and exit to see accurate information."));
```

Update `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`:
```csharp
Assert.That(
    reported,
    Has.Some.Contains(
        "This avatar has a state behavior that AMUSE does not know. " +
        "Run a Play mode build for accurate information."));
```

- [ ] **Step 2: Update AmuseReportStrings and MaterialTransformationRow**

In `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`:
```csharp
["amuse.avatar.UnrecognizedStateMachineBehaviour"] =
    "This avatar has a state behavior that AMUSE does not know. " +
    "Run a Play mode build for accurate information.",
```

In `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs`:
```csharp
case 0:
    return "This view runs the same proof as a build on the avatar in "
        + "the scene. Other tools can change the avatar during build "
        + "time. Run a Play mode build and exit to see accurate "
        + "information.";
```

- [ ] **Step 3: Run EditMode tests and verify PASS**

Run the EditMode tests for `AmuseReportStringsTests`, `MaterialTransformationRowTests`, and `AmusePlatformFinishPluginTests`. Verify both tests pass.

---

### Task 2: Play Mode Build Outcome Capture, DTO Schema, and SessionState Persistence
**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishState.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs`

- [ ] **Step 1: Define PlayModeBuildStore, DTOs, and Policy Fingerprint in Editor/Build**

In `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs`:
```csharp
using System;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    [Serializable]
    internal struct SerializedTransformationRow
    {
        public int kind;
        public string rendererPath;
        public string rendererTypeName;
        public string sourceMaterialPath;
        public int slotIndex;
        public int movedTriangles;
        public int provenTriangles;
        public int totalTriangles;
        public int refusedSlots;
        public int slotCount;
        public string sentence;
    }

    [Serializable]
    internal sealed class PlayModeBuildRecord
    {
        public string avatarKey;
        public string policyFingerprint;
        public SerializedTransformationRow[] avatarRows;
        public SerializedTransformationRow[] rendererRows;
    }

    internal static class PlayModeBuildStore
    {
        private const string SessionPrefix = "Amuse_PlayMode_";

        public static string ComputeAvatarKey(Transform avatarRoot)
        {
            if (avatarRoot == null) return string.Empty;
            var rootName = avatarRoot.name;
            if (rootName.EndsWith("(Clone)", StringComparison.Ordinal))
            {
                rootName = rootName.Substring(0, rootName.Length - 7).TrimEnd();
            }
            var scenePath = avatarRoot.gameObject.scene.path;
            if (string.IsNullOrEmpty(scenePath)) scenePath = avatarRoot.gameObject.scene.name;
            return scenePath + "::" + rootName;
        }

        public static string ComputePolicyFingerprint(
            AmuseAvatarOptimizer component,
            BuildTarget activeTarget)
        {
            if (component == null) return string.Empty;
            return string.Join(";",
                activeTarget.ToString(),
                component.PreserveTransparencyMaxMipLevel,
                component.PreserveTransparencyMinTextureSize,
                component.PolygonMinimumOpaqueCoveragePercent,
                component.MinimumOpaqueCoveragePercent,
                component.MinimumOpaqueCoverageToPreservePercent,
                component.AllowDepthTestChange,
                component.MinimumOpaqueAlphaPercent,
                component.PolygonAlphaUpperClampPercent);
        }

        public static void Save(PlayModeBuildRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.avatarKey)) return;
            var json = JsonUtility.ToJson(record);
            SessionState.SetString(SessionPrefix + record.avatarKey, json);
        }

        public static PlayModeBuildRecord TryLoad(string avatarKey, string expectedFingerprint)
        {
            if (string.IsNullOrEmpty(avatarKey)) return null;
            var key = SessionPrefix + avatarKey;
            var json = SessionState.GetString(key, null);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var record = JsonUtility.FromJson<PlayModeBuildRecord>(json);
                if (record == null || record.policyFingerprint != expectedFingerprint)
                {
                    return null;
                }
                return record;
            }
            catch
            {
                SessionState.EraseString(key);
                return null;
            }
        }

        public static void Clear(string avatarKey)
        {
            if (string.IsNullOrEmpty(avatarKey)) return;
            SessionState.EraseString(SessionPrefix + avatarKey);
        }
    }
}
```

- [ ] **Step 2: Add SourceMaterialPath and mapper to MaterialTransformationRow**

In `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs`:
- Add property: `public string SourceMaterialPath { get; }`.
- Update constructor or add constructor overload accepting `sourceMaterialPath`.
- Add factory method: `internal static MaterialTransformationRow FromSerialized(SerializedTransformationRow row)`.

- [ ] **Step 3: Capture build outcomes in AmusePlatformFinishPass and AlphaSeparationApply**

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs` and `AlphaSeparationApply.cs`:
- When `state.Lifecycle.BuildPath == AmuseBuildPath.ApplyOnPlay`:
  - Collect `SerializedTransformationRow` records for banner rows, renderer notes, renderer summaries, and slot outcomes.
  - Compute `avatarKey` and `policyFingerprint`.
  - Assemble `PlayModeBuildRecord` and call `PlayModeBuildStore.Save(record)`.

- [ ] **Step 4: Write unit tests in PlayModeBuildStoreTests.cs**

```csharp
[Test]
public void ComputeAvatarKeyStripsCloneSuffix()
{
    var go = new GameObject("Avatar(Clone)");
    try
    {
        var key = PlayModeBuildStore.ComputeAvatarKey(go.transform);
        Assert.That(key, Does.EndWith("::Avatar"));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(go);
    }
}

[Test]
public void SaveAndTryLoadRoundtripsThroughSessionState()
{
    var record = new PlayModeBuildRecord
    {
        avatarKey = "TestScene::Avatar",
        policyFingerprint = "fingerprint_1",
        avatarRows = Array.Empty<SerializedTransformationRow>(),
        rendererRows = new[]
        {
            new SerializedTransformationRow
            {
                kind = 2,
                rendererPath = "Body",
                rendererTypeName = "SkinnedMeshRenderer",
                slotIndex = 0,
                sentence = "Convertable: 100 out of 100 polygons may be moved"
            }
        }
    };

    try
    {
        PlayModeBuildStore.Save(record);
        var loaded = PlayModeBuildStore.TryLoad("TestScene::Avatar", "fingerprint_1");
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.rendererRows.Length, Is.EqualTo(1));
        Assert.That(loaded.rendererRows[0].rendererPath, Is.EqualTo("Body"));
    }
    finally
    {
        PlayModeBuildStore.Clear("TestScene::Avatar");
    }
}
```
### Task 3: Lazy Loading and Inspector Integration in AmuseAvatarOptimizerEditor

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

- [ ] **Step 1: Add PumpAll() to AlphaSeparationDryRun**

In `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`:
```csharp
internal void PumpAll()
{
    while (!Done && Step())
    {
    }
}
```

- [ ] **Step 2: Implement cooperative stepping on EditorApplication.update**

In `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`:
- In `OnEnable()`: `EditorApplication.update += OnEditorUpdate;`.
- In `OnDisable()`: `EditorApplication.update -= OnEditorUpdate;`.
- In `OnEditorUpdate()`:
  - Guard: `if (target == null) return;`
  - Guard: `if (!_materialTransformationsOpen || !_alphaSeparationTransformationsOpen) return;`
  - Guard: `if (EditorApplication.isPlayingOrWillChangePlaymode) return;`
  - If `_dryRun != null && !_dryRun.Done`:
    - Call `_dryRun.Step()`.
    - If `_dryRun.Done`: call `Repaint()`.

- [ ] **Step 3: Integrate Play mode build inspection and lazy loading row withholding with Layout latching**

In `AmuseAvatarOptimizerEditor.DrawMaterialTransformations()`:
- Add field: `private bool _dryRunCompletedLatched;`.
- In `Event.current.type == EventType.Layout`:
  - Latch: `_dryRunCompletedLatched = _dryRun != null && _dryRun.Done;`.
- Check Play mode build store:
  - Compute `avatarKey = PlayModeBuildStore.ComputeAvatarKey(avatarRoot);`
  - Compute `fingerprint = PlayModeBuildStore.ComputePolicyFingerprint(optimizer, EditorUserBuildSettings.activeBuildTarget);`
  - `var playRecord = PlayModeBuildStore.TryLoad(avatarKey, fingerprint);`
  - If `playRecord != null`:
    - Render HelpBox: `"This view shows accurate results from the Play mode build."`.
    - Render rows using `MaterialTransformationRow.FromSerialized(row)`.
    - Return early so no dry run executes.
- If no Play mode record:
  - If `_dryRun == null`: create dry run, call `Begin()`, and latch `_dryRunCompletedLatched = false;`.
  - If `!_dryRunCompletedLatched`:
    - Render `EditorGUILayout.HelpBox("AMUSE computes material transformations.", MessageType.Info);`
    - Return early (transformation rows withheld).
  - If `_dryRunCompletedLatched`:
    - Render avatar rows and renderer rows.
- In `DrawTransformationRow()`:
  - If `renderer == null`:
    - Render `EditorGUILayout.LabelField(row.RendererPath, EditorStyles.boldLabel)` instead of empty `ObjectField`.
    - If `!string.IsNullOrEmpty(row.SourceMaterialPath)`:
      - `var mat = AssetDatabase.LoadAssetAtPath<Material>(row.SourceMaterialPath);`
      - `EditorGUILayout.ObjectField(mat, typeof(Material), false);`
- In `Refresh` button click:
  - `PlayModeBuildStore.Clear(avatarKey);`
  - `DryRunCache.Clear();`
  - `_dryRun = null;`
  - `_dryRunCompletedLatched = false;`

- [ ] **Step 4: Write tests for lazy loading and inspector integration**

In `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`:
```csharp
[Test]
public void LazyLoadingWithholdsRowsUntilDone()
{
    // Verify that when _dryRun.Done is false, DrawMaterialTransformations does not draw slot fields
    // and draws the computing status box instead.
}

[Test]
public void PlayModeRecordDisplaysBannerAndBypassesDryRun()
{
    // Verify that a valid PlayModeBuildRecord displays "This view shows accurate results from the Play mode build."
}

[Test]
public void MissingRendererDrawsTextLabelFallback()
{
    // Verify that a null renderer in DrawTransformationRow draws a text label with row.RendererPath
    // rather than an empty ObjectField.
}
```

---

### Task 4: Full Test Suite Verification and Identifier Sweep

- [ ] **Step 1: Run all EditMode tests in Alrauna.Amuse.Tests.Editor**
- [ ] **Step 2: Sweep changed files for identifiers (ports, hashes, machine paths, user names)**
- [ ] **Step 3: Git diff --check for whitespace and clean code**
