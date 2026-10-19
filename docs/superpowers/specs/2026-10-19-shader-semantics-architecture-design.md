# Shader Semantics Architecture Design

Status as of 2026-10-19.

Spec for: `docs/superpowers/investigations/2026-10-19-shader-semantics-architecture-investigation.md`

## 1. Decision Record

The investigation on 2026-10-19 evaluated whether AMUSE must store shader semantics as compiled C# code, as JSON data files, or as a hybrid model.
The owner approved the architectural recommendation:

1. **Retain Compiled C# as the Sole Source of Truth**:
   AMUSE maintains shader semantic interpretation, evidence capture schemas, and algebraic value folding in compiled C#.
   AMUSE rejects runtime JSON interpretation and custom domain-specific expression languages.

2. **Preserve Proof-Grade Invariants**:
   Every shader evaluation must fail closed.
   Unknown shader features, missing properties, or failed source attestations must produce `TriangleAlphaOutcome.Unknown`.
   Proof justifications and mathematical citations must remain local to their gate code.

3. **Consolidate Poiyomi Property Literals with Ponytail Minimalism**:
   The investigation revealed that multiple C# files repeat Poiyomi property string literals and private constants.
   AMUSE introduces `PoiyomiProperties.cs` to mirror the existing `LilToonProperties.cs` pattern.
   This change centralizes shared property names in one file and eliminates duplicate private string constants.

4. **Add Architectural Invariant Tests**:
   AMUSE adds automated architectural tests to verify that the Semantics module remains free of runtime JSON dependencies and preserves strict type safety.

---

## 2. Goal

Formalize the compiled C# architecture of the Semantics module and implement targeted maintainability improvements without behavioral change.
Every classification outcome, refusal diagnostic, and material conversion recipe remains byte-for-byte identical.

---

## 3. Non-Goals

- No runtime JSON loaders, no asset deserializers, and no dynamic expression interpreters.
- Compacting static attestation tables in `LilToonSourceAttestation.cs` into binary spans is deferred to a future change.
- No changes to mathematical formulas, rational intervals, or UV classification logic in `Editor/Analysis/`.
- No changes to `docs/architecture/vision.md`.
- No changes to the runtime component `AmuseAvatarOptimizer.cs`.
- No changes to Unity scene files, prefabs, materials, or asset GUIDs.
- No new external package dependencies.
---

## 4. Architectural Rules

### Rule 1: Strict Module Encapsulation and Deep Interfaces
The `Editor/Semantics/` module sits at a clean seam between Unity material inspection and pure mathematical analysis.
The module hides internal gate evaluations, canonicalization state machines, and conversion recipes behind a narrow interface:
- Entry point: `UnityMaterialSemantics.AnalyzeAlphaMaterial`.
- Output models: `ScalarSemanticValue`, `AffineAlphaMap`, `UvMapping`, and `TextureSampling`.
Callers in `Editor/Analysis/` and `Editor/Build/` never inspect vendor-specific AST nodes or private gate flags.

### Rule 2: Shared Vendor Property Vocabulary
When more than one C# file in a shader family reads a shader property name, that name must live in that family's `*Properties.cs` class.
- lilToon uses `LilToonProperties.cs`.
- Poiyomi uses `PoiyomiProperties.cs`.
A property read by only one private helper retains its local scope.
Every property constant must use exact ordinal spelling matching the vendor shader definition.

### Rule 3: Fail-Closed Attestation Discipline
Every supported shader must undergo static cryptographic attestation before AMUSE evaluates material properties.
Attestation records pinned asset GUIDs, package version strings, and SHA-256 source digests.
If a shader asset fails attestation, the evaluator returns `Unknown` and halts optimization for that material.
Attestation constants compile directly into C# bytecode.

### Rule 4: Zero Runtime Allocation for Semantic Parsing
Shader analysis must execute without reading disk files or allocating dynamic parser ASTs during NDMF builds.
Static schemas (`MaterialEvidenceRequest`) and pre-allocated static arrays provide zero-allocation evidence collection.

---

## 5. Detailed Changes

### Component 1: Poiyomi Property Consolidation (`PoiyomiProperties.cs`)

Create `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiProperties.cs`.
Define an internal static class that centralizes shared Poiyomi property names read by two or more files:
- `ShaderOptimizerEnabled = "_ShaderOptimizerEnabled"`
- `Mode = "_Mode"`
- `TwoPassMode = "_ModeTwoPass"`
- `Cutoff = "_Cutoff"`
- `AlphaForceOpaque = "_AlphaForceOpaque"`
- `MainTex = "_MainTex"`
- `Color = "_Color"`
- `MainTexUv = "_MainTexUV"`
- `MainTexPan = "_MainTexPan"`
- `EnableOutlines = "_EnableOutlines"`
- `OutlineOverrideAlpha = "_OutlineOverrideAlpha"`
- `LineColor = "_LineColor"`
- `OutlineTexture = "_OutlineTexture"`
- `OutlineTextureUv = "_OutlineTextureUV"`
- `OutlineTexturePan = "_OutlineTexturePan"`
- `SrcBlend = "_SrcBlend"`
- `DstBlend = "_DstBlend"`
- `BlendOp = "_BlendOp"`
- `SrcBlendAlpha = "_SrcBlendAlpha"`
- `DstBlendAlpha = "_DstBlendAlpha"`
- `BlendOpAlpha = "_BlendOpAlpha"`
- `AlphaToCoverage = "_AlphaToCoverage"`

Properties read in only one file retain local private scope in that file, complying with Rule 2.

Retarget callers in:
- `PoiyomiMaterialSemantics.cs`
- `PoiyomiAlphaInterpreter.cs`
- `PoiyomiSourceAttestation.cs`
- `PoiyomiOpaqueConversion.cs`

In `PoiyomiMaterialSemantics.cs`, retain internal constants as forwarding aliases to `PoiyomiProperties`:
- `internal const string OutlineEnabledProperty = PoiyomiProperties.EnableOutlines`
- `internal const string OutlineOverrideAlphaProperty = PoiyomiProperties.OutlineOverrideAlpha`
- `internal const string LineColorProperty = PoiyomiProperties.LineColor`
- `internal const string OutlineTextureProperty = PoiyomiProperties.OutlineTexture`
- `internal const string OutlineTextureUvProperty = PoiyomiProperties.OutlineTextureUv`
- `internal const string OutlineTexturePanProperty = PoiyomiProperties.OutlineTexturePan`
This preservation maintains the internal test seam used by `PoiyomiCutoutSplitTests.cs`.
### Component 2: Architectural Invariant Test Suite (`ShaderSemanticsArchitectureTests.cs`)

Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs`.
Add tests to verify architectural constraints:
1. `SemanticsDirectoryContainsZeroRuntimeDataFiles`:
   Scan `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/` and assert that no `.json`, `.yaml`, `.xml`, or `.txt` files exist.
2. `PoiyomiPropertiesMatchShaderConventions`:
   Assert that every constant in `PoiyomiProperties` starts with an underscore.
   Assert that all property names are distinct.
3. `LilToonPropertiesMatchShaderConventions`:
   Assert that every constant in `LilToonProperties` starts with an underscore.
   Assert that all property names are distinct.
4. `AttestationProfilesContainValidGuidsAndHashes`:
   Assert that pinned shader GUIDs are valid 32-character hexadecimal strings.
   Assert that pinned profile source digests are valid 64-character hexadecimal strings.
---

## 6. Safety, Security, and Risk Analysis

### Safety and Invariants
The proposed changes perform mechanical refactoring.
The changes replace duplicate string literals with typed internal constants.
The runtime values are identical.
Therefore, the changes preserve all existing mathematical proofs and regression tests.

### Performance
The changes eliminate repeated string literal allocations across assemblies.
Accessing internal `const string` values in C# compiles to direct `ldstr` IL instructions.
The change introduces zero runtime overhead and zero memory allocations.

### Ponytail Compliance
The changes delete redundant private constants.
The changes reuse the established `*Properties.cs` pattern already present in `LilToonProperties.cs`.
The changes add zero external dependencies and zero speculative abstractions.
The design complies with the Ponytail ladder.
