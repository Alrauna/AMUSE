# Analysis Proof Walk Soundness: Design and Specification

Date: 2026-10-09.
Branch: fix/latent-bugs-and-impurities. Base: fae7e7e.
No source assets are modified.
All changes are Editor-time analysis code and EditMode tests.

Privacy note: this document contains no private avatar, renderer, material, scene, or machine identity. Public vendor shader identifiers stay exact.

Labels: `[SOURCE]` is a fact read from cited code lines in this repository.
`[INFERENCE]` is a conclusion from evidence.

---

## 1. Summary

This design fixes two high-priority proof soundness defects and four smaller defects in the Analysis module.

Finding 4: the bilinear clamp pre-filter skips exact intersection checks on double margins that large UV coordinates overflow. The fix translates the pre-filter inputs per candidate so the coordinates stay small, and it routes any candidate past a proven magnitude bound straight to the exact intersection test. The exact test stays the sole arbiter.

Finding 7: the disjunction fold and the saturating sum prove transparency from a false lemma. The fix retracts every absorbing transparency claim to Unknown. The proven-opaque arms stay. The pinned test that bakes the false proof gets a mandated expectation update. This update is a shared ruling, not a test weakening.

Finding 48: two logically equivalent identity predicates and duplicated bit length and rational conversion helpers invite silent drift. The fix keeps one predicate on the `UvMapping` type and one helper set on `ExactUvGeometry`.

Finding 49: four method summaries say the density gate refuses at equality. The code and the pinned test implement at-or-under. The fix corrects the four summaries.

Finding 50: a NaN multiplier takes three resolver arms down three different paths, and one path fails open. The fix refuses NaN in every arm.

Finding 51: the exponent seed of -1 in the texture-scaled domain construction is load-bearing and carries no comment. The fix documents the seed and pins it with tests on integer UV coordinates.

This pair shares one production file with another pair. The plan adds `UvMapping.IsIdentity` to `Editor/Semantics/MaterialSemantics.cs`. Pair 6 deletes the `threadMaps` parameter of `ScalarProductFold.Fold` in the same file at position 6 in the shared execution order. The regions are disjoint. This pair lands first.
The pair runs at position 2 in the shared execution order, after the multi-capture pair and before the host pair.

---

## 2. Background and Motivation

All line numbers in this section were re-verified against the working tree on 2026-10-09. Several citations in the investigation drifted by a few lines. The numbers below are the observed ones.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:1006-1102, 1104-1180`.
`ClassifyBilinearClamp` and `HasMappedWitnessBilinearClamp` run a double-precision separating-axis pre-filter before the exact rational intersection test.
The pre-filter decides with absolute margins of 1e-12 and 1e-9 at `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:1321-1350`.
`ExtractTexelVertices` at `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:1352-1370` converts exact rational domain vertices to doubles in absolute texel units.
The absolute rounding error of that conversion grows with the coordinate magnitude without a bound.
A triangle with UV vertices in the millions of texel units carries conversion error above the margins.
The pre-filter then reports separation for a support box the triangle truly touches.
The walk skips the witness candidate, never runs the exact test, and returns ProvenOpaque.
The repeat walks stay safe because they normalize the domain into one texture period first, at `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:945-990`.
The clamp walks never normalize.
Failure direction: wrongly opaque.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:236-249, 251-278, 365-390, 694-766`.
The `Or` summary claims the sum reaches exactly one exactly when either factor reaches one.
The claim is false. Two factors bounded in [0, 1] reach a sum of one whenever they add to at least one.
The `Classify` disjunction arm returns MustRemainTransparent when both factors classify transparent.
Each factor proves a sub-one witness, but the witnesses can sit at different texels.
No one proves a sub-one witness for the sum.
`ResolveSaturatingSum` repeats the same defect twice in its uniform shortcuts at `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:744-762`: when one factor is uniformly transparent, it returns the other factor's resolution.
The same file states the correct rule two methods earlier: cross-field correlation between two sampled terms is unknowable, so no per-triangle proof exists without an additive classifier.
Failure direction: wrongly transparent, with a false proof claim in the absorbing slot.

`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs:1672-1690`.
The pinned test `SaturatingSumOfTwoMixedFieldsIsUnknownWhereNeitherProves` builds a disjunction of two classified factors that both refute through their second mip.
It asserts the absorbing outcome MustRemainTransparent.
The test name already states Unknown. The body bakes the absorbing arm.
The shared ruling mandates updating this expectation.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:404-409` and `Packages/com.alrauna.amuse/Editor/Analysis/AffineUvTransform.cs:105-111`.
Two identity predicates decide whether the resolver transforms and whether the transform transforms.
The resolver carries the test inline. The transform carries a private `IsIdentity` method.
The two forms are De Morgan negations of each other. They agree logically today, not textually.
A future tolerance added to one silently desynchronizes the envelope from the transform.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs:380-386, 407-414`, `Packages/com.alrauna.amuse/Editor/Analysis/AffineUvTransform.cs:256-262, 263-270`, `Packages/com.alrauna.amuse/Editor/Semantics/AffineAlphaMap.cs:143-168`.
The dyadic to rational conversion and the bit length helper are hand-rolled three times.
The two Analysis copies are byte-identical shift loops under one shared comment.
The Semantics copy in `AffineAlphaMap` is a different byte-array algorithm for the same function.
Only comments keep the normalization behaviors apart.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:677-687, 941-951, 1186-1196, 1536-1546` against `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs:1135-1152`.
Four erasure gate summaries say the erased share stays strictly under the policy and that equality refuses.
The code at `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:724, 988, 1225, 1580` implements `erased * 100 <= (long)maxNoiseTexelPercent * consulted`.
The pinned test `EqualityAtTheCoverageBoundMoves` bakes the at-or-under rule.
The code and the test agree. The summaries drift.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:543-573, 640-654, 883-902`.
A NaN multiplier fails every ordered float comparison.
The constant arm `ResolveScalar` falls through both branches and refuses as unsupported.
The scaled-sample arm `ResolveScaledSample` falls through both branches and returns a uniform MustRemainTransparent.
The product chain arm `ResolveProductChain` fails the greater-than and less-than checks, treats NaN as exactly one, and resolves a NaN alpha as proven opaque through an opaque factor.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs:428-430, 452-458, 644`.
The public `ScalarSemanticValue` factories validate finiteness today, so no NaN multiplier enters through them.
The fail-open arms remain a defect for any future construction path.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs:455, 461` against `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:992-1003, 1228-1264, 1658-1687`.
The seed `var exponent = -1` forces the texture-scaled domain's `TexelScale` to at least 2.
BigInteger division truncates. Every half-texel computation `texelScale / 2` in the interval builders is exact only when the texel scale is at least 2.
A cleanup that raises the seed to 0 lets integer UV coordinates produce a texel scale of 1.
The half texel becomes zero, the repeat and support intervals collapse, and the walks return false ProvenOpaque verdicts.
The seed carries no comment.

---

## 3. Detailed Design

### 3.1 Finding 4: Per-Candidate Pre-Filter Translation with a Proven Magnitude Bound

In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`:

Keep `ConservativeBilinearSupportOverlapsTriangle` and `EdgeSeparates` unchanged. The separating-axis test is frame-agnostic.
Keep `ExtractTexelVertices` and its once-per-walk absolute conversion. The absolute doubles feed only the `isBoundary` bypass, whose comparisons are sign-safe at any magnitude.
Keep `canPreFilter`, the candidate window, and the budget guard unchanged.
Keep the exact `ExactUvGeometry.Intersects` call as the sole arbiter.

Add a named bound and a per-candidate extraction helper:

```csharp
// The pre-filter margins (1e-12 and 1e-9) are proven safe only for
// translated vertex magnitudes at or under this bound. Half an ulp at
// 2^8 is 2.8e-14. The full dot-product error stack stays below 2.2e-10,
// under the 1e-9 margin with headroom. Above the bound the pre-filter
// stands down and the exact intersection test decides.
private const double PreFilterSafeCoordinateBound = 256;

private static bool TryExtractTexelVerticesRelativeToCandidate(
    ExactUvDomain domain, int x, int y,
    out double v0x, out double v0y,
    out double v1x, out double v1y,
    out double v2x, out double v2y)
```

The helper subtracts the candidate support box center `(x + 0.5, y + 0.5)` from each exact rational vertex, converts the small translated values to texel-unit doubles, and returns false when any translated magnitude exceeds the bound.

In `ClassifyBilinearClamp` and `HasMappedWitnessBilinearClamp`, replace the single absolute pre-filter extraction with the per-candidate call. The fixed support box becomes `(-1, 1, -1, 1)`:

```csharp
if (canPreFilter && TryExtractTexelVerticesRelativeToCandidate(
        domain, x, y, out v0x, out v0y, out v1x, out v1y, out v2x, out v2y))
{
    if (!isBoundary && !ConservativeBilinearSupportOverlapsTriangle(
            -1, 1, -1, 1,
            v0x, v0y, v1x, v1y, v2x, v2y))
    {
        continue;
    }
}
```

Soundness argument:
All translated coordinates are exact dyadic rationals of magnitude at most 256, so each double conversion carries at most half an ulp, 2.8e-14.
Edge normals then have magnitude at most 2^9. The dot-product terms have magnitude at most 2^18 and round with error at most 2.2e-10.
That stays below the 1e-9 separation margin, so a reported separation implies true separation.
A misfired 1e-12 collinearity escape only suppresses a skip. That direction is conservative because the exact test stays the arbiter.
`[INFERENCE]` The falsifier below reproduces the defect before the fix and the same-verdict property after it.

### 3.2 Finding 7: Retract the Absorbing Transparency Claims to Unknown

In `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`:

Rewrite the `Or` summary at lines 236 to 249. The new text states the true rule: each factor is bounded in [0, 1] by the field contract, so the sum reaches one whenever the factors add to at least one. Complementary masks with sub-one witnesses at different texels sum to identically one. The opaque arm is sound, because a factor proven exactly one decides the sum alone. The absorbing arm returns Unknown, because cross-field correlation is unknowable without an additive classifier.

Change the `Classify` disjunction arm at lines 365 to 390. Delete the absorbing return. Both factors transparent now falls through to Unknown:

```csharp
if (_isDisjunction)
{
    var first = _firstFactor.Classify(triangle);
    if (first == TriangleAlphaOutcome.ProvenOpaque)
    {
        return TriangleAlphaOutcome.ProvenOpaque;
    }

    var second = _secondFactor.Classify(triangle);
    if (second == TriangleAlphaOutcome.ProvenOpaque)
    {
        return TriangleAlphaOutcome.ProvenOpaque;
    }

    return TriangleAlphaOutcome.Unknown;
}
```

Correct the `ResolveSaturatingSum` summary at lines 694 to 712 with the same true rule.
Replace the two uniform shortcut returns at lines 744 to 762. A uniformly transparent factor proves nothing about the sum:

```csharp
var firstTransparent = firstResolution.TryGetUniformOutcome(
    out var firstOutcome) &&
    firstOutcome == TriangleAlphaOutcome.MustRemainTransparent;
var secondTransparent = secondResolution.TryGetUniformOutcome(
    out var secondOutcome) &&
    secondOutcome == TriangleAlphaOutcome.MustRemainTransparent;
if (firstTransparent || secondTransparent)
{
    // No common-point additive argument exists. A sub-one witness
    // for one factor says nothing about the sum at that texel.
    return AlphaResolution.Uniform(TriangleAlphaOutcome.Unknown);
}
```

Keep the two `IsUniformlyProvenOpaque` early returns. They are sound: one side attested exactly one decides the sum alone.

Update the pinned test `SaturatingSumOfTwoMixedFieldsIsUnknownWhereNeitherProves` at `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs:1672-1690`. Change the expected outcome from MustRemainTransparent to Unknown and update the comment. The shared ruling mandates this update. The test name already states Unknown. The old expectation pinned a false proof, so the update is not a test weakening.

Add the mandated falsifier as a named test. Two complementary classified masks whose sum is identically one must not prove transparency:

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

    // Each factor refutes with a sub-one witness at a different texel.
    // The saturate of the two fields is identically one, so the honest
    // answer without an additive classifier is Unknown, never a
    // transparency proof.
    Assert.That(
        resolution.Classify(SpanningTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.Unknown));
}
```

Add one test for the uniform shortcut arm:

```csharp
[Test]
public void SaturatingSumOfTransparentScaledSampleAndRefutingFieldIsUnknown()
{
    var value = ScalarSemanticValue.SaturatingSum(
        ScalarSemanticValue.TextureTimesConstant(
            Sample(), TextureChannel.Alpha, 0.5f),
        ScalarSemanticValue.Texture(MaskSample(), TextureChannel.Red));
    var resolution = AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(value),
        ProvidingTwo(Chain(Field(1, 1, 255)), OpaqueThenTransparentChain()), 0);

    Assert.That(
        resolution.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.Unknown));
}
```

### 3.3 Finding 50: Refuse NaN in Every Resolver Arm

In `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`:

Add the same first check to `ResolveScaledSample`, `ResolveProductChain`, and `ResolveScalar`:

```csharp
if (float.IsNaN(multiplier))
{
    return AlphaResolution.Refused(
        AlphaResolutionFailure.UnsupportedMultiplier);
}
```

`ResolveScalar` takes the value directly, so its check reads `float.IsNaN(scalar)`.
In `ResolveScalar` the check changes no outcome. The fall-through already refuses. The explicit branch gives all three arms one identical NaN discipline and one named failure.
Update the multiplier lemma summary above `ResolveScalar` to state that a NaN multiplier refuses in every arm.

The public factories validate finiteness today, so the RED tests build their values through the private constructor by reflection. The private eleven-parameter constructor at `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs:878` performs no validation. The tests use `Activator.CreateInstance` with `BindingFlags.NonPublic | BindingFlags.Instance`.

### 3.4 Finding 49: Correct the Four Density Gate Summaries

In `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`, correct one sentence in the four erasure gate summaries at lines 677 to 687, 941 to 951, 1186 to 1196, and 1536 to 1546.

Replace:

> Erasure substitutes only when the erased share of the consulted candidates stays strictly under the policy. Equality refuses.

With:

> Erasure substitutes when the erased share of the consulted candidates stays at or under the policy. Equality moves.

The code at lines 724, 988, 1225, and 1580 stays unchanged. The pinned test `EqualityAtTheCoverageBoundMoves` stays unchanged. This is a documentation fix with no behavior change.

### 3.5 Finding 51: Document the Load-Bearing Exponent Seed

In `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`, add this comment above the seed at line 455:

```csharp
// Load-bearing seed. BigInteger division truncates, so every
// half-texel computation (texelScale / 2 in the classifier's interval
// builders) is exact only when TexelScale is at least 2. This seed
// forces exponent <= -1 and therefore TexelScale >= 2. A seed of 0
// would let integer UVs produce TexelScale 1, a zero half texel, an
// empty support interval, and false ProvenOpaque verdicts.
var exponent = -1;
```

Pin the seed with two tests. Both are characterization tests. Both pass on the first run by design. Their purpose is to fail a future cleanup that touches the seed. The defect today is the missing comment and the missing pin, not the behavior.

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
    var texture = new AlphaTextureData(2, 2, new byte[] { 255, 255, 255, 0 });

    Assert.That(
        TriangleAlphaClassifier.Classify(
            triangle, texture,
            new TextureSampling(TextureFilterMode.Bilinear, TextureWrapMode.Clamp),
            AlphaUvEnvelope.Zero),
        Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
}
```

### 3.6 Finding 48: One Identity Predicate and One Helper Set

In `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`, add one identity predicate to the `UvMapping` type next to `Equals`:

```csharp
/// <summary>
/// True when the mapping scales and offsets by identity. The resolver
/// and the affine transform must decide identity from this one member,
/// so the envelope and the transform can never desynchronize.
/// </summary>
internal bool IsIdentity =>
    Scale.x == 1f && Scale.y == 1f &&
    Offset.x == 0f && Offset.y == 0f;
```

Delete the private `IsIdentity` in `Packages/com.alrauna.amuse/Editor/Analysis/AffineUvTransform.cs:105-111` and call the member.
Replace the inline comparison in `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:404-409` with `if (!_mapping.IsIdentity)`.
NaN semantics stay identical, and a NaN cannot reach the member anyway, because the `UvMapping` constructor validates finiteness.

In `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs`, change the two private helpers `ToRational(ExactDyadic)` at lines 380 to 386 and `BitLength(BigInteger)` at lines 407 to 414 to `internal`. Delete the byte-identical private copies in `Packages/com.alrauna.amuse/Editor/Analysis/AffineUvTransform.cs:256-262, 263-270` and call the shared members. `AffineUvTransform` already calls other `ExactUvGeometry` members, so the dependency direction is established.

`AffineAlphaMap` in `Packages/com.alrauna.amuse/Editor/Semantics/AffineAlphaMap.cs:143-168` carries a third bit length implementation with a different byte-array algorithm. Fold it into the shared member. The two algorithms compute the same function for every non-negative value, and a sweep test proves the equivalence. `AffineAlphaMap` sits outside this pair's primary file list, so the executor treats this one deletion as the finding's third consumer and nothing more.

---

## 4. Verification and Test Plan

1. Unit test `BilinearClampLargeCoordinateRunMatchesSmallRunVerdict`:
   Classify an 8 by 8 clamp texture with one sub-opaque texel at index 27.
   The triangle vertices sit in texel units at (-16M, -16M), (16M+2, 16M+2), and (-16M, 16M+2), with the diagonal exactly through the support box corner (4.5, 4.5) of the sub-opaque texel.
   The small control uses the same shape at (-8, -8), (10, 10), and (-8, 10).
   Both runs must return MustRemainTransparent. Before the fix the large run returns ProvenOpaque.
2. Unit test `BilinearClampSixteenTexelShiftedRunMatchesSmallRunVerdict`:
   Run the same shape shifted by 16 texels. All three runs must agree.
3. Unit test `ComplementaryMasksSummingToIdenticallyOneAreNotProvenTransparent`:
   The disjunction of two complementary classified masks returns Unknown. Before the fix it returns MustRemainTransparent.
4. Unit test `SaturatingSumOfTransparentScaledSampleAndRefutingFieldIsUnknown`:
   The uniform shortcut returns Unknown instead of the other factor's resolution.
5. Unit test `SaturatingSumOfTwoMixedFieldsIsUnknownWhereNeitherProves` (pinned update):
   The pinned expectation changes from MustRemainTransparent to Unknown. This update is mandated by the shared ruling.
6. Unit tests `ScaledSampleArmRefusesNaNMultiplier`, `ProductChainArmRefusesNaNMultiplier`, and `ConstantArmRefusesNaNMultiplier`:
   The three arms refuse a NaN multiplier as `AlphaResolutionFailure.UnsupportedMultiplier`. The tests build values by reflection through the private constructor. The constant arm test is characterization and passes on the first run.
7. Unit test `UvMappingIdentityMatchesPerComponentComparison`:
   The shared `IsIdentity` member matches the per-component comparison across identity, scaled, offset, and channel-only mappings.
8. Unit tests `SharedBitLengthMatchesReferenceAcrossMagnitudes` and `SharedDyadicToRationalMatchesDirectConstruction`:
   The shared helpers match reference implementations across a magnitude sweep. The test for the `AffineAlphaMap` consumer sweep proves the byte-array algorithm and the shift loop agree.
9. Unit tests `IntegerUvVerticesKeepTexelScaleAtTwo` and `BilinearClampIntegerUvTriangleStillFindsSubOpaqueTexel`:
   Both pin the load-bearing seed. Both are characterization tests and pass on the first run.
10. Pinned test `EqualityAtTheCoverageBoundMoves`:
    Stays unchanged and green. Confirms the Finding 49 summaries now describe the code the test pins.
11. A plan whose filtered run reports 0 tests is a failure. Every task names its tests, and the verification steps require the named tests in the filtered run output.
