# Triangle Alpha Geometry Classifier Design

**Date:** 2026-08-15

**Status:** Approved

## Problem statement

Alpha Material Optimizer needs a first production analysis primitive that classifies one indexed mesh triangle against one base-level alpha texture under the approved Point/Bilinear and Clamp/Repeat semantics. The result is exactly one of:

- `ProvenOpaque`: every reachable sample over the complete continuous closed UV domain has alpha exactly `255`;
- `MustRemainTransparent`: at least one reachable sample is known to have alpha below `255`;
- `Unknown`: the supported bounded analyzer cannot prove either result safely.

`ProvenOpaque` is a proof claim. Vertex-only, centroid, raster-grid, random, supersampled, or other finite sampling cannot establish it. Additional uncertainty must never produce a more aggressive result.

The approved deterministic fixture specification, `fixture-inputs.json`, `fixture-expectations.json`, and the fixture integrity tests were compared directly. They agree on all thirteen case IDs, alpha semantics, coordinate conventions, expected outcomes, intentional missing UV0, geometry degeneracy, and the continuous closed barycentric UV domain.

## Goals

- Classify each triangle independently from immutable geometry, UV, alpha, and sampling data.
- Cover the entire UV triangle, line segment, or point analytically.
- Support alpha bytes, Point/Bilinear filtering, Clamp/Repeat wrapping, and base-level textures exactly as schema version 1 defines them.
- Keep malformed inputs distinct from conservative `Unknown`.
- Terminate predictably for very large coordinates and UV spans.
- Reuse the approved fixture input catalog in tests while consulting the separate oracle only at the assertion boundary.

## Semantic contract summary

- The relevant UV set is the complete continuous closed barycentric image of the three indexed vertex UVs, not a finite set of samples.
- Alpha `255` is exactly opaque. Every lower byte, including `254`, is non-opaque.
- Point uses the approved wrapped/clamped `floor(t * size)` cells with the last-index clamp rule.
- Bilinear uses `p = t * size - 0.5` and exact real weights; only texels with positive weight affect opacity.
- Clamp and Repeat are applied exactly as the approved fixture specification defines them, including negative coordinates and seams.
- Mipmaps, material/shader interpretation, cutoff thresholds, and imported texture behavior do not exist in this classifier contract.
- `ProvenOpaque` requires proof over every reachable sample; a known non-opaque witness yields `MustRemainTransparent`; incomplete supported analysis yields `Unknown`.
- Malformed data is rejected and never silently converted to `Unknown`.

## Non-goals

This milestone does not include mesh splitting or mutation, material generation or rewriting, NDMF passes, optimization plans, animation or material-state tracing, shader adapters, texture import, mipmaps, profitability analysis, acceleration frameworks, compatibility integrations, CI, release automation, or private-avatar behavior.

The classifier does not read fixture JSON, expectations, Unity `Mesh` or `Texture2D` objects, GameObjects, assets, files, MCP state, or NDMF build state.

## Considered approaches

### 1. Exact texture-scaled rational geometry — selected

Convert the finite C# `float` inputs exactly to dyadic integers, scale UV axes by texture width and height, and express texel and bilinear-support boundaries on the same integer lattice. Use `BigInteger` predicates and a minimal exact rational type for the new vertices created while clipping the convex domain. Enumerate only a bounded candidate region and return `Unknown` before work exceeds the fixed budget.

This is the smallest approach found that makes boundary behavior a proof rather than an undocumented floating-point assumption. It uses the standard library, limits rational arithmetic to a constant-size clipped polygon, and handles collapsed UV domains with the same intersection primitive.

### 2. `double` geometry with fail-closed intervals

Widen each calculation by a derived floating-point error interval and return `Unknown` whenever a boundary predicate is indeterminate. This could be faster and initially shorter, but a correct outward-rounding implementation and its proof are at least as subtle as the classifier itself. Arbitrary epsilons are unsafe, and a hand-waved interval error bound would not justify `ProvenOpaque`.

### 3. Unlimited exact periodic enumeration

Use the same exact support regions but enumerate every repeated cell crossed by the domain. This is semantically broad but can make time proportional to arbitrarily large UV spans. It violates bounded termination and is unnecessary for the approved fixtures.

Finite sampling is rejected outright because it cannot prove a property over the continuous domain.

## Production boundary and proposed API

The Editor-only production assembly gains a pure data API. Names are proposed and may be refined during approved TDD without changing the boundary.

```csharp
internal enum TriangleAlphaOutcome
{
    ProvenOpaque,
    MustRemainTransparent,
    Unknown
}

internal enum AlphaFilterMode
{
    Point,
    Bilinear
}

internal enum AlphaWrapMode
{
    Clamp,
    Repeat
}

internal readonly struct AlphaSamplingSettings
{
    internal AlphaFilterMode FilterMode { get; }
    internal AlphaWrapMode WrapMode { get; }
}

internal readonly struct TriangleAlphaInput
{
    internal Vector3 Position0 { get; }
    internal Vector3 Position1 { get; }
    internal Vector3 Position2 { get; }
    internal bool HasUv0 { get; }
    internal Vector2 Uv0 { get; }
    internal Vector2 Uv1 { get; }
    internal Vector2 Uv2 { get; }

    internal static TriangleAlphaInput WithUv0(
        Vector3 p0, Vector3 p1, Vector3 p2,
        Vector2 uv0, Vector2 uv1, Vector2 uv2);

    internal static TriangleAlphaInput MissingUv0(
        Vector3 p0, Vector3 p1, Vector3 p2);
}

internal sealed class AlphaTextureData
{
    internal int Width { get; }
    internal int Height { get; }

    internal AlphaTextureData(
        int width,
        int height,
        IReadOnlyList<byte> alpha8BottomToTop);
}

internal static class TriangleAlphaClassifier
{
    internal const int MaxSupportRegions = 65536;

    internal static TriangleAlphaOutcome Classify(
        TriangleAlphaInput triangle,
        AlphaTextureData texture,
        AlphaSamplingSettings sampling);
}
```

`AlphaTextureData` validates dimensions and length, copies the alpha bytes once, and records the all-opaque/all-nonopaque states. The copy prevents cached facts from being invalidated by caller mutation. It exposes only internal texel lookup to the classifier.

`TriangleAlphaInput` uses named factories so intentional missing UV0 is explicit. The placeholder UV fields of a missing channel have no semantic meaning. Tests adapt fixture records into these production types; production code never depends on the fixture DTOs.

The production types remain internal. A narrow `InternalsVisibleTo` assembly attribute exposes them to the existing Editor test assembly without creating a premature public package API.

## Validation and outcome order

1. Validate non-null texture data, positive dimensions, exact alpha length, supported enum values, and finite geometry positions. Malformed input throws `ArgumentException` or `ArgumentOutOfRangeException`; it is not `Unknown`.
2. Compute the 3D geometry cross product exactly from the input floats. A zero cross product returns `Unknown`.
3. If UV0 is intentionally absent, return `Unknown` without validating meaningless placeholder UV values.
4. Validate present UV0 values are finite.
5. If every texel is `255`, return `ProvenOpaque`.
6. If every texel is below `255`, return `MustRemainTransparent`.
7. Build the exact texture-scaled UV domain and classify by support-region intersection. If the complete candidate set exceeds the fixed work budget, return `Unknown` before enumeration.

Geometry degeneracy and missing UV therefore retain their approved `Unknown` result even on a fully opaque texture.

## Degeneracy and missing UV0

Geometry and UV degeneracy are independent:

- If the three 3D positions are exactly collinear or coincident as supplied floats, the geometry triangle has zero area and returns `Unknown` before texture fast paths.
- If geometry is nondegenerate but the UV hull collapses to a segment or point, that closed lower-dimensional domain is classified normally.
- `TriangleAlphaInput.MissingUv0` represents intentionally absent supported information and returns `Unknown`.
- Non-finite present positions/UVs, invalid dimensions, invalid alpha length, and undefined sampling enum values are malformed API input and throw.

## Exact numeric representation

Every finite IEEE-754 `float` is an exact dyadic number `m * 2^e`. Decode its sign, exponent, and significand from its bit representation. For each classification:

1. multiply each exact `u` value by texture width and each exact `v` by texture height;
2. choose a common binary exponent no greater than `-1` and no greater than any input exponent;
3. left-shift each significand onto that common lattice.

The decoder canonicalizes both signed zero encodings to `(0, 0)` and removes powers of two from every nonzero significand while increasing its exponent. Focused tests cover `+0.0f`, `-0.0f`, the smallest positive subnormal, a negative non-integer dyadic value, and an ordinary positive power of two. These tests pressure the exact-number foundation directly instead of relying only on later geometry outcomes.

On this lattice, one texel is an integer `Scale = 2^-e` and half a texel is `Scale / 2`. Texel edges, texel centers, Repeat periods, and bilinear support boundaries are therefore exact integers. No epsilon, threshold, or rounding-to-opaque is needed.

Clipping a triangle edge against an integer boundary can create a non-dyadic rational vertex. Represent such coordinates as a `BigInteger` numerator and a positive `BigInteger` denominator. Keep the helper minimal: comparison, addition/subtraction, multiplication by an integer, and exact segment/boundary intersection only. Canonicalize zero and denominator sign; greatest-common-divisor reduction is optional because at most four box sides clip a domain with at most seven vertices. All comparisons use cross multiplication, so no floating conversion occurs.

Geometry degeneracy uses the same float-to-dyadic conversion without texture scaling. Exact `BigInteger` subtraction and cross products decide whether the three 3D positions are collinear.

The representation is exact relative to the C# float values supplied to production. JSON parsing into floats occurs in test-only fixture adaptation and does not introduce any later classifier tolerance rule.

## Continuous UV domain

The exact scaled UV vertices form a convex hull. Initial vertices lie on the dyadic integer lattice; clipping vertices use the exact rational representation described above:

- three non-collinear points: a closed triangle;
- collinear distinct points: the closed segment between the extreme points;
- identical points: a closed point.

UV collapse does not imply `Unknown`; only geometry collapse does. One convex-domain intersection primitive supports all three domain dimensions.

To test a candidate axis-aligned support region with open or closed sides:

1. clip the UV domain against the closed version of the region using exact rational segment/boundary intersections and half-plane predicates;
2. reject an empty clipped domain;
3. for every open side, require at least one vertex of the clipped point/segment/polygon to lie strictly inside that side.

If each open constraint has a strict witness, the average of those witnesses lies in the convex clipped domain and satisfies every strict constraint simultaneously. This handles half-open Point cells and open bilinear support without treating zero-weight boundaries as reachable.

## Point filtering

In texture-scaled coordinates, an unwrapped Point cell with integer indices `(cx, cy)` is:

```text
[cx, cx + 1) × [cy, cy + 1)
```

The exact intersection test covers the entire UV triangle/segment/point, including edges and vertices. A non-opaque texel whose actual half-open cell intersects the domain is a witness for `MustRemainTransparent`. If the complete bounded candidate set contains no such witness, every reachable Point texel is `255`, so the result is `ProvenOpaque`.

### Point + Clamp

Clamp is handled through cell preimages in the original, unclamped domain:

- with size `1`, the only texel interval is all real coordinates;
- texel `0` uses `(-∞, 1)`;
- interior texel `i` uses `[i, i + 1)`;
- texel `size - 1` uses `[size - 1, +∞)`.

This exactly includes all coordinates below `0`, above `1`, and the normalized endpoint `1` in the edge texels selected by the fixture contract. Monotonic clamp bounds restrict the candidate index rectangle before any texel lookup.

### Point + Repeat

Every integer unwrapped cell `(cx, cy)` maps to stored texel:

```text
(FloorMod(cx, width), FloorMod(cy, height))
```

`FloorDiv` and `FloorMod` use mathematical floor semantics, not truncation toward zero, so negative UVs are correct. Exact half-open cells assign an integer Repeat seam to the first cell of the next period, matching `t - floor(t)`.

Before determining candidates, subtract one common whole texture period per axis from all three vertices so the minimum coordinate lies in the base period. This translation preserves Repeat sampling, removes huge common offsets, and does not alter triangle shape or span. Candidate ranges depend on span rather than absolute coordinate magnitude.

Triangles spanning several periods are processed exactly while their candidate grid remains within `MaxSupportRegions`. The candidate count is computed as `BigInteger` before conversion to loop indices. If it exceeds the budget, the mixed-texture result is `Unknown`; the algorithm never walks an unbounded number of periods.

## Bilinear filtering

For texel index `cx` in texture-scaled coordinates, the one-dimensional linear kernel has positive weight exactly on:

```text
(cx - 0.5, cx + 1.5)
```

A two-dimensional texel contributes positive weight on the Cartesian product of its horizontal and vertical intervals. The boundaries are open because the kernel weight is zero exactly one texel away from the texel center.

All alpha bytes are at most `255`. Therefore a bilinear sample is exactly `255` if and only if every texel with positive weight at that location is `255`. Any reachable non-opaque texel support is an exact witness that the interpolated alpha is below `255`; its positive weight may be arbitrarily small.

### Bilinear + Clamp

Neighbor indices are independently clamped. Their support preimages are:

- with size `1`, the only texel interval is all real coordinates;
- texel `0` uses `(-∞, 1.5)`;
- interior texel `i` uses `(i - 0.5, i + 1.5)`;
- texel `size - 1` uses `(size - 1.5, +∞)`.

The infinite edge tails account for clamped out-of-range neighbors accumulating into the edge texel. Candidate texel bounds expand the clamped domain bounding box by one texel before exact support intersection.

The approved `bilinear-filter-boundary` triangle intersects the open support of the first transparent column even though its vertices remain geometrically left of the alpha split, so it produces `MustRemainTransparent` without sampling.

### Bilinear + Repeat

Each unwrapped texel index has the same open support interval `(cx - 0.5, cx + 1.5)` and maps to the stored index with `FloorMod`. Supports naturally cross seams and include wrapped neighbors at both texture edges. Integer-period normalization and the same precomputed work bound apply. A one-texel axis maps every unwrapped support back to that single texel and remains correct.

## Exact, conservative, and unsupported behavior

Exact behavior:

- alpha equality (`255` only);
- finite-float geometry degeneracy;
- continuous UV triangle, line, and point domains;
- Point half-open sampling cells;
- Bilinear positive-weight support;
- Clamp edge preimages;
- Repeat seams, negative coordinates, and common integer-period translations;
- all candidate intersections processed within the work budget.

Conservative behavior:

- a mixed texture whose complete candidate support count exceeds `65536` returns `Unknown` before enumeration;
- any future unsupported sampling enum or malformed value is rejected at the API boundary rather than guessed.

The workload limit can only replace a possible `ProvenOpaque` or `MustRemainTransparent` with `Unknown`; it cannot create `ProvenOpaque`. There is no numeric over-approximation and no tolerance band in the selected design.

`65536` is an operational first-milestone ceiling (a `256 × 256` candidate grid), not a semantic tolerance. It is far above every approved fixture while placing a concrete upper bound on exact clipping work. Profiling may later change the ceiling or replace the enumeration strategy; either change affects conservative coverage, never the meaning of `ProvenOpaque`.

## Complexity and performance

Constructing `AlphaTextureData` costs `O(width * height)` once to copy and summarize alpha bytes. It is intended to be reused for many triangle calls.

Per triangle:

- validation, geometry degeneracy, UV hull creation, uniform-texture fast paths, and Repeat normalization are constant-count exact operations;
- candidate generation is proportional to the texture-scaled UV bounding region expanded by filter support and by the number of Repeat periods crossed;
- at most `65536` candidate support regions are examined;
- each region performs one alpha lookup and, only for alpha below `255`, a constant-size exact convex clipping/intersection operation.

Thus the normal cost is `O(C)` for `C` candidate support regions, capped at `65536`, rather than `O(texture area)` for every small triangle. `BigInteger` operand cost grows with the bit length of the float-derived coordinates, but Repeat normalization prevents large common offsets from inflating loop counts. Uniform textures return before candidate enumeration.

Caching, spatial indexes, Burst, Jobs, SIMD, GPU work, and parallelism are deferred until correctness is established and profiling demonstrates a need.

## Fixture-family mapping

| Fixture family | Cases | Design behavior |
|---|---|---|
| Intentional uncertainty | `degenerate-triangle`, `missing-uv0` | exact geometry check or explicit missing channel returns `Unknown` |
| Uniform alpha | `fully-opaque-texture`, `alpha-254-boundary`, `fully-transparent-texture` | validated uniform fast paths enforce `255` exactly |
| Point + Clamp | `mixed-alpha-texture`, `triangle-in-opaque-region`, `triangle-in-transparent-region`, `triangle-crosses-alpha-boundary`, `mixed-triangle-mesh`, `outside-uv-clamp` | exact Point-cell preimage intersection per triangle |
| Point + Repeat | `outside-uv-repeat` | whole-period normalization, unwrapped cells, floor modulus |
| Bilinear + Clamp | `bilinear-filter-boundary` | exact open positive-weight support reaches transparent texels |

Direct production tests, without changing the catalogs, cover collapsed UV line/point domains, negative Repeat coordinates, exact seams, several periods, Bilinear + Repeat, one-texel axes, huge common offsets, the work cap, winding invariance, and zero-weight support boundaries.

## TDD sequence

1. Add fixture-adapter tests for missing UV0 and degenerate geometry; accept the one initial compile-red before production types exist, add the minimal API shell, then drive the float decoder and exact geometry check through executable assertion failures.
2. Add fixture tests for uniform `255`, `254`, and `0`; implement immutable alpha data and uniform fast paths.
3. Add the Point + Clamp fixture family; implement exact dyadic UV domains, candidate bounds, Clamp preimages, and half-open intersection.
4. Add Point + Repeat fixture and metamorphic/edge tests; implement floor arithmetic, period normalization, bounded candidates, and `Unknown` on budget exhaustion.
5. Add Bilinear + Clamp fixture and boundary/collapsed-domain tests; implement positive-weight support and clamped edge tails.
6. Add Bilinear + Repeat seam and translation tests; reuse Repeat enumeration with bilinear supports.
7. Add focused adversarial/property tests, run all thirteen fixture outcomes and the complete EditMode suite, and inspect Console and Git/Unity asset integrity.

Each family first fails for the absent behavior, then receives only the smallest implementation required to turn that family green. Expected fixture outcomes remain solely in `fixture-expectations.json` and are joined to actual results only after actual classification.

The initial Task 1 red is intentionally a compiler failure because no production types exist. Once that boundary compiles, every later red step should be an executable assertion failure; adding another intentionally uncompilable test is not acceptable when a minimal compiling shell can expose the missing behavior at runtime.

## Metamorphic and adversarial coverage

The focused direct tests should establish:

- reversing geometry winding and corresponding UV order does not change the result;
- integer UV translation under Repeat does not change the result;
- UV line and point collapse remain classifiable;
- a tiny domain and a long thin domain cannot skip a crossed cell;
- a triangle with vertices in opaque cells can still be rejected when an edge/interior reaches non-opaque support;
- negative Repeat coordinates and exact integer seams use floor semantics;
- `FloorDiv`/`FloorMod` handle `-period`, one cell below and above that boundary, and the final negative cell before zero;
- Clamp at exactly normalized `0` and `1` selects the correct edge behavior;
- an opaque-side triangle whose bilinear support reaches transparency is rejected;
- one-texel width/height works for both filters and wraps;
- huge common Repeat offsets normalize without overflow;
- huge mixed-texture spans return `Unknown` before excessive work;
- all-opaque and all-nonopaque fast paths remain valid for huge spans;
- alpha `254` never becomes opaque;
- invalid/non-finite data throws rather than becoming `Unknown`.

## Validation plan

During implementation:

- run each focused EditMode test red before the corresponding production behavior;
- run it green after the minimal behavior;
- keep all fixture integrity tests green;
- run the complete EditMode suite after every coherent task;
- confirm all fourteen triangle outcomes across the thirteen approved cases match the independent expectation catalog;
- read current compiler errors, warnings, and relevant Console error messages;
- inspect unstaged and staged diffs separately;
- run `git diff --check`;
- verify only intended `.cs`, `.meta`, spec, and plan files changed and no GUID, manifest, dependency, CI, or release changes occurred.

The private Unity testbed is neither needed nor authorized.

## Known risks

- `BigInteger` and constant-size rational clipping may be slower than floating-point geometry at avatar scale. The bounded workload and uniform fast paths protect this milestone; profiling must precede any optimization.
- A fixed support-region budget creates conservative false negatives on large mixed-texture spans. This is intentional and visible as `Unknown`.
- Exact support-region clipping must preserve strict/open sides. Treating bilinear zero-weight boundaries as positive or Point upper boundaries as inclusive would change semantics; dedicated tests are required.
- C# integer division truncates toward zero. All negative-coordinate paths must use reviewed `FloorDiv`/`FloorMod` helpers.
- Cached texture summaries require immutable alpha storage; retaining the caller's mutable list would be unsafe.

## Explicitly deferred work

- transforming or splitting meshes;
- creating or rewriting materials;
- creating optimization plans or NDMF passes;
- animation, material-state, or shader semantics;
- texture import and mipmaps;
- profitability and whole-avatar caching;
- performance acceleration or parallelism;
- private-avatar and third-party integration behavior;
- CI, publishing, and release changes.

## Verified design-phase baseline

- Branch: `feat/geometry-classifier`, clean, at the same commit as `main` (`a259d76`).
- Unity project: `E:/AI/Git/alpha-material-optimizer-ndmf`.
- Unity: `2022.3.22f1`.
- Embedded package: `com.alrauna.alpha-material-optimizer@0.0.1`.
- Discovered tests: nine EditMode tests and one unrelated PlayMode listing entry.
- Console: zero compiler diagnostics, zero warnings, and two duplicate error-level entries for the intentionally scriptless production Editor asmdef.
- Private testbed: not connected, inspected, or modified.
