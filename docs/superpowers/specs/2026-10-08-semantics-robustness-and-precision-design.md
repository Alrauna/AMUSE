# Semantics Robustness and Precision: Design and Specification

Date: 2026-10-08.
Branch plan: fix/latent-bugs-and-impurities. Base: main at c375101.
No source assets are modified.
All mutations remain inside the NDMF build copy.

Privacy note: this document contains no private avatar, renderer, material, scene, or machine identity. Public vendor shader identifiers stay exact.

Labels: `[SOURCE]` is a fact read from cited code lines in this repository.
`[INFERENCE]` is a conclusion from evidence.

---

## 1. Summary

This design resolves three latent bugs and precision flaws in the lilToon Semantics frontend.
The changes ensure that Multi container opaque clones set the `_TransparentMode` property to zero.
The changes add finiteness checks to alpha mask scale and offset properties to eliminate unhandled exceptions.
The changes assign main texture sampler state to blend mask samples in layer alpha calculations and enforce fail-closed behavior when main sampling is unavailable.

All changes preserve strict fail-closed refusal and match shader execution.

---

## 2. Background and Motivation

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:132-163, 254-275`.
When preparing an opaque clone for a Multi container shader, `WriteMultiModeZeroKeywordSet` derives and writes mode zero shader keywords.
However, the method does not set the float property `_TransparentMode` to zero.
The clone retains the original `_TransparentMode` property value of 1 or 2 from the source material.
`LilToonMultiResolution.GateAndMap` evaluates `_TransparentMode` to determine the material mode.
When inspected, the property value indicates cutout or transparent, which contradicts the mode zero keywords.
Furthermore, `TryFindNonCanonicalFact` does not inspect `_TransparentMode`.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs:290-307`.
In `LilToonAlphaMaskSemantics.cs`, `Interpret` checks that `assignment.HasScaleOffset` is true.
It does not verify that `assignment.Scale` and `assignment.Offset` contain finite float values.
Line 305 passes these values to the `UvMapping` constructor.
`UvMapping` validates finiteness and throws an `ArgumentException` when a value is NaN or infinity.
This throws an unhandled exception instead of returning a fail-closed refusal.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonLayerAlphaTerm.cs:360-370`.
Line 368 instantiates the blend mask `TextureSample` using `layerSampling`.
`layerSampling` represents the sampling state of the second or third layer texture.
However, comment lines 361 to 363 state:
"The mask borrows _MainTex's sampler, which is the main assignment's sampling evidence."
If the layer texture uses wrap or filter settings that differ from `_MainTex`, the classifier uses incorrect sampler parameters.
If `_MainTex` is missing or lacks sampling evidence, the method must refuse fail-closed rather than substitute layer sampling.

---

## 3. Detailed Design

### 3.1 Mode Zero Property Write on Multi Container Opaque Clones
In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs`:
Update `PrepareCanonicalOpaqueClone`:
When `attestedTarget == source.shader`, set `_TransparentMode` to zero before keyword derivation:
```csharp
if (attestedTarget == source.shader)
{
    clone.SetFloat("_TransparentMode", 0f);
    WriteMultiModeZeroKeywordSet(clone);
}
```

In `TryFindNonCanonicalFact(Material candidate, out string factName)`:
Add validation for `_TransparentMode` on candidate materials that declare the property:
```csharp
if (candidate.HasProperty("_TransparentMode") && candidate.GetFloat("_TransparentMode") != 0f)
{
    factName = "_TransparentMode";
    return true;
}
```
This guarantees property and keyword consistency on all Multi container opaque clones.

### 3.2 Finiteness Validation in Alpha Mask Interpretation
In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs`:
In `Interpret`, validate finiteness before `UvMapping` instantiation:
```csharp
if (!assignment.HasScaleOffset ||
    !float.IsFinite(assignment.Scale.x) ||
    !float.IsFinite(assignment.Scale.y) ||
    !float.IsFinite(assignment.Offset.x) ||
    !float.IsFinite(assignment.Offset.y))
{
    return Refuse(
        diagnostics,
        LilToonSemanticDiagnosticCode.UnsupportedFeature,
        MaskProperty);
}
```
When non-finite values are present, the method returns a fail-closed refusal.
It logs `LilToonSemanticDiagnosticCode.UnsupportedFeature` and never throws an exception.

### 3.3 Main Texture Sampler Assignment for Blend Masks
In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonLayerAlphaTerm.cs`:
Retrieve the main texture assignment from `evidence`.
Verify that `_MainTex` exists and provides valid sampling evidence.
If `_MainTex` is missing, unassigned, or lacks sampling evidence, refuse fail-closed:
```csharp
if (!evidence.TryGetTexture("_MainTex", out var mainAssignment) ||
    mainAssignment.Texture == null ||
    !mainAssignment.Texture.HasSampling)
{
    return Refuse(diagnostics, "_MainTex");
}

var maskSampling = mainAssignment.Texture.Sampling;
factors.Add((
    new TextureSample(
        blendMask.Texture.SourceIdentity,
        new UvMapping(0, Vector2.one, Vector2.zero),
        maskSampling),
    TextureChannel.Red));
```
This aligns the blend mask sampler state with shader behavior and preserves strict fail-closed safety.

---

## 4. Verification and Test Plan

1. Unit test `MultiContainerOpaqueCloneSetsTransparentModeZero`:
   Create Multi container materials with `_TransparentMode = 1.0f` (cutout) and `_TransparentMode = 2.0f` (transparent).
   Run `PrepareCanonicalOpaqueClone`.
   Assert that `clone.GetFloat("_TransparentMode")` equals `0.0f`.
   Assert that `TryFindNonCanonicalFact` succeeds with no non-canonical facts.
2. Unit test `TryFindNonCanonicalFactCatchesNonZeroTransparentMode`:
   Create a material with `_TransparentMode = 1.0f`.
   Run `TryFindNonCanonicalFact`.
   Assert that the method returns true and sets `factName = "_TransparentMode"`.
3. Unit test `AlphaMaskWithNonFiniteScaleOrOffsetRefusesWithoutException`:
   Create captured material evidence with `_AlphaMask_ST` containing `float.NaN`, `float.PositiveInfinity`, and `float.NegativeInfinity` across scale and offset.
   Run `LilToonAlphaMaskSemantics.Interpret`.
   Assert that the method returns a refused term with `UnsupportedFeature` diagnostic.
   Assert that zero exceptions are thrown.
4. Unit test `BlendMaskUsesMainTexSamplingSettings`:
   Configure `_MainTex` with Clamp wrap mode and `_Main2ndTex` with Repeat wrap mode.
   Evaluate `LilToonLayerAlphaTerm.Interpret`.
   Assert that the blend mask `TextureSample.Sampling` matches `_MainTex` Clamp mode.
5. Unit test `BlendMaskRefusesWhenMainTexLacksSampling`:
   Configure `_MainTex` with null texture or without sampling evidence.
   Evaluate `LilToonLayerAlphaTerm.Interpret`.
   Assert that the method refuses with `_MainTex` diagnostic and returns a refused term.
