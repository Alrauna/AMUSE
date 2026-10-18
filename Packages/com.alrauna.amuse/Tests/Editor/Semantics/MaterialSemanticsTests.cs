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
        public void SamplingDefaultsToForceAnisotropyEligible()
        {
            var twoArgument = new TextureSampling(
                TextureFilterMode.Point,
                TextureWrapMode.Clamp);
            var threeArgument = new TextureSampling(
                TextureFilterMode.Point,
                TextureWrapMode.Clamp,
                TextureAnisoMode.None);
            var explicitEligible = new TextureSampling(
                TextureFilterMode.Point,
                TextureWrapMode.Clamp,
                TextureAnisoMode.None,
                forceAnisotropyEligible: true);

            Assert.That(twoArgument.ForceAnisotropyEligible, Is.True);
            Assert.That(threeArgument.ForceAnisotropyEligible, Is.True);
            Assert.That(explicitEligible.ForceAnisotropyEligible, Is.True);
        }

        [Test]
        public void SamplingDistinguishesForceAnisotropyEligibility()
        {
            // An authored level one and an authored level zero can share
            // filter, wrap, and aniso mode. Only eligibility tells them
            // apart, so equality and hashing must read it.
            var eligible = new TextureSampling(
                TextureFilterMode.Point,
                TextureWrapMode.Clamp,
                TextureAnisoMode.None,
                forceAnisotropyEligible: true);
            var ineligible = new TextureSampling(
                TextureFilterMode.Point,
                TextureWrapMode.Clamp,
                TextureAnisoMode.None,
                forceAnisotropyEligible: false);

            Assert.That(eligible, Is.Not.EqualTo(ineligible));
            Assert.That(
                eligible.GetHashCode(),
                Is.Not.EqualTo(ineligible.GetHashCode()));
        }

        [Test]
        public void AnisotropicSampleWithoutEligibilityIsAnInvariantError()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new TextureSampling(
                    TextureFilterMode.Point,
                    TextureWrapMode.Clamp,
                    TextureAnisoMode.Anisotropic,
                    forceAnisotropyEligible: false));
            Assert.DoesNotThrow(() =>
                new TextureSampling(
                    TextureFilterMode.Point,
                    TextureWrapMode.Clamp,
                    TextureAnisoMode.Anisotropic,
                    forceAnisotropyEligible: true));
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
        public void MappedTexture_CarriesSampleChannelAndMap()
        {
            var sample = Sample(
                "mapped_carry",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var map = AffineAlphaMap.FromBinary32(0.5f, 0.5f);
            var value = ScalarSemanticValue.MappedTexture(
                sample, TextureChannel.Red, map);
            Assert.That(value.Kind, Is.EqualTo(
                ScalarSemanticValueKind.MappedTextureSample));
            Assert.That(value.GetTextureSample(), Is.EqualTo(sample));
            Assert.That(value.GetChannel(), Is.EqualTo(TextureChannel.Red));
            Assert.That(value.GetMap(), Is.EqualTo(map));
        }

        [Test]
        public void ProductChain_KeepsPerFactorMaps_AndDefaultsNull()
        {
            var first = Sample(
                "chain_map_first",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var second = Sample(
                "chain_map_second",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var map = AffineAlphaMap.FromBinary32(2f, 1f);
            var mapped = ScalarSemanticValue.ProductChain(
                new[] { first, second },
                new[] { TextureChannel.Alpha, TextureChannel.Red },
                1f,
                new AffineAlphaMap?[] { null, map });
            Assert.That(mapped.GetChainMap(0), Is.Null);
            Assert.That(mapped.GetChainMap(1), Is.EqualTo(map));

            var plain = ScalarSemanticValue.ProductChain(
                new[] { first, second },
                new[] { TextureChannel.Alpha, TextureChannel.Red },
                1f);
            Assert.That(plain.GetChainMap(0), Is.Null);
            Assert.That(plain.GetChainMap(1), Is.Null);
        }

        [Test]
        public void ProductChain_AllowsASingleFactorOnlyWhenItCarriesAMap()
        {
            var sample = Sample(
                "chain_map_single",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var map = AffineAlphaMap.FromBinary32(2f, 1f);
            Assert.Throws<ArgumentException>(
                () => ScalarSemanticValue.ProductChain(
                    new[] { sample },
                    new[] { TextureChannel.Red },
                    1f));
            var single = ScalarSemanticValue.ProductChain(
                new[] { sample },
                new[] { TextureChannel.Red },
                0.5f,
                new AffineAlphaMap?[] { map });
            Assert.That(single.GetChainFactorCount(), Is.EqualTo(1));
            Assert.That(single.GetChainMap(0), Is.EqualTo(map));
        }

        [Test]
        public void MappedTexture_RejectsIdentityMap()
        {
            var sample = Sample(
                "mapped_identity",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            Assert.Throws<ArgumentException>(
                () => ScalarSemanticValue.MappedTexture(
                    sample, TextureChannel.Red,
                    AffineAlphaMap.FromBinary32(1f, 0f)));
        }

        [Test]
        public void MappedTexture_RejectsDefaultMap()
        {
            var sample = Sample(
                "mapped_default",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            Assert.Throws<ArgumentException>(
                () => ScalarSemanticValue.MappedTexture(
                    sample, TextureChannel.Red,
                    default(AffineAlphaMap)));
        }

        [Test]
        public void GetChainMap_RejectsOutOfRangeIndex_OnMaplessChain()
        {
            var first = Sample(
                "chain_range_first",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var second = Sample(
                "chain_range_second",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var plain = ScalarSemanticValue.ProductChain(
                new[] { first, second },
                new[] { TextureChannel.Alpha, TextureChannel.Red },
                1f);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => plain.GetChainMap(-1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => plain.GetChainMap(2));
        }

        [Test]
        public void ProductChain_EqualsComparesChainMaps()
        {
            var first = Sample(
                "chain_equal_first",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var second = Sample(
                "chain_equal_second",
                0,
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var channels = new[] { TextureChannel.Alpha, TextureChannel.Red };
            var map = AffineAlphaMap.FromBinary32(2f, 1f);

            var mapped = ScalarSemanticValue.ProductChain(
                new[] { first, second }, channels, 1f,
                new AffineAlphaMap?[] { null, map });
            var sameAgain = ScalarSemanticValue.ProductChain(
                new[] { first, second }, channels, 1f,
                new AffineAlphaMap?[] { null, map });
            var flipped = ScalarSemanticValue.ProductChain(
                new[] { first, second }, channels, 1f,
                new AffineAlphaMap?[] { map, null });
            var mapless = ScalarSemanticValue.ProductChain(
                new[] { first, second }, channels, 1f);
            var explicitIdentity = ScalarSemanticValue.ProductChain(
                new[] { first, second }, channels, 1f,
                new AffineAlphaMap?[] { null, null });

            Assert.That(mapped.Equals(sameAgain), Is.True);
            Assert.That(
                mapped.GetHashCode(),
                Is.EqualTo(sameAgain.GetHashCode()));
            Assert.That(mapped.Equals(flipped), Is.False);
            Assert.That(mapped.Equals(mapless), Is.False);
            // A null array means all identity, so it equals an explicit
            // all-null array under the carried chain-map ruling.
            Assert.That(mapless.Equals(explicitIdentity), Is.True);
        }

        [Test]
        public void Fold_WithMappedFactor_KeepsTheMapInTheProductChain()
        {
            var source = new TextureSourceId("test:fold_map");
            var mapping = new UvMapping(0, Vector2.one, Vector2.zero);
            var sampling = new TextureSampling(
                TextureFilterMode.Point,
                TextureWrapMode.Repeat,
                TextureAnisoMode.None);
            var sampleA = new TextureSample(source, mapping, sampling);
            var sampleB = new TextureSample(source, mapping, sampling);
            var map = AffineAlphaMap.FromBinary32(2f, -0.5f);

            var folded = ScalarProductFold.Fold(
                ScalarSemanticValue.MappedTexture(
                    sampleA, TextureChannel.Alpha, map),
                ScalarSemanticValue.Texture(sampleB, TextureChannel.Alpha),
                "_FoldProperty",
                property => Assert.Fail("no refusal is expected"));

            Assert.That(folded, Is.Not.Null);
            Assert.That(
                folded.Kind,
                Is.EqualTo(ScalarSemanticValueKind
                    .ProductChainOfTextureSamples));
            Assert.That(folded.GetChainFactorCount(), Is.EqualTo(2));
            Assert.That(folded.GetChainMap(0), Is.Not.Null);
            Assert.That(folded.GetChainMap(1), Is.Null);
        }

        [Test]
        public void IndependentlyConstructedValuesCompareStructurally()
        {
            var first = ScalarSemanticValue.TextureTimesConstant(
                Sample(
                    "texture:alpha",
                    0,
                    TextureFilterMode.Bilinear,
                    TextureWrapMode.Clamp),
                TextureChannel.Alpha,
                0.25f);
            var second = ScalarSemanticValue.TextureTimesConstant(
                Sample(
                    "texture:alpha",
                    0,
                    TextureFilterMode.Bilinear,
                    TextureWrapMode.Clamp),
                TextureChannel.Alpha,
                0.25f);

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
                ScalarSemanticValue.Constant(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ScalarSemanticValue.Texture(
                    sample,
                    (TextureChannel)99));
            Assert.Throws<ArgumentException>(() =>
                new TextureSample(
                    default,
                    new UvMapping(0, Vector2.one, Vector2.zero),
                    new TextureSampling(
                        TextureFilterMode.Point,
                        TextureWrapMode.Clamp)));
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
        public void CompleteOutputRejectsNullReferenceValue()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SemanticOutput<ScalarSemanticValue>.Complete(null));
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
            var provenFallback = SemanticOutput<ScalarSemanticValue>.Complete(
                ScalarSemanticValue.Constant(1f));
            var unsupportedMissingBehavior =
                SemanticOutput<ScalarSemanticValue>.Unknown();

            Assert.That(
                provenFallback.GetCompleteValue().Kind,
                Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.Throws<InvalidOperationException>(() =>
                unsupportedMissingBehavior.GetCompleteValue());
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
                SemanticOutput<ScalarSemanticValue>.Complete(
                    ScalarSemanticValue.Constant(alpha)));
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
