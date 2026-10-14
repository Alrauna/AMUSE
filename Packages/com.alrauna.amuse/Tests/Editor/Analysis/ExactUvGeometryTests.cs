using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using BigInteger = System.Numerics.BigInteger;
using Alrauna.Amuse.Editor.Analysis;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class ExactUvGeometryTests
    {
        [Test]
        public void FloorMod_IntegerOverloadMatchesBigIntegerOverload()
        {
            var integerOverload = typeof(ExactUvGeometry).GetMethod(
                nameof(ExactUvGeometry.FloorMod),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int) },
                null);

            Assert.That(integerOverload, Is.Not.Null, "FloorMod must provide a 32-bit integer overload.");

            int[] values =
            {
                0,
                1,
                2,
                5,
                17,
                100,
                1024,
                1234567,
                -1,
                -2,
                -5,
                -17,
                -100,
                -1024,
                -1234567,
                int.MaxValue,
                int.MaxValue - 1,
                int.MinValue,
                int.MinValue + 1
            };

            int[] moduli =
            {
                1,
                2,
                3,
                5,
                7,
                8,
                16,
                31,
                64,
                100,
                1024,
                65536
            };

            for (var valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                var value = values[valueIndex];
                for (var modulusIndex = 0; modulusIndex < moduli.Length; modulusIndex++)
                {
                    var modulus = moduli[modulusIndex];
                    var integerResult = (int)integerOverload.Invoke(null, new object[] { value, modulus });
                    var directResult = ExactUvGeometry.FloorMod(value, modulus);
                    var bigIntegerResult = FloorMod(new BigInteger(value), modulus);

                    Assert.That(
                        integerResult,
                        Is.EqualTo(bigIntegerResult),
                        $"Value {value} mod {modulus} did not match.");

                    Assert.That(
                        directResult,
                        Is.EqualTo(bigIntegerResult),
                        $"Direct call for value {value} mod {modulus} did not match.");

                    Assert.That(integerResult, Is.GreaterThanOrEqualTo(0));
                    Assert.That(integerResult, Is.LessThan(modulus));
                }
            }

            int[] invalidModuli = { 0, -1, -2, -100, int.MinValue };
            for (var index = 0; index < invalidModuli.Length; index++)
            {
                var invalidModulus = invalidModuli[index];

                var targetInvocationException = Assert.Throws<TargetInvocationException>(
                    () => integerOverload.Invoke(null, new object[] { 10, invalidModulus }));
                Assert.That(
                    targetInvocationException.InnerException,
                    Is.InstanceOf<ArgumentOutOfRangeException>());

                Assert.Throws<ArgumentOutOfRangeException>(
                    () => ExactUvGeometry.FloorMod(10, invalidModulus));

                Assert.Throws<ArgumentOutOfRangeException>(
                    () => FloorMod(new BigInteger(10), invalidModulus));
            }
        }

        [Test]
        public void CreateHull_CollinearAndWindingPoints_UsesRationalOrientation()
        {
            var origin = new ExactUvPoint(new ExactRational(0), new ExactRational(0));
            var collinearMid = new ExactUvPoint(new ExactRational(2, 5), new ExactRational(4, 15));
            var collinearEnd = new ExactUvPoint(new ExactRational(1, 2), new ExactRational(1, 3));

            var collinearHull = ExactUvGeometry.CreateHull(new[] { origin, collinearMid, collinearEnd });

            Assert.That(collinearHull.Count, Is.EqualTo(2), "Collinear points must reduce to two endpoints.");
            Assert.That(collinearHull[0].X.CompareTo(new ExactRational(0)), Is.EqualTo(0));
            Assert.That(collinearHull[0].Y.CompareTo(new ExactRational(0)), Is.EqualTo(0));
            Assert.That(collinearHull[1].X.CompareTo(new ExactRational(1, 2)), Is.EqualTo(0));
            Assert.That(collinearHull[1].Y.CompareTo(new ExactRational(1, 3)), Is.EqualTo(0));

            var clockwiseFirst = new ExactUvPoint(new ExactRational(1, 3), new ExactRational(1, 2));
            var clockwiseSecond = new ExactUvPoint(new ExactRational(1, 2), new ExactRational(1, 4));

            var clockwiseHull = ExactUvGeometry.CreateHull(new[] { origin, clockwiseFirst, clockwiseSecond });

            Assert.That(clockwiseHull.Count, Is.EqualTo(3), "Clockwise points must remain a triangle.");
            Assert.That(clockwiseHull[0].X.CompareTo(new ExactRational(0)), Is.EqualTo(0));
            Assert.That(clockwiseHull[0].Y.CompareTo(new ExactRational(0)), Is.EqualTo(0));
            Assert.That(clockwiseHull[1].X.CompareTo(new ExactRational(1, 2)), Is.EqualTo(0));
            Assert.That(clockwiseHull[1].Y.CompareTo(new ExactRational(1, 4)), Is.EqualTo(0));
            Assert.That(clockwiseHull[2].X.CompareTo(new ExactRational(1, 3)), Is.EqualTo(0));
            Assert.That(clockwiseHull[2].Y.CompareTo(new ExactRational(1, 2)), Is.EqualTo(0));

            var counterClockwiseHull = ExactUvGeometry.CreateHull(
                new[] { origin, clockwiseSecond, clockwiseFirst });

            Assert.That(counterClockwiseHull.Count, Is.EqualTo(3));
            Assert.That(counterClockwiseHull[0].X.CompareTo(new ExactRational(0)), Is.EqualTo(0));
            Assert.That(counterClockwiseHull[0].Y.CompareTo(new ExactRational(0)), Is.EqualTo(0));
            Assert.That(counterClockwiseHull[1].X.CompareTo(new ExactRational(1, 2)), Is.EqualTo(0));
            Assert.That(counterClockwiseHull[1].Y.CompareTo(new ExactRational(1, 4)), Is.EqualTo(0));
            Assert.That(counterClockwiseHull[2].X.CompareTo(new ExactRational(1, 3)), Is.EqualTo(0));
            Assert.That(counterClockwiseHull[2].Y.CompareTo(new ExactRational(1, 2)), Is.EqualTo(0));
        }

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

        [TestCase(0, -140, 0L, 1L)]
        [TestCase(1, -20, 1L, 1048576L)]
        [TestCase(-3, -20, -3L, 1048576L)]
        [TestCase(7, 0, 7L, 1L)]
        [TestCase(12345, 20, 12944670720L, 1L)]
        public void SharedDyadicToRationalMatchesIndependentRationals(
            int significand, int exponent, long expectedNumerator,
            long expectedDenominator)
        {
            var dyadic = new ExactDyadic(significand, exponent);
            // The same ExactDyadic construction the replaced test used,
            // kept as-is.
            var actual = ExactUvGeometry.ToRational(dyadic);

            Assert.That(
                actual,
                Is.EqualTo(new ExactRational(
                    new BigInteger(expectedNumerator),
                    new BigInteger(expectedDenominator))));
        }

        private static int FloorMod(BigInteger value, int modulus)
        {
            if (modulus <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(modulus));
            }
            var remainder = value % modulus;
            if (remainder.Sign < 0)
            {
                remainder += modulus;
            }
            return (int)remainder;
        }
    }
}
