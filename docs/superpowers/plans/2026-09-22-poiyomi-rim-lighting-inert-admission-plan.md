# Poiyomi rim lighting inert admission implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admit the poiyomi rim lighting family as an alpha identity when the vendor alpha scalar `_RimApplyAlpha` is proven exactly zero, and refuse any nonzero, non-finite, or missing value by name.

**Architecture:** Swap the two blanket `_EnableRimLighting` and `_EnableRim2Lighting` entries in the alpha feature gate array for `_RimApplyAlpha`. No helper is needed: the existing exact-zero gate machinery carries the whole contract, and the evidence request unions the gate list, so the new scalar rides the existing capture and animation closure. The base color and emission gate arrays keep their rim entries.

**Tech Stack:** Unity 2022.3.22f1 editor C#, NUnit EditMode tests, Unity MCP test runner. Compilation happens inside the pinned dev editor instance.

**Spec:** `docs/superpowers/specs/2026-09-22-poiyomi-rim-lighting-inert-admission-design.md`

## Global Constraints

- Base branch `feat/poiyomi-rim-lighting` at the design commit `b46c2fc`.
- One production assembly. Production types `internal`. File name equals type name. One public type per file.
- RED before GREEN: every behavior test is observed failing against the current code first, for the named plausible wrong implementation.
- A filtered run reporting 0 tests is a failure. Record observed counts.
- Tests run in the pinned dev editor instance after its identity check. Never in the Census Lab project. Before every Unity MCP operation: enumerate reachable instances, check that `Application.dataPath` equals the repository root's `Assets` folder exactly, and pin the dev editor instance.
- No staging or committing beyond the commit steps in this plan without further authorization.
- Simple english in documents and comments. Short active sentences. No contractions in XML doc comments.

---

### Task 1: RED - stand-in schema, mirror update, and the failing tests

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader` (property block, after `_EnableRim2Lighting`, line 111)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiTwoPassSemanticTest.shader` (same block, after `_EnableRim2Lighting`, line 117)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiBaseColorAlphaTests.cs` (alpha mirror array and its comment only, lines 419 to 446)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiRimLightingAlphaTests.cs` (+ the `.meta` Unity generates on refresh)

**Interfaces:**
- Consumes: `PoiyomiFixtureTestBase.NewFixtureMaterial()`, `PoiyomiFixtureTestBase.NewTwoPassFixtureMaterial()`, `AssertOutputComplete`, `AssertUnsupportedOutput`, and the local `Interpret` and `NonForcedMaterial` helper pattern of `PoiyomiDecalSlotAlphaTests`.
- Produces: the new contract the production change must satisfy.

- [ ] **Step 1: Add `_RimApplyAlpha` to both stand-in shaders, after `_EnableRim2Lighting`**

In `PoiyomiSemanticTest.shader`:

```shaderlab
        _EnableRimLighting ("Rim", Float) = 0
        _EnableRim2Lighting ("Rim 2", Float) = 0
        _RimApplyAlpha ("Rim Apply to Alpha", Float) = 0
        _EnableDepthRimLighting ("Depth Rim", Float) = 0
        _EnableEnvironmentalRim ("Environmental Rim", Float) = 0
```

The same three context lines and the same insertion in `PoiyomiTwoPassSemanticTest.shader`:

```shaderlab
        _EnableRimLighting ("Rim", Float) = 0
        _EnableRim2Lighting ("Rim 2", Float) = 0
        _RimApplyAlpha ("Rim Apply to Alpha", Float) = 0
        _EnableDepthRimLighting ("Depth Rim", Float) = 0
        _EnableEnvironmentalRim ("Environmental Rim", Float) = 0
```

The declared default zero matches the vendor default of `_RimApplyAlpha` (pinned 9.3.64 source, line 1663). The stand-ins do not need `_RimApplyAlphaBlend` or the style floats: the new contract reads `_RimApplyAlpha` only.

- [ ] **Step 2: Swap the two rim enable entries for `_RimApplyAlpha` in the `AlphaFeatureGates` mirror of `PoiyomiBaseColorAlphaTests.cs`, and update the comment**

Replace lines 419 to 446 (comment plus array) with:

```csharp
        // Enabled writers/masks that modify the non-forced alpha term.
        // _MainAlphaMaskMode is deliberately absent: it is no longer an
        // exact-off gate but an interpreted mode, so PoiyomiAlphaMaskTests owns
        // its supported and refused cases. The four decal slots are absent:
        // PoiyomiDecalSlotAlphaTests owns their per-slot inert proof. The two
        // rim enable floats are absent: the rim family's alpha behavior is
        // governed by _RimApplyAlpha alone, so PoiyomiRimLightingAlphaTests
        // owns its contract. Depth rim and environmental rim stay gated.
        private static readonly string[] AlphaFeatureGates =
        {
            "_AlphaMod",
            "_AlphaDistanceFade",
            "_AlphaFresnel",
            "_AlphaAngular",
            "_AlphaAudioLinkEnabled",
            "_EnableAudioLink",
            "_AlphaGlobalMask",
            "_BackFaceEnabled",
            "_RGBMaskEnabled",
            "_EnableFlipbook",
            "_RimApplyAlpha",
            "_EnableDepthRimLighting",
            "_EnableEnvironmentalRim",
            "_VideoEffectsEnable",
            "_EnableTouchGlow",
            "_MainVertexColoringEnabled",
        };
```

Leave the `BaseColorFeatureGates` mirror near line 25 unchanged: the base color output keeps refusing rim materials by name.

- [ ] **Step 3: Write `PoiyomiRimLightingAlphaTests.cs`**

```csharp
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    public sealed class PoiyomiRimLightingAlphaTests : PoiyomiFixtureTestBase
    {
        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        // A material on the non-forced alpha path with the mask mode off, so
        // alpha is proven from _MainTex.a and/or _Color.a.
        private Material NonForcedMaterial()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            return material;
        }

        // A non-forced material on the Two Pass stand-in shader.
        private Material NonForcedTwoPassMaterial()
        {
            var material = NewTwoPassFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            return material;
        }

        [Test]
        public void
            RimSlot1_Enabled_WithZeroApplyAlpha_KeepsAlphaComplete()
        {
            // --- Falsifier 1: a plausible wrong implementation keeps the
            // committed enable gates and refuses every enabled rim slot by
            // name. It fails this case.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 0f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            BothRimSlots_Enabled_WithZeroApplyAlpha_KeepsAlphaComplete()
        {
            // --- Falsifier 2: a plausible wrong implementation admits slot
            // one only, or reads only slot one's enable float.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_EnableRim2Lighting", 1f);
            material.SetFloat("_RimApplyAlpha", 0f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            RimSlot1_Enabled_WithAddApplyAlpha_RefusesNamingApplyAlpha()
        {
            // --- Falsifier 3: a plausible wrong implementation refuses the
            // rim family but names the enable float instead of the scalar
            // that carries the alpha decision.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_RimApplyAlpha");
        }

        [Test]
        public void
            RimSlot1_Enabled_WithMultiplyApplyAlpha_RefusesNamingApplyAlpha()
        {
            // --- Falsifier 4: a plausible wrong implementation checks the
            // add mode only and treats the multiply mode as inert, claiming
            // an alpha the vendor rescales (pinned 9.3.64 source, vendor
            // line 25843).
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 2f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_RimApplyAlpha");
        }

        [Test]
        public void
            RimSlots_Disabled_WithNonZeroApplyAlpha_RefusesNamingApplyAlpha()
        {
            // --- Falsifier 5: a plausible wrong implementation binds the
            // scalar check on the enable floats. The vendor call sites guard
            // on the enable keywords, not on the floats, so the gate must
            // bind whatever the floats say. This case closes the
            // float-keyword soundness hole the design names in section 8.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 0f);
            material.SetFloat("_EnableRim2Lighting", 0f);
            material.SetFloat("_RimApplyAlpha", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_RimApplyAlpha");
        }

        [Test]
        public void
            RimSlot1_Enabled_WithNonFiniteApplyAlpha_RefusesNamingApplyAlpha()
        {
            // --- Falsifier 6: a plausible wrong implementation treats a
            // garbage scalar as inert. The fail-closed direction refuses it.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", float.NaN);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_RimApplyAlpha");
        }

        [Test]
        public void
            ForcedOpaque_WithNonZeroApplyAlpha_StillClaimsConstantOne()
        {
            // No-op guard: the forced short-circuit precedes the gate check,
            // matching the vendor order where the forcing runs after every
            // rim write. It must pass before and after the change.
            var material = NonForcedMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 1f);
            material.SetFloat("_AlphaForceOpaque", 1f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            TwoPass_RimSlot1_Enabled_WithZeroApplyAlpha_KeepsAlphaComplete()
        {
            // --- Falsifier 7: a plausible wrong implementation admits the
            // plain family only. The Two Pass source carries the same rim
            // mechanism (pinned source, vendor lines 25933 to 25940), so the
            // contract covers both families.
            var material = NonForcedTwoPassMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 0f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            TwoPass_WithAddApplyAlpha_RefusesNamingApplyAlpha()
        {
            // --- Falsifier 8: a plausible wrong implementation gates the
            // scalar on the plain family only.
            var material = NonForcedTwoPassMaterial();
            material.SetFloat("_EnableRimLighting", 1f);
            material.SetFloat("_RimApplyAlpha", 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_RimApplyAlpha");
        }
    }
}
```

One recorded deviation from the design's case 6: the design asked for a material where `_RimApplyAlpha` is absent. Both stand-in shaders carry every gate property by design, so absence would need a third, partial fixture shader for one case. The non-finite case above covers the fail-closed direction through the same `FirstFailedZeroGate` branch (`TryGetScalar` false and a non-finite value take the same return), so the plan implements garbage-value coverage and leaves the absent-property branch to the shared machinery.

- [ ] **Step 4: Refresh Unity and run the filtered test class**

Refresh assets, then `run_tests` (EditMode, filter `PoiyomiRimLightingAlphaTests`) in the pinned dev editor instance after its identity check. Expected: eight of the nine tests fail, and only the forced-opaque guard passes. Precisely: cases 1 and 2 fail refusing naming `_EnableRimLighting`; cases 3 and 4 fail refusing naming `_EnableRimLighting` instead of `_RimApplyAlpha`; case 5 fails because the code as of 2026-09-22 completes the material; case 6 fails for the same reason; both Two Pass cases fail like their plain twins. Record the observed failure count and names. A green run here means the plan's premise is wrong: stop and re-read the spec.

- [ ] **Step 5: Run the touched neighbor classes**

`PoiyomiBaseColorAlphaTests`, `PoiyomiAlphaMaskTests`, `PoiyomiDecalSlotAlphaTests`. Expected: green. The mirror swap keeps the loop test consistent with the current production gate list, so no failure is expected yet.

### Task 2: GREEN - the production change

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` (the `AlphaFeatureGates` array only, lines 244 to 263)

**Interfaces:**
- Consumes: nothing new. The existing `FirstFailedZeroGate` machinery, the consult at line 1149, and the union at line 2500 stay untouched.
- Produces: the alpha contract the new tests assert.

- [ ] **Step 1: Edit the gate array**

Replace the two rim enable entries with `_RimApplyAlpha` and record why:

```csharp
        // Coverage/clip mechanisms that change effective alpha coverage even
        // when the alpha value is forced opaque. Proven off on every alpha path,
        // including the forced-opaque short-circuit.
        private static readonly string[] AlphaFeatureGates =
        {
            "_AlphaMod",
            "_AlphaDistanceFade",
            "_AlphaFresnel",
            "_AlphaAngular",
            "_AlphaAudioLinkEnabled",
            "_EnableAudioLink",
            "_AlphaGlobalMask",
            "_BackFaceEnabled",
            "_RGBMaskEnabled",
            "_ALDecalControlsAlpha",
            "_EnableFlipbook",
            // The rim family: the liltoon-style and UTS2-style arms never
            // touch the chain alpha, and the poi-style arm writes it only
            // through _RimApplyAlpha (design 2026-09-22; pinned 9.3.64
            // source, vendor lines 25838 to 25843, both slots sharing the
            // one scalar). At zero every compiled arm is an alpha identity,
            // whatever the enable floats and keywords say, because the call
            // sites guard on the keywords.
            "_RimApplyAlpha",
            "_EnableDepthRimLighting",
            "_EnableEnvironmentalRim",
            "_VideoEffectsEnable",
            "_EnableTouchGlow",
            "_MainVertexColoringEnabled",
        };
```

The list keeps `_ALDecalControlsAlpha`, `_EnableDepthRimLighting`, and `_EnableEnvironmentalRim` exactly where they are; only the two enable names leave and `_RimApplyAlpha` joins.

- [ ] **Step 2: Refresh Unity and run the filtered test class**

`run_tests` (EditMode, filter `PoiyomiRimLightingAlphaTests`). Expected: all nine tests green. Record the observed count.

- [ ] **Step 3: Run the touched neighbors again**

`PoiyomiBaseColorAlphaTests`, `PoiyomiAlphaMaskTests`, `PoiyomiDecalSlotAlphaTests`, `PoiyomiTwoPassAlphaTests`, `PoiyomiMaterialSemanticsTests`, `PoiyomiAdversarialTests`. Expected: green.

### Task 3: Full suite and commit

**Files:**
- Modify: none new; validation and commit only.

- [ ] **Step 1: Run the full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies**

Expected: every test green except the known baseline failures (four in the Avatar Optimizer environment merged-consumption class, one degenerate scale-offset refusal). Record observed totals and compare against the baseline: no new failures.

- [ ] **Step 2: Identifier sweep of every changed file**

The sweep checks for an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and every private asset name known to the session. Fix every hit before reporting.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiTwoPassSemanticTest.shader Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiBaseColorAlphaTests.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiRimLightingAlphaTests.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiRimLightingAlphaTests.cs.meta
git commit -m "feat: admit the poiyomi rim lighting slots proven alpha inert"
```

The plan document commits separately:

```bash
git add docs/superpowers/plans/2026-09-22-poiyomi-rim-lighting-inert-admission-plan.md
git commit -m "docs: plan the poiyomi rim lighting inert admission"
```

## Self-review

- Spec coverage: design sections 1 and 7 map to Task 1 steps 1 to 2 and Task 2 step 1 (the gate swap). Design section 5 (keyword independence) is case 5. Design section 8 (the narrowing) is case 5's RED obligation. Design section 9 cases 1 to 9 are the plan's nine tests; design case 6 (absent property) is the recorded deviation in Task 1 step 3, covered by the non-finite case through the same gate branch. Design section 10 (honest expectation) needs no task: it is a corpus judgment, not code.
- Placeholders: none; every code step carries its content.
- Type consistency: `Interpret`, `NonForcedMaterial`, `NonForcedTwoPassMaterial`, and the `_RimApplyAlpha` property name appear identically across Task 1 steps 1 to 3 and Task 2 step 1. The mirror array name `AlphaFeatureGates` matches production.
