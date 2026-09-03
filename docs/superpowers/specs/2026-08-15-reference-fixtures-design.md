# Deterministic Reference Fixtures Design

**Date:** 2026-08-15

**Status:** Approved

## Purpose

Define the first public reference-fixture framework. Define executable semantic specifications for future texture-alpha and triangle analysis. The framework supplies deterministic inputs and independent expected outcomes. It does not implement an analyzer, classifier, optimization plan, transformation, or NDMF pass.

All fixtures are synthetic, minimal, redistributable, deterministic, and human-auditable. They do not depend on private avatars, production shaders, imported texture behavior, or the private Unity testbed.

## Scope

This increment contains:

- one machine-readable input catalog.
- one separate machine-readable expectation catalog.
- test-only C# data loading and in-memory `Texture2D`/`Mesh` construction.
- EditMode integrity tests for the catalogs and fixture framework.
- thirteen small reference cases.

This increment does not contain:

- a production alpha-material classifier.
- mesh or material transformation.
- optimization-plan or NDMF integration.
- material rewriting, animation tracing, shader adapters, or private-avatar compatibility logic.
- production shader or material fixtures.
- mipmap cases or mipmap semantics.
- CI or release/listing workflow changes.

## Architecture decision

Use two JSON catalogs plus the minimum test-only C# support:

```text
Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/
  Data/
    fixture-inputs.json
    fixture-expectations.json
  ReferenceFixtureData.cs
  ReferenceFixtureIntegrityTests.cs
```

Unity `.meta` files accompany each added directory and file. No committed PNG, mesh, material, ScriptableObject, scene, or prefab assets are needed. Texture and mesh objects exist only in memory during tests.

JSON keeps input changes readable in Git, separates data from C# behavior, and permits a future Blender/Python implementation to consume the same fixture data. The built-in Unity JSON facilities and existing NUnit test assembly are sufficient. The framework needs no new dependency.

The rejected alternatives are:

- C#-only fixture declarations, because they mix data with test behavior and are less portable.
- committed Unity assets, because they add binary data, importer behavior, GUID surface area, and unnecessary `.meta` churn.

## Separation of responsibilities

`fixture-inputs.json` is the sole source of texture, geometry, UV, filter, and wrap inputs. `fixture-expectations.json` is the independent semantic oracle. Neither catalog is generated from the other.

The C# loader parses and validates data. Its builder may translate input records into disposable Unity `Texture2D` and `Mesh` objects for future Unity tests, but it remains in the test assembly. It must not contain classification logic.

The builder must not read the expectation catalog. The expectation loader must not inspect or sample constructed Unity objects. Future analyzer tests may join analyzer results to oracle records by case ID only at the assertion boundary.

Building a case must not mutate state observable by another case. Each case must use disposable, case-local Unity texture and mesh objects, or isolate them equivalently. Shared logical texture or mesh definitions in the input catalog must not imply shared mutable Unity objects. In particular, setting one case's filter or wrap mode must not change any previously built case.

Unity texture sampling APIs must never derive or rewrite expected outcomes. Authors define expected outcomes directly from the semantic contract in this document. Unity construction verifies that the portable fixture data can exist in the current test environment. It is not the oracle.

## Input catalog schema

The input catalog has this logical shape:

```json
{
  "schemaVersion": 1,
  "textures": [
    {
      "id": "alpha-255-2x2",
      "width": 2,
      "height": 2,
      "alpha8BottomToTop": [255, 255, 255, 255]
    }
  ],
  "meshes": [
    {
      "id": "single-triangle-unit",
      "positions": [0, 0, 0, 1, 0, 0, 0, 1, 0],
      "uv0Status": "Present",
      "uv0": [0, 0, 1, 0, 0, 1],
      "triangleVertexIndices": [0, 1, 2]
    }
  ],
  "cases": [
    {
      "id": "fully-opaque-texture",
      "textureId": "alpha-255-2x2",
      "meshId": "single-triangle-unit",
      "filterMode": "Point",
      "wrapMode": "Clamp"
    }
  ]
}
```

Schema version 1 accepts only `Point` and `Bilinear` filter modes and only `Clamp` and `Repeat` wrap modes. The only UV states are `Present` and `Missing`. A present UV array has exactly two values per vertex. A missing UV channel uses `"uv0Status": "Missing"` and an empty `uv0` array. This makes the missing data intentional and distinguishes it from an invalid catalog entry.

All JSON numeric values must be finite. Texture dimensions are positive integers. Alpha values are integers from 0 through 255. Position and UV values are JSON numbers. Vertex indices are non-negative integers within the referenced vertex array.

Catalog files use UTF-8 JSON. Array order is semantically significant for structural numeric arrays such as alpha bytes, positions, UVs, and triangle indices. Collection order of texture, mesh, input-case, and expectation-case records is not semantically significant. IDs identify those records. JSON object-property order is not significant. IDs are globally unique across textures, meshes, and input cases. Expectation case IDs correspond to those input-case IDs.

## Cross-language coordinate and data conventions

These rules are part of schema version 1 and do not rely on Unity defaults.

### Texture storage and alpha

- `alpha8BottomToTop` is a flat row-major array.
- Rows are stored from bottom to top. Values within each row run from left to right.
- Texel `(x, y)` is stored at index `y * width + x`. `y = 0` is the bottom row.
- Normalized UV `(0, 0)` is the lower-left texture corner. `u` increases to the right and `v` increases upward. No implementation may flip `v` implicitly.
- Texel `(x, y)` has center `((x + 0.5) / width, (y + 0.5) / height)`.
- Each stored value is an unsigned straight-alpha byte. RGB, color space, premultiplication, material cutoff, and shader behavior are outside the schema.
- Alpha byte `255` is exactly fully opaque. Every byte below `255`, including `254`, `128`, and `0`, is not fully opaque. No threshold or rounding promotes a lower value to `255`.
- Schema version 1 describes only the base texture level. Mipmaps are neither generated nor sampled.
- Constructed fixture textures must have mipmaps disabled. The test-only builder must not create mip levels implicitly or explicitly.

### Wrap and filtering

Portable implementations use these definitions when interpreting fixture inputs:

- `Repeat` maps a normalized coordinate `t` to `t - floor(t)`.
- `Clamp` constrains a normalized coordinate to the closed interval `[0, 1]`.
- Point sampling selects `min(floor(t * size), size - 1)` after the coordinate is wrapped or clamped.
- Bilinear sampling maps the wrapped or clamped coordinate to texel space as `p = t * size - 0.5`. Let `i = floor(p)` and `f = p - i`. The samples at integer texel coordinates `i` and `i + 1` have weights `1 - f` and `f`. Neighbor indices are independently repeated or clamped according to the case's wrap mode. In two dimensions, the four weights are the products of the corresponding horizontal and vertical weights.
- Bilinear interpolation operates on alpha values as exact real-number weights for semantic purposes. An interpolated value below `255` is not fully opaque.

Fixture coordinates avoid unnecessary exact-seam ambiguity. The one bilinear-boundary case deliberately places part of a triangle inside the filter footprint of both opaque and transparent texels.

### Geometry, UVs, and triangles

- `positions` is a flat array of `(x, y, z)` triples in vertex order.
- Positions use an abstract Cartesian fixture space. All version 1 meshes are planar at `z = 0`. Only topology and degeneracy are semantically relevant.
- `uv0` is a flat array of `(u, v)` pairs in the same vertex order as `positions`.
- `triangleVertexIndices` is a flat zero-based index array. Each consecutive triple `(i0, i1, i2)` defines one triangle.
- Triangle index `n` refers to entries `3n`, `3n + 1`, and `3n + 2` in `triangleVertexIndices`. Triangle order is therefore explicit and stable.
- Nondegenerate version 1 triangles use counter-clockwise order when projected onto the XY plane. Builders preserve the recorded order.
- Winding has no significance for version 1 alpha classification or expected outcomes. This rule only removes cross-language ambiguity. The degenerate triangle has no meaningful winding.

For a nondegenerate geometry triangle with present UV0, let the three indexed vertex UVs be `q0`, `q1`, and `q2`. Its sampling domain is the continuous closed barycentric UV domain

`D = {b0*q0 + b1*q1 + b2*q2 | b0, b1, b2 >= 0 and b0 + b1 + b2 = 1}`.

`D` is a closed triangle when the UV mapping is nondegenerate. It may collapse to a closed line segment or point when the UV mapping is degenerate.

Semantic outcomes consider every UV reachable anywhere in `D`, including its interior, edges, and vertices, together with the case's wrap and filter footprint. Testing only vertex UVs, texel centers, or any other finite sample set does not satisfy the fixture contract.

## Expectation catalog and outcome semantics

The independent expectation catalog has this logical shape:

```json
{
  "schemaVersion": 1,
  "cases": [
    {
      "caseId": "fully-opaque-texture",
      "triangleOutcomes": [
        {
          "triangleIndex": 0,
          "outcome": "ProvenOpaque"
        }
      ]
    }
  ]
}
```

The only valid outcomes are:

- `ProvenOpaque`: the fixture establishes that the triangle can sample only alpha exactly equal to `255` under the stated UV, wrap, and filter semantics.
- `MustRemainTransparent`: the fixture establishes known possible sampling below `255`. This includes fully transparent, mixed, partially transparent, and bilinearly blended values. Alpha `254` and `128` are `MustRemainTransparent`, not `Unknown`.
- `Unknown`: the analyzer cannot establish a safe supported classification from the available information.

`Unknown` represents insufficient or unsupported analysis, not known transparency. It is conservative. Implementations must never interpret it as permission to optimize. Unrecognized outcome strings, schema versions, filter modes, wrap modes, or UV states are catalog errors. Validation must reject them instead of using a default outcome.

## Initial fixture catalog

The catalog contains exactly thirteen cases.

| Case ID | Minimal input | Expected outcome by triangle |
|---|---|---|
| `fully-opaque-texture` | Point/Clamp; 2x2 texture containing only alpha 255; one unit triangle | `0: ProvenOpaque` |
| `alpha-254-boundary` | Point/Clamp; 2x2 texture containing only alpha 254; one unit triangle | `0: MustRemainTransparent` |
| `fully-transparent-texture` | Point/Clamp; 2x2 texture containing only alpha 0; one unit triangle | `0: MustRemainTransparent` |
| `mixed-alpha-texture` | Point/Clamp; 2x2 checker alpha texture; one triangle covering opaque and transparent texels | `0: MustRemainTransparent` |
| `triangle-in-opaque-region` | Point/Clamp; triangle wholly in the opaque half of a vertical 4x4 split texture | `0: ProvenOpaque` |
| `triangle-in-transparent-region` | Point/Clamp; triangle wholly in the transparent half of the same split texture | `0: MustRemainTransparent` |
| `triangle-crosses-alpha-boundary` | Point/Clamp; triangle spanning both halves of the split texture | `0: MustRemainTransparent` |
| `mixed-triangle-mesh` | Point/Clamp; two triangles, one wholly in each half of the split texture | `0: ProvenOpaque`, `1: MustRemainTransparent` |
| `outside-uv-clamp` | Point/Clamp; all triangle `u` values above 1, clamping to the transparent right edge | `0: MustRemainTransparent` |
| `outside-uv-repeat` | Same texture and mesh as the preceding case, but Point/Repeat wraps into the opaque left region | `0: ProvenOpaque` |
| `degenerate-triangle` | Point/Clamp; collinear positions, valid UV0, and an opaque texture | `0: Unknown` |
| `missing-uv0` | Point/Clamp; valid indexed triangle, intentional `Missing` UV0 state, and an opaque texture | `0: Unknown` |
| `bilinear-filter-boundary` | Bilinear/Clamp; triangle vertices remain on the nominally opaque side, but part of its filter footprint blends with transparent texels | `0: MustRemainTransparent` |

The reusable textures are:

- `alpha-255-2x2`: four values of `255`.
- `alpha-254-2x2`: four values of `254`.
- `alpha-0-2x2`: four values of `0`.
- `mixed-checker-2x2`: bottom row `[255, 0]`, top row `[0, 255]`.
- `split-vertical-4x4`: every bottom-to-top row is `[255, 255, 0, 0]`.

The Clamp and Repeat cases share geometry with UVs `(1.125, 0.125)`, `(1.375, 0.125)`, and `(1.125, 0.375)`. Clamp reaches the transparent right edge. Repeat maps the full triangle into the opaque left half.

The bilinear case uses the vertical split texture and includes UVs with `u` between `0.375` and `0.49`. The rightmost opaque texel center is at `u = 0.375`. Values above it have a bilinear footprint that includes the first transparent column. These values remain left of the geometric split at `u = 0.5`.

## Framework integrity tests

The EditMode integrity tests validate the framework, not a nonexistent classifier:

1. Both catalogs load twice to structurally identical records.
2. Both schema versions are supported and equal.
3. Texture, mesh, and case IDs are non-empty and globally unique.
4. Every case references an existing texture and mesh definition.
5. Texture dimensions and alpha-array lengths agree, and every alpha value is a byte.
6. Position arrays contain complete triples and at least three vertices.
7. UV status and data agree. `Present` has exactly two values per vertex. `Missing` has an empty array.
8. Triangle arrays contain complete triples, and every vertex index exists.
9. Every input case has exactly one expectation record, and no expectation lacks an input case.
10. Every expected triangle index exists and occurs exactly once. Together, the outcomes cover every triangle in the referenced mesh.
11. Filter, wrap, UV-state, and outcome strings are from the closed schema version 1 sets.
12. Build the same input twice. Verify that the textures and meshes are isolated. Verify identical dimensions, alpha bytes, positions, UV state/data, index order, filter mode, and wrap mode. Verify that mutating one built case cannot affect another.
13. Every constructed texture has mipmaps disabled and exposes only the base level.
14. The catalog contains exactly the thirteen approved case IDs. Targeted contract assertions preserve the alpha-254 `MustRemainTransparent` boundary. They also preserve the degenerate/missing-UV `Unknown` outcomes without duplicating every oracle value in C#.

Tests do not invoke a classifier. They do not infer an oracle through a test-side classifier. They do not call Unity sampling APIs to calculate expected outcomes.

## Validation after implementation

After the fixture files and tests are implemented:

- run the complete EditMode suite and record the observed result.
- confirm zero compiler errors.
- inspect unstaged and staged diffs separately.
- confirm only package test fixtures, test support, tests, and their `.meta` files changed.
- check for unexpected GUID or package-manifest changes.
- confirm no private/testbed content or new dependency appears.
- leave release/listing workflows and CI unchanged.

The private Unity testbed is not needed for this increment and must not be accessed or modified.
