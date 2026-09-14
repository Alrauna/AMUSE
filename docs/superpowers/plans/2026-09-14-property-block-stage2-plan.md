# Property block per-property domains implementation plan (Stage 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate materialization clone overhead for scalar, color, and vector property blocks by proving effective values through in-memory per-property domains in `Analysis`, while preserving texture materialization and fail-closed safety.

**Architecture:** `EffectiveMaterialMaterialization` clones slots only when declared texture properties or companion vectors (`_ST`, `_TexelSize`, `_HDR`) are present in property blocks. Scalar, color, and vector entries are extracted into immutable `BlockStateEntry` collections and passed to `AdmittedMaterialStates.ResolveSlot`. For unanimated properties, block values override serialized defaults in the captured evidence. For animated properties, the domain is the union of serialized, block, and curve values; non-singletons refuse with `AnimatedMaterialPropertyNotSingleton`.

**Tech Stack:** Unity 2022.3.22f1 editor APIs, NDMF 1.14.4, NUnit through Unity Test Framework (EditMode only).

**Spec:** `docs/superpowers/specs/2026-09-14-property-block-stage2-design.md`

## Global Constraints

- Compilation happens inside Unity. There is no dotnet build and no CLI test runner. Refresh the editor after new files, then run EditMode filters.
- A filtered run that reports 0 tests is a failure. Record observed counts.
- File name equals type name. One type per file. Production types are internal. Namespaces mirror folders.
- Vendor shaders are never installed. Tests use stand-in shaders under `Hidden/Alrauna/AmuseTests/*`.
- Fail closed. Never weaken a valid test. Justified test adjustments are documented in Tasks 5 and 6.
- Source assets are never mutated. Transient clones are destroyed in the same scope that created them.
- Privacy: never record private names, machine paths, ports, or instance hashes. All prose follows ASD-STE100.

---

### Task 1: Bypass material cloning for scalar-only blocks

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/EffectiveMaterialMaterialization.cs:60-78, 115-138, 327-340`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/EffectiveMaterialMaterializationTests.cs`

**Interfaces:**
- Consumes: `EffectiveMaterialMaterialization.TouchesSchema`, `ShaderPropertyType`.
- Produces: `EffectiveMaterialMaterialization.Materialize` and `MaterializeAdmitted` only allocate clones when a block touches a declared `ShaderPropertyType.Texture` or companion vector (`_ST`, `_TexelSize`, `_HDR`). Scalar, color, and independent vector overrides bypass cloning and return original material references with zero created clones.

- [ ] **Step 1: Write the failing tests**

Add two tests to `EffectiveMaterialMaterializationTests.cs`:

```csharp
[Test]
public void ScalarAndColorOverridesDoNotCreateClones()
{
    var block = new MaterialPropertyBlock();
    block.SetFloat("_Cutoff", 0.25f);
    block.SetFloat("_AlphaForceOpaque", 1f);
    block.SetColor("_Color", Color.red);
    renderer.SetPropertyBlock(block);

    var slots = renderer.sharedMaterials;
    var result = EffectiveMaterialMaterialization.Materialize(
        renderer, slots, out var clones);

    Assert.That(result, Is.SameAs(slots));
    Assert.That(clones, Is.Empty);
}

[Test]
public void TextureOverrideCreatesCloneWhileScalarOnlyDoesNot()
{
    var block = new MaterialPropertyBlock();
    block.SetFloat("_Cutoff", 0.25f);
    var tex = Texture2D.whiteTexture;
    block.SetTexture("_MainTex", tex);
    renderer.SetPropertyBlock(block);

    var slots = renderer.sharedMaterials;
    var result = EffectiveMaterialMaterialization.Materialize(
        renderer, slots, out var clones);

    Assert.That(clones.Count, Is.EqualTo(2));
    Assert.That(result[0].GetTexture("_MainTex"), Is.SameAs(tex));
}
```

- [ ] **Step 2: Run the test to verify failure**

Run EditMode filter `Alrauna.Amuse.Tests.Editor.Host.EffectiveMaterialMaterializationTests.ScalarAndColorOverridesDoNotCreateClones`.
Expected: FAIL because stage 1 clones slots whenever any declared property is touched.

- [ ] **Step 3: Implement texture-only cloning filter**

In `EffectiveMaterialMaterialization.cs`, refine `TouchesSchema` or add `TouchesTextureSchema`:

```csharp
private static bool TouchesTextureSchema(
    List<SchemaEntry> schema,
    MaterialPropertyBlock block)
{
    foreach (var entry in schema)
    {
        if (entry.Type != ShaderPropertyType.Texture &&
            !entry.Name.EndsWith("_ST", StringComparison.Ordinal) &&
            !entry.Name.EndsWith("_TexelSize", StringComparison.Ordinal) &&
            !entry.Name.EndsWith("_HDR", StringComparison.Ordinal))
        {
            continue;
        }

        if (block.HasProperty(entry.Name))
        {
            return true;
        }
    }

    return false;
}
```

Update `Materialize` and `MaterializeAdmitted` to check `TouchesTextureSchema` instead of `TouchesSchema` when deciding whether to instantiate a material clone. Keep `CaptureBlockState` untouched so all property entries continue to be snapshotted.

- [ ] **Step 4: Run tests to verify pass**

Run EditMode filter `Alrauna.Amuse.Tests.Editor.Host.EffectiveMaterialMaterializationTests`.
Expected: All tests pass. Existing float/color tests that asserted clone creation on `Materialize` are updated to assert on `CaptureBlockState` or texture overrides.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/EffectiveMaterialMaterialization.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Host/EffectiveMaterialMaterializationTests.cs
git commit -m "Bypass material cloning for scalar and color property blocks"
```

---

### Task 2: Pass per-slot block state into `AdmittedMaterialStates.ResolveSlot`

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:198-237`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:956-975`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Consumes: `IReadOnlyList<BlockStateEntry>`.
- Produces: `AdmittedMaterialStates.ResolveSlot` accepts `IReadOnlyList<BlockStateEntry> slotBlockEntries` and passes them to `TryAdmitDerivedEvidence`.

- [ ] **Step 1: Write the failing unit test**

In `AdmittedMaterialStatesTests.cs`, add a test asserting that `ResolveSlot` accepts slot block entries:

```csharp
[Test]
public void ResolveSlotAcceptsBlockEntriesWithoutThrowing()
{
    var slot = new CapturedMaterialSlotEvidence(0, new[] { 0 });
    var blockEntries = new List<BlockStateEntry>
    {
        new BlockStateEntry(0, "_Cutoff", ShaderPropertyType.Float, 0.25f, default, default, null),
    };
    // Call ResolveSlot with blockEntries overload
}
```

- [ ] **Step 2: Run test to verify failure**

Compile failure: `ResolveSlot` has no overload taking `IReadOnlyList<BlockStateEntry>`.

- [ ] **Step 3: Update `ResolveSlot` signature and caller**

In `AdmittedMaterialStates.cs`:
Add parameter `IReadOnlyList<BlockStateEntry> slotBlockEntries = null` to `ResolveSlot`.
Forward `slotBlockEntries` to `TryAdmitDerivedEvidence`.

In `AmusePlatformFinishPlugin.cs:956-975`:
Collect block entries for the slot:
```csharp
var slotEntries = blockState != null
    ? FilterBlockEntriesForSlot(blockState, slotIndex)
    : Array.Empty<BlockStateEntry>();
```
Pass `slotEntries` to `ResolveSlot`.

- [ ] **Step 4: Run test to verify pass**

Run EditMode filter `Alrauna.Amuse.Tests.Editor.Analysis.AdmittedMaterialStatesTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
  Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs
git commit -m "Thread per-slot block state into slot resolution"
```

---

### Task 3: Apply unanimated block overrides in slot resolution evidence

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:220-250, 300-330`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Consumes: `BlockStateEntry`, `CapturedMaterialEvidence.WithScalar`, `WithColor`, `WithVector`.
- Produces: For each admitted material in a slot, any unanimated declared property present in `slotBlockEntries` updates the evidence before semantics resolution.

- [ ] **Step 1: Write the failing test**

In `AdmittedMaterialStatesTests.cs`:

```csharp
[Test]
public void UnanimatedBlockOverrideUpdatesEvidenceScalar()
{
    var material = CreateTestCapturedAlphaMaterial(cutoff: 0.5f);
    var blockEntries = new[]
    {
        new BlockStateEntry(0, "_Cutoff", ShaderPropertyType.Float, 0.25f, default, default, null),
    };

    var success = AdmittedMaterialStates.TryAdmitDerivedEvidence(
        material,
        Array.Empty<(CapturedFloatBinding, AnimatedPropertyRef)>(),
        CreateCutoffEvidenceRequest(),
        blockEntries,
        out var derived,
        out var refusal);

    Assert.That(success, Is.True);
    Assert.That(derived.TryGetScalar("_Cutoff", out var value), Is.True);
    Assert.That(value, Is.EqualTo(0.25f));
}
```

- [ ] **Step 2: Run test to verify failure**

Expected: FAIL because `TryAdmitDerivedEvidence` does not yet apply unanimated block overrides to `derived`.

- [ ] **Step 3: Implement unanimated block override application**

In `AdmittedMaterialStates.cs`, in `TryAdmitDerivedEvidence`:
Before or after `GroupByProperty(bindings)`:
For each entry in `slotBlockEntries`:
If the property is not animated by `bindings`:
If entry is Float/Range: `derived = derived.WithScalar(entry.Name, entry.FloatValue)`
If entry is Color: `derived = derived.WithColor(entry.Name, entry.ColorValue)`
If entry is Vector: `derived = derived.WithVector(entry.Name, entry.VectorValue)`

- [ ] **Step 4: Run test to verify pass**

Run EditMode filter `Alrauna.Amuse.Tests.Editor.Analysis.AdmittedMaterialStatesTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs
git commit -m "Apply unanimated block overrides in admitted material evidence"
```

---

### Task 4: Verify animated property domains across Serialized, Block, and Curve

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:368-438, 639-730`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Consumes: `BlockStateEntry`, `AdmitScalar`, `AdmitColor`, `AdmitVector`.
- Produces: If an animated property has a block entry, the block value is verified against the serialized default and curve values. If they disagree, `Admit` returns `RendererAnalysisRefusal.AnimatedMaterialPropertyNotSingleton`.

- [ ] **Step 1: Write the failing tests**

In `AdmittedMaterialStatesTests.cs`:

```csharp
[Test]
public void AgreeingBlockEntryAdmitsAsSingleton()
{
    var material = CreateTestCapturedAlphaMaterial(cutoff: 0.5f);
    var bindings = CreateConstantCurveBindings("_Cutoff", 0.5f);
    var blockEntries = new[]
    {
        new BlockStateEntry(0, "_Cutoff", ShaderPropertyType.Float, 0.5f, default, default, null),
    };

    var success = AdmittedMaterialStates.TryAdmitDerivedEvidence(
        material, bindings, CreateCutoffEvidenceRequest(), blockEntries,
        out var derived, out var refusal);

    Assert.That(success, Is.True);
    Assert.That(refusal, Is.EqualTo(RendererAnalysisRefusal.None));
}

[Test]
public void DisagreeingBlockEntryRefusesWithNotSingleton()
{
    var material = CreateTestCapturedAlphaMaterial(cutoff: 0.5f);
    var bindings = CreateConstantCurveBindings("_Cutoff", 0.5f);
    var blockEntries = new[]
    {
        new BlockStateEntry(0, "_Cutoff", ShaderPropertyType.Float, 0.25f, default, default, null),
    };

    var success = AdmittedMaterialStates.TryAdmitDerivedEvidence(
        material, bindings, CreateCutoffEvidenceRequest(), blockEntries,
        out var derived, out var refusal);

    Assert.That(success, Is.False);
    Assert.That(refusal, Is.EqualTo(RendererAnalysisRefusal.AnimatedMaterialPropertyNotSingleton));
}
```

- [ ] **Step 2: Run tests to verify failure**

Expected: `DisagreeingBlockEntryRefusesWithNotSingleton` fails because `AdmitScalar` does not yet check the block value.

- [ ] **Step 3: Implement domain verification in `Admit`**

In `AdmittedMaterialStates.cs`:
In `Admit`:
Lookup property name in `slotBlockEntries`.
If block entry is present:
Pass `blockValue` to `AdmitScalar` (or check `blockValue == serializedDefault` before `AdmitScalar`).
If `blockValue != serializedDefault`, return `RendererAnalysisRefusal.AnimatedMaterialPropertyNotSingleton`.
Apply the same rule to `AdmitColor` and `AdmitVector`.

- [ ] **Step 4: Run tests to verify pass**

Run EditMode filter `Alrauna.Amuse.Tests.Editor.Analysis.AdmittedMaterialStatesTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs
git commit -m "Verify animated property block domain equality in admission"
```

---

### Task 5: Split and re-pin `CutoutAnimationAtTheSerializedValuePrepares`

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs:2780-2845, 4385-4405`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: Unity animation curve binding to `material._MainTex_ST.x`.
- Produces:
  1. `CutoutAnimationAtTheSerializedValuePrepares`: exercises all scalar, color, and independent vector properties (positive control).
  2. `CutoutAnimationOfDegenerateStComponentVectorRefuses`: exercises `material._MainTex_ST.x` and asserts fail-closed refusal with `AdmittedMaterialSemanticsUnknown`.

- [ ] **Step 1: Write the failing negative regression test**

Add `CutoutAnimationOfDegenerateStComponentVectorRefuses` to `AlphaSeparationPreparationTests.cs`:

```csharp
[Test]
public void CutoutAnimationOfDegenerateStComponentVectorRefuses()
{
    using var assets = new OverrideTemporaryDirectoryScope(null);
    var fixtures = new LilToonCutoutConversionFixtures();
    try
    {
        fixtures.BaseSetUp();
        var texture = fixtures.ImportFullyOpaqueMipmap("cutout_st_degenerate");
        using var arm = CutoutArmFixture.Create(
            texture,
            "AMUSE cutout degenerate ST",
            null,
            "material._MainTex_ST.x",
            1f);
        var amuse = arm.Run();

        Assert.That(amuse.SemanticallyRefusedRendererCount, Is.EqualTo(1));
        Assert.That(
            amuse.RendererRefusalCount(
                RendererAnalysisRefusal.AdmittedMaterialSemanticsUnknown),
            Is.EqualTo(1),
            "animating a single ST component writes a degenerate vector (x, 0, 0, 0) " +
            "that collapses UV space and must refuse fail-closed");
    }
    finally
    {
        fixtures.BaseTearDown();
    }
}
```

- [ ] **Step 2: Exclude ST component properties from the positive loop**

In `CutoutAnimatedPropertyCases` in `AlphaSeparationPreparationTests.cs`:
Remove `_MainTex_ST` and `_AlphaMask_ST` from the positive preparation loop.
Keep all float scalars, `_Color.a`, `_DissolveParams`, and `_MainTex_ScrollRotate`.

- [ ] **Step 3: Run both tests**

Run EditMode filter `Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationPreparationTests.CutoutAnimation`.
Expected:
`CutoutAnimationAtTheSerializedValuePrepares`: PASS (all remaining properties prepare).
`CutoutAnimationOfDegenerateStComponentVectorRefuses`: PASS (asserts exact fail-closed refusal).

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs
git commit -m "Split cutout ST component animation into dedicated fail-closed pin"
```

---

### Task 6: Re-pin `RuntimeStateProductionEntry_PostClosureSlotRefusalKeepsTheValidSibling`

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:3404-3498`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: Two-slot renderer where slot 0 fails post-closure admission and slot 1 resolves.
- Produces: Slot 0 failure is isolated to slot 0 without renderer-wide block contamination. Slot 1 resolves and produces an opaque candidate triangle.

- [ ] **Step 1: Inspect the test failure and isolate slot 0 failure**

In `AmusePlatformFinishPluginTests.cs`:
Update `RuntimeStateProductionEntry_PostClosureSlotRefusalKeepsTheValidSibling` so that slot 0's failure is slot-scoped:
Instead of a renderer-wide curve `material._AlphaForceOpaque = 1f` conflicting with slot 0's default, use:
- Slot 0: material with an unprovable alpha setup or per-slot cutoff that fails threshold.
- Slot 1: material that admits and proves opaque.
- Animation: drives slot 1 or an agreeing property.

- [ ] **Step 2: Run test and observe pass**

Run EditMode filter `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.RuntimeStateProductionEntry_PostClosureSlotRefusalKeepsTheValidSibling`.
Expected: PASS with `amuse.SemanticallyRefusedRendererCount == 0` and `amuse.OpaqueCandidateTriangleCount == 1`.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "Isolate slot-scoped failure in post-closure valid sibling test"
```

---

### Task 7: Full suite verification and identifier sweep

**Files:**
- Verification only. No functional file modifications.

- [ ] **Step 1: Run full product tests**

Run EditMode assembly: `Alrauna.Amuse.Tests.Editor`.
Record observed passed and failed test counts.

- [ ] **Step 2: Run full research tests**

Run EditMode assembly: `Alrauna.Amuse.Research.Tests.Editor`.
Record observed passed and failed test counts.

- [ ] **Step 3: Run git diff and whitespace checks**

Run: `git diff --check`.
Expected: clean, zero whitespace errors.

- [ ] **Step 4: Run identifier sweep**

Search git diff against `main` for:
- Drive letters, home directory paths, absolute paths.
- Four-digit ports.
- Hexadecimal hashes.
- Private avatar or vendor asset names.
Expected: zero hits.
