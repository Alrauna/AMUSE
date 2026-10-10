# Analysis Proof Walk Soundness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the unsound bilinear clamp pre-filter, the false transparency lemma in the disjunction fold, NaN multiplier arms, the identity predicate and helper duplication, the density gate documentation drift, and the undocumented load-bearing exponent seed in the Analysis module.

**Architecture:** Pure, headless mathematics in `Editor/Analysis/`, one predicate member on a Semantics value type. All fixes preserve or restore fail-closed classification. No runtime or build output changes.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode).

**Spec:** `docs/superpowers/specs/2026-10-09-analysis-proof-walk-soundness-design.md`

## Global Constraints

- Use simple English. Short active sentences. One idea per sentence.
- No semicolons in prose.
- No contractions.
- Never record absolute or machine-specific paths. Use repository-relative paths only.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names.
- Date every status claim. As of 2026-10-09.
- Never stage, commit, push, or modify Git history without explicit user authorization.
- The dev editor instance with `Application.dataPath == <repo-root>/Assets` is the only authorized test instance.
- A plan whose filtered run reports 0 tests is a failure. Every verification step names its tests and requires them in the filtered run output.
- Vendor shaders never enter tests. The Analysis tests are pure and headless.

---

### Task 1: Finding 4, Per-Candidate Pre-Filter Translation with a Proven Magnitude Bound

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`

**Interfaces:**
- Consumes: `ClassifyBilinearClamp`, `HasMappedWitnessBilinearClamp`, `ExtractTexelVertices`, `ConservativeBilinearSupportOverlapsTriangle`, `ExactUvGeometry.Intersects`
- Produces: A pre-filter that never reports separation above a proven coordinate bound, with the exact intersection test as the sole arbiter

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`, add two tests. The texture is an 8 by 8 clamp texture whose bytes are all 255 except index 27 (texel (3, 3)), which is 0. The triangle diagonal in texel units is the line y = x, which passes exactly through the support box corner (4.5, 4.5) of texel (3, 3).

```csharp
private static TriangleAlphaInput DiagonalTriangle(
    float u0x, float u0y, float u1x, float u1y, float u2x, float u2y)
{
    return TriangleAlphaInput.WithUv0(
        Vector3.zero, Vector3.right, Vector3.up,
        new Vector2(u0x, u0y), new Vector2(u1x, u1y), new Vector2(u2x, u2y));
}

private static AlphaTextureData EightByEightOneWitnessTexture()
{
    var bytes = new byte[64];
    for (var index = 0; index < bytes.Length; index++)
    {
        bytes[index] = 255;
    }
    bytes[27] = 0;
    return new AlphaTextureData(8, 8, bytes);
}

[Test]
public void BilinearClampLargeCoordinateRunMatchesSmallRunVerdict()
{
    var texture = EightByEightOneWitnessTexture();
    var sampling = new TextureSampling(
        TextureFilterMode.Bilinear, TextureWrapMode.Clamp);

    // Small control: same shape near the origin, vertices in texel
    // units (-8, -8), (10, 10), (-8, 10), so UV units divide by 8.
    var small = TriangleAlphaClassifier.Classify(
        DiagonalTriangle(-1f, -1f, 1.25f, 1.25f, -1f, 1.25f),
        texture, sampling, AlphaUvEnvelope.Zero);

    // Large run: the same shape near plus and minus 16 million texel
    // units, so UV units are the texel units divided by 8.
    var large = TriangleAlphaClassifier.Classify(
        DiagonalTriangle(
            -2000000f, -2000000f,
            2000000.25f, 2000000.25f,
            -2000000f, 2000000.25f),
        texture, sampling, AlphaUvEnvelope.Zero);

    Assert.That(small, Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
    Assert.That(large, Is.EqualTo(small));
}

[Test]
public void BilinearClampSixteenTexelShiftedRunMatchesSmallRunVerdict()
{
    var texture = EightByEightOneWitnessTexture();
    var sampling = new TextureSampling(
        TextureFilterMode.Bilinear, TextureWrapMode.Clamp);
    var shifted = TriangleAlphaClassifier.Classify(
        DiagonalTriangle(
            -1999998f, -1999998f,
            2000002.25f, 2000002.25f,
            -1999998f, 2000002.25f),
        texture, sampling, AlphaUvEnvelope.Zero);

    Assert.That(
        shifted, Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
}
```

All six UV values are exact binary32 values. The granularity at 2000000 is 0.125, so every vertex encodes exactly.

- [ ] **Step 2: Run tests to verify they fail**

Run the two new tests via the Unity Test Runner, EditMode, filtered on `BilinearClampLargeCoordinate` and `BilinearClampSixteenTexelShifted`. The filtered run must report 2 tests, not 0. The small control passes. At least one large run returns ProvenOpaque today, which fails the same-verdict assertion. If the first large run does not reproduce, rerun with the witness byte moved to index 18 (texel (2, 2)) or index 36 (texel (4, 4)), which places the exact-touch corner on the same diagonal. Record the observed failing index in the test comment.

- [ ] **Step 3: Implement the per-candidate translation and magnitude bound**

In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`:
- Add the constant `private const double PreFilterSafeCoordinateBound = 256;` with the spec's comment about the error stack.
- Add `private static bool TryExtractTexelVerticesRelativeToCandidate(ExactUvDomain domain, int x, int y, out double v0x, out double v0y, out double v1x, out double v1y, out double v2x, out double v2y)`. The helper subtracts the exact rational box center `(x + 0.5, y + 0.5)` from each exact vertex coordinate, converts the translated values to texel-unit doubles, and returns false when any magnitude exceeds `PreFilterSafeCoordinateBound`.
- In `ClassifyBilinearClamp` and `HasMappedWitnessBilinearClamp`, keep `ExtractTexelVertices` as the once-per-walk source for the `isBoundary` bypass. Replace the pre-filter body: per candidate, call the new helper. When it returns false, skip the pre-filter and fall through to the exact `ExactUvGeometry.Intersects` check. When it returns true, call `ConservativeBilinearSupportOverlapsTriangle(-1, 1, -1, 1, v0x, v0y, v1x, v1y, v2x, v2y)`.
- Keep `canPreFilter`, the budget guard, the window bounds, and the exact intersection call unchanged.

- [ ] **Step 4: Run tests to verify they pass**

Run the two new tests plus the existing pre-filter tests `PreFilterRejectsSeparatedTexelSupportBox`, `PreFilterRetainsOverlappingTexelSupportBox`, `PreFilterNeverRejectsBoundaryTouchingTexel`, and the existing bilinear clamp tests via the Unity Test Runner, EditMode. The filtered run must report all named tests. All pass.

---

### Task 2: Finding 7, Retract the Absorbing Transparency Claims to Unknown

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`

**Interfaces:**
- Consumes: `AlphaResolution.Or`, `AlphaResolution.Classify`, `ResolveSaturatingSum`
- Produces: A disjunction fold whose absorbing arm is Unknown, with the sound proven-opaque arms kept

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`, add the mandated falsifier and the uniform shortcut test. The two fields are complementary: field A is 0 at texel (1, 1) and field B is 0 at texel (0, 0), so the saturate of the two fields is identically one.

```csharp
[Test]
public void ComplementaryMasksSummingToIdenticallyOneAreNotProvenTransparent()
{
    var fieldA = new AlphaTextureData(2, 2, new byte[] { 255, 255, 255, 0 });
    var fieldB = new AlphaTextureData(2, 2, new byte[] { 0, 255, 255, 255 });
    var value = ScalarSemanticValue.SaturatingSum(
        ScalarSemanticValue.Texture(Sample(), TextureChannel.Alpha),
        ScalarSemanticValue.Texture(MaskSample(), TextureChannel.Red));
    var resolution = AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(value),
        ProvidingTwo(Chain(fieldA), Chain(fieldB)), 0);

    Assert.That(
        resolution.Classify(SpanningTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.Unknown));
}

[Test]
public void SaturatingSumOfTransparentScaledSampleAndRefutingFieldIsUnknown()
{
    var value = ScalarSemanticValue.SaturatingSum(
        ScalarSemanticValue.TextureTimesConstant(
            Sample(), TextureChannel.Alpha, 0.5f),
        ScalarSemanticValue.Texture(MaskSample(), TextureChannel.Red));
    var resolution = AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(value),
        ProvidingTwo(
            Chain(Field(1, 1, 255)), OpaqueThenTransparentChain()), 0);

    Assert.That(
        resolution.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.Unknown));
}
```

Update the pinned test `SaturatingSumOfTwoMixedFieldsIsUnknownWhereNeitherProves` at lines 1672 to 1690. Change the assertion from `Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent)` to `Is.EqualTo(TriangleAlphaOutcome.Unknown)`. Update the comment to state that the absorbing arm now returns Unknown. The shared ruling mandates this expectation update. The test name already states Unknown. The old expectation pinned a false proof claim, so this is not a test weakening.

- [ ] **Step 2: Run tests to verify they fail**

Run the two new tests and the updated pinned test via the Unity Test Runner, EditMode, filtered on `ComplementaryMasks` and `SaturatingSum`. The filtered run must report at least 3 tests, not 0. All three fail today: the fold returns MustRemainTransparent in every case.

- [ ] **Step 3: Implement the retraction**

In `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`:
- Rewrite the `Or` summary (lines 236 to 249) with the true rule from the spec: the sum reaches one whenever the factors add to at least one, complementary masks with sub-one witnesses at different texels sum to identically one, the opaque arm is sound, and the absorbing arm returns Unknown without an additive classifier.
- In the `Classify` disjunction arm (lines 365 to 390), delete the both-transparent absorbing return and fall through to `return TriangleAlphaOutcome.Unknown;`.
- Correct the `ResolveSaturatingSum` summary (lines 694 to 712) with the same true rule.
- Replace the two uniform shortcut returns (lines 744 to 762) with one branch: when either factor's uniform outcome is MustRemainTransparent, return `AlphaResolution.Uniform(TriangleAlphaOutcome.Unknown)`.
- Keep the two `IsUniformlyProvenOpaque` early returns unchanged.

- [ ] **Step 4: Run tests to verify they pass**

Run the new tests, the updated pinned test, and the existing saturating sum tests, including the opaque-arm disjunction test at lines 1651 to 1670, via the Unity Test Runner, EditMode. The filtered run must report all named tests. All pass.

---

### Task 3: Finding 50, Refuse NaN in Every Resolver Arm

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`

**Interfaces:**
- Consumes: `ResolveScaledSample`, `ResolveProductChain`, `ResolveScalar`, `AlphaResolution.Refused`, `AlphaResolutionFailure.UnsupportedMultiplier`
- Produces: One identical NaN discipline in all three multiplier arms, failing closed

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`, add a reflection helper and three tests. The public factories validate finiteness, so the private eleven-parameter constructor at `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs:878` is the only route to a NaN multiplier or a NaN constant. The helper mirrors the body of the existing `ResolveMultiplied` helper at line 739. The constant kind carries its NaN in the constant value parameter. The scaled and chain kinds carry the NaN in the multiplier parameter.

```csharp
private static ScalarSemanticValue ValueWithNaN(
    ScalarSemanticValueKind kind)
{
    var constructor = typeof(ScalarSemanticValue).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance, null,
        new[]
        {
            typeof(ScalarSemanticValueKind), typeof(float),
            typeof(TextureSample), typeof(TextureChannel), typeof(float),
            typeof(TextureSample[]), typeof(TextureChannel[]),
            typeof(ScalarSemanticValue), typeof(ScalarSemanticValue),
            typeof(AffineAlphaMap?[]), typeof(AffineAlphaMap)
        }, null);
    var isChain =
        kind == ScalarSemanticValueKind.ProductChainOfTextureSamples;
    var isConstant = kind == ScalarSemanticValueKind.Constant;
    return (ScalarSemanticValue)constructor.Invoke(new object[]
    {
        kind,
        isConstant ? float.NaN : default(float),
        Sample(),
        TextureChannel.Alpha,
        isConstant ? default(float) : float.NaN,
        isChain ? new[] { Sample() } : null,
        isChain ? new[] { TextureChannel.Alpha } : null,
        null, null, null, default(AffineAlphaMap)
    });
}

[Test]
public void ScaledSampleArmRefusesNaNMultiplier()
{
    var resolution = AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(
            ValueWithNaN(
                ScalarSemanticValueKind.TextureSampleTimesConstant)),
        Providing(AllOpaqueChain()), 0);

    Assert.That(resolution.IsResolved, Is.False);
    Assert.That(
        resolution.Failure,
        Is.EqualTo(AlphaResolutionFailure.UnsupportedMultiplier));
}

[Test]
public void ProductChainArmRefusesNaNMultiplier()
{
    var resolution = AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(
            ValueWithNaN(
                ScalarSemanticValueKind.ProductChainOfTextureSamples)),
        Providing(AllOpaqueChain()), 0);

    Assert.That(resolution.IsResolved, Is.False);
    Assert.That(
        resolution.Failure,
        Is.EqualTo(AlphaResolutionFailure.UnsupportedMultiplier));
}

[Test]
public void ConstantArmRefusesNaNMultiplier()
{
    var resolution = AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(
            ValueWithNaN(ScalarSemanticValueKind.Constant)),
        Providing(AllOpaqueChain()), 0);

    Assert.That(resolution.IsResolved, Is.False);
    Assert.That(
        resolution.Failure,
        Is.EqualTo(AlphaResolutionFailure.UnsupportedMultiplier));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the three tests via the Unity Test Runner, EditMode, filtered on `RefusesNaNMultiplier`. The filtered run must report 3 tests, not 0. The scaled-sample arm test fails today: the arm returns a uniform MustRemainTransparent. The product chain arm test fails today: the arm resolves Uniform ProvenOpaque through its one opaque factor. The constant arm test passes today and is characterization. Its purpose is to keep the constant arm's refusal pinned while the other two arms change.

- [ ] **Step 3: Implement the NaN refusals**

In `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`:
- In `ResolveScaledSample` (lines 543 to 573), add as the first check: `if (float.IsNaN(multiplier)) return AlphaResolution.Refused(AlphaResolutionFailure.UnsupportedMultiplier);`.
- In `ResolveProductChain` (lines 640 to 654), add the same first check after `GetProductMultiplier()`.
- In `ResolveScalar` (lines 883 to 902), add the same first check on `scalar` before the equality branches.
- Update the multiplier lemma summary above `ResolveScalar` to state that a NaN multiplier refuses as unsupported in every arm.

- [ ] **Step 4: Run tests to verify they pass**

Run the three tests and the existing multiplier resolution tests via the Unity Test Runner, EditMode. The filtered run must report the three named tests plus the existing multiplier tests. All pass.

---

### Task 4: Finding 49, Correct the Four Density Gate Summaries

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs` (no change, run only)

**Interfaces:**
- Consumes: The four erasure gate summaries at lines 677 to 687, 941 to 951, 1186 to 1196, and 1536 to 1546
- Produces: Summaries that describe the at-or-under rule the code and the pinned test implement

- [ ] **Step 1: Confirm the pinned rule before the edit**

Run the pinned density tests `EqualityAtTheCoverageBoundMoves`, `DenseErasedNoiseRefusesUnderTheGate`, and `ZeroDensityKeepsErasureInert` via the Unity Test Runner, EditMode. The filtered run must report the named tests. All pass before any edit. The pinned test `EqualityAtTheCoverageBoundMoves` already bakes the correct at-or-under rule. This task is a documentation fix with no behavior change, so there is no RED step by design.

- [ ] **Step 2: Correct the four summaries**

In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`, replace one sentence in each of the four summaries. Replace:

> Erasure substitutes only when the erased share of the consulted candidates stays strictly under the policy. Equality refuses.

With:

> Erasure substitutes when the erased share of the consulted candidates stays at or under the policy. Equality moves.

The four summaries are `PointClampSubstitutesErasedTexels`, `BilinearRepeatSubstitutesErasedTexels`, `BilinearClampSubstitutesErasedTexels`, and `PointRepeatSubstitutesErasedTexels`. The code at lines 724, 988, 1225, and 1580 stays unchanged.

- [ ] **Step 3: Verify wording and behavior**

Search `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` for `strictly under`. The search must return 0 hits. Run the three pinned density tests again via the Unity Test Runner, EditMode. All pass.

---

### Task 5: Finding 51, Document the Load-Bearing Exponent Seed and Pin It

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/ExactUvGeometryTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`

**Interfaces:**
- Consumes: `CreateTextureScaledDomain`, the seed at `ExactUvGeometry.cs:455`, the half-texel consumers `BilinearRepeatInterval`, `BilinearClampInterval`, `PointRepeatInterval`, `PointClampInterval`
- Produces: A documented seed and two characterization pins on integer UV coordinates

- [ ] **Step 1: Write the pinning tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/ExactUvGeometryTests.cs`:

```csharp
[Test]
public void IntegerUvVerticesKeepTexelScaleAtTwo()
{
    var triangle = TriangleAlphaInput.WithUv0(
        Vector3.zero, Vector3.right, Vector3.up,
        new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(0f, 2f));

    var domain = ExactUvGeometry.CreateTextureScaledDomain(
        triangle, 4, 4, AlphaUvEnvelope.Zero);

    Assert.That(domain.TexelScale, Is.EqualTo(BigInteger.One << 1));
}
```

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`:

```csharp
[Test]
public void BilinearClampIntegerUvTriangleStillFindsSubOpaqueTexel()
{
    var triangle = TriangleAlphaInput.WithUv0(
        Vector3.zero, Vector3.right, Vector3.up,
        new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f));
    var texture = new AlphaTextureData(
        2, 2, new byte[] { 255, 255, 255, 0 });

    Assert.That(
        TriangleAlphaClassifier.Classify(
            triangle, texture,
            new TextureSampling(
                TextureFilterMode.Bilinear, TextureWrapMode.Clamp),
            AlphaUvEnvelope.Zero),
        Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
}
```

- [ ] **Step 2: Run the pins and record their characterization status**

Run both tests via the Unity Test Runner, EditMode, filtered on `TexelScaleAtTwo` and `IntegerUvTriangle`. The filtered run must report 2 tests, not 0. Both pass on the first run. This is expected and intended. The defect is the missing comment and the missing pin, not the behavior. A failure here means the seed analysis is wrong, and work on this task stops until the mechanism is understood.

- [ ] **Step 3: Document the seed**

In `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`, add the spec's comment above `var exponent = -1;` at line 455. The comment names the truncating BigInteger division, the half-texel consumers in the classifier's interval builders, and the false ProvenOpaque collapse a seed of 0 would cause. No code line changes.

- [ ] **Step 4: Run tests to verify they pass**

Run both pins again via the Unity Test Runner, EditMode. Both pass.

---

### Task 6: Finding 48, One Identity Predicate and One Helper Set

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AffineUvTransform.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/AffineAlphaMap.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AffineUvTransformTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/ExactUvGeometryTests.cs`

**Interfaces:**
- Consumes: `UvMapping`, `ExactUvGeometry.BitLength`, `ExactUvGeometry.ToRational(ExactDyadic)`, `AffineAlphaMap`
- Produces: One identity predicate on the `UvMapping` type and one shared helper set on `ExactUvGeometry`, all consumers routed through them

- [ ] **Step 1: Write the equivalence tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AffineUvTransformTests.cs`:

```csharp
[TestCase(1f, 1f, 0f, 0f, 0, true)]
[TestCase(2f, 1f, 0f, 0f, 0, false)]
[TestCase(1f, 0.5f, 0f, 0f, 0, false)]
[TestCase(1f, 1f, 0.5f, 0f, 0, false)]
[TestCase(1f, 1f, 0f, -0.5f, 0, false)]
[TestCase(1f, 1f, 0f, 0f, 2, true)]
public void UvMappingIdentityMatchesPerComponentComparison(
    float scaleX, float scaleY, float offsetX, float offsetY,
    int channel, bool expected)
{
    var mapping = new UvMapping(
        channel, new Vector2(scaleX, scaleY),
        new Vector2(offsetX, offsetY));

    Assert.That(mapping.IsIdentity, Is.EqualTo(expected));
    Assert.That(
        mapping.IsIdentity,
        Is.EqualTo(
            mapping.Scale.x == 1f && mapping.Scale.y == 1f &&
            mapping.Offset.x == 0f && mapping.Offset.y == 0f));
}
```

In `Packages/com.alrauna.amuse/Tests/Editor/Analysis/ExactUvGeometryTests.cs`:

```csharp
[Test]
public void SharedBitLengthMatchesReferenceAcrossMagnitudes()
{
    for (var exponent = 1; exponent <= 256; exponent++)
    {
        var power = BigInteger.One << exponent;
        Assert.That(
            ExactUvGeometry.BitLength(power - BigInteger.One),
            Is.EqualTo(exponent));
        Assert.That(
            ExactUvGeometry.BitLength(power),
            Is.EqualTo(exponent + 1));
        Assert.That(
            ExactUvGeometry.BitLength(power + BigInteger.One),
            Is.EqualTo(exponent + 1));
    }
}

[TestCase(0, -140)]
[TestCase(1, -20)]
[TestCase(-3, -20)]
[TestCase(7, 0)]
[TestCase(12345, 20)]
public void SharedDyadicToRationalMatchesDirectConstruction(
    int significand, int exponent)
{
    var dyadic = new ExactDyadic(significand, exponent);
    var shared = ExactUvGeometry.ToRational(dyadic);
    var direct = exponent >= 0
        ? new ExactRational((BigInteger)significand << exponent)
        : new ExactRational(
            (BigInteger)significand, BigInteger.One << -exponent);

    Assert.That(
        shared.Numerator, Is.EqualTo(direct.Numerator));
    Assert.That(
        shared.Denominator, Is.EqualTo(direct.Denominator));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the three new tests via the Unity Test Runner, EditMode. The run fails to compile: `UvMapping.IsIdentity`, the internal `ExactUvGeometry.BitLength`, and the internal `ExactUvGeometry.ToRational` do not exist yet. The compile failure is the RED state. The filtered run reports 0 executed tests, which is the expected shape for a compile-level RED, and Step 4 requires the named tests in the passing run.

- [ ] **Step 3: Implement the shared predicate and shared helpers**

In `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`:
- Add the `IsIdentity` member to `UvMapping` next to `Equals`, with the spec's summary sentence.

In `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`:
- Replace the inline four-line comparison at lines 404 to 409 with `if (!_mapping.IsIdentity)`.

In `Packages/com.alrauna.amuse/Editor/Analysis/AffineUvTransform.cs`:
- Delete the private `IsIdentity` at lines 105 to 111 and call `mapping.IsIdentity`.
- Delete the private `ToRational` at lines 256 to 262 and the private `BitLength` at lines 263 to 270. Call the `ExactUvGeometry` members.

In `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`:
- Change `ToRational(ExactDyadic)` at lines 380 to 386 and `BitLength(BigInteger)` at lines 407 to 414 from `private` to `internal`. Keep the hand-rolled comment on `BitLength`.

In `Packages/com.alrauna.amuse/Editor/Semantics/AffineAlphaMap.cs`:
- Delete the private byte-array `BitLength` at lines 143 to 168 and call `ExactUvGeometry.BitLength`. This is the finding's third consumer. The sweep test proves the two algorithms agree.

- [ ] **Step 4: Run tests to verify they pass**

Run the three new tests, the existing `AffineUvTransformTests` class, and the mapped-sample resolution tests in `AlphaSemanticsResolverTests` via the Unity Test Runner, EditMode. The filtered run must report the named tests. All pass.
