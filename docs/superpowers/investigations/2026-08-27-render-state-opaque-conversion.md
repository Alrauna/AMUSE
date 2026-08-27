# Render State / Opaque Conversion — Investigation

**Status: historical investigation. The capability is not implemented.**

- Its prior alpha-mask blocker was **partially resolved by PR #22** (merge commit
  `11765f4`), which made Poiyomi Replace mode with no assigned `_AlphaMask` a proven
  constant alpha.
- **Assigned-mask cases remain blocked** on the real texture-evidence investigation.
- **The render-state capability itself remains unimplemented** and is subject to
  controller-reviewed design refinement. Several conclusions below were superseded after
  review; they are marked in §8 rather than deleted, so the reasoning stays inspectable.

This note distinguishes four kinds of content deliberately: **verified evidence** (§2–§6),
**superseded conclusions** (§8), **current direction** (§9), and **unresolved design
questions** (§10). It is not an implementation specification, and nothing here should be
read as an approved requirement unless §9 says so.

Census Lab observations are architectural pressure and validation evidence, never
correctness authority. Correctness comes from pinned shader source and public deterministic
tests.

## 1. Blocking capability

Alpha separation must place proven-opaque triangles on a generated material that is
canonically opaque. AMUSE cannot produce that material today because it has no
representation of a material's render mode, no attested description of what "canonical
opaque" means for the supported shader, no conversion-relevant properties in any evidence
request, and no generated-material construction or validation path.

## 2. Verified evidence — Poiyomi mode presets are declared in the attested source

Poiyomi's `_Mode` presets are declared **inside the pinned shader source** as ThryEditor
`on_value_actions` metadata. The pinned Poiyomi Toon 9.3.64 identity is public and already
recorded in AMUSE's production attestation; it was re-verified by asset GUID and normalized
source hash before any fact was read.

Unity blend enum: `0 Zero, 1 One, 2 DstColor, 3 SrcColor, 4 OneMinusDstColor, 5 SrcAlpha,
10 OneMinusSrcAlpha`.

| `_Mode` | queue | RenderType | ForceOpaque | Cutoff | Src | Dst | ZWrite | A2C | Premul | `dst := src` at α=1? |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 Opaque | 2000 | Opaque | 1 | 0 | One | Zero | 1 | 0 | 0 | yes (already opaque) |
| 1 Cutout | 2450 | TransparentCutout | 0 | .5 | One | Zero | 1 | 0 | 0 | **yes** |
| 9 TransClipping | 2460 | TransparentCutout | 0 | .01 | SrcAlpha | OneMinusSrcAlpha | 1 | 0 | 0 | **yes** |
| 2 Fade | 3000 | Transparent | 0 | .002 | SrcAlpha | OneMinusSrcAlpha | 0 | 0 | 0 | **yes** |
| 3 Transparent | 3000 | Transparent | 0 | 0 | One | OneMinusSrcAlpha | 0 | 0 | **1** | blend yes, but premultiply changes RGB |
| 4 Additive | 3000 | Transparent | 0 | 0 | One | One | 0 | 0 | 0 | **no** |
| 5 SoftAdditive | 3000 | Transparent | 0 | 0 | OneMinusDstColor | One | 0 | 0 | 0 | **no** |
| 6 Multiplicative | 3000 | Transparent | 0 | 0 | DstColor | Zero | 0 | 0 | 0 | **no** |
| 7 2xMultiplicative | 3000 | Transparent | 0 | 0 | DstColor | SrcColor | 0 | 0 | 0 | **no** |

`_BlendOp` is Add in every preset. These values come from public vendor source, not from
private observation.

### `_Mode` is a preset hint, not a fact

Two independent findings force this:

1. **Runtime.** `_Mode` is a live uniform but reaches RGB nowhere. Its only reads are an
   opaque branch whose body is commented out, a post-clip cutout alpha force, and an
   alpha-to-coverage sharpening branch. Opacity actually comes from `_AlphaForceOpaque`,
   the blend state, `ZWrite` and `_Cutoff`.
2. **Reality.** Real materials in the authorized corpus do **not** reliably match preset
   tuples. Custom render queues diverged from the preset value for the same declared mode,
   and depth-write state was observed disagreeing with what the declared mode implies.
   Authors tune render state after applying a preset.

**Classification must therefore read effective render state and never infer it from
`_Mode`.** A design that trusted `_Mode` would misclassify real materials.

## 3. Verified evidence — the opaque-equivalence predicate

The load-bearing generic fact is whether the blend equation degenerates to `dst := src`
when alpha is 1:

- `BlendOp == Add`, and
- `SrcBlend ∈ {One, SrcAlpha}` (both evaluate to 1 at α=1), and
- `DstBlend ∈ {Zero, OneMinusSrcAlpha}` (both evaluate to 0 at α=1).

This cleanly separates convertible Cutout / TransClipping / Fade from non-convertible
Additive / SoftAdditive / Multiplicative / 2xMultiplicative. Premultiplied alpha is
excluded separately, because premultiplication changes how RGB is produced rather than how
it is combined.

## 4. Verified evidence — Poiyomi's own conversion path is GUI-bound

The vendor's `SET_PROPERTY` action routes to a helper whose first act is to read the
**active material inspector singleton**; it ignores the target array it is handed and
writes to whatever the live inspector is editing. That singleton is assigned only from the
inspector's own GUI path. There is no headless entry point, and an NDMF build pass has no
inspector.

Invoking the vendor conversion is therefore not available to a build-time pass, and would
additionally make Poiyomi a hard dependency of AMUSE, which repository policy forbids for
product code.

## 5. Verified evidence — clone fidelity

Characterized in the public AMUSE Editor project (instance identity confirmed by exact
normalized data-path match before use). `new Material(src)` copies shader, all properties,
texture scale/offset, the render-queue override, override tags, double-sided GI, instancing
and shader keywords. Mutating the clone left the source untouched on every field tested.

One trap recorded: `IsKeywordEnabled` returns false for a keyword the material's shader
does not declare, even when that keyword is present in the keyword list. A naive
keyword-preservation test written against a fixture shader that declares no keywords will
produce a false failure.

Also observed: a material with no explicit queue override still reports an effective queue,
so "has an override" and "what queue applies" are distinct questions. See §8 for why
treating override identity as a generic render-state fact was superseded.

## 6. Verified evidence — NDMF generated-asset lifecycle

Checked against the installed NDMF source rather than assumed:

- the build context walks assets reachable from the avatar root at the end of the build and
  saves them; and
- the asset saver persists immediately when called directly, with no reachability check.

So an eager save on a clone that is never referenced leaves an abandoned asset in the build
output. A generated material should be a plain transient clone, destroyed if preparation
later refuses, and left for NDMF to persist once a renderer references it.

## 7. Verified evidence — visual/functional compatibility contract

This work does not attempt framebuffer equivalence. The bounded claim is:

> For a surface whose alpha is independently proven to be exactly 1 across every admitted
> runtime state, rendering it through the canonical opaque counterpart of its attested
> Poiyomi material is a supported normalization under the default policy.

Admission requires attested unlocked pinned Poiyomi, the §3 predicate, premultiplied alpha
off, every coverage mechanism off, and conversion-relevant state proven (§10).

Under those conditions the policy authorizes the queue move, the depth-write enable and the
blend normalization. Forcing the shader's opacity flag is likewise a no-op exactly on the
triangle set the consumer proved to be alpha 1, so that part of the conversion and the proof
obligation line up by construction.

### The alpha clip threshold needs its own proof

An earlier revision of this note claimed that forcing the generated material's clip
threshold to zero is safe "because the clip is a threshold test and alpha is proven 1: the
fragment survived the original threshold and survives zero." **That reasoning is wrong and
is withdrawn.** Proving alpha is exactly 1 does not prove the fragment survived its original
threshold. A clip is a comparison against the threshold, so a sufficiently high threshold
discards the fragment *even at alpha 1*. Whether the original surface was visible is a fact
about the threshold, not about alpha.

The required condition is therefore:

> For every admitted runtime state in which the pinned shader actually performs alpha
> clipping, AMUSE must establish that the effective clip behavior is known and that alpha
> exactly 1 is not discarded.

That condition is the correctness requirement. It says nothing about *how* it is proven, and
this note deliberately does not fix a mechanism.

Establishing it means, at minimum, determining the **exact clip behavior** from the pinned
source — which term is compared, against what, and with which comparison strictness — rather
than assuming a particular form.

**Serialized values must not be assumed to lie within the inspector's declared range.** The
vendor declares a bounded range for the threshold, but a declared range constrains the
inspector widget, not what can be serialized into the material, written by script, or driven
by animation. Eligibility must reason about the effective value actually present, not about
the range the shader advertises.

**Unknown, non-finite, or unsafe threshold states refuse.** A threshold that cannot be read,
is not finite, or does not permit alpha 1 to survive is not a case to approximate; it is a
refusal.

**An animated threshold is admissible only when the condition can be proven for every
admitted relevant state.** Inability to establish that universal condition — for any reason
— is a refusal. This is the fail-closed direction: an animated threshold can legitimately
hide geometry, and a conversion that pinned the threshold to zero would resurrect what the
author deliberately hid.

Finite enumeration of the admitted values is one way the current admitted-state machinery
could discharge that obligation, and is the likeliest near-term mechanism. It is **not**
itself the requirement. A future proof that soundly establishes the same universal condition
over the admitted set by some other representation would satisfy it equally, and nothing in
this note should be read as excluding one. No such mechanism is proposed or designed here.

**When alpha clipping is inactive in the pinned source state, the threshold is irrelevant**
and must not cause refusal. Nothing is being compared, so an unknown, out-of-range, or
animated threshold cannot change the result, and treating it as a blocker would be an
unnecessary refusal rather than conservatism.

**Only once those conditions are established** is setting the generated opaque material's
threshold to zero justified. It is a consequence of the proof, not a premise of it.

Designing the evidence machinery that discharges these obligations is out of scope for this
note; §10 records it as unresolved.

## 8. Superseded conclusions and portions under controller review

These were proposed by this investigation and are **not** approved requirements. They are
retained for reasoning history only.

- **Globally widening ordinary alpha-analysis relevance with conversion-only properties.**
  Superseded. Folding conversion-only render-state properties into the general alpha
  evidence request would make unrelated analysis refuse on state it does not depend on. See
  §9 for the current direction.
- **Treating serialized custom-render-queue override identity as necessarily part of
  generic render-state facts.** Under review. Whether "an override exists" is a generic
  fact, or merely an implementation detail of reading the effective queue, is unresolved.
- **NDMF persistence integration tests on this prerequisite branch.** Superseded. The
  lifecycle finding in §6 is real, but exercising NDMF persistence belongs with the
  consumer that actually assigns a generated material to a renderer, not here.
- **Census launcher or new Census metric implementation.** Superseded. No launcher should
  be built and no new publishable metric introduced for this work.
- **Any global render-state framework.** Superseded. One shader, one direction, one
  version does not justify a general model, a mode graph, or pluggable adapters.
- **The proposed failure taxonomy, validation model and test strategy** were drafted before
  the above supersessions and should be re-derived against §9 rather than adopted as
  written.

## 9. Current controller direction

- **Conversion-specific evidence and relevance remain separate** from ordinary alpha
  analysis.
- **The future alpha-separation consumer combines them only when opaque conversion is
  actually being attempted**, rather than every analysis paying for conversion-only state.
- **`MaterialSemantics` remains unchanged.** Render state is not a shading output, and it
  gains no render-state fields.
- **Effective render facts matter more than `_Mode`** (§2).
- **The conversion remains pinned and Poiyomi-specific**, expressed as an AMUSE-owned
  version-pinned recipe derived from the attested source, with vendor source usable as a
  test oracle rather than invoked at build time (§4).
- **The source material is never touched.**
- **The generated material is a transient validated clone** (§5, §6).
- **No mesh, renderer, NDMF persistence, lilToon, planner, or texture-evidence
  implementation belongs on this branch.**

## 10. Unresolved design questions

- What the minimum generic render-state fact set actually is, once conversion-specific
  evidence is kept separate — in particular whether a `SurfaceMode`-style classification is
  worth having at all, or whether the explicit facts plus the §3 predicate suffice.
- Whether render-queue override identity is a fact or an implementation detail (§8).
- How conversion-relevant state is admitted without widening general relevance, given that
  the vendor marks most blend and depth properties non-animatable but leaves the mode, clip
  threshold and opacity flag animatable.
- How the §7 clip-threshold condition is discharged in practice: establishing the exact clip
  behavior from the pinned source, reading the effective threshold without assuming the
  declared range bounds it, deciding when clipping is inactive so the threshold can be
  ignored, and proving the condition holds across every admitted relevant state when it is
  animated. The requirement and the failure direction are settled — the condition is
  universal over admitted clipping states, and inability to establish it refuses — but the
  proving mechanism is open. Finite enumeration is the likeliest near-term fit for the
  existing machinery rather than a mandated architecture, and no alternative is proposed
  here.
- Whether an already-canonically-opaque source is a distinct successful outcome or a
  refusal.
- What validation can honestly claim. Comparing modeled semantic outputs before and after
  conversion proves the modeled equations agree; it is not a pixel-equivalence proof and
  must not be described as one.

## 11. Census Lab findings

Census Lab was used **read-only**; nothing was modified. The authorized private root is
`Assets/!CENSUSLAB/` and the authoritative corpus is `Assets/!CENSUSLAB/Scenes/`. The Lab's
location on disk is discovered at runtime and is never recorded here. Findings are
qualitative only; no corpus counts, ratios, cross-tabulations or per-entity observations are
published, and no new publishable metric was introduced.

- **Unlocked non-opaque Poiyomi candidates exist.** There is real material for this
  capability to act on; it is not a hypothetical transformation.
- **Locked and generated shader variants exist and remain refused.** Locking rewrites the
  material onto a generated shader, so such materials fail the pinned-source attestation
  before any render-state question is reached. That refusal is expected, not a gap to close
  here.
- **Real materials may diverge from `_Mode` presets** — the evidence behind §2.
- **Premultiplication and nonstandard blend modes are realistic refusal pressure**, not
  theoretical edge cases. Both appear in ordinary avatar materials, and both must refuse.
- Coverage and dithering mechanisms were not what stood in the way for the observed
  candidates.

## 12. Dependency direction

1. Poiyomi Replace / no-mask alpha semantics — **merged (PR #22)**.
2. Pinned Poiyomi opaque conversion — this note; not implemented.
3. Real runtime texture-evidence investigation — not started; gates assigned-mask alpha and
   any texture-backed triangle proof on real avatars.
4. Alpha-separation vertical slice — the eventual consumer.
