# lilToon layer alpha support implementation plan

Date: 2026-09-15. Status: plan for review. Implementation has not
started.

Parent design: the lilToon layer alpha support design of this date.

Every behavior change in this plan follows the repository RED/GREEN
discipline. A task names the plausible wrong implementation the red
test must fail. An assertion that passes on its first run is recorded
as characterization and never dressed up as red.

## Task order

### Stage 0

1. Write the dated investigation record that pins the layer equation
   from the installed vendor include tree in the dev editor instance.
   The record pins: the UV mode property names and legal values per
   layer, the scroll and rotate property names per layer, the alpha
   mask property names and modes per layer, every alpha write site,
   and the off-state reduction of each layer function. Borrowed facts
   carry tags. No vendor source is committed.
2. Sweep the record for identifiers and style. Stop conditions: any
   pinned fact that contradicts this design changes the design first,
   with a dated addendum, before code.

### Stage A

3. Extend the capture allowlist for no-alpha formats and wire the
   importer theorem into the alpha field admission for both capture
   routes, generated and streaming.
   - Red first: a fixture whose main texture imports as a compressed
     format without an alpha channel refuses today with
     `UnsupportedFormat`, and classifies proven-opaque after the
     change. The plausible wrong implementation is an allowlist edit
     that skips the streaming route, or a theorem application that
     skips the mask term.
   - Assert the capture refusal vocabulary stays closed.
4. Implement the shared layer alpha term with the four alpha mode
   writers.
   - Red first, one falsifier per alpha mode: replace, multiply, add,
     subtract. Each falsifier fixture uses an independent outcome
     oracle. The plausible wrong implementation composes the mode in
     the wrong order or drops the saturate.
   - Red: the off-toggle fixture. A layer toggled off with an unknown
     mode must stay admitted. The plausible wrong implementation reads
     the mode behind a disabled toggle and refuses.
5. Remove the two layer toggles from the coverage gate lists of both
   frontends. Extend both evidence requests with the pinned layer
   facts. Add the named refusal diagnostics for the stage A refusals:
   layer UV mode, layer scroll, layer alpha mask, layer alpha mode.
   - Red: a layer at a non-zero UV mode or a non-identity scroll
     refuses with its named diagnostic and keeps its sibling slots
     clean.
   - Full suite after this task. The gate removal touches both
     frontends and their characterization tests.
6. Add the suspected mistake report for no-alpha textures.
   - Red: a plausible implementation that refuses, or that stays
     silent, fails the fixture asserting exactly one information line
     with the renderer, slot, material, and property, plus a split.
7. Checkpoint. Run both suites. The product owner re-runs the Lab
   characterization. Update the investigation record with a dated
   subsection. This task blocks stage B.

### Stage B

8. Write the stage B design addendum from the stage 0 record: the UV
   mode encodings and the scroll and rotate equation per layer.
9. Implement the layer UV modes and the scroll and rotate path on the
   exact UV geometry.
   - Red first, one falsifier per admitted UV mode, plus a scroll
     falsifier with an independent oracle. The plausible wrong
     implementation samples the layer at the main UV without the layer
     transform.
10. Final validation. Both suites green. Lab re-characterization by
    the product owner. Investigation record dated subsection. Open
    the pull request.

## Stop conditions

1. Stage 0 pins a layer behavior the exact scalar or UV algebra cannot
   carry exactly. Stop. Return to the design with the evidence.
2. The defect branch is not merged before the stage A checkpoint.
   Stop at the checkpoint. Do not read Lab numbers the defect can
   mask.
3. Any new refusal would need to widen beyond a named property scope.
   Stop and return to the design.

## Git and review boundaries

1. The defect branch lands first. This branch rebases on it before
   the stage A checkpoint.
2. One pull request for this branch, opened after stage B validation.
3. No staging, commit, push, or merge beyond the standing per-branch
   authorization.
