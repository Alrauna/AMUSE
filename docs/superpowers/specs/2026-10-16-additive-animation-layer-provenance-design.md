# Additive Animation Layer Provenance and Renderer Proof Admission Design

Date: 2026-10-16

Base: `main` at `1fd711b`. Work branch: `additive-animation-layer-proofing`.

Investigation: `docs/superpowers/investigations/2026-10-16-additive-animation-layer-proof-investigation.md`

## Privacy Note

This record characterizes private assets in the Census Lab editor instance.
This document sanitizes all avatar, renderer, material, texture, animation, controller, and hierarchy names.
It describes assets by role.
It expresses exact counts as ranges.

---

## 1. Context and Problem Statement

On 2026-10-16, an investigation established why AMUSE refuses renderers with `RendererAnalysisRefusal.AdditiveLayerWithProofRelevantMaterialProperty`.
The refusal fires when the captured evidence carries one graph-wide boolean, `HasAdditiveLayer`, and the renderer has any proof-relevant animated material property.
The two facts need no overlap.
The SDK default controllers give almost every real avatar build graph at least one additive layer.
The refusal therefore reduces in practice to "any renderer with any proof-relevant animated material property".
On the Census Lab test avatar this refused a small group of renderers whose shaders AMUSE fully supports.
Their material curves are attributed to override layers by inference.
The additive layers in that graph are consistent with pose-only content.

The capture seam destroys clip-to-layer attribution by design.
The layer walk at `Editor/Host/UnityAnimationEvidenceCapture.cs` sees each committed layer's blending mode before it flattens clips into the immutable record.
The record keeps only per-clip bindings and two graph-wide booleans.

The value algebra admits only animated values exactly equal to serialized defaults.
It has no vocabulary for a bounded additive offset.
The investigation concluded that per-clip additive provenance is the small, sound admission path.
Refuse when an additive layer's own clip animates a proof-relevant float property of this renderer, or when such a clip carries a material-slot object binding on this renderer.
Everything else proceeds.
The singleton algebra stays closed and unchanged.

The object-binding part of the predicate tightens the behavior as implemented on 2026-10-16.
On 2026-10-16, object reference curves on additive layers are admitted into the material swap closure with no additive consideration.
The investigation recorded this as a potential soundness gap in the opposite direction.
The renderer-scope predicate in this design closes that gap for the renderer proof.
Unity's documented runtime behavior for object curves under additive blending remains unverified.
Refusing is the conservative direction.
Increased uncertainty must never make a transformation more aggressive.

## 2. Goals and Non-Goals

### Goals

1. Restore per-clip additive provenance at the capture seam.
   Each captured clip record carries whether it was observed on an additive layer.
2. Narrow the renderer-scope refusal to clips that actually carry the additive fact.
3. Narrow the slot-scope conversion twin the same way.
4. Keep the refusal values, message strings, census vocabulary, and report plumbing unchanged.
5. Add RED/GREEN regression tests that fail on a plausible wrong implementation.

### Non-Goals

1. No additive offset algebra.
   The proof never computes base plus weighted additive contributions.
2. No provenance for `HasUnnormalizedDirectBlendTree`.
   That sibling flag keeps its graph-wide shape.
   The provenance seam this design adds is the natural landing point for a later twin fix, and nothing more.
3. No layer weight modeling, no mask modeling, no runtime sampling.
4. No changes to refusal enum values, string tables, census categories, report writers, play-mode rows, or dry-run structure.
5. No verification of Unity additive float math.
   This design never relies on it, because it refuses every additive clip that animates a relevant property.

## 3. Architectural Design and Invariants

### Invariant 1: Provenance Is Captured Where It Is Known

The capture walk iterates `graph.Layers` and then `layer.Clips`.
A committed layer carries its full `BlendingMode`.
The immutable record therefore gains the fact per clip, at the same step where it is observed.
No new graph walk, no second enumeration, no NDMF API change.

### Invariant 2: Shared Clips Are Marked Conservatively

One clip can be referenced from several layers.
The walk observes one clip record per occurrence.
An occurrence under an additive layer is marked additive.
An occurrence of the same clip under an override layer is not.
The proof sees both occurrences.
The additive occurrence triggers the refusal on its own, so a shared clip can never slip through by the order of observations.

Index-sourced observations carry the additive fact `false`.
The committed graph walk enumerates states, nested state machines, and blend trees exhaustively from the committed controllers (`Editor/Host/CommittedControllerGraph.cs:212-281`).
The animation index only holds clips committed into those same controllers before the barrier enumerates them.
Every index clip is therefore a duplicate of a correctly provenanced layer occurrence.
Duplicate occurrences all survive into the record, so the additive occurrence still fires the refusal.
This containment argument is pinned by an index-route capture test in section 6.
A future capture route that is not derived from the committed controllers must build its own provenance.

### Invariant 3: Refusal Predicates Narrow, Values Stay

The renderer-scope refusal keeps `RendererAnalysisRefusal.AdditiveLayerWithProofRelevantMaterialProperty` with its existing strings.
Its predicate changes from "any RendererWide float binding and any additive layer anywhere" to exactly:

- some RendererWide float binding originates from a clip observed on an additive layer, or
- some material-slot object binding on a clip observed on an additive layer parses against this renderer.

The parse and renderer-path match are the same `parseMaterialSlotBinding` delegate call that `MaterialSlotsFor` uses at `Editor/Analysis/RuntimeStateResolution.cs:503-507`.
The new object check runs after the existing unrecognized-object check, so unrecognized syntax keeps its own fail-closed refusal.
The structural refusal delegate is untouched.
Its call site at `Editor/Analysis/RuntimeStateResolution.cs:162` keeps the raw flat lists, and the flatten loop projects the binding out of its pair to feed it.
`// ponytail: weight-blind; a weight-0 additive layer refuses its own clips; model weights only if Census shows coverage loss`

The slot-scope twin keeps `AlphaSeparationSlotRefusal.ConversionStateUnderAdditiveLayer`.
Its predicate changes the same way for conversion-relevant float bindings, inside the per-clip loop at `Editor/Build/AlphaSeparationPreparation.cs:253-300`.
The twin needs no object-binding check.
A material-slot object binding from an additive clip refuses the renderer before preparation runs, so the twin can never observe one.

### Invariant 4: The Graph-Wide Additive Boolean Dies

With provenance in place, `CapturedAnimationEvidence.HasAdditiveLayer` has no consumer.
It is removed together with its constructor parameter and the additive half of the OR-ing loop at `Editor/Host/UnityAnimationEvidenceCapture.cs:448-457`.
`HasUnnormalizedDirectBlendTree` keeps its current shape.
Clean cutover.
No alias, no deprecated shim.

### Invariant 5: The Proof Algebra Stays Closed

No change to `AdmittedMaterialStates`, `AlphaSemanticsResolver`, the classifier, or the outcome intersector.
An additive clip that animates a relevant property refuses.
An additive clip that does not is transparent to the proof.
Both answers stay inside the singleton-over-serialized-defaults model.

### Invariant 6: One Implementation For Build And Dry Run

The dry run reruns the identical capture and resolution.
It needs no change and gains the narrowed predicates automatically.

### Invariant 7: Minimal Implementation (Ponytail)

The change threads one boolean through records that already exist.
It deletes one graph-wide boolean.
It touches two predicates.
It adds no abstraction, no registry, no strategy, no configuration.

## 4. Data Model Changes

All types are internal.

### `CapturedClipEvidence` (`Editor/Host/CapturedAnimationEvidence.cs:83-103`)

- Gains `bool isFromAdditiveLayer` as the second constructor parameter.
- Gains `internal bool IsFromAdditiveLayer { get; }`.
- XML doc states the conservative meaning: true when this clip occurrence was observed on a layer whose blending mode is additive. Layer weight is not modeled.

### `LiveClipObservation` (`Editor/Host/LiveAnimationObservation.cs:172`)

- Gains `bool isFromAdditiveLayer` as a required constructor parameter and an `internal bool IsFromAdditiveLayer { get; }` property.
- The parameter is required, not optional.
  An optional parameter would let every hand-built observation below default silently.
- `CaptureObserved` builds `CapturedClipEvidence` purely from the observation (`Editor/Host/UnityAnimationEvidenceCapture.cs:781-785`), so the observation is the only threading point.
- Production construction: `LiveAnimationObservation.ObserveClip` and `LiveAnimationObservation.ObserveVirtualClip`.
  Hand-built constructions in tests: `UnityAnimationEvidenceCaptureTests.cs:302,1970,2000,2234,2274,2296` plus the local helpers.
  All are listed in section 6's migration list.

### `LiveAnimationObservation.ObserveClip` and `ObserveVirtualClip` (`Editor/Host/UnityAnimationEvidenceCapture.cs`)

- Both factories gain the additive fact as a required parameter, so the observation twins keep matching signatures.
- `CaptureGraph` passes `layer.BlendingMode == AnimatorLayerBlendingMode.Additive` for each clip it observes.
- `CaptureWithAnimationIndex` passes the same fact in its committed-layer walk (`:322-330`).
  Its index loop passes `false` for every `ObserveVirtualClip` observation, with the containment argument of invariant 2.
  `// ponytail: index occurrences marked false; the committed walk is the provenance authority; build real virtual provenance if a capture route stops deriving from the committed controllers`

### `CapturedAnimationEvidence` (`Editor/Host/CapturedAnimationEvidence.cs:176-232`)

- Loses the `hasAdditiveLayer` constructor parameter and the `HasAdditiveLayer` property.
- Loses the additive term in the capture-side OR-ing loop.
- `HasUnnormalizedDirectBlendTree` keeps its positional slot.
  Only the trailing additive bool is deleted.
- `HasUnnormalizedDirectBlendTree` is otherwise untouched.

### The refusal's documentation

- The XML doc on `RendererAnalysisRefusal.AdditiveLayerWithProofRelevantMaterialProperty` (`Editor/Host/UnityRendererAlphaAnalysis.cs:32-36`) is rewritten.
  It states the new meaning: the refusal names a renderer with a proof-relevant float binding or material-slot object binding whose clip was observed on an additive layer.
  The enum member name, the string table entries, and all report plumbing stay unchanged.

## 5. Predicate Changes

### Renderer scope (`Editor/Analysis/RuntimeStateResolution.cs:155-220`)

The flatten loop records, per float binding, whether its source clip is additive.
A RendererWide classification from an additive-source binding sets a renderer-scope flag.
After the existing unrecognized-object loop, the resolution adds one object pass over additive-source clips only: any binding for which `parseMaterialSlotBinding` returns true refuses.
The additive refusal then fires on that flag or that object pass.
The unnormalized-direct-blend-tree check that follows keeps its exact form as of 2026-10-16.

The structural refusal delegate keeps its signature.
The flatten loop projects the binding out of the pair when it calls the delegate.

### Slot scope (`Editor/Build/AlphaSeparationPreparation.cs:249-300`)

The conversion loop already iterates clips.
It marks bindings from additive clips.
The twin refusal fires when any conversion-relevant binding is additive-sourced.

## 6. Test Specification

Existing tests that stay valid without modification:

- `RuntimeStateIntegration_AdditiveLayerWithRelevantPropertyRefuses` (`Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:4073`).
  The additive layer animates the relevant property, so the refusal must survive.
- `ConversionStateUnderAdditiveLayer_RefusesEveryCandidateSlot` (`Tests/Editor/Build/AlphaSeparationPreparationTests.cs:577`).
  The conversion-relevant curve sits on the additive layer itself.

Existing tests that move with the design:

- `RuntimeStateIntegration_AdditiveFlagAloneDoesNotRefuseRenderer` (`:4752`) is rewritten into integration Test 3 below.
  Its fixture is exactly the additive material swap: the only layer carries an `m_Materials.Array.data[0]` object curve (`AddAnimatedMaterialAssignment`, `:5934-5958`) and is set additive (`:4764,5861`).
  Under the new object pass that renderer refuses.
  This is a deliberate, documented behavior change, not a weakened test.
  The old pin's purpose, flag alone does not refuse, is carried by Test 1's control case.
  The rewritten test keeps the fixture-precondition assertions on per-clip provenance from `:4096,4774`.
- The capture tests that assert both graph-wide booleans (`Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs:958-977,1296-1327`) keep only the blend-tree boolean assertions.
  `GraphBlendFactsSurviveIntoImmutableEvidence` builds a graph whose additive layer has no clips, so a provenance assertion there would be vacuous.
  The additive-provenance derivation moves into `ProductionGraphRouteObservesEveryClipAndHostSpecialFact` (`:980`), the real production-route test.
  That test grows an assertion that the additive clip's per-clip provenance survives the index-containing route.
- Direct `ObserveClip` and `ObserveVirtualClip` test callers pass `false`: `UnityAnimationEvidenceCaptureTests.cs:107,220,369-370,2027` and the `ObservationWithMaterialSwap` helper, plus `LiveAnimationObservationVirtualClipTests.cs:59,100,116`.
  `ObserveVirtualClipMatchesObserveClipSemantics` (`:53-62`) passes the same fact to both twins.
- Hand-built `LiveClipObservation` constructions (`UnityAnimationEvidenceCaptureTests.cs:302,1970,2000,2234,2274,2296` plus local helpers) pass the required constructor argument.
- The hand-built evidence fixtures in `Tests/Editor/Analysis/RuntimeStateResolutionTests.cs` and the direct-record constructions in `Tests/Editor/Build/AlphaSeparationApplyTests.cs:3754,3839,6152,6325` pass the new constructor argument.
  Both new parameters are required, so omission fails compilation.

New and rewritten behavior tests:

0. **Index occurrences carry no provenance. Characterization guard and Falsifier 4 pin.**
   A committed-graph Override layer animates a proof-relevant property of the fixture renderer while the animation index observes the same clip through `CaptureWithAnimationIndex`.
   The renderer resolves with no additive refusal.
   `--- Falsifier 4: an implementation that defaults index-sourced occurrences to true re-widens the refusal for every renderer whose clips the index observes twice. This test must fail it. ---`
1. **Pose layer does not poison an override-animated renderer. RED.**
   A controller whose first layer is Override animates a proof-relevant material property of the fixture renderer.
   A second, additive layer carries a transform-only clip.
   Before: the renderer refuses with `AdditiveLayerWithProofRelevantMaterialProperty`.
   After: no additive renderer refusal fires, and the renderer's own analysis outcome matches an otherwise identical fixture without the additive layer.
2. **Additive float animation still refuses. Characterization guard.**
   The additive layer animates the proof-relevant property.
   The refusal must stay.
   This passes on the implementation as of 2026-10-16 too.
   It is regression protection against overshoot, not RED evidence.
3. **Additive material swap refuses. RED, from the rewritten `:4752` fixture.**
   The additive layer carries an `m_Materials.Array.data[i]` object curve on the fixture renderer.
   On 2026-10-16, no additive refusal fires and the swap enters the closure.
   After: the renderer refuses with `AdditiveLayerWithProofRelevantMaterialProperty`.
4. **Conversion twin narrows. RED.**
   A conversion-relevant curve sits on an Override layer while a pose-only additive layer exists.
   Before: every candidate slot refuses with `ConversionStateUnderAdditiveLayer`.
   After: no additive slot refusal fires.
5. **Shared clip conservatism. Characterization guard.**
   One clip animating a proof-relevant property is referenced from both the additive layer and an Override layer.
   The Override layer is enumerated before the additive layer, so a first-observation-wins implementation is caught.
   The refusal must fire.
   `--- Falsifier 1: an implementation that marks provenance from the first observation only, or that drops additive occurrences when the same clip also appears on an Override layer, lets the additive contribution through. This test must fail it. ---`

Falsifier hypotheses for the production change:

`--- Falsifier 2: an implementation that narrows the renderer predicate but forgets the object-binding pass leaves the additive material swap admitted. Test 3 must fail it. ---`
`--- Falsifier 3: an implementation that narrows the slot twin by renderer-scope facts alone, or that keeps reading the deleted graph-wide boolean through a leftover alias, either over-refuses (test 1) or fails to compile. ---`

Every falsifier test carries a no-op guard so a later refactor cannot silence it silently.

## 7. Security, Safety, and Privacy

1. **Safety**: the change only narrows when a refusal fires, except the additive material-swap case where it widens the refusal.
   That widening closes a recorded soundness gap and moves toward the conservative direction.
   Source assets are never mutated.
   The mutation boundary of the apply pass is untouched.
2. **Privacy**: tests use minimal synthetic schemas under `Hidden/Alrauna/AmuseTests/*`.
   No private assets enter the repository.
3. **Determinism**: all new tests are EditMode tests with deterministic counts.
4. **Fail closed**: unrecognized binding syntax keeps its renderer-wide refusal ahead of the additive checks.
   Capture-time closure failures keep their slot scope.

## 8. Risks

1. **Additive material-swap refusal may reduce coverage on some avatars.**
   A renderer whose materials are swapped only by additive layers loses optimization it incorrectly gained before.
   The investigation recorded the admission as implemented on 2026-10-16 as a potential soundness gap.
   The refusal names the additive layer, so an author can move the curve.
2. **Fixture drift.**
   The record shape change touches several hand-built fixtures.
   Both new constructor parameters are required, so omissions are compile errors.
3. **Unity additive float math stays unverified.**
   This design does not depend on it.
   A future design that admits nonzero additive contributions must verify the math first.
4. **Index containment is load-bearing.**
   The `false` default for index-sourced observations rests on the containment argument of invariant 2.
   The production-route capture test pins it.
   A capture route that stops deriving from the committed controllers must build real virtual provenance.
