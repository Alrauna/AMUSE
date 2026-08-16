using System;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;
using TextureWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;

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
                new TextureSampling(
                    (TextureFilterMode)99,
                    TextureWrapMode.Clamp));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new TextureSampling(
                    TextureFilterMode.Point,
                    (TextureWrapMode)99));
        }

        [Test]
        public void TextureSampleHasNoUnityObjectOrPixelDependency()
        {
            var sample = Sample(
                "texture:shirt",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Repeat);

            Assert.That(
                sample.Source,
                Is.EqualTo(new TextureSourceId("texture:shirt")));
            Assert.That(sample.Coordinates.Channel, Is.Zero);
            Assert.That(
                sample.Sampling.Filter,
                Is.EqualTo(TextureFilterMode.Bilinear));
            Assert.That(
                sample.Sampling.Wrap,
                Is.EqualTo(TextureWrapMode.Repeat));
        }

        [Test]
        public void ColorSupportsConstantTextureAndTextureTimesConstant()
        {
            var sample = Sample(
                "texture:shirt",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var constant = ColorSemanticValue.Constant(
                new Vector3(1f, 0f, 0f));
            var texture = ColorSemanticValue.Texture(
                sample,
                TextureColorInterpretation.Srgb);
            var multiplied = ColorSemanticValue.TextureTimesConstant(
                sample,
                TextureColorInterpretation.Srgb,
                new Vector3(1f, 0f, 0f));

            Assert.That(
                constant.Kind,
                Is.EqualTo(ColorSemanticValueKind.Constant));
            Assert.That(
                texture.Kind,
                Is.EqualTo(ColorSemanticValueKind.TextureSample));
            Assert.That(
                multiplied.Kind,
                Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
            Assert.That(
                constant.GetConstantValue(),
                Is.EqualTo(new Vector3(1f, 0f, 0f)));
            Assert.That(texture.GetTextureSample(), Is.SameAs(sample));
            Assert.That(
                texture.GetColorInterpretation(),
                Is.EqualTo(TextureColorInterpretation.Srgb));
            Assert.That(
                multiplied.GetMultiplier(),
                Is.EqualTo(new Vector3(1f, 0f, 0f)));
        }

        [Test]
        public void ScalarSupportsConstantTextureAndTextureTimesConstant()
        {
            var sample = Sample(
                "texture:alpha",
                1,
                TextureFilterMode.Point,
                TextureWrapMode.Repeat);
            var constant = ScalarSemanticValue.Constant(0.5f);
            var texture = ScalarSemanticValue.Texture(
                sample,
                TextureChannel.Alpha);
            var multiplied = ScalarSemanticValue.TextureTimesConstant(
                sample,
                TextureChannel.Alpha,
                0.5f);

            Assert.That(
                constant.Kind,
                Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(constant.GetConstantValue(), Is.EqualTo(0.5f));
            Assert.That(texture.GetTextureSample(), Is.SameAs(sample));
            Assert.That(texture.GetChannel(), Is.EqualTo(TextureChannel.Alpha));
            Assert.That(
                multiplied.Kind,
                Is.EqualTo(ScalarSemanticValueKind.TextureSampleTimesConstant));
            Assert.That(multiplied.GetMultiplier(), Is.EqualTo(0.5f));
        }

        [Test]
        public void ColorWrongKindPayloadAccessThrows()
        {
            var sample = Sample(
                "texture:color",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var constant = ColorSemanticValue.Constant(Vector3.one);
            var texture = ColorSemanticValue.Texture(
                sample,
                TextureColorInterpretation.Srgb);
            var multiplied = ColorSemanticValue.TextureTimesConstant(
                sample,
                TextureColorInterpretation.Srgb,
                Vector3.one);

            Assert.Throws<InvalidOperationException>(
                () => constant.GetTextureSample());
            Assert.Throws<InvalidOperationException>(
                () => constant.GetColorInterpretation());
            Assert.Throws<InvalidOperationException>(
                () => constant.GetMultiplier());
            Assert.Throws<InvalidOperationException>(
                () => texture.GetConstantValue());
            Assert.Throws<InvalidOperationException>(
                () => texture.GetMultiplier());
            Assert.Throws<InvalidOperationException>(
                () => multiplied.GetConstantValue());
        }

        [Test]
        public void ScalarWrongKindPayloadAccessThrows()
        {
            var sample = Sample(
                "texture:scalar",
                0,
                TextureFilterMode.Point,
                TextureWrapMode.Repeat);
            var constant = ScalarSemanticValue.Constant(1f);
            var texture = ScalarSemanticValue.Texture(
                sample,
                TextureChannel.Alpha);
            var multiplied = ScalarSemanticValue.TextureTimesConstant(
                sample,
                TextureChannel.Alpha,
                0.5f);

            Assert.Throws<InvalidOperationException>(
                () => constant.GetTextureSample());
            Assert.Throws<InvalidOperationException>(
                () => constant.GetChannel());
            Assert.Throws<InvalidOperationException>(
                () => constant.GetMultiplier());
            Assert.Throws<InvalidOperationException>(
                () => texture.GetConstantValue());
            Assert.Throws<InvalidOperationException>(
                () => texture.GetMultiplier());
            Assert.Throws<InvalidOperationException>(
                () => multiplied.GetConstantValue());
        }

        [Test]
        public void IndependentlyConstructedValuesCompareStructurally()
        {
            var first = ColorSemanticValue.TextureTimesConstant(
                Sample(
                    "texture:shirt",
                    0,
                    TextureFilterMode.Bilinear,
                    TextureWrapMode.Clamp),
                TextureColorInterpretation.Srgb,
                new Vector3(1f, 0.25f, 0.5f));
            var second = ColorSemanticValue.TextureTimesConstant(
                Sample(
                    "texture:shirt",
                    0,
                    TextureFilterMode.Bilinear,
                    TextureWrapMode.Clamp),
                TextureColorInterpretation.Srgb,
                new Vector3(1f, 0.25f, 0.5f));

            Assert.That(first, Is.EqualTo(second));
            Assert.That(ReferenceEquals(first, second), Is.False);
        }

        [Test]
        public void SemanticValuesRejectNonFiniteOrUndefinedInputs()
        {
            var sample = Sample(
                "texture:value",
                0,
                TextureFilterMode.Point,
                TextureWrapMode.Clamp);

            Assert.Throws<ArgumentException>(() =>
                ColorSemanticValue.Constant(
                    new Vector3(float.NaN, 0f, 0f)));
            Assert.Throws<ArgumentException>(() =>
                ScalarSemanticValue.Constant(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ColorSemanticValue.Texture(
                    sample,
                    (TextureColorInterpretation)99));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ScalarSemanticValue.Texture(
                    sample,
                    (TextureChannel)99));
            Assert.Throws<ArgumentNullException>(() =>
                ColorSemanticValue.Texture(
                    null,
                    TextureColorInterpretation.Linear));
            Assert.Throws<ArgumentException>(() =>
                new TextureSample(
                    default,
                    new UvMapping(0, Vector2.one, Vector2.zero),
                    new TextureSampling(
                        TextureFilterMode.Point,
                        TextureWrapMode.Clamp)));
        }

        [Test]
        public void NormalSupportsOnlyUnmodifiedAndCanonicalTangentMap()
        {
            var unmodified = NormalSemanticValue.Unmodified();
            var mapped = NormalSemanticValue.TangentSpaceNormalMap(
                Sample(
                    "texture:normal",
                    0,
                    TextureFilterMode.Bilinear,
                    TextureWrapMode.Clamp));

            Assert.That(
                unmodified.Kind,
                Is.EqualTo(NormalSemanticValueKind.Unmodified));
            Assert.That(
                mapped.Kind,
                Is.EqualTo(NormalSemanticValueKind.TangentSpaceNormalMap));
            Assert.Throws<InvalidOperationException>(
                () => unmodified.GetTextureSample());
            Assert.That(mapped.GetTextureSample(), Is.Not.Null);
        }

        [Test]
        public void ExplicitUnknownOutputCannotBeReadAsAValue()
        {
            var unknown = SemanticOutput<ScalarSemanticValue>.Unknown();

            Assert.That(unknown.IsComplete, Is.False);
            Assert.Throws<InvalidOperationException>(
                () => unknown.GetCompleteValue());
        }

        [Test]
        public void DefaultSemanticOutputIsConservativelyUnknown()
        {
            var output = default(SemanticOutput<ScalarSemanticValue>);

            Assert.That(output.IsComplete, Is.False);
            Assert.Throws<InvalidOperationException>(
                () => output.GetCompleteValue());
            Assert.That(
                output,
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

        [Test]
        public void TintDifferenceIsRepresentedWithoutABakingDecision()
        {
            var shirt = ColorSemanticValue.TextureTimesConstant(
                Sample(
                    "texture:shirt",
                    0,
                    TextureFilterMode.Bilinear,
                    TextureWrapMode.Clamp),
                TextureColorInterpretation.Srgb,
                new Vector3(1f, 0f, 0f));
            var pants = ColorSemanticValue.TextureTimesConstant(
                Sample(
                    "texture:pants",
                    0,
                    TextureFilterMode.Bilinear,
                    TextureWrapMode.Clamp),
                TextureColorInterpretation.Srgb,
                new Vector3(0f, 0f, 1f));

            Assert.That(
                shirt.Kind,
                Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
            Assert.That(
                pants.Kind,
                Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
            Assert.That(
                shirt.GetTextureSample().Source,
                Is.EqualTo(new TextureSourceId("texture:shirt")));
            Assert.That(
                pants.GetTextureSample().Source,
                Is.EqualTo(new TextureSourceId("texture:pants")));
            Assert.That(shirt, Is.Not.EqualTo(pants));
        }

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

            Assert.That(
                alpha.Kind,
                Is.EqualTo(ScalarSemanticValueKind.TextureSampleTimesConstant));
            Assert.That(alpha.GetChannel(), Is.EqualTo(TextureChannel.Alpha));
            Assert.That(alpha.GetMultiplier(), Is.EqualTo(0.75f));
            Assert.That(
                alpha.GetTextureSample().Source,
                Is.EqualTo(new TextureSourceId("texture:main")));
            Assert.That(alpha.GetTextureSample().Coordinates.Channel, Is.Zero);
            Assert.That(
                alpha.GetTextureSample().Sampling.Filter,
                Is.EqualTo(TextureFilterMode.Point));
            Assert.That(
                alpha.GetTextureSample().Sampling.Wrap,
                Is.EqualTo(TextureWrapMode.Repeat));
        }

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
                new TextureSourceId("texture:base"),
                coordinates,
                sampling);
            var emissionSample = new TextureSample(
                new TextureSourceId("texture:emission"),
                coordinates,
                sampling);
            var normalSample = new TextureSample(
                new TextureSourceId("texture:normal"),
                coordinates,
                sampling);

            var semantics = new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Complete(
                    ColorSemanticValue.Texture(
                        baseSample,
                        TextureColorInterpretation.Srgb)),
                SemanticOutput<ScalarSemanticValue>.Complete(
                    ScalarSemanticValue.Constant(1f)),
                SemanticOutput<ColorSemanticValue>.Complete(
                    ColorSemanticValue.Texture(
                        emissionSample,
                        TextureColorInterpretation.Srgb)),
                SemanticOutput<NormalSemanticValue>.Complete(
                    NormalSemanticValue.TangentSpaceNormalMap(normalSample)));

            Assert.That(
                semantics.BaseColor.GetCompleteValue()
                    .GetTextureSample().Coordinates,
                Is.EqualTo(semantics.Emission.GetCompleteValue()
                    .GetTextureSample().Coordinates));
            Assert.That(
                semantics.BaseColor.GetCompleteValue()
                    .GetTextureSample().Coordinates,
                Is.EqualTo(semantics.Normal.GetCompleteValue()
                    .GetTextureSample().Coordinates));
        }

        [Test]
        public void UvChannelOrTransformDifferenceBreaksV1CouplingEquality()
        {
            var uv0 = new UvMapping(0, Vector2.one, Vector2.zero);
            var uv1 = new UvMapping(1, Vector2.one, Vector2.zero);
            var offset = new UvMapping(
                0,
                Vector2.one,
                new Vector2(0.5f, 0f));

            Assert.That(uv0, Is.Not.EqualTo(uv1));
            Assert.That(uv0, Is.Not.EqualTo(offset));
        }

        [Test]
        public void MissingTextureIsAProvenFallbackOrUnknownNeverNull()
        {
            var provenFallback = SemanticOutput<ColorSemanticValue>.Complete(
                ColorSemanticValue.Constant(Vector3.one));
            var unsupportedMissingBehavior =
                SemanticOutput<ColorSemanticValue>.Unknown();

            Assert.That(
                provenFallback.GetCompleteValue().Kind,
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
            var pointClamp = Sample(
                "texture:x",
                0,
                TextureFilterMode.Point,
                TextureWrapMode.Clamp);
            var bilinearClamp = Sample(
                "texture:x",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var pointRepeat = Sample(
                "texture:x",
                0,
                TextureFilterMode.Point,
                TextureWrapMode.Repeat);

            Assert.That(pointClamp, Is.Not.EqualTo(bilinearClamp));
            Assert.That(pointClamp, Is.Not.EqualTo(pointRepeat));
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
    }
}
