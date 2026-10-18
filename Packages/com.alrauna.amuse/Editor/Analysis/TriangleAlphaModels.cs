using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Analysis
{
    internal enum TriangleAlphaOutcome
    {
        ProvenOpaque,
        MustRemainTransparent,
        Unknown
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

        private readonly IReadOnlyList<IReadOnlyList<Vector2>> _extraUvSets;
        private readonly int _indexA;
        private readonly int _indexB;
        private readonly int _indexC;

        private TriangleAlphaInput(
            Vector3 position0,
            Vector3 position1,
            Vector3 position2,
            bool hasUv0,
            Vector2 uv0,
            Vector2 uv1,
            Vector2 uv2,
            IReadOnlyList<IReadOnlyList<Vector2>> extraUvSets,
            int indexA,
            int indexB,
            int indexC)
        {
            Position0 = position0;
            Position1 = position1;
            Position2 = position2;
            HasUv0 = hasUv0;
            Uv0 = uv0;
            Uv1 = uv1;
            Uv2 = uv2;
            _extraUvSets = extraUvSets;
            _indexA = indexA;
            _indexB = indexB;
            _indexC = indexC;
        }

        /// <summary>
        /// Selects one UV channel's three vertex coordinates. Channel zero is
        /// the copied uv0 triplet; channels one to three read the extra sets
        /// at this triangle's vertex indices. A channel the mesh does not
        /// carry answers false, which the resolution classifies as Unknown.
        /// </summary>
        internal bool TryGetUvSet(
            int channel,
            out Vector2 a,
            out Vector2 b,
            out Vector2 c)
        {
            if (channel == 0)
            {
                if (!HasUv0)
                {
                    a = default;
                    b = default;
                    c = default;
                    return false;
                }

                a = Uv0;
                b = Uv1;
                c = Uv2;
                if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
                {
                    a = default;
                    b = default;
                    c = default;
                    return false;
                }

                return true;
            }

            var index = channel - 1;
            if (_extraUvSets == null ||
                index < 0 ||
                index >= _extraUvSets.Count ||
                _extraUvSets[index] == null)
            {
                a = default;
                b = default;
                c = default;
                return false;
            }

            var uvList = _extraUvSets[index];
            if ((uint)_indexA >= (uint)uvList.Count ||
                (uint)_indexB >= (uint)uvList.Count ||
                (uint)_indexC >= (uint)uvList.Count)
            {
                a = default;
                b = default;
                c = default;
                return false;
            }

            a = uvList[_indexA];
            b = uvList[_indexB];
            c = uvList[_indexC];
            if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
            {
                a = default;
                b = default;
                c = default;
                return false;
            }

            return true;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y);
        }

        /// <summary>
        /// Attaches the mesh's extra UV channel arrays and this triangle's
        /// vertex indices, so a layer mapping naming uv1 to uv3 selects its
        /// channel without copying. The arrays are the snapshot's own; the
        /// input never mutates them.
        /// </summary>
        internal TriangleAlphaInput WithChannels(
            IReadOnlyList<IReadOnlyList<Vector2>> extraUvSets,
            int indexA,
            int indexB,
            int indexC)
        {
            return new TriangleAlphaInput(
                Position0, Position1, Position2,
                HasUv0, Uv0, Uv1, Uv2,
                extraUvSets, indexA, indexB, indexC);
        }

        private TriangleAlphaInput(
            Vector3 position0,
            Vector3 position1,
            Vector3 position2,
            bool hasUv0,
            Vector2 uv0,
            Vector2 uv1,
            Vector2 uv2)
            : this(
                position0, position1, position2,
                hasUv0, uv0, uv1, uv2,
                null, 0, 0, 0)
        {
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
}
