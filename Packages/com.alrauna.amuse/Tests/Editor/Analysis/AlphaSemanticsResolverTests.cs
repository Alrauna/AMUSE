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

        /// <summary>
        /// Half opaque (bottom row 255), half transparent (top row 0).
        /// </summary>
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

        /// <summary>
        /// A nondegenerate triangle covering the lower-left quarter of UV space.
        /// </summary>
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

        /// <summary>
        /// The same shape in the upper half of UV space.
        /// </summary>
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

        // --- Task 1: resolution boundary --------------------------------------

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

        // --- Task 2: constant alpha forms -------------------------------------

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

        // --- Task 3: texture-sampled alpha ------------------------------------

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

        // The semantic and classifier enums are internal, so a public NUnit
        // TestCase cannot carry them as parameters (CS0051). These cases are
        // enumerated inside one public test instead.
        [Test]
        public void EveryScalarChannelUsesTheSameScalarField()
        {
            var channels = new[]
            {
                TextureChannel.Red,
                TextureChannel.Green,
                TextureChannel.Blue,
                TextureChannel.Alpha,
            };

            foreach (var channel in channels)
            {
                var requested = (TextureChannel?)null;
                AlphaFieldProvider provider = (TextureSourceId source,
                    TextureChannel requestedChannel,
                    out AlphaTextureData result) =>
                {
                    requested = requestedChannel;
                    result = MixedField();
                    return true;
                };

                var resolution = ResolveSample(Sample(), channel, provider);

                Assert.That(requested, Is.EqualTo(channel), channel.ToString());
                Assert.That(
                    resolution.Classify(OpaqueCornerTriangle()),
                    Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                    channel.ToString());
            }
        }

        [Test]
        public void SamplingMapsExhaustivelyAndExactly()
        {
            var field = MixedField();
            var triangles = new[]
            {
                OpaqueCornerTriangle(), TransparentCornerTriangle()
            };

            foreach (var filter in new[]
                     {
                         TextureFilterMode.Point, TextureFilterMode.Bilinear
                     })
            {
                foreach (var wrap in new[]
                         {
                             TextureWrapMode.Clamp, TextureWrapMode.Repeat
                         })
                {
                    var resolution = ResolveSample(
                        Sample(filter, wrap),
                        TextureChannel.Alpha,
                        Providing(field));
                    var expected = new AlphaSamplingSettings(
                        filter == TextureFilterMode.Point
                            ? AlphaFilterMode.Point
                            : AlphaFilterMode.Bilinear,
                        wrap == TextureWrapMode.Clamp
                            ? AlphaWrapMode.Clamp
                            : AlphaWrapMode.Repeat);

                    foreach (var triangle in triangles)
                    {
                        Assert.That(
                            resolution.Classify(triangle),
                            Is.EqualTo(TriangleAlphaClassifier.Classify(
                                triangle, field, expected)),
                            filter + "/" + wrap);
                    }
                }
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

        [TestCase(1, 1f, 1f, 0f, 0f)]         // non-zero UV set
        [TestCase(0, 2f, 1f, 0f, 0f)]         // scaled U
        [TestCase(0, 1f, 1f, 0.5f, 0f)]       // offset U
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

        // --- Task 4: the multiplier lemma -------------------------------------

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
            var opaqueField = ResolveMultiplied(
                multiplier, Providing(Field(2, 2, 255)));
            var emptyField = ResolveMultiplied(
                multiplier, Providing(Field(2, 2, 0)));

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

        // --- Task 5: adversarial and pass-through coverage ---------------------

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
                ResolveSample(
                    Sample(), TextureChannel.Alpha, Providing(MixedField())),
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
    }
}
