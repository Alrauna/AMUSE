# Depth Test Policy Setting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Advanced Settings toggle "Allow Depth Test Change on Moved Triangles" (default on) that admits `_ZTest == Less` sources to opaque conversion, flags the divergence, refuses mixed splits for divergent sources, and reports the change.

**Architecture:** One policy boolean flows from the component through the build pass into the three conversion-eligibility gates, which admit exactly `Less` beside `LEqual`. The convertible outcome carries a divergence flag; the planner refuses mixed splits for flagged slots; the report names the change. The recipe still writes `LEqual`.

**Tech Stack:** C# 9, Unity 2022.3, NUnit (Unity Test Framework EditMode), NDMF API.

**Spec:** `docs/superpowers/specs/2026-09-17-depth-test-policy-design.md`

## Global Constraints

- RED first for every behavior change: observe the new test fail before the minimal fix. Never weaken a valid test. The existing policy-off refusal tests stay green.
- Every English text that a human reads uses ASD-STE100 Simplified Technical English: short active sentences, one idea per sentence, no semicolons, and no contractions.
- The optional policy parameter defaults to `false` (fail closed). An absent component reads as the default `true`.
- The policy admits exactly `Less` (2). Every other non-`LEqual` comparison keeps refusing with `UnsupportedDepthComparison`.
- The recipe still writes `_ZTest = 4`. No recipe change anywhere.
- No new dependency. Production types stay `internal`. The one new public surface is the component property, beside its existing siblings.
- All unit tests use the NUnit constraint model (`Assert.That(actual, Is.EqualTo(expected))`).
- All paths are relative to `<repo-root>/Packages/com.alrauna.amuse/`. Line numbers refer to the tree at the plan date. When earlier edits shift later anchors, locate the symbol by name, not by number.
- Tests run through the Unity Test Runner, EditMode mode. Refresh Unity after new files. A filtered run that reports zero tests is a failure. Record observed counts.
- Never record private avatar, renderer, material, animation clip, or controller names. Never record machine names, user paths, or ports.
- Base branch: cut `feature/depth-test-policy` from the latest local `main` at execution time. The investigation branch keeps only documents.

---

### Task 1: Policy gate and divergence flag, lilToon transparent

**Files:**
- Modify: `Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs:75-106` (`LilToonOpaqueConversionEligibility`)
- Modify: `Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs:196,288-293` (signature and gate 5)
- Modify: the file that defines `LilToonOpaqueConversionFactors.LEqualDepthComparison` (locate by name)
- Test: `Tests/Editor/Semantics/LilToon/LilToonTransparentSourceEligibilityTests.cs`

**Interfaces:**
- Consumes: existing `EvaluateVerifiedEligibility(CapturedMaterialEvidence, int, string)`, existing test helpers `Capture`, `AssertRefusal`, `AssertConvertible`
- Produces: `EvaluateVerifiedEligibility(CapturedMaterialEvidence, int, string, bool allowDepthTestChange = false)`; `LilToonOpaqueConversionEligibility.DepthTestDivergence` (bool property); `Convertible(bool depthTestDivergence = false)`; `LilToonOpaqueConversionFactors.LessDepthComparison` (float constant, 2)

- [ ] **Step 1: Write the failing tests**

Add the helper, mirroring the file's existing `EvaluateFor` shape:

```csharp
        private static LilToonOpaqueConversionEligibility EvaluateWithPolicy(
            Material material,
            string property,
            float value,
            bool allowDepthTestChange)
        {
            LilToonOpaqueTarget.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            return LilToonTransparentSourceEligibility
                .EvaluateVerifiedEligibility(
                    Capture(material).WithScalar(property, value),
                    queue,
                    renderType,
                    allowDepthTestChange);
        }
```

Add four tests. Reuse the exact material fixture the file's existing `UnsupportedDepthComparison` test builds:

```csharp
        [Test]
        public void DepthTestLessWithPolicyConvertsAndFlagsDivergence()
        {
            var eligibility = EvaluateWithPolicy(
                <depth test fixture>, "_ZTest", 2f, true);
            AssertConvertible(eligibility);
            Assert.That(eligibility.DepthTestDivergence, Is.True);
        }

        [Test]
        public void DepthTestLessWithoutPolicyStillRefuses()
        {
            var eligibility = EvaluateWithPolicy(
                <depth test fixture>, "_ZTest", 2f, false);
            AssertRefusal(
                eligibility,
                LilToonOpaqueConversionRefusal.UnsupportedDepthComparison);
        }

        [Test]
        public void DepthTestGreaterWithPolicyStillRefuses()
        {
            var eligibility = EvaluateWithPolicy(
                <depth test fixture>, "_ZTest", 5f, true);
            AssertRefusal(
                eligibility,
                LilToonOpaqueConversionRefusal.UnsupportedDepthComparison);
        }

        [Test]
        public void DepthTestLEqualWithPolicyCarriesNoDivergence()
        {
            var eligibility = EvaluateWithPolicy(
                <depth test fixture>, "_ZTest", 4f, true);
            AssertConvertible(eligibility);
            Assert.That(eligibility.DepthTestDivergence, Is.False);
        }
```

`<depth test fixture>` is the file's existing fixture construction, not new code. The `Greater` test is the falsifier for the plausible wrong implementation that admits every non-`LEqual` value under the policy. The `LEqual` test is the falsifier for the wrong implementation that flags divergence unconditionally.

- [ ] **Step 2: Observe the failure**

Refresh Unity. Run the EditMode filter `LilToonTransparentSourceEligibilityTests`.
Expected: the new tests fail (the parameter does not exist yet). Record the observed failure mode and count.

- [ ] **Step 3: Implement the minimal change**

Add `LessDepthComparison` beside `LEqualDepthComparison` in `LilToonOpaqueConversionFactors`. Extend the struct:

```csharp
        internal bool DepthTestDivergence { get; }

        internal static LilToonOpaqueConversionEligibility Convertible(
            bool depthTestDivergence = false)
        {
            return new LilToonOpaqueConversionEligibility(
                LilToonOpaqueConversionOutcome.Convertible,
                LilToonOpaqueConversionRefusal.None,
                depthTestDivergence);
        }
```

Thread the new field through the private constructor. Change gate 5 in `LilToonTransparentSourceEligibility`:

```csharp
            var depthComparison = Read(values, "_ZTest");
            if (depthComparison !=
                    LilToonOpaqueConversionFactors.LEqualDepthComparison &&
                !(allowDepthTestChange &&
                  depthComparison ==
                  LilToonOpaqueConversionFactors.LessDepthComparison))
            {
                return LilToonOpaqueConversionEligibility.Refused(
                    LilToonOpaqueConversionRefusal.UnsupportedDepthComparison);
            }
```

The success path becomes:

```csharp
            return LilToonOpaqueConversionEligibility.Convertible(
                depthComparison ==
                LilToonOpaqueConversionFactors.LessDepthComparison);
```

- [ ] **Step 4: Observe the pass**

Run the same filter. Expected: every test passes, including the untouched policy-off tests. Record the observed count.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentSourceEligibilityTests.cs
git commit -m "feat: admit Less depth test in lilToon transparent eligibility behind policy"
```

(Include the factors file in the same commit if it is a separate file.)

### Task 2: Policy gate, lilToon cutout

**Files:**
- Modify: `Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs:139,203-208`
- Test: `Tests/Editor/Semantics/LilToon/LilToonCutoutSourceEligibilityTests.cs`

**Interfaces:**
- Consumes: Task 1's struct flag and factors constant
- Produces: `LilToonCutoutSourceEligibility.EvaluateVerifiedEligibility` with the same optional fourth parameter

- [ ] **Step 1: Write the failing tests**

Mirror the four Task 1 tests with the file's existing helpers (`CaptureConversion`, `EvaluateWith`, `AssertRefusal`, `AssertConvertible`) and the file's existing depth-comparison fixture:

```csharp
        [Test]
        public void DepthTestLessWithPolicyConvertsAndFlagsDivergence()
        {
            var eligibility = EvaluateWith(
                <depth test fixture>, "_ZTest", 2f);
            AssertConvertible(eligibility);
            Assert.That(eligibility.DepthTestDivergence, Is.True);
        }
```

For this file, extend the existing `EvaluateWith` helper with an optional fifth parameter instead of adding a new helper:

```csharp
        private static LilToonOpaqueConversionEligibility EvaluateWith(
            Material material, string property, float value,
            bool allowDepthTestChange = false)
```

Add the same four behavior tests as Task 1 (Less with policy, Less without policy, Greater with policy, LEqual carries no divergence).

- [ ] **Step 2: Observe the failure**

Run the EditMode filter `LilToonCutoutSourceEligibilityTests`. Expected: the new tests fail. Record the failure mode.

- [ ] **Step 3: Implement the mirror change**

Apply the Task 1 gate and success-path change to `LilToonCutoutSourceEligibility`.

- [ ] **Step 4: Observe the pass**

Run the filter. Expected: all pass. Record the count.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutSourceEligibilityTests.cs
git commit -m "feat: admit Less depth test in lilToon cutout eligibility behind policy"
```

### Task 3: Policy gate, Poiyomi

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:244,313-318` and the `PoiyomiOpaqueConversionEligibility` struct in the same file
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`

**Interfaces:**
- Consumes: the Task 1 pattern; Poiyomi's local `LEqualDepthComparison` constant
- Produces: `PoiyomiOpaqueConversionEligibility.DepthTestDivergence`; `PoiyomiOpaqueConversion.EvaluateVerifiedEligibility` with the optional fourth parameter

- [ ] **Step 1: Write the failing tests**

Mirror the four tests with the file's existing helpers (`CaptureConversion`, the `EvaluateWith`-style helper at the file's line 857, `AssertRefusal` equivalent) and the file's existing depth-comparison fixture. Add a local `LessDepthComparison` constant beside Poiyomi's existing `LEqualDepthComparison`.

- [ ] **Step 2: Observe the failure**

Run the EditMode filter `PoiyomiOpaqueConversionTests`. Expected: the new tests fail. Record the failure mode.

- [ ] **Step 3: Implement the mirror change**

Apply the Task 1 gate, flag, and factory change to the Poiyomi struct and gate.

- [ ] **Step 4: Observe the pass**

Run the filter. Expected: all pass. Record the count.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs
git commit -m "feat: admit Less depth test in Poiyomi eligibility behind policy"
```

### Task 4: Component setting

**Files:**
- Modify: `Runtime/AmuseAvatarOptimizer.cs` (field beside `_ignoreOutOfRangeMaterialSlots` at line 96, property beside `IgnoreOutOfRangeMaterialSlots` at line 155)

**Interfaces:**
- Produces: `public bool AllowDepthTestChange => _allowDepthTestChange;`

- [ ] **Step 1: Add the field and property**

Insert the field and property exactly as the spec's "The setting" section shows, with the spec's doc comment text.

- [ ] **Step 2: Verify**

Refresh Unity. The component compiles. No dedicated test exists for the passthrough property, matching the existing `_ignoreOutOfRangeMaterialSlots` sibling; Task 6's plugin plumbing covers the read path.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs
git commit -m "feat: add Allow Depth Test Change component setting"
```

### Task 5: Inspector entry

**Files:**
- Modify: `Editor/AmuseAvatarOptimizerEditor.cs:306-321` (`DrawAdvancedSettings`)

**Interfaces:**
- Consumes: Task 4's serialized field name `_allowDepthTestChange`

- [ ] **Step 1: Add the entry**

Insert the `EditorGUILayout.PropertyField` call from the spec's "The setting" section after the existing out-of-range entry, inside the same foldout body.

- [ ] **Step 2: Verify**

Refresh Unity. Select a GameObject with the component. Expected: the Advanced Settings foldout shows "Allow Depth Test Change on Moved Triangles", checked by default on a fresh component. Confirm the tooltip text renders.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs
git commit -m "feat: surface depth test policy in Advanced Settings"
```

### Task 6: Build plumbing

**Files:**
- Modify: `Editor/Build/AmusePlatformFinishPlugin.cs:458-460` (component read) and the `AlphaSeparationPreparation` entry methods the pass calls
- Modify: `Editor/Build/AlphaSeparationPreparation.cs:654-659,768-770` (thread the parameter into both `EvaluateVerifiedEligibility` call sites)
- Modify: `Tests/Editor/Build/VerifiedLilToonTestSeams.cs:230,270` and `Tests/Editor/Build/VerifiedPoiyomiTestSeams.cs:44` only if their signatures need the passthrough

**Interfaces:**
- Consumes: Task 4's component property; Tasks 1 to 3's optional parameters
- Produces: `AlphaSeparationPreparation` entry methods carry `bool allowDepthTestChange = false` down to both eligibility call sites

- [ ] **Step 1: Read the component**

Beside the existing out-of-range read, add:

```csharp
            var allowDepthTestChange =
                optimizer == null || optimizer.AllowDepthTestChange;
```

- [ ] **Step 2: Thread the parameter**

Add `bool allowDepthTestChange = false` to the preparation entry method the pass calls, and pass it at both eligibility call sites. Existing preparation tests stay valid through the default.

- [ ] **Step 3: Verify**

Refresh Unity. Run the EditMode filter `AlphaSeparationPreparationTests`. Expected: all existing tests pass unchanged (the default preserves today's refusals). Record the count.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
  Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs
git commit -m "feat: thread depth test policy from component to eligibility"
```

### Task 7: Mixed-split guard

**Files:**
- Modify: the file that defines `AlphaSeparationSlotRefusal` (locate by name)
- Modify: `Editor/Build/AlphaSeparationPreparation.cs` at the point where a slot's split decision is known
- Test: `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: Task 1 to 3's `DepthTestDivergence` flag on both eligibility types
- Produces: `AlphaSeparationSlotRefusal.DepthTestDivergenceMixedSplit`; a flagged slot with a mixed plan refuses

- [ ] **Step 1: Add the refusal value**

Add to `AlphaSeparationSlotRefusal`, with the same named-cause guard the other values use:

```csharp
        DepthTestDivergenceMixedSplit,
```

- [ ] **Step 2: Write the failing test**

Reuse the harness this file already uses for transparent conversion tests. Build a wholly transparent-but-provable slot fixture with `_ZTest = 2`:

- With the policy on and a plan that is wholly opaque: the slot converts.
- With the policy on and a plan that is mixed (a fixture region that stays unproven): the slot refuses `DepthTestDivergenceMixedSplit`.
- With the policy off: the slot refuses `UnsupportedDepthComparison` as today.

Assert the refusal through the file's existing refusal-shape helper.

- [ ] **Step 3: Observe the failure**

Run the EditMode filter `AlphaSeparationPreparationTests`. Expected: the mixed-split assertion fails. Record the failure mode.

- [ ] **Step 4: Implement the guard**

At the point where the eligibility result and the slot's split outcome both exist:

```csharp
                        if (eligibility.DepthTestDivergence &&
                            <slot plan is a mixed split>)
                        {
                            return <the slot's named refusal path>(
                                AlphaSeparationSlotRefusal
                                    .DepthTestDivergenceMixedSplit);
                        }
```

`<slot plan is a mixed split>` is the planner outcome that decides between whole-slot replacement and an appended submesh; locate it by the existing minimum-coverage decision.

- [ ] **Step 5: Observe the pass**

Run the filter. Expected: all pass. Record the count.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs
git commit -m "feat: refuse mixed splits for depth-test divergent sources"
```

(Include the refusal-enum file in the same commit.)

### Task 8: Report the divergence

**Files:**
- Modify: `Editor/Build/AlphaSeparationPreparation.cs` (carry the flag onto the prepared slot record) and the slot status message construction (locate by name)

**Interfaces:**
- Consumes: the eligibility `DepthTestDivergence` flag
- Produces: one fixed sentence in the slot's success report

- [ ] **Step 1: Carry the flag**

Add `DepthTestDivergence` to the prepared slot record, set from the eligibility result.

- [ ] **Step 2: Append the sentence**

Where the slot's success message is built, append when the flag is set:

```
Some moved triangles now use the normal depth rule because their source material set a special one.
```

- [ ] **Step 3: Verify**

Run the EditMode filter `AlphaSeparationPreparationTests`. Expected: all pass. Extend the Task 7 wholly-opaque test to assert the sentence appears in the report. Record the count.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs
git commit -m "feat: report depth test divergence for converted slots"
```

### Task 9: Full validation

- [ ] **Step 1: Full product suite**

Refresh Unity. Run the full `Alrauna.Amuse.Tests.Editor` assembly. Expected: zero failures. Record the observed count.

- [ ] **Step 2: Research suite**

Run the full `Alrauna.Amuse.Research.Tests.Editor` assembly. Expected: zero failures. Record the observed count.

- [ ] **Step 3: Whitespace and diff check**

Run `git diff --check`. Expected: clean.

- [ ] **Step 4: Report**

Report the observed counts of every focused run and both full assemblies. State any deviation from this plan and the reason.
