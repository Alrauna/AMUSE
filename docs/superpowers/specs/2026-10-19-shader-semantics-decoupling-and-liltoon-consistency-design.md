# Shader Semantics Decoupling and lilToon Consistency Design

Status as of 2026-10-19.

Spec for: `docs/superpowers/investigations/2026-10-19-shader-semantics-decoupling-and-liltoon-consistency-investigation.md`

## 1. Decision Record

The investigation on 2026-10-19 confirmed that `Editor/Semantics/Poiyomi/` and `Editor/Semantics/LilToon/` maintain complete cross-vendor decoupling.
The investigation revealed that lilToon has incomplete property centralization.
AMUSE approves the architectural design to:
1. Preserve total cross-vendor decoupling between `Poiyomi/` and `LilToon/`.
2. Expand `LilToonProperties.cs` with all 21 missing shared properties.
3. Retarget all 12 lilToon caller files and delete redundant private constants.
4. Retarget `EffectiveRenderState.cs` to use `CanonicalOpaqueVerification.RenderTypeTagName`.
5. Enforce full property centralization in `ShaderSemanticsArchitectureTests.cs`.

---

## 2. Goal

Establish complete architectural symmetry between lilToon and Poiyomi.
Centralize all properties that two or more files read in `LilToonProperties.cs`.
Eliminate duplicate private constants across lilToon source files.
Retarget raw string literals to typed constants.
Preserve byte-identical classification, refusal reporting, and material conversion outcomes.

---

## 3. Non-Goals

- No runtime JSON loaders, no asset deserializers, and no dynamic expression interpreters.
- No changes to mathematical formulas, rational intervals, or UV classification logic in `Editor/Analysis/`.
- No changes to `docs/architecture/vision.md`.
- No changes to the runtime component `AmuseAvatarOptimizer.cs`.
- No changes to Unity scene files, prefabs, materials, or asset GUIDs.
- No new external package dependencies.

---

## 4. Architectural Rules

### Rule 1: Cross-Vendor Independence
Files under `Editor/Semantics/LilToon/` must never import or reference types from `Editor/Semantics/Poiyomi/`.
Files under `Editor/Semantics/Poiyomi/` must never import or reference types from `Editor/Semantics/LilToon/`.
Both frontends communicate with AMUSE exclusively through `UnityMaterialSemantics.cs` and the domain models in `MaterialSemantics.cs`.

### Rule 2: Shared Property Centralization
When two or more files in a shader family read a property name, that name must live in that family's `*Properties.cs` class.
Properties that only one private helper reads retain their local private scope.

### Rule 3: Zero Behavioral Regressions
Every refactoring must preserve exact string values and ordinal comparisons.
Every existing test in `Tests/Editor/Semantics/` must continue to pass without modification.

---

## 5. Detailed Changes

### Component 1: `LilToonProperties.cs` Expansion

Expand `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonProperties.cs` with 21 new property constants:
- `DistanceFade = "_DistanceFade"`
- `TransparentMode = "_TransparentMode"`
- `UseClippingCanceller = "_UseClippingCanceller"`
- `AsOverlay = "_AsOverlay"`
- `OutlineTexHsvg = "_OutlineTexHSVG"`
- `AlphaMask = "_AlphaMask"`
- `ZTest = "_ZTest"`
- `ZWrite = "_ZWrite"`
- `ColorMask = "_ColorMask"`
- `BlendOp = "_BlendOp"`
- `SrcBlend = "_SrcBlend"`
- `DstBlend = "_DstBlend"`
- `BlendOpAlpha = "_BlendOpAlpha"`
- `SrcBlendAlpha = "_SrcBlendAlpha"`
- `DstBlendAlpha = "_DstBlendAlpha"`
- `OffsetFactor = "_OffsetFactor"`
- `OffsetUnits = "_OffsetUnits"`
- `SrcBlendFa = "_SrcBlendFA"`
- `DstBlendFa = "_DstBlendFA"`
- `BlendOpFa = "_BlendOpFA"`
- `BlendOpAlphaFa = "_BlendOpAlphaFA"`

### Component 2: Retargeting in `Editor/Semantics/LilToon/`

Retarget callers and delete redundant private constants in 12 files:

1. `LilToonAlphaInterpreter.cs`:
   - Delete private constants `DissolveParamsProperty`, `MainTexScrollRotateProperty`, `CutoffProperty`, `ColorProperty`, `MainTextureProperty`, and `AlphaMaskModeProperty`.
   - Retarget all usages to `LilToonProperties.*`.

2. `LilToonCutoutSourceEligibility.cs`:
   - Delete private constant `CutoffProperty`.
   - Retarget `CutoffProperty` and `_DistanceFade` to `LilToonProperties.Cutoff` and `LilToonProperties.DistanceFade`.

3. `LilToonTransparentSourceEligibility.cs`:
   - Delete private constants `CutoffProperty`, `AlphaBoostFaProperty`, `SubpassCutoffProperty`, and `DistanceFadeProperty`.
   - Retarget all usages to `LilToonProperties.*`.

4. `LilToonMultiSourceEligibility.cs`:
   - Delete 10 private constants: `CutoffProperty`, `AlphaMaskModeProperty`, `DitherProperty`, `AlphaBoostFaProperty`, `SubpassCutoffProperty`, `DissolveParamsProperty`, `ClippingCancellerProperty`, `OverlayProperty`, `DistanceFadeProperty`, and `OutlineToneProperty`.
   - Retarget all usages to `LilToonProperties.*`.

5. `LilToonMultiModeGate.cs`:
   - Delete 10 private constants: `ClippingCancellerProperty`, `OverlayProperty`, `DitherProperty`, `AlphaMaskModeProperty`, `DissolveParamsProperty`, `DistanceFadeProperty`, `OutlineToneProperty`, `UseParallaxProperty`, `UseMain2ndTexProperty`, and `UseMain3rdTexProperty`.
   - Retarget all usages to `LilToonProperties.*`.

6. `LilToonAlphaMaskSemantics.cs`:
   - Delete `internal const string` fields: `ModeProperty`, `ScaleProperty`, `ValueProperty`, `MaskProperty`.
   - Retarget usages inside the file to `LilToonProperties.*`.

7. `LilToonDistanceFadeSemantics.cs`:
   - Retarget `DistanceFadeProperty = "_DistanceFade"` to `LilToonProperties.DistanceFade`.

8. `LilToonAlphaEvidenceRequests.cs`:
   - Retarget raw string literal `"_AlphaMask"` to `LilToonProperties.AlphaMask`.

9. `LilToonMultiResolution.cs`:
   - Retarget `ModeProperty = "_TransparentMode"` to `LilToonProperties.TransparentMode`.

10. `LilToonSharedEligibility.cs`:
    - Retarget render-state literals (`_ZTest`, `_ZWrite`, `_ColorMask`, `_OffsetFactor`, `_OffsetUnits`, `_BlendOp`, `_SrcBlend`, `_DstBlend`, `_BlendOpAlpha`, `_SrcBlendAlpha`, `_DstBlendAlpha`, `_SrcBlendFA`, `_DstBlendFA`, `_BlendOpFA`, `_BlendOpAlphaFA`, `_Cutoff`) to `LilToonProperties.*`.

11. `LilToonOpaqueTarget.cs`:
    - Retarget render-state tuples in `CanonicalOpaqueProperties` and `_TransparentMode` to `LilToonProperties.*`.

12. `LilToonMaterialSemantics.cs`:
    - Retarget raw literal `_DistanceFade` to `LilToonProperties.DistanceFade`.
    - Retarget dictionary keys in `FeatureLabels` to `LilToonProperties.*`.

### Component 3: Root Semantics Hygiene

In `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/EffectiveRenderState.cs:30`:
- Replace raw literal `"RenderType"` with `CanonicalOpaqueVerification.RenderTypeTagName`.

### Component 4: Architectural Invariant Tests

In `<repo-root>/Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs`:
- Assert that `LilToonProperties` constants all start with an underscore and remain distinct across all 48 fields.

---

## 6. Safety, Security, and Risk Analysis

### Safety and Invariants
All string values remain identical.
The refactoring is mechanical.
It replaces duplicate local constants and string literals with centralized typed constants.
The change introduces zero behavioral divergence.

### Performance
The changes eliminate duplicate string literal instances in metadata.
Accessing internal `const string` values compiles directly to `ldstr` instructions.
The change introduces zero runtime overhead and zero heap allocations.

### Ponytail Compliance
The changes delete redundant private constants.
The changes remove raw string literals.
The changes add zero abstractions, zero wrappers, and zero dependencies.
The design complies with the Ponytail ladder.
