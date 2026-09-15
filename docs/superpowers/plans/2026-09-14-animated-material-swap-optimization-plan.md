# Animated material swap alpha optimization implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable alpha mesh separation and curve rewriting for renderers with animated material swaps. Scope float curves to declaring materials and enable identity mapping fallbacks for unconverted swap states.

**Architecture:** In `AdmittedMaterialStates.Admit`, undeclared properties are treated as inert for that material instead of triggering `AnimatedPropertyAbsentFromAdmittedMaterial`. In `AlphaSeparationPreparation`, an unconverted material maps to identity on the appended slot when another material in the slot converts to canonical opaque. `AlphaSeparationApply` rewrites the appended swap curves using the mapped materials.

**Tech Stack:** Unity 2022.3.22f1 editor APIs, NDMF 1.14.4, NUnit through Unity Test Framework (EditMode only).

**Spec:** `docs/superpowers/specs/2026-09-14-animated-material-swap-optimization-design.md`

## Global Constraints

- Compilation happens inside Unity. There is no dotnet build and no CLI test runner. Refresh the editor after new files, then run EditMode filters.
- A filtered run that reports 0 tests is a failure. Record observed counts.
- File name equals type name. One type per file. Production types are internal. Namespaces mirror folders.
- Vendor shaders are never installed. Tests use stand-in shaders under `Hidden/Alrauna/AmuseTests/*`.
- Fail closed. Never weaken a valid test.
- Source assets are never mutated. Transient clones are destroyed in the same scope that created them.
- Privacy: never record private names, machine paths, ports, or instance hashes. All prose follows ASD-STE100.

---

### Task 1: Scoped float property admission across heterogeneous swapped materials

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:415-445`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Consumes: `CapturedMaterialEvidence.TryGetScalar`, `TryGetColor`, `TryGetVector`.
- Produces: If an admitted material in a multi-material slot does not declare an animated property, `Admit` skips that property for that material instead of returning `RendererAnalysisRefusal.AnimatedPropertyAbsentFromAdmittedMaterial`.

- [ ] **Step 1: Write the failing test**

In `AdmittedMaterialStatesTests.cs`, add a test with a two-material slot where Material A declares `_Cutoff` and Material B does not:

```csharp
[Test]
public void SwappedMaterialMissingPropertyDoesNotRefuseSlot()
{
    var withProperty = Admitted(MaterialWithForcedOpaque(true))[0];
    var withoutProperty = Admitted(TransparentMaterialWithoutProperty())[0];
    var slot = new CapturedMaterialSlotEvidence(0, new[] { 0, 1 });

    var result = AdmittedMaterialStates.ResolveSlot(
        slot,
        new[] { withProperty, withoutProperty },
        new[] { Animated("_AlphaForceOpaque", 1f) },
        Relevance,
        NoAlphaFields,
        0,
        VerifiedAlphaOnly);

    Assert.That(result.IsResolved, Is.True);
}
```

- [ ] **Step 2: Run test to verify failure**

Run EditMode filter `Alrauna.Amuse.Tests.Editor.Analysis.AdmittedMaterialStatesTests.SwappedMaterialMissingPropertyDoesNotRefuseSlot`.
Expected: FAIL with `AnimatedPropertyAbsentFromAdmittedMaterial`.

- [ ] **Step 3: Implement tolerant property admission**

In `AdmittedMaterialStates.cs`:
In `Admit`:
When `evidence.TryGetScalar` (or color/vector) returns false:
Check if the slot has multiple admitted materials:
If the property is absent from this material, return `RendererAnalysisRefusal.None` instead of `AnimatedPropertyAbsentFromAdmittedMaterial`.
If the property is absent from all materials on the renderer, the outer structural check retains the refusal.

- [ ] **Step 4: Run test to verify pass**

Run EditMode filter `Alrauna.Amuse.Tests.Editor.Analysis.AdmittedMaterialStatesTests.SwappedMaterialMissingPropertyDoesNotRefuseSlot`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs
git commit -m "Allow undeclared animated properties on heterogeneous swapped materials"
```

---

### Task 2: Resilient opaque conversion with identity fallback

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:308-368, 475-520`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: `ConvertAdmittedMaterial`.
- Produces: If an admitted material in a multi-material slot cannot convert to a canonical opaque recipe, it maps to itself (`opaque = live`), as long as at least one admitted material in the slot converts to a canonical opaque material.

- [ ] **Step 1: Write the failing test**

In `AlphaSeparationPreparationTests.cs`, add a test with a two-material swap slot where Material A converts to a canonical opaque clone, but Material B belongs to an unconverted shader family:

```csharp
[Test]
public void SwappedMaterialWithUnconvertedSiblingMapsToIdentityAndPrepares()
{
    // Fixture with convertible Material A and unconverted Material B in slot 0.
    // Assert: slot prepares, mapping[A] is converted clone, mapping[B] is B itself.
}
```

- [ ] **Step 2: Run test to verify failure**

Run EditMode filter for the new test.
Expected: FAIL with `OpaqueConversionUnsupportedFamily` or `slot.Dropped`.

- [ ] **Step 3: Implement identity mapping fallback**

In `AlphaSeparationPreparation.cs`:
In the admitted materials loop for each candidate submesh:
If `ConvertAdmittedMaterial` returns an unconverted refusal (such as `OpaqueConversionUnsupportedFamily` or `ConversionStateNotAdmitted`), check if the slot has multiple admitted materials and another material can convert:
If so, set `opaque = live`, record no slot refusal, and add `mapping[live] = live`.
Require that at least one material in the slot converts to a non-identity replacement before the slot prepares.

- [ ] **Step 4: Run test to verify pass**

Run EditMode filter for the new test.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs
git commit -m "Support identity mapping fallback for unconverted swap states"
```

---

### Task 3: Integration testing of animated material swaps

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Consumes: `AlphaSeparationApply.Apply`.
- Produces: Verified end-to-end split of a mesh renderer that animates material swaps between convertible and identity-fallback materials.

- [ ] **Step 1: Write the integration test**

Add `AnimatedMaterialSwapWithPartialConversionSplitsAndRewritesAppendedCurve` to `AlphaSeparationApplyTests.cs`:
- Renderer has 1 slot swapping between Material A (convertible cutout) and Material B (opaque fallback).
- Polygons are visually opaque under both materials.
- Run `Apply`.
- Assert:
  - Renderer has 2 slots (original slot + appended slot).
  - Original slot keeps swap between Material A and Material B.
  - Appended slot has a new curve swapping between $A_{\text{opaque}}$ and Material B.

- [ ] **Step 2: Run test to verify pass**

Run EditMode filter for the integration test.
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs
git commit -m "Verify end-to-end animated material swap split and curve rewrite"
```

---

### Task 4: Full suite verification and identifier sweep

**Files:**
- Verification only.

- [ ] **Step 1: Run full product tests**

Run EditMode assembly: `Alrauna.Amuse.Tests.Editor`.
Record observed counts.

- [ ] **Step 2: Run full research tests**

Run EditMode assembly: `Alrauna.Amuse.Research.Tests.Editor`.
Record observed counts.

- [ ] **Step 3: Run git diff and whitespace checks**

Run: `git diff --check`.
Expected: clean.

- [ ] **Step 4: Run identifier sweep**

Run sweep for machine paths, user home directory paths, ports, instance hashes, and private names across the branch diff.
Expected: zero hits.
