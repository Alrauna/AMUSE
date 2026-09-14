# Property blocks as effective material state

Date: 2026-09-14. Status: design for review. No implementation has started.

Privacy note. The motivating observation comes from private avatars. This
document names no avatar, renderer, material, or animation asset.

## Problem

AMUSE refuses any renderer that carries a Unity property block. The refusal
is presence-only and fails closed. This session proved the block writer is
not avatar authoring: the NDMF animator commit rebinds the live Animator,
and Unity's animation runtime stores sampled `material._X` values as
renderer-wide block state. The call path is `AvatarProcessor.ProcessAvatar`,
then the NDMF animator commit, then
`GenericPlatformAnimatorBindings.CommitControllers`, which assigns the
committed controller. The engine writes the block during that assignment.

Every real build of an avatar whose renderers animate material properties
therefore reaches the PlatformFinish pass with a transient block present.
The presence-only refusal then marks those renderers unanalyzable. The
thirteen test failures diagnosed on 2026-09-13 are the same mechanism seen
through the test fixtures.

The product consequence is reported behavior from private avatars: renderers
that swap whole materials through animation do not split, although their
triangle populations contain faces that are opaque under every swap state.
The swap machinery is not the missing piece. The block refusal stops the
renderer before that machinery runs.

The swap machinery already exists and is tested:

- `UnityAnimationEvidenceCapture` admits `m_Materials.Array.data[N]`
  object-reference curves as material swaps.
- `AdmittedMaterialStates` proves per-triangle opacity across admitted
  states. Two distinct opaque admitted states proving one triangle is a
  passing contract test.
- `AlphaSeparationApply` rewrites swap curves onto the appended opaque slot.

## Goal

Renderers whose blocks are part of their effective material state become
analyzable. Blocks become an evidence source, not a refusal trigger. The
existing closure algebra and refusal reasons stay unchanged and keep
refusing everything they refuse today.

## Non-goals

No new shader families. No generalized mutation framework. No changes to
swap capture, closure semantics, or the canonical opaque recipe. No release
or VPM changes. Indexed material property syntax such as `material[0]._X`
keeps refusing; it is a separate binding form.

## Semantic model

The effective material state of slot `i` applies three layers in Unity's
precedence order: per-material-index block, then renderer-wide block, then
serialized material values. The animation closure sits on top. An animated
property's runtime domain is still its captured value set, and the existing
per-property refusal reasons apply to that domain exactly as today. A block
entry for an animated property is the current carrier of one value from
that domain. A block entry for an unanimated property extends the base
value the proof rests on.

## Stage 1: materialization

A new internal type in `Editor/Host/` materializes effective state. For
each slot it clones the admitted material and copies every block entry onto
the clone, in precedence order. The materialized clones become the capture
inputs where `sharedMaterials` enter capture today. Nothing downstream
changes: family selection, `MaterialEvidenceRequest` scoping, `AlphaFieldSet`
predicate keys, and the GPU alpha readback all read the clone as they read
any material. A block texture override reaches the readback as an ordinary
texture assignment on the clone, with the existing texture-identity oracle.

The copy schema comes from the shader's own property list through the
`Shader` property enumeration API. A `MaterialPropertyBlock` cannot be
enumerated, so capture probes every property the shader declares and reads
the block for each. This closes the soundness question: a key the shader
never declared cannot change rendering, so leaving it uncopied is exact.
Properties are declared once per Shader, not per subshader, so the
enumeration is complete.

Renderers without blocks take a zero-cost fast path. The clone is skipped
and the original material is captured as today. Clones are transient capture
inputs owned by the existing capture scope. They are not retained evidence;
captured records keep holding values, never live objects. `AlphaSeparationApply`
re-materializes during revalidation, so a block that changed between prepare
and apply cannot validate a stale proof.

## Contract changes

`HostStructuralRefusalFor` drops the presence check at both call sites.
`MaterialPropertyOverridesPresent` leaves `RendererAnalysisRefusal`, and its
strings leave `AmuseReportStrings`. Under effective-state semantics no block
case must refuse: declared keys are proven through the clone, and undeclared
keys are render-inert. This is the structural refusal contract change the
diagnosis task deferred to a design question. The user approved exploring it
on 2026-09-14.

Pins that change, with justification:

- The deliberate-block refusal tests in `UnityRendererAlphaAnalysisTests`
  become acceptance tests: a block present and materialized yields an
  analyzable renderer with the block's values in the proof inputs.
- `RendererRefusalCalibrationTests` in the research package flips from
  observing refusal to observing analysis of the overridden state.
- The per-material-index presence pin stays. Capture still needs per-index
  reads, and the pin guards presence detection.
- The thirteen animated fixtures on the fix branch carry the
  `ClearCommitAnimationPropertyBlocks` helpers. That commit is a
  deliberate, temporary weakening: the tests classify a cleaned
  renderer, not the real commit output. Stage 1 acceptance must delete
  the helpers and re-strengthen the fixtures, so the tests measure
  production behavior through the real commit-written block again.
- The refusal-site doc comment at the top of `UnityRendererAlphaAnalysis`
  is rewritten to state the effective-state claim.

## Falsifiers

- A block overrides `_Cutoff` to a provable value. The renderer must be
  analyzable, and the proof must use the overridden value.
- A block swaps `_MainTex` to another texture. The readback must prove the
  override texture's sampled domain, not the original's.
- A block entry joins an animated property whose captured domain has more
  than one value. `AnimatedMaterialPropertyNotSingleton` must refuse.
- Renderer-wide and per-index blocks disagree. The per-index value must win.
- A block contains only keys the shader does not declare. The renderer must
  be analyzable with serialized values.
- A block defines a base value for an animated property, and the
  animation disagrees with the serialized default. The existing
  relevance-layer refusal must fire.
- A material-swap renderer with transient block present, all states opaque
  on one triangle population. The renderer must split, and the swap curve
  must map onto the appended opaque slot.

## Stage 2: per-property domains, gated

Replace materialization for scalar, color, and vector entries with
per-property value domains inside `Analysis`: serialized union block union
closure set. Keep materialization for texture entries. Stage 2 buys
per-property refusal precision and removes clone cost for scalar-heavy
blocks. The decision gate is measured clone and readback overhead on
many-slot renderers from stage 1, not speculation.

## Validation plan


Refresh the editor, then run the two product fixture classes, the full
`Alrauna.Amuse.Tests.Editor` assembly, and the full
`Alrauna.Amuse.Research.Tests.Editor` assembly. The thirteen previously
failing tests must pass twice in a row in one editor session. New falsifier
tests must fail before the fix and pass after, for one named wrong
implementation each. A filtered run that reports zero tests is a failure.
Record observed counts.

After stage 1, one read-only characterization run in the Census Lab editor
confirms the production hypothesis on a private swap avatar: the transient
block is present, the renderer analyzes, and the split outcome matches the
admitted states. The lab run records counts by role, never identifiers.

## Risks

Clone fidelity: the copy must cover every declared property type, including
int-typed gates. The enumeration covers them, and the falsifiers pin the
alpha-relevant ones. Capture cost grows with block-carrying slots; the
no-block fast path bounds it, and private avatars animate few slots. Clone
lifecycle leaks are owned by the existing AMUSE clone sweep in Apply.
