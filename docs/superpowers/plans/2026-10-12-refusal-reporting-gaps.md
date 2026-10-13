# Refusal Reporting Gaps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the six silent refusal-reporting paths (G1-G6) and the reporting test holes found in the 2026-10-12 investigation, with report-only changes plus one additive Analysis carrier.

**Architecture:** All reporting flows through `Build/AmuseReports.cs` (sole `ErrorReport.ReportError` consumer, all `ErrorSeverity.Information`) with strings in `Build/AmuseReportStrings.cs`. New emissions reuse existing report methods where one fits; G1 adds one new key family and one new report method. One additive field on `SlotResolutionResult` carries the raw per-material resolutions for offender attribution.

**Tech Stack:** C# against Unity 2022.3, one editor-only assembly (`Alrauna.Amuse.Editor`), NUnit EditMode tests via Unity Test Runner, `ErrorReport.CaptureErrors` seam for report capture.

**Spec:** `docs/superpowers/specs/2026-10-12-refusal-reporting-gaps-design.md` (binding authority). Investigation: `docs/superpowers/investigations/2026-10-12-refusal-reporting-gaps-investigation.md`.

## Global Constraints

- Branch: `fix/refusal-reporting-gaps` (already checked out, base `2e9bee9`). Task-scoped commits with explicit paths only. NEVER push, open PRs, or touch other branches.
- Unity MCP safety before ANY Unity MCP call: enumerate instances read-only, check `Application.dataPath` equals this repo's `Assets` folder exactly, pin the dev editor instance. A case-only match is a stop.
- Tests: EditMode, assembly `Alrauna.Amuse.Tests.Editor`. After creating files: refresh Unity, wait for compile, check console clean, then run. A filtered run reporting 0 tests is a failure. Record observed counts.
- RED discipline: every behavior-change task observes its new test failing on current code first, for the named wrong implementation. Characterization tests (Task 4) pin current behavior and say so in a comment.
- Never weaken or delete a valid test. The only deletions allowed are the `ConsentDeclined` flag references in Task 7, with replacement coverage as specified there.
- All new report strings: short active sentences, one idea per sentence, no semicolons, no contractions.
- Every new report entry uses `ErrorSeverity.Information` and a context object (renderer, or avatar root for avatar-scope).
- No absolute paths, no machine names, no private identifiers in any file, comment, or commit message.
- Line numbers below are from base `2e9bee9` and drift. Symbol names are the anchors.
- Test naming: behavior sentences, one class per production type, folders mirror production.

---

### Task 1: Restore-mismatch detail in the swap-in report (G3, spec D3)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockSwapIn.cs` (`RecordRefusal` ~540-552, call site ~226-243, `ClonePersistRestoreVerify` ~250-310)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs`

**Interfaces:**
- Consumes: `AmuseReports.SlotSeparationRefusal(Renderer, int slotIndex, AlphaSeparationSlotRefusal cause, string rendererName, Material offendingMaterial, provenMapping, string detail)` — the `detail` argument already exists (`Build/AmuseReports.cs:247-287`).
- Produces: `RecordRefusal(...)` gains a `string detail` parameter threaded to `SlotSeparationRefusal`. Nothing else changes signature.

- [ ] **Step 1: Read the current shape**

Read `TransientUnlockSwapIn.cs:220-320` and `:530-560`. Confirm: `ClonePersistRestoreVerify` computes `mismatchReason` via `RestoreMismatchReason` (~295) and discards it except as a null gate; the refusal call site (~240) passes only cause and renderer/slot; `RecordRefusal` calls `SlotSeparationRefusal` without `detail`.

- [ ] **Step 2: Write the RED test**

In `TransientUnlockTransformationTests.cs`, extend the existing scenario `AMixedClosedSetDegradesPerSlot`, which fails the restore for real via an unresolvable recorded GUID and asserts `SlotRefusalCount(TransientUnlockRestoreMismatch) == 1`. After the swap-in execute, capture reports and assert the rendered `amuse.slotSeparation.TransientUnlockRestoreMismatch` entry contains the mismatch fact text for that shape: the recorded-GUID mismatch fact from `RestoreMismatchReason`. Use `ErrorReport.CaptureErrors` around the execute, mirroring `AmusePlatformFinishPluginTests.AnisotropyDifferencesRefusalReportsToNdmfConsole` (`Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:3204-3296`). Mark the test `TransientUnlockRestoreMismatchEntryCarriesTheMismatchFact`.

- [ ] **Step 3: Run the test, observe RED**

Refresh Unity, wait for compile, console clean. Run the filter `TransientUnlockRestoreMismatchEntryCarriesTheMismatchFact`. Expected: FAIL — the entry renders the bare cause name, not the mismatch fact. Record the observed failure output.

- [ ] **Step 4: Thread the detail**

In `RecordRefusal`, add `string detail = null` as the last parameter and pass it through to `SlotSeparationRefusal`'s `detail:` argument. At the refusal call site (~240), pass the `mismatchReason` string that `ClonePersistRestoreVerify` already returned. No other call sites change (they pass nothing and keep today's output).

- [ ] **Step 5: Run the test, observe GREEN**

Same filter. Expected: PASS. Record observed count.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/TransientUnlockSwapIn.cs Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs
git commit -m "fix: carry the restore mismatch fact into the swap-in report"
```

---

### Task 2: Close-time avatar refusal entry (G4, spec D5)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs` (graph enumeration ~109-115)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs`

**Interfaces:**
- Consumes: `AmuseReports.AvatarRefusal(GameObject avatarRoot, AvatarAnimationRefusal cause)` (`Build/AmuseReports.cs:504-520`, throws on None).
- Produces: no signature changes. New emission only.

- [ ] **Step 1: Write the RED test**

In `TransientUnlockTransformationTests.cs`, take an existing close-pass test that runs with one open pair (the file drives `TransientUnlockWindowClose.Execute(context)` directly, seeding precedent at :919 with `.AnimatorBindings = null`). Add a test: open a pair via swap-in, then seed `state.AnimatorBindings = null` so the close pass hits its null-bindings sentinel, run the close pass under `ErrorReport.CaptureErrors`, and assert exactly one `amuse.avatar.*` entry (TitleKey starts `amuse.avatar.`) and that the pair was retained. Name: `CloseTimeGraphRefusalReportsOneAvatarEntry`.

- [ ] **Step 2: Run the test, observe RED**

Expected: FAIL — zero `amuse.avatar.*` entries; only `amuse.slotSeparation.TransientUnlockCloneRetained` entries. Record output.

- [ ] **Step 3: Emit the entry**

In `TransientUnlockWindowClose.Execute`, immediately after the graph enumeration block (~115), add:

```csharp
// The close-time enumeration is this build's first and only avatar
// refusal surface: a barrier avatar refusal returns before the swap-in
// opens pairs, and this pass exits when no pairs exist. Reporting here
// can therefore never duplicate the barrier's report.
if (graph.Refusal != AvatarAnimationRefusal.None)
{
    AmuseReports.AvatarRefusal(context.AvatarRootObject, graph.Refusal);
}
```

- [ ] **Step 4: Run the test, observe GREEN**

Expected: PASS. Record count.

- [ ] **Step 5: Falsifier 5**

Add `--- Falsifier 5: reporting the avatar refusal in both the barrier and the close pass ---` as a test that runs a barrier avatar-refusal build and asserts the close pass emits zero avatar entries (it never runs with open pairs). Include a no-op guard so later changes cannot silence it (assert the barrier entry count is 1 as a precondition).

- [ ] **Step 6: Run both new tests, then commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs
git commit -m "fix: name the close-time graph refusal in the ndmf console"
```

---

### Task 3: Per-material conversion refusals in partial multi-material slots (G5, spec D4)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs` (conversion loop ~414-531)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: `AmuseReports.SlotSeparationRefusal` (unchanged signature).
- Produces: a private per-material refusal record list replacing the `lastConversionRefusal` / `lastRefusedMaterial` / `lastConversionRefusedDetail` locals (~416-423). All-fail path keeps its single slot-level entry, now built from the LAST collected record, matching today's `last*` semantics.

- [ ] **Step 1: Write the RED test**

In `AlphaSeparationPreparationTests.cs`, build the RED fixture from the existing two-material lever `SwappedMaterialWithUnconvertedSiblingMapsToIdentityAndPrepares` (~817-905): a one-slot swap curve admitting two materials where the verified Poiyomi material converts and the unsupported family refuses. Run the real barrier under `ErrorReport.CaptureErrors` (the `CaptureErrors(() => RunBarrier(root))` pattern is established in the file). Assert exactly one `amuse.slotSeparation.*` entry whose text names the refusing material, and assert the slot still prepared (the converting material's candidate survived). Name: `PartialConversionRefusalNamesTheRefusingMaterial`.

- [ ] **Step 2: Run the test, observe RED**

Expected: FAIL — zero entries. Record output.

- [ ] **Step 3: Collect records, emit post-loop**

Replace the three `last*` locals with a local list of `(AlphaSeparationSlotRefusal cause, Material material, string detail)` collected at the refusal site (~480-491). Post-loop, keep the all-fail branch exactly as today (`isMultiMaterialSlot && unconvertedCount >= count` fires the slot refusal from the LAST collected record, matching today's `last*` overwrite semantics and the code comment naming the last conversion failure). Add the partial branch:

```csharp
// A slot that kept converting emits one entry per refused material,
// so the console names every conversion that did not happen. The slot
// is not refused, so this writes no census record.
else if (isMultiMaterialSlot && collectedRefusals.Count > 0)
{
    foreach (var refusal in collectedRefusals)
    {
        AmuseReports.SlotSeparationRefusal(
            target.Renderer, slotIndex, refusal.cause,
            offendingMaterial: refusal.material,
            detail: refusal.detail);
    }
}
```

Mirror the all-fail branch's exact named-argument shape (`target.Renderer`, `offendingMaterial:`, `detail:`; no renderer name, no proven mapping, matching `AlphaSeparationPreparation.cs:522-531`). The branch order guarantees mutual exclusion: the all-fail branch returns or flags before the partial branch can run.

- [ ] **Step 4: Run the test, observe GREEN**

Expected: PASS. Record count.

- [ ] **Step 5: Falsifier 4**

Add `--- Falsifier 4: the all-fail case double-reporting per material ---`: extend the existing all-fail coverage with a two-refusing-material slot asserting the all-fail entry names the LAST refusing material (pinning today's `last*` semantics) and that the per-material partial branch produced zero entries in the all-fail case. Include a no-op guard so later changes cannot silence it.

- [ ] **Step 6: Run the focused conversion tests, then commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs
git commit -m "fix: name conversion refusals in partially converted slots"
```

---

### Task 4: Slot-less pair retention characterization and contract (G6, spec D6)

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs` (class doc ~28-58 only)

**Interfaces:**
- Produces: no code changes to the close pass. Doc-only contract correction plus a permanent characterization test.

- [ ] **Step 1: Write the characterization test**

Add `SlotLessPairDispositionIsPinned`: drive the close pass with one open pair whose `Slots` list is empty (open the pair via `OpenPair`, perform no successful slot swaps — the swap-in's `SwapSlot` false path is the natural producer; construct the state directly if the test seams allow). Run on both paths. On the success path, assert zero entries total and the pair destroyed. On the retention path (seed `state.AnimatorBindings = null` per Task 2's lever), assert exactly one `amuse.avatar.*` entry (Task 2's close-time emission) and zero `amuse.slotSeparation.*` retention entries, and observe the pair's disposition. Comment the test: `// Characterization: pins today's slot-less pair disposition. Spec D6 decided doc-only.`

- [ ] **Step 2: Run, observe, record**

This is a characterization test: it should pass on current code. If it FAILS, the disposition differs from the spec's assumption — STOP, record the observed dispositions in the report, and continue with the doc wording that matches the observation (the contract correction must describe reality).

- [ ] **Step 3: Correct the class doc**

In the `TransientUnlockWindowClose` class doc, replace the never-passes-silently sentence with the precise contract: the promise covers pairs with recorded slots; a slot-less pair has no slot or renderer scope to report against, so its disposition is silent by contract; in the refusal-driven case the close-time avatar entry names the cause for the whole build. Keep the existing sentences about inversion and destruction untouched.

- [ ] **Step 4: Run the full transient-unlock test filter, then commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs
git commit -m "docs: pin the slot-less pair retention contract"
```

---

### Task 5: Resolution-refused diagnostic on resolved slots (G1, spec D1)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs` (`SlotResolutionResult` ~29-120, `ResolveSlot` ~260-360)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (emission between the closure loop ~811-830 and the capture gate ~834)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs` (new method + sentence builder)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` (new key family)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Produces: `SlotResolutionResult.MaterialResolutions` (`IReadOnlyList<AlphaResolution>`, raw pre-dedup list, parallel to `AdmittedMaterialIndices`); `AmuseReports.SlotResolutionRefused(Renderer, int slotIndex, string rendererName, CapturedAlphaMaterial offenderMaterial, AlphaResolutionFailure failure)`; `AmuseReports.ResolutionFailureSentence(AlphaResolutionFailure)`; `AmuseReportStrings.SlotResolutionKey(AlphaResolutionFailure)`; keys `amuse.slotResolution.UnsupportedMultiplier|.MissingTextureEvidence|.UnsupportedUvMapping`.
- Consumes: `AlphaResolution.IsResolved` / `.Failure` (`Analysis/AlphaSemanticsResolver.cs:116-117`).

- [ ] **Step 1: Add the carrier field**

In `SlotResolutionResult`: add `internal IReadOnlyList<AlphaResolution> MaterialResolutions { get; }`, set from a new `Resolved(IReadOnlyList<AlphaResolution> resolutions, IReadOnlyList<AlphaResolution> materialResolutions)` overload; keep the existing single-argument `Resolved` delegating with `materialResolutions: resolutions` so existing callers compile. In `ResolveSlot`, pass its raw per-material list as `materialResolutions`. In `AmusePlatformFinishPlugin.ResolveRuntimeStates` (~1417-1419), pass `resolved.MaterialResolutions` through so the stored result keeps the raw list alongside the deduped one. `DistinctResolutions` stays exactly where it is. Classification consumers untouched.

- [ ] **Step 2: String table and builders**

In `AmuseReportStrings.cs`, add `SlotResolutionKey`:

```csharp
internal static string SlotResolutionKey(AlphaResolutionFailure failure)
{
    // SemanticsUnknown promotes to a slot refusal at ResolveSlot and
    // never reaches this path. None never names a fact.
    if (failure == AlphaResolutionFailure.None ||
        failure == AlphaResolutionFailure.SemanticsUnknown)
    {
        throw new InvalidOperationException(
            "Slot resolution reporting has no key for " + failure + ".");
    }
    return Prefix + "slotResolution." + failure;
}
```

Add three entries per key with the pinned argument contract: title `{0}` slot index; description `{0}` slot index, `{1}` renderer name, `{2}` material description, `{3}` failure sentence; hint names the material setup. Titles: "AMUSE could not prove alpha on slot {0}." Descriptions per failure, one sentence per idea, e.g. UnsupportedMultiplier: "The alpha of material {2} on slot {0} of renderer {1} multiplies a sampled alpha by a value above one." plus one sentence naming the consequence (no proof, triangles keep the original material). In `AmuseReports.cs`, add `ResolutionFailureSentence` with the same two-value throw guard and one plain sentence per failure value; add `SlotResolutionRefused` modeled on `TextureCaptureRefusal` (~343-367): renderer context object, `ErrorSeverity.Information`, args `{0}` slotIndex, `{1}` rendererName, `{2}` `MaterialDescription(offenderMaterial)`, `{3}` sentence.

- [ ] **Step 3: Extend the completeness tests**

In `AmuseReportStringsTests.cs`, add a loop over `AlphaResolutionFailure` values that skips `None` and `SemanticsUnknown` and asserts the key builder plus a non-empty title, description, and hint for every other value. In `AmuseReportsSlotTests.cs`, add per-value helper tests for `SlotResolutionRefused` (key, slot index, renderer name, material description) and the throw tests for `None` and `SemanticsUnknown` on both builders.

- [ ] **Step 4: RED fixture and test**

In `AmusePlatformFinishPluginTests.cs`, build on the verified Poiyomi fixture base: take an existing verified material fixture and set its `_Color.a` above one (review confirmed `PoiyomiMaterialSemantics` composes `TextureTimesConstant` from that channel unclamped, and the resolver refuses an above-one multiplier before every other gate). Run the real pass under `ErrorReport.CaptureErrors`. Assert: exactly one `amuse.slotResolution.UnsupportedMultiplier` entry; zero `amuse.slotAnalysis.Refusal` entries for that slot; the summary line appears with the same analyzed-renderer count as the unmodified fixture baseline (assert the count value, not just presence). Name: `ResolutionRefusedSlotReportsOneDiagnosticEntry`.

Add the two falsifiers:
- `--- Falsifier 1: reading the deduped Resolutions list ---`: two admitted materials in one slot, both with above-one multipliers but distinct colors. Assert two entries with two distinct material names. A wrong implementation reading `Resolutions` (deduped) emits one.
- `--- Falsifier 2: indexing AdmittedMaterials directly ---`: construct admission with non-contiguous indices (the swap-binding fixture path admits arbitrary indices). Assert the entry names the material at `AdmittedMaterialIndices[i]`, not `AdmittedMaterials[i]`.

- [ ] **Step 5: Run, observe RED**

Expected: FAIL — zero `amuse.slotResolution.*` entries (and the falsifiers fail on missing/misnamed entries). Record output.

- [ ] **Step 6: Emission loop**

In `AmusePlatformFinishPlugin.Execute`, between the out-of-range closure loop and the capture gate, add:

```csharp
// A resolved slot can still carry refused material resolutions. The
// slot stays resolved and the triangles classify Unknown; this entry
// names the lost proof reason. Diagnostic only: no census record.
if (resolved.Refusal == RendererAnalysisRefusal.None)
{
    for (var slotIndex = 0; slotIndex < resolved.SlotResults.Length; slotIndex++)
    {
        var slotResult = resolved.SlotResults[slotIndex];
        if (!slotResult.IsResolved)
        {
            continue; // refused slots report through the fold loop above.
        }
        for (var i = 0; i < slotResult.MaterialResolutions.Count; i++)
        {
            var resolution = slotResult.MaterialResolutions[i];
            if (resolution.IsResolved)
            {
                continue;
            }
            var offender = evidence.AdmittedMaterials[
                slots[slotIndex].AdmittedMaterialIndices[i]];
            AmuseReports.SlotResolutionRefused(
                renderer, slotIndex, renderer.gameObject.name, offender,
                resolution.Failure);
        }
    }
}
```

`slots` is the `MaterialSlotsFor` result already in scope above the fold loop, and `SlotResults[i]` corresponds to `slots[i]` by the loop's own documented invariant. Keep the existing style for the renderer name (the surrounding code passes `renderer.gameObject.name` inline). The indirection through `AdmittedMaterialIndices` is load-bearing; carry a comment saying so.

- [ ] **Step 7: Run the new tests plus the full plugin test filter, observe GREEN**

Expected: all PASS, existing counts unchanged. Record counts.

- [ ] **Step 8: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "feat: name a refused material resolution on the resolved slot"
```

---

### Task 6: Per-slot evidence facts for renderer-refused renderers (G2, spec D2)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (renderer-refused path, the `continue` after the renderer refusal report ~903-917)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: existing `AmuseReports.TextureCaptureRefusal`, `AmuseReports.TextureAlphaMissing`, `AmuseReports.SlotAnalysisRefusal` (all unchanged); per-slot evidence `CaptureRefusals`, `NoAlphaChannelProperties`, and captured material `LockedIdentityRefusal` (`Host/CapturedAnimationEvidence.cs:164`, `Host/UnityAnimationEvidenceCapture.cs:650-668`).
- Produces: no signature changes. New emissions only, on the renderer-refused path.

- [ ] **Step 1: RED tests**

Three end-to-end tests in `AmusePlatformFinishPluginTests.cs`, all under `ErrorReport.CaptureErrors`, all built on the established additive-layer lever (`RuntimeStateIntegration_AdditiveLayerWithRelevantPropertyRefuses`, :4054):

1. `RendererRefusalCarriesTheRefusedSlotsCaptureFacts`: additive-layer fixture plus one slot whose texture capture refuses (an unsupported-format or unavailable-capture lever from the existing capture-refusal tests). Assert the renderer refusal entry plus exactly one `amuse.texture.*` entry naming the refusing slot, where current code emits none.
2. `RendererRefusalNamesTheLockedIdentityFact`: additive-layer fixture plus a locked material with an unattested original shader on that renderer. Assert exactly one `amuse.slotAnalysis.Refusal` entry whose text names the unattested-original fact.
3. `RendererRefusalCarriesTheNoAlphaDiagnostics`: additive-layer fixture plus a no-alpha-channel texture property. Assert the `amuse.texture.NoAlphaChannel` entry appears.

- [ ] **Step 2: Run, observe RED**

Expected: all three FAIL with zero target entries. Record output.

- [ ] **Step 3: Emission block**

On the renderer-refused path before the `continue`, add one block (read the surrounding code first and match locals; `resolved` may carry empty `SlotResults` here, and `slots` — the `MaterialSlotsFor` result — is in scope):

```csharp
// A renderer refusal suppresses the per-slot loops below, so the
// slots' captured facts would die. Report exactly the facts that did
// not surface: with empty slot results, every slot; otherwise only
// resolved slots, because refused slots folded their facts already.
// Report-only: no census record, and the host-structural path never
// reaches this point with captured evidence.
for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
{
    var foldedAlready =
        resolved.SlotResults.Length > 0 &&
        !resolved.SlotResults[slotIndex].IsResolved;
    if (foldedAlready)
    {
        continue;
    }
    var slotEvidence = slots[slotIndex];
    foreach (var captureRefusal in slotEvidence.CaptureRefusals)
    {
        AmuseReports.TextureCaptureRefusal(
            renderer, slotIndex, captureRefusal);
    }
    foreach (var noAlphaProperty in slotEvidence.NoAlphaChannelProperties)
    {
        AmuseReports.TextureAlphaMissing(
            renderer, slotIndex, noAlphaProperty);
    }
    // Locked identity: emit at the first carrier in material order.
    foreach (var materialIndex in slotEvidence.AdmittedMaterialIndices)
    {
        var material = evidence.AdmittedMaterials[materialIndex];
        if (material.LockedIdentityRefusal != RendererAnalysisRefusal.None)
        {
            AmuseReports.SlotAnalysisRefusal(
                renderer, slotIndex, material.LockedIdentityRefusal,
                renderer.gameObject.name, material,
                capturedSemantics: null, captureRefusals: null,
                closureWaySentence: null);
            break;
        }
    }
}
```

Mirror the standalone loops' exact argument shapes (~853-879) and the fold loop's `SlotAnalysisRefusal` shape (~793-806) — read both first and adapt parameter names to the real signatures. No census calls. Guard every fact emission against double-reporting by the `foldedAlready` rule alone — no extra dedup.

- [ ] **Step 4: Run the three tests, observe GREEN**

Expected: PASS. Record counts.

- [ ] **Step 5: Falsifier 3**

Add `--- Falsifier 3: emitting on a resolving renderer ---`: run the same fixtures through a fully resolving renderer and assert the entry sets are exactly today's (fold entries for refused slots, standalone texture/no-alpha entries for resolved slots, and nothing new). A wrong implementation emitting unconditionally fails this.

- [ ] **Step 6: Run the full plugin filter, then commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "fix: report slot evidence facts for renderer-refused renderers"
```

---

### Task 7: Hygiene batch — dead flag, doc sentence, None guards, missing tests (spec D7)

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` (flag declaration :21, write :566)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs` (`AmuseBuildPath.Unknown` doc ~15-20)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs` (`RendererRefusal` ~396-415, `TextureCaptureRefusal` ~343-367)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Build/VersionConsentTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsSlotTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportsRendererTests.cs`, `Packages/com.alrauna.amuse/Tests/Editor/Build/MergedConsumptionFixture.cs`

**Interfaces:**
- Produces: `RendererRefusal` and `TextureCaptureRefusal` throw `InvalidOperationException` on their None member; `AmusePlatformFinishState.ConsentDeclined` removed; `AmuseBuildPath.Unknown` doc states its test-seam role.

- [ ] **Step 1: None guards, characterization-first**

The guards change throw behavior; current code forms `amuse.renderer.None` / `amuse.texture.None`. Add the same None throw the other four methods have, each with a comment naming the key it prevents. Tests: one throw test per guarded method — all SIX (`SlotAnalysisRefusal`, `SlotSeparationRefusal`, `LifecycleRefusal`, `AvatarRefusal`, `RendererRefusal`, `TextureCaptureRefusal`), since only `LifecycleRefusal` has one today (behavior sentences, e.g. `RendererRefusalThrowsOnNoneCause`). Run the two new-guard tests first without the guards to observe RED, then add guards, observe GREEN; the four backfill throw tests are characterization on already-guarded methods and are labeled as such.

- [ ] **Step 2: Missing reporting tests**

Characterization tests, marked as such in comments: `SlotSeparationDivergence` and `SlotSeparationPremultiplyNormalization` helper tests in `AmuseReportsSlotTests.cs` (key, slot index, renderer name); `amuse.consent.Subject` per-subject count and content by capturing the consent-declined path in `AmusePlatformFinishPluginTests` (extend the existing `DeclinedConsentReportsTheDeclinedEntryExactlyOnce` shape at :360-398: assert one `amuse.consent.Subject` entry per subject with the subject text).

- [ ] **Step 3: Delete the ConsentDeclined flag**

Remove the declaration (`AmusePlatformFinishPlugin.cs:21`), the write (`:566`), and the stale doc claim. Update the seven test references: four assertions in `AmusePlatformFinishPluginTests.cs` (:350, :423, :598, :702), two in `VersionConsentTests.cs` (:304, :382), one string use in `MergedConsumptionFixture.cs` (:674). Replace flag assertions with the equivalent observable: the decline report's exactly-once entry (already tested) or renderer-count assertions that already exist. Delete nothing else.

- [ ] **Step 4: AmuseBuildPath.Unknown doc sentence**

Add one sentence to the value's doc comment: production code never constructs it; tests construct it to drive the `UnsupportedBuildPath` refusal path.

- [ ] **Step 5: Run the full test assembly, record counts**

Run the full `Alrauna.Amuse.Tests.Editor` assembly. Expected: all PASS. Record the observed counts (total, passed, skipped).

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs Packages/com.alrauna.amuse/Tests/Editor/Build/
git commit -m "fix: reporting guard symmetry, consent flag removal, and coverage holes"
```

---

## Execution notes

- Task order 1→7 is dependency-safe: 1-3 are independent small fixes; Task 4 depends on Task 2's landed close-time avatar entry for its retention-path expectations; Task 5 lands the Analysis carrier and new vocabulary; Task 6 depends on nothing from 5 but touches the same file region, so it follows; Task 7 sweeps the hygiene.
- Every task: refresh Unity after file creation, verify compile via console, run the named filter, record observed counts in the implementer report.
- If any RED test cannot be made to fail for the named wrong implementation, STOP and report: the plan's premise for that task needs a ruling, not a workaround.
