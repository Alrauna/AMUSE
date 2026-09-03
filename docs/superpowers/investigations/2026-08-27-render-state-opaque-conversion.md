# Render State / Opaque Conversion — Investigation

**Status: historical investigation. The capability is not implemented.**

- PR #22 **partially resolved its prior alpha-mask blocker** (merge commit
  `11765f4`). The PR proved that Poiyomi Replace mode has constant alpha when no `_AlphaMask` is assigned.
- **Assigned-mask cases remain blocked** on the real texture-evidence investigation.
- **The render-state capability itself remains unimplemented** and requires controller-reviewed design refinement. Review superseded several conclusions below.
  They are marked in §8 rather than deleted, so readers can inspect the reasoning.

This note deliberately distinguishes four content types: **verified evidence** (§2–§6),
**superseded conclusions** (§8), **current direction** (§9), and **unresolved design
questions** (§10). It is not an implementation specification. Do not read anything here as an approved requirement unless §9 says so.

Census Lab observations provide architectural pressure and validation evidence. They never
provide correctness authority. Pinned shader source and public deterministic tests provide correctness.

## 1. Blocking capability

Alpha separation must place proven-opaque triangles on a generated material that is
canonically opaque. AMUSE cannot produce that material today for four reasons. It cannot
represent a material's render mode or attest what "canonical opaque" means for the supported shader. Its evidence requests contain no conversion-relevant properties. It also has no generated-material construction or validation path.

## 2. Verified evidence — Poiyomi mode presets are declared in the attested source

Poiyomi declares its `_Mode` presets **inside the pinned shader source** as ThryEditor
`on_value_actions` metadata. The pinned Poiyomi Toon 9.3.64 identity is public and already
recorded in AMUSE's production attestation. Before reading any fact, investigators re-verified it by asset GUID and normalized source hash.

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

Two independent findings require this:

1. **Runtime.** `_Mode` is a live uniform, but it never reaches RGB. Its only reads occur in three places.
   They occur in an opaque branch with a commented-out body, a post-clip cutout alpha force, and an alpha-to-coverage sharpening branch.
   Opacity comes from `_AlphaForceOpaque`, the blend state, `ZWrite`, and `_Cutoff`.
2. **Reality.** Real materials in the authorized corpus do **not** reliably match preset
   tuples. For the same declared mode, custom render queues diverged from the preset value.
   The observed depth-write state also disagreed with the declared mode. Authors tune render state after they apply a preset.

**Classification must therefore read effective render state and never infer it from
`_Mode`.** A design that trusted `_Mode` would misclassify real materials.

## 3. Verified evidence — the opaque-equivalence predicate

The load-bearing generic fact is whether the blend equation degenerates to `dst := src`
when alpha is 1:

- `BlendOp == Add`, and
- `SrcBlend ∈ {One, SrcAlpha}` (both evaluate to 1 at α=1), and
- `DstBlend ∈ {Zero, OneMinusSrcAlpha}` (both evaluate to 0 at α=1).

This separates convertible Cutout / TransClipping / Fade from non-convertible
Additive / SoftAdditive / Multiplicative / 2xMultiplicative. Premultiplied alpha is
excluded separately. Premultiplication changes how the shader produces RGB, not how it combines RGB.

## 4. Verified evidence — Poiyomi's own conversion path is GUI-bound

The vendor's `SET_PROPERTY` action routes to a helper that first reads the
**active material inspector singleton**. It ignores the target array and writes to the material that the live inspector edits.
The inspector assigns that singleton only from its own GUI path. There is no headless entry point. An NDMF build pass has no inspector.

A build-time pass therefore cannot invoke the vendor conversion. It would also make Poiyomi a hard AMUSE dependency, which repository policy forbids for product code.

## 5. Verified evidence — clone fidelity

Investigators characterized this behavior in the public AMUSE Editor project. They confirmed instance identity by exact normalized data-path match before use.
`new Material(src)` copies the shader, all properties, texture scale/offset, the render-queue override, override tags, double-sided GI, instancing,
and shader keywords. In all tested fields, changes to the clone left the source unchanged.

One trap was recorded. `IsKeywordEnabled` returns false for a keyword that the material's shader
does not declare, even when the keyword list contains that keyword. A naive
keyword-preservation test will falsely fail if its fixture shader declares no keywords.

A material with no explicit queue override still reports an effective queue. Thus, "has an override" and "what queue applies" are separate questions.
See §8 for why review superseded the use of override identity as a generic render-state fact.

## 6. Verified evidence — NDMF generated-asset lifecycle

Investigators checked the installed NDMF source instead of making assumptions:

- the build context walks assets reachable from the avatar root at the end of the build and
  saves them, and
- the asset saver persists immediately when called directly, with no reachability check.

Thus, an eager save on an unreferenced clone leaves an abandoned asset in the build
output. A generated material should be a plain transient clone. Destroy it if preparation
later refuses. Let NDMF persist it after a renderer references it.

## 7. Verified evidence — visual/functional compatibility contract

This work does not attempt framebuffer equivalence. The bounded claim is:

> For a surface whose alpha is independently proven to be exactly 1 across every admitted
> runtime state, rendering it through the canonical opaque counterpart of its attested
> Poiyomi material is a supported normalization under the default policy.

Admission requires attested unlocked pinned Poiyomi and the §3 predicate. It also requires premultiplied alpha off, every coverage mechanism off, and proven conversion-relevant state (§10).

Under those conditions, the policy authorizes the queue move, the depth-write enable, and the
blend normalization. Forcing the shader's opacity flag is also a no-op on exactly the
triangle set that the consumer proved has alpha 1. Thus, the conversion and proof obligation align by construction.

### The alpha clip threshold needs its own proof

An earlier revision claimed that forcing the generated material's clip
threshold to zero is safe "because the clip is a threshold test and alpha is proven 1: the
fragment survived the original threshold and survives zero." **That reasoning is wrong and
is withdrawn.** Proving that alpha is exactly 1 does not prove that the fragment survived its original
threshold. A clip compares alpha against the threshold. Therefore, a sufficiently high threshold
discards the fragment *even at alpha 1*. The threshold, not alpha, determines whether the original surface was visible.

The required condition is therefore:

> For every admitted runtime state in which the pinned shader actually performs alpha
> clipping, AMUSE must establish that the effective clip behavior is known and that alpha
> exactly 1 is not discarded.

That condition is the correctness requirement. It does not specify *how* to prove it. This note deliberately does not specify a mechanism.

At minimum, proof requires the **exact clip behavior** from the pinned
source. Determine which term it compares, its comparison target, and the comparison strictness. Do not assume a particular form.

**Do not assume that serialized values lie within the inspector's declared range.** The
vendor declares a bounded threshold range. However, that range constrains the inspector widget, not values serialized into the material, written by script, or driven by animation.
Eligibility must use the effective value that is present. It must not use the range that the shader advertises.

**Unknown, non-finite, or unsafe threshold states refuse.** Refuse if the threshold cannot be read,
is not finite, or does not let alpha 1 survive. Do not approximate these cases.

**An animated threshold is admissible only when the condition can be proven for every
admitted relevant state.** Refuse if that universal condition cannot be established for any reason.
This is the fail-closed direction. An animated threshold can legitimately hide geometry. A conversion that pins the threshold to zero would restore geometry that the author deliberately hid.

Finite enumeration of admitted values could discharge that obligation with the current admitted-state machinery. It is the most likely near-term mechanism.
It is **not** the requirement. A future proof can use another representation if it soundly establishes the same universal condition over the admitted set.
Nothing in this note excludes such a proof. This note proposes or designs no such mechanism.

**When alpha clipping is inactive in the pinned source state, the threshold is irrelevant**
and must not cause refusal. Nothing is compared. Thus, an unknown, out-of-range, or
animated threshold cannot change the result. Treating it as a blocker would cause an unnecessary refusal, not conservatism.

Set the generated opaque material's threshold to zero **only once those conditions are established**.
This setting is a consequence of the proof, not a premise.

This note does not cover the design of evidence machinery that discharges these obligations.
Section §10 records it as unresolved.

## 8. Superseded conclusions and portions under controller review

This investigation proposed these items, but they are **not** approved requirements. They remain only as reasoning history.

- **Globally widening ordinary alpha-analysis relevance with conversion-only properties.**
  Superseded. Adding conversion-only render-state properties to the general alpha
evidence request would make unrelated analysis refuse on state that it does not use. See
  §9 for the current direction.
- **Treating serialized custom-render-queue override identity as necessarily part of
  generic render-state facts.** Under review. It is unresolved whether "an override exists" is a generic
  fact or only an implementation detail of reading the effective queue.
- **NDMF persistence integration tests on this prerequisite branch.** Superseded. The
  lifecycle finding in §6 is real. However, the consumer that assigns a generated material to a renderer should test NDMF persistence, not this branch.
- **Census launcher or new Census metric implementation.** Superseded. Do not
  build a launcher or introduce a new publishable metric for this work.
- **Any global render-state framework.** Superseded. One shader, one direction, and one
  version do not justify a general model, a mode graph, or pluggable adapters.
- **The proposed failure taxonomy, validation model and test strategy** were drafted before
  the above supersessions. Re-derive them against §9 instead of adopting them as
  written.

## 9. Current controller direction

- **Conversion-specific evidence and relevance remain separate** from ordinary alpha
  analysis.
- **The future alpha-separation consumer combines them only when it
  actually attempts opaque conversion.** Unrelated analysis does not process conversion-only state.
- **`MaterialSemantics` remains unchanged.** Render state is not a shading output. It
  gains no render-state fields.
- **Effective render facts matter more than `_Mode`** (§2).
- **The conversion remains pinned and Poiyomi-specific.** It uses an AMUSE-owned,
  version-pinned recipe derived from the attested source. Vendor source can serve as a
test oracle, but product code does not invoke it at build time (§4).
- **The source material is never touched.**
- **The generated material is a transient validated clone** (§5, §6).
- **No mesh, renderer, NDMF persistence, lilToon, planner, or texture-evidence
  implementation belongs on this branch.**

## 10. Unresolved design questions

- What is the minimum generic render-state fact set when conversion-specific evidence remains separate?
  Specifically, is a `SurfaceMode`-style classification useful, or do the explicit facts and §3 predicate suffice?
- Is render-queue override identity a fact or an implementation detail (§8)?
- How can conversion-relevant state be admitted without widening general relevance? The
  vendor marks most blend and depth properties non-animatable. However, it marks the mode, clip
  threshold, and opacity flag animatable.
- How is the §7 clip-threshold condition discharged in practice? This requires establishing exact clip
  behavior from the pinned source and reading the effective threshold without assuming the declared range bounds it.
  It also requires deciding when clipping is inactive, so the threshold can be ignored. For animation, the condition must hold across every admitted relevant state.
  The requirement and failure direction are settled. The condition is universal over admitted clipping states, and inability to establish it causes refusal.
  Finite enumeration is the most likely near-term fit for existing machinery, but it is not a mandated architecture. This note proposes no alternative.
- Is an already-canonically-opaque source a separate successful outcome or a
  refusal?
- What can validation honestly claim? Comparing modeled semantic outputs before and after
  conversion proves that the modeled equations agree. It is not a pixel-equivalence proof. Do not describe it as one.

## 11. Census Lab findings

Census Lab was used **read-only**. Nothing was modified. The authorized private root is
`Assets/!CENSUSLAB/`, and the authoritative corpus is `Assets/!CENSUSLAB/Scenes/`. The Lab's
location on disk is discovered at runtime and is never recorded here. Findings are
qualitative only. No corpus counts, ratios, cross-tabulations, or per-entity observations are
published. No new publishable metric was introduced.

- **Unlocked non-opaque Poiyomi candidates exist.** This capability has real material to
  process. It is not a hypothetical transformation.
- **Locked and generated shader variants exist and remain refused.** Locking rewrites the
  material onto a generated shader. Thus, such materials fail the pinned-source attestation
  before any render-state question is reached. That refusal is expected. It is not a gap to close
  here.
- **Real materials may diverge from `_Mode` presets.** This is the evidence behind §2.
- **Premultiplication and nonstandard blend modes are realistic refusal pressure**, not
  theoretical edge cases. Both occur in ordinary avatar materials, and both must refuse.
- Coverage and dithering mechanisms did not block the observed
  candidates.

## 12. Dependency direction

1. Poiyomi Replace / no-mask alpha semantics: **merged (PR #22)**.
2. Pinned Poiyomi opaque conversion: this note. Not implemented.
3. Real runtime texture-evidence investigation: not started. It gates assigned-mask alpha and
   all texture-backed triangle proofs on real avatars.
4. Alpha-separation vertical slice: the eventual consumer.
