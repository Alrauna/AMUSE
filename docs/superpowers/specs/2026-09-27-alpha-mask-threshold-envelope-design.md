# lilToon Alpha-Mask Threshold-Envelope Design

Date: 2026-09-27.
Branch: `feat/alpha-mask-value-support`.
Base: `main` at `7d8a429`.
Investigation: `docs/superpowers/investigations/2026-09-27-alpha-mask-value-refusal-investigation.md`.
Status on 2026-09-27: design awaiting implementation. No production code had changed.

Privacy note: the triggering evidence comes from two private Census Lab slots described by shader family and state. This document repeats none of the private identifiers.

## Problem

The 2026-09-07 alpha-mask composition design admitted exactly three assigned-mask shapes and deferred the rest to a "threshold-envelope contract". On 2026-09-27 the Census Lab console held two slot refusals naming `_AlphaMaskValue`. Both slots hold an assigned mask with the vendor-default scale 1 and an offset strictly between 0 and 1, which is the deferred shape. Today `LilToonAlphaMaskTerm.Interpret` refuses that shape with `UnsupportedFeature`, the slot resolves `AdmittedMaterialSemanticsUnknown`, and AMUSE proves nothing for two materials whose alpha it could classify per triangle.

The masked multiply lowers alpha by a per-texel factor in [value, 1]. Proving a triangle opaque needs the composed alpha to stay at or above the exact-one opacity bar at every sample. That needs a per-texel predicate over the mask field, composed with the main alpha, together with a binary32 rounding argument for `r * scale + value` under both evaluation orders. This design supplies that predicate and lifts the refusal.

## Pinned source equation

Unchanged from the 2026-09-07 design: `lil_common_frag.hlsl:469-473` in the attested vendor sources composes `alphaMask = saturate(alphaMask * _AlphaMaskScale + _AlphaMaskValue)` and applies mode 1 as replace, mode 2 as multiply. The mask sample borrows `_MainTex`'s sampler and rides `uvMain` transformed by the mask's own `_ST`. Modes 3 and 4 stay refused. The canonical digests in `LilToonSourceAttestation` pin the sources that declare these equations, so a vendor change breaks attestation before any proof can go stale.

## The rounding argument

The field contract attests, for every level of a chain, that every effective per-texel value lies in [0, 1], that byte 255 marks exactly the texels whose value is exactly 1, and that the sampled value is 1 exactly when every positive-weight contributing texel is 255. The mapped proof needs one more step: what does `saturate(r * s + v)` do to a sampled red `r` whose exact value the contract bounds but does not name?

The answer is an envelope, computed exactly. Both binary32 evaluation orders are monotone nondecreasing in `r`, because round-to-nearest and saturate are monotone:

- fused: `fl(r * s + v)`, one rounding of the exact rational `r·s + v`;
- unfused: `fl(fl(r * s) + v)`, a rounding of the exact product followed by a rounding of the exact sum.

So the term over any red interval is bounded by the evaluations at that interval's endpoints, in both orders, with every operation carried out on exact BigInteger rationals and rounded once to the binary32 grid at half-even. No ulp margins, no inequalities: each endpoint evaluation is an exact computation of the two values the hardware could produce there, and monotonicity bounds everything between them. For a sampled red the contract gives exactly two interval shapes:

- every contributing texel is 255, so the filtered red is exactly 1: the envelope is the evaluation at `r = 1`;
- some contributing texel is below 255, so the filtered red lies in [0, 1): the envelope is [evaluation at `r = 0`, evaluation at `r = 1`], with the upper end a sound bound for an unattained supremum.

The factor outcome is then the existing three-way lattice on the envelope: the lower binary32 bound equal to exactly 1 proves the factor opaque at this level; the upper bound strictly below 1 proves the factor must remain transparent; anything else is Unknown. Saturate is exact at both clamps, so a real product at or above 1 yields the grid point 1 under either order, and the opaque test is no coarser than today's admitted `(1, v >= 1)` arm.

## Admitted cases after this design

Assigned mask, modes 1 and 2, finite scale and value:

| `scale`, `value` | Mode 1 (Replace) | Mode 2 (Multiply) |
|---|---|---|
| `s == 1`, `v == 0` | plain red sample (existing) | plain red product factor (existing) |
| `s == 1`, `v >= 1` | `Constant(1)` (existing) | main-unchanged (existing) |
| `s == 0` | `Constant(saturate(v))` | `saturate(v) >= 1` main-unchanged; else refusal naming `_AlphaMaskScale` |
| anything else finite | mapped red sample | mapped red product factor |

Unassigned masks keep the constant arm. Non-finite scale or value keeps the existing refusal naming the offending property. Modes 3 and 4 keep their refusal. The fall-through refusal at `LilToonAlphaMaskSemantics.cs:269-275` becomes unreachable and is deleted.

The constant arm for `s == 0` is exact with no rounding question: `0 * s` is exactly zero in binary32, `zero + v` is exactly `v`, so the term is `saturate(v)` with the same operands under both orders.

## Approach decision

Approach A, chosen: an exact affine-saturate map in the value algebra, resolved to a mapped classified resolution whose per-level predicate evaluates the map at the two red bounds. The arithmetic is exact, the shape follows the existing sample and product kinds, and the raw proof paths stay untouched.

Approach B, rejected: remap texels during capture and feed the classifier a virtual field. It destroys the shared `(source, channel)` evidence model, per-materializes fields for one consumer, and still needs the same rounding argument at the byte boundary. Against the repository rule that shader-specific behavior stays shader-specific.

Approach C, rejected: float envelopes widened by an ulp margin. Sound-looking but not exact; the repository's classification standard is exact rational intervals (`TriangleAlphaClassifier`, `ExactUvGeometry`, `AffineUvTransform`), and a margin-based proof could not state its own tightness.

## Design

### The exact map: `AffineAlphaMap` (Semantics)

One small immutable struct beside the value algebra in `Semantics/AffineAlphaMap.cs`. It lives in Semantics because both the algebra that carries it and the Analysis resolver and classifier that consume it already depend on Semantics, and the dependency direction Analysis → Semantics stays acyclic.

- Factory `FromBinary32(float scale, float value)`: converts both floats to exact BigInteger rationals, validates finiteness, and throws on the identity pair `(1, 0)` and on `scale == 0`, because those shapes route to the existing sample and constant arms and the kinds must stay disjoint.
- `Evaluate(float r)` returns the binary32 envelope `(lower, upper)` for `saturate(r * s + v)` at that exact float input: the minimum and maximum over the fused and unfused evaluations, each computed exactly and rounded once to the binary32 grid at half-even, infinities included.
- Equality and a load-bearing doc comment stating the monotonicity argument above.

### The classifier: mapped walks (Analysis)

`TriangleAlphaClassifier` gains `ClassifyMapped(triangle, texture, sampling, envelope, map)` with the same dispatch shape as today: degenerate and missing-UV checks, the anisotropy refusal, and one mapped walk per admitted filter-and-wrap pair. Point and bilinear walks cover their wrap modes; trilinear rides the bilinear per-level verdict exactly as the raw path documents; anisotropic stays Unknown.

Each mapped walk reuses the raw walk's geometry to answer one question: does the triangle's contributing domain contain a texel below byte 255? Erased texels always count as witnesses on the mapped path. The raw path's erasure-substitution policy is a statement about the raw witness verdict; a mapped factor reads the red value those texels would contribute, which erasure leaves unknown in [0, 1]. Treating them as witnesses is the sound reading, and the design deliberately does not widen the substitution policy to the mapped path.

The outcome comes from one shared tail on the map:

- no witness: the domain is all-255, the red is exactly 1, and the envelope is `Evaluate(1)`;
- a witness: the red lies in [0, 1), and the envelope is [`Evaluate(0)`.lower, `Evaluate(1)`.upper];
- lower bound == 1 → `ProvenOpaque`; upper bound < 1 → `MustRemainTransparent`; otherwise `Unknown`.

The raw strategies keep their bodies and their early exits untouched. The mapped walks are separate, small, and duplication is accepted over perturbing proven hot paths.

### The resolution: mapped classified and uniform arms (Analysis)

`AlphaResolution.Classified` gains an optional `AffineAlphaMap` parameter, default null. A mapped classified resolution carries the same chain, sampling, and mapping as today, and its per-level loop calls `ClassifyMapped` with the map instead of `Classify`. The absorbing per-level fold is unchanged.

`AlphaSemanticsResolver` gains `ResolveMappedSample(sample, channel, map, provider, noise)`:

- `Evaluate(0)` and `Evaluate(1)` decide two uniform arms with no texel: both lower bounds equal to 1 → `Uniform(ProvenOpaque)`; both upper bounds below 1 → `Uniform(MustRemainTransparent)`. The field contract bounds every reachable red to [0, 1], so these arms need no contents, exactly like the existing multiplier shortcut.
- Otherwise a provider lookup for `(source, channel)`, a missing field refusing with `MissingTextureEvidence`, and a mapped classified resolution on success.

`ResolveProductChain` learns two folds for uniform factors, both already true of the existing multiplier lemmas: a `Uniform(MustRemainTransparent)` factor makes the whole chain uniform transparent, and a `Uniform(ProvenOpaque)` factor drops out of the conjunction. Classified factors conjoin as today, each under its own map.

### The algebra: `MappedTextureSample` and chain maps (Semantics)

`ScalarSemanticValueKind` gains `MappedTextureSample`. The constructor carries the sample, the channel, and an already-constructed `AffineAlphaMap`, and mirrors the existing kind accessors with kind-mismatch throws. The frontend builds the map from the term's floats through `FromBinary32`, so validation happens once at the construction site. The value is `saturate(channel * s + v)` at the sampled coordinate.

`ProductChainOfTextureSamples` gains a parallel per-factor map list, null entries meaning identity, with the same validation style as the existing sample and channel lists. The represented value becomes `fl(fl(k * f0) * f1) ...` where each factor may be a mapped sample. A single-factor chain is valid only when its factor carries a map, because a collapsed main constant times one mapped mask factor is exactly that shape and the kind system must keep its two-step rounding chain distinct from a bare scaled sample. The product lemma survives unchanged: every factor is bounded in [0, 1] by the field contract and saturate, and a product of such values rounds to one only when every factor is one, so the per-factor conjunction remains the product's predicate.

Both `Multiply` helpers in the lilToon frontends and the Poiyomi one flatten factors into a chain; the lilToon ones learn to carry a mapped factor's map. Poiyomi is otherwise untouched.

### The term: `MappedSample` and the new arms (Semantics)

`LilToonAlphaMaskTermKind` gains `MappedSample`, carrying source, mapping, scale, value, and the replace flag. `Interpret` keeps its gate order and gains two arms:

- `scale == 0`: the constant arm of the admitted-case table, reusing `ConstantTerm` and `MainUnchanged` and refusing with `_AlphaMaskScale` when mode 2 cannot prove the unchanged main.
- otherwise, for finite non-identity pairs with an assigned mask: `MappedSample` with the captured source identity, the mask's own plain affine mapping, and the raw scale and value floats.

The final fall-through refusal is deleted, and the type's doc block states the new admitted set and the rounding argument.

### The frontends (Semantics)

Both lilToon alpha frontends change only the mask consumption site. Mode 1 completes the alpha as `ScalarSemanticValue.MappedTexture(maskSample, TextureChannel.Red, scale, value)`. Mode 2 multiplies the layered chain by the same mapped factor through the existing `Multiply` helper. The unassigned-main `UnsupportedSampling` refusal for a sampled mask is unchanged: a mapped mask borrows the main sampler exactly as the plain one does. Runtime-state admission is unchanged: `_AlphaMaskScale` and `_AlphaMaskValue` are already requested scalars, so non-singleton animation refuses through the existing machinery, and object curves on `_MainTex` refuse upstream as before.

## Testing

Product tests run in the dev editor instance through the Test Runner, EditMode mode. Tests never run in the Census Lab. A filtered run reporting zero tests is a failure, and observed counts are recorded.

- `AffineAlphaMapTests`: exact envelopes against both C# orders as oracles, `MathF.FusedMultiplyAdd` and plain `r * s + v`, over adversarial and random finite floats; the envelope must contain both clamped orders and collapse to a point when they agree; identity and zero-scale factory throws; infinity rounding.
- Classifier mapped tests in `TriangleAlphaClassifierTests`: all-255 domains prove through maps that keep one; a witness domain proves transparent only when the mapped upper bound stays below 1; mixed envelopes answer Unknown; erased texels act as witnesses; anisotropy refuses; wrap and filter dispatch mirrors the raw table.
- Resolver tests in `AlphaSemanticsResolverTests`: the two uniform arms from pure map facts; missing red evidence refuses by name; a mapped product factor conjoins; uniform factors fold in the product; the identity map never reaches a mapped kind.
- Frontend tests flip and extend. Today's pinned transparent refusals become the RED set: the `LilToonTransparentAlphaTests` value-low and scale-shifted rows must change from Unknown-at-`_AlphaMaskValue` to their new provable outcomes, observed failing before the frontend task lands. The cutout family has no pinned refusal rows for this shape, so its tests are new RED pairs written first for the same configurations. New falsifiers pin the boundary: a value chosen so the fused and unfused orders disagree at a sub-255 red must answer Unknown, never opaque; negative scale swaps the envelope; `scale == 0` arms behave per table; modes 3 and 4 keep refusing.
- The evidence-request schema pins stay green and unchanged: this design requests no new capture kinds.

After the focused runs, the full `Alrauna.Amuse.Tests.Editor` assembly runs, and the `Alrauna.Amuse.Research.Tests.Editor` assembly runs because it consumes the same internals.

## Expected population effect

The two investigated Census Lab slots stop refusing and classify per triangle. Mask regions the map proves at or above one convert under the existing opaque policy; regions the map proves below one record honest must-stay-transparent; genuinely mixed domains stay Unknown with the original material. Materials on the `(1, 0)` and `(1, v >= 1)` arms are unaffected bit for bit.

## Non-goals

- Modes 3 and 4.
- The Poiyomi mask equation and its own admitted strength-value pairs; it applies pressure on this seam in a separate design.
- Outline families; no attested frontend exists.
- Mip-tail reachability and near-one tolerance.
- Widening the erasure-substitution policy to mapped factors.
- A sampled mask over an unassigned main, which keeps refusing by name.

## Risks and limits

The one external assumption stays the field contract itself, which the existing capture attests and the 2026-09-07 design argued for both channels. The mapped proof adds no new assumption about capture. The duplicated mapped walks risk drifting from the raw geometry; the mitigation is the shared geometry helpers plus falsifier tests that fail if the two walks disagree on the witness question for the same domain. The uniform arms widen what a product can fold; the guard is the existing rule that product composition rejects uniform factors except through the two explicit folds this design adds.
