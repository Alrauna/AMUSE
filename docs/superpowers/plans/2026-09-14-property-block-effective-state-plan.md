# Property block effective state implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Privacy note: This plan names public synthetic fixtures and vendor packages only. It contains no private assets, names, paths, or identifiers.

Date: 2026-09-14.
Base branch: `feat/material-property-block-support` at `8204385` (from `main` at `5bf3c48`).
Spec: `docs/superpowers/specs/2026-09-14-material-property-block-effective-state-design.md`.

**Goal:** Renderers that carry Unity property blocks become analyzable. AMUSE proves alpha against each slot's effective material state: per-material-index block, then renderer-wide block, then serialized values, with the animation closure on top.

**Architecture:** A new Host-layer type materializes per-slot effective materials by copying block entries onto clones, with the copy schema taken from each shader's own property list. The PlatformFinish capture path and the Apply revalidation path consume materialized state. The presence-only refusal and its report strings are removed. Classification, admission, and the canonical opaque recipe do not change.

**Tech Stack:** Unity 2022.3.22f1 editor APIs, NDMF 1.14.4, NUnit through the Unity Test Framework, EditMode only.

## Global Constraints

- Compilation happens inside Unity. There is no dotnet build and no CLI test runner. Refresh the editor after new files, then run EditMode filters.
- A filtered run that reports 0 tests is a failure. Record observed counts.
- File name equals type name. One type per file. Production types are `internal`. Namespaces mirror folders: `Alrauna.Amuse.Editor.Host` for Host files.
- Vendor shaders are never installed. Tests use the stand-in shaders under `Hidden/Alrauna/AmuseTests/*` through the existing fixture helpers.
- Fail closed. Never weaken a valid test. An assertion change needs a written justification. This plan's justified assertion changes are listed in Task 3.
- Source assets are never mutated. Materialization clones are transient capture inputs, never written back to a renderer or an asset.
- The validation Clone lifecycle rule: every clone is destroyed in the same scope that created it, on every exit path.

---

### Task 1: Block schema probe and materialization type

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/EffectiveMaterialMaterialization.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/EffectiveMaterialMaterializationTests.cs`

**Interfaces:**
- Consumes: Unity APIs `Renderer.GetPropertyBlock`, `Renderer.GetPropertyBlock(block, index)`, `MaterialPropertyBlock.HasProperty`, `Shader.GetPropertyCount/GetPropertyName/GetPropertyType`, `Material.Set*`.
- Produces: `internal static class EffectiveMaterialMaterialization` with
  `internal static Material[] Materialize(Renderer renderer, Material[] slotMaterials, out List<Material> createdClones)`.
  Returns the `slotMaterials` array reference unchanged when no effective difference exists. Otherwise returns one material per slot: the original reference for unchanged slots, a block-applied clone for overridden slots. `createdClones` lists every clone for scoped disposal.

- [ ] **Step 1: Write the failing tests**

Create `EffectiveMaterialMaterializationTests.cs` in `Tests/Editor/Host/`. Fixture: a two-slot renderer on the stand-in shader from the existing Host fixtures, two materials, one `MaterialPropertyBlock` per case. Seven cases:

```csharp
[Test] public void NoBlockReturnsTheSameArrayWithNoClones()
{
    var renderer = /* existing two-slot fixture helper */;
    var slots = renderer.sharedMaterials;
    var result = EffectiveMaterialMaterialization.Materialize(
        renderer, slots, out var clones);
    Assert.That(result, Is.SameAs(slots));
    Assert.That(clones, Is.Empty);
}

[Test] public void RendererWideFloatOverrideLandsOnEverySlotClone()
{
    // block.SetFloat("_AlphaForceOpaque", 1f) renderer-wide.
    // Assert: result[i] != slots[i] for both slots, originals unchanged
    // (slots[i].GetFloat still 0f), clones report 1f.
}

[Test] public void PerIndexBlockOverridesRendererWide()
{
    // Renderer-wide sets "_Cutoff" 0.5f; per-index 0 sets "_Cutoff" 0.25f.
    // Assert: clone[0] reports 0.25f, clone[1] reports 0.5f.
}

[Test] public void IntAndColorAndVectorAndTextureEntriesCopy()
{
    // Set "_MainAlphaMaskMode" int 1, "_Color", "_MainTex_ST", "_MainTex"
    // renderer-wide. Assert each on the clone, including texture identity
    // (ReferenceEquals to the override texture).
}

[Test] public void UndeclaredKeysChangeNothing()
{
    // block.SetFloat("AMUSE_Undeclared_Key", 3f) renderer-wide.
    // Assert: result values equal originals on every declared probed
    // property, and no clone was needed (clones empty).
}

[Test] public void OriginalsAreNeverMutated()
{
    // After materializing an overridden renderer, assert every original
    // material's probed properties still equal their pre-call values.
}

[Test] public void MatrixEntriesCopyWhenDeclared()
{
    // Only if the stand-in shader declares a matrix property; otherwise
    // assert a matrix block entry is ignored like an undeclared key and
    // record that limitation in the test name.
}
```

- [ ] **Step 2: Run the tests and observe failure**

Refresh the editor. Run the EditMode filter `Alrauna.Amuse.Tests.Editor.Host.EffectiveMaterialMaterializationTests`.
Expected: compile failure, because `EffectiveMaterialMaterialization` does not exist. Record the observation as RED evidence.

- [ ] **Step 3: Implement the type**

Implementation shape:

```csharp
namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>Why: a block is part of the renderer's effective state.
    /// Capture must prove the values the renderer actually shows.</summary>
    internal static class EffectiveMaterialMaterialization
    {
        internal static Material[] Materialize(
            Renderer renderer,
            Material[] slotMaterials,
            out List<Material> createdClones)
        {
            createdClones = new List<Material>();
            if (!renderer.HasPropertyBlock())
                return slotMaterials;

            // Renderer-wide block first, then per-index blocks over it.
            // For each slot: enumerate the slot material's shader
            // properties; for each declared property present in the
            // blocks, copy by ShaderPropertyType onto a clone of the
            // slot material (Material.Instantiate). Copy order:
            // renderer-wide, then per-index. Copy Float, Int, Color,
            // Vector, Texture, Matrix, and Buffer entries. If no
            // declared property was present in any block, return the
            // original array and keep createdClones empty.
        }
    }
}
```

Rules the implementation must follow:

- The schema comes only from the shader's declared properties. A block key that no shader property declares is never copied.
- Per-index blocks read through `GetPropertyBlock(block, index)` for every slot index `0..slotMaterials.Length-1`. A renderer-wide entry applies to all slots; a per-index entry for the same property replaces it on that slot.
- Originals are read-only inputs. The method never calls `Set*` on an original material.
- One clone per changed slot. Slots with no copied entry keep their original reference.
- Cache the schema per distinct `Shader` instance within one `Materialize` call with a local dictionary.

- [ ] **Step 4: Run the tests and observe pass**

Run the same filter. Expected: 7 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/EffectiveMaterialMaterialization.cs \
  Packages/com.alrauna.amuse/Editor/Host/EffectiveMaterialMaterialization.cs.meta \
  Packages/com.alrauna.amuse/Tests/Editor/Host/EffectiveMaterialMaterializationTests.cs
git commit -m "Add block materialization for effective slot materials"
```

---

### Task 2: Wire materialization into the PlatformFinish capture path

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:493-510`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/RendererAlphaAnalysisIntegrationTests.cs`

**Interfaces:**
- Consumes: `EffectiveMaterialMaterialization.Materialize` from Task 1; the two capture calls at `AmusePlatformFinishPlugin.cs:493-497` and `502-506` that pass `renderer.sharedMaterials`.
- Produces: the capture inputs become effective materials. The structural slot-count checks and the write path keep using the live `sharedMaterials`.

- [ ] **Step 1: Write the failing integration falsifier**

Add to `RendererAlphaAnalysisIntegrationTests.cs`, reusing that file's renderer and sampled-material helpers:

```csharp
[Test]
public void BlockForcedOpaqueOverrideAnalyzesThroughTheOverride()
{
    // Fixture: one-skinned-slot renderer with the sampled transparent
    // material (declared _AlphaForceOpaque 0). Then:
    var block = new MaterialPropertyBlock();
    block.SetFloat("_AlphaForceOpaque", 1f);
    renderer.SetPropertyBlock(block);

    // Run this file's existing capture-and-analyze helper.
    // Assert: refusal is None, opaque candidate triangle count is 1,
    // and the original material's _AlphaForceOpaque is still 0.
}
```

Name the plausible wrong implementation in the failure message: an implementation that classifies the serialized material instead of the materialized one proves the wrong state.

- [ ] **Step 2: Run the falsifier and observe failure**

Refresh, then run the filter `Alrauna.Amuse.Tests.Editor.Host.RendererAlphaAnalysisIntegrationTests.BlockForcedOpaqueOverrideAnalyzesThroughTheOverride`.
Expected: FAIL. Record the observed message. The renderer is refused before capture, so the refusal reads `MaterialPropertyOverridesPresent`. This RED carries into Task 3.

- [ ] **Step 3: Wire the capture calls**

At `AmusePlatformFinishPlugin.cs:493-510`, replace both `renderer.sharedMaterials` capture inputs:

```csharp
var effective = EffectiveMaterialMaterialization.Materialize(
    renderer, renderer.sharedMaterials, out var effectiveClones);
try
{
    // existing capture calls, with `effective` in place of
    // renderer.sharedMaterials
}
finally
{
    foreach (var clone in effectiveClones)
        UnityEngine.Object.DestroyImmediate(clone);
}
```

Keep `HostStructuralRefusalFor` and the slot-count logic on the live `renderer.sharedMaterials`. Keep `CaptureGeometry` on the captured material slots.

- [ ] **Step 4: Verify no regression in the capture neighborhood**

Refresh, then run the filter `Alrauna.Amuse.Tests.Editor.Host.RendererAlphaAnalysisIntegrationTests`.
Expected: all cases pass except the new falsifier, which still fails with `MaterialPropertyOverridesPresent` (the gate at `AmusePlatformFinishPlugin.cs:456` precedes capture). Record counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Host/RendererAlphaAnalysisIntegrationTests.cs
git commit -m "Capture effective slot materials in the finish pass"
```

---

### Task 3: Remove the presence refusal and rewrite its pins

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:21` (enum value), `:196-208` (type doc), `:444-476` (private structural check)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:30-39` (two strings)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs:120-162` (deliberate-block pins)
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/RendererRefusalCalibrationTests.cs:94-98` (calibration flip)

**Interfaces:**
- Consumes: nothing new.
- Produces: `RendererAnalysisRefusal` without `MaterialPropertyOverridesPresent`; `HostStructuralRefusalFor` never refuses on block presence.

- [ ] **Step 1: Sweep the references**

Run a repository search for `MaterialPropertyOverridesPresent` and `amuse.renderer.MaterialPropertyOverridesPresent`. Expected sites: the enum, the two report strings, `UnityRendererAlphaAnalysisTests.cs:133` and `:161`, and `RendererRefusalCalibrationTests.cs`. If any other site appears, extend this task before continuing.

- [ ] **Step 2: Remove the gate and the value**

- Delete the presence check and its comment at `UnityRendererAlphaAnalysis.cs:465-470`, leaving the unsupported-type and missing-mesh checks.
- Delete the enum value `MaterialPropertyOverridesPresent` from `RendererAnalysisRefusal`.
- Delete the two entries in `AmuseReportStrings.cs`.
- Rewrite the type doc at `UnityRendererAlphaAnalysis.cs:196-208`: the analysis reads the effective material state captured by the caller; block contents reach the proof through materialization, so presence alone is not a refusal.

- [ ] **Step 3: Rewrite the pins with justification**

In `UnityRendererAlphaAnalysisTests.cs`, replace the two deliberate-block refusal tests (lines 120-162) with acceptance pins:

```csharp
[Test] public void DeliberateBlockIsAcceptedAndStructuralChecksStay()
{
    // Fixture as before: renderer.SetPropertyBlock(colorOverrideBlock).
    // Assert: HostStructuralRefusalFor returns None, the supported-type
    // and missing-mesh refusals still fire for their own fixtures, and
    // a capture of the blocked renderer does not attach or clear the
    // block (block contents survive the call untouched).
}
```

Justification for the assertion change: the structural refusal contract changed by approved design on 2026-09-14. Presence of a block is no longer a refusal; the block is evidence. The per-material-index presence pin on the same file stays, because capture still needs per-index reads.

In `RendererRefusalCalibrationTests.cs`, flip the blocked-renderer case: the observation must succeed and record the overridden `_Cutoff` as the effective value, instead of asserting the refusal.

- [ ] **Step 4: Run and observe green**

Refresh. Run the filters `Alrauna.Amuse.Tests.Editor.Host` and the research collection fixture class.
Expected: the Task 2 falsifier now passes; the rewritten pins pass; the per-index presence pin passes; no other case regresses. Record counts.

- [ ] **Step 5: Commit**

```bash
git add -u
git commit -m "Accept property blocks as effective state in the structural layer"
```

---

### Task 4: Revalidate against materialized state at Apply

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:144` (validation read) and the enclosing renderer scope
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Consumes: `EffectiveMaterialMaterialization.Materialize` from Task 1; the validation read at `AlphaSeparationApply.cs:144` feeding `ValidateCandidateSlot` (`AlphaSeparationApply.cs:506`).
- Produces: Apply validation proves candidates against the renderer's current block state, never against barrier-time state. The write array keeps original material references for carried-through slots.

- [ ] **Step 1: Write the failing falsifier**

Add to `AlphaSeparationApplyTests.cs`, using the file's verified fixture and `ApplyTestPlatform`:

```csharp
[Test]
public void CandidatePreparedUnderABlockingProofDoesNotApplyAfterTheBlockIsRemoved()
{
    // Build the proven-opaque fixture; block.SetFloat("_AlphaForceOpaque", 1f).
    // Prepare (the plan survives; the candidate is valid under the block).
    // Then renderer.SetPropertyBlock(new MaterialPropertyBlock()) — the
    // override is gone.
    // Run Apply.
    // Assert: Apply refuses the renderer with the serialized-state slot
    // refusal, no curve edit lands, and renderer.sharedMaterials still
    // reference the original fixture materials.
}
```

- [ ] **Step 2: Run the falsifier and observe failure**

Refresh, run the filter `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests.CandidatePreparedUnderABlockingProofDoesNotApplyAfterTheBlockIsRemoved`.
Expected: FAIL. Record the observation. Today validation reads the raw `sharedMaterials`, which still match the prepared state, so the stale candidate applies.

- [ ] **Step 3: Materialize the validation read**

At `AlphaSeparationApply.cs:144`, replace the raw read:

```csharp
var live = EffectiveMaterialMaterialization.Materialize(
    renderer, renderer.sharedMaterials, out var liveClones);
try
{
    // existing validation and finalization for this renderer
}
finally
{
    foreach (var clone in liveClones)
        UnityEngine.Object.DestroyImmediate(clone);
}
```

Invariant for the finalization code: the new `sharedMaterials` array is built from the original live materials for carried-through slots. Validation clones never enter the write array. If the compiler or a test observes a clone flowing into `write.Materials`, stop; that is a plan defect.

- [ ] **Step 4: Run and observe green**

Refresh, run the full `AlphaSeparationApplyTests` filter.
Expected: the falsifier passes; every existing case passes. Record counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs
git commit -m "Validate candidates against materialized block state"
```

---

### Task 5: Re-strengthen the animated fixtures

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs` (helper at the fixture-helpers region, call in `RunBarrier`)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (helper before `DestroyCommittedClone`, calls in `ExecuteVerifiedPlatformFinish`, `RuntimeStateProductionEntry_PostClosureSlotRefusalKeepsTheValidSibling`, `RuntimeStateProductionEntry_EverySlotFailingRefusesTheRendererOnce`)

**Interfaces:**
- Consumes: production block handling from Tasks 2 through 4.
- Produces: the thirteen animated fixtures measure production behavior through the real commit-written block again.

- [ ] **Step 1: Delete the helpers and their calls**

Delete the `ClearCommitAnimationPropertyBlocks` helper definition and the `ClearCommitAnimationPropertyBlocks(root);` call in `AlphaSeparationPreparationTests.cs` (inside `RunBarrier`, after the retained-bindings line). Delete the same helper definition and the three calls in `AmusePlatformFinishPluginTests.cs`. Search the test tree for `ClearCommitAnimationPropertyBlocks`; the expected remainder after deletion is zero references.

- [ ] **Step 2: Run the thirteen and observe green without the helpers**

Refresh. Run the two filters `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationPreparationTests` and `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests`.
Expected: 0 failed. The thirteen named fixtures from the 2026-09-13 diagnosis must pass. Then run the same thirteen by full name a second time, immediately, in the same editor session. Expected: 0 failed again. Record both counts.

- [ ] **Step 3: Commit**

```bash
git add -u
git commit -m "Re-strengthen animated fixtures through real commit blocks"
```

---

### Task 6: Full validation and evidence

- [ ] **Step 1: Full product assembly**

Run the full EditMode assembly `Alrauna.Amuse.Tests.Editor`.
Expected: 0 failed. The two `DaoMergedConsumptionTests` cases report Inconclusive by design (the `AMUSE_DAO_INTEGRATION` guard). Record the observed passed and failed counts.

- [ ] **Step 2: Full research assembly**

Run the full EditMode assembly `Alrauna.Amuse.Research.Tests.Editor`.
Expected: 0 failed. Record counts.

- [ ] **Step 3: Hygiene**

Run `git diff --check` against `main`. Sweep the changed files for an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and every private asset name known to the session. Every hit is a defect; fix hits before reporting.

- [ ] **Step 4: Report**

Report per task: RED observation, GREEN observation, and the recorded counts from this task. State the one remaining manual step: the read-only Census Lab characterization on a private swap avatar, by role names only, never identifiers.

---

## Stop conditions

- The materialized clone cannot reproduce a block value the renderer shows, for any declared property type. Stop; that is a capture soundness defect, not an accepted limitation.
- Correctness requires touching `TriangleAlphaClassifier`, admission, or the canonical opaque recipe. Stop; this plan must compose with those layers, not change them.
- A clone escapes its scope and reaches `sharedMaterials` or an asset. Stop; the mutation boundary is violated.
- Stage 2 (per-property domains replacing scalar materialization) is out of scope here. Its gate is measured clone and readback overhead from this plan, collected during Task 6.
