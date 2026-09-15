# lilToon layer alpha support design

Date: 2026-09-15. Status: design for review. Implementation has not
started.

Parent context: the animated material swap work merged on 2026-09-15.
Confirmed coverage gaps: the investigation record of 2026-09-14 and
2026-09-15, plus the code-fact review of this date.

Privacy note. This document names no avatar, renderer, material, or
texture asset. Renderers are named by role. Exact triangle counts are
written as ranges.

## Problem, measured 2026-09-15

1. One characterized skirt renderer refuses with
   `AdmittedMaterialSemanticsUnknown`. The lilToon transparent frontend
   reports `UnsupportedFeature` on `_UseMain2ndTex`. The material
   enables the second main texture layer. Both alpha frontends gate the
   two layer toggles as all-or-nothing coverage gates today.
   [DOC: investigation of 2026-09-14 and code review of this date]
2. A texture without an alpha channel, for example a compressed DXT1
   import, refuses at capture with `UnsupportedFormat`. The capture
   allowlist admits RGBA32, ARGB32, Alpha8, RGB24, DXT5, and BC7. DXT1
   is absent. RGB24 has no alpha channel and proves constant-one
   through the GPU predicate today, so the DXT1 gap is an allowlist
   omission, not a missing mechanism. Test fixtures already work around
   it by forcing uncompressed imports.
   [MEASURED 2026-09-15: `UnityAlphaFieldEvidence.IsAdmittedFormat`,
   the capture route order, and the fixture comments]
3. Emission blocks nothing in lilToon today. The transparent and cutout
   frontends contain no emission gate. The opaque frontend computes an
   emission output with no production consumer. The canonical opaque
   recipe overrides no emission property, so emission content carries
   into the target implicitly through the material copy. The editor
   logs contain zero emission refusal lines.
   [MEASURED 2026-09-15: frontend code review and log search]

## Premises agreed with the product owner

1. Branch shape is stacked vertical branches. This branch carries layer
   alpha support. A later branch carries emission carryover
   verification. A defect branch comes first, see ordering below.
2. Scope is lilToon only. Poiyomi keeps its current behavior and gets
   its own coverage decision after this branch lands.
3. A texture whose format has no alpha channel samples alpha exactly
   one. That fact is a format theorem. It is exact, needs no GPU
   readback, and needs no new readback machinery.
4. The material as a whole is still classified honestly. The theorem
   replaces only the texture sample term. The tint alpha and the alpha
   mask term still compose with it. A no-alpha texture under an active
   alpha mask stays transparent wherever the mask says so.
5. A material whose texture has no alpha channel is probably
   misconfigured. AMUSE classifies it honestly and reports the suspected
   mistake at information severity. It does not refuse and does not
   stay silent.
6. Stage A is conservative. Stage B widens. A checkpoint separates
   them.

## Ordering and the prerequisite defect

The 2026-09-14 investigation records two characterized top garment
renderers that analyze with no refusal yet classify zero of several
thousand triangles opaque each. The prime suspect is the alpha mip
chain of a streaming texture never reaching the field set. The suspicion
is recorded as unconfirmed.

A separate branch diagnoses and fixes this defect first. It is a
capture-versus-classification correctness issue. It is orthogonal to
layer semantics. It gates the credibility of every Lab number this
effort produces, because the same defect can mask real classification
wins as zero.

This branch returns to that record before its stage A checkpoint. The
rebase order is: defect branch merges to main, then this branch
rebases.

## Stage 0: pin the layer equation

The vendor lilToon source is not committed to this repository. The
attestation digests the installed package at runtime. The 2026-08-30
cutout investigation and the 2026-09-01 transparent investigation pin
part of the layer surface:

- `lilGetMain2nd` and `lilGetMain3rd` in `lil_common_frag.hlsl`, with
  the layer gates and the transparent write sites.
- `_Main2ndTexAlphaMode` and `_Main3rdTexAlphaMode`. Value zero writes
  no alpha. Values one to four write alpha by replace, multiply, add,
  and subtract. [DOC: the two investigation records]

The per-layer UV mode property names and values, the per-layer
scroll and rotate properties, the per-layer angle properties, and the
per-layer alpha mask properties and modes are pinned nowhere in this
repository.

Stage 0 is a dated, sanitized investigation record produced against the
installed vendor include tree in the dev editor instance. The dev
project has lilToon installed. The record pins every property name,
every legal value, and every write site the layer alpha term needs,
with the same discipline as the 2026-08-30 record. No vendor source is
committed. Every borrowed fact carries a tag.

Stage A implementation starts only after this record lands.

## Stage A: layer alpha term and format theorem

### Layer alpha term

1. A shared layer alpha term, one type, consumed by both the
   transparent and the cutout frontend. The shape follows the existing
   shared alpha mask term.
2. `_UseMain2ndTex` and `_UseMain3rdTex` leave the coverage gate lists
   of both frontends. Every other existing gate stays.
3. The evidence requests of both frontends extend with the layer facts:
   the layer texture with scale and offset, source identity, sampling,
   and alpha channel evidence, plus the layer alpha mode scalar and the
   layer alpha mask facts that stage 0 pins.
4. The admitted shape in stage A is: layer sampled at UV zero with an
   exact identity scroll and rotate vector, and the identity scale and
   offset the main texture arm already requires. Everything else about
   a layer refuses with a named diagnostic: the UV mode property, the
   scroll property, the mask property, whatever stage 0 pins.
5. All four alpha mode writers are modeled exactly in stage A. Replace,
   multiply, add, and subtract are one closed-form write site each in
   the vendor source. The proof core composes them with its existing
   exact scalar algebra. Add and subtract become exact interval bounds
   through the saturate. Unknown in any layer absorbs, per the absorbing
   rule.
6. A layer with its alpha mode at zero contributes no alpha write. The
   layer toggles stay in the evidence request so the term can observe
   the mode values. A toggle that is off with an unknown mode stays
   admitted, because the pinned write sites are strictly inside the
   toggle gate. [DOC: vendor write sites. INFERRED: the off-state
   reduction, to be re-verified in stage 0]

### No-alpha format theorem

1. The capture allowlist admits formats whose definition has no alpha
   channel, DXT1 first among them. Unity names DXT1
   `RGBA_DXT1_UNorm` and `RGBA_DXT1_SRGB` with implicit alpha one.
   [DOC: Unity format table. The opaque color allowlist already admits
   these names]
2. The importer-keyed constant-alpha theorem already exists and also
   covers the streaming route. The design prefers it as the primary
   proof, with the extended allowlist keeping the blit route consistent.
   The implementation decides the split by the cheapest exact route per
   capture path.
3. The proof replaces only the texture sample term. The tint alpha and
   the mask term compose exactly as today. This is the honesty
   requirement from the product owner, stated above.

### Suspected mistake report

1. When a sampled texture of an admitted transparent or cutout material
   has no alpha channel, AMUSE reports one information line through the
   error report. The line names the renderer, the slot, the material,
   and the texture property. The wording lives with the other report
   strings.
2. The report never changes a classification. It is a diagnosis aid,
   not a refusal.

### Frontend scope

Both frontends consume the shared term in stage A. The cutout family
keeps its cutoff behavior after the composition. One shared term with
two consumers matches the two-consumer rule for shared seams.

## Checkpoint between stages

Stage A ends with: product suite green, research suite green, new
falsifier fixtures green, the Lab re-characterization run by the
product owner, and the investigation record updated with a dated
subsection. Stage B starts only after the product owner observes the
checkpoint result.

## Stage B: layer UV modes and scroll

Stage B admits layer UV modes one to three and non-identity scroll and
rotate for the layers, on the exact UV geometry the main texture arm
already proves with. The property names and value encodings come from
the stage 0 record. Each admitted UV mode gains its own falsifier
fixtures. The stage B design addendum records the equation deltas
before code.

## Emission follow-up, recorded for the later branch

Emission blocks nothing today, so there is no refusal to fix. The
agreed follow-up is a carryover verification gate: the conversion proves
the generated opaque target retains the source emission content by
property identity, and refuses only if the carryover breaks. This
closes the silent-drop hole without refusing any material that splits
today. The gate design is a separate dated document when that branch
starts.

## Refusal and report vocabulary

New named diagnostics join the closed per-scope vocabularies:

- Layer UV mode unsupported, named per layer property.
- Layer scroll unsupported, named per layer property.
- Layer alpha mask unsupported, named per layer property.
- Layer alpha mode unknown, when the mode value is not finite.
- One new information report for the no-alpha texture mistake.

No refusal widens silently. Every new refusal names the property the
proof cannot carry.

## Test strategy

Falsifier fixtures follow the repository convention. Each falsifier is
an adversarial case a plausible wrong implementation fails.

1. --- Falsifier 1: a plausible implementation that treats a layer
   toggle as before, refusing the whole material, fails the fixture
   where the layer is admitted and the material splits.
2. --- Falsifier 2: per alpha mode, a plausible implementation that
   composes the mode wrongly fails that mode fixture with an independent
   outcome oracle.
3. --- Falsifier 3: a plausible implementation that lets a no-alpha
   texture prove the whole material constant-opaque fails the fixture
   where an active alpha mask keeps triangles transparent.
4. --- Falsifier 4: a plausible implementation that reports the mistake
   or refuses on it fails the fixture asserting the information line
   and the split at the same time.
5. --- Falsifier 5: a plausible implementation that models the cutout
   family differently from the transparent family fails the shared-term
   parity fixtures.

Fixtures use the schema-only stand-in shaders under the test hidden
namespace, deterministic synthetic textures, and independent oracles.
Fixture textures that need an alpha channel force an uncompressed
import, as the existing fixtures already do.

## Risks

1. Stage 0 may pin layer alpha writers beyond the four modes, for
   example dissolve masks or noise. Those become named refusals in
   stage A, not silent admissions.
2. The off-state reduction of a layer needs re-verification in stage 0.
   The design assumes it. A contrary pin changes the term, not the
   architecture.
3. The streaming capture route must reach the theorem through the same
   admission path, or streaming no-alpha textures keep refusing. The
   implementation covers both routes and the fixtures assert both.
4. The defect branch may reclassify more triangles on the characterized
   content. The checkpoint Lab run separates the two effects by design:
   the defect branch lands first, so the layer wins read clean.
