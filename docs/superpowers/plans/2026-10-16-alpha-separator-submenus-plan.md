# Alpha Separator Submenus Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to execute this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the Transparent and Cutout submenus with four arm-scoped cutout settings, arm-pure capture and scope selection, per-slot coverage, the version 4 presets, and the honest upgrade backfill.

**Architecture:** The component holds both arms' values and backfills once on upgrade. Capture buckets pick their cap by arm. `GatherAlphaFields` scopes each field by its captured threshold. Preparation and the dry run select coverage per slot from the recorded menu membership, through one shared decision ladder. Presets and reports carry both arms honestly.

**Tech Stack:** Unity 2022.3.22f1, C#, NDMF, NUnit via Unity Test Framework, EditMode only. No dotnet build, no Node.

**Spec:** `docs/superpowers/specs/2026-10-16-alpha-separator-submenus-design.md`

**Investigation:** `docs/superpowers/investigations/2026-10-16-alpha-separator-submenus-investigation.md`

## Working discipline

- Branch: `feat/alpha-separator-submenus`. Base: the spec commit (`0171473`) on that branch. Do not branch from anything else.
- Allowed mutations, exhaustive: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`, `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`, `Packages/com.alrauna.amuse/Editor/Presets/OptimizerPreset.cs`, `Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs`, `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs`, `Packages/com.alrauna.amuse/Presets/safe.json`, `Packages/com.alrauna.amuse/Presets/normal.json`, `Packages/com.alrauna.amuse/Presets/aggressive.json`, `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`, `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs`, `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`, `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs`, `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`, `Packages/com.alrauna.amuse/Editor/Analysis/SlotTransformDecision.cs`, `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`, `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`, `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`, `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`, `README.md`, and the mirrored test files under `Packages/com.alrauna.amuse/Tests/Editor/`.
- Never touch: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated `.csproj`/`.sln`, `Packages/nadena.dev.ndmf/`, `Packages/com.alrauna.amuse.research/`, anything under `docs/superpowers/` dated before 2026-10-16, and any file not listed above.
- Every task ends with exactly one commit of the paths the task names. Do not stage unrelated files. The owner authorized one commit per finalized task on 2026-10-16.
- Stop and return evidence when: a step cannot be completed as written because the code differs from this plan; a test fails for a reason the task does not name; compilation fails and the fix is not mechanical; or a discovery matches the spec's stop conditions.
- Unity validation: request an asset refresh through the Unity MCP refresh tool and wait for readiness before running tests. Run tests through the Unity MCP test runner. A filtered run that reports zero tests is a failure. Record observed pass and fail counts in the task report.
- Falsifier markers follow the house style: a `--- Falsifier N: ... ---` comment in the test's doc comment block, plus a no-op guard assertion so a later change cannot silence the test.
- Every label, tooltip, report sentence, description, and README sentence uses short active sentences. No contractions. One idea per sentence.

## Global constraints

- The two keys never swap. Proof mechanics (bucket caps, field scopes) key on the captured threshold value below one. Policy (coverage gates, fold cause) keys on the recorded predicate declaration. The stored family enum keys nothing.
- The inert-under-cutoff capture contract is untouched. No capture route reads policy bounds for a cutoff source.
- No new refusal values. No new production types. No new serialized field beyond the four settings and the backfill marker.
- RED/GREEN: write the test first, run it, observe the failure against the named wrong implementation, record the observed failure, then apply the minimal production change and observe green. A first-run pass is recorded as characterization.
- Runtime file changes are data, doc comments, and the backfill callback only.
- The preset version 4 cutover is one atomic commit across the parser, the record, the applier, and the three shipped files.
- Existing serialized names, JSON keys of existing settings, refusal values, and the clamp-pair invariant never change.

## Task 1: Runtime fields and the upgrade backfill

**Files:**
- Modify: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`

**Interfaces:**
- Produces: `CutoutPreserveTransparencyMaxMipLevel`, `CutoutPreserveTransparencyMinTextureSize`, `CutoutMinimumOpaqueCoveragePercent`, `CutoutMinimumOpaqueCoverageToPreservePercent` (int properties), serialized fields `_cutoutPreserveTransparencyMaxMipLevel` (4), `_cutoutPreserveTransparencyMinTextureSize` (128), `_cutoutMinimumOpaqueCoveragePercent` (25), `_cutoutMinimumOpaqueCoverageToPreservePercent` (5), `_cutoutSettingsBackfilled` (false). Later tasks read exactly these names.

- [ ] **1.1** In `Runtime/AmuseAvatarOptimizer.cs`, add the four serialized fields in one block after `_polygonMinimumOpaqueCoveragePercent` and before `_allowDepthTestChange`, with `[Range(0, 100)]` on the two percents, defaults 4, 128, 25, 5, and doc comments quoting the identical inspector labels and stating that each setting governs cutout sources only. Add `[SerializeField] private bool _cutoutSettingsBackfilled;` directly before them. Add the read-only properties beside the transparent ones. Do not add the callback yet.
- [ ] **1.2 RED first:** in `Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`, add the backfill suite. `UpgradedComponentBackfillsCutoutValuesFromItsOwnTransparentValues`: create a component, set `_preserveTransparencyMaxMipLevel` to 7, `_preserveTransparencyMinTextureSize` to 256, `_minimumOpaqueCoveragePercent` to 40, `_minimumOpaqueCoverageToPreservePercent` to 0 through the serialized object, then set `_cutoutSettingsBackfilled` to false through the same serialized object, which is exactly the state a legacy payload deserializes to. Simulate the legacy payload with `JsonUtility.FromJsonOverwrite` using a JSON object that contains only the transparent fields and none of the cutout fields, and assert the four cutout fields equal 7, 256, 40, 0 and `_cutoutSettingsBackfilled` is true. `BackfillRunsOnceAndKeepsEditedCutoutValues`: after one backfill, set a cutout field to a different value, re-run the overlay, and assert the edit survives. `FreshComponentKeepsEditedCutoutValuesAcrossAReload`: create a component, write the four cutout fields through the serialized object, drive a serialize and deserialize round trip, and assert the edited values survive. Add the no-op guard `Assert.That(4 + 4, Is.EqualTo(8))` inside the suite so a later cleanup cannot silence it. Add the falsifier block `--- Falsifier 9: A missing backfill lets an upgraded component with transparent preserve 0 read cutout preserve 5 and flatten cutout slots the base never flattened. The backfill suite asserts equality with the transparent values and fails it. ---` in the suite's doc comment block. Never invoke `OnAfterDeserialize` directly in a test; the trigger under test is the serialization pipeline. Run the `AmuseAvatarOptimizerTests` filter and observe the backfill assertions fail, because the component has no callback. Record the observed failure. If `FromJsonOverwrite` does not fire the callback on any route the tests drive, switch to a true deserialization route: donor component, `EditorJsonUtility.ToJson`, a temporary prefab asset outside `Assets`, instantiate. If that also fails, stop and report, because the trigger design becomes an owner decision.
- [ ] **1.3** Add `ISerializationCallbackReceiver` to the class declaration. The marker field from 1.1 carries `[SerializeField]`; without it the marker resets on every load and the copy would overwrite stored cutout values each session. Add:

```csharp
public void OnBeforeSerialize()
{
}

public void OnAfterDeserialize()
{
    if (_cutoutSettingsBackfilled)
    {
        return;
    }
    _cutoutSettingsBackfilled = true;
    _cutoutPreserveTransparencyMaxMipLevel = _preserveTransparencyMaxMipLevel;
    _cutoutPreserveTransparencyMinTextureSize = _preserveTransparencyMinTextureSize;
    _cutoutMinimumOpaqueCoveragePercent = _minimumOpaqueCoveragePercent;
    _cutoutMinimumOpaqueCoverageToPreservePercent = _minimumOpaqueCoverageToPreservePercent;
}
```

with a doc comment stating why: an upgraded component must keep proofing every source with the values its owner set, so the one-time copy keeps the upgrade verdict-identical.
- [ ] **1.4** Add the four default tests (`DefaultCutoutPreserveTransparencyMaxMipLevelIs4`, and one per field) and four serialized round-trip tests, mirroring the existing pairs. The default tests pass on first run; record them as characterization. The backfill tests from 1.2 now pass; record the observed green.
- [ ] **1.5** Refresh Unity, run the `AmuseAvatarOptimizerTests` filter. Expect green, nonzero count. Record counts.
- [ ] **1.6** Commit: `git add Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs && git commit -m "feat: add arm-scoped cutout settings with upgrade backfill"`

## Task 2: Policy fan-out, fingerprint, and play-mode store

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`, `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/DryRun/DryRunCacheTests.cs`

**Interfaces:**
- Consumes: Task 1 properties.
- Produces: the plugin mapping block and `AlphaSeparationDryRun.MapPolicy` each carry `cutoutMaxMipLevel`, `cutoutMinTextureSize`, `cutoutMinimumCoverage`, `cutoutPreserveCoverage` fields or locals with exactly these names; the dry-run fingerprint string includes the four cutout values; the play-mode store record includes them.

- [ ] **2.1 RED first:** in `Packages/com.alrauna.amuse/Tests/Editor/DryRun/DryRunCacheTests.cs`, add `FingerprintChangesWhenACutoutValueChanges`: build two fingerprints that differ only in `_cutoutPreserveTransparencyMaxMipLevel` (5 versus 6) and assert they differ. Add the falsifier block `--- Falsifier 5: A fingerprint that omits the cutout values leaves the dry-run view stale after a cutout edit. The fingerprint-change assertion fails it. ---`. Run the filter and observe the failure, because the fingerprint omits the cutout values. Record the failure.
- [ ] **2.2** In `AmusePlatformFinishPlugin.cs`, read the four cutout properties in the mapping block beside the transparent ones, through the same defensive mappers (`ProofMipCapFrom`, `MinTextureSizeFrom`, `MinimumCoverageFrom`, `MinimumPreserveCoverageFrom`). At this commit the only consumers are the dry run and the store, so carry the reads no further; Task 3 reads them at its own stamp sites.
- [ ] **2.3** In `AlphaSeparationDryRun.cs`, extend `MapPolicy` with the same four reads into new private fields of the same names.
- [ ] **2.4** In the dry-run fingerprint method, append the four cutout values beside the thirteen existing entries, in the order mip cap, texture size, coverage, preserve. In `PlayModeBuildStore.cs`, extend `ComputePolicyFingerprint` with the four raw stored values beside the transparent ones, so the store's fingerprint match distinguishes all eight.
- [ ] **2.5** Add the mapper fan-out tests to `AmusePlatformFinishPluginTests`, mirroring the policy-mapper suite: inert without a component, defensive on out-of-band stored values, exact in band, one test per arm sharing the fixture shape.
- [ ] **2.6** Add the play-mode store test: two records whose transparent and cutout values differ produce different fingerprints.
- [ ] **2.7** Re-run the RED test and observe green. Refresh Unity, run the touched test filters. Expect green, nonzero counts. Record counts.
- [ ] **2.8** Commit: `git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/PlayModeBuildStoreTests.cs Packages/com.alrauna.amuse/Tests/Editor/DryRun/DryRunCacheTests.cs && git commit -m "feat: carry cutout policy values through mapping, fingerprint, and store"`

## Task 3: Arm-pure capture caps

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`, `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`, `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Consumes: Task 1 properties and Task 2's mapped values.
- Produces: `ClosedAlphaMaterialCapturer` with two new trailing parameters `int cutoutRawMipCap, int exactRawMipCap`; `TryCaptureClosedAlphaMaterials` and `TryCaptureClosedAlphaMaterialsTransferred` with the same two trailing parameters; every lambda materializing the delegate updated; each capture bucket's chain built at its arm's cap.

- [ ] **3.1 RED first:** in `UnityMaterialEvidenceCaptureTests.cs`, author `MixedBatchCapturesEachArmAtItsOwnCap` against the current one-cap signature: one admitted batch with a lilToon cutout material and a transparent material sharing one source texture, cutout cap 0 and transparent cap 4. The base signature accepts one cap, so the test asserts the ideal per-arm outcome in comments and initially asserts the strongest claim the current signature can express: the exact arm's chain holds five levels. Run the filter and observe the behavioral failure, because one request carries one cap. Add the falsifier block `--- Falsifier 2: A batch-uniform capture cap, stamped once per batch as at base, gives both arms one cap. The mixed-batch shared-texture fixture asserts each arm's chain level count and fails it. ---`. Record the failure. The signature growth in 3.2 then migrates the test to the two-cap form.
- [ ] **3.2** Grow the delegate and the two entry points with the two trailing cap parameters. The plumbing chain is longer than the delegate: the caps must reach the entry points through `CaptureObserved`, `CaptureGraph`, `CaptureWithAnimationIndex`, `Capture`, and `CaptureGraphThroughSeams`, because the machinery invokes the capturer at the batch assembly site where no cap exists in scope. Update the plugin's `CaptureWithAnimationIndex` call site and the dry run's capture entry beside it. Update every materializing lambda: the finish plugin's transferred lambda, the wrapper in `CaptureThroughEffectiveMaterials`, the dry-run `RealCapturer`, `ProbeCapturer`, and `DefaultInnerCapturer`, and the test fakes `FixtureCapturer`, `CountingCapturer`, and `CapturerFailingOrdinals`.
- [ ] **3.3** Stamp the batch request with the wider cap: `request.WithRawMipCap(Math.Max(cutoutRawMipCap, exactRawMipCap))` at both wrapper sites, replacing the single-cap stamp.
- [ ] **3.4** Carry the two caps into the closed-batch capture only. `Capture(inputs, bounds)` grows the two parameters; the one-argument `Capture(inputs)` overload and the null-bounds route keep the smallest-cap merge exactly as at base, because they serve conversion-eligibility, attestation, and opaque-target reads that carry family-schema caps and must never see the component's arm values. In the closed-batch shared-bucket path, each bucket's shared chain captures at its arm's cap: `bucketCap = bucketThreshold < 1f ? cutoutRawMipCap : exactRawMipCap`. The bucket threshold is the bucket key, so no key dimension grows. The unshared fallback keeps the request cap, which is over-capture only.
- [ ] **3.5** Add `CutoutMaterialsAtCutoffOneCaptureAtTheTransparentCap`: a cutout material with `_Cutoff` exactly 1.0 sharing a texture with a transparent material captures one exact-arm chain at the transparent cap. Add the falsifier block `--- Falsifier 3: A bucket cap keyed on the predicate declaration instead of the captured threshold lets a cutoff-1.0 cutout material stamp the cutout cap into the 1.0 bucket. The exact arm's chain level count assertion fails it. ---`
- [ ] **3.6** Add `ChangingTheCutoutCapLeavesTheExactArmsChainUnchanged`: capture the mixed batch with cutout cap 3 and assert the exact arm's chain still holds five levels. Add the falsifier block `--- Falsifier 4: A cross-family leak lets a cutout value move a transparent verdict, or the reverse. The isolation assertions fail it. ---` covering this and the sibling test.
- [ ] **3.7** Re-run the RED test and observe green. Refresh Unity, run the capture test filter and the plugin test filter. Expect green, nonzero counts. Record counts.
- [ ] **3.8** Commit: `git add Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs && git commit -m "feat: capture each proof arm at its own mip cap"`

## Task 4: Record the menu membership per material

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`

**Interfaces:**
- Produces: `CapturedAlphaMaterial` gains `public bool PredicatesDeclareCutoff { get; }`, true when the material's own predicate request declares a cutoff property, with a constructor parameter that defaults to false so existing construction sites compile unchanged. Derived evidence keeps the value. Later tasks read exactly this name.

- [ ] **4.1 RED first:** in `UnityMaterialEvidenceCaptureTests.cs`, add `MenuMembershipRecordsPerMaterial`: capture one batch covering the six sources and assert each recorded flag: lilToon cutout true, lilToon transparent false, Multi mode 1 true, Multi mode 2 false, Poiyomi cutout preset true, Poiyomi plain false. Add the falsifier block `--- Falsifier 8: A coverage selection keyed on the stored family enum routes a Poiyomi cutout preset and a plain Poiyomi sibling to the same pair. The Poiyomi sibling rows of this table assert different memberships and fail it. ---` as the no-op guard on the Poiyomi rows. Run the filter and observe the failure, because `CapturedAlphaMaterial` carries no such member. Record the failure.
- [ ] **4.2** In `CaptureBatch`, where each `MaterialEvidenceCaptureInput` is built, evaluate the predicate's cutoff declaration once per material from `AlphaPredicateRequestFor(materials[index], families[index])` and thread the bool into the capture outcome so `CapturedAlphaMaterial` carries it. The declaration test mirrors the gate the capture assignments already use for `DeclaresCutoffFor`, evaluated on the input's own predicate request.
- [ ] **4.3** Extend `CapturedAlphaMaterial` with the optional constructor parameter defaulting to false, set it at the production construction sites in `UnityMaterialSemantics.cs`, and pass the original material's recorded value through the derived-evidence rebuild in `AdmittedMaterialStates.cs`, which must not silently reset it to false.
- [ ] **4.4** Run the table test and observe green.
- [ ] **4.5** Refresh Unity, run the touched filters. Expect green, nonzero counts. Record counts.
- [ ] **4.6** Commit: `git add Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs && git commit -m "feat: record per-material cutoff membership at capture"`

## Task 5: Arm-scoped field selection in GatherAlphaFields

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`, `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`

**Interfaces:**
- Consumes: Task 2 values, Task 3 captured arms.
- Produces: `GatherAlphaFields(IReadOnlyList<CapturedAlphaMaterial> materials, int maxMipLevel, int cutoutMaxMipLevel, int minTextureSize, int cutoutMinTextureSize)`. Callers: the plugin, the dry run, and the structural probe, which passes the uncapped pair for both arms.

- [ ] **5.1 RED first:** add `EachFieldScopesByItsOwnArm`: a captured material set where a cutoff field and an exact field share one source, cutout cap 0 with transparent cap 4, cutout size 32 with transparent size 128. Assert the cutoff field's chain holds one level and the exact field's holds five, and that a 64-texel texture, which sits in the band between the two floors, drops only from the transparent arm. Add the falsifier block `--- Falsifier 1: A shared-scope implementation applies one mip cap or one size minimum to both arms in GatherAlphaFields. The mixed-renderer fixture with a cutout cap of 0 and a transparent cap of 4 shows one arm over-reading or under-reading. The per-arm assertion fails it. ---`. Author the test against the current signature, observe the behavioral failure, then grow the signature and migrate the test. Record the failure.
- [ ] **5.2** Grow the signature to the five parameters. Inside the texture loop, select the pair by the texture's own captured threshold: below one reads the cutout pair, the sentinel reads the transparent pair. Apply the size gate and `Math.Min(maxMipLevelOfArm, sizeScope)` with the selected pair. No other body change.
- [ ] **5.3** Update the five call sites: the production three are the plugin, the dry run, and the structural probe, which passes `int.MaxValue, int.MaxValue, 1, 1`. The test callers `AaoMergedConsumptionTests.cs` and `AmusePlatformFinishPluginTests.cs` pass the uncapped pair for both arms.
- [ ] **5.4** Re-run the RED test and observe green. Refresh Unity, run the touched filters. Expect green, nonzero counts. Record counts.
- [ ] **5.5** Commit: `git add Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AaoMergedConsumptionTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs && git commit -m "feat: scope each alpha field by its own proof arm"`

## Task 6: Per-slot coverage selection in preparation and the dry run

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/SlotTransformDecision.cs`, `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`, `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Analysis/SlotTransformDecisionTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: Task 2 percents, Task 4 membership.
- Produces: `Decide(MeshSeparationPlan plan, IReadOnlyList<SlotResolutionResult> slotResults, IReadOnlyList<bool> alreadyOpaqueBySlot, IReadOnlyList<int> minimumOpaqueCoveragePercents, IReadOnlyList<int> minimumOpaqueCoverageToPreservePercents)`; `Prepare` gains exactly three per-slot vectors after its existing percents: `IReadOnlyList<int> coveragePercentsBySlot`, `IReadOnlyList<int> preservePercentsBySlot`, and `IReadOnlyList<bool> cutoutMembershipBySlot`, all indexed by slot ordinal and covering the largest binding index plus one. Preparation passes each slot's own percents into its two gate calls and reads the membership vector as the fold cause for Task 7. Both callers build the vectors from `PredicatesDeclareCutoff` of each slot's live captured material.

- [ ] **6.1 RED first:** in the decision test class, add `LadderReadsPerSlotPercents`: a two-slot plan where slot 0 pairs with the transparent percents and slot 1 with the cutout percents, values chosen so one ladder outcome differs per slot. Author it against the current one-pair signature, observe the behavioral failure, then grow `Decide` and `DecideSlot` to the per-slot percent vectors and migrate the test. Add the falsifier block `--- Falsifier 10: A dry run that keeps one coverage pair diverges from the build for a mixed renderer at non-default values. The shared-ladder test asserts equal verdicts and fails it. ---`. Record the failure. The gates `PreserveSlotQualifies(slotEntries, percent)` and `OpaqueCoverageBelow(percent, submesh)` keep their signatures and math.
- [ ] **6.2** In the finish plugin, build the three per-slot vectors: for each slot, read the slot's live captured material's `PredicatesDeclareCutoff`, select the transparent or cutout pair, and fill the two percent vectors and the membership vector. Pass the vectors into `Prepare`'s new parameters. Preparation passes each slot's own percents into its two gate calls and keeps the membership vector for the fold cause.
- [ ] **6.3** In the dry run, build the same vectors from the same recorded membership and pass the two percent vectors into `Decide`, replacing the single-pair call.
- [ ] **6.4** In `AlphaSeparationPreparationTests`, add `EachSlotSelectsItsOwnCoveragePair`: a renderer with a cutout slot and a transparent slot at different values, asserting the fold and refusal outcomes match each slot's own pair. Add the falsifier block `--- Falsifier 8: A coverage selection keyed on the stored family enum routes a Poiyomi cutout preset and a plain Poiyomi sibling to the same pair. The Poiyomi sibling fixture asserts different pairs and fails it. ---` on a Poiyomi sibling fixture built from Task 4's evidence.
- [ ] **6.5** In `AmusePlatformFinishPluginTests`, add the spec's two end-to-end pins. `MixedRendererHonorsEachArmsValuesEndToEnd`: a cutout slot and a transparent slot with different caps, size minimums, and coverages, asserting each honors its own through the full barrier. `PoiyomiSiblingsProveUnderDifferentPairs`: one cutout-preset Poiyomi slot and one plain Poiyomi slot on one renderer, asserting different pairs. These pins make the lazy route of passing the request cap twice at the batch assembly site fail, because only the arm-split plumbing survives them.
- [ ] **6.6** In the dry-run tests, add `DryRunVerdictsMatchTheBuildOnAMixedRenderer`: the mixed renderer from 6.4 runs through both paths and produces equal per-slot verdicts. Re-run the 6.1 RED test and observe green.
- [ ] **6.7** Refresh Unity, run the touched filters. Expect green, nonzero counts. Record counts.
- [ ] **6.8** Commit: `git add Packages/com.alrauna.amuse/Editor/Analysis/SlotTransformDecision.cs Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs Packages/com.alrauna.amuse/Tests/Editor/Analysis/SlotTransformDecisionTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs && git commit -m "feat: select coverage per slot by recorded proof arm"`

## Task 7: Cutout fold-cause report variants

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`, `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs`, `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs`

**Interfaces:**
- Consumes: Task 6's per-slot selection and membership vector.
- Produces: the forced-opaque and folded-premultiply report calls carry a bool `causedByCutoutPreset` read from the membership vector, never from percent comparison, because two presets honestly store equal values; the string layer selects `amuse.slotSeparation.ForcedOpaqueCutout` and `amuse.slotSeparation.PremultiplyNormalizationFoldedCutout`, mirroring the existing variant pair.

- [ ] **7.1 RED first:** in the reports slot test class, add `CutoutFoldReportsTheCutoutVariant`: fold a slot through the cutout preserve value and assert the report text references the Cutout submenu's preserve setting. Add `TransparentFoldReportsByteIdenticalBaseOutput`: fold through the transparent value and assert the report text equals the base string. Run the filters and observe both failures, because one variant exists. Record the failures.
- [ ] **7.2** In `AmuseReportStrings.cs`, add the two cutout variants as full three-part entries beside the existing folded pair, referencing the Cutout submenu. Read the existing entries first and match their exact entry shape.
- [ ] **7.3** In `AmuseReports.cs` and the preparation call sites, carry the fold cause from Task 6's membership vector and select the key exactly as the premultiply variant precedent selects its key.
- [ ] **7.4** Re-run the RED tests and observe green. Refresh Unity, run the report filters. Expect green, nonzero counts. Record counts.
- [ ] **7.5** Commit: `git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs && git commit -m "feat: report the cutout fold cause in the folded disclosures"`

## Task 8: The preset schema version 4 cutover

One atomic task. The parser, the record, the applier, the three shipped files, and every pinned fixture land in one commit.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs`, `Packages/com.alrauna.amuse/Editor/Presets/OptimizerPreset.cs`, `Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs`, `Packages/com.alrauna.amuse/Presets/safe.json`, `Packages/com.alrauna.amuse/Presets/normal.json`, `Packages/com.alrauna.amuse/Presets/aggressive.json`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Presets/OptimizerPresetTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetApplierTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetFileStoreTests.cs`

**Interfaces:**
- Consumes: Task 1 serialized field names.
- Produces: `CurrentSchemaVersion = 4`; sixteen mapped values; the four new `SettingKeys` entries directly after `polygonMinimumOpaqueCoveragePercent`.

- [ ] **8.1 RED first:** in `PresetParserTests`, add `SchemaVersionThreeRefusesAsUnknown`, mirroring the version-two refusal test. Run the `PresetParserTests` filter and observe the failure, because the parser still accepts version 3. Record the failure. Add the falsifier block `--- Falsifier 7: A parser that invents cutout values for a version 3 file answers a preset instead of UnknownSchemaVersion. The refusal test fails it. ---`
- [ ] **8.2** In `PresetParser.cs`: set `CurrentSchemaVersion` to 4; add the four cutout keys to `SettingKeys` directly after `polygonMinimumOpaqueCoveragePercent` in the order mip cap, texture size, coverage, preserve; read the mip key through the mip range reader, the size key through the size range reader, and the two percents through `TryReadPercent`; thread the values into the `OptimizerPreset` constructor directly after their transparent counterparts.
- [ ] **8.3** In `OptimizerPreset.cs`: add the four properties, grow the constructor with the four arguments beside their transparent counterparts, add the assignments and the `Matches` clauses, and update the count comments from twelve to sixteen.
- [ ] **8.4** In `PresetApplier.cs`: write the four serialized fields beside their transparent counterparts and update the count comments from twelve to sixteen.
- [ ] **8.5** Edit the three shipped files: bump `"schemaVersion"` to 4 and add the four cutout keys after `polygonMinimumOpaqueCoveragePercent` with the spec's Data files values: Safe 4, 128, 25, 0; Normal 4, 128, 25, 5; Aggressive 3, 128, 25, 10. Change nothing else.
- [ ] **8.6** Update the fixtures in the same task. The `WithSettings(find, replace)` helper anchors every replace on the literal version string inside `ValidJson`, so bumping the body silently no-ops every helper: update every `WithSettings` find literal and both legacy version builders to the version 4 source, retarget `WrongSchemaVersionRefuses` to version 5, and author `SchemaVersionThreeRefusesAsUnknown` by stripping the four cutout keys from the version 4 body, not by replacing the version literal. `ValidSchemaParsesAllTwelveValues` becomes `ValidSchemaParsesAllSixteenValues`; add one range-edge refusal per reader shape routed through a cutout key and one missing-key representative; `ShippedDefaults` gains the four literals; the flip table gains four rows, with the falsifier block `--- Falsifier 6: A preset press that matches only the transparent fields presses while a cutout value differs. The sixteen-row flip table fails it. ---` above it; `ApplyWritesAllTwelveFields` becomes `ApplyWritesAllSixteenFields`; add the shipped-apply test `ApplyingEachShippedPresetWritesItsCutoutValues` over the three real files, asserting Safe's preserve 0 and Aggressive's mip cap 3 among them; `ValidBody` becomes version 4 with the four keys.
- [ ] **8.7** Re-run the RED test and observe green. Refresh Unity, run all four preset filters. Expect green, nonzero counts, including the real shipped files. Record counts.
- [ ] **8.8** Commit: `git add Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs Packages/com.alrauna.amuse/Editor/Presets/OptimizerPreset.cs Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs Packages/com.alrauna.amuse/Presets/safe.json Packages/com.alrauna.amuse/Presets/normal.json Packages/com.alrauna.amuse/Presets/aggressive.json Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetParserTests.cs Packages/com.alrauna.amuse/Tests/Editor/Presets/OptimizerPresetTests.cs Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetApplierTests.cs Packages/com.alrauna.amuse/Tests/Editor/Presets/PresetFileStoreTests.cs && git commit -m "feat: cut the preset schema over to version 4"`

## Task 9: The inspector submenus

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**
- Consumes: Task 1 field names, the spec's tooltip texts.
- Produces: `DrawTransparentSubmenu()` and `DrawCutoutSubmenu()`; parameterized `DrawMipCapPopup(string propertyPath, GUIContent content)` and `DrawMinTextureSizePopup(string propertyPath, GUIContent content)`; four new `GUIContent` fields named `CutoutMipCapRowContent`, `CutoutMinTextureRowContent`, `CutoutMaterialCoverageRowContent`, `CutoutPreserveCoverageRowContent`.

- [ ] **9.1 RED first:** in `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`, add `SubmenuHeadersDrawUnderTheReveal`: with `_settingsOpen` and `_advancedSettingsRevealed` on and the parent foldout open, assert the hook saw `Transparent` and `Cutout`. Run the filter and observe the failure. Record the failure.
- [ ] **9.2** Add the four cutout `GUIContent` fields with the identical labels and the exact tooltip texts from the spec's Inspector section.
- [ ] **9.3** Append the four cutout contents to `RowContents` after `ActiveSamplingProofRowContent`, and update the array's doc comment to name which rows each submenu draws.
- [ ] **9.4** Parameterize `DrawMipCapPopup` and `DrawMinTextureSizePopup` with `(string propertyPath, GUIContent content)` and update the two transparent call sites to pass the existing paths and contents. Behavior is unchanged for the transparent rows.
- [ ] **9.5** Add `_transparentSettingsOpen` and `_cutoutSettingsOpen`, both initialized true. Add the two submenu draw methods: the foldout with `EditorStyles.foldoutHeader`, the test hook invocation before the early return, then the rows. `DrawTransparentSubmenu` draws the seven existing rows through the existing methods with their existing arguments. `DrawCutoutSubmenu` draws the popups with the cutout paths and contents and the two sliders through `PercentSlider`.
- [ ] **9.6** Reorder `DrawAlphaSeparatorSettings` body: `DrawAcceptanceControls()`, then `DrawTransparentSubmenu()`, then `DrawCutoutSubmenu()`, keeping the label-width measurement and the final `ApplyModifiedProperties`.
- [ ] **9.7** Add the out-of-band warning test for one cutout popup, mirroring the transparent popup test. Update the preset-row doc comment at `AmuseAvatarOptimizerEditor.cs:434` from `all twelve fields` to `all sixteen fields`, because Task 8 grew the pressed state. Re-run the RED test and observe green.
- [ ] **9.8** Refresh Unity, run the `AmuseAvatarOptimizerEditorTests` filter. Expect green, nonzero counts. Record counts.
- [ ] **9.9** Commit: `git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs && git commit -m "feat: group alpha separator settings into transparent and cutout submenus"`

## Task 10: README updates

**Files:**
- Modify: `README.md`

- [ ] **10.1** Apply the three exact replacements from the spec's Documentation section: the navigation sentence, the stale toggles sentence, and the migration paragraph. Then add one sentence at the end of the proof modes section, exact text: `A brand-new component's first cutout edit in the session that adds it is replaced once by the transparent values. Every later edit sticks.` Change nothing else in the file.
- [ ] **10.2** Verify the file contains no private identifiers, no absolute paths, no ports, and no unqualified `today`.
- [ ] **10.3** Commit: `git add README.md && git commit -m "docs: document the transparent and cutout submenus and the version 4 migration"`

## Task 11: Full validation

- [ ] **11.1** Refresh Unity and wait for readiness. Confirm zero compile errors in the console.
- [ ] **11.2** Run the full `Alrauna.Amuse.Tests.Editor` assembly. Expect green with a nonzero count. Record the observed pass and fail counts and the duration.
- [ ] **11.3** Run the full `Alrauna.Amuse.Research.Tests.Editor` assembly. Expect green with a nonzero count. Record counts.
- [ ] **11.4** Re-run every falsifier test by filter and record each observed failure mode against its named wrong implementation is not possible post-hoc; instead confirm each falsifier test is present and green, and that the task reports recorded their original RED observations.
- [ ] **11.5** Report the observed counts, the characterization runs, and any deviation from this plan in the completion summary.
