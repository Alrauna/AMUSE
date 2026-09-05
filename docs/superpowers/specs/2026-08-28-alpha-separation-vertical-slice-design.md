# Alpha Separation Vertical Slice — Design

| | |
|---|---|
| Branch | `feat/alpha-separation-vertical-slice` |
| Created from | `main`, verified equal to `origin/main` (0 ahead, 0 behind) |
| Base SHA | `d4202d81d2221f0b5e454453c748c3090e1818de` |
| Required containment | `85abf26e88a83f047eed5c17e4ad5c8901665f1a` confirmed contained in `origin/main` |
| Working tree at branch creation | clean |
| Unity / NDMF | 2022.3.22f1 / NDMF 1.14.4 (pinned, embedded) |
| Census Lab | not used, not inspected, not modified |

Each claim carries a tag: **[SOURCE]** (read from checked-out repository or pinned vendor source),
**[MEASURED]** (established by a characterization already in this repository),
**[INFERENCE]**, or **[DECISION]** (a choice this design makes and the controller may
overturn).

---

## 1. What this feature is

One nondestructive build-time transformation, on the NDMF build avatar only:

For each renderer material slot, examine every admitted runtime material state that affects
the slot. If the triangles are proven opaque across all of those states, move those triangles
onto an AMUSE-generated canonical opaque Poiyomi material. Do this by replacing the slot
material outright (`WhollyOpaqueCandidate`), or by appending an opaque submesh and material

slot beside the preserved alpha slot (`Split`). Then rewrite the affected material-swap
animation so every admitted runtime value maps to its corresponding opaque result.

Both dispositions ship together. Admitted runtime material swaps and the relevant animation
states are supported from the outset.

Source meshes, materials, textures, importer settings, animation clips, controllers, prefabs
and scenes are never written.

---

## 2. Repository reality that materially changed this design

The three prerequisites the animation-rewrite investigation named as blocking are now
resolved. Reading current code rather than the notes changes several things.

### 2.1 Both merged prerequisites are in `main`

**[SOURCE]** `main` contains both: `fix/scope-material-swap-closure-to-renderer` (commit
`9c1510f`) and `refactor: separate capture schema from alpha relevance` (commit `47cabae`).

- `UnityAnimationEvidenceCapture.CaptureObserved` now takes `rendererPath` and filters
  material-slot object bindings through `AddressesAnalyzedRenderer` **before** admission and
  again when building the immutable copy. The graph-wide closure defect and the
  cross-renderer refusal it caused are gone. Admitted sets are genuinely renderer-local.

- `AlphaMaterialRequestSelector` now yields **two** requests per material:
  `alphaRelevanceRequest` and `captureRequest`. `CapturedAnimationEvidence` retains only
  `AlphaRelevanceRequest`. `UnityMaterialSemantics.CaptureRequestForFamily` returns
  `Combine(PoiyomiMaterialSemantics.AlphaEvidenceRequest,
  PoiyomiOpaqueConversion.ConversionEvidenceRequest)` for Poiyomi and the alpha request alone
  for lilToon.

**Consequence: seam E is already closed.** The capture half already captures conversion-relevant
evidence for every admitted Poiyomi material today, in the single closed capture, without widening
ordinary alpha relevance. This design does **not** need to touch the capture schema, `MaterialEvidenceRequest`,
or request selection.

**Superseded:** consumer obligation 1 of the merged conversion design — "build the renderer's
closed request as `Combine(<alpha requests>, ConversionEvidenceRequest)` **for that renderer
only**" — describes a route the repository did not take. The realized shape combines per
*family* rather than per renderer, and achieves the same coverage guarantee by keeping the

relevance request narrow instead of keeping the capture request narrow. Obligation 1 is
satisfied. Its wording is stale.

### 2.2 The mesh-finalization blocker is resolved and its route is characterized

**[MEASURED]** `MeshCloneFinalizationCharacterizationTests` establishes route 1: an unassigned
native `Object.Instantiate` clone taken in the barrier, finalized layout-only against the
surviving set, assigned once at apply. It also establishes the two bounds obligations and the
base-vertex normalization. This design adopts route 1 exactly and adds nothing to it.

**Withdrawn upstream, and this design relies on the withdrawal:** non-readable mesh
reconstruction is not an acceptance criterion (VRChat requires Read/Write for upload).

### 2.3 Claims in the older notes that are stale or contradicted

| Claim | Where | Status |
|---|---|---|
| "A slot with more than one admitted material cannot be safely separated" | vertical-slice note §6, §12.12 | **Superseded.** True only without curve rewriting. Rewriting the appended slot's curve so each admitted source maps to its opaque counterpart removes the divergence premise. This feature depends on that. |
| "Exactly one admitted material per affected slot" as a correctness requirement | vertical-slice note §3 | **Superseded** for the same reason. |
| Geometry-only separation applying the same alpha material to both slots | vertical-slice note §4 | Already marked superseded there; restated because it is the shape this design must not be read as. |
| Conversion evidence "is not captured at all" (seam E) | animation-rewrite note §2.1, §3, §14.5 | **Resolved by `47cabae`.** |
| Material-swap closure is graph-wide (§2.2) | animation-rewrite note | **Resolved by `9c1510f`.** |
| "How AMUSE retains full-fidelity mesh state" is unresolved (blocker 6) | animation-rewrite note §11, §14 | **Resolved** by the mesh-finalization characterization. |
| Consumer obligation 1's per-renderer combined request | conversion design §7 | **Stale wording**, see §2.1. |
| Conversion design §7.1's "required condition" (no closed admission flow exposes conversion inputs) | conversion design | **Still true and this design supplies it** (§7). The derived evidence is still discarded by `ResolveSlot`, and `CapturedAlphaMaterial` still carries no originating `Material`. |

### 2.4 Seams still open, confirmed against current source

- **A — live/admitted pairing.** **[SOURCE]** `CaptureObserved` still builds `List<Material>
  admitted` index-aligned with `capturedMaterials` and lets the live half fall out of scope.

- **B — derived per-(slot, material) evidence.** **[SOURCE]** `AdmittedMaterialStates.ResolveSlot`
  still constructs the derived `CapturedAlphaMaterial` and returns only `AlphaResolution`.

- **C — `UnityRendererMutationTarget`.** **[SOURCE]** Still produced by `CaptureGeometry` and
  never read.

- **D — `MeshSeparationPlan`.** **[SOURCE]** `ClassifyRuntimeStates` still ends
  `return plan.OpaqueTriangleCount;`.

- **F — no third pass.** **[SOURCE]** `AmusePlatformFinishPlugin.Configure` declares exactly
  two passes.

- **G — `CapturedObjectBinding.TypeName` is a string.** Unchanged, and still resolved the same
  way: the second window reads real bindings with real `Type`s from live clips, and membership
  comparison uses `(Path, TypeName, PropertyName)`.

### 2.5 The pre-existing slot→renderer alpha escalation — an independently mergeable prerequisite

**[SOURCE]** `AmusePlatformFinishPass.ResolveRuntimeStates` returns on the **first** slot whose
`AdmittedMaterialStates.ResolveSlot` refuses, so one unresolvable slot refuses the analysis
for the whole renderer — including sibling slots whose alpha proof does not depend on it.

**Exactly what is in scope, and what is not.** **[SOURCE]** Material-dependency closure runs
*before* any slot exists to resolve, and it is renderer-wide and all-or-nothing:
`UnityAnimationEvidenceCapture.CaptureObserved` selects and captures **one** closed renderer-wide
admitted batch, an unselectable or unattested material returns
`MaterialDependencyClosureFailure.UnattestedMaterial`, failed evidence carries no admitted
materials at all, and `ResolveRuntimeStates` returns `MaterialDependencyClosureFailed` **before**

the build reaches `MaterialSlotsFor` or `AdmittedMaterialStates.ResolveSlot`. An unattested or
unsupported material therefore resists localization to its slot through changes to `ResolveRuntimeStates`,
and this design does not claim otherwise.

The escalation that *is* in scope is the one occurring **after closure has already succeeded**:
the per-slot `AdmittedMaterialStates.ResolveSlot` loop returns on the first refusing slot, so one
refusing slot eliminates a *valid* candidate slot beside it.

**Which post-closure failures are genuinely per-slot, and which are not.** The distinction matters,
because a `material.<Property>` float curve at the renderer path is **renderer-wide**: **[SOURCE]**
`ResolveProofRelevant` returns `RendererWide`, and `ResolveRuntimeStates` hands the same relevant
binding list to **every** slot. So the *binding* is never slot-scoped, and a curve that no material
can admit is not isolated to one slot — it refuses every slot it reaches, and localizing the loop

changes nothing about it.

What *is* per-slot is the **admission outcome**. **[SOURCE]** `ResolveSlot` admits every group
against *the captured defaults of that admitted material itself* — its doc comment states the
rule directly: "the same animated binding may therefore be admitted against one admitted material and
refused against another." The failures the prerequisite genuinely localizes are therefore
material-dependent failures that arise from one shared renderer-wide binding:

- the animated singleton **equals** the serialized default of the material in slot 1 but
  **differs** from the default in slot 0, so `AdmitScalar` returns `SourcesDisagree` for slot 0
  only → `AnimatedMaterialPropertyNotSingleton`.

- the animated property is **present** on the material in slot 1 and **absent** from the
  material in slot 0 → `AnimatedPropertyAbsentFromAdmittedMaterial` for slot 0 only.

- the admitted material in slot 0 resolves to unknown alpha semantics while the material in
  slot 1 does not → `AdmittedMaterialSemanticsUnknown` for slot 0 only.

Each of these is a genuinely slot-scoped failure. Today the loop gives it renderer scope, and the
feature cannot avoid that from inside. A curve form that is not finite-exact
(`UnsupportedAnimationCurveForm`) is **not** in this group: **[SOURCE]** `AdmitScalar` checks
`IsFiniteExact` on the binding itself, before the code consults the default of any material, so it

refuses every slot the binding reaches regardless of the scope of the loop. This section lists it
so no one mistakes it for something the prerequisite fixes.

**[DECISION]** Recorded as an independently mergeable **prerequisite**,
`fix/scope-slot-alpha-refusal-to-slot`, completed and merged **before** vertical-slice
implementation starts. The independent-prerequisite rule in `AGENTS.md` §Working discipline
applies: park this branch → start the prerequisite from fresh `main` →
complete, review and merge it → resume the vertical slice from updated `main`.

What this design requires of that branch, and nothing more: the per-slot `ResolveSlot` loop must
yield per-slot resolutions with per-slot refusals. It must not return on the first refusing
slot. Then a slot whose alpha cannot be resolved is not a separation candidate, while its

siblings still are. This changes *which slots a post-closure failure eliminates*. It never
changes what is proven, and it never changes how a slot is proven.

**Explicitly outside that branch.** It must not redesign `CapturedAnimationEvidence`, the closed
capturer, or the single closed batch. It must not localize closure. Renderer- and
avatar-scoped failures keep their current scope unchanged: material-dependency closure failure
(including unattested and unsupported materials), unrecognized animated material bindings,

additive layers, unnormalized direct blend trees, animated mesh or slot count, and every host
structural refusal.

**Kept out of the vertical slice.** This design does not implement the prerequisite, does not
depend on its internal shape, and does not fold it into the feature branch. Its own falsifiers
belong to it. The merged prerequisite regression
`RuntimeStateProductionEntry_PostClosureSlotRefusalKeepsTheValidSibling` (`914d9db`) discharges

the falsifier-19 obligation for the vertical slice. It is re-run on this branch, and falsifier 1
proves at the feature layer that a wholly opaque candidate which reaches the feature is
applied. No standalone feature-level F19 test exists. §13 records why.

### 2.6 Renderer-wide dependency closure — existing coverage pressure, not a prerequisite

Renderer-wide closure failure is a **conservative false negative that this milestone keeps**.
One unsupported material — a locked Poiyomi material, a transparent or cutout lilToon variant,
a third-family shader — refuses every slot on its renderer. That includes slots whose own
materials are attested and would still separate.

This section records it as **existing coverage pressure**, deliberately **not** a second
prerequisite and **not** a follow-up this design specifies. Changing it would mean changing the
single closed capture representation: per-material or per-slot capture, partial batches, or a
partial-attestation outcome. That is a materially larger architectural decision than any

coverage argument currently available justifies. Revisit it only when real coverage evidence,
from the approved Census corpus or reduced public fixtures, shows what it actually costs.
Increasing coverage is never a reason to weaken the closed-capture guarantee.

---

## 3. Lifecycle

**Prerequisite ordering.** One independently mergeable prerequisite precedes implementation,
for everything in this document:

1. **`fix/scope-slot-alpha-refusal-to-slot`** (§2.5) — from fresh `main`. Completed, reviewed
   and merged on its own.
2. **This vertical slice** — resumed or recreated from updated `main`.

Nothing else remains open. The two prerequisites the earlier notes named — renderer-scoped
material-swap closure and the capture-schema/alpha-relevance split — are already merged (§2.1),
and §2.2 resolves the mesh-finalization question.

Three passes in one `InPhase(BuildPhase.PlatformFinish)` sequence.

```
pass 1  "AMUSE animator bindings capture"      WithRequiredExtension(AnimatorServicesContext)
        unchanged: retains IPlatformAnimatorBindings

pass 2  "AMUSE semantic barrier"               no extension  → committed graph
        unchanged analysis, plus:
          retain live/admitted pairing              (seam A)
          retain MeshSeparationPlan                 (seam D)
          retain UnityRendererMutationTarget        (seam C)
          conversion admission + eligibility        (seam B, per candidate slot)
          prepare opaque material clones            (transient objects)
          if plan.RequiresAnySplit && a Split slot survived preparation:
              one unassigned native mesh clone
        → PreparedAlphaSeparation on AmusePlatformFinishState
        NO build-avatar mutation, NO asset saving, NO source mutation

pass 3  "AMUSE alpha separation apply"         WithRequiredExtension(AnimatorServicesContext)
        AmuseBuildOperation.Execute(state.Lifecycle, context.AssetSaver, prepare, apply)
          prepare:  1. validate every candidate slot, reads only → surviving set S
                    2. finalize against S (transient objects only)
                    3. sweep transient objects no slot in S references
                    → Ready / NoMutation
          apply:    the single build-avatar mutation boundary
        → BuildContext.Serialize() persists the assigned generated objects
```

### 3.1 Every seam in that outline, verified rather than assumed

| Seam | Verification |
|---|---|
| A third pass can reactivate `AnimatorServicesContext` after an extension-free barrier | **[MEASURED]** `AnimatorServicesReactivationCharacterizationTests`, RED-proven: removing `WithRequiredExtension` fails with `Extension … not active`. |
| The reactivated index observes the committed graph the barrier analyzed | **[MEASURED]** same test: exact object-reference keyframes and times observed in the second window. |
| Editing through `VirtualClip` commits, with times preserved bit-identically | **[MEASURED]** same test, `"R"` round-trip formatting. |
| Clip association is by binding identity, never by name | **[MEASURED]** same test: two clips share a display name; exactly one is edited. |
| Extension instances survive deactivation; reactivation is re-entry | **[SOURCE]** `BuildContext.ActivateExtensionContext` / `DeactivateExtensionContext`; `LayerState.Revalidate`. |
| A transient object created in pass 2 is intact in pass 3 | **[SOURCE]** NDMF passes are synchronous calls inside one `ProcessAvatar`; `GetState<T>()` is a plain object; Editor Unity objects live until explicit destroy or domain reload, and no domain reload occurs inside a synchronous build. `AnimatorBindings` is the existing precedent. |
| Correctness must not depend on pass adjacency | **[SOURCE]+[MEASURED]** the solver strongly prefers sequence successors and did schedule adjacently, but `NextPriorityPass` falls back to `ready.Min`; therefore pass 3 revalidates every live binding and value regardless (§9). |
| Generated objects persist with no `SaveAsset` call | **[SOURCE]** `BuildContext.Serialize()` walks `ReferencedAssets` from the avatar root; `VisitAssets.ObjectReferences` has an explicit `AnimationClip` arm reaching object-reference keyframe values, and renderer `sharedMesh`/`sharedMaterials` are reachable trivially. |
| Eager saving is harmful | **[SOURCE]** `Serialize()`'s cleanup skips anything that is not a `Component` or `GameObject`, so a saved-then-abandoned asset is welded permanently into the shipped container. Saving would also make preparation observably mutating. |
| `AmuseBuildOperation` cannot straddle passes | **[SOURCE]** `Execute` calls `prepare` then `apply` synchronously. It is therefore used **inside pass 3 only**, which is exactly where a validate/mutate boundary belongs. |

### 3.2 Why the barrier stays extension-free

**[SOURCE]** `AmusePlatformFinishPass.RequireAnimatorServicesContextInactive` exists because a
barrier under an active extension reads *pre-commit* controller state. Then
`CommittedControllerGraph.Enumerate` would read the un-virtualized innate controllers of the

avatar. The barrier placement is load-bearing and unchanged. Pass 3 does not analyze.
It validates against already-taken evidence and mutates.

---

## 4. Prepared state — exact shape

One new field on the existing `AmusePlatformFinishState`, following the `AnimatorBindings`
precedent for a live transient host capability held outside the immutable evidence graph.

```
AmusePlatformFinishState
    PreparedAlphaSeparation Separation { get; set; }        // null when nothing was prepared

PreparedAlphaSeparation                                      // avatar-scoped
    IReadOnlyList<PreparedRendererSeparation> Renderers      // deterministic traversal order
    IReadOnlyDictionary<Material, Material>   OpaqueBySource // live, reference-keyed
    IReadOnlyList<Material>                   CreatedClones  // live; AMUSE-instantiated only

PreparedRendererSeparation
    UnityRendererMutationTarget Target        // existing type, held whole: Renderer,
                                              // ExpectedMesh, ExpectedMaterialSlotCount
    string                      RendererPath  // immutable
    MeshSeparationPlan          Plan          // immutable evidence
    CapturedAnimationEvidence   Evidence      // immutable evidence
    Mesh                        MeshClone     // live; null unless a Split slot survived
                                              // preparation
    IReadOnlyList<PreparedSlotSeparation> CandidateSlots    // ascending slot index

PreparedSlotSeparation
    SubmeshSeparationPlan  Plan               // existing type, held whole: SourceSubmeshIndex,
                                              // SourceMaterialBindingIndex, Disposition,
                                              // Opaque/TransparentTriangleOrdinals
    IReadOnlyDictionary<Material, Material> OpaqueOfAdmitted   // live, reference-keyed
```
**This design copies nothing out of an existing type, and it modifies neither existing type.**
`UnityRendererMutationTarget` **[SOURCE]** comes from `CaptureGeometry` today, and nothing
consumes it. Holding it whole is what closes seam C. `SubmeshSeparationPlan` already carries the
slot index, the disposition and both ordinal lists, so:

- slot index — `Plan.SourceMaterialBindingIndex`. **[SOURCE]** `ClassifyRuntimeStates` fills it
  from `UnitySubmeshAlphaSnapshot.MaterialSlotIndex`, and `CaptureGeometry` constructs every
  snapshot as `new UnitySubmeshAlphaSnapshot(submesh, submesh, indices)`, so submesh index,
  material slot index and `SourceSubmeshIndex` are the same number by construction.

- disposition — `Plan.Disposition`.

- triangle ordinals — `Plan.OpaqueTriangleOrdinals` / `Plan.TransparentTriangleOrdinals`.

- expected renderer, mesh and slot count — `Target.Renderer`, `Target.ExpectedMesh`,
  `Target.ExpectedMaterialSlotCount`.

**There is no `CurrentOpaque` field, deliberately.** The current material assignment is *live
mutable state*, so a barrier-time copy of it would be exactly the stale-state hazard §9.1
exists to close. Pass 3 reads the current material from the live renderer, validates it against
`OpaqueOfAdmitted`, and uses the validated live value.

**Immutable evidence:** `Plan` (both levels), `Evidence`, `RendererPath`, and
`Target.ExpectedMaterialSlotCount`.
**Live transient Unity objects:** `Target.Renderer`, `Target.ExpectedMesh`, `MeshClone`, every
`Material` key and value, `OpaqueBySource`, `CreatedClones`.

Nothing here enters `CapturedAnimationEvidence`, `MaterialSemantics`, `MeshSeparationPlan`,
`SubmeshSeparationPlan`, `UnityRendererMutationTarget`, or `UnityRendererAlphaSnapshot`.

**`OpaqueBySource` is avatar-scoped on purpose.** The clone depends only on the source
material (`new Material(source)` plus fixed canonical constants), so two renderers referencing
the same source material share one clone. The *decision* to use it stays per slot. This costs
nothing and avoids duplicate generated assets in the shipped container.

**`CreatedClones` holds only materials AMUSE instantiated.** An `AlreadyOpaque` mapping is
`source → source` and never enters it, so the sweep can never destroy a source asset. That is
a categorical guarantee, not a check.

---

## 5. Barrier pass — data flow per renderer

Everything before "NEW" is the existing loop, unchanged.

```
HostStructuralRefusalFor(renderer)                              existing
rendererPath = AnimationUtility.CalculateTransformPath(...)     existing
evidence = UnityAnimationEvidenceCapture.Capture(               existing signature +
              rendererPath, renderer.sharedMaterials,           NEW out live pairing
              graph, bindings, out liveAdmittedMaterials)
resolved = ResolveRuntimeStates(rendererPath, evidence, …)      existing
extraction = UnityRendererAlphaAnalysis.CaptureGeometry(...)    existing
plan = MeshSeparationPlanner.Create(...)                        existing, now RETAINED
state.OpaqueCandidateTriangleCount += plan.OpaqueTriangleCount  existing

NEW, only if plan.HasAnyOpaqueCandidates:
  conversionBindings = float bindings re-resolved against
      PoiyomiOpaqueConversion.ConversionEvidenceRequest         §7.1
  for each submesh plan with disposition != Unchanged:
      map that slot's admitted materials to opaque results      §7.2–§7.4
      on any slot-local refusal: drop this slot, continue
  if any candidate slot survived:
      if plan.RequiresAnySplit and any surviving slot is Split:
          MeshClone = Object.Instantiate(extraction.MutationTarget.ExpectedMesh)
      append a PreparedRendererSeparation
```

**The mesh clone trigger is narrowed twice.** `plan.RequiresAnySplit` is necessary but not
sufficient. If every `Split` slot is dropped during preparation, no clone is created at all.
**[SOURCE]** `RequiresAnySplit` already exists on `MeshSeparationPlan`. No new predicate is

introduced. The sweep handles clones abandoned by *later* (validation-time) failures, not
avoidance of creation.

**Determinism.** Renderer order is `GetComponentsInChildren<Renderer>(true)` order, already the
order of the existing loop. Candidate slots ascend by slot index. The loop visits admitted materials
in `CapturedMaterialSlotEvidence.AdmittedMaterialIndices` order, which
`AmusePlatformFinishPass.MaterialSlotsFor` builds deterministically (current assignment first,

then clip/binding/value order). No dictionary iteration order is ever observable in an output.

---

## 6. Seam A — the live/admitted pairing

**[SOURCE]** The pairing exists for one statement inside `CaptureObserved` and is then
unrecoverable, yet `PrepareCanonicalOpaqueClone`, `ReadEffectiveRenderState`,
`GatherConversionSourceEvidence` (which needs `material.shader`) and slot assignment all need
the live object.

**Minimal change:** thread an `out IReadOnlyList<Material> admittedLiveMaterials` through
`CaptureObserved` → `CaptureGraph` → `Capture` / `CaptureGraphForTests` /
`CaptureObservedForTests`, index-aligned with `CapturedAnimationEvidence.AdmittedMaterials`.

- No new type, no second capture, no re-admission.

- The list is a **local** in the barrier and reaches build state only through the
  live half of the prepared record. It never enters `CapturedAnimationEvidence`, whose no-live-Unity-object
  guarantee still holds.

- On a closure failure the out list is empty. Callers must not read it.

**Rejected:** a `RendererAnimationCapture` wrapper type (a new type to carry two values across
one call), and re-deriving the pairing later from `renderer.sharedMaterials` plus curve values
(a second, weaker admission, and stale by construction).

---

## 7. Poiyomi conversion boundary

`PoiyomiOpaqueConversion` is used exactly as it stands. Its recipe, eligibility order,
This design does not redesign, wrap, or re-implement the recipe, eligibility order,
`AlreadyOpaque` classification, clone preparation or read-back validation. No field is added to `MaterialSemantics`.

### 7.1 Conversion-relevant animated properties are admitted through conversion's own request

**[SOURCE]** `UnityAnimationEvidenceCapture.ResolveProofRelevant` takes the relevance request as
a parameter. Called with `evidence.AlphaRelevanceRequest`, a `material._ZWrite` curve at the
renderer path resolves `Irrelevant`. Called with
`PoiyomiOpaqueConversion.ConversionEvidenceRequest`, the same binding resolves `RendererWide`.

So the second resolution is a **second call to the existing function with a different
relevance**, not a new mechanism, and it happens **only for renderers that produced opaque
candidates**. Ordinary alpha proof still resolves only alpha-relevant bindings. **Per-slot alpha
relevance and semantic resolution are unchanged** — the same bindings are judged relevant, the

same values admitted, the same semantics resolved, the same triangles proven. The prerequisite of
§2.5 changes only the *scope* of a per-slot refusal, never any of that. This realizes shape 2 of
the animation-rewrite note §9, with the capture half already merged.

Three renderer-scoped conditions arise from this second resolution, and they are genuinely
renderer-scoped because the bindings themselves are renderer-wide:

- `ResolveProofRelevant` returns `UnrecognizedMaterialBinding` under conversion relevance.
- `evidence.HasAdditiveLayer` with at least one conversion-relevant binding.
- `evidence.HasUnnormalizedDirectBlendTree` with at least one conversion-relevant binding.

Each invalidates **every candidate slot on that renderer** and nothing else: no other
renderer, and not the alpha analysis of the renderer or its accounting.

### 7.2 Per admitted material state, in one consistent tuple

For each candidate slot, for each admitted material index in the
`AdmittedMaterialIndices` of that slot — and **only** those indices, never the whole `AdmittedMaterials`
list:

1. **Derived conversion evidence.** Group the conversion-relevant bindings of this slot by logical
   property and admit each group against **the captured defaults of this admitted material itself**,
   accumulating derivations. This is exactly what `AdmittedMaterialStates.Admit` does for

   alpha. The change is to let it run a second time against a different relevance set. See
   §12 for the one production method this requires.

2. **Effective non-property facts.** `PoiyomiOpaqueConversion.ReadEffectiveRenderState(live,
   out queue, out renderType)`, read **in the barrier, in the same pass as the evidence**.
   **[SOURCE]** Neither fact is animation-reachable — the material binding syntax of Unity is
   `material.<PropertyName>`, and no binding form addresses a render queue or an override tag
   — so neither belongs in an evidence request, and reading them beside the capture is not a
   late live read of animation-relevant state.

3. **Conversion attestation**, per the merged conversion design §5.2:
   `PoiyomiOpaqueConversion.GatherConversionSourceEvidence(live.shader, derivedEvidence)` then
   `PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(...)`. This is the sole reason the live
   `Shader` is needed. A locked Poiyomi material fails here — a correct expected refusal.

4. **Eligibility.** `PoiyomiOpaqueConversion.EvaluateVerifiedEligibility(derivedEvidence,
   queue, renderType)`.

Every one of these consumes the evidence captured in this same barrier pass. Nothing
re-captures material state after the evidence a decision depends on. **This supplies the
"required condition" the merged conversion design §7.1 recorded and deliberately left open.**

### 7.3 The renderer-wide overwrite rule

**[SOURCE]** A `material.<Property>` curve addresses no slot and therefore drives *every*
material on the renderer, including a generated one AMUSE appends. Conversion design §7
obligation 4 verbatim, adopted as written:

> for every recipe property carrying an admitted conversion binding at this renderer path, the
> admitted value must already equal that property's canonical value.

Because admission is exact-singleton against the serialized default of the material itself, the
admitted value *is* the serialized value, so this reduces to a comparison against
`PoiyomiOpaqueConversion.CanonicalOpaqueProperties` — already exposed, no new surface.

Failing it is a slot-local refusal, not a defect: the recipe AMUSE would write is provably
overwritten at runtime, so the tuple the proof reasons about would be fiction.

`_EnableOutlines` is eligibility-only and never written, so it needs no overwrite check beyond
eligibility. `_AddBlendOp` / `_AddBlendOpAlpha` are neither read nor written and need none.

**Known conservative refusal, deferred unchanged:** an animated `_Cutoff` with admitted
singleton 0.5 would in fact still let α = 1 survive. Relaxing it needs per-property
interchangeability reasoning the approved scope does not require.

### 7.4 Outcomes map into prepared mappings

| Eligibility outcome | Mapping | Clone |
|---|---|---|
| `AlreadyOpaque` | `source → source` (identity, a successful no-op) | none created; never entered in `CreatedClones`; never destroyed |
| `Convertible` | `source → clone` | `PrepareCanonicalOpaqueClone(source)`, deduplicated by source material through `OpaqueBySource`, added to `CreatedClones` |
| `Refused(reason)` | none | none; the **slot** is dropped, carrying the `PoiyomiOpaqueConversionRefusal` for reporting |

**Every admitted material of a candidate slot must map before the barrier prepares that slot at all.**
The barrier drops a slot with any unmapped admitted value, and that slot never reaches
validation. This is what makes "a triangle may move to opaque only when it is proven opaque
across every admitted runtime material state affecting its slot" true of the *material* half
as well as the alpha half.

**Clone naming.** `PrepareCanonicalOpaqueClone` deliberately leaves the clone unnamed, because
NDMF sub-asset names come from the name of the object itself and guarantee no determinism. AMUSE names
it immediately on creation:

```
"<sourceMaterial.name> (AMUSE Opaque <n>)"
```

where `n` is the zero-based index the clone holds in `CreatedClones`, which grows in the
deterministic order of §5. Two source materials sharing a name therefore still produce distinct
sub-asset names.

**Clone re-read/validation.** Already inside `PrepareCanonicalOpaqueClone`: it writes the
complete canonical tuple, then re-reads all 25 canonical facts via `TryFindNonCanonicalFact`
and checks shader preservation, destroying the clone before throwing on disagreement. That
throw is an invariant failure and must stay one — the no-catch policy applies. AMUSE adds no

second validation and no catch.

**No eager save.** `PrepareCanonicalOpaqueClone` takes no asset saver and cannot persist
anything. AMUSE calls no `IAssetSaver.SaveAsset` for materials, meshes, or curve-referenced
objects, for the two reasons in §3.1.

---

## 8. Slot-local refusal

### 8.1 Vocabulary

**[DECISION]** One new enum, `AlphaSeparationSlotRefusal`, owned by the feature. It is
deliberately *not* merged into `RendererAnalysisRefusal` — analysis reads that vocabulary, and
putting transformation conditions there would let unknown transformation state start refusing
analysis that does not depend on it. This is the same argument `PoiyomiOpaqueConversionRefusal`

already makes in its own doc comment, and the same separation.

| Member | Raised in | Cause |
|---|---|---|
| `OpaqueConversionUnsupportedFamily` | barrier | an admitted material of this slot is not Poiyomi (includes every lilToon and every mixed-family slot) |
| `OpaqueConversionRefused` | barrier | `EvaluateVerifiedEligibility` refused for an admitted material |
| `ConversionStateNotAdmitted` | barrier | a conversion-relevant animated property is not an exact singleton, is not finite-exact, or is absent from an admitted material |
| `ConversionPropertyOverwrittenAtRuntime` | barrier | §7.3 |
| `MarkerClipCarriesSlotBinding` | barrier (evidence) **and** pass 3 (live, authoritative) | a special/marker motion carries this slot's material-swap binding |
| `RuntimeMaterialValueNotMapped` | pass 3 | a live runtime material value for this slot is absent from its mapping — whether it arrives from a curve keyframe or from the live current assignment (§9.1 items 4–5). One member, because the condition is identical and the arrival route carries no information |
| `SlotBindingAbsentFromEvidence` | pass 3 | a target binding exists live that evidence did not record |

Renderer-scoped members, applied to *all* candidate slots of one renderer and to nothing else:
`ConversionBindingUnrecognized`, `ConversionStateUnderAdditiveLayer`,
`ConversionStateUnderUnnormalizedDirectBlendTree`, `RendererChangedSincePreparation`.

Counted on `AmusePlatformFinishState` through a `RecordSlotRefusal(reason)` writer mirroring the
existing `RecordRendererRefusal` pattern, so the total and the per-reason buckets cannot drift.

### 8.2 Scope rules

**The exact scope claim, qualified.** Refusal is slot-local *within* a renderer whose material
dependency closure succeeded — it is not slot-local all the way down:

1. **Renderer material-dependency closure remains a precondition for any slot on that renderer to
   be analyzed at all.** **[SOURCE]** Closure is renderer-wide and all-or-nothing, and a failed
   closure returns before any slot is resolved (§2.5).

2. **After successful closure**, ordinary per-slot alpha-resolution failures, conversion failures
   and late-validation failures are slot-local — the first of these conditional on the
   prerequisite of §2.5.

3. **Unsupported or unattested material closure remains a renderer-wide conservative false
   negative in this milestone**, recorded as existing coverage pressure in §2.6 and not addressed
   here.

The rules below state (2).

- A failed slot keeps its **original submesh, its original material assignment, and its original
  material-swap curve**. AMUSE writes nothing for it.

- Independently valid slots on the same renderer continue.

- Other renderers are unaffected.

- **All** candidate slots on **all** renderers are validated before **any** build-avatar write.

- A failure that is genuinely renderer-scoped (§8.1, second group) drops the candidate
  slots of that renderer and nothing more — never the alpha analysis of the renderer, never another renderer.

- Cleanup happens once, after `S` is known.

- **No reference counting.** The single post-validation sweep already expresses the rule.

### 8.3 Marker clips

**[SOURCE]** `VirtualClip.SetObjectCurve` starts with `if (IsMarkerClip) return;` — a silent no-op —
and `VirtualClip.FromMarker` keeps the original SDK asset. Editing one would appear to succeed
while the avatar kept swapping to the alpha material, and reaching the asset directly would write
a source asset.

Checked **twice**: in the barrier against `CapturedClipEvidence.IsSpecialMotion` (cheap, drops the
slot before any clone is prepared for it), and again in pass 3 against the live `VirtualClip`,
which is authoritative. The live check is the one correctness depends on. The early check exists
to avoid preparing work that will be discarded.

### 8.4 The clone-without-survivors case

Required behaviour, and the answer of this design:

> a renderer whose plan required a mesh clone, where every split slot is later invalidated

The clone was created in the barrier because at least one `Split` slot survived *preparation*.
When validation invalidates all of them, `S` for that renderer contains only
`WhollyOpaqueCandidate` slots (possibly none). Then:

- **the mesh clone is referenced by no surviving slot, so the sweep destroys it.**
- `renderer.sharedMesh` is never touched and stays the source mesh.
- surviving wholly-opaque slots still apply — material assignment plus curve rewriting, which
  need no geometry.
- if `S` is empty for that renderer, the feature writes nothing at all for it.

This is exactly the expected direction stated in the task, and it needs no special case: it falls
out of "finalize against `S`, then sweep what `S` does not reference". Test 4 (§13) asserts it at
the preparation seam, holding the clone reference across `prepare`, because a completed build
cannot observe a destroyed transient.

---

## 9. Pass 3 — validation, finalization, sweep, apply

The design calls `AmuseBuildOperation.Execute(state.Lifecycle, context.AssetSaver, prepare, apply)`
**once for the whole avatar**, so there is exactly one mutation boundary. It becomes the
first production consumer of the type and is **not modified**. The `assetSaver` argument is required by its
signature, and the design deliberately never uses it — nothing obliges a consumer to save, and §3.1 explains

why saving would be wrong.

### 9.1 Validate — reads only, nothing constructed, nothing written

Per renderer, before its slots:

- `Target.Renderer` is alive. `renderer.sharedMesh` is reference-equal to
  `Target.ExpectedMesh`. `renderer.sharedMaterials.Length ==
  Target.ExpectedMaterialSlotCount`. `sharedMesh.subMeshCount ==
  Target.ExpectedMaterialSlotCount`. Any mismatch → `RendererChangedSincePreparation` for all

  its candidate slots. This is an ordinary refusal, not a defect: another pass in this phase may
  legitimately replace the mesh or the slot array.

- **Then snapshot the live `renderer.sharedMaterials` array once**, and use that snapshot for
  the rest of validation, finalization and apply. It is the authoritative statement of what the
  renderer currently holds. Nothing downstream re-reads the renderer.

**Why the live snapshot is required.** Length and mesh identity do not constrain *contents*.
**[SOURCE]** a foreign NDMF pass in this phase may replace `sharedMaterials[i]` while preserving
the array length, and nothing in the barrier-time record would notice. Since §3.1 establishes
that correctness must not depend on pass adjacency, a barrier-time current material is stale
state by construction, and applying an opaque result derived from it would silently overwrite

an assignment made by another tool with a material AMUSE proved nothing about. Reading the current
material live and validating it is what makes the adjacency-independence claim true rather than
aspirational.

Per candidate slot, independently:

1. Discover the live target bindings — **never by clip name**:
   `AnimationIndex.GetClipsForObjectPath(rendererPath)`, **materialized to a list first**
   (**[SOURCE]** it returns the live `HashSet` owned by the index), then per clip
   `clip.GetObjectCurveBindings()` filtered to `binding.path == rendererPath` and
   `LiveAnimationObservation.TryParseMaterialSlotBinding(binding.propertyName) == slotIndex`.
   Every declared binding type at that path and slot is a target, which is why a clip binding

   `Renderer` and one binding `SkinnedMeshRenderer` are both handled.

2. Any target clip with `IsMarkerClip` → `MarkerClipCarriesSlotBinding`.

3. Any target binding whose `(path, type.FullName, propertyName)` triple is absent from the
   `CapturedAnimationEvidence` object bindings of this renderer → `SlotBindingAbsentFromEvidence`.
   This resolves seam G without carrying `Type` in evidence: the real `Type` comes from the live
   binding, and evidence membership is a string comparison.

4. Every keyframe value of every target curve must be a key in `OpaqueOfAdmitted`. Otherwise
   `RuntimeMaterialValueNotMapped`. A `null` or non-`Material` value cannot occur here — closure
   already failed the renderer — and an empty or zero-length curve contributes no values and is a
   no-op, not a refusal.

5. **The live current material** — `liveMaterials[slotIndex]` from the snapshot above — must also
   be a key in `OpaqueOfAdmitted`. Otherwise `RuntimeMaterialValueNotMapped`, refusing **only
   this slot**. Finalization uses its mapped opaque result (§9.2). The barrier-time

   assignment is never consulted. This is deliberately the same refusal member as item 4 and not
   a new one: the condition is identical — a runtime material value this slot did not prove and
   cannot map — and the only difference is whether that value arrives from a curve keyframe or
   from the current assignment. Splitting one condition across two members would state a

   distinction that carries no information.
   - If the live current material is a *different but already admitted and mapped* material, that
     is not a refusal: it is one of the states this slot was proven against, so the slot applies
     with the opaque result of **that** material.

6. **Fewer** live values than the captured admitted set is safe and is not a refusal: the captured
   set is a conservative structural enumeration, and a subset is strictly less than what was
   proven. Timing changes are irrelevant — the proof quantifies over the *set* of admitted
   materials and carries no temporal component.

The surviving set `S` is the set of (renderer, slot) pairs that passed. `S` may be empty, in
which case `prepare` returns `NoMutation()` and the feature applies nothing anywhere.

### 9.2 Finalize against `S` — transient objects only

This step, never preparation, produces **all** of these: the appended slot indices, the generated
mesh layout, the shared-material array and the complete curve-edit set.

**Appended indexing.** **[DECISION]** **One appended opaque submesh and one appended material
slot per surviving `Split` slot.** They cannot be shared even when two split slots have the same
source material: `UnityRendererAlphaAnalysis.MaterialSlotMappingRefusalFor` requires material slot
count to equal submesh count, each appended submesh needs its own slot, and each appended slot
carries its own curve derived from its own source slot. Sharing would also merge two independent

proofs.

For renderer `R` with original slot count `n` and surviving split slots `i₁ < i₂ < … < i_k`
(ascending source slot index):

```
appended slot for i_m  =  n + (m − 1)
```

Deterministic, independent of dictionary iteration, and stable under any change that does not
change `S`.

**Materials array.** Built **from the validated live snapshot of §9.1**, never from a
barrier-time copy, so any unrelated same-count change another pass made passes through
untouched. Length `n + k`, where `live[…]` is that snapshot:
- `[i]` for a surviving `WhollyOpaqueCandidate` slot → `OpaqueOfAdmitted[live[i]]`.
- `[i]` for a surviving `Split` slot → `live[i]`, **unchanged** (the alpha half is preserved).

- `[i]` for every other slot → `live[i]`, unchanged — including slots AMUSE never examined and
  slots a foreign pass reassigned.
- `[n + m − 1]` → `OpaqueOfAdmitted[live[i_m]]`.

Because every `OpaqueOfAdmitted` lookup here was already validated in §9.1 item 5, finalization
runs no lookup that can fail.

**Mesh finalization**, on the clone only, exactly per the merged characterization:

```
save mesh.bounds and every source submesh descriptor's bounds
subMeshCount = n + k                                  ← recalculates BOTH bounds levels
for each surviving split slot i_m:
    SetIndices(transparent triples of i_m, Triangles, i_m,       calculateBounds: false)
    SetIndices(opaque      triples of i_m, Triangles, n+m−1,     calculateBounds: false)
for each submesh: re-read its descriptor, replace only `bounds`,
    SetSubMesh(..., MeshUpdateFlags.DontRecalculateBounds)
    - a rewritten split submesh and its appended sibling inherit the SOURCE submesh's bounds
    - every untouched submesh is restored to its own saved bounds
mesh.bounds = saved
name = "<sourceMesh.name> (AMUSE Separated <rendererOrdinal>)"
```

- Submeshes belonging to `WhollyOpaqueCandidate` and `Unchanged` slots keep their index data —
  finalization restores only their bounds, because raising `subMeshCount` recalculated them.

- Index triples come from `MeshSeparationPlan`'s ordinals over
  `UnityRendererAlphaSnapshot.Submeshes[i].Indices`, which **[SOURCE]** are absolute
  (`Mesh.GetIndices` applies base vertex). The snapshot needs no new fields.

- **[MEASURED]** `calculateBounds: false` is not sufficient on its own: raising `subMeshCount`
  recalculates mesh bounds *and* the bounds of every submesh before any index is written, and leaves an
  appended submesh at zero bounds. Both write-backs are obligations, not defensiveness.

- **[MEASURED]** Base-vertex normalization on rewritten submeshes (`baseVertex` 4 → 0,
  `firstVertex` 0 → 4) is an intentional, characterized representation change: effective indices
  are identical and `baseVertex + firstVertex` is preserved.

- No vertex is added, removed, moved or reindexed. Positions, normals, tangents, colors, every UV
  channel, bone weights, bindposes, blendshape names, frame weights and per-frame deltas come from
  `Object.Instantiate` and are never rewritten. **[MEASURED]** lossless for the characterized
  state, submesh descriptors included.

**Curve-edit set.** For each surviving slot, the list of (clip, binding, new keyframe values)
derived in §10. This step writes nothing yet.

### 9.3 Sweep

Destroy, with `Object.DestroyImmediate`:

- every `Material` in `CreatedClones` that the mapping values of no surviving slot reference.
- every `PreparedRendererSeparation.MeshClone` that no surviving `Split` slot references.

**[MEASURED]** destroying an abandoned, never-assigned clone leaves the characterized state of the source mesh
unchanged. **[SOURCE]** an unassigned clone is unreachable from the avatar root, so
`Serialize()` never persists it — and never sweeps it either, which is exactly why AMUSE must.

One sweep runs after the code fixes `S`. It covers preparation-time drops and validation-time failures with
one mechanism and handles a clone shared by slots `i` and `k` when only `i` fails. **No reference
counting.**

`prepare` returns `Ready()` if any surviving slot produces at least one actual write, and
`NoMutation()` otherwise — including the case where every surviving mapping is the `AlreadyOpaque`
identity, which by construction changes nothing.

### 9.4 Apply — the single build-avatar mutation boundary

In deterministic renderer order:

```
1. curve edits          (VirtualClip.SetObjectCurve on the discovered bindings)
2. renderer.sharedMesh  (only when the renderer has a surviving Split slot)
3. renderer.sharedMaterials
```

- Steps 1–3 of §9.1–§9.3 leave the build avatar and every source asset exactly as they were.
  Apply is the first write anything can observe.

- Ordering within apply is fixed for determinism and reproducibility. Nothing renders mid-pass, so
  no intermediate state is observable.

- **The renderer component is never replaced.** **[SOURCE]** Assigning the shared mesh suffices.
  Replacement would break animation path bindings, component references and the object-registry
  entries of NDMF. Bones, root bone, local bounds, quality, blendshape weights, probe/shadow settings,
  enabled state and sorting order are untouched, and blendshape weights survive because blendshape
  order and count are preserved.

- **An unexpected exception during apply is not caught and is build-fatal.** The build avatar may be
  half-mutated. That is precisely why it must not continue. `AmuseBuildOperation` documents no
  rollback and catches nothing. A *validation* failure is not a defect and never reaches here.

- Replaced-object registration, if called at all, is **[SOURCE]** error-report provenance only. It
  rewrites no animation and no component reference, and correctness never rests on it.

### 9.5 Persistence

**[SOURCE]** `BuildContext.Serialize()` runs from `Finish()` after all extensions deactivate and
saves every non-persistent asset reachable from the avatar root, traversing renderer
`sharedMesh`/`sharedMaterials` and `AnimationClip` object-reference curve values. So the assigned
mesh, the assigned materials, and materials referenced *only* by a rewritten curve are all
persisted with **no `SaveAsset` call**, and none is made.

### 9.6 Why source assets stay untouched

- Meshes: `Object.Instantiate` is the only relationship between source and clone. The source is
  read and never written. **[MEASURED]** its characterized state is unchanged after cloning,
  finalizing and destroying.

- Materials: `new Material(source)` inside `PrepareCanonicalOpaqueClone` is the only relationship.

- Clips and controllers: every edit goes through `VirtualClip` inside an active
  `AnimatorServicesContext`, so NDMF owns cloning and committing. Marker clips — the one form whose
  `VirtualClip` still points at the original SDK asset — meet refusal before any edit (§8.3), and
  `SetObjectCurve` on one is a no-op anyway.

- Renderers, prefabs, scenes: only the renderer components of the NDMF build copy are written, and only
  their `sharedMesh` / `sharedMaterials`.

---

## 10. Animation rewrite mapping

Shared preconditions per surviving slot `i` (all already established by §7–§9): the admitted
set of the slot is closed. Every admitted material is mapped. Every affected triangle is `ProvenOpaque` across all
admitted resolutions (the existing `IntersectOutcomes` contract). No target clip is a marker.

**Domain.** `OpaqueOfAdmitted` for slot `i`, whose keys are exactly the live materials behind
`CapturedMaterialSlotEvidence.AdmittedMaterialIndices` for that slot — **not** the whole
`AdmittedMaterials` list, even now that closure is renderer-scoped, because the admitted
list of a renderer still spans all its slots.

**Deduplication** is by source material, so a material shared across slots or clips yields one
clone, never one per animator state.

### 10.1 `WhollyOpaqueCandidate` slot `i`

1. `sharedMaterials[i] := OpaqueOfAdmitted[live[i]]`, where `live` is the validated live
   snapshot of §9.1 — never a barrier-time current material.

2. For every target binding at `(rendererPath, m_Materials.Array.data[i])` in every target clip:
   replace the `value` of each keyframe with `OpaqueOfAdmitted[value]`. **`time` is never touched**.
   Write with `SetObjectCurve(binding, curve)`.

3. This disposition adds no submesh, generates no mesh, adds no draw call, and leaves `sharedMesh` untouched.

4. When every mapping in the slot is the `AlreadyOpaque` identity, steps 1–2 are no-ops, and the
   feature skips them rather than writing them.

### 10.2 `Split` slot `i`, appended slot `j`

1. The curve of slot `i` **and** `sharedMaterials[i]` are left completely unchanged — the alpha half must
   keep behaving exactly as authored.

2. `sharedMaterials[j] := OpaqueOfAdmitted[live[i]]`, from the same validated live snapshot.

3. For every target binding at slot `i`, construct a **new** binding identical except
   `propertyName = "m_Materials.Array.data[j]"` — inheriting the real `Type` of the observed binding —
   and set a curve with **identical keyframe times** and values `OpaqueOfAdmitted[value]`.

4. Appending is mandatory. **[MEASURED, existing]** prepending or inserting silently redirects the
   existing material-swap animation onto the AMUSE-generated slot.

5. **[SOURCE]** `AnimatedMaterialSlotCount` already refuses any renderer whose
   `m_Materials.Array.size` is animated, so no curve can contradict the new slot count.

### 10.3 Forms, and how each behaves

| Form | Behaviour | Basis |
|---|---|---|
| Multiple controllers | handled; `AnimationIndex` lookups span `GetAllControllers()` | [SOURCE] |
| Clips with identical display names | irrelevant by construction; name participates in no lookup | [SOURCE]+[MEASURED] |
| The same clip reached from several graph locations | edited once; `EnumerateClips` dedupes by node identity | [SOURCE] |
| Binding typed `Renderer` vs `SkinnedMeshRenderer` | distinct bindings; **both** rewritten, each inheriting its own `Type` | [SOURCE] |
| One clip carrying several target bindings | handled: discovery is per clip per binding, so the callback handles all of that clip's targets | [SOURCE] |
| Marker / special motion | slot refusal before any edit (§8.3) | [SOURCE] |
| Empty or zero-length object curve | no-op for that clip, not a refusal | [SOURCE] |
| `null` / non-`Material` / destroyed value | already a closure failure upstream; the renderer never reaches here | [SOURCE] |
| Unadmitted or newly appeared value | slot refusal at validation, before any mutation | §9.1 |
| The same source material in several slots | correct by construction: one clone shared, each slot independently proven | [INFERENCE] |
| Renderer-wide `material.<Property>` curves | governed by §7.3; a surviving appended slot holds the opaque counterpart of the same source material, for which that property was already admitted and proven canonical | [SOURCE]+[INFERENCE] |
| Synced layers, override controllers, virtualized motions, animation events | already avatar-scoped refusals upstream | [SOURCE] |

**Never used:** `AnimationIndex.RewriteObjectCurves` — **[SOURCE]** a global rewrite of every object
curve in the avatar, which would remap a material everywhere it appears including slots AMUSE proved
nothing about.

---

## 11. Future lilToon compatibility — the exact boundary

Poiyomi is the only opaque-conversion implementation on this branch, and this design adds no
lilToon conversion property, recipe, clone, or evidence request.

**Current policy, realized:**

- lilToon alpha analysis is untouched. `LilToonMaterialSemantics.AlphaEvidenceRequest` remains both
  its alpha relevance and its capture schema, and nothing widens it.

- A lilToon-only candidate slot **whose materials are attested and capture successfully** is refused
  locally with `OpaqueConversionUnsupportedFamily` and stays exactly as authored.

- A slot whose admitted set mixes Poiyomi and attested lilToon meets the same refusal for the same reason — not
  every admitted runtime value can be mapped, and §7.4 requires all of them to map before anything
  happens.

- Unrelated Poiyomi-only slots on the same renderer and the same avatar still optimize.

- **[SOURCE]** An *unattestable* lilToon variant — transparent or cutout, which is a different shader
  asset — is a different case and is **not** slot-local today: it fails alpha-family selection, so the
  material-dependency closure of the renderer fails and the closure refuses every slot on that renderer. That is
  the existing coverage pressure of §2.6, not a refusal of this feature. Widening it is not the job

  of this feature.

- Capture schema and ordinary alpha relevance remain separate, as merged.

**The single family branch** lives at the orchestration boundary, in the per-slot mapping step of the barrier
(§7.2), and nowhere else:

```
switch (capturedAdmitted.Family)
{
    case CapturedAlphaMaterialFamily.Poiyomi:
        // conversion relevance → derived evidence → attestation → eligibility → mapping
    default:
        return AlphaSeparationSlotRefusal.OpaqueConversionUnsupportedFamily;
}
```

Adding lilToon later requires: a lilToon conversion evidence request added to
`UnityMaterialSemantics.CaptureRequestForFamily`, a lilToon conversion implementation with its own
recipe and attestation, and **one new `case` in that switch**. It requires **no change** to geometry
planning, mesh finalization, appended-slot indexing, animation discovery or rewriting, validation,
the sweep, the apply boundary, or the shape of the prepared record — all of which are expressed in terms

of `Material → Material` mappings and triangle ordinals, with no shader knowledge at all.

**Explicitly not created** — and each would be created only when a second implementation actually
exists: `IOpaqueConversion`, a shader adapter interface, a conversion registry, a generic conversion
result hierarchy, a universal material or render-state IR. **[SOURCE]** The doc comment of `UnityMaterialSemantics`
already states the rule of the repository for this: family selection is an exclusive trial,
and "with a third family it becomes a third branch, and that is when a registry earns its first
honest argument."

**Recorded follow-up, not designed here:** `investigate/liltoon-opaque-conversion`. lilToon encodes
render mode by switching the shader asset, and the lilToon attestation of AMUSE accepts one shader name
and its opaque pass, so transparent and cutout lilToon variants are unattestable today. That is an
investigation, not a recipe, and this branch does not start it.

---

## 12. New production types and methods, with YAGNI answers

| # | New | Kind |
|---|---|---|
| 1 | `PreparedAlphaSeparation` / `PreparedRendererSeparation` / `PreparedSlotSeparation` | three plain records that *hold* `UnityRendererMutationTarget` and `SubmeshSeparationPlan` rather than copying from them (§4) |
| 2 | `AlphaSeparationSlotRefusal` | enum (§8.1) |
| 3 | `AmuseAlphaSeparationPass` — pass 3 entry, plus the barrier-side preparation it is paired with | static orchestration |
| 4 | `AmusePlatformFinishState.Separation` + `RecordSlotRefusal` + two counters | fields/methods on an existing type |
| 5 | `out IReadOnlyList<Material> admittedLiveMaterials` on the capture entry points | signature change (§6) |
| 6 | `AdmittedMaterialStates.TryAdmitDerivedEvidence(captured, bindings, relevance, out derived, out refusal)` | one extracted method |
| 7 | one additional test-only delegate on the existing internal `AmusePlatformFinishPass.Execute` fixture overload, substituting only the shader-family opaque-conversion step | test seam, **approved** |

**#6 in detail.** `ResolveSlot` already does this: "group bindings by logical property, admit each
group against this material's own captured default, accumulate derived evidence" — and then throws
the derived evidence away. The change extracts that loop into an internal method and has `ResolveSlot`
call it, so alpha and conversion share one admission implementation rather than two. It removes real

duplication rather than relocating it: without it, conversion would need a second copy of
`GroupByProperty` + `Admit`, which is exactly the "second admission pipeline" the merged conversion
design forbids.

**#7 in detail — approved.** **[SOURCE]** No vendor shader package is present in this repository, and
none may be added. The public Poiyomi fixture is a schema-complete stand-in that deliberately fails
identity attestation on name, GUID and source hash. The internal
`AmusePlatformFinishPass.Execute` fixture overload already carries three delegates

(`selectRequest`, `capturer`, `resolveSemantics`) for exactly this reason, documented as the
public-fixture seam. Conversion takes a fourth of the same kind, and its scope is fixed:

- it substitutes **only** the shader-family opaque-conversion step of §7.2 — attestation,
  eligibility and clone preparation for one admitted material — returning either a mapped opaque
  `Material` or a conversion refusal.

- it is reachable **only** through the existing internal test overload. Production
  `AmusePlatformFinishPass.Execute(BuildContext)` passes nothing and runs the real
  `PoiyomiOpaqueConversion` path, exactly as it already does for the other three.

- it changes no other behaviour: family selection, admission, relevance resolution, planning,
  validation, finalization, the sweep and the apply boundary are the production ones in every test.

It is a delegate on an existing overload, not an interface, registry, adapter hierarchy, result
framework, or a new test fixture framework, and this design introduces none of those.

### 12.1 The five YAGNI questions, answered

| Question | #1 records | #2 enum | #3 pass | #4 state | #5 out-param | #6 method | #7 seam |
|---|---|---|---|---|---|---|---|
| **Which current consumer requires it?** | this feature | this feature | this feature | this feature | this feature | this feature + `ResolveSlot` | this feature's public tests |
| **Why can existing state / an existing record not express it?** | `AmuseBuildOperation` is synchronous and cannot straddle passes and nothing today carries a prepared result between passes; the records add only what no existing type holds — the source→opaque mapping and the mesh clone — and hold `UnityRendererMutationTarget` and `SubmeshSeparationPlan` whole rather than copying out of them | the conditions are unrepresentable today; `RendererAnalysisRefusal` is read by analysis and `PoiyomiOpaqueConversionRefusal` is one shader's vocabulary | there is no way to reach a `VirtualClip` after the barrier — RED-proven | the counters and the carrier have nowhere else to live; `AnimatorBindings` is the precedent | the pairing is destroyed in a local scope and is unrecoverable | the derived evidence is computed and discarded | vendor source is unpublishable |
| **Does it remove real duplication or just rename it?** | neither — it carries only data that exists nowhere, and duplicates no field of an existing type | removes the need to overload two unrelated vocabularies | n/a | n/a | avoids a second admission | **removes real duplication** | avoids duplicating production logic in tests |
| **Generic because two implementations need it, or because lilToon might?** | feature-specific; no shader knowledge | feature-specific | feature-specific | feature-specific | feature-specific | genuinely two current callers (alpha + conversion) | mirrors three existing seams |
| **Can Poiyomi ship safely without it?** | no | no — refusals would be unnameable | no | no | no | no | not verifiably: without it no public deterministic test reaches the feature through an NDMF build |

### 12.2 Explicitly not built

| Not built | Why |
|---|---|
| `IOpaqueConversion`, shader adapter interface, conversion registry, generic conversion result hierarchy | one implementation; one `switch` arm is the whole extension point (§11) |
| Universal material / render-state IR; any render-state field on `MaterialSemantics` | forbidden by policy and unnecessary — conversion already models what it needs |
| Generic mesh cloning service, custom full-fidelity copier, mesh IR, expanded `UnityRendererAlphaSnapshot` | **[MEASURED]** `Object.Instantiate` plus a layout rewrite is the whole mechanism |
| A "requires clone" predicate | `MeshSeparationPlan.RequiresAnySplit` already exists |
| Clone reference counting or a lifetime registry | the single post-validation sweep expresses the rule exactly |
| Allocation instrumentation, a clone factory, or a clone registry added for testability | the prepared record already exposes `MeshClone` and `CreatedClones`; the seam tests of §13 hold those references across `prepare` and need nothing further |
| A general curve-remapping framework or an animation reachability graph | `AnimationIndex` already provides binding-keyed, identity-based lookup; reachability is not required (§9.1 item 5) |
| A cross-pass transaction or mutation framework | `AmuseBuildOperation` plus a build-state record is sufficient, and the former is used unmodified |
| Changes to `MeshSeparationPlan`, `SubmeshSeparationPlan`, `MeshSeparationPlanner`, `UnityRendererMutationTarget`, `AmuseBuildOperation`, `MaterialSemantics`, `MaterialEvidenceRequest`, the capture schema | none is needed; the prepared records hold the last two whole |
| A new test fixture framework | the existing per-class builders, `PoiyomiFixtureTestBase`, `OverrideTemporaryDirectoryScope`, synthetic `INDMFPlatformProvider` plugins and `AvatarProcessor.ProcessAvatar` cover every case in §13 |
| Profitability / cost modelling, cross-renderer planning, UV repacking, texture modulation, material simplification, a Census launcher or schema change | out of scope |

---

## 13. Test obligations

All deterministic, all public, all EditMode. Layered narrowest-first, and reusing the existing
infrastructure named in §12.2. Each test below must fail under a plausible incorrect implementation.
Where the falsifier is not obvious, the test states it.

| # | Obligation | Falsifier it must catch |
|---|---|---|
| 1 | **Wholly opaque slot**: `sharedMaterials[i]` becomes the opaque result; `sharedMesh` is **reference-equal** to the source mesh and its `subMeshCount` is unchanged | an implementation that clones or rebuilds the mesh for every candidate |
| 2 | **Mixed (`Split`) slot**: submesh `i` retains exactly the transparent triples and submesh `j` exactly the opaque triples, compared as index sets from `GetIndices`; `sharedMaterials[i]` unchanged, `[j]` the opaque result | swapped halves; ordinal-vs-index confusion; a `baseVertex`-blind rewrite (fixture must carry a nonzero source `baseVertex`) |
| 3 | **No split anywhere**: at the preparation seam, every `PreparedRendererSeparation.MeshClone` is `null` | a clone taken for every candidate renderer and then swept, which the final build output cannot distinguish from never cloning |
| 4 | **Planned split later invalidated**: the clone reference retained in prepared state is non-null after preparation, and after `prepare` returns that same reference reports Unity-destroyed (`clone == null` under Unity's overloaded equality); the renderer's mesh, materials and curves are unchanged | an unassigned clone left alive to domain reload; a mesh assigned despite no surviving split; a sweep that never ran |
| 5 | **Valid wholly-opaque slot survives while a `Split` sibling refuses** on the same renderer | renderer-scoped escalation of a slot-local failure |
| 6 | **Every material-swap value maps**: a three-keyframe curve with two distinct admitted materials yields the two corresponding opaque materials at **identical times** (`"R"` formatting) | a mapping over the whole `AdmittedMaterials` list; a mapping of only the current material; any implementation that rewrites times |
| 7 | **An unmapped/new live value invalidates only its slot**: sibling slot still applies | renderer or avatar escalation; discovering the failure after writing |
| 8 | **A marker clip invalidates only affected slots, before mutation**: the marker clip's own curve is unchanged, the source SDK clip asset is unchanged, and an unaffected slot still applies | relying on `SetObjectCurve`'s silent no-op instead of refusing — the single most dangerous failure mode in the design |
| 9 | **A slot with mixed Poiyomi + lilToon admitted materials is entirely unchanged.** The lilToon material must be one that **passes existing alpha-family selection and the closed capture** — an attested supported-shader-name lilToon material — so evidence closes and the slot is actually reached. The test asserts closure succeeded and that the refusal is `OpaqueConversionUnsupportedFamily` | a mapping that converts the Poiyomi members and leaves the lilToon value unmapped; and a fixture that accidentally tests renderer dependency closure instead of conversion-family support |
| 10 | **A Poiyomi slot still optimizes beside a conversion-unsupported lilToon slot on the *same renderer*.** Two slots, one renderer — **required, not incidental**: on separate renderers an implementation that escalated the lilToon slot's refusal to its whole renderer would still leave the Poiyomi renderer succeeding, so the test could not falsify the very escalation it exists to catch. Same closure requirement as #9: the lilToon material is attested and captures successfully, so the renderer's closure succeeds and the refusal under test is `OpaqueConversionUnsupportedFamily`. **[SOURCE]** An *unattestable* transparent or cutout lilToon variant is **not** localized to its slot today — it fails family selection and refuses the whole renderer's closure (§2.6) — so it must not be used here and the test must not be read as claiming otherwise | a feature-owned slot refusal escalated to renderer scope, which is only observable when the surviving slot shares the refusing slot's renderer; a fixture whose lilToon material silently refuses closure, which would make the test pass vacuously on some implementations and fail for the wrong reason on others |
| 11 | **Conversion-only animation affects conversion admission but not alpha proof**: with `material._ZWrite` animated *away from* the default, opaque-candidate triangle accounting is identical to the unanimated case **and** the slot is refused; with it animated *to* the default, the slot converts | folding conversion relevance into alpha relevance (alpha analysis would refuse or its candidate count would change); ignoring conversion-relevant animation entirely (the first case would convert) |
| 12 | **`AlreadyOpaque` maps without a clone**: at the preparation seam, `CreatedClones` is empty and `OpaqueOfAdmitted` maps the source material to **itself** (reference equality); end to end, `sharedMaterials[i]` stays reference-equal to the source and curve values are unchanged | a clone created unconditionally for every convertible-or-canonical material — invisible in final output, because a clone that is created, mapped, unused and swept leaves the same build result |
| 13 | **Source assets unchanged**: source materials' full property sets, the source mesh's characterized state, and the source `AnimationClip` / `AnimatorController` are all unchanged after a successful build; the committed clip is asserted **not** reference-equal to the source clip | any write that reaches an authoring input |
| 14 | **Mesh and per-submesh bounds survive split finalization**: mesh bounds and *every* submesh's bounds, including the untouched ones and the appended one, on a fixture whose authored bounds are deliberately unrelated to its geometry | omitting either write-back; trusting `calculateBounds: false`; leaving the appended submesh at zero bounds — none of which is visible without unrelated authored bounds |
| 15 | **The sweep destroys exactly the unreferenced clones**: at the preparation seam, holding the `CreatedClones` and `MeshClone` references across `prepare`, every clone no surviving slot references reports Unity-destroyed afterwards and every clone a surviving slot still references does **not** | "destroy everything on any refusal", which would strip a surviving slot of a shared clone; no sweep at all. Both are unobservable from final build output alone, which is why this is a seam test |
| 16 | **Assigned generated objects persist through NDMF serialization**: with a real temporary directory, the assigned mesh and materials — including a material referenced *only* by a rewritten object curve — are persistent after the build, with no `SaveAsset` call | assuming curve-only references are not traversed; adding an eager save (which the same test can catch by asserting no abandoned asset was persisted) |
| 17 | **No partial mutation before validation completes**: `prepare` is observably non-mutating — after it returns, and with at least one slot invalidated, every renderer's `sharedMesh`, `sharedMaterials` and every clip curve are exactly as before — **and** every candidate slot was validated before it returned (assert the observed validation count) | per-slot apply-as-you-go; short-circuiting validation on first failure |
| 18 | **Wrong appended index and wrong binding matching fail**: two surviving split slots on one renderer, so `n`, `i+1` and descending-order implementations all fail; plus two clips sharing a display name, a decoy binding at another renderer path, and a decoy binding at another slot, so name-based, path-blind and slot-blind matching all fail | exactly those |
| 19 | **A material-dependent post-closure admission failure does not eliminate a valid sibling.** Discharged at the **prerequisite level** by the merged regression `AmusePlatformFinishPluginTests.RuntimeStateProductionEntry_PostClosureSlotRefusalKeepsTheValidSibling` (`914d9db`) — re-run, not re-created — which proves the surviving sibling reaches the separation plan with per-slot refusals instead of being eliminated. **Feature layer:** falsifier 1 is the consumer proof that a wholly opaque candidate which reaches the feature is applied, and falsifier 15 that only surviving writes are applied. **No standalone feature-level F19 test exists, deliberately: [MEASURED]** the production-like `AnimatorServicesContext` lifecycle materializes a renderer-wide `material.<Property>` float curve as a non-empty `MaterialPropertyBlock` on the build renderer before the barrier, so the pre-existing structural refusal `MaterialPropertyOverridesPresent` refuses the renderer before any feature code runs — a conservative false negative this milestone retains (§15.2). Reaching the feature layer would require bypassing a retained production gate, and the feature layer receives only candidate slots, so it has no representation for an already-refused sibling. Revisit only if real-avatar evidence justifies it | the pre-existing first-refusal return in the per-slot `ResolveSlot` loop (§2.5) — proven fixed by the merged regression; the `MaterialPropertyOverridesPresent` boundary itself is retained, not fixed (§15.2) |
| 20 | **The live current material is used, not barrier state**: between the barrier and pass 3, a probe pass replaces one candidate slot's `sharedMaterials[i]` **without changing the array length**. (a) replaced with a material absent from `OpaqueOfAdmitted` → that slot is entirely unchanged and an independent sibling slot still applies; (b) replaced with a *different already-admitted, already-mapped* material → the slot applies using **that** material's opaque result, and the assertion names the expected opaque material by reference | an implementation carrying `CurrentOpaque` from the barrier: (a) would overwrite a foreign pass's assignment with a stale opaque result, and (b) would apply the wrong opaque material while looking successful. Neither is visible without a same-length replacement between the passes |

**Layering.**

- **Preparation / pass seam** — #3, #4, #12, #15, #17. These inspect prepared state and hold clone
  references across `prepare`, because **final build output cannot distinguish "never created" from
  "created, unused and swept"**, and cannot observe a destroyed transient at all. This uses the
  prepared record and the existing internal pass entry points. **This design adds no allocation

  instrumentation, clone factory, clone registry, or production seam for it**.

- **Feature seams over synthetic fixtures** — #6, #10, #11, #14, #18.

- **NDMF build, through `AvatarProcessor.ProcessAvatar`** with a confined synthetic
  `INDMFPlatformProvider`, following `AmusePlatformFinishPluginTests` and
  `AnimatorServicesReactivationCharacterizationTests` — #1, #2, #5, #7, #8, #9, #13, #16, #20.
  #20 also declares a probe pass between the barrier and pass 3, the same technique
  `AmusePlatformFinishPluginTests` already uses for `ZzzAnonymousOptimizingProducerPlugin` and
  `AfterAmusePlatformFinishObserverPlugin`.

Successful persistence (#16) and source-asset preservation (#13) stay end-to-end NDMF tests, because
those are exactly the claims only a completed build can support. Only #16 points the persistence
scope of NDMF at a real temporary directory. Every other test uses `OverrideTemporaryDirectoryScope(null)`.
The split fixtures (#2, #4, #5, #14, #15, #18) also require a synthetic **importer-backed**

texture — current texture evidence refuses textures without importer identity (§15.2) — so their
tests create the test-owned `Assets/AmuseTests_AlphaSplit` folder in the AssetDatabase and delete it
unconditionally in `finally`, including on assertion failure.
Every fixture tracks and destroys its Unity objects through a `using`-scoped disposable so teardown
runs on assertion failure.

**Vacuity guards.** Tests asserting "unchanged" must first assert the fixture actually reached the
condition under test — a candidate slot was planned, a clone was created, a validation ran — so that a
build which refused for an unrelated upstream reason cannot pass them silently. This is the existing
repository practice ("the fixture no longer produces the classifier result this test needs") and is
mandatory here.

---

## 14. Self-review

| Risk | Finding |
|---|---|
| Contradictions with current code | Four found and corrected in the design: seam E is already closed (§2.1); closure is already renderer-scoped (§2.1); the mesh-finalization route is already characterized (§2.2); the conversion design's obligation-1 wording is stale (§2.1). One contradiction with the *older* note is deliberate and load-bearing: multi-admitted-material slots **are** separable, given curve rewriting (§2.3). |
| Stale investigation claims | Enumerated in §2.3 rather than left implicit. |
| Hidden renderer-wide refusal | Three things, all stated rather than hidden. (a) Two genuine renderer-scoped groups are declared in §8.1 with their dependency justification. (b) The **pre-existing** first-refusal escalation in the per-slot `ResolveSlot` loop is promoted to a merge-first prerequisite (§2.5, §3), discharged by the merged prerequisite regression (§13, falsifier 19). (c) Renderer-wide **material-dependency closure** stays renderer-wide and is disclosed as such in §2.6 and §8.2, not papered over — the local-refusal claim is explicitly conditioned on closure having succeeded. |
| Overclaimed local refusal | Closed. §8.2 now states the three-part qualification; §11 marks the unattestable-lilToon case as closure-scoped rather than slot-local; tests 9 and 10 must assert closure succeeded, so neither can pass through a renderer-wide closure refusal. |
| Mutation before complete validation | §9 orders validate → finalize → sweep → apply, with `AmuseBuildOperation`'s single boundary, and test 17 asserts `prepare` is observably non-mutating with every candidate validated. |
| Stale barrier state reaching a live write | Closed. No current material is carried across the passes: `PreparedSlotSeparation` has no `CurrentOpaque`, pass 3 snapshots and validates the live `sharedMaterials` array (§9.1), finalization builds the output array from that snapshot so unrelated same-count changes survive (§9.2), and test 20 falsifies the stale-state implementation in both its wrong-refusal and wrong-material forms. |
| Duplicated prepared state | Closed. `PreparedRendererSeparation` holds `UnityRendererMutationTarget` whole and `PreparedSlotSeparation` holds `SubmeshSeparationPlan` whole; every value previously copied is derived from them (§4). Neither existing type changes. |
| Unnecessary cloning | Mesh clones require `RequiresAnySplit` **and** a surviving `Split` slot after preparation. Material clones are skipped entirely for `AlreadyOpaque` and deduplicated avatar-wide by source material. |
| Loss of bounds or mesh fidelity | Both characterized bounds obligations are specified explicitly, including the untouched-submesh restore and the appended submesh's inheritance rule; index-only rewriting keeps every vertex attribute, blendshape and skinning datum untouched; test 14 authors bounds unrelated to geometry so the omission cannot hide. |
| Capture-schema / alpha-relevance conflation | Nothing is added to `AlphaRelevanceRequest`. Conversion resolves the *same captured evidence* against `ConversionEvidenceRequest`, only for renderers with opaque candidates. Test 11 asserts the alpha side is bit-for-bit unaffected. |
| Accidental lilToon implementation | No lilToon conversion request, recipe or clone. lilToon reaches exactly one `default:` arm. Tests 9 and 10 pin both directions. |
| Speculative interfaces or registries | §12.2. The extension point is one `switch` arm. |
| Vacuous tests | §13's vacuity guards, plus per-test falsifiers stated in the table's right-hand column. |
| Tests claiming to observe what final output cannot show | Closed. The three clone claims — never created, created-then-swept, retained-then-destroyed — moved to the preparation/pass seam (#3, #4, #12, #15), because a completed build cannot distinguish an unnecessary clone that was swept from one that never existed, and cannot observe a destroyed transient at all. Falsifier wording now states only what each test observes. Persistence (#16) and source preservation (#13) stay end-to-end, where they genuinely belong. |

**No unresolved controller decision remains in this document.** The two previously open ones are
settled: the prepared record holds the existing `UnityRendererMutationTarget` and
`SubmeshSeparationPlan` (§4), and the fourth test delegate has approval, with its scope fixed
(§12, #7).

**Two things this design accepts rather than solves:**

1. **Conversion attestation re-reads and re-hashes the shader source per admitted Poiyomi material**
   (§7.2 step 3), because that is the specified attestation of the merged conversion design and its schema
   list differs from the alpha one. It is redundant with the batch attestation on every fact except the
   required-schema list. Recorded as a known cost, not optimized away, because narrowing an attestation

   to save work is exactly the kind of change that must be argued explicitly rather than assumed.

2. **`AmuseBuildOperation.Execute` requires an `IAssetSaver` this feature deliberately never uses.**
   Passing `context.AssetSaver` and ignoring it is honest. Changing the signature of the type for one
   consumer would not be.

---

## 15. Prerequisite ordering and remaining coverage boundaries

### 15.1 Ordering

One prerequisite must merge before vertical-slice implementation starts:

1. **`fix/scope-slot-alpha-refusal-to-slot`** (§2.5) — from fresh `main`, completed, reviewed and
   merged independently. Scope: the first-refusal behaviour of the **post-closure** per-slot
   `AdmittedMaterialStates.ResolveSlot` loop, and nothing else. Without it, a slot whose alpha
   cannot be resolved eliminates a valid sibling before preparation runs.

2. **This vertical slice** — resumed or recreated from updated `main`.

Nothing else remains open, and no controller decision is left open (§14). Renderer-wide
material-dependency closure is **not** a second prerequisite: it stays as it is, disclosed as
existing coverage pressure (§2.6, §15.2).

### 15.2 Remaining coverage boundaries — false negatives only

Runtime texture evidence is already in `main` (`346f231`, PR #26) and supports its admitted formats and
host gates, so texture-backed triangle proof is a working capability rather than pending work. What
remains are bounded, verified false-negative boundaries. Each limits *how many* triangles are proven

opaque. None affects any mechanism in this design, and this feature must not widen any of them.

**[SOURCE]** Verified against current code:

- **Poiyomi alpha mask.** An **assigned** `_AlphaMask` in Replace mode is still unsupported — the
  pinned equation needs a red-channel texture field AMUSE does not produce, and the unbound-mask
  argument ("`mask.r` is exactly one, so the fused multiply-add cannot round differently") is
  unavailable once a mask is sampled. Multiply, Add and Subtract mask modes each combine a term the
  closed scalar vocabulary cannot express.

- **Texture formats.** Alpha-field acquisition admits `RGBA32`, `ARGB32`, `Alpha8`, `RGB24`, `DXT5`
  and `BC7`. Float formats and `DXT5Crunched` are refused. Streaming textures are refused outright.

- **Sampler state.** Point and Bilinear filtering only, with equal Clamp/Repeat wrap. Nonzero mip
  bias and Trilinear are refused for scope rather than soundness. Anisotropy above 1 is refused
  because the classifier does not model an elongated footprint.

- **Asset identity.** A texture with no importer — scene-only or generated — cannot prove a source
  identity or a colour interpretation, so it is refused.

- **Host gates.** Texture-alpha capture requires the lazily evaluated once-per-AppDomain host
  capability check to pass, and the `StandaloneWindows64` build target. A failure refuses every
  texture-alpha capture for the remainder of the AppDomain, with no partial credit and no retry.

**Renderer-wide material-dependency closure (§2.6).** One unsupported material on a renderer —
a locked Poiyomi material, an unattestable transparent or cutout lilToon variant, a third-family
shader — refuses **every** slot on that renderer, including attested slots that would still
separate. This is the widest false negative in the milestone and the design keeps it deliberately:
narrowing it means changing the single closed capture representation, which needs real coverage

evidence rather than a coverage wish. It is neither a prerequisite nor a follow-up specified here.

Structural refusals unchanged by this feature also remain: property blocks, non-triangle topology,
animated mesh or slot count, and every host structural refusal.

**Material-property animation under the reactivated extension.** **[MEASURED]** A renderer-wide
`material.<Property>` float curve committed through the production-like `AnimatorServicesContext`
lifecycle materializes as a non-empty `MaterialPropertyBlock` on the build renderer before the
barrier, so the pre-existing structural refusal (`MaterialPropertyOverridesPresent`) refuses that
renderer before any feature code runs. Retained deliberately as a conservative false negative —

the same direction as every boundary in this section. Revisit it only if real-avatar evidence
justifies it. This is why falsifier 19 (§13) has no standalone feature-level fixture.

The feature is fully exercisable on public synthetic fixtures — constant-alpha for most falsifiers
and the synthetic importer-backed texture for the split fixtures (§13) — so none of these boundaries
gates implementation.

---

## 16. Explicit non-goals

lilToon opaque conversion. Any conversion interface, registry, or result hierarchy. Any universal
material, render-state, mesh, or mutation IR. Changes to `MaterialSemantics`, `MeshSeparationPlan`,
`MeshSeparationPlanner`, `UnityRendererAlphaSnapshot`, `UnityRendererMutationTarget`,

`AmuseBuildOperation`, `MaterialEvidenceRequest`, or the capture schema. Reference counting or any
generated-asset lifetime registry. A curve-remapping framework or an animation reachability graph. A

cross-pass transaction framework. Non-readable mesh support. Multi-renderer or cross-renderer planning.
Profitability and cost modelling. UV repacking, texture modulation, material simplification. Any Census

launcher, metric, or schema change. Any change to the upload-authorization or lifecycle boundaries.
Fixing the pre-existing slot→renderer refusal escalation **inside this branch** — it is a separate
prerequisite that merges first (§2.5, §15.1), and this design neither implements it nor depends on
its internal shape.
