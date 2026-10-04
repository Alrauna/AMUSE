# LAC persisted copies refuse the capture; round two of the more-moved question

Privacy note: This record uses only this repository's own sources, the
public LAC package state in the dev and Census Lab projects, and in-memory
characterization runs. It names no avatar, renderer, material, or texture
beyond role descriptions. It writes no instance names, hashes, ports, or
machine paths. Where exact triangle counts appear they are observed counts
from the Lab characterization runs, kept to the minimum that makes the
argument checkable.

Date: 2026-10-03.
Branch: `fix/generated-route-alpha-policy-erasure`.
Investigation status on 2026-10-03: the first round's mid-band erasure
defect is real but orthogonal to the reported symptom. This round measured
the actual LAC producer shape in the Census Lab and found a different,
coverage-direction defect, plus an unexplained gap against the reported
symptom. The gap needs one fact only the reporter holds: the exact two
runs the comparison came from.

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
   resized by a complexity divisor rather than a fixed cap, chose DXT1,
   DXT5, or BC7 per texture, and persisted every replacement as a project
   asset during the build. AMUSE moved zero triangles in this arm.
3. A persisted copy refuses the alpha capture with `UnavailableCapture`.
   The copy is a project asset in an admitted format with clean residency.
   LAC's forced streaming flag routes an asset-backed texture to the
   streaming route. That route first looks for a readable source image,
   which a native `.asset` copy does not have, and then falls back to the
   readable-clone step, which needs a `TextureImporter` to force readable.
   A persisted copy carries only the base importer, so the step cannot
   run, and the route refuses. Every triangle over every replaced slot
   answers Unknown through the missing-evidence path.
4. The Alpha Separator policy is inert by default, and the generated
   route's flag handling before the first-round fix was byte-identical to
   the fixed version under inert bounds. The first-round fix therefore
   could not change the reported comparison, whichever runs produced it.
5. The 2026-10-03 admission design targets path-less in-memory copies
   with a `_compressed` name and a registered source pair. The installed
   0.9.0 produces asset-backed persisted copies instead, so the
   replacement admission never fires for them. The admission is not
   wrong; it is not exercised by this producer shape.

## What follows

On the measured shape, LAC presence can only reduce provable triangles:
replaced slots refuse wholesale, untouched slots behave as before. The
reported direction, more triangles moved with LAC present, cannot be
produced by this shape and this code. It is reproducible only if the
reporter's compared runs analyzed path-less copies through the generated
route, where two content effects are real and faithful: a DXT1 copy has
no alpha channel at all, so the shipped texture genuinely renders opaque
and a whole cutout material proves opaque; and a second-generation
downscale-plus-block-compression copy genuinely flattens near-one alpha
to exactly one, which restores provability over domains that were
refused against the original. Both are the contract working: AMUSE proves
the shipped pixels, not the authoring intent.

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
3. In every branch of the mechanism, AMUSE analyzes the shipped copy
   through the generated route and the moved triangles render opaque in
   the shipped avatar. The visible change is the compressor's doing; the
   move follows the shipped pixels. That is the declared contract, not a
   soundness defect.
4. Separately measured and real, but not part of this delta: a persisted
   copy refuses re-capture after the build, so any later analysis of a
   baked avatar answers Unknown on every replaced slot.

## Branch disposition

The branch carries two things: the mid-band erasure fix, which is real,
tested, and orthogonal, and the dated records. Nothing measured today
invalidates it. Recommendation: keep the branch, and treat the persisted
copy refusal as a separate, well-scoped increment on a fresh branch,
because it is a coverage defect in the streaming route for importer-less
asset-backed replacement copies: forced-flag copies created by a build
tool can never be analyzed, so LAC-processed avatars lose all alpha
optimization on replaced slots. The fix direction is a route decision at
the capture gates, not a policy change.

## Open items

- The exact failing step inside the streaming route is pinned to the
  readable-clone import requirement by type inspection, not by a
  step-level trace.
- The reporter's two compared runs are unidentified; one fact from the
  reporter selects between the candidate explanations.
