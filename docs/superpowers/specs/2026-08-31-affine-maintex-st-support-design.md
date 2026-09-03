# Affine `_MainTex_ST` support — design specification

Status: design (no production or test code). Branch:
`design/affine-maintex-st-support` from `main` at `89cc5be` (merge of PR #41).

This design supersedes nothing. It extends the deferred obligation recorded in
`docs/superpowers/specs/2026-08-17-alpha-semantics-resolver-design.md` ("UV
mapping", follow-up paragraph) and in the cutout plan
(`docs/superpowers/plans/2026-08-30-liltoon-cutout-opaque-conversion.md`,
"Follow-ups").

The design does not rewrite the historical identity-ST records.
It references them where it reuses their facts.

Revision 3 (2026-09-01) repairs the accepted-in-principle design in six places.
The exact-tier predicate is now path-independent (§7 Lemma P). The family
compatibility boundary now rests on source evidence, not on an adopted
cross-platform trig assumption (§3.2, §5-G, §11).

The arithmetic envelopes now compose over the runtime domain, not over the
ideal one (§6.2). The soundness proof carries every term (§6.3). Plan §Tasks
now orders the tasks so every step compiles.

The identity short-circuit
contract now specifies field-level bit equality (§6.4).

The repair narrows the lilToon half of the original coverage claim. §11 states
the resulting matrix.

Labels: `[SOURCE]` read from pinned code (AMUSE checkout, or official vendor
source fetched and hashed for this design, file digests in Appendix A).
`[MEASURED]` observed from executed commands. `[SPEC]` verified from a primary
API/architecture specification. `[INFERENCE]` bounded conclusion from those
facts. `[DECISION]` a controller-owned choice made here.

## 1. Problem and user-visible coverage gain

`AlphaSemanticsResolver.IsSupportedMapping`
(`Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:338-345`)
admits only channel 0 with `Scale == (1,1)` and `Offset == (0,0)`. Every
material whose `_MainTex` uses a tiling scale or an atlas offset therefore
loses its alpha proof entirely.

The resolver refuses the resolution (`UnsupportedUvMapping`), every triangle
of the submesh classifies `Unknown`, and no triangle ever reaches
proven-opaque conversion.

Non-identity `_MainTex_ST` is common on avatars: power-of-two tiling (hair,
fabric), half/fractional atlas rectangles, and mirrored placements.

The coverage gain: **materials whose attested sampler coordinate is exactly
the binary32 affine image of the interpolated UV** keep the full exact
per-triangle alpha proof. Triangles whose transformed domain lies entirely
over opaque texels in every mip become `ProvenOpaque` and convertible instead
of unconditionally transparent.

Today that set is Poiyomi Toon 9.3.64 with the gates its frontend already
enforces (§3.2). lilToon 2.3.4 cutout keeps its shipped identity-only
coverage, because its fragment applies an unbounded zero-angle rotation round
trip after the affine step (§3.1, §5-G, §11).

The risk that bounds the design: the classifier receives binary32 triangle UVs
and the GPU evaluates `uv * scale + offset` per fragment in binary32. A naive
transform can round the analyzed domain inward at a texel or wrap boundary,
omit a transparent texel, and produce a false `ProvenOpaque`.

That is the
worst kind of false positive (invisible geometry materialized). The
controlling invariant:

> Increasing arithmetic uncertainty must never increase the chance of
> `ProvenOpaque`.

## 2. Current behavior and exact refusal point

Data flow today (`[SOURCE]`):

1. Capture. The capture step requests `_MainTex` with
   `TextureEvidenceKinds.ScaleOffset`. `UnityMaterialEvidenceCapture` stores
   `material.GetTextureScale/Offset` as `Vector2`s on the texture assignment
   (`UnityMaterialEvidenceCapture.cs:843-856`).

   The `UvMapping` constructor
   (`MaterialSemantics.cs:47-60`) validates the scale/offset and throws on
   NaN/±∞, so a non-finite mapping cannot reach the resolver.

2. Animation admission. A derived `material._MainTex_ST` binding is
   proof-relevant (`UnityAnimationEvidenceCapture.DeriveTextureScaleOffsetProperties`,
   `:480-495`, suffix `_ST` at `:95`).

   Only an exact singleton equal to the
   captured default of the material enters the slot
   (`AdmittedMaterialStates.cs:418-472`).

   Non-singleton refuses the slot (`AnimatedMaterialPropertyNotSingleton`).
   Non-finite refuses (`UnsupportedAnimationCurveForm`).

3. Semantic value. Both family frontends already construct non-identity
   `UvMapping`s without gating ST. The lilToon cutout frontend builds
   `new UvMapping(0, assignment.Scale, assignment.Offset)`
   (`LilToonCutoutMaterialSemantics.cs:330`).

   The Poiyomi alpha path builds
   `new UvMapping(channel, assignment.Scale, assignment.Offset)`
   (`PoiyomiMaterialSemantics.cs:601-603`).

   Neither frontend gates the values. Only the resolver does. The lilToon
   cutout frontend comment (`LilToonCutoutMaterialSemantics.cs:284-291`)
   explicitly delegates the refusal to the resolver. §11 revokes exactly that
   delegation for lilToon.

4. Refusal point. `AlphaSemanticsResolver.ResolveSampled` calls
   `IsSupportedMapping(sample.Coordinates)` and refuses with
   `UnsupportedUvMapping` before evidence lookup
   (`AlphaSemanticsResolver.cs:300-325, 338-345`).

   A refused resolution
   produces `Unknown` for every triangle downstream
   (`UnityRendererAlphaAnalysis.cs:618-633`).

   Analysis completes, but the pipeline proves nothing. [MEASURED against
   current checkout: the gate is the sole place that requires identity.
   Capture, admission, and both frontends already carry arbitrary finite ST.]

The classifier itself (`TriangleAlphaClassifier.Classify`,
`TriangleAlphaClassifier.cs:175-232`) takes only `(triangle, texture,
sampling)`. It decodes the supplied binary32 corner UVs exactly
(`ExactUvGeometry.DecodeFloat`).

It treats their closed convex hull in texel
units as the sampled domain (`CreateTextureScaledDomain`,
`ExactUvGeometry.cs:227-262`).

It proves per mode (Point/Bilinear × Clamp/Repeat) that no footprint of a
non-opaque texel intersects the domain, with a per-level region budget
(`MaxSupportRegions = 65536` → `Unknown`). `AlphaResolution.Classify`
(`AlphaSemanticsResolver.cs:165-204`) runs the conjunction over the full mip
chain. `MustRemainTransparent` absorbs, `Unknown` propagates, and
`ProvenOpaque` requires every level to prove.

## 3. Verified shader and runtime arithmetic facts

### 3.1 lilToon 2.3.4 cutout: affine plus an unbounded fragment round trip

`[SOURCE]`: fetched from the official `lilxyzw/lilToon` tag `2.3.4`
(commit `252fd8cfc46106d4967e95b3f2c788418502f227` per B2 §2). File digests
are in Appendix A.

- The cutout pass unconditionally defines `LIL_FEATURE_ANIMATE_MAIN_UV`
  (`Shader/ltspass_cutout.shader:645`), so the main-UV macro of the fragment
  picks the time-variant arm (`Shader/Includes/lil_common_frag.hlsl:260-263`):
  `fd.uvMain = lilCalcDoubleSideUV(fd.uv0, fd.facing, _ShiftBackfaceUV);`
  `fd.uvMain = lilCalcUV(fd.uvMain, _MainTex_ST, _MainTex_ScrollRotate);`

- `fd.uv0` is the interpolated raw UV0 (`lil_common_vert.hlsl:186`:
  `LIL_V2F_OUT_BASE.uv0 = input.uv0;`).

  The ST transform is a
  **fragment-stage** expression applied after rasterizer interpolation. (The
  vertex stage computes a separate `uvMain` at `lil_common_vert.hlsl:98` for
  vertex purposes. The main sample uses the fragment value.)

- `lilCalcUV` (`lil_common_functions.hlsl:453-465`):
  `outuv = uv * uv_st.xy + uv_st.zw;` then
  `lilRotateUV(outuv, uv_sr.z + uv_sr.w * LIL_TIME) + frac(uv_sr.xy * LIL_TIME)`.
  With `_MainTex_ScrollRotate` exactly `(0,0,0,0)` (a standing gate) both time
  terms vanish and the angle is exactly `+0`.

  The gate check reads **per binary32 component**
  (`x != 0f || y != 0f || z != 0f || w != 0f`).

  The `Vector4 ==`/`!=`
  operators of Unity are epsilon-based (equal when the squared distance is
  under `0.00001f * 0.00001f`) and are **prohibited** for this proof, because
  a near-zero `z` is a real static rotation and a near-zero `x`/`y`/`w` is a
  real time-varying term.

- `lilRotateUV(uv, 0)` (`lil_common_functions.hlsl:424-437`) has **no
  zero-angle early-out**: it evaluates `sincos(0, si, co)`,
  `outuv = uv - 0.5`, rotates by the matrix `[co −si; si co]`, and adds
  `0.5`. So even at the gated identity the executed expression is
  `fl(fl(fl(t−0.5)·co − fl(t'−0.5)·si) + 0.5)` per axis, not the identity.

- `_ShiftBackfaceUV == 0` (a standing gate) makes `lilCalcDoubleSideUV` return
  `uv` unchanged with no arithmetic (`lil_common_functions.hlsl:467-470`).

- The main sample then reads `_MainTex` at `fd.uvMain`
  (`lil_common_frag.hlsl:311-312`, `LIL_SAMPLE_2D_POM`). The gradient-sample
  footprint of the POM macro equals a normal sample at `_UseParallax == 0`.

Consequence: the sampler coordinate of lilToon is **not** the affine result.
It is the affine result pushed through a rotation. Only the `sin`/`cos`
accuracy of the runtime at angle `+0` bounds that displacement, scaled by the
coordinate magnitude.

§3.3-A4 shows that no absolute bound for that term is citable on every
runtime target that the AMUSE contract covers. §5-G decides what follows. The
identity slice already ships on top of this un-modeled term as a declared
abstraction (C6). This design does not extend coverage on top of it.

### 3.2 Poiyomi Toon 9.3.64: affine plus a provably zero-displacement add

`[MEASURED]`: the pinned source that AMUSE attests is public, and AMUSE
fetched it for this design:
`_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader` at
`poiyomi/PoiyomiToonShader` commit
`e125e1c33cbfb860f59330799dd4d10a1097242d` (the commit named in
`PoiyomiMaterialSemantics.cs:25`).

Its `.shader.meta` carries `guid: 9444ce77bf4418748b1e8591b9d97f85`, equal to
`PoiyomiMaterialSemantics.CanonicalShaderGuid`. Its SHA-256 is
`31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755`, equal to
`PoiyomiMaterialSemantics.CanonicalNormalizedSourceHash`.

The file has LF endings and no BOM, so the frontend normalization is the
identity on it. The bytes read below are therefore exactly the bytes that the
frontend attestation admits. Any content change fails that attestation closed.

`[SOURCE]`: the 9.x shader generator emits one copy of the fragment body per
pass. Line numbers name the first copy. The cited lines are textually
identical in the later copies (`mainUV` at `29132`/`46233`/`57486`, the main
sample at `29139`/`46240`/`57493`).

- Raw interpolated UV sets: `poiMesh.uv[0..3] = i.uv[0].xy / i.uv[0].zw /
  i.uv[1].xy / i.uv[1].zw` (`:29065-29068`). No arithmetic.

- The affine step is a named function with no other term:
  `float2 poiUV(float2 uv, float4 tex_st) { return uv * tex_st.xy + tex_st.zw; }`
  (`:6544-6547`).

- Main coordinate: `float2 mainUV = poiUV(poiMesh.uv[_MainTexUV].xy,
  _MainTex_ST);` (`:29132`).

- Post-affine arithmetic, in order:

  - `if (_MainPixelMode) { mainUV = sharpSample(_MainTex_TexelSize, mainUV); }`
    (`:29134-29137`), a UV quantization, **not** affine.

  - `float4 mainTexture = POI2D_SAMPLER_PAN_STOCHASTIC(_MainTex, _MainTex,
    mainUV, _MainTexPan, _MainTexStochastic);` (`:29139`). Here
    `POI2D_SAMPLER_PAN_STOCHASTIC` selects a stochastic-tiling sampler when
    `useStochastic` is active and otherwise `POI2D_SAMPLER_PAN`
    (`:18882`, `:18889`, `:18899`).

    It expands through `#define POI2D_SAMPLER_PAN(tex, texSampler, uv, pan)
    (UNITY_SAMPLE_TEX2D_SAMPLER(tex, texSampler, POI_PAN_UV(uv, pan)))` with
    `#define POI_PAN_UV(uv, pan) (uv + _Time.x * pan)` (`:4890-4891`).

- The only writer of `poiMesh.uv[...]` before the main sample is parallax
  (`:20020-20029`, `poiMesh.uv[_ParallaxUV] = parallaxUV`).

`[SOURCE]`: the AMUSE frontend already gates every one of those. It proves
`MainSamplingModeGates = { "_MainPixelMode", "_MainTexStochastic" }` exactly
zero before any `_MainTex` claim (`PoiyomiMaterialSemantics.cs:98-102`,
`:444-453`, `:512-522`).

It proves `_MainTexPan` exactly zero in all four
components, and it proves the UV channel an exact integer in `0..3`
(`:567-604`).

It proves `TextureBackedAlphaGates = { "_PoiParallax" }` plus
`"_PoiInternalParallax"` off immediately before a texture-backed `_MainTex`
alpha claim (`:197-205`, `:142-143`, `:235-236`).

`[INFERENCE, load-bearing]` Under those gates the sampler coordinate is
`fl(mainUV + fl(_Time.x · 0))` per axis. `_Time.x` is finite, so
`_Time.x · 0 = ±0` exactly under every admitted rounding and flush behavior
(§3.3), and `x + (±0) = x` exactly for every finite `x` (only the sign of a
zero result can differ, and ±0 decode identically in
`ExactUvGeometry.DecodeFloat` and sample identically).

Contraction or reassociation cannot change this: fusing gives
`fma(_Time.x, 0, mainUV) = mainUV`, and re-associating the pan term into the
affine expression gives `uv·s + (o + ±0) = uv·s + o`. Therefore **the
post-affine fragment displacement is exactly zero for Poiyomi**, not merely
bounded.

The family-fragment term of §6.2 is `0` for this consumer by proof
from the attested source.

### 3.3 GPU binary32 arithmetic guarantees

- binary32: 24-bit significand (incl. implicit bit), normal exponent range
  2^-126…(2−2^-23)·2^127, subnormals down to 2^-149, `ulp(x) = 2^(e-23)` for
  `|x| ∈ [2^e, 2^(e+1))`, hence `ulp(x) ≤ 2^-23·|x|` for normal x.
  [SPEC: IEEE 754-2019 representation.]

**A1: correctly rounded add/multiply, exact when the result is
representable.** Each admitted `mul`/`add`/`fma` returns the infinitely
precise result of that operation rounded to a binary32 value by *some*
rounding function. Rounding functions are the identity on representable
values.

- Direct3D 11.3 functional spec §3.1: 32-bit shader `add`/`sub`/`mul` must be
  within **0.5 ULP** of the infinitely precise result (ties permitted either
  way), and **fused** `mad` must be at least as accurate as the worst
  permitted serial expansion.

  A representable exact result has no other float
  within 0.5 ULP, so the operation is exact on it. [SPEC]
  (`https://microsoft.github.io/DirectX-Specs/d3d/archive/D3D11_3_FunctionalSpec.htm`).

- Vulkan/SPIR-V environment appendix, Table 3 "Precision of Core SPIR-V
  Instructions": `OpFAdd`, `OpFSub`, `OpFMul`, `OpFmaKHR` — "Correctly
  rounded." And the definition of correct rounding under an
  implementation-defined mode is explicit: *"will return the infinitely precise
  result, x, rounded so as to be representable in floating-point. **If x is
  exactly representable then x will be returned.**"* [SPEC, quoted]
  (`https://docs.vulkan.org/spec/latest/appendices/spirvenv.html`).
- GLSL ES 3.20 §4.7.1: *"For single precision operations, precisions are
  required as follows: `a + b`, `a - b`, `a * b` — Correctly rounded."* [SPEC,
  quoted]
  (`https://registry.khronos.org/OpenGL/specs/es/3.2/GLSL_ES_Specification_3.20.html`).

- Metal Shading Language Specification: Metal adheres to IEEE 754. The spec
  states add/multiply accuracy in ULP against the infinitely precise result.
  The compiler allows contraction of `a*b+c` into a single FMA by default.
  Fast-math also assumes no NaN/Inf/signed zero and allows reassociation.

  [SPEC]
  (`https://developer.apple.com/metal/Metal-Shading-Language-Specification.pdf`).

**A2: flush behavior is unconstrained.** On every target, subnormal
*operands* may enter arithmetic as zero (DAZ), and subnormal *results* may
flush to zero (FTZ). Vulkan makes `DenormPreserve` an optional execution
mode. Metal permits denormal flushing, and D3D11 does not forbid it.

AMUSE assumes DAZ and FTZ always on and may never assume denormal
preservation, a specific rounding mode, or extra intermediate precision.
[SPEC + INFERENCE, load-bearing]

**A3: admitted evaluation paths for the affine expression.** The shader
source expression is a single multiply followed by a single add per axis. Its
admitted compilations are therefore exactly: **serial** (two
correctly-rounded operations) and **fused** (one correctly-rounded operation
on the exact `u·s + o`), each with A2 flushes.

None of the four specs above admits division-introducing algebraic rewrites
such as `s·(u + o/s)`. The one additional term that any admitted consumer
applies (the pan add of Poiyomi) is exactly zero and is therefore invariant
under reassociation (§3.2). [SPEC for contraction, DECISION to declare the
path set, stop condition S3 if contradicted.]

**A4: no target-independent bound exists for trig accuracy at angle zero.**
D3D11.3 specifies `sincos` maximum absolute error **0.0008** for inputs in
`[−100π, +100π]`. Vulkan Table 4 and the GLSL ES built-in table specify
`sin`/`cos` as an *absolute* error inside the principal range.

The Metal specification states math-function accuracy in **ULP relative to
the infinitely precise result**, which does not bound the absolute error of a
result whose exact value is zero. MSL fast math explicitly relaxes IEEE
conformance for such functions.

Consequently no absolute bound on `|sin(+0)|` and `|cos(+0) − 1|` is citable
across all four targets, and AMUSE cannot restrict the runtime target of an
uploaded avatar (the same avatar runs on D3D11, Vulkan, GLES, and Metal
viewers). [SPEC for the first three, INFERENCE for the Metal gap.] This is
the fact §5-G decides on.

### 3.4 Interpolation

Perspective-correct interpolation of a vertex attribute is, per fragment, a
convex combination (positive weights summing to one) of the three corner
values. Every hardware path (direct or perspective-divided) yields a value
within the closed convex hull of the corners.

No cross-vendor spec that AMUSE
can cite pins the precision of that interpolation. [SPEC for convexity,
INFERENCE for the precision vacuum.]

The existing classifier already declares the resulting model: the sampled
domain is the exact hull of the decoded binary32 corners, so AMUSE idealizes
interpolation as exact-linear onto binary32 values inside the hull. Identity
ST ships on this idealization today.

This design changes neither its level
nor its role (§4).

## 4. Declared product arithmetic contract

This section makes the previously implicit modeling explicit and states the
new clauses. It is the contract the proof in §6.3 consumes.

C1. **Corner decode.** Triangle UVs reach analysis as binary32. Analysis
decodes them exactly (dyadic). No rounding ever enters the arithmetic of
AMUSE.

C2. **Hull model (pre-existing, now written down).** The pre-transform
coordinate of every fragment is a binary32 value inside the closed convex
hull of the three decoded corner values. AMUSE does not model the deviation
between the hardware-interpolated value and the exact-linear value.

This is
the idealization behind identity ST since the identity slice. Its magnitude
is relative (≈½ ulp of the coordinate), and this design does not change it.

C3. **Transform expression.** The sampler input is the binary32 evaluation
of the affine expression `t = uv·s + o` per axis, evaluated along one of the
admitted paths of A3, optionally followed by family-specific fragment
arithmetic. Any such displacement must carry a bound as a function of the
**runtime** coordinate magnitude (§6.2).

For the admitted consumer set that
displacement is exactly zero (§3.2).

C4. **Coordinate-fidelity emission invariant (new).** A semantic frontend may
emit a non-identity `UvMapping` for an alpha-relevant `TextureSample` only
when its attested source proves the sampler coordinate is the binary32
affine image of the interpolated UV of that channel, with no further
fragment arithmetic of unbounded displacement.

A frontend whose source applies such arithmetic must refuse the non-identity
mapping itself, in its own vocabulary, with its own diagnostic. The resolver
consumes mappings and never branches on family identity (§11). [DECISION]

C5. **Two-tier modeling (new).** For an admitted non-identity mapping the
resolver classifies either:

  - **Exact tier**: when every admitted evaluation path provably returns the
    exact real affine image for every binary32 value in the hull (Lemma P and
    the E1/E2/E3 predicates of §7), the modeled domain is the exact affine
    image of the hull, represented exactly (the transformed corners are
    binary32-representable), and the envelope is **zero**.

  - **Envelope tier**: otherwise the modeled domain is the exact affine
    image, corner-encoded to nearest binary32, inflated by an exact dyadic
    outward envelope that bounds every admitted evaluation result and the
    family fragment (§6.2). Every boundary the runtime could cross is inside
    the classified domain.

For the identity mapping the pipeline short-circuits before tier selection
(§6.4). Inputs, envelope, and outcomes match today byte for byte.

C6. **Baseline clause.** The identity slice absorbs un-modeled family
fragment noise (the rotate-at-zero round trip of lilToon) without modeling
it. That is shipped behavior, unchanged.

No newly admitted transform proves
with weaker modeling than that baseline: every non-identity admission either
models the expression exactly with a proven-zero fragment term, or models it
with the outward envelope of §6.2.

C7. **Monotone safety direction.** Modeled domains only ever widen relative
to the idealized model, widening can only preserve or weaken `ProvenOpaque`
(structural property, §6.3 step 5), so increased arithmetic uncertainty can
never increase the chance of `ProvenOpaque`. This discharges the controlling
invariant by construction, not by assumption.

## 5. Alternatives considered

### A. Exactly representable transformed vertices

Compute `uv·s + o` in exact arithmetic per corner. Admit when every result is
exactly representable as binary32. Feed the transformed floats to the
unchanged classifier.

Verdict: **necessary but insufficient as stated, rejected as the mechanism.**
`[INFERENCE]` Corner representability constrains nothing about the rounding
of the expression at interior coordinates. The runtime evaluates the
expression at every interpolated binary32 `u`, and a corner-exact transform
can still round interior samples across a texel or wrap boundary.

The classifier would then prove an exact statement about a domain that
excludes realizable sample points: exactly the inward-rounding false positive
that the problem statement bans.

Strengthened to "exact on the whole hull, on every admitted path" the
criterion becomes decidable and sound. That strengthening is kept as the
exact tier inside the selected design (§7).

Alone it covers only power-of-two
scales with zero offset (plus degenerate axes and zero scale), which forfeits
the offset half of the coverage goal.

### B. Exact-rational transformed hull (no rounding model)

Carry `UvMapping` into exact geometry, apply scale/offset to the exact domain,
classify the rational hull directly.

Verdict: **rejected.** `[INFERENCE]` This models the ideal real-number
expression, not the domain the shader samples. The runtime rounds per
fragment, and at coordinates whose exact image sits on a cell or wrap
boundary the rounded sample can land on the far side.

An exact model of the
wrong domain is unsound.

Making it "sound" by declaring expression rounding away would admit a *new*
unmodeled uncertainty together with new coverage, a direct violation of C7.
It is approach C with the envelope forced to zero.

### C. Conservative arithmetic envelope — **selected**

Bound every admitted binary32 evaluation result outward with an exact dyadic
envelope and classify the union (the inflated exact image).

Feasible with the
existing machinery: the classifier is coordinate-set-based (hull + clip +
interval intersection), so inflating the hull before mode dispatch preserves
the semantics of every mode (Point/Bilinear footprints, Clamp/Repeat
normalization, all-mip conjunction, region budget).

Costs: one exact-hull expansion, one dyadic bound derivation, and a one-cell
caution ring at footprint boundaries for envelope-tier triangles. Sound by
construction and covers the full useful subset (§7). Selected.

### D. Narrower supported subset (power-of-two scales, zero offset)

Admit only `s ∈ ±2^k`, `o = 0` (and the degenerate/zero-scale companions).

Verdict: **rejected as the architecture, retained as the exact tier.** The
proof is trivial and the machinery minimal, but it forfeits every
atlas-rectangle material: scale-with-offset, the single most common
non-identity form. Its admission predicate survives as the exact tier of the
selected design (§7 E1).

### E. Per-target trig bound for the lilToon fragment (option "prove every target")

Establish an absolute bound on `|sin(+0)|` and `|cos(+0) − 1|` for every runtime
target the optimization contract covers, compose it with the runtime magnitude
bound (§6.2), and admit lilToon non-identity ST with that term.

Verdict: **rejected, not citable today.** `[SPEC + INFERENCE, §3.3-A4]`
D3D11.3 (0.0008), Vulkan, and GLSL ES all publish absolute error bounds
inside the principal range. The Metal specification states trig accuracy in
ULP relative to the infinitely precise result, which does not bound absolute
error when that result is zero.

MSL fast math explicitly relaxes conformance
for those functions.

Adopting the constant of one API for the others, as revision 2 of this design
did, is an assumption dressed as a bound, and it is load-bearing for a
false-positive-critical proof. Re-openable: §11 records the exact evidence
that would make this option available.

### F. Restrict non-identity lilToon support to targets with an established bound

Admit lilToon non-identity ST only on runtime targets whose trig bound is
citable (D3D11, Vulkan, GLES) and refuse on the rest.

Verdict: **rejected, unenforceable.** `[INFERENCE]` AMUSE runs at build time
on the machine of the author and produces one uploaded avatar that later
renders on whatever viewer loads it, including Metal. Analysis has no
evidence about the runtime API, and no NDMF, VRChat, or Unity build-time fact
pins it.

A boundary that analysis cannot enforce is not a boundary.

### G. Admit only consumers whose attested coordinate is the affine result — **selected**

Grant non-identity ST coverage exactly where the pinned source proves the
sampler coordinate is the binary32 affine image with no further unbounded
fragment arithmetic, and keep the refusal in the frontend that owns the
opposite fact.

Verdict: **selected.** `[DECISION]` Grounding: the coordinate of Poiyomi
9.3.64 is `poiUV(uv, _MainTex_ST) + _Time.x · 0`, whose extra term is exactly
zero under every admitted path, verified by reading the exact attested bytes
(§3.2).

The family-fragment term of §6.2 is then `0` by proof rather than by
adopted constant, and the envelope contains only expression terms whose
derivation rests on A1/A2 alone.

lilToon 2.3.4 cutout keeps its shipped identity-only coverage and refuses
non-identity ST in its own frontend with its existing `UnsupportedUv`
diagnostic vocabulary, which is where the shader-specific fact lives.

Cost: this design defers the lilToon half of the original coverage claim.
That half was never sound as specified. No currently-supported material loses
coverage: the resolver gate refuses lilToon non-identity ST today, and the
frontend gate refuses it after this change. §11 states the matrix. §12 states
the compatibility impact.

## 6. Selected design and proof argument

### 6.1 Mechanism

1. `AlphaResolution` carries the `UvMapping`. Factory `Classified` gains one
   parameter. The resolver makes one other change: `IsSupportedMapping`
   narrows to the channel test (§8).

2. `AlphaResolution.Classify(triangle)` computes, once per triangle:

   - the **identity short-circuit first**, before any tier logic:
     `s = (1,1), o = (0,0)` yields the original triangle with a zero envelope
     and never calls the transform helper. This ordering is load-bearing, not
     an optimization.

     An identity hull that contains `0` or a subnormal value
     fails the normality guard of E1 and would otherwise fall to the envelope
     tier and change shipped identity outcomes (§6.4, F13).

   - otherwise, per axis: the tier decision and, in the envelope tier, the
     dyadic envelope (new pure helper `AffineUvTransform`, §7).

   - the transformed corner `Vector2`s. Exact tier: exact products, which the
     tier predicate makes representable. Envelope tier: exact products
     encoded to nearest binary32.

3. `TriangleAlphaClassifier.Classify` gains a fourth parameter: the per-axis
   UV-space envelope as an exact rational pair (`AlphaUvEnvelope`, `Zero` for
   identity and for the exact tier).

   `ExactUvGeometry.CreateTextureScaledDomain` gains the same parameter and
   inflates the hull after construction by `(ex·width·T, ey·height·T)` where
   `T = domain.TexelScale` is the common power-of-two texel denominator of
   the domain. Cell `i` occupies `[i·T, (i+1)·T)`, so a UV displacement `ex`
   displaces the domain by `ex·width·T`, not `ex·width`.

   The implementation builds the inflation as the convex hull of every domain
   vertex offset by `(±ex, ±ey)`, a small exact monotone-chain hull added to
   `ExactUvGeometry`. The hull is the exact Minkowski sum of the domain with
   the envelope box.

   A single-point domain becomes its rectangle, an axis-aligned segment
   becomes its rectangle, and a diagonal segment becomes the exact hexagon,
   tighter than a bounding rectangle and therefore conservative in the safe
   direction. A zero-envelope early-out returns the domain object unchanged,
   so identity parity is structural, not incidental.

   Every downstream step (mode dispatch, candidate ranges, clip,
   `NormalizeRepeat`, budget) stays unchanged.

   The implementation inflates before Repeat normalization, but the ordering
   is not load-bearing. `NormalizeRepeat` translates a domain by an exact
   integer number of periods, and `OutwardExpand` commutes with translation,
   so `NormalizeRepeat(Expand(D))` and `Expand(NormalizeRepeat(D))` differ
   only by an outcome-neutral integer-period shift.

   Candidate ranges shift by whole texture dimensions, the candidate count
   stays the same, `FloorMod` selects the same wrapped texels, and
   domain/cell intersections translate covariantly. This reconciles §6.1 with
   the Repeat row of the §9 boundary table.

   The design withdraws falsifiers
   F5, F7, and F21 on that basis (§15). All callers migrate (clean cutover,
   no overload shim).

4. Overflow guard: if the exact `|u·s|` or `|u·s + o|` of any corner reaches
   2^127 (dyadic corner check, with 2^127 a conservative stand-in for the
   binary32 maximum finite value `(2−2^-23)·2^127`), the triangle returns
   `Unknown`. The runtime could produce ±∞, which no envelope contains.

   In
   the exact tier the "inside the admitted finite range" condition means
   `< 2^127`, the same constant.

5. Frontend boundary (C4):
   `LilToonCutoutMaterialSemantics.InterpretCutoutAlpha` gains one gate. A
   non-identity `_MainTex` scale/offset refuses the alpha output with
   `LilToonSemanticDiagnosticCode.UnsupportedUv` naming `_MainTex_ST`, in the
   same shape as the existing `_MainTex_ScrollRotate` gate.

   **"Same shape" means placement and diagnostic behavior only, not Unity
   vector equality.** The gate checks `_MainTex_ST` identity **per component
   with exact binary32 comparisons** (`Scale.x != 1f || Scale.y != 1f ||
   Offset.x != 0f || Offset.y != 0f`).

   Epsilon-based `Vector2 ==`/`!=` is **prohibited** at this C4 boundary. It
   treats vectors within `1e-5` as equal, so a near-identity ST would bypass
   the gate while the exact identity test of the resolver classifies it as
   non-identity, admitting an unattested lilToon coordinate into the
   family-blind affine path.

   `-0.0f` remains admitted (`-0.0f != 0f` is false, and ±0 are equivalent
   for this coordinate model).

   The design scopes the gate to the cutout **alpha** interpretation, the
   only lilToon path that feeds `AlphaSemanticsResolver`
   (`LilToonCutoutMaterialSemantics.cs:330-339` is the sole lilToon
   alpha-sample emitter). Base color, emission, and normal mappings stay
   untouched.

   The same exactness rule governs the sibling proof gates in the lilToon and
   Poiyomi semantics: the `_MainTex_ScrollRotate` and
   `_EmissionMap_ScrollRotate` zero gates, the `_MainTexHSVG` identity gate,
   and the unit-tint simplification all compare per binary32 component.

   The
   aggregate `Vector3`/`Vector4` equality of Unity is epsilon-based, and the
   design prohibits it for every semantic proof decision.

### 6.2 Envelope derivation and composition

All quantities are exact dyadic/rational computations on the decoded corner
values, per axis (corner index `i`, axis coordinate `u_i`, scale `s`, offset
`o`). Each term below is an **independent additive bound**. No term relies on
the slack of another term.

Exact quantities (corner maxima suffice because `|u·s|` and `|u·s + o|` are
convex in `u`, so each maximum over the triangle occurs at a corner, and a
per-axis affine map sends the hull exactly onto the hull of the corner
images):

- `t_i = u_i·s + o` (exact).
- `P = max_i |u_i·s|`: the **pre-cancellation product magnitude**.
- `M_exact = max_i |t_i|`: the magnitude of the exact affine image.
- `t̃_i` = `t_i` encoded to nearest binary32 (the classifier consumes
  binary32 corners).

Envelope terms:

- `B_enc = max_i |t_i − t̃_i| ≤ max_i ½·ulp(t̃_i)`: rational→float
  **encoding** of the modeled corners. Direction-free: the envelope absorbs
  either rounding direction. This is a modeling term, not a runtime
  displacement.

  It is zero in the exact tier because the tier predicate makes
  every `t_i` representable.

- `B_st = 2^-22·(P + M_exact) + 2^-125`: **expression rounding and result
  flush.** Serial path: `e₁ = fl(u·s) − u·s` with `|e₁| ≤ 2^-23·|u·s|`, then
  `e₂` from the addition with `|e₂| ≤ 2^-23·(|t| + |e₁|)`, so
  `|e₁| + |e₂| ≤ 2^-23|u·s| + 2^-23|t| + 2^-46|u·s| ≤ 2^-22(|u·s| + |t|)`.
  Fused path: one rounding, `≤ 2^-23·|t|`, which is smaller.

  Under A1 the
  1-ulp form is valid for any admitted rounding direction (the true bound is
  ½ ulp).

  The `2^-125` floor covers the two possible **result** flushes (A2): a
  subnormal `fl(u·s)` flushed to zero displaces by `< 2^-126`, and a
  subnormal final sum flushed to zero displaces by `< 2^-126`. Both
  propagate through the addition with gain one, so
  `< 2·2^-126 = 2^-125`.

- `B_daz = 2^-126·(|s| + max_i |u_i| + 1)`: **input-operand flush (DAZ)
  amplification** (A2), one independent sub-term per operand. A flushed
  subnormal `u` displaces the product by `|u·s| ≤ 2^-126·|s|`. A flushed
  subnormal `s` displaces it by `≤ 2^-126·max_i|u_i|`.

  A flushed subnormal
  `o` displaces the sum by `|o| < 2^-126`.

  Unconditional and negligible for sane scales, but not implied by either
  the relative term or the result-flush floor (a subnormal `u` with
  `|s| ≈ 2^127` displaces by ≈ 2).

- `B_expression = B_enc + B_st + B_daz` (envelope tier), `0` in the exact
  tier.

Runtime magnitude bound (the term revision 2 got wrong):

- `M_runtime = M_exact + B_st + B_daz`. For every reachable fragment the
  runtime affine result `rt` satisfies `|rt| ≤ |t(x)| + |rt − t(x)| ≤
  M_exact + B_st + B_daz`. `B_enc` is deliberately absent: it describes the
  modeled corners, not any runtime value.

  **This is not `M_exact`.** `M_runtime > M_exact` on every envelope-tier
  axis. `B_st` (hence the pre-cancellation `P`) and `B_daz` drive the
  excess, and neither is a function of `M_exact`. Two consequences, both
  pinned by F25:

  1. The design must evaluate a magnitude-scaled fragment term at
     `M_runtime`. Revision 2 evaluated it at `max_i|t_exact,i|`.

     That
     under-bounds the true operand magnitude of the fragment by the factor
     `M_runtime / M_exact`, ≈ 3 on fixture B of F25, and always in the
     unsound direction.

  2. The design must derive the expression term itself from `P`, not from
     `M_exact`. The discrepancy is unbounded in the cancellation family:
     with the adjacent-float hull `u ∈ {1, 1+2^-23}`, `s = 2^20`,
     `o = −2^20`, the exact image is `{0, 2^-3}` so `M_exact = 2^-3`, while
     `P = 2^20 + 2^-3` and the correct `B_st ≈ 2^-2` UV.

     A bound of the
     shape `2^-22·2·M_exact ≈ 2^-24` UV is smaller by a factor of ≈ 2^22.

- `B_fragment = F(M_runtime)`. Here `F` is the post-affine fragment
  displacement bound of the admitted consumer, **evaluated at the runtime
  magnitude bound**, not at `M_exact`.

  For every consumer this design
  admits, `F ≡ 0` by proof from the attested source (§3.2): the only
  post-affine arithmetic is an addition of an exact zero.

  The frontend refuses consumers with a nonzero `F` (C4, §11). Admitting one
  later requires a bound citable on every runtime target, and composition at
  `M_runtime` by this rule.

- **No recursive or fixed-point term exists.** The fragment consumes the
  affine result and produces the sampler coordinate. Nothing else transforms
  that coordinate. The composition is a finite two-stage chain.

  A
  hypothetical multi-stage fragment would get a stage-by-stage bound, each
  stage magnitude derived from the output bound of the previous stage, still
  finite, still no fixed point.

Final per-axis envelope:

```
E_axis = 0                                          (identity mapping: short circuit)
E_axis = 0                                          (exact tier: every term proven zero)
E_axis = B_enc + B_st + B_daz + F(M_runtime)         (envelope tier)
       = B_enc + B_st + B_daz                       (admitted consumer set: F ≡ 0)
```

Exact-tier zeroing is by proof, term by term: `B_enc = 0` (representable
corners), `B_st = 0` (Lemma P: no rounding, no result flush), `B_daz = 0`
(no subnormal operand, because E1/E3 require normal operands and E2
annihilates the input, so a flushed subnormal `u` cannot displace a product
that is zero either way), `F ≡ 0` (§3.2).

Accepted false negatives, recorded deliberately: `B_st` uses the
pre-cancellation `P`, so a large-magnitude cancelling mapping gets a very
conservative envelope and will usually return `Unknown` (often via the region
budget). That is the safe direction, and the design does not tune it here.

`[INFERENCE]`: every numeric constant above is dyadic and conservative. Code
comments repeat the derivation, and boundary tests pin it.

### 6.3 Soundness statement (the required proof)

Claim: for every triangle classified under a resolved, admitted mapping, and
for every fragment the rasterizer can produce, the sampler coordinate of the
fragment lies inside the domain the classifier tests.

1. **Pre-transform.** The coordinate `x` of the fragment is a binary32 value
   in the closed corner hull `H = hull(u_i)` (C2, convexity, §3.4).

2. **Exact image.** The per-axis affine map `t(·)` is affine, so
   `t(H) = hull(t_i)` exactly.

3. **Runtime affine result ⊆ encoded affine hull ⊕ expression envelope.**

   - Envelope tier: `|rt − t(x)| ≤ B_st + B_daz`. `B_st` covers the rounding
     of both admitted evaluation paths (A1, A3) and both result flushes (A2).
     `B_daz` covers all three input-operand flushes (A2). With step 2,
     `rt ∈ hull(t_i) ⊕ box(B_st + B_daz)`.

     Each `t_i ∈ t̃_i ⊕ box(B_enc)`,
     and the Minkowski sum with a box preserves convexity, so
     `hull(t_i) ⊆ hull(t̃_i) ⊕ box(B_enc)`.

     Therefore `rt ∈ hull(t̃_i) ⊕ box(B_enc + B_st + B_daz)` =
     encoded affine hull ⊕ `B_expression`.

   - Exact tier: Lemma P (§7) gives `rt = t(x)` exactly on every admitted
     path, and the tier predicate makes every `t_i` representable, so
     `t̃_i = t_i` and `rt ∈ hull(t̃_i) ⊕ box(0)`. The same statement holds
     with `B_expression = 0`.

4. **Runtime post-fragment coordinate ⊆ encoded affine hull ⊕ final
   envelope.** The family fragment displaces by at most `F(M_runtime)`, and
   step 3 established `|rt| ≤ M_runtime` (§6.2).

   Hence the sampler coordinate
   `c` satisfies `|c − rt| ≤ F(M_runtime)` and
   `c ∈ hull(t̃_i) ⊕ box(B_expression + F(M_runtime)) = hull(t̃_i) ⊕ box(E)`,
   which is precisely the inflated domain the classifier tests.

   For every consumer admitted here `F ≡ 0` and `c = rt`, so step 4 is an
   identity. The proof states it as a composition because that is what a
   later consumer must satisfy.

5. **Monotonicity.** The classifier proves `ProvenOpaque` only when the
   footprint interval of no non-opaque texel intersects the supplied domain,
   and `MustRemainTransparent` when some does. Inflating the domain can only
   add intersecting texels. `ProvenOpaque` can only persist or vanish, never
   appear.

   The all-mip conjunction inherits the direction (`Unknown`
   propagates, transparency absorbs).

6. Therefore no transformed sampled coordinate can escape the classified
   domain. No false `ProvenOpaque` is reachable under C1 through C7. "Exact
   arithmetic" appears only as the tooling.

   The proof rests on the
   containment argument above, not on exactness alone.

### 6.4 Why identity is untouched

`s = (1,1), o = (0,0)` short-circuits before any transform work. The original
corner floats go to the classifier with `AlphaUvEnvelope.Zero`, giving
identical classifier inputs and outcomes. The contract is **field-level bit
equality**, not reference identity.

`TriangleAlphaInput` is a readonly struct
(`TriangleAlphaClassifier.cs:41-101`), so the design cannot even express "the
same object", and it must not assert that either. The pinned contract:
`HasUv0` equal, and all fifteen float fields (`Position0/1/2.xyz`,
`Uv0/1/2.xy`) bit-identical under `BitConverter.SingleToInt32Bits`.

Two fixtures make that contract bite (F13):

- A hull containing exactly `0.0` (ubiquitous on real UV layouts). Identity
  through the general path fails the all-normal guard of E1, falls to the
  envelope tier, and inflates. That changes shipped identity outcomes.

  Only
  the short-circuit keeps parity.

- A `−0.0f` UV corner. The general path decodes `−0` to the dyadic zero and
  re-encodes it as `+0`, so the bit comparison fails for any implementation
  that recomputes instead of copying. `±0` sample identically, so this is a
  structural parity pin rather than a numeric one.

  The design states it as
  such.

The design deliberately does not assert whether the implementation *invoked*
the helper. Proving that negative about a pure function would require
production instrumentation, which this design refuses.

The contract covers
identical classifier inputs and identical outcomes only, which is what
soundness and parity need.

The lilToon rotate-at-zero noise exists at runtime for identity as for
everything else. It is the declared abstraction (C6) of the shipped baseline.
This design deliberately does not model it anew. Doing so would widen
identity domains and change identity outcomes.

### 6.5 Performance and allocation constraints

The code computes envelope and tier decisions once per `Classify` call (not
per mip, not per texel). Per-level expansion is two rational multiplications
per vertex. All arithmetic reuses `ExactDyadic`/`ExactRational`/`BigInteger`.

The allocation profile is the existing profile of the classifier plus O(1)
rationals per triangle. The new code uses no LINQ and no closures. The region
budget `MaxSupportRegions` stays untouched and continues to bound all cell
enumeration. [SOURCE: current classifier structure.]

[INFERENCE: additive cost stays within one hull construction.]

## 7. Exact supported affine subset

**Definition (value classes).** For an exact dyadic `v`: `Zero(v)` iff `v =
0`. `Normal(v)` iff `2^-126 ≤ |v| ≤ (2−2^-23)·2^127`. `Exact32(v)` iff `v` is
representable in binary32 (24-bit significand, exponent in range).

**Lemma P (path-independent exact evaluation).** Let `x`, `s`, `o` be binary32
values and `p = x·s`, `r = p + o` their exact products/sums. If

1. each of `x`, `s`, `o` is `Zero` or `Normal`,

2. `Exact32(p)` and (`Zero(p)` or `Normal(p)`),

3. `Exact32(r)` and (`Zero(r)` or `Normal(r)`) and `|r| < 2^127`,

then every admitted evaluation path returns exactly `r`.

*Proof.* No operand of either operation is subnormal (1, 2), so DAZ is inert
(A2). Serial path: the multiply is correctly rounded and its exact result `p`
is representable, so it returns `p`. Rounding functions are the identity on
representable values.

The Vulkan environment states that verbatim for
implementation-defined modes, and the 0.5-ULP requirement implies it
elsewhere (A1). `p` is zero or normal, so FTZ is inert.

The add is correctly rounded with representable exact result `r`, so it
returns `r`. `r` is zero or normal, so FTZ is inert. Fused path: one
correctly-rounded operation on the exact `r` returns `r`.

A3 admits no other
path (and the one extra term any admitted consumer applies is an exact zero,
invariant under reassociation). ∎

**Corollary P0 (annihilation).** If `s = ±0` then `p = ±0` regardless of the
class of `x`, because a DAZ-flushed subnormal `x` yields the same zero
product. The design may drop the class requirement on `x` in that case.

Per axis, with `hull` the exact corner interval:

| # | Condition (per axis) | Tier | Modeled axis domain |
|---|---|---|---|
| E1 | `s = ±2^k` with `Normal(s)`, `o = ±0`, and **both** the hull interval and its image lie entirely in `[2^-126, 2^127)` or entirely in `(−2^127, −2^-126]` | Exact | exact scaled hull |
| E2 | `s = ±0` with `o = ±0` or `Normal(o)` | Exact | the single point `o` |
| E3 | axis degenerate (`min == max == c`) and Lemma P's hypotheses hold for `(c, s, o)`: `c`, `s`, `o` each `Zero` or `Normal`; `Exact32(c·s)` and `c·s` zero-or-normal; `Exact32(c·s + o)` and `c·s + o` zero-or-normal and `< 2^127` in magnitude | Exact | the point `c·s + o` |
| V | anything else | Envelope | inflated exact image (`B_enc + B_st + B_daz`) |

Why each exact arm satisfies Lemma P:

- **E1.** Every binary32 `x` in the hull is normal (the interval excludes
  zero and subnormals on both sides), `p = x·2^k` preserves the significand
  so `Exact32(p)` holds, and `p` is normal by the image guard (monotone
  scaling puts every interior image between the endpoint images).
  `r = p + (±0) = p`, also representable and normal.

  Decidable from the two
  endpoints alone, so E1 is the only *interval-wide* exact arm.

- **E2.** `p = ±0` by Corollary P0. `r = o`, representable and
  zero-or-normal by the condition. `fl(±0 ± 0) = ±0` and ±0 sample
  identically.

- **E3.** Lemma P applied to the single hull value.

Deliberate narrowing versus revision 2: E3 previously verified one serial
evaluation `fl(fl(c·s)+o) == c·s + o` in dyadics. That check is unsound under
A1/A3.

It can accept a value whose product rounds and whose addition happens
to compensate under one rounding direction, while the fused path or another
admitted rounding direction returns something else.

Representability of **both** `c·s` and `c·s + o` is path-independent and
strictly stronger. The cases it gives up (compensating rounding on a
degenerate axis) fall to the envelope tier and still classify.

The resolver admits the material when `Channel == 0` (§8). The tier decision
is per-triangle because E1/E3 depend on the hull. A mapping with
`s = (4, 0.5)`, `o = (0,0)` is exact-tier on both axes.

A mapping with
`s = (2,2)`, `o = (0.5, 0.25)` is envelope-tier on both axes. A mapping with
`s = (2,2)`, `o = (0.5, 0)` is envelope-tier on the u axis and exact-tier
(E1) on the v axis.

Zero scale on one axis with an envelope on the other composes (point axis ⊕
box = a degenerate rectangle the hull machinery already represents). The
§6.1 step 4 overflow guard checks every corner before any tier applies, so no
exact arm can run with a magnitude at or above 2^127.

## 8. Refusal versus per-triangle-`Unknown` boundary

- **Frontend-level refusal (family vocabulary, C4):** a frontend whose
  attested source applies unbounded post-affine fragment arithmetic refuses
  its own non-identity mapping.

  Today: lilToon cutout alpha with non-identity
  `_MainTex` scale/offset → `UnsupportedUv` naming `_MainTex_ST`
  (`LilToonSemanticDiagnosticCode.UnsupportedUv` already exists, and lilToon
  uses it for every other lilToon UV refusal).

  The alpha output is `Unknown`. The resolver then refuses with
  `SemanticsUnknown`, exactly as it does for the `_MainTex_ScrollRotate` gate
  today. lilToon identity ST stays unaffected.

- **Material-level refusal (`UnsupportedUvMapping`):** `Channel != 0` only.
  Mesh capture supplies UV0 alone (`UnityRendererAlphaAnalysis.cs:346-357`),
  so no caller could honor a wider channel claim. Non-finite scale/offset is
  unreachable (the `UvMapping` constructor throws).

  If a future capture path
  bypasses it, that is a programming defect, not a refusal.

- **Per-triangle `Unknown`:** overflow guard (§6.1 step 4), region-budget
  overflow (`MaxSupportRegions`, now more reachable for huge-scale Repeat
  mappings and for large-magnitude cancellation), degenerate mesh geometry
  (existing), missing UV0 (existing). None of these may collapse to
  `ProvenOpaque` or to material-level support.

- **Uniform `MustRemainTransparent`:** unchanged sources (fully non-opaque
  level, multiplier lemma), orthogonal to ST.

- **Renderer-level refusal:** unchanged. Non-singleton/non-finite/absent
  `_MainTex_ST` animation admission (existing machinery, §10).

## 9. Supported/unsupported boundary table

| Boundary | Outcome |
|---|---|
| Positive fractional scale (e.g. 2.5) | supported — classified (envelope tier) |
| Scale > 1 (e.g. 4) | supported — classified (exact tier if ±2^k with zero offset, else envelope) |
| Negative scale / reflection | supported — classified (E1 exact for ±2^k; hull of transformed corners covers the flip; envelope otherwise) |
| Zero scale (one axis) | supported — classified as a point/line domain (E2); explicitly not missing-UV evidence |
| Zero scale (both axes) | supported — classified as a point domain |
| Fractional offset | supported — classified (envelope tier) |
| Negative offset | supported — classified (envelope tier) |
| Very large finite ST values | supported while the mapped hull stays finite; per-triangle `Unknown` at the 2^127 guard |
| Large-magnitude cancellation (`|u·s|`, `|o|` large, `u·s + o` ≈ 0) | supported — envelope tier with an envelope driven by `P`, not by `M_exact`; usually `Unknown` (budget), never `ProvenOpaque` (F25) |
| Normal ST/UV values | supported (E1/E2/E3 exact or envelope) |
| Subnormal ST or UV values | supported — classified (envelope tier; `B_daz` covers input-operand flush, the `2^-125` floor covers result flush); subnormal scale never exact-tier (E1/E3 normality guards) |
| Exact product (`u·s` representable) | part of tier logic (E1/E3) — zero envelope in the exact tier |
| Inexact product | envelope tier — padded |
| Exact addition (`u·s + o` representable on the hull) | only pointwise decidable (E3 degenerate axes); non-degenerate axes with nonzero offset always envelope |
| Compensating rounding on a degenerate axis | envelope tier (E3 narrowed to path-independent exactness) |
| Underflow (subnormal results) | envelope tier — classified (floor term); the exact tier never produces a subnormal result |
| Signed zero (`o = −0`, `s = −0`) | `−0 == 0` numerically; E1/E2 arms handle; coordinates ±0 sample identically — classified |
| Nonzero UV channel | material-level refusal (`UnsupportedUvMapping`) — unchanged |
| Degenerate mesh geometry | per-triangle `Unknown` — unchanged |
| Degenerate UV hull (line/point) | supported — classified (hull machinery handles 1–2 vertex domains; E3 covers pointwise exactness) |
| Point filter | classified; footprint = cell (envelope-agnostic) |
| Bilinear filter | classified; half-texel open footprints applied to the inflated domain |
| Clamp wrap | classified; border-cell unbounded intervals absorb envelope overshoot |
| Repeat wrap | classified; `NormalizeRepeat` is exact integer-period translation — commutes with inflation; boundary cells included by open/closed interval ownership |
| Every mip level | conjunction unchanged — `ProvenOpaque` only if every level proves over the inflated per-level domain |
| Poiyomi non-identity ST (gates satisfied) | supported — classified (the new coverage) |
| Poiyomi with `_MainTexPan`/`_MainPixelMode`/`_MainTexStochastic`/parallax on | unchanged existing refusals (`UnsupportedUv`/`UnsupportedFeature`) |
| lilToon cutout identity ST | supported — unchanged, byte-for-byte shipped behavior |
| lilToon cutout non-identity ST | frontend refusal (`UnsupportedUv`, `_MainTex_ST`); alpha `Unknown`, never proven |
| Exact-singleton ST animation | admitted (existing singleton machinery; the animated value equals the captured non-identity default and flows through as the material's mapping) |
| Non-singleton ST animation | renderer-level refusal (`AnimatedMaterialPropertyNotSingleton`) — unchanged |
| Texture swaps carrying different ST | supported — each admitted material resolves against its own captured mapping; consensus intersection across states (existing `IntersectOutcomes`) |
| Transform exceeding exact-region complexity limits | per-triangle `Unknown` (`MaxSupportRegions`) — never "empty support" and never opacity |

No uncertain case defaults to `ProvenOpaque`: every uncertain arm above lands
on refusal, `Unknown`, or `MustRemainTransparent`.

## 10. Data flow (capture → apply)

1. **Capture**: unchanged. `ScaleOffset` evidence on `_MainTex` (both family
   alpha requests already request it).

2. **Animation admission**: unchanged. The pipeline admits derived
   `_MainTex_ST` bindings as exact singletons against the captured default of
   each material. Refusal modes stay unchanged.

   No new relevance names
   (`_MainTex_ST` stays a derived texture-scale-offset name, never vector
   evidence).

3. **Semantic value**: Poiyomi stays unchanged (it already emits the
   mapping). lilToon cutout alpha gains the C4 gate, so its emitted mappings
   remain identity-only. `TextureSample.Coordinates` now carries non-identity
   values through to the resolver for affine-only consumers.

4. **Resolution**: `ResolveSampled` narrows `IsSupportedMapping` to the
   channel test. `AlphaResolution.Classified` stores the mapping.

5. **Per-triangle transformation**: `Classify` short-circuits identity, else
   computes tier + transformed corners + envelope once (§6.1).

6. **All-mip classification**: per-level inflation of the exact hull. The
   conjunction, the absorbing transparency, the propagating `Unknown`, and
   the budget stay untouched. Outcomes flow to `IntersectOutcomes` and the
   separation planner exactly as today.

## 11. Family and architecture boundary

**Decision: one family-agnostic resolver capability, gated by an attested
semantic fact that each frontend owns.** [DECISION, §5-G]

The resolver keeps its family-blind signature
(`AlphaSemanticsResolver.Resolve` takes an alpha value and a field provider,
`AlphaSemanticsResolver.cs:216-218`) and applies the same tier/envelope rules
to every mapping it receives.

The frontend that owns the shader fact (C4) enforces the compatibility
boundary one level up. A frontend whose attested source proves an affine-only
sampler coordinate may emit a non-identity mapping.

A frontend whose source
applies unbounded post-affine arithmetic must refuse it in its own
vocabulary.

This keeps shader-specific behavior shader-specific, adds no registry,
provider interface, or family parameter, and fails closed for any future
frontend. That frontend must satisfy C4 before it emits a non-identity
mapping.

Support matrix:

| Family / version | Identity ST | Non-identity ST | Enforcement |
|---|---|---|---|
| Poiyomi Toon 9.3.64 (attested hash, unlocked), pan/pixel-mode/stochastic/parallax gates satisfied | supported (unchanged) | **newly supported** — exact tier (E1/E2/E3) or envelope tier | source-verified affine-only coordinate (§3.2); attestation hash fails closed on any source change |
| Poiyomi any other version / locked / gate not satisfied | unchanged refusals | refused (existing gates) | existing attestation + gates |
| lilToon 2.3.4 cutout | supported (unchanged, C6 baseline abstraction) | **refused at the frontend** (`UnsupportedUv`, `_MainTex_ST`) | new C4 gate in `InterpretCutoutAlpha` |
| Any future frontend | as designed | requires a C4-satisfying attestation | C4 |

Reopening path for lilToon non-identity ST (not in scope here): establish an
absolute bound on `|sin(+0)|` and `|cos(+0) − 1|` for **every** runtime
target that the optimization contract covers. Metal is the missing piece,
because its math accuracy is ULP-relative (§3.3-A4).

Then compose the bound as `F(M_runtime)` by the §6.2 rule (never at
`M_exact`) and add the term to the envelope for that consumer only.
Alternatively, a pinned lilToon version whose `lilRotateUV` has a zero-angle
early-out would make the fragment exactly the identity and admit the family
directly.

Not introduced: a lilToon-only classifier, a provider registry, a family
parameter on the resolver, a generalized shader graph, a second UV geometry
implementation (the envelope reuses the rationals of `ExactUvGeometry` and
adds members to it), new sampling modes, UV1+ support, rotation/scrolling
support (`_MainTex_ScrollRotate` remains the separate exact-zero gate of the
cutout frontend), cutoff-margin support, a new shader family.

## 12. Compatibility impact

- **Poiyomi:** alpha values with channel 0 and non-identity ST now resolve
  instead of refusing. Every other output and every existing gate stays
  unchanged. This is the entire coverage gain of the design.

  The design
  charges no slack term to Poiyomi. Its post-affine displacement is zero by
  proof, so the exact tier really is zero-envelope and the envelope tier
  carries only expression terms.

- **lilToon cutout:** identity ST behavior stays byte-for-byte unchanged.
  Non-identity ST moves its refusal from the resolver (`UnsupportedUvMapping`,
  alpha value complete) to the frontend (`UnsupportedUv` diagnostic naming
  `_MainTex_ST`, alpha value `Unknown` → resolver `SemanticsUnknown`).

  **No triangle changes outcome**: both paths yield `Unknown` for every
  triangle. The observable difference is the diagnostic surface, so
  `LilToonCutoutAlphaTests` gains an explicit expectation rather than
  inheriting one.

- **Other semantics** (base color, emission, normal): they do not pass
  through `AlphaSemanticsResolver` and stay unchanged. The design scopes the
  lilToon gate to the cutout alpha interpretation, and it does not touch the
  other lilToon mapping builders (`LilToonMaterialSemantics.cs:381, 831,
  1004`).

- **Analysis/refusal surface:** the `UnsupportedUvMapping` refusal remains
  defined and reachable (channel ≠ 0). Dedup (`DistinctResolutions`)
  semantics stay unchanged (classified resolutions still never merge).

- **Coverage versus revision 2 of this design:** the design withdraws the
  lilToon non-identity half. Versus shipped `main`, nothing regresses.

## 13. Security / privacy / package boundaries

No new capture, no asset reads, no network at analysis time, no shader
compilation, no Census Lab access: the feature is pure arithmetic over
already-captured evidence. The design fetched vendor shader source **for
this design document only** (digests in Appendix A).

No vendor bytes enter
the repository.

The design crosses no product/research package boundary. No `.meta` churn
exists beyond the one new production file (`AffineUvTransform.cs`) and the
two new test files (`ExactUvEnvelopeTests.cs`, `AffineUvTransformTests.cs`).
The pipeline generates the metas with the files and treats them as one unit.
No manifest or lockfile change.

## 14. Acceptance criteria

1. Resolver: channel 0 + any finite ST resolves (no `UnsupportedUvMapping`).
   Channel ≠ 0 still refuses. Identity parity stays pinned (identical
   outcomes and field-level bit-identical classifier inputs for identity
   mappings, including a hull containing `0.0` and a `−0.0` corner).

2. Classifier: with `AlphaUvEnvelope.Zero`, outcomes match today for
   identical inputs (parity suite). With a nonzero envelope, every boundary
   case a runtime rounding could cross sits inside the tested domain
   (boundary fixtures below), and the UV→domain unit conversion is
   `ex·width·T`.

3. Transform: E1/E2/E3/V tier decisions match §7 on boundary fixtures
   (power-of-two, fractional, negative, zero, subnormal scale, subnormal or
   zero-crossing hulls, degenerate axes, compensating-rounding degenerate
   axis → V, overflow guard).

   The envelope goldens cover `B_enc`, `B_st` (including the `2^-125` floor
   and the `P`-driven cancellation case), and `B_daz` (including its
   `o`-flush sub-term). Exact-tier envelopes are exactly zero.

4. Families: one Poiyomi material with non-identity ST classifies instead of
   refusing. One lilToon cutout material with non-identity ST refuses at the
   frontend with `UnsupportedUv` naming `_MainTex_ST` while the identity
   cutout fixture stays unchanged.

   Poiyomi pan/pixel-mode/stochastic/parallax
   and lilToon ScrollRotate gates still refuse.

5. Animation: singleton non-identity ST re-assertion still admits (existing
   behavior, regression-pinned through the widened path). Non-singleton
   refuses.

6. Preparation: one full-path Poiyomi scenario where non-identity ST moves a
   triangle from transparent to provably opaque and the separation plan
   migrates it. The lilToon non-identity counterpart prepares zero opaque
   candidates. Source assets stay bit-unchanged.

7. Full product + research EditMode suites pass. The Unity console stays
   clean of new errors/warnings. No source-asset mutation occurs. Nothing
   outside the declared file map changes.

## 15. Falsifiers and counterexamples

Each acceptance criterion is paired with tests that fail under the plausible
wrong implementation (full map in the implementation plan):

- F1 scale ignored / F2 offset ignored: a triangle transparent under identity
  but over opaque texels only after the true transform must flip to
  `ProvenOpaque` (and the mirror image must not flip when it must not).

- F3 negative scale abs()-ed: a mirrored placement whose transparent region
  is the mirror image of the opaque one must classify `MustRemainTransparent`
  under abs-scaling but `ProvenOpaque` under true reflection.

- F4 mip-0-only transform: a chain transparent only at mip ≥ 1 must stay
  `Unknown`/transparent under non-identity ST.

- F5 wrap-after-transform and F7 boundary-cell loss: **withdrawn as
  executable falsifiers (controller equivalence audit, 2026-09-01).** Repeat
  normalization commutes with envelope inflation up to an integer-period
  translation (§6.1), so both named mutations are behavior-preserving. The
  Repeat period-boundary fixtures remain as positive behavioral coverage.

  F6
  untransformed footprint remains executable: footprint fixtures whose
  supporting cell lies outside the untransformed domain.

- F8 exact-real-for-runtime / F9 double-as-exact / F10 inward rounding: a
  transform whose exact image sits on a texel boundary with a transparent
  texel on the rounded side must not prove. The envelope absorbs the
  rounding, and the boundary-exact coordinate must classify conservatively.

- F11 overflow/underflow as usable coordinates: ≥ 2^127 guard → `Unknown`.
  Subnormal range → envelope-classified, never opacity.

- F12 per-triangle failure promoted: complexity overflow and the overflow
  guard must yield `Unknown`, never material support or opacity.

- F13 identity changed: the parity suite of §6.4: field-level bit equality
  including a hull that contains `0.0` and a `−0.0` corner, plus identical
  outcomes. Removing the short-circuit (letting identity fall through to tier
  selection) must fail it.

- F14 non-singleton ST admitted: existing refusal pinned through the new
  path.

- F15 family boundary erased: the lilToon cutout non-identity fixture must
  refuse with `UnsupportedUv` naming `_MainTex_ST`, and the Poiyomi
  non-identity fixture must classify. Deleting the lilToon gate makes the
  first fail.

  Adding a family branch inside the resolver is structurally
  impossible (`AlphaSemanticsResolver.Resolve` takes no family input,
  audit-pinned in the plan), and the file map excludes it too.

- F16 complexity-as-empty: budget overflow returns `Unknown` outcomes that
  reach the planner as transparency, never as "no candidate, prove all".

- F17 zero-scale-as-missing-UV: E2 point domains classify. They are distinct
  from `MissingUv0`.

- F18 degenerate-hull/degenerate-mesh confusion: a degenerate UV hull with a
  valid mesh classifies. A degenerate mesh stays `Unknown` even with a
  well-formed hull.

- F19 component swap (`_ST.zw` ↔ `.xy` exchanged, or per-axis scale crossed):
  asymmetric fixtures (offset `(0.5, 0.25)`, scale `(2, 3)`) must fail any
  swapped evaluation.

- F20 footprint width scaled by `|s|`: **reclassified as a structural,
  non-executable audit (controller decision, 2026-09-01)**.

  The current API
  makes an ST-scaled filter footprint impossible to express:
  `TriangleAlphaClassifier.Classify` receives only triangle, texture,
  sampling, and envelope, and `ExactUvGeometry.CreateTextureScaledDomain`
  receives only triangle, texture dimensions, and envelope.

  Neither carries `UvMapping` or an ST scale. Bilinear interval width and
  candidate expansion derive exclusively from `domain.TexelScale`, which is
  texture geometry (width/height under a power-of-two alignment), not
  `_MainTex_ST` scale.

  The ST scale exists only in `AffineUvTransform`, and
  classification consumes it before the fact: classifier fixtures supply
  their envelope directly and bypass it.

  The design retains the positive bilinear one-texel-reach tests: the
  footprint is one texel of the *texture* in the transformed domain, never
  `|s|·texel`.

- F21 inflate-after-normalize: **withdrawn as an executable falsifier
  (controller equivalence audit, 2026-09-01)**. Same commutativity as F5/F7
  (§6.1): a Repeat fixture whose envelope crosses a period boundary includes
  the boundary cell under either ordering.

  The existing fixture remains as
  positive behavioral coverage.

- F22 wider-type-as-proof: the design refuses computing the transform or
  envelope in `double` without dyadic representability proof (the prohibition
  of the 2026-08-17 spec, restated).

  A direct discriminator pins exact
  rational → binary32 rounding against a double-rounding counterexample:
  `ExactUvEnvelopeTests.EncodeToNearestFloatDoesNotDoubleRoundAboveBinary32Midpoint`
  encodes `1 + 2^-24 + 2^-78`, whose `2^-78` perturbation vanishes in
  binary64.

  A `double`-routed encoder lands exactly on the binary32 midpoint and ties
  to even at the wrong neighbor (`0x3f800000`), while exact encoding selects
  the next neighbor `0x3f800001`. The design withdraws the earlier claim that
  the F10 boundary fixture must distinguish this.

  Its inputs are
  binary64-sufficient.

- F23 test-contract rewrite, not deletion: the resolver tests that assert the
  identity-only refusals of today for scaled/offset mappings
  (`AlphaSemanticsResolverTests.UnsupportedUvMappingRefuses`) encode the
  product decision this design revises. The plan rewrites them to the new
  boundary (channel refusal only) and never deletes or hollows them.

- F24 subnormal-operand DAZ amplification admitted as exact: `s = 2^-127`
  (subnormal power of two) over an all-normal hull must fall to the envelope
  tier (`B_daz`) and classify, never E1. Likewise a subnormal UV corner with
  large `|s|`, and a subnormal `o` on an E2/E3 axis.

  A texture opaque over
  the mapped hull but transparent at the flushed-to-zero coordinate must not
  prove.

- F25 **envelope composed over the ideal domain instead of the runtime
  domain** (the revision-2 defect). Fixture A: the adjacent-float hull
  `u ∈ {1, 1+2^-23}` with `s = 2^20`, `o = −2^20`.

  The exact image is
  `{0, 2^-3}`, so `M_exact = 2^-3`, while the pre-cancellation product
  magnitude is `P = 2^20 + 2^-3` and the correct `B_st ≈ 2^-2` UV (≈ 2
  texels of an 8×8 texture).

  An implementation that bounds the expression displacement by the final
  magnitude alone (`2^-22·2·M_exact ≈ 2^-24` UV) is smaller by a factor of
  ≈ 2^22. The non-opaque texel of the fixture sits inside the correct
  envelope and outside the wrong one, so the wrong implementation returns
  `ProvenOpaque` where the right one returns `MustRemainTransparent`.

  Fixture B: the same hull with `s = 2^40`, `o = −2^40` pins the transform
  golden (`M_exact = 2^17`, `P = 2^40 + 2^17`, `B_st ≈ 2^18` UV) and asserts
  the outcome is never `ProvenOpaque`. It is also the fixture on which
  evaluating a fragment term at `M_exact` instead of `M_runtime` under-bounds
  the operand magnitude by ≈ 3×.

  Both fixtures are the documented counterexamples. Any future `F(M_runtime)`
  term must compose against them (§6.2).

- F26 path-dependent exactness accepted. Constructed counterexample
  `[MEASURED by hand]`: degenerate axis `c = 1 + 2^-12`, `s = 1 + 2^-12`,
  `o = 3·2^-24`.

  Then `c·s = 1 + 2^-11 + 2^-24` needs 25 significand bits
  and is **not** binary32-representable, so the repaired predicate
  classifies the axis **V**.

  The check of revision 2 admitted it as E3 with a zero envelope, because the
  round-to-nearest serial evaluation happens to compensate:
  `fl_RNE(c·s) = 1 + 2^-11` (exact tie, ties-to-even), and
  `fl_RNE(1 + 2^-11 + 3·2^-24) = 1 + 2^-11 + 2^-22`, which equals the exact
  `c·s + o`.

  Under a truncating implementation-defined rounding mode, which A1/A2
  admit, the same serial path returns `1 + 2^-11 + 2^-23`, i.e. `2^-23` away
  from the value that a zero-envelope exact-tier proof asserts. The test
  asserts tier `V` with a non-zero envelope on this axis.

  The
  classifier-level consequence is the F8/F10 boundary shape (a coordinate
  placed exactly on a texel edge with a non-opaque texel on the far side
  must not prove).

## 16. Stop conditions

- S1. A boundary test that cannot pass without weakening the §6.2 envelope
  derivation (i.e. the bound proves insufficient under any covered platform
  behavior): stop, report the counterexample, retain identity-only.

- S2. Evidence that the attested source of Poiyomi does **not** deliver the
  affine result to the sampler under the gates of the frontend (§3.2
  re-verified at implementation time against the same commit and hash):
  stop.

  The only admitted consumer of the selected architecture is then
  gone.

- S3. Evidence that an admitted runtime target evaluates a source-level
  `a*b+c` along a path outside A3 (e.g. a division-introducing algebraic
  rewrite): stop. Lemma P and `B_st` both depend on that path set.

- S4. Region-budget behavior under real avatar scales showing pathological
  `Unknown` rates (coverage collapse): report it. Do not widen budgets here.

- S5. Any need to touch capture, admission, planner, apply semantics, or any
  frontend beyond the single lilToon cutout alpha gate of §6.1 step 5: out
  of scope. Stop and return to the controller.

- S6. The preparation harness cannot express a texture-backed Poiyomi alpha
  slot with an imported mipmap chain (acceptance criterion 6): report it. Do
  not invent new host seams to make the scenario fit.

## 17. Explicit non-goals

UV channels 1 to 3. `_MainTex_ScrollRotate` (scroll/rotate). lilToon
non-identity ST (deferred with the reopening path in §11). Parallax-aware
domains. Trilinear/anisotropic/mirrored filtering. Differing wrapU/wrapV.

Cutoff-margin proofs. UDIM/IDMask-aware proofs. Vertex-stage UV transforms.
Generalized texture-transform IRs. Per-family envelope constants.

Widening
`MaxSupportRegions`. Changing capture, admission, planner, apply, or any
frontend gate other than the one lilToon cutout alpha gate this design adds.

## 18. Remaining open questions

None blocking. Recorded for the controller:

(a) This design defers lilToon non-identity ST, and does not refuse it
forever. §11 states the two evidence routes that reopen it (a Metal-inclusive
absolute trig bound, or a pinned lilToon version with a zero-angle
early-out). Either is a new design.

(b) The un-modeled rotate-at-zero noise of the identity baseline remains a
declared abstraction (C6). Closing it would change identity outcomes, and
parity puts it out of scope.

(c) `B_st` is conservative for large-magnitude cancellation because it uses
the pre-cancellation product magnitude `P`. A tighter per-operation bound
(zero multiply term when the product is provably exact on the hull) is
available for free from the E1 predicate, but it only helps mappings with
huge scales, so the design deliberately does not implement it (YAGNI).

---

## Appendix A — pinned-source digests used by this design

`[MEASURED]` lilToon, fetched from
`https://raw.githubusercontent.com/lilxyzw/lilToon/2.3.4/` (tag `2.3.4`,
commit `252fd8cfc46106d4967e95b3f2c788418502f227` per B2 §2):

```
96b1bbfecc32d16735db16b5a0c46db3bf81c8f28b9d247c3394ae3c6af84dc1  Shader/Includes/lil_common_frag.hlsl
daee7c7dc133d85eb8096fe465e208d21361a4e6a570af1b2fe37c8b7bd296ed  Shader/Includes/lil_common_functions.hlsl
9863c86c76682c5132ec04937977c96d22a12979b79527b698771214999ed9e0  Shader/Includes/lil_common_input.hlsl
49b4c364f1bd2f46a4dcb34921512c13473c03abb055428ee4da19dcce461802  Shader/Includes/lil_common_macro.hlsl
19c7764d77ad29f14f62b3e4e7458f6c30b9e518cc875d86354dacb82560c6ed  Shader/Includes/lil_common_vert.hlsl
```

`ltspass_cutout.shader`, fetched for the `LIL_FEATURE_ANIMATE_MAIN_UV`
verification (line 645). B2 does not pin its digest. The design re-fetches
it at implementation time if needed.

`[MEASURED]` Poiyomi, fetched from
`https://raw.githubusercontent.com/poiyomi/PoiyomiToonShader/e125e1c33cbfb860f59330799dd4d10a1097242d/`
(the commit named in `PoiyomiMaterialSemantics.cs:25`):

```
31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755  _PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader
```

That digest equals `PoiyomiMaterialSemantics.CanonicalNormalizedSourceHash`
(`PoiyomiMaterialSemantics.cs:32-33`). The file has LF endings and no BOM,
so the frontend normalization is the identity on it.

The accompanying
`.shader.meta` carries `guid: 9444ce77bf4418748b1e8591b9d97f85`, equal to
`CanonicalShaderGuid` (`:30-31`).

The §3.2 line numbers refer to this file. The design read the vendor bytes
for verification only and does not vendor them into the repository.
