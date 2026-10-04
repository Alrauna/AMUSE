# The generated capture route erases the mid alpha band under an active policy

Privacy note: This record uses only this repository's own sources and tests.
It names no avatar, renderer, material, texture, animation clip, or asset
path. It writes no instance names, hashes, ports, or machine paths. It holds
no per-renderer or per-slot table.

Date: 2026-10-03.
Branch: `fix/generated-route-alpha-policy-erasure`.
Base: `main` at `b02bbc8`.
Investigation status on 2026-10-03: static analysis complete. The EditMode
runs named in the test plan ran on 2026-10-03 in the dev editor instance,
after the project identity check passed. Observed counts stand in the
evidence section.

## Question

An avatar that carries the LAC Texture Compressor component moves more
triangles to the canonical opaque material than the same avatar without the
component. The expectation was the opposite. A smaller texture consults
smaller copies, so the analysis should refuse more, not less. The first
suspect was the mipmap handling not following the Alpha Separator settings.

## Method

On 2026-10-03 this session read the capture routes, the exact fold, the
policy algebra, and the settings plumbing on this branch. It traced one
triangle's evidence from the Alpha Separator component through the capture
route selection to the per-level fold. The EditMode runs used the dev
editor instance. No private avatar data was read.

## Findings

### 1. The mipmap settings are followed on every route. Rejected as the cause.

`AmusePlatformFinishPass` reads `PreserveTransparencyMaxMipLevel` and the
minimum texture size from the component and hands both to
`GatherAlphaFields`. That method applies the same mip cap and the same size
scope to every captured texture, whatever route captured it. A replacement
copy carries the `unity-replacement` identity form and is scoped exactly
like an asset texture. The suspicion about the mip cap does not hold.

### 2. The generated route ignores the alpha policy at blit time.

The route that LAC replacement copies take is
`UnityGeneratedTextureEvidence.TryCapture`. It builds one blit material and
sets only `_Mip` per level. It never writes `_OpaqueBound` or `_NoiseBound`.
The GPU route sets both uniforms on its own material
(`UnityAlphaFieldEvidence.TryAcquireLevel`). The predicate shader's property
defaults are `_OpaqueBound = 1.0` and `_NoiseBound = 0.0`, which are the
inert bounds. So the generated route always blits at the exact-255 contract,
whatever the Alpha Separator tab says. A lowered opaque bound is ignored.
This defect is conservative on its own.

### 3. The readback turns the whole mid band into erasable noise.

The generated route re-derives the stored flags from the readback instead of
storing the shader's verdict bytes:

    var isErased = threshold >= 1.0f &&
        bounds.NoiseBound > 0 &&
        data[i].r < bounds.NoiseBound;

The shader writes a three-state verdict in red: byte 255 opaque, byte 1
noise, byte 0 for everything between the bounds. The comparison asks whether
the verdict byte sits below the policy's noise byte. Byte 1 sits below it,
which is correct. Byte 0 also sits below it, which is not. With an active
noise policy, every texel between the noise bound and the opaque bound
becomes `ErasedFlag`.

The defect compounds with finding 2. The shader never emitted a noise
verdict, because its uniform stayed at the inert noise bound 0. So byte 0
covers every texel below exact one, and the readback erases every one of
them.

### 4. Erased texels cannot refute opacity.

`TriangleAlphaClassifier` treats an erased texel as a non-witness when the
density policy substitutes it, and counts it as noise in the density gates.
A triangle whose domain holds genuine partial transparency therefore proves
opaque, as long as the erased share stays under the coverage slider. The
same triangle over the same content refuses on the GPU route, because there
the mid band stays a witness at byte 0.

### 5. The route split explains the observed direction.

With the compressor off, the avatar's asset textures capture through the
direct GPU route or the streaming route. Both apply the policy correctly.
With the compressor on, the replacement copies capture only through the
generated route, because the route order puts it before the streaming clone
route. The flawed route is the only route the new copies can take. The
result is more proven triangles with the compressor on. The size reduction
is present but incidental. LAC copies admitted on 2026-10-03 made the
defect visible at scale. The exposure is older: every admitted generated
texture, meaning Avatar Optimizer atlas outputs and VRCFury container
textures, captured through the same route.

### 6. Secondary observation, not the cause.

A smaller texture carries a shorter declared chain, so the consulted prefix
is shorter and the fold sees fewer refutation opportunities. This
consultation-count asymmetry is real but policy-coherent. The mip cap and
the size scope define the consulted prefix in levels, and the policy accepts
the levels outside it. It cannot explain a large direction change on its
own.

## Root cause

`UnityGeneratedTextureEvidence.TryCapture` breaks the policy contract in two
places. It blits with the shader's inert default bounds instead of the
active policy, and its readback maps the mid-band verdict byte 0 to
`ErasedFlag` whenever a noise policy is active. The classifier then reads
genuine partial transparency as substitutable noise, and triangles over LAC
copies prove opaque where the sources refuse.

## Fix plan

One change site, `UnityGeneratedTextureEvidence.TryCapture`.

1. Compute the effective bounds the way the GPU route does: the policy when
   no shader cutoff applies, `AlphaPolicyBounds.Inert` under a shader
   cutoff. Write them to the material as normalized floats.
2. In the exact arm, store the shader's verdict byte verbatim, exactly as
   `TryAcquireLevel` stores its bytes. The shader already emits the three
   states under the active bounds, so no readback arithmetic remains.
3. Keep the cutoff arm's binarization of the green channel. A shader cutoff
   source stays policy-inert on this route, exactly as on the other three.
4. Validate the stored flags with the same predicate-buffer check the GPU
   route uses, so a broken blit refuses instead of storing foreign bytes.

The existing test that pins erasure under `AlphaPolicyBounds.From(100, 2)`
stays green. With the bounds applied, the shader itself emits byte 1 for a
sub-noise texel, and the verbatim store keeps it. The routes become
byte-identical in flag derivation.

## Test plan

Both tests live in `UnityGeneratedTextureEvidenceTests` and use the
container sub-asset seam the file already uses.

1. `AnActiveNoisePolicyKeepsMidBandTexelsWitnessesOnTheGeneratedRoute`:
   alpha byte 128 under `AlphaPolicyBounds.From(100, 10)`, noise byte 26.
   The captured texel must stay a witness at byte 0.
2. `AnActiveOpaqueBoundLowersTheOpaqueBarOnTheGeneratedRoute`: alpha byte
   240 under `AlphaPolicyBounds.From(90, 10)`, opaque byte 230. The
   captured texel must read opaque at byte 255.

Both fail on the current code and pass after the fix. That is the RED and
GREEN pair.

## Evidence

This section records observed runs only. EditMode, dev editor instance,
`Alrauna.Amuse.Tests.Editor`.

- 2026-10-03, before the fix: the full EditMode suite ran with the two new
  tests in place and the production change absent. Observed 2539 tests
  completed and exactly 2 failed. Both failures were the new tests, and
  each stored the byte 1 where the policy demands 0 or 255. No other test
  failed in the run.
- 2026-10-03, after the fix: the same full EditMode suite ran on the fixed
  code. Observed 2537 passed, 0 failed, 0 skipped, in about 182 seconds.
  The two opt-in integration guards report inconclusive by design. Their
  environment gate keeps them inert outside a disposable integration
  project, and the summary counts them with the passing set.

