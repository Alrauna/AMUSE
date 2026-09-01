using System;
using System.Collections.Generic;
using NUnit.Framework;
using BigInteger = System.Numerics.BigInteger;
using Alrauna.Amuse.Editor.Analysis;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class ExactUvEnvelopeTests
    {
        [Test]
        public void ZeroHasExactlyZeroComponents()
        {
            AssertRational(AlphaUvEnvelope.Zero.X, 0, 1);
            AssertRational(AlphaUvEnvelope.Zero.Y, 0, 1);
        }

        [TestCase(3, -1, 1, -24)]
        [TestCase(1, -126, 1, -150)]
        [TestCase(8388607, -149, 1, -150)]
        [TestCase(16777215, -24, 1, -25)]
        [TestCase(1, 0, 1, -24)]
        public void HalfUlpUsesBinary32Spacing(
            long significand,
            int exponent,
            long expectedSignificand,
            int expectedExponent)
        {
            var result = ExactUvGeometry.HalfUlp(
                new ExactDyadic(new BigInteger(significand), exponent));

            Assert.That(result.Significand, Is.EqualTo(new BigInteger(expectedSignificand)));
            Assert.That(result.Exponent, Is.EqualTo(expectedExponent));
        }

        [Test]
        public void EncodeToNearestFloatKeepsExactBinary32Value()
        {
            AssertEncoding(
                new ExactRational(3, 2),
                0x3fc00000,
                new ExactRational(0));
        }

        [Test]
        public void EncodeToNearestFloatRoundsPositiveHalfwayToEven()
        {
            AssertEncoding(
                new ExactRational((BigInteger.One << 24) + BigInteger.One, BigInteger.One << 24),
                0x3f800000,
                new ExactRational(1, BigInteger.One << 24));
        }

        [Test]
        public void EncodeToNearestFloatRoundsNegativeHalfwayToEven()
        {
            AssertEncoding(
                new ExactRational(-((BigInteger.One << 24) + BigInteger.One), BigInteger.One << 24),
                unchecked((int)0xbf800000),
                new ExactRational(-1, BigInteger.One << 24));
        }

        [Test]
        public void EncodeToNearestFloatRoundsOddHalfwayTowardEvenNeighbor()
        {
            AssertEncoding(
                new ExactRational((BigInteger.One << 24) + 3, BigInteger.One << 24),
                0x3f800002,
                new ExactRational(-1, BigInteger.One << 24));
        }

        [Test]
        public void EncodeToNearestFloatRoundsPowerBoundaryHalfwayToEven()
        {
            AssertEncoding(
                new ExactRational((BigInteger.One << 26) - BigInteger.One, BigInteger.One << 25),
                0x40000000,
                new ExactRational(-1, BigInteger.One << 25));
        }

        [Test]
        public void EncodeToNearestFloatRoundsSubnormalBoundaryHalfwayToZero()
        {
            AssertEncoding(
                new ExactRational(1, BigInteger.One << 150),
                0,
                new ExactRational(1, BigInteger.One << 150));
        }

        [Test]
        public void EncodeToNearestFloatRoundsLargestSubnormalHalfwayToMinimumNormal()
        {
            AssertEncoding(
                new ExactRational((BigInteger.One << 24) - BigInteger.One, BigInteger.One << 150),
                0x00800000,
                new ExactRational(-1, BigInteger.One << 150));
        }

        [Test]
        public void EncodeToNearestFloatDoesNotDoubleRoundAboveBinary32Midpoint()
        {
            // 1 + 2^-24 + 2^-78: the 2^-78 perturbation is below binary64
            // precision, so a double-routed encoder lands exactly on the
            // binary32 midpoint and ties to even at 0x3f800000. Exact
            // rational -> binary32 rounding must keep the perturbation and
            // select the next neighbor 0x3f800001; this defends against
            // wider-type-as-proof / double rounding.
            AssertEncoding(
                new ExactRational(
                    (BigInteger.One << 78) + (BigInteger.One << 54) + BigInteger.One,
                    BigInteger.One << 78),
                0x3f800001,
                new ExactRational(BigInteger.One - (BigInteger.One << 54), BigInteger.One << 78));
        }

        [Test]
        public void ExactBinary32RequiresSignificandAndRange()
        {
            Assert.That(ExactUvGeometry.IsExactBinary32(
                new ExactDyadic((BigInteger.One << 24) - BigInteger.One, -23)), Is.True);
            Assert.That(ExactUvGeometry.IsExactBinary32(
                new ExactDyadic((BigInteger.One << 24) + BigInteger.One, -24)), Is.False);
            Assert.That(ExactUvGeometry.IsExactBinary32(
                new ExactDyadic(BigInteger.One, -149)), Is.True);
            Assert.That(ExactUvGeometry.IsExactBinary32(
                new ExactDyadic((BigInteger.One << 24) - BigInteger.One, 104)), Is.True);
            Assert.That(ExactUvGeometry.IsExactBinary32(
                new ExactDyadic(BigInteger.One, 128)), Is.False);
        }

        [Test]
        public void NormalOrZeroBinary32RejectsSubnormalAndOverflow()
        {
            Assert.That(ExactUvGeometry.IsNormalOrZeroBinary32(
                new ExactDyadic(BigInteger.Zero, 0)), Is.True);
            Assert.That(ExactUvGeometry.IsNormalOrZeroBinary32(
                new ExactDyadic(BigInteger.One, -126)), Is.True);
            Assert.That(ExactUvGeometry.IsNormalOrZeroBinary32(
                new ExactDyadic(BigInteger.One, -127)), Is.False);
            Assert.That(ExactUvGeometry.IsNormalOrZeroBinary32(
                new ExactDyadic((BigInteger.One << 24) - BigInteger.One, 104)), Is.True);
            Assert.That(ExactUvGeometry.IsNormalOrZeroBinary32(
                new ExactDyadic(BigInteger.One, 128)), Is.False);
        }

        [Test]
        public void ConvexHullRemovesDuplicatesAndOrdersCounterClockwise()
        {
            var hull = ExactUvGeometry.ConvexHull(new[]
            {
                Point(1, 1),
                Point(0, 0),
                Point(1, 0),
                Point(0, 1),
                Point(1, 0)
            });

            AssertHull(hull, Point(0, 0), Point(1, 0), Point(1, 1), Point(0, 1));
        }

        [Test]
        public void ConvexHullReducesCollinearPointsToEndpoints()
        {
            var hull = ExactUvGeometry.ConvexHull(new[]
            {
                Point(1, 1),
                Point(0, 0),
                Point(new ExactRational(1, 2), new ExactRational(1, 2)),
                Point(1, 1)
            });

            AssertHull(hull, Point(0, 0), Point(1, 1));
        }

        [Test]
        public void ConvexHullUsesDenominatorCorrectOrientation()
        {
            var hull = ExactUvGeometry.ConvexHull(new[]
            {
                Point(-1, new ExactRational(-2, 3)),
                Point(-2, -2),
                Point(-1, -1)
            });

            AssertHull(
                hull,
                Point(-2, -2),
                Point(-1, -1),
                Point(-1, new ExactRational(-2, 3)));
        }

        [Test]
        public void OutwardExpandZeroReturnsOriginalDomain()
        {
            var domain = new ExactUvDomain(
                new[] { Point(0, 0), Point(1, 0), Point(0, 1) },
                new BigInteger(8));

            Assert.That(
                ExactUvGeometry.OutwardExpand(domain, new ExactRational(0), new ExactRational(0)),
                Is.SameAs(domain));
        }

        [Test]
        public void OutwardExpandRejectsNegativeEnvelope()
        {
            var domain = new ExactUvDomain(
                new[] { Point(0, 0) },
                new BigInteger(1));

            try
            {
                ExactUvGeometry.OutwardExpand(
                    domain,
                    new ExactRational(-1, 2),
                    new ExactRational(0));
                Assert.Fail("Expected a negative X envelope to be rejected.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }

            try
            {
                ExactUvGeometry.OutwardExpand(
                    domain,
                    new ExactRational(0),
                    new ExactRational(-1, 2));
                Assert.Fail("Expected a negative Y envelope to be rejected.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        [Test]
        public void OutwardExpandOnePointCreatesExactRectangle()
        {
            var domain = new ExactUvDomain(
                new[] { Point(new ExactRational(1, 3), new ExactRational(2, 5)) },
                new BigInteger(8));

            var expanded = ExactUvGeometry.OutwardExpand(
                domain,
                new ExactRational(1, 2),
                new ExactRational(1, 4));

            Assert.That(expanded.TexelScale, Is.EqualTo(new BigInteger(8)));
            AssertHull(
                expanded.Vertices,
                Point(new ExactRational(-1, 6), new ExactRational(3, 20)),
                Point(new ExactRational(5, 6), new ExactRational(3, 20)),
                Point(new ExactRational(5, 6), new ExactRational(13, 20)),
                Point(new ExactRational(-1, 6), new ExactRational(13, 20)));
        }

        [Test]
        public void OutwardExpandTwoPointsCreatesExactRectangle()
        {
            var domain = new ExactUvDomain(
                new[] { Point(0, 0), Point(1, 0) },
                new BigInteger(4));

            var expanded = ExactUvGeometry.OutwardExpand(
                domain,
                new ExactRational(1, 2),
                new ExactRational(1, 4));

            Assert.That(expanded.TexelScale, Is.EqualTo(new BigInteger(4)));
            AssertHull(
                expanded.Vertices,
                Point(new ExactRational(-1, 2), new ExactRational(-1, 4)),
                Point(new ExactRational(3, 2), new ExactRational(-1, 4)),
                Point(new ExactRational(3, 2), new ExactRational(1, 4)),
                Point(new ExactRational(-1, 2), new ExactRational(1, 4)));
        }

        [Test]
        public void OutwardExpandContainsEveryOriginalVertex()
        {
            var domain = new ExactUvDomain(
                new[] { Point(0, 0), Point(2, 0), Point(1, 1) },
                new BigInteger(2));
            var expanded = ExactUvGeometry.OutwardExpand(
                domain,
                new ExactRational(1, 3),
                new ExactRational(1, 5));

            for (var index = 0; index < domain.Vertices.Count; index++)
            {
                Assert.That(IsInsideCounterClockwiseHull(expanded.Vertices, domain.Vertices[index]), Is.True);
            }
        }

        private static void AssertEncoding(ExactRational input, int expectedBits, ExactRational expectedError)
        {
            var encoded = ExactUvGeometry.EncodeToNearestFloat(input);

            Assert.That(
                BitConverter.ToInt32(BitConverter.GetBytes(encoded.Value), 0),
                Is.EqualTo(expectedBits));
            Assert.That(encoded.Error.CompareTo(expectedError), Is.EqualTo(0));
            var decoded = ToRational(ExactUvGeometry.DecodeFloat(encoded.Value));
            Assert.That(
                ExactRational.Subtract(input, decoded).CompareTo(encoded.Error),
                Is.EqualTo(0));
            Assert.That(
                Absolute(encoded.Error).CompareTo(ToRational(ExactUvGeometry.HalfUlp(
                    ExactUvGeometry.DecodeFloat(encoded.Value)))),
                Is.LessThanOrEqualTo(0));
        }

        private static bool IsInsideCounterClockwiseHull(
            IReadOnlyList<ExactUvPoint> hull,
            ExactUvPoint point)
        {
            for (var index = 0; index < hull.Count; index++)
            {
                var next = (index + 1) % hull.Count;
                if (Cross(hull[index], hull[next], point).Numerator.Sign < 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static ExactRational Cross(ExactUvPoint first, ExactUvPoint second, ExactUvPoint third)
        {
            return ExactRational.Subtract(
                ExactRational.Multiply(
                    ExactRational.Subtract(second.X, first.X),
                    ExactRational.Subtract(third.Y, first.Y)),
                ExactRational.Multiply(
                    ExactRational.Subtract(second.Y, first.Y),
                    ExactRational.Subtract(third.X, first.X)));
        }

        private static ExactRational Absolute(ExactRational value)
        {
            return new ExactRational(BigInteger.Abs(value.Numerator), value.Denominator);
        }

        private static ExactRational ToRational(ExactDyadic value)
        {
            return value.Exponent >= 0
                ? new ExactRational(value.Significand << value.Exponent)
                : new ExactRational(value.Significand, BigInteger.One << -value.Exponent);
        }

        private static void AssertHull(IReadOnlyList<ExactUvPoint> actual, params ExactUvPoint[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].X.CompareTo(expected[index].X), Is.EqualTo(0));
                Assert.That(actual[index].Y.CompareTo(expected[index].Y), Is.EqualTo(0));
            }
        }

        private static void AssertRational(ExactRational actual, long numerator, long denominator)
        {
            Assert.That(actual.Numerator, Is.EqualTo(new BigInteger(numerator)));
            Assert.That(actual.Denominator, Is.EqualTo(new BigInteger(denominator)));
        }

        private static ExactUvPoint Point(long x, long y)
        {
            return Point(new ExactRational(x), new ExactRational(y));
        }

        private static ExactUvPoint Point(long x, ExactRational y)
        {
            return Point(new ExactRational(x), y);
        }

        private static ExactUvPoint Point(ExactRational x, ExactRational y)
        {
            return new ExactUvPoint(x, y);
        }
    }
}
