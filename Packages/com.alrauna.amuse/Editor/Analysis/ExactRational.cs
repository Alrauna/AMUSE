using System;
using System.Numerics;

namespace Alrauna.Amuse.Editor.Analysis
{
    internal readonly struct ExactDyadic
    {
        internal BigInteger Significand { get; }
        internal int Exponent { get; }

        internal ExactDyadic(BigInteger significand, int exponent)
        {
            Significand = significand;
            Exponent = exponent;
        }
    }

    internal readonly struct ExactRational : IComparable<ExactRational>
    {
        internal BigInteger Numerator { get; }
        internal BigInteger Denominator { get; }

        internal ExactRational(BigInteger numerator, BigInteger denominator)
        {
            if (denominator == BigInteger.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(denominator));
            }
            if (numerator == BigInteger.Zero)
            {
                Numerator = BigInteger.Zero;
                Denominator = BigInteger.One;
                return;
            }
            if (denominator < BigInteger.Zero)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            Numerator = numerator / divisor;
            Denominator = denominator / divisor;
        }

        internal ExactRational(BigInteger value)
            : this(value, BigInteger.One)
        {
        }

        public int CompareTo(ExactRational other)
        {
            return (Numerator * other.Denominator).CompareTo(
                other.Numerator * Denominator);
        }

        internal static ExactRational Add(ExactRational left, ExactRational right)
        {
            return new ExactRational(
                left.Numerator * right.Denominator + right.Numerator * left.Denominator,
                left.Denominator * right.Denominator);
        }

        internal static ExactRational Subtract(ExactRational left, ExactRational right)
        {
            return new ExactRational(
                left.Numerator * right.Denominator - right.Numerator * left.Denominator,
                left.Denominator * right.Denominator);
        }

        internal static ExactRational Multiply(ExactRational left, ExactRational right)
        {
            return new ExactRational(
                left.Numerator * right.Numerator,
                left.Denominator * right.Denominator);
        }

        internal static ExactRational Divide(ExactRational left, ExactRational right)
        {
            if (right.Numerator == BigInteger.Zero)
            {
                throw new DivideByZeroException();
            }
            return new ExactRational(
                left.Numerator * right.Denominator,
                left.Denominator * right.Numerator);
        }

        /// <summary>The absolute value; the denominator is already positive.</summary>
        internal static ExactRational Abs(ExactRational value)
        {
            return new ExactRational(
                BigInteger.Abs(value.Numerator), value.Denominator);
        }

        /// <summary>The largest of three rationals.</summary>
        internal static ExactRational Maximum(
            ExactRational a,
            ExactRational b,
            ExactRational c)
        {
            return a.CompareTo(b) >= 0 && a.CompareTo(c) >= 0
                ? a
                : b.CompareTo(c) >= 0
                    ? b
                    : c;
        }
    }
}
