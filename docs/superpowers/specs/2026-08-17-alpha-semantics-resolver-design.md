# Alpha Semantics Resolver Design

**Date:** 2026-08-17

**Status:** Approved 2026-08-17 with review amendments applied (see "Review amendments"). Implemented on `feat/alpha-semantics-resolver`, waiting for code review.

## Problem statement

Two proven subsystems exist, one on each side of a gap:

- `MaterialSemantics.Alpha` describes what a resolved material state's opacity *means*, in a closed shader-independent vocabulary.
- `TriangleAlphaClassifier` proves, exactly, whether every reachable sample of one triangle against one immutable byte-encoded scalar field is opaque.

Nothing connects them. This milestone designs the single decision function that connects them, and nothing else.

The resolver answers one question:

> Given one complete normalized Alpha semantic value and the immutable scalar evidence it names: What may the existing classifier be asked? What may be concluded without asking it?

The resolver is a *consumer* of semantics, not a second adapter. It is a *configurer* of the classifier, not a second classifier.

## Goals

- Map every currently representable Alpha semantic form to exactly one of: a uniform triangle outcome, an exact classifier configuration, or a named refusal.
- Preserve the `MaterialSemantics` and `TriangleAlphaClassifier` contracts byte-for-byte.
- Keep shader identity, Unity objects, asset access, mesh access, and NDMF out of the resolver.
- Make every unsupported case a distinguishable refusal, not an exception, a default, or a silently aggressive outcome.
- Stay testable with plain NUnit: no Poiyomi package, no texture assets, no private testbed.

## Non-goals

This milestone adds none of the following:

- shader adapter, adapter registry, public API, NDMF pass
- Unity `Material`/`Mesh`/`Texture` traversal, pixel readback
- render-state or cutout modeling, animation or material-state analysis
- atlasing, material combining, profitability, expression graph, classifier redesign

No texture is ever written, baked, quantized, or altered. This holds for this milestone, and for the evidence normalization it describes for a later host producer.

Extracting real texture pixels into scalar fields on the host side is deliberately a **separate later milestone**. This milestone defines the evidence contract and consumes it. It does not implement a Unity producer for it. This project built the classifier and planner the same way, before any host wiring existed.

## Responsibility boundary

```text
shader/host semantic interpretation      (Poiyomi adapter, future lilToon)  -- upstream, unchanged
        |
        v  ScalarSemanticValue (Alpha)
host texture-content extraction          (future milestone) -- produces immutable scalar fields
        |
        v  AlphaTextureData for (TextureSourceId, TextureChannel)
alpha semantics resolver                 (this milestone)
        |
        v  AlphaTextureData + AlphaSamplingSettings, or a uniform outcome, or a refusal
exact triangle classification            (TriangleAlphaClassifier) -- unchanged
        |
        v  TriangleAlphaOutcome
separation planning                      (MeshSeparationPlanner) -- unchanged
```

The resolver:

- **does** switch exhaustively over the closed Alpha vocabulary.
- **does** translate the semantic sampling enums into the sampling enums that the classifier uses.
- **does** decide which conclusions need texture contents and which do not.
- **does not** read, transform, filter, multiply, bake, quantize, or synthesize any texture data.
- **does not** know a shader, property name, version, Unity type, or mesh.
- **does not** re-derive anything the classifier already proves.

Its only original proof is the multiplier lemma below. Everything else is delegation or refusal.

## What the existing classifier actually requires

Inspected at `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`:

| Input | Shape | Consequence for the resolver |
|---|---|---|
| `AlphaTextureData(width, height, alpha8BottomToTop)` | copied bytes. Opacity predicate is exactly `byte == 255` | evidence must partition texels into "exactly 1" and "strictly below 1" |
| `TriangleAlphaInput` | three positions plus one UV pair set (`WithUv0`) | one UV set only, no scale/offset input |
| `AlphaSamplingSettings` | `AlphaFilterMode {Point, Bilinear}` × `AlphaWrapMode {Clamp, Repeat}` | exhaustive 2×2 mapping is possible |
| domain | continuous closed hull of the supplied UV values, exact rational | the supplied UVs *are* the contract. Nothing else is transformed |
| refusal | `Unknown` for degenerate geometry, absent UV, and workload cap | per-triangle, not resolver-level |

Nothing in the classifier interprets the bytes as *the alpha channel*. Nothing requires them to be a particular texture format. Mathematically, `AlphaTextureData` is an immutable per-texel partition. Byte `255` means "exactly `1`". Every other byte means "strictly below `1`". The scalar field is bounded by `1`. The name is historical. This is the single most important finding for question 5: **arbitrary scalar channels need no classifier change at all**. The host extracts the selected channel into the same byte field, and the predicate of the classifier stays unchanged.

Two genuine mismatches exist. Both are resolver-local restrictions (category A), not classifier changes (category C):

1. **UV transform.** `UvMapping` carries `Scale`/`Offset`. `TriangleAlphaInput` carries neither. See "UV mapping" below.
2. **UV set index.** `UvMapping.Channel` may be any non-negative index. The classifier takes one supplied UV pair set. See below.

Neither pressures the semantic core, and neither requires an expression system.

## The multiplier lemma

Let `s` be the effective sampled scalar at one surface point, and let `k` be the finite semantic multiplier. The effective alpha is `alpha = s · k`. Filtering happens on the channel, and the multiply is a per-fragment scalar, so the relation holds pointwise. Bilinear filtering is a weighted mean of stored values, so the range bound `s ∈ [0, 1]` in the field contract survives filtering.

The opacity predicate in the classifier is exactly `s == 1`. Therefore:

**(1) `k == 1`.** `alpha == 1 ⟺ s == 1`. The predicate *is* the required predicate. Delegate unchanged.

**(2) `k < 1`** (including `k == 0` and every negative `k`).
- `0 < k < 1`: `s ∈ [0,1] ⇒ alpha ∈ [0, k] ⊂ [0, 1)`.
- `k ≤ 0`: `s ∈ [0,1] ⇒ alpha ∈ [k, 0] ⊆ (-∞, 0]`.

In both cases **no reachable sample can be opaque, whatever the texture contains**. The conclusion is `MustRemainTransparent` for every triangle, proven without scanning, classifying, or even reading one texel. This answers thought experiments E and G directly and identically.

**(3) `k > 1`.** `alpha == 1 ⟺ s == 1/k`, with `1/k ∈ (0,1)`. The classifier can only prove `s == 1`. It cannot express `s == c` for any other `c`. Rebuilding a field for that predicate would count as baking. Worse, an all-`255` field then yields `alpha = k > 1`. The semantic model gives that opacity no defined meaning. `MaterialSemantics` explicitly implies "no v1 clamp, saturate, cutoff, or render-state behavior". Assuming saturation would be exactly the kind of guess this project forbids. **Refuse.**

Consequences worth stating plainly:

- Multiplication needs **no** classifier change, **no** input extension, **no** texture baking, and **no** floating-point tolerance.
- The only multiplier that preserves the proof is exactly `1`, compared exactly, with no epsilon.
- Every other supported multiplier is decided *without evidence contents*. Uncertainty about the texture therefore cannot make the answer more aggressive.

## Decision table

`Resolve` is total over the closed vocabulary. First failure wins. The order below is the evaluation order.

| Alpha semantic value | Contents needed | Result |
|---|---|---|
| output not `Complete` | no | refuse `SemanticsUnknown` |
| `Constant(k)`, `k == 1` | no | uniform `ProvenOpaque` |
| `Constant(k)`, `k < 1` | no | uniform `MustRemainTransparent` |
| `Constant(k)`, `k > 1` | no | refuse `UnsupportedMultiplier` |
| `TextureSampleTimesConstant(sample, ch, k)`, `k > 1` | no | refuse `UnsupportedMultiplier` |
| `TextureSampleTimesConstant(sample, ch, k)`, `k < 1` | field must exist, contents unused | uniform `MustRemainTransparent` |
| `TextureSample(sample, ch)` or `…TimesConstant` with `k == 1` | yes | classify per triangle with `(field, mapped sampling)` |

For the classified row, the resolver runs the checks in order: UV mapping supported → sampling mapped → field available. Each has its own refusal code.

### Why the `k < 1` row still requires the field to exist

The premise of the lemma is `s ∈ [0, 1]`. `MaterialSemantics` says nothing about a sampled channel's numeric range. An HDR or float source could exceed 1. The range fact comes from the *field evidence contract*. The resolver therefore requires the evidence even when it never inspects a byte. This keeps the proof grounded, instead of assuming a range the semantic model never promised. It is the one place where the resolver asks for data it does not read. That is deliberate.

### Why constants bypass the classifier entirely

A constant alpha is independent of UV, geometry, sampling, and texture identity. A route through the classifier needs a synthesized 1×1 field and an invented sampler. That route would fabricate sampling facts the semantics never stated. It would also make the answer depend on geometry that cannot affect it. The uniform outcome is therefore returned directly.

One consequence is explicit: a uniform outcome ignores degenerate geometry and absent UV sets, where the classifier would answer `Unknown`. This is sound, because a constant alpha does not depend on either. It is also safe in both directions: a zero-area triangle renders nothing on whichever path it lands. The alternative (mirroring the classifier's `Unknown` for degeneracy) would add a geometry dependency to a geometry-independent fact, purely for symmetry. That alternative is rejected, and documented here so the difference is never mistaken for an oversight.

## Proposed API

All types are internal, Editor-only, in `Alrauna.Amuse.Editor.Analysis`, and consume `Alrauna.Amuse.Editor.Semantics`. The dependency direction is Analysis → Semantics. Semantics gains no reference to analysis.

```csharp
internal enum AlphaResolutionFailure
{
    None,
    SemanticsUnknown,
    UnsupportedMultiplier,
    UnsupportedUvMapping,
    UnsupportedSampling,
    MissingTextureEvidence,
}

/// Immutable predicate-equivalent scalar evidence supplied by the host.
/// Returns false when no evidence satisfying the contract can be proven for
/// this source and channel.
internal delegate bool AlphaFieldProvider(
    TextureSourceId source,
    TextureChannel channel,
    out AlphaTextureData field);

internal sealed class AlphaResolution
{
    internal bool IsResolved { get; }
    internal AlphaResolutionFailure Failure { get; }   // None iff IsResolved

    /// Throws InvalidOperationException when !IsResolved.
    internal TriangleAlphaOutcome Classify(TriangleAlphaInput triangle);
}
```

`AlphaResolution` has exactly three internal shapes. The type must make an invalid one impossible to build, or reject it at construction:

```text
refused    => !IsResolved, Failure != None,  no field, no sampling
uniform    =>  IsResolved, Failure == None,  an outcome, no field
classified =>  IsResolved, Failure == None,  a non-null field and mapped sampling
```

Named private factories carry these invariants. A refusal constructed with `None` must be unrepresentable or throw. The same holds for a resolved value with a failure and for a classified value with a null field. No extra type, wrapper, or state enum is introduced for this. The private constructor plus the three factories are the whole mechanism.

```csharp

internal static class AlphaSemanticsResolver
{
    internal static AlphaResolution Resolve(
        SemanticOutput<ScalarSemanticValue> alpha,
        AlphaFieldProvider fieldProvider);
}
```

`Resolve` accepts the `SemanticOutput` wrapper rather than an unwrapped value, so a caller cannot reach the resolver by unwrapping an unknown output. An unresolved resolution exposes no outcome: `Classify` throws, exactly as `SemanticOutput<T>.GetCompleteValue()` does. A refusal can therefore never be misread as a result.

`Classify` keeps the uniform-versus-classified dispatch inside the proof boundary. Future call sites do not duplicate it.

A null `alpha` payload cannot occur. The wrapper validates it. A null `fieldProvider` is malformed API use and throws `ArgumentNullException`.

## Texture evidence contract

The resolver never touches `AssetDatabase`, `Texture2D`, GUIDs, importers, or files. It receives `AlphaTextureData` through the provider delegate. The delegate is deliberately a lookup, not an interface. There is one production implementation ahead (a later host milestone) and one test implementation (a lambda). An interface would be an abstraction with no second consumer.

The contract describes the proof that the classifier actually consumes. It does not describe a storage format. `AlphaTextureData` is *predicate-equivalent evidence* about the effective scalar values of one channel, not a copy of a particular file's bytes.

A provider **must** return `false` unless it can prove all of:

1. the evidence corresponds to the named `TextureSourceId` and `TextureChannel`.
2. it represents the relevant base-level texel domain. Rows follow the existing `AlphaTextureData` bottom-to-top order. The texel dimensions match the supported sampling model.
3. every effective per-texel scalar value relevant to that sampling model is finite and lies in `[0, 1]`.
4. byte `255` marks exactly those texels whose effective scalar value is exactly `1`.
5. every non-`255` evidence byte marks a texel whose effective scalar value is strictly below `1`.
6. no mip selection, mip bias, anisotropy, or filtering beyond the sampling recorded in the semantics applies.

Points 3 to 5 are precisely what the proof needs. Together they give the predicate that the classifier relies on:

> Under Point or Bilinear sampling, the sampled value equals `1` if and only if every positive-weight contributing evidence texel is `255`.

Bilinear filtering is a weighted mean of values in `[0, 1]`, so it reaches `1` only when every contributing value is `1`. Point sampling reads a single texel. The intermediate bytes carry no proof obligation at all. Only the distinction "exactly 1" versus "strictly below 1" carries one.

The contract therefore does **not** require the original source channel to literally be an uncompressed 8-bit `b/255` field. Any source qualifies whose effective values can be proven to satisfy points 3 to 5. This includes channels the hardware decodes through a monotone transfer function such as sRGB. For any monotone `f` with `f(255) = 1` and `f(b) < 1` below that, the "exactly 1" partition stays unchanged, so the predicate survives unchanged.

A future host producer may therefore **normalize** trustworthy source evidence into `AlphaTextureData`. It reads a channel, maps proven-`1` texels to `255`, and maps every other texel to a byte strictly below `255`. That is proof normalization for analysis. It produces no asset, changes no material, and never reaches the rendered result. It is not texture baking, not a runtime transformation, and not an optimization. Nothing in this design permits writing such data back into the avatar.

A producer returns `false` when it cannot establish points 3 to 5. Cases: unproven or lossy decode, unclear effective dimensions, unreadable data, or effective values outside `[0, 1]`.

Failing any point gives `MissingTextureEvidence`, not an exception and not a guess. This is thought experiment J: known identity, unavailable contents, conservative named refusal.

The `k < 1` shortcut depends on point 3 specifically. That attestation, obtained by requiring the evidence, is the sole source of the `s ∈ [0, 1]` premise in the multiplier lemma.

## UV mapping

`UvMapping` is accepted only when it is exactly:

- `Channel == 0`, and
- `Scale == (1, 1)` and `Offset == (0, 0)` by exact float comparison.

Everything else refuses with `UnsupportedUvMapping`. No defaulting, no rounding, no "close enough".

**Why not apply the transform?** The proof in the classifier is exact relative to the float UV values it receives. It decodes them to dyadic rationals and treats their convex hull as the reachable domain. If the resolver pre-computed `uv * Scale + Offset` in `float`, the rounded result would differ from the mathematical transform by up to one ulp. The classifier would then prove an exact statement about *slightly the wrong domain*. That difference is invisible in the result. It can be argued away, but never observed. That is precisely the failure mode this project rejects. Non-identity transforms therefore refuse.

A clean follow-up exists for later, when coverage demands it. Accept a transform only when `uv * Scale + Offset` is provably *exact* for each supplied vertex. "Exact" here means the mathematical affine result is itself representable in binary32 and equals the supplied `float`.

Establishing exactness requires exact arithmetic, not wider floating point. A future implementation must compute the affine result with exact dyadic/rational arithmetic. It can use the `ExactDyadic`/`ExactRational` machinery already in `ExactUvGeometry`, or an equivalently rigorous exact method. It must accept the transform only when that exact value is representable by the supplied binary32 value. Computing in `double` is **not** sufficient evidence. `double` rounds too. Its exactness for one operand pair does not establish binary32 representability. Treating a wider approximation as proof is the same category of error the restriction exists to avoid.

When the transform is exact in that sense, the transformed floats equal the mathematical transform. Because the transform is affine, `hull(T(uv)) == T(hull(uv))`, so the domain of the classifier stays exactly right with no contract change. That extension is deliberately deferred. It is not needed to make this milestone useful, and identity `_MainTex_ST` is the common case.

**Why not other UV sets?** The classifier does not care which mesh UV set a caller supplies. Its contract is "the supplied UV values". Supporting `Channel == N` therefore costs only a `RequiredUvChannel` property plus an obligation on the caller to feed that set. There is no mesh-side caller yet, so an unenforceable obligation would be speculative. Refuse now. Lift the restriction in the milestone that introduces mesh extraction. There the obligation can actually be honored and tested.

## Sampling mapping

Exhaustive, explicit, and fail-closed. No default arm produces a sampling value.

| Semantic | Classifier |
|---|---|
| `TextureFilterMode.Point` | `AlphaFilterMode.Point` |
| `TextureFilterMode.Bilinear` | `AlphaFilterMode.Bilinear` |
| `TextureWrapMode.Clamp` | `AlphaWrapMode.Clamp` |
| `TextureWrapMode.Repeat` | `AlphaWrapMode.Repeat` |
| any undefined enum value | refuse `UnsupportedSampling` |

The semantic enums validate on construction, so the undefined arm is unreachable through the public path. The arm exists so that a semantic mode added later fails closed, instead of mapping to a wrong classifier mode. The duplication between the two enum sets stays a deliberate layer adapter, as the semantics-core design already recorded. Neither set is renamed, moved, or merged.

## Diagnostics and refusal

The five refusal codes must stay distinguishable. They are:

| Condition | Representation |
|---|---|
| malformed API use (null provider) | throws `ArgumentNullException` |
| semantic Alpha is unknown | `Failure.SemanticsUnknown` |
| unsupported multiplier (`k > 1`) | `Failure.UnsupportedMultiplier` |
| unsupported UV channel or transform | `Failure.UnsupportedUvMapping` |
| unmappable sampling | `Failure.UnsupportedSampling` |
| texture contents unavailable or unattestable | `Failure.MissingTextureEvidence` |
| classifier workload cap, degenerate geometry, absent UV | per-triangle `TriangleAlphaOutcome.Unknown` |

Resolution refusal and per-triangle `Unknown` are deliberately different kinds. A refusal is material-scoped. No triangle outcome exists, and the caller must preserve everything. `Unknown` is a per-triangle answer the classifier already owns. Collapsing them would hide which of the two happened.

The vocabulary is one enum. No detail strings, no severities, no logging, no Poiyomi diagnostic types. Those types live in the shader-specific namespace and must not leak into a generic consumer.

## Thought experiments

| Case | Result | Evidence used |
|---|---|---|
| A. `Constant(1)` | uniform `ProvenOpaque` for every triangle | none |
| B. `Constant(0.5)` | uniform `MustRemainTransparent`, no triangle can ever be opaque | none |
| C. alpha = texture alpha | classify per triangle with the alpha-channel field | field contents |
| D. alpha = texture red | identical path. The field is the red channel, the predicate is unchanged | field contents |
| E. texture alpha × 0.5 | uniform `MustRemainTransparent` (`s ≤ 1 ⇒ alpha ≤ 0.5 < 1`, no reachable sample equals 1) | field existence only |
| F. texture alpha × 2 | refuse `UnsupportedMultiplier`. Alpha is not implicitly saturated, so `alpha = 2` has no defined opacity meaning | none |
| G. texture alpha × 0 | uniform `MustRemainTransparent`, alpha is 0 everywhere | field existence only |
| H. `Unknown` | refuse `SemanticsUnknown` | none |
| I. Repeat + Bilinear + UV transform | refuse `UnsupportedUvMapping` while the transform is non-identity. With identity ST the Repeat/Bilinear pair maps exactly. The classifier's own Repeat normalization and open bilinear supports do the rest | field contents |
| J. identity known, contents unavailable | refuse `MissingTextureEvidence` | none |

## Determinism and immutability

`Resolve` is a pure function: its only inputs are `(alpha value, provider results)`. No Unity API, no `AssetDatabase`, no static mutable state, no time, no ordering over collections, no floating-point tolerance, no randomness. A provider can return a shared field. If that field changes later, the resolution cannot change, because `AlphaTextureData` already copied and summarized its bytes on construction. The same semantic value and the same fields give an identical resolution and identical triangle outcomes across runs and machines.

## Testing strategy

Plain NUnit EditMode tests live in the existing test assembly, constructed directly. Semantic values come from the `Semantics` namespace. Fields are byte arrays. The provider is a lambda. No Poiyomi package, no shader, no texture asset, no `AssetDatabase`, no mesh, no private testbed.

Coverage:

- every row of the decision table, including all three constant regions and all three multiplier regions.
- multiplier boundaries by exact float: `1f`, the float below `1f`, `0.5f`, `0f`, `-1f`, the float above `1f`, `2f`.
- thought experiments A to J as named tests.
- **delegation equivalence:** for supported forms, the resolver's per-triangle result equals a direct `TriangleAlphaClassifier.Classify` call with the same field and mapped sampling.
  The sweep covers Point/Bilinear × Clamp/Repeat and several triangles. This pins the resolver as a configurer, not a second classifier.
- classifier `Unknown` cases surface unchanged through the resolver: degenerate geometry, `MissingUv0`, and the workload cap.
- each refusal code, and first-failure ordering when several would apply.
- `Classify` on an unresolved resolution throws `InvalidOperationException`.
- null provider throws `ArgumentNullException`.
- the `k < 1` path consults the provider but gives the same answer for an all-opaque field and an all-transparent field. This proves the contents are genuinely unused.
- refusals for `Channel == 1`, `Scale == (2,1)`, `Offset == (0.5, 0)`, and a one-ulp non-identity scale.
- uniform outcomes are independent of geometry, including degenerate input.

The classifier's own fixture catalog is not re-run through the resolver. The oracle for the resolver is the classifier itself. The equivalence tests express that directly.

## Architectural pressure discovered

Recorded, not resolved:

1. **Saturation is unmodeled.**
   The design refuses `k > 1` only because `MaterialSemantics` declines to state whether alpha saturates. If real materials often produce `k > 1`, the answer is a narrow, separately justified semantic fact, never a resolver guess. The fact could be a saturate form or a render-state contract.
2. **Classifier inputs cannot express a UV transform.**
   Real coverage loss on non-identity `_MainTex_ST`. The exact-transform criterion above is the documented next step. It needs its own approval, because it touches how UV values reach a proven component.
3. **No producer exists for scalar-field evidence.**
   The resolver is correct and fully tested. It has no production data source until a later milestone delivers host texture extraction. That milestone is the natural next step.
4. **`AlphaTextureData` is really a generic byte-encoded scalar-evidence field.**
   It fits any channel and any source format whose effective values can be proven to satisfy the contract. The name is now slightly narrow. The project does not rename proven code for cosmetics. The resolver's documentation carries the generalized meaning instead.
5. **Semantics state no numeric range for a sampled channel.**
   The evidence contract supplies it as an explicit provable point. It is not a side effect of a storage format. A producer that faces float/HDR sources with effective values above `1` must return `false`. It must never clamp them into range and call that evidence.
6. **`ProvenOpaque` here means "effective alpha is exactly 1".**
   That is the right predicate for the transparent-path question in the separation planner. It is *not* a render-mode decision. Cutout, blending, and queue stay out of the model, as previously decided.

This milestone resolves none of them by adding an abstraction.

## Stop-condition findings

Checked against every stop condition in the task brief:

| Stop condition | Triggered? |
|---|---|
| `MaterialSemantics` requires a general expression DAG | No — the closed vocabulary is sufficient, and the multiplier lemma removes the only apparent pressure |
| exact classifier must be substantially redesigned | No — zero classifier changes |
| shader-specific information must leak into the resolver | No — the resolver names no shader, property, or version |
| live Unity objects must enter the generic reasoning core | No — evidence arrives as immutable bytes through a delegate |
| NDMF types must enter semantic/proof APIs | No |
| arbitrary texture baking required | No — the only baking-shaped case (`k > 1`) is refused instead |
| current Alpha semantics insufficient, requiring core redesign | No — insufficiency appears only for `k > 1`, handled by refusal |
| task expands into render state, animation, modifiers, atlasing, or combining | No — each refused or already outside the value model |

**Recommendation: APPROVE DESIGN.** The milestone is one file of production code plus one test file. It changes no existing contract. Its only new proof is the multiplier lemma.

## Known risks

- Coverage on real avatars is limited by the identity-ST restriction and by the absent evidence producer. Both limits show as visible refusals, never as wrong answers.
- The `k < 1` shortcut depends entirely on the range premise in the field contract. If a future provider violates that premise, the shortcut becomes unsound. Hence the requirement to get the field, and the explicit provider obligations.
- The uniform path is deliberately independent of geometry. This differs from the degeneracy rule of the classifier. Documented above, so the difference is not "corrected" later by mistake.
- `AlphaFieldProvider` invites an eager, whole-project extraction implementation. The delegate shape permits laziness. The future host milestone should use it.

## Explicitly deferred

- host extraction that turns real texture channels into `AlphaTextureData`, as proof normalization
- non-identity UV transforms under the exactness criterion, and non-zero UV sets with a `RequiredUvChannel` obligation
- saturation/clamp semantics and render-state modeling
- any predicate other than "effective alpha equals 1"
- orchestration that walks renderers, meshes, and materials to drive resolver plus classifier plus planner

## Design-phase baseline

- Base: `main` at `4e37d29` (merge of PR #8), identical to refreshed `origin/main`. Working tree clean.
- Topic branch: `feat/alpha-semantics-resolver`, created from that commit.
- Inspected: `AGENTS.md`, `CLAUDE.md`, `docs/architecture/vision.md`, `TriangleAlphaClassifier.cs`, `ExactUvGeometry.cs`, `MeshSeparationPlanner.cs`, `MaterialSemantics.cs`, `PoiyomiMaterialSemantics.cs`, the geometry-classifier and material-semantics-core design docs, the Poiyomi plan, and the existing test suites and asmdefs.
- This phase used no Unity MCP, because it changes documentation only. The phase had nothing to compile, import, or run. It opened or modified no Unity project, public or private.

## Review amendments

Applied 2026-08-17 after design review, before any implementation:

1. **Texture evidence contract corrected and generalized.**
   It no longer demands that each stored byte be the exact effective GPU value times 255. That demand was stronger than the proof needs. It was also in tension with the sRGB discussion that followed. The contract is now the six provable points above. Their operative content is the "exactly 1" versus "strictly below 1" partition plus the `[0, 1]` bound. The contract explicitly permits a future host producer to normalize trustworthy source evidence into `AlphaTextureData`. That is proof normalization, never texture baking or a rendered transformation. The `k < 1` proof still depends on the `[0, 1]` attestation.
2. **Deferred exact-UV-transform criterion corrected.**
   `double` arithmetic is explicitly *not* sufficient evidence for binary32 representability. A future implementation must use exact dyadic/rational arithmetic or an equivalently rigorous exact method.
3. **`AlphaResolution` invariants made explicit.**
   They are now a construction obligation (resolved ⇒ `Failure == None`, refused ⇒ `Failure != None`, classified ⇒ non-null field), with no new abstraction.

The accepted decisions are otherwise unchanged:

- resolver responsibility and API shape
- no changes to `MaterialSemantics` or `TriangleAlphaClassifier`
- constant fast paths
- the multiplier lemma
- `k > 1` refusal
- the identity-ST/UV0-only restriction for this milestone
- the `k < 1` result that survives an unsupported UV mapping
- the shader-independent evidence-provider boundary

## Approval gate

The design and its implementation plan were approved on 2026-08-17. Implementation was explicitly authorized. Execution added `AlphaSemanticsResolver.cs` and `AlphaSemanticsResolverTests.cs` only. No existing contract, test, or metadata changed, and nothing was committed. Any change beyond the approved decisions requires a new design gate. This covers a semantic form, a classifier input, a transform, or a baked field.
