# Way-4 ordinal scoping and swap-consent subjects: design

Date: 2026-10-07. Branch plan: `feat/census-refusal-coverage` or a child of
it, two commits, Part A first. No source-asset mutations. All mutations stay
inside the NDMF build copy.

Privacy note: this spec names no private avatar, renderer, material, scene,
or machine identity. lilToon and Poiyomi shader names are public vendor
identifiers. The avatar in prior records is "the avatar under test".

Labels: `[SOURCE]` is a fact read at cited lines in this tree.
`[INFERENCE]` is a conclusion. `[DECISION NEEDED]` is a choice for the user.

## 1. Summary

Two changes, one shared goal: a renderer should refuse only the slots that
carry the problem, and the consent dialog should see every material the
avatar can actually show.

**Part A.** The closed batch capture currently fails the whole renderer when
one material fails source verification. The capturer layer already knows
which material failed. This part puts that ordinal into the capturer
contract, turns the failing material into the existing per-slot sentinel
refusal, and deletes the renderer-wide closure failure path.

**Part B.** The D8 consent pre-scan walks assigned materials only. A material
that only an animation swap can bring onto a slot never gets a consent
subject. It fails the batch unconsented and, before Part A, refuses the whole
renderer. This part extends the pre-scan to swap-reachable materials, so the
user is offered the same click-through the assigned materials get.

The two compose: Part A makes any residual refusal per-slot. Part B removes
the refusal entirely when the user consents.

## 2. Ground truth: the three refusal layers today

`[SOURCE]` for all cites.

**Layer 1, selection.** One exact-name map
(`UnityMaterialSemantics.cs:670-801`). A name no family selects produces an
unsupported sentinel per material. Slot resolution refuses that slot only.
This layer is already slot-scoped and stays untouched.

**Layer 2, per-material attestation inside capture.** In
`CaptureObserved`, a selection-admitted material whose captured result fails
`IsAttestedAlphaMaterial` is currently impossible to isolate: the wrappers
impose all-or-nothing on top of a batch that was already built
per-material. `TryCaptureClosedAlphaMaterials` loops the per-material result
list and returns false on the first unattested member, discarding the
ordinal it is holding
(`UnityMaterialSemantics.cs:319-325`). The transferred variant does the
same, except a member whose shader name is in the granted set continues
(`UnityMaterialSemantics.cs:388-407`). The capture then returns
`Failed(MaterialDependencyClosureFailure.UnattestedMaterial)`
(`UnityAnimationEvidenceCapture.cs:721-723`), which is renderer-wide.

**Layer 3, renderer-wide plumbing.** The `Failed` helper builds an empty
evidence record with only graph facts
(`UnityAnimationEvidenceCapture.cs:477-494`). `IsClosed` is
`ClosureFailure == None` (`CapturedAnimationEvidence.cs:218-220`). The
plugin gate refuses renderer-wide on `!IsClosed` and returns empty slot
results (`AmusePlatformFinishPlugin.cs:1170`). The renderer report entry
names the way through `ClosureFailureWayFor`
(`AmusePlatformFinishPlugin.cs:839-841, 1353-1360`).

The slot-scoped machinery from the S1 slice already exists and Part A
reuses it: `SlotClosureFailure` records
(`CapturedAnimationEvidence.cs:17-45`), the per-slot refusal loop in
`ResolveRuntimeStates` (`AmusePlatformFinishPlugin.cs:738-756`), and the
apply-side appended-slot guard keyed on `SlotClosureFailures`
(`AlphaSeparationApply.cs:601-611`).

## 3. Part A: way-4 ordinal scoping

### 3.1 The one-line insight

`CaptureBatch` already produces one result per material
(`UnityMaterialSemantics.cs:411-423`). The all-or-nothing refusal is a
wrapper loop that throws away the index it just checked. No capture
capability is missing. The contract just refuses to carry the fact it
already has. `[SOURCE]`

### 3.2 Contract change

`ClosedAlphaMaterialCapturer` (`UnityAnimationEvidenceCapture.cs:87-98`)
keeps its bool-free shape but reports failures:

```csharp
internal sealed class ClosedAlphaCaptureOutcome
{
    // Captured results for the surviving batch, in batch order.
    internal IReadOnlyList<CapturedAlphaMaterial> Captured { get; }
    // Batch ordinals whose captured result failed attestation and was
    // not admitted. Empty on full success.
    internal IReadOnlyList<int> UnattestedOrdinals { get; }
}
```

The delegate becomes
`ClosedAlphaMaterialCapturer(..., out CapturedAlphaMaterialOutcome outcome)`
returning false only for a failure that names no material. After the
change, no production caller can produce that case: both wrappers fail
per member, and `CaptureBatch` returns a list or throws. The false return
stays in the contract for tests and for a future genuinely batch-wide
capability failure; the closed refusal for it remains
`UnattestedMaterial`. Approved on 2026-10-07: delete the renderer-wide
path (decision A1, section 3.6).

Both production entries change:

- `TryCaptureClosedAlphaMaterials`: the wrapper loop records each
  unattested member's index into `UnattestedOrdinals` and keeps going.
  `Captured` carries the survivors.
- `TryCaptureClosedAlphaMaterialsTransferred`: same loop, with the
  granted-name skip it already has
  (`UnityMaterialSemantics.cs:388-407`). An ungranted unattested member
  lands in `UnattestedOrdinals` instead of failing the batch.

`DefaultCapturer` (`UnityAnimationEvidenceCapture.cs:117-127`) forwards the
outcome unchanged.

### 3.3 Capture change

In `CaptureObserved`, the batch call site
(`UnityAnimationEvidenceCapture.cs:714-733`) becomes:

1. Capturer returns the outcome. Every ordinal in `UnattestedOrdinals`
   maps back through `attestedIndices[ordinal]` to its admitted index, and
   that index receives the existing sentinel:
   `UnityMaterialSemantics.UnattestedMaterial(lockedRefusal, material,
   resolveRegisteredSource)` — the same sentinel the selection-failure
   path builds at `:672-684`, including the locked-identity pre-check so
   the refusal keeps naming the cause.
2. The surviving captured materials fill `capturedByIndex` exactly as the
   success path does today.
3. The `Failed(UnattestedMaterial)` return and, per `[DECISION NEEDED
   A1]`, the whole renderer-wide closure path, go away.

Sentinel consumers need no change: `CapturedAnimationEvidence` already
documents that a sentinel never fails the renderer and its slots refuse as
semantics-unknown (`CapturedAnimationEvidence.cs:231-238`). Slot
resolution refuses those slots through the existing
`AdmittedMaterialSemanticsUnknown` path, which the audit measured per slot.

### 3.4 Report and enum changes

- Slot refusals keep `MaterialDependencyClosureFailed` out of it: a
  batch-failed material refuses its slots as `AdmittedMaterialSemanticsUnknown`
  with the named cause, exactly like a selection-missed material. The
  `ClosureFailureSentence` wording for `UnattestedMaterial`
  (`AmuseReports.cs:420`) survives for the transferred-capture tests that
  pin the consent wording.
- With the renderer-wide path deleted: remove the `ClosureFailure`
  property and constructor parameter, remove `IsClosed`, remove the
  `Failed` helper, remove the plugin gate at `:1170` and
  `ClosureFailureWayFor`'s renderer-wide branch, and remove the
  renderer-wide closure report entries. The enum keeps all five members:
  the slot-scoped records still use `MissingCurrentMaterial`,
  `SlotOutOfRange`, and `InvalidSwapValue`, and the sentinel path still
  answers through `UnattestedMaterial`. The renderer-level entry for a
  fully failed renderer keeps working through the S1 all-slots-failed
  mirror, which names the first failed slot's way.

### 3.5 False-positive safety

- A batch-failed material keeps its admitted index and its slots refuse.
  No triangle moves on a refused slot. The apply pass revalidates every
  candidate against live state and against `SlotClosureFailures`
  (`AlphaSeparationApply.cs:601-611`), so a split can never append a slot
  that a failed binding could address. That guard was built for the S1
  ways and covers this change with no new conjunct: the failed material's
  slots are ordinary refused slots.
- The alpha relevance union is computed from selection-admitted requests
  before the batch runs (`UnityAnimationEvidenceCapture.cs:696-704`), so
  removing the batch-failure return does not shrink any union. The
  committed behavior stays the more-refusing one recorded in amendment 3
  of the coverage record: failed materials stay in the relevance union.
- The transferred variant's consent gate is unchanged: a granted name
  skips verification for exactly that name this build; everything else
  fails named. Declining consent still grants nothing.

### 3.6 Decisions

**A1, approved 2026-10-07:** delete the renderer-wide closure path
entirely. No dormant path. A future genuinely batch-wide failure
reintroduces the member explicitly, with its own design.

**A2, approved 2026-10-07:** capture the survivors in the same capturer
call. One call returns survivors plus failing ordinals. No retry loop.
Rationale: a plain-bool retry is blind, because the contract carries no
failing identity, so any retry that can remove the failed member must
first carry the ordinal, which is this contract's data. The `false`
return stays as the door for a future genuinely batch-coupled capture
mode; the retry gets designed there, against a real requirement.

### 3.7 Tests (RED/GREEN)

RED first, each against the committed all-or-nothing behavior:

1. Wrapper: a two-material batch with one unattested member returns the
   survivor in `Captured` and the failing ordinal in `UnattestedOrdinals`.
   Committed behavior: returns false, no survivors. Falsifier: an
   implementation that reports ordinals but drops survivors fails the
   survivor assertion.
2. Wrapper transferred: an ungranted unattested member reports its
   ordinal; a granted one is captured. Committed behavior: whole batch
   false.
3. Capture: a renderer with two slots, one holding a poison material,
   closes with the healthy slot fully analyzable and the poison slot
   refusing with the named cause. Committed behavior: empty slot results.
4. Capture: all materials fail. The renderer entry is the all-slots-failed
   mirror naming the cause, with no renderer-wide closure entry.
5. Deletion guards: no test may construct a renderer-wide
   `ClosureFailure` anymore; the compile itself enforces the parameter
   removal. Re-specify the S1 tests that pin `Failed` and
   `ClosureFailureRetainsGraphFactsButNoPartialEvidence`
   (`UnityAnimationEvidenceCaptureTests.cs:1179-1203`) to pin the
   slot-scoped outcome instead.
6. Apply: end-to-end appended-slot hazard with a batch-failed slot,
   mirroring the S1 hazard test, so the existing guard demonstrably
   covers the new refusal source.

**Implemented on 2026-10-07** (Part A, one commit): all three stages ran
RED before GREEN. Observed after the cutover: the semantics group passed
36 of 36, the capture group 87 of 87, the plugin and apply groups 111 of
111, and the full product assembly 2497 of 2497 with 2 by-design
inconclusive rows. The S1-era tests that pinned the renderer-wide return
were re-specified under new names; the brief's names for them were
stale. The brief's renderer-entry deletion wording was superseded by
section 3.4's own constraint: the entry stays and names the first failed
slot's way.

## 4. Part B: consent subjects for swap-only materials

### 4.1 Mechanism today

The D8 consent block runs early in `Execute`
(`AmusePlatformFinishPlugin.cs:439-490`), before the structural graph
enumerates. It walks `AllAssignedMaterials`
(`AmusePlatformFinishPlugin.cs:1080-1093`): renderer sharedMaterials only.
`CollectTransferConsent` dedupes by shader instance, skips unsupported
families, and offers one subject per distinct unverified supported shader
(`UnityMaterialSemantics.cs:481-531`).

A material that appears only inside animation swap curves is absent from
that walk. It gets no subject, so a consent-granting build grants nothing
for it, and the transferred capturer fails the batch on it. Before Part A
this refused renderer-wide; after Part A it refuses per slot. Amendment 5
of the coverage record documents exactly this hole for a locked old
Poiyomi 9.0 clone. `[SOURCE]`

### 4.2 The clip universe already exists

The structural pass enumerates the real pre-virtualization graph and
stores it; the capture reads clips from the stored graph's layers
(`AmusePlatformFinishPlugin.cs:503-522`,
`UnityAnimationEvidenceCapture.cs:327-333`). Reading material swap keys
from clips has one existing convention:
`AnimationUtility.GetObjectReferenceCurveBindings` plus
`GetObjectReferenceCurve` (`LiveAnimationObservation.cs:96-97`).

### 4.3 Design

1. **Move the consent block after the graph gate.** The consent, window,
   and swap-in block currently sits before the structural check. Move it
   directly before the swap-in, which already runs after the graph gate
   (`AmusePlatformFinishPlugin.cs:527-537`). The stored graph is then
   available to the pre-scan.
2. **Extend the material walk.** New private enumerator
   `AssignedAndSwapCurvedMaterials(context, graph)`: assigned materials as
   today, then for every clip in every stored graph layer, the material
   values of its object-reference curves, read through the
   `LiveAnimationObservation` reader. Null and non-material values skip.
   The result feeds the same `CollectTransferConsent`, which already
   dedupes by shader instance.
3. **No new gates.** The subjects and granted names flow into the
   existing three consumers unchanged: the transferred capture skip, the
   conversion boundary skip, and `GrantedAwareOriginalAttestation`
   (`AmusePlatformFinishPlugin.cs:452-478`).

### 4.4 Safety

- Strictly more prompts, never weaker gates. A declined build grants
  nothing, exactly as today. Batch mode still refuses.
- Unsupported families produce no subject (`UnityMaterialSemantics.cs:496-500`).
  A gem material reachable only by swap stays unsupported; after Part A
  its slots refuse per slot, and no dialog mentions it.
- The swap-in path is unaffected for swap-only materials: the swap-in
  touches materials assigned at build start, and a swap-only material is
  not assigned. Its consent rides the capture and conversion gates, which
  accept exactly the granted name.
- Shader names, not material references, are what grants name. Clip
  keyframes hold real material references; the pre-scan reads shader
  names from them and never mutates anything.
- Virtualization: the stored graph is the pre-virtualization view by
  construction, the same view the committed-graph gate and the capture
  already trust.

### 4.5 Behavior change to record

Moving the block after the graph gate changes one user-visible thing: an
avatar that refuses avatar-wide on the committed-graph gate no longer sees
the consent dialog. Today it is asked and then the build stops. The
bindings-invariant throw also precedes the dialog now, so a defect-state
caller fails fast before any user interface. The reporting order of
`ConsentDeclined` moves with the block. Approved on
2026-10-07 (decision B1): move the block after the graph gate. No
pointless prompts, one clip-walk convention, no duplicate enumeration.

### 4.6 Tests (RED/GREEN)

1. Pre-scan: a material present only in a swap curve, on an
   otherwise-unconsented avatar, produces its subject and granted name.
   Committed behavior: no subject.
2. Pre-scan: a granted swap-only name flows to `grantedShaderNames` and
   the transferred capture admits it end to end through a stand-in
   frontend.
3. A declined build grants nothing for swap-only materials; their slots
   refuse per Part A.
4. A graph-refused avatar runs no consent dialog (pins the moved block).
5. Falsifier: an implementation that grants by material reference instead
   of shader name must fail test 2's distinct-materials-same-shader case.

**Implemented on 2026-10-07** (Part B, one commit): RED observed with
three of the four new tests failing as predicted. The fourth, the
declined-build test, passed on first run and is recorded as
characterization: decline semantics and the Part A ordinal contract both
predate it. Green: the consent and plugin groups passed 101 of 101, and
the full assembly 2501 of 2501 with 2 by-design inconclusive rows. The
declined-path per-slot refusal is pinned at the capture level, not
through the full pipeline: consent is all-or-nothing, so an ungranted
build halts before capture and no pipeline test can reach a slot
refusal. Two pre-existing declining-consent fixtures seed the retained
bindings now that the block runs after the bindings invariant; their
assertions are unchanged.

## 5. Sequencing

Part A first, one commit, full product assembly green. Part B second, on
top, because its end-to-end consent test uses Part A's per-slot outcomes
for the declined path. The Census Lab validation for both lands with the
next authorized Lab build: the transferred-capture tests pin the consent
semantics in-repo, and the Lab exercises the real dialog and the real
swap-only 9.0 row.

## 6. Out of scope

- The gem verdict stands: no gem frontend, no conversion path. Gem
  materials keep refusing per slot, with no consent subject offered.
- The way-1-to-3 slot machinery, the apply guard, and the report shapes
  from S1 do not change.
- No new consent scope beyond shader names: host-version subjects are
  untouched.
