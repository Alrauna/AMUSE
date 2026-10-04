# LAC persisted copies refuse the capture; round two of the more-moved question

Privacy note: This record uses only this repository's own sources, the
public LAC package state in the dev and Census Lab projects, and in-memory
characterization runs. It names no avatar, renderer, material, or texture
beyond role descriptions. It writes no instance names, hashes, ports, or
machine paths. Slots appear by ordinal. Counts are observed counts from
the Lab characterization runs, kept to the minimum that makes the
argument checkable.

Date: 2026-10-03. Re-verified and corrected on 2026-10-04.
Branch: `fix/generated-route-alpha-policy-erasure`. Correction pass on
`fix/lac-persisted-copy-capture`, base `main` at `08d09f9`.
Investigation status on 2026-10-03: the first round's mid-band erasure
defect is real but orthogonal to the reported symptom. This round measured
the actual LAC producer shape in the Census Lab and found a different,
coverage-direction defect, plus an unexplained gap against the reported
symptom. The gap needs one fact only the reporter holds: the exact two
runs the comparison came from.
Investigation status on 2026-10-04: an adversarial re-derivation struck
three claims of this record. The corrections are dated in place. See the
correction section below. The surviving product defects are admission
decisions, so no production fix rides on this branch.

## Correction summary, 2026-10-04

The re-derivation reproduced every reachable claim from primary evidence.
It used the LAC 0.9.0 source in the Census Lab project, the NDMF 1.14.4
source in this repository, gate-level probes in the dev editor instance,
and a fresh two-arm build rerun in the Census Lab editor instance. Both
editor instances passed the project identity check. The evidence section
at the end lists the observed facts.

Struck claims and their replacements:

1. Struck: the copies persist during the build, before PlatformFinish,
   so every replaced slot answers Unknown through missing evidence.
   Replacement: the copies stay path-less in memory at PlatformFinish.
   NDMF runs the compressor phase and the PlatformFinish phase inside
   one pipeline call. NDMF persists the copies only after the last
   phase. A fresh Lab build on 2026-10-04 analyzed the copies as live
   objects through the generated route. Only two of seventeen slots
   refused the texture read. The slot that moved 12,136 triangles
   without the compressor captured its shipped copy without refusal and
   proved zero triangles against it.
2. Struck: the refusing gate for a persisted copy is the streaming
   route's readable-clone step, which needs a `TextureImporter`.
   Replacement: the refusing gate is the identity gate. NDMF persists
   each copy as a sub-asset of a container asset. The identity gate
   refuses sub-assets of a container when the producer name is not in
   the admitted set. The capture never reaches the streaming route. The
   readable-clone importer step is a real refusal for a bare main-asset
   texture with the forced flag. The producer and NDMF never write that
   shape.
3. Struck: on the measured shape, LAC presence can only reduce provable
   triangles, and the reported direction cannot be produced by this
   shape and this code. Replacement: the reduction was observed on the
   probe avatar on 2026-10-04, 12,136 moved without the compressor and
   zero with it. But the mechanism is honest classification of the
   shipped copy, not wholesale refusal. Because fresh-build copies
   analyze through the generated route, a positive delta on another
   avatar can come from honest analysis of its shipped copies. The
   earlier impossibility claim does not hold.

Confirmed on 2026-10-04: a persisted copy refuses re-capture with
`UnavailableCapture` after the build, at the identity gate. The mid-band
erasure fix is a no-op under the inert policy. The version seam in the
Census Lab reads 0.9.0 and admits it.

## Question, restated

The first round fixed a real policy-contract defect in the generated
capture route. After the fix, the reporter still sees more triangles moved
to the canonical opaque material with the LAC Texture Compressor present
than without it. This round asks what actually produces that delta.

## Method

Two controlled builds of the same probe avatar in the Census Lab editor
instance, in one session on 2026-10-03: the avatar's own prefab processed
through the SDK preprocess chain as-is, and the same with one LAC
`TextureCompressor` component added at its defaults. Per-slot
dispositions, per-level alpha-chain composition, and the pipeline console
were captured for both arms. The persisted build products on disk were
then inspected read-only. The component's Alpha Separator values were at
their defaults: mip cap 4, minimum texture size 128, alpha policy inert.

## Measured facts

1. LAC absent: the pipeline merged the avatar to a small number of
   renderers, and AMUSE moved a few thousand triangles to its canonical
   opaque submesh, with zero soundness violations. The moved material's
   main texture holds no texel below roughly 250 alpha, and about one
   tenth to one fifth of each consulted level reads sub-one, which is the
   near-one population.
2. LAC present at defaults: the installed package is 0.9.0. Its pass
   cloned materials, baked some lilToon slots, cleared unused slots,
   resized by a complexity divisor rather than a fixed cap, and chose
   DXT1, DXT5, or BC7 per texture. AMUSE moved zero triangles in this
   arm. Corrected 2026-10-04: the sentence "persisted every replacement
   as a project asset during the build" was wrong. The pass itself never
   writes an asset. NDMF persists the copies after the last phase, so
   the persistence lands after PlatformFinish. The zero-moved outcome
   reproduced on 2026-10-04. Its cause is honest classification of the
   shipped copies, not in-build persistence.
3. A persisted copy refuses the alpha capture with `UnavailableCapture`.
   The copy is a project asset in an admitted format with clean
   residency. Corrected 2026-10-04: the refusing gate is the identity
   gate, not the streaming route. NDMF persists each copy as a sub-asset
   of a container asset. The container is the file's main object. The
   identity gate refuses a sub-asset of a container when the producer
   name is outside the admitted set, and a `_compressed` name is outside
   it. The capture returns `UnavailableCapture` before any route runs.
   The streaming route's readable-clone step does refuse a bare
   main-asset texture with the forced flag, because that file carries
   the base importer. That shape is real but no producer writes it.
   Also corrected: the fresh-build claim. During a fresh build the
   copies are path-less in memory. The capture admitted them and read
   them through the generated route. Two slots still refused the texture
   read in the Lab rerun on 2026-10-04. Those two slots never moved in
   the arm without the compressor either. The slot that moves without
   the compressor captured its copy without refusal.
4. The Alpha Separator policy is inert by default, and the generated
   route's flag handling before the first-round fix was byte-identical to
   the fixed version under inert bounds. The first-round fix therefore
   could not change the reported comparison, whichever runs produced it.
   Confirmed 2026-10-04 by source re-derivation: the inert bounds equal
   the predicate shader's default uniforms, the cutoff arm is unchanged,
   and the verbatim store matches the old derivation byte for byte on
   conformant buffers.
5. The 2026-10-03 admission design targets path-less in-memory copies
   with a `_compressed` name and a registered source pair. Corrected
   2026-10-04: the admission does fire for fresh builds. The Lab seam
   read version 0.9.0 and admitted it. A registered path-less copy of
   the real shape was identified, resolved, and captured through the
   generated route. The admission fails only for two other shapes: a
   persisted copy after the build, because it now has an asset path, and
   a baked lilToon output, because its name lacks the `_compressed`
   suffix.

## What follows

Corrected 2026-10-04. The 2026-10-03 deduction was struck: replaced
slots do not refuse wholesale, and the copies are not persisted during
the build. The replacement statement: in a fresh build the copies are
path-less in memory at PlatformFinish. The capture admits them and
reads them through the generated route. What each replaced slot proves
then depends on the shipped copy's own pixels. On the probe avatar on
2026-10-04 the compressor reduced the moved count from 12,136 to zero.
The cutout slot that moved without the compressor captured its shipped
BC7 copy without refusal. Classification proved zero triangles against
that copy. That is the contract working: AMUSE proves the shipped
pixels, not the authoring intent. On another avatar the same honest
path can raise the moved count, for two content reasons that stay real
and faithful: a DXT1 copy has no alpha channel at all, so the shipped
texture genuinely renders opaque and a whole cutout material can prove
opaque. A second-generation downscale-plus-block-compression copy can
also flatten near-one alpha to exactly one. The earlier claim that the
reported direction cannot be produced by this shape does not hold. Both
directions are honest analysis of the shipped copies.

## Candidate explanations for the reported delta

The reporter later re-ran both arms on the avatar in question and reported
observed totals: about forty-one thousand triangles moved with the
compressor present, about forty thousand six hundred without. The delta is
real and positive. Measured facts now constrain it:

1. The compressor's pass replaced textures, cleared unused texture slots,
   baked some lilToon slots into main textures, and re-encoded formats.
   Each of those changes what the alpha equation sees at
   PlatformFinish.
2. The fur material's main texture was measured in its source form: it is
   already an alpha-free block-compressed format and its captured alpha
   field is fully opaque at every level. So the delta on that slot family
   is not alpha stripping of the main texture. Its LAC-off refusal must
   come from another fact in the slot's equation, most plausibly a
   secondary texture or material feature the compressor's slot clearing
   or baking removed, which the analysis had answered Unknown for. The
   per-slot refusal reasons of both runs would settle this from the
   report; the console summary lines do not carry them.
3. Corrected 2026-10-04: not every branch runs through the generated
   route. Fresh-build copies do, and their moves follow the shipped
   pixels. Persisted copies after the build refuse at the identity gate.
   Baked lilToon outputs refuse the admission because their name lacks
   the `_compressed` suffix. Where the route does run, the moved
   triangles render opaque in the shipped avatar, the visible change is
   the compressor's doing, and the move follows the shipped pixels. That
   is the declared contract, not a soundness defect.
4. Separately measured and real, but not part of this delta: a persisted
   copy refuses re-capture after the build. Corrected 2026-10-04: the
   refusing gate is the identity gate. A later analysis of a baked
   avatar answers Unknown on every replaced slot whose copy carries the
   `_compressed` suffix, and on every slot whose main texture is a bare
   baked output.

## Branch disposition

Corrected 2026-10-04. The 2026-10-03 disposition stays in force for the
merged branch: the mid-band erasure fix is real, tested, and orthogonal.
The persisted copy refusal is corrected in scope. It is a coverage
defect at the identity gate, not in the streaming route. NDMF-persisted
copies inside container assets can never be re-analyzed, because the
container basis admits only the Avatar Optimizer names and the VRCFury
container. The same gap covers bare baked lilToon outputs, whose names
carry no admitted suffix. Each fix widens an admission basis, so each is
a correctness-contract change. That needs a design decision before any
production work, following the 2026-10-02 record's option framing. One
premise of the attestation spec also needs correction: its section 12
says a container-persisted copy would be admitted unchanged by the
existing container basis. On 2026-10-04 that was measured false. The
container basis checks producer names, and `_compressed` names are
outside the admitted set.

## Open items

- Resolved 2026-10-04: the failing step for a persisted copy is the
  identity gate's sub-asset producer check. It was traced with live
  probes in both editor instances, not by type inspection alone.
- Open 2026-10-04: two slots in the compressor arm refused the texture
  read with the `UnavailableCapture` family in both reruns. The refusing
  copies are DXT5, 64 by 64, seven mips, and below the component's
  minimum texture size. An isolated copy of the same shape captured
  without refusal. The in-build cause is unidentified.
- Open 2026-10-04: the reporter's two compared runs are unidentified.
  One fact from the reporter selects between the candidate explanations.
  The 2026-10-04 rerun on the probe avatar produced a negative delta,
  12,136 to zero, so it cannot adjudicate the reporter's positive delta.

## Re-verification evidence, 2026-10-04

Method: source re-derivation from the LAC 0.9.0 package in the Census
Lab project and the NDMF 1.14.4 package in this repository. Live gate
probes in the dev editor instance and the Census Lab editor instance,
each after the project identity check passed. A fresh two-arm build
rerun in the Census Lab editor instance on the same probe avatar, one
arm without the compressor and one arm with one component at defaults.
Observed facts:

- NDMF runs the compressor's Optimizing phase and AMUSE's
  PlatformFinish phase inside one pipeline call. NDMF persists in-memory
  textures only after the last phase, in `Finish`. The earlier
  persistence point covers only the phases before Optimizing. So the
  copies are path-less at PlatformFinish.
- Dev instance, persisted container shape: a DXT5 texture with the
  forced streaming flag, saved as a sub-asset of a container asset. The
  reloaded texture answered sub-asset true, main object type the
  container, streaming true, seven mips. The identity gate answered
  false. The route attestation answered false. The capture answered
  false with `UnavailableCapture`.
- Dev instance, bare main-asset shape: the same texture saved as its
  own asset file. The file's importer is the base importer. The identity
  gate answered true. The capture answered false. This is the shape the
  2026-10-03 mechanism described. No producer writes it.
- Lab instance, real persisted copy from an earlier build: format BC7,
  streaming true, sub-asset true, name suffix present. Identity gate
  false. Capture false with `UnavailableCapture`.
- Lab instance, registered path-less copy of the real shape: registry
  resolve true, admission true, identity true, residency true, capture
  true through the generated route. The version seam read 0.9.0 and
  admitted it.
- Lab rerun, arm without the compressor: accepted true, one renderer
  after merging, 18 slots, 190,931 total triangles, one moved slot,
  12,136 moved triangles, zero soundness violations. Thirteen distinct
  main textures, all asset-backed after the build, none compressed.
- Lab rerun, arm with the compressor at defaults: accepted true, one
  renderer, 17 slots, 17 cloned materials, 190,931 total triangles,
  zero moved triangles. Thirteen distinct main textures, all
  asset-backed after the build, nine compressed. The per-slot map held
  one baked DXT1 main texture and the rest compressed or plain copies.
  The report named two texture-read refusals by slot ordinal, two
  proved-nothing slots, and two left-unchanged slots. The report was
  identical across both compressor-arm runs.
- Corpus preflight: each arm restored its own changes. Three scene
  files stayed drifted. Their last write times are 2026-09-30 and
  2026-10-02, before this session, so this session did not cause them.
  They are digest-only entries and cannot be restored by the preflight.
