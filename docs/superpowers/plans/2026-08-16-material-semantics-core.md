# Material Semantics Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans by default. Use superpowers:subagent-driven-development only if the user separately authorizes subagents. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the approved minimum immutable normalized material-semantics core without implementing shader adapters or changing the existing alpha and separation contracts.

**Architecture:** One internal Editor-only semantic model describes one resolved effective material state through four independently complete or unknown outputs. Closed typed values support only constants, texture samples, texture-sample-times-constant, and the narrow normal forms justified by the UV-coupling case; later analyzers, adapters, content providers, and transformation evidence remain separate.

**Tech Stack:** Unity 2022.3.22f1, C#, Unity `Vector2`/`Vector3`, NUnit EditMode tests, existing `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor` assemblies.

## Global Constraints

- The approved specification is `docs/superpowers/specs/2026-08-16-material-semantics-core-design.md`.
- Work only on a fresh topic branch based on current `main`; the design branch is `feat/material-semantics-core` based on `dabf36e`.
- Use red/green TDD and observe each requested test result before production changes.
- Keep every new production type `internal` in `Alrauna.Amuse.Editor.Semantics`.
- Keep tests in the existing friend assembly under `Alrauna.Amuse.Tests.Editor.Semantics`.
- Do not add dependencies, assemblies, adapter interfaces, adapter registries/factories, shader names, property names, expression graphs, transformation capabilities, or public APIs.
- Do not modify the classifier, exact geometry, separation planner, existing tests, fixture JSON, asmdefs, package metadata, manifests/locks, workflows, website, project settings, or private testbed.
- Unknown output meaning must never become a default color, scalar, UV channel, sampling mode, normal, or opaque result.
- Use ordinary constructors and static value factories; do not add dependency injection, builders, interning, canonicalization, reflection, or serialization.
- Create and retain Unity `.meta` files only as asset pairs for the new directories and C# files.
- Do not commit, push, open a PR, publish, or change repository settings without separate authorization.

---

## Planned file structure

**Create:**

- `Packages/com.alrauna.amuse/Editor/Semantics.meta` — Unity folder metadata.
- `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs` — the complete v1 semantic vocabulary and immutable resolved-state container.
- `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs.meta` — Unity script metadata.
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics.meta` — Unity test-folder metadata.
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/MaterialSemanticsTests.cs` — direct NUnit contract, thought-experiment, and adversarial tests.
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/MaterialSemanticsTests.cs.meta` — Unity script metadata.

**Do not modify:** existing production, tests, asmdefs, metadata, fixtures, or project/package configuration.

Keeping the vocabulary in one production file is deliberate. Split it only if the approved implementation becomes difficult to review as one coherent contract; a speculative type-per-file tree is out of scope.

---

### Task 1: Texture identity, UV mapping, and sampling primitives

**Files:**

- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/MaterialSemanticsTests.cs`
- Create after red: `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`
- Create with Unity import: the four containing-file/folder `.meta` pairs listed above

**Interfaces:**

- Produces: `TextureSourceId`, `UvMapping`, `TextureFilterMode`, `TextureWrapMode`, and `TextureSampling`.
- Consumers in later tasks use exact structural equality and validated immutable construction.

- [ ] **Step 1: Add failing primitive construction and validation tests**

Create the test namespace and these focused tests:

```csharp
using System;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public sealed class MaterialSemanticsTests
    {
        [Test]
        public void TextureSourceIdentityIsOpaqueOrdinalAndStructural()
        {
            var first = new TextureSourceId("texture:shirt");
            var second = new TextureSourceId("texture:shirt");
            var otherCase = new TextureSourceId("Texture:shirt");

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.EqualTo(otherCase));
            Assert.That(first.Value, Is.EqualTo("texture:shirt"));
        }

        [Test]
        public void UvMappingRecordsUv1ScaleAndOffsetExactly()
        {
            var mapping = new UvMapping(
                1,
                new Vector2(2f, 3f),
                new Vector2(0.25f, -0.5f));

            Assert.That(mapping.Channel, Is.EqualTo(1));
            Assert.That(mapping.Scale, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(mapping.Offset, Is.EqualTo(new Vector2(0.25f, -0.5f)));
        }

        [Test]
        public void SamplingDistinguishesEverySupportedMode()
        {
            var pointClamp = new TextureSampling(
                TextureFilterMode.Point,
                TextureWrapMode.Clamp);
            var bilinearClamp = new TextureSampling(
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var pointRepeat = new TextureSampling(
                TextureFilterMode.Point,
                TextureWrapMode.Repeat);

            Assert.That(pointClamp, Is.Not.EqualTo(bilinearClamp));
            Assert.That(pointClamp, Is.Not.EqualTo(pointRepeat));
        }

        [Test]
        public void PrimitiveMalformedInputsThrow()
        {
            Assert.Throws<ArgumentException>(() => new TextureSourceId(""));
            Assert.Throws<ArgumentException>(() => new TextureSourceId("   "));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UvMapping(-1, Vector2.one, Vector2.zero));
            Assert.Throws<ArgumentException>(() =>
                new UvMapping(0, new Vector2(float.NaN, 1f), Vector2.zero));
            Assert.Throws<ArgumentException>(() =>
                new UvMapping(0, Vector2.one,
                    new Vector2(0f, float.PositiveInfinity)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TextureSampling((TextureFilterMode)99, TextureWrapMode.Clamp));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TextureSampling(TextureFilterMode.Point, (TextureWrapMode)99));
        }
    }
}
```

- [ ] **Step 2: Import and run the focused class to verify red**

Use only the public AMUSE Unity project. Discover `mcpforunity://instances`, select the instance whose root is `E:/AI/Git/AMUSE`, wait for compilation, then run:

```text
mode: EditMode
test_names: Alrauna.Amuse.Tests.Editor.Semantics.MaterialSemanticsTests
include_failed_tests: true
```

Expected red: the test assembly fails to compile because the `Alrauna.Amuse.Editor.Semantics` types do not exist. If no public Unity Editor is running, open the public project through the user's normal Unity workflow; never select the private avatar testbed as a substitute.

- [ ] **Step 3: Implement the minimal primitive API**

Start `MaterialSemantics.cs` with these exact contracts and validation rules:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    internal readonly struct TextureSourceId : IEquatable<TextureSourceId>
    {
        internal string Value { get; }

        internal TextureSourceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Texture source identity must be non-empty.", nameof(value));

            Value = value;
        }

        public bool Equals(TextureSourceId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TextureSourceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }
    }

    internal readonly struct UvMapping : IEquatable<UvMapping>
    {
        internal int Channel { get; }
        internal Vector2 Scale { get; }
        internal Vector2 Offset { get; }

        internal UvMapping(int channel, Vector2 scale, Vector2 offset)
        {
            if (channel < 0)
                throw new ArgumentOutOfRangeException(nameof(channel));
            ValidateFinite(scale, nameof(scale));
            ValidateFinite(offset, nameof(offset));

            Channel = channel;
            Scale = scale;
            Offset = offset;
        }

        public bool Equals(UvMapping other)
        {
            return Channel == other.Channel &&
                   Scale.Equals(other.Scale) &&
                   Offset.Equals(other.Offset);
        }

        public override bool Equals(object obj)
        {
            return obj is UvMapping other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Channel;
                hash = hash * 397 ^ Scale.GetHashCode();
                return hash * 397 ^ Offset.GetHashCode();
            }
        }

        private static void ValidateFinite(Vector2 value, string parameterName)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y))
            {
                throw new ArgumentException(
                    "UV mapping values must be finite.", parameterName);
            }
        }
    }

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

        internal TextureSampling(TextureFilterMode filter, TextureWrapMode wrap)
        {
            if (!Enum.IsDefined(typeof(TextureFilterMode), filter))
                throw new ArgumentOutOfRangeException(nameof(filter));
            if (!Enum.IsDefined(typeof(TextureWrapMode), wrap))
                throw new ArgumentOutOfRangeException(nameof(wrap));

            Filter = filter;
            Wrap = wrap;
        }

        public bool Equals(TextureSampling other)
        {
            return Filter == other.Filter && Wrap == other.Wrap;
        }

        public override bool Equals(object obj)
        {
            return obj is TextureSampling other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Filter * 397) ^ (int)Wrap;
        }
    }
}
```

Do not add implicit conversions to Unity textures, GUID parsing, source registries, per-axis wrap, or a general validation utility.

- [ ] **Step 4: Run the primitive tests green**

Expected: all four current `MaterialSemanticsTests` pass with zero skips. Read Unity Console errors after compilation; expect none related to the new files.

---

### Task 2: Texture samples and closed color/scalar values

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/MaterialSemanticsTests.cs`
- Modify after red: `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`

**Interfaces:**

- Consumes: `TextureSourceId`, `UvMapping`, and `TextureSampling` from Task 1.
- Produces: `TextureSample`, `TextureColorInterpretation`, `TextureChannel`, `ColorSemanticValue`, and `ScalarSemanticValue`.

- [ ] **Step 1: Add failing texture sample and value-form tests**

Add these tests to the existing class:

```csharp
[Test]
public void TextureSampleHasNoUnityObjectOrPixelDependency()
{
    var sample = Sample("texture:shirt", 0,
        TextureFilterMode.Bilinear, TextureWrapMode.Repeat);

    Assert.That(sample.Source, Is.EqualTo(new TextureSourceId("texture:shirt")));
    Assert.That(sample.Coordinates.Channel, Is.Zero);
    Assert.That(sample.Sampling.Filter, Is.EqualTo(TextureFilterMode.Bilinear));
    Assert.That(sample.Sampling.Wrap, Is.EqualTo(TextureWrapMode.Repeat));
}

[Test]
public void ColorSupportsConstantTextureAndTextureTimesConstant()
{
    var sample = Sample("texture:shirt", 0,
        TextureFilterMode.Bilinear, TextureWrapMode.Clamp);
    var constant = ColorSemanticValue.Constant(new Vector3(1f, 0f, 0f));
    var texture = ColorSemanticValue.Texture(
        sample, TextureColorInterpretation.Srgb);
    var multiplied = ColorSemanticValue.TextureTimesConstant(
        sample,
        TextureColorInterpretation.Srgb,
        new Vector3(1f, 0f, 0f));

    Assert.That(constant.Kind, Is.EqualTo(ColorSemanticValueKind.Constant));
    Assert.That(texture.Kind, Is.EqualTo(ColorSemanticValueKind.TextureSample));
    Assert.That(multiplied.Kind,
        Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
    Assert.That(constant.GetConstantValue(), Is.EqualTo(new Vector3(1f, 0f, 0f)));
    Assert.That(texture.GetTextureSample(), Is.SameAs(sample));
    Assert.That(texture.GetColorInterpretation(),
        Is.EqualTo(TextureColorInterpretation.Srgb));
    Assert.That(multiplied.GetMultiplier(),
        Is.EqualTo(new Vector3(1f, 0f, 0f)));
}

[Test]
public void ScalarSupportsConstantTextureAndTextureTimesConstant()
{
    var sample = Sample("texture:alpha", 1,
        TextureFilterMode.Point, TextureWrapMode.Repeat);
    var constant = ScalarSemanticValue.Constant(0.5f);
    var texture = ScalarSemanticValue.Texture(sample, TextureChannel.Alpha);
    var multiplied = ScalarSemanticValue.TextureTimesConstant(
        sample, TextureChannel.Alpha, 0.5f);

    Assert.That(constant.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
    Assert.That(constant.GetConstantValue(), Is.EqualTo(0.5f));
    Assert.That(texture.GetTextureSample(), Is.SameAs(sample));
    Assert.That(texture.GetChannel(), Is.EqualTo(TextureChannel.Alpha));
    Assert.That(multiplied.Kind,
        Is.EqualTo(ScalarSemanticValueKind.TextureSampleTimesConstant));
    Assert.That(multiplied.GetMultiplier(), Is.EqualTo(0.5f));
}

[Test]
public void ColorWrongKindPayloadAccessThrows()
{
    var sample = Sample("texture:color", 0,
        TextureFilterMode.Bilinear, TextureWrapMode.Clamp);
    var constant = ColorSemanticValue.Constant(Vector3.one);
    var texture = ColorSemanticValue.Texture(
        sample, TextureColorInterpretation.Srgb);
    var multiplied = ColorSemanticValue.TextureTimesConstant(
        sample, TextureColorInterpretation.Srgb, Vector3.one);

    Assert.Throws<InvalidOperationException>(() => constant.GetTextureSample());
    Assert.Throws<InvalidOperationException>(() => constant.GetColorInterpretation());
    Assert.Throws<InvalidOperationException>(() => constant.GetMultiplier());
    Assert.Throws<InvalidOperationException>(() => texture.GetConstantValue());
    Assert.Throws<InvalidOperationException>(() => texture.GetMultiplier());
    Assert.Throws<InvalidOperationException>(() => multiplied.GetConstantValue());
}

[Test]
public void ScalarWrongKindPayloadAccessThrows()
{
    var sample = Sample("texture:scalar", 0,
        TextureFilterMode.Point, TextureWrapMode.Repeat);
    var constant = ScalarSemanticValue.Constant(1f);
    var texture = ScalarSemanticValue.Texture(sample, TextureChannel.Alpha);
    var multiplied = ScalarSemanticValue.TextureTimesConstant(
        sample, TextureChannel.Alpha, 0.5f);

    Assert.Throws<InvalidOperationException>(() => constant.GetTextureSample());
    Assert.Throws<InvalidOperationException>(() => constant.GetChannel());
    Assert.Throws<InvalidOperationException>(() => constant.GetMultiplier());
    Assert.Throws<InvalidOperationException>(() => texture.GetConstantValue());
    Assert.Throws<InvalidOperationException>(() => texture.GetMultiplier());
    Assert.Throws<InvalidOperationException>(() => multiplied.GetConstantValue());
}

[Test]
public void IndependentlyConstructedValuesCompareStructurally()
{
    var first = ColorSemanticValue.TextureTimesConstant(
        Sample("texture:shirt", 0,
            TextureFilterMode.Bilinear, TextureWrapMode.Clamp),
        TextureColorInterpretation.Srgb,
        new Vector3(1f, 0.25f, 0.5f));
    var second = ColorSemanticValue.TextureTimesConstant(
        Sample("texture:shirt", 0,
            TextureFilterMode.Bilinear, TextureWrapMode.Clamp),
        TextureColorInterpretation.Srgb,
        new Vector3(1f, 0.25f, 0.5f));

    Assert.That(first, Is.EqualTo(second));
    Assert.That(ReferenceEquals(first, second), Is.False);
}

[Test]
public void SemanticValuesRejectNonFiniteOrUndefinedInputs()
{
    var sample = Sample("texture:value", 0,
        TextureFilterMode.Point, TextureWrapMode.Clamp);

    Assert.Throws<ArgumentException>(() =>
        ColorSemanticValue.Constant(new Vector3(float.NaN, 0f, 0f)));
    Assert.Throws<ArgumentException>(() =>
        ScalarSemanticValue.Constant(float.PositiveInfinity));
    Assert.Throws<ArgumentOutOfRangeException>(() =>
        ColorSemanticValue.Texture(sample, (TextureColorInterpretation)99));
    Assert.Throws<ArgumentOutOfRangeException>(() =>
        ScalarSemanticValue.Texture(sample, (TextureChannel)99));
    Assert.Throws<ArgumentNullException>(() =>
        ColorSemanticValue.Texture(null, TextureColorInterpretation.Linear));
    Assert.Throws<ArgumentException>(() =>
        new TextureSample(
            default,
            new UvMapping(0, Vector2.one, Vector2.zero),
            new TextureSampling(
                TextureFilterMode.Point,
                TextureWrapMode.Clamp)));
}

private static TextureSample Sample(
    string source,
    int uvChannel,
    TextureFilterMode filter,
    TextureWrapMode wrap)
{
    return new TextureSample(
        new TextureSourceId(source),
        new UvMapping(uvChannel, Vector2.one, Vector2.zero),
        new TextureSampling(filter, wrap));
}
```

- [ ] **Step 2: Run the focused class and verify red**

Expected red: missing texture sample and semantic value types. The Task 1 tests remain compiled and green once the missing types are introduced.

- [ ] **Step 3: Implement immutable texture sample and typed closed forms**

Append these exact public surfaces to the production namespace:

```csharp
internal sealed class TextureSample : IEquatable<TextureSample>
{
    internal TextureSourceId Source { get; }
    internal UvMapping Coordinates { get; }
    internal TextureSampling Sampling { get; }

    internal TextureSample(
        TextureSourceId source,
        UvMapping coordinates,
        TextureSampling sampling);

    public bool Equals(TextureSample other);
    public override bool Equals(object obj);
    public override int GetHashCode();
}

internal enum TextureColorInterpretation
{
    Linear,
    Srgb
}

internal enum TextureChannel
{
    Red,
    Green,
    Blue,
    Alpha
}

internal enum ColorSemanticValueKind
{
    Constant,
    TextureSample,
    TextureSampleTimesConstant
}

internal sealed class ColorSemanticValue : IEquatable<ColorSemanticValue>
{
    internal ColorSemanticValueKind Kind { get; }

    internal static ColorSemanticValue Constant(Vector3 value);
    internal static ColorSemanticValue Texture(
        TextureSample sample,
        TextureColorInterpretation interpretation);
    internal static ColorSemanticValue TextureTimesConstant(
        TextureSample sample,
        TextureColorInterpretation interpretation,
        Vector3 multiplier);

    internal Vector3 GetConstantValue();
    internal TextureSample GetTextureSample();
    internal TextureColorInterpretation GetColorInterpretation();
    internal Vector3 GetMultiplier();
}

internal enum ScalarSemanticValueKind
{
    Constant,
    TextureSample,
    TextureSampleTimesConstant
}

internal sealed class ScalarSemanticValue : IEquatable<ScalarSemanticValue>
{
    internal ScalarSemanticValueKind Kind { get; }

    internal static ScalarSemanticValue Constant(float value);
    internal static ScalarSemanticValue Texture(
        TextureSample sample,
        TextureChannel channel);
    internal static ScalarSemanticValue TextureTimesConstant(
        TextureSample sample,
        TextureChannel channel,
        float multiplier);

    internal float GetConstantValue();
    internal TextureSample GetTextureSample();
    internal TextureChannel GetChannel();
    internal float GetMultiplier();
}
```

Implementation rules are exact:

- `TextureSample` rejects an empty/default `TextureSourceId`, assigns its three immutable values, and compares them structurally.
- Each factory stores only the payload meaningful to its kind in private fields; inactive private storage is never exposed as semantic data.
- `Constant` exposes only `GetConstantValue`.
- `Texture` exposes only `GetTextureSample` plus `GetColorInterpretation` or `GetChannel`.
- `TextureTimesConstant` stores the supplied finite multiplier.
- `TextureTimesConstant` exposes the texture accessors plus `GetMultiplier`; it does not expose a constant value.
- `GetTextureSample` and the typed interpretation/channel accessor succeed for both texture kinds; `GetMultiplier` succeeds only for `TextureSampleTimesConstant`.
- Every payload accessor checks `Kind` and throws `InvalidOperationException` when its payload is inactive. A plain texture has no observable identity multiplier, and a constant has no observable sample, channel, or interpretation.
- Every factory taking a sample throws `ArgumentNullException` for null.
- Every enum factory parameter is checked with `Enum.IsDefined`.
- Every scalar or vector constant/multiplier rejects NaN and infinity.
- Equality first compares kind. It then compares only fields meaningful to that kind: constant only; sample plus interpretation/channel; or sample plus interpretation/channel plus multiplier.
- Hash codes use the same meaningful fields as equality. Do not use object identity.

Do not introduce subclasses, visitor interfaces, generic graph nodes, operator overloads, implicit constant folding, or `A * 1 == A` equivalence.

- [ ] **Step 4: Run Tasks 1 and 2 green**

Expected: all current semantic tests pass. Independently allocated equal values compare equal, plain texture and texture-times-one remain different kinds, and every wrong-kind payload access throws rather than exposing an inactive value.

---

### Task 3: Normal forms, per-output knowledge, and resolved material state

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/MaterialSemanticsTests.cs`
- Modify after red: `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`

**Interfaces:**

- Consumes: texture sample and color/scalar values from Task 2.
- Produces: `NormalSemanticValue`, `SemanticOutput<T>`, and `MaterialSemantics`.

- [ ] **Step 1: Add failing normal, unknown, partial-knowledge, and equality tests**

```csharp
[Test]
public void NormalSupportsOnlyUnmodifiedAndCanonicalTangentMap()
{
    var unmodified = NormalSemanticValue.Unmodified();
    var mapped = NormalSemanticValue.TangentSpaceNormalMap(
        Sample("texture:normal", 0,
            TextureFilterMode.Bilinear, TextureWrapMode.Clamp));

    Assert.That(unmodified.Kind,
        Is.EqualTo(NormalSemanticValueKind.Unmodified));
    Assert.That(mapped.Kind,
        Is.EqualTo(NormalSemanticValueKind.TangentSpaceNormalMap));
    Assert.Throws<InvalidOperationException>(() =>
        unmodified.GetTextureSample());
    Assert.That(mapped.GetTextureSample(), Is.Not.Null);
}

[Test]
public void ExplicitUnknownOutputCannotBeReadAsAValue()
{
    var unknown = SemanticOutput<ScalarSemanticValue>.Unknown();

    Assert.That(unknown.IsComplete, Is.False);
    Assert.Throws<InvalidOperationException>(() => unknown.GetCompleteValue());
}

[Test]
public void DefaultSemanticOutputIsConservativelyUnknown()
{
    var output = default(SemanticOutput<ScalarSemanticValue>);

    Assert.That(output.IsComplete, Is.False);
    Assert.Throws<InvalidOperationException>(() => output.GetCompleteValue());
    Assert.That(output,
        Is.EqualTo(SemanticOutput<ScalarSemanticValue>.Unknown()));
}

[Test]
public void MaterialSupportsIndependentPartialKnowledge()
{
    var semantics = new MaterialSemantics(
        SemanticOutput<ColorSemanticValue>.Complete(
            ColorSemanticValue.Constant(Vector3.one)),
        SemanticOutput<ScalarSemanticValue>.Complete(
            ScalarSemanticValue.Constant(1f)),
        SemanticOutput<ColorSemanticValue>.Unknown(),
        SemanticOutput<NormalSemanticValue>.Complete(
            NormalSemanticValue.Unmodified()));

    Assert.That(semantics.BaseColor.IsComplete, Is.True);
    Assert.That(semantics.Alpha.IsComplete, Is.True);
    Assert.That(semantics.Emission.IsComplete, Is.False);
    Assert.That(semantics.Normal.IsComplete, Is.True);
}

[Test]
public void CompleteOutputRejectsNullReferenceValue()
{
    Assert.Throws<ArgumentNullException>(() =>
        SemanticOutput<ColorSemanticValue>.Complete(null));
}

[Test]
public void ResolvedStatesUseStructuralNotObjectEquality()
{
    var first = ConstantMaterial(0.5f);
    var same = ConstantMaterial(0.5f);
    var animatedAlternate = ConstantMaterial(0.25f);

    Assert.That(first, Is.EqualTo(same));
    Assert.That(ReferenceEquals(first, same), Is.False);
    Assert.That(first, Is.Not.EqualTo(animatedAlternate));
}

private static MaterialSemantics ConstantMaterial(float alpha)
{
    return new MaterialSemantics(
        SemanticOutput<ColorSemanticValue>.Complete(
            ColorSemanticValue.Constant(Vector3.one)),
        SemanticOutput<ScalarSemanticValue>.Complete(
            ScalarSemanticValue.Constant(alpha)),
        SemanticOutput<ColorSemanticValue>.Complete(
            ColorSemanticValue.Constant(Vector3.zero)),
        SemanticOutput<NormalSemanticValue>.Complete(
            NormalSemanticValue.Unmodified()));
}
```

- [ ] **Step 2: Run the focused class and verify red**

Expected red: missing normal, output wrapper, and material container types. Earlier primitive/value tests remain valid.

- [ ] **Step 3: Implement the narrow normal and knowledge contracts**

Append these surfaces:

```csharp
internal enum NormalSemanticValueKind
{
    Unmodified,
    TangentSpaceNormalMap
}

internal sealed class NormalSemanticValue : IEquatable<NormalSemanticValue>
{
    internal NormalSemanticValueKind Kind { get; }

    internal static NormalSemanticValue Unmodified();
    internal static NormalSemanticValue TangentSpaceNormalMap(
        TextureSample sample);

    internal TextureSample GetTextureSample();
}

internal readonly struct SemanticOutput<T> : IEquatable<SemanticOutput<T>>
    where T : class
{
    private readonly T _value;

    internal bool IsComplete { get; }

    private SemanticOutput(bool isComplete, T value)
    {
        IsComplete = isComplete;
        _value = value;
    }

    internal static SemanticOutput<T> Complete(T value)
    {
        if (ReferenceEquals(value, null))
            throw new ArgumentNullException(nameof(value));

        return new SemanticOutput<T>(true, value);
    }

    internal static SemanticOutput<T> Unknown()
    {
        return new SemanticOutput<T>(false, default);
    }

    internal T GetCompleteValue()
    {
        if (!IsComplete)
            throw new InvalidOperationException("Semantic output is unknown.");

        return _value;
    }

    public bool Equals(SemanticOutput<T> other)
    {
        return IsComplete == other.IsComplete &&
               (!IsComplete || EqualityComparer<T>.Default.Equals(_value, other._value));
    }

    public override bool Equals(object obj)
    {
        return obj is SemanticOutput<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return !IsComplete || ReferenceEquals(_value, null)
            ? 0
            : EqualityComparer<T>.Default.GetHashCode(_value);
    }
}

internal sealed class MaterialSemantics : IEquatable<MaterialSemantics>
{
    internal SemanticOutput<ColorSemanticValue> BaseColor { get; }
    internal SemanticOutput<ScalarSemanticValue> Alpha { get; }
    internal SemanticOutput<ColorSemanticValue> Emission { get; }
    internal SemanticOutput<NormalSemanticValue> Normal { get; }

    internal MaterialSemantics(
        SemanticOutput<ColorSemanticValue> baseColor,
        SemanticOutput<ScalarSemanticValue> alpha,
        SemanticOutput<ColorSemanticValue> emission,
        SemanticOutput<NormalSemanticValue> normal)
    {
        BaseColor = baseColor;
        Alpha = alpha;
        Emission = emission;
        Normal = normal;
    }

    public bool Equals(MaterialSemantics other)
    {
        return other != null &&
               BaseColor.Equals(other.BaseColor) &&
               Alpha.Equals(other.Alpha) &&
               Emission.Equals(other.Emission) &&
               Normal.Equals(other.Normal);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as MaterialSemantics);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = BaseColor.GetHashCode();
            hash = hash * 397 ^ Alpha.GetHashCode();
            hash = hash * 397 ^ Emission.GetHashCode();
            return hash * 397 ^ Normal.GetHashCode();
        }
    }
}
```

Implement `NormalSemanticValue` as a two-factory immutable value. `Unmodified` means the incoming host-provided surface/shading normal is not perturbed by the material and exposes no sample; `GetTextureSample` throws `InvalidOperationException` for that kind. `TangentSpaceNormalMap` rejects null and returns its sample through the accessor. Equality compares kind and compares the sample only for the mapped kind. Do not add normal strength, inversion, encoding enums, or composition.

- [ ] **Step 4: Run Tasks 1 through 3 green**

Expected: all semantic tests pass. Explicit and default unknown retrieval throw, unmodified Normal exposes no sample, partial output knowledge remains independent, and two independently constructed resolved states compare structurally.

---

### Task 4: Required thought experiments and adversarial boundary tests

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/MaterialSemanticsTests.cs`
- Modify only for a demonstrated contract defect: `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`

**Interfaces:** No new production API. These tests prove the approved vocabulary is sufficient and no shader/host or transformation policy leaked into it.

- [ ] **Step 1: Add the bakeable-tint representation test**

```csharp
[Test]
public void TintDifferenceIsRepresentedWithoutABakingDecision()
{
    var shirt = ColorSemanticValue.TextureTimesConstant(
        Sample("texture:shirt", 0,
            TextureFilterMode.Bilinear, TextureWrapMode.Clamp),
        TextureColorInterpretation.Srgb,
        new Vector3(1f, 0f, 0f));
    var pants = ColorSemanticValue.TextureTimesConstant(
        Sample("texture:pants", 0,
            TextureFilterMode.Bilinear, TextureWrapMode.Clamp),
        TextureColorInterpretation.Srgb,
        new Vector3(0f, 0f, 1f));

    Assert.That(shirt.Kind,
        Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
    Assert.That(pants.Kind,
        Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
    Assert.That(shirt.GetTextureSample().Source,
        Is.EqualTo(new TextureSourceId("texture:shirt")));
    Assert.That(pants.GetTextureSample().Source,
        Is.EqualTo(new TextureSourceId("texture:pants")));
    Assert.That(shirt, Is.Not.EqualTo(pants));
}
```

The production type must not gain `CanBake`, `CanCombineWith`, or a capability annotation to make this test pass.

- [ ] **Step 2: Add the alpha resolver-boundary representation test**

```csharp
[Test]
public void TextureAlphaTimesOpacityExposesFutureResolverInputs()
{
    var alpha = ScalarSemanticValue.TextureTimesConstant(
        new TextureSample(
            new TextureSourceId("texture:main"),
            new UvMapping(
                0,
                new Vector2(2f, 2f),
                new Vector2(0.25f, -0.5f)),
            new TextureSampling(
                TextureFilterMode.Point,
                TextureWrapMode.Repeat)),
        TextureChannel.Alpha,
        0.75f);

    Assert.That(alpha.Kind,
        Is.EqualTo(ScalarSemanticValueKind.TextureSampleTimesConstant));
    Assert.That(alpha.GetChannel(), Is.EqualTo(TextureChannel.Alpha));
    Assert.That(alpha.GetMultiplier(), Is.EqualTo(0.75f));
    Assert.That(alpha.GetTextureSample().Source,
        Is.EqualTo(new TextureSourceId("texture:main")));
    Assert.That(alpha.GetTextureSample().Coordinates.Channel, Is.Zero);
    Assert.That(alpha.GetTextureSample().Sampling.Filter,
        Is.EqualTo(TextureFilterMode.Point));
    Assert.That(alpha.GetTextureSample().Sampling.Wrap,
        Is.EqualTo(TextureWrapMode.Repeat));
}
```

Do not call or modify `TriangleAlphaClassifier`. The test proves representation only.

- [ ] **Step 3: Add shared/different UV coupling tests including Normal**

```csharp
[Test]
public void TextureOutputsExposeSharedUvCoupling()
{
    var coordinates = new UvMapping(
        0,
        new Vector2(2f, 2f),
        new Vector2(0.1f, 0.2f));
    var sampling = new TextureSampling(
        TextureFilterMode.Bilinear,
        TextureWrapMode.Clamp);
    var baseSample = new TextureSample(
        new TextureSourceId("texture:base"), coordinates, sampling);
    var emissionSample = new TextureSample(
        new TextureSourceId("texture:emission"), coordinates, sampling);
    var normalSample = new TextureSample(
        new TextureSourceId("texture:normal"), coordinates, sampling);

    var semantics = new MaterialSemantics(
        SemanticOutput<ColorSemanticValue>.Complete(
            ColorSemanticValue.Texture(
                baseSample, TextureColorInterpretation.Srgb)),
        SemanticOutput<ScalarSemanticValue>.Complete(
            ScalarSemanticValue.Constant(1f)),
        SemanticOutput<ColorSemanticValue>.Complete(
            ColorSemanticValue.Texture(
                emissionSample, TextureColorInterpretation.Srgb)),
        SemanticOutput<NormalSemanticValue>.Complete(
            NormalSemanticValue.TangentSpaceNormalMap(normalSample)));

    Assert.That(
        semantics.BaseColor.GetCompleteValue().GetTextureSample().Coordinates,
        Is.EqualTo(semantics.Emission.GetCompleteValue()
            .GetTextureSample().Coordinates));
    Assert.That(
        semantics.BaseColor.GetCompleteValue().GetTextureSample().Coordinates,
        Is.EqualTo(semantics.Normal.GetCompleteValue()
            .GetTextureSample().Coordinates));
}

[Test]
public void UvChannelOrTransformDifferenceBreaksV1CouplingEquality()
{
    var uv0 = new UvMapping(0, Vector2.one, Vector2.zero);
    var uv1 = new UvMapping(1, Vector2.one, Vector2.zero);
    var offset = new UvMapping(0, Vector2.one, new Vector2(0.5f, 0f));

    Assert.That(uv0, Is.Not.EqualTo(uv1));
    Assert.That(uv0, Is.Not.EqualTo(offset));
}
```

- [ ] **Step 4: Add missing, unknown-modifier, and sampling adversarial tests**

```csharp
[Test]
public void MissingTextureIsAProvenFallbackOrUnknownNeverNull()
{
    var provenFallback = SemanticOutput<ColorSemanticValue>.Complete(
        ColorSemanticValue.Constant(Vector3.one));
    var unsupportedMissingBehavior =
        SemanticOutput<ColorSemanticValue>.Unknown();

    Assert.That(provenFallback.GetCompleteValue().Kind,
        Is.EqualTo(ColorSemanticValueKind.Constant));
    Assert.Throws<InvalidOperationException>(() =>
        unsupportedMissingBehavior.GetCompleteValue());
}

[Test]
public void UnknownModifierCanInvalidateOnlyProvenAffectedOutput()
{
    var semantics = new MaterialSemantics(
        SemanticOutput<ColorSemanticValue>.Complete(
            ColorSemanticValue.Constant(Vector3.one)),
        SemanticOutput<ScalarSemanticValue>.Unknown(),
        SemanticOutput<ColorSemanticValue>.Complete(
            ColorSemanticValue.Constant(Vector3.zero)),
        SemanticOutput<NormalSemanticValue>.Complete(
            NormalSemanticValue.Unmodified()));

    Assert.That(semantics.BaseColor.IsComplete, Is.True);
    Assert.That(semantics.Alpha.IsComplete, Is.False);
    Assert.That(semantics.Emission.IsComplete, Is.True);
    Assert.That(semantics.Normal.IsComplete, Is.True);
}

[Test]
public void FilterAndWrapDifferencesRemainObservable()
{
    var pointClamp = Sample("texture:x", 0,
        TextureFilterMode.Point, TextureWrapMode.Clamp);
    var bilinearClamp = Sample("texture:x", 0,
        TextureFilterMode.Bilinear, TextureWrapMode.Clamp);
    var pointRepeat = Sample("texture:x", 0,
        TextureFilterMode.Point, TextureWrapMode.Repeat);

    Assert.That(pointClamp, Is.Not.EqualTo(bilinearClamp));
    Assert.That(pointClamp, Is.Not.EqualTo(pointRepeat));
}
```

- [ ] **Step 5: Run the full semantic class green**

Expected: every `MaterialSemanticsTests` case passes with zero failures/skips. If any test demonstrates the approved API cannot represent the case, stop and return to the design approval gate instead of adding a broader expression or capability system.

---

### Task 5: Contract isolation and full repository validation

**Files:**

- Modify only for demonstrated defects: the two new semantic C# files.
- Do not change approved design semantics silently.

**Interfaces:** No new API. This task proves that the milestone remained a semantic language only and preserved existing contracts.

- [ ] **Step 1: Run the complete EditMode suite**

Use the verified public `E:/AI/Git/AMUSE` Unity instance and run all EditMode tests. Record total, passed, failed, skipped, and duration. Expected: zero failures and no unexpected Console errors/warnings. This includes unchanged classifier, exact geometry, fixture integrity, separation planner, smoke tests, and the new semantic tests.

- [ ] **Step 2: Verify forbidden coupling is absent**

Run:

```powershell
rg -n "UnityEngine\.(Material|Texture|Texture2D|Renderer)|AssetDatabase|GameObject|NDMF|Poiyomi|lilToon|_MainTex|_Color|CanBake|CanCombine|BakeableIntoTexture|Expression|DAG|Registry|Adapter" Packages/com.alrauna.amuse/Editor/Semantics Packages/com.alrauna.amuse/Tests/Editor/Semantics
```

Expected: no matches. `using UnityEngine;` is allowed only for `Vector2` and `Vector3`.

Run:

```powershell
rg -n "Vector2|Vector3|TextureSourceId|UvMapping|TextureSampling|SemanticOutput|MaterialSemantics" Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs
```

Expected: the approved immutable value boundary is present.

- [ ] **Step 3: Prove existing alpha and separation files are unchanged**

Run:

```powershell
git diff --name-only main...HEAD -- Packages/com.alrauna.amuse/Editor/Analysis Packages/com.alrauna.amuse/Tests/Editor/Analysis Packages/com.alrauna.amuse/Tests/Editor/ReferenceFixtures
```

Expected: no output.

Then run the existing focused EditMode fixtures explicitly:

```text
Alrauna.Amuse.Tests.Editor.Analysis.TriangleAlphaClassifierTests
Alrauna.Amuse.Tests.Editor.Analysis.MeshSeparationPlannerTests
Alrauna.Amuse.Tests.Editor.ReferenceFixtures.ReferenceFixtureIntegrityTests
```

Expected: zero failures. Record actual counts rather than copying historical totals.

- [ ] **Step 4: Inspect Unity asset integrity and repository scope**

Run:

```powershell
git diff --check
git status --short
git diff --stat
git diff
git diff --cached --stat
git diff --cached
git diff --name-only main...HEAD
```

Confirm only the approved design/plan documents plus the two new semantic C# files and their four directory/file `.meta` pairs changed. Inspect each `.meta` for a unique stable GUID and correct asset pairing. Confirm asmdefs, package metadata, manifests/locks, workflows, project settings, website assets, and existing analysis/tests are unchanged.

- [ ] **Step 5: Review the approved specification line by line**

Record direct evidence for:

- exact outputs: BaseColor, Alpha, Emission, Normal;
- exact color/scalar forms and narrow normal forms;
- no default retrieval from unknown outputs;
- inactive variant payloads hidden behind kind-checked accessors, with wrong-kind access throwing;
- conservative `default(SemanticOutput<T>)` behavior;
- per-output partial knowledge;
- opaque source identity with no Unity object or pixels;
- UV0/UV1, scale, offset, filter, wrap, channel, and color interpretation;
- structural equality and independent resolved states;
- no transformation capabilities or optimization methods;
- no shader-adapter or modifier framework;
- no classifier/planner changes;
- every adversarial case in the approved specification;
- observed tests and skipped validation;
- public Unity MCP use and confirmation that the private testbed was not selected or modified.

- [ ] **Step 6: Leave publication actions gated**

Do not stage or commit unless the user separately authorizes it. If authorization is later given, stage only the reviewed semantic files and their `.meta` pairs; keep the already reviewed design/plan documents in the same branch only if the user includes them in the authorized scope. Suggested coherent commit messages are:

```text
test: specify normalized material semantics
feat: add normalized material semantics core
```

Do not push or open a PR without separate authorization.

## Approval and execution gate

This plan is documentation only. Stop until the user explicitly approves both this plan and `docs/superpowers/specs/2026-08-16-material-semantics-core-design.md`.

After approval, use `superpowers:executing-plans` by default, together with `superpowers:test-driven-development`, `ponytail:ponytail`, and `superpowers:verification-before-completion`. Use subagent-driven development only if the user separately authorizes subagents.
