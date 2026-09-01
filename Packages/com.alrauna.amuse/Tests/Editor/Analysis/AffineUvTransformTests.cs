using System;
using NUnit.Framework;
using UnityEngine;
using BigInteger = System.Numerics.BigInteger;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class AffineUvTransformTests
    {
        [Test]
        public void E1PositiveAndNegativePowerOfTwoScalesAreExact()
        {
            AssertExact(Mapping(2f, 4f, 0f, 0f), 0.25f, 0.5f, 0.75f, 0.25f, 0.5f, 0.75f);
            AssertExact(Mapping(-2f, -4f, 0f, 0f), 0.25f, 0.5f, 0.75f, 0.25f, 0.5f, 0.75f);
        }

        [Test]
        public void E1AcceptsMinimumNormalBoundaryButRefusesOtherBoundaryCases()
        {
            AssertExact(Mapping(2f, 2f, 0f, 0f), MinimumNormal, MinimumNormal, MinimumNormal, 1f, 1f, 1f);
            AssertEnvelope(Mapping(2f, 2f, 0f, 0f), -1f, 1f, 1f, 1f, 1f, 1f);
            AssertEnvelope(Mapping(2f, 2f, 0f, 0f), SubnormalHalf, 1f, 1f, 1f, 1f, 1f);
            AssertEnvelope(Mapping(0.5f, 2f, 0f, 0f), MinimumNormal, 2f * MinimumNormal, MinimumNormal, 1f, 1f, 1f);
            AssertEnvelope(Mapping(SubnormalHalf, 2f, 0f, 0f), 1f, 2f, 1f, 1f, 1f, 1f);
        }

        [Test]
        public void E2ZeroScaleIsExactForOneAndBothAxes()
        {
            AssertExact(Mapping(0f, 2f, 0.5f, 0f), 0.25f, 0.5f, 0.75f, 0.25f, 0.5f, 0.75f);
            AssertExact(Mapping(0f, 0f, 0.5f, -0.25f), 0.25f, 0.5f, 0.75f, 0.25f, 0.5f, 0.75f);
            AssertEnvelope(Mapping(0f, 0f, SubnormalHalf, 0f), 1f, 1f, 1f, 1f, 1f, 1f);
        }

        [Test]
        public void E3RequiresExactNormalProductAndSum()
        {
            AssertExact(Mapping(3f, 0f, 0.5f, 0f), 0.25f, 0.25f, 0.25f, 1f, 1f, 1f);
            AssertEnvelope(Mapping(F26Value, 0f, 0f, 0f), F26Value, F26Value, F26Value, 1f, 1f, 1f);
            AssertEnvelope(Mapping(1f, 0f, FloatPow2(-24), 0f), 1f, 1f, 1f, 1f, 1f, 1f);
            AssertEnvelope(Mapping(1f, 0f, 0f, 0f), SubnormalHalf, SubnormalHalf, SubnormalHalf, 1f, 1f, 1f);
            AssertEnvelope(Mapping(SubnormalHalf, 0f, 0f, 0f), 1f, 1f, 1f, 1f, 1f, 1f);
            AssertEnvelope(Mapping(1f, 0f, SubnormalHalf, 0f), 1f, 1f, 1f, 1f, 1f, 1f);
        }

        [Test]
        public void F26CompensatingRoundingUsesEnvelopeTier()
        {
            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(
                Mapping(F26Value, 0f, F26Offset, 0f),
                Triangle(F26Value, F26Value, F26Value, 1f, 1f, 1f),
                out transformed,
                out envelope), Is.True);
            Assert.That(envelope.X.Numerator.Sign, Is.Not.EqualTo(0));
            Assert.That(BitConverter.SingleToInt32Bits(transformed.Uv0.x), Is.EqualTo(0x3f801002));
        }

        [Test]
        public void FractionalAndNegativeNonPowerOfTwoMappingsUseEnvelopeTier()
        {
            AssertEnvelope(Mapping(1.5f, 2f, 0f, 0f), 0.25f, 0.5f, 0.75f, 1f, 1f, 1f);
            AssertEnvelope(Mapping(1.5f, 2f, 0.25f, 0f), 0.25f, 0.5f, 0.75f, 1f, 1f, 1f);
            AssertEnvelope(Mapping(1.5f, 2f, -0.25f, 0f), 0.25f, 0.5f, 0.75f, 1f, 1f, 1f);
            AssertEnvelope(Mapping(-1.5f, 2f, 0f, 0f), 0.25f, 0.5f, 0.75f, 1f, 1f, 1f);
        }

        [Test]
        public void F19KeepsAxisSpecificResultsAndEnvelopes()
        {
            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(
                Mapping(2f, 3f, 0.5f, 0.25f),
                Triangle(0.25f, 0.5f, 0.75f, 0.125f, 0.25f, 0.375f),
                out transformed,
                out envelope), Is.True);
            Assert.That(transformed.Uv0, Is.EqualTo(new Vector2(1f, 0.625f)));
            Assert.That(transformed.Uv1, Is.EqualTo(new Vector2(1.5f, 1f)));
            Assert.That(transformed.Uv2, Is.EqualTo(new Vector2(2f, 1.375f)));
            AssertRational(envelope.X, Add(Add(new ExactRational(7, BigInteger.One << 23), Pow2(-125)), new ExactRational(15, BigInteger.One << 128)));
            AssertRational(envelope.Y, Add(Add(new ExactRational(5, BigInteger.One << 23), Pow2(-125)), new ExactRational(35, BigInteger.One << 129)));
        }

        [Test]
        public void F3NegativePowerOfTwoScaleReflectsExactTierCoordinates()
        {
            // Falsifies F3 (s = |s| in AffineUvTransform): a negative
            // power-of-two scale must reflect the transformed coordinates,
            // not merely preserve the exact-tier classification.
            // AssertExact/AssertEnvelope cannot catch this mutation: IsE1's
            // own exactness test compares BigInteger.Abs(scale.Significand)
            // to one, so the exact tier is reported identically whether the
            // true or the absolute-valued scale is used — only the
            // transformed VALUES differ, which those tier-only helpers never
            // inspect.
            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(
                Mapping(-2f, -4f, 0f, 0f),
                Triangle(0.25f, 0.5f, 0.75f, 0.25f, 0.5f, 0.75f),
                out transformed,
                out envelope), Is.True);
            AssertVectorBits(transformed.Uv0, new Vector2(-0.5f, -1f));
            AssertVectorBits(transformed.Uv1, new Vector2(-1f, -2f));
            AssertVectorBits(transformed.Uv2, new Vector2(-1.5f, -3f));
            Assert.That(envelope.X.Numerator.Sign, Is.EqualTo(0));
            Assert.That(envelope.Y.Numerator.Sign, Is.EqualTo(0));
        }

        [Test]
        public void F3NegativeScaleWithOffsetCannotBeMaskedByCompensation()
        {
            // Falsifies F3 (s = |s| in AffineUvTransform): a negative scale
            // combined with a non-zero offset, chosen so neither the
            // offset's sign nor its magnitude can compensate for a dropped
            // scale sign. This asserts the coordinates directly via
            // AssertVectorBits rather than through AssertExact/
            // AssertEnvelope, which read only the envelope/tier and never
            // the transformed triangle.
            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(
                Mapping(-4f, -0.5f, 1f, 0.5f),
                Triangle(0.25f, 0.5f, 0.75f, 1f, 2f, 3f),
                out transformed,
                out envelope), Is.True);
            AssertVectorBits(transformed.Uv0, new Vector2(0f, 0f));
            AssertVectorBits(transformed.Uv1, new Vector2(-1f, -0.5f));
            AssertVectorBits(transformed.Uv2, new Vector2(-2f, -1f));
        }

        [Test]
        public void OverflowProductOrSumReturnsFalse()
        {
            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(
                Mapping(2f, 0f, 0f, 0f),
                Triangle(FloatPow2(126), FloatPow2(126), FloatPow2(126), 0f, 0f, 0f),
                out transformed,
                out envelope), Is.False);
            Assert.That(AffineUvTransform.TryTransform(
                Mapping(1f, 0f, FloatPow2(127), 0f),
                Triangle(1f, 1f, 1f, 0f, 0f, 0f),
                out transformed,
                out envelope), Is.False);
        }

        [Test]
        public void EnvelopeIncludesExactEncodingAndResultFlushTerms()
        {
            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(
                Mapping(F26Value, 0f, 0f, 0f),
                Triangle(F26Value, F26Value, F26Value, 0f, 0f, 0f),
                out transformed,
                out envelope), Is.True);
            var expected = Add(
                Pow2(-24),
                Add(
                    Add(
                        Add(Add(Pow2(-21), Pow2(-32)), Pow2(-45)),
                        Pow2(-125)),
                    Add(new ExactRational(3, BigInteger.One << 126), Pow2(-137))));
            AssertRational(envelope.X, expected);
        }

        [Test]
        public void EnvelopeDazCoversSubnormalScaleInputAndOffset()
        {
            AssertEnvelopeRational(
                Mapping(SubnormalHalf, 0f, 0f, 0f),
                Triangle(1f, 1f, 1f, 0f, 0f, 0f),
                Add(Pow2(-148), Add(Pow2(-125), Add(Pow2(-125), Pow2(-253)))));
            AssertEnvelopeRational(
                Mapping(FloatPow2(20), 0f, 0f, 0f),
                Triangle(SubnormalHalf, SubnormalHalf, SubnormalHalf, 0f, 0f, 0f),
                Add(Pow2(-128), Add(Pow2(-125), Add(Pow2(-106), Add(Pow2(-126), Pow2(-253))))));
            AssertEnvelopeRational(
                Mapping(0f, 0f, SubnormalHalf, 0f),
                Triangle(1f, 1f, 1f, 0f, 0f, 0f),
                Add(Pow2(-149), Add(Pow2(-125), Pow2(-125))));
        }

        [Test]
        public void F25CancellationEnvelopeUsesPreCancellationProductMagnitude()
        {
            AssertF25(FloatPow2(20), FloatPow2(-3), Add(Add(Pow2(-2), Pow2(-24)), Add(Pow2(-125), Add(Pow2(-106), Add(Pow2(-125), Pow2(-149))))));
            AssertF25(FloatPow2(40), FloatPow2(17), Add(Add(Pow2(18), Pow2(-4)), Add(Pow2(-125), Add(Pow2(-86), Add(Pow2(-125), Pow2(-149))))));
        }

        [Test]
        public void ExactTiersProduceZeroEnvelope()
        {
            AssertExact(Mapping(2f, 0f, 0f, 0.5f), 0.25f, 0.5f, 0.75f, 0.25f, 0.5f, 0.75f);
        }

        [Test]
        public void IdentityPreservesAllFieldsIncludingSignedZero()
        {
            AssertIdentity(Triangle(0f, 1f, 2f, 0.25f, 0.5f, 0.75f));
            AssertIdentity(Triangle(Bits(unchecked((int)0x80000000)), 1f, 2f, 0.25f, 0.5f, 0.75f));
        }

        [Test]
        public void MissingUv0SurvivesNonidentityMappingUnchanged()
        {
            var input = TriangleAlphaInput.MissingUv0(
                new Vector3(Bits(unchecked((int)0x80000000)), 2f, -3f),
                new Vector3(4f, Bits(unchecked((int)0x80000000)), 6f),
                new Vector3(7f, 8f, Bits(unchecked((int)0x80000000))));

            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(
                Mapping(1.5f, -2.5f, 0.25f, -0.75f),
                input,
                out transformed,
                out envelope), Is.True);

            Assert.That(transformed.HasUv0, Is.False);
            AssertVectorBits(transformed.Position0, input.Position0);
            AssertVectorBits(transformed.Position1, input.Position1);
            AssertVectorBits(transformed.Position2, input.Position2);
            AssertVectorBits(transformed.Uv0, input.Uv0);
            AssertVectorBits(transformed.Uv1, input.Uv1);
            AssertVectorBits(transformed.Uv2, input.Uv2);
            AssertRational(envelope.X, new ExactRational(BigInteger.Zero));
            AssertRational(envelope.Y, new ExactRational(BigInteger.Zero));
        }

        private static void AssertF25(float scale, float exactDelta, ExactRational expected)
        {
            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(
                Mapping(scale, 0f, -scale, 0f),
                Triangle(1f, AdjacentOne, 1f, 0f, 0f, 0f),
                out transformed,
                out envelope), Is.True);
            Assert.That(transformed.Uv0.x, Is.EqualTo(0f));
            Assert.That(transformed.Uv1.x, Is.EqualTo(exactDelta));
            AssertRational(envelope.X, expected);
        }

        private static void AssertIdentity(TriangleAlphaInput input)
        {
            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(
                Mapping(1f, 1f, 0f, 0f), input, out transformed, out envelope), Is.True);
            Assert.That(envelope.X.Numerator.Sign, Is.EqualTo(0));
            Assert.That(envelope.Y.Numerator.Sign, Is.EqualTo(0));
            Assert.That(transformed.HasUv0, Is.EqualTo(input.HasUv0));
            AssertVectorBits(transformed.Position0, input.Position0);
            AssertVectorBits(transformed.Position1, input.Position1);
            AssertVectorBits(transformed.Position2, input.Position2);
            AssertVectorBits(transformed.Uv0, input.Uv0);
            AssertVectorBits(transformed.Uv1, input.Uv1);
            AssertVectorBits(transformed.Uv2, input.Uv2);
        }

        private static void AssertExact(UvMapping mapping, float x0, float x1, float x2, float y0, float y1, float y2)
        {
            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(mapping, Triangle(x0, x1, x2, y0, y1, y2), out transformed, out envelope), Is.True);
            Assert.That(envelope.X.Numerator.Sign, Is.EqualTo(0));
            Assert.That(envelope.Y.Numerator.Sign, Is.EqualTo(0));
        }

        private static void AssertEnvelope(UvMapping mapping, float x0, float x1, float x2, float y0, float y1, float y2)
        {
            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(mapping, Triangle(x0, x1, x2, y0, y1, y2), out transformed, out envelope), Is.True);
            Assert.That(envelope.X.Numerator.Sign != 0 || envelope.Y.Numerator.Sign != 0, Is.True);
        }

        private static void AssertEnvelopeRational(UvMapping mapping, TriangleAlphaInput triangle, ExactRational expected)
        {
            TriangleAlphaInput transformed;
            AlphaUvEnvelope envelope;
            Assert.That(AffineUvTransform.TryTransform(mapping, triangle, out transformed, out envelope), Is.True);
            AssertRational(envelope.X, expected);
        }

        private static void AssertVectorBits(Vector3 actual, Vector3 expected)
        {
            Assert.That(BitConverter.SingleToInt32Bits(actual.x), Is.EqualTo(BitConverter.SingleToInt32Bits(expected.x)));
            Assert.That(BitConverter.SingleToInt32Bits(actual.y), Is.EqualTo(BitConverter.SingleToInt32Bits(expected.y)));
            Assert.That(BitConverter.SingleToInt32Bits(actual.z), Is.EqualTo(BitConverter.SingleToInt32Bits(expected.z)));
        }

        private static void AssertVectorBits(Vector2 actual, Vector2 expected)
        {
            Assert.That(BitConverter.SingleToInt32Bits(actual.x), Is.EqualTo(BitConverter.SingleToInt32Bits(expected.x)));
            Assert.That(BitConverter.SingleToInt32Bits(actual.y), Is.EqualTo(BitConverter.SingleToInt32Bits(expected.y)));
        }

        private static void AssertRational(ExactRational actual, ExactRational expected)
        {
            Assert.That(actual.CompareTo(expected), Is.EqualTo(0));
        }

        private static ExactRational Add(ExactRational left, ExactRational right)
        {
            return ExactRational.Add(left, right);
        }

        private static ExactRational Pow2(int exponent)
        {
            return exponent >= 0
                ? new ExactRational(BigInteger.One << exponent)
                : new ExactRational(BigInteger.One, BigInteger.One << -exponent);
        }

        private static UvMapping Mapping(float scaleX, float scaleY, float offsetX, float offsetY)
        {
            return new UvMapping(0, new Vector2(scaleX, scaleY), new Vector2(offsetX, offsetY));
        }

        private static TriangleAlphaInput Triangle(float x0, float x1, float x2, float y0, float y1, float y2)
        {
            return TriangleAlphaInput.WithUv0(
                new Vector3(1f, 2f, 3f),
                new Vector3(4f, 5f, 6f),
                new Vector3(7f, 8f, 9f),
                new Vector2(x0, y0),
                new Vector2(x1, y1),
                new Vector2(x2, y2));
        }

        private static float Bits(int bits)
        {
            return BitConverter.Int32BitsToSingle(bits);
        }

        private static float FloatPow2(int exponent)
        {
            return Bits((exponent + 127) << 23);
        }

        private static readonly float MinimumNormal = Bits(0x00800000);
        private static readonly float SubnormalHalf = Bits(0x00400000);
        private static readonly float AdjacentOne = Bits(0x3f800001);
        private static readonly float F26Value = Bits(0x3f800800);
        private static readonly float F26Offset = Bits(0x34400000);
    }
}
