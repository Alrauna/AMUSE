# Alpha Semantics Resolver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` by default. Use `superpowers:subagent-driven-development` only if the user separately authorizes subagents. Track execution with the checkboxes below.

**Goal:** Implement the approved shader-independent bridge from a complete normalized `MaterialSemantics.Alpha` value to the existing exact triangle alpha classifier.

**Architecture:** One internal Editor-only static resolver switches exhaustively over the closed Alpha vocabulary. It returns one immutable resolution: a uniform triangle outcome, an exact classifier configuration, or a named refusal. Immutable scalar-field evidence comes through a caller-supplied lookup delegate. Thus, the resolver never accesses Unity assets, meshes, or shaders. It uses the classifier, geometry, semantic core, and planner without changes.

**Tech Stack:** Unity 2022.3.22f1, C#, NUnit EditMode tests, and existing `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor` assemblies. Do not add a dependency, assembly, asmdef, or package metadata change.

## Global constraints

- The approved specification is `docs/superpowers/specs/2026-08-17-alpha-semantics-resolver-design.md`. Read it before Task 1, including its "Review amendments" section. The evidence contract is a provable "exactly 1 versus strictly below 1" partition plus a `[0, 1]` bound. It does **not** require the source to be a literal 8-bit `b/255` field. The deferred UV-transform criterion requires exact dyadic/rational arithmetic, never `double`. `AlphaResolution` must make its invariants unrepresentable or reject them.
- Do not write, bake, quantize, or alter a texture. Evidence normalization is the responsibility of a later host producer and is out of scope. This milestone only consumes evidence.
- Execute only after explicit design/plan approval, on `feat/alpha-semantics-resolver` based on `4e37d29`.
- Use red/green TDD. Observe each focused red for the intended reason before you write production code. Then observe the same scope green. Never write production code first.
- All new production types are `internal` and Editor-only, in namespace `Alrauna.Amuse.Editor.Analysis`.
- Do **not** modify `TriangleAlphaClassifier.cs`, `ExactUvGeometry.cs`, `MeshSeparationPlanner.cs`, `MaterialSemantics.cs`, the Poiyomi adapter, any existing test, the reference fixtures, or asmdefs. Also, do **not** modify `AssemblyInfo.cs`, package metadata, manifests/locks, workflows, or project settings. The resolver adds files and changes none.
- Do not use `AssetDatabase`, `Texture`, `Texture2D`, `Material`, `Mesh`, `Renderer`, `Shader`, an NDMF type, or an MCP call. Do not access file I/O, `Debug.Log`, or `QualitySettings` in the resolver or its tests.
- Do not use epsilon, tolerance, rounding, or approximate comparisons. Compare floats exactly.
- Never widen a claim under uncertainty. Each unsupported case is a refusal or `TriangleAlphaOutcome.Unknown`, never `ProvenOpaque`.
- Treat each Unity asset and its `.meta` file as one unit. Unity import must supply the `.meta` for new `.cs` files. Inspect each new GUID. Do not manually write, copy, or delete `.meta` files.
- Do not commit, push, open a PR, tag, publish, or change repository settings. Those actions require separate authorization. The plan ends at a review handoff.
- Stop at a new design approval gate if execution finds that the approved vocabulary or classifier contract cannot express a required case. Do not grow an expression form, extend classifier inputs, or bake a texture during execution.

---

## Planned files

**Create:**

- `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`
- `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs.meta`
- `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs.meta`

**Modify:** none.

Both target directories already exist, and the existing asmdefs already cover them. Thus, you do not need a new folder or `.meta` for a folder. Keep the enum, delegate, resolution type, and resolver in the single production file. They form one small contract, as `MaterialSemantics.cs` keeps its vocabulary together.

## Interfaces produced by this plan

The complete public surface after Task 4, for reference by every task:

```csharp
namespace Alrauna.Amuse.Editor.Analysis
{
    internal enum AlphaResolutionFailure
    {
        None,
        SemanticsUnknown,
        UnsupportedMultiplier,
        UnsupportedUvMapping,
        UnsupportedSampling,
        MissingTextureEvidence,
    }

    internal delegate bool AlphaFieldProvider(
        TextureSourceId source,
        TextureChannel channel,
        out AlphaTextureData field);

    internal sealed class AlphaResolution
    {
        internal bool IsResolved { get; }
        internal AlphaResolutionFailure Failure { get; }
        internal TriangleAlphaOutcome Classify(TriangleAlphaInput triangle);
    }

    internal static class AlphaSemanticsResolver
    {
        internal static AlphaResolution Resolve(
            SemanticOutput<ScalarSemanticValue> alpha,
            AlphaFieldProvider fieldProvider);
    }
}
```

`TextureSourceId`, `TextureChannel`, `ScalarSemanticValue`, `SemanticOutput<T>`, `TextureSample`, `UvMapping`, `TextureSampling`, `TextureFilterMode`, and `TextureWrapMode` come from `Alrauna.Amuse.Editor.Semantics`. Use them as they exist today.

## Shared test helpers

Add these once at the top of the test class in Task 1. Reuse them in each later task. Do not redefine them for each task.

```csharp
using System;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;

// Required: UnityEngine also declares TextureWrapMode, so the semantic enum
// must be named explicitly in this test file.
using TextureWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class AlphaSemanticsResolverTests
    {
        private static TextureSample Sample(
            TextureFilterMode filter = TextureFilterMode.Point,
            TextureWrapMode wrap = TextureWrapMode.Clamp,
            int uvChannel = 0,
            float scaleX = 1f,
            float scaleY = 1f,
            float offsetX = 0f,
            float offsetY = 0f)
        {
            return new TextureSample(
                new TextureSourceId("test:field"),
                new UvMapping(
                    uvChannel,
                    new Vector2(scaleX, scaleY),
                    new Vector2(offsetX, offsetY)),
                new TextureSampling(filter, wrap));
        }

        private static AlphaTextureData Field(int width, int height, byte value)
        {
            var bytes = new byte[width * height];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = value;
            }

            return new AlphaTextureData(width, height, bytes);
        }

        /// Half opaque (bottom row 255), half transparent (top row 0).
        private static AlphaTextureData MixedField()
        {
            return new AlphaTextureData(
                2, 2, new byte[] { 255, 255, 0, 0 });
        }

        private static AlphaFieldProvider Providing(AlphaTextureData field)
        {
            return (TextureSourceId source, TextureChannel channel,
                out AlphaTextureData result) =>
            {
                result = field;
                return true;
            };
        }

        private static AlphaFieldProvider ProvidingNothing()
        {
            return (TextureSourceId source, TextureChannel channel,
                out AlphaTextureData result) =>
            {
                result = null;
                return false;
            };
        }

        /// A nondegenerate triangle covering the lower-left quarter of UV space.
        private static TriangleAlphaInput OpaqueCornerTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.05f, 0.05f),
                new Vector2(0.45f, 0.05f),
                new Vector2(0.05f, 0.45f));
        }

        /// The same shape in the upper half of UV space.
        private static TriangleAlphaInput TransparentCornerTriangle()
        {
            return TriangleAlphaInput.WithUv0(
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector2(0.55f, 0.55f),
                new Vector2(0.95f, 0.55f),
                new Vector2(0.55f, 0.95f));
        }
    }
}
```

`MixedField` stores rows from bottom to top. Thus, texel row 0 (`v < 0.5`) is opaque, and row 1 (`v > 0.5`) is transparent. With Point/Clamp sampling, `OpaqueCornerTriangle` therefore classifies `ProvenOpaque`, and `TransparentCornerTriangle` classifies `MustRemainTransparent`. Verify this assumption in Task 3 Step 1. Assert the same outcomes from a direct `TriangleAlphaClassifier.Classify` call in the same test. If the direct call disagrees, fix the fixture coordinates, never the assertion.

## Running tests

Run all tests on the public `<repo-root>` Unity instance through Unity MCP `run_tests` (EditMode). Before the first run, use read-only MCP discovery. Confirm that the connected instance's project root is `<repo-root>`. If no public Unity Editor is running, **do not** use the private avatar testbed. Report the blocked validation and stop.

Focused run:

```text
test_names: Alrauna.Amuse.Tests.Editor.Analysis.AlphaSemanticsResolverTests
include_failed_tests: true
```

Full run: all EditMode tests, no filter.

---

### Task 1: Resolution boundary and unknown semantics

**Files:**

- Create: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`
- Create after red: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`

**Interfaces:**

- Consumes: `SemanticOutput<ScalarSemanticValue>`, `TriangleAlphaInput`, `AlphaTextureData`, `TextureSourceId`, `TextureChannel` (all existing).
- Produces: `AlphaResolutionFailure`, `AlphaFieldProvider`, `AlphaResolution`, `AlphaSemanticsResolver.Resolve` as declared above.

- [x] **Step 1: Write the failing boundary tests**

Add the shared helpers above, then add these tests:

```csharp
[Test]
public void UnknownAlphaSemanticsRefuses()
{
    var resolution = AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Unknown(),
        ProvidingNothing());

    Assert.That(resolution.IsResolved, Is.False);
    Assert.That(
        resolution.Failure,
        Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown));
}

[Test]
public void DefaultSemanticOutputRefusesLikeExplicitUnknown()
{
    var resolution = AlphaSemanticsResolver.Resolve(
        default(SemanticOutput<ScalarSemanticValue>),
        ProvidingNothing());

    Assert.That(
        resolution.Failure,
        Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown));
}

[Test]
public void RefusedResolutionCannotProduceAnOutcome()
{
    var resolution = AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Unknown(),
        ProvidingNothing());

    Assert.Throws<InvalidOperationException>(
        () => resolution.Classify(OpaqueCornerTriangle()));
}

[Test]
public void NullFieldProviderIsMalformed()
{
    Assert.Throws<ArgumentNullException>(() => AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(
            ScalarSemanticValue.Constant(1f)),
        null));
}
```

- [x] **Step 2: Run the focused tests and observe red**

Expected: the test assembly cannot resolve `AlphaSemanticsResolver`, `AlphaResolution`, `AlphaResolutionFailure`, or `AlphaFieldProvider`. This first red is a compile failure because no production type exists yet. Each later red must be an executable assertion failure.

- [x] **Step 3: Write the minimal production shell**

Create `AlphaSemanticsResolver.cs` with the enum, delegate, resolution type, and resolver. Implement only the unknown-semantics path and the malformed-argument check. Leave the complete-value path throwing `NotImplementedException` so Task 2 has a genuine executable red.

```csharp
using System;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Analysis
{
    internal enum AlphaResolutionFailure
    {
        None,
        SemanticsUnknown,
        UnsupportedMultiplier,
        UnsupportedUvMapping,
        UnsupportedSampling,
        MissingTextureEvidence,
    }

    /// <summary>
    /// Host-supplied lookup of immutable, predicate-equivalent scalar
    /// evidence. It returns false unless the provider can prove, for the named
    /// source and channel over the relevant base-level texel domain in
    /// bottom-to-top order, that every effective per-texel scalar value is
    /// finite and within [0, 1], that byte 255 marks exactly the texels whose
    /// value is exactly 1, and that every other byte marks a value strictly
    /// below 1. Under Point or Bilinear sampling those facts give the
    /// classifier its predicate: the sampled value is 1 exactly when every
    /// positive-weight contributing texel is 255. The source need not itself be
    /// an uncompressed 8-bit b/255 field. The resolver never opens an asset.
    /// </summary>
    internal delegate bool AlphaFieldProvider(
        TextureSourceId source,
        TextureChannel channel,
        out AlphaTextureData field);

    /// <summary>
    /// One immutable decision about how a normalized Alpha semantic value may
    /// be proven: a uniform outcome that needs no geometry, an exact
    /// classifier configuration, or a named refusal that yields no outcome.
    /// </summary>
    internal sealed class AlphaResolution
    {
        private readonly bool _isUniform;
        private readonly TriangleAlphaOutcome _uniformOutcome;
        private readonly AlphaTextureData _field;
        private readonly AlphaSamplingSettings _sampling;

        private AlphaResolution(
            bool isResolved,
            AlphaResolutionFailure failure,
            bool isUniform,
            TriangleAlphaOutcome uniformOutcome,
            AlphaTextureData field,
            AlphaSamplingSettings sampling)
        {
            // Invariants: a resolved value carries no failure, a refusal
            // carries one, and a classified value always has its field.
            if (isResolved != (failure == AlphaResolutionFailure.None))
            {
                throw new ArgumentException(
                    "A resolution is resolved exactly when it has no failure.",
                    nameof(failure));
            }
            if (isResolved && !isUniform && field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            IsResolved = isResolved;
            Failure = failure;
            _isUniform = isUniform;
            _uniformOutcome = uniformOutcome;
            _field = field;
            _sampling = sampling;
        }

        internal bool IsResolved { get; }
        internal AlphaResolutionFailure Failure { get; }

        internal static AlphaResolution Refused(AlphaResolutionFailure failure)
        {
            return new AlphaResolution(
                false, failure, false, default, null, default);
        }

        internal static AlphaResolution Uniform(TriangleAlphaOutcome outcome)
        {
            return new AlphaResolution(
                true,
                AlphaResolutionFailure.None,
                true,
                outcome,
                null,
                default);
        }

        internal static AlphaResolution Classified(
            AlphaTextureData field,
            AlphaSamplingSettings sampling)
        {
            return new AlphaResolution(
                true,
                AlphaResolutionFailure.None,
                false,
                default,
                field,
                sampling);
        }

        /// <summary>
        /// Classifies one triangle under this resolution. A uniform resolution
        /// is independent of geometry and UV data and ignores the triangle: a
        /// constant alpha cannot vary across the surface. A refused resolution
        /// exposes no outcome at all.
        /// </summary>
        internal TriangleAlphaOutcome Classify(TriangleAlphaInput triangle)
        {
            if (!IsResolved)
            {
                throw new InvalidOperationException(
                    "A refused alpha resolution has no triangle outcome.");
            }

            return _isUniform
                ? _uniformOutcome
                : TriangleAlphaClassifier.Classify(triangle, _field, _sampling);
        }
    }

    internal static class AlphaSemanticsResolver
    {
        internal static AlphaResolution Resolve(
            SemanticOutput<ScalarSemanticValue> alpha,
            AlphaFieldProvider fieldProvider)
        {
            if (fieldProvider == null)
            {
                throw new ArgumentNullException(nameof(fieldProvider));
            }

            if (!alpha.IsComplete)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.SemanticsUnknown);
            }

            throw new NotImplementedException();
        }
    }
}
```

- [x] **Step 4: Import and inspect the new asset pair**

Let Unity import the new files (MCP `refresh_unity`). Confirm that Unity created `AlphaSemanticsResolver.cs.meta` and `AlphaSemanticsResolverTests.cs.meta` with fresh unique GUIDs. Confirm that no other `.meta` changed.

- [x] **Step 5: Run the focused tests and observe green**

Expected: the four boundary tests pass. No other test changes state. The Console has no unexpected errors.

---

### Task 2: Constant alpha forms

**Files:**

- Modify: both files from Task 1

**Interfaces:**

- Consumes: `ScalarSemanticValue.Constant`, `ScalarSemanticValueKind`.
- Produces: no new type. `Resolve` now returns uniform resolutions for constants.

- [x] **Step 1: Write the failing constant tests**

```csharp
private static AlphaResolution ResolveConstant(float value)
{
    return AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(
            ScalarSemanticValue.Constant(value)),
        ProvidingNothing());
}

[Test]
public void ConstantOneIsProvenOpaqueForEveryTriangle()
{
    var resolution = ResolveConstant(1f);

    Assert.That(resolution.IsResolved, Is.True);
    Assert.That(
        resolution.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
    Assert.That(
        resolution.Classify(TransparentCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
}

[TestCase(0.5f)]
[TestCase(0f)]
[TestCase(-1f)]
[TestCase(0.99999994f)] // the largest float below 1
public void ConstantBelowOneCanNeverBeOpaque(float value)
{
    var resolution = ResolveConstant(value);

    Assert.That(
        resolution.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
}

[TestCase(1.0000001f)] // the smallest float above 1
[TestCase(2f)]
public void ConstantAboveOneHasNoDefinedOpacityMeaning(float value)
{
    var resolution = ResolveConstant(value);

    Assert.That(resolution.IsResolved, Is.False);
    Assert.That(
        resolution.Failure,
        Is.EqualTo(AlphaResolutionFailure.UnsupportedMultiplier));
}

[Test]
public void ConstantAlphaIgnoresDegenerateGeometryAndMissingUv()
{
    var degenerate = TriangleAlphaInput.MissingUv0(
        Vector3.zero, Vector3.zero, Vector3.zero);

    Assert.That(
        ResolveConstant(1f).Classify(degenerate),
        Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
}

[Test]
public void ConstantAlphaNeverConsultsTheFieldProvider()
{
    var consulted = false;
    AlphaFieldProvider provider = (TextureSourceId source,
        TextureChannel channel, out AlphaTextureData result) =>
    {
        consulted = true;
        result = null;
        return false;
    };

    AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(
            ScalarSemanticValue.Constant(1f)),
        provider);

    Assert.That(consulted, Is.False);
}
```

- [x] **Step 2: Run the focused tests and observe red**

Expected: each new test fails with `NotImplementedException` from `Resolve`. The four Task 1 tests still pass.

- [x] **Step 3: Implement the constant branch**

Replace the `NotImplementedException` with a kind switch. Its constant arm must apply the decision table. Leave the two texture arms throwing `NotImplementedException` for Task 3.

```csharp
var value = alpha.GetCompleteValue();
switch (value.Kind)
{
    case ScalarSemanticValueKind.Constant:
        return ResolveScalar(value.GetConstantValue());
    default:
        throw new NotImplementedException();
}
```

```csharp
/// <summary>
/// The multiplier lemma for a value already known to lie in [0, 1] before
/// scaling. Exactly one is opaque; anything below one can never reach one;
/// anything above one has no defined opacity meaning because the semantic
/// model states no clamp or saturate behavior.
/// </summary>
private static AlphaResolution ResolveScalar(float scalar)
{
    if (scalar == 1f)
    {
        return AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque);
    }

    if (scalar < 1f)
    {
        return AlphaResolution.Uniform(
            TriangleAlphaOutcome.MustRemainTransparent);
    }

    return AlphaResolution.Refused(
        AlphaResolutionFailure.UnsupportedMultiplier);
}
```

`ResolveScalar` applies the lemma to the constant itself, which is the degenerate case `s = 1`. Task 4 handles the multiplier separately. Its `k < 1` arm must still obtain the range-attesting field before it concludes. Do not merge the two.

- [x] **Step 4: Run the focused tests and observe green**

Expected: all Task 1 and Task 2 tests pass. `ScalarSemanticValue.Constant` rejects NaN and infinity during construction. Thus, no non-finite arm is needed or permitted here.

---

### Task 3: Texture-sampled alpha with an exact multiplier of one

**Files:**

- Modify: both files from Task 1

**Interfaces:**

- Consumes: `TextureSample`, `UvMapping`, `TextureSampling`, `TextureFilterMode`, `TextureWrapMode`, `TextureChannel`, `AlphaSamplingSettings`, `TriangleAlphaClassifier.Classify`.
- Produces: no new type. `Resolve` now returns classified resolutions.

- [x] **Step 1: Write the failing sampled-alpha tests**

```csharp
private static AlphaResolution ResolveSample(
    TextureSample sample,
    TextureChannel channel,
    AlphaFieldProvider provider)
{
    return AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(
            ScalarSemanticValue.Texture(sample, channel)),
        provider);
}

[Test]
public void SampledAlphaDelegatesToTheClassifier()
{
    var field = MixedField();
    var resolution = ResolveSample(
        Sample(), TextureChannel.Alpha, Providing(field));
    var sampling = new AlphaSamplingSettings(
        AlphaFilterMode.Point, AlphaWrapMode.Clamp);

    Assert.That(resolution.IsResolved, Is.True);
    Assert.That(
        resolution.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaClassifier.Classify(
            OpaqueCornerTriangle(), field, sampling)));
    Assert.That(
        resolution.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
    Assert.That(
        resolution.Classify(TransparentCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
}

[TestCase(TextureChannel.Red)]
[TestCase(TextureChannel.Green)]
[TestCase(TextureChannel.Blue)]
[TestCase(TextureChannel.Alpha)]
public void EveryScalarChannelUsesTheSameScalarField(TextureChannel channel)
{
    var requested = (TextureChannel?)null;
    AlphaFieldProvider provider = (TextureSourceId source,
        TextureChannel requestedChannel, out AlphaTextureData result) =>
    {
        requested = requestedChannel;
        result = MixedField();
        return true;
    };

    var resolution = ResolveSample(Sample(), channel, provider);

    Assert.That(requested, Is.EqualTo(channel));
    Assert.That(
        resolution.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
}

[TestCase(TextureFilterMode.Point, TextureWrapMode.Clamp,
    AlphaFilterMode.Point, AlphaWrapMode.Clamp)]
[TestCase(TextureFilterMode.Point, TextureWrapMode.Repeat,
    AlphaFilterMode.Point, AlphaWrapMode.Repeat)]
[TestCase(TextureFilterMode.Bilinear, TextureWrapMode.Clamp,
    AlphaFilterMode.Bilinear, AlphaWrapMode.Clamp)]
[TestCase(TextureFilterMode.Bilinear, TextureWrapMode.Repeat,
    AlphaFilterMode.Bilinear, AlphaWrapMode.Repeat)]
public void SamplingMapsExhaustivelyAndExactly(
    TextureFilterMode filter,
    TextureWrapMode wrap,
    AlphaFilterMode expectedFilter,
    AlphaWrapMode expectedWrap)
{
    var field = MixedField();
    var resolution = ResolveSample(
        Sample(filter, wrap), TextureChannel.Alpha, Providing(field));
    var expected = new AlphaSamplingSettings(expectedFilter, expectedWrap);

    foreach (var triangle in new[]
             {
                 OpaqueCornerTriangle(), TransparentCornerTriangle()
             })
    {
        Assert.That(
            resolution.Classify(triangle),
            Is.EqualTo(TriangleAlphaClassifier.Classify(
                triangle, field, expected)));
    }
}

[Test]
public void MissingTextureEvidenceRefuses()
{
    var resolution = ResolveSample(
        Sample(), TextureChannel.Alpha, ProvidingNothing());

    Assert.That(resolution.IsResolved, Is.False);
    Assert.That(
        resolution.Failure,
        Is.EqualTo(AlphaResolutionFailure.MissingTextureEvidence));
}

[TestCase(1, 1f, 1f, 0f, 0f)]        // non-zero UV set
[TestCase(0, 2f, 1f, 0f, 0f)]        // scaled U
[TestCase(0, 1f, 1f, 0.5f, 0f)]      // offset U
[TestCase(0, 1.0000001f, 1f, 0f, 0f)] // one ulp above identity scale
public void UnsupportedUvMappingRefuses(
    int uvChannel, float scaleX, float scaleY, float offsetX, float offsetY)
{
    var resolution = ResolveSample(
        Sample(
            TextureFilterMode.Point,
            TextureWrapMode.Clamp,
            uvChannel,
            scaleX,
            scaleY,
            offsetX,
            offsetY),
        TextureChannel.Alpha,
        Providing(MixedField()));

    Assert.That(
        resolution.Failure,
        Is.EqualTo(AlphaResolutionFailure.UnsupportedUvMapping));
}

[Test]
public void UnsupportedUvMappingIsCheckedBeforeTextureEvidence()
{
    var resolution = ResolveSample(
        Sample(TextureFilterMode.Point, TextureWrapMode.Clamp, 1),
        TextureChannel.Alpha,
        ProvidingNothing());

    Assert.That(
        resolution.Failure,
        Is.EqualTo(AlphaResolutionFailure.UnsupportedUvMapping));
}
```

- [x] **Step 2: Run the focused tests and observe red**

Expected: each new test fails with `NotImplementedException`. Tasks 1–2 stay green.

- [x] **Step 3: Implement the sampled branch**

Add the `TextureSample` arm to the kind switch and add the three helpers below. Check UV mapping first, sampling second, and evidence third.

```csharp
case ScalarSemanticValueKind.TextureSample:
    return ResolveSampled(
        value.GetTextureSample(),
        value.GetChannel(),
        fieldProvider);
```

```csharp
private static AlphaResolution ResolveSampled(
    TextureSample sample,
    TextureChannel channel,
    AlphaFieldProvider fieldProvider)
{
    if (!IsSupportedMapping(sample.Coordinates))
    {
        return AlphaResolution.Refused(
            AlphaResolutionFailure.UnsupportedUvMapping);
    }

    if (!TryMapSampling(sample.Sampling, out var sampling))
    {
        return AlphaResolution.Refused(
            AlphaResolutionFailure.UnsupportedSampling);
    }

    if (!fieldProvider(sample.Source, channel, out var field) ||
        field == null)
    {
        return AlphaResolution.Refused(
            AlphaResolutionFailure.MissingTextureEvidence);
    }

    return AlphaResolution.Classified(field, sampling);
}

/// <summary>
/// The classifier's exact domain is the hull of the UV values it is given;
/// it has no transform input and takes one supplied UV set. Only the
/// identity mapping on UV set 0 can therefore be expressed without either
/// rounding the transform into float or handing a caller an unenforceable
/// obligation about which mesh UV set to supply. Anything else fails closed.
/// Supporting a transform later requires proving with exact dyadic/rational
/// arithmetic that the affine result is representable by the supplied
/// binary32 value; wider floating point is not such a proof.
/// </summary>
private static bool IsSupportedMapping(UvMapping mapping)
{
    return mapping.Channel == 0 &&
           mapping.Scale.x == 1f &&
           mapping.Scale.y == 1f &&
           mapping.Offset.x == 0f &&
           mapping.Offset.y == 0f;
}

/// <summary>
/// Exhaustive translation between the two deliberately separate closed
/// sampling vocabularies. An undefined value is unreachable through the
/// validating semantic constructors; the arm exists so a future semantic
/// mode fails closed instead of falling into a wrong classifier mode.
/// </summary>
private static bool TryMapSampling(
    TextureSampling semantic,
    out AlphaSamplingSettings sampling)
{
    sampling = default;

    AlphaFilterMode filter;
    switch (semantic.Filter)
    {
        case TextureFilterMode.Point:
            filter = AlphaFilterMode.Point;
            break;
        case TextureFilterMode.Bilinear:
            filter = AlphaFilterMode.Bilinear;
            break;
        default:
            return false;
    }

    AlphaWrapMode wrap;
    switch (semantic.Wrap)
    {
        case TextureWrapMode.Clamp:
            wrap = AlphaWrapMode.Clamp;
            break;
        case TextureWrapMode.Repeat:
            wrap = AlphaWrapMode.Repeat;
            break;
        default:
            return false;
    }

    sampling = new AlphaSamplingSettings(filter, wrap);
    return true;
}
```

The file needs `using TextureWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;` only if `UnityEngine` is also imported. Do not import `UnityEngine` in this file. Nothing here needs it.

- [x] **Step 4: Run the focused tests and observe green**

Expected: all Task 1–3 tests pass, including the direct-classifier equivalence assertions.

---

### Task 4: The multiplier lemma for sampled alpha

**Files:**

- Modify: both files from Task 1

**Interfaces:**

- Consumes: `ScalarSemanticValue.TextureTimesConstant`, `ScalarSemanticValueKind.TextureSampleTimesConstant`.
- Produces: no new type. `Resolve` becomes total over the closed vocabulary.

- [x] **Step 1: Write the failing multiplier tests**

```csharp
private static AlphaResolution ResolveMultiplied(
    float multiplier,
    AlphaFieldProvider provider)
{
    return AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(
            ScalarSemanticValue.TextureTimesConstant(
                Sample(), TextureChannel.Alpha, multiplier)),
        provider);
}

[Test]
public void MultiplierOfExactlyOnePreservesTheClassifierPredicate()
{
    var field = MixedField();
    var resolution = ResolveMultiplied(1f, Providing(field));
    var sampling = new AlphaSamplingSettings(
        AlphaFilterMode.Point, AlphaWrapMode.Clamp);

    Assert.That(
        resolution.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaClassifier.Classify(
            OpaqueCornerTriangle(), field, sampling)));
    Assert.That(
        resolution.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
}

[TestCase(0.5f)]
[TestCase(0f)]
[TestCase(-1f)]
[TestCase(0.99999994f)]
public void MultiplierBelowOneIsNeverOpaqueWhateverTheFieldContains(
    float multiplier)
{
    var opaqueField = ResolveMultiplied(multiplier, Providing(Field(2, 2, 255)));
    var emptyField = ResolveMultiplied(multiplier, Providing(Field(2, 2, 0)));

    Assert.That(
        opaqueField.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
    Assert.That(
        emptyField.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
}

[Test]
public void MultiplierBelowOneStillRequiresTheRangeAttestingField()
{
    var resolution = ResolveMultiplied(0.5f, ProvidingNothing());

    Assert.That(resolution.IsResolved, Is.False);
    Assert.That(
        resolution.Failure,
        Is.EqualTo(AlphaResolutionFailure.MissingTextureEvidence));
}

[TestCase(1.0000001f)]
[TestCase(2f)]
public void MultiplierAboveOneIsRefusedWithoutTouchingEvidence(
    float multiplier)
{
    var consulted = false;
    AlphaFieldProvider provider = (TextureSourceId source,
        TextureChannel channel, out AlphaTextureData result) =>
    {
        consulted = true;
        result = MixedField();
        return true;
    };

    var resolution = ResolveMultiplied(multiplier, provider);

    Assert.That(
        resolution.Failure,
        Is.EqualTo(AlphaResolutionFailure.UnsupportedMultiplier));
    Assert.That(consulted, Is.False);
}

[Test]
public void MultiplierBelowOneIgnoresUnsupportedUvMapping()
{
    var resolution = AlphaSemanticsResolver.Resolve(
        SemanticOutput<ScalarSemanticValue>.Complete(
            ScalarSemanticValue.TextureTimesConstant(
                Sample(TextureFilterMode.Point, TextureWrapMode.Clamp, 1),
                TextureChannel.Alpha,
                0.5f)),
        Providing(MixedField()));

    Assert.That(
        resolution.Classify(OpaqueCornerTriangle()),
        Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
}
```

The last test sets a deliberate design decision. With a multiplier below one, the conclusion depends only on the sampled range, not the sample location. Thus, an unsupported UV mapping does not weaken the conclusion. A reviewer preference for refusal is a design amendment, not an implementation choice.

- [x] **Step 2: Run the focused tests and observe red**

Expected: each new test fails with `NotImplementedException`. Tasks 1–3 stay green.

- [x] **Step 3: Implement the multiplier branch**

```csharp
case ScalarSemanticValueKind.TextureSampleTimesConstant:
    return ResolveScaledSample(
        value.GetTextureSample(),
        value.GetChannel(),
        value.GetMultiplier(),
        fieldProvider);
```

```csharp
/// <summary>
/// alpha = s * k, where the field contract bounds the sampled value s to
/// [0, 1] and bilinear filtering, being a convex combination, preserves that
/// bound. k == 1 leaves the classifier's own "s == 1" predicate intact.
/// k &lt; 1 forces alpha &lt;= max(0, k) &lt; 1 at every reachable sample, so the
/// answer needs the field's range attestation but not one byte of its
/// contents. k &gt; 1 would require proving s == 1/k, which the classifier
/// cannot express, and would leave alpha above one, whose opacity meaning the
/// semantic model deliberately does not define.
/// </summary>
private static AlphaResolution ResolveScaledSample(
    TextureSample sample,
    TextureChannel channel,
    float multiplier,
    AlphaFieldProvider fieldProvider)
{
    if (multiplier > 1f)
    {
        return AlphaResolution.Refused(
            AlphaResolutionFailure.UnsupportedMultiplier);
    }

    if (multiplier == 1f)
    {
        return ResolveSampled(sample, channel, fieldProvider);
    }

    if (!fieldProvider(sample.Source, channel, out var field) ||
        field == null)
    {
        return AlphaResolution.Refused(
            AlphaResolutionFailure.MissingTextureEvidence);
    }

    return AlphaResolution.Uniform(
        TriangleAlphaOutcome.MustRemainTransparent);
}
```

Then replace the switch's `default:` arm. It must return `AlphaResolution.Refused(AlphaResolutionFailure.SemanticsUnknown)` instead of throwing. Thus, a semantic form added later fails closed instead of crashing a build. Remove the `NotImplementedException` entirely.

- [x] **Step 4: Run the focused tests and observe green**

Expected: all Task 1–4 tests pass. `Resolve` now has no unimplemented path.

---

### Task 5: Adversarial and pass-through coverage

**Files:**

- Modify: test file only, unless a demonstrated defect requires the minimum production correction

- [x] **Step 1: Write the failing (or immediately passing) adversarial tests**

These tests assert properties that the implementation should already satisfy. A failure is a real defect. Fix production minimally and record the fix.

```csharp
[Test]
public void ClassifierUnknownForDegenerateGeometryPassesThrough()
{
    var resolution = ResolveSample(
        Sample(), TextureChannel.Alpha, Providing(MixedField()));
    var degenerate = TriangleAlphaInput.WithUv0(
        Vector3.zero,
        new Vector3(1f, 1f, 1f),
        new Vector3(2f, 2f, 2f),
        new Vector2(0.05f, 0.05f),
        new Vector2(0.45f, 0.05f),
        new Vector2(0.05f, 0.45f));

    Assert.That(
        resolution.Classify(degenerate),
        Is.EqualTo(TriangleAlphaOutcome.Unknown));
}

[Test]
public void ClassifierUnknownForMissingUvPassesThrough()
{
    var resolution = ResolveSample(
        Sample(), TextureChannel.Alpha, Providing(MixedField()));

    Assert.That(
        resolution.Classify(TriangleAlphaInput.MissingUv0(
            Vector3.zero, Vector3.right, Vector3.up)),
        Is.EqualTo(TriangleAlphaOutcome.Unknown));
}

[Test]
public void ClassifierWorkloadRefusalPassesThroughAsUnknown()
{
    var resolution = ResolveSample(
        Sample(TextureFilterMode.Point, TextureWrapMode.Repeat),
        TextureChannel.Alpha,
        Providing(MixedField()));
    var huge = TriangleAlphaInput.WithUv0(
        Vector3.zero,
        Vector3.right,
        Vector3.up,
        new Vector2(0f, 0f),
        new Vector2(100000f, 0f),
        new Vector2(0f, 100000f));

    Assert.That(
        resolution.Classify(huge),
        Is.EqualTo(TriangleAlphaOutcome.Unknown));
}

[Test]
public void MalformedTriangleStillThrowsThroughTheResolver()
{
    var resolution = ResolveSample(
        Sample(), TextureChannel.Alpha, Providing(MixedField()));

    Assert.Throws<ArgumentException>(() => resolution.Classify(
        TriangleAlphaInput.MissingUv0(
            new Vector3(float.NaN, 0f, 0f), Vector3.right, Vector3.up)));
}

[Test]
public void RepeatedClassificationIsDeterministic()
{
    var resolution = ResolveSample(
        Sample(TextureFilterMode.Bilinear, TextureWrapMode.Repeat),
        TextureChannel.Alpha,
        Providing(MixedField()));

    var first = resolution.Classify(TransparentCornerTriangle());
    for (var attempt = 0; attempt < 5; attempt++)
    {
        Assert.That(
            resolution.Classify(TransparentCornerTriangle()),
            Is.EqualTo(first));
    }
}

[Test]
public void ProviderIsConsultedWithTheSemanticSourceIdentity()
{
    var seen = default(TextureSourceId);
    AlphaFieldProvider provider = (TextureSourceId source,
        TextureChannel channel, out AlphaTextureData result) =>
    {
        seen = source;
        result = MixedField();
        return true;
    };

    ResolveSample(Sample(), TextureChannel.Alpha, provider);

    Assert.That(seen, Is.EqualTo(new TextureSourceId("test:field")));
}

[Test]
public void EveryResolutionKeepsTheResolvedFailureInvariant()
{
    var resolutions = new[]
    {
        AlphaSemanticsResolver.Resolve(
            SemanticOutput<ScalarSemanticValue>.Unknown(),
            ProvidingNothing()),
        ResolveConstant(1f),
        ResolveConstant(0.25f),
        ResolveConstant(3f),
        ResolveSample(Sample(), TextureChannel.Alpha, Providing(MixedField())),
        ResolveSample(Sample(), TextureChannel.Alpha, ProvidingNothing()),
        ResolveMultiplied(0.5f, Providing(MixedField())),
        ResolveMultiplied(2f, Providing(MixedField())),
    };

    foreach (var resolution in resolutions)
    {
        Assert.That(
            resolution.IsResolved,
            Is.EqualTo(
                resolution.Failure == AlphaResolutionFailure.None),
            "A resolution is resolved exactly when it has no failure.");
    }
}

[Test]
public void NoResolutionEverReportsProvenOpaqueWithoutProof()
{
    var refusals = new[]
    {
        AlphaSemanticsResolver.Resolve(
            SemanticOutput<ScalarSemanticValue>.Unknown(),
            ProvidingNothing()),
        ResolveSample(Sample(), TextureChannel.Alpha, ProvidingNothing()),
        ResolveMultiplied(2f, Providing(MixedField())),
        ResolveSample(
            Sample(TextureFilterMode.Point, TextureWrapMode.Clamp, 1),
            TextureChannel.Alpha,
            Providing(MixedField())),
    };

    foreach (var refusal in refusals)
    {
        Assert.That(refusal.IsResolved, Is.False);
        Assert.That(
            refusal.Failure,
            Is.Not.EqualTo(AlphaResolutionFailure.None));
        Assert.Throws<InvalidOperationException>(
            () => refusal.Classify(OpaqueCornerTriangle()));
    }
}
```

- [x] **Step 2: Run the focused tests**

Expected: all tests pass. Investigate each failure as a genuine defect before you change a test. Never relax an assertion to make it pass.

- [x] **Step 3: Confirm the shader-independence boundary by inspection**

Read the finished production file from start to end. Confirm that it names no shader, property, package, version, Unity object type, mesh concept, render mode, or NDMF type. Confirm that it performs no arithmetic on field bytes.

---

### Task 6: Full verification and review handoff

**Files:** none expected

- [x] **Step 1: Run focused and full EditMode validation**

Run the focused resolver class, then run each EditMode test. Record total, passed, failed, skipped, duration, and all Console errors. Expected: zero failures and zero skips. All existing classifier, geometry, planner, fixture, and semantics tests remain unchanged and green.

If no public Unity Editor is available, report the blocked validation and its exact reason. Do not use the private testbed.

- [x] **Step 2: Run static boundary checks**

```bash
rg -n "AssetDatabase|Texture2D|UnityEditor|Material|Mesh|Renderer|Shader|NDMF|nadena|Poiyomi|_MainTex|Debug\." Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs
```

```bash
rg -n "Epsilon|Approximately|Mathf\.|1e-|tolerance" Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs
```

Expected: no matches. Inspect each match instead of using the command as a blind gate.

- [x] **Step 3: Inspect Git and Unity asset scope**

Each file that this milestone adds is **untracked**. Therefore, `git diff --check` and `git diff --stat` inspect nothing. Stage exactly the approved set, inspect the cached diff, and then unstage it. Do not commit.

```bash
git status --short
```

Expected: only the two approved documents and the four planned files are present. The files are `AlphaSemanticsResolver.cs`, `AlphaSemanticsResolver.cs.meta`, `AlphaSemanticsResolverTests.cs`, and `AlphaSemanticsResolverTests.cs.meta`. All six files are untracked. Explain anything else before you continue.

```bash
git add -- docs/superpowers/specs/2026-08-17-alpha-semantics-resolver-design.md docs/superpowers/plans/2026-08-17-alpha-semantics-resolver.md Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs.meta Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs.meta
```

```bash
git diff --cached --check
```

```bash
git diff --cached --stat
```

```bash
git diff --cached
```

Read the full cached diff. Confirm that each new `.meta` has a unique stable GUID paired with its script. Confirm that the classifier, geometry, planner, semantic core, Poiyomi adapter, fixtures, and asmdefs are absent from it. Also confirm that `AssemblyInfo.cs`, package metadata, manifests/locks, workflows, and project settings are absent from it.

`git diff --cached --check` reports trailing whitespace in Unity-generated `.meta` files. This is standard Unity output for this repository. Identify it as such in the report and leave it unchanged. Never "normalize" a `.meta` file to remove the warning.

Then unstage everything and leave the working tree exactly as it was:

```bash
git reset
```

```bash
git status --short
```

Expected: the same six untracked paths, with nothing staged and nothing committed.

- [x] **Step 4: Re-run the closed-vocabulary gate**

Classify each unsupported case observed during execution:

- A: safely deferred as a refusal (expected for every case in this milestone)
- B: a generic extraction/evidence boundary justified by two concrete producers
- C: one small closed classifier or semantic addition with a concrete consumer
- D: expression-graph pressure: a hard stop

Expected: all pressure remains A. Any B, C, or D evidence stops execution for a design amendment.

- [x] **Step 5: Report for review**

Report the branch and base commit. Report the implemented decision table and each refusal code. Report the field-evidence contract as implemented. Include focused and full test results with observed counts. Report all skipped validation and the reason. Report the architectural pressures that execution confirmed or contradicted. Include all changed files and the Git-scope checks. State whether Unity MCP was used and against which project. Report remaining risk, especially the identity-ST coverage limit and the absent evidence producer.

Stop for review. Commit, push, PR, publishing, and settings changes require separate authorization.
