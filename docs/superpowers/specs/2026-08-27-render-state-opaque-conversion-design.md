# Pinned Poiyomi Opaque Conversion — Design

**Status: proposed design, not implemented. Design-only. This design produced no production code or tests.**

Prerequisite branch: `feat/render-state-opaque-conversion`, recreated from `main` at
`18b62d6`.

This specification designs one bounded capability: given a material whose shader is the
attested unlocked pinned Poiyomi Toon 9.3.64 source, decide whether that material may be
normalized to its canonical Opaque counterpart, and if so produce a transient validated
clone carrying the complete version-pinned canonical Opaque tuple.

It designs nothing else. It does not prove alpha, touch a mesh, a renderer, a submesh or a
material slot, integrate NDMF persistence, gather texture evidence, extend lilToon, run an
animation pipeline, or implement any part of the alpha-separation vertical slice.

Prior art this refines rather than repeats:
`docs/superpowers/investigations/2026-08-27-render-state-opaque-conversion.md` (the
investigation) and `docs/superpowers/investigations/2026-08-27-alpha-separation-vertical-slice.md`
(the eventual consumer). §10 corrects several investigation claims against re-verified
pinned source.

---

## 1. Verified basis

### 1.1 Source identity re-verified before any fact was read

AMUSE located the pinned Poiyomi Toon 9.3.64 shader asset in a vendor package by asset GUID.
It confirmed the asset against the production attestation constants:

| Check | Pinned constant | Observed |
|---|---|---|
| package name | `com.poiyomi.toon` | matches |
| package version | `9.3.64` | matches |
| shader asset GUID | `9444ce77bf4418748b1e8591b9d97f85` | matches |
| normalized source hash | `31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755` | matches |

AMUSE recomputed the hash with its own normalization
(`PoiyomiMaterialSemantics.ComputeNormalizedSourceHash`: strip a leading BOM, fold CRLF and
lone CR to LF, SHA-256 the UTF-8 bytes). Every fact in §1.2–§1.8 comes from that exact file.
We read the vendor source read-only. We modified nothing, copied nothing into this
repository, and made nothing a build dependency.

### 1.2 The complete canonical Opaque tuple

The vendor declares the presets of `_Mode` as ThryEditor `on_value_actions` metadata inside the
attested shader source. The Opaque preset (`value:0`) carries **24 actions**: 22 material
properties, one render queue, one shader tag override. Selecting the preset also sets
`_Mode` itself, so the complete recipe is **23 properties + queue + tag = 25 facts**.

Material properties written by the recipe:

| Property | Canonical value | Property | Canonical value |
|---|---|---|---|
| `_Mode` | 0 | `_AddSrcBlend` | 1 |
| `_AlphaForceOpaque` | 1 | `_AddDstBlend` | 1 |
| `_BlendOp` | 0 | `_AddSrcBlendAlpha` | 0 |
| `_BlendOpAlpha` | 4 | `_AddDstBlendAlpha` | 1 |
| `_Cutoff` | 0 | `_AlphaToCoverage` | 0 |
| `_SrcBlend` | 1 | `_ZWrite` | 1 |
| `_DstBlend` | 0 | `_ZTest` | 4 |
| `_SrcBlendAlpha` | 1 | `_AlphaPremultiply` | 0 |
| `_DstBlendAlpha` | 1 | `_OutlineSrcBlend` | 1 |
| `_OutlineDstBlend` | 0 | `_OutlineSrcBlendAlpha` | 1 |
| `_OutlineDstBlendAlpha` | **0** | `_OutlineBlendOp` | 0 |
| `_OutlineBlendOpAlpha` | 4 | | |

Non-property state:

| Fact | Canonical value | Unity expression |
|---|---|---|
| render queue | 2000 | `material.renderQueue` |
| `RenderType` tag | `Opaque` | `material.SetOverrideTag("RenderType", "Opaque")` |

Two details matter and are easy to lose:

- **`_OutlineDstBlendAlpha = 0` is unique to the Opaque preset.** Every one of the other
  eight presets sets it to 1. It is the single field that most cheaply distinguishes a
  correct recipe from one assembled by copying a neighbouring preset.

- **`_AddBlendOp` and `_AddBlendOpAlpha` appear in no action list of any preset.** The recipe
  therefore does not write them, and neither does AMUSE. The same holds for
  `_OutlineZWrite`, `_OutlineZTest` and `_OutlineCull`, which the vendor declares but no
  preset sets. Because conversion never writes them, they are not conversion
  dependencies either (§1.7, §5.4).

### 1.3 The render-state properties are pass-wired exactly as expected

One SubShader, five passes, no `Fallback` shader.

| Pass | Render state read from |
|---|---|
| `EarlyZ` | fixed `ZWrite On`, `ColorMask 0`, `Cull [_Cull]` |
| `Base` | `ZWrite [_ZWrite]`, `ZTest [_ZTest]`, `AlphaToMask [_AlphaToCoverage]`, `BlendOp [_BlendOp],[_BlendOpAlpha]`, `Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]` |
| `Add` | `BlendOp [_AddBlendOp],[_AddBlendOpAlpha]`, `Blend [_AddSrcBlend] [_AddDstBlend], [_AddSrcBlendAlpha] [_AddDstBlendAlpha]` |
| `Outline` | `ZWrite [_OutlineZWrite]`, `ZTest [_OutlineZTest]`, `BlendOp [_OutlineBlendOp],[_OutlineBlendOpAlpha]`, `Blend [_OutlineSrcBlend] [_OutlineDstBlend], [_OutlineSrcBlendAlpha] [_OutlineDstBlendAlpha]` |
| `ShadowCaster` | `ZWrite [_ZWrite]`, `ZTest [_ZTest]`, `Blend [_SrcBlend] [_DstBlend], …` |

The SubShader tags are `RenderType = Opaque`, `Queue = Geometry`,
`VRCFallback = Standard`. A material with no queue override therefore already reports an
effective queue of 2000.

### 1.4 The alpha clip is unconditional

`clip(poiFragData.alpha - _Cutoff);` appears in the `Base`, `Add`, `Outline` and
`ShadowCaster` passes. **In none of them is it gated** — not on `_Mode`, not on a feature
toggle, not behind a preprocessor branch. There is no admitted state of this shader in which
alpha clipping is inactive.

Consequences:

1. The investigation states a branch: "when alpha clipping is inactive the threshold is irrelevant and must
   not cause refusal". That branch is **vacuous for this shader**. It stays correct as a general
   rule. We deliberately do not implement it, because an unreachable branch would be
   speculative infrastructure.

2. The clip-threshold obligation is **always live**. Eligibility can never skip it.

HLSL `clip(x)` discards when `x < 0`. A fragment survives iff `alpha - _Cutoff >= 0`, i.e.
`alpha >= _Cutoff`. At the proven alpha of exactly 1, survival is exactly `_Cutoff <= 1`.

### 1.5 The declared cutoff range is itself unsafe

```
_Cutoff ("Alpha Cutoff", Range(0, 1.001)) = 0.5
```

The vendor declares a maximum of **1.001**, not 1. A material whose `_Cutoff` sits at the
top of its own inspector-declared range discards alpha exactly 1. The investigation warned
that a declared range constrains the widget and not what Unity can serialize. The stronger
fact is that this declared range *already admits a discarding value*.

`_Cutoff` carries no `[DoNotAnimate]` attribute. `_AlphaForceOpaque`, `_AlphaToCoverage`,
`_AlphaPremultiply` and `_Mode` likewise carry none. The blend, depth and `_EnableOutlines`
properties do carry `[DoNotAnimate]` — but that is a ThryEditor GUI hint that suppresses
inspector recording, not a runtime guarantee about what a committed animator graph holds.
**This design never treats `[DoNotAnimate]` as evidence.**

### 1.6 Every runtime read of `_Mode`, enumerated

| Site | Effect | Status at base alpha 1, outlines disabled |
|---|---|---|
| `Base` pass, `if (_Mode == OPAQUE)` | body is commented out | no effect |
| `Add`, `Outline`, `ShadowCaster`, `if (_Mode == OPAQUE)` | `alpha = 1` before the clip | identity — but see §1.8 for the `Outline` pass |
| `if (_Mode == CUTOUT && !_AlphaToCoverage)` | `alpha = 1` after the clip | identity |
| `Add`, `if (_Mode != TRANSPARENT)` | `finalColor *= alpha` | identity |
| `ApplyAlphaToCoverage`, `if (_Mode == 1)` | A2C sharpening | unreachable: eligibility requires `_AlphaToCoverage == 0` |

Writing `_Mode = 0` is an identity **conditional on** the consumer proof that alpha ≡ 1 over
the same admitted states **and** on outlines that stay disabled. Both conditions are load-bearing.

### 1.7 The ForwardAdd pass has its own blend state, and its op is never written

```
[DoNotAnimate][Enum(Thry.BlendOp)] _AddBlendOp    ("RGB Blend Op", Int) = 4
[DoNotAnimate][Enum(...BlendMode)] _AddSrcBlend   ("RGB Source Blend", Int) = 1
[DoNotAnimate][Enum(...BlendMode)] _AddDstBlend   ("RGB Destination Blend", Int) = 1
```

We read `Thry.BlendOp` from the vendor file `ThryWideEnum.cs`, and it matches
`UnityEngine.Rendering.BlendOp` ordering: `Add=0, Subtract=1, ReverseSubtract=2, Min=3,
Max=4`. So the serialized default of **`_AddBlendOp` is 4 (Max)**, and no preset action
changes it — including the Opaque preset. The recipe rewrites only the ForwardAdd *factors*,
so the op is identical on both sides of the conversion and cancels out of any comparison
between them. §5.4 records why that makes `_AddBlendOp` a non-dependency rather than a gate.

The `Add` pass also contains `if (_AddBlendOp == 4) { poiFragData.alpha =
saturate(poiFragData.alpha * _AlphaBoostFA); }`. The recipe writes neither
`_AddBlendOp` nor `_AlphaBoostFA`, so this branch behaves identically before and after
conversion and is not a conversion concern.

### 1.8 The outline pass writes alpha, and AMUSE does not model it

`applyOutlineColor` — called at the top of the `Outline` pass fragment — does, in source
order:

1. `clip(_EnableOutlines - 0.01);`
2. samples `_OutlineMask` and optionally `clip(OutlineMask * lineWidth - 0.001)`;
3. samples `_OutlineTexture` into `col` and blends `poiFragData.baseColor`;
4. **`if (_OutlineOverrideAlpha) poiFragData.alpha = col.a; else poiFragData.alpha *= col.a;`**
5. `if (_OutlineAlphaDistanceFade) poiFragData.alpha *= lerp(…smoothstep(distance…));`

Later in the same pass, at source order after `applyOutlineColor` returns:

```
if (_Mode == POI_MODE_OPAQUE) { poiFragData.alpha = 1; }
clip(poiFragData.alpha - _Cutoff);
```

**This is why enabled outlines must refuse.** The `MaterialSemantics.Alpha` model of AMUSE
covers the base-material alpha equation. It models none of steps 2–5: not the outline mask,
not the outline texture or line-colour alpha, not the distance fade. A proof that base alpha
≡ 1 therefore says nothing about the alpha the `Outline` pass carries into its own clip.

Writing `_Mode = 0` would force that unmodelled alpha to 1 before the clip — resurrecting
outline fragments the author faded or clipped away. That is a false positive in exactly the
direction the correctness policy forbids. Base alpha of exactly 1 does not fix it.

The shader declares `_EnableOutlines` as `[DoNotAnimate][HideInInspector][ToggleUI] … = 0`.
It serves both as the `applyOutlineColor` clip term and as a vertex offset scale
(`lineWidth * _EnableOutlines / 100`).

**When `_EnableOutlines` is exactly 0**, `clip(_EnableOutlines - 0.01)` discards every
outline fragment at the first statement of `applyOutlineColor`, before any of steps 2–5 run,
and the vertex offset is zero. The outline pass then contributes nothing to visible output,
so applying the canonical outline blend tuple of the vendor is irrelevant rather than merely
harmless. That is the narrow safe rule this design adopts (§5.3).

---

## 2. The conversion contract

> For a surface whose alpha is independently proven to be exactly 1 across every admitted
> runtime state, rendering it through the canonical Opaque counterpart of its attested
> Poiyomi material is a supported normalization under the default policy.

Conversion **does not prove alpha**. It consumes that proof as a precondition supplied by
the consumer.

The cutoff correctness condition, restated in the form this design discharges:

> For every admitted runtime state in which the pinned shader performs alpha clipping, AMUSE
> must establish that it knows the effective clip behavior and that the clip does not
> discard alpha exactly 1.

Given §1.4 (clipping always active) and §1.5 (`_Cutoff <= 1` is the exact survival
condition), the discharge is: **`_Cutoff` must be present, finite, and `<= 1`**. Nothing
weaker. This design introduces no interval, symbolic or richer-admission machinery to say it.

This is not framebuffer equivalence. This design must never describe it as one.

### 2.1 What conversion changes, and by what authority

Policy-authorized normalizations, retained:

| Change | Authority |
|---|---|
| effective queue → 2000 | default policy authorizes the queue move |
| `RenderType` tag → `Opaque` | part of the canonical tuple |
| `_ZWrite` → 1 | default policy authorizes enabling depth write |
| `_Cutoff` → 0 | justified only *after* `_Cutoff <= 1` is established (§5.3) |
| base RGB blend → `One, Zero` | justified by the §5.3 opaque-equivalence predicate at α = 1 |
| the complete vendor recipe | applied once every prerequisite is established |

Changes conversion deliberately does **not** make:

| Preserved | Why |
|---|---|
| `_ZTest` (value) | the recipe writes `4`, but eligibility requires the material *already* be `4`, so the write is a no-op and the comparison is never changed: a different depth comparison changes visibility independently of alpha (§5.3) |
| `_AddBlendOp` | not in the vendor recipe; preserved unchanged, along with any animation of it (§5.4) |
| `_OutlineZWrite`, `_OutlineZTest`, `_OutlineCull` | not in the vendor recipe; preserved unchanged |
| outline alpha behaviour | not modelled; enabled outlines refuse (§1.8) |

---

## 3. Architectural boundaries

### 3.1 `MaterialSemantics` remains unchanged

Render state is not a shading output. `MaterialSemantics` stays exactly
`{ BaseColor, Alpha, Emission, Normal }`. Semantics describe output facts. Conversion
decides mutation. Nothing here adds a field, a `SurfaceMode` classification, or a
render-state member to any semantics type.

### 3.2 Conversion evidence is separate from ordinary alpha evidence

`UnityAnimationEvidenceCapture.CaptureObserved` builds one **closed** request —
`MaterialEvidenceRequest.Combine` over the family alpha request of each admitted material —
and that single `CapturedAnimationEvidence.RelevanceRequest` is used for two different things:

1. capturing the evidence of every admitted material, and
2. deciding, in `ResolveProofRelevant`, whether an animated binding is `Irrelevant`,
   `RendererWide`, or `UnrecognizedMaterialBinding`.

Because of (2), **widening the closed request widens what counts as proof-relevant
animation** for every renderer, including ones no conversion will ever touch. A material
with an animated `material._ZWrite` would start to fail ordinary alpha analysis on state that
alpha does not depend on. That is a coverage regression, not a safety improvement.

Therefore conversion owns its own `MaterialEvidenceRequest`. Ordinary alpha analysis never
sees it. `MaterialEvidenceRequest.Combine` remains the composition seam, exercised by the
consumer (§7), not here.

### 3.3 Unknown conversion-relevant state refuses only conversion

A material whose `_Cutoff` is 1.001, whose `_EnableOutlines` is 1, or whose `_ZTest` is 8
yields a **conversion refusal**. Its alpha analysis result stays the same. The refusal
vocabularies are disjoint types (§4.1) precisely so that no conversion condition can leak
into `RendererAnalysisRefusal` and start refusing analysis.

### 3.4 The effective queue is read directly, not through the evidence request

The effective render queue is `material.renderQueue`. It is not a shader property, and
adding a `renderQueue` flag to `MaterialEvidenceRequest` would be wrong for a stronger
reason than inconvenience:

**The queue is not animation-reachable.** The material animation binding syntax of Unity is
`material.<PropertyName>`. No binding form addresses the render queue of a material, and
`Renderer` exposes none either. `MaterialEvidenceRequest` exists to close the set of
*animation-relevant* facts. The same argument applies to the `RenderType` override tag.

This resolves the open question of the investigation about render-queue override identity:
**"an override exists" is an implementation detail, not a fact this design models.**
`material.renderQueue` already resolves an absent override to the queue the shader declares.

| Class | Facts | Read by | Animation-reachable |
|---|---|---|---|
| property facts | the 24 conversion-read properties | conversion `MaterialEvidenceRequest` | yes |
| non-property facts | effective queue, `RenderType` tag | read directly from the `Material` | no |

### 3.5 `_Mode` is a preset hint, never authoritative

Eligibility reads effective outline, blend, depth, coverage, premultiply and clip facts. It
**never consults `_Mode` to decide anything.** `_Mode` appears only as a recipe field that
conversion writes. A test pins this directly (§8.5).

### 3.6 This branch contains no animation runner

Relevance requires a renderer path. Correct admission requires material-swap closure and
per-slot resolution. Both already exist, in `UnityAnimationEvidenceCapture` and
`AdmittedMaterialStates`. A conversion-local reimplementation would duplicate them and would
be incomplete besides.

So this branch ships **no** binding lists, additive-layer flags, admission pipeline, or
conversion-specific animation refusals. Its evaluator is pure over *already captured and
already admitted* evidence. §7 specifies the obligations of the consumer, and the consumer
implements them there.

---

## 4. Surface
One new production file, `Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`, all
`internal`. Poiyomi-specific behaviour stays in the Poiyomi frontend directory.
`Editor/Analysis/` holds shader-independent proof machinery and gains nothing here.

### 4.1 Types

```csharp
internal enum PoiyomiOpaqueConversionOutcome
{
    Refused,
    AlreadyOpaque,   // successful no-op; no clone is created
    Convertible,     // eligibility passed; preparation may proceed
}

internal enum PoiyomiOpaqueConversionRefusal
{
    None,

    // Identity (produced by the attestation step, not by the evaluator)
    UnattestedMaterial,

    // Schema / readability
    ConversionPropertyAbsent,
    ConversionPropertyNotFinite,

    // Effective render-state eligibility
    OutlinesEnabled,
    PremultipliedAlphaEnabled,
    AlphaToCoverageEnabled,
    UnsupportedDepthComparison,
    UnsupportedBlendEquation,
    UnsupportedForwardAddBlendEquation,
    ClipThresholdDiscardsOpaqueAlpha,
}

internal readonly struct PoiyomiOpaqueConversionEligibility
{
    internal PoiyomiOpaqueConversionOutcome Outcome { get; }
    internal PoiyomiOpaqueConversionRefusal Refusal { get; }
}
```

A separate enum, not a `RendererAnalysisRefusal` extension. These are conversion decisions
about one material. Merging them would put conversion conditions where analysis reads them.

There is **no** `GeneratedMaterialNotCanonical` member and there are **no** animation
members. Validation failure is a defect (§6.2). Animation belongs to the consumer (§7).

### 4.2 Entry points

```csharp
/// The closed conversion request: independently sufficient for conversion
/// source attestation and conversion eligibility.
internal static readonly MaterialEvidenceRequest ConversionEvidenceRequest;

/// The recipe, as data, so callers need not restate it.
internal static IReadOnlyList<(string Property, float Value)> CanonicalOpaqueProperties { get; }
internal const int    CanonicalOpaqueRenderQueue = 2000;
internal const string RenderTypeTagName          = "RenderType";
internal const string CanonicalOpaqueRenderType  = "Opaque";

/// Narrow conversion entry to the shared Poiyomi source-evidence gatherer.
/// Verification stays PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity.
internal static PoiyomiSourceEvidence GatherConversionSourceEvidence(
    Shader shader, CapturedMaterialEvidence evidence);

/// Effective non-property render state, read directly from the material.
internal static void ReadEffectiveRenderState(
    Material material, out int renderQueue, out string renderType);

/// Pure evaluation over already-captured, already-admitted evidence.
internal static PoiyomiOpaqueConversionEligibility EvaluateVerifiedEligibility(
    CapturedMaterialEvidence evidence,
    int effectiveRenderQueue,
    string effectiveRenderType);

/// Clone, apply, re-read, validate. Destroys the clone and throws on an
/// invariant failure; never returns a partially prepared material.
internal static Material PrepareCanonicalOpaqueClone(Material source);

/// First canonical fact the candidate disagrees with, if any. Used by
/// preparation to name the invariant it violated, and by tests to assert
/// which perturbation was detected.
internal static bool TryFindNonCanonicalFact(Material candidate, out string factName);
```

`EvaluateVerifiedEligibility` is not a new test-only seam. It is the same
identity-gate/interpretation split the Poiyomi and lilToon frontends already use
(`TryVerifyPoiyomiIdentity` gating `InterpretVerifiedAlpha`), for the same reason: the public
project ships no vendor shader, so attestation and interpretation must be separately
reachable.

It has a production caller in the sequence of the consumer (§7).

---

## 5. Evidence and eligibility

### 5.1 The conversion evidence request

Conversion attestation must close over its own inputs. It must **not** be reached by running
the alpha evidence capture path, which would couple conversion to a request it does not own
and would not carry the schema of conversion.

```csharp
ConversionEvidenceRequest = new MaterialEvidenceRequest(
    shaderName:         true,                              // identity
    activeColorSpace:   false,
    presenceProperties: ConversionRequiredSchemaProperties, // the 24 below
    scalarProperties:   <25 names>,                        // the 24 + _ShaderOptimizerEnabled
    colorProperties:    Array.Empty<string>(),
    vectorProperties:   Array.Empty<string>(),
    textureProperties:  Array.Empty<TexturePropertyEvidenceRequest>());
```

The 24 conversion-read properties:

| Group | Properties |
|---|---|
| recipe (23, §1.2) | `_Mode`, `_AlphaForceOpaque`, `_BlendOp`, `_BlendOpAlpha`, `_Cutoff`, `_SrcBlend`, `_DstBlend`, `_SrcBlendAlpha`, `_DstBlendAlpha`, `_AddSrcBlend`, `_AddDstBlend`, `_AddSrcBlendAlpha`, `_AddDstBlendAlpha`, `_AlphaToCoverage`, `_ZWrite`, `_ZTest`, `_AlphaPremultiply`, `_OutlineSrcBlend`, `_OutlineDstBlend`, `_OutlineSrcBlendAlpha`, `_OutlineDstBlendAlpha`, `_OutlineBlendOp`, `_OutlineBlendOpAlpha` |
| eligibility-only (1) | `_EnableOutlines` (§1.8) |

`_ShaderOptimizerEnabled` is a scalar rather than a schema entry because the shared
source-evidence gatherer reads it with `TryGetScalar` to detect the locked state.

**Two sets, deliberately distinct.** The *24 conversion-read properties* above are what
eligibility reads. The *25 canonical facts* used by `AlreadyOpaque` (§5.5) and validation
(§6.2) are the 23 recipe properties plus the effective queue plus the `RenderType` tag.
Conversion reads `_EnableOutlines` but never writes it. It writes the queue and tag, and
they are not properties.

`_AlphaToCoverage` and `_AlphaPremultiply` also appear in the gate sets of
`PoiyomiMaterialSemantics.AlphaEvidenceRequest`. That overlap is benign and
`Combine` handles it: in the composed pipeline these two cost conversion zero additional
coverage, because an attested alpha proof already forced both to zero.

**Relevance follows the request.** Because the design computes relevance from the request
(`ResolveProofRelevant(binding, path, request, …)`), listing `_EnableOutlines`
in `ConversionEvidenceRequest` is exactly what makes a curve on it conversion-relevant. No
separate relevance list exists or should exist. `_AddBlendOp` is deliberately absent, so a
curve on it is `Irrelevant` to conversion — correctly, since conversion neither reads nor
writes it.

### 5.2 Attestation, closed independently

Conversion attestation is:

1. capture the material with `ConversionEvidenceRequest`.
2. `GatherConversionSourceEvidence(shader, evidence)` — a thin wrapper on
   `PoiyomiOpaqueConversion` passing `ConversionRequiredSchemaProperties` to the existing
   parameterized `PoiyomiMaterialSemantics.GatherSourceEvidence`, exactly as
   `GatherAlphaSourceEvidence` passes the alpha schema. That gatherer is currently `private`
   and becomes assembly-`internal` so the conversion-owned wrapper can call it. Its body,
   signature and every existing caller are unchanged. The conversion class owns its own
   schema array.
3. `PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(evidence, out _)`, reused unchanged.

No hashing, GUID lookup, package check, locked-state check or identity conjunction is
duplicated. Failure is `UnattestedMaterial`. A locked Poiyomi material fails here, before any
render-state question — a correct expected refusal, never a render-state finding.

Because `ConversionRequiredSchemaProperties` is the same 24 names eligibility reads,
attestation already establishes their presence in the production sequence. The evaluator
still returns `ConversionPropertyAbsent` because it is a pure function that does not assume
that its caller attested. In production that member is defence in depth rather than the
normal path.

### 5.3 Evaluation order

The design evaluates gates in this order. The first failure is the reported refusal. The order is
load-bearing: **the no-op classification precedes every gate whose only purpose is to
authorize mutation.**

1. **Schema.** Every one of the 24 conversion-read properties must be readable —
   `TryGetScalar` false → `ConversionPropertyAbsent`. In production, attestation (§5.2) has
   already established this. The evaluator repeats it because it does not assume its caller
   attested.
2. **`AlreadyOpaque`.** If all 25 canonical facts already match, return `AlreadyOpaque` and
   stop. No clone, no further gate. See §5.5.

Everything below is a **transformation gate**: it exists to authorize changing the material,
and it is therefore unreachable once step 2 established that nothing will change.

3. **Finiteness.** Every one of the 24 values must be finite → `ConversionPropertyNotFinite`.
   Non-finite render state is not a case to approximate.
4. **Outlines.** `_EnableOutlines != 0f` → `OutlinesEnabled`. Required exactly 0, per §1.8.
   Enabled, non-zero, unknown, non-finite, or unadmitted outline state all refuse — the
   unknown and non-finite cases via steps 1 and 3, the unadmitted case at the consumer (§7).
5. **Premultiplied alpha.** `_AlphaPremultiply != 0` → `PremultipliedAlphaEnabled`.
   Premultiplication changes how RGB is *produced*, not how it is combined, so the blend
   predicate cannot excuse it.
6. **Coverage.** `_AlphaToCoverage != 0` → `AlphaToCoverageEnabled`.
7. **Depth comparison.** `_ZTest != 4` → `UnsupportedDepthComparison`. Conversion requires
   the material already use `LEqual` rather than normalizing to it, because a different
   depth comparison changes visibility independently of alpha. A material authored to draw
   with `Always`, `Greater` or `Disabled` expresses a visibility intent the alpha proof
   knows nothing about. The recipe still writes `4`, and on an eligible material that write
   is a no-op, which is the point.
8. **Base RGB blend.** The opaque-equivalence predicate on the `Base`/`ShadowCaster` state:
   - `_BlendOp == 0` (Add), and
   - `_SrcBlend ∈ {1 (One), 5 (SrcAlpha)}`, and
   - `_DstBlend ∈ {0 (Zero), 10 (OneMinusSrcAlpha)}`.

   Both accepted source factors evaluate to 1 and both accepted destination factors to 0 at
   α = 1, so the blend degenerates to `dst := src` and normalizing to `One, Zero` is an
   identity there. Anything else → `UnsupportedBlendEquation`. This separates convertible
   Cutout / TransClipping / Fade from non-convertible Additive / SoftAdditive /
   Multiplicative / 2xMultiplicative without consulting `_Mode`.
9. **ForwardAdd RGB blend factors.** The `Add` pass has independent blend state (§1.3) whose
   *factors* the recipe rewrites to `One, One`, so those factors need their own predicate:
   - `_AddSrcBlend ∈ {1 (One), 5 (SrcAlpha)}`, and
   - `_AddDstBlend == 1 (One)`.

   At α = 1 the accepted source factors evaluate to 1 and the accepted destination factor to
   1, so the accepted states are equivalent to the canonical `One, One` tuple. Anything else
   → `UnsupportedForwardAddBlendEquation`. The blend *operation* is not constrained (§5.4).
   **This is a pinned Poiyomi predicate over two named passes, not a pass model**. Nothing
   here generalizes to arbitrary passes, and this design introduces no pass abstraction.
10. **Clip threshold.** `_Cutoff > 1f` → `ClipThresholdDiscardsOpaqueAlpha`. Step 3 already
    established finiteness. `_Cutoff < 0` is eligible, since α = 1 trivially survives.
    Only once this passes is writing `_Cutoff = 0` justified — it is a consequence of the
    proof, not a premise of it.

**Read but never gated**: `_ZWrite`, `_AlphaForceOpaque`, the alpha-channel blend fields the
recipe writes (`_SrcBlendAlpha`, `_DstBlendAlpha`, `_BlendOpAlpha`, `_AddSrcBlendAlpha`,
`_AddDstBlendAlpha`), and the six `_Outline*` blend fields. Policy (§2.1) authorizes depth
write and the opacity flag as normalizations. The alpha-channel fields govern the
destination alpha channel, which the compatibility contract does not constrain. The outline
blend fields are irrelevant once step 4 established that no outline fragment survives.

**Not read at all**: `_AddBlendOp` and `_AddBlendOpAlpha`, which the recipe never writes and
which therefore cancel from the comparison (§5.4).

### 5.4 Why `_AddBlendOp` is not a conversion dependency

The canonical Opaque recipe writes `_AddSrcBlend`, `_AddDstBlend`, `_AddSrcBlendAlpha` and
`_AddDstBlendAlpha`. It does **not** write `_AddBlendOp` or `_AddBlendOpAlpha` (§1.2, §1.7).

The `Add` pass computes `op(src · S, dst · D)`, where `op` is `_AddBlendOp`. Conversion
leaves `op` untouched, so it is the same function on both sides of the comparison. Once step
9 proved that the factors the recipe *does* change are equivalent at α = 1 —
`S` evaluating to 1 and `D` to 1 — the operands are identical, and an unchanged `op` applied
to identical operands yields identical results. **The operation cancels.**

Therefore `_AddBlendOp` is not read, not gated, not in `ConversionEvidenceRequest`, not in
`ConversionRequiredSchemaProperties`, and not conversion-relevant. Conversion likewise
preserves a curve that animates it: the curve drives the generated material exactly as it
drove the source, through the same unchanged operation, over operands the factor predicate
already proved identical. There is nothing for conversion to depend on.

The same reasoning covers `_AddBlendOpAlpha`, which the recipe also leaves alone, and it is
why the `if (_AddBlendOp == 4) { alpha = saturate(alpha * _AlphaBoostFA); }` branch noted in
§1.7 needs no handling: neither `_AddBlendOp` nor `_AlphaBoostFA` changes, so the branch
behaves identically before and after.

### 5.5 `AlreadyOpaque` is a classification, not an eligibility outcome

`AlreadyOpaque` is a **successful no-op**, not a refusal — and, per §5.3 step 2, not a reward
for passing the transformation gates either.

It holds when, after successful source attestation, all 25 canonical facts already match: the
23 recipe properties at their canonical values, effective queue 2000, and the `RenderType`
override tag reading `Opaque`. The design creates no clone. The consumer uses the source
material unchanged.

The tag is part of the comparison on purpose. A material matching every property and queue
but tagged `Transparent` is not canonically opaque and requires a clone, so the tag becomes
correct.

**Why the transformation gates must not run first.** Every gate in §5.3 steps 3–10 exists to
authorize a *change*. The outline gate is the clearest case: §1.8 shows the hazard is that
writing `_Mode = 0` forces unmodelled outline alpha to 1 before the outline clip. A material
that is already canonical already has `_Mode == 0`. That forcing is the existing state of the
author, not something AMUSE would introduce. Reporting a refusal there would claim AMUSE
declined to do something it was never going to do, and would deny the consumer a correct
no-op answer about a material it may legitimately leave alone.

**Exactly one gate can fail on a canonical material.** `_EnableOutlines` is the sole
conversion-read property outside the 25 canonical facts, because it is the only one the
recipe does not write. Every other transformation gate reads a recipe property whose
canonical value already satisfies it:

| Gate | Canonical value | Result |
|---|---|---|
| step 3 finiteness | all 23 recipe values are exact constants | passes |
| step 5 `_AlphaPremultiply` | 0 | passes |
| step 6 `_AlphaToCoverage` | 0 | passes |
| step 7 `_ZTest` | 4 | passes |
| step 8 base blend | `_BlendOp` 0, `_SrcBlend` 1, `_DstBlend` 0 | passes |
| step 9 ForwardAdd factors | `_AddSrcBlend` 1, `_AddDstBlend` 1 | passes |
| step 10 `_Cutoff` | 0 | passes |

So the ordering question is entirely about outlines: an already-canonical material with
`_EnableOutlines = 1`, or with a non-finite `_EnableOutlines`, is still `AlreadyOpaque`. A
**non**-canonical material with outlines enabled still refuses `OutlinesEnabled`, because
there conversion really would write `_Mode`.

Note what this does **not** say. A gate that reads a recipe property can never fire on a
canonical material, because failing that gate is itself a departure from the canonical value.

`_Cutoff` is the sharpest instance: its canonical value is 0, so a material with
`_Cutoff > 1` cannot have all 25 canonical facts matching. Such a material fails the step-2
comparison, evaluation proceeds through the transformation gates, and the answer is
`ClipThresholdDiscardsOpaqueAlpha` — never `AlreadyOpaque`.

---

## 6. Preparation and validation

### 6.1 Preparation

```
clone = new Material(source);                      // transient
foreach (property, value) in CanonicalOpaqueProperties:
    clone.SetFloat(property, value);
clone.renderQueue = CanonicalOpaqueRenderQueue;
clone.SetOverrideTag(RenderTypeTagName, CanonicalOpaqueRenderType);
```

Invariants, each with its reason:

- **The source material is never written.** `new Material(source)` is the only relationship
  between them. Source avatar assets are evidence, not mutation targets.
- **`new Material(source)` is the clone primitive.** The investigation characterized it in
  the public project: it copies shader, all properties, texture scale/offset, the render
  queue override, override tags, double-sided GI, instancing and shader keywords, and
  mutating the clone leaves the source untouched.

- **Nothing is saved.** No `IAssetSaver` call, no `AssetDatabase` write.
  `BuildContext.Serialize()` persists assets reachable from the avatar root at build end.
  The cleanup of NDMF destroys only unreferenced *components and game objects* — a material
  saved eagerly and then abandoned is welded into the generated-asset container forever.
  Persistence belongs to assignment, and that is the job of the consumer. `PrepareCanonicalOpaqueClone`
  takes no asset saver, so it cannot do this by accident.
- **The clone is not named.** Deterministic generated-asset naming and logical output identity
  were deferred to the design of the vertical slice itself. **Handoff obligation: the consumer must
  name the clone before assignment**, because container sub-asset names come from the name of
  the object itself and NDMF guarantees no determinism.
- **No keyword manipulation.** Recorded trap for any future work here: `IsKeywordEnabled`
  returns false for a keyword the shader of the material does not declare, even when the keyword
  is present in the keyword list.

### 6.2 Validation failure is a defect

After writing, eligibility re-reads the clone through the same reader it uses — a fresh
`UnityMaterialEvidenceCapture.Capture` with `ConversionEvidenceRequest`, plus
`ReadEffectiveRenderState` — and `TryFindNonCanonicalFact` compares all 25 canonical facts.
`clone.shader` must also be reference-equal to `source.shader`.

**A disagreement is an invariant failure, not a refusal.** Eligibility already proved
that every property is present and that every input value is finite and exact. Preparation
then clones the *same shader* and writes *exact canonical constants* to properties the shader
declares. If a written value does not read back, the assumption that AMUSE can write this
render state is false, and that is a compatibility or programming failure, not an
unsupported material. Converting it into a conservative refusal would hide a broken write
path behind a plausible-looking "preserved the input" outcome — precisely the pattern the
correctness policy forbids.

Therefore:

1. re-read and validate the clone.
2. `Object.DestroyImmediate` the clone if validation fails, so no material leaks.
3. throw an `InvalidOperationException` naming the first disagreeing fact.

`GeneratedMaterialNotCanonical` is removed from the refusal vocabulary. Nothing is caught:
there is no `try`/`catch` around Unity calls anywhere in this design, and a Unity exception
propagates as the defect it is. This design does not repeat full re-attestation — the shader
object is the same instance whose GUID and normalized hash were just verified.

Tests still falsify every recipe field, by perturbing an already-prepared clone and asserting
`TryFindNonCanonicalFact` detects it (§8.3). That exercises the detector without requiring a
production write/read disagreement, which must stay visible as a defect.

---

## 7. Consumer obligations (specified, not implemented)

The alpha-separation vertical slice, when it attempts conversion, must:

1. **Combine requests only where conversion is attempted.** Build the closed request of the
   renderer as `MaterialEvidenceRequest.Combine(<family alpha requests…>,
   PoiyomiOpaqueConversion.ConversionEvidenceRequest)` for that renderer only. Renderers not
   attempting conversion keep the alpha-only request, so ordinary analysis coverage stays
   the same (§3.2).

2. **Run the existing closure and admission once.** `UnityAnimationEvidenceCapture` for
   material-swap closure, then `AdmittedMaterialStates.ResolveSlot` with the exact-singleton
   policy, using the combined request as relevance. No second admission pipeline.
3. **Evaluate conversion per admitted material state** with `EvaluateVerifiedEligibility`,
   supplying the derived conversion-relevant property evidence of that state together with the
   effective queue and `RenderType` facts belonging to the *same* captured material state.
   **The repository cannot supply that pairing today — see §7.1.**
4. **Enforce the renderer-wide overwrite rule.** A `material.<Property>` curve addresses no
   slot and therefore drives *every* material on the renderer, including a generated one the
   consumer appends. So for every recipe property carrying an admitted binding, the admitted
   value — which the exact-singleton policy makes equal to the serialized value — must
   already equal the canonical value of that property; otherwise the applied recipe is provably
   overwritten at runtime and the tuple this design reasons about is fiction. The
   eligibility-only property `_EnableOutlines` is not written, so it needs no overwrite check
   beyond eligibility itself. `_AddBlendOp` is neither read nor written and needs none for the
   reason given in §5.4.
5. **Refuse conversion for unadmitted conversion-relevant state**, including `_EnableOutlines`,
   and keep that refusal in the vocabulary of the consumer, not in ordinary alpha analysis.
6. **Name the generated material** before assignment (§6.1).

The graph-level gates the alpha path already applies — additive layers and unnormalized
Direct Blend Trees with proof-relevant properties — apply automatically once conversion
properties are part of the combined relevance request. Those gates key on the
relevant-binding set.

**Known conservative refusal, deferred.** Obligation 4 refuses cases that are in fact sound:
an animated `_Cutoff` with admitted singleton 0.5 would still let α = 1 survive. Relaxing it
needs a per-property *interchangeability* argument — for each recipe field, whether the
admitted value is provably equivalent to the canonical one given α ≡ 1. That is tractable but
is per-property semantic reasoning the approved scope does not require. Recorded as deferred
coverage, not a defect.

### 7.1 Discovered prerequisite: no closed admission flow exposes conversion inputs

Obligation 3 above cannot be met against the repository as it stands. We found this while
writing this design. We state it as a required condition, **not** solved here.

Current reality, read from the checked-out source:

- `AdmittedMaterialStates.ResolveSlot` builds a per-admitted-state `CapturedMaterialEvidence`
  internally — accumulating `WithScalar`/`WithColor`/`WithVector` derivations per admitted
  material — and then discards it, returning only `AlphaResolution` values through
  `SlotResolutionResult`.
- `CapturedAlphaMaterial` carries family, evidence and source-attestation data. It carries
  **no originating `Material`**, and no effective queue or `RenderType` fact.

So the design computes the derived evidence that conversion needs and then throws it away, and there is no
captured handle from which the matching non-property facts could be read. Reading them later
from a live `Material` would recapture mutable state *after* the evidence the decision
depends on, which is exactly the stale-capture hazard that the guidance of the repository forbids.

**Required condition.** The vertical slice needs one closed admission flow that makes
available, for every admitted material state, both the derived conversion-relevant property
evidence and the effective queue and `RenderType` facts of the same consistent captured
material state. It must not duplicate admission, and it must not recapture mutable live state
after the evidence on which the decision depends.

**Not decided here, and deliberately not designed here.** This branch does not extend
`CapturedAlphaMaterial`, change `SlotResolutionResult`, introduce a generic evidence
framework, or select a concrete API. Which of those the eventual shape uses is a controller
decision (§11.1), and its natural home is the design of the vertical slice itself rather than this
prerequisite.

This does not block the present branch. `EvaluateVerifiedEligibility` is pure over evidence
and two scalars. It is fully specifiable and fully testable now, and stays correct whatever
carries its inputs later.

---

## 8. Test plan

All deterministic, all in the public project, all EditMode. Layered narrowest-first. Nothing
here requires an animation runner.

### 8.1 Independence rule

Tests of the canonical tuple **must state the expected property/value set literally**,
transcribed from §1.2, and assert `CanonicalOpaqueProperties` matches it. They must not
derive their expectation from `CanonicalOpaqueProperties`, or a wrong production tuple would
test itself.

### 8.2 Recipe completeness

- The literal 23-entry expected table equals `CanonicalOpaqueProperties`, as a set of
  (name, value) pairs.
- **Count assertions**: `CanonicalOpaqueProperties.Count == 23`. The conversion request
  declares 24 presence names and 25 scalar names, and declares no `_AddBlendOp`. A dropped or
  smuggled field fails.
- Applying the recipe to a fixture material makes all 25 canonical facts read back canonical.

### 8.3 Per-field falsifiability

For each of the 25 canonical facts in turn: prepare a clone, perturb that one fact, call
`TryFindNonCanonicalFact`, assert it reports **that** fact. This is the test that fails under
the most plausible incorrect implementation — an omitted field. It catches
`_OutlineDstBlendAlpha` copied as 1 from a neighbouring preset, `_ZTest` forgotten, or the
`RenderType` tag never written.

### 8.4 Outlines refuse conversion, but never the no-op

Non-canonical materials — conversion would really write `_Mode`:

- `_EnableOutlines = 1` → `OutlinesEnabled`.
- **`_EnableOutlines = 1` with every other fact eligible and base alpha exactly 1 →
  `OutlinesEnabled`.** The explicit expectation that a perfect base-alpha proof does not
  rescue enabled outlines (§1.8).

- `_EnableOutlines = 0.5`, `= 0.005`, `= -1` → `OutlinesEnabled`. Only exactly 0 passes.
- `_EnableOutlines` non-finite → `ConversionPropertyNotFinite`. Absent →
  `ConversionPropertyAbsent`.
- `_EnableOutlines = 0` with an otherwise eligible material → `Convertible`, and the
  canonical outline blend tuple is still applied.

Already-canonical materials — nothing would be written, so no gate applies:

- **All 25 canonical facts matching, `_EnableOutlines = 1` → `AlreadyOpaque`, and the design
  creates no clone.** The ordering regression for §5.3 step 2 and §5.5.

- All 25 canonical facts matching with a **non-finite** `_EnableOutlines` → `AlreadyOpaque`,
  and the design creates no clone. A transformation gate must never fire when there is no
  transformation.

- **Contrast — a gate-failing recipe property is not a canonical material.** Every canonical
  fact except `_Cutoff`, with `_Cutoff = 1.001`: the step-2 comparison fails, because
  the canonical value of `_Cutoff` is 0. Evaluation proceeds to the transformation gates and the
  result is `ClipThresholdDiscardsOpaqueAlpha`, **not** `AlreadyOpaque`. `_EnableOutlines` is
  the only conversion-read property that can differ while all 25 facts still match, so it is
  the only property for which the two bullets above are constructible at all.

### 8.5 Effective state beats `_Mode`

- Table over all nine preset tuples: Opaque → `AlreadyOpaque`. Cutout, TransClipping, Fade →
  `Convertible`. Transparent → `PremultipliedAlphaEnabled`. Additive, SoftAdditive,
  Multiplicative, 2xMultiplicative → `UnsupportedBlendEquation`.
  Each row is built with `_EnableOutlines = 0`, `_ZTest = 4` and canonical ForwardAdd
  factors so the row tests what it names.

- **`_Mode = 0` with an Additive base blend must refuse.**
- **`_Mode = 4` with One/Zero, `_ZTest = 4`, outlines off, premultiply off must be
  `Convertible`.**

### 8.6 Depth comparison and ForwardAdd factors

- `_ZTest ∈ {0, 2, 3, 5, 6, 7, 8}` → `UnsupportedDepthComparison`. `_ZTest = 4` passes.
- `_AddSrcBlend ∈ {1, 5}` passes. Other values → `UnsupportedForwardAddBlendEquation`.
- `_AddDstBlend = 1` passes. `_AddDstBlend ∈ {0, 7, 10}` → `UnsupportedForwardAddBlendEquation`.

- **`_AddBlendOp` does not affect the outcome.** A material eligible with `_AddBlendOp = 0`
  is equally eligible with the serialized default of the vendor, `4` (Max), and with every
  other value, because conversion neither reads nor writes it (§5.4). Asserting this pins the
  non-dependency, so reintroducing a gate on it would fail a test.

### 8.7 Clip-threshold boundary

Every row varies `_Cutoff` on a fixture that is **otherwise non-canonical** — a Fade-derived
tuple, so `_SrcBlend`, `_DstBlend`, `_ZWrite` and the queue all differ from canonical. That
precondition is what makes the rows exercise the step-10 gate rather than the step-2
classification: on a fixture that was canonical apart from `_Cutoff`, the `0` row would
correctly report `AlreadyOpaque` instead (§8.4).

| `_Cutoff` | Expected |
|---|---|
| 0, 0.5 | `Convertible` |
| 1.0 | `Convertible` (`clip(0)` survives) |
| 1.001 — *the vendor's declared maximum* | `ClipThresholdDiscardsOpaqueAlpha` |
| 2.0 | `ClipThresholdDiscardsOpaqueAlpha` |
| −0.5 | `Convertible` |
| `NaN`, `+∞`, `−∞` | `ConversionPropertyNotFinite` |

The 1.001 row pins §1.5.

### 8.8 Source immutability and clone hygiene

- Capture every property of the source before preparation and assert byte-identical after,
  for the prepared, `AlreadyOpaque` and refused paths.
- The returned clone is not reference-equal to the source, and shares its shader.
- `AssetDatabase.Contains(clone)` is false and `AssetDatabase.GetAssetPath(clone)` is empty.
- `AlreadyOpaque` creates no clone at all.

### 8.9 Request and relevance isolation

The load-bearing separation test of §3.2, and the only relevance test meaningful on this
branch, because `ResolveProofRelevant` is a pure function of a binding and a request. For a
binding on `material._ZWrite`, and likewise `material._EnableOutlines`:

- under `PoiyomiMaterialSemantics.AlphaEvidenceRequest`, `ResolveProofRelevant` returns
  `Irrelevant` — ordinary alpha analysis stays untouched.
- under `PoiyomiOpaqueConversion.ConversionEvidenceRequest`, it returns `RendererWide`.

And, for `material._AddBlendOp`, `Irrelevant` under **both** requests — conversion depends on
it in neither direction (§5.4).

And the coverage half: a material with `_Cutoff = 1.001` or `_EnableOutlines = 1` produces
the *same* alpha analysis result as an otherwise identical material, proving unknown
conversion state refuses only conversion.

### 8.10 Attestation

- An unattested or locked material refuses `UnattestedMaterial` before any render-state
  question is evaluated.
- `ConversionEvidenceRequest` alone is sufficient to reach identity verification: the
  attestation test captures with that request only and never touches
  `AlphaEvidenceRequest`.

### 8.11 Recipe re-attestation guard

A test asserting the literal values of `PoiyomiMaterialSemantics.CanonicalNormalizedSourceHash`
and `CanonicalShaderGuid`, commented to state that changing either requires re-deriving the
canonical Opaque tuple from the new source before the pin is updated (§9).

---

## 9. Vendor-source oracle: recommendation

**This design proposes no vendor-source parser, and repository inspection found no stronger
dependency-free oracle to report.**

- The `Packages/` directory of the public AMUSE project contains `com.alrauna.amuse`,
  `com.alrauna.amuse.research`, NDMF and the VRChat bootstrap/resolver. No Poiyomi package,
  and `vpm-manifest.json` locks only `nadena.dev.ndmf`. There is no deterministic vendor
  source to parse in CI.

- The only in-repo Poiyomi artifact is `Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader`,
  an AMUSE-authored stand-in reproducing property names, types and defaults, containing none
  of the equations of Poiyomi or `on_value_actions`.
- Copying the `on_value_actions` excerpt of the vendor into the repository would produce a second
  copy of the same recipe, detecting transcription drift between the two copies but never
  vendor drift.

The existing attestation is the real drift detector: any vendor content change moves the
normalized source hash and fails identity *before* any render-state question is reached. The
recipe is safe as AMUSE-owned constants when the design binds the two together, which
§8.11 does by making a pin bump without recipe re-derivation a failing test. That ships no
vendor content and downloads nothing.

---

## 10. Test fixture work

`PoiyomiSemanticTest.shader` currently declares only `_Mode`, `_Cutoff`, `_SrcBlend` and
`_DstBlend` from the render-state family, with `_Cutoff` as `Range(0, 1)`.

It must be extended to declare **all 24 conversion-read properties with the exact
types, defaults and ranges of the vendor**, most importantly:

```
_Cutoff ("Alpha Cutoff", Range(0, 1.001)) = 0.5
[DoNotAnimate][HideInInspector][ToggleUI] _EnableOutlines ("Enable Outlines", float) = 0
```

The `1.001` bound is load-bearing for §8.7. `_AddBlendOp` is also declared — with the default
of `4` from the vendor — but only so §8.6 can vary it and assert it changes nothing. It is not a
conversion-read property. Also added: `_BlendOp`,
`_BlendOpAlpha`, `_SrcBlendAlpha`, `_DstBlendAlpha`, `_AddSrcBlend`, `_AddDstBlend`,
`_AddSrcBlendAlpha`, `_AddDstBlendAlpha`, `_ZWrite`, `_ZTest`, and the six `_Outline*` blend
properties. `_AlphaToCoverage`, `_AlphaPremultiply`, `_AlphaForceOpaque` and `_Mode` already
exist.

This edits an existing test asset in place — no file move, so no GUID churn. Adding
properties cannot change the result of any existing test, because every current test reads named
properties rather than enumerating the schema.

---

## 11. Corrections, decisions and deferred pressure

### 11.1 Open controller decisions

One, and it belongs to the consumer rather than to this branch.

1. **How a closed admission flow exposes conversion inputs** (§7.1). Conversion needs, per
   admitted material state, the derived conversion-relevant property evidence *and* the
   effective queue and `RenderType` facts of the same consistent captured state.
   `ResolveSlot` computes the former and discards it. `CapturedAlphaMaterial` carries neither
   an originating `Material` nor the latter. Extending `CapturedAlphaMaterial`, changing
   `SlotResolutionResult`, or introducing something else are all open. The design chooses none
   of them here, and none is implemented on this branch.

Both decisions previously listed here are resolved: §5.4 removes `_AddBlendOp` from conversion
entirely, and the design now classifies `AlreadyOpaque` before the transformation gates
(§5.3 step 2, §5.5).

### 11.2 Corrections to the investigation

1. **The tuple table of §2 is incomplete.** It lists 9 fields per preset. The Opaque preset carries
   24 actions plus the `_Mode` value. Missing: `_BlendOpAlpha`, `_SrcBlendAlpha`,
   `_DstBlendAlpha`, four `_Add*`, `_ZTest`, six `_Outline*`, and the `render_type` tag.
2. **The `_Mode` runtime claim of §2 is incomplete.** The opaque branch body is commented out
   **only in the `Base` pass**. It is live in `Add`, `Outline` and `ShadowCaster`. And
   `if (_Mode != POI_MODE_TRANSPARENT) finalColor *= poiFragData.alpha;` in the `Add` pass
   does reach RGB. The conclusion survives — read effective state, never `_Mode` — but the
   supporting claim needed correcting, and the `Outline`-pass instance is now known to be
   unsafe rather than an identity (§1.8).
3. **The "clipping inactive" branch of §7 is vacuous for this shader** (§1.4). The general rule
   stays correct. No code implements it, because the branch is unreachable.
4. **The declared cutoff range is itself unsafe** (§1.5).
5. **The admission list of §7 is insufficient.** It requires attestation, the base blend predicate,
   premultiply off, coverage off and clip proven — but not disabled outlines, not `_ZTest`,
   and not the ForwardAdd blend factors. This design adds all three.
6. **The render-queue-override question of §10 is resolved as an implementation detail** (§3.4).
7. **The `AlreadyOpaque` question of §10 is resolved as a successful no-op** (§5.5).
8. **The clone-fidelity and NDMF-lifecycle characterizations in §5 and §6 exist only as
   investigation prose.** The repository contains no `renderQueue`, `SetOverrideTag` or
   `GetTag` usage and no material-clone test anywhere. Whatever this branch relies on from
   them must be re-established by its own committed tests.

### 11.3 Deferred architectural pressure

- **Per-property interchangeability for animated recipe fields** — the known conservative
  refusal of the consumer (§7).
- **Outline alpha modelling.** Deliberately not started. Supporting enabled outlines requires
  modelling `_OutlineMask`, `_OutlineTexture`, `_LineColor` alpha, `_OutlineOverrideAlpha` and
  the distance fade — a texture-evidence-dependent expansion of the alpha domain of
  `MaterialSemantics`, gated behind the unstarted texture-evidence investigation.
- **Generated-material naming and logical identity** — deferred to the vertical slice, and a
  handoff obligation on the consumer (§6.1).
- **Where conversion is attempted** — per slot, per admitted material, or per distinct
  material — is a consumer decision constrained by the single-admitted-material rule the
  slice already identified.
- **The `_BSSEnabled` toggle of the vendor carries its own `on_value_actions`** writing
  `_BlendOpAlpha`, `_SrcBlendAlpha` and `_DstBlendAlpha`. That is an inspector action, not
  runtime behaviour, and eligibility reads effective serialized values, so a BSS-tuned
  material needs no special handling. Recorded so a future reader does not rediscover it as a
  surprise.

---

## 12. Explicitly out of scope for this branch

No mesh, renderer, submesh or material-slot code. No NDMF persistence integration or
`IAssetSaver` use. No `AmuseBuildOperation` wiring and no production caller. No animation
runner, binding list, additive-layer flag, or conversion-specific admission pipeline. No
texture evidence acquisition. No outline alpha modelling. No lilToon render-state work. No
Census launcher, no new Census metric, no Census Lab modification. No interval reasoning,
symbolic proof, richer animation admission, global planner, mutation IR, universal
render-state framework, pass model, `SurfaceMode` classification, or third-party shader
extension API.

Like `AmuseBuildOperation` before it, this capability lands complete and tested with no
production caller. That is the intended shape of a prerequisite.
