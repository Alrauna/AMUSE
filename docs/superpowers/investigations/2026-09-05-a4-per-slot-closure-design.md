# A4 investigation: animation material-dependency closure and per-slot refusal granularity

Date: 2026-09-05. Scope decision: V6 of `docs/superpowers/specs/2026-09-05-0.1.0-scope-design.md`.
Method: read-only code investigation of the current tree at `main` `0493d68`, performed by a
read-only investigation agent; the controller verified the flow anchors and authored this
document. No Census Lab data used. No repo edits by the agent.

## 1. Question

V6 requires material-level unknowns to refuse per material slot, not per renderer. The
animation material-dependency closure is the suspected blocker. The question: is per-slot
refusal granularity viable, what exactly changes, and what are the falsifiers?

## 2. Verdict

**Per-slot refusal granularity is VIABLE.** The architecture already contains every per-slot
mechanism below the closure: named per-slot resolution refusals (`SlotResolutionResult`,
`Editor/Analysis/AdmittedMaterialStates.cs:26-70`), the keep-going slot loop
(`Editor/Build/AmusePlatformFinishPlugin.cs:582-635`), the material-blind planner
(`Editor/Analysis/MeshSeparationPlanner.cs:195-246`), per-slot prepared records and clone
hygiene (`Editor/Build/AlphaSeparationRecords.cs:198-230`,
`Editor/Build/AlphaSeparationPreparation.cs:350-375`), per-slot live revalidation
(`Editor/Build/AlphaSeparationApply.cs:473-533`), and per-slot refusal accounting
(`Editor/Build/AmusePlatformFinishPlugin.cs:41-69`). The closure is the single
all-or-nothing layer. The refactor changes its output from `(failure, evidence | empty)` to
per-slot admission status plus shared evidence, attributes each failure to its owning slots,
and routes it into the existing structures.

## 3. Current flow

All paths are repo-relative under `Packages/com.alrauna.amuse/`.

### Pass layout

`AmusePlatformFinishPlugin.Configure` registers three passes in `BuildPhase.PlatformFinish`
(`Editor/Build/AmusePlatformFinishPlugin.cs:176-191`): bindings capture under
`AnimatorServicesContext` (`:179-184`), the extension-free semantic barrier (`:186-188`),
and `AlphaSeparationApply` under a reactivated `AnimatorServicesContext` (`:189-191`).

### Where the closure is built

- The barrier's renderer loop walks every `Renderer`
  (`Editor/Build/AmusePlatformFinishPlugin.cs:346-355`), refuses structurally broken ones
  first (`UnityRendererAlphaAnalysis.HostStructuralRefusalFor`, `:360-366`), computes
  `rendererPath` via `AnimationUtility.CalculateTransformPath` (`:368-370`), and calls
  `UnityAnimationEvidenceCapture.Capture` with `renderer.sharedMaterials` as `currentSlots`
  (`:375-388`).
- `Capture` (`Editor/Host/UnityAnimationEvidenceCapture.cs:124-140`) delegates to
  `CaptureGraph` (`:183-221`), which observes every clip of every committed layer of the
  whole avatar graph (`:205-215`; the graph is enumerated avatar-wide in
  `Editor/Host/CommittedControllerGraph.cs:88`, `:124`, `:154`), then to `CaptureObserved`
  (`:224-453`).
- `CaptureObserved` builds the admitted set renderer-wide: `TryAdmit` dedupes by reference
  (`:262-280`); current slot materials are admitted first in slot order (`:282-296`, null
  current means `MissingCurrentMaterial` at `:295`); then every
  `m_Materials.Array.data[k]` object binding on this renderer's path is admitted with all
  its keyframe values (`:298-328`; out-of-range slot means `SlotOutOfRange` at `:315-317`;
  a non-`Material` value means `InvalidSwapValue` at `:319-326`). Path filtering is
  `AddressesAnalyzedRenderer`, an ordinal path equality (`:536-551`).
- The failure helper `Failed` (`:242-260`) returns evidence that is empty everywhere but
  still carries the graph-wide `hasAdditiveLayer`/`hasUnnormalizedDirectBlendTree` flags
  (`:229-240`).

### What evidence it admits per slot today

Nothing per-slot at closure time. The per-slot view is derived afterwards by
`MaterialSlotsFor` (`Editor/Build/AmusePlatformFinishPlugin.cs:777-822`): slot k's admitted
index list is its `CurrentMaterialIndices[k]` seed (`:781-790`) plus every admitted index
from bindings parsing to slot k on this renderer's path, in clip, binding, and value order
(`:791-802`). `CapturedMaterialSlotEvidence` exists for every slot; an unanimated slot
carries exactly one index (`Editor/Host/CapturedAnimationEvidence.cs:86-116`).

### Where the renderer-wide refusal fires

`ResolveRuntimeStates` (`Editor/Build/AmusePlatformFinishPlugin.cs:492-505`):
`!evidence.IsClosed` means `Refused(RendererAnalysisRefusal.MaterialDependencyClosureFailed)`
(`:501-505`; enum value at `Editor/Host/UnityRendererAlphaAnalysis.cs:20`). `IsClosed` is
`ClosureFailure == None` (`Editor/Host/CapturedAnimationEvidence.cs:163`). The closure has
five failure modes: `None`, `MissingCurrentMaterial`, `SlotOutOfRange`, `InvalidSwapValue`,
`UnattestedMaterial` (`Editor/Host/CapturedAnimationEvidence.cs:8-15`). The two
`UnattestedMaterial` sites are family-selection failure
(`Editor/Host/UnityAnimationEvidenceCapture.cs:333-350`, `:348`) and the closed-batch
capturer returning false (`:360-374`, `:373`). The capturer is documented as the sole
material-evidence capture and the sole source-attestation decision for the whole admitted
batch (`:38-53`), and its contract says returning false rejects the complete batch with no
partial prefix (`:55-79`).

### What the finish pass retains on failure

The renderer loop consumes the refusal at `:391`, records it with
`state.RecordRendererRefusal(refusal)` (`:415-424`), and continues. Nothing else survives:
no `CaptureGeometry`, no plan, no retained separation, no counters
(`:392-397`, `:426-429`). Later renderers continue.

A test currently pins this scope as renderer-level: a two-submesh renderer whose slot 1
holds an unselectable family yields `RendererRefusalCount(MaterialDependencyClosureFailed)
== 1` and zero in every `AlphaSeparationSlotRefusal` bucket, "renderer-scoped closure, not a
slot refusal" (`Tests/Editor/Build/AlphaSeparationPreparationTests.cs:4116-4258`, pins at
`:4218-4222` and `:4243-4258`). V6 deliberately reverses this pin.

## 4. Slot model

### Slot to submesh mapping

Strict 1:1, enforced before closure results are consumed:
`MaterialSlotMappingRefusalFor` refuses `UnprovenMaterialSlotMapping` when
`materialSlotCount != mesh.subMeshCount` (`Editor/Host/UnityRendererAlphaAnalysis.cs:478-484`),
applied in `HostStructuralRefusalFor` (`:277-289`, before capture) and in `CaptureGeometry`
(`:326-328`). Snapshot submeshes are constructed `(submesh, submesh, indices)`, so source
submesh index and material slot index are the same number (`:363-388`), and
`ClassifyRuntimeStates` looks up `slotResults[submesh.MaterialSlotIndex]`
(`Editor/Build/AmusePlatformFinishPlugin.cs:663-685`). A count mismatch is a renderer
refusal, never a per-slot condition. Slot-count-versus-animation mismatches are caught
inside the closure as `SlotOutOfRange` (`Editor/Host/UnityAnimationEvidenceCapture.cs:315-317`).

### What a swap animation targets

A `(binding.path, binding.propertyName)` pair: `path` must ordinal-equal the renderer's
animation path (`:536-551`), and `propertyName` must match
`"m_Materials.Array.data[" + k + "]"`, parsed by `TryParseMaterialSlotBinding`
(`Editor/Host/LiveAnimationObservation.cs:83`, `:132-148`). The target is a renderer-path
plus slot-index pair. Per-slot swap value sets are extracted today only via
`MaterialSlotsFor` (`Editor/Build/AmusePlatformFinishPlugin.cs:777-822`), which iterates the
captured evidence's object bindings stored as admitted-material indices
(`Editor/Host/CapturedAnimationEvidence.cs:60-82`, written at
`Editor/Host/UnityAnimationEvidenceCapture.cs:401-421`).

## 5. Per-slot semantics and all-or-nothing consumers

### What slot refusal means geometrically

With the 1:1 mapping, slot k is exactly submesh k
(`Editor/Analysis/MeshSeparationPlanner.cs:107-140`). Refusing slot k leaves submesh k on
its original material and original swap curve. The planner is material-blind
(`:195-246`), and the machinery for "this submesh proves nothing" already exists:
`UnprovenOutcomes` fills every triangle with `TriangleAlphaOutcome.Unknown` for an
unresolved slot (`Editor/Build/AmusePlatformFinishPlugin.cs:746-775`), which yields zero
opaque ordinals and therefore `SubmeshSeparationDisposition.Unchanged`
(`Editor/Analysis/MeshSeparationPlanner.cs:222-226`). The planner can already split other
slots of the same renderer around an unresolved slot.

The per-slot refusal structure below the closure already exists too: the slot loop in
`ResolveRuntimeStates` resolves every slot and retains a refused slot's result
(`:582-625`, "no slot's failure stops the loop"), and refuses the renderer only when no
slot resolved (`:627-635`).

### Every consumer that assumes all-or-nothing

1. The renderer gate: `!evidence.IsClosed` means renderer refusal
   (`Editor/Build/AmusePlatformFinishPlugin.cs:501-505`). The all-or-nothing point.
2. The `currentMaterials` array: built from `CurrentMaterialIndices`
   (`:637-645`) and passed to `CaptureGeometry` (`:395-396`), which requires length equal
   to slot count (`Editor/Host/UnityRendererAlphaAnalysis.cs:326-328`) and tolerates null
   entries (`:396-406`). A per-slot null-current design must keep the array length intact
   through a sentinel.
3. The `AlphaRelevanceRequest`: one union `MaterialEvidenceRequest.Combine` over ALL
   admitted materials (`Editor/Host/UnityAnimationEvidenceCapture.cs:333-358`). Consumed
   renderer-wide for binding recognition (floats `:516-534`, objects `:537-553`; both can
   produce the renderer-scoped `UnrecognizedAnimatedMaterialBinding`) and passed into every
   `ResolveSlot` (`Editor/Analysis/AdmittedMaterialStates.cs:190-196` documents it must be
   the same request the bindings were judged against). Design constraint: the union must
   keep including materials of closure-refused slots, or binding recognition silently
   changes scope.
4. `GatherAlphaFields(evidence.AdmittedMaterials)` (`Editor/Build/AmusePlatformFinishPlugin.cs:577`):
   union mip-chain fields over all admitted materials. Each slot's resolution reads its own
   materials' sources (`Editor/Analysis/AdmittedMaterialStates.cs:216-258`), so extra
   fields stay inert. Keep the union. `[INFERENCE]` on inertness: `AlphaSemanticsResolver`
   lookup internals were not read.
5. Conversion relevance in preparation: iterates `evidence.AdmittedMaterials`
   (`Editor/Build/AlphaSeparationPreparation.cs:108-118`);
   `ConversionBindingUnrecognized` refuses every candidate slot via
   `RefuseEveryCandidateSlot` (`:152-158`, `:412-426`), a documented renderer-scoped
   slot-vocabulary member (`Editor/Build/AlphaSeparationRecords.cs:68-75`) that V6 keeps
   renderer-scoped.
6. Retained state: `PreparedRendererSeparation.CandidateSlots` is already a per-slot list
   (`Editor/Build/AlphaSeparationRecords.cs:160-206`); a slot's mapping registers only
   after its full admitted set maps (`Editor/Build/AlphaSeparationPreparation.cs:66-85`,
   `:369-375`), and slot-local failure drops the slot with its pending clones
   (`:350-366`). Only the closure-failed slot must now reach this structure as "not a
   candidate" instead of the renderer being absent.
7. Apply-side captured bindings: renderer-wide but keyed by `(path, type, propertyName)`
   triples, and the property name encodes the slot index, so attribution is inherent
   (`Editor/Build/AlphaSeparationApply.cs:135-143`, `:499-506`).
8. Records and counters: closure failure is counted in a renderer bucket today
   (`:415-424`). Per-slot refusal routes a slot-vocabulary reason through
   `RecordSlotRefusal` (`Editor/Build/AmusePlatformFinishPlugin.cs:52-69`);
   `SlotResolutionResult.Refusal` already stores a `RendererAnalysisRefusal` per slot
   (`Editor/Analysis/AdmittedMaterialStates.cs:26-70`), so
   `MaterialDependencyClosureFailed` can be carried per slot without new enum plumbing.
9. Live pairing: `admittedLiveMaterials` is index-aligned with `AdmittedMaterials` only at
   the single success return (`Editor/Host/UnityAnimationEvidenceCapture.cs:443-453`,
   contract `:106-123`). Any partial-admission shape must preserve or redefine this
   pairing.

## 6. Edge cases

- Swap set differs from current set on one slot: the normal case. Slot set = current plus
  swap values (`Editor/Build/AmusePlatformFinishPlugin.cs:781-802`; test
  `OwningRendererStillCapturesItsOwnCurrentAndSwappedMaterials`,
  `Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs:1449`). An unattested swap value
  on slot k leaves submesh k on the current material with its curve untouched; apply edits
  curves only for surviving slots (`Editor/Build/AlphaSeparationApply.cs:228-264`).
- Cross-renderer bindings: the closure scan walks all clips of all committed layers
  avatar-wide, then path-filters per binding
  (`Editor/Host/UnityAnimationEvidenceCapture.cs:205-215`, `:307-310`, `:405-415`,
  `:522-535`). Renderer B's swaps stay invisible to renderer A (test
  `ForeignRendererUnattestedMaterialCannotRefuseThisRenderer`,
  `Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs:1387-1434`). `[UNKNOWN]` the
  capture retains no layer or clip-of-origin provenance beyond names
  (`Editor/Host/CapturedAnimationEvidence.cs:104-118`).
- Null materials: null current means `MissingCurrentMaterial`, slot-indexed
  (`Editor/Host/UnityAnimationEvidenceCapture.cs:264-266`, `:289-296`). Null swap keyframe
  means `InvalidSwapValue`, indexed by the binding's parsed slot (`:319-326`). Both are
  slot-attributable.
- One material shared by two slots: `TryAdmit` dedupes by reference (`:262-280`), so both
  slots seed the same index (`Editor/Build/AmusePlatformFinishPlugin.cs:781-790`). If it is
  unattested, both slots refuse. Conversion artifacts are avatar-wide deduplicated
  (`Editor/Build/AlphaSeparationRecords.cs:120-150`;
  `Editor/Build/AlphaSeparationPreparation.cs:317-327`), so one slot surviving while its
  twin refuses cannot leak prepared state.
- `SlotOutOfRange` ownership: a binding naming slot k at or above the slot count has no
  owning slot. It is a fact about the renderer's slot topology and stays renderer-scoped,
  like `UnprovenMaterialSlotMapping`. `[UNKNOWN]` the firing sites of
  `RendererAnalysisRefusal.AnimatedMeshReplacement`/`AnimatedMaterialSlotCount`
  (`Editor/Host/UnityRendererAlphaAnalysis.cs:28-29`) were not traced.

## 7. Revalidation

Today before mutation (`Editor/Build/AlphaSeparationApply.cs:77-180`, `:473-533`):
renderer-scoped checks (renderer alive, `sharedMesh` reference-equals the expected mesh,
slot-count equality `:85-99`) record `RendererChangedSincePreparation` for every candidate
slot on failure (`:100-119`); the live `sharedMaterials` array is read exactly once
(`:121-124`). Per-slot checks (`ValidateCandidateSlot`, `:473-533`): live marker clip on
this slot's binding (`:493-497`), live target binding absent from evidence
(`:499-506`), curve keyframes and current material mapped in the slot's admitted set
(`:508-531`).

Per-slot closure refusal adds three things:

1. A per-slot closure-failure reason in the slot accounting, so buckets distinguish it
   from renderer refusals (`Editor/Build/AmusePlatformFinishPlugin.cs:41-69`).
2. No structural change on the apply side: the structural checks are slot-count based and
   unchanged; the finalized materials array (`:200-226`) is built only from survivors plus
   untouched live entries, which is already correct per-slot behavior.
3. The evidence carried into apply (`prepared.Evidence`,
   `Editor/Build/AlphaSeparationRecords.cs:175-190`) must stay complete enough for the
   apply-side binding set (`Editor/Build/AlphaSeparationApply.cs:135-143`): per-slot
   closure must retain clip evidence for surviving slots even when a sibling slot failed.
   Today `Failed()` empties clips (`Editor/Host/UnityAnimationEvidenceCapture.cs:242-260`).

## 8. Test seams

Existing production delegates: `AlphaMaterialRequestSelector`
(`Editor/Host/UnityAnimationEvidenceCapture.cs:51-56`), `ClosedAlphaMaterialCapturer`
(`:71-79`), injected through `CaptureObservedForTests` (`:145-160`),
`CaptureGraphForTests` (`:164-176`), the full-loop `Execute` overload
(`Editor/Build/AmusePlatformFinishPlugin.cs:302-345`), and the `RunBarrier` harness with
five optional delegates (`Tests/Editor/Build/AlphaSeparationPreparationTests.cs:4737-4767`).
`VerifiedPoiyomiConversion`/`VerifiedLilToonConversion`
(`Editor/Build/AlphaSeparationPreparation.cs:18-44`) with fixture implementations in
`Tests/Editor/Build/VerifiedLilToonTestSeams.cs:34-274`. `AnalyzeRuntimeStatesForTests`
(`Editor/Build/AmusePlatformFinishPlugin.cs:437-456`). Unattestation is faked three ways in
`Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`: an unregistered family
(`:1629-1662`), a stand-in shader with a real vendor name that cannot pass source
attestation (`:79-101`, exercised `:1216-1234`), and a capturer returning false with call
counting (`:1208-1244`, which pins `captureCalls == 0` when selection fails).

New seams per-slot refusal needs:

1. Slot-selective selection failure: one material unselectable only for slot k while slot
   0's materials stay selectable. Today isolation exists only across renderer paths.
2. Slot-selective capturer failure with per-call recording (the `captureCalls` pattern
   generalized).
3. A two-slot fixture where only slot 1's swap set is unattested and a shared float
   binding stays recognized.

## 9. Risks and falsifiers

### Risk 1: evidence-shape regression in the immutable capture contract

`Failed()` empties everything today (`:242-260`) and tests pin empty evidence on every
failure mode (`Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs:525-529`, `:659-663`,
`:681-684`, `:1052-1056`, `:1127-1131`, `:1253-1257`); the immutable-copy loop assumes every
this-renderer swap value is in `materialIndices` (`:416-421`). A partial-evidence shape must
decide sentinel-versus-drop for refused slots' binding values and must keep the
alpha-relevance union defined over all selected materials, or binding recognition silently
changes (flipping recognized bindings into renderer-wide `UnrecognizedAnimatedMaterialBinding`
at `Editor/Build/AmusePlatformFinishPlugin.cs:516-553`, a scope change V6 forbids).

Falsifier: renderer with two slots; slot 1's swap material unattested; slot 0's materials
selected such that a shared float binding is recognized. Assert slot 0 still splits, the
binding stays renderer-wide (not unrecognized), and `RendererRefusalCount(*)` is zero.

### Risk 2: the batch capturer cannot express per-material refusal

`ClosedAlphaMaterialCapturer` returns one bool for the whole batch
(`Editor/Host/UnityAnimationEvidenceCapture.cs:55-79`). The wrong implementation keeps one
batch call and maps its single false to all slots, silently reintroducing renderer scope.
The existing `captureCalls == 0` pin (`:1208-1244`) forces selection to precede capture per
slot. Either invoke the capturer per surviving slot subset (deduplicating shared materials)
or widen the contract to per-material outcomes; both change the documented sole-capture
invariant (`:38-53`).

Falsifier: slot 1 has an unattested swap. Assert slot 0's split is not zeroed: the
renderer's prepared separation contains slot 0, `SlotRefusalCount(newSlotReason) == 1`,
`RendererRefusalCount(MaterialDependencyClosureFailed) == 0`, `AnalyzedRendererCount == 1`.

### Risk 3: slot-count and sentinel integrity

`MissingCurrentMaterial` leaves a hole in `CurrentMaterialIndices`, which feeds the
`currentMaterials` array (`Editor/Build/AmusePlatformFinishPlugin.cs:637-645`) whose length
must keep equaling the submesh count (`Editor/Host/UnityRendererAlphaAnalysis.cs:326-328`)
and which seeds `MaterialSlotsFor` (`:781-790`). Wrong implementations: skipping the slot
(shifts every later slot index), or mapping `SlotOutOfRange` onto an arbitrary existing
slot. `SlotOutOfRange` stays renderer-scoped; null current carries a sentinel index
resolved to an unresolved slot (`Editor/Build/AmusePlatformFinishPlugin.cs:746-775`).

Falsifier A: renderer with 2 slots plus a clip binding `m_Materials.Array.data[5]` means a
renderer-scoped closure refusal exactly once, no prepared state, no slot bucket.

Falsifier B: slot 1's current material null, slot 0 attested. Slot 0 splits, the materials
array length stays 2, slot 1's submesh stays untouched.

## 10. Explicit unknowns

- Firing sites of `RendererAnalysisRefusal.AnimatedMeshReplacement`/`AnimatedMaterialSlotCount`.
- Whether `CommittedLayer.Clips` dedupes a clip reachable from multiple layers
  (double-observation is harmless under reference dedup, but unverified).
- `AlphaSemanticsResolver.Resolve` field-lookup internals; the inertness claim for extra
  `GatherAlphaFields` entries is inferred from `ResolveSlot`'s per-material reading.
- Exact ordering guarantees of `AnimationUtility` binding enumeration inside `ObserveClip`;
  affects deterministic first-failure reporting only.

## 11. Privacy statement

No Census Lab data and no private avatar data was used. No private name, path, GUID, or
identifier appears. All citations are tracked files in this repository.
