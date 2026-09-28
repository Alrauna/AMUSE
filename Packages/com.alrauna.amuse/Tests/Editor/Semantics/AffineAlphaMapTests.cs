using System;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public class AffineAlphaMapTests
    {
        private static float Saturate(float value)
        {
            return value <= 0f ? 0f : value >= 1f ? 1f : value;
        }

        private static float RandomFinite(Random rng)
        {
            float value;
            do
            {
                value = (float)(rng.NextDouble() * 4.0 - 2.0);
            }
            while (float.IsNaN(value));
            return value;
        }

        [Test]
        public void Evaluate_ContainsTheRoundedOrder_ForPseudoRandomInputs()
        {
            var rng = new Random(20260927);
            var checkedPairs = 0;
            for (var index = 0; index < 4000; index++)
            {
                var scale = RandomFinite(rng);
                var value = RandomFinite(rng);
                if (scale == 0f || (scale == 1f && value == 0f))
                {
                    continue;
                }

                var map = AffineAlphaMap.FromBinary32(scale, value);
                var r = Saturate((float)(rng.NextDouble() * 1.5 - 0.25));
                var (lower, upper) = map.Evaluate(r);
                var rounded = Saturate(r * scale + value);

                Assert.That(lower, Is.LessThanOrEqualTo(upper),
                    $"inverted envelope for ({r}, {scale}, {value})");
                Assert.That(lower, Is.LessThanOrEqualTo(rounded),
                    $"lower exceeds the rounded order for ({r}, {scale}, {value})");
                Assert.That(upper, Is.GreaterThanOrEqualTo(rounded),
                    $"upper drops below the rounded order for ({r}, {scale}, {value})");
                checkedPairs++;
            }
            Assert.That(checkedPairs, Is.GreaterThan(3000));
        }

        [Test]
        public void Evaluate_AtOne_ProvesTheSaturatedPairExactlyOne()
        {
            var map = AffineAlphaMap.FromBinary32(1f, 0.5f);
            var (lower, upper) = map.Evaluate(1f);
            Assert.That(lower == 1f && upper == 1f, Is.True);
        }

        [Test]
        public void Evaluate_AtZero_IsTheClampedValue_Alone()
        {
            var map = AffineAlphaMap.FromBinary32(1f, 0.25f);
            var (lower, upper) = map.Evaluate(0f);
            Assert.That(lower == 0.25f && upper == 0.25f, Is.True);
        }

        [Test]
        public void Evaluate_WithNegativeScale_SwapsTheEnvelopeEndpoints()
        {
            var map = AffineAlphaMap.FromBinary32(-1f, 1f);
            var (lower, upper) = map.Evaluate(0.25f);
            // saturate(0.25 * -1 + 1) == 0.75 exactly in binary32.
            Assert.That(lower == 0.75f && upper == 0.75f, Is.True);
        }

        [Test]
        public void Evaluate_WithSumAboveOne_ClampsToExactlyOne()
        {
            var map = AffineAlphaMap.FromBinary32(1e30f, 1e30f);
            var (lower, upper) = map.Evaluate(1f);
            Assert.That(lower == 1f && upper == 1f, Is.True);
        }

        [Test]
        public void Evaluate_AtExactTie_RoundsHalfToEven()
        {
            // r * s + v is exactly 16777217 * 2^-25, the midpoint between
            // 0.5f and its successor; half-even keeps 0.5f in both orders.
            var map = AffineAlphaMap.FromBinary32(
                1f / 33554432f, 0.5f);
            var (lower, upper) = map.Evaluate(1f);
            Assert.That(lower == 0.5f && upper == 0.5f, Is.True);
        }

        [Test]
        public void Factory_RejectsIdentityZeroScaleAndNonFinite()
        {
            Assert.Throws<ArgumentException>(
                () => AffineAlphaMap.FromBinary32(1f, 0f));
            Assert.Throws<ArgumentException>(
                () => AffineAlphaMap.FromBinary32(0f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AffineAlphaMap.FromBinary32(float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AffineAlphaMap.FromBinary32(1f, float.PositiveInfinity));
        }

        [Test]
        public void Equality_ComparesTheExactRationalComponents()
        {
            var first = AffineAlphaMap.FromBinary32(2f, 0.5f);
            var second = AffineAlphaMap.FromBinary32(2f, 0.5f);
            var other = AffineAlphaMap.FromBinary32(2f, 0.25f);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.EqualTo(other));
        }
    }
}
