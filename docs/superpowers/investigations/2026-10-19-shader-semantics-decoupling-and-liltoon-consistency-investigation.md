# Shader Semantics Decoupling and lilToon Consistency Investigation

Status as of 2026-10-19.

## 1. Summary and Verdict

This investigation evaluates the architecture across the entire `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/` folder.
The review evaluates two core questions:
1. Do `lilToon` and `Poiyomi` declare and centralize shader property names with architectural symmetry?
2. Do the frontends maintain sufficient decoupling to support future shader revisions without mutual entanglement?

### The Verdict

1. Cross-vendor decoupling between `Poiyomi/` and `LilToon/` is complete:
   `Poiyomi/` and `LilToon/` isolate their types from each other completely.
   There are zero cross-vendor type dependencies, zero cross-vendor namespace imports, and zero shared vendor constants.
   The core value algebra in `MaterialSemantics.cs` is strictly mathematical and vendor-agnostic.
   A future replacement or upgrade of either frontend will not impact the other frontend.

2. lilToon has incomplete property centralization:
   `LilToonProperties.cs` defines 27 properties.
   Twenty-five private and internal constants across six lilToon source files duplicate properties in `LilToonProperties`.
   Two or more lilToon files read 16 shared properties as raw string literals.
   AMUSE must expand `LilToonProperties.cs` and retarget all lilToon callers to achieve full architectural symmetry with Poiyomi.

---

## 2. Cross-Vendor Decoupling and Future Extensibility

The investigation analyzed how AMUSE isolates vendor frontends from the core engine:

### 2.1 Cross-Vendor Independence
- `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/` contains 5 source files.
- `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/LilToon/` contains 20 source files.
- The inspection confirmed zero cross-references between the two directories.
- `Poiyomi/` has no knowledge of `LilToon/`.
- `LilToon/` has no knowledge of `Poiyomi/`.

### 2.2 Core Algebra Decoupling
- `MaterialSemantics.cs` defines the shader-independent value algebra.
- The types `ScalarSemanticValue`, `AffineAlphaMap`, and `UvMapping` contain zero vendor property names and zero shader names.
- Both frontends translate vendor-specific material properties into these vendor-agnostic mathematical structures.
- Callers in `<repo-root>/Packages/com.alrauna.amuse/Editor/Analysis/` consume only these vendor-agnostic models.

### 2.3 Dispatcher Seam in `UnityMaterialSemantics.cs`
- `UnityMaterialSemantics.cs` is the central dispatcher.
- It identifies shader families by exact name matching in `ClassifyShaderName`.
- It gathers evidence through vendor-specific capture schemas.
- It verifies cryptographic attestation via vendor delegates (`IsAttestedAlphaMaterial`).
- A new frontend touches only `UnityMaterialSemantics.cs`. It requires zero edits to the existing frontend.

---

## 3. lilToon Property Consistency Audit

The audit measured property usage across all 20 source files in `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/LilToon/`:

### 3.1 Duplicate Constants in lilToon
The audit discovered 25 constants that duplicate properties in `LilToonProperties`:
- Four files duplicate `_Cutoff` (`LilToonAlphaInterpreter.cs:29`, `LilToonCutoutSourceEligibility.cs:46`, `LilToonMultiSourceEligibility.cs:41`, `LilToonTransparentSourceEligibility.cs:91`).
- Four files duplicate `_AlphaMaskMode` (`LilToonAlphaInterpreter.cs:33`, `LilToonAlphaMaskSemantics.cs:100`, `LilToonMultiModeGate.cs:76`, `LilToonMultiSourceEligibility.cs:42`).
- Two files duplicate `_UseDither` (`LilToonMultiModeGate.cs:75`, `LilToonMultiSourceEligibility.cs:43`).
- Two files duplicate `_AlphaBoostFA` (`LilToonMultiSourceEligibility.cs:44`, `LilToonTransparentSourceEligibility.cs:92`).
- Two files duplicate `_SubpassCutoff` (`LilToonMultiSourceEligibility.cs:45`, `LilToonTransparentSourceEligibility.cs:93`).
- Three files duplicate `_DissolveParams` (`LilToonAlphaInterpreter.cs:26`, `LilToonMultiModeGate.cs:77`, `LilToonMultiSourceEligibility.cs:46`).
- `LilToonAlphaInterpreter.cs:30-31` duplicates `_MainTex` and `_Color`.
- `LilToonAlphaInterpreter.cs:27-28` duplicates `_MainTex_ScrollRotate`.
- `LilToonAlphaMaskSemantics.cs:101-102` duplicates `_AlphaMaskScale` and `_AlphaMaskValue`.
- `LilToonMultiModeGate.cs:101-108` duplicates `_UseParallax`, `_UseMain2ndTex`, and `_UseMain3rdTex`.

### 3.2 Shared Properties Missing from `LilToonProperties`
The audit identified 16 properties that two or more files read as raw string literals:
1. Six files read `_DistanceFade` (`LilToonCutoutSourceEligibility.cs:51`, `LilToonDistanceFadeSemantics.cs:35`, `LilToonMaterialSemantics.cs:380`, `LilToonMultiModeGate.cs:78`, `LilToonMultiSourceEligibility.cs:50`, `LilToonTransparentSourceEligibility.cs:94`).
2. Two files read `_TransparentMode` (`LilToonMultiResolution.cs:49`, `LilToonOpaqueTarget.cs:142`).
3. Two files read `_UseClippingCanceller` (`LilToonMultiModeGate.cs:73`, `LilToonMultiSourceEligibility.cs:48`).
4. Two files read `_AsOverlay` (`LilToonMultiModeGate.cs:74`, `LilToonMultiSourceEligibility.cs:49`).
5. Two files read `_OutlineTexHSVG` (`LilToonMultiModeGate.cs:79`, `LilToonMultiSourceEligibility.cs:51`).
6. Two files read `_AlphaMask` (`LilToonAlphaMaskSemantics.cs:103`, `LilToonAlphaEvidenceRequests.cs:225`).
7. `LilToonSharedEligibility.cs` and `LilToonOpaqueTarget.cs` share 10 render-state properties: `_ZTest`, `_ZWrite`, `_ColorMask`, `_BlendOp`, `_SrcBlend`, `_DstBlend`, `_BlendOpAlpha`, `_SrcBlendAlpha`, `_DstBlendAlpha`, and `_OffsetFactor`.

---

## 4. Root Semantics Folder Hygiene

The audit inspected all root files in `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/`:
1. `EffectiveRenderState.cs:30` uses a raw string literal `"RenderType"`.
   `CanonicalOpaqueVerification.RenderTypeTagName` defines this constant.
   AMUSE must retarget `EffectiveRenderState.cs` to use this existing constant.

---

## 5. Recommended Actions

1. Expand `LilToonProperties.cs`:
   Add the 16 shared properties identified in Section 3.2 to `LilToonProperties.cs`.
2. Retarget lilToon Callers:
   Retarget all callers in `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/LilToon/` to `LilToonProperties.*`.
   Delete the 25 redundant constants.
3. Retarget Root Hygiene Finding:
   Replace `"RenderType"` in `EffectiveRenderState.cs` with `CanonicalOpaqueVerification.RenderTypeTagName`.
4. Update Invariant Tests:
   Update `ShaderSemanticsArchitectureTests.cs` to assert that all constants in `LilToonProperties` match the expected naming rules and remain unique.
