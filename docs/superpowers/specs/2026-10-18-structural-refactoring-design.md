# Structural Refactoring Design Specification

Status as of 2026-10-18.

## Goal

This specification defines the structural refactoring plan for the AMUSE C# codebase.
The refactoring improves cohesion, separation of concerns, coupling, readability, and maintainability.
The target size for focused files is 200 to 400 lines.
Files with more than 500 lines cross the soft review threshold.
Files with 700 or more lines give a strong refactoring signal.
Large files whose size is justified by a single cohesive responsibility or static data tables remain intact.

## Architectural Approach

AMUSE uses an evidence-first, pipeline architecture across five modules:
1. `Host/`: Interfaces with live Unity engine objects. It captures immutable evidence.
2. `Analysis/`: Pure mathematical proof core. It operates on immutable data structures and has zero UnityEditor dependency.
3. `Semantics/`: Pinned shader frontends (Poiyomi and LilToon) and value algebra.
4. `Build/`: NDMF plugin lifecycle passes, material preparation, and mesh mutation.
5. `DryRun/`: Editor-time preview calculation and background caching.

Refactoring proceeds strictly from foundational models and leaf dependencies up to orchestrators.
The refactoring avoids speculative abstractions and partial classes.
It preserves public and internal API contracts through forwarding delegates and methods.

---

## Detailed Component Specifications

### 1. Host Subsystem Extractions (`Packages/com.alrauna.amuse/Editor/Host/`)

#### Component 1.1: `MaterialEvidenceRequest.cs`
- **Source:** Extracted from `UnityMaterialEvidenceCapture.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Host/MaterialEvidenceRequest.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Host`.
- **Contents:**
  - `TextureEvidenceKinds` flags enum.
  - `TexturePropertyEvidenceRequest` readonly struct.
  - `MaterialEvidenceRequest` class (with `Empty`, property collections, mip cap mutators, `Combine`, and `Equals`).
  - `MaterialEvidenceCaptureInput` readonly struct.
- **Responsibility:** Defines the immutable request schema for properties and textures.
- **Estimated Size:** ~410 lines.

#### Component 1.2: `CapturedMaterialEvidence.cs`
- **Source:** Extracted from `UnityMaterialEvidenceCapture.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Host/CapturedMaterialEvidence.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Host`.
- **Contents:**
  - `CapturedMaterialEvidence` class (with property entry structs: `PresenceEntry`, `ScalarEntry`, `ColorEntry`, `VectorEntry`, `TextureEntry`).
  - Immutable derivation methods: `WithScalar`, `WithColor`, `WithVector`, `WithTexture`.
  - `CapturedTextureEvidence` internal sealed class.
  - `CapturedTextureAssignment` readonly struct.
- **Responsibility:** Immutable data containers for captured material and texture properties.
- **Estimated Size:** ~590 lines.

#### Component 1.3: Retained `UnityMaterialEvidenceCapture.cs`
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Host`.
- **Contents:** `UnityMaterialEvidenceCapture` static class (`Capture`, `CaptureCore`, `CaptureAssignments`, `CaptureTexture`, `MaterialBuilder`, `SharedTextureBuilder`).
- **Responsibility:** Batch evidence capture engine from Unity `Material` and `Texture` objects.
- **Estimated Size:** ~755 lines.

#### Component 1.4: `AnimationBindingResolution.cs`
- **Source:** Extracted from `UnityAnimationEvidenceCapture.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Host/AnimationBindingResolution.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Host`.
- **Contents:**
  - `AnimatedPropertyKind` enum.
  - `ProofRelevantBindingResolution` enum.
  - `AnimatedPropertyRef` readonly struct.
  - Delegate types (`TryParseMaterialSlotBindingFor`, `TryResolveProofRelevantFloatBinding`, `AddressesAnalyzedRendererCheck`, `IsUnrecognizedObjectMaterialBindingCheck`).
  - Parsing and resolution methods (`ResolveProofRelevant`, `TryParseMaterialSlotBindingFor`, `IsCompatibleRendererType`, `AddressesAnalyzedRenderer`, `IsUnrecognizedObjectMaterialBinding`, `TryResolveGeneratedProperty`, string parsing helpers).
- **Responsibility:** Pure animation property path parsing and binding qualification without graph traversal.
- **Estimated Size:** ~470 lines.

#### Component 1.5: Retained `UnityAnimationEvidenceCapture.cs`
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Host`.
- **Contents:** Traversal and capture methods (`Capture`, `CaptureGraphThroughSeams`, `CaptureWithAnimationIndex`, `CaptureGraph`, `CaptureObserved`).
- **Compatibility:** Forwarding static delegates (`ProofRelevantResolver`, `MaterialSlotBindingParser`) and methods forward to `AnimationBindingResolution`.
- **Estimated Size:** ~760 lines.

#### Component 1.6: `RendererAlphaAnalysis.cs`
- **Source:** Extracted from `UnityRendererAlphaAnalysis.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Host/RendererAlphaAnalysis.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Host`.
- **Contents:**
  - `RendererAnalysisRefusal` closed enum.
  - `SubmeshAlphaAnalysis` class.
  - `RendererAlphaAnalysis` class.
- **Responsibility:** Data models and refusal taxonomy for renderer alpha analysis.
- **Estimated Size:** ~205 lines.

#### Component 1.7: `UnityRendererGeometryExtractor.cs`
- **Source:** Extracted from `UnityRendererAlphaAnalysis.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Host/UnityRendererGeometryExtractor.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Host`.
- **Contents:**
  - `CaptureGeometry` method.
  - `HostStructuralRefusalFor` method.
  - `MaterialSlotMappingRefusalFor` method.
  - Topology extraction from live Unity `Mesh` and `Renderer` into `UnityRendererAlphaSnapshot`.
- **Responsibility:** Extracts mesh geometry from live Unity renderers into immutable snapshots.
- **Estimated Size:** ~310 lines.

#### Component 1.8: Retained `UnityRendererAlphaAnalysis.cs`
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Host`.
- **Contents:** `Analyze(UnityRendererAlphaSnapshot, ...)`, `GatherAlphaFields`, `Classify`, `StructuralRefusalFor`, and `IsSupportedRendererType`.
- **Compatibility:** `CaptureGeometry` forwards to `UnityRendererGeometryExtractor.CaptureGeometry`.
- **Estimated Size:** ~710 lines.

---

### 2. Analysis Subsystem Extractions (`Packages/com.alrauna.amuse/Editor/Analysis/`)

#### Invariant: Zero `UnityEditor` references
The namespace `Alrauna.Amuse.Editor.Analysis` contains no `UnityEditor` references.
The test `AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace` enforces this invariant.

#### Component 2.1: `TriangleAlphaModels.cs`
- **Source:** Extracted from `TriangleAlphaClassifier.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaModels.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Analysis`.
- **Contents:**
  - `TriangleAlphaOutcome` enum.
  - `TriangleAlphaInput` readonly struct (with vertex positions, UV channels, channel access, and finiteness validation).
  - `AlphaTextureData` class (with alpha texels, dimensions, and fast opacity flags).
- **Responsibility:** Foundational geometry inputs and texel buffer data models.
- **Estimated Size:** ~285 lines.

#### Component 2.2: Retained `TriangleAlphaClassifier.cs`
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Analysis`.
- **Contents:** Mathematical proof core (`Classify`, `ClassifyMapped`, `DecideMapped`, sampling mode proofs: Point Clamp, Bilinear Repeat, Bilinear Clamp, Point Repeat, Separating Axis Theorem filters, texel interval math).
- **Estimated Size:** ~2005 lines.
- **Justification for Size:** Single cohesive mathematical algorithm. Splitting sampling proofs into strategy classes would introduce premature virtual dispatch.

#### Component 2.3: `ExactRational.cs`
- **Source:** Extracted from `ExactUvGeometry.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Analysis/ExactRational.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Analysis`.
- **Contents:**
  - `ExactDyadic` struct.
  - `ExactRational` struct (arbitrary-precision rational arithmetic using `BigInteger`).
- **Responsibility:** Domain-agnostic numeric arithmetic primitives.
- **Estimated Size:** ~115 lines.

#### Component 2.4: Retained `ExactUvGeometry.cs`
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Analysis`.
- **Contents:** Geometric structures (`AlphaUvEnvelope`, `ExactUvPoint`, `ExactUvDomain`, `ExactInterval`), float decoding, Sutherland-Hodgman polygon clipping, monotone chain convex hull, and `FloorMod`.
- **Compatibility:** `FloorMod` remains on `ExactUvGeometry` for unit tests.
- **Estimated Size:** ~830 lines.

#### Component 2.5: `AlphaResolution.cs`
- **Source:** Extracted from `AlphaSemanticsResolver.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Analysis/AlphaResolution.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Analysis`.
- **Contents:**
  - `AlphaResolutionFailure` enum.
  - `AlphaFieldProvider` delegate.
  - `AlphaResolution` class (factory methods, triangle evaluation, mip chain traversal, UV mappings, and conjunction lattice logic).
- **Responsibility:** Resolved proof representation and runtime triangle classifier.
- **Estimated Size:** ~475 lines.

#### Component 2.6: Retained `AlphaSemanticsResolver.cs`
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Analysis`.
- **Contents:** Static AST compiler methods (`Resolve`, `ResolveScaledSample`, `ResolveMappedSample`, `ResolveProductChain`, `ResolveSaturatingSum`, `ResolveSaturatingDifference`, `ResolveScalar`, `ResolveSampled`, `EffectiveSampling`).
- **Responsibility:** Compiles semantic value AST into `AlphaResolution` proof instances.
- **Estimated Size:** ~500 lines.

#### Component 2.7: `AnimatedPropertyAdmission.cs`
- **Source:** Extracted from `AdmittedMaterialStates.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Analysis/AnimatedPropertyAdmission.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Analysis`.
- **Contents:**
  - `AdmittedPropertyOutcome` enum.
  - `AnimatedPropertyGroup` class.
  - Methods: `TryAdmitDerivedEvidence`, `AnyAdmittedMaterialDeclaresProperty`, `GroupByProperty`, `Admit`, `OwningTextureProperty`, `RefusalFor`, `AdmitScalar`, `AdmitColor`, `AdmitVector`, `TryGetBlockEntry`.
- **Responsibility:** Calculates property admission bounds across animation curves and material property blocks.
- **Estimated Size:** ~600 lines.

#### Component 2.8: Retained `AdmittedMaterialStates.cs`
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Analysis`.
- **Contents:** `SlotResolutionResult` class, `ResolveSlot`, `ResolveAdmittedMaterialSemantics`, `DistinctResolutions`, `AreExactlyInterchangeable`.
- **Compatibility:**
  - `TryAdmitDerivedEvidence` forwards to `AnimatedPropertyAdmission.TryAdmitDerivedEvidence`.
  - `AdmitScalar`, `AdmitColor`, and `AdmitVector` forward to `AnimatedPropertyAdmission` methods to maintain backwards compatibility for tests.
- **Estimated Size:** ~475 lines.

---

### 3. Semantics Subsystem Extractions (`Packages/com.alrauna.amuse/Editor/Semantics/`)

#### Invariant: Closed vendor sub-namespaces
All Poiyomi extractions remain inside `Alrauna.Amuse.Editor.Semantics.Poiyomi`.

#### Component 3.1: `PoiyomiSourceAttestation.cs`
- **Source:** Extracted from `PoiyomiMaterialSemantics.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Semantics.Poiyomi`.
- **Contents:**
  - Canonical shader GUIDs, package version constants, normalized SHA-256 hashes, `PoiyomiPackageName`.
  - `PoiyomiSourceEvidence` struct.
  - Methods: `ComputeNormalizedSourceHash`, `GatherSourceEvidence`, `GatherAlphaSourceEvidence`, `TryVerifyPoiyomiIdentity`, `ClearSourceHashMemo`.
- **Responsibility:** Shader source attestation and source evidence collection for Poiyomi.
- **Estimated Size:** ~450 lines.

#### Component 3.2: `PoiyomiAlphaInterpreter.cs`
- **Source:** Extracted from `PoiyomiMaterialSemantics.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Semantics.Poiyomi`.
- **Contents:**
  - `AlphaEvidenceRequest`, `PlainAlphaEvidenceRequest`, `TwoPassAlphaEvidenceRequest`, `PlainTwoPassAlphaEvidenceRequest`.
  - Feature gate arrays and predicate generators.
  - Methods: `AlphaPredicateRequestFor`, `InterpretVerifiedAlpha`, `InterpretVerifiedTwoPassAlpha`, `AlphaUnknownReasonFor`, `InterpretAlpha`, `TryInterpretAlphaMask`, `IsProvenOpaqueBlend`, decal slot helpers.
- **Responsibility:** Interprets alpha channel evidence and feature gates for Poiyomi shaders.
- **Estimated Size:** ~1350 lines.

#### Component 3.3: Retained `PoiyomiMaterialSemantics.cs`
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Semantics.Poiyomi`.
- **Contents:**
  - Channel semantics: BaseColor, Emission, Normal.
  - Public entry points: `AnalyzeBaseMaterial`, `InterpretVerifiedMaterial`, `InterpretVerifiedTwoPassMaterial`.
- **Compatibility:**
  - Constant aliases for `CanonicalShaderGuid`, `CanonicalNormalizedSourceHash`, `TwoPassCanonicalShaderGuid`, `TwoPassCanonicalNormalizedSourceHash`, `PoiyomiPackageVersion`, `PoiyomiPackageName`, `PoiyomiToonShaderName`, `PoiyomiTwoPassShaderName`.
  - Forwarding properties: `AlphaEvidenceRequest`, `PlainAlphaEvidenceRequest`, `TwoPassAlphaEvidenceRequest`, `PlainTwoPassAlphaEvidenceRequest`.
  - Forwarding methods: `GatherSourceEvidence`, `GatherAlphaSourceEvidence`, `TryVerifyPoiyomiIdentity`, `ClearSourceHashMemo`, `ComputeNormalizedSourceHash`, `InterpretVerifiedAlpha`, `InterpretVerifiedTwoPassAlpha`, `AlphaPredicateRequestFor`.
- **Estimated Size:** ~1350 lines.

---

### 4. Build and DryRun Subsystems Extractions

#### Component 4.1: `AmusePlatformFinishPass.cs`
- **Source:** Extracted from `AmusePlatformFinishPlugin.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Build`.
- **Contents:**
  - `AmusePlatformFinishPass` static class (`Execute`).
  - Reflected helper methods: `OpaquePercentFrom`, `PolygonClampPercentFrom`, `DensityCapFromCoveragePercent`.
- **Responsibility:** Implements Pass 3 (the Semantic Barrier pass).
- **Estimated Size:** ~1380 lines.

#### Component 4.2: Retained `AmusePlatformFinishPlugin.cs`
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Build`.
- **Contents:**
  - `AmusePlatformFinishState` class (lines 15 to 236).
  - `AmusePlatformFinishPlugin` NDMF plugin coordinator (`Configure`, pass registrations).
  - Static methods: `ActiveBuildTarget()` and delegate field `ActiveBuildTargetForTests`.
- **Responsibility:** Plugin coordinator and build-wide context state.
- **Estimated Size:** ~307 lines.

#### Component 4.3: `AlphaSeparationMaterialConverter.cs`
- **Source:** Extracted from `AlphaSeparationPreparation.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationMaterialConverter.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Build`.
- **Contents:** Material clone creation, conversion recipe dispatch (`PrepareLilToonCanonicalOpaque`, `PreparePoiyomiCanonicalOpaque`, `ConvertAdmittedMaterial`), and conversion verification.
- **Responsibility:** Material clone creation and canonical opaque recipe execution.
- **Estimated Size:** ~400 lines.

#### Component 4.4: Retained `AlphaSeparationPreparation.cs`
- **Target Path:** `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.Build`.
- **Contents:** Candidate qualification, animation gate checks, and pass coordination (`Prepare`).
- **Compatibility:** `TryMapLilToonOutcome`, `TryMapPoiyomiOutcome`, and `ConvertAdmittedMaterial` forward to `AlphaSeparationMaterialConverter`.
- **Estimated Size:** ~935 lines.

#### Component 4.5: `DryRunRowBuilder.cs`
- **Source:** Extracted from `AlphaSeparationDryRun.cs`.
- **Target Path:** `Packages/com.alrauna.amuse/Editor/DryRun/DryRunRowBuilder.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.DryRun`.
- **Contents:** `BuildRows`, `BuildResolvedRefusalRows`, and helper methods that format `MaterialTransformationRow` entries from analysis results.
- **Responsibility:** Formats transformation preview rows for the editor dry-run table.
- **Estimated Size:** ~380 lines.

#### Component 4.6: Retained `AlphaSeparationDryRun.cs`
- **Target Path:** `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`.
- **Namespace:** `Alrauna.Amuse.Editor.DryRun`.
- **Contents:** Background worker pacing, cancellation management, geometry fingerprinting, and session caching.
- **Compatibility:** Retain `BuildPolicyFingerprint`, `_cts`, and `_inFlightTask` on `AlphaSeparationDryRun` for unit test reflection.
- **Estimated Size:** ~1360 lines.

---

## Phased Execution Roadmap

Execution proceeds strictly in three sequential phases:

### Phase 1: Foundational Models and Data Contracts
1. Extract `MaterialEvidenceRequest.cs` from `UnityMaterialEvidenceCapture.cs`.
2. Extract `CapturedMaterialEvidence.cs` from `UnityMaterialEvidenceCapture.cs`.
3. Extract `TriangleAlphaModels.cs` from `TriangleAlphaClassifier.cs`.
4. Extract `ExactRational.cs` from `ExactUvGeometry.cs`.
5. Extract `RendererAlphaAnalysis.cs` from `UnityRendererAlphaAnalysis.cs`.
6. Extract `AlphaResolution.cs` from `AlphaSemanticsResolver.cs`.

### Phase 2: Analysis and Semantics Logic Separation
1. Extract `AnimatedPropertyAdmission.cs` from `AdmittedMaterialStates.cs`.
2. Extract `AnimationBindingResolution.cs` from `UnityAnimationEvidenceCapture.cs`.
3. Extract `UnityRendererGeometryExtractor.cs` from `UnityRendererAlphaAnalysis.cs`.
4. Extract `PoiyomiSourceAttestation.cs` from `PoiyomiMaterialSemantics.cs`.
5. Extract `PoiyomiAlphaInterpreter.cs` from `PoiyomiMaterialSemantics.cs`.

### Phase 3: Build Pipeline and DryRun Decomposition
1. Extract `AmusePlatformFinishPass.cs` from `AmusePlatformFinishPlugin.cs`.
2. Extract `AlphaSeparationMaterialConverter.cs` from `AlphaSeparationPreparation.cs`.
3. Extract `DryRunRowBuilder.cs` from `AlphaSeparationDryRun.cs`.

---

## Architectural Rules and Invariants

1. **No Partial Classes:**
   Every extracted component is an independent, non-partial class or struct.

2. **No Speculative Abstractions:**
   All extractions use concrete static classes or direct composition.
   No interfaces with single implementations are introduced.

3. **Behavioral Invariance:**
   Every refactoring step is a pure structural extraction.
   No algorithm logic, mathematical interval proofs, or mutation sequences are altered.

4. **Preserve Compatibility:**
   All public and internal method signatures remain intact.
   Original types provide forwarding properties or methods where external callers or tests expect them.

5. **Mutation Safety:**
   `AlphaSeparationApply.cs` remains intact.
   The mutation sequence (curves, then sharedMesh, then sharedMaterials) is preserved.
   The text anchor `ObjectRegistry.RegisterReplacedObject` remains inside `AlphaSeparationApply.cs`.

6. **Continuous Test Verification:**
   After each extracted component, run the test suite in the dev editor instance.
   Verify that all tests pass before proceeding to the next extraction.
