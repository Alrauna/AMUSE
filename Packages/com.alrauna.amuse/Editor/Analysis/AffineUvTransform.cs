using System;
using BigInteger = System.Numerics.BigInteger;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Analysis
{
    internal static class AffineUvTransform
    {
        internal static bool TryTransform(
            UvMapping mapping,
            TriangleAlphaInput triangle,
            out TriangleAlphaInput transformed,
            out AlphaUvEnvelope envelope)
        {
            // Identity and missing UV evidence must preserve the input field-for-field.
            if (IsIdentity(mapping) || !triangle.HasUv0)
            {
                transformed = triangle;
                envelope = AlphaUvEnvelope.Zero;
                return true;
            }
            var x0 = ExactUvGeometry.DecodeFloat(triangle.Uv0.x);
            var x1 = ExactUvGeometry.DecodeFloat(triangle.Uv1.x);
            var x2 = ExactUvGeometry.DecodeFloat(triangle.Uv2.x);
            var y0 = ExactUvGeometry.DecodeFloat(triangle.Uv0.y);
            var y1 = ExactUvGeometry.DecodeFloat(triangle.Uv1.y);
            var y2 = ExactUvGeometry.DecodeFloat(triangle.Uv2.y);
            AxisResult x;
            AxisResult y;
            if (!TryTransformAxis(
                    x0,
                    x1,
                    x2,
                    ExactUvGeometry.DecodeFloat(mapping.Scale.x),
                    ExactUvGeometry.DecodeFloat(mapping.Offset.x),
                    out x) ||
                !TryTransformAxis(
                    y0,
                    y1,
                    y2,
                    ExactUvGeometry.DecodeFloat(mapping.Scale.y),
                    ExactUvGeometry.DecodeFloat(mapping.Offset.y),
                    out y))
            {
                transformed = triangle;
                envelope = AlphaUvEnvelope.Zero;
                return false;
            }

            transformed = TriangleAlphaInput.WithUv0(
                triangle.Position0,
                triangle.Position1,
                triangle.Position2,
                new Vector2(x.Value0, y.Value0),
                new Vector2(x.Value1, y.Value1),
                new Vector2(x.Value2, y.Value2));
            envelope = new AlphaUvEnvelope(x.Envelope, y.Envelope);
            return true;
        }

        private static bool TryTransformAxis(
            ExactDyadic c0,
            ExactDyadic c1,
            ExactDyadic c2,
            ExactDyadic scale,
            ExactDyadic offset,
            out AxisResult result)
        {
            var p0 = Multiply(c0, scale);
            var p1 = Multiply(c1, scale);
            var p2 = Multiply(c2, scale);
            var t0 = Add(p0, offset);
            var t1 = Add(p1, offset);
            var t2 = Add(p2, offset);
            // 2^127 and larger can reach infinity at runtime, so no finite envelope is sound.
            if (AtOrAboveOverflow(p0) ||
                AtOrAboveOverflow(p1) ||
                AtOrAboveOverflow(p2) ||
                AtOrAboveOverflow(t0) ||
                AtOrAboveOverflow(t1) ||
                AtOrAboveOverflow(t2))
            {
                result = default;
                return false;
            }

            // E1, E2, and E3 are the three Lemma P exact arms.
            var exact = IsE1(c0, c1, c2, scale, offset, t0, t1, t2) ||
                        IsE2(scale, offset) ||
                        IsE3(c0, c1, c2, scale, offset, p0, t0);
            var e0 = ExactUvGeometry.EncodeToNearestFloat(ToRational(t0));
            var e1 = ExactUvGeometry.EncodeToNearestFloat(ToRational(t1));
            var e2 = ExactUvGeometry.EncodeToNearestFloat(ToRational(t2));
            result = new AxisResult(
                e0.Value,
                e1.Value,
                e2.Value,
                exact
                    ? new ExactRational(BigInteger.Zero)
                    : Envelope(c0, c1, c2, scale, p0, p1, p2, t0, t1, t2, e0.Error, e1.Error, e2.Error));
            return true;
        }

        private static bool IsIdentity(UvMapping mapping)
        {
            return mapping.Scale.x == 1f &&
                   mapping.Scale.y == 1f &&
                   mapping.Offset.x == 0f &&
                   mapping.Offset.y == 0f;
        }

        private static bool IsE1(
            ExactDyadic c0,
            ExactDyadic c1,
            ExactDyadic c2,
            ExactDyadic scale,
            ExactDyadic offset,
            ExactDyadic t0,
            ExactDyadic t1,
            ExactDyadic t2)
        {
            return offset.Significand == BigInteger.Zero &&
                   BigInteger.Abs(scale.Significand) == BigInteger.One &&
                   IsNormalStrict(scale) &&
                   SameNormalSide(c0, c1, c2) &&
                   SameNormalSide(t0, t1, t2);
        }

        private static bool IsE2(ExactDyadic scale, ExactDyadic offset)
        {
            return scale.Significand == BigInteger.Zero && (offset.Significand == BigInteger.Zero || IsNormalStrict(offset));
        }

        private static bool IsE3(
            ExactDyadic c0,
            ExactDyadic c1,
            ExactDyadic c2,
            ExactDyadic scale,
            ExactDyadic offset,
            ExactDyadic product,
            ExactDyadic sum)
        {
            // Lemma P needs both exact operations; an exact sum cannot repair an inexact product.
            return Same(c0, c1) &&
                   Same(c0, c2) &&
                   NormalOrZero(c0) &&
                   NormalOrZero(scale) &&
                   NormalOrZero(offset) &&
                   ExactUvGeometry.IsExactBinary32(product) &&
                   NormalOrZero(product) &&
                   ExactUvGeometry.IsExactBinary32(sum) &&
                   NormalOrZero(sum) &&
                   !AtOrAboveOverflow(sum);
        }

        // B_enc covers encoding; B_st uses pre-cancellation P; B_daz covers input flushing.
        private static ExactRational Envelope(
            ExactDyadic c0,
            ExactDyadic c1,
            ExactDyadic c2,
            ExactDyadic scale,
            ExactDyadic p0,
            ExactDyadic p1,
            ExactDyadic p2,
            ExactDyadic t0,
            ExactDyadic t1,
            ExactDyadic t2,
            ExactRational error0,
            ExactRational error1,
            ExactRational error2)
        {
            var p = Maximum(Abs(ToRational(p0)), Abs(ToRational(p1)), Abs(ToRational(p2)));
            var m = Maximum(Abs(ToRational(t0)), Abs(ToRational(t1)), Abs(ToRational(t2)));
            var encoding = Maximum(Abs(error0), Abs(error1), Abs(error2));
            var relative = Multiply(new ExactRational(BigInteger.One, BigInteger.One << 22), Add(p, m));
            var flush = new ExactRational(BigInteger.One, BigInteger.One << 125);
            var daz = Multiply(
                new ExactRational(BigInteger.One, BigInteger.One << 126),
                Add(
                    Add(
                        Abs(ToRational(scale)),
                        Maximum(
                            Abs(ToRational(c0)),
                            Abs(ToRational(c1)),
                            Abs(ToRational(c2)))),
                    new ExactRational(BigInteger.One)));
            return Add(Add(encoding, Add(relative, flush)), daz);
        }

        private static bool SameNormalSide(ExactDyadic a, ExactDyadic b, ExactDyadic c)
        {
            return IsNormalStrict(a) &&
                   IsNormalStrict(b) &&
                   IsNormalStrict(c) &&
                   a.Significand.Sign == b.Significand.Sign &&
                   a.Significand.Sign == c.Significand.Sign;
        }

        private static bool NormalOrZero(ExactDyadic value)
        {
            return value.Significand == BigInteger.Zero || IsNormalStrict(value);
        }

        private static bool IsNormalStrict(ExactDyadic value)
        {
            return ExactUvGeometry.IsNormalOrZeroBinary32(value) && value.Significand != BigInteger.Zero && !AtOrAboveOverflow(value);
        }

        private static bool AtOrAboveOverflow(ExactDyadic value)
        {
            if (value.Significand == BigInteger.Zero)
            {
                return false;
            }

            var magnitude = BigInteger.Abs(value.Significand);
            var highest = value.Exponent + BitLength(magnitude) - 1;
            return highest >= 127;
        }

        private static ExactDyadic Multiply(ExactDyadic left, ExactDyadic right)
        {
            return new ExactDyadic(left.Significand * right.Significand, left.Exponent + right.Exponent);
        }

        private static ExactDyadic Add(ExactDyadic left, ExactDyadic right)
        {
            var exponent = Math.Min(left.Exponent, right.Exponent);
            return new ExactDyadic(
                (left.Significand << (left.Exponent - exponent)) +
                (right.Significand << (right.Exponent - exponent)),
                exponent);
        }

        private static bool Same(ExactDyadic left, ExactDyadic right)
        {
            return ToRational(left).CompareTo(ToRational(right)) == 0;
        }

        private static ExactRational ToRational(ExactDyadic value)
        {
            return value.Exponent >= 0
                ? new ExactRational(value.Significand << value.Exponent)
                : new ExactRational(value.Significand, BigInteger.One << -value.Exponent);
        }

        private static ExactRational Add(ExactRational left, ExactRational right)
        {
            return ExactRational.Add(left, right);
        }

        private static ExactRational Multiply(ExactRational left, ExactRational right)
        {
            return ExactRational.Multiply(left, right);
        }

        private static ExactRational Abs(ExactRational value)
        {
            return new ExactRational(BigInteger.Abs(value.Numerator), value.Denominator);
        }

        private static ExactRational Maximum(
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

        private static int BitLength(BigInteger value)
        {
            var length = 0;
            while (value > BigInteger.Zero)
            {
                value >>= 1;
                length++;
            }

            return length;
        }

        private readonly struct AxisResult
        {
            internal AxisResult(
                float value0,
                float value1,
                float value2,
                ExactRational envelope)
            {
                Value0 = value0;
                Value1 = value1;
                Value2 = value2;
                Envelope = envelope;
            }
            internal float Value0 { get; }
            internal float Value1 { get; }
            internal float Value2 { get; }
            internal ExactRational Envelope { get; }
        }
    }
}
