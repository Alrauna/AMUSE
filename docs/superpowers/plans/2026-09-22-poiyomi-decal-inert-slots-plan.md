# Poiyomi decal inert slots implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admit an enabled poiyomi decal slot as an alpha identity when its `OverrideAlpha` mode is proven zero, and gate the audio link decal module's alpha weight.

**Architecture:** Replace the four blanket `_DecalEnabled` entries in the alpha feature gate array with a per-slot inert proof at the same precedence point inside the single-family alpha interpreter. Add `_ALDecalControlsAlpha` as an ordinary zero gate. New scalars join the alpha evidence request, which both families and the live-material path derive from.

**Tech Stack:** Unity 2022.3.22f1 editor C#, NUnit EditMode tests, Unity MCP test runner. Compilation happens inside the pinned dev editor instance.

**Spec:** `docs/superpowers/specs/2026-09-22-poiyomi-decal-inert-slots-design.md`

## Global Constraints

- Base branch `feat/poiyomi-decal-slots` at the characterization commit `77b6396`.
- One production assembly. Production types `internal`. File name equals type name. One public type per file.
- RED before GREEN: every behavior test is observed failing against the current code first, for the named plausible wrong implementation.
- A filtered run reporting 0 tests is a failure. Record observed counts.
- Tests run in the pinned dev editor instance after its identity check. Never in the Census Lab project.
- No staging or committing beyond the commit steps in this plan without further authorization.
- Simple english in documents and comments. Short active sentences. No contractions in XML doc comments.

---

### Task 1: RED - stand-in schema, mirror update, and the failing tests

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader` (property block, after `_DecalEnabled3`)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiTwoPassSemanticTest.shader` (same block)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiBaseColorAlphaTests.cs` (mirror array only)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiDecalSlotAlphaTests.cs` (+ `.meta` copied pattern from an existing test `.meta`)

**Interfaces:**
- Consumes: `PoiyomiFixtureTestBase.NewFixtureMaterial()`, `NonForcedMaterial()` pattern, `Interpret`, `AssertOutputComplete`, `AssertUnsupportedOutput`.
- Produces: the new contract the production change must satisfy.

- [ ] **Step 1: Add the five floats to both stand-in shaders, after `_DecalEnabled3`**

```shaderlab
        _DecalOverrideAlpha ("Decal 0 Alpha Blend Mode", Float) = 0
        _DecalOverrideAlpha1 ("Decal 1 Alpha Blend Mode", Float) = 0
        _DecalOverrideAlpha2 ("Decal 2 Alpha Blend Mode", Float) = 0
        _DecalOverrideAlpha3 ("Decal 3 Alpha Blend Mode", Float) = 0
        _ALDecalControlsAlpha ("AL Decal Controls Alpha", Float) = 0
```

- [ ] **Step 2: Drop the four decal entries from the `AlphaFeatureGates` mirror in `PoiyomiBaseColorAlphaTests.cs`, and note why**

```csharp
        // Enabled writers/masks that modify the non-forced alpha term.
        // _MainAlphaMaskMode is deliberately absent: it is no longer an
        // exact-off gate but an interpreted mode, so PoiyomiAlphaMaskTests owns
        // its supported and refused cases. The four decal slots are absent:
        // PoiyomiDecalSlotAlphaTests owns their per-slot inert proof.
```

- [ ] **Step 3: Write `PoiyomiDecalSlotAlphaTests.cs`**

```csharp
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    public sealed class PoiyomiDecalSlotAlphaTests : PoiyomiFixtureTestBase
    {
        private static readonly string[] SlotOverrideAlphaProperties =
        {
            "_DecalOverrideAlpha",
            "_DecalOverrideAlpha1",
            "_DecalOverrideAlpha2",
            "_DecalOverrideAlpha3",
        };

        private static readonly int[] OverrideAlphaModes =
            { 1, 2, 3, 4, 5, 6 };

        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        private Material MaterialWithDecalSlot(int slot, float mode)
        {
            var material = NonForcedMaterial();
            material.SetFloat(
                slot == 0 ? "_DecalEnabled" : "_DecalEnabled" + slot, 1f);
            material.SetFloat(SlotOverrideAlphaProperties[slot], mode);
            return material;
        }

        [Test]
        public void
            EnabledDecalSlot_ProvenInertOverrideAlpha_KeepsAlphaComplete()
        {
            // --- Falsifier 1: a plausible wrong implementation keeps the
            // committed blanket gate and refuses every enabled decal by
            // name. It fails this case.
            var material = MaterialWithDecalSlot(0, 0f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            EnabledDecalSlot_WithAnyOverrideAlphaMode_RefusesNamingTheMode(
                [ValueSource(nameof(OverrideAlphaModes))] int mode)
        {
            // --- Falsifier 2: a plausible wrong implementation refuses the
            // slot but names the enable float instead of the mode float.
            var material = MaterialWithDecalSlot(0, mode);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_DecalOverrideAlpha");
        }

        [Test]
        public void
            EnabledHigherSlot_WithOverrideAlphaMode_RefusesNamingThatSlot(
                [ValueSource(nameof(SlotOverrideAlphaProperties))]
                string overrideProperty)
        {
            // --- Falsifier 3: a plausible wrong implementation checks slot
            // zero only, or names slot zero's property for every slot.
            var slot = System.Array.IndexOf(
                SlotOverrideAlphaProperties, overrideProperty);
            var material = MaterialWithDecalSlot(slot, 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                overrideProperty);
        }

        [Test]
        public void
            DisabledDecalSlot_WithAnOverrideAlphaMode_DoesNotRefuse()
        {
            // --- Falsifier 4: a plausible wrong implementation requires
            // the inert proof even for a slot whose enable float is zero,
            // refusing materials the committed gate already admitted.
            var material = NonForcedMaterial();
            material.SetFloat("_DecalEnabled", 0f);
            material.SetFloat("_DecalOverrideAlpha", 3f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            AudioLinkDecalControlsAlpha_NonZero_RefusesNamingTheWeight()
        {
            // --- Falsifier 5: a plausible wrong implementation admits the
            // decal slots but leaves the audio link decal module's weight
            // ungated, claiming an alpha the module scales.
            var material = NonForcedMaterial();
            material.SetFloat("_ALDecalControlsAlpha", 0.5f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_ALDecalControlsAlpha");
        }

        [Test]
        public void
            ForcedOpaque_EnabledDecalSlot_StillClaimsConstantOne()
        {
            // No-op guard: the forced short-circuit precedes the decal
            // proof, matching the vendor order where the forcing runs
            // after the decal block.
            var material = MaterialWithDecalSlot(0, 2f);
            material.SetFloat("_AlphaForceOpaque", 1f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }
    }
}
```

- [ ] **Step 4: Refresh Unity and run the filtered test class**

`run_tests` (EditMode, filter `PoiyomiDecalSlotAlphaTests`) in the pinned dev editor instance after its identity check. Expected: the six tests of cases 1 to 5 fail, and the guard test 6 fails too, because the current blanket gate refuses before every new path. Record the observed failure count and names. A green run here means the plan's premise is wrong: stop and re-read the spec.

Observed 2026-09-22: the filter selected 14 cases. Twelve failed, exactly the falsified ones: the inert-slot case, all six mode cases, all four slot cases, and the audio link weight case. Two passed: the forced-opaque guard, and case 4, the disabled slot. Case 4 passes on the committed code because the blanket gate reads the enable float, so it never fires on a zero enable. Case 4 is therefore a characterization guard for the new contract, not a RED obligation, and it is recorded as such.

- [ ] **Step 5: Run the two touched neighbor classes**

`PoiyomiBaseColorAlphaTests` and `PoiyomiAlphaMaskTests`. Expected: green. The mirror drop keeps the loop test consistent with the current production gate list, so no failure is expected yet.

### Task 2: GREEN - the production change

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` (gate arrays, request union, helper, hook)

**Interfaces:**
- Consumes: `FirstFailedZeroGate`, `RecordUnknown`, `AddDiagnostic`, `CapturedMaterialEvidence.TryGetScalar`, `IsFinite`.
- Produces: `FirstNonInertDecalSlot(CapturedMaterialEvidence)` returning the refusal property name or null; `AlphaDecalSlotEnables`; `AlphaDecalSlotOverrideAlphaProperties`.

- [ ] **Step 1: Edit the gate arrays**

Remove the four `_DecalEnabled` entries from `AlphaFeatureGates` and add the audio link weight:

```csharp
        // The audio link decal module writes the chain alpha through
        // lerp(alpha, alpha * audioLinkValue, _ALDecalControlsAlpha)
        // (investigation 2026-09-22, vendor line 24833), independently of
        // the four decal slots. A zero weight is an identity; any other
        // value scales the chain alpha, so it is proven exactly zero on
        // every non-forced alpha path.
        "_ALDecalControlsAlpha",
```

- [ ] **Step 2: Add the slot arrays beside the gate arrays**

```csharp
        // The four decal slots. An enabled slot writes the chain alpha only
        // inside its override-alpha block (pinned 9.3.64 source, vendor
        // lines 22958 to 22978), so a slot with the override-alpha mode
        // proven zero composes color and emission and writes no alpha. The
        // enable float decides whether the block exists for the slot, on
        // the same float-as-evidence trust every other gate here uses. The
        // enable names stay read on the alpha path, so the per-slot proof
        // can tell a disabled slot from an enabled one.
        private static readonly string[] AlphaDecalSlotEnables =
        {
            "_DecalEnabled",
            "_DecalEnabled1",
            "_DecalEnabled2",
            "_DecalEnabled3",
        };

        private static readonly string[] AlphaDecalSlotOverrideAlphaProperties =
        {
            "_DecalOverrideAlpha",
            "_DecalOverrideAlpha1",
            "_DecalOverrideAlpha2",
            "_DecalOverrideAlpha3",
        };
```

- [ ] **Step 3: Union the new scalars in `CreateAlphaEvidenceRequest`**

```csharp
            scalars.UnionWith(AlphaDecalSlotEnables);
            scalars.UnionWith(AlphaDecalSlotOverrideAlphaProperties);
```

- [ ] **Step 4: Add the per-slot helper next to `FirstFailedZeroGate`**

```csharp
        /// <summary>
        /// The first decal slot the alpha equation cannot prove inert, or
        /// null when every slot writes no alpha. A slot with its enable
        /// float proven zero has no alpha effect. An enabled slot is an
        /// alpha identity exactly when its override-alpha mode is exactly
        /// zero (pinned 9.3.64 source, vendor lines 22958 to 22978). The
        /// returned name is the vendor property the refusal reports.
        /// </summary>
        private static string FirstNonInertDecalSlot(
            CapturedMaterialEvidence evidence)
        {
            for (var slot = 0; slot < AlphaDecalSlotEnables.Length; slot++)
            {
                var enable = AlphaDecalSlotEnables[slot];
                if (!evidence.TryGetScalar(enable, out var enabled) ||
                    !IsFinite(enabled))
                {
                    return enable;
                }

                if (enabled == 0f)
                {
                    continue;
                }

                var overrideAlpha =
                    AlphaDecalSlotOverrideAlphaProperties[slot];
                if (!evidence.TryGetScalar(
                        overrideAlpha, out var mode) ||
                    !IsFinite(mode) ||
                    mode != 0f)
                {
                    return overrideAlpha;
                }
            }

            return null;
        }
```

- [ ] **Step 5: Hook the helper in `InterpretSingleFamilyAlpha`, directly after the `AlphaFeatureGates` check**

```csharp
            var nonInertDecalSlot = FirstNonInertDecalSlot(evidence);
            if (nonInertDecalSlot != null)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    PoiyomiSemanticOutput.Alpha,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    nonInertDecalSlot);
            }
```

- [ ] **Step 6: Refresh Unity and run the filtered test class**

`run_tests` (EditMode, filter `PoiyomiDecalSlotAlphaTests`). Expected: all green. Record the observed count.

- [ ] **Step 7: Run the touched neighbors again**

`PoiyomiBaseColorAlphaTests`, `PoiyomiAlphaMaskTests`, `PoiyomiTwoPassAlphaTests`, `UnityMaterialSemanticsTests`, `PoiyomiNeutralClaimGatingTests`. Expected: green.

### Task 3: Full suite and docs

**Files:**
- Modify: none new; validation only.

- [ ] **Step 1: Run the full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies**

Expected: every test green except the five known baseline failures (four in the Avatar Optimizer environment merged-consumption class, one degenerate scale-offset refusal). Record observed totals.

- [ ] **Step 2: Identifier sweep of every changed file**

The sweep checks for an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and every private asset name known to the session. Fix every hit before reporting.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiTwoPassSemanticTest.shader Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiBaseColorAlphaTests.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiDecalSlotAlphaTests.cs
git commit -m "feat: admit the poiyomi decal slots proven alpha inert"
```

The plan and design documents commit separately:

```bash
git add docs/superpowers/specs/2026-09-22-poiyomi-decal-inert-slots-design.md docs/superpowers/plans/2026-09-22-poiyomi-decal-inert-slots-plan.md
git commit -m "docs: design the poiyomi decal inert slot admission"
```

## Self-review

- Spec coverage: design sections 2 to 7 map to Task 1 steps 1 to 3 (schema, mirror, contract tests) and Task 2 steps 1 to 5 (gates, arrays, request, helper, hook). Design section 6 point 4 is Task 2 step 3. Design section 7 cases 1 to 7 are the plan's test list.
- Placeholders: none; every code step carries its content.
- Names: `AlphaDecalSlotEnables`, `AlphaDecalSlotOverrideAlphaProperties`, `FirstNonInertDecalSlot` appear identically in Task 2 steps 2 to 5.
