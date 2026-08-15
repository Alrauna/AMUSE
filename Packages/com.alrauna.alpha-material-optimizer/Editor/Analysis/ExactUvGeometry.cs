using System;
using System.Collections.Generic;
using System.Numerics;

namespace Alrauna.AlphaMaterialOptimizer.Editor.Analysis
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
    }

    internal readonly struct ExactUvPoint
    {
        internal ExactRational X { get; }
        internal ExactRational Y { get; }

        internal ExactUvPoint(ExactRational x, ExactRational y)
        {
            X = x;
            Y = y;
        }
    }

    internal sealed class ExactUvDomain
    {
        internal IReadOnlyList<ExactUvPoint> Vertices { get; }
        internal BigInteger TexelScale { get; }

        internal ExactUvDomain(
            IReadOnlyList<ExactUvPoint> vertices,
            BigInteger texelScale)
        {
            Vertices = vertices;
            TexelScale = texelScale;
        }
    }

    internal readonly struct ExactInterval
    {
        internal bool HasLowerBound { get; }
        internal ExactRational LowerBound { get; }
        internal bool IsLowerInclusive { get; }
        internal bool HasUpperBound { get; }
        internal ExactRational UpperBound { get; }
        internal bool IsUpperInclusive { get; }

        internal ExactInterval(
            bool hasLowerBound,
            ExactRational lowerBound,
            bool isLowerInclusive,
            bool hasUpperBound,
            ExactRational upperBound,
            bool isUpperInclusive)
        {
            HasLowerBound = hasLowerBound;
            LowerBound = lowerBound;
            IsLowerInclusive = isLowerInclusive;
            HasUpperBound = hasUpperBound;
            UpperBound = upperBound;
            IsUpperInclusive = isUpperInclusive;
        }
    }

    internal static class ExactUvGeometry
    {
        internal static ExactDyadic DecodeFloat(float value)
        {
            var bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            var exponentBits = (bits >> 23) & 0xff;
            var fractionBits = bits & 0x7fffff;

            if (exponentBits == 0xff)
            {
                throw new ArgumentException("Value must be finite.", nameof(value));
            }
            if (exponentBits == 0 && fractionBits == 0)
            {
                return new ExactDyadic(BigInteger.Zero, 0);
            }

            BigInteger significand;
            int exponent;
            if (exponentBits == 0)
            {
                significand = fractionBits;
                exponent = -149;
            }
            else
            {
                significand = (1 << 23) | fractionBits;
                exponent = exponentBits - 150;
            }

            if (bits < 0)
            {
                significand = -significand;
            }
            while (significand.IsEven)
            {
                significand >>= 1;
                exponent++;
            }

            return new ExactDyadic(significand, exponent);
        }

        internal static bool IsDegenerateGeometry(TriangleAlphaInput triangle)
        {
            var x = Align(
                DecodeFloat(triangle.Position0.x),
                DecodeFloat(triangle.Position1.x),
                DecodeFloat(triangle.Position2.x));
            var y = Align(
                DecodeFloat(triangle.Position0.y),
                DecodeFloat(triangle.Position1.y),
                DecodeFloat(triangle.Position2.y));
            var z = Align(
                DecodeFloat(triangle.Position0.z),
                DecodeFloat(triangle.Position1.z),
                DecodeFloat(triangle.Position2.z));

            var ax = x[1] - x[0];
            var ay = y[1] - y[0];
            var az = z[1] - z[0];
            var bx = x[2] - x[0];
            var by = y[2] - y[0];
            var bz = z[2] - z[0];

            return ay * bz - az * by == BigInteger.Zero &&
                   az * bx - ax * bz == BigInteger.Zero &&
                   ax * by - ay * bx == BigInteger.Zero;
        }

        private static BigInteger[] Align(
            ExactDyadic first,
            ExactDyadic second,
            ExactDyadic third)
        {
            var exponent = Math.Min(first.Exponent, Math.Min(second.Exponent, third.Exponent));
            return new[]
            {
                first.Significand << (first.Exponent - exponent),
                second.Significand << (second.Exponent - exponent),
                third.Significand << (third.Exponent - exponent)
            };
        }

        internal static ExactUvDomain CreateTextureScaledDomain(
            TriangleAlphaInput triangle,
            int textureWidth,
            int textureHeight)
        {
            var u = new[]
            {
                DecodeFloat(triangle.Uv0.x),
                DecodeFloat(triangle.Uv1.x),
                DecodeFloat(triangle.Uv2.x)
            };
            var v = new[]
            {
                DecodeFloat(triangle.Uv0.y),
                DecodeFloat(triangle.Uv1.y),
                DecodeFloat(triangle.Uv2.y)
            };
            var exponent = -1;
            for (var index = 0; index < 3; index++)
            {
                exponent = Math.Min(exponent, Math.Min(u[index].Exponent, v[index].Exponent));
            }

            var scale = BigInteger.One << -exponent;
            var points = new ExactUvPoint[3];
            for (var index = 0; index < points.Length; index++)
            {
                points[index] = new ExactUvPoint(
                    new ExactRational(
                        u[index].Significand * textureWidth << (u[index].Exponent - exponent)),
                    new ExactRational(
                        v[index].Significand * textureHeight << (v[index].Exponent - exponent)));
            }

            return new ExactUvDomain(CreateHull(points), scale);
        }

        internal static ExactUvDomain NormalizeRepeat(
            ExactUvDomain domain,
            int textureWidth,
            int textureHeight)
        {
            var xPeriod = textureWidth * domain.TexelScale;
            var yPeriod = textureHeight * domain.TexelScale;
            var xOffset = FloorDiv(
                Minimum(domain, true).Numerator,
                Minimum(domain, true).Denominator * xPeriod) * xPeriod;
            var yOffset = FloorDiv(
                Minimum(domain, false).Numerator,
                Minimum(domain, false).Denominator * yPeriod) * yPeriod;
            var vertices = new ExactUvPoint[domain.Vertices.Count];
            for (var index = 0; index < vertices.Length; index++)
            {
                vertices[index] = new ExactUvPoint(
                    ExactRational.Subtract(
                        domain.Vertices[index].X,
                        new ExactRational(xOffset)),
                    ExactRational.Subtract(
                        domain.Vertices[index].Y,
                        new ExactRational(yOffset)));
            }
            return new ExactUvDomain(vertices, domain.TexelScale);
        }

        internal static bool Intersects(
            ExactUvDomain domain,
            ExactInterval x,
            ExactInterval y)
        {
            var vertices = new List<ExactUvPoint>(domain.Vertices);
            if (x.HasLowerBound)
            {
                vertices = Clip(vertices, true, x.LowerBound, true);
            }
            if (x.HasUpperBound)
            {
                vertices = Clip(vertices, true, x.UpperBound, false);
            }
            if (y.HasLowerBound)
            {
                vertices = Clip(vertices, false, y.LowerBound, true);
            }
            if (y.HasUpperBound)
            {
                vertices = Clip(vertices, false, y.UpperBound, false);
            }
            if (vertices.Count == 0)
            {
                return false;
            }

            return HasOpenSideWitness(vertices, true, x, true) &&
                   HasOpenSideWitness(vertices, true, x, false) &&
                   HasOpenSideWitness(vertices, false, y, true) &&
                   HasOpenSideWitness(vertices, false, y, false);
        }

        internal static ExactRational Minimum(ExactUvDomain domain, bool xAxis)
        {
            var minimum = Coordinate(domain.Vertices[0], xAxis);
            for (var index = 1; index < domain.Vertices.Count; index++)
            {
                var coordinate = Coordinate(domain.Vertices[index], xAxis);
                if (coordinate.CompareTo(minimum) < 0)
                {
                    minimum = coordinate;
                }
            }
            return minimum;
        }

        internal static ExactRational Maximum(ExactUvDomain domain, bool xAxis)
        {
            var maximum = Coordinate(domain.Vertices[0], xAxis);
            for (var index = 1; index < domain.Vertices.Count; index++)
            {
                var coordinate = Coordinate(domain.Vertices[index], xAxis);
                if (coordinate.CompareTo(maximum) > 0)
                {
                    maximum = coordinate;
                }
            }
            return maximum;
        }

        private static IReadOnlyList<ExactUvPoint> CreateHull(ExactUvPoint[] points)
        {
            var unique = new List<ExactUvPoint>(3);
            for (var index = 0; index < points.Length; index++)
            {
                if (!Contains(unique, points[index]))
                {
                    unique.Add(points[index]);
                }
            }
            if (unique.Count < 3)
            {
                return unique;
            }

            var cross = Cross(unique[0], unique[1], unique[2]);
            if (cross == BigInteger.Zero)
            {
                unique.Sort(ComparePoints);
                return new[] { unique[0], unique[2] };
            }
            if (cross < BigInteger.Zero)
            {
                var swap = unique[1];
                unique[1] = unique[2];
                unique[2] = swap;
            }
            return unique;
        }

        private static bool Contains(IReadOnlyList<ExactUvPoint> points, ExactUvPoint point)
        {
            for (var index = 0; index < points.Count; index++)
            {
                if (SamePoint(points[index], point))
                {
                    return true;
                }
            }
            return false;
        }

        private static int ComparePoints(ExactUvPoint left, ExactUvPoint right)
        {
            var x = left.X.CompareTo(right.X);
            return x != 0 ? x : left.Y.CompareTo(right.Y);
        }

        private static BigInteger Cross(ExactUvPoint first, ExactUvPoint second, ExactUvPoint third)
        {
            var ax = second.X.Numerator - first.X.Numerator;
            var ay = second.Y.Numerator - first.Y.Numerator;
            var bx = third.X.Numerator - first.X.Numerator;
            var by = third.Y.Numerator - first.Y.Numerator;
            return ax * by - ay * bx;
        }

        private static List<ExactUvPoint> Clip(
            IReadOnlyList<ExactUvPoint> input,
            bool xAxis,
            ExactRational boundary,
            bool keepGreater)
        {
            var output = new List<ExactUvPoint>();
            if (input.Count == 0)
            {
                return output;
            }

            var previous = input[input.Count - 1];
            var previousInside = IsInside(previous, xAxis, boundary, keepGreater);
            for (var index = 0; index < input.Count; index++)
            {
                var current = input[index];
                var currentInside = IsInside(current, xAxis, boundary, keepGreater);
                if (currentInside)
                {
                    if (!previousInside)
                    {
                        AddDistinct(output, BoundaryIntersection(previous, current, xAxis, boundary));
                    }
                    AddDistinct(output, current);
                }
                else if (previousInside)
                {
                    AddDistinct(output, BoundaryIntersection(previous, current, xAxis, boundary));
                }
                previous = current;
                previousInside = currentInside;
            }
            if (output.Count > 1 && SamePoint(output[0], output[output.Count - 1]))
            {
                output.RemoveAt(output.Count - 1);
            }
            return output;
        }

        private static bool IsInside(
            ExactUvPoint point,
            bool xAxis,
            ExactRational boundary,
            bool keepGreater)
        {
            var comparison = Coordinate(point, xAxis).CompareTo(boundary);
            return keepGreater ? comparison >= 0 : comparison <= 0;
        }

        private static ExactUvPoint BoundaryIntersection(
            ExactUvPoint start,
            ExactUvPoint end,
            bool xAxis,
            ExactRational boundary)
        {
            var startCoordinate = Coordinate(start, xAxis);
            var endCoordinate = Coordinate(end, xAxis);
            var t = ExactRational.Divide(
                ExactRational.Subtract(boundary, startCoordinate),
                ExactRational.Subtract(endCoordinate, startCoordinate));
            if (xAxis)
            {
                return new ExactUvPoint(
                    boundary,
                    ExactRational.Add(
                        start.Y,
                        ExactRational.Multiply(t, ExactRational.Subtract(end.Y, start.Y))));
            }
            return new ExactUvPoint(
                ExactRational.Add(
                    start.X,
                    ExactRational.Multiply(t, ExactRational.Subtract(end.X, start.X))),
                boundary);
        }

        private static bool HasOpenSideWitness(
            IReadOnlyList<ExactUvPoint> vertices,
            bool xAxis,
            ExactInterval interval,
            bool lowerSide)
        {
            var hasBound = lowerSide ? interval.HasLowerBound : interval.HasUpperBound;
            var inclusive = lowerSide ? interval.IsLowerInclusive : interval.IsUpperInclusive;
            if (!hasBound || inclusive)
            {
                return true;
            }

            var boundary = lowerSide ? interval.LowerBound : interval.UpperBound;
            for (var index = 0; index < vertices.Count; index++)
            {
                var comparison = Coordinate(vertices[index], xAxis).CompareTo(boundary);
                if (lowerSide ? comparison > 0 : comparison < 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static ExactRational Coordinate(ExactUvPoint point, bool xAxis)
        {
            return xAxis ? point.X : point.Y;
        }

        private static void AddDistinct(List<ExactUvPoint> points, ExactUvPoint point)
        {
            if (points.Count == 0 || !SamePoint(points[points.Count - 1], point))
            {
                points.Add(point);
            }
        }

        private static bool SamePoint(ExactUvPoint left, ExactUvPoint right)
        {
            return left.X.CompareTo(right.X) == 0 && left.Y.CompareTo(right.Y) == 0;
        }

        internal static BigInteger FloorDiv(BigInteger value, BigInteger divisor)
        {
            if (divisor <= BigInteger.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(divisor));
            }
            var quotient = BigInteger.DivRem(value, divisor, out var remainder);
            return remainder.Sign < 0 ? quotient - BigInteger.One : quotient;
        }

        internal static int FloorMod(BigInteger value, int modulus)
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
