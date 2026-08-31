# Affine `_MainTex_ST` support — design specification

Status: design (no production or test code). Branch:
`design/affine-maintex-st-support` from `main` at `89cc5be` (merge of PR #41).
Supersedes nothing; it extends the deferred obligation recorded in
`docs/superpowers/specs/2026-08-17-alpha-semantics-resolver-design.md` ("UV
mapping", follow-up paragraph) and in the cutout plan
(`docs/superpowers/plans/2026-08-30-liltoon-cutout-opaque-conversion.md`,
"Follow-ups"). The historical identity-ST records are not rewritten; they are
referenced where their facts are reused.

Labels: `[SOURCE]` read from pinned code (AMUSE checkout, or the official
lilToon `2.3.4` tag fetched and hashed for this design — file SHA-256 values in
Appendix A); `[MEASURED]` observed from executed commands; `[SPEC]` verified
from a primary API/architecture specification; `[INFERENCE]` bounded conclusion
from those facts; `[DECISION]` a controller-owned choice made here.

## 1. Problem and user-visible coverage gain

`AlphaSemanticsResolver.IsSupportedMapping`
(`Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:338-345`)
admits only channel 0 with `Scale == (1,1)` and `Offset == (0,0)`. Every
material whose `_MainTex` uses a tiling scale or an atlas offset therefore
loses its alpha proof entirely: the resolution is refused
(`UnsupportedUvMapping`), every triangle of the submesh classifies `Unknown`,
and no triangle is ever eligible for proven-opaque conversion.

Non-identity `_MainTex_ST` is common on avatars — power-of-two tiling (hair,
fabric), half/fractional atlas rectangles, and mirrored placements. The
coverage gain is: materials in this class keep the full exact per-triangle
alpha proof, and triangles whose transformed domain lies entirely over opaque
texels in every mip become `ProvenOpaque` and convertible, instead of being
unconditionally transparent.

The risk that bounds the design: the classifier receives binary32 triangle UVs
and the GPU evaluates `uv * scale + offset` per fragment in binary32. A naive
transform can round the analyzed domain inward at a texel or wrap boundary,
omit a transparent texel, and produce a false `ProvenOpaque` — a false positive
of the worst kind (invisible geometry materialized). The controlling invariant:

> Increasing arithmetic uncertainty must never increase the chance of
> `ProvenOpaque`.

## 2. Current behavior and exact refusal point

Data flow today (`[SOURCE]`):

1. Capture. `_MainTex` is requested with `TextureEvidenceKinds.ScaleOffset`;
   `UnityMaterialEvidenceCapture` stores `material.GetTextureScale/Offset` as
   `Vector2`s on the texture assignment (`UnityMaterialEvidenceCapture.cs:843-856`).
   Finite-ness of scale/offset is validated by the `UvMapping` constructor
   (`MaterialSemantics.cs:47-60`), which throws on NaN/±∞ — so a non-finite
   mapping cannot reach the resolver.
2. Animation admission. A derived `material._MainTex_ST` binding is
   proof-relevant (`UnityAnimationEvidenceCapture.DeriveTextureScaleOffsetProperties`,
   `:480-495`; suffix `_ST` at `:95`) and is admitted only as an exact
   singleton equal to the material's captured default
   (`AdmittedMaterialStates.cs:418-472`); non-singleton refuses the slot
   (`AnimatedMaterialPropertyNotSingleton`), non-finite refuses
   (`UnsupportedAnimationCurveForm`).
3. Semantic value. Both family frontends already construct non-identity
   `UvMapping`s without gating ST: the lilToon cutout frontend builds
   `new UvMapping(0, assignment.Scale, assignment.Offset)`
   (`LilToonCutoutMaterialSemantics.cs:330`), and Poiyomi's alpha path builds
   `new UvMapping(channel, assignment.Scale, assignment.Offset)`
   (`PoiyomiMaterialSemantics.cs:601-603`). Neither gates the values; only the
   resolver does.
4. Refusal point. `AlphaSemanticsResolver.ResolveSampled` calls
   `IsSupportedMapping(sample.Coordinates)` and refuses with
   `UnsupportedUvMapping` before evidence lookup
   (`AlphaSemanticsResolver.cs:300-325, 338-345`). A refused resolution
   produces `Unknown` for every triangle downstream
   (`UnityRendererAlphaAnalysis.cs:618-633`) — analysis completes, but nothing
   is ever proven. [MEASURED against current checkout: the gate is the sole
   place identity is required; capture, admission, and both frontends already
   carry arbitrary finite ST.]

The classifier itself (`TriangleAlphaClassifier.Classify`,
`TriangleAlphaClassifier.cs:175-232`) takes only `(triangle, texture,
sampling)`: it decodes the supplied binary32 corner UVs exactly
(`ExactUvGeometry.DecodeFloat`), treats their closed convex hull in texel
units as the sampled domain (`CreateTextureScaledDomain`,
`ExactUvGeometry.cs:227-262`), and proves per mode (Point/Bilinear ×
Clamp/Repeat) that no non-opaque texel's footprint intersects the domain, with
a per-level region budget (`MaxSupportRegions = 65536` → `Unknown`).
`AlphaResolution.Classify` (`AlphaSemanticsResolver.cs:165-204`) runs the
conjunction over the full mip chain: `MustRemainTransparent` is absorbing,
`Unknown` propagates, `ProvenOpaque` requires every level to prove.

## 3. Verified shader and runtime arithmetic facts

### 3.1 Where lilToon 2.3.4 cutout applies `_MainTex_ST`

`[SOURCE]` — fetched from the official `lilxyzw/lilToon` tag `2.3.4`
(commit `252fd8cfc46106d4967e95b3f2c788418502f227` per B2 §2); file digests in
Appendix A.

- The cutout pass unconditionally defines `LIL_FEATURE_ANIMATE_MAIN_UV`
  (`Shader/ltspass_cutout.shader:645`), so the fragment's main-UV macro picks
  the time-variant arm (`Shader/Includes/lil_common_frag.hlsl:260-263`):
  `fd.uvMain = lilCalcDoubleSideUV(fd.uv0, fd.facing, _ShiftBackfaceUV);`
  `fd.uvMain = lilCalcUV(fd.uvMain, _MainTex_ST, _MainTex_ScrollRotate);`
- `fd.uv0` is the interpolated raw UV0 (`lil_common_vert.hlsl:186`:
  `LIL_V2F_OUT_BASE.uv0 = input.uv0;`) — the ST transform is a **fragment-stage**
  expression applied after rasterizer interpolation. (The vertex stage computes
  a separate `uvMain` at `lil_common_vert.hlsl:98` for vertex purposes; the
  main sample uses the fragment value.)
- `lilCalcUV` (`lil_common_functions.hlsl:453-465`):
  `outuv = uv * uv_st.xy + uv_st.zw;` then
  `lilRotateUV(outuv, uv_sr.z + uv_sr.w * LIL_TIME) + frac(uv_sr.xy * LIL_TIME)`.
  With `_MainTex_ScrollRotate == (0,0,0,0)` (a standing gate) both time terms
  vanish and the angle is exactly `+0`.
- `lilRotateUV(uv, 0)` (`lil_common_functions.hlsl:424-437`) has **no
  zero-angle early-out**: it evaluates
  `sincos(0, si, co)`, `outuv = uv - 0.5`, rotates by the matrix `[co −si; si co]`,
  and adds `0.5`. So even at the gated identity the executed expression is
  `fl(fl(fl(t−0.5)·co − fl(t'−0.5)·si) + 0.5)` per axis, not the identity —
  a bounded identity-value arithmetic fragment (deviation bound in §4).
- `_ShiftBackfaceUV == 0` (a standing gate) makes `lilCalcDoubleSideUV` return
  `uv` unchanged with no arithmetic (`lil_common_functions.hlsl:467-470`).
- The main sample then reads `_MainTex` at `fd.uvMain`
  (`lil_common_frag.hlsl:311-312`, `LIL_SAMPLE_2D_POM`; the POM macro's
  gradient-sample footprint equals a normal sample at `_UseParallax == 0`).

Consequence: for the supported cutout source the semantic claim "sampler
receives the binary32 affine expression of the interpolated UV0" is exact
up to (i) binary32 rounding of `uv*st.xy + st.zw` and (ii) the bounded
rotate-at-zero fragment (ii) exists for identity too and is a pre-existing
declared abstraction of the shipped identity slice (§4).

`[SOURCE]` — Poiyomi: the alpha frontend is pinned to Poiyomi Toon Shader
9.3.64 with readable-source attestation
(`PoiyomiMaterialSemantics.cs:14-17, 1256-1341`) and its semantic value claims
the canonical `uv * _MainTex_ST.xy + _MainTex_ST.zw` shape with pan gated to
exact zero (`PoiyomiMaterialSemantics.cs:585-599`) — the expression the
existing adapter design pinned (`docs/superpowers/specs/2026-08-16-poiyomi-semantics-adapter-design.md`).
No trigonometric or other fragment arithmetic sits between the affine result
and the sampler in that claim. The vendor source is not present in this
project; the frontend's attestation is the enforcement point for the claim.

### 3.2 GPU binary32 arithmetic guarantees

- binary32: 24-bit significand (incl. implicit bit), normal exponent range
  2^-126…2^127, subnormals down to 2^-149, `ulp(x) = 2^(e-23)` for
  `|x| ∈ [2^e, 2^(e+1))`, hence `ulp(x) ≤ 2^-23·|x|` for normal x.
  [SPEC: IEEE 754-2019 representation.]
- Direct3D 11.3 functional spec: 32-bit shader `add`/`sub`/`mul` must be
  within **0.5 ULP** of the infinitely precise result (IEEE-754
  round-to-nearest-equivalent, ties permitted either way); **fused** multiply-add
  (`mad`) must be at least as accurate as the worst permitted serial
  unfused expansion, and may retain extra intermediate precision. [SPEC:
  D3D11.3 Functional Spec §3.1 "Floating Point Rules"]
  (`https://microsoft.github.io/DirectX-Specs/d3d/archive/D3D11_3_FunctionalSpec.htm`).
- D3D11.3 `sincos`: **maximum absolute error 0.0008** for inputs in
  `[−100π, +100π]`; result confined to `[−1,1]` outside. [SPEC: same spec,
  `sincos` instruction section.] The angle here is exactly `+0` at the gated
  identity; the spec bound (not the observably exact `(0,1)` of real
  implementations) is what a sound design may assume. [INFERENCE: relying on
  vendor special-casing of sin(0)=0 would be claiming universal GPU behavior;
  this design refuses to.]
- Vulkan: without an explicit execution mode, correctly rounded operations use
  an **implementation-defined rounding mode**, and **denormal inputs and
  intermediate results may be flushed to zero** by default; `RoundingModeRTE`
  and `DenormPreserve` are optional features. [SPEC: Vulkan 1.2 SPIR-V
  environment appendix / `VK_KHR_shader_float_controls`]
  (`https://docs.vulkan.org/spec/latest/appendices/spirvenv.html`).
- Metal: FP32 permits subnormal operands/results to be flushed to zero;
  fast-math defaults have historically been permissive and are controlled by
  `MTLCompileOptions.mathMode`. [SPEC: Metal Shading Language Specification;
  Apple developer documentation.]
- Compiler contraction (FMA) is allowed and varies by driver/platform.
  [SPEC: D3D11.3 fused-op rule above; cross-API practice.]

Summary consequence for the design: AMUSE may assume per-operation error
bounded by **one ulp of the operand magnitude** (covers 0.5-ulp RNE, ties
either way, RTZ-style implementation-defined rounding within the same ulp
grid, and fused-or-not contraction), a **fixed 2^-149 subnormal ulp**, and
**flush-to-zero of subnormal operands and results**. It may not assume any
specific rounding mode, exact `sincos(0)`, denormal preservation, or
intermediate precision. [INFERENCE, load-bearing]

### 3.3 Interpolation

Perspective-correct interpolation of a vertex attribute is, per fragment, a
convex combination (positive weights summing to one) of the three corner
values; every hardware path (direct or perspective-divided) yields a value
within the closed convex hull of the corners. The precision of that
interpolation is not pinned by any cross-vendor spec AMUSE can cite.
[SPEC for convexity; INFERENCE for the precision vacuum.] The existing
classifier already declares the resulting model: the sampled domain is the
exact hull of the decoded binary32 corners — i.e., AMUSE idealizes
interpolation as exact-linear onto binary32 values inside the hull. Identity
ST ships on this idealization today; this design changes neither its level nor
its role (§4).

## 4. Declared product arithmetic contract

This section makes the previously implicit modeling explicit and states the
one new clause. It is the contract the proof in §6 consumes.

C1. **Corner decode.** Triangle UVs reach analysis as binary32; analysis
decodes them exactly (dyadic). No rounding ever enters AMUSE's own arithmetic.

C2. **Hull model (pre-existing, now written down).** Every fragment's
pre-transform coordinate is a binary32 value inside the closed convex hull of
the three decoded corner values. AMUSE does not model the deviation between
the hardware-interpolated value and the exact-linear value. This is the
idealization identity ST has shipped with since the identity slice; its
magnitude is relative (≈½ ulp of the coordinate) and it is unchanged by this
design.

C3. **Transform expression.** The sampler input is the binary32 evaluation of
the affine expression `t = uv·s + o` per axis (validated per family by the
frontend's attested source pin), optionally followed by bounded
identity-value arithmetic specific to the family fragment (today: only the
lilToon rotate-at-zero round trip, bounded by the D3D11.3 `sincos` absolute
error 0.0008 < 2^-10 plus three ulp-bounded roundings).

C4. **Two-tier modeling (new clause).** For a non-identity mapping the
resolver classifies either:
  - **Exact tier** — when the binary32 expression provably equals the real
    affine map for every binary32 value in the hull (conditions in §7): the
    modeled domain is the exact affine image of the hull, represented exactly
    (transformed corners are binary32-representable, expression envelope
    zero), plus the family-fragment allowance of §6.2 — which is zero only
    for the identity mapping and otherwise scales with the mapped magnitude,
    because the fragment's noise scales with `|t|` and an unpadded
    large-magnitude exact-tier proof would be unsound. This is precisely the
    extension criterion the 2026-08-17 resolver spec deferred, strengthened
    from "exact at the vertices" to "exact on the hull" (§5-A).
  - **Envelope tier** — otherwise: the modeled domain is the exact affine
    image, corner-encoded to nearest binary32, inflated by an exact dyadic
    outward envelope that bounds every binary32 evaluation result and the
    family fragment (derivation in §6). Every boundary the runtime could
    cross is inside the classified domain.
For the identity mapping the envelope is exactly zero and the pipeline,
inputs, and outcomes are byte-for-byte today's (parity, §6.4).

C5. **Baseline clause.** The identity slice absorbs the family fragment noise
(C3) without modeling it — shipped behavior, unchanged. Every non-identity
admission models strictly more uncertainty than that baseline (envelope tier)
or models the expression exactly while carrying an explicit family-fragment
allowance (§7). No newly admitted transform ever proves with weaker modeling
than the identity baseline it joins.

C6. **Monotone safety direction.** Modeled domains only ever widen relative
to the idealized model, widening can only preserve or weaken `ProvenOpaque`
(structural property, §6.3 step 4), so increased arithmetic uncertainty can
never increase the chance of `ProvenOpaque`. This discharges the controlling
invariant by construction, not by assumption.


## 5. Alternatives considered

### A. Exactly representable transformed vertices

Compute `uv·s + o` in exact arithmetic per corner; admit when every result is
exactly representable as binary32; feed the transformed floats to the
unchanged classifier.

Verdict: **necessary but insufficient as stated; rejected as the mechanism.**
`[INFERENCE]` Corner representability constrains nothing about the expression's
rounding at interior coordinates: the runtime evaluates `fl(fl(u·s)+o)` at
every interpolated binary32 `u`, and a corner-exact transform (e.g.
`s = 0.1f`-ish products that happen to fit) can still round interior samples
across a texel or wrap boundary. The classifier would then prove an exact
statement about a domain that excludes realizable sample points — exactly the
inward-rounding false positive the problem statement bans. Strengthened to
"exact on the whole hull" the criterion becomes decidable and sound — that
strengthening is kept, as the exact tier inside the selected design (§7), but
alone it covers only power-of-two scales with zero offset (plus degenerate
axes and zero scale), which forfeits the offset half of the coverage goal.

### B. Exact-rational transformed hull (no rounding model)

Carry `UvMapping` into exact geometry, apply scale/offset to the exact domain,
classify the rational hull directly.

Verdict: **rejected.** `[INFERENCE]` This models the ideal real-number
expression, not the domain the shader samples: the runtime rounds
`uv·s + o` per fragment, and at coordinates whose exact image sits on a cell
or wrap boundary the rounded sample can land on the far side. An exact model
of the wrong domain is unsound, and making it "sound" by declaring expression
rounding away (as the hull model does for interpolation) would be a *new*
unmodeled uncertainty admitted together with new coverage — a direct violation
of C6/the controlling invariant. It is exactly approach C with the envelope
forced to zero.

### C. Conservative arithmetic envelope — **selected**

Bound every possible binary32 evaluation result outward with an exact dyadic
envelope and classify the union (the inflated exact image). Feasible with the
existing machinery: the classifier is coordinate-set-based (hull + clip +
interval intersection), so inflating the hull before mode dispatch preserves
every mode's semantics (Point/Bilinear footprints, Clamp/Repeat
normalization, all-mip conjunction, region budget). Costs: one exact-hull
expansion, one dyadic bound derivation, and a one-cell caution ring at
footprint boundaries for envelope-tier triangles. Sound by construction and
covers the full useful subset (§7). Selected.

### D. Narrower supported subset (power-of-two scales, zero offset)

Admit only `s ∈ ±2^k`, `o = 0` (and the degenerate/zero-scale companions).

Verdict: **rejected as the architecture; retained as the exact tier.** The
proof is trivial (expression value-exactness) and the machinery minimal, but
it forfeits every atlas-rectangle material — scale-with-offset, the single
most common non-identity form. Its admission predicate survives verbatim as
the exact tier of the selected design (which, for non-identity mappings,
carries the same family-fragment allowance as every other admitted transform,
§6.2).

## 6. Selected design and proof argument

### 6.1 Mechanism

1. `AlphaResolution` carries the `UvMapping` (factory `Classified` gains one
   parameter; the resolver's only other change is that `IsSupportedMapping`
   narrows to the channel test — §8).

2. `AlphaResolution.Classify(triangle)` computes, once per triangle:
   - per axis: the tier decision and, in the envelope tier, the dyadic
     envelope (new pure helper `AffineUvTransform`, §7);
   - the transformed corner `Vector2`s (exact tier: exact products;
     envelope tier: exact products encoded to nearest binary32);
   - the mapping-identity short-circuit: `s = (1,1), o = (0,0)` yields the
     original triangle with zero envelope and skips the helper entirely.
3. `TriangleAlphaClassifier.Classify` gains a fourth parameter: the
   per-axis UV-space envelope as an exact rational pair (zero for the identity
   mapping). `CreateTextureScaledDomain` inflates the hull by
   `(ex·width, ey·height)` in texel units after construction — implemented as
   the convex hull of every domain vertex offset by `(±ex, ±ey)` (a small
   exact monotone-chain hull added to `ExactUvGeometry`, which also turns a
   degenerate 1–2-vertex domain into its rectangle), with a zero-envelope
   early-out that returns the domain object unchanged so identity parity is
   structural, not incidental. Every downstream step (mode dispatch,
   candidate ranges, clip, `NormalizeRepeat`, budget) is unchanged, and the
   inflation strictly precedes Repeat normalization so the floor shift sees
   the inflated minima. All callers are migrated (clean cutover, no overload
   shim).
4. Overflow guard: if any corner's `|u·s|` or `|u·s + o|` reaches 2^127
   (dyadic corner check), the triangle returns `Unknown` — the runtime could
   produce ±∞, which no envelope contains.

### 6.2 Envelope derivation

All quantities are exact dyadic/rational computations on the decoded corner
values (corner index `i`, axis coordinate `u_i`, scale `s`, offset `o`):

- `t_i = u_i·s + o` (exact). Encoded corner `t̃_i` = nearest binary32;
  `B_enc = max_i ½·ulp(t̃_i)` covers rational→float encoding (direction-free:
  the envelope absorbs either rounding direction).
- `B_st = 2^-22 · (max_i |u_i·s| + max_i |t_i|) + 2^-125`.
  Coverage argument, one term at a time, under §3.2's guarantees:
  two roundings (`u·s`, then `+o`), each ≤ 1 ulp of its operand — 0.5-ulp RNE,
  ties either way, implementation-defined one-ulp-conforming rounding, fused
  or unfused contraction all fit — gives
  `|rt − t| ≤ ulp(|u·s|) + ulp(|t + e₁|)` where `|e₁| ≤ 2^-23|u·s|`;
  with `ulp(x) ≤ 2^-23·|x|` this is `≤ 2^-23|u·s| + 2^-23(|t| + 2^-23|u·s|)
  ≤ 2^-22(|u·s| + |t|)`; the fixed `2^-125` floor covers subnormal-result
  granularity (subnormal ulp is 2^-149 ≤ 2^-23·2^-126 for normal-bounded
  terms) and FTZ/DAZ displacement (both bounded by the subnormal magnitude
  < 2^-126 < 2^-125). Corner-max suffices because `|u·s|` and `|u·s + o|` are
  convex in `u`, so their maxima over the triangle are attained at corners.
- `B_noise = 2^-9 · (1 + max_i|t_x,i| + max_i|t_y,i|)` (both axes, added to
  each axis's envelope). Coverage: the family fragment of C3 — today only the
  lilToon rotate-at-zero round trip: `|si| ≤ 2^-10` and `|co − 1| ≤ 2^-10`
  (D3D11.3 `sincos` absolute error 0.0008 < 2^-10 = 0.000977), applied to
  `|t − 0.5| ≤ |t| + 0.5`, plus three ≤ 1-ulp roundings of operands ≤
  `|t| + 1`; the sum is dominated by `2^-9·(1 + |t_x| + |t_y|)`. Poiyomi
  carries this term as pure slack. APIs without a citable `sincos` error
  bound (Metal, GLSL) adopt the same constant as a declared conservative
  assumption — the D3D11.3 number is the only anchor any vendor pins.
  `[INFERENCE]` — every numeric constant above is dyadic and conservative;
  the derivation is repeated as code comments and pinned by boundary tests.
- Per-axis envelope `E_axis = [tier == envelope ? B_enc + B_st : 0]
  + [mapping == identity ? 0 : B_noise]`. The noise term applies to the
  exact tier as well (zero only for identity): the fragment executes for
  every mapping, and its absolute deviation scales with the mapped
  magnitude `|t|` — an unpadded large-magnitude exact-tier proof (e.g.
  `s = 2^20`) would be unsound. The expression terms `B_enc`/`B_st` are
  exact-tier-zero because the expression is proven exact there.

### 6.3 Soundness statement (the required proof)

Claim: for every triangle classified under a resolved, admitted mapping, and
for every fragment that the rasterizer can produce, the fragment's sampler
coordinate lies inside the domain the classifier tests.

1. The fragment's pre-transform coordinate is a binary32 value in the closed
   corner hull (C2; convexity of perspective-correct interpolation, §3.3).
2. Exact tier: the binary32 expression maps every binary32 hull value to its
   exact affine image (§7 conditions are chosen to make exactly this true —
   power-of-two scaling preserves significands and normal-range guards exclude
   rounding; zero-scale and degenerate-axis cases are pointwise exact by
   direct check), and `B_noise` bounds the family fragment; hence
   `rt ∈ hull(t̃_i) ⊕ box(E)` with `E = box(B_noise)`.
3. Envelope tier: `|rt − t| ≤ B_enc + B_st` by §6.2 (encode, then two
   ulp-bounded operations), and `B_noise` covers the family
   fragment; hence `rt ∈ hull(t̃_i) ⊕ box(E)`, which is precisely the inflated
   domain.
4. Monotonicity: the classifier proves `ProvenOpaque` only when no non-opaque
   texel's footprint interval intersects the supplied domain, and
   `MustRemainTransparent` when some does. Inflating the domain can only add
   intersecting texels — `ProvenOpaque` can only be preserved or lost, never
   gained, and the all-mip conjunction inherits the direction (`Unknown`
   propagates, transparency absorbs).
5. Therefore no transformed sampled coordinate can escape the classified
   domain; no false `ProvenOpaque` is reachable under C1–C6. "Exact
   arithmetic" appears only as the tooling; the proof rests on the containment
   argument above, not on exactness alone.

### 6.4 Why identity is untouched

`s = (1,1), o = (0,0)` short-circuits before any transform work: original
corner floats, zero envelope, identical classifier inputs. Existing resolver,
classifier, integration, fixture, and preparation tests are asserted unchanged
in the plan (parity falsifier M13). The lilToon rotate-at-zero noise exists at
runtime for identity as for everything else; it is the shipped baseline's
declared abstraction (C5) and is deliberately not newly modeled — doing so
would widen identity domains and change identity outcomes.

### 6.5 Performance and allocation constraints

Envelope and tier decisions are computed once per `Classify` call (not per
mip, not per texel); per-level expansion is two rational multiplications per
vertex. All arithmetic reuses `ExactDyadic`/`ExactRational`/`BigInteger` —
the allocation profile is the classifier's existing profile plus O(1)
rationals per triangle. No LINQ, no closures in the new code; the region
budget `MaxSupportRegions` is untouched and continues to bound all cell
enumeration. [SOURCE: current classifier structure; INFERENCE: additive cost
is within one hull construction.]

## 7. Exact supported affine subset

Per axis, with `hull` the exact corner interval and `s`, `o` the decoded
binary32 mapping values:

| # | Condition (per axis) | Tier | Modeled axis domain |
|---|---|---|---|
| E1 | `s ∈ ±2^k` (any k), `o = ±0`, and both the hull interval and the mapped hull interval each lie entirely in `[2^-126, 2^127)` or entirely in `(−2^127, −2^-126]` (all-normal on the input side too — a subnormal input can be DAZ-flushed even when its scaled product is normal) | Exact | exact scaled hull (+ `B_noise` unless identity) |
| E2 | `s = ±0` (any `o`) | Exact | the single point `o` (`fl(±0 + o) = o` exactly; `fl(±0 ± 0) = ±0`), + `B_noise` unless identity |
| E3 | axis degenerate (`min == max`) and `fl(fl(c·s)+o) == c·s + o` verified exactly in dyadics for the single value `c` | Exact | the point `c·s + o`, + `B_noise` unless identity |
| V | anything else (fractional scales, offsets on non-degenerate axes, subnormal-adjacent or zero-crossing mapped ranges) | Envelope | inflated exact image |

The material is admitted when `Channel == 0` (§8); the tier decision is
per-triangle because E1/E3 depend on the hull. A mapping with `s = (4,0.5)`,
`o = (0,0)` is fully exact-tier; `s = (2,2), o = (0.5,0.25)` is envelope-tier
on both axes; `s = (2,2), o = (0.5,0)` is exact on V, envelope on U. Zero
scale on one axis with envelope on the other composes (point axis ⊕ box = a
degenerate rectangle the hull machinery already represents).

## 8. Material-refusal versus per-triangle-`Unknown` boundary

- **Material-level refusal (`UnsupportedUvMapping`):** `Channel != 0` only.
  Mesh capture supplies UV0 alone (`UnityRendererAlphaAnalysis.cs:346-357`),
  so no caller could honor a wider channel claim. Non-finite scale/offset is
  unreachable (the `UvMapping` constructor throws); if a future capture path
  bypasses it, that is a programming defect, not a refusal.
- **Per-triangle `Unknown`:** overflow guard (§6.1); region-budget overflow
  (`MaxSupportRegions`, now more reachable for huge-scale Repeat mappings);
  degenerate mesh geometry (existing); missing UV0 (existing). None of these
  may collapse to `ProvenOpaque` or to material-level support.
- **Uniform `MustRemainTransparent`:** unchanged sources (fully non-opaque
  level, multiplier lemma) — orthogonal to ST.
- **Renderer-level refusal:** unchanged — non-singleton/non-finite/absent
  `_MainTex_ST` animation admission (existing machinery; §10).

## 9. Supported/unsupported boundary table

Every required boundary, with its outcome:

| Boundary | Outcome |
|---|---|
| Positive fractional scale (e.g. 2.5) | supported — classified (envelope tier) |
| Scale > 1 (e.g. 4) | supported — classified (exact tier if ±2^k, else envelope) |
| Negative scale / reflection | supported — classified (E1 exact for ±2^k; hull of transformed corners covers the flip; envelope otherwise) |
| Zero scale (one axis) | supported — classified as a point/line domain (E2); explicitly not missing-UV evidence |
| Zero scale (both axes) | supported — classified as a point domain |
| Fractional offset | supported — classified (envelope tier) |
| Negative offset | supported — classified (envelope tier) |
| Very large finite ST values | supported while the mapped hull stays finite; per-triangle `Unknown` at the 2^127 guard |
| Normal ST/UV values | supported (E1 exact or envelope) |
| Subnormal ST or UV values | supported — classified (envelope tier; floors cover FTZ/DAZ); E1 refuses subnormal ranges into the envelope tier |
| Exact product (`u·s` representable) | part of tier logic (E1) — no expression padding (`B_noise` still applies unless identity) |
| Inexact product | envelope tier — padded |
| Exact addition (`u·s + o` representable on the hull) | only pointwise decidable (E3 degenerate axes); non-degenerate offsets always envelope |
| Overflow (runtime ±∞ reachable) | per-triangle `Unknown` (guard) |
| Underflow (subnormal results) | envelope tier — classified (floor term) |
| Signed zero (`o = −0`, `s = −0`) | `−0 == 0` numerically; E1/E2 arms handle; coordinates ±0 sample identically — classified |
| Nonzero UV channel | material-level refusal (`UnsupportedUvMapping`) — unchanged |
| Degenerate mesh geometry | per-triangle `Unknown` — unchanged |
| Degenerate UV hull (line/point) | supported — classified (hull machinery handles 1–2 vertex domains; E3 covers pointwise exactness) |
| Point filter | classified; footprint = cell (envelope-agnostic) |
| Bilinear filter | classified; half-texel open footprints applied to the inflated domain |
| Clamp wrap | classified; border-cell unbounded intervals absorb envelope overshoot |
| Repeat wrap | classified; `NormalizeRepeat` is exact integer-period translation — commutes with inflation; boundary cells included by open/closed interval ownership |
| Every mip level | conjunction unchanged — `ProvenOpaque` only if every level proves over the inflated per-level domain |
| Exact-singleton ST animation | admitted (existing singleton machinery; the animated value equals the captured non-identity default and flows through as the material's mapping) |
| Non-singleton ST animation | renderer-level refusal (`AnimatedMaterialPropertyNotSingleton`) — unchanged |
| Texture swaps carrying different ST | supported — each admitted material resolves against its own captured mapping; consensus intersection across states (existing `IntersectOutcomes`) |
| Transform exceeding exact-region complexity limits | per-triangle `Unknown` (`MaxSupportRegions`) — never "empty support" and never opacity |

No uncertain case defaults to `ProvenOpaque`: every uncertain arm above lands
on refusal, `Unknown`, or `MustRemainTransparent`.

## 10. Data flow (capture → apply)

1. **Capture** — unchanged: `ScaleOffset` evidence on `_MainTex` (both family
   alpha requests already request it).
2. **Animation admission** — unchanged: derived `_MainTex_ST` bindings are
   exact-singleton-admitted against each material's own captured default;
   refusal modes unchanged. No new relevance names (`_MainTex_ST` stays a
   derived texture-scale-offset name, never vector evidence).
3. **Semantic value** — unchanged constructors; `TextureSample.Coordinates`
   now carries non-identity values through both frontends to the resolver.
4. **Resolution** — `ResolveSampled` narrows `IsSupportedMapping` to the
   channel test; `AlphaResolution.Classified` stores the mapping.
5. **Per-triangle transformation** — `Classify` computes tier + transformed
   corners + envelope once (§6.1); identity short-circuit preserves parity.
6. **All-mip classification** — per-level inflation of the exact hull; the
   conjunction, absorbing transparency, propagating `Unknown`, and budget are
   untouched; outcomes flow to `IntersectOutcomes` and the separation planner
   exactly as today.

## 11. Family and architecture boundary

**Decision: shared `AlphaSemanticsResolver` capability, available to every
semantic frontend — restriction rejected.** [DECISION, matching the task's
default recommendation.] Grounding: `UvMapping` belongs to the generic
`TextureSample` semantic value (`MaterialSemantics.cs:146-191`); both shipped
frontends already construct non-identity mappings (`§2.3`); the transform and
envelope are functions of the mapping plus the triangle and texture — no
family input exists or is needed. Concrete family-specific evidence that would
override this (a family whose ST is *not* an affine fragment-stage transform
of UV0) was sought and not found: lilToon's expression is fragment-stage
affine (+ bounded noise, §3.1); Poiyomi's claim is affine with pan gated off.
The B_noise allowance keeps the resolver family-agnostic while soundly
covering the one known noisy fragment.

Not introduced: a lilToon-only classifier; a provider registry; a generalized
shader graph; a second UV geometry implementation (the envelope reuses
`ExactUvGeometry`'s rationals and adds members to it); new sampling modes;
UV1+ support; rotation/scrolling support (`_MainTex_ScrollRotate` remains the
cutout frontend's separate exact-zero gate — this design does not touch it);
cutoff-margin support; a new shader family.

## 12. Compatibility impact

- **Poiyomi:** alpha values with channel 0 and non-identity ST now resolve
  instead of refusing; all other outputs unchanged; Poiyomi receives exactly
  the same mapping semantics as lilToon (same resolver, same envelope rules).
  `B_noise` is slack for Poiyomi — an accepted, documented false-negative
  margin at footprint boundaries.
- **lilToon cutout:** same widening; the standing gates (`_MainTex_ScrollRotate`
  exact zero, coverage gates, `_Cutoff` bound) are untouched; the rotate-at-zero
  fragment is modeled inside `B_noise` for non-identity mappings and stays the
  declared baseline abstraction for identity (§6.4).
- **Other semantics** (base color, emission, normal): they do not pass through
  `AlphaSemanticsResolver`; unchanged.
- **Analysis/refusal surface:** the `UnsupportedUvMapping` refusal remains
  defined and reachable (channel ≠ 0); dedup (`DistinctResolutions`) semantics
  unchanged (classified resolutions still never merge).

## 13. Security / privacy / package boundaries

No new capture, no asset reads, no network, no shader compilation, no Census
Lab access: the feature is pure arithmetic over already-captured evidence.
No product/research package boundary is crossed; no `.meta` churn beyond the
one new production file + one new test file pair (metas generated with the
files, treated as one unit). No manifest or lockfile change.

## 14. Acceptance criteria

1. Resolver: channel 0 + any finite ST resolves (no `UnsupportedUvMapping`);
   channel ≠ 0 still refuses; identity parity pinned (identical outcomes and
   identical classifier inputs for identity mappings).
2. Classifier: with zero envelope, outcomes are bit-identical to today's for
   identical inputs (parity suite); with a nonzero envelope, every boundary
   case a runtime rounding could cross is inside the tested domain (boundary
   fixtures below).
3. Transform: E1–E3 tier decisions match the §7 table on boundary fixtures
   (power-of-two, fractional, negative, zero, subnormal-adjacent, degenerate,
   overflow guard).
4. Families: one Poiyomi and one lilToon cutout material with non-identity ST
   classify instead of refusing; ScrollRotate/pan gates still refuse.
5. Animation: singleton non-identity ST re-assertion resolves; non-singleton
   refuses (existing behavior, regression-pinned through the widened path).
6. Preparation: one full-path scenario where ST moves a triangle from
   transparent to provably opaque and the separation plan migrates it; source
   assets bit-unchanged.
7. Full product + research EditMode suites pass; Unity console clean of new
   errors/warnings; no source-asset mutation; nothing outside the declared
   file map changes.

## 15. Falsifiers and counterexamples

Each acceptance criterion is paired with tests that fail under the plausible
wrong implementation (full map in the implementation plan):

- F1 scale ignored / F2 offset ignored: a triangle transparent under identity
  but over opaque texels only after the true transform must flip to
  `ProvenOpaque` (and the mirror image must not flip when it shouldn't).
- F3 negative scale abs()-ed: a mirrored placement whose transparent region is
  the mirror image of the opaque one must classify `MustRemainTransparent`
  under abs-scaling but `ProvenOpaque` under true reflection.
- F4 mip-0-only transform: a chain transparent only at mip ≥ 1 must stay
  `Unknown`/transparent under non-identity ST.
- F5 wrap-after-transform / F6 untransformed footprint / F7 boundary-cell
  loss: Repeat fixtures with transform landing exactly on period boundaries;
  footprint fixtures whose supporting cell lies outside the untransformed
  domain.
- F8 exact-real-for-runtime / F9 double-as-exact / F10 inward rounding: a
  transform whose exact image sits on a texel boundary with a transparent
  texel on the rounded side must not prove (envelope absorbs the rounding;
  the boundary-exact coordinate must classify conservatively).
- F11 overflow/underflow as usable coordinates: ≥ 2^127 guard → `Unknown`;
  subnormal range → envelope-classified, never opacity.
- F12 per-triangle failure promoted: complexity overflow and the overflow
  guard must yield `Unknown`, never material support or opacity.
- F13 identity changed: the identity parity suite (§6.4).
- F14 non-singleton ST admitted: existing refusal pinned through the new path.
- F15 family divergence: the same (mapping, triangle, texture, sampling) must
  classify identically through both frontends' values.
- F16 complexity-as-empty: budget overflow returns `Unknown` outcomes that
  reach the planner as transparency, never as "no candidate, prove all".
- F17 zero-scale-as-missing-UV: E2 point domains classify; they are distinct
  from `MissingUv0`.
- F18 degenerate-hull/degenerate-mesh confusion: a degenerate UV hull with a
  valid mesh classifies; a degenerate mesh stays `Unknown` even with a
  well-formed hull.
- F19 component swap (`_ST.zw` ↔ `.xy` exchanged, or per-axis scale crossed):
  asymmetric fixtures (offset `(0.5, 0.25)`, scale `(2, 3)`) must fail any
  swapped evaluation.
- F20 footprint width scaled by `|s|`: the bilinear footprint is one texel of
  the *texture* in the transformed domain; a fixture whose padded hull only
  reaches a supporting cell at exact one-texel width must not pass under a
  `|s|·texel` footprint.
- F21 inflate-after-normalize: a Repeat fixture whose envelope crosses a
  period boundary must include the boundary cell; normalizing before
  inflating loses it and must fail.
- F22 wider-type-as-proof: computing the transform or envelope in `double`
  without dyadic representability proof must fail the F10 boundary fixture
  (the 2026-08-17 spec's prohibition, restated).
- F23 test-contract rewrite, not deletion: the resolver tests that assert
  today's identity-only refusals for scaled/offset mappings
  (`AlphaSemanticsResolverTests.UnsupportedUvMappingRefuses`) encode the
  product decision this design revises; they are rewritten to the new
  boundary (channel refusal only), never deleted or hollowed.

## 16. Stop conditions

- A boundary test that cannot be made to pass without weakening the envelope
  derivation (i.e., the §6.2 bound proves insufficient under any covered
  platform behavior) — stop, report the counterexample, retain identity-only.
- Evidence that either family's ST is not the affine fragment-stage transform
  the frontends claim — stop; the semantic claim itself is wrong.
- Region-budget behavior under real avatar scales showing pathological
  `Unknown` rates (coverage collapse) — report; do not widen budgets here.
- Any need to touch capture, admission, planner, or apply semantics to make
  ST work — out of scope; stop and return to the controller.

## 17. Explicit non-goals

UV channels 1–3; `_MainTex_ScrollRotate` (scroll/rotate); parallax-aware
domains; trilinear/anisotropic/mirrored filtering; differing wrapU/wrapV;
cutoff-margin proofs; UDIM/IDMask-aware proofs; vertex-stage UV transforms;
generalized texture-transform IRs; per-family envelope constants; widening
`MaxSupportRegions`; changing capture, admission, planner, apply, or any
frontend gate.

## 18. Remaining open questions

None blocking. Recorded for the controller: (a) `B_noise` is sized by the
D3D11.3 `sincos` bound; if the controller later wants the slack reclaimed for
Poiyomi, that is a deliberate family-agnostic-to-family-coupled architecture
change and must be re-approved; (b) the identity baseline's un-modeled
rotate-at-zero noise remains a declared abstraction — closing it would change
identity outcomes and is out of scope by parity.

---

## Appendix A — pinned-source digests used by this design

`[MEASURED]` — fetched from `https://raw.githubusercontent.com/lilxyzw/lilToon/2.3.4/`
(tag `2.3.4`, commit `252fd8cfc46106d4967e95b3f2c788418502f227` per B2 §2):

```
96b1bbfecc32d16735db16b5a0c46db3bf81c8f28b9d247c3394ae3c6af84dc1  Shader/Includes/lil_common_frag.hlsl
daee7c7dc133d85eb8096fe465e208d21361a4e6a570af1b2fe37c8b7bd296ed  Shader/Includes/lil_common_functions.hlsl
9863c86c76682c5132ec04937977c96d22a12979b79527b698771214999ed9e0  Shader/Includes/lil_common_input.hlsl
49b4c364f1bd2f46a4dcb34921512c13473c03abb055428ee4da19dcce461802  Shader/Includes/lil_common_macro.hlsl
19c7764d77ad29f14f62b3e4e7458f6c30b9e518cc875d86354dacb82560c6ed  Shader/Includes/lil_common_vert.hlsl
```

`ltspass_cutout.shader` fetched for the `LIL_FEATURE_ANIMATE_MAIN_UV`
verification (line 645); digest not pinned by B2 and re-fetched at
implementation time if needed.
