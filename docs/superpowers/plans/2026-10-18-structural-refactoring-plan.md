# Structural Refactoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the AMUSE repository to improve cohesion, separation of concerns, coupling, and readability across production modules while preserving all existing behaviors and test compatibility.

**Architecture:** Decompose 11 oversized source files into 14 focused components across Host, Analysis, Semantics, Build, and DryRun modules. The decomposition proceeds from foundational leaf data models up to orchestrators. It avoids speculative abstractions, partial classes, and preserves all public and internal interfaces through forwarding delegates and methods.

**Tech Stack:** C#, Unity 2022.3, NDMF 1.14.4, NUnit (EditMode).

**Spec:** `docs/superpowers/specs/2026-10-18-structural-refactoring-design.md`

## Global Constraints

- General file size target is 200 to 400 lines per file.
- No partial classes. Every extracted class or struct must be an independent non-partial type.
- No speculative abstractions. Do not introduce interfaces with one implementation or factory classes.
- Zero `UnityEditor` references in `Packages/com.alrauna.amuse/Editor/Analysis/`. The regex `\bUnityEditor\b` must not match any file content, including comments.
- Closed Semantics namespaces. Only `LilToon` and `Poiyomi` sub-namespaces are allowed.
- Public and internal API compatibility must be preserved. Keep original signatures and provide forwarding methods where callers or tests expect them.
- Preserve mutation order in `Build/AlphaSeparationApply.cs` (animation curves, then `sharedMesh`, then `sharedMaterials`). `AlphaSeparationApply.cs` remains intact.
- The text anchor `ObjectRegistry.RegisterReplacedObject` must remain in `Build/AlphaSeparationApply.cs`.
- Every English text that a human reads uses Simple English: short active sentences, one idea per sentence, no semicolons, and no contractions.
- Never record absolute paths, user account names, machine IDs, ports, or Unity MCP instance names. Name machines and instances by role only (`the dev editor instance`).

---

### Task 1: Extract `MaterialEvidenceRequest.cs` from `UnityMaterialEvidenceCapture.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/MaterialEvidenceRequest.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:11-430`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`

**Interfaces:**
- Produces: `TextureEvidenceKinds`, `TexturePropertyEvidenceRequest`, `MaterialEvidenceRequest`, `MaterialEvidenceCaptureInput` in namespace `Alrauna.Amuse.Editor.Host`.
- Consumes: Standard collections and `UnityEngine.Material`.

- [ ] **Step 1: Create `MaterialEvidenceRequest.cs`**
Extract `TextureEvidenceKinds`, `TexturePropertyEvidenceRequest`, `MaterialEvidenceRequest`, and `MaterialEvidenceCaptureInput` into `Packages/com.alrauna.amuse/Editor/Host/MaterialEvidenceRequest.cs`.

- [ ] **Step 2: Remove extracted types from `UnityMaterialEvidenceCapture.cs`**
Remove lines 11 to 430 in `UnityMaterialEvidenceCapture.cs`.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `UnityMaterialEvidenceCaptureTests` and verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Host/MaterialEvidenceRequest.cs* Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs
git commit -m "refactor: extract MaterialEvidenceRequest from UnityMaterialEvidenceCapture"
```

---

### Task 2: Extract `CapturedMaterialEvidence.cs` from `UnityMaterialEvidenceCapture.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/CapturedMaterialEvidence.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:431-1020`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`

**Interfaces:**
- Produces: `CapturedMaterialEvidence` (and nested property entry structs), `CapturedTextureEvidence` (internal sealed class), `CapturedTextureAssignment` in `Alrauna.Amuse.Editor.Host`.
- Consumes: `MaterialEvidenceRequest`, `AlphaPolicyBounds`, `AlphaMipChain`, `TextureSampling`.

- [ ] **Step 1: Create `CapturedMaterialEvidence.cs`**
Extract `CapturedTextureEvidence`, `CapturedTextureAssignment`, and `CapturedMaterialEvidence` into `Packages/com.alrauna.amuse/Editor/Host/CapturedMaterialEvidence.cs`. Keep `CapturedTextureEvidence` as an `internal sealed class`.

- [ ] **Step 2: Remove extracted types from `UnityMaterialEvidenceCapture.cs`**
Remove extracted models from `UnityMaterialEvidenceCapture.cs`. `UnityMaterialEvidenceCapture.cs` now contains only the capture engine.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `UnityMaterialEvidenceCaptureTests` and verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Host/CapturedMaterialEvidence.cs* Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs
git commit -m "refactor: extract CapturedMaterialEvidence from UnityMaterialEvidenceCapture"
```

---

### Task 3: Extract `TriangleAlphaModels.cs` from `TriangleAlphaClassifier.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaModels.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:10-281`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`

**Interfaces:**
- Produces: `TriangleAlphaOutcome`, `TriangleAlphaInput`, `AlphaTextureData` in `Alrauna.Amuse.Editor.Analysis`.
- Constraint: Zero `UnityEditor` references. Do not include the string `UnityEditor` anywhere in the file.

- [ ] **Step 1: Create `TriangleAlphaModels.cs`**
Extract `TriangleAlphaOutcome`, `TriangleAlphaInput`, and `AlphaTextureData` into `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaModels.cs`.
Ensure no `UnityEditor` token appears in comments or code.

- [ ] **Step 2: Remove extracted models from `TriangleAlphaClassifier.cs`**
Remove lines 10 to 281 in `TriangleAlphaClassifier.cs`. Lines 283 onward must remain untouched.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `TriangleAlphaClassifierTests` and `UnityAlphaFieldEvidenceTests.AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace`. Verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaModels.cs* Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs
git commit -m "refactor: extract TriangleAlphaModels from TriangleAlphaClassifier"
```

---

### Task 4: Extract `ExactRational.cs` from `ExactUvGeometry.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Analysis/ExactRational.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs:7-112`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/ExactUvGeometryTests.cs`

**Interfaces:**
- Produces: `ExactDyadic`, `ExactRational` in `Alrauna.Amuse.Editor.Analysis`.
- Constraint: Zero `UnityEditor` references.
- Compatibility: `ExactUvGeometry.FloorMod` must remain declared on `ExactUvGeometry`.

- [ ] **Step 1: Create `ExactRational.cs`**
Extract `ExactDyadic` and `ExactRational` into `Packages/com.alrauna.amuse/Editor/Analysis/ExactRational.cs`. Ensure no `UnityEditor` token appears in the file.

- [ ] **Step 2: Remove extracted structs from `ExactUvGeometry.cs`**
Remove `ExactDyadic` and `ExactRational` definitions from `ExactUvGeometry.cs`. Keep `FloorMod` on `ExactUvGeometry`.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `ExactUvGeometryTests` and `UnityAlphaFieldEvidenceTests.AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace`. Verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/ExactRational.cs* Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs
git commit -m "refactor: extract ExactRational from ExactUvGeometry"
```

---

### Task 5: Extract `RendererAlphaAnalysis.cs` from `UnityRendererAlphaAnalysis.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/RendererAlphaAnalysis.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:18-203`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`

**Interfaces:**
- Produces: `RendererAnalysisRefusal`, `SubmeshAlphaAnalysis`, `RendererAlphaAnalysis` in `Alrauna.Amuse.Editor.Host`.
- Invariant: Retain lines 205 to 215 in `UnityRendererAlphaAnalysis.cs` (`CapturedAlphaMaterialSemanticsResolver` and related delegates).

- [ ] **Step 1: Create `RendererAlphaAnalysis.cs`**
Extract `RendererAnalysisRefusal`, `SubmeshAlphaAnalysis`, and `RendererAlphaAnalysis` into `Packages/com.alrauna.amuse/Editor/Host/RendererAlphaAnalysis.cs`.

- [ ] **Step 2: Remove lines 18 to 203 from `UnityRendererAlphaAnalysis.cs`**
Remove lines 18 to 203. Keep delegates on lines 205 to 215 intact in `UnityRendererAlphaAnalysis.cs`.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `UnityRendererAlphaAnalysisTests` and verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Host/RendererAlphaAnalysis.cs* Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs
git commit -m "refactor: extract RendererAlphaAnalysis from UnityRendererAlphaAnalysis"
```

---

### Task 6: Extract `AlphaResolution.cs` from `AlphaSemanticsResolver.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaResolution.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:12-503`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`

**Interfaces:**
- Produces: `AlphaResolutionFailure`, `AlphaFieldProvider`, `AlphaResolution` in `Alrauna.Amuse.Editor.Analysis`.
- Constraint: Zero `UnityEditor` references.

- [ ] **Step 1: Create `AlphaResolution.cs`**
Extract `AlphaResolutionFailure`, `AlphaFieldProvider`, and `AlphaResolution` into `Packages/com.alrauna.amuse/Editor/Analysis/AlphaResolution.cs`.
Ensure no `UnityEditor` token appears in comments or code.

- [ ] **Step 2: Remove extracted types from `AlphaSemanticsResolver.cs`**
Remove lines 12 to 503 from `AlphaSemanticsResolver.cs`.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `AlphaSemanticsResolverTests` and `UnityAlphaFieldEvidenceTests.AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace`. Verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AlphaResolution.cs* Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs
git commit -m "refactor: extract AlphaResolution from AlphaSemanticsResolver"
```

---

### Task 7: Extract `AnimatedPropertyAdmission.cs` from `AdmittedMaterialStates.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Analysis/AnimatedPropertyAdmission.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:10-18,452-1076`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Produces: `AdmittedPropertyOutcome`, `AnimatedPropertyGroup`, and static admission methods in `Alrauna.Amuse.Editor.Analysis`.
- Compatibility: Forwarding static methods on `AdmittedMaterialStates` for `TryAdmitDerivedEvidence`, `AdmitScalar`, `AdmitColor`, and `AdmitVector`.
- Constraint: Zero `UnityEditor` references.

- [ ] **Step 1: Create `AnimatedPropertyAdmission.cs`**
Extract `AdmittedPropertyOutcome` (from lines 10-18) and admission calculation logic (from lines 452-1076) into `Packages/com.alrauna.amuse/Editor/Analysis/AnimatedPropertyAdmission.cs`.
Ensure no `UnityEditor` token appears in the file.

- [ ] **Step 2: Add forwarding methods to `AdmittedMaterialStates.cs` and remove moved logic**
Remove `AdmittedPropertyOutcome` from lines 10-18 of `AdmittedMaterialStates.cs`.
Add forwarding methods on `AdmittedMaterialStates` for `TryAdmitDerivedEvidence`, `AdmitScalar`, `AdmitColor`, and `AdmitVector` that delegate to `AnimatedPropertyAdmission`. Remove the moved calculation methods.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `AdmittedMaterialStatesTests` and `UnityAlphaFieldEvidenceTests.AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace`. Verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AnimatedPropertyAdmission.cs* Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs
git commit -m "refactor: extract AnimatedPropertyAdmission from AdmittedMaterialStates"
```

---

### Task 8: Extract `AnimationBindingResolution.cs` from `UnityAnimationEvidenceCapture.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/AnimationBindingResolution.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs:12-53,866-1233`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Produces: `AnimatedPropertyKind`, `ProofRelevantBindingResolution`, `AnimatedPropertyRef`, and binding resolution methods in `Alrauna.Amuse.Editor.Host`.
- Compatibility: Retain forwarding delegate instances (`ProofRelevantResolver`, `MaterialSlotBindingParser`, `UnrecognizedObjectBinding`) and methods on `UnityAnimationEvidenceCapture`. Retain lines 55 to 114 (`AlphaMaterialRequestSelector`, `ClosedAlphaMaterialCapturer`) in `UnityAnimationEvidenceCapture.cs`.

- [ ] **Step 1: Create `AnimationBindingResolution.cs`**
Extract `AnimatedPropertyKind`, `ProofRelevantBindingResolution`, `AnimatedPropertyRef`, and string parsing methods into `Packages/com.alrauna.amuse/Editor/Host/AnimationBindingResolution.cs`.

- [ ] **Step 2: Add forwarding delegates and methods on `UnityAnimationEvidenceCapture.cs`**
In `UnityAnimationEvidenceCapture.cs`, delegate `ProofRelevantResolver`, `MaterialSlotBindingParser`, and `UnrecognizedObjectBinding` point to `AnimationBindingResolution` methods. Add forwarding methods for `ResolveProofRelevant` and `TryParseMaterialSlotBindingFor`. Remove moved logic.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `UnityAnimationEvidenceCaptureTests` and verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Host/AnimationBindingResolution.cs* Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs
git commit -m "refactor: extract AnimationBindingResolution from UnityAnimationEvidenceCapture"
```

---

### Task 9: Extract `UnityRendererGeometryExtractor.cs` from `UnityRendererAlphaAnalysis.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererGeometryExtractor.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:312-605`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`

**Interfaces:**
- Produces: `UnityRendererGeometryExtractor` static class (`CaptureGeometry`, `HostStructuralRefusalFor`, `MaterialSlotMappingRefusalFor`) in `Alrauna.Amuse.Editor.Host`.
- Compatibility: `UnityRendererAlphaAnalysis.CaptureGeometry` and `UnityRendererAlphaAnalysis.HostStructuralRefusalFor` forward to `UnityRendererGeometryExtractor`.

- [ ] **Step 1: Create `UnityRendererGeometryExtractor.cs`**
Extract `CaptureGeometry`, `HostStructuralRefusalFor`, and `MaterialSlotMappingRefusalFor` into `Packages/com.alrauna.amuse/Editor/Host/UnityRendererGeometryExtractor.cs`.

- [ ] **Step 2: Add forwarding methods on `UnityRendererAlphaAnalysis.cs`**
Add `CaptureGeometry` and `HostStructuralRefusalFor` forwarding methods on `UnityRendererAlphaAnalysis` and remove moved method bodies.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `UnityRendererAlphaAnalysisTests` and verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityRendererGeometryExtractor.cs* Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs
git commit -m "refactor: extract UnityRendererGeometryExtractor from UnityRendererAlphaAnalysis"
```

---

### Task 10: Extract `PoiyomiSourceAttestation.cs` from `PoiyomiMaterialSemantics.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:25-55,2482-2720,3023-3061`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiAttestationTests.cs`

**Interfaces:**
- Produces: `PoiyomiSourceAttestation` static class and `PoiyomiSourceEvidence` readonly struct in `Alrauna.Amuse.Editor.Semantics.Poiyomi`.
- Compatibility: Forwarding constants (`CanonicalShaderGuid`, `CanonicalNormalizedSourceHash`, `TwoPassCanonicalShaderGuid`, `TwoPassCanonicalNormalizedSourceHash`, `PoiyomiPackageVersion`, `PoiyomiPackageName`, `PoiyomiToonShaderName`, `PoiyomiTwoPassShaderName`) and forwarding methods (`ComputeNormalizedSourceHash`, `GatherSourceEvidence`, `GatherAlphaSourceEvidence`, `TryVerifyPoiyomiIdentity`, `ClearSourceHashMemo`, `TryReadNormalizedSourceHashCached`) on `PoiyomiMaterialSemantics`.

- [ ] **Step 1: Create `PoiyomiSourceAttestation.cs`**
Extract shader GUIDs, package version constants, normalized SHA-256 hashes, `PoiyomiSourceEvidence`, and source evidence methods into `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs`.

- [ ] **Step 2: Add forwarding constants and methods on `PoiyomiMaterialSemantics.cs`**
Add forwarding constants, properties, and methods on `PoiyomiMaterialSemantics` that reference `PoiyomiSourceAttestation`. Remove moved logic.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `PoiyomiAttestationTests` and verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs* Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs
git commit -m "refactor: extract PoiyomiSourceAttestation from PoiyomiMaterialSemantics"
```

---

### Task 11: Extract `PoiyomiAlphaInterpreter.cs` from `PoiyomiMaterialSemantics.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:320-355,862-2160,2740-2810`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`

**Interfaces:**
- Produces: `PoiyomiAlphaInterpreter` static class in `Alrauna.Amuse.Editor.Semantics.Poiyomi`.
- Compatibility: Forwarding properties (`AlphaEvidenceRequest`, `PlainAlphaEvidenceRequest`, `TwoPassAlphaEvidenceRequest`, `PlainTwoPassAlphaEvidenceRequest`) and forwarding methods (`AlphaPredicateRequestFor`, `InterpretVerifiedAlpha`, `InterpretVerifiedTwoPassAlpha`, `FeatureLabelFor`) on `PoiyomiMaterialSemantics`.

- [ ] **Step 1: Create `PoiyomiAlphaInterpreter.cs`**
Extract alpha evidence requests, feature gate arrays, `FeatureLabelFor`, and alpha interpretation methods into `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs`.

- [ ] **Step 2: Add forwarding properties and methods on `PoiyomiMaterialSemantics.cs`**
Add forwarding properties and methods on `PoiyomiMaterialSemantics` delegating to `PoiyomiAlphaInterpreter`. Remove moved logic.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `PoiyomiMaterialSemanticsTests` and `PoiyomiAlphaMaskTests`. Verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs* Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs
git commit -m "refactor: extract PoiyomiAlphaInterpreter from PoiyomiMaterialSemantics"
```

---

### Task 12: Extract `AmusePlatformFinishPass.cs` from `AmusePlatformFinishPlugin.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:322-1704`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Produces: `AmusePlatformFinishPass` static class (`Execute`) in `Alrauna.Amuse.Editor.Build`.
- Compatibility: Reflected helper methods (`OpaquePercentFrom`, `PolygonClampPercentFrom`, `DensityCapFromCoveragePercent`) remain declared on `AmusePlatformFinishPass`.
- Invariant: `ActiveBuildTarget()` and delegate `ActiveBuildTargetForTests` remain declared on `AmusePlatformFinishPlugin`.

- [ ] **Step 1: Create `AmusePlatformFinishPass.cs`**
Extract `AmusePlatformFinishPass` (and its clamp helper methods) into `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs`.

- [ ] **Step 2: Clean up `AmusePlatformFinishPlugin.cs`**
Remove `AmusePlatformFinishPass` from `AmusePlatformFinishPlugin.cs`. `AmusePlatformFinishPlugin.cs` now retains `AmusePlatformFinishState`, plugin configuration, and `ActiveBuildTarget()`.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `AmusePlatformFinishPluginTests` and verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs* Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs
git commit -m "refactor: extract AmusePlatformFinishPass from AmusePlatformFinishPlugin"
```

---

### Task 13: Extract `AlphaSeparationMaterialConverter.cs` from `AlphaSeparationPreparation.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationMaterialConverter.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Produces: `AlphaSeparationMaterialConverter` static class in `Alrauna.Amuse.Editor.Build`.
- Compatibility: Forwarding methods for `TryMapLilToonOutcome`, `TryMapPoiyomiOutcome`, and `ConvertAdmittedMaterial` on `AlphaSeparationPreparation`.

- [ ] **Step 1: Create `AlphaSeparationMaterialConverter.cs`**
Extract material clone preparation, conversion recipe dispatch, and outcome mapping methods into `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationMaterialConverter.cs`.

- [ ] **Step 2: Add forwarding methods on `AlphaSeparationPreparation.cs`**
Add forwarding methods on `AlphaSeparationPreparation` delegating to `AlphaSeparationMaterialConverter`. Remove moved logic.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `AlphaSeparationPreparationTests` and verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationMaterialConverter.cs* Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs
git commit -m "refactor: extract AlphaSeparationMaterialConverter from AlphaSeparationPreparation"
```

---

### Task 14: Extract `DryRunRowBuilder.cs` from `AlphaSeparationDryRun.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/DryRun/DryRunRowBuilder.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Produces: `DryRunRowBuilder` static class (`BuildRows`, `BuildResolvedRefusalRows`) in `Alrauna.Amuse.Editor.DryRun`.
- Compatibility: Retain reflected fields (`_cts`, `_inFlightTask`) and reflected method (`BuildPolicyFingerprint`) on `AlphaSeparationDryRun`.

- [ ] **Step 1: Create `DryRunRowBuilder.cs`**
Extract transformation row creation (`BuildRows`, `BuildResolvedRefusalRows`) and formatting helpers into `Packages/com.alrauna.amuse/Editor/DryRun/DryRunRowBuilder.cs`.

- [ ] **Step 2: Call `DryRunRowBuilder` from `AlphaSeparationDryRun.cs`**
Replace moved row formatting logic in `AlphaSeparationDryRun.cs` with calls to `DryRunRowBuilder`.

- [ ] **Step 3: Refresh asset database and run tests**
Refresh the asset database in the dev editor instance. Run `AlphaSeparationDryRunTests` and `DryRunCacheTests`. Verify all tests pass.

- [ ] **Step 4: Commit**
```bash
git add Packages/com.alrauna.amuse/Editor/DryRun/DryRunRowBuilder.cs* Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs
git commit -m "refactor: extract DryRunRowBuilder from AlphaSeparationDryRun"
```
