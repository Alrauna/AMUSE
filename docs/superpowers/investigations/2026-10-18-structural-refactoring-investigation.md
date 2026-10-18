# Structural Refactoring Investigation

Status as of 2026-10-18.

## Summary

This investigation reviews all C# files in the AMUSE repository.
The review identifies candidate files and classes for structural refactoring.
The review uses 200 to 400 lines per file as a general target.
A file with more than 500 lines reaches the soft review threshold.
A file with 700 or more lines gives a strong refactoring signal.
The investigation prioritizes cohesion, separation of concerns, readability, and maintainability.
Files with justified size remain intact.
Justified files include static attestation tables, localization string tables, and single cohesive mathematical algorithms.

The baseline test run in the dev editor instance passed completely.
The observed count is 3152 passed tests, 0 failed tests, and 4 skipped tests.

Three adversarial reviews examined this investigation.
The reviews evaluated architecture, safety, latent bugs, and Ponytail minimalism.
The reviews confirmed that micro-file splitting causes unnecessary file proliferation and Unity GUID churn.
The revised plan applies Ponytail minimalism.
It reduces file proliferation by combining related data contracts and keeping atomic mutation sequences intact.

## Repository Inventory and Line Count Distribution

The repository contains two packages:
1. `Packages/com.alrauna.amuse` (production code and product tests)
2. `Packages/com.alrauna.amuse.research` (research tooling and tests)

All 14 C# files in `Packages/com.alrauna.amuse.research/Editor/` contain 280 lines or fewer.
No files in the research package require refactoring.

In `Packages/com.alrauna.amuse/Editor/` and `Runtime/`, 28 files exceed 500 lines.
22 files exceed 700 lines.

### Production Files Above 500 Lines

| File Path | Line Count | Classification | Final Recommendation |
| :--- | :--- | :--- | :--- |
| `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` | 3152 | Mixed responsibilities | Refactor |
| `Editor/Semantics/LilToon/LilToonSourceAttestation.cs` | 2923 | Static attestation tables | Leave intact |
| `Editor/Analysis/TriangleAlphaClassifier.cs` | 2290 | Proof core plus models | Refactor models only |
| `Editor/Host/UnityMaterialEvidenceCapture.cs` | 1776 | Mixed capture and models | Refactor |
| `Editor/DryRun/AlphaSeparationDryRun.cs` | 1743 | Mixed pipeline and preview | Refactor |
| `Editor/Build/AmusePlatformFinishPlugin.cs` | 1704 | Mixed registration and pass | Refactor |
| `Editor/Build/AlphaSeparationPreparation.cs` | 1334 | Mixed planning and conversion | Refactor |
| `Editor/AmuseAvatarOptimizerEditor.cs` | 1327 | IMGUI custom inspector | Leave intact |
| `Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` | 1321 | Channel semantics | Leave intact |
| `Editor/Semantics/MaterialSemantics.cs` | 1317 | Domain model vocabulary | Leave intact |
| `Editor/Build/AlphaSeparationApply.cs` | 1243 | Atomic mutation pass | Leave intact |
| `Editor/Host/UnityAnimationEvidenceCapture.cs` | 1233 | Mixed capture and parsing | Refactor |
| `Editor/Semantics/UnityMaterialSemantics.cs` | 1226 | Orchestration facade | Leave intact |
| `Editor/Host/UnityRendererAlphaAnalysis.cs` | 1224 | Mixed geometry and refusal | Refactor |
| `Editor/Host/UnityAlphaFieldEvidence.cs` | 1099 | GPU blit engine | Leave intact |
| `Editor/Analysis/AdmittedMaterialStates.cs` | 1076 | Mixed slot and property math | Refactor |
| `Editor/Analysis/AlphaSemanticsResolver.cs` | 975 | Mixed AST compiler and proof | Refactor |
| `Editor/Build/AmuseReportStrings.cs` | 950 | Static string tables | Leave intact |
| `Editor/Analysis/ExactUvGeometry.cs` | 946 | Geometry plus rational numbers | Refactor |
| `Editor/Build/AmuseReports.cs` | 841 | NDMF report emitter | Leave intact |
| `Editor/Semantics/LilToon/LilToonMultiModeGate.cs` | 791 | Static keyword derivations | Leave intact |
| `Editor/Analysis/RuntimeStateResolution.cs` | 752 | Pipeline plus setting clamps | Leave intact |
| `Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs` | 628 | Cohesive conversion recipe | Leave intact |
| `Editor/Build/HostLifecycleCapability.cs` | 601 | Cohesive version checks | Leave intact |
| `Editor/Build/TransientUnlockWindowClose.cs` | 595 | Cohesive lifecycle pass | Leave intact |
| `Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs` | 588 | Cohesive alpha proof | Leave intact |
| `Editor/Build/LockedMaterialReconstruction.cs` | 574 | Cohesive serialization repair | Leave intact |
| `Editor/Build/TransientUnlockSwapIn.cs` | 562 | Cohesive lifecycle pass | Leave intact |

---

## Detailed Subsystem Evaluations

### 1. Host Subsystem (`Packages/com.alrauna.amuse/Editor/Host/`)

#### `UnityMaterialEvidenceCapture.cs` (1776 lines)
- **Problem:** The file bundles request data contracts, immutable evidence data contracts, and the GPU/CPU capture engine. Over 50 files in downstream modules depend on the data contracts.
- **Decision:** Refactor the file. Extract data models into two focused model files. Keep the capture engine in the original file.
- **Decomposition:**
  - `MaterialEvidenceRequest.cs` (~410 lines): Contains `TextureEvidenceKinds`, `TexturePropertyEvidenceRequest`, `MaterialEvidenceRequest`, and `MaterialEvidenceCaptureInput`.
  - `CapturedMaterialEvidence.cs` (~590 lines): Contains `CapturedMaterialEvidence`, property entry structs, `CapturedTextureEvidence`, and `CapturedTextureAssignment`.
  - `UnityMaterialEvidenceCapture.cs` (~755 lines): Retains the capture execution engine.
- **Impact:** Zero breaking API changes. All types stay in `Alrauna.Amuse.Editor.Host`.

#### `UnityAnimationEvidenceCapture.cs` (1233 lines)
- **Problem:** The file combines graph traversal with animation curve property path parsing. Callers in `Analysis/`, `Build/`, and `DryRun/` call path parsing without using graph capture.
- **Decision:** Refactor the file. Separate property path parsing from graph capture.
- **Decomposition:**
  - `AnimationBindingResolution.cs` (~470 lines): Contains `AnimatedPropertyKind`, `ProofRelevantBindingResolution`, `AnimatedPropertyRef`, and parsing methods (`ResolveProofRelevant`, `TryParseMaterialSlotBindingFor`).
  - `UnityAnimationEvidenceCapture.cs` (~760 lines): Retains graph capture orchestration. Retain forwarding delegates and methods for compatibility.
- **Impact:** Zero breaking API changes.

#### `UnityRendererAlphaAnalysis.cs` (1224 lines)
- **Problem:** The file mixes refusal models, host mesh extraction from live renderers, and triangle classification.
- **Decision:** Refactor the file. Extract data models and live mesh extraction into focused classes.
- **Decomposition:**
  - `RendererAlphaAnalysis.cs` (~205 lines): Contains `RendererAnalysisRefusal` enum, `SubmeshAlphaAnalysis`, `RendererAlphaAnalysis`, and `UnityRendererAlphaExtraction`.
  - `UnityRendererGeometryExtractor.cs` (~310 lines): Contains live mesh extraction logic (`CaptureGeometry`, `HostStructuralRefusalFor`, `MaterialSlotMappingRefusalFor`).
  - `UnityRendererAlphaAnalysis.cs` (~710 lines): Retains triangle classification and animation structural gates.
- **Impact:** Zero breaking API changes.

#### `UnityAlphaFieldEvidence.cs` (1099 lines)
- **Decision:** Leave intact.
- **Justification:** The file coordinates GPU blit capture and readback. Sub-routes for generated and streaming textures already live in separate files. Fragmenting gate methods would harm cohesion.

---

### 2. Analysis Subsystem (`Packages/com.alrauna.amuse/Editor/Analysis/`)

#### Invariant: Zero `UnityEditor` references
The test `AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace` enforces that no file in `Editor/Analysis/` references `UnityEditor`.
All extracted files must obey this rule strictly.

#### `TriangleAlphaClassifier.cs` (2290 lines)
- **Problem:** The file bundles three shared data models inside a 2000-line mathematical sampling engine.
- **Decision:** Refactor data models only. Combine the data models into a single focused file. Keep the mathematical sampling engine intact.
- **Decomposition:**
  - `TriangleAlphaModels.cs` (~285 lines): Contains `TriangleAlphaOutcome`, `TriangleAlphaInput`, and `AlphaTextureData`.
  - `TriangleAlphaClassifier.cs` (~2005 lines): Retains the mathematical sampling proofs.
- **Justification:** The sampling proofs form one cohesive mathematical algorithm. Splitting sampling modes into strategy classes would introduce needless abstraction. Combining data models prevents micro-file proliferation.
- **Impact:** Zero breaking API changes.

#### `AdmittedMaterialStates.cs` (1076 lines)
- **Problem:** The file mixes slot resolution orchestration with animated property admission calculus.
- **Decision:** Refactor the file. Extract property admission into a dedicated class.
- **Decomposition:**
  - `AnimatedPropertyAdmission.cs` (~600 lines): Contains `AdmittedPropertyOutcome`, property admission calculus, grouping, and evaluation.
  - `AdmittedMaterialStates.cs` (~475 lines): Retains `SlotResolutionResult`, slot resolution lifecycle, and deduplication.
- **Impact:** Zero breaking API changes. Forwarding delegates maintain compatibility.

#### `AlphaSemanticsResolver.cs` (975 lines)
- **Problem:** The file combines semantic AST compilation with proof execution.
- **Decision:** Refactor the file. Separate executable proof representation from AST compilation.
- **Decomposition:**
  - `AlphaResolution.cs` (~475 lines): Contains `AlphaResolutionFailure`, `AlphaFieldProvider`, and `AlphaResolution`.
  - `AlphaSemanticsResolver.cs` (~500 lines): Retains static semantic compiler methods.
- **Impact:** Zero breaking API changes.

#### `ExactUvGeometry.cs` (946 lines)
- **Problem:** The file bundles general-purpose arbitrary-precision rational arithmetic with 2D polygon clipping algorithms.
- **Decision:** Refactor the file. Extract rational arithmetic into a dedicated primitive file.
- **Decomposition:**
  - `ExactRational.cs` (~115 lines): Contains `ExactDyadic` and `ExactRational`.
  - `ExactUvGeometry.cs` (~830 lines): Retains polygon clipping and UV domain structures.
- **Safety check:** Retain `FloorMod` on `ExactUvGeometry` because unit tests reflect on it directly.
- **Impact:** Zero breaking API changes.

#### `RuntimeStateResolution.cs` (752 lines)
- **Decision:** Leave intact.
- **Justification:** Adversarial reviews confirmed that extracting seven simple clamp functions creates unnecessary boilerplate and requires seven forwarding methods. The file remains cohesive.

---

### 3. Semantics Subsystem (`Packages/com.alrauna.amuse/Editor/Semantics/`)

#### Invariant: Closed vendor sub-namespaces
Only `LilToon` and `Poiyomi` sub-namespaces are permitted.
Extracted classes must remain in `Alrauna.Amuse.Editor.Semantics.Poiyomi`.

#### `PoiyomiMaterialSemantics.cs` (3152 lines)
- **Problem:** The file mixes source attestation, evidence requests, deep alpha channel interpretation (~1300 lines), and base channel semantics (BaseColor, Emission, Normal).
- **Decision:** Refactor the file into three focused classes. This decomposition matches the LilToon subsystem architecture.
- **Decomposition:**
  - `PoiyomiSourceAttestation.cs` (~450 lines): GUID constants, hash tables, and source verification.
  - `PoiyomiAlphaInterpreter.cs` (~1350 lines): Alpha channel interpretation and feature gates.
  - `PoiyomiMaterialSemantics.cs` (~1350 lines): Base opaque channel semantics (BaseColor, Emission, Normal).
- **Safety check:** Retain const aliases on `PoiyomiMaterialSemantics` for GUIDs, hashes, and version strings. Unit tests access these constants directly.
- **Impact:** Zero breaking API changes.

#### Remaining Semantics Files
- `LilToonSourceAttestation.cs` (2923 lines), `LilToonMaterialSemantics.cs` (1321 lines), `MaterialSemantics.cs` (1317 lines), `UnityMaterialSemantics.cs` (1226 lines), `LilToonMultiModeGate.cs` (791 lines), `PoiyomiOpaqueConversion.cs` (628 lines), and `LilToonAlphaInterpreter.cs` (588 lines) remain intact.

---

### 4. Build, DryRun, and UI Subsystems

#### `AmusePlatformFinishPlugin.cs` (1704 lines)
- **Problem:** The file defines the NDMF plugin coordinator, build state records, and the entire 1380-line Semantic Barrier pass (`AmusePlatformFinishPass`).
- **Decision:** Refactor the file. Separate build state records and pass execution from plugin registration.
- **Decomposition:**
  - `AmusePlatformFinishState.cs` (~160 lines): Build context state definitions and records. Include build target properties to prevent circular coupling with the pass.
  - `AmusePlatformFinishPass.cs` (~1250 lines): Pass execution logic. Retain reflected methods (`OpaquePercentFrom`, `PolygonClampPercentFrom`, `DensityCapFromCoveragePercent`).
  - `AmusePlatformFinishPlugin.cs` (~290 lines): NDMF plugin configuration and pass registrations. Retain forwarding properties for `ActiveBuildTarget` and `ActiveBuildTargetForTests`.
- **Impact:** Zero breaking API changes.

#### `AlphaSeparationPreparation.cs` (1334 lines)
- **Problem:** The file mixes candidate slot qualification, coverage validation, and material conversion recipe preparation.
- **Decision:** Refactor the file. Extract material conversion orchestration. Keep animation checks inline to preserve cohesive control flow.
- **Decomposition:**
  - `AlphaSeparationMaterialConverter.cs` (~400 lines): Material clone creation and conversion recipe dispatch.
  - `AlphaSeparationPreparation.cs` (~935 lines): Candidate qualification, animation gating, and pass coordination.
- **Impact:** Zero breaking API changes.

#### `AlphaSeparationApply.cs` (1243 lines)
- **Decision:** Leave intact.
- **Justification:** Adversarial reviews confirmed that `AlphaSeparationApply` is a single atomic mutation pass with strict sequential dependencies (curves, then sharedMesh, then sharedMaterials).
Slicing this class into four micro-files increases coupling, introduces state passing, and risks mutation order bugs.
Furthermore, unit tests verify that `ObjectRegistry.RegisterReplacedObject` literally appears in `Build/AlphaSeparationApply.cs`.

#### `AmuseAvatarOptimizerEditor.cs` (1327 lines)
- **Decision:** Leave intact.
- **Justification:** Adversarial reviews confirmed that extracting view classes from an IMGUI custom inspector creates fake abstractions.
It forces passing serialized properties and foldout flags across classes.
Multiple unit tests reflect on non-public editor fields (`_settingsOpen`, `_alphaSeparatorOpen`, `DrawTransformationRow`).
Leaving the file intact avoids test breakage and preserves IMGUI cohesion.

#### `AlphaSeparationDryRun.cs` (1743 lines)
- **Problem:** The file combines background dry-run orchestration with preview row generation.
- **Decision:** Refactor the file. Extract preview row construction. Keep worker coordination and fingerprinting in the coordinator.
- **Decomposition:**
  - `DryRunRowBuilder.cs` (~380 lines): Constructs `MaterialTransformationRow` entries.
  - `AlphaSeparationDryRun.cs` (~1360 lines): Retains background execution coordination, fingerprinting, and caching.
- **Safety check:** Retain `BuildPolicyFingerprint`, `_cts`, and `_inFlightTask` on `AlphaSeparationDryRun`. Tests reflect on these members.
- **Impact:** Zero breaking API changes.

---

## Prioritized Refactoring Plan

Refactoring proceeds in sequential order from foundational components to downstream consumers:

1. **Phase 1: Foundational Data Contracts and Models**
   - Extract `MaterialEvidenceRequest.cs` and `CapturedMaterialEvidence.cs` from `UnityMaterialEvidenceCapture.cs`.
   - Extract `TriangleAlphaModels.cs` from `TriangleAlphaClassifier.cs`.
   - Extract `ExactRational.cs` from `ExactUvGeometry.cs`.
   - Extract `RendererAlphaAnalysis.cs` from `UnityRendererAlphaAnalysis.cs`.
   - Extract `AlphaResolution.cs` from `AlphaSemanticsResolver.cs`.

2. **Phase 2: Analysis and Semantics Logic Separation**
   - Extract `AnimatedPropertyAdmission.cs` from `AdmittedMaterialStates.cs`.
   - Extract `AnimationBindingResolution.cs` from `UnityAnimationEvidenceCapture.cs`.
   - Extract `UnityRendererGeometryExtractor.cs` from `UnityRendererAlphaAnalysis.cs`.
   - Refactor `PoiyomiMaterialSemantics.cs` by extracting `PoiyomiSourceAttestation.cs` and `PoiyomiAlphaInterpreter.cs`.

3. **Phase 3: Build Pipeline and DryRun Decomposition**
   - Extract `AmusePlatformFinishState.cs` and `AmusePlatformFinishPass.cs` from `AmusePlatformFinishPlugin.cs`.
   - Extract `AlphaSeparationMaterialConverter.cs` from `AlphaSeparationPreparation.cs`.
   - Extract `DryRunRowBuilder.cs` from `AlphaSeparationDryRun.cs`.

---

## Architectural Rules and Guardrails

1. **No Partial Classes:**
   Every extracted component must be an independent class with a clear boundary.

2. **No Speculative Abstractions:**
   Do not introduce interfaces with one implementation.
   Do not add factory classes or dependency injection containers.
   Use static classes or direct composition.

3. **Preserve Compatibility:**
   All public and internal methods must keep existing signatures.
   Add forwarding methods on original classes where external callers or tests expect them.
   Unity serialized fields, prefabs, and scenes must not be modified.

4. **Zero `UnityEditor` in `Analysis/`:**
   No file in `Editor/Analysis/` may reference the `UnityEditor` namespace.

5. **Closed Semantics Namespaces:**
   All Poiyomi extractions must remain in `Alrauna.Amuse.Editor.Semantics.Poiyomi`.

6. **Test Verification Between Changes:**
   Execute tests in the dev editor instance after each change.
   Verify that all tests continue to pass before proceeding to the next file.
