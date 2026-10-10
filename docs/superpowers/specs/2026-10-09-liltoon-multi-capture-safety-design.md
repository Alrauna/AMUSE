# lilToon Multi Capture Safety: Design and Specification

Date: 2026-10-09.
Branch plan: fix/latent-bugs-and-impurities. Base: fae7e7e.
No source assets are modified.
No vendor shader is installed.

Privacy note: this document contains no private avatar, renderer, material, scene, or machine identity. Public vendor shader identifiers stay exact.

Labels: `[SOURCE]` is a fact read from cited code lines in this repository.
`[INFERENCE]` is a conclusion from evidence.

---

## 1. Summary

This design fixes two latent defects in the lilToon Multi capture path.

Finding 1 makes the Multi capture request carry every property name the conversion eligibility evaluator reads. Without the union, every admissible Multi conversion throws a programming-defect exception at build time.

Finding 2 closes the family request switch for the Multi family and adds a field-predicate agreement gate to the transparent frontend. Without the closure and the gate, a Multi material resolved to transparent mode can prove triangles opaque from a binarized field that lies about exact texel alpha.

Both changes are capture-schema and interpretation changes.
They touch no asset and no vendor shader.
They keep the fail-closed direction of every existing refusal.

---

## 2. Background and Motivation

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs:67-89`.
The Multi family captures under `MultiEvidenceRequest` alone.
That request combines three alpha requests and the mode-gate request.
It names none of the eighteen canonical recipe scalars.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:44-69, 102-112`.
The recipe scalars are `_SrcBlend`, `_DstBlend`, `_AlphaToMask`, `_ZWrite`, `_ZTest`, `_OffsetFactor`, `_OffsetUnits`, `_ColorMask`, `_SrcBlendAlpha`, `_DstBlendAlpha`, `_BlendOp`, `_BlendOpAlpha`, `_SrcBlendFA`, `_DstBlendFA`, `_SrcBlendAlphaFA`, `_DstBlendAlphaFA`, `_BlendOpFA`, and `_BlendOpAlphaFA`.
`RecipeEvidenceRequest` requests exactly those names as presence and scalars.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiSourceEligibility.cs:180-186, 217-226, 288-301`.
`ConversionEvidenceRequest` combines `RecipeEvidenceRequest`, `SourceEvidenceRequest`, and `MultiStateEvidenceRequest`.
Its comment states the purpose: capture and conversion reads cannot drift apart.
No production call site uses it.
The only use is the test helper `CaptureMultiConversion` in `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonMultiSourceEligibilityTests.cs:144-154`.
The eligibility evaluator `EvaluateVerifiedEligibility` walks `EligibilitySchema`, which starts with `LilToonOpaqueTarget.RecipeSchemaProperties`, and reads each name through `evidence.TryGetScalar`.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:621-635, 833-837`.
`TryGetScalar` throws `ArgumentException` with the message `"Property '<name>' was not requested."` for a name outside the capture request.
A name that is requested but absent from the material returns false instead and yields the named refusal `ConversionPropertyAbsent`.

`[SOURCE]` investigation Finding 1, `docs/superpowers/investigations/2026-10-09-third-latent-bugs-and-architectural-impurities-investigation.md:37-55`.
A resolved, fully proven Multi material reaches eligibility and the schema loop throws.
The same throw fires earlier in admission and in the runtime overwrite loop when a recipe property is animated.
The failure direction is build fatal.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:802-831, 836-851, 404-416`.
`AlphaRequestForFamily` has cases for the Poiyomi families and the regular lilToon families.
It has no `LilToonMulti` case, and the default arm returns null.
`AlphaPredicateRequestFor` reaches that null through its default arm for the Multi classification family.
`CaptureBatch` builds every material's predicate from that method before capture runs.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:1147-1160, 1194-1211`.
`DeclaresCutoffFor` treats a null predicate as "keep the union request declaration" and returns true.
The union contains the cutout member, whose own request declares the `_Cutoff` binarization for `_MainTex` (`Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaEvidenceRequests.cs:175-186`).
The transparent member declares no cutoff for `_MainTex` (same file, lines 188-213).
So every Multi capture binarizes the `_MainTex` alpha field by the live `_Cutoff`, whatever mode the resolution later produces.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:105-134, 529-551`.
The constructor guard stores the resolved regular family on a resolved Multi capture and keeps `LilToonMulti` only for a refusal.
So analysis-time request lookups for a resolved Multi already receive a regular family request, while a refused Multi keeps the `LilToonMulti` family and reaches the null.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1355-1363`.
The Poiyomi frontend guards the same hazard with a field-predicate agreement check.
A non-cutout claim over a field captured under a cutoff threshold refuses naming `_Cutoff` before any field read.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs:224-321`.
The shared lilToon alpha interpreter builds the main alpha sample from the assigned `_MainTex` under the exact-255 exact-one rule.
The lilToon frontends have no equivalent of the Poiyomi capture-threshold check.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/AlphaFieldSet.cs:162-256`.
`TryGetFor` scopes the deciding texture assignments by the caller's request and derives the field key from the captured assignment itself.
The key carries the capture threshold, so a binarized field is served whenever the request asks the channel.
The investigation marks the downstream direction as needing runtime confirmation: misclassification (wrongly opaque) or over-refusal (Unknown).

---

## 3. Detailed Design

### 3.1 Finding 1: One Capture Request Serves Capture and Conversion Reads

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs`:
Add `LilToonMultiSourceEligibility.ConversionEvidenceRequest` as the fifth member of the `MultiEvidenceRequest` combination:

```csharp
internal static readonly MaterialEvidenceRequest
    MultiEvidenceRequest =
        MaterialEvidenceRequest.Combine(
            LilToonMaterialSemantics.AlphaEvidenceRequest,
            LilToonCutoutMaterialSemantics.AlphaEvidenceRequest,
            LilToonTransparentMaterialSemantics.AlphaEvidenceRequest,
            LilToonMultiSourceEligibility.ConversionEvidenceRequest,
            new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: BuildMultiScalarSchema(),
                colorProperties: Array.Empty<string>(),
                vectorProperties:
                    LilToonMultiModeGate.ConsultedVectorProperties,
                textureProperties: BuildMultiTextureRequests(),
                captureKeywords: true));
```

The added member names the eighteen recipe scalars, the mirrored source facts, and the Multi state facts.
`MaterialEvidenceRequest.Combine` unions scalar names and keeps one cutoff declaration per texture property.
It throws only when one property declares two different cutoff scalars.
The added member declares no texture property, so the combination introduces no cutoff conflict.

The eligibility schema loop then finds every name requested.
A recipe property the material lacks returns false and yields the named refusal `ConversionPropertyAbsent`.
A recipe property the material carries is captured and evaluated.
The admission path and the runtime overwrite loop read the same evidence object, so the union heals those sites without separate edits.

The doc comment above the request gains one sentence.
It names the eligibility evaluator as the second conversion reader the request serves, beside the resolved-mode interpreters.

### 3.2 Finding 2, Part A: Close the Family Request Switch

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`:
Add the missing case to `AlphaRequestForFamily`:

```csharp
case CapturedAlphaMaterialFamily.LilToonMulti:
    return LilToonMultiResolution.MultiEvidenceRequest;
```

Consequences at the three caller groups:

1. `AlphaPredicateRequestFor` default arm at capture time.
   The predicate for a Multi classification becomes the union instead of null.
   `DeclaresCutoffFor` then consults the union's own `_MainTex` declaration, which carries the cutout member's `_Cutoff`.
   The captured bytes stay binarized.
   The declaration is now named instead of falling out of the null fallback.
2. The analyzer field-provider sites in `Editor/Analysis/AdmittedMaterialStates.cs:296-308` and `Editor/Host/UnityRendererAlphaAnalysis.cs:687-707`.
   A refused Multi material keeps the `LilToonMulti` family.
   Those sites now receive a non-null scoping request, so the lookup consults exactly the assignments the union asks.
3. Resolved Multi materials are unaffected here.
   Their stored family is the resolved regular family, by the constructor guard.

### 3.3 Finding 2, Part B: The Resolved-Mode Field-Predicate Gate

The transparent frontend gains the Poiyomi field-predicate agreement check.
In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs`:
Add one parameter to `Interpret`:

```csharp
internal static SemanticOutput<ScalarSemanticValue> Interpret(
    CapturedMaterialEvidence evidence,
    List<LilToonSemanticDiagnostic> diagnostics,
    string[] coverageGates,
    float maxProvableCutoff,
    LilToonAlphaGate[] transparentOnlyGates,
    bool requiresExactMainField)
```

Inside the assigned-texture arm, after the `SampledAlphaIsProvenOne` short-circuit and after the source-identity refusal, add:

```csharp
if (requiresExactMainField &&
    assignment.Texture.CaptureThreshold < 1f)
{
    return RecordUnknown<ScalarSemanticValue>(
        diagnostics,
        LilToonSemanticOutput.Alpha,
        LilToonSemanticDiagnosticCode.UnsupportedFeature,
        CutoffProperty);
}
```

The placement is load-bearing.
The proven-one importer theorem arm stays ahead of the gate, because that theorem reads no field and stays valid under any capture predicate.
The identity refusal stays ahead of the gate, because the field key needs the source identity.
The gate stands before the field read, so a disagreeing predicate refuses before any byte is consumed.

The two bindings change:

1. `LilToonCutoutMaterialSemantics.InterpretCutoutAlpha` passes `false`.
   The cutout family's own request declares the cutoff, and the cutout theorem reads the binarized field by its own rule.
2. `LilToonTransparentMaterialSemantics.InterpretTransparentAlpha` passes `true`.

Why the transparent arm needs the gate:
A Multi material resolved to transparent mode carries a transparent claim over a field binarized by the union's cutout declaration.
Byte 255 then means "alpha at or above the cutoff".
The transparent exact-255 rule reads it as "alpha exactly one".
An alpha 0.5 texel with `_Cutoff` 0.3 stores 255 and would prove wrongly opaque.
The gate refuses naming `_Cutoff` before any field read, exactly as `PoiyomiMaterialSemantics.cs:1355-1363` does for the Poiyomi frontend.

The gate is inert on the regular transparent path.
The transparent family's own request declares no cutoff for `_MainTex`, so a regular transparent capture keeps `CaptureThreshold` at 1.
An unassigned `_MainTex` takes the declared-default arm before the gate and never reaches it.

Accepted outcome:
A transparent-resolved Multi with an assigned `_MainTex` and a declared sub-one cutoff refuses with a named reason instead of proving triangles from a lying field.
This is the fail-closed direction.
Lifting that refusal would need a mode-aware capture predicate, which is outside this fix.

### 3.4 Runtime Confirmation of the Current Downstream Direction

The investigation verifies the switch cases and the Poiyomi precedent.
It leaves the downstream direction unconfirmed.
The plan's first task runs a probe before any production change:

1. Mint a Multi container stand-in at mode 2 with `_MainTex` assigned at alpha 0.5 and `_Cutoff` at 0.3.
2. Capture under the production request with the production predicate.
3. Resolve through the profile injection seam to the transparent family.
4. Analyze, gather the production field set, resolve, and classify one triangle.
5. Report the observed outcome.

`[INFERENCE]` The served field key carries the capture threshold 0.3, and the transparent request asks the `_MainTex` alpha channel, so the lookup serves the binarized field and the resolver reads 255 as exactly one.
The expected observation is a proven-opaque misclassification.
The probe records what actually runs.
The observed outcome goes into the comment of the pinned refusal test.
The pinned expectation is the fail-closed refusal either way.

No existing pinned test bakes the pre-fix behavior.
The probe itself is the characterization, and the plan says so in the task.

### 3.5 Cross-Pair Touchpoints

`Editor/Semantics/UnityMaterialSemantics.cs` is shared with pair 6 (semantics robustness, Finding 7).
Pair 1 edits only `AlphaRequestForFamily` at lines 802-831 and, through its default arm, the predicate path at lines 836-851.
Pair 6 edits the fold helpers elsewhere in the file.
Pair 1 runs first in the execution order, so pair 6 rebases on these regions.

`Editor/Build/AlphaSeparationPreparation.cs` is cited by Finding 1 but modified by no task in this plan.
The widened evidence heals its reads.
Pair 4 owns the Build edits.

No test fixture factory changes.
Both committed Multi container stand-ins, `LilToonMultiCutoutTest.shader` and `LilToonMultiTransparentTest.shader`, already declare the eighteen recipe scalars.

---

## 4. Verification and Test Plan

1. Unit test `ProductionMultiRequestCarriesTheCanonicalRecipeSchema`:
   Assert every name of `LilToonOpaqueTarget.RecipeSchemaProperties` sits in `LilToonMultiResolution.MultiEvidenceRequest.ScalarProperties`.
2. Unit test `ProductionMultiCaptureEvaluatesModeOneEligibilityToConvertible`:
   Capture the committed cutout container stand-in under the production request and assert `EvaluateVerifiedEligibility` returns `Convertible` at mode 1 without a throw.
3. Unit test `ProductionMultiCaptureEvaluatesModeTwoEligibilityToConvertible`:
   Same shape for the committed transparent container stand-in at mode 2.
4. Unit test `AlphaRequestForTheMultiFamilyIsTheCombinedMultiRequest`:
   Assert `AlphaRequestForFamily(LilToonMulti)` and `AlphaPredicateRequestFor(material, LilToonMulti)` return `LilToonMultiResolution.MultiEvidenceRequest`.
5. Unit test `BinarizedMainFieldUnderTheTransparentClaimRefusesNamingCutoff`:
   Capture a transparent-claim fixture material with an assigned alpha-0.5 `_MainTex` and `_Cutoff` 0.3 under the production request.
   Assert the transparent interpretation stays Unknown with an `UnsupportedFeature` diagnostic naming `_Cutoff`.
6. Unit test `ResolvedTransparentMultiWithBinarizedMainFieldStaysUnknownNamingCutoff`:
   The converted probe.
   Assert the seam-resolved transparent Multi analyzes to all-Unknown alpha with an `UnsupportedFeature` reason naming `_Cutoff`.
7. Regression sweep:
   Run `LilToonMultiSourceEligibilityTests`, `LilToonMultiResolutionTests`, `LilToonMultiModeGateTests`, `LilToonTransparentAlphaTests`, `LilToonCutoutAlphaTests`, `UnityMaterialSemanticsTests`, and `AlphaSeparationPreparationTests` in the Unity Test Runner, EditMode.
   Every run must report more than zero tests.
   A filtered run that reports 0 tests is a failure of the verification step, not a pass.
