# Triangle Alpha Geometry Classifier Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` for inline implementation. Use `superpowers:subagent-driven-development` only if the user separately authorizes subagents. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a pure Editor-only classifier that returns `ProvenOpaque`, `MustRemainTransparent`, or `Unknown` for one geometry triangle over the approved continuous Point/Bilinear and Clamp/Repeat alpha semantics.

**Architecture:** Adapt fixture inputs into immutable production value/data types, represent finite floats exactly on a texture-scaled dyadic `BigInteger` lattice, and use a minimal exact rational type for vertices created by clipping the full UV triangle/segment/point against Point cells or Bilinear positive-weight support regions. Normalize Repeat by whole periods and return `Unknown` before mixed-texture candidate work exceeds `65536` support regions.

**Tech Stack:** Unity 2022.3.22f1, C# Editor assembly, `UnityEngine.Vector2`/`Vector3`, `System.Numerics.BigInteger`, NUnit EditMode tests, and the existing deterministic fixture catalogs.

## Global Constraints

- Do not start implementation until the user explicitly approves `docs/superpowers/specs/2026-08-15-geometry-classifier-design.md` and this plan.
- Work on `feat/geometry-classifier`; recheck branch, status, merge base, and user changes before editing.
- Alpha byte `255` is the only opaque value. Values `0` through `254` remain non-opaque without thresholds or rounding.
- Classify the entire continuous closed barycentric UV domain, including edges, vertices, UV line collapse, and UV point collapse.
- Geometry degeneracy and intentional missing UV0 return `Unknown`; malformed/non-finite inputs throw.
- Use exact dyadic/integer boundary reasoning. Do not introduce numeric epsilons, finite sampling, raster grids, or Unity texture-sampling APIs.
- `MaxSupportRegions` is exactly `65536`; exceeding it on a mixed texture returns `Unknown` before enumeration.
- Keep production code independent of fixture DTOs, expectation data, `Mesh`, `Texture2D`, NDMF state, assets, files, GameObjects, and MCP.
- Keep actual fixture classification separate from expectation lookup until the assertion boundary.
- The first Task 1 red may be a compiler failure because no production analysis types exist. After the production boundary compiles, record subsequent red states as executable assertion failures, using minimal compiling method shells where necessary.
- Do not modify either fixture JSON catalog, fixture expected outcomes, package manifests/locks, dependencies, asmdefs, workflows, release automation, scenes, or private testbed content.
- Do not add a dependency, generic property-testing framework, caching framework, jobs/Burst/GPU path, transform logic, or speculative future integration abstraction.
- Preserve Unity `.meta` files as asset pairs and inspect GUID/scope changes before completion.
- Do not commit or push unless the user separately authorizes it.

---

## File map

**Create during approved implementation:**

- `Packages/com.alrauna.alpha-material-optimizer/Editor/AssemblyInfo.cs` — expose internal production types only to the existing Editor test assembly.
- `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/TriangleAlphaClassifier.cs` — result/input types, immutable alpha data, validation, fast paths, support enumeration, and public classifier boundary.
- `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/ExactUvGeometry.cs` — exact float-to-dyadic conversion, minimal rational clipping, geometry degeneracy, scaled UV hull, exact interval/box intersection, and floor arithmetic.
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs` — fixture adapters, fixture-family tests, direct boundary tests, and metamorphic tests.
- Matching `.meta` files for each new folder and file.

**Read but do not modify:**

- `docs/superpowers/specs/2026-08-15-reference-fixtures-design.md`
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data/fixture-inputs.json`
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/Data/fixture-expectations.json`
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureData.cs`
- `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/ReferenceFixtures/ReferenceFixtureIntegrityTests.cs`
- both existing asmdefs, package metadata, NDMF/bootstrap files, manifests, and workflows.

## Shared production interfaces

Keep these exact names across tasks unless an approved red/green step demonstrates a concrete correction is needed:

```csharp
namespace Alrauna.AlphaMaterialOptimizer.Editor.Analysis
{
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

        internal AlphaSamplingSettings(
            AlphaFilterMode filterMode,
            AlphaWrapMode wrapMode);
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
        internal bool IsFullyOpaque { get; }
        internal bool IsFullyNonOpaque { get; }

        internal AlphaTextureData(
            int width,
            int height,
            IReadOnlyList<byte> alpha8BottomToTop);

        internal byte GetAlpha(int x, int y);
    }

    internal static class TriangleAlphaClassifier
    {
        internal const int MaxSupportRegions = 65536;

        internal static TriangleAlphaOutcome Classify(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaSamplingSettings sampling);
    }
}
```

`ExactUvGeometry.cs` remains an internal implementation detail. It produces a point/segment/polygon domain on one exact integer lattice and tests it against intervals whose finite bounds use that lattice:

```csharp
internal readonly struct ExactDyadic
{
    internal BigInteger Significand { get; }
    internal int Exponent { get; }
}

internal readonly struct ExactRational
{
    internal BigInteger Numerator { get; }
    internal BigInteger Denominator { get; } // Always positive.
}

internal readonly struct ExactUvPoint
{
    internal ExactRational X { get; }
    internal ExactRational Y { get; }
}

internal sealed class ExactUvDomain
{
    internal IReadOnlyList<ExactUvPoint> Vertices { get; }
    internal BigInteger TexelScale { get; }
}

internal readonly struct ExactInterval
{
    internal bool HasLowerBound { get; }
    internal ExactRational LowerBound { get; }
    internal bool IsLowerInclusive { get; }
    internal bool HasUpperBound { get; }
    internal ExactRational UpperBound { get; }
    internal bool IsUpperInclusive { get; }
}

internal static class ExactUvGeometry
{
    internal static ExactDyadic DecodeFloat(float value);

    internal static bool IsDegenerateGeometry(TriangleAlphaInput triangle);

    internal static ExactUvDomain CreateTextureScaledDomain(
        TriangleAlphaInput triangle,
        int textureWidth,
        int textureHeight);

    internal static ExactUvDomain NormalizeRepeat(
        ExactUvDomain domain,
        int textureWidth,
        int textureHeight);

    internal static bool Intersects(
        ExactUvDomain domain,
        ExactInterval x,
        ExactInterval y);

    internal static BigInteger FloorDiv(BigInteger value, BigInteger divisor);
    internal static int FloorMod(BigInteger value, int modulus);
}
```

## Shared fixture adapter

Add one adapter in `TriangleAlphaClassifierTests.cs`. It must compute all actual outcomes using only `FixtureInputCatalog`; only afterward may the assertion helper read `FixtureExpectationCatalog`:

```csharp
private static TriangleAlphaOutcome[] ClassifyInputCase(
    FixtureInputCatalog inputs,
    string caseId)
{
    var fixtureCase = ReferenceFixtureData.FindCase(inputs, caseId);
    var textureRecord = inputs.textures.Single(item => item.id == fixtureCase.textureId);
    var meshRecord = inputs.meshes.Single(item => item.id == fixtureCase.meshId);
    var texture = new AlphaTextureData(
        textureRecord.width,
        textureRecord.height,
        textureRecord.alpha8BottomToTop.Select(value => checked((byte)value)).ToArray());
    var sampling = new AlphaSamplingSettings(
        fixtureCase.filterMode == "Point" ? AlphaFilterMode.Point : AlphaFilterMode.Bilinear,
        fixtureCase.wrapMode == "Clamp" ? AlphaWrapMode.Clamp : AlphaWrapMode.Repeat);
    var results = new TriangleAlphaOutcome[meshRecord.triangleVertexIndices.Length / 3];

    for (var triangleIndex = 0; triangleIndex < results.Length; triangleIndex++)
    {
        var offset = triangleIndex * 3;
        var i0 = meshRecord.triangleVertexIndices[offset];
        var i1 = meshRecord.triangleVertexIndices[offset + 1];
        var i2 = meshRecord.triangleVertexIndices[offset + 2];
        var triangle = CreateTriangleInput(meshRecord, i0, i1, i2);
        results[triangleIndex] = TriangleAlphaClassifier.Classify(triangle, texture, sampling);
    }

    return results;
}

private static void AssertCaseMatchesOracle(string caseId)
{
    var catalogs = ReferenceFixtureData.Load();
    var actual = ClassifyInputCase(catalogs.Inputs, caseId);
    var expected = ReferenceFixtureData.FindExpectation(catalogs.Expectations, caseId)
        .triangleOutcomes
        .OrderBy(item => item.triangleIndex)
        .Select(item => item.outcome)
        .ToArray();

    CollectionAssert.AreEqual(expected, actual.Select(item => item.ToString()), caseId);
}
```

`CreateTriangleInput` copies the three indexed positions and either calls `WithUv0` or `MissingUv0`. It does not inspect expectation data or sample Unity objects.

```csharp
private static TriangleAlphaInput CreateTriangleInput(
    MeshFixtureRecord mesh,
    int i0,
    int i1,
    int i2)
{
    var p0 = PositionAt(mesh, i0);
    var p1 = PositionAt(mesh, i1);
    var p2 = PositionAt(mesh, i2);
    if (mesh.uv0Status == "Missing")
        return TriangleAlphaInput.MissingUv0(p0, p1, p2);

    return TriangleAlphaInput.WithUv0(
        p0, p1, p2,
        UvAt(mesh, i0), UvAt(mesh, i1), UvAt(mesh, i2));
}

private static Vector3 PositionAt(MeshFixtureRecord mesh, int index)
{
    return new Vector3(
        mesh.positions[index * 3],
        mesh.positions[index * 3 + 1],
        mesh.positions[index * 3 + 2]);
}

private static Vector2 UvAt(MeshFixtureRecord mesh, int index)
{
    return new Vector2(mesh.uv0[index * 2], mesh.uv0[index * 2 + 1]);
}
```

---

### Task 1: Explicit uncertainty and the production/test boundary

**Files:**

- Create: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`
- Create: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis.meta`
- Create: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs.meta`
- Create after red: `Packages/com.alrauna.alpha-material-optimizer/Editor/AssemblyInfo.cs`
- Create after red: `Packages/com.alrauna.alpha-material-optimizer/Editor/AssemblyInfo.cs.meta`
- Create after red: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis.meta`
- Create after red: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/TriangleAlphaClassifier.cs`
- Create after red: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/TriangleAlphaClassifier.cs.meta`
- Create after red: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/ExactUvGeometry.cs`
- Create after red: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/ExactUvGeometry.cs.meta`

**Interfaces:** Produces every shared type/signature, the fixture adapter, and exact geometry-degeneracy detection. All support enumeration remains absent.

- [ ] **Step 1: Reinspect the approved branch and contracts**

Run:

```powershell
git branch --show-current
git status --short --branch
git merge-base main HEAD
git diff --name-status main...HEAD
```

Expected: `feat/geometry-classifier`; no unrelated or unexplained changes. Re-read the design and fixture contract if the branch moved.

- [ ] **Step 2: Add the first fixture-driven classifier tests**

Add the shared adapter and these tests before creating production types:

```csharp
[TestCase("degenerate-triangle")]
[TestCase("missing-uv0")]
public void ExplicitUncertaintyMatchesOracle(string caseId)
{
    AssertCaseMatchesOracle(caseId);
}

[Test]
public void NonFiniteGeometryIsMalformed()
{
    var triangle = TriangleAlphaInput.MissingUv0(
        new Vector3(float.NaN, 0f, 0f),
        Vector3.right,
        Vector3.up);
    var texture = new AlphaTextureData(1, 1, new byte[] { 255 });

    Assert.Throws<ArgumentException>(() => TriangleAlphaClassifier.Classify(
        triangle,
        texture,
        new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp)));
}
```

- [ ] **Step 3: Observe the one permitted compile-red state**

Wait for Unity compilation, then read error-level Console entries. Expected: compile failures because the production analysis namespace/types do not exist. This is the only planned compile-red in the implementation sequence. Do not reinterpret the existing empty-asmdef message as this red result.

- [ ] **Step 4: Add only the compiling production boundary**

Create `AssemblyInfo.cs` exactly as:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Alrauna.AlphaMaterialOptimizer.Tests.Editor")]
```

Implement the shared input/result types, constructor validation, `ExactDyadic`, and the shared helper signatures. Use minimal compiling shells for exact behavior that later tests will drive: `DecodeFloat` returns canonical zero, `IsDegenerateGeometry` returns `false`, `FloorDiv` uses ordinary integer division, and `FloorMod` uses ordinary remainder. `Classify` follows this order:

```csharp
ValidateSampling(sampling);
ValidateFinitePositions(triangle);

if (ExactUvGeometry.IsDegenerateGeometry(triangle))
    return TriangleAlphaOutcome.Unknown;

if (!triangle.HasUv0)
    return TriangleAlphaOutcome.Unknown;

ValidateFiniteUvs(triangle);
return TriangleAlphaOutcome.Unknown;
```

Do not claim this shell is the implementation; its purpose is to make the next red state executable.

- [ ] **Step 5: Add direct float-decoder and exact-geometry tests**

Canonicalize zero to `(0, 0)` and remove powers of two from every nonzero significand. Add:

```csharp
[TestCase(0, 0, 0)]                 // +0.0f
[TestCase(unchecked((int)0x80000000), 0, 0)] // -0.0f
[TestCase(0x00000001, 1, -149)]     // Smallest positive subnormal.
[TestCase(unchecked((int)0xBFC00000), -3, -1)] // -1.5f.
[TestCase(0x41000000, 1, 3)]        // 8.0f.
public void FloatDecoderProducesCanonicalExactDyadic(
    int bits,
    long expectedSignificand,
    int expectedExponent)
{
    var value = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
    var decoded = ExactUvGeometry.DecodeFloat(value);
    Assert.That(decoded.Significand, Is.EqualTo(new BigInteger(expectedSignificand)));
    Assert.That(decoded.Exponent, Is.EqualTo(expectedExponent));
}

[Test]
public void GeometryDegeneracyUsesExactDecodedValues()
{
    var degenerate = TriangleAlphaInput.MissingUv0(
        Vector3.zero,
        new Vector3(1f, 0.5f, 0.25f),
        new Vector3(2f, 1f, 0.5f));
    Assert.That(ExactUvGeometry.IsDegenerateGeometry(degenerate), Is.True);
}
```

- [ ] **Step 6: Observe executable assertion failures**

Run the decoder and exact-geometry tests. Expected: the nonzero decoder cases and geometry-degeneracy assertion fail while the project compiles. If the run is compile-red, fix only the boundary/signature problem and rerun before recording the red evidence.

- [ ] **Step 7: Implement exact decoding and geometry degeneracy**

Decode each finite float exactly, canonicalize signed zero and trailing binary factors as specified, move geometry axes to common exponents, compute `(p1 - p0) × (p2 - p0)` with `BigInteger`, and return true only when all three components are zero.

- [ ] **Step 8: Run focused tests green**

Use Unity MCP `run_tests` with `mode: EditMode` and test names for `FloatDecoderProducesCanonicalExactDyadic`, `GeometryDegeneracyUsesExactDecodedValues`, `ExplicitUncertaintyMatchesOracle`, and `NonFiniteGeometryIsMalformed`; poll `get_test_job(wait_timeout: 60, include_failed_tests: true)`. Expected: all focused tests pass, including both parameterized fixture cases; zero compiler errors.

- [ ] **Step 9: Run the existing fixture integrity tests**

Run `Alrauna.AlphaMaterialOptimizer.Tests.Editor.ReferenceFixtures.ReferenceFixtureIntegrityTests` in EditMode. Expected: all existing integrity tests pass unchanged.

- [ ] **Step 10: Review the task diff**

Run `git diff --check`, `git diff --stat`, and `git status --short`. Confirm no fixture JSON, asmdef, manifest, workflow, or ignored Unity state changed.

---

### Task 2: Exact alpha and uniform-texture fast paths

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`
- Modify: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/TriangleAlphaClassifier.cs`

**Interfaces:** `AlphaTextureData` owns a copied byte array and exposes cached `IsFullyOpaque`, `IsFullyNonOpaque`, and bounded `GetAlpha`.

- [ ] **Step 1: Add the uniform fixture family and immutability tests**

```csharp
[TestCase("fully-opaque-texture")]
[TestCase("alpha-254-boundary")]
[TestCase("fully-transparent-texture")]
public void UniformAlphaCasesMatchOracle(string caseId)
{
    AssertCaseMatchesOracle(caseId);
}

[Test]
public void TextureDataCopiesCallerAlpha()
{
    var source = new byte[] { 255 };
    var texture = new AlphaTextureData(1, 1, source);
    source[0] = 0;
    Assert.That(texture.GetAlpha(0, 0), Is.EqualTo(255));
    Assert.That(texture.IsFullyOpaque, Is.True);
}
```

- [ ] **Step 2: Run executable assertion-red**

Run the two focused tests. Expected: the project compiles; uniform fixture assertions fail because the classifier still returns `Unknown`, and the immutability assertion fails until storage is copied/cached.

- [ ] **Step 3: Implement exact uniform behavior**

In the constructor, require `width > 0`, `height > 0`, non-null alpha, and exact checked length `width * height`; copy once while calculating:

```csharp
IsFullyOpaque = true;
IsFullyNonOpaque = true;
for (var index = 0; index < _alpha8.Length; index++)
{
    var alpha = alpha8BottomToTop[index];
    _alpha8[index] = alpha;
    if (alpha != byte.MaxValue) IsFullyOpaque = false;
    if (alpha == byte.MaxValue) IsFullyNonOpaque = false;
}
```

After geometry and UV validation, add:

```csharp
if (texture.IsFullyOpaque)
    return TriangleAlphaOutcome.ProvenOpaque;
if (texture.IsFullyNonOpaque)
    return TriangleAlphaOutcome.MustRemainTransparent;
```

- [ ] **Step 4: Run focused and complete current tests green**

Run the uniform tests, uncertainty tests, and all fixture integrity tests. Expected: all pass; alpha `254` is `MustRemainTransparent`.

- [ ] **Step 5: Inspect Console and diff**

Confirm no new compiler errors/warnings, then run `git diff --check` and inspect scope.

---

### Task 3: Exact UV domain and Point + Clamp

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`
- Modify: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/TriangleAlphaClassifier.cs`
- Modify: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/ExactUvGeometry.cs`

**Interfaces:** Produces exact scaled UV hulls, a minimal rational coordinate used only for clipping, finite/infinite intervals with side inclusivity, exact domain/box intersection, Point candidate bounds, and Clamp preimages.

- [ ] **Step 1: Add Point + Clamp fixture tests**

```csharp
[TestCase("mixed-alpha-texture")]
[TestCase("triangle-in-opaque-region")]
[TestCase("triangle-in-transparent-region")]
[TestCase("triangle-crosses-alpha-boundary")]
[TestCase("mixed-triangle-mesh")]
[TestCase("outside-uv-clamp")]
public void PointClampCasesMatchOracle(string caseId)
{
    AssertCaseMatchesOracle(caseId);
}
```

Add direct collapsed-domain checks: an opaque Point/Clamp UV point returns `ProvenOpaque`; a UV line entering a non-opaque cell returns `MustRemainTransparent`; a point exactly on a Point upper boundary belongs only to the next cell.

- [ ] **Step 2: Run executable assertion-red**

Expected: the project compiles and mixed Point/Clamp assertions fail because those cases still return `Unknown`.

- [ ] **Step 3: Implement the exact lattice**

Decode a float as `significand * 2^exponent`, choose a common exponent `<= -1`, multiply U significands by width and V significands by height, and shift to one `BigInteger` lattice. Set:

```text
TexelScale = 2^(-commonExponent)
HalfTexel = TexelScale / 2
```

Build the convex hull of the three UV points. Preserve triangle, extreme segment endpoints, or one point. Keep initial coordinates on the integer lattice.

- [ ] **Step 4: Implement exact open/closed box intersection**

Clipping an edge against an integer support boundary can create a non-dyadic rational vertex. Add a private `ExactRational` represented by a `BigInteger` numerator and positive denominator; implement exact comparison by cross multiplication and exact segment/boundary intersection. Canonicalize zero and denominator sign, but do not add a general symbolic algebra layer.

Clip the domain against the closed closure of four finite interval bounds using those exact rational vertices. If empty, return false. For each open side, require at least one clipped vertex strictly inside that side; otherwise return false. If every open side has a strict witness, return true by convexity.

Unit-test the helper indirectly with the collapsed point/line and exact-boundary classifier tests; do not expose it as public API.

- [ ] **Step 5: Implement Point + Clamp support**

Use per-axis preimages:

```text
size == 1:          (-infinity, +infinity)
index == 0:         (-infinity, 1 texel)
0 < index < last:   [index, index + 1 texels)
index == last:      [last, +infinity)
```

Restrict candidate x/y indices using the monotonic clamped domain bounding box. Compute candidate count with checked/`BigInteger` arithmetic; return `Unknown` if it exceeds `65536`. For each candidate with alpha below `255`, call exact intersection. Return `MustRemainTransparent` on the first witness and `ProvenOpaque` only after the complete bounded set has no witness.

- [ ] **Step 6: Run Point + Clamp green**

Run all tests in `TriangleAlphaClassifierTests` plus fixture integrity tests. Expected: every current case passes, including both triangles in `mixed-triangle-mesh` independently.

- [ ] **Step 7: Review exactness and scope**

Search the production files for `epsilon`, sampling grids, `Texture2D`, `Mesh`, fixture namespaces, and expectation strings. Expected: none. Run `git diff --check`.

---

### Task 4: Point + Repeat and bounded work

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`
- Modify: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/TriangleAlphaClassifier.cs`
- Modify: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/ExactUvGeometry.cs`

**Interfaces:** Produces mathematical floor division/modulus, common-period normalization, unwrapped Point cells, and budget refusal.

- [ ] **Step 1: Add Repeat fixture and edge/property tests**

```csharp
[Test]
public void PointRepeatFixtureMatchesOracle()
{
    AssertCaseMatchesOracle("outside-uv-repeat");
}
```

Add direct tests for: negative U wrapping into an opaque texel; U exactly at an integer seam selecting period cell zero; translating all UVs by `(17, -23)` preserving the outcome; a long thin domain crossing a non-opaque cell; several periods under the budget; and a mixed-texture span over the budget returning `Unknown`.

The Task 1 shells keep these tests compilable while retaining C#'s incorrect truncating behavior. Directly exercise a four-cell period at the negative exact multiple and both conceptual sides:

```csharp
[TestCase(-4, -1, 0)] // Exactly -period.
[TestCase(-5, -2, 3)] // One cell below -period.
[TestCase(-3, -1, 1)] // One cell above -period.
[TestCase(-1, -1, 3)] // Last negative cell before zero.
public void RepeatFloorArithmeticUsesMathematicalFloor(
    int value,
    int expectedQuotient,
    int expectedRemainder)
{
    Assert.That(
        ExactUvGeometry.FloorDiv(value, 4),
        Is.EqualTo(new BigInteger(expectedQuotient)));
    Assert.That(
        ExactUvGeometry.FloorMod(value, 4),
        Is.EqualTo(expectedRemainder));
}
```

- [ ] **Step 2: Run executable assertion-red**

Expected: the project compiles; Repeat outcome assertions fail, and naive truncating division specifically fails for `-5`, `-3`, and `-1` while the `-4` exact-multiple control passes.

- [ ] **Step 3: Implement floor arithmetic**

Implement and directly exercise through classifier behavior:

```csharp
q = BigInteger.DivRem(value, divisor, out remainder);
if (remainder.Sign < 0) q -= BigInteger.One;

floorMod = value % modulus;
if (floorMod.Sign < 0) floorMod += modulus;
```

Require positive divisors/moduli.

- [ ] **Step 4: Normalize and enumerate bounded unwrapped cells**

For each axis, subtract `FloorDiv(min, texturePeriod) * texturePeriod` from all domain vertices. Enumerate integer Point cells from `floor(min / TexelScale)` through `floor(max / TexelScale)` and map indices with `FloorMod`.

Calculate `xCount * yCount` as `BigInteger`. If above `MaxSupportRegions`, return `Unknown` before converting bounds to loop integers. Otherwise use the exact half-open support box from Task 3.

- [ ] **Step 5: Run Repeat green and all twelve fixture cases covered so far**

Run all classifier and integrity tests. Expected: `outside-uv-clamp` remains transparent while the same geometry under Repeat is proven opaque; property tests pass.

- [ ] **Step 6: Inspect performance guards**

Confirm there is no loop whose bound derives from a `BigInteger` span before the budget comparison. Confirm huge common offsets normalize and do not overflow a primitive integer conversion.

---

### Task 5: Bilinear + Clamp positive-weight support

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`
- Modify: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/TriangleAlphaClassifier.cs`
- Modify: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/ExactUvGeometry.cs`

**Interfaces:** Reuses exact domain intersection with open bilinear support and adds clamped edge tails.

- [ ] **Step 1: Add Bilinear + Clamp tests**

```csharp
[Test]
public void BilinearBoundaryFixtureMatchesOracle()
{
    AssertCaseMatchesOracle("bilinear-filter-boundary");
}
```

Add direct tests for: an opaque-side triangle whose support does not reach transparency; a UV point exactly one texel-center distance from a non-opaque texel (zero weight, so no witness); the same point moved to an explicitly chosen dyadic coordinate inside positive support; Clamp at normalized `0` and `1`; collapsed UV line/point; and textures one texel wide or high.

- [ ] **Step 2: Run executable assertion-red**

Expected: the project compiles; the approved boundary assertion fails because the result is not yet `MustRemainTransparent`, and the zero/positive support distinction is absent.

- [ ] **Step 3: Implement Bilinear + Clamp regions**

For stored texel index `i`, use exact positive-weight intervals:

```text
size == 1:          (-infinity, +infinity)
index == 0:         (-infinity, i + 1.5 texels)
0 < index < last:   (i - 0.5, i + 1.5 texels)
index == last:      (i - 0.5, +infinity)
```

Represent half-texel bounds as lattice integers. Expand candidate bounds by one texel, cap before enumeration, skip alpha `255`, and use exact open-side intersection. A positive-weight non-opaque witness returns `MustRemainTransparent`; no witness returns `ProvenOpaque`.

- [ ] **Step 4: Run Bilinear + Clamp green**

Run all classifier and integrity tests. Expected: all fourteen triangle outcomes in the thirteen approved fixtures match the oracle.

- [ ] **Step 5: Check Console and numeric code**

Confirm zero C# compiler errors and no new warnings/error-level messages. The two baseline empty-asmdef entries may remain as stale Console history because this workflow does not clear the Console; new instances should stop once production scripts exist. Search for floating comparison tolerances and verify support endpoints remain open.

---

### Task 6: Bilinear + Repeat seams

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`
- Modify: `Packages/com.alrauna.alpha-material-optimizer/Editor/Analysis/TriangleAlphaClassifier.cs`

**Interfaces:** Reuses common-period normalization and maps each unwrapped bilinear texel index with floor modulus.

- [ ] **Step 1: Add direct Bilinear + Repeat tests**

Use a two-texel alpha row `[255, 0]` and assert:

```csharp
[Test]
public void BilinearRepeatSupportCrossesTheSeam()
{
    var texture = new AlphaTextureData(2, 1, new byte[] { 255, 0 });
    var uv = new Vector2(0f, 0.5f);
    var triangle = TriangleAlphaInput.WithUv0(
        Vector3.zero, Vector3.right, Vector3.up,
        uv, uv, uv);
    var result = TriangleAlphaClassifier.Classify(
        triangle,
        texture,
        new AlphaSamplingSettings(AlphaFilterMode.Bilinear, AlphaWrapMode.Repeat));
    Assert.That(result, Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
}
```

Also assert negative seam coordinates, integer-period translation invariance, a one-texel texture remaining opaque, and an over-budget mixed span returning `Unknown`.

- [ ] **Step 2: Run executable assertion-red**

Expected: the project compiles and Bilinear + Repeat assertions fail because the behavior is absent or uses Clamp edge tails.

- [ ] **Step 3: Implement unwrapped open supports**

For each unwrapped texel index `i`, use `(i - 0.5, i + 1.5)` in each axis and map stored alpha through `FloorMod(i, size)`. Reuse Repeat normalization, exact open intersection, and the same pre-loop budget check. Do not add a second geometry engine.

- [ ] **Step 4: Run green**

Run all classifier and fixture integrity tests. Expected: Bilinear Repeat properties pass and all approved fixtures remain unchanged.

- [ ] **Step 5: Refactor only observed duplication**

If Point/Bilinear or Clamp/Repeat paths now duplicate candidate-loop mechanics, extract one private iterator/helper only when both existing callers become shorter and semantics remain explicit. Do not add interfaces, factories, strategy classes, or future filter modes.

---

### Task 7: Adversarial coverage and complete verification

**Files:**

- Modify: `Packages/com.alrauna.alpha-material-optimizer/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`
- Modify only if a new test fails for a real defect: the two production analysis files

**Interfaces:** No new production API.

- [ ] **Step 1: Add focused adversarial/metamorphic cases**

Add tests that construct inputs directly and assert:

- reversing positions and matching UV winding preserves the result;
- a tiny triangle whose vertices select opaque Point cells but whose edge/interior enters a non-opaque checker cell is not proven opaque;
- a long thin triangle crossing many cells finds a non-opaque witness;
- UV line and point collapses classify without geometry becoming `Unknown`;
- very large positive/negative Repeat offsets give the same result after normalization;
- a large mixed span returns `Unknown`, while all-opaque and all-nonopaque textures still take exact fast paths;
- replacing present UV0 with intentional missing UV0 never promotes a result to `ProvenOpaque`;
- invalid texture dimensions/length, undefined enum values, and non-finite present UVs throw;
- alpha `254` remains non-opaque in direct Point and Bilinear inputs.

Use ordinary NUnit test cases; add no dependency or generated property framework.

- [ ] **Step 2: Run each new test and correct only demonstrated defects**

For any failure, inspect the exact support/domain math before editing. Keep the red output, make the smallest correction, and rerun the failing test green.

- [ ] **Step 3: Run the full classifier fixture join**

Run every test in `TriangleAlphaClassifierTests`. Expected: every approved fixture case and direct property test passes; the fixture adapter obtains expected strings only after actual results are computed.

- [ ] **Step 4: Run the complete EditMode suite**

Use Unity MCP `run_tests(mode: EditMode, include_failed_tests: true)` and poll with `get_test_job(wait_timeout: 60, include_failed_tests: true)`. Record observed total, passed, failed, skipped, and duration. Expected: zero failures.

- [ ] **Step 5: Read the final Unity baseline**

Read `mcpforunity://project/info`, `mcpforunity://tests`, and Console errors/warnings. Confirm the exact public root, Unity/package version, classifier test discovery, zero compiler errors, and zero unexpected warnings/errors. Do not access the private testbed.

- [ ] **Step 6: Verify documentation and repository scope**

Run:

```powershell
git diff --check
git status --short
git diff --stat
git diff
git diff --cached --stat
git diff --cached
```

Confirm only the approved spec/plan, production analysis files, tests, assembly visibility attribute, and matching `.meta` files changed. Confirm fixture JSON, expected outcomes, asmdefs, manifests, dependencies, workflows, and private content did not change.

For any still-untracked intended file, also run `git diff --no-index --check -- NUL <path>` on Windows. Exit `1` is expected because the file differs from `NUL`; any whitespace-error diagnostic is not expected.

- [ ] **Step 7: Review requirements line by line**

Check the implementation against every section of `docs/superpowers/specs/2026-08-15-geometry-classifier-design.md`. Explicitly record exact behaviors, the sole conservative workload-cap behavior, unsupported/deferred work, validation skipped, and whether MCP or the private testbed was modified.

- [ ] **Step 8: Commit only if separately authorized**

If and only if the user has authorized commits, stage only the reviewed files and commit coherent task groups with focused messages such as:

```text
test: specify triangle alpha classification
feat: classify continuous triangle alpha support
```

Otherwise leave all changes unstaged and report commit status.

## Approval and execution gate

This plan is documentation only. Stop here until the user approves both the design and implementation plan. After approval, use inline `superpowers:executing-plans` by default, plus `superpowers:test-driven-development`, `ponytail:ponytail`, and `superpowers:verification-before-completion`. Do not dispatch subagents, commit, push, or open a PR without separate authorization.
