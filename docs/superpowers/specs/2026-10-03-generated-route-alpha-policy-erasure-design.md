# Generated-Route Alpha Policy Erasure Fix Design

Date: 2026-10-03.
Status: executed on 2026-10-03 on branch
`fix/generated-route-alpha-policy-erasure`. The RED and GREEN runs stand in
the investigation record `docs/superpowers/investigations/2026-10-03-generated-route-alpha-policy-erasure.md`
and in the plan. This spec records the design those runs implemented.
Scope: the generated capture route's policy handling, the two behavior
tests that pin it, and nothing else.

## 1. Problem

An avatar that carries the LAC Texture Compressor component moves more
triangles to the canonical opaque material than the same avatar without the
component. The expectation was the opposite. The cause is not the mipmap
handling. The Alpha Separator mip cap and the minimum texture size are
applied uniformly on every capture route.

The generated capture route broke the policy contract in two places:

1. It never wrote the active policy bounds to its blit material. The
   predicate shader's property defaults are `_OpaqueBound = 1.0` and
   `_NoiseBound = 0.0`, which are the inert bounds. The route always blitted
   at the exact-255 contract, whatever the tab said. A lowered opaque bound
   was ignored. On its own this defect is conservative.
2. Its readback re-derived the stored flags from the verdict byte with the
   test `data[i].r < bounds.NoiseBound`. The shader writes a three-state
   verdict in red: byte 255 opaque, byte 1 noise, byte 0 for everything
   between the bounds. With an active noise policy, byte 1 and byte 0 both
   sit below the noise byte. The whole mid band became `ErasedFlag`.

The two defects compounded. Because the shader blitted at the inert noise
bound 0, it never emitted a noise verdict, so byte 0 covered every texel
below exact one, and the readback erased every one of them. The classifier
treats an erased texel as a non-witness when the density policy substitutes
it, so triangles over genuine partial transparency proved opaque.

The route order makes the defect visible exactly when the compressor runs.
The generated route precedes the streaming clone route, so a LAC replacement
copy takes the generated route whatever its flags say. The avatar's asset
textures capture through the direct GPU route or the streaming route, and
both apply the policy correctly. So the same triangles refuse without the
component and prove opaque with it.

The exposure is older than the LAC admission. Every admitted generated
texture captured through the same route: Avatar Optimizer atlas outputs and
VRCFury container textures.

## 2. Measured or read evidence

All facts below were read from source on 2026-10-03 on this branch, or were
observed in the runs the investigation record cites.

- The GPU route writes both uniforms on its blit material
  (`UnityAlphaFieldEvidence.TryAcquireLevel`). The generated route set only
  `_Mip`.
- The predicate shaders emit `float4(verdict, value, 0, 1)`. The verdict is
  1.0, 1/255.0, or 0.0. An R8-family target stores these as bytes 255, 1,
  and 0.
- The GPU route stores its readback bytes verbatim and validates them with
  `IsPredicateFlagBuffer`, which admits only 255, 1, and 0.
- The classifier substitutes erased texels only under an active density
  policy, and the substitution needs the erased share strictly under the
  coverage percent. Mid-band texels are witnesses at byte 0 on every
  correct route.
- The Alpha Separator mip cap and minimum texture size flow through
  `GatherAlphaFields` and apply to every captured texture, whatever its
  identity form. The mip settings were not the cause.
- Observed on 2026-10-03, before the fix: full EditMode run, 2539 tests
  completed, exactly 2 failed, both new tests, each stored byte 1 where the
  policy demands 0 or 255. No other test failed.
- Observed on 2026-10-03, after the fix: the same full EditMode run gave
  2537 passed, 0 failed, 0 skipped, in about 182 seconds. The two opt-in
  integration guards report inconclusive by design.

## 3. Scope and non-goals

In scope:

- One change site: `UnityGeneratedTextureEvidence.TryCapture`.
- Two behavior tests in `UnityGeneratedTextureEvidenceTests`.
- Code comments at the change site that state why the verdict stores
  verbatim.

Non-goals:

- No new refusal kinds. A failed validation refuses
  `UnavailableCapture` through the existing path.
- No change to the mip cap, the minimum texture size, the policy algebra,
  the classifier, the fold, or the two correct routes.
- No change to the cutoff arm's semantics. A shader cutoff source stays
  policy-inert on this route, exactly as on the other three.
- No report string changes. The refusal vocabulary already covers the new
  validation failure.
- No change to the LAC admission, the identity forms, or the version seam.

## 4. Design questions

1. Where do the effective bounds come from?
2. What does the exact arm store?
3. What guards a broken blit?
4. Why does the cutoff arm stay as it is?

## 5. Constraints

- Fail closed. A readback that does not carry the three verdict bytes
  refuses the texture.
- Route parity. The stored bytes for one texel must not depend on which
  route captured it.
- The policy decision lives in the shader, not in readback arithmetic.
- Production types stay `internal`. Comments state why, not what.
- Simplified Technical English for every human-read sentence.
- No absolute paths, no host names, no ports, no private identifiers.

## 6. The fix contract

1. Effective bounds. The route computes its blit bounds the same way the
   GPU route does: the caller's policy when no shader cutoff applies,
   `AlphaPolicyBounds.Inert` under a shader cutoff. It writes them to the
   material as normalized floats, a bound byte over 255.
2. Verbatim verdict store. In the exact arm, the route stores the shader's
   red byte as the grid byte, with no arithmetic. The shader already emits
   the three states under the active bounds.
3. Buffer validation. After the store, the route validates the level's
   bytes with the same predicate-buffer check the GPU route uses. A
   filtered, rescaled, or transfer-converted readback refuses.
4. Cutoff arm. Under a shader cutoff the route binarizes the raw green
   value by the threshold, with no erasure and no bounds. This preserves
   the published inert-under-cutoff contract.

## 7. Route parity after the fix

For one texel of one texture, the stored byte now agrees across routes:

- Alpha at or above the policy's opaque byte stores 255 everywhere.
- Alpha strictly below the policy's noise byte stores 1 everywhere. The
  shader emits this verdict on every blit route, and the source routes
  compute it from the raw byte.
- Alpha between the bounds stores 0 everywhere. It is a witness and can
  refute opacity.

For an inert policy the bytes are unchanged from the old generated route:
the old defaults equal the inert bounds, and the verbatim store reproduces
the old byte set. Users who never raised a policy slider see no change.

## 8. Refusal and report surface

No new refusal kinds. `IsPredicateFlagBuffer` failure returns false from
the capture, and the route caller records `UnavailableCapture`, which the
existing hint already explains. The fix makes refusals more faithful, not
more frequent: a texel that read erased before now reads a witness, which
turns a false proof into a refusal of exactly the triangles that depend on
it.

## 9. Testing strategy

Both tests live in `UnityGeneratedTextureEvidenceTests` and use the
container sub-asset seam the file already uses. Each test pins one defect,
and each exact-byte assertion is a no-op guard: a change that does not fix
the defect cannot pass it.

- Falsifier 1: a route that re-derives flags from the verdict byte with a
  noise-bound comparison maps byte 0 into the erased flag.
  `AnActiveNoisePolicyKeepsMidBandTexelsWitnessesOnTheGeneratedRoute`
  fails it. Alpha byte 128 under `AlphaPolicyBounds.From(100, 10)` must
  store 0.
- Falsifier 2: a route that blits with the shader's inert default bounds
  cannot honor a lowered opaque bound.
  `AnActiveOpaqueBoundLowersTheOpaqueBarOnTheGeneratedRoute` fails it.
  Alpha byte 240 under `AlphaPolicyBounds.From(90, 10)` must store 255.

The pre-existing test that pins erasure under `AlphaPolicyBounds.From(100, 2)`
stays green. With the bounds applied, the shader itself emits byte 1 for a
sub-noise texel, and the verbatim store keeps it. That test is the guard
that the fix did not overcorrect into never erasing.

Test mechanics: refresh Unity and wait for compile, then run a focused
EditMode filter through the dev editor instance after the
`Application.dataPath` identity check. A focused run that reports 0 tests
is a failure. Record observed counts.

## 10. Risks and residual limits

- The verbatim store trusts the R8-family target to quantize 1/255.0 to
  byte 1. The GPU route pins the same emission on R8, and the validation
  refuses any foreign byte, so a quantizing platform degrades to a refusal,
  never to a wrong proof.
- Analyses that used the erased mid band now see witnesses. Avatars analyzed
  before the fix may move fewer triangles. That direction restores the
  declared contract; it is the intended effect, not a regression.
- The defect predates LAC, so the fix changes results for Avatar Optimizer
  atlas and VRCFury textures too, in the same faithful direction.

## 11. Effect

After the fix, the generated route applies the Alpha Separator policy
exactly like the direct GPU route and the streaming route. Triangles over
generated textures and LAC replacement copies prove opaque only when the
policy's opaque bound holds and no witness remains in their domain. The
compressor no longer flips triangle dispositions upward.
