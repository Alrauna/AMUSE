# Swap admission from the live clip index (single source)

Date: 2026-09-15. Status: design addendum for review. Implementation has
not started.

Parent spec: the animated material swap design on 2026-09-14. Confirmed
diagnosis: the investigation record of 2026-09-14 and 2026-09-15.

Privacy note. This document names no avatar, renderer, material, or
animation asset. Renderers are named by role: the top garment renderers
and the skirt renderer.

## Problem, measured 2026-09-15

A Lab build with the slot-level reports produced exact refusal lines:

1. Each top garment renderers' slot 0 prepared, then dropped at apply
   with RuntimeMaterialValueNotMapped. The material the slot held at
   apply time was a palette variant. The proven mapping covered only the
   serialized current material.
2. Twenty-four further slots across palette-swapped renderers dropped
   the same way.

The palette variants are swap values inside the FX controller. The
apply pass reads them from the reactivated AnimatorServicesContext's
AnimationIndex. The barrier captured swap admissions from
CommittedControllerGraph.Enumerate over the innate controllers. The two
sources disagree on this avatar: the barrier admitted only the
serialized current material, the apply-time index contains the palette
variants.

The refusal is correct and fail-closed. A palette variant was never
proven, so the slot keeps its original material. The coverage defect is
upstream: barrier admission and apply validation read different
sources.

## Design

One source of truth: the barrier admits swap values from the same
AnimationIndex the apply validates against.

1. The barrier pass declares the AnimatorServicesContext as a required
   extension, so the barrier and the apply share one extension scope.
   NDMF re-virtualizes once before the barrier. It commits once after
   the apply.
2. Swap-value admission reads AnimationIndex.GetClipsForObjectPath for
   each renderer path. Every material slot binding on that path admits
   its values exactly as the current capture does, with the same
   fail-closed checks: valid slot parse, slot below the current count,
   every value a supported material.
3. The committed-graph walk remains, and keeps its present duties: float
   bindings for proof-relevant properties, special-motion marking,
   animation-event refusal, additive-layer and blend-tree facts, and
   behaviour identity validation. Object bindings for swap admission
   become a union: walk-derived observations plus index-derived
   observations for the renderer path. The union can only grow
   coverage. Every admission check still fails closed.
4. RuntimeMaterialValueNotMapped keeps its meaning: a live value the
   barrier never proved. With one source it names a genuinely foreign
   material injected between barrier and apply, and stays a refusal.

## Consequences

1. Lifecycle: the barrier observes the virtual graph, not the committed
   graph. Content is equal: NDMF re-virtualizes from the committed
   state. Stage-1 chose the committed observation deliberately, so this
   is a contract change and needs review.
2. Block state timing: the engine writes the transient property block
   during the commit at extension deactivation. With one scope there is
   no deactivation between bindings and apply, so the barrier's block
   snapshot is empty and the apply's block check compares empty to
   empty. The proof instead covers the curve values directly, which is
   the same value set the block would carry. The stage-2 per-property
   block domains lose their input on this path. The curve admission
   covers the same values at the same exactness.
3. Build cost: every reachable palette variant is captured, proven, and
   converted. More variants means more capture and conversion work per
   build. The no-block fast path bounds the cost as it does today.
4. Census Lab parity: the Lab build exercises the same single scope, so
   the manual test and the reports stop diverging.

## Falsifiers

1. A renderer whose swap curve lives only inside a blend tree in a
   VRCFury-merged layer admits the variant and prepares. A states-only
   walk misses it. The index source does not.
2. A palette variant whose shader is unattested refuses admission with
   InvalidSwapValue at capture, before any conversion runs.
3. Two clips animate the same slot with two different variants. Both
   variants admit. The singleton rule still refuses conflicting float
   values, and the swap mapping covers both variants.
4. A material injected by a later pass at apply time, present in no
   clip, still refuses with RuntimeMaterialValueNotMapped.

## Test plan

1. RED: an integration test whose swap clip is reachable only through
   the index source admits the variant and prepares. Fails on current
   code with RuntimeMaterialValueNotMapped at apply.
2. The full product suite plus the research suite after each task.
3. A Census Lab characterization run on the private swap avatar: the
   top garment renderers split. The skirt still refuses on its feature
   gate. Every refusal line names renderer, slot, and cause.
