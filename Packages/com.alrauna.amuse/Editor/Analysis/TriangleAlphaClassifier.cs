using System;
using System.Collections.Generic;
using UnityEngine;
using BigInteger = System.Numerics.BigInteger;

namespace Alrauna.Amuse.Editor.Analysis
{
    internal enum TriangleAlphaOutcome
    {
        ProvenOpaque,
        MustRemainTransparent,
        Unknown
    }

    internal enum AlphaFilterMode
    {
        Point,
        Bilinear,

        /// <summary>
        /// Bilinear within the selected level plus a blend of the two adjacent
        /// selected levels. The within-level footprint model is identical to
        /// bilinear; the between-level blend is monotone, so the mip-chain
        /// conjunction - which proves every level - supplies the blend's two
        /// operands and the classification carries over.
        /// </summary>
        Trilinear
    }

    internal enum AlphaWrapMode
    {
        Clamp,
        Repeat
    }

    /// <summary>
    /// Whether the sample averages an anisotropic footprint. An anisotropic
    /// footprint is unmodeled, so an anisotropic sample classifies only
    /// through a level's fully-opaque fast path; anywhere else it stays
    /// unknown.
    /// </summary>
    internal enum AlphaAnisoMode
    {
        None,
        Anisotropic
    }

    internal readonly struct AlphaSamplingSettings
    {
        private const AlphaAnisoMode DefaultAniso = AlphaAnisoMode.None;

        internal AlphaFilterMode FilterMode { get; }
        internal AlphaWrapMode WrapMode { get; }
        internal AlphaAnisoMode AnisoMode { get; }

        /// <summary>
        /// The common sampling shape: no anisotropy.
        /// </summary>
        internal AlphaSamplingSettings(
            AlphaFilterMode filterMode,
            AlphaWrapMode wrapMode)
            : this(filterMode, wrapMode, DefaultAniso)
        {
        }

        internal AlphaSamplingSettings(
            AlphaFilterMode filterMode,
            AlphaWrapMode wrapMode,
            AlphaAnisoMode anisoMode)
        {
            FilterMode = filterMode;
            WrapMode = wrapMode;
            AnisoMode = anisoMode;
        }
    }

    internal readonly struct TriangleAlphaInput
    {
        internal Vector3 Position0 { get; }
        internal Vector3 Position1 { get; }
        internal Vector3 Position2 { get; }
        internal bool HasUv0 { get; }
        internal Vector2 Uv0 { get; }
        internal Vector2 Uv1 { get; }
        internal Vector2 Uv2 { get; }

        private TriangleAlphaInput(
            Vector3 position0,
            Vector3 position1,
            Vector3 position2,
            bool hasUv0,
            Vector2 uv0,
            Vector2 uv1,
            Vector2 uv2)
        {
            Position0 = position0;
            Position1 = position1;
            Position2 = position2;
            HasUv0 = hasUv0;
            Uv0 = uv0;
            Uv1 = uv1;
            Uv2 = uv2;
        }

        internal static TriangleAlphaInput WithUv0(
            Vector3 position0,
            Vector3 position1,
            Vector3 position2,
            Vector2 uv0,
            Vector2 uv1,
            Vector2 uv2)
        {
            return new TriangleAlphaInput(
                position0,
                position1,
                position2,
                true,
                uv0,
                uv1,
                uv2);
        }

        internal static TriangleAlphaInput MissingUv0(
            Vector3 position0,
            Vector3 position1,
            Vector3 position2)
        {
            return new TriangleAlphaInput(
                position0,
                position1,
                position2,
                false,
                default,
                default,
                default);
        }
    }

    internal sealed class AlphaTextureData
    {
        private readonly byte[] _alpha8;

        /// <summary>
        /// The stored value for an erased texel: noise under the user's
        /// gate. Capture writes only 255, 0, and this flag, so a fixture
        /// that stores 1 by hand must not expect erasure unless the
        /// classifier runs with an active density policy.
        /// </summary>
        internal const byte ErasedFlag = 1;

        internal int Width { get; }
        internal int Height { get; }
        internal bool IsFullyOpaque { get; private set; }
        internal bool IsFullyNonOpaque { get; private set; }

        internal AlphaTextureData(
            int width,
            int height,
            IReadOnlyList<byte> alpha8BottomToTop)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }
            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }
            if (alpha8BottomToTop == null)
            {
                throw new ArgumentNullException(nameof(alpha8BottomToTop));
            }
            if ((long)width * height != alpha8BottomToTop.Count)
            {
                throw new ArgumentException(
                    "Alpha data length must equal width times height.",
                    nameof(alpha8BottomToTop));
            }

            Width = width;
            Height = height;
            _alpha8 = new byte[alpha8BottomToTop.Count];
            IsFullyOpaque = true;
            IsFullyNonOpaque = true;
            for (var index = 0; index < _alpha8.Length; index++)
            {
                var alpha = alpha8BottomToTop[index];
                _alpha8[index] = alpha;
                if (alpha != byte.MaxValue)
                {
                    IsFullyOpaque = false;
                }
                else
                {
                    IsFullyNonOpaque = false;
                }
            }
        }

        internal byte GetAlpha(int x, int y)
        {
            if (x < 0 || x >= Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x));
            }
            if (y < 0 || y >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(y));
            }

            return _alpha8[y * Width + x];
        }
    }

    internal static class TriangleAlphaClassifier
    {
        internal const int MaxSupportRegions = 65536;

        internal static TriangleAlphaOutcome Classify(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaSamplingSettings sampling,
            AlphaUvEnvelope envelope)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }
            ValidateSampling(sampling);
            ValidateFinite(triangle.Position0, nameof(triangle.Position0));
            ValidateFinite(triangle.Position1, nameof(triangle.Position1));
            ValidateFinite(triangle.Position2, nameof(triangle.Position2));

            if (ExactUvGeometry.IsDegenerateGeometry(triangle))
            {
                return TriangleAlphaOutcome.Unknown;
            }
            if (!triangle.HasUv0)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            ValidateFinite(triangle.Uv0, nameof(triangle.Uv0));
            ValidateFinite(triangle.Uv1, nameof(triangle.Uv1));
            ValidateFinite(triangle.Uv2, nameof(triangle.Uv2));
            if (texture.IsFullyOpaque)
            {
                return TriangleAlphaOutcome.ProvenOpaque;
            }
            if (texture.IsFullyNonOpaque)
            {
                return TriangleAlphaOutcome.MustRemainTransparent;
            }

            // Anisotropy averages an elongated footprint that no per-filter
            // envelope model bounds, so it must be decided before any filter
            // dispatch: a level that is neither fully opaque nor fully
            // non-opaque - the fast paths above answered those - stays
            // unknown, and the chain conjunction then admits an anisotropic
            // triangle only when every level is fully opaque, which is
            // exactly the anisotropic soundness condition.
            if (sampling.AnisoMode == AlphaAnisoMode.Anisotropic)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            if (sampling.FilterMode == AlphaFilterMode.Point &&
                sampling.WrapMode == AlphaWrapMode.Clamp)
            {
                return ClassifyPointClamp(triangle, texture, envelope);
            }
            if (sampling.FilterMode == AlphaFilterMode.Point &&
                sampling.WrapMode == AlphaWrapMode.Repeat)
            {
                return ClassifyPointRepeat(triangle, texture, envelope);
            }
            if (sampling.FilterMode == AlphaFilterMode.Bilinear &&
                sampling.WrapMode == AlphaWrapMode.Clamp)
            {
                return ClassifyBilinearClamp(triangle, texture, envelope);
            }
            if (sampling.FilterMode == AlphaFilterMode.Bilinear &&
                sampling.WrapMode == AlphaWrapMode.Repeat)
            {
                return ClassifyBilinearRepeat(triangle, texture, envelope);
            }

            // Trilinear's within-level footprint is the bilinear one; the
            // between-level blend of the two adjacent selected levels is
            // monotone, and the chain conjunction in the resolver proves both
            // operands, so the bilinear per-level verdict carries over.
            if (sampling.FilterMode == AlphaFilterMode.Trilinear &&
                sampling.WrapMode == AlphaWrapMode.Clamp)
            {
                return ClassifyBilinearClamp(triangle, texture, envelope);
            }
            if (sampling.FilterMode == AlphaFilterMode.Trilinear &&
                sampling.WrapMode == AlphaWrapMode.Repeat)
            {
                return ClassifyBilinearRepeat(triangle, texture, envelope);
            }

            return TriangleAlphaOutcome.Unknown;
        }

        private static TriangleAlphaOutcome ClassifyPointClamp(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope)
        {
            var domain = ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope);
            var minimumX = PointClampIndex(
                ExactUvGeometry.Minimum(domain, true),
                texture.Width,
                domain.TexelScale);
            var maximumX = PointClampIndex(
                ExactUvGeometry.Maximum(domain, true),
                texture.Width,
                domain.TexelScale);
            var minimumY = PointClampIndex(
                ExactUvGeometry.Minimum(domain, false),
                texture.Height,
                domain.TexelScale);
            var maximumY = PointClampIndex(
                ExactUvGeometry.Maximum(domain, false),
                texture.Height,
                domain.TexelScale);
            var candidateCount = (long)(maximumX - minimumX + 1) *
                                 (maximumY - minimumY + 1);
            if (candidateCount > MaxSupportRegions)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            for (var y = minimumY; y <= maximumY; y++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                {
                    if (texture.GetAlpha(x, y) == byte.MaxValue)
                    {
                        continue;
                    }
                    if (ExactUvGeometry.Intersects(
                        domain,
                        PointClampInterval(x, texture.Width, domain.TexelScale),
                        PointClampInterval(y, texture.Height, domain.TexelScale)))
                    {
                        return TriangleAlphaOutcome.MustRemainTransparent;
                    }
                }
            }
            return TriangleAlphaOutcome.ProvenOpaque;
        }

        private static TriangleAlphaOutcome ClassifyBilinearRepeat(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope)
        {
            var domain = ExactUvGeometry.NormalizeRepeat(
                ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope),
                texture.Width,
                texture.Height);
            if (!TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, true),
                    domain.TexelScale,
                    out var minCellX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, true),
                    domain.TexelScale,
                    out var maxCellX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, false),
                    domain.TexelScale,
                    out var minCellY) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, false),
                    domain.TexelScale,
                    out var maxCellY))
            {
                return TriangleAlphaOutcome.Unknown;
            }

            if (minCellX <= int.MinValue || maxCellX >= int.MaxValue ||
                minCellY <= int.MinValue || maxCellY >= int.MaxValue)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            var minimumX = minCellX - 1;
            var maximumX = maxCellX + 1;
            var minimumY = minCellY - 1;
            var maximumY = maxCellY + 1;
            var candidateCount = (long)(maximumX - minimumX + 1) *
                                 (maximumY - minimumY + 1);
            if (candidateCount > MaxSupportRegions)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            var canPreFilter = domain.Vertices.Count == 3;
            double v0x = 0, v0y = 0, v1x = 0, v1y = 0, v2x = 0, v2y = 0;
            if (canPreFilter)
            {
                ExtractTexelVertices(domain, out v0x, out v0y, out v1x, out v1y, out v2x, out v2y);
            }

            for (var unwrappedY = minimumY; unwrappedY <= maximumY; unwrappedY++)
            {
                var y = ExactUvGeometry.FloorMod(unwrappedY, texture.Height);
                for (var unwrappedX = minimumX; unwrappedX <= maximumX; unwrappedX++)
                {
                    var x = ExactUvGeometry.FloorMod(unwrappedX, texture.Width);
                    if (texture.GetAlpha(x, y) == byte.MaxValue)
                    {
                        continue;
                    }
                    if (canPreFilter && !ConservativeBilinearSupportOverlapsTriangle(
                            unwrappedX - 0.5, unwrappedX + 1.5,
                            unwrappedY - 0.5, unwrappedY + 1.5,
                            v0x, v0y, v1x, v1y, v2x, v2y))
                    {
                        continue;
                    }
                    if (ExactUvGeometry.Intersects(
                        domain,
                        BilinearRepeatInterval(unwrappedX, domain.TexelScale),
                        BilinearRepeatInterval(unwrappedY, domain.TexelScale)))
                    {
                        return TriangleAlphaOutcome.MustRemainTransparent;
                    }
                }
            }
            return TriangleAlphaOutcome.ProvenOpaque;
        }

        private static ExactInterval BilinearRepeatInterval(
            int index,
            BigInteger texelScale)
        {
            var halfTexel = texelScale / 2;
            var center = new BigInteger(index) * texelScale;
            return new ExactInterval(
                true,
                new ExactRational(center - halfTexel),
                false,
                true,
                new ExactRational(center + 3 * halfTexel),
                false);
        }

        private static TriangleAlphaOutcome ClassifyBilinearClamp(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope)
        {
            var domain = ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope);
            var minimumX = Math.Max(0, PointClampIndex(
                ExactUvGeometry.Minimum(domain, true),
                texture.Width,
                domain.TexelScale) - 1);
            var maximumX = Math.Min(texture.Width - 1, PointClampIndex(
                ExactUvGeometry.Maximum(domain, true),
                texture.Width,
                domain.TexelScale) + 1);
            var minimumY = Math.Max(0, PointClampIndex(
                ExactUvGeometry.Minimum(domain, false),
                texture.Height,
                domain.TexelScale) - 1);
            var maximumY = Math.Min(texture.Height - 1, PointClampIndex(
                ExactUvGeometry.Maximum(domain, false),
                texture.Height,
                domain.TexelScale) + 1);
            var candidateCount = (long)(maximumX - minimumX + 1) *
                                 (maximumY - minimumY + 1);
            if (candidateCount > MaxSupportRegions)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            var canPreFilter = domain.Vertices.Count == 3;
            double v0x = 0, v0y = 0, v1x = 0, v1y = 0, v2x = 0, v2y = 0;
            if (canPreFilter)
            {
                ExtractTexelVertices(domain, out v0x, out v0y, out v1x, out v1y, out v2x, out v2y);
            }

            for (var y = minimumY; y <= maximumY; y++)
            {
                for (var x = minimumX; x <= maximumX; x++)
                {
                    if (texture.GetAlpha(x, y) == byte.MaxValue)
                    {
                        continue;
                    }
                    if (canPreFilter)
                    {
                        var isBoundary = texture.Width == 1 || texture.Height == 1 ||
                                         (x == 0 && (v0x < -0.5 || v1x < -0.5 || v2x < -0.5)) ||
                                         (x == texture.Width - 1 && (v0x > texture.Width - 0.5 || v1x > texture.Width - 0.5 || v2x > texture.Width - 0.5)) ||
                                         (y == 0 && (v0y < -0.5 || v1y < -0.5 || v2y < -0.5)) ||
                                         (y == texture.Height - 1 && (v0y > texture.Height - 0.5 || v1y > texture.Height - 0.5 || v2y > texture.Height - 0.5));
                        if (!isBoundary && !ConservativeBilinearSupportOverlapsTriangle(
                                x - 0.5, x + 1.5,
                                y - 0.5, y + 1.5,
                                v0x, v0y, v1x, v1y, v2x, v2y))
                        {
                            continue;
                        }
                    }
                    if (ExactUvGeometry.Intersects(
                        domain,
                        BilinearClampInterval(x, texture.Width, domain.TexelScale),
                        BilinearClampInterval(y, texture.Height, domain.TexelScale)))
                    {
                        return TriangleAlphaOutcome.MustRemainTransparent;
                    }
                }
            }
            return TriangleAlphaOutcome.ProvenOpaque;
        }

        private static ExactInterval BilinearClampInterval(
            int index,
            int size,
            BigInteger texelScale)
        {
            if (size == 1)
            {
                return new ExactInterval(
                    false,
                    default,
                    false,
                    false,
                    default,
                    false);
            }

            var halfTexel = texelScale / 2;
            if (index == 0)
            {
                return new ExactInterval(
                    false,
                    default,
                    false,
                    true,
                    new ExactRational(3 * halfTexel),
                    false);
            }

            var center = new BigInteger(index) * texelScale;
            if (index == size - 1)
            {
                return new ExactInterval(
                    true,
                    new ExactRational(center - halfTexel),
                    false,
                    false,
                    default,
                    false);
            }
            return new ExactInterval(
                true,
                new ExactRational(center - halfTexel),
                false,
                true,
                new ExactRational(center + 3 * halfTexel),
                false);
        }

        /// <summary>
        /// Conservatively tests whether the bilinear reconstruction support box
        /// can intersect the triangle. Returns false only when the shapes are
        /// definitely separated. Returns true if they overlap or are within
        /// numerical tolerance.
        /// </summary>
        internal static bool ConservativeBilinearSupportOverlapsTriangle(
            double boxMinX, double boxMaxX,
            double boxMinY, double boxMaxY,
            double v0x, double v0y,
            double v1x, double v1y,
            double v2x, double v2y)
        {
            // Axis 1 and 2: AABB check.
            var triMinX = Math.Min(v0x, Math.Min(v1x, v2x));
            var triMaxX = Math.Max(v0x, Math.Max(v1x, v2x));
            if (boxMaxX < triMinX || boxMinX > triMaxX)
            {
                return false;
            }

            var triMinY = Math.Min(v0y, Math.Min(v1y, v2y));
            var triMaxY = Math.Max(v0y, Math.Max(v1y, v2y));
            if (boxMaxY < triMinY || boxMinY > triMaxY)
            {
                return false;
            }

            // Axis 3, 4, 5: Triangle edge normal half planes.
            if (EdgeSeparates(v0x, v0y, v1x, v1y, v2x, v2y, boxMinX, boxMaxX, boxMinY, boxMaxY))
            {
                return false;
            }
            if (EdgeSeparates(v1x, v1y, v2x, v2y, v0x, v0y, boxMinX, boxMaxX, boxMinY, boxMaxY))
            {
                return false;
            }
            if (EdgeSeparates(v2x, v2y, v0x, v0y, v1x, v1y, boxMinX, boxMaxX, boxMinY, boxMaxY))
            {
                return false;
            }

            return true;
        }

        private static bool EdgeSeparates(
            double aX, double aY,
            double bX, double bY,
            double cX, double cY,
            double boxMinX, double boxMaxX,
            double boxMinY, double boxMaxY)
        {
            var nx = -(bY - aY);
            var ny = bX - aX;

            // Sign of third triangle vertex.
            var cDot = nx * (cX - aX) + ny * (cY - aY);
            if (Math.Abs(cDot) < 1e-12)
            {
                return false;
            }

            // Find the box vertex that extends farthest toward the triangle interior.
            // If that extreme box vertex is outside, the whole box is outside.
            var extremeX = cDot > 0 ? (nx >= 0 ? boxMaxX : boxMinX) : (nx >= 0 ? boxMinX : boxMaxX);
            var extremeY = cDot > 0 ? (ny >= 0 ? boxMaxY : boxMinY) : (ny >= 0 ? boxMinY : boxMaxY);

            var extremeDot = nx * (extremeX - aX) + ny * (extremeY - aY);
            if (cDot > 0)
            {
                return extremeDot < -1e-9;
            }

            return extremeDot > 1e-9;
        }

        private static void ExtractTexelVertices(
            ExactUvDomain domain,
            out double v0x, out double v0y,
            out double v1x, out double v1y,
            out double v2x, out double v2y)
        {
            var scale = (double)domain.TexelScale;
            var pt0 = domain.Vertices[0];
            var pt1 = domain.Vertices[1];
            var pt2 = domain.Vertices[2];
            v0x = (double)pt0.X.Numerator / (double)pt0.X.Denominator / scale;
            v0y = (double)pt0.Y.Numerator / (double)pt0.Y.Denominator / scale;
            v1x = (double)pt1.X.Numerator / (double)pt1.X.Denominator / scale;
            v1y = (double)pt1.Y.Numerator / (double)pt1.Y.Denominator / scale;
            v2x = (double)pt2.X.Numerator / (double)pt2.X.Denominator / scale;
            v2y = (double)pt2.Y.Numerator / (double)pt2.Y.Denominator / scale;
        }

        private static TriangleAlphaOutcome ClassifyPointRepeat(
            TriangleAlphaInput triangle,
            AlphaTextureData texture,
            AlphaUvEnvelope envelope)
        {
            var domain = ExactUvGeometry.NormalizeRepeat(
                ExactUvGeometry.CreateTextureScaledDomain(triangle, texture.Width, texture.Height, envelope),
                texture.Width,
                texture.Height);
            if (!TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, true),
                    domain.TexelScale,
                    out var minimumX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, true),
                    domain.TexelScale,
                    out var maximumX) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Minimum(domain, false),
                    domain.TexelScale,
                    out var minimumY) ||
                !TryGetCellIndex(
                    ExactUvGeometry.Maximum(domain, false),
                    domain.TexelScale,
                    out var maximumY))
            {
                return TriangleAlphaOutcome.Unknown;
            }

            var candidateCount = (long)(maximumX - minimumX + 1) *
                                 (maximumY - minimumY + 1);
            if (candidateCount > MaxSupportRegions)
            {
                return TriangleAlphaOutcome.Unknown;
            }

            for (var unwrappedY = minimumY; unwrappedY <= maximumY; unwrappedY++)
            {
                var y = ExactUvGeometry.FloorMod(unwrappedY, texture.Height);
                for (var unwrappedX = minimumX; unwrappedX <= maximumX; unwrappedX++)
                {
                    var x = ExactUvGeometry.FloorMod(unwrappedX, texture.Width);
                    if (texture.GetAlpha(x, y) == byte.MaxValue)
                    {
                        continue;
                    }
                    if (ExactUvGeometry.Intersects(
                        domain,
                        PointRepeatInterval(unwrappedX, domain.TexelScale),
                        PointRepeatInterval(unwrappedY, domain.TexelScale)))
                    {
                        return TriangleAlphaOutcome.MustRemainTransparent;
                    }
                }
            }
            return TriangleAlphaOutcome.ProvenOpaque;
        }

        private static bool TryGetCellIndex(
            ExactRational coordinate,
            BigInteger texelScale,
            out int index)
        {
            var div = ExactUvGeometry.FloorDiv(
                coordinate.Numerator,
                coordinate.Denominator * texelScale);
            if (div < int.MinValue || div > int.MaxValue)
            {
                index = 0;
                return false;
            }
            index = (int)div;
            return true;
        }

        private static ExactInterval PointRepeatInterval(
            int index,
            BigInteger texelScale)
        {
            var center = new BigInteger(index) * texelScale;
            return new ExactInterval(
                true,
                new ExactRational(center),
                true,
                true,
                new ExactRational(center + texelScale),
                false);
        }

        private static int PointClampIndex(
            ExactRational coordinate,
            int size,
            BigInteger texelScale)
        {
            if (coordinate.CompareTo(new ExactRational(BigInteger.Zero)) <= 0)
            {
                return 0;
            }
            if (coordinate.CompareTo(new ExactRational(size * texelScale)) >= 0)
            {
                return size - 1;
            }
            return (int)(coordinate.Numerator /
                (coordinate.Denominator * texelScale));
        }

        private static ExactInterval PointClampInterval(
            int index,
            int size,
            BigInteger texelScale)
        {
            if (size == 1)
            {
                return new ExactInterval(
                    false,
                    default,
                    false,
                    false,
                    default,
                    false);
            }
            if (index == 0)
            {
                return new ExactInterval(
                    false,
                    default,
                    false,
                    true,
                    new ExactRational(texelScale),
                    false);
            }
            if (index == size - 1)
            {
                return new ExactInterval(
                    true,
                    new ExactRational(index * texelScale),
                    true,
                    false,
                    default,
                    false);
            }
            return new ExactInterval(
                true,
                new ExactRational(index * texelScale),
                true,
                true,
                new ExactRational((index + 1) * texelScale),
                false);
        }

        private static void ValidateSampling(AlphaSamplingSettings sampling)
        {
            if (sampling.FilterMode != AlphaFilterMode.Point &&
                sampling.FilterMode != AlphaFilterMode.Bilinear &&
                sampling.FilterMode != AlphaFilterMode.Trilinear)
            {
                throw new ArgumentOutOfRangeException(nameof(sampling));
            }
            if (sampling.WrapMode != AlphaWrapMode.Clamp &&
                sampling.WrapMode != AlphaWrapMode.Repeat)
            {
                throw new ArgumentOutOfRangeException(nameof(sampling));
            }
            if (!Enum.IsDefined(typeof(AlphaAnisoMode), sampling.AnisoMode))
            {
                throw new ArgumentOutOfRangeException(nameof(sampling));
            }
        }

        private static void ValidateFinite(Vector3 value, string parameterName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
            {
                throw new ArgumentException("Geometry positions must be finite.", parameterName);
            }
        }

        private static void ValidateFinite(Vector2 value, string parameterName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y))
            {
                throw new ArgumentException("UV0 values must be finite.", parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
