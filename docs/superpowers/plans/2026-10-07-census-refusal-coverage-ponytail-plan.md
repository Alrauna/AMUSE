# Census Refusal Coverage Ponytail Cuts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the eleven complexity cuts from the ponytail review of branch `feat/census-refusal-coverage` to remove 91 lines without behavior change.

**Architecture:** Behavior-preserving simplification across Host evidence capture, Semantics alpha resolution, and Build lifecycle passes. Redundant loops, single-caller helpers, duplicate branches, and temporary allocations are replaced with concise direct expressions.

**Tech Stack:** C# (Unity 2022.3), .NET Standard 2.1, NUnit (Unity Test Framework EditMode).

**Spec:** `docs/superpowers/specs/2026-10-07-census-refusal-coverage-ponytail-design.md`

## Global Constraints

- Preserve all existing behavior. No refusal value, schema value, report format, or semantic result changes.
- Never weaken a test assertion. All existing assertions remain intact.
- Every English sentence uses ASD-STE100 simple English: short active sentences, one idea per sentence, no semicolons, and no contractions.
- Never record absolute or machine-specific paths in documents, comments, or commit messages. Use `<repo-root>`-relative paths.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names. Name machines and instances by role only.
- Never expose private Census names, paths, GUIDs, per-avatar/per-renderer rows, or fingerprint-like identifiers.
- A filtered test run that reports 0 tests is a failure. Record observed test counts for every run.
- Unity MCP safety: verify `Application.dataPath` matches `<repo-root>/Assets` before any Unity MCP operation.
- Commit steps run only when explicitly authorized by the user.

---

### Task 1: Host and Animation Evidence Capture Simplifications

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs:717-739`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs:2149-2172`

**Interfaces:**
- Consumes: `ClosedAlphaCaptureOutcome` properties `Captured` and `UnattestedOrdinals`.
- Produces: Simplified loop in `UnityAnimationEvidenceCapture.CaptureGraphThroughSeams` and direct LINQ filtering in `UnityAnimationEvidenceCaptureTests.CapturerFailingOrdinals`.

- [ ] **Step 1: Record baseline test pass**

Run the EditMode tests for `UnityAnimationEvidenceCaptureTests` on the dev editor instance.
Expected: all tests pass. Record the observed count.

- [ ] **Step 2: Combine batch outcome loops in UnityAnimationEvidenceCapture**

In `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`, replace lines 717 to 739:

```csharp
                var failedOrdinals = new HashSet<int>(batchOutcome.UnattestedOrdinals);
                var survivorOrdinal = 0;
                for (var index = 0; index < attestedIndices.Count; index++)
                {
                    var admittedIndex = attestedIndices[index];
                    if (failedOrdinals.Contains(index))
                    {
                        var lockedRefusal = lockedRefusalCheck?.Invoke(
                            admitted[admittedIndex]) ?? RendererAnalysisRefusal.None;
                        capturedByIndex[admittedIndex] =
                            UnityMaterialSemantics.UnattestedMaterial(
                                lockedRefusal,
                                admitted[admittedIndex],
                                resolveRegisteredSource);
                    }
                    else
                    {
                        capturedByIndex[admittedIndex] =
                            batchOutcome.Captured[survivorOrdinal++];
                    }
                }
```

- [ ] **Step 3: Simplify survivor filtering in UnityAnimationEvidenceCaptureTests**

In `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`, replace lines 2149 to 2172:

```csharp
                var failedSet = new HashSet<int>(failedOrdinals);
                var survivors = batch.Captured
                    .Where((_, index) => !failedSet.Contains(index))
                    .ToList();
```

- [ ] **Step 4: Verify tests pass**

Run the EditMode tests for `UnityAnimationEvidenceCaptureTests` on the dev editor instance.
Expected: all tests pass with the same observed count as the baseline.

- [ ] **Step 5: Commit task changes**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs
git add Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "refactor(host): simplify batch capture outcome loops and test filtering"
```

---

### Task 2: Material Semantics and Poiyomi Simplifications

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:304-343`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1504-1512,2098-2107`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOutlineAlphaSemanticsTests.cs:21-27,39`

**Interfaces:**
- Consumes: `TryCaptureClosedAlphaMaterialsTransferred` with `System.Array.Empty<string>()`.
- Produces: Delegated `TryCaptureClosedAlphaMaterials`, merged outline constant branches, factored mapped texture construction, and inlined assertion helper.

- [ ] **Step 1: Record baseline test pass**

Run EditMode tests for `UnityMaterialSemanticsTests`, `PoiyomiMaterialSemanticsTests`, and `PoiyomiOutlineAlphaSemanticsTests` on the dev editor instance.
Expected: all tests pass. Record the observed count.

- [ ] **Step 2: Delegate plain capture in UnityMaterialSemantics**

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, replace the body of `TryCaptureClosedAlphaMaterials` (lines 315 to 343) with:

```csharp
            return TryCaptureClosedAlphaMaterialsTransferred(
                materials,
                families,
                request,
                bounds,
                System.Array.Empty<string>(),
                out outcome,
                resolveRegisteredSource);
```

- [ ] **Step 3: Merge outline constant branches in PoiyomiMaterialSemantics**

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`, replace lines 1504 to 1512 with:

```csharp
            if (!outlineTexture.IsAssigned ||
                (outlineTexture.Texture != null &&
                 outlineTexture.Texture.SampledAlphaIsProvenOne))
            {
                outlineFactor = ScalarSemanticValue.Constant(1f);
            }
```

- [ ] **Step 4: Factor MappedTexture construction in PoiyomiMaterialSemantics**

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`, replace lines 2098 to 2107 with:

```csharp
                var factor = ScalarSemanticValue.MappedTexture(
                    maskSample, TextureChannel.Red, mapped);
                if (replace)
                {
                    replacement = factor;
                }
                else
                {
                    multiplier = factor;
                }
```

- [ ] **Step 5: Inline assertion helper in PoiyomiOutlineAlphaSemanticsTests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOutlineAlphaSemanticsTests.cs`, delete lines 21 to 27 (`AssertUnsupportedOutputIsAbsent`).
In line 39, replace the call `AssertUnsupportedOutputIsAbsent(result)` with:

```csharp
            Assert.That(
                result.Diagnostics.Any(d => d.Output == PoiyomiSemanticOutput.Alpha),
                Is.False,
                "Alpha output must not emit a diagnostic.");
```

- [ ] **Step 6: Verify tests pass**

Run EditMode tests for `UnityMaterialSemanticsTests`, `PoiyomiMaterialSemanticsTests`, and `PoiyomiOutlineAlphaSemanticsTests` on the dev editor instance.
Expected: all tests pass with the same observed count as the baseline.

- [ ] **Step 7: Commit task changes**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs
git add Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOutlineAlphaSemanticsTests.cs
git commit -m "refactor(semantics): delegate plain capture overload and remove duplicate branches"
```

---

### Task 3: Build Lifecycle and Identity Simplifications

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs:238-253`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:819-821,841-843,1398-1407`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationSplitTests.cs:723-726`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:5154-5158`

**Interfaces:**
- Consumes: `GrantedAwareOriginalAttestation`, `grantedShaderNames`, and `SlotClosureFailures`.
- Produces: Inlined helpers, simplified parameter passing, and stdlib array generation.

- [ ] **Step 1: Record baseline test pass**

Run EditMode tests for `LockedMaterialIdentityTests`, `AlphaSeparationSplitTests`, and `AmusePlatformFinishPluginTests` on the dev editor instance.
Expected: all tests pass. Record the observed count.

- [ ] **Step 2: Inline RecordedListedOriginalName in LockedMaterialIdentity**

In `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs`, delete lines 246 to 253 (`RecordedListedOriginalName`).
In `GrantedAwareOriginalAttestation`, replace lines 238 to 244 with:

```csharp
            return material =>
            {
                if (baseAttestation != null
                    ? baseAttestation(material)
                    : OriginalShaderAttested(material))
                {
                    return true;
                }

                var original = RecordedOriginalShaderName(material);
                return !string.IsNullOrEmpty(original) &&
                       grantedShaderNames.Contains(original);
            };
```

- [ ] **Step 3: Simplify parameter and inline helper in AmusePlatformFinishPlugin**

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, replace lines 819 to 821 with:

```csharp
                            allowDepthTestChange,
                            grantedShaderNames);
```

In line 841, replace:

```csharp
                        closureWaySentence:
                        refusal ==
                        RendererAnalysisRefusal
                            .MaterialDependencyClosureFailed
                            ? AmuseReports.ClosureFailureSentence(
                                ClosureFailureWayFor(evidence))
                            : null);
```

with:

```csharp
                        closureWaySentence:
                        refusal ==
                        RendererAnalysisRefusal
                            .MaterialDependencyClosureFailed &&
                        evidence.SlotClosureFailures.Count > 0
                            ? AmuseReports.ClosureFailureSentence(
                                evidence.SlotClosureFailures[0].Failure)
                            : null);
```

Delete lines 1398 to 1407 (`ClosureFailureWayFor`).

- [ ] **Step 4: Remove manual array zeroing in AlphaSeparationSplitTests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationSplitTests.cs`, delete lines 724 to 727:

```csharp
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(0, 0, 0, 0);
            }
```

- [ ] **Step 5: Use Enumerable.Range in AmusePlatformFinishPluginTests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`, replace lines 5154 to 5158:

```csharp
            var ordinals = System.Linq.Enumerable.Range(0, materials.Count).ToArray();
```

- [ ] **Step 6: Verify tests pass**

Run EditMode tests for `LockedMaterialIdentityTests`, `AlphaSeparationSplitTests`, and `AmusePlatformFinishPluginTests` on the dev editor instance.
Expected: all tests pass with the same observed count as the baseline.

- [ ] **Step 7: Commit task changes**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationSplitTests.cs
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "refactor(build): inline single-caller helpers and simplify parameter checks"
```

---

### Task 4: Full Suite Validation and Identifier Sweep

**Files:**
- Inspect: All repository files modified on branch `feat/census-refusal-coverage`.

- [ ] **Step 1: Run full product EditMode test suite**

Run all tests in assembly `Alrauna.Amuse.Tests.Editor` on the dev editor instance.
Expected: all tests pass. Record the observed count.

- [ ] **Step 2: Run whitespace and format check**

Run `git diff --check` to verify no whitespace errors exist.
Expected: clean output with no whitespace issues.

- [ ] **Step 3: Run identifier and privacy sweep**

Sweep all modified files for forbidden identifiers:
- Hexadecimal hashes preceded by `@`.
- Machine-specific or absolute drive paths.
- User home directory paths.
- Four-digit port numbers.
- Private avatar, renderer, or asset names.
- Semicolons or contractions in documentation files.
Expected: zero hits.
