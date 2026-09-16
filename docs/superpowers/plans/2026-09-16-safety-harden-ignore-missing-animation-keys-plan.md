# Safety Hardening for Missing Animation Keys and Components Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Safely ignore animation bindings that target missing components, incompatible renderer types, or out-of-range material slots so that valid renderers optimize without false closure refusals.

**Architecture:** Add component type compatibility checking (`IsCompatibleRendererType`) to `UnityAnimationEvidenceCapture` to filter out animation bindings targeting missing or incompatible component types. Default `ignoreOutOfRangeSlots` to true during evidence capture, guarded by `AlphaSeparationApply`'s existing slot collision check. Align structural checks and apply-pass curve rewriting with the same component compatibility filter.

**Tech Stack:** C# 9, Unity 2022.3, NUnit (Unity Test Framework EditMode), NDMF API.

**Spec:** `docs/superpowers/specs/2026-09-16-safety-harden-ignore-missing-animation-keys-design.md`

## Global Constraints

- C# against Unity 2022.3 APIs, editor-only.
- Every English text that a human reads uses ASD-STE100 Simplified Technical English: short active sentences, one idea per sentence, no semicolons, and no contractions.
- Never record private avatar, renderer, material, animation clip, or controller names.
- Never record machine names, user account names, home-directory paths, or ports. Machines and instances are named by role only.
- Captured evidence records (`CapturedAnimationEvidence`, `PreparedRendererSeparation`) must hold no live Unity object references.
- All unit tests use NUnit constraint model (`Assert.That(actual, Is.EqualTo(expected))`).
- Every behavior change requires RED/GREEN tests observed failing with a clear diagnostic message before implementation and passing afterward.

---

### Task 1: Component type compatibility predicate

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Produces: `bool UnityAnimationEvidenceCapture.IsCompatibleRendererType(string bindingTypeName, string rendererTypeName)`

- [ ] **Step 1: Write the failing tests for renderer type compatibility**

Add tests to `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`:

```csharp
        [Test]
        public void CompatibleRendererTypesAreAdmitted()
        {
            Assert.That(
                UnityAnimationEvidenceCapture.IsCompatibleRendererType(
                    typeof(SkinnedMeshRenderer).FullName,
                    typeof(SkinnedMeshRenderer).FullName),
                Is.True);
            Assert.That(
                UnityAnimationEvidenceCapture.IsCompatibleRendererType(
                    typeof(MeshRenderer).FullName,
                    typeof(MeshRenderer).FullName),
                Is.True);
            Assert.That(
                UnityAnimationEvidenceCapture.IsCompatibleRendererType(
                    typeof(Renderer).FullName,
                    typeof(SkinnedMeshRenderer).FullName),
                Is.True);
            Assert.That(
                UnityAnimationEvidenceCapture.IsCompatibleRendererType(
                    nameof(SkinnedMeshRenderer),
                    typeof(SkinnedMeshRenderer).FullName),
                Is.True);
        }

        [Test]
        public void IncompatibleRendererTypesAreRejected()
        {
            Assert.That(
                UnityAnimationEvidenceCapture.IsCompatibleRendererType(
                    typeof(MeshRenderer).FullName,
                    typeof(SkinnedMeshRenderer).FullName),
                Is.False);
            Assert.That(
                UnityAnimationEvidenceCapture.IsCompatibleRendererType(
                    typeof(SkinnedMeshRenderer).FullName,
                    typeof(MeshRenderer).FullName),
                Is.False);
            Assert.That(
                UnityAnimationEvidenceCapture.IsCompatibleRendererType(
                    typeof(Transform).FullName,
                    typeof(SkinnedMeshRenderer).FullName),
                Is.False);
            Assert.That(
                UnityAnimationEvidenceCapture.IsCompatibleRendererType(
                    "UnityEngine.Cloth",
                    typeof(SkinnedMeshRenderer).FullName),
                Is.False);
        }

        [Test]
        public void NullOrEmptyRendererTypeAdmitsAllBindings()
        {
            Assert.That(
                UnityAnimationEvidenceCapture.IsCompatibleRendererType(
                    typeof(MeshRenderer).FullName, null),
                Is.True);
            Assert.That(
                UnityAnimationEvidenceCapture.IsCompatibleRendererType(
                    null, typeof(SkinnedMeshRenderer).FullName),
                Is.True);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run via Unity Test Runner or compile check.
Expected: FAIL because `IsCompatibleRendererType` does not exist.

- [ ] **Step 3: Implement IsCompatibleRendererType**

Add to `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`:

```csharp
        internal static bool IsCompatibleRendererType(
            string bindingTypeName,
            string rendererTypeName)
        {
            if (string.IsNullOrEmpty(rendererTypeName) ||
                string.IsNullOrEmpty(bindingTypeName))
            {
                return true;
            }

            if (string.Equals(
                    bindingTypeName, rendererTypeName, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(
                    bindingTypeName, typeof(Renderer).FullName, StringComparison.Ordinal) ||
                string.Equals(
                    bindingTypeName, nameof(Renderer), StringComparison.Ordinal))
            {
                return true;
            }

            var bindingShort = ShortName(bindingTypeName);
            var rendererShort = ShortName(rendererTypeName);
            return string.Equals(
                       bindingShort, rendererShort, StringComparison.Ordinal) ||
                   string.Equals(
                       bindingShort, nameof(Renderer), StringComparison.Ordinal);
        }

        private static string ShortName(string typeName)
        {
            var lastDot = typeName.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < typeName.Length
                ? typeName.Substring(lastDot + 1)
                : typeName;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run `UnityAnimationEvidenceCaptureTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "Add component type compatibility predicate for animation evidence capture"
```

---

### Task 2: Filter object and float bindings during evidence capture

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Modifies: `UnityAnimationEvidenceCapture.Capture(..., string rendererTypeName = null)`
- Modifies: `UnityAnimationEvidenceCapture.CaptureWithAnimationIndex(..., string rendererTypeName = null)`
- Modifies: `UnityAnimationEvidenceCapture.ResolveProofRelevant(..., string rendererTypeName = null)`

- [ ] **Step 1: Write the failing tests for filtering incompatible component bindings**

Add tests to `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`:

```csharp
        [Test]
        public void MaterialSwapOnIncompatibleComponentTypeIsIgnored()
        {
            var current = Own(NewMaterial());
            var swapObservation = new LiveClipObservation(
                "cross_avatar_swap",
                false,
                Array.Empty<LiveFloatObservation>(),
                new[]
                {
                    // This curve targets MeshRenderer on the body path with a null material
                    new LiveObjectObservation(
                        AnalyzedRendererPath,
                        typeof(MeshRenderer).FullName,
                        "m_Materials.Array.data[0]",
                        new UnityEngine.Object[] { null }),
                });

            // When capturing for SkinnedMeshRenderer, the MeshRenderer curve must be ignored
            var evidence = UnityAnimationEvidenceCapture.CaptureObservedForTests(
                AnalyzedRendererPath,
                new[] { swapObservation },
                new[] { current },
                EmptyGraph(),
                rendererTypeName: typeof(SkinnedMeshRenderer).FullName);

            Assert.That(evidence.IsClosed, Is.True);
            Assert.That(
                evidence.ClosureFailure,
                Is.EqualTo(MaterialDependencyClosureFailure.None));
            Assert.That(evidence.AdmittedMaterials, Has.Count.EqualTo(1));
        }

        [Test]
        public void FloatBindingOnIncompatibleComponentTypeIsIrrelevant()
        {
            var binding = new CapturedFloatBinding(
                AnalyzedRendererPath,
                typeof(MeshRenderer).FullName,
                "material._Cutoff",
                true,
                new[] { 0.5f });

            var relevance = new MaterialEvidenceRequest(
                false, false,
                Array.Empty<string>(),
                new[] { "_Cutoff" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<TexturePropertyEvidenceRequest>());

            var resolution = UnityAnimationEvidenceCapture.ResolveProofRelevant(
                binding,
                AnalyzedRendererPath,
                relevance,
                out _,
                rendererTypeName: typeof(SkinnedMeshRenderer).FullName);

            Assert.That(
                resolution,
                Is.EqualTo(ProofRelevantBindingResolution.Irrelevant));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run tests.
Expected: FAIL because `CaptureObservedForTests` and `ResolveProofRelevant` do not take `rendererTypeName` or fail with `InvalidSwapValue`.

- [ ] **Step 3: Implement component filtering in evidence capture**

In `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`:
1. Update `AddressesAnalyzedRenderer`:
```csharp
        private static bool AddressesAnalyzedRenderer(
            string bindingPath,
            string bindingTypeName,
            string rendererPath,
            string rendererTypeName)
        {
            return string.Equals(
                       bindingPath, rendererPath, StringComparison.Ordinal) &&
                   IsCompatibleRendererType(bindingTypeName, rendererTypeName);
        }
```
2. Update `Capture`, `CaptureWithAnimationIndex`, `CaptureGraph`, `CaptureObserved`, and test entry points to accept `string rendererTypeName = null`.
3. In `CaptureObserved`, pass `binding.TypeName` and `rendererTypeName` to `AddressesAnalyzedRenderer`.
4. In `ResolveProofRelevant`, accept `string rendererTypeName = null` and check `AddressesAnalyzedRenderer(binding.Path, binding.TypeName, rendererPath, rendererTypeName)`.

- [ ] **Step 4: Run tests to verify they pass**

Run `UnityAnimationEvidenceCaptureTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "Filter animation bindings by component type during evidence capture"
```

---

### Task 3: Filter structural bindings in UnityRendererAlphaAnalysis

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`

**Interfaces:**
- Modifies: `UnityRendererAlphaAnalysis.StructuralRefusalFor(..., string rendererTypeName = null)`

- [ ] **Step 1: Write failing test for structural filtering by component type**

Add test to `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`:

```csharp
        [Test]
        public void MeshReplacementOnIncompatibleComponentTypeDoesNotRefuseRenderer()
        {
            var meshFilterObject = new CapturedObjectBinding(
                "Body",
                typeof(MeshFilter).FullName,
                "m_Mesh",
                Array.Empty<int>());

            var refusal = UnityRendererAlphaAnalysis.StructuralRefusalFor(
                Array.Empty<CapturedFloatBinding>(),
                new[] { meshFilterObject },
                "Body",
                rendererTypeName: typeof(SkinnedMeshRenderer).FullName);

            Assert.That(refusal, Is.EqualTo(RendererAnalysisRefusal.None));
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run test.
Expected: FAIL because `StructuralRefusalFor` returns `AnimatedMeshReplacement`.

- [ ] **Step 3: Update StructuralRefusalFor with component type check**

In `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`:
1. Add `string rendererTypeName = null` parameter to `StructuralRefusalFor` and `NamesStructuralProperty`.
2. In `NamesStructuralProperty`, use `UnityAnimationEvidenceCapture.IsCompatibleRendererType(binding.TypeName, rendererTypeName)`.

- [ ] **Step 4: Run test to verify it passes**

Run `UnityRendererAlphaAnalysisTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs
git commit -m "Filter structural animation bindings by component type"
```

---

### Task 4: Align separation records and apply pass curve rewriting

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Modifies: `PreparedRendererSeparation.RendererTypeName`
- Modifies: `AlphaSeparationPreparation.Prepare(..., string rendererTypeName)`

- [ ] **Step 1: Write failing test in AlphaSeparationApplyTests**

Add test to `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`:

Verify that `AlphaSeparationApply` ignores curves targeting an incompatible component type when discovering `targetBindings`.

```csharp
        [Test]
        public void ApplyPassDoesNotRewriteIncompatibleComponentBindings()
        {
            // Verify that an inert curve for MeshRenderer on a SkinnedMeshRenderer
            // is not rewritten or added to targetBindings
            ...
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run test.
Expected: FAIL.

- [ ] **Step 3: Implement RendererTypeName on PreparedRendererSeparation and filter in apply**

1. In `AlphaSeparationRecords.cs`, add `string rendererTypeName` to `PreparedRendererSeparation`.
2. In `AlphaSeparationPreparation.cs`, pass `rendererTypeName` when constructing `PreparedRendererSeparation`.
3. In `AlphaSeparationApply.cs`, when filtering `clip.GetObjectCurveBindings()`:
```csharp
if (!LiveAnimationObservation.TryParseMaterialSlotBinding(
        binding.propertyName, out var slotIndex) ||
    !string.Equals(
        binding.path, prepared.RendererPath, StringComparison.Ordinal) ||
    !UnityAnimationEvidenceCapture.IsCompatibleRendererType(
        binding.type.FullName, prepared.RendererTypeName))
{
    continue;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run `AlphaSeparationApplyTests`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs
git commit -m "Align separation records and apply pass with component type filtering"
```

---

### Task 5: Integration wiring in AmusePlatformFinishPlugin

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Connects `renderer.GetType().FullName` to evidence capture, runtime state resolution, and separation preparation.
- Enables `ignoreOutOfRangeSlots: true` by default in evidence capture.

- [ ] **Step 1: Write integration test for multi-avatar curve tolerance**

Add test to `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`:

Create an avatar fixture where the body renderer has:
- `SkinnedMeshRenderer` with 1 material slot.
- Animation clip with curve targeting `Body`, `type = typeof(MeshRenderer)` with `value = null`.
- Animation clip with curve targeting `Body`, `type = typeof(SkinnedMeshRenderer)` on slot 3 (out-of-range).
Verify that:
- AMUSE analyzes the renderer successfully (`AnalyzedRendererCount == 1`).
- `RendererRefusalCount(MaterialDependencyClosureFailed) == 0`.
- Opaque triangles are optimized.

- [ ] **Step 2: Run test to verify it fails**

Run test.
Expected: FAIL because `MaterialDependencyClosureFailed` is reported.

- [ ] **Step 3: Update AmusePlatformFinishPlugin**

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`:
1. Obtain `renderer.GetType().FullName` as `rendererTypeName`.
2. Pass `rendererTypeName` to `CaptureWithAnimationIndex`, `Capture`, `ResolveRuntimeStates`, and `AlphaSeparationPreparation.Prepare`.
3. Enable `ignoreOutOfRangeSlots: true` by default in `CaptureWithAnimationIndex`.
4. Pass `rendererTypeName` to `ResolveRuntimeStates` and `StructuralRefusalFor`.

- [ ] **Step 4: Run test to verify it passes**

Run test.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "Wire component type filtering and out-of-range slot tolerance into finish plugin"
```

---

### Task 6: Full suite regression run and verification

**Files:**
- Verify only (no new production files).

- [ ] **Step 1: Run full EditMode test suite for Alrauna.Amuse.Tests.Editor**

Run all tests in assembly `Alrauna.Amuse.Tests.Editor`.
Expected: 0 failures, 0 regressions.

- [ ] **Step 2: Inspect git diff**

Run `git diff --check` to ensure no trailing whitespace or format issues.

- [ ] **Step 3: Commit any final test adjustments**
