using System;
using System.Numerics;
using Alrauna.Amuse.Editor.Analysis;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// The exact binary32 map <c>saturate(r * scale + value)</c>. Both
    /// hardware evaluation orders are monotone in r, because
    /// round-to-nearest-half-even and saturate are monotone; the direction
    /// follows the scale sign. The term over any red interval is bounded
    /// by the evaluations at that interval's endpoints, in both orders.
    /// Every evaluation is carried out on exact
    /// BigInteger rationals and rounded once per hardware step to the
    /// binary32 grid at half-even; there are no ulp margins. This is the
    /// rounding argument the 2026-09-07 mask composition design deferred.
    /// </summary>
    internal readonly struct AffineAlphaMap : IEquatable<AffineAlphaMap>
    {
        private readonly BigInteger _scaleNumerator;
        private readonly int _scaleExponent;
        private readonly BigInteger _valueNumerator;
        private readonly int _valueExponent;

        private AffineAlphaMap(
            BigInteger scaleNumerator,
            int scaleExponent,
            BigInteger valueNumerator,
            int valueExponent)
        {
            _scaleNumerator = scaleNumerator;
            _scaleExponent = scaleExponent;
            _valueNumerator = valueNumerator;
            _valueExponent = valueExponent;
        }

        internal static AffineAlphaMap FromBinary32(float scale, float value)
        {
            if (!IsFinite(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale));
            }
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (scale == 1f && value == 0f)
            {
                throw new ArgumentException(
                    "The identity pair routes to the plain sample arm and " +
                    "never constructs a map.",
                    nameof(scale));
            }
            if (scale == 0f)
            {
                throw new ArgumentException(
                    "A zero scale routes to the constant arm and never " +
                    "constructs a map.",
                    nameof(scale));
            }

            Decompose(scale, out var scaleNumerator, out var scaleExponent);
            Decompose(value, out var valueNumerator, out var valueExponent);
            return new AffineAlphaMap(
                scaleNumerator, scaleExponent, valueNumerator, valueExponent);
        }

        /// <summary>
        /// Returns the lower and upper binary32 bounds of the mapped term at
        /// the exact input r, covering the fused and the multiply-then-add
        /// order. When the orders agree the envelope collapses to a point.
        /// </summary>
        internal (float Lower, float Upper) Evaluate(float r)
        {
            if (!IsFinite(r))
            {
                throw new ArgumentOutOfRangeException(nameof(r));
            }

            Decompose(r, out var rn, out var rd);
            var productNumerator = rn * _scaleNumerator;
            var productExponent = rd + _scaleExponent;

            var (fusedN, fusedD) = Sum(
                productNumerator, productExponent,
                _valueNumerator, _valueExponent);
            var fused = Saturate(Round(fusedN, fusedD));

            var roundedProduct = Round(productNumerator, productExponent);
            Decompose(roundedProduct, out var pn, out var pd);
            var (unfusedN, unfusedD) = Sum(pn, pd, _valueNumerator, _valueExponent);
            var unfused = Saturate(Round(unfusedN, unfusedD));

            return fused <= unfused ? (fused, unfused) : (unfused, fused);
        }

        public bool Equals(AffineAlphaMap other)
        {
            return _scaleNumerator.Equals(other._scaleNumerator) &&
                   _scaleExponent.Equals(other._scaleExponent) &&
                   _valueNumerator.Equals(other._valueNumerator) &&
                   _valueExponent.Equals(other._valueExponent);
        }

        public override bool Equals(object obj)
        {
            return obj is AffineAlphaMap other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _scaleNumerator.GetHashCode();
                hash = hash * 31 + _scaleExponent.GetHashCode();
                hash = hash * 31 + _valueNumerator.GetHashCode();
                hash = hash * 31 + _valueExponent.GetHashCode();
                return hash;
            }
        }

        /// <summary>Decomposes a finite float into n * 2^d with integer n.</summary>
        private static void Decompose(float value, out BigInteger n, out int d)
        {
            var bits = BitConverter.SingleToInt32Bits(value);
            var rawExponent = (int)((bits >> 23) & 0xFF);
            if (rawExponent == 0)
            {
                n = bits & 0x007FFFFF;
                d = -149;
            }
            else
            {
                n = (bits & 0x007FFFFF) | 0x00800000;
                d = rawExponent - 150;
            }

            if (bits < 0)
            {
                n = -n;
            }
        }

        /// <summary>Exact sum (n1 * 2^d1) + (n2 * 2^d2) as n * 2^d.</summary>
        private static (BigInteger n, int d) Sum(
            BigInteger n1, int d1, BigInteger n2, int d2)
        {
            var d = Math.Min(d1, d2);
            return ((n1 << (d1 - d)) + (n2 << (d2 - d)), d);
        }

        /// <summary>
        /// Rounds n * 2^d to the nearest binary32 at half-even, infinities
        /// included, and never produces NaN.
        /// </summary>
        private static float Round(BigInteger n, int d)
        {
            if (n.IsZero)
            {
                return 0f;
            }

            var negative = n.Sign < 0;
            var abs = BigInteger.Abs(n);
            var exponent = ExactUvGeometry.BitLength(abs) - 1 + d;

            if (exponent > 127)
            {
                return negative
                    ? float.NegativeInfinity
                    : float.PositiveInfinity;
            }

            BigInteger mantissa;
            if (exponent >= -126)
            {
                // Normal: 24 significant bits, implicit leading one.
                var shift = ExactUvGeometry.BitLength(abs) - 24;
                if (shift > 0)
                {
                    mantissa = abs >> shift;
                    var remainder = abs - (mantissa << shift);
                    var half = BigInteger.One << (shift - 1);
                    if (remainder > half ||
                        (remainder == half && !mantissa.IsEven))
                    {
                        mantissa += 1;
                    }
                }
                else
                {
                    mantissa = abs << -shift;
                    shift = -shift;
                }

                if (mantissa == (BigInteger.One << 24))
                {
                    mantissa >>= 1;
                    exponent += 1;
                    if (exponent > 127)
                    {
                        return negative
                            ? float.NegativeInfinity
                            : float.PositiveInfinity;
                    }
                }

                var bits = (int)((exponent + 127) << 23) |
                           (int)(mantissa & 0x007FFFFF);
                return Int32BitsToSingle(bits, negative);
            }

            // Subnormal: grid spacing 2^-149.
            var subShift = 149 + d;
            if (subShift >= 0)
            {
                mantissa = abs << subShift;
            }
            else
            {
                mantissa = abs >> -subShift;
                var remainder = abs - (mantissa << -subShift);
                var half = BigInteger.One << (-subShift - 1);
                if (remainder > half ||
                    (remainder == half && !mantissa.IsEven))
                {
                    mantissa += 1;
                }
            }

            if (mantissa == 0)
            {
                return 0f;
            }

            if (mantissa >= (BigInteger.One << 23))
            {
                // Rounded up into the smallest normal.
                var bits = (int)(1 << 23) | (int)((mantissa - (BigInteger.One << 23)) & 0x007FFFFF);
                return Int32BitsToSingle(bits, negative);
            }

            return Int32BitsToSingle((int)mantissa, negative);
        }

        private static float Int32BitsToSingle(int bits, bool negative)
        {
            var result = BitConverter.Int32BitsToSingle(bits);
            return negative ? -result : result;
        }

        private static float Saturate(float value)
        {
            return value <= 0f ? 0f : value >= 1f ? 1f : value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
