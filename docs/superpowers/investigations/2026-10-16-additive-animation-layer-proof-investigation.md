# Investigation: Additive Animation Layers That Touch Renderer Alpha Proofs

Date: 2026-10-16

## Privacy Note

This record characterizes private assets in the Census Lab editor instance.
This document sanitizes all avatar, renderer, material, texture, animation, controller, and hierarchy names.
It describes assets by role.
It expresses exact counts as ranges.
The private project is referenced by role as the Census Lab project.
Public package names and code symbols are included.

---

## 1. Executive Summary

On 2026-10-16, an investigation examined why AMUSE refuses certain renderers with the NDMF console message "This avatar has an additive animation layer that touches this renderer".
The refusal key is `amuse.renderer.AdditiveLayerWithProofRelevantMaterialProperty`.
The investigation established these facts.
Items marked as inference carry lower confidence.
Each names its evidence chain.

1. The refusal fires when two things hold at once.
   The captured evidence says some layer of some controller is additive.
   And at least one float curve that addresses a proof-relevant material property of the analyzed renderer exists anywhere in the graph.
   The check needs no overlap between the two.
   The additive fact is one graph-wide boolean.
   The curves carry no layer attribution.

2. The additive fact is almost always present.
   The VRChat SDK ships default playable controllers whose layers include an additive muscle layer.
   NDMF's platform bindings enumerate or repair those default controllers during the build.
   Almost every real avatar build graph therefore carries at least one additive layer.
   Fully customized playable slots and upstream flattening are the exceptions that section 5 records.

3. The proof-relevant material curves on the Census Lab renderers are attributed to override layers by inference, not by observation.
   The build graph's additive layers are consistent with pose-only content.
   The build artifacts that could confirm the attribution are not text-scannable.
   Section 2 records that limitation and section 5 records the inference chain.

4. The result is a refusal that is much wider than its stated cause.
   A renderer whose material properties are animated by ordinary override layers is refused because a pose layer somewhere else in the graph happens to be additive.
   The refusal stops the whole renderer before any slot analysis.

5. The refusal exists because the capture layer deliberately drops per-layer provenance.
   `CapturedAnimationEvidence` keeps two graph-wide booleans for layer structure.
   Clips keep no layer index and no blending mode.
   The pure proof pipeline cannot express a bounded additive offset, so the capture refuses early instead.

6. A sound, small admission path exists.
   Restore per-clip additive provenance at the capture seam.
   Refuse when an additive layer's own clips animate proof-relevant float properties of this renderer, or when those clips carry material-slot object bindings on this renderer.
   Pose layers, muscle layers, and additive layers that touch no renderer material stay transparent to the proof.
   The conservative singleton value algebra stays unchanged.

The investigation also found two adjacent facts that this work must not silently absorb.
They are recorded in sections 6 and 7.

---

## 2. Evidence from the Census Lab Editor Instance

The observation below comes from a read-only session in the Census Lab editor instance on 2026-10-16.
The session read the NDMF error report and the animator data of the loaded test avatar.
The session modified no files.
It started no build.

### 2.1 The Optimizer Component

The avatar root carries the AMUSE optimizer component.
The alpha separator is enabled.
All fields match the component defaults except a small number of strict alpha thresholds.
A quality preset relaxed those thresholds from 100 to the mid-90s.

### 2.2 The NDMF Console

The last build of the test avatar produced an NDMF report with a large number of informational refusals.
Among them was a small group of renderers, between two and four, refused with `AdditiveLayerWithProofRelevantMaterialProperty`.
The refused renderers were mesh renderers or skinned mesh renderers.
Their material slot counts varied between one and four.
All of their materials use lilToon transparent, outline, or cutout family build clone shaders.
All of the renderers are enabled in the default state.
All of their shaders belong to shader families AMUSE attests and supports.
These are renderers AMUSE could otherwise analyze and prove.
The additive refusal alone stops them.

The same report showed the usual neighboring refusals on other renderers.
Those were unrelated reasons.
Unsupported third-party shaders and slot-count mismatches accounted for the rest.

### 2.3 The Authored Animator Graph

A read-only walk of the descriptor's playable controllers found:

- Between 40 and 50 layers across the playable controllers.
- Between 100 and 110 distinct clips.
- Between 1000 and 1100 distinct renderer material-property float bindings.
- No additive layer in any authored descriptor controller.

The additive layers observed in the NDMF report are therefore not authored in the descriptor controllers AMUSE walks by default.
They enter the graph during the build.
The VRChat SDK ships default playable controllers.
NDMF's platform bindings enumerate or repair those defaults at build scope activation.
That mechanism is documented in the repository SDK audit (`docs/task-6-vrchat-sdk-3.10.4-source-audit.md`).
Upstream tooling also clones authored controller assets into the build graph.
Section 5 shows that no installed tool synthesizes additive material curves.

The walk covered the descriptor's playable controllers only.
It did not cover build artifacts.
The build-time FX artifact of this avatar is too large for text scanning in the available tools.
Its layer blending modes could not be observed directly.

---

## 3. Codebase Analysis

Three parallel audits mapped the AMUSE code on 2026-10-16.
All paths are relative to `Packages/com.alrauna.amuse`.

### 3.1 Where the Refusal Originates

- The refusal enum value is `RendererAnalysisRefusal.AdditiveLayerWithProofRelevantMaterialProperty` (`Editor/Host/UnityRendererAlphaAnalysis.cs:37`).
  Its doc comment states the design reason.
  Captured graph facts carry no per-binding provenance, so version 1 cannot prove that the additive contribution leaves this renderer's singleton property value intact.
- The check runs in `RuntimeStateResolution.ResolveRuntimeStates` (`Editor/Analysis/RuntimeStateResolution.cs:206-212`).
  It fires when `relevantBindings.Count > 0` and `evidence.HasAdditiveLayer`.
  `relevantBindings` contains only float curves classified RendererWide.
  The refusal is renderer-scoped.
  It stops the renderer before any per-slot work.
- The console message comes from the string table (`Editor/Build/AmuseReportStrings.cs:104-110,148-150`).
  The key is built from the enum name (`AmuseReportStrings.RendererKey`, `Editor/Build/AmuseReportStrings.cs:748-751`).
- A slot-scope twin exists for the conversion path.
  `AlphaSeparationPreparation` refuses every candidate slot with `AlphaSeparationSlotRefusal.ConversionStateUnderAdditiveLayer` (`Editor/Build/AlphaSeparationPreparation.cs:292-300`, `Editor/Build/AlphaSeparationRecords.cs:105`).

### 3.2 Where Provenance Is Lost

- The structural graph check enumerates the committed controllers before virtualization.
  Its `CommittedLayer` records keep the full blending mode and layer index (`Editor/Host/CommittedControllerGraph.cs:25-52,216-247`).
  Layer weight is never read.
- The per-renderer evidence capture walks layers and then clips (`Editor/Host/UnityAnimationEvidenceCapture.cs:359-396`).
  At the record-building step it collapses layer structure into two graph-wide booleans (`:448-457`):
  `HasAdditiveLayer` and `HasUnnormalizedDirectBlendTree`.
  Both are OR-ed across every layer of every controller.
- The immutable record keeps per-clip `Name`, `IsSpecialMotion`, `FloatBindings`, and `ObjectBindings` (`CapturedClipEvidence`, `Editor/Host/CapturedAnimationEvidence.cs:83-103`).
  It keeps no layer index, no blending mode, and no layer name.
  Clip-to-layer attribution is destroyed at capture.

### 3.3 How the Proof Consumes the Evidence

- `ResolveRuntimeStates` flattens every clip's float and object bindings (`Editor/Analysis/RuntimeStateResolution.cs:155-160`).
- `ResolveProofRelevant` classifies each float binding (`Editor/Host/UnityAnimationEvidenceCapture.cs:917-976`).
  Only `material.<P>` curves that address a proof-relevant property of this renderer become RendererWide.
  Transform and visibility curves become Irrelevant.
  Indexed `material[i].<P>` syntax fails closed as UnrecognizedMaterialBinding.
- The additive check only sees RendererWide float bindings.
  The distinction between material curves and other curves is real and tested.
  `RuntimeStateIntegration_AdditiveFlagAloneDoesNotRefuseRenderer` pins that the flag alone does not refuse (`Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:4752`).
- There is no positive layer-structure proof anywhere in the pipeline.
  The value algebra is singleton-over-captured-defaults.
  `AdmittedMaterialStates.AdmitScalar` admits an animated value only when it is finite and equal to the serialized default (`Editor/Analysis/AdmittedMaterialStates.cs:956-981`).
  Disagreement is `AnimatedMaterialPropertyNotSingleton`.
  The algebra has no vocabulary for a bounded additive offset.

### 3.4 The Seams an Admission Path Can Use

- The capture already walks layers before it flattens them.
  Per-clip additive provenance is available at `Editor/Host/UnityAnimationEvidenceCapture.cs:448-457` and is dropped only there.
- The production delegates injected at the barrier (`Editor/Build/AmusePlatformFinishPlugin.cs:887-899`) are the same shape the pure tests inject (`Tests/Editor/Analysis/RuntimeStateResolutionTests.cs:36-41`).
- The edit-time dry run reruns the identical capture and resolution (`Editor/DryRun/AlphaSeparationDryRun.cs:24-38,887,1341,1475`).
  Any change must keep the dry run and the build on one implementation.
- The research census mirrors `RendererAnalysisRefusal` exhaustively (`Packages/com.alrauna.amuse.research/Editor/Collection/CensusVocabulary.cs:22-70`).
  Parity tests fail when the enums drift.

### 3.5 The Consumer Checklist for the Refusal Enums

A new or changed refusal value touches, at minimum:

- The string table entries and the key builders (`Editor/Build/AmuseReportStrings.cs`).
- The report writers (`Editor/Build/AmuseReports.cs:492-517,214-306`).
  `AmuseReports.SlotAnalysisRefusal` (`Editor/Build/AmuseReports.cs:41-57`) also consumes the renderer enum through a constant key, so it needs no new table entry but must stay total.
- The census buckets sized by `Enum.GetValues` (`Editor/Build/AmusePlatformFinishPlugin.cs:90-152`).
- The play-mode row writers (`Editor/Build/AmusePlatformFinishPlugin.cs:1449-1478,1575-1579`).
- The dry-run refusal rows (`Editor/DryRun/AlphaSeparationDryRun.cs:621-625,1474-1478`).
- The research census mirror: a member in `Packages/com.alrauna.amuse.research/Editor/Census/CensusCategories.cs:43-48`, the exhaustive `CensusVocabulary.ToCensus` arm (`Packages/com.alrauna.amuse.research/Editor/Collection/CensusVocabulary.cs:22-70`), and the parity tests (`CensusVocabularyTests.cs:42-46,97-102`, `CensusCategorySnapshotTests.cs:41-42`).
- The renderer-cause string completeness test (`Tests/Editor/Build/AmuseReportStringsTests.cs:20-42`) and the slot twin's completeness test (`Tests/Editor/Build/AmuseReportsSlotTests.cs:415-438`).
- The slot twin's apply-side validation (`Editor/Build/AlphaSeparationApply.cs:683-777`) and the transient-unlock writers (`Editor/Build/TransientUnlockSwapIn.cs:209-212,509-515`, `Editor/Build/TransientUnlockWindowClose.cs:165-170`).

### 3.6 Existing Tests Around the Refusal

- `RuntimeStateIntegration_AdditiveLayerWithRelevantPropertyRefuses` (`Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:4073`) builds a real controller whose first layer is additive and asserts the refusal.
- `RuntimeStateIntegration_AdditiveFlagAloneDoesNotRefuseRenderer` (`:4752`) pins that the flag alone does not refuse.
- Three refusal-report tests use the additive fixture as a precondition (`:4207,4322,4444`).
- `ConversionStateUnderAdditiveLayer_RefusesEveryCandidateSlot` (`Tests/Editor/Build/AlphaSeparationPreparationTests.cs:577`) covers the slot twin.
- Capture-level tests pin that both graph-wide booleans survive into immutable evidence (`Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs:958,1296`).
  A production-route test walks every clip with an additive layer present (`:980`).
- Schema-only stand-in tests hand-build `CapturedAnimationEvidence` records (`Tests/Editor/Analysis/RuntimeStateResolutionTests.cs`).
  The record shape change must keep these compilable and honest.

---

## 4. NDMF Platform Analysis

The audit read the restored NDMF 1.14.4 source in `Packages/nadena.dev.ndmf` on 2026-10-16.

1. AMUSE runs in the PlatformFinish phase.
   The virtual controller context clones every innate controller at scope activation.
   The clone already contains every layer upstream tools committed.
   The committed graph AMUSE sees is the complete post-tooling graph.
2. `VirtualLayer` exposes `BlendingMode` and `DefaultWeight` (`Editor/API/AnimatorServices/VirtualObjects/VirtualLayer.cs:48,56`).
   No parameter-driven layer weight exists in the model.
   Runtime weight changes come only from state behaviours such as `VRCAnimatorLayerControl`.
   NDMF preserves those behaviours opaquely and remaps their layer indices only.
3. NDMF provides no facility that samples or composes layer contributions.
   There is no weight application, blend tree sampling, or layer composition anywhere in the package.
   A consumer that wants the effective additive contribution must compose it itself.
4. The committed controller preserves each layer's serialized blending mode, default weight, and mask.
   Final material values emerge only from Unity runtime evaluation.
   A build-time proof must therefore reason from the structural facts NDMF supplies.
5. Avatar masks carry a path-to-weight map for bones.
   Material-property curves are not bone-gated in the mask model.
   The audit found no evidence that masks scale generic property contributions.

---

## 5. Upstream Tool Analysis

The audit read the package sources installed in the Census Lab project on 2026-10-16.
The headline conclusion:

**No installed tool creates additive layers from code.**
Every additive layer in the ecosystem comes from an authored controller asset.
Build-time tooling preserves authored blending modes while cloning layers into the build graph.

### 5.1 Per-Tool Findings

|Tool|Creates additive layers?|Preserves authored additive layers?|Material curves on additive layers?|
|---|---|---|---|
|VRChat SDK|No. Ships a default playable controller whose one layer is additive, authored weight 0, muscles only.|Not applicable.|None.|
|VRCFury|No. Layer creation defaults to Override.|Yes. Clone paths copy blending mode.|Never. Its generated material curves sit on Override layers.|
|GogoLoco|No. Ships authored additive pose layers.|Via full-controller cloning.|None. Its only material-animating clips live on Override layers.|
|AAO|No. Created layers are Override.|Yes. Clone preserves blending mode.|Models additive semantics for analysis, including material floats and swaps.|
|d4rk|No. Its merged layer is Override.|Yes for kept layers. See the hazard in 5.2.|Not verified.|
|Modular Avatar|No. Clone preserves blending mode.|Yes.|Forces Write Defaults on for additive merged layers.|

### 5.2 An Observed Upstream Hazard

The d4rk merge eligibility analysis has no blending-mode guard.
An additive FX layer that otherwise qualifies for merging would be flattened into an Override Write-Defaults-On direct blend tree.
That conversion silently changes semantics.
This is an upstream latent defect, not an AMUSE defect.
AMUSE must expect post-tooling graphs whose additive layers may already have been flattened or preserved.
No fix belongs in this work.
The fact is recorded because it bounds what a build-time proof can assume about layer semantics.

### 5.3 Prior Art in a Mature Tool

AAO models additive layers for its own analysis.
Its parser tracks layer weights and implements additive semantics for float and material-swap properties.
Two rules matter as prior art.
An additive layer with a single possible value contributes a constant.
A weight-0 layer may still matter under Write Defaults, so AAO keeps a caveat rather than skipping it blindly.
AAO also collects layer-control behaviours to model scripted weight changes.
This is evidence that ecosystem tooling treats per-layer provenance and weight facts as necessary inputs for sound material analysis.

### 5.4 The Always-Present Additive Layer

The SDK default additive playable layer enters almost every humanoid build graph.
NDMF's default-controller handling enumerates or repairs the SDK defaults at build scope activation.
The authored weight of that layer is 0.
An observed build clone carried it at weight 1.
Either way, a graph-wide additive flag is true for almost all real avatars.
Fully customized playable slots without default layers are one exception.
Upstream flattening of additive layers is the other exception, recorded in section 5.2.
For almost all real avatars the additive refusal therefore reduces in practice to "any renderer with any proof-relevant animated material property".

---

## 6. Root Cause

The refusal as implemented on 2026-10-16 is sound but far wider than its stated cause.

1. The capture collapses layer structure into one graph-wide boolean.
2. The proof algebra admits only values exactly equal to serialized defaults.
3. An additive contribution could shift any animated value away from its default.
4. Version 1 resolved this uncertainty by refusing the whole renderer whenever any additive layer existed and any proof-relevant material curve existed.

The stated cause, "an additive animation layer that touches this renderer", is true in only a subset of refused cases.
In the common case the additive layer animates poses only, and the material curves live on override layers.
Unknown information invalidates conclusions that depend on it.
A pose layer's blending mode does not depend on anything the alpha proof consumes.
The refusal as implemented on 2026-10-16 expands a local fact into renderer-wide refusal.

The capture seam already sees everything needed to fix this.
The layer walk knows each clip's blending mode before it flattens the graph.

### The Adjacent Object-Curve Question

The additive check guards float bindings only.
On 2026-10-16, object reference curves (material swaps) on an additive layer are admitted into the reachable material closure with no additive consideration.
The audit could not establish Unity's documented runtime behavior for object reference curves under additive blending.
This is a potential soundness gap in the opposite direction from the over-refusal.
Section 7 lists it as an open question.
The admission path in this investigation refuses additive-layer clips that carry material-slot object bindings on the renderer.
That predicate tightens this gap as a side effect.
This work must not widen it.

### The Sibling Flag

`HasUnnormalizedDirectBlendTree` has the same graph-wide shape and the same over-refusal pattern.
Its twin refusal is `UnnormalizedDirectBlendTreeWithProofRelevantMaterialProperty`.
Scope discipline keeps that flag out of this work.
The design should preserve the provenance seam so the same fix pattern can land later.

---

## 7. Open Questions and Risks

1. **Unity additive math for material float curves.**
   The expected runtime semantics are componentwise addition of weighted curve values on top of lower layers.
   This is inferred from ecosystem tooling behavior, not verified against Unity documentation or a runtime sample.
   Any design that admits a nonzero additive contribution must verify this first.
   The provenance-only admission path in this investigation does not depend on the exact math, because it refuses every additive clip that animates a proof-relevant property.
2. **Object reference curves under additive blending.**
   Undocumented.
   On 2026-10-16, AMUSE admits them into the material swap closure with no additive consideration.
   Conservative tightening is possible.
   Verification belongs in characterization before any change.
3. **Layer weight dynamics.**
   State behaviours can adjust layer weights at runtime.
   A provenance-only path that refuses on overlap never reasons about weight, so it stays sound without modeling this.
4. **Avatar masks and generic curves.**
   The mask model gates bones.
   The audit found no evidence that masks scale material-property contributions.
   The provenance-only path does not rely on this either.
5. **Fixture drift.**
   The record shape changes, so hand-built evidence fixtures must gain the new field honestly.
   Schema-only tests that omit it must fail compilation rather than silently default.

---

## 8. Summary of Findings

1. The additive refusal is renderer-scoped and fires on a graph-wide additive boolean plus any proof-relevant animated material property on the renderer.
2. A graph-wide additive boolean is almost always true on real avatars.
   SDK default controllers and pose tooling enter additive layers during the build.
3. The refused Census Lab renderers use supported shader families.
   Their material curves are attributed to override layers by inference.
   The build artifacts that would confirm the attribution are not scannable.
4. The capture seam destroys clip-to-layer attribution by design.
   Restoring a per-clip additive fact at that seam is a small, sound step.
5. Refusing when an additive layer's own clips animate proof-relevant float properties of the renderer, or carry material-slot object bindings on it, removes the over-refusal without weakening any existing guarantee.
   The singleton value algebra stays closed and unchanged.
6. The slot-scope conversion twin needs the same provenance fact.
7. The object-curve question and the sibling blend-tree flag are recorded as adjacent facts.
   Neither belongs in the first fix.
