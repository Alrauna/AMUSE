# Shader Semantics Decoupling and lilToon Consistency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish complete architectural symmetry between lilToon and Poiyomi by centralizing shared properties in `LilToonProperties.cs`, eliminating 25 duplicate constants, retargeting raw string literals, and enforcing architectural invariants.

**Architecture:** Maintain complete cross-vendor decoupling between `Editor/Semantics/Poiyomi/` and `Editor/Semantics/LilToon/`. Expand `LilToonProperties.cs` with 21 missing shared properties. Retarget all lilToon callers to `LilToonProperties.*` and delete redundant private constants across all schema arrays and gate methods. Retarget `EffectiveRenderState.cs` to reuse `CanonicalOpaqueVerification.RenderTypeTagName`. Update architectural invariant tests in `ShaderSemanticsArchitectureTests.cs`.

**Tech Stack:** C# 9.0, Unity 2022.3 Editor API, NUnit constraint model (`Assert.That`).

**Spec:** `docs/superpowers/specs/2026-10-19-shader-semantics-decoupling-and-liltoon-consistency-design.md`

## Global Constraints

- Retain compiled C# as the sole source of truth for shader semantics.
- No runtime JSON loaders, no asset deserializers, and no dynamic expression interpreters.
- Cross-vendor independence: `Editor/Semantics/LilToon/` must never import or reference types from `Editor/Semantics/Poiyomi/`, and vice versa.
- Compacting static attestation tables in `LilToonSourceAttestation.cs` into binary spans is deferred to a future change.
- No changes to mathematical formulas, rational intervals, or UV classification logic in `Editor/Analysis/`.
- No changes to `docs/architecture/vision.md`.
- No changes to the runtime component `AmuseAvatarOptimizer.cs`.
- No changes to Unity scene files, prefabs, materials, or asset GUIDs.
- No new external package dependencies.
- Zero behavior changes to existing material classification, refusal reporting, or opaque conversion recipes.
- Simple English / ASD-STE100 rules apply to all documentation: short active sentences, one idea per sentence, no semicolons, and no contractions.
- Never record machine-specific paths, hostnames, usernames, ports, or at-sign-hash identifiers.

---

### Task 1: Expand `LilToonProperties.cs` and Retarget Distance Fade, Alpha Mask, and Evidence Requests

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonProperties.cs:35-41`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonDistanceFadeSemantics.cs:33-36`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaEvidenceRequests.cs:224-226`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs:98-106`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs`

**Interfaces:**
- Consumes: Nothing
- Produces: 21 new string constants on `LilToonProperties`:
  `DistanceFade`, `TransparentMode`, `UseClippingCanceller`, `AsOverlay`, `OutlineTexHsvg`, `AlphaMask`, `ZTest`, `ZWrite`, `ColorMask`, `BlendOp`, `SrcBlend`, `DstBlend`, `BlendOpAlpha`, `SrcBlendAlpha`, `DstBlendAlpha`, `OffsetFactor`, `OffsetUnits`, `SrcBlendFa`, `DstBlendFa`, `BlendOpFa`, `BlendOpAlphaFa`.

- [ ] **Step 1: Expand `LilToonProperties.cs`**

Modify `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonProperties.cs` to add the 21 constants:

```csharp
        internal const string DistanceFade = "_DistanceFade";
        internal const string TransparentMode = "_TransparentMode";
        internal const string UseClippingCanceller = "_UseClippingCanceller";
        internal const string AsOverlay = "_AsOverlay";
        internal const string OutlineTexHsvg = "_OutlineTexHSVG";
        internal const string AlphaMask = "_AlphaMask";
        internal const string ZTest = "_ZTest";
        internal const string ZWrite = "_ZWrite";
        internal const string ColorMask = "_ColorMask";
        internal const string BlendOp = "_BlendOp";
        internal const string SrcBlend = "_SrcBlend";
        internal const string DstBlend = "_DstBlend";
        internal const string BlendOpAlpha = "_BlendOpAlpha";
        internal const string SrcBlendAlpha = "_SrcBlendAlpha";
        internal const string DstBlendAlpha = "_DstBlendAlpha";
        internal const string OffsetFactor = "_OffsetFactor";
        internal const string OffsetUnits = "_OffsetUnits";
        internal const string SrcBlendFa = "_SrcBlendFA";
        internal const string DstBlendFa = "_DstBlendFA";
        internal const string BlendOpFa = "_BlendOpFA";
        internal const string BlendOpAlphaFa = "_BlendOpAlphaFA";
```

- [ ] **Step 2: Retarget `LilToonDistanceFadeSemantics.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonDistanceFadeSemantics.cs`:
Retarget `DistanceFadeProperty` to forward to `LilToonProperties.DistanceFade`:
```csharp
        internal const string DistanceFadeProperty =
            LilToonProperties.DistanceFade;
```

- [ ] **Step 3: Retarget `LilToonAlphaEvidenceRequests.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaEvidenceRequests.cs:225`:
Replace raw literal `"_AlphaMask"` with `LilToonProperties.AlphaMask`.

- [ ] **Step 4: Retarget `LilToonAlphaMaskSemantics.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs`:
Delete internal constants `ModeProperty`, `ScaleProperty`, `ValueProperty`, `MaskProperty`.
Retarget internal usages to `LilToonProperties.AlphaMaskMode`, `LilToonProperties.AlphaMaskScale`, `LilToonProperties.AlphaMaskValue`, and `LilToonProperties.AlphaMask`.

- [ ] **Step 5: Run existing tests to verify compilation and passing status**

Run `LilToonAlphaMaskSemanticsTests` and `LilToonTransparentAlphaTests`.
Expected: PASS.

- [ ] **Step 6: Commit Task 1**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonProperties.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonDistanceFadeSemantics.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaEvidenceRequests.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs
git commit -m "refactor(semantics): expand LilToonProperties and retarget mask and fade evidence"
```

---

### Task 2: Retarget Alpha Interpreter, Eligibility Checkers, and Multi-Mode Gate

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs:25-35`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs:45-60`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs:90-115`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiSourceEligibility.cs:40-165`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiModeGate.cs:72-110`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiResolutionTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs`

**Interfaces:**
- Consumes: `LilToonProperties`
- Produces: Retargeted callers across all schema arrays and gate checks with deleted duplicate constants.

- [ ] **Step 1: Retarget `LilToonAlphaInterpreter.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs`:
1. Delete private constants: `DissolveParamsProperty`, `MainTexScrollRotateProperty`, `CutoffProperty`, `ColorProperty`, `MainTextureProperty`, and `AlphaMaskModeProperty`.
2. Retarget all usages across the class to `LilToonProperties.*`.
3. In lines 204 and 211, replace `LilToonDistanceFadeSemantics.DistanceFadeProperty` with `LilToonProperties.DistanceFade`.

- [ ] **Step 2: Retarget `LilToonCutoutSourceEligibility.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs`:
1. Delete private constant `CutoffProperty`.
2. In `SourcePresenceSchema`, `SourceSchema` (line 57), and `Evaluate`, retarget `CutoffProperty` to `LilToonProperties.Cutoff` and `_DistanceFade` to `LilToonProperties.DistanceFade`.

- [ ] **Step 3: Retarget `LilToonTransparentSourceEligibility.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs`:
1. Delete private constants: `CutoffProperty`, `AlphaBoostFaProperty`, `SubpassCutoffProperty`, and `DistanceFadeProperty`.
2. In `SourcePresenceSchema`, `SourceSchema` (line 104), `SourceVectorSchema` (line 109), and `Evaluate`, retarget all usages to `LilToonProperties.*`.

- [ ] **Step 4: Retarget `LilToonMultiSourceEligibility.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiSourceEligibility.cs`:
1. Delete 10 private constants: `CutoffProperty`, `AlphaMaskModeProperty`, `DitherProperty`, `AlphaBoostFaProperty`, `SubpassCutoffProperty`, `DissolveParamsProperty`, `ClippingCancellerProperty`, `OverlayProperty`, `DistanceFadeProperty`, and `OutlineToneProperty`.
2. Retarget usages across all schemas (`SourcePresenceSchema`, `CutoutSourceSchema`, `TransparentSourceSchema`, `SourceSchema`, `SourceVectorSchema`, `MultiStateEvidenceRequest`), and `Evaluate` to `LilToonProperties.*`.

- [ ] **Step 5: Retarget `LilToonMultiModeGate.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiModeGate.cs`:
1. Delete 10 private constants: `ClippingCancellerProperty`, `OverlayProperty`, `DitherProperty`, `AlphaMaskModeProperty`, `DissolveParamsProperty`, `DistanceFadeProperty`, `OutlineToneProperty`, `UseParallaxProperty`, `UseMain2ndTexProperty`, and `UseMain3rdTexProperty`.
2. Retarget usages across `LilToonMultiModeGate.cs` to `LilToonProperties.*`.

- [ ] **Step 6: Run existing lilToon tests to verify compilation and passing status**

Run `LilToonCutoutAlphaTests`, `LilToonMultiResolutionTests`, and `LilToonTransparentAlphaTests`.
Expected: PASS.

- [ ] **Step 7: Commit Task 2**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiSourceEligibility.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiModeGate.cs
git commit -m "refactor(semantics): retarget lilToon interpreter and eligibility gates to LilToonProperties"
```

---

### Task 3: Retarget Multi-Resolution, Shared Eligibility, Opaque Target, and Root Hygiene

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs:48-50,95-100,360-365`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSharedEligibility.cs:50-165`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:55-78,140-145,265-285`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs:360-400`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/EffectiveRenderState.cs:28-32`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs:80-100`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueConversionTests.cs`

**Interfaces:**
- Consumes: `LilToonProperties`, `CanonicalOpaqueVerification`
- Produces: Fully centralized property usages across all semantics files.

- [ ] **Step 1: Retarget `LilToonMultiResolution.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs`:
1. Delete private constant `ModeProperty`.
2. In lines 98 and 362, replace `ModeProperty` with `LilToonProperties.TransparentMode`.

- [ ] **Step 2: Retarget `LilToonSharedEligibility.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSharedEligibility.cs`:
Replace raw string literals in gates 5-12 with `LilToonProperties.*`:
- `"_ZTest"` -> `LilToonProperties.ZTest`
- `"_ZWrite"` -> `LilToonProperties.ZWrite`
- `"_ColorMask"` -> `LilToonProperties.ColorMask`
- `"_OffsetFactor"` -> `LilToonProperties.OffsetFactor`
- `"_OffsetUnits"` -> `LilToonProperties.OffsetUnits`
- `"_BlendOp"` -> `LilToonProperties.BlendOp`
- `"_SrcBlend"` -> `LilToonProperties.SrcBlend`
- `"_DstBlend"` -> `LilToonProperties.DstBlend`
- `"_BlendOpAlpha"` -> `LilToonProperties.BlendOpAlpha`
- `"_SrcBlendAlpha"` -> `LilToonProperties.SrcBlendAlpha`
- `"_DstBlendAlpha"` -> `LilToonProperties.DstBlendAlpha`
- `"_SrcBlendFA"` -> `LilToonProperties.SrcBlendFa`
- `"_DstBlendFA"` -> `LilToonProperties.DstBlendFa`
- `"_BlendOpFA"` -> `LilToonProperties.BlendOpFa`
- `"_BlendOpAlphaFA"` -> `LilToonProperties.BlendOpAlphaFa`
- `"_Cutoff"` -> `LilToonProperties.Cutoff`

- [ ] **Step 3: Retarget `LilToonOpaqueTarget.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`:
1. In `CanonicalOpaqueProperties` array (lines 57-76), retarget shared property literals to `LilToonProperties.*`:
   - `("_SrcBlend", 1f)` -> `(LilToonProperties.SrcBlend, 1f)`
   - `("_DstBlend", 0f)` -> `(LilToonProperties.DstBlend, 0f)`
   - `("_ZWrite", 1f)` -> `(LilToonProperties.ZWrite, 1f)`
   - `("_ZTest", 4f)` -> `(LilToonProperties.ZTest, 4f)`
   - `("_OffsetFactor", 0f)` -> `(LilToonProperties.OffsetFactor, 0f)`
   - `("_OffsetUnits", 0f)` -> `(LilToonProperties.OffsetUnits, 0f)`
   - `("_ColorMask", 15f)` -> `(LilToonProperties.ColorMask, 15f)`
   - `("_SrcBlendAlpha", 1f)` -> `(LilToonProperties.SrcBlendAlpha, 1f)`
   - `("_DstBlendAlpha", 10f)` -> `(LilToonProperties.DstBlendAlpha, 10f)`
   - `("_BlendOp", 0f)` -> `(LilToonProperties.BlendOp, 0f)`
   - `("_BlendOpAlpha", 0f)` -> `(LilToonProperties.BlendOpAlpha, 0f)`
   - `("_SrcBlendFA", 1f)` -> `(LilToonProperties.SrcBlendFa, 1f)`
   - `("_DstBlendFA", 1f)` -> `(LilToonProperties.DstBlendFa, 1f)`
   - `("_BlendOpFA", 4f)` -> `(LilToonProperties.BlendOpFa, 4f)`
   - `("_BlendOpAlphaFA", 4f)` -> `(LilToonProperties.BlendOpAlphaFa, 4f)`
   Single-use properties (`_AlphaToMask`, `_SrcBlendAlphaFA`, `_DstBlendAlphaFA`) remain local string literals in this array per Rule 2.
2. In lines 142, 268, and 280, replace `"_TransparentMode"` with `LilToonProperties.TransparentMode`.

- [ ] **Step 4: Retarget `LilToonMaterialSemantics.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`:
In `FeatureLabels` dictionary (lines 360-397), retarget shared string literals to `LilToonProperties.*`:
- `"_Invisible"` -> `LilToonProperties.Invisible`
- `"_UDIMDiscardCompile"` -> `LilToonProperties.UdimDiscardCompile`
- `"_IDMask1"` -> `LilToonProperties.IdMask1`
- `"_IDMask2"` -> `LilToonProperties.IdMask2`
- `"_IDMask3"` -> `LilToonProperties.IdMask3`
- `"_IDMask4"` -> `LilToonProperties.IdMask4`
- `"_IDMask5"` -> `LilToonProperties.IdMask5`
- `"_IDMask6"` -> `LilToonProperties.IdMask6`
- `"_IDMask7"` -> `LilToonProperties.IdMask7`
- `"_IDMask8"` -> `LilToonProperties.IdMask8`
- `"_IDMaskControlsDissolve"` -> `LilToonProperties.IdMaskControlsDissolve`
- `"_UseMain2ndTex"` -> `LilToonProperties.UseMain2ndTex`
- `"_UseMain3rdTex"` -> `LilToonProperties.UseMain3rdTex`
- `"_UseDither"` -> `LilToonProperties.UseDither`
- `"_UseParallax"` -> `LilToonProperties.UseParallax`
- `"_AlphaMaskMode"` -> `LilToonProperties.AlphaMaskMode`
- `"_DissolveParams"` -> `LilToonProperties.DissolveParams`
- `"_DistanceFade"` -> `LilToonProperties.DistanceFade`

- [ ] **Step 5: Retarget `EffectiveRenderState.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/EffectiveRenderState.cs:30`:
Replace `"RenderType"` with `CanonicalOpaqueVerification.RenderTypeTagName`.

- [ ] **Step 6: Update Architectural Test in `ShaderSemanticsArchitectureTests.cs`**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs`:
Ensure `LilToonPropertiesMatchShaderConventions` asserts:
```csharp
            Assert.That(fields.Length, Is.EqualTo(48),
                "LilToonProperties must contain exactly 48 centralized constants.");
```

- [ ] **Step 7: Run all Semantics tests in Unity Test Runner**

Run all tests under `Alrauna.Amuse.Tests.Editor.Semantics`.
Expected: 1,116/1,116 tests PASS.

- [ ] **Step 8: Commit Task 3**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSharedEligibility.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs Packages/com.alrauna.amuse/Editor/Semantics/EffectiveRenderState.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs
git commit -m "refactor(semantics): complete lilToon property retargeting and root hygiene"
```
