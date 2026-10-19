# AMUSE v0.1.0 Release Audit Remediation Investigation

Date: 2026-10-19

## Status and scope

- Branch: `cleanup/v0-1-0-release-audit`
- Intended change: investigation document for v0.1.0 release audit remediation.
- Production source, tests, research source, assets, and workflows: read-only inspection.
- Active instances: the dev editor instance.
- Private avatars and external assets: not used.

This investigation answers one central question:
What exact changes must AMUSE make to resolve the v0.1.0 release audit across the build pipeline, package metadata, release documentation, and GitHub automation?

## Privacy note

This investigation reads public repository source code and public project metadata. It records no private avatar name. It records no private identifier. It records no machine-specific file path.

## Method

On 2026-10-19, this investigation inspected the codebase across four target areas:

1. Build pipeline code defects in `PlayModeBuildStore.cs`, `AmusePlatformFinishPass.cs`, `AlphaSeparationApply.cs`, and `TransientUnlockWindowClose.cs`.
2. Package metadata and VPM dependency specifications in `Packages/com.alrauna.amuse/package.json` and `HostLifecycleCapability.cs`.
3. Release artifact documentation, licensing files, and changelog requirements in `.github/workflows/release.yml` and `Packages/com.alrauna.amuse/`.
4. GitHub Actions release workflows, repository dispatch variables, and testing prerequisites in `.github/workflows/release.yml` and `.github/workflows/build-listing.yml`.

All findings cite file paths and line ranges observed on 2026-10-19.

---

## 1. Build Pipeline Code Defects

### 1.1 Policy Fingerprint Incompleteness in PlayModeBuildStore

#### Observation
`Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs:52-68` computes a policy fingerprint for caching Play mode build records. It serializes `activeTarget.ToString()` and 12 numeric and boolean fields:
- `PreserveTransparencyMaxMipLevel`
- `PreserveTransparencyMinTextureSize`
- `PolygonMinimumOpaqueCoveragePercent`
- `MinimumOpaqueCoveragePercent`
- `MinimumOpaqueCoverageToPreservePercent`
- `CutoutPreserveTransparencyMaxMipLevel`
- `CutoutPreserveTransparencyMinTextureSize`
- `CutoutMinimumOpaqueCoveragePercent`
- `CutoutMinimumOpaqueCoverageToPreservePercent`
- `AllowDepthTestChange`
- `MinimumOpaqueAlphaPercent`
- `PolygonAlphaUpperClampPercent`

In contrast, `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs:1378-1406` computes a 17-field policy fingerprint. `PlayModeBuildStore.ComputePolicyFingerprint` omits five component fields:
1. `_amuseDisabled` (`component.AmuseDisabled`)
2. `_alphaSeparatorEnabled` (`component.AlphaSeparatorEnabled`)
3. `_ignoreOutOfRangeMaterialSlots` (`component.IgnoreOutOfRangeMaterialSlots`)
4. `_acceptDesktopAnisotropyDifferences` (`component.AcceptDesktopAnisotropyDifferences`)
5. `_acceptMobileAnisotropyDifferences` (`component.AcceptMobileAnisotropyDifferences`)

#### Root Cause
The author did not update the policy fingerprint in `PlayModeBuildStore.cs` when adding master feature switches and anisotropy policy flags to `AmuseAvatarOptimizer`.

#### Failure Mode
If a user performs a Play mode build, and then disables AMUSE or disables the Alpha Separator, the component policy fingerprint does not change. On subsequent inspector redraws, `PlayModeBuildStore.TryLoad` matches the cached record from the previous run. `AmuseAvatarOptimizerEditor.cs:265-268` displays the cached transformation rows. The inspector tells the user that the view displays accurate results from the Play mode build. That claim is false because the user disabled optimization.

#### Remediation
Update `PlayModeBuildStore.ComputePolicyFingerprint` to mirror all 17 component properties. Include `AmuseDisabled`, `AlphaSeparatorEnabled`, `IgnoreOutOfRangeMaterialSlots`, `AcceptDesktopAnisotropyDifferences`, and `AcceptMobileAnisotropyDifferences`.
Update `PlayModeBuildStoreTests.cs:68` where the test asserts that splitting the fingerprint yields 13 elements. The updated string will contain 18 elements (`activeTarget` plus 17 fields). Add tests asserting that toggling each of the five flags changes the fingerprint.

---

### 1.2 Persistent Speculative Sentences for Skipped Candidate Slots

#### Observation
In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs:1225-1257`, candidate slots generate transformation rows. When `isCandidate` is true, line 1242 or 1246 formats `sentence` using `DryRun.RowText.ConvertableSlotSentence(total)` or `DryRun.RowText.SplittableSlotSentence(proven, total)`. These sentences state:
- `"Convertable: X out of X polygons may be moved"`
- `"Splittable: X out of Y polygons may be moved"`

Line 1265 initializes `movedTriangles` to 0.
Later, in `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:1134-1150`, `UpdateAppliedMovedTriangles` runs:
```csharp
for (var i = 0; i < state.PlayModeRendererRows.Count; i++)
{
    var row = state.PlayModeRendererRows[i];
    if (row.kind == 3 && row.provenTriangles > 0)
    {
        if (appliedSlots.Contains((row.rendererPath, row.slotIndex)))
        {
            row.movedTriangles = row.provenTriangles;
            state.PlayModeRendererRows[i] = row;
        }
    }
}
```
If a candidate slot is skipped during Apply, `appliedSlots.Contains` returns false. `row.movedTriangles` remains 0. The method does not update `row.sentence`.
In `AlphaSeparationApply.cs:1180-1234` (the fallback path executed when `state.PlayModeRendererRows.Count == 0`), lines 1208-1221 format `sentence` using `ConvertableSlotSentence` or `SplittableSlotSentence` even when `isApplied` is false.

#### Root Cause
`AlphaSeparationApply` updates `movedTriangles` but does not update `sentence` when candidate slots are skipped.

#### Failure Mode
In Play mode builds, any candidate slot that was skipped during Apply displays a sentence stating that polygons "may be moved". The inspector displays `movedTriangles = 0`, but the text indicates polygons were eligible to move. This contradicts the purpose of the Play mode build record. The record must show actual applied build results.

#### Remediation
In `AlphaSeparationApply.UpdateAppliedMovedTriangles`, when a row is not in `appliedSlots`, set `row.movedTriangles = 0` and update `row.sentence` to `DryRun.RowText.UntouchableSlotSentence(row.totalTriangles)`.
In `AlphaSeparationApply.CaptureAndSavePlayModeBuildRecord` (lines 1208-1221), check `isApplied`. If `isApplied` is false, set `sentence = DryRun.RowText.UntouchableSlotSentence(total)`.
Add tests in `AlphaSeparationApplyTests.cs` to verify that skipped candidate slots clear their speculative sentence and display the untouchable sentence.

---

### 1.3 Unguarded Material Reversion in TransientUnlockWindowClose

#### Observation
In `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:124-136`, `Execute` iterates through open swapped pairs:
```csharp
foreach (var pair in window.OpenPairs.ToList())
{
    InvertReferences(committedClips, pair);

    ReassertShippedSlots(pair, recorded);

    if (DestroyPairCopiesOrNameRetention(
            context, finishState, pair, committedClips, graph))
    {
        window.Remove(pair);
    }
}
```
`InvertReferences` iterates over `pair.Slots` and rewrites renderer `sharedMaterials` from the unlocked clone back to the locked original. It then calls `InvertCommittedCurves` across all committed animation clips.
`ReassertShippedSlots` re-applies recorded finalization material writes.
`DestroyPairCopiesOrNameRetention` verifies that no curve or renderer references the clone, destroys the clone, and removes the pair from the window.

#### Root Cause
The operations inside the `foreach` loop have no exception boundaries. If an exception occurs during reference inversion on one renderer slot or one animation curve, the method aborts. The method does not process remaining pairs in `window.OpenPairs`.

#### Failure Mode
If an unexpected exception occurs during inversion or reassertion:
1. The faulted pair leaves its unlocked clone in memory without completing verification or refusal recording.
2. All subsequent pairs in `window.OpenPairs` remain unprocessed. Their unlocked clones stay assigned to avatar renderers.
3. The build pass leaves cloned objects in memory. Build artifacts may reference transient unlocked copies.

#### Remediation
Wrap pair processing in structured guarded blocks.
Each pair iteration in `Execute` requires a `try/catch` boundary. If an exception occurs, log the exception and invoke `DestroyPairCopiesOrNameRetention`.
`DestroyPairCopiesOrNameRetention` invokes `CurveInversionWasComplete`. If any curve or renderer still references the unlocked clone, AMUSE must retain the clone and record `AlphaSeparationSlotRefusal.TransientUnlockCloneRetained`. AMUSE must never destroy the clone while live references remain. Destroying an in-use clone creates missing object references.
Inside `InvertReferences`, wrap renderer slot restoration and animation curve inversion so that a failure on one slot does not prevent restoring remaining slots.
Inside `ReassertShippedSlots`, wrap individual slot writes so that a failure on one slot does not abort subsequent slot writes.
Add tests in `TransientUnlockWindowCloseTests.cs` to verify that when an individual slot throws an exception, remaining slots are still processed, live references retain the clone safely, and refusals are recorded.

---

## 2. Package Metadata and Dependency Specifications

### 2.1 Package Manifest Version
In `Packages/com.alrauna.amuse/package.json:4`, on 2026-10-19, `"version"` is `"0.1.0-pre.7"`.
For the stable v0.1.0 release, update this value to `"0.1.0"`.

### 2.2 Invalid VRChat Version Baseline
In `Packages/com.alrauna.amuse/package.json:23`, on 2026-10-19, `"vrchatVersion"` is `"2026.3.1"`.
This value is an invalid future typo.
The standard VRChat Unity baseline for avatars is `"2022.1.1"`, as used in `Packages/nadena.dev.ndmf/package.json:7`.
Update `"vrchatVersion"` to `"2022.1.1"`.

### 2.3 VPM SDK Dependency Range
In `Packages/com.alrauna.amuse/package.json:17-19`, on 2026-10-19, `vpmDependencies` declares only:
```json
"vpmDependencies": {
  "nadena.dev.ndmf": ">=1.14.4 <2.0.0-a"
}
```
In `Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs:95-99`, AMUSE enforces runtime requirements for the VRChat SDK:
- `VrchatSdkFloor = { 3, 10, 4 }`
- `VrchatSdkUpperBound = { 4, 0, 0 }`

Declaring `"com.vrchat.avatars": ">=3.10.4 <4.0.0"` (or `"<4.0.0-a"`) in `vpmDependencies` allows the VRChat Creator Companion (VCC) to enforce SDK compatibility at package installation time.
Note the architectural trade-off: `HostLifecycleCapability.cs:170-178` supports a runtime consent prompt (`PackageVersionBeyondMajorBound`) for SDK versions at or beyond 4.0.0. A strict upper bound in `package.json` causes VCC to block installation before runtime consent can evaluate. Enforcing the verified boundary in `package.json` provides earlier protection for standard users.

---

## 3. Release Artifact Documentation and Packaging Assets

### 3.1 Packaging Exclusions and File Locations
In `.github/workflows/release.yml:64-65`, the release workflow creates the package zip:
```bash
working-directory: "${{ env.packagePath }}"
run: zip -r "${{ github.workspace }}/${{ env.zipFile }}" . -x "Tests/*" "Tests.meta"
```
The workflow also builds `metaList` for the unitypackage from `Packages/com.alrauna.amuse/`:
```bash
run: find "${{ env.packagePath }}/" -name \*.meta -not -path "*/Tests/*" -not -path "*/Tests.meta" >> metaList
```
On 2026-10-19, `README.md` and `LICENSE` reside only at `<repo-root>/`. They do not exist inside `Packages/com.alrauna.amuse/`.
As a result, neither the release zip nor the unitypackage contains user documentation or license terms.
Copying `README.md` and `LICENSE` directly into `Packages/com.alrauna.amuse/` ensures both distribution formats include documentation and license files. Unity assets require companion `.meta` files, so valid Unity `.meta` files must accompany both files.

### 3.2 Package Changelog
`Packages/com.alrauna.amuse/package.json:9` defines `"changelogUrl"`. Standard VPM packages also include a local `CHANGELOG.md` file in the package root.
The author must create `Packages/com.alrauna.amuse/CHANGELOG.md` and its companion `.meta` file.
The changelog for version 0.1.0 must document:
- Supported shader frontends: lilToon 2.3.0 to 2.3.4, Poiyomi Toon 9.3.64 (including Two Pass).
- NDMF version requirement: minimum 1.14.4, tested up to 1.14.8.
- VRChat Avatar SDK requirement: minimum 3.10.4.
- Core features: exact rational polygon alpha classification, non-destructive mesh splitting, canonical opaque material generation.
- Known limitations and caveats:
  - Finding 4: Deferred material content revalidation during the Apply pass (`docs/superpowers/investigations/2026-10-15-full-repo-audit.md:65`).
  - Finding 9: Synchronous GPU readback hitch during large texture dry-runs.

---

## 4. GitHub Repository Automation and Dispatch Prerequisites

### 4.1 Release Workflow Configuration
In `.github/workflows/release.yml:21-28`:
The workflow validates `${{ vars.PACKAGE_NAME }}`. If the variable is unset, the build job skips execution.
For the workflow to succeed, the maintainer must configure `PACKAGE_NAME` in repository settings as `com.alrauna.amuse`.
In `.github/workflows/build-listing.yml:61`, the workflow passes `--current-package-name ${{ vars.PACKAGE_NAME }}`. This command also requires the variable.

### 4.2 GitHub Pages Deployment Source
In `.github/workflows/build-listing.yml`, the listing deployment uses `actions/deploy-pages@v4`.
The repository GitHub Pages settings must have the build and deployment source configured to "GitHub Actions".

### 4.3 Clean Artifact Verification
The release workflow explicitly enforces:
- `Tests/` and `Tests.meta` are excluded from the zip.
- `Tests/` is excluded from the unitypackage file list.
- Production editor assembly `Editor/Alrauna.Amuse.Editor.asmdef` is present.
- The research package (`Packages/com.alrauna.amuse.research/`) is outside `Packages/com.alrauna.amuse/` and is never included in the release artifacts.

---

## 5. Verification Strategy

1. Code Fix Verification:
   - Run tests in `Tests/Editor/Build/` in the dev editor instance to verify:
     - `PlayModeBuildStoreTests` passes with the 17-field policy fingerprint.
     - `AlphaSeparationApplyTests` passes and asserts skipped candidate slot sentences are Untouchable.
     - `TransientUnlockWindowCloseTests` passes and verifies error boundaries.
2. Metadata Verification:
   - Validate JSON syntax of `Packages/com.alrauna.amuse/package.json`.
   - Verify `version`, `vrchatVersion`, and `vpmDependencies`.
3. Asset Verification:
   - Verify `Packages/com.alrauna.amuse/README.md`, `LICENSE`, `CHANGELOG.md`, and their `.meta` files.
4. Full Suite Validation:
   - Run all tests in `Alrauna.Amuse.Tests.Editor` in the dev editor instance.
   - Run all tests in `Alrauna.Amuse.Research.Tests.Editor` in the dev editor instance.

---

## 6. Identifier Sweep

A sweep for prohibited identifiers was performed across this document:
- Zero absolute or machine-specific paths were found.
- Zero host names, user account names, ports, or GUIDs were found.
- Machine instances are named by role only.
- All dates are explicit.
