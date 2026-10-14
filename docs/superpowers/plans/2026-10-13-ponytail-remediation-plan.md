# Ponytail Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute the 51 selected ponytail audit findings as behavior-identical deletions, consolidations, and shrinks.

**Architecture:** Three classes of change in sequence: test consolidation (D3), dead-symbol deletion (D1), production shrink (D4). No behavior change. The existing suites are the whole regression guard.

**Tech Stack:** Unity 2022.3.22f1, EditMode NUnit through the Unity Test Runner, one editor assembly plus two test assemblies. No CLI test path.

**Spec:** `docs/superpowers/specs/2026-10-13-ponytail-remediation-design.md`

**Audit:** `docs/superpowers/audits/2026-10-13-ponytail-audit.md` (56 findings; this round executes all except 1, 4, 7, 9, and 13)

Revision note: first issue 2026-10-13. Five adversarial reviews ran the same day against the first issue: accuracy against the spec, accuracy against the repository, safety and security, architectural purity and ponytail adherence, and latent bugs. They found a critical whole-file deletion that would have destroyed kept alpha tests, a spec decision 8 clause that could not compile, a finding 35 carrier-file gap, a wrong fixture-file name, a false finding 36 premise, and several rewiring and count corrections. This revision incorporates every confirmed fix. The finding 36 drop and the subject-based test dispositions were the load-bearing changes. In the owner's review the same day, the owner declined findings 1, 4, and 13. This revision removes the finding 1 and finding 4 tasks, strips the finding 13 content from the display-chain task, and rescopes the round to 51 findings. Four parallel scouts then audited all 51 remaining findings for future-work debt at the owner's request. All returned keep. This revision folds their adjustments: Task 9 gains the two production call-site files of finding 11, Task 11 gains the finding 45 doc-comment correction and the finding 51 prose-comment note, Task 10 gains the vision-future-home note, Task 12 gains the finding 10 rescope note, and Task 14 gains the finding 50 vocabulary note.

## Global Constraints

- Behavior-identity contract (spec decision 7): every existing test outcome stays as it is, except tests whose subject a selected finding deletes and the sentence-based rewrite of spec decision 3.
- No new production type. No new production abstraction. New test types exist only per spec decision 8: `AnisotropicFilteringPin` under `Tests/Editor/Shared/`, the NDMF apply-lifecycle helper class under `Tests/Editor/Build/`, and internal static helpers beside their suites for findings 17 and 24.
- Attestation stays pinned data. No GUID, digest, version, or shader name in the attestation tables changes. A diff that touches one is a stop condition.
- No NDMF report string, refusal value, classification outcome, serialized field, or public API changes.
- Verification greps match deleted names as bare tokens across `Packages/com.alrauna.amuse/Editor/`, `Packages/com.alrauna.amuse/Tests/`, and `Packages/com.alrauna.amuse.research/` (spec decision 6). A failed verification drops the finding and records the drop. Spec decision 6 assigns findings 10, 15, 18, 28, 30, and 49 caller-grep verification; the Step 1 gates of their tasks are that verification, executed at task start, and finding 30's gate is the caller-grep form. The D3 findings 21, 36, 38, and 39 verify in-task per decision 6.
- Tests run in the Unity Test Runner, EditMode mode, through the dev editor instance. Refresh Unity after new files before running. A filtered run that reports zero tests is a failure. Record observed counts for every run.
- Never delete a test because it fails. A valid test that fails after a change is a stop condition.
- Every changed file passes the identifier sweep (`@hex`, drive-letter paths, home paths, four-digit ports, session-private names) before its commit.
- Stage only the paths a task names. Never stage `Assets/AmuseTests_Temp N.meta`. Implementation starts only after the owner reviews this plan (spec Authorization boundary).
- Stop and return evidence on any spec Validation-and-stop trigger.

Paths under the AMUSE package drop the `Packages/com.alrauna.amuse/` prefix. The research package paths name it.

---

### Task 1: Shared anisotropic-filtering pin (finding 6)

**Files:**

- Create: `Tests/Editor/Shared/AnisotropicFilteringPin.cs` and `.meta`
- Modify: `Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs`, `Tests/Editor/Semantics/Poiyomi/PoiyomiFixtureTestBase.cs`, `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`, `Tests/Editor/Build/AlphaSeparationApplyTests.cs`, `Tests/Editor/Build/AlphaSeparationPersistenceTests.cs`, `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`, `Tests/Editor/Build/AlphaSeparationSplitTests.cs`, `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`, `Tests/Editor/Build/MergedConsumptionFixture.cs`, `Tests/Editor/Host/AlphaEvidenceClassifierIntegrationTests.cs`, `Tests/Editor/Host/RendererAlphaAnalysisIntegrationTests.cs`

**Interfaces:**

- Produces: `Alrauna.Amuse.Tests.Editor.Shared.AnisotropicFilteringPin`, `IDisposable`. Constructor takes no arguments, saves `QualitySettings.anisotropicFiltering`, sets `AnisotropicFiltering.Enable`. `Dispose()` restores the saved value. Later tasks use `using (new AnisotropicFilteringPin())` inside fixtures.

- [ ] **Step 1: Verify the premise**

Grep `QualitySettings.anisotropicFiltering` and `_savedAnisotropicFiltering` across `Tests/`. Expect the eleven fixture copies the audit row names. Fewer than eleven means a copy already moved: adapt the file list, keep the count recorded.

- [ ] **Step 2: Create the pin**

```csharp
namespace Alrauna.Amuse.Tests.Editor.Shared
{
    /// <summary>
    /// Pins per-texture anisotropic filtering to Forced On for the scope of
    /// one using block. The dev editor persists the setting, so every test
    /// that needs it must restore the old value on exit.
    /// </summary>
    internal sealed class AnisotropicFilteringPin : System.IDisposable
    {
        private readonly UnityEngine.AnisotropicFiltering _saved;

        internal AnisotropicFilteringPin()
        {
            _saved = UnityEngine.QualitySettings.anisotropicFiltering;
            UnityEngine.QualitySettings.anisotropicFiltering =
                UnityEngine.AnisotropicFiltering.Enable;
        }

        public void Dispose()
        {
            UnityEngine.QualitySettings.anisotropicFiltering = _saved;
        }
    }
}
```

Refresh Unity. Expect compile success.

- [ ] **Step 3: Route the eleven fixtures**

In each fixture: delete the `_savedAnisotropicFiltering` field, the save-and-set block in `SetUp` (or `BaseSetUp`), the restore line in `TearDown` (or `BaseTearDown`), and the seven-line explanatory comment. Add a pin at the top of the same `SetUp`: `private AnisotropicFilteringPin _filteringPin;` assigned `_filteringPin = new AnisotropicFilteringPin();`, and `Dispose` it first in `TearDown`. `MergedConsumptionFixture` and the two fixture bases hold the pin for all their subclasses. Per-test `ForceEnable` pins inside test bodies stay untouched; they are deliberate behavior tests.

- [ ] **Step 4: Run the touched suites**

Run focused filters: `LilToonFixtureTestBase`-derived suites, `PoiyomiFixtureTestBase`-derived suites, `AlphaSeparationDryRunTests`, `AlphaSeparationApplyTests`, `AlphaSeparationPersistenceTests`, `AlphaSeparationPreparationTests`, `AlphaSeparationSplitTests`, `AmusePlatformFinishPluginTests`, `EffectiveMaterialMaterializationTests` (uses `MergedConsumptionFixture`? verify), `AlphaEvidenceClassifierIntegrationTests`, `RendererAlphaAnalysisIntegrationTests`. Expected: all green, observed counts recorded, unchanged totals.

- [ ] **Step 5: Sweep and commit**

Sweep the changed files. Then:

```bash
git add Tests/Editor/Shared/AnisotropicFilteringPin.cs \
  Tests/Editor/Shared/AnisotropicFilteringPin.cs.meta \
  Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs \
  Tests/Editor/Semantics/Poiyomi/PoiyomiFixtureTestBase.cs \
  Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs \
  Tests/Editor/Build/AlphaSeparationApplyTests.cs \
  Tests/Editor/Build/AlphaSeparationPersistenceTests.cs \
  Tests/Editor/Build/AlphaSeparationPreparationTests.cs \
  Tests/Editor/Build/AlphaSeparationSplitTests.cs \
  Tests/Editor/Build/AmusePlatformFinishPluginTests.cs \
  Tests/Editor/Build/MergedConsumptionFixture.cs \
  Tests/Editor/Host/AlphaEvidenceClassifierIntegrationTests.cs \
  Tests/Editor/Host/RendererAlphaAnalysisIntegrationTests.cs
git commit -m "refactor: consolidate anisotropic filtering pin into shared helper"
```

(All paths above are under `Packages/com.alrauna.amuse/`; keep that prefix in the actual commands.)

---

### Task 2: Tracked-object scope flag and Build routing (findings 3 Build side, 56)

**Files:**

- Modify: `Tests/Editor/Shared/TestTransientScope.cs`
- Modify: `Tests/Editor/Build/AlphaSeparationApplyTests.cs`, `AlphaSeparationPersistenceTests.cs`, `AlphaSeparationSplitTests.cs`, `LockedMaterialIdentityTests.cs`, `LockedMaterialReconstructionTests.cs`, `MergedConsumptionFixture.cs`, `TransientUnlockAvailabilityTests.cs`, `TransientUnlockSwapInTests.cs`, `TransientUnlockTransformationTests.cs`, `TransientUnlockWindowCloseTests.cs`, `TransientUnlockWindowStateTests.cs`
- Modify: `Tests/Editor/Build/TransientUnlockTestLifecycle.cs` (add `DeleteTempFolder`)

**Interfaces:**

- Consumes: the existing `TestTransientScope` constructor `(string tempFolder = null)`. Produces: that constructor plus one flag-carrying overload `TestTransientScope(bool allowDestroyingAssets, string tempFolder = null)`. The existing parameterless-at-call-site usages keep current semantics (no asset destruction). `TearDown` destroys tracked objects with `DestroyImmediate(obj, allowDestroyingAssets)`.

- [ ] **Step 1: Verify the premise**

Grep `private T Track<T>` and `List<UnityEngine.Object>` in `Tests/Editor/Build/`. Expect the eleven files above (the audit's ten classes plus the shared fixture base; `AlphaSeparationApplyTests` also holds one nested fixture holder). Grep `DeleteTempFolder`. Expect three byte-identical copies in `TransientUnlockSwapInTests`, `TransientUnlockTransformationTests`, `TransientUnlockWindowCloseTests`.

- [ ] **Step 2: Add the flag**

Keep the existing `(string tempFolder = null)` constructor. Add the flag beside it:

```csharp
private readonly bool _allowDestroyingAssets;

internal TestTransientScope(bool allowDestroyingAssets, string tempFolder = null)
{
    TempFolder = tempFolder;
    _allowDestroyingAssets = allowDestroyingAssets;
}
```

In `TearDown`, replace the destroy call with `UnityEngine.Object.DestroyImmediate(obj, _allowDestroyingAssets)`. The null check stays. No existing call site changes.

- [ ] **Step 3: Route the Build classes**

In each class: delete the `List<UnityEngine.Object>` field (or `List<Object>`), the `Track<T>` method, and the teardown loop. Replace with one `TestTransientScope _scope = new TestTransientScope(allowDestroyingAssets: <matches the deleted variant>)` and `return _scope.Track(obj);` in a thin `Track<T>` one-liner, or direct `_scope.Track(...)` at construction sites. Preserve each class's destroy-assets semantics: `AlphaSeparationApplyTests`, `LockedMaterialReconstructionTests`, `TransientUnlockSwapInTests`, `TransientUnlockTransformationTests`, and `TransientUnlockWindowCloseTests` pass `true` (correction 2026-10-14: the first issue omitted SwapIn; its deleted hunks show a pre-existing positional `DestroyImmediate(..., true)`); the rest use the parameterless form. `MergedConsumptionFixture` routes through `TestTransientScope(allowDestroyingAssets: true)` plus the unchanged `TempFolder` deletion, per the recorded Task 2 ruling. `TransientUnlockAvailabilityTests` has a teardown loop with no `Track<T>`: route its `_tracked` list the same way.

- [ ] **Step 4: Move `DeleteTempFolder`**

Add to `TransientUnlockTestLifecycle`:

```csharp
/// <summary>Deletes the temp folder the lifecycle created, if present.</summary>
internal static void DeleteTempFolder()
{
    if (TempFolder != null && UnityEditor.AssetDatabase.IsValidFolder(TempFolder))
    {
        UnityEditor.AssetDatabase.DeleteAsset(TempFolder);
    }
}
```

Delete the three copies and point their callers at `TransientUnlockTestLifecycle.DeleteTempFolder()`.

- [ ] **Step 5: Run the Build suites**

Run: `AlphaSeparationApplyTests`, `AlphaSeparationPersistenceTests`, `AlphaSeparationSplitTests`, `LockedMaterialIdentityTests`, `LockedMaterialReconstructionTests`, `MergedConsumptionFixture`-derived suites, `TransientUnlockAvailabilityTests`, `TransientUnlockSwapInTests`, `TransientUnlockTransformationTests`, `TransientUnlockWindowCloseTests`, `TransientUnlockWindowStateTests`, and the rest of `Tests/Editor/Build` if the runner groups them. Expected: green, unchanged totals, recorded counts.

- [ ] **Step 6: Sweep and commit**

Stage exactly the files listed under **Files** above. Then:

```bash
git commit -m "refactor: route build test suites through TestTransientScope"
```

---

### Task 3: Tracked-object routing for Analysis, Host, DryRun, and the editor tests (findings 3 rest, 40)

**Files:**

- Modify: `Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`, `Tests/Editor/Host/AnimatorServicesReactivationCharacterizationTests.cs`, `EffectiveMaterialMaterializationTests.cs`, `MeshCloneFinalizationCharacterizationTests.cs`, `RendererAlphaAnalysisIntegrationTests.cs`, `UnityAnimationEvidenceCaptureTests.cs`, `UnityMaterialEvidenceCaptureTests.cs`, `UnityRendererAlphaAnalysisTests.cs`, `UnityRendererAlphaSnapshotTests.cs`, `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`, `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**

- Consumes: `TestTransientScope` from Task 2.

- [ ] **Step 1: Verify the premise**

Grep `List<UnityEngine.Object>|List<Object>|List<Material>` in the named folders. Expect the copies the audit rows name, including the list-only forms in `UnityAnimationEvidenceCaptureTests` and `UnityMaterialEvidenceCaptureTests` (whose list is `List<Material> _materials` with a manual destroy loop) and the nested holder in `AnimatorServicesReactivationCharacterizationTests`.

- [ ] **Step 2: Route each class**

Same mechanical rule as Task 2 Step 3. `AlphaSeparationDryRunTests` keeps `_scope` for the folder lifecycle and drops its parallel `_transient` list plus local `Track<T>` (finding 40): route its tracked objects through `_scope.Track`. `AmuseAvatarOptimizerEditorTests` routes its `_tracked` list through a scope. Preserve each class's existing null-guard and destroy semantics exactly as found.

- [ ] **Step 3: Run the touched suites**

Run: `AdmittedMaterialStatesTests`, the nine Host suites, `AlphaSeparationDryRunTests`, `AmuseAvatarOptimizerEditorTests`. Expected: green, unchanged totals, recorded counts.

- [ ] **Step 4: Sweep and commit**

Stage exactly the files listed under **Files** above. Then:

```bash
git commit -m "refactor: route remaining test suites through TestTransientScope"
```

---

### Task 4: NDMF apply-lifecycle helper class (finding 2)

**Files:**

- Create: `Tests/Editor/Build/AlphaSeparationTestLifecycle.cs` and `.meta`
- Modify: `Tests/Editor/Build/AlphaSeparationApplyTests.cs`, `AlphaSeparationPersistenceTests.cs`, `AlphaSeparationPreparationTests.cs`, `AlphaSeparationSplitTests.cs`, `AmusePlatformFinishPluginTests.cs`

**Interfaces:**

- Produces: internal static class `AlphaSeparationTestLifecycle` with the consolidated members: `NewSwapClip`, `NewController`, `AddRenderer`, `DestroyGenerated`, `DestroyCommittedClone`, the committed-clip readers (`CommittedClipWithObjectBinding`, `CommittedClipCarrying`, `CommittedClips`), `DescribeCurve`, `SingleTriangleMesh`. Signatures: copy the dominant variant verbatim from the current copies. Four members have known body drift, not default-argument drift: `DescribeCurve` (number formatting and null rendering), `CommittedClipCarrying` (first-match versus `SingleOrDefault` throw-on-two), `AddRenderer` (sharedMaterials padding), and `DestroyCommittedClone` (recursive versus non-recursive state-machine destruction). For each: keep both variants under distinct internal names, or verify at every call site of the dropped variant that the divergence is unreachable and record that verification per suite.

- [ ] **Step 1: Verify the premise**

Grep each member name. Expect the copies per the audit row: `NewSwapClip` at ApplyTests:638, SplitTests:412, PreparationTests:10762, and so on through the full member list. Fewer or more copies: adapt, record.

- [ ] **Step 2: Create the helper and rewire**

Create the static class in `Tests/Editor/Build/` (namespace matches neighbors). Move each member once, verbatim from its dominant copy. Reconcile the four drifted members per the Interfaces rule. In the five suites, delete the private copies and qualify call sites with `AlphaSeparationTestLifecycle.`. No member gains or loses semantics.

- [ ] **Step 3: Refresh and run the touched suites**

Refresh Unity. Expect compile success. Run the five suites plus `MergedConsumptionFixture`-derived suites. Expected: green, unchanged totals, recorded counts.

- [ ] **Step 4: Sweep and commit**

Stage exactly the files listed under **Files** above, including the new file's `.meta`. Then:

```bash
git commit -m "refactor: consolidate ndmf apply lifecycle test helpers"
```

---

### Task 5: Preparation test arm runners and fixtures (findings 5, 8)

**Files:**

- Modify: `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**

- Produces: one private parameterized runner whose parameters are plain delegates and values, not a new options type: the material factory, the optional serialized policy-field writer, the optional fixtures and temporary-directory lifecycle step, and the explicit `RunBarrier` seam arguments including the per-arm `lilToonConversion` choice (`RunAlphaPolicyArm` passes `VerifiedFamilyConversion`; the other two arms take `RunBarrier`'s default). Plus one base arm fixture class holding the shared `Run()` and the `Create` skeleton, with `LocalityArmFixture`, `TransparentArmFixture`, and `DepthTestPolicyArmFixture` as subclasses supplying only their differences.

- [ ] **Step 1: Verify the premise**

Read `RunCutoutPolicyArm` (:5061), `RunMipPolicyArm` (:5133), `RunAlphaPolicyArm` (:5209) and the three nested fixtures. Confirm the shared shape the audit describes. Any drift beyond the audit's description: stop and report before parameterizing.

- [ ] **Step 2: Parameterize the runners and fold the fixtures**

Fold the three runners into one parameterized runner and the three nested fixtures onto one base class. The parameter set carries the three per-arm behavior differences the fold must preserve: the `lilToonConversion` seam choice per arm, the fixtures and `OverrideTemporaryDirectoryScope` lifecycle that the cutout and alpha arms own and the mip arm lacks, and each arm's exact serialized-write and acceptance-call order. `DepthTestPolicyArmFixture.Create` writes its policy and acceptance values before `PinAllSizes` and `AttachVrcDescriptor`; the runners write after. Preserve those orders exactly. Name the documented stale-cache hazard: a stale `SerializedObject` apply after the acceptance helper replays the cached component and restores acceptance to false. The three public test entry points stay as thin calls into the runner, so no test name disappears. Their doc comments lose the mirror-image sentences.

- [ ] **Step 3: Introduce the base arm fixture**

Move the byte-identical `Run()` and the shared `Create` skeleton into a private base class. Subclasses keep only their differences. No fixture class is deleted; test counts stay identical.

- [ ] **Step 4: Run the suite**

Run `AlphaSeparationPreparationTests`. Expected: green, unchanged totals (the audit's `AlphaSeparationPreparationTests` count), recorded.

- [ ] **Step 5: Sweep and commit**

```bash
git add Tests/Editor/Build/AlphaSeparationPreparationTests.cs
git commit -m "refactor: parameterize preparation arm runners and fixtures"
```

---

### Task 6: Remaining Build test consolidations (findings 16, 20, 23, 53)

**Files:**

- Modify: `Tests/Editor/Semantics/Poiyomi/PoiyomiFixtureTestBase.cs`, `Tests/Editor/Build/AlphaSeparationApplyTests.cs`, `AlphaSeparationPersistenceTests.cs`, `AlphaSeparationPreparationTests.cs`, `AlphaSeparationSplitTests.cs`, `AmusePlatformFinishPluginTests.cs`, `TransientUnlockTransformationTests.cs`, `TransientUnlockWindowCloseTests.cs`, `TransientUnlockSwapInTests.cs`, `TransientUnlockTestLifecycle.cs`, `FixtureAvatarIdentity.cs`

**Interfaces:**

- Produces: two shared statics `CreateVerifiedOpaqueMaterial` and `CreateVerifiedTransparentMaterial` beside `PoiyomiFixtureTestBase.CreateVerifiedMaterial` at `Tests/Editor/Semantics/Poiyomi/PoiyomiFixtureTestBase.cs` (finding 16; name that file explicitly, because `LilToonFixtureTestBase` hosts a same-named member); one blend-tree-aware clip helper pair on `TransientUnlockTestLifecycle` (finding 20); one shared `CreateOptedRoot` on `FixtureAvatarIdentity` (finding 23); `CreateSplitTexture` inlined at its single call site (finding 53).

- [ ] **Step 1: Verify the premises**

Grep `VerifiedOpaqueMaterial|VerifiedTransparentMaterial`: expect nine definitions across the five classes, not ten. `AlphaSeparationPersistenceTests` carries only the opaque variant, so its transparent call sites become calls to the shared static. Grep `CommittedClip|CurveMaterials|ClipInMotion` in the two transient-unlock suites, `CreateAvatarRoot|BuildAvatarRoot` (expect four copies), `CreateSplitTexture` (expect one definition, one call).

- [ ] **Step 2: Execute the four consolidations**

Finding 16: move the two mint variants beside `PoiyomiFixtureTestBase.CreateVerifiedMaterial`, delete the nine copies, qualify call sites, including the persistence suite's transparent call sites that gain their shared static. Finding 20: move the `WindowClose` copy's blend-tree-descending helper pair onto `TransientUnlockTestLifecycle`; delete both private copies; point both suites at it, so the transformation suite gains blend-tree descent it previously missed if its fixtures ever grow one. Finding 23: add `CreateOptedRoot` to `FixtureAvatarIdentity`, delete the four copies, rewire. Finding 53: inline `EnsureSplitFolder` plus `ImportSplitAlphaTexture` at the single `AlphaSeparationApplyTests` call site, delete the wrapper.

- [ ] **Step 3: Run the touched suites**

Run the five alpha-separation suites, the three transient-unlock suites, and any `FixtureAvatarIdentity` consumers. Expected: green, unchanged totals, recorded counts.

- [ ] **Step 4: Sweep and commit**

Stage exactly the files listed under **Files** above. Then:

```bash
git commit -m "refactor: consolidate remaining build test fixture helpers"
```

---

### Task 7: Semantics test consolidations (findings 17, 21, 24, 36, 39)

**Files:**

- Create: `Tests/Editor/Semantics/PropertySnapshot.cs` and `.meta` (finding 17 helper, internal static)
- Modify: `Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs`, `Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`, `Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs`, `Tests/Editor/Semantics/Poiyomi/PoiyomiFixtureTestBase.cs`, `Tests/Editor/Semantics/ReplacementTextureIdentityTests.cs`, `Tests/Editor/Semantics/ReplacementTextureAttestationTests.cs`, `Tests/Editor/Presets/PresetParserTests.cs`, `Tests/Editor/Presets/PresetFileStoreTests.cs`, `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`, `Tests/Editor/Semantics/LilToon/LilToonMultiResolutionTests.cs`, `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`, `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`, `Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`, plus `Tests/Editor/Shared/TestShaderWriter.cs` if the overload lands there

**Interfaces:**

- Produces: internal static `PropertySnapshot.SnapshotFloats(shader, mat)` and `PropertySnapshot.AssertUnchanged(...)` (finding 17); static helpers only for the `LilToon`/`Poiyomi` fixture-base boilerplate (finding 21: `Track` wrapper, `ImportTexture`, native-texture wrapper, verified-material mint), hosted on the existing fixture bases or an existing shared test type, with no new base class; one `ImportSourceAsset(marker, folder)` static beside the two replacement-texture suites (finding 24); `TestShaderWriter.WriteStandInShader(path, name, properties)` overload (finding 39). Finding 36 is dropped: the two preset JSON literals are not duplicates. `PresetParserTests.ValidJson` carries description "Shipped defaults." and `PresetFileStoreTests.ValidBody` carries a different description plus an alphaSeparator features block, and the store suite rewrites the name field through `ValidBody.Replace`, so sharing a verbatim constant would change a load-bearing fixture. Record the drop with this evidence.

- [ ] **Step 1: Verify the premises**

In-task verification per spec decision 6: byte-compare `SnapshotFloats` bodies, the fixture-base members, `ImportSourceAsset` bodies, the two preset JSON literals, and the shader-mint templates. The preset comparison is expected to fail; that failure is finding 36's recorded drop. Any other drift drops that finding and records the drop.

- [ ] **Step 2: Execute the four consolidations**

Finding 17: create `PropertySnapshot`, move the verbatim pair, delete both copies. Finding 21: extract the four duplicated members into static helpers on the existing fixture bases or one existing shared test type. The two fixture bases keep their class names and inheritance shape, so no derived suite changes. Finding 24: parameterize `ImportSourceAsset` on the folder constant, delete the twin. Finding 39: add the `WriteStandInShader` overload carrying the properties block, route the five mint sites through it, delete the inline templates.

- [ ] **Step 3: Refresh and run the touched suites**

Refresh Unity. Expect compile success. Run: `LilToonOpaqueTargetTests`, `PoiyomiOpaqueConversionTests`, the two fixture-base-derived families, `ReplacementTextureIdentityTests`, `ReplacementTextureAttestationTests`, `PresetParserTests`, `PresetFileStoreTests`, `UnityMaterialSemanticsTests`, `LilToonMultiResolutionTests`, and the Build/Host suites touched by the shader overload. Expected: green, unchanged totals, recorded counts.

- [ ] **Step 4: Sweep and commit**

Stage exactly the files listed under **Files** above, including any new helper's `.meta`. Then:

```bash
git commit -m "refactor: consolidate semantics and preset test helpers"
```

---

### Task 8: Analysis and Host test consolidations (findings 12, 22, 25, 26, 30, 37, 38, 48)

**Files:**

- Modify: `Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs`, `Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs` and the two other `OddBoundaryPixels` copies, `Tests/Editor/Host/AlphaFieldSetTests.cs`, `Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`, `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`, `Tests/Editor/Analysis/AlphaMipChainTests.cs`, `Tests/Editor/Analysis/SourceImageMaskedChainTests.cs`, `Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`, `Tests/Editor/Host/EffectiveMaterialMaterializationTests.cs`, `Tests/Editor/Host/MeshCloneFinalizationCharacterizationTests.cs`, `Tests/Editor/Build/AlphaSeparationSplitTests.cs`, plus the `LilToonCutoutAlphaTests`/`LilToonTransparentAlphaTests`/`PoiyomiCutoutSplitTests` copies of the corner triangle and refuse-provider helpers

**Interfaces:**

- Produces: a local `AddContainerTexture` helper inside `UnityGeneratedTextureEvidenceTests` (finding 12); one shared `Color32[] OddBoundaryPixels()` fixture helper as an internal static on an existing suite class in the same assembly (finding 25); one shared `EvidenceNamingAlphaSource()` builder as an internal static on an existing suite class (finding 26); one `NewTwoSlotFixture()` helper inside `EffectiveMaterialMaterializationTests` (finding 30); one shared lower-left `CornerTriangle()` fixture helper as an internal static on `AlphaSemanticsResolverTests` (finding 22; the different-shaped `TriangleAlphaClassifierTests.CornerTriangle` at :1505 stays); one always-refuse `AlphaFieldProvider` test stub formed by promoting one of the four existing per-file stubs to internal and routing the other three files to it (finding 38, no new type); `Array.Fill` at four loop sites (finding 37); one shared mesh-bounds helper as an internal static on an existing suite class for `AuthoredSubmeshBounds`/`AuthoredMeshBounds` (finding 48). No helper becomes a new type.

- [ ] **Step 1: Verify the premises**

Byte-compare each duplicated body named above (per spec decision 6). The `TriangleAlphaClassifierTests.CornerTriangle` at :1505 is a different shape: it stays. Drift drops the finding; record it.

- [ ] **Step 2: Execute the eight consolidations**

One helper per finding, exactly the audit replacement column. Finding 37 is mechanical: replace four hand-rolled fill loops with `Array.Fill`, matching the repo's existing usage in `TriangleAlphaClassifierTests.Opaque8x8Except`.

- [ ] **Step 3: Run the touched suites**

Run the named Analysis, Host, and Build suites plus the three Semantics suites carrying corner-triangle and provider copies. Expected: green, unchanged totals, recorded counts.

- [ ] **Step 4: D3 checkpoint: full assemblies**

Refresh Unity. Run the full `Alrauna.Amuse.Tests.Editor` assembly, then the full `Alrauna.Amuse.Research.Tests.Editor` assembly. Expected: zero failures, unchanged totals versus the recorded pre-round baselines, observed counts recorded. Any failure is a stop condition.

- [ ] **Step 5: Sweep and commit**

Stage exactly the files listed under **Files** above. Then:

```bash
git commit -m "refactor: consolidate analysis and host test fixture helpers"
```

---

### Task 9: D1 Host seam deletions (findings 11, 14, 19, 32)

**Files:**

- Modify: `Editor/Host/UnityAnimationEvidenceCapture.cs`, `Editor/Host/UnityAlphaFieldEvidence.cs`, `Editor/Host/UnityRendererAlphaAnalysis.cs`, `Editor/Host/AlphaFieldSet.cs`, `Editor/Build/AmusePlatformFinishPlugin.cs`, `Editor/DryRun/AlphaSeparationDryRun.cs`
- Modify: `Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`, `CapturedAlphaFieldBag.cs`, `UnityAlphaFieldEvidenceTests.cs`, `AlphaEvidenceClassifierIntegrationTests.cs`, `RendererAlphaAnalysisIntegrationTests.cs`, `UnityGeneratedTextureEvidenceTests.cs`, `LilToonCutoutAlphaTests.cs` (its `TryCapture` call), `UnityRendererAlphaAnalysisTests.cs`, `UnityRendererAlphaSnapshotTests.cs`, `AmusePlatformFinishPluginTests.cs`, `Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`, `AlphaFieldSetTests.cs`, `Tests/Editor/Build/AaoMergedConsumptionTests.cs`

**Interfaces:**

- Consumes: nothing new. Produces: narrower Host surface. `CaptureGraphThroughSeams` gains an observations parameter (finding 11); `Analyze(Renderer)` calls the private capture core directly (finding 19); kept forms per the audit: `HostStructuralRefusalFor(renderer, out, out)`, `CaptureGeometry`, `Analyze(Renderer)`, `Analyze(snapshot, ...)`.

- [ ] **Step 1: Verify the premises**

Bare-token greps per spec decision 6: `CaptureObservedForTests`, `TryCapture` short-form call sites, `Capture(` and `Analyze(` and `HostStructuralRefusalFor` call sites in all three roots, `WithField`, and `AlphaFieldSet.Count` plus the bare `fields.Count` reader. Confirm the audit caller facts hold, including `AaoMergedConsumptionTests.cs:216` reading `fields.Count` from `GatherAlphaFields` for an assertion message. A failed verification drops the finding and records the drop.

- [ ] **Step 2: Execute the four deletions**

Finding 11: delete both `CaptureObservedForTests` wrappers; add the observations parameter to `CaptureGraphThroughSeams`. Rewire every former wrapper call site to one exact shape: observations inserted after `currentSlots`, bounds pinned to `AlphaPolicyBounds.Inert` at every site, `lockedRefusalCheck` and `observedMaterialMapper` left null, and `out _` where the wrapper discarded the admitted set. The wrappers' doc comments pin the policy-free closure seam, so a positional slip that changes capture policy is a defect, not a compile error. The parameter shift touches the two production callers, `AmusePlatformFinishPlugin.cs:739` and `AlphaSeparationDryRun.cs:606`; add the null observations argument there. The compiler catches both sites. The landed dry run deliberately uses the production-shaped entry with real policy bounds, and this cut does not change that. Finding 14: delete the three convenience `TryCapture` overloads. Re-express each deleted-overload call site with exactly the arguments the deleted overload forwarded: the two channel-less overloads' call sites pass `TextureChannel.Alpha, 1.0f, AlphaPolicyBounds.Inert` explicitly, and `CapturedAlphaFieldBag.cs` keeps its per-request `channel` variable, calling `TryCapture(texture, channel, 1.0f, AlphaPolicyBounds.Inert, out source, out chain, out _)`, because the channel is part of its dictionary key. Finding 19: delete `Analyze(Renderer, RegisteredSourceLookup)`, `Capture(Renderer)`, `Capture(Renderer, RegisteredSourceLookup)`, and one-argument `HostStructuralRefusalFor`; fold `Analyze(Renderer)` onto the private capture core; rewire the named test sites to the kept forms. Finding 32: delete `AlphaFieldSet.WithField` and `AlphaFieldSet.Count`; rewire the five `WithField` test sites to the constructor-over-dictionary pattern the same files already use, and rewire `AaoMergedConsumptionTests.cs:216` to fold the count into a local before building the assertion message.

- [ ] **Step 3: Run the touched suites**

Run the Host suites, `AdmittedMaterialStatesTests`, `AlphaFieldSetTests`, `LilToonCutoutAlphaTests`, and `AmusePlatformFinishPluginTests` (it touches the capture seams). Expected: green, unchanged totals, recorded counts.

- [ ] **Step 4: Sweep and commit**

Stage exactly the files listed under **Files** above. Then:

```bash
git commit -m "refactor: delete test-only host capture and analysis seams"
```

---

### Task 10: D1 classifier and geometry seams (findings 41, 42, 47)

**Files:**

- Modify: `Editor/Analysis/ExactUvGeometry.cs`, `Editor/Analysis/TriangleAlphaClassifier.cs`
- Modify: `Tests/Editor/Analysis/ExactUvEnvelopeTests.cs`, `ExactUvGeometryTests.cs`, `AlphaSemanticsResolverTests.cs`, `TriangleAlphaClassifierTests.cs`, `AlphaSeparationPreparationTests.cs`, `Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`

**Interfaces:**

- Consumes: nothing. Produces: `TriangleAlphaClassifier.Classify` in one five-argument form; `ExactUvGeometry` without `HalfUlp` and without `FloorMod(BigInteger, int)`.

Execution note from the future-work audit: `ExactUvGeometry` is the file the vision names as the future home of UV-island, atlas, and texture-generation work. Both deleted helpers are trivially re-addable in whatever shape that future work needs, and no plan dated 2026-10 or later touches the file. Record in the execution note that a future atlas round should expect to grow this file.

- [ ] **Step 1: Verify the premises**

Bare-token greps: `HalfUlp`, `FloorMod`, `Classify(`. Confirm production always calls the five-argument `Classify` and the int `FloorMod`. Confirm `ExactUvGeometryTests.FloorMod_IntegerOverloadMatchesBigIntegerOverload` calls the BigInteger overload at :72 and :104 as oracle.

- [ ] **Step 2: Execute the three deletions**

Finding 41: delete `HalfUlp`; `ExactUvEnvelopeTests` gains a local 12-line half-ulp helper with the same body. Finding 42: delete `FloorMod(BigInteger, int)`; the equivalence test gains a local `BigInteger` floor-mod reference used as oracle at both former call sites (spec invariant 3); the reflection arm pins the int overload directly. Finding 47: delete the four-argument `Classify` overload; every test call site appends the literal `0` for the noise percent.

- [ ] **Step 3: Run the classifier matrix**

Run `ExactUvEnvelopeTests`, `ExactUvGeometryTests`, `TriangleAlphaClassifierTests`, `AlphaSemanticsResolverTests`, `AlphaSeparationPreparationTests`, `UnityMaterialEvidenceCaptureTests`, and `AlphaFieldSetTests`-adjacent classifier consumers. Expected: green, unchanged totals, recorded counts. Any outcome change is a stop condition.

- [ ] **Step 4: Sweep and commit**

```bash
git add Editor/Analysis/ExactUvGeometry.cs Editor/Analysis/TriangleAlphaClassifier.cs Tests/Editor/Analysis/
git commit -m "refactor: delete classifier and geometry test-only overloads"
```

---

### Task 11: D1 remaining deletions (findings 27, 31, 34, 35, 43, 44, 45, 51, 52, 54, 55)

**Files:**

- Modify: `Editor/Analysis/RuntimeStateResolution.cs`, `Editor/Semantics/UnityMaterialSemantics.cs`, `Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`, `Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs`, `Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs`, `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`, `Editor/Build/TransientUnlockWindowClose.cs`, `Editor/Build/HostLifecycleCapability.cs`, `Editor/Semantics/LilToon/LilToonSourceAttestation.cs`, `Editor/Semantics/AaoAtlasTextureAttestation.cs`, research `Tests/Editor/Calibration/AlphaEvidenceProbe.cs`
- Modify the matching test files: `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (finding 27 sites :5070, :5133, :7274), `TransientUnlockWindowCloseTests.cs`, `HostLifecycleCapabilityTests.cs`, `LilToonAttestationTests.cs`, `LilToonSourceAttestationTests.cs`, `Tests/Editor/Shared/AttestationEnvironment.cs`, and the five no-out overload consumer files (`AdmittedMaterialStatesTests.cs`, `AlphaSeparationPreparationTests.cs`, `VerifiedPoiyomiTestSeams.cs`, `RendererAlphaAnalysisIntegrationTests.cs`, `LilToonMultiResolutionTests.cs`, `LilToonCutoutAlphaTests.cs`, `LilToonTransparentAlphaTests.cs`), plus research `AlphaEvidenceCharacterizationTests.cs` construction sites

**Interfaces:**

- Consumes: nothing. Produces: no public surface change; deleted symbols per the audit rows.

- [ ] **Step 1: Verify the premises**

Bare-token greps for all eleven names across the three roots. Confirm: `AnalyzeRuntimeStatesForTests` callers are the three named test sites; `UnityMaterialSemantics.AnalyzeBaseMaterial` callers are its own tests only; the two-argument `CurveInversionWasComplete` has zero callers; the no-out alpha overloads have test-only callers; `SupportedAssumption` has one test reader; `GatherOpaqueTargetSourceEvidence`, `AaoAtlasTextureAttestation.SetAdmittedVersionsForTests`, `Canonicalize`, `ExecuteClose`, `LilToonActivatorOccurrence.Text`, and `AlphaProbeLevel.Mip` have the audit's caller facts. Drops are recorded.

- [ ] **Step 2: Execute the deletions**

Finding 27: delete the method; the three test sites compose `ResolveRuntimeStates` + `ClassifyRuntimeStates` directly. Finding 31: delete the dispatcher and its three test calls. Finding 34: delete the two-argument overload. Finding 35: delete the five no-out overloads, which live in `LilToonMaterialSemantics.cs:507`, `LilToonCutoutMaterialSemantics.cs:100`, `LilToonTransparentMaterialSemantics.cs:143`, and `PoiyomiMaterialSemantics.cs:908` and :935; each test site appends `out _`. Finding 43: delete `SupportedAssumption`, its constructor parameter, and the built string; fix the one test assertion. Finding 44: delete the method; `LilToonOpaqueTargetTests` rewires to `TryGatherSourceEvidenceForShaderName`. Finding 45: delete the forwarded seam. Its doc comment at `AaoAtlasTextureAttestation.cs:61` claims a test addresses it; the bare-token verification will flag that stale comment, so correct the comment in the same task. Finding 51: delete `Canonicalize`; `AttestationEnvironment` appends `.CanonicalSource` on the analysis form. The bare-token grep also hits the prose comment at `LilToonSourceAttestation.cs:2374-2375` and the `Canonicalize_`-named tests. Reword the comment, and keep the test names while rewiring their bodies to the analysis form. The landed ltcgi regression matrix reaches canonicalization through that shared helper, so its outcomes are untouched. Finding 52: delete `ExecuteClose`. Finding 54: delete `LilToonActivatorOccurrence.Text` and its constructor assignments. Finding 55: delete the `Mip` property and the `mip` constructor parameter, dropping the argument at the single `new AlphaProbeLevel` construction site; `TryCaptureLevel(texture, mip)` keeps its parameter.

- [ ] **Step 3: Run the touched suites**

Run the named Build, Host, Semantics, and research suites. Expected: green, unchanged totals, recorded counts. The attestation suites (`LilToonAttestationTests`, `LilToonSourceAttestationTests`) must pass unchanged: a pinned-value diff is a stop condition.

- [ ] **Step 4: Sweep and commit**

Stage exactly the files listed under **Files** above. The research file's full path is `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/AlphaEvidenceProbe.cs`. Then:

```bash
git commit -m "refactor: delete dead production symbols and test seams"
```

---

### Task 12: Build production shrinks (findings 10 rescoped, 18, 28, 29)

**Files:**

- Modify: `Editor/Build/AmusePlatformFinishPlugin.cs`, `Editor/Build/AmuseReports.cs`, `Editor/Build/AlphaSeparationPreparation.cs`, `Editor/Build/AlphaSeparationApply.cs`, `Editor/Build/TransientUnlockWindowClose.cs`, `Editor/Build/TransientUnlockSwapIn.cs`
- Modify: `Tests/Editor/Build/TransientUnlockSwapInTests.cs`, `Tests/Editor/Build/TransientUnlockTestLifecycle.cs` (construction sites per grep)

**Interfaces:**

- Consumes: `RuntimeStateResolution` policy mapping helpers (they already exist) and the existing `AmuseReports` type, which already owns `SlotSeparationRefusal`. Produces: the four cut wrappers gone (`ProofMipCapFrom`, `MinTextureSizeFrom`, `MinimumCoverageFrom`, `MinimumPreserveCoverageFrom`); the three reflection-pinned wrappers stay (spec decision 13); one internal static refusal helper on `AmuseReports`, shaped like `TransientUnlockSwapIn.RecordRefusal`, used by the eleven two-step sites in preparation, apply, and window-close, with no visibility change to `RecordRefusal` (spec decision 8 as amended); `TransientUnlockSwapIn` takes `TransientUnlockRestoreDelegate` directly; `HasAnyUnknownOutcome` uses `System.Linq`.

- [ ] **Step 1: Verify the premises**

Bare-token greps: the seven wrapper names (string-literal reflection sites count as callers — spec decision 6), the eleven `RecordSlotRefusal` plus `SlotSeparationRefusal` two-step sites, `Availability`, `HasAnyUnknownOutcome`. Confirm the rescope: exactly the four wrappers lack reflection callers. Execution note from the future-work audit: the rescope is load-bearing. The landed show-material-transformations plan declares that its reflection tests keep passing through the three wrappers by name, and those tests pin wiring-time inertness, defensive clamping, and the density wiring-time throw. Do not widen the cut back to seven wrappers.

- [ ] **Step 2: Execute the four shrinks**

Finding 10 (rescoped): delete the four wrappers; their single call sites in the barrier `Execute` become `RuntimeStateResolution.<Helper>(optimizer?.<Field> ?? <inert>)`, preserving the wiring-time throw for out-of-range density coverage. Finding 18: add one internal static helper on `AmuseReports`, shaped like `TransientUnlockSwapIn.RecordRefusal`; route the eleven two-step sites in preparation, apply, and window-close through it; delete the redundant explicit `rendererName` arguments where the callee computes the same fallback. Finding 28: replace the `Availability` parameter with the delegate; keep `FromProduction`'s production wiring at `AmusePlatformFinishPlugin.cs:582` by passing the delegate; rewire the two test construction sites, one of which is `Tests/Editor/Build/TransientUnlockTestLifecycle.cs:194`. Finding 29: replace the hand-rolled loop with `submeshOutcomes?.Contains(TriangleAlphaOutcome.Unknown) == true` behind the existing null and range guards.

- [ ] **Step 3: Run the Build suites**

Run `AmusePlatformFinishPluginTests` (including the three reflection-pinned mapper tests and `DensityMapperRejectsAboveRangePolicyAtWiringTime`), `AlphaSeparationPreparationTests`, `AlphaSeparationApplyTests`, `AlphaSeparationSplitTests`, `AlphaSeparationPersistenceTests`, and the transient-unlock suites. Expected: green, unchanged totals, recorded counts.

- [ ] **Step 4: Sweep and commit**

Stage exactly the files listed under **Files** above. Then:

```bash
git commit -m "refactor: shrink build barrier policy and refusal plumbing"
```

---

### Task 13: Attestation gather dedup (finding 15)

**Files:**

- Modify: `Editor/Semantics/LilToon/LilToonSourceAttestation.cs`

**Interfaces:**

- Consumes: nothing new. Produces: one private shared gather helper used by both `Gather` and `GatherMultiContainerEvidence`. Both private gather bodies keep their callers' signatures, their pass resolution, their render-mode scan differences, and their fail-closed omissions.

- [ ] **Step 1: Verify the premise**

Read `Gather` (:2456-2557) and `GatherMultiContainerEvidence` (:2559 onward) side by side. Both are the private gather bodies behind the internal entry points. Confirm the audit's byte-for-byte front-half claim and the differing tails. Any divergence the audit missed: stop and report.

- [ ] **Step 2: Extract the shared front half**

Move the common sequence (GUID and local id, version stamp, package lookup, project root, shader path, include-tree enumerate and digest, canonicalization analysis, canonical digest) into one private helper. Both entry points call it and keep their distinct tails. No pinned value is recomputed differently: the digest inputs and order are byte-identical to before.

- [ ] **Step 3: Run the attestation suites**

Run `LilToonAttestationTests`, `LilToonSourceAttestationTests`, `LilToonOpaqueTargetTests`, and the opaque conversion suites. Expected: green, unchanged totals. The diff must show moved lines, not changed constants.

- [ ] **Step 4: Sweep and commit**

```bash
git add Editor/Semantics/LilToon/LilToonSourceAttestation.cs
git commit -m "refactor: deduplicate lilToon attestation gather front half"
```

---

### Task 14: Dry run display chain (findings 50, 33, 46, 49, in sequence)

**Files:**

- Modify: `Editor/DryRun/MaterialTransformationRow.cs`, `Editor/DryRun/DryRunCache.cs`, `Editor/AmuseAvatarOptimizerEditor.cs`, `Editor/DryRun/AlphaSeparationDryRun.cs`
- Modify: `Tests/Editor/DryRun/MaterialTransformationRowTests.cs`, `AlphaSeparationDryRunTests.cs`, `DryRunCacheTests.cs`, `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**

- Produces: no `Disclosure` kind on `MaterialTransformationRow`. The eight row data fields and the widened builders stay exactly as they are, per the owner's decision on finding 13. `AmuseAvatarOptimizerEditor.TransformationRows` replaced by a one-line internal test seam or removed with the two test sites concat'ing `AvatarRows`/`RendererRows`; no `DryRunFingerprint.Renderer` wrapper; `DryRunCache` avatar slot as one nullable field plus `storedAvatarKey`, one `Clear` method.

- [ ] **Step 1: Verify the premises**

Bare-token greps: `Disclosure`, `TransformationRows`, `DryRunFingerprint.Renderer`. Confirm no emitter produces a `Disclosure` row, the drawer's kind switch has no Disclosure case, the two disclosure sentences render as editor labels through `RowText.DisclosureSentence`, and the decision 9 caller facts hold.

- [ ] **Step 2: Unused disclosure kind (finding 50)**

Delete the `Disclosure` member, its builder, and the `Disclosure`-shaped test touches whose subject is the kind: `AvatarLevelFactoriesCarryNoRendererCounts` (:303-317) drops its `Disclosure` construction and its field loop and becomes a Banner and Note kind round-trip plus sentence non-empty, and the `Disclosure(null)` throws case (:384-385) drops from the empty-sentence guard test, whose surviving guard coverage stays. The eight data fields, the sentinel pins, and `ProvenTriangleDisplaysAsWouldSplit` stay untouched. Execution note from the future-work audit: the landed feature design declares five row kinds with disclosure rows always present, but the landed code meets that contract through the two editor labels, so the code's row-kind count becomes four. Record that vocabulary divergence in the execution note. Run `MaterialTransformationRowTests`, `AlphaSeparationDryRunTests`. Expected: green with the recorded delta, recorded counts.

- [ ] **Step 3: Property seam (finding 33)**

Delete the 25-line concatenating `TransformationRows` property; expose the dry run internally or concat the internal lists at the two test sites. Run `AmuseAvatarOptimizerEditorTests`. Expected: green, unchanged totals.

- [ ] **Step 4: Fingerprint wrapper and cache slot (findings 46, 49)**

Delete `DryRunFingerprint.Renderer`; `AlphaSeparationDryRun.cs:424` uses its local `body`; `DryRunCacheTests` reads `RendererFingerprintBody`. Collapse the `AvatarRows` dictionary to one nullable field guarded by `storedAvatarKey`; merge `Clear`/`ClearAll`. Run `DryRunCacheTests` including the avatar-one/avatar-two sweep. Expected: green, unchanged totals, recorded counts. A stale-row failure is a stop condition.

- [ ] **Step 5: Sweep and commit**

Stage exactly the files listed under **Files** above. Then:

```bash
git commit -m "refactor: cut dry run disclosure kind and cache wrapper surface"
```

---

### Task 15: Full validation

**Files:** none. Validation only.

- [ ] **Step 1: Acceptance sweep**

Verify each spec acceptance criterion: the executed list against the decision 1 list (51 findings, with decision 13's finding 10 rescope and any recorded drops); the new test helper types at their decision 8 paths; the deletion greps by symbol class (criterion 3); the `LilToonSourceAttestation.cs` diff shows moved lines only; the working tree holds only this round's changes plus the user-owned untracked temp meta files.

- [ ] **Step 2: Full assemblies**

Refresh Unity. Run the full `Alrauna.Amuse.Tests.Editor` assembly, then `Alrauna.Amuse.Research.Tests.Editor`. Record observed counts and compare with the Task 8 checkpoint and the per-task focused-suite baselines. Expected: zero failures.

- [ ] **Step 3: Tree hygiene**

Run `git status --porcelain` and `git diff --check`. Expected: clean except the user-owned untracked temp meta files and no whitespace errors. Run `git log --oneline 2170174..HEAD` and confirm one commit per task.

Report the executed findings, any recorded drops, the observed counts, the commit list, and the final net line estimate to the owner. Do not commit in this task.
