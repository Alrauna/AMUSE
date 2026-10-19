# AMUSE v0.1.0 Release Audit Remediation Implementation Plan

Date: 2026-10-19

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve all code-level pipeline defects, package metadata inaccuracies, release documentation omissions, and workflow dispatch prerequisites identified in the v0.1.0 release audit.

**Architecture:** Mirror the 17-field policy fingerprint into `PlayModeBuildStore`, clear speculative sentences on un-applied candidate slots in `AlphaSeparationApply`, guard transient unlock material reversion with isolated exception blocks, update package metadata and VPM dependency ranges in `package.json`, assemble release documentation assets (`README.md`, `LICENSE`, `CHANGELOG.md`) with Unity `.meta` files, and verify GitHub Actions release prerequisites and full test suites.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (NUnit), Newtonsoft JSON / Unity JsonUtility, VPM packaging specifications, GitHub Actions workflows.

**Spec:** `docs/superpowers/specs/2026-10-19-v0-1-0-release-audit-remediation-design.md`

## Global Constraints

- Simple English / ASD-STE100 compliance: short active sentences, one idea per sentence, no semicolons in narrative text, no contractions.
- Privacy rules: no absolute paths, no machine-specific paths, no user account names, no ports, no GUIDs in narrative text.
- Machines and instances are named by role only (`the dev editor instance`).
- Every file operation on Unity assets must create and maintain valid companion `.meta` files. Never churn existing GUIDs.
- Invariants: AMUSE must never destroy an unlocked clone while live curves or renderer slots reference it.
- Commit between every finalized task.

---

### Task 1: Mirror 17-Field Policy Fingerprint in PlayModeBuildStore

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs:52-68`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs`

**Interfaces:**
- Consumes: `AmuseAvatarOptimizer` component properties (`AmuseDisabled`, `AlphaSeparatorEnabled`, `IgnoreOutOfRangeMaterialSlots`, `AcceptDesktopAnisotropyDifferences`, `AcceptMobileAnisotropyDifferences`).
- Produces: `PlayModeBuildStore.ComputePolicyFingerprint(AmuseAvatarOptimizer, BuildTarget)` producing an 18-part semicolon-delimited string.

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs`:
Add helper method `SetStoredBool` if not present:
```csharp
private static void SetStoredBool(
    AmuseAvatarOptimizer component, string propertyName, bool value)
{
    var so = new SerializedObject(component);
    var prop = so.FindProperty(propertyName);
    if (prop != null)
    {
        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
```

Update `ComputePolicyFingerprintProducesConsistentString` to expect 18 parts:
```csharp
[Test]
public void ComputePolicyFingerprintProducesConsistentString()
{
    var go = new GameObject("Avatar");
    try
    {
        var component = go.AddComponent<AmuseAvatarOptimizer>();
        var fingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
            component, BuildTarget.StandaloneWindows64);
        Assert.That(fingerprint, Does.StartWith("StandaloneWindows64;"));
        Assert.That(fingerprint.Split(';').Length, Is.EqualTo(18));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(go);
    }
}

[Test]
public void ComputePolicyFingerprintChangesWhenMasterSwitchesOrAnisotropyToggle()
{
    var go = new GameObject("Avatar");
    try
    {
        var component = go.AddComponent<AmuseAvatarOptimizer>();
        var baseline = PlayModeBuildStore.ComputePolicyFingerprint(
            component, BuildTarget.StandaloneWindows64);

        SetStoredBool(component, "_amuseDisabled", true);
        var disabledFingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
            component, BuildTarget.StandaloneWindows64);
        Assert.That(disabledFingerprint, Is.Not.EqualTo(baseline));

        SetStoredBool(component, "_amuseDisabled", false);
        SetStoredBool(component, "_alphaSeparatorEnabled", false);
        var separatorOffFingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
            component, BuildTarget.StandaloneWindows64);
        Assert.That(separatorOffFingerprint, Is.Not.EqualTo(baseline));

        SetStoredBool(component, "_alphaSeparatorEnabled", true);
        SetStoredBool(component, "_ignoreOutOfRangeMaterialSlots", true);
        var ignoreSlotsFingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
            component, BuildTarget.StandaloneWindows64);
        Assert.That(ignoreSlotsFingerprint, Is.Not.EqualTo(baseline));

        SetStoredBool(component, "_ignoreOutOfRangeMaterialSlots", false);
        SetStoredBool(component, "_acceptDesktopAnisotropyDifferences", true);
        var desktopAnisoFingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
            component, BuildTarget.StandaloneWindows64);
        Assert.That(desktopAnisoFingerprint, Is.Not.EqualTo(baseline));

        SetStoredBool(component, "_acceptDesktopAnisotropyDifferences", false);
        SetStoredBool(component, "_acceptMobileAnisotropyDifferences", true);
        var mobileAnisoFingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
            component, BuildTarget.StandaloneWindows64);
        Assert.That(mobileAnisoFingerprint, Is.Not.EqualTo(baseline));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(go);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run `PlayModeBuildStoreTests` in the dev editor instance.
Expected: FAIL with split length 13 != 18.

- [ ] **Step 3: Write minimal implementation**

In `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs`:

```csharp
        public static string ComputePolicyFingerprint(
            AmuseAvatarOptimizer component,
            BuildTarget activeTarget)
        {
            if (component == null) return string.Empty;
            return string.Join(";",
                activeTarget.ToString(),
                component.AmuseDisabled ? "1" : "0",
                component.AlphaSeparatorEnabled ? "1" : "0",
                component.PreserveTransparencyMaxMipLevel,
                component.PreserveTransparencyMinTextureSize,
                component.MinimumOpaqueCoveragePercent,
                component.MinimumOpaqueCoverageToPreservePercent,
                component.CutoutPreserveTransparencyMaxMipLevel,
                component.CutoutPreserveTransparencyMinTextureSize,
                component.CutoutMinimumOpaqueCoveragePercent,
                component.CutoutMinimumOpaqueCoverageToPreservePercent,
                component.MinimumOpaqueAlphaPercent,
                component.PolygonAlphaUpperClampPercent,
                component.PolygonMinimumOpaqueCoveragePercent,
                component.AcceptDesktopAnisotropyDifferences ? "1" : "0",
                component.AcceptMobileAnisotropyDifferences ? "1" : "0",
                component.IgnoreOutOfRangeMaterialSlots ? "1" : "0",
                component.AllowDepthTestChange ? "1" : "0");
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run `PlayModeBuildStoreTests` in the dev editor instance.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs
git commit -m "fix(build): mirror 17-field policy fingerprint in PlayModeBuildStore"
```

---

### Task 2: Clear Speculative Sentences for Skipped Candidate Slots

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:1134-1150,1208-1221`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Consumes: `AmusePlatformFinishState.PlayModeRendererRows`, `DryRun.RowText.UntouchableSlotSentence`.
- Produces: Persistent `SerializedTransformationRow` objects with accurate non-speculative sentences for skipped slots.

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`, update `UpdateAppliedMovedTrianglesUpdatesOnlyMatchingSlots`:

```csharp
[Test]
public void UpdateAppliedMovedTrianglesUpdatesOnlyMatchingSlots()
{
    var state = new AmusePlatformFinishState();
    state.PlayModeRendererRows.Add(new SerializedTransformationRow
    {
        kind = 3,
        rendererPath = "SceneRoot/Renderer",
        slotIndex = 0,
        movedTriangles = 0,
        provenTriangles = 100,
        totalTriangles = 100,
        sentence = DryRun.RowText.ConvertableSlotSentence(100)
    });
    state.PlayModeRendererRows.Add(new SerializedTransformationRow
    {
        kind = 3,
        rendererPath = "SceneRoot/Renderer",
        slotIndex = 1,
        movedTriangles = 0,
        provenTriangles = 50,
        totalTriangles = 50,
        sentence = DryRun.RowText.SplittableSlotSentence(50, 50)
    });

    var appliedSlots = new HashSet<(string path, int slot)>
    {
        ("SceneRoot/Renderer", 0)
    };

    AlphaSeparationApply.UpdateAppliedMovedTriangles(state, appliedSlots);

    Assert.That(state.PlayModeRendererRows[0].movedTriangles, Is.EqualTo(100));
    Assert.That(state.PlayModeRendererRows[0].sentence, Is.EqualTo(DryRun.RowText.ConvertableSlotSentence(100)));

    Assert.That(state.PlayModeRendererRows[1].movedTriangles, Is.EqualTo(0));
    Assert.That(state.PlayModeRendererRows[1].sentence, Is.EqualTo(DryRun.RowText.UntouchableSlotSentence(50)));
}
```

Add a test verifying that `CaptureAndSavePlayModeBuildRecord` assigns `UntouchableSlotSentence` when a candidate slot is omitted from `appliedSlots`:

```csharp
[Test]
public void CaptureAndSavePlayModeBuildRecordAssignsUntouchableSentenceToUnappliedCandidate()
{
    var source = Track(PoiyomiFixtureTestBase.CreateVerifiedOpaqueMaterial());
    var rendererObj = Track(new GameObject("AMUSE unapplied candidate test renderer"));
    var renderer = rendererObj.AddComponent<MeshRenderer>();
    renderer.sharedMaterials = new[] { source };

    var mesh = Track(PoiyomiFixtureTestBase.CreateTwoTriangleMesh());
    var filter = rendererObj.AddComponent<MeshFilter>();
    filter.sharedMesh = mesh;

    var context = new BuildContext(rendererObj, "Assets/Temp");
    var state = context.GetState<AmusePlatformFinishState>();

    var candidate = new PreparedCandidateSlot(
        new CandidateSlotPlan(
            0,
            new[] { 0 },
            new[] { 1 }),
        new Dictionary<Material, Material> { { source, source } },
        new Dictionary<Material, Material>());

    var prepared = new PreparedRenderer(
        new RendererAnalysisTarget(renderer, "RendererPath"),
        "MeshRenderer",
        "RendererPath",
        mesh,
        new[] { source },
        new[] { candidate });

    state.Separation = new PreparedAlphaSeparation(
        new[] { prepared },
        new Dictionary<Material, Material>(),
        Array.Empty<Material>());

    // Notice: state.AppliedFinalization is null, so appliedSlots will be empty.
    AlphaSeparationApply.CaptureAndSavePlayModeBuildRecord(context, state);

    Assert.That(state.PlayModeRendererRows.Count, Is.EqualTo(1));
    var row = state.PlayModeRendererRows[0];
    Assert.That(row.movedTriangles, Is.EqualTo(0));
    Assert.That(row.sentence, Is.EqualTo(DryRun.RowText.UntouchableSlotSentence(2)));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run `AlphaSeparationApplyTests` in the dev editor instance.
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`, update `UpdateAppliedMovedTriangles`:

```csharp
        internal static void UpdateAppliedMovedTriangles(
            AmusePlatformFinishState state,
            HashSet<(string path, int slot)> appliedSlots)
        {
            if (state == null || appliedSlots == null) return;
            for (var i = 0; i < state.PlayModeRendererRows.Count; i++)
            {
                var row = state.PlayModeRendererRows[i];
                if (row.kind == 3 && row.provenTriangles > 0)
                {
                    if (appliedSlots.Contains((row.rendererPath, row.slotIndex)))
                    {
                        row.movedTriangles = row.provenTriangles;
                    }
                    else
                    {
                        row.movedTriangles = 0;
                        row.sentence = DryRun.RowText.UntouchableSlotSentence(row.totalTriangles);
                    }
                    state.PlayModeRendererRows[i] = row;
                }
            }
        }
```

In lines 1208-1221 of `CaptureAndSavePlayModeBuildRecord`:

```csharp
                            string sentence;
                            if (isApplied)
                            {
                                if (total > 0 && proven == total)
                                {
                                    sentence = DryRun.RowText.ConvertableSlotSentence(total);
                                }
                                else if (proven > 0)
                                {
                                    sentence = DryRun.RowText.SplittableSlotSentence(proven, total);
                                }
                                else
                                {
                                    sentence = DryRun.RowText.UntouchableSlotSentence(total);
                                }
                            }
                            else
                            {
                                sentence = DryRun.RowText.UntouchableSlotSentence(total);
                            }
```

- [ ] **Step 4: Run test to verify it passes**

Run `AlphaSeparationApplyTests` in the dev editor instance.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs
git commit -m "fix(build): clear speculative sentences for skipped candidate slots in Play mode record"
```

---

### Task 3: Enclose Material Reference Reversion in Guarded Blocks

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:124-136,185-222,260-295`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`

**Interfaces:**
- Consumes: `TransientUnlockWindowState.OpenPairs`, `InvertReferences`, `ReassertShippedSlots`, `DestroyPairCopiesOrNameRetention`.
- Produces: Guarded execution where individual slot exceptions do not abort remaining pairs, and faulted pairs safely retain clones.

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`, add:

```csharp
[Test]
public void ExceptionDuringPairReversionDoesNotAbortSubsequentPairs()
{
    using var assets = new OverrideTemporaryDirectoryScope(
        TransientUnlockTestLifecycle.TempFolder);

    var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE exception isolation fixture"));
    var locked1 = Track(TransientUnlockTestLifecycle.LockedMaterial("Locked1"));
    var clone1 = Track(Object.Instantiate(locked1));
    clone1.name = locked1.name + "_AmuseUnlockedClone";

    var locked2 = Track(TransientUnlockTestLifecycle.LockedMaterial("Locked2"));
    var clone2 = Track(Object.Instantiate(locked2));
    clone2.name = locked2.name + "_AmuseUnlockedClone";

    var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
    var renderer1 = TransientUnlockTestLifecycle.AddRenderer(root, mesh, clone1);
    var renderer2 = TransientUnlockTestLifecycle.AddRenderer(root, mesh, clone2);

    var context = new BuildContext(root, TransientUnlockTestLifecycle.TempFolder);
    var finishState = context.GetState<AmusePlatformFinishState>();
    finishState.AnimatorBindings = GenericPlatformAnimatorBindings.Instance;

    var window = context.GetState<TransientUnlockWindowState>();
    var pair1 = window.OpenPair(locked1);
    pair1.UnlockedClone = clone1;
    pair1.RecordSlot(renderer1, 0);

    var pair2 = window.OpenPair(locked2);
    pair2.UnlockedClone = clone2;
    pair2.RecordSlot(renderer2, 0);

    // Destroy renderer1's gameObject to provoke an exception during renderer access
    Object.DestroyImmediate(renderer1.gameObject);

    TransientUnlockWindowClose.Execute(context);

    Assert.That(clone1 != null, Is.True, "faulted pair must retain clone");
    Assert.That(finishState.SlotRefusalCount(
        AlphaSeparationSlotRefusal.TransientUnlockCloneRetained), Is.GreaterThan(0));

    Assert.That(renderer2.sharedMaterials[0], Is.EqualTo(locked2));
    Assert.That(clone2 == null, Is.True, "subsequent pair must complete reversion and destroy clone");
    Assert.That(window.PairFor(locked2), Is.Null, "successful pair must be removed from window");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run `TransientUnlockWindowCloseTests` in the dev editor instance.
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:

1. Update `Execute`:
```csharp
            foreach (var pair in window.OpenPairs.ToList())
            {
                try
                {
                    InvertReferences(committedClips, pair);
                    ReassertShippedSlots(pair, recorded);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                try
                {
                    if (DestroyPairCopiesOrNameRetention(
                            context, finishState, pair, committedClips, graph))
                    {
                        window.Remove(pair);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
```

2. Update `InvertReferences`:
```csharp
            foreach (var slot in pair.Slots)
            {
                try
                {
                    var renderer = slot.Renderer;
                    if (renderer == null)
                    {
                        continue;
                    }

                    var live = renderer.sharedMaterials;
                    var changed = false;
                    for (var index = 0; index < live.Length; index++)
                    {
                        if (NamesSwappedCopy(live[index], clone))
                        {
                            live[index] = locked;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        renderer.sharedMaterials = live;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            try
            {
                InvertCommittedCurves(committedClips, pair);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
```

3. Update `ReassertShippedSlots`:
```csharp
            foreach (var slot in pair.Slots)
            {
                try
                {
                    var renderer = slot.Renderer;
                    if (renderer == null)
                    {
                        continue;
                    }

                    var live = renderer.sharedMaterials;
                    if (slot.SlotIndex >= live.Length)
                    {
                        continue;
                    }

                    var current = live[slot.SlotIndex];
                    if (!NamesSwappedCopy(current, pair.LockedOriginal) &&
                        !NamesSwappedCopy(current, pair.UnlockedClone))
                    {
                        continue;
                    }

                    var write = RecordedWriteMaterial(
                        recorded, renderer, slot.SlotIndex);
                    if (write == null || write == pair.UnlockedClone)
                    {
                        continue;
                    }

                    live[slot.SlotIndex] = write;
                    renderer.sharedMaterials = live;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
```

- [ ] **Step 4: Run test to verify it passes**

Run `TransientUnlockWindowCloseTests` in the dev editor instance.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs
git commit -m "fix(build): enclose transient unlock material reference reversion in guarded blocks"
```

---

### Task 4: Run Focused Build Tests Verification

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/`

- [ ] **Step 1: Run focused build tests in the dev editor instance**

Run all tests in group `Alrauna.Amuse.Tests.Editor.Build` via the Unity test runner.

- [ ] **Step 2: Verify zero failures**

Confirm that all tests in `Tests/Editor/Build/` pass cleanly.

- [ ] **Step 3: Log verification output**

Record observed test count and execution duration.

---

### Task 5: Update Package Metadata and Dependency Specifications

**Files:**
- Modify: `Packages/com.alrauna.amuse/package.json`

- [ ] **Step 1: Update package version to 0.1.0**

Change line 4:
`"version": "0.1.0",`

- [ ] **Step 2: Update vrchatVersion to 2022.1.1**

Change line 23:
`"vrchatVersion": "2022.1.1",`

- [ ] **Step 3: Update vpmDependencies with VRChat Avatars SDK range**

Update lines 17-19:
```json
  "vpmDependencies": {
    "nadena.dev.ndmf": ">=1.14.4 <2.0.0-a",
    "com.vrchat.avatars": ">=3.10.4 <4.0.0-a"
  },
```

- [ ] **Step 4: Validate package.json syntax**

Validate that `package.json` contains valid JSON.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/package.json
git commit -m "chore(pkg): update package metadata for v0.1.0 release"
```

---

### Task 6: Assemble Release Documentation and Packaging Assets

**Files:**
- Create: `Packages/com.alrauna.amuse/README.md`
- Create: `Packages/com.alrauna.amuse/README.md.meta`
- Create: `Packages/com.alrauna.amuse/LICENSE`
- Create: `Packages/com.alrauna.amuse/LICENSE.meta`
- Create: `Packages/com.alrauna.amuse/CHANGELOG.md`
- Create: `Packages/com.alrauna.amuse/CHANGELOG.md.meta`

- [ ] **Step 1: Copy README.md and LICENSE into package folder**

Copy `<repo-root>/README.md` to `Packages/com.alrauna.amuse/README.md`.
Copy `<repo-root>/LICENSE` to `Packages/com.alrauna.amuse/LICENSE`.

- [ ] **Step 2: Create companion .meta files for README and LICENSE**

Create `Packages/com.alrauna.amuse/README.md.meta`:
```yaml
fileFormatVersion: 2
guid: 8e5f2a1b3c4d5e6f7a8b9c0d1e2f3a4b
TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

Create `Packages/com.alrauna.amuse/LICENSE.meta`:
```yaml
fileFormatVersion: 2
guid: 9a6b3c2d4e5f6a7b8c9d0e1f2a3b4c5d
TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 3: Create CHANGELOG.md with release notes and companion .meta file**

Create `Packages/com.alrauna.amuse/CHANGELOG.md`:
```markdown
# Changelog

All notable changes to AMUSE are documented in this file.

## [0.1.0] - 2026-10-19

### Added
- Exact rational polygon alpha classification proving opacity across UV coordinates.
- Non-destructive mesh splitting separating proven-opaque triangles to canonical opaque materials.
- Shader frontends for lilToon (versions 2.3.0 to 2.3.4) and Poiyomi Toon Shader (version 9.3.64, including Two Pass).
- EditMode DryRun preview inspecting avatar materials and expected polygon splits.
- PlayMode build record store caching transformation results for inspection.
- Optimization presets: Safe, Normal, and Aggressive.
- Consent gate prompting before processing unverified shader versions.

### Requirements
- Unity 2022.3.22f1 or compatible 2022.3 LTS release.
- NDMF (Non-Destructive Modular Framework) version 1.14.4 or higher.
- VRChat Avatar 3.0 SDK version 3.10.4 or higher.

### Known Limitations
- Finding 4: Deep material property revalidation is deferred during the Apply pass.
- Finding 9: Large texture dry-runs execute synchronous GPU readbacks that cause brief editor hitches.
```

Create `Packages/com.alrauna.amuse/CHANGELOG.md.meta`:
```yaml
fileFormatVersion: 2
guid: a1b2c3d4e5f67890123456789abcdef0
TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 4: Verify files and companion .meta files exist**

Confirm all three files and their companion `.meta` files exist in `Packages/com.alrauna.amuse/`.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/README.md Packages/com.alrauna.amuse/README.md.meta Packages/com.alrauna.amuse/LICENSE Packages/com.alrauna.amuse/LICENSE.meta Packages/com.alrauna.amuse/CHANGELOG.md Packages/com.alrauna.amuse/CHANGELOG.md.meta
git commit -m "docs(pkg): add README, LICENSE, and CHANGELOG to package root"
```

---

### Task 7: Verify GitHub Actions Automation and Run Full Validation Suites

**Files:**
- Inspect: `.github/workflows/release.yml`
- Inspect: `.github/workflows/build-listing.yml`

- [ ] **Step 1: Verify GitHub Actions workflow prerequisites**

Inspect `.github/workflows/release.yml:21-28` and `.github/workflows/build-listing.yml:61`.
Confirm `PACKAGE_NAME` variable requirement (`com.alrauna.amuse`).
Confirm release artifact exclusions (`Tests/*`, `Tests.meta`, research package).

- [ ] **Step 2: Run full product test suite in the dev editor instance**

Run all tests in assembly `Alrauna.Amuse.Tests.Editor` in the dev editor instance.
Expected: PASS (all tests pass, 0 failures).

- [ ] **Step 3: Run full research test suite in the dev editor instance**

Run all tests in assembly `Alrauna.Amuse.Research.Tests.Editor` in the dev editor instance.
Expected: PASS (all tests pass, 0 failures, 4 synthetic fixtures skipped by design).

- [ ] **Step 4: Perform final sweep for private identifiers**

Sweep all modified files for absolute paths, machine names, user names, ports, and GUIDs.

---

## 4. Self-Review and Checklist

1. **Spec Coverage**: All items in the design specification map directly to Tasks 1 through 7.
2. **Placeholder Scan**: No `TODO`, `TBD`, or placeholders exist. All steps have concrete code and commands.
3. **Type Consistency**: Method signatures, parameter names, and property names match production code exactly.
