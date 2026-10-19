# AMUSE v0.1.0 Release Audit Remediation Design

Date: 2026-10-19

## Status and scope

- Branch: `cleanup/v0-1-0-release-audit`
- Intended change: design specification for v0.1.0 release audit remediation.
- Target components: `PlayModeBuildStore`, `AlphaSeparationApply`, `TransientUnlockWindowClose`, `package.json`, release documentation, and packaging assets.
- Active instances: the dev editor instance.
- Private avatars and external assets: not used.

## Privacy note

This design specification reads public repository source code and public project metadata. It records no private avatar name. It records no private identifier. It records no machine-specific file path.

---

## 1. Architectural Philosophy and Ponytail Adherence

AMUSE adheres to the ponytail principle:
- The shortest working diff is the right solution.
- Prefer existing types, methods, and utilities over new abstractions.
- Never add an unnecessary interface, wrapper, or framework.
- Fix bugs at their root cause.
- Preserve all transformation safety invariants.

Every change in this specification follows this approach.

---

## 2. Detailed Technical Design

### 2.1 Build Pipeline Fix 1: Policy Fingerprint Mirroring in PlayModeBuildStore

#### Context and Invariant
`PlayModeBuildStore.ComputePolicyFingerprint` generates a cache key from the optimizer component settings and the active build target.
The cache key must change whenever any optimization parameter changes.
`AlphaSeparationDryRun.cs:1378-1406` already serializes 17 fields in its fingerprint.

#### Design
Update `PlayModeBuildStore.ComputePolicyFingerprint(AmuseAvatarOptimizer component, BuildTarget activeTarget)` in `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs`.
Serialize `activeTarget.ToString()` followed by the 17 component properties in the exact same sequence as `AlphaSeparationDryRun.BuildPolicyFingerprint`:

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

#### Test Design
In `Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs`:
- Update `ComputePolicyFingerprintProducesConsistentString` to assert that splitting the fingerprint yields 18 parts.
- Add focused tests asserting that toggling `AmuseDisabled`, `AlphaSeparatorEnabled`, `IgnoreOutOfRangeMaterialSlots`, `AcceptDesktopAnisotropyDifferences`, and `AcceptMobileAnisotropyDifferences` alters the fingerprint.

---

### 2.2 Build Pipeline Fix 2: Clearing Speculative Sentences for Skipped Candidate Slots

#### Context and Invariant
Play mode build records must reflect actual applied build results.
When candidate slots are skipped at apply time, their moved polygon count is 0.
Their displayed sentence must not state that polygons "may be moved".
The sentence must display `RowText.UntouchableSlotSentence(total)`.

#### Design
In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`:

1. In `UpdateAppliedMovedTriangles(AmusePlatformFinishState state, HashSet<(string path, int slot)> appliedSlots)`:
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

2. In `CaptureAndSavePlayModeBuildRecord` (fallback loop when `state.PlayModeRendererRows.Count == 0`):
Guard the sentence construction with `isApplied`:
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

#### Test Design
In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`:
- Update `UpdateAppliedMovedTrianglesUpdatesOnlyMatchingSlots` to assert that skipped candidate slots update their sentence to `DryRun.RowText.UntouchableSlotSentence(row.totalTriangles)`.
- Add a test verifying that candidate slots omitted from `appliedSlots` clear their speculative sentence in `CaptureAndSavePlayModeBuildRecord`.

---

### 2.3 Build Pipeline Fix 3: Guarded Reversion in TransientUnlockWindowClose

#### Context and Invariant
The transient unlock window must maintain two invariants:
1. No unhandled exception on one slot or pair may abort cleanup for remaining pairs.
2. AMUSE must never destroy an unlocked clone while any live curve or renderer slot references it. Destroying an in-use clone causes missing object references.
When references cannot be inverted, AMUSE must retain the clone and record `AlphaSeparationSlotRefusal.TransientUnlockCloneRetained`.

#### Design
In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs`:

1. In `Execute(BuildContext context)`:
Enclose each pair in isolated `try/catch` blocks so that inversion failures do not prevent retention verification, and neither step aborts the loop:
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

2. In `InvertReferences(IReadOnlyList<AnimationClip> committedClips, TransientUnlockWindowState.SwappedPair pair)`:
Wrap the per-slot renderer array reversion in a `try/catch` block.
Wrap the curve inversion call in a `try/catch` block:
```csharp
private static void InvertReferences(
    IReadOnlyList<AnimationClip> committedClips,
    TransientUnlockWindowState.SwappedPair pair)
{
    var clone = pair.UnlockedClone;
    var locked = pair.LockedOriginal;

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
}
```

3. In `ReassertShippedSlots(TransientUnlockWindowState.SwappedPair pair, AlphaSeparationFinalization recorded)`:
Wrap per-slot writes in a `try/catch` block:
```csharp
private static void ReassertShippedSlots(
    TransientUnlockWindowState.SwappedPair pair,
    AlphaSeparationFinalization recorded)
{
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
}
```

#### Test Design
In `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockWindowCloseTests.cs`:
Add a test with two open pairs where the first pair encounters an exception during renderer access. Verify that:
- The second pair completes reference inversion and its clone is destroyed.
- The faulted first pair retains its clone safely.
- `AlphaSeparationSlotRefusal.TransientUnlockCloneRetained` is recorded for the faulted pair.
- The method does not throw an unhandled exception.

---

### 2.4 Package Metadata and Dependency Specifications

#### Files
- `Packages/com.alrauna.amuse/package.json`

#### Changes
1. Change `"version": "0.1.0-pre.7"` to `"version": "0.1.0"`.
2. Change `"vrchatVersion": "2026.3.1"` to `"vrchatVersion": "2022.1.1"`.
3. Add `"com.vrchat.avatars": ">=3.10.4 <4.0.0-a"` to `vpmDependencies`.

```json
  "vpmDependencies": {
    "nadena.dev.ndmf": ">=1.14.4 <2.0.0-a",
    "com.vrchat.avatars": ">=3.10.4 <4.0.0-a"
  },
```

---

### 2.5 Release Artifact Documentation and Packaging Assets

#### Files
- Copy `<repo-root>/README.md` to `Packages/com.alrauna.amuse/README.md`.
- Copy `<repo-root>/LICENSE` to `Packages/com.alrauna.amuse/LICENSE`.
- Create companion `.meta` files for both copied assets.
- Create `Packages/com.alrauna.amuse/CHANGELOG.md` and companion `.meta` file.

#### Changelog Content
Document for v0.1.0:
- Initial stable release of AMUSE (Alrauna's Material Understanding & Simplification Engine).
- Supported shader frontends: lilToon 2.3.0 to 2.3.4, Poiyomi Toon 9.3.64 (including Two Pass).
- NDMF requirement: minimum 1.14.4, tested up to 1.14.8.
- VRChat Avatar SDK requirement: minimum 3.10.4.
- Core features: exact rational polygon alpha classification, non-destructive mesh splitting, canonical opaque material generation.
- Known limitations and caveats:
  - Finding 4: Deferred material content revalidation during the Apply pass.
  - Finding 9: Synchronous GPU readback hitch during large texture dry-runs.

---

### 2.6 GitHub Automation and Dispatch Prerequisites Verification

1. Verify `.github/workflows/release.yml`:
   - Enforces repository variable `PACKAGE_NAME = com.alrauna.amuse`.
   - Excludes `Tests/*` and `Tests.meta` from the release zip.
   - Excludes `Tests/*` from the unitypackage `metaList`.
   - Confirms production assembly `Editor/Alrauna.Amuse.Editor.asmdef` exists in the archive.
2. Verify `.github/workflows/build-listing.yml`:
   - Passes `--current-package-name ${{ vars.PACKAGE_NAME }}` to Nuke.
   - Deploys repository listing via GitHub Pages with GitHub Actions source.
3. Verify test prerequisite:
   - Full test run of `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` in the dev editor instance before any push.

---

## 3. Risk and Safety Analysis

| Risk | Likelihood | Impact | Mitigation in this Design |
|---|---|---|---|
| PlayMode fingerprint mismatch breaks existing test assertions | High | Medium | Update test in `PlayModeBuildStoreTests.cs` to assert 18 parts, matching the 17 component properties. |
| Clearing speculative sentences creates confusing empty strings | Low | Low | Design assigns standard `RowText.UntouchableSlotSentence(total)` which is already used for un-applied slots. |
| Inverting exception causes orphaned destroyed clone with missing shaders | Medium | High | Design retains the clone and records `TransientUnlockCloneRetained` whenever inversion does not prove complete. |
| Strict SDK upper bound blocks future SDKs before consent evaluates | Medium | Low | VCC packages follow semver ranges. Using `<4.0.0-a` aligns package installation with attested boundaries. |
| Missing `.meta` files cause Unity GUID churn | Low | High | Companion `.meta` files are created alongside all new documentation assets. |

---

## 4. Identifier Sweep

A sweep for prohibited identifiers was performed across this document:
- Zero absolute or machine-specific paths were found.
- Zero host names, user account names, ports, or GUIDs were found.
- Machine instances are named by role only.
- All dates are explicit.
