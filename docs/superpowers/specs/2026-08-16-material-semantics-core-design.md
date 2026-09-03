# Material Semantics Core Design

**Date:** 2026-08-16

**Status:** Proposed for approval

## Problem statement

AMUSE has two proven Editor-only foundations: exact triangle-alpha classification and immutable mesh-separation planning. Future shader adapters, alpha resolution, atlasing, material combining, and state analysis need a shared description of a resolved material state. Consumers must not learn shader-specific property names. Pure analysis must not depend on live Unity objects.

This milestone defines the smallest normalized core for material semantics that supports the requested thought experiments. It describes meaning only. It does not extract Unity materials, implement shader or modifier adapters, decide transformations, or change the existing alpha subsystem.

Three concerns remain separate:

1. A source adapter interprets shader properties and other host state.
2. The semantic core records the resulting behavior for each supported output.
3. Later analyzers decide whether the active policy permits transforming a represented difference.

## Goals

- Represent one resolved effective material state with immutable, deterministic, internal values.
- Remove shader property names and live Unity object identity from semantic consumers.
- Represent complete knowledge independently for base color, alpha, emission, and the narrow normal form that UV-coupling analysis requires.
- Express the three v1 value forms that current thought experiments need: constant, texture sample, and texture sample times a constant.
- Expose UV channel, scale, offset, Point/Bilinear filtering, Clamp/Repeat wrapping, source identity, channel interpretation, and color interpretation where they affect meaning.
- Make unknown output meaning explicit and impossible to read as a default value.
- Keep a direct, conservative future boundary from normalized alpha meaning to the existing exact classifier.
- Keep source construction ordinary and avoid a shader-adapter registry, plugin API, expression compiler, or new assembly.

## Non-goals

This milestone does not implement or plan these items:

- shader-specific adapter
- Unity `Material` extraction
- shader-property inspection
- ShaderLab parsing
- modifier framework
- animation analysis
- material-swap tracing
- renderer traversal
- texture loading or readback
- texture baking
- atlasing
- UV packing
- material combining
- material normalization
- mesh transformation
- NDMF pass
- profitability model
- optimizer orchestration
- public plugin API
- standalone package
- render-state model
- generalized shader language
- symbolic algebra

The implementation phase will not modify these files and settings:

- `TriangleAlphaClassifier`
- `ExactUvGeometry`
- `MeshSeparationPlanner`
- their tests, asmdefs, package metadata, manifests, workflows, project settings, and fixtures
- the private testbed

## Relationship to the architecture vision

The core sits between host/shader interpretation and pure analysis:

```text
resolved Unity material state + recognized effective modifiers
        |
        v
shader/host producer
        |
        v
MaterialSemantics
        |
        +--> future alpha resolver --> existing exact classifier
        +--> future atlas planner
        +--> future material-combination analyzer
        +--> future multi-state relationship analysis
```

`MaterialSemantics` describes one resolved effective state. It is not a snapshot object that holds a Unity `Material`. It is not an optimization plan. A future animation analyzer can construct or receive several independent `MaterialSemantics` values and reason across them.

## Current contracts preserved

### Exact alpha classifier

The implemented classifier accepts `TriangleAlphaInput`, immutable alpha bytes, and alpha-specific Point/Bilinear and Clamp/Repeat settings. Only `ProvenOpaque` permits opaque candidacy. Missing UV, unsupported work, and incomplete evidence remain `Unknown`. This milestone does not widen, rename, refactor, or weaken that contract.

Normalized alpha meaning feeds a future resolver:

```text
complete normalized Alpha output
        |
        v
future alpha resolver
  - obtains immutable pixels for TextureSourceId
  - obtains the requested mesh UV channel
  - applies the declared scale and offset
  - maps supported sampling semantics exactly
  - resolves the scalar constant where proof permits
        |
        v
AlphaTextureData + TriangleAlphaInput + AlphaSamplingSettings
        |
        v
TriangleAlphaClassifier
```

If the producer cannot prove a step equivalent, the resolver refuses to invoke the aggressive path. It does not reinterpret unknown sampling, coordinates, channels, or constants as classifier defaults.

### Immutable separation planner

The planner consumes completed triangle outcomes and partitions only `ProvenOpaque` into opaque membership. Material semantics do not enter that planner directly. Future orchestration can resolve alpha semantics, classify triangles, and then pass those unchanged outcomes into the existing planner.

## Considered representation approaches

### 1. Fixed typed outputs with closed value forms — selected

`MaterialSemantics` exposes fixed output slots. Each slot is independently complete or unknown. Color and scalar slots use closed value kinds for constant, texture sample, and texture sample times a constant. Normal uses only an unmodified geometry normal or a narrowly specified tangent-space normal-map sample.

The design selects this option because every v1 form has a named consumer. Code can switch exhaustively over the supported language. Unsupported shader behavior fails closed at an output boundary. Adding a genuinely required future form is an explicit contract change. The core never silently accepts arbitrary expression graphs.

### 2. Small typed expression tree or DAG

A graph with `Constant`, `TextureSample`, `Multiply`, `Add`, and `ChannelSelect` would compose across outputs and could later support more shader behavior. It also creates immediate questions: node typing, normalization, commutativity, graph ownership, canonicalization, equivalence, and traversal. It also asks which shader operations the graph promises to model.

The three thought experiments require only three closed forms. A graph therefore adds machinery without increasing safe v1 coverage. Reconsider a graph only when a real adapter needs a second composition level. That need exists only if closed forms cannot represent the level without duplicate or ambiguous special cases.

### 3. Extensible semantic dictionary keyed by roles

A map from arbitrary semantic keys to generic values would make future outputs additive. It would also weaken compile-time typing and make required output presence ambiguous. It would also introduce ordering and key-versioning questions and invite shader-specific names into the core. Fixed fields are smaller and safer for the known consumers.

## Selected high-level API

All types remain `internal` in `Alrauna.Amuse.Editor.Semantics`. Approved TDD can mechanically refine exact names, but the closed semantic boundary is part of this design.

```csharp
internal sealed class MaterialSemantics : IEquatable<MaterialSemantics>
{
    internal SemanticOutput<ColorSemanticValue> BaseColor { get; }
    internal SemanticOutput<ScalarSemanticValue> Alpha { get; }
    internal SemanticOutput<ColorSemanticValue> Emission { get; }
    internal SemanticOutput<NormalSemanticValue> Normal { get; }
}

internal readonly struct SemanticOutput<T> : IEquatable<SemanticOutput<T>>
    where T : class
{
    internal bool IsComplete { get; }
    internal T GetCompleteValue();

    internal static SemanticOutput<T> Complete(T value);
    internal static SemanticOutput<T> Unknown();
}
```

`GetCompleteValue` is the only way to retrieve a value. It throws `InvalidOperationException` for an unknown output. An unknown output stores no meaningful fallback. The design deliberately has no `ValueOrDefault` API and no material-wide known flag.

The four v1 outputs are exact:

- `BaseColor`: linear-light RGB behavior before alpha composition.
- `Alpha`: scalar opacity behavior represented independently from render mode.
- `Emission`: linear-light RGB emission behavior.
- `Normal`: the incoming surface/shading normal from the host, which the material does not perturb, or the one supported tangent-space normal-map form.

The design includes the Normal output because the required UV-coupling case has a concrete normal-texture consumer. The v1 normal vocabulary is intentionally narrower than a general normal-expression language.

## Value model

### Color

```csharp
internal enum ColorSemanticValueKind
{
    Constant,
    TextureSample,
    TextureSampleTimesConstant
}
```

A color constant is a finite `Vector3` in linear-light RGB. A color texture sample selects RGB and declares whether the source RGB is linear or sRGB. The core normalizes both to linear-light values. `TextureSampleTimesConstant` records component-wise multiplication in linear space. It does not bake and does not claim that baking is safe.

### Scalar

```csharp
internal enum ScalarSemanticValueKind
{
    Constant,
    TextureSample,
    TextureSampleTimesConstant
}

internal enum TextureChannel
{
    Red,
    Green,
    Blue,
    Alpha
}
```

A scalar constant is finite. The core implies no v1 clamp, saturate, cutoff, or render-state behavior. A scalar texture sample selects exactly one channel. `TextureSampleTimesConstant` is ordinary scalar multiplication. Thus the alpha thought experiment has an Alpha output with an Alpha-channel sample times the opacity constant.

### Normal

```csharp
internal enum NormalSemanticValueKind
{
    Unmodified,
    TangentSpaceNormalMap
}
```

`Unmodified` means the material does not perturb the incoming surface/shading normal from the host. With `TangentSpaceNormalMap`, one texture sample uses the v1 canonical unsigned RGB-to-signed tangent-space interpretation. Normal strength, channel inversion, reconstructed channels, object/world-space normals, detail normal composition, and custom decoding are unknown in v1.

### Exact v1 operation set

The complete value/operation vocabulary is:

- finite scalar constant
- finite linear-light RGB constant
- RGB texture sample with linear or sRGB interpretation
- single-channel texture sample
- component-wise RGB texture sample times RGB constant
- scalar texture sample times scalar constant
- surface/shading normal from the host left unperturbed by the material
- canonical tangent-space normal-map sample

The vocabulary has no general `Multiply` node, `Add`, channel shuffle, condition, comparison, clamp, lerp, power, normal blend, graph node, or user-defined operation.

### Active variant payload access

The closed values keep their `Kind` discriminators. The core does not expose inactive payloads as usable properties or placeholder defaults. Code reads each payload only through a kind-checked accessor:

```csharp
internal sealed class ColorSemanticValue
{
    internal ColorSemanticValueKind Kind { get; }
    internal Vector3 GetConstantValue();
    internal TextureSample GetTextureSample();
    internal TextureColorInterpretation GetColorInterpretation();
    internal Vector3 GetMultiplier();
}

internal sealed class ScalarSemanticValue
{
    internal ScalarSemanticValueKind Kind { get; }
    internal float GetConstantValue();
    internal TextureSample GetTextureSample();
    internal TextureChannel GetChannel();
    internal float GetMultiplier();
}

internal sealed class NormalSemanticValue
{
    internal NormalSemanticValueKind Kind { get; }
    internal TextureSample GetTextureSample();
}
```

`GetConstantValue` succeeds only for `Constant`. Texture sample and interpretation/channel access succeed for `TextureSample` and `TextureSampleTimesConstant`. `GetMultiplier` succeeds only for `TextureSampleTimesConstant`. The normal texture accessor succeeds only for `TangentSpaceNormalMap`. Every wrong-kind access throws `InvalidOperationException`.

Thus a plain texture exposes no artificial multiplier. A constant exposes no null sample or fake interpretation/channel. `Unmodified` exposes no usable texture sample.

## Texture identity and content boundary

```csharp
internal readonly struct TextureSourceId : IEquatable<TextureSourceId>
{
    internal string Value { get; }
}
```

The producer supplies a non-empty opaque string token. Equality is ordinal. The semantic core does not parse the token or carry pixels. It does not require an asset path or GUID, open the asset database, or retain a Unity `Texture`. Within a state set that consumers compare, a producer must use the same token for the same source.

Texture identity and immutable content remain separate:

```text
MaterialSemantics: sample source token X
later content provider: token X -> immutable pixels and dimensions
```

Stable serialization across processes is not a v1 promise. A future host can choose its own deterministic token scheme without changing semantic consumers.

## UV model

```csharp
internal readonly struct UvMapping : IEquatable<UvMapping>
{
    internal int Channel { get; }
    internal Vector2 Scale { get; }
    internal Vector2 Offset { get; }
}
```

`Channel` is a non-negative host-neutral UV set index. `Scale` and `Offset` are finite. The model defines sampling coordinates as component-wise `meshUv * Scale + Offset`. This represents UV0, UV1, and other ordinary indexed UV sets without inventing separate enums.

The model does not represent rotation, triplanar, generated, screen-space, vertex-derived, animated, procedural, or custom mappings. When a producer finds one, it marks every affected output unknown. It never substitutes UV0, identity scale, or zero offset.

For v1 atlas reasoning, two samples with structurally equal `UvMapping` values count as spatially coupled. This exposes the requested relationship. It does not define island packing. It does not claim that equal mappings alone make atlasing safe.

## Sampling model

```csharp
internal enum TextureFilterMode
{
    Point,
    Bilinear
}

internal enum TextureWrapMode
{
    Clamp,
    Repeat
}

internal readonly struct TextureSampling : IEquatable<TextureSampling>
{
    internal TextureFilterMode Filter { get; }
    internal TextureWrapMode Wrap { get; }
}
```

The model supports only the exact modes that the alpha subsystem already proved. Point/Bilinear and Clamp/Repeat apply after the declared UV transform, at base-level normalized coordinates. They have the same mathematical sampling conventions as the existing classifier contract. A producer that cannot prove that match marks the affected output unknown. One wrap mode applies to both axes. Per-axis differences, mirror modes, anisotropy, mip selection, derivatives, border color, and other filtering behavior make an affected output unknown.

The core does not reuse the `AlphaFilterMode` and `AlphaWrapMode` enums. Those types belong to the current classifier input contract and carry an explicit alpha prefix. This milestone also does not move them, because a move would refactor proven code for no immediate behavior change. A future alpha resolver maps exhaustively between the two closed enum sets. This duplication is a deliberate layer adapter. It is not permission for their meanings to drift.

## Texture sample

```csharp
internal sealed class TextureSample : IEquatable<TextureSample>
{
    internal TextureSourceId Source { get; }
    internal UvMapping Coordinates { get; }
    internal TextureSampling Sampling { get; }
}
```

The sample contains identity and sampling meaning only. Color interpretation, scalar channel selection, and normal decoding belong to their typed semantic value. Thus a raw texture reference does not pretend that all consumers interpret its bytes identically.

A null source does not represent a missing texture. If the adapter proves the shader fallback for a missing texture, it emits the resulting constant or another complete value. Otherwise it marks the affected output unknown.

## Unknown and partial knowledge

Knowledge is per output:

```text
BaseColor: complete
Alpha: complete
Emission: unknown
Normal: complete
```

The producer must assert that the represented value fully describes the effective output. Only then is the output `Complete`. If a known base-shader term and an unmodeled term both affect that output, the output is `Unknown`. It is not a partially usable expression.

The v1 core deliberately uses one `Unknown` state rather than separate unknown, unsupported, missing, or invalid states. They have the same safety meaning to consumers. Producer diagnostics can keep the reason outside the semantic value. Adding diagnostic provenance to this small immutable model would not help any v1 semantic consumer.

Because `SemanticOutput<T>` is a readonly struct, `default(SemanticOutput<T>)` is deliberately equivalent to `Unknown()`: `IsComplete` is false and `GetCompleteValue()` throws `InvalidOperationException`. Default construction therefore fails closed rather than exposing a default semantic value.

An unrelated unknown feature does not automatically invalidate every output. The producer keeps complete outputs only when it can prove that the feature cannot affect them. If the affected outputs of the feature are themselves unknown, all outputs become unknown. This is the minimum reliable answer to output interaction without a dependency graph.

Malformed construction remains distinct from unknown meaning. Construction throws for null complete values, empty or default source IDs, and non-finite constants or UV values. It also throws for negative UV channels and undefined enum values. The core represents unsupported shader behavior as `Unknown`. The core rejects malformed use of the semantic API.

## Render-state decision

The core defers render state. None of the three v1 thought experiments needs an Opaque/Cutout/Transparent/MultiPass enum to describe the requested color, alpha, or sampling meaning. Alpha semantics alone do not determine queue, blending, cutoff, depth writes, or pass behavior. A small render enum in the core now could encourage consumers to infer those missing facts. A later optimization that needs effective render state must add a separately justified normalized contract and combine it with Alpha. It must not derive render state from Alpha defaults.

## Transformation-capability boundary

The milestone selects semantics-only option 1 after comparing all three proposed boundaries:

1. **Semantics only now — selected**: Later analyzers pattern-match complete semantic forms and build proof from reachable content, target, states, and the active policy. This is the smallest boundary, and it prevents a semantic fact from becoming policy-dependent.

2. **Capabilities on semantic facts — rejected**: An annotation is convenient for one consumer. But bakeability and canonicalization depend on target format, color handling, other outputs, modifiers, all states, and policy. An annotation on the fact is either wrong, or it forces the semantic value to absorb optimizer context.

3. **Separate associated capability/evidence model — deferred**: This preserves conceptual separation and can be correct once two consumers share the same proof result. No v1 consumer produces or consumes such evidence. Defining its categories now is speculative.

Semantic values carry no `BakeableIntoTexture`, `Canonicalizable`, `MaterialGlobal`, `PerRegionEncodable`, profitability, or policy annotation. A future analyzer can recognize a complete `TextureSampleTimesConstant` form. It then establishes separate transformation evidence from texture content, target format, color space, sampling, reachable states, and the active policy.

This keeps a stable fact separate from a policy-dependent conclusion:

- Example fact: “base color is texture times red.”
- Example conclusion: “the policy allows baking red into this atlas region.”

If several consumers later need the same proof result, that result can become a separate evidence model. It does not belong inside the semantic fact today.

## Shader-adapter producer boundary

Shader adapters are producers of ordinary immutable values. V1 adds no adapter interface, registry, factory, discovery mechanism, shader name, or property-name abstraction. The first real adapter can construct `MaterialSemantics` directly. Its implementation will reveal whether a formal producer contract is useful.

Source representation stays outside the core. Names such as `_Color`, `_MainTex`, Poiyomi, and lilToon never appear in semantic types or consumer tests.

## Future modifier boundary

The core does not assume that base shader semantics are the complete effective semantics. A future pipeline can successively construct new resolved semantic values after recognized modifier contributions. Values are immutable, so applying a modifier conceptually replaces the affected output slots. It does not mutate a shared graph.

An unknown modifier scoped to alpha forces Alpha to unknown. An unknown modifier with an unproven scope forces all outputs to unknown. The core introduces no modifier list, provenance graph, or patch interface now.

## Future animation and state boundary

Each `MaterialSemantics` instance represents one resolved state. A future state analyzer can compare state A, B, and C with structural values and stable texture source tokens. It can discover that only an alpha constant changes, or that UV mappings differ. The semantic core contains no animation parameter, binding, controller, transition, or symbolic value.

Construction does not retain the currently observed Unity material. Thus the model can represent alternate resolved states independently. Exact floating-point equality is structural equality. Any normalization or tolerance policy belongs to the producing adapter or later analyzer and must be explicit.

## Thought experiments

### Bakeable tint difference

Material A and B each have complete BaseColor values of `TextureSampleTimesConstant`, with different source tokens and red/blue linear constants. The model represents their Alpha, Emission, and Normal outputs independently. The model records the original multiplication. A future material-combination analyzer can test whether baking is safe. No `CanCombineWith` or baking capability exists on the values.

### Alpha expression feeding the classifier

Alpha is complete and contains an Alpha-channel `TextureSampleTimesConstant`. A future resolver can inspect the source token, UV mapping, filter, wrap, selected channel, and opacity constant. It gets pixels separately, and it then constructs exact classifier input or refuses. The classifier remains unchanged.

### UV-coupled texture inputs

BaseColor and Emission contain color texture samples. Normal contains a canonical tangent-space normal-map sample. Each texture has a distinct source token but the same `UvMapping(UV0, scale, offset)`. A future atlas planner can observe structural equality of those mappings and keep placements and remaps coordinated. Different transforms or UV1 compare unequal. Equal mappings are necessary information, not sufficient transformation proof.

## Consumer justification

| Proposed type or enum | Concrete consumer | Why needed in v1 |
|---|---|---|
| `MaterialSemantics` | all semantic consumers | Gives consumers four shader-independent outputs for one resolved state |
| `SemanticOutput<T>` | alpha resolver and partial material analysis | Gives per-output complete/unknown safety without a global invalidation flag |
| `ColorSemanticValue`, `ColorSemanticValueKind` | material combiner and atlas planner | Represents constant, texture, and texture-times-constant color facts exhaustively |
| `ScalarSemanticValue`, `ScalarSemanticValueKind` | alpha resolver | Represents constant, sampled, and multiplied opacity facts exhaustively |
| `NormalSemanticValue`, `NormalSemanticValueKind` | atlas planner | Covers the concrete normal-texture UV-coupling case without general normal algebra |
| `TextureSample` | alpha resolver, atlas planner, combiner | Groups source, coordinate, and sampling meaning for a single sample |
| `TextureSourceId` | all texture consumers | Decouples semantics from live Unity objects and pixel storage |
| `UvMapping` | atlas planner and alpha resolver | Distinguishes UV0/UV1 and observable scale/offset transforms |
| `TextureSampling` | alpha resolver and atlas planner | Keeps the supported filter and wrap facts together |
| `TextureFilterMode` | alpha resolver and atlas planner | Distinguishes Point from Bilinear without a Unity enum dependency |
| `TextureWrapMode` | alpha resolver and atlas planner | Distinguishes Clamp from Repeat without a Unity enum dependency |
| `TextureChannel` | alpha resolver | Expresses texture alpha and other scalar channel selection without a graph |
| `TextureColorInterpretation` | combiner and future baking | Prevents color multiplication in the wrong linear/sRGB interpretation |

No v1 type exists solely for a hypothetical shader feature.

## Immutability, equality, and determinism

- Validated construction assigns all fields, and they never mutate.
- The model contains fixed fields and no caller-owned collections.
- `TextureSourceId` compares with ordinal string equality.
- Samples, mappings, values, output wrappers, and `MaterialSemantics` compare structurally.
- Closed value equality and hashing use `Kind` plus only the active payload. Inactive private storage has no semantic meaning.
- Object identity has no semantic meaning.
- Independently constructed values with the same field values compare equal.
- Child ordering and DAG sharing do not arise because v1 has no expression tree.
- Float and vector components compare exactly. The core rejects NaN and infinity.
- Hash codes support in-process dictionaries but are not serialized stable IDs.
- The core does no interning, hash caching, canonicalization, commutative reordering, or algebraic simplification.

## Portability

The model does not reference live `Material`, `Texture`, `Renderer`, asset database, NDMF, or MCP state. It can use Unity `Vector2` and `Vector3` value types. The current package already depends on Unity, and those immutable value types do not leak host object identity. A second host could construct equivalent values by converting its numeric data.

Portability remains a design property. The design creates no assembly split, standalone library, serialization format, or cross-platform package.

## Placement and assembly scope

Implementation adds the minimum directory evolution:

```text
Packages/com.alrauna.amuse/
  Editor/
    Semantics/
      MaterialSemantics.cs
  Tests/Editor/
    Semantics/
      MaterialSemanticsTests.cs
```

Unity-generated `.meta` pairs accompany the new directories and files. Production stays in `Alrauna.Amuse.Editor`. Tests stay in `Alrauna.Amuse.Tests.Editor` through the existing `InternalsVisibleTo`. The implementation requires no asmdef or package metadata changes.

One production file keeps the tightly coupled v1 vocabulary visible as a single small contract. The plan defers split files until implementation size or independent change patterns justify them.

## Testing strategy

Approved implementation uses direct NUnit tests and red/green TDD. Tests operate on semantic facts, never shader property names.

Coverage includes:

- immutable construction and read-only properties
- no v1 collection acceptance or exposure, so caller-collection and returned-view mutation cases do not apply by design
- constructor validation for non-empty source IDs, finite values, non-negative UV channels, and defined enums
- constant BaseColor and Alpha
- color and scalar texture samples with explicit UV and sampling facts
- color and scalar texture-sample-times-constant forms
- canonical normal-map sampling and unmodified normal
- kind-checked payload access, including `InvalidOperationException` for every wrong-kind accessor
- complete and unknown outputs, including failure to retrieve an unknown fallback
- conservative `default(SemanticOutput<T>)` behavior matching explicit unknown
- partial knowledge where one output is unknown and others remain complete
- independent construction with structural equality and no object-identity meaning
- texture source identity without Unity objects or pixel content
- shared versus different UV mappings, UV0 versus UV1, Repeat versus Clamp, and Point versus Bilinear
- missing texture as a proven fallback constant or unknown, never null
- the three required thought experiments as executable semantic construction tests
- an alternate resolved state with a changed constant and no animation machinery

The plan adds no fixture JSON catalog. The values are small enough for direct NUnit construction.

## Complexity

Construction, validation, equality, and accessor calls are O(1). v1 has four fixed outputs and no expression or collection traversal. Memory is O(1) per resolved state plus opaque source-ID strings. Future analyzers own any state-set or texture-content collections they require.

## Adversarial review

| Case | Result |
|---|---|
| texture times tint | Exact closed color form. No baking claim |
| texture alpha times opacity | Exact closed scalar form. The future resolver boundary is explicit |
| BaseColor, Emission, Normal sharing UV0 transform | All expose equal `UvMapping`. Placement is not yet planned |
| two textures using different transforms | Mappings compare unequal |
| UV1 instead of UV0 | Non-negative channel index records UV1 exactly |
| Repeat versus Clamp | Distinct defined sampling values |
| Point versus Bilinear | Distinct defined sampling values |
| missing texture | Proven shader fallback becomes a constant. Otherwise the output is unknown |
| constant-only material | Complete constant BaseColor/Alpha/Emission and unmodified Normal are representable |
| alpha completely unknown | No consumer can retrieve the Alpha value or read it as opaque |
| BaseColor understood, another output unknown | Per-output status preserves BaseColor only |
| unknown modifier potentially affecting alpha | Alpha becomes unknown. An unscoped modifier invalidates all outputs |
| independently constructed identical values | Structural equality succeeds. Reference identity is irrelevant |
| future state with different constant | Separate `MaterialSemantics` value compares unequal without symbolic animation |
| source identity without Unity object | Opaque ordinal token survives independently of live host state |
| unusual coordinate mapping | Affected output is unknown, never silently UV0 |
| unsupported filter or wrap | Affected output is unknown. Undefined enum construction throws |
| inactive value payload access | Wrong-kind access throws instead of exposing placeholders or null semantic data |
| default `SemanticOutput<T>` | Behaves as unknown and cannot expose a complete value |
| shader property names | None are present in the core or semantic tests |

The review found one justified addition beyond the initial BaseColor/Alpha/Emission candidates: the narrow Normal output. Omitting it would leave the required three-texture UV-coupling case unrepresentable. It would also force an immediate contract addition before the first atlas consumer. No other shader feature has that concrete need.

## Known risks

- A real shader adapter may require a composition form beyond the three closed color/scalar forms. That evidence is the gate for reconsidering a tiny typed expression tree.
- One wrap mode for both axes conservatively rejects textures with differing U/V behavior. Per-axis wrap belongs in the first adapter milestone only if a representative supported shader requires it.
- The canonical tangent-space normal-map form deliberately excludes common variations such as green-channel inversion or strength. Such outputs remain unknown until a consumer and adapter justify the vocabulary.
- Opaque string source tokens require producers to define stable identity within the compared state set. The core intentionally does not prescribe asset GUIDs or paths.
- Exact float equality may distinguish numerically close states. Producers can normalize source values only when that normalization itself preserves meaning.
- This design includes color interpretation because omitting it could make future baking incorrect. The core does not yet define texture import/readback behavior.

## Explicitly deferred capabilities

- general expression graph or DAG
- addition, lerp, clamp, cutoff, power, conditions, arbitrary channel composition, and symbolic algebra
- algebraic equivalence and canonicalization
- per-axis or additional wrap modes, mipmaps, derivatives, anisotropy, and border behavior
- normal strength, alternative encodings, and normal composition
- masks, metallic, smoothness, occlusion, render state, passes, and special effects
- transformation annotations or evidence objects
- adapter interfaces, registries, factories, and discovery
- modifier provenance and dependency graphs
- animation/state machines and symbolic parameters
- texture pixels, import settings, serialization, and content providers
- public APIs, assembly extraction, and cross-host packaging

## Design-phase baseline

- Branch base: `main` at `dabf36e`, identical to refreshed `origin/main`.
- Rebrand verification: `dabf36e` is the merge commit for PR #6, containing rebrand commit `67132c9`.
- Topic branch: `feat/material-semantics-core`.
- Working tree before documentation: clean.
- Unity project version on disk: `2022.3.22f1`.
- Package: `com.alrauna.amuse` version `0.0.1`.
- Production structure: one Editor assembly and one friend Editor test assembly.
- Existing documentation discrepancy: `README.md` says `ProvenTransparent`, while the merged classifier contract and tests use `MustRemainTransparent`. This milestone does not edit the README.
- Unity MCP discovery found no running Editor instance. Live compilation state, Console state, and test discovery were not available. This phase ran no Unity tests because it changes documentation only.
- Private Unity testbed: not selected, not inspected, and not modified.

## Approval gate

This document and the matching implementation plan are documentation only. Production code and tests must not start until the user explicitly approves both.
